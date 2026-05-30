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
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using UtinniCoreDotNet.Formats.StringTable;
using UtinniCoreDotNet.Tests.FormatsTests.StringTable;
using Xunit;

namespace UtinniCoreDotNet.Tests.UITests
{
    /// <summary>
    /// D-03c view-only sort + filter enforcement (Plan 10-04). The REAL test that a genuine
    /// <see cref="DataGridView.Sort(DataGridViewColumn, ListSortDirection)"/> AND a filter (Row.Visible)
    /// on the VIEW do NOT change the bytes <see cref="StringTableWriter"/> emits from the MODEL. Uses a
    /// plain <see cref="System.Windows.Forms.DataGridView"/> per the cross-repo UI-test-placement
    /// decision shared with Plan 09-03 / 10-03 — NO <c>TheJawaToolboxDotNet</c> reference, NO
    /// <c>ThemedDataGridView</c>.
    ///
    /// <para><b>STA requirement:</b> WinForms controls require an STA thread; the [Fact] runs the
    /// WinForms work on a dedicated STA thread it creates + joins (no Xunit.StaFact dependency).</para>
    /// </summary>
    public class StringTableSortViewOnlyTests
    {
        [Fact(DisplayName = "D-03c: a live DataGridView sort + filter does NOT mutate the model serialization")]
        public void SortAndFilter_DoNotMutateSerializedBytes()
        {
            Exception staFailure = null;

            var staThread = new Thread(() =>
            {
                try
                {
                    RunSortFilterCheck();
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
                throw new Exception("STA sort/filter check failed: " + staFailure.Message, staFailure);
            }
        }

        private static void RunSortFilterCheck()
        {
            MutableStringTableDocument doc =
                StringTableDocument.FromBytes(StringTableFixtures.BuildV1MultiEntry()).Mutable; // alpha/beta/gamma
            const int keyCol = 0;

            byte[] preBytes = StringTableWriter.Serialize(doc);
            List<string> preModelOrder = doc.Entries.Select(e => e.Name).ToList();

            using (var grid = new DataGridView())
            {
                grid.AllowUserToAddRows = false;
                grid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "Key", SortMode = DataGridViewColumnSortMode.Automatic });
                grid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "Text", SortMode = DataGridViewColumnSortMode.Automatic });

                // Populate rows in MODEL order.
                foreach (MutableStringTableEntry e in doc.Entries)
                {
                    grid.Rows.Add(e.Name, e.Text);
                }

                var dummy = grid.Handle; // force handle creation so Sort actually reorders the view.
                GC.KeepAlive(dummy);

                List<string> preView = grid.Rows.Cast<DataGridViewRow>()
                    .Where(r => !r.IsNewRow).Select(r => r.Cells[keyCol].Value?.ToString()).ToList();

                // A real descending sort on Key reorders the VIEW (alpha/beta/gamma → gamma/beta/alpha).
                grid.Sort(grid.Columns[keyCol], ListSortDirection.Descending);

                List<string> postView = grid.Rows.Cast<DataGridViewRow>()
                    .Where(r => !r.IsNewRow).Select(r => r.Cells[keyCol].Value?.ToString()).ToList();
                Assert.NotEqual(preView, postView); // proves the sort RAN.

                // Simulate a live filter: hide a row (the D-03c filter is view-only Row.Visible).
                grid.Rows[0].Visible = false;
            }

            // Re-serialize the MODEL — byte-identical despite the live view sort + filter (D-03c).
            byte[] postBytes = StringTableWriter.Serialize(doc);
            Assert.Equal(preBytes, postBytes);

            // The model's Entries order + clean state are untouched by the view operations.
            Assert.Equal(preModelOrder, doc.Entries.Select(e => e.Name).ToList());
            Assert.False(doc.AnyDirtyOrAdded());
        }
    }
}
