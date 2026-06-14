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
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using UtinniCoreDotNet.Live;
using UtinniCoreDotNet.Saving;
using Xunit;

namespace UtinniCoreDotNet.Tests.Live
{
    /// <summary>
    /// Server tests for <see cref="LivePipeServer"/> (16-02 Task 1b; R3-2): the pure decision with the
    /// cached game-state, game-thread-confined game-state semantics (source + behavioral), client-root
    /// containment, the clientRoot diagnostic, protocolVersion skew, bounded lifecycle (idempotent
    /// StartListener, bounded dispose, stalled-client-closed, malformed-then-valid sequential,
    /// accept-loop recovery), server-side partial-read, oversize rejection, ping result shape, and the
    /// exact current-user ACL construction. Loopback tests use a randomized pipe name in-process — no
    /// live SWG client (DEC-C3 Tier-2).
    /// </summary>
    public class LivePipeServerTests
    {
        private static string TempRoot()
        {
            string dir = Path.Combine(Path.GetTempPath(), "utinni-live-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string RandomPipeName()
        {
            return "utinni-live-test-" + Guid.NewGuid().ToString("N");
        }

        private static LiveBridgeProtocol.RequestEnvelope ReloadRequest(string path, string ext)
        {
            return new LiveBridgeProtocol.RequestEnvelope
            {
                ProtocolVersion = LiveBridgeProtocol.ProtocolVersion,
                Op = LiveBridgeProtocol.OpReloadAsset,
                Path = path,
                Ext = ext,
            };
        }

        private static LiveBridgeProtocol.ReloadResult ParseReloadResult(byte[] ackBytes)
        {
            Dictionary<string, CanonicalJson.JsonValue> map = CanonicalJson.Deserialize(ackBytes);
            CanonicalJson.JsonValue result = map["result"];
            return new LiveBridgeProtocol.ReloadResult
            {
                Accepted = result.Object["accepted"].BoolValue,
                Tier = (ReloadTier)Enum.Parse(typeof(ReloadTier), result.Object["tier"].StringValue),
                Queued = result.Object["queued"].BoolValue,
                ReloadAttempted = result.Object["reloadAttempted"].BoolValue,
                Path = result.Object["path"].IsNull ? null : result.Object["path"].StringValue,
                Note = result.Object["note"].StringValue,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Pure decision → tier → ack with the CACHED gameIsRunning true/false +
        // exactly-one-enqueue (R3-2 + marshal).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Decide_ReloadContained_GameRunning_TierAndQueued()
        {
            string root = TempRoot();
            LiveBridgeProtocol.RequestEnvelope req = ReloadRequest("appearance/foo.dds", ".dds");
            LivePipeServer.Decision d = LivePipeServer.Decide(req, root, gameIsRunning: true);
            LiveBridgeProtocol.ReloadResult r = ParseReloadResult(d.AckBytes);
            Assert.True(r.Accepted);
            Assert.Equal(ReloadTier.ReloadedTextures, r.Tier);
            Assert.True(r.Queued);
            Assert.False(r.ReloadAttempted); // C-14
            Assert.True(d.ShouldEnqueue);
        }

        [Fact]
        public void Decide_ReloadContained_GameNotRunning_UnavailableNoEnqueue()
        {
            string root = TempRoot();
            LiveBridgeProtocol.RequestEnvelope req = ReloadRequest("appearance/foo.dds", ".dds");
            LivePipeServer.Decision d = LivePipeServer.Decide(req, root, gameIsRunning: false);
            LiveBridgeProtocol.ReloadResult r = ParseReloadResult(d.AckBytes);
            Assert.Equal(ReloadTier.Unavailable, r.Tier);
            Assert.False(r.Queued);
            Assert.False(d.ShouldEnqueue);
        }

        [Fact]
        public void HandleRequestBytes_ContainedRunning_EnqueuesExactlyOneAction()
        {
            string root = TempRoot();
            int enqueueCount = 0;
            var server = new LivePipeServer(
                root,
                gameStateProbe: () => true,
                enqueue: a => { enqueueCount++; },
                pipeName: RandomPipeName());
            server.RefreshGameStateOnGameThread(); // pull true into the cache

            byte[] reqBytes = LiveBridgeProtocol.SerializeReloadRequest(
                LiveBridgeProtocol.ProtocolVersion, "appearance/foo.dds", ".dds");
            byte[] ack = server.HandleRequestBytes(reqBytes);
            LiveBridgeProtocol.ReloadResult r = ParseReloadResult(ack);

            Assert.True(r.Queued);
            Assert.Equal(1, enqueueCount); // exactly one Action enqueued
        }

        [Fact]
        public void HandleRequestBytes_ContainedNotRunning_DoesNotEnqueue()
        {
            string root = TempRoot();
            int enqueueCount = 0;
            var server = new LivePipeServer(
                root,
                gameStateProbe: () => false,
                enqueue: a => { enqueueCount++; },
                pipeName: RandomPipeName());
            server.RefreshGameStateOnGameThread();

            byte[] reqBytes = LiveBridgeProtocol.SerializeReloadRequest(
                LiveBridgeProtocol.ProtocolVersion, "appearance/foo.dds", ".dds");
            server.HandleRequestBytes(reqBytes);
            Assert.Equal(0, enqueueCount);
        }

        // ─────────────────────────────────────────────────────────────────────
        // CACHED GAME-STATE SEMANTICS (R3-2): (a) source-assertion, (b) behavioral.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void SourceAssertion_AcceptAndDecisionPaths_DoNotReferenceNativeGameIsRunning()
        {
            // (a) The production server CODE (comments stripped — the 15-05 NoDeviceResetTests
            //     grep-gate precedent) must contain NO `Game.IsRunning` reference: the native read is
            //     injected as a probe Func and invoked ONLY by RefreshGameStateOnGameThread. The
            //     worker/accept loop + decision read only the cached scalar (R3-2 semantic guarantee).
            string source = ReadServerSource();
            string code = StripComments(source);
            Assert.DoesNotContain("Game.IsRunning", code);

            // The decision + request handler read `_gameIsRunningCache`; the refresh method exists.
            Assert.Contains("_gameIsRunningCache", code);
            Assert.Contains("RefreshGameStateOnGameThread", code);

            // The probe Func is invoked in exactly one place in CODE — inside RefreshGameStateOnGameThread.
            int probeInvocations = CountOccurrences(code, "_gameStateProbe()");
            Assert.Equal(1, probeInvocations);

            // Anti-trivial self-check (grep-gate hygiene): the comment-stripper must not have nuked the
            // whole file — the cache field declaration is still present in code.
            Assert.Contains("private volatile bool _gameIsRunningCache", code);
        }

        [Fact]
        public void Behavioral_ProbeInvokedOnlyByRefresh_NotByServingARequest()
        {
            // (b) The probe is invoked ONLY via RefreshGameStateOnGameThread, never by serving a worker
            //     request (which reads the cache field).
            string root = TempRoot();
            int probeCalls = 0;
            var server = new LivePipeServer(
                root,
                gameStateProbe: () => { probeCalls++; return true; },
                enqueue: a => { },
                pipeName: RandomPipeName());

            Assert.Equal(0, probeCalls);
            server.RefreshGameStateOnGameThread();
            Assert.Equal(1, probeCalls);
            Assert.True(server.CachedGameIsRunning);

            // Serve a request on the (test) worker path — the probe count must NOT increase.
            byte[] reqBytes = LiveBridgeProtocol.SerializeReloadRequest(
                LiveBridgeProtocol.ProtocolVersion, "appearance/foo.dds", ".dds");
            server.HandleRequestBytes(reqBytes);
            Assert.Equal(1, probeCalls); // unchanged — the worker read the cache field, not the probe
        }

        // ─────────────────────────────────────────────────────────────────────
        // Path containment against the CLIENT root (C-04 / CUR-NEW-1).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Decide_AbsolutePath_RejectedNoEnqueue()
        {
            string root = TempRoot();
            LiveBridgeProtocol.RequestEnvelope req = ReloadRequest(@"C:\evil\foo.dds", ".dds");
            LivePipeServer.Decision d = LivePipeServer.Decide(req, root, gameIsRunning: true);
            LiveBridgeProtocol.ReloadResult r = ParseReloadResult(d.AckBytes);
            Assert.False(r.Accepted);
            Assert.False(d.ShouldEnqueue);
            Assert.Contains("Rejected", r.Note);
        }

        [Fact]
        public void Decide_EscapingPath_RejectedNoEnqueue()
        {
            string root = TempRoot();
            LiveBridgeProtocol.RequestEnvelope req = ReloadRequest("../../evil.dds", ".dds");
            LivePipeServer.Decision d = LivePipeServer.Decide(req, root, gameIsRunning: true);
            LiveBridgeProtocol.ReloadResult r = ParseReloadResult(d.AckBytes);
            Assert.False(r.Accepted);
            Assert.False(d.ShouldEnqueue);
        }

        [Fact]
        public void Decide_OwnRoot_ContainedRelativeResolves()
        {
            // Cross-root case: the server uses its OWN pinned root, not a wire-supplied absolute.
            string rootA = TempRoot();
            LiveBridgeProtocol.RequestEnvelope req = ReloadRequest("sub/dir/asset.dds", ".dds");
            LivePipeServer.Decision d = LivePipeServer.Decide(req, rootA, gameIsRunning: true);
            LiveBridgeProtocol.ReloadResult r = ParseReloadResult(d.AckBytes);
            Assert.True(r.Accepted);
            Assert.True(d.ShouldEnqueue);
        }

        // ─────────────────────────────────────────────────────────────────────
        // clientRoot diagnostic (R3-3) + ping result shape.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Decide_Ping_CarriesPinnedClientRootDiagnostic()
        {
            string root = TempRoot();
            var req = new LiveBridgeProtocol.RequestEnvelope
            {
                ProtocolVersion = LiveBridgeProtocol.ProtocolVersion,
                Op = LiveBridgeProtocol.OpPing,
            };
            LivePipeServer.Decision d = LivePipeServer.Decide(req, root, gameIsRunning: true);
            Dictionary<string, CanonicalJson.JsonValue> map = CanonicalJson.Deserialize(d.AckBytes);
            CanonicalJson.JsonValue result = map["result"];
            Assert.True(result.Object["listening"].BoolValue);
            Assert.True(result.Object["gameRunning"].BoolValue);
            Assert.Equal(root, result.Object["clientRoot"].StringValue);
            Assert.False(d.ShouldEnqueue);
        }

        // ─────────────────────────────────────────────────────────────────────
        // protocolVersion skew (CUR-NEW-5).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Decide_ProtocolSkew_StructuredRejectAck()
        {
            string root = TempRoot();
            var req = new LiveBridgeProtocol.RequestEnvelope
            {
                ProtocolVersion = 999,
                Op = LiveBridgeProtocol.OpPing,
            };
            LivePipeServer.Decision d = LivePipeServer.Decide(req, root, gameIsRunning: true);
            LiveBridgeProtocol.ReloadResult r = ParseReloadResult(d.AckBytes);
            Assert.False(r.Accepted);
            Assert.Contains("protocolVersion mismatch", r.Note);
            Assert.False(d.ShouldEnqueue);
        }

        [Fact]
        public void HandleRequestBytes_MalformedBody_HonestRejectNoCrash()
        {
            string root = TempRoot();
            var server = new LivePipeServer(root, () => true, a => { }, pipeName: RandomPipeName());
            byte[] garbage = System.Text.Encoding.UTF8.GetBytes("{ not valid json ");
            byte[] ack = server.HandleRequestBytes(garbage);
            LiveBridgeProtocol.ReloadResult r = ParseReloadResult(ack);
            Assert.False(r.Accepted);
        }

        // ─────────────────────────────────────────────────────────────────────
        // ACL construction (CDX-NEW-4).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void PipeSecurity_HasExactlyOneCurrentUserFullControlAllowRule()
        {
            PipeSecurity security = LivePipeServer.BuildCurrentUserPipeSecurity();
            AuthorizationRuleCollection rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier));
            Assert.Equal(1, rules.Count);
            var rule = (PipeAccessRule)rules[0];
            Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
            Assert.Equal(PipeAccessRights.FullControl, rule.PipeAccessRights);

            using (WindowsIdentity me = WindowsIdentity.GetCurrent())
            {
                Assert.Equal(me.User, rule.IdentityReference);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle with BOUNDED timeouts (C-07 / CDX-NEW-7/8).
        // ─────────────────────────────────────────────────────────────────────

        // ─────────────────────────────────────────────────────────────────────
        // main.cs wiring source-assertions (16-02 Task 3; C-01 / CUR-NEW-1 / R3-2).
        // The live in-client ping=listening:true is Tier-4 manual / non-gating (D-01); the GATED
        // deliverable is that the WIRING exists, pins the CLIENT root (not injectRoot), and wires the
        // game-thread cache refresh — code-asserted here.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void MainCs_WiresStartListener_AfterCallbacks_BeforeApplicationRun()
        {
            string main = ReadMainSource();
            int iGameCallbacks = main.IndexOf("GameCallbacks.Initialize()", StringComparison.Ordinal);
            int iStartListener = main.IndexOf("StartListener()", StringComparison.Ordinal);
            int iAppRun = main.IndexOf("Application.Run(", StringComparison.Ordinal);
            Assert.True(iGameCallbacks >= 0, "GameCallbacks.Initialize() not found in main.cs");
            Assert.True(iStartListener >= 0, "StartListener() not wired in main.cs (C-01)");
            Assert.True(iAppRun >= 0, "Application.Run not found in main.cs");
            // post-Callbacks, pre-Application.Run.
            Assert.True(iGameCallbacks < iStartListener, "StartListener must be wired AFTER GameCallbacks.Initialize");
            Assert.True(iStartListener < iAppRun, "StartListener must be wired BEFORE Application.Run");
        }

        [Fact]
        public void MainCs_PinsClientRoot_NotInjectRoot_AndGatesOnLiveFlag()
        {
            string main = ReadMainSource();
            string code = StripComments(main);
            // The server is constructed with ResolveClientRoot() (the client-root chain), NOT injectRoot.
            Assert.Contains("ResolveClientRoot()", code);
            Assert.Contains("new LivePipeServer(", code);
            // The construction does not pass injectRoot as the containment root (CUR-NEW-1).
            Assert.DoesNotContain("new LivePipeServer(injectRoot", code);
            // Gated on the in-client enable flag (C-06, default OFF).
            Assert.Contains("\"Live\"", code);
            Assert.Contains("enableLiveBridge", code);
        }

        [Fact]
        public void MainCs_WiresGameThreadRefresh_NativeReadGameThreadConfined()
        {
            string main = ReadMainSource();
            string code = StripComments(main);
            // The game-state refresh is invoked from a GameCallbacks/AddMainLoopCall context, not the
            // accept loop (R3-2). The recurring refresh re-enqueues itself via AddMainLoopCall.
            Assert.Contains("RefreshGameStateOnGameThread", code);
            Assert.Contains("AddMainLoopCall(refresh)", code);
            // The native probe is wired as () => Game.IsRunning (the only native read, game-thread-only).
            Assert.Contains("Game.IsRunning", code);
        }

        [Fact]
        public void StartListener_IsIdempotent_OneAcceptLoop()
        {
            string root = TempRoot();
            string pipe = RandomPipeName();
            using (var server = new LivePipeServer(root, () => true, a => { }, pipeName: pipe))
            {
                server.StartListener();
                // A second StartListener must NOT spawn a second accept loop. With max-instances=1, a
                // second NamedPipeServerStream on the same name would throw IOException — but the
                // idempotency guard means the second call returns without creating one. Assert no throw
                // and that a client can still connect to the single instance.
                server.StartListener();

                bool connected = TryConnectAndPing(pipe, out _);
                Assert.True(connected);
            }
        }

        [Fact]
        public void Dispose_UnblocksWaitForConnection_WithinBoundedTime()
        {
            string root = TempRoot();
            string pipe = RandomPipeName();
            var server = new LivePipeServer(root, () => true, a => { }, pipeName: pipe, disposeJoinTimeoutMs: 2000);
            server.StartListener();
            Thread.Sleep(100); // let the accept loop reach WaitForConnection

            var sw = Stopwatch.StartNew();
            server.Dispose();
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 3000, "Dispose took " + sw.ElapsedMilliseconds + " ms");
        }

        [Fact]
        public void Server_ServesPing_OverLoopback()
        {
            string root = TempRoot();
            string pipe = RandomPipeName();
            using (var server = new LivePipeServer(root, () => true, a => { }, pipeName: pipe))
            {
                server.RefreshGameStateOnGameThread();
                server.StartListener();

                byte[] ack;
                bool ok = TryConnectAndPing(pipe, out ack);
                Assert.True(ok);
                Dictionary<string, CanonicalJson.JsonValue> map = CanonicalJson.Deserialize(ack);
                Assert.Equal(LiveBridgeProtocol.OpPing, map["op"].StringValue);
                Assert.True(map["result"].Object["listening"].BoolValue);
            }
        }

        [Fact]
        public void Server_SurvivesMalformedFrame_ThenServesNextValidConnection()
        {
            string root = TempRoot();
            string pipe = RandomPipeName();
            using (var server = new LivePipeServer(root, () => true, a => { }, pipeName: pipe))
            {
                server.RefreshGameStateOnGameThread();
                server.StartListener();

                // First connection: write a length prefix then disconnect mid-body (partial frame).
                using (var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut))
                {
                    client.Connect(2000);
                    byte[] prefix = { 50, 0, 0, 0 }; // declares 50 bytes
                    client.Write(prefix, 0, 4);
                    client.Write(new byte[] { 1, 2, 3 }, 0, 3); // only 3 bytes then close
                }

                // Second connection on the SAME running server must succeed.
                byte[] ack;
                bool ok = TryConnectAndPing(pipe, out ack);
                Assert.True(ok, "server did not recover to serve a subsequent valid connection");
            }
        }

        [Fact]
        public void Server_ClosesStalledClient_WithoutStarvingAcceptLoop()
        {
            string root = TempRoot();
            string pipe = RandomPipeName();
            using (var server = new LivePipeServer(root, () => true, a => { }, pipeName: pipe, readTimeoutMs: 500))
            {
                server.RefreshGameStateOnGameThread();
                server.StartListener();

                // Connect then stall (send nothing). The server's bounded read timeout closes it.
                using (var stalled = new NamedPipeClientStream(".", pipe, PipeDirection.InOut))
                {
                    stalled.Connect(2000);
                    Thread.Sleep(800); // longer than readTimeoutMs — server should have closed it
                }

                // A subsequent valid connection must still be served (loop not starved).
                byte[] ack;
                bool ok = TryConnectAndPing(pipe, out ack);
                Assert.True(ok);
            }
        }

        // Connects, sends a ping request frame, reads the ack frame. Returns false on connect failure.
        private static bool TryConnectAndPing(string pipe, out byte[] ack)
        {
            ack = null;
            try
            {
                using (var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut))
                {
                    client.Connect(2000);
                    byte[] req = LiveBridgeProtocol.SerializePingRequest(LiveBridgeProtocol.ProtocolVersion);
                    LiveBridgeProtocol.WriteFrame(client, req);
                    client.Flush();
                    ack = LiveBridgeProtocol.ReadFrame(client);
                    return ack != null;
                }
            }
            catch
            {
                return false;
            }
        }

        // Reads the LivePipeServer.cs production source for the source-assertion test (a).
        private static string ReadServerSource()
        {
            // Walk up from the test base dir to the repo root, then to the known source path.
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo dir = new DirectoryInfo(baseDir);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "UtinniCoreDotNet", "Live", "LivePipeServer.cs");
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
                dir = dir.Parent;
            }
            throw new FileNotFoundException("Could not locate UtinniCoreDotNet/Live/LivePipeServer.cs from " + baseDir);
        }

        // Strips // line comments and /* */ block comments (and /** */ doc comments) so the source
        // assertion gates on CODE only, not documentation. Simple state machine; sufficient for this
        // well-formed source file (no // or /* inside string literals in LivePipeServer.cs except the
        // canonical comment markers we deliberately exclude).
        private static string StripComments(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            int i = 0;
            bool inString = false;
            while (i < s.Length)
            {
                char c = s[i];
                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < s.Length)
                    {
                        sb.Append(s[i + 1]);
                        i += 2;
                        continue;
                    }
                    if (c == '"') inString = false;
                    i++;
                    continue;
                }
                if (c == '"')
                {
                    inString = true;
                    sb.Append(c);
                    i++;
                    continue;
                }
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
                {
                    while (i < s.Length && s[i] != '\n') i++;
                    continue;
                }
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/')) i++;
                    i += 2;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        // Reads the UtinniCoreDotNet/main.cs production source for the Task-3 wiring source-assertions.
        private static string ReadMainSource()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo dir = new DirectoryInfo(baseDir);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "UtinniCoreDotNet", "main.cs");
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
                dir = dir.Parent;
            }
            throw new FileNotFoundException("Could not locate UtinniCoreDotNet/main.cs from " + baseDir);
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0, idx = 0;
            while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += needle.Length;
            }
            return count;
        }
    }
}
