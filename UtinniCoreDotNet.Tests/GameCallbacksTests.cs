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
using System.IO;
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
    /// CallInstallCallbacks regardless of native availability (Probe 2 below); that is
    /// the deterministic green-CI assertion.
    ///
    /// 06-04 OPT-A: the former native probe called utinni_triggerInstallCallbacks, which
    /// iterates raw void(*)() function pointers over UNDEFINED native state in this
    /// non-injected test process — producing an ASLR-dependent AccessViolationException
    /// that flaked CI (D-17). It is replaced by a deterministic, side-effect-free sentinel
    /// export (utinni_testHarnessProbe) that proves the native boundary is loaded + callable
    /// without crashing. See 06-04-FLAKE-INVESTIGATION.md.
    /// </summary>
    public class GameCallbacksTests
    {
        private static class NativeBridge
        {
            // 06-04 OPT-A: deterministic liveness sentinel — returns 0xDEADBEEF and touches
            // no callback state, so it can never AV. Replaces the AV-prone trigger probe.
            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_testHarnessProbe")]
            public static extern uint Utinni_TestHarnessProbe();
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

            // Probe 1 (06-04 OPT-A): deterministic native-boundary liveness check.
            // The former probe called utinni_triggerInstallCallbacks, which iterates raw
            // void(*)() function pointers over undefined native state in this non-injected
            // test process — producing an ASLR-dependent AccessViolationException that
            // flaked CI (D-17). It is replaced by a side-effect-free sentinel that touches
            // no callback state and can never AV: it proves UtinniCore.dll is loaded and
            // callable across the P/Invoke boundary without crashing. Gated on the DLL
            // actually sitting next to the test assembly (Tests.csproj CopyNativeArtifactsForTests);
            // local dev runs without it simply skip this probe. Probe 2 below is the real
            // GC-survival assertion and ALWAYS runs.
            //
            // Regression fence: this fix is fenced — if the gate/sentinel is removed and the
            // raw native trigger rejoins the green-CI path, the test will flake on CI again.
            // See 06-04-FLAKE-INVESTIGATION.md.
            string nativeDll = Path.Combine(AppContext.BaseDirectory, "UtinniCore.dll");
            if (File.Exists(nativeDll))
            {
                uint probe = NativeBridge.Utinni_TestHarnessProbe();
                Assert.Equal(0xDEADBEEFu, probe);
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
