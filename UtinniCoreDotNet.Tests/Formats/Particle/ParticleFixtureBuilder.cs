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

using System.Collections.Generic;
using System.IO;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Tests.Formats.Particle
{
    /// <summary>
    /// Synthesizes deterministic <c>.prt</c> / FORM PEFT fixtures THROUGH the framework IFF primitives
    /// (<see cref="MutableIffNode"/> + <see cref="IffWriter"/>), mirroring the Datatable/STF/OT
    /// synth-fixture precedent — no real <c>.prt</c> fixtures exist today. Building through the writer
    /// makes every fixture canonical-by-construction so a byte-exact round-trip assertion is meaningful.
    /// </summary>
    internal static class ParticleFixtureBuilder
    {
        // ── little-endian scalar helpers ────────────────────────────────────────

        public static byte[] Int32Le(int v)
        {
            return new[] { (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)((v >> 24) & 0xFF) };
        }

        public static byte[] UInt32Le(uint v)
        {
            return new[] { (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)((v >> 24) & 0xFF) };
        }

        public static byte[] FloatLe(float v)
        {
            byte[] four = System.BitConverter.GetBytes(v);
            if (!System.BitConverter.IsLittleEndian) System.Array.Reverse(four);
            return four;
        }

        // ── WaveForm version-chunk payloads (the bytes INSIDE the WVFM version chunk) ──

        /// <summary>WVFM 0002 payload: int32 interp, int32 sample, float min, float max, int32 count, count×4 floats.</summary>
        public static byte[] WaveForm0002Payload(int interp, int sample, float min, float max,
            float[][] points)
        {
            using (var ms = new MemoryStream())
            {
                Write(ms, Int32Le(interp));
                Write(ms, Int32Le(sample));
                Write(ms, FloatLe(min));
                Write(ms, FloatLe(max));
                Write(ms, Int32Le(points.Length));
                foreach (float[] p in points)
                {
                    for (int i = 0; i < 4; i++) Write(ms, FloatLe(p[i]));
                }
                return ms.ToArray();
            }
        }

        /// <summary>WVFM 0001 payload: int32 interp, int32 sample, int32 count, count×4 floats.</summary>
        public static byte[] WaveForm0001Payload(int interp, int sample, float[][] points)
        {
            using (var ms = new MemoryStream())
            {
                Write(ms, Int32Le(interp));
                Write(ms, Int32Le(sample));
                Write(ms, Int32Le(points.Length));
                foreach (float[] p in points)
                {
                    for (int i = 0; i < 4; i++) Write(ms, FloatLe(p[i]));
                }
                return ms.ToArray();
            }
        }

        /// <summary>WVFM 0000 payload: int32 interp, int32 count, count×4 floats.</summary>
        public static byte[] WaveForm0000Payload(int interp, float[][] points)
        {
            using (var ms = new MemoryStream())
            {
                Write(ms, Int32Le(interp));
                Write(ms, Int32Le(points.Length));
                foreach (float[] p in points)
                {
                    for (int i = 0; i < 4; i++) Write(ms, FloatLe(p[i]));
                }
                return ms.ToArray();
            }
        }

        /// <summary>CLRR 0001 payload: uint32 interp, uint32 sample, uint32 count, count×4 floats (percent,r,g,b).</summary>
        public static byte[] ColorRamp0001Payload(uint interp, uint sample, float[][] points)
        {
            using (var ms = new MemoryStream())
            {
                Write(ms, UInt32Le(interp));
                Write(ms, UInt32Le(sample));
                Write(ms, UInt32Le((uint)points.Length));
                foreach (float[] p in points)
                {
                    for (int i = 0; i < 4; i++) Write(ms, FloatLe(p[i]));
                }
                return ms.ToArray();
            }
        }

        // ── form builders ───────────────────────────────────────────────────────

        /// <summary>Builds a standalone <c>FORM WVFM { &lt;versionTag&gt; payload }</c> serialized to bytes.</summary>
        public static byte[] BuildWaveFormBytes(string versionTag, byte[] versionChunkPayload)
        {
            MutableIffNode wvfm = MutableIffNode.NewContainer("FORM", "WVFM");
            wvfm.AddLeaf(versionTag, versionChunkPayload);
            return IffWriter.Write(new MutableIffDocument(wvfm));
        }

        /// <summary>Builds a standalone <c>FORM CLRR { &lt;versionTag&gt; payload }</c> serialized to bytes.</summary>
        public static byte[] BuildColorRampBytes(string versionTag, byte[] versionChunkPayload)
        {
            MutableIffNode clrr = MutableIffNode.NewContainer("FORM", "CLRR");
            clrr.AddLeaf(versionTag, versionChunkPayload);
            return IffWriter.Write(new MutableIffDocument(clrr));
        }

        /// <summary>A minimal valid two-point waveform payload (the engine requires ≥ 2 points).</summary>
        public static float[][] TwoPoints()
        {
            return new[]
            {
                new[] { 0.0f, 1.0f, 0.0f, 0.0f },
                new[] { 1.0f, 2.5f, 0.1f, 0.2f }
            };
        }

        /// <summary>A minimal valid two-point color ramp (percent, r, g, b).</summary>
        public static float[][] TwoColorPoints()
        {
            return new[]
            {
                new[] { 0.0f, 1.0f, 0.0f, 0.0f },
                new[] { 1.0f, 0.0f, 0.0f, 1.0f }
            };
        }

        // ── full PEFT tree (Task 2 + 3 consume these) ────────────────────────────

        /// <summary>
        /// Builds a minimal valid <c>FORM PEFT</c> 0002 tree with one EMGP group containing one EMTR
        /// emitter of the given version, serialized to bytes via <see cref="IffWriter"/>.
        ///
        /// Structure:
        /// <code>
        /// FORM PEFT
        /// └── FORM 0002
        ///     ├── FORM PTIM { 0000 timing-payload }
        ///     ├── CHUNK 0000 { int32 groupCount=1, 4 floats }
        ///     └── FORM EMGP
        ///         ├── FORM PTIM { 0000 timing-payload }
        ///         ├── CHUNK 0000 { int32 emitterCount=1 }
        ///         └── FORM EMTR
        ///             └── FORM &lt;emitterVersion&gt; { scalar chunk + a WVFM field }
        /// </code>
        /// </summary>
        public static byte[] BuildMinimalPeft(string emitterVersion)
        {
            MutableIffNode peft = MutableIffNode.NewContainer("FORM", "PEFT");
            MutableIffNode effVersion = peft.AddContainer("FORM", "0002");

            AddTiming(effVersion);

            // Effect-level group-count chunk: int32 count + 4 floats (0002 layout).
            using (var ms = new MemoryStream())
            {
                Write(ms, Int32Le(1));
                Write(ms, FloatLe(1.0f));
                Write(ms, FloatLe(0.0f));
                Write(ms, FloatLe(1.0f));
                Write(ms, FloatLe(1.0f));
                effVersion.AddLeaf("0000", ms.ToArray());
            }

            MutableIffNode emgp = effVersion.AddContainer("FORM", "EMGP");
            AddTiming(emgp);
            emgp.AddLeaf("0000", Int32Le(1)); // emitter-count chunk

            MutableIffNode emtr = emgp.AddContainer("FORM", "EMTR");
            MutableIffNode emtrVersion = emtr.AddContainer("FORM", emitterVersion);

            // A small recognizable scalar chunk + a WaveForm field so a typed decode (Task 2) has
            // something to walk, but a known-version emitter still round-trips byte-exact.
            emtrVersion.AddLeaf("0000", Int32Le(42));
            MutableIffNode wvfm = emtrVersion.AddContainer("FORM", "WVFM");
            wvfm.AddLeaf("0002", WaveForm0002Payload(0, 1, -10000f, 10000f, TwoPoints()));

            return IffWriter.Write(new MutableIffDocument(peft));
        }

        /// <summary>
        /// Builds a <c>FORM PEFT</c> tree whose ROOT version FORM uses the given (possibly unrecognized)
        /// version tag, so Task 3 can prove the root-version degrade path.
        /// </summary>
        public static byte[] BuildPeftWithRootVersion(string rootVersion)
        {
            MutableIffNode peft = MutableIffNode.NewContainer("FORM", "PEFT");
            MutableIffNode effVersion = peft.AddContainer("FORM", rootVersion);
            // Opaque content under the unknown version — the codec must raw-preserve the whole sub-tree.
            effVersion.AddLeaf("0000", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            MutableIffNode sub = effVersion.AddContainer("FORM", "ZZZZ");
            sub.AddLeaf("0000", new byte[] { 9, 10, 11, 12 });
            return IffWriter.Write(new MutableIffDocument(peft));
        }

        private static void AddTiming(MutableIffNode parent)
        {
            MutableIffNode ptim = parent.AddContainer("FORM", "PTIM");
            // Opaque timing payload — the codec preserves it verbatim (typed timing decode is a leaf
            // detail; byte-exactness is what this plan locks).
            using (var ms = new MemoryStream())
            {
                for (int i = 0; i < 6; i++) Write(ms, FloatLe(0.0f));
                ptim.AddLeaf("0000", ms.ToArray());
            }
        }

        private static void Write(Stream s, byte[] b)
        {
            s.Write(b, 0, b.Length);
        }
    }
}
