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
// Terrain (.trn / FORM TGEN) format understood by reading swg-client-v2
// .../sharedTerrain/.../generator/{TerrainGenerator,TerrainGeneratorLoader,AffectorHeight,AffectorShader,
// Boundary,Filter}.{cpp,h} (SOE/Bootprint, All Rights Reserved) and the EA-IFF-85 public standard. Only
// the on-disk layout was studied — no code, comments, identifier names, or test fixtures copied from any
// reference source. Implementation original to Utinni under MIT.

namespace UtinniCoreDotNet.Formats.Terrain
{
    /// <summary>
    /// Discriminates the kind of HARD structural problem the terrain (<c>.trn</c> / FORM TGEN) codec
    /// rejects up front. Mirrors the <see cref="Particle.ParticleParseError"/> taxonomy.
    ///
    /// <para><b>NOTE:</b> an unrecognized FORM version or an unknown FourCC is NOT an error — it
    /// degrades to byte-exact raw-preserve (D-01/D-02) and never throws. Only a hard malformation that
    /// cannot be expressed at all (a non-TGEN root) reaches this exception. The decoder NEVER hard-aborts
    /// the way the SOE reference loader does on an unknown layer/affector tag.</para>
    /// </summary>
    public enum TerrainParseError
    {
        /// <summary>The root FORM was not <c>TGEN</c> (nor a <c>PTAT</c> wrapper around one) — not a terrain template.</summary>
        UnexpectedForm
    }

    /// <summary>
    /// Thrown when the terrain (<c>.trn</c> / FORM TGEN) codec encounters a hard structural problem it
    /// cannot degrade to raw-preserve — i.e. the root is not a <c>TGEN</c> (nor a <c>PTAT</c> wrapping a
    /// <c>TGEN</c>). Carries a <see cref="TerrainParseError"/> discriminant for structured reporting.
    /// </summary>
    public sealed class TerrainParseException : System.Exception
    {
        /// <summary>The structured failure discriminant.</summary>
        public TerrainParseError Kind { get; }

        /// <summary>Constructs a terrain parse exception with a kind + message.</summary>
        public TerrainParseException(TerrainParseError kind, string message)
            : base(message)
        {
            Kind = kind;
        }
    }
}
