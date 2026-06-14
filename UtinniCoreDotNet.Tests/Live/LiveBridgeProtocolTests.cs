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
using System.IO;
using System.Text;
using UtinniCoreDotNet.Live;
using UtinniCoreDotNet.Saving;
using Xunit;

namespace UtinniCoreDotNet.Tests.Live
{
    /// <summary>
    /// Wire-contract tests for the live bridge (16-02 Task 1a; R3-1): the deterministic
    /// <see cref="CanonicalJson"/> writer, the four GOLDEN WIRE byte-vectors (produce + consume,
    /// byte-EXACT, incl. the nested acks — R3-7), camelCase + string-tier + clientRoot diagnostic,
    /// the protocolVersion-skew helper, the pipe-name/framing fixture equality, and the 4-byte LE
    /// length-prefix framing round-trip + oversize reject.
    /// </summary>
    public class LiveBridgeProtocolTests
    {
        // The fixtures are linked from Utinni.Cli.Tests/Fixtures/live/ and copied to the test output
        // under Fixtures\live\ (see UtinniCoreDotNet.Tests.csproj). Resolve relative to the test dll.
        private static string FixturePath(string name)
        {
            string baseDir = System.AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, "Fixtures", "live", name);
        }

        private static byte[] ReadFixtureBytes(string name)
        {
            return File.ReadAllBytes(FixturePath(name));
        }

        // ─────────────────────────────────────────────────────────────────────
        // GOLDEN WIRE BYTE-VECTORS — produce (byte-exact) + consume (round-trip).
        // All four, incl. the nested acks (R3-7 — no key-order-independent fallback).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void PingRequest_ProducesByteExactGolden()
        {
            byte[] produced = LiveBridgeProtocol.SerializePingRequest(LiveBridgeProtocol.ProtocolVersion);
            byte[] golden = ReadFixtureBytes("wire-ping-request.json");
            Assert.Equal(golden, produced);
        }

        [Fact]
        public void ReloadRequest_ProducesByteExactGolden()
        {
            byte[] produced = LiveBridgeProtocol.SerializeReloadRequest(
                LiveBridgeProtocol.ProtocolVersion, "appearance/mesh/foo.msh", ".msh");
            byte[] golden = ReadFixtureBytes("wire-reload-request.json");
            Assert.Equal(golden, produced);
        }

        [Fact]
        public void PingAck_ProducesByteExactGolden()
        {
            // Stable PINNED placeholder values so the byte-vector is deterministic (R3-7 / R3-3).
            var result = new LiveBridgeProtocol.PingResult
            {
                Listening = true,
                GameRunning = true,
                Pid = 0,
                ClientRoot = "C:/swg/client",
            };
            byte[] produced = LiveBridgeProtocol.SerializePingAck(result);
            byte[] golden = ReadFixtureBytes("wire-ping-ack.json");
            Assert.Equal(golden, produced);
        }

        [Fact]
        public void ReloadAck_ProducesByteExactGolden()
        {
            var result = new LiveBridgeProtocol.ReloadResult
            {
                Accepted = true,
                Tier = ReloadTier.PendingNextSceneChange, // string-encoded on the wire (CUR-NEW-9)
                Queued = false,
                ReloadAttempted = false, // C-14 candor
                Path = "appearance/mesh/foo.msh",
                Note = "transport-only until RESID-03; queued != visible reload",
            };
            byte[] produced = LiveBridgeProtocol.SerializeReloadAck(result);
            byte[] golden = ReadFixtureBytes("wire-reload-ack.json");
            Assert.Equal(golden, produced);
        }

        [Fact]
        public void PingRequest_ConsumesGolden_RoundTrips()
        {
            byte[] golden = ReadFixtureBytes("wire-ping-request.json");
            LiveBridgeProtocol.RequestEnvelope env = LiveBridgeProtocol.ParseRequest(golden);
            Assert.Equal(LiveBridgeProtocol.ProtocolVersion, env.ProtocolVersion);
            Assert.Equal(LiveBridgeProtocol.OpPing, env.Op);
        }

        [Fact]
        public void ReloadRequest_ConsumesGolden_RoundTrips()
        {
            byte[] golden = ReadFixtureBytes("wire-reload-request.json");
            LiveBridgeProtocol.RequestEnvelope env = LiveBridgeProtocol.ParseRequest(golden);
            Assert.Equal(LiveBridgeProtocol.ProtocolVersion, env.ProtocolVersion);
            Assert.Equal(LiveBridgeProtocol.OpReloadAsset, env.Op);
            Assert.Equal("appearance/mesh/foo.msh", env.Path);
            Assert.Equal(".msh", env.Ext);
        }

        [Fact]
        public void PingAck_ConsumesGolden_RoundTrips()
        {
            byte[] golden = ReadFixtureBytes("wire-ping-ack.json");
            Dictionary<string, CanonicalJson.JsonValue> map = CanonicalJson.Deserialize(golden);
            Assert.Equal(LiveBridgeProtocol.SchemaVersion, (int)map["schemaVersion"].NumberValue);
            Assert.Equal(LiveBridgeProtocol.OpPing, map["op"].StringValue);
            CanonicalJson.JsonValue result = map["result"];
            Assert.Equal(CanonicalJson.JsonKind.Object, result.Kind);
            Assert.True(result.Object["listening"].BoolValue);
            Assert.True(result.Object["gameRunning"].BoolValue);
            Assert.Equal(0, (int)result.Object["pid"].NumberValue);
            Assert.Equal("C:/swg/client", result.Object["clientRoot"].StringValue);
        }

        [Fact]
        public void ReloadAck_ConsumesGolden_RoundTrips()
        {
            byte[] golden = ReadFixtureBytes("wire-reload-ack.json");
            Dictionary<string, CanonicalJson.JsonValue> map = CanonicalJson.Deserialize(golden);
            Assert.Equal(LiveBridgeProtocol.OpReloadAsset, map["op"].StringValue);
            CanonicalJson.JsonValue result = map["result"];
            Assert.True(result.Object["accepted"].BoolValue);
            Assert.Equal("PendingNextSceneChange", result.Object["tier"].StringValue);
            Assert.False(result.Object["queued"].BoolValue);
            Assert.False(result.Object["reloadAttempted"].BoolValue);
            Assert.Equal("appearance/mesh/foo.msh", result.Object["path"].StringValue);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Canonical-writer ORDER STABILITY (R3-1): determinism is from fixed
        // emission, NOT field-set order / reflection.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Serialize_SameDto_ProducesIdenticalBytes()
        {
            var r1 = new LiveBridgeProtocol.ReloadResult
            {
                Accepted = true,
                Tier = ReloadTier.ReloadedTextures,
                Queued = true,
                ReloadAttempted = false,
                Path = "a/b.dds",
                Note = "n",
            };
            byte[] first = LiveBridgeProtocol.SerializeReloadAck(r1);
            byte[] second = LiveBridgeProtocol.SerializeReloadAck(r1);
            Assert.Equal(first, second);
        }

        [Fact]
        public void Serialize_FieldsSetInDifferentOrder_SameOutputKeyOrder()
        {
            // Set the fields in a deliberately different source order than the wire order; the OUTPUT
            // key order must still be the fixed declared order (proves determinism from fixed emission).
            var a = new LiveBridgeProtocol.PingResult();
            a.ClientRoot = "C:/r";
            a.Pid = 7;
            a.GameRunning = false;
            a.Listening = true;

            var b = new LiveBridgeProtocol.PingResult();
            b.Listening = true;
            b.GameRunning = false;
            b.Pid = 7;
            b.ClientRoot = "C:/r";

            byte[] fromA = LiveBridgeProtocol.SerializePingAck(a);
            byte[] fromB = LiveBridgeProtocol.SerializePingAck(b);
            Assert.Equal(fromA, fromB);

            // And the key order is listening,gameRunning,pid,clientRoot.
            string text = Encoding.UTF8.GetString(fromA);
            int iListening = text.IndexOf("\"listening\"");
            int iGameRunning = text.IndexOf("\"gameRunning\"");
            int iPid = text.IndexOf("\"pid\"");
            int iClientRoot = text.IndexOf("\"clientRoot\"");
            Assert.True(iListening < iGameRunning && iGameRunning < iPid && iPid < iClientRoot);
        }

        // ─────────────────────────────────────────────────────────────────────
        // camelCase + string-tier (CUR-NEW-4 / CUR-NEW-9).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ReloadAck_UsesCamelCaseKeys_AndStringTier()
        {
            var result = new LiveBridgeProtocol.ReloadResult
            {
                Accepted = false,
                Tier = ReloadTier.PendingNextSceneChange,
                Queued = false,
                ReloadAttempted = false,
                Path = "x.iff",
                Note = "n",
            };
            string text = Encoding.UTF8.GetString(LiveBridgeProtocol.SerializeReloadAck(result));
            // string tier, not integer
            Assert.Contains("\"tier\":\"PendingNextSceneChange\"", text);
            Assert.DoesNotContain("\"tier\":2", text);
            // camelCase, not PascalCase
            Assert.Contains("\"protocolVersion\"", text);
            Assert.DoesNotContain("\"ProtocolVersion\"", text);
            Assert.Contains("\"reloadAttempted\"", text);
        }

        // ─────────────────────────────────────────────────────────────────────
        // clientRoot diagnostic (R3-3).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void PingAck_CarriesClientRootDiagnostic_AndRoundTrips()
        {
            var result = new LiveBridgeProtocol.PingResult
            {
                Listening = true,
                GameRunning = false,
                Pid = 123,
                ClientRoot = "D:/some/client/root",
            };
            byte[] bytes = LiveBridgeProtocol.SerializePingAck(result);
            string text = Encoding.UTF8.GetString(bytes);
            Assert.Contains("\"clientRoot\":\"D:/some/client/root\"", text);

            Dictionary<string, CanonicalJson.JsonValue> map = CanonicalJson.Deserialize(bytes);
            Assert.Equal("D:/some/client/root", map["result"].Object["clientRoot"].StringValue);
        }

        // ─────────────────────────────────────────────────────────────────────
        // protocolVersion skew helper (CUR-NEW-5).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ProtocolVersionSkew_Helper_FlagsMismatch()
        {
            Assert.True(LiveBridgeProtocol.IsProtocolVersionCompatible(LiveBridgeProtocol.ProtocolVersion));
            Assert.False(LiveBridgeProtocol.IsProtocolVersionCompatible(999));
            Assert.False(LiveBridgeProtocol.IsProtocolVersionCompatible(0));
        }

        // ─────────────────────────────────────────────────────────────────────
        // pipe-name / framing fixture equality (line 1 + line 2 — C-05).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void PipeNameFixture_Line1AndLine2_MatchConstants()
        {
            string[] lines = File.ReadAllText(FixturePath("pipe-name.txt")).Replace("\r\n", "\n").Split('\n');
            Assert.Equal(LiveBridgeProtocol.PipeName, lines[0]);
            Assert.Equal(LiveBridgeProtocol.FramingDescriptor, lines[1]);
        }

        [Fact]
        public void FramingDescriptor_EncodesProtocolAndMaxFrameConstants()
        {
            Assert.Contains("protocol=" + LiveBridgeProtocol.ProtocolVersion, LiveBridgeProtocol.FramingDescriptor);
            Assert.Contains("maxframe=" + LiveBridgeProtocol.MaxFrameBytes, LiveBridgeProtocol.FramingDescriptor);
            Assert.Contains("tier=stringnames", LiveBridgeProtocol.FramingDescriptor);
            Assert.Contains("json=camelCase", LiveBridgeProtocol.FramingDescriptor);
        }

        // ─────────────────────────────────────────────────────────────────────
        // framing round-trip + oversize reject (the 4-byte LE prefix, R3-6).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Framing_WriteThenRead_ReturnsSameBody()
        {
            byte[] body = LiveBridgeProtocol.SerializePingRequest(LiveBridgeProtocol.ProtocolVersion);
            using (var ms = new MemoryStream())
            {
                LiveBridgeProtocol.WriteFrame(ms, body);
                ms.Position = 0;
                byte[] readBack = LiveBridgeProtocol.ReadFrame(ms);
                Assert.Equal(body, readBack);
            }
        }

        [Fact]
        public void Framing_WriteFrame_RejectsOversizeBody()
        {
            byte[] tooBig = new byte[LiveBridgeProtocol.MaxFrameBytes + 1];
            using (var ms = new MemoryStream())
            {
                Assert.Throws<System.ArgumentException>(() => LiveBridgeProtocol.WriteFrame(ms, tooBig));
            }
        }

        [Fact]
        public void Framing_ReadFrame_RejectsOversizeDeclaredLength_WithoutReadingBody()
        {
            // Craft a prefix declaring MaxFrameBytes+1 with NO body following — ReadFrame must reject
            // on the prefix alone (it must not attempt to read the oversize body).
            int declared = LiveBridgeProtocol.MaxFrameBytes + 1;
            byte[] prefix = new byte[]
            {
                (byte)(declared & 0xFF),
                (byte)((declared >> 8) & 0xFF),
                (byte)((declared >> 16) & 0xFF),
                (byte)((declared >> 24) & 0xFF),
            };
            using (var ms = new MemoryStream(prefix))
            {
                Assert.Throws<InvalidDataException>(() => LiveBridgeProtocol.ReadFrame(ms));
            }
        }

        [Fact]
        public void Framing_ReadFrame_ReturnsNullOnCleanEof()
        {
            using (var ms = new MemoryStream(new byte[0]))
            {
                Assert.Null(LiveBridgeProtocol.ReadFrame(ms));
            }
        }
    }
}
