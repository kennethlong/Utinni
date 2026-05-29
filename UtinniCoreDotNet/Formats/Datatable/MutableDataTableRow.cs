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
// DTII (.tab datatable) format understood by reading
// swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/{DataTable,DataTableColumnType,
// DataTableCell,DataTableWriter}.{h,cpp} (SOE/Bootprint, All Rights Reserved). No code, comments,
// identifier names, or test fixtures copied from any reference source. Implementation original to
// Utinni under MIT.

using System;
using System.Collections.Generic;

namespace UtinniCoreDotNet.Formats.Datatable
{
    /// <summary>
    /// Mutable per-row model: an ordered list of <see cref="MutableDataTableCell"/> (one per column)
    /// plus a dirty roll-up. A row is dirty if any of its cells is dirty OR the row was added after
    /// load. <see cref="MarkDirty"/> propagates to the parent document one level up.
    /// </summary>
    public sealed class MutableDataTableRow
    {
        private readonly List<MutableDataTableCell> cells = new List<MutableDataTableCell>();

        /// <summary>The cells of this row, one per column, in column order.</summary>
        public IReadOnlyList<MutableDataTableCell> Cells
        {
            get { return cells.AsReadOnly(); }
        }

        /// <summary>True iff this row was added after load (Plan 09-04 AddRow).</summary>
        public bool IsAdded { get; internal set; }

        /// <summary>The owning document (set at construction); MarkDirty propagates here.</summary>
        internal MutableDataTableDocument ParentDocument { get; set; }

        /// <summary>
        /// True iff any cell in the row is dirty OR the row was added. Computed on read so it always
        /// reflects current cell state (no stale roll-up).
        /// </summary>
        public bool IsDirty
        {
            get
            {
                if (IsAdded) return true;
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].IsDirty) return true;
                }

                return false;
            }
        }

        /// <summary>Constructs an empty row.</summary>
        public MutableDataTableRow()
        {
        }

        // Append a cell during the reader build. The cell's ParentRow back-ref is wired here.
        internal void AddCellInternal(MutableDataTableCell cell)
        {
            if (cell == null) throw new ArgumentNullException("cell");
            cells.Add(cell);
            cell.SetParentRow(this);
        }

        // Remove the cell at the given column index during a RemoveColumn structural edit. Internal
        // because row-cell shape is owned by the document-level structural helpers
        // (MutableDataTableDocument.RemoveColumnAt) — direct callers go through the document so the
        // column list and every row's cell list stay in lock-step.
        internal void RemoveCellInternal(int index)
        {
            if (index < 0 || index >= cells.Count)
            {
                throw new ArgumentOutOfRangeException("index", index, "Cell index is out of range for this row.");
            }

            cells.RemoveAt(index);
            MarkDirty();
        }

        /// <summary>Notifies the parent document that this row changed (one-level propagation).</summary>
        internal void MarkDirty()
        {
            if (ParentDocument != null)
            {
                ParentDocument.MarkDirty();
            }
        }
    }
}
