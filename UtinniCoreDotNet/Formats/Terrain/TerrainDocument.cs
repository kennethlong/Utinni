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
// Terrain (.trn / FORM TGEN) root typed-decode entry understood by reading swg-client-v2
// .../sharedTerrain/.../generator/TerrainGenerator.cpp (the PTAT/TGEN root + load_0000 palette/layer
// order) (SOE/Bootprint, All Rights Reserved) and the EA-IFF-85 public standard. Composes the shipped
// IffReader + MutableIffDocument exactly the ParticleEffectDocument FromBytes/FromIff idiom — no
// hand-rolled chunk/pad/endianness handling. Only the on-disk layout was studied — no code, comments,
// identifier names, or test fixtures copied from any reference source. Implementation original to Utinni
// under MIT.

using System;
using System.Collections.Generic;
using System.IO;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.Terrain
{
    /// <summary>
    /// The decoded terrain (<c>.trn</c> / FORM TGEN) document: the navigable layer tree + the six
    /// read-only palettes, decoded over the shipped IFF DOM. HOLDS the underlying
    /// <see cref="MutableIffDocument"/> so the edit/roundtrip path (Plan 03) can re-emit through
    /// <see cref="Serialize"/> (= <c>IffWriter.Write(heldMutable)</c>, concern #12) — clean nodes re-emit
    /// their captured slice verbatim; only a <c>SetPayload</c>'d leaf re-encodes.
    ///
    /// <para><b>Entry idiom (mirrors <see cref="Particle.ParticleEffectDocument"/>):</b>
    /// <see cref="FromBytes"/> does <c>IffReader.Read → MutableIffDocument.FromDocument → TgenDecoder.Decode</c>;
    /// <see cref="FromIff"/> skips the re-read. A non-TGEN root raises <see cref="TerrainParseException"/>
    /// (not a generic throw).</para>
    /// </summary>
    public sealed class TerrainDocument
    {
        /// <summary>The held mutable IFF DOM — the byte-exact re-emit source for <see cref="Serialize"/>.</summary>
        public MutableIffDocument Mutable { get; }

        /// <summary>The six read-only palettes in fixed load order (D-04).</summary>
        public TerrainPalettes Palettes { get; }

        /// <summary>The navigable top-level layer list (each layer recurses into sub-layers).</summary>
        public IReadOnlyList<TerrainLayer> Layers { get; }

        /// <summary>The root FORM sub-type that was decoded ("TGEN").</summary>
        public string RootForm { get; }

        /// <summary>Constructs a decoded terrain document. Called by <see cref="TgenDecoder"/>.</summary>
        public TerrainDocument(
            MutableIffDocument mutable,
            string rootForm,
            TerrainPalettes palettes,
            IReadOnlyList<TerrainLayer> layers)
        {
            Mutable = mutable ?? throw new ArgumentNullException(nameof(mutable));
            RootForm = rootForm;
            Palettes = palettes ?? throw new ArgumentNullException(nameof(palettes));
            Layers = layers ?? throw new ArgumentNullException(nameof(layers));
        }

        /// <summary>
        /// Parses a <c>.trn</c> byte buffer into a decoded terrain document. Throws
        /// <see cref="TerrainParseException"/> on a non-TGEN root; an unknown FourCC or unrecognized FORM
        /// version degrades to raw-preserve (D-01/D-02), never an exception.
        /// </summary>
        public static TerrainDocument FromBytes(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            IffDocument iffDoc;
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                iffDoc = IffReader.Read(ms);
            }
            MutableIffDocument mutableIff = MutableIffDocument.FromDocument(iffDoc, bytes);
            return TgenDecoder.Decode(mutableIff);
        }

        /// <summary>Parses a <c>.trn</c> from an already-parsed <see cref="IffDocument"/> + its source bytes.</summary>
        public static TerrainDocument FromIff(IffDocument iffDoc, byte[] sourceBytes)
        {
            if (iffDoc == null) throw new ArgumentNullException(nameof(iffDoc));
            if (sourceBytes == null) throw new ArgumentNullException(nameof(sourceBytes));
            MutableIffDocument mutableIff = MutableIffDocument.FromDocument(iffDoc, sourceBytes);
            return TgenDecoder.Decode(mutableIff);
        }

        /// <summary>
        /// Re-emits the held mutable DOM to bytes (<c>IffWriter.Write</c>). On an unedited document this
        /// is byte-identical to the input (captured-slice verbatim re-emit); Plan 03's field edit dirties
        /// only the touched leaf, so untouched leaves stay byte-exact (concern #12).
        /// </summary>
        public byte[] Serialize()
        {
            return IffWriter.Write(Mutable);
        }
    }
}
