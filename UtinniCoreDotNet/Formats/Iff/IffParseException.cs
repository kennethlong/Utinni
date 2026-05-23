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
// Format understood by reading swg-client-v2/src/engine/shared/library/sharedFile/src/shared/{TreeFile,Iff}.{h,cpp}
// (SOE/Bootprint, All Rights Reserved) and the EA-IFF-85 public standard. No code,
// comments, identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.

namespace UtinniCoreDotNet.Formats.Iff
{
    /// <summary>
    /// Enumeration of structured error kinds that can be raised by <see cref="IffReader"/>.
    ///
    /// <para><b>Iter-4 HIGH-1 taxonomy (each value is reachable via a distinct deterministic code path):</b></para>
    /// <list type="bullet">
    ///   <item><see cref="NegativeLength"/> — chunk length field is negative.</item>
    ///   <item><see cref="ChunkLengthExceedsCap"/> — chunk length exceeds 64 MB cap (T-04-DoS).</item>
    ///   <item><see cref="NestedChunkOverflow"/> — nested chunk's declared end exceeds the parent's end.</item>
    ///   <item><see cref="Truncated"/> — streaming read of payload hits EOF before declared length is consumed,
    ///         OR a short read in ReadInt32Be, OR an odd-length chunk at parent boundary with no pad byte
    ///         (REVIEWS MEDIUM-11 + iter-3 MED-3).</item>
    ///   <item><see cref="MalformedFourCc"/> — chunk type ID contains non-printable-ASCII bytes.</item>
    /// </list>
    ///
    /// <para>NOTE: The previous file-bound error value was REMOVED under iter-4 HIGH-1.
    /// Top-level chunks cannot exceed file length (their parentEnd IS stream.Length); nested chunks raise
    /// <see cref="NestedChunkOverflow"/> on parent-bound failure.</para>
    /// </summary>
    public enum IffParseError
    {
        /// <summary>A chunk's length field is negative.</summary>
        NegativeLength,

        /// <summary>A chunk's length field exceeds the 64 MB cap (T-04-DoS).</summary>
        ChunkLengthExceedsCap,

        /// <summary>
        /// A nested chunk's declared end (chunkStart + 8 + length) exceeds the parent chunk's end.
        /// Iter-4 HIGH-1: only applies to nested chunks; top-level FORM is the file.
        /// </summary>
        NestedChunkOverflow,

        /// <summary>
        /// The underlying stream ends before the declared payload length is consumed (streaming-read EOF),
        /// a short-read in ReadInt32Be, or an odd-length chunk at parent boundary with no pad byte
        /// (REVIEWS MEDIUM-11 + iter-3 MED-3).
        /// </summary>
        Truncated,

        /// <summary>A chunk's TypeID field contains non-printable-ASCII bytes.</summary>
        MalformedFourCc
    }

    /// <summary>
    /// Structured exception raised by <see cref="IffReader"/> when the input stream does not
    /// conform to the EA-IFF-85 format or violates a safety constraint.
    /// </summary>
    public sealed class IffParseException : System.Exception
    {
        /// <summary>The structured error kind for programmatic dispatch.</summary>
        public IffParseError Kind { get; }

        /// <summary>Creates a new <see cref="IffParseException"/> with the given kind and message.</summary>
        public IffParseException(IffParseError kind, string message)
            : base(message)
        {
            Kind = kind;
        }
    }
}
