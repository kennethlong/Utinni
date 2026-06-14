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
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Utinni.Mcp.Server;

/// <summary>
/// The named-pipe CLIENT half of the MCP-03 live bridge (16-03 Task 1) — the structural twin of
/// <see cref="CliDispatcher"/>. Where the dispatcher spawns the net472 <c>utinni-cli.exe</c>, this
/// connects to the in-client (injected) <c>LivePipeServer</c> (16-02) over a loopback named pipe and
/// round-trips a single framed request/ack.
///
/// <para><b>Never-hang discipline (mirrors CliDispatcher; RESEARCH Pitfall 3):</b> a SHORT default
/// connect/read timeout (much smaller than CliDispatcher's 60s — a no-client ping must return fast),
/// the whole connect+send+read wrapped in a <see cref="CancellationTokenSource"/>. On a connect
/// failure (no server listening), a timeout, or a server-close mid-message, the methods return a
/// RESULT OBJECT (<see cref="LivePingResult.NotListening"/> / a hard-error <see cref="LiveReloadResult"/>)
/// — they NEVER throw and NEVER hang.</para>
///
/// <para><b>Wire contract (R3-1):</b> requests are serialized via the net10 <see cref="CanonicalJson"/>
/// re-implementation so the bytes are byte-identical to the committed
/// <c>Utinni.Cli.Tests/Fixtures/live/wire-*-request.json</c> goldens the net472 server asserts; acks
/// are parsed via <see cref="CanonicalJson.Deserialize"/>. The request envelope carries
/// <c>protocolVersion</c> (C-12); the reload request's <c>path</c> is the RELATIVE asset path
/// (CUR-NEW-1). The ping ack surfaces the in-client <c>clientRoot</c> diagnostic (R3-3).</para>
///
/// <para><b>protocolVersion skew (CUR-NEW-5):</b> if a received ack's <c>protocolVersion</c> differs
/// from <see cref="ProtocolVersion"/>, the result surfaces a structured error (it is NOT silently
/// parsed / its fields ignored).</para>
///
/// <para><b>Duplicated canonical constants (TFM wall):</b> net10 <c>Utinni.Mcp</c> cannot
/// project-reference net472 <c>UtinniCoreDotNet.Live.LiveBridgeProtocol</c>, so <see cref="PipeName"/>,
/// <see cref="ProtocolVersion"/>, <see cref="MaxFrameBytes"/>, and <see cref="FramingDescriptor"/> are
/// DUPLICATED here byte-identical to the 16-02 canonical values. The documented source-of-truth is
/// <c>UtinniCoreDotNet/Live/LiveBridgeProtocol.cs</c> + <c>CanonicalJson.cs</c>; the agreement anchors
/// the Task-3 tests assert against are <c>Utinni.Cli.Tests/Fixtures/live/{pipe-name.txt, wire-*.json}</c>
/// (line 1 = PipeName, line 2 = FramingDescriptor, the four wire-*.json = the golden byte-vectors).</para>
/// </summary>
public class LivePipeClient
{
    // ── Duplicated canonical constants (byte-identical to 16-02 LiveBridgeProtocol) ──

    /// <summary>Byte-identical to <c>Fixtures/live/pipe-name.txt</c> line 1 (16-02 PipeName).</summary>
    public const string PipeName = "utinni-live-bridge";

    /// <summary>Byte-identical to the 16-02 protocol version (pipe-name.txt line 2 token).</summary>
    public const int ProtocolVersion = 1;

    /// <summary>Byte-identical to the 16-02 max frame size (pipe-name.txt line 2 token).</summary>
    public const int MaxFrameBytes = 65536;

    /// <summary>Byte-identical to <c>Fixtures/live/pipe-name.txt</c> line 2 (16-02 FramingDescriptor).</summary>
    public const string FramingDescriptor =
        "frame=len32le+utf8json;maxframe=65536;protocol=1;json=camelCase;tier=stringnames";

    // Op tokens (kept tiny per D-02): only ping + reload-asset.
    private const string OpPing = "ping";
    private const string OpReloadAsset = "reload-asset";

    /// <summary>Short default connect/read timeout — a no-client ping must return fast (Pitfall 3).</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    private readonly string _pipeName;
    private readonly TimeSpan _timeout;

    /// <summary>Constructs a client with the short default timeout.</summary>
    public LivePipeClient(string pipeName) : this(pipeName, DefaultTimeout)
    {
    }

    /// <summary>Constructs a client with an INJECTABLE timeout (tests use a sub-second value).</summary>
    public LivePipeClient(string pipeName, TimeSpan timeout)
    {
        _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
        _timeout = timeout;
    }

    /// <summary>The pipe name this client connects to.</summary>
    public string TargetPipeName => _pipeName;

    // ─────────────────────────────────────────────────────────────────────
    // Public API: PingAsync + ReloadAssetAsync. Neither throws on transport
    // failure; both return a result object the tools map.
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a ping request and returns the parsed result. On a no-client / timeout / transport error
    /// returns <see cref="LivePingResult.NotListening"/> (listening=false) — never throws, never hangs.
    /// </summary>
    public async Task<LivePingResult> PingAsync()
    {
        byte[] request = SerializePingRequest(ProtocolVersion);
        byte[]? ack = await ExchangeAsync(request).ConfigureAwait(false);
        if (ack == null)
        {
            return LivePingResult.NotListening();
        }

        try
        {
            Dictionary<string, CanonicalJson.JsonValue> env = CanonicalJson.Deserialize(ack);

            // protocolVersion skew (CUR-NEW-5): surface a structured error, never a silent parse.
            if (!ProtocolVersionMatches(env))
            {
                return LivePingResult.ProtocolSkew(ReadInt(env, "protocolVersion"));
            }

            CanonicalJson.JsonValue? result = GetObject(env, "result");
            if (result?.Object == null)
            {
                return LivePingResult.MalformedAck("ping ack missing result object");
            }

            Dictionary<string, CanonicalJson.JsonValue> r = result.Object;
            return new LivePingResult
            {
                Listening = ReadBool(r, "listening"),
                GameRunning = ReadBool(r, "gameRunning"),
                Pid = ReadInt(r, "pid"),
                ClientRoot = ReadString(r, "clientRoot"),
                Error = null,
            };
        }
        catch (FormatException ex)
        {
            return LivePingResult.MalformedAck("ping ack parse error: " + ex.Message);
        }
    }

    /// <summary>
    /// Sends a reload-asset request carrying the RELATIVE path (CUR-NEW-1) and returns the parsed ack.
    /// On a no-client / timeout / transport error returns a hard-error result — never throws/hangs.
    /// </summary>
    public async Task<LiveReloadResult> ReloadAssetAsync(string relativePath, string ext)
    {
        byte[] request = SerializeReloadRequest(ProtocolVersion, relativePath, ext);
        byte[]? ack = await ExchangeAsync(request).ConfigureAwait(false);
        if (ack == null)
        {
            return LiveReloadResult.NoClient(relativePath);
        }

        try
        {
            Dictionary<string, CanonicalJson.JsonValue> env = CanonicalJson.Deserialize(ack);

            // protocolVersion skew (CUR-NEW-5): structured error.
            if (!ProtocolVersionMatches(env))
            {
                return LiveReloadResult.ProtocolSkew(relativePath, ReadInt(env, "protocolVersion"));
            }

            CanonicalJson.JsonValue? result = GetObject(env, "result");
            if (result?.Object == null)
            {
                return LiveReloadResult.Malformed(relativePath, "reload ack missing result object");
            }

            Dictionary<string, CanonicalJson.JsonValue> r = result.Object;
            return new LiveReloadResult
            {
                Accepted = ReadBool(r, "accepted"),
                Tier = ReadString(r, "tier"),     // string-encoded enum name (CUR-NEW-9)
                Queued = ReadBool(r, "queued"),
                ReloadAttempted = ReadBool(r, "reloadAttempted"),
                Path = ReadString(r, "path"),
                Note = ReadString(r, "note"),
                Error = null,
            };
        }
        catch (FormatException ex)
        {
            return LiveReloadResult.Malformed(relativePath, "reload ack parse error: " + ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Transport: connect + write one frame + read one frame, all bounded.
    // VIRTUAL so the loopback test can substitute a recording/stub exchange.
    // Returns the ack BODY bytes, or null on no-client / timeout / transport error.
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Connects to the pipe, writes the framed <paramref name="requestBody"/>, reads one framed ack,
    /// and returns the ack body bytes. Returns null on a no-server / timeout / server-close / oversize
    /// path — NEVER throws, NEVER hangs (bounded by <see cref="_timeout"/>). Virtual for test
    /// substitution.
    /// </summary>
    protected virtual async Task<byte[]?> ExchangeAsync(byte[] requestBody)
    {
        using var cts = new CancellationTokenSource(_timeout);
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            // Bounded connect. ConnectAsync(token) throws OperationCanceledException on timeout and
            // a no-server connect never blocks past the token.
            await pipe.ConnectAsync(cts.Token).ConfigureAwait(false);

            await WriteFrameAsync(pipe, requestBody, cts.Token).ConfigureAwait(false);
            try { await pipe.FlushAsync(cts.Token).ConfigureAwait(false); } catch { /* best effort */ }

            byte[]? ack = await ReadFrameAsync(pipe, cts.Token).ConfigureAwait(false);
            return ack;
        }
        catch (OperationCanceledException)
        {
            return null; // timeout / cancel → no-client / hard-error mapping upstream
        }
        catch (TimeoutException)
        {
            return null; // connect timeout
        }
        catch (InvalidDataException)
        {
            return null; // oversize ack frame rejected (IOException subtype — must precede IOException)
        }
        catch (EndOfStreamException)
        {
            return null; // partial/truncated frame (IOException subtype — must precede IOException)
        }
        catch (IOException)
        {
            return null; // server-close mid-message / broken pipe
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // OUTPUT serialization — EXCLUSIVELY via the net10 CanonicalJson (R3-1).
    // Field order below IS the wire contract (mirrors 16-02 LiveBridgeProtocol).
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Serializes a ping REQUEST: <c>{"protocolVersion":N,"op":"ping"}</c>.</summary>
    public static byte[] SerializePingRequest(int protocolVersion)
    {
        var w = new CanonicalJson.JsonObjectWriter()
            .AddInt("protocolVersion", protocolVersion)
            .AddString("op", OpPing);
        return CanonicalJson.Serialize(w);
    }

    /// <summary>Serializes a reload-asset REQUEST:
    /// <c>{"protocolVersion":N,"op":"reload-asset","path":"...","ext":"..."}</c> (path RELATIVE).</summary>
    public static byte[] SerializeReloadRequest(int protocolVersion, string relativePath, string ext)
    {
        var w = new CanonicalJson.JsonObjectWriter()
            .AddInt("protocolVersion", protocolVersion)
            .AddString("op", OpReloadAsset)
            .AddString("path", relativePath)
            .AddString("ext", ext);
        return CanonicalJson.Serialize(w);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Framing: 4-byte little-endian length prefix + UTF-8 JSON body (mirrors
    // 16-02 LiveBridgeProtocol.WriteFrame/ReadFrame; goldens hold BODY only — R3-6).
    // ─────────────────────────────────────────────────────────────────────

    private static async Task WriteFrameAsync(Stream stream, byte[] body, CancellationToken ct)
    {
        if (body.Length > MaxFrameBytes)
        {
            throw new InvalidDataException(
                "Frame body (" + body.Length + " bytes) exceeds MaxFrameBytes (" + MaxFrameBytes + ").");
        }
        byte[] prefix = new byte[4];
        int len = body.Length;
        prefix[0] = (byte)(len & 0xFF);
        prefix[1] = (byte)((len >> 8) & 0xFF);
        prefix[2] = (byte)((len >> 16) & 0xFF);
        prefix[3] = (byte)((len >> 24) & 0xFF);
        await stream.WriteAsync(prefix, 0, 4, ct).ConfigureAwait(false);
        await stream.WriteAsync(body, 0, body.Length, ct).ConfigureAwait(false);
    }

    private static async Task<byte[]?> ReadFrameAsync(Stream stream, CancellationToken ct)
    {
        byte[]? prefix = await ReadExactlyAsync(stream, 4, ct, allowZeroEof: true).ConfigureAwait(false);
        if (prefix == null)
        {
            return null; // clean EOF before any frame (server closed without replying)
        }
        long len = (uint)(prefix[0] | (prefix[1] << 8) | (prefix[2] << 16) | (prefix[3] << 24));
        if (len < 0 || len > MaxFrameBytes)
        {
            throw new InvalidDataException(
                "Declared ack frame length " + len + " exceeds MaxFrameBytes (" + MaxFrameBytes + ").");
        }
        byte[]? body = await ReadExactlyAsync(stream, (int)len, ct, allowZeroEof: false).ConfigureAwait(false);
        return body;
    }

    // Reads exactly count bytes under a cancellation token. Returns null on a clean EOF before any
    // byte when allowZeroEof is true; throws EndOfStreamException on a partial read.
    private static async Task<byte[]?> ReadExactlyAsync(Stream stream, int count, CancellationToken ct, bool allowZeroEof)
    {
        byte[] buffer = new byte[count];
        int total = 0;
        while (total < count)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(total, count - total), ct).ConfigureAwait(false);
            if (n <= 0)
            {
                if (total == 0 && allowZeroEof)
                {
                    return null; // clean EOF before any byte
                }
                throw new EndOfStreamException("Truncated read (" + total + " of " + count + " bytes).");
            }
            total += n;
        }
        return buffer;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Parse helpers over the CanonicalJson value map.
    // ─────────────────────────────────────────────────────────────────────

    private static bool ProtocolVersionMatches(Dictionary<string, CanonicalJson.JsonValue> env)
    {
        return ReadInt(env, "protocolVersion") == ProtocolVersion;
    }

    private static CanonicalJson.JsonValue? GetObject(Dictionary<string, CanonicalJson.JsonValue> map, string key)
    {
        return map.TryGetValue(key, out CanonicalJson.JsonValue? v) && v.Kind == CanonicalJson.JsonKind.Object
            ? v
            : null;
    }

    private static bool ReadBool(Dictionary<string, CanonicalJson.JsonValue> map, string key)
    {
        return map.TryGetValue(key, out CanonicalJson.JsonValue? v) && v.Kind == CanonicalJson.JsonKind.Bool && v.BoolValue;
    }

    private static int ReadInt(Dictionary<string, CanonicalJson.JsonValue> map, string key)
    {
        return map.TryGetValue(key, out CanonicalJson.JsonValue? v) && v.Kind == CanonicalJson.JsonKind.Number
            ? (int)v.NumberValue
            : 0;
    }

    private static string? ReadString(Dictionary<string, CanonicalJson.JsonValue> map, string key)
    {
        return map.TryGetValue(key, out CanonicalJson.JsonValue? v) && v.Kind == CanonicalJson.JsonKind.String
            ? v.StringValue
            : null;
    }
}

/// <summary>The parsed ping result the <c>live_ping</c> tool maps. Never thrown — always returned.</summary>
public sealed class LivePingResult
{
    public bool Listening;
    public bool GameRunning;
    public int Pid;
    public string? ClientRoot; // R3-3 root-reconciliation diagnostic
    public string? Error;      // non-null on a protocolVersion skew / malformed ack (structured error)

    /// <summary>No server listening / timeout / transport error → honest listening=false (Pitfall 3).</summary>
    public static LivePingResult NotListening() => new LivePingResult { Listening = false };

    /// <summary>protocolVersion skew (CUR-NEW-5): a structured error, not a silent parse.</summary>
    public static LivePingResult ProtocolSkew(int received) => new LivePingResult
    {
        Listening = false,
        Error = "protocolVersion skew: client expects " + LivePipeClient.ProtocolVersion +
                ", server ack was " + received,
    };

    /// <summary>A structurally invalid ack → a structured error.</summary>
    public static LivePingResult MalformedAck(string detail) => new LivePingResult
    {
        Listening = false,
        Error = detail,
    };
}

/// <summary>The parsed reload-asset result the <c>live_reload_asset</c> tool maps. Never thrown.</summary>
public sealed class LiveReloadResult
{
    public bool Accepted;
    public string? Tier;          // string-encoded enum name (CUR-NEW-9)
    public bool Queued;
    public bool ReloadAttempted;  // always false this phase (C-14)
    public string? Path;
    public string? Note;
    public string? Error;         // non-null on no-client / skew / malformed (structured error)

    /// <summary>No server listening / timeout / transport error.</summary>
    public static LiveReloadResult NoClient(string? path) => new LiveReloadResult
    {
        Accepted = false,
        Queued = false,
        ReloadAttempted = false,
        Path = path,
        Error = "no live client listening (or transport timeout)",
    };

    /// <summary>protocolVersion skew (CUR-NEW-5): a structured error.</summary>
    public static LiveReloadResult ProtocolSkew(string? path, int received) => new LiveReloadResult
    {
        Accepted = false,
        Path = path,
        Error = "protocolVersion skew: client expects " + LivePipeClient.ProtocolVersion +
                ", server ack was " + received,
    };

    /// <summary>A structurally invalid ack → a structured error.</summary>
    public static LiveReloadResult Malformed(string? path, string detail) => new LiveReloadResult
    {
        Accepted = false,
        Path = path,
        Error = detail,
    };
}
