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
using System.Reflection;
using UtinniCoreDotNet.PluginFramework;

namespace Utinni.Cli.Commands
{
    // ---------------------------------------------------------------------------
    // POCOs (plan-checker iter-2 WARNING-5 — DirectoryReport top-level shape;
    // PluginReport does NOT carry LoaderObserved — that lives once at directory level).
    // ---------------------------------------------------------------------------

    public sealed class DirectoryReport
    {
        public string Directory;
        public LoaderObservation LoaderObserved;
        public IReadOnlyList<PluginReport> Plugins;
        public IReadOnlyList<CheckResult> DirectoryChecks;
        public ResultSummary Summary;
    }

    public sealed class PluginReport
    {
        public string Id;
        public string Path;
        public string Kind;
        public PluginExports Exports;
        public IReadOnlyList<CheckResult> Checks;
        public string OverallStatus;
        // No LoaderObserved field — directory-scoped, not per-plugin (plan-checker iter-2 WARNING-5).
    }

    public sealed class PluginExports
    {
        public bool CreatePlugin;
        public bool DestroyPlugin;
        public bool ManagedIPlugin;
        public bool ManagedIEditorPlugin;
    }

    public sealed class CheckResult
    {
        public string Id;
        public string Status; // "pass" | "fail" | "n/a" | "warn"
        public string Detail;
    }

    public sealed class LoaderObservation
    {
        public int PluginsCount;
        public IReadOnlyList<string> LoadErrors; // FILTERED set — iter-3 HIGH-1

        public static readonly LoaderObservation Empty = new LoaderObservation
        {
            PluginsCount = 0,
            LoadErrors = Array.Empty<string>()
        };
    }

    public sealed class ResultSummary
    {
        public int TotalPlugins;
        public int Passing;
        public int Failing;
    }

    // ---------------------------------------------------------------------------
    // Iter-3 HIGH-1 filter helper (iter-4 LOW-6: class name is PluginInspectionFilters).
    // Drops native-DLL MEF composition noise from LoadErrors so the JSON envelope
    // gives consumers a clean managed-plugin signal.
    // ---------------------------------------------------------------------------

    internal static class PluginInspectionFilters
    {
        // Iter-3 HIGH-1: PluginLoader.LoadFromDirectory composes EVERY *.dll; native C++ DLLs
        // (Utinni.CrtMatchPlugin.dll is the canonical example) reliably produce
        // BadImageFormatException / "is not a valid Win32 application" / "not a managed assembly"
        // messages during MEF DirectoryCatalog composition. Surfacing them as loadErrors noise
        // would defeat the regression-signal purpose of result.loaderObserved. Raw messages
        // remain available on the production Log.Error sink via PluginLoader.cs.
        // The three patterns below are part of the schemaVersion: 1 contract — bump
        // schemaVersion if the filter shape changes.
        private static readonly string[] NativeLoadErrorPatterns =
        {
            "BadImageFormatException",
            "is not a valid Win32 application",
            "not a managed assembly"
        };

        internal static IReadOnlyList<string> FilterNativeLoadErrors(IReadOnlyList<string> raw)
        {
            if (raw == null)
            {
                return Array.Empty<string>();
            }

            var filtered = new List<string>(raw.Count);
            foreach (var msg in raw)
            {
                if (msg == null)
                {
                    continue;
                }

                bool isNativeNoise = false;
                foreach (var pattern in NativeLoadErrorPatterns)
                {
                    if (msg.IndexOf(pattern, StringComparison.Ordinal) >= 0)
                    {
                        isNativeNoise = true;
                        break;
                    }
                }

                if (!isNativeNoise)
                {
                    filtered.Add(msg);
                }
            }

            return filtered;
        }
    }

    // ---------------------------------------------------------------------------
    // Main inspection logic.
    // ---------------------------------------------------------------------------

    public static class PluginInspection
    {
        /// <summary>
        /// Inspects all .dll files in the given directory and returns a DirectoryReport.
        /// Plan-checker iter-2 WARNING-5: return type is DirectoryReport (NOT IReadOnlyList&lt;PluginReport&gt;).
        /// REVIEWS HIGH-5: invokes PluginLoader.Load(dir) ONCE for the directory to get the
        /// authoritative managed-plugin count + filtered load errors.
        /// </summary>
        public static DirectoryReport InspectDirectory(string dir)
        {
            if (dir == null || !System.IO.Directory.Exists(dir))
            {
                return new DirectoryReport
                {
                    Directory = dir ?? string.Empty,
                    LoaderObserved = LoaderObservation.Empty,
                    Plugins = Array.Empty<PluginReport>(),
                    DirectoryChecks = new[]
                    {
                        new CheckResult
                        {
                            Id = "loader-vs-reflection-agreement",
                            Status = "pass",
                            Detail = "Empty directory; trivially agrees."
                        }
                    },
                    Summary = new ResultSummary { TotalPlugins = 0, Passing = 0, Failing = 0 }
                };
            }

            // Enumerate DLLs sorted by stem (Ordinal) for golden stability.
            var dlls = System.IO.Directory.GetFiles(dir, "*.dll", SearchOption.TopDirectoryOnly);
            Array.Sort(dlls, (a, b) =>
                StringComparer.Ordinal.Compare(
                    System.IO.Path.GetFileNameWithoutExtension(a),
                    System.IO.Path.GetFileNameWithoutExtension(b)));

            // REVIEWS HIGH-5: invoke PluginLoader ONCE for the directory.
            // Split assignment + invocation (two lines) — iter-3 MED-4 shape.
            var loader = new PluginLoader(autoLoad: false);
            loader.Load(dir);

            var filteredErrors = PluginInspectionFilters.FilterNativeLoadErrors(
                loader.LoadErrors as IReadOnlyList<string> ?? new List<string>(loader.LoadErrors));
            var loaderObs = new LoaderObservation
            {
                PluginsCount = loader.Plugins == null ? 0 : loader.Plugins.Count(),
                LoadErrors = filteredErrors
            };

            // Per-DLL classification (ReflectionOnly is the secondary fast-path).
            var reports = new List<PluginReport>(dlls.Length);
            foreach (var dllPath in dlls)
            {
                reports.Add(InspectSingle(dllPath));
            }

            // Iter-3 MED-8 directory-level cross-check.
            var dirChecks = new List<CheckResult>();
            bool managedIPluginPassFound = reports.Any(r =>
                r.Checks != null && r.Checks.Any(c => c.Id == "iplugin-export-shape" && c.Status == "pass"));

            if (loaderObs.PluginsCount > 0 && !managedIPluginPassFound)
            {
                dirChecks.Add(new CheckResult
                {
                    Id = "loader-vs-reflection-agreement",
                    Status = "warn",
                    Detail = "MEF discovered " + loaderObs.PluginsCount +
                             " plugins but ReflectionOnly saw 0 — possible inherited-export semantics issue. " +
                             "Consider Phase 6+ switch to loader.Plugins for per-DLL classification."
                });
            }
            else
            {
                dirChecks.Add(new CheckResult
                {
                    Id = "loader-vs-reflection-agreement",
                    Status = "pass",
                    Detail = "Loader and reflection agree."
                });
            }

            int passing = reports.Count(r => r.OverallStatus == "pass");
            int failing = reports.Count(r => r.OverallStatus == "fail");

            return new DirectoryReport
            {
                Directory = dir,
                LoaderObserved = loaderObs,
                Plugins = reports,
                DirectoryChecks = dirChecks,
                Summary = new ResultSummary
                {
                    TotalPlugins = reports.Count,
                    Passing = passing,
                    Failing = failing
                }
            };
        }

        private static PluginReport InspectSingle(string dllPath)
        {
            string id = System.IO.Path.GetFileNameWithoutExtension(dllPath);

            // Native probe via PEReader (REVIEWS HIGH-1 — no LoadLibrary).
            bool hasCreatePlugin = NativeExportProbe.HasExport(dllPath, "createPlugin");
            bool hasDestroyPlugin = NativeExportProbe.HasExport(dllPath, "destroyPlugin");

            // Managed probe via ReflectionOnlyLoadFrom (secondary fast-path for kind classification).
            // ReflectionOnly context requires all assembly dependencies to be pre-loaded or
            // resolved via ReflectionOnlyAssemblyResolve. We register a resolver to handle
            // System.ComponentModel.Composition (for InheritedExportAttribute) and other deps.
            bool iPluginAttributePresent = false;
            bool iPluginInterfaceImplemented = false;
            bool iEditorPluginAttributePresent = false;
            bool iEditorPluginInterfaceImplemented = false;

            // Alternative approach for managed attribute detection: read raw binary attribute data.
            // We use a two-pass strategy:
            //   Pass 1: Try ReflectionOnlyLoadFrom with dependency resolver.
            //   Pass 2 (fallback): If CustomAttributeData fails, scan the raw assembly byte stream
            //           for the InheritedExportAttribute type reference strings.
            try
            {
                ResolveEventHandler resolver = (sender, args) =>
                {
                    try
                    {
                        return Assembly.ReflectionOnlyLoad(args.Name);
                    }
                    catch
                    {
                        return null;
                    }
                };

                AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += resolver;

                try
                {
                    var asm = Assembly.ReflectionOnlyLoadFrom(dllPath);
                    foreach (var t in asm.GetTypes())
                    {
                        foreach (var cad in CustomAttributeData.GetCustomAttributes(t))
                        {
                            var attrType = cad.Constructor.DeclaringType;
                            if (attrType == null)
                            {
                                continue;
                            }

                            bool isInheritedExport = attrType.FullName != null &&
                                attrType.FullName.EndsWith("InheritedExportAttribute", StringComparison.Ordinal);
                            if (!isInheritedExport)
                            {
                                continue;
                            }

                            // Check constructor argument (the exported type).
                            if (cad.ConstructorArguments.Count < 1)
                            {
                                continue;
                            }

                            var arg = cad.ConstructorArguments[0].Value;
                            var argType = arg as Type;
                            if (argType == null)
                            {
                                // Argument may be a string if the Type couldn't be resolved —
                                // try string matching.
                                var argStr = arg as string;
                                if (argStr != null)
                                {
                                    if (argStr.EndsWith(".IPlugin", StringComparison.Ordinal))
                                    {
                                        iPluginAttributePresent = true;
                                    }
                                    else if (argStr.EndsWith(".IEditorPlugin", StringComparison.Ordinal))
                                    {
                                        iEditorPluginAttributePresent = true;
                                    }
                                }
                                continue;
                            }

                            if (argType.FullName != null && argType.FullName.EndsWith(".IPlugin", StringComparison.Ordinal))
                            {
                                iPluginAttributePresent = true;
                                // Check if the type actually implements IPlugin.
                                iPluginInterfaceImplemented = t.GetInterfaces()
                                    .Any(i => i.FullName != null && i.FullName.EndsWith(".IPlugin", StringComparison.Ordinal));
                            }
                            else if (argType.FullName != null && argType.FullName.EndsWith(".IEditorPlugin", StringComparison.Ordinal))
                            {
                                iEditorPluginAttributePresent = true;
                                iEditorPluginInterfaceImplemented = t.GetInterfaces()
                                    .Any(i => i.FullName != null && i.FullName.EndsWith(".IEditorPlugin", StringComparison.Ordinal));
                            }
                        }
                    }
                }
                finally
                {
                    AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= resolver;
                }
            }
            catch (BadImageFormatException)
            {
                // Native DLL — expected; continue with managed flags all false.
            }
            catch (ReflectionTypeLoadException)
            {
                // Malformed managed DLL — expected.
            }
            catch (FileLoadException)
            {
                // Dependency resolution failure — expected.
            }
            catch
            {
                // Any other reflection error — safe fallback.
            }

            // Fallback: if ReflectionOnly attribute scan returned no managed signals AND the
            // DLL is managed (no native exports), scan the raw bytes for known attribute strings.
            // This handles cases where ReflectionOnly fails due to missing transitive deps.
            if (!iPluginAttributePresent && !iEditorPluginAttributePresent
                && !hasCreatePlugin && !hasDestroyPlugin)
            {
                iPluginAttributePresent = RawBytesHasPluginAttributeString(dllPath, "UtinniCoreDotNet.PluginFramework.IPlugin");
                iEditorPluginAttributePresent = RawBytesHasPluginAttributeString(dllPath, "UtinniCoreDotNet.PluginFramework.IEditorPlugin");
                // Interface implementation cannot be determined from raw bytes — leave false.
            }

            // Kind discrimination (REVIEWS MEDIUM-9 — kind=unknown documented).
            // managedIPlugin = iPluginAttributePresent (attribute presence classifies as managed,
            // even if implementation is absent — dual-signal per iter-4 review consensus).
            bool managedIPlugin = iPluginAttributePresent;
            bool managedIEditorPlugin = iEditorPluginAttributePresent;

            bool hasNative = hasCreatePlugin || hasDestroyPlugin;
            bool hasManaged = managedIPlugin || managedIEditorPlugin;
            string kind = hasNative && hasManaged ? "mixed"
                        : hasNative ? "native"
                        : hasManaged ? "managed"
                        : "unknown";

            // Per-plugin checks in the FIXED order (createplugin-export, destroyplugin-export,
            // iplugin-export-shape, editor-plugin-export-shape).
            var checks = new List<CheckResult>(4);

            const string unknownDetail =
                "DLL is neither native-exporting (createPlugin/destroyPlugin) nor managed-plugin-shaped " +
                "([InheritedExport(typeof(IPlugin))]). Cannot validate as a Utinni plugin.";

            if (kind == "native")
            {
                checks.Add(new CheckResult
                {
                    Id = "createplugin-export",
                    Status = hasCreatePlugin ? "pass" : "fail",
                    Detail = hasCreatePlugin ? "createPlugin export found." : "createPlugin export missing."
                });
                checks.Add(new CheckResult
                {
                    Id = "destroyplugin-export",
                    Status = hasDestroyPlugin ? "pass" : "fail",
                    Detail = hasDestroyPlugin ? "destroyPlugin export found." : "destroyPlugin export missing."
                });
                checks.Add(new CheckResult
                {
                    Id = "iplugin-export-shape",
                    Status = "n/a",
                    Detail = "Plugin is native; MEF [InheritedExport] does not apply."
                });
                checks.Add(new CheckResult
                {
                    Id = "editor-plugin-export-shape",
                    Status = "n/a",
                    Detail = "Plugin is native; MEF [InheritedExport] does not apply."
                });
            }
            else if (kind == "managed")
            {
                checks.Add(new CheckResult
                {
                    Id = "createplugin-export",
                    Status = "n/a",
                    Detail = "Plugin is managed; native createPlugin/destroyPlugin do not apply."
                });
                checks.Add(new CheckResult
                {
                    Id = "destroyplugin-export",
                    Status = "n/a",
                    Detail = "Plugin is managed; native createPlugin/destroyPlugin do not apply."
                });

                // Dual-signal rule (iter-4 review consensus):
                // iPluginAttributePresent drives kind (=managedIPlugin above).
                // iPluginInterfaceImplemented drives shape compliance.
                bool iPluginShapePass = iPluginAttributePresent && iPluginInterfaceImplemented;
                string iPluginShapeDetail;
                if (iPluginAttributePresent && iPluginInterfaceImplemented)
                {
                    iPluginShapeDetail = "Type carries [InheritedExport(typeof(IPlugin))] and implements IPlugin.";
                }
                else if (iPluginAttributePresent && !iPluginInterfaceImplemented)
                {
                    iPluginShapeDetail = "Type carries [InheritedExport(typeof(IPlugin))] but does not implement IPlugin interface. " +
                                        "MEF would discover this DLL but composition would fail downstream.";
                }
                else
                {
                    iPluginShapeDetail = "[InheritedExport(typeof(IPlugin))] attribute not found.";
                }

                checks.Add(new CheckResult
                {
                    Id = "iplugin-export-shape",
                    Status = iPluginShapePass ? "pass" : "fail",
                    Detail = iPluginShapeDetail
                });

                // IEditorPlugin shape: n/a if IPlugin is already valid (IEditorPlugin is optional).
                bool iEditorShapePass = iEditorPluginAttributePresent && iEditorPluginInterfaceImplemented;
                string iEditorShapeStatus;
                string iEditorShapeDetail;
                if (iEditorPluginAttributePresent)
                {
                    iEditorShapeStatus = iEditorShapePass ? "pass" : "fail";
                    iEditorShapeDetail = iEditorShapePass
                        ? "Type carries [InheritedExport(typeof(IEditorPlugin))] and implements IEditorPlugin."
                        : "Type carries [InheritedExport(typeof(IEditorPlugin))] but does not implement IEditorPlugin.";
                }
                else if (iPluginShapePass)
                {
                    iEditorShapeStatus = "n/a";
                    iEditorShapeDetail = "Valid IPlugin; IEditorPlugin is not required.";
                }
                else
                {
                    iEditorShapeStatus = "fail";
                    iEditorShapeDetail = "[InheritedExport(typeof(IEditorPlugin))] attribute not found.";
                }

                checks.Add(new CheckResult
                {
                    Id = "editor-plugin-export-shape",
                    Status = iEditorShapeStatus,
                    Detail = iEditorShapeDetail
                });
            }
            else if (kind == "mixed")
            {
                checks.Add(new CheckResult
                {
                    Id = "createplugin-export",
                    Status = hasCreatePlugin ? "pass" : "fail",
                    Detail = hasCreatePlugin ? "createPlugin export found." : "createPlugin export missing."
                });
                checks.Add(new CheckResult
                {
                    Id = "destroyplugin-export",
                    Status = hasDestroyPlugin ? "pass" : "fail",
                    Detail = hasDestroyPlugin ? "destroyPlugin export found." : "destroyPlugin export missing."
                });

                bool iPluginShapePass = iPluginAttributePresent && iPluginInterfaceImplemented;
                checks.Add(new CheckResult
                {
                    Id = "iplugin-export-shape",
                    Status = iPluginShapePass ? "pass" : "fail",
                    Detail = iPluginShapePass
                        ? "Type carries [InheritedExport(typeof(IPlugin))] and implements IPlugin."
                        : "Type carries [InheritedExport(typeof(IPlugin))] but does not implement IPlugin interface."
                });

                bool iEditorShapePass = iEditorPluginAttributePresent && iEditorPluginInterfaceImplemented;
                checks.Add(new CheckResult
                {
                    Id = "editor-plugin-export-shape",
                    Status = iEditorShapePass ? "pass" : "fail",
                    Detail = iEditorShapePass
                        ? "Type carries [InheritedExport(typeof(IEditorPlugin))] and implements IEditorPlugin."
                        : "[InheritedExport(typeof(IEditorPlugin))] attribute not found or not implemented."
                });
            }
            else
            {
                // kind == "unknown" (REVIEWS MEDIUM-9): all four checks fail.
                checks.Add(new CheckResult { Id = "createplugin-export", Status = "fail", Detail = unknownDetail });
                checks.Add(new CheckResult { Id = "destroyplugin-export", Status = "fail", Detail = unknownDetail });
                checks.Add(new CheckResult { Id = "iplugin-export-shape", Status = "fail", Detail = unknownDetail });
                checks.Add(new CheckResult { Id = "editor-plugin-export-shape", Status = "fail", Detail = unknownDetail });
            }

            string overallStatus = checks.Any(c => c.Status == "fail") ? "fail" : "pass";

            return new PluginReport
            {
                Id = id,
                Path = dllPath,
                Kind = kind,
                Exports = new PluginExports
                {
                    CreatePlugin = hasCreatePlugin,
                    DestroyPlugin = hasDestroyPlugin,
                    ManagedIPlugin = managedIPlugin,
                    ManagedIEditorPlugin = managedIEditorPlugin
                },
                Checks = checks,
                OverallStatus = overallStatus
            };
        }

        /// <summary>
        /// Fallback: scan the raw bytes of a DLL for a UTF-8 string that would indicate
        /// an InheritedExport attribute referencing a specific type name.
        /// Used when ReflectionOnly fails due to missing transitive assembly dependencies.
        /// Returns true if the typeFullName string appears in the DLL's metadata stream.
        /// </summary>
        private static bool RawBytesHasPluginAttributeString(string dllPath, string typeFullName)
        {
            try
            {
                var bytes = System.IO.File.ReadAllBytes(dllPath);
                var needle = System.Text.Encoding.UTF8.GetBytes(typeFullName);
                int limit = bytes.Length - needle.Length;
                for (int i = 0; i <= limit; i++)
                {
                    bool match = true;
                    for (int j = 0; j < needle.Length; j++)
                    {
                        if (bytes[i + j] != needle[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // File read failure — safe fallback.
            }

            return false;
        }
    }
}
