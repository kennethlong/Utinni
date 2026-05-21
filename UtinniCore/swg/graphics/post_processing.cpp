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

#include <mutex>
#include <unordered_map>
#include <vector>

namespace swg::bloom
{
using pPreSceneRender = void(__cdecl*)();
using pPostSceneRender = void(__cdecl*)();

pPreSceneRender preSceneRender = (pPreSceneRender)0x0064B500;
pPostSceneRender postSceneRender = (pPostSceneRender)0x0064B560;

}

// Phase 3 R-A native-side (per 03-CONTEXT D-08/D-09): handle-based registries.
// CR-01 (03-REVIEW): per-registry mutex for thread-safe Subscribe / Unsubscribe / snapshot.
static std::unordered_map<int, void(*)()> preSceneRenderCallbacks;
static std::unordered_map<int, void(*)()> postSceneRenderCallbacks;
static std::mutex preSceneRenderCallbacksMutex;
static std::mutex postSceneRenderCallbacksMutex;
static int s_nextPreSceneRenderId = 1;
static int s_nextPostSceneRenderId = 1;

namespace utinni::postProcessing
{
void __cdecl hkPreSceneRender() // Originally a Bloom class function, repurposed to be a general PostProcessing function.
{
    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot.
    std::vector<void(*)()> snapshot;
    {
        std::lock_guard<std::mutex> guard(preSceneRenderCallbacksMutex);
        snapshot.reserve(preSceneRenderCallbacks.size());
        for (const auto& kv : preSceneRenderCallbacks)
        {
            snapshot.push_back(kv.second);
        }
    }
    for (const auto& func : snapshot)
    {
        func();
    }

    swg::bloom::preSceneRender();
}

void __cdecl hkPostSceneRender() // Originally a Bloom class function, repurposed to be a general PostProcessing function.
{
    swg::bloom::postSceneRender();

    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot.
    std::vector<void(*)()> snapshot;
    {
        std::lock_guard<std::mutex> guard(postSceneRenderCallbacksMutex);
        snapshot.reserve(postSceneRenderCallbacks.size());
        for (const auto& kv : postSceneRenderCallbacks)
        {
            snapshot.push_back(kv.second);
        }
    }
    for (const auto& func : snapshot)
    {
        func();
    }
}

// Phase 3 R-A: handle-based Subscribe/Unsubscribe per D-08/D-09.
int subscribePreSceneRenderCallback(void(*func)())
{
    std::lock_guard<std::mutex> guard(preSceneRenderCallbacksMutex);
    int id = s_nextPreSceneRenderId++;
    preSceneRenderCallbacks[id] = func;
    return id;
}

bool unsubscribePreSceneRenderCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(preSceneRenderCallbacksMutex);
    return preSceneRenderCallbacks.erase(handle) > 0;
}

int subscribePostSceneRenderCallback(void(*func)())
{
    std::lock_guard<std::mutex> guard(postSceneRenderCallbacksMutex);
    int id = s_nextPostSceneRenderId++;
    postSceneRenderCallbacks[id] = func;
    return id;
}

bool unsubscribePostSceneRenderCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(postSceneRenderCallbacksMutex);
    return postSceneRenderCallbacks.erase(handle) > 0;
}

void addPreSceneRenderCallback(void(* func)())
{
    subscribePreSceneRenderCallback(func);
}

void addPostSceneRenderCallback(void(* func)())
{
    subscribePostSceneRenderCallback(func);
}

void detour()
{
    swg::bloom::preSceneRender = (swg::bloom::pPreSceneRender)Detour::Create(swg::bloom::preSceneRender, hkPreSceneRender, DETOUR_TYPE_PUSH_RET);
    swg::bloom::postSceneRender = (swg::bloom::pPostSceneRender)Detour::Create(swg::bloom::postSceneRender, hkPostSceneRender, DETOUR_TYPE_PUSH_RET);

}
}