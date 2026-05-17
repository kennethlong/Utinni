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

#include <Windows.h>
#include <cstdio>

// Harness for C-01: measure DllMain entry-exit timing of UtinniCore.dll.
// Exits 0 if LoadLibraryA returns within 50 ms threshold (DllMain did no heavy work).
// Exits 1 otherwise (DllMain regression -- heavy startup leaked back in).
// Exits 2 if LoadLibraryA fails entirely.
//
// This is a regression guard: it catches "someone moved heavy work back into DllMain".
// Full proof of "no deadlock under loader-lock contention" remains a Tier-4 manual
// verification per CONTEXT.md D-06 (inject UtinniCore.dll into a live SWG client).

int main(int /*argc*/, char* /*argv*/[])
{
    LARGE_INTEGER freq, start, end;
    QueryPerformanceFrequency(&freq);
    QueryPerformanceCounter(&start);

    HMODULE hDll = LoadLibraryA("UtinniCore.dll");

    QueryPerformanceCounter(&end);

    if (hDll == nullptr)
    {
        std::fprintf(stderr, "[ERROR] LoadLibraryA(UtinniCore.dll) returned nullptr (GLE=%lu)\n", GetLastError());
        return 2;
    }

    const double elapsedMs = (double)(end.QuadPart - start.QuadPart) * 1000.0 / (double)freq.QuadPart;
    std::printf("UtinniCore DllMain elapsed: %.3f ms\n", elapsedMs);

    FreeLibrary(hDll);

    return (elapsedMs < 50.0) ? 0 : 1;
}
