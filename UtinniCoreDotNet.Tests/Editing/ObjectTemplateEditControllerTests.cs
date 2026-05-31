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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UtinniCoreDotNet.Editing;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.ObjectTemplate;
using Xunit;

namespace UtinniCoreDotNet.Tests.Editing
{
    /// <summary>
    /// Coverage for the editor-local object-template undo/redo controller + the three D-04 mutation
    /// commands (11-02 Task 2): <see cref="ObjectTemplateEditController"/> +
    /// <see cref="ObjectTemplateEditCommands"/>. Asserts EditValue→Undo restores byte-exactly,
    /// AddOverride→Undo removes, RemoveOverride→Undo restores, MarkSaved clears IsDirty, and the
    /// CanUndo/CanRedo transitions. Pure-managed — no scene UndoRedoManager coupling (CON-M-05).
    /// </summary>
    public class ObjectTemplateEditControllerTests
    {
        // ── Synthetic-fixture builders (mirror the MutableObjectTemplateTests idiom) ──

        private static byte[] EncodeParam(string name, byte[] valueRegion)
        {
            using (var ms = new MemoryStream())
            {
                byte[] n = Encoding.ASCII.GetBytes(name);
                ms.Write(n, 0, n.Length);
                ms.WriteByte(0);
                if (valueRegion != null && valueRegion.Length > 0) ms.Write(valueRegion, 0, valueRegion.Length);
                return ms.ToArray();
            }
        }

        private static byte[] Int32Le(int v)
        {
            return new[] { (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)((v >> 24) & 0xFF) };
        }

        private static byte[] IntValueRegion(int v, char delta)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(1);
                ms.WriteByte((byte)delta);
                byte[] le = Int32Le(v);
                ms.Write(le, 0, le.Length);
                return ms.ToArray();
            }
        }

        private static byte[] BuildTemplate(IList<KeyValuePair<string, byte[]>> paramChunks)
        {
            MutableIffNode root = MutableIffNode.NewContainer("FORM", "SHOT");
            MutableIffNode derv = root.AddContainer("FORM", "DERV");
            using (var ms = new MemoryStream())
            {
                byte[] n = Encoding.ASCII.GetBytes("object/base.iff");
                ms.Write(n, 0, n.Length);
                ms.WriteByte(0);
                derv.AddLeaf("XXXX", ms.ToArray());
            }
            MutableIffNode versionForm = root.AddContainer("FORM", "0000");
            versionForm.AddLeaf("PCNT", Int32Le(paramChunks.Count));
            foreach (KeyValuePair<string, byte[]> p in paramChunks)
            {
                versionForm.AddLeaf("XXXX", EncodeParam(p.Key, p.Value));
            }
            return IffWriter.Write(new MutableIffDocument(root));
        }

        private static byte[] TwoParamTemplate()
        {
            return BuildTemplate(new List<KeyValuePair<string, byte[]>>
            {
                new KeyValuePair<string, byte[]>("hitPoints", IntValueRegion(100, ' ')),
                new KeyValuePair<string, byte[]>("armor", IntValueRegion(5, ' '))
            });
        }

        private static MutableObjectTemplate Load(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            {
                IffDocument doc = IffReader.Read(ms);
                MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
                return MutableObjectTemplate.FromMutableIff(mut);
            }
        }

        private static MutableObjectTemplateParam Find(MutableObjectTemplate model, string name)
        {
            return model.LocalParams.FirstOrDefault(p => p.FieldName == name);
        }

        // ── EditValue → Undo restores byte-exact ─────────────────────────────

        [Fact]
        public void EditValue_ThenUndo_RestoresByteExact()
        {
            byte[] input = TwoParamTemplate();
            MutableObjectTemplate model = Load(input);
            var controller = new ObjectTemplateEditController(model);

            controller.Apply(ObjectTemplateEditCommands.EditValue("hitPoints",
                ObjectTemplateParamValue.FromInt(250, (byte)' ')));

            Assert.True(controller.IsDirty);
            Assert.Equal(250, Find(model, "hitPoints").Value.IntValue);

            controller.Undo();

            Assert.False(controller.IsDirty);
            Assert.Equal(100, Find(model, "hitPoints").Value.IntValue);

            // Byte-exact: serializing the undone model reproduces the original file.
            Assert.Equal(input, model.Serialize());

            // Redo re-applies.
            controller.Redo();
            Assert.True(controller.IsDirty);
            Assert.Equal(250, Find(model, "hitPoints").Value.IntValue);
        }

        // ── AddOverride → Undo removes ───────────────────────────────────────

        [Fact]
        public void AddOverride_ThenUndo_RemovesTheChunk()
        {
            byte[] input = TwoParamTemplate();
            MutableObjectTemplate model = Load(input);
            var controller = new ObjectTemplateEditController(model);

            controller.Apply(ObjectTemplateEditCommands.AddOverride("scale",
                ObjectTemplateParamValue.FromInt(2, (byte)' ')));

            Assert.NotNull(Find(model, "scale"));
            Assert.Equal(3, model.LocalParams.Count);

            controller.Undo();

            Assert.Null(Find(model, "scale"));
            Assert.Equal(2, model.LocalParams.Count);
            Assert.False(controller.IsDirty);

            // Byte-exact restore after undoing the add.
            Assert.Equal(input, model.Serialize());
        }

        // ── RemoveOverride → Undo restores ───────────────────────────────────

        [Fact]
        public void RemoveOverride_ThenUndo_RestoresTheChunk()
        {
            byte[] input = TwoParamTemplate();
            MutableObjectTemplate model = Load(input);
            var controller = new ObjectTemplateEditController(model);

            controller.Apply(ObjectTemplateEditCommands.RemoveOverride("armor"));

            Assert.Null(Find(model, "armor"));
            Assert.Equal(1, model.LocalParams.Count);

            controller.Undo();

            MutableObjectTemplateParam restored = Find(model, "armor");
            Assert.NotNull(restored);
            Assert.Equal(5, restored.Value.IntValue);
            Assert.Equal(2, model.LocalParams.Count);
            Assert.False(controller.IsDirty);
        }

        // ── MarkSaved sets IsDirty false ─────────────────────────────────────

        [Fact]
        public void MarkSaved_AfterEdit_ClearsIsDirty()
        {
            MutableObjectTemplate model = Load(TwoParamTemplate());
            var controller = new ObjectTemplateEditController(model);

            controller.Apply(ObjectTemplateEditCommands.EditValue("armor",
                ObjectTemplateParamValue.FromInt(9, (byte)' ')));
            Assert.True(controller.IsDirty);

            controller.MarkSaved();
            Assert.False(controller.IsDirty);

            // Editing again after a save dirties the document anew.
            controller.Apply(ObjectTemplateEditCommands.EditValue("armor",
                ObjectTemplateParamValue.FromInt(11, (byte)' ')));
            Assert.True(controller.IsDirty);
        }

        // ── CanUndo / CanRedo transitions ────────────────────────────────────

        [Fact]
        public void CanUndoCanRedo_TransitionsAcrossApplyUndoRedo()
        {
            MutableObjectTemplate model = Load(TwoParamTemplate());
            var controller = new ObjectTemplateEditController(model);

            Assert.False(controller.CanUndo);
            Assert.False(controller.CanRedo);

            controller.Apply(ObjectTemplateEditCommands.EditValue("hitPoints",
                ObjectTemplateParamValue.FromInt(1, (byte)' ')));
            Assert.True(controller.CanUndo);
            Assert.False(controller.CanRedo);

            controller.Undo();
            Assert.False(controller.CanUndo);
            Assert.True(controller.CanRedo);

            // A fresh Apply truncates the redo tail.
            controller.Apply(ObjectTemplateEditCommands.EditValue("hitPoints",
                ObjectTemplateParamValue.FromInt(2, (byte)' ')));
            Assert.True(controller.CanUndo);
            Assert.False(controller.CanRedo);
        }

        // ── EditApplied event fires on each operation ────────────────────────

        [Fact]
        public void EditApplied_FiresOnApplyUndoRedoAndMarkSaved()
        {
            MutableObjectTemplate model = Load(TwoParamTemplate());
            var controller = new ObjectTemplateEditController(model);
            int fired = 0;
            controller.EditApplied += (s, e) => fired++;

            controller.Apply(ObjectTemplateEditCommands.EditValue("armor",
                ObjectTemplateParamValue.FromInt(3, (byte)' ')));
            controller.Undo();
            controller.Redo();
            controller.MarkSaved();

            Assert.Equal(4, fired);
        }
    }
}
