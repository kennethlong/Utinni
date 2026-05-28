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
    /// Tier-2 golden tests for the roundtrip-iff CLI command — the D-02 max-harness for the IFF
    /// write path and the automated gate for PROD-W1-IFF Success Criterion 4 ("no corruption of
    /// unedited chunks").
    ///
    /// <para>Four goldens:</para>
    /// <list type="bullet">
    ///   <item><c>synthetic-nested</c> — no mutation; asserts byte-exact round-trip on a small
    ///         even-length-leaf-only nested IFF.</item>
    ///   <item><c>odd-chunk-no-pad</c> — no mutation; asserts byte-exact round-trip on an
    ///         odd-length-payload chunk with NO trailing pad byte (the SWG no-pad quirk).</item>
    ///   <item><c>mutation-leaf-edit</c> — one-leaf payload edit via <c>--mutate-leaf</c>;
    ///         asserts <c>byteExact:false, untouchedLeafRangesIdentical:true</c>, proving every
    ///         OTHER leaf round-trips identically through the write path
    ///         (closes the 08-REVIEWS round-1 mutation-golden gap).</item>
    ///   <item><c>mutation-leaf-removed</c> — one-leaf structural removal via <c>--remove-leaf</c>;
    ///         asserts <c>byteExact:false, byteExactExceptRemovedLeaf:true,
    ///         parentContainerChildCountBefore:3, parentContainerChildCountAfter:2</c>
    ///         (closes 08-REVIEWS round-2 MEDIUM 8 / cursor N-M4 — structural-op golden).</item>
    /// </list>
    ///
    /// <para>Naming convention: [Method]_[Scenario]_[ExpectedOutcome] (Phase 1 D-04).</para>
    /// </summary>
    public class RoundtripIffCommandTests
    {
        /// <summary>
        /// Masks the absolute fixture path in the CLI's JSON output with the stable sentinel
        /// used in committed expected.json files (mirrors InspectIffCommandTests.MaskPath).
        /// </summary>
        private static string MaskPath(string actual, string path)
        {
            string escapedPath = path.Replace("\\", "\\\\");
            return actual.Replace(escapedPath, "<fixture-path>").Replace(path, "<fixture-path>");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Theory: no-mutation round-trip — byte-exact identity over the whole file.
        // Covers both the nested fixture and the odd-length-no-pad regression fixture.
        // ─────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("synthetic-nested")]
        [InlineData("odd-chunk-no-pad")]
        public void Run_NoMutation_ExitsZeroAndMatchesGoldenWithByteExactTrue(string fixtureName)
        {
            var fixturePath = FixturePath.Resolve("iff/roundtrip/" + fixtureName + ".iff");

            var result = InProcessCliRunner.Run("roundtrip-iff", fixturePath);

            Assert.Equal(0, result.ExitCode);

            var masked = MaskPath(result.Stdout, fixturePath);
            GoldenTestRunner.Matches("iff/roundtrip/" + fixtureName, masked);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fact: --mutate-leaf — proves untouched-leaf-range identity on an edited PAYLOAD write
        // (closes 08-REVIEWS round-1 mutation-golden gap).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Run_MutateOneLeaf_ExitsZeroAndAssertsUntouchedLeafRangesIdentical()
        {
            var fixturePath = FixturePath.Resolve("iff/roundtrip/mutation-leaf-edit.iff");

            var result = InProcessCliRunner.Run(
                "roundtrip-iff", fixturePath,
                "--mutate-leaf", "FORM:WSNP/0/NAME:NAME/1",
                "--mutate-hex",  "DEADBEEF");

            Assert.Equal(0, result.ExitCode);

            var masked = MaskPath(result.Stdout, fixturePath);
            GoldenTestRunner.Matches("iff/roundtrip/mutation-leaf-edit", masked);

            // Also sanity-check the parsed envelope directly: byteExact MUST be false (we edited),
            // untouchedLeafRangesIdentical MUST be true (every other leaf round-trips byte-identical),
            // and untouchedLeafCount MUST equal N-1 = 3 (the fixture has 4 leaves total).
            var root = JToken.Parse(result.Stdout);
            Assert.False(root["result"]["byteExact"].Value<bool>());
            Assert.True(root["result"]["untouchedLeafRangesIdentical"].Value<bool>());
            Assert.Equal(3, root["result"]["untouchedLeafCount"].Value<int>());
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fact: --remove-leaf — proves untouched-leaf-range identity on a STRUCTURAL removal
        // (closes 08-REVIEWS round-2 MEDIUM 8 / cursor N-M4 — structural-op golden).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Run_RemoveOneLeaf_ExitsZeroAndAssertsByteExactExceptRemovedLeaf()
        {
            var fixturePath = FixturePath.Resolve("iff/roundtrip/mutation-leaf-removed.iff");

            // Remove the LAST sibling under FORM:OBJS so the surviving siblings keep their
            // ordinals — the gate doesn't rely on the sibling-shift fix-up here, just on the
            // pure structural-removal byte-range identity. The implementation also handles the
            // sibling-shift case (when a non-last leaf is removed) but this golden documents the
            // simpler, structurally-pure case.
            var result = InProcessCliRunner.Run(
                "roundtrip-iff", fixturePath,
                "--remove-leaf", "FORM:WSNP/0/FORM:OBJS/1/EXTR:EXTR/2");

            Assert.Equal(0, result.ExitCode);

            var masked = MaskPath(result.Stdout, fixturePath);
            GoldenTestRunner.Matches("iff/roundtrip/mutation-leaf-removed", masked);

            // Sanity-check the parsed envelope directly per the round-2 MEDIUM 8 closure spec:
            // byteExact MUST be false (structure changed); byteExactExceptRemovedLeaf MUST be true
            // (every untouched leaf is byte-identical); parentContainerChildCountAfter -
            // parentContainerChildCountBefore == -1 (one leaf removed); untouchedLeafCount = N-1.
            var root = JToken.Parse(result.Stdout);
            Assert.False(root["result"]["byteExact"].Value<bool>());
            Assert.True(root["result"]["byteExactExceptRemovedLeaf"].Value<bool>());
            Assert.Equal(3, root["result"]["parentContainerChildCountBefore"].Value<int>());
            Assert.Equal(2, root["result"]["parentContainerChildCountAfter"].Value<int>());
            Assert.Equal(3, root["result"]["untouchedLeafCount"].Value<int>());
            Assert.Equal("EXTR", root["result"]["removedLeafTypeId"].Value<string>());
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fact: missing file → exit 3 + FileNotFound (exit-code contract mirror).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Run_WithMissingFile_ExitsThreeAndEmitsFileNotFoundError()
        {
            var result = InProcessCliRunner.Run("roundtrip-iff", @"Z:\nonexistent\roundtrip-cannot-exist.iff");

            Assert.Equal(3, result.ExitCode);

            var root = JToken.Parse(result.Stdout);
            Assert.Equal("roundtrip-iff", root["command"].Value<string>());
            Assert.Equal("FileNotFound",  root["error"]["kind"].Value<string>());
        }

        // ─────────────────────────────────────────────────────────────────────
        // Facts: usage-error paths (exit 1) — guard the mutually-exclusive option contract.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Run_WithBothMutateLeafAndRemoveLeaf_ExitsOneWithUsageError()
        {
            var fixturePath = FixturePath.Resolve("iff/roundtrip/synthetic-nested.iff");

            var result = InProcessCliRunner.Run(
                "roundtrip-iff", fixturePath,
                "--mutate-leaf", "FORM:WSNP/0/DATA:DATA/0",
                "--mutate-hex",  "AABBCCDD",
                "--remove-leaf", "FORM:WSNP/0/NAME:NAME/1");

            Assert.Equal(1, result.ExitCode);
            var root = JToken.Parse(result.Stdout);
            Assert.Equal("UsageError", root["error"]["kind"].Value<string>());
        }

        [Fact]
        public void Run_WithMutateLeafButNoMutateHex_ExitsOneWithUsageError()
        {
            var fixturePath = FixturePath.Resolve("iff/roundtrip/synthetic-nested.iff");

            var result = InProcessCliRunner.Run(
                "roundtrip-iff", fixturePath,
                "--mutate-leaf", "FORM:WSNP/0/DATA:DATA/0");

            Assert.Equal(1, result.ExitCode);
            var root = JToken.Parse(result.Stdout);
            Assert.Equal("UsageError", root["error"]["kind"].Value<string>());
        }

        [Fact]
        public void Run_WithUnknownMutateLeafId_ExitsOneWithUsageError()
        {
            var fixturePath = FixturePath.Resolve("iff/roundtrip/synthetic-nested.iff");

            var result = InProcessCliRunner.Run(
                "roundtrip-iff", fixturePath,
                "--mutate-leaf", "NOPE:NOPE/0",
                "--mutate-hex",  "01");

            Assert.Equal(1, result.ExitCode);
            var root = JToken.Parse(result.Stdout);
            Assert.Equal("UsageError", root["error"]["kind"].Value<string>());
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fact: REVIEWS HIGH-6 envelope-shape regression guard (schemaVersion + command at root).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Run_AnyHappyPath_EnvelopeHasTopLevelSchemaVersionAndCommand()
        {
            var fixturePath = FixturePath.Resolve("iff/roundtrip/synthetic-nested.iff");
            var result = InProcessCliRunner.Run("roundtrip-iff", fixturePath);

            Assert.Equal(0, result.ExitCode);
            var root = JToken.Parse(result.Stdout);

            Assert.Equal(1, root["schemaVersion"].Value<int>());
            Assert.Equal("roundtrip-iff", root["command"].Value<string>());
            Assert.Null(root["result"]["schemaVersion"]);
            Assert.Null(root["result"]["command"]);
            Assert.Null(root["error"]);
        }
    }
}
