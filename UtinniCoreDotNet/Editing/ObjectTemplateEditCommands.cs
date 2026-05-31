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
// Factory + concrete commands for the three D-04 object-template mutations (Phase 11): edit a local
// override's value, add-override (promote an inherited field to a local override), and remove-override
// (revert a local override back to inherited). The object-template analog of
// UtinniCoreDotNet.Editing.DatatableEditCommands. Each command captures the inverse state needed for a
// byte-exact Undo. Pure-managed (NO scene undo/redo manager reference per CON-M-05); xUnit-testable.

using System;
using UtinniCoreDotNet.Formats.ObjectTemplate;

namespace UtinniCoreDotNet.Editing
{
    /// <summary>
    /// Factory methods for the three D-04 object-template edit commands consumed by
    /// FormObjectTemplateEditor. Mirrors the <see cref="DatatableEditCommands"/> factory layout — public
    /// static factories returning private nested <see cref="IObjectTemplateEditCommand"/> implementations,
    /// each capturing inverse state for Undo.
    /// </summary>
    public static class ObjectTemplateEditCommands
    {
        /// <summary>
        /// Edit a local override's value (D-04 mutation 1). <see cref="IObjectTemplateEditCommand.Do"/>
        /// captures the prior value then forwards to <see cref="MutableObjectTemplate.EditOverride"/>;
        /// <see cref="IObjectTemplateEditCommand.UndoOp"/> restores the captured prior value byte-exactly.
        /// </summary>
        public static IObjectTemplateEditCommand EditValue(string fieldName, ObjectTemplateParamValue newValue)
        {
            if (fieldName == null) throw new ArgumentNullException("fieldName");
            if (newValue == null) throw new ArgumentNullException("newValue");
            return new EditValueCommand(fieldName, newValue);
        }

        /// <summary>
        /// Add-override — promote an inherited field to a local override (D-04 mutation 2).
        /// <see cref="IObjectTemplateEditCommand.Do"/> forwards to
        /// <see cref="MutableObjectTemplate.AddOverride"/>; <see cref="IObjectTemplateEditCommand.UndoOp"/>
        /// forwards to <see cref="MutableObjectTemplate.RemoveOverride"/> to remove the chunk it added.
        /// </summary>
        public static IObjectTemplateEditCommand AddOverride(string fieldName, ObjectTemplateParamValue value)
        {
            if (fieldName == null) throw new ArgumentNullException("fieldName");
            if (value == null) throw new ArgumentNullException("value");
            return new AddOverrideCommand(fieldName, value);
        }

        /// <summary>
        /// Remove-override — revert a local override back to inherited (D-04 mutation 3).
        /// <see cref="IObjectTemplateEditCommand.Do"/> captures the removed chunk's value then forwards
        /// to <see cref="MutableObjectTemplate.RemoveOverride"/>; <see cref="IObjectTemplateEditCommand.UndoOp"/>
        /// re-adds the captured value via <see cref="MutableObjectTemplate.AddOverride"/>.
        /// </summary>
        public static IObjectTemplateEditCommand RemoveOverride(string fieldName)
        {
            if (fieldName == null) throw new ArgumentNullException("fieldName");
            return new RemoveOverrideCommand(fieldName);
        }

        // ── Concrete commands ─────────────────────────────────────────────────

        private sealed class EditValueCommand : IObjectTemplateEditCommand
        {
            private readonly string _fieldName;
            private readonly ObjectTemplateParamValue _newValue;
            private ObjectTemplateParamValue _priorValue;

            public EditValueCommand(string fieldName, ObjectTemplateParamValue newValue)
            {
                _fieldName = fieldName;
                _newValue = newValue;
            }

            public void Do(MutableObjectTemplate document)
            {
                _priorValue = CapturePriorValue(document, _fieldName);
                document.EditOverride(_fieldName, _newValue);
            }

            public void UndoOp(MutableObjectTemplate document)
            {
                // Restore the captured prior value in place — the same byte-exact leaf re-encode path.
                document.EditOverride(_fieldName, _priorValue);
            }
        }

        private sealed class AddOverrideCommand : IObjectTemplateEditCommand
        {
            private readonly string _fieldName;
            private readonly ObjectTemplateParamValue _value;

            public AddOverrideCommand(string fieldName, ObjectTemplateParamValue value)
            {
                _fieldName = fieldName;
                _value = value;
            }

            public void Do(MutableObjectTemplate document)
            {
                document.AddOverride(_fieldName, _value);
            }

            public void UndoOp(MutableObjectTemplate document)
            {
                document.RemoveOverride(_fieldName);
            }
        }

        private sealed class RemoveOverrideCommand : IObjectTemplateEditCommand
        {
            private readonly string _fieldName;
            private ObjectTemplateParamValue _removedValue;

            public RemoveOverrideCommand(string fieldName)
            {
                _fieldName = fieldName;
            }

            public void Do(MutableObjectTemplate document)
            {
                _removedValue = CapturePriorValue(document, _fieldName);
                document.RemoveOverride(_fieldName);
            }

            public void UndoOp(MutableObjectTemplate document)
            {
                // Re-add the captured chunk to restore the local override.
                document.AddOverride(_fieldName, _removedValue);
            }
        }

        // Captures the current typed value of a local param so UndoOp can restore it byte-exactly. Throws
        // if the field is not a local override (Edit/Remove only operate on existing local params).
        private static ObjectTemplateParamValue CapturePriorValue(MutableObjectTemplate document, string fieldName)
        {
            foreach (MutableObjectTemplateParam p in document.LocalParams)
            {
                if (string.Equals(p.FieldName, fieldName, StringComparison.Ordinal))
                {
                    return p.Value;
                }
            }
            throw new InvalidOperationException(
                "No local override named '" + fieldName + "' to capture for undo.");
        }
    }
}
