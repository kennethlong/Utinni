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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Template;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("roundtrip-template", HelpText = "Decode then re-encode ONE template-eligible IFF leaf through its chunk template and assert the leaf payload is byte-identical (the DEC-C3 byte-exact gate).")]
    public class RoundtripTemplateOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .iff file carrying the template-eligible leaf.")]
        public string Path { get; set; }

        [Option("template", HelpText = "Path to a single template .json to apply (skips pack auto-resolution).")]
        public string TemplatePath { get; set; }

        [Option("templates-dir", HelpText = "Optional project-local pack directory added to the default scanned allow-list for auto-resolution.")]
        public string TemplatesDir { get; set; }

        [Option("leaf", HelpText = "Stable id of the leaf to round-trip; auto-resolves the single eligible leaf otherwise.")]
        public string LeafId { get; set; }
    }

    /// <summary>
    /// The byte-exact round-trip gate (D-15 / DEC-C3 / PROD-IFFT-02): <c>roundtrip-template &lt;path&gt;</c>
    /// resolves a template-eligible leaf (via the SHARED <see cref="TemplateDecodeShared"/> pool/resolver),
    /// decodes its payload with <see cref="KernelCodec.Decode"/>, re-encodes via <see cref="KernelCodec.Encode"/>,
    /// and asserts the re-encoded payload bytes are IDENTICAL to the originals. A byte-exact pass exits 0
    /// (<c>bytesIdentical=true</c>); a mismatch exits 2 (a corrupted fixture / wrong-layout template surfaces
    /// here, NOT silently).
    ///
    /// <para><b>Exit codes:</b> 0 byte-identical; 2 TemplateException / DecoderException / IffParseException /
    /// IOException / byte-mismatch / no-match; 3 FileNotFound. Generic <see cref="System.Exception"/>
    /// intentionally NOT caught.</para>
    /// </summary>
    public static class RoundtripTemplateCommand
    {
        public static int Run(RoundtripTemplateOptions o)
        {
            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("roundtrip-template", "FileNotFound",
                    "IFF file not found: " + o.Path, exitCode: 3);
            }
            if (!string.IsNullOrEmpty(o.TemplatePath) && !File.Exists(o.TemplatePath))
            {
                return JsonOutput.EmitError("roundtrip-template", "FileNotFound",
                    "template .json not found: " + o.TemplatePath, exitCode: 3);
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(o.Path);
                MutableIffDocument mutable = TemplateDecodeShared.LoadMutable(bytes);
                List<TemplateModel> pool = TemplateDecodeShared.LoadTemplatePool(o.TemplatePath, o.TemplatesDir);

                string leafId = TemplateDecodeShared.PickLeafStableId(mutable, pool, o.LeafId, out string reason);
                if (leafId == null)
                {
                    return JsonOutput.EmitError("roundtrip-template", "NoMatch", reason, exitCode: 2);
                }

                var resolver = new TemplateResolver(pool);
                TemplateResolution resolution = resolver.Resolve(mutable, leafId);
                if (resolution.SuppressedByBuiltin)
                {
                    return JsonOutput.EmitError("roundtrip-template", "NoMatch",
                        "the root FORM has a built-in decoder (D-05 altitude); a template never engages here", exitCode: 2);
                }
                if (resolution.Best == null)
                {
                    return JsonOutput.EmitError("roundtrip-template", "NoMatch",
                        "no template in the pool matched the leaf " + leafId, exitCode: 2);
                }

                TemplateModel template = resolution.Best.Template;
                byte[] originalPayload = TemplateDecodeShared.FindLeafPayload(mutable, leafId);

                DecodedTemplate decoded = KernelCodec.Decode(template, originalPayload);
                byte[] reEncoded = KernelCodec.Encode(decoded);

                bool identical = originalPayload.Length == reEncoded.Length
                    && originalPayload.SequenceEqual(reEncoded);

                if (!identical)
                {
                    return JsonOutput.EmitError("roundtrip-template", "ByteMismatch",
                        "decode->encode is NOT byte-identical for leaf " + leafId
                        + " (original " + originalPayload.Length + "B vs re-encoded " + reEncoded.Length + "B); "
                        + "nothing is written, but the template does not byte-exactly round-trip this leaf.", exitCode: 2);
                }

                var result = new
                {
                    bytesIdentical = true,
                    leaf = leafId,
                    matchLeafTag = template.MatchLeafTag,
                    payloadBytes = originalPayload.Length,
                    source = o.Path,
                    type = "template-roundtrip"
                };
                return JsonOutput.EmitSuccess("roundtrip-template", result);
            }
            catch (TemplateException ex)
            {
                return JsonOutput.EmitError("roundtrip-template", "TemplateError", ex.Message, exitCode: 2);
            }
            catch (DecoderException ex)
            {
                return JsonOutput.EmitError("roundtrip-template", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("roundtrip-template", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("roundtrip-template", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception intentionally NOT caught.
        }
    }
}
