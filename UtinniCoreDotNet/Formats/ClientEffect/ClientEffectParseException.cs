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
// ClientEffect (.iff / FORM CLEF) format understood by reading swg-client-v2
// .../clientGame/.../ClientEffectTemplate.cpp (versions 0001/0002/0003) + .../sharedFile/.../Iff.{h,cpp}
// (SOE/Bootprint, All Rights Reserved). Only the on-disk layout was studied — no code, comments,
// identifier names, or test fixtures copied from any reference source. Implementation original to
// Utinni under MIT.

namespace UtinniCoreDotNet.Formats.ClientEffect
{
    /// <summary>
    /// Discriminates the kind of structural problem the ClientEffect codec rejects up front. Mirrors
    /// the shared <see cref="Decoders.DecoderError"/> taxonomy so the CLI can surface a JSON error
    /// envelope. NOTE: an UNRECOGNIZED CLEF version FORM is NOT an error — it raw-preserves the whole
    /// CLEF sub-tree (D-13) and never throws; an UNKNOWN command tag or a TRUNCATED/undecodable KNOWN
    /// command leaf is NOT an error either — it degrades to a raw command view (REVIEWS #4). Only a
    /// hard CONTAINER-framing malformation (wrong root FORM, forged child count) — never a command-leaf
    /// payload — reaches this exception (REVIEWS #4).
    /// </summary>
    public enum ClientEffectParseError
    {
        /// <summary>The root FORM was not <c>CLEF</c> — this is not a ClientEffect template.</summary>
        UnexpectedForm,

        /// <summary>
        /// The flat command count exceeds what the version FORM could possibly hold
        /// (forged-count over-allocation guard — rejected BEFORE allocating; T-22-forged-count).
        /// </summary>
        ForgedCount,

        /// <summary>A required command payload read ran past the end of the chunk's bytes.</summary>
        Truncated,

        /// <summary>
        /// The CPAP encoder was asked to emit a field that the SOURCE version does not define
        /// (a v0003-only field below v0003, or the v0002 field below v0002) — the D-03 encode-boundary
        /// guard that prevents a v0001 file silently gaining v0003 bytes (REVIEWS HIGH #2).
        /// </summary>
        VersionFieldMismatch
    }

    /// <summary>
    /// Thrown when the ClientEffect (.iff / FORM CLEF) codec encounters a hard CONTAINER-framing
    /// problem it cannot degrade to raw-preserve (a non-CLEF root or a forged/oversized command count),
    /// OR when the CPAP encoder is asked to emit a field above the source version (the D-03 encode
    /// guard). Carries a <see cref="ClientEffectParseError"/> discriminant for structured reporting.
    /// </summary>
    public sealed class ClientEffectParseException : System.Exception
    {
        /// <summary>The structured failure discriminant.</summary>
        public ClientEffectParseError Kind { get; }

        /// <summary>Constructs a ClientEffect parse exception with a kind + message.</summary>
        public ClientEffectParseException(ClientEffectParseError kind, string message)
            : base(message)
        {
            Kind = kind;
        }
    }
}
