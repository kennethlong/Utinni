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
// Lightweight FORM-tag + child-count summary for the shader and UI-page asset classes
// (PROD-01 criterion #3). Recognition tags verified against the swg-client-v2 engine + real assets:
// a SWG shader (.sht) is a binary IFF whose root FORM sub-type is SSHT (static shader) or CSHD
// (customizable shader) — confirmed from StaticShaderTemplate.cpp / CustomizableShaderTemplate.cpp
// and a real 2d_bloom.sht (FORM ... SSHT). SWG UI pages (.gui) are NOT IFF — they are text parsed by
// the engine's UILoader (UIPage::TypeName = "Page"), so there is no UI-page FORM tag to lock; the
// UI-page class is recognized by the virtual-path/extension hint (.gui / ui/ prefix) per the plan's
// "AND/OR the virtual-path/extension hint" clause. No code/identifiers copied from any reference
// source. Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.Decoders
{
    /// <summary>
    /// A lightweight structural summary of an IFF asset: its root FORM tag, immediate child count
    /// and child tags, plus a <see cref="RecognizedAs"/> classification ("shader" / "ui-page" / "").
    /// </summary>
    public sealed class StructureInfo
    {
        public string RootTag { get; }
        public int ChildCount { get; }
        public IReadOnlyList<string> ChildTags { get; }

        /// <summary>"shader", "ui-page", or "" (unrecognized — caller hides the structured view).</summary>
        public string RecognizedAs { get; }

        public StructureInfo(string rootTag, int childCount, IReadOnlyList<string> childTags, string recognizedAs)
        {
            RootTag = rootTag ?? "";
            ChildCount = childCount;
            ChildTags = childTags ?? new string[0];
            RecognizedAs = recognizedAs ?? "";
        }
    }

    /// <summary>
    /// Produces a lightweight FORM-tag + child-count summary and classifies the shader / UI-page
    /// asset classes — WITHOUT a deep graphics decode. Shaders (root FORM SSHT/CSHD) are classified
    /// by their LOCKED root tag; UI pages (text .gui, not IFF) are classified by the path/extension
    /// hint. "Summarize any FORM" is the UNRECOGNIZED no-throw fallback only (RecognizedAs == ""),
    /// never the primary UI-page detection path (review consensus #1). Pure — no JSON/console/file.
    /// </summary>
    public static class IffStructureSummary
    {
        // Shader root FORM sub-types (verified: StaticShaderTemplate / CustomizableShaderTemplate).
        private static readonly HashSet<string> ShaderRootTags =
            new HashSet<string>(StringComparer.Ordinal) { "SSHT", "CSHD" };

        public static StructureInfo Summarize(IffDocument doc, string virtualPathOrExt)
        {
            var root = doc?.Root as IffContainerChunk;
            string rootTag = root != null ? root.SubTypeId : "";

            var childTags = new List<string>();
            if (root != null)
            {
                foreach (var child in root.Children)
                {
                    // Report the meaningful tag: a container's sub-type, else a leaf's TypeId.
                    childTags.Add(child is IffContainerChunk c ? c.SubTypeId : child.TypeId);
                }
            }

            string recognizedAs;
            if (root != null && ShaderRootTags.Contains(rootTag))
            {
                recognizedAs = "shader";
            }
            else if (IsUiPagePath(virtualPathOrExt))
            {
                // UI pages are text, recognized by path/extension (no IFF tag exists). The primary
                // UI-page detection path — NOT the any-FORM fallback.
                recognizedAs = "ui-page";
            }
            else
            {
                // Unrecognized: still return a non-empty RootTag/ChildCount (no throw) so the CLI/UI
                // can show a generic structure, but RecognizedAs == "" tells the UI to hide the
                // dedicated structured view (review consensus #1).
                recognizedAs = "";
            }

            return new StructureInfo(rootTag, childTags.Count, childTags, recognizedAs);
        }

        /// <summary>
        /// True when the virtual path / extension hint identifies a SWG UI page: a <c>.gui</c>
        /// extension or a <c>ui/</c> path segment (the SWG client UI layout). UI pages are text,
        /// not IFF — this hint is how the UI-page class is recognized.
        /// </summary>
        public static bool IsUiPagePath(string virtualPathOrExt)
        {
            if (string.IsNullOrEmpty(virtualPathOrExt)) return false;
            string s = virtualPathOrExt.Replace('\\', '/').ToLowerInvariant();
            if (s.EndsWith(".gui")) return true;
            if (s == "ui" || s.StartsWith("ui/") || s.Contains("/ui/")) return true;
            return false;
        }
    }
}
