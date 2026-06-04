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
using Utinni.Cli.Commands.Subprocess;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    /// <summary>
    /// Plan 13-05 Task 3: shared driver for the two AUTH-06 item exporters (Armor/CoreWeapon). Each
    /// reads a datatable <c>.iff</c>, emits a <c>.tpf</c>, then chains <c>system("TemplateCompiler")</c>
    /// — so the verb stages the exporter exe + a <c>tools.cfg</c> + the TemplateCompiler exe in one dir
    /// and runs with <c>WorkingDirectory</c> set there (T-13-15). The Perforce FATAL is already neutered
    /// by the Plan 13-01 source-stub (UTINNI_TOOLS_NO_PERFORCE).
    ///
    /// <para><b>Security (T-13-03):</b> the exporter's internal <c>system("TemplateCompiler "+path)</c> is
    /// the inherited injection surface; this driver VALIDATES the input path (rejects shell-meta / <c>..</c>)
    /// BEFORE invoking, so an unsanitised caller string never reaches the native shell-out.</para>
    /// </summary>
    public static class ExportCommandShared
    {
        // Characters that would let an input path break out of the exporter's internal system() call.
        private static readonly char[] ShellMeta = { '&', '|', ';', '<', '>', '^', '"', '\'', '`', '$', '%', '\n', '\r', '*', '?' };

        public static int Run(string verb, string toolBaseName, string datatableIff, string outDir, string toolPath, string stagedDir)
        {
            // T-13-03: reject shell-meta / parent-escape in the input path before any native invocation.
            if (string.IsNullOrEmpty(datatableIff)
                || datatableIff.IndexOfAny(ShellMeta) >= 0
                || datatableIff.Contains(".."))
            {
                return JsonOutput.EmitError(verb, "UnsafeInputPath",
                    "input path contains shell-meta or '..' and is rejected before invocation: " + datatableIff, exitCode: 2);
            }

            if (!File.Exists(datatableIff))
            {
                return JsonOutput.EmitError(verb, "FileNotFound", "datatable .iff not found: " + datatableIff, exitCode: 3);
            }

            string exe = NativeToolResolver.Resolve(toolPath, toolBaseName);
            // WorkingDirectory: the staged tool dir (must hold the exporter exe + tools.cfg + the
            // TemplateCompiler exe so the exporter's system("TemplateCompiler") chain + config-load
            // resolve). Defaults to the exe's own directory.
            string workingDir = !string.IsNullOrEmpty(stagedDir)
                ? Path.GetFullPath(stagedDir)
                : Path.GetDirectoryName(Path.GetFullPath(exe));

            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(Path.GetFullPath(outDir));

            // The exporter's output location is governed by its tools.cfg (schematicTemplatePath); the
            // produced-artifact path is not known a-priori, so expectedOutputPath is null (the envelope
            // reports the native exit + stderr; the caller inspects the staged dir for artifacts).
            return NativeToolRunner.Run(exe, new[] { "-i", Path.GetFullPath(datatableIff) },
                workingDir, verb, toolBaseName, expectedOutputPath: null);
        }
    }
}
