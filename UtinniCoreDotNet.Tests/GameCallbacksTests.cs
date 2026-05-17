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
    /// The P/Invoke path (utinni_triggerInstallCallbacks -> Game::triggerInstallCallbacks)
    /// requires UtinniCore.dll; if the DLL is not loaded the test falls back to verifying
    /// managed-side GC-survival via CallInstallCallbacks reflection invocation.
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

            // Act: trigger all install callbacks via the P/Invoke test export
            // (requires UtinniCore.dll to be loaded; on CI it is; locally it may not be)
            Exception ex = Record.Exception(() => NativeBridge.Utinni_TriggerInstallCallbacks());

            // If UtinniCore.dll is not available (local dev without DXSDK build),
            // the P/Invoke will throw a DllNotFoundException. In that case, verify
            // via managed-side invocation using reflection on the internal collection.
            if (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                // Managed-side verification: the callback is still in the collection
                // (i.e. has not been GC'd). Invoke it manually.
                fired = false;
                InvokeCallbacksViaManagedReflection();
            }
            else
            {
                // P/Invoke succeeded; no AV = the GC root preserved the delegate
                Assert.Null(ex);
            }

            // In both paths the callback must have fired
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
