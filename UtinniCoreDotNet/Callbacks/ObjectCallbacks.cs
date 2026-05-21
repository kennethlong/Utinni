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
    // Phase 3 R-A + R-H managed-side: onTarget is subscriber-backed (gets handle
    // Subscribe/Unsubscribe per D-08/D-09) while onTargetCall stays queue-backed
    // (IN-05 / D-11). Snapshot dispatch per D-12.
    public static class ObjectCallbacks
    {
        // Subscriber-backed registry (R-A D-08/D-09).
        private static readonly Dictionary<int, Action> onTargetSubscribers = new Dictionary<int, Action>();
        private static int s_nextOnTargetId = 1; // 0 reserved as invalid handle per D-09
        private static readonly object onTargetLock = new object();

        // Queue-backed registrations: stay queues per IN-05 / D-11.
        private static readonly ConcurrentQueue<Action> onTargetCallQueue = new ConcurrentQueue<Action>();

        // C-16 (resolves CON-O-03 2026-Q2): The static field acts as a GC root for the
        // delegate passed to native via Add*Callback. Without it, the GC can collect the
        // managed delegate while native still holds its stub, causing AVs on later callback
        // dispatch. See https://docs.microsoft.com/dotnet/standard/native-interop/best-practices#function-pointers
        private static UtinniCore.Delegates.Action_IntPtr_C dequeueOnTargetCallsAction;
        public static void Initialize()
        {
            dequeueOnTargetCallsAction = DequeueOnTargetCalls;
            UtinniCore.Utinni.CreatureObject.creature_object.AddOnTargetCallback(dequeueOnTargetCallsAction);
        }

        public static void AddOnTargetCall(Action call)
        {
            onTargetCallQueue.Enqueue(call);
        }

        // --- OnTarget callback (R-A subscriber-backed) ---

        public static int SubscribeOnTarget(Action call)
        {
            lock (onTargetLock)
            {
                int id = s_nextOnTargetId++;
                if (id == 0) { id = s_nextOnTargetId++; } // WR-04 skip-zero
                onTargetSubscribers[id] = call;
                return id;
            }
        }

        public static bool UnsubscribeOnTarget(int handle)
        {
            if (handle == 0)
            {
                return false;
            }
            lock (onTargetLock)
            {
                return onTargetSubscribers.Remove(handle);
            }
        }

        // D-10: legacy Add/Remove retained as wrappers (return value discarded).
        public static void AddOnTargetCallback(Action call)
        {
            SubscribeOnTarget(call);
        }

        public static void RemoveOnTargetCallback(Action call)
        {
            lock (onTargetLock)
            {
                int matchId = 0;
                foreach (var kvp in onTargetSubscribers)
                {
                    if (Equals(kvp.Value, call))
                    {
                        matchId = kvp.Key;
                        break;
                    }
                }
                if (matchId != 0)
                {
                    onTargetSubscribers.Remove(matchId);
                }
            }
        }

        private static void DequeueOnTargetCalls(IntPtr pTargetObject)
        {
            CallbackHelpers.Drain(onTargetCallQueue);

            // R-H snapshot iteration per D-12.
            Action[] snapshot;
            lock (onTargetLock)
            {
                snapshot = onTargetSubscribers.Values.ToArray();
            }
            foreach (Action callback in snapshot)
            {
                callback();
            }
        }

        // Phase 3 R-A / IN-05 (per 03-CONTEXT D-11): inline Drain body removed —
        // consolidated into CallbackHelpers.Drain. Wrapper retained so any future
        // test reach via `ObjectCallbacks.Drain(queue)` stays compatible.
        // ToDo fix being able to set IntPtr to object, etc, to be able to pass it
        internal static void Drain(ConcurrentQueue<Action> queue)
        {
            CallbackHelpers.Drain(queue);
        }
    }
}
