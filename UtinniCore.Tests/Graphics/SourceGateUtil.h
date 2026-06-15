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

// ============================================================================
// Phase 18 / RNDR-01: shared source grep-gate helpers for the D-06 API-neutrality
// gate (ImguiApiNeutralityTests.cpp). These are lifted VERBATIM from
// NoDeviceResetTests.cpp:63-170 (repoRootFromThisFile / readFile / stripComments)
// plus a small countSubstr. They live in a `source_gate` namespace so they can be
// shared without an ODR clash.
//
// NOTE (review amendment 10): NoDeviceResetTests.cpp keeps its OWN file-local copies
// of these helpers untouched -- this header is used by the NEW D-06 gate ONLY, so
// the proven [resid04] gate has ZERO churn.
//
// repoRootFromThisFile() derives the repo root from THIS header's compile-time
// __FILE__, which is <repo>/UtinniCore.Tests/Graphics/SourceGateUtil.h, so the
// repo root is three path segments up -- identical to NoDeviceResetTests.cpp,
// which sits in the same Graphics/ folder.
// ============================================================================

#include <fstream>
#include <sstream>
#include <string>
#include <vector>

#include <catch2/catch_all.hpp> // REQUIRE in readFile

namespace source_gate
{
// Repo root derived from this header's compile-time path so the gate works
// regardless of the test exe's runtime cwd. __FILE__ is
//   <repo>/UtinniCore.Tests/Graphics/SourceGateUtil.h
// so the repo root is three path segments up from the file name.
inline std::string repoRootFromThisFile()
{
    std::string self = __FILE__;
    // Normalize backslashes to forward slashes for uniform splitting.
    for (char& c : self)
    {
        if (c == '\\')
            c = '/';
    }
    // Strip "/UtinniCore.Tests/Graphics/SourceGateUtil.h" (3 segments).
    for (int i = 0; i < 3; ++i)
    {
        const size_t slash = self.find_last_of('/');
        if (slash == std::string::npos)
            return self;
        self.erase(slash);
    }
    return self;
}

inline std::string readFile(const std::string& path)
{
    std::ifstream in(path, std::ios::binary);
    REQUIRE(in.good()); // file must be locatable for the gate to be meaningful
    std::ostringstream ss;
    ss << in.rdbuf();
    return ss.str();
}

// Strip comment lines so RATIONALE comments mentioning the gated symbols do not
// self-invalidate the gate (grep-gate hygiene). We remove:
//   - full-line `//` comments (after leading whitespace)
//   - the trailing `// ...` portion of any line
//   - lines inside `/* ... */` block comments
// The scan is STRING-LITERAL-AWARE (WR-02): a `//` or `/*` that appears inside a
// `"..."` or '...' literal is NOT treated as a comment start, so a real banned
// symbol sitting after an in-string `//` (e.g. `const char* u = "http://x";
// directX::getDevice();`) is preserved and can still trip the gate. Without this
// the stripper would truncate the line at the in-string `//`, silently discarding
// the trailing real symbol and letting it slip the gate (a false PASS). Escaped
// quotes (`\"`, `\'`) inside a literal do not end the literal.
inline std::string stripComments(const std::string& src)
{
    std::string out;
    out.reserve(src.size());
    bool inBlockComment = false;

    std::istringstream lines(src);
    std::string line;
    while (std::getline(lines, line))
    {
        std::string kept;
        bool inString = false; // inside a "..." literal
        bool inChar = false;   // inside a '...' literal
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

            // Inside a string/char literal: copy verbatim, only watching for the
            // (unescaped) closing delimiter. A `//` or `/*` here is literal text,
            // never a comment.
            if (inString || inChar)
            {
                kept.push_back(line[i]);
                if (line[i] == '\\' && i + 1 < line.size())
                {
                    // Escape sequence: copy the escaped char too so an escaped
                    // quote does not prematurely close the literal.
                    kept.push_back(line[i + 1]);
                    ++i;
                    continue;
                }
                if (inString && line[i] == '"')
                    inString = false;
                else if (inChar && line[i] == '\'')
                    inChar = false;
                continue;
            }

            // Not in a literal or comment: a comment can start here, OR a literal.
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
            if (line[i] == '"')
                inString = true;
            else if (line[i] == '\'')
                inChar = true;
            kept.push_back(line[i]);
        }
        out += kept;
        out.push_back('\n');
    }
    return out;
}

// Count non-overlapping occurrences of `needle` in `haystack`.
inline int countSubstr(const std::string& haystack, const std::string& needle)
{
    if (needle.empty())
        return 0;
    int count = 0;
    size_t pos = 0;
    while ((pos = haystack.find(needle, pos)) != std::string::npos)
    {
        ++count;
        pos += needle.size();
    }
    return count;
}

// Convenience: read a repo-relative source file with comments stripped.
inline std::string readStripped(const std::string& relPath)
{
    const std::string full = repoRootFromThisFile() + "/" + relPath;
    return stripComments(readFile(full));
}
} // namespace source_gate
