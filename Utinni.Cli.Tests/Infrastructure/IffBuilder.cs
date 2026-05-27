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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Utinni.Cli.Tests.Infrastructure
{
    /// <summary>
    /// Builds EA-IFF-85 chunk byte streams for tests. Chunk header = 4-byte ASCII tag + 4-byte
    /// BIG-endian length; container payloads start with a 4-byte sub-type tag; an odd-length child
    /// is followed by a single 0x00 pad byte that the PARENT counts in its length (matching the
    /// IffReader's pad consumption). Payload SCALARS written by the helpers below are LITTLE-endian
    /// (Pitfall 6), the same convention the decoders read.
    ///
    /// <para>These synthesize MINIMAL CONTRACT (smoke) fixtures — they prove a decoder's chunk-layout
    /// contract (COLS/TYPE/ROWS, STF entries, template base+fields). They are NOT real-loader-layout
    /// confidence; the env-gated supplemental tests decode real loose-IFF assets when present.</para>
    /// </summary>
    public static class IffBuilder
    {
        // ── Payload scalar primitives (little-endian) ──────────────────────────

        public static byte[] Int32Le(int v) => new[]
        {
            (byte)(v & 0xFF),
            (byte)((v >> 8) & 0xFF),
            (byte)((v >> 16) & 0xFF),
            (byte)((v >> 24) & 0xFF)
        };

        public static byte[] FloatLe(float v)
        {
            byte[] b = BitConverter.GetBytes(v);
            if (!BitConverter.IsLittleEndian) Array.Reverse(b);
            return b;
        }

        /// <summary>A NUL-terminated string in the given encoding (defaults to ASCII).</summary>
        public static byte[] CString(string s, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.ASCII;
            byte[] text = encoding.GetBytes(s ?? string.Empty);
            byte[] result = new byte[text.Length + 1];
            Array.Copy(text, result, text.Length);
            result[text.Length] = 0; // NUL terminator
            return result;
        }

        public static byte[] Concat(params byte[][] parts)
        {
            using (var ms = new MemoryStream())
            {
                foreach (var p in parts)
                {
                    if (p != null && p.Length > 0) ms.Write(p, 0, p.Length);
                }
                return ms.ToArray();
            }
        }

        // ── Chunk builders ─────────────────────────────────────────────────────

        /// <summary>A leaf chunk: tag(4) + big-endian length(4) + raw payload (no trailing pad).</summary>
        public static byte[] Leaf(string tag, byte[] data)
        {
            data = data ?? new byte[0];
            using (var ms = new MemoryStream())
            {
                ms.Write(Tag4(tag), 0, 4);
                ms.Write(Int32Be(data.Length), 0, 4);
                ms.Write(data, 0, data.Length);
                return ms.ToArray();
            }
        }

        /// <summary>A FORM container with the given sub-type tag and serialized child chunks.</summary>
        public static byte[] Form(string subType, params byte[][] children) =>
            Container("FORM", subType, children);

        /// <summary>
        /// A container ("FORM"/"LIST"/"CAT ") with a sub-type tag and child chunks. Adds a 0x00 pad
        /// byte after any odd-length child (counted in this container's length) so the reader's
        /// even-boundary expectation holds.
        /// </summary>
        public static byte[] Container(string tag, string subType, IEnumerable<byte[]> children)
        {
            using (var payload = new MemoryStream())
            {
                payload.Write(Tag4(subType), 0, 4);
                foreach (var child in children ?? Enumerable.Empty<byte[]>())
                {
                    if (child == null || child.Length == 0) continue;
                    payload.Write(child, 0, child.Length);
                    if (child.Length % 2 == 1) payload.WriteByte(0x00); // EA-IFF-85 pad to even
                }

                byte[] payloadBytes = payload.ToArray();
                using (var ms = new MemoryStream())
                {
                    ms.Write(Tag4(tag), 0, 4);
                    ms.Write(Int32Be(payloadBytes.Length), 0, 4);
                    ms.Write(payloadBytes, 0, payloadBytes.Length);
                    return ms.ToArray();
                }
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        /// <summary>4-byte ASCII tag, space-padded on the right (e.g. "CAT ") or truncated to 4.</summary>
        private static byte[] Tag4(string tag)
        {
            tag = tag ?? string.Empty;
            byte[] b = new byte[4] { 0x20, 0x20, 0x20, 0x20 };
            byte[] ascii = Encoding.ASCII.GetBytes(tag);
            for (int i = 0; i < 4 && i < ascii.Length; i++) b[i] = ascii[i];
            return b;
        }

        private static byte[] Int32Be(int v) => new[]
        {
            (byte)((v >> 24) & 0xFF),
            (byte)((v >> 16) & 0xFF),
            (byte)((v >> 8) & 0xFF),
            (byte)(v & 0xFF)
        };
    }
}
