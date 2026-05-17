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
using UtinniCoreDotNet.Utility;

namespace UtinniCoreDotNet.Hotkeys
{
    public class Hotkey
    {
        public string Name;
        public string Text;
        public Keys ModifierKeys;
        public Keys Key;
        public Action OnDownCallback;
        public bool OverrideGameInput;
        public bool Enabled;
        public bool OnGameFocusOnly;

        public Hotkey(string name, string text, string keyComboStr, Action onDownCallback, bool overrideGameInput, bool enabled = true, bool onGameFocusOnly = false)
        {
            Name = name;
            Text = text;
            OnDownCallback = onDownCallback;
            OverrideGameInput = overrideGameInput;
            Enabled = enabled;
            OnGameFocusOnly = onGameFocusOnly;

            ProcessString(keyComboStr);
        }

        public Hotkey(string name, string text, Keys modifierKeys, Keys key, Action onDownCallback, bool overrideGameInput, bool enabled = true, bool onGameFocusOnly = false)
        {
            Name = name;
            Text = text;
            ModifierKeys = modifierKeys;
            Key = key;
            OnDownCallback = onDownCallback;
            OverrideGameInput = overrideGameInput;
            Enabled = enabled;
            OnGameFocusOnly = onGameFocusOnly;
        }

        private void ProcessString(string keyComboStr)
        {
            // C-08 fix: Enum.Parse throws ArgumentException on any unrecognized name
            // (e.g. 'Ctrl' instead of 'Control', or any typo in input.ini). Pre-fix,
            // a single bad hotkey aborted Hotkey.UpdateKeys → HotkeyManager.Load and
            // surfaced as a C-06 cascading failure. Replaced with Enum.TryParse +
            // warn-and-disable. Also fix the multi-modifier case: 'Shift + Alt + Z'
            // previously kept ModifierKeys='Shift' and passed 'Alt + Z' to Enum.Parse
            // (ArgumentException on net472); now the first N-1 segments are OR'd as
            // modifiers and the last segment is the single key.
            if (String.IsNullOrEmpty(keyComboStr))
            {
                TryLogWarning("Hotkey " + Name + " failed to process empty key combo string; disabling.");
                Enabled = false;
                return;
            }

            var segments = keyComboStr.Split('+');
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = segments[i].Trim();
            }

            if (segments.Length == 1)
            {
                if (!Enum.TryParse<Keys>(segments[0], true, out var single))
                {
                    TryLogWarning("Hotkey " + Name + " has unrecognized key '" + segments[0] + "'; disabling.");
                    Enabled = false;
                    return;
                }

                ModifierKeys = Keys.None;
                Key = single;
                return;
            }

            // First N-1 segments combine as modifiers; last segment is the single key.
            Keys mods = Keys.None;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (!Enum.TryParse<Keys>(segments[i], true, out var m))
                {
                    TryLogWarning("Hotkey " + Name + " has unrecognized modifier '" + segments[i] + "'; disabling.");
                    Enabled = false;
                    return;
                }

                mods |= m;
            }

            if (!Enum.TryParse<Keys>(segments[segments.Length - 1], true, out var keyOnly))
            {
                TryLogWarning("Hotkey " + Name + " has unrecognized key '" + segments[segments.Length - 1] + "'; disabling.");
                Enabled = false;
                return;
            }

            ModifierKeys = mods;
            Key = keyOnly;
        }

        // The native log sink is not initialized in unit-test mode; route warnings
        // through a try/catch so test-mode parse-failure paths do not throw out of
        // the Hotkey ctor (which would re-introduce a C-08-shaped failure).
        private static void TryLogWarning(string msg)
        {
            try
            {
                Log.Warning(msg);
            }
            catch
            {
                // Test-mode: log subsystem not initialized.
            }
        }

        public void UpdateKeys(string keyComboStr)
        {
            ProcessString(keyComboStr);
        }

        public void UpdateKeys(Keys modifierKeys, Keys key)
        {
            ModifierKeys = modifierKeys;
            Key = key;
        }

        public string GetKeyComboString()
        {
            if (ModifierKeys == Keys.None)
            {
                return Key.ToString();
            }

            return ModifierKeys + " + " + Key;
        }

    }
}
