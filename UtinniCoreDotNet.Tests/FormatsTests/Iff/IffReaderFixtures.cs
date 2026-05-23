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
using System.Text;

namespace UtinniCoreDotNet.Tests.FormatsTests.Iff
{
    /// <summary>
    /// Deterministic byte-sequence builders for IFF parser unit tests.
    /// Each helper method builds a minimal, hand-crafted IFF byte sequence that exercises
    /// a specific parser code path.
    ///
    /// <para>All helpers use big-endian length fields per EA-IFF-85 (IFF is big-endian,
    /// unlike TRE which is little-endian).</para>
    /// </summary>
    public static class IffReaderFixtures
    {
        // ─────────────────────────────────────────────────────────────────────
        // Helper: write a 32-bit big-endian int
        // ─────────────────────────────────────────────────────────────────────

        private static void WriteInt32Be(BinaryWriter bw, int value)
        {
            bw.Write((byte)((value >> 24) & 0xFF));
            bw.Write((byte)((value >> 16) & 0xFF));
            bw.Write((byte)((value >> 8) & 0xFF));
            bw.Write((byte)(value & 0xFF));
        }

        private static void WriteFourCc(BinaryWriter bw, string fourCc)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(fourCc);
            bw.Write(bytes, 0, 4);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Happy-path fixtures
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the synthetic-nested IFF fixture (7 total nodes):
        /// FORM/WSNP containing:
        ///   DATA (8-byte payload 0x01..0x08)
        ///   NAME (4-byte ASCII "hi\0\0")
        ///   DESC (13-byte ASCII "Hello, World!" — odd-length, exercises pad-byte path)
        ///   FORM/OBJS containing:
        ///     INFO (4 bytes 0xA1..0xA4)
        ///     BODY (6 bytes 0xB1..0xB6)
        /// </summary>
        public static byte[] BuildHappyPath()
        {
            // Build inner FORM/OBJS first so we know its total size.
            // INFO leaf: TypeID(4) + Len(4) + 4 bytes = 12 bytes
            // BODY leaf: TypeID(4) + Len(4) + 6 bytes = 14 bytes
            // FORM/OBJS payload = SubType(4) + INFO(12) + BODY(14) = 30 bytes
            // FORM/OBJS chunk: TypeID(4) + Len(4) + 30 bytes = 38 bytes

            // Outer FORM/WSNP payload:
            //   SubType(4) = 4
            //   DATA leaf: TypeID(4) + Len(4) + 8 = 16
            //   NAME leaf: TypeID(4) + Len(4) + 4 = 12
            //   DESC leaf: TypeID(4) + Len(4) + 13 + 1(pad) = 22  [odd length 13 → pad byte]
            //   FORM/OBJS: 38
            //   Total payload = 4 + 16 + 12 + 22 + 38 = 92

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                // ── Outer FORM/WSNP ──────────────────────────────────────────
                WriteFourCc(bw, "FORM");
                WriteInt32Be(bw, 92); // outer payload length
                WriteFourCc(bw, "WSNP"); // sub-type

                // DATA leaf (8 bytes, even)
                WriteFourCc(bw, "DATA");
                WriteInt32Be(bw, 8);
                bw.Write(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 });

                // NAME leaf (4 bytes, even)
                WriteFourCc(bw, "NAME");
                WriteInt32Be(bw, 4);
                bw.Write(Encoding.ASCII.GetBytes("hi\0\0"));

                // DESC leaf (13 bytes, ODD — pad byte follows)
                WriteFourCc(bw, "DESC");
                WriteInt32Be(bw, 13);
                bw.Write(Encoding.ASCII.GetBytes("Hello, World!"));
                bw.Write((byte)0x00); // word-alignment pad byte

                // ── Inner FORM/OBJS ────────────────────────────────────────
                WriteFourCc(bw, "FORM");
                WriteInt32Be(bw, 30); // inner payload: sub-type(4) + INFO(12) + BODY(14)
                WriteFourCc(bw, "OBJS"); // sub-type

                // INFO leaf (4 bytes, even)
                WriteFourCc(bw, "INFO");
                WriteInt32Be(bw, 4);
                bw.Write(new byte[] { 0xA1, 0xA2, 0xA3, 0xA4 });

                // BODY leaf (6 bytes, even)
                WriteFourCc(bw, "BODY");
                WriteInt32Be(bw, 6);
                bw.Write(new byte[] { 0xB1, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6 });

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Builds the synthetic-secondary IFF fixture:
        /// CAT  container (with trailing space) with two PROP leaf children.
        /// REVIEWS MEDIUM-14: exercises CAT  trailing-space TypeID + PROP-as-leaf classification.
        /// </summary>
        public static byte[] BuildSecondaryHappyPath()
        {
            // PROP1 leaf: TypeID(4) + Len(4) + 6 = 14 bytes
            // PROP2 leaf: TypeID(4) + Len(4) + 4 = 12 bytes
            // CAT  payload: SubType(4) + PROP1(14) + PROP2(12) = 30 bytes
            // Total: TypeID(4) + Len(4) + 30 = 38 bytes

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                // CAT  (with trailing space) container
                WriteFourCc(bw, "CAT ");
                WriteInt32Be(bw, 30); // payload length
                WriteFourCc(bw, "OBJT"); // sub-type

                // PROP leaf 1 (6 bytes, even)
                WriteFourCc(bw, "PROP");
                WriteInt32Be(bw, 6);
                bw.Write(new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66 });

                // PROP leaf 2 (4 bytes, even)
                WriteFourCc(bw, "PROP");
                WriteInt32Be(bw, 4);
                bw.Write(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Builds a CAT  container — used by the CAT  trailing-space FOURCC test.
        /// </summary>
        public static byte[] BuildCatTypeId()
        {
            // CAT  with one DATA leaf (4 bytes)
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                // CAT  container: payload = SubType(4) + DATA(12) = 16
                WriteFourCc(bw, "CAT ");
                WriteInt32Be(bw, 16);
                WriteFourCc(bw, "TEST");

                WriteFourCc(bw, "DATA");
                WriteInt32Be(bw, 4);
                bw.Write(new byte[] { 0x01, 0x02, 0x03, 0x04 });

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Builds a CAT  container with two PROP children (for REVIEWS MEDIUM-14 regression guard).
        /// </summary>
        public static byte[] BuildCatWithPropChildren()
        {
            return BuildSecondaryHappyPath();
        }

        /// <summary>
        /// Builds a sequence with an odd-length leaf followed by another chunk.
        /// Tests that the pad byte is consumed and the second chunk parses cleanly.
        /// </summary>
        public static byte[] BuildOddLengthLeafFollowedByChunk()
        {
            // FORM/TEST containing:
            //   TEST leaf (5 bytes, ODD) + pad byte
            //   DATA leaf (4 bytes, even)
            // FORM payload: SubType(4) + TEST(14) + DATA(12) = 30
            // TEST chunk: TypeID(4) + Len(4) + 5 + 1pad = 14
            // DATA chunk: TypeID(4) + Len(4) + 4 = 12

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                WriteFourCc(bw, "FORM");
                WriteInt32Be(bw, 30);
                WriteFourCc(bw, "TEST");

                // Odd-length leaf (5 bytes)
                WriteFourCc(bw, "TEST");
                WriteInt32Be(bw, 5);
                bw.Write(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });
                bw.Write((byte)0x00); // pad byte

                // Following leaf (4 bytes)
                WriteFourCc(bw, "DATA");
                WriteInt32Be(bw, 4);
                bw.Write(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Builds an outer FORM containing an odd-length leaf followed immediately by EOF (no pad byte).
        /// REVIEWS MEDIUM-11: tests STRICT missing-pad-byte at file EOF (outermost parentEnd == stream.Length).
        /// The outer FORM's declared length EXACTLY accounts for the leaf but NOT for the pad byte.
        /// </summary>
        public static byte[] BuildOddLengthLeafAtEofMissingPad()
        {
            // FORM/TEST payload: SubType(4) + TEST(12) = 16
            // TEST leaf: TypeID(4) + Len(4) + 7 = 15 bytes (odd) — but FORM payload says 16 bytes total
            // Wait: if FORM payload length = 16 = SubType(4) + TypeID(4) + Len(4) + 7 payload = 19, that's wrong.
            //
            // Let me think: we want the leaf to have odd length (say 7), and the FORM's declared length
            // to exactly include the leaf payload but NOT the pad byte.
            // FORM payload = SubType(4) + TEST_header(8) + TEST_data(7) = 19 bytes
            // But we want the FORM to declare exactly 19 so parentEnd = 8 + 19 = 27 = stream.Length.
            // After reading the TEST leaf (7 bytes), position = 8 + 4 + 4 + 7 = 23.
            // parentEnd = 27. 23 < 27 is true, but then ReadByte would need to find a pad at position 23
            // and there's nothing there (stream ends at 27 but we haven't written the pad).
            //
            // Actually: the simplest case: FORM/TEST with one leaf of odd length 7.
            // The file ends EXACTLY at the end of the 7-byte payload with NO pad byte.
            // FORM outer payload length = SubType(4) + TypeID(4) + Len(4) + data(7) = 19
            // But FORM declared length = 19 → parentEnd = 8 + 19 = 27.
            // After reading leaf data: position = 8 (FORM header) + 4 (sub-type) + 4 (TypeID) + 4 (Len) + 7 (data) = 27.
            // position (27) >= parentEnd (27) → STRICT: throw Truncated. ✓

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                // FORM/TEST: payload = SubType(4) + leaf_header(8) + leaf_data(7) = 19
                WriteFourCc(bw, "FORM");
                WriteInt32Be(bw, 19); // declared payload length
                WriteFourCc(bw, "TEST"); // sub-type (4 bytes)

                // Leaf chunk with odd length 7
                WriteFourCc(bw, "TEST");
                WriteInt32Be(bw, 7);
                bw.Write(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 });
                // NO pad byte — file ends here at position 27 = parentEnd = 8 + 19

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Builds an outer FORM containing a nested FORM, where the nested FORM contains
        /// an odd-length leaf exactly at the nested FORM's parentEnd with NO pad byte.
        /// Iter-3 MED-3: boundary is parentEnd of the PARENT (nested FORM), not file EOF.
        /// </summary>
        public static byte[] BuildOddLengthLeafAtParentEndNoPad()
        {
            // Outer FORM/TEST:
            //   Inner FORM/INNR containing:
            //     TEST leaf (5 bytes, ODD) — immediately at inner FORM's parentEnd, no pad byte
            //
            // Inner FORM payload = SubType(4) + TEST_header(8) + TEST_data(5) = 17
            // Inner FORM chunk = 8 + 17 = 25 bytes
            // Outer FORM payload = SubType(4) + InnerFORM(25) = 29
            // File total = 8 + 29 = 37 bytes
            //
            // After reading TEST leaf (5 bytes):
            //   position = 8 (outer header) + 4 (outer sub-type) + 8 (inner header) + 4 (inner sub-type) + 8 (TEST header) + 5 (TEST data) = 37
            // inner FORM parentEnd = 8 (outer header) + 4 (outer sub-type) + 8 (inner header) + 17 (inner payload) = 37
            // Actually inner parentEnd = chunkStart_inner + 8 + length_inner = (8+4) + 8 + 17 = 37
            // position (37) >= parentEnd_inner (37) → STRICT: throw Truncated. ✓

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                // Outer FORM/TEST: payload = SubType(4) + InnerFORM(25) = 29
                WriteFourCc(bw, "FORM");
                WriteInt32Be(bw, 29);
                WriteFourCc(bw, "TEST"); // sub-type

                // Inner FORM/INNR: payload = SubType(4) + TEST_header(8) + TEST_data(5) = 17
                WriteFourCc(bw, "FORM");
                WriteInt32Be(bw, 17);
                WriteFourCc(bw, "INNR"); // sub-type

                // Leaf chunk with odd length 5 — no pad byte after
                WriteFourCc(bw, "TEST");
                WriteInt32Be(bw, 5);
                bw.Write(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });
                // NO pad byte — position == inner parentEnd → Truncated

                return ms.ToArray();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Negative-case fixtures (iter-4 HIGH-1 algorithm)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the malformed-nested-overflow IFF fixture (iter-4 HIGH-1 redesign).
        /// Total file size: 100 bytes.
        /// Outer FORM at offset 0: TypeID "FORM", length 92 (outer parentEnd = 8 + 92 = 100).
        /// Outer payload starts at offset 8 with sub-type "WSNP" (4 bytes, offsets 8-11).
        /// Inner FORM header at offset 12: TypeID "FORM", length 90.
        /// Inner declared end = (12 + 8) + 90 = 110 > outer parentEnd 100 → NestedChunkOverflow.
        /// Remaining 80 bytes are padding to reach 100 total.
        /// </summary>
        public static byte[] BuildNestedChunkOverflow()
        {
            var bytes = new byte[100];

            // Outer FORM header at offset 0
            bytes[0] = (byte)'F'; bytes[1] = (byte)'O'; bytes[2] = (byte)'R'; bytes[3] = (byte)'M';
            // Length = 92 (big-endian)
            bytes[4] = 0x00; bytes[5] = 0x00; bytes[6] = 0x00; bytes[7] = 0x5C; // 0x5C = 92

            // Sub-type at offset 8: "WSNP"
            bytes[8] = (byte)'W'; bytes[9] = (byte)'S'; bytes[10] = (byte)'N'; bytes[11] = (byte)'P';

            // Inner FORM header at offset 12
            bytes[12] = (byte)'F'; bytes[13] = (byte)'O'; bytes[14] = (byte)'R'; bytes[15] = (byte)'M';
            // Length = 90 (big-endian) → inner end = (12 + 8) + 90 = 110 > parentEnd 100 → NestedChunkOverflow
            bytes[16] = 0x00; bytes[17] = 0x00; bytes[18] = 0x00; bytes[19] = 0x5A; // 0x5A = 90

            // Remaining bytes 20..99 = 80 bytes of padding (zeros, already zero-initialized)

            return bytes;
        }

        /// <summary>
        /// Builds the malformed-truncated IFF fixture (iter-4 HIGH-1 redesign).
        /// Total file size: 50 bytes.
        /// Outer FORM at offset 0: TypeID "FORM", length 92.
        /// (top-level FORM IS the file; no early throw on length > file)
        /// Outer payload at offset 8: sub-type "WSNP" (4 bytes, offsets 8-11).
        /// Inner leaf-chunk header at offset 12: TypeID "DATA", length 80.
        /// Inner payload would span offset 20..100 ≤ outer parentEnd 100 → nested check passes.
        /// Streaming read of inner's leaf payload from offset 20 attempts 80 bytes but file ends at 50 → Truncated.
        /// </summary>
        public static byte[] BuildTruncatedFile()
        {
            var bytes = new byte[50];

            // Outer FORM header at offset 0
            bytes[0] = (byte)'F'; bytes[1] = (byte)'O'; bytes[2] = (byte)'R'; bytes[3] = (byte)'M';
            // Length = 92 (big-endian) — LARGER than file
            bytes[4] = 0x00; bytes[5] = 0x00; bytes[6] = 0x00; bytes[7] = 0x5C; // 0x5C = 92

            // Sub-type at offset 8: "WSNP"
            bytes[8] = (byte)'W'; bytes[9] = (byte)'S'; bytes[10] = (byte)'N'; bytes[11] = (byte)'P';

            // Inner leaf-chunk header at offset 12: TypeID "DATA"
            bytes[12] = (byte)'D'; bytes[13] = (byte)'A'; bytes[14] = (byte)'T'; bytes[15] = (byte)'A';
            // Length = 80 (big-endian) → inner payload span = 20..100 ≤ parentEnd 100 → nested check passes
            bytes[16] = 0x00; bytes[17] = 0x00; bytes[18] = 0x00; bytes[19] = 0x50; // 0x50 = 80

            // Payload bytes 20..49 = 30 bytes (file ends here; 80 - 30 = 50 bytes short → Truncated)
            for (int i = 20; i < 50; i++)
            {
                bytes[i] = (byte)(i & 0xFF);
            }

            return bytes;
        }

        /// <summary>
        /// Builds the malformed-missing-padbyte IFF fixture (REVIEWS MEDIUM-11).
        /// Single FORM/TEST containing one leaf chunk with odd length 7.
        /// File ends EXACTLY at the 7-byte payload boundary with NO pad byte.
        /// Triggers IffParseException(Truncated).
        /// </summary>
        public static byte[] BuildOddLengthLeafAtEofMissingPadForFixture()
        {
            // Same as BuildOddLengthLeafAtEofMissingPad — for fixture file use.
            return BuildOddLengthLeafAtEofMissingPad();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Other negative-case fixtures
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Builds a chunk with a negative length field (0x80000001 as big-endian).</summary>
        public static byte[] BuildNegativeLength()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                WriteFourCc(bw, "FORM");
                // 0x80000001 as big-endian = large negative when read as signed int32
                bw.Write((byte)0x80); bw.Write((byte)0x00); bw.Write((byte)0x00); bw.Write((byte)0x01);
                // SubType (so we can distinguish from TypeID read failure)
                WriteFourCc(bw, "TEST");
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Builds a leaf chunk with length exceeding 64 MB cap.
        /// Uses little-endian bytes interpreted as big-endian to get a value > MaxChunkSize.
        /// 0x04000001 = 67108865 bytes = 64 MB + 1.
        /// </summary>
        public static byte[] BuildChunkLengthExceedsCap()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                WriteFourCc(bw, "TEST");
                // Big-endian 0x04000001 = 67108865 > MaxChunkSize (67108864 = 64MB)
                bw.Write((byte)0x04); bw.Write((byte)0x00); bw.Write((byte)0x00); bw.Write((byte)0x01);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Builds a chunk whose first byte is 0xFF (non-printable ASCII).
        /// Triggers IffParseException(MalformedFourCc).
        /// </summary>
        public static byte[] BuildMalformedFourCc()
        {
            return new byte[] { 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10 };
        }
    }
}
