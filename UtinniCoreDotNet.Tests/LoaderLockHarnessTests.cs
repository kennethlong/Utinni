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

using System.Diagnostics;
using System.IO;
using Xunit;

namespace UtinniCoreDotNet.Tests
{
    public class LoaderLockHarnessTests
    {
        // Resolve the harness exe path relative to the test output directory.
        // Test bin is at bin/Release/net472/ (AppendPlatformToOutputPath=false).
        // Harness builds to $(SolutionDir)bin/Release/Utinni.LoaderLockHarness.exe.
        // Walking up 2 levels from net472/ reaches bin/Release/ where the harness lives.
        private static string GetHarnessPath()
        {
            return Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "Utinni.LoaderLockHarness.exe"));
        }

        [Fact]
        public void LoaderLockHarness_LoadsUtinniCoreUnderThreshold()
        {
            var harnessPath = GetHarnessPath();
            Assert.True(File.Exists(harnessPath), $"Harness exe not found at {harnessPath}");

            var psi = new ProcessStartInfo(harnessPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var proc = Process.Start(psi);
            bool exited = proc.WaitForExit(5000);
            string stdout = proc.StandardOutput.ReadToEnd();

            Assert.True(exited, "Harness process did not exit within 5 seconds");
            Assert.Contains("elapsed:", stdout);
            Assert.Equal(0, proc.ExitCode);
        }
    }
}
