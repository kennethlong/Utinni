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

// Harness for C-01: regression guard that UtinniCore.dll's DllMain stays lightweight
// ("nobody moved heavy startup back into DllMain"). Exit codes:
//   0  intrinsic load time  <  50 ms threshold (DllMain did no heavy work)
//   1  intrinsic load time  >= 50 ms threshold (DllMain regression -- heavy startup leaked back in)
//   2  LoadLibraryA failed entirely
//
// 06-04 OPT-A (best-of-3 minimum) per 06-04-FLAKE-INVESTIGATION.md
// -------------------------------------------------------------------------------------
// The previous form took a SINGLE cold LoadLibraryA sample and compared its wall time to
// 50 ms. That wall time conflates two unrelated things: the OS loader overhead (page-
// mapping the image + resolving UtinniCore's static-import graph + CRT static-init -- the
// part that spikes under shared-runner CONTENTION) and the DllMain body itself, which is
// microseconds (DisableThreadLibraryCalls + return TRUE; see utinni.cpp DllMain). A single
// sample against a single number flaked red whenever the loader was momentarily slow
// (see run 26190579282).
//
// Fix: run three full LoadLibraryA + FreeLibrary cycles and compare the MINIMUM measured
// elapsed to the threshold. Contention only ever ADDS time to a load, so the minimum across
// the cycles is the cleanest estimate of the intrinsic load cost -- to flake, all three
// cycles would have to be simultaneously contended. A genuine "heavy work back in DllMain"
// regression runs on EVERY DLL_PROCESS_ATTACH: UtinniCore does not self-pin (its DllMain
// neither starts the CLR nor spawns threads), so each cycle fully unloads + reloads +
// re-runs DllMain, inflating all three samples including the minimum -> still caught. 50 ms
// against a microsecond-scale body keeps an enormous margin; no realistic heavy-work
// regression squeaks under it.
//
// Full proof of "no deadlock under loader-lock contention" remains a Tier-4 manual
// verification per CONTEXT.md D-06 (inject UtinniCore.dll into a live SWG client).

namespace
{
constexpr double kThresholdMs = 50.0;
constexpr int kCycles = 3; // OPT-A: best-of-3

// One LoadLibraryA + FreeLibrary cycle. Returns the measured load time in ms, or
// a negative value if LoadLibraryA failed (caller maps that to exit code 2).
double measureLoadCycle()
{
    LARGE_INTEGER freq, start, end;
    QueryPerformanceFrequency(&freq);
    QueryPerformanceCounter(&start);

    HMODULE hDll = LoadLibraryA("UtinniCore.dll");

#ifdef LOADER_LOCK_HARNESS_REGRESSION_PROBE
    // T-06-04-01 regression probe (NOT compiled by default). Define this macro to
    // simulate a DllMain that got heavy: an artificial in-window delay inflates EVERY
    // cycle, so the min-of-3 must cross the 50 ms threshold and the harness must exit 1.
    // This proves OPT-A's best-of-3 minimum did not blunt the regression guard.
    // See 06-04-FLAKE-INVESTIGATION.md (Loader-Lock-Harness > Regression probe).
    Sleep(75);
#endif

    QueryPerformanceCounter(&end);

    if (hDll == nullptr)
    {
        return -1.0;
    }

    const double elapsedMs = (double)(end.QuadPart - start.QuadPart) * 1000.0 / (double)freq.QuadPart;
    FreeLibrary(hDll);
    return elapsedMs;
}
} // namespace

int main(int /*argc*/, char* /*argv*/[])
{
    double samples[kCycles];

    for (int i = 0; i < kCycles; ++i)
    {
        const double elapsedMs = measureLoadCycle();
        if (elapsedMs < 0.0)
        {
            std::fprintf(stderr, "[ERROR] LoadLibraryA(UtinniCore.dll) returned nullptr (GLE=%lu)\n", GetLastError());
            return 2;
        }
        samples[i] = elapsedMs;
        std::printf("UtinniCore DllMain cycle %d elapsed: %.3f ms\n", i + 1, elapsedMs);
    }

    // OPT-A: compare the MINIMUM sample to the threshold (06-04-FLAKE-INVESTIGATION.md).
    double minElapsedMs = samples[0];
    for (int i = 1; i < kCycles; ++i)
    {
        if (samples[i] < minElapsedMs)
        {
            minElapsedMs = samples[i];
        }
    }

    std::printf("UtinniCore DllMain elapsed: %.3f ms (min of %d samples)\n", minElapsedMs, kCycles);

    return (minElapsedMs < kThresholdMs) ? 0 : 1;
}
