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

#include "swg_utility.h"
#include "swg/endpoints.h"

namespace swg::utility
{
using pCalculateCrc = unsigned int(__cdecl*)(const char* str);
using pTreeFileOpen = swgptr(__cdecl*)(const char* filename, int priorityType, bool allowFail);

pCalculateCrc calculateCrc = (pCalculateCrc)0x00AA4790;
pTreeFileOpen treeFileOpen = (pTreeFileOpen)0x00A931E0;

} // namespace swg::utility

namespace utinni
{
unsigned int calculateCrc(const char* string)
{
    return swg::utility::calculateCrc(string);
}

swgptr treeFileOpen(const char* filename, int priorityType, bool allowFail)
{
    // Phase 24: swg::utility::treeFileOpen is a hardcoded SWGEmu address (0x00A931E0) that is
    // NOT in the advertised catalog, so it is not resolved on the advertised client -- calling
    // it executes the wrong code and crashes. Fail safe (return 0 = "not found"; callers pass
    // allowFail) on the advertised client. No-op on SWGEmu (D-00).
    if (swg::endpoints::isAdvertisedClient())
        return 0;

    return swg::utility::treeFileOpen(filename, priorityType, allowFail);
}

} // namespace utinni
