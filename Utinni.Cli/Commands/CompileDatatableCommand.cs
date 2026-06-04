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

using System.IO;
using CommandLine;
using Utinni.Cli.Commands.Subprocess;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("compile-datatable", HelpText = "Compile a tab-delimited spreadsheet (or .xml) to a datatable .iff via the revived DataTableTool (AUTH-06).")]
    public class CompileDatatableOptions
    {
        [Value(0, MetaName = "input", Required = true, HelpText = "Input spreadsheet: tab-delimited text or .xml (the native isXmlFile switch decides). NOT comma-CSV.")]
        public string Input { get; set; }

        [Value(1, MetaName = "output", Required = true, HelpText = "Output datatable .iff path.")]
        public string Output { get; set; }

        [Option("tool-path", HelpText = "Override path to the DataTableTool exe (default: resolved beside utinni-cli).")]
        public string ToolPath { get; set; }
    }

    /// <summary>
    /// Plan 13-05 (AUTH-06): a BUILD verb wrapping the Plan-13-01-lifted <c>DataTableTool</c> native via
    /// the 13-02 <see cref="NativeToolRunner"/> seam. The native accepts a TAB-delimited spreadsheet or
    /// <c>.xml</c> (its own <c>isXmlFile</c> switch decides) and emits a datatable <c>.iff</c> — a thin
    /// pass-through; it does NOT invent a comma-CSV front-end (the managed <c>CsvCellCoercion</c> path is
    /// distinct). Cross-checked against the managed <c>DataTableWriter</c> oracle (D-03) in the goldens;
    /// native output is SOE-authoritative on disagreement (D-04).
    ///
    /// <para>Exit codes: 0 ok; 2 native error; 3 exe/input missing.</para>
    /// </summary>
    public static class CompileDatatableCommand
    {
        public static int Run(CompileDatatableOptions o)
        {
            if (!File.Exists(o.Input))
            {
                return JsonOutput.EmitError("compile-datatable", "FileNotFound", "input spreadsheet not found: " + o.Input, exitCode: 3);
            }

            string exe = NativeToolResolver.Resolve(o.ToolPath, "DataTableTool");
            string inputFull = Path.GetFullPath(o.Input);
            string outputFull = Path.GetFullPath(o.Output);
            string workingDir = Path.GetDirectoryName(inputFull);
            string outDir = Path.GetDirectoryName(outputFull);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

            // DataTableTool uses named options: -i <input> -o <output> (NOT positional args).
            return NativeToolRunner.Run(exe, new[] { "-i", inputFull, "-o", outputFull },
                workingDir, "compile-datatable", "DataTableTool", outputFull);
        }
    }
}
