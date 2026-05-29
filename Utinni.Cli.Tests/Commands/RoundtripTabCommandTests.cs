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
    /// Tier-2 golden tests for the <c>roundtrip-tab</c> CLI command — the CF-02 max-harness for the
    /// DTII (.tab) write path and the automated gate for Phase 9 Success Criterion 4 ("no silent schema
    /// corruption"). The analog of <see cref="RoundtripIffCommandTests"/>.
    ///
    /// <para>The SC4 structural gate is the per-cell ROWS-slice byte-exact-on-untouched-cells assertion
    /// after a <c>--mutate-cell</c> edit. The one-cell-differs golden proves the comparison isolates a
    /// single changed cell without false-positiving on neighbors. The non-optional drift detector
    /// proves the CLI-side <see cref="DataTableFixtureBuilder"/> still emits a canonical fixture that
    /// stays in lock-step with the 09-01 framework builder.</para>
    ///
    /// <para>Naming convention: [Method]_[Scenario]_[ExpectedOutcome] (Phase 1 D-04).</para>
    /// </summary>
    public class RoundtripTabCommandTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Temp-file IDisposable wrapper (Phase 4 convention; hand-rolled here so the
        // test file is self-contained).
        // ─────────────────────────────────────────────────────────────────────
        private sealed class TempTab : IDisposable
        {
            public string Path { get; }

            public TempTab(byte[] bytes)
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "utinni-roundtrip-tab-" + Guid.NewGuid().ToString("N") + ".tab");
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

        private static JToken RunAndParse(out int exitCode, params string[] args)
        {
            var result = InProcessCliRunner.Run(args);
            exitCode = result.ExitCode;
            return JToken.Parse(result.Stdout);
        }

        // Masks the absolute (temp) source path in the CLI's JSON output with the stable sentinel used
        // in the committed Goldens/roundtrip-tab/*.json files (mirrors the Phase 8 MaskPath idiom).
        private static string MaskPath(string actualJson, string path)
        {
            string escaped = path.Replace("\\", "\\\\");
            return actualJson.Replace(escaped, "<fixture-path>").Replace(path, "<fixture-path>");
        }

        // Compares the masked CLI stdout against a committed golden envelope (sorted-key JSON) by
        // JToken.DeepEquals — the goldens are committed artifacts (this plan's files_modified) so a
        // drift in the envelope shape fails CI.
        private static void AssertGolden(string goldenName, string maskedStdout)
        {
            string goldenPath = Path.Combine(AppContext.BaseDirectory, "Goldens", "roundtrip-tab", goldenName + ".json");
            string expectedRaw = File.ReadAllText(goldenPath).Replace("\r\n", "\n").Replace("\r", "\n");
            JToken expected = JToken.Parse(expectedRaw);
            JToken actual = JToken.Parse(maskedStdout.Replace("\r\n", "\n").Replace("\r", "\n"));
            Assert.True(JToken.DeepEquals(expected, actual),
                "Golden mismatch for " + goldenName + ".\nExpected:\n" + expected + "\n\nActual:\n" + actual);
        }

        // ─────────────────────────────────────────────────────────────────────
        // No-mutation round-trip — whole-file byte-exact identity (canonical fixtures).
        // One [Fact] per fixture (6 facts).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Roundtrip_NoMutate_BuildV0Minimal_ExitsZero_BytesEqualUntouched()
        {
            AssertNoMutateWholeFileExact(DataTableFixtureBuilder.BuildV0Minimal(), "0000", "v0-minimal");
        }

        [Fact]
        public void Roundtrip_NoMutate_BuildV1Minimal_ExitsZero_BytesEqualUntouched()
        {
            AssertNoMutateWholeFileExact(DataTableFixtureBuilder.BuildV1Minimal(), "0001", "v1-minimal");
        }

        [Fact]
        public void Roundtrip_NoMutate_BuildV1AllTypes_ExitsZero_BytesEqualUntouched()
        {
            AssertNoMutateWholeFileExact(DataTableFixtureBuilder.BuildV1AllTypes(), "0001", "v1-all-types");
        }

        [Fact]
        public void Roundtrip_NoMutate_BuildV1WithDefaultsAndEnums_ExitsZero_BytesEqualUntouched()
        {
            AssertNoMutateWholeFileExact(DataTableFixtureBuilder.BuildV1WithDefaultsAndEnums(), "0001", "v1-defaults-and-enums");
        }

        [Fact]
        public void Roundtrip_NoMutate_BuildV1WithComment_ExitsZero_BytesEqualUntouched()
        {
            AssertNoMutateWholeFileExact(DataTableFixtureBuilder.BuildV1WithComment(), "0001", "v1-with-comment");
        }

        [Fact]
        public void Roundtrip_NoMutate_BuildV1EmptyTable_ExitsZero_BytesEqualUntouched()
        {
            AssertNoMutateWholeFileExact(DataTableFixtureBuilder.BuildV1EmptyTable(), "0001", "v1-empty-table");
        }

        private static void AssertNoMutateWholeFileExact(byte[] tab, string expectedVersion, string goldenName)
        {
            using (var tmp = new TempTab(tab))
            {
                var result = InProcessCliRunner.Run("roundtrip-tab", tmp.Path);
                var root = JToken.Parse(result.Stdout);

                Assert.Equal(0, result.ExitCode);
                Assert.Equal("roundtrip-tab", root["command"].Value<string>());
                Assert.True(root["result"]["bytesEqualUntouched"].Value<bool>());
                Assert.Equal("whole-file", root["result"]["comparisonGranularity"].Value<string>());
                Assert.Equal("none", root["result"]["mutationApplied"].Value<string>());
                Assert.Equal(expectedVersion, root["result"]["version"].Value<string>());

                // The on-disk bytes equal the serialized bytes for a canonical fixture — confirm the
                // writer is a true identity on the no-mutate path.
                byte[] onDisk = File.ReadAllBytes(tmp.Path);
                byte[] reserialized = ReserializeViaWriter(onDisk);
                Assert.Equal(onDisk, reserialized);

                // Committed golden envelope comparison (this plan's files_modified artifacts).
                AssertGolden(goldenName, MaskPath(result.Stdout, tmp.Path));
            }
        }

        private static byte[] ReserializeViaWriter(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                var iff = UtinniCoreDotNet.Formats.Iff.IffReader.Read(ms);
                var mutableIff = UtinniCoreDotNet.Formats.Iff.MutableIffDocument.FromDocument(iff, bytes);
                var dt = UtinniCoreDotNet.Formats.Datatable.DataTableDocument.FromIff(mutableIff);
                return new UtinniCoreDotNet.Formats.Datatable.DataTableWriter(dt.Mutable).Serialize();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // SC4 CLI gate: --mutate-cell — untouched cells remain byte-exact (per-cell ROWS-slice).
        // ─────────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "SC4: byte-exact (per-cell rows-slice) on untouched cells after --mutate-cell")]
        public void Roundtrip_MutateCell_BuildV1AllTypes_UntouchedCellsRemainByteExact()
        {
            using (var tmp = new TempTab(DataTableFixtureBuilder.BuildV1AllTypes()))
            {
                var result = InProcessCliRunner.Run(
                    "roundtrip-tab", tmp.Path, "--mutate-cell", "0,0", "--mutate-value", "999");
                var root = JToken.Parse(result.Stdout);

                Assert.Equal(0, result.ExitCode);
                Assert.Equal("mutate-cell(0,0)", root["result"]["mutationApplied"].Value<string>());
                Assert.Equal("per-cell-rows-slice", root["result"]["comparisonGranularity"].Value<string>());
                Assert.True(root["result"]["bytesEqualUntouched"].Value<bool>(),
                    "every cell except (0,0) must re-emit an identical ROWS slice");
                Assert.True(root["result"]["firstMismatch"].Type == JTokenType.Null);

                AssertGolden("v1-all-types-mutate-cell-0-0", MaskPath(result.Stdout, tmp.Path));
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // iter-2 item 3: one-cell-differs isolation golden — the comparison isolates exactly one cell.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Roundtrip_OneCellDiffers_ComparisonIsolatesExactlyOneCell()
        {
            // The differing file already has cell (0,2) = "changed"; feeding the SAME edit through the
            // verb means the verb-mutated cell (0,2) is the ONLY cell whose slice may differ from the
            // re-loaded baseline — every OTHER cell slice must match between loaded and roundtripped.
            byte[] differing = DataTableFixtureBuilder.BuildV1OneCellDiffersFrom(0, 2, "changed");
            using (var tmp = new TempTab(differing))
            {
                var result = InProcessCliRunner.Run(
                    "roundtrip-tab", tmp.Path, "--mutate-cell", "0,2", "--mutate-value", "changed");
                var root = JToken.Parse(result.Stdout);

                Assert.Equal(0, result.ExitCode);
                Assert.Equal("per-cell-rows-slice", root["result"]["comparisonGranularity"].Value<string>());
                Assert.True(root["result"]["bytesEqualUntouched"].Value<bool>(),
                    "the algorithm must isolate the single changed cell and not false-positive on neighbors");
                Assert.True(root["result"]["firstMismatch"].Type == JTokenType.Null);

                AssertGolden("v1-one-cell-differs", MaskPath(result.Stdout, tmp.Path));
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // --remove-row — index-shift map (surviving row r>N -> r-1).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Roundtrip_RemoveRow_BuildV1CombatDataTableLike_ShiftsCellsCorrectly()
        {
            using (var tmp = new TempTab(DataTableFixtureBuilder.BuildV1CombatDataTableLike()))
            {
                int exit;
                var root = RunAndParse(out exit,
                    "roundtrip-tab", tmp.Path, "--remove-row", "50");

                Assert.Equal(0, exit);
                Assert.Equal(199, root["result"]["rows"].Value<int>());
                Assert.Equal("remove-row(50)", root["result"]["mutationApplied"].Value<string>());
                Assert.Equal("per-cell-rows-slice", root["result"]["comparisonGranularity"].Value<string>());
                Assert.True(root["result"]["bytesEqualUntouched"].Value<bool>(),
                    "every surviving row's cell slices must match across the r>50 -> r-1 shift");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // --remove-column — index-shift map (surviving col c>idx -> c-1).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Roundtrip_RemoveColumn_BuildV1AllTypes_ShiftsCellsCorrectly()
        {
            using (var tmp = new TempTab(DataTableFixtureBuilder.BuildV1AllTypes()))
            {
                int exit;
                var root = RunAndParse(out exit,
                    "roundtrip-tab", tmp.Path, "--remove-column", "2");

                Assert.Equal(0, exit);
                Assert.Equal(7, root["result"]["cols"].Value<int>());
                Assert.Equal("remove-column(2)", root["result"]["mutationApplied"].Value<string>());
                Assert.Equal("per-cell-rows-slice", root["result"]["comparisonGranularity"].Value<string>());
                Assert.True(root["result"]["bytesEqualUntouched"].Value<bool>(),
                    "every surviving column's cell slices must match across the c>2 -> c-1 shift");
            }
        }

        [Fact]
        public void Roundtrip_RemoveColumnByName_BuildV1AllTypes_ResolvesAndShifts()
        {
            using (var tmp = new TempTab(DataTableFixtureBuilder.BuildV1AllTypes()))
            {
                int exit;
                var root = RunAndParse(out exit,
                    "roundtrip-tab", tmp.Path, "--remove-column", "aString");

                Assert.Equal(0, exit);
                Assert.Equal(7, root["result"]["cols"].Value<int>());
                // "aString" is column index 2 in BuildV1AllTypes.
                Assert.Equal("remove-column(2)", root["result"]["mutationApplied"].Value<string>());
                Assert.True(root["result"]["bytesEqualUntouched"].Value<bool>());
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Exit-code matrix.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Roundtrip_FileNotFound_ExitsThree()
        {
            int exit;
            var root = RunAndParse(out exit, "roundtrip-tab", @"Z:\nonexistent\roundtrip-cannot-exist.tab");

            Assert.Equal(3, exit);
            Assert.Equal("roundtrip-tab", root["command"].Value<string>());
            Assert.Equal("FileNotFound", root["error"]["kind"].Value<string>());
        }

        [Fact]
        public void Roundtrip_MalformedDtii_ExitsTwo()
        {
            // Take a valid V0001 fixture and corrupt the version FORM tag ("0001" -> "9999") so the
            // typed reader rejects it with DataTableParseException (unsupported version FORM).
            byte[] tab = DataTableFixtureBuilder.BuildV1Minimal();
            CorruptVersionFormTag(tab, "9999");

            using (var tmp = new TempTab(tab))
            {
                int exit;
                var root = RunAndParse(out exit, "roundtrip-tab", tmp.Path);

                Assert.Equal(2, exit);
                Assert.Equal("DataTableParseException", root["error"]["kind"].Value<string>());
            }
        }

        // Overwrite the four ASCII bytes of the inner version FORM's sub-type tag in place. The DTII
        // layout is: "FORM"|len|"DTII"|"FORM"|len|<versionTag>. The first "FORM" sub-type "DTII" sits
        // at offset 8; the inner "FORM" sub-type (the version) sits at offset 20.
        private static void CorruptVersionFormTag(byte[] tab, string newTag)
        {
            const int versionTagOffset = 20;
            byte[] ascii = System.Text.Encoding.ASCII.GetBytes(newTag);
            for (int i = 0; i < 4; i++)
            {
                tab[versionTagOffset + i] = ascii[i];
            }
        }

        [Fact]
        public void Roundtrip_ConflictingMutationFlags_ExitsOne_UsageError()
        {
            using (var tmp = new TempTab(DataTableFixtureBuilder.BuildV1Minimal()))
            {
                int exit;
                var root = RunAndParse(out exit,
                    "roundtrip-tab", tmp.Path, "--mutate-cell", "0,0", "--mutate-value", "1", "--remove-row", "0");

                Assert.Equal(1, exit);
                Assert.Equal("UsageError", root["error"]["kind"].Value<string>());
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Drift detector (iter-2 LOW, NON-OPTIONAL): the CLI-side builder still emits a canonical
        // round-trippable fixture (whole-file byte-exact on no-mutate).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void DataTableFixtureBuilder_BuildV1Minimal_IsCanonicalRoundTrippable()
        {
            using (var tmp = new TempTab(DataTableFixtureBuilder.BuildV1Minimal()))
            {
                int exit;
                var root = RunAndParse(out exit, "roundtrip-tab", tmp.Path);

                Assert.Equal(0, exit);
                Assert.Equal("whole-file", root["result"]["comparisonGranularity"].Value<string>());
                Assert.True(root["result"]["bytesEqualUntouched"].Value<bool>(),
                    "the duplicated CLI fixture builder must stay canonical (in lock-step with 09-01)");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Envelope-shape regression guard (schemaVersion + command at root).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Roundtrip_AnyHappyPath_EnvelopeHasTopLevelSchemaVersionAndCommand()
        {
            using (var tmp = new TempTab(DataTableFixtureBuilder.BuildV1Minimal()))
            {
                int exit;
                var root = RunAndParse(out exit, "roundtrip-tab", tmp.Path);

                Assert.Equal(0, exit);
                Assert.Equal(1, root["schemaVersion"].Value<int>());
                Assert.Equal("roundtrip-tab", root["command"].Value<string>());
                Assert.Null(root["result"]["schemaVersion"]);
                Assert.Null(root["result"]["command"]);
                Assert.Null(root["error"]);
            }
        }
    }
}
