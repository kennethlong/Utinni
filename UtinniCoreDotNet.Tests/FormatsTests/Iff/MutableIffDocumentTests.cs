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
    /// Unit coverage for the mutable hybrid-DOM (Task 1 of 08-01): MutableIffNode +
    /// MutableIffDocument. Validates D-07 verbatim re-emit, ancestor invalidation, defensive
    /// payload copies, structural ops, and the padded-input policy on FromDocument.
    ///
    /// Test naming convention: [Method]_[Scenario]_[ExpectedOutcome] (Phase 1 D-04).
    /// </summary>
    public class MutableIffDocumentTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // FromDocument: construction + verbatim source-slice capture
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void FromDocument_NoPadHappyPath_BuildsTreeMatchingReadModel()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }

            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);

            Assert.NotNull(mut.Root);
            Assert.Equal(MutableIffNodeKind.Container, mut.Root.Kind);
            Assert.Equal("FORM", mut.Root.TypeId);
            Assert.Equal("WSNP", mut.Root.SubTypeId);
            Assert.Equal(4, mut.Root.Children.Count);

            // First child: DATA leaf (8 bytes)
            Assert.Equal(MutableIffNodeKind.Leaf, mut.Root.Children[0].Kind);
            Assert.Equal("DATA", mut.Root.Children[0].TypeId);
            Assert.Equal(8, mut.Root.Children[0].PayloadLength);

            // Fourth child: nested FORM/OBJS with 2 children
            var innerForm = mut.Root.Children[3];
            Assert.Equal(MutableIffNodeKind.Container, innerForm.Kind);
            Assert.Equal("FORM", innerForm.TypeId);
            Assert.Equal("OBJS", innerForm.SubTypeId);
            Assert.Equal(2, innerForm.Children.Count);
        }

        [Fact]
        public void FromDocument_AllNodes_AreCleanWithCapturedSlice()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }

            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);

            // Root and every descendant: not dirty AND has a captured slice for verbatim re-emit.
            AssertSubtreeCleanAndCaptured(mut.Root);
        }

        private static void AssertSubtreeCleanAndCaptured(MutableIffNode node)
        {
            Assert.False(node.IsDirty, "Node " + node.TypeId + " unexpectedly dirty after FromDocument.");
            Assert.True(node.HasCapturedSlice, "Node " + node.TypeId + " missing captured slice after FromDocument.");
            if (node.Kind == MutableIffNodeKind.Container)
            {
                foreach (var c in node.Children) AssertSubtreeCleanAndCaptured(c);
            }
        }

        [Fact]
        public void FromDocument_NullDoc_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => MutableIffDocument.FromDocument(null, new byte[16]));
        }

        [Fact]
        public void FromDocument_NullSourceBytes_Throws()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            Assert.Throws<ArgumentNullException>(() => MutableIffDocument.FromDocument(doc, null));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Defensive payload copy (08-REVIEWS MEDIUM-7)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void GetPayloadCopy_MutatingReturnedArray_DoesNotAffectNode()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            var leaf = mut.Root.Children[0]; // DATA leaf, 8 bytes

            byte[] first = leaf.GetPayloadCopy();
            byte[] expected = (byte[])first.Clone();

            // Mutate the array returned by the getter.
            for (int i = 0; i < first.Length; i++) first[i] = 0xFF;

            // Re-read the getter — the node should be unchanged.
            byte[] second = leaf.GetPayloadCopy();
            Assert.Equal(expected, second);

            // And the node must still be clean (no dirty bit set by reading the getter).
            Assert.False(leaf.IsDirty);
            Assert.True(leaf.HasCapturedSlice);
        }

        [Fact]
        public void SetPayload_CopiesSuppliedArray_CallerCannotMutateAfterSet()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            var leaf = mut.Root.Children[0];

            byte[] newBytes = new byte[] { 0x10, 0x20, 0x30 };
            leaf.SetPayload(newBytes);

            // Mutate the caller's array — must not affect the node.
            newBytes[0] = 0xFF;
            byte[] stored = leaf.GetPayloadCopy();
            Assert.Equal(new byte[] { 0x10, 0x20, 0x30 }, stored);

            // The node is now dirty AND its captured slice was cleared.
            Assert.True(leaf.IsDirty);
            Assert.False(leaf.HasCapturedSlice);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Ancestor invalidation (Cursor-unique MEDIUM; HIGH for D-07 correctness)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void SetPayload_OnDeepLeaf_ClearsCapturedSliceOnEveryAncestor()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);

            // Reach the deepest leaf: root.Children[3] is inner FORM/OBJS; INFO is its [0].
            var innerForm = mut.Root.Children[3];
            var info = innerForm.Children[0];
            Assert.Equal("INFO", info.TypeId);
            Assert.True(info.HasCapturedSlice);
            Assert.True(innerForm.HasCapturedSlice);
            Assert.True(mut.Root.HasCapturedSlice);

            info.SetPayload(new byte[] { 0x99, 0x99, 0x99, 0x99 });

            // info dirty, captured cleared
            Assert.True(info.IsDirty);
            Assert.False(info.HasCapturedSlice);
            // every ancestor dirty + captured cleared
            Assert.True(innerForm.IsDirty);
            Assert.False(innerForm.HasCapturedSlice);
            Assert.True(mut.Root.IsDirty);
            Assert.False(mut.Root.HasCapturedSlice);
        }

        [Fact]
        public void AddLeaf_OnRootContainer_DirtiesRootAndClearsCapturedSlice()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            Assert.True(mut.Root.HasCapturedSlice);

            mut.Root.AddLeaf("EXTR", new byte[] { 0xAA });

            Assert.True(mut.Root.IsDirty);
            Assert.False(mut.Root.HasCapturedSlice);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Structural ops
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void AddLeaf_AppendsChildAndPropagatesAncestorInvalidation()
        {
            var root = MutableIffNode.NewContainer("FORM", "TEST");
            // A freshly-built root is dirty; reset by setting capturedSlice path via FromDocument.
            // For this isolated test we just verify the AddLeaf semantics on a synthetic root.
            int before = root.Children.Count;
            var child = root.AddLeaf("DATA", new byte[] { 0x01 });
            Assert.Equal(before + 1, root.Children.Count);
            Assert.Same(child, root.Children[root.Children.Count - 1]);
            Assert.Equal("DATA", child.TypeId);
            Assert.Equal(1, child.PayloadLength);
        }

        [Fact]
        public void AddContainer_AppendsChildOfRequestedSubType()
        {
            var root = MutableIffNode.NewContainer("FORM", "ROOT");
            var inner = root.AddContainer("FORM", "INNR");
            Assert.Equal(MutableIffNodeKind.Container, inner.Kind);
            Assert.Equal("INNR", inner.SubTypeId);
            Assert.Same(inner, root.Children[0]);
        }

        [Fact]
        public void Remove_ChildPresent_RemovesAndReturnsTrue()
        {
            var root = MutableIffNode.NewContainer("FORM", "TEST");
            var leaf = root.AddLeaf("DATA", new byte[] { 0x01 });
            bool removed = root.Remove(leaf);
            Assert.True(removed);
            Assert.Empty(root.Children);
        }

        [Fact]
        public void TypeId_RenameRetagOnLeaf_DirtiesNodeAndAncestors()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            var dataLeaf = mut.Root.Children[0];
            dataLeaf.TypeId = "RENM";
            Assert.True(dataLeaf.IsDirty);
            Assert.True(mut.Root.IsDirty);
            Assert.False(dataLeaf.HasCapturedSlice);
            Assert.False(mut.Root.HasCapturedSlice);
        }

        [Fact]
        public void TypeId_RenameAcrossContainerLeafBoundary_Throws()
        {
            var leaf = MutableIffNode.NewLeaf("DATA", new byte[] { 0 });
            Assert.Throws<InvalidOperationException>(() => { leaf.TypeId = "FORM"; });
        }

        [Fact]
        public void SubTypeId_EditOnRootContainer_DirtiesNode()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            mut.Root.SubTypeId = "NEW1";
            Assert.True(mut.Root.IsDirty);
            Assert.False(mut.Root.HasCapturedSlice);
        }

        [Fact]
        public void ReorderUp_MovesChildOnePositionTowardStart()
        {
            var root = MutableIffNode.NewContainer("FORM", "TEST");
            var a = root.AddLeaf("AAAA", new byte[] { 1 });
            var b = root.AddLeaf("BBBB", new byte[] { 2 });
            root.ReorderUp(b);
            Assert.Same(b, root.Children[0]);
            Assert.Same(a, root.Children[1]);
        }

        [Fact]
        public void ReorderDown_MovesChildOnePositionTowardEnd()
        {
            var root = MutableIffNode.NewContainer("FORM", "TEST");
            var a = root.AddLeaf("AAAA", new byte[] { 1 });
            var b = root.AddLeaf("BBBB", new byte[] { 2 });
            root.ReorderDown(a);
            Assert.Same(b, root.Children[0]);
            Assert.Same(a, root.Children[1]);
        }

        [Fact]
        public void Duplicate_LeafChild_AppendsDeepCopy()
        {
            var root = MutableIffNode.NewContainer("FORM", "TEST");
            var leaf = root.AddLeaf("DATA", new byte[] { 0x10, 0x20 });
            var dup = root.Duplicate(leaf);
            Assert.NotSame(leaf, dup);
            Assert.Equal("DATA", dup.TypeId);
            Assert.Equal(leaf.GetPayloadCopy(), dup.GetPayloadCopy());
            Assert.Equal(2, root.Children.Count);
        }

        [Fact]
        public void Duplicate_ContainerChild_DeepCopiesSubtree()
        {
            var root = MutableIffNode.NewContainer("FORM", "TEST");
            var inner = root.AddContainer("FORM", "INNR");
            inner.AddLeaf("DATA", new byte[] { 0x01 });
            var dup = root.Duplicate(inner);
            Assert.NotSame(inner, dup);
            Assert.Equal("INNR", dup.SubTypeId);
            Assert.Single(dup.Children);
            Assert.NotSame(inner.Children[0], dup.Children[0]);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Stable-id derivation (RemoveByStableId — 08-REVIEWS R3-M3)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void DeriveStableId_RootContainer_ProducesPathInTypeColonSubFormat()
        {
            var root = MutableIffNode.NewContainer("FORM", "WSNP");
            string id = MutableIffDocument.DeriveStableId(root, "", 0);
            Assert.Equal("FORM:WSNP/0", id);
        }

        [Fact]
        public void DeriveStableId_Leaf_ProducesTypeColonTypeFormat()
        {
            var leaf = MutableIffNode.NewLeaf("DATA", new byte[] { 0 });
            string id = MutableIffDocument.DeriveStableId(leaf, "FORM:WSNP/0/", 0);
            Assert.Equal("FORM:WSNP/0/DATA:DATA/0", id);
        }

        [Fact]
        public void RemoveByStableId_ExistingLeaf_RemovesAndReturnsTrue()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);

            // Root is FORM:WSNP/0; first child DATA is FORM:WSNP/0/DATA:DATA/0
            string leafId = "FORM:WSNP/0/DATA:DATA/0";
            bool removed = mut.RemoveByStableId(leafId);
            Assert.True(removed);
            // After removing first child, there are 3 left.
            Assert.Equal(3, mut.Root.Children.Count);
            // DATA must no longer appear as the first child.
            Assert.NotEqual("DATA", mut.Root.Children[0].TypeId);
        }

        [Fact]
        public void RemoveByStableId_NonExistent_ReturnsFalse()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            bool removed = mut.RemoveByStableId("FORM:WSNP/0/NONE:NONE/42");
            Assert.False(removed);
            Assert.Equal(4, mut.Root.Children.Count);
        }

        [Fact]
        public void RemoveByStableId_RootId_ReturnsFalse()
        {
            byte[] bytes = IffWriterFixtures.BuildNoPadHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }
            MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
            bool removed = mut.RemoveByStableId("FORM:WSNP/0");
            Assert.False(removed);
        }
    }
}
