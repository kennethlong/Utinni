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

// ============================================================================
// Phase 19 / RNDR-03 / D-21 automated layer 4: the gl%02d_r.dll backend-selection
// DECISION unit test. selectBackend() is a PURE function (backend_select.h) taking
// the two install-time booleans, so the four detection states are exercised by
// injecting the booleans directly -- no real DLLs, no GetModuleHandle, runs in CI.
//
// Decision contract (D-15/D-16/D-17): pick D3D11 ONLY when gl11 is present AND the
// advertised GetHookPoints() contract resolved a usable swapchain; every other
// state (default, ambiguous, contract-absent) falls back to D3D9.
// ============================================================================

#include <catch2/catch_all.hpp>

#include "swg/graphics/backend_select.h"

using render_backend::BackendChoice;
using render_backend::selectBackend;

TEST_CASE("RNDR-03 backend selection across the four gl11/contract states", "[rndr03][detect]")
{
    SECTION("default: no gl11 module present -> D3D9 (number->API mapping, D-16)")
    {
        // gl11 absent => DX9 regardless of the (unread) contract bit.
        REQUIRE(selectBackend(/*gl11Present=*/false, /*getHookPointsResolved=*/false) == BackendChoice::Dx9);
        REQUIRE(selectBackend(/*gl11Present=*/false, /*getHookPointsResolved=*/true) == BackendChoice::Dx9);
    }

    SECTION("positive: gl11 present AND GetHookPoints resolved -> D3D11")
    {
        REQUIRE(selectBackend(/*gl11Present=*/true, /*getHookPointsResolved=*/true) == BackendChoice::Dx11);
    }

    SECTION("ambiguous: gl11 present but GetHookPoints absent/not-ready -> D3D9 + fallback log")
    {
        // gl11 detected but the advertised contract is missing or the swapchain is
        // not yet live -> conservative D3D9 fallback (the caller logs the one-shot
        // RNDR-03 fallback diagnostic on this branch).
        REQUIRE(selectBackend(/*gl11Present=*/true, /*getHookPointsResolved=*/false) == BackendChoice::Dx9);
    }

    SECTION("only D3D11 is ever chosen when BOTH signals are positive")
    {
        // Exhaustive truth table: exactly one of the four states yields Dx11.
        int dx11Count = 0;
        for (bool gl11 : {false, true})
        {
            for (bool resolved : {false, true})
            {
                if (selectBackend(gl11, resolved) == BackendChoice::Dx11)
                {
                    ++dx11Count;
                    REQUIRE(gl11);
                    REQUIRE(resolved);
                }
            }
        }
        REQUIRE(dx11Count == 1);
    }
}
