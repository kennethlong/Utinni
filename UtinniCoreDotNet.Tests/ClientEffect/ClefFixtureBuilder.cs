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
using UtinniCoreDotNet.Formats.ClientEffect;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Tests.ClientEffect
{
    /// <summary>
    /// Synthesizes deterministic ClientEffect (FORM CLEF) <c>.iff</c> fixtures THROUGH the framework IFF
    /// primitives (<see cref="MutableIffNode"/> + <see cref="IffWriter"/>) + the <see cref="ClefFieldCodec"/>
    /// command-payload encoders, mirroring the Particle/Terrain synth-fixture precedent. Building through
    /// the writer makes every fixture canonical-by-construction so a byte-exact round-trip assertion is
    /// meaningful. The committed goldens under <c>Fixtures/ClientEffect/</c> are exactly the bytes these
    /// builders emit; <see cref="ClefFixtureFiles"/> names them.
    /// </summary>
    internal static class ClefFixtureBuilder
    {
        // Small fixed values so the bytes are stable + reviewable.
        public const string AppearanceName = "appearance/test.prt";
        public const string SoundName = "sound/test.snd";
        public const string FfFile = "forcefeedback/test.ffe";

        // ── LE scalar helpers (for the hand-authored hex + truncation surgery) ──

        public static byte[] FloatLe(float v)
        {
            byte[] four = System.BitConverter.GetBytes(v);
            if (!System.BitConverter.IsLittleEndian) System.Array.Reverse(four);
            return four;
        }

        // ── per-command payloads via the production codec ───────────────────────

        public static byte[] CpapPayload(string version)
        {
            switch (version)
            {
                case "0001":
                    return ClefFieldCodec.EncodeCpap("0001", AppearanceName, 2.5f, null, null, null, null, null);
                case "0002":
                    return ClefFieldCodec.EncodeCpap("0002", AppearanceName, 2.5f, true, null, null, null, null);
                default: // 0003
                    return ClefFieldCodec.EncodeCpap("0003", AppearanceName, 2.5f, true, 0.5f, 1.5f, 1.0f, 2.0f);
            }
        }

        public static byte[] PsndPayload() => ClefFieldCodec.EncodePsnd(SoundName);

        public static byte[] ClgtPayload() =>
            ClefFieldCodec.EncodeClgt(10, 20, 30, 1.0f, 0.1f, 0.2f, 0.3f, 25.0f);

        public static byte[] CamsPayload() =>
            ClefFieldCodec.EncodeCams(0.5f, 4.0f, 1.5f, 12.0f);

        public static byte[] FfbkPayload() =>
            ClefFieldCodec.EncodeFfbk(FfFile, 3, 7.5f);

        // ── full CLEF trees ─────────────────────────────────────────────────────

        /// <summary>FORM CLEF { FORM &lt;version&gt; { CPAP } } — the single-command CPAP fixture.</summary>
        public static byte[] BuildSingleCpap(string version)
        {
            MutableIffNode clef = MutableIffNode.NewContainer("FORM", "CLEF");
            MutableIffNode ver = clef.AddContainer("FORM", version);
            ver.AddLeaf("CPAP", CpapPayload(version));
            return IffWriter.Write(new MutableIffDocument(clef));
        }

        /// <summary>FORM CLEF { FORM &lt;version&gt; { CPAP, PSND, CLGT, CAMS, FFBK } } — all five commands.</summary>
        public static byte[] BuildAllFive(string version)
        {
            MutableIffNode clef = MutableIffNode.NewContainer("FORM", "CLEF");
            MutableIffNode ver = clef.AddContainer("FORM", version);
            ver.AddLeaf("CPAP", CpapPayload(version));
            ver.AddLeaf("PSND", PsndPayload());
            ver.AddLeaf("CLGT", ClgtPayload());
            ver.AddLeaf("CAMS", CamsPayload());
            ver.AddLeaf("FFBK", FfbkPayload());
            return IffWriter.Write(new MutableIffDocument(clef));
        }

        /// <summary>FORM CLEF { FORM 9999 { opaque } } — an unrecognized version (raw-preserve whole CLEF).</summary>
        public static byte[] BuildUnknownVersion()
        {
            MutableIffNode clef = MutableIffNode.NewContainer("FORM", "CLEF");
            MutableIffNode ver = clef.AddContainer("FORM", "9999");
            ver.AddLeaf("ZZZZ", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            return IffWriter.Write(new MutableIffDocument(clef));
        }

        /// <summary>FORM CLEF { FORM 0003 { CPAP, XXXX (unknown command) } } — unknown command raw-preserve.</summary>
        public static byte[] BuildUnknownCommand()
        {
            MutableIffNode clef = MutableIffNode.NewContainer("FORM", "CLEF");
            MutableIffNode ver = clef.AddContainer("FORM", "0003");
            ver.AddLeaf("CPAP", CpapPayload("0003"));
            ver.AddLeaf("XXXX", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
            return IffWriter.Write(new MutableIffDocument(clef));
        }

        /// <summary>FORM CLEF { FORM 0003 { } } — an empty (zero-command) effect.</summary>
        public static byte[] BuildEmpty()
        {
            MutableIffNode clef = MutableIffNode.NewContainer("FORM", "CLEF");
            clef.AddContainer("FORM", "0003");
            return IffWriter.Write(new MutableIffDocument(clef));
        }

        /// <summary>
        /// FORM CLEF { FORM 0001 { CPAP-truncated } } — a CPAP payload cut mid-C-string (no NUL
        /// terminator before the end). The codec must degrade the CPAP to IsRaw=true and still
        /// round-trip byte-exact (REVIEWS #4). Built by emitting a valid v0001 CPAP then truncating its
        /// payload to a prefix that has no NUL terminator.
        /// </summary>
        public static byte[] BuildTruncatedCpap()
        {
            // A v0001 CPAP whose payload is just the appearance-name bytes WITHOUT the terminator — so
            // ReadCString runs to the end without finding a 0x00 and throws Truncated -> raw degrade.
            byte[] full = CpapPayload("0001");
            // Keep only the first few name bytes (no NUL). "appearance/test.prt" is 19 chars; take 8.
            byte[] cut = new byte[8];
            System.Array.Copy(full, 0, cut, 0, 8);

            MutableIffNode clef = MutableIffNode.NewContainer("FORM", "CLEF");
            MutableIffNode ver = clef.AddContainer("FORM", "0001");
            ver.AddLeaf("CPAP", cut);
            return IffWriter.Write(new MutableIffDocument(clef));
        }

        // ── writer-INDEPENDENT hand-authored hex fixture (REVIEWS #6) ───────────

        /// <summary>
        /// A literal, hand-spelled FORM CLEF { FORM 0003 { CLGT } } authored byte-by-byte (NOT via
        /// IffWriter) so it guards against a shared fixture-builder/writer bug. IFF tags + lengths are
        /// BIG-endian; the 23-byte CLGT payload is 3 uint8 RGB + 5 LE floats. The expected typed CLGT
        /// values are r=1,g=2,b=3 and floats 1.0, 0.0, 0.0, 0.0, 50.0.
        /// </summary>
        public static byte[] BuildHandAuthoredClgtHex()
        {
            // CLGT payload (23 bytes): r,g,b then 5 LE floats.
            var payload = new List<byte> { 0x01, 0x02, 0x03 };
            payload.AddRange(FloatLe(1.0f));   // timeInSeconds
            payload.AddRange(FloatLe(0.0f));   // constantAtt
            payload.AddRange(FloatLe(0.0f));   // linearAtt
            payload.AddRange(FloatLe(0.0f));   // quadraticAtt
            payload.AddRange(FloatLe(50.0f));  // range
            byte[] clgtPayload = payload.ToArray(); // 23 bytes

            // CLGT leaf chunk = tag(4) + BE length(4) + payload.
            var clgt = new List<byte>();
            clgt.AddRange(Ascii("CLGT"));
            clgt.AddRange(Be32((uint)clgtPayload.Length));
            clgt.AddRange(clgtPayload);

            // FORM 0003 container = "FORM" + BE innerLen(4) + "0003" + clgt; innerLen = 4 + clgt.Count.
            var form0003 = new List<byte>();
            form0003.AddRange(Ascii("FORM"));
            form0003.AddRange(Be32((uint)(4 + clgt.Count)));
            form0003.AddRange(Ascii("0003"));
            form0003.AddRange(clgt);

            // FORM CLEF container = "FORM" + BE innerLen(4) + "CLEF" + form0003.
            var clef = new List<byte>();
            clef.AddRange(Ascii("FORM"));
            clef.AddRange(Be32((uint)(4 + form0003.Count)));
            clef.AddRange(Ascii("CLEF"));
            clef.AddRange(form0003);

            return clef.ToArray();
        }

        private static byte[] Ascii(string s)
        {
            var b = new byte[s.Length];
            for (int i = 0; i < s.Length; i++) b[i] = (byte)s[i];
            return b;
        }

        private static byte[] Be32(uint v)
        {
            return new[]
            {
                (byte)((v >> 24) & 0xFF), (byte)((v >> 16) & 0xFF),
                (byte)((v >> 8) & 0xFF), (byte)(v & 0xFF)
            };
        }
    }

    /// <summary>
    /// The committed golden fixture file names (under <c>Fixtures/ClientEffect/</c>) paired with the
    /// builder that produces each one. The test reads the committed file AND asserts it equals the
    /// builder output, so a drift between the committed golden and the deterministic builder is caught.
    /// </summary>
    internal static class ClefFixtureFiles
    {
        public const string Dir = "Fixtures/ClientEffect";

        public static readonly Dictionary<string, System.Func<byte[]>> All =
            new Dictionary<string, System.Func<byte[]>>
        {
            { "clef_v0001_cpap.iff", () => ClefFixtureBuilder.BuildSingleCpap("0001") },
            { "clef_v0002_cpap.iff", () => ClefFixtureBuilder.BuildSingleCpap("0002") },
            { "clef_v0003_cpap.iff", () => ClefFixtureBuilder.BuildSingleCpap("0003") },
            { "clef_v0003_all5.iff", () => ClefFixtureBuilder.BuildAllFive("0003") },
            { "clef_v0001_all5.iff", () => ClefFixtureBuilder.BuildAllFive("0001") },
            { "clef_unknown_version.iff", ClefFixtureBuilder.BuildUnknownVersion },
            { "clef_unknown_command.iff", ClefFixtureBuilder.BuildUnknownCommand },
            { "clef_empty.iff", ClefFixtureBuilder.BuildEmpty },
            { "clef_truncated_cpap.iff", ClefFixtureBuilder.BuildTruncatedCpap },
            { "clef_v0003_clgt_handauthored.hex", ClefFixtureBuilder.BuildHandAuthoredClgtHex },
        };
    }
}
