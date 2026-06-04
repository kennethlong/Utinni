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
using Utinni.Cli.Commands.Subprocess;
using Utinni.Cli.Tests.Infrastructure;
using UtinniCoreDotNet.Formats.Tre;
using Xunit;

namespace Utinni.Cli.Tests.Subprocess
{
    /// <summary>
    /// Plan 13-02 Task 2: validates the .rsp RECIPE shape (order preserved, @u uncompressed marker,
    /// disk-first direction, no pre-sort). The actual build-tre byte-compare against the corpus is
    /// Plan 13-04's job — this proves the synthesizer reads the Phase-7 reader correctly.
    /// </summary>
    public sealed class RspSynthesizerTests : IDisposable
    {
        private readonly string _work;

        public RspSynthesizerTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "rspsynth_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_work);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_work)) Directory.Delete(_work, recursive: true); }
            catch { /* best-effort temp cleanup */ }
        }

        private string[] SynthesizeLines(string trePath, string extractDir)
        {
            TreFile tre = TreFile.Open(trePath); // path-backed; lazy reads, not IDisposable
            string rsp = RspSynthesizer.Synthesize(tre, extractDir);
            return rsp.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        [Fact]
        public void Synthesize_PreservesRecordOrder_NoPreSort()
        {
            // synthetic-5000: records in order [texture/alpha.dds, appearance/beta.msh] — NOT
            // alphabetical (a pre-sort would emit appearance first since 'a' < 't').
            string tre = Path.Combine(_work, "five.tre");
            TreFixtureBuilder.WriteSynthetic5000(tre);

            string[] lines = SynthesizeLines(tre, Path.Combine(_work, "extract5000"));

            Assert.Equal(2, lines.Length);
            // Line order == record order (no pre-sort): texture/alpha.dds first.
            // Tree-first format: the tree path is the FIRST token on each line.
            Assert.StartsWith("texture/alpha.dds ", lines[0]);
            Assert.StartsWith("appearance/beta.msh ", lines[1]);
        }

        [Fact]
        public void Synthesize_UncompressedRecord_EmitsAtUMarker_TreeFirst()
        {
            string tre = Path.Combine(_work, "five.tre");
            TreFixtureBuilder.WriteSynthetic5000(tre); // both records compressor=0 → "none"
            string extract = Path.Combine(_work, "extractU");

            string[] lines = SynthesizeLines(tre, extract);

            foreach (string line in lines)
            {
                // Tree-first builder format: "<treePath> @u <diskPath>".
                string[] parts = line.Split(new[] { ' ' }, 3);
                Assert.Equal(3, parts.Length);
                string treePath = parts[0];
                string marker = parts[1];
                string diskPath = parts[2];

                Assert.Equal("@u", marker); // uncompressed → @u
                // Tree-first: the FIRST token is the logical name (contains a forward-slash).
                Assert.Contains("/", treePath);
                Assert.False(Path.IsPathRooted(treePath));
                // The disk path (last token) is the absolute on-disk extract path.
                Assert.True(Path.IsPathRooted(diskPath));
                Assert.StartsWith(Path.GetFullPath(extract), diskPath, StringComparison.OrdinalIgnoreCase);
                Assert.True(File.Exists(diskPath), "record payload was extracted to disk: " + diskPath);
            }
        }

        [Fact]
        public void Synthesize_DeflateRecord_EmitsBareAtMarker()
        {
            // COT2000 companion tree0.tre carries a single raw-deflate record (compressor=1 → "deflate").
            string toc = Path.Combine(_work, "x.toc");
            string companions = Path.Combine(_work, "cot");
            TreFixtureBuilder.WriteCot2000TwoTree(toc, companions);

            string deflateTre = Path.Combine(companions, "tree0.tre");
            string noneTre = Path.Combine(companions, "tree1.tre");

            string[] deflateLines = SynthesizeLines(deflateTre, Path.Combine(_work, "exDeflate"));
            Assert.Single(deflateLines);
            string marker = deflateLines[0].Split(new[] { ' ' }, 3)[1];
            Assert.Equal("@", marker); // deflate → bare @ (NOT @u)

            string[] noneLines = SynthesizeLines(noneTre, Path.Combine(_work, "exNone"));
            Assert.Single(noneLines);
            Assert.Equal("@u", noneLines[0].Split(new[] { ' ' }, 3)[1]); // none → @u
        }

        [Fact]
        public void Synthesize_ExtractedBytes_MatchRecordPayload()
        {
            string tre = Path.Combine(_work, "five.tre");
            TreFixtureBuilder.WriteSynthetic5000(tre);
            string extract = Path.Combine(_work, "extractBytes");

            TreFile treFile = TreFile.Open(tre);
            RspSynthesizer.Synthesize(treFile, extract);
            for (int i = 0; i < treFile.Records.Count; i++)
            {
                string diskPath = Path.Combine(Path.GetFullPath(extract),
                    treFile.Records[i].Name.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(diskPath));
                Assert.Equal(treFile.GetRecordData(i), File.ReadAllBytes(diskPath));
            }
        }
    }
}
