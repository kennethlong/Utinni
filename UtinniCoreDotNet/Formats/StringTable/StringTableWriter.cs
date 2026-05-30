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
// String-table (.stf) writer. The flat format does NOT get the Phase 8/9 IFF hybrid-DOM
// "byte-exact-on-untouched for free" mechanism, so byte-exactness is engineered EXPLICITLY here:
// the F2a zero-dirty short-circuit returns the captured original bytes verbatim, and untouched
// entries re-emit their captured string-block slice. Write framing mirrors SOE
// LocalizedStringTableReaderWriter.cpp:107-203 (header + string block id-ascending + name block
// name-ascending). DELIBERATELY does NOT port `unfubarMicrosoftInvalidTextCharacters` (the SOE
// smart-quote rewrite) — Utinni preserves text byte-faithfully (D-02 / SC4 / STAB-03). No code,
// comments, identifiers, or fixtures copied from any reference source. Original to Utinni under MIT.

using System;
using System.IO;
using System.Linq;
using System.Text;

namespace UtinniCoreDotNet.Formats.StringTable
{
    /// <summary>
    /// Serializes a <see cref="MutableStringTableDocument"/> to flat <c>.stf</c> bytes with EXPLICIT
    /// byte-exactness (no IffWriter composition).
    /// </summary>
    public static class StringTableWriter
    {
        /// <summary>
        /// Serializes <paramref name="doc"/> to <c>.stf</c> bytes.
        ///
        /// <para><b>F2a zero-dirty short-circuit:</b> when no entry is dirty AND none is added, the
        /// captured ORIGINAL whole-file bytes are returned verbatim (the unconditional byte-exact
        /// no-edit path; neutralizes the unverified canonical-ordering assumption for the no-mutate
        /// case). Otherwise the table is re-serialized in canonical order.</para>
        /// </summary>
        public static byte[] Serialize(MutableStringTableDocument doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");

            // F2a: nothing changed → emit the exact original file bytes.
            if (!doc.AnyDirtyOrAdded() && doc.OriginalFileBytes != null)
            {
                return (byte[])doc.OriginalFileBytes.Clone();
            }

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((uint)StringTableDocument.StfMagic); // 0xABCD as u32 LE -> CD AB 00 00
                bw.Write((byte)doc.Version);
                bw.Write(doc.NextUniqueId);
                bw.Write((uint)doc.Entries.Count);

                // String block: id-ascending (std::map<id,...> order). Untouched entries re-emit their
                // captured original slice verbatim (F2b — malformed UTF-16 survives); dirty/added
                // entries re-encode fresh (UTF-16LE, NO unfubar/smart-quote rewrite).
                foreach (var e in doc.Entries.OrderBy(e => e.Id))
                {
                    if (!e.IsDirty && e.OriginalStringBytes != null)
                    {
                        bw.Write(e.OriginalStringBytes);
                    }
                    else
                    {
                        bw.Write(e.SerializeStringBlock());
                    }
                }

                // Name block: name-ascending ORDINAL (std::map<std::string,...> byte order). Emit a row
                // only for entries that HAVE a name (F4b). A culture-aware sort here would break
                // byte-exactness for mixed-case keys.
                foreach (var e in doc.Entries
                             .Where(e => !string.IsNullOrEmpty(e.Name))
                             .OrderBy(e => e.Name, StringComparer.Ordinal))
                {
                    byte[] nameBytes = Encoding.ASCII.GetBytes(e.Name);
                    bw.Write(e.Id);
                    bw.Write((uint)nameBytes.Length);
                    bw.Write(nameBytes);
                }

                bw.Flush();
                return ms.ToArray();
            }
        }
    }
}
