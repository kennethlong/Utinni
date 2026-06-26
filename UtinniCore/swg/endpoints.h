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

// Phase 24 / EPA-02, EPA-04: the swg::endpoints resolver -- the CONSUMER half of
// the GetEngineHookPoints() advertisement contract (the exe-side game-logic twin
// of the gl11_r.dll!GetHookPoints graphics consumer in directx11.cpp).
//
// At utinni_init time (the launcher remote thread, NOT DllMain -- CON-H-01),
// resolveFromExe() probes GetProcAddress(GetModuleHandleA(NULL),
// "GetEngineHookPoints"). On the advertised client (SwgClient_*.exe) it overwrites
// the per-subsystem swg::* function-pointer literals BY NAME before createDetours()
// runs, fixing the first-detour 0xC0000005 READ crash. On SWGEmu Pre-CU (export
// absent) it is a STRICT NO-OP -- the hardcoded-RVA path stays byte-for-byte
// unchanged (D-00).
//
// LEAN-HEADER CONTRACT (mirror backend_select.h): this header transitively
// includes ONLY engine_hookpoints.h + <cstddef>. It names ZERO
// DX/DXGI/injection/Windows types so the test project can compile endpoints.cpp
// standalone (the pure resolve() is unit-testable WITHOUT injection -- D-03b). The
// GetModuleHandle/GetProcAddress shell + the logging live in the .cpp only.

#include "swg/engine_hookpoints.h"
#include <cstddef>

namespace swg::endpoints
{
// One binding per advertised name -> the storage cell of the existing pFn literal.
// (void** so the resolver can overwrite whatever concrete typedef the subsystem
// declared -- the cast back to the typed pointer is the subsystem's, unchanged.)
struct Binding
{
    const char* name; // contract name, e.g. "config::loadOverrideConfig"
    void** slot;      // &swg::<sub>::<fn> -- the literal's storage cell to overwrite
};

// Linear scan of table->entries for `name`; returns the borrowed addr or nullptr.
// Null-safe: a null table, null entries, or zero count returns nullptr.
const void* lookupByName(const EngineHookPoints* table, const char* name);

// WS-3 (init-only telemetry). Per-slot provenance AFTER resolve, on the advertised path:
//   Advertised  -- the slot was overwritten with the table's advertised addr (safe to call).
//   SwgemuRva   -- a MISS on a slot that still holds its (non-null) hardcoded SWGEmu literal:
//                  drift / version skew -> GARBAGE on the advertised client. Surfaced by the
//                  one-shot log so a dropped/renamed contract name is caught at init, not at crash.
//   Unresolved  -- a MISS on a slot that was null to begin with (advertised-only feature the
//                  provider didn't advertise this session; inert -- the null-guarded read-site degrades).
// NOTE: this is DIAGNOSTIC ONLY (crew review 2026-06-25). It is NOT a runtime safety layer and does NOT
// validate ABI/preconditions (the "resolved-but-wrong" 5th state) -- the static audit + per-subsystem
// isAdvertisedClient() guards own actual call-safety. Tags reflect what the slot holds at resolve time;
// the provider's lazy-fill GetEngineHookPoints() makes that authoritative at init (the static-init race
// that once null'd dynamic rows is fixed). A provider race regression would mislabel late-resolving rows.
enum class Source : unsigned char
{
    Unresolved = 0,
    Advertised,
    SwgemuRva,
};

// The PURE resolver (testable; no injection, no DLL access). For each binding whose
// name is found in the table with a non-null addr, overwrites *slot with that addr
// and counts it resolved; a missing name (or null addr) leaves the slot -- the RVA
// literal -- UNTOUCHED and counts it missing. NEVER nulls a slot on a miss (that
// would turn a graceful degrade into a guaranteed null-deref). Returns the resolved
// count. A null table / null entries / zero count resolves nothing and mutates
// nothing. A version mismatch logs a soft warning but still resolves by name.
//
// When `outSources` is non-null it MUST point at `count` Source cells: each processed
// binding i writes outSources[i] per the taxonomy above (a malformed row -> Unresolved).
// The early-return guard paths (null table etc.) do NOT touch outSources -- the caller
// zero-inits the array (Unresolved) before the call. Passing nullptr keeps the original
// behavior verbatim (the SWGEmu path never calls resolve at all).
int resolve(const EngineHookPoints* table, const Binding* bindings, size_t count,
            Source* outSources = nullptr);

// The thin dual-path shell (EPA-02): GetProcAddress(GetModuleHandleA(NULL),
// "GetEngineHookPoints"). If absent -> log info, return false, mutate nothing
// (SWGEmu Pre-CU no-op, D-00). If present -> call it and delegate to
// resolve(table, s_bindings, ...). Returns true iff the export was present.
bool resolveFromExe();

// Phase 24 advertised-client per-subsystem install gate.
// isAdvertisedClient() reflects whether resolveFromExe() found the GetEngineHookPoints
// export this session (the gl11/D3D11-or-SwgClient_* path vs SWGEmu Pre-CU).
// installable(target) is the gate each createDetours()/createPatches() subsystem checks
// on its PRIMARY hooked target BEFORE installing anything: on SWGEmu (export absent) it is
// ALWAYS true -- the full hook set installs byte-for-byte unchanged (D-00); on the
// advertised client it is true only when `target` is committed + executable, i.e. the
// subsystem's entry point WAS resolved from the catalog. A subsystem whose primary stayed
// at its (unmapped) SWGEmu literal is skipped WHOLE, so none of its detours, patches, or
// inline pointer derefs ever touch the wrong address. The lean-header contract is preserved
// (only bool / const void* named -- no Windows/DX types).
bool isAdvertisedClient();
bool installable(const void* target);

// DIAGNOSTIC (2026-06-24): re-read the advertised table at a POST-static-init call site (hkInstall)
// and count how many bound names have a non-null addr NOW -- a pure read, writes NO slot (a blanket
// re-resolve would clobber the detour trampolines). If this jumps from the utinni_init count (~40) to
// ~96, it confirms the table's function-call-initialized rows were null when resolveFromExe() ran at
// utinni_init (the exe's CRT static init had not yet run -- injected remote thread races the suspended
// main thread). Logs the count + a spot-check of a few dynamic-row names. Returns the count (-1 on the
// SWGEmu/no-export path).
int countResolvableNow();
} // namespace swg::endpoints
