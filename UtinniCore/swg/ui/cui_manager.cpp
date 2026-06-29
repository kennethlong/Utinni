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

#include "cui_manager.h"
#include "swg/endpoints.h"
#include "swg/camera/camera.h"
#include "swg/misc/swg_string.h"
#include "utility/log.h"

#include <cstdio>
#include <mutex>
#include <vector>

namespace swg::cuiManager
{
using pRender = void(__thiscall*)(swgptr pThis);
using pFindObjectUnderCursor = utinni::Object*(__cdecl*)(utinni::Camera * camera, math::Vector* worldStart, math::Vector* worldEnd, utinni::Object* player);

using pSetSize = void(__cdecl*)(int width, int height);
using pTogglePointer = void(__cdecl*)(bool isOn);

using pRestartMusic = void(__cdecl*)(bool notPlaying);

pRender render = (pRender)0x00881210;
pFindObjectUnderCursor findObjectUnderCursor = (pFindObjectUnderCursor)0x00BD3E20;

pSetSize setSize = (pSetSize)0x00882410;
pTogglePointer togglePointer = (pTogglePointer)0x00881940;

pRestartMusic restartMusic = (pRestartMusic)0x00881560;

// Phase 24 / D-04 accessor-style global. Provider advertises cuiManager::g_instance
// as &CuiManager::getIoWin (call-not-read): CuiManager is all-static (no instance);
// getIoWin() returns the CuiIoWin singleton. No SWGEmu RVA literal -- the consumer has
// no g_instance read-site today, so this slot is bound for catalog completeness and
// resolves only on the advertised client (null on SWGEmu).
using pGetIoWin = swgptr(__cdecl*)();
pGetIoWin g_instance = nullptr;
} // namespace swg::cuiManager

namespace swg::uiManager
{
using pDrawCursor = void(__thiscall*)(utinni::UiManager* pThis, bool value);

pDrawCursor drawCursor = (pDrawCursor)0x010E8410;

} // namespace swg::uiManager

namespace swg::systemMessageManager
{
using pReceiveMessage = void(__cdecl*)(swgptr pChatSystemMsg);
using pSendMessage = void(__cdecl*)(const swg::WString& message, bool chatOnly);

pReceiveMessage receiveMessage = (pReceiveMessage)0x008ABEB0;
pSendMessage sendMessage = (pSendMessage)0x008AC250;

} // namespace swg::systemMessageManager

// Bucket A-2 (v10) world-pick / HUD-target. Advertised-ONLY accessor + getter (null on SWGEmu --
// there is no SWGEmu RVA; the resolver fills them by name on the advertised client). g_instance ->
// SwgCuiHudFactory::findMediatorForCurrentHud() (the live SwgCuiHud*, resolves HudGround/HudSpace).
// getTarget -> the provider's __fastcall thunk over SwgCuiHud::getLastSelectedObject() const (the
// last world-picked Object*). Opaque swgptr -- a consumer accessor will cast when world-pick is wired.
namespace swg::cuiHud
{
using pGetInstance = swgptr(__cdecl*)();
using pGetTarget = swgptr(__thiscall*)(swgptr pHud);
pGetInstance g_instance = nullptr;
pGetTarget getTarget = nullptr;
} // namespace swg::cuiHud

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

static std::vector<CallbackEntry<void (*)(const char* msg)>> receiveSystemMessageCallbacks;
static std::mutex receiveSystemMessageCallbacksMutex;
static int s_nextReceiveSystemMessageId = 1;

namespace utinni
{
bool isRenderingUi = false;
bool hasObjectUnderCursor = false;

void CuiManager::setSize(int width, int height)
{
    swg::cuiManager::setSize(width, height);
}

void CuiManager::togglePointer(bool isOn)
{
    swg::cuiManager::togglePointer(isOn);
}

bool CuiManager::isRenderingUi()
{
    return utinni::isRenderingUi;
}

bool CuiManager::hasObjectUnderCursor()
{
    return utinni::hasObjectUnderCursor;
}

Object* CuiManager::getSelectedObject()
{
    // Advertised-only world-pick (Bucket A-2): read the live HUD's last-selected object via the
    // advertised cuiHud rows (g_instance -> the live SwgCuiHud*; getTarget -> its last-selected
    // Object*). Null on SWGEmu / pre-v10 (slots null) or when no hud is live (not in-world). Pure
    // getter -- no detour, no per-frame cost; safe to call any time.
    if (swg::cuiHud::g_instance == nullptr || swg::cuiHud::getTarget == nullptr)
    {
        log::info("CuiManager::getSelectedObject: cuiHud rows not advertised (g_instance/getTarget null) -- pre-v10 / SWGEmu");
        return nullptr;
    }
    const swgptr hud = swg::cuiHud::g_instance();
    const swgptr obj = (hud != 0) ? swg::cuiHud::getTarget(hud) : 0;

    // Diagnostic: which is empty? hud==0 -> findMediatorForCurrentHud gave no live HUD; hud!=0 &&
    // obj==0 -> HUD is live but getLastSelectedObject returned null (the NGE blue-selection may live
    // in a different member than the mapped m_lastSelectedObject -> a provider re-map question).
    char m[96];
    std::snprintf(m, sizeof(m), "CuiManager::getSelectedObject: hud=0x%p getLastSelectedObject=0x%p", (void*)hud, (void*)obj);
    log::info(m);

    return reinterpret_cast<Object*>(obj);
}

void CuiManager::restartMusic()
{
    swg::cuiManager::restartMusic(true);
}

void __fastcall hkRender(swgptr pThis, void* useless)
{
    isRenderingUi = true;
    swg::cuiManager::render(pThis);
    isRenderingUi = false;
}

Object* __cdecl hkFindObjectUnderCursor(Camera* camera, swg::math::Vector* worldStart, swg::math::Vector* worldEnd, Object* player)
{
    Object* result = swg::cuiManager::findObjectUnderCursor(camera, worldStart, worldEnd, player);

    hasObjectUnderCursor = result != nullptr;

    return result;
}

void CuiManager::detour()
{
    // WS-4 (advertised-client MISC slice): per-detour SPLIT -- the two CuiManager hooks have different
    // advertised-safety, and the gating idiom differs per case (the reusable pattern for the rest of the
    // MISC/INPUT unlock).
    //
    // render: advertised CLEAN row -> the resolver rebinds the SWGEmu literal 0x00881210 to the relocated
    // provider entry before createDetours(), so installable() is authoritative. Install on BOTH targets.
    // hkRender only brackets the engine render with isRenderingUi, which is read ONLY by the D3D9 wireframe
    // path (directx9.cpp) -> inert on the D3D11 advertised client, so this cannot perturb the working overlay.
    if (swg::endpoints::installable((const void*)swg::cuiManager::render))
    {
        swg::cuiManager::render = (swg::cuiManager::pRender)Detour::Create(swg::cuiManager::render, hkRender, DETOUR_TYPE_PUSH_RET);
    }

    // findObjectUnderCursor (0x00BD3E20) is NOT in the advertised catalog -> the resolver never rebinds it,
    // so on the advertised client the literal is a STALE SWGEmu RVA that lands on unrelated relocated code.
    // installable() is necessary-not-sufficient here (committed+executable WRONGLY passes on the stale addr
    // -> DetourXS corrupts that code, the CuiStringIds-region crash class), so gate it OFF explicitly on the
    // advertised client. SWGEmu installs it byte-for-byte (D-00); hasObjectUnderCursor stays false on
    // advertised (safe default -- the click-through accessor just reports "no object under cursor").
    if (!swg::endpoints::isAdvertisedClient())
    {
        swg::cuiManager::findObjectUnderCursor = (swg::cuiManager::pFindObjectUnderCursor)Detour::Create(swg::cuiManager::findObjectUnderCursor, hkFindObjectUnderCursor, DETOUR_TYPE_PUSH_RET);
    }
}

UiManager* UiManager::get()
{
    return memory::read<UiManager*>(0x1996E98);
}

void UiManager::drawCursor(bool value)
{
    swg::uiManager::drawCursor(this, value);
}

// Phase 3 R-A: handle-based Subscribe/Unsubscribe per D-08/D-09.
int SystemMessageManager::subscribeReceiveMessageCallback(void (*func)(const char* msg))
{
    std::lock_guard<std::mutex> guard(receiveSystemMessageCallbacksMutex);
    int id = s_nextReceiveSystemMessageId++;
    if (id == 0)
    {
        id = s_nextReceiveSystemMessageId++;
    } // WR-04 skip-zero
    receiveSystemMessageCallbacks.push_back({id, func});
    return id;
}

bool SystemMessageManager::unsubscribeReceiveMessageCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(receiveSystemMessageCallbacksMutex);
    for (auto it = receiveSystemMessageCallbacks.begin(); it != receiveSystemMessageCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            receiveSystemMessageCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

void SystemMessageManager::addReceiveMessageCallback(void (*func)(const char* msg))
{
    subscribeReceiveMessageCallback(func);
}

void SystemMessageManager::sendMessage(const char* message, bool chatOnly)
{
    swg::systemMessageManager::sendMessage(swg::WString(message), chatOnly);
}

void __cdecl hkReceiveMessage(swgptr pChatSystemMsg)
{
    const auto msg = memory::read<swg::WString>(pChatSystemMsg + 0x44); // ToDo do this proper int he future

    if (msg.isEmpty())
    {
        return;
    }

    const std::string msgStr = msg.toString();

    if (!msgStr._Starts_with("[____hidden____]"))
    {
        swg::systemMessageManager::receiveMessage(pChatSystemMsg);
    }

    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot. Stack-snapshot
    // via dispatchSnapshot keeps the path heap-free.
    const char* msgCStr = msgStr.c_str();
    dispatchSnapshot(receiveSystemMessageCallbacks, receiveSystemMessageCallbacksMutex,
                     [msgCStr](void (*func)(const char*))
                     { func(msgCStr); });
}

void SystemMessageManager::detour()
{
    // Phase 24: skip on the advertised client when the primary target is unresolved.
    if (!swg::endpoints::installable((const void*)swg::systemMessageManager::receiveMessage))
        return;

    swg::systemMessageManager::receiveMessage = (swg::systemMessageManager::pReceiveMessage)Detour::Create(swg::systemMessageManager::receiveMessage, hkReceiveMessage, DETOUR_TYPE_PUSH_RET);
}
} // namespace utinni
