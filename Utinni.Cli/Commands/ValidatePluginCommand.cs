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

using System.IO;
using System.Linq;
using CommandLine;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("validate-plugin", HelpText = "Reflect on a plugin directory and report compliance. " +
        "WARNING: Loads each .dll under the given directory; only run against trusted plugin directories. " +
        "PluginLoader composition + managed-DLL inspection execute static initialisers as a side effect.")]
    public class ValidatePluginOptions
    {
        [Value(0, MetaName = "dir", Required = true, HelpText = "Plugin directory to validate.")]
        public string Dir { get; set; }
    }

    public static class ValidatePluginCommand
    {
        public static int Run(ValidatePluginOptions o)
        {
            // 1. Validate argument.
            if (string.IsNullOrEmpty(o.Dir))
            {
                return JsonOutput.EmitError(
                    "validate-plugin",
                    "MissingArgument",
                    "Plugin directory argument is required.",
                    exitCode: 1);
            }

            // 2. Validate directory exists (exit 3 per D-02).
            if (!Directory.Exists(o.Dir))
            {
                return JsonOutput.EmitError(
                    "validate-plugin",
                    "DirectoryNotFound",
                    "Plugin directory does not exist: " + o.Dir,
                    exitCode: 3);
            }

            // 3. Inspect the directory (REVIEWS HIGH-5 — delegates to PluginLoader + PEReader).
            var dirReport = PluginInspection.InspectDirectory(o.Dir);

            // 4. Build the result payload matching the JSON contract exactly.
            //    REVIEWS HIGH-6: schemaVersion + command at root — EmitSuccess wraps.
            //    Plan-checker iter-2 WARNING-5: loaderObserved at TOP-LEVEL result, NOT per-plugin.
            //    Iter-3 MED-8: directoryChecks at TOP-LEVEL result.
            var result = new
            {
                directory = dirReport.Directory,
                directoryChecks = dirReport.DirectoryChecks
                    .Select(c => new { detail = c.Detail, id = c.Id, status = c.Status })
                    .ToList(),
                loaderObserved = new
                {
                    loadErrors = dirReport.LoaderObserved.LoadErrors.ToList(),
                    pluginsCount = dirReport.LoaderObserved.PluginsCount
                },
                plugins = dirReport.Plugins
                    .Select(r => new
                    {
                        checks = r.Checks
                            .Select(c => new { detail = c.Detail, id = c.Id, status = c.Status })
                            .ToList(),
                        exports = new
                        {
                            createPlugin = r.Exports.CreatePlugin,
                            destroyPlugin = r.Exports.DestroyPlugin,
                            managedIEditorPlugin = r.Exports.ManagedIEditorPlugin,
                            managedIPlugin = r.Exports.ManagedIPlugin
                        },
                        id = r.Id,
                        kind = r.Kind,
                        overallStatus = r.OverallStatus,
                        path = r.Path
                    })
                    .ToList(),
                summary = new
                {
                    failing = dirReport.Summary.Failing,
                    passing = dirReport.Summary.Passing,
                    totalPlugins = dirReport.Summary.TotalPlugins
                }
            };

            // 5. Emit success envelope at root (REVIEWS HIGH-6).
            // Exit 0 regardless of per-plugin overallStatus (per D-02:
            // exit codes 0=ok, 1=usage, 2=parse-error, 3=fixture-not-found).
            return JsonOutput.EmitSuccess("validate-plugin", result);
        }
    }
}
