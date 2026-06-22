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
// UtinniEngineHookPoints table + a Binding list over LOCAL void* slot cells, so the
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

#include "swg/endpoints.h"

using swg::endpoints::Binding;
using swg::endpoints::lookupByName;
using swg::endpoints::resolve;

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

UtinniEngineHookPoints makeTable(unsigned int version, const UtinniEngineHookPoint* entries,
                                 unsigned int count)
{
    UtinniEngineHookPoints t{};
    t.version = version;
    t.count = count;
    t.entries = entries;
    return t;
}
} // namespace

TEST_CASE("endpoints: resolve overwrites bound slots, leaves an absent name's slot", "[endpoints][resolve]")
{
    const UtinniEngineHookPoint entries[] = {
        {"config::loadOverrideConfig", kRealA},
        {"graphics::install", kRealB},
        // NOTE: "config::loadConfigFileBuffer" deliberately NOT advertised.
    };
    UtinniEngineHookPoints table = makeTable(UTINNI_HOOKPOINTS_VERSION, entries, 2);

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
    REQUIRE(slotA == kRealA);       // overwritten by name
    REQUIRE(slotB == kRealB);       // overwritten by name
    REQUIRE(slotC == kSentinelC);   // absent name -> RVA literal untouched (graceful)
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
    const UtinniEngineHookPoint entries[] = {
        {"config::loadOverrideConfig", kRealA},
    };
    UtinniEngineHookPoints table = makeTable(/*version=*/999, entries, 1);

    void* slotA = kSentinelA;
    const Binding bindings[] = {
        {"config::loadOverrideConfig", &slotA},
    };

    const int resolved = resolve(&table, bindings, 1);

    REQUIRE(resolved > 0);    // soft path: resolves by name despite version drift
    REQUIRE(slotA == kRealA);
}

TEST_CASE("endpoints: coverage counts resolved/missing, carve-out not in binding list", "[endpoints][coverage]")
{
    // The advertised table has one of the two requested names; the D-02 carve-out
    // (consoleHelper::sendInput) is NOT placed in the binding list -- it is
    // allow-listed, so its absence is NOT a coverage failure.
    const UtinniEngineHookPoint entries[] = {
        {"config::loadOverrideConfig", kRealA},
    };
    UtinniEngineHookPoints table = makeTable(UTINNI_HOOKPOINTS_VERSION, entries, 1);

    void* slotA = kSentinelA; // present in the table
    void* slotB = kSentinelB; // requested but absent from the table -> counted missing

    const Binding bindings[] = {
        {"config::loadOverrideConfig", &slotA},
        {"graphics::install", &slotB},
    };

    const int resolved = resolve(&table, bindings, 2);

    REQUIRE(resolved == 1);          // exactly one of two requested names resolved
    REQUIRE(slotA == kRealA);
    REQUIRE(slotB == kSentinelB);    // missing -> RVA literal kept

    // The carve-out name is intentionally never requested; lookupByName confirms it
    // is simply not in this fixture (the resolver allow-lists it from the gate).
    REQUIRE(lookupByName(&table, "consoleHelper::sendInput") == nullptr);
}

TEST_CASE("endpoints: null entries / zero count / null addr degrade without crash or mutation", "[endpoints][robustness]")
{
    void* slotA = kSentinelA;
    const Binding bindings[] = {
        {"config::loadOverrideConfig", &slotA},
    };

    SECTION("null entries pointer")
    {
        UtinniEngineHookPoints table = makeTable(UTINNI_HOOKPOINTS_VERSION, nullptr, 3);
        REQUIRE(resolve(&table, bindings, 1) == 0);
        REQUIRE(slotA == kSentinelA);
    }

    SECTION("zero count")
    {
        const UtinniEngineHookPoint entries[] = {{"config::loadOverrideConfig", kRealA}};
        UtinniEngineHookPoints table = makeTable(UTINNI_HOOKPOINTS_VERSION, entries, 0);
        REQUIRE(resolve(&table, bindings, 1) == 0);
        REQUIRE(slotA == kSentinelA); // zero count -> nothing scanned, literal kept
    }

    SECTION("entry with a null addr is treated as not-bindable (slot untouched)")
    {
        const UtinniEngineHookPoint entries[] = {{"config::loadOverrideConfig", nullptr}};
        UtinniEngineHookPoints table = makeTable(UTINNI_HOOKPOINTS_VERSION, entries, 1);
        REQUIRE(resolve(&table, bindings, 1) == 0);
        REQUIRE(slotA == kSentinelA); // null addr -> never written
    }

    SECTION("a binding row with a null slot pointer is skipped, not dereferenced")
    {
        const UtinniEngineHookPoint entries[] = {{"config::loadOverrideConfig", kRealA}};
        UtinniEngineHookPoints table = makeTable(UTINNI_HOOKPOINTS_VERSION, entries, 1);
        const Binding badBindings[] = {{"config::loadOverrideConfig", nullptr}};
        REQUIRE(resolve(&table, badBindings, 1) == 0); // null slot -> skipped
    }
}
