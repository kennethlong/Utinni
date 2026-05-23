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

using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    public class CommandDispatchTests
    {
        // Mask version strings emitted by CommandLineParser in the banner.
        // When run in-process, CLP uses its own metadata: "CommandLine 2.9.1".
        // When run as a standalone exe, the assembly version is: "1.0.0+<githash>".
        // Both formats are masked to "<version>" so fixtures don't need re-baselining
        // on package bumps or commits.
        private static string MaskVersion(string text)
        {
            // Match X.Y.Z+hash (exe form) and X.Y.Z (CLP in-process form)
            return Regex.Replace(text, @"\d+\.\d+\.\d+(?:\+[0-9a-f]+)?", "<version>");
        }

        [Fact]
        public void Run_WithNoArgs_ExitsOneAndMatchesNoArgsGolden()
        {
            var result = InProcessCliRunner.Run();
            Assert.Equal(1, result.ExitCode);
            var combined = MaskVersion(result.Stdout + result.Stderr);
            GoldenTestRunner.MatchesText("dispatch/no-args", combined);
        }

        [Fact]
        public void Run_WithHelpFlag_ExitsOneAndMatchesHelpGolden()
        {
            var result = InProcessCliRunner.Run("--help");
            Assert.Equal(1, result.ExitCode);
            var combined = MaskVersion(result.Stdout + result.Stderr);
            GoldenTestRunner.MatchesText("dispatch/help", combined);
        }

        [Fact]
        public void Run_WithUnknownVerb_ExitsOneAndMatchesUnknownCommandGolden()
        {
            var result = InProcessCliRunner.Run("totally-unknown-verb");
            Assert.Equal(1, result.ExitCode);
            var combined = MaskVersion(result.Stdout + result.Stderr);
            Assert.Contains("totally-unknown-verb", combined);
            GoldenTestRunner.MatchesText("dispatch/unknown-command", combined);
        }

        // 4th [Fact]: verifies that a verb invoked without a required positional arg exits 1
        // (CommandLineParser returns the error handler — errs => 1 in Program.Main).
        [Fact]
        public void Run_WithVerbAndMissingRequiredArg_ExitsOne()
        {
            var result = InProcessCliRunner.Run("parse-tre");  // no path arg
            Assert.Equal(1, result.ExitCode);
        }

        // parse-tre, list-objects (Plan 04-02), and inspect-iff (Plan 04-03) are now implemented
        // and no longer return NotImplemented. Only validate-plugin remains as a stub.
        [Theory]
        [InlineData("validate-plugin", "/tmp/somedir")]
        public void Run_WithVerbStub_ExitsOneAndEmitsNotImplementedJson(string verb, string path)
        {
            var result = InProcessCliRunner.Run(verb, path);
            Assert.Equal(1, result.ExitCode);

            // stdout must be valid JSON with the REVIEWS HIGH-6 locked envelope
            var root = JObject.Parse(result.Stdout);

            // schemaVersion at the envelope ROOT (HIGH-6)
            Assert.Equal(1, root["schemaVersion"].Value<int>());

            // command at the envelope ROOT (HIGH-6)
            Assert.Equal(verb, root["command"].Value<string>());

            // error.kind == "NotImplemented"
            Assert.Equal("NotImplemented", root["error"]["kind"].Value<string>());

            // No nested schemaVersion inside error (HIGH-6)
            Assert.Null(root["error"]["schemaVersion"]);
        }
    }
}
