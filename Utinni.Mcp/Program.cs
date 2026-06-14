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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Utinni.Mcp.Server;
using Utinni.Mcp.Tools;

// ─────────────────────────────────────────────────────────────────────────────
// Phase 14 — Utinni.Mcp generic-host bootstrap (the centerpiece).
//
// CRITICAL (RESEARCH Pitfall 2): stdout is the MCP stdio transport. ALL host logging
// MUST go to stderr or it corrupts the JSON-RPC framing. LogToStandardErrorThreshold
// = Trace routes every log record to stderr; the round-trip test in Plan 14-04 proves
// the stdout channel is clean before MCP framing.
//
// Startup order is load-bearing:
//   1. ResolvedRoot.PinOrThrow(args) — fail-closed access-control pin BEFORE the
//      transport opens. No --root / UTINNI_MCP_ROOT and no existing dir => throws,
//      the server refuses to start (T-14-04).
//   2. CliLocator.Resolve(...) -> CliDispatcher singleton (the subprocess seam).
//   3. AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly() — the
//      assembly scan finds NO tools this wave (expected); Wave-2/3 plans add them.
// ─────────────────────────────────────────────────────────────────────────────

var builder = Host.CreateApplicationBuilder(args);

// All logs to stderr — stdout is reserved for the MCP transport.
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// (1) Fail-closed root pin. Throws before the transport opens if no root is configured
//     or the configured path does not exist (T-14-04). Registered as a singleton so the
//     Wave-2/3 tool classes resolve agent-supplied relative paths through it.
var resolvedRoot = ResolvedRoot.PinOrThrow(args);
builder.Services.AddSingleton(resolvedRoot);

// (2) Deterministic utinni-cli.exe locator -> the subprocess-dispatch seam. The locator
//     resolves an ABSOLUTE path (AppContext.BaseDirectory-relative; never CWD-dependent);
//     a missing exe surfaces as a hard ExeFound=false result at dispatch time, not a hang.
var serverArgs = ServerArgs.Parse(args);
var cliExePath = CliLocator.Resolve(serverArgs.CliPath);
builder.Services.AddSingleton(new CliDispatcher(cliExePath));

// (3) MCP server over stdio. WithToolsFromAssembly() registers every [McpServerToolType] class
//     (the read / save / repack / verify tools). It deliberately does NOT pick up LiveTools — that
//     class carries NO [McpServerToolType] attribute, so the scan skips it (D-04 fail-closed by
//     absence; the registration-path lock is proven by the off-state enumeration assert in
//     LivePipeProtocolTests).
var mcpBuilder = builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// (4) LIVE TIER (16-03, D-04): the live_* tools reach the SDK ONLY via this conditional
//     WithTools<LiveTools>() registration, gated on serverArgs.EnableLive (--enable-live /
//     UTINNI_MCP_ENABLE_LIVE). When the flag is OFF (default) the live tools are NOT advertised.
//     The LivePipeClient singleton (the live tools' dispatch dependency) is also registered ONLY
//     under this guard (C-13). Fail-closed by absence on both.
//
//     NOTE: the live-tier security surface (dual-flag operator contract, root-reconciliation
//     requirement, named-pipe ACL) is documented in
//     .planning/phases/14-headless-mcp-server-utinni-mcp-the-centerpiece/MCP-SECURITY.md (C-11).
if (serverArgs.EnableLive)
{
    builder.Services.AddSingleton(new LivePipeClient(LivePipeClient.PipeName));
    mcpBuilder.WithTools<LiveTools>();
}

await builder.Build().RunAsync();
