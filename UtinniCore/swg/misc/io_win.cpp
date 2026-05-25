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

#include "io_win.h"
#include "utility/log.h"

#include <cstdio>
#include <intrin.h>

namespace swg::ioWin
{
using pDraw = void(__thiscall*)(utinni::IoWin* pThis);

pDraw draw = (pDraw)0x00AB58E0;
} // namespace swg::ioWin

namespace swg::messageQueue
{
using pGetCount = int(__thiscall*)(utinni::MessageQueue* pThis);
using pGetMessage = void(__thiscall*)(utinni::MessageQueue* pThis, int index, int* msg, float* value, uint32_t* flags);
using pAppendMessage = void(__thiscall*)(utinni::MessageQueue* pThis, int msg, float value, uint32_t* flags);
using pAppendMessageData = void(__thiscall*)(utinni::MessageQueue* pThis, int msg, float value, swgptr data, uint32_t* flags);

pGetCount getCount = (pGetCount)0x00AA6660;
pGetMessage getMessage = (pGetMessage)0x00AA63B0;
pAppendMessage appendMessage = (pAppendMessage)0x00AA6640;
pAppendMessageData appendMessageData = (pAppendMessageData)0x00AA6480;
} // namespace swg::messageQueue

namespace utinni
{

void __fastcall hkDraw(IoWin* pThis, swgptr EDX)
{
    swg::ioWin::draw(pThis);
}
void IoWin::detour()
{
    // swg::ioWin::draw = (swg::ioWin::pDraw)Detour::Create(swg::ioWin::draw, hkDraw, DETOUR_TYPE_PUSH_RET);
}

// DIAG 2026-05-20 Issue #11 Phase G (per CODEX consult): instrument
// MessageQueue::appendMessage and ::appendMessageData. These are what the
// GroundScene input map emits after translating raw IoEvents into
// game commands. Logging here reveals what command was emitted for Enter
// -- comparing standalone-Enter behavior vs editor-injected behavior will
// either localize the bug here ("input map emits wrong command") or
// downstream ("command is correct but consumed by wrong mediator").
void __fastcall hkAppendMessage(MessageQueue* pThis, DWORD EDX, int msg, float value, uint32_t* flags)
{
    static int s_appendMsgLogCount = 0;
    if (s_appendMsgLogCount < 5)
    {
        ++s_appendMsgLogCount;
        const void* callerPC = _ReturnAddress();
        char m[200];
        snprintf(m, sizeof(m),
                 "hkAppendMessage[%d]: queue=0x%p msg=0x%X value=%.3f flags=0x%p caller=0x%p",
                 s_appendMsgLogCount, (void*)pThis, (unsigned)msg, value, (void*)flags, callerPC);
        utinni::log::info(m);
    }
    swg::messageQueue::appendMessage(pThis, msg, value, flags);
}

void __fastcall hkAppendMessageData(MessageQueue* pThis, DWORD EDX, int msg, float value, swgptr data, uint32_t* flags)
{
    static int s_appendDataLogCount = 0;
    if (s_appendDataLogCount < 5)
    {
        ++s_appendDataLogCount;
        const void* callerPC = _ReturnAddress();
        char m[224];
        snprintf(m, sizeof(m),
                 "hkAppendMessageData[%d]: queue=0x%p msg=0x%X value=%.3f data=0x%p flags=0x%p caller=0x%p",
                 s_appendDataLogCount, (void*)pThis, (unsigned)msg, value, (void*)data, (void*)flags, callerPC);
        utinni::log::info(m);
    }
    swg::messageQueue::appendMessageData(pThis, msg, value, data, flags);
}

void MessageQueue::detour()
{
    swg::messageQueue::appendMessage = (swg::messageQueue::pAppendMessage)Detour::Create(swg::messageQueue::appendMessage, hkAppendMessage, DETOUR_TYPE_PUSH_RET);
    swg::messageQueue::appendMessageData = (swg::messageQueue::pAppendMessageData)Detour::Create(swg::messageQueue::appendMessageData, hkAppendMessageData, DETOUR_TYPE_PUSH_RET);
}

int MessageQueue::getCount()
{
    return swg::messageQueue::getCount(this);
}

void MessageQueue::getMessage(int index, int* msg, float* value)
{
    swg::messageQueue::getMessage(this, index, msg, value, nullptr);
}

void MessageQueue::appendMessage(int msg, float value)
{
    swg::messageQueue::appendMessage(this, msg, value, nullptr);
}

void MessageQueue::appendMessage(int msg, float value, swgptr data)
{
    swg::messageQueue::appendMessageData(this, msg, value, data, nullptr);
}
} // namespace utinni
