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
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.Datatable
{
    /// <summary>
    /// Serializes a <see cref="MutableDataTableDocument"/> to DTII bytes by composing on the Phase 8
    /// <see cref="IffWriter.Write(MutableIffDocument)"/> primitive. This class does NO chunk framing,
    /// BE/LE plumbing, or 64 MB cap logic of its own — it builds the FORM tree via
    /// <see cref="MutableDataTableDocument.BuildMutableIff"/> and hands it to the existing writer
    /// (which inherits the per-chunk 64 MB cap, T-09-02).
    ///
    /// <para><b>D-09 view-only-sort defense:</b> the row iteration lives in
    /// <c>MutableDataTableDocument.BuildMutableIff</c> over the in-memory <c>Rows</c> collection —
    /// never a UI binding view. This file has no WinForms dependency.</para>
    /// </summary>
    public sealed class DataTableWriter
    {
        private readonly MutableDataTableDocument doc;

        /// <summary>Creates a writer bound to the given document.</summary>
        public DataTableWriter(MutableDataTableDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");
            doc = document;
        }

        /// <summary>
        /// Builds the intermediate FORM DTII tree and serializes it to bytes via
        /// <see cref="IffWriter.Write(MutableIffDocument)"/>.
        /// </summary>
        public byte[] Serialize()
        {
            MutableIffDocument mIff = doc.BuildMutableIff();
            return IffWriter.Write(mIff);
        }

        /// <summary>
        /// Exposes the intermediate <see cref="MutableIffDocument"/> tree so Plan 09-05's save targets
        /// can pass it directly to Phase 8's <c>IffSaveTargets</c> (which take a
        /// <see cref="MutableIffDocument"/>, not raw bytes) without a byte round-trip.
        /// </summary>
        public MutableIffDocument BuildMutableIff()
        {
            return doc.BuildMutableIff();
        }
    }
}
