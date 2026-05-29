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
using Xunit;
using UtinniCoreDotNet.Formats.Tre;

namespace UtinniCoreDotNet.Tests.FormatsTests.Tre
{
    /// <summary>
    /// Phase 8 Plan 07 Task 1 — verbatim raw-slice APIs added to TreFile in support of TreWriter
    /// (08-REVIEWS HIGH-1 + round-2 MEDIUM 6):
    ///
    /// <list type="bullet">
    ///   <item><see cref="TreFile.GetRecordCompressedBytes(int)"/> — raw compressed slice at
    ///         [rec.Offset, rec.Offset+rec.CompressedSize) from the source .tre verbatim.</item>
    ///   <item><see cref="TreFile.GetRecordNameBytes(int)"/> — raw name-block slice at
    ///         [rec.NameOffset, rec.NameOffset+nameByteLength) verbatim, with the byte
    ///         length matching what TreFile.Parse consumed.</item>
    /// </list>
    ///
    /// <para>Both APIs mirror <see cref="TreFile.GetRecordData(int)"/>'s lazy / stream-backed /
    /// bounds / fresh-copy / Truncated contracts. They do NOT decompress; they do NOT
    /// validate framing — TreWriter writes them verbatim into a rebuilt archive.</para>
    /// </summary>
    public class TreFileGetRecordCompressedBytesTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // GetRecordCompressedBytes — bounds, stream-backed, equality, zero-len,
        // fresh-copy, truncated
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void GetRecordCompressedBytes_NegativeIndex_ThrowsArgumentOutOfRange()
        {
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string path = WriteTemp(bytes);
            try
            {
                TreFile tre = TreFile.Open(path);
                Assert.Throws<ArgumentOutOfRangeException>(() => tre.GetRecordCompressedBytes(-1));
            }
            finally { TryDelete(path); }
        }

        [Fact]
        public void GetRecordCompressedBytes_OutOfRangeIndex_ThrowsArgumentOutOfRange()
        {
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string path = WriteTemp(bytes);
            try
            {
                TreFile tre = TreFile.Open(path);
                Assert.Throws<ArgumentOutOfRangeException>(() => tre.GetRecordCompressedBytes(tre.Records.Count));
                Assert.Throws<ArgumentOutOfRangeException>(() => tre.GetRecordCompressedBytes(99));
            }
            finally { TryDelete(path); }
        }

        [Fact]
        public void GetRecordCompressedBytes_StreamBackedInstance_ThrowsInvalidOperation()
        {
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                TreFile tre = TreFile.Open(ms);
                Assert.Throws<InvalidOperationException>(() => tre.GetRecordCompressedBytes(0));
            }
        }

        [Fact]
        public void GetRecordCompressedBytes_EqualsRawFileBytesAtOffset()
        {
            // Mixed-compressor fixture (3 records: none/deflate/none with zero-len last).
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string path = WriteTemp(bytes);
            try
            {
                TreFile tre = TreFile.Open(path);
                Assert.Equal(3, tre.Records.Count);

                for (int i = 0; i < tre.Records.Count; i++)
                {
                    var rec = tre.Records[i];
                    byte[] slice = tre.GetRecordCompressedBytes(i);
                    Assert.NotNull(slice);
                    Assert.Equal(rec.CompressedSize, slice.Length);

                    // Compare to the raw file bytes at [rec.Offset, rec.Offset+rec.CompressedSize).
                    for (int j = 0; j < rec.CompressedSize; j++)
                    {
                        Assert.Equal(bytes[rec.Offset + j], slice[j]);
                    }
                }
            }
            finally { TryDelete(path); }
        }

        [Fact]
        public void GetRecordCompressedBytes_ZeroCompressedSize_ReturnsEmptyArray()
        {
            // Record index 2 of BuildValidV0005ThreeRecord has empty payload (CompressedSize=0).
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string path = WriteTemp(bytes);
            try
            {
                TreFile tre = TreFile.Open(path);
                Assert.Equal(0, tre.Records[2].CompressedSize);

                byte[] slice = tre.GetRecordCompressedBytes(2);
                Assert.NotNull(slice);
                Assert.Equal(0, slice.Length);
            }
            finally { TryDelete(path); }
        }

        [Fact]
        public void GetRecordCompressedBytes_FreshCopy_MutationDoesNotAffectNextCall()
        {
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string path = WriteTemp(bytes);
            try
            {
                TreFile tre = TreFile.Open(path);
                byte[] first = tre.GetRecordCompressedBytes(1);
                Assert.True(first.Length > 0);

                // Mutate the first copy.
                byte original = first[0];
                first[0] = (byte)~original;

                // Second call returns a fresh copy unaffected by the mutation.
                byte[] second = tre.GetRecordCompressedBytes(1);
                Assert.NotSame(first, second);
                Assert.Equal(original, second[0]);
            }
            finally { TryDelete(path); }
        }

        [Fact]
        public void GetRecordCompressedBytes_TruncatedFile_ThrowsTruncated()
        {
            // Build a valid v0005 fixture then truncate so record[1] (compressed payload) is
            // partially missing. The bounds-check during Parse uses the full streamLength of the
            // ORIGINAL bytes; we re-write the truncated bytes to a temp path, then synthesize a
            // TreFile from the original (so Parse succeeds), and write the truncated bytes to
            // the SAME path so GetRecordCompressedBytes reads short. Trick: TreFile lazily reads
            // payloads on demand via _sourcePath, so we Open from full bytes, then truncate the
            // file in-place under it.
            byte[] full = TreFileFixtures.BuildValidV0005ThreeRecord();
            string path = WriteTemp(full);
            try
            {
                TreFile tre = TreFile.Open(path);
                var rec1 = tre.Records[1];
                Assert.True(rec1.CompressedSize > 0);

                // Truncate the file so reading record[1]'s payload returns short.
                int truncateTo = rec1.Offset + (rec1.CompressedSize / 2);
                Assert.True(truncateTo < full.Length);
                byte[] truncated = new byte[truncateTo];
                Buffer.BlockCopy(full, 0, truncated, 0, truncateTo);
                File.WriteAllBytes(path, truncated);

                var ex = Assert.Throws<TreParseException>(() => tre.GetRecordCompressedBytes(1));
                Assert.Equal(TreParseError.Truncated, ex.Kind);
            }
            finally { TryDelete(path); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // GetRecordNameBytes — round-2 MEDIUM 6 (name-block byte-layout preservation)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void GetRecordNameBytes_NegativeIndex_ThrowsArgumentOutOfRange()
        {
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string path = WriteTemp(bytes);
            try
            {
                TreFile tre = TreFile.Open(path);
                Assert.Throws<ArgumentOutOfRangeException>(() => tre.GetRecordNameBytes(-1));
            }
            finally { TryDelete(path); }
        }

        [Fact]
        public void GetRecordNameBytes_OutOfRangeIndex_ThrowsArgumentOutOfRange()
        {
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string path = WriteTemp(bytes);
            try
            {
                TreFile tre = TreFile.Open(path);
                Assert.Throws<ArgumentOutOfRangeException>(() => tre.GetRecordNameBytes(tre.Records.Count));
            }
            finally { TryDelete(path); }
        }

        [Fact]
        public void GetRecordNameBytes_StreamBackedInstance_ThrowsInvalidOperation()
        {
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                TreFile tre = TreFile.Open(ms);
                Assert.Throws<InvalidOperationException>(() => tre.GetRecordNameBytes(0));
            }
        }

        [Fact]
        public void GetRecordNameBytes_LengthMatchesParse_AndDecodesToRecName()
        {
            // The byte length the new API returns MUST match what TreFile.Parse consumed when
            // materializing rec.Name. For the v0005 fixture the name block stores null-terminated
            // ASCII strings, so the returned slice's length should be rec.Name.Length + 1
            // (including the terminator).
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string path = WriteTemp(bytes);
            try
            {
                TreFile tre = TreFile.Open(path);
                for (int i = 0; i < tre.Records.Count; i++)
                {
                    var rec = tre.Records[i];
                    byte[] slice = tre.GetRecordNameBytes(i);
                    Assert.NotNull(slice);

                    // Expect the encoded name + null terminator (the SWG fixture format).
                    int expectedLen = Encoding.ASCII.GetByteCount(rec.Name) + 1;
                    Assert.Equal(expectedLen, slice.Length);

                    // Decode (strip optional trailing null) and compare to rec.Name.
                    int strLen = slice.Length;
                    if (strLen > 0 && slice[strLen - 1] == 0) strLen--;
                    string decoded = Encoding.ASCII.GetString(slice, 0, strLen);
                    Assert.Equal(rec.Name, decoded);
                }
            }
            finally { TryDelete(path); }
        }

        [Fact]
        public void GetRecordNameBytes_EqualsRawFileNameBlockBytes()
        {
            // For the uncompressed-name-block v0005 fixture, the in-file name block lives at
            // [namesOffset, namesOffset + nameSize). The bytes returned by GetRecordNameBytes(i)
            // must equal the raw file bytes at [namesOffset + rec.NameOffset, ... + len).
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string path = WriteTemp(bytes);
            try
            {
                TreFile tre = TreFile.Open(path);

                // Re-derive namesOffset from the header — for uncompressed name blocks this is
                // (infoOffset + infoCompressedSize), per TreFile.Parse line 244.
                int infoOffset = tre.Header.InfoOffset;
                int infoCompressedSize = tre.Header.InfoCompressedSize;
                int namesOffset = infoOffset + infoCompressedSize;

                for (int i = 0; i < tre.Records.Count; i++)
                {
                    var rec = tre.Records[i];
                    byte[] slice = tre.GetRecordNameBytes(i);
                    int absStart = namesOffset + rec.NameOffset;
                    for (int j = 0; j < slice.Length; j++)
                    {
                        Assert.Equal(bytes[absStart + j], slice[j]);
                    }
                }
            }
            finally { TryDelete(path); }
        }

        [Fact]
        public void GetRecordNameBytes_FreshCopy_MutationDoesNotAffectNextCall()
        {
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string path = WriteTemp(bytes);
            try
            {
                TreFile tre = TreFile.Open(path);
                byte[] first = tre.GetRecordNameBytes(0);
                Assert.True(first.Length > 0);

                byte original = first[0];
                first[0] = (byte)~original;

                byte[] second = tre.GetRecordNameBytes(0);
                Assert.NotSame(first, second);
                Assert.Equal(original, second[0]);
            }
            finally { TryDelete(path); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers (mirror the existing TreFileTests temp-file pattern).
        // ─────────────────────────────────────────────────────────────────────

        private static string WriteTemp(byte[] bytes)
        {
            string path = Path.Combine(Path.GetTempPath(), "tre-getrec-" + Guid.NewGuid().ToString("N") + ".tre");
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best-effort */ }
        }
    }
}
