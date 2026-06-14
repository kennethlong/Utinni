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
using ModelContextProtocol.Client;
using Xunit;

namespace Utinni.Mcp.Tests;

/// <summary>
/// The live-bridge (MCP-03 host half, 16-03) test class. <c>--filter LivePipe</c> selects it.
///
/// <para>Task 0 lands the D-04 GATING LOCK here: a REAL <c>McpClient.ListToolsAsync()</c>
/// enumeration (mirroring <c>RoundTripTests.Handshake_ListsExactlyTheTwelveNamedTools</c>) launches
/// the BUILT <c>Utinni.Mcp.exe</c> TWICE — once WITHOUT <c>--enable-live</c> and once WITH it — and
/// asserts <c>live_ping</c>/<c>live_reload_asset</c> are ABSENT when off and PRESENT when on. This is
/// the real-enumeration proof that the live tools reach the SDK ONLY via the conditional
/// <c>WithTools&lt;LiveTools&gt;()</c> registration, never the assembly scan (C-10/CDX-NEW-5/6/CUR-NEW-8).
/// The off-state assert doubles as the explicit verification that <c>WithToolsFromAssembly()</c> skips
/// a class WITHOUT <c>[McpServerToolType]</c> (CDX-NEW-6).</para>
///
/// <para>Tasks 1+3 add the loopback protocol facts (golden-wire byte-equality, ping/reload round-trip,
/// framing edge cases, protocolVersion skew, no-client, name/framing fixture-equality) to this class.</para>
/// </summary>
public sealed partial class LivePipeProtocolTests
{
    // The non-live baseline surface (12 tools) the assembly scan registers regardless of --enable-live.
    private static readonly string[] BaselineTools =
    {
        "read_tre", "inspect_iff", "decode_iff", "summarize_particle", "list_world_objects",
        "get_template_schema",
        "save_iff", "save_datatable", "save_stringtable", "save_object_template",
        "repack_tre", "roundtrip_check"
    };

    private const string LivePingTool = "live_ping";
    private const string LiveReloadTool = "live_reload_asset";

    // ─────────────────────────────────────────────────────────────────────────────
    // Task 0: D-04 fail-closed gating, proven via real tool enumeration (C-10).
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LiveTools_AreAbsentWithoutEnableLive_PresentWithIt()
    {
        string serverExe = ResolveServerExe();
        string root = CreateTempRoot();
        try
        {
            // (a) WITHOUT --enable-live: the assembly scan must NOT advertise the live_* tools.
            //     This is also the explicit proof that WithToolsFromAssembly() skips a class without
            //     [McpServerToolType] (CDX-NEW-6).
            string[] offTools = await ListToolNamesAsync(serverExe, root, enableLive: false);
            Assert.DoesNotContain(LivePingTool, offTools);
            Assert.DoesNotContain(LiveReloadTool, offTools);
            // Sanity: the baseline non-live surface is still advertised when off.
            foreach (string baseline in BaselineTools)
            {
                Assert.Contains(baseline, offTools);
            }

            // (b) WITH --enable-live: the conditional WithTools<LiveTools>() registration advertises them.
            string[] onTools = await ListToolNamesAsync(serverExe, root, enableLive: true);
            Assert.Contains(LivePingTool, onTools);
            Assert.Contains(LiveReloadTool, onTools);
            // The baseline surface is unchanged when the live tier is on (live tools are additive).
            foreach (string baseline in BaselineTools)
            {
                Assert.Contains(baseline, onTools);
            }
        }
        finally
        {
            TryDeleteDir(root);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Shared host-launch helpers (mirroring RoundTripTests' real-McpClient pattern).
    // ─────────────────────────────────────────────────────────────────────────────

    private static async Task<string[]> ListToolNamesAsync(string serverExe, string root, bool enableLive)
    {
        List<string> arguments = new() { "--root", root };
        if (enableLive)
        {
            arguments.Add("--enable-live");
        }

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "utinni-mcp-live-gating",
            Command = serverExe,
            Arguments = arguments.ToArray(),
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using McpClient client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);
        IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: cts.Token);
        return tools.Select(t => t.Name).ToArray();
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "mcp_live_gate_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string ResolveServerExe()
    {
        // Mirror RoundTripTests.ResolveBuiltArtifact: walk up to the repo root and probe the built exe.
        bool preferRelease = AppContext.BaseDirectory
            .Replace('\\', '/').IndexOf("/release/", StringComparison.OrdinalIgnoreCase) >= 0;
        string[] cfgOrder = preferRelease ? new[] { "Release", "Debug" } : new[] { "Debug", "Release" };
        string projectBinRelative = Path.Combine("Utinni.Mcp", "bin");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            foreach (string cfg in cfgOrder)
            {
                string candidate = Path.Combine(dir.FullName, projectBinRelative, cfg, "net10.0", "Utinni.Mcp.exe");
                if (File.Exists(candidate)) return candidate;
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate built Utinni.Mcp.exe under any Utinni.Mcp/bin/{Release,Debug}/net10.0 " +
            "walking up from " + AppContext.BaseDirectory + ". Build it first (dotnet build Utinni.Mcp).");
    }

    private static void TryDeleteDir(string dir)
    {
        try { if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}
