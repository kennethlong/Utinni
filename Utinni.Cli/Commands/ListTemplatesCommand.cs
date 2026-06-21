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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using UtinniCoreDotNet.Formats.Template;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("list-templates", HelpText = "Enumerate the user-definable chunk templates discovered across the scanned pack allow-list (shipped -> app-data -> optional project-local), reporting each template's match key and source pack.")]
    public class ListTemplatesOptions
    {
        [Option("templates-dir", HelpText = "Optional project-local pack directory added (highest priority) to the default scanned allow-list.")]
        public string TemplatesDir { get; set; }
    }

    /// <summary>
    /// The verbs-first template inventory (D-15 / D-12): <c>list-templates</c> runs
    /// <see cref="TemplatePackStore.LoadAll"/> over the default scanned allow-list (plus an optional
    /// <c>--templates-dir</c> project-local root) and emits each discovered template's match key
    /// (ancestor path + leaf tag + tag-only flag), its source pack file, and its load-order priority.
    /// A malformed pack file is reported with its skip reason rather than aborting the listing (D-12
    /// resilience). The UI (23-07) and MCP (23-06) consume THIS verb to populate their template pickers.
    ///
    /// <para><b>Exit codes:</b> 0 success (an empty allow-list is still a valid, empty listing); 2
    /// IOException. Generic <see cref="System.Exception"/> intentionally NOT caught.</para>
    /// </summary>
    public static class ListTemplatesCommand
    {
        public static int Run(ListTemplatesOptions o)
        {
            try
            {
                TemplatePackStore store = TemplatePackStore.DefaultRoots(o.TemplatesDir);
                List<TemplateLoadResult> all = store.LoadAll();

                var templates = all
                    .Where(r => r.Loaded)
                    .Select(r => new
                    {
                        matchAncestorPath = r.Template.MatchAncestorPath,
                        matchKey = MatchKeyOf(r.Template),
                        matchLeafTag = r.Template.MatchLeafTag,
                        matchTagOnly = r.Template.MatchTagOnly,
                        priority = r.Priority,
                        sourcePack = r.Path,
                        version = r.Template.Version
                    })
                    .ToList();

                var skipped = all
                    .Where(r => !r.Loaded)
                    .Select(r => new { reason = r.SkipReason, sourcePack = r.Path })
                    .ToList();

                var roots = store.Roots
                    .Select(root => new { directory = root.Directory, label = root.Label, priority = root.Priority })
                    .ToList();

                var result = new
                {
                    roots = roots,
                    skipped = skipped,
                    skippedCount = skipped.Count,
                    templateCount = templates.Count,
                    templates = templates,
                    type = "template-list"
                };
                return JsonOutput.EmitSuccess("list-templates", result);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("list-templates", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception intentionally NOT caught.
        }

        // The match key surfaced for each template (mirrors TemplatePackStore's shadow key composition):
        // ancestor path + leaf tag + tag-only flag — the predicate the resolver matches a leaf against.
        private static string MatchKeyOf(TemplateModel t)
        {
            string ancestor = (t.MatchAncestorPath ?? "").Trim();
            string leaf = (t.MatchLeafTag ?? "").TrimEnd();
            string tagOnly = t.MatchTagOnly ? "tagOnly" : "ancestor+tag";
            return (ancestor.Length == 0 ? "(tag-only)" : ancestor) + " :: " + leaf + " [" + tagOnly + "]";
        }
    }
}
