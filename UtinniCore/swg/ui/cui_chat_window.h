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

#pragma once

#include "utinni.h"

namespace utinni
{
class CommandParser;

class UTINNI_API CuiChatWindow // ToDo change to non static with getChatWindow func
{
public:
    // Phase 3 R-A primary API (per 03-CONTEXT D-08/D-09).
    static int subscribeCreateCommandParserCallback(void (*func)(CommandParser* commandParser));
    static bool unsubscribeCreateCommandParserCallback(int handle);
    // Legacy add* API (D-10): thin wrapper.
    static void addCreateCommandParserCallback(void (*func)(CommandParser* commandParser));

    static void enableTextInput(bool value);
    static void writeToAllTabs(const char* str);
    static void writeToCurrentTab(const char* str);
    static void sendMessage(const char* msg, bool addToChatHistory);

    // DIAG 2026-05-20 Issue #11 (per CODEX consult): force-open SWG's
    // chat input box directly via native enableTextInput(pCuiChatWindow,
    // true, true, false). Bypasses whatever upstream dispatch path
    // normally translates in-game Enter into chat-open. If wired to a
    // free hotkey (F11), pressing it should open chat reliably -- if so,
    // chat mediator is fine and the bug is upstream. Guarded by
    // pCuiChatWindow != 0.
    static void forceOpenChatInputFromCpp();

    // DIAG 2026-05-20 Issue #11 Phase F: dump the .bss-resident action-name
    // string pointers that gate the SwgCuiChatWindow performAction
    // dispatcher (0x00F37C00..0x00F37F58). Each case loads two pointers
    // (start, end) from globals at 0x0197BAxx / 0x0197Exxx -- those globals
    // are uninitialized at file-load time but populated at runtime by SWG's
    // constructors. Read them at runtime and resolve to readable strings so
    // we know which action name routes to which handler -- especially
    // crucial for identifying what's at 0x00F3E440 OPEN (the OPEN call we
    // expect for in-game Enter) vs 0x00F3E420 CLOSE (the close-on-Enter
    // call that actually fires today).
    static void dumpActionStringSlotsFromCpp();

    // Phase G workaround (Issue #11, CODEX-endorsed): track the last value
    // SWG passed to enableTextInput, so external code (the WndProc
    // subclass intercepting VK_RETURN) can tell whether chat is currently
    // in input mode and decide whether to short-circuit Enter to
    // forceOpenChatInputFromCpp. Updated by hkEnableTextInput on every
    // call.
    static bool isChatInputModeActive();

    static void detour();
};

} // namespace utinni