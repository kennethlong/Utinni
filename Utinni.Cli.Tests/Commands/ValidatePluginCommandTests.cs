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
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// Tier-2 golden tests for the validate-plugin CLI command.
    /// Task 4.3 (Plan 04-04): four Theory rows (one per sub-fixture) + four Facts.
    ///
    /// Test naming convention: [Method]_[Scenario]_[ExpectedOutcome] (Phase 1 D-04).
    /// </summary>
    public class ValidatePluginCommandTests
    {
        // ---------------------------------------------------------------------------
        // Path masking helpers
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Masks the absolute fixture directory path in the CLI's JSON output with the
        /// stable sentinel used in committed expected.json files.
        /// Handles both raw path form and JSON-escaped backslash form.
        /// </summary>
        private static string MaskDirectory(string actual, string dirPath)
        {
            string escapedPath = dirPath.Replace("\\", "\\\\");
            return actual
                .Replace(escapedPath, "<TEST_FIXTURE_DIR>")
                .Replace(dirPath, "<TEST_FIXTURE_DIR>");
        }

        /// <summary>
        /// For the wrong-iplugin-shape fixture: also masks non-empty loadErrors array
        /// entries with a stable sentinel. The CompositionException message text is
        /// environment-dependent and not load-bearing for the test assertion.
        /// </summary>
        private static string MaskLoadErrors(string json)
        {
            // Replace each quoted loadErrors entry (non-empty) with "<LOAD_ERROR_MASKED>".
            return Regex.Replace(
                json,
                @"""loadErrors""\s*:\s*\[([^\[\]]+)\]",
                match =>
                {
                    // WR-03: Only mask if the array contains at least one non-whitespace
                    // character. A pretty-printed empty array such as "[\n      ]" has a
                    // non-empty .Trim() result only because Trim drops trailing whitespace —
                    // \s-matching is the load-bearing test. The previous
                    // `IsNullOrEmpty(content) || content == ""` check was redundant
                    // (the second clause is a strict subset of the first) AND missed
                    // whitespace-only arrays that the JSON pretty printer might emit.
                    string content = match.Groups[1].Value;
                    if (!Regex.IsMatch(content, @"\S"))
                    {
                        return match.Value;
                    }
                    return "\"loadErrors\": [\n        \"<LOAD_ERROR_MASKED>\"\n      ]";
                });
        }

        // ---------------------------------------------------------------------------
        // Theory: four sub-fixtures, all exit 0 (overallStatus is in JSON, not exitCode)
        // ---------------------------------------------------------------------------

        [Theory]
        [InlineData("valid-plugin", false)]
        [InlineData("missing-createplugin", false)]
        [InlineData("missing-destroyplugin", false)]
        [InlineData("wrong-iplugin-shape", true)]
        public void Run_AgainstSubFixture_MatchesGolden(string subFixture, bool maskLoadErrors)
        {
            string fixtureDir = FixturePath.Resolve("plugins/" + subFixture);
            var result = InProcessCliRunner.Run("validate-plugin", fixtureDir);

            // Exit 0 regardless of overallStatus — validate-plugin always succeeds if directory exists.
            Assert.Equal(0, result.ExitCode);

            string masked = MaskDirectory(result.Stdout, fixtureDir);
            // Also mask the path that uses backslash (DLL path inside the dir).
            masked = MaskDirectory(masked, fixtureDir.Replace("/", "\\"));

            if (maskLoadErrors)
            {
                masked = MaskLoadErrors(masked);
            }

            // The expected.json lives at Fixtures/plugins/<name>/expected.json.
            // GoldenTestRunner.Matches appends ".expected.json" so we pass the path without the extension.
            // Since GoldenTestRunner uses FixturePath which looks in the Fixtures/ dir, we need:
            //   FixturePath.Resolve("plugins/<name>/expected" + ".expected.json")
            // But that would be "plugins/<name>/expected.expected.json" which is wrong.
            // Instead, use FixturePath directly and load+compare manually.
            string goldenPath = FixturePath.Resolve("plugins/" + subFixture + "/expected.json");
            string expectedJson = File.ReadAllText(goldenPath).Replace("\r\n", "\n").Replace("\r", "\n");

            // Normalize actual (path masking already applied above).
            string actualJson = masked.Replace("\r\n", "\n").Replace("\r", "\n");

            var expected = Newtonsoft.Json.Linq.JToken.Parse(expectedJson);
            var actual = Newtonsoft.Json.Linq.JToken.Parse(actualJson);

            if (!Newtonsoft.Json.Linq.JToken.DeepEquals(expected, actual))
            {
                // Dump for diagnosis.
                string dumpDir = System.IO.Path.Combine(AppContext.BaseDirectory, "TestResults", "plugins_" + subFixture);
                System.IO.Directory.CreateDirectory(dumpDir);
                File.WriteAllText(System.IO.Path.Combine(dumpDir, "expected.json"), expected.ToString());
                File.WriteAllText(System.IO.Path.Combine(dumpDir, "actual.json"), actual.ToString());
                throw new Xunit.Sdk.XunitException(
                    "Golden mismatch for plugins/" + subFixture + ". Dumps at: " + dumpDir + "\n" +
                    "Expected (first 500): " + expected.ToString().Substring(0, Math.Min(500, expected.ToString().Length)) + "\n" +
                    "Actual (first 500): " + actual.ToString().Substring(0, Math.Min(500, actual.ToString().Length)));
            }
        }

        // ---------------------------------------------------------------------------
        // Fact: --help contains the T-04-EoP warning (REVIEWS HIGH-8)
        // ---------------------------------------------------------------------------

        [Fact]
        public void Help_ContainsTEoPMitigationWarning()
        {
            // The WARNING text appears in the top-level --help (verb listing).
            // CommandLineParser emits the full HelpText in the verb listing.
            // The text is wrapped at the console width, so check for stable substrings
            // that fit within a single wrapped line.
            var result = InProcessCliRunner.Run("--help");

            // Normalize whitespace: collapse newlines + indentation into single spaces
            // so multi-line wrapped text can be matched as a single string.
            string combined = (result.Stdout + result.Stderr)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");

            // Collapse multi-line wrapped indentation (lines that start with spaces
            // continuation of the previous line in CommandLineParser's output).
            string normalized = System.Text.RegularExpressions.Regex.Replace(
                combined,
                @"\n\s+",
                " ");

            // Check for the trusted-directory warning.
            Assert.Contains(
                "only run against trusted plugin directories",
                normalized,
                StringComparison.Ordinal);

            // Check for the PluginLoader managed static initialisers disclosure.
            Assert.Contains(
                "PluginLoader composition",
                normalized,
                StringComparison.Ordinal);

            Assert.Contains(
                "execute static initialisers as a side effect",
                normalized,
                StringComparison.Ordinal);
        }

        // ---------------------------------------------------------------------------
        // Fact: --help top-level verb listing includes validate-plugin
        // ---------------------------------------------------------------------------

        [Fact]
        public void Help_TopLevelListsValidatePluginVerb()
        {
            var result = InProcessCliRunner.Run("--help");

            string combined = result.Stdout + result.Stderr;
            Assert.Contains("validate-plugin", combined, StringComparison.Ordinal);
        }

        // ---------------------------------------------------------------------------
        // Fact: envelope has TOP-LEVEL schemaVersion + command (REVIEWS HIGH-6)
        // ---------------------------------------------------------------------------

        [Fact]
        public void Run_AgainstEmptyDirectory_EnvelopeHasTopLevelSchemaVersionAndCommand()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "utinni-test-vp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var result = InProcessCliRunner.Run("validate-plugin", tempDir);
                Assert.Equal(0, result.ExitCode);

                var root = JObject.Parse(result.Stdout);

                // REVIEWS HIGH-6: schemaVersion and command must be at root.
                Assert.Equal(1, root["schemaVersion"].Value<int>());
                Assert.Equal("validate-plugin", root["command"].Value<string>());

                // schemaVersion and command must NOT appear nested inside result.
                Assert.Null(root["result"]?["schemaVersion"]);
                Assert.Null(root["result"]?["command"]);

                // No error envelope (success path).
                Assert.Null(root["error"]);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        // ---------------------------------------------------------------------------
        // Fact: loaderObserved at TOP-LEVEL result (REVIEWS HIGH-5 + iter-2 WARNING-5)
        //       + no per-plugin loaderObserved + directoryChecks present (iter-3 MED-8)
        //       + loadErrors filtered (iter-3 HIGH-1)
        // ---------------------------------------------------------------------------

        [Fact]
        public void Run_AgainstValidPlugin_LoaderObservedAtTopLevelResult()
        {
            string fixtureDir = FixturePath.Resolve("plugins/valid-plugin");
            var result = InProcessCliRunner.Run("validate-plugin", fixtureDir);
            Assert.Equal(0, result.ExitCode);

            var root = JObject.Parse(result.Stdout);
            var resultNode = root["result"];
            Assert.NotNull(resultNode);

            // REVIEWS HIGH-5 + plan-checker iter-2 WARNING-5: loaderObserved at top-level result.
            var loaderObserved = resultNode["loaderObserved"];
            Assert.NotNull(loaderObserved);
            Assert.NotNull(loaderObserved["pluginsCount"]);
            Assert.NotNull(loaderObserved["loadErrors"]);

            // Iter-3 HIGH-1: loadErrors filtered — native fixture produces [] (BadImageFormatException filtered).
            Assert.Equal(0, loaderObserved["loadErrors"].Value<JArray>().Count);

            // Plan-checker iter-2 WARNING-5: loaderObserved must NOT appear per-plugin.
            var firstPlugin = resultNode["plugins"]?.Value<JArray>()?[0];
            Assert.NotNull(firstPlugin);
            Assert.Null(firstPlugin["loaderObserved"]);

            // Iter-3 MED-8: directoryChecks at top-level result.
            var dirChecks = resultNode["directoryChecks"]?.Value<JArray>();
            Assert.NotNull(dirChecks);
            Assert.True(dirChecks.Count >= 1);
            Assert.Equal("loader-vs-reflection-agreement", dirChecks[0]["id"].Value<string>());
        }
    }
}
