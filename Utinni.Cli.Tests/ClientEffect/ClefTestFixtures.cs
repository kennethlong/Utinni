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
// Format understood by reading swg-client-v2 .../clientGame/.../ClientEffectTemplate.cpp (SOE/Bootprint,
// All Rights Reserved). No code, comments, identifier names, or test fixtures copied from any reference
// source. Implementation original to Utinni under MIT.

using UtinniCoreDotNet.Formats.ClientEffect;
using UtinniCoreDotNet.Formats.Iff;

namespace Utinni.Cli.Tests.ClientEffect
{
    /// <summary>
    /// Synthesizes deterministic FORM CLEF <c>.iff</c> fixtures through the framework IFF primitives +
    /// <see cref="ClefFieldCodec"/> command-payload encoders (the same compose-through-the-writer idiom the
    /// codec test project's <c>ClefFixtureBuilder</c> uses — re-stated here because that builder lives in a
    /// sibling test assembly). Building through the writer makes every fixture canonical-by-construction.
    /// </summary>
    internal static class ClefTestFixtures
    {
        public const string AppearanceName = "appearance/test.prt";
        public const string SoundName = "sound/test.snd";
        public const string FfFile = "forcefeedback/test.ffe";

        public static byte[] CpapPayload(string version)
        {
            switch (version)
            {
                case "0001": return ClefFieldCodec.EncodeCpap("0001", AppearanceName, 2.5f, null, null, null, null, null);
                case "0002": return ClefFieldCodec.EncodeCpap("0002", AppearanceName, 2.5f, true, null, null, null, null);
                default:      return ClefFieldCodec.EncodeCpap("0003", AppearanceName, 2.5f, true, 0.5f, 1.5f, 1.0f, 2.0f);
            }
        }

        /// <summary>FORM CLEF { FORM &lt;version&gt; { CPAP, PSND, CLGT, CAMS, FFBK } } — all five commands.</summary>
        public static byte[] BuildAllFive(string version)
        {
            MutableIffNode clef = MutableIffNode.NewContainer("FORM", "CLEF");
            MutableIffNode ver = clef.AddContainer("FORM", version);
            ver.AddLeaf("CPAP", CpapPayload(version));
            ver.AddLeaf("PSND", ClefFieldCodec.EncodePsnd(SoundName));
            ver.AddLeaf("CLGT", ClefFieldCodec.EncodeClgt(10, 20, 30, 1.0f, 0.1f, 0.2f, 0.3f, 25.0f));
            ver.AddLeaf("CAMS", ClefFieldCodec.EncodeCams(0.5f, 4.0f, 1.5f, 12.0f));
            ver.AddLeaf("FFBK", ClefFieldCodec.EncodeFfbk(FfFile, 3, 7.5f));
            return IffWriter.Write(new MutableIffDocument(clef));
        }

        /// <summary>FORM CLEF { FORM &lt;version&gt; { CPAP } } — a single CPAP at the given version.</summary>
        public static byte[] BuildSingleCpap(string version)
        {
            MutableIffNode clef = MutableIffNode.NewContainer("FORM", "CLEF");
            MutableIffNode ver = clef.AddContainer("FORM", version);
            ver.AddLeaf("CPAP", CpapPayload(version));
            return IffWriter.Write(new MutableIffDocument(clef));
        }

        /// <summary>FORM CLEF { FORM 0003 { CPAP, XXXX (unknown command -> raw-fallback) } }.</summary>
        public static byte[] BuildWithRawCommand()
        {
            MutableIffNode clef = MutableIffNode.NewContainer("FORM", "CLEF");
            MutableIffNode ver = clef.AddContainer("FORM", "0003");
            ver.AddLeaf("CPAP", CpapPayload("0003"));
            ver.AddLeaf("XXXX", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
            return IffWriter.Write(new MutableIffDocument(clef));
        }
    }
}
