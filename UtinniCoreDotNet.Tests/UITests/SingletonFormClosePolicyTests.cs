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

using System.Windows.Forms;
using UtinniCoreDotNet.UI;
using Xunit;

namespace UtinniCoreDotNet.Tests.UITests
{
    /// <summary>
    /// Canonical regression guard for the Phase-8-smoke-discovered singleton-form
    /// <c>ObjectDisposedException</c> defect class (FormIffEditor <c>b899504</c> + FormTreBrowser
    /// <c>ce2a0a4</c>; memory <c>singleton-form-hide-not-dispose</c>). Exercises ONLY the framework
    /// helper <see cref="SingletonFormClosePolicy"/> + <see cref="CloseReason"/> — no TJT type, no
    /// WinForms form instantiation, no STA requirement (iter-2 item 3 cross-repo placement). The
    /// full WinForms+MEF hide-not-dispose round-trip is verified by the Plan 09-03 Task 4 live
    /// checkpoint.
    /// </summary>
    public class SingletonFormClosePolicyTests
    {
        [Fact]
        public void ShouldHide_OnUserClosing_ReturnsTrue()
        {
            Assert.True(SingletonFormClosePolicy.ShouldHideInsteadOfDispose(CloseReason.UserClosing));
        }

        [Theory]
        [InlineData(CloseReason.ApplicationExitCall)]
        [InlineData(CloseReason.TaskManagerClosing)]
        [InlineData(CloseReason.WindowsShutDown)]
        [InlineData(CloseReason.FormOwnerClosing)]
        [InlineData(CloseReason.MdiFormClosing)]
        public void ShouldNotHide_OnShutdownReasons_ReturnsFalse(CloseReason reason)
        {
            Assert.False(SingletonFormClosePolicy.ShouldHideInsteadOfDispose(reason));
        }
    }
}
