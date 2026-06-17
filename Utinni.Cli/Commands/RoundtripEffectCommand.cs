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
using UtinniCoreDotNet.Formats.ClientEffect;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("roundtrip-effect", HelpText = "Parse -> serialize -> re-parse a ClientEffect .iff (FORM CLEF); assert byte-exact identity on the whole file.")]
    public class RoundtripEffectOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the ClientEffect .iff (FORM CLEF) file.")]
        public string Path { get; set; }
    }

    /// <summary>
    /// Byte-exact max-harness for the ClientEffect (<c>.iff</c> / FORM CLEF) write path — the CLEF analog
    /// of <see cref="RoundtripParticleCommand"/>. Loads via the Plan 01 codec
    /// (<see cref="ClientEffectDocument.FromBytes"/>), re-serializes (<c>model.Serialize()</c> ==
    /// <c>IffWriter.Write(SourceIff)</c>), re-parses for structural validity, and asserts the round-tripped
    /// bytes are IDENTICAL to the input. Because the codec re-emits the full captured IFF tree verbatim
    /// (including every raw-preserved unrecognized-version / raw-command sub-tree), even a degraded fixture
    /// round-trips byte-exact — the gate that locks D-01/D-13.
    ///
    /// <para><b>Exit codes:</b> 0 success; 1 UsageError; 2 ClientEffectParseException / DecoderException /
    /// IffParseException / IOException; 3 FileNotFound. Generic <see cref="System.Exception"/> intentionally
    /// NOT caught.</para>
    /// </summary>
    public static class RoundtripEffectCommand
    {
        public static int Run(RoundtripEffectOptions o)
        {
            if (string.IsNullOrEmpty(o.Path))
            {
                return JsonOutput.EmitError("roundtrip-effect", "UsageError",
                    "A ClientEffect .iff path is required.", exitCode: 1);
            }

            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("roundtrip-effect", "FileNotFound",
                    "ClientEffect .iff not found: " + o.Path, exitCode: 3);
            }

            try
            {
                byte[] loadedBytes = File.ReadAllBytes(o.Path);
                MutableClientEffect model = ClientEffectDocument.FromBytes(loadedBytes);

                byte[] roundtrippedBytes = model.Serialize();
                MutableClientEffect rtModel = ClientEffectDocument.FromBytes(roundtrippedBytes); // re-parse for structural validity

                bool bytesEqual = loadedBytes.Length == roundtrippedBytes.Length
                    && loadedBytes.SequenceEqual(roundtrippedBytes);

                var result = new JObject
                {
                    ["bytesIdentical"] = bytesEqual,
                    ["commandCount"] = rtModel.Commands.Count,
                    ["comparisonGranularity"] = "whole-file",
                    ["rawPreserved"] = rtModel.IsRawPreserved,
                    ["rootType"] = "CLEF",
                    ["source"] = o.Path,
                    ["version"] = rtModel.Version
                };
                return JsonOutput.EmitSuccess("roundtrip-effect", result);
            }
            catch (ClientEffectParseException ex)
            {
                return JsonOutput.EmitError("roundtrip-effect", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (DecoderException ex)
            {
                return JsonOutput.EmitError("roundtrip-effect", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("roundtrip-effect", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("roundtrip-effect", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught.
        }
    }
}
