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
// Terrain (.trn / FORM TGEN) field-aware apply-save understood by reading swg-client-v2
// .../sharedTerrain/.../generator/TerrainGenerator.cpp (the LAYR -> IHDR active-flag leaf + the typed
// boundary/filter/affector FORM<tag> -> FORM<version> -> DATA shape) (SOE/Bootprint, All Rights Reserved)
// and the EA-IFF-85 public standard. Only the on-disk layout was studied — no code, comments, identifier
// names, or test fixtures copied from any reference source. Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Terrain;
using UtinniCoreDotNet.Saving;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("apply-save-trn", HelpText = "Apply ONE fixed-length terrain field edit (scalar/enum/familyId/active) to a loose-override .trn, verify only the target field's exact byte span changed, then atomically commit.")]
    public class ApplySaveTrnOptions
    {
        [Value(0, MetaName = "relAsset", Required = true, HelpText = "Relative asset path under --root (loose-override destination).")]
        public string RelAsset { get; set; }

        [Option("root", Required = true, HelpText = "Client root for the loose-override destination; relAsset is resolved + contained under it.")]
        public string Root { get; set; }

        [Option("leaf", Required = true, HelpText = "Stable id of the DATA leaf to edit (the typed node's DATA leaf, or the IHDR DATA leaf for --field active).")]
        public string LeafId { get; set; }

        [Option("field", Required = true, HelpText = "Field name to edit (a TgenFieldLayouts descriptor name, or 'active' for the IHDR layer active flag).")]
        public string Field { get; set; }

        [Option("value", Required = true, HelpText = "New value (invariant-culture decimal for floats; int for scalars/enums/familyId; true/false/1/0 for active).")]
        public string Value { get; set; }
    }

    /// <summary>
    /// The field-aware terrain member of the <c>apply-save-*</c> family (TRN-03 / TRN-04). Recovers the
    /// (tag, version) of the addressed leaf by walking the parent chain (<see cref="ResolveFieldContext"/>,
    /// #10) — it does NOT assume the DATA leaf's own node carries them — re-encodes EXACTLY ONE field via
    /// <see cref="TrnFieldEncoder"/> (exact byte-span replacement; all other bytes copied verbatim, #2/#6),
    /// re-serializes via <see cref="IffWriter"/>, re-parses for validity, verifies the output differs from
    /// source ONLY in the target field's exact byte span AND every untouched leaf is byte-identical, and —
    /// ONLY on a clean verify — atomically commits under the contained path. Fails closed (no write) on any
    /// rejection.
    ///
    /// <para><b>Active-flag location (#10, ONE decision block):</b> <c>--field active</c> mutates the int32
    /// active flag at offset 0 of the <c>IHDR</c> layer-item-header DATA leaf under <c>LAYR</c> — the SAME
    /// leaf Plan 02's decoder reads <c>TerrainLayer.Active</c> from (read↔write parity). It is resolved by
    /// asserting the addressed leaf's container parent is an <c>IHDR</c> FORM.</para>
    ///
    /// <para><b>Scope (D-05/D-06):</b> fixed-length scalar / int / enum / familyId / active only; no FORM
    /// length ripple; variable-length / name edits are rejected.</para>
    ///
    /// <para><b>Exit codes:</b> 0 ok; 1 usage; 2 verify | parse | path-containment; 3 file-not-found.
    /// Generic <see cref="System.Exception"/> intentionally NOT caught.</para>
    /// </summary>
    public static class ApplySaveTrnCommand
    {
        // Test-only seam: perturb the serialized bytes before the verify (mirrors ApplySaveIffCommand).
        internal static Func<byte[], byte[]> TestPerturbSerialized;

        private const string ActiveField = "active";
        private const string LayerItemHeaderForm = "IHDR";

        public static int Run(ApplySaveTrnOptions o)
        {
            if (string.IsNullOrEmpty(o.LeafId) || string.IsNullOrEmpty(o.Field) || o.Value == null)
            {
                return JsonOutput.EmitError("apply-save-trn", "UsageError",
                    "apply-save-trn requires --leaf, --field and --value.", exitCode: 1);
            }

            string destPath;
            try
            {
                destPath = LooseOverridePath.Resolve(o.Root, o.RelAsset);
            }
            catch (ArgumentException ex)
            {
                return JsonOutput.EmitError("apply-save-trn", "PathContainment", ex.Message, exitCode: 2);
            }

            if (!File.Exists(destPath))
            {
                return JsonOutput.EmitError("apply-save-trn", "FileNotFound",
                    "loose-override asset not found: " + destPath, exitCode: 3);
            }

            try
            {
                byte[] loadedBytes = File.ReadAllBytes(destPath);

                if (IsTreMagic(loadedBytes))
                {
                    return JsonOutput.EmitError("apply-save-trn", "UsageError",
                        ".tre archives are not an apply-save target; use the repack-tre verb.", exitCode: 1);
                }

                IffDocument originalDoc;
                using (var ms = new MemoryStream(loadedBytes, writable: false))
                {
                    originalDoc = IffReader.Read(ms);
                }
                MutableIffDocument mutable = MutableIffDocument.FromDocument(originalDoc, loadedBytes);

                // Decode the terrain DOM so a non-editable target (raw / truncated / DEAD) is rejected before
                // any write (#4). A non-TGEN root raises TerrainParseException -> exit 2.
                TerrainDocument terrain = TerrainDocument.FromIff(originalDoc, loadedBytes);

                // Locate the mutable DATA leaf addressed by --leaf.
                MutableIffNode leaf = FindMutableLeafByStableId(mutable, o.LeafId);
                if (leaf == null || leaf.Kind != MutableIffNodeKind.Leaf)
                {
                    return JsonOutput.EmitError("apply-save-trn", "UsageError",
                        "--leaf id not found (or is not a leaf) in the document: " + o.LeafId, exitCode: 1);
                }

                bool isActiveEdit = string.Equals(o.Field, ActiveField, StringComparison.Ordinal);

                // Reject an edit on a node the decoder did not fully understand (raw / truncated / DEAD) — #4.
                // The active flag lives on an IHDR (a layer header, always editable when present); typed-field
                // edits are gated by the decoded TerrainNode.IsEditable for the addressed leaf's enclosing tag.
                if (!isActiveEdit && !TargetTypedNodeIsEditable(terrain, o.LeafId))
                {
                    return JsonOutput.EmitError("apply-save-trn", "UsageError",
                        "--leaf addresses a non-editable (raw-fallback / truncated / DEAD) node; refusing to rewrite a half-understood payload (#4).",
                        exitCode: 1);
                }

                FieldContext ctx;
                try
                {
                    ctx = ResolveFieldContext(leaf, isActiveEdit);
                }
                catch (ArgumentException ex)
                {
                    return JsonOutput.EmitError("apply-save-trn", "UsageError", ex.Message, exitCode: 1);
                }

                byte[] originalPayload = leaf.GetPayloadCopy();
                byte[] newPayload;
                int spanOffset, spanWidth;
                try
                {
                    newPayload = EncodeOneField(ctx, o.Field, o.Value, originalPayload, out spanOffset, out spanWidth);
                }
                catch (ArgumentException ex)
                {
                    return JsonOutput.EmitError("apply-save-trn", "UsageError", ex.Message, exitCode: 1);
                }

                if (newPayload.Length != originalPayload.Length)
                {
                    // Fixed-length scope guard (D-05) — must never ripple.
                    return JsonOutput.EmitError("apply-save-trn", "VerifyFailed",
                        "fixed-length edit produced a different-length payload; nothing written.", exitCode: 2);
                }

                leaf.SetPayload(newPayload);

                byte[] mutatedBytes = IffWriter.Write(mutable);
                if (TestPerturbSerialized != null) mutatedBytes = TestPerturbSerialized(mutatedBytes);

                // Re-parse for structural validity.
                IffDocument rewrittenDoc;
                try
                {
                    using (var ms = new MemoryStream(mutatedBytes, writable: false))
                    {
                        rewrittenDoc = IffReader.Read(ms);
                    }
                }
                catch (IffParseException ex)
                {
                    return JsonOutput.EmitError("apply-save-trn", "VerifyFailed",
                        "serialized bytes failed to re-parse; nothing written: " + ex.Message, exitCode: 2);
                }

                // Verify: every UNTOUCHED leaf byte-identical (by stable id) AND the touched leaf differs ONLY
                // in the target field's exact byte span (#2).
                if (!UntouchedLeavesByteIdentical(originalDoc, rewrittenDoc, o.LeafId))
                {
                    return JsonOutput.EmitError("apply-save-trn", "VerifyFailed",
                        "untouched-leaf byte-identity check failed; nothing written.", exitCode: 2);
                }

                if (!OnlyTargetSpanDiffers(originalPayload, newPayload, spanOffset, spanWidth))
                {
                    return JsonOutput.EmitError("apply-save-trn", "VerifyFailed",
                        "edit changed bytes outside the target field's exact span; nothing written.", exitCode: 2);
                }

                SaveCommandIo.WriteAtomic(destPath, mutatedBytes);

                var result = new JObject
                {
                    ["bytesEqualUntouched"] = true,
                    ["bytesWritten"] = mutatedBytes.Length,
                    ["field"] = o.Field,
                    ["fieldSpanOnly"] = true,
                    ["mutationApplied"] = "field(" + ctx.Tag + "/" + ctx.Version + ":" + o.Field + ")",
                    ["path"] = destPath,
                    ["tag"] = ctx.Tag,
                    ["validated"] = true,
                    ["version"] = ctx.Version,
                    ["written"] = true
                };
                return JsonOutput.EmitSuccess("apply-save-trn", result);
            }
            catch (TerrainParseException ex)
            {
                return JsonOutput.EmitError("apply-save-trn", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("apply-save-trn", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("apply-save-trn", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught.
        }

        /// <summary>
        /// The (tag, version) recovered for an addressed DATA leaf by walking the parent chain. For a typed
        /// node leaf this is the enclosing <c>FORM &lt;tag&gt; → FORM &lt;version&gt;</c>; for the active-flag
        /// edit it is the synthetic <c>(IHDR, version-of-IHDR)</c> marker (the encoder is bypassed for active).
        /// </summary>
        private struct FieldContext
        {
            public string Tag;
            public string Version;
            public bool IsActive;
        }

        // Walks leaf.Parent up the chain to recover the enclosing typed FORM's FourCC (tag) AND the version
        // FORM (#10). It does NOT read the leaf's own node for the tag/version. For --field active the leaf's
        // immediate container parent must be an IHDR FORM (the pinned active-flag location).
        private static FieldContext ResolveFieldContext(MutableIffNode leaf, bool isActiveEdit)
        {
            MutableIffNode parent = leaf.Parent;
            if (parent == null || parent.Kind != MutableIffNodeKind.Container)
                throw new ArgumentException("--leaf has no enclosing FORM; cannot resolve field context.");

            if (isActiveEdit)
            {
                if (!string.Equals(parent.SubTypeId, LayerItemHeaderForm, StringComparison.Ordinal))
                    throw new ArgumentException(
                        "--field active requires --leaf to address the IHDR layer-item-header DATA leaf (got parent FORM '"
                        + parent.SubTypeId + "').");
                return new FieldContext { Tag = LayerItemHeaderForm, Version = ParentVersionOf(parent), IsActive = true };
            }

            // Typed node leaf: parent is the version FORM; grandparent is the tag FORM.
            string version = parent.SubTypeId;
            MutableIffNode grandparent = parent.Parent;
            if (grandparent == null || grandparent.Kind != MutableIffNodeKind.Container)
                throw new ArgumentException("--leaf's version FORM has no enclosing tag FORM; cannot resolve (tag,version).");
            string tag = grandparent.SubTypeId;
            return new FieldContext { Tag = tag, Version = version, IsActive = false };
        }

        // The version FORM enclosing an IHDR (the LAYR body version) — best-effort; not used for encoding,
        // only for the result envelope. Returns the IHDR's parent SubTypeId when it is a version-form, else "".
        private static string ParentVersionOf(MutableIffNode ihdr)
        {
            MutableIffNode p = ihdr.Parent;
            return (p != null && p.Kind == MutableIffNodeKind.Container) ? (p.SubTypeId ?? "") : "";
        }

        // Encodes ONE field into a COPY of the original payload, returning the new payload + the exact byte
        // span [spanOffset .. spanOffset+spanWidth) that changed. Active is written directly (int32 @ offset 0
        // of the IHDR DATA leaf — the read↔write parity location); all other fields route through the
        // single-source TrnFieldEncoder (#2/#6).
        private static byte[] EncodeOneField(
            FieldContext ctx, string field, string value, byte[] originalPayload, out int spanOffset, out int spanWidth)
        {
            if (ctx.IsActive)
            {
                if (originalPayload.Length < 4)
                    throw new ArgumentException("IHDR active-flag DATA leaf is shorter than 4 bytes; cannot edit active.");
                int iv;
                if (bool.TryParse(value, out bool b)) iv = b ? 1 : 0;
                else if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out iv))
                    throw new ArgumentException("--field active --value must be true/false/1/0 (got '" + value + "').");

                byte[] result = (byte[])originalPayload.Clone();
                result[0] = (byte)(iv & 0xFF);
                result[1] = (byte)((iv >> 8) & 0xFF);
                result[2] = (byte)((iv >> 16) & 0xFF);
                result[3] = (byte)((iv >> 24) & 0xFF);
                spanOffset = 0;
                spanWidth = 4;
                return result;
            }

            // Typed field: locate the descriptor span (so the verify can assert exact-span) then encode.
            var descriptors = TgenFieldLayouts.For(ctx.Tag, ctx.Version);
            if (descriptors == null)
                throw new ArgumentException("No descriptor layout for " + ctx.Tag + "/" + ctx.Version + " (D-02).");
            TgenFieldDescriptor target = descriptors.FirstOrDefault(d => string.Equals(d.Name, field, StringComparison.Ordinal));
            if (target == null)
                throw new ArgumentException("Field '" + field + "' is not a descriptor field of " + ctx.Tag + "/" + ctx.Version + ".");

            spanOffset = target.Offset;
            spanWidth = target.Width;
            return TrnFieldEncoder.EncodeField(originalPayload, ctx.Tag, ctx.Version, field, value);
        }

        // True when the typed node enclosing the addressed leaf decoded to an editable TerrainNode (#4). The
        // addressed leaf's stable id is the DATA leaf; the enclosing node's stable id is the leaf id minus its
        // trailing "/FORM:<version>/.../DATA:DATA/0" — but rather than parse the id we match the decoded
        // node whose StableIdPath is a PREFIX of the leaf id. A raw/truncated/DEAD node is non-editable.
        private static bool TargetTypedNodeIsEditable(TerrainDocument terrain, string leafId)
        {
            bool? editable = null;
            foreach (var layer in terrain.Layers)
                editable = WalkLayerForNode(layer, leafId) ?? editable;
            // If no decoded typed/raw/dead node claims the leaf, it is not an editable typed-node payload.
            return editable == true;
        }

        private static bool? WalkLayerForNode(TerrainLayer layer, string leafId)
        {
            foreach (var node in layer.Nodes)
            {
                if (node.StableIdPath != null && leafId.StartsWith(node.StableIdPath, StringComparison.Ordinal))
                    return node.IsEditable;
            }
            foreach (var sub in layer.SubLayers)
            {
                bool? r = WalkLayerForNode(sub, leafId);
                if (r != null) return r;
            }
            return null;
        }

        private static bool OnlyTargetSpanDiffers(byte[] original, byte[] edited, int spanOffset, int spanWidth)
        {
            if (original.Length != edited.Length) return false;
            for (int i = 0; i < original.Length; i++)
            {
                bool inSpan = i >= spanOffset && i < spanOffset + spanWidth;
                if (!inSpan && original[i] != edited[i]) return false;
            }
            return true;
        }

        private static bool UntouchedLeavesByteIdentical(IffDocument originalDoc, IffDocument rewrittenDoc, string editedLeafId)
        {
            var rewrittenById = new Dictionary<string, IffLeafChunk>(StringComparer.Ordinal);
            foreach (var node in rewrittenDoc.AllNodesInPreorder)
                if (node is IffLeafChunk leaf) rewrittenById[leaf.Id] = leaf;

            foreach (var node in originalDoc.AllNodesInPreorder)
            {
                if (!(node is IffLeafChunk origLeaf)) continue;
                if (origLeaf.Id == editedLeafId) continue; // the edited leaf is allowed to differ
                if (!rewrittenById.TryGetValue(origLeaf.Id, out IffLeafChunk rw)) return false;
                if (rw.TypeId != origLeaf.TypeId || !rw.Data.SequenceEqual(origLeaf.Data)) return false;
            }
            return true;
        }

        private static bool IsTreMagic(byte[] b)
        {
            return b.Length >= 4 && b[0] == (byte)'E' && b[1] == (byte)'E' && b[2] == (byte)'R' && b[3] == (byte)'T';
        }

        private static MutableIffNode FindMutableLeafByStableId(MutableIffDocument doc, string stableId)
        {
            return FindMutableLeafRecursive(doc.Root, "", 0, stableId);
        }

        private static MutableIffNode FindMutableLeafRecursive(MutableIffNode node, string parentPrefix, int ordinal, string targetId)
        {
            string thisId = MutableIffDocument.DeriveStableId(node, parentPrefix, ordinal);
            if (node.Kind == MutableIffNodeKind.Leaf && thisId == targetId) return node;
            if (node.Kind == MutableIffNodeKind.Container)
            {
                string childPrefix = thisId + "/";
                for (int i = 0; i < node.Children.Count; i++)
                {
                    var found = FindMutableLeafRecursive(node.Children[i], childPrefix, i, targetId);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}
