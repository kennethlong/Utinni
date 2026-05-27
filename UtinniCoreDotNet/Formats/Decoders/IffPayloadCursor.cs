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
// Format understood by reading the swg-client-v2 engine loaders (SOE/Bootprint, All Rights
// Reserved). Per Pitfall 6: IFF chunk tags+lengths are big-endian, but chunk PAYLOAD scalars are
// little-endian on the original Win32 client. The shared Formats/Iff reader already exposes leaf
// payloads as raw bytes; this cursor reads those payload scalars little-endian with bounds checks.
// No code or identifiers copied from any reference source. Implementation original to Utinni under MIT.

using System;
using System.Text;

namespace UtinniCoreDotNet.Formats.Decoders
{
    /// <summary>
    /// A forward-only, bounds-checked reader over an <see cref="Formats.Iff.IffLeafChunk"/> payload.
    /// All multi-byte scalars are read LITTLE-endian (Pitfall 6); a read that would run past the end
    /// of the payload throws <see cref="DecoderException"/> with <see cref="DecoderError.Truncated"/>
    /// rather than reading out of bounds. The cursor never allocates based on attacker-controlled
    /// counts — callers bound counts with the division-form guard before looping reads.
    /// </summary>
    internal sealed class IffPayloadCursor
    {
        private readonly byte[] _data;
        private int _pos;

        public IffPayloadCursor(byte[] data)
        {
            _data = data ?? new byte[0];
            _pos = 0;
        }

        /// <summary>Bytes remaining between the cursor and the end of the payload.</summary>
        public int Remaining => _data.Length - _pos;

        /// <summary>Total payload length in bytes.</summary>
        public int Length => _data.Length;

        /// <summary>Reads a 32-bit little-endian signed integer, advancing 4 bytes.</summary>
        public int ReadInt32Le()
        {
            Need(4);
            int v = _data[_pos]
                  | (_data[_pos + 1] << 8)
                  | (_data[_pos + 2] << 16)
                  | (_data[_pos + 3] << 24);
            _pos += 4;
            return v;
        }

        /// <summary>Reads a 32-bit little-endian IEEE-754 float, advancing 4 bytes.</summary>
        public float ReadFloatLe()
        {
            Need(4);
            // Assemble host-independently: copy the 4 payload bytes in LE order, then let
            // BitConverter interpret them (reversing first on a big-endian host).
            byte[] four = new byte[4];
            four[0] = _data[_pos];
            four[1] = _data[_pos + 1];
            four[2] = _data[_pos + 2];
            four[3] = _data[_pos + 3];
            _pos += 4;
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(four);
            }
            return BitConverter.ToSingle(four, 0);
        }

        /// <summary>
        /// Reads a NUL-terminated string in the given encoding (the on-disk C-string idiom the engine
        /// loaders use), advancing past the terminator. Throws <see cref="DecoderError.Truncated"/> if
        /// no terminator appears before the end of the payload.
        /// </summary>
        public string ReadCString(Encoding encoding)
        {
            int start = _pos;
            while (_pos < _data.Length && _data[_pos] != 0)
            {
                _pos++;
            }

            if (_pos >= _data.Length)
            {
                throw new DecoderException(DecoderError.Truncated,
                    "Unterminated string at payload offset " + start + " (no NUL before end of chunk).");
            }

            string s = encoding.GetString(_data, start, _pos - start);
            _pos++; // consume the NUL terminator
            return s;
        }

        private void Need(int n)
        {
            if (n < 0 || _pos + n > _data.Length)
            {
                throw new DecoderException(DecoderError.Truncated,
                    "Short read: need " + n + " byte(s) at payload offset " + _pos
                    + " but only " + Remaining + " remain.");
            }
        }
    }
}
