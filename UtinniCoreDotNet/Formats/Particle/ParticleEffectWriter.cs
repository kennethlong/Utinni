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
// Particle-effect (.prt / FORM PEFT) byte-exact serializer. Format understood by reading swg-client-v2
// .../clientParticle/.../ParticleEffectDescription.cpp (SOE/Bootprint, All Rights Reserved). Only the
// on-disk layout was studied — no code, comments, identifier names, or test fixtures copied from any
// reference source. Implementation original to Utinni under MIT.

using System;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.Particle
{
    /// <summary>
    /// Byte-exact particle-effect serializer. Composes <see cref="IffWriter.Write(MutableIffDocument)"/>
    /// over the mutated captured document — the same shape as <c>ObjectTemplateWriter</c>: no bespoke
    /// serializer, no hand-rolled framing. Untouched leaves (including every raw-preserved
    /// unrecognized-version sub-tree) re-emit their captured verbatim source-byte slice; only the
    /// leaves the model mutated reserialize fresh. For an unmodified model the output equals the input
    /// bytes exactly.
    /// </summary>
    public static class ParticleEffectWriter
    {
        /// <summary>Serializes the model to byte-exact output via the shared <see cref="IffWriter"/>.</summary>
        public static byte[] Serialize(MutableParticleEffect model)
        {
            if (model == null) throw new ArgumentNullException("model");
            if (model.SourceIff == null)
            {
                throw new InvalidOperationException(
                    "MutableParticleEffect has no captured SourceIff to serialize.");
            }
            return IffWriter.Write(model.SourceIff);
        }
    }
}
