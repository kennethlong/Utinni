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
using UtinniCoreDotNet.Saving;

namespace UtinniCoreDotNet.Live
{
    /// <summary>
    /// The shared live-bridge protocol: pipe name, protocol version, bounded frame size, the four
    /// request/ack envelope DTOs, the 4-byte little-endian length-prefix framing, and the
    /// protocol-version-skew check helper (16-02 Task 1a).
    ///
    /// <para><b>Canonical agreement anchors (R3-1 / CDX-NEW-1):</b> <see cref="PipeName"/> is
    /// byte-identical to line 1 of <c>Utinni.Cli.Tests/Fixtures/live/pipe-name.txt</c>;
    /// <see cref="FramingDescriptor"/> is byte-identical to line 2. The four
    /// <c>Utinni.Cli.Tests/Fixtures/live/wire-*.json</c> golden byte-vectors are the produced/consumed
    /// payloads. 16-03's net10 re-implementation of <see cref="CanonicalJson"/> asserts against the
    /// SAME committed fixtures, so the duplicated literals are provably byte-identical across the TFM
    /// wall.</para>
    ///
    /// <para><b>OUTPUT determinism (C-03):</b> every ack/request body is produced by
    /// <see cref="CanonicalJson"/> (hand-rolled, BCL-only — no <c>System.Text.Json</c> package). INPUT
    /// is parsed via <c>CanonicalJson.Deserialize</c>.</para>
    /// </summary>
    public static class LiveBridgeProtocol
    {
        // Byte-identical to Utinni.Cli.Tests/Fixtures/live/pipe-name.txt line 1. The canonical
        // agreement anchor BOTH pipe ends (this net472 server + the 16-03 net10 client) assert against.
        public const string PipeName = "utinni-live-bridge";

        // Byte-identical to the protocol/maxframe tokens in pipe-name.txt line 2.
        public const int ProtocolVersion = 1;
        public const int MaxFrameBytes = 65536;

        // Byte-identical to Utinni.Cli.Tests/Fixtures/live/pipe-name.txt line 2 — the framing
        // descriptor (C-05): 4-byte LE length prefix + UTF-8 JSON body, bounded max frame, protocol
        // version, camelCase JSON policy, and the ReloadTier stringnames wire encoding (CUR-NEW-9).
        // Both ends parse this and assert their implemented constants match.
        public const string FramingDescriptor =
            "frame=len32le+utf8json;maxframe=65536;protocol=1;json=camelCase;tier=stringnames";

        // The CLI sorted-key envelope mirror (RESEARCH A3): every ack carries schemaVersion=1.
        public const int SchemaVersion = 1;

        // Op tokens (kept deliberately tiny per D-02): only ping + reload-asset.
        public const string OpPing = "ping";
        public const string OpReloadAsset = "reload-asset";

        // ─────────────────────────────────────────────────────────────────────
        // Envelope DTOs (plain classes). The wire path RELATIVE (CUR-NEW-1).
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Inbound request envelope: <c>{ protocolVersion, op }</c> plus
        /// <c>{ path, ext }</c> for reload-asset (path RELATIVE — CUR-NEW-1).</summary>
        public sealed class RequestEnvelope
        {
            public int ProtocolVersion;
            public string Op;
            public string Path; // relative; null for ping
            public string Ext;  // null for ping
        }

        /// <summary>Ping ack result: <c>{ listening, gameRunning, pid, clientRoot }</c>
        /// (R3-3 — clientRoot is the in-client resolved client root, a diagnostic).</summary>
        public sealed class PingResult
        {
            public bool Listening;
            public bool GameRunning;
            public int Pid;
            public string ClientRoot; // R3-3 root-reconciliation diagnostic
        }

        /// <summary>Reload-asset ack result:
        /// <c>{ accepted, tier, queued, reloadAttempted, path, note }</c>. <c>tier</c> is the EXACT
        /// <see cref="ReloadTier"/> enum name (CUR-NEW-9); <c>reloadAttempted</c> is always false
        /// this phase (C-14 candor); <c>note</c> carries the transport-only candor string.</summary>
        public sealed class ReloadResult
        {
            public bool Accepted;
            public ReloadTier Tier;
            public bool Queued;
            public bool ReloadAttempted; // always false this phase (C-14)
            public string Path;          // echo of the relative wire path (or the rejected input)
            public string Note;
        }

        // ─────────────────────────────────────────────────────────────────────
        // OUTPUT serialization — EXCLUSIVELY via CanonicalJson (R3-1). Field order
        // below IS the wire contract (fixed declared order; the goldens reflect it).
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
        /// <c>{"protocolVersion":N,"op":"reload-asset","path":"...","ext":"..."}</c>.</summary>
        public static byte[] SerializeReloadRequest(int protocolVersion, string relativePath, string ext)
        {
            var w = new CanonicalJson.JsonObjectWriter()
                .AddInt("protocolVersion", protocolVersion)
                .AddString("op", OpReloadAsset)
                .AddString("path", relativePath)
                .AddString("ext", ext);
            return CanonicalJson.Serialize(w);
        }

        /// <summary>Serializes a ping ACK:
        /// <c>{"schemaVersion":1,"protocolVersion":1,"op":"ping","result":{...}}</c>.</summary>
        public static byte[] SerializePingAck(PingResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var inner = new CanonicalJson.JsonObjectWriter()
                .AddBool("listening", result.Listening)
                .AddBool("gameRunning", result.GameRunning)
                .AddInt("pid", result.Pid)
                .AddString("clientRoot", result.ClientRoot);
            var w = new CanonicalJson.JsonObjectWriter()
                .AddInt("schemaVersion", SchemaVersion)
                .AddInt("protocolVersion", ProtocolVersion)
                .AddString("op", OpPing)
                .AddObject("result", inner);
            return CanonicalJson.Serialize(w);
        }

        /// <summary>Serializes a reload-asset ACK:
        /// <c>{"schemaVersion":1,"protocolVersion":1,"op":"reload-asset","result":{...}}</c>.
        /// <c>tier</c> is the exact enum name (CUR-NEW-9).</summary>
        public static byte[] SerializeReloadAck(ReloadResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var inner = new CanonicalJson.JsonObjectWriter()
                .AddBool("accepted", result.Accepted)
                .AddString("tier", result.Tier.ToString()) // exact pinned enum NAME (CUR-NEW-9)
                .AddBool("queued", result.Queued)
                .AddBool("reloadAttempted", result.ReloadAttempted)
                .AddString("path", result.Path)
                .AddString("note", result.Note);
            var w = new CanonicalJson.JsonObjectWriter()
                .AddInt("schemaVersion", SchemaVersion)
                .AddInt("protocolVersion", ProtocolVersion)
                .AddString("op", OpReloadAsset)
                .AddObject("result", inner);
            return CanonicalJson.Serialize(w);
        }

        // ─────────────────────────────────────────────────────────────────────
        // INPUT parsing — via CanonicalJson.Deserialize.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Parses an inbound request body into a <see cref="RequestEnvelope"/>. Missing
        /// optional fields stay null; a missing protocolVersion parses as 0 (which the skew check
        /// then rejects). Throws <see cref="FormatException"/> on a structurally invalid body.</summary>
        public static RequestEnvelope ParseRequest(byte[] utf8)
        {
            Dictionary<string, CanonicalJson.JsonValue> map = CanonicalJson.Deserialize(utf8);
            var env = new RequestEnvelope();
            CanonicalJson.JsonValue v;
            if (map.TryGetValue("protocolVersion", out v) && v.Kind == CanonicalJson.JsonKind.Number)
            {
                env.ProtocolVersion = (int)v.NumberValue;
            }
            if (map.TryGetValue("op", out v) && v.Kind == CanonicalJson.JsonKind.String)
            {
                env.Op = v.StringValue;
            }
            if (map.TryGetValue("path", out v) && v.Kind == CanonicalJson.JsonKind.String)
            {
                env.Path = v.StringValue;
            }
            if (map.TryGetValue("ext", out v) && v.Kind == CanonicalJson.JsonKind.String)
            {
                env.Ext = v.StringValue;
            }
            return env;
        }

        // ─────────────────────────────────────────────────────────────────────
        // protocolVersion skew (CUR-NEW-5).
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Returns true when the request's protocolVersion matches
        /// <see cref="ProtocolVersion"/>. A false return routes the server to a structured reject ack
        /// (Task 1b), NOT a silent partial parse (CUR-NEW-5).</summary>
        public static bool IsProtocolVersionCompatible(int requestProtocolVersion)
        {
            return requestProtocolVersion == ProtocolVersion;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Framing: 4-byte little-endian length prefix + UTF-8 JSON body (RESEARCH A3).
        // The goldens hold the BODY only (no prefix — R3-6); these helpers add/strip it.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Writes a framed message: a 4-byte little-endian length prefix followed by
        /// <paramref name="body"/>. Throws <see cref="ArgumentException"/> if the body exceeds
        /// <see cref="MaxFrameBytes"/>.</summary>
        public static void WriteFrame(Stream stream, byte[] body)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (body.Length > MaxFrameBytes)
            {
                throw new ArgumentException(
                    "Frame body (" + body.Length + " bytes) exceeds MaxFrameBytes (" + MaxFrameBytes + ").",
                    nameof(body));
            }
            byte[] prefix = new byte[4];
            int len = body.Length;
            prefix[0] = (byte)(len & 0xFF);
            prefix[1] = (byte)((len >> 8) & 0xFF);
            prefix[2] = (byte)((len >> 16) & 0xFF);
            prefix[3] = (byte)((len >> 24) & 0xFF);
            stream.Write(prefix, 0, 4);
            stream.Write(body, 0, body.Length);
        }

        /// <summary>Reads one framed message: a 4-byte little-endian length prefix, then exactly that
        /// many body bytes. A declared length &gt; <see cref="MaxFrameBytes"/> (or &lt; 0) is rejected
        /// with <see cref="InvalidDataException"/> WITHOUT reading the oversize body. Returns null on a
        /// clean end-of-stream before the prefix; throws <see cref="EndOfStreamException"/> on a
        /// partial/truncated frame.</summary>
        public static byte[] ReadFrame(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] prefix = new byte[4];
            int got = ReadFully(stream, prefix, 0, 4);
            if (got == 0)
            {
                return null; // clean EOF before any frame
            }
            if (got < 4)
            {
                throw new EndOfStreamException("Truncated length prefix (" + got + " of 4 bytes).");
            }
            long len = (uint)(prefix[0] | (prefix[1] << 8) | (prefix[2] << 16) | (prefix[3] << 24));
            if (len < 0 || len > MaxFrameBytes)
            {
                throw new InvalidDataException(
                    "Declared frame length " + len + " exceeds MaxFrameBytes (" + MaxFrameBytes + ").");
            }
            byte[] body = new byte[(int)len];
            int bodyGot = ReadFully(stream, body, 0, (int)len);
            if (bodyGot < len)
            {
                throw new EndOfStreamException(
                    "Truncated frame body (" + bodyGot + " of " + len + " bytes).");
            }
            return body;
        }

        // Reads up to count bytes; returns the number actually read (may be < count at EOF).
        private static int ReadFully(Stream stream, byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = stream.Read(buffer, offset + total, count - total);
                if (n <= 0) break;
                total += n;
            }
            return total;
        }
    }
}
