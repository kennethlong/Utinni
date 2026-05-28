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

using System.IO;
using System.Text;

namespace UtinniCoreDotNet.Tests.FormatsTests.Iff
{
    /// <summary>
    /// Byte-sequence builders for IffWriter / MutableIffDocument round-trip tests.
    /// These build inputs in the SWG no-pad style (no EA-IFF-85 0x00 pad byte after odd-length
    /// leaves) so write→re-parse round-trips can be asserted byte-exact.
    /// </summary>
    public static class IffWriterFixtures
    {
        // BE u32 helper (writer side; the reader side has its own copy in IffReaderFixtures).
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

        /// <summary>
        /// SWG no-pad nested fixture, byte-shape:
        /// FORM/WSNP (root) containing
        ///   DATA leaf (8 bytes, even)
        ///   NAME leaf (4 bytes, even)
        ///   DESC leaf (13 bytes, ODD — but NO pad byte per SWG)
        ///   FORM/OBJS (nested container) containing
        ///     INFO leaf (4 bytes, even)
        ///     BODY leaf (6 bytes, even)
        /// </summary>
        public static byte[] BuildNoPadHappyPath()
        {
            // Inner FORM/OBJS payload = SubType(4) + INFO_chunk(12) + BODY_chunk(14) = 30
            // Inner FORM/OBJS chunk = TypeID(4) + Len(4) + 30 = 38
            //
            // Outer FORM/WSNP payload:
            //   SubType(4) = 4
            //   DATA leaf chunk: 4 + 4 + 8 = 16
            //   NAME leaf chunk: 4 + 4 + 4 = 12
            //   DESC leaf chunk: 4 + 4 + 13 = 21 (odd payload, NO pad — SWG style)
            //   inner FORM chunk: 38
            //   total payload = 4 + 16 + 12 + 21 + 38 = 91
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                WriteFourCc(bw, "FORM");
                WriteInt32Be(bw, 91);
                WriteFourCc(bw, "WSNP");

                WriteFourCc(bw, "DATA");
                WriteInt32Be(bw, 8);
                bw.Write(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 });

                WriteFourCc(bw, "NAME");
                WriteInt32Be(bw, 4);
                bw.Write(Encoding.ASCII.GetBytes("hi\0\0"));

                WriteFourCc(bw, "DESC");
                WriteInt32Be(bw, 13);
                bw.Write(Encoding.ASCII.GetBytes("Hello, World!"));
                // NO pad — SWG no-pad

                WriteFourCc(bw, "FORM");
                WriteInt32Be(bw, 30);
                WriteFourCc(bw, "OBJS");

                WriteFourCc(bw, "INFO");
                WriteInt32Be(bw, 4);
                bw.Write(new byte[] { 0xA1, 0xA2, 0xA3, 0xA4 });

                WriteFourCc(bw, "BODY");
                WriteInt32Be(bw, 6);
                bw.Write(new byte[] { 0xB1, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6 });

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Single FORM/TEST containing one odd-length-7 leaf, SWG no-pad style.
        /// Used for the no-pad regression test (output length == input length).
        /// </summary>
        public static byte[] BuildOddLengthNoPad()
        {
            // FORM/TEST payload = SubType(4) + TEST_chunk(15) = 19  (15 = header 8 + payload 7)
            // File total = TypeID(4) + Len(4) + 19 = 27
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                WriteFourCc(bw, "FORM");
                WriteInt32Be(bw, 19);
                WriteFourCc(bw, "TEST");

                WriteFourCc(bw, "TEST");
                WriteInt32Be(bw, 7);
                bw.Write(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 });
                // NO pad

                return ms.ToArray();
            }
        }
    }
}
