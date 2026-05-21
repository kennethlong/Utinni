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

#pragma once

#include "utinni.h"
#include "swg/misc/repository.h"

namespace utinni
{
class Camera;
class Object;

class UTINNI_API Game
{
public:
    // Phase 3 R-A primary API (per 03-CONTEXT D-08/D-09): handle-based
    // Subscribe returns an opaque int (handle 0 reserved as invalid sentinel);
    // pair with Unsubscribe(handle). Add* retained per D-10 as thin wrappers
    // for source-compat with existing UtinniPlugins (TJT, Sytner).
    static int subscribeInstallCallback(void(*func)());
    static bool unsubscribeInstallCallback(int handle);
    static int subscribePreMainLoopCallback(void(*func)());
    static bool unsubscribePreMainLoopCallback(int handle);
    static int subscribeMainLoopCallback(void(*func)());
    static bool unsubscribeMainLoopCallback(int handle);
    static int subscribeSetSceneCallback(void(*func)());
    static bool unsubscribeSetSceneCallback(int handle);
    static int subscribeCleanupSceneCallback(void(*func)());
    static bool unsubscribeCleanupSceneCallback(int handle);

    // Legacy add* API (D-10): thin wrappers around subscribe*. Return value
    // discarded — existing UtinniPlugins keep working without recompile.
    static void addInstallCallback(void(*func)());
    static void addPreMainLoopCallback(void(*func)());
    static void addMainLoopCallback(void(*func)());
    static void addSetSceneCallback(void(*func)());
    static void addCleanupSceneCallback(void(*func)());

    static void detour();
    static void quit();

    static bool isRunning();

    static void loadScene();
    static void loadScene(const char* terrainFilename, const char* avatarObjectFilename = "object/creature/player/shared_human_male.iff");
    static void cleanupScene();

    static Repository* getRepository();

    static Object* getPlayer();
    static Object* getPlayerCreatureObject();
    static swgptr getPlayerLookAtTargetObjectNetworkId();
    static Object* getPlayerLookAtTargetObject();

    static Camera* getCamera();
    static const Camera* getConstCamera();

    static bool isSafeToUse();

    // Test-only: fires all registered install callbacks (used by utinni_triggerInstallCallbacks).
    static void triggerInstallCallbacks();

    // Phase 3 R-A test-only: registry size accessor for the utinni_test_*
    // bridge in test_exports.cpp. Not for production consumption.
    static int getInstallSubscriberCount();

};
}
