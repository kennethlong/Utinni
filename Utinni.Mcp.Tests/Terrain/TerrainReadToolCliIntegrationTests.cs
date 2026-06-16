// Implementation original to Utinni under MIT. No code or identifiers copied from any reference source.

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using Utinni.Mcp.Server;
using Utinni.Mcp.Tools;
using Xunit;

namespace Utinni.Mcp.Tests.Terrain;

/// <summary>
/// End-to-end proof that <c>summarize_terrain</c> shells the FRESHLY-BUILT x86 Release
/// <c>utinni-cli.exe</c> (the build output carrying the Plan-03 <c>decode-iff</c> TGEN branch — NOT a
/// stale PATH copy) and returns the navigable terrain envelope. Drives the production
/// <see cref="CliDispatcher"/> (a real subprocess spawn) over committed low/high <c>.trn</c> fixtures
/// generated once from the shipped x86 <c>UtinniCoreDotNet</c> IFF writer (see
/// <c>Fixtures/trn/README.md</c>) — the net10 test project cannot reference the net472 synthesizer
/// (cross-TFM), so the bytes are committed, mirroring the Plan-14 RoundTripTests fixture convention.
///
/// <para>Asserts: (a) a low-version (<c>terrain_low.trn</c>) and a high-version (<c>terrain_high.trn</c>)
/// fixture each route to the <c>type:terrain</c> / <c>rootType:TGEN</c> envelope (both lineages); (b) a
/// malformed <c>.trn</c> propagates the nonzero CLI exit as an MCP tool error (concern #5). Filter trait:
/// <c>Terrain</c>.</para>
/// </summary>
public sealed class TerrainReadToolCliIntegrationTests : IDisposable
{
    private const string LowFixture = "terrain_low.trn";
    private const string HighFixture = "terrain_high.trn";
    private const string MalformedFixture = "terrain_malformed.trn";

    private readonly string _cliExe;
    private readonly string _rootDir;
    private readonly CliDispatcher _cli;

    public TerrainReadToolCliIntegrationTests()
    {
        // Resolve the freshly-built x86 Release utinni-cli.exe by walking up to the repo root — NOT a
        // stale PATH copy. The CLI builds under x86/<cfg>/net472 (Platform=x86); fall back to <cfg>/net472.
        _cliExe = ResolveBuiltCli();

        _rootDir = Path.Combine(Path.GetTempPath(), "mcp_terrain_cli_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);

        // Copy the committed .trn fixtures into the resolved-root so Resolve() contains them.
        string fixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "trn");
        foreach (string name in new[] { LowFixture, HighFixture, MalformedFixture })
        {
            File.Copy(Path.Combine(fixturesDir, name), Path.Combine(_rootDir, name), overwrite: true);
        }

        _cli = new CliDispatcher(_cliExe, TimeSpan.FromSeconds(30));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_rootDir)) Directory.Delete(_rootDir, recursive: true); }
        catch { /* best-effort */ }
    }

    private ResolvedRoot Root() => ResolvedRoot.PinOrThrow(new[] { "--root", _rootDir });

    [Theory]
    [Trait("Category", "Terrain")]
    [InlineData(LowFixture)]  // SWGEmu-era low arm
    [InlineData(HighFixture)] // Infinity-era high arm — both lineages route through the SAME decode-iff branch
    public async Task SummarizeTerrain_RealCli_ReturnsNavigableTerrainEnvelope(string fixture)
    {
        CallToolResult result = await ReadTools.SummarizeTerrain(Root(), _cli, fixture);

        Assert.False(result.IsError ?? false);
        // Assert against the verbatim StructuredContent (robust to the CLI's pretty-print spacing) — the
        // host passed the navigable terrain envelope through unchanged (schema pass-through, zero logic).
        JsonElement env = StructuredOf(result);
        Assert.Equal("decode-iff", env.GetProperty("command").GetString());
        JsonElement resultObj = env.GetProperty("result");
        Assert.Equal("terrain", resultObj.GetProperty("type").GetString());
        Assert.Equal("TGEN", resultObj.GetProperty("rootType").GetString());
        Assert.True(resultObj.TryGetProperty("layers", out JsonElement layers));
        Assert.Equal(JsonValueKind.Array, layers.ValueKind);
    }

    [Fact]
    [Trait("Category", "Terrain")]
    public async Task SummarizeTerrain_RealCli_MalformedInput_PropagatesNonzeroExitAsToolError()
    {
        // A non-TGEN/non-IFF blob → decode-iff exits 2 (domain) with a valid error envelope →
        // CliResultMapper surfaces an in-band TOOL ERROR (IsError=true), never a success envelope (#5).
        CallToolResult result = await ReadTools.SummarizeTerrain(Root(), _cli, MalformedFixture);

        Assert.True(result.IsError ?? false, "a malformed .trn must surface as a tool error, not a success (#5)");
        JsonElement env = StructuredOf(result);
        Assert.True(env.TryGetProperty("error", out _), "the propagated envelope must carry the CLI error object");
        Assert.False(env.TryGetProperty("result", out _), "a malformed input must NOT carry a success result");
    }

    private static JsonElement StructuredOf(CallToolResult result)
    {
        Assert.NotNull(result.StructuredContent);
        return (JsonElement)result.StructuredContent!;
    }

    private static string ResolveBuiltCli()
    {
        const string exe = "utinni-cli.exe";
        bool preferRelease = AppContext.BaseDirectory
            .Replace('\\', '/').IndexOf("/release/", StringComparison.OrdinalIgnoreCase) >= 0;
        string[] cfgOrder = preferRelease ? new[] { "Release", "Debug" } : new[] { "Debug", "Release" };

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            foreach (string cfg in cfgOrder)
            {
                // Platform=x86 build → bin/x86/<cfg>/net472; SDK-default → bin/<cfg>/net472.
                string x86 = Path.Combine(dir.FullName, "Utinni.Cli", "bin", "x86", cfg, "net472", exe);
                if (File.Exists(x86)) return x86;
                string plain = Path.Combine(dir.FullName, "Utinni.Cli", "bin", cfg, "net472", exe);
                if (File.Exists(plain)) return plain;
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate the freshly-built utinni-cli.exe under Utinni.Cli/bin/[x86/]{Release,Debug}/net472 " +
            "walking up from " + AppContext.BaseDirectory + ". Build it first (VS2026 MSBuild Release|x86).");
    }
}
