// Implementation original to Utinni under MIT. No code or identifiers copied from any reference source.

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

namespace Utinni.Mcp.Tests.Template;

/// <summary>
/// MCP <c>summarize_with_template</c> thin-shell read-tool dispatch (23-06, D-16, DEC-V2-MCP-OOP,
/// PROD-IFFT-03). The tool is a <c>[McpServerTool(ReadOnly = true)]</c> wrapper with ZERO format logic:
/// it resolves the agent-supplied relative path under the pinned resolved-root and shells
/// <c>utinni-cli decode-with-template</c> (the verbs-first schema-driven decode), passing the envelope
/// through verbatim via <see cref="CliResultMapper"/>. The net10 host hosts NO template/codec logic —
/// the subprocess boundary to the x86 <c>utinni-cli</c> IS the architecture boundary (MCP-OOP).
///
/// <para>These UNIT tests prove dispatch (verb + Resolve), verbatim envelope pass-through, the
/// nonzero-CLI-exit → tool-error mapping, and path-escape fail-closed with ZERO CLI spawns — WITHOUT a
/// real CLI (a recording dispatcher returns a canned envelope / exit code). Filter trait:
/// <c>Template</c>.</para>
/// </summary>
public class TemplateMcpToolTests
{
    // Records every spawn (verb + argv) and returns a canned envelope at a canned exit code — proves
    // dispatch + the exit-code → tool-error mapping WITHOUT a real CLI.
    private sealed class RecordingDispatcher : CliDispatcher
    {
        public readonly List<(string Verb, IReadOnlyList<string> Args)> Invocations = new();
        private readonly int _exitCode;
        private readonly string _stdout;
        private readonly string _stderr;

        public RecordingDispatcher(int exitCode, string stdout, string stderr = "")
            : base(cliExePath: "stub-not-used.exe")
        {
            _exitCode = exitCode;
            _stdout = stdout;
            _stderr = stderr;
        }

        public override Task<CliInvocationResult> RunAsync(string verb, IReadOnlyList<string> args)
        {
            Invocations.Add((verb, args.ToList()));
            return Task.FromResult(CliInvocationResult.Completed(_exitCode, _stdout, _stderr));
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

    // The navigable decode-with-template envelope the verb emits (23-05 decode-envelope + D-07 FitReport).
    private static string TemplateEnvelope() =>
        "{\"command\":\"decode-with-template\",\"result\":{\"matched\":true," +
        "\"template\":\"counted_records\",\"leaf\":\"CNTR/0001\",\"values\":[]," +
        "\"fitReport\":{\"fits\":true}},\"schemaVersion\":1}";

    // A no-match / domain-error envelope (exit 2) — what decode-with-template emits on an unmatched leaf.
    private static string NoMatchErrorEnvelope() =>
        "{\"command\":\"decode-with-template\",\"error\":{\"kind\":\"Decode\",\"message\":\"no template matched\"}," +
        "\"schemaVersion\":1}";

    private static (ResolvedRoot root, string rootDir) MakeRoot()
    {
        string rootDir = Path.Combine(Path.GetTempPath(), "utinni-mcp-template-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDir);
        return (ResolvedRoot.PinOrThrow(new[] { "--root", rootDir }), rootDir);
    }

    private static string TextOf(CallToolResult result) => result.Content
        .OfType<TextContentBlock>()
        .Select(t => t.Text)
        .FirstOrDefault() ?? string.Empty;

    // ── Test 1: resolves under the pinned root and dispatches decode-with-template ──

    [Fact]
    [Trait("Category", "Template")]
    public async Task SummarizeWithTemplate_ResolvesUnderRoot_DispatchesDecodeWithTemplate()
    {
        var cli = new RecordingDispatcher(0, TemplateEnvelope());
        (ResolvedRoot root, string rootDir) = MakeRoot();
        File.WriteAllBytes(Path.Combine(rootDir, "chunk.iff"), new byte[] { 0 });

        CallToolResult result = await ReadTools.SummarizeWithTemplate(root, cli, "chunk.iff");

        Assert.False(result.IsError ?? false);
        Assert.Single(cli.Invocations);
        Assert.Equal("decode-with-template", cli.Invocations[0].Verb);
        // The dispatched argv is the RESOLVED ABSOLUTE path under the pinned root (Resolve ran).
        Assert.Single(cli.Invocations[0].Args);
        string dispatched = cli.Invocations[0].Args[0];
        Assert.True(Path.IsPathRooted(dispatched), "dispatched path must be the resolved absolute path");
        Assert.StartsWith(Path.GetFullPath(rootDir), Path.GetFullPath(dispatched), StringComparison.OrdinalIgnoreCase);
    }

    // ── Test 2: the CLI envelope passes through verbatim (ZERO format logic in the MCP process) ──

    [Fact]
    [Trait("Category", "Template")]
    public async Task SummarizeWithTemplate_PassesEnvelopeThroughVerbatim()
    {
        var cli = new RecordingDispatcher(0, TemplateEnvelope());
        (ResolvedRoot root, string rootDir) = MakeRoot();
        File.WriteAllBytes(Path.Combine(rootDir, "chunk.iff"), new byte[] { 0 });

        CallToolResult result = await ReadTools.SummarizeWithTemplate(root, cli, "chunk.iff");

        string text = TextOf(result);
        // The decode-with-template envelope passed through verbatim — the host decided nothing on the bytes.
        Assert.Contains("\"command\":\"decode-with-template\"", text);
        Assert.Contains("\"matched\":true", text);
        Assert.Contains("\"fitReport\":", text);
        Assert.False(result.IsError ?? false);
    }

    // ── Test 3: a nonzero CLI exit (no template matched → exit 2) is propagated as a TOOL ERROR ──

    [Fact]
    [Trait("Category", "Template")]
    public async Task SummarizeWithTemplate_NoMatch_NonzeroExit_SurfacesToolError_NotSuccessEnvelope()
    {
        var cli = new RecordingDispatcher(2, NoMatchErrorEnvelope());
        (ResolvedRoot root, string rootDir) = MakeRoot();
        File.WriteAllBytes(Path.Combine(rootDir, "bad.iff"), new byte[] { 0 });

        CallToolResult result = await ReadTools.SummarizeWithTemplate(root, cli, "bad.iff");

        // CliResultMapper maps a nonzero exit + a valid error envelope to an in-band TOOL ERROR
        // (the agent self-corrects) — NOT a success envelope.
        Assert.True(result.IsError ?? false);
        Assert.Contains("\"error\":", TextOf(result));
    }

    // ── Test 4: a path-escape relativePath surfaces a tool error BEFORE any CLI spawn ──

    [Theory]
    [Trait("Category", "Template")]
    [InlineData("../../outside.iff")]
    [InlineData("..\\..\\outside.iff")]
    [InlineData("subdir/../../escape.iff")]
    public async Task SummarizeWithTemplate_PathEscape_IsHardError_NoCliSpawned(string escapePath)
    {
        var cli = new SpawnRecordingDispatcher();
        (ResolvedRoot root, _) = MakeRoot();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await ReadTools.SummarizeWithTemplate(root, cli, escapePath));

        Assert.Equal(0, cli.Count); // boundary gate threw before any dispatch
    }
}
