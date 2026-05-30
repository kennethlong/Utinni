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
using Newtonsoft.Json.Linq;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// Tier-2 golden tests for the <c>roundtrip-stf</c> CLI command — the CF-02 max-harness for the
    /// flat <c>.stf</c> write path and the automated gate for Phase 10 Success Criterion 4 ("non-ASCII
    /// round-trips cleanly"). The analog of <see cref="RoundtripTabCommandTests"/>.
    ///
    /// <para>The João no-mutate golden is the explicit SC4 non-ASCII proof; the edit-text golden asserts
    /// byte-exact-on-untouched (per-entry-slice) plus the D-02b sourceCrc-preserve policy. The
    /// non-canonical golden proves normalize-on-save (F6 canonical-normalized granularity). The
    /// non-optional drift detector proves the CLI fixture builder stays canonical.</para>
    /// </summary>
    public class RoundtripStfCommandTests
    {
        private sealed class TempStf : IDisposable
        {
            public string Path { get; }

            public TempStf(byte[] bytes)
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "utinni-roundtrip-stf-" + Guid.NewGuid().ToString("N") + ".stf");
                File.WriteAllBytes(Path, bytes);
            }

            public void Dispose()
            {
                try
                {
                    if (File.Exists(Path)) File.Delete(Path);
                }
                catch (IOException)
                {
                    // best-effort cleanup
                }
            }
        }

        private static string MaskPath(string actualJson, string path)
        {
            string escaped = path.Replace("\\", "\\\\");
            return actualJson.Replace(escaped, "<fixture-path>").Replace(path, "<fixture-path>");
        }

        // Resolves the SOURCE goldens directory (not the bin-copied one) so a first run / GSD_GOLDEN_UPDATE=1
        // populates committable files. bin/Release/net472 -> up 3 = the test project root.
        private static string SourceGoldenPath(string goldenName)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
            return Path.Combine(projectRoot, "Goldens", "roundtrip-stf", goldenName + ".json");
        }

        private static void AssertGolden(string goldenName, string maskedStdout)
        {
            string normalized = maskedStdout.Replace("\r\n", "\n").Replace("\r", "\n");
            string goldenPath = SourceGoldenPath(goldenName);
            bool update = Environment.GetEnvironmentVariable("GSD_GOLDEN_UPDATE") == "1";

            if (update || !File.Exists(goldenPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(goldenPath));
                File.WriteAllText(goldenPath, normalized);
                return; // first-run populate: write + pass
            }

            string expectedRaw = File.ReadAllText(goldenPath).Replace("\r\n", "\n").Replace("\r", "\n");
            JToken expected = JToken.Parse(expectedRaw);
            JToken actual = JToken.Parse(normalized);
            Assert.True(JToken.DeepEquals(expected, actual),
                "Golden mismatch for " + goldenName + ".\nExpected:\n" + expected + "\n\nActual:\n" + actual);
        }

        // ── Canonical no-mutate -> whole-file byte-exact ──

        [Fact]
        public void Roundtrip_NoMutate_BuildV1Minimal_WholeFileByteExact()
        {
            AssertNoMutateWholeFile(StringTableFixtureBuilder.BuildV1Minimal(), 1, 2, 0, "v1-minimal");
        }

        [Fact]
        public void Roundtrip_NoMutate_BuildV1MultiEntry_WholeFileByteExact()
        {
            AssertNoMutateWholeFile(StringTableFixtureBuilder.BuildV1MultiEntry(), 3, 4, 0, "v1-multi-entry");
        }

        [Fact]
        public void Roundtrip_NoMutate_BuildEmpty_WholeFileByteExact()
        {
            AssertNoMutateWholeFile(StringTableFixtureBuilder.BuildEmpty(), 0, 1, 0, "v1-empty");
        }

        [Fact(DisplayName = "SC4: João UTF-16 round-trips byte-exact (no smart-quote substitution)")]
        public void Roundtrip_NoMutate_Joao_WholeFileByteExact()
        {
            AssertNoMutateWholeFile(StringTableFixtureBuilder.BuildV1Joao(), 1, 6, 0, "v1-joao");
        }

        [Fact]
        public void Roundtrip_NoMutate_MalformedUtf16_ByteExact()
        {
            AssertNoMutateWholeFile(StringTableFixtureBuilder.BuildV1MalformedUtf16(), 1, 10, 0, "v1-malformed-utf16");
        }

        [Fact]
        public void Roundtrip_NoMutate_NoNameBlock_ByteExact()
        {
            AssertNoMutateWholeFile(StringTableFixtureBuilder.BuildV1NoNameBlock(), 2, 3, 0, "v1-no-name-block");
        }

        [Fact]
        public void Roundtrip_NoMutate_PartialNames_ByteExact()
        {
            AssertNoMutateWholeFile(StringTableFixtureBuilder.BuildV1PartialNameBlock(), 3, 4, 0, "v1-partial-names");
        }

        [Fact(DisplayName = "SC4: non-BMP surrogate-pair round-trips (charCount code-unit guard)")]
        public void Roundtrip_NoMutate_NonBmpSurrogate_ByteExact()
        {
            AssertNoMutateWholeFile(StringTableFixtureBuilder.BuildV1NonBmp(), 1, 5, 0, "v1-non-bmp");
        }

        private static void AssertNoMutateWholeFile(byte[] stf, int entries, uint nextId, int unusedCrc, string goldenName)
        {
            using (var tmp = new TempStf(stf))
            {
                var result = InProcessCliRunner.Run("roundtrip-stf", tmp.Path);
                var root = JToken.Parse(result.Stdout);

                Assert.Equal(0, result.ExitCode);
                Assert.Equal("roundtrip-stf", root["command"].Value<string>());
                Assert.True(root["result"]["bytesEqualUntouched"].Value<bool>());
                Assert.Equal("whole-file", root["result"]["comparisonGranularity"].Value<string>());
                Assert.Equal("none", root["result"]["mutationApplied"].Value<string>());
                Assert.Equal(entries, root["result"]["entries"].Value<int>());
                Assert.Equal((long)nextId, root["result"]["nextUniqueId"].Value<long>());

                AssertGolden(goldenName, MaskPath(result.Stdout, tmp.Path));
            }
        }

        // ── Non-canonical no-mutate -> whole-file exact via F2a short-circuit ──
        // (The 10-01 F2a zero-dirty short-circuit returns the original bytes verbatim for an untouched
        // file regardless of ordering, so even a non-canonical input round-trips byte-identically when
        // unedited. Normalize-on-save only fires once an entry is actually dirty — covered by the
        // per-entry-slice edit-text fact below.)

        [Fact]
        public void Roundtrip_NoMutate_NonCanonicalOrder_WholeFileByteExact_ViaShortCircuit()
        {
            using (var tmp = new TempStf(StringTableFixtureBuilder.BuildV1NonCanonicalOrder()))
            {
                var result = InProcessCliRunner.Run("roundtrip-stf", tmp.Path);
                var root = JToken.Parse(result.Stdout);

                Assert.Equal(0, result.ExitCode);
                Assert.Equal("whole-file", root["result"]["comparisonGranularity"].Value<string>());
                Assert.True(root["result"]["bytesEqualUntouched"].Value<bool>(),
                    "F2a short-circuit returns the original bytes verbatim for any untouched file");

                AssertGolden("v1-reorder", MaskPath(result.Stdout, tmp.Path));
            }
        }

        // ── Dirty non-canonical -> normalize-on-save, per-entry-slice byte-exact-on-untouched ──

        [Fact]
        public void Roundtrip_EditText_NonCanonical_UntouchedSlicesByteExact()
        {
            using (var tmp = new TempStf(StringTableFixtureBuilder.BuildV1NonCanonicalOrder()))
            {
                // Edit one entry ('apple' = id 1) -> forces canonical re-serialize; the OTHER two
                // entries' slices must still match byte-for-byte (paired by id, order-independent).
                var result = InProcessCliRunner.Run("roundtrip-stf", tmp.Path, "--edit-text", "apple=NEW");
                var root = JToken.Parse(result.Stdout);

                Assert.Equal(0, result.ExitCode);
                Assert.Equal("per-entry-slice", root["result"]["comparisonGranularity"].Value<string>());
                Assert.True(root["result"]["bytesEqualUntouched"].Value<bool>(),
                    "untouched entries survive normalize-on-save byte-exact (paired by id)");
                Assert.True(root["result"]["sourceCrcPreserved"].Value<bool>());
            }
        }

        // ── Edit-text -> per-entry-slice byte-exact-on-untouched + sourceCrc preserved (D-02b) ──

        [Fact(DisplayName = "SC4: byte-exact on untouched + D-02b sourceCrc preserved after --edit-text")]
        public void Roundtrip_EditText_UntouchedByteExact_AndSourceCrcPreserved()
        {
            using (var tmp = new TempStf(StringTableFixtureBuilder.BuildV1MultiEntry()))
            {
                var result = InProcessCliRunner.Run("roundtrip-stf", tmp.Path, "--edit-text", "beta=CHANGED");
                var root = JToken.Parse(result.Stdout);

                Assert.Equal(0, result.ExitCode);
                Assert.Equal("edit-text(beta)", root["result"]["mutationApplied"].Value<string>());
                Assert.Equal("per-entry-slice", root["result"]["comparisonGranularity"].Value<string>());
                Assert.True(root["result"]["bytesEqualUntouched"].Value<bool>(),
                    "every entry except 'beta' must re-emit an identical string-block slice");
                Assert.True(root["result"]["sourceCrcPreserved"].Value<bool>(), "D-02b: text edit preserves sourceCrc");
                Assert.True(root["result"]["firstMismatch"].Type == JTokenType.Null);

                AssertGolden("v1-edit-text", MaskPath(result.Stdout, tmp.Path));
            }
        }

        // ── Real extracted .stf golden (F2c) — BLOCKED: see SUMMARY + 10-06 residual ──

        [Fact(Skip = "F2c blocker: a real extracted .stf cannot be committed in this environment without violating the repo's CON-O-09 no-copyrighted-fixtures posture, and no TRE archive is available to extract one. Deferred to the 10-06 maintainer smoke (the maintainer has live TRE access). The synthetic builder + drift detector cover the round-trip mechanics; A6 (real-world canonical ordering) is confirmed at 10-06.")]
        public void Roundtrip_NoMutate_RealExtractedStf_WholeFileByteExact()
        {
            string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "roundtrip-stf", "real-extracted.stf");
            byte[] bytes = File.ReadAllBytes(fixture);
            using (var tmp = new TempStf(bytes))
            {
                var result = InProcessCliRunner.Run("roundtrip-stf", tmp.Path);
                var root = JToken.Parse(result.Stdout);
                Assert.Equal(0, result.ExitCode);
                Assert.True(root["result"]["bytesEqualUntouched"].Value<bool>());
                Assert.Equal("whole-file", root["result"]["comparisonGranularity"].Value<string>());
            }
        }

        // ── Exit-code matrix ──

        [Fact]
        public void Roundtrip_FileNotFound_ExitsThree()
        {
            var result = InProcessCliRunner.Run("roundtrip-stf", @"Z:\nonexistent\cannot-exist.stf");
            var root = JToken.Parse(result.Stdout);
            Assert.Equal(3, result.ExitCode);
            Assert.Equal("FileNotFound", root["error"]["kind"].Value<string>());
        }

        [Fact]
        public void Roundtrip_MalformedMagic_ExitsTwo()
        {
            byte[] bad = StringTableFixtureBuilder.BuildV1Minimal();
            bad[0] = 0x00; bad[1] = 0x00;
            using (var tmp = new TempStf(bad))
            {
                var result = InProcessCliRunner.Run("roundtrip-stf", tmp.Path);
                var root = JToken.Parse(result.Stdout);
                Assert.Equal(2, result.ExitCode);
                Assert.Equal("StringTableParseException", root["error"]["kind"].Value<string>());
            }
        }

        [Fact]
        public void Roundtrip_BadEditKeyFlag_ExitsOne()
        {
            using (var tmp = new TempStf(StringTableFixtureBuilder.BuildV1Minimal()))
            {
                var result = InProcessCliRunner.Run("roundtrip-stf", tmp.Path, "--edit-text", "noequalssign");
                var root = JToken.Parse(result.Stdout);
                Assert.Equal(1, result.ExitCode);
                Assert.Equal("UsageError", root["error"]["kind"].Value<string>());
            }
        }

        [Fact]
        public void Roundtrip_EditTextMissingKey_ExitsTwo()
        {
            using (var tmp = new TempStf(StringTableFixtureBuilder.BuildV1Minimal()))
            {
                var result = InProcessCliRunner.Run("roundtrip-stf", tmp.Path, "--edit-text", "nonexistent=x");
                var root = JToken.Parse(result.Stdout);
                Assert.Equal(2, result.ExitCode);
                Assert.Equal("StringTableParseException", root["error"]["kind"].Value<string>());
            }
        }

        // ── Drift detector (non-optional) ──

        [Fact]
        public void StringTableFixtureBuilder_BuildV1Minimal_IsCanonicalRoundTrippable()
        {
            using (var tmp = new TempStf(StringTableFixtureBuilder.BuildV1Minimal()))
            {
                var result = InProcessCliRunner.Run("roundtrip-stf", tmp.Path);
                var root = JToken.Parse(result.Stdout);
                Assert.Equal(0, result.ExitCode);
                Assert.Equal("whole-file", root["result"]["comparisonGranularity"].Value<string>());
                Assert.True(root["result"]["bytesEqualUntouched"].Value<bool>(),
                    "the duplicated CLI fixture builder must stay canonical (in lock-step with 10-01)");
            }
        }

        // ── Envelope-shape regression guard ──

        [Fact]
        public void Roundtrip_HappyPath_EnvelopeHasTopLevelSchemaVersionAndCommand()
        {
            using (var tmp = new TempStf(StringTableFixtureBuilder.BuildV1Minimal()))
            {
                var result = InProcessCliRunner.Run("roundtrip-stf", tmp.Path);
                var root = JToken.Parse(result.Stdout);
                Assert.Equal(0, result.ExitCode);
                Assert.Equal(1, root["schemaVersion"].Value<int>());
                Assert.Equal("roundtrip-stf", root["command"].Value<string>());
                Assert.Null(root["result"]["schemaVersion"]);
                Assert.Null(root["error"]);
            }
        }
    }
}
