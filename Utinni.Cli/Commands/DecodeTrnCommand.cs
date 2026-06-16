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
using CommandLine;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Terrain;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("decode-trn", HelpText = "Decode a terrain .trn (FORM TGEN / PTAT) to a navigable JSON terrain envelope.")]
    public class DecodeTrnOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the terrain .trn file.")]
        public string Path { get; set; }
    }

    /// <summary>
    /// Thin discoverability ALIAS for the terrain decode path: <c>decode-trn &lt;path&gt;</c> emits the SAME
    /// navigable terrain envelope as <c>decode-iff</c> on a TGEN root (Claude's Discretion — an alias, not a
    /// fork). Delegates to <see cref="TerrainDocument.FromBytes"/> + <see cref="DecodeIffCommand.BuildTerrainResult"/>
    /// so the two verbs provably cannot drift.
    ///
    /// <para><b>Exit codes:</b> 0 success; 2 TerrainParseException / DecoderException / IffParseException /
    /// IOException; 3 FileNotFound. Generic <see cref="System.Exception"/> intentionally NOT caught.</para>
    /// </summary>
    public static class DecodeTrnCommand
    {
        public static int Run(DecodeTrnOptions o)
        {
            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("decode-trn", "FileNotFound",
                    "Terrain .trn not found: " + o.Path, exitCode: 3);
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(o.Path);
                TerrainDocument doc = TerrainDocument.FromBytes(bytes);
                return JsonOutput.EmitSuccess("decode-trn", DecodeIffCommand.BuildTerrainResult(doc, o.Path));
            }
            catch (TerrainParseException ex)
            {
                return JsonOutput.EmitError("decode-trn", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (DecoderException ex)
            {
                return JsonOutput.EmitError("decode-trn", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("decode-trn", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("decode-trn", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception intentionally NOT caught.
        }
    }
}
