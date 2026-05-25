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

#include "network.h"
#include "swg/object/object.h"

namespace swg::network
{
// 00B37F30 -- NetworkIdManager & NetworkIdManager::getInstance ()
using pIdManagerGetObjectById = utinni::Object*(__cdecl*)(const int64_t& id);
using pIdManagerGetInstance = swgptr(__cdecl*)();

using pCachedNetworkIdGetObject = utinni::Object*(__thiscall*)(swgptr pThis); // pThis = CachedNetworkId, should be 8 bytes?

// Note: pCast is declared in network.h (namespace swg::network) — CR-02: OUT param widened to int64_t*.

pIdManagerGetObjectById idManagerGetObjectById = (pIdManagerGetObjectById)0x00B380E0;
pIdManagerGetInstance idManagerGetInstance = (pIdManagerGetInstance)0x00B37F30;

pCachedNetworkIdGetObject cachedNetworkIdGetObject = (pCachedNetworkIdGetObject)0x00B30160;

// Keep realCast as the original RVA; cast is the mutable reseat-able pointer.
static pCast realCast = (pCast)0xAA4900;
pCast cast = realCast;

// Test-seam: allows test_exports.cpp to temporarily replace the cast pointer.
// Not guarded by #ifdef UTINNI_TESTS — project convention is unguarded test exports
// (see test_exports.cpp). Production code never calls setCastForTest.
void setCastForTest(pCast fn)
{
    cast = fn;
}
void resetCast()
{
    cast = realCast;
}
} // namespace swg::network

namespace utinni
{

swgptr Network::getManger()
{
    return swg::network::idManagerGetInstance(); // See if this needs to be read
}

Object* Network::getObjectById(const int64_t& id)
{
    return swg::network::idManagerGetObjectById(id);
}

Object* Network::getCachedObjectById(swgptr pCachedNetworkId)
{
    return swg::network::cachedNetworkIdGetObject(pCachedNetworkId);
}

int64_t Network::cast(int64_t id)
{
    // CR-02/CR-03: OUT param is int64_t* (8-byte slot); shift on int64_t is well-defined.
    // C-03: networkId must be initialized; SWG cast writes through &networkId; the function's
    // int64_t return is unreliable per CONCERNS.md TD-03 — read networkId after the call.
    static_assert(sizeof(int64_t) == 8, "CR-02: networkId slot must be 8 bytes");
    int64_t networkId = 0;
    swg::network::cast(&networkId,
                       static_cast<int>(id & 0xFFFFFFFFLL),
                       static_cast<int>((id >> 32) & 0xFFFFFFFFLL));
    return networkId;
}

} // namespace utinni
