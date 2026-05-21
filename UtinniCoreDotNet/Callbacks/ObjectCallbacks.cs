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

namespace UtinniCoreDotNet.Callbacks
{
    public static class ObjectCallbacks
    {
        private static readonly SynchronizedCollection<Action> onTargetCallbacks = new SynchronizedCollection<Action>();
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

        public static void AddOnTargetCallback(Action call)
        {
            onTargetCallbacks.Add(call);
        }

        public static void RemoveOnTargetCallback(Action call)
        {
            onTargetCallbacks.Remove(call);
        }

        private static void DequeueOnTargetCalls(IntPtr pTargetObject)
        {
            CallbackHelpers.Drain(onTargetCallQueue);

            foreach (Action callback in onTargetCallbacks)
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
