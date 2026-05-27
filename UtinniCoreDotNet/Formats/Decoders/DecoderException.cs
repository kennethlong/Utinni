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
// Per-type structured decoders. Formats understood by reading the swg-client-v2 engine loaders
// (DataTable.cpp, LocalizedStringTableReaderWriter.cpp, ObjectTemplate.cpp — SOE/Bootprint, All
// Rights Reserved). No code, comments, identifier names, or test fixtures copied from any reference
// source — only the on-disk layout was studied. Implementation original to Utinni under MIT.

namespace UtinniCoreDotNet.Formats.Decoders
{
    /// <summary>
    /// Discriminates the kind of structured-decode failure. Mirrors the
    /// <see cref="Formats.Tre.TreParseError"/> idiom so the CLI can wrap a decode failure in a
    /// JSON error envelope with <c>error.kind = ex.Kind.ToString()</c>.
    /// </summary>
    public enum DecoderError
    {
        /// <summary>The root FORM (or a required sub-FORM) was not the tag this decoder expects.</summary>
        UnexpectedForm,

        /// <summary>A read ran past the end of a chunk's payload bytes (short read).</summary>
        Truncated,

        /// <summary>A count field (numCols/numRows/entry/field) read as a negative value.</summary>
        NegativeCount,

        /// <summary>
        /// A count field exceeds what the chunk's remaining bytes could possibly hold
        /// (forged-count over-allocation guard — rejected BEFORE allocating; T-07-13).
        /// </summary>
        CountExceedsCap,

        /// <summary>A version sub-tag or column-type code this bounded Phase-7 decoder does not support.</summary>
        UnsupportedVersion
    }

    /// <summary>
    /// Thrown when a per-type decoder encounters a structural problem in the IFF parse output it
    /// consumes. Carries a <see cref="DecoderError"/> discriminant for structured error reporting.
    /// </summary>
    public sealed class DecoderException : System.Exception
    {
        public DecoderError Kind { get; }

        public DecoderException(DecoderError kind, string message)
            : base(message)
        {
            Kind = kind;
        }
    }
}
