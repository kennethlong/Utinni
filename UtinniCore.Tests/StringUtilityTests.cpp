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

// D-06 max-harness compliance: each TEST_CASE below catches a specific
// PluginManager::loadPlugins regression. If any covered helper were reverted
// to the broken state in column 2, the corresponding TEST_CASE would FAIL.
//
// Per 05-REVIEWS.md item C2: this file contains 4 top-level TEST_CASEs
// covering 6 conceptual coverage cases via Catch2 SECTIONs. The count
// language is normalized everywhere to "4 top-level TEST_CASEs covering
// 6 conceptual cases via SECTIONs" — do not conflate the section count
// with the case count.
//
// | Test                                                | Failure caught                                                                                                                                                                              |
// |-----------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
// | toBool("true") == true                              | std::boolalpha regression -> every plugin treated as disabled                                                                                                                               |
// | toString(0, 2) == "00"                              | std::setfill('0') regression -> writes "plugin_ 0", next-startup read fails to find "plugin_00", drops every plugin from priority list                                                       |
// | toHexString(0xDEADBEEF, 8) == "deadbeef"            | std::hex regression -> writes decimal "3735928559" for every logged RVA; log lines become un-greppable                                                                                       |
// | trim(" hello ") == "hello"                          | find_first_not_of operator inversion -> trim(" hello ") returns " "; PluginManager's trim(directoryName) corrupts every plugin dir string                                                    |
//
// Source: 05-RESEARCH.md §"Code Examples > Failure modes each test catches" (lines 425-432).
// Consumer (PluginManager::loadPlugins): UtinniCore/plugin_framework/plugin_manager.cpp:121-138.

#include <catch2/catch_amalgamated.hpp>
#include "utility/string_utility.h"

TEST_CASE("stringUtility::toBool round-trip via boolalpha", "[utility][string]")
{
    SECTION("canonical true/false strings")
    {
        REQUIRE(stringUtility::toBool("true")  == true);
        REQUIRE(stringUtility::toBool("false") == false);
    }

    SECTION("case-sensitivity (std::boolalpha is case-sensitive)")
    {
        REQUIRE(stringUtility::toBool("True")  == false);
        REQUIRE(stringUtility::toBool("FALSE") == false);
    }

    SECTION("non-matching input defaults to false (istringstream fails silently)")
    {
        // Per 05-REVIEWS.md item C1, string_utility.h:46 initializes `bool result = false;`
        // so extraction failure deterministically returns false. This SECTION catches a
        // regression that drops the initializer (would re-introduce UB on extraction failure).
        REQUIRE(stringUtility::toBool("")        == false);
        REQUIRE(stringUtility::toBool("garbage") == false);
    }
}

TEST_CASE("stringUtility::toString(int, fillCount) zero-pads correctly", "[utility][string]")
{
    REQUIRE(stringUtility::toString(0,   2) == "00");
    REQUIRE(stringUtility::toString(7,   2) == "07");
    REQUIRE(stringUtility::toString(42,  2) == "42");
    REQUIRE(stringUtility::toString(100, 2) == "100");  // wider than fill — not truncated
}

TEST_CASE("stringUtility::toHexString lowercase hex with zero padding", "[utility][string]")
{
    REQUIRE(stringUtility::toHexString(0,    4) == "0000");
    REQUIRE(stringUtility::toHexString(0xAB, 4) == "00ab");
    REQUIRE(stringUtility::toHexString(static_cast<uint32_t>(0xDEADBEEF), 8) == "deadbeef");
}

TEST_CASE("stringUtility::trim / trimStart / trimEnd strip default whitespace", "[utility][string]")
{
    SECTION("trim strips both ends")
    {
        std::string s = "  hello  ";
        REQUIRE(stringUtility::trim(s) == "hello");
        REQUIRE(s == "hello");  // in-place mutation
    }

    SECTION("trimStart strips only leading")
    {
        std::string s = "  hello  ";
        REQUIRE(stringUtility::trimStart(s) == "hello  ");
    }

    SECTION("trimEnd strips only trailing")
    {
        std::string s = "  hello  ";
        REQUIRE(stringUtility::trimEnd(s) == "  hello");
    }

    SECTION("PluginManager [Plugins] line idiom: trim then toBool")
    {
        std::string isEnabled = " true ";
        REQUIRE(stringUtility::toBool(stringUtility::trim(isEnabled)) == true);

        std::string dirName = "  MyPlugin  ";
        REQUIRE(stringUtility::trim(dirName) == "MyPlugin");
    }
}

// Total: 4 top-level TEST_CASEs / 6 conceptual coverage cases via SECTIONs
// (toBool 3 sections, toString 1 case, toHexString 1 case, trim 4 sections grouped as
// 1 conceptual case + PluginManager idiom). Per 05-REVIEWS.md item C2.
