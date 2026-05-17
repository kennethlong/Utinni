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
using Xunit;

namespace UtinniCoreDotNet.Tests
{
    // C-12 regression: the VSIX manifest previously pinned to [16.0,17.0) — VS 2019
    // only. CON-O-04 (VS 2019 pin rationale) defaulted to "widen if VS 2022 build is
    // clean"; the audit confirmed clean. These tests assert the four Version range
    // strings (3 InstallationTarget elements + 1 Prerequisite) all carry the widened
    // [16.0,18.0) range so a future edit cannot silently re-pin VS 2019.
    //
    // Path resolution: tests run from <repo>/UtinniCoreDotNet.Tests/bin/Release/net472/.
    // The manifest is at <repo>/sdk/UtinniPluginTemplates/Vsix/source.extension.vsixmanifest.
    // Walk up 4 levels from AppContext.BaseDirectory to reach <repo>/.
    public class VsixManifestTests
    {
        private const string Vsx2011 = "http://schemas.microsoft.com/developer/vsx-schema/2011";

        private static string FindManifest()
        {
            var baseDir = AppContext.BaseDirectory;
            // Primary: bin/Release/net472 → up 4 → repo root
            var candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "sdk", "UtinniPluginTemplates", "Vsix", "source.extension.vsixmanifest"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
            // Fallback: in case Platform sub-dir is reintroduced (bin/Release/net472/x86 → up 5)
            candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "..", "sdk", "UtinniPluginTemplates", "Vsix", "source.extension.vsixmanifest"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
            throw new FileNotFoundException(
                "Could not locate source.extension.vsixmanifest from " + baseDir);
        }

        [Fact]
        public void Manifest_FileExistsAndIsValidXml()
        {
            var path = FindManifest();
            Assert.True(File.Exists(path), "Manifest missing at " + path);
            var ex = Record.Exception(() => XDocument.Load(path));
            Assert.Null(ex);
        }

        [Fact]
        public void InstallationTargets_AllUseVersionRange_16To18Exclusive()
        {
            var doc = XDocument.Load(FindManifest());
            XNamespace ns = Vsx2011;
            var targets = doc.Descendants(ns + "InstallationTarget").ToList();
            Assert.Equal(3, targets.Count);
            foreach (var t in targets)
            {
                var version = t.Attribute("Version")?.Value;
                Assert.Equal("[16.0,18.0)", version);
            }
        }

        [Fact]
        public void Prerequisite_CoreEditor_UsesVersionRange_16To18Exclusive()
        {
            var doc = XDocument.Load(FindManifest());
            XNamespace ns = Vsx2011;
            var prereqs = doc.Descendants(ns + "Prerequisite")
                .Where(p => (string)p.Attribute("Id") == "Microsoft.VisualStudio.Component.CoreEditor")
                .ToList();
            Assert.Single(prereqs);
            Assert.Equal("[16.0,18.0)", prereqs[0].Attribute("Version")?.Value);
        }
    }
}
