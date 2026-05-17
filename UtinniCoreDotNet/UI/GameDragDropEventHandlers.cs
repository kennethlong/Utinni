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
using System.Windows.Forms;

namespace UtinniCoreDotNet.UI
{
    public static class GameDragDropEventHandlers
    {
        // C-05: converted from static fields to static events. The original pattern
        // captured the (null) field value at Initialize() time and wired that snapshot
        // to the panel — subsequent += on the static field never reached the panel.
        // The static event + forwarder lambda pattern captures the live event symbol,
        // so any handler added after Initialize() still receives events.
        public static event DragEventHandler OnDragDrop;
        public static event DragEventHandler OnDragEnter;
        public static event EventHandler OnDragLeave;
        public static event DragEventHandler OnDragOver;

        // Accept Panel (base class) so unit tests can pass a lightweight TestPanel
        // without the PanelGame P/Invoke ctor. Production call site (PanelGame.cs:68)
        // passes 'this' (a PanelGame, which derives from Panel) — still type-compatible.
        public static void Initialize(Panel panelGame)
        {
            panelGame.DragDrop += (s, e) => OnDragDrop?.Invoke(s, e);
            panelGame.DragEnter += (s, e) => OnDragEnter?.Invoke(s, e);
            panelGame.DragLeave += (s, e) => OnDragLeave?.Invoke(s, e);
            panelGame.DragOver += (s, e) => OnDragOver?.Invoke(s, e);
        }
    }
}
