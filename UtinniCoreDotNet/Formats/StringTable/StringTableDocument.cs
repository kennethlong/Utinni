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
// String-table (.stf) format understood by reading swg-client-v2
// .../localization/src/shared/{LocalizedStringTable,LocalizedString,LocalizedStringTableReaderWriter}.cpp
// (SOE/Bootprint, All Rights Reserved). The .stf is NOT an IFF container — it is a raw little-endian
// binary: magic(0xABCD, long) + version(byte) + nextUniqueId(u32) + count(u32), then `count` string
// entries (id u32 + sourceCrc u32 + charCount u32 + UTF-16LE text), then up to `count` name entries
// (id u32 + length u32 + ASCII name). Only the on-disk layout was studied — no code, comments,
// identifier names, or test fixtures copied from any reference source. Original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.Text;
using UtinniCoreDotNet.Formats.Decoders;

namespace UtinniCoreDotNet.Formats.StringTable
{
    /// <summary>
    /// Flat-binary <c>.stf</c> reader (GREENFIELD — there is no existing flat-binary <c>.stf</c>
    /// reader; the Phase 7 <see cref="StringTableDecoder"/> is a read-only immutable decoder reused
    /// here only as the proven byte-layout reference). <see cref="FromBytes"/> parses raw bytes into a
    /// mutable model, capturing the FULL original whole-file bytes (for the F2a zero-dirty short-circuit)
    /// AND each entry's original string-block slice (for byte-exact re-emit of untouched entries,
    /// including malformed-UTF-16 per F2b).
    /// </summary>
    public sealed class StringTableDocument
    {
        /// <summary>The .stf magic (LocalizedStringTable::ms_MAGIC), a little-endian 32-bit value.</summary>
        public const long StfMagic = 0xABCD;

        private readonly List<string> warnings;

        /// <summary>The parsed format version (0 or 1).</summary>
        public int Version { get; }

        /// <summary>The engine's <c>m_nextUniqueId</c> auto-id high-water mark.</summary>
        public uint NextUniqueId { get { return Mutable.NextUniqueId; } }

        /// <summary>The mutable model (the editable surface + writer input).</summary>
        public MutableStringTableDocument Mutable { get; }

        /// <summary>
        /// Non-fatal load warnings (F9). A name-less entry (no matching name row) is tolerated by the
        /// decoder but surfaced here as a warning — "a save will normalize it" — rather than silently
        /// exposing an id-only row.
        /// </summary>
        public IReadOnlyList<string> Warnings { get { return warnings; } }

        private StringTableDocument(int version, MutableStringTableDocument mutable, List<string> warnings)
        {
            Version = version;
            Mutable = mutable;
            this.warnings = warnings;
        }

        /// <summary>
        /// Parses a flat-binary <c>.stf</c> from <paramref name="data"/>. Throws
        /// <see cref="StringTableParseException"/> on a structural/format problem (bad magic,
        /// unsupported version, truncation, forged count). Malformed UTF-16 text is replaced with
        /// U+FFFD rather than throwing (STAB-03) — but an UNTOUCHED entry still re-emits byte-exact via
        /// its captured original slice (F2b).
        /// </summary>
        public static StringTableDocument FromBytes(byte[] data)
        {
            byte[] original = data ?? new byte[0];

            // Minimal header is magic(4) + version(1) + nextUniqueId(4) + count(4) = 13 bytes. Guard up
            // front so a truncated/empty buffer surfaces as a StringTableParseException rather than the
            // low-level cursor's DecoderException.
            if (original.Length < 13)
            {
                throw new StringTableParseException(
                    "Truncated .stf: need at least a 13-byte header, got " + original.Length + " byte(s).");
            }

            var cursor = new IffPayloadCursor(original);

            long magic = cursor.ReadUInt32Le();
            if (magic != StfMagic)
            {
                throw new StringTableParseException(
                    "Not a string table: magic 0x" + magic.ToString("X") + " != 0x" + StfMagic.ToString("X") + ".");
            }

            int version = cursor.ReadBytes(1)[0];
            if (version != 0 && version != 1)
            {
                throw new StringTableParseException("Unsupported .stf version " + version + " (supported: 0 and 1).");
            }

            long nextUniqueId = cursor.ReadUInt32Le();
            long count = cursor.ReadUInt32Le();

            // Forged-count guard: each string entry is at least 12 bytes (id + sourceCrc + charCount,
            // text optionally empty). Reject before allocating per-entry.
            const int MinStringEntryBytes = 12;
            if (count > (long)cursor.Remaining / MinStringEntryBytes)
            {
                throw new StringTableParseException(
                    "Entry count " + count + " exceeds what " + cursor.Remaining + " remaining byte(s) can hold.");
            }

            var enc = Encoding.Unicode; // UTF-16LE, replacement fallback (does not throw)
            var entries = new List<MutableStringTableEntry>((int)count);

            for (long i = 0; i < count; i++)
            {
                int start = original.Length - cursor.Remaining;
                uint id = (uint)cursor.ReadUInt32Le();
                uint sourceCrc = (uint)cursor.ReadUInt32Le();
                long charCount = cursor.ReadUInt32Le();
                if (charCount < 0 || charCount > (long)cursor.Remaining / 2)
                {
                    throw new StringTableParseException(
                        "String length " + charCount + " for id " + id + " exceeds the remaining bytes.");
                }

                byte[] textBytes = cursor.ReadBytes((int)charCount * 2);
                string text = enc.GetString(textBytes);

                int end = original.Length - cursor.Remaining;
                byte[] slice = new byte[end - start];
                Array.Copy(original, start, slice, 0, slice.Length);

                entries.Add(new MutableStringTableEntry(id, sourceCrc, text, slice));
            }

            // Name block: up to `count` rows of (id, length, ASCII name). Tolerate a string-only table
            // (no name block) and a partial name block (fewer rows than entries) — mirror the decoder.
            var idToName = new Dictionary<uint, string>();
            while (cursor.Remaining >= 8)
            {
                uint id = (uint)cursor.ReadUInt32Le();
                long nameLen = cursor.ReadUInt32Le();
                if (nameLen < 0 || nameLen > cursor.Remaining)
                {
                    throw new StringTableParseException(
                        "Name length " + nameLen + " for id " + id + " exceeds the remaining bytes.");
                }

                byte[] nameBytes = cursor.ReadBytes((int)nameLen);
                idToName[id] = Encoding.ASCII.GetString(nameBytes);
            }

            int nameless = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                string name;
                if (idToName.TryGetValue(entries[i].Id, out name))
                {
                    entries[i].SetLoadedName(name);
                }
                else
                {
                    nameless++;
                }
            }

            var warnings = new List<string>();
            if (nameless > 0)
            {
                warnings.Add(nameless + " entry(ies) have no name — this string table is corrupt; a save will normalize it.");
            }

            var mutable = new MutableStringTableDocument(version, (uint)nextUniqueId, entries);
            byte[] originalCopy = new byte[original.Length];
            Array.Copy(original, originalCopy, original.Length);
            mutable.SetOriginalFileBytes(originalCopy);

            return new StringTableDocument(version, mutable, warnings);
        }
    }
}
