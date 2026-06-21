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
// The D-12 scanned-dir allow-list loader. Templates load ONLY from a small set of allow-listed pack
// directories, scanned in load-order (shipped/built-in -> per-user app-data -> optional project-local).
// This is the SWG searchPath mental model: a template in a LATER (higher-priority) pack shadows a
// same-match-key template in an earlier pack. A malformed pack file is SKIPPED with a captured reason --
// one bad file never aborts the whole scan (D-12 resilience). Path security (T-23-04-PATH): every file is
// loaded only if it resolves WITHIN one of the allow-listed roots, reusing the LooseOverridePath
// containment idiom (reject '..'/absolute escapes), so an imported pack that points outside its root is
// rejected before deserialization.

using System;
using System.Collections.Generic;
using System.IO;
using UtinniCoreDotNet.Saving;

namespace UtinniCoreDotNet.Formats.Template
{
    /// <summary>
    /// A single pack directory in the D-12 scanned allow-list, with its load-order rank. Higher
    /// <see cref="Priority"/> shadows lower on a same-match-key collision (the searchPath model).
    /// </summary>
    public sealed class TemplatePackRoot
    {
        /// <summary>The (canonical) absolute directory path scanned for <c>*.json</c> template packs.</summary>
        public string Directory { get; set; }

        /// <summary>Load-order rank; higher wins a same-key shadow. Shipped = lowest, project-local = highest.</summary>
        public int Priority { get; set; }

        /// <summary>A human label for diagnostics (e.g. "shipped", "app-data", "project-local").</summary>
        public string Label { get; set; }
    }

    /// <summary>The outcome of loading one pack file: either a template or a skip reason (never a throw).</summary>
    public sealed class TemplateLoadResult
    {
        /// <summary>The pack file path this result describes.</summary>
        public string Path { get; set; }

        /// <summary>The loaded template, or null when the file was skipped.</summary>
        public TemplateModel Template { get; set; }

        /// <summary>The load-order priority of the root this file came from (for shadow resolution).</summary>
        public int Priority { get; set; }

        /// <summary>Null on success; a captured human reason when the file was skipped (malformed/rejected).</summary>
        public string SkipReason { get; set; }

        /// <summary>True when the template loaded cleanly.</summary>
        public bool Loaded { get { return Template != null && SkipReason == null; } }
    }

    /// <summary>
    /// Loads user-definable templates from the D-12 scanned-dir allow-list with load-order precedence and
    /// path containment. Headless and side-effect-free apart from reading the configured roots.
    /// </summary>
    /// <remarks>
    /// <para><b>Scan order (lowest -&gt; highest priority):</b>
    /// <list type="number">
    ///   <item><b>shipped</b> — the <c>Presets/</c> pack next to the assembly
    ///         (<see cref="AppContext.BaseDirectory"/><c>/Formats/Template/Presets</c>); the built-in
    ///         D-09 presets from 23-02 (resolved via <see cref="AppContext.BaseDirectory"/>, never
    ///         <c>Assembly.Location</c>, to survive xUnit shadow-copy).</item>
    ///   <item><b>app-data</b> — <c>%APPDATA%/Utinni/templates</c> (the per-user pack dir).</item>
    ///   <item><b>project-local</b> — an optional caller-supplied project root (the open-mod dir).</item>
    /// </list>
    /// A later (higher-priority) pack shadows a same-match-key template in an earlier one.</para>
    /// <para>The exact dirs are construction-time configurable; <see cref="DefaultRoots"/> wires the
    /// documented default order.</para>
    /// </remarks>
    public sealed class TemplatePackStore
    {
        private readonly List<TemplatePackRoot> roots;

        /// <summary>Builds a store over an explicit ordered root list (Priority decides shadow precedence).</summary>
        public TemplatePackStore(IEnumerable<TemplatePackRoot> packRoots)
        {
            roots = new List<TemplatePackRoot>();
            if (packRoots != null)
            {
                foreach (TemplatePackRoot r in packRoots)
                {
                    if (r != null && !string.IsNullOrEmpty(r.Directory)) roots.Add(r);
                }
            }
        }

        /// <summary>The resolved (canonical) pack roots this store scans, in load order.</summary>
        public IReadOnlyList<TemplatePackRoot> Roots { get { return roots.AsReadOnly(); } }

        /// <summary>
        /// Builds the documented default root list: shipped (next to the assembly) -&gt; app-data -&gt;
        /// optional project-local. Each root is canonicalized; a project-local root of null/empty is
        /// omitted. Directories that do not exist are still listed (LoadAll simply finds no files there).
        /// </summary>
        public static TemplatePackStore DefaultRoots(string projectLocalDirOrNull)
        {
            var list = new List<TemplatePackRoot>();

            // (1) shipped — the Presets pack ships next to the assembly (CopyToOutputDirectory). Resolve via
            // AppContext.BaseDirectory (NOT Assembly.Location) so xUnit's shadow-copy does not hide it.
            string shipped = Path.Combine(AppContext.BaseDirectory, "Formats", "Template", "Presets");
            list.Add(new TemplatePackRoot { Directory = Canonical(shipped), Priority = 0, Label = "shipped" });

            // (2) app-data — per-user pack dir.
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                string userDir = Path.Combine(appData, "Utinni", "templates");
                list.Add(new TemplatePackRoot { Directory = Canonical(userDir), Priority = 10, Label = "app-data" });
            }

            // (3) project-local — optional, highest priority (the open mod's own pack).
            if (!string.IsNullOrEmpty(projectLocalDirOrNull))
            {
                list.Add(new TemplatePackRoot
                {
                    Directory = Canonical(projectLocalDirOrNull),
                    Priority = 20,
                    Label = "project-local"
                });
            }

            return new TemplatePackStore(list);
        }

        /// <summary>
        /// Scans every configured root for <c>*.json</c> packs in load order and returns a per-file result.
        /// A file that fails to deserialize, or that does not resolve WITHIN its declaring root (containment
        /// defense, T-23-04-PATH), is recorded with a <see cref="TemplateLoadResult.SkipReason"/> rather than
        /// aborting the scan. Use <see cref="LoadEffective"/> for the shadow-resolved template set.
        /// </summary>
        public List<TemplateLoadResult> LoadAll()
        {
            var results = new List<TemplateLoadResult>();

            foreach (TemplatePackRoot root in roots)
            {
                string canonicalRoot = Canonical(root.Directory);
                if (!Directory.Exists(canonicalRoot))
                {
                    continue; // a missing root is not an error; it simply contributes no packs.
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(canonicalRoot, "*.json", SearchOption.TopDirectoryOnly);
                }
                catch (Exception ex)
                {
                    results.Add(new TemplateLoadResult
                    {
                        Path = canonicalRoot,
                        Priority = root.Priority,
                        SkipReason = "Could not enumerate pack root: " + ex.Message
                    });
                    continue;
                }

                Array.Sort(files, StringComparer.OrdinalIgnoreCase); // deterministic intra-root order

                foreach (string file in files)
                {
                    results.Add(LoadOne(canonicalRoot, file, root.Priority));
                }
            }

            return results;
        }

        /// <summary>
        /// Loads every pack, then resolves load-order shadowing: for each match key
        /// (<see cref="TemplateModel.MatchAncestorPath"/> + <see cref="TemplateModel.MatchLeafTag"/> +
        /// <see cref="TemplateModel.MatchTagOnly"/>), the template from the HIGHEST-priority pack wins.
        /// Returns the effective template set in deterministic order. Skipped files are dropped.
        /// </summary>
        public List<TemplateModel> LoadEffective()
        {
            var byKey = new Dictionary<string, TemplateLoadResult>(StringComparer.Ordinal);
            foreach (TemplateLoadResult r in LoadAll())
            {
                if (!r.Loaded) continue;
                string key = MatchKeyOf(r.Template);
                TemplateLoadResult existing;
                if (!byKey.TryGetValue(key, out existing) || r.Priority >= existing.Priority)
                {
                    // >= so a later pack at equal priority (within a single root, deterministic file order)
                    // still shadows; a strictly-higher-priority pack always wins.
                    byKey[key] = r;
                }
            }

            var effective = new List<TemplateModel>();
            foreach (TemplateLoadResult r in byKey.Values)
            {
                effective.Add(r.Template);
            }
            return effective;
        }

        // Loads one file with containment + deserialization defenses. Never throws; a failure becomes a skip.
        private static TemplateLoadResult LoadOne(string canonicalRoot, string file, int priority)
        {
            var result = new TemplateLoadResult { Path = file, Priority = priority };

            // Containment defense (T-23-04-PATH): the resolved file MUST live within the declaring root.
            // GetFiles already returns paths under the root, but an imported/symlinked entry or a future
            // recursive caller could point outside -- route through the shared LooseOverridePath predicate
            // so the rule is single-sourced with the save-path containment gate.
            string fullFile;
            try
            {
                fullFile = Path.GetFullPath(file);
            }
            catch (Exception ex)
            {
                result.SkipReason = "Invalid pack file path: " + ex.Message;
                return result;
            }

            if (!LooseOverridePath.IsContainedUnderRoot(canonicalRoot, fullFile))
            {
                result.SkipReason = "Pack file escapes its allow-listed root: " + fullFile;
                return result;
            }

            string json;
            try
            {
                json = File.ReadAllText(fullFile);
            }
            catch (Exception ex)
            {
                result.SkipReason = "Could not read pack file: " + ex.Message;
                return result;
            }

            try
            {
                result.Template = TemplateJson.Deserialize(json);
            }
            catch (Exception ex)
            {
                // Malformed JSON / bad enum / structurally invalid template: skip with the reason, do NOT
                // abort the whole scan (D-12 resilience).
                result.SkipReason = "Malformed template pack: " + ex.Message;
                result.Template = null;
            }

            return result;
        }

        // The shadow key: a template is uniquely addressed by its match predicate (ancestor path + leaf tag
        // + tag-only flag). Two packs declaring the same predicate collide; the higher-priority one wins.
        private static string MatchKeyOf(TemplateModel t)
        {
            string ancestor = (t.MatchAncestorPath ?? "").Trim();
            string leaf = (t.MatchLeafTag ?? "").TrimEnd();
            string tagOnly = t.MatchTagOnly ? "1" : "0";
            return ancestor + " " + leaf + " " + tagOnly;
        }

        private static string Canonical(string dir)
        {
            try { return Path.GetFullPath(dir); }
            catch { return dir; }
        }
    }
}
