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
using System.Text;
using UtinniCoreDotNet.Formats.ObjectTemplate;
using Xunit;

namespace UtinniCoreDotNet.Tests.ObjectTemplate
{
    /// <summary>
    /// Coverage for the self-describing object-template param codec (11-01 Task 1):
    /// <see cref="ObjectTemplateParamCodec"/> + <see cref="ObjectTemplateParamValue"/>. Asserts
    /// decode→encode byte-identity for the scalar set, verbatim delta-byte preservation, NONE
    /// handling, and the consume-exactly-or-hex defensive routing (WEIGHTED_LIST and short/long
    /// scalar payloads route to RawBytesHexFallback, never mis-typed).
    /// </summary>
    public class ObjectTemplateParamTests
    {
        // Builds a param chunk payload = NUL-terminated name + the supplied value-region bytes.
        private static byte[] Chunk(string name, params byte[] valueRegion)
        {
            using (var ms = new MemoryStream())
            {
                byte[] nameAscii = Encoding.ASCII.GetBytes(name);
                ms.Write(nameAscii, 0, nameAscii.Length);
                ms.WriteByte(0);
                if (valueRegion != null && valueRegion.Length > 0)
                {
                    ms.Write(valueRegion, 0, valueRegion.Length);
                }
                return ms.ToArray();
            }
        }

        private static byte[] Concat(params byte[][] parts)
        {
            var list = new List<byte>();
            foreach (var p in parts) list.AddRange(p);
            return list.ToArray();
        }

        private static byte[] Int32Le(int v)
        {
            return new[] { (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)((v >> 24) & 0xFF) };
        }

        // ── SINGLE int: tag(1) + delta + int32 ───────────────────────────────

        [Fact]
        public void SingleInt_DecodeEncode_ByteIdentical()
        {
            byte[] payload = Chunk("count", Concat(new byte[] { 1, (byte)' ' }, Int32Le(42)));

            ObjectTemplateParamEntry entry = ObjectTemplateParamCodec.Decode(payload);

            Assert.Equal("count", entry.FieldName);
            Assert.Equal(ObjectTemplateParamKind.Int, entry.Value.Kind);
            Assert.Equal(42, entry.Value.IntValue);
            Assert.Equal((byte)' ', entry.Value.DeltaType.Value);

            byte[] reEncoded = ObjectTemplateParamCodec.Encode(entry.FieldName, entry.Value);
            Assert.Equal(payload, reEncoded);
        }

        [Fact]
        public void SingleInt_PlusDelta_RoundTripsVerbatim()
        {
            // The '+' delta byte must round-trip verbatim (delta-on-base numeric param).
            byte[] payload = Chunk("hitPoints", Concat(new byte[] { 1, (byte)'+' }, Int32Le(7)));

            ObjectTemplateParamEntry entry = ObjectTemplateParamCodec.Decode(payload);

            Assert.Equal(ObjectTemplateParamKind.Int, entry.Value.Kind);
            Assert.Equal((byte)'+', entry.Value.DeltaType.Value);
            Assert.Equal(7, entry.Value.IntValue);

            byte[] reEncoded = ObjectTemplateParamCodec.Encode(entry.FieldName, entry.Value);
            Assert.Equal(payload, reEncoded);
            // The delta byte sits at name + NUL + tag → index = name.Length + 2.
            int deltaIndex = "hitPoints".Length + 2;
            Assert.Equal((byte)'+', reEncoded[deltaIndex]);
        }

        // ── SINGLE float: encode→decode→encode byte-identical ────────────────

        [Fact]
        public void SingleFloat_EncodeDecodeEncode_ByteIdentical()
        {
            // A float SINGLE value carries the same tag + delta + 4 value bytes as an int; the generic
            // decoder cannot disambiguate int vs float from bytes, so it surfaces a typed Int — but the
            // 4 value bytes (and delta) round-trip byte-for-byte either way.
            ObjectTemplateParamValue floatValue = ObjectTemplateParamValue.FromFloat(2.5f, (byte)' ');
            byte[] payload = ObjectTemplateParamCodec.Encode("radius", floatValue);

            ObjectTemplateParamEntry entry = ObjectTemplateParamCodec.Decode(payload);
            byte[] reEncoded = ObjectTemplateParamCodec.Encode(entry.FieldName, entry.Value);

            Assert.Equal(payload, reEncoded);
            Assert.Equal((byte)' ', entry.Value.DeltaType.Value);
        }

        // ── SINGLE bool: tag(1) + int8 bool, no delta byte ───────────────────

        [Fact]
        public void SingleBool_DecodeEncode_ByteIdentical_NoDeltaByte()
        {
            byte[] payloadTrue = Chunk("targetable", 1, 1);
            byte[] payloadFalse = Chunk("targetable", 1, 0);

            ObjectTemplateParamEntry t = ObjectTemplateParamCodec.Decode(payloadTrue);
            ObjectTemplateParamEntry f = ObjectTemplateParamCodec.Decode(payloadFalse);

            Assert.Equal(ObjectTemplateParamKind.Bool, t.Value.Kind);
            Assert.True(t.Value.BoolValue);
            Assert.False(f.Value.BoolValue);
            Assert.Null(t.Value.DeltaType); // bool carries NO delta byte

            Assert.Equal(payloadTrue, ObjectTemplateParamCodec.Encode(t.FieldName, t.Value));
            Assert.Equal(payloadFalse, ObjectTemplateParamCodec.Encode(f.FieldName, f.Value));
        }

        // ── SINGLE string: tag(1) + NUL-terminated, no delta byte ────────────

        [Fact]
        public void SingleString_DecodeEncode_ByteIdentical()
        {
            byte[] body = Encoding.ASCII.GetBytes("appearance/foo.apt");
            byte[] payload = Chunk("appearanceFilename", Concat(new byte[] { 1 }, body, new byte[] { 0 }));

            ObjectTemplateParamEntry entry = ObjectTemplateParamCodec.Decode(payload);

            Assert.Equal(ObjectTemplateParamKind.String, entry.Value.Kind);
            Assert.Equal("appearance/foo.apt", entry.Value.StringValue);
            Assert.Null(entry.Value.DeltaType);

            Assert.Equal(payload, ObjectTemplateParamCodec.Encode(entry.FieldName, entry.Value));
        }

        // ── NONE: tag(0), no value bytes ─────────────────────────────────────

        [Fact]
        public void None_DecodeEncode_ByteIdentical()
        {
            byte[] payload = Chunk("clearedField", 0);

            ObjectTemplateParamEntry entry = ObjectTemplateParamCodec.Decode(payload);

            Assert.Equal(ObjectTemplateParamKind.None, entry.Value.Kind);
            Assert.Equal(ObjectTemplateDataTypeTag.None, entry.Value.DataTypeTag);

            Assert.Equal(payload, ObjectTemplateParamCodec.Encode(entry.FieldName, entry.Value));
        }

        // ── WEIGHTED_LIST tag (2) → RawBytesHexFallback (not mis-typed) ──────

        [Fact]
        public void WeightedListTag_RoutesToHexFallback()
        {
            // tag=2 (WEIGHTED_LIST) + int32 count + (weight + value) — generic decoder must NOT type it.
            byte[] valueRegion = Concat(new byte[] { 2 }, Int32Le(1), Int32Le(100), new byte[] { 1, (byte)' ' }, Int32Le(5));
            byte[] payload = Chunk("lootList", valueRegion);

            ObjectTemplateParamEntry entry = ObjectTemplateParamCodec.Decode(payload);

            Assert.Equal(ObjectTemplateParamKind.RawBytesHexFallback, entry.Value.Kind);
            Assert.Equal(ObjectTemplateDataTypeTag.WeightedList, entry.Value.DataTypeTag);
            Assert.Equal(valueRegion, entry.Value.GetRawBytesCopy());

            Assert.Equal(payload, ObjectTemplateParamCodec.Encode(entry.FieldName, entry.Value));
        }

        // ── Consume-exactly guard: short/long scalar payload → hex fallback ──

        [Fact]
        public void ShortScalarPayload_RoutesToHexFallback()
        {
            // SINGLE tag + delta + only 2 of the 4 expected int32 bytes → cannot consume exactly.
            byte[] valueRegion = new byte[] { 1, (byte)' ', 0x01, 0x02 };
            byte[] payload = Chunk("truncated", valueRegion);

            ObjectTemplateParamEntry entry = ObjectTemplateParamCodec.Decode(payload);

            Assert.Equal(ObjectTemplateParamKind.RawBytesHexFallback, entry.Value.Kind);
            Assert.Equal(valueRegion, entry.Value.GetRawBytesCopy());
            Assert.Equal(payload, ObjectTemplateParamCodec.Encode(entry.FieldName, entry.Value));
        }

        [Fact]
        public void LongScalarPayload_RoutesToHexFallback()
        {
            // SINGLE int region (tag + delta + int32) with TWO trailing junk bytes → not consumed exactly.
            byte[] valueRegion = Concat(new byte[] { 1, (byte)' ' }, Int32Le(9), new byte[] { 0xAB, 0xCD });
            byte[] payload = Chunk("overlong", valueRegion);

            ObjectTemplateParamEntry entry = ObjectTemplateParamCodec.Decode(payload);

            Assert.Equal(ObjectTemplateParamKind.RawBytesHexFallback, entry.Value.Kind);
            Assert.Equal(valueRegion, entry.Value.GetRawBytesCopy());
            Assert.Equal(payload, ObjectTemplateParamCodec.Encode(entry.FieldName, entry.Value));
        }

        // ── ParamTypeLabel UI-SPEC labels ────────────────────────────────────

        [Fact]
        public void ParamTypeLabel_MatchesUiSpec()
        {
            Assert.Equal("bool", ObjectTemplateParamValue.FromBool(true).ParamTypeLabel);
            Assert.Equal("int", ObjectTemplateParamValue.FromInt(1, (byte)' ').ParamTypeLabel);
            Assert.Equal("float", ObjectTemplateParamValue.FromFloat(1f, (byte)' ').ParamTypeLabel);
            Assert.Equal("string", ObjectTemplateParamValue.FromString("x").ParamTypeLabel);
            Assert.Equal("(none)", ObjectTemplateParamValue.FromNone().ParamTypeLabel);
            Assert.Equal("raw bytes (hex)",
                ObjectTemplateParamValue.FromRawBytes(new byte[] { 2, 3 }, ObjectTemplateDataTypeTag.WeightedList).ParamTypeLabel);
        }
    }
}
