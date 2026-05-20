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
    static void addCreateCommandParserCallback(void(*func)(CommandParser* commandParser));

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

    static void detour();
};

}