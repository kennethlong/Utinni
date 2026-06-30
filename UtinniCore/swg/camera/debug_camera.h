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
#include "swg/misc/io_win.h"

namespace utinni
{
class GroundScene; // v13 (free-cam): processIoEvent now takes the live GroundScene (no GroundScene::get())
class Camera;      // v13 (free-cam): ensureAdvertisedAlterDetour resolves alter off the live camera
} // namespace utinni

namespace utinni::debugCamera
{
enum Commands
{
    cmd_up = 3332,
    cmd_down,
    cmd_forward,
    cmd_backward,
    cmd_right,
    cmd_left,
    cmd_speedBoost,
    cmd_mouseMovePosition,

};

UTINNI_API extern void setSpeed(float value);
UTINNI_API extern float getSpeed();
UTINNI_API extern void setFastSpeedMulti(float value);
UTINNI_API extern void setMouseWheelSpeed(float value);
UTINNI_API extern void enableDragPlayer(bool value);

void processIoEvent(GroundScene* groundScene, IoEvent* ioEvent);

// v13 (free-cam): the alter detour can't install at startup on the advertised client (no camera exists +
// the SWGEmu RVA is unmapped). Call this once free-cam is toggled on (DebugPortalCamera is now current) to
// vtable-resolve its alter (Object slot 4) and install the detour. One-shot; no-op on repeat / null camera.
void ensureAdvertisedAlterDetour(Camera* currentCamera);

void detour();
void patch();
}; // namespace utinni::debugCamera
