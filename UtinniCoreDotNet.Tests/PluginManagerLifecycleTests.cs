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
using Xunit;

namespace UtinniCoreDotNet.Tests
{
    // Phase 3 R-B regression Facts (per 03-CONTEXT D-13..D-17 + Plan 03-02 Task 2).
    //
    // Exercises:
    //   1. Two-phase init (D-14): both fixtures' init() invoked after both
    //      createPlugin returns.
    //   2. LoadLibrary failure logged with GetLastError (D-17): a 0-byte
    //      Broken.dll alongside CrtMatchPlugin -- assert
    //      lastLoadLibraryError captures GetLastError and CrtMatchPlugin
    //      still loaded.
    //   3. Per-plugin init() exception isolation (D-14 + Phase 02 C-06):
    //      CrtMatchPlugin's init throws; LegacyPlugin's init still runs.
    //   4. destroyPlugin invoked on shutdown when present (D-13).
    //   5. Legacy fallback to virtual destructor (D-13 + D-15): no crash
    //      when destroyPlugin export absent.
    //
    // Module-binding strategy: each test copies the fixture DLLs into a
    // fresh temp dir and calls NativeBridge.LoadFromDir(tempDir). PluginManager
    // (and its test-bridge equivalent) calls LoadLibrary against the full
    // temp-dir path -- so the diagnostic-counter delegates must bind to THAT
    // same loaded module instance (not the one in the test bin dir). We use
    // GetModuleHandleA(tempDirPath) post-LoadFromDir to obtain the
    // HMODULE PluginManager loaded, then Marshal.GetDelegateForFunctionPointer
    // each export. This guarantees the test reads the same counter that
    // PluginManager's createPlugin/init/destroyPlugin invocations updated.
    //
    // [Collection("StaticCallbackState")] serializes against other tests
    // that touch process-wide native state.
    [Collection("StaticCallbackState")]
    public class PluginManagerLifecycleTests
    {
        // -- P/Invoke bridges -----------------------------------------------

        private static class NativeBridge
        {
            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_pluginManagerLoadFromDir",
                CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            public static extern int LoadFromDir([MarshalAs(UnmanagedType.LPStr)] string dir);

            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_pluginManagerLoadedCount")]
            public static extern int LoadedCount();

            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_pluginManagerDispose")]
            public static extern void Dispose();

            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_lastLoadLibraryError")]
            public static extern uint LastLoadLibraryError();
        }

        // -- Win32 kernel32 ---------------------------------------------------

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr LoadLibraryA([MarshalAs(UnmanagedType.LPStr)] string lpLibFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetModuleHandleA([MarshalAs(UnmanagedType.LPStr)] string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        // -- Delegates for fixture diagnostic exports -------------------------

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int IntReturnNoArgs();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void VoidNoArgs();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void VoidIntArg(int x);

        private sealed class CrtMatchBridge
        {
            public IntReturnNoArgs GetCreateCount;
            public IntReturnNoArgs GetInitCount;
            public IntReturnNoArgs GetDestroyCount;
            public VoidNoArgs ResetCounters;
            public VoidIntArg SetInitShouldThrow;
        }

        private sealed class LegacyBridge
        {
            public IntReturnNoArgs GetInitCount;
            public VoidNoArgs ResetCounters;
        }

        private static T BindExport<T>(IntPtr hModule, string name) where T : class
        {
            IntPtr fp = GetProcAddress(hModule, name);
            if (fp == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "GetProcAddress('" + name + "') on hModule 0x"
                    + hModule.ToInt64().ToString("X") + " failed: GLE="
                    + Marshal.GetLastWin32Error());
            }
            return Marshal.GetDelegateForFunctionPointer(fp, typeof(T)) as T;
        }

        private static CrtMatchBridge BindCrtMatch(IntPtr hModule)
        {
            return new CrtMatchBridge
            {
                GetCreateCount = BindExport<IntReturnNoArgs>(hModule, "crtmatch_getCreateCount"),
                GetInitCount = BindExport<IntReturnNoArgs>(hModule, "crtmatch_getInitCount"),
                GetDestroyCount = BindExport<IntReturnNoArgs>(hModule, "crtmatch_getDestroyCount"),
                ResetCounters = BindExport<VoidNoArgs>(hModule, "crtmatch_resetCounters"),
                SetInitShouldThrow = BindExport<VoidIntArg>(hModule, "crtmatch_setInitShouldThrow"),
            };
        }

        private static LegacyBridge BindLegacy(IntPtr hModule)
        {
            return new LegacyBridge
            {
                GetInitCount = BindExport<IntReturnNoArgs>(hModule, "legacy_getInitCount"),
                ResetCounters = BindExport<VoidNoArgs>(hModule, "legacy_resetCounters"),
            };
        }

        // -- Helpers ----------------------------------------------------------

        // Walks up from AppContext.BaseDirectory looking for the native fixture
        // DLL. The CopyNativeArtifactsForTests target places it directly in
        // TargetDir alongside UtinniCore.dll.
        private static string FindNativeFixtureDll(string fixtureName)
        {
            string baseDir = AppContext.BaseDirectory;
            string direct = Path.Combine(baseDir, fixtureName + ".dll");
            if (File.Exists(direct)) { return direct; }

            for (int up = 1; up <= 5; up++)
            {
                string upDir = baseDir;
                for (int j = 0; j < up; j++)
                {
                    upDir = Path.GetDirectoryName(upDir);
                    if (string.IsNullOrEmpty(upDir)) { break; }
                }
                if (string.IsNullOrEmpty(upDir)) { continue; }
                string candidate = Path.Combine(upDir, "bin", "Release", fixtureName + ".dll");
                if (File.Exists(candidate)) { return candidate; }
            }
            throw new FileNotFoundException(
                "Could not find " + fixtureName + ".dll near " + baseDir);
        }

        private static string MakeTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "Utinni-PluginManagerLifecycleTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void TryDeleteDir(string dir)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        // Stage a fixture DLL into the temp dir and return the absolute path
        // of the copied DLL. PluginManager will LoadLibrary against this exact
        // path; tests later use GetModuleHandle on the same path to obtain
        // the HMODULE for delegate binding.
        private static string StageFixture(string fixtureName, string tempDir)
        {
            string src = FindNativeFixtureDll(fixtureName);
            string dst = Path.Combine(tempDir, fixtureName + ".dll");
            File.Copy(src, dst, overwrite: true);
            return dst;
        }

        // Obtain HMODULE of the named module loaded from the staged path.
        // PluginManager LoadLibrarys the temp-dir path; this returns the same
        // HMODULE without bumping the ref count. Asserts non-zero -- it's a
        // test bug if the module isn't loaded after LoadFromDir.
        private static IntPtr GetStagedModule(string stagedPath)
        {
            IntPtr h = GetModuleHandleA(stagedPath);
            if (h == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "GetModuleHandleA('" + stagedPath + "') returned NULL -- "
                    + "PluginManager did not load this module. GLE="
                    + Marshal.GetLastWin32Error());
            }
            return h;
        }

        // If UtinniCore.dll or the fixture DLLs aren't available, gracefully
        // skip the test. Same pattern as NativeCallbacksHandleTests.
        private static bool TryEnsureNativeAvailable()
        {
            try
            {
                NativeBridge.LoadedCount();
                return true;
            }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }

        // -- Tests ------------------------------------------------------------

        [Fact]
        public void LoadPlugins_CrtMatchLoaded_LegacyRejected_InitsCalledForSymmetricOnly()
        {
            // CR-02 (03-REVIEW): the previous "both fixtures load" assertion
            // was tied to the legacy fallback that has been removed. With
            // /MT-mismatched LegacyPlugin now rejected at load time, this
            // test asserts the surviving symmetric-ABI plugin still loads
            // and inits correctly (the LegacyPlugin rejection is covered by
            // LegacyPlugin_NoDestroyPlugin_RejectedAtLoadTime + the
            // LegacyPlugin_AlongsideCrtMatch... fact).
            if (!TryEnsureNativeAvailable()) { return; }

            string tempDir = MakeTempDir();
            try
            {
                string crtPath = StageFixture("Utinni.CrtMatchPlugin", tempDir);
                StageFixture("Utinni.LegacyPlugin", tempDir);

                try
                {
                    int loaded = NativeBridge.LoadFromDir(tempDir);
                    Assert.Equal(1, loaded);
                    Assert.Equal(1, NativeBridge.LoadedCount());

                    // Bind bridge to the module instance PluginManager loaded.
                    CrtMatchBridge crt = BindCrtMatch(GetStagedModule(crtPath));

                    // D-14: init() invoked on the symmetric plugin.
                    Assert.Equal(1, crt.GetInitCount());

                    // D-14 ordering: createPlugin returned BEFORE init ran
                    // (the fixture increments createCount in createPlugin
                    // and initCount in init; both being 1 proves create→init).
                    Assert.Equal(1, crt.GetCreateCount());

                    // D-17: a successful LoadLibrary (even when followed by
                    // a destroyPlugin-missing rejection) leaves
                    // lastLoadLibraryError = 0 -- the rejection path doesn't
                    // touch GetLastError, only the LoadLibrary-itself-failed
                    // path does.
                    Assert.Equal(0u, NativeBridge.LastLoadLibraryError());
                }
                finally
                {
                    NativeBridge.Dispose();
                }
            }
            finally
            {
                TryDeleteDir(tempDir);
            }
        }

        [Fact]
        public void LoadLibraryFailure_LoggedWithGetLastError_OtherPluginsStillLoad()
        {
            if (!TryEnsureNativeAvailable()) { return; }

            string tempDir = MakeTempDir();
            try
            {
                string crtPath = StageFixture("Utinni.CrtMatchPlugin", tempDir);
                // 0-byte DLL: LoadLibrary will fail with a non-zero GetLastError
                // (typically ERROR_BAD_EXE_FORMAT = 193 on modern Windows).
                File.WriteAllBytes(Path.Combine(tempDir, "Broken.dll"), new byte[0]);

                try
                {
                    int loaded = NativeBridge.LoadFromDir(tempDir);
                    // The good plugin loaded; the broken one was logged + skipped.
                    Assert.Equal(1, loaded);
                    Assert.Equal(1, NativeBridge.LoadedCount());

                    // D-17: lastLoadLibraryError captured the GetLastError of
                    // the failed LoadLibrary. Any non-zero value is acceptable.
                    uint err = NativeBridge.LastLoadLibraryError();
                    Assert.NotEqual(0u, err);

                    // The good plugin's init still fired (log+continue).
                    CrtMatchBridge crt = BindCrtMatch(GetStagedModule(crtPath));
                    Assert.Equal(1, crt.GetInitCount());
                }
                finally
                {
                    NativeBridge.Dispose();
                }
            }
            finally
            {
                TryDeleteDir(tempDir);
            }
        }

        [Fact]
        public void PluginInitThrows_LoadCompletes_NoCrash()
        {
            // D-14 + C-06: per-plugin try/catch isolation around init() means
            // a throwing init() doesn't bring down loadFromDirectory itself.
            //
            // CR-02 (03-REVIEW): the previous version of this fact asserted
            // "LegacyPlugin's init still ran" as the isolation proof, but
            // LegacyPlugin is now rejected at load time (no destroyPlugin
            // export). The isolation invariant remains: a throwing init()
            // must not crash the loader, the plugin counts as loaded, and
            // dispose runs cleanly. We assert those structural properties
            // here. (Future: add a second symmetric-ABI fixture to test
            // multi-plugin isolation end-to-end.)
            if (!TryEnsureNativeAvailable()) { return; }

            string tempDir = MakeTempDir();
            try
            {
                string crtPath = StageFixture("Utinni.CrtMatchPlugin", tempDir);

                // Arm the throw flag BEFORE LoadFromDir. To toggle it we need
                // the CrtMatchPlugin module loaded; LoadLibrary it ourselves
                // first (this is the same path PluginManager will use, so
                // Windows refcount-bumps the same module when LoadFromDir
                // runs -- a single shared module instance + shared statics).
                IntPtr crtModule = LoadLibraryA(crtPath);
                Assert.NotEqual(IntPtr.Zero, crtModule);
                try
                {
                    CrtMatchBridge crt = BindCrtMatch(crtModule);
                    crt.ResetCounters();
                    crt.SetInitShouldThrow(1);

                    try
                    {
                        int loaded = NativeBridge.LoadFromDir(tempDir);
                        Assert.Equal(1, loaded);

                        // CrtMatchPlugin's init incremented its counter BEFORE
                        // throwing (per the fixture's body); per-plugin
                        // try/catch caught the exception, so the load
                        // completed without bringing down the harness.
                        Assert.Equal(1, crt.GetInitCount());
                    }
                    finally
                    {
                        // Disarm before dispose so subsequent destroyPlugin
                        // calls don't trip the flag.
                        crt.SetInitShouldThrow(0);
                        NativeBridge.Dispose();
                    }
                }
                finally
                {
                    FreeLibrary(crtModule);
                }
            }
            finally
            {
                TryDeleteDir(tempDir);
            }
        }

        [Fact]
        public void DestroyPlugin_InvokedOnShutdown_WhenPresent()
        {
            if (!TryEnsureNativeAvailable()) { return; }

            string tempDir = MakeTempDir();
            try
            {
                string crtPath = StageFixture("Utinni.CrtMatchPlugin", tempDir);

                // LoadLibrary the fixture FIRST so the test holds a refcount
                // that survives PluginManager's FreeLibrary on Dispose. Without
                // this extra ref, FreeLibrary drops the refcount to 0 and the
                // module is unloaded; our delegate function pointers would
                // then be dangling.
                IntPtr ourRef = LoadLibraryA(crtPath);
                Assert.NotEqual(IntPtr.Zero, ourRef);

                try
                {
                    int loaded = NativeBridge.LoadFromDir(tempDir);
                    Assert.Equal(1, loaded);

                    CrtMatchBridge crt = BindCrtMatch(GetStagedModule(crtPath));
                    Assert.Equal(0, crt.GetDestroyCount());

                    NativeBridge.Dispose();

                    // D-13: destroyPlugin invoked on shutdown when the export
                    // is present. CrtMatchPlugin exports it; counter
                    // increments to 1.
                    Assert.Equal(1, crt.GetDestroyCount());
                }
                finally
                {
                    FreeLibrary(ourRef);
                }
            }
            finally
            {
                TryDeleteDir(tempDir);
            }
        }

        [Fact]
        public void LegacyPlugin_NoDestroyPlugin_RejectedAtLoadTime()
        {
            // CR-02 (03-REVIEW) regression: the previous "legacy fallback"
            // path in ~PluginManager called `delete loaded.plugin` from
            // UtinniCore.dll's /MD CRT against memory allocated in the
            // plugin's CRT (here, /MT static CRT). That is exactly the
            // CON-B-04 cross-CRT-free crash class R-B was designed to
            // eliminate; the fixture asserting "no crash" was a happy-path
            // tautology because the cross-CRT free often "succeeds" silently
            // while corrupting both heaps.
            //
            // Fix: PluginManager now rejects plugins missing destroyPlugin
            // at load time (the symmetric ABI is the only sound shape per
            // D-13). This test now asserts the rejection rather than the
            // unsafe-but-doesn't-AV-deterministically fallback.
            //
            // CON-O-07 disposition D-15: legacy plugins (Sytner upstream is
            // dormant) have no compat target. Authors must adopt the
            // UTINNI_PLUGIN macro which emits both createPlugin and
            // destroyPlugin.
            if (!TryEnsureNativeAvailable()) { return; }

            string tempDir = MakeTempDir();
            try
            {
                string legacyPath = StageFixture("Utinni.LegacyPlugin", tempDir);

                int loaded = NativeBridge.LoadFromDir(tempDir);
                Assert.Equal(0, loaded);
                Assert.Equal(0, NativeBridge.LoadedCount());

                // Dispose must remain a no-op when nothing loaded. (No
                // crash from disposing an empty impl is the structural
                // invariant we're preserving end-to-end.)
                NativeBridge.Dispose();
                Assert.Equal(0, NativeBridge.LoadedCount());
            }
            finally
            {
                TryDeleteDir(tempDir);
            }
        }

        [Fact]
        public void LegacyPlugin_AlongsideCrtMatch_LegacyRejected_CrtMatchStillLoads()
        {
            // CR-02 + D-17 (per-plugin isolation): one bad plugin in a dir
            // doesn't sink the others. CrtMatchPlugin has destroyPlugin and
            // loads; LegacyPlugin doesn't and is rejected. Same "log+continue"
            // disposition as the LoadLibrary-failure case (Phase 02 C-06).
            if (!TryEnsureNativeAvailable()) { return; }

            string tempDir = MakeTempDir();
            try
            {
                string crtPath = StageFixture("Utinni.CrtMatchPlugin", tempDir);
                StageFixture("Utinni.LegacyPlugin", tempDir);

                try
                {
                    int loaded = NativeBridge.LoadFromDir(tempDir);
                    Assert.Equal(1, loaded);
                    Assert.Equal(1, NativeBridge.LoadedCount());

                    CrtMatchBridge crt = BindCrtMatch(GetStagedModule(crtPath));
                    Assert.Equal(1, crt.GetInitCount());
                }
                finally
                {
                    NativeBridge.Dispose();
                }
            }
            finally
            {
                TryDeleteDir(tempDir);
            }
        }
    }
}
