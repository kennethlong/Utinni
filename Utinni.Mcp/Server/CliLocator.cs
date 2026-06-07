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

namespace Utinni.Mcp.Server;

/// <summary>
/// Resolves the path to <c>utinni-cli.exe</c> for the dispatch seam. Mirrors the Phase-13
/// <c>NativeToolResolver</c> override → adjacent-probe shape, but with the Codex #11 fix: it
/// NEVER returns the bare CWD-relative name (<c>File.Exists("utinni-cli.exe")</c> depends on the
/// current working directory and does not PATH-probe). Instead the fallback is an ABSOLUTE path
/// rooted at <see cref="AppContext.BaseDirectory"/> — the dispatcher's <c>File.Exists</c> on that
/// absolute path then surfaces a clear not-found result regardless of CWD.
///
/// <para>Resolution order: explicit <c>--cli-path</c> override → <c>UTINNI_CLI_PATH</c> env →
/// absolute <c>AppContext.BaseDirectory</c>-adjacent probe.</para>
/// </summary>
public static class CliLocator
{
    private const string CliExeName = "utinni-cli.exe";

    /// <summary>
    /// Returns the resolved <c>utinni-cli.exe</c> path. The override / env value is returned
    /// verbatim; otherwise an ABSOLUTE adjacent-probe path under <see cref="AppContext.BaseDirectory"/>
    /// is returned (existing or not — the dispatcher's File.Exists surfaces the not-found case).
    /// </summary>
    public static string Resolve(string? cliPathOverride)
    {
        if (!string.IsNullOrEmpty(cliPathOverride))
        {
            return cliPathOverride;
        }

        string? envPath = Environment.GetEnvironmentVariable("UTINNI_CLI_PATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            return envPath;
        }

        // Codex #11: deterministic ABSOLUTE path, independent of the current working directory.
        // AppContext.BaseDirectory is the directory of the running host assembly (where the
        // deployed utinni-cli.exe is staged alongside Utinni.Mcp.exe). If it does not exist there,
        // the absolute path still produces a sensible "not found at <abs>" error downstream.
        return System.IO.Path.Combine(AppContext.BaseDirectory, CliExeName);
    }
}
