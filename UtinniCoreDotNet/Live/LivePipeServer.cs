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
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using UtinniCoreDotNet.Saving;

namespace UtinniCoreDotNet.Live
{
    /// <summary>
    /// The in-client (injected-host) named-pipe SERVER half of the MCP-03 live bridge (16-02 Task 1b).
    /// Pure-managed net472 — the CLR is already hosted in-proc (clr.cpp), so there is no C++ and no
    /// cross-arch marshaling. A background-thread accept loop parses a request envelope, derives the
    /// honest ack tier via the shipped <see cref="ReloadAssetClassifier"/>, contains the received
    /// RELATIVE path under the pinned SWG CLIENT root via the shared
    /// <see cref="LooseOverridePath.Resolve"/>, reads a GAME-THREAD-CACHED game-state snapshot
    /// (never a worker-thread native read), and marshals reload work onto the shipped
    /// <see cref="Callbacks.GameCallbacks.AddMainLoopCall"/> queue.
    ///
    /// <para><b>R3-2 (the HIGH fix) — game-state is game-thread-confined:</b> the worker / accept loop
    /// reads ONLY <see cref="_gameIsRunningCache"/> (a volatile scalar) and NEVER invokes the injected
    /// <see cref="_gameStateProbe"/> nor references native <c>Game.IsRunning</c>. The probe is invoked
    /// ONLY inside <see cref="RefreshGameStateOnGameThread"/>, which production wires to a game-thread
    /// callback (Task 3). This makes Pitfall-2 SEMANTIC: the worker cannot trigger a native read on its
    /// thread; the native read is structurally confined to the refresh path.</para>
    ///
    /// <para><b>Supported reload asset classes this phase (Codex):</b> the tier comes from
    /// <see cref="ReloadAssetClassifier.Classify"/> with <c>rootType=null</c>, so: texture extensions
    /// (.dds/.tga/.png/.jpg/.jpeg) → ReloadedTextures; .trn → ReloadedTerrain; everything else
    /// (.iff datatable/object-template carriers, .stf, .ws, .prt, unknown) → PendingNextSceneChange.
    /// Because rootType is null this phase, object-template root-type-specific tiers under-classify to
    /// PendingNextSceneChange (conservative; never over-promises a no-op reload).</para>
    ///
    /// <para><b>D-01 candor (C-14):</b> the enqueued Action is an INTENTIONAL best-effort placeholder
    /// this phase (visible render NON-gating; rides on RESID-03). The ack ALWAYS sets
    /// <c>reloadAttempted=false</c> + a transport-only note. The ack returns IMMEDIATELY (no completion
    /// signal — RESEARCH Open Q2). All OUTPUT bytes go through <see cref="CanonicalJson"/> (Task 1a).</para>
    /// </summary>
    public sealed class LivePipeServer : IDisposable
    {
        // ─────────────────────────────────────────────────────────────────────
        // Tunable bounded timeouts (CDX-NEW-7/8). Defaults chosen so a stalled
        // client is closed quickly without starving the accept loop, and Dispose
        // unblocks WaitForConnection within a small bounded window.
        // ─────────────────────────────────────────────────────────────────────
        private readonly int _readTimeoutMs;
        private readonly int _disposeJoinTimeoutMs;

        private readonly string _pipeName;
        private readonly string _clientRoot;          // pinned SWG CLIENT root (NOT injectRoot — CUR-NEW-1)
        private readonly Func<bool> _gameStateProbe;  // production: () => Game.IsRunning. Invoked ONLY by Refresh.
        private readonly Action<Action> _enqueue;     // production: GameCallbacks.AddMainLoopCall
        private readonly Action<string> _log;         // production: Log.Info

        // R3-2: GAME-THREAD-UPDATED cached scalar. The worker/accept loop reads ONLY this field.
        private volatile bool _gameIsRunningCache;

        // Lifecycle.
        private int _started; // Interlocked guard — idempotent StartListener (0 = not started, 1 = started)
        private CancellationTokenSource _cts;
        private Thread _worker;
        private volatile bool _disposed;

        /// <summary>
        /// Constructs the server. <paramref name="gameStateProbe"/> is the ONLY native-game-state read
        /// source and is invoked exclusively by <see cref="RefreshGameStateOnGameThread"/> (R3-2 —
        /// document game-thread-only). <paramref name="enqueue"/> is the game-thread marshal seam
        /// (production: <c>GameCallbacks.AddMainLoopCall</c>). <paramref name="clientRoot"/> is the
        /// pinned SWG CLIENT root for path containment (CUR-NEW-1).
        /// </summary>
        public LivePipeServer(
            string clientRoot,
            Func<bool> gameStateProbe,
            Action<Action> enqueue,
            Action<string> log = null,
            string pipeName = null,
            int readTimeoutMs = 5000,
            int disposeJoinTimeoutMs = 3000)
        {
            _clientRoot = clientRoot;
            _gameStateProbe = gameStateProbe ?? throw new ArgumentNullException(nameof(gameStateProbe));
            _enqueue = enqueue ?? throw new ArgumentNullException(nameof(enqueue));
            _log = log ?? (s => { });
            _pipeName = pipeName ?? LiveBridgeProtocol.PipeName;
            _readTimeoutMs = readTimeoutMs;
            _disposeJoinTimeoutMs = disposeJoinTimeoutMs;
        }

        /// <summary>The pinned SWG CLIENT root used for containment + the ping clientRoot diagnostic.</summary>
        public string ClientRoot => _clientRoot;

        /// <summary>The pipe name this server listens on.</summary>
        public string PipeName => _pipeName;

        /// <summary>The current cached game-state snapshot (the value the worker reads). Exposed for tests.</summary>
        public bool CachedGameIsRunning => _gameIsRunningCache;

        /// <summary>
        /// Refreshes <see cref="_gameIsRunningCache"/> from the injected probe. R3-2: this is the ONLY
        /// place the probe (native <c>Game.IsRunning</c> in production) is invoked, and it is intended
        /// to run on the GAME THREAD (wired via GameCallbacks in Task 3). The accept/read loop never
        /// calls this; it reads the cached field.
        /// </summary>
        public void RefreshGameStateOnGameThread()
        {
            _gameIsRunningCache = _gameStateProbe();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle: idempotent StartListener + bounded Stop/Dispose (C-07/CDX-NEW-7/8).
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Starts the background accept loop. IDEMPOTENT: a second call is a no-op (Interlocked guard),
        /// so it never spawns a second accept loop. Non-blocking (the loop runs on a background thread).
        /// </summary>
        public void StartListener()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LivePipeServer));
            // Interlocked idempotency guard — only the first caller wins.
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            {
                return;
            }
            _cts = new CancellationTokenSource();
            _worker = new Thread(() => AcceptLoop(_cts.Token))
            {
                IsBackground = true,
                Name = "Utinni.LivePipeServer",
            };
            _worker.Start();
        }

        /// <summary>
        /// Signals the accept loop to stop and unblocks any in-flight WaitForConnection within a bounded
        /// window, then joins the worker within <see cref="_disposeJoinTimeoutMs"/>. Idempotent.
        /// </summary>
        public void Stop()
        {
            CancellationTokenSource cts = _cts;
            if (cts != null)
            {
                try { cts.Cancel(); } catch { /* already disposed */ }
            }
            Thread worker = _worker;
            if (worker != null && worker.IsAlive)
            {
                worker.Join(_disposeJoinTimeoutMs);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            try { _cts?.Dispose(); } catch { /* best effort */ }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Accept loop — sequential single-client; one request per connection.
        // R3-2: reads ONLY _gameIsRunningCache; NO Game.IsRunning, NO probe invocation here.
        // ─────────────────────────────────────────────────────────────────────

        private void AcceptLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (NamedPipeServerStream pipe = CreateServerStream())
                    {
                        // BOUNDED connect-wait: WaitForConnectionAsync + a linked CTS so Dispose/Cancel
                        // unblocks it; closing the stream on cancel forces the wait to throw.
                        if (!WaitForConnection(pipe, token))
                        {
                            return; // cancelled
                        }

                        ServeOneConnection(pipe, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Accept-loop-throw recovery: one bad connection never tears down the listener.
                    _log("LivePipeServer: accept-loop exception (recovering): " + ex.Message);
                    if (token.IsCancellationRequested) return;
                }
            }
        }

        private NamedPipeServerStream CreateServerStream()
        {
            // SECURITY — EXACT current-user ACL (CDX-NEW-4): a single PipeAccessRule for
            // WindowsIdentity.GetCurrent().User with FullControl/Allow. NO other rules
            // (deny-others-by-omission). NamedPipeServerStream is local-machine only.
            PipeSecurity security = BuildCurrentUserPipeSecurity();
            return new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                1, // max one server instance — sequential single-client model
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 0,
                outBufferSize: 0,
                pipeSecurity: security);
        }

        /// <summary>
        /// Builds the exact current-user PipeSecurity (CDX-NEW-4): one allow-FullControl rule for the
        /// current user's SID, no other rules. Exposed for the ACL-construction assertion test.
        /// </summary>
        public static PipeSecurity BuildCurrentUserPipeSecurity()
        {
            PipeSecurity security = new PipeSecurity();
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                IdentityReference user = identity.User;
                security.AddAccessRule(new PipeAccessRule(
                    user,
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));
            }
            return security;
        }

        // Bounded connect-wait. Returns true on a connection, false on cancellation.
        private bool WaitForConnection(NamedPipeServerStream pipe, CancellationToken token)
        {
            Task wait = pipe.WaitForConnectionAsync(token);
            try
            {
                wait.GetAwaiter().GetResult();
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (IOException)
            {
                // The pipe was broken/closed (e.g. on cancel). Treat as no-connection.
                return token.IsCancellationRequested ? false : throw new IOException("pipe broken");
            }
        }

        // Serves exactly one request on a connected pipe, with a bounded read timeout, then closes it.
        private void ServeOneConnection(NamedPipeServerStream pipe, CancellationToken token)
        {
            try
            {
                byte[] body = ReadFrameBounded(pipe, token);
                if (body == null)
                {
                    return; // client connected then disconnected / clean EOF before a frame
                }

                byte[] ack = HandleRequestBytes(body);
                LiveBridgeProtocol.WriteFrame(pipe, ack);
                try { pipe.Flush(); } catch { /* best effort */ }
            }
            catch (OperationCanceledException)
            {
                // Stalled client closed within the bounded read window — close this connection only.
                _log("LivePipeServer: connection timed out / cancelled (closing this connection only).");
            }
            catch (EndOfStreamException)
            {
                // Partial / mid-message disconnect — close this connection only; loop continues.
                _log("LivePipeServer: partial/truncated frame (closing this connection only).");
            }
            catch (InvalidDataException ex)
            {
                // Oversize declared length — close this connection only.
                _log("LivePipeServer: rejected frame: " + ex.Message);
            }
            catch (Exception ex)
            {
                _log("LivePipeServer: connection error (closing this connection only): " + ex.Message);
            }
        }

        // Reads one framed message with a BOUNDED per-read timeout (CDX-NEW-7). A client that connects
        // then stalls, drips the body slowly, or declares an oversize frame is closed within the window.
        private byte[] ReadFrameBounded(NamedPipeServerStream pipe, CancellationToken token)
        {
            using (var timeoutCts = new CancellationTokenSource(_readTimeoutMs))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token))
            {
                CancellationToken ct = linked.Token;
                // Read the whole framed message under the linked (timeout + external-cancel) token.
                // We read into a MemoryStream via a bounded helper so a slow drip is cancelled.
                byte[] prefix = ReadExactlyAsync(pipe, 4, ct, allowZeroEof: true);
                if (prefix == null)
                {
                    return null; // clean EOF before any frame
                }
                long len = (uint)(prefix[0] | (prefix[1] << 8) | (prefix[2] << 16) | (prefix[3] << 24));
                if (len < 0 || len > LiveBridgeProtocol.MaxFrameBytes)
                {
                    throw new InvalidDataException(
                        "Declared frame length " + len + " exceeds MaxFrameBytes (" + LiveBridgeProtocol.MaxFrameBytes + ").");
                }
                byte[] bodyBytes = ReadExactlyAsync(pipe, (int)len, ct, allowZeroEof: false);
                return bodyBytes;
            }
        }

        // Reads exactly count bytes under a cancellation token (bounded). Returns null on a clean EOF
        // before any byte when allowZeroEof is true; throws EndOfStreamException on a partial read.
        private static byte[] ReadExactlyAsync(Stream stream, int count, CancellationToken ct, bool allowZeroEof)
        {
            byte[] buffer = new byte[count];
            int total = 0;
            while (total < count)
            {
                Task<int> readTask = stream.ReadAsync(buffer, total, count - total, ct);
                int n;
                try
                {
                    n = readTask.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    throw; // bounded timeout / external cancel
                }
                if (n <= 0)
                {
                    if (total == 0 && allowZeroEof)
                    {
                        return null; // clean EOF before any byte
                    }
                    throw new EndOfStreamException(
                        "Truncated read (" + total + " of " + count + " bytes).");
                }
                total += n;
            }
            return buffer;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Request handling: parse → pure decision (with cached game state) → ack;
        // then perform the marshal as a side effect for a contained, game-running reload.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses an inbound request body, derives the ack via the pure decision (using the CACHED
        /// game-state), performs the game-thread marshal as a side-effect when applicable, and returns
        /// the canonical ack bytes. Public for the loopback/integration test.
        /// </summary>
        public byte[] HandleRequestBytes(byte[] requestBody)
        {
            LiveBridgeProtocol.RequestEnvelope request;
            try
            {
                request = LiveBridgeProtocol.ParseRequest(requestBody);
            }
            catch (FormatException)
            {
                // A structurally invalid body becomes an honest reject ack (never a crash).
                return LiveBridgeProtocol.SerializeReloadAck(new LiveBridgeProtocol.ReloadResult
                {
                    Accepted = false,
                    Tier = ReloadTier.Unavailable,
                    Queued = false,
                    ReloadAttempted = false,
                    Path = null,
                    Note = "malformed request envelope",
                });
            }

            // R3-2: read ONLY the cached scalar here — NO Game.IsRunning, NO probe invocation.
            bool gameIsRunning = _gameIsRunningCache;

            Decision decision = Decide(request, _clientRoot, gameIsRunning);

            // MARSHAL: a contained reload-asset with the cached game running enqueues exactly one Action.
            if (decision.ShouldEnqueue)
            {
                _enqueue(() =>
                {
                    // TODO(RESID-03): best-effort placeholder per D-01 — wired reload deferred;
                    // NOT functional re-render. The ack already reported reloadAttempted=false.
                });
            }

            return decision.AckBytes;
        }

        /// <summary>Internal decision result: the ack bytes + whether the caller should enqueue.</summary>
        public struct Decision
        {
            public byte[] AckBytes;
            public bool ShouldEnqueue;
        }

        /// <summary>
        /// PURE decision (C-09 / CDX-NEW-2): maps a parsed request + the pinned client root + a
        /// <paramref name="gameIsRunning"/> SCALAR (the cached value) to an ack, WITHOUT performing the
        /// enqueue. Unit-testable with no live game thread.
        ///
        /// <list type="bullet">
        ///   <item>protocolVersion mismatch → structured reject ack (CUR-NEW-5).</item>
        ///   <item>ping → ping result incl. the pinned clientRoot diagnostic (R3-3).</item>
        ///   <item>reload-asset → containment first; if contained,
        ///         <see cref="ReloadAssetClassifier.Classify"/> for the tier; if gameIsRunning →
        ///         tier as classified + queued=true (ShouldEnqueue=true); if !gameIsRunning →
        ///         Unavailable + queued=false (no enqueue).</item>
        ///   <item>absolute / escaping path → accepted=false + Rejected disposition + no enqueue.</item>
        /// </list>
        /// </summary>
        public static Decision Decide(LiveBridgeProtocol.RequestEnvelope request, string clientRoot, bool gameIsRunning)
        {
            // protocolVersion skew (CUR-NEW-5).
            if (!LiveBridgeProtocol.IsProtocolVersionCompatible(request.ProtocolVersion))
            {
                byte[] reject = LiveBridgeProtocol.SerializeReloadAck(new LiveBridgeProtocol.ReloadResult
                {
                    Accepted = false,
                    Tier = ReloadTier.Unavailable,
                    Queued = false,
                    ReloadAttempted = false,
                    Path = request.Path,
                    Note = "protocolVersion mismatch: server expects " + LiveBridgeProtocol.ProtocolVersion
                           + ", request was " + request.ProtocolVersion,
                });
                return new Decision { AckBytes = reject, ShouldEnqueue = false };
            }

            // ping → diagnostic result (clientRoot — R3-3).
            if (request.Op == LiveBridgeProtocol.OpPing)
            {
                byte[] ping = LiveBridgeProtocol.SerializePingAck(new LiveBridgeProtocol.PingResult
                {
                    Listening = true,
                    GameRunning = gameIsRunning,
                    Pid = SafePid(),
                    ClientRoot = clientRoot,
                });
                return new Decision { AckBytes = ping, ShouldEnqueue = false };
            }

            // reload-asset.
            if (request.Op == LiveBridgeProtocol.OpReloadAsset)
            {
                // PATH CONTAINMENT against the CLIENT root (C-04 / CUR-NEW-1): the wire path is RELATIVE;
                // resolve via the shared LooseOverridePath.Resolve (reuse, do not reimplement). A rooted /
                // '..'-escaping path throws → Rejected disposition + NO enqueue.
                bool contained;
                try
                {
                    LooseOverridePath.Resolve(clientRoot, request.Path);
                    contained = true;
                }
                catch (ArgumentException)
                {
                    contained = false;
                }

                if (!contained)
                {
                    byte[] rejected = LiveBridgeProtocol.SerializeReloadAck(new LiveBridgeProtocol.ReloadResult
                    {
                        Accepted = false,
                        Tier = ReloadTier.Unavailable,
                        Queued = false,
                        ReloadAttempted = false,
                        Path = request.Path,
                        Note = "Rejected: path is absolute or escapes the client root.",
                    });
                    return new Decision { AckBytes = rejected, ShouldEnqueue = false };
                }

                // Contained: derive the honest tier. rootType is null this phase (Codex doc above).
                ReloadTier tier = ReloadAssetClassifier.Classify(request.Ext, null);

                if (!gameIsRunning)
                {
                    // No live client — Unavailable (the upstream dispatcher's !Game.IsRunning mapping).
                    byte[] unavailable = LiveBridgeProtocol.SerializeReloadAck(new LiveBridgeProtocol.ReloadResult
                    {
                        Accepted = true,
                        Tier = ReloadTier.Unavailable,
                        Queued = false,
                        ReloadAttempted = false,
                        Path = request.Path,
                        Note = "No live client (game not running); reload unavailable.",
                    });
                    return new Decision { AckBytes = unavailable, ShouldEnqueue = false };
                }

                // Game running + contained → honest tier + queued; enqueue exactly one Action.
                byte[] ack = LiveBridgeProtocol.SerializeReloadAck(new LiveBridgeProtocol.ReloadResult
                {
                    Accepted = true,
                    Tier = tier,
                    Queued = true,
                    ReloadAttempted = false, // C-14 candor
                    Path = request.Path,
                    Note = "transport-only until RESID-03; queued != visible reload",
                });
                return new Decision { AckBytes = ack, ShouldEnqueue = true };
            }

            // Unknown op → honest reject.
            byte[] unknown = LiveBridgeProtocol.SerializeReloadAck(new LiveBridgeProtocol.ReloadResult
            {
                Accepted = false,
                Tier = ReloadTier.Unavailable,
                Queued = false,
                ReloadAttempted = false,
                Path = request.Path,
                Note = "unknown op: " + (request.Op ?? "(null)"),
            });
            return new Decision { AckBytes = unknown, ShouldEnqueue = false };
        }

        private static int SafePid()
        {
            try
            {
                return System.Diagnostics.Process.GetCurrentProcess().Id;
            }
            catch
            {
                return 0;
            }
        }
    }
}
