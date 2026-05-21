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
using System.Reflection;
using Xunit;
using UtinniCoreDotNet.Callbacks;
using UtinniCoreDotNet.Utility;

namespace UtinniCoreDotNet.Tests
{
    // Phase 3 R-A managed-side regression tests (per 03-CONTEXT D-08/D-09/D-10):
    // every *Callbacks class plus Log.cs must expose handle-based
    // Subscribe/Unsubscribe + retain the legacy Add* wrapper. Reflection-based
    // private-state probe (mirrors GroundSceneCallbacksTests:45-59) verifies the
    // underlying Dictionary<int, Action> shape.
    //
    // [Collection("StaticCallbackState")] serializes against other tests that
    // touch the same static registries.
    [Collection("StaticCallbackState")]
    public class CallbacksSubscribeUnsubscribeTests
    {
        // --- Reflection probe helpers ---

        private static Dictionary<int, Action> GetActionSubscribers(Type type, string fieldName)
        {
            var f = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(f);
            return (Dictionary<int, Action>)f.GetValue(null);
        }

        private static Dictionary<int, Action<string>> GetActionStringSubscribers(Type type, string fieldName)
        {
            var f = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(f);
            return (Dictionary<int, Action<string>>)f.GetValue(null);
        }

        private static void InvokePrivate(Type type, string methodName, params object[] args)
        {
            var m = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(m);
            m.Invoke(null, args);
        }

        // --- GameCallbacks.Install Subscribe/Unsubscribe tests ---

        [Fact]
        public void SubscribeInstall_ReturnsNonZeroHandle()
        {
            int handle = GameCallbacks.SubscribeInstall(() => { });
            try
            {
                Assert.NotEqual(0, handle);
            }
            finally
            {
                GameCallbacks.UnsubscribeInstall(handle);
            }
        }

        [Fact]
        public void Unsubscribe_RemovesEntry_NotInvokedOnDispatch()
        {
            // Capture starting subscriber count (other tests may run first; we only
            // care about delta, not absolute count).
            var registry = GetActionSubscribers(typeof(GameCallbacks), "installSubscribers");
            int countBefore = registry.Count;

            int counter = 0;
            int handle = GameCallbacks.SubscribeInstall(() => counter++);
            Assert.Equal(countBefore + 1, registry.Count);

            bool removed = GameCallbacks.UnsubscribeInstall(handle);
            Assert.True(removed);
            Assert.Equal(countBefore, registry.Count);

            // Invoke the private dispatch path; counter must stay 0.
            InvokePrivate(typeof(GameCallbacks), "CallInstallCallbacks");
            Assert.Equal(0, counter);
        }

        [Fact]
        public void Unsubscribe_UnknownHandle_ReturnsFalse()
        {
            // 99999 is far outside any plausibly-allocated handle range; even if
            // prior tests subscribed many callbacks, this won't collide.
            Assert.False(GameCallbacks.UnsubscribeInstall(99999));
        }

        [Fact]
        public void Unsubscribe_HandleZero_ReturnsFalse()
        {
            // Handle 0 is reserved as the invalid sentinel per D-09.
            Assert.False(GameCallbacks.UnsubscribeInstall(0));
        }

        [Fact]
        public void LegacyAddCallback_StillWorks()
        {
            // D-10: AddInstallCallback is the legacy wrapper around Subscribe.
            // Existing UtinniPlugins (TJT, Sytner) keep working without recompile.
            int counter = 0;
            Action cb = () => counter++;
            GameCallbacks.AddInstallCallback(cb);
            try
            {
                InvokePrivate(typeof(GameCallbacks), "CallInstallCallbacks");
                Assert.Equal(1, counter);
            }
            finally
            {
                GameCallbacks.RemoveInstallCallback(cb);
            }
        }

        // --- Per-class smoke tests: every *Callbacks class exposes Subscribe* / Unsubscribe* ---

        [Theory]
        [InlineData(typeof(GameCallbacks), "SubscribeInstall", "UnsubscribeInstall")]
        [InlineData(typeof(GameCallbacks), "SubscribeSetupScene", "UnsubscribeSetupScene")]
        [InlineData(typeof(GameCallbacks), "SubscribeCleanupScene", "UnsubscribeCleanupScene")]
        [InlineData(typeof(GroundSceneCallbacks), "SubscribeCameraChange", "UnsubscribeCameraChange")]
        [InlineData(typeof(ObjectCallbacks), "SubscribeOnTarget", "UnsubscribeOnTarget")]
        [InlineData(typeof(ImGuiCallbacks), "SubscribeOnEnabled", "UnsubscribeOnEnabled")]
        [InlineData(typeof(ImGuiCallbacks), "SubscribeOnDisabled", "UnsubscribeOnDisabled")]
        [InlineData(typeof(ImGuiCallbacks), "SubscribeOnPositionChanged", "UnsubscribeOnPositionChanged")]
        [InlineData(typeof(ImGuiCallbacks), "SubscribeOnRotationChanged", "UnsubscribeOnRotationChanged")]
        public void EveryCallbackClass_HasSubscribeAndUnsubscribePair_ReturningHandleAndBool(
            Type type, string subscribeName, string unsubscribeName)
        {
            var subscribe = type.GetMethod(subscribeName, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(subscribe);
            Assert.Equal(typeof(int), subscribe.ReturnType);
            var subParams = subscribe.GetParameters();
            Assert.Single(subParams);
            Assert.Equal(typeof(Action), subParams[0].ParameterType);

            var unsubscribe = type.GetMethod(unsubscribeName, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(unsubscribe);
            Assert.Equal(typeof(bool), unsubscribe.ReturnType);
            var unsubParams = unsubscribe.GetParameters();
            Assert.Single(unsubParams);
            Assert.Equal(typeof(int), unsubParams[0].ParameterType);

            // Verify the round-trip: subscribe returns non-zero, unsubscribe returns true.
            int handle = (int)subscribe.Invoke(null, new object[] { (Action)(() => { }) });
            Assert.NotEqual(0, handle);
            bool removed = (bool)unsubscribe.Invoke(null, new object[] { handle });
            Assert.True(removed);
        }

        [Fact]
        public void CuiCallbacks_HasSubscribeAndUnsubscribePair_ForActionString()
        {
            // CuiCallbacks uses Action<string>, so the [Theory] above can't cover it
            // (the InlineData parameter type would mismatch). Verified separately.
            var subscribe = typeof(CuiCallbacks).GetMethod(
                "SubscribeOnReceiveSystemMessage", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(subscribe);
            Assert.Equal(typeof(int), subscribe.ReturnType);

            var unsubscribe = typeof(CuiCallbacks).GetMethod(
                "UnsubscribeOnReceiveSystemMessage", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(unsubscribe);
            Assert.Equal(typeof(bool), unsubscribe.ReturnType);

            int handle = (int)subscribe.Invoke(null, new object[] { (Action<string>)(_ => { }) });
            Assert.NotEqual(0, handle);
            bool removed = (bool)unsubscribe.Invoke(null, new object[] { handle });
            Assert.True(removed);
        }

        [Fact]
        public void Log_SubscribeOutputSink_ReturnsNonZeroHandle_Unsubscribe_RemovesEntry()
        {
            int handle = Log.SubscribeOutputSink(_ => { });
            try
            {
                Assert.NotEqual(0, handle);
                var registry = GetActionStringSubscribers(typeof(Log), "outputSinkSubscribers");
                Assert.True(registry.ContainsKey(handle));
            }
            finally
            {
                bool removed = Log.UnsubscribeOutputSink(handle);
                Assert.True(removed);
            }
        }
    }

    // R-A typo-fix tests (per 03-CONTEXT D-08 + PATTERNS.md §"R-A typo-fix pattern"):
    // the original API misspelled "Output" as "Ouput"; Phase 3 adds the correctly-
    // spelled twin and retains the misspelled aliases for source-compat.
    [Collection("StaticCallbackState")]
    public class LogTypoFixTests
    {
        [Fact]
        public void AddOutputSinkCallback_CorrectlySpelled_Exists()
        {
            var m = typeof(Log).GetMethod("AddOutputSinkCallback", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(m);
            Assert.Equal(typeof(void), m.ReturnType);
            var p = m.GetParameters();
            Assert.Single(p);
            Assert.Equal(typeof(Action<string>), p[0].ParameterType);
        }

        [Fact]
        public void AddOuputSinkCallback_LegacyAlias_StillCallable()
        {
            // Source-compat: the misspelled wrapper still exists and forwards to
            // the correctly-spelled method.
            var legacy = typeof(Log).GetMethod("AddOuputSinkCallback", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(legacy);

            var removeLegacy = typeof(Log).GetMethod("RemoveOuputSinkCallback", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(removeLegacy);

            // Functional check: legacy add then legacy remove should be a no-op net
            // effect (the entry is added then removed via the typo'd wrappers,
            // which forward to the correctly-spelled methods).
            Action<string> cb = _ => { };
            legacy.Invoke(null, new object[] { cb });
            removeLegacy.Invoke(null, new object[] { cb });
            // Did not throw — that is the success condition; the internal registry
            // size is exercised in the Subscribe/Unsubscribe test above.
        }

        [Fact]
        public void RemoveOutputSinkCallback_CorrectlySpelled_Exists()
        {
            var m = typeof(Log).GetMethod("RemoveOutputSinkCallback", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(m);
        }
    }
}
