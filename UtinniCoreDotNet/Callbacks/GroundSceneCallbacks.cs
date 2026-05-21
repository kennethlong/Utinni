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
    // Phase 3 R-A + R-H managed-side: same pattern as GameCallbacks
    // (Subscribe/Unsubscribe + snapshot dispatch + Drain delegated to
    // CallbackHelpers). Queue-backed registrations (update/preDraw/postDraw)
    // remain queues per IN-05; the subscriber-backed cameraChange callback now
    // exposes handle-based Subscribe/Unsubscribe in addition to legacy Add per
    // D-10.
    public static class GroundSceneCallbacks
    {
        // Queue-backed registrations: stay queues per IN-05 / D-11.
        private static readonly ConcurrentQueue<Action> updateLoopCallQueue = new ConcurrentQueue<Action>();
        private static readonly ConcurrentQueue<Action> preDrawLoopCallQueue = new ConcurrentQueue<Action>();
        private static readonly ConcurrentQueue<Action> postDrawLoopCallQueue = new ConcurrentQueue<Action>();

        // Subscriber-backed registry (R-A D-08/D-09).
        private static readonly Dictionary<int, Action> cameraChangeSubscribers = new Dictionary<int, Action>();
        private static int s_nextCameraChangeId = 1; // 0 reserved as invalid handle per D-09
        private static readonly object cameraChangeLock = new object();

        // C-16 (resolves CON-O-03 2026-Q2): The static field acts as a GC root for the
        // delegate passed to native via Add*Callback. Without it, the GC can collect the
        // managed delegate while native still holds its stub, causing AVs on later callback
        // dispatch. See https://docs.microsoft.com/dotnet/standard/native-interop/best-practices#function-pointers
        private static UtinniCore.Delegates.Action_IntPtr_float dequeueUpdateLoopCallsAction;
        private static UtinniCore.Delegates.Action_IntPtr_C dequeuePreDrawLoopCallsAction;
        private static UtinniCore.Delegates.Action_IntPtr_C dequeuePostDrawLoopCallsAction;
        private static UtinniCore.Delegates.Action_ callCameraChangeCallbacksAction;

        public static void Initialize()
        {
            dequeueUpdateLoopCallsAction = DequeueUpdateLoopCalls;
            dequeuePreDrawLoopCallsAction = DequeuePreDrawLoopCalls;
            dequeuePostDrawLoopCallsAction = DequeuePostDrawLoopCalls;
            callCameraChangeCallbacksAction = CallCameraChangeCallbacks;
            UtinniCore.Utinni.GroundScene.AddUpdateLoopCallback(dequeueUpdateLoopCallsAction);
            UtinniCore.Utinni.GroundScene.AddPreDrawLoopCallback(dequeuePreDrawLoopCallsAction);
            UtinniCore.Utinni.GroundScene.AddPostDrawLoopCallback(dequeuePostDrawLoopCallsAction);
            UtinniCore.Utinni.GroundScene.AddCameraChangeCallback(callCameraChangeCallbacksAction);
        }

        public static void AddUpdateLoopCall(Action call)
        {
            updateLoopCallQueue.Enqueue(call);
        }

        public static void AddPreDrawLoopCall(Action call)
        {
            preDrawLoopCallQueue.Enqueue(call);
        }

        public static void AddPostDrawLoopCall(Action call)
        {
            postDrawLoopCallQueue.Enqueue(call);
        }

        // --- CameraChange callback (R-A subscriber-backed) ---

        public static int SubscribeCameraChange(Action call)
        {
            lock (cameraChangeLock)
            {
                int id = s_nextCameraChangeId++;
                if (id == 0) { id = s_nextCameraChangeId++; } // WR-04 skip-zero
                cameraChangeSubscribers[id] = call;
                return id;
            }
        }

        public static bool UnsubscribeCameraChange(int handle)
        {
            if (handle == 0)
            {
                return false;
            }
            lock (cameraChangeLock)
            {
                return cameraChangeSubscribers.Remove(handle);
            }
        }

        // D-10: legacy Add retained as wrapper (return value discarded). Existing
        // UtinniPlugins keep working without recompile.
        public static void AddCameraChangeCallback(Action call)
        {
            SubscribeCameraChange(call);
        }

        public static void RemoveCameraChangeCallback(Action call)
        {
            // Best-effort legacy lookup; new code should use UnsubscribeCameraChange(handle).
            lock (cameraChangeLock)
            {
                int matchId = 0;
                foreach (var kvp in cameraChangeSubscribers)
                {
                    if (Equals(kvp.Value, call))
                    {
                        matchId = kvp.Key;
                        break;
                    }
                }
                if (matchId != 0)
                {
                    cameraChangeSubscribers.Remove(matchId);
                }
            }
        }

        private static void DequeueUpdateLoopCalls(IntPtr pGroundScene, float elapsedTime)
        {
            CallbackHelpers.Drain(updateLoopCallQueue);
        }

        private static void DequeuePreDrawLoopCalls(IntPtr pGroundScene)
        {
            CallbackHelpers.Drain(preDrawLoopCallQueue);
        }

        private static void DequeuePostDrawLoopCalls(IntPtr pGroundScene)
        {
            // C-04: this previously drained preDrawLoopCallQueue (typo). The shared
            // CallbackHelpers.Drain helper makes the queue-vs-method correspondence
            // explicit so this class of bug cannot recur.
            CallbackHelpers.Drain(postDrawLoopCallQueue);
        }

        // R-H snapshot iteration per D-12: copy Values under lock, iterate outside.
        private static void CallCameraChangeCallbacks()
        {
            Action[] snapshot;
            lock (cameraChangeLock)
            {
                snapshot = cameraChangeSubscribers.Values.ToArray();
            }
            foreach (Action callback in snapshot)
            {
                callback();
            }
        }

        // Phase 3 R-A / IN-05 (per 03-CONTEXT D-11): the inline Drain helper that
        // previously lived here has moved to CallbackHelpers.Drain. The wrapper
        // below preserves the existing `GroundSceneCallbacks.Drain(queue)` call sites
        // (GroundSceneCallbacksTests reaches the helper via this surface) without
        // duplicating the body.
        internal static void Drain(ConcurrentQueue<Action> queue)
        {
            CallbackHelpers.Drain(queue);
        }

    }
}
