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
    [Verb("decode-with-template", HelpText = "Decode ONE template-eligible IFF leaf with a user-definable chunk template (resolved from a pack or supplied explicitly), emitting a navigable decode envelope plus the byte-exact fit report.")]
    public class DecodeWithTemplateOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .iff file carrying the template-eligible leaf.")]
        public string Path { get; set; }

        [Option("template", HelpText = "Path to a single template .json to apply (skips pack auto-resolution).")]
        public string TemplatePath { get; set; }

        [Option("templates-dir", HelpText = "Optional project-local pack directory added to the default scanned allow-list for auto-resolution.")]
        public string TemplatesDir { get; set; }

        [Option("leaf", HelpText = "Stable id of the leaf to decode; required only when more than one leaf is eligible (auto-resolves the single eligible leaf otherwise).")]
        public string LeafId { get; set; }
    }

    /// <summary>
    /// The verbs-first descriptive read for a user-definable IFF chunk template (D-15 / PROD-IFFT-02):
    /// <c>decode-with-template &lt;path&gt;</c> loads the template pack (or the <c>--template</c> file),
    /// resolves the template-eligible leaf, decodes it through <see cref="KernelCodec"/>, and emits a
    /// navigable envelope carrying the decoded field values AND the <see cref="FitReport"/> (D-07 — a
    /// key-matched-but-non-round-tripping template is surfaced does-not-fit, never silently trusted).
    ///
    /// <para>The envelope is produced by the SHARED <see cref="TemplateDecodeShared.BuildDecodeEnvelope"/>
    /// builder so the explicit-template path, the auto-resolution path, and the roundtrip-template verb
    /// cannot drift (the alias-delegation precedent of <c>decode-trn</c>).</para>
    ///
    /// <para><b>Exit codes:</b> 0 success (incl. a clear no-match envelope); 2 TemplateException /
    /// DecoderException / IffParseException / IOException; 3 FileNotFound. Generic
    /// <see cref="System.Exception"/> intentionally NOT caught.</para>
    /// </summary>
    public static class DecodeWithTemplateCommand
    {
        public static int Run(DecodeWithTemplateOptions o)
        {
            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("decode-with-template", "FileNotFound",
                    "IFF file not found: " + o.Path, exitCode: 3);
            }
            if (!string.IsNullOrEmpty(o.TemplatePath) && !File.Exists(o.TemplatePath))
            {
                return JsonOutput.EmitError("decode-with-template", "FileNotFound",
                    "template .json not found: " + o.TemplatePath, exitCode: 3);
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(o.Path);
                MutableIffDocument mutable = TemplateDecodeShared.LoadMutable(bytes);

                List<TemplateModel> pool = TemplateDecodeShared.LoadTemplatePool(o.TemplatePath, o.TemplatesDir);

                object envelope = TemplateDecodeShared.BuildDecodeEnvelope(mutable, pool, o.LeafId, o.Path);
                return JsonOutput.EmitSuccess("decode-with-template", envelope);
            }
            catch (TemplateException ex)
            {
                return JsonOutput.EmitError("decode-with-template", "TemplateError", ex.Message, exitCode: 2);
            }
            catch (DecoderException ex)
            {
                return JsonOutput.EmitError("decode-with-template", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("decode-with-template", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("decode-with-template", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception intentionally NOT caught.
        }
    }

    /// <summary>
    /// SHARED template-verb plumbing: the ONE pack-load + resolve + decode-envelope builder consumed by
    /// <see cref="DecodeWithTemplateCommand"/> and <see cref="RoundtripTemplateCommand"/> so the two verbs
    /// (and a future <c>decode-iff --template</c> branch) provably cannot fork their decode logic.
    /// </summary>
    internal static class TemplateDecodeShared
    {
        // Parses bytes into a mutable IFF document (the resolver's edit source). Throws IffParseException
        // on malformed framing (surfaced as exit 2 by the caller).
        public static MutableIffDocument LoadMutable(byte[] bytes)
        {
            IffDocument doc;
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                doc = IffReader.Read(ms);
            }
            return MutableIffDocument.FromDocument(doc, bytes);
        }

        // Builds the candidate template pool: an explicit --template wins outright; otherwise the D-12
        // scanned allow-list (shipped -> app-data -> optional project-local) supplies the auto-resolution
        // pool. A malformed --template throws TemplateException (surfaced exit 2).
        public static List<TemplateModel> LoadTemplatePool(string explicitTemplatePathOrNull, string templatesDirOrNull)
        {
            if (!string.IsNullOrEmpty(explicitTemplatePathOrNull))
            {
                string json = File.ReadAllText(explicitTemplatePathOrNull);
                return new List<TemplateModel> { TemplateJson.Deserialize(json) };
            }
            return TemplatePackStore.DefaultRoots(templatesDirOrNull).LoadEffective();
        }

        // Resolves a leaf to decode: an explicit --leaf id is used verbatim; otherwise the single eligible
        // leaf is auto-picked. Returns the chosen stable id, or null with a reason when no unique leaf is
        // resolvable (the caller emits a no-match envelope, NOT a crash).
        public static string PickLeafStableId(MutableIffDocument doc, List<TemplateModel> pool,
            string explicitLeafOrNull, out string reason)
        {
            reason = null;
            if (!string.IsNullOrEmpty(explicitLeafOrNull)) return explicitLeafOrNull;

            var resolver = new TemplateResolver(pool);
            var eligible = new List<string>();
            foreach (KeyValuePair<string, string> leaf in TemplateResolver.EnumerateLeafStableIds(doc))
            {
                TemplateResolution r = resolver.Resolve(doc, leaf.Key);
                if (r.Best != null) eligible.Add(leaf.Key);
            }

            if (eligible.Count == 1) return eligible[0];
            if (eligible.Count == 0)
            {
                reason = "no template-eligible leaf matched the loaded template pool";
                return null;
            }
            reason = "more than one eligible leaf; pass --leaf <stableId> to select one ("
                + string.Join(", ", eligible) + ")";
            return null;
        }

        // The SHARED decode envelope. Resolves the leaf's template (D-04/D-05/D-07), decodes it, and
        // projects the decoded values + fit report. A no-match (or built-in-suppressed, or ambiguous) leaf
        // yields a clear matched=false envelope rather than an exception.
        public static object BuildDecodeEnvelope(MutableIffDocument doc, List<TemplateModel> pool,
            string explicitLeafOrNull, string sourcePath)
        {
            string leafId = PickLeafStableId(doc, pool, explicitLeafOrNull, out string reason);
            if (leafId == null)
            {
                return new
                {
                    leaf = (string)null,
                    matched = false,
                    reason = reason,
                    source = sourcePath,
                    type = "template-decode"
                };
            }

            var resolver = new TemplateResolver(pool);
            TemplateResolution resolution = resolver.Resolve(doc, leafId);

            if (resolution.SuppressedByBuiltin)
            {
                return new
                {
                    leaf = leafId,
                    matched = false,
                    reason = "the root FORM has a built-in decoder (D-05 altitude); a template never engages here",
                    source = sourcePath,
                    type = "template-decode"
                };
            }
            if (resolution.Best == null)
            {
                return new
                {
                    leaf = leafId,
                    matched = false,
                    reason = "no template in the pool matched this leaf's version-FORM-aware key",
                    source = sourcePath,
                    type = "template-decode"
                };
            }

            TemplateModel template = resolution.Best.Template;
            byte[] payload = FindLeafPayload(doc, leafId);
            DecodedTemplate decoded = KernelCodec.Decode(template, payload);

            return new
            {
                ambiguousCandidateCount = resolution.Candidates.Count,
                fit = BuildFit(resolution.Best.Fit),
                fits = resolution.Best.Fits,
                leaf = leafId,
                matchAncestorPath = template.MatchAncestorPath,
                matchLeafTag = template.MatchLeafTag,
                matched = true,
                source = sourcePath,
                type = "template-decode",
                values = ProjectValues(decoded.Values),
                versionFormAware = resolution.Best.VersionFormAware
            };
        }

        private static object BuildFit(FitReport fit)
        {
            if (fit == null) return null;
            return new
            {
                bytesConsumed = fit.BytesConsumed,
                bytesTotal = fit.BytesTotal,
                consumedExactly = fit.ConsumedExactly,
                perField = (fit.PerField ?? new List<FieldPlausibility>())
                    .Select(p => new { name = p.FieldName, plausible = p.Plausible })
                    .ToList()
            };
        }

        // Projects decoded values to a JSON-friendly shape: byte[] spans become hex (so the envelope is
        // printable and the raw spans round-trip out of band), nested maps/lists recurse.
        private static object ProjectValues(Dictionary<string, object> values)
        {
            var into = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> kv in values)
            {
                into[kv.Key] = ProjectValue(kv.Value);
            }
            return into;
        }

        private static object ProjectValue(object v)
        {
            if (v is byte[] span) return ToHex(span);
            if (v is Dictionary<string, object> map) return ProjectValues(map);
            if (v is List<object> list) return list.Select(ProjectValue).ToList();
            return v;
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new System.Text.StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        // Returns the leaf payload bytes for a stable id (the resolver located it; we re-walk to fetch the
        // payload copy for the codec). Throws TemplateException if the id is not addressable.
        public static byte[] FindLeafPayload(MutableIffDocument doc, string leafStableId)
        {
            byte[] found = FindLeafPayloadRecursive(doc.Root, "", 0, leafStableId);
            if (found == null)
            {
                throw new TemplateException("leaf id not addressable in document: " + leafStableId);
            }
            return found;
        }

        private static byte[] FindLeafPayloadRecursive(MutableIffNode node, string parentPrefix, int ordinal,
            string targetId)
        {
            string thisId = MutableIffDocument.DeriveStableId(node, parentPrefix, ordinal);
            if (node.Kind == MutableIffNodeKind.Leaf && thisId == targetId) return node.GetPayloadCopy();
            if (node.Kind == MutableIffNodeKind.Container)
            {
                string childPrefix = thisId + "/";
                for (int i = 0; i < node.Children.Count; i++)
                {
                    byte[] found = FindLeafPayloadRecursive(node.Children[i], childPrefix, i, targetId);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}
