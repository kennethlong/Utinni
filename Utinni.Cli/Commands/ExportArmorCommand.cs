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
// Implementation original to Utinni under MIT.

using CommandLine;

namespace Utinni.Cli.Commands
{
    [Verb("export-armor", HelpText = "Run the revived ArmorExporterTool over a datatable .iff (emits .tpf + chained .iff) (AUTH-06).")]
    public class ExportArmorOptions
    {
        [Value(0, MetaName = "datatable-iff", Required = true, HelpText = "Armor schematic datatable .iff input.")]
        public string DatatableIff { get; set; }

        [Value(1, MetaName = "out-dir", HelpText = "Output directory for the produced .tpf/.iff (default: the staged tool dir per tools.cfg).")]
        public string OutDir { get; set; }

        [Option("tool-path", HelpText = "Override path to the ArmorExporterTool exe.")]
        public string ToolPath { get; set; }

        [Option("staged-dir", HelpText = "Staged tool dir holding the exporter exe + tools.cfg + TemplateCompiler exe (WorkingDirectory).")]
        public string StagedDir { get; set; }
    }

    /// <summary>Plan 13-05 (AUTH-06): the armor item-exporter verb. See <see cref="ExportCommandShared"/>.</summary>
    public static class ExportArmorCommand
    {
        public static int Run(ExportArmorOptions o)
        {
            return ExportCommandShared.Run("export-armor", "ArmorExporterTool", o.DatatableIff, o.OutDir, o.ToolPath, o.StagedDir);
        }
    }
}
