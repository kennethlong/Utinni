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

using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Utinni.Mcp.Tools;

/// <summary>
/// The D-02 minimal live-tier surface (16-03): <c>live_ping</c> + <c>live_reload_asset</c>, the two
/// agent-facing tools that drive the injected SWG client over the named-pipe bridge.
///
/// <para><b>D-04 FAIL-CLOSED GATING — single registration path (LOCKED in Task 0, citing
/// Assumption A1):</b> this class is DELIBERATELY <b>NOT</b> decorated with
/// <c>[McpServerToolType]</c>. The host's <c>WithToolsFromAssembly()</c> assembly scan only
/// registers classes carrying <c>[McpServerToolType]</c>; a class without it is SKIPPED by the scan
/// (verified empirically by the off-state enumeration assert in
/// <c>LivePipeProtocolTests.LiveTools_AreAbsentWithoutEnableLive_PresentWithIt</c> — a real
/// <c>McpClient.ListToolsAsync()</c> round-trip, mirroring
/// <c>RoundTripTests.Handshake_ListsExactlyTheTwelveNamedTools</c>). The <c>[McpServerTool]</c>
/// method attributes below therefore reach the SDK ONLY via the explicit conditional
/// <c>WithTools&lt;LiveTools&gt;()</c> registration in <c>Program.cs</c>, which is itself gated on
/// <c>serverArgs.EnableLive</c> (<c>--enable-live</c> / <c>UTINNI_MCP_ENABLE_LIVE</c>). Net effect:
/// EXACTLY ONE registration path for <c>live_*</c> = the conditional one; the assembly scan never
/// also registers it. When <c>--enable-live</c> is OFF the tools are not advertised at all (D-04).</para>
///
/// <para>If a future SDK upgrade caused <c>WithToolsFromAssembly()</c> to pick up
/// <c>[McpServerTool]</c> methods in a class WITHOUT <c>[McpServerToolType]</c> (Assumption A1
/// falsified), the documented fallback (RESEARCH option b) is to scope the assembly scan to exclude
/// this namespace / register the non-live tools explicitly so <c>live_*</c> still NEVER registers
/// unless <c>--enable-live</c>. The off-state enumeration assert is the regression tripwire for that.</para>
///
/// <para>Bodies are filled in Task 2 (ping/reload dispatch + clientRoot diagnostic + root
/// reconciliation + C-14 reload candor). This Task-0 form carries placeholder bodies so the gating
/// enumeration test can prove the registration-path lock failing→passing NOW.</para>
///
/// <para><b>Non-static (NOT a static class) on purpose:</b> <c>WithTools&lt;T&gt;()</c> cannot take a
/// static type as its type argument (CS0718). The tool METHODS stay <c>static</c> (the SDK invokes
/// them statically); the CLASS is a plain class with a private ctor so it is never instantiated.</para>
/// </summary>
public class LiveTools
{
    private LiveTools() { }

    private const string PathParamDescription =
        "Asset path relative to the configured client root. Never an absolute path. " +
        "The relative path is what crosses the pipe; the in-client server re-resolves it under " +
        "its OWN pinned client root (root reconciliation).";

    [McpServerTool(Name = "live_ping", ReadOnly = true, Idempotent = true)]
    [Description("Ping the live-injected SWG client over the named-pipe bridge. Returns listening / " +
                 "gameRunning / pid AND the in-client resolved clientRoot diagnostic (compare against " +
                 "--root to confirm root alignment). Returns listening:false (never hangs) when no client " +
                 "is listening — the bridge requires BOTH --enable-live (this host) AND the in-client " +
                 "[Live] enableLiveBridge flag.")]
    public static async Task<CallToolResult> LivePing()
    {
        // Task 0 placeholder. Task 2 rewrites this signature to (LivePipeClient pipe) and fills the
        // body (pipe.PingAsync + the clientRoot diagnostic mapping). The Task-0 gating test only
        // enumerates the advertised tool list via McpClient.ListToolsAsync(), so a parameterless
        // placeholder suffices to PROVE the registration-path lock here.
        await Task.CompletedTask.ConfigureAwait(false);
        return new CallToolResult
        {
            IsError = false,
            Content = { new TextContentBlock { Text = "live_ping placeholder (Task 2 fills the body)." } }
        };
    }

    [McpServerTool(Name = "live_reload_asset", ReadOnly = false, Idempotent = false)]
    [Description("Queue a best-effort live reload of an edited asset over the bridge. CANDOR: queued:true " +
                 "does NOT mean a visible reload occurred — this is TRANSPORT-ONLY until RESID-03 " +
                 "(reloadAttempted is always false this phase). The path is validated under the MCP root, " +
                 "then the RELATIVE path is sent on the wire; the in-client server re-resolves it under " +
                 "its own pinned client root and returns the honest reload tier.")]
    public static async Task<CallToolResult> LiveReloadAsset(
        [Description(PathParamDescription)] string relativePath)
    {
        // Task 0 placeholder. Task 2 rewrites this signature to (ResolvedRoot root, LivePipeClient
        // pipe, string relativePath) and fills the body (root.Resolve validate → send RELATIVE path →
        // map ack tier/candor).
        await Task.CompletedTask.ConfigureAwait(false);
        return new CallToolResult
        {
            IsError = false,
            Content = { new TextContentBlock { Text = "live_reload_asset placeholder (Task 2 fills the body)." } }
        };
    }
}
