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

#include "shader.h"
#include "swg/graphics/directx9.h"

#include <mutex>
#include <unordered_map>
#include <vector>

namespace swg::shaderPrimitiveSorter
{

}

// Phase 3 R-A native-side (per 03-CONTEXT D-08/D-09): handle-based registry
// with parameterized `void(*)(int)` callback type. Handle 0 reserved as
// invalid sentinel.
// CR-01 (03-REVIEW): per-registry mutex protects Subscribe / Unsubscribe / snapshot.
static std::unordered_map<int, void(*)(int currentPhase)> drawPhaseCallbacks;
static std::mutex drawPhaseCallbacksMutex;
static int s_nextDrawPhaseId = 1;

// Non-naked dispatch helper: called from inside the __declspec(naked)
// midPopCell function below. Lives outside the naked function so std::vector
// stack allocation + exception-unwind frames work normally. CR-01: snapshot
// built under lock, iteration runs outside the lock.
static void dispatchDrawPhaseCallbacks(int phase)
{
    std::vector<void(*)(int)> snapshot;
    {
        std::lock_guard<std::mutex> guard(drawPhaseCallbacksMutex);
        snapshot.reserve(drawPhaseCallbacks.size());
        for (const auto& kv : drawPhaseCallbacks)
        {
            snapshot.push_back(kv.second);
        }
    }
    for (const auto& func : snapshot)
    {
        func(phase);
    }
}

namespace utinni::shaderPrimitiveSorter
{
directX::DepthTexture* depthTexture;

int vecOffset = 0;
int phase = 0;
constexpr uint8_t phaseStructSize = 36;
constexpr swgptr midPopCell_Call = 0x772D60;
constexpr swgptr start_midPopCell = 0x00773E39;
constexpr swgptr return_midPopCell = 0x00773E41;
__declspec(naked) void midPopCell()
{
    __asm
    {
        mov vecOffset, esi
        pushad
        pushfd
        call midPopCell_Call
    }

    depthTexture = directX::getDepthTexture();
    phase = vecOffset / phaseStructSize;
    if (phase == depthTexture->getStage()) // divide offset by struct size to get stage
    {
        if (depthTexture != nullptr && depthTexture->isSupported() && depthTexture->getTextureDepth() != nullptr)
        {
            depthTexture->resolveDepth();
        }
    }

    // R-H snapshot dispatch per D-12: factored to a non-naked helper because
    // MSVC C2489/C3068 reject local std::vector inside __declspec(naked)
    // (the prologue elision means there is no exception-unwind frame). The
    // helper builds the snapshot vector in its own frame, then iterates.
    dispatchDrawPhaseCallbacks(phase);

    __asm
    {
        popfd
        popad
        add esi, 0x24
        jmp[return_midPopCell]
    }
}


// Phase 3 R-A: handle-based Subscribe/Unsubscribe per D-08/D-09.
int subscribeDrawPhaseCallback(void(*func)(int currentPhase))
{
    std::lock_guard<std::mutex> guard(drawPhaseCallbacksMutex);
    int id = s_nextDrawPhaseId++;
    if (id == 0) { id = s_nextDrawPhaseId++; } // WR-04 skip-zero
    drawPhaseCallbacks[id] = func;
    return id;
}

bool unsubscribeDrawPhaseCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(drawPhaseCallbacksMutex);
    return drawPhaseCallbacks.erase(handle) > 0;
}

void drawPhaseCallback(void(* func)(int currentPhase))
{
    subscribeDrawPhaseCallback(func);
}

void detour()
{
    memory::createJMP(start_midPopCell, (swgptr)midPopCell, 6);
}
}