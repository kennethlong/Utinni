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
using Xunit;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Tests.FormatsTests.Iff
{
    /// <summary>
    /// Unit coverage for the IFF serializer (Task 2 of 08-01): IffWriter.
    /// Validates byte-exact untouched round-trip, length roll-up on edits, structural-op survival
    /// through write→re-parse, the SWG no-pad invariant, ancestor invalidation, defensive payload
    /// copy in the write path, and checked-long-arithmetic overflow refusal.
    ///
    /// Test naming convention: [Method]_[Scenario]_[ExpectedOutcome] (Phase 1 D-04).
    /// </summary>
    public class IffWriterTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Round-trip: byte-exact for an untouched no-pad tree (D-07)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Write_NoPadHappyPathNoMutation_RoundtripsByteExact()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);

            byte[] rewritten = IffWriter.Write(mut);

            Assert.Equal(bytes, rewritten);
        }

        [Fact]
        public void Write_NoPadHappyPathNoMutation_ReparsesToEqualTree()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);

            byte[] rewritten = IffWriter.Write(mut);
            IffDocument doc2;
            using (var ms = new MemoryStream(rewritten))
            {
                doc2 = IffReader.Read(ms);
            }

            // Top-level tree shape matches the read model that came from the same bytes.
            var rootA = (IffContainerChunk)doc.Root;
            var rootB = (IffContainerChunk)doc2.Root;
            Assert.Equal(rootA.TypeId, rootB.TypeId);
            Assert.Equal(rootA.SubTypeId, rootB.SubTypeId);
            Assert.Equal(rootA.Children.Count, rootB.Children.Count);
            Assert.Equal(rootA.LengthBytes, rootB.LengthBytes);
        }

        // ─────────────────────────────────────────────────────────────────────
        // No-pad invariant (Pitfall 1)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Write_OddLengthLeafNoMutation_OutputLengthEqualsInputLength()
        {
            byte[] bytes = IffWriterFixtures.BuildOddLengthNoPad();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);

            byte[] rewritten = IffWriter.Write(mut);

            Assert.Equal(bytes.Length, rewritten.Length);
            Assert.Equal(bytes, rewritten);
        }

        [Fact]
        public void Write_DirtyOddLengthLeaf_EmitsNoPadByte()
        {
            // Build a fresh single-leaf container with an odd-length payload (7 bytes), so the
            // writer emits a FRESH (no captured slice) and proves the no-pad rule directly.
            var root = MutableIffNode.NewContainer("FORM", "TEST");
            root.AddLeaf("TEST", new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 });
            var doc = new MutableIffDocument(root);

            byte[] written = IffWriter.Write(doc);

            // Outer FORM payload = SubType(4) + TEST_chunk(15) = 19; file total = 27.
            Assert.Equal(27, written.Length);
            // Re-parse cleanly.
            using (var ms = new MemoryStream(written))
            {
                IffDocument doc2 = IffReader.Read(ms);
                var rootB = (IffContainerChunk)doc2.Root;
                Assert.Equal("FORM", rootB.TypeId);
                Assert.Equal("TEST", rootB.SubTypeId);
                Assert.Single(rootB.Children);
                Assert.Equal("TEST", rootB.Children[0].TypeId);
                Assert.Equal(7, rootB.Children[0].LengthBytes);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Length roll-up on edited-leaf write
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Write_AfterLeafPayloadGrowth_ParentLengthRollsUpReparsesClean()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);

            // Grow the DATA leaf payload from 8 -> 16 bytes.
            var dataLeaf = mut.Root.Children[0];
            dataLeaf.SetPayload(new byte[] {
                0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10 });

            byte[] rewritten = IffWriter.Write(mut);

            // Output must be exactly 8 bytes longer than the input.
            Assert.Equal(bytes.Length + 8, rewritten.Length);

            // Re-parse cleanly (no NestedChunkOverflow / Truncated).
            using (var ms = new MemoryStream(rewritten))
            {
                IffDocument doc2 = IffReader.Read(ms);
                var rootB = (IffContainerChunk)doc2.Root;
                Assert.Equal(4, rootB.Children.Count);
                // DATA payload is now 16 bytes.
                Assert.Equal(16, ((IffLeafChunk)rootB.Children[0]).LengthBytes);
                // Outer length grew by 8.
                Assert.Equal(((IffContainerChunk)doc.Root).LengthBytes + 8, rootB.LengthBytes);
            }
        }

        [Fact]
        public void Write_AfterLeafPayloadShrink_ParentLengthRollsUpReparsesClean()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            var dataLeaf = mut.Root.Children[0]; // 8 bytes -> 4 bytes
            dataLeaf.SetPayload(new byte[] { 0xFF, 0xEE, 0xDD, 0xCC });

            byte[] rewritten = IffWriter.Write(mut);
            using (var ms = new MemoryStream(rewritten))
            {
                IffDocument doc2 = IffReader.Read(ms);
                var rootB = (IffContainerChunk)doc2.Root;
                Assert.Equal(4, ((IffLeafChunk)rootB.Children[0]).LengthBytes);
                Assert.Equal(((IffContainerChunk)doc.Root).LengthBytes - 4, rootB.LengthBytes);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Structural ops survive write → re-parse
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Write_AfterAddLeaf_StructureSurvivesReparse()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            mut.Root.AddLeaf("EXTR", new byte[] { 0xAB, 0xCD });

            byte[] rewritten = IffWriter.Write(mut);
            using (var ms = new MemoryStream(rewritten))
            {
                IffDocument doc2 = IffReader.Read(ms);
                var rootB = (IffContainerChunk)doc2.Root;
                Assert.Equal(5, rootB.Children.Count);
                Assert.Equal("EXTR", rootB.Children[4].TypeId);
                Assert.Equal(2, ((IffLeafChunk)rootB.Children[4]).LengthBytes);
            }
        }

        [Fact]
        public void Write_AfterRemoveLeaf_StructureSurvivesReparse()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            mut.Root.Remove(mut.Root.Children[1]); // remove NAME

            byte[] rewritten = IffWriter.Write(mut);
            using (var ms = new MemoryStream(rewritten))
            {
                IffDocument doc2 = IffReader.Read(ms);
                var rootB = (IffContainerChunk)doc2.Root;
                Assert.Equal(3, rootB.Children.Count);
                Assert.Equal("DATA", rootB.Children[0].TypeId);
                Assert.Equal("DESC", rootB.Children[1].TypeId);
                Assert.Equal("FORM", rootB.Children[2].TypeId);
            }
        }

        [Fact]
        public void Write_AfterRenameLeaf_TypeIdChangePersistsOnReparse()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            mut.Root.Children[0].TypeId = "RENM"; // DATA -> RENM

            byte[] rewritten = IffWriter.Write(mut);
            using (var ms = new MemoryStream(rewritten))
            {
                IffDocument doc2 = IffReader.Read(ms);
                var rootB = (IffContainerChunk)doc2.Root;
                Assert.Equal("RENM", rootB.Children[0].TypeId);
            }
        }

        [Fact]
        public void Write_AfterReorderUp_OrderPersistsOnReparse()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            // Move NAME (index 1) up to index 0.
            mut.Root.ReorderUp(mut.Root.Children[1]);

            byte[] rewritten = IffWriter.Write(mut);
            using (var ms = new MemoryStream(rewritten))
            {
                IffDocument doc2 = IffReader.Read(ms);
                var rootB = (IffContainerChunk)doc2.Root;
                Assert.Equal("NAME", rootB.Children[0].TypeId);
                Assert.Equal("DATA", rootB.Children[1].TypeId);
            }
        }

        [Fact]
        public void Write_AfterDuplicateLeaf_DuplicatePersistsOnReparse()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            int before = mut.Root.Children.Count;
            mut.Root.Duplicate(mut.Root.Children[0]); // duplicate DATA

            byte[] rewritten = IffWriter.Write(mut);
            using (var ms = new MemoryStream(rewritten))
            {
                IffDocument doc2 = IffReader.Read(ms);
                var rootB = (IffContainerChunk)doc2.Root;
                Assert.Equal(before + 1, rootB.Children.Count);
                Assert.Equal("DATA", rootB.Children[before].TypeId);
            }
        }

        [Fact]
        public void Write_AfterEditFormSubType_SubTypePersistsOnReparse()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            // Inner FORM/OBJS is Children[3]; rename its sub-type to NEWS.
            mut.Root.Children[3].SubTypeId = "NEWS";

            byte[] rewritten = IffWriter.Write(mut);
            using (var ms = new MemoryStream(rewritten))
            {
                IffDocument doc2 = IffReader.Read(ms);
                var rootB = (IffContainerChunk)doc2.Root;
                var innerForm = (IffContainerChunk)rootB.Children[3];
                Assert.Equal("NEWS", innerForm.SubTypeId);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Ancestor invalidation in the writer path
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Write_AfterDeepLeafEdit_AncestorCapturedSliceClearedReparsesClean()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            var innerForm = mut.Root.Children[3];
            var info = innerForm.Children[0]; // INFO inside FORM/OBJS

            // Reach the deepest leaf and grow its payload.
            info.SetPayload(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE });

            // Verify ancestor-invalidation contract (Cursor-unique MEDIUM, HIGH for D-07).
            Assert.False(info.HasCapturedSlice);
            Assert.False(innerForm.HasCapturedSlice);
            Assert.False(mut.Root.HasCapturedSlice);

            byte[] rewritten = IffWriter.Write(mut);

            // Re-parse must produce a sensible tree (root container length rolled up).
            using (var ms = new MemoryStream(rewritten))
            {
                IffDocument doc2 = IffReader.Read(ms);
                var rootB = (IffContainerChunk)doc2.Root;
                Assert.Equal(((IffContainerChunk)doc.Root).LengthBytes + 1, rootB.LengthBytes);
                var innerB = (IffContainerChunk)rootB.Children[3];
                Assert.Equal(5, ((IffLeafChunk)innerB.Children[0]).LengthBytes);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Defensive payload copy in the write path
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void GetPayloadCopy_MutateReturnedArrayBeforeWrite_DoesNotChangeWrittenBytes()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            var dataLeaf = mut.Root.Children[0];

            // Pull a copy and mutate it; the node must remain clean and re-emit verbatim.
            byte[] view = dataLeaf.GetPayloadCopy();
            for (int i = 0; i < view.Length; i++) view[i] = 0xFF;

            Assert.False(dataLeaf.IsDirty);
            byte[] rewritten = IffWriter.Write(mut);
            // Untouched round-trip: must be byte-identical to the input.
            Assert.Equal(bytes, rewritten);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Checked-arithmetic overflow refusal
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Write_LeafPayloadAboveMaxChunkSize_ThrowsChunkLengthExceedsCap()
        {
            // The writer caps leaf payload serialization at 64 MB (reuses IffReader.MaxChunkSize).
            // Use the internal const directly via InternalsVisibleTo (AssemblyInfo.cs line 28).
            // Build a fresh single-leaf container with the oversize payload and assert the cap
            // branch throws with the right error kind.
            int cap = IffWriter.MaxChunkSize;
            Assert.Equal(64 * 1024 * 1024, cap);

            byte[] big;
            try
            {
                big = new byte[cap + 1];
            }
            catch (OutOfMemoryException)
            {
                // Memory-constrained runner — skip rather than crash. The cap branch is also
                // partially exercised by the container roll-up overflow guard, which we keep
                // in code even when this allocation cannot happen.
                return;
            }

            // Build the leaf via NewLeaf (one defensive clone). Drop the local reference first so
            // the GC may reclaim our copy before the clone allocates.
            MutableIffNode leaf;
            try
            {
                leaf = MutableIffNode.NewLeaf("TEST", big);
            }
            catch (OutOfMemoryException)
            {
                return;
            }
            big = null;

            // Wrap the leaf in a fresh container via the internal AddChildInternal helper
            // (visible to tests via InternalsVisibleTo). This avoids AddLeaf's defensive copy
            // path, keeping the test's transient footprint at ~2x cap rather than 3x.
            var root = MutableIffNode.NewContainer("FORM", "ROOT");
            root.AddChildInternal(leaf);
            var doc = new MutableIffDocument(root);

            var ex = Assert.Throws<IffParseException>(() => IffWriter.Write(doc));
            Assert.Equal(IffParseError.ChunkLengthExceedsCap, ex.Kind);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Null / argument guards
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Write_NullDoc_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => IffWriter.Write((MutableIffDocument)null));
        }

        [Fact]
        public void Write_NullStream_Throws()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            Assert.Throws<ArgumentNullException>(() => IffWriter.Write(mut, null));
        }
    }
}
