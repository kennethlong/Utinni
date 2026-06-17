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
// ClientEffect (.iff / FORM CLEF) root layout understood by reading swg-client-v2
// .../clientGame/.../ClientEffectTemplate.cpp (versions 0001/0002/0003) (SOE/Bootprint, All Rights
// Reserved). Only the on-disk layout was studied — no code, comments, identifier names, or test
// fixtures copied from any reference source. Implementation original to Utinni under MIT.

using System.IO;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.ClientEffect
{
    /// <summary>
    /// CLEF root typed-decode entry. Composes the shipped tree-based <see cref="IffReader"/> +
    /// <see cref="MutableIffDocument"/> to parse a ClientEffect <c>.iff</c> byte stream into a
    /// <see cref="MutableClientEffect"/> — the same <c>bytes → IffReader.Read →
    /// MutableIffDocument.FromDocument → Mutable*</c> compose-never-reframe idiom
    /// <c>ParticleEffectDocument</c> uses. No hand-rolled chunk/pad/endianness handling — that all
    /// lives in the shared IFF primitives.
    /// </summary>
    public static class ClientEffectDocument
    {
        /// <summary>
        /// Parses a ClientEffect <c>.iff</c> byte buffer into a mutable typed effect. Throws
        /// <see cref="ClientEffectParseException"/> on a non-CLEF root or a forged count; an
        /// unrecognized version FORM degrades to byte-exact raw-preserve (D-13), never an exception,
        /// and an unknown/truncated command leaf degrades to a raw command view (REVIEWS #4).
        /// </summary>
        public static MutableClientEffect FromBytes(byte[] bytes)
        {
            if (bytes == null) throw new System.ArgumentNullException("bytes");
            IffDocument iffDoc;
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                iffDoc = IffReader.Read(ms); // tree-walk reader
            }
            MutableIffDocument mutableIff = MutableIffDocument.FromDocument(iffDoc, bytes);
            return MutableClientEffect.FromMutableIff(mutableIff);
        }

        /// <summary>
        /// Parses a ClientEffect from an already-parsed <see cref="IffDocument"/> + its source bytes
        /// (skips the re-read; used by the decode-iff CLEF branch in Plan 02).
        /// </summary>
        public static MutableClientEffect FromIff(IffDocument iffDoc, byte[] sourceBytes)
        {
            if (iffDoc == null) throw new System.ArgumentNullException("iffDoc");
            if (sourceBytes == null) throw new System.ArgumentNullException("sourceBytes");
            MutableIffDocument mutableIff = MutableIffDocument.FromDocument(iffDoc, sourceBytes);
            return MutableClientEffect.FromMutableIff(mutableIff);
        }
    }
}
