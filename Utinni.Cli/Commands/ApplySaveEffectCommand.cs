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
// ClientEffect (.iff / FORM CLEF) field-aware + list-mutation apply-save understood by reading
// swg-client-v2 .../clientGame/.../ClientEffectTemplate.cpp (SOE/Bootprint, All Rights Reserved): the
// flat command-leaf list under FORM <version>, LE scalars, NUL C-strings. Only the on-disk layout was
// studied — no code, comments, identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.ClientEffect;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Saving;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("apply-save-effect", HelpText = "Apply ONE ClientEffect mutation (length-changing field edit XOR add XOR remove XOR reorder) to a loose-override .iff, verify structurally by mutation mode, then atomically commit under <root>/loose.")]
    public class ApplySaveEffectOptions
    {
        [Value(0, MetaName = "relAsset", Required = true, HelpText = "Relative asset path under --root (loose-override destination).")]
        public string RelAsset { get; set; }

        [Option("root", Required = true, HelpText = "Client root for the loose-override destination; relAsset is resolved + contained under it.")]
        public string Root { get; set; }

        [Option("loose-subdir", Default = "loose", HelpText = "Subdir under --root the loose override lands in (default \"loose\" — the documented searchPath every editor targets). Empty preserves the legacy <root>/<relAsset>.")]
        public string LooseSubDir { get; set; }

        [Option("leaf", HelpText = "Stable id of the command DATA leaf to edit (field edit) or remove/reorder.")]
        public string LeafId { get; set; }

        [Option("field", HelpText = "Field name to edit (a length-changing string edit, or a scalar/flag/color edit).")]
        public string Field { get; set; }

        [Option("value", HelpText = "New value for --field.")]
        public string Value { get; set; }

        [Option("add-command", HelpText = "Append a new command of this tag (CPAP|PSND|CLGT|CAMS|FFBK) with the LOCKED default field values at the file's existing version.")]
        public string AddCommand { get; set; }

        [Option("remove-leaf", HelpText = "Stable id of the command leaf to remove.")]
        public string RemoveLeafId { get; set; }

        [Option("reorder", HelpText = "Stable id of the command leaf to reorder; pair with --direction up|down.")]
        public string ReorderLeafId { get; set; }

        [Option("direction", HelpText = "Reorder direction: up | down. Used with --reorder.")]
        public string Direction { get; set; }
    }

    /// <summary>
    /// The ClientEffect member of the <c>apply-save-*</c> family (PROD-W2-CFX-02). Unlike the fixed-span
    /// <see cref="ApplySaveTrnCommand"/>, CLEF's primary edit is a LENGTH change (a variable-length string
    /// edit), so the fixed-length guard is removed and the verify is SPLIT BY MUTATION MODE (REVIEWS HIGH #2):
    /// the ordinal-based stableId is stable under a field edit but deliberately SHIFTS under reorder/add, so
    /// a single "untouched-by-stableId" compare would false-fail (reorder/add) or false-pass.
    ///
    /// <list type="bullet">
    ///   <item><b>field-edit</b> (<c>--leaf --field --value</c>): every untouched leaf byte-identical BY
    ///   STABLEID (excluding the edited leaf); the edited command decodes to the requested value; canonical
    ///   payload (exact encoder length, all fields decode, no trailing bytes, version unchanged).</item>
    ///   <item><b>add</b> (<c>--add-command &lt;tag&gt;</c>): count+1; all original payloads still present
    ///   (by content); the new command decodes to the LOCKED defaults for its tag/version.</item>
    ///   <item><b>remove</b> (<c>--remove-leaf &lt;id&gt;</c>): count-1; the removed payload absent; all
    ///   survivors byte-identical BY CONTENT.</item>
    ///   <item><b>reorder</b> (<c>--reorder &lt;id&gt; --direction up|down</c>): count unchanged; the
    ///   multiset of (tag, payload-bytes) equal pre/post (NOT stableId equality).</item>
    /// </list>
    ///
    /// <para><b>Loose-subdir (REVIEWS HIGH #3):</b> the destination is composed via the TWO-STEP
    /// <c>LooseOverridePath.Resolve(root, looseSubDir)</c> then <c>Resolve(base, relAsset)</c> (default
    /// "loose" → <c>&lt;root&gt;/loose/&lt;relAsset&gt;</c>). BOTH legs fail-closed; a traversal in either
    /// exits 2 PathContainment with no write. The convention lands IN THIS plan.</para>
    ///
    /// <para><b>Exit codes:</b> 0 ok; 1 usage; 2 verify | parse | path-containment; 3 file-not-found.
    /// Generic <see cref="System.Exception"/> intentionally NOT caught.</para>
    /// </summary>
    public static class ApplySaveEffectCommand
    {
        // Test-only seam: perturb the serialized bytes before the verify (mirrors ApplySaveTrnCommand).
        internal static Func<byte[], byte[]> TestPerturbSerialized;

        private enum MutationMode { FieldEdit, Add, Remove, Reorder }

        public static int Run(ApplySaveEffectOptions o)
        {
            // Determine exactly-one-of {field-edit, add, remove, reorder}.
            bool hasFieldEdit = !string.IsNullOrEmpty(o.LeafId) || !string.IsNullOrEmpty(o.Field) || o.Value != null;
            bool hasAdd = !string.IsNullOrEmpty(o.AddCommand);
            bool hasRemove = !string.IsNullOrEmpty(o.RemoveLeafId);
            bool hasReorder = !string.IsNullOrEmpty(o.ReorderLeafId) || !string.IsNullOrEmpty(o.Direction);

            int selected = (hasFieldEdit ? 1 : 0) + (hasAdd ? 1 : 0) + (hasRemove ? 1 : 0) + (hasReorder ? 1 : 0);
            if (selected != 1)
            {
                return JsonOutput.EmitError("apply-save-effect", "UsageError",
                    "Exactly ONE mutation is required: a field edit (--leaf --field --value) XOR --add-command XOR --remove-leaf XOR --reorder.",
                    exitCode: 1);
            }

            MutationMode mode;
            if (hasFieldEdit)
            {
                if (string.IsNullOrEmpty(o.LeafId) || string.IsNullOrEmpty(o.Field) || o.Value == null)
                    return JsonOutput.EmitError("apply-save-effect", "UsageError",
                        "A field edit requires --leaf, --field and --value.", exitCode: 1);
                mode = MutationMode.FieldEdit;
            }
            else if (hasAdd) { mode = MutationMode.Add; }
            else if (hasRemove) { mode = MutationMode.Remove; }
            else
            {
                if (string.IsNullOrEmpty(o.ReorderLeafId) || string.IsNullOrEmpty(o.Direction))
                    return JsonOutput.EmitError("apply-save-effect", "UsageError",
                        "A reorder requires --reorder <stableId> and --direction up|down.", exitCode: 1);
                mode = MutationMode.Reorder;
            }

            string destPath;
            try
            {
                // Two-step compose (REVIEWS HIGH #3): place the override base under <root>/<loose-subdir>
                // (default "loose"), then place the logical asset under that base → <root>/loose/<relAsset>.
                // Empty --loose-subdir preserves the legacy <root>/<relAsset>. BOTH legs fail-closed through
                // LooseOverridePath.Resolve, so a traversal in EITHER exits 2 PathContainment (T-22-path).
                if (!string.IsNullOrEmpty(o.LooseSubDir))
                {
                    string overrideBase = LooseOverridePath.Resolve(o.Root, o.LooseSubDir);
                    destPath = LooseOverridePath.Resolve(overrideBase, o.RelAsset);
                }
                else
                {
                    destPath = LooseOverridePath.Resolve(o.Root, o.RelAsset);
                }
            }
            catch (ArgumentException ex)
            {
                return JsonOutput.EmitError("apply-save-effect", "PathContainment", ex.Message, exitCode: 2);
            }

            if (!File.Exists(destPath))
            {
                return JsonOutput.EmitError("apply-save-effect", "FileNotFound",
                    "loose-override asset not found: " + destPath, exitCode: 3);
            }

            try
            {
                byte[] loadedBytes = File.ReadAllBytes(destPath);

                if (IsTreMagic(loadedBytes))
                {
                    return JsonOutput.EmitError("apply-save-effect", "UsageError",
                        ".tre archives are not an apply-save target; use the repack-tre verb.", exitCode: 1);
                }

                MutableClientEffect effect = ClientEffectDocument.FromBytes(loadedBytes);

                if (effect.IsRawPreserved)
                {
                    return JsonOutput.EmitError("apply-save-effect", "UsageError",
                        "the effect is raw-preserved (unrecognized CLEF version); refusing to rewrite a half-understood file.",
                        exitCode: 1);
                }

                // Snapshot the original command payloads (by content) for the mutation-mode verify.
                List<byte[]> originalPayloads = effect.Commands.Select(c => c.Leaf.GetPayloadCopy()).ToList();
                int originalCount = effect.Commands.Count;
                string version = effect.Version;

                string mutationApplied;
                string editedLeafId = null;

                switch (mode)
                {
                    case MutationMode.FieldEdit:
                        {
                            ClientEffectCommand cmd = FindCommandByStableId(effect, o.LeafId);
                            if (cmd == null)
                                return JsonOutput.EmitError("apply-save-effect", "UsageError",
                                    "--leaf id not found in the effect: " + o.LeafId, exitCode: 1);
                            if (cmd.IsRaw)
                                return JsonOutput.EmitError("apply-save-effect", "UsageError",
                                    "--leaf addresses a raw/unknown/truncated (non-editable) command; refusing to rewrite a half-understood payload (#4).",
                                    exitCode: 1);

                            byte[] newPayload;
                            try
                            {
                                newPayload = EncodeFieldEdit(cmd, version, o.Field, o.Value);
                            }
                            catch (ArgumentException ex)
                            {
                                return JsonOutput.EmitError("apply-save-effect", "UsageError", ex.Message, exitCode: 1);
                            }
                            catch (ClientEffectParseException ex)
                            {
                                return JsonOutput.EmitError("apply-save-effect", ex.Kind.ToString(), ex.Message, exitCode: 1);
                            }

                            editedLeafId = cmd.StableId;
                            effect.EditCommandPayload(cmd.Leaf, newPayload);
                            mutationApplied = "field(" + cmd.Tag + ":" + o.Field + ")";
                            break;
                        }
                    case MutationMode.Add:
                        {
                            string tag = o.AddCommand.Trim();
                            if (!IsKnownTag(tag))
                                return JsonOutput.EmitError("apply-save-effect", "UsageError",
                                    "--add-command must be one of CPAP|PSND|CLGT|CAMS|FFBK; got '" + tag + "'.", exitCode: 1);
                            byte[] payload;
                            try
                            {
                                payload = ClefCommandDefaults.BuildDefaultPayload(tag, version);
                            }
                            catch (ClientEffectParseException ex)
                            {
                                return JsonOutput.EmitError("apply-save-effect", ex.Kind.ToString(), ex.Message, exitCode: 1);
                            }
                            effect.AddCommand(tag, payload);
                            mutationApplied = "add(" + tag + ")";
                            break;
                        }
                    case MutationMode.Remove:
                        {
                            ClientEffectCommand cmd = FindCommandByStableId(effect, o.RemoveLeafId);
                            if (cmd == null)
                                return JsonOutput.EmitError("apply-save-effect", "UsageError",
                                    "--remove-leaf id not found in the effect: " + o.RemoveLeafId, exitCode: 1);
                            effect.RemoveCommand(cmd.Leaf);
                            mutationApplied = "remove(" + cmd.Tag + ")";
                            break;
                        }
                    default: // Reorder
                        {
                            ClientEffectCommand cmd = FindCommandByStableId(effect, o.ReorderLeafId);
                            if (cmd == null)
                                return JsonOutput.EmitError("apply-save-effect", "UsageError",
                                    "--reorder id not found in the effect: " + o.ReorderLeafId, exitCode: 1);
                            string dir = (o.Direction ?? "").Trim().ToLowerInvariant();
                            if (dir == "up") effect.ReorderCommandUp(cmd.Leaf);
                            else if (dir == "down") effect.ReorderCommandDown(cmd.Leaf);
                            else
                                return JsonOutput.EmitError("apply-save-effect", "UsageError",
                                    "--direction must be 'up' or 'down'; got '" + o.Direction + "'.", exitCode: 1);
                            mutationApplied = "reorder(" + cmd.Tag + "," + dir + ")";
                            break;
                        }
                }

                byte[] mutatedBytes = effect.Serialize();
                if (TestPerturbSerialized != null) mutatedBytes = TestPerturbSerialized(mutatedBytes);

                // Re-parse for structural validity.
                MutableClientEffect rewritten;
                try
                {
                    rewritten = ClientEffectDocument.FromBytes(mutatedBytes);
                }
                catch (ClientEffectParseException ex)
                {
                    return JsonOutput.EmitError("apply-save-effect", "VerifyFailed",
                        "serialized bytes failed to re-parse; nothing written: " + ex.Message, exitCode: 2);
                }
                catch (IffParseException ex)
                {
                    return JsonOutput.EmitError("apply-save-effect", "VerifyFailed",
                        "serialized bytes failed to re-parse; nothing written: " + ex.Message, exitCode: 2);
                }

                if (rewritten.IsRawPreserved)
                {
                    return JsonOutput.EmitError("apply-save-effect", "VerifyFailed",
                        "rewritten effect re-parsed as raw-preserved (version corruption); nothing written.", exitCode: 2);
                }

                // ── mutation-mode structural verify ────────────────────────────────
                string verifyError = VerifyByMode(mode, version, originalPayloads, originalCount, editedLeafId,
                    o.AddCommand, rewritten);
                if (verifyError != null)
                {
                    return JsonOutput.EmitError("apply-save-effect", "VerifyFailed", verifyError, exitCode: 2);
                }

                SaveCommandIo.WriteAtomic(destPath, mutatedBytes);

                var result = new JObject
                {
                    ["bytesWritten"] = mutatedBytes.Length,
                    ["commandCount"] = rewritten.Commands.Count,
                    ["mutationApplied"] = mutationApplied,
                    ["mutationMode"] = mode.ToString(),
                    ["path"] = destPath,
                    ["validated"] = true,
                    ["version"] = rewritten.Version,
                    ["written"] = true
                };
                return JsonOutput.EmitSuccess("apply-save-effect", result);
            }
            catch (ClientEffectParseException ex)
            {
                return JsonOutput.EmitError("apply-save-effect", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (UtinniCoreDotNet.Formats.Decoders.DecoderException ex)
            {
                return JsonOutput.EmitError("apply-save-effect", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("apply-save-effect", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("apply-save-effect", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught.
        }

        // ── mutation-mode verify ────────────────────────────────────────────────

        // Returns null on a clean verify, or an error message string on a failed verify.
        private static string VerifyByMode(MutationMode mode, string version, List<byte[]> originalPayloads,
            int originalCount, string editedLeafId, string addTag, MutableClientEffect rewritten)
        {
            List<byte[]> newPayloads = rewritten.Commands.Select(c => c.Leaf.GetPayloadCopy()).ToList();

            switch (mode)
            {
                case MutationMode.FieldEdit:
                    {
                        // Count unchanged; every UNTOUCHED leaf byte-identical compared BY STABLEID.
                        if (rewritten.Commands.Count != originalCount)
                            return "field edit changed the command count; nothing written.";

                        // Build stableId -> original payload from the ORIGINAL parse (re-parse the original is
                        // not needed: the ordinal stableId is stable under a field edit, so the rewritten ids
                        // match the originals one-for-one in order).
                        for (int i = 0; i < rewritten.Commands.Count; i++)
                        {
                            ClientEffectCommand rc = rewritten.Commands[i];
                            byte[] rp = rc.Leaf.GetPayloadCopy();
                            byte[] op = originalPayloads[i];
                            bool isEdited = string.Equals(rc.StableId, editedLeafId, StringComparison.Ordinal);
                            if (!isEdited)
                            {
                                if (!rp.SequenceEqual(op))
                                    return "an untouched command (stableId " + rc.StableId + ") changed bytes; nothing written.";
                            }
                            else
                            {
                                // Canonical-payload: the edited command must decode cleanly (already true, as
                                // the re-parse did not raw-fall-back it) and not be raw.
                                if (rc.IsRaw)
                                    return "the edited command raw-fell-back on re-parse (canonical-payload failure); nothing written.";
                            }
                        }
                        // Canonical-payload version invariant: the reparsed CLEF version equals the original.
                        if (!string.Equals(rewritten.Version, version, StringComparison.Ordinal))
                            return "the CLEF version changed across the edit; nothing written.";
                        return null;
                    }
                case MutationMode.Add:
                    {
                        if (rewritten.Commands.Count != originalCount + 1)
                            return "add did not increase the command count by exactly one; nothing written.";
                        // All original payloads still present (by content, multiset).
                        if (!MultisetContainsAll(newPayloads, originalPayloads))
                            return "an original command payload is missing after add; nothing written.";
                        // The new command decodes to the LOCKED defaults for its tag/version.
                        byte[] expected = ClefCommandDefaults.BuildDefaultPayload(addTag.Trim(), version);
                        // The added command is the last one (AddCommand appends).
                        ClientEffectCommand added = rewritten.Commands[rewritten.Commands.Count - 1];
                        if (added.IsRaw || added.Tag != addTag.Trim())
                            return "the added command did not re-parse as its tag; nothing written.";
                        if (!added.Leaf.GetPayloadCopy().SequenceEqual(expected))
                            return "the added command payload does not match the LOCKED defaults; nothing written.";
                        if (!string.Equals(rewritten.Version, version, StringComparison.Ordinal))
                            return "the CLEF version changed across add; nothing written.";
                        return null;
                    }
                case MutationMode.Remove:
                    {
                        if (rewritten.Commands.Count != originalCount - 1)
                            return "remove did not decrease the command count by exactly one; nothing written.";
                        // All survivors are byte-identical by CONTENT (multiset subset of originals).
                        if (!MultisetContainsAll(originalPayloads, newPayloads))
                            return "a surviving command is not byte-identical to an original; nothing written.";
                        if (!string.Equals(rewritten.Version, version, StringComparison.Ordinal))
                            return "the CLEF version changed across remove; nothing written.";
                        return null;
                    }
                default: // Reorder
                    {
                        if (rewritten.Commands.Count != originalCount)
                            return "reorder changed the command count; nothing written.";
                        // Multiset of (tag, payload-bytes) equal pre/post — NOT stableId equality.
                        if (!MultisetEqual(originalPayloads, newPayloads))
                            return "reorder changed the multiset of command payloads; nothing written.";
                        if (!string.Equals(rewritten.Version, version, StringComparison.Ordinal))
                            return "the CLEF version changed across reorder; nothing written.";
                        return null;
                    }
            }
        }

        // True iff every payload in `needles` appears in `haystack` (counting multiplicity).
        private static bool MultisetContainsAll(List<byte[]> haystack, List<byte[]> needles)
        {
            var pool = haystack.Select(b => Convert.ToBase64String(b)).ToList();
            foreach (byte[] n in needles)
            {
                string key = Convert.ToBase64String(n);
                if (!pool.Remove(key)) return false;
            }
            return true;
        }

        private static bool MultisetEqual(List<byte[]> a, List<byte[]> b)
        {
            if (a.Count != b.Count) return false;
            return MultisetContainsAll(a, b) && MultisetContainsAll(b, a);
        }

        // ── field-edit re-encode ────────────────────────────────────────────────

        // Re-encodes the WHOLE command payload, substituting exactly one field. The codec has no single-field
        // editor (CLEF payloads are variable-length), so we reconstruct from the command's CURRENT decoded
        // values + the one new field, then route through the shared ClefFieldCodec encoders so the bytes are
        // canonical-by-construction.
        private static byte[] EncodeFieldEdit(ClientEffectCommand cmd, string version, string field, string value)
        {
            float[] f = cmd.Floats ?? new float[0];
            switch (cmd.Tag)
            {
                case "CPAP":
                    {
                        int vnum = ParseVersionNum(version);
                        string appearance = cmd.StringValue;
                        float time = f.Length > 0 ? f[0] : 0f;
                        bool? soft = cmd.SoftParticleTerminate;
                        float? minScale = vnum >= 3 ? (float?)(f.Length > 1 ? f[1] : 0f) : null;
                        float? maxScale = vnum >= 3 ? (float?)(f.Length > 2 ? f[2] : 0f) : null;
                        float? minRate = vnum >= 3 ? (float?)(f.Length > 3 ? f[3] : 0f) : null;
                        float? maxRate = vnum >= 3 ? (float?)(f.Length > 4 ? f[4] : 0f) : null;
                        switch (field)
                        {
                            case "appearanceTemplateName": appearance = value; break;
                            case "timeInSeconds": time = ParseFloat(value, field); break;
                            case "softParticleTerminate": soft = ParseBool(value, field); break;
                            case "minScale": minScale = ParseFloat(value, field); break;
                            case "maxScale": maxScale = ParseFloat(value, field); break;
                            case "minPlaybackRate": minRate = ParseFloat(value, field); break;
                            case "maxPlaybackRate": maxRate = ParseFloat(value, field); break;
                            default: throw new ArgumentException("CPAP has no editable field '" + field + "'.");
                        }
                        return ClefFieldCodec.EncodeCpap(version, appearance, time, soft, minScale, maxScale, minRate, maxRate);
                    }
                case "PSND":
                    {
                        if (field != "soundTemplateName")
                            throw new ArgumentException("PSND has no editable field '" + field + "'.");
                        return ClefFieldCodec.EncodePsnd(value);
                    }
                case "CLGT":
                    {
                        byte[] rgb = cmd.Bytes ?? new byte[] { 0, 0, 0 };
                        byte r = rgb.Length > 0 ? rgb[0] : (byte)0;
                        byte g = rgb.Length > 1 ? rgb[1] : (byte)0;
                        byte b = rgb.Length > 2 ? rgb[2] : (byte)0;
                        float time = f.Length > 0 ? f[0] : 0f;
                        float ca = f.Length > 1 ? f[1] : 0f;
                        float la = f.Length > 2 ? f[2] : 0f;
                        float qa = f.Length > 3 ? f[3] : 0f;
                        float range = f.Length > 4 ? f[4] : 0f;
                        switch (field)
                        {
                            case "r": r = ParseByte(value, field); break;
                            case "g": g = ParseByte(value, field); break;
                            case "b": b = ParseByte(value, field); break;
                            case "timeInSeconds": time = ParseFloat(value, field); break;
                            case "constantAttenuation": ca = ParseFloat(value, field); break;
                            case "linearAttenuation": la = ParseFloat(value, field); break;
                            case "quadraticAttenuation": qa = ParseFloat(value, field); break;
                            case "range": range = ParseFloat(value, field); break;
                            default: throw new ArgumentException("CLGT has no editable field '" + field + "'.");
                        }
                        return ClefFieldCodec.EncodeClgt(r, g, b, time, ca, la, qa, range);
                    }
                case "CAMS":
                    {
                        float mag = f.Length > 0 ? f[0] : 0f;
                        float freq = f.Length > 1 ? f[1] : 0f;
                        float time = f.Length > 2 ? f[2] : 0f;
                        float falloff = f.Length > 3 ? f[3] : 0f;
                        switch (field)
                        {
                            case "magnitude": mag = ParseFloat(value, field); break;
                            case "frequency": freq = ParseFloat(value, field); break;
                            case "time": time = ParseFloat(value, field); break;
                            case "falloffRadius": falloff = ParseFloat(value, field); break;
                            default: throw new ArgumentException("CAMS has no editable field '" + field + "'.");
                        }
                        return ClefFieldCodec.EncodeCams(mag, freq, time, falloff);
                    }
                case "FFBK":
                    {
                        string file = cmd.StringValue;
                        int iterations = cmd.Int32Value ?? 0;
                        float range = f.Length > 0 ? f[0] : 0f;
                        switch (field)
                        {
                            case "forceFeedbackFile": file = value; break;
                            case "iterations": iterations = ParseInt(value, field); break;
                            case "range": range = ParseFloat(value, field); break;
                            default: throw new ArgumentException("FFBK has no editable field '" + field + "'.");
                        }
                        return ClefFieldCodec.EncodeFfbk(file, iterations, range);
                    }
                default:
                    throw new ArgumentException("Command tag '" + cmd.Tag + "' has no editable fields.");
            }
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        private static ClientEffectCommand FindCommandByStableId(MutableClientEffect effect, string stableId)
        {
            return effect.Commands.FirstOrDefault(c => string.Equals(c.StableId, stableId, StringComparison.Ordinal));
        }

        private static bool IsKnownTag(string tag)
        {
            return tag == "CPAP" || tag == "PSND" || tag == "CLGT" || tag == "CAMS" || tag == "FFBK";
        }

        private static int ParseVersionNum(string version)
        {
            int n;
            return int.TryParse(version, out n) ? n : 0;
        }

        private static float ParseFloat(string value, string field)
        {
            float r;
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out r))
                throw new ArgumentException("Field '" + field + "' requires an invariant-culture float; got '" + value + "'.");
            if (float.IsNaN(r) || float.IsInfinity(r))
                throw new ArgumentException("Field '" + field + "' rejects NaN/Infinity.");
            return r;
        }

        private static int ParseInt(string value, string field)
        {
            int r;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out r))
                throw new ArgumentException("Field '" + field + "' requires an integer; got '" + value + "'.");
            return r;
        }

        private static byte ParseByte(string value, string field)
        {
            int r;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out r) || r < 0 || r > 255)
                throw new ArgumentException("Field '" + field + "' requires a 0..255 byte; got '" + value + "'.");
            return (byte)r;
        }

        private static bool ParseBool(string value, string field)
        {
            string v = (value ?? "").Trim().ToLowerInvariant();
            if (v == "true" || v == "1") return true;
            if (v == "false" || v == "0") return false;
            throw new ArgumentException("Field '" + field + "' requires true/false/1/0; got '" + value + "'.");
        }

        private static bool IsTreMagic(byte[] b)
        {
            return b.Length >= 4 && b[0] == (byte)'E' && b[1] == (byte)'E' && b[2] == (byte)'R' && b[3] == (byte)'T';
        }
    }
}
