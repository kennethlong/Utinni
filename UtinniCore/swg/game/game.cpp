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
#include <mutex>
#include <vector>
#include "swg/client/client.h"
#include "swg/misc/config.h"
#include "swg/scene/ground_scene.h"
#include "swg/scene/world_snapshot.h"
#include "swg/object/client_object.h"
#include "swg/object/object.h"
#include "swg/ui/imgui_impl.h"

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

    RECT rect;
    if (Client::getEditorMode() && GetWindowRect(Client::getHwnd(), &rect))
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
        utinni::log::info("hkMainLoop: loadNewScene+sceneCleaned -> calling swg::game::setupScene via trampoline");
        loadNewScene = false;
        sceneCleaned = false;
        swg::game::setupScene(GroundScene::ctor(sceneToLoadTerrainFilename.c_str(), sceneToLoadAvatarObjectFilename.c_str()));
        utinni::log::info("hkMainLoop: swg::game::setupScene returned");
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
        if (swg::endpoints::installable((const void*)swg::game::setupScene))
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
    const Object* playerObj = getPlayerCreatureObject();

    if (!playerObj)
    {
        return 0;
    }

    return (swgptr)playerObj + 1432;
}

Object* Game::getPlayerLookAtTargetObject()
{
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
    // SWGEmu RVA path: returns true only when both SWG-internal safety flags are set.
    // Per docs/ai/internals.md:218-231, "AND ... Both must be true" — the operator was
    // previously || (logical-OR), which returned true when only one flag was set, allowing
    // world-snapshot mutations during scene transitions that the second flag would have
    // blocked. CON-O-01 disposition: docs/ai/internals.md is the source of truth; the
    // operator is &&. See assessment.md Open Questions §1.
    return memory::read<bool>(0x01908858) && memory::read<bool>(0x01919410);
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
