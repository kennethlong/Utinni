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
using System.IO;
using System.Text;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.Datatable
{
    /// <summary>
    /// Mutable typed datatable model — version + columns + rows + a back-reference to the originating
    /// raw <see cref="MutableIffDocument"/>. <see cref="BuildMutableIff"/> materializes a fresh
    /// <c>FORM DTII { FORM &lt;ver&gt; { COLS, TYPE, ROWS } }</c> tree for re-serialize, composing on
    /// the Phase 8 IFF primitives (no new chunk framing here — see
    /// <c>DataTableWriter.cpp:650-664,821-911</c> for the SOE save dispatch this mirrors).
    ///
    /// <para><b>DT_Comment handling (item 13):</b> a DT_Comment column is PRESERVED in COLS (its
    /// name) and TYPE (its <c>c</c> spec) — Phase 9 round-trips from disk, so it does NOT replicate
    /// SOE <c>_saveColumns</c>/<c>_saveTypes</c>' comment-column skip (which would drop hand-crafted
    /// comment columns). Only the per-cell ROWS payload of a comment cell is zero bytes.</para>
    ///
    /// <para><b>D-09 view-only-sort defense (RESEARCH Pitfall 9):</b> ROWS is built by iterating the
    /// in-memory <see cref="Rows"/> collection directly — NEVER a UI-side DataGridView binding view.
    /// This framework type has no WinForms dependency.</para>
    /// </summary>
    public sealed class MutableDataTableDocument
    {
        /// <summary>Format version — "0000" or "0001".</summary>
        public string Version { get; }

        /// <summary>The columns, in column order.</summary>
        public IList<MutableDataTableColumn> Columns { get; }

        /// <summary>The rows, in row order.</summary>
        public IList<MutableDataTableRow> Rows { get; }

        /// <summary>
        /// The originating raw-IFF root (the byte source), retained so Plan 09-04's RemoveColumn can
        /// locate the original-bytes slice when needed. Null for a fresh (non-loaded) document.
        /// </summary>
        public MutableIffDocument SourceIff { get; internal set; }

        /// <summary>True iff any column or row is dirty.</summary>
        public bool IsDirty { get; private set; }

        /// <summary>Constructs a document with the given version, columns, and rows.</summary>
        public MutableDataTableDocument(string version, IList<MutableDataTableColumn> columns, IList<MutableDataTableRow> rows)
        {
            if (version == null) throw new ArgumentNullException("version");
            if (version != "0000" && version != "0001")
            {
                throw new DataTableParseException("Unsupported DTII version '" + version + "' (expected 0000 or 0001).");
            }

            if (columns == null) throw new ArgumentNullException("columns");
            if (rows == null) throw new ArgumentNullException("rows");

            Version = version;
            Columns = columns;
            Rows = rows;

            // Wire each row's parent-document back-ref so MarkDirty propagates here.
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].ParentDocument = this;
            }
        }

        /// <summary>Marks the document dirty (called by row roll-up).</summary>
        internal void MarkDirty()
        {
            IsDirty = true;
        }

        /// <summary>
        /// Materializes a fresh <c>FORM DTII { FORM &lt;ver&gt; { COLS, TYPE, ROWS } }</c>
        /// <see cref="MutableIffDocument"/> for serialization. The three leaf payloads are built as:
        /// <list type="bullet">
        ///   <item>COLS — <c>int32 numCols</c> (little-endian) + each column name NUL-terminated
        ///         (including DT_Comment columns, item 13).</item>
        ///   <item>TYPE — V0001: each column's <c>TypeSpec</c> NUL-terminated (including the <c>c</c>
        ///         spec for comment columns); V0000: each column's enum-ordinal as <c>int32 LE</c>
        ///         (<c>DataTable.cpp:502-534</c>).</item>
        ///   <item>ROWS — <c>int32 numRows</c> + per-row, per-column cell payload via
        ///         <see cref="MutableDataTableCell.WritePayload"/> (comment cells contribute zero bytes).</item>
        /// </list>
        /// </summary>
        public MutableIffDocument BuildMutableIff()
        {
            var dtiiRoot = MutableIffNode.NewContainer("FORM", "DTII");
            MutableIffNode versionForm = dtiiRoot.AddContainer("FORM", Version);

            versionForm.AddLeaf("COLS", BuildColsPayload());
            versionForm.AddLeaf("TYPE", BuildTypePayload());
            versionForm.AddLeaf("ROWS", BuildRowsPayload());

            return new MutableIffDocument(dtiiRoot);
        }

        // COLS: int32 numCols (LE) + numCols NUL-terminated ASCII column names.
        private byte[] BuildColsPayload()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(Columns.Count); // int32 little-endian on this platform
                for (int i = 0; i < Columns.Count; i++)
                {
                    WriteNulString(bw, Columns[i].Name);
                }

                bw.Flush();
                return ms.ToArray();
            }
        }

        // TYPE: V0001 → NUL-terminated type-spec strings; V0000 → int32 LE enum ordinals.
        private byte[] BuildTypePayload()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                if (Version == "0001")
                {
                    for (int i = 0; i < Columns.Count; i++)
                    {
                        WriteNulString(bw, Columns[i].ColumnType.TypeSpec);
                    }
                }
                else
                {
                    for (int i = 0; i < Columns.Count; i++)
                    {
                        bw.Write((int)Columns[i].ColumnType.Type);
                    }
                }

                bw.Flush();
                return ms.ToArray();
            }
        }

        // ROWS: int32 numRows (LE) + per-row, per-column cell payload.
        private byte[] BuildRowsPayload()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(Rows.Count);
                for (int r = 0; r < Rows.Count; r++)
                {
                    IReadOnlyList<MutableDataTableCell> cells = Rows[r].Cells;
                    for (int c = 0; c < Columns.Count; c++)
                    {
                        cells[c].WritePayload(bw, Columns[c].ColumnType);
                    }
                }

                bw.Flush();
                return ms.ToArray();
            }
        }

        private static void WriteNulString(BinaryWriter bw, string s)
        {
            s = s ?? string.Empty;
            byte[] ascii = Encoding.ASCII.GetBytes(s);
            bw.Write(ascii, 0, ascii.Length);
            bw.Write((byte)0);
        }
    }
}
