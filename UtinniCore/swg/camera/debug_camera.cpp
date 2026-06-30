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

#include "debug_camera.h"
#include "swg/endpoints.h"
#include "camera.h"
#include "swg/scene/ground_scene.h"
#include "swg/object/player_object.h"
#include "swg/game/game.h"
#include "swg/vtbl_resolve.h"
#include "utility/log.h"

#include <atomic>
#include <cstdio>

namespace swg::debugCamera
{
using pAlter = float(__thiscall*)(utinni::GameCamera* pThis, float time);

pAlter alter = (pAlter)0x006DA1B0;
} // namespace swg::debugCamera

// v13 (free-cam): the advertised accessor slots (defined + bound in ground_scene.cpp / camera.cpp /
// endpoints_bindings.cpp). Referenced here to replace the fragile struct-offset reads on the advertised
// client. null on SWGEmu -> the code falls back to the existing offset read (D-00 byte-behavior preserved).
namespace swg::groundScene
{
using pGetDebugPortalCameraMessageQueue = utinni::MessageQueue*(__thiscall*)(utinni::GroundScene * pThis);
extern pGetDebugPortalCameraMessageQueue getDebugPortalCameraMessageQueue;
} // namespace swg::groundScene

namespace swg::gameCamera
{
using pGetMessageQueue = utinni::MessageQueue*(__thiscall*)(utinni::GameCamera * pThis);
extern pGetMessageQueue getMessageQueue;
} // namespace swg::gameCamera

static const swg::math::Vector dirX(1, 0, 0);
static const swg::math::Vector dirY(0, 1, 0);
static const swg::math::Vector dirZ(0, 0, 1);

static const swg::math::Vector dirNegX(-1, 0, 0);
static const swg::math::Vector dirNegY(0, -1, 0);
static const swg::math::Vector dirNegZ(0, 0, -1);

namespace utinni::debugCamera
{

// ToDo make these settings
static float normalSpeed = 7;
static float fastSpeedMulti = 5;
static float wheelSpeed = 120;

static bool dragPlayer;

void setSpeed(float value)
{
    normalSpeed = value;
}

float getSpeed()
{
    return normalSpeed;
}

void setFastSpeedMulti(float value)
{
    fastSpeedMulti = value;
}

void setMouseWheelSpeed(float value)
{
    wheelSpeed = value;
}

void enableDragPlayer(bool value)
{
    dragPlayer = value;
}

void processIoEvent(GroundScene* groundScene, IoEvent* ioEvent)
{
    // ADVERTISED (NGE): do NOTHING here. The 4-AI consult (2026-06-30) confirmed the engine routes IOET_KeyDown
    // to its own debug-portal input map (CM_walk/...) and DebugPortalCamera::alter flies the camera natively
    // once CuiIoWin keyboard capture is released (GroundScene::toggleFreeCamera does that). Our SWGEmu cmd_*
    // injection here would be ignored by native alter (different message IDs) AND double-counted by hkAlter.
    if (swg::endpoints::isAdvertisedClient())
    {
        return;
    }

    // groundScene is now passed in from hkHandleInputEvent (the live `this`) instead of GroundScene::get(),
    // which returns nullptr on the advertised client by design. On advertised, source the input MessageQueue
    // via the advertised accessor (replaces the debugPortalCameraInputMap+0xC struct-offset read); on SWGEmu
    // the accessor slot is null -> fall back to the offset (byte-behavior unchanged, D-00).
    if (groundScene == nullptr)
    {
        return;
    }

    MessageQueue* messageQueue;
    if (swg::groundScene::getDebugPortalCameraMessageQueue != nullptr)
    {
        messageQueue = swg::groundScene::getDebugPortalCameraMessageQueue(groundScene);
    }
    else if (swg::endpoints::isAdvertisedClient())
    {
        // FAIL-CLOSED (4-AI pre-smoke review): the accessor is null on advertised ONLY if its v13 row failed
        // to resolve -- do NOT fall back to the debugPortalCameraInputMap+0xC SWGEmu offset on an NGE layout
        // (the exact AV this change exists to avoid). Bail; the init resolver log flags the missed row.
        return;
    }
    else
    {
        messageQueue = memory::read<MessageQueue*>((swgptr)groundScene->debugPortalCameraInputMap + 0xC); // SWGEmu
    }
    if (messageQueue == nullptr)
    {
        return;
    }

    switch (ioEvent->type)
    {
    case IoEvent::Type::t_Character:
        break;

    case IoEvent::Type::t_KeyDown:
        switch (ioEvent->arg2)
        {
        case IoEvent::Keys::kc_W:
            messageQueue->appendMessage(cmd_forward, 1);
            break;

        case IoEvent::Keys::kc_S:
            messageQueue->appendMessage(cmd_backward, 1);
            break;

        case IoEvent::Keys::kc_A:
            messageQueue->appendMessage(cmd_left, 1);
            break;

        case IoEvent::Keys::kc_D:
            messageQueue->appendMessage(cmd_right, 1);
            break;

        case IoEvent::Keys::kc_Space:
            messageQueue->appendMessage(cmd_up, 1);
            break;

        case IoEvent::Keys::kc_C:
            messageQueue->appendMessage(cmd_down, 1);
            break;

        case IoEvent::Keys::kc_LShift:
            messageQueue->appendMessage(cmd_speedBoost, 1);
            break;
        }
        break;

    case IoEvent::Type::t_KeyUp:
        switch (ioEvent->arg2)
        {
        case IoEvent::Keys::kc_W:
            messageQueue->appendMessage(cmd_forward, 0);
            break;

        case IoEvent::Keys::kc_S:
            messageQueue->appendMessage(cmd_backward, 0);
            break;

        case IoEvent::Keys::kc_A:
            messageQueue->appendMessage(cmd_left, 0);
            break;

        case IoEvent::Keys::kc_D:
            messageQueue->appendMessage(cmd_right, 0);
            break;

        case IoEvent::Keys::kc_Space:
            messageQueue->appendMessage(cmd_up, 0);
            break;

        case IoEvent::Keys::kc_C:
            messageQueue->appendMessage(cmd_down, 0);
            break;

        case IoEvent::Keys::kc_LShift:
            messageQueue->appendMessage(cmd_speedBoost, 0);
            break;
        }
        break;

    case IoEvent::Type::t_MouseButtonDown:
        break;

    case IoEvent::Type::t_MouseButtonUp:
        break;
    }
}

static bool forward;
static bool backward;
static bool right;
static bool left;
static bool up;
static bool down;
static bool speedBoost;

float __fastcall hkAlter(GameCamera* pThis, swgptr EDX, float time)
{
    if (!pThis->isActive())
    {
        return 0;
    }

    float result = 0;
    result = swg::debugCamera::alter(pThis, time);

    float speedMulti;
    float wheelSpeedMulti;
    if (speedBoost)
    {
        speedMulti = time * (normalSpeed * fastSpeedMulti);
        wheelSpeedMulti = time * (wheelSpeed * fastSpeedMulti);
    }
    else
    {
        speedMulti = time * normalSpeed;
        wheelSpeedMulti = time * wheelSpeed;
    }

    swg::math::Vector direction;

    // v13 (free-cam): source the movement MessageQueue via the advertised accessor (replaces the camera+0x248
    // struct-offset read). null on SWGEmu -> the offset read (D-00). getCount/getMessage below go through the
    // utinni::MessageQueue wrappers, whose swg pointers are also re-pointed on advertised (v13 bound rows).
    MessageQueue* messageQueue;
    if (swg::gameCamera::getMessageQueue != nullptr)
    {
        messageQueue = swg::gameCamera::getMessageQueue(pThis);
    }
    else if (swg::endpoints::isAdvertisedClient())
    {
        return 0; // FAIL-CLOSED: never read the camera+0x248 SWGEmu offset on the advertised client
    }
    else
    {
        messageQueue = memory::read<MessageQueue*>((swgptr)pThis + 0x248); // SWGEmu
    }
    if (messageQueue == nullptr)
    {
        return 0;
    }

    for (int i = 0; i < messageQueue->getCount(); ++i)
    {
        int msg;
        float value;
        messageQueue->getMessage(i, &msg, &value);

        switch (msg)
        {
        case 167: // cui mouseWheel
            if (value > 0)
            {
                direction = direction + (dirZ * wheelSpeedMulti);
            }
            else
            {
                direction = direction + (dirNegZ * wheelSpeedMulti);
            }
            break;

        case cmd_forward:
            forward = value == 1;
            break;

        case cmd_backward:
            backward = value == 1;
            break;

        case cmd_right:
            right = value == 1;
            break;

        case cmd_left:
            left = value == 1;
            break;

        case cmd_up:
            up = value == 1;
            break;

        case cmd_down:
            down = value == 1;
            break;

        case cmd_speedBoost:
            speedBoost = value == 1;
            break;
        }
    }

    if (forward)
    {
        direction = direction + (dirZ * speedMulti);
    }

    if (backward)
    {
        direction = direction + (dirNegZ * speedMulti);
    }

    if (right)
    {
        direction = direction + (dirX * speedMulti);
    }

    if (left)
    {
        direction = direction + (dirNegX * speedMulti);
    }

    if (up)
    {
        direction = direction + (dirY * speedMulti);
    }

    if (down)
    {
        direction = direction + (dirNegY * speedMulti);
    }

    // v13 (free-cam): rotate the move vector by the camera transform. On advertised use the advertised
    // getTransform_o2w() (replaces the objectToParent struct-field read); on SWGEmu keep objectToParent
    // (D-00). For a free-flying (unparented) camera o2p == o2w, so this is behavior-identical there.
    swg::math::Transform* cameraTransform =
        swg::endpoints::isAdvertisedClient() ? pThis->getTransform_o2w() : &pThis->objectToParent;
    if (cameraTransform != nullptr) // null guard (pre-smoke review): getTransform_o2w should be live, but bail safe
    {
        pThis->move(cameraTransform->rotate_l2p(direction));
    }

    if (Game::isSafeToUse() && dragPlayer && (direction.X != 0 || direction.Y != 0 || direction.Z != 0))
    {
        const auto pos = pThis->getTransform()->getPosition();
        playerObject::teleport(pos.X, 0, pos.Z);
    }

    return result;
}

// The alter override sits at Object virtual slot 4 (provider-confirmed; cross-validated by the SWGEmu
// runtime self-check in GroundScene::toggleFreeCamera, which asserts slot 4 == the known RVA).
static_assert(swg::vtbl::kObjectAlter == 4, "alter must be Object virtual slot 4 (DebugPortalCamera override)");

void ensureAdvertisedAlterDetour(Camera* currentCamera)
{
    // One-shot: vtable-resolve DebugPortalCamera::alter (Object slot 4) off the live camera and detour it.
    // The patch is on the function code, not the instance, so it persists across scene changes / re-toggles.
    // Called from GroundScene::toggleFreeCamera on the advertised client once changeCamera(cm_Free) has made
    // the DebugPortalCamera current (it can't install at startup -- no camera exists + the SWGEmu RVA is
    // unmapped). Game-thread only.
    //
    // Pre-smoke review hardening: validate the resolved entry is committed-executable (installable) and that
    // Detour::Create actually returns a trampoline BEFORE claiming s_installed -- so a failed/partial install
    // RETRIES on the next toggle instead of leaving swg::debugCamera::alter null (which hkAlter would call).
    static std::atomic<bool> s_installed{false};
    if (currentCamera == nullptr || s_installed.load(std::memory_order_acquire))
    {
        return;
    }

    const void* alterEntry = swg::vtbl::slot(currentCamera, swg::vtbl::kObjectAlter);
    if (alterEntry == nullptr || !swg::endpoints::installable(alterEntry))
    {
        // no live vtable yet, or the resolved slot isn't committed-executable code -> log + retry next toggle.
        char m[160];
        std::snprintf(m, sizeof(m), "debugCamera: alter slot4=0x%p not installable yet -> deferring (retry next toggle)", alterEntry);
        log::info(m);
        return;
    }

    void* trampoline = Detour::Create((LPVOID)alterEntry, hkAlter, DETOUR_TYPE_PUSH_RET);
    if (trampoline == nullptr)
    {
        log::info("debugCamera: alter Detour::Create returned null -> NOT installed (retry next toggle)");
        return; // do NOT claim s_installed -> retry
    }

    swg::debugCamera::alter = (swg::debugCamera::pAlter)trampoline; // hkAlter calls the original through this
    s_installed.store(true, std::memory_order_release);

    char m[128];
    std::snprintf(m, sizeof(m), "debugCamera: advertised alter vtable-resolve slot4=0x%p -> detour installed", alterEntry);
    log::info(m);
}

void detour()
{
    // Phase 24: skip on the advertised client when the primary target is unresolved. The SWGEmu RVA
    // (0x006DA1B0) is mapped+installable on SWGEmu -> alter detours here at startup (D-00). On the advertised
    // client the RVA is unmapped -> installable() is false -> skip; the detour installs lazily on first
    // free-cam toggle via ensureAdvertisedAlterDetour() (vtable-resolved, needs a live camera).
    if (!swg::endpoints::installable((const void*)swg::debugCamera::alter))
        return;

    swg::debugCamera::alter = (swg::debugCamera::pAlter)Detour::Create((LPVOID)swg::debugCamera::alter, hkAlter, DETOUR_TYPE_PUSH_RET);
}

void patch()
{
    // Phase 24: skip on the advertised client when this SWGEmu patch site is unmapped.
    if (!swg::endpoints::installable((const void*)0x0051AA8D))
        return;

    // Enable mouse wheel to be sent to debugCamera::alter
    memory::nopAddress(0x0051AA8D, 2);
}
}; // namespace utinni::debugCamera
