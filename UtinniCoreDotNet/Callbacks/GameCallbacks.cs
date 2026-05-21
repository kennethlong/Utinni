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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace UtinniCoreDotNet.Callbacks
{
    // Phase 3 R-A + R-H managed-side (per 03-CONTEXT D-08/D-09/D-10/D-12):
    //   * Handle-based Subscribe(Action) -> int / Unsubscribe(int) -> bool per
    //     callback, backed by a per-callback Dictionary<int, Action> + lock.
    //     Handle 0 is reserved as the invalid sentinel (D-09).
    //   * Legacy Add*Callback retained as a thin wrapper around Subscribe per
    //     D-10 (return value discarded) — preserves source-compat for existing
    //     UtinniPlugins (TJT, Sytner). Where a Remove*Callback existed, it now
    //     does a best-effort reverse lookup so the prior API keeps working.
    //   * Dispatch sites copy registry Values under the per-callback lock to a
    //     local array, then iterate outside the lock — Subscribe-during-dispatch
    //     lands in the registry but doesn't fire until the next iteration (R-H
    //     snapshot pattern; cannot throw InvalidOperationException).
    //   * Queue-backed registrations (preMainLoopCallQueue / mainLoopCallQueue)
    //     stay queues per IN-05 / D-11; Drain delegates to CallbackHelpers.
    public static class GameCallbacks
    {
        // Subscriber-backed registries (per-callback Dictionary + monotonic next-id + lock).
        private static readonly Dictionary<int, Action> installSubscribers = new Dictionary<int, Action>();
        private static int s_nextInstallId = 1; // 0 reserved as invalid handle per D-09
        private static readonly object installLock = new object();

        private static readonly Dictionary<int, Action> setupSceneSubscribers = new Dictionary<int, Action>();
        private static int s_nextSetupSceneId = 1;
        private static readonly object setupSceneLock = new object();

        private static readonly Dictionary<int, Action> cleanupSceneSubscribers = new Dictionary<int, Action>();
        private static int s_nextCleanupSceneId = 1;
        private static readonly object cleanupSceneLock = new object();

        // Queue-backed registrations: stay queues per IN-05 / D-11 (drain helper).
        private static readonly ConcurrentQueue<Action> preMainLoopCallQueue = new ConcurrentQueue<Action>();
        private static readonly ConcurrentQueue<Action> mainLoopCallQueue = new ConcurrentQueue<Action>();

        // C-16 (resolves CON-O-03 2026-Q2): The static fields below act as GC roots for
        // the delegates passed to native via Add*Callback. Without them, the GC can
        // collect the managed delegate while native still holds its stub, causing AVs on
        // later callback dispatch. See https://docs.microsoft.com/dotnet/standard/native-interop/best-practices#function-pointers
        private static UtinniCore.Delegates.Action_ callInstallCallbacksAction;
        private static UtinniCore.Delegates.Action_ callSetupSceneCallbacksAction;
        private static UtinniCore.Delegates.Action_ callCleanupSceneCallbacksAction;
        private static UtinniCore.Delegates.Action_ dequeuePreMainLoopCallsAction;
        private static UtinniCore.Delegates.Action_ dequeueMainLoopCallsAction;
        public static void Initialize()
        {
            callInstallCallbacksAction = CallInstallCallbacks;
            callSetupSceneCallbacksAction = CallSetupSceneCallbacks;
            callCleanupSceneCallbacksAction = CallCleanupSceneCallbacks;
            dequeuePreMainLoopCallsAction = DequeuePreMainLoopCalls;
            dequeueMainLoopCallsAction = DequeueMainLoopCalls;

            UtinniCore.Utinni.Game.AddInstallCallback(callInstallCallbacksAction);
            UtinniCore.Utinni.Game.AddSetSceneCallback(callSetupSceneCallbacksAction);
            UtinniCore.Utinni.Game.AddCleanupSceneCallback(callCleanupSceneCallbacksAction);
            UtinniCore.Utinni.Game.AddPreMainLoopCallback(dequeuePreMainLoopCallsAction);
            UtinniCore.Utinni.Game.AddMainLoopCallback(dequeueMainLoopCallsAction);
        }

        // --- Install callback (subscriber-backed) ---

        public static int SubscribeInstall(Action call)
        {
            lock (installLock)
            {
                int id = s_nextInstallId++;
                // WR-04 (03-REVIEW) skip-zero: post-2^31 wrap produces D-09's
                // invalid sentinel; advance past it.
                if (id == 0) { id = s_nextInstallId++; }
                installSubscribers[id] = call;
                return id;
            }
        }

        public static bool UnsubscribeInstall(int handle)
        {
            // Handle 0 is reserved as invalid sentinel (D-09).
            if (handle == 0)
            {
                return false;
            }
            lock (installLock)
            {
                return installSubscribers.Remove(handle);
            }
        }

        // D-10: legacy Add/Remove retained as wrappers around Subscribe/Unsubscribe
        // (return value discarded). Existing UtinniPlugins keep working without
        // recompile; new code uses Subscribe/Unsubscribe directly.
        public static void AddInstallCallback(Action call)
        {
            SubscribeInstall(call);
        }

        public static void RemoveInstallCallback(Action call)
        {
            // Best-effort legacy lookup: scan for first matching delegate. New code
            // should use UnsubscribeInstall(handle).
            lock (installLock)
            {
                int matchId = 0;
                foreach (var kvp in installSubscribers)
                {
                    if (Equals(kvp.Value, call))
                    {
                        matchId = kvp.Key;
                        break;
                    }
                }
                if (matchId != 0)
                {
                    installSubscribers.Remove(matchId);
                }
            }
        }

        // --- SetupScene callback (subscriber-backed) ---

        public static int SubscribeSetupScene(Action call)
        {
            lock (setupSceneLock)
            {
                int id = s_nextSetupSceneId++;
                if (id == 0) { id = s_nextSetupSceneId++; } // WR-04 skip-zero
                setupSceneSubscribers[id] = call;
                return id;
            }
        }

        public static bool UnsubscribeSetupScene(int handle)
        {
            if (handle == 0)
            {
                return false;
            }
            lock (setupSceneLock)
            {
                return setupSceneSubscribers.Remove(handle);
            }
        }

        public static void AddSetupSceneCall(Action call)
        {
            SubscribeSetupScene(call);
        }

        public static void RemoveSetupSceneCall(Action call)
        {
            lock (setupSceneLock)
            {
                int matchId = 0;
                foreach (var kvp in setupSceneSubscribers)
                {
                    if (Equals(kvp.Value, call))
                    {
                        matchId = kvp.Key;
                        break;
                    }
                }
                if (matchId != 0)
                {
                    setupSceneSubscribers.Remove(matchId);
                }
            }
        }

        // --- CleanupScene callback (subscriber-backed) ---

        public static int SubscribeCleanupScene(Action call)
        {
            lock (cleanupSceneLock)
            {
                int id = s_nextCleanupSceneId++;
                if (id == 0) { id = s_nextCleanupSceneId++; } // WR-04 skip-zero
                cleanupSceneSubscribers[id] = call;
                return id;
            }
        }

        public static bool UnsubscribeCleanupScene(int handle)
        {
            if (handle == 0)
            {
                return false;
            }
            lock (cleanupSceneLock)
            {
                return cleanupSceneSubscribers.Remove(handle);
            }
        }

        public static void AddCleanupSceneCall(Action call)
        {
            SubscribeCleanupScene(call);
        }

        public static void RemoveCleanupSceneCall(Action call)
        {
            lock (cleanupSceneLock)
            {
                int matchId = 0;
                foreach (var kvp in cleanupSceneSubscribers)
                {
                    if (Equals(kvp.Value, call))
                    {
                        matchId = kvp.Key;
                        break;
                    }
                }
                if (matchId != 0)
                {
                    cleanupSceneSubscribers.Remove(matchId);
                }
            }
        }

        // --- Queue-backed registrations (IN-05 / D-11) ---

        public static void AddPreMainLoopCall(Action call)
        {
            preMainLoopCallQueue.Enqueue(call);
        }

        public static void AddMainLoopCall(Action call)
        {
            mainLoopCallQueue.Enqueue(call);
        }

        private static void DequeuePreMainLoopCalls()
        {
            CallbackHelpers.Drain(preMainLoopCallQueue);
        }

        private static void DequeueMainLoopCalls()
        {
            CallbackHelpers.Drain(mainLoopCallQueue);
        }

        // Phase 3 R-A / IN-05 (per 03-CONTEXT D-11): inline Drain body removed —
        // consolidated into CallbackHelpers.Drain along with the GroundSceneCallbacks
        // and ObjectCallbacks duplicates. Wrapper retained so existing test reach via
        // `GameCallbacks.Drain(queue)` keeps compiling.
        internal static void Drain(ConcurrentQueue<Action> queue)
        {
            CallbackHelpers.Drain(queue);
        }

        // --- Dispatch sites (R-H snapshot iteration per D-12) ---

        private static void CallInstallCallbacks()
        {
            Action[] snapshot;
            lock (installLock)
            {
                // R-H: snapshot under lock; Subscribe-during-dispatch via callback
                // re-entry lands in installSubscribers but doesn't appear in
                // snapshot, so it fires on the NEXT dispatch instead of throwing
                // InvalidOperationException.
                snapshot = installSubscribers.Values.ToArray();
            }
            foreach (Action callback in snapshot)
            {
                callback();
            }
        }

        private static void CallSetupSceneCallbacks()
        {
            Action[] snapshot;
            lock (setupSceneLock)
            {
                snapshot = setupSceneSubscribers.Values.ToArray();
            }
            foreach (Action callback in snapshot)
            {
                callback();
            }
        }

        private static void CallCleanupSceneCallbacks()
        {
            Action[] snapshot;
            lock (cleanupSceneLock)
            {
                snapshot = cleanupSceneSubscribers.Values.ToArray();
            }
            foreach (Action callback in snapshot)
            {
                callback();
            }
        }
    }
}
