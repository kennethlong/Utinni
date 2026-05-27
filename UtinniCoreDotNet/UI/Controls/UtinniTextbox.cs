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
using System.Runtime.InteropServices;
using System.Windows.Forms;
using UtinniCoreDotNet.UI.Theme;

namespace UtinniCoreDotNet.UI.Controls
{
    public class UtinniTextbox : TextBox
    {
        // EM_SETCUEBANNER: the native edit-control message that paints a dimmed placeholder hint
        // when the box is empty. .NET 4.7.2 TextBox has no managed PlaceholderText, so we P/Invoke.
        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private string _cueBanner;

        /// <summary>
        /// Dimmed placeholder hint shown while the box is empty (e.g. "Filter files…"). Applied via
        /// the native EM_SETCUEBANNER message; persists across focus (wParam=1). Reusable across the
        /// themed control set so any panel's search/filter box can carry an affordance.
        /// </summary>
        public string CueBanner
        {
            get { return _cueBanner; }
            set
            {
                _cueBanner = value;
                if (IsHandleCreated)
                {
                    ApplyCueBanner();
                }
            }
        }

        public UtinniTextbox()
        {
            BorderStyle = BorderStyle.FixedSingle;

            UpdateColors();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyCueBanner();
        }

        private void ApplyCueBanner()
        {
            if (!string.IsNullOrEmpty(_cueBanner))
            {
                // wParam = 1 → keep the hint visible even when the control has focus (until typing).
                SendMessage(Handle, EM_SETCUEBANNER, (IntPtr)1, _cueBanner);
            }
        }

        private void UpdateColors()
        {
            BackColor = Colors.PrimaryHighlight();
            ForeColor = Colors.Font();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);

            if (Enabled)
            {
                ForeColor = Colors.Font();
            }
            else
            {
                ForeColor = Colors.FontDisabled();
            }
        }
    }
}
