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
using Xunit;
using UtinniCoreDotNet.Hotkeys;

namespace UtinniCoreDotNet.Tests
{
    public class HotkeyTests
    {
        [Fact]
        public void Ctor_StringConstructor_SingleKey_SetsKeyAndNoModifier()
        {
            var hk = new Hotkey("test", "test", "F1", () => { }, overrideGameInput: false);
            Assert.Equal(Keys.None, hk.ModifierKeys);
            Assert.Equal(Keys.F1, hk.Key);
        }

        [Fact]
        public void Ctor_StringConstructor_ModifierChord_SetsBoth()
        {
            var hk = new Hotkey("test", "test", "Control + S", () => { }, overrideGameInput: false);
            Assert.Equal(Keys.Control, hk.ModifierKeys);
            Assert.Equal(Keys.S, hk.Key);
        }

        [Theory]
        [InlineData("Shift + Alt + Z", Keys.Shift | Keys.Alt, Keys.Z)]
        public void Ctor_StringConstructor_MultiModifierChord_ParsesFlags(string combo, Keys expectedMods, Keys expectedKey)
        {
            var hk = new Hotkey("test", "test", combo, () => { }, overrideGameInput: false);
            Assert.Equal(expectedMods, hk.ModifierKeys);
            Assert.Equal(expectedKey, hk.Key);
        }

        [Fact]
        public void Ctor_StringConstructor_MalformedInput_DoesNotThrow()
        {
            // C-08 regression: malformed input like 'Ctrl + T' ('Ctrl' is not a valid
            // Keys enum name — should be 'Control') previously threw ArgumentException
            // out of Hotkey.ProcessString. Post-fix it warns-and-disables.
            Hotkey hk = null;
            var ex = Record.Exception(() => hk = new Hotkey("test", "test", "Ctrl + T", () => { }, overrideGameInput: false));
            Assert.Null(ex);
            Assert.NotNull(hk);
            Assert.False(hk.Enabled);
        }

        [Fact]
        public void Ctor_StringConstructor_MalformedModifier_DoesNotThrow_DisablesHotkey()
        {
            // C-08 regression: malformed modifier ('Garbage' is not a valid Keys enum
            // name) should warn-and-disable, not throw.
            Hotkey hk = null;
            var ex = Record.Exception(() => hk = new Hotkey("test", "test", "Garbage + S", () => { }, overrideGameInput: false));
            Assert.Null(ex);
            Assert.NotNull(hk);
            Assert.False(hk.Enabled);
        }
    }
}
