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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utinni.Mcp.Server;
using Utinni.Mcp.Tools;
using Xunit;

namespace Utinni.Mcp.Tests;

/// <summary>
/// 14-03 Task 1 — the MCP-BOUNDARY path-escape proof (Cursor divergent, SC3 at the boundary, T-14-01).
/// A path-escape <c>relativePath</c> (e.g. <c>../../outside.tab</c>) is a HARD error AT THE MCP TOOL
/// BOUNDARY (<see cref="ResolvedRoot.Resolve"/> throws before any dispatch) and spawns NO CLI process
/// — proven by asserting the recording dispatcher recorded ZERO invocations. This covers a
/// <c>save_*</c> tool AND <c>repack_tre</c>.
/// </summary>
public class McpBoundaryPathEscapeTests
{
    // A dispatcher that FAILS the test if it is ever invoked (proves zero-spawn on escape).
    private sealed class SpawnRecordingDispatcher : CliDispatcher
    {
        public int Count;

        public SpawnRecordingDispatcher() : base(cliExePath: "must-never-spawn.exe") { }

        public override Task<CliInvocationResult> RunAsync(string verb, IReadOnlyList<string> args)
        {
            Interlocked.Increment(ref Count);
            return Task.FromResult(CliInvocationResult.Completed(0, "{}", string.Empty));
        }
    }

    private static ResolvedRoot MakeRoot()
    {
        string rootDir = Path.Combine(Path.GetTempPath(), "utinni-mcp-escape-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDir);
        return ResolvedRoot.PinOrThrow(new[] { "--root", rootDir });
    }

    [Theory]
    [InlineData("../../outside.tab")]
    [InlineData("..\\..\\outside.tab")]
    [InlineData("subdir/../../escape.tab")]
    public async Task SaveDatatable_PathEscape_IsHardError_NoCliSpawned(string escapePath)
    {
        var cli = new SpawnRecordingDispatcher();
        ResolvedRoot root = MakeRoot();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await SaveTools.SaveDatatable(root, cli, escapePath, recordIndex: 0, columnId: "0", value: "x"));

        // The boundary gate threw BEFORE any dispatch — zero spawns.
        Assert.Equal(0, cli.Count);
    }

    [Fact]
    public async Task RepackTre_PathEscape_IsHardError_NoCliSpawned_NoDryRunNotice()
    {
        var cli = new SpawnRecordingDispatcher();
        ResolvedRoot root = MakeRoot();

        // dry_run=false to prove the escape throws even on the spawn path (and dry_run=true to prove
        // the escape throws before the plan-only notice is ever produced).
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await RepackTool.RepackTre(root, cli, "../../outside.tre", dry_run: false));
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await RepackTool.RepackTre(root, cli, "../../outside.tre", dry_run: true));

        Assert.Equal(0, cli.Count);
    }

    [Fact]
    public async Task RepackTre_DryRunDefault_DoesNotSpawn()
    {
        var cli = new SpawnRecordingDispatcher();
        ResolvedRoot root = MakeRoot();

        // A CONTAINED path with the default dry_run=true must NOT spawn (plan-only host-side gate).
        var result = await RepackTool.RepackTre(root, cli, "archive.tre");

        Assert.Equal(0, cli.Count);
        Assert.False(result.IsError);
    }
}
