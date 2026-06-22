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

#include "cui_hud.h"
#include "swg/endpoints.h"
#include "swg/ui/imgui_impl.h"
#include "swg/misc/swg_misc.h"
#include "swg/camera/camera.h"
#include "swg/scene/client_world.h"
#include "swg/scene/ground_scene.h"
#include "utility/log.h"

#include <cstdio>
#include <intrin.h>

namespace swg::cuiHud
{
using pUpdate = void(__thiscall*)(swgptr pThis, float time);
using pActionPerformAction = bool(__thiscall*)(swgptr pThis, DWORD val1, DWORD val2);
using pGetTarget = utinni::Object*(__cdecl*)(utinni::Camera * pCamera, math::Vector* worldStart, math::Vector* worldEnd, utinni::Object* obj);

pUpdate update = (pUpdate)0x00BD56F0;
pActionPerformAction actionPerformAction = (pActionPerformAction)0x00EDBAA0;
pGetTarget getTarget = (pGetTarget)0x00BD3E20;

} // namespace swg::cuiHud

// DIAG 2026-05-20 Issue #12 Phase A: SwgCuiGameMenu is the SWG system-menu
// mediator class (resolved by hunting for the class-name string
// "SwgCuiGameMenu" at VA 0x018BFD68, finding its sole push imm32
// reference at 0x00C7D386, then identifying the surrounding function as
// the mediator's C++ constructor -- standard MSVC pattern, pushes class
// name + arg, calls base class ctor at 0x009BFF00). Detour the ctor entry
// at 0x00C7D360 to log every construction. Three diagnostic outcomes:
//   - Fires at program startup AND on every Esc -> menu is being created
//     on-demand but failing to render (CUI rendering / layering bug).
//   - Fires only at startup -> mediator is a singleton; activation goes
//     through a separate Activate() method we'll need to find next.
//   - Never fires -> the factory itself isn't running this constructor;
//     deeper investigation (registry, factory init, etc.) needed.
namespace swg::cuiGameMenu
{
using pSwgCuiGameMenuCtor = swgptr(__thiscall*)(swgptr pThis, swgptr pPage);
pSwgCuiGameMenuCtor swgCuiGameMenuCtor = (pSwgCuiGameMenuCtor)0x00C7D360;
} // namespace swg::cuiGameMenu

namespace utinni::cuiHud
{
Object* targetUnderCursor;

void __fastcall hkUpdate(swgptr pThis, float time)
{
    swg::cuiHud::update(pThis, time);
}

bool __fastcall hkActionPerformAction(swgptr pThis, DWORD EDX, DWORD val1, DWORD val2)
{
    // DIAG 2026-05-20 Issue #11 Phase D (per CODEX consult): the hover-skip
    // is restored (was confirmed no-op in Phase C -- gizmoHover always 0).
    // Phase D logs *everything* CODEX asked for: pThis vtable ptr, first
    // 8 DWORDs of val1 + val2 (GameAction / ActionContext structs on the
    // caller's stack), return value, caller return address. Goal: identify
    // (a) what action code Esc dispatches, (b) why SWG's
    // actionPerformAction accepts it but produces no visible result.
    bool hover = imgui_gizmo::hasMouseHover();

    // Capture caller BEFORE doing anything that might shift the call stack.
    const void* callerPC = _ReturnAddress();

    // Pre-call snapshot: vtable + struct contents
    DWORD vtbl = (pThis != 0) ? memory::read<DWORD>(pThis) : 0;
    DWORD v1[8] = {0};
    DWORD v2[8] = {0};
    if (val1 != 0)
    {
        for (int i = 0; i < 8; ++i)
            v1[i] = memory::read<DWORD>(val1 + (DWORD)(i * 4));
    }
    if (val2 != 0)
    {
        for (int i = 0; i < 8; ++i)
            v2[i] = memory::read<DWORD>(val2 + (DWORD)(i * 4));
    }

    // Hover-skip path: log + return false without calling trampoline.
    static int s_logCount = 0;
    if (hover)
    {
        if (s_logCount < 20)
        {
            ++s_logCount;
            char m[512];
            snprintf(m, sizeof(m),
                     "hkActionPerformAction[%d]: SKIPPED (gizmoHover=1) pThis=0x%p vtbl=0x%08X caller=0x%p\n"
                     "  val1=0x%08X [%08X %08X %08X %08X %08X %08X %08X %08X]\n"
                     "  val2=0x%08X [%08X %08X %08X %08X %08X %08X %08X %08X]",
                     s_logCount, (void*)pThis, vtbl, callerPC,
                     (unsigned)val1, v1[0], v1[1], v1[2], v1[3], v1[4], v1[5], v1[6], v1[7],
                     (unsigned)val2, v2[0], v2[1], v2[2], v2[3], v2[4], v2[5], v2[6], v2[7]);
            utinni::log::info(m);
        }
        return false;
    }

    // Passthrough path: call trampoline, log with return value.
    bool ret = swg::cuiHud::actionPerformAction(pThis, val1, val2);
    if (s_logCount < 20)
    {
        ++s_logCount;
        char m[512];
        snprintf(m, sizeof(m),
                 "hkActionPerformAction[%d]: PASS pThis=0x%p vtbl=0x%08X ret=%d caller=0x%p\n"
                 "  val1=0x%08X [%08X %08X %08X %08X %08X %08X %08X %08X]\n"
                 "  val2=0x%08X [%08X %08X %08X %08X %08X %08X %08X %08X]",
                 s_logCount, (void*)pThis, vtbl, ret ? 1 : 0, callerPC,
                 (unsigned)val1, v1[0], v1[1], v1[2], v1[3], v1[4], v1[5], v1[6], v1[7],
                 (unsigned)val2, v2[0], v2[1], v2[2], v2[3], v2[4], v2[5], v2[6], v2[7]);
        utinni::log::info(m);

        // DIAG 2026-05-20 Issue #12 Phase B: dump the strings pointed to by
        // val1[0] (8-char range) and val1[3] (16-char range). Phase A
        // pattern analysis: val1 layout for in-game Esc is two CharPair
        // strings -- looks like (group, action) e.g. ("gameMenu",
        // "gameMenuActivate"). Confirming the strings tells us exactly
        // which action SWG is trying to dispatch and lets us find the
        // (currently broken) activation handler.
        char str_a[40] = "<null>";
        char str_b[40] = "<null>";
        if (v1[0] != 0)
        {
            int len = (int)(v1[1] - v1[0]);
            if (len < 1)
                len = 1;
            if (len > 32)
                len = 32;
            for (int j = 0; j < len; ++j)
            {
                __try
                {
                    str_a[j] = (char)memory::read<byte>((swgptr)(v1[0] + (DWORD)j));
                }
                __except (EXCEPTION_EXECUTE_HANDLER)
                {
                    str_a[j] = '?';
                    break;
                }
                if (str_a[j] == 0)
                    break;
                if (str_a[j] < 0x20 || str_a[j] >= 0x7F)
                    str_a[j] = '.';
            }
            str_a[len] = 0;
        }
        if (v1[3] != 0)
        {
            int len = (int)(v1[4] - v1[3]);
            if (len < 1)
                len = 1;
            if (len > 32)
                len = 32;
            for (int j = 0; j < len; ++j)
            {
                __try
                {
                    str_b[j] = (char)memory::read<byte>((swgptr)(v1[3] + (DWORD)j));
                }
                __except (EXCEPTION_EXECUTE_HANDLER)
                {
                    str_b[j] = '?';
                    break;
                }
                if (str_b[j] == 0)
                    break;
                if (str_b[j] < 0x20 || str_b[j] >= 0x7F)
                    str_b[j] = '.';
            }
            str_b[len] = 0;
        }
        char m2[256];
        snprintf(m2, sizeof(m2),
                 "  ^ str_a (val1[0..1]) = '%s'   str_b (val1[3..4]) = '%s'",
                 str_a, str_b);
        utinni::log::info(m2);
    }
    return ret;
}

bool collideCursorWithWorld(int x, int y, swg::math::Vector& result, Object* excludeObject)
{
    Camera* camera = GroundScene::get()->getCurrentCamera();
    swg::math::Vector worldStart = camera->getTransform_o2w()->getPosition();
    swg::math::Vector viewDirection = camera->getTransform_o2w()->rotate_l2p(camera->reverseProjectInViewportSpace(x - camera->viewportX, y - camera->viewportY));
    viewDirection.normalize();

    const float viewDistance = memory::read<float>(0x19488C8); // 0x19488C8 = Static location of view distance
    swg::math::Vector worldEnd = worldStart + viewDirection * viewDistance;

    static constexpr uint16_t flags = (1 | 128 | 4096); // terrain | child objects | disable portal crossing
    CollisionInfo collisionResults;
    if (clientWorld::collide(camera->getParentCell(), &worldStart, &worldEnd, collisionResults, flags, excludeObject))
    {
        result = swg::math::Vector(collisionResults.point);
        return true;
    }
    return false;
}

static swg::math::Vector ws;
static swg::math::Vector we;

static swg::math::Vector cursorWorldPos;
Object* __cdecl hkGetTarget(Camera* pCamera, swg::math::Vector* worldStart, swg::math::Vector* worldEnd, Object* obj)
{
    ws = swg::math::Vector(*worldStart);
    we = swg::math::Vector(*worldEnd);

    static constexpr uint16_t flags = 511; // (1 | 128 | 4096); // terrain | child objects | disable portal crossing
    CollisionInfo collisionResults;
    if (clientWorld::collide(pCamera->getParentCell(), worldStart, worldEnd, collisionResults, flags, obj))
    {
        cursorWorldPos = swg::math::Vector(collisionResults.point);

        // log::info(collisionResults.object->getAppearanceFilename());
    }

    return swg::cuiHud::getTarget(pCamera, worldStart, worldEnd, obj);
}

static constexpr swgptr jmpToTarget_midUpdate = 0x00BD5961;
static constexpr swgptr return_midUpdate = 0x00BD595C;
__declspec(naked) void midUpdate()
{
    swgptr pTargetUnderCursor;
    __asm
        {
        mov pTargetUnderCursor, ecx
        pushad
        pushfd
        }

    targetUnderCursor = (Object*)pTargetUnderCursor;

    __asm
    {
        popfd
        popad
        test eax, eax
        jz jumpToTarget
        mov targetUnderCursor, 0
        push edi
        lea ecx, [0x84 + eax]
        jmp[return_midUpdate]

        jumpToTarget:
        jmp[jmpToTarget_midUpdate]
    }
}

const swg::math::Vector& getCursorWorldPosition()
{
    return cursorWorldPos;
}

void patchAllowTargetEverything(bool value)
{
    if (value)
    {
        memory::createJMP(0x00BD3FA3, 0x00BD403D, 5);
    }
    else
    {
        static constexpr byte originalBytes[] = {0x8B, 0xCE, 0xE8, 0x76, 0xF9}; // call client.9C18A0
        memory::copy(0x00BD3FA3, originalBytes);
    }
}

swg::math::Vector* getWs()
{
    return &ws;
}

swg::math::Vector* getWe()
{
    return &we;
}

// DIAG 2026-05-20 Issue #12 Phase A: log every SwgCuiGameMenu ctor call
// + return-address. Returns swgptr (the constructed `this`) so the caller
// downstream still gets the right value.
swgptr __fastcall hkSwgCuiGameMenuCtor(swgptr pThis, swgptr EDX, swgptr pPage)
{
    static int s_count = 0;
    if (s_count < 20)
    {
        ++s_count;
        const void* callerPC = _ReturnAddress();
        char m[200];
        snprintf(m, sizeof(m),
                 "hkSwgCuiGameMenuCtor[%d]: SwgCuiGameMenu CONSTRUCTED pThis=0x%p pPage=0x%p caller=0x%p",
                 s_count, (void*)pThis, (void*)pPage, callerPC);
        utinni::log::info(m);
    }
    return swg::cuiGameMenu::swgCuiGameMenuCtor(pThis, pPage);
}

void detour()
{
    // Phase 24: skip on the advertised client when the primary target is unresolved.
    if (!swg::endpoints::installable((const void*)swg::cuiHud::actionPerformAction))
        return;

    // swg::cuiHud::update = (swg::cuiHud::pUpdate)Detour::Create((LPVOID)swg::cuiHud::update, hkUpdate, DETOUR_TYPE_PUSH_RET);
    swg::cuiHud::actionPerformAction = (swg::cuiHud::pActionPerformAction)Detour::Create((LPVOID)swg::cuiHud::actionPerformAction, hkActionPerformAction, DETOUR_TYPE_PUSH_RET);
    swg::cuiHud::getTarget = (swg::cuiHud::pGetTarget)Detour::Create((LPVOID)swg::cuiHud::getTarget, hkGetTarget, DETOUR_TYPE_PUSH_RET);

    // DIAG Issue #12 Phase A: SwgCuiGameMenu ctor detour
    swg::cuiGameMenu::swgCuiGameMenuCtor = (swg::cuiGameMenu::pSwgCuiGameMenuCtor)Detour::Create((LPVOID)swg::cuiGameMenu::swgCuiGameMenuCtor, hkSwgCuiGameMenuCtor, DETOUR_TYPE_PUSH_RET);

    // memory::createJMP(0x00BD5951, (swgptr)midUpdate, 6); // Mid CuiHud::update detour
}

} // namespace utinni::cuiHud
