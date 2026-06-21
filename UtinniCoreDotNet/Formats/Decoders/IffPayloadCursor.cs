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
    /// A forward-only, bounds-checked reader over a byte buffer — an
    /// <see cref="Formats.Iff.IffLeafChunk"/> payload (datatable/object-template) or a whole raw
    /// asset (the .stf string table, which is not IFF-wrapped). All multi-byte scalars are read
    /// LITTLE-endian (Pitfall 6); a read that would run past the end of the buffer throws
    /// <see cref="DecoderException"/> with <see cref="DecoderError.Truncated"/> rather than reading
    /// out of bounds. The cursor never allocates based on attacker-controlled counts — callers bound
    /// counts with the division-form guard before looping reads.
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

        /// <summary>
        /// Reads a 32-bit little-endian UNSIGNED integer as a <see cref="long"/> (0 .. 4294967295),
        /// advancing 4 bytes. Used for the .stf id/count fields, which are <c>unsigned long</c> on
        /// disk — returning a long keeps bound arithmetic safe from int overflow.
        /// </summary>
        public long ReadUInt32Le()
        {
            Need(4);
            long v = (long)((uint)_data[_pos]
                  | ((uint)_data[_pos + 1] << 8)
                  | ((uint)_data[_pos + 2] << 16)
                  | ((uint)_data[_pos + 3] << 24));
            _pos += 4;
            return v;
        }

        /// <summary>
        /// Reads a single byte, advancing 1. The 1-byte primitive the object-template self-describing
        /// decode needs for the data-type tag and the numeric delta-type byte (Phase 11). Throws
        /// <see cref="DecoderError.Truncated"/> at end-of-payload.
        /// </summary>
        public byte ReadInt8()
        {
            Need(1);
            byte v = _data[_pos];
            _pos += 1;
            return v;
        }

        /// <summary>Reads <paramref name="count"/> raw bytes, advancing past them.</summary>
        public byte[] ReadBytes(int count)
        {
            Need(count);
            byte[] result = new byte[count];
            System.Array.Copy(_data, _pos, result, 0, count);
            _pos += count;
            return result;
        }

        /// <summary>
        /// Reads a 16-bit little-endian SIGNED integer as an <see cref="int"/>, advancing 2 bytes. A
        /// kernel primitive (i16). Returning an int keeps the sign-extension explicit and overflow-safe.
        /// Throws <see cref="DecoderError.Truncated"/> at end-of-payload.
        /// </summary>
        public int ReadInt16Le()
        {
            Need(2);
            short v = (short)(_data[_pos] | (_data[_pos + 1] << 8));
            _pos += 2;
            return v;
        }

        /// <summary>
        /// Reads a 16-bit little-endian UNSIGNED integer as an <see cref="int"/> (0 .. 65535), advancing
        /// 2 bytes. A kernel primitive (u16). Returning an int keeps it overflow-safe (a ushort would
        /// silently wrap on arithmetic). Throws <see cref="DecoderError.Truncated"/> at end-of-payload.
        /// </summary>
        public int ReadUInt16Le()
        {
            Need(2);
            int v = _data[_pos] | (_data[_pos + 1] << 8);
            _pos += 2;
            return v;
        }

        /// <summary>
        /// Reads a 64-bit little-endian IEEE-754 double, advancing 8 bytes. The kernel f64 primitive (a
        /// kernel addition over the SOE .tdf type set, which has no 64-bit float). Assembled
        /// host-independently like <see cref="ReadFloatLe"/>. Throws <see cref="DecoderError.Truncated"/>
        /// at end-of-payload.
        /// </summary>
        public double ReadDoubleLe()
        {
            Need(8);
            byte[] eight = new byte[8];
            for (int i = 0; i < 8; i++)
            {
                eight[i] = _data[_pos + i];
            }
            _pos += 8;
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(eight);
            }
            return BitConverter.ToDouble(eight, 0);
        }

        /// <summary>
        /// Reads a fixed-width char[n] field, advancing exactly <paramref name="n"/> bytes. The kernel
        /// FixedChar primitive. NOTE the asymmetry: the returned DISPLAY string is trimmed at the first
        /// NUL (the conventional fixed-char display form), but the cursor still consumes the full
        /// <paramref name="n"/>-byte span — the encoder must re-emit the full n-byte field (NUL pad on
        /// the right) to round-trip byte-exact, so the codec keeps the raw n-byte span on encode rather
        /// than re-deriving it from the trimmed string. Throws <see cref="DecoderError.Truncated"/> if
        /// fewer than n bytes remain.
        /// </summary>
        public string ReadFixedChar(int n, Encoding encoding)
        {
            Need(n);
            int len = 0;
            while (len < n && _data[_pos + len] != 0)
            {
                len++;
            }
            string s = encoding.GetString(_data, _pos, len);
            _pos += n; // consume the FULL fixed width, not just the trimmed prefix
            return s;
        }

        /// <summary>
        /// Reads <paramref name="n"/> raw bytes (the kernel RawBytes primitive), advancing past them.
        /// A bounds-checked alias of <see cref="ReadBytes"/> kept as a distinct kernel-vocabulary name so
        /// the codec's intent (opaque verbatim span) reads clearly. Throws
        /// <see cref="DecoderError.Truncated"/> at end-of-payload.
        /// </summary>
        public byte[] ReadRawBytes(int n)
        {
            return ReadBytes(n);
        }

        /// <summary>
        /// Skips <paramref name="n"/> padding bytes (the kernel Pad primitive), advancing the cursor
        /// without returning them. The codec round-trips a Pad span by re-emitting the captured raw bytes
        /// on encode (so it must capture, not discard, them — Pad fields carry their raw span in the
        /// decoded value). This skip is the DECODE-position advance only. Throws
        /// <see cref="DecoderError.Truncated"/> at end-of-payload.
        /// </summary>
        public void SkipPad(int n)
        {
            Need(n);
            _pos += n;
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
