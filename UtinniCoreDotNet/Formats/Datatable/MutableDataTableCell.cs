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
using System.IO;

namespace UtinniCoreDotNet.Formats.Datatable
{
    /// <summary>
    /// Per-cell hybrid-DOM node — the cell-granularity analog of
    /// <c>UtinniCoreDotNet.Formats.Iff.MutableIffNode</c> (mirrors its captured-slice + IsDirty +
    /// reserialize-fresh idiom from <c>MutableIffNode.cs:156-296,484-495</c>, but at PER-CELL rather
    /// than per-chunk granularity).
    ///
    /// <para><b>CF-04 byte preservation:</b> a clean cell (<c>!IsDirty</c> with a non-null
    /// originalSlice) re-emits its captured ROWS-payload byte slice verbatim via
    /// <see cref="WritePayload"/>; a dirty cell reserializes fresh via
    /// <see cref="DataTableCellValue.SerializeFresh"/>. Setting <see cref="Value"/> to an equal value
    /// is a no-op that preserves the slice + clean state; any real change sets dirty AND nulls the
    /// original slice (so a stale slice can never re-emit over an edited value).</para>
    ///
    /// <para><b>Undo primitive (iter-2 item 3 — codex HIGH):</b> <see cref="CaptureState"/> /
    /// <see cref="RestoreState"/> snapshot and restore the value + originalSlice + dirty + needs-review
    /// AS A UNIT, bypassing the <see cref="Value"/> setter's slice-nulling. This is the ONLY way to
    /// make <c>EditCellValue → Undo → bytes EXACTLY equal loaded</c> hold: a plain "set value back"
    /// would have nulled the slice and reserialize fresh (e.g. different float formatting or NUL
    /// length), which can differ. <see cref="RestoreState"/> re-attaches the exact original slice so
    /// <see cref="WritePayload"/> re-emits the original bytes byte-for-byte. Consumed by 09-01's SC4
    /// writer test, Plan 09-04's EditCellValue UndoOp, and Plan 09-06's ApplyCsvImport per-cell revert.</para>
    /// </summary>
    public sealed class MutableDataTableCell
    {
        private DataTableCellValue value;
        private byte[] originalSlice;
        private bool isDirty;
        private bool needsReview;

        /// <summary>The owning row (immutable after construction).</summary>
        public MutableDataTableRow Row { get; private set; }

        /// <summary>The owning column (immutable after construction).</summary>
        public MutableDataTableColumn Column { get; private set; }

        /// <summary>True iff this cell has been edited since load (drives fresh-vs-verbatim emit).</summary>
        public bool IsDirty
        {
            get { return isDirty; }
        }

        /// <summary>
        /// True iff a column-type change (Plan 09-04 ChangeColumnType cascade) flagged this cell for
        /// re-coercion review. Cleared by ChangeColumnType.Undo and by a fresh successful Value set.
        /// </summary>
        public bool NeedsReview
        {
            get { return needsReview; }
            internal set { needsReview = value; }
        }

        /// <summary>
        /// The typed value. Setting it to an EQUAL value (per <see cref="DataTableCellValue.Equals"/>)
        /// is a no-op preserving the original byte slice + clean state. Any real change sets dirty,
        /// nulls the original slice, clears needs-review, and rolls dirty up to the row + column.
        /// </summary>
        public DataTableCellValue Value
        {
            get { return value; }
            set
            {
                if (value == null) throw new ArgumentNullException("value");
                if (DataTableCellValue.Equals(this.value, value))
                {
                    return;
                }

                this.value = value;
                isDirty = true;
                originalSlice = null;
                needsReview = false;
                if (Row != null) Row.MarkDirty();
                if (Column != null) Column.MarkDirty();
            }
        }

        /// <summary>
        /// Constructs a freshly-loaded cell carrying its captured original ROWS-payload byte slice.
        /// Starts clean (<c>IsDirty == false</c>). The slice is the exact bytes for this cell within
        /// the ROWS chunk payload (zero-length for a DT_Comment cell).
        /// </summary>
        internal MutableDataTableCell(DataTableCellValue initialValue, byte[] capturedSlice)
        {
            value = initialValue;
            originalSlice = capturedSlice;
            isDirty = false;
            needsReview = false;
        }

        /// <summary>
        /// Returns a COPY of this cell's captured original ROWS-payload byte slice, or an empty array
        /// when the cell carries no slice (a freshly-built or edited cell). This is the read-side the
        /// Plan 09-02 <c>roundtrip-tab</c> per-cell ROWS-slice comparison algorithm uses against a fresh
        /// no-op re-load of a byte array — it never exposes the live backing array, so a caller cannot
        /// mutate the cell's captured slice.
        /// </summary>
        public byte[] GetOriginalSliceForCompare()
        {
            if (originalSlice == null || originalSlice.Length == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] copy = new byte[originalSlice.Length];
            Array.Copy(originalSlice, copy, originalSlice.Length);
            return copy;
        }

        internal void SetParentRow(MutableDataTableRow row)
        {
            Row = row;
        }

        internal void SetParentColumn(MutableDataTableColumn column)
        {
            Column = column;
        }

        /// <summary>
        /// Immutable snapshot of a cell's full state (value + original slice + dirty + needs-review),
        /// captured by <see cref="MutableDataTableCell.CaptureState"/> and restored atomically by
        /// <see cref="MutableDataTableCell.RestoreState"/>.
        /// </summary>
        internal struct CellState
        {
            internal readonly DataTableCellValue Value;
            internal readonly byte[] OriginalSlice;
            internal readonly bool IsDirty;
            internal readonly bool NeedsReview;

            internal CellState(DataTableCellValue value, byte[] originalSlice, bool isDirty, bool needsReview)
            {
                Value = value;
                OriginalSlice = originalSlice;
                IsDirty = isDirty;
                NeedsReview = needsReview;
            }
        }

        /// <summary>
        /// Captures a snapshot of all four fields. The originalSlice REFERENCE is copied (it is never
        /// mutated in place — the <see cref="Value"/> setter nulls-and-replaces, so the captured
        /// reference stays valid for a later <see cref="RestoreState"/>).
        /// </summary>
        internal CellState CaptureState()
        {
            return new CellState(value, originalSlice, isDirty, needsReview);
        }

        /// <summary>
        /// Restores a previously-captured snapshot DIRECTLY (bypassing the <see cref="Value"/>
        /// setter's slice-nulling), re-attaching the exact original byte slice + dirty/needs-review
        /// state so <see cref="WritePayload"/> re-emits the original bytes verbatim. This is the
        /// undo/revert primitive that makes the byte-exact undo invariant achievable (item 3).
        /// </summary>
        internal void RestoreState(CellState state)
        {
            value = state.Value;
            originalSlice = state.OriginalSlice;
            isDirty = state.IsDirty;
            needsReview = state.NeedsReview;
        }

        /// <summary>
        /// Rebaselines this cell after a successful save (Plan 09-04 <c>DatatableEditController.MarkSaved</c>,
        /// iter-2 item 8). Re-serializes the CURRENT value under the given column type and adopts those
        /// bytes as the new <c>originalSlice</c>, then clears the dirty + needs-review bits. After this
        /// call <see cref="IsDirty"/> reads false (the just-saved bytes are now canonical); the NEXT real
        /// <see cref="Value"/> set nulls the slice + flips dirty true again, so dirtiness is re-computed
        /// against the saved state rather than the original-load state.
        /// </summary>
        internal void RebaselineAfterSave(DataTableColumnType columnType)
        {
            if (columnType == null) throw new ArgumentNullException("columnType");

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                DataTableCellValue.SerializeFresh(bw, columnType, value);
                bw.Flush();
                originalSlice = ms.ToArray();
            }

            isDirty = false;
            needsReview = false;
        }

        /// <summary>
        /// Writes this cell's ROWS payload to the given writer. A clean cell with a non-empty original
        /// slice re-emits that slice verbatim (CF-04 byte preservation); otherwise it reserializes
        /// fresh via <see cref="DataTableCellValue.SerializeFresh"/>. A clean DT_Comment cell has a
        /// zero-length slice, so it falls through to SerializeFresh which also emits nothing — both
        /// paths produce zero bytes, consistent with item 13.
        /// </summary>
        internal void WritePayload(BinaryWriter bw, DataTableColumnType columnType)
        {
            if (bw == null) throw new ArgumentNullException("bw");
            if (columnType == null) throw new ArgumentNullException("columnType");

            if (!isDirty && originalSlice != null && originalSlice.Length > 0)
            {
                bw.Write(originalSlice, 0, originalSlice.Length);
                return;
            }

            DataTableCellValue.SerializeFresh(bw, columnType, value);
        }
    }
}
