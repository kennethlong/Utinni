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
using UtinniCoreDotNet.Formats.ClientEffect;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("decode-effect", HelpText = "Decode a ClientEffect .iff (FORM CLEF) to a JSON command-list envelope (per-command stableId).")]
    public class DecodeEffectOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the ClientEffect .iff (FORM CLEF) file.")]
        public string Path { get; set; }
    }

    /// <summary>
    /// Thin discoverability ALIAS for the ClientEffect decode path: <c>decode-effect &lt;path&gt;</c> emits
    /// the SAME command-list envelope as <c>decode-iff</c> on a FORM CLEF root (cf. <see cref="DecodeTrnCommand"/>
    /// → <see cref="DecodeIffCommand.BuildTerrainResult"/>). Delegates to <see cref="ClientEffectDocument.FromBytes"/>
    /// + <see cref="DecodeIffCommand.BuildClefResult"/> so the two verbs provably cannot drift; the emitted
    /// envelope carries a per-command <c>stableId</c> (REVIEWS HIGH #1).
    ///
    /// <para><b>Exit codes:</b> 0 success; 2 ClientEffectParseException / DecoderException / IffParseException /
    /// IOException; 3 FileNotFound. Generic <see cref="System.Exception"/> intentionally NOT caught.</para>
    /// </summary>
    public static class DecodeEffectCommand
    {
        public static int Run(DecodeEffectOptions o)
        {
            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("decode-effect", "FileNotFound",
                    "ClientEffect .iff not found: " + o.Path, exitCode: 3);
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(o.Path);
                MutableClientEffect model = ClientEffectDocument.FromBytes(bytes);
                return JsonOutput.EmitSuccess("decode-effect", DecodeIffCommand.BuildClefResult(model, o.Path));
            }
            catch (ClientEffectParseException ex)
            {
                return JsonOutput.EmitError("decode-effect", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (DecoderException ex)
            {
                return JsonOutput.EmitError("decode-effect", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("decode-effect", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("decode-effect", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception intentionally NOT caught.
        }
    }
}
