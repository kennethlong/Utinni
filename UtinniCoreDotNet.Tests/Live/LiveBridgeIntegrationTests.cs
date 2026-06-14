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
using Xunit;
using UtinniCoreDotNet.Live;
using UtinniCoreDotNet.Saving;

namespace UtinniCoreDotNet.Tests.Live
{
    /// <summary>
    /// Task 3 (B): the REAL cross-implementation integration test (C-02). This is the ONLY side that
    /// can reference the real 16-02 <see cref="LivePipeServer"/> + <see cref="LiveBridgeProtocol"/>
    /// (the TFM wall — <c>UtinniCoreDotNet.Tests</c> is net472; <c>Utinni.Mcp.Tests</c> is net10 and
    /// cannot see the real server). It spins up the REAL <see cref="LivePipeServer"/> on a RANDOMIZED
    /// pipe name with a pinned temp CLIENT root + an injectable game-state probe + an enqueue sink, and
    /// round-trips ping + reload-asset over an ACTUAL named pipe using the shared
    /// <see cref="LiveBridgeProtocol"/> framing/serialization — proving wire-protocol agreement, not
    /// just pipe-name literal equality.
    ///
    /// <para><c>--filter LiveBridgeIntegration</c> selects this class. (net10 agreement with this exact
    /// wire is held by the shared golden byte-vectors + framing fixture both sides assert — Task 3 (A)
    /// + Task 1.)</para>
    /// </summary>
    public sealed class LiveBridgeIntegrationTests
    {
        private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(10);

        // ─────────────────────────────────────────────────────────────────────
        // ping: the real server replies listening:true + the clientRoot diagnostic.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Ping_RealServer_RoundTripsListeningAndClientRoot()
        {
            string clientRoot = CreateTempRoot();
            string pipeName = RandomPipeName();
            int enqueueCount = 0;

            using (var server = new LivePipeServer(
                clientRoot,
                gameStateProbe: () => true,
                enqueue: _ => Interlocked.Increment(ref enqueueCount),
                log: null,
                pipeName: pipeName))
            {
                server.RefreshGameStateOnGameThread(); // cache gameIsRunning=true on the "game thread"
                server.StartListener();

                LiveBridgeProtocol.PingResult result = SendPing(pipeName);

                Assert.True(result.Listening);
                Assert.True(result.GameRunning);
                // R3-3: the diagnostic equals the server's pinned client root.
                Assert.Equal(clientRoot, result.ClientRoot);
            }

            Assert.Equal(0, enqueueCount); // ping never enqueues
            TryDeleteDir(clientRoot);
        }

        // ─────────────────────────────────────────────────────────────────────
        // reload-asset: a CONTAINED relative path with game running → accepted +
        // honest tier + queued + exactly one enqueue.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ReloadAsset_ContainedRelative_GameRunning_AcceptedQueuedAndEnqueuesOnce()
        {
            string clientRoot = CreateTempRoot();
            string pipeName = RandomPipeName();
            int enqueueCount = 0;

            using (var server = new LivePipeServer(
                clientRoot,
                gameStateProbe: () => true,
                enqueue: _ => Interlocked.Increment(ref enqueueCount),
                log: null,
                pipeName: pipeName))
            {
                server.RefreshGameStateOnGameThread();
                server.StartListener();

                // A relative path contained under the temp client root.
                LiveBridgeProtocol.ReloadResult result =
                    SendReload(pipeName, "appearance/mesh/foo.msh", ".msh");

                Assert.True(result.Accepted);
                Assert.Equal(ReloadTier.PendingNextSceneChange, result.Tier); // honest classifier tier
                Assert.True(result.Queued);
                Assert.False(result.ReloadAttempted); // C-14 candor
                Assert.Equal("appearance/mesh/foo.msh", result.Path);
            }

            Assert.Equal(1, enqueueCount); // exactly one Action enqueued
            TryDeleteDir(clientRoot);
        }

        // ─────────────────────────────────────────────────────────────────────
        // reload-asset: an absolute / escaping path → the server rejects it
        // (server-side client-root containment over the wire) and does NOT enqueue.
        // ─────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("C:/windows/system32/evil.msh")] // absolute
        [InlineData("../../escape.msh")]              // .. escape
        public void ReloadAsset_AbsoluteOrEscape_RejectedNoEnqueue(string badPath)
        {
            string clientRoot = CreateTempRoot();
            string pipeName = RandomPipeName();
            int enqueueCount = 0;

            using (var server = new LivePipeServer(
                clientRoot,
                gameStateProbe: () => true,
                enqueue: _ => Interlocked.Increment(ref enqueueCount),
                log: null,
                pipeName: pipeName))
            {
                server.RefreshGameStateOnGameThread();
                server.StartListener();

                LiveBridgeProtocol.ReloadResult result = SendReload(pipeName, badPath, ".msh");

                Assert.False(result.Accepted);
                Assert.Equal(ReloadTier.Unavailable, result.Tier);
            }

            Assert.Equal(0, enqueueCount); // server-side containment blocked the enqueue
            TryDeleteDir(clientRoot);
        }

        // ─────────────────────────────────────────────────────────────────────
        // root reconciliation (CUR-NEW-1 / R3-3): the same-root case succeeds AND
        // the ping clientRoot equals that root; the different-root case exposes the
        // divergence via the diagnostic AND the server rejects a path not contained
        // under ITS OWN pinned root (predictable rejection).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void RootReconciliation_SameRoot_Succeeds_DifferentRoot_DiagnosticExposesDivergence()
        {
            string serverRoot = CreateTempRoot();      // the in-client server's pinned root
            string mcpRootMismatch = CreateTempRoot(); // a DIFFERENT root the MCP host might have pinned
            string pipeName = RandomPipeName();
            int enqueueCount = 0;

            try
            {
                using (var server = new LivePipeServer(
                    serverRoot,
                    gameStateProbe: () => true,
                    enqueue: _ => Interlocked.Increment(ref enqueueCount),
                    log: null,
                    pipeName: pipeName))
                {
                    server.RefreshGameStateOnGameThread();
                    server.StartListener();

                    // (1) SAME-ROOT: the diagnostic equals the server's pinned root, so an operator
                    //     comparing --root (== serverRoot) sees alignment. A contained relative reload
                    //     succeeds (resolved under the server's OWN root).
                    LiveBridgeProtocol.PingResult ping = SendPing(pipeName);
                    Assert.Equal(serverRoot, ping.ClientRoot);
                    Assert.NotEqual(mcpRootMismatch, ping.ClientRoot); // the divergence is visible

                    LiveBridgeProtocol.ReloadResult ok = SendReload(pipeName, "shaders/foo.sht", ".sht");
                    Assert.True(ok.Accepted);

                    // (2) DIFFERENT-ROOT predictable rejection: regardless of what the MCP host pinned,
                    //     the server resolves under ITS OWN root. A relative path is always interpreted
                    //     under serverRoot — an absolute path (the only way to reference mcpRootMismatch)
                    //     is rejected by the server's containment. This proves the server uses its own
                    //     pinned root, and the ping diagnostic is the runtime mechanism to detect the
                    //     mismatch BEFORE relying on a preview.
                    string absoluteIntoMismatch = Path.Combine(mcpRootMismatch, "foo.msh");
                    LiveBridgeProtocol.ReloadResult rejected =
                        SendReload(pipeName, absoluteIntoMismatch, ".msh");
                    Assert.False(rejected.Accepted);
                    Assert.Equal(ReloadTier.Unavailable, rejected.Tier);
                }
            }
            finally
            {
                TryDeleteDir(serverRoot);
                TryDeleteDir(mcpRootMismatch);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Real-wire client helpers — connect to the real server over an ACTUAL
        // NamedPipe and exchange a single framed request/ack via LiveBridgeProtocol.
        // ─────────────────────────────────────────────────────────────────────

        private static LiveBridgeProtocol.PingResult SendPing(string pipeName)
        {
            byte[] request = LiveBridgeProtocol.SerializePingRequest(LiveBridgeProtocol.ProtocolVersion);
            byte[] ack = Exchange(pipeName, request);
            return ParsePingAck(ack);
        }

        private static LiveBridgeProtocol.ReloadResult SendReload(string pipeName, string relativePath, string ext)
        {
            byte[] request = LiveBridgeProtocol.SerializeReloadRequest(
                LiveBridgeProtocol.ProtocolVersion, relativePath, ext);
            byte[] ack = Exchange(pipeName, request);
            return ParseReloadAck(ack);
        }

        private static byte[] Exchange(string pipeName, byte[] requestBody)
        {
            using (var pipe = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                pipe.Connect((int)ClientTimeout.TotalMilliseconds);
                LiveBridgeProtocol.WriteFrame(pipe, requestBody);
                pipe.Flush();
                byte[] ack = LiveBridgeProtocol.ReadFrame(pipe);
                Assert.NotNull(ack);
                return ack;
            }
        }

        // The ack is { schemaVersion, protocolVersion, op, result:{...} }; parse via CanonicalJson.
        private static LiveBridgeProtocol.PingResult ParsePingAck(byte[] ack)
        {
            Dictionary<string, CanonicalJson.JsonValue> env = CanonicalJson.Deserialize(ack);
            Dictionary<string, CanonicalJson.JsonValue> r = env["result"].Object;
            return new LiveBridgeProtocol.PingResult
            {
                Listening = r["listening"].BoolValue,
                GameRunning = r["gameRunning"].BoolValue,
                Pid = (int)r["pid"].NumberValue,
                ClientRoot = r["clientRoot"].StringValue,
            };
        }

        private static LiveBridgeProtocol.ReloadResult ParseReloadAck(byte[] ack)
        {
            Dictionary<string, CanonicalJson.JsonValue> env = CanonicalJson.Deserialize(ack);
            Dictionary<string, CanonicalJson.JsonValue> r = env["result"].Object;
            ReloadTier tier = (ReloadTier)Enum.Parse(typeof(ReloadTier), r["tier"].StringValue);
            return new LiveBridgeProtocol.ReloadResult
            {
                Accepted = r["accepted"].BoolValue,
                Tier = tier,
                Queued = r["queued"].BoolValue,
                ReloadAttempted = r["reloadAttempted"].BoolValue,
                Path = r["path"].StringValue,
                Note = r["note"].StringValue,
            };
        }

        // ─────────────────────────────────────────────────────────────────────

        private static string CreateTempRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "live_xt_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static string RandomPipeName() => "utinni-live-xtest-" + Guid.NewGuid().ToString("N");

        private static void TryDeleteDir(string dir)
        {
            try { if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
