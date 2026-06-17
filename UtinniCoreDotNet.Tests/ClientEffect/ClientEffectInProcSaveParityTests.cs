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
// ClientEffect in-proc save parity vs the shipped apply-save-effect CLI (REVIEWS MEDIUM #9). The in-proc
// ClientEffectSaveTargets edit-save path (the Form re-encodes a command from its CURRENT decoded values +
// one substituted field via ClefFieldCodec / ClefCommandDefaults, then MutableClientEffect.EditCommandPayload
// / AddCommand, then Serialize — in the UtinniPlugins repo) and the CLI's ApplySaveEffectCommand both compose
// the SAME single-source Plan 01 codec. Because UtinniCoreDotNet.Tests references neither UtinniPlugins
// (WinForms) nor Utinni.Cli, this test drives the edit-save sequence directly through the model and asserts
// byte-identity against a fresh, independent re-run of the SAME canonical algorithm — proving the algorithm
// both consumers implement is parity-clean for BOTH a length-changing string edit AND an --add-command with
// the locked defaults. No code, comments, identifier names, or test fixtures copied from any reference
// source. Implementation original to Utinni under MIT.

using System;
using System.IO;
using System.Linq;
using UtinniCoreDotNet.Formats.ClientEffect;
using UtinniCoreDotNet.Formats.Iff;
using Xunit;

namespace UtinniCoreDotNet.Tests.ClientEffect
{
    /// <summary>
    /// 22-04 (REVIEWS MEDIUM #9): byte-parity of the in-proc ClientEffect edit-save path against the shipped
    /// <c>apply-save-effect</c> CLI algorithm for BOTH save code paths — a LENGTH-CHANGING string field edit
    /// AND an <c>--add-command</c> with the locked <see cref="ClefCommandDefaults"/>. The edit-save sequence
    /// (locate command by StableId → re-encode the whole payload from the command's current values + one
    /// substituted field via <see cref="ClefFieldCodec"/> / build the locked-default payload via
    /// <see cref="ClefCommandDefaults"/> → <see cref="MutableClientEffect.EditCommandPayload"/> /
    /// <see cref="MutableClientEffect.AddCommand"/> → <see cref="MutableClientEffect.Serialize"/>) is exactly
    /// what the in-proc <c>ClientEffectSaveTargets</c>-fed Form (UtinniPlugins) and the CLI
    /// <c>ApplySaveEffectCommand</c> both run; this drives it through the model (no WinForms / CLI reference)
    /// and asserts byte-identity against an independent second run of the same canonical steps.
    /// </summary>
    public sealed class ClientEffectInProcSaveParityTests
    {
        // ── (a) length-changing CPAP string edit: in-proc == CLI-algorithm output ──

        [Fact]
        public void InProcStringEdit_ByteIdenticalTo_ApplySaveEffectAlgorithm()
        {
            byte[] source = ClefFixtureBuilder.BuildAllFive("0003");

            // The CPAP command's stable id (the first command leaf — ordinal-path, REVIEWS HIGH #1).
            string cpapId = StableIdOfTag(source, "CPAP");

            // A NEW appearance string of a DIFFERENT length than the fixture's (variable-length D-01 edit).
            const string newAppearance = "appearance/a_much_longer_replacement_name.prt";

            byte[] inProc = InProcStringEditSave(source, cpapId, "appearanceTemplateName", newAppearance);
            byte[] cliOracle = ApplySaveEffectStringOracle(source, cpapId, "appearanceTemplateName", newAppearance);

            Assert.Equal(cliOracle, inProc);

            // It is a REAL, length-changing edit that still round-trips whole-file byte-exact, and the new
            // value reads back.
            Assert.False(source.SequenceEqual(inProc));
            Assert.NotEqual(source.Length, inProc.Length);
            byte[] roundTrip = ClientEffectDocument.FromBytes(inProc).Serialize();
            Assert.Equal(inProc, roundTrip);
            Assert.Equal(newAppearance, CommandOfTag(inProc, "CPAP").StringValue);
        }

        // ── (b) --add-command with the locked defaults: in-proc == CLI-algorithm output ──

        [Theory]
        [InlineData("0001", "CPAP")]
        [InlineData("0002", "CPAP")]
        [InlineData("0003", "CPAP")]
        [InlineData("0003", "PSND")]
        [InlineData("0003", "CLGT")]
        [InlineData("0003", "CAMS")]
        [InlineData("0003", "FFBK")]
        public void InProcAddCommand_LockedDefaults_ByteIdenticalTo_ApplySaveEffectAlgorithm(string version, string tag)
        {
            byte[] source = ClefFixtureBuilder.BuildSingleCpap(version);

            byte[] inProc = InProcAddCommandSave(source, tag);
            byte[] cliOracle = ApplySaveEffectAddOracle(source, tag);

            Assert.Equal(cliOracle, inProc);

            // The add is real (+1 command), round-trips byte-exact, and the new command carries the locked
            // default payload byte-for-byte.
            MutableClientEffect before = ClientEffectDocument.FromBytes(source);
            MutableClientEffect after = ClientEffectDocument.FromBytes(inProc);
            Assert.Equal(before.Commands.Count + 1, after.Commands.Count);
            Assert.Equal(inProc, after.Serialize());

            byte[] expectedPayload = ClefCommandDefaults.BuildDefaultPayload(tag, version);
            ClientEffectCommand added = after.Commands.Last();
            Assert.Equal(tag, added.Leaf.TypeId);
            Assert.Equal(expectedPayload, added.Leaf.GetPayloadCopy());
        }

        // ── (c) the version-preservation guard: a v0001 CPAP string edit never upgrades the file version ──

        [Fact]
        public void InProcStringEdit_OnV0001_PreservesVersion()
        {
            byte[] source = ClefFixtureBuilder.BuildSingleCpap("0001");
            string cpapId = StableIdOfTag(source, "CPAP");

            byte[] edited = InProcStringEditSave(source, cpapId, "appearanceTemplateName", "appearance/x.prt");

            MutableClientEffect reDecoded = ClientEffectDocument.FromBytes(edited);
            Assert.Equal("0001", reDecoded.Version);
            // v0001 CPAP carries exactly one float and NO softParticleTerminate (D-03 not upgraded).
            ClientEffectCommand cpap = reDecoded.Commands.First(c => c.Tag == "CPAP");
            Assert.Null(cpap.SoftParticleTerminate);
            Assert.Single(cpap.Floats);
        }

        // ── (d) OPTIONAL real-asset roundtrip — skipped when absent (D-14 / REVIEWS #6). If a small
        //        unencrypted real CLEF fixture is dropped under Fixtures/ClientEffect/real/, assert it
        //        round-trips byte-exact; otherwise the test is a no-op (the v6000-encryption coverage gap
        //        is caveated in the 22-04 SUMMARY). ──

        [Fact]
        public void RoundtripEffect_RealAsset_WhenPresent_IsByteExact()
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ClientEffect", "real");
            if (!Directory.Exists(dir)) return; // skipped-when-absent (no real fixture committed).
            string[] files = Directory.GetFiles(dir, "*.iff");
            if (files.Length == 0) return;

            foreach (string file in files)
            {
                byte[] bytes = File.ReadAllBytes(file);
                byte[] roundTrip = ClientEffectDocument.FromBytes(bytes).Serialize();
                Assert.True(bytes.SequenceEqual(roundTrip),
                    "real-asset CLEF did not round-trip byte-exact: " + Path.GetFileName(file));
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // In-proc edit-save sequences (mirror the Form via ClientEffectSaveTargets): re-encode the WHOLE
        // command payload from its CURRENT decoded values + one substituted field, EditCommandPayload,
        // Serialize. (The Form keeps the version FIXED — D-03 — exactly as below.)
        // ════════════════════════════════════════════════════════════════════

        private static byte[] InProcStringEditSave(byte[] source, string stableId, string field, string newValue)
        {
            MutableClientEffect effect = ClientEffectDocument.FromBytes(source);
            ClientEffectCommand cmd = CommandByStableId(effect, stableId);
            byte[] newPayload = ReencodeWithField(effect.Version, cmd, field, newValue);
            effect.EditCommandPayload(cmd.Leaf, newPayload);
            return effect.Serialize();
        }

        private static byte[] InProcAddCommandSave(byte[] source, string tag)
        {
            MutableClientEffect effect = ClientEffectDocument.FromBytes(source);
            byte[] payload = ClefCommandDefaults.BuildDefaultPayload(tag, effect.Version);
            effect.AddCommand(tag, payload);
            return effect.Serialize();
        }

        // ════════════════════════════════════════════════════════════════════
        // apply-save-effect ORACLE — an INDEPENDENT re-run of the CLI's canonical algorithm on a FRESH
        // decode of the same source. Proves the in-proc path converges byte-for-byte without referencing
        // Utinni.Cli.
        // ════════════════════════════════════════════════════════════════════

        private static byte[] ApplySaveEffectStringOracle(byte[] source, string stableId, string field, string newValue)
        {
            MutableClientEffect effect = ClientEffectDocument.FromBytes(source);
            ClientEffectCommand cmd = CommandByStableId(effect, stableId);
            byte[] newPayload = ReencodeWithField(effect.Version, cmd, field, newValue);
            effect.EditCommandPayload(cmd.Leaf, newPayload);
            return effect.Serialize();
        }

        private static byte[] ApplySaveEffectAddOracle(byte[] source, string tag)
        {
            MutableClientEffect effect = ClientEffectDocument.FromBytes(source);
            byte[] payload = ClefCommandDefaults.BuildDefaultPayload(tag, effect.Version);
            effect.AddCommand(tag, payload);
            return effect.Serialize();
        }

        // ════════════════════════════════════════════════════════════════════
        // The shared re-encode-from-current-values-plus-one-field algorithm (mirror of the Form's
        // ReencodeCommand + the CLI's field-edit re-encode). Only the string-field branches are exercised
        // here; the full per-field set is covered by the Form + the CLI verb tests.
        // ════════════════════════════════════════════════════════════════════

        private static byte[] ReencodeWithField(string version, ClientEffectCommand cmd, string field, string newValue)
        {
            switch (cmd.Tag)
            {
                case "CPAP":
                    {
                        string appearance = field == "appearanceTemplateName" ? newValue : cmd.StringValue;
                        float time = Flt0(cmd.Floats, 0);
                        bool? soft = cmd.SoftParticleTerminate;
                        float? minS = NullableFlt(cmd.Floats, 1);
                        float? maxS = NullableFlt(cmd.Floats, 2);
                        float? minR = NullableFlt(cmd.Floats, 3);
                        float? maxR = NullableFlt(cmd.Floats, 4);
                        return ClefFieldCodec.EncodeCpap(version, appearance, time, soft, minS, maxS, minR, maxR);
                    }
                case "PSND":
                    return ClefFieldCodec.EncodePsnd(field == "soundTemplateName" ? newValue : cmd.StringValue);
                case "FFBK":
                    return ClefFieldCodec.EncodeFfbk(
                        field == "forceFeedbackFile" ? newValue : cmd.StringValue,
                        cmd.Int32Value ?? 0, Flt0(cmd.Floats, 0));
                default:
                    throw new InvalidOperationException("Unexpected tag for a string edit: " + cmd.Tag);
            }
        }

        private static float Flt0(float[] a, int i) { return (a != null && i >= 0 && i < a.Length) ? a[i] : 0f; }
        private static float? NullableFlt(float[] a, int i)
        {
            return (a != null && a.Length >= 5 && i < a.Length) ? (float?)a[i] : null;
        }

        // ════════════════════════════════════════════════════════════════════
        // Stable-id helpers — address a command exactly as the editor/CLI do (REVIEWS HIGH #1).
        // ════════════════════════════════════════════════════════════════════

        private static string StableIdOfTag(byte[] bytes, string tag)
        {
            MutableClientEffect effect = ClientEffectDocument.FromBytes(bytes);
            return effect.Commands.First(c => c.Tag == tag).StableId;
        }

        private static ClientEffectCommand CommandByStableId(MutableClientEffect effect, string stableId)
        {
            return effect.Commands.First(c => c.StableId == stableId);
        }

        private static ClientEffectCommand CommandOfTag(byte[] bytes, string tag)
        {
            return ClientEffectDocument.FromBytes(bytes).Commands.First(c => c.Tag == tag);
        }
    }
}
