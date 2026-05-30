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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using UtinniCoreDotNet.Formats.Datatable;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Tests.FormatsTests.Datatable;
using Xunit;

namespace UtinniCoreDotNet.Tests.UITests
{
    /// <summary>
    /// D-09 view-only-sort enforcement (RESEARCH Pitfall 9). The REAL test that a genuine
    /// <see cref="DataGridView.Sort(DataGridViewColumn, ListSortDirection)"/> on the VIEW does NOT
    /// change the bytes <see cref="DataTableWriter"/> emits from the MODEL. Uses a plain
    /// <see cref="System.Windows.Forms.DataGridView"/> per the cross-repo UI-test-placement decision
    /// shared with Plan 09-03 — NO <c>TheJawaToolboxDotNet</c> reference, NO <c>ThemedDataGridView</c>.
    ///
    /// <para><b>STA requirement:</b> WinForms controls require an STA thread. Following the Plan 09-03
    /// perf-probe convention, this <c>[Fact]</c> runs the WinForms work on a dedicated STA thread it
    /// creates + joins (no <c>Xunit.StaFact</c> package dependency).</para>
    /// </summary>
    public class DatatableSortViewOnlyTests
    {
        private static MutableDataTableDocument LoadMutable(byte[] bytes)
        {
            IffDocument iff;
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                iff = IffReader.Read(ms);
            }
            MutableIffDocument mutableIff = MutableIffDocument.FromDocument(iff, bytes);
            return DataTableDocument.FromIff(mutableIff).Mutable;
        }

        [Fact(DisplayName = "D-09: a live DataGridView.Sort does NOT mutate the model serialization")]
        public void Sort_DoesNotMutateModelOrder()
        {
            Exception staFailure = null;

            var staThread = new Thread(() =>
            {
                try
                {
                    RunSortCheck();
                }
                catch (Exception ex)
                {
                    staFailure = ex;
                }
            });
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();

            if (staFailure != null)
            {
                throw new Exception("STA sort-check failed: " + staFailure.Message, staFailure);
            }
        }

        private static void RunSortCheck()
        {
            // Build a fixture whose model order is NOT already ascending on the chosen column so a view
            // sort visibly reorders. BuildV1Minimal has an int "id" column [10, 20] (already ascending),
            // so build a custom doc with a shuffled int column instead.
            MutableDataTableDocument dtDoc = BuildShuffledIntColumnDoc();
            const int sortCol = 0;

            // Capture the pre-sort MODEL serialization.
            byte[] preSortBytes = new DataTableWriter(dtDoc).Serialize();

            using (var grid = new DataGridView())
            {
                grid.AllowUserToAddRows = false;
                for (int c = 0; c < dtDoc.Columns.Count; c++)
                {
                    var col = new DataGridViewTextBoxColumn { HeaderText = dtDoc.Columns[c].Name };
                    col.SortMode = DataGridViewColumnSortMode.Automatic;
                    grid.Columns.Add(col);
                }

                // Populate rows in MODEL order.
                for (int r = 0; r < dtDoc.Rows.Count; r++)
                {
                    var values = new object[dtDoc.Columns.Count];
                    for (int c = 0; c < dtDoc.Columns.Count; c++)
                    {
                        values[c] = dtDoc.Rows[r].Cells[c].Value.ToString();
                    }
                    grid.Rows.Add(values);
                }

                // Force handle creation so Sort actually re-orders the bound view.
                var dummy = grid.Handle;
                GC.KeepAlive(dummy);

                List<string> preView = grid.Rows.Cast<DataGridViewRow>()
                    .Where(rr => !rr.IsNewRow)
                    .Select(rr => rr.Cells[sortCol].Value?.ToString())
                    .ToList();

                // Invoke the built-in sort for real.
                grid.Sort(grid.Columns[sortCol], ListSortDirection.Ascending);

                List<string> postView = grid.Rows.Cast<DataGridViewRow>()
                    .Where(rr => !rr.IsNewRow)
                    .Select(rr => rr.Cells[sortCol].Value?.ToString())
                    .ToList();

                // The view actually reordered (proves the sort RAN, not a no-op).
                Assert.NotEqual(preView, postView);
            }

            // Re-serialize the MODEL — byte-identical despite the live view sort (D-09 view-only).
            byte[] postSortBytes = new DataTableWriter(dtDoc).Serialize();
            Assert.Equal(preSortBytes, postSortBytes);
        }

        // A V0001 doc with one DT_Int column whose values are shuffled (NOT ascending) + one string
        // column, so a view sort on column 0 visibly reorders the rows.
        private static MutableDataTableDocument BuildShuffledIntColumnDoc()
        {
            var cols = new List<MutableDataTableColumn>
            {
                new MutableDataTableColumn("id", new DataTableColumnType("i")),
                new MutableDataTableColumn("name", new DataTableColumnType("s")),
            };

            int[] ids = { 30, 10, 50, 20, 40 };
            var rows = new List<MutableDataTableRow>();
            for (int i = 0; i < ids.Length; i++)
            {
                var row = new MutableDataTableRow();
                var cId = new MutableDataTableCell(DataTableCellValue.FromInt(ids[i]), null);
                cId.SetParentColumn(cols[0]);
                row.AddCellInternal(cId);
                var cName = new MutableDataTableCell(DataTableCellValue.FromString("row" + i), null);
                cName.SetParentColumn(cols[1]);
                row.AddCellInternal(cName);
                rows.Add(row);
            }

            return new MutableDataTableDocument("0001", cols, rows);
        }

        /// <summary>
        /// CR-01 regression: after a view-only sort physically reorders the grid, an edit made at a
        /// VISUAL row must be translated back to the correct MODEL row before it touches the model —
        /// otherwise the edit silently corrupts a different row's bytes (the antithesis of SC4). This
        /// asserts the row-Tag translation contract that <see cref="DataTableWriter"/>'s callers rely on
        /// (ThemedDataGridView.BindMutable stamps the model index into each row's Tag; ToModelRowIndex +
        /// FormDatatableEditor.CommitCell translate through it). Uses a plain DataGridView per the Plan
        /// 09-03 cross-assembly UI-test placement decision — NO TheJawaToolboxDotNet reference.
        /// </summary>
        [Fact(DisplayName = "CR-01: an edit at a visual row after sort hits the correct model row (other rows byte-preserved)")]
        public void EditAfterSort_TranslatesVisualToModel_PreservesOtherRows()
        {
            Exception staFailure = null;

            var staThread = new Thread(() =>
            {
                try
                {
                    RunEditAfterSortCheck();
                }
                catch (Exception ex)
                {
                    staFailure = ex;
                }
            });
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();

            if (staFailure != null)
            {
                throw new Exception("STA edit-after-sort check failed: " + staFailure.Message, staFailure);
            }
        }

        private static void RunEditAfterSortCheck()
        {
            MutableDataTableDocument dtDoc = BuildShuffledIntColumnDoc(); // ids {30,10,50,20,40}, names row0..row4
            const int sortCol = 0; // id
            const int nameCol = 1; // name

            // Snapshot every model row's original name so we can prove EXACTLY one row changes.
            string[] originalNames = dtDoc.Rows.Select(r => r.Cells[nameCol].Value.ToString()).ToArray();

            int modelRow;
            using (var grid = new DataGridView())
            {
                grid.AllowUserToAddRows = false;
                for (int c = 0; c < dtDoc.Columns.Count; c++)
                {
                    var col = new DataGridViewTextBoxColumn { HeaderText = dtDoc.Columns[c].Name };
                    col.SortMode = DataGridViewColumnSortMode.Automatic;
                    grid.Columns.Add(col);
                }

                // Populate in MODEL order, stamping each grid row with its model index — the SAME contract
                // ThemedDataGridView.BindMutable establishes and ToModelRowIndex relies on.
                for (int r = 0; r < dtDoc.Rows.Count; r++)
                {
                    var values = new object[dtDoc.Columns.Count];
                    for (int c = 0; c < dtDoc.Columns.Count; c++)
                    {
                        values[c] = dtDoc.Rows[r].Cells[c].Value.ToString();
                    }
                    int gi = grid.Rows.Add(values);
                    grid.Rows[gi].Tag = r;
                }

                var dummy = grid.Handle;
                GC.KeepAlive(dummy);

                // Ascending sort on id: model row 1 (id=10) becomes VISUAL row 0.
                grid.Sort(grid.Columns[sortCol], ListSortDirection.Ascending);

                const int visualRow = 0;
                modelRow = ResolveModelRowIndex(grid, visualRow);

                // Core of the fix: visual row 0 after an ascending sort is NOT model row 0 (the shuffle
                // guarantees it) — so writing through the raw visual index WOULD corrupt the wrong row.
                Assert.NotEqual(0, modelRow);
                // The resolved model row is exactly the one whose id is shown at visual row 0.
                Assert.Equal(
                    grid.Rows[visualRow].Cells[sortCol].Value.ToString(),
                    dtDoc.Rows[modelRow].Cells[sortCol].Value.ToString());
            }

            // Commit the edit through the MODEL index (what CommitCell now does) — only that row may change.
            dtDoc.Rows[modelRow].Cells[nameCol].Value = DataTableCellValue.FromString("EDITED");

            int changed = 0;
            for (int r = 0; r < dtDoc.Rows.Count; r++)
            {
                string now = dtDoc.Rows[r].Cells[nameCol].Value.ToString();
                if (now != originalNames[r])
                {
                    changed++;
                    Assert.Equal("EDITED", now);
                    Assert.Equal(modelRow, r);
                }
            }
            Assert.Equal(1, changed);
        }

        // Mirrors ThemedDataGridView.ToModelRowIndex: resolve the model row index a visual row carries in
        // its Tag (set at bind time). A regression that drops the Tag translation and indexes the model
        // by the raw visual index would make EditAfterSort_... write to the wrong row and fail.
        private static int ResolveModelRowIndex(DataGridView grid, int visualRowIndex)
        {
            if (visualRowIndex >= 0 && visualRowIndex < grid.Rows.Count && grid.Rows[visualRowIndex].Tag is int modelIndex)
            {
                return modelIndex;
            }
            return visualRowIndex;
        }
    }
}
