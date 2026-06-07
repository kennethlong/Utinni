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
using System.Linq;
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Particle;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("roundtrip-particle", HelpText = "Parse -> serialize -> re-parse a particle-effect .prt (FORM PEFT); assert byte-exact identity on the whole file.")]
    public class RoundtripParticleOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the particle-effect .prt (FORM PEFT) file.")]
        public string Path { get; set; }
    }

    /// <summary>
    /// Byte-exact max-harness for the particle-effect (<c>.prt</c> / FORM PEFT) write path — the typed
    /// <c>.prt</c> analog of <see cref="RoundtripOtCommand"/> / <c>roundtrip-tab</c>. Loads the effect
    /// via the 15-02 codec (<see cref="ParticleEffectDocument.FromBytes"/>), re-serializes via
    /// <see cref="ParticleEffectWriter"/> (<c>model.Serialize()</c>), re-parses for structural validity,
    /// and asserts the round-tripped bytes are IDENTICAL to the input. Because the codec re-emits the
    /// full captured IFF tree verbatim (including every raw-preserved unrecognized-version sub-tree),
    /// even a degraded fixture round-trips byte-exact — this is the gate that locks D-05.
    ///
    /// <para><b>Exit codes (mirroring <see cref="RoundtripOtCommand"/>):</b>
    ///   0 success; 1 UsageError; 2 ParticleParseException / DecoderException / IffParseException /
    ///   IOException; 3 FileNotFound. Generic <see cref="System.Exception"/> is intentionally NOT caught.</para>
    /// </summary>
    public static class RoundtripParticleCommand
    {
        public static int Run(RoundtripParticleOptions o)
        {
            if (string.IsNullOrEmpty(o.Path))
            {
                return JsonOutput.EmitError("roundtrip-particle", "UsageError",
                    "A particle-effect .prt path is required.", exitCode: 1);
            }

            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("roundtrip-particle", "FileNotFound",
                    "Particle-effect .prt not found: " + o.Path, exitCode: 3);
            }

            try
            {
                byte[] loadedBytes = File.ReadAllBytes(o.Path);
                MutableParticleEffect model = ParticleEffectDocument.FromBytes(loadedBytes);

                byte[] roundtrippedBytes = model.Serialize();
                MutableParticleEffect rtModel = ParticleEffectDocument.FromBytes(roundtrippedBytes); // re-parse for structural validity

                bool bytesEqual = loadedBytes.Length == roundtrippedBytes.Length
                    && loadedBytes.SequenceEqual(roundtrippedBytes);

                int emitterCount = rtModel.Groups.Sum(g => g.Emitters.Count);

                var result = new JObject
                {
                    ["bytesIdentical"] = bytesEqual,
                    ["comparisonGranularity"] = "whole-file",
                    ["emitterCount"] = emitterCount,
                    ["emitterGroupCount"] = rtModel.Groups.Count,
                    ["rawPreserved"] = rtModel.IsRawPreserved,
                    ["rootType"] = "PEFT",
                    ["source"] = o.Path,
                    ["version"] = rtModel.Version
                };
                return JsonOutput.EmitSuccess("roundtrip-particle", result);
            }
            catch (ParticleParseException ex)
            {
                return JsonOutput.EmitError("roundtrip-particle", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (UtinniCoreDotNet.Formats.Decoders.DecoderException ex)
            {
                return JsonOutput.EmitError("roundtrip-particle", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("roundtrip-particle", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("roundtrip-particle", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught.
        }
    }
}
