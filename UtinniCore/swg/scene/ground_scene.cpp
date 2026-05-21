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

#include "ground_scene.h"
#include "terrain.h"
#include "world_snapshot.h"
#include "render_world.h"
#include "swg/misc/swg_memory.h"
#include "swg/misc/io_win.h"
#include "swg/game/game.h"
#include "swg/object/client_object.h"
#include "utility/log.h"
#include "utility/string_utility.h"
#include "swg/appearance/portal.h"
#include "swg/camera/debug_camera.h"

#include <atomic>
#include <cstdio>
#include <intrin.h>
#include <mutex>
#include <unordered_map>
#include <vector>


namespace swg::groundScene
{
using pCtor = utinni::GroundScene* (__thiscall*)(void* pThis, const char* terrainFilename, const char* avatarObjectFilename, swgptr customPlayer); // Offline scene ctor
using pReloadTerrain = void(__thiscall*)(utinni::GroundScene* pThis);
using pChangeCamera = int(__thiscall*)(utinni::GroundScene* pThis, utinni::Camera::Modes cameraMode, float);
using pGetCurrentCamera = utinni::Camera* (__thiscall*)(utinni::GroundScene* pThis);

using pDraw = void(__thiscall*)(utinni::GroundScene* pThis);
using pUpdate = void(__thiscall*)(utinni::GroundScene* pThis, float time);
using pHandleInputMapUpdate = void(__thiscall*)(utinni::GroundScene* pThis);
using pHandleInputMapEvent = void(__thiscall*)(utinni::GroundScene* pThis, utinni::IoEvent* ioEvent);

using pInit = void(__thiscall*)(utinni::GroundScene* pThis, const char* terrain, utinni::Object* playerObj, float time);

pCtor ctor = (pCtor)0x00519830; // Offline scene ctor
pReloadTerrain reloadTerrain = (pReloadTerrain)0x0051A4F0;
pChangeCamera changeCamera = (pChangeCamera)0x0051A350;
pGetCurrentCamera getCurrentCamera = (pGetCurrentCamera)0x0051A4D0;

pDraw draw = (pDraw)0x0051B770;
pUpdate update = (pUpdate)0x0051AF10;
pHandleInputMapUpdate handleInputMapUpdate = (pHandleInputMapUpdate)0x0051AB20;
pHandleInputMapEvent handleInputMapEvent = (pHandleInputMapEvent)0x0051AA40;

pInit init = (pInit)0x00518EB0;
}

// Phase 3 R-A native-side (per 03-CONTEXT D-08/D-09): handle-based registries
// backed by std::unordered_map<int, fn_ptr>. Handle 0 reserved as invalid
// sentinel.
//
// CR-01 (03-REVIEW): per-registry std::mutex serializes Subscribe / Unsubscribe
// writes against the snapshot-build read in the dispatch sites.
static std::unordered_map<int, void(*)(utinni::GroundScene* pThis)> preDrawLoopCallbacks;
static std::unordered_map<int, void(*)(utinni::GroundScene* pThis)> postDrawLoopCallbacks;
static std::unordered_map<int, void(*)(utinni::GroundScene* pThis, float time)> updateLoopCallbacks;
static std::unordered_map<int, void(*)()> cameraChangeCallbacks;
static std::mutex preDrawLoopCallbacksMutex;
static std::mutex postDrawLoopCallbacksMutex;
static std::mutex updateLoopCallbacksMutex;
static std::mutex cameraChangeCallbacksMutex;
static int s_nextPreDrawId = 1;
static int s_nextPostDrawId = 1;
static int s_nextUpdateId = 1;
static int s_nextCameraChangeId = 1;

namespace utinni
{
GroundScene* GroundScene::get() // Static GroundScene Pointer
{
    return memory::read<GroundScene*>(0x190885C);
}

GroundScene* GroundScene::ctor(const char* terrainFilename, const char* avatarObjectFilename)
{
    return swg::groundScene::ctor(utinni::allocate(0xF4), terrainFilename, avatarObjectFilename, 0);
}

std::string GroundScene::getName()
{
    const std::string terrainPath = Terrain::get()->getFilename();

    if (terrainPath.empty())
    {
        return "";
    }

    int i = terrainPath.find_first_of('/') + 1;
    const int length = terrainPath.size() - i - 5;

    if (length < 0)
    {
        return "";
    }

    return terrainPath.substr(i, length);
}

// Phase 3 R-A: handle-based Subscribe/Unsubscribe per D-08/D-09. Add* retained
// per D-10 as wrappers (return value discarded).
int GroundScene::subscribePreDrawLoopCallback(void(*func)(GroundScene* pThis))
{
    std::lock_guard<std::mutex> guard(preDrawLoopCallbacksMutex);
    int id = s_nextPreDrawId++;
    preDrawLoopCallbacks[id] = func;
    return id;
}

bool GroundScene::unsubscribePreDrawLoopCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(preDrawLoopCallbacksMutex);
    return preDrawLoopCallbacks.erase(handle) > 0;
}

int GroundScene::subscribePostDrawLoopCallback(void(*func)(GroundScene* pThis))
{
    std::lock_guard<std::mutex> guard(postDrawLoopCallbacksMutex);
    int id = s_nextPostDrawId++;
    postDrawLoopCallbacks[id] = func;
    return id;
}

bool GroundScene::unsubscribePostDrawLoopCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(postDrawLoopCallbacksMutex);
    return postDrawLoopCallbacks.erase(handle) > 0;
}

int GroundScene::subscribeUpdateLoopCallback(void(*func)(GroundScene* pThis, float elapsedTime))
{
    std::lock_guard<std::mutex> guard(updateLoopCallbacksMutex);
    int id = s_nextUpdateId++;
    updateLoopCallbacks[id] = func;
    return id;
}

bool GroundScene::unsubscribeUpdateLoopCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(updateLoopCallbacksMutex);
    return updateLoopCallbacks.erase(handle) > 0;
}

int GroundScene::subscribeCameraChangeCallback(void(*func)())
{
    std::lock_guard<std::mutex> guard(cameraChangeCallbacksMutex);
    int id = s_nextCameraChangeId++;
    cameraChangeCallbacks[id] = func;
    return id;
}

bool GroundScene::unsubscribeCameraChangeCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(cameraChangeCallbacksMutex);
    return cameraChangeCallbacks.erase(handle) > 0;
}

void GroundScene::addPreDrawLoopCallback(void(*func)(GroundScene* pThis))
{
    subscribePreDrawLoopCallback(func);
}

void GroundScene::addPostDrawLoopCallback(void(*func)(GroundScene* pThis))
{
    subscribePostDrawLoopCallback(func);
}

void __fastcall hkDrawLoop(GroundScene* pThis, DWORD EDX)
{
    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot.
    {
        std::vector<void(*)(GroundScene*)> snapshot;
        {
            std::lock_guard<std::mutex> guard(preDrawLoopCallbacksMutex);
            snapshot.reserve(preDrawLoopCallbacks.size());
            for (const auto& kv : preDrawLoopCallbacks)
            {
                snapshot.push_back(kv.second);
            }
        }
        for (const auto& func : snapshot)
        {
            func(pThis);
        }
    }

    swg::groundScene::draw(pThis);

    {
        std::vector<void(*)(GroundScene*)> snapshot;
        {
            std::lock_guard<std::mutex> guard(postDrawLoopCallbacksMutex);
            snapshot.reserve(postDrawLoopCallbacks.size());
            for (const auto& kv : postDrawLoopCallbacks)
            {
                snapshot.push_back(kv.second);
            }
        }
        for (const auto& func : snapshot)
        {
            func(pThis);
        }
    }
}

void GroundScene::addUpdateLoopCallback(void(*func)(GroundScene* pThis, float elapsedTime))
{
    subscribeUpdateLoopCallback(func);
}

void GroundScene::addCameraChangeCallback(void(*func)())
{
    subscribeCameraChangeCallback(func);
}

void __fastcall hkUpdateLoop(GroundScene* pThis, DWORD EDX, float time)
{
    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot.
    std::vector<void(*)(GroundScene*, float)> snapshot;
    {
        std::lock_guard<std::mutex> guard(updateLoopCallbacksMutex);
        snapshot.reserve(updateLoopCallbacks.size());
        for (const auto& kv : updateLoopCallbacks)
        {
            snapshot.push_back(kv.second);
        }
    }
    for (const auto& func : snapshot)
    {
        func(pThis, time);
    }
    swg::groundScene::update(pThis, time);
}

void __fastcall hkHandleInputEvent(GroundScene* pThis, DWORD EDX, IoEvent* ioEvent)
{
    // DIAG 2026-05-20 Issue #11 Phase G (per CODEX consult): log every
    // IoEvent that reaches GroundScene::handleInputMapEvent. This is the
    // earliest local hook point after CuiIo::processEvent where the
    // game-mode input map should translate kc_Enter (28 / 0x1C) into a
    // command/action. If Enter shows up here, GroundScene's input map is
    // getting the event but routing it to chatWindow instead of to
    // game-mode 'startChat'. If Enter doesn't show up, it's already been
    // siphoned off before this point. Filter out t_Update (4) and other
    // noisy continuous events; log everything else, capped at 40 entries.
    if (ioEvent != nullptr && ioEvent->type != IoEvent::t_Update)
    {
        // WR-01 (03-REVIEW): the diag counter is read+modified from any
        // thread that reaches GroundScene::handleInputMapEvent (typically
        // SWG's main thread, but defensive against the event-handling
        // chain crossing threads). std::atomic<int> with fetch_add gives
        // us a race-free counter -- previously the non-atomic ++ could
        // double-allocate a slot under interleaved RMW.
        //
        // _ReturnAddress() limitation (documented, not fixable without
        // de-optimizing the whole function): with MSVC /O2 the captured
        // PC may point inside a trampoline rather than the original
        // caller. The diagnostic value here is "in what part of the
        // input pipeline did this event arrive" -- the exact byte offset
        // is best-effort and acknowledged.
        static std::atomic<int> s_ioEventLogCount{0};
        if (s_ioEventLogCount.load(std::memory_order_relaxed) < 40)
        {
            int slot = s_ioEventLogCount.fetch_add(1, std::memory_order_relaxed);
            if (slot < 40)
            {
                const void* callerPC = _ReturnAddress();
                char m[200];
                snprintf(m, sizeof(m),
                    "hkHandleInputEvent[%d]: type=%d arg1=%d arg2=%d arg3=%.3f pThis=0x%p freeCam=%d caller=0x%p",
                    slot + 1, ioEvent->type, ioEvent->arg1, ioEvent->arg2,
                    ioEvent->arg3, (void*)pThis, pThis->isFreeCameraActive() ? 1 : 0,
                    callerPC);
                utinni::log::info(m);
            }
        }
    }

    if (pThis->isFreeCameraActive())
    {
        debugCamera::processIoEvent(ioEvent);
    }

    swg::groundScene::handleInputMapEvent(pThis, ioEvent);
}

void GroundScene::detour()
{
    swg::groundScene::draw = (swg::groundScene::pDraw)Detour::Create(swg::groundScene::draw, hkDrawLoop, DETOUR_TYPE_PUSH_RET);
    swg::groundScene::update = (swg::groundScene::pUpdate)Detour::Create(swg::groundScene::update, hkUpdateLoop, DETOUR_TYPE_PUSH_RET);
    swg::groundScene::handleInputMapEvent = (swg::groundScene::pHandleInputMapEvent )Detour::Create(swg::groundScene::handleInputMapEvent, hkHandleInputEvent, DETOUR_TYPE_PUSH_RET);

    WorldSnapshot::setPreloadSnapshot(false);
}

void GroundScene::removeDetour()
{
    //Detour::Remove((LPVOID)swg::groundScene::handleInputMapUpdate);
}

Camera* GroundScene::getCurrentCamera()
{
    return swg::groundScene::getCurrentCamera(this);
}

void GroundScene::toggleFreeCamera()
{
    if (isFreeCameraActive())
    {
        swg::groundScene::changeCamera(this, Camera::Modes::cm_FreeChase, 0);
    }
    else
    {
        swg::groundScene::changeCamera(this, Camera::Modes::cm_Free, 0);
    }

    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot.
    {
        std::vector<void(*)()> snapshot;
        {
            std::lock_guard<std::mutex> guard(cameraChangeCallbacksMutex);
            snapshot.reserve(cameraChangeCallbacks.size());
            for (const auto& kv : cameraChangeCallbacks)
            {
                snapshot.push_back(kv.second);
            }
        }
        for (const auto& func : snapshot)
        {
            func();
        }
    }
}

void GroundScene::changeCameraMode(int cameraMode)
{
    swg::groundScene::changeCamera(this, (Camera::Modes) cameraMode, 0);
}

bool GroundScene::isFreeCameraActive() const
{
    return currentView == Camera::Modes::cm_Free;
}

void GroundScene::reloadTerrain()
{
    swg::groundScene::reloadTerrain(this);
}

void GroundScene::createObjectAtPlayer(const char* filename)
{
    Object* player = Game::getPlayer();
    if (!player)
    {
        return;
    }

    SharedObjectTemplate* objTemplate = ObjectTemplateList::getObjectTemplateByFilename(filename);
    if (objTemplate == nullptr)
    {
        return;
    }

    Object* obj = nullptr;
    const char* pobFilename = objTemplate->getPortalLayoutFilename();
    if (constCharUtility::isEmpty(pobFilename))
    {
        obj = ObjectTemplate::createObject(filename);
    }
    else
    {
        PortalPropertyTemplate* pob = PortalPropertyTemplateList::getPobByCrcString(PersistentCrcString::ctor(pobFilename));
        obj = Object::ctor();

        obj->addNotification(0x019136E4, false);
        obj->setAppearance(Appearance::create(pob->getExteriorAppearanceName()));
        renderWorld::addObjectNotifications(obj);
    }

    ClientObject* clientObj = (ClientObject*)obj;
    clientObj->setParentCell(player->getParentCell());

    CellProperty::setPortalTransitions(false);
    { // ToDO see if this can be removed
        memcpy((void*)(((int)obj) + 0x50), (void*)(((int)player) + 0x50), 0x30u); // Todo see if it can be replaced
    }
    CellProperty::setPortalTransitions(true);

    renderWorld::addObjectNotifications(obj);
    clientObj->endBaselines();

    obj->addToWorld();
}

void GroundScene::createAppearanceAtPlayer(const char* filename)
{
    Object* player = Game::getPlayer();
    if (!player)
    {
        return;
    }

    Appearance* appearance;
    std::string str = filename; // ToDo replace with a 'endsWith' utility function for const char*
    if (str.substr(str.length() - 4) == ".pob")
    {
        PortalPropertyTemplate* pob = PortalPropertyTemplateList::getPobByCrcString(PersistentCrcString::ctor(filename));
        appearance = Appearance::create(pob->getExteriorAppearanceName());
    }
    else
    {
        appearance = Appearance::create(filename);
    }

    if (appearance == nullptr)
    {
        return;
    }

    Object* obj = Object::ctor();

    obj->addNotification(0x019136E4, false);
    obj->setAppearance(appearance);

    memcpy((void*)(((int)obj) + 0x50), (void*)(((int)player) + 0x50), 0x30u);  // Todo see if it can be replaced

    renderWorld::addObjectNotifications(obj);

    obj->addToWorld();
}
}


