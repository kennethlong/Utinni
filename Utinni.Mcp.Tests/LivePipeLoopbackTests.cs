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
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utinni.Mcp.Server;
using Xunit;

namespace Utinni.Mcp.Tests;

/// <summary>
/// Task 3 (A): the net10 loopback protocol facts + GOLDEN-WIRE byte-equality + name/framing
/// fixture-equality. <c>--filter LivePipe</c> selects this partial of
/// <see cref="LivePipeProtocolTests"/> alongside the Task-0 gating fact.
///
/// <para>The loopback facts stand up a real <see cref="NamedPipeServerStream"/> test-stub on a
/// randomized pipe name, point a REAL <see cref="LivePipeClient"/> at it, and assert the wire
/// round-trip + the never-hang edge cases. The byte-equality facts assert the net10
/// <see cref="CanonicalJson"/> produces the request goldens + consumes the ack goldens BYTE-EXACT
/// against the SAME committed <c>Utinni.Cli.Tests/Fixtures/live/wire-*.json</c> files the net472
/// server side asserts — closing the cross-TFM wire gap (R3-1/R3-7). The D-04 real-enumeration gating
/// assertion already landed in Task 0; it is not duplicated here.</para>
/// </summary>
public sealed partial class LivePipeProtocolTests
{
    // ── Reload golden field values (the wire-reload-*.json fixtures) ──
    private const string ReloadGoldenPath = "appearance/mesh/foo.msh";
    private const string ReloadGoldenExt = ".msh";
    private const string ReloadGoldenTier = "PendingNextSceneChange";
    private const string ReloadGoldenNote = "transport-only until RESID-03; queued != visible reload";

    // ── Ping golden field values (the wire-ping-ack.json fixture) ──
    private const string PingGoldenClientRoot = "C:/swg/client";

    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(750);

    // ─────────────────────────────────────────────────────────────────────────────
    // GOLDEN-WIRE byte-equality (R3-1 / R3-7): all four, produce + consume, no
    // key-order-independent fallback. The net10 CanonicalJson must emit/parse the
    // SAME committed bytes the net472 side asserts.
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GoldenWire_PingRequest_ProducedByteExact()
    {
        byte[] produced = LivePipeClient.SerializePingRequest(LivePipeClient.ProtocolVersion);
        byte[] golden = ReadFixtureBytes("wire-ping-request.json");
        Assert.Equal(golden, produced);
    }

    [Fact]
    public void GoldenWire_ReloadRequest_ProducedByteExact()
    {
        byte[] produced = LivePipeClient.SerializeReloadRequest(
            LivePipeClient.ProtocolVersion, ReloadGoldenPath, ReloadGoldenExt);
        byte[] golden = ReadFixtureBytes("wire-reload-request.json");
        Assert.Equal(golden, produced);
    }

    [Fact]
    public void GoldenWire_PingAck_ConsumedExact()
    {
        byte[] golden = ReadFixtureBytes("wire-ping-ack.json");
        var env = CanonicalJson.Deserialize(golden);

        Assert.Equal(1, (int)env["schemaVersion"].NumberValue);
        Assert.Equal(LivePipeClient.ProtocolVersion, (int)env["protocolVersion"].NumberValue);
        Assert.Equal("ping", env["op"].StringValue);

        var result = env["result"].Object!;
        Assert.True(result["listening"].BoolValue);
        Assert.True(result["gameRunning"].BoolValue);
        Assert.Equal(0, (int)result["pid"].NumberValue);
        Assert.Equal(PingGoldenClientRoot, result["clientRoot"].StringValue); // R3-3 clientRoot present
    }

    [Fact]
    public void GoldenWire_ReloadAck_ConsumedExact()
    {
        byte[] golden = ReadFixtureBytes("wire-reload-ack.json");
        var env = CanonicalJson.Deserialize(golden);

        Assert.Equal(1, (int)env["schemaVersion"].NumberValue);
        Assert.Equal(LivePipeClient.ProtocolVersion, (int)env["protocolVersion"].NumberValue);
        Assert.Equal("reload-asset", env["op"].StringValue);

        var result = env["result"].Object!;
        Assert.True(result["accepted"].BoolValue);
        Assert.Equal(ReloadGoldenTier, result["tier"].StringValue);  // string-encoded enum name (CUR-NEW-9)
        Assert.False(result["queued"].BoolValue);
        Assert.False(result["reloadAttempted"].BoolValue);           // C-14 candor
        Assert.Equal(ReloadGoldenPath, result["path"].StringValue);
        Assert.Equal(ReloadGoldenNote, result["note"].StringValue);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Pipe-name + framing fixture-equality (C-05): the net10 duplicated literals must
    // byte-equal fixture line 1 (PipeName) and line 2 (FramingDescriptor).
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FixtureEquality_PipeNameAndFraming_ByteEqualTheCanonicalAnchor()
    {
        string text = ReadFixtureText("pipe-name.txt").Replace("\r\n", "\n");
        string[] lines = text.Split('\n');

        Assert.True(lines.Length >= 2, "pipe-name.txt must have a name line + a framing line.");
        Assert.Equal(LivePipeClient.PipeName, lines[0]);            // line 1 == PipeName
        Assert.Equal(LivePipeClient.FramingDescriptor, lines[1]);   // line 2 == FramingDescriptor
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Loopback round-trips against a real NamedPipeServerStream test-stub.
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Loopback_Ping_RoundTripsAndMapsClientRoot()
    {
        string pipeName = RandomPipeName();
        using var server = new StubServer(pipeName, StubReply.PingListening);
        Task serve = server.ServeOneAsync();

        var client = new LivePipeClient(pipeName, ShortTimeout);
        LivePingResult result = await client.PingAsync();
        await serve;

        Assert.Null(result.Error);
        Assert.True(result.Listening);
        Assert.True(result.GameRunning);
        Assert.Equal(4321, result.Pid);
        Assert.Equal("D:/swg/live", result.ClientRoot); // R3-3 clientRoot diagnostic mapped through
    }

    [Fact]
    public async Task Loopback_ReloadAsset_SendsRelativeEnvelope_AndMapsAck()
    {
        string pipeName = RandomPipeName();
        using var server = new StubServer(pipeName, StubReply.ReloadAck);
        Task serve = server.ServeOneAsync();

        var client = new LivePipeClient(pipeName, ShortTimeout);
        LiveReloadResult result = await client.ReloadAssetAsync(ReloadGoldenPath, ReloadGoldenExt);
        await serve;

        // The client SENT a well-formed reload envelope: op=reload-asset + protocolVersion + the
        // RELATIVE path + ext (CUR-NEW-1).
        var sent = CanonicalJson.Deserialize(server.LastRequestBody!);
        Assert.Equal(LivePipeClient.ProtocolVersion, (int)sent["protocolVersion"].NumberValue);
        Assert.Equal("reload-asset", sent["op"].StringValue);
        Assert.Equal(ReloadGoldenPath, sent["path"].StringValue); // RELATIVE — not an absolute path
        Assert.Equal(ReloadGoldenExt, sent["ext"].StringValue);

        // The ack maps through (tier string + reloadAttempted=false + note candor).
        Assert.Null(result.Error);
        Assert.True(result.Accepted);
        Assert.Equal(ReloadGoldenTier, result.Tier);
        Assert.True(result.Queued);
        Assert.False(result.ReloadAttempted);
        Assert.Equal(ReloadGoldenPath, result.Path);
        Assert.Equal(ReloadGoldenNote, result.Note);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Never-hang edge cases (Pitfall 3 / framing). Each completes within the timeout.
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoClient_Ping_ReturnsListeningFalseQuickly_NeverHangs()
    {
        // A pipe name no server owns: connect must fail fast and map to listening:false.
        string pipeName = RandomPipeName();
        var client = new LivePipeClient(pipeName, ShortTimeout);

        var sw = Stopwatch.StartNew();
        LivePingResult result = await client.PingAsync();
        sw.Stop();

        Assert.False(result.Listening);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"No-client ping took {sw.Elapsed.TotalSeconds:F1}s — must return well within the timeout.");
    }

    [Fact]
    public async Task ServerClosesMidMessage_Ping_HardErrorNoHang()
    {
        string pipeName = RandomPipeName();
        using var server = new StubServer(pipeName, StubReply.ConnectThenClose);
        Task serve = server.ServeOneAsync();

        var client = new LivePipeClient(pipeName, ShortTimeout);
        var sw = Stopwatch.StartNew();
        LivePingResult result = await client.PingAsync();
        sw.Stop();
        await serve;

        // Server accepted then closed without a full ack → listening:false (transport error), no hang.
        Assert.False(result.Listening);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task OversizeAckFrame_Ping_HardErrorNoHang()
    {
        string pipeName = RandomPipeName();
        using var server = new StubServer(pipeName, StubReply.OversizeFrame);
        Task serve = server.ServeOneAsync();

        var client = new LivePipeClient(pipeName, ShortTimeout);
        var sw = Stopwatch.StartNew();
        LivePingResult result = await client.PingAsync();
        sw.Stop();
        await serve;

        // The declared ack frame length exceeds MaxFrameBytes → rejected before reading the body → no hang.
        Assert.False(result.Listening);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PartialAckFrame_Ping_HardErrorNoHang()
    {
        string pipeName = RandomPipeName();
        using var server = new StubServer(pipeName, StubReply.PartialFrame);
        Task serve = server.ServeOneAsync();

        var client = new LivePipeClient(pipeName, ShortTimeout);
        var sw = Stopwatch.StartNew();
        LivePingResult result = await client.PingAsync();
        sw.Stop();
        await serve;

        Assert.False(result.Listening);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ProtocolVersionSkew_Ping_SurfacesStructuredError()
    {
        string pipeName = RandomPipeName();
        using var server = new StubServer(pipeName, StubReply.SkewedPingAck);
        Task serve = server.ServeOneAsync();

        var client = new LivePipeClient(pipeName, ShortTimeout);
        LivePingResult result = await client.PingAsync();
        await serve;

        // A protocolVersion=999 ack is NOT silently parsed — it surfaces a structured error (CUR-NEW-5).
        Assert.NotNull(result.Error);
        Assert.Contains("protocolVersion skew", result.Error);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Fixture I/O helpers
    // ─────────────────────────────────────────────────────────────────────────────

    private static byte[] ReadFixtureBytes(string name)
    {
        return File.ReadAllBytes(FixturePath(name));
    }

    private static string ReadFixtureText(string name)
    {
        return File.ReadAllText(FixturePath(name));
    }

    private static string FixturePath(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", "live", name);
    }

    private static string RandomPipeName() => "utinni-live-test-" + Guid.NewGuid().ToString("N");

    // ─────────────────────────────────────────────────────────────────────────────
    // The NamedPipeServerStream test-stub. Reads one framed request and replies per
    // the configured StubReply mode, mirroring the LiveBridgeProtocol framing.
    // ─────────────────────────────────────────────────────────────────────────────

    private enum StubReply
    {
        PingListening,
        ReloadAck,
        ConnectThenClose,
        OversizeFrame,
        PartialFrame,
        SkewedPingAck,
    }

    private sealed class StubServer : IDisposable
    {
        private readonly NamedPipeServerStream _pipe;
        private readonly StubReply _mode;

        public byte[]? LastRequestBody { get; private set; }

        public StubServer(string pipeName, StubReply mode)
        {
            _mode = mode;
            _pipe = new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        }

        public async Task ServeOneAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await _pipe.WaitForConnectionAsync(cts.Token).ConfigureAwait(false);

                // Read the request frame (4-byte LE prefix + body).
                byte[]? prefix = await ReadExactly(_pipe, 4, cts.Token).ConfigureAwait(false);
                if (prefix == null) return;
                int len = prefix[0] | (prefix[1] << 8) | (prefix[2] << 16) | (prefix[3] << 24);
                LastRequestBody = await ReadExactly(_pipe, len, cts.Token).ConfigureAwait(false);

                switch (_mode)
                {
                    case StubReply.ConnectThenClose:
                        // Reply nothing — close the connection (the client sees a clean EOF / break).
                        return;

                    case StubReply.OversizeFrame:
                        // Declare a frame length above MaxFrameBytes; write only the prefix.
                        await WriteRawPrefix(_pipe, LivePipeClient.MaxFrameBytes + 1, cts.Token).ConfigureAwait(false);
                        return;

                    case StubReply.PartialFrame:
                        // Declare 100 bytes but write only 10 → the client's ReadExactly throws EndOfStream.
                        await WriteRawPrefix(_pipe, 100, cts.Token).ConfigureAwait(false);
                        await _pipe.WriteAsync(new byte[10], 0, 10, cts.Token).ConfigureAwait(false);
                        return;

                    case StubReply.SkewedPingAck:
                        await WriteFrame(_pipe, BuildPingAck(protocolVersion: 999), cts.Token).ConfigureAwait(false);
                        return;

                    case StubReply.PingListening:
                        await WriteFrame(_pipe, BuildPingAck(protocolVersion: LivePipeClient.ProtocolVersion), cts.Token).ConfigureAwait(false);
                        return;

                    case StubReply.ReloadAck:
                        await WriteFrame(_pipe, BuildReloadAck(), cts.Token).ConfigureAwait(false);
                        return;
                }
            }
            catch (OperationCanceledException) { /* timed out waiting — test will fail on the assert */ }
            catch (IOException) { /* client closed first — fine for the close/edge tests */ }
        }

        private static byte[] BuildPingAck(int protocolVersion)
        {
            // Mirror LiveBridgeProtocol.SerializePingAck via the net10 CanonicalJson writer.
            var inner = new CanonicalJson.JsonObjectWriter()
                .AddBool("listening", true)
                .AddBool("gameRunning", true)
                .AddInt("pid", 4321)
                .AddString("clientRoot", "D:/swg/live");
            var w = new CanonicalJson.JsonObjectWriter()
                .AddInt("schemaVersion", 1)
                .AddInt("protocolVersion", protocolVersion)
                .AddString("op", "ping")
                .AddObject("result", inner);
            return CanonicalJson.Serialize(w);
        }

        private static byte[] BuildReloadAck()
        {
            var inner = new CanonicalJson.JsonObjectWriter()
                .AddBool("accepted", true)
                .AddString("tier", ReloadGoldenTier)
                .AddBool("queued", true)
                .AddBool("reloadAttempted", false)
                .AddString("path", ReloadGoldenPath)
                .AddString("note", ReloadGoldenNote);
            var w = new CanonicalJson.JsonObjectWriter()
                .AddInt("schemaVersion", 1)
                .AddInt("protocolVersion", LivePipeClient.ProtocolVersion)
                .AddString("op", "reload-asset")
                .AddObject("result", inner);
            return CanonicalJson.Serialize(w);
        }

        private static async Task WriteFrame(Stream s, byte[] body, CancellationToken ct)
        {
            await WriteRawPrefix(s, body.Length, ct).ConfigureAwait(false);
            await s.WriteAsync(body, 0, body.Length, ct).ConfigureAwait(false);
            await s.FlushAsync(ct).ConfigureAwait(false);
        }

        private static async Task WriteRawPrefix(Stream s, int len, CancellationToken ct)
        {
            byte[] prefix =
            {
                (byte)(len & 0xFF),
                (byte)((len >> 8) & 0xFF),
                (byte)((len >> 16) & 0xFF),
                (byte)((len >> 24) & 0xFF),
            };
            await s.WriteAsync(prefix, 0, 4, ct).ConfigureAwait(false);
            await s.FlushAsync(ct).ConfigureAwait(false);
        }

        private static async Task<byte[]?> ReadExactly(Stream s, int count, CancellationToken ct)
        {
            byte[] buf = new byte[count];
            int total = 0;
            while (total < count)
            {
                int n = await s.ReadAsync(buf.AsMemory(total, count - total), ct).ConfigureAwait(false);
                if (n <= 0) return total == 0 ? null : buf;
                total += n;
            }
            return buf;
        }

        public void Dispose()
        {
            try { _pipe.Dispose(); } catch { /* best effort */ }
        }
    }
}
