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
// Particle (.prt / FORM PEFT) format understood by reading swg-client-v2
// .../clientParticle/.../ParticleEffectDescription.cpp + ParticleEmitterGroupDescription.cpp +
// ParticleEmitterDescription.cpp + ParticleTiming.cpp, and .../sharedMath/.../WaveForm.cpp +
// .../clientParticle/.../ColorRamp.cpp (SOE/Bootprint, All Rights Reserved). Only the on-disk layout
// was studied — no code, comments, identifier names, or test fixtures copied from any reference
// source. Implementation original to Utinni under MIT.

namespace UtinniCoreDotNet.Formats.Particle
{
    /// <summary>
    /// Discriminates the kind of structural problem the particle codec rejects up front. Mirrors the
    /// shared <see cref="Decoders.DecoderError"/> taxonomy so the CLI can surface a JSON error
    /// envelope. NOTE: an UNRECOGNIZED FORM version is NOT an error — it degrades to byte-exact
    /// raw-preserve (D-05) and never throws (the codec NEVER hard-aborts the way the SOE reference does).
    /// Only a hard malformation (an attacker-forged oversized count, a truncated required scalar that
    /// cannot even be raw-preserved as a sub-tree) reaches this exception.
    /// </summary>
    public enum ParticleParseError
    {
        /// <summary>The root FORM was not <c>PEFT</c> — this is not a particle effect template.</summary>
        UnexpectedForm,

        /// <summary>A count field read as a negative value.</summary>
        NegativeCount,

        /// <summary>
        /// A count field exceeds what the remaining payload / sub-tree budget could possibly hold
        /// (forged-count over-allocation guard — rejected BEFORE allocating; T-15-02).
        /// </summary>
        CountExceedsCap
    }

    /// <summary>
    /// Thrown when the particle (.prt / FORM PEFT) codec encounters a hard structural problem it
    /// cannot degrade to raw-preserve (a non-PEFT root or a forged/oversized count). Carries a
    /// <see cref="ParticleParseError"/> discriminant for structured error reporting.
    /// </summary>
    public sealed class ParticleParseException : System.Exception
    {
        /// <summary>The structured failure discriminant.</summary>
        public ParticleParseError Kind { get; }

        /// <summary>Constructs a particle parse exception with a kind + message.</summary>
        public ParticleParseException(ParticleParseError kind, string message)
            : base(message)
        {
            Kind = kind;
        }
    }
}
