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

using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Utinni.Mcp.Server;

namespace Utinni.Mcp.Tools;

/// <summary>
/// The verify-only surface (14-03). <c>roundtrip_check</c> runs a <c>roundtrip-*</c> CLI verb that
/// applies ONE typed edit IN MEMORY and asserts byte-identity on the untouched region — WITHOUT
/// persisting anything to the asset. It is the explicit "dry-verify before you save" tool an agent
/// can call to confirm an edit will round-trip cleanly. ZERO format / business / domain-field logic:
/// resolve under root, dispatch one <c>roundtrip-*</c> verb, map the verify envelope opaquely.
///
/// <para><b>Non-persisting:</b> the <c>roundtrip-*</c> verbs DISCARD the mutated bytes (they verify
/// then throw the result away); nothing is written to the asset. To actually persist an edit, use a
/// <c>save_*</c> tool (which wraps the apply-save-* verb).</para>
/// </summary>
[McpServerToolType]
public static class VerifyTools
{
    private const string PathParamDescription =
        "Asset path relative to the configured client root. Never an absolute path.";

    [McpServerTool(Name = "roundtrip_check", ReadOnly = false, Idempotent = true)]
    [Description("VERIFY (do NOT persist): apply ONE typed edit in memory and confirm the untouched region is " +
                 "byte-identical, returning the verify envelope. Writes NOTHING to the asset — use a save_* tool to " +
                 "actually persist. Set format to 'datatable' | 'iff' | 'stringtable' | 'object_template' and supply " +
                 "the matching typed mutation (the same args the save_* tools take).")]
    public static Task<CallToolResult> RoundtripCheck(
        ResolvedRoot root,
        CliDispatcher cli,
        [Description(PathParamDescription)] string relativePath,
        [Description("Asset format: 'datatable', 'iff', 'stringtable', or 'object_template'.")] string format,
        // datatable
        [Description("datatable: 0-based record index for a cell edit (use with columnId + value).")] int recordIndex = -1,
        [Description("datatable: column id/name for a cell edit. iff: leaf id (alias). object_template: field name (alias).")] string? columnId = null,
        [Description("datatable cell value / object_template valueInt-as-string is not used here; see typed params.")] string? value = null,
        [Description("datatable: remove this 0-based row (>=0).")] int removeRow = -1,
        [Description("datatable: remove this column (index or name).")] string? removeColumn = null,
        // iff
        [Description("iff: stable leaf id to mutate (use with payloadHex).")] string? leafId = null,
        [Description("iff: replacement leaf payload hex (use with leafId).")] string? payloadHex = null,
        [Description("iff: stable leaf id to remove.")] string? removeLeaf = null,
        // stringtable
        [Description("stringtable: entry key to edit (use with stringValue).")] string? key = null,
        [Description("stringtable: replacement text (use with key).")] string? stringValue = null,
        // object_template
        [Description("object_template: field to add/edit/remove.")] string? field = null,
        [Description("object_template: operation 'add' | 'edit' | 'remove'.")] string? operation = null,
        [Description("object_template: int value for add/edit.")] int valueInt = 0)
    {
        // Boundary path-escape gate FIRST — throws on escape, NO CLI spawned. The roundtrip-* verbs
        // take a positional ABSOLUTE path (no --root flag), so we pass the resolved abs path.
        string abs = root.Resolve(relativePath);

        SaveVerb.SaveFormat fmt = ParseFormat(format);
        IReadOnlyList<string> argv = BuildRoundtripArgv(
            fmt, abs, recordIndex, columnId, value, removeRow, removeColumn,
            leafId, payloadHex, removeLeaf, key, stringValue, field, operation, valueInt);

        // Serialize per resolved path for parity with the write surface (a roundtrip read while a
        // save commits the same asset stays consistent), even though roundtrip itself never writes.
        return SaveVerb.RunSerializedAsync(abs, async () =>
        {
            CliInvocationResult r = await cli.RunAsync(SaveVerb.RoundtripVerb(fmt), argv).ConfigureAwait(false);
            return CliResultMapper.ToCallToolResult(r);
        });
    }

    private static SaveVerb.SaveFormat ParseFormat(string format) =>
        (format ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "datatable" or "tab" => SaveVerb.SaveFormat.Datatable,
            "iff" => SaveVerb.SaveFormat.Iff,
            "stringtable" or "stf" => SaveVerb.SaveFormat.StringTable,
            "object_template" or "ot" => SaveVerb.SaveFormat.ObjectTemplate,
            _ => throw new System.ArgumentException(
                "roundtrip_check: format must be 'datatable', 'iff', 'stringtable', or 'object_template'.", nameof(format))
        };

    // Builds the roundtrip-* argv (positional abs path + the same mutation flags as apply-save-*,
    // but NO --root). The verb validates mutation presence/exclusivity and returns in-band usage
    // errors the mapper surfaces — the host stays thin.
    private static IReadOnlyList<string> BuildRoundtripArgv(
        SaveVerb.SaveFormat fmt, string absPath,
        int recordIndex, string? columnId, string? value, int removeRow, string? removeColumn,
        string? leafId, string? payloadHex, string? removeLeaf,
        string? key, string? stringValue,
        string? field, string? operation, int valueInt)
    {
        var argv = new List<string> { absPath };

        switch (fmt)
        {
            case SaveVerb.SaveFormat.Datatable:
                if (!string.IsNullOrEmpty(removeColumn)) { argv.Add("--remove-column"); argv.Add(removeColumn!); }
                else if (removeRow >= 0) { argv.Add("--remove-row"); argv.Add(removeRow.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
                else
                {
                    argv.Add("--mutate-cell");
                    argv.Add(recordIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + (columnId ?? string.Empty));
                    argv.Add("--mutate-value"); argv.Add(value ?? string.Empty);
                }
                break;

            case SaveVerb.SaveFormat.Iff:
                if (!string.IsNullOrEmpty(removeLeaf)) { argv.Add("--remove-leaf"); argv.Add(removeLeaf!); }
                else { argv.Add("--mutate-leaf"); argv.Add(leafId ?? string.Empty); argv.Add("--mutate-hex"); argv.Add(payloadHex ?? string.Empty); }
                break;

            case SaveVerb.SaveFormat.StringTable:
                argv.Add("--edit-text"); argv.Add((key ?? string.Empty) + "=" + (stringValue ?? string.Empty));
                break;

            case SaveVerb.SaveFormat.ObjectTemplate:
                string op = (operation ?? string.Empty).Trim().ToLowerInvariant();
                if (op == "remove") { argv.Add("--remove-override"); argv.Add(field ?? string.Empty); }
                else if (op == "edit") { argv.Add("--edit"); argv.Add(field ?? string.Empty); argv.Add("--value-int"); argv.Add(valueInt.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
                else { argv.Add("--add-override"); argv.Add(field ?? string.Empty); argv.Add("--value-int"); argv.Add(valueInt.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
                break;
        }

        return argv;
    }
}
