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
    /// Structured exception raised by the Phase 9 typed-DTII reader/parser
    /// (<see cref="DataTableColumnType"/>, <see cref="DataTableDocument"/>) when a <c>.tab</c> payload
    /// (or an in-memory DTII <c>MutableIffDocument</c>) does not conform to the expected datatable
    /// shape or violates a safety constraint.
    ///
    /// <para>Shape mirrors <c>UtinniCoreDotNet.Formats.Iff.IffParseException</c> (the Phase 8
    /// analog) — a sealed exception type carrying a human-readable message and (optionally) an
    /// inner exception. Reachable causes include: unsupported FORM version (not 0000/0001),
    /// missing COLS/TYPE/ROWS chunk, cell-count sanity-cap breach (T-09-01 DoS), unterminated
    /// NUL string, and unknown type-spec discriminator.</para>
    /// </summary>
    [Serializable]
    public sealed class DataTableParseException : Exception
    {
        /// <summary>Creates a new <see cref="DataTableParseException"/> with the given message.</summary>
        public DataTableParseException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Creates a new <see cref="DataTableParseException"/> with the given message and the
        /// inner exception that caused it.
        /// </summary>
        public DataTableParseException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
