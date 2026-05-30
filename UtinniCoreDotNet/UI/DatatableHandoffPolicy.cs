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

namespace UtinniCoreDotNet.UI
{
    /// <summary>
    /// Pure decision policy for the TRE Browser → Datatable Editor hand-off (D-10.2). Extracted to the
    /// framework (no WinForms / no TheJawaToolboxDotNet dependency) so the visibility + content gates are
    /// unit-testable, following the <see cref="SingletonFormClosePolicy"/> precedent.
    ///
    /// <para><b>Why this exists:</b> the 09-05 hand-off originally gated the "Open in Datatable Editor"
    /// context-menu item on the source <c>.tab</c> extension only. But compiled datatables inside a TRE
    /// are <c>datatables/**/*.iff</c> (a <c>FORM DTII</c>), not <c>.tab</c> — so the item never appeared
    /// for real TRE browsing. This policy offers the item for <c>.tab</c> OR a <c>.iff</c> under a
    /// <c>datatables/</c> path (the SWG convention), and the content gate (<see cref="IsDtiiPayload"/>)
    /// lets the click handler refuse a non-DTII <c>.iff</c> cleanly instead of throwing.</para>
    /// </summary>
    public static class DatatableHandoffPolicy
    {
        /// <summary>
        /// Whether the TRE Browser should SHOW "Open in Datatable Editor" for a leaf entry. True when the
        /// payload is resolvable (not enumerate-only) AND the entry looks like a datatable carrier: a
        /// <c>.tab</c> source file, or a <c>.iff</c> located under a <c>datatables/</c> path segment.
        /// Visibility is the cheap path-based gate; the actual FORM-DTII content is re-checked on click
        /// via <see cref="IsDtiiPayload"/> (the payload is not resolved at context-menu-open time).
        /// </summary>
        public static bool ShouldOfferDatatableEditor(string logicalPath, bool enumerateOnly)
        {
            if (enumerateOnly) return false;
            if (string.IsNullOrEmpty(logicalPath)) return false;

            string ext = Path.GetExtension(logicalPath);
            if (string.Equals(ext, ".tab", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(ext, ".iff", StringComparison.OrdinalIgnoreCase) && IsUnderDatatablesPath(logicalPath)) return true;
            return false;
        }

        /// <summary>
        /// True iff any path segment equals <c>datatables</c> (case-insensitive), robust to either
        /// <c>/</c> or <c>\</c> separators. Matches the SWG client convention that compiled datatables
        /// live under a <c>datatables/</c> tree (e.g. <c>datatables/mob/creatures.iff</c>).
        /// </summary>
        public static bool IsUnderDatatablesPath(string logicalPath)
        {
            if (string.IsNullOrEmpty(logicalPath)) return false;
            string[] segments = logicalPath.Split('/', '\\');
            for (int i = 0; i < segments.Length; i++)
            {
                if (string.Equals(segments[i], "datatables", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// Cheap content gate: a SWG datatable IFF begins with the top-level chunk <c>FORM</c> + a
        /// big-endian length + the form type <c>DTII</c> — i.e. bytes[0..4] == "FORM" and bytes[8..12] ==
        /// "DTII". Lets the click handler verify a <c>datatables/*.iff</c> is genuinely a datatable and
        /// surface a clean message otherwise, instead of letting <c>DataTableDocument.FromIff</c> throw.
        /// </summary>
        public static bool IsDtiiPayload(byte[] payload)
        {
            if (payload == null || payload.Length < 12) return false;
            return payload[0] == (byte)'F' && payload[1] == (byte)'O' && payload[2] == (byte)'R' && payload[3] == (byte)'M'
                && payload[8] == (byte)'D' && payload[9] == (byte)'T' && payload[10] == (byte)'I' && payload[11] == (byte)'I';
        }
    }
}
