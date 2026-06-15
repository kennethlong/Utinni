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
// Phase 18 / RNDR-01 / D-06 structural source gate: after the Plan-02 carve,
// imgui_impl.{cpp,h} must be FULLY DX9-API-neutral. This gate fails the build if
// any Direct3D / DX9-imgui-backend / device-facade symbol from the EXTENDED token
// set (review amendment 3) reappears in either file, AND it proves the carve
// actually wired the seam in by asserting render_backend::get()-> appears at
// least 5 times in imgui_impl.cpp (review amendment 8) -- absence of DX9 symbols
// plus presence of seam dispatch together prove a real carve, not empty stubs.
//
// HOW IT WORKS (mirrors NoDeviceResetTests.cpp's [resid04] gate): read each
// guarded source, STRIP comments (so the D-05 RATIONALE comments do NOT
// self-invalidate the gate -- the feedback_gsd_grep_gate_hygiene lesson), then
// count concrete symbol forms. The shared helpers live in SourceGateUtil.h; the
// proven [resid04] gate (NoDeviceResetTests.cpp) keeps its own file-local copies
// untouched (review amendment 10).
//
// WHAT IS DELIBERATELY NOT GATED (Pitfall 5): the BARE strings "D3D9" / "DX9".
// imgui_impl.cpp has live comments mentioning D3D9 / Reset (e.g. the no-Reset
// Present-stretch rationale); the gate keys on concrete symbol forms only.
// ============================================================================

#include <catch2/catch_all.hpp>

#include <string>

#include "SourceGateUtil.h"

using source_gate::countSubstr;
using source_gate::readStripped;
using source_gate::stripComments;

namespace
{
// The EXTENDED DX9 token set (review amendment 3). Each must count to ZERO in the
// comment-stripped imgui_impl.{cpp,h}.
const std::vector<std::string>& dx9Tokens()
{
    static const std::vector<std::string> tokens = {
        "#include <d3d9.h>",
        "#include \"d3d9.h\"",
        "#include <imgui_impl_dx9.h>",
        "imgui_impl_dx9",                 // the DX9 imgui backend header/symbol form
        "#include \"swg/graphics/directx9.h\"",
        "#include <swg/graphics/directx9.h>",
        "directx9.h",                     // any include form of the DX9 tier header
        "IDirect3DDevice9",
        "LPDIRECT3DDEVICE9",
        "LPDIRECT3D",
        "D3DDEVICE_CREATION_PARAMETERS",
        "ImGui_ImplDX9_",                 // prefix: _Init/_NewFrame/_RenderDrawData/_Invalidate/_Create
        "directX::",                      // the DX9 namespace reach-in (getDepthTexture/getDevice)
    };
    return tokens;
}
} // namespace

TEST_CASE("D-06 imgui_impl is fully DX9-API-neutral after the carve", "[rndr01][graphics]")
{
    SECTION("stripper hygiene self-check")
    {
        // Plant the gated tokens INSIDE comments and assert the stripped count is
        // 0 while the raw count is non-zero -- proves the stripper does real work,
        // so the gate cannot be a false `== 0` that merely never had a hit. Mirror
        // NoDeviceResetTests.cpp:174-188.
        const std::string sample =
            "int x = 0; // IDirect3DDevice9 in a line comment must be stripped\n"
            "/* block: #include <d3d9.h> and ImGui_ImplDX9_Init also stripped */\n"
            "int y = 1; // directX::getDevice() in a comment\n";
        REQUIRE(countSubstr(stripComments(sample), "IDirect3DDevice9") == 0);
        REQUIRE(countSubstr(stripComments(sample), "ImGui_ImplDX9_") == 0);
        REQUIRE(countSubstr(stripComments(sample), "directX::") == 0);
        // And the un-stripped sample DOES contain matches -> stripper is doing work.
        REQUIRE(countSubstr(sample, "IDirect3DDevice9") == 1);
        REQUIRE(countSubstr(sample, "ImGui_ImplDX9_") == 1);
        REQUIRE(countSubstr(sample, "directX::") == 1);
    }

    SECTION("imgui_impl.cpp contains zero DX9 symbols (extended token set)")
    {
        const std::string code = readStripped("UtinniCore/swg/ui/imgui_impl.cpp");
        for (const std::string& token : dx9Tokens())
        {
            INFO("DX9 symbol leaked into imgui_impl.cpp: " << token);
            REQUIRE(countSubstr(code, token) == 0);
        }
    }

    SECTION("imgui_impl.h contains zero DX9 symbols (extended token set)")
    {
        const std::string code = readStripped("UtinniCore/swg/ui/imgui_impl.h");
        for (const std::string& token : dx9Tokens())
        {
            INFO("DX9 symbol leaked into imgui_impl.h: " << token);
            REQUIRE(countSubstr(code, token) == 0);
        }
    }

    SECTION("seam dispatch present in imgui_impl.cpp (carve actually wired in)")
    {
        // Review amendment 8: prove the carve routed the former DX9 touch-points
        // through the seam, not merely deleted them. The five dispatch sites are
        // newFrame, renderDrawData, sceneDepthTexture, sceneColorTexture, and the
        // depth-stage get/set; the literal `render_backend::get()->` form appears
        // at the texture + stage accessor call sites.
        const std::string code = readStripped("UtinniCore/swg/ui/imgui_impl.cpp");
        REQUIRE(countSubstr(code, "render_backend::get()->") >= 5);
    }
}
