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

// Phase 24 follow-on (Item 2): consumer-side resolution of VIRTUAL engine methods off
// the live object vtable. The advertised client (SwgClient_*.exe) does NOT advertise
// virtuals -- a member-pointer to a virtual encodes a vtable dispatch, not a stable
// code address, so the provider correctly skips them. Instead the consumer reads the
// real address out of a live instance's vtable at a known INDEX. The index is
// build-INDEPENDENT (same engine source => same vtable layout); only the addresses
// differ between SWGEmu and the from-source client, so a single hardcoded index works
// on both, exactly like directx9.cpp hardcodes the D3D9 device vtable indices
// (d3di_Present_Index = 17, ...).
//
// INDEX DERIVATION (MSVC x86, single inheritance, virtual-dtor-declared-first => slot 0;
// an override reuses its base slot). Derived from the swg-client-v2 source's virtual
// declaration order and cross-validated by a second model (Codex, 2026-06-22):
//
//   IoWin (no base):  ~IoWin(0)  processEvent(1)  draw(2)
//     => CuiIoWin::processEvent (override) = index 1
//
//   Object (no base): ~Object(0) isInitialized(1) addToWorld(2) removeFromWorld(3)
//     alter(4) conclude(5) kill(6) setRegionOfInfluenceEnabled(7)
//     getContainedByProperty(8) getContainedByProperty-const(9) containedByModified(10)
//     arrangementModified(11) setActive(12) setParentCell(13)
//     => addToWorld=2, removeFromWorld=3, setParentCell=13
//
// These are EMPIRICALLY self-checked on SWGEmu (where the original RVA is known-good):
// vtbl::slot(liveInstance, index) MUST equal that known RVA, else the index is wrong --
// caught at the next SWGEmu smoke instead of by a crash on the advertised client. An
// inline detour does NOT move the vtable slot (it patches the function prologue), so the
// slot still holds the ORIGINAL address post-detour -- compare against the captured
// original RVA, not the (now-trampoline) function-pointer literal.
//
// LEAN HEADER: depends on nothing (no Windows/DX types); included only from .cpp files,
// so it is never seen by the CppSharp parser -- zero managed-ABI surface.

namespace swg::vtbl
{
// CuiIoWin::processEvent -- virtual on the IoWin base (overridden by CuiIoWin).
inline constexpr int kCuiIoProcessEvent = 1;

// Object virtuals.
inline constexpr int kObjectAddToWorld = 2;
inline constexpr int kObjectRemoveFromWorld = 3;
inline constexpr int kObjectSetParentCell = 13;

// Read vtable slot `index` off a live polymorphic instance. The instance's first
// pointer-sized word is its vtable pointer; the vtable is an array of code addresses.
// Null-safe (a null instance or null vtable pointer returns nullptr). Pure -- no
// allocation, no side effects -- so it is unit-testable against a synthetic vtable.
inline const void* slot(const void* instance, int index)
{
    if (instance == nullptr)
    {
        return nullptr;
    }
    const void* const* vtable = *reinterpret_cast<const void* const* const*>(instance);
    if (vtable == nullptr)
    {
        return nullptr;
    }
    return vtable[index];
}
} // namespace swg::vtbl
