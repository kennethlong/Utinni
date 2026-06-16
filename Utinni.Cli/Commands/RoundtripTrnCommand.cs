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
// Implementation original to Utinni under MIT.

using System.IO;
using System.Linq;
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Terrain;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("roundtrip-trn", HelpText = "Parse -> serialize -> re-parse a terrain .trn (FORM TGEN / PTAT); assert byte-exact identity on the whole file.")]
    public class RoundtripTrnOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the terrain .trn (FORM TGEN, optionally PTAT-wrapped) file.")]
        public string Path { get; set; }
    }

    /// <summary>
    /// Byte-exact max-harness for the terrain (<c>.trn</c> / FORM TGEN) write path — the terrain analog of
    /// <see cref="RoundtripParticleCommand"/>. Loads via the Plan-02 codec
    /// (<see cref="TerrainDocument.FromBytes"/>), re-serializes via the held <see cref="MutableIffDocument"/>
    /// (<c>Serialize()</c> = <see cref="IffWriter"/> verbatim re-emit of clean nodes), re-parses for
    /// structural validity, and asserts the round-tripped bytes are IDENTICAL to the input. Because the codec
    /// re-emits the full captured IFF tree verbatim (including every raw-preserved / DEAD sub-tree), even a
    /// degraded fixture round-trips byte-exact — this is the DEC-C3 codec-level gate (TRN-03/04).
    ///
    /// <para><b>Exit codes:</b> 0 success (byte-identical); 1 UsageError; 2 VerifyFailed (NOT byte-identical)
    /// / TerrainParseException / DecoderException / IffParseException / IOException; 3 FileNotFound. Generic
    /// <see cref="System.Exception"/> NOT caught. A non-identical round-trip fails closed (WR-01).</para>
    /// </summary>
    public static class RoundtripTrnCommand
    {
        // Test-only seam: perturb the round-tripped bytes before the byte-identity compare (mirrors
        // ApplySaveTrnCommand.TestPerturbSerialized). Lets a test force a NON-identical round-trip to
        // assert the WR-01 fail-closed exit path without shipping a deliberately-broken codec.
        internal static System.Func<byte[], byte[]> TestPerturbRoundtripped;

        public static int Run(RoundtripTrnOptions o)
        {
            if (string.IsNullOrEmpty(o.Path))
            {
                return JsonOutput.EmitError("roundtrip-trn", "UsageError",
                    "A terrain .trn path is required.", exitCode: 1);
            }

            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("roundtrip-trn", "FileNotFound",
                    "Terrain .trn not found: " + o.Path, exitCode: 3);
            }

            try
            {
                byte[] loadedBytes = File.ReadAllBytes(o.Path);
                TerrainDocument model = TerrainDocument.FromBytes(loadedBytes);

                byte[] roundtrippedBytes = model.Serialize();
                if (TestPerturbRoundtripped != null) roundtrippedBytes = TestPerturbRoundtripped(roundtrippedBytes);
                TerrainDocument rtModel = TerrainDocument.FromBytes(roundtrippedBytes); // re-parse for validity

                bool bytesEqual = loadedBytes.Length == roundtrippedBytes.Length
                    && loadedBytes.SequenceEqual(roundtrippedBytes);

                var result = new JObject
                {
                    ["bytesIdentical"] = bytesEqual,
                    ["comparisonGranularity"] = "whole-file",
                    ["layerCount"] = rtModel.Layers.Count,
                    ["rootType"] = "TGEN",
                    ["source"] = o.Path
                };

                // Fail closed: DEC-C3 byte-exact is THE codec gate for this phase, so a non-identical
                // round-trip MUST exit nonzero — a CI/agent gating on exit code would otherwise treat a
                // byte-exact regression as a pass (WR-01). Exit 2 = VerifyFailed, matching the sibling
                // apply-save-trn verb's verify-failure band (FileNotFound owns 3 here, so VerifyFailed
                // reuses the existing 2 "structural/verify" band rather than overloading 3).
                if (!bytesEqual)
                {
                    return JsonOutput.EmitError("roundtrip-trn", "VerifyFailed",
                        "round-trip was not byte-identical (whole-file); the codec write path regressed.",
                        exitCode: 2);
                }
                return JsonOutput.EmitSuccess("roundtrip-trn", result);
            }
            catch (TerrainParseException ex)
            {
                return JsonOutput.EmitError("roundtrip-trn", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (DecoderException ex)
            {
                return JsonOutput.EmitError("roundtrip-trn", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("roundtrip-trn", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("roundtrip-trn", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught.
        }
    }
}
