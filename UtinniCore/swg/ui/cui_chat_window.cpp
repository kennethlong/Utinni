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

#include "cui_chat_window.h"
#include "command_parser.h"
#include "utinni_command_parser.h"
#include "swg/misc/swg_memory.h"
#include "utility/log.h"

#include <cstdio>
#include <intrin.h>

namespace swg::cuiChatWindow
{
using pCtor = swgptr(__thiscall*)(swgptr pThis, swgptr uiPage, DWORD unk1, DWORD unk2, DWORD unk3);
using pEnableTextInput = void(__thiscall*)(swgptr pThis, bool value, bool setKeyboardInput, bool unfocus);
using pWriteToTab = swgptr(__thiscall*)(swgptr pThis, const WString& str);

pCtor ctor = (pCtor)0x00F364B0;
pEnableTextInput enableTextInput = (pEnableTextInput)0x00F38500;
pWriteToTab writeToAllTabs = (pWriteToTab)0x00F3BFD0;
pWriteToTab writeToCurrentTab = (pWriteToTab)0x00F3C1F0;
}

namespace swg::cuiConsoleHelper
{
using pSendInput = bool(__thiscall*)(swgptr pThis, const swg::WString& str, swgptr unk, bool addToChatHistory);
pSendInput sendInput = (pSendInput)0x009141D0;

}

static swgptr pCuiChatWindow; // ToDo use the getChatWindow function instead of the ctor detour?
static swgptr pCuiConsoleHelper;
static std::vector<void(*)(utinni::CommandParser* mainCommandParser)> addCommandParserCallback;

namespace utinni
{
bool enableInput;

void CuiChatWindow::addCreateCommandParserCallback(void(*func)(CommandParser* commandParser))
{
    addCommandParserCallback.emplace_back(func);
}

void CuiChatWindow::enableTextInput(bool value) // Accepts color codes by prefixing them with \\, ie "\\#888888 test"
{
    enableInput = value;
}

void CuiChatWindow::writeToAllTabs(const char* str)
{
    if (pCuiChatWindow == 0)
    {
        return;
    }

    swg::cuiChatWindow::writeToAllTabs(pCuiChatWindow, swg::WString(str));
}

void CuiChatWindow::writeToCurrentTab(const char* str) // Accepts color codes by prefixing them with \\, ie "\\#888888 test"
{
    if (pCuiChatWindow == 0)
    {
        return;
    }

    swg::cuiChatWindow::writeToCurrentTab(pCuiChatWindow, swg::WString(str));
}

// DIAG 2026-05-20 Issue #11 Phase F: probe the SwgCuiChatWindow
// performAction dispatcher's action-name string slots. Static byte-level
// analysis of 0x00F37C00..0x00F37F58 identified these 12 cases. The
// globals at the listed addresses are .bss pointers populated at runtime;
// each pair (start, end) defines an interned action name. Two cases use a
// different style (push imm32 directly into strcmp): for those, the imm32
// itself is the .bss slot holding a char*. Reading at runtime should
// reveal which action name routes to which handler, including which
// action triggers the 0x00F3E440 OPEN call we expect on in-game Enter.
struct DispatchSlot
{
    DWORD startPtrAddr;  // VA of .bss slot holding (char*)action_start
    DWORD endPtrAddr;    // VA of .bss slot holding (char*)action_end
    DWORD handlerVA;     // target of the dispatched CALL
    const char* role;
};
static const DispatchSlot kDispatchSlots[] = {
    { 0x0197BAAC, 0x0197BAB0, 0x00F3E3A0, "OPEN 1arg-v1 (case 0 @F37CB4)" },
    { 0x0197BAA0, 0x0197BAA4, 0x00F3E3A0, "OPEN 1arg-v1 (case 1 @F37CF2)" },
    { 0x0197E310, 0x0197E314, 0x00F3E370, "OPEN 2arg   (case 2 @F37D31)" },
    { 0x0197E304, 0x0197E308, 0x00F3E370, "OPEN 2arg   (case 3 @F37D70)" },
    { 0x0197E328, 0x0197E32C, 0x00F3E370, "OPEN 2arg   (case 4 @F37DB0)" },
    { 0x0197E31C, 0x0197E320, 0x00F3E370, "OPEN 2arg   (case 5 @F37DF0)" },
    { 0x0197E340, 0x0197E344, 0x00F3E3D0, "OPEN 1arg-v2(case 6 @F37E2E)" },
    { 0x0197E334, 0x0197E338, 0x00F3E3D0, "OPEN 1arg-v2(case 7 @F37E6C)" },
    { 0x0197E2F8, 0x0197E2FC, 0x00F3E400, "OPEN 0arg   (case 8 @F37EA8)" },
    { 0x0197E2EC, 0x0197E2F0, 0x00F3E460, "CLOSE 0arg  (case 9 @F37EE4)" },
    // Cases 10/11 use a different style: the imm32 IS the .bss slot
    { 0x0197E2A4, 0x0197E2A4, 0x00F3E440, "OPEN 0arg   (case 10 @F37F19 -- direct push imm)" },
    { 0x0197E28C, 0x0197E28C, 0x00F3E420, "CLOSE pushArg1 (case 11 @F37F4B -- direct push imm <-- IN-GAME ENTER FIRES THIS)" },
};

static void dumpStringAt(swgptr addr, char* buf, size_t buflen, size_t max = 48)
{
    if (addr == 0) { snprintf(buf, buflen, "<null>"); return; }
    size_t pos = 0;
    buf[0] = '\'';
    pos = 1;
    for (size_t i = 0; i < max && pos < buflen - 4; ++i)
    {
        byte b = 0;
        // memory::read might throw on bad addresses; guard with __try
        __try
        {
            b = memory::read<byte>(addr + (DWORD)i);
        }
        __except(EXCEPTION_EXECUTE_HANDLER)
        {
            snprintf(buf + pos, buflen - pos, "...<AV>");
            return;
        }
        if (b == 0) break;
        if (b >= 0x20 && b < 0x7F) { buf[pos++] = (char)b; }
        else { buf[pos++] = '.'; }
    }
    buf[pos++] = '\'';
    buf[pos] = 0;
}

void CuiChatWindow::dumpActionStringSlotsFromCpp()
{
    utinni::log::info("=== Phase F: SwgCuiChatWindow performAction dispatcher string slots ===");
    char m[400];
    char s[80];
    for (size_t i = 0; i < sizeof(kDispatchSlots) / sizeof(kDispatchSlots[0]); ++i)
    {
        const DispatchSlot& slot = kDispatchSlots[i];

        // Read the slot as if it holds a pointer (interpretation 1: *(char**)slot is a string ptr)
        DWORD ptrVal = 0;
        __try { ptrVal = memory::read<DWORD>(slot.startPtrAddr); }
        __except(EXCEPTION_EXECUTE_HANDLER) { ptrVal = 0; }

        // Treat ptrVal as a char* and read string from there
        dumpStringAt((swgptr)ptrVal, s, sizeof(s));

        snprintf(m, sizeof(m),
            "  slot[%2d] %s\n"
            "    startAddr=0x%08X *startAddr=0x%08X str=%s",
            (int)i, slot.role, slot.startPtrAddr, ptrVal, s);
        utinni::log::info(m);

        // For paired slots, also dump end pointer
        if (slot.endPtrAddr != slot.startPtrAddr)
        {
            DWORD endVal = 0;
            __try { endVal = memory::read<DWORD>(slot.endPtrAddr); }
            __except(EXCEPTION_EXECUTE_HANDLER) { endVal = 0; }
            DWORD len = (endVal >= ptrVal && endVal - ptrVal < 64) ? endVal - ptrVal : 0;
            snprintf(m, sizeof(m),
                "    endAddr=0x%08X *endAddr=0x%08X (computed len = %u)",
                slot.endPtrAddr, endVal, len);
            utinni::log::info(m);
        }

        // Also try interpretation 2: the slot ADDRESS itself is the string start (for cases 10/11)
        if (slot.startPtrAddr == slot.endPtrAddr)
        {
            char s2[80];
            dumpStringAt((swgptr)slot.startPtrAddr, s2, sizeof(s2));
            snprintf(m, sizeof(m), "    alt: string starting AT addr 0x%08X = %s",
                slot.startPtrAddr, s2);
            utinni::log::info(m);
        }
    }
    utinni::log::info("=== Phase F dump complete ===");
}

void CuiChatWindow::forceOpenChatInputFromCpp()
{
    // DIAG 2026-05-20 Issue #11 (per CODEX consult): A/B test whether SWG's
    // chat mediator itself works when we call enableTextInput directly with
    // the captured CuiChatWindow instance pointer. If pressing F11 opens
    // chat reliably, the Enter-in-game dispatch is broken UPSTREAM of the
    // chat mediator (search the 0x00F38390 / 0x00F38430 functions next).
    // If F11 ALSO does nothing, the mediator/window is broken or
    // pCuiChatWindow is stale.
    if (pCuiChatWindow == 0)
    {
        utinni::log::warning("CuiChatWindow::forceOpenChatInputFromCpp: pCuiChatWindow is null (chat window was never constructed?)");
        return;
    }
    utinni::log::info("CuiChatWindow::forceOpenChatInputFromCpp: calling swg::cuiChatWindow::enableTextInput(pCuiChatWindow, true, true, false)");
    swg::cuiChatWindow::enableTextInput(pCuiChatWindow, true, true, false);
}

void CuiChatWindow::sendMessage(const char* msg, bool addToChatHistory)
{
    if (pCuiChatWindow == 0 || pCuiConsoleHelper == 0)
    {
        return;
    }

    // Due to the unknown, most likely old std parameter in sendInput,
    // which we don't need for the purpose of this function anyway,
    // we need to patch out calls utilizing that parameter inside the SWG function,
    // else we crash due to access violations
    const auto origBytes1 = memory::nopAddress(0x00914245, 5);
    const auto origBytes2 = memory::nopAddress(0x00914250, 5);
    const auto origBytes3 = memory::nopAddress(0x0091425D, 5);
    const auto origBytes4 = memory::nopAddress(0x0091427D, 5);
    const auto origBytes5 = memory::nopAddress(0x009142E4, 5);

    memory::set(0x0091428C, 0x75, 1);

    swg::cuiConsoleHelper::sendInput(pCuiConsoleHelper, swg::WString(msg), 0, addToChatHistory);

    // Once the call has been made, the function needs to be restored
    // for potential swg calls that might utilize the parameter
    memory::restoreBytes(origBytes1);
    memory::restoreBytes(origBytes2);
    memory::restoreBytes(origBytes3);
    memory::restoreBytes(origBytes4);
    memory::restoreBytes(origBytes5);

    memory::set(0x0091428C, 0x74, 1);
}

void __fastcall hkEnableTextInput(swgptr pThis, swgptr EDX, bool value, bool setKeyboardInput, bool unfocus)
{
    // DIAG 2026-05-20 Issue #11 Phase E (per CODEX consult): log every call
    // to identify (a) the natural caller(s) when SWG opens chat at login
    // and (b) whether ANYTHING fires enableTextInput when the user presses
    // in-game Enter (without our F11 bypass). If a caller fires for login
    // chat-open but nothing fires for in-game Enter -> bug is in the SWG
    // upstream code that decides "Enter is pressed in game-mode, open chat"
    // (likely in CuiHud's keyboard handler or CuiActionMap dispatch).
    static int s_logCount = 0;
    if (s_logCount < 30)
    {
        ++s_logCount;
        const void* callerPC = _ReturnAddress();
        char m[224];
        snprintf(m, sizeof(m),
            "hkEnableTextInput[%d]: pThis=0x%p value=%d setKbdInput=%d unfocus=%d caller=0x%p captured_pCuiChatWindow=0x%p",
            s_logCount, (void*)pThis, value ? 1 : 0, setKeyboardInput ? 1 : 0,
            unfocus ? 1 : 0, callerPC, (void*)pCuiChatWindow);
        utinni::log::info(m);
    }

    // CODEX bug-fix: previous code called with pCuiChatWindow instead of
    // pThis. Wrong for an instance hook -- SWG might construct multiple
    // CuiChatWindow instances and the captured one might be stale or not
    // the one we were just called on. Always forward to the instance we
    // were actually invoked on.
    swg::cuiChatWindow::enableTextInput(pThis, value, setKeyboardInput, unfocus);
}

CommandParser* mainCommandParser;
swgptr __fastcall hkCtor(swgptr pThis, swgptr EDX, swgptr uiPage, DWORD unk1, DWORD unk2, DWORD unk3)
{
    swgptr result = swg::cuiChatWindow::ctor(pThis, uiPage, unk1, unk2, unk3);
    pCuiChatWindow = pThis;
    pCuiConsoleHelper = memory::read<swgptr>(pThis + 0xBC); // Store the CuiConsoleHelper* for later use

    mainCommandParser->addSubCommand(swg_new<UtinniCommandParser>());

    for (const auto& func : addCommandParserCallback)
    {
        func(mainCommandParser);
    }

    return result;
}

swgptr return_midCtor = 0x00F3679D;
__declspec(naked) void midCtor()
{
    swgptr pMainCommandParser;
    __asm
    {
        mov pMainCommandParser, edx
        pushad
        pushfd
    }

    mainCommandParser = (CommandParser*)pMainCommandParser;

    __asm
    {
        popfd
        popad
        mov eax, dword ptr ss : [esi + 0xE4]
        jmp[return_midCtor]
    }
}

void CuiChatWindow::detour()
{
    swg::cuiChatWindow::ctor = (swg::cuiChatWindow::pCtor)Detour::Create(swg::cuiChatWindow::ctor, hkCtor, DETOUR_TYPE_PUSH_RET);

    // DIAG 2026-05-20 Issue #11 Phase E: enabled for caller-tracing. Was
    // commented-out historically (likely because of the pCuiChatWindow vs
    // pThis bug now fixed in hkEnableTextInput). Logs every caller of
    // enableTextInput; nothing is suppressed -- pure passthrough.
    swg::cuiChatWindow::enableTextInput = (swg::cuiChatWindow::pEnableTextInput)Detour::Create(swg::cuiChatWindow::enableTextInput, hkEnableTextInput, DETOUR_TYPE_PUSH_RET);

    memory::createJMP(0x00F36797, (swgptr)midCtor, 6); // Mid CuiChatWindow::ctor detour
}

}
