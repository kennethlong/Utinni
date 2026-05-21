/**
 * MIT License
 *
 * Copyright (c) 2020 Philip Klatt
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
**/

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using PluginTemplateWizard;
using Xunit;

namespace UtinniCoreDotNet.Tests
{
    // Phase 3 R-G regression tests (per 03-CONTEXT D-23 + PATTERNS.md §"R-G
    // idempotent merge"). Verifies Props.CreateDotNetDirectoryProps:
    //   1. Creates a fresh Directory.Build.props skeleton with all four
    //      expected PropertyGroups when the file does not exist.
    //   2. Merges Utinni properties into an existing file WITHOUT removing the
    //      user's own PropertyGroups / properties.
    //   3. Is idempotent -- calling twice produces a value-stable result.
    //   4. Updates stale Utinni property values when re-run after an older
    //      wizard had emitted them.
    //   5. Is XXE-safe -- the .NET 4.7.2 default DtdProcessing=Prohibit
    //      rejects a DOCTYPE/entity attack at parse time.
    //
    // Test fixtures use the temp-dir pattern from CppSharpSlnDirTests:55-71.
    // Props.cs is linked-source via UtinniCoreDotNet.Tests.csproj <Compile
    // Include="..\sdk\...\Props.cs" Link="Vsix\Props.cs" /> -- a
    // ProjectReference to the VSIX project is impractical (different
    // ProjectTypeGuid + VSIX-tooling deps).
    public class DirectoryBuildPropsTests
    {
        private static readonly XNamespace Msbuild =
            "http://schemas.microsoft.com/developer/msbuild/2003";

        // --- Fact 1: no existing file -> fresh skeleton with expected groups. ---

        [Fact]
        public void CreateDotNetDirectoryProps_NoExistingFile_CreatesFreshSkeleton()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempRoot);
            string slnPath = tempRoot + Path.DirectorySeparatorChar;
            string filePath = slnPath + Props.DirectoryBuildPropsFilename;

            try
            {
                Assert.False(File.Exists(filePath));

                Props.CreateDotNetDirectoryProps(slnPath);

                Assert.True(File.Exists(filePath));

                XDocument doc = XDocument.Load(filePath);
                XElement project = doc.Element(Msbuild + "Project");
                Assert.NotNull(project);

                var groups = project.Elements(Msbuild + "PropertyGroup").ToList();
                // One unconditional + three configuration-conditional.
                Assert.Equal(4, groups.Count);

                // Unconditional group carries PluginOutputDir +
                // UtinniCoreDotNetPath.
                var unconditional = groups.First(g => g.Attribute("Condition") == null);
                Assert.Equal(
                    @"$(SolutionDir)\bin\$(Configuration)\",
                    unconditional.Element(Msbuild + "PluginOutputDir").Value);
                Assert.Equal(
                    @"..\..\..\bin\Release\",
                    unconditional.Element(Msbuild + "UtinniCoreDotNetPath").Value);

                // RelWithDbgInfo group.
                var relWithDbg = groups.FirstOrDefault(g =>
                    g.Attribute("Condition") != null
                    && g.Attribute("Condition").Value ==
                        "'$(Configuration)|$(Platform)' == 'RelWithDbgInfo|AnyCPU'");
                Assert.NotNull(relWithDbg);
                Assert.Equal("$(PluginOutputDir)", relWithDbg.Element(Msbuild + "OutputPath").Value);
                Assert.Equal("pdbonly", relWithDbg.Element(Msbuild + "DebugType").Value);
                Assert.Equal("MinimumRecommendedRules.ruleset",
                    relWithDbg.Element(Msbuild + "CodeAnalysisRuleSet").Value);

                // Release group.
                var release = groups.FirstOrDefault(g =>
                    g.Attribute("Condition") != null
                    && g.Attribute("Condition").Value ==
                        "'$(Configuration)|$(Platform)' == 'Release|AnyCPU'");
                Assert.NotNull(release);
                Assert.Equal("$(PluginOutputDir)", release.Element(Msbuild + "OutputPath").Value);
                Assert.Equal("4", release.Element(Msbuild + "WarningLevel").Value);

                // Debug group.
                var debug = groups.FirstOrDefault(g =>
                    g.Attribute("Condition") != null
                    && g.Attribute("Condition").Value ==
                        "'$(Configuration)|$(Platform)' == 'Debug|AnyCPU'");
                Assert.NotNull(debug);
                Assert.Equal("full", debug.Element(Msbuild + "DebugType").Value);
                Assert.Equal("DEBUG;TRACE", debug.Element(Msbuild + "DefineConstants").Value);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        // --- Fact 2: existing file without Utinni properties -> merge in;
        //     user-authored properties preserved. ---

        [Fact]
        public void CreateDotNetDirectoryProps_ExistingFileMissingUtinniProps_MergesIn()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempRoot);
            string slnPath = tempRoot + Path.DirectorySeparatorChar;
            string filePath = slnPath + Props.DirectoryBuildPropsFilename;

            try
            {
                // Pre-seed: user-authored Directory.Build.props with one
                // unconditional PropertyGroup containing UserProperty=foo.
                string existing =
                    "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
                    + "<PropertyGroup>"
                    + "<UserProperty>foo</UserProperty>"
                    + "</PropertyGroup>"
                    + "</Project>";
                File.WriteAllText(filePath, existing);

                Props.CreateDotNetDirectoryProps(slnPath);

                XDocument doc = XDocument.Load(filePath);
                XElement project = doc.Element(Msbuild + "Project");
                Assert.NotNull(project);

                // The unconditional PropertyGroup that already existed got the
                // Utinni properties merged in alongside UserProperty.
                var unconditional = project
                    .Elements(Msbuild + "PropertyGroup")
                    .First(g => g.Attribute("Condition") == null);

                Assert.Equal("foo", unconditional.Element(Msbuild + "UserProperty").Value);
                Assert.NotNull(unconditional.Element(Msbuild + "PluginOutputDir"));
                Assert.NotNull(unconditional.Element(Msbuild + "UtinniCoreDotNetPath"));

                // All three configuration-conditional PropertyGroups added.
                var conditionalCount = project
                    .Elements(Msbuild + "PropertyGroup")
                    .Count(g => g.Attribute("Condition") != null);
                Assert.Equal(3, conditionalCount);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        // --- Fact 3: idempotent -- second call is a value-stable no-op. ---

        [Fact]
        public void CreateDotNetDirectoryProps_AlreadyApplied_Idempotent()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempRoot);
            string slnPath = tempRoot + Path.DirectorySeparatorChar;
            string filePath = slnPath + Props.DirectoryBuildPropsFilename;

            try
            {
                Props.CreateDotNetDirectoryProps(slnPath);
                string firstSave = File.ReadAllText(filePath);

                Props.CreateDotNetDirectoryProps(slnPath);
                string secondSave = File.ReadAllText(filePath);

                // XDocument writes deterministic output for unchanged input, so
                // byte-equality is the cleanest assertion. If that ever proves
                // flaky on a future framework version, fall back to value-stable
                // equality via XNode.DeepEquals.
                Assert.Equal(firstSave, secondSave);

                XDocument doc1 = XDocument.Parse(firstSave);
                XDocument doc2 = XDocument.Parse(secondSave);
                Assert.True(XNode.DeepEquals(doc1, doc2));
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        // --- Fact 4: stale Utinni property values are UPDATED to wizard
        //     defaults on re-run (catches the case where an older wizard
        //     populated an obsolete value). ---

        [Fact]
        public void CreateDotNetDirectoryProps_ExistingFileWithStaleUtinniValues_UpdatesValues()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempRoot);
            string slnPath = tempRoot + Path.DirectorySeparatorChar;
            string filePath = slnPath + Props.DirectoryBuildPropsFilename;

            try
            {
                // Pre-seed: existing file with STALE UtinniCoreDotNetPath value.
                string existing =
                    "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
                    + "<PropertyGroup>"
                    + "<UtinniCoreDotNetPath>OLD\\stale\\path\\</UtinniCoreDotNetPath>"
                    + "</PropertyGroup>"
                    + "</Project>";
                File.WriteAllText(filePath, existing);

                Props.CreateDotNetDirectoryProps(slnPath);

                XDocument doc = XDocument.Load(filePath);
                var unconditional = doc
                    .Element(Msbuild + "Project")
                    .Elements(Msbuild + "PropertyGroup")
                    .First(g => g.Attribute("Condition") == null);

                // The stale value got replaced with the wizard's default.
                Assert.Equal(
                    @"..\..\..\bin\Release\",
                    unconditional.Element(Msbuild + "UtinniCoreDotNetPath").Value);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        // --- Fact 5: XXE-safe -- an existing file containing a DOCTYPE/entity
        //     definition is rejected (XmlException at parse time) per
        //     XDocument's default DtdProcessing=Prohibit. ---

        [Fact]
        public void CreateDotNetDirectoryProps_XXEAttempt_RejectedByDtdProhibit()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempRoot);
            string slnPath = tempRoot + Path.DirectorySeparatorChar;
            string filePath = slnPath + Props.DirectoryBuildPropsFilename;

            try
            {
                // XXE attempt: external-entity reference to a sensitive system file.
                string xxeAttempt =
                    "<?xml version=\"1.0\"?>"
                    + "<!DOCTYPE foo [<!ENTITY xxe SYSTEM \"file:///c:/windows/win.ini\">]>"
                    + "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
                    + "<PropertyGroup><Pwned>&xxe;</Pwned></PropertyGroup>"
                    + "</Project>";
                File.WriteAllText(filePath, xxeAttempt);

                // Default DtdProcessing=Prohibit on XDocument.Load: an
                // XmlException is thrown when the parser encounters the DOCTYPE.
                // The wizard surfaces it (does NOT swallow) so the operator
                // notices the malformed input rather than silently letting an
                // attacker influence build output.
                Assert.ThrowsAny<System.Xml.XmlException>(
                    () => Props.CreateDotNetDirectoryProps(slnPath));
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        // --- Fact 6: user's siblings other than PropertyGroup (ItemGroup,
        //     Import, Target) survive the merge untouched. ---

        [Fact]
        public void CreateDotNetDirectoryProps_PreservesNonPropertyGroupSiblings()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempRoot);
            string slnPath = tempRoot + Path.DirectorySeparatorChar;
            string filePath = slnPath + Props.DirectoryBuildPropsFilename;

            try
            {
                string existing =
                    "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
                    + "<Import Project=\"my.user.targets\" />"
                    + "<ItemGroup><Reference Include=\"My.Custom.Reference\" /></ItemGroup>"
                    + "<Target Name=\"MyCustomTarget\"><Message Text=\"hi\" /></Target>"
                    + "</Project>";
                File.WriteAllText(filePath, existing);

                Props.CreateDotNetDirectoryProps(slnPath);

                XDocument doc = XDocument.Load(filePath);
                XElement project = doc.Element(Msbuild + "Project");

                // User-authored siblings still present.
                Assert.NotNull(project.Element(Msbuild + "Import"));
                Assert.Equal("my.user.targets",
                    project.Element(Msbuild + "Import").Attribute("Project").Value);

                Assert.NotNull(project.Element(Msbuild + "ItemGroup"));
                Assert.NotNull(project.Element(Msbuild + "Target"));
                Assert.Equal("MyCustomTarget",
                    project.Element(Msbuild + "Target").Attribute("Name").Value);

                // Utinni PropertyGroups added.
                var groupCount = project.Elements(Msbuild + "PropertyGroup").Count();
                Assert.Equal(4, groupCount);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
