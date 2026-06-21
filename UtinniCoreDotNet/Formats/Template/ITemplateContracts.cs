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
// Schema-driven IFF chunk-template contracts. The composite scalar write order this surface models
// was understood by reading swg-client-v2/.../sharedFile/.../Iff.cpp + the per-engine composite
// writers (SOE/Bootprint, All Rights Reserved): chunk PAYLOAD scalars are LITTLE-endian and strings
// are NUL-terminated C-strings (strlen+1 on disk, never length-prefixed). Only the on-disk layout was
// studied — no code, comments, identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.
//
// FROZEN CONTRACT (Wave 0, plan 23-01): definitions only, no engine logic. Waves 1-2 implement the
// KernelCodec / resolver against these EXACT type + member names. Do not rename without re-pinning the
// dependent test files (KernelCodecTests / RoundtripTemplateCommandTests / ApplySaveTemplateTests).

using System;
using System.Collections.Generic;

namespace UtinniCoreDotNet.Formats.Template
{
    /// <summary>
    /// The kernel type vocabulary (D-08). Each member is one primitive the schema-driven codec can
    /// decode/encode against a chunk payload. Sized integers are LITTLE-endian; floats are IEEE-754
    /// LE; <see cref="CString"/> is a NUL-terminated C-string (strlen+1 on disk); <see cref="FixedChar"/>
    /// is a fixed-width char[n]; <see cref="RawBytes"/>/<see cref="Pad"/> are opaque/skipped spans;
    /// <see cref="Struct"/> nests an ordered sub-field list; <see cref="Array"/> repeats an element type
    /// per a <see cref="RepeatSpec"/>.
    /// </summary>
    public enum KernelType
    {
        /// <summary>Signed 8-bit integer.</summary>
        I8,
        /// <summary>Unsigned 8-bit integer.</summary>
        U8,
        /// <summary>Signed 16-bit LE integer.</summary>
        I16,
        /// <summary>Unsigned 16-bit LE integer.</summary>
        U16,
        /// <summary>Signed 32-bit LE integer.</summary>
        I32,
        /// <summary>Unsigned 32-bit LE integer.</summary>
        U32,
        /// <summary>IEEE-754 32-bit LE float.</summary>
        F32,
        /// <summary>IEEE-754 64-bit LE double.</summary>
        F64,
        /// <summary>NUL-terminated C-string (strlen+1 bytes on disk).</summary>
        CString,
        /// <summary>Fixed-width char[n] (ByteWidth = n). WR-04: decoded as an OPAQUE n-byte span (a
        /// byte[], NOT a string) so it round-trips byte-exact -- the NUL padding and any bytes past the
        /// first NUL are preserved verbatim. On the wire it is identical to <see cref="RawBytes"/>; the
        /// distinct name only signals the editor's "char data" display intent. The <see
        /// cref="FieldRecord.Encoding"/> member does NOT apply to FixedChar (it is inert; the dialog does
        /// not offer it) -- a lossy string round-trip would break byte-exactness.</summary>
        FixedChar,
        /// <summary>Opaque byte span of fixed width (ByteWidth = n).</summary>
        RawBytes,
        /// <summary>Skipped padding span of fixed width (ByteWidth = n); round-trips verbatim.</summary>
        Pad,
        /// <summary>Nested ordered sub-field record list (<see cref="FieldRecord.StructFields"/>).</summary>
        Struct,
        /// <summary>Repeated element type governed by a <see cref="FieldRecord.Repeat"/> spec.</summary>
        Array
    }

    /// <summary>
    /// How an <see cref="KernelType.Array"/> field decides its element count (D-10).
    /// </summary>
    public enum RepeatKind
    {
        /// <summary>A literal element count fixed in the template (<see cref="RepeatSpec.FixedCount"/>).</summary>
        FixedCount,
        /// <summary>The count is read from a prior decoded field by name (<see cref="RepeatSpec.CountFieldName"/>). This is the count-from-prior case the DEC-C3 grow/shrink golden exercises.</summary>
        CountFromField,
        /// <summary>Repeat until the payload is exhausted (trailing-remainder array).</summary>
        UntilEnd
    }

    /// <summary>
    /// Array repeat policy (D-10). <see cref="Kind"/> selects which of the two count carriers applies:
    /// <see cref="FixedCount"/> for <see cref="RepeatKind.FixedCount"/>, <see cref="CountFieldName"/>
    /// for <see cref="RepeatKind.CountFromField"/>; neither for <see cref="RepeatKind.UntilEnd"/>.
    /// </summary>
    public sealed class RepeatSpec
    {
        /// <summary>Which counting strategy this array uses.</summary>
        public RepeatKind Kind { get; set; }

        /// <summary>Literal element count when <see cref="Kind"/> == <see cref="RepeatKind.FixedCount"/>.</summary>
        public int FixedCount { get; set; }

        /// <summary>Name of the prior field carrying the element count when <see cref="Kind"/> == <see cref="RepeatKind.CountFromField"/>. The codec MUST recompute this field's on-wire value from the element count when the array grows/shrinks (DEC-C3).</summary>
        public string CountFieldName { get; set; }
    }

    /// <summary>
    /// Named-value map for a field (D-11): either an enumeration (mutually exclusive values) or a flags
    /// set (bit positions 1..32). <see cref="IsFlags"/> discriminates the two interpretations of
    /// <see cref="Entries"/> (name -&gt; numeric value / bit position).
    /// </summary>
    public sealed class NamedValueMap
    {
        /// <summary>True = bit-flag set (entries are bit positions 1..32); false = enumeration (entries are exact values).</summary>
        public bool IsFlags { get; set; }

        /// <summary>Name -&gt; numeric value (enum) or bit position 1..32 (flags).</summary>
        public Dictionary<string, long> Entries { get; set; }
    }

    /// <summary>
    /// One field in a template (D-08/D-10/D-11). A field is a named, typed slot in a chunk payload.
    /// <see cref="Type"/> selects the kernel primitive; <see cref="ByteWidth"/> sizes the fixed-width
    /// kinds (FixedChar/RawBytes/Pad); <see cref="Encoding"/> names a string encoding for CString/
    /// FixedChar; <see cref="Repeat"/> is non-null only for arrays; <see cref="ValueMap"/> is optional
    /// (enum/flags decoration); <see cref="StructFields"/> is the ordered sub-field list for structs.
    /// </summary>
    public sealed class FieldRecord
    {
        /// <summary>Field name (referenced by <see cref="RepeatSpec.CountFieldName"/> for count-from-prior arrays).</summary>
        public string Name { get; set; }

        /// <summary>Kernel primitive for this field.</summary>
        public KernelType Type { get; set; }

        /// <summary>Fixed byte width for FixedChar/RawBytes/Pad (and the on-disk width of the integer kinds where it disambiguates); 0 where implied by <see cref="Type"/>.</summary>
        public int ByteWidth { get; set; }

        /// <summary>String encoding name for CString (e.g. "ascii"); null otherwise. WR-04: this is INERT
        /// for FixedChar -- FixedChar is decoded as an opaque byte span, never a string, so no encoding
        /// applies.</summary>
        public string Encoding { get; set; }

        /// <summary>Repeat policy when <see cref="Type"/> == <see cref="KernelType.Array"/>; null otherwise.</summary>
        public RepeatSpec Repeat { get; set; }

        /// <summary>Optional enum/flags decoration for the decoded scalar value; null when the raw value stands alone.</summary>
        public NamedValueMap ValueMap { get; set; }

        /// <summary>Ordered sub-field list when <see cref="Type"/> == <see cref="KernelType.Struct"/> (or the element shape of a struct array); null/empty otherwise.</summary>
        public List<FieldRecord> StructFields { get; set; }
    }

    /// <summary>
    /// A user-definable IFF chunk template (D-13). <see cref="Version"/> carries the template's own
    /// schema version. The match predicate is the ancestor-FORM path + leaf tag (D-04): a template
    /// engages on a leaf chunk whose ancestor-FORM chain ends with <see cref="MatchAncestorPath"/> and
    /// whose leaf tag is <see cref="MatchLeafTag"/>. <see cref="MatchTagOnly"/> relaxes the match to the
    /// leaf tag alone (ignore the ancestor path). <see cref="Fields"/> is the ordered field layout.
    /// </summary>
    public sealed class TemplateModel
    {
        /// <summary>Template schema version (D-13).</summary>
        public int Version { get; set; }

        /// <summary>Ancestor-FORM path predicate (D-04), e.g. the FORM sub-type chain enclosing the matched leaf.</summary>
        public string MatchAncestorPath { get; set; }

        /// <summary>Leaf tag predicate (D-04), e.g. the 4-char chunk tag the template decodes.</summary>
        public string MatchLeafTag { get; set; }

        /// <summary>When true, match on <see cref="MatchLeafTag"/> alone and ignore <see cref="MatchAncestorPath"/>.</summary>
        public bool MatchTagOnly { get; set; }

        /// <summary>Ordered field layout the codec decodes/encodes against the chunk payload.</summary>
        public List<FieldRecord> Fields { get; set; }
    }

    /// <summary>
    /// Plausibility verdict for one decoded field (D-17.2). Lets the resolver report which fields decoded
    /// to a sensible value (vs garbage from a mis-matched template) without throwing.
    /// </summary>
    public sealed class FieldPlausibility
    {
        /// <summary>Name of the field this verdict covers.</summary>
        public string FieldName { get; set; }

        /// <summary>True when the decoded value looks plausible for its kernel type.</summary>
        public bool Plausible { get; set; }
    }

    /// <summary>
    /// Fit report for a decode attempt (D-17.2). <see cref="ConsumedExactly"/> is the make-or-break
    /// signal: a correct template consumes the whole payload and no more. <see cref="PerField"/> carries
    /// the per-field plausibility battery for soft scoring.
    /// </summary>
    public sealed class FitReport
    {
        /// <summary>True iff decode consumed exactly the whole payload (<see cref="BytesConsumed"/> == <see cref="BytesTotal"/>).</summary>
        public bool ConsumedExactly { get; set; }

        /// <summary>Bytes the decode consumed.</summary>
        public int BytesConsumed { get; set; }

        /// <summary>Total payload bytes available.</summary>
        public int BytesTotal { get; set; }

        /// <summary>Per-field plausibility verdicts.</summary>
        public List<FieldPlausibility> PerField { get; set; }
    }

    /// <summary>
    /// Concrete value carrier the codec round-trips through (Wave 0 frozen shape). A decoded template is
    /// a field-name -&gt; value map plus the originating <see cref="TemplateModel"/>; this gives the tests
    /// a concrete type to assert against (and to mutate — add/remove an array element — before re-encode).
    /// Engine logic (the actual decode/encode) lands in Wave 1; this is the carrier only.
    /// </summary>
    public sealed class DecodedTemplate
    {
        /// <summary>The template this value set was decoded with.</summary>
        public TemplateModel Template { get; set; }

        /// <summary>
        /// Field name -&gt; decoded value. Scalars carry their CLR value (long/double/string/byte[]);
        /// arrays carry a <see cref="List{T}"/> of element value-maps; structs carry a nested
        /// field-name -&gt; value map. Count-from-prior fields are recomputed on encode from the bound
        /// array's element count (DEC-C3).
        /// </summary>
        public Dictionary<string, object> Values { get; set; }
    }

    // KernelCodec (Decode/Encode) is implemented in KernelCodec.cs (Wave 1, plan 23-03); the FitReport
    // PURE function (D-17.2) lives in FitReport.cs. The Wave-0 NotImplementedException stubs that formerly
    // sat here were replaced by the real engine -- the frozen surface (the type + member NAMES the test
    // files reference) is preserved, only the bodies moved into their own files.
}
