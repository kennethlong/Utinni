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

namespace UtinniCoreDotNet.UI
{
    /// <summary>
    /// Pure, dependency-free decision helper encoding the singleton-form hide-vs-dispose policy for
    /// plugin-registered editor forms (the ones a plugin adds ONCE to its <c>GetForms()</c> list).
    ///
    /// <remarks>
    /// <para><b>Why this exists:</b> Phase 8 hit an <c>ObjectDisposedException</c> defect TWICE
    /// during live-SWG smoke — a singleton editor form (FormIffEditor, commit <c>b899504</c>;
    /// FormTreBrowser defensive, commit <c>ce2a0a4</c>) was disposed on user close, so the next
    /// <c>Show()</c> threw at <c>Form.CreateHandle</c>. The canonical fix is: on
    /// <see cref="CloseReason.UserClosing"/>, cancel the close and <c>Hide()</c> instead of
    /// disposing; editor-host shutdown reasons (ApplicationExitCall / TaskManagerClosing /
    /// WindowsShutDown) fall through and dispose normally.</para>
    ///
    /// <para><b>Why a framework helper (iter-2 item 3 cross-repo placement):</b> extracting the
    /// CloseReason DECISION here keeps it CI-coverable from <c>UtinniCoreDotNet.Tests</c> WITHOUT a
    /// cross-repo <c>TheJawaToolboxDotNet.dll</c> reference. The TJT form's <c>FormClosing</c>
    /// handler is the thin WinForms adapter that delegates to this predicate; the xUnit guard
    /// exercises this predicate directly (no WinForms form instantiated). See memory
    /// <c>singleton-form-hide-not-dispose</c>.</para>
    /// </remarks>
    /// </summary>
    public static class SingletonFormClosePolicy
    {
        /// <summary>
        /// Returns <c>true</c> iff a singleton form closing for the given <paramref name="reason"/>
        /// should be HIDDEN (cancel the close) instead of disposed. Only a user-initiated close
        /// (<see cref="CloseReason.UserClosing"/>) hides; every host-shutdown reason returns
        /// <c>false</c> so the form disposes normally.
        /// </summary>
        public static bool ShouldHideInsteadOfDispose(CloseReason reason)
        {
            return reason == CloseReason.UserClosing;
        }
    }
}
