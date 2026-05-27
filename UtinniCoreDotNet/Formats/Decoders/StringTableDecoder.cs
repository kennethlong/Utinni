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
// String-table (.stf) layout understood by reading swg-client-v2
// .../localization/src/shared/{LocalizedStringTable,LocalizedString,LocalizedStringTableReaderWriter}.cpp
// (SOE/Bootprint, All Rights Reserved). The .stf is NOT an IFF container — it is a raw little-endian
// binary: magic(0xABCD, long) + version(byte) + nextUniqueId(u32) + count(u32), then `count` string
// entries (id u32 + crc u32 + charCount u32 + UTF-16LE text), then `count` name entries (id u32 +
// length u32 + ASCII name). Only the on-disk layout was studied — no code, comments, identifier
// names, or test fixtures copied from any reference source. Implementation original to Utinni under MIT.

using System.Collections.Generic;
using System.Text;

namespace UtinniCoreDotNet.Formats.Decoders
{
    /// <summary>One decoded string-table entry: its numeric id, optional symbolic name, and text.</summary>
    public sealed class StfEntry
    {
        public uint Id { get; }
        public string Name { get; }
        public string Text { get; }

        public StfEntry(uint id, string name, string text)
        {
            Id = id;
            Name = name;
            Text = text;
        }
    }

    /// <summary>A decoded .stf string table: its format version and (id, name, text) entries.</summary>
    public sealed class StfTable
    {
        public int Version { get; }
        public IReadOnlyList<StfEntry> Entries { get; }

        public StfTable(int version, IReadOnlyList<StfEntry> entries)
        {
            Version = version;
            Entries = entries;
        }
    }

    /// <summary>
    /// Decodes a SWG localized string table (.stf) from raw bytes into (id, name, text) entries.
    ///
    /// <para>Unlike the datatable/object-template decoders, the .stf is NOT IFF — so this decoder
    /// consumes the raw payload <see cref="byte"/>[] directly (the TRE Browser detail pane and the
    /// decode-iff verb both pass the resolved bytes). Text is UTF-16LE, so non-ASCII characters
    /// round-trip; malformed code units are replaced (U+FFFD) rather than throwing (T-07-16).</para>
    ///
    /// <para><b>Parser purity:</b> NO JSON, NO console output, NO file writes.</para>
    /// </summary>
    public static class StringTableDecoder
    {
        /// <summary>The .stf magic (LocalizedStringTable::ms_MAGIC), stored as a little-endian long.</summary>
        public const long StfMagic = 0xABCD;

        /// <summary>True when <paramref name="data"/> begins with the .stf magic (cheap sniff for dispatch).</summary>
        public static bool LooksLikeStf(byte[] data)
        {
            if (data == null || data.Length < 5) return false;
            // 0xABCD as a little-endian 32-bit value: CD AB 00 00.
            return data[0] == 0xCD && data[1] == 0xAB && data[2] == 0x00 && data[3] == 0x00;
        }

        /// <summary>
        /// Decodes the string table from <paramref name="data"/>.
        /// Throws <see cref="DecoderException"/> on a structural/format problem.
        /// </summary>
        public static StfTable Decode(byte[] data)
        {
            var cursor = new IffPayloadCursor(data ?? new byte[0]);

            long magic = cursor.ReadUInt32Le();
            if (magic != StfMagic)
            {
                throw new DecoderException(DecoderError.UnexpectedForm,
                    "Not a string table: magic 0x" + magic.ToString("X") + " != 0x" + StfMagic.ToString("X") + ".");
            }

            int version = cursor.ReadBytes(1)[0];
            if (version != 0 && version != 1)
            {
                throw new DecoderException(DecoderError.UnsupportedVersion,
                    "Unsupported .stf version " + version + " (Phase 7 supports 0 and 1).");
            }

            cursor.ReadUInt32Le();              // nextUniqueId — not needed for display
            long count = cursor.ReadUInt32Le(); // number of string entries

            if (count < 0)
            {
                throw new DecoderException(DecoderError.NegativeCount, "Entry count is negative: " + count + ".");
            }
            // Forged-count guard: each string-table entry is at least 12 bytes on disk
            // (id u32 + crc u32 + charCount u32, text optionally empty). Reject before looping.
            const int MinStringEntryBytes = 12;
            if (count > (long)cursor.Remaining / MinStringEntryBytes)
            {
                throw new DecoderException(DecoderError.CountExceedsCap,
                    "Entry count " + count + " exceeds what " + cursor.Remaining + " remaining byte(s) can hold.");
            }

            // ── String table: count entries of (id, crc, charCount, UTF-16LE text) ──
            var idToText = new List<KeyValuePair<uint, string>>((int)count);
            var encoding = Encoding.Unicode; // UTF-16LE, replacement fallback (does not throw)
            for (long i = 0; i < count; i++)
            {
                uint id = (uint)cursor.ReadUInt32Le();
                cursor.ReadUInt32Le(); // sourceCrc — not needed for display
                long charCount = cursor.ReadUInt32Le();
                if (charCount < 0 || charCount > (long)cursor.Remaining / 2)
                {
                    throw new DecoderException(DecoderError.CountExceedsCap,
                        "String length " + charCount + " for id " + id + " exceeds the remaining bytes.");
                }
                byte[] textBytes = cursor.ReadBytes((int)charCount * 2);
                idToText.Add(new KeyValuePair<uint, string>(id, encoding.GetString(textBytes)));
            }

            // ── Name table: count entries of (id, length, ASCII name) ──
            var idToName = new Dictionary<uint, string>();
            for (long i = 0; i < count; i++)
            {
                if (cursor.Remaining < 8) break; // tolerate a string-only table with no name block
                uint id = (uint)cursor.ReadUInt32Le();
                long nameLen = cursor.ReadUInt32Le();
                if (nameLen < 0 || nameLen > cursor.Remaining)
                {
                    throw new DecoderException(DecoderError.CountExceedsCap,
                        "Name length " + nameLen + " for id " + id + " exceeds the remaining bytes.");
                }
                byte[] nameBytes = cursor.ReadBytes((int)nameLen);
                idToName[id] = Encoding.ASCII.GetString(nameBytes);
            }

            var entries = new List<StfEntry>(idToText.Count);
            foreach (var kv in idToText)
            {
                idToName.TryGetValue(kv.Key, out string name);
                entries.Add(new StfEntry(kv.Key, name, kv.Value));
            }

            return new StfTable(version, entries);
        }
    }
}
