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
// Implementation original to Utinni under MIT.
//
// Wave 1 (plan 23-03) GREEN: the engine-level kernel decode/encode goldens. These exercise the headless
// KernelCodec directly over leaf-chunk PAYLOAD bytes (TemplateTestFixtures.*Payload()), NOT a whole IFF
// file -- the version-FORM-aware match + D-05 precedence (which need a whole file + the resolver) are
// Wave-2 concerns (plan 23-04) and stay RED-via-Skip below. Method names embed the VALIDATION.md
// --filter tokens (Template, KernelCodec, CountRecompute, Roundtrip, Array, Preset, Truncated,
// VersionMatch, Precedence); methodDisplay=method makes the method name the VSTest DisplayName.

using System.Collections.Generic;
using System.Text;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Template;
using Xunit;

namespace Utinni.Cli.Tests.Template
{
    /// <summary>
    /// Per-type kernel decode/encode goldens + the CRITICAL count-from-prior grow/shrink round-trip
    /// (DEC-C3) + array / preset goldens, all at the headless-engine (payload) level (plan 23-03). The
    /// version-FORM divergence + D-05 altitude precedence goldens remain RED-via-Skip pending the Wave-2
    /// resolver (plan 23-04). Filter tokens live in the method names.
    /// </summary>
    public sealed class KernelCodecTests
    {
        // ── per-type decode/encode (PROD-IFFT-01) — filter "Template&KernelCodec" ──

        [Fact]
        [Trait("Category", "TemplateKernelCodec")]
        public void Template_KernelCodec_EveryScalarType_DecodesToExpectedValue_ReEncodesByteIdentical()
        {
            byte[] payload = TemplateTestFixtures.KernPayload();
            TemplateModel template = BuildKernTemplate();

            DecodedTemplate decoded = KernelCodec.Decode(template, payload);

            Assert.Equal(-5L, decoded.Values["i8"]);
            Assert.Equal(250L, decoded.Values["u8"]);
            Assert.Equal(-1000L, decoded.Values["i16"]);
            Assert.Equal(60000L, decoded.Values["u16"]);
            Assert.Equal(-123456L, decoded.Values["i32"]);
            Assert.Equal(4000000000L, decoded.Values["u32"]);
            Assert.Equal(1.5f, (float)decoded.Values["f32"]);
            Assert.Equal(2.25, (double)decoded.Values["f64"]);
            Assert.Equal("kernel/test.name", decoded.Values["name"]);
            Assert.Equal("CHAR8", Encoding.ASCII.GetString(TrimNul((byte[])decoded.Values["fixed8"])));
            Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, (byte[])decoded.Values["raw4"]);
            Assert.Equal(new byte[] { 0x00, 0x00 }, (byte[])decoded.Values["pad2"]);

            byte[] reencoded = KernelCodec.Encode(decoded);
            Assert.Equal(payload, reencoded);
        }

        // ── CRITICAL count-from-prior grow/shrink round-trip (PROD-IFFT-02, DEC-C3) ──
        // filter "Template&CountRecompute&Roundtrip"

        [Fact]
        [Trait("Category", "TemplateCountRecomputeRoundtrip")]
        public void Template_CountRecompute_Roundtrip_AddElement_CountFieldReadsNPlus1_PayloadByteExact()
        {
            TemplateModel template = BuildCountFromPriorTemplate();
            DecodedTemplate decoded = KernelCodec.Decode(template, TemplateTestFixtures.CountFromPriorPayload(3));

            // ADD one element shaped like CountFromPriorPayload's 4th record: id=103, weight=0.25*4=1.0f.
            var elements = (List<object>)decoded.Values["records"];
            elements.Add(new Dictionary<string, object>
            {
                { "id", (long)103 },
                { "weight", 1.0f }
            });

            byte[] reencoded = KernelCodec.Encode(decoded);

            // The count field on the wire must now read 4 (recomputed, not the stale 3) AND the whole
            // payload must equal the canonical n=4 fixture byte-for-byte.
            Assert.Equal(TemplateTestFixtures.CountFromPriorPayload(4), reencoded);
            Assert.Equal((uint)4, System.BitConverter.ToUInt32(reencoded, 0));
        }

        [Fact]
        [Trait("Category", "TemplateCountRecomputeRoundtrip")]
        public void Template_CountRecompute_Roundtrip_RemoveElement_CountFieldReadsNMinus1_PayloadByteExact()
        {
            TemplateModel template = BuildCountFromPriorTemplate();
            DecodedTemplate decoded = KernelCodec.Decode(template, TemplateTestFixtures.CountFromPriorPayload(3));

            var elements = (List<object>)decoded.Values["records"];
            elements.RemoveAt(elements.Count - 1); // shrink to 2

            byte[] reencoded = KernelCodec.Encode(decoded);

            Assert.Equal(TemplateTestFixtures.CountFromPriorPayload(2), reencoded);
            Assert.Equal((uint)2, System.BitConverter.ToUInt32(reencoded, 0));
        }

        // ── over-claimed count throws Truncated, not over-read — filter "Template&KernelCodec&Truncated" ──

        [Fact]
        [Trait("Category", "TemplateKernelCodecTruncated")]
        public void Template_KernelCodec_Truncated_OverClaimedCount_ThrowsRatherThanOverReads()
        {
            TemplateModel template = BuildCountFromPriorTemplate();
            // A payload whose count field claims 99 records but only carries 1 record's worth of bytes.
            byte[] forged = new byte[4 + 6]; // u32 count + one {u16 + f32} = 6 bytes
            forged[0] = 99; // count = 99 (LE), grossly over-claims

            DecoderException ex = Assert.Throws<DecoderException>(() => KernelCodec.Decode(template, forged));
            Assert.Equal(DecoderError.Truncated, ex.Kind);
        }

        // ── CR-02: count auto-recompute overflowing its on-wire width throws, never wraps ──
        // filter "Template&CountRecompute&Roundtrip"

        [Fact]
        [Trait("Category", "TemplateCountRecomputeRoundtrip")]
        public void Template_CountRecompute_Roundtrip_GrowPastU8CountWidth_ThrowsRatherThanWraps()
        {
            // A count-from-prior array whose count field is declared U8 (max 255). Decode a 1-element
            // payload, then grow the array to 256 elements. The auto-recomputed count (256) does NOT fit a
            // U8 field: encode must throw a typed TemplateException, NOT silently write a wrapped 0.
            TemplateModel template = BuildU8CountFromPriorTemplate();

            // Hand-build a decoded carrier: a U8 count + an array of single-u8 records.
            var records = new List<object>();
            for (int i = 0; i < 256; i++) records.Add((long)1);
            var decoded = new DecodedTemplate
            {
                Template = template,
                Values = new Dictionary<string, object>
                {
                    { "count", (long)1 }, // stale; auto-recompute will derive 256 from the array
                    { "records", records }
                }
            };

            TemplateException ex = Assert.Throws<TemplateException>(() => KernelCodec.Encode(decoded));
            Assert.Contains("does not fit", ex.Message);
        }

        [Fact]
        [Trait("Category", "TemplateCountRecomputeRoundtrip")]
        public void Template_CountRecompute_Roundtrip_GrowWithinU8CountWidth_Succeeds()
        {
            // The boundary case: 255 elements still fits a U8 count field, so encode must NOT throw.
            TemplateModel template = BuildU8CountFromPriorTemplate();

            var records = new List<object>();
            for (int i = 0; i < 255; i++) records.Add((long)1);
            var decoded = new DecodedTemplate
            {
                Template = template,
                Values = new Dictionary<string, object>
                {
                    { "count", (long)0 },
                    { "records", records }
                }
            };

            byte[] reencoded = KernelCodec.Encode(decoded);
            Assert.Equal(255, reencoded[0]); // U8 count recomputed to 255
            Assert.Equal(1 + 255, reencoded.Length);
        }

        // CNT8: u8 count then count × u8 record (a count-from-prior array sized by a U8 field, for CR-02).
        private static TemplateModel BuildU8CountFromPriorTemplate()
        {
            return new TemplateModel
            {
                Version = 1,
                MatchLeafTag = "CNT8",
                Fields = new List<FieldRecord>
                {
                    new FieldRecord { Name = "count", Type = KernelType.U8 },
                    new FieldRecord
                    {
                        Name = "records",
                        Type = KernelType.Array,
                        Repeat = new RepeatSpec { Kind = RepeatKind.CountFromField, CountFieldName = "count" },
                        StructFields = new List<FieldRecord> { new FieldRecord { Name = "b", Type = KernelType.U8 } }
                    }
                }
            };
        }

        // ── trailing-remainder + fixed-count arrays (PROD-IFFT-02) — filter "Template&Array&Roundtrip" ──

        [Fact]
        [Trait("Category", "TemplateArrayRoundtrip")]
        public void Template_Array_Roundtrip_FixedCountAndUntilEnd_ByteExact()
        {
            byte[] payload = TemplateTestFixtures.FixedAndRemainderArrayPayload();
            TemplateModel template = BuildArraysTemplate();

            DecodedTemplate decoded = KernelCodec.Decode(template, payload);

            var fixedArr = (List<object>)decoded.Values["fixed3"];
            Assert.Equal(3, fixedArr.Count);
            Assert.Equal(11L, fixedArr[0]);
            Assert.Equal(33L, fixedArr[2]);

            var remainder = (List<object>)decoded.Values["rest"];
            Assert.Equal(4, remainder.Count);
            Assert.Equal(1.0f, (float)remainder[0]);
            Assert.Equal(4.0f, (float)remainder[3]);

            byte[] reencoded = KernelCodec.Encode(decoded);
            Assert.Equal(payload, reencoded);
        }

        // ── D-09 presets (PROD-IFFT-01) — filter "Template&Preset" ──

        [Fact]
        [Trait("Category", "TemplatePreset")]
        public void Template_Preset_VectorQuaternionMatrixColor_DecodeToExactValues()
        {
            byte[] payload = TemplateTestFixtures.PresetPayload();
            TemplateModel template = BuildPresetTemplate();

            DecodedTemplate decoded = KernelCodec.Decode(template, payload);

            var vector = (Dictionary<string, object>)decoded.Values["vector"];
            Assert.Equal(1.0f, (float)vector["x"]);
            Assert.Equal(2.0f, (float)vector["y"]);
            Assert.Equal(3.0f, (float)vector["z"]);

            var quat = (Dictionary<string, object>)decoded.Values["quaternion"];
            Assert.Equal(1.0f, (float)quat["w"]); // w FIRST
            Assert.Equal(0.0f, (float)quat["x"]);
            Assert.Equal(0.0f, (float)quat["y"]);
            Assert.Equal(0.0f, (float)quat["z"]);

            var matrix = (Dictionary<string, object>)decoded.Values["matrix"];
            Assert.Equal(0.0f, (float)matrix["m00"]);
            Assert.Equal(11.0f, (float)matrix["m23"]); // last of 12 row-major floats

            var color = (Dictionary<string, object>)decoded.Values["color"];
            Assert.Equal(255L, color["r"]);
            Assert.Equal(128L, color["g"]);
            Assert.Equal(0L, color["b"]);

            byte[] reencoded = KernelCodec.Encode(decoded);
            Assert.Equal(payload, reencoded);
        }

        // ── version-FORM divergence (PROD-IFFT-02) — filter "Template&VersionMatch" (Wave-2, plan 23-04) ──
        // GREEN as of plan 23-04: the dedicated resolver-level assertions live in TemplateResolverTests
        // (filters Template&VersionMatch / Template&Precedence / Template&MultiMatch / Template&Fit&Flag).
        // These two anchors are kept so the original RED-via-Skip token row stays visible in this file's
        // history; they delegate to the resolver so the VALIDATION.md --filter rows resolve here too.

        [Theory]
        [Trait("Category", "TemplateVersionMatch")]
        [InlineData(TemplateTestFixtures.VersionLow)]
        [InlineData(TemplateTestFixtures.VersionHigh)]
        public void Template_VersionMatch_DivergentCpapLayout_PicksRightLayoutPerVersionForm(string version)
        {
            byte[] file = TemplateTestFixtures.BuildVersionDivergent(version);
            MutableIffDocument doc = TemplateResolverTests.LoadMutable(file);
            string leafId = "FORM:TPLX/0/FORM:" + version + "/0/CPAP:CPAP/0";

            var low = TemplateResolverTests.CpapTemplate(TemplateTestFixtures.VersionLow, 1);
            var high = TemplateResolverTests.CpapTemplate(TemplateTestFixtures.VersionHigh, 5);
            var resolver = new TemplateResolver(new[] { low, high });

            TemplateResolution res = resolver.Resolve(doc, leafId);

            Assert.NotNull(res.Best);
            Assert.True(res.Best.Fits, "the version-specific template must round-trip the version's layout");
            string expectedVersion = version == TemplateTestFixtures.VersionHigh
                ? TemplateTestFixtures.VersionHigh
                : TemplateTestFixtures.VersionLow;
            Assert.Contains("FORM:" + expectedVersion, res.Best.Template.MatchAncestorPath);
        }

        // ── D-05 altitude / precedence (PROD-IFFT-02) — filter "Template&Precedence" (Wave-2, plan 23-04) ──

        [Fact]
        [Trait("Category", "TemplatePrecedence")]
        public void Template_Precedence_BuiltinRootForm_TemplateNeverEngages()
        {
            byte[] file = TemplateTestFixtures.BuildBuiltinRootFile(); // root FORM CLEF (built-in)
            MutableIffDocument doc = TemplateResolverTests.LoadMutable(file);
            string leafId = "FORM:CLEF/0/FORM:0001/0/CPAP:CPAP/0";

            // A template that WOULD match the leaf tag — but the root FORM is built-in-owned (CLEF).
            var t = TemplateResolverTests.CpapTemplate("0001", 1);
            var resolver = new TemplateResolver(new[] { t });

            TemplateResolution res = resolver.Resolve(doc, leafId);

            Assert.True(res.SuppressedByBuiltin, "a built-in root FORM must suppress all templates (D-05)");
            Assert.Null(res.Best);
        }

        // ── template builders mirroring TemplateTestFixtures payload shapes ──

        private static byte[] TrimNul(byte[] raw)
        {
            int len = 0;
            while (len < raw.Length && raw[len] != 0) len++;
            byte[] trimmed = new byte[len];
            System.Array.Copy(raw, trimmed, len);
            return trimmed;
        }

        // KERN: i8,u8,i16,u16,i32,u32,f32,f64,cstring,char[8],raw[4],pad[2] (KernPayload order).
        private static TemplateModel BuildKernTemplate()
        {
            return new TemplateModel
            {
                Version = 1,
                MatchLeafTag = "KERN",
                Fields = new List<FieldRecord>
                {
                    new FieldRecord { Name = "i8", Type = KernelType.I8 },
                    new FieldRecord { Name = "u8", Type = KernelType.U8 },
                    new FieldRecord { Name = "i16", Type = KernelType.I16 },
                    new FieldRecord { Name = "u16", Type = KernelType.U16 },
                    new FieldRecord { Name = "i32", Type = KernelType.I32 },
                    new FieldRecord { Name = "u32", Type = KernelType.U32 },
                    new FieldRecord { Name = "f32", Type = KernelType.F32 },
                    new FieldRecord { Name = "f64", Type = KernelType.F64 },
                    new FieldRecord { Name = "name", Type = KernelType.CString, Encoding = "ascii" },
                    new FieldRecord { Name = "fixed8", Type = KernelType.FixedChar, ByteWidth = 8, Encoding = "ascii" },
                    new FieldRecord { Name = "raw4", Type = KernelType.RawBytes, ByteWidth = 4 },
                    new FieldRecord { Name = "pad2", Type = KernelType.Pad, ByteWidth = 2 }
                }
            };
        }

        // CNTR: u32 count then count × { u16 id, f32 weight } (CountFromPriorPayload shape, DEC-C3).
        private static TemplateModel BuildCountFromPriorTemplate()
        {
            return new TemplateModel
            {
                Version = 1,
                MatchLeafTag = "CNTR",
                Fields = new List<FieldRecord>
                {
                    new FieldRecord { Name = "count", Type = KernelType.U32 },
                    new FieldRecord
                    {
                        Name = "records",
                        Type = KernelType.Array,
                        Repeat = new RepeatSpec { Kind = RepeatKind.CountFromField, CountFieldName = "count" },
                        StructFields = new List<FieldRecord>
                        {
                            new FieldRecord { Name = "id", Type = KernelType.U16 },
                            new FieldRecord { Name = "weight", Type = KernelType.F32 }
                        }
                    }
                }
            };
        }

        // FIXA: fixed[3] of i32 then until-end of f32 (FixedAndRemainderArrayPayload shape).
        private static TemplateModel BuildArraysTemplate()
        {
            return new TemplateModel
            {
                Version = 1,
                MatchLeafTag = "FIXA",
                Fields = new List<FieldRecord>
                {
                    new FieldRecord
                    {
                        Name = "fixed3",
                        Type = KernelType.Array,
                        Repeat = new RepeatSpec { Kind = RepeatKind.FixedCount, FixedCount = 3 },
                        StructFields = new List<FieldRecord> { new FieldRecord { Name = "v", Type = KernelType.I32 } }
                    },
                    new FieldRecord
                    {
                        Name = "rest",
                        Type = KernelType.Array,
                        Repeat = new RepeatSpec { Kind = RepeatKind.UntilEnd },
                        StructFields = new List<FieldRecord> { new FieldRecord { Name = "v", Type = KernelType.F32 } }
                    }
                }
            };
        }

        // PRST: vector(x,y,z) + quaternion(w,x,y,z) + matrix(3x4 row-major) + color(r,g,b u8)
        // (PresetPayload shape — the D-09 composite presets).
        private static TemplateModel BuildPresetTemplate()
        {
            var matrixFields = new List<FieldRecord>();
            string[] m = { "m00", "m01", "m02", "m03", "m10", "m11", "m12", "m13", "m20", "m21", "m22", "m23" };
            foreach (string name in m) matrixFields.Add(new FieldRecord { Name = name, Type = KernelType.F32 });

            return new TemplateModel
            {
                Version = 1,
                MatchLeafTag = "PRST",
                Fields = new List<FieldRecord>
                {
                    new FieldRecord
                    {
                        Name = "vector", Type = KernelType.Struct,
                        StructFields = new List<FieldRecord>
                        {
                            new FieldRecord { Name = "x", Type = KernelType.F32 },
                            new FieldRecord { Name = "y", Type = KernelType.F32 },
                            new FieldRecord { Name = "z", Type = KernelType.F32 }
                        }
                    },
                    new FieldRecord
                    {
                        Name = "quaternion", Type = KernelType.Struct,
                        StructFields = new List<FieldRecord>
                        {
                            new FieldRecord { Name = "w", Type = KernelType.F32 },
                            new FieldRecord { Name = "x", Type = KernelType.F32 },
                            new FieldRecord { Name = "y", Type = KernelType.F32 },
                            new FieldRecord { Name = "z", Type = KernelType.F32 }
                        }
                    },
                    new FieldRecord { Name = "matrix", Type = KernelType.Struct, StructFields = matrixFields },
                    new FieldRecord
                    {
                        Name = "color", Type = KernelType.Struct,
                        StructFields = new List<FieldRecord>
                        {
                            new FieldRecord { Name = "r", Type = KernelType.U8 },
                            new FieldRecord { Name = "g", Type = KernelType.U8 },
                            new FieldRecord { Name = "b", Type = KernelType.U8 }
                        }
                    }
                }
            };
        }
    }
}
