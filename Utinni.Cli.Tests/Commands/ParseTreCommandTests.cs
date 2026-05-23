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

using Newtonsoft.Json.Linq;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// Tier-2 golden tests for the parse-tre CLI command.
    /// Each test runs the command in-process via InProcessCliRunner and compares output
    /// against committed expected.json goldens via JToken.DeepEquals.
    /// </summary>
    public class ParseTreCommandTests
    {
        /// <summary>
        /// Masks the absolute fixture path in the CLI's JSON output with the stable sentinel
        /// used in committed expected.json files.
        /// The CLI emits Windows paths in JSON with escaped backslashes (e.g. "D:\\Code\\..."),
        /// so we replace both the literal path and its JSON-escaped form.
        /// </summary>
        private static string MaskPath(string actual, string path)
        {
            // Replace JSON-escaped form first (e.g., "D:\\Code\\..." in JSON)
            string escapedPath = path.Replace("\\", "\\\\");
            return actual.Replace(escapedPath, "<fixture-path>").Replace(path, "<fixture-path>");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Happy-path golden tests
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// PLAN: Task 2.4 test 1.
        /// parse-tre on v0005 3-record fixture exits 0 and matches golden.
        /// </summary>
        [Fact]
        public void Run_WithSynthesizedV0005ThreeRecord_ExitsZeroAndMatchesGolden()
        {
            var fixturePath = FixturePath.Resolve("tre/synthesized-3record-v0005.tre");
            var result = InProcessCliRunner.Run("parse-tre", fixturePath);

            Assert.Equal(0, result.ExitCode);
            var masked = MaskPath(result.Stdout, fixturePath);
            GoldenTestRunner.Matches("tre/synthesized-3record-v0005", masked);
        }

        /// <summary>
        /// PLAN: Task 2.4 test 2 (REVIEWS LOW-20 — dedicated v0006 Tier-2 row).
        /// parse-tre on v0006 2-record fixture exits 0 and matches golden.
        /// </summary>
        [Fact]
        public void Run_WithSynthesizedV0006TwoRecord_ExitsZeroAndMatchesGolden()
        {
            var fixturePath = FixturePath.Resolve("tre/synthesized-2record-v0006.tre");
            var result = InProcessCliRunner.Run("parse-tre", fixturePath);

            Assert.Equal(0, result.ExitCode);
            var masked = MaskPath(result.Stdout, fixturePath);
            GoldenTestRunner.Matches("tre/synthesized-2record-v0006", masked);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Negative-case golden tests
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// PLAN: Task 2.4 test 3.
        /// parse-tre on malformed-magic.tre exits 2 and matches golden (error.kind=BadMagic).
        /// </summary>
        [Fact]
        public void Run_WithMalformedMagic_ExitsTwoAndMatchesGolden()
        {
            var fixturePath = FixturePath.Resolve("tre/malformed-magic.tre");
            var result = InProcessCliRunner.Run("parse-tre", fixturePath);

            Assert.Equal(2, result.ExitCode);
            var masked = MaskPath(result.Stdout, fixturePath);
            GoldenTestRunner.Matches("tre/malformed-magic", masked);
        }

        /// <summary>
        /// PLAN: Task 2.4 test 4.
        /// parse-tre on truncated.tre exits 2 and matches golden (error.kind=Truncated).
        /// </summary>
        [Fact]
        public void Run_WithTruncated_ExitsTwoAndMatchesGolden()
        {
            var fixturePath = FixturePath.Resolve("tre/truncated.tre");
            var result = InProcessCliRunner.Run("parse-tre", fixturePath);

            Assert.Equal(2, result.ExitCode);
            var masked = MaskPath(result.Stdout, fixturePath);
            GoldenTestRunner.Matches("tre/truncated", masked);
        }

        /// <summary>
        /// PLAN: Task 2.4 test 5.
        /// parse-tre on unsupported-version.tre exits 2 and matches golden (error.kind=UnsupportedVersion).
        /// </summary>
        [Fact]
        public void Run_WithUnsupportedVersion_ExitsTwoAndMatchesGolden()
        {
            var fixturePath = FixturePath.Resolve("tre/unsupported-version.tre");
            var result = InProcessCliRunner.Run("parse-tre", fixturePath);

            Assert.Equal(2, result.ExitCode);
            var masked = MaskPath(result.Stdout, fixturePath);
            GoldenTestRunner.Matches("tre/unsupported-version", masked);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Structural tests (no committed golden — host-path-dependent)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// PLAN: Task 2.4 test 6.
        /// parse-tre with a path that cannot exist exits 3 with FileNotFound error.
        /// No committed golden — uses direct JToken assertions instead.
        /// </summary>
        [Fact]
        public void Run_WithMissingFile_ExitsThreeAndMatchesFileNotFoundShape()
        {
            var result = InProcessCliRunner.Run("parse-tre", @"Z:\nonexistent\path-that-cannot-exist.tre");

            Assert.Equal(3, result.ExitCode);
            var root = JToken.Parse(result.Stdout);
            Assert.Equal("parse-tre",   root["command"].Value<string>());
            Assert.Equal("FileNotFound", root["error"]["kind"].Value<string>());
        }

        /// <summary>
        /// PLAN: Task 2.4 test 7 — REVIEWS HIGH-6 envelope-shape regression guard.
        /// Asserts that top-level schemaVersion and command are at the ROOT of the envelope
        /// and are NOT nested inside result (no nested schemaVersion, no nested command).
        /// </summary>
        [Fact]
        public void Run_AnyHappyPath_EnvelopeHasTopLevelSchemaVersionAndCommand()
        {
            var fixturePath = FixturePath.Resolve("tre/synthesized-3record-v0005.tre");
            var result = InProcessCliRunner.Run("parse-tre", fixturePath);

            Assert.Equal(0, result.ExitCode);

            var root = JToken.Parse(result.Stdout);

            // REVIEWS HIGH-6: schemaVersion and command MUST be at root level.
            Assert.Equal(1, root["schemaVersion"].Value<int>());
            Assert.Equal("parse-tre", root["command"].Value<string>());

            // REVIEWS HIGH-6: schemaVersion and command MUST NOT be nested inside result.
            Assert.Null(root["result"]["schemaVersion"]);
            Assert.Null(root["result"]["command"]);

            // Verify no error key on success path.
            Assert.Null(root["error"]);
        }
    }
}
