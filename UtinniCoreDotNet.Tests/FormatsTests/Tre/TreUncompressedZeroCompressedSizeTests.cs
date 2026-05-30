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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UtinniCoreDotNet.Formats.Tre;
using Xunit;

namespace UtinniCoreDotNet.Tests.FormatsTests.Tre
{
    /// <summary>
    /// Regression for the real <c>appearance/abyssin_m.sat</c> bug (in <c>patch_11_00.tre</c>): an
    /// UNCOMPRESSED record whose <c>dataCompressedSize == 0</c> (the patch-archive "not compressed"
    /// marker) was read as an EMPTY payload because both read paths keyed off CompressedSize. The fix:
    /// the on-disk length is <c>UncompressedSize</c> for an uncompressed record. Without it, opening such a
    /// record (e.g. in the IFF Editor) fails with "unexpected end of stream at position 0".
    /// </summary>
    public class TreUncompressedZeroCompressedSizeTests
    {
        private static readonly byte[] Payload =
            Encoding.ASCII.GetBytes("FORM....SMAT this is a 358-style uncompressed payload with content");

        private static string WriteTempTre(out string name)
        {
            name = "appearance/abyssin_m.sat";
            byte[] tre = TreFileFixtures.BuildUncompressedRecordWithZeroCompressedSize(name, Payload);
            string path = Path.Combine(Path.GetTempPath(), "utinni_zerocomp_" + Guid.NewGuid().ToString("N") + ".tre");
            File.WriteAllBytes(path, tre);
            return path;
        }

        [Fact]
        public void GetRecordData_UncompressedRecordWithZeroCompressedSize_ReturnsFullPayload()
        {
            string path = WriteTempTre(out string name);
            try
            {
                TreFile tf = TreFile.Open(path);
                Assert.Single(tf.Records);
                TreRecord rec = tf.Records[0];
                Assert.Equal(name, rec.Name);
                Assert.Equal(Payload.Length, rec.UncompressedSize);
                Assert.Equal(0, rec.CompressedSize); // the patch convention

                byte[] data = tf.GetRecordData(0);
                Assert.Equal(Payload, data); // NOT empty — reads UncompressedSize bytes
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void TrePayloadResolver_UncompressedRecordWithZeroCompressedSize_ResolvesFullPayload()
        {
            string path = WriteTempTre(out string name);
            try
            {
                TreFile tf = TreFile.Open(path);
                TreRecord rec = tf.Records[0];

                // A loose-archive descriptor (TreeFileIndex < 0 skips the master-index containment check),
                // mirroring exactly what the TRE Browser hand-off builds from this record.
                var d = new TreEntryDescriptor
                {
                    Path = name,
                    EnumerateOnly = false,
                    Compressor = rec.Compressor,          // 0 (none)
                    ArchiveLocalOffset = rec.Offset,
                    Length = rec.UncompressedSize,        // 66
                    CompressedLength = rec.CompressedSize, // 0 (the bug trigger)
                    TreeFileIndex = -1,
                    ResolvedArchivePath = path
                };

                bool ok = TrePayloadResolver.TryResolve(d, out byte[] payload);
                Assert.True(ok);
                Assert.Equal(Payload, payload); // NOT empty
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void GetRecordCompressedBytes_UncompressedRecordWithZeroCompressedSize_ReturnsFullOnDiskSlice()
        {
            string path = WriteTempTre(out string _);
            try
            {
                TreFile tf = TreFile.Open(path);
                // The raw-slice copy path (used by TreWriter repack) must return the real on-disk bytes,
                // NOT empty — otherwise a repack drops the record's content.
                byte[] slice = tf.GetRecordCompressedBytes(0);
                Assert.Equal(Payload, slice);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Repack_PreservesUntouchedZeroCompressedSizeRecord_ByteForByte()
        {
            byte[] payload0 = Encoding.ASCII.GetBytes("FORM....SMAT untouched zero-compressed-size patch record");
            byte[] payload1 = Encoding.ASCII.GetBytes("RECORD ONE original content (will be edited)");
            byte[] tre = TreFileFixtures.BuildTwoRecord_FirstUncompressedZeroCompressedSize(
                "appearance/abyssin_m.sat", payload0, "datatable/foo/bar.iff", payload1);

            string src = Path.Combine(Path.GetTempPath(), "utinni_repacksrc_" + Guid.NewGuid().ToString("N") + ".tre");
            string dst = Path.Combine(Path.GetTempPath(), "utinni_repackdst_" + Guid.NewGuid().ToString("N") + ".tre");
            File.WriteAllBytes(src, tre);
            try
            {
                byte[] newPayload1 = Encoding.ASCII.GetBytes("RECORD ONE EDITED content");
                TreFile original = TreFile.Open(src);
                byte[] rebuilt = TreWriter.Repack(original, new Dictionary<int, byte[]> { { 1, newPayload1 } });
                File.WriteAllBytes(dst, rebuilt);

                TreFile reopened = TreFile.Open(dst);
                Assert.Equal(2, reopened.Records.Count);
                // The UNTOUCHED zero-compressed-size record survives byte-for-byte (NOT dropped to empty).
                Assert.Equal(payload0, reopened.GetRecordData(0));
                // The edited record carries the new content.
                Assert.Equal(newPayload1, reopened.GetRecordData(1));
            }
            finally
            {
                File.Delete(src);
                File.Delete(dst);
            }
        }

        [Fact]
        public void GetRecordData_GenuinelyEmptyUncompressedRecord_StillReturnsEmpty()
        {
            // Sanity: a record with UncompressedSize=0 AND CompressedSize=0 is genuinely empty.
            string name = "appearance/empty.sat";
            byte[] tre = TreFileFixtures.BuildUncompressedRecordWithZeroCompressedSize(name, new byte[0]);
            string path = Path.Combine(Path.GetTempPath(), "utinni_empty_" + Guid.NewGuid().ToString("N") + ".tre");
            File.WriteAllBytes(path, tre);
            try
            {
                TreFile tf = TreFile.Open(path);
                Assert.Empty(tf.GetRecordData(0));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
