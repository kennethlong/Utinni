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
// String-table (.stf) format understood by reading swg-client-v2 localization sources (SOE/Bootprint,
// All Rights Reserved). No code/comments/identifiers/fixtures copied. Implementation original to Utinni.

namespace UtinniCoreDotNet.Formats.StringTable
{
    /// <summary>
    /// Immutable read snapshot of one string-table entry: its numeric <see cref="Id"/>
    /// (machine-managed), symbolic <see cref="Name"/> (key, may be null for a name-less row),
    /// <see cref="Text"/> (UTF-16), and <see cref="SourceCrc"/> (a timestamp the engine sets via
    /// <c>int(time(0))</c> — NOT a content hash; see <c>LocalizedString.cpp</c>).
    ///
    /// <para>This is the <see cref="StringTableDocument.FromBytes"/> read-side DTO. The editable
    /// surface is <see cref="MutableStringTableEntry"/>.</para>
    /// </summary>
    public sealed class StringTableEntry
    {
        /// <summary>The entry's numeric id (machine-managed lookup key).</summary>
        public uint Id { get; }

        /// <summary>The entry's symbolic name (the resolve key), or null for a name-less row.</summary>
        public string Name { get; }

        /// <summary>The entry's localized text (UTF-16).</summary>
        public string Text { get; }

        /// <summary>The entry's source CRC (an <c>int(time(0))</c> timestamp, not a content hash).</summary>
        public uint SourceCrc { get; }

        /// <summary>Creates an immutable entry snapshot.</summary>
        public StringTableEntry(uint id, string name, string text, uint sourceCrc)
        {
            Id = id;
            Name = name;
            Text = text;
            SourceCrc = sourceCrc;
        }
    }
}
