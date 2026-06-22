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

#include "creature_object.h"
#include "swg/endpoints.h"
#include "swg/object/object.h"
#include "swg/misc/network.h"

#include <mutex>
#include <vector>

namespace swg::creatureObject
{
using pSetTarget = void(__thiscall*)(swgptr pThis, const int64_t& id);

pSetTarget setTarget = (pSetTarget)0x00434AB0;
} // namespace swg::creatureObject

// Phase 3 R-A native-side (per 03-CONTEXT D-08/D-09): handle-based registry
// backed by insertion-order std::vector<{handle, fn_ptr}>. Handle 0 reserved
// as invalid sentinel.
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

static std::vector<CallbackEntry<void (*)(utinni::Object* target)>> onTargetCallbacks;
static std::mutex onTargetCallbacksMutex;
static int s_nextOnTargetId = 1;

namespace utinni::creatureObject
{

int subscribeOnTargetCallback(void (*func)(Object* target))
{
    std::lock_guard<std::mutex> guard(onTargetCallbacksMutex);
    int id = s_nextOnTargetId++;
    if (id == 0)
    {
        id = s_nextOnTargetId++;
    } // WR-04 skip-zero
    onTargetCallbacks.push_back({id, func});
    return id;
}

bool unsubscribeOnTargetCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(onTargetCallbacksMutex);
    for (auto it = onTargetCallbacks.begin(); it != onTargetCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            onTargetCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

void addOnTargetCallback(void (*func)(Object* target))
{
    subscribeOnTargetCallback(func);
}

void __fastcall hkSetTarget(swgptr pThis, DWORD EDX, const int64_t& id)
{
    swg::creatureObject::setTarget(pThis, id);

    Object* obj = Network::getObjectById(id);

    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot. Stack-snapshot
    // via dispatchSnapshot keeps the path heap-free.
    dispatchSnapshot(onTargetCallbacks, onTargetCallbacksMutex,
                     [obj](void (*func)(Object*))
                     { func(obj); });
}

void detour()
{
    // Phase 24: skip on the advertised client when the primary target is unresolved.
    if (!swg::endpoints::installable((const void*)swg::creatureObject::setTarget))
        return;

    swg::creatureObject::setTarget = (swg::creatureObject::pSetTarget)Detour::Create(swg::creatureObject::setTarget, hkSetTarget, DETOUR_TYPE_PUSH_RET);
};
} // namespace utinni::creatureObject