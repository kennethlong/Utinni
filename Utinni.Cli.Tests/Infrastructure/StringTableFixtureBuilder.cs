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

using System.IO;
using System.Text;

namespace Utinni.Cli.Tests.Infrastructure
{
    /// <summary>
    /// CLI-side synthetic <c>.stf</c> byte builders — a DUPLICATE of
    /// <c>UtinniCoreDotNet.Tests.FormatsTests.StringTable.StringTableFixtures</c> (Plan 10-01).
    ///
    /// <para><b>Why a duplicate, not a reference:</b> xUnit test projects do NOT ProjectReference each
    /// other, so the CLI golden suite cannot reach the framework test project's builder. The duplication
    /// is safe because both builders emit the raw flat <c>.stf</c> layout directly (no shared logic to
    /// drift), and the non-optional drift-detector [Fact] in RoundtripStfCommandTests proves this
    /// builder still emits a CANONICAL round-trippable fixture. Mirrors the Phase 9 DataTableFixtureBuilder
    /// posture.</para>
    /// </summary>
    public static class StringTableFixtureBuilder
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
            bw.Write((uint)text.Length);
            bw.Write(t);
        }

        private static void WriteStringRaw(BinaryWriter bw, uint id, uint crc, byte[] utf16Bytes)
        {
            bw.Write(id);
            bw.Write(crc);
            bw.Write((uint)(utf16Bytes.Length / 2));
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

        /// <summary>v1, one entry (id 1, "Hello"), name "greeting", nextId 2.</summary>
        public static byte[] BuildV1Minimal()
        {
            return Build(bw =>
            {
                WriteHeader(bw, 1, 2, 1);
                WriteStringText(bw, 1, 100, "Hello");
                WriteName(bw, 1, "greeting");
            });
        }

        /// <summary>v1, three canonically-ordered entries (alpha/beta/gamma), nextId 4.</summary>
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

        /// <summary>v1, entries authored OUT of id/name order (normalize-on-save), nextId 4.</summary>
        public static byte[] BuildV1NonCanonicalOrder()
        {
            return Build(bw =>
            {
                WriteHeader(bw, 1, 4, 3);
                WriteStringText(bw, 3, 33, "third");
                WriteStringText(bw, 1, 11, "first");
                WriteStringText(bw, 2, 22, "second");
                WriteName(bw, 2, "mango");
                WriteName(bw, 1, "apple");
                WriteName(bw, 3, "zebra");
            });
        }

        /// <summary>v1, one entry whose text is a LONE high surrogate (malformed UTF-16), name "broken", nextId 10.</summary>
        public static byte[] BuildV1MalformedUtf16()
        {
            return Build(bw =>
            {
                WriteHeader(bw, 1, 10, 1);
                WriteStringRaw(bw, 9, 1, new byte[] { 0x00, 0xD8 });
                WriteName(bw, 9, "broken");
            });
        }

        /// <summary>v1, two string entries and ZERO name rows (string-only table), nextId 3.</summary>
        public static byte[] BuildV1NoNameBlock()
        {
            return Build(bw =>
            {
                WriteHeader(bw, 1, 3, 2);
                WriteStringText(bw, 1, 10, "one");
                WriteStringText(bw, 2, 20, "two");
            });
        }

        /// <summary>v1, three string entries but only two name rows (partial name block), nextId 4.</summary>
        public static byte[] BuildV1PartialNameBlock()
        {
            return Build(bw =>
            {
                WriteHeader(bw, 1, 4, 3);
                WriteStringText(bw, 1, 10, "one");
                WriteStringText(bw, 2, 20, "two");
                WriteStringText(bw, 3, 30, "three");
                WriteName(bw, 1, "first_key");
                WriteName(bw, 3, "third_key");
            });
        }

        /// <summary>v1, a non-BMP surrogate-pair text entry (U+10437, 2 code units), nextId 5.</summary>
        public static byte[] BuildV1NonBmp()
        {
            return Build(bw =>
            {
                WriteHeader(bw, 1, 5, 1);
                WriteStringText(bw, 4, 9, "𐐷");
                WriteName(bw, 4, "deseret");
            });
        }
    }
}
