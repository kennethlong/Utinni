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
// Terrain (.trn / FORM TGEN) decode path understood by reading swg-client-v2
// .../sharedTerrain/.../generator/{TerrainGenerator,TerrainGeneratorLoader}.{cpp,h} (SOE/Bootprint, All
// Rights Reserved): TGEN -> 0000 -> [six palettes in fixed load order, each optional, two MGRP slots
// disambiguated by POSITION] -> optional LYRS -> LAYR* (each layer reads its active flag + name from the
// IHDR layer-item header leaf and recurses into sub-layers + child boundary/filter/affector forms);
// version is read FIRST and an unknown version/tag raw-falls-back the whole chunk; a fixed DEAD set is
// recognized-and-skipped. Only the on-disk layout was studied — no code, comments, identifier names, or
// test fixtures copied from any reference source. Implementation original to Utinni under MIT.

using System.Collections.Generic;
using System.Text;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Terrain;

namespace UtinniCoreDotNet.Formats.Decoders
{
    /// <summary>
    /// Decodes a terrain (<c>.trn</c> / FORM TGEN) document from the shipped IFF DOM into a navigable
    /// <see cref="TerrainDocument"/> (TRN-01/TRN-02).
    ///
    /// <para><b>Decode posture:</b> version-FIRST dispatch through the single-source
    /// <see cref="TgenFieldLayouts"/> table (no offset literals here — concern #2). A typed node is emitted
    /// ONLY when a complete descriptor exists AND the consumed length equals the payload length; ANY
    /// mismatch (unknown tag, unrecognized version, truncated, trailing bytes) raw-falls-back to a
    /// non-editable <see cref="TerrainNode"/> (concern #4 / D-02), never throwing. A fixed DEAD set is
    /// recognized-and-skipped (D-03). The six palettes decode positionally via a sequential optional-slot
    /// state machine mirroring the C++ <c>load_0000</c> order — an absent slot does not shift later slots,
    /// and a lone MGRP is marked Ambiguous rather than guessed (concern #3). Every node carries its
    /// physical-path stable id derived from the DOM walk INCLUDING raw/dead siblings (concern #14).</para>
    /// </summary>
    public static class TgenDecoder
    {
        private const string RootTgen = "TGEN";
        private const string RootPtat = "PTAT"; // real .trn root wraps TGEN; the synthesizer emits TGEN directly.
        private const string LayerListForm = "LYRS";
        private const string LayerForm = "LAYR";
        private const string LayerItemHeaderForm = "IHDR";

        // The fixed load order of the six shared palettes (positional, NOT tag lookup — D-04 / Pitfall 4).
        private static readonly string[] PaletteRoles = { "Shader", "Flora", "Radial", "Environment", "Fractal", "Bitmap" };
        private static readonly string[] PaletteTags = { "SGRP", "FGRP", "RGRP", "EGRP", "MGRP", "MGRP" };

        // DEAD/obsolete tags — recognized-and-skipped (display-only), never raw-fallback editable (D-03).
        private static readonly HashSet<string> DeadTags = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "BALL", "BSPL", "AHSM", "AHBM", "ACBM", "ASBM", "AFBM"
        };

        // Container child tags that are NOT terrain leaf nodes (structural / palette / header) and so must
        // not be decoded as boundary/filter/affector nodes when walking a layer's children.
        private static readonly HashSet<string> StructuralTags = new HashSet<string>(System.StringComparer.Ordinal)
        {
            LayerListForm, LayerForm, LayerItemHeaderForm, "ACTN", "ADTA",
            "SGRP", "FGRP", "RGRP", "EGRP", "MGRP"
        };

        /// <summary>
        /// Cheap structural sniff for the <c>decode-iff</c> dispatcher: the root FORM (or the inner FORM of
        /// a PTAT wrapper) is a <c>TGEN</c>.
        /// </summary>
        public static bool LooksLikeTerrain(IffChunk root)
        {
            var container = root as IffContainerChunk;
            if (container == null) return false;
            if (container.SubTypeId == RootTgen) return true;
            if (container.SubTypeId == RootPtat)
                return FindContainerChild(container, RootTgen) != null;
            return false;
        }

        /// <summary>Decodes a terrain document from an immutable <see cref="IffDocument"/> + its source bytes.</summary>
        public static TerrainDocument Decode(IffDocument doc, byte[] sourceBytes)
        {
            MutableIffDocument mutable = MutableIffDocument.FromDocument(doc, sourceBytes);
            return Decode(mutable);
        }

        /// <summary>Decodes a terrain document from a mutable DOM (the held edit source).</summary>
        public static TerrainDocument Decode(MutableIffDocument mutable)
        {
            if (mutable == null) throw new System.ArgumentNullException(nameof(mutable));
            MutableIffNode root = mutable.Root;

            // Locate the TGEN form — directly, or nested inside a PTAT wrapper (the real .trn root).
            MutableIffNode tgen = LocateTgen(root);
            if (tgen == null)
            {
                throw new TerrainParseException(TerrainParseError.UnexpectedForm,
                    "Root FORM is not TGEN (nor a PTAT wrapping a TGEN); not a terrain template.");
            }

            string tgenId = MutableIffDocument.DeriveStableId(root, "", 0);
            // If the TGEN is nested under PTAT, recompute its path relative to root.
            tgenId = StableIdOf(root, tgen) ?? tgenId;

            // The single TGEN body version form (e.g. "0000").
            MutableIffNode body = FirstContainerChild(tgen);
            var palettes = TerrainPalettes.AllAbsent();
            var layers = new List<TerrainLayer>();

            if (body != null)
            {
                string bodyId = tgenId + "/" + MutableIffDocument.DeriveStableId(body, "", IndexOf(tgen, body));
                palettes = DecodePalettes(body);
                MutableIffNode lyrs = FindContainerChild(body, LayerListForm);
                if (lyrs != null)
                {
                    string lyrsId = bodyId + "/" + MutableIffDocument.DeriveStableId(lyrs, "", IndexOf(body, lyrs));
                    int ordinal = 0;
                    foreach (var child in lyrs.Children)
                    {
                        if (child.Kind != MutableIffNodeKind.Container) { ordinal++; continue; }
                        string childId = lyrsId + "/" + MutableIffDocument.DeriveStableId(child, "", ordinal);
                        layers.Add(DecodeLayer(child, childId));
                        ordinal++;
                    }
                }
            }

            return new TerrainDocument(mutable, RootTgen, palettes, layers.AsReadOnly());
        }

        // ─────────────────────────────────────────────────────────────────────
        // Palette state machine — sequential optional-slot, positional disambiguation (Pitfall 4 / #3).
        // ─────────────────────────────────────────────────────────────────────

        private static TerrainPalettes DecodePalettes(MutableIffNode body)
        {
            // Walk the body's children once, consuming palette forms in the fixed load order. A child
            // matching the next expected palette tag binds that slot; a non-match leaves the slot absent
            // and does NOT advance past later slots (it simply is not consumed by the palette machine).
            var slots = new TerrainPalette[PaletteRoles.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = TerrainPalette.Absent(PaletteRoles[i]);

            // Gather the MGRP children up front so the two MGRP slots can be disambiguated by position.
            var mgrpForms = new List<MutableIffNode>();
            foreach (var c in body.Children)
                if (c.Kind == MutableIffNodeKind.Container && c.SubTypeId == "MGRP")
                    mgrpForms.Add(c);

            // Slots 1..4 are uniquely tagged (SGRP/FGRP/RGRP/EGRP) — bind by tag presence.
            for (int slot = 0; slot < 4; slot++)
            {
                MutableIffNode form = FindContainerChild(body, PaletteTags[slot]);
                if (form != null)
                    slots[slot] = TerrainPalette.Of(PaletteRoles[slot], ReadFamilies(form));
            }

            // Slots 5 (Fractal) + 6 (Bitmap) BOTH use MGRP — positional disambiguation (Pitfall 4).
            if (mgrpForms.Count >= 2)
            {
                slots[4] = TerrainPalette.Of("Fractal", ReadFamilies(mgrpForms[0]));
                slots[5] = TerrainPalette.Of("Bitmap", ReadFamilies(mgrpForms[1]));
            }
            else if (mgrpForms.Count == 1)
            {
                // A single MGRP cannot be classified fractal-vs-bitmap by position — bind it to the
                // expected next slot (Fractal) and mark it Ambiguous rather than guessing (concern #3).
                slots[4] = TerrainPalette.Of("Fractal", ReadFamilies(mgrpForms[0]), ambiguous: true);
                slots[5] = TerrainPalette.Absent("Bitmap");
            }

            return new TerrainPalettes(slots[0], slots[1], slots[2], slots[3], slots[4], slots[5]);
        }

        // Reads a palette's familyId -> name entries. v1 scope: best-effort, bounded, never throws — a
        // palette whose family layout is not understood degrades to an empty (but Present) family list.
        private static IReadOnlyList<TerrainPaletteFamily> ReadFamilies(MutableIffNode paletteForm)
        {
            var families = new List<TerrainPaletteFamily>();
            // The v1 envelope surfaces presence + verbatim familyIds where a simple [int32 familyId]
            // [cstring name] DATA leaf is found; deeper palette typing is a Tier-2 follow-up. Bounded reads
            // only — a malformed family leaf is skipped, never over-read.
            foreach (var leaf in EnumerateLeaves(paletteForm, "DATA"))
            {
                byte[] data = leaf.GetPayloadCopy();
                var cursor = new IffPayloadCursor(data);
                try
                {
                    if (cursor.Remaining < 4) continue;
                    int familyId = cursor.ReadInt32Le();
                    string name = cursor.Remaining > 0 ? TryReadCString(cursor) : string.Empty;
                    families.Add(new TerrainPaletteFamily(familyId, name));
                }
                catch (DecoderException)
                {
                    // Bounded read failed — skip this family leaf, never over-read (V5 input validation).
                }
            }
            return families.AsReadOnly();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Layer recursion — name + active from IHDR; child nodes; sub-layers.
        // ─────────────────────────────────────────────────────────────────────

        private static TerrainLayer DecodeLayer(MutableIffNode layerForm, string stableIdPath)
        {
            // The layer's body is its version form (e.g. "0000"/"0003") OR, in the synthesizer's collapsed
            // shape, the layerForm IS the version form directly under LYRS. Walk to find the IHDR + children.
            MutableIffNode walkRoot = layerForm;
            // Children (IHDR + boundary/filter/affector nodes + sub-layers) are addressed RELATIVE TO walkRoot.
            // For a real LAYR FORM the children live under its single version-form body, so that version form
            // contributes a path segment that MUST be folded into every child's stable id. Omitting it (the
            // pre-21-04 bug) made the decoded StableIdPath unresolvable by the save-side FindNodeByStableId,
            // which walks the TRUE DOM — surfacing live as "no IHDR DATA child leaf" (active flag) and
            // "No typed node FORM container found" (typed field) on real high-era terrain (naboo.trn). The
            // synthesizer's collapsed fixture (layer FORM sub-type "0003", not "LAYR") skips this descent, so
            // walkRootId stays == stableIdPath and the existing golden tests are unaffected.
            string walkRootId = stableIdPath;
            if (layerForm.SubTypeId == LayerForm)
            {
                MutableIffNode versionBody = FirstContainerChild(layerForm);
                if (versionBody != null)
                {
                    walkRoot = versionBody;
                    walkRootId = stableIdPath + "/"
                        + MutableIffDocument.DeriveStableId(versionBody, "", IndexOf(layerForm, versionBody));
                }
            }

            bool active = true;     // C++ default is active=true when no IHDR active flag is present.
            string name = string.Empty;
            ReadLayerItemHeader(walkRoot, ref active, ref name);

            var nodes = new List<TerrainNode>();
            var subLayers = new List<TerrainLayer>();

            int ordinal = 0;
            foreach (var child in walkRoot.Children)
            {
                if (child.Kind != MutableIffNodeKind.Container) { ordinal++; continue; }
                string childId = walkRootId + "/" + MutableIffDocument.DeriveStableId(child, "", ordinal);
                string tag = child.SubTypeId;

                if (tag == LayerForm || tag == LayerListForm)
                {
                    // A nested sub-layer (recursion).
                    subLayers.Add(DecodeLayer(child, childId));
                }
                else if (!StructuralTags.Contains(tag))
                {
                    nodes.Add(DecodeNode(child, childId));
                }
                // Structural children (IHDR/ACTN/ADTA/palettes) are not boundary/filter/affector nodes.
                ordinal++;
            }

            return new TerrainLayer(name, active, stableIdPath, nodes.AsReadOnly(), subLayers.AsReadOnly());
        }

        // Reads the active flag (int32) + name (cstring) from the IHDR layer-item header leaf, when present.
        // This is the SAME DOM leaf Plan 03 `--field active` mutates (read<->write location parity, #10).
        private static void ReadLayerItemHeader(MutableIffNode layerBody, ref bool active, ref string name)
        {
            MutableIffNode ihdr = FindContainerChild(layerBody, LayerItemHeaderForm);
            if (ihdr == null) return;

            // The IHDR DATA leaf is, on the collapsed / current direct-DATA fixtures, a DIRECT child of IHDR;
            // on real high-era terrain (naboo.trn, PTAT/0014) it is ONE version-form deeper —
            // IHDR -> <IHDR-version FORM> -> DATA (R1). Prefer the direct-DATA leaf when present; otherwise
            // descend one level via FirstContainerChild(ihdr) to the IHDR version FORM and read its DATA leaf.
            // The save-side TerrainSaveTargets.ResolveIhdrLeafStableId mirrors this EXACT descent so read and
            // write address the SAME leaf (concern #10).
            MutableIffNode leafSearchRoot = HasDirectLeaf(ihdr, "DATA") ? ihdr : (FirstContainerChild(ihdr) ?? ihdr);
            foreach (var leaf in EnumerateLeaves(leafSearchRoot, "DATA"))
            {
                byte[] data = leaf.GetPayloadCopy();
                var cursor = new IffPayloadCursor(data);
                try
                {
                    if (cursor.Remaining >= 4) active = cursor.ReadInt32Le() != 0;
                    if (cursor.Remaining > 0) name = TryReadCString(cursor);
                }
                catch (DecoderException)
                {
                    // Bounded read failed — keep the defaults, never over-read.
                }
                return; // first DATA leaf carries the active flag + name.
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Node typed-decode / raw-fallback / DEAD-skip.
        // ─────────────────────────────────────────────────────────────────────

        private static TerrainNode DecodeNode(MutableIffNode nodeForm, string stableIdPath)
        {
            string tag = nodeForm.SubTypeId;

            // DEAD/obsolete tags are recognized-and-skipped, never raw-fallback editable (D-03).
            if (DeadTags.Contains(tag))
                return TerrainNode.DeadSkipped(tag, FindVersion(nodeForm), stableIdPath);

            // Read the FORM version FIRST (Pitfall 5 — never guess field offsets for an unknown version).
            string version = FindVersion(nodeForm);
            byte[] dataBytes = FindDataPayload(nodeForm);

            if (version == null || dataBytes == null)
                return TerrainNode.RawPreserved(tag, version, dataBytes ?? new byte[0], stableIdPath);

            IReadOnlyList<TgenFieldDescriptor> descriptors = TgenFieldLayouts.For(tag, version);
            if (descriptors == null || descriptors.Count == 0)
                return TerrainNode.RawPreserved(tag, version, dataBytes, stableIdPath); // unknown tag/version (D-02).

            // Descriptor-driven typed decode. Read each field AT its descriptor offset/width via the
            // bounds-checked cursor; on ANY shortfall fall back to raw + non-editable (concern #4).
            try
            {
                var fields = DecodeFields(descriptors, dataBytes);

                // Consumed-length MUST equal payload-length — a complete, exact match. Trailing bytes or a
                // short payload => raw-fallback (concern #4/#17).
                int consumed = 0;
                for (int i = 0; i < descriptors.Count; i++) consumed += descriptors[i].Width;
                if (consumed != dataBytes.Length)
                    return TerrainNode.RawPreserved(tag, version, dataBytes, stableIdPath);

                return TerrainNode.Typed(tag, version, fields, stableIdPath);
            }
            catch (DecoderException)
            {
                // Truncated mid-field — raw-fallback, non-editable, no over-read (T-20-02).
                return TerrainNode.RawPreserved(tag, version, dataBytes, stableIdPath);
            }
        }

        private static IReadOnlyList<TerrainField> DecodeFields(IReadOnlyList<TgenFieldDescriptor> descriptors, byte[] data)
        {
            var fields = new List<TerrainField>(descriptors.Count);
            foreach (var d in descriptors)
            {
                // Position the cursor at the descriptor offset and read its width via the bounds-checked
                // primitives. A read past the payload throws DecoderException (caught by the caller).
                var cursor = new IffPayloadCursor(SliceAt(data, d.Offset, d.Width));
                string value;
                switch (d.DisplayType)
                {
                    case TgenDisplayType.ScalarFloat:
                        value = cursor.ReadFloatLe().ToString(System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case TgenDisplayType.Enum32:
                    case TgenDisplayType.Int32:
                    case TgenDisplayType.FamilyIdRef:
                    case TgenDisplayType.ActiveFlag:
                        value = cursor.ReadInt32Le().ToString(System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    default:
                        // A new TgenDisplayType value is a single-source layout-table bug — fail loudly rather
                        // than silently decoding it as int32 (IN-03). This is NOT a DecoderException, so it does
                        // NOT raw-fall-back: an unhandled display type is a programmer error, not malformed data.
                        throw new System.ArgumentException(
                            "Unhandled TgenDisplayType " + d.DisplayType + " for descriptor '" + d.Name
                            + "'; refusing to default to int32.", nameof(descriptors));
                }
                fields.Add(new TerrainField(d.Name, value, d.DisplayType, d.Editable));
            }
            return fields.AsReadOnly();
        }

        // Returns the descriptor's byte window from the payload; throws DecoderException(Truncated) if the
        // window runs past the payload end (so a truncated known tag raw-falls-back, never over-reads).
        private static byte[] SliceAt(byte[] data, int offset, int width)
        {
            if (offset < 0 || width < 0 || offset + width > data.Length)
                throw new DecoderException(DecoderError.Truncated,
                    "Field window [" + offset + ".." + (offset + width) + ") exceeds payload length " + data.Length + ".");
            byte[] window = new byte[width];
            System.Array.Copy(data, offset, window, 0, width);
            return window;
        }

        // ─────────────────────────────────────────────────────────────────────
        // DOM navigation helpers.
        // ─────────────────────────────────────────────────────────────────────

        private static MutableIffNode LocateTgen(MutableIffNode root)
        {
            if (root == null || root.Kind != MutableIffNodeKind.Container) return null;
            if (root.SubTypeId == RootTgen) return root;
            if (root.SubTypeId == RootPtat)
            {
                // Real .trn: PTAT/<version> wraps a TGEN. Search the immediate + version-form children.
                MutableIffNode direct = FindContainerChild(root, RootTgen);
                if (direct != null) return direct;
                foreach (var c in root.Children)
                {
                    if (c.Kind != MutableIffNodeKind.Container) continue;
                    MutableIffNode nested = FindContainerChild(c, RootTgen);
                    if (nested != null) return nested;
                }
            }
            return null;
        }

        // The first container child of a node (used to descend into a single version-form body).
        private static MutableIffNode FirstContainerChild(MutableIffNode node)
        {
            foreach (var c in node.Children)
                if (c.Kind == MutableIffNodeKind.Container) return c;
            return null;
        }

        private static MutableIffNode FindContainerChild(MutableIffNode node, string subTypeId)
        {
            foreach (var c in node.Children)
                if (c.Kind == MutableIffNodeKind.Container && c.SubTypeId == subTypeId) return c;
            return null;
        }

        private static IffContainerChunk FindContainerChild(IffContainerChunk node, string subTypeId)
        {
            foreach (var c in node.Children)
            {
                var cc = c as IffContainerChunk;
                if (cc != null && cc.SubTypeId == subTypeId) return cc;
            }
            return null;
        }

        // A typed node form is FORM <tag> -> FORM <version> -> DATA. The version is the sub-type of the
        // first container child; null when there is no version form.
        private static string FindVersion(MutableIffNode nodeForm)
        {
            MutableIffNode versionForm = FirstContainerChild(nodeForm);
            return versionForm?.SubTypeId;
        }

        // The DATA payload of a typed node form (under its version form). Null when none.
        private static byte[] FindDataPayload(MutableIffNode nodeForm)
        {
            MutableIffNode versionForm = FirstContainerChild(nodeForm);
            if (versionForm == null) return null;
            foreach (var leaf in EnumerateLeaves(versionForm, "DATA"))
                return leaf.GetPayloadCopy();
            return null;
        }

        private static IEnumerable<MutableIffNode> EnumerateLeaves(MutableIffNode container, string typeId)
        {
            foreach (var c in container.Children)
                if (c.Kind == MutableIffNodeKind.Leaf && c.TypeId == typeId)
                    yield return c;
        }

        // True when `container` has a DIRECT leaf child of the given type id (the direct-DATA shape). Drives
        // the IHDR direct-vs-version-form leaf-search-root choice in ReadLayerItemHeader.
        private static bool HasDirectLeaf(MutableIffNode container, string typeId)
        {
            foreach (var c in container.Children)
                if (c.Kind == MutableIffNodeKind.Leaf && c.TypeId == typeId)
                    return true;
            return false;
        }

        private static int IndexOf(MutableIffNode parent, MutableIffNode child)
        {
            for (int i = 0; i < parent.Children.Count; i++)
                if (ReferenceEquals(parent.Children[i], child)) return i;
            return 0;
        }

        // Builds the stable-id path of `target` relative to `root` over the physical DOM (including raw/
        // dead siblings), or null when target is not found.
        private static string StableIdOf(MutableIffNode root, MutableIffNode target)
        {
            string rootId = MutableIffDocument.DeriveStableId(root, "", 0);
            if (ReferenceEquals(root, target)) return rootId;
            return StableIdOfRecursive(root, target, rootId + "/");
        }

        private static string StableIdOfRecursive(MutableIffNode node, MutableIffNode target, string prefix)
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                var c = node.Children[i];
                string cid = MutableIffDocument.DeriveStableId(c, prefix, i);
                if (ReferenceEquals(c, target)) return cid;
                if (c.Kind == MutableIffNodeKind.Container)
                {
                    string found = StableIdOfRecursive(c, target, cid + "/");
                    if (found != null) return found;
                }
            }
            return null;
        }

        // Reads a NUL-terminated string, returning the bytes-before-terminator as ASCII even when the
        // string is not NUL-terminated within the payload (bounded — never over-reads into the next chunk).
        private static string TryReadCString(IffPayloadCursor cursor)
        {
            try
            {
                return cursor.ReadCString(Encoding.ASCII);
            }
            catch (DecoderException)
            {
                // Unterminated within this payload — return empty rather than over-read.
                return string.Empty;
            }
        }
    }
}
