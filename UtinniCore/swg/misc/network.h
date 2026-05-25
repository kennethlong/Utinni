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

#include "utinni.h"
#include "swg/object/object.h"

// CR-02: test-seam declarations for swg::network::cast pointer reseat.
// setCastForTest and resetCast are implemented in network.cpp; only callable
// from test_exports.cpp. Production code does not call these.
namespace swg::network
{
// CR-02: OUT param widened to int64_t* (D-06 option c hybrid).
using pCast = int64_t(__thiscall*)(int64_t*, int, int);
void setCastForTest(pCast fn); // test-only seam; do not call from production
void resetCast();              // restores realCast pointer
} // namespace swg::network

namespace utinni
{
class UTINNI_API Network
{
public:
    static swgptr getManger();
    static Object* getObjectById(const int64_t& id);
    static Object* getCachedObjectById(swgptr pCachedNetworkId);

    static int64_t cast(int64_t id);

public:
    static bool isServerId(int64_t id)
    {
        if (id >= 0x1000000)
        {
            return true;
        }

        return false;
    }
};
} // namespace utinni
