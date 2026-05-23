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

using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// Tier-2 golden tests for the inspect-iff CLI command.
    /// Each test runs the command in-process via InProcessCliRunner and compares output
    /// against committed expected.json goldens via JToken.DeepEquals.
    ///
    /// Test naming convention: [Method]_[Scenario]_[ExpectedOutcome] (Phase 1 D-04).
    /// </summary>
    public class InspectIffCommandTests
    {
        /// <summary>
        /// Masks the absolute fixture path in the CLI's JSON output with the stable sentinel
        /// used in committed expected.json files.
        /// The CLI emits Windows paths in JSON with escaped backslashes (e.g. "D:\\Code\\..."),
        /// so we replace both the literal path and its JSON-escaped form.
        /// </summary>
        private static string MaskPath(string actual, string path)
        {
            string escapedPath = path.Replace("\\", "\\\\");
            return actual.Replace(escapedPath, "<fixture-path>").Replace(path, "<fixture-path>");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Theory: five fixtures via InlineData
        // ─────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("synthetic-nested", 0)]
        [InlineData("synthetic-secondary", 0)]          // REVIEWS MEDIUM-12 + MEDIUM-14 — no real-sample; CAT  + PROP-as-leaf
        [InlineData("malformed-nested-overflow", 2)]     // iter-4 HIGH-1 — RENAMED from malformed-chunk-overflow; golden carries NestedChunkOverflow
        [InlineData("malformed-truncated", 2)]           // iter-4 HIGH-1 — redesigned byte layout; golden carries Truncated
        [InlineData("malformed-missing-padbyte", 2)]     // REVIEWS MEDIUM-11 — odd-length leaf at EOF with no pad byte; golden carries Truncated
        public void Run_WithFixture_ExitsWithExpectedCodeAndMatchesGolden(string fixtureName, int expectedExitCode)
        {
            var fixturePath = FixturePath.Resolve("iff/" + fixtureName + ".iff");
            var result = InProcessCliRunner.Run("inspect-iff", fixturePath);

            Assert.Equal(expectedExitCode, result.ExitCode);

            var masked = MaskPath(result.Stdout, fixturePath);
            GoldenTestRunner.Matches("iff/" + fixtureName, masked);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fact: missing file → exit 3 + FileNotFound
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Run_WithMissingFile_ExitsThreeAndEmitsFileNotFoundError()
        {
            var result = InProcessCliRunner.Run("inspect-iff", @"Z:\nonexistent\path-that-cannot-exist.iff");

            Assert.Equal(3, result.ExitCode);

            var root = JToken.Parse(result.Stdout);
            Assert.Equal("inspect-iff",  root["command"].Value<string>());
            Assert.Equal("FileNotFound", root["error"]["kind"].Value<string>());
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fact: REVIEWS HIGH-6 envelope-shape regression guard
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Run_AnyHappyPath_EnvelopeHasTopLevelSchemaVersionAndCommand()
        {
            var fixturePath = FixturePath.Resolve("iff/synthetic-nested.iff");
            var result = InProcessCliRunner.Run("inspect-iff", fixturePath);

            Assert.Equal(0, result.ExitCode);

            var root = JToken.Parse(result.Stdout);

            // REVIEWS HIGH-6: schemaVersion and command MUST be at root level.
            Assert.Equal(1, root["schemaVersion"].Value<int>());
            Assert.Equal("inspect-iff", root["command"].Value<string>());

            // REVIEWS HIGH-6: schemaVersion and command MUST NOT be nested inside result.
            Assert.Null(root["result"]["schemaVersion"]);
            Assert.Null(root["result"]["command"]);

            // Verify no error key on success path.
            Assert.Null(root["error"]);

            // Verify result has both tree and flat.
            Assert.NotNull(root["result"]["tree"]);
            Assert.NotNull(root["result"]["flat"]);
            Assert.NotNull(root["result"]["flat"]["nodes"]);
            Assert.NotNull(root["result"]["flat"]["edges"]);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fact: cross-view id correspondence (flat.nodes ids == tree ids)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Run_WithSyntheticNested_FlatAndTreeIdsAgree()
        {
            var fixturePath = FixturePath.Resolve("iff/synthetic-nested.iff");
            var result = InProcessCliRunner.Run("inspect-iff", fixturePath);

            Assert.Equal(0, result.ExitCode);

            var root = JToken.Parse(result.Stdout);

            // Collect all ids from flat.nodes.
            var flatIds = new List<string>();
            foreach (var node in root["result"]["flat"]["nodes"])
            {
                flatIds.Add(node["id"].Value<string>());
            }

            // Collect all ids from the tree via recursive descent.
            var treeIds = new List<string>();
            CollectTreeIds(root["result"]["tree"], treeIds);

            // The two sets must be equal (order-independent).
            flatIds.Sort(System.StringComparer.Ordinal);
            treeIds.Sort(System.StringComparer.Ordinal);

            Assert.Equal(treeIds.Count, flatIds.Count);
            for (int i = 0; i < treeIds.Count; i++)
            {
                Assert.Equal(treeIds[i], flatIds[i]);
            }
        }

        /// <summary>
        /// Recursively collects all 'id' values from the tree projection.
        /// </summary>
        private static void CollectTreeIds(JToken node, List<string> acc)
        {
            if (node == null) return;

            var idToken = node["id"];
            if (idToken != null)
            {
                acc.Add(idToken.Value<string>());
            }

            var children = node["children"];
            if (children != null)
            {
                foreach (var child in children)
                {
                    CollectTreeIds(child, acc);
                }
            }
        }
    }
}
