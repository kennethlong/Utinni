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
/// <see cref="CliLocator"/> resolution-order + Codex #11 absolute-path tests: an explicit override
/// wins, then UTINNI_CLI_PATH, then a deterministic ABSOLUTE AppContext.BaseDirectory probe — never
/// the bare CWD-relative name.
/// </summary>
public class CliLocatorTests
{
    private const string EnvName = "UTINNI_CLI_PATH";

    [Fact]
    public void ExplicitOverride_ReturnedVerbatim()
    {
        string? saved = Environment.GetEnvironmentVariable(EnvName);
        try
        {
            Environment.SetEnvironmentVariable(EnvName, @"C:\env\utinni-cli.exe");
            // Override beats env.
            string result = CliLocator.Resolve(@"C:\override\utinni-cli.exe");
            Assert.Equal(@"C:\override\utinni-cli.exe", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvName, saved);
        }
    }

    [Fact]
    public void EnvVar_ReturnedWhenNoOverride()
    {
        string? saved = Environment.GetEnvironmentVariable(EnvName);
        try
        {
            Environment.SetEnvironmentVariable(EnvName, @"C:\env\utinni-cli.exe");
            string result = CliLocator.Resolve(null);
            Assert.Equal(@"C:\env\utinni-cli.exe", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvName, saved);
        }
    }

    [Fact]
    public void NoOverrideNoEnv_ReturnsAbsoluteBaseDirectoryProbe()
    {
        string? saved = Environment.GetEnvironmentVariable(EnvName);
        try
        {
            Environment.SetEnvironmentVariable(EnvName, null);
            string result = CliLocator.Resolve(null);

            // Codex #11: must be an absolute path, never the bare CWD-relative name.
            Assert.True(Path.IsPathRooted(result), "CliLocator fallback must be an absolute path.");
            Assert.NotEqual("utinni-cli.exe", result);
            Assert.StartsWith(AppContext.BaseDirectory, result, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("utinni-cli.exe", result, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvName, saved);
        }
    }
}
