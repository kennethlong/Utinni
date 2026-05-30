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
// PO/gettext EXPORT for the string-table editor (Phase 10 D-03d). Export-only — PO import is NOT V1.
// Hand-rolled (no gettext dependency): one msgid/msgstr pair per entry (msgid = key, msgstr = text),
// gettext string-escaped. Pure-managed BCL-only; the TJT-side serializer writes the returned string as
// UTF-8 so non-ASCII text (e.g. João) survives.

using System.Text;

namespace UtinniCoreDotNet.Formats.StringTable
{
    /// <summary>
    /// Minimal PO/gettext exporter: <c>msgid "{key}"</c> + <c>msgstr "{text}"</c> per entry, gettext-
    /// escaped, blank-line separated. Export-only (D-03d — the lowest-priority bulk feature; PO import is
    /// explicitly not V1).
    /// </summary>
    public static class StringTablePoExport
    {
        /// <summary>
        /// Renders <paramref name="doc"/> to a minimal <c>.po</c> string. Emits a one-line header comment,
        /// then for each entry that has a name a <c>msgid</c>/<c>msgstr</c> pair (gettext-escaped) with a
        /// blank line between entries. The caller writes the result as UTF-8.
        /// </summary>
        public static string ToPo(MutableStringTableDocument doc)
        {
            if (doc == null) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("# Exported by Utinni — The Jawa Toolbox string-table editor.\n\n");

            foreach (MutableStringTableEntry entry in doc.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue; // a nameless entry has no msgid key.
                sb.Append("msgid \"").Append(Escape(entry.Name)).Append("\"\n");
                sb.Append("msgstr \"").Append(Escape(entry.Text)).Append("\"\n");
                sb.Append('\n');
            }

            return sb.ToString();
        }

        // gettext string escaping: backslash, double-quote, and the control chars that cannot sit raw
        // inside a quoted PO string (newline / carriage-return / tab).
        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }

            return sb.ToString();
        }
    }
}
