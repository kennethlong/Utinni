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
using Utinni.Mcp.Server;

namespace Utinni.Mcp.Tools;

/// <summary>
/// The read half of the centerpiece (14-02, MCP-01). Five ReadOnly+Idempotent MCP tools, each a
/// THIN attribute-decorated wrapper with ZERO format/business logic: resolve the agent-supplied
/// relative path under the pinned root (<see cref="ResolvedRoot.Resolve"/> — throws on escape, the
/// SDK maps that to a tool error, T-14-01), dispatch the single matching <c>utinni-cli</c> verb
/// (<see cref="CliDispatcher"/>), and hand the result to <see cref="CliResultMapper"/> for verbatim
/// envelope pass-through + shape validation + exit-code taxonomy.
///
/// <para>The four primary reads wrap <c>parse-tre</c> / <c>inspect-iff</c> / <c>decode-iff</c> /
/// <c>list-objects</c>. <c>decode_iff</c> is the SINGLE typed-read tool — the CLI auto-dispatches
/// datatable / string-table / object-template / appearance / structure by root FORM, so there are
/// NO per-format read tools. <c>inspect_iff</c> stays distinct (raw chunk tree vs typed model).</para>
///
/// <para><c>get_template_schema</c> is the ReadOnly schema fetch over
/// <c>compile-definition --skip-native</c> (no native compiler runs). It is the one tool that writes
/// to a host-temp <c>--out</c> OUTSIDE resolvedRoot via the IDisposable <see cref="TempSchemaOutput"/>
/// boundary (cleaned up on Dispose); the CLI emits the <c>{schemaPath,...}</c> envelope, the host
/// PARSES NOTHING from the temp file. The Phase-13 BUILD verbs (compile-template / build-tre /
/// compile-datatable / export-*) are deliberately NOT exposed here (D-01 defers them).</para>
/// </summary>
[McpServerToolType]
public static class ReadTools
{
    private const string PathParamDescription =
        "Asset path relative to the configured client root. Never an absolute path.";

    [McpServerTool(Name = "read_tre", ReadOnly = true, Idempotent = true)]
    [Description("Parse a .tre archive (header + record table) and return the utinni-cli JSON envelope.")]
    public static async Task<CallToolResult> ReadTre(
        ResolvedRoot root,
        CliDispatcher cli,
        [Description(PathParamDescription)] string relativePath)
    {
        string abs = root.Resolve(relativePath);
        CliInvocationResult r = await cli.RunAsync("parse-tre", new[] { abs }).ConfigureAwait(false);
        return CliResultMapper.ToCallToolResult(r);
    }

    [McpServerTool(Name = "inspect_iff", ReadOnly = true, Idempotent = true)]
    [Description("Emit the raw chunk tree of an IFF file (FORM/leaf structure) as the utinni-cli JSON envelope.")]
    public static async Task<CallToolResult> InspectIff(
        ResolvedRoot root,
        CliDispatcher cli,
        [Description(PathParamDescription)] string relativePath)
    {
        string abs = root.Resolve(relativePath);
        CliInvocationResult r = await cli.RunAsync("inspect-iff", new[] { abs }).ConfigureAwait(false);
        return CliResultMapper.ToCallToolResult(r);
    }

    [McpServerTool(Name = "decode_iff", ReadOnly = true, Idempotent = true)]
    [Description("Decode a typed IFF asset to JSON. Auto-dispatches by root FORM " +
                 "(datatable / string-table / object-template / appearance / structure) — the single typed-read tool.")]
    public static async Task<CallToolResult> DecodeIff(
        ResolvedRoot root,
        CliDispatcher cli,
        [Description(PathParamDescription)] string relativePath)
    {
        string abs = root.Resolve(relativePath);
        CliInvocationResult r = await cli.RunAsync("decode-iff", new[] { abs }).ConfigureAwait(false);
        return CliResultMapper.ToCallToolResult(r);
    }

    [McpServerTool(Name = "list_world_objects", ReadOnly = true, Idempotent = true)]
    [Description("List world-snapshot objects from a ws.iff and return the utinni-cli JSON envelope.")]
    public static async Task<CallToolResult> ListWorldObjects(
        ResolvedRoot root,
        CliDispatcher cli,
        [Description(PathParamDescription)] string relativePath)
    {
        string abs = root.Resolve(relativePath);
        CliInvocationResult r = await cli.RunAsync("list-objects", new[] { abs }).ConfigureAwait(false);
        return CliResultMapper.ToCallToolResult(r);
    }

    [McpServerTool(Name = "get_template_schema", ReadOnly = true, Idempotent = true)]
    [Description("Fetch the per-class param->type schema for an object-template definition (.tdf) " +
                 "via compile-definition --skip-native (no native compiler runs). Returns the utinni-cli " +
                 "JSON envelope reporting schemaPath/classCount. A host temp file (outside the client root) " +
                 "holds the schema artifact and is deleted after the call.")]
    public static async Task<CallToolResult> GetTemplateSchema(
        ResolvedRoot root,
        CliDispatcher cli,
        [Description("Path to a .tdf template-definition source, relative to the configured client root. Never an absolute path.")]
        string tdfRelativePath)
    {
        string abs = root.Resolve(tdfRelativePath);

        // The one documented writable boundary OUTSIDE resolvedRoot: a host temp --out, cleaned up
        // on Dispose regardless of outcome (T-14-13). The host parses NOTHING from this file.
        using var temp = TempSchemaOutput.Create();

        CliInvocationResult r = await cli.RunAsync(
            "compile-definition",
            new[] { "--tdf", abs, "--out", temp.Path, "--skip-native" }).ConfigureAwait(false);

        // The mapper passes the CLI's {schemaPath,...} envelope through verbatim (shape-validated).
        return CliResultMapper.ToCallToolResult(r);
    }
}
