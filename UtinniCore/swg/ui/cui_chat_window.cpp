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
#include "swg/endpoints.h"
#include "command_parser.h"
#include "utinni_command_parser.h"
#include "swg/misc/swg_memory.h"
#include "swg/scene/ground_scene.h"
#include "utility/log.h"

#include <atomic>
#include <cstdio>
#include <intrin.h>
#include <mutex>
#include <vector>

namespace swg::cuiChatWindow
{
using pCtor = swgptr(__thiscall*)(swgptr pThis, swgptr uiPage, DWORD unk1, DWORD unk2, DWORD unk3);
using pEnableTextInput = void(__thiscall*)(swgptr pThis, bool value, bool setKeyboardInput, bool unfocus);
using pWriteToTab = swgptr(__thiscall*)(swgptr pThis, const WString& str);

// 2026-05-20 Issue #11 Phase H: SwgCuiChatWindow's "chatEnter" action handler.
// Per Phase F dispatcher decode, this is the small wrapper at 0x00F3E420 that
// calls CuiConsoleHelper::?(1) then enableTextInput(false). Normally invoked
// by SWG's input map when chat is in input mode (legitimate submit + close).
// Under editor injection it ALSO fires for in-game Enter while chat is in
// display mode -- the context routing layer is broken and sends the
// chat-input-mode binding instead of the game-mode openChat binding. Our
// hkChatEnter (in utinni namespace) overrides the display-mode case to open
// chat input instead of attempting a no-op submit/close.
using pChatEnterHandler = void(__thiscall*)(swgptr pThis);
pChatEnterHandler chatEnterHandler = (pChatEnterHandler)0x00F3E420;

pCtor ctor = (pCtor)0x00F364B0;
pEnableTextInput enableTextInput = (pEnableTextInput)0x00F38500;
pWriteToTab writeToAllTabs = (pWriteToTab)0x00F3BFD0;
pWriteToTab writeToCurrentTab = (pWriteToTab)0x00F3C1F0;

// Phase 24 v4: a C++ ctor address cannot be taken, so cuiChatWindow::ctor is infeasible to
// advertise. The provider instead advertises cuiChatWindow::createNewWindow -> the sole
// construction funnel (static factory SwgCuiChatWindow::createNewWindow, __cdecl(UIPage&,
// Game::SceneType, std::string const&) -- the one `new SwgCuiChatWindow` site). On the
// advertised client the chat-construction hook detours THIS funnel instead of the ctor;
// advertised-only (null on SWGEmu, where hkCtor detours the ctor literal above). The hkCtor
// retarget to this funnel is the smoke-gated chat unlock.
using pCreateNewWindow = swgptr(__cdecl*)(swgptr uiPage, int sceneType, swgptr stdString);
pCreateNewWindow createNewWindow = nullptr;
} // namespace swg::cuiChatWindow

namespace swg::cuiConsoleHelper
{
using pSendInput = bool(__thiscall*)(swgptr pThis, const swg::WString& str, swgptr unk, bool addToChatHistory);
pSendInput sendInput = (pSendInput)0x009141D0;

} // namespace swg::cuiConsoleHelper

// WR-06 (03-REVIEW): pCuiChatWindow + pCuiConsoleHelper are written from
// hkCtor on whatever thread SWG constructs the chat window on (typically the
// main thread post-login) and read from many reader sites (writeToAllTabs,
// writeToCurrentTab, forceOpenChatInputFromCpp, sendMessage). x86's strong
// memory model permitted the raw read/write but a future compiler hoist or
// ARM port would break. Promoted to std::atomic<swgptr>. Release store on
// publish (hkCtor) pairs with relaxed loads at reader sites: the pointer
// value itself is the synchronization datum (the SWG objects pointed at
// finished their ctor before swg::cuiChatWindow::ctor returned).
static std::atomic<swgptr> pCuiChatWindow{0}; // ToDo use the getChatWindow function instead of the ctor detour?
static std::atomic<swgptr> pCuiConsoleHelper{0};
// Phase 3 R-A native-side (per 03-CONTEXT D-08/D-09): handle-based registry
// backed by insertion-order std::vector<{handle, fn_ptr}>.
// CR-01 (03-REVIEW): per-registry mutex protects Subscribe / Unsubscribe / snapshot.
//
// 2026-05-22 follow-up to ground_scene fix (commit 7201700): switched from
// std::unordered_map to insertion-order vector with stack-allocated fixed-size
// snapshot in dispatch sites. See [[project-rh-snapshot-no-heap-alloc]] memory.
namespace
{
template <typename Fn>
struct CallbackEntry
{
    int handle;
    Fn func;
};

template <typename Fn, typename Invoke>
void dispatchSnapshot(
    const std::vector<CallbackEntry<Fn>>& registry,
    std::mutex& mutex,
    Invoke&& invoke)
{
    constexpr size_t kInlineCap = 16;
    Fn stackSnap[kInlineCap];
    Fn* snapshot = stackSnap;
    std::vector<Fn> heapSnap;
    size_t count = 0;
    {
        std::lock_guard<std::mutex> guard(mutex);
        const size_t total = registry.size();
        if (total <= kInlineCap)
        {
            count = total;
            for (size_t i = 0; i < count; ++i)
            {
                stackSnap[i] = registry[i].func;
            }
        }
        else
        {
            heapSnap.reserve(total);
            for (const auto& e : registry)
            {
                heapSnap.push_back(e.func);
            }
            snapshot = heapSnap.data();
            count = total;
        }
    }
    for (size_t i = 0; i < count; ++i)
    {
        invoke(snapshot[i]);
    }
}
} // namespace

static std::vector<CallbackEntry<void (*)(utinni::CommandParser* mainCommandParser)>> addCommandParserCallback;
static std::mutex addCommandParserCallbackMutex;
static int s_nextCommandParserId = 1;

// Phase G (Issue #11): mirrors the last value SWG passed to enableTextInput.
// Updated in hkEnableTextInput. Used by external code (imgui_impl
// hkWndProcHandler VK_RETURN intercept) to decide whether to short-circuit
// in-game Enter to forceOpenChatInputFromCpp. Default false: chat starts
// in display mode at scene-load (scene-init calls enableTextInput(false)).
//
// WR-05 (03-REVIEW): read on the imgui WndProc thread (via
// isChatInputModeActive + hkChatEnter) and written on the SWG main thread
// (via hkEnableTextInput, forceOpenChatInputFromCpp, hkChatEnter). Promoted
// to std::atomic<bool> with relaxed ordering -- the value alone is the
// synchronization datum (a single-word bool flag).
static std::atomic<bool> s_chatInputActive{false};

namespace utinni
{
bool enableInput;

// Phase 3 R-A: handle-based Subscribe/Unsubscribe per D-08/D-09.
int CuiChatWindow::subscribeCreateCommandParserCallback(void (*func)(CommandParser* commandParser))
{
    std::lock_guard<std::mutex> guard(addCommandParserCallbackMutex);
    int id = s_nextCommandParserId++;
    if (id == 0)
    {
        id = s_nextCommandParserId++;
    } // WR-04 skip-zero
    addCommandParserCallback.push_back({id, func});
    return id;
}

bool CuiChatWindow::unsubscribeCreateCommandParserCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(addCommandParserCallbackMutex);
    for (auto it = addCommandParserCallback.begin(); it != addCommandParserCallback.end(); ++it)
    {
        if (it->handle == handle)
        {
            addCommandParserCallback.erase(it);
            return true;
        }
    }
    return false;
}

void CuiChatWindow::addCreateCommandParserCallback(void (*func)(CommandParser* commandParser))
{
    subscribeCreateCommandParserCallback(func);
}

void CuiChatWindow::enableTextInput(bool value) // Accepts color codes by prefixing them with \\, ie "\\#888888 test"
{
    enableInput = value;
}

void CuiChatWindow::writeToAllTabs(const char* str)
{
    // WR-06: atomic load (published with release in hkCtor).
    const swgptr p = pCuiChatWindow.load(std::memory_order_relaxed);
    if (p == 0)
    {
        return;
    }

    swg::cuiChatWindow::writeToAllTabs(p, swg::WString(str));
}

void CuiChatWindow::writeToCurrentTab(const char* str) // Accepts color codes by prefixing them with \\, ie "\\#888888 test"
{
    // WR-06: atomic load.
    const swgptr p = pCuiChatWindow.load(std::memory_order_relaxed);
    if (p == 0)
    {
        return;
    }

    swg::cuiChatWindow::writeToCurrentTab(p, swg::WString(str));
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
    DWORD startPtrAddr; // VA of .bss slot holding (char*)action_start
    DWORD endPtrAddr;   // VA of .bss slot holding (char*)action_end
    DWORD handlerVA;    // target of the dispatched CALL
    const char* role;
};
static const DispatchSlot kDispatchSlots[] = {
    {0x0197BAAC, 0x0197BAB0, 0x00F3E3A0, "OPEN 1arg-v1 (case 0 @F37CB4)"},
    {0x0197BAA0, 0x0197BAA4, 0x00F3E3A0, "OPEN 1arg-v1 (case 1 @F37CF2)"},
    {0x0197E310, 0x0197E314, 0x00F3E370, "OPEN 2arg   (case 2 @F37D31)"},
    {0x0197E304, 0x0197E308, 0x00F3E370, "OPEN 2arg   (case 3 @F37D70)"},
    {0x0197E328, 0x0197E32C, 0x00F3E370, "OPEN 2arg   (case 4 @F37DB0)"},
    {0x0197E31C, 0x0197E320, 0x00F3E370, "OPEN 2arg   (case 5 @F37DF0)"},
    {0x0197E340, 0x0197E344, 0x00F3E3D0, "OPEN 1arg-v2(case 6 @F37E2E)"},
    {0x0197E334, 0x0197E338, 0x00F3E3D0, "OPEN 1arg-v2(case 7 @F37E6C)"},
    {0x0197E2F8, 0x0197E2FC, 0x00F3E400, "OPEN 0arg   (case 8 @F37EA8)"},
    {0x0197E2EC, 0x0197E2F0, 0x00F3E460, "CLOSE 0arg  (case 9 @F37EE4)"},
    // Cases 10/11 use a different style: the imm32 IS the .bss slot
    {0x0197E2A4, 0x0197E2A4, 0x00F3E440, "OPEN 0arg   (case 10 @F37F19 -- direct push imm)"},
    {0x0197E28C, 0x0197E28C, 0x00F3E420, "CLOSE pushArg1 (case 11 @F37F4B -- direct push imm <-- IN-GAME ENTER FIRES THIS)"},
};

static void dumpStringAt(swgptr addr, char* buf, size_t buflen, size_t max = 48)
{
    if (addr == 0)
    {
        snprintf(buf, buflen, "<null>");
        return;
    }
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
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            snprintf(buf + pos, buflen - pos, "...<AV>");
            return;
        }
        if (b == 0)
            break;
        if (b >= 0x20 && b < 0x7F)
        {
            buf[pos++] = (char)b;
        }
        else
        {
            buf[pos++] = '.';
        }
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
        __try
        {
            ptrVal = memory::read<DWORD>(slot.startPtrAddr);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            ptrVal = 0;
        }

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
            __try
            {
                endVal = memory::read<DWORD>(slot.endPtrAddr);
            }
            __except (EXCEPTION_EXECUTE_HANDLER)
            {
                endVal = 0;
            }
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
    // WR-06: atomic load.
    const swgptr p = pCuiChatWindow.load(std::memory_order_relaxed);
    if (p == 0)
    {
        utinni::log::warning("CuiChatWindow::forceOpenChatInputFromCpp: pCuiChatWindow is null (chat window was never constructed?)");
        return;
    }
    utinni::log::info("CuiChatWindow::forceOpenChatInputFromCpp: calling swg::cuiChatWindow::enableTextInput(pCuiChatWindow, true, true, false)");
    swg::cuiChatWindow::enableTextInput(p, true, true, false);
    // Phase H: enableTextInput pointer is the trampoline (post-detour), which
    // bypasses our hkEnableTextInput. Update tracker directly so downstream
    // consumers (hkChatEnter, etc.) see the correct state. WR-05 atomic.
    s_chatInputActive.store(true, std::memory_order_relaxed);
}

void CuiChatWindow::sendMessage(const char* msg, bool addToChatHistory)
{
    // WR-06: atomic loads.
    const swgptr p = pCuiChatWindow.load(std::memory_order_relaxed);
    const swgptr helper = pCuiConsoleHelper.load(std::memory_order_relaxed);
    if (p == 0 || helper == 0)
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

    swg::cuiConsoleHelper::sendInput(helper, swg::WString(msg), 0, addToChatHistory);

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
    // WR-05 companion: atomic counter so concurrent hkEnableTextInput firings
    // (SWG main thread + any future detour-thread reentrancy) don't double-
    // log a slot or miss the 30-cap.
    static std::atomic<int> s_logCount{0};
    int slot = s_logCount.load(std::memory_order_relaxed);
    if (slot < 30)
    {
        slot = s_logCount.fetch_add(1, std::memory_order_relaxed);
        if (slot < 30)
        {
            const void* callerPC = _ReturnAddress();
            char m[224];
            snprintf(m, sizeof(m),
                     "hkEnableTextInput[%d]: pThis=0x%p value=%d setKbdInput=%d unfocus=%d caller=0x%p captured_pCuiChatWindow=0x%p",
                     slot + 1, (void*)pThis, value ? 1 : 0, setKeyboardInput ? 1 : 0,
                     unfocus ? 1 : 0, callerPC,
                     (void*)pCuiChatWindow.load(std::memory_order_relaxed));
            utinni::log::info(m);
        }
    }

    // Phase G: mirror the most recent value for external consumers (WR-05 atomic).
    s_chatInputActive.store(value, std::memory_order_relaxed);

    // CODEX bug-fix: previous code called with pCuiChatWindow instead of
    // pThis. Wrong for an instance hook -- SWG might construct multiple
    // CuiChatWindow instances and the captured one might be stale or not
    // the one we were just called on. Always forward to the instance we
    // were actually invoked on.
    swg::cuiChatWindow::enableTextInput(pThis, value, setKeyboardInput, unfocus);
}

bool CuiChatWindow::isChatInputModeActive()
{
    return s_chatInputActive.load(std::memory_order_relaxed);
}

CommandParser* mainCommandParser;
swgptr __fastcall hkCtor(swgptr pThis, swgptr EDX, swgptr uiPage, DWORD unk1, DWORD unk2, DWORD unk3)
{
    swgptr result = swg::cuiChatWindow::ctor(pThis, uiPage, unk1, unk2, unk3);
    // WR-06: publish chat-window + console-helper pointers atomically. Release
    // store pairs with relaxed loads at reader sites; SWG objects finished
    // construction inside swg::cuiChatWindow::ctor before the publish.
    pCuiChatWindow.store(pThis, std::memory_order_release);
    pCuiConsoleHelper.store(memory::read<swgptr>(pThis + 0xBC), std::memory_order_release);

    mainCommandParser->addSubCommand(swg_new<UtinniCommandParser>());

    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot. Stack-snapshot
    // via dispatchSnapshot keeps the path heap-free.
    dispatchSnapshot(addCommandParserCallback, addCommandParserCallbackMutex,
                     [](void (*func)(CommandParser*))
                     { func(mainCommandParser); });

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

// Phase H (Issue #11): override SwgCuiChatWindow's chatEnter wrapper at
// 0x00F3E420. Under editor injection, SWG's input-map context routing is
// broken and fires chatEnter for in-game Enter even when chat is in
// display mode. The original handler does helper(1)+enableTextInput(false)
// -- a no-op in display mode. Replace display-mode behavior with "open chat
// input" (what the missing openChat action should have done). In input
// mode, fall through to the original handler so submit+close behaves
// normally.
void __fastcall hkChatEnter(swgptr pThis, swgptr EDX)
{
    // 2026-05-30 (V1 smoke): the display-mode override below drives
    // enableTextInput(..., setKeyboardInput=true), which walks SWG's CUI
    // keyboard-focus path. That path is only initialized once a ground scene
    // is live. Pressing Enter at the intro/login screen -- or during the
    // intro->login hkCleanupScene transition -- hard-AVs SWG (no crash log;
    // SWG's own dumper never runs). Phase H was only ever validated in-world
    // (Issue #11, Tatooine). Gate the override on an active ground scene;
    // when not in-world, fall through to SWG's own chatEnter handler, which
    // is exactly the code that would run without our detour (so it is at
    // least as safe as stock SWG). GroundScene::get() reads a static global
    // SWG pointer (0 pre-world) -- the same null-guard pattern as
    // debug_camera.cpp::processIoEvent.
    if (!s_chatInputActive.load(std::memory_order_relaxed) && GroundScene::get() != nullptr)
    {
        utinni::log::info("hkChatEnter: chat is in display mode -- overriding to open chat input (was: submit+close)");
        // enableTextInput pointer is the post-detour trampoline; it goes
        // straight to SWG without re-entering our hook, so update tracker
        // manually. WR-05 atomic.
        swg::cuiChatWindow::enableTextInput(pThis, true, true, false);
        s_chatInputActive.store(true, std::memory_order_relaxed);
        return;
    }
    // Input mode (legitimate submit+close), or not in a world scene
    // (login/intro/teardown -- override is unsafe there): pass through to the
    // original chatEnter handler.
    swg::cuiChatWindow::chatEnterHandler(pThis);
}

// Bucket A (v9) advertised-client publish. The MI ctor is un-addressable (OMIT) on the
// advertised client, so hkCtor never runs there and pCuiChatWindow would stay null -> every
// method hook reads a null instance (the prior advertised-unlock crash). The provider advertises
// the SOLE construction funnel cuiChatWindow::createNewWindow (v4); detour IT to publish the live
// instance the method hooks depend on. __cdecl factory; returns the new SwgCuiChatWindow*.
swgptr __cdecl hkCreateNewWindow(swgptr uiPage, int sceneType, swgptr stdString)
{
    swgptr result = swg::cuiChatWindow::createNewWindow(uiPage, sceneType, stdString);
    if (result != 0)
    {
        // Publish ONLY the chat-window instance (the std::atomic the readers load). Deliberately
        // do NOT publish pCuiConsoleHelper from result+0xBC here: that offset is RE'd from Pre-CU
        // and UNVERIFIED on the advertised client, and its sole reader (sendMessage) both null-
        // guards it AND is itself SWGEmu-only (hardcoded nop RVAs) -> leaving it 0 is safe.
        pCuiChatWindow.store(result, std::memory_order_release);
    }
    return result;
}

void CuiChatWindow::detour()
{
    // ctor + its mid-ctor JMP are SWGEmu-ONLY. The MI ctor is NOT advertised (un-addressable), so
    // on the advertised client its RVA (0x00F364B0) + the 0x00F36797 offset are unmapped/relocated;
    // installable() is NECESSARY-not-sufficient there (a stale RVA can land on committed code -> a
    // JMP into the wrong instruction stream -> 0xC0000096). Hard-gate OFF on advertised via
    // isAdvertisedClient(); the advertised path publishes through createNewWindow below instead.
    if (!swg::endpoints::isAdvertisedClient() && swg::endpoints::installable((const void*)swg::cuiChatWindow::ctor))
    {
        swg::cuiChatWindow::ctor = (swg::cuiChatWindow::pCtor)Detour::Create(swg::cuiChatWindow::ctor, hkCtor, DETOUR_TYPE_PUSH_RET);
        memory::createJMP(0x00F36797, (swgptr)midCtor, 6); // Mid CuiChatWindow::ctor detour (SWGEmu offset)
    }

    // Advertised client: publish the live instance via the construction funnel (v4). createNewWindow
    // is null on SWGEmu (where hkCtor handles publish) -> the != nullptr guard skips it there; on the
    // advertised client the resolver filled it with the real factory addr -> installable() passes.
    if (swg::cuiChatWindow::createNewWindow != nullptr &&
        swg::endpoints::installable((const void*)swg::cuiChatWindow::createNewWindow))
    {
        swg::cuiChatWindow::createNewWindow = (swg::cuiChatWindow::pCreateNewWindow)Detour::Create((LPVOID)swg::cuiChatWindow::createNewWindow, hkCreateNewWindow, DETOUR_TYPE_PUSH_RET);
    }

    // DIAG 2026-05-20 Issue #11 Phase E: enabled for caller-tracing. Was
    // commented-out historically (likely because of the pCuiChatWindow vs
    // pThis bug now fixed in hkEnableTextInput). Logs every caller of
    // enableTextInput; nothing is suppressed -- pure passthrough.
    if (swg::endpoints::installable((const void*)swg::cuiChatWindow::enableTextInput))
        swg::cuiChatWindow::enableTextInput = (swg::cuiChatWindow::pEnableTextInput)Detour::Create(swg::cuiChatWindow::enableTextInput, hkEnableTextInput, DETOUR_TYPE_PUSH_RET);

    // Phase H (Issue #11): chatEnter override (see hkChatEnter above).
    if (swg::endpoints::installable((const void*)swg::cuiChatWindow::chatEnterHandler))
        swg::cuiChatWindow::chatEnterHandler = (swg::cuiChatWindow::pChatEnterHandler)Detour::Create((LPVOID)swg::cuiChatWindow::chatEnterHandler, hkChatEnter, DETOUR_TYPE_PUSH_RET);
}

} // namespace utinni
