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
#include "utinni.h";
#include <imgui/imgui_user.h>
#include <mutex>
#include <unordered_map>
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

using pGetPlayer = utinni::Object* (__cdecl*)();
using pGetPlayerCreatureObject = utinni::Object* (__cdecl*)();

using pGetCamera = utinni::Camera* (__cdecl*)();
using pGetConstCamera = const utinni::Camera* (__cdecl*)();

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
}

// Phase 3 R-A native-side (per 03-CONTEXT D-08/D-09): handle-based registries
// backed by std::unordered_map<int, fn_ptr> + monotonic next-id. Handle 0 is
// reserved as the invalid sentinel.
//
// CR-01 (03-REVIEW): per-registry std::mutex serializes Subscribe / Unsubscribe
// writes against the snapshot-build read in the dispatch sites. Mirrors the
// managed-side `lock` discipline (e.g. GameCallbacks.cs xxxLock). Dispatch
// copies under the lock, iterates the snapshot outside the lock so callbacks
// can re-subscribe without deadlock.
static std::unordered_map<int, void(*)()> installCallbacks;
static std::unordered_map<int, void(*)()> preMainLoopCallbacks;
static std::unordered_map<int, void(*)()> mainLoopCallbacks;
static std::unordered_map<int, void(*)()> setSceneCallbacks;
static std::unordered_map<int, void(*)()> cleanUpSceneCallbacks;
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

int Game::subscribeInstallCallback(void(*func)())
{
    std::lock_guard<std::mutex> guard(installCallbacksMutex);
    int id = s_nextInstallId++;
    installCallbacks[id] = func;
    return id;
}

bool Game::unsubscribeInstallCallback(int handle)
{
    if (handle == 0)
    {
        return false; // D-09: handle 0 reserved as invalid sentinel.
    }
    std::lock_guard<std::mutex> guard(installCallbacksMutex);
    return installCallbacks.erase(handle) > 0;
}

int Game::subscribePreMainLoopCallback(void(*func)())
{
    std::lock_guard<std::mutex> guard(preMainLoopCallbacksMutex);
    int id = s_nextPreMainLoopId++;
    preMainLoopCallbacks[id] = func;
    return id;
}

bool Game::unsubscribePreMainLoopCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(preMainLoopCallbacksMutex);
    return preMainLoopCallbacks.erase(handle) > 0;
}

int Game::subscribeMainLoopCallback(void(*func)())
{
    std::lock_guard<std::mutex> guard(mainLoopCallbacksMutex);
    int id = s_nextMainLoopId++;
    mainLoopCallbacks[id] = func;
    return id;
}

bool Game::unsubscribeMainLoopCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(mainLoopCallbacksMutex);
    return mainLoopCallbacks.erase(handle) > 0;
}

int Game::subscribeSetSceneCallback(void(*func)())
{
    std::lock_guard<std::mutex> guard(setSceneCallbacksMutex);
    int id = s_nextSetSceneId++;
    setSceneCallbacks[id] = func;
    return id;
}

bool Game::unsubscribeSetSceneCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(setSceneCallbacksMutex);
    return setSceneCallbacks.erase(handle) > 0;
}

int Game::subscribeCleanupSceneCallback(void(*func)())
{
    std::lock_guard<std::mutex> guard(cleanUpSceneCallbacksMutex);
    int id = s_nextCleanUpSceneId++;
    cleanUpSceneCallbacks[id] = func;
    return id;
}

bool Game::unsubscribeCleanupSceneCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(cleanUpSceneCallbacksMutex);
    return cleanUpSceneCallbacks.erase(handle) > 0;
}

void Game::addInstallCallback(void(*func)())
{
    subscribeInstallCallback(func);
}

void Game::addPreMainLoopCallback(void(*func)())
{
    subscribePreMainLoopCallback(func);
}

void Game::addMainLoopCallback(void(*func)())
{
    subscribeMainLoopCallback(func);
}

void Game::addSetSceneCallback(void(*func)())
{
    subscribeSetSceneCallback(func);
}

void Game::addCleanupSceneCallback(void(*func)())
{
    subscribeCleanupSceneCallback(func);
}

int getMainLoopCount()
{
    return memory::read<int>(0x1908830); // Ptr to the main loop count
}

bool loadNewScene = false;
bool sceneCleaned = false;
std::string sceneToLoadTerrainFilename;
std::string sceneToLoadAvatarObjectFilename = "object/creature/player/shared_human_male.iff";
void __cdecl hkMainLoop(bool presentToWindow, HWND hwnd, int width, int height)
{
    // Phase 3 R-H snapshot dispatch per D-12: copy values into a local vector
    // before iteration so Subscribe-during-dispatch can't invalidate the iterator.
    // Subscribers added mid-iteration land in the registry but fire on the NEXT
    // dispatch. CR-01 (03-REVIEW): snapshot copy taken under per-registry lock
    // so concurrent Subscribe/Unsubscribe from other threads can't race the
    // map's bucket structure. Iteration runs outside the lock so callbacks
    // can safely re-subscribe.
    {
        std::vector<void(*)()> snapshot;
        {
            std::lock_guard<std::mutex> guard(preMainLoopCallbacksMutex);
            snapshot.reserve(preMainLoopCallbacks.size());
            for (const auto& kv : preMainLoopCallbacks)
            {
                snapshot.push_back(kv.second);
            }
        }
        for (const auto& func : snapshot)
        {
            func();
        }
    }

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

    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot.
    {
        std::vector<void(*)()> snapshot;
        {
            std::lock_guard<std::mutex> guard(mainLoopCallbacksMutex);
            snapshot.reserve(mainLoopCallbacks.size());
            for (const auto& kv : mainLoopCallbacks)
            {
                snapshot.push_back(kv.second);
            }
        }
        for (const auto& func : snapshot)
        {
            func();
        }
    }

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
    {
        std::vector<void(*)()> snapshot;
        {
            std::lock_guard<std::mutex> guard(installCallbacksMutex);
            snapshot.reserve(installCallbacks.size());
            for (const auto& kv : installCallbacks)
            {
                snapshot.push_back(kv.second);
            }
        }
        for (const auto& func : snapshot)
        {
            func();
        }
    }
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
        // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot.
        std::vector<void(*)()> snapshot;
        {
            std::lock_guard<std::mutex> guard(setSceneCallbacksMutex);
            char msg[80];
            snprintf(msg, sizeof(msg), "hkSetScene: scene!=null, firing %zu setSceneCallbacks",
                     setSceneCallbacks.size());
            utinni::log::info(msg);
            snapshot.reserve(setSceneCallbacks.size());
            for (const auto& kv : setSceneCallbacks)
            {
                snapshot.push_back(kv.second);
            }
        }
        for (const auto& func : snapshot)
        {
            func();
        }
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

    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot.
    {
        std::vector<void(*)()> snapshot;
        {
            std::lock_guard<std::mutex> guard(cleanUpSceneCallbacksMutex);
            char msg[80];
            snprintf(msg, sizeof(msg), "hkCleanupScene: firing %zu cleanUpSceneCallbacks",
                     cleanUpSceneCallbacks.size());
            utinni::log::info(msg);
            snapshot.reserve(cleanUpSceneCallbacks.size());
            for (const auto& kv : cleanUpSceneCallbacks)
            {
                snapshot.push_back(kv.second);
            }
        }
        for (const auto& func : snapshot)
        {
            func();
        }
    }
    utinni::log::info("hkCleanupScene: cleanUpSceneCallbacks complete; EXIT");
}

void Game::detour()
{
    if (getMainLoopCount() == 0) // Checks the Games main loop count, if 0, we're in the 'suspended' startup entry point loop
    {
        //utility::showMessageBox("");
        swg::game::mainLoop = (swg::game::pMainLoop)Detour::Create(swg::game::mainLoop, hkMainLoop, DETOUR_TYPE_PUSH_RET);
        swg::game::install = (swg::game::pInstall)Detour::Create(swg::game::install, hkInstall, DETOUR_TYPE_PUSH_RET);
        swg::game::setupScene = (swg::game::pSetupScene)Detour::Create(swg::game::setupScene, hkSetScene, DETOUR_TYPE_PUSH_RET);
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
    // Returns true only when both SWG-internal safety flags are set. Per docs/ai/internals.md:218-231,
    // "AND ... Both must be true" — the operator was previously || (logical-OR), which returned true
    // when only one flag was set, allowing world-snapshot mutations during scene transitions that the
    // second flag would have blocked. CON-O-01 disposition: docs/ai/internals.md is the source of truth;
    // the operator is &&. See assessment.md Open Questions §1.
    return memory::read<bool>(0x01908858) && memory::read<bool>(0x01919410);
}

void Game::triggerInstallCallbacks()
{
    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot.
    std::vector<void(*)()> snapshot;
    {
        std::lock_guard<std::mutex> guard(installCallbacksMutex);
        snapshot.reserve(installCallbacks.size());
        for (const auto& kv : installCallbacks)
        {
            snapshot.push_back(kv.second);
        }
    }
    for (const auto& func : snapshot)
    {
        func();
    }
}

// Phase 3 R-A test-bridge support: expose the registry size to native
// test-only exports without leaking the registry symbol. Used by
// utinni_test_installSubscriberCount in test_exports.cpp.
int Game::getInstallSubscriberCount()
{
    std::lock_guard<std::mutex> guard(installCallbacksMutex);
    return static_cast<int>(installCallbacks.size());
}

}
