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

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands.Subprocess
{
    /// <summary>
    /// Plan 13-02: the FIRST subprocess seam in <c>utinni-cli</c> — shared by every Plan 13-04/13-05
    /// BUILD verb (compile-template, build-tre, compile-definition, compile-datatable, export-*).
    /// The CLI is otherwise fully in-process; this is the lone place a native exe is spawned.
    ///
    /// <para><b>Security (T-13-04, ASVS V5):</b> spawned with <c>UseShellExecute=false</c> — there is
    /// NO shell (no cmd.exe), so shell-metacharacter injection is structurally impossible. The
    /// remaining concern is argument-boundary injection (a path with spaces/quotes splitting into
    /// extra args). The caller supplies an explicit <paramref name="args"/> array; this class quotes
    /// each element via the canonical CommandLineToArgvW algorithm (<see cref="AppendArgument"/>) so
    /// the native process re-parses exactly the array the caller passed. net472's
    /// <see cref="ProcessStartInfo"/> has no <c>ArgumentList</c> (that is .NET Core 2.1+), so building
    /// a correctly-escaped <see cref="ProcessStartInfo.Arguments"/> string is the only available
    /// mechanism — this is deliberate per-arg quoting, NOT a naive concatenation of caller strings.</para>
    ///
    /// <para><b>Exit-code contract</b> (mirrors RoundtripTabCommand): native exit 0 → CLI exit 0 with
    /// the success envelope; native non-zero → CLI exit 2 ("ToolError") with normalized stderr;
    /// the exe missing → CLI exit 3 ("FileNotFound") WITHOUT throwing.</para>
    /// </summary>
    /// <summary>Captured outcome of a native-tool spawn (see <see cref="NativeToolRunner.RunCaptured"/>).</summary>
    public sealed class NativeToolResult
    {
        public bool ExeFound { get; set; }
        public bool TimedOut { get; set; }
        public int ExitCode { get; set; }
        public string Stdout { get; set; }
        public string Stderr { get; set; }
    }

    public static class NativeToolRunner
    {
        // Wall-clock backstop for a single native-tool invocation. Generous for any real build
        // (these tools compile a single .tpf/.tab in well under a second) while bounding a wedged
        // error/exit path to a finite wait.
        private const int ToolTimeoutMs = 60000;

        /// <summary>
        /// Spawns <paramref name="toolExe"/> with the explicit <paramref name="args"/> array, captures
        /// stdout/stderr/exit-code, and emits the locked envelope
        /// <c>{ tool, exitCode, outputPath, produced, stderr }</c> through <see cref="JsonOutput"/>.
        /// </summary>
        /// <param name="toolExe">Absolute path to the native exe.</param>
        /// <param name="args">Explicit argument array (each element is one argv entry, quoted internally).</param>
        /// <param name="workingDir">Working directory for the child (the staged tool dir — where the
        /// exporter chain's <c>system("TemplateCompiler")</c> and <c>tools.cfg</c> resolve). May be null.</param>
        /// <param name="verb">CLI verb name for the envelope <c>command</c> field.</param>
        /// <param name="toolName">Display tool name for the envelope <c>tool</c> field.</param>
        /// <param name="expectedOutputPath">Path the native tool is expected to produce; <c>produced</c>
        /// reports whether it exists post-run. May be null (then produced=false).</param>
        public static int Run(string toolExe, string[] args, string workingDir, string verb, string toolName, string expectedOutputPath)
        {
            if (verb == null) throw new ArgumentNullException("verb");

            NativeToolResult r = RunCaptured(toolExe, args, workingDir);

            if (!r.ExeFound)
            {
                return JsonOutput.EmitError(verb, "FileNotFound",
                    toolName + " executable not found: " + toolExe, exitCode: 3);
            }
            if (r.TimedOut)
            {
                return JsonOutput.EmitError(verb, "ToolTimeout",
                    toolName + " did not exit within " + (ToolTimeoutMs / 1000) + "s (killed).", exitCode: 2);
            }

            string normalizedStderr = NormalizeBanner(r.Stderr);
            if (r.ExitCode != 0)
            {
                return JsonOutput.EmitError(verb, "ToolError", normalizedStderr, exitCode: 2);
            }

            bool produced = !string.IsNullOrEmpty(expectedOutputPath) && File.Exists(expectedOutputPath);
            var result = new JObject
            {
                ["exitCode"] = r.ExitCode,
                ["outputPath"] = expectedOutputPath,
                ["produced"] = produced,
                ["stderr"] = normalizedStderr,
                ["tool"] = toolName
            };
            return JsonOutput.EmitSuccess(verb, result);
        }

        /// <summary>
        /// Spawns the tool and CAPTURES the outcome without emitting any envelope — for callers (e.g.
        /// compile-definition) that run the native best-effort alongside another deliverable. Mirrors
        /// the same hang-proofing (stdin-close + timeout) as <see cref="Run"/>.
        /// </summary>
        public static NativeToolResult RunCaptured(string toolExe, string[] args, string workingDir)
        {
            if (toolExe == null) throw new ArgumentNullException("toolExe");
            if (args == null) throw new ArgumentNullException("args");

            if (!File.Exists(toolExe))
            {
                return new NativeToolResult { ExeFound = false };
            }

            var psi = new ProcessStartInfo
            {
                FileName = toolExe,
                Arguments = BuildArguments(args),
                UseShellExecute = false,
                RedirectStandardInput = true,   // close stdin so a native getchar() (usage/error paths) gets EOF, never hangs
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            if (!string.IsNullOrEmpty(workingDir))
            {
                psi.WorkingDirectory = workingDir;
            }

            using (var process = new Process())
            {
                process.StartInfo = psi;
                process.Start();
                process.StandardInput.Close();

                System.Threading.Tasks.Task<string> outTask = process.StandardOutput.ReadToEndAsync();
                System.Threading.Tasks.Task<string> errTask = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(ToolTimeoutMs))
                {
                    try { process.Kill(); } catch { /* already exiting */ }
                    process.WaitForExit(5000);
                    return new NativeToolResult { ExeFound = true, TimedOut = true };
                }

                return new NativeToolResult
                {
                    ExeFound = true,
                    ExitCode = process.ExitCode,
                    Stdout = outTask.Result,
                    Stderr = errTask.Result
                };
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Banner normalization (Pitfall 3 / T-13-06).
        //
        // The native tools print `__DATE__ " " __TIME__` banners and embed absolute maintainer
        // paths in their output; TemplateDefinitionCompiler embeds them in generated .cpp/.h.
        // Strip them so two runs on different days/machines produce identical text before any
        // committed golden compare — and so no maintainer-local path leaks into the repo.
        //
        // Idempotent by construction: each pattern is replaced by a fixed token that contains no
        // date/time/path shape, so NormalizeBanner(NormalizeBanner(x)) == NormalizeBanner(x).
        // ─────────────────────────────────────────────────────────────────────

        // __DATE__ form: "Mmm D YYYY" / "Mmm DD YYYY" (one or two spaces before the day).
        private static readonly Regex DatePattern = new Regex(
            @"\b(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s+\d{1,2}\s+\d{4}\b",
            RegexOptions.Compiled);

        // __TIME__ form: "HH:MM:SS".
        private static readonly Regex TimePattern = new Regex(
            @"\b\d{2}:\d{2}:\d{2}\b",
            RegexOptions.Compiled);

        // Windows drive-letter absolute path (the maintainer-local leak surface). Whitespace ends a
        // path token (so the regex does not greedily swallow the words after the path); paths with
        // embedded spaces are stripped up to the first space — sufficient to stabilise the banner.
        private static readonly Regex AbsPathPattern = new Regex(
            @"[A-Za-z]:\\[^\s""<>|]*",
            RegexOptions.Compiled);

        /// <summary>
        /// Strips <c>__DATE__</c>/<c>__TIME__</c> banner fragments and Windows absolute paths from
        /// <paramref name="text"/>, replacing each with a stable token. Idempotent. Null → empty.
        /// </summary>
        public static string NormalizeBanner(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            string result = AbsPathPattern.Replace(text, "<PATH>");
            result = DatePattern.Replace(result, "<DATE>");
            result = TimePattern.Replace(result, "<TIME>");
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Argument quoting — the canonical CommandLineToArgvW round-trip algorithm.
        // (Equivalent to the .NET runtime's internal PasteArguments.AppendArgument.)
        // ─────────────────────────────────────────────────────────────────────

        private static string BuildArguments(string[] args)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                AppendArgument(sb, args[i] ?? string.Empty);
            }
            return sb.ToString();
        }

        private static void AppendArgument(StringBuilder sb, string argument)
        {
            // Fast path: a non-empty arg with no whitespace/quote needs no escaping.
            if (argument.Length != 0 && argument.IndexOfAny(QuoteOrWhitespace) < 0)
            {
                sb.Append(argument);
                return;
            }

            sb.Append('"');
            int idx = 0;
            while (idx < argument.Length)
            {
                char c = argument[idx++];
                if (c == '\\')
                {
                    int backslashCount = 1;
                    while (idx < argument.Length && argument[idx] == '\\')
                    {
                        idx++;
                        backslashCount++;
                    }

                    if (idx == argument.Length)
                    {
                        // Backslashes at the end: double them so the closing quote is not escaped.
                        sb.Append('\\', backslashCount * 2);
                    }
                    else if (argument[idx] == '"')
                    {
                        // Backslashes before a quote: double them, then escape the quote.
                        sb.Append('\\', backslashCount * 2 + 1);
                        sb.Append('"');
                        idx++;
                    }
                    else
                    {
                        // Backslashes before a normal char: emit as-is.
                        sb.Append('\\', backslashCount);
                    }
                }
                else if (c == '"')
                {
                    sb.Append('\\');
                    sb.Append('"');
                }
                else
                {
                    sb.Append(c);
                }
            }
            sb.Append('"');
        }

        private static readonly char[] QuoteOrWhitespace = { ' ', '\t', '\n', '\v', '"' };
    }
}
