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

// ─────────────────────────────────────────────────────────────────────────────
// Phase 14 — Utinni.Mcp generic-host bootstrap (the centerpiece).
//
// CRITICAL (RESEARCH Pitfall 2): stdout is the MCP stdio transport. ALL host logging
// MUST go to stderr or it corrupts the JSON-RPC framing. LogToStandardErrorThreshold
// = Trace routes every log record to stderr; the round-trip test in Plan 14-04 proves
// the stdout channel is clean before MCP framing.
//
// NOTE (Task 1 of 14-01): this is the host skeleton. Task 2 of this plan wires in the
// fail-closed ResolvedRoot.PinOrThrow(args) singleton + the CliLocator/CliDispatcher
// subprocess seam (their classes land in Server/* with the Wave-0 unit tests). No MCP
// tools exist yet — the assembly scan finds none this wave; that is expected.
// ─────────────────────────────────────────────────────────────────────────────

var builder = Host.CreateApplicationBuilder(args);

// All logs to stderr — stdout is reserved for the MCP transport.
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// MCP server over stdio. The assembly scan finds no [McpServerTool] types yet — that is
// expected this wave; the tool plans (Wave-2/3) add them.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
