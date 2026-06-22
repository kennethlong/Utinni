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
    swg::cuiManager::render = (swg::cuiManager::pRender)Detour::Create(swg::cuiManager::render, hkRender, DETOUR_TYPE_PUSH_RET);
    swg::cuiManager::findObjectUnderCursor = (swg::cuiManager::pFindObjectUnderCursor)Detour::Create(swg::cuiManager::findObjectUnderCursor, hkFindObjectUnderCursor, DETOUR_TYPE_PUSH_RET);
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
    if (!swg::endpoints::installable((const void*)swg::systemMessageManager::receiveMessage)) return;

    swg::systemMessageManager::receiveMessage = (swg::systemMessageManager::pReceiveMessage)Detour::Create(swg::systemMessageManager::receiveMessage, hkReceiveMessage, DETOUR_TYPE_PUSH_RET);
}
} // namespace utinni
