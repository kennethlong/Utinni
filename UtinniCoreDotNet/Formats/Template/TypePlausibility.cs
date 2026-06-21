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
// Implementation original to Utinni under MIT.
//
// D-17.3 type-plausibility predicate library: standalone, dependency-light heuristics that judge whether
// a decoded value "looks like" what its kernel type claims it is. This is the ONE genuinely new piece of
// logic in the phase with no in-repo analog (no codec to copy). It is deliberately a pure static
// predicate library with no I/O and no engine coupling so the future Tier-C corpus-inference pass reuses
// it verbatim. The thresholds are soft signals for a confidence indicator, NOT byte-layout decisions --
// byte-exactness lives entirely in the kernel codec; these predicates never gate a decode.

using System;

namespace UtinniCoreDotNet.Formats.Template
{
    /// <summary>
    /// Standalone plausibility predicates (D-17.3) used by <see cref="FitChecker"/> to populate
    /// per-field confidence (and, later, by Tier-C corpus scoring). Each is a pure function over a
    /// decoded value -- no side effects, no I/O.
    /// </summary>
    public static class TypePlausibility
    {
        // A field that decodes to a float larger than this magnitude is almost certainly mis-typed garbage
        // (a real SWG f32 -- a coordinate, scale, time, color channel -- sits well within this bound).
        private const float MaxPlausibleFloatMagnitude = 1e18f;

        // A count field above this is implausible for a real SWG leaf array; used only as a soft signal.
        private const long MaxPlausibleCount = 1 << 24; // ~16M

        /// <summary>
        /// True when <paramref name="value"/> is a finite float of non-absurd magnitude (rejects NaN,
        /// infinities, and values so large they signal a mis-typed field). A soft confidence signal only.
        /// </summary>
        public static bool LooksLikeFloat(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return false;
            }
            return Math.Abs(value) <= MaxPlausibleFloatMagnitude;
        }

        /// <summary>
        /// True when <paramref name="value"/> is a finite double of non-absurd magnitude (the f64 analog
        /// of <see cref="LooksLikeFloat(float)"/>).
        /// </summary>
        public static bool LooksLikeFloat(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return false;
            }
            return Math.Abs(value) <= MaxPlausibleFloatMagnitude;
        }

        /// <summary>
        /// True when <paramref name="span"/> is a printable-ASCII run terminated by a NUL within the span
        /// (the on-wire C-string shape). Control bytes before the terminator, or no terminator at all,
        /// read as implausible. A lone NUL (the empty string) is plausible.
        /// </summary>
        public static bool LooksLikeCStringRun(byte[] span)
        {
            if (span == null || span.Length == 0)
            {
                return false;
            }

            bool sawTerminator = false;
            foreach (byte b in span)
            {
                if (b == 0x00)
                {
                    sawTerminator = true;
                    break;
                }
                // Printable ASCII range (space .. tilde); tab is also tolerated.
                bool printable = (b >= 0x20 && b <= 0x7E) || b == 0x09;
                if (!printable)
                {
                    return false;
                }
            }

            return sawTerminator;
        }

        /// <summary>
        /// True when <paramref name="value"/> is a non-negative count below a sane upper bound -- the
        /// shape a count-from-prior length field should have.
        /// </summary>
        public static bool LooksLikeCount(long value)
        {
            return value >= 0 && value <= MaxPlausibleCount;
        }
    }
}
