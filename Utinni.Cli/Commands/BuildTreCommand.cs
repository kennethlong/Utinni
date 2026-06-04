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
using UtinniCoreDotNet.Formats.Tre;

namespace Utinni.Cli.Commands
{
    [Verb("build-tre", HelpText = "Build a .tre archive via the revived TreeFileBuilder from an explicit --rsp recipe, or synthesize one from --from-tre (AUTH-04).")]
    public class BuildTreOptions
    {
        [Value(0, MetaName = "out", Required = true, HelpText = "Output .tre path to build.")]
        public string OutPath { get; set; }

        [Option("rsp", HelpText = "Explicit TreeFileBuilder .rsp recipe path (builder-format diskPath @ treePath lines).")]
        public string Rsp { get; set; }

        [Option("from-tre", HelpText = "Synthesize the .rsp from this existing .tre (extracts records, recovers order + per-file compression).")]
        public string FromTre { get; set; }

        [Option("tool-path", HelpText = "Override path to the TreeFileBuilder exe (default: resolved beside utinni-cli).")]
        public string ToolPath { get; set; }
    }

    /// <summary>
    /// Plan 13-04 (AUTH-04): a BUILD verb wrapping the Phase-12 <c>TreeFileBuilder</c> native via the
    /// 13-02 <see cref="NativeToolRunner"/> seam. Either consumes an explicit <c>--rsp</c> recipe or
    /// synthesizes one from <c>--from-tre</c> via <see cref="RspSynthesizer"/> (D-06 — makes byte-exact
    /// a testable hypothesis). Invokes <c>TreeFileBuilder -r &lt;rsp&gt; &lt;out.tre&gt;</c>.
    ///
    /// <para>Exit codes: 0 ok; 1 usage (need exactly one of --rsp/--from-tre); 2 native error;
    /// 3 exe/input missing.</para>
    /// </summary>
    public static class BuildTreCommand
    {
        public static int Run(BuildTreOptions o)
        {
            bool hasRsp = !string.IsNullOrEmpty(o.Rsp);
            bool hasFromTre = !string.IsNullOrEmpty(o.FromTre);

            if (hasRsp == hasFromTre)
            {
                return JsonOutput.EmitError("build-tre", "UsageError",
                    "build-tre requires exactly one of --rsp <recipe> or --from-tre <source.tre>.", exitCode: 1);
            }

            string rspPath;
            if (hasFromTre)
            {
                if (!File.Exists(o.FromTre))
                {
                    return JsonOutput.EmitError("build-tre", "FileNotFound", "--from-tre source not found: " + o.FromTre, exitCode: 3);
                }
                // Synthesize the .rsp + extract record payloads beside the output.
                string outFull = Path.GetFullPath(o.OutPath);
                string extractDir = outFull + ".extract";
                rspPath = outFull + ".rsp";
                TreFile source = TreFile.Open(o.FromTre);
                string rspText = RspSynthesizer.Synthesize(source, extractDir);
                File.WriteAllText(rspPath, rspText);
            }
            else
            {
                if (!File.Exists(o.Rsp))
                {
                    return JsonOutput.EmitError("build-tre", "FileNotFound", "--rsp recipe not found: " + o.Rsp, exitCode: 3);
                }
                rspPath = Path.GetFullPath(o.Rsp);
            }

            string exe = NativeToolResolver.Resolve(o.ToolPath, "TreeFileBuilder");
            string outPathFull = Path.GetFullPath(o.OutPath);
            string dir = Path.GetDirectoryName(outPathFull);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            return NativeToolRunner.Run(exe, new[] { "-r", rspPath, outPathFull },
                dir, "build-tre", "TreeFileBuilder", outPathFull);
        }
    }
}
