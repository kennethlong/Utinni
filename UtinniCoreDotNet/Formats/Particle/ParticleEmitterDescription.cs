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
// Emitter (FORM EMTR) layout understood by reading swg-client-v2
// .../clientParticle/.../ParticleEmitterDescription.cpp (15 versions) + ParticleDescriptionQuad.cpp /
// ParticleDescriptionMesh.cpp / ParticleTexture.cpp (SOE/Bootprint, All Rights Reserved). An EMTR
// form holds a version sub-form (0000..0014) whose body is a sequence of WaveForm (FORM WVFM) fields,
// a scalar/enum/bool chunk, and a FORM PTQD|PTMH leaf carrying a ColorRamp (FORM CLRR) + a PTEX
// texture ref. Only the on-disk layout was studied — no code, comments, identifier names, or test
// fixtures copied from any reference source. Implementation original to Utinni under MIT. Per
// Pitfall 2 the SOE reference hard-aborts on an unknown version; this codec NEVER hard-aborts — an unrecognized
// version sub-form is raw-preserved by the caller (D-05).

using System.Collections.Generic;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.Particle
{
    /// <summary>
    /// Typed read view of one <c>FORM EMTR</c> emitter. Decodes the WaveForm fields (via
    /// <see cref="WaveFormCodec"/>) and the ColorRamp leaf (via <see cref="ColorRampCodec"/>) found
    /// anywhere in the emitter's version sub-form into byte-exact typed values, and records whether
    /// the whole emitter was raw-preserved because its version tag was not recognized (D-05).
    ///
    /// <para>The typed decode is a NON-destructive overlay: the authoritative bytes live in the
    /// captured <see cref="MutableIffNode"/> tree (held by <see cref="MutableParticleEffect"/>), so the
    /// byte-exact round-trip never depends on this view. This view exists so the editor can show typed
    /// waveform / color fields, and so a future plan can wire per-field edits.</para>
    /// </summary>
    public sealed class ParticleEmitterDescription
    {
        // The set of EMTR version tags the typed reader walks. Versions outside this set are
        // raw-preserved by the caller — NEVER hard-abort (Pitfall 2). The on-disk body of every version is
        // a tree of WVFM/CLRR forms + scalar chunks, so the version-agnostic tree walk below types the
        // recurring leaves regardless of which exact version produced them; the set gates the
        // raw-preserve decision, not the field walk.
        private static readonly HashSet<string> KnownEmitterVersions = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "0000", "0001", "0002", "0003", "0004", "0005", "0006", "0007",
            "0008", "0009", "0010", "0011", "0012", "0013", "0014"
        };

        private readonly List<ParticleFieldValue> _waveFormFields;
        private readonly List<ParticleFieldValue> _colorRampFields;

        private ParticleEmitterDescription(string version, bool rawPreserved,
            List<ParticleFieldValue> waveFormFields, List<ParticleFieldValue> colorRampFields)
        {
            Version = version;
            IsRawPreserved = rawPreserved;
            _waveFormFields = waveFormFields;
            _colorRampFields = colorRampFields;
        }

        /// <summary>The emitter version tag (e.g. "0011"), or the unrecognized tag when raw-preserved.</summary>
        public string Version { get; }

        /// <summary>
        /// True when the emitter's version was not in the typed set and its whole sub-tree was
        /// raw-preserved verbatim (D-05). A raw-preserved emitter still opens + round-trips byte-exact,
        /// but typed editing is refused gracefully.
        /// </summary>
        public bool IsRawPreserved { get; }

        /// <summary>The typed WaveForm fields found in the emitter, in tree order.</summary>
        public IReadOnlyList<ParticleFieldValue> WaveFormFields { get { return _waveFormFields.AsReadOnly(); } }

        /// <summary>The typed ColorRamp fields found in the emitter, in tree order.</summary>
        public IReadOnlyList<ParticleFieldValue> ColorRampFields { get { return _colorRampFields.AsReadOnly(); } }

        /// <summary>
        /// Builds the typed view from an <c>EMTR</c> container node. An unrecognized version sub-form
        /// returns a raw-preserved view (no field walk, no throw). A recognized version walks the
        /// sub-tree typing every WVFM + CLRR leaf via the leaf codecs (each of which itself
        /// raw-preserves an unrecognized leaf version — degrade is layered, never hard-abort).
        /// </summary>
        public static ParticleEmitterDescription FromEmtrNode(MutableIffNode emtr)
        {
            if (emtr == null) throw new System.ArgumentNullException("emtr");

            MutableIffNode versionForm = FirstContainerChild(emtr);
            if (versionForm == null)
            {
                // Malformed EMTR with no version sub-form — raw-preserve the whole thing.
                return new ParticleEmitterDescription("", true,
                    new List<ParticleFieldValue>(), new List<ParticleFieldValue>());
            }

            string version = versionForm.SubTypeId;
            if (!KnownEmitterVersions.Contains(version))
            {
                // D-05: unrecognized version → raw-preserve the whole sub-tree (the captured bytes in
                // the MutableIffNode re-emit verbatim through IffWriter). NEVER hard-abort (Pitfall 2).
                return new ParticleEmitterDescription(version, true,
                    new List<ParticleFieldValue>(), new List<ParticleFieldValue>());
            }

            var waveForms = new List<ParticleFieldValue>();
            var colorRamps = new List<ParticleFieldValue>();
            WalkForWaveFormsAndColorRamps(versionForm, waveForms, colorRamps);

            return new ParticleEmitterDescription(version, false, waveForms, colorRamps);
        }

        // Recursively walks a sub-tree typing every FORM WVFM and FORM CLRR via the leaf codecs. The
        // leaf codecs raw-preserve an unrecognized leaf version, so this never hard-aborts.
        private static void WalkForWaveFormsAndColorRamps(MutableIffNode node,
            List<ParticleFieldValue> waveForms, List<ParticleFieldValue> colorRamps)
        {
            if (node.Kind != MutableIffNodeKind.Container) return;

            if (node.SubTypeId == "WVFM")
            {
                MutableIffNode chunk = FirstLeafChild(node);
                if (chunk != null)
                {
                    waveForms.Add(WaveFormCodec.TryDecode(chunk.TypeId, chunk.GetPayloadCopy()));
                }
                return;
            }

            if (node.SubTypeId == "CLRR")
            {
                MutableIffNode chunk = FirstLeafChild(node);
                if (chunk != null)
                {
                    colorRamps.Add(ColorRampCodec.TryDecode(chunk.TypeId, chunk.GetPayloadCopy()));
                }
                return;
            }

            foreach (MutableIffNode child in node.Children)
            {
                WalkForWaveFormsAndColorRamps(child, waveForms, colorRamps);
            }
        }

        private static MutableIffNode FirstContainerChild(MutableIffNode parent)
        {
            foreach (MutableIffNode child in parent.Children)
            {
                if (child.Kind == MutableIffNodeKind.Container) return child;
            }
            return null;
        }

        private static MutableIffNode FirstLeafChild(MutableIffNode parent)
        {
            foreach (MutableIffNode child in parent.Children)
            {
                if (child.Kind == MutableIffNodeKind.Leaf) return child;
            }
            return null;
        }
    }
}
