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
using System.Linq;
using UtinniCoreDotNet.Formats.Tre;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// 07-01 Task 2: COT2000 + SearchTOC master-index reader, against the 07-00 self-contained
    /// synthetic COT2000 fixture (+ env-gated real set) plus bounds-check cases.
    /// </summary>
    public class CotMasterIndexTests
    {
        private static string Toc() => FixturePath.Resolve("tre/synthetic-cot2000-2tree.toc");

        [Fact]
        public void CotMasterIndex_DetectsCot2000Kind_OnSyntheticToc()
        {
            CotMasterIndex idx = CotMasterIndex.Open(Toc());

            Assert.Equal(MasterIndexKind.Cot2000, idx.Kind);
            Assert.Equal(2, idx.TreeFileNames.Count);
            Assert.True(idx.Entries.Count >= 2);
            foreach (CotEntry e in idx.Entries)
            {
                Assert.True(e.TreeFileIndex >= 0 && e.TreeFileIndex < idx.TreeFileNames.Count);
            }
            // The two known virtual paths resolve.
            Assert.Contains(idx.Entries, e => e.Path == "object/tangible/foo.iff");
            Assert.Contains(idx.Entries, e => e.Path == "string/en/bar.stf");
        }

        [Fact]
        public void CotMasterIndex_DetectsSearchTOCKind_AndDefersWithDocumentedError()
        {
            // SearchTOC magic: "TOC " (0x20434F54) + "0001". RESEARCH item 6 — SearchTOC is
            // RECOGNIZED and dispositioned (not silently dropped); it throws a documented error
            // until a fixture/spec is sourced (see the skipped fixture-gated test below).
            byte[] bytes = new byte[64];
            byte[] head = System.Text.Encoding.ASCII.GetBytes("TOC 0001");
            Buffer.BlockCopy(head, 0, bytes, 0, head.Length);

            Assert.Equal(MasterIndexKind.SearchTOC, CotMasterIndex.DetectKind(bytes));

            string temp = WriteTemp(bytes);
            try
            {
                var ex = Record.Exception(() => CotMasterIndex.Open(temp));
                var treEx = Assert.IsType<TreParseException>(ex);
                Assert.Equal(TreParseError.UnsupportedVersion, treEx.Kind);
                Assert.Contains("SearchTOC", treEx.Message);
            }
            finally { TryDelete(temp); }
        }

        [Fact(Skip = "TODO(searchtoc-fixture): enable when a real SearchTOC sample/spec is sourced")]
        public void CotMasterIndex_ParsesSearchTOC_WhenFixtureExists()
        {
            // Placeholder for the SearchTOC parse golden once a fixture is available.
        }

        [Fact]
        public void CotMasterIndex_TreeFileIndexOutOfRange_Throws()
        {
            byte[] bytes = File.ReadAllBytes(Toc());
            int sizeOfTreeNameBlock = BitConverter.ToInt32(bytes, 32);
            int tocStart = 36 + sizeOfTreeNameBlock;
            // Patch entry 0's treeFileIndex (u16 @ tocStart+2) to an out-of-range value.
            byte[] bad = BitConverter.GetBytes((ushort)99);
            bytes[tocStart + 2] = bad[0];
            bytes[tocStart + 3] = bad[1];

            string temp = WriteTemp(bytes);
            try
            {
                var ex = Record.Exception(() => CotMasterIndex.Open(temp));
                Assert.IsType<TreParseException>(ex);
            }
            finally { TryDelete(temp); }
        }

        [Fact]
        public void CotMasterIndex_OversizedTocBlock_ThrowsNotOom()
        {
            byte[] bytes = File.ReadAllBytes(Toc());
            // Patch sizeOfTocBlock (@16) to a value far beyond the stream.
            byte[] huge = BitConverter.GetBytes(0x40000000); // 1 GB
            Buffer.BlockCopy(huge, 0, bytes, 16, 4);

            string temp = WriteTemp(bytes);
            try
            {
                var ex = Record.Exception(() => CotMasterIndex.Open(temp));
                var treEx = Assert.IsType<TreParseException>(ex);
                Assert.True(treEx.Kind == TreParseError.Truncated || treEx.Kind == TreParseError.ChunkLengthExceedsCap);
            }
            finally { TryDelete(temp); }
        }

        [Fact]
        public void CotMasterIndex_EnvGatedReal_ReportsExpectedCounts()
        {
            if (!FixturePath.HasSampleTreDir())
            {
                return; // SUPPLEMENTARY real-set golden — skip cleanly when SWG_SAMPLE_TRE_DIR unset.
            }

            string masterIndex = Directory.GetFiles(FixturePath.SampleTreDir())
                .FirstOrDefault(f => CotMasterIndex.IsMasterIndex(f));
            if (masterIndex == null)
            {
                return; // sample dir present but no master index found — skip cleanly.
            }

            CotMasterIndex idx = CotMasterIndex.Open(masterIndex);
            Assert.Equal(213086, idx.Entries.Count);
            Assert.Equal(45, idx.TreeFileNames.Count);
        }

        // ── helpers ──

        private static string WriteTemp(byte[] bytes)
        {
            string path = Path.Combine(Path.GetTempPath(), "cot-" + Guid.NewGuid().ToString("N") + ".toc");
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
        }
    }
}
