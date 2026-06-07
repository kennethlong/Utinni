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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using Utinni.Mcp.Server;
using Utinni.Mcp.Tools;
using Xunit;

namespace Utinni.Mcp.Tests;

/// <summary>
/// 14-03 Task 1 — the write-surface composition tests. Each <c>save_*</c> tool must spawn EXACTLY
/// ONE <c>apply-save-*</c> verb with the typed argv, pass the envelope through OPAQUELY (decide
/// persist-vs-fail on the EXIT CODE — never parse a domain field), and serialize same-path
/// concurrent calls. The stub <see cref="RecordingDispatcher"/> records every spawn so we can prove
/// the spawn count, verb name, and argv WITHOUT a real CLI.
/// </summary>
public class SaveCompositionTests
{
    // ── recording stub dispatcher: overrides RunAsync to record (verb, argv) and return a canned result ──
    private sealed class RecordingDispatcher : CliDispatcher
    {
        public readonly List<(string Verb, IReadOnlyList<string> Args)> Invocations = new();
        private readonly Func<string, CliInvocationResult> _respond;
        private readonly int _delayMs;
        public int Concurrent;
        public int MaxConcurrent;
        private readonly object _gate = new();

        public RecordingDispatcher(Func<string, CliInvocationResult>? respond = null, int delayMs = 0)
            : base(cliExePath: "stub-not-used.exe")
        {
            _respond = respond ?? (_ => Completed0(SuccessEnvelope("apply-save-tab")));
            _delayMs = delayMs;
        }

        public override async Task<CliInvocationResult> RunAsync(string verb, IReadOnlyList<string> args)
        {
            int now = Interlocked.Increment(ref Concurrent);
            lock (_gate) { if (now > MaxConcurrent) MaxConcurrent = now; }
            lock (_gate) { Invocations.Add((verb, args.ToList())); }
            try
            {
                if (_delayMs > 0) await Task.Delay(_delayMs).ConfigureAwait(false);
                return _respond(verb);
            }
            finally
            {
                Interlocked.Decrement(ref Concurrent);
            }
        }
    }

    private static string SuccessEnvelope(string command) =>
        "{\"command\":\"" + command + "\",\"result\":{\"written\":true,\"bytesEqualUntouched\":true},\"schemaVersion\":1}";

    private static string ErrorEnvelope(string command) =>
        "{\"command\":\"" + command + "\",\"error\":{\"kind\":\"VerifyFailed\",\"message\":\"untouched-region byte-identity check failed; nothing written.\"},\"schemaVersion\":1}";

    private static CliInvocationResult Completed0(string stdout) => CliInvocationResult.Completed(0, stdout, string.Empty);
    private static CliInvocationResult Completed2(string stdout) => CliInvocationResult.Completed(2, stdout, string.Empty);

    private static ResolvedRoot MakeRoot(out string rootDir)
    {
        rootDir = Path.Combine(Path.GetTempPath(), "utinni-mcp-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDir);
        return ResolvedRoot.PinOrThrow(new[] { "--root", rootDir });
    }

    private static string GetText(CallToolResult r) =>
        r.Content.OfType<TextContentBlock>().Single().Text;

    // ─────────────────────────────── (a) exactly ONE apply-save-tab with typed argv ───────────────────────────────

    [Fact]
    public async Task SaveDatatable_SpawnsExactlyOne_ApplySaveTab_WithTypedArgv()
    {
        var cli = new RecordingDispatcher(_ => Completed0(SuccessEnvelope("apply-save-tab")));
        ResolvedRoot root = MakeRoot(out string rootDir);

        CallToolResult result = await SaveTools.SaveDatatable(root, cli, "tables/foo.tab", recordIndex: 3, columnId: "5", value: "hello");

        Assert.Single(cli.Invocations);
        (string verb, IReadOnlyList<string> args) = cli.Invocations[0];
        Assert.Equal("apply-save-tab", verb);
        // typed argv: <rel> --root <root> --mutate-cell 3,5 --mutate-value hello
        Assert.Equal("tables/foo.tab", args[0]);
        Assert.Contains("--root", args);
        Assert.Equal(rootDir, args[args.ToList().IndexOf("--root") + 1]);
        Assert.Contains("--mutate-cell", args);
        Assert.Equal("3,5", args[args.ToList().IndexOf("--mutate-cell") + 1]);
        Assert.Contains("--mutate-value", args);
        Assert.Equal("hello", args[args.ToList().IndexOf("--mutate-value") + 1]);
        Assert.False(result.IsError);
    }

    [Fact]
    public async Task SaveDatatable_RemoveRow_BuildsRemoveRowArgv()
    {
        var cli = new RecordingDispatcher();
        ResolvedRoot root = MakeRoot(out _);

        await SaveTools.SaveDatatable(root, cli, "tables/foo.tab", removeRow: 2);

        (string verb, IReadOnlyList<string> args) = Assert.Single(cli.Invocations);
        Assert.Equal("apply-save-tab", verb);
        Assert.Contains("--remove-row", args);
        Assert.Equal("2", args[args.ToList().IndexOf("--remove-row") + 1]);
        Assert.DoesNotContain("--mutate-cell", args);
    }

    [Fact]
    public async Task SaveDatatable_RemoveColumn_BuildsRemoveColumnArgv()
    {
        var cli = new RecordingDispatcher();
        ResolvedRoot root = MakeRoot(out _);

        await SaveTools.SaveDatatable(root, cli, "tables/foo.tab", removeColumn: "name");

        (string verb, IReadOnlyList<string> args) = Assert.Single(cli.Invocations);
        Assert.Equal("apply-save-tab", verb);
        Assert.Contains("--remove-column", args);
        Assert.Equal("name", args[args.ToList().IndexOf("--remove-column") + 1]);
    }

    // ─────────────────────────────── (b) exit 2 surfaces in-band, envelope opaque ───────────────────────────────

    [Fact]
    public async Task SaveDatatable_VerbExit2_SurfacesInBandIsError_EnvelopePassedThroughUnchanged()
    {
        string env = ErrorEnvelope("apply-save-tab");
        var cli = new RecordingDispatcher(_ => Completed2(env));
        ResolvedRoot root = MakeRoot(out _);

        CallToolResult result = await SaveTools.SaveDatatable(root, cli, "tables/foo.tab", recordIndex: 0, columnId: "0", value: "x");

        // exit 2 => in-band isError=true (agent self-corrects); the host did NOT parse any field —
        // the raw envelope is mirrored verbatim in the text content.
        Assert.True(result.IsError);
        Assert.Equal(env, GetText(result));
        Assert.Single(cli.Invocations);
    }

    // ─────────────────────────────── (c) save_iff / stf / ot map to the right verbs ───────────────────────────────

    [Fact]
    public async Task SaveIff_MapsTo_ApplySaveIff()
    {
        var cli = new RecordingDispatcher(_ => Completed0(SuccessEnvelope("apply-save-iff")));
        ResolvedRoot root = MakeRoot(out _);

        await SaveTools.SaveIff(root, cli, "obj/a.iff", leafId: "0001", payloadHex: "deadbeef");

        (string verb, IReadOnlyList<string> args) = Assert.Single(cli.Invocations);
        Assert.Equal("apply-save-iff", verb);
        Assert.Contains("--mutate-leaf", args);
        Assert.Equal("0001", args[args.ToList().IndexOf("--mutate-leaf") + 1]);
        Assert.Contains("--mutate-hex", args);
        Assert.Equal("deadbeef", args[args.ToList().IndexOf("--mutate-hex") + 1]);
    }

    [Fact]
    public async Task SaveStringtable_MapsTo_ApplySaveStf_WithKeyEqualsValue()
    {
        var cli = new RecordingDispatcher(_ => Completed0(SuccessEnvelope("apply-save-stf")));
        ResolvedRoot root = MakeRoot(out _);

        await SaveTools.SaveStringtable(root, cli, "string/en/foo.stf", key: "greeting", value: "hello world");

        (string verb, IReadOnlyList<string> args) = Assert.Single(cli.Invocations);
        Assert.Equal("apply-save-stf", verb);
        Assert.Contains("--edit-text", args);
        Assert.Equal("greeting=hello world", args[args.ToList().IndexOf("--edit-text") + 1]);
    }

    [Fact]
    public async Task SaveObjectTemplate_Add_MapsTo_ApplySaveOt_AddOverride()
    {
        var cli = new RecordingDispatcher(_ => Completed0(SuccessEnvelope("apply-save-ot")));
        ResolvedRoot root = MakeRoot(out _);

        await SaveTools.SaveObjectTemplate(root, cli, "obj/tmpl.iff", field: "maxHitPoints", operation: "add", valueInt: 500);

        (string verb, IReadOnlyList<string> args) = Assert.Single(cli.Invocations);
        Assert.Equal("apply-save-ot", verb);
        Assert.Contains("--add-override", args);
        Assert.Equal("maxHitPoints", args[args.ToList().IndexOf("--add-override") + 1]);
        Assert.Contains("--value-int", args);
        Assert.Equal("500", args[args.ToList().IndexOf("--value-int") + 1]);
    }

    [Fact]
    public async Task SaveObjectTemplate_Remove_MapsTo_RemoveOverride_NoValueInt()
    {
        var cli = new RecordingDispatcher(_ => Completed0(SuccessEnvelope("apply-save-ot")));
        ResolvedRoot root = MakeRoot(out _);

        await SaveTools.SaveObjectTemplate(root, cli, "obj/tmpl.iff", field: "maxHitPoints", operation: "remove");

        (string verb, IReadOnlyList<string> args) = Assert.Single(cli.Invocations);
        Assert.Equal("apply-save-ot", verb);
        Assert.Contains("--remove-override", args);
        Assert.DoesNotContain("--add-override", args);
    }

    // ─────────────────────────────── roundtrip_check is non-persisting (roundtrip-* verb) ───────────────────────────────

    [Fact]
    public async Task RoundtripCheck_RunsRoundtripVerb_NotApplySave()
    {
        var cli = new RecordingDispatcher(_ => Completed0(
            "{\"command\":\"roundtrip-tab\",\"result\":{\"bytesEqualUntouched\":true},\"schemaVersion\":1}"));
        ResolvedRoot root = MakeRoot(out string rootDir);

        await VerifyTools.RoundtripCheck(root, cli, "tables/foo.tab", format: "datatable", recordIndex: 1, columnId: "0", value: "z");

        (string verb, IReadOnlyList<string> args) = Assert.Single(cli.Invocations);
        Assert.Equal("roundtrip-tab", verb);
        // roundtrip-* takes a positional ABSOLUTE path (no --root); proves the verb name is the
        // verify-only sibling, not apply-save-*.
        Assert.StartsWith(rootDir, args[0]);
        Assert.DoesNotContain("--root", args);
        Assert.Contains("--mutate-cell", args);
    }

    // ─────────────────────────────── (d) per-resolved-path serialization ───────────────────────────────

    [Fact]
    public async Task ConcurrentSavesOnSamePath_AreSerialized()
    {
        var cli = new RecordingDispatcher(_ => Completed0(SuccessEnvelope("apply-save-tab")), delayMs: 60);
        ResolvedRoot root = MakeRoot(out _);

        Task<CallToolResult> a = SaveTools.SaveDatatable(root, cli, "tables/same.tab", recordIndex: 0, columnId: "0", value: "a");
        Task<CallToolResult> b = SaveTools.SaveDatatable(root, cli, "tables/same.tab", recordIndex: 0, columnId: "0", value: "b");
        await Task.WhenAll(a, b);

        Assert.Equal(2, cli.Invocations.Count);
        // Serialized: never two in flight at once on the same resolved path.
        Assert.Equal(1, cli.MaxConcurrent);
    }

    [Fact]
    public async Task ConcurrentSavesOnDifferentPaths_RunInParallel()
    {
        var cli = new RecordingDispatcher(_ => Completed0(SuccessEnvelope("apply-save-tab")), delayMs: 80);
        ResolvedRoot root = MakeRoot(out _);

        Task<CallToolResult> a = SaveTools.SaveDatatable(root, cli, "tables/one.tab", recordIndex: 0, columnId: "0", value: "a");
        Task<CallToolResult> b = SaveTools.SaveDatatable(root, cli, "tables/two.tab", recordIndex: 0, columnId: "0", value: "b");
        await Task.WhenAll(a, b);

        Assert.Equal(2, cli.Invocations.Count);
        // Different paths are NOT serialized => both can be in flight at once.
        Assert.Equal(2, cli.MaxConcurrent);
    }
}
