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

// (3) MCP server over stdio. The assembly scan finds no [McpServerTool] types yet — that is
//     expected this wave; the tool plans (Wave-2/3) add them.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
