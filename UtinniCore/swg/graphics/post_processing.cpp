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

#include "post_processing.h"
#include "swg/endpoints.h"

#include <mutex>
#include <vector>

namespace swg::bloom
{
using pPreSceneRender = void(__cdecl*)();
using pPostSceneRender = void(__cdecl*)();

pPreSceneRender preSceneRender = (pPreSceneRender)0x0064B500;
pPostSceneRender postSceneRender = (pPostSceneRender)0x0064B560;

} // namespace swg::bloom

// Phase 3 R-A native-side (per 03-CONTEXT D-08/D-09): handle-based registries
// backed by insertion-order std::vector<{handle, fn_ptr}>.
// CR-01 (03-REVIEW): per-registry mutex for thread-safe Subscribe / Unsubscribe / snapshot.
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

static std::vector<CallbackEntry<void (*)()>> preSceneRenderCallbacks;
static std::vector<CallbackEntry<void (*)()>> postSceneRenderCallbacks;
static std::mutex preSceneRenderCallbacksMutex;
static std::mutex postSceneRenderCallbacksMutex;
static int s_nextPreSceneRenderId = 1;
static int s_nextPostSceneRenderId = 1;

namespace utinni::postProcessing
{
void __cdecl hkPreSceneRender() // Originally a Bloom class function, repurposed to be a general PostProcessing function.
{
    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot. Stack-snapshot
    // via dispatchSnapshot keeps the per-frame path heap-free.
    dispatchSnapshot(preSceneRenderCallbacks, preSceneRenderCallbacksMutex,
                     [](void (*func)())
                     { func(); });

    swg::bloom::preSceneRender();
}

void __cdecl hkPostSceneRender() // Originally a Bloom class function, repurposed to be a general PostProcessing function.
{
    swg::bloom::postSceneRender();

    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot. Stack-snapshot
    // via dispatchSnapshot keeps the per-frame path heap-free.
    dispatchSnapshot(postSceneRenderCallbacks, postSceneRenderCallbacksMutex,
                     [](void (*func)())
                     { func(); });
}

// Phase 3 R-A: handle-based Subscribe/Unsubscribe per D-08/D-09.
int subscribePreSceneRenderCallback(void (*func)())
{
    std::lock_guard<std::mutex> guard(preSceneRenderCallbacksMutex);
    int id = s_nextPreSceneRenderId++;
    if (id == 0)
    {
        id = s_nextPreSceneRenderId++;
    } // WR-04 skip-zero
    preSceneRenderCallbacks.push_back({id, func});
    return id;
}

bool unsubscribePreSceneRenderCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(preSceneRenderCallbacksMutex);
    for (auto it = preSceneRenderCallbacks.begin(); it != preSceneRenderCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            preSceneRenderCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

int subscribePostSceneRenderCallback(void (*func)())
{
    std::lock_guard<std::mutex> guard(postSceneRenderCallbacksMutex);
    int id = s_nextPostSceneRenderId++;
    if (id == 0)
    {
        id = s_nextPostSceneRenderId++;
    } // WR-04 skip-zero
    postSceneRenderCallbacks.push_back({id, func});
    return id;
}

bool unsubscribePostSceneRenderCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(postSceneRenderCallbacksMutex);
    for (auto it = postSceneRenderCallbacks.begin(); it != postSceneRenderCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            postSceneRenderCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

void addPreSceneRenderCallback(void (*func)())
{
    subscribePreSceneRenderCallback(func);
}

void addPostSceneRenderCallback(void (*func)())
{
    subscribePostSceneRenderCallback(func);
}

void detour()
{
    // Phase 24: skip on the advertised client when the primary target is unresolved.
    if (!swg::endpoints::installable((const void*)swg::bloom::preSceneRender)) return;

    swg::bloom::preSceneRender = (swg::bloom::pPreSceneRender)Detour::Create(swg::bloom::preSceneRender, hkPreSceneRender, DETOUR_TYPE_PUSH_RET);
    swg::bloom::postSceneRender = (swg::bloom::pPostSceneRender)Detour::Create(swg::bloom::postSceneRender, hkPostSceneRender, DETOUR_TYPE_PUSH_RET);
}
} // namespace utinni::postProcessing