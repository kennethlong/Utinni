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

// CON-O-06 / D-03 max-harness fence (Phase 6 Plan 06-03 Task 3): regression suite for
// the hand-rolled INI parser inside UtINI::Impl that replaced LeksysINI. Each TEST_CASE
// below pins one behavioural contract; if a future cleanup reverts the parser to a broken
// state, the named TEST_CASE + its documenting SECTION identifies the regression class
// (see [[feedback-max-harness]]). The suite drives only the public UtINI ABI in
// UtINI/utini.h -- the Impl/IniStore internals stay private behind the PIMPL boundary.
//
// Exercised entirely through fresh temp files under std::filesystem::temp_directory_path()
// written in binary (LF) so byte-equal round-trip assertions are platform-deterministic.
//
// Convention under test: sectionless keys (those before the first [Section]) live under the
// empty-string section name, retrieved via getString("", key).

#include <catch2/catch_all.hpp>

#include <atomic>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <string>
#include <system_error>

#include "UtINI/utini.h"

namespace
{
// RAII temp .ini file: written once in binary (no CRLF translation), removed on scope exit.
struct TempIni
{
    std::filesystem::path path;

    explicit TempIni(const std::string& content)
    {
        static std::atomic<int> counter{ 0 };
        path = std::filesystem::temp_directory_path() /
               ("utinni_ini_test_" + std::to_string(++counter) + ".ini");
        std::ofstream f(path, std::ios::binary | std::ios::trunc);
        f << content;
    }

    ~TempIni()
    {
        std::error_code ec;
        std::filesystem::remove(path, ec);
    }

    std::string str() const { return path.string(); }
};

std::string readAll(const std::string& p)
{
    std::ifstream f(p, std::ios::binary);
    return std::string((std::istreambuf_iterator<char>(f)), std::istreambuf_iterator<char>());
}
} // namespace

TEST_CASE("INI parser: round-trip preserves unrelated lines byte-for-byte", "[utini][ini]")
{
    SECTION("mutating one value rewrites only that line; comments/sections/keys stay byte-equal")
    {
        // Regression caught: a parser that re-sorts sections/keys (LeksysINI behaviour) or
        // drops blank lines/comments on Save would change the unrelated bytes here.
        const std::string original =
            "; top comment\n"
            "[Editor]\n"
            "width = 1200\n"
            "height = 500\n"
            "\n"
            "[Log]\n"
            "writeClassName = false\n";

        TempIni tmp(original);
        utinni::UtINI ini(tmp.str());
        ini.load();
        REQUIRE(ini.getInt("Editor", "width") == 1200);

        ini.setInt("Editor", "width", 1920);
        ini.save();

        const std::string saved = readAll(tmp.str());
        std::string expected = original;
        expected.replace(expected.find("width = 1200"), std::string("width = 1200").size(), "width = 1920");
        REQUIRE(saved == expected);

        utinni::UtINI reloaded(tmp.str());
        reloaded.load();
        REQUIRE(reloaded.getInt("Editor", "width") == 1920);
        REQUIRE(reloaded.getInt("Editor", "height") == 500);
        REQUIRE(reloaded.getString("Log", "writeClassName") == "false");
    }
}

TEST_CASE("INI parser: quoted value with embedded '=' is decoded and round-trips", "[utini][ini]")
{
    SECTION("split-on-first-'=' must not truncate a quoted value containing more '='")
    {
        const std::string original =
            "[Conn]\n"
            "token = \"a=b=c\"\n";

        TempIni tmp(original);
        utinni::UtINI ini(tmp.str());
        ini.load();
        REQUIRE(ini.getString("Conn", "token") == "a=b=c");

        ini.setString("Conn", "token", "x=y=z");
        ini.save();

        utinni::UtINI reloaded(tmp.str());
        reloaded.load();
        REQUIRE(reloaded.getString("Conn", "token") == "x=y=z");
    }
}

TEST_CASE("INI parser: sectionless keys live under the empty-string section", "[utini][ini]")
{
    SECTION("keys appearing before any [Section] are retrieved via getString(\"\", key)")
    {
        const std::string original =
            "globalKey = globalVal\n"
            "[S]\n"
            "k = v\n";

        TempIni tmp(original);
        utinni::UtINI ini(tmp.str());
        ini.load();
        REQUIRE(ini.getString("", "globalKey") == "globalVal");
        REQUIRE(ini.getString("S", "k") == "v");
    }
}

TEST_CASE("INI parser: semicolon comment round-trips byte-equal", "[utini][ini]")
{
    SECTION("a ';' full-line comment survives load->save unchanged")
    {
        const std::string original =
            "; this is a semicolon comment\n"
            "[Section]\n"
            "key = value\n";

        TempIni tmp(original);
        utinni::UtINI ini(tmp.str());
        ini.load();
        ini.save();
        REQUIRE(readAll(tmp.str()) == original);
    }
}

TEST_CASE("INI parser: hash comment round-trips byte-equal", "[utini][ini]")
{
    SECTION("a '#' full-line comment survives load->save unchanged")
    {
        const std::string original =
            "# this is a hash comment\n"
            "[Section]\n"
            "key = value\n";

        TempIni tmp(original);
        utinni::UtINI ini(tmp.str());
        ini.load();
        ini.save();
        REQUIRE(readAll(tmp.str()) == original);
    }
}

TEST_CASE("INI parser: inline comment is preserved on save", "[utini][ini]")
{
    SECTION("a trailing ';' inline comment is kept when its entry is rewritten")
    {
        const std::string original =
            "[S]\n"
            "key = value ; trailing comment\n";

        TempIni tmp(original);
        utinni::UtINI ini(tmp.str());
        ini.load();
        REQUIRE(ini.getString("S", "key") == "value"); // value excludes the inline comment

        ini.setString("S", "key", "newval");
        ini.save();

        const std::string saved = readAll(tmp.str());
        REQUIRE(saved.find("; trailing comment") != std::string::npos);

        utinni::UtINI reloaded(tmp.str());
        reloaded.load();
        REQUIRE(reloaded.getString("S", "key") == "newval");
    }
}

TEST_CASE("INI parser: empty value reads as empty string and round-trips", "[utini][ini]")
{
    SECTION("'key =' yields \"\"; setString(\"\") round-trips as empty")
    {
        const std::string original =
            "[S]\n"
            "blank =\n"
            "filled = something\n";

        TempIni tmp(original);
        utinni::UtINI ini(tmp.str());
        ini.load();
        REQUIRE(ini.getString("S", "blank") == "");
        REQUIRE(ini.getString("S", "filled") == "something");

        ini.setString("S", "filled", "");
        ini.save();

        utinni::UtINI reloaded(tmp.str());
        reloaded.load();
        REQUIRE(reloaded.getString("S", "filled") == "");
    }
}

TEST_CASE("INI parser: repeated keys are last-write-wins; earlier duplicate dropped on save", "[utini][ini]")
{
    SECTION("two '[A] key=...' lines: getString returns the second; save emits only one")
    {
        const std::string original =
            "[A]\n"
            "key = x\n"
            "key = y\n";

        TempIni tmp(original);
        utinni::UtINI ini(tmp.str());
        ini.load();
        REQUIRE(ini.getString("A", "key") == "y"); // last occurrence wins

        ini.save();
        const std::string saved = readAll(tmp.str());
        REQUIRE(saved.find("key = y") != std::string::npos);
        REQUIRE(saved.find("key = x") == std::string::npos); // earlier duplicate is dropped
    }
}

TEST_CASE("INI parser: malformed section header is preserved opaquely (no crash)", "[utini][ini]")
{
    SECTION("'[NotClosed' (missing ']') is kept verbatim and does not start a section")
    {
        const std::string original =
            "[Good]\n"
            "k = v\n"
            "[NotClosed\n"
            "stray = 1\n";

        TempIni tmp(original);
        utinni::UtINI ini(tmp.str());
        ini.load();   // must not crash
        ini.save();
        const std::string saved = readAll(tmp.str());
        REQUIRE(saved.find("[NotClosed") != std::string::npos); // preserved opaquely
    }
}

TEST_CASE("INI parser: line missing '=' is preserved opaquely (no crash)", "[utini][ini]")
{
    SECTION("a non-comment non-section line without '=' round-trips intact and is not indexed")
    {
        const std::string original =
            "[S]\n"
            "keywithoutequals\n"
            "real = 1\n";

        TempIni tmp(original);
        utinni::UtINI ini(tmp.str());
        ini.load();   // must not crash
        REQUIRE(ini.getString("S", "real") == "1");
        ini.save();
        REQUIRE(readAll(tmp.str()).find("keywithoutequals") != std::string::npos);
    }
}

TEST_CASE("INI parser: runaway quote does not crash and round-trips", "[utini][ini]")
{
    SECTION("'key = \"unterminated' keeps the remainder verbatim (quote included) as the value")
    {
        // Documented behaviour: an unterminated quote is treated as include-the-rest-of-line,
        // so the leading quote stays in the decoded value and the raw line round-trips on save.
        const std::string original =
            "[S]\n"
            "key = \"unterminated\n";

        TempIni tmp(original);
        utinni::UtINI ini(tmp.str());
        ini.load();   // must not crash
        REQUIRE(ini.getString("S", "key") == "\"unterminated");
        ini.save();
        REQUIRE(readAll(tmp.str()).find("key = \"unterminated") != std::string::npos);
    }
}

TEST_CASE("INI parser: type coercion matches the legacy AsBool/AsInt/AsDouble semantics", "[utini][ini]")
{
    SECTION("bool: first char in {1,t,T,Y,y} is true; everything else (incl. FALSE) is false")
    {
        const std::string original =
            "[T]\n"
            "b_true = true\n"
            "b_one = 1\n"
            "b_yes = yes\n"
            "b_false = FALSE\n"
            "b_zero = 0\n"
            "b_empty =\n"
            "n = 42\n"
            "trunc = 3.14\n"
            "f = 3.14\n";

        TempIni tmp(original);
        utinni::UtINI ini(tmp.str());
        ini.load();

        REQUIRE(ini.getBool("T", "b_true") == true);
        REQUIRE(ini.getBool("T", "b_one") == true);
        REQUIRE(ini.getBool("T", "b_yes") == true);
        REQUIRE(ini.getBool("T", "b_false") == false); // 'F' is NOT in the true-set
        REQUIRE(ini.getBool("T", "b_zero") == false);
        REQUIRE(ini.getBool("T", "b_empty") == false);

        REQUIRE(ini.getInt("T", "n") == 42);
        REQUIRE(ini.getInt("T", "trunc") == 3);        // stringstream int extraction stops at '.'
        REQUIRE(ini.getInt("T", "missing") == 0);      // missing key coerces to 0
        REQUIRE(ini.getFloat("T", "f") == Catch::Approx(3.14f));
    }
}
