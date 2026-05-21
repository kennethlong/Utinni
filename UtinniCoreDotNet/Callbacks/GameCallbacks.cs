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
    public static class GameCallbacks
    {
        private static readonly SynchronizedCollection<Action> installCallbacks = new SynchronizedCollection<Action>();
        private static readonly SynchronizedCollection<Action> setupSceneCallbacks = new SynchronizedCollection<Action>();
        private static readonly SynchronizedCollection<Action> cleanupSceneCallbacks = new SynchronizedCollection<Action>();
        private static readonly ConcurrentQueue<Action> preMainLoopCallQueue = new ConcurrentQueue<Action>();
        private static readonly ConcurrentQueue<Action> mainLoopCallQueue = new ConcurrentQueue<Action>();

        // C-16 (resolves CON-O-03 2026-Q2): The static field acts as a GC root for the
        // delegate passed to native via Add*Callback. Without it, the GC can collect the
        // managed delegate while native still holds its stub, causing AVs on later callback
        // dispatch. See https://docs.microsoft.com/dotnet/standard/native-interop/best-practices#function-pointers
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

        public static void AddInstallCallback(Action call)
        {
            installCallbacks.Add(call);
        }

        public static void AddSetupSceneCall(Action call)
        {
            setupSceneCallbacks.Add(call);
        }

        public static void AddCleanupSceneCall(Action call)
        {
            cleanupSceneCallbacks.Add(call);
        }

        public static void AddPreMainLoopCall(Action call)
        {
            preMainLoopCallQueue.Enqueue(call);
        }

        public static void AddMainLoopCall(Action call)
        {
            mainLoopCallQueue.Enqueue(call);
        }

        public static void RemoveInstallCallback(Action call)
        {
            installCallbacks.Remove(call);
        }

        public static void RemoveSetupSceneCall(Action call)
        {
            setupSceneCallbacks.Remove(call);
        }

        public static void RemoveCleanupSceneCall(Action call)
        {
            cleanupSceneCallbacks.Remove(call);
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

        private static void CallInstallCallbacks()
        {
            foreach (Action callback in installCallbacks)
            {
                callback();
            }
        }

        private static void CallSetupSceneCallbacks()
        {
            foreach (Action callback in setupSceneCallbacks)
            {
                callback();
            }
        }

        private static void CallCleanupSceneCallbacks()
        {
            foreach (Action callback in cleanupSceneCallbacks)
            {
                callback();
            }
        }
    }
}