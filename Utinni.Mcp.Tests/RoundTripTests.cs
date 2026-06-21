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
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Utinni.Mcp.Tests;

/// <summary>
/// Plan 14-04 Task 2 — the DEC-C3 automatable end-to-end proof (SC1 / SC2 / SC5). A REAL
/// <see cref="McpClient"/> completes the stdio handshake against the BUILT <c>Utinni.Mcp.exe</c>
/// (launched as a child process over <see cref="StdioClientTransport"/> with the
/// <c>utinni-cli.exe</c> path PINNED via <c>--cli-path</c>, Consensus #11) and drives:
///
/// <list type="bullet">
///   <item>Handshake + EXACT 11-tool surface by name (5 read + 4 save + repack_tre + roundtrip_check).</item>
///   <item>read_tre over the v0006 archive + decode_iff over .tab / .stf / OT (SC1 multi-format read).</item>
///   <item>The CENTERPIECE: save_datatable a cell edit, then READ BACK via decode_iff and assert the
///         edited cell holds the new value AND the file hash changed (a no-op re-serialize MUST fail) (SC2).</item>
///   <item>save_datatable whose verify would fail surfaces an in-band error AND leaves the file unchanged.</item>
///   <item>repack_tre dry_run=true no-write / dry_run=false rewrite + backup, on an ISOLATED COPY only (SC2).</item>
///   <item>A path-escape at the MCP boundary is a hard error and writes nothing outside the root.</item>
/// </list>
///
/// <para>There is NO recorded-transcript fallback (Consensus): if the child launch is flaky, the fix is
/// process launch / test isolation, never lowering the bar. On any failure the child's stderr is surfaced
/// for diagnostics while stdout stays reserved for the MCP framing.</para>
/// </summary>
public sealed class RoundTripTests : IAsyncLifetime
{
    private const string TabFixture = "sample.tab";
    private const string StfFixture = "sample.stf";
    private const string OtFixture = "sample_ot.iff";
    private const string TreFixture = "sample_v0006.tre";

    private static readonly string[] ExpectedTools =
    {
        "read_tre", "inspect_iff", "decode_iff", "summarize_particle", "summarize_terrain",
        "summarize_clienteffect", "summarize_with_template",
        "list_world_objects", "get_template_schema",
        "save_iff", "save_datatable", "save_stringtable", "save_object_template",
        "repack_tre", "roundtrip_check"
    };

    private string _tempRoot = string.Empty;
    private string _serverExe = string.Empty;
    private string _cliExe = string.Empty;

    public Task InitializeAsync()
    {
        _serverExe = ResolveBuiltArtifact(
            Path.Combine("Utinni.Mcp", "bin"), "net10.0", "Utinni.Mcp.exe");
        _cliExe = ResolveBuiltArtifact(
            Path.Combine("Utinni.Cli", "bin"), "net472", "utinni-cli.exe");

        // A fresh resolved-root per fixture-class instance; the committed fixtures are copied in.
        _tempRoot = Path.Combine(Path.GetTempPath(), "mcp_roundtrip_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        string fixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        foreach (string src in Directory.GetFiles(fixturesDir))
        {
            File.Copy(src, Path.Combine(_tempRoot, Path.GetFileName(src)), overwrite: true);
        }

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        TryDeleteDir(_tempRoot);
        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Handshake + exact tool surface (SC5)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handshake_ListsExactlyTheNamedTools()
    {
        await using McpClient client = await CreateClientAsync(_tempRoot);
        IList<McpClientTool> tools = await client.ListToolsAsync();

        var names = tools.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var expected = ExpectedTools.OrderBy(n => n, StringComparer.Ordinal).ToArray();

        // EXACT set + count — no "tolerate N optional" fudge (SC5).
        Assert.Equal(expected.Length, names.Length);
        Assert.Equal(expected, names);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Multi-format read (SC1): read_tre + decode_iff over .tab / .stf / OT
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadRoundTrip_ReadTre_ReturnsParseTreEnvelope()
    {
        await using McpClient client = await CreateClientAsync(_tempRoot);
        CallToolResult r = await CallAsync(client, "read_tre",
            new Dictionary<string, object?> { ["relativePath"] = TreFixture });

        Assert.False(r.IsError);
        JsonElement env = StructuredOf(r);
        Assert.Equal("parse-tre", env.GetProperty("command").GetString());
        JsonElement header = env.GetProperty("result").GetProperty("header");
        Assert.Equal("0006", header.GetProperty("version").GetString());
        Assert.Equal(2, header.GetProperty("recordCount").GetInt32());
    }

    [Theory]
    [InlineData(TabFixture, "datatable")]
    [InlineData(StfFixture, "stringtable")]
    [InlineData(OtFixture, "objecttemplate")]
    public async Task DecodeMultiFormat_DecodeIff_ReturnsTypedEnvelopeOfTheRightKind(string fixture, string expectedType)
    {
        await using McpClient client = await CreateClientAsync(_tempRoot);
        CallToolResult r = await CallAsync(client, "decode_iff",
            new Dictionary<string, object?> { ["relativePath"] = fixture });

        Assert.False(r.IsError);
        JsonElement env = StructuredOf(r);
        Assert.Equal("decode-iff", env.GetProperty("command").GetString());
        Assert.Equal(expectedType, env.GetProperty("result").GetProperty("type").GetString());
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // CENTERPIECE: edit -> save -> READ BACK (SC2). A no-op re-serialize MUST fail.
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EditSaveRoundTrip_SaveDatatable_ReadBackReflectsEditedCell_AndFileHashChanged()
    {
        await using McpClient client = await CreateClientAsync(_tempRoot);

        string assetAbs = Path.Combine(_tempRoot, TabFixture);
        byte[] before = File.ReadAllBytes(assetAbs);
        byte[] hashBefore = Sha(before);

        // Pre-edit read-back value at cell (0,0) — the int column "anInt" (fixture value 7).
        int preValue = await ReadCellInt(client, TabFixture, 0);
        int newValue = preValue + 12345;

        CallToolResult save = await CallAsync(client, "save_datatable", new Dictionary<string, object?>
        {
            ["relativePath"] = TabFixture,
            ["recordIndex"] = 0,
            ["columnId"] = "0", // column 0 = the int column "anInt"; --mutate-cell takes a numeric col index
            ["value"] = newValue.ToString(),
        });

        Assert.False(save.IsError);
        JsonElement saveEnv = StructuredOf(save);
        JsonElement saveResult = saveEnv.GetProperty("result");
        Assert.True(saveResult.GetProperty("written").GetBoolean());
        Assert.True(saveResult.GetProperty("validated").GetBoolean());
        Assert.True(saveResult.GetProperty("bytesEqualUntouched").GetBoolean());

        // READ-BACK: decode the SAME loose path and assert the edited cell now holds <newValue>.
        int postValue = await ReadCellInt(client, TabFixture, 0);
        Assert.Equal(newValue, postValue);

        // The persisted bytes are the POST-mutation bytes — hash MUST differ (no-op re-serialize fails here).
        byte[] after = File.ReadAllBytes(assetAbs);
        Assert.False(hashBefore.SequenceEqual(Sha(after)), "the saved file must differ from the pre-edit file");
    }

    [Fact]
    public async Task EditSaveVerifyFail_SurfacesInBandError_AndLeavesFileUnchanged()
    {
        await using McpClient client = await CreateClientAsync(_tempRoot);

        string assetAbs = Path.Combine(_tempRoot, TabFixture);
        byte[] before = File.ReadAllBytes(assetAbs);

        // A cell edit on an OUT-OF-RANGE row -> the apply-save-tab verb fails (non-zero exit);
        // the host surfaces it as an in-band error and writes NOTHING (fail-closed, no partial write).
        CallToolResult save = await CallAsync(client, "save_datatable", new Dictionary<string, object?>
        {
            ["relativePath"] = TabFixture,
            ["recordIndex"] = 999,
            ["columnId"] = "0",
            ["value"] = "1",
        });

        Assert.True(save.IsError, "a failing apply-save-tab must surface IsError=true in-band");

        byte[] after = File.ReadAllBytes(assetAbs);
        Assert.True(before.SequenceEqual(after), "a failed save must leave the on-disk file byte-unchanged");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // repack_tre: dry-run no-write / real-run rewrite + backup, on an ISOLATED COPY (SC2)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RepackDryRun_TrueNoWrite_FalseRewritesIsolatedCopy_WithBackupUnderPolicy()
    {
        await using McpClient client = await CreateClientAsync(_tempRoot);

        // Operate on an ISOLATED copy under the root (never the committed fixture in place).
        const string isolatedRel = "repack_copy/sample_v0006.tre";
        string isolatedAbs = Path.Combine(_tempRoot, "repack_copy", "sample_v0006.tre");
        Directory.CreateDirectory(Path.GetDirectoryName(isolatedAbs)!);
        File.Copy(Path.Combine(_tempRoot, TreFixture), isolatedAbs, overwrite: true);

        long sizeBefore = new FileInfo(isolatedAbs).Length;
        byte[] hashBefore = Sha(File.ReadAllBytes(isolatedAbs));
        int recordsBefore = await ReadTreRecordCount(client, isolatedRel);

        // (1) dry_run=true (default semantics, explicit here): NO bytes written, NO backup created.
        CallToolResult dry = await CallAsync(client, "repack_tre", new Dictionary<string, object?>
        {
            ["relativePath"] = isolatedRel,
            ["dry_run"] = true,
        });
        Assert.False(dry.IsError);
        Assert.Equal(sizeBefore, new FileInfo(isolatedAbs).Length);
        Assert.True(hashBefore.SequenceEqual(Sha(File.ReadAllBytes(isolatedAbs))), "dry-run must not modify the archive");
        Assert.Empty(BackupSiblings(isolatedAbs));

        // (2) dry_run=false: the archive is rewritten + a backup lands under the TreBackupPath policy.
        CallToolResult real = await CallAsync(client, "repack_tre", new Dictionary<string, object?>
        {
            ["relativePath"] = isolatedRel,
            ["dry_run"] = false,
        });
        Assert.False(real.IsError, "real repack on a supported non-encrypted v0006 archive must succeed");

        // Re-parse validity + same record count (tolerate nondeterministic repacked bytes).
        int recordsAfter = await ReadTreRecordCount(client, isolatedRel);
        Assert.Equal(recordsBefore, recordsAfter);

        // A backup file exists under the expected backup policy; clean it up.
        string[] backups = BackupSiblings(isolatedAbs);
        Assert.NotEmpty(backups);
        foreach (string b in backups) { try { File.Delete(b); } catch { /* best-effort */ } }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Path-escape at the MCP boundary (complements the 14-03 unit test)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PathEscapeAtBoundary_SaveDatatable_HardError_NoFileOutsideRoot()
    {
        await using McpClient client = await CreateClientAsync(_tempRoot);

        string outsideMarker = Path.GetFullPath(Path.Combine(_tempRoot, "..", "outside.tab"));
        if (File.Exists(outsideMarker)) File.Delete(outsideMarker);

        CallToolResult r = await CallAsync(client, "save_datatable", new Dictionary<string, object?>
        {
            ["relativePath"] = "../../outside.tab",
            ["recordIndex"] = 0,
            ["columnId"] = "anInt",
            ["value"] = "1",
        });

        Assert.True(r.IsError, "a path-escape must be a hard error at the MCP boundary");
        Assert.False(File.Exists(outsideMarker), "no file may be created outside the resolved root");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────

    private async Task<McpClient> CreateClientAsync(string root)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "utinni-mcp-roundtrip",
            Command = _serverExe,
            Arguments = new[] { "--root", root, "--cli-path", _cliExe },
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            return await McpClient.CreateAsync(transport, cancellationToken: cts.Token);
        }
        catch (Exception ex)
        {
            throw new Xunit.Sdk.XunitException(
                "Failed to launch / handshake the built Utinni.Mcp server over stdio.\n" +
                "  server: " + _serverExe + "\n" +
                "  cli:    " + _cliExe + "\n" +
                "  root:   " + root + "\n" +
                ex);
        }
    }

    private static async Task<CallToolResult> CallAsync(McpClient client, string tool, IReadOnlyDictionary<string, object?> args)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        return await client.CallToolAsync(tool, args, cancellationToken: cts.Token);
    }

    /// <summary>decode_iff the datatable and return the int at row 0, column <paramref name="col"/>.</summary>
    private static async Task<int> ReadCellInt(McpClient client, string rel, int col)
    {
        CallToolResult r = await CallAsync(client, "decode_iff",
            new Dictionary<string, object?> { ["relativePath"] = rel });
        Assert.False(r.IsError);
        JsonElement env = StructuredOf(r);
        JsonElement row0 = env.GetProperty("result").GetProperty("rows")[0];
        return row0[col].GetInt32();
    }

    private static async Task<int> ReadTreRecordCount(McpClient client, string rel)
    {
        CallToolResult r = await CallAsync(client, "read_tre",
            new Dictionary<string, object?> { ["relativePath"] = rel });
        Assert.False(r.IsError);
        JsonElement env = StructuredOf(r);
        return env.GetProperty("result").GetProperty("header").GetProperty("recordCount").GetInt32();
    }

    /// <summary>Backup siblings the TreBackupPath policy leaves beside the archive (timestamped *.bak / *.tre.bak*).</summary>
    private static string[] BackupSiblings(string archiveAbs)
    {
        string dir = Path.GetDirectoryName(archiveAbs)!;
        string name = Path.GetFileName(archiveAbs);
        return Directory.GetFiles(dir)
            .Where(f => !string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase))
            .Where(f =>
            {
                string fn = Path.GetFileName(f);
                return fn.IndexOf(".bak", StringComparison.OrdinalIgnoreCase) >= 0
                    || (fn.StartsWith(Path.GetFileNameWithoutExtension(name), StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(fn, name, StringComparison.OrdinalIgnoreCase));
            })
            .ToArray();
    }

    /// <summary>The verbatim CLI envelope the mapper passed through as StructuredContent.</summary>
    private static JsonElement StructuredOf(CallToolResult r)
    {
        Assert.True(r.StructuredContent.HasValue, "the mapper must attach the CLI envelope as StructuredContent");
        return r.StructuredContent.Value;
    }

    private static byte[] Sha(byte[] b)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(b);
    }

    private static void TryDeleteDir(string dir)
    {
        try { if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    /// <summary>
    /// Resolves a built artifact by walking up from the test base dir to the repo root and probing
    /// <c>&lt;projectBin&gt;/&lt;cfg&gt;/&lt;tfm&gt;/&lt;exe&gt;</c>. Prefers the configuration matching the
    /// test build (Release/Debug inferred from the base dir), then falls back to either.
    /// </summary>
    private static string ResolveBuiltArtifact(string projectBinRelative, string tfm, string exeName)
    {
        bool preferRelease = AppContext.BaseDirectory
            .Replace('\\', '/').IndexOf("/release/", StringComparison.OrdinalIgnoreCase) >= 0;
        string[] cfgOrder = preferRelease ? new[] { "Release", "Debug" } : new[] { "Debug", "Release" };

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            foreach (string cfg in cfgOrder)
            {
                string candidate = Path.Combine(dir.FullName, projectBinRelative, cfg, tfm, exeName);
                if (File.Exists(candidate)) return candidate;
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate built " + exeName + " under any " + projectBinRelative +
            "/{Release,Debug}/" + tfm + " walking up from " + AppContext.BaseDirectory +
            ". Build it first (dotnet build for the server; VS2026 MSBuild Release|x86 for utinni-cli).");
    }
}
