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

using System.Text;
using UtinniCoreDotNet.Formats.Tre;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// 07-01 Task 2: the shared TreArchiveIndex browse facade + the single pinned
    /// TrePayloadResolver.TryResolve contract, exercised over the SELF-CONTAINED synthetic
    /// COT2000 fixture + its companion archives (no env var) and a v6000 enumerate-only archive.
    /// </summary>
    public class TreArchiveIndexTests
    {
        private const string Cot2000PayloadA = "COT2000 companion payload A -> object/tangible/foo.iff";

        [Fact]
        public void TreArchiveIndex_OverSyntheticCot2000_ExposesFlatPaths_AndResolutionCompleteDescriptors()
        {
            TreArchiveIndex index = TreArchiveIndex.Build(FixturePath.Resolve("tre/synthetic-cot2000-2tree.toc"));

            Assert.Equal(2, index.AllPaths.Count);
            Assert.Contains("object/tangible/foo.iff", index.AllPaths);

            Assert.True(index.TryGetDescriptor("object/tangible/foo.iff", out TreEntryDescriptor d));
            Assert.Equal(0, d.TreeFileIndex);
            Assert.Equal("cot2000/tree0.tre", d.TreeFileName);
            Assert.False(string.IsNullOrEmpty(d.ResolvedArchivePath));
            Assert.Contains("tree0.tre", d.ResolvedArchivePath);
            Assert.True(d.ArchiveLocalOffset > 0);
            Assert.True(d.CompressedLength > 0);
            Assert.False(d.EnumerateOnly);
        }

        [Fact]
        public void TrePayloadResolver_ReadableCot2000Entry_ReturnsTrueWithBytes_NoEnvVar()
        {
            TreArchiveIndex index = TreArchiveIndex.Build(FixturePath.Resolve("tre/synthetic-cot2000-2tree.toc"));
            Assert.True(index.TryGetDescriptor("object/tangible/foo.iff", out TreEntryDescriptor d));

            byte[] payload;
            bool ok = TrePayloadResolver.TryResolve(d, out payload);

            Assert.True(ok);
            Assert.NotNull(payload);
            Assert.Equal(Cot2000PayloadA, Encoding.ASCII.GetString(payload));
        }

        [Fact]
        public void TrePayloadResolver_EnumerateOnlyV6000Entry_ReturnsFalseAndNull()
        {
            TreArchiveIndex index = TreArchiveIndex.Build(FixturePath.Resolve("tre/synthetic-v6000-2record.tre"));
            Assert.True(index.TryGetDescriptor("alpha.iff", out TreEntryDescriptor d));
            Assert.True(d.EnumerateOnly); // v6000 payloads are encrypted (D-07)

            byte[] payload;
            bool ok = TrePayloadResolver.TryResolve(d, out payload);

            Assert.False(ok);
            Assert.Null(payload);
        }
    }
}
