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

// Phase 24 / EPA-02, EPA-04: swg::endpoints resolver implementation.
//
// The header (endpoints.h) is deliberately lean (only utinni_engine_hookpoints.h
// + <cstddef>). This TU carries the INJECTION-FREE half -- the pure resolve(),
// lookupByName(), the D-02 allow-list, and the D-03a compile-time X-macro subset
// static_assert -- none of which reference any subsystem symbol, so the test
// project can compile it standalone (Option-A split, mirroring render_backend.cpp).
//
// The symbol-bearing half -- the extern config/graphics pFn declarations, the
// s_bindings[] critical-path seed, and the resolveFromExe() GetProcAddress shell --
// lives in endpoints_bindings.cpp so it is compiled ONLY into UtinniCore.dll and
// never drags the not-exported subsystem literals into the test link (LNK2001).
// The subset static_assert lives HERE next to the .inc re-include and is duplicated
// against the s_bindings[] names in endpoints_bindings.cpp (one assert per side).

#include "endpoints.h"

#include <cstring>
#include <cstdio>

#include "utinni.h"          // byte/swgptr + the build's common prelude
#include "utility/log.h"     // utinni::log::{info,warning}

namespace swg::endpoints
{
// ----------------------------------------------------------------------
// D-03a: compile-time X-macro subset assert. Re-include the canonical .inc with
// a UTINNI_HOOKPOINT macro that builds a constexpr "is this name in the .inc set?"
// predicate, then static_assert each s_bindings[] name is a member. A bogus
// binding name (drift, Pitfall 5) fails the BUILD here, not silently at runtime.
//
// The predicate is a constexpr strcmp-OR over every .inc row. consteval/constexpr
// string compare avoids any runtime cost; the static_asserts below are the gate.
// ----------------------------------------------------------------------
namespace
{
constexpr bool ceStrEq(const char* a, const char* b)
{
    while (*a && (*a == *b))
    {
        ++a;
        ++b;
    }
    return *a == *b;
}

// True iff `n` (a "group::name" contract string) is advertised in the .inc set.
constexpr bool isInHookpointInc(const char* n)
{
    return false
// Each .inc row contributes an OR term: name == "group::name".
#define UTINNI_HOOKPOINT(group, name) || ceStrEq(n, #group "::" #name)
#include "swg/utinni_engine_hookpoints.inc"
#undef UTINNI_HOOKPOINT
        ;
}

// One static_assert per seeded binding name -- the build fails if any name is not
// a member of the .inc catalog (subset invariant, EPA-04 layer a).
static_assert(isInHookpointInc("config::loadOverrideConfig"), "binding not in .inc");
static_assert(isInHookpointInc("config::loadConfigFileBuffer"), "binding not in .inc");
static_assert(isInHookpointInc("config::loadConfigFileString"), "binding not in .inc");
static_assert(isInHookpointInc("graphics::install"), "binding not in .inc");
// Negative control: a name the .inc does NOT advertise must fail the predicate,
// proving the gate actually discriminates (drift would otherwise pass silently).
static_assert(!isInHookpointInc("config::bogusNotAdvertised"), "predicate too permissive");

// D-02 carve-out is allow-listed OUT of the coverage gate: it IS in the .inc but
// is intentionally NOT bound. Counting it missing would read as a false EPA-04
// failure. Kept as a named constant so the coverage log can exclude it.
constexpr const char* kIntentionalUnbound[] = {"consoleHelper::sendInput"};

bool isIntentionalUnbound(const char* name)
{
    for (const char* u : kIntentionalUnbound)
    {
        if (ceStrEq(u, name))
        {
            return true;
        }
    }
    return false;
}
} // namespace

const void* lookupByName(const UtinniEngineHookPoints* table, const char* name)
{
    if (table == nullptr || table->entries == nullptr || name == nullptr)
    {
        return nullptr; // null/partial table -> graceful, no deref (T-24-01)
    }

    for (unsigned int i = 0; i < table->count; ++i)
    {
        const UtinniEngineHookPoint& e = table->entries[i];
        if (e.name != nullptr && std::strcmp(e.name, name) == 0)
        {
            return e.addr; // may itself be null -> caller treats as "not bindable"
        }
    }
    return nullptr;
}

int resolve(const UtinniEngineHookPoints* table, const Binding* bindings, size_t count)
{
    // T-24-01: null/partial table or empty binding list -> resolve nothing, mutate
    // nothing, no deref. (table->entries / table->count are guarded in lookupByName.)
    if (table == nullptr || table->entries == nullptr || table->count == 0 ||
        bindings == nullptr || count == 0)
    {
        return 0;
    }

    // T-24-02: version drift logs a soft warning but still resolves BY NAME (the
    // real drift gate is the byte-identical .inc + the compile-time subset assert).
    if (table->version != UTINNI_HOOKPOINTS_VERSION)
    {
        utinni::log::warning("endpoints: contract version mismatch -- resolving by name anyway");
    }

    int resolved = 0;
    int missing = 0;
    for (size_t i = 0; i < count; ++i)
    {
        const Binding& b = bindings[i];
        if (b.name == nullptr || b.slot == nullptr)
        {
            continue; // a malformed binding row -> skip, never deref
        }

        const void* addr = lookupByName(table, b.name);
        if (addr != nullptr)
        {
            // Overwrite the literal in place; the subsystem's typed pointer now
            // points at the advertised function instead of the hardcoded RVA.
            *b.slot = const_cast<void*>(addr);
            ++resolved;
        }
        else
        {
            // Miss: leave the RVA literal UNTOUCHED (graceful degrade -- never null
            // a slot). The D-02 carve-out is allow-listed so it is not flagged.
            if (!isIntentionalUnbound(b.name))
            {
                ++missing;
            }
        }
    }

    // D-03c: resolved/missing/coverage summary (EPA-04 runtime layer).
    char msg[160];
    std::snprintf(msg, sizeof(msg),
                  "endpoints: resolved %d/%zu by name (%d missing, carve-outs allow-listed)",
                  resolved, count, missing);
    utinni::log::info(msg);
    return resolved;
}
} // namespace swg::endpoints
