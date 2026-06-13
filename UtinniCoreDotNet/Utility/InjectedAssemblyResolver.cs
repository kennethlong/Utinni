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

// ─────────────────────────────────────────────────────────────────────────────
// 15-12 Injected-host assembly-resolve probe DECISION helper (PROD-W2-PRT /
// RESID-03, 15-SMOKE Checklist B5 — BLOCKING).
//
// WHY THIS EXISTS (15-SMOKE B5 — loose-override Save fails for EVERY editor):
//   Under injection the managed runtime is brought up via
//   pClrRuntimeHost->ExecuteInDefaultAppDomain(<injectRoot>/UtinniCoreDotNet.dll,
//   "UtinniCoreDotNet.Startup", "EntryPoint", ...) (UtinniCore/clr.cpp:115-138).
//   ExecuteInDefaultAppDomain runs in the DEFAULT AppDomain whose APPBASE is the
//   HOST EXE directory (SWGEmu.exe), NOT the Utinni inject root where
//   UtinniCoreDotNet.dll lives. So the BCL assembly probe never searches the
//   inject root. `UtinniCoreDotNet.PathContainment` (the netstandard2.0 lib that
//   owns LooseOverridePath since Phase-14's [TypeForwardedTo] rework) needs the
//   `netstandard` 4.0.30319 façade to load inside the .NET-Framework host, and
//   that façade is NOT on the host-dir probe path → every loose-override Save
//   throws "Could not load file or assembly 'UtinniCoreDotNet.PathContainment …'".
//   The bug never surfaced in CLI/MCP because those run in a native net472
//   process where the façade resolves natively.
//
//   This helper carries the PROBE-PATH decision an AppDomain.AssemblyResolve
//   handler (installed at Startup.EntryPoint) consults: given the inject root and
//   a requested assembly display name, it maps a narrow allow-list of names to a
//   candidate dll under the inject root and returns the path ONLY when the file
//   actually exists on disk.
//
// FRAMEWORK-LEG DISCIPLINE (checker B-1 — mirrors WorldSnapshotCommandGuard /
// Phase-8 LooseOverridePath / Phase-9 CsvCellCoercion / LivePatchValidator):
//   PURE, BCL-ONLY. It does NOT reference the native UtinniCore game-bridge
//   namespace (UtinniCore.Utinni), the WinForms UI assembly, or the ground-scene
//   update-loop callbacks. The on-disk probe is injected as a Func<string,bool>
//   delegate so the DECISION (resolve-when-present / null-when-absent /
//   ignore-unrelated-names) is unit-tested independent of the live host filesystem.
//
// SECURITY (threat T-15-12-02): the allow-list is NARROW and file-existence-gated.
//   The handler must never become a general LoadFrom-anything resolver — any name
//   outside the allow-list returns null so unrelated binds are never hijacked to an
//   attacker-placed DLL in the inject root.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.IO;

namespace UtinniCoreDotNet.Utility
{
    /// <summary>
    /// Pure, BCL-only probe-path decision for the injected-host <c>AssemblyResolve</c> handler. Maps a
    /// requested assembly display name to a candidate dll path under the Utinni inject root, gated on a
    /// narrow allow-list (<c>netstandard</c> + <c>UtinniCoreDotNet.PathContainment</c>) and on the file
    /// actually existing on disk. Exists because <c>ExecuteInDefaultAppDomain</c> runs in the default
    /// AppDomain whose APPBASE is the host exe dir (SWGEmu.exe), so the inject root is not on the BCL
    /// probe path (15-SMOKE B5).
    /// </summary>
    public static class InjectedAssemblyResolver
    {
        /// <summary>
        /// The exact (and only) simple assembly names this resolver rescues. Kept as a single
        /// auditable list so the handled names are visible in one place (threat T-15-12-02 — the
        /// handler must stay narrow and never hijack unrelated resolution).
        /// </summary>
        private static readonly string[] HandledSimpleNames =
        {
            "netstandard",
            "UtinniCoreDotNet.PathContainment"
        };

        /// <summary>
        /// Decides the probe path for a requested assembly under the inject root.
        /// </summary>
        /// <param name="injectRoot">
        /// The directory of the executing <c>UtinniCoreDotNet.dll</c> (the Utinni inject root).
        /// </param>
        /// <param name="requestedAssemblyName">
        /// The assembly name from the resolve request. May be the FULL display name
        /// (<c>name, Version=…, Culture=…, PublicKeyToken=…</c>) the <c>AssemblyResolve</c> event hands;
        /// it is reduced to its simple name before the allow-list check.
        /// </param>
        /// <param name="fileExists">
        /// Injected on-disk probe (production passes <c>File.Exists</c>); lets the decision be unit-tested
        /// without a live filesystem.
        /// </param>
        /// <returns>
        /// The inject-root path of the candidate dll when the requested simple name is on the allow-list
        /// AND the file exists; otherwise <c>null</c> (let the default loader continue — never hijack).
        /// </returns>
        public static string ResolveProbePath(string injectRoot, string requestedAssemblyName, Func<string, bool> fileExists)
        {
            if (string.IsNullOrEmpty(injectRoot) || string.IsNullOrEmpty(requestedAssemblyName) || fileExists == null)
            {
                return null;
            }

            // The AssemblyResolve event passes the FULL display name; reduce to the simple name
            // (the substring before the first comma, trimmed).
            string simpleName = requestedAssemblyName;
            int comma = simpleName.IndexOf(',');
            if (comma >= 0)
            {
                simpleName = simpleName.Substring(0, comma);
            }
            simpleName = simpleName.Trim();

            // Narrow allow-list gate: anything else is not ours (threat T-15-12-02).
            bool handled = false;
            foreach (string handledName in HandledSimpleNames)
            {
                if (string.Equals(simpleName, handledName, StringComparison.OrdinalIgnoreCase))
                {
                    handled = true;
                    break;
                }
            }
            if (!handled)
            {
                return null;
            }

            // Probe the inject root; only claim the path when the file is actually present.
            string candidate = Path.Combine(injectRoot, simpleName + ".dll");
            return fileExists(candidate) ? candidate : null;
        }
    }
}
