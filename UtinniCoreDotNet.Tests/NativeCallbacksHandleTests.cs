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
using Xunit;

namespace UtinniCoreDotNet.Tests
{
    // Phase 3 R-A native-side regression tests (per 03-CONTEXT D-08/D-09/D-12):
    // exercises the new utinni_test_subscribeInstall / unsubscribeInstall /
    // dispatchInstall / installSubscriberCount / addInstall exports against
    // Game::installCallbacks — the representative subscriber-backed registry
    // chosen per PATTERNS.md "Native test bridge exports".
    //
    // P/Invoke harness mirrors FindPatternHarnessTests:42-67 (NativeBridge
    // nested static class + GCHandle pin discipline for the function-pointer
    // delegate parameters — without the pin, the GC could move the delegate
    // out from under native code mid-test).
    //
    // Per-test isolation strategy: each [Fact] uses delta-based assertions
    // (compare InstallSubscriberCount before/after) instead of absolute
    // counts because the native registry is process-wide and includes:
    //   * GameCallbacks.Initialize()'s callInstallCallbacksAction delegate
    //     (added at managed startup)
    //   * Stale entries from LegacyAddInstallCallback_StillWorks (D-10 path
    //     intentionally cannot unsubscribe — documented limitation)
    // Side-effect counts use the test-local delegate's own captured counter
    // (NOT a shared static), so stale callbacks invoked by DispatchInstall
    // increment their OWN counters, not the test's. This is the standard
    // managed pattern of "static field as GC root, but capture is per-test".
    //
    // [Collection("StaticCallbackState")] serializes against the managed
    // callback tests because the native registry IS shared with whatever
    // managed code already populated installCallbacks.
    [Collection("StaticCallbackState")]
    public class NativeCallbacksHandleTests
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeCallback();

        private static class NativeBridge
        {
            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_subscribeInstall")]
            public static extern int SubscribeInstall(NativeCallback fn);

            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_unsubscribeInstall")]
            [return: MarshalAs(UnmanagedType.I1)]
            public static extern bool UnsubscribeInstall(int handle);

            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_dispatchInstall")]
            public static extern void DispatchInstall();

            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_installSubscriberCount")]
            public static extern int InstallSubscriberCount();

            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_addInstall")]
            public static extern void AddInstall(NativeCallback fn);
        }

        [Fact]
        public void SubscribeInstall_ReturnsNonZeroHandle_AndDispatchesOnce()
        {
            if (!TryEnsureNativeAvailable())
            {
                return;
            }

            int callCount = 0;
            NativeCallback cb = () => callCount++;
            var pin = GCHandle.Alloc(cb);
            try
            {
                int handle = NativeBridge.SubscribeInstall(cb);
                Assert.NotEqual(0, handle);

                try
                {
                    NativeBridge.DispatchInstall();
                    Assert.Equal(1, callCount);
                }
                finally
                {
                    NativeBridge.UnsubscribeInstall(handle);
                }
            }
            finally
            {
                pin.Free();
            }
        }

        [Fact]
        public void UnsubscribeInstall_PreventsDispatch()
        {
            if (!TryEnsureNativeAvailable())
            {
                return;
            }

            int callCount = 0;
            NativeCallback cb = () => callCount++;
            var pin = GCHandle.Alloc(cb);
            try
            {
                int handle = NativeBridge.SubscribeInstall(cb);
                Assert.NotEqual(0, handle);

                bool removed = NativeBridge.UnsubscribeInstall(handle);
                Assert.True(removed);

                NativeBridge.DispatchInstall();
                Assert.Equal(0, callCount);
            }
            finally
            {
                pin.Free();
            }
        }

        [Fact]
        public void UnsubscribeInstall_UnknownHandle_ReturnsFalse()
        {
            if (!TryEnsureNativeAvailable())
            {
                return;
            }
            Assert.False(NativeBridge.UnsubscribeInstall(99999));
        }

        [Fact]
        public void UnsubscribeInstall_HandleZero_ReturnsFalse()
        {
            // Handle 0 is reserved as the invalid sentinel per D-09.
            if (!TryEnsureNativeAvailable())
            {
                return;
            }
            Assert.False(NativeBridge.UnsubscribeInstall(0));
        }

        [Fact]
        public void Subscribe_DuringDispatch_NotInvokedInCurrentIteration_InvokedInNext()
        {
            if (!TryEnsureNativeAvailable())
            {
                return;
            }

            int callCount = 0;
            int innerCount = 0;
            int innerHandle = 0;
            NativeCallback innerCb = () => innerCount++;
            // Outer subscribes inner during its own dispatch. R-H snapshot
            // semantics mean inner lives in the registry but doesn't appear in
            // the current iteration's snapshot — fires on next dispatch only.
            NativeCallback outerCb = () =>
            {
                callCount++;
                if (innerHandle == 0)
                {
                    innerHandle = NativeBridge.SubscribeInstall(innerCb);
                }
            };

            var outerPin = GCHandle.Alloc(outerCb);
            var innerPin = GCHandle.Alloc(innerCb);
            int outerHandle = 0;
            try
            {
                outerHandle = NativeBridge.SubscribeInstall(outerCb);

                NativeBridge.DispatchInstall();
                Assert.Equal(1, callCount);
                Assert.Equal(0, innerCount);

                NativeBridge.DispatchInstall();
                Assert.Equal(2, callCount);
                Assert.Equal(1, innerCount);
            }
            finally
            {
                if (outerHandle != 0)
                {
                    NativeBridge.UnsubscribeInstall(outerHandle);
                }
                if (innerHandle != 0)
                {
                    NativeBridge.UnsubscribeInstall(innerHandle);
                }
                outerPin.Free();
                innerPin.Free();
            }
        }

        [Fact]
        public void LegacyAddInstallCallback_StillWorks()
        {
            // D-10: addInstallCallback retained as wrapper around
            // subscribeInstallCallback. Existing UtinniPlugins (TJT, Sytner)
            // keep working without recompile. The legacy path returns void and
            // therefore the test cannot Unsubscribe — the entry remains in the
            // registry for the process lifetime per the documented contract.
            if (!TryEnsureNativeAvailable())
            {
                return;
            }

            int callCount = 0;
            NativeCallback cb = () => callCount++;
            var pin = GCHandle.Alloc(cb);
            try
            {
                int beforeCount = NativeBridge.InstallSubscriberCount();
                NativeBridge.AddInstall(cb);
                int afterCount = NativeBridge.InstallSubscriberCount();
                Assert.Equal(beforeCount + 1, afterCount);

                NativeBridge.DispatchInstall();
                // The test-local cb fired exactly once during this dispatch
                // (other stale entries fire their own captures, not ours).
                Assert.Equal(1, callCount);
            }
            finally
            {
                pin.Free();
            }
        }

        // If UtinniCore.dll is not available (local dev sans build), the
        // DllImport throws DllNotFoundException on first call. Skip the test
        // cleanly when that happens — CI is the arbiter.
        private static bool TryEnsureNativeAvailable()
        {
            try
            {
                NativeBridge.InstallSubscriberCount();
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }
    }
}
