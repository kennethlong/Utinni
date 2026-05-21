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
using System.Diagnostics;
using System.Linq;
using UtinniCore.Utinni.Log;
using static UtinniCore.Utinni.utinni;

namespace UtinniCoreDotNet.Utility
{
    public static class Log
    {
        private static bool writeClassName;
        private static bool writeFunctionName;

        // Phase 3 R-A managed-side (per 03-CONTEXT D-08/D-09): handle-based
        // Subscribe/Unsubscribe over a Dictionary<int, Action<string>>. Per-type
        // signature is Action<string> rather than Action because the log sink
        // produces a string message payload. Handle 0 is reserved as the invalid
        // sentinel per D-09.
        private static readonly Dictionary<int, Action<string>> outputSinkSubscribers = new Dictionary<int, Action<string>>();
        private static int s_nextOutputSinkId = 1; // 0 reserved as invalid handle per D-09
        private static readonly object outputSinkLock = new object();

        private static UtinniCore.Delegates.Action_string outputSinkCallbacksAction;
        public static void Setup()
        {
            writeClassName = GetConfig().GetBool("Log", "writeClassName");
            writeFunctionName = GetConfig().GetBool("Log", "writeFunctionName");

            outputSinkCallbacksAction = CallOutputSinkCallbacks;
            log.AddOutputSinkCallback(outputSinkCallbacksAction);
        }

        private static string FormatText(string text)
        {
            if (writeClassName)
            {
                var method = new StackTrace().GetFrame(2).GetMethod();

                if (writeFunctionName)
                {
                    return "[" + method.ReflectedType.Name + "][" + method.Name + "] " + text;
                }
                else
                {
                    return "[" + method.ReflectedType.Name + "] " + text;
                }
            }
            else
            {
                return text;
            }
        }

        public static void Critical(string text)
        {
            log.Critical(FormatText(text));
        }

        public static void Debug(string text)
        {
            log.Debug(FormatText(text));
        }

        public static void Error(string text)
        {
            log.Error(FormatText(text));
        }

        public static void Info(string text)
        {
            log.Info(FormatText(text));
        }

        public static void Warning(string text)
        {
            log.Warning(FormatText(text));
        }

        public static void CriticalSimple(string text)
        {
            log.Critical(text);
        }

        public static void DebugSimple(string text)
        {
            log.Debug(text);
        }

        public static void ErrorSimple(string text)
        {
            log.Error(text);
        }

        public static void InfoSimple(string text)
        {
            log.Info(text);
        }

        public static void WarningSimple(string text)
        {
            log.Warning(text);
        }

        // Phase 3 R-A managed-side (per 03-CONTEXT D-08/D-09/D-10): primary API.
        // SubscribeOutputSink returns an opaque int handle; pair with
        // UnsubscribeOutputSink(handle) for symmetric Add/Remove.
        public static int SubscribeOutputSink(Action<string> call)
        {
            lock (outputSinkLock)
            {
                int id = s_nextOutputSinkId++;
                outputSinkSubscribers[id] = call;
                return id;
            }
        }

        public static bool UnsubscribeOutputSink(int handle)
        {
            // Handle 0 is reserved as invalid sentinel (D-09).
            if (handle == 0)
            {
                return false;
            }
            lock (outputSinkLock)
            {
                return outputSinkSubscribers.Remove(handle);
            }
        }

        // Phase 3 R-A typo-fix: AddOutputSinkCallback / RemoveOutputSinkCallback
        // are the correctly-spelled primary legacy API. They wrap Subscribe per D-10.
        public static void AddOutputSinkCallback(Action<string> call)
        {
            SubscribeOutputSink(call);
        }

        public static void RemoveOutputSinkCallback(Action<string> call)
        {
            // Best-effort legacy lookup: scan for first matching delegate. New code
            // should use UnsubscribeOutputSink(handle).
            lock (outputSinkLock)
            {
                int matchId = 0;
                foreach (var kvp in outputSinkSubscribers)
                {
                    if (Equals(kvp.Value, call))
                    {
                        matchId = kvp.Key;
                        break;
                    }
                }
                if (matchId != 0)
                {
                    outputSinkSubscribers.Remove(matchId);
                }
            }
        }

        // Phase 3 R-A typo-aliases: the original API misspelled "Output" as "Ouput".
        // The aliases below preserve source-compat with any existing UtinniPlugin
        // caller while the correctly-spelled versions above are the new primary API.
        public static void AddOuputSinkCallback(Action<string> call)
        {
            AddOutputSinkCallback(call);
        }

        public static void RemoveOuputSinkCallback(Action<string> call)
        {
            RemoveOutputSinkCallback(call);
        }

        // R-H snapshot iteration per D-12: copy subscribers under lock, iterate outside.
        private static void CallOutputSinkCallbacks(string msg)
        {
            Action<string>[] snapshot;
            lock (outputSinkLock)
            {
                snapshot = outputSinkSubscribers.Values.ToArray();
            }
            foreach (Action<string> callback in snapshot)
            {
                callback(msg);
            }
        }
    }
}
