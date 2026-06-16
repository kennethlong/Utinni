// Implementation original to Utinni under MIT. No code or identifiers copied from any reference source.

using Xunit;

namespace Utinni.Mcp.Tests.Terrain;

/// <summary>
/// MCP <c>summarize_terrain</c> thin-shell read-tool dispatch (PROD-W2-TRN-04, D-10). The tool is a
/// <c>[McpServerTool(ReadOnly = true)]</c> wrapper with ZERO format logic (MCP-OOP lock): it resolves the
/// agent-supplied relative path under the pinned resolved-root and shells <c>utinni-cli</c> (copying the
/// <c>summarize_particle</c> shape), passing the envelope through verbatim.
///
/// <para>All <c>Skip</c>-marked until Wave 3 (the tool is built in Plan 04). COLLECTED by the runner so the
/// wave inherits no silent gap. Filter trait: <c>Terrain</c>.</para>
/// </summary>
public class TerrainReadToolTests
{
    private const string WaveThree = "Wave 3 — pending summarize_terrain MCP tool (Plan 04)";

    [Fact(Skip = WaveThree)]
    [Trait("Category", "Terrain")]
    public void SummarizeTerrain_DispatchesDecodeIffVerb_ResolvesPathUnderRoot()
    {
        // TODO Wave 3: prove the tool resolves the relative path under the pinned root and dispatches the verb.
    }

    [Fact(Skip = WaveThree)]
    [Trait("Category", "Terrain")]
    public void SummarizeTerrain_PassesEnvelopeThroughVerbatim_ZeroFormatLogic()
    {
        // TODO Wave 3: assert the CLI envelope is passed through verbatim (no format logic in Utinni.Mcp — MCP-OOP).
    }

    [Fact(Skip = WaveThree)]
    [Trait("Category", "Terrain")]
    public void SummarizeTerrain_PathEscape_FailClosed_ZeroCliSpawns()
    {
        // TODO Wave 3: a path-escape attempt fails closed with ZERO CLI spawns (boundary mitigation).
    }

    [Fact(Skip = WaveThree)]
    [Trait("Category", "Terrain")]
    public void SummarizeTerrain_IsReadOnly_NoWriteOrMutateVerb()
    {
        // TODO Wave 3: confirm ReadOnly = true — the tool dispatches no write/mutate verb.
    }
}
