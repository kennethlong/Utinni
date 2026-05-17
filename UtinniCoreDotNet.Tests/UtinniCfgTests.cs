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
using Xunit;

namespace UtinniCoreDotNet.Tests
{
    // C-14 regression: data/utinni.cfg previously shipped with
    //   loginServerPort0=44453
    //   loginServerAddress0=login.swgemu.com
    // — SWGEmu's defaults baked into a sovereign-fork that explicitly disclaims
    // affiliation. Per CON-D-01 the file MUST ship with blank login fields so the
    // user is forced to enter their own shard's host:port.
    //
    // This test asserts the file-content invariant via regex on the value after
    // each "loginServerXxx0=" key. If a future commit re-introduces a default
    // value, this assertion goes red.
    //
    // Path resolution: same 4-levels-up walk as VsixManifestTests.
    public class UtinniCfgTests
    {
        private static string FindCfg()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "data", "utinni.cfg"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
            candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "..", "data", "utinni.cfg"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
            throw new FileNotFoundException("Could not locate data/utinni.cfg from " + baseDir);
        }

        [Fact]
        public void LoginServerAddress0_AndLoginServerPort0_AreBlank()
        {
            var content = File.ReadAllText(FindCfg());
            // "key=" followed by optional whitespace then end-of-line or end-of-file —
            // the value MUST be blank.
            Assert.Matches(@"loginServerAddress0\s*=\s*(\r?\n|$)", content);
            Assert.Matches(@"loginServerPort0\s*=\s*(\r?\n|$)", content);
            // Defense-in-depth: explicit negative checks against the SWGEmu defaults.
            Assert.DoesNotMatch(@"loginServerAddress0\s*=\s*login\.swgemu\.com", content);
            Assert.DoesNotMatch(@"loginServerPort0\s*=\s*44453", content);
        }

        [Fact]
        public void Cfg_HasClientGameSection()
        {
            var content = File.ReadAllText(FindCfg());
            Assert.Contains("[ClientGame]", content);
        }
    }
}
