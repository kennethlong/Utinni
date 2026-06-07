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
using UtinniCoreDotNet.Saving;

namespace Utinni.Mcp.Server;

/// <summary>
/// The MCP server's access-control boundary (T-14-01, T-14-04). Pinned ONCE at startup to an
/// absolute, canonical, existing root directory; every agent-supplied relative asset path is
/// resolved through <see cref="Resolve"/> which delegates to the shared netstandard2.0
/// <see cref="LooseOverridePath"/> containment helper (rooted / <c>..</c> / canonicalization /
/// prefix-attack defenses).
///
/// <para><b>Fail-closed (T-14-04):</b> <see cref="PinOrThrow"/> throws (the server refuses to
/// start, BEFORE the MCP transport opens) when neither <c>--root</c> nor <c>UTINNI_MCP_ROOT</c>
/// is set, or when the configured path does not resolve to an existing directory. No absolute
/// path is ever accepted from the agent at <see cref="Resolve"/> time.</para>
/// </summary>
public sealed class ResolvedRoot
{
    /// <summary>The absolute, canonical, existing root directory.</summary>
    public string Path { get; }

    private ResolvedRoot(string path)
    {
        Path = path;
    }

    /// <summary>
    /// Resolves the root from <c>--root</c> (arg) ?? <c>UTINNI_MCP_ROOT</c> (env), canonicalizes it,
    /// and verifies it exists. Throws <see cref="InvalidOperationException"/> with a clear,
    /// stderr-bound message on every fail-closed path (no root configured, invalid path, or
    /// non-existent directory).
    /// </summary>
    public static ResolvedRoot PinOrThrow(string[] args)
    {
        ServerArgs parsed = ServerArgs.Parse(args);

        // Precedence: explicit --root wins over the UTINNI_MCP_ROOT env fallback.
        string? configured = parsed.Root;
        if (string.IsNullOrEmpty(configured))
        {
            configured = Environment.GetEnvironmentVariable("UTINNI_MCP_ROOT");
        }

        if (string.IsNullOrEmpty(configured))
        {
            throw new InvalidOperationException(
                "Utinni.Mcp refuses to start: no client root configured. Pass --root <path> or set " +
                "the UTINNI_MCP_ROOT environment variable to the absolute path of the SWG client root.");
        }

        string canonical;
        try
        {
            canonical = System.IO.Path.GetFullPath(configured);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Utinni.Mcp refuses to start: the configured root '" + configured +
                "' is not a valid path: " + ex.Message, ex);
        }

        if (!Directory.Exists(canonical))
        {
            throw new InvalidOperationException(
                "Utinni.Mcp refuses to start: the configured root '" + canonical +
                "' does not exist or is not a directory.");
        }

        return new ResolvedRoot(canonical);
    }

    /// <summary>
    /// Resolves an agent-supplied RELATIVE asset path to an absolute path UNDER the pinned root,
    /// delegating to <see cref="LooseOverridePath.Resolve"/>. Throws on every escape class
    /// (rooted candidate, <c>..</c> traversal, canonicalization escape, prefix-match attack).
    /// </summary>
    public string Resolve(string relativeAssetPath)
    {
        return LooseOverridePath.Resolve(Path, relativeAssetPath);
    }
}
