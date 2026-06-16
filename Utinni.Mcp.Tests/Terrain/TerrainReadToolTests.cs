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

namespace Utinni.Mcp.Tests.Terrain;

/// <summary>
/// MCP <c>summarize_terrain</c> thin-shell read-tool dispatch (PROD-W2-TRN-04, D-10). The tool is a
/// <c>[McpServerTool(ReadOnly = true)]</c> wrapper with ZERO format logic (MCP-OOP lock): it resolves the
/// agent-supplied relative path under the pinned resolved-root and shells <c>utinni-cli decode-iff</c>
/// (which auto-routes a FORM TGEN root), passing the envelope through verbatim via
/// <see cref="CliResultMapper"/>. The net10 host hosts NO codec — the subprocess boundary to the x86
/// <c>utinni-cli</c> IS the architecture boundary (DEC-V2-MCP-OOP).
///
/// <para>These UNIT tests prove dispatch (verb + Resolve), verbatim envelope pass-through (both a low and
/// a high version envelope), the nonzero-CLI-exit → tool-error mapping (concern #5), and path-escape
/// fail-closed with ZERO CLI spawns — WITHOUT a real CLI (a recording dispatcher returns a canned
/// envelope / exit code). The end-to-end proof against the freshly-built x86 Release <c>utinni-cli.exe</c>
/// lives in <see cref="TerrainReadToolCliIntegrationTests"/>. Filter trait: <c>Terrain</c>.</para>
/// </summary>
public class TerrainReadToolTests
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

    // The navigable terrain envelope the decode-iff TGEN branch emits (Plan 03 BuildTerrainResult shape).
    // The synthesizer's low arm pins BREC=0002; the high arm pins BREC=0003 — the one observed divergence.
    private static string TerrainEnvelope(string brecVersion) =>
        "{\"command\":\"decode-iff\",\"result\":{\"layerCount\":1,\"layers\":[{\"active\":true," +
        "\"children\":[{\"editable\":true,\"fields\":[],\"kind\":\"typed\",\"stableId\":\"TGEN/0000/LYRS/LAYR#0/BREC#0\"," +
        "\"tag\":\"BREC\",\"version\":\"" + brecVersion + "\"}],\"name\":\"\"}],\"paletteCount\":6," +
        "\"palettes\":[],\"rootType\":\"TGEN\",\"type\":\"terrain\"},\"schemaVersion\":1}";

    // A malformed-input in-band error envelope (exit 2 = domain) — what decode-iff emits on a bad .trn.
    private static string MalformedErrorEnvelope() =>
        "{\"command\":\"decode-iff\",\"error\":{\"kind\":\"Decode\",\"message\":\"not a terrain template\"}," +
        "\"schemaVersion\":1}";

    private static (ResolvedRoot root, string rootDir) MakeRoot()
    {
        string rootDir = Path.Combine(Path.GetTempPath(), "utinni-mcp-terrain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDir);
        return (ResolvedRoot.PinOrThrow(new[] { "--root", rootDir }), rootDir);
    }

    private static string TextOf(CallToolResult result) => result.Content
        .OfType<TextContentBlock>()
        .Select(t => t.Text)
        .FirstOrDefault() ?? string.Empty;

    // ── Test 1: resolves under the pinned root and dispatches decode-iff ──

    [Fact]
    [Trait("Category", "Terrain")]
    public async Task SummarizeTerrain_ResolvesUnderRoot_DispatchesDecodeIff()
    {
        var cli = new RecordingDispatcher(0, TerrainEnvelope("0002"));
        (ResolvedRoot root, string rootDir) = MakeRoot();
        File.WriteAllBytes(Path.Combine(rootDir, "tatooine.trn"), new byte[] { 0 });

        CallToolResult result = await ReadTools.SummarizeTerrain(root, cli, "tatooine.trn");

        Assert.False(result.IsError ?? false);
        Assert.Single(cli.Invocations);
        Assert.Equal("decode-iff", cli.Invocations[0].Verb);
        Assert.Single(cli.Invocations[0].Args);
        string dispatched = cli.Invocations[0].Args[0];
        Assert.True(Path.IsPathRooted(dispatched), "dispatched path must be the resolved absolute path");
        Assert.StartsWith(Path.GetFullPath(rootDir), Path.GetFullPath(dispatched), StringComparison.OrdinalIgnoreCase);
    }

    // ── Test 2: schema pass-through — both a low and a high version envelope route verbatim ──

    [Theory]
    [Trait("Category", "Terrain")]
    [InlineData("0002")] // SWGEmu-era low arm (BREC 0002)
    [InlineData("0003")] // Infinity-era high arm (BREC 0003) — the one observed lineage divergence
    public async Task SummarizeTerrain_PassesNavigableEnvelopeThroughVerbatim_BothLineages(string brecVersion)
    {
        var cli = new RecordingDispatcher(0, TerrainEnvelope(brecVersion));
        (ResolvedRoot root, string rootDir) = MakeRoot();
        File.WriteAllBytes(Path.Combine(rootDir, "planet.trn"), new byte[] { 0 });

        CallToolResult result = await ReadTools.SummarizeTerrain(root, cli, "planet.trn");

        Assert.False(result.IsError ?? false);
        string text = TextOf(result);
        // The navigable terrain envelope passed through verbatim — schema pass-through, ZERO format logic.
        Assert.Contains("\"type\":\"terrain\"", text);
        Assert.Contains("\"rootType\":\"TGEN\"", text);
        Assert.Contains("\"layers\":", text);
        // The version word came straight from the CLI — the host neither read nor rewrote it.
        Assert.Contains("\"version\":\"" + brecVersion + "\"", text);
    }

    // ── Test 3: a nonzero CLI exit (malformed .trn → exit 2) is propagated as a TOOL ERROR (#5) ──

    [Fact]
    [Trait("Category", "Terrain")]
    public async Task SummarizeTerrain_MalformedInput_NonzeroExit_SurfacesToolError_NotSuccessEnvelope()
    {
        var cli = new RecordingDispatcher(2, MalformedErrorEnvelope());
        (ResolvedRoot root, string rootDir) = MakeRoot();
        File.WriteAllBytes(Path.Combine(rootDir, "bad.trn"), new byte[] { 0 });

        CallToolResult result = await ReadTools.SummarizeTerrain(root, cli, "bad.trn");

        // CliResultMapper maps exit 1/2/3 + a valid error envelope to an in-band TOOL ERROR (agent
        // self-corrects) — NOT a success envelope (concern #5).
        Assert.True(result.IsError ?? false);
        Assert.Contains("\"error\":", TextOf(result));
    }

    // ── Test 4: a path-escape relativePath surfaces a tool error BEFORE any CLI spawn ──

    [Theory]
    [Trait("Category", "Terrain")]
    [InlineData("../../outside.trn")]
    [InlineData("..\\..\\outside.trn")]
    [InlineData("subdir/../../escape.trn")]
    public async Task SummarizeTerrain_PathEscape_IsHardError_NoCliSpawned(string escapePath)
    {
        var cli = new SpawnRecordingDispatcher();
        (ResolvedRoot root, _) = MakeRoot();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await ReadTools.SummarizeTerrain(root, cli, escapePath));

        Assert.Equal(0, cli.Count); // boundary gate threw before any dispatch
    }
}
