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

#include "cui_radial_menu.h"
#include "swg/endpoints.h"
#include "swg/ui/imgui_impl.h"

namespace swg::cuiRadialMenuManager
{
using pUpdate = void(__cdecl*)();
using pClear = void(__cdecl*)();

pUpdate update = (pUpdate)0x009698C0;
pClear clear = (pClear)0x0096C550;

} // namespace swg::cuiRadialMenuManager

namespace utinni
{
void __cdecl hkUpdate()
{
    // cuiRadialMenuManager::clear is a hardcoded SWGEmu RVA (0x0096C550) -- NOT an advertised row,
    // so on the advertised client it stays garbage. It was unreachable there until the Wave-3 gizmo
    // unlock made isEnabled() true on advertised (cui_radial_menu latent bug, cdb-confirmed
    // 2026-07-18: enabling the gizmo called clear() -> jump to the stale RVA -> crash). Skip it on
    // advertised -- the radial-menu clear-on-gizmo is a nicety, not load-bearing; degrade to
    // not-cleared rather than crash. (A provider `clear` row would restore the behavior; follow-up.)
    if (imgui_gizmo::isEnabled() && !swg::endpoints::isAdvertisedClient())
    {
        swg::cuiRadialMenuManager::clear();
    }

    swg::cuiRadialMenuManager::update();
}

void cuiRadialMenuManager::detour()
{
    // Phase 24: skip on the advertised client when the primary target is unresolved.
    if (!swg::endpoints::installable((const void*)swg::cuiRadialMenuManager::update))
        return;

    swg::cuiRadialMenuManager::update = (swg::cuiRadialMenuManager::pUpdate)Detour::Create((LPVOID)swg::cuiRadialMenuManager::update, hkUpdate, DETOUR_TYPE_PUSH_RET);
}

} // namespace utinni
