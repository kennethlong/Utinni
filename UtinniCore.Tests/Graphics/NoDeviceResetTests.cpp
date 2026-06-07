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
// RESID-04 / D-13 regression gate: Utinni must NEVER INITIATE a device Reset
// on SWG's third-party IDirect3DDevice9.
//
// Rationale (auto-memory feedback_d3d9_reset_third_party + STATE Issue #10):
// SWG owns dozens of default-pool resources Utinni cannot enumerate. Calling
// pDevice->Reset(...) returns D3DERR_INVALIDCALL, the device goes DEVICELOST,
// and the next SetVertexShaderConstantF crashes the process (live-verified).
// The embedded-window resize/fullscreen story (RESID-04, D-12) is therefore
// solved WITHOUT a device Reset: suppress the exclusive-fullscreen switch at
// the DirectInput level and let the windowed COPY Present self-stretch the
// backbuffer onto the resized client rect. This gate fails the build if any
// RESID-04-touched source reintroduces a Utinni-initiated device Reset.
//
// HOW IT WORKS (source grep-gate):
//   For each guarded source file, read it, STRIP comment lines (so the no-Reset
//   RATIONALE comments do NOT self-invalidate the gate -- the
//   feedback_gsd_grep_gate_hygiene lesson; a bare unfiltered "== 0" is
//   forbidden), then count device-method Reset INVOCATIONS of the shape
//   `->Reset(` or `.Reset(`. Assert the count is ZERO.
//
// WHAT IS LEGITIMATELY EXCLUDED:
//   - directx9.cpp hkReset's pass-through `reset(pDevice, pPresentationParameters)`
//     is a FREE-FUNCTION call through the captured original device vtable pointer
//     (named `reset`, lowercase) -- it is SWG's OWN Reset flowing through our
//     detour, NOT a Utinni-initiated one. It does not match `->Reset(`/`.Reset(`,
//     so it is naturally excluded by the invocation-shape pattern.
//   - Tokens like `ResetEvent(`, `manual-reset`, `d3di_Reset_Index`,
//     `D3DERR_INVALIDCALL` never match `->Reset(`/`.Reset(`.
// ============================================================================

#include <catch2/catch_all.hpp>

#include <fstream>
#include <sstream>
#include <string>
#include <vector>

namespace
{
// Repo root derived from this test file's compile-time path so the gate works
// regardless of the test exe's runtime cwd. __FILE__ is
//   <repo>/UtinniCore.Tests/Graphics/NoDeviceResetTests.cpp
// so the repo root is three path segments up from the file name.
std::string repoRootFromThisFile()
{
    std::string self = __FILE__;
    // Normalize backslashes to forward slashes for uniform splitting.
    for (char& c : self)
    {
        if (c == '\\')
            c = '/';
    }
    // Strip "/UtinniCore.Tests/Graphics/NoDeviceResetTests.cpp" (3 segments).
    for (int i = 0; i < 3; ++i)
    {
        const size_t slash = self.find_last_of('/');
        if (slash == std::string::npos)
            return self;
        self.erase(slash);
    }
    return self;
}

std::string readFile(const std::string& path)
{
    std::ifstream in(path, std::ios::binary);
    REQUIRE(in.good()); // file must be locatable for the gate to be meaningful
    std::ostringstream ss;
    ss << in.rdbuf();
    return ss.str();
}

// Strip comment lines so RATIONALE comments mentioning Reset do not
// self-invalidate the gate (grep-gate hygiene). We remove:
//   - full-line `//` comments (after leading whitespace)
//   - the trailing `// ...` portion of any line
//   - lines inside `/* ... */` block comments
// This is intentionally conservative: it errs toward stripping, which can only
// make the gate STRICTER (it never hides a real Reset call, because a real
// `pDevice->Reset(...)` statement is code, not a comment).
std::string stripComments(const std::string& src)
{
    std::string out;
    out.reserve(src.size());
    bool inBlockComment = false;

    std::istringstream lines(src);
    std::string line;
    while (std::getline(lines, line))
    {
        std::string kept;
        for (size_t i = 0; i < line.size(); ++i)
        {
            if (inBlockComment)
            {
                if (i + 1 < line.size() && line[i] == '*' && line[i + 1] == '/')
                {
                    inBlockComment = false;
                    ++i; // consume '/'
                }
                continue;
            }

            if (i + 1 < line.size() && line[i] == '/' && line[i + 1] == '/')
            {
                break; // rest of line is a line comment
            }
            if (i + 1 < line.size() && line[i] == '/' && line[i + 1] == '*')
            {
                inBlockComment = true;
                ++i; // consume '*'
                continue;
            }
            kept.push_back(line[i]);
        }
        out += kept;
        out.push_back('\n');
    }
    return out;
}

// Count device-method Reset invocations of the shape `->Reset(` or `.Reset(`.
int countDeviceResetInvocations(const std::string& codeNoComments)
{
    int count = 0;
    const std::vector<std::string> needles = {"->Reset(", ".Reset("};
    for (const std::string& needle : needles)
    {
        size_t pos = 0;
        while ((pos = codeNoComments.find(needle, pos)) != std::string::npos)
        {
            ++count;
            pos += needle.size();
        }
    }
    return count;
}

int guardedResetCount(const std::string& relPath)
{
    const std::string full = repoRootFromThisFile() + "/" + relPath;
    const std::string code = stripComments(readFile(full));
    return countDeviceResetInvocations(code);
}
} // namespace

TEST_CASE("D-13 no Utinni-initiated device Reset in RESID-04-touched sources", "[resid04][graphics]")
{
    SECTION("comment-stripping is active (gate hygiene self-check)")
    {
        // A bare unfiltered count would be tripped by the legitimate no-Reset
        // RATIONALE comments in these files. Prove the stripper removes a
        // `.Reset(`-shaped token that appears ONLY inside a comment, so the
        // gate cannot be a false `== 0` that merely never had a comment hit.
        const std::string sample =
            "int x = 0; // device->Reset( in a comment must be stripped\n"
            "/* block: ptr.Reset( also stripped */\n"
            "int y = 1;\n";
        REQUIRE(countDeviceResetInvocations(stripComments(sample)) == 0);
        // And the un-stripped sample DOES contain matches -> proves the
        // stripper is doing real work, not trivially passing.
        REQUIRE(countDeviceResetInvocations(sample) == 2);
    }

    SECTION("directx9.cpp has no Utinni-initiated device Reset")
    {
        // The only legitimate Reset is hkReset's free-function pass-through
        // `reset(pDevice, ...)` of SWG's OWN Reset, which is not a
        // `->Reset(`/`.Reset(` invocation.
        REQUIRE(guardedResetCount("UtinniCore/swg/graphics/directx9.cpp") == 0);
    }

    SECTION("direct_input.cpp (RESID-04 suppress fix) has no device Reset")
    {
        REQUIRE(guardedResetCount("UtinniCore/swg/misc/direct_input.cpp") == 0);
    }

    SECTION("PanelGame.cs (resize path) has no device Reset")
    {
        REQUIRE(guardedResetCount("UtinniCoreDotNet/UI/Controls/PanelGame.cs") == 0);
    }
}
