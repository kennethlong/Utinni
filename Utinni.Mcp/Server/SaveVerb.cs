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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Utinni.Mcp.Server;

/// <summary>
/// Shared argv-assembly + concurrency policy for the write surface (14-03). The four
/// <c>save_*</c> tools and the verify-only <c>roundtrip_check</c> tool route through here so the
/// format -> verb-name mapping and the typed-arg -> argv translation live in ONE place (no
/// duplication across tools). It carries ZERO format / business / domain-field logic — it only
/// builds the argv string list the <see cref="CliDispatcher"/> spawns and runs the dispatch under
/// a per-resolved-path lock.
///
/// <para><b>Verb-name mapping (Cursor divergent naming concern, 14-REVIEWS):</b>
/// <list type="bullet">
///   <item><see cref="SaveFormat.Datatable"/> -> <c>apply-save-tab</c> (typed DTII semantics)</item>
///   <item><see cref="SaveFormat.Iff"/> -> <c>apply-save-iff</c> (generic IFF leaf)</item>
///   <item><see cref="SaveFormat.StringTable"/> -> <c>apply-save-stf</c></item>
///   <item><see cref="SaveFormat.ObjectTemplate"/> -> <c>apply-save-ot</c></item>
/// </list>
/// The verify-only family uses the <c>roundtrip-*</c> sibling names.</para>
///
/// <para><b>Concurrent-dispatch policy (Cursor, T-14-16):</b> two parallel
/// <c>save_*</c>/<c>repack_tre</c> calls on the SAME resolved absolute path are SERIALIZED through
/// a per-path async lock (<see cref="RunSerializedAsync"/>); calls on DIFFERENT paths run in
/// parallel. The lock key is the case-insensitive resolved absolute path (Windows filesystem
/// semantics). This prevents two writers interleaving an atomic overwrite on one asset.</para>
/// </summary>
public static class SaveVerb
{
    /// <summary>The asset formats the write surface targets, each mapped to one apply-save verb.</summary>
    public enum SaveFormat
    {
        Datatable,
        Iff,
        StringTable,
        ObjectTemplate
    }

    /// <summary>Maps a format to its <c>apply-save-*</c> (persisting) CLI verb name.</summary>
    public static string ApplyVerb(SaveFormat format) => format switch
    {
        SaveFormat.Datatable => "apply-save-tab",
        SaveFormat.Iff => "apply-save-iff",
        SaveFormat.StringTable => "apply-save-stf",
        SaveFormat.ObjectTemplate => "apply-save-ot",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "unknown save format")
    };

    /// <summary>Maps a format to its <c>roundtrip-*</c> (verify-only, non-persisting) CLI verb name.</summary>
    public static string RoundtripVerb(SaveFormat format) => format switch
    {
        SaveFormat.Datatable => "roundtrip-tab",
        SaveFormat.Iff => "roundtrip-iff",
        SaveFormat.StringTable => "roundtrip-stf",
        SaveFormat.ObjectTemplate => "roundtrip-ot",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "unknown save format")
    };

    // ─── typed-arg -> argv builders (each verb's <relAsset> is RELATIVE; the verb re-resolves
    //     under --root as defense-in-depth, so the host passes the relative path + --root) ───

    /// <summary>Builds the datatable cell-edit argv: <c>&lt;rel&gt; --root R --mutate-cell row,col --mutate-value V</c>.</summary>
    public static IReadOnlyList<string> TabMutateCell(string relAsset, string rootPath, int recordIndex, string columnId, string value)
        => new List<string>
        {
            relAsset,
            "--root", rootPath,
            "--mutate-cell", recordIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + columnId,
            "--mutate-value", value ?? string.Empty
        };

    /// <summary>Builds the datatable remove-row argv.</summary>
    public static IReadOnlyList<string> TabRemoveRow(string relAsset, string rootPath, int rowIndex)
        => new List<string>
        {
            relAsset,
            "--root", rootPath,
            "--remove-row", rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

    /// <summary>Builds the datatable remove-column argv (index or name).</summary>
    public static IReadOnlyList<string> TabRemoveColumn(string relAsset, string rootPath, string columnIdOrName)
        => new List<string>
        {
            relAsset,
            "--root", rootPath,
            "--remove-column", columnIdOrName ?? string.Empty
        };

    /// <summary>Builds the IFF mutate-leaf argv: <c>&lt;rel&gt; --root R --mutate-leaf id --mutate-hex hex</c>.</summary>
    public static IReadOnlyList<string> IffMutateLeaf(string relAsset, string rootPath, string leafId, string payloadHex)
        => new List<string>
        {
            relAsset,
            "--root", rootPath,
            "--mutate-leaf", leafId ?? string.Empty,
            "--mutate-hex", payloadHex ?? string.Empty
        };

    /// <summary>Builds the IFF remove-leaf argv.</summary>
    public static IReadOnlyList<string> IffRemoveLeaf(string relAsset, string rootPath, string leafId)
        => new List<string>
        {
            relAsset,
            "--root", rootPath,
            "--remove-leaf", leafId ?? string.Empty
        };

    /// <summary>Builds the string-table edit-text argv: <c>&lt;rel&gt; --root R --edit-text KEY=VALUE</c>.</summary>
    public static IReadOnlyList<string> StfEditText(string relAsset, string rootPath, string key, string value)
        => new List<string>
        {
            relAsset,
            "--root", rootPath,
            "--edit-text", (key ?? string.Empty) + "=" + (value ?? string.Empty)
        };

    /// <summary>Builds the object-template add-override argv.</summary>
    public static IReadOnlyList<string> OtAddOverride(string relAsset, string rootPath, string field, int valueInt)
        => new List<string>
        {
            relAsset,
            "--root", rootPath,
            "--add-override", field ?? string.Empty,
            "--value-int", valueInt.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

    /// <summary>Builds the object-template edit argv.</summary>
    public static IReadOnlyList<string> OtEdit(string relAsset, string rootPath, string field, int valueInt)
        => new List<string>
        {
            relAsset,
            "--root", rootPath,
            "--edit", field ?? string.Empty,
            "--value-int", valueInt.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

    /// <summary>Builds the object-template remove-override argv.</summary>
    public static IReadOnlyList<string> OtRemoveOverride(string relAsset, string rootPath, string field)
        => new List<string>
        {
            relAsset,
            "--root", rootPath,
            "--remove-override", field ?? string.Empty
        };

    // ─── per-resolved-path serialization (T-14-16) ───

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Runs <paramref name="dispatch"/> under a lock keyed by <paramref name="resolvedAbsPath"/> so
    /// concurrent writes to the SAME asset are serialized while different assets proceed in parallel
    /// (T-14-16). The lock key is the case-insensitive resolved absolute path.
    /// </summary>
    public static async Task<T> RunSerializedAsync<T>(string resolvedAbsPath, Func<Task<T>> dispatch)
    {
        if (resolvedAbsPath == null) throw new ArgumentNullException(nameof(resolvedAbsPath));
        if (dispatch == null) throw new ArgumentNullException(nameof(dispatch));

        SemaphoreSlim gate = Locks.GetOrAdd(resolvedAbsPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await dispatch().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }
}
