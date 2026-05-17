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
using System.Runtime.InteropServices;
using UtinniCoreDotNet.Callbacks;
using Xunit;

namespace UtinniCoreDotNet.Tests
{
    /// <summary>
    /// C-16 GC-survival regression test.
    /// GameCallbacks uses private static fields as GC roots for delegates passed to native.
    /// This test verifies that a callback added via AddInstallCallback survives GC.Collect()
    /// because the installCallbacks SynchronizedCollection (a static field on GameCallbacks)
    /// holds a reference to it.
    ///
    /// GC-survival is a purely managed-side property — invoked via reflection on
    /// CallInstallCallbacks regardless of native availability. The P/Invoke to
    /// Game::triggerInstallCallbacks is a separate "doesn't AV at the native boundary"
    /// probe; it is allowed to be unavailable (no UtinniCore.dll, local dev environment)
    /// or to be a no-op in the test process (no native callback list populated).
    /// </summary>
    public class GameCallbacksTests
    {
        private static class NativeBridge
        {
            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_triggerInstallCallbacks")]
            public static extern void Utinni_TriggerInstallCallbacks();
        }

        [Fact]
        public void RegisterCallback_ForceGCCollect_CallbackStillFiresWithoutAV()
        {
            // Arrange: add a callback to the managed installCallbacks collection
            bool fired = false;
            // Local reference — will be set to null to drop the local reference
            Action callback = () => fired = true;
            GameCallbacks.AddInstallCallback(callback);

            // Drop the local reference so only the static SynchronizedCollection holds it
            callback = null;

            // Force a full GC collection — the delegate should survive because
            // GameCallbacks.installCallbacks (static SynchronizedCollection) is the GC root
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);

            // Probe 1: native trigger must not AV (proves the native side is callable and
            // does not crash from any pinned-but-collected delegate hazard). The native
            // function iterates a NATIVE callback list, not the managed installCallbacks —
            // so it is not expected to fire our managed delegate. We just want "no AV".
            // If UtinniCore.dll is unavailable (local dev), skip the native probe.
            Exception ex = Record.Exception(() => NativeBridge.Utinni_TriggerInstallCallbacks());
            if (!(ex is DllNotFoundException || ex is EntryPointNotFoundException))
            {
                Assert.Null(ex);
            }

            // Probe 2: managed-side GC-survival — invoke the managed iteration path
            // via reflection. This is what actually proves the installCallbacks static
            // field kept the delegate alive across GC.Collect.
            InvokeCallbacksViaManagedReflection();

            Assert.True(fired, "Callback should still be alive after GC.Collect (static field is GC root).");
        }

        /// <summary>
        /// Fallback invocation for environments where UtinniCore.dll is not available.
        /// Invokes CallInstallCallbacks via reflection to verify GC-survival on managed side.
        /// </summary>
        private static void InvokeCallbacksViaManagedReflection()
        {
            var method = typeof(GameCallbacks).GetMethod(
                "CallInstallCallbacks",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            if (method != null)
            {
                method.Invoke(null, null);
            }
        }
    }
}
