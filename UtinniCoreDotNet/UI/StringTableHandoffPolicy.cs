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
using System.IO;
using UtinniCoreDotNet.Formats.Decoders;

namespace UtinniCoreDotNet.UI
{
    /// <summary>
    /// Pure decision policy for the TRE Browser → String-table Editor hand-off (Phase 10 D-04). The
    /// flat-format analog of <see cref="DatatableHandoffPolicy"/>, extracted to the framework (no WinForms
    /// / no TheJawaToolboxDotNet dependency) so the visibility gate is unit-testable.
    ///
    /// <para><b>Simpler than the datatable gate:</b> there is NO <c>datatables/</c> path rule (that was a
    /// datatable-specific convention) and NO IFF-Editor hand-off (<c>.stf</c> is not IFF — D-04). The gate
    /// is: extension <c>.stf</c> OR the <c>0xABCD</c> magic sniff
    /// (<see cref="StringTableDecoder.LooksLikeStf"/>), AND the entry is resolvable (not enumerate-only).</para>
    ///
    /// <para><b>Payload availability (F11):</b> the FormTreBrowser context-menu Opening event resolves the
    /// payload lazily (only on click, mirroring the datatable hand-off), so the live menu gate calls this
    /// with <c>payloadOrNull == null</c> and relies on the extension branch. The magic-sniff branch is a
    /// SECONDARY affordance — exercised in isolation by the unit tests for extension-less <c>.stf</c>
    /// entries where a payload IS available — never the sole live gate.</para>
    /// </summary>
    public static class StringTableHandoffPolicy
    {
        /// <summary>
        /// Whether the TRE Browser should SHOW "Open in String-table Editor" for a leaf entry. True when
        /// the entry is resolvable (not enumerate-only) AND it looks like a string table: a <c>.stf</c>
        /// extension, OR (when a payload is available) the <c>0xABCD</c> magic sniff succeeds.
        /// </summary>
        public static bool ShouldOfferStringTableEditor(string logicalPath, byte[] payloadOrNull, bool enumerateOnly)
        {
            if (enumerateOnly) return false;

            if (!string.IsNullOrEmpty(logicalPath))
            {
                string ext = Path.GetExtension(logicalPath);
                if (string.Equals(ext, ".stf", StringComparison.OrdinalIgnoreCase)) return true;
            }

            if (payloadOrNull != null && StringTableDecoder.LooksLikeStf(payloadOrNull)) return true;

            return false;
        }
    }
}
