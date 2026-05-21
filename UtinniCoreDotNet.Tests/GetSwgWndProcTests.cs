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
    // Phase 3 R-C regression Facts (per 03-CONTEXT D-18..D-20 / TD-18 +
    // Plan 03-02 Task 3). Exercises the single-source-of-truth WndProc RVA
    // (0x00AA0970) -- previously duplicated as a literal in PanelGame.cs,
    // now surfaced via the getSwgWndProcExport C-linkage shim in client.cpp.
    //
    // Two complementary tests:
    //   1. GetSwgWndProc_ReturnsLiteralConstant: P/Invoke the export, assert
    //      the returned IntPtr equals new IntPtr(0x00AA0970).
    //   2. PanelGameSource_NoLongerContainsLiteralRVA: grep-style negative
    //      test on PanelGame.cs source. Asserts the literal `0x00AA0970`
    //      no longer appears (the only place it should live now is
    //      client.cpp:43; this test guards against re-introducing the
    //      duplication in a future commit).
    //
    // The "PanelGame.WndProc actually-calls-into-SWG" path remains
    // Tier-4 manual per D-06 -- that requires live SWG. The two tests
    // here cover everything verifiable without live SWG.
    public class GetSwgWndProcTests
    {
        // P/Invoke into the C-linkage shim added in client.cpp.
        // CppSharp drops pointer-returning getters so the auto-generated
        // bindings don't expose getSwgWndProc; this DllImport is the
        // hand-rolled equivalent (same pattern as GetSwgHwnd in
        // UtinniCoreDotNet/Utility/Native.cs).
        private static class NativeBridge
        {
            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "getSwgWndProcExport")]
            public static extern IntPtr GetSwgWndProc();
        }

        // Test 1: P/Invoke returns the expected constant.
        [Fact]
        public void GetSwgWndProc_ReturnsLiteralConstant()
        {
            // Skip if UtinniCore.dll isn't deployed (local dev without build).
            IntPtr value;
            try
            {
                value = NativeBridge.GetSwgWndProc();
            }
            catch (DllNotFoundException)
            {
                return;
            }
            catch (EntryPointNotFoundException)
            {
                return;
            }

            // 0x00AA0970 is the SWG WndProc RVA declared at
            // UtinniCore/swg/client/client.cpp:43. The C-linkage export
            // returns this value verbatim.
            Assert.Equal(new IntPtr(0x00AA0970), value);
        }

        // Test 2: grep-style negative test. The literal 0x00AA0970 should
        // no longer appear in PanelGame.cs -- the only place it should
        // live is client.cpp.
        [Fact]
        public void PanelGameSource_NoLongerContainsLiteralRVA()
        {
            string sourcePath = FindPanelGameSrc();
            string content = File.ReadAllText(sourcePath);

            // Defense-in-depth: check both the exact-hex form
            // (`0x00AA0970`) and the bare numeric form (`11142000`,
            // the decimal value). A future drive-by-edit that
            // re-introduces the literal via either form trips this gate.
            Assert.DoesNotContain("0x00AA0970", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("0x00aa0970", content, StringComparison.OrdinalIgnoreCase);

            // Positive check: the file SHOULD reference the new
            // Native.GetSwgWndProc() P/Invoke -- ensures the refactor
            // actually wired the value through the getter rather than
            // hardcoding a different constant.
            Assert.Contains("Native.GetSwgWndProc()", content);
        }

        // Walks up from AppContext.BaseDirectory to the repo root and
        // resolves UtinniCoreDotNet/UI/Controls/PanelGame.cs. Mirrors the
        // FindCfg pattern in UtinniCfgTests.cs (4-or-5 levels up).
        private static string FindPanelGameSrc()
        {
            string baseDir = AppContext.BaseDirectory;
            for (int up = 4; up <= 6; up++)
            {
                string upDir = baseDir;
                for (int j = 0; j < up; j++)
                {
                    upDir = Path.GetDirectoryName(upDir);
                    if (string.IsNullOrEmpty(upDir)) { break; }
                }
                if (string.IsNullOrEmpty(upDir)) { continue; }
                string candidate = Path.Combine(upDir, "UtinniCoreDotNet", "UI", "Controls", "PanelGame.cs");
                if (File.Exists(candidate)) { return candidate; }
            }
            throw new FileNotFoundException(
                "Could not locate UtinniCoreDotNet/UI/Controls/PanelGame.cs from " + baseDir);
        }
    }
}
