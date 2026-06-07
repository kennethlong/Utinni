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

namespace utinni
{
class UTINNI_API DirectInput
{
public:
    static void suspend();
    static void resume();

    static void detour();

    // RESID-04 / D-12: runtime A/B toggle (default ON) for the exclusive-
    // fullscreen suppression in hkSetCooperativeLevel. When ON, an incoming
    // DISCL_EXCLUSIVE request is redirected to DISCL_NONEXCLUSIVE so SWG stays
    // windowed-embedded. Exposed so the maintainer can flip it live (15-08
    // smoke) without a rebuild, and so the deferred detached-fullscreen
    // fallback stays reachable if suppress proves wrong.
    static void setSuppressExclusiveFullscreen(bool enabled);
    static bool getSuppressExclusiveFullscreen();
};
} // namespace utinni