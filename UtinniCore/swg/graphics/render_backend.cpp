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

// Phase 18 / RNDR-01: this TU is the DX9-FREE half of the seam (Option A split,
// 18-01 Task 2 decision). It holds ONLY the active-backend pointer and the
// get()/set() accessors -- it has ZERO Direct3D / imgui-backend / device-facade
// dependencies, so the D-07 mock-dispatch test can compile it DIRECTLY and link
// the dispatch contract device-free (the test references render_backend::set/get
// only; it never touches the Dx9Backend concrete type). The Dx9Backend method
// bodies, its static-storage singleton, and dx9Singleton() live in the sibling
// render_backend_dx9.cpp, which owns all the DX9/ImGui_ImplDX9 dependencies.
#include "render_backend.h"

namespace render_backend
{
// The single installed backend. NULLABLE: nullptr until setup() installs the
// Dx9 singleton (Plan 02) or a test installs a mock; Plan 02 guards every call
// site. Static storage, no heap (heap-free per-frame dispatch -- Pitfall 1).
static IRenderBackend* s_active = nullptr;

IRenderBackend* get()
{
    return s_active;
}

void set(IRenderBackend* backend)
{
    s_active = backend;
}
} // namespace render_backend
