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

namespace Utinni.Cli.Tests.Infrastructure
{
    public sealed class CliResult
    {
        public int ExitCode { get; set; }
        public string Stdout { get; set; }
        public string Stderr { get; set; }
    }

    public static class InProcessCliRunner
    {
        // CR-05: Console.Out / Console.Error are process-global singletons. xunit can
        // run tests from separate collections (or separate assemblies, when a future
        // dotnet test run wires this assembly into a multi-project invocation) on
        // different threads, and Console.SetOut / Console.SetError are not safe to
        // interleave: two concurrent Run() calls would both save the real stdout as
        // `prevOut`, then both set their own StringWriter, and the second restore
        // would put back the real stdout — leaving the first caller's StringWriter
        // empty while the second caller's StringWriter captured whatever the
        // serialized writes happened to land on. Serialize the whole capture/run/
        // restore body under a single static lock so the redirection is always
        // paired against itself.
        private static readonly object _consoleLock = new object();

        /// <summary>
        /// Runs Utinni.Cli.Program.Main in-process with the given args, capturing
        /// stdout and stderr via Console.SetOut / Console.SetError.
        /// Restores original Console.Out / Console.Error in a finally block.
        /// Stdout and stderr are CRLF-normalised to LF for cross-platform golden comparisons.
        /// </summary>
        public static CliResult Run(params string[] args)
        {
            lock (_consoleLock)
            {
                var prevOut = Console.Out;
                var prevErr = Console.Error;

                var swOut = new StringWriter();
                var swErr = new StringWriter();
                int exitCode;

                try
                {
                    Console.SetOut(swOut);
                    Console.SetError(swErr);
                    exitCode = Utinni.Cli.Program.Main(args);
                }
                finally
                {
                    Console.SetOut(prevOut);
                    Console.SetError(prevErr);
                }

                return new CliResult
                {
                    ExitCode = exitCode,
                    Stdout = swOut.ToString().Replace("\r\n", "\n").Replace("\r", "\n"),
                    Stderr = swErr.ToString().Replace("\r\n", "\n").Replace("\r", "\n")
                };
            }
        }
    }
}
