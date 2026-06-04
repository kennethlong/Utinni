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
using System.IO;

namespace Utinni.Cli.Commands.Subprocess
{
    /// <summary>
    /// Plan 13-04/13-05: resolves a native build-tool exe path for the BUILD verbs. Prefers an
    /// explicit <c>--tool-path</c>; otherwise looks beside the utinni-cli assembly (the deployed
    /// staging convention), trying the release name then the <c>_d</c>/<c>_r</c>/<c>_o</c> build
    /// suffixes the SOE vcxprojs emit. Returns a best-effort path even if nothing exists — the
    /// caller's <see cref="NativeToolRunner"/> File.Exists check then surfaces exit 3 (FileNotFound).
    /// </summary>
    public static class NativeToolResolver
    {
        public static string Resolve(string toolPathOverride, string toolBaseName)
        {
            if (!string.IsNullOrEmpty(toolPathOverride))
            {
                return toolPathOverride;
            }

            string baseDir = AppContext.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, toolBaseName + ".exe"),
                Path.Combine(baseDir, toolBaseName + "_r.exe"),
                Path.Combine(baseDir, toolBaseName + "_d.exe"),
                Path.Combine(baseDir, toolBaseName + "_o.exe")
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            // Nothing found — return the canonical release name so the error message is sensible.
            return candidates[0];
        }
    }
}
