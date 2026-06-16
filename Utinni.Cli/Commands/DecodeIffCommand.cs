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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("decode-iff", HelpText = "Decode a typed IFF asset (datatable / string-table / object-template) to JSON.")]
    public class DecodeIffOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .iff/.tab/.stf file.")]
        public string Path { get; set; }
    }

    /// <summary>
    /// Reads an IFF asset via the shared <see cref="IffReader"/>, dispatches on its root FORM tag to
    /// the matching per-type decoder in <c>UtinniCoreDotNet.Formats.Decoders</c>, and emits the
    /// decoded model as a sorted-key schemaVersion:1 JSON envelope via <see cref="JsonOutput"/>.
    ///
    /// <para>This verb is the golden-test harness for the decoders — the TRE Browser detail pane
    /// (07-04b) calls the SAME decoders over the SAME <see cref="IffReader"/> output, so the CLI and
    /// the UI never drift (success criterion #4; Pitfall 7).</para>
    /// </summary>
    public static class DecodeIffCommand
    {
        public static int Run(DecodeIffOptions o)
        {
            // FileNotFound check: exit 3 (D-02), mirroring inspect-iff.
            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("decode-iff", "FileNotFound",
                    "IFF file not found: " + o.Path, exitCode: 3);
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(o.Path);

                // A .stf string table is NOT IFF-wrapped — it is a raw magic+version binary. Sniff the
                // leading magic and decode the raw bytes before handing anything to the IFF parser.
                if (StringTableDecoder.LooksLikeStf(bytes))
                {
                    return JsonOutput.EmitSuccess("decode-iff",
                        BuildStringTableResult(StringTableDecoder.Decode(bytes), o.Path));
                }

                // SWG UI pages (.gui) are TEXT, not IFF — recognize them by the path/extension hint
                // and emit a text classification rather than feeding non-IFF bytes to the parser.
                if (!StartsWithIffContainer(bytes) && IffStructureSummary.IsUiPagePath(o.Path))
                {
                    return JsonOutput.EmitSuccess("decode-iff", BuildUiPageTextResult(o.Path, bytes));
                }

                var doc = IffReader.Read(new MemoryStream(bytes));

                // A particle-effect (FORM PEFT) root auto-dispatches into the 15-02 particle codec.
                // It needs the source bytes (the typed model captures the raw IFF tree for byte-exact
                // re-emit), so it is handled here rather than in TryDecode. Mirrors how the other typed
                // reads route by root FORM — PEFT is just a new branch (PATTERNS.md MCP note).
                if ((doc.Root as IffContainerChunk)?.SubTypeId == "PEFT")
                {
                    return JsonOutput.EmitSuccess("decode-iff",
                        BuildParticleResult(
                            UtinniCoreDotNet.Formats.Particle.ParticleEffectDocument.FromIff(doc, bytes), o.Path));
                }

                // A terrain template (FORM TGEN directly, or PTAT wrapping a TGEN) auto-dispatches into the
                // Plan-02 terrain codec and emits a NAVIGABLE terrain envelope (layer tree + palettes + typed/
                // raw/dead children) — MCP routing for free (#9). Mirrors the PEFT branch above.
                if (UtinniCoreDotNet.Formats.Decoders.TgenDecoder.LooksLikeTerrain(doc.Root))
                {
                    return JsonOutput.EmitSuccess("decode-iff",
                        BuildTerrainResult(
                            UtinniCoreDotNet.Formats.Terrain.TerrainDocument.FromIff(doc, bytes), o.Path));
                }

                object result;
                if (!TryDecode(doc, o.Path, out result, out string unsupportedTag))
                {
                    return JsonOutput.EmitError("decode-iff", "UnsupportedForm",
                        "No structured decoder for root form '" + unsupportedTag + "'.", exitCode: 2);
                }
                return JsonOutput.EmitSuccess("decode-iff", result);
            }
            catch (UtinniCoreDotNet.Formats.Particle.ParticleParseException ex)
            {
                return JsonOutput.EmitError("decode-iff", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (UtinniCoreDotNet.Formats.Terrain.TerrainParseException ex)
            {
                return JsonOutput.EmitError("decode-iff", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (DecoderException ex)
            {
                return JsonOutput.EmitError("decode-iff", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("decode-iff", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("decode-iff", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception intentionally NOT caught — unexpected types bubble for diagnosis.
        }

        /// <summary>
        /// Dispatches on the root FORM sub-type tag to the matching decoder and builds the result
        /// projection. Returns false (with the offending tag) when no decoder matches.
        /// </summary>
        private static bool TryDecode(IffDocument doc, string sourcePath, out object result, out string unsupportedTag)
        {
            result = null;
            unsupportedTag = null;

            var root = doc.Root as IffContainerChunk;
            string subType = root?.SubTypeId;

            if (subType == DataTableDecoder.RootSubType)
            {
                result = BuildDataTableResult(DataTableDecoder.Decode(doc), sourcePath);
                return true;
            }

            // Mesh / skeletal-mesh / skeleton / animation -> structural-count summary.
            if (subType == "MESH" || subType == "SKMG" || subType == "SKTM"
                || subType == "KFAT" || subType == "CKAT")
            {
                var appearance = AppearanceSummary.Summarize(doc);
                if (appearance != null)
                {
                    result = BuildAppearanceResult(appearance, sourcePath);
                    return true;
                }
            }

            // Shader (SSHT/CSHD) -> lightweight FORM-tag + child-count structure summary.
            if (subType == "SSHT" || subType == "CSHD")
            {
                result = BuildStructureResult(IffStructureSummary.Summarize(doc, sourcePath), sourcePath);
                return true;
            }

            if (ObjectTemplateDecoder.LooksLikeObjectTemplate(doc.Root))
            {
                result = BuildObjectTemplateResult(ObjectTemplateDecoder.Decode(doc), sourcePath);
                return true;
            }

            unsupportedTag = DescribeRoot(doc.Root);
            return false;
        }

        // True when the bytes begin with an EA-IFF-85 container tag (FORM/LIST/CAT ). Used to keep
        // non-IFF text assets (.gui UI pages) out of the IFF parser.
        private static bool StartsWithIffContainer(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4) return false;
            string tag = System.Text.Encoding.ASCII.GetString(bytes, 0, 4);
            return tag == "FORM" || tag == "LIST" || tag == "CAT ";
        }

        private static object BuildDataTableResult(DataTableView dt, string sourcePath)
        {
            return new
            {
                columns = dt.Columns
                    .Select(c => new { kind = c.Kind.ToString(), name = c.Name, typeSpec = c.TypeSpec })
                    .ToList(),
                rowCount = dt.Rows.Count,
                rows = dt.Rows,
                source = sourcePath,
                type = "datatable",
                version = dt.Version
            };
        }

        private static object BuildStringTableResult(StfTable stf, string sourcePath)
        {
            return new
            {
                entries = stf.Entries
                    .Select(e => new { id = e.Id, name = e.Name, text = e.Text })
                    .ToList(),
                entryCount = stf.Entries.Count,
                source = sourcePath,
                type = "stringtable",
                version = stf.Version
            };
        }

        private static object BuildObjectTemplateResult(ObjectTemplateView ot, string sourcePath)
        {
            return new
            {
                baseTemplate = ot.BaseTemplate,
                fields = ot.Fields
                    .Select(f => new { inheritedFrom = f.InheritedFrom, name = f.Name, value = f.Value })
                    .ToList(),
                rootType = ot.RootType,
                source = sourcePath,
                type = "objecttemplate",
                version = ot.Version
            };
        }

        // Particle-effect (FORM PEFT) summary: root version, emitter-group / emitter counts, the
        // raw-preserve disposition, and per-group emitter counts + raw-preserved-emitter tally. The
        // typed views are a non-destructive overlay (D-05); a raw-preserved effect still summarizes.
        private static object BuildParticleResult(
            UtinniCoreDotNet.Formats.Particle.MutableParticleEffect effect, string sourcePath)
        {
            int rawPreservedEmitters = 0;
            var groups = new System.Collections.Generic.List<object>();
            foreach (var g in effect.Groups)
            {
                int rawInGroup = 0;
                foreach (var e in g.Emitters)
                {
                    if (e.IsRawPreserved) { rawInGroup++; rawPreservedEmitters++; }
                }
                groups.Add(new
                {
                    emitterCount = g.Emitters.Count,
                    rawPreservedEmitters = rawInGroup
                });
            }

            int emitterCount = 0;
            foreach (var g in effect.Groups) emitterCount += g.Emitters.Count;

            return new
            {
                emitterCount = emitterCount,
                emitterGroupCount = effect.Groups.Count,
                groups = groups,
                rawPreserved = effect.IsRawPreserved,
                rawPreservedEmitters = rawPreservedEmitters,
                rootType = "PEFT",
                source = sourcePath,
                type = "particle",
                version = effect.Version
            };
        }

        // Terrain (FORM TGEN / PTAT) NAVIGABLE envelope (#9): a layer tree (name + active + recursive
        // children + sub-layers), the six positional palettes (slot role / present / ambiguous / family
        // names), and per-child typed fields | raw hex | dead disposition + stable id + editable. Counts
        // (layerCount / paletteCounts / rawFallbackCount) ride alongside the navigable tree for convenience —
        // NOT summary-only. Routed without any MCP-layer change.
        public static object BuildTerrainResult(
            UtinniCoreDotNet.Formats.Terrain.TerrainDocument doc, string sourcePath)
        {
            int rawFallbackCount = 0;
            var palettes = new System.Collections.Generic.List<object>();
            foreach (var p in doc.Palettes.InLoadOrder)
            {
                palettes.Add(new
                {
                    ambiguous = p.Ambiguous,
                    families = p.Families
                        .Select(f => new { familyId = f.FamilyId, name = f.Name })
                        .ToList(),
                    present = p.Present,
                    slot = p.Role
                });
            }

            int layerCount = 0;
            var layers = new System.Collections.Generic.List<object>();
            foreach (var layer in doc.Layers)
            {
                layers.Add(BuildTerrainLayer(layer, ref rawFallbackCount, ref layerCount));
            }

            return new
            {
                layerCount = layerCount,
                layers = layers,
                paletteCount = palettes.Count,
                palettes = palettes,
                rawFallbackCount = rawFallbackCount,
                rootType = "TGEN",
                source = sourcePath,
                type = "terrain"
            };
        }

        private static object BuildTerrainLayer(
            UtinniCoreDotNet.Formats.Terrain.TerrainLayer layer, ref int rawFallbackCount, ref int layerCount)
        {
            layerCount++;
            var children = new System.Collections.Generic.List<object>();
            foreach (var node in layer.Nodes)
            {
                string kind = node.IsDeadSkipped ? "dead" : (node.IsRawPreserved ? "raw" : "typed");
                if (node.IsRawPreserved) rawFallbackCount++;
                children.Add(new
                {
                    editable = node.IsEditable,
                    fields = node.TypedFields
                        .Select(f => new { displayType = f.DisplayType.ToString(), editable = f.Editable, name = f.Name, value = f.Value })
                        .ToList(),
                    hex = node.RawHex,
                    kind = kind,
                    stableId = node.StableIdPath,
                    tag = node.Tag,
                    version = node.Version
                });
            }

            var subLayers = new System.Collections.Generic.List<object>();
            foreach (var sub in layer.SubLayers)
            {
                subLayers.Add(BuildTerrainLayer(sub, ref rawFallbackCount, ref layerCount));
            }

            return new
            {
                active = layer.Active,
                children = children,
                name = layer.Name,
                stableId = layer.StableIdPath,
                subLayers = subLayers
            };
        }

        private static object BuildAppearanceResult(AppearanceInfo a, string sourcePath)
        {
            return new
            {
                frameCount = a.FrameCount,
                jointCount = a.JointCount,
                jointNames = a.JointNames,
                kind = a.Kind,
                shaderCount = a.ShaderCount,
                source = sourcePath,
                type = "appearance",
                vertexCount = a.VertexCount
            };
        }

        private static object BuildStructureResult(StructureInfo s, string sourcePath)
        {
            return new
            {
                childCount = s.ChildCount,
                childTags = s.ChildTags,
                recognizedAs = s.RecognizedAs,
                rootTag = s.RootTag,
                source = sourcePath,
                type = "structure"
            };
        }

        private static object BuildUiPageTextResult(string sourcePath, byte[] bytes)
        {
            return new
            {
                byteCount = bytes != null ? bytes.Length : 0,
                isText = true,
                recognizedAs = "ui-page",
                source = sourcePath,
                type = "ui-page"
            };
        }

        private static string DescribeRoot(IffChunk root)
        {
            if (root is IffContainerChunk c)
            {
                return (c.TypeId ?? "").TrimEnd() + " " + (c.SubTypeId ?? "").TrimEnd();
            }
            return root != null ? (root.TypeId ?? "").TrimEnd() : "(none)";
        }
    }
}
