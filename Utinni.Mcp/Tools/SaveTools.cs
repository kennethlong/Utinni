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
/// The write half of the centerpiece (14-03, MCP-02). Four <c>save_*</c> tools, each a THIN
/// dispatcher over the SINGLE matching Plan-14-03a <c>apply-save-*</c> CLI verb — ONE
/// <c>Process.Start</c> via <see cref="CliDispatcher"/>, the verb's envelope passed through
/// OPAQUELY by <see cref="CliResultMapper"/>. The host decides persist-vs-fail on the CLI EXIT
/// CODE alone (0 = persisted -> isError=false; 2 = verify-failed -> isError=true in-band) and
/// NEVER parses <c>bytesEqualUntouched</c> or any other domain field. Zero format / business /
/// domain-field logic; the apply-save-* verb (14-03a) does apply + verify + commit atomically in
/// one process (no TOCTOU window).
///
/// <para><b>Typed args only (never a free-form blob):</b> each tool takes the format-appropriate
/// typed mutation (record index / column id / typed value, etc.) so an agent supplies a STRUCTURED
/// edit, not "apply the change you inferred."</para>
///
/// <para><b>Path-escape gate (T-14-01):</b> the body resolves the agent-supplied RELATIVE path via
/// <see cref="ResolvedRoot.Resolve"/> FIRST — a path-escape (<c>../../outside.tab</c>, a rooted
/// candidate, a canonicalization escape) THROWS here, the SDK maps it to a hard tool error, and NO
/// CLI process is spawned (proven by <c>McpBoundaryPathEscapeTests</c>). The relative path then
/// stays RELATIVE in the argv (the verb re-resolves under <c>--root</c> as defense-in-depth).</para>
///
/// <para><b>Concurrency (T-14-16):</b> every dispatch runs under
/// <see cref="SaveVerb.RunSerializedAsync{T}"/> keyed by the resolved absolute path, so two parallel
/// writes to the SAME asset are serialized (different assets stay parallel).</para>
///
/// <para><c>.tre</c> archives are NEVER an apply-save target — the verbs reject them; the
/// destructive repack lives in the distinct <see cref="RepackTool"/> (unreachable from here).</para>
/// </summary>
[McpServerToolType]
public static class SaveTools
{
    private const string PathParamDescription =
        "Asset path relative to the configured client root (the loose-override write destination). Never an absolute path.";

    // ─────────────────────────────── save_datatable -> apply-save-tab ───────────────────────────────

    [McpServerTool(Name = "save_datatable", Destructive = true, Idempotent = false, ReadOnly = false)]
    [Description("Apply ONE typed datatable (.tab/.iff DTII) edit and persist it to the loose-override tier. " +
                 "Supply a single typed mutation: a cell edit (recordIndex + columnId + value), or set removeRow, " +
                 "or set removeColumn. Wraps the apply-save-tab verb (apply + byte-exact verify + atomic commit in " +
                 "one process). Persists only on a clean verify (exit 0); a failed verify is an in-band error and writes nothing.")]
    public static Task<CallToolResult> SaveDatatable(
        ResolvedRoot root,
        CliDispatcher cli,
        [Description(PathParamDescription)] string relativePath,
        [Description("0-based record (row) index for a cell edit. Use with columnId + value. Ignored when removeRow/removeColumn is set.")]
        int recordIndex = -1,
        [Description("Column id or name for a cell edit. Use with recordIndex + value.")]
        string? columnId = null,
        [Description("Replacement cell value (coerced per column type by the verb). Use with recordIndex + columnId.")]
        string? value = null,
        [Description("If >= 0, remove this 0-based row instead of editing a cell.")]
        int removeRow = -1,
        [Description("If set, remove this column (index or name) instead of editing a cell.")]
        string? removeColumn = null)
    {
        // (1) Boundary path-escape gate FIRST — throws on escape, NO CLI is spawned.
        string abs = root.Resolve(relativePath);

        // (2) Pick the typed mutation argv (relative path stays relative; verb re-resolves under --root).
        System.Collections.Generic.IReadOnlyList<string> argv =
            !string.IsNullOrEmpty(removeColumn) ? SaveVerb.TabRemoveColumn(relativePath, root.Path, removeColumn!)
            : removeRow >= 0 ? SaveVerb.TabRemoveRow(relativePath, root.Path, removeRow)
            : SaveVerb.TabMutateCell(relativePath, root.Path, recordIndex, columnId ?? string.Empty, value ?? string.Empty);

        // (3) ONE dispatch, serialized per resolved path; (4) opaque pass-through (decide on exit code).
        return SaveVerb.RunSerializedAsync(abs, async () =>
        {
            CliInvocationResult r = await cli.RunAsync(SaveVerb.ApplyVerb(SaveVerb.SaveFormat.Datatable), argv).ConfigureAwait(false);
            return CliResultMapper.ToCallToolResult(r);
        });
    }

    // ─────────────────────────────── save_iff -> apply-save-iff ───────────────────────────────

    [McpServerTool(Name = "save_iff", Destructive = true, Idempotent = false, ReadOnly = false)]
    [Description("Apply ONE generic IFF leaf edit and persist it to the loose-override tier. Supply either " +
                 "leafId + payloadHex (mutate a leaf's bytes) OR removeLeaf (remove a leaf). Wraps the apply-save-iff " +
                 "verb (apply + byte-exact verify of untouched leaves + atomic commit). Use save_datatable for typed " +
                 "DTII datatable semantics; this is the generic IFF-leaf surface. Persists only on a clean verify.")]
    public static Task<CallToolResult> SaveIff(
        ResolvedRoot root,
        CliDispatcher cli,
        [Description(PathParamDescription)] string relativePath,
        [Description("Stable id of the leaf chunk to mutate. Use with payloadHex.")]
        string? leafId = null,
        [Description("Hex string (no separators) of the replacement leaf payload. Use with leafId.")]
        string? payloadHex = null,
        [Description("If set, remove this leaf id instead of mutating a leaf.")]
        string? removeLeaf = null)
    {
        string abs = root.Resolve(relativePath);

        System.Collections.Generic.IReadOnlyList<string> argv =
            !string.IsNullOrEmpty(removeLeaf) ? SaveVerb.IffRemoveLeaf(relativePath, root.Path, removeLeaf!)
            : SaveVerb.IffMutateLeaf(relativePath, root.Path, leafId ?? string.Empty, payloadHex ?? string.Empty);

        return SaveVerb.RunSerializedAsync(abs, async () =>
        {
            CliInvocationResult r = await cli.RunAsync(SaveVerb.ApplyVerb(SaveVerb.SaveFormat.Iff), argv).ConfigureAwait(false);
            return CliResultMapper.ToCallToolResult(r);
        });
    }

    // ─────────────────────────────── save_stringtable -> apply-save-stf ───────────────────────────────

    [McpServerTool(Name = "save_stringtable", Destructive = true, Idempotent = false, ReadOnly = false)]
    [Description("Apply ONE string-table (.stf) text edit (set entry key's text to value) and persist it to the " +
                 "loose-override tier. Wraps the apply-save-stf verb (apply + byte-exact verify of untouched entries " +
                 "+ sourceCrc-preserved + atomic commit). Persists only on a clean verify.")]
    public static Task<CallToolResult> SaveStringtable(
        ResolvedRoot root,
        CliDispatcher cli,
        [Description(PathParamDescription)] string relativePath,
        [Description("The string-table entry key to edit.")] string key,
        [Description("The replacement text for the entry.")] string value)
    {
        string abs = root.Resolve(relativePath);

        System.Collections.Generic.IReadOnlyList<string> argv =
            SaveVerb.StfEditText(relativePath, root.Path, key, value);

        return SaveVerb.RunSerializedAsync(abs, async () =>
        {
            CliInvocationResult r = await cli.RunAsync(SaveVerb.ApplyVerb(SaveVerb.SaveFormat.StringTable), argv).ConfigureAwait(false);
            return CliResultMapper.ToCallToolResult(r);
        });
    }

    // ─────────────────────────────── save_object_template -> apply-save-ot ───────────────────────────────

    [McpServerTool(Name = "save_object_template", Destructive = true, Idempotent = false, ReadOnly = false)]
    [Description("Apply ONE typed object-template override mutation and persist it to the loose-override tier. " +
                 "Set operation to 'add', 'edit', or 'remove'; supply field, and valueInt for add/edit. Wraps the " +
                 "apply-save-ot verb (apply + byte-exact verify of untouched params + atomic commit). Persists only on a clean verify.")]
    public static Task<CallToolResult> SaveObjectTemplate(
        ResolvedRoot root,
        CliDispatcher cli,
        [Description(PathParamDescription)] string relativePath,
        [Description("The override field name to add / edit / remove.")] string field,
        [Description("The mutation operation: 'add' (new override), 'edit' (change existing), or 'remove'.")] string operation,
        [Description("Int value (absolute delta) for add/edit. Ignored for remove.")] int valueInt = 0)
    {
        string abs = root.Resolve(relativePath);

        string op = (operation ?? string.Empty).Trim().ToLowerInvariant();
        System.Collections.Generic.IReadOnlyList<string> argv = op switch
        {
            "remove" => SaveVerb.OtRemoveOverride(relativePath, root.Path, field),
            "edit" => SaveVerb.OtEdit(relativePath, root.Path, field, valueInt),
            // Default/'add': any unrecognized op falls through to add-override; the verb validates
            // and returns an in-band usage error (exit 1) which the mapper surfaces. The host stays thin.
            _ => SaveVerb.OtAddOverride(relativePath, root.Path, field, valueInt)
        };

        return SaveVerb.RunSerializedAsync(abs, async () =>
        {
            CliInvocationResult r = await cli.RunAsync(SaveVerb.ApplyVerb(SaveVerb.SaveFormat.ObjectTemplate), argv).ConfigureAwait(false);
            return CliResultMapper.ToCallToolResult(r);
        });
    }
}
