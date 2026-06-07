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
// Particle-effect (.prt / FORM PEFT) root layout understood by reading swg-client-v2
// .../clientParticle/.../ParticleEffectDescription.cpp (versions 0000/0001/0002 — 0002 is the current
// write version) (SOE/Bootprint, All Rights Reserved). Only the on-disk layout was studied — no code,
// comments, identifier names, or test fixtures copied from any reference source. Implementation
// original to Utinni under MIT.

using System.IO;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.Particle
{
    /// <summary>
    /// PEFT root typed-decode entry. Composes the shipped tree-based <see cref="IffReader"/> +
    /// <see cref="MutableIffDocument"/> to parse a <c>.prt</c> byte stream into a
    /// <see cref="MutableParticleEffect"/> — exactly the <c>RoundtripOtCommand.ParseOt</c> idiom
    /// (<c>bytes → IffReader.Read → MutableIffDocument.FromDocument → Mutable*</c>). No hand-rolled
    /// chunk/pad/endianness handling — that all lives in the shared IFF primitives (Don't Hand-Roll).
    /// </summary>
    public static class ParticleEffectDocument
    {
        /// <summary>
        /// Parses a <c>.prt</c> byte buffer into a mutable typed effect. Throws
        /// <see cref="ParticleParseException"/> on a non-PEFT root or a forged count; an unrecognized
        /// FORM version degrades to byte-exact raw-preserve (D-05), never an exception.
        /// </summary>
        public static MutableParticleEffect FromBytes(byte[] bytes)
        {
            if (bytes == null) throw new System.ArgumentNullException("bytes");
            IffDocument iffDoc;
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                iffDoc = IffReader.Read(ms); // tree-walk reader
            }
            MutableIffDocument mutableIff = MutableIffDocument.FromDocument(iffDoc, bytes);
            return MutableParticleEffect.FromMutableIff(mutableIff);
        }

        /// <summary>
        /// Parses a <c>.prt</c> from an already-parsed <see cref="IffDocument"/> + its source bytes.
        /// </summary>
        public static MutableParticleEffect FromIff(IffDocument iffDoc, byte[] sourceBytes)
        {
            if (iffDoc == null) throw new System.ArgumentNullException("iffDoc");
            if (sourceBytes == null) throw new System.ArgumentNullException("sourceBytes");
            MutableIffDocument mutableIff = MutableIffDocument.FromDocument(iffDoc, sourceBytes);
            return MutableParticleEffect.FromMutableIff(mutableIff);
        }
    }
}
