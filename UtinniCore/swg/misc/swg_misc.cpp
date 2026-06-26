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

#include "swg_misc.h"
#include "swg/endpoints.h"

namespace swg::report
{
using pPrint = void(__cdecl*)(const char* msg);

pPrint print = (pPrint)0x00A88F90;
} // namespace swg::report

namespace utinni::report
{
void __cdecl hkPrint(const char* msg)
{
    swg::report::print(msg);

    std::string output = msg;

    // Check if the message contains newline's and remove them to not clog up the log
    if (output[output.length() - 1] == '\n')
    {
        output.erase(std::remove(output.begin(), output.end(), '\n'), output.end());
    }

    log::info(("[SWG] " + output).c_str());
}
void detour()
{
    // WS-4 (first MISC slice unlocked on the advertised client). report::print is an advertised CLEAN
    // row (resolved 97/97 by name on advertised), so the resolver overwrites the SWGEmu literal
    // 0x00A88F90 with the relocated provider address before createDetours() runs. installable()-gate
    // the detour so it installs on the REAL target only: on SWGEmu byte-for-byte as before (D-00); on
    // the advertised client on the relocated provider entry. hkPrint is a pure pass-through + log
    // forward (no scene/player/RVA state, exact __cdecl(const char*) ABI), so it is safe on both. If
    // report::print were ever left unresolved on advertised, skip rather than risk corrupting relocated
    // code (installable() is necessary-not-sufficient, but 97/97 resolution makes the address authoritative).
    if (!swg::endpoints::installable((const void*)swg::report::print))
    {
        utinni::log::info("report: detour SKIPPED (print target not installable on this client)");
        return;
    }

    swg::report::print = (swg::report::pPrint)Detour::Create(swg::report::print, hkPrint, DETOUR_TYPE_PUSH_RET);
}
} // namespace utinni::report
