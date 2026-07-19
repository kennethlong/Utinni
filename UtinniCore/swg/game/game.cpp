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

#include "game.h"
#include "game_test_internal.h"
#include "utinni.h"
#include "swg/endpoints.h"
#include <atomic>
#include <mutex>
#include <vector>
#include "swg/client/client.h"
#include "swg/misc/config.h"
#include "swg/scene/ground_scene.h"
#include "swg/scene/world_snapshot.h"
#include "swg/misc/network.h"
#include "swg/object/client_object.h"
#include "swg/object/object.h"
#include "swg/ui/imgui_impl.h"

// WS-4: drop the advertised GroundScene reload latch on scene teardown (defined in ground_scene.cpp).
// Global-scope forward-decl (the hooks live in namespace utinni; declaring it there would create a phantom
// utinni::swg). Same lightweight forward-decl pattern as direct_input's isEditorSceneLoaded.
namespace swg::groundScene
{
void clearAdvertisedInstance();
} // namespace swg::groundScene

namespace swg::game
{
using pInstall = void(__cdecl*)(int applicationType);
using pQuit = void(__cdecl*)();
using pMainLoop = void(__cdecl*)(bool presentToWindow, HWND hwnd, int width, int height);

using pSetupScene = void(__cdecl*)(utinni::GroundScene* newScene);
using pCleanupScene = void(__cdecl*)();

using pGetPlayer = utinni::Object*(__cdecl*)();
using pGetPlayerCreatureObject = utinni::Object*(__cdecl*)();

using pGetCamera = utinni::Camera*(__cdecl*)();
using pGetConstCamera = const utinni::Camera*(__cdecl*)();

using pIsViewFirstPerson = bool(__cdecl*)();
using pIsHudSceneTypeSpace = bool(__cdecl*)();

pInstall install = (pInstall)0x00422E80;
pQuit quit = (pQuit)0x00423720;
pMainLoop mainLoop = (pMainLoop)0x004237C0;

pSetupScene setupScene = (pSetupScene)0x00424220;
pCleanupScene cleanupScene = (pCleanupScene)0x00423700;

pGetPlayer getPlayer = (pGetPlayer)0x00425140;
pGetPlayerCreatureObject getPlayerCreatureObject = (pGetPlayerCreatureObject)0x004251D0;

pGetCamera getCamera = (pGetCamera)0x00425BB0;
pGetConstCamera getConstCamera = (pGetConstCamera)0x00425BE0;

pIsViewFirstPerson isViewFirstPerson = (pIsViewFirstPerson)0x00425C10;
pIsHudSceneTypeSpace isHudSceneTypeSpace = (pIsHudSceneTypeSpace)0x00426170;

// Phase 24 / D-04 accessor-style global. The provider advertises game::g_runningFlags
// as the STATIC ACCESSOR &Game::isOver (call-not-read): there is no addressable raw
// global, so the only legitimate handle is the bool() accessor. There is NO SWGEmu RVA
// literal -- the consumer's isSafeToUse() reads two engine safety-flag globals directly
// via memory::read on the SWGEmu path. The slot starts null and resolves only on the
// advertised client; the read-site (Task 2) calls !isOver() when non-null, else the
// existing two-bool memory::read expression.
using pIsOver = bool(__cdecl*)();
pIsOver g_runningFlags = nullptr;

// Phase 24 v4 / D-04 accessor-style global. The main-loop counter (Game::ms_loops) is a
// private static with only an INLINE getLoopCount() (no ODR address) -- the read-site below
// reads the SWGEmu global at 0x1908830 directly. The provider added an out-of-line
// Game::getMainLoopCount() and advertises game::g_mainLoopCounter -> &Game::getMainLoopCount
// (call-not-read). The slot starts null and resolves only on the advertised client; the
// read-site (getMainLoopCount) calls it when non-null, else the 0x1908830 read (D-00).
using pMainLoopCount = int(__cdecl*)();
pMainLoopCount g_mainLoopCounter = nullptr;

// v6 (24): full SceneCreator string-based scene load. Provider advertises game::loadScene ->
// utinni_gameLoadScene -> Game::setScene(true, terrain, player, nullptr) (the whole _setScene
// (SceneCreator&)/_startScene lifecycle). The slot starts null and resolves only on the
// advertised client; hkMainLoop drives it instead of building a GroundScene there (D-00).
using pLoadScene = void(__cdecl*)(const char* terrain, const char* player);
pLoadScene loadScene = nullptr;

// v16 (24 / Goal A+): player lookAt-target id READ. Provider advertises
// game::getPlayerLookAtTargetId -> extern "C" utinni_getPlayerLookAtTargetId: the player
// CreatureObject's lookAt/selection-target NetworkId VALUE (full 64 bits; 0 = no player /
// no target). Primitive-only shim per the sysmsg rev-2 ABI rule (getLookAtTarget() is inline
// + returns const CachedNetworkId& -- a Watcher-bearing type the consumer does not model).
// The slot starts null and resolves only on the advertised client; the read-site is
// Game::getPlayerLookAtTargetObject()'s advertised branch (D-00: SWGEmu path untouched).
using pGetPlayerLookAtTargetId = int64_t(__cdecl*)();
pGetPlayerLookAtTargetId getPlayerLookAtTargetId = nullptr;

// WS-0/WS-1 (advertised-client editor unlock): consumer-maintained "advertised editor scene loaded" flag.
// The advertised contract exposes NO isInWorld/getScene signal, so this is the closest proxy for "an
// editor-initiated scene is live": set true after the advertised swg::game::loadScene returns in hkMainLoop,
// cleared at the TOP of hkCleanupScene (before teardown). It is NOT an engine in-world truth -- do not
// over-trust it as one. Read by the DI Enter-mask (direct_input.cpp) so in-world Enter is masked only while
// a scene is loaded, never on login/character-select. std::atomic (relaxed): set/cleared on the game thread,
// read on the DI-pump thread (same cross-thread pattern as direct_input's s_suppressExclusiveFullscreen). On
// SWGEmu it is never set true (the advertised loadScene branch is skipped) and nothing reads it -> inert (D-00).
std::atomic<bool> g_editorSceneLoaded{false};
bool isEditorSceneLoaded()
{
    return g_editorSceneLoaded.load(std::memory_order_relaxed);
}
} // namespace swg::game

// Phase 3 R-A native-side (per 03-CONTEXT D-08/D-09): handle-based registries
// backed by insertion-order std::vector<{handle, fn_ptr}> + monotonic next-id.
// Handle 0 is reserved as the invalid sentinel.
//
// CR-01 (03-REVIEW): per-registry std::mutex serializes Subscribe / Unsubscribe
// writes against the snapshot-build read in the dispatch sites. Mirrors the
// managed-side `lock` discipline (e.g. GameCallbacks.cs xxxLock). Dispatch
// copies under the lock, iterates the snapshot outside the lock so callbacks
// can re-subscribe without deadlock.
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

static std::vector<CallbackEntry<void (*)()>> installCallbacks;
static std::vector<CallbackEntry<void (*)()>> preMainLoopCallbacks;
static std::vector<CallbackEntry<void (*)()>> mainLoopCallbacks;
static std::vector<CallbackEntry<void (*)()>> setSceneCallbacks;
static std::vector<CallbackEntry<void (*)()>> cleanUpSceneCallbacks;
static std::mutex installCallbacksMutex;
static std::mutex preMainLoopCallbacksMutex;
static std::mutex mainLoopCallbacksMutex;
static std::mutex setSceneCallbacksMutex;
static std::mutex cleanUpSceneCallbacksMutex;
static int s_nextInstallId = 1;
static int s_nextPreMainLoopId = 1;
static int s_nextMainLoopId = 1;
static int s_nextSetSceneId = 1;
static int s_nextCleanUpSceneId = 1;
static utinni::Repository repository;

// Goal B Wave-2 smoke fix: on the advertised client setupScene is un-detourable (tiny-thunk
// trampoline corruption), so a NORMAL (non-editor) login never fired setSceneCallbacks — every
// scene-gated panel stayed dead unless the scene arrived through the editor loadScene path
// (which dispatches manually, WS-1). Bit twice on the 07-18 smokes (placements arming, then the
// whole Snapshot panel on a server login). The per-frame GroundScene latch (ground_scene.cpp
// hkUpdateLoop) is the universal scene-alive signal there: its first tick after boot/cleanup
// dispatches the callbacks exactly once via notifyAdvertisedSceneTick() below; hkCleanupScene
// re-arms. The WS-1 editor path routes through the same once-latch, so whichever fires first
// wins and there is never a double dispatch. Inert on SWGEmu (hkSetScene keeps dispatching
// natively; the tick entry point is advertised-only at its call site).
static std::atomic<bool> s_advertisedSceneCallbacksFired{false};

namespace swg::game
{
void notifyAdvertisedSceneTick()
{
    if (s_advertisedSceneCallbacksFired.exchange(true, std::memory_order_relaxed))
    {
        return;
    }

    dispatchSnapshot(setSceneCallbacks, setSceneCallbacksMutex,
                     [](void (*func)())
                     { func(); });
    utinni::log::info("game: setSceneCallbacks fired (advertised scene-arm latch)");
}
} // namespace swg::game

namespace utinni
{

// Phase 3 R-A primary API: Subscribe returns opaque handle; pair with Unsubscribe.
// D-10: legacy add* retained as wrappers (return value discarded) — existing
// UtinniPlugins (TJT, Sytner) keep working without recompile.

int Game::subscribeInstallCallback(void (*func)())
{
    std::lock_guard<std::mutex> guard(installCallbacksMutex);
    int id = s_nextInstallId++;
    // WR-04 (03-REVIEW): after 2^31 subscribes the post-increment wraps
    // negative and eventually hits 0 -- which is D-09's reserved invalid
    // sentinel. Skip 0 so handles emitted to callers are always non-zero.
    if (id == 0)
    {
        id = s_nextInstallId++;
    }
    installCallbacks.push_back({id, func});
    return id;
}

bool Game::unsubscribeInstallCallback(int handle)
{
    if (handle == 0)
    {
        return false; // D-09: handle 0 reserved as invalid sentinel.
    }
    std::lock_guard<std::mutex> guard(installCallbacksMutex);
    for (auto it = installCallbacks.begin(); it != installCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            installCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

int Game::subscribePreMainLoopCallback(void (*func)())
{
    std::lock_guard<std::mutex> guard(preMainLoopCallbacksMutex);
    int id = s_nextPreMainLoopId++;
    if (id == 0)
    {
        id = s_nextPreMainLoopId++;
    } // WR-04 skip-zero
    preMainLoopCallbacks.push_back({id, func});
    return id;
}

bool Game::unsubscribePreMainLoopCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(preMainLoopCallbacksMutex);
    for (auto it = preMainLoopCallbacks.begin(); it != preMainLoopCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            preMainLoopCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

int Game::subscribeMainLoopCallback(void (*func)())
{
    std::lock_guard<std::mutex> guard(mainLoopCallbacksMutex);
    int id = s_nextMainLoopId++;
    if (id == 0)
    {
        id = s_nextMainLoopId++;
    } // WR-04 skip-zero
    mainLoopCallbacks.push_back({id, func});
    return id;
}

bool Game::unsubscribeMainLoopCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(mainLoopCallbacksMutex);
    for (auto it = mainLoopCallbacks.begin(); it != mainLoopCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            mainLoopCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

int Game::subscribeSetSceneCallback(void (*func)())
{
    std::lock_guard<std::mutex> guard(setSceneCallbacksMutex);
    int id = s_nextSetSceneId++;
    if (id == 0)
    {
        id = s_nextSetSceneId++;
    } // WR-04 skip-zero
    setSceneCallbacks.push_back({id, func});
    return id;
}

bool Game::unsubscribeSetSceneCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(setSceneCallbacksMutex);
    for (auto it = setSceneCallbacks.begin(); it != setSceneCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            setSceneCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

int Game::subscribeCleanupSceneCallback(void (*func)())
{
    std::lock_guard<std::mutex> guard(cleanUpSceneCallbacksMutex);
    int id = s_nextCleanUpSceneId++;
    if (id == 0)
    {
        id = s_nextCleanUpSceneId++;
    } // WR-04 skip-zero
    cleanUpSceneCallbacks.push_back({id, func});
    return id;
}

bool Game::unsubscribeCleanupSceneCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(cleanUpSceneCallbacksMutex);
    for (auto it = cleanUpSceneCallbacks.begin(); it != cleanUpSceneCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            cleanUpSceneCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

void Game::addInstallCallback(void (*func)())
{
    subscribeInstallCallback(func);
}

void Game::addPreMainLoopCallback(void (*func)())
{
    subscribePreMainLoopCallback(func);
}

void Game::addMainLoopCallback(void (*func)())
{
    subscribeMainLoopCallback(func);
}

void Game::addSetSceneCallback(void (*func)())
{
    subscribeSetSceneCallback(func);
}

void Game::addCleanupSceneCallback(void (*func)())
{
    subscribeCleanupSceneCallback(func);
}

int getMainLoopCount()
{
    // Phase 24 v4 (Game unlock): prefer the advertised accessor -- the provider advertises
    // game::g_mainLoopCounter -> &Game::getMainLoopCount (call-not-read), which resolves on the
    // advertised client where the hardcoded 0x1908830 is unmapped. Fall back to the SWGEmu global
    // when the slot is null (export-absent / Pre-CU path byte-for-byte unchanged, D-00).
    if (swg::game::g_mainLoopCounter != nullptr)
    {
        return swg::game::g_mainLoopCounter();
    }
    return memory::read<int>(0x1908830); // Ptr to the main loop count
}

bool loadNewScene = false;
bool sceneCleaned = false;
std::string sceneToLoadTerrainFilename;
std::string sceneToLoadAvatarObjectFilename = "object/creature/player/shared_human_male.iff";
void __cdecl hkMainLoop(bool presentToWindow, HWND hwnd, int width, int height)
{
    // Phase 3 R-H snapshot dispatch per D-12. CR-01 (03-REVIEW): lock-around-
    // snapshot. Stack-snapshot via dispatchSnapshot keeps the per-frame path
    // heap-free. Subscribers added mid-iteration land in the registry but fire
    // on the NEXT dispatch.
    dispatchSnapshot(preMainLoopCallbacks, preMainLoopCallbacksMutex,
                     [](void (*func)())
                     { func(); });

    // Phase 24 v4: the editor-mode resize override (force presentToWindow=false + the window-rect
    // dimensions) is a D3D9/SWGEmu embed mechanism. On the advertised DX11 client the embed sizing
    // is handled NATIVELY (provider CuiManager reflow + Graphics::beginScene poll-fallback + scene
    // RT/proj resize, plus the consumer deferred-reparent -- RNDR-04). Before the GAME unlock
    // hkMainLoop didn't run there and the DX11 resize was correct; once it runs, the override
    // fights the native path (world clipped on the right/bottom). So on the advertised client pass
    // the engine's own per-frame args straight through -- hkMainLoop stays transparent to render.
    RECT rect;
    if (Client::getEditorMode() && !swg::endpoints::isAdvertisedClient() &&
        GetWindowRect(Client::getHwnd(), &rect))
    {
        int newWidth = rect.right - rect.left;
        int newHeight = rect.bottom - rect.top;

        swg::game::mainLoop(false, Client::getHwnd(), newWidth, newHeight); // ToDo fix random crash with getHwnd on load Scene?
    }
    else
    {
        swg::game::mainLoop(presentToWindow, hwnd, width, height);
    }

    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot. Stack-snapshot
    // via dispatchSnapshot keeps the per-frame path heap-free.
    dispatchSnapshot(mainLoopCallbacks, mainLoopCallbacksMutex,
                     [](void (*func)())
                     { func(); });

    // DIAG 2026-05-19: log the scene-load state machine transitions. Conditional
    // so we don't spam the log every frame; only fires when loadNewScene flag is set.
    if (loadNewScene && sceneCleaned)
    {
        loadNewScene = false;
        sceneCleaned = false;
        // v6 (24): the advertised client must drive the FULL SceneCreator string-based load via
        // game::loadScene -- handing the engine a pre-built GroundScene through setupScene
        // (_setScene(Scene*)) only sets ms_scene and skips the _startScene lifecycle, leaving the
        // scene half-integrated -> engine throws ~1s later. SWGEmu keeps the GroundScene+setupScene
        // path (that's where its scene integration lives).
        if (swg::endpoints::isAdvertisedClient())
        {
            // FAIL CLOSED on the advertised client: NEVER fall back to the SWGEmu setupScene(GroundScene::ctor)
            // path -- BOTH setupScene's thunk and GroundScene::ctor (0x00519830) are unbound SWGEmu RVAs there,
            // so a fallback would be a guaranteed crash (crew impl review). If game::loadScene didn't resolve,
            // skip the load and log; do NOT build a GroundScene.
            if (swg::game::loadScene != nullptr)
            {
                utinni::log::info("hkMainLoop: loadNewScene+sceneCleaned -> calling swg::game::loadScene (advertised string-load)");
                swg::game::loadScene(sceneToLoadTerrainFilename.c_str(), sceneToLoadAvatarObjectFilename.c_str());
                utinni::log::info("hkMainLoop: swg::game::loadScene returned");

                // WS-0/WS-1: the advertised editor scene is now live. (1) latch the scene-loaded flag (the DI
                // Enter-mask reads it); (2) drive the consumer-side scene-change notification. On the advertised
                // client the setupScene/hkSetScene detour is deliberately SKIPPED (tiny-thunk trampoline
                // corruption), so setSceneCallbacks never fires natively -- replicate hkSetScene's dispatch here
                // so editor scene-setup subscribers (scene-panel refresh etc.) run. The scene is fully integrated
                // (loadScene == Game::setScene(true,...), the synchronous SceneCreator lifecycle); loadNewScene/
                // sceneCleaned were already reset above, so a subscriber that re-requests a load is handled NEXT
                // frame, not re-entrantly. Subscribers' player/scene reads are advertised-guarded (WS-1,
                // player_object.cpp + GroundScene::get), so a camera-only load with no player degrades, not crashes.
                swg::game::g_editorSceneLoaded.store(true, std::memory_order_relaxed);
                // Wave-2 scene-arm: route through the once-latch so the update-loop tick (the
                // normal-login arm) and this editor-load arm can never double-dispatch.
                swg::game::notifyAdvertisedSceneTick();
                utinni::log::info("hkMainLoop: WS-1 setSceneCallbacks fired (advertised scene-change notify)");
            }
            else
            {
                utinni::log::warning("hkMainLoop: advertised client but game::loadScene unresolved -- skipping load (fail closed, no GroundScene ctor)");
            }
        }
        else
        {
            utinni::log::info("hkMainLoop: loadNewScene+sceneCleaned -> calling swg::game::setupScene via trampoline");
            swg::game::setupScene(GroundScene::ctor(sceneToLoadTerrainFilename.c_str(), sceneToLoadAvatarObjectFilename.c_str()));
            utinni::log::info("hkMainLoop: swg::game::setupScene returned");
        }
    }

    if (loadNewScene)
    {
        utinni::log::info("hkMainLoop: loadNewScene set, sceneCleaned=false -> calling Game::cleanupScene");
        Game::cleanupScene();
        sceneCleaned = true;
        utinni::log::info("hkMainLoop: Game::cleanupScene returned, sceneCleaned=true");
    }
}

void __cdecl hkInstall(int application)
{
    // DIAG 2026-05-19: pinpoint where post-preload init hangs when scene-load fails.
    utinni::log::info("hkInstall: ENTRY (Game::install) -> calling swg::game::install trampoline");
    swg::game::install(application);
    utinni::log::info("hkInstall: swg::game::install returned; constructing Repository");

    // DIAGNOSTIC (2026-06-24): one-shot re-read of the advertised table HERE (post exe static-init --
    // the engine is fully up by Game::install). Pure read, writes no slot. If this reports ~96 vs the
    // 40 resolveFromExe() got at utinni_init, the static-init race theory is confirmed.
    {
        static bool s_diagDone = false;
        if (!s_diagDone)
        {
            swg::endpoints::countResolvableNow();
            s_diagDone = true;
        }
    }

    repository = Repository();
    utinni::log::info("hkInstall: Repository constructed; WorldSnapshot::generateHighestId()");

    WorldSnapshot::generateHighestId();

    {
        std::lock_guard<std::mutex> guard(installCallbacksMutex);
        char msg[80];
        snprintf(msg, sizeof(msg), "hkInstall: firing %zu installCallbacks",
                 installCallbacks.size());
        utinni::log::info(msg);
    }
    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot.
    dispatchSnapshot(installCallbacks, installCallbacksMutex,
                     [](void (*func)())
                     { func(); });
    utinni::log::info("hkInstall: installCallbacks complete");

    if (getConfig().getBool("UtinniCore", "autoLoadScene"))
    {
        utinni::log::info("hkInstall: autoLoadScene=true -> calling Game::loadScene");
        Game::loadScene();
        utinni::log::info("hkInstall: Game::loadScene returned");
    }
    else
    {
        utinni::log::info("hkInstall: autoLoadScene=false; EXIT");
    }
}

void __cdecl hkSetScene(GroundScene* scene)
{
    {
        char msg[80];
        snprintf(msg, sizeof(msg), "hkSetScene: ENTRY (scene=%p)", (void*)scene);
        utinni::log::info(msg);
    }

    swg::game::setupScene(scene);
    utinni::log::info("hkSetScene: swg::game::setupScene returned");

    if (scene != nullptr)
    {
        {
            std::lock_guard<std::mutex> guard(setSceneCallbacksMutex);
            char msg[80];
            snprintf(msg, sizeof(msg), "hkSetScene: scene!=null, firing %zu setSceneCallbacks",
                     setSceneCallbacks.size());
            utinni::log::info(msg);
        }
        // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot.
        dispatchSnapshot(setSceneCallbacks, setSceneCallbacksMutex,
                         [](void (*func)())
                         { func(); });
        utinni::log::info("hkSetScene: setSceneCallbacks complete; EXIT");
    }
    else
    {
        utinni::log::info("hkSetScene: scene==null; EXIT (no callbacks fired)");
    }
}

void __cdecl hkCleanupScene()
{
    // WS-0/WS-1: drop the advertised editor-scene-loaded flag BEFORE the teardown trampoline so the DI
    // Enter-mask + scene subscribers stop treating the world as active during cleanup frames. Inert on
    // SWGEmu (never set true there).
    swg::game::g_editorSceneLoaded.store(false, std::memory_order_relaxed);

    // Wave-2 scene-arm: re-arm the once-latch so the NEXT scene's first update tick (or editor
    // load) re-dispatches setSceneCallbacks. Inert on SWGEmu (the latch never fires there).
    s_advertisedSceneCallbacksFired.store(false, std::memory_order_relaxed);

    // WS-4: drop the GroundScene reload latch before teardown so a reload queued mid-cleanup no-ops rather
    // than calling reloadTerrain on a freed instance. Inert on SWGEmu (latch never set there).
    swg::groundScene::clearAdvertisedInstance();

    utinni::log::info("hkCleanupScene: ENTRY -> calling swg::game::cleanupScene trampoline");
    swg::game::cleanupScene();
    utinni::log::info("hkCleanupScene: swg::game::cleanupScene returned; disabling imgui_gizmo");

    imgui_gizmo::disable();

    {
        std::lock_guard<std::mutex> guard(cleanUpSceneCallbacksMutex);
        char msg[80];
        snprintf(msg, sizeof(msg), "hkCleanupScene: firing %zu cleanUpSceneCallbacks",
                 cleanUpSceneCallbacks.size());
        utinni::log::info(msg);
    }
    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot.
    dispatchSnapshot(cleanUpSceneCallbacks, cleanUpSceneCallbacksMutex,
                     [](void (*func)())
                     { func(); });
    utinni::log::info("hkCleanupScene: cleanUpSceneCallbacks complete; EXIT");
}

void Game::detour()
{
    // Phase 24 v4 (MISC/INPUT unlock -- GAME subsystem, live-smoke-gated): the wholesale
    // advertised-client skip is DROPPED. The v3-era blockers are resolved by the provider's v4
    // handback: (4a) game::mainLoop now resolves to the REAL per-frame tick Game::runGameLoopOnce
    // -- an EXACT __cdecl(bool,HWND,int,int) match to hkMainLoop, so no stack corruption; (4b)
    // getMainLoopCount() reads the relocated counter via the advertised g_mainLoopCounter accessor
    // instead of the unmapped 0x1908830. install/setupScene/cleanupScene are advertised statics.
    // Each Detour::Create is now PER-TARGET installable()-gated: on SWGEmu every literal is mapped
    // -> the full set installs byte-for-byte unchanged (D-00); on the advertised client each
    // installs only if its target resolved from the catalog.
    //
    // INTERDEPENDENCY (acceptable for this increment): hkInstall builds a Repository from
    // treefile::getAllFilenames(), which is EMPTY until the treefile subsystem is also unlocked
    // (its hook never ran to populate the set). So the file-backed editor data is empty on the
    // advertised client until then -- it DEGRADES (empty repository -> generateHighestId()==0),
    // it does NOT crash; the scene-lifecycle callbacks this subsystem exists for still fire.
    if (getMainLoopCount() == 0) // suspended startup entry-point loop
    {
        if (swg::endpoints::installable((const void*)swg::game::mainLoop))
            swg::game::mainLoop = (swg::game::pMainLoop)Detour::Create(swg::game::mainLoop, hkMainLoop, DETOUR_TYPE_PUSH_RET);
        if (swg::endpoints::installable((const void*)swg::game::install))
            swg::game::install = (swg::game::pInstall)Detour::Create(swg::game::install, hkInstall, DETOUR_TYPE_PUSH_RET);
        // Phase 24: on the advertised client game::setupScene resolves to a SMALL provider thunk
        // (utinni_gameSetupScene over Game::_setScene). Detouring a tiny thunk whose prologue holds a
        // relative call corrupts the DetourXS trampoline -> hkMainLoop's trampoline call jumps to null
        // (0xC0000005 EXEC target=0). It is also POINTLESS there: the engine's own scene-set goes
        // through Game::_setScene directly, NOT this thunk, so the detour could only ever intercept
        // Utinni's own calls. Leave setupScene UNDETOURED on the advertised client -- hkMainLoop calls
        // the thunk directly (it sets the pre-built GroundScene). On SWGEmu the target is the real
        // setupScene function and the detour installs unchanged (D-00). (setSceneCallbacks for editor
        // scene-change notification on the advertised client are a follow-up -- they need the engine's
        // real scene-set path, not the thunk.)
        if (swg::endpoints::installable((const void*)swg::game::setupScene) && !swg::endpoints::isAdvertisedClient())
            swg::game::setupScene = (swg::game::pSetupScene)Detour::Create(swg::game::setupScene, hkSetScene, DETOUR_TYPE_PUSH_RET);
        if (swg::endpoints::installable((const void*)swg::game::cleanupScene))
            swg::game::cleanupScene = (swg::game::pCleanupScene)Detour::Create(swg::game::cleanupScene, hkCleanupScene, DETOUR_TYPE_PUSH_RET);
    }
}

void Game::quit()
{
    swg::game::quit();
}

bool Game::isRunning()
{
    return getMainLoopCount();
}

void Game::loadScene()
{
    const char* terrainFilename = swg::config::clientGame::getSceneTerrainFilename();
    const char* avatarFilename = swg::config::clientGame::getSceneAvatarFilename();

    if (terrainFilename != nullptr)
    {
        sceneToLoadTerrainFilename = terrainFilename;
    }

    if (avatarFilename != nullptr)
    {
        sceneToLoadAvatarObjectFilename = avatarFilename;
    }

    if (sceneToLoadTerrainFilename.empty())
    {
        log::error("Failed to load scene due to there being no set terrain filename.");
        return;
    }

    loadNewScene = true;
}

void Game::loadScene(const char* terrainFilename, const char* avatarObjectFilename)
{
    sceneToLoadTerrainFilename = terrainFilename;
    sceneToLoadAvatarObjectFilename = avatarObjectFilename;
    loadNewScene = true;
}

void Game::cleanupScene()
{
    swg::game::cleanupScene();
}

Repository* Game::getRepository()
{
    return &repository;
}

Object* Game::getPlayer()
{
    return swg::game::getPlayer();
}

Object* Game::getPlayerCreatureObject() // ToDo return CreatureObject*
{
    return swg::game::getPlayerCreatureObject();
}

swgptr Game::getPlayerLookAtTargetObjectNetworkId()
{
    // WorldSnapshot chain / target-change un-gate (2026-07-03): the +1432 lookAt-target slot is a
    // RAW CreatureObject byte-offset from the 2002 SWGEmu layout -- SS5-fragile on the advertised NGE
    // client (wrong field there). Degrade to 0 -- this SWGEmu-shaped return is a *pointer into the
    // creature* (fed to Object::getObjectById), meaningless on advertised, so the v16 accessor does
    // NOT light this function up: the advertised read lives in getPlayerLookAtTargetObject()'s own
    // branch (int64 id -> Network::getObjectById), never round-tripped through this swgptr.
    if (swg::endpoints::isAdvertisedClient())
    {
        return 0;
    }

    const Object* playerObj = getPlayerCreatureObject();

    if (!playerObj)
    {
        return 0;
    }

    return (swgptr)playerObj + 1432;
}

Object* Game::getPlayerLookAtTargetObject()
{
    // v16 (24 / Goal A+) advertised branch: read the id via the advertised shim and resolve it
    // through the v12 network::getObjectById row -- NOT Object::getObjectById, whose
    // Network::getCachedObjectById path is nulled on advertised (wave 1), so routing the id
    // through it would return null forever. The id stays in an int64_t local end-to-end
    // (NGE NetworkIds carry cluster-id high bits; swgptr is 32-bit and would truncate).
    // A null resolve with a non-zero id is a normal staleness outcome (target unloaded /
    // out of range), not an error.
    if (swg::endpoints::isAdvertisedClient())
    {
        if (swg::game::getPlayerLookAtTargetId == nullptr)
        {
            // Distinct from the no-target case: the row itself is missing (provider < v16).
            static std::atomic<bool> s_loggedUnadvertised{false};
            if (!s_loggedUnadvertised.exchange(true, std::memory_order_relaxed))
            {
                utinni::log::warning("getPlayerLookAtTargetObject: game::getPlayerLookAtTargetId "
                                     "not advertised (provider < v16) -- returning null");
            }
            return nullptr;
        }

        const int64_t lookAtId = swg::game::getPlayerLookAtTargetId();

        if (lookAtId == 0)
        {
            return nullptr;
        }

        return Network::getObjectById(lookAtId);
    }

    const swgptr lookAtId = getPlayerLookAtTargetObjectNetworkId();

    if (lookAtId == 0)
    {
        return nullptr;
    }

    return Object::getObjectById(lookAtId);
}

Camera* Game::getCamera()
{
    return swg::game::getCamera();
}

const Camera* Game::getConstCamera()
{
    return swg::game::getConstCamera();
}

bool Game::isSafeToUse()
{
    // Phase 24 / D-04 read->call dual-path. The advertised client exposes the run-state
    // through the static accessor game::g_runningFlags -> &Game::isOver() (call-not-read;
    // the two raw safety-flag globals are private). isOver() is true when the game is
    // shutting down / has no window; "safe to use" is its inverse, so on the advertised
    // path we CALL the resolved accessor and negate it. A function pointer cannot be
    // memory::read'd (Pitfall 4). On SWGEmu the slot is null -> keep the existing two-bool
    // memory::read expression byte-for-byte (D-00, no regression).
    if (swg::game::g_runningFlags != nullptr)
    {
        return !swg::game::g_runningFlags();
    }
    // SWGEmu RVA path: true when EITHER SWG-internal safety flag is set. 2026-07-19 LIVE
    // COUNTER-EVIDENCE reverted the && this briefly was: in a fully-loaded in-world Core3
    // session (player targeting/resolving objects, scene stable), one of the two flags is
    // NOT set, so the && made isSafeToUse() false and silently no-op'd every world-snapshot
    // mutation (remove regression, caught by the Node::removeNode diag). The || is the
    // field-proven semantics (years of SWGEmu editing); internals.md's "both must be true"
    // model does not hold live and has been corrected. See assessment.md Open Questions §1.
    return memory::read<bool>(0x01908858) || memory::read<bool>(0x01919410);
}

// WR-03 (03-REVIEW): test-only accessors relocated to the test_internal
// namespace (declared in game_test_internal.h). They are NOT members of
// the public Game class -- mirrors the R-B test_internal::TestImpl pattern
// in plugin_manager.cpp.
namespace test_internal
{
void triggerInstallCallbacks()
{
    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot.
    dispatchSnapshot(installCallbacks, installCallbacksMutex,
                     [](void (*func)())
                     { func(); });
}

int getInstallSubscriberCount()
{
    std::lock_guard<std::mutex> guard(installCallbacksMutex);
    return static_cast<int>(installCallbacks.size());
}
} // namespace test_internal

} // namespace utinni
