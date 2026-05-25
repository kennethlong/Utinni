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

#include "cui_io.h"
#include "swg/ui/imgui_impl.h"
#include "utility/log.h"

#include <cstdio>

namespace swg::cuiIo
{
using pProcessEvent = swgptr(__thiscall*)(swgptr pThis, swgptr pEvent);
using pSetKeyboardInputActive = swgptr(__thiscall*)(swgptr pThis, bool value);
using pRequestKeyboard = swgptr(__thiscall*)(swgptr pThis, bool value);
using pDraw = void(__thiscall*)(swgptr pThis);

pProcessEvent processEvent = (pProcessEvent)0x093BD50;
pSetKeyboardInputActive setKeyboardInputActive = (pSetKeyboardInputActive)0x0093D490;
pRequestKeyboard requestKeyboard = (pRequestKeyboard)0x0093D560;
pDraw draw = (pDraw)0x0093B2B0;

} // namespace swg::cuiIo

namespace utinni
{
bool oldIsKeyboardEnabled;
bool isKeyboardEnabled = true;

enum EventTypes
{
    KeyCharacter = 6,
    KeyDown = 7,
    KeyUp = 8
};

swgptr cuiIo::get()
{
    return memory::read<swgptr>(0x192613C); // Static CuiIo ptr
}

void cuiIo::enableKeyboard(bool value)
{
    // DIAG 2026-05-20 Issue #11: log every call so we can see WHO flips
    // isKeyboardEnabled to false (HotkeyManager has the only known caller,
    // gated by OverrideGameInput hotkeys -- but the comment on
    // restorePreviousEnableKeyboardValue admits the toggle gets broken).
    char msg[128];
    snprintf(msg, sizeof(msg),
             "cuiIo::enableKeyboard: value=%d (was=%d)",
             value ? 1 : 0, isKeyboardEnabled ? 1 : 0);
    utinni::log::info(msg);

    oldIsKeyboardEnabled = isKeyboardEnabled;
    isKeyboardEnabled = value;
}

void cuiIo::restorePreviousEnableKeyboardValue()
{
    // ToDo this can get broken somehow when called from .NET, figure out why. Use enableKeyboard(true) for now
    utinni::log::info("cuiIo::restorePreviousEnableKeyboardValue: forcing isKeyboardEnabled=true");
    isKeyboardEnabled = true;
}

bool cuiIo::isInputBlocked()
{
    return !isKeyboardEnabled;
}

swgptr __fastcall hkProcessEvent(swgptr pThis, swgptr EDX, swgptr pEvent)
{
    const int eventType = memory::read<int>(pEvent + 4);

    // DIAG 2026-05-20 Issue #11: log any keyboard-event drop. If
    // isKeyboardEnabled gets stuck false, SWG's CUI silently stops
    // receiving KeyCharacter(6)/KeyDown(7) -- which matches "Enter/Esc
    // dead in-game while WASD (polled separately by SWG) still works".
    // Rate-limit by only logging drops, not every passthrough, to keep
    // log readable. Also log the FIRST passthrough of types 6/7 each
    // session so we can confirm the detour is on the right RVA.
    if ((eventType == 6 || eventType == 7) && !isKeyboardEnabled)
    {
        // Read a couple more event-struct words to help identify which key
        // got dropped. Layout is informed-guess; if wrong, fields will be
        // garbage but the log still confirms the drop is happening.
        unsigned w2 = (unsigned)memory::read<int>(pEvent + 8);
        unsigned w3 = (unsigned)memory::read<int>(pEvent + 12);
        char msg[160];
        snprintf(msg, sizeof(msg),
                 "hkProcessEvent: DROPPING type=%d (isKeyboardEnabled=0) pEvent=0x%p w2=0x%08X w3=0x%08X",
                 eventType, (void*)pEvent, w2, w3);
        utinni::log::info(msg);
        return 0;
    }

    // DIAG 2026-05-20 Issue #11 Phase B: prior diag with first-fire-only
    // logs proved isKeyboardEnabled stays true (no drops) but couldn't
    // tell whether in-game Enter/Esc actually reach processEvent (only
    // the FIRST type-7 fired the log). Upgrade: log EVERY type-6 and
    // type-7 with pEvent+8/+12 dumps, capped at 60 total to avoid runaway
    // spam. The cap is generous enough for a "press Enter+Esc 5x each
    // in-game" repro to clearly show whether events reach CUI in-game.
    if (eventType == 6 || eventType == 7)
    {
        static int s_eventLogCount = 0;
        if (s_eventLogCount < 60)
        {
            ++s_eventLogCount;
            unsigned w2 = (unsigned)memory::read<int>(pEvent + 8);
            unsigned w3 = (unsigned)memory::read<int>(pEvent + 12);
            char msg[160];
            snprintf(msg, sizeof(msg),
                     "hkProcessEvent[%d]: type=%d PASS pEvent=0x%p w2=0x%08X w3=0x%08X",
                     s_eventLogCount, eventType, (void*)pEvent, w2, w3);
            utinni::log::info(msg);
        }
        else if (s_eventLogCount == 60)
        {
            ++s_eventLogCount;
            utinni::log::info("hkProcessEvent: 60 type-6/7 events logged, suppressing further passthrough logs");
        }
    }

    return swg::cuiIo::processEvent(pThis, pEvent);
}

void __fastcall hkDraw(swgptr pThis, swgptr EDX)
{
    swg::cuiIo::draw(pThis);
}

void cuiIo::detour()
{
    swg::cuiIo::processEvent = (swg::cuiIo::pProcessEvent)Detour::Create((LPVOID)swg::cuiIo::processEvent, hkProcessEvent, DETOUR_TYPE_PUSH_RET);
    // swg::cuiIo::draw = (swg::cuiIo::pDraw)Detour::Create((LPVOID)swg::cuiIo::draw, hkDraw, DETOUR_TYPE_PUSH_RET);
}

} // namespace utinni
