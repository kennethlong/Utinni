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

using Utinni.Mcp.Server;
using Xunit;

namespace Utinni.Mcp.Tests;

/// <summary>
/// Arg-parsing edge cases for <see cref="ServerArgs"/> (Codex review: specify + test these
/// rather than inlining ad-hoc parsing in Program.cs). Covers space form, equals form, missing
/// value tolerance, first-wins on duplicates, and symmetric --cli-path handling.
/// </summary>
public class ServerArgsTests
{
    [Fact]
    public void SpaceForm_Root_IsParsed()
    {
        var result = ServerArgs.Parse(new[] { "--root", @"C:\x" });
        Assert.Equal(@"C:\x", result.Root);
    }

    [Fact]
    public void EqualsForm_Root_IsParsed()
    {
        var result = ServerArgs.Parse(new[] { @"--root=C:\x" });
        Assert.Equal(@"C:\x", result.Root);
    }

    [Fact]
    public void Root_WithNoFollowingValue_LeavesRootNull()
    {
        // Trailing flag with no value must be tolerated (not crash) and leave Root null.
        var result = ServerArgs.Parse(new[] { "--root" });
        Assert.Null(result.Root);
    }

    [Fact]
    public void Root_FollowedByAnotherFlag_LeavesRootNull()
    {
        // The next token is itself a flag, so --root has no value (and --cli-path is still parsed).
        var result = ServerArgs.Parse(new[] { "--root", "--cli-path", @"C:\cli.exe" });
        Assert.Null(result.Root);
        Assert.Equal(@"C:\cli.exe", result.CliPath);
    }

    [Fact]
    public void DuplicateRoot_FirstWins()
    {
        var result = ServerArgs.Parse(new[] { "--root", "A", "--root", "B" });
        Assert.Equal("A", result.Root);
    }

    [Fact]
    public void QuotedValue_AlreadyDequotedByOsArgv_IsUnchanged()
    {
        // The OS argv splitter de-quotes "C:\Program Files\x" before it reaches Parse; the parser
        // sees a single token with the space embedded and must return it verbatim.
        var result = ServerArgs.Parse(new[] { "--root", @"C:\Program Files\x" });
        Assert.Equal(@"C:\Program Files\x", result.Root);
    }

    [Fact]
    public void CliPath_SpaceForm_IsParsed()
    {
        var result = ServerArgs.Parse(new[] { "--cli-path", @"C:\tools\utinni-cli.exe" });
        Assert.Equal(@"C:\tools\utinni-cli.exe", result.CliPath);
    }

    [Fact]
    public void CliPath_EqualsForm_IsParsed()
    {
        var result = ServerArgs.Parse(new[] { @"--cli-path=C:\tools\utinni-cli.exe" });
        Assert.Equal(@"C:\tools\utinni-cli.exe", result.CliPath);
    }

    [Fact]
    public void BothFlags_ParsedTogether()
    {
        var result = ServerArgs.Parse(new[] { "--root", @"C:\root", "--cli-path", @"C:\cli.exe" });
        Assert.Equal(@"C:\root", result.Root);
        Assert.Equal(@"C:\cli.exe", result.CliPath);
    }

    [Fact]
    public void EmptyArgs_BothNull()
    {
        var result = ServerArgs.Parse(System.Array.Empty<string>());
        Assert.Null(result.Root);
        Assert.Null(result.CliPath);
    }

    [Fact]
    public void UnknownArgs_AreIgnored()
    {
        var result = ServerArgs.Parse(new[] { "--verbose", "--unknown", "value", "--root", @"C:\x" });
        Assert.Equal(@"C:\x", result.Root);
        Assert.Null(result.CliPath);
    }

    // ─────────────────────────────────────────────────────────────────────
    // --enable-live (16-02 Task 2; CUR-NEW-6 dual-flag contract, fail-closed default).
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void EnableLive_FlagPresent_IsTrue()
    {
        WithEnvCleared(() =>
        {
            var result = ServerArgs.Parse(new[] { "--enable-live" });
            Assert.True(result.EnableLive);
        });
    }

    [Fact]
    public void EnableLive_FlagAbsent_DefaultsFalse()
    {
        WithEnvCleared(() =>
        {
            var result = ServerArgs.Parse(new[] { "--root", @"C:\x" });
            Assert.False(result.EnableLive); // fail-closed by absence (D-04)
        });
    }

    [Fact]
    public void EnableLive_IsPresenceFlag_DoesNotConsumeFollowingValue()
    {
        WithEnvCleared(() =>
        {
            // The token after --enable-live is NOT consumed as its value; --root still parses.
            var result = ServerArgs.Parse(new[] { "--enable-live", "--root", @"C:\x" });
            Assert.True(result.EnableLive);
            Assert.Equal(@"C:\x", result.Root);
        });
    }

    [Fact]
    public void EnableLive_CoexistsWithRootAndCliPath()
    {
        WithEnvCleared(() =>
        {
            var result = ServerArgs.Parse(new[] { "--root", @"C:\root", "--cli-path", @"C:\cli.exe", "--enable-live" });
            Assert.Equal(@"C:\root", result.Root);
            Assert.Equal(@"C:\cli.exe", result.CliPath);
            Assert.True(result.EnableLive);
        });
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("yes")]
    public void EnableLive_EnvVar_ExplicitTruthy_Enables(string envValue)
    {
        WithEnv(envValue, () =>
        {
            var result = ServerArgs.Parse(System.Array.Empty<string>());
            Assert.True(result.EnableLive);
        });
    }

    [Theory]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("garbage")]
    [InlineData("")]
    public void EnableLive_EnvVar_NonTruthy_DoesNotEnable(string envValue)
    {
        WithEnv(envValue, () =>
        {
            var result = ServerArgs.Parse(System.Array.Empty<string>());
            Assert.False(result.EnableLive); // not any-non-empty (Codex LOW)
        });
    }

    // Runs the action with UTINNI_MCP_ENABLE_LIVE explicitly cleared, restoring it afterwards.
    private static void WithEnvCleared(System.Action action)
    {
        WithEnv(null, action);
    }

    // Runs the action with UTINNI_MCP_ENABLE_LIVE set to the given value (null = unset), restoring it.
    private static void WithEnv(string? value, System.Action action)
    {
        const string name = "UTINNI_MCP_ENABLE_LIVE";
        string? prior = System.Environment.GetEnvironmentVariable(name);
        try
        {
            System.Environment.SetEnvironmentVariable(name, value);
            action();
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(name, prior);
        }
    }
}
