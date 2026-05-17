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
    public static class GroundSceneCallbacks
    {
        private static readonly ConcurrentQueue<Action> updateLoopCallQueue = new ConcurrentQueue<Action>();
        private static readonly ConcurrentQueue<Action> preDrawLoopCallQueue = new ConcurrentQueue<Action>();
        private static readonly ConcurrentQueue<Action> postDrawLoopCallQueue = new ConcurrentQueue<Action>();
        private static readonly SynchronizedCollection<Action> cameraChangeCallbacks = new SynchronizedCollection<Action>();

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

        public static void AddCameraChangeCallback(Action call)
        {
            cameraChangeCallbacks.Add(call);
        }

        private static void DequeueUpdateLoopCalls(IntPtr pGroundScene, float elapsedTime)
        {
            Drain(updateLoopCallQueue);
        }

        private static void DequeuePreDrawLoopCalls(IntPtr pGroundScene)
        {
            Drain(preDrawLoopCallQueue);
        }

        private static void DequeuePostDrawLoopCalls(IntPtr pGroundScene)
        {
            // C-04: this previously drained preDrawLoopCallQueue (typo). The Drain helper
            // makes the queue-vs-method correspondence explicit so this class of bug
            // cannot recur.
            Drain(postDrawLoopCallQueue);
        }

        private static void CallCameraChangeCallbacks()
        {
            foreach (Action callback in cameraChangeCallbacks)
            {
                callback();
            }
        }

        // Shared drain helper introduced for C-04 (CON-O-02 default-fallback per D-12):
        // a single TryDequeue loop pattern that every queue-drain call site reuses. The
        // outer Count > 0 check from the original code was dropped because TryDequeue
        // already returns false on an empty queue, and the Count read was racey under
        // concurrent producers anyway.
        internal static void Drain(ConcurrentQueue<Action> queue)
        {
            while (queue.TryDequeue(out var func))
            {
                func();
            }
        }

    }
}
