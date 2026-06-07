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
using Utinni.Mcp.Server;
using Xunit;

namespace Utinni.Mcp.Tests;

/// <summary>
/// SC3 access-control boundary tests for <see cref="ResolvedRoot"/>. The escape cases prove
/// <see cref="ResolvedRoot.Resolve"/> delegates to the shared netstandard2.0 LooseOverridePath
/// containment defenses; the PinOrThrow cases prove the fail-closed startup pin (T-14-04) and
/// the --root vs UTINNI_MCP_ROOT precedence.
///
/// <para>The pin tests use a REAL temp directory (so Directory.Exists passes) and clear the
/// env var around the precedence assertions so the host machine's environment cannot leak in.</para>
/// </summary>
public class ResolvedRootTests
{
    private const string EnvName = "UTINNI_MCP_ROOT";

    // Pin against a real temp dir so the escape-case Resolve() calls have an existing root.
    private static ResolvedRoot PinTempRoot(out string tempRoot)
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "utinni-mcp-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string? saved = Environment.GetEnvironmentVariable(EnvName);
        try
        {
            Environment.SetEnvironmentVariable(EnvName, null);
            return ResolvedRoot.PinOrThrow(new[] { "--root", tempRoot });
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvName, saved);
        }
    }

    // ── SC3 escape rejections (each delegates to LooseOverridePath and must throw) ──

    [Fact]
    public void Resolve_DotDotTraversal_Throws()
    {
        var root = PinTempRoot(out _);
        Assert.Throws<ArgumentException>(() => root.Resolve(@"..\..\etc"));
    }

    [Fact]
    public void Resolve_DriveRooted_Throws()
    {
        var root = PinTempRoot(out _);
        Assert.Throws<ArgumentException>(() => root.Resolve(@"C:\evil.iff"));
    }

    [Fact]
    public void Resolve_UncRooted_Throws()
    {
        var root = PinTempRoot(out _);
        Assert.Throws<ArgumentException>(() => root.Resolve(@"\\unc\x"));
    }

    [Fact]
    public void Resolve_DriveRelative_Throws()
    {
        var root = PinTempRoot(out _);
        Assert.Throws<ArgumentException>(() => root.Resolve("D:foo"));
    }

    [Fact]
    public void Resolve_PrefixAttackSibling_Throws()
    {
        // Pin a root literally named "...\swg-client" and feed a relative path that tries to
        // climb into the sibling "...\swg-clientx\loot". The '..' segment defense rejects it.
        string baseDir = Path.Combine(Path.GetTempPath(), "utinni-mcp-prefix-" + Guid.NewGuid().ToString("N"));
        string clientRoot = Path.Combine(baseDir, "swg-client");
        Directory.CreateDirectory(clientRoot);
        string? saved = Environment.GetEnvironmentVariable(EnvName);
        try
        {
            Environment.SetEnvironmentVariable(EnvName, null);
            var root = ResolvedRoot.PinOrThrow(new[] { "--root", clientRoot });
            Assert.Throws<ArgumentException>(() => root.Resolve(@"..\swg-clientx\loot"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvName, saved);
        }
    }

    [Fact]
    public void Resolve_LegitRelativePath_ResolvesUnderRoot()
    {
        var root = PinTempRoot(out string tempRoot);
        string full = root.Resolve(@"creature/path.iff");
        Assert.StartsWith(tempRoot + Path.DirectorySeparatorChar, full, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(@"creature\path.iff", full, StringComparison.OrdinalIgnoreCase);
    }

    // ── Fail-closed pin (T-14-04) ──

    [Fact]
    public void PinOrThrow_NoRootArgAndNoEnv_Throws()
    {
        string? saved = Environment.GetEnvironmentVariable(EnvName);
        try
        {
            Environment.SetEnvironmentVariable(EnvName, null);
            Assert.Throws<InvalidOperationException>(() => ResolvedRoot.PinOrThrow(Array.Empty<string>()));
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvName, saved);
        }
    }

    [Fact]
    public void PinOrThrow_RootArgPointsAtNonExistentDir_Throws()
    {
        string? saved = Environment.GetEnvironmentVariable(EnvName);
        try
        {
            Environment.SetEnvironmentVariable(EnvName, null);
            string missing = Path.Combine(Path.GetTempPath(), "utinni-mcp-missing-" + Guid.NewGuid().ToString("N"));
            Assert.Throws<InvalidOperationException>(() => ResolvedRoot.PinOrThrow(new[] { "--root", missing }));
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvName, saved);
        }
    }

    // ── Precedence: --root wins over env; env used when no --root ──

    [Fact]
    public void PinOrThrow_RootArgWinsOverEnv()
    {
        string argDir = Path.Combine(Path.GetTempPath(), "utinni-mcp-arg-" + Guid.NewGuid().ToString("N"));
        string envDir = Path.Combine(Path.GetTempPath(), "utinni-mcp-env-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(argDir);
        Directory.CreateDirectory(envDir);
        string? saved = Environment.GetEnvironmentVariable(EnvName);
        try
        {
            Environment.SetEnvironmentVariable(EnvName, envDir);
            var root = ResolvedRoot.PinOrThrow(new[] { "--root", argDir });
            Assert.Equal(Path.GetFullPath(argDir), root.Path);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvName, saved);
        }
    }

    [Fact]
    public void PinOrThrow_EnvUsedWhenNoRootArg()
    {
        string envDir = Path.Combine(Path.GetTempPath(), "utinni-mcp-envonly-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(envDir);
        string? saved = Environment.GetEnvironmentVariable(EnvName);
        try
        {
            Environment.SetEnvironmentVariable(EnvName, envDir);
            var root = ResolvedRoot.PinOrThrow(Array.Empty<string>());
            Assert.Equal(Path.GetFullPath(envDir), root.Path);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvName, saved);
        }
    }
}
