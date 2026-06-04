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
    [Verb("compile-template", HelpText = "Compile a .tpf object-template source to a byte-correct .iff via the revived TemplateCompiler (AUTH-03).")]
    public class CompileTemplateOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .tpf object-template source file.")]
        public string Path { get; set; }

        [Option("tool-path", HelpText = "Override path to the TemplateCompiler exe (default: resolved beside utinni-cli).")]
        public string ToolPath { get; set; }
    }

    /// <summary>
    /// Plan 13-04 (AUTH-03): a BUILD verb wrapping the Phase-12 <c>TemplateCompiler</c> native via the
    /// 13-02 <see cref="NativeToolRunner"/> seam. <c>TemplateCompiler -compile &lt;x.tpf&gt;</c> emits a
    /// sibling <c>x.iff</c>. Coexistence-by-verb-ownership: this is a <c>compile-*</c> BUILD verb
    /// (native, byte-correct), distinct from the managed <c>roundtrip-*</c>/<c>save</c> EDIT verbs.
    ///
    /// <para>Exit codes (inherited from the seam): 0 ok; 2 native tool error; 3 exe/input missing.
    /// The wrapper never triggers the native's interactive <c>getchar()</c> usage path (always a
    /// valid <c>-compile</c> invocation).</para>
    /// </summary>
    public static class CompileTemplateCommand
    {
        public static int Run(CompileTemplateOptions o)
        {
            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("compile-template", "FileNotFound", ".tpf file not found: " + o.Path, exitCode: 3);
            }

            string exe = NativeToolResolver.Resolve(o.ToolPath, "TemplateCompiler");
            // WorkingDirectory = the .tpf's directory so the @class <name>.tdf resolves relative to it.
            string workingDir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(o.Path));
            string expectedIff = System.IO.Path.ChangeExtension(System.IO.Path.GetFullPath(o.Path), ".iff");

            return NativeToolRunner.Run(exe, new[] { "-compile", System.IO.Path.GetFullPath(o.Path) },
                workingDir, "compile-template", "TemplateCompiler", expectedIff);
        }
    }
}
