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

// Phase 19 / RNDR-03 (D-15/D-16/D-17): the render-backend selection DECISION,
// factored out as a PURE function so it is unit-testable WITHOUT real DLLs and
// so imgui_impl.cpp may include it WITHOUT tripping the D-06 API-neutrality gate.
//
// This header deliberately names ZERO concrete DX11/DXGI types (no DX11/DXGI
// SDK includes and no D3D-device/swapchain symbols) -- so imgui_impl.cpp may
// include it without tripping the D-06 grep gate. It only models the two
// booleans the caller resolves at install time:
//   * gl11Present          -- a gl%02d_r.dll module-presence check (GetModuleHandle)
//   * getHookPointsResolved -- the advertised GetHookPoints() contract resolved AND
//                              returned a usable (non-null) swapchain (spec 3.x)
//
// The decision is intentionally conservative: pick D3D11 ONLY when BOTH signals
// are positive; otherwise default to D3D9. Ambiguous/partial states (gl11 present
// but the contract is absent or not-yet-ready) fall back to D3D9 -- the caller
// emits the one-shot fallback log (RNDR-03 diagnostic) on that branch.

namespace render_backend
{
enum class BackendChoice
{
    Dx9,
    Dx11
};

// Pure decision: Dx11 iff (gl11Present && getHookPointsResolved), else Dx9.
// No side effects, no DLL access -- the caller injects the resolved booleans.
inline BackendChoice selectBackend(bool gl11Present, bool getHookPointsResolved)
{
    if (gl11Present && getHookPointsResolved)
    {
        return BackendChoice::Dx11;
    }
    return BackendChoice::Dx9;
}
} // namespace render_backend
