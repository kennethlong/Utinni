using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace PluginTemplateWizard
{
    // Phase 3 R-G (per 03-CONTEXT D-23 + PATTERNS.md §"R-G idempotent merge"):
    // CreateDotNetDirectoryProps used to silently no-op when the user already
    // had a Directory.Build.props in their solution dir -- the wizard simply
    // returned, omitting Utinni's plugin-build properties and leaving the user
    // wondering why their template-emitted plugin wouldn't build (TD-22).
    // The method is now an idempotent XDocument-based merger: it loads the
    // existing file if present (otherwise creates a minimal skeleton), upserts
    // each Utinni-owned PropertyGroup (one unconditional + three configuration-
    // conditional) without disturbing other PropertyGroups the user added, and
    // writes the result back. Re-running the wizard against an
    // already-merged file is a value-stable no-op (idempotency invariant).
    //
    // CON-T-04 invariant preserved: the method stays in Props.cs as the single
    // factored entry point for the wizard's Directory.Build.props creation. The
    // UpsertPropertyGroup helper lives as a private static method inside this
    // file -- NOT factored into a sibling file.
    //
    // T-03-03-01 (XXE safety): the wizard loads the file via XmlReader with an
    // explicit DtdProcessing = Prohibit setting -- any DOCTYPE declaration in
    // the input raises XmlException at parse time, so entity expansion attacks
    // cannot run. (Empirically the .NET 4.7.2 XDocument.Load(string) overload
    // does NOT inherit DtdProcessing=Prohibit from XmlReaderSettings.Default;
    // an explicit XmlReader instance is required.)
    public static class Props
    {
        public static string DirectoryBuildPropsFilename = "Directory.Build.props";

        private static readonly XNamespace MsbuildNs =
            "http://schemas.microsoft.com/developer/msbuild/2003";

        public static void CreateDotNetDirectoryProps(string slnPath)
        {
            string filePath = slnPath + DirectoryBuildPropsFilename;

            XDocument doc;
            if (File.Exists(filePath))
            {
                // (b)/(c): existing file -- load, locate-or-create the target
                // PropertyGroups, merge Utinni properties without disturbing
                // user-authored siblings. Explicit XmlReaderSettings with
                // DtdProcessing = Prohibit (T-03-03-01) rejects any
                // DOCTYPE/XXE entity definition at parse time.
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                };
                using (XmlReader reader = XmlReader.Create(filePath, settings))
                {
                    doc = XDocument.Load(reader);
                }
            }
            else
            {
                // (a): no file -- create the minimal Project skeleton; the
                // upserts below will populate all four expected PropertyGroups.
                doc = new XDocument(new XElement(MsbuildNs + "Project"));
            }

            XElement project = doc.Element(MsbuildNs + "Project");
            if (project == null)
            {
                project = new XElement(MsbuildNs + "Project");
                doc.Add(project);
            }

            // Unconditional Utinni properties (PluginOutputDir + UtinniCoreDotNetPath
            // -- previously emitted as two separate unconditional PropertyGroups
            // in the old StringBuilder body; consolidated here into one).
            UpsertPropertyGroup(project, conditionAttr: null,
                new[]
                {
                    new PropertyEntry("PluginOutputDir",       @"$(SolutionDir)\bin\$(Configuration)\"),
                    new PropertyEntry("UtinniCoreDotNetPath",  @"..\..\..\bin\Release\"),
                });

            UpsertPropertyGroup(project,
                conditionAttr: "'$(Configuration)|$(Platform)' == 'RelWithDbgInfo|AnyCPU'",
                new[]
                {
                    new PropertyEntry("OutputPath",            "$(PluginOutputDir)"),
                    new PropertyEntry("DebugType",             "pdbonly"),
                    new PropertyEntry("Optimize",              "true"),
                    new PropertyEntry("DefineConstants",       "TRACE"),
                    new PropertyEntry("PlatformTarget",        "x86"),
                    new PropertyEntry("ErrorReport",           "prompt"),
                    new PropertyEntry("LangVersion",           "7.3"),
                    new PropertyEntry("CodeAnalysisRuleSet",   "MinimumRecommendedRules.ruleset"),
                });

            UpsertPropertyGroup(project,
                conditionAttr: "'$(Configuration)|$(Platform)' == 'Release|AnyCPU'",
                new[]
                {
                    new PropertyEntry("OutputPath",            "$(PluginOutputDir)"),
                    new PropertyEntry("DebugType",             "pdbonly"),
                    new PropertyEntry("Optimize",              "true"),
                    new PropertyEntry("DefineConstants",       "TRACE"),
                    new PropertyEntry("PlatformTarget",        "x86"),
                    new PropertyEntry("ErrorReport",           "prompt"),
                    new PropertyEntry("WarningLevel",          "4"),
                    new PropertyEntry("LangVersion",           "7.3"),
                });

            UpsertPropertyGroup(project,
                conditionAttr: "'$(Configuration)|$(Platform)' == 'Debug|AnyCPU'",
                new[]
                {
                    new PropertyEntry("OutputPath",            "$(PluginOutputDir)"),
                    new PropertyEntry("DebugType",             "full"),
                    new PropertyEntry("DebugSymbols",          "true"),
                    new PropertyEntry("Optimize",              "false"),
                    new PropertyEntry("DefineConstants",       "DEBUG;TRACE"),
                    new PropertyEntry("PlatformTarget",        "x86"),
                    new PropertyEntry("ErrorReport",           "prompt"),
                    new PropertyEntry("WarningLevel",          "4"),
                    new PropertyEntry("LangVersion",           "7.3"),
                });

            doc.Save(filePath);
        }

        // Locates an existing PropertyGroup matching conditionAttr (null means
        // "the unconditional PropertyGroup with no Condition attribute") and
        // upserts each (name, value) entry into it. Creates the group if not
        // present. Existing child elements with the same name have their value
        // updated (idempotent re-run). Other siblings under <Project> are left
        // untouched -- user-authored PropertyGroups, ItemGroups, Imports,
        // Targets all survive.
        private static void UpsertPropertyGroup(
            XElement project,
            string conditionAttr,
            PropertyEntry[] properties)
        {
            XElement group = project
                .Elements(MsbuildNs + "PropertyGroup")
                .FirstOrDefault(pg =>
                    (conditionAttr == null && pg.Attribute("Condition") == null) ||
                    (conditionAttr != null && pg.Attribute("Condition") != null
                        && pg.Attribute("Condition").Value == conditionAttr));

            if (group == null)
            {
                group = new XElement(MsbuildNs + "PropertyGroup");
                if (conditionAttr != null)
                {
                    group.SetAttributeValue("Condition", conditionAttr);
                }
                project.Add(group);
            }

            foreach (PropertyEntry entry in properties)
            {
                XElement child = group.Element(MsbuildNs + entry.Name);
                if (child == null)
                {
                    group.Add(new XElement(MsbuildNs + entry.Name, entry.Value));
                }
                else
                {
                    child.Value = entry.Value;
                }
            }
        }

        // Small POCO -- C# 7.3 (the project's LangVersion) doesn't support the
        // (string, string) value-tuple syntax in this project's binding-redirect
        // chain reliably, and the named struct avoids the System.ValueTuple
        // dependency in the VSIX package.
        private struct PropertyEntry
        {
            public string Name;
            public string Value;

            public PropertyEntry(string name, string value)
            {
                Name = name;
                Value = value;
            }
        }
    }
}
