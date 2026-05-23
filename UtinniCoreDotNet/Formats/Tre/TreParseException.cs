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
// (SOE/Bootprint, All Rights Reserved). No code, comments, identifier names, or test fixtures copied
// from any reference source. Implementation original to Utinni under MIT.

namespace UtinniCoreDotNet.Formats.Tre
{
    /// <summary>
    /// Discriminates the kind of TRE parse failure.
    /// </summary>
    public enum TreParseError
    {
        BadMagic,
        UnsupportedVersion,
        Truncated,
        NegativeLength,
        ChunkLengthExceedsCap,
        DeflateExpansionExceedsCap
    }

    /// <summary>
    /// Thrown when the TRE parser encounters a structural problem in the input stream.
    /// Carries a <see cref="TreParseError"/> discriminant for structured error reporting
    /// (the CLI wraps this in a JSON error envelope with <c>error.kind = ex.Kind.ToString()</c>).
    /// </summary>
    public sealed class TreParseException : System.Exception
    {
        public TreParseError Kind { get; }

        public TreParseException(TreParseError kind, string message)
            : base(message)
        {
            Kind = kind;
        }
    }
}
