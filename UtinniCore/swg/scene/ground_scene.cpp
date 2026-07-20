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
#include "swg/endpoints.h"
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
#include "swg/vtbl_resolve.h" // free-cam: SWGEmu alter-slot self-check (.cpp-only include -> not CppSharp-parsed)

// CONSULT-69 .ilf probe tick, defined in ui/cui_manager.cpp. Forward-declared here (not in a header)
// so the probe adds zero CppSharp-parsed surface.
namespace utinni::cuiHud
{
void ilfProbeTick();
void ilfProbeReset();
} // namespace utinni::cuiHud

#include <atomic>
#include <cstdio>
#include <intrin.h>
#include <mutex>
#include <vector>

// Wave-2 scene-arm (defined in game.cpp): once-per-scene setSceneCallbacks dispatch for the
// advertised client, driven from hkUpdateLoop's first tick below (setupScene is un-detourable
// there, so a normal login otherwise never fires scene-active). Cross-TU forward declaration,
// same pattern as game.cpp's declaration of clearAdvertisedInstance().
namespace swg::game
{
void notifyAdvertisedSceneTick();
}

namespace swg::groundScene
{
using pCtor = utinni::GroundScene*(__thiscall*)(void* pThis, const char* terrainFilename, const char* avatarObjectFilename, swgptr customPlayer); // Offline scene ctor
using pReloadTerrain = void(__thiscall*)(utinni::GroundScene* pThis);
using pChangeCamera = int(__thiscall*)(utinni::GroundScene* pThis, utinni::Camera::Modes cameraMode, float);
using pGetCurrentCamera = utinni::Camera*(__thiscall*)(utinni::GroundScene * pThis);

using pDraw = void(__thiscall*)(utinni::GroundScene* pThis);
using pUpdate = void(__thiscall*)(utinni::GroundScene* pThis, float time);
using pHandleInputMapUpdate = void(__thiscall*)(utinni::GroundScene* pThis);
using pHandleInputMapEvent = void(__thiscall*)(utinni::GroundScene* pThis, utinni::IoEvent* ioEvent);

using pInit = void(__thiscall*)(utinni::GroundScene* pThis, const char* terrain, utinni::Object* playerObj, float time);

// v13 (free-cam): advertised-ONLY accessors replacing fragile NGE struct offsets. null on SWGEmu (the
// consumer keeps its currentView read + debugPortalCameraInputMap+0xC path there); the resolver fills
// these by name on the advertised client. getDebugPortalCameraMessageQueue returns the input MQ.
using pIsFreeCameraActive = bool(__thiscall*)(utinni::GroundScene* pThis);
using pGetDebugPortalCameraMessageQueue = utinni::MessageQueue*(__thiscall*)(utinni::GroundScene * pThis);

pCtor ctor = (pCtor)0x00519830; // Offline scene ctor
pReloadTerrain reloadTerrain = (pReloadTerrain)0x0051A4F0;
pChangeCamera changeCamera = (pChangeCamera)0x0051A350;
pGetCurrentCamera getCurrentCamera = (pGetCurrentCamera)0x0051A4D0;

pDraw draw = (pDraw)0x0051B770;
pUpdate update = (pUpdate)0x0051AF10;
pHandleInputMapUpdate handleInputMapUpdate = (pHandleInputMapUpdate)0x0051AB20;
pHandleInputMapEvent handleInputMapEvent = (pHandleInputMapEvent)0x0051AA40;

pInit init = (pInit)0x00518EB0;

pIsFreeCameraActive isFreeCameraActive = nullptr;
pGetDebugPortalCameraMessageQueue getDebugPortalCameraMessageQueue = nullptr;
} // namespace swg::groundScene

// WS-4 Terrain slice: latched live GroundScene instance for the advertised client (which has NO GroundScene
// singleton in the GetEngineHookPoints catalog -- 0x190885C is an unadvertised SWGEmu global). Set per-frame
// from hkUpdateLoop (advertised real-entry, probe-validated); cleared in hkCleanupScene before engine
// teardown. Read ONLY by the dedicated utinni_reloadCurrentTerrain() export below. CRUCIALLY,
// GroundScene::get() deliberately STAYS nullptr on advertised -- we do NOT widen the shared accessor, so no
// dormant editor loop (FreeCamImpl/GroundSceneImpl/SnapshotPanel, all gated on `GroundScene.Get() != null`
// and started by the WS-1 setupScene callbacks) wakes -> zero blast radius (Codex+Cursor HIGH). Game-thread
// only (hkUpdateLoop / hkMainLoop drain / hkCleanupScene all run there); std::atomic is belt-and-suspenders.
static std::atomic<utinni::GroundScene*> s_advertisedGroundScene{nullptr};

// v13 free-cam (advertised). On the NGE client the engine's DebugPortalCamera flies natively from its own
// input map (groundinputmap_debugportalcamera.iff): right-mouse -> CM_mouseWalk (forward) + mouse-look work
// out of the box once changeCamera(cm_Free) selects the debug-portal view. We do NOT touch CuiIoWin keyboard
// capture: globally releasing it killed the UI's keyboard (chat/Enter) and the input map doesn't bind WASD
// anyway, so the release bought nothing. WASD movement (feed CM_walk/down/left/right to the camera queue from
// a non-UI-disruptive input read) is a follow-up. Tracks OUR engaged state (NOT isFreeCameraActive(), which
// reads true from scene-load -- the loading screen forces view CI_debugPortal) to drive the toggle direction.
static std::atomic<bool> s_advFreeCamEngaged{false};

namespace swg::groundScene
{
// Called from game.cpp hkCleanupScene (forward-declared there) to drop the latch before the engine tears the
// scene down. hkUpdateLoop also re-stores the current instance every frame, so the only stale window is a
// reload queued during a teardown gap with no intervening update -- narrow + bounded (reload no-ops on null).
void clearAdvertisedInstance()
{
    s_advertisedGroundScene.store(nullptr, std::memory_order_relaxed);
    // Scene teardown: drop free-cam engagement so the next scene starts un-engaged.
    s_advFreeCamEngaged.store(false, std::memory_order_release);
}
} // namespace swg::groundScene

// Phase 3 R-A native-side (per 03-CONTEXT D-08/D-09): handle-based registries
// backed by insertion-order std::vector<{handle, fn_ptr}>. Handle 0 reserved as
// invalid sentinel.
//
// CR-01 (03-REVIEW): per-registry std::mutex serializes Subscribe / Unsubscribe
// writes against the snapshot-build read in the dispatch sites.
//
// 2026-05-22 (post-CODEX-consult): switched from std::unordered_map (heap-allocated
// bucket nodes + per-frame std::vector::reserve() snapshot allocation in
// hkDrawLoop/hkUpdateLoop) to insertion-order vector with stack-allocated
// fixed-size snapshot. Per-frame heap allocation in the render hot path was
// implicated in a scene-change AV at SWG 0x0051fb0a (inside GroundScene::ctor).
// See .planning/debug/03-scene-change-av-0x0051fb0a.md and
// .planning/debug/codex-consult-ground-scene-av.md for the 11-cycle bisect and
// CODEX's fix recommendation.
namespace
{
template <typename Fn>
struct CallbackEntry
{
    int handle;
    Fn func;
};

// Dispatch helper: build a stack-allocated snapshot under the registry's mutex,
// release the lock, then invoke each callback via `invoke`. kInlineCap=16 gives
// 16x headroom over the current native subscriber count (1 per registry: the
// managed bridge in UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs). If a
// registry ever exceeds kInlineCap, fall back to a heap snapshot for that
// dispatch -- heap allocation is gated to N>16, so the hot path stays heap-free
// in practice. Lambda `invoke(Fn)` is called with each function pointer outside
// the lock per R-H D-12 snapshot semantics: callback bodies may Subscribe /
// Unsubscribe safely; new subscribers fire on the NEXT dispatch.
template <typename Fn, typename Invoke>
void dispatchSnapshot(
    const std::vector<CallbackEntry<Fn>>& registry,
    std::mutex& mutex,
    Invoke&& invoke)
{
    constexpr size_t kInlineCap = 16;
    Fn stackSnap[kInlineCap];
    Fn* snapshot = stackSnap;
    std::vector<Fn> heapSnap; // capacity 0 until reserve() -> no heap alloc on the hot path
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

static std::vector<CallbackEntry<void (*)(utinni::GroundScene* pThis)>> preDrawLoopCallbacks;
static std::vector<CallbackEntry<void (*)(utinni::GroundScene* pThis)>> postDrawLoopCallbacks;
static std::vector<CallbackEntry<void (*)(utinni::GroundScene* pThis, float time)>> updateLoopCallbacks;
static std::vector<CallbackEntry<void (*)()>> cameraChangeCallbacks;
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
    // WS-1 (advertised-client RVA-safety). 0x190885C is the HARDCODED SWGEmu GroundScene-instance global,
    // NOT in the GetEngineHookPoints catalog -> garbage on the advertised DX11 client. Before WS-1 the editor
    // panels stayed disabled there (no scene-change notification fired), so nothing called this; WS-1's
    // UpdateSceneAvailability(true) now ENABLES them, exposing every editor action that does GroundScene.Get().
    // Return nullptr on the advertised client so those degrade to an isolated C# null-ref (caught per-subscriber
    // by GameCallbacks) instead of a native 0xC0000005. The engine's own scene/render is unaffected (it does
    // NOT use this consumer accessor). SWGEmu is unchanged -- isAdvertisedClient() is false there (D-00). The
    // remaining GroundScene editor-action surface (reloadTerrain/freecam/weather) is a per-editor WS-4 slice.
    if (swg::endpoints::isAdvertisedClient())
    {
        return nullptr;
    }

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

// Wave 3 (v19): the current scene name derived from the loaded terrain (GroundScene::getName's
// logic without needing a GroundScene instance -- getName touches only Terrain::get(), which is
// advertised-safe via the terrain-editor path, NOT the nulled GroundScene::get()). Used by the
// WorldSnapshotLive reload leg (unload + advertised load(sceneName)). Empty if no scene.
std::string getCurrentSceneNameFromTerrain()
{
    const std::string terrainPath = Terrain::get()->getFilename();
    if (terrainPath.empty())
    {
        return "";
    }

    const int i = terrainPath.find_first_of('/') + 1;
    const int length = static_cast<int>(terrainPath.size()) - i - 5;
    if (length < 0)
    {
        return "";
    }

    return terrainPath.substr(i, length);
}

// Phase 3 R-A: handle-based Subscribe/Unsubscribe per D-08/D-09. Add* retained
// per D-10 as wrappers (return value discarded).
int GroundScene::subscribePreDrawLoopCallback(void (*func)(GroundScene* pThis))
{
    std::lock_guard<std::mutex> guard(preDrawLoopCallbacksMutex);
    int id = s_nextPreDrawId++;
    if (id == 0)
    {
        id = s_nextPreDrawId++;
    } // WR-04 skip-zero
    preDrawLoopCallbacks.push_back({id, func});
    return id;
}

bool GroundScene::unsubscribePreDrawLoopCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(preDrawLoopCallbacksMutex);
    for (auto it = preDrawLoopCallbacks.begin(); it != preDrawLoopCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            preDrawLoopCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

int GroundScene::subscribePostDrawLoopCallback(void (*func)(GroundScene* pThis))
{
    std::lock_guard<std::mutex> guard(postDrawLoopCallbacksMutex);
    int id = s_nextPostDrawId++;
    if (id == 0)
    {
        id = s_nextPostDrawId++;
    } // WR-04 skip-zero
    postDrawLoopCallbacks.push_back({id, func});
    return id;
}

bool GroundScene::unsubscribePostDrawLoopCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(postDrawLoopCallbacksMutex);
    for (auto it = postDrawLoopCallbacks.begin(); it != postDrawLoopCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            postDrawLoopCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

int GroundScene::subscribeUpdateLoopCallback(void (*func)(GroundScene* pThis, float elapsedTime))
{
    std::lock_guard<std::mutex> guard(updateLoopCallbacksMutex);
    int id = s_nextUpdateId++;
    if (id == 0)
    {
        id = s_nextUpdateId++;
    } // WR-04 skip-zero
    updateLoopCallbacks.push_back({id, func});
    return id;
}

bool GroundScene::unsubscribeUpdateLoopCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(updateLoopCallbacksMutex);
    for (auto it = updateLoopCallbacks.begin(); it != updateLoopCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            updateLoopCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

int GroundScene::subscribeCameraChangeCallback(void (*func)())
{
    std::lock_guard<std::mutex> guard(cameraChangeCallbacksMutex);
    int id = s_nextCameraChangeId++;
    if (id == 0)
    {
        id = s_nextCameraChangeId++;
    } // WR-04 skip-zero
    cameraChangeCallbacks.push_back({id, func});
    return id;
}

bool GroundScene::unsubscribeCameraChangeCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(cameraChangeCallbacksMutex);
    for (auto it = cameraChangeCallbacks.begin(); it != cameraChangeCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            cameraChangeCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

void GroundScene::addPreDrawLoopCallback(void (*func)(GroundScene* pThis))
{
    subscribePreDrawLoopCallback(func);
}

void GroundScene::addPostDrawLoopCallback(void (*func)(GroundScene* pThis))
{
    subscribePostDrawLoopCallback(func);
}

void __fastcall hkDrawLoop(GroundScene* pThis, DWORD EDX)
{
    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot. Stack-snapshot
    // via dispatchSnapshot keeps the per-frame path heap-free (see file header
    // comment for 2026-05-22 fix rationale).
    dispatchSnapshot(preDrawLoopCallbacks, preDrawLoopCallbacksMutex,
                     [pThis](void (*func)(GroundScene*))
                     { func(pThis); });

    swg::groundScene::draw(pThis);

    dispatchSnapshot(postDrawLoopCallbacks, postDrawLoopCallbacksMutex,
                     [pThis](void (*func)(GroundScene*))
                     { func(pThis); });
}

void GroundScene::addUpdateLoopCallback(void (*func)(GroundScene* pThis, float elapsedTime))
{
    subscribeUpdateLoopCallback(func);
}

void GroundScene::addCameraChangeCallback(void (*func)())
{
    subscribeCameraChangeCallback(func);
}

void __fastcall hkUpdateLoop(GroundScene* pThis, DWORD EDX, float time)
{
    // WS-4 Terrain PROBE (2026-06-26): confirm the advertised `update` real-entry detour fires per-frame
    // with a STABLE non-null GroundScene `this` -- the one unproven Option-A assumption (Codex+Cursor review)
    // before we rely on it to latch the instance for GroundScene::get(). Rate-limited + advertised-only ->
    // SWGEmu byte-behavior unchanged (D-00). Pure diagnostic: does NOT yet feed GroundScene::get(), so no
    // blast-radius widening of the shared accessor until this probe passes.
    if (swg::endpoints::isAdvertisedClient())
    {
        // WS-4: latch the live instance for the reload export. Store BEFORE dispatch so a same-frame consumer
        // sees the current scene. (Probe log retained -- confirms the latch source keeps firing.)
        s_advertisedGroundScene.store(pThis, std::memory_order_relaxed);

        // Wave-2 scene-arm: the first update tick after boot/cleanup IS scene-start on the
        // advertised client (setupScene is un-detourable there, so a normal login never fires
        // setSceneCallbacks natively). Once-latched in game.cpp; re-armed by hkCleanupScene.
        swg::game::notifyAdvertisedSceneTick();

        // Wave-3 gizmo unlock: GroundScene::draw is an UNADVERTISED virtual so hkDrawLoop never
        // installs here -- dispatch the preDrawLoop queue (the gizmo enable/disable marshal) from
        // the update tick instead. Safe as of v19: imgui_impl::draw() now reads the camera via the
        // advertised camera::getTransformO2W/getProjectionMatrix rows (rider 4C) instead of the raw
        // NGE-unsafe projectionMatrix offset that crashed the §5.6 probe. Same game thread, same
        // per-frame cadence; postDraw stays undispatched on advertised (no consumer, no blast radius).
        dispatchSnapshot(preDrawLoopCallbacks, preDrawLoopCallbacksMutex,
                         [pThis](void (*func)(GroundScene*))
                         { func(pThis); });

        // CONSULT-69 .ilf pointer-selection probe (cui_hud.cpp): no-op while disarmed.
        utinni::cuiHud::ilfProbeTick();

        static std::atomic<int> s_updateProbeCount{0};
        const int n = s_updateProbeCount.fetch_add(1, std::memory_order_relaxed);
        if (n < 5 || n == 300)
        {
            char m[160];
            snprintf(m, sizeof(m), "hkUpdateLoop[probe %d]: advertised update fired pThis=0x%p time=%.4f", n, (void*)pThis, time);
            utinni::log::info(m);
        }
    }

    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot. Stack-snapshot
    // via dispatchSnapshot keeps the per-frame path heap-free.
    dispatchSnapshot(updateLoopCallbacks, updateLoopCallbacksMutex,
                     [pThis, time](void (*func)(GroundScene*, float))
                     { func(pThis, time); });

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
    // siphoned off before this point. Filter out the noisy per-frame/continuous events so the 40-entry cap
    // captures the interesting ones (KeyDown=7 / KeyUp=8 / MouseButton=15/16): skip Prepare(3), Update(4),
    // MouseMove(14), SetSystemMouseCursorPosition(17). This is how the free-cam smoke confirms whether
    // IOET_KeyDown actually reaches GroundScene after the CuiIoWin keyboard-capture release.
    const bool noisyContinuous = ioEvent != nullptr &&
                                 (ioEvent->type == 3 || ioEvent->type == IoEvent::t_Update || ioEvent->type == 14 || ioEvent->type == 17);
    if (ioEvent != nullptr && !noisyContinuous)
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

    if (ioEvent != nullptr && pThis->isFreeCameraActive())
    {
        // v13 (free-cam): pass the live `this` -- processIoEvent no longer calls GroundScene::get() (nullptr
        // on the advertised client). pThis is the valid GroundScene on both targets. ioEvent null-guarded
        // (pre-smoke review) since processIoEvent dereferences ioEvent->type and this path is now live on advertised.
        debugCamera::processIoEvent(pThis, ioEvent);
    }

    swg::groundScene::handleInputMapEvent(pThis, ioEvent);
}

void GroundScene::detour()
{
    // WS-4 Terrain PROBE: per-target SPLIT. On SWGEmu all three install + setPreloadSnapshot runs (D-00).
    // On the advertised client install ONLY `update` (the Option-A latch precondition); everything else is
    // skipped to keep the probe minimal and avoid the reverted-unlock surface.
    //
    // draw is an UNADVERTISED virtual -> gate with !isAdvertisedClient(), NOT installable(): a stale SWGEmu
    // literal can land on committed+executable relocated code and wrongly pass installable() -> DetourXS
    // corrupts that code (the CuiStringIds crash class -- flagged HIGH in the Codex+Cursor review).
    if (!swg::endpoints::isAdvertisedClient())
        swg::groundScene::draw = (swg::groundScene::pDraw)Detour::Create(swg::groundScene::draw, hkDrawLoop, DETOUR_TYPE_PUSH_RET);

    // update is an advertised CLEAN row (v3 real-entry, delta==0) -> installable()-gated, installs on BOTH.
    // PROBE: this is the ONLY groundScene detour on advertised; hkUpdateLoop logs whether it fires with a
    // stable pThis. Log the bound address first (confirm the resolver rebound it off the SWGEmu literal).
    if (swg::endpoints::installable((const void*)swg::groundScene::update))
    {
        if (swg::endpoints::isAdvertisedClient())
        {
            char m[128];
            snprintf(m, sizeof(m), "groundScene::update: installing advertised detour on bound=0x%p", (void*)swg::groundScene::update);
            utinni::log::info(m);
        }
        swg::groundScene::update = (swg::groundScene::pUpdate)Detour::Create(swg::groundScene::update, hkUpdateLoop, DETOUR_TYPE_PUSH_RET);
    }

    // handleInputMapEvent is an advertised real-entry row -> installable()-gated, installs on BOTH (v13
    // free-cam un-skip). On advertised it routes WASD into the free-cam input path (hkHandleInputEvent ->
    // processIoEvent with the live pThis). Inert until free-cam is toggled (the isFreeCameraActive() gate).
    if (swg::endpoints::installable((const void*)swg::groundScene::handleInputMapEvent))
        swg::groundScene::handleInputMapEvent = (swg::groundScene::pHandleInputMapEvent)Detour::Create(swg::groundScene::handleInputMapEvent, hkHandleInputEvent, DETOUR_TYPE_PUSH_RET);

    // setPreloadSnapshot writes a hardcoded SWGEmu DATA global (0x191113C) unmapped on the advertised client
    // -> 0xC0000005 WRITE during createDetours. installable() is an EXECUTABLE-target check (wrong for a data
    // write), so gate on the dual-path flag directly: SWGEmu writes the flag; the advertised client skips it.
    if (!swg::endpoints::isAdvertisedClient())
        WorldSnapshot::setPreloadSnapshot(false);
}

void GroundScene::removeDetour()
{
    // Detour::Remove((LPVOID)swg::groundScene::handleInputMapUpdate);
}

Camera* GroundScene::getCurrentCamera()
{
    return swg::groundScene::getCurrentCamera(this);
}

void GroundScene::toggleFreeCamera()
{
    if (swg::endpoints::isAdvertisedClient())
    {
        // ADVERTISED (NGE): toggle on OUR engaged flag, not isFreeCameraActive() (which reads true from
        // scene-load). The engine's native DebugPortalCamera::alter does the flying; we only switch the view
        // (selects the debug-portal input map) and release/restore CuiIoWin keyboard capture so IOET_KeyDown
        // reaches that input map. No alter detour -- native alter handles movement (our hkAlter would double-move).
        if (s_advFreeCamEngaged.load(std::memory_order_acquire))
        {
            swg::groundScene::changeCamera(this, Camera::Modes::cm_FreeChase, 0);
            s_advFreeCamEngaged.store(false, std::memory_order_release);
            utinni::log::info("freecam[advertised]: OFF -- changeCamera(cm_FreeChase)");
        }
        else
        {
            swg::groundScene::changeCamera(this, Camera::Modes::cm_Free, 0);
            s_advFreeCamEngaged.store(true, std::memory_order_release);
            utinni::log::info("freecam[advertised]: ON -- changeCamera(cm_Free) (mouse-fly: right-mouse=forward + look)");
        }
    }
    else if (isFreeCameraActive())
    {
        swg::groundScene::changeCamera(this, Camera::Modes::cm_FreeChase, 0);
    }
    else
    {
        swg::groundScene::changeCamera(this, Camera::Modes::cm_Free, 0);

        // Empirical slot-4 self-check (SWGEmu only -- the advertised path no longer installs hkAlter; native
        // DebugPortalCamera::alter flies the camera there). On SWGEmu the alter override sits at the known RVA,
        // so vtbl::slot(debugCamera, kObjectAlter) MUST equal it -- a mismatch means kObjectAlter is wrong.
        static std::atomic<bool> s_alterSlotChecked{false};
        if (!s_alterSlotChecked.exchange(true))
        {
            const void* slot = swg::vtbl::slot(getCurrentCamera(), swg::vtbl::kObjectAlter);
            const void* knownSwgemuAlter = reinterpret_cast<const void*>(0x006DA1B0);
            char m[160];
            if (slot != knownSwgemuAlter)
            {
                snprintf(m, sizeof(m), "WARNING freecam: SWGEmu alter slot %d=0x%p != the known alter RVA -- kObjectAlter index is WRONG", swg::vtbl::kObjectAlter, slot);
            }
            else
            {
                snprintf(m, sizeof(m), "freecam: SWGEmu alter slot-%d self-check PASSED (matches the known alter RVA)", swg::vtbl::kObjectAlter);
            }
            utinni::log::info(m);
        }
    }

    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot. Stack-snapshot
    // via dispatchSnapshot keeps the path heap-free (toggleFreeCamera is not
    // per-frame, but uses the same pattern for consistency).
    //
    // SWGEmu ONLY: the camera-change callback fires the managed FreeCamImpl.OnCameraChangeCallback (panel
    // availability UI). On the advertised client this dispatch crashed (cdb: a dangling managed-delegate
    // thunk in the CLR JIT region -- this path is reached for the first time there, via our own toggle).
    // The camera flies without it (the panel toggle is force-enabled separately); skip it on advertised.
    if (!swg::endpoints::isAdvertisedClient())
    {
        dispatchSnapshot(cameraChangeCallbacks, cameraChangeCallbacksMutex,
                         [](void (*func)())
                         { func(); });
    }
}

void GroundScene::changeCameraMode(int cameraMode)
{
    swg::groundScene::changeCamera(this, (Camera::Modes)cameraMode, 0);
}

bool GroundScene::isFreeCameraActive() const
{
    // v13 (free-cam): on the advertised client use the advertised accessor (provider getCurrentView() ==
    // CI_debugPortal) instead of the currentView struct-field read. null on SWGEmu -> the field read (D-00).
    if (swg::groundScene::isFreeCameraActive != nullptr)
    {
        return swg::groundScene::isFreeCameraActive(const_cast<GroundScene*>(this));
    }
    // FAIL-CLOSED (pre-smoke review): a null accessor on the advertised client means the row failed to resolve
    // -> do NOT read currentView at the SWGEmu struct offset (garbage bool would drive the per-input MQ paths).
    if (swg::endpoints::isAdvertisedClient())
    {
        return false;
    }
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
    {                                                                             // ToDO see if this can be removed
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

    memcpy((void*)(((int)obj) + 0x50), (void*)(((int)player) + 0x50), 0x30u); // Todo see if it can be replaced

    renderWorld::addObjectNotifications(obj);

    obj->addToWorld();
}
} // namespace utinni

// WS-4 Terrain reload export. `ClientReloadDispatcher` calls this instead of `GroundScene.Get().ReloadTerrain()`
// so terrain reload works on BOTH targets WITHOUT flipping the shared `GroundScene::get()` accessor (which on
// the advertised client would wake dormant Tier-2 editor loops -> the blast-radius HIGH from the Codex+Cursor
// review). SWGEmu: resolves the instance from the real singleton -> functionally identical to the old path
// (D-00). Advertised: uses the per-frame latched instance (nullptr until the first post-load update / after
// cleanup -> safe no-op, not an NRE on the unguarded mainLoop drain). `reloadTerrain` itself is an advertised
// CLEAN row, so the call hits the relocated provider entry on advertised.
extern "C" __declspec(dllexport) void __cdecl utinni_reloadCurrentTerrain()
{
    utinni::GroundScene* gs = swg::endpoints::isAdvertisedClient()
                                  ? s_advertisedGroundScene.load(std::memory_order_relaxed)
                                  : utinni::GroundScene::get();
    if (gs != nullptr)
    {
        swg::groundScene::reloadTerrain(gs);
    }
}

// v13 free-cam exports (Wave 4). The managed FreeCamImpl drives free-cam through these instead of
// GroundScene.Get().{ToggleFreeCamera,IsFreeCameraActive} -- GroundScene.Get() is nullptr on the advertised
// client by design (so dormant Tier-2 editor loops stay asleep). Same latch pattern as the terrain reload:
// SWGEmu resolves the real singleton (D-00 identical), advertised uses the per-frame latched instance (null
// no-op / false until a scene is loaded). toggleFreeCamera also lazily installs the advertised alter detour.
// Game-thread only (the managed side marshals via AddUpdateLoopCall, which fires in hkUpdateLoop).
extern "C" __declspec(dllexport) void __cdecl utinni_toggleFreeCamera()
{
    utinni::GroundScene* gs = swg::endpoints::isAdvertisedClient()
                                  ? s_advertisedGroundScene.load(std::memory_order_relaxed)
                                  : utinni::GroundScene::get();
    if (gs != nullptr)
    {
        gs->toggleFreeCamera();
    }
}

extern "C" __declspec(dllexport) bool __cdecl utinni_isFreeCameraActive()
{
    utinni::GroundScene* gs = swg::endpoints::isAdvertisedClient()
                                  ? s_advertisedGroundScene.load(std::memory_order_relaxed)
                                  : utinni::GroundScene::get();
    return (gs != nullptr) && gs->isFreeCameraActive();
}
