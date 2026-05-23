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
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Iff;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("inspect-iff", HelpText = "Emit the chunk tree of an IFF file as JSON.")]
    public class InspectIffOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .iff file.")]
        public string Path { get; set; }
    }

    /// <summary>
    /// Reads an IFF file via IffReader, builds the dual tree+flat JSON projection, and emits
    /// via JsonOutput. This is the ONLY class in Plan 04-03 that references Newtonsoft.Json
    /// or constructs JObject/JArray.
    ///
    /// <para><b>React Flow portability note:</b>
    /// Both <c>tree</c> and <c>flat</c> views derive from the same parser pass — they are
    /// alternative projections of one data structure, not two parses. The flat view is included
    /// so future React Flow tooling can lift this output without re-shaping it.
    /// Do NOT drop the flat view in 'cleanup' refactors — it is a stable part of the
    /// schemaVersion: 1 contract.</para>
    /// </summary>
    public static class InspectIffCommand
    {
        public static int Run(InspectIffOptions o)
        {
            // FileNotFound check: exit 3 (D-02).
            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("inspect-iff", "FileNotFound",
                    "IFF file not found: " + o.Path, exitCode: 3);
            }

            try
            {
                var doc = IffReader.Read(o.Path);
                return JsonOutput.EmitSuccess("inspect-iff", BuildResult(doc, o.Path));
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("inspect-iff", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("inspect-iff", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught here — unexpected
            // exception types should bubble to the top level for diagnosis.
        }

        /// <summary>
        /// Builds the result object containing source path, hierarchical tree, and flat projection.
        /// JsonOutput.EmitSuccess wraps this in the root envelope:
        ///   { "command": "inspect-iff", "result": &lt;below&gt;, "schemaVersion": 1 }
        /// The result object itself NEVER pre-adds schemaVersion or command (REVIEWS HIGH-6).
        /// </summary>
        private static JObject BuildResult(IffDocument doc, string sourcePath)
        {
            var treeNode = BuildTreeNode(doc.Root);
            var flatProjection = BuildFlatProjection(doc.AllNodesInPreorder, doc.Root.Id);

            return new JObject
            {
                ["flat"] = flatProjection,
                ["source"] = sourcePath,
                ["tree"] = treeNode
            };
        }

        /// <summary>
        /// Builds a hierarchical tree node for the given chunk.
        /// Each node carries: id, kind, lengthBytes, typeId.
        /// Containers additionally carry: subTypeId, children.
        /// Leaves additionally carry: dataBase64.
        /// Keys will be sorted alphabetically by JsonOutput's SortJObjectKeys pass.
        /// </summary>
        private static JObject BuildTreeNode(IffChunk chunk)
        {
            var node = new JObject
            {
                ["id"] = chunk.Id,
                ["kind"] = (chunk is IffContainerChunk) ? "container" : "leaf",
                ["lengthBytes"] = chunk.LengthBytes,
                ["typeId"] = chunk.TypeId
            };

            if (chunk is IffContainerChunk container)
            {
                node["children"] = new JArray();
                foreach (var child in container.Children)
                {
                    ((JArray)node["children"]).Add(BuildTreeNode(child));
                }
                node["subTypeId"] = container.SubTypeId;
            }
            else if (chunk is IffLeafChunk leaf)
            {
                node["dataBase64"] = Convert.ToBase64String(leaf.Data);
            }

            return node;
        }

        /// <summary>
        /// Builds the flat projection for React Flow consumers.
        /// flat.nodes: preorder list of { id, kind, lengthBytes, name, parentId, typeId }.
        /// flat.edges: list of { from, to } for each parent-child relationship.
        /// React Flow consumers can call useNodesState(json.result.flat.nodes) and
        /// useEdgesState(json.result.flat.edges) directly without re-shaping the payload.
        /// </summary>
        private static JObject BuildFlatProjection(IReadOnlyList<IffChunk> preorderNodes, string rootId)
        {
            // Build a mapping from chunk Id to parent Id by walking the tree.
            var parentMap = new Dictionary<string, string>(StringComparer.Ordinal);
            BuildParentMap(preorderNodes, parentMap);

            var nodesArr = new JArray();
            var edgesArr = new JArray();

            foreach (var chunk in preorderNodes)
            {
                string parentId = null;
                parentMap.TryGetValue(chunk.Id, out parentId);

                // Build human-readable name label.
                string name;
                if (chunk is IffContainerChunk c)
                {
                    name = c.TypeId.TrimEnd() + " " + c.SubTypeId.TrimEnd();
                }
                else
                {
                    name = chunk.TypeId.TrimEnd();
                }

                var nodeObj = new JObject
                {
                    ["id"] = chunk.Id,
                    ["kind"] = (chunk is IffContainerChunk) ? "container" : "leaf",
                    ["lengthBytes"] = chunk.LengthBytes,
                    ["name"] = name,
                    ["parentId"] = parentId != null ? (JToken)parentId : JValue.CreateNull(),
                    ["typeId"] = chunk.TypeId
                };
                nodesArr.Add(nodeObj);

                // Add edge for non-root nodes.
                if (parentId != null)
                {
                    edgesArr.Add(new JObject
                    {
                        ["from"] = parentId,
                        ["to"] = chunk.Id
                    });
                }
            }

            return new JObject
            {
                ["edges"] = edgesArr,
                ["nodes"] = nodesArr
            };
        }

        /// <summary>
        /// Walks the preorder list and populates a mapping from each child's id to its parent's id.
        /// Uses the IffContainerChunk.Children property for accurate parent tracking.
        /// </summary>
        private static void BuildParentMap(IReadOnlyList<IffChunk> preorderNodes, Dictionary<string, string> parentMap)
        {
            // Find container chunks in the preorder list and populate their children's parent ids.
            foreach (var chunk in preorderNodes)
            {
                if (chunk is IffContainerChunk container)
                {
                    foreach (var child in container.Children)
                    {
                        parentMap[child.Id] = container.Id;
                    }
                }
            }
        }
    }
}
