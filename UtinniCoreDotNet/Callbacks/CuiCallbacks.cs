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
    // Phase 3 R-A + R-H managed-side: onReceiveSystemMessage is subscriber-backed
    // with Action<string> payload. Per-callback Dictionary<int, Action<string>>
    // + lock per D-09; snapshot dispatch per D-12. Pre-Phase-3 this class only
    // had Add (no Remove); Phase 3 completes the pair plus the canonical
    // Subscribe/Unsubscribe API.
    public static class CuiCallbacks
    {
        private static readonly Dictionary<int, Action<string>> onReceiveSystemMessageSubscribers = new Dictionary<int, Action<string>>();
        private static int s_nextOnReceiveSystemMessageId = 1; // 0 reserved as invalid handle per D-09
        private static readonly object onReceiveSystemMessageLock = new object();

        private static UtinniCore.Delegates.Action_string dequeueOnReceiveSystemMessageAction;
        public static void Initialize()
        {
            dequeueOnReceiveSystemMessageAction = DequeueOnReceiveSystemMessageCallbacks; // Storing this in a variable is somehow needed to prevent corruption on WinForms resize. Very odd bug that I still don't fully understand.
            UtinniCore.Utinni.SystemMessageManager.AddReceiveMessageCallback(dequeueOnReceiveSystemMessageAction);
        }

        public static int SubscribeOnReceiveSystemMessage(Action<string> call)
        {
            lock (onReceiveSystemMessageLock)
            {
                int id = s_nextOnReceiveSystemMessageId++;
                if (id == 0) { id = s_nextOnReceiveSystemMessageId++; } // WR-04 skip-zero
                onReceiveSystemMessageSubscribers[id] = call;
                return id;
            }
        }

        public static bool UnsubscribeOnReceiveSystemMessage(int handle)
        {
            if (handle == 0)
            {
                return false;
            }
            lock (onReceiveSystemMessageLock)
            {
                return onReceiveSystemMessageSubscribers.Remove(handle);
            }
        }

        // D-10: legacy Add retained as wrapper (return value discarded).
        public static void AddOnReceiveSystemMessageCallback(Action<string> call)
        {
            SubscribeOnReceiveSystemMessage(call);
        }

        // Phase 3 R-A: Remove pair previously missing; added per PATTERNS.md (line 427)
        // as a best-effort legacy lookup. New code should use Unsubscribe(handle).
        public static void RemoveOnReceiveSystemMessageCallback(Action<string> call)
        {
            lock (onReceiveSystemMessageLock)
            {
                int matchId = 0;
                foreach (var kvp in onReceiveSystemMessageSubscribers)
                {
                    if (Equals(kvp.Value, call))
                    {
                        matchId = kvp.Key;
                        break;
                    }
                }
                if (matchId != 0)
                {
                    onReceiveSystemMessageSubscribers.Remove(matchId);
                }
            }
        }

        // R-H snapshot iteration per D-12.
        private static void DequeueOnReceiveSystemMessageCallbacks(string msg)
        {
            Action<string>[] snapshot;
            lock (onReceiveSystemMessageLock)
            {
                snapshot = onReceiveSystemMessageSubscribers.Values.ToArray();
            }
            foreach (Action<string> callback in snapshot)
            {
                callback(msg);
            }
        }
    }
}
