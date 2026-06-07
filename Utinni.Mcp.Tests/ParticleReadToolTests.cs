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
using ModelContextProtocol.Protocol;
using Utinni.Mcp.Server;
using Utinni.Mcp.Tools;
using Xunit;

namespace Utinni.Mcp.Tests;

/// <summary>
/// 15-04 Task 2 — the particle (<c>.prt</c>) MCP read tool (D-07/D-08). The tool is a THIN
/// dispatch-by-exit-code wrapper with ZERO format logic (D-06): it resolves the agent-supplied
/// relative path under the pinned <see cref="ResolvedRoot"/>, dispatches the single <c>decode-iff</c>
/// verb (which auto-routes a FORM PEFT root in the CLI), and passes the envelope through verbatim via
/// <see cref="CliResultMapper"/>. It is read-assist ONLY — <c>ReadOnly = true</c>, no write/mutate
/// verb. These tests prove dispatch (verb name + Resolve was called), boundary path-escape with ZERO
/// CLI spawns, and verbatim envelope pass-through.
/// </summary>
public class ParticleReadToolTests
{
    // Records every spawn (verb + argv) and returns a canned envelope — proves dispatch WITHOUT a real CLI.
    private sealed class RecordingDispatcher : CliDispatcher
    {
        public readonly List<(string Verb, IReadOnlyList<string> Args)> Invocations = new();
        private readonly string _stdout;

        public RecordingDispatcher(string stdout) : base(cliExePath: "stub-not-used.exe")
        {
            _stdout = stdout;
        }

        public override Task<CliInvocationResult> RunAsync(string verb, IReadOnlyList<string> args)
        {
            Invocations.Add((verb, args.ToList()));
            return Task.FromResult(CliInvocationResult.Completed(0, _stdout, string.Empty));
        }
    }

    // Fails the test if ever invoked — proves zero-spawn on a boundary escape.
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

    private static string ParticleEnvelope() =>
        "{\"command\":\"decode-iff\",\"result\":{\"type\":\"particle\",\"rootType\":\"PEFT\"," +
        "\"version\":\"0002\",\"emitterGroupCount\":1,\"emitterCount\":1,\"rawPreserved\":false}," +
        "\"schemaVersion\":1}";

    private static (ResolvedRoot root, string rootDir) MakeRoot()
    {
        string rootDir = Path.Combine(Path.GetTempPath(), "utinni-mcp-particle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDir);
        return (ResolvedRoot.PinOrThrow(new[] { "--root", rootDir }), rootDir);
    }

    // ── Test 1: resolves under the pinned root and dispatches the verb ──

    [Fact]
    public async Task SummarizeParticle_ResolvesUnderRoot_DispatchesDecodeIff()
    {
        var cli = new RecordingDispatcher(ParticleEnvelope());
        (ResolvedRoot root, string rootDir) = MakeRoot();

        // A real on-disk relative path under the root (Resolve requires the resolved path to be contained).
        File.WriteAllBytes(Path.Combine(rootDir, "effect.prt"), new byte[] { 0 });

        CallToolResult result = await ReadTools.SummarizeParticle(root, cli, "effect.prt");

        Assert.False(result.IsError ?? false);
        Assert.Single(cli.Invocations);
        Assert.Equal("decode-iff", cli.Invocations[0].Verb);
        // The dispatched argv is the RESOLVED ABSOLUTE path under the pinned root (Resolve ran).
        Assert.Single(cli.Invocations[0].Args);
        string dispatched = cli.Invocations[0].Args[0];
        Assert.True(Path.IsPathRooted(dispatched), "dispatched path must be the resolved absolute path");
        Assert.StartsWith(Path.GetFullPath(rootDir), Path.GetFullPath(dispatched), StringComparison.OrdinalIgnoreCase);
    }

    // ── Test 2: a path-escape is rejected at the boundary BEFORE any CLI spawn ──

    [Theory]
    [InlineData("../../outside.prt")]
    [InlineData("..\\..\\outside.prt")]
    [InlineData("subdir/../../escape.prt")]
    public async Task SummarizeParticle_PathEscape_IsHardError_NoCliSpawned(string escapePath)
    {
        var cli = new SpawnRecordingDispatcher();
        (ResolvedRoot root, _) = MakeRoot();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await ReadTools.SummarizeParticle(root, cli, escapePath));

        Assert.Equal(0, cli.Count); // boundary gate threw before any dispatch
    }

    // ── Test 3: the CLI envelope passes through verbatim (exit-code-only, no byte decisions) ──

    [Fact]
    public async Task SummarizeParticle_PassesEnvelopeThroughVerbatim()
    {
        var cli = new RecordingDispatcher(ParticleEnvelope());
        (ResolvedRoot root, string rootDir) = MakeRoot();
        File.WriteAllBytes(Path.Combine(rootDir, "effect.prt"), new byte[] { 0 });

        CallToolResult result = await ReadTools.SummarizeParticle(root, cli, "effect.prt");

        // The mapper surfaces the verbatim envelope text; the tool decided nothing on the bytes.
        string text = result.Content
            .OfType<TextContentBlock>()
            .Select(t => t.Text)
            .FirstOrDefault() ?? string.Empty;
        Assert.Contains("\"type\":\"particle\"", text);
        Assert.Contains("\"rootType\":\"PEFT\"", text);
        Assert.False(result.IsError ?? false);
    }
}
