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

namespace UtinniCoreDotNet.Formats.Datatable
{
    /// <summary>
    /// Mutable per-column model: the column NAME (COLS chunk entry) + its parsed
    /// <see cref="DataTableColumnType"/> (TYPE chunk entry). Dirty/added/reordered flags drive the
    /// Plan 09-04 cascade and the TYPE/COLS chunk rebuild on serialize.
    /// </summary>
    public sealed class MutableDataTableColumn
    {
        private string name;
        private DataTableColumnType columnType;

        /// <summary>Column name (re-emitted into the COLS chunk). Setting it marks the column dirty.</summary>
        public string Name
        {
            get { return name; }
            set
            {
                if (value == null) throw new ArgumentNullException("value");
                if (!string.Equals(name, value, StringComparison.Ordinal))
                {
                    name = value;
                    IsDirty = true;
                }
            }
        }

        /// <summary>Parsed type-spec for this column. Setting it marks the column dirty.</summary>
        public DataTableColumnType ColumnType
        {
            get { return columnType; }
            set
            {
                if (value == null) throw new ArgumentNullException("value");
                if (!ReferenceEquals(columnType, value))
                {
                    columnType = value;
                    IsDirty = true;
                }
            }
        }

        /// <summary>True iff the column name or type has changed since load (drives TYPE/COLS rebuild).</summary>
        public bool IsDirty { get; internal set; }

        /// <summary>True iff the column was added after load (Plan 09-04 AddColumn).</summary>
        public bool IsAdded { get; internal set; }

        /// <summary>True iff the column was reordered via a header drag (Plan 09-04).</summary>
        public bool IsReordered { get; internal set; }

        /// <summary>Constructs a column from a freshly-loaded name + type (starts clean).</summary>
        public MutableDataTableColumn(string columnName, DataTableColumnType type)
        {
            if (columnName == null) throw new ArgumentNullException("columnName");
            if (type == null) throw new ArgumentNullException("type");
            name = columnName;
            columnType = type;
            IsDirty = false;
        }

        /// <summary>Marks this column dirty (used by the Plan 09-04 cascade).</summary>
        internal void MarkDirty()
        {
            IsDirty = true;
        }
    }
}
