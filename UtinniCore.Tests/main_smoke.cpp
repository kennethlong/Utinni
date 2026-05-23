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

#include <catch2/catch_amalgamated.hpp>
#include "utility/string_utility.h"

#include <stdexcept>

TEST_CASE("Smoke: vendored Catch2 runs", "[smoke]")
{
    REQUIRE(1 + 1 == 2);
}

TEST_CASE("Smoke: exception machinery works", "[smoke]")
{
    REQUIRE_THROWS_AS(
        []() { throw std::runtime_error("boom"); }(),
        std::runtime_error);
}

TEST_CASE("Smoke: SECTION re-entry produces fresh state", "[smoke]")
{
    int counter = 0;

    SECTION("first section increments counter")
    {
        counter++;
        REQUIRE(counter == 1);
    }

    SECTION("second section sees a fresh counter (state did not leak)")
    {
        counter++;
        REQUIRE(counter == 1);
    }
}

TEST_CASE("Smoke: utility/string_utility.h include graph compiles", "[smoke]")
{
    REQUIRE(stringUtility::toBool("true") == true);
}
