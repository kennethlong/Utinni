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
// String-table (.stf) format understood by reading swg-client-v2
// .../localization/src/shared/{LocalizedStringTable,LocalizedString,LocalizedStringTableReaderWriter}.cpp
// (SOE/Bootprint, All Rights Reserved). Only the on-disk layout + writer algorithm + name-validation
// rules were studied — no code, comments, identifier names, or test fixtures copied from any reference
// source. Implementation original to Utinni under MIT.

using System;

namespace UtinniCoreDotNet.Formats.StringTable
{
    /// <summary>
    /// Structured exception raised by the Phase 10 flat-binary <c>.stf</c> reader
    /// (<see cref="StringTableDocument.FromBytes"/>) when a payload does not conform to the expected
    /// string-table shape or violates a safety constraint.
    ///
    /// <para>Shape mirrors <c>UtinniCoreDotNet.Formats.Datatable.DataTableParseException</c> (the
    /// Phase 9 analog) — a sealed exception carrying a human-readable message and (optionally) an
    /// inner exception. Reachable causes include: bad magic (not <c>0xABCD</c>), unsupported version
    /// (not 0 or 1), truncated payload, and a forged entry/char/name count (T-10-01 DoS).</para>
    /// </summary>
    [Serializable]
    public sealed class StringTableParseException : Exception
    {
        /// <summary>Creates a new <see cref="StringTableParseException"/> with the given message.</summary>
        public StringTableParseException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Creates a new <see cref="StringTableParseException"/> with the given message and the inner
        /// exception that caused it.
        /// </summary>
        public StringTableParseException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
