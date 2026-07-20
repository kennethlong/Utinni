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
// Phase 24 / EPA-02, EPA-04 / D-03b -- the swg::endpoints resolver unit harness.
// resolve() is a PURE function (endpoints.h) that takes a synthetic
// EngineHookPoints table + a Binding list over LOCAL void* slot cells, so the
// table-parse + name-bind logic is exercised process-isolated -- NO injection, NO
// GetProcAddress, runs in CI (mirrors Dx11DetectionTests.cpp's pure-fn shape). The
// 3 maintainer live-smokes (Plan 04) then prove only inject + render, not the
// resolver logic.
//
// Coverage of the five behaviors:
//   [resolve]    two bound names overwrite their slots; a third absent name leaves
//                its slot at its sentinel/RVA value.
//   [dualpath]   resolve(nullptr,...) returns 0 and mutates no slot -- the SWGEmu
//                no-op analog (export-absent leaves every literal byte-for-byte).
//   [version]    version=999 still binds names (soft-warn path).
//   [coverage]   resolved/missing counts; the D-02 carve-out name is NOT in the
//                test binding list (allow-listed), so it is not a failure.
//   [robustness] null entries, count=0, and a row with addr=nullptr each leave
//                slots untouched and do not crash.
// ============================================================================

#include <catch2/catch_all.hpp>

#include <cstring>
#include <cstdint>
#include <vector>

#include "swg/endpoints.h"
#include "swg/vtbl_resolve.h"

using swg::endpoints::Binding;
using swg::endpoints::lookupByName;
using swg::endpoints::resolve;
using swg::endpoints::Source;

namespace
{
// Distinct sentinel addresses standing in for the hardcoded RVA literals. After a
// successful resolve the slot must hold the table's addr; on a miss it must still
// hold the sentinel (the RVA literal, untouched).
void* const kSentinelA = reinterpret_cast<void*>(static_cast<uintptr_t>(0x00401000));
void* const kSentinelB = reinterpret_cast<void*>(static_cast<uintptr_t>(0x007548A0));
void* const kSentinelC = reinterpret_cast<void*>(static_cast<uintptr_t>(0x00A9C6C0));

// Distinct "real function" addresses the synthetic table advertises.
void* const kRealA = reinterpret_cast<void*>(static_cast<uintptr_t>(0xAAAA0001));
void* const kRealB = reinterpret_cast<void*>(static_cast<uintptr_t>(0xBBBB0002));

EngineHookPoints makeTable(unsigned int version, const EngineHookPoint* entries,
                           unsigned int count)
{
    EngineHookPoints t{};
    t.version = version;
    t.count = count;
    t.entries = entries;
    return t;
}
} // namespace

TEST_CASE("endpoints: resolve overwrites bound slots, leaves an absent name's slot", "[endpoints][resolve]")
{
    const EngineHookPoint entries[] = {
        {"config::loadOverrideConfig", kRealA},
        {"graphics::install", kRealB},
        // NOTE: "config::loadConfigFileBuffer" deliberately NOT advertised.
    };
    EngineHookPoints table = makeTable(ENGINE_HOOKPOINTS_VERSION, entries, 2);

    void* slotA = kSentinelA; // config::loadOverrideConfig  -> should bind to kRealA
    void* slotB = kSentinelB; // graphics::install           -> should bind to kRealB
    void* slotC = kSentinelC; // config::loadConfigFileBuffer -> absent, stays sentinel

    const Binding bindings[] = {
        {"config::loadOverrideConfig", &slotA},
        {"graphics::install", &slotB},
        {"config::loadConfigFileBuffer", &slotC},
    };

    const int resolved = resolve(&table, bindings, 3);

    REQUIRE(resolved == 2);
    REQUIRE(slotA == kRealA);     // overwritten by name
    REQUIRE(slotB == kRealB);     // overwritten by name
    REQUIRE(slotC == kSentinelC); // absent name -> RVA literal untouched (graceful)
}

TEST_CASE("endpoints: null table is a strict no-op (export-absent / SWGEmu path)", "[endpoints][dualpath]")
{
    void* slotA = kSentinelA;
    void* slotB = kSentinelB;
    const Binding bindings[] = {
        {"config::loadOverrideConfig", &slotA},
        {"graphics::install", &slotB},
    };

    // Export-absent analog: a null table resolves nothing and mutates no slot --
    // every literal holds its pre-call value EXACTLY (D-00 / criterion 3).
    const int resolved = resolve(nullptr, bindings, 2);

    REQUIRE(resolved == 0);
    REQUIRE(slotA == kSentinelA);
    REQUIRE(slotB == kSentinelB);
}

TEST_CASE("endpoints: version mismatch still binds names (soft-warn path)", "[endpoints][version]")
{
    const EngineHookPoint entries[] = {
        {"config::loadOverrideConfig", kRealA},
    };
    EngineHookPoints table = makeTable(/*version=*/999, entries, 1);

    void* slotA = kSentinelA;
    const Binding bindings[] = {
        {"config::loadOverrideConfig", &slotA},
    };

    const int resolved = resolve(&table, bindings, 1);

    REQUIRE(resolved > 0); // soft path: resolves by name despite version drift
    REQUIRE(slotA == kRealA);
}

TEST_CASE("endpoints: coverage counts resolved/missing, carve-out not in binding list", "[endpoints][coverage]")
{
    // The advertised table has one of the two requested names; the D-02 carve-out
    // (consoleHelper::sendInput) is NOT placed in the binding list -- it is
    // allow-listed, so its absence is NOT a coverage failure.
    const EngineHookPoint entries[] = {
        {"config::loadOverrideConfig", kRealA},
    };
    EngineHookPoints table = makeTable(ENGINE_HOOKPOINTS_VERSION, entries, 1);

    void* slotA = kSentinelA; // present in the table
    void* slotB = kSentinelB; // requested but absent from the table -> counted missing

    const Binding bindings[] = {
        {"config::loadOverrideConfig", &slotA},
        {"graphics::install", &slotB},
    };

    const int resolved = resolve(&table, bindings, 2);

    REQUIRE(resolved == 1); // exactly one of two requested names resolved
    REQUIRE(slotA == kRealA);
    REQUIRE(slotB == kSentinelB); // missing -> RVA literal kept

    // The carve-out name is intentionally never requested; lookupByName confirms it
    // is simply not in this fixture (the resolver allow-lists it from the gate).
    REQUIRE(lookupByName(&table, "consoleHelper::sendInput") == nullptr);
}

// ----------------------------------------------------------------------
// Plan 02 / D-01: full-catalog coverage. The complete advertised name set is the
// .inc X-macro list MINUS the single D-02 carve-out (consoleHelper::sendInput).
// We build that name set HERE from the canonical .inc (re-included with a local
// ENGINE_HOOKPOINT macro) so this test fails the moment the .inc and the expected
// 78-name override scope drift apart -- the same single-source-of-truth gate the
// resolver's compile-time static_assert uses, exercised at runtime over a synthetic
// table. No injection, no real s_bindings[] -- pure process-isolated proof that the
// resolver resolves the entire catalog when every name is advertised.
// ----------------------------------------------------------------------
namespace
{
// The carve-outs the coverage gate allow-lists: in the .inc, intentionally NOT bound.
// consoleHelper::sendInput (D-02/WR-05) + client::wndProc (embed-resize regression; RNDR-04).
constexpr const char* kCarveOuts[] = {"consoleHelper::sendInput", "client::wndProc"};

bool sameName(const char* a, const char* b)
{
    return std::strcmp(a, b) == 0;
}

bool isCarveOut(const char* n)
{
    for (const char* c : kCarveOuts)
    {
        if (sameName(n, c))
        {
            return true;
        }
    }
    return false;
}
} // namespace

TEST_CASE("endpoints: full catalog (141 of 143 .inc names) all resolve, carve-outs excluded", "[endpoints][coverage]")
{
    // Every .inc name, in declaration order, sourced from the canonical X-macro list.
    static const char* const kAllIncNames[] = {
#define ENGINE_HOOKPOINT(group, name) #group "::" #name,
#include "swg/engine_hookpoints.inc"
#undef ENGINE_HOOKPOINT
    };
    constexpr size_t kIncCount = sizeof(kAllIncNames) / sizeof(kAllIncNames[0]);
    REQUIRE(kIncCount == 143); // the contract size (drift gate -- mirrors the provider count, v21 / current-scene-id copy-out: 1 name-add)

    // The expected override scope is the .inc MINUS the two carve-outs -> 141.
    std::vector<const char*> expectedNames;
    for (const char* n : kAllIncNames)
    {
        if (!isCarveOut(n))
        {
            expectedNames.push_back(n);
        }
    }
    REQUIRE(expectedNames.size() == 141);

    // Synthesize a table advertising EVERY .inc name (incl. the carve-out -- the
    // provider DOES advertise it; the consumer simply does not bind it). Each row
    // gets a distinct non-null sentinel address.
    std::vector<EngineHookPoint> entries;
    entries.reserve(kIncCount);
    for (size_t i = 0; i < kIncCount; ++i)
    {
        void* addr = reinterpret_cast<void*>(static_cast<uintptr_t>(0xC0DE0000u + i));
        entries.push_back({kAllIncNames[i], addr});
    }
    EngineHookPoints table =
        makeTable(ENGINE_HOOKPOINTS_VERSION, entries.data(), static_cast<unsigned int>(entries.size()));

    // Build the binding list = the 92 expected names over local slot cells. The
    // carve-out is deliberately NOT in this list (allow-listed out of the gate).
    std::vector<void*> slots(expectedNames.size(), nullptr);
    std::vector<Binding> bindings;
    bindings.reserve(expectedNames.size());
    for (size_t i = 0; i < expectedNames.size(); ++i)
    {
        bindings.push_back({expectedNames[i], &slots[i]});
    }

    const int resolved = resolve(&table, bindings.data(), bindings.size());

    REQUIRE(resolved == 141); // full catalog resolved (D-01, v21 minus 2 carve-outs)
    for (void* s : slots)     // every requested name overwrote its slot
    {
        REQUIRE(s != nullptr);
    }

    // The carve-outs are advertised by the table but never requested -> not a failure.
    for (const char* c : kCarveOuts)
    {
        REQUIRE(lookupByName(&table, c) != nullptr); // each IS in the table
        for (const Binding& b : bindings)            // but never in the binding list
        {
            REQUIRE_FALSE(sameName(b.name, c));
        }
    }
}

TEST_CASE("endpoints: coverage counts resolved/missing with one absent name", "[endpoints][coverage]")
{
    // The advertised table has one of the two requested names; the D-02 carve-out
    // (consoleHelper::sendInput) is NOT placed in the binding list -- it is
    // allow-listed, so its absence is NOT a coverage failure.
    const EngineHookPoint entries[] = {
        {"config::loadOverrideConfig", kRealA},
    };
    EngineHookPoints table = makeTable(ENGINE_HOOKPOINTS_VERSION, entries, 1);

    void* slotA = kSentinelA; // present in the table
    void* slotB = kSentinelB; // requested but absent from the table -> counted missing

    const Binding bindings[] = {
        {"config::loadOverrideConfig", &slotA},
        {"graphics::install", &slotB},
    };

    const int resolved = resolve(&table, bindings, 2);

    REQUIRE(resolved == 1); // exactly one of two requested names resolved
    REQUIRE(slotA == kRealA);
    REQUIRE(slotB == kSentinelB); // missing -> RVA literal kept

    // The carve-out name is intentionally never requested; lookupByName confirms it
    // is simply not in this fixture (the resolver allow-lists it from the gate).
    REQUIRE(lookupByName(&table, "consoleHelper::sendInput") == nullptr);
}

// ----------------------------------------------------------------------
// Plan 03 / EPA-03: the DX11 kickoff depends on graphics::install resolving from
// the advertised table (Plan 02 bound it). On the advertised client the resolver
// overwrites the graphics::install literal so the existing hkInstall detour installs
// on the REAL Graphics::install and directX11::kickoff() fires naturally (D-05
// Approach A). This unit LOCKS that dependency headlessly: if a future change drops
// graphics::install from the bindings, the DX11 overlay would silently regress (the
// detour would stay on the wrong hardcoded 0x007548A0 on SwgClient_r.exe). Proving
// the resolver still binds graphics::install here keeps the maintainer live-smoke
// (Plan 04) focused on proving render, not re-discovering a dropped binding.
// ----------------------------------------------------------------------
TEST_CASE("endpoints: graphics::install resolves from the table (EPA-03 DX11 kickoff dependency)", "[endpoints][epa03]")
{
    SECTION("advertised graphics::install overwrites its slot (DX11 kickoff path)")
    {
        // A synthetic table advertising graphics::install at a sentinel "real" address.
        const EngineHookPoint entries[] = {
            {"graphics::install", kRealB},
        };
        EngineHookPoints table = makeTable(ENGINE_HOOKPOINTS_VERSION, entries, 1);

        // The slot stands in for swg::graphics::install (hardcoded 0x007548A0 literal).
        void* installSlot = kSentinelB; // == 0x007548A0, the SWGEmu install RVA
        const Binding bindings[] = {
            {"graphics::install", &installSlot},
        };

        const int resolved = resolve(&table, bindings, 1);

        REQUIRE(resolved == 1);
        REQUIRE(installSlot == kRealB); // resolved -> hkInstall now detours the REAL fn
    }

    SECTION("absent graphics::install leaves the install literal untouched (SWGEmu no-op)")
    {
        // The table advertises something else; graphics::install is NOT in it. On
        // SWGEmu (export absent / name not advertised) the literal stays on its RVA so
        // the existing D3D9 hkInstall path is byte-for-byte unchanged (D-00).
        const EngineHookPoint entries[] = {
            {"config::loadOverrideConfig", kRealA},
        };
        EngineHookPoints table = makeTable(ENGINE_HOOKPOINTS_VERSION, entries, 1);

        void* installSlot = kSentinelB; // 0x007548A0 -- must remain the RVA literal
        const Binding bindings[] = {
            {"graphics::install", &installSlot},
        };

        const int resolved = resolve(&table, bindings, 1);

        REQUIRE(resolved == 0);
        REQUIRE(installSlot == kSentinelB); // untouched -> SWGEmu D3D9 install path intact
    }
}

// ----------------------------------------------------------------------
// WS-3 (init telemetry): resolve(..., Source* out) tags each slot's provenance so
// resolveFromExe() can one-shot log bound-but-missed names (drift / version skew) at
// init instead of at the eventual crash. Pure, process-isolated -- the same synthetic
// table shape as the resolver tests, no injection. This is DIAGNOSTIC ONLY (not a
// runtime safety layer -- crew review 2026-06-25); the test pins the taxonomy:
//   Advertised  -- name found in the table (slot overwritten).
//   SwgemuRva   -- name absent AND the slot still holds a non-null literal (unsafe on advertised).
//   Unresolved  -- name absent AND the slot was null (inert advertised-only miss), or a malformed row.
// ----------------------------------------------------------------------
TEST_CASE("endpoints: resolve tags slot provenance (Advertised / SwgemuRva / Unresolved)", "[endpoints][source]")
{
    // Table advertises ONLY name A; B and C are absent (a drift / partial-table scenario).
    const EngineHookPoint entries[] = {
        {"config::loadOverrideConfig", kRealA},
    };
    EngineHookPoints table = makeTable(ENGINE_HOOKPOINTS_VERSION, entries, 1);

    void* slotA = kSentinelA; // present -> Advertised
    void* slotB = kSentinelB; // absent, non-null literal -> SwgemuRva (garbage on advertised)
    void* slotC = nullptr;    // absent, null slot -> Unresolved (inert advertised-only miss)
    const Binding bindings[] = {
        {"config::loadOverrideConfig", &slotA},
        {"graphics::install", &slotB},
        {"game::loadScene", &slotC},
    };

    Source sources[3] = {Source::Unresolved, Source::Unresolved, Source::Unresolved};
    const int resolved = resolve(&table, bindings, 3, sources);

    REQUIRE(resolved == 1);
    REQUIRE(slotA == kRealA);     // hit -> overwritten
    REQUIRE(slotB == kSentinelB); // miss -> literal kept (graceful degrade, never nulled)
    REQUIRE(slotC == nullptr);    // miss -> null slot kept

    REQUIRE(sources[0] == Source::Advertised);
    REQUIRE(sources[1] == Source::SwgemuRva); // THE drift signal the init log surfaces
    REQUIRE(sources[2] == Source::Unresolved);

    SECTION("a malformed binding row tags Unresolved, never deref")
    {
        void* slot = kSentinelA;
        const Binding bad[] = {
            {nullptr, &slot},                        // null name
            {"config::loadOverrideConfig", nullptr}, // null slot
        };
        Source s[2] = {Source::Advertised, Source::Advertised}; // poisoned to prove they get rewritten
        const int r = resolve(&table, bad, 2, s);
        REQUIRE(r == 0);
        REQUIRE(s[0] == Source::Unresolved);
        REQUIRE(s[1] == Source::Unresolved);
        REQUIRE(slot == kSentinelA); // untouched
    }

    SECTION("nullptr outSources keeps the original behavior (no crash, slots still resolve)")
    {
        void* s = kSentinelB;
        const Binding b[] = {{"config::loadOverrideConfig", &s}};
        // The 3-arg call shape (outSources defaulted to nullptr) must still bind by name.
        REQUIRE(resolve(&table, b, 1) == 1);
        REQUIRE(s == kRealA);
    }
}

TEST_CASE("endpoints: null entries / zero count / null addr degrade without crash or mutation", "[endpoints][robustness]")
{
    void* slotA = kSentinelA;
    const Binding bindings[] = {
        {"config::loadOverrideConfig", &slotA},
    };

    SECTION("null entries pointer")
    {
        EngineHookPoints table = makeTable(ENGINE_HOOKPOINTS_VERSION, nullptr, 3);
        REQUIRE(resolve(&table, bindings, 1) == 0);
        REQUIRE(slotA == kSentinelA);
    }

    SECTION("zero count")
    {
        const EngineHookPoint entries[] = {{"config::loadOverrideConfig", kRealA}};
        EngineHookPoints table = makeTable(ENGINE_HOOKPOINTS_VERSION, entries, 0);
        REQUIRE(resolve(&table, bindings, 1) == 0);
        REQUIRE(slotA == kSentinelA); // zero count -> nothing scanned, literal kept
    }

    SECTION("entry with a null addr is treated as not-bindable (slot untouched)")
    {
        const EngineHookPoint entries[] = {{"config::loadOverrideConfig", nullptr}};
        EngineHookPoints table = makeTable(ENGINE_HOOKPOINTS_VERSION, entries, 1);
        REQUIRE(resolve(&table, bindings, 1) == 0);
        REQUIRE(slotA == kSentinelA); // null addr -> never written
    }

    SECTION("a binding row with a null slot pointer is skipped, not dereferenced")
    {
        const EngineHookPoint entries[] = {{"config::loadOverrideConfig", kRealA}};
        EngineHookPoints table = makeTable(ENGINE_HOOKPOINTS_VERSION, entries, 1);
        const Binding badBindings[] = {{"config::loadOverrideConfig", nullptr}};
        REQUIRE(resolve(&table, badBindings, 1) == 0); // null slot -> skipped
    }
}

// ============================================================================
// Phase 24 Item-2 -- consumer-side virtual-method vtable resolution (vtbl_resolve.h).
// Pure, process-isolated: a synthetic vtable + a synthetic instance prove slot()
// reads the right entry, that the SWGEmu empirical self-check LOGIC trips on a wrong
// index, and that the source-derived + Codex-validated indices stay pinned. The LIVE
// empirical check (slot(liveInstance,index) == known SWGEmu RVA) runs in
// cui_io.cpp::hkProcessEvent on the next SWGEmu smoke.
// ============================================================================
TEST_CASE("vtbl::slot resolves the correct vtable entry; indices pinned", "[endpoints][vtbl]")
{
    using namespace swg::vtbl;

    // A real C++ object's first word is its vtable pointer; the vtable is an array of
    // code addresses. Build that shape synthetically (16 fake entries).
    void* fakeVtable[16];
    for (int i = 0; i < 16; ++i)
    {
        fakeVtable[i] = reinterpret_cast<void*>(static_cast<std::uintptr_t>(0x00B20000 + i * 0x10));
    }
    void* vtablePtr = &fakeVtable[0];
    const void* instance = &vtablePtr; // *instance == &fakeVtable[0]

    SECTION("slot() returns the entry at the given index")
    {
        REQUIRE(slot(instance, 0) == fakeVtable[0]);
        REQUIRE(slot(instance, kCuiIoProcessEvent) == fakeVtable[1]);
        REQUIRE(slot(instance, kObjectAddToWorld) == fakeVtable[2]);
        REQUIRE(slot(instance, kObjectRemoveFromWorld) == fakeVtable[3]);
        REQUIRE(slot(instance, kObjectSetParentCell) == fakeVtable[13]);
    }

    SECTION("slot() is null-safe (null instance / null vtable pointer)")
    {
        REQUIRE(slot(nullptr, 0) == nullptr);
        void* nullVtbl = nullptr;
        const void* badInstance = &nullVtbl;
        REQUIRE(slot(badInstance, 5) == nullptr);
    }

    SECTION("the empirical self-check logic detects a WRONG index")
    {
        // The known-good RVA sits at the CORRECT slot; the self-check passes there and
        // trips at any other index -- exactly the SWGEmu guard in hkProcessEvent.
        const void* knownRva = fakeVtable[kObjectSetParentCell];
        REQUIRE(slot(instance, kObjectSetParentCell) == knownRva);     // correct -> PASS
        REQUIRE(slot(instance, kObjectSetParentCell - 1) != knownRva); // wrong   -> FAIL trips
    }

    SECTION("derived indices are pinned (a vtbl_resolve.h regression trips CI here)")
    {
        REQUIRE(kCuiIoProcessEvent == 1);
        REQUIRE(kObjectAddToWorld == 2);
        REQUIRE(kObjectRemoveFromWorld == 3);
        REQUIRE(kObjectSetParentCell == 13);
    }
}
