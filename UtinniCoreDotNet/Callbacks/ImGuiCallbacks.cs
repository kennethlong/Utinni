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
using System.Collections.Generic;
using System.Linq;

namespace UtinniCoreDotNet.Callbacks
{
    // Phase 3 R-A + R-H managed-side: four ImGui-gizmo callbacks
    // (onEnabled/onDisabled/onPositionChanged/onRotationChanged) each gain a
    // handle-based Subscribe/Unsubscribe per D-08/D-09. Pre-Phase-3, this class
    // had partial Remove coverage (onPositionChanged/onRotationChanged only);
    // Phase 3 completes the Remove pair for onEnabled/onDisabled as well.
    // Dispatch sites use the R-H snapshot pattern per D-12.
    public static class ImGuiCallbacks
    {
        private static readonly Dictionary<int, Action> onEnabledSubscribers = new Dictionary<int, Action>();
        private static int s_nextOnEnabledId = 1; // 0 reserved as invalid handle per D-09
        private static readonly object onEnabledLock = new object();

        private static readonly Dictionary<int, Action> onDisabledSubscribers = new Dictionary<int, Action>();
        private static int s_nextOnDisabledId = 1;
        private static readonly object onDisabledLock = new object();

        private static readonly Dictionary<int, Action> onPositionChangedSubscribers = new Dictionary<int, Action>();
        private static int s_nextOnPositionChangedId = 1;
        private static readonly object onPositionChangedLock = new object();

        private static readonly Dictionary<int, Action> onRotationChangedSubscribers = new Dictionary<int, Action>();
        private static int s_nextOnRotationChangedId = 1;
        private static readonly object onRotationChangedLock = new object();

        private static UtinniCore.Delegates.Action_ onEnabledCallbackAction;
        private static UtinniCore.Delegates.Action_ onDisabledCallbackAction;
        private static UtinniCore.Delegates.Action_ onPositionChangedCallbackAction;
        private static UtinniCore.Delegates.Action_ onRotationChangedCallbackAction;
        public static void Initialize()
        {
            onEnabledCallbackAction = OnEnabledCallback; // Storing this in a variable is somehow needed to prevent corruption on WinForms resize. Very odd bug that I still don't fully understand.
            onDisabledCallbackAction = OnDisabledCallback;
            onPositionChangedCallbackAction = OnPositionChangedCallback;
            onRotationChangedCallbackAction = OnRotationChangedCallback;

            UtinniCore.ImguiGizmo.imgui_impl.AddOnEnabledCallback(onEnabledCallbackAction);
            UtinniCore.ImguiGizmo.imgui_impl.AddOnDisabledCallback(onDisabledCallbackAction);
            UtinniCore.ImguiGizmo.imgui_impl.AddOnPositionChangedCallback(onPositionChangedCallbackAction);
            UtinniCore.ImguiGizmo.imgui_impl.AddOnRotationChangedCallback(onRotationChangedCallbackAction);
        }

        // --- OnEnabled callback ---

        public static int SubscribeOnEnabled(Action call)
        {
            lock (onEnabledLock)
            {
                int id = s_nextOnEnabledId++;
                onEnabledSubscribers[id] = call;
                return id;
            }
        }

        public static bool UnsubscribeOnEnabled(int handle)
        {
            if (handle == 0)
            {
                return false;
            }
            lock (onEnabledLock)
            {
                return onEnabledSubscribers.Remove(handle);
            }
        }

        public static void AddOnEnabledCallback(Action call)
        {
            SubscribeOnEnabled(call);
        }

        // Phase 3 R-A: previously missing — completes the Add/Remove pair (best-effort
        // legacy lookup; new code uses Unsubscribe(handle)).
        public static void RemoveOnEnabledCallback(Action call)
        {
            RemoveByDelegate(onEnabledSubscribers, onEnabledLock, call);
        }

        // --- OnDisabled callback ---

        public static int SubscribeOnDisabled(Action call)
        {
            lock (onDisabledLock)
            {
                int id = s_nextOnDisabledId++;
                onDisabledSubscribers[id] = call;
                return id;
            }
        }

        public static bool UnsubscribeOnDisabled(int handle)
        {
            if (handle == 0)
            {
                return false;
            }
            lock (onDisabledLock)
            {
                return onDisabledSubscribers.Remove(handle);
            }
        }

        public static void AddOnDisabledCallback(Action call)
        {
            SubscribeOnDisabled(call);
        }

        // Phase 3 R-A: previously missing — completes the Add/Remove pair.
        public static void RemoveOnDisabledCallback(Action call)
        {
            RemoveByDelegate(onDisabledSubscribers, onDisabledLock, call);
        }

        // --- OnPositionChanged callback ---

        public static int SubscribeOnPositionChanged(Action call)
        {
            lock (onPositionChangedLock)
            {
                int id = s_nextOnPositionChangedId++;
                onPositionChangedSubscribers[id] = call;
                return id;
            }
        }

        public static bool UnsubscribeOnPositionChanged(int handle)
        {
            if (handle == 0)
            {
                return false;
            }
            lock (onPositionChangedLock)
            {
                return onPositionChangedSubscribers.Remove(handle);
            }
        }

        public static void AddOnPositionChangedCallback(Action call)
        {
            SubscribeOnPositionChanged(call);
        }

        public static void RemoveOnPositionChangedCallback(Action call)
        {
            RemoveByDelegate(onPositionChangedSubscribers, onPositionChangedLock, call);
        }

        // --- OnRotationChanged callback ---

        public static int SubscribeOnRotationChanged(Action call)
        {
            lock (onRotationChangedLock)
            {
                int id = s_nextOnRotationChangedId++;
                onRotationChangedSubscribers[id] = call;
                return id;
            }
        }

        public static bool UnsubscribeOnRotationChanged(int handle)
        {
            if (handle == 0)
            {
                return false;
            }
            lock (onRotationChangedLock)
            {
                return onRotationChangedSubscribers.Remove(handle);
            }
        }

        public static void AddOnRotationChangedCallback(Action call)
        {
            SubscribeOnRotationChanged(call);
        }

        public static void RemoveOnRotationChangedCallback(Action call)
        {
            RemoveByDelegate(onRotationChangedSubscribers, onRotationChangedLock, call);
        }

        // --- Dispatch sites (R-H snapshot per D-12) ---

        private static void OnEnabledCallback()
        {
            DispatchSnapshot(onEnabledSubscribers, onEnabledLock);
        }

        private static void OnDisabledCallback()
        {
            DispatchSnapshot(onDisabledSubscribers, onDisabledLock);
        }

        private static void OnPositionChangedCallback()
        {
            DispatchSnapshot(onPositionChangedSubscribers, onPositionChangedLock);
        }

        private static void OnRotationChangedCallback()
        {
            DispatchSnapshot(onRotationChangedSubscribers, onRotationChangedLock);
        }

        private static void DispatchSnapshot(Dictionary<int, Action> registry, object syncRoot)
        {
            Action[] snapshot;
            lock (syncRoot)
            {
                snapshot = registry.Values.ToArray();
            }
            foreach (Action callback in snapshot)
            {
                callback();
            }
        }

        private static void RemoveByDelegate(Dictionary<int, Action> registry, object syncRoot, Action call)
        {
            // Best-effort legacy lookup: scan for first matching delegate. New code
            // should use Unsubscribe*(handle) directly.
            lock (syncRoot)
            {
                int matchId = 0;
                foreach (var kvp in registry)
                {
                    if (Equals(kvp.Value, call))
                    {
                        matchId = kvp.Key;
                        break;
                    }
                }
                if (matchId != 0)
                {
                    registry.Remove(matchId);
                }
            }
        }
    }
}
