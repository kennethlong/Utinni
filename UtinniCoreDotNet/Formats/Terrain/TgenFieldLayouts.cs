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
// Terrain (.trn / FORM TGEN) Tier-1 leaf field layouts understood by reading swg-client-v2
// .../sharedTerrain/.../generator/{AffectorHeight,AffectorColor,AffectorShader,AffectorFloraStatic,
// Boundary,Filter}.{cpp,h} and Feather.h (SOE/Bootprint, All Rights Reserved) and the EA-IFF-85
// public standard. Per 20-RESEARCH.md §"Tag Taxonomy" + §"SWGEmu vs Restoration version dispatch":
// each typed leaf is a fixed-length little-endian scalar/enum blob; higher FORM versions append a
// feather-function enum + feather-distance float. Only the on-disk layout was studied — no code,
// comments, identifier names, or test fixtures copied from any reference source. Implementation
// original to Utinni under MIT.

using System;
using System.Collections.Generic;

namespace UtinniCoreDotNet.Formats.Terrain
{
    /// <summary>Endianness of a descriptor field's on-disk scalar. SWG payload scalars are little-endian (Pitfall 6).</summary>
    public enum TgenFieldEndian
    {
        /// <summary>Little-endian (the SWG Win32 payload-scalar convention).</summary>
        Little
    }

    /// <summary>
    /// Display + edit classification for a <see cref="TgenFieldDescriptor"/>. Drives how a decoded value
    /// is rendered in the navigable envelope and how Plan 03's <c>apply-save-trn --value</c> parses an
    /// incoming edit. Kept deliberately small (D-05 edit scope is fixed-length scalar / enum / active-flag).
    /// </summary>
    public enum TgenDisplayType
    {
        /// <summary>32-bit IEEE-754 float scalar.</summary>
        ScalarFloat,
        /// <summary>32-bit signed integer scalar.</summary>
        Int32,
        /// <summary>32-bit enum (e.g. the AHCN height operation, or a feather function).</summary>
        Enum32,
        /// <summary>32-bit boolean active flag (0 / non-0). The IHDR layer-item active flag.</summary>
        ActiveFlag,
        /// <summary>32-bit reference into a shared palette (familyId → name). NOT renumbered on save (D-04).</summary>
        FamilyIdRef
    }

    /// <summary>
    /// How Plan 03's <c>apply-save-trn --value</c> parses an incoming string edit for this field. Pairs
    /// with <see cref="TgenDisplayType"/>; named separately so the parser contract is explicit in the
    /// single-source table (review concern #2 — width/signedness/enum-width/endian/parser/editable all
    /// recorded HERE, not re-derived in the encoder).
    /// </summary>
    public enum TgenEditParser
    {
        /// <summary>Parse the value as a <see cref="float"/>.</summary>
        Float,
        /// <summary>Parse the value as a 32-bit signed integer.</summary>
        Int32,
        /// <summary>Parse the value as a boolean (true/false/1/0) written as an int32.</summary>
        Bool32
    }

    /// <summary>
    /// One field of a Tier-1 terrain leaf payload: WHERE it sits in the packed <c>DATA</c> blob
    /// (<see cref="Offset"/>/<see cref="Width"/>), HOW its bytes are interpreted
    /// (<see cref="Signed"/>/<see cref="Endian"/>/<see cref="EnumStorageWidth"/>/<see cref="DisplayType"/>),
    /// how an edit to it is parsed (<see cref="EditParser"/>), and whether it is editable at all
    /// (<see cref="Editable"/>). A sealed, get-only DTO mirroring <see cref="Decoders.ObjectTemplateField"/>.
    ///
    /// <para>This is the SINGLE source of truth consumed by BOTH the read path (<see cref="Decoders.TgenDecoder"/>,
    /// Plan 02) and the encode path (Plan 03's <c>apply-save-trn</c> field encoder) — neither hard-codes
    /// offset literals (review concern #2).</para>
    /// </summary>
    public sealed class TgenFieldDescriptor
    {
        /// <summary>The field's human name (the decoded node exposes this; the encoder matches <c>--field</c> on it).</summary>
        public string Name { get; }

        /// <summary>Byte offset of this field within the packed <c>DATA</c> payload.</summary>
        public int Offset { get; }

        /// <summary>Field width in bytes.</summary>
        public int Width { get; }

        /// <summary>True iff the integer scalar is signed (floats ignore this).</summary>
        public bool Signed { get; }

        /// <summary>On-disk endianness (always <see cref="TgenFieldEndian.Little"/> for SWG payload scalars).</summary>
        public TgenFieldEndian Endian { get; }

        /// <summary>Storage width of an enum field in bytes (equals <see cref="Width"/> for enums; 0 otherwise).</summary>
        public int EnumStorageWidth { get; }

        /// <summary>Display classification driving rendering + edit-parse selection.</summary>
        public TgenDisplayType DisplayType { get; }

        /// <summary>How <c>apply-save-trn --value</c> parses an incoming edit for this field (Plan 03).</summary>
        public TgenEditParser EditParser { get; }

        /// <summary>True iff this field may be edited by <c>apply-save-trn</c> (D-05 fixed-length scope).</summary>
        public bool Editable { get; }

        /// <summary>Constructs a fully-specified field descriptor.</summary>
        public TgenFieldDescriptor(
            string name,
            int offset,
            int width,
            bool signed,
            TgenFieldEndian endian,
            int enumStorageWidth,
            TgenDisplayType displayType,
            TgenEditParser editParser,
            bool editable)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Offset = offset;
            Width = width;
            Signed = signed;
            Endian = endian;
            EnumStorageWidth = enumStorageWidth;
            DisplayType = displayType;
            EditParser = editParser;
            Editable = editable;
        }
    }

    /// <summary>
    /// The single-source per-tag × version binary field-descriptor table for the Tier-1 terrain leaf set
    /// (D-01). <see cref="For"/> returns the ordered, contiguous, non-overlapping descriptor list for a
    /// recognized (tag, version) pair, or <c>null</c> for an unrecognized one — it NEVER throws.
    ///
    /// <para><b>Versions covered</b> are exactly the values observed/pinned in <c>TgenEraVersions</c>
    /// (Plan 01 Task 3, grounded against real SWGEmu + SWG Infinity assets). Most tags ship the same
    /// version in both lineages; <c>BREC</c> is the one genuine low(0002)/high(0003) divergence. A version
    /// outside this set is intentionally absent so the decoder raw-falls-back the whole chunk (D-02 — never
    /// guess field offsets).</para>
    ///
    /// <para><b>Feather block</b> (review-source: higher FORM versions add a feather-override): for the
    /// tags whose observed version is NOT <c>0000</c>, the layout appends a feather-function enum (int32)
    /// + a feather-distance float (float32) after the tag's base scalars — exactly matching the on-disk
    /// shape. Tags pinned at version <c>0000</c> (AHCN/ACCN/ACRH/AFCN) carry no feather block (Pitfall 5 —
    /// never read a field a version did not write).</para>
    /// </summary>
    public static class TgenFieldLayouts
    {
        // Fixed scalar widths (all SWG payload scalars are 4-byte LE).
        private const int W = 4;

        /// <summary>The layer-item-header FORM tag carrying the int32 active flag (read↔write parity location).</summary>
        public const string LayerHeaderTag = "IHDR";

        /// <summary>The synthetic version key under which the IHDR active-flag descriptor is registered.</summary>
        public const string LayerHeaderVersion = "active";

        /// <summary>The descriptor field name of the IHDR int32 active flag.</summary>
        public const string ActiveFieldName = "active";

        /// <summary>
        /// Returns the ordered descriptor list for <paramref name="tag"/> at <paramref name="version"/>,
        /// or <c>null</c> if the (tag, version) pair is not a recognized Tier-1 layout. Never throws.
        /// </summary>
        public static IReadOnlyList<TgenFieldDescriptor> For(string tag, string version)
        {
            if (tag == null || version == null) return null;
            if (Table.TryGetValue(Key(tag, version), out var list)) return list;
            return null;
        }

        /// <summary>
        /// True iff a complete typed descriptor exists for the (tag, version) pair (i.e. the tag is in the
        /// Tier-1 set at a recognized version). Convenience over a null-check on <see cref="For"/>.
        /// </summary>
        public static bool HasLayout(string tag, string version) => For(tag, version) != null;

        /// <summary>The sum of all descriptor widths for a (tag, version) — the fixed DATA payload length.</summary>
        public static int PayloadLength(string tag, string version)
        {
            var list = For(tag, version);
            if (list == null) return -1;
            int total = 0;
            for (int i = 0; i < list.Count; i++) total += list[i].Width;
            return total;
        }

        private static string Key(string tag, string version) => tag + "/" + version;

        // ─────────────────────────────────────────────────────────────────────
        // Field-builder helpers.
        // ─────────────────────────────────────────────────────────────────────

        private static TgenFieldDescriptor F32(string name, int offset, bool editable = true) =>
            new TgenFieldDescriptor(name, offset, W, true, TgenFieldEndian.Little, 0,
                TgenDisplayType.ScalarFloat, TgenEditParser.Float, editable);

        private static TgenFieldDescriptor I32(string name, int offset, bool editable = true) =>
            new TgenFieldDescriptor(name, offset, W, true, TgenFieldEndian.Little, 0,
                TgenDisplayType.Int32, TgenEditParser.Int32, editable);

        private static TgenFieldDescriptor Enum32(string name, int offset, bool editable = true) =>
            new TgenFieldDescriptor(name, offset, W, true, TgenFieldEndian.Little, W,
                TgenDisplayType.Enum32, TgenEditParser.Int32, editable);

        private static TgenFieldDescriptor Family(string name, int offset, bool editable = true) =>
            new TgenFieldDescriptor(name, offset, W, true, TgenFieldEndian.Little, 0,
                TgenDisplayType.FamilyIdRef, TgenEditParser.Int32, editable);

        // Appends the two-field feather block (function enum + distance float) starting at the given
        // offset. The observed higher-version tags all carry this trailing block.
        private static void AddFeather(List<TgenFieldDescriptor> fields, int offset)
        {
            fields.Add(Enum32("featherFunction", offset));
            fields.Add(F32("featherDistance", offset + W));
        }

        // Builds an ordered, contiguous list from the supplied base fields, optionally appending the
        // feather block when the tag's observed version is non-0000.
        private static IReadOnlyList<TgenFieldDescriptor> Layout(bool withFeather, params TgenFieldDescriptor[] baseFields)
        {
            var fields = new List<TgenFieldDescriptor>(baseFields);
            if (withFeather)
            {
                int next = baseFields.Length == 0 ? 0 : baseFields[baseFields.Length - 1].Offset + baseFields[baseFields.Length - 1].Width;
                AddFeather(fields, next);
            }
            return fields.AsReadOnly();
        }

        // ─────────────────────────────────────────────────────────────────────
        // The single-source table — keyed (tag/version), populated at the EXACT versions pinned in
        // TgenEraVersions (Plan 01). Offsets are contiguous from 0; widths sum to the fixed DATA length.
        // ─────────────────────────────────────────────────────────────────────

        private static readonly IReadOnlyDictionary<string, IReadOnlyList<TgenFieldDescriptor>> Table = Build();

        private static IReadOnlyDictionary<string, IReadOnlyList<TgenFieldDescriptor>> Build()
        {
            var t = new Dictionary<string, IReadOnlyList<TgenFieldDescriptor>>(StringComparer.Ordinal);

            // ── Layer-item header ──────────────────────────────────────────
            // IHDR layer-item header: [int32 active][cstring name]. Only the active flag (the int32 at
            // offset 0) is an editable fixed-length field; the trailing variable-length name is copied
            // verbatim by the encoder (it overwrites ONLY the descriptor span). Keyed under the synthetic
            // "active" version so the apply-save active branch routes the int32 write through the
            // single-source encoder rather than hand-rolling the LE packing (WR-03 / review concern #2).
            t[Key(LayerHeaderTag, LayerHeaderVersion)] = Layout(false,
                new TgenFieldDescriptor(ActiveFieldName, 0, W, true, TgenFieldEndian.Little, 0,
                    TgenDisplayType.ActiveFlag, TgenEditParser.Bool32, true));

            // ── Affectors ──────────────────────────────────────────────────
            // AHCN AffectorHeightConstant v0000: [operation enum][height float]; no feather at 0000.
            t[Key("AHCN", "0000")] = Layout(false, Enum32("operation", 0), F32("height", W));

            // AHTR AffectorHeightTerrace v0004: [fraction float][height float] + feather.
            t[Key("AHTR", "0004")] = Layout(true, F32("fraction", 0), F32("height", W));

            // ACCN AffectorColorConstant v0000: [r int][g int][b int]; no feather at 0000.
            t[Key("ACCN", "0000")] = Layout(false, I32("colorR", 0), I32("colorG", W), I32("colorB", 2 * W));

            // ACRH AffectorColorRampHeight v0000: [lowHeight float][highHeight float]; no feather at 0000.
            t[Key("ACRH", "0000")] = Layout(false, F32("lowHeight", 0), F32("highHeight", W));

            // ASCN AffectorShaderConstant v0001: [familyId ref] + feather override.
            t[Key("ASCN", "0001")] = Layout(true, Family("familyId", 0));

            // ASRP AffectorShaderReplace v0001: [sourceFamilyId ref][destinationFamilyId ref] + feather.
            t[Key("ASRP", "0001")] = Layout(true, Family("sourceFamilyId", 0), Family("destinationFamilyId", W));

            // AFCN flora-static-collidable-constant alias v0000 (still-ASSUMED — Plan 04 Task 3 confirms):
            // [familyId ref][density float]; no feather at 0000.
            t[Key("AFCN", "0000")] = Layout(false, Family("familyId", 0), F32("density", W));

            // AFSC FloraStaticCollidableConstant v0004: [familyId ref][density float] + feather.
            t[Key("AFSC", "0004")] = Layout(true, Family("familyId", 0), F32("density", W));

            // AFSN FloraStaticNonCollidableConstant v0004: [familyId ref][density float] + feather.
            t[Key("AFSN", "0004")] = Layout(true, Family("familyId", 0), F32("density", W));

            // ── Boundaries ────────────────────────────────────────────────
            // BCIR BoundaryCircle v0002: [centerX float][centerZ float][radius float] + feather.
            t[Key("BCIR", "0002")] = Layout(true, F32("centerX", 0), F32("centerZ", W), F32("radius", 2 * W));

            // BREC BoundaryRectangle — the one genuine lineage divergence: low v0002, high v0003. Both arms
            // carry [x0][z0][x1][z1] + feather (the higher arm differs by version word only in the matrix).
            t[Key("BREC", "0002")] = Layout(true, F32("x0", 0), F32("z0", W), F32("x1", 2 * W), F32("z1", 3 * W));
            t[Key("BREC", "0003")] = Layout(true, F32("x0", 0), F32("z0", W), F32("x1", 2 * W), F32("z1", 3 * W));

            // ── Filters ───────────────────────────────────────────────────
            // FHGT FilterHeight v0002: [lowHeight float][highHeight float] + feather.
            t[Key("FHGT", "0002")] = Layout(true, F32("lowHeight", 0), F32("highHeight", W));

            // FSLP FilterSlope v0002: [minSlope float][maxSlope float] + feather.
            t[Key("FSLP", "0002")] = Layout(true, F32("minSlope", 0), F32("maxSlope", W));

            return t;
        }
    }
}
