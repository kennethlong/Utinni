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

#include "utinni.h"
#include "clr.h"

#include <atomic>

#include "plugin_framework/plugin_manager.h"
#include "swg/appearance/skeleton.h"
#include "swg/camera/debug_camera.h"
#include "swg/client/client.h"
#include "swg/game/game.h"
#include "swg/graphics/graphics.h"
#include "swg/misc/config.h"
#include "swg/endpoints.h"
#include "swg/misc/direct_input.h"
#include "swg/misc/io_win.h"
#include "swg/misc/tree_file.h"
#include "swg/object/creature_object.h"
#include "swg/scene/client_world.h"
#include "swg/scene/ground_scene.h"
#include "swg/ui/cui_manager.h"
#include "swg/ui/imgui_impl.h"
#include "swg/ui/cui_chat_window.h"
#include "swg/ui/cui_radial_menu.h"
#include "swg/ui/cui_hud.h"
#include "swg/ui/cui_menu.h"
#include "swg/ui/cui_misc.h"
#include "swg/ui/cui_io.h"
#include "swg/graphics/directx9.h"
#include "swg/graphics/shader.h"
#include "swg/graphics/post_processing.h"
#include "swg/scene/render_world.h"

std::string path;
std::string swgOverrideCfgFilename = "utinni.cfg";

static utinni::UtINI ini;
utinni::PluginManager pluginManager;

// 2026-05-19: signal-based sync with Launcher. The Launcher creates a named
// manual-reset event "Local\\UtinniReady_<pid>" and passes the C-string name as
// utinni_init's lpThreadParam. The managed-side Startup.EntryPoint invokes
// utinni_signal_launcher_ready() right before Application.Run blocks, which
// opens the named event and SetEvents it. That unblocks the Launcher's
// WaitForSingleObject so it can restore the PE entry bytes (originally patched
// to EB FE to stall the main thread during injection).
static std::string g_readyEventName;

// 2026-05-31 (Phase 11 / V1 smoke): module handles used by utinniBreakpointVEH
// to filter fatal-exception logging to the SWG client + our own DLL. Set in
// utinni_init before AddVectoredExceptionHandler.
static HMODULE g_utinniModule = nullptr;
static HMODULE g_swgModule = nullptr;

// 2026-06-12 (phantom-forward-walk debug): ini-gated bisect skip flags. Each
// group can be skipped at init via [DebugBisect] keys in ut.ini WITHOUT a
// rebuild, so one binary serves an entire binary search. Keys DEFAULT to false
// (getBool on a missing key returns false), so an unmodified ut.ini runs the
// FULL detour/patch set exactly as before. Set a key = true to SKIP that group.
//
// Groups (split for binary search; INPUT-path group is the prime suspect for
// the persistent forward-walk + god-mode-overlay synthesizer):
//   skipInputGroup  -> GroundScene (draw/update/handleInputMapEvent),
//                      MessageQueue, cuiIo (processEvent), cuiHud
//                      (actionPerformAction/getTarget/gameMenuCtor),
//                      debugCamera detour, creatureObject (setTarget),
//                      AND debugCamera::patch (the unconditional 2-byte NOP of
//                      `xor cl,cl` at 0x0051AA8D inside GroundScene's
//                      input-map handler — see ground_scene 0x0051AA40).
//   skipRenderGroup -> Graphics, renderWorld, shaderPrimitiveSorter,
//                      postProcessing, skeletalAppearance,
//                      ParticleEffectAppearance.
//   skipMiscGroup   -> Client, clientWorld, Game, CuiChatWindow, CuiManager,
//                      cuiMenu, cuiRadialMenuManager, cuiLoginScreen,
//                      SystemMessageManager, report, treefile, config, IoWin,
//                      cuiMisc::patch.
// REVERTABLE: deleting this block + restoring the original call list removes
// all bisect scaffolding. No behavior change unless a skip key is set true.
static bool bisectSkip(const char* key)
{
    const bool skip = ini.getBool("DebugBisect", key);
    char msg[96];
    snprintf(msg, sizeof(msg), "  [DebugBisect] %s = %s", key, skip ? "SKIP" : "run");
    utinni::log::info(msg);
    return skip;
}

void createDetours()
{
    utinni::log::info("Creating detours");

    const bool skipInput = bisectSkip("skipInputGroup");
    const bool skipRender = bisectSkip("skipRenderGroup");
    const bool skipMisc = bisectSkip("skipMiscGroup");

    // Phase 24: on the advertised client (GetEngineHookPoints present) the MISC + INPUT
    // subsystems are SWGEmu-addressed -- their hooks bind/call hardcoded RVAs and
    // ABI-mismatched symbols that are NOT in the advertised catalog (only ~77 of ~230 hook
    // points are advertised this milestone), so installing them corrupts the relocated
    // client. Until those hook points are advertised + ported, run RENDER-only on the
    // advertised client (the overlay path is self-contained in graphics::install -> kickoff).
    // No-op on SWGEmu Pre-CU (export absent) -- the full hook set installs unchanged (D-00).
    const bool advertised = swg::endpoints::isAdvertisedClient();
    if (advertised)
        utinni::log::info("createDetours: advertised client -- RENDER + GAME subsystem active (other MISC/INPUT subsystems unlocked per-subsystem behind live smokes)");

    // 2026-05-19: full detour set restored after bisection (rounds 0-10).
    // The audio-init stall was traced to Client::detour's hkSetupStartInstall
    // writing pStartupData->createOwnWindow=false, which SWG's setupStartDataInstall
    // on the current SWGEmu binary rejects. That field write is now removed
    // (see client.cpp). All other detours+patches were proven innocent by
    // the bisection; restoring them here.

    // --- MISC / UI / lifecycle group ---
    // Phase 24 v4: still wholesale-skipped on the advertised client -- these subsystems hook
    // un-advertised / mid-function / NONEXISTENT targets (see 24-PROVIDER-REQUEST-misc-input-
    // editor-unlock.md) and are unlocked one-at-a-time behind their own live smokes.
    if (!skipMisc && !advertised)
    {
        swg::config::detour();

        utinni::Client::detour();
        utinni::clientWorld::detour();
        // utinni::cuiIntro::detour();
        utinni::cuiLoginScreen::detour();
        // utinni::cuiMediatorFactorySetup::detour();
        utinni::treefile::detour();
        utinni::IoWin::detour();
    }

    // --- Bucket A: clean advertised-clean editor un-gates (radial menu / system messages / menu cursor) ---
    // Lifted out of the wholesale-skipped MISC block. Each detour() hooks a SINGLE advertised-clean row
    // (cuiRadialMenuManager::update / systemMessageManager::receiveMessage / cuiMenu::infoTypesFindDefaultCursor)
    // and already self-gates on installable() of that primary -> the resolver rebinds the SWGEmu literal to the
    // relocated provider entry before this runs, so installable() is authoritative on the advertised client and
    // each installs only when its row resolved. No unadvertised secondary hooks (unlike chat/CuiManager), so no
    // isAdvertisedClient() carve-out needed. Behavior-identical on SWGEmu (advertised==false -> !skipMisc) -- D-00.
    if (!skipMisc)
    {
        utinni::cuiRadialMenuManager::detour();
        utinni::SystemMessageManager::detour();
        utinni::cuiMenu::detour();
    }

    // --- Phase 24 v4: GAME subsystem -- FIRST advertised-client editor-unlock (live-smoke-gated) ---
    // Lifted OUT of the wholesale-skipped MISC block so it runs on BOTH targets. Game::detour() is
    // now per-target installable()-gated internally (game.cpp): on SWGEmu every literal is mapped ->
    // the full game hook set installs byte-for-byte unchanged (D-00); on the advertised client it
    // installs the advertised game entry points -- mainLoop -> the provider's real per-frame tick
    // Game::runGameLoopOnce (v4 4a, exact __cdecl(bool,HWND,int,int) match), install, setupScene,
    // cleanupScene -- and reads the loop counter via the advertised g_mainLoopCounter accessor (4b).
    // Still honors the [DebugBisect] skipMisc bisect flag. The OTHER MISC subsystems stay skipped
    // above until each is unlocked + smoked in turn.
    if (!skipMisc)
    {
        utinni::Game::detour();
    }

    // --- WS-0b: DirectInput -- lifted OUT of the advertised-skipped MISC block (it used to run inside
    // Client::detour()) so it installs on BOTH targets. On SWGEmu this is identical to before (it was
    // reached via Client::detour() under `!skipMisc && !advertised`; with advertised==false that equals
    // `!skipMisc`). On the advertised client it now installs the DirectInput8Create import detour (keyboard
    // vtable capture -> the staged-disarmed Enter-mask + the SetCooperativeLevel embed shim); the SWGEmu
    // setupInstall RVA detour inside is installable()-gated off there. Honors the skipMisc bisect flag.
    if (!skipMisc)
    {
        utinni::DirectInput::detour();
    }

    // --- WS-4: first MISC editor-unlock SLICE on the advertised client (smallest safe increment) ---
    // report::print is an advertised CLEAN row -- lifted OUT of the wholesale-skipped MISC block so it
    // installs on BOTH targets, with report::detour() internally installable()-gated (swg_misc.cpp). On
    // SWGEmu this is behavior-identical to the old MISC-block reach (advertised==false -> !skipMisc); on
    // the advertised client SWG's report/debug messages now forward into utinni.log (pure pass-through,
    // no scene/player/RVA state). This proves the per-target MISC-lift pattern; subsequent editor slices
    // (config, CuiManager::render, ...) follow the same shape, each behind its own live smoke.
    if (!skipMisc)
    {
        utinni::report::detour();
    }

    // --- WS-4: second MISC slice -- CuiManager render-split on the advertised client ---
    // Lifted out of the wholesale-skipped MISC block. CuiManager::detour() is internally SPLIT
    // (cui_manager.cpp): the advertised-clean `render` row installs on both targets (its isRenderingUi
    // flag is read only by the D3D9 wireframe path -> inert on the D3D11 advertised client); the
    // unadvertised `findObjectUnderCursor` literal is isAdvertisedClient()-gated OFF (installable() is
    // insufficient for a stale literal). Behavior-identical on SWGEmu (advertised==false -> !skipMisc) -- D-00.
    if (!skipMisc)
    {
        utinni::CuiManager::detour();
    }

    // --- Bucket A: CHAT editor unlock on the advertised client (first per-editor §2.A slice) ---
    // Lifted out of the wholesale-skipped MISC block. CuiChatWindow::detour() is internally per-target:
    // the un-addressable MI ctor + its mid-ctor JMP are isAdvertisedClient()-gated OFF (SWGEmu-only);
    // on the advertised client the live instance is published through the advertised v4 construction
    // funnel createNewWindow (hkCreateNewWindow), and the v3 real-entry method hooks (enableTextInput,
    // chatEnterHandler) install on both. Behavior-identical on SWGEmu (advertised==false -> !skipMisc) -- D-00.
    if (!skipMisc)
    {
        utinni::CuiChatWindow::detour();
    }

    // --- INPUT / EVENT / MOVEMENT / LOCOMOTION group (prime suspect) ---
    if (!skipInput && !advertised)
    {
        utinni::creatureObject::detour();
        utinni::cuiHud::detour();
        utinni::cuiIo::detour();
        utinni::debugCamera::detour();
        utinni::MessageQueue::detour(); // Phase G (Issue #11): instrument input-map output
    }

    // --- WS-4 Terrain PROBE: GroundScene -- lifted out of the advertised-skipped INPUT block ---
    // Honors skipInput. On SWGEmu all of GroundScene::detour() installs (D-00). On the advertised client its
    // internal split installs ONLY `update` (skips draw/handleInputMapEvent/setPreloadSnapshot there), so
    // hkUpdateLoop can log whether the advertised real-entry fires with a stable pThis -- the precondition for
    // the Option-A instance latch. GroundScene::get() is UNCHANGED here (still nullptr on advertised): the
    // probe must pass before we widen the shared accessor (Codex+Cursor HIGH: flipping get() re-enables every
    // Get() consumer, incl. the GroundSceneImpl per-frame Tier-2 Terrain.Get() poll).
    if (!skipInput)
    {
        utinni::GroundScene::detour();
    }

    // --- RENDER group ---
    if (!skipRender)
    {
        // graphics::* ARE advertised -> the resolver overwrites the hardcoded literals with
        // valid relocated addresses, so this drives the overlay kickoff (graphics::install)
        // and is safe on both targets.
        utinni::Graphics::detour();

        // Phase 24: these 5 detour HARDCODED SWGEmu absolute addresses (shaderPrimitiveSorter
        // 0x00773E39, renderWorld 0x00766DE0, bloom 0x0064B500, + particle/skeletal) and are
        // NOT in the advertised .inc catalog, so the resolver never overwrites them. On the
        // advertised client those literals land on UNRELATED relocated code -- live-confirmed:
        // all three symbolize to the §1 client's CuiStringIds static-init region -- and
        // installable() (committed+executable only) WRONGLY passes, so Detour::Create writes a
        // JMP into that code and corrupts it -> 0xC0000096 privileged-instruction crash during
        // the client's CuiStringIds dynamic-init at startup (ASLR-roulette; same failure class
        // as the getSwgWndProcExport stale-RVA bug). None are needed for the overlay (which
        // graphics::install self-contains). Skip on the advertised client until they are
        // advertised + ported.
        if (!advertised)
        {
            utinni::ParticleEffectAppearance::detour();
            utinni::skeletalAppearance::detour();
            utinni::renderWorld::detour();
            utinni::shaderPrimitiveSorter::detour();
            utinni::postProcessing::detour();
        }
    }

    // --- Phase 24 v3: advertised-client editor unlock -- DEFERRED (per-subsystem live work) ---
    // groundScene + cuiChatWindow are per-target installable()-gated (commit d3b5468) and the
    // resolver binds their v3 real-entry targets, but a live-smoke of the selective unlock
    // showed that ENABLING their detours on the advertised client hits per-HOOK runtime
    // crashes (not just install-time address issues): the trailing setPreloadSnapshot write
    // (now gated), then a client-side std::string fault (0xC0000005 READ 0xFFFFFFFF) ~1s after
    // load. installable() / the memory guards cannot catch these -- each hook needs its own
    // advertised-client ABI + state-precondition live-verification (e.g. cuiChatWindow's method
    // hooks vs its ctor hook, which is gated off because the ctor is not advertised). So the
    // advertised client stays RENDER-only here; the bindings + per-target gating remain ready
    // for a per-hook follow-on. The DX11 embed-resize fix (client::wndProc binding) is
    // independent of this unlock and stays active.
}

void createPatches()
{
    utinni::log::info("Creating patches");

    const bool skipInput = ini.getBool("DebugBisect", "skipInputGroup");
    const bool skipMisc = ini.getBool("DebugBisect", "skipMiscGroup");

    // Phase 24: same advertised-client RENDER-only policy as createDetours -- these patches
    // write to hardcoded SWGEmu addresses that are wrong on the relocated client.
    const bool advertised = swg::endpoints::isAdvertisedClient();

    // cuiMisc::patch is itself fully gated on enableOfflineScenes; group it with
    // MISC for completeness.
    if (!skipMisc && !advertised)
    {
        utinni::cuiMisc::patch();
    }

    // debugCamera::patch performs an UNCONDITIONAL 2-byte NOP of `xor cl,cl` at
    // 0x0051AA8D inside GroundScene::handleInputMapEvent (0x0051AA40). It alters
    // input-map control flow ("enable mouse wheel to debugCamera"). Grouped with
    // INPUT so the bisect can disable the only unconditional code-write that sits
    // directly on the locomotion input path.
    if (!skipInput && !advertised)
    {
        utinni::debugCamera::patch();
    }
}

// DIAG 2026-05-19: Vectored Exception Handler to log first-chance int 3
// (EXCEPTION_BREAKPOINT) events. Source-level analysis points at SWG's
// InternalFatal (Fatal.cpp:156) ending in `__asm int 3` followed by code
// that should run but apparently parks at a `jmp $` — meaning either a
// pre-existing VEH on this process consumes the int 3, or SWG's SEH
// returns into a halt sequence. By logging EIP at int 3, we identify the
// exact call site without needing a debugger attached.
//
// Returns EXCEPTION_CONTINUE_SEARCH so we don't change behavior — just
// observe. AddVectoredExceptionHandler with FirstHandler=1 ensures we run
// before any other VEH on the process.
static LONG WINAPI utinniBreakpointVEH(PEXCEPTION_POINTERS pInfo)
{
    if (pInfo && pInfo->ExceptionRecord &&
        pInfo->ExceptionRecord->ExceptionCode == EXCEPTION_BREAKPOINT)
    {
        char msg[256];
        // Snapshot 16 bytes around EIP for context (e.g. CC EB FE pattern recognition).
        BYTE* eipBytes = (BYTE*)pInfo->ContextRecord->Eip;
        char hexDump[80] = {0};
        for (int i = -4; i < 12; ++i)
        {
            char b[6];
            // SAFE: VEH runs in-process; EIP is by definition mapped+readable.
            // Bytes just before/after EIP are typically same code page.
            snprintf(b, sizeof(b), "%s%02X", (i == 0 ? "[" : (i == 1 ? "]" : " ")),
                     eipBytes[i]);
            // Avoid overflow.
            if (strlen(hexDump) + strlen(b) < sizeof(hexDump) - 1)
                strcat_s(hexDump, sizeof(hexDump), b);
        }

        // 2026-06-24 (Game-unlock smoke): resolve the int3's MODULE + RVA so a fixed-base
        // engine assert (e.g. SwgClient_r.exe with no ASLR) is symbolizable against the
        // provider's PDB / our .map -- the EIP alone can't be matched across modules.
        const DWORD eip = pInfo->ContextRecord->Eip;
        HMODULE mod = nullptr;
        char modName[64] = "<unmapped-EIP>";
        DWORD modBase = 0;
        if (GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                               (LPCSTR)eip, &mod) &&
            mod != nullptr)
        {
            char full[MAX_PATH];
            if (GetModuleFileNameA(mod, full, sizeof(full)) != 0)
            {
                const char* leaf = strrchr(full, '\\');
                strcpy_s(modName, sizeof(modName), leaf != nullptr ? leaf + 1 : full);
            }
            modBase = (DWORD)(uintptr_t)mod;
        }

        snprintf(msg, sizeof(msg),
                 "VEH int3: EIP=0x%08X module=%s base=0x%08X rva=0x%08X bytes(-4..+11)=%s ESP=0x%08X",
                 eip, modName, modBase, modBase ? (eip - modBase) : eip, hexDump,
                 pInfo->ContextRecord->Esp);
        utinni::log::info(msg);

        // Mapped return-address stack scan (same idiom as the FATAL branch): the top entries
        // are the caller chain into the int3 -- pinpoints which hook/callback reached it.
        {
            const DWORD* sp = (const DWORD*)(uintptr_t)pInfo->ContextRecord->Esp;
            char stk[320] = "VEH int3 stack (mapped return-addr candidates): ";
            int found = 0;
            for (int i = 0; i < 64 && found < 8; ++i)
            {
                const DWORD val = sp[i];
                HMODULE m = nullptr;
                if (GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                                           GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                                       (LPCSTR)(uintptr_t)val, &m) &&
                    (m == g_swgModule || m == g_utinniModule))
                {
                    char e[48];
                    snprintf(e, sizeof(e), "%s+0x%X ", (m == g_swgModule ? "swg" : "utinni"),
                             (DWORD)(val - (DWORD)(uintptr_t)m));
                    if (strlen(stk) + strlen(e) < sizeof(stk) - 1)
                        strcat_s(stk, sizeof(stk), e);
                    ++found;
                }
            }
            if (found > 0)
                utinni::log::warning(stk);
        }
        return EXCEPTION_CONTINUE_SEARCH;
    }

    // 2026-05-31 (Phase 11 / V1 smoke): the intro-skip scene-transition crash
    // left NO SWG stage dump and NO WER event -- SWG's own SEH was bypassed.
    // Capture fatal-class exceptions here so the faulting site is recorded.
    // Filter to EIP inside SWGEmu.exe / UtinniCore.dll (or an unmapped EIP --
    // a wild jump from corruption); this skips the CLR's frequent first-chance
    // managed AVs so the real native crash is not buried. The logged rva
    // (EIP - module base) is directly matchable against SWG's hardcoded RVAs
    // and our hook addresses. flush_on(info) (log.cpp) guarantees the line
    // hits disk before the process dies. Still returns CONTINUE_SEARCH -- pure
    // observation, normal crash handling proceeds unchanged.
    const DWORD code = pInfo->ExceptionRecord->ExceptionCode;
    switch (code)
    {
    case EXCEPTION_ACCESS_VIOLATION:
    case EXCEPTION_IN_PAGE_ERROR:
    case EXCEPTION_ILLEGAL_INSTRUCTION:
    case EXCEPTION_PRIV_INSTRUCTION:
    case EXCEPTION_STACK_OVERFLOW:
    case EXCEPTION_DATATYPE_MISALIGNMENT:
    case 0xC0000409: // STATUS_STACK_BUFFER_OVERRUN / __fastfail
    case 0x4000001F: // STATUS_WX86_BREAKPOINT (WOW64 fatal assert/int3)
        break;
    default:
        return EXCEPTION_CONTINUE_SEARCH;
    }

    // Cap total fatal lines so a first-chance storm can't fill the log; the
    // real crash is among the last entries before the process dies.
    static std::atomic<int> s_fatalLogCount{0};
    if (s_fatalLogCount.load(std::memory_order_relaxed) >= 64)
    {
        return EXCEPTION_CONTINUE_SEARCH;
    }

    const DWORD eip = pInfo->ContextRecord->Eip;
    HMODULE mod = nullptr;
    const bool resolved =
        GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           (LPCSTR)eip, &mod) != 0;

    // Skip exceptions originating in the CLR / system modules (noise).
    if (resolved && mod != g_swgModule && mod != g_utinniModule)
    {
        return EXCEPTION_CONTINUE_SEARCH;
    }

    s_fatalLogCount.fetch_add(1, std::memory_order_relaxed);

    char modName[64] = "<unmapped-EIP>";
    DWORD modBase = 0;
    if (resolved && mod != nullptr)
    {
        char full[MAX_PATH];
        if (GetModuleFileNameA(mod, full, sizeof(full)) != 0)
        {
            const char* leaf = strrchr(full, '\\');
            strcpy_s(modName, sizeof(modName), leaf != nullptr ? leaf + 1 : full);
        }
        modBase = (DWORD)(uintptr_t)mod;
    }

    // For AV / in-page error, ExceptionInformation[0] = 0 read / 1 write / 8 DEP,
    // ExceptionInformation[1] = the target address that faulted.
    char avDetail[64] = "";
    if ((code == EXCEPTION_ACCESS_VIOLATION || code == EXCEPTION_IN_PAGE_ERROR) &&
        pInfo->ExceptionRecord->NumberParameters >= 2)
    {
        const ULONG_PTR op = pInfo->ExceptionRecord->ExceptionInformation[0];
        const ULONG_PTR tgt = pInfo->ExceptionRecord->ExceptionInformation[1];
        snprintf(avDetail, sizeof(avDetail), " %s target=0x%08X",
                 op == 1 ? "WRITE" : (op == 8 ? "EXEC" : "READ"), (DWORD)tgt);
    }

    char fmsg[320];
    snprintf(fmsg, sizeof(fmsg),
             "VEH FATAL: code=0x%08X EIP=0x%08X module=%s base=0x%08X rva=0x%08X ESP=0x%08X%s",
             code, eip, modName, modBase, resolved ? (eip - modBase) : eip,
             pInfo->ContextRecord->Esp, avDetail);
    utinni::log::warning(fmsg);

    // Wild-jump backtrace: when EIP is unmapped/garbage (a corrupted CALL/JMP) it can't be
    // symbolized, so scan the live stack upward from ESP for values that fall inside
    // SwgClient_r.exe / UtinniCore.dll code and log them as rva candidates -- the top one is
    // typically the return address pushed by the faulting call, i.e. the CALLER. SAFE: reading
    // UPWARD from ESP walks toward the (committed) stack base; bounded to 64 slots.
    {
        const DWORD* sp = (const DWORD*)(uintptr_t)pInfo->ContextRecord->Esp;
        char stk[320] = "VEH stack (mapped return-addr candidates): ";
        int found = 0;
        for (int i = 0; i < 64 && found < 8; ++i)
        {
            const DWORD val = sp[i];
            HMODULE m = nullptr;
            if (GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                                       GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                                   (LPCSTR)(uintptr_t)val, &m) &&
                (m == g_swgModule || m == g_utinniModule))
            {
                char e[48];
                snprintf(e, sizeof(e), "%s+0x%X ", (m == g_swgModule ? "swg" : "utinni"),
                         (DWORD)(val - (DWORD)(uintptr_t)m));
                if (strlen(stk) + strlen(e) < sizeof(stk) - 1)
                    strcat_s(stk, sizeof(stk), e);
                ++found;
            }
        }
        if (found > 0)
            utinni::log::warning(stk);
    }
    return EXCEPTION_CONTINUE_SEARCH;
}

// C-01: utinni_init runs synchronously on the launcher-spawned thread.
// Launcher's WaitForSingleObject blocks until this returns. Synchronous startup
// is easier to debug than fire-and-forget; bring-up is bounded (CLR init + plugin load).
//
// WINAPI (__stdcall) is required by LPTHREAD_START_ROUTINE (CreateRemoteThread's
// target type). On x86, __stdcall + extern "C" decorates the export name as
// `_utinni_init@4`; the launcher's GetProcAddress("utinni_init") cannot find the
// decorated form. The /EXPORT linker directive below adds an alias so BOTH names
// resolve to the same RVA in UtinniCore.dll's export table.
//
// Why this slipped Phase 02 Plan 02-03's harness: the LoaderLockHarness only
// measures LoadLibraryA("UtinniCore.dll") timing -- it never calls GetProcAddress
// for utinni_init. The mismatch is invisible to that harness. Caught by the live
// SWG manual UAT on 2026-05-18 (the Tier-4 residual doing its job).
#pragma comment(linker, "/EXPORT:utinni_init=_utinni_init@4")
extern "C" __declspec(dllexport) DWORD WINAPI utinni_init(LPVOID lpThreadParam)
{
    // 2026-05-19: capture the ready-event name passed by the Launcher.
    // Stored in g_readyEventName for utinni_signal_launcher_ready() to use.
    // Defensive: if lpThreadParam is null (e.g. tests invoking utinni_init
    // directly, or an older Launcher that doesn't pass the name), the signal
    // export becomes a no-op and the Launcher's wait will simply time out --
    // not a crash.
    if (lpThreadParam != nullptr)
    {
        g_readyEventName = static_cast<const char*>(lpThreadParam);
    }

    char dllPathbuffer[MAX_PATH];
    HMODULE handle = nullptr;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, (LPCSTR)&createDetours, &handle);
    GetModuleFileNameA(handle, dllPathbuffer, sizeof(dllPathbuffer));
    std::string dllPath = std::string(dllPathbuffer);
    path = dllPath.substr(0, dllPath.find_last_of("\\/")) + "\\";

    utinni::log::create();

    // DIAG 2026-05-19: register VEH BEFORE anything else so int 3 events
    // from any source (CRT init, SWG fatal, library asserts) get logged.
    // FirstHandler=1 → our handler runs before other VEHs.
    // 2026-05-31: capture the SWG exe + our DLL module handles first so the
    // VEH can filter fatal-exception logging to those two modules.
    g_utinniModule = handle;                 // UtinniCore.dll (from &createDetours)
    g_swgModule = GetModuleHandleA(nullptr); // the injected SWG client exe
    AddVectoredExceptionHandler(1, utinniBreakpointVEH);

    ini.createUtinniSettings();
    ini.load(path + "ut.ini");

    utinni::Client::setEditorMode(ini.getBool("Editor", "enableEditorMode"));
    imgui_impl::enableInternalUi(ini.getBool("UtinniCore", "enableInternalUi"));

    // CR-04/WR-03: Eagerly initialize native objects that hkPresent (render thread) needs.
    // These calls are BEFORE createDetours() so hkPresent cannot fire before they complete.
    // CON-H-01: running in utinni_init (launcher remote thread), NOT DllMain.
    // CON-N-01: these are NOT Detour::Create calls.
    directX::initPresentBlockedEvent(); // CR-04: hPresentBlockedEvent eager init
    directX::initDepthTexture();        // WR-03: depthTexture eager init

    // Phase 24 / EPA-02: the SINGLE dual-path branch. Probe the advertised
    // GetEngineHookPoints() export and, on the advertised client, overwrite the
    // swg::* function-pointer literals BY NAME *before* createDetours() runs --
    // fixing the first-detour 0xC0000005 READ crash (the detour-length disassembly
    // reads bytes at the wrong hardcoded RVA otherwise). On SWGEmu Pre-CU (export
    // absent) this is a strict no-op and the RVA literals are used exactly as today
    // (D-00). Reuses the already-captured g_swgModule. CON-H-01: this runs in
    // utinni_init (launcher remote thread), NEVER DllMain.
    swg::endpoints::resolveFromExe();
    utinni::log::info("endpoints: resolver ran before createDetours()");

    // Adds hooks to functions inside the game
    createDetours();

    // Patches memory instructions inside the game
    createPatches();

    utinni::log::info("Loading C++ plugins");
    pluginManager.loadPlugins();

    // WR-09 (REVISED 2026-05-18 after live UAT): CoInitializeEx MUST stay.
    //
    // Phase 02.1 Plan 02.1-01 attempted to remove this call under the rationale
    // that "CLR host is free-threaded; no COM consumer in utinni_init call chain".
    // That analysis missed the IMPLICIT consumer: WinForms drag-and-drop
    // registration (`RegisterDragDrop`, called by `Control.SetAcceptDrops`) requires
    // the calling thread to be in STA mode. PanelGame.cs and other editor controls
    // throw `System.InvalidOperationException: DragDrop registration did not
    // succeed --- ThreadStateException: Current thread must be set to single thread
    // apartment (STA) mode before OLE calls can be made` if this CoInitializeEx is
    // absent.
    //
    // docs/ai/injection.md line 167 documents this explicitly:
    //   "CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED) — for COM types used by
    //    the editor (drag-drop and any OLE-flavoured WinForms behaviour)."
    //
    // Phase 02.1's grep-based audit (RESEARCH.md §3) only scanned UtinniCore native
    // sources for `CoCreateInstance` / `CoTaskMemAlloc` and concluded "no consumer"
    // — missing the managed-side WinForms surface entirely. The C-01 manual UAT on
    // 2026-05-18 caught the regression; this comment is the post-mortem trace.
    //
    // Pairing concern: there is no matching CoUninitialize. The utinni_init thread
    // returns 0 immediately after clr::load() returns. But clr::load() doesn't
    // return until CLR shutdown (Application.Run blocks until FormMain closes), so
    // utinni_init's thread effectively IS the CLR's main thread for process
    // lifetime. The per-thread apartment refcount "leak" is bounded by process
    // lifetime — conventional Win32 main-thread COM init pattern; OS reclaims on
    // process exit. NOT a bug; accepted as documented.
    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);

    utinni::log::info("Loading .NET plugins");
    // Load the clr and UtinniCoreDotNet
    clr::load();

    return 0;
}

void detach()
{
    directX::cleanup();
    clr::stop();
}

// 2026-05-19: managed Startup.EntryPoint calls this right before Application.Run
// to release the Launcher's WaitForSingleObject on the named ready event. By the
// time this fires, createDetours()/createPatches() have run, C++ plugins are
// loaded, and managed *Callbacks.Initialize() have registered their delegates --
// so the main thread is safe to release into SWG's WinMain. See utinni.cpp top
// for the full sync rationale and Launcher/main.cpp loadDll() for the wait side.
extern "C" __declspec(dllexport) void __cdecl utinni_signal_launcher_ready()
{
    if (g_readyEventName.empty())
    {
        utinni::log::critical("utinni_signal_launcher_ready: g_readyEventName is empty -- Launcher will time out");
        return;
    }

    HANDLE h = OpenEventA(EVENT_MODIFY_STATE, FALSE, g_readyEventName.c_str());
    if (h == nullptr)
    {
        char msg[160];
        snprintf(msg, sizeof(msg),
                 "utinni_signal_launcher_ready: OpenEventA('%s') failed (GetLastError=0x%08lX)",
                 g_readyEventName.c_str(), GetLastError());
        utinni::log::critical(msg);
        return;
    }

    if (!SetEvent(h))
    {
        char msg[160];
        snprintf(msg, sizeof(msg),
                 "utinni_signal_launcher_ready: SetEvent failed (GetLastError=0x%08lX)",
                 GetLastError());
        utinni::log::critical(msg);
    }
    else
    {
        char msg[160];
        snprintf(msg, sizeof(msg),
                 "utinni_signal_launcher_ready: signaled '%s'",
                 g_readyEventName.c_str());
        utinni::log::info(msg);
    }

    CloseHandle(h);
}

// C-01: DllMain MUST complete in microseconds. Heavy startup (CLR + plugin load) moved
// to utinni_init, fired by the launcher via a second CreateRemoteThread after this DLL
// is loaded. DllMain does ONLY DisableThreadLibraryCalls + return TRUE on attach.
BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved)
{
    switch (fdwReason)
    {
    case DLL_PROCESS_ATTACH:
        DisableThreadLibraryCalls(hinstDLL);
        return TRUE;

    case DLL_PROCESS_DETACH:
        detach();
        return TRUE;
    }
    return TRUE;
}

namespace utinni
{
const std::string& getPath()
{
    return path;
}

const std::string& getSwgCfgFilename()
{
    return swgOverrideCfgFilename;
}

UtINI& getConfig()
{
    return ini;
}

PluginManager& getPluginManager()
{
    return pluginManager;
}
} // namespace utinni
