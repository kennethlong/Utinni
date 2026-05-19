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

#include "plugin_framework/plugin_manager.h"
#include "swg/appearance/skeleton.h"
#include "swg/camera/debug_camera.h"
#include "swg/client/client.h"
#include "swg/game/game.h"
#include "swg/graphics/graphics.h"
#include "swg/misc/config.h"
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

void createDetours()
{
    utinni::log::info("Creating detours");

    // BISECT 2026-05-19 ROUND 1: first-half ENABLED, second-half DISABLED.
    // Round 0 (everything off) lost the [SWG] log prefix because report::detour
    // is the hook that captures SWG's print output. We re-enable report so we
    // can see SWG's progression, and bisect the rest in halves.
    //
    // Outcomes:
    //   - SWG stalls again at "Audio: Finished initializing" → culprit is in
    //     this enabled half (or in report itself, but RVA 0x00A88F90 was
    //     already proven correct because it captured logs in earlier runs).
    //   - SWG logs progress past audio init → culprit is in the still-disabled
    //     second half.

    swg::config::detour();
    utinni::Client::detour();
    utinni::clientWorld::detour();
    utinni::creatureObject::detour();
    utinni::CuiChatWindow::detour();
    utinni::CuiManager::detour();
    utinni::cuiHud::detour();
    utinni::cuiIo::detour();
    //utinni::cuiIntro::detour();
    utinni::cuiMenu::detour();
    utinni::cuiRadialMenuManager::detour();
    utinni::cuiLoginScreen::detour();
    //utinni::cuiMediatorFactorySetup::detour();
    utinni::debugCamera::detour();

    // Re-enabled so we capture [SWG] log lines and can see how far init goes.
    utinni::report::detour();

    // BISECT 2026-05-19 ROUND 1: second-half still disabled.
    // utinni::Game::detour();
    // utinni::GroundScene::detour();
    // utinni::Graphics::detour();
    // utinni::ParticleEffectAppearance::detour();
    // utinni::skeletalAppearance::detour();
    // utinni::SystemMessageManager::detour();
    // utinni::treefile::detour();
    // utinni::renderWorld::detour();
    // utinni::shaderPrimitiveSorter::detour();
    // utinni::IoWin::detour();
    // utinni::postProcessing::detour();
}

void createPatches()
{
    utinni::log::info("Creating patches");

    // BISECT 2026-05-19 ROUND 1: patches still disabled.
    // utinni::cuiMisc::patch();
    // utinni::debugCamera::patch();
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
    char dllPathbuffer[MAX_PATH];
    HMODULE handle = nullptr;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, (LPCSTR)&createDetours, &handle);
    GetModuleFileNameA(handle, dllPathbuffer, sizeof(dllPathbuffer));
    std::string dllPath = std::string(dllPathbuffer);
    path = dllPath.substr(0, dllPath.find_last_of("\\/")) + "\\";

    utinni::log::create();

    ini.createUtinniSettings();
    ini.load(path + "ut.ini");

    utinni::Client::setEditorMode(ini.getBool("Editor", "enableEditorMode"));
    imgui_impl::enableInternalUi(ini.getBool("UtinniCore", "enableInternalUi"));

    // CR-04/WR-03: Eagerly initialize native objects that hkPresent (render thread) needs.
    // These calls are BEFORE createDetours() so hkPresent cannot fire before they complete.
    // CON-H-01: running in utinni_init (launcher remote thread), NOT DllMain.
    // CON-N-01: these are NOT Detour::Create calls.
    directX::initPresentBlockedEvent();  // CR-04: hPresentBlockedEvent eager init
    directX::initDepthTexture();         // WR-03: depthTexture eager init

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

void detatch()
{
    directX::cleanup();
    clr::stop();
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
        detatch();
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
}
