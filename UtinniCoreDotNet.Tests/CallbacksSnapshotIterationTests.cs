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
using System.Reflection;
using Xunit;
using UtinniCoreDotNet.Callbacks;

namespace UtinniCoreDotNet.Tests
{
    // Phase 3 R-H managed-side regression tests (per 03-CONTEXT D-12): every
    // managed dispatch site must copy subscribers under lock to a local array
    // and iterate the local copy. Subscribe-during-dispatch lands in the
    // registry but doesn't appear in the current iteration's snapshot, so it
    // fires on the NEXT dispatch — never throws InvalidOperationException.
    // Same property holds for Unsubscribe-during-dispatch.
    [Collection("StaticCallbackState")]
    public class CallbacksSnapshotIterationTests
    {
        private static void InvokePrivate(Type type, string methodName, params object[] args)
        {
            var m = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(m);
            m.Invoke(null, args);
        }

        [Fact]
        public void Subscribe_DuringDispatch_NotInvokedInCurrentIteration_InvokedInNext()
        {
            int outerCount = 0;
            int innerCount = 0;
            int innerHandle = 0;

            // Outer subscribes inner during its own dispatch. Snapshot semantics
            // mean inner lives in the registry after Outer runs, but only fires
            // on the NEXT dispatch.
            int outerHandle = GameCallbacks.SubscribeInstall(() =>
            {
                outerCount++;
                if (innerHandle == 0)
                {
                    innerHandle = GameCallbacks.SubscribeInstall(() => innerCount++);
                }
            });

            try
            {
                // First dispatch: outer fires; inner is subscribed but NOT in the
                // snapshot for this iteration.
                InvokePrivate(typeof(GameCallbacks), "CallInstallCallbacks");
                Assert.Equal(1, outerCount);
                Assert.Equal(0, innerCount);

                // Second dispatch: outer fires again (no-op for inner subscribe
                // since innerHandle is now set), inner DOES fire because it's
                // in the snapshot for this iteration.
                InvokePrivate(typeof(GameCallbacks), "CallInstallCallbacks");
                Assert.Equal(2, outerCount);
                Assert.Equal(1, innerCount);
            }
            finally
            {
                GameCallbacks.UnsubscribeInstall(outerHandle);
                if (innerHandle != 0)
                {
                    GameCallbacks.UnsubscribeInstall(innerHandle);
                }
            }
        }

        [Fact]
        public void Unsubscribe_DuringDispatch_DoesNotThrow()
        {
            int selfRemovingCount = 0;
            int handle = 0;

            // Self-removing callback: unsubscribes itself during its own dispatch.
            // The pre-Phase-3 SynchronizedCollection-based dispatch would throw
            // InvalidOperationException ("Collection was modified ... ") here;
            // the R-H snapshot pattern makes it safe.
            handle = GameCallbacks.SubscribeInstall(() =>
            {
                selfRemovingCount++;
                GameCallbacks.UnsubscribeInstall(handle);
            });

            try
            {
                var ex = Record.Exception(() => InvokePrivate(typeof(GameCallbacks), "CallInstallCallbacks"));
                Assert.Null(ex);
                Assert.Equal(1, selfRemovingCount);

                // Second dispatch: handle is gone from the registry, so the
                // self-removing callback does NOT fire again.
                InvokePrivate(typeof(GameCallbacks), "CallInstallCallbacks");
                Assert.Equal(1, selfRemovingCount);
            }
            finally
            {
                // Defensive cleanup (no-op if already removed).
                GameCallbacks.UnsubscribeInstall(handle);
            }
        }

        [Fact]
        public void Subscribe_DuringDispatch_GroundSceneCameraChange_NotInvokedInCurrentIteration()
        {
            // Same property as GameCallbacks but on GroundSceneCallbacks.CameraChange —
            // verifies R-H snapshot pattern applies uniformly across *Callbacks classes.
            int outerCount = 0;
            int innerCount = 0;
            int innerHandle = 0;

            int outerHandle = GroundSceneCallbacks.SubscribeCameraChange(() =>
            {
                outerCount++;
                if (innerHandle == 0)
                {
                    innerHandle = GroundSceneCallbacks.SubscribeCameraChange(() => innerCount++);
                }
            });

            try
            {
                InvokePrivate(typeof(GroundSceneCallbacks), "CallCameraChangeCallbacks");
                Assert.Equal(1, outerCount);
                Assert.Equal(0, innerCount);

                InvokePrivate(typeof(GroundSceneCallbacks), "CallCameraChangeCallbacks");
                Assert.Equal(2, outerCount);
                Assert.Equal(1, innerCount);
            }
            finally
            {
                GroundSceneCallbacks.UnsubscribeCameraChange(outerHandle);
                if (innerHandle != 0)
                {
                    GroundSceneCallbacks.UnsubscribeCameraChange(innerHandle);
                }
            }
        }
    }
}
