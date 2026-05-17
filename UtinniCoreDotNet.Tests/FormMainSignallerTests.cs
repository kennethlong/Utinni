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
    //
    // FormMain.TestSignaller is reset to null in every finally block so test runs stay
    // isolated from each other regardless of execution order or parallelism.
    public class FormMainSignallerTests
    {
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
            // Arrange: create an unsignalled event; signal it asynchronously after 10 ms.
            using (var mockSignal = new EventWaitHandle(false, EventResetMode.ManualReset))
            {
                FormMain.TestSignaller = mockSignal;
                try
                {
                    var timeout = TimeSpan.FromMilliseconds(500);

                    // Signal the event after 10 ms on a background thread.
                    Task.Run(() =>
                    {
                        Thread.Sleep(10);
                        mockSignal.Set();
                    });

                    var sw = Stopwatch.StartNew();

                    // Act
                    bool result = FormMain.WaitForPresentBlock(timeout);

                    sw.Stop();

                    // Assert: must return true (signal observed) and well under the 500 ms timeout.
                    Assert.True(result);
                    Assert.True(sw.ElapsedMilliseconds < 150,
                        $"Expected WaitForPresentBlock to return within ~50 ms but took {sw.ElapsedMilliseconds} ms");
                }
                finally
                {
                    FormMain.TestSignaller = null;
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
    }
}
