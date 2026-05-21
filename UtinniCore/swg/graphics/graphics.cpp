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

#include "graphics.h"
#include "swg/scene/ground_scene.h"
#include "swg/client/client.h"
#include "swg/ui/cui_manager.h"
#include "directx9.h"

#include <mutex>
#include <unordered_map>
#include <vector>

namespace swg::graphics
{
using pInstall = bool(__cdecl*)();

using pUpdate = void(__cdecl*)(float elapsedTime);
using pBeginScene = void(__cdecl*)();
using pEndScene = void(__cdecl*)();

using pPresentWindow = int(__cdecl*)(HWND hwnd, int width, int height);
using pPresent = void(__cdecl*)();

using pUseHardwareCursor = bool(__cdecl*)(bool value);
using pShowMouseCursor = bool(__cdecl*)(bool isShown);
using pSetSystemMouseCursorPosition = void(__cdecl*)(int X, int Y);

using pResize = void(__cdecl*)(int width, int height);
using pFlushResources = void(__cdecl*)(bool reset);

using pTextureListReloadTextures = void(__cdecl*)();

using pSetStaticShader = void(__cdecl*)(swgptr staticShader, int pass);
using pSetObjectToWorldTransformAndScale = void(__cdecl*)(math::Transform* objecToWorld, math::Vector* scale);
using pDrawExtent = void(__cdecl*)(utinni::Extent* extent, swgptr vecArgbColor);

using pScreenshot = bool(__cdecl*)(const char* filename);

pInstall install = (pInstall)0x007548A0;

pUpdate update = (pUpdate)0x00755700; 
pBeginScene beginScene = (pBeginScene)0x00755730; 
pEndScene endScene = (pEndScene)0x00755740;

pPresentWindow presentWindow = (pPresentWindow)0x00755810;
pPresent present = (pPresent)0x00755800; 

pUseHardwareCursor useHardwareCursor = (pUseHardwareCursor)0x00755940;
pShowMouseCursor showMouseCursor = (pShowMouseCursor)0x00755A50;
pSetSystemMouseCursorPosition setSystemMouseCursorPosition = (pSetSystemMouseCursorPosition)0x00755AC0;

pResize resize = (pResize)0x00754E40;
pFlushResources flushResources = (pFlushResources)0x00755520;

pTextureListReloadTextures textureListReloadTextures = (pTextureListReloadTextures)0x00764B70;

pSetStaticShader setStaticShader = (pSetStaticShader)0x00755910;
pSetObjectToWorldTransformAndScale setObjectToWorldTransformAndScale = (pSetObjectToWorldTransformAndScale)0x00755D30;
pDrawExtent drawExtent = (pDrawExtent)0x00759A70;

pScreenshot screenshot = (pScreenshot)0x00755890;
}

static std::string screenshotsDir = "screenshots/";

// Phase 3 R-A native-side (per 03-CONTEXT D-08/D-09): handle-based registries.
// Handle 0 reserved as invalid sentinel.
// CR-01 (03-REVIEW): per-registry std::mutex serializes Subscribe / Unsubscribe
// writes against the snapshot-build read in the dispatch sites.
static std::unordered_map<int, void(*)(float elapsedTime)> preUpdateCallback;
static std::unordered_map<int, void(*)(float elapsedTime)> postUpdateCallback;

static std::unordered_map<int, void(*)()> preBeginSceneCallback;
static std::unordered_map<int, void(*)()> postBeginSceneCallback;

static std::unordered_map<int, void(*)()> preEndSceneCallback;
static std::unordered_map<int, void(*)()> postEndSceneCallback;

static std::unordered_map<int, void(*)(HWND hwnd, int width, int height)> prePresentWindowCallback;
static std::unordered_map<int, void(*)(HWND hwnd, int width, int height)> postPresentWindowCallback;

static std::unordered_map<int, void(*)()> prePresentCallback;
static std::unordered_map<int, void(*)()> postPresentCallback;

static std::mutex preUpdateCallbackMutex;
static std::mutex postUpdateCallbackMutex;
static std::mutex preBeginSceneCallbackMutex;
static std::mutex postBeginSceneCallbackMutex;
static std::mutex preEndSceneCallbackMutex;
static std::mutex postEndSceneCallbackMutex;
static std::mutex prePresentWindowCallbackMutex;
static std::mutex postPresentWindowCallbackMutex;
static std::mutex prePresentCallbackMutex;
static std::mutex postPresentCallbackMutex;

static int s_nextPreUpdateId = 1;
static int s_nextPostUpdateId = 1;
static int s_nextPreBeginSceneId = 1;
static int s_nextPostBeginSceneId = 1;
static int s_nextPreEndSceneId = 1;
static int s_nextPostEndSceneId = 1;
static int s_nextPrePresentWindowId = 1;
static int s_nextPostPresentWindowId = 1;
static int s_nextPrePresentId = 1;
static int s_nextPostPresentId = 1;

namespace utinni
{
// Phase 3 R-A: handle-based Subscribe/Unsubscribe per D-08/D-09. addX wrappers
// retained per D-10.

int Graphics::subscribePreUpdateLoopCallback(void(*func)(float elapsedTime))
{
    std::lock_guard<std::mutex> guard(preUpdateCallbackMutex);
    int id = s_nextPreUpdateId++;
    preUpdateCallback[id] = func;
    return id;
}

bool Graphics::unsubscribePreUpdateLoopCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(preUpdateCallbackMutex);
    return preUpdateCallback.erase(handle) > 0;
}

int Graphics::subscribePostUpdateLoopCallback(void(*func)(float elapsedTime))
{
    std::lock_guard<std::mutex> guard(postUpdateCallbackMutex);
    int id = s_nextPostUpdateId++;
    postUpdateCallback[id] = func;
    return id;
}

bool Graphics::unsubscribePostUpdateLoopCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(postUpdateCallbackMutex);
    return postUpdateCallback.erase(handle) > 0;
}

int Graphics::subscribePreBeginSceneCallback(void(*func)())
{
    std::lock_guard<std::mutex> guard(preBeginSceneCallbackMutex);
    int id = s_nextPreBeginSceneId++;
    preBeginSceneCallback[id] = func;
    return id;
}

bool Graphics::unsubscribePreBeginSceneCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(preBeginSceneCallbackMutex);
    return preBeginSceneCallback.erase(handle) > 0;
}

int Graphics::subscribePostBeginSceneCallback(void(*func)())
{
    std::lock_guard<std::mutex> guard(postBeginSceneCallbackMutex);
    int id = s_nextPostBeginSceneId++;
    postBeginSceneCallback[id] = func;
    return id;
}

bool Graphics::unsubscribePostBeginSceneCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(postBeginSceneCallbackMutex);
    return postBeginSceneCallback.erase(handle) > 0;
}

int Graphics::subscribePreEndSceneCallback(void(*func)())
{
    std::lock_guard<std::mutex> guard(preEndSceneCallbackMutex);
    int id = s_nextPreEndSceneId++;
    preEndSceneCallback[id] = func;
    return id;
}

bool Graphics::unsubscribePreEndSceneCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(preEndSceneCallbackMutex);
    return preEndSceneCallback.erase(handle) > 0;
}

int Graphics::subscribePostEndSceneCallback(void(*func)())
{
    std::lock_guard<std::mutex> guard(postEndSceneCallbackMutex);
    int id = s_nextPostEndSceneId++;
    postEndSceneCallback[id] = func;
    return id;
}

bool Graphics::unsubscribePostEndSceneCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(postEndSceneCallbackMutex);
    return postEndSceneCallback.erase(handle) > 0;
}

int Graphics::subscribePrePresentWindowCallback(void(*func)(HWND hwnd, int width, int height))
{
    std::lock_guard<std::mutex> guard(prePresentWindowCallbackMutex);
    int id = s_nextPrePresentWindowId++;
    prePresentWindowCallback[id] = func;
    return id;
}

bool Graphics::unsubscribePrePresentWindowCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(prePresentWindowCallbackMutex);
    return prePresentWindowCallback.erase(handle) > 0;
}

int Graphics::subscribePostPresentWindowCallback(void(*func)(HWND hwnd, int width, int height))
{
    std::lock_guard<std::mutex> guard(postPresentWindowCallbackMutex);
    int id = s_nextPostPresentWindowId++;
    postPresentWindowCallback[id] = func;
    return id;
}

bool Graphics::unsubscribePostPresentWindowCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(postPresentWindowCallbackMutex);
    return postPresentWindowCallback.erase(handle) > 0;
}

int Graphics::subscribePrePresentCallback(void(*func)())
{
    std::lock_guard<std::mutex> guard(prePresentCallbackMutex);
    int id = s_nextPrePresentId++;
    prePresentCallback[id] = func;
    return id;
}

bool Graphics::unsubscribePrePresentCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(prePresentCallbackMutex);
    return prePresentCallback.erase(handle) > 0;
}

int Graphics::subscribePostPresentCallback(void(*func)())
{
    std::lock_guard<std::mutex> guard(postPresentCallbackMutex);
    int id = s_nextPostPresentId++;
    postPresentCallback[id] = func;
    return id;
}

bool Graphics::unsubscribePostPresentCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(postPresentCallbackMutex);
    return postPresentCallback.erase(handle) > 0;
}

// Legacy add* API (D-10): wrappers around subscribe* (return value discarded).
void Graphics::addPreUpdateLoopCallback(void(*func)(float elapsedTime))
{
    subscribePreUpdateLoopCallback(func);
}

void Graphics::addPostUpdateLoopCallback(void(*func)(float elapsedTime))
{
    subscribePostUpdateLoopCallback(func);
}

void Graphics::addPreBeginSceneCallback(void(*func)())
{
    subscribePreBeginSceneCallback(func);
}

void Graphics::addPostBeginSceneCallback(void(*func)())
{
    subscribePostBeginSceneCallback(func);
}

void Graphics::addPreEndSceneCallback(void(*func)())
{
    subscribePreEndSceneCallback(func);
}

void Graphics::addPostEndSceneCallback(void(*func)())
{
    subscribePostEndSceneCallback(func);
}

void Graphics::addPrePresentWindowCallback(void(*func)(HWND hwnd, int width, int height))
{
    subscribePrePresentWindowCallback(func);
}

void Graphics::addPostPresentWindowCallback(void(*func)(HWND hwnd, int width, int height))
{
    subscribePostPresentWindowCallback(func);
}

void Graphics::addPrePresentCallback(void(*func)())
{
    subscribePrePresentCallback(func);
}

void Graphics::addPostPresentCallback(void(*func)())
{
    subscribePostPresentCallback(func);
}

void Graphics::useHardwareCursor(bool value)
{
    swg::graphics::useHardwareCursor(value);
}

void Graphics::showMouseCursor(bool isShown)
{
    swg::graphics::showMouseCursor(isShown);
}

void Graphics::setSystemMouseCursorPosition(int X, int Y)
{
    swg::graphics::setSystemMouseCursorPosition(X, Y);
}

int Graphics::getCurrentRenderTargetWidth() // 0x00754DB0 is the clients function address
{
    return memory::read<int>(0x1922E64); // static ptr to RenderTargetWidth
}

int Graphics::getCurrentRenderTargetHeight() // 0x00754DC0 is the clients function address
{
    return memory::read<int>(0x1922E60); // static ptr to RenderTargetHeight
}

void Graphics::flushResources(bool fullFlush)
{
    swg::graphics::flushResources(fullFlush);
}

void Graphics::reloadTextures()
{
    swg::graphics::textureListReloadTextures();
}

void Graphics::setStaticShader(swgptr staticShader, int pass)
{
    swg::graphics::setStaticShader(staticShader, pass);
}

void Graphics::setObjectToWorldTransformAndScale(swg::math::Transform* objecToWorld, swg::math::Vector* scale)
{
    swg::graphics::setObjectToWorldTransformAndScale(objecToWorld, scale);
}

void Graphics::drawExtent(Extent* extent, swgptr vecArgbColor)
{
    swg::graphics::drawExtent(extent, vecArgbColor);
}

bool __cdecl hkInstall()
{
    // DIAG 2026-05-19: one-shot entry/exit logs to confirm SWG reaches
    // graphics::install AND our directX::detour completes. Catches stalls
    // inside either swg::graphics::install (real SWG code) or our
    // dummy-device vtable harvest in directX::detour.
    static bool s_first = true;
    bool firstFire = s_first;
    if (firstFire)
    {
        s_first = false;
        utinni::log::info("hkInstall: ENTRY (calling swg::graphics::install)");
    }

    bool result = swg::graphics::install();

    if (firstFire)
    {
        char msg[96];
        snprintf(msg, sizeof(msg), "hkInstall: swg::graphics::install returned %d; calling directX::detour", result ? 1 : 0);
        utinni::log::info(msg);
    }

    directX::detour();

    if (firstFire)
    {
        utinni::log::info("hkInstall: directX::detour returned; EXIT");
    }

    return result;
}

// R-H snapshot dispatch helpers per D-12: copy registry values into a local
// vector before iteration so Subscribe-during-dispatch can't invalidate the
// iterator. Subscribers added mid-iteration land in the registry but fire on
// the NEXT dispatch.
//
// CR-01 (03-REVIEW): snapshot is built under the registry's mutex so concurrent
// Subscribe / Unsubscribe writes can't race the map's bucket structure during
// iteration. The mutex is dropped before invoking callbacks (callbacks can
// re-subscribe without deadlock; new subscribers fire on the next dispatch).
static void dispatchVoid(const std::unordered_map<int, void(*)()>& registry, std::mutex& mtx)
{
    std::vector<void(*)()> snapshot;
    {
        std::lock_guard<std::mutex> guard(mtx);
        snapshot.reserve(registry.size());
        for (const auto& kv : registry)
        {
            snapshot.push_back(kv.second);
        }
    }
    for (const auto& func : snapshot)
    {
        func();
    }
}

static void dispatchFloat(const std::unordered_map<int, void(*)(float)>& registry, std::mutex& mtx, float arg)
{
    std::vector<void(*)(float)> snapshot;
    {
        std::lock_guard<std::mutex> guard(mtx);
        snapshot.reserve(registry.size());
        for (const auto& kv : registry)
        {
            snapshot.push_back(kv.second);
        }
    }
    for (const auto& func : snapshot)
    {
        func(arg);
    }
}

static void dispatchPresentWindow(const std::unordered_map<int, void(*)(HWND, int, int)>& registry,
                                  std::mutex& mtx, HWND hwnd, int width, int height)
{
    std::vector<void(*)(HWND, int, int)> snapshot;
    {
        std::lock_guard<std::mutex> guard(mtx);
        snapshot.reserve(registry.size());
        for (const auto& kv : registry)
        {
            snapshot.push_back(kv.second);
        }
    }
    for (const auto& func : snapshot)
    {
        func(hwnd, width, height);
    }
}

void __cdecl hkUpdate(float elapsedTime)
{
    dispatchFloat(preUpdateCallback, preUpdateCallbackMutex, elapsedTime);

    swg::graphics::update(elapsedTime);

    dispatchFloat(postUpdateCallback, postUpdateCallbackMutex, elapsedTime);
}

void __cdecl hkBeginScene()
{
    dispatchVoid(preBeginSceneCallback, preBeginSceneCallbackMutex);

    swg::graphics::beginScene();

    dispatchVoid(postBeginSceneCallback, postBeginSceneCallbackMutex);
}

int oldWidth = 0;
int oldHeight = 0;
void __cdecl hkEndScene()
{
    dispatchVoid(preEndSceneCallback, preEndSceneCallbackMutex);

    swg::graphics::endScene();

    dispatchVoid(postEndSceneCallback, postEndSceneCallbackMutex);

    RECT rect;
    if (Client::getEditorMode() && GetWindowRect(Client::getHwnd(), &rect))
    {
        int newWidth = rect.right - rect.left;
        int newHeight = rect.bottom - rect.top;

        if (newWidth == 0 || newHeight == 0)
        {
            // ToDo Fix Present crash after WinForms sizes above 0 again
            return;
        }

        if (newWidth != oldWidth || newHeight != oldHeight)
        {
            oldWidth = newWidth;
            oldHeight = newHeight;
            swg::graphics::resize(newWidth, newHeight);
            swg::graphics::flushResources(false);
            CuiManager::setSize(newWidth, newHeight);
        }
    }
}

void __cdecl hkPresentWindow(HWND hwnd, int width, int height)
{
    dispatchPresentWindow(prePresentWindowCallback, prePresentWindowCallbackMutex, hwnd, width, height);

    swg::graphics::presentWindow(hwnd, width, height);

    dispatchPresentWindow(postPresentWindowCallback, postPresentWindowCallbackMutex, hwnd, width, height);
}

void __cdecl hkPresent()
{
    dispatchVoid(prePresentCallback, prePresentCallbackMutex);

    swg::graphics::present();

    dispatchVoid(postPresentCallback, postPresentCallbackMutex);
}

bool __cdecl hkScreenshot(const char* filename)
{
    std::string newFilename = screenshotsDir;

    const char* pos = strrchr(filename, '/');
    if (pos != nullptr)
    {
        newFilename += pos + 1;
    }
    else
    {
        newFilename += filename;
    }

    CreateDirectory((utility::getWorkingDirectory() + "/" + screenshotsDir).c_str(), nullptr);

    return swg::graphics::screenshot(newFilename.c_str());
}

void Graphics::detour()
{
    swg::graphics::install = (swg::graphics::pInstall)Detour::Create(swg::graphics::install, hkInstall, DETOUR_TYPE_PUSH_RET);

    swg::graphics::update = (swg::graphics::pUpdate)Detour::Create(swg::graphics::update, hkUpdate, DETOUR_TYPE_PUSH_RET);
    swg::graphics::beginScene = (swg::graphics::pBeginScene)Detour::Create(swg::graphics::beginScene, hkBeginScene, DETOUR_TYPE_JMP, 5);
    swg::graphics::endScene = (swg::graphics::pEndScene)Detour::Create(swg::graphics::endScene, hkEndScene, DETOUR_TYPE_JMP, 5);

    swg::graphics::presentWindow = (swg::graphics::pPresentWindow)Detour::Create(swg::graphics::presentWindow, hkPresentWindow, DETOUR_TYPE_JMP, 5);
    swg::graphics::present = (swg::graphics::pPresent)Detour::Create(swg::graphics::present, hkPresent, DETOUR_TYPE_JMP, 5);

    swg::graphics::screenshot = (swg::graphics::pScreenshot)Detour::Create(swg::graphics::screenshot, hkScreenshot, DETOUR_TYPE_PUSH_RET);
}

}

