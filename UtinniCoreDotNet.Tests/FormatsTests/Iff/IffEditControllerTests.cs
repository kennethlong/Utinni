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
using System.Linq;
using Xunit;
using UtinniCoreDotNet.Editing;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Tests.FormatsTests.Iff
{
    /// <summary>
    /// Unit tests for the editor-local undo/redo controller used by FormIffEditor (08-04).
    /// <para>Contract under test (D-08 / 08-04-PLAN):</para>
    /// <list type="bullet">
    ///   <item>Apply → Undo restores the prior MutableIffDocument byte-for-byte (verified via re-serialize).</item>
    ///   <item>Apply → Undo → Redo restores the edited state byte-for-byte.</item>
    ///   <item>Each D-03 structural op (edit-leaf-payload, replace-leaf-from-bytes, add-leaf,
    ///         add-container, remove, rename-retag, edit-FORM-sub-type, duplicate, move-up, move-down)
    ///         is independently undoable.</item>
    ///   <item>Dirty propagates from the edited node to the root container.</item>
    ///   <item>Baseline-clean dirty semantics: a freshly-loaded document is Dirty=false; Apply marks
    ///         Dirty=true; Undo back to baseline marks Dirty=false again (08-REVIEWS Codex unique).</item>
    ///   <item>CanUndo/CanRedo flip correctly; Apply after Undo truncates the redo tail.</item>
    ///   <item>Controller references neither the scene UndoRedoManager nor System.Windows.Forms
    ///         (pure-managed). Source-level grep gate enforced separately in Task 1 acceptance.</item>
    /// </list>
    /// </summary>
    public class IffEditControllerTests
    {
        // Helper: load a fresh controller over a no-pad version of BuildHappyPath.
        //
        // The round-trip equality tests below compare `Serialize(doc) before` vs
        // `Serialize(doc) after Apply+Undo`. For the comparison to be byte-stable, the document
        // must serialize identically via the verbatim-captured-slice path (used while clean) AND
        // via the fresh-serialize path (used after ancestor invalidation). That requires NO pad
        // bytes in the source (the writer never emits pads per SWG no-pad quirk). BuildHappyPath
        // contains a DESC leaf with odd length 13 + a pad byte; the verbatim path emits 92 bytes,
        // the fresh path emits 91. We use a no-pad variant here so before/after compare cleanly.
        private static IffEditController NewControllerOverHappyPath(out byte[] baselineBytes)
        {
            baselineBytes = BuildHappyPathNoPad();
            IffDocument doc;
            using (var ms = new MemoryStream(baselineBytes))
            {
                doc = IffReader.Read(ms);
            }
            var mut = MutableIffDocument.FromDocument(doc, baselineBytes);
            return new IffEditController(mut);
        }

        // Builds the same logical fixture as IffReaderFixtures.BuildHappyPath but with the DESC
        // leaf shortened to even length 12 so no pad byte is present. Test-local because the
        // production fixtures exercise the pad-detection path elsewhere; here we want round-trip
        // stability across the controller's clean/dirty path-switching writer.
        private static byte[] BuildHappyPathNoPad()
        {
            using (var ms = new MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
            {
                // Outer FORM/WSNP payload =
                //   SubType(4) + DATA(16) + NAME(12) + DESC(20=8+12) + FORM/OBJS(38) = 90
                WriteFourCc(bw, "FORM");
                WriteInt32Be(bw, 90);
                WriteFourCc(bw, "WSNP");

                WriteFourCc(bw, "DATA");
                WriteInt32Be(bw, 8);
                bw.Write(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 });

                WriteFourCc(bw, "NAME");
                WriteInt32Be(bw, 4);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("hi\0\0"));

                WriteFourCc(bw, "DESC");
                WriteInt32Be(bw, 12);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("Hello, World"));

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

        private static void WriteInt32Be(System.IO.BinaryWriter bw, int v)
        {
            bw.Write((byte)((v >> 24) & 0xFF));
            bw.Write((byte)((v >> 16) & 0xFF));
            bw.Write((byte)((v >> 8) & 0xFF));
            bw.Write((byte)(v & 0xFF));
        }

        private static void WriteFourCc(System.IO.BinaryWriter bw, string s)
        {
            bw.Write(System.Text.Encoding.ASCII.GetBytes(s), 0, 4);
        }

        private static byte[] Serialize(MutableIffDocument doc)
        {
            return IffWriter.Write(doc);
        }

        // ─────────────────────────────────────────────────────────────────────
        // CanUndo / CanRedo + baseline state
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void NewController_FromCleanDocument_CanUndoCanRedoFalseAndDocClean()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);

            Assert.False(ctrl.CanUndo);
            Assert.False(ctrl.CanRedo);
            Assert.False(ctrl.IsDirty);
        }

        [Fact]
        public void Apply_OneEdit_CanUndoTrueCanRedoFalseIsDirtyTrue()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            var leaf = FindLeaf(ctrl.Document, "DATA");

            ctrl.Apply(IffEditCommands.EditLeafPayload(leaf, new byte[] { 0xAA, 0xBB }));

            Assert.True(ctrl.CanUndo);
            Assert.False(ctrl.CanRedo);
            Assert.True(ctrl.IsDirty);
        }

        [Fact]
        public void Undo_AfterApply_CanUndoFalseCanRedoTrueIsDirtyFalse()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            var leaf = FindLeaf(ctrl.Document, "DATA");

            ctrl.Apply(IffEditCommands.EditLeafPayload(leaf, new byte[] { 0xAA, 0xBB }));
            ctrl.Undo();

            Assert.False(ctrl.CanUndo);
            Assert.True(ctrl.CanRedo);
            // Baseline-clean dirty semantics — undo back to baseline clears dirty.
            Assert.False(ctrl.IsDirty);
        }

        [Fact]
        public void Redo_AfterUndo_RestoresEditedStateAndCanUndoFlips()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            var leaf = FindLeaf(ctrl.Document, "DATA");

            ctrl.Apply(IffEditCommands.EditLeafPayload(leaf, new byte[] { 0xAA, 0xBB }));
            byte[] afterEdit = Serialize(ctrl.Document);
            ctrl.Undo();
            ctrl.Redo();

            Assert.True(ctrl.CanUndo);
            Assert.False(ctrl.CanRedo);
            Assert.True(ctrl.IsDirty);
            Assert.Equal(afterEdit, Serialize(ctrl.Document));
        }

        [Fact]
        public void Apply_AfterUndo_TruncatesRedoTail()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            var leafData = FindLeaf(ctrl.Document, "DATA");
            var leafName = FindLeaf(ctrl.Document, "NAME");

            ctrl.Apply(IffEditCommands.EditLeafPayload(leafData, new byte[] { 0xAA }));
            ctrl.Undo();
            Assert.True(ctrl.CanRedo);

            ctrl.Apply(IffEditCommands.EditLeafPayload(leafName, new byte[] { 0xCC }));
            Assert.False(ctrl.CanRedo); // redo tail truncated
            Assert.True(ctrl.CanUndo);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Apply → Undo identity (byte-for-byte via re-serialize)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ApplyThenUndo_EditLeafPayload_SerializedBytesMatchBaseline()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            byte[] before = Serialize(ctrl.Document);
            var leaf = FindLeaf(ctrl.Document, "DATA");

            ctrl.Apply(IffEditCommands.EditLeafPayload(leaf, new byte[] { 0x99, 0x88, 0x77 }));
            ctrl.Undo();
            byte[] after = Serialize(ctrl.Document);

            Assert.Equal(before, after);
        }

        [Fact]
        public void ApplyThenUndoThenRedo_EditLeafPayload_SerializedBytesMatchEdited()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            var leaf = FindLeaf(ctrl.Document, "DATA");

            ctrl.Apply(IffEditCommands.EditLeafPayload(leaf, new byte[] { 0x99, 0x88 }));
            byte[] edited = Serialize(ctrl.Document);
            ctrl.Undo();
            ctrl.Redo();
            byte[] redone = Serialize(ctrl.Document);

            Assert.Equal(edited, redone);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Each D-03 structural op is independently undoable
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ApplyThenUndo_AddLeaf_RestoresOriginal()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            byte[] before = Serialize(ctrl.Document);
            var root = ctrl.Document.Root;

            ctrl.Apply(IffEditCommands.AddLeaf(root, "BLAH", new byte[] { 0xFF, 0xEE }));
            Assert.NotEqual(before, Serialize(ctrl.Document));
            ctrl.Undo();

            Assert.Equal(before, Serialize(ctrl.Document));
        }

        [Fact]
        public void ApplyThenUndo_AddContainer_RestoresOriginal()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            byte[] before = Serialize(ctrl.Document);
            var root = ctrl.Document.Root;

            ctrl.Apply(IffEditCommands.AddContainer(root, "FORM", "NEWS"));
            Assert.NotEqual(before, Serialize(ctrl.Document));
            ctrl.Undo();

            Assert.Equal(before, Serialize(ctrl.Document));
        }

        [Fact]
        public void ApplyThenUndo_Remove_RestoresOriginal()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            byte[] before = Serialize(ctrl.Document);
            var leaf = FindLeaf(ctrl.Document, "DATA");

            ctrl.Apply(IffEditCommands.Remove(leaf));
            Assert.NotEqual(before, Serialize(ctrl.Document));
            ctrl.Undo();

            Assert.Equal(before, Serialize(ctrl.Document));
        }

        [Fact]
        public void ApplyThenUndo_RenameRetag_RestoresOriginal()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            byte[] before = Serialize(ctrl.Document);
            var leaf = FindLeaf(ctrl.Document, "DATA");

            ctrl.Apply(IffEditCommands.RenameRetag(leaf, "XXXX"));
            Assert.NotEqual(before, Serialize(ctrl.Document));
            ctrl.Undo();

            Assert.Equal(before, Serialize(ctrl.Document));
        }

        [Fact]
        public void ApplyThenUndo_EditFormSubType_RestoresOriginal()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            byte[] before = Serialize(ctrl.Document);
            // BuildHappyPath has FORM/OBJS — find it.
            var inner = ctrl.Document.Root.Children.First(n => n.Kind == MutableIffNodeKind.Container);

            ctrl.Apply(IffEditCommands.EditFormSubType(inner, "ZZZZ"));
            Assert.NotEqual(before, Serialize(ctrl.Document));
            ctrl.Undo();

            Assert.Equal(before, Serialize(ctrl.Document));
        }

        [Fact]
        public void ApplyThenUndo_Duplicate_RestoresOriginal()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            byte[] before = Serialize(ctrl.Document);
            var leaf = FindLeaf(ctrl.Document, "DATA");

            ctrl.Apply(IffEditCommands.Duplicate(leaf));
            Assert.NotEqual(before, Serialize(ctrl.Document));
            ctrl.Undo();

            Assert.Equal(before, Serialize(ctrl.Document));
        }

        [Fact]
        public void ApplyThenUndo_MoveUp_RestoresOriginal()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            byte[] before = Serialize(ctrl.Document);
            // NAME is the second child of FORM/WSNP — move it up.
            var leaf = FindLeaf(ctrl.Document, "NAME");

            ctrl.Apply(IffEditCommands.MoveUp(leaf));
            Assert.NotEqual(before, Serialize(ctrl.Document));
            ctrl.Undo();

            Assert.Equal(before, Serialize(ctrl.Document));
        }

        [Fact]
        public void ApplyThenUndo_MoveDown_RestoresOriginal()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            byte[] before = Serialize(ctrl.Document);
            // DATA is the first child of FORM/WSNP — move it down.
            var leaf = FindLeaf(ctrl.Document, "DATA");

            ctrl.Apply(IffEditCommands.MoveDown(leaf));
            Assert.NotEqual(before, Serialize(ctrl.Document));
            ctrl.Undo();

            Assert.Equal(before, Serialize(ctrl.Document));
        }

        [Fact]
        public void ApplyThenUndo_ReplaceLeafFromBytes_RestoresOriginal()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            byte[] before = Serialize(ctrl.Document);
            var leaf = FindLeaf(ctrl.Document, "DESC");

            ctrl.Apply(IffEditCommands.ReplaceLeafFromBytes(leaf, new byte[] { 0x01, 0x02, 0x03, 0x04 }));
            Assert.NotEqual(before, Serialize(ctrl.Document));
            ctrl.Undo();

            Assert.Equal(before, Serialize(ctrl.Document));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Dirty propagation to root
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Apply_OnDeeplyNestedLeaf_DirtyPropagatesToRoot()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            // INFO leaf lives inside FORM/OBJS inside FORM/WSNP — two levels deep.
            var leaf = FindLeaf(ctrl.Document, "INFO");
            Assert.False(ctrl.Document.Root.IsDirty);

            ctrl.Apply(IffEditCommands.EditLeafPayload(leaf, new byte[] { 0x42 }));

            Assert.True(leaf.IsDirty);
            // Walk up to root — every ancestor must be dirty.
            var p = leaf.Parent;
            while (p != null)
            {
                Assert.True(p.IsDirty);
                p = p.Parent;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Baseline-clean dirty semantics (08-REVIEWS Codex unique)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Undo_BackToBaseline_IsDirtyBecomesFalse()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            var leaf = FindLeaf(ctrl.Document, "DATA");

            ctrl.Apply(IffEditCommands.EditLeafPayload(leaf, new byte[] { 0xAA }));
            Assert.True(ctrl.IsDirty);
            ctrl.Undo();
            Assert.False(ctrl.IsDirty);
        }

        [Fact]
        public void Apply_Undo_Apply_DirtyTrueAcrossSequence()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            var leaf = FindLeaf(ctrl.Document, "DATA");

            ctrl.Apply(IffEditCommands.EditLeafPayload(leaf, new byte[] { 0xAA }));
            Assert.True(ctrl.IsDirty);
            ctrl.Undo();
            Assert.False(ctrl.IsDirty);
            ctrl.Apply(IffEditCommands.EditLeafPayload(leaf, new byte[] { 0xBB, 0xCC }));
            Assert.True(ctrl.IsDirty);
        }

        // ─────────────────────────────────────────────────────────────────────
        // EditApplied event
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void EditApplied_FiresOnApplyUndoRedo()
        {
            byte[] baseline;
            var ctrl = NewControllerOverHappyPath(out baseline);
            var leaf = FindLeaf(ctrl.Document, "DATA");
            int fires = 0;
            ctrl.EditApplied += (sender, args) => { fires++; };

            ctrl.Apply(IffEditCommands.EditLeafPayload(leaf, new byte[] { 0xAA }));
            Assert.Equal(1, fires);
            ctrl.Undo();
            Assert.Equal(2, fires);
            ctrl.Redo();
            Assert.Equal(3, fires);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static MutableIffNode FindLeaf(MutableIffDocument doc, string typeId)
        {
            var found = FindLeafRecursive(doc.Root, typeId);
            Assert.NotNull(found);
            return found;
        }

        private static MutableIffNode FindLeafRecursive(MutableIffNode node, string typeId)
        {
            if (node.Kind == MutableIffNodeKind.Leaf && node.TypeId == typeId) return node;
            if (node.Kind == MutableIffNodeKind.Container)
            {
                foreach (var c in node.Children)
                {
                    var r = FindLeafRecursive(c, typeId);
                    if (r != null) return r;
                }
            }
            return null;
        }
    }
}
