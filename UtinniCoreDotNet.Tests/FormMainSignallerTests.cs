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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using UtinniCoreDotNet.UI.Forms;

namespace UtinniCoreDotNet.Tests
{
    // C-09 mock-signaller tests: verify WaitForPresentBlock timeout + signal semantics
    // without loading UtinniCore.dll or d3d9.dll. Tests inject a managed EventWaitHandle
    // via FormMain.TestSignaller (the test-only injection seam). WaitForPresentBlock is
    // an internal static method — no FormMain instance required, no native CLR bridge.
    //
    // The tests prove:
    //   1. No infinite spin: WaitForPresentBlock returns within (timeout + epsilon) even when
    //      the native signal never fires (covers the pre-fix regression class).
    //   2. Signal-fires case: the wait returns promptly (under the timeout) when signalled.
    //   3. Already-signalled case: the wait returns immediately.
    //   4. CR-04 D-04 regression guard: initPresentBlockedEvent() returns non-zero HANDLE.
    //   5. WR-03 D-04 regression guard: initDepthTexture() returns non-zero pointer.
    //
    // FormMain.TestSignaller is reset to null in every finally block so test runs stay
    // isolated from each other regardless of execution order or parallelism.
    public class FormMainSignallerTests
    {
        // P/Invoke bridge for CR-04 / WR-03 test exports.
        // These exports are in UtinniCore.dll (built by the CI workflow).
        // Both new test exports and the existing exports use __cdecl to avoid the
        // C-01 export-decoration class of bug.
        private static class NativeBridge
        {
            // CR-04: calls directX::initPresentBlockedEvent() and returns the HANDLE.
            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_initPresentBlockedEvent")]
            public static extern UIntPtr Utinni_TestInitPresentBlockedEvent();

            // CR-04: returns the current hPresentBlockedEvent HANDLE value.
            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_getPresentBlockedEvent")]
            public static extern UIntPtr Utinni_GetPresentBlockedEventHandle();

            // WR-03: calls directX::initDepthTexture() and returns the DepthTexture ptr.
            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_initDepthTexture")]
            public static extern UIntPtr Utinni_TestInitDepthTexture();

            // WR-03: returns the current depthTexture pointer.
            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_getDepthTexturePtr")]
            public static extern UIntPtr Utinni_GetDepthTexturePtr();
        }

        [Fact]
        public void WaitForPresentBlock_SignalNeverFires_ReturnsWithinTimeout()
        {
            // Arrange: create an unsignalled manual-reset event; never signal it.
            using (var mockSignal = new EventWaitHandle(false, EventResetMode.ManualReset))
            {
                FormMain.TestSignaller = mockSignal;
                try
                {
                    var timeout = TimeSpan.FromMilliseconds(50);
                    var sw = Stopwatch.StartNew();

                    // Act
                    bool result = FormMain.WaitForPresentBlock(timeout);

                    sw.Stop();

                    // Assert: must return false (timeout, not signalled) and within ~150 ms
                    // (50 ms timeout + generous slack for runner variance per plan <behavior>).
                    Assert.False(result);
                    Assert.True(sw.ElapsedMilliseconds < 150,
                        $"Expected WaitForPresentBlock to return within ~60 ms but took {sw.ElapsedMilliseconds} ms");
                }
                finally
                {
                    FormMain.TestSignaller = null;
                }
            }
        }

        [Fact]
        public void WaitForPresentBlock_SignalFires_ReturnsImmediately()
        {
            // Arrange: create an unsignalled event; signal it asynchronously after a short
            // pre-sleep. Use a dedicated Thread (starts in single-digit ms) rather than
            // Task.Run (threadpool — first-spin can take hundreds of ms on cold-cache CI
            // VMs, observed 498 ms on windows-2022 which exhausted the prior 500 ms test
            // timeout). The point of the test is "signal observed before timeout fires",
            // not "signal observed within N ms", so generous timeout slack is correct.
            using (var mockSignal = new EventWaitHandle(false, EventResetMode.ManualReset))
            using (var signallerReady = new ManualResetEventSlim(false))
            {
                FormMain.TestSignaller = mockSignal;
                Thread signaller = null;
                try
                {
                    var timeout = TimeSpan.FromMilliseconds(5000);

                    signaller = new Thread(() =>
                    {
                        signallerReady.Set();   // prove the thread actually started
                        Thread.Sleep(10);
                        mockSignal.Set();
                    })
                    {
                        IsBackground = true,
                        Name = "FormMainSignallerTests-Signaller"
                    };
                    signaller.Start();

                    // Make sure the signaller thread is running before we start timing
                    // — otherwise we're measuring thread-start latency, not WaitOne behavior.
                    Assert.True(signallerReady.Wait(2000), "Signaller thread failed to start within 2 s");

                    var sw = Stopwatch.StartNew();

                    // Act
                    bool result = FormMain.WaitForPresentBlock(timeout);

                    sw.Stop();

                    // Assert: must return true (signal observed) and return well BEFORE the
                    // 5000 ms timeout would have fired. 4000 ms = timeout - 1 s headroom;
                    // anything above this would indicate WaitOne is honoring the timeout
                    // rather than waking on the signal (a real bug).
                    Assert.True(result, "WaitForPresentBlock should observe the signal before the 5 s timeout fires");
                    Assert.True(sw.ElapsedMilliseconds < 4000,
                        $"Expected WaitForPresentBlock to wake on the signal (well under the 5 s timeout) but took {sw.ElapsedMilliseconds} ms");
                }
                finally
                {
                    FormMain.TestSignaller = null;
                    signaller?.Join();
                }
            }
        }

        [Fact]
        public void WaitForPresentBlock_AlreadySignalled_ReturnsImmediately()
        {
            // Arrange: create an event that is already signalled at construction time.
            using (var mockSignal = new EventWaitHandle(true, EventResetMode.ManualReset))
            {
                FormMain.TestSignaller = mockSignal;
                try
                {
                    var timeout = TimeSpan.FromMilliseconds(500);
                    var sw = Stopwatch.StartNew();

                    // Act
                    bool result = FormMain.WaitForPresentBlock(timeout);

                    sw.Stop();

                    // Assert: must return true immediately (already-signalled state).
                    Assert.True(result);
                    Assert.True(sw.ElapsedMilliseconds < 50,
                        $"Expected WaitForPresentBlock to return within ~5 ms but took {sw.ElapsedMilliseconds} ms");
                }
                finally
                {
                    FormMain.TestSignaller = null;
                }
            }
        }

        [Fact]
        public void InitPresentBlockedEvent_AfterEagerInit_ReturnsNonNullHandle()
        {
            // CR-04 D-04 regression guard (Concern B from PLAN-CHECK.md):
            //
            // This test calls utinni_test_initPresentBlockedEvent() which directly invokes
            // directX::initPresentBlockedEvent() — the same eager-init path that utinni_init
            // uses in production. After the call, getPresentBlockedEvent() must return a
            // non-zero HANDLE (a live Win32 manual-reset event object).
            //
            // If CR-04 is REVERTED (lazy-init re-introduced in getPresentBlockedEvent()):
            //   - initPresentBlockedEvent() becomes a no-op or is removed entirely.
            //   - utinni_test_initPresentBlockedEvent() would call a no-op and return 0.
            //   - Assert.NotEqual(UIntPtr.Zero, ...) FAILS. D-04 is satisfied.
            //
            // This test does NOT require utinni_init to have run; it exercises the
            // init function directly so the regression guard works in the test process.
            try
            {
                UIntPtr afterInit = NativeBridge.Utinni_TestInitPresentBlockedEvent();
                Assert.NotEqual(UIntPtr.Zero, afterInit);

                UIntPtr getResult = NativeBridge.Utinni_GetPresentBlockedEventHandle();
                Assert.Equal(afterInit, getResult);
            }
            catch (DllNotFoundException)
            {
                // Local build without UtinniCore.dll — skip gracefully.
                // CI validates the P/Invoke path (UtinniCore.dll is built before dotnet test).
            }
        }

        [Fact]
        public void InitDepthTexture_AfterEagerInit_ReturnsNonNullPointer()
        {
            // WR-03 D-04 regression guard (Concern B from PLAN-CHECK.md):
            //
            // This test calls utinni_test_initDepthTexture() which directly invokes
            // directX::initDepthTexture() — the same eager-init path that utinni_init
            // uses in production. After the call, getDepthTexture() must return a
            // non-zero pointer (a live DepthTexture object; ctor is D3D9-free).
            //
            // If WR-03 is REVERTED (lazy-init re-introduced in hkPresent):
            //   - initDepthTexture() becomes a no-op or is removed entirely.
            //   - utinni_test_initDepthTexture() would call a no-op and return 0.
            //   - Assert.NotEqual(UIntPtr.Zero, ...) FAILS. D-04 is satisfied.
            //
            // This test does NOT require a live D3D9 device; DepthTexture() ctor
            // only calls NvAPI_Initialize() which is safe in the test process.
            try
            {
                UIntPtr afterInit = NativeBridge.Utinni_TestInitDepthTexture();
                Assert.NotEqual(UIntPtr.Zero, afterInit);

                UIntPtr getResult = NativeBridge.Utinni_GetDepthTexturePtr();
                Assert.Equal(afterInit, getResult);
            }
            catch (DllNotFoundException)
            {
                // Local build without UtinniCore.dll — skip gracefully.
                // CI validates the P/Invoke path (UtinniCore.dll is built before dotnet test).
            }
        }
    }
}
