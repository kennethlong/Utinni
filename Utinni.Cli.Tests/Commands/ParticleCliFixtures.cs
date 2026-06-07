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

using System.IO;
using UtinniCoreDotNet.Formats.Iff;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// Synthesizes deterministic <c>.prt</c> / FORM PEFT golden fixtures THROUGH the framework IFF
    /// primitives (<see cref="MutableIffNode"/> + <see cref="IffWriter"/>) — no real <c>.prt</c>
    /// fixtures exist today (extract-from-<c>.tre</c> is the documented alternate). Building through the
    /// writer makes every fixture canonical-by-construction so a byte-exact round-trip assertion over
    /// the CLI verb is meaningful. Mirrors the layout of the codec's own
    /// <c>UtinniCoreDotNet.Tests.Formats.Particle.ParticleFixtureBuilder</c> (which is internal to that
    /// test assembly and not visible here).
    /// </summary>
    internal static class ParticleCliFixtures
    {
        public static byte[] Int32Le(int v)
        {
            return new[] { (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)((v >> 24) & 0xFF) };
        }

        public static byte[] FloatLe(float v)
        {
            byte[] four = System.BitConverter.GetBytes(v);
            if (!System.BitConverter.IsLittleEndian) System.Array.Reverse(four);
            return four;
        }

        private static byte[] WaveForm0002Payload(int interp, int sample, float min, float max, float[][] points)
        {
            using (var ms = new MemoryStream())
            {
                ms.Write(Int32Le(interp), 0, 4);
                ms.Write(Int32Le(sample), 0, 4);
                ms.Write(FloatLe(min), 0, 4);
                ms.Write(FloatLe(max), 0, 4);
                ms.Write(Int32Le(points.Length), 0, 4);
                foreach (float[] p in points)
                    for (int i = 0; i < 4; i++) ms.Write(FloatLe(p[i]), 0, 4);
                return ms.ToArray();
            }
        }

        private static float[][] TwoPoints()
        {
            return new[]
            {
                new[] { 0.0f, 1.0f, 0.0f, 0.0f },
                new[] { 1.0f, 2.5f, 0.1f, 0.2f }
            };
        }

        private static void AddTiming(MutableIffNode parent)
        {
            MutableIffNode ptim = parent.AddContainer("FORM", "PTIM");
            using (var ms = new MemoryStream())
            {
                for (int i = 0; i < 6; i++) ms.Write(FloatLe(0.0f), 0, 4);
                ptim.AddLeaf("0000", ms.ToArray());
            }
        }

        /// <summary>
        /// FORM PEFT 0002 with one EMGP group containing one EMTR emitter of the given version
        /// (a recognized version round-trips byte-exact with a typed walk).
        /// </summary>
        public static byte[] BuildMinimalPeft(string emitterVersion)
        {
            MutableIffNode peft = MutableIffNode.NewContainer("FORM", "PEFT");
            MutableIffNode effVersion = peft.AddContainer("FORM", "0002");
            AddTiming(effVersion);

            using (var ms = new MemoryStream())
            {
                ms.Write(Int32Le(1), 0, 4);
                ms.Write(FloatLe(1.0f), 0, 4);
                ms.Write(FloatLe(0.0f), 0, 4);
                ms.Write(FloatLe(1.0f), 0, 4);
                ms.Write(FloatLe(1.0f), 0, 4);
                effVersion.AddLeaf("0000", ms.ToArray());
            }

            MutableIffNode emgp = effVersion.AddContainer("FORM", "EMGP");
            AddTiming(emgp);
            emgp.AddLeaf("0000", Int32Le(1));

            MutableIffNode emtr = emgp.AddContainer("FORM", "EMTR");
            MutableIffNode emtrVersion = emtr.AddContainer("FORM", emitterVersion);
            emtrVersion.AddLeaf("0000", Int32Le(42));
            MutableIffNode wvfm = emtrVersion.AddContainer("FORM", "WVFM");
            wvfm.AddLeaf("0002", WaveForm0002Payload(0, 1, -10000f, 10000f, TwoPoints()));

            return IffWriter.Write(new MutableIffDocument(peft));
        }

        /// <summary>FORM PEFT whose ROOT version FORM is the given (possibly unrecognized) tag.</summary>
        public static byte[] BuildPeftWithRootVersion(string rootVersion)
        {
            MutableIffNode peft = MutableIffNode.NewContainer("FORM", "PEFT");
            MutableIffNode effVersion = peft.AddContainer("FORM", rootVersion);
            effVersion.AddLeaf("0000", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            MutableIffNode sub = effVersion.AddContainer("FORM", "ZZZZ");
            sub.AddLeaf("0000", new byte[] { 9, 10, 11, 12 });
            return IffWriter.Write(new MutableIffDocument(peft));
        }

        /// <summary>
        /// FORM PEFT 0002 with a recognized-version EMTR whose WVFM field carries a TRUNCATED 0002
        /// payload (claims control points it does not contain). The codec must raw-preserve that field
        /// (consume-exactly-or-hex) rather than OOB-throwing, and the whole effect must round-trip exact.
        /// </summary>
        public static byte[] BuildPeftWithTruncatedWaveForm()
        {
            MutableIffNode peft = MutableIffNode.NewContainer("FORM", "PEFT");
            MutableIffNode effVersion = peft.AddContainer("FORM", "0002");
            AddTiming(effVersion);
            using (var ms = new MemoryStream())
            {
                ms.Write(Int32Le(1), 0, 4);
                for (int i = 0; i < 4; i++) ms.Write(FloatLe(1.0f), 0, 4);
                effVersion.AddLeaf("0000", ms.ToArray());
            }

            MutableIffNode emgp = effVersion.AddContainer("FORM", "EMGP");
            AddTiming(emgp);
            emgp.AddLeaf("0000", Int32Le(1));

            MutableIffNode emtr = emgp.AddContainer("FORM", "EMTR");
            MutableIffNode emtrVersion = emtr.AddContainer("FORM", "0011");
            emtrVersion.AddLeaf("0000", Int32Le(7));

            MutableIffNode wvfm = emtrVersion.AddContainer("FORM", "WVFM");
            using (var ms = new MemoryStream())
            {
                ms.Write(Int32Le(0), 0, 4);
                ms.Write(Int32Le(0), 0, 4);
                ms.Write(FloatLe(-1f), 0, 4);
                ms.Write(FloatLe(1f), 0, 4);
                ms.Write(Int32Le(5), 0, 4);   // claims 5 points
                ms.Write(FloatLe(0.0f), 0, 4); // ... but only one stray float follows
                wvfm.AddLeaf("0002", ms.ToArray());
            }

            return IffWriter.Write(new MutableIffDocument(peft));
        }

        /// <summary>A well-formed IFF whose root is NOT FORM PEFT (FORM SHOT) — for the non-PEFT rejection.</summary>
        public static byte[] BuildNonPeftIff()
        {
            MutableIffNode root = MutableIffNode.NewContainer("FORM", "SHOT");
            MutableIffNode version = root.AddContainer("FORM", "0000");
            version.AddLeaf("PCNT", Int32Le(0));
            return IffWriter.Write(new MutableIffDocument(root));
        }
    }
}
