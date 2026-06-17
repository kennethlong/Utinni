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
using System.IO;
using System.Linq;
using UtinniCoreDotNet.Formats.ClientEffect;
using UtinniCoreDotNet.Formats.Iff;
using Xunit;

namespace UtinniCoreDotNet.Tests.ClientEffect
{
    /// <summary>
    /// ClientEffect (FORM CLEF) codec coverage (filter substring <c>ClientEffect</c>): byte-exact
    /// round-trip across the committed golden corpus + the writer-independent hand-authored hex,
    /// length-ripple string/list/field edits, raw-fallback (unknown version + unknown command),
    /// truncated-known degrade, canonical-payload + consume-all-bytes, CLGT 23-byte no-pad/endianness,
    /// stableId DeriveStableId parity, the D-03 encode-version guard, and the v0003 CPAP load order.
    /// All fixtures are byte-stable (synthesized through IffWriter / the production codec) so the
    /// byte-exact assertions are meaningful.
    /// </summary>
    public class ClientEffectCodecTests
    {
        // ── fixture access ──────────────────────────────────────────────────────

        private static string FixtureDir =>
            Path.Combine(System.AppContext.BaseDirectory, "Fixtures", "ClientEffect");

        private static byte[] LoadFixture(string name)
        {
            string path = Path.Combine(FixtureDir, name);
            Assert.True(File.Exists(path), "Committed golden fixture missing: " + path);
            return File.ReadAllBytes(path);
        }

        public static IEnumerable<object[]> AllFixtures()
        {
            foreach (string name in ClefFixtureFiles.All.Keys)
                yield return new object[] { name };
        }

        // ── committed-golden integrity: file == deterministic builder output ─────

        [Theory]
        [MemberData(nameof(AllFixtures))]
        public void ClientEffect_CommittedGolden_MatchesDeterministicBuilder(string name)
        {
            byte[] onDisk = LoadFixture(name);
            byte[] built = ClefFixtureFiles.All[name]();
            Assert.Equal(built.Length, onDisk.Length);
            Assert.True(built.SequenceEqual(onDisk),
                "Committed golden '" + name + "' drifted from its deterministic builder output.");
        }

        // ── Roundtrip (no edit) over every fixture incl. the hand-authored hex ──

        [Theory]
        [MemberData(nameof(AllFixtures))]
        public void RoundtripClientEffect_NoEdit_ByteExact(string name)
        {
            byte[] source = LoadFixture(name);
            MutableClientEffect effect = ClientEffectDocument.FromBytes(source);
            byte[] serialized = effect.Serialize();
            Assert.Equal(source.Length, serialized.Length);
            Assert.True(source.SequenceEqual(serialized),
                "No-edit roundtrip of '" + name + "' was not byte-exact.");
        }

        // ── Typed decode across versions ────────────────────────────────────────

        [Theory]
        [InlineData("clef_v0001_cpap.iff", "0001")]
        [InlineData("clef_v0002_cpap.iff", "0002")]
        [InlineData("clef_v0003_cpap.iff", "0003")]
        public void ClientEffect_CpapDecode_PerVersionFields(string name, string version)
        {
            MutableClientEffect effect = ClientEffectDocument.FromBytes(LoadFixture(name));
            Assert.False(effect.IsRawPreserved);
            Assert.Equal(version, effect.Version);
            ClientEffectCommand cpap = effect.Commands.Single(c => c.Tag == "CPAP");
            Assert.False(cpap.IsRaw);
            Assert.Equal(ClefFixtureBuilder.AppearanceName, cpap.StringValue);
            Assert.Equal(2.5f, cpap.Floats[0]); // timeInSeconds

            if (version == "0001")
            {
                Assert.Null(cpap.SoftParticleTerminate);
                Assert.Single(cpap.Floats);
            }
            else
            {
                Assert.True(cpap.SoftParticleTerminate);
            }
            if (version == "0003")
            {
                Assert.Equal(5, cpap.Floats.Length); // time + min/max scale + min/max rate
            }
        }

        // ── Load-order: v0003 CPAP field order (REVIEWS reference-validation) ────

        [Fact]
        public void ClefLoadOrder_V0003Cpap_DecodesInSoeFieldOrder()
        {
            // The v0003 fixture encodes time=2.5, minScale=0.5, maxScale=1.5, minRate=1.0, maxRate=2.0
            // in that exact order; the decode must surface them in the SAME order
            // (name, time, softTerminate, minScale, maxScale, minRate, maxRate).
            MutableClientEffect effect = ClientEffectDocument.FromBytes(LoadFixture("clef_v0003_cpap.iff"));
            ClientEffectCommand cpap = effect.Commands.Single(c => c.Tag == "CPAP");
            Assert.Equal(ClefFixtureBuilder.AppearanceName, cpap.StringValue);
            Assert.True(cpap.SoftParticleTerminate);
            Assert.Equal(2.5f, cpap.Floats[0]); // time
            Assert.Equal(0.5f, cpap.Floats[1]); // minScale
            Assert.Equal(1.5f, cpap.Floats[2]); // maxScale
            Assert.Equal(1.0f, cpap.Floats[3]); // minPlaybackRate
            Assert.Equal(2.0f, cpap.Floats[4]); // maxPlaybackRate
        }

        // ── StableId: DeriveStableId parity + field-edit stable / reorder shifts ──

        [Fact]
        public void ClefStableId_EqualsDeriveStableId_AndIsFieldEditStable()
        {
            MutableClientEffect effect = ClientEffectDocument.FromBytes(LoadFixture("clef_v0003_all5.iff"));
            Assert.Equal(5, effect.Commands.Count);

            // Each command's StableId is distinct AND equals DeriveStableId for its leaf's ordinal path.
            string rootId = MutableIffDocument.DeriveStableId(effect.SourceIff.Root, "", 0);
            MutableIffNode versionForm = effect.VersionForm;
            int versionOrdinal = IndexOf(effect.SourceIff.Root, versionForm);
            string versionPrefix = rootId + "/"
                + MutableIffDocument.DeriveStableId(versionForm, "", versionOrdinal) + "/";

            var seen = new HashSet<string>();
            int ordinal = 0;
            foreach (MutableIffNode child in versionForm.Children)
            {
                if (child.Kind != MutableIffNodeKind.Leaf) { ordinal++; continue; }
                string expected = MutableIffDocument.DeriveStableId(child, versionPrefix, ordinal);
                ClientEffectCommand cmd = effect.Commands.Single(c => System.Object.ReferenceEquals(c.Leaf, child));
                Assert.Equal(expected, cmd.StableId);
                Assert.True(seen.Add(cmd.StableId), "StableIds must be distinct per command.");
                ordinal++;
            }

            // Field-edit (no position change) leaves the edited command's StableId UNCHANGED.
            ClientEffectCommand clgt = effect.Commands.Single(c => c.Tag == "CLGT");
            string before = clgt.StableId;
            byte[] newClgt = ClefFieldCodec.EncodeClgt(99, 20, 30, 1.0f, 0.1f, 0.2f, 0.3f, 25.0f);
            effect.EditCommandPayload(clgt.Leaf, newClgt);
            MutableClientEffect reparsed = ClientEffectDocument.FromBytes(effect.Serialize());
            ClientEffectCommand clgtAfter = reparsed.Commands.Single(c => c.Tag == "CLGT");
            Assert.Equal(before, clgtAfter.StableId); // ordinal unchanged under field-edit
            Assert.Equal((byte)99, clgtAfter.Bytes[0]);
        }

        [Fact]
        public void ClefStableId_Reorder_ShiftsOrdinals()
        {
            // Document the contract that justifies 22-02's split verify modes: a reorder CHANGES the
            // ordinal-path ids (unlike a field edit).
            MutableClientEffect effect = ClientEffectDocument.FromBytes(LoadFixture("clef_v0003_all5.iff"));
            ClientEffectCommand first = effect.Commands.First();   // CPAP at ordinal 0
            string firstIdBefore = first.StableId;
            effect.ReorderCommandDown(first.Leaf);                 // CPAP moves to ordinal 1
            MutableClientEffect reparsed = ClientEffectDocument.FromBytes(effect.Serialize());
            ClientEffectCommand cpapAfter = reparsed.Commands.Single(c => c.Tag == "CPAP");
            Assert.NotEqual(firstIdBefore, cpapAfter.StableId);
        }

        // ── Canonical-payload: exact encoder length + consume-all-bytes + version-unchanged ──

        [Fact]
        public void ClefCanonicalPayload_EditedLengthEqualsEncoder_AndVersionUnchanged()
        {
            MutableClientEffect effect = ClientEffectDocument.FromBytes(LoadFixture("clef_v0003_all5.iff"));
            ClientEffectCommand ffbk = effect.Commands.Single(c => c.Tag == "FFBK");

            byte[] newPayload = ClefFieldCodec.EncodeFfbk("forcefeedback/longer-name.ffe", 9, 99.0f);
            effect.EditCommandPayload(ffbk.Leaf, newPayload);

            MutableClientEffect reparsed = ClientEffectDocument.FromBytes(effect.Serialize());
            ClientEffectCommand ffbkAfter = reparsed.Commands.Single(c => c.Tag == "FFBK");

            // The edited command decodes (consume-all-bytes guard fired: it is NOT raw) to the new value.
            Assert.False(ffbkAfter.IsRaw);
            Assert.Equal("forcefeedback/longer-name.ffe", ffbkAfter.StringValue);
            Assert.Equal(9, ffbkAfter.Int32Value);
            Assert.Equal(99.0f, ffbkAfter.Floats[0]);
            // The reparsed version equals the original (no version upgrade on edit).
            Assert.Equal("0003", reparsed.Version);
            // The edited leaf payload length equals the EXACT encoder length for that command.
            Assert.Equal(newPayload.Length, ffbkAfter.Leaf.PayloadLength);
        }

        [Fact]
        public void ClefCanonicalPayload_ResidualBytes_DegradeToRaw()
        {
            // A KNOWN command tag (CAMS) whose payload carries trailing slack must degrade to raw,
            // never silently survive (REVIEWS #7).
            MutableIffNode clef = MutableIffNode.NewContainer("FORM", "CLEF");
            MutableIffNode ver = clef.AddContainer("FORM", "0003");
            byte[] cams = ClefFixtureBuilder.CamsPayload();
            byte[] withSlack = cams.Concat(new byte[] { 0xAA, 0xBB }).ToArray(); // 2 residual bytes
            ver.AddLeaf("CAMS", withSlack);
            byte[] bytes = IffWriter.Write(new MutableIffDocument(clef));

            MutableClientEffect effect = ClientEffectDocument.FromBytes(bytes);
            ClientEffectCommand camsCmd = effect.Commands.Single(c => c.Tag == "CAMS");
            Assert.True(camsCmd.IsRaw);
            // Still byte-exact on roundtrip.
            Assert.True(bytes.SequenceEqual(effect.Serialize()));
        }

        // ── CLGT no-pad / endianness (REVIEWS #7) ───────────────────────────────

        [Fact]
        public void ClefClgtNoPad_PayloadIs23Bytes_LeFloats_BeLengths_NextSiblingImmediate()
        {
            MutableClientEffect effect = ClientEffectDocument.FromBytes(LoadFixture("clef_v0003_all5.iff"));
            ClientEffectCommand clgt = effect.Commands.Single(c => c.Tag == "CLGT");
            Assert.Equal(23, clgt.Leaf.PayloadLength); // 3 uint8 RGB + 5 LE floats, odd -> no pad

            // RGB are one byte each; floats are little-endian (decode matched the encoded values).
            Assert.Equal(new byte[] { 10, 20, 30 }, clgt.Bytes);
            Assert.Equal(1.0f, clgt.Floats[0]);
            Assert.Equal(25.0f, clgt.Floats[4]);

            // Re-read the raw bytes: the chunk immediately following CLGT (CAMS) starts right after the
            // 23-byte payload (no pad). Locate the CLGT chunk header in the serialized stream and check
            // the next FourCC lands at offset = clgtHeaderEnd + 23.
            byte[] raw = effect.Serialize();
            int clgtTagPos = IndexOfAscii(raw, "CLGT");
            Assert.True(clgtTagPos >= 0);
            // Leaf framing: tag(4) + BE len(4) + payload(23). Next sibling tag begins at +8+23.
            int nextSiblingPos = clgtTagPos + 8 + 23;
            string nextTag = AsciiAt(raw, nextSiblingPos, 4);
            Assert.Equal("CAMS", nextTag); // immediately after byte 23, NO pad

            // BE length field of the CLGT chunk equals 23.
            int beLen = (raw[clgtTagPos + 4] << 24) | (raw[clgtTagPos + 5] << 16)
                      | (raw[clgtTagPos + 6] << 8) | raw[clgtTagPos + 7];
            Assert.Equal(23, beLen);
        }

        // ── String edit: longer + shorter + middle-command (REVIEWS Cursor #7) ──

        [Theory]
        [InlineData("a/much/longer/appearance/name.prt")]
        [InlineData("x.prt")]
        public void ClefStringEdit_LongerAndShorter_ReparsesUntouchedByteIdentical(string newName)
        {
            MutableClientEffect effect = ClientEffectDocument.FromBytes(LoadFixture("clef_v0003_all5.iff"));

            // Capture the untouched siblings' payloads (CLGT/CAMS/FFBK) before the edit.
            byte[] psndBefore = effect.Commands.Single(c => c.Tag == "PSND").Leaf.GetPayloadCopy();
            byte[] camsBefore = effect.Commands.Single(c => c.Tag == "CAMS").Leaf.GetPayloadCopy();

            ClientEffectCommand cpap = effect.Commands.Single(c => c.Tag == "CPAP");
            byte[] newCpap = ClefFieldCodec.EncodeCpap("0003", newName, 2.5f, true, 0.5f, 1.5f, 1.0f, 2.0f);
            effect.EditCommandPayload(cpap.Leaf, newCpap);

            MutableClientEffect reparsed = ClientEffectDocument.FromBytes(effect.Serialize());
            ClientEffectCommand cpapAfter = reparsed.Commands.Single(c => c.Tag == "CPAP");
            Assert.False(cpapAfter.IsRaw);
            Assert.Equal(newName, cpapAfter.StringValue);

            // Untouched commands are byte-identical.
            Assert.True(psndBefore.SequenceEqual(reparsed.Commands.Single(c => c.Tag == "PSND").Leaf.GetPayloadCopy()));
            Assert.True(camsBefore.SequenceEqual(reparsed.Commands.Single(c => c.Tag == "CAMS").Leaf.GetPayloadCopy()));
        }

        [Fact]
        public void ClefStringEdit_MiddleCommand_FirstAndLastLeavesUnchanged()
        {
            MutableClientEffect effect = ClientEffectDocument.FromBytes(LoadFixture("clef_v0003_all5.iff"));
            // Middle command = CLGT (index 2 of 5). Edit a sound-string-bearing neighbour PSND? No —
            // edit the MIDDLE command itself: replace CLGT, assert FIRST (CPAP) + LAST (FFBK) unchanged.
            byte[] firstBefore = effect.Commands.First().Leaf.GetPayloadCopy();
            byte[] lastBefore = effect.Commands.Last().Leaf.GetPayloadCopy();

            ClientEffectCommand clgt = effect.Commands.Single(c => c.Tag == "CLGT");
            effect.EditCommandPayload(clgt.Leaf, ClefFieldCodec.EncodeClgt(7, 8, 9, 2f, 0f, 0f, 0f, 5f));

            MutableClientEffect reparsed = ClientEffectDocument.FromBytes(effect.Serialize());
            Assert.True(firstBefore.SequenceEqual(reparsed.Commands.First().Leaf.GetPayloadCopy()));
            Assert.True(lastBefore.SequenceEqual(reparsed.Commands.Last().Leaf.GetPayloadCopy()));
        }

        // ── List mutation: add / remove / reorder (REVIEWS D-02) ────────────────

        [Fact]
        public void ClefListMutation_Add_ReparsesAndRerollsLength()
        {
            MutableClientEffect effect = ClientEffectDocument.FromBytes(LoadFixture("clef_v0003_cpap.iff"));
            int before = effect.Commands.Count;
            effect.AddCommand("PSND", ClefFieldCodec.EncodePsnd("sound/added.snd"));
            MutableClientEffect reparsed = ClientEffectDocument.FromBytes(effect.Serialize());
            Assert.Equal(before + 1, reparsed.Commands.Count);
            Assert.Contains(reparsed.Commands, c => c.Tag == "PSND" && c.StringValue == "sound/added.snd");
        }

        [Fact]
        public void ClefListMutation_Remove_ReparsesAndRerollsLength()
        {
            MutableClientEffect effect = ClientEffectDocument.FromBytes(LoadFixture("clef_v0003_all5.iff"));
            ClientEffectCommand psnd = effect.Commands.Single(c => c.Tag == "PSND");
            Assert.True(effect.RemoveCommand(psnd.Leaf));
            MutableClientEffect reparsed = ClientEffectDocument.FromBytes(effect.Serialize());
            Assert.Equal(4, reparsed.Commands.Count);
            Assert.DoesNotContain(reparsed.Commands, c => c.Tag == "PSND");
        }

        [Fact]
        public void ClefListMutation_ReorderDownThenUp_IsByteReversible()
        {
            // A two-command list: down-then-up returns to identity (byte-exact).
            MutableIffNode clef = MutableIffNode.NewContainer("FORM", "CLEF");
            MutableIffNode ver = clef.AddContainer("FORM", "0003");
            ver.AddLeaf("PSND", ClefFieldCodec.EncodePsnd("a.snd"));
            ver.AddLeaf("CAMS", ClefFixtureBuilder.CamsPayload());
            byte[] original = IffWriter.Write(new MutableIffDocument(clef));

            MutableClientEffect effect = ClientEffectDocument.FromBytes(original);
            MutableIffNode psndLeaf = effect.Commands.Single(c => c.Tag == "PSND").Leaf;
            effect.ReorderCommandDown(psndLeaf);
            effect.ReorderCommandUp(psndLeaf);
            byte[] roundTrip = effect.Serialize();
            Assert.True(original.SequenceEqual(roundTrip));
        }

        // ── Field edit: CLGT uint8 channel + CAMS float (byte-exact except target) ──

        [Fact]
        public void ClefFieldEdit_ClgtChannelAndCamsFloat_ReparseToNewValue()
        {
            MutableClientEffect effect = ClientEffectDocument.FromBytes(LoadFixture("clef_v0003_all5.iff"));

            ClientEffectCommand clgt = effect.Commands.Single(c => c.Tag == "CLGT");
            effect.EditCommandPayload(clgt.Leaf,
                ClefFieldCodec.EncodeClgt(200, 20, 30, 1.0f, 0.1f, 0.2f, 0.3f, 25.0f));

            ClientEffectCommand cams = effect.Commands.Single(c => c.Tag == "CAMS");
            effect.EditCommandPayload(cams.Leaf,
                ClefFieldCodec.EncodeCams(9.0f, 4.0f, 1.5f, 12.0f));

            MutableClientEffect reparsed = ClientEffectDocument.FromBytes(effect.Serialize());
            Assert.Equal((byte)200, reparsed.Commands.Single(c => c.Tag == "CLGT").Bytes[0]);
            Assert.Equal(9.0f, reparsed.Commands.Single(c => c.Tag == "CAMS").Floats[0]);
        }

        // ── Raw-fallback: unknown version + unknown command ─────────────────────

        [Fact]
        public void ClefRawFallback_UnknownVersion_IsRawPreservedAndByteExact()
        {
            byte[] source = LoadFixture("clef_unknown_version.iff");
            MutableClientEffect effect = ClientEffectDocument.FromBytes(source);
            Assert.True(effect.IsRawPreserved);
            Assert.Equal("9999", effect.Version);
            Assert.Empty(effect.Commands);
            Assert.True(source.SequenceEqual(effect.Serialize()));
        }

        [Fact]
        public void ClefRawFallback_UnknownCommand_PreservesChunkAndByteExact()
        {
            byte[] source = LoadFixture("clef_unknown_command.iff");
            MutableClientEffect effect = ClientEffectDocument.FromBytes(source);
            Assert.False(effect.IsRawPreserved);
            ClientEffectCommand unknown = effect.Commands.Single(c => c.Tag == "XXXX");
            Assert.True(unknown.IsRaw);
            // The known CPAP sibling still decodes.
            Assert.False(effect.Commands.Single(c => c.Tag == "CPAP").IsRaw);
            Assert.True(source.SequenceEqual(effect.Serialize()));
        }

        // ── Truncated-known degrade (REVIEWS #4) ────────────────────────────────

        [Fact]
        public void ClefTruncatedKnown_CpapDegradesToRaw_AndByteExact()
        {
            byte[] source = LoadFixture("clef_truncated_cpap.iff");
            MutableClientEffect effect = ClientEffectDocument.FromBytes(source); // must NOT throw
            ClientEffectCommand cpap = effect.Commands.Single(c => c.Tag == "CPAP");
            Assert.True(cpap.IsRaw); // truncated mid-string -> raw, never a hard abort
            Assert.True(source.SequenceEqual(effect.Serialize()));
        }

        // ── D-03 encode-version guard (REVIEWS HIGH #2) ─────────────────────────

        [Fact]
        public void ClefEncodeVersionGuard_V0001PlusV0003Field_Throws()
        {
            // Editing a v0001 CPAP and attempting to set a v0003-only field through the codec THROWS.
            var ex = Assert.Throws<ClientEffectParseException>(() =>
                ClefFieldCodec.EncodeCpap("0001", "x.prt", 1.0f, null, 0.5f, null, null, null));
            Assert.Equal(ClientEffectParseError.VersionFieldMismatch, ex.Kind);
        }

        [Fact]
        public void ClefEncodeVersionGuard_V0001PlusV0002Field_Throws()
        {
            var ex = Assert.Throws<ClientEffectParseException>(() =>
                ClefFieldCodec.EncodeCpap("0001", "x.prt", 1.0f, true, null, null, null, null));
            Assert.Equal(ClientEffectParseError.VersionFieldMismatch, ex.Kind);
        }

        // ── Container-framing malformation DOES throw (REVIEWS #4) ──────────────

        [Fact]
        public void ClientEffect_WrongRootForm_Throws()
        {
            MutableIffNode notClef = MutableIffNode.NewContainer("FORM", "PEFT");
            notClef.AddContainer("FORM", "0003");
            byte[] bytes = IffWriter.Write(new MutableIffDocument(notClef));
            Assert.Throws<ClientEffectParseException>(() => ClientEffectDocument.FromBytes(bytes));
        }

        // ── Writer-independent hand-authored hex (REVIEWS #6) ───────────────────

        [Fact]
        public void ClefHandAuthored_HexDecodesToExpectedClgt_AndByteExact()
        {
            byte[] source = LoadFixture("clef_v0003_clgt_handauthored.hex");
            MutableClientEffect effect = ClientEffectDocument.FromBytes(source);
            Assert.False(effect.IsRawPreserved);
            Assert.Equal("0003", effect.Version);
            ClientEffectCommand clgt = effect.Commands.Single(c => c.Tag == "CLGT");
            Assert.False(clgt.IsRaw);
            Assert.Equal(new byte[] { 1, 2, 3 }, clgt.Bytes);
            Assert.Equal(1.0f, clgt.Floats[0]);
            Assert.Equal(0.0f, clgt.Floats[1]);
            Assert.Equal(50.0f, clgt.Floats[4]); // range
            Assert.Equal(23, clgt.Leaf.PayloadLength);
            Assert.True(source.SequenceEqual(effect.Serialize()));
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        private static int IndexOf(MutableIffNode parent, MutableIffNode child)
        {
            for (int i = 0; i < parent.Children.Count; i++)
                if (System.Object.ReferenceEquals(parent.Children[i], child)) return i;
            return 0;
        }

        private static int IndexOfAscii(byte[] haystack, string needle)
        {
            byte[] n = needle.Select(c => (byte)c).ToArray();
            for (int i = 0; i + n.Length <= haystack.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < n.Length; j++)
                    if (haystack[i + j] != n[j]) { match = false; break; }
                if (match) return i;
            }
            return -1;
        }

        private static string AsciiAt(byte[] data, int offset, int len)
        {
            var chars = new char[len];
            for (int i = 0; i < len; i++) chars[i] = (char)data[offset + i];
            return new string(chars);
        }
    }
}
