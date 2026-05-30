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

using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UtinniCoreDotNet.Tests.FormatsTests.StringTable
{
    /// <summary>
    /// Synthetic <c>.stf</c> byte builders (no copyrighted real <c>.stf</c> checked in, per Phase 4
    /// CON-O-09). Each builder emits the flat little-endian layout:
    /// magic(0xABCD u32) + version(byte) + nextUniqueId(u32) + count(u32), then <c>count</c> string
    /// entries (id u32 + sourceCrc u32 + charCount u32 + UTF-16LE text), then name entries
    /// (id u32 + length u32 + ASCII name).
    /// </summary>
    public static class StringTableFixtures
    {
        private const uint Magic = 0xABCD;

        private static void WriteHeader(BinaryWriter bw, byte version, uint nextId, uint count)
        {
            bw.Write(Magic);
            bw.Write(version);
            bw.Write(nextId);
            bw.Write(count);
        }

        private static void WriteStringText(BinaryWriter bw, uint id, uint crc, string text)
        {
            text = text ?? string.Empty;
            byte[] t = Encoding.Unicode.GetBytes(text);
            bw.Write(id);
            bw.Write(crc);
            bw.Write((uint)text.Length); // charCount = UTF-16 code units
            bw.Write(t);
        }

        private static void WriteStringRaw(BinaryWriter bw, uint id, uint crc, byte[] utf16Bytes)
        {
            bw.Write(id);
            bw.Write(crc);
            bw.Write((uint)(utf16Bytes.Length / 2)); // charCount = code units
            bw.Write(utf16Bytes);
        }

        private static void WriteName(BinaryWriter bw, uint id, string name)
        {
            byte[] nb = Encoding.ASCII.GetBytes(name);
            bw.Write(id);
            bw.Write((uint)nb.Length);
            bw.Write(nb);
        }

        private static byte[] Build(System.Action<BinaryWriter> body)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                body(bw);
                bw.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>v1, one entry (id 1, crc 100, "Hello"), name "greeting", nextId 2.</summary>
        public static byte[] BuildV1Minimal()
        {
            return Build(bw =>
            {
                WriteHeader(bw, 1, 2, 1);
                WriteStringText(bw, 1, 100, "Hello");
                WriteName(bw, 1, "greeting");
            });
        }

        /// <summary>v1, three canonically-ordered entries (ids 1/2/3, names alpha/beta/gamma), nextId 4.</summary>
        public static byte[] BuildV1MultiEntry()
        {
            return Build(bw =>
            {
                WriteHeader(bw, 1, 4, 3);
                WriteStringText(bw, 1, 11, "first");
                WriteStringText(bw, 2, 22, "second");
                WriteStringText(bw, 3, 33, "third");
                WriteName(bw, 1, "alpha");
                WriteName(bw, 2, "beta");
                WriteName(bw, 3, "gamma");
            });
        }

        /// <summary>v1, a non-ASCII "João" entry, name "player_city", nextId 6.</summary>
        public static byte[] BuildV1Joao()
        {
            return Build(bw =>
            {
                WriteHeader(bw, 1, 6, 1);
                WriteStringText(bw, 5, 7, "João");
                WriteName(bw, 5, "player_city");
            });
        }

        /// <summary>v1, empty table (count 0), nextId 1.</summary>
        public static byte[] BuildEmpty()
        {
            return Build(bw => WriteHeader(bw, 1, 1, 0));
        }

        /// <summary>v1, entries authored OUT of id/name order (for the normalize-on-save test), nextId 4.</summary>
        public static byte[] BuildV1NonCanonicalOrder()
        {
            return Build(bw =>
            {
                WriteHeader(bw, 1, 4, 3);
                // String block authored 3,1,2 (NOT id-ascending).
                WriteStringText(bw, 3, 33, "third");
                WriteStringText(bw, 1, 11, "first");
                WriteStringText(bw, 2, 22, "second");
                // Name block authored mango/apple/zebra (NOT name-ascending).
                WriteName(bw, 2, "mango");
                WriteName(bw, 1, "apple");
                WriteName(bw, 3, "zebra");
            });
        }

        /// <summary>v0 legacy (version byte 0), one entry, name "legacy", nextId 2.</summary>
        public static byte[] BuildV0Legacy()
        {
            return Build(bw =>
            {
                WriteHeader(bw, 0, 2, 1);
                WriteStringText(bw, 1, 100, "Hello");
                WriteName(bw, 1, "legacy");
            });
        }

        /// <summary>v1, one entry whose text is a LONE high surrogate (malformed UTF-16, F4a), name "broken", nextId 10.</summary>
        public static byte[] BuildV1MalformedUtf16()
        {
            return Build(bw =>
            {
                WriteHeader(bw, 1, 10, 1);
                WriteStringRaw(bw, 9, 1, new byte[] { 0x00, 0xD8 }); // U+D800 lone high surrogate
                WriteName(bw, 9, "broken");
            });
        }

        /// <summary>v1, a normal entry + a malformed-UTF-16 entry (for the F2b untouched-survives-edit test), nextId 3.</summary>
        public static byte[] BuildV1MalformedPlusNormal()
        {
            return Build(bw =>
            {
                WriteHeader(bw, 1, 3, 2);
                WriteStringText(bw, 1, 100, "ok");
                WriteStringRaw(bw, 2, 200, new byte[] { 0x00, 0xD8 }); // lone high surrogate
                WriteName(bw, 1, "good");
                WriteName(bw, 2, "zbad");
            });
        }

        /// <summary>v1, two string entries and ZERO name rows (F4a/F4b string-only table), nextId 3.</summary>
        public static byte[] BuildV1NoNameBlock()
        {
            return Build(bw =>
            {
                WriteHeader(bw, 1, 3, 2);
                WriteStringText(bw, 1, 10, "one");
                WriteStringText(bw, 2, 20, "two");
            });
        }

        /// <summary>v1, three string entries but only two name rows (partial name block, F4b), nextId 4.</summary>
        public static byte[] BuildV1PartialNameBlock()
        {
            return Build(bw =>
            {
                WriteHeader(bw, 1, 4, 3);
                WriteStringText(bw, 1, 10, "one");
                WriteStringText(bw, 2, 20, "two");
                WriteStringText(bw, 3, 30, "three");
                WriteName(bw, 1, "first_key");
                WriteName(bw, 3, "third_key"); // id 2 has no name row
            });
        }

        /// <summary>v1, a non-BMP surrogate-pair text entry (U+10437, charCount = 2 code units, F4a), nextId 5.</summary>
        public static byte[] BuildV1NonBmp()
        {
            return Build(bw =>
            {
                WriteHeader(bw, 1, 5, 1);
                WriteStringText(bw, 4, 9, "𐐷"); // U+10437 (Deseret), 2 UTF-16 code units
                WriteName(bw, 4, "deseret");
            });
        }

        // ── On-disk inspection helpers (verify the writer's canonical ordering) ──

        /// <summary>Reads the string-block ids in on-disk order from a serialized <c>.stf</c>.</summary>
        public static List<uint> ReadOnDiskStringIds(byte[] data)
        {
            var ids = new List<uint>();
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                br.ReadUInt32();              // magic
                br.ReadByte();               // version
                br.ReadUInt32();             // nextId
                uint count = br.ReadUInt32();
                for (uint i = 0; i < count; i++)
                {
                    ids.Add(br.ReadUInt32()); // id
                    br.ReadUInt32();          // crc
                    uint charCount = br.ReadUInt32();
                    br.ReadBytes((int)charCount * 2);
                }
            }

            return ids;
        }

        /// <summary>Reads the name-block names in on-disk order from a serialized <c>.stf</c>.</summary>
        public static List<string> ReadOnDiskNames(byte[] data)
        {
            var names = new List<string>();
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                br.ReadUInt32();              // magic
                br.ReadByte();               // version
                br.ReadUInt32();             // nextId
                uint count = br.ReadUInt32();
                for (uint i = 0; i < count; i++)
                {
                    br.ReadUInt32();          // id
                    br.ReadUInt32();          // crc
                    uint charCount = br.ReadUInt32();
                    br.ReadBytes((int)charCount * 2);
                }

                while (ms.Length - ms.Position >= 8)
                {
                    br.ReadUInt32();          // id
                    uint len = br.ReadUInt32();
                    byte[] nb = br.ReadBytes((int)len);
                    names.Add(Encoding.ASCII.GetString(nb));
                }
            }

            return names;
        }
    }
}
