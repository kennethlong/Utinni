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
//
// The match + precedence layer (D-04 / D-05 / D-07 / D-17). A user-definable template engages on a leaf
// chunk ONLY where (a) the file's root FORM has no built-in decoder (D-05 altitude -- built-ins win
// wholesale, gated via BuiltinRootForms, the SINGLE source) AND (b) a loaded template's match key is a
// prefix/predicate over the leaf's stable-id path (D-04 -- ancestor-FORM path + version-FORM + leaf tag;
// version-FORM awareness is REQUIRED so the CLEF-CPAP 3-layout case cannot silently mis-decode). The match
// key is derived ENTIRELY from MutableIffDocument.DeriveStableId -- no new addressing scheme. On a match
// the resolver runs FitChecker.FitCheck (D-07) so the caller gets a does-it-actually-round-trip confidence
// signal alongside the template (a key-matched but byte-mismatched template is SURFACED, not silently
// applied). Most-specific-key-wins on ties; a genuine tie is exposed as a candidate list for the UI picker.
// The match key is kept a reusable predicate (Resolve takes a stable-id path string), so this resolver
// doubles as Tier-C's corpus-query substrate (D-17.4) -- not welded to a single open document.

using System;
using System.Collections.Generic;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.Template
{
    /// <summary>
    /// One resolved template candidate: the matched <see cref="TemplateModel"/>, the
    /// <see cref="FitReport"/> from running the byte-exact fit-check over the leaf payload (D-07
    /// confidence), and the match specificity (length of the version-FORM-aware ancestor key, longer =
    /// more specific). The <see cref="FitReport"/> is null for a <see cref="TemplateResolver.MatchKey"/>
    /// query that supplies no payload (the Tier-C corpus-query substrate path).
    /// </summary>
    public sealed class TemplateMatch
    {
        /// <summary>The matched template.</summary>
        public TemplateModel Template { get; set; }

        /// <summary>Byte-exact fit-check report over the leaf payload (D-07), or null when no payload was supplied.</summary>
        public FitReport Fit { get; set; }

        /// <summary>True when this match key includes the version-FORM segment (NOT a tag-only widen). Diagnostic.</summary>
        public bool VersionFormAware { get; set; }

        /// <summary>True when <see cref="Fit"/> reports the template consumed the payload exactly (D-07 fits).</summary>
        public bool Fits { get { return Fit != null && Fit.ConsumedExactly; } }

        // Match specificity score: longer ancestor key (version-FORM-aware, full ancestor prefix) outranks
        // a tag-only widen. Used for most-specific-key-wins tie-break.
        internal int Specificity { get; set; }
    }

    /// <summary>
    /// The aggregate result of resolving a single leaf: the winning (most-specific) match plus any genuine
    /// ties the UI should let the user disambiguate. <see cref="Best"/> is null when nothing matched (no
    /// eligible template, or the file's root FORM is built-in-owned per D-05).
    /// </summary>
    public sealed class TemplateResolution
    {
        /// <summary>The most-specific matching template, or null when none matched / the file is built-in-owned.</summary>
        public TemplateMatch Best { get; set; }

        /// <summary>All candidates that tied at the winning specificity (count &gt; 1 means a genuine tie for the UI picker).</summary>
        public List<TemplateMatch> Candidates { get; set; }

        /// <summary>True when the D-05 altitude gate suppressed all templates (the root FORM is built-in-owned).</summary>
        public bool SuppressedByBuiltin { get; set; }
    }

    /// <summary>
    /// Resolves a template-eligible leaf chunk to its template by the version-FORM-aware match key, enforces
    /// the D-05 altitude gate (built-ins win), and surfaces the D-07 round-trip fit-confidence. Headless and
    /// reusable as a corpus-query predicate (D-17.4).
    /// </summary>
    public sealed class TemplateResolver
    {
        private readonly List<TemplateModel> templates;

        /// <summary>
        /// Builds a resolver over a set of loaded templates. The order matters only as a stable tie-break
        /// fallback; the primary match ranking is most-specific-key-wins (the version-FORM-aware ancestor
        /// key length). A null/empty set yields a resolver that matches nothing.
        /// </summary>
        public TemplateResolver(IEnumerable<TemplateModel> loadedTemplates)
        {
            templates = new List<TemplateModel>();
            if (loadedTemplates != null)
            {
                foreach (TemplateModel t in loadedTemplates)
                {
                    if (t != null) templates.Add(t);
                }
            }
        }

        /// <summary>
        /// Resolves the leaf identified by <paramref name="leafStableId"/> within <paramref name="doc"/>.
        /// Enforces the D-05 altitude gate FIRST: if the document's root FORM has a built-in decoder
        /// (<see cref="BuiltinRootForms.HasBuiltinDecoder"/>), returns a resolution with
        /// <see cref="TemplateResolution.SuppressedByBuiltin"/> = true and no match. Otherwise locates the
        /// leaf, matches every eligible template by the version-FORM-aware key over the leaf's stable-id
        /// path, runs <see cref="FitChecker.FitCheck"/> on each match (D-07), and returns the most-specific
        /// winner plus any genuine ties.
        /// </summary>
        public TemplateResolution Resolve(MutableIffDocument doc, string leafStableId)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (leafStableId == null) throw new ArgumentNullException("leafStableId");

            var resolution = new TemplateResolution { Candidates = new List<TemplateMatch>() };

            // ── D-05 altitude gate FIRST: a template never engages where a built-in owns the root FORM ──
            if (RootHasBuiltin(doc))
            {
                resolution.SuppressedByBuiltin = true;
                return resolution;
            }

            // Locate the leaf node + capture its stable-id path so the match key is a predicate over it.
            MutableIffNode leaf = FindLeafByStableId(doc, leafStableId);
            if (leaf == null || leaf.Kind != MutableIffNodeKind.Leaf)
            {
                return resolution; // nothing to match against
            }

            byte[] payload = leaf.GetPayloadCopy();
            string leafTag = leaf.TypeId.TrimEnd();

            CollectMatches(leafStableId, leafTag, payload, resolution);
            return resolution;
        }

        /// <summary>
        /// The Tier-C corpus-query entry point (D-17.4): matches templates against a stable-id path +
        /// leaf tag WITHOUT a live document or payload. The match key is a pure predicate over the path,
        /// so a corpus enumerator can filter on it. <see cref="TemplateMatch.Fit"/> is null (no payload to
        /// fit-check). Does NOT apply the altitude gate -- the caller owns that decision when querying a
        /// corpus by path.
        /// </summary>
        public TemplateResolution MatchKey(string leafStableId, string leafTag)
        {
            if (leafStableId == null) throw new ArgumentNullException("leafStableId");
            if (leafTag == null) throw new ArgumentNullException("leafTag");

            var resolution = new TemplateResolution { Candidates = new List<TemplateMatch>() };
            CollectMatches(leafStableId, leafTag.TrimEnd(), null, resolution);
            return resolution;
        }

        // ── match collection + ranking ───────────────────────────────────────────

        private void CollectMatches(string leafStableId, string leafTag, byte[] payloadOrNull,
            TemplateResolution into)
        {
            var matches = new List<TemplateMatch>();
            foreach (TemplateModel t in templates)
            {
                int specificity;
                bool versionAware;
                if (!KeyMatches(t, leafStableId, leafTag, out specificity, out versionAware))
                {
                    continue;
                }

                var m = new TemplateMatch
                {
                    Template = t,
                    Specificity = specificity,
                    VersionFormAware = versionAware,
                    // D-07: run the byte-exact fit-check so the caller sees whether the key-matched template
                    // ACTUALLY round-trips. A non-fitting template is returned WITH a does-not-fit report --
                    // surfaced, never silently applied.
                    Fit = payloadOrNull != null ? FitChecker.FitCheck(t, payloadOrNull) : null
                };
                matches.Add(m);
            }

            if (matches.Count == 0)
            {
                return;
            }

            // Most-specific-key-wins: the highest specificity (version-FORM-aware ancestor key) is the
            // winner. Everything tied at that specificity is a genuine tie the UI must disambiguate.
            int bestSpecificity = int.MinValue;
            foreach (TemplateMatch m in matches)
            {
                if (m.Specificity > bestSpecificity) bestSpecificity = m.Specificity;
            }

            foreach (TemplateMatch m in matches)
            {
                if (m.Specificity == bestSpecificity)
                {
                    into.Candidates.Add(m);
                }
            }

            into.Best = into.Candidates[0];
        }

        // True when a template's match key matches the leaf's stable-id path. The version-FORM segment is
        // ALWAYS required (D-04): tag-only widening drops the ANCESTOR prefix requirement, never the
        // version-FORM. specificity = the matched ancestor-path length (0 for a tag-only widen, the full
        // MatchAncestorPath length otherwise) so the longest ancestor key wins ties.
        private static bool KeyMatches(TemplateModel t, string leafStableId, string leafTag,
            out int specificity, out bool versionFormAware)
        {
            specificity = 0;
            versionFormAware = false;

            string templateLeafTag = (t.MatchLeafTag ?? "").TrimEnd();
            if (templateLeafTag.Length == 0) return false;

            // The leaf tag must match the chunk's tag (the terminal segment of the stable-id path).
            if (!string.Equals(templateLeafTag, leafTag, StringComparison.Ordinal)) return false;

            // The version-FORM segment carried in the leaf's stable-id path. REQUIRED: even a tag-only
            // widen keeps the version-FORM (a tag-only template still only matches the SAME version-FORM
            // family unless the author leaves MatchAncestorPath null/empty AND the version segment is also
            // unconstrained). We surface version-awareness for diagnostics, and require that when the
            // template DOES constrain the ancestor path, that path (incl. its version-FORM segment) is a
            // prefix of the leaf's stable-id path.
            string versionSegment = ExtractVersionFormSegment(leafStableId);
            versionFormAware = versionSegment != null;

            if (t.MatchTagOnly)
            {
                // Tag-only widen: ancestor prefix is dropped, but the match still REQUIRES the leaf carry a
                // version-FORM in its path (version-FORM awareness is never dropped -- D-04). specificity 0.
                specificity = 0;
                return true;
            }

            string ancestor = (t.MatchAncestorPath ?? "").Trim();
            if (ancestor.Length == 0)
            {
                // No ancestor constraint and not tag-only-flagged: behave as a low-specificity tag match
                // that still respects version-FORM awareness (the leaf must have a version segment).
                specificity = 0;
                return true;
            }

            // The ancestor path (which carries the version-FORM segment) must be a path-prefix of the
            // leaf's stable-id path. Compare on '/'-delimited segment boundaries so "FORM:0001" does not
            // spuriously prefix-match "FORM:00010".
            if (!IsPathPrefix(ancestor, leafStableId)) return false;

            specificity = ancestor.Length;
            return true;
        }

        // Returns true when `prefix` is a segment-aligned path prefix of `full` (both '/'-delimited
        // stable-id paths). "a/b" is a prefix of "a/b/c" and of "a/b" but NOT of "a/bc".
        private static bool IsPathPrefix(string prefix, string full)
        {
            if (prefix.Length > full.Length) return false;
            if (!full.StartsWith(prefix, StringComparison.Ordinal)) return false;
            if (prefix.Length == full.Length) return true;
            // The char immediately after the prefix must be a separator for a segment-aligned match.
            return full[prefix.Length] == '/';
        }

        // Extracts the version-FORM segment from a stable-id path, e.g. for
        // "FORM:TPLX/0/FORM:0003/0/CPAP:CPAP/0" -> "0003" (the sub-type of the innermost FORM container
        // enclosing the leaf). Returns null when no enclosing FORM container with a sub-type is found.
        private static string ExtractVersionFormSegment(string stableId)
        {
            // Segments look like "FORM:TPLX/0" -> split on '/' yields ["FORM:TPLX","0","FORM:0003","0",...].
            string[] parts = stableId.Split('/');
            string lastFormSub = null;
            for (int i = 0; i + 1 < parts.Length; i++)
            {
                string seg = parts[i];
                int colon = seg.IndexOf(':');
                if (colon <= 0) continue;
                string type = seg.Substring(0, colon);
                string sub = seg.Substring(colon + 1);
                if (type == "FORM" || type == "LIST" || type == "CAT")
                {
                    lastFormSub = sub; // keep walking; the LAST (innermost) FORM sub is the version-FORM
                }
            }
            return lastFormSub;
        }

        // ── document traversal ────────────────────────────────────────────────────

        private static bool RootHasBuiltin(MutableIffDocument doc)
        {
            MutableIffNode root = doc.Root;
            if (root == null || root.Kind != MutableIffNodeKind.Container) return false;
            // Reuse the SINGLE-source built-in predicate (D-05). It operates on the immutable IffChunk
            // surface; reconstruct a minimal immutable container mirror (type + sub-type + children) just
            // far enough for the structural sniffs (terrain/object-template) to run against the same logic.
            return BuiltinRootForms.HasBuiltinDecoder(ToImmutableMirror(root));
        }

        // Builds an immutable IffChunk mirror of a mutable subtree so BuiltinRootForms' sniffs (which take
        // an IffChunk) can run over the live edited tree without a separate built-in list. Leaf Data is the
        // real payload (LooksLikeObjectTemplate inspects the first leaf's length); offsets/ids are nominal.
        private static IffChunk ToImmutableMirror(MutableIffNode node)
        {
            if (node.Kind == MutableIffNodeKind.Leaf)
            {
                byte[] data = node.GetPayloadCopy();
                return new IffLeafChunk(node.TypeId, data.Length, node.TypeId.TrimEnd(), 0, data);
            }

            var children = new List<IffChunk>();
            foreach (MutableIffNode child in node.Children)
            {
                children.Add(ToImmutableMirror(child));
            }
            return new IffContainerChunk(node.TypeId, 0, node.TypeId.TrimEnd(), 0, node.SubTypeId,
                children.AsReadOnly());
        }

        // Finds the leaf node whose DeriveStableId equals targetStableId (walking the same ordinal-path
        // scheme the rest of the codebase uses -- no new addressing).
        private static MutableIffNode FindLeafByStableId(MutableIffDocument doc, string targetStableId)
        {
            MutableIffNode root = doc.Root;
            if (root == null) return null;
            string rootId = MutableIffDocument.DeriveStableId(root, "", 0);
            if (rootId == targetStableId) return root.Kind == MutableIffNodeKind.Leaf ? root : null;
            return FindRecursive(root, rootId + "/", targetStableId);
        }

        private static MutableIffNode FindRecursive(MutableIffNode node, string idPrefix, string target)
        {
            IReadOnlyList<MutableIffNode> children = node.Children;
            for (int i = 0; i < children.Count; i++)
            {
                MutableIffNode c = children[i];
                string cid = MutableIffDocument.DeriveStableId(c, idPrefix, i);
                if (cid == target) return c;
                if (c.Kind == MutableIffNodeKind.Container)
                {
                    MutableIffNode hit = FindRecursive(c, cid + "/", target);
                    if (hit != null) return hit;
                }
            }
            return null;
        }

        /// <summary>
        /// Enumerates the stable-id path of every leaf in <paramref name="doc"/> (the addressing the
        /// resolver matches on). Handy for the UI/Tier-C to discover which leaves are template-eligible.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, string>> EnumerateLeafStableIds(MutableIffDocument doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            var results = new List<KeyValuePair<string, string>>();
            MutableIffNode root = doc.Root;
            if (root == null) return results;
            string rootId = MutableIffDocument.DeriveStableId(root, "", 0);
            if (root.Kind == MutableIffNodeKind.Leaf)
            {
                results.Add(new KeyValuePair<string, string>(rootId, root.TypeId.TrimEnd()));
                return results;
            }
            EnumerateRecursive(root, rootId + "/", results);
            return results;
        }

        private static void EnumerateRecursive(MutableIffNode node, string idPrefix,
            List<KeyValuePair<string, string>> into)
        {
            IReadOnlyList<MutableIffNode> children = node.Children;
            for (int i = 0; i < children.Count; i++)
            {
                MutableIffNode c = children[i];
                string cid = MutableIffDocument.DeriveStableId(c, idPrefix, i);
                if (c.Kind == MutableIffNodeKind.Leaf)
                {
                    into.Add(new KeyValuePair<string, string>(cid, c.TypeId.TrimEnd()));
                }
                else
                {
                    EnumerateRecursive(c, cid + "/", into);
                }
            }
        }
    }
}
