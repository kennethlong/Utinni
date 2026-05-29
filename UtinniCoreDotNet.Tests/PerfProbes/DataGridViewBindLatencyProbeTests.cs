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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using UtinniCoreDotNet.Formats.Datatable;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Tests.FormatsTests.Datatable;
using Xunit;
using Xunit.Abstractions;

namespace UtinniCoreDotNet.Tests.PerfProbes
{
    /// <summary>
    /// Performance probe (Assumption A5 decision-branch hinge for Plan 09-06). Measures the
    /// DataGridView initial-bind latency for <c>BuildV1CombatDataTableLike()</c> (~200 rows × ~30
    /// cols) on the PRODUCTION path — a plain <see cref="System.Windows.Forms.DataGridView"/> WITH a
    /// representative <c>CellFormatting</c> overlay handler attached (the cell-state painting the
    /// ThemedDataGridView ships). References NO TJT type (iter-2 item 3 cross-repo decision): the
    /// probe mirrors <c>DatatableColumnFactory</c>'s type→column mapping and the overlay handler
    /// inline.
    ///
    /// <remarks>
    /// <para>The <c>ThemedDataGridView</c> constructor's one-time property assignments are NOT on the
    /// measured hot path; the per-cell <c>CellFormatting</c> overlay IS (cursor item 2 MEDIUM — a
    /// bare grid under-measures), so this probe attaches an equivalent handler.</para>
    ///
    /// <para><b>NOT a fail-on-threshold test</b> — it records the measured value via
    /// <see cref="Trace.WriteLine"/> (parseable <c>DataGridViewBindLatency_CombatDataTableLike:</c>
    /// prefix) and always passes (the only assert is a 10s sanity floor against a hang). The 100 ms
    /// threshold gates Plan 09-06's VirtualMode decision, not this Fact's pass/fail.</para>
    ///
    /// <para><b>STA requirement:</b> WinForms controls require an STA thread. Rather than add the
    /// <c>Xunit.StaFact</c> package (a new external dependency), this <c>[Fact]</c> runs the WinForms
    /// work on a dedicated STA thread it creates + joins — same observable behavior, zero new
    /// packages.</para>
    /// </remarks>
    /// </summary>
    public class DataGridViewBindLatencyProbeTests
    {
        private readonly ITestOutputHelper output;

        public DataGridViewBindLatencyProbeTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void BindLatency_CombatDataTableLike_RecordsMeasurement()
        {
            double elapsedMs = 0;
            int rowCount = 0;
            int colCount = 0;
            Exception staFailure = null;

            var staThread = new Thread(() =>
            {
                try
                {
                    Measure(out elapsedMs, out rowCount, out colCount);
                }
                catch (Exception ex)
                {
                    staFailure = ex;
                }
            });
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();

            Assert.Null(staFailure);

            string measurement =
                "DataGridViewBindLatency_CombatDataTableLike: "
                + elapsedMs.ToString("F2") + " ms (" + rowCount + " rows × " + colCount
                + " cols, CellFormatting overlay attached)";
            Trace.WriteLine(measurement);
            // Surface the measurement in the dotnet-test console output (Trace alone is swallowed by
            // the console logger) so the executor can capture it for the SUMMARY.
            output.WriteLine(measurement);

            // Sanity floor against a hang only — the threshold decision lives in the SUMMARY.
            Assert.True(elapsedMs < 10_000,
                "DataGridView bind took " + elapsedMs.ToString("F2") + " ms (> 10s sanity floor).");
        }

        private static void Measure(out double elapsedMs, out int rowCount, out int colCount)
        {
            // (1) Load the fixture purely in test code (no TJT type).
            byte[] bytes = DatatableFixtures.BuildV1CombatDataTableLike();
            IffDocument iff;
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                iff = IffReader.Read(ms);
            }
            MutableIffDocument mutableIff = MutableIffDocument.FromDocument(iff, bytes);
            DataTableDocument doc = DataTableDocument.FromIff(mutableIff);
            MutableDataTableDocument model = doc.Mutable;

            rowCount = model.Rows.Count;
            colCount = model.Columns.Count;

            using (var grid = new DataGridView())
            {
                // (2) Representative CellFormatting overlay handler — mirrors UI-SPEC § Cell-state
                // visual overlays (dirty ForeColor swap + needs-review BackColor branch). This is the
                // real per-cell run-time cost the production grid pays.
                grid.CellFormatting += (s, e) =>
                {
                    if (e.RowIndex < 0 || e.RowIndex >= model.Rows.Count) return;
                    if (e.ColumnIndex < 0 || e.ColumnIndex >= model.Columns.Count) return;
                    MutableDataTableCell cell = model.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    if (cell.NeedsReview)
                    {
                        e.CellStyle.BackColor = Color.Red;
                    }
                    else if (cell.IsDirty)
                    {
                        e.CellStyle.ForeColor = Color.FromArgb(0, 122, 204);
                    }
                };

                // (3) Per-column DataGridViewColumn list mirroring DatatableColumnFactory's mapping
                // inline (the test cannot reference the TJT factory).
                var columns = new DataGridViewColumn[colCount];
                for (int c = 0; c < colCount; c++)
                {
                    columns[c] = BuildColumnLike(model.Columns[c]);
                }

                // (4) Force handle creation so the bind + CellFormatting paint cost is realized.
                var ignore = grid.Handle;

                var sw = Stopwatch.StartNew();
                grid.Columns.AddRange(columns);
                for (int r = 0; r < model.Rows.Count; r++)
                {
                    int rowIndex = grid.Rows.Add();
                    DataGridViewRow gridRow = grid.Rows[rowIndex];
                    MutableDataTableRow modelRow = model.Rows[r];
                    for (int c = 0; c < model.Columns.Count && c < gridRow.Cells.Count; c++)
                    {
                        gridRow.Cells[c].Value = modelRow.Cells[c].Value.ToString();
                    }
                }
                // Force a layout + paint pass so CellFormatting actually fires for visible cells.
                grid.Size = new Size(1200, 700);
                grid.Refresh();
                sw.Stop();

                elapsedMs = sw.Elapsed.TotalMilliseconds;
            }
        }

        // Mirror DatatableColumnFactory.Build's type→column mapping inline (no TJT reference).
        private static DataGridViewColumn BuildColumnLike(MutableDataTableColumn column)
        {
            DataTableColumnType ct = column.ColumnType;
            DataGridViewColumn dgc;
            switch (ct.Type)
            {
                case DataType.Bool:
                    dgc = new DataGridViewCheckBoxColumn();
                    break;
                case DataType.Enum:
                {
                    var combo = new DataGridViewComboBoxColumn();
                    foreach (var label in ct.EnumMap.Keys)
                    {
                        combo.Items.Add(label);
                    }
                    dgc = combo;
                    break;
                }
                default:
                    dgc = new DataGridViewTextBoxColumn();
                    break;
            }
            dgc.Name = column.Name;
            dgc.HeaderText = column.Name;
            return dgc;
        }
    }
}
