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

#include "utini.h"

#include <algorithm>
#include <cctype>
#include <fstream>
#include <iterator>
#include <sstream>
#include <string>
#include <unordered_map>
#include <vector>

// CON-O-06 (Phase 6 Plan 06-03 Task 2): the legacy INI library was replaced with the hand-rolled
// INI parser below, living entirely inside UtINI::Impl so the public ABI in utini.h is
// unchanged (PIMPL boundary per CON-N-08; all 15+ callsites stay byte-compatible).
//
// Unlike the legacy INI library -- which stored sections/keys in std::map (alphabetical) and dropped
// blank lines + original formatting on Save -- this parser keeps a raw-line model
// (std::vector<Line>) so Save round-trip-preserves order, comments, blanks, and even
// malformed lines. Type coercion (getBool/getInt/getFloat) deliberately mirrors
// the legacy INI library's old AsBool/AsInt/AsDouble semantics so existing utinni.cfg values keep
// reading the same way (see the Impl coercion helpers + IniParserTests.cpp Task 3).

namespace utinni
{
const static UtINI::Value utinniSettings[] = {

    // Launcher settings
    {"Launcher", "swgClientName", "", UtINI::Value::vt_string},
    {"Launcher", "swgClientPath", "", UtINI::Value::vt_string},

    // UtinniCore settings
    {"UtinniCore", "enableInternalUi", "false", UtINI::Value::vt_bool},
    {"UtinniCore", "enableOfflineScenes", "false", UtINI::Value::vt_bool},
    {"UtinniCore", "useSwgOverrideCfg", "false", UtINI::Value::vt_bool},
    {"UtinniCore", "autoLoadScene", "false", UtINI::Value::vt_bool},
    {"UtinniCore", "autoLogin", "false", UtINI::Value::vt_bool},
    {"UtinniCore", "autoLoginUsername", "Local", UtINI::Value::vt_string},

    // Log settings
    {"Log", "writeClassName", "false", UtINI::Value::vt_bool},
    {"Log", "writeFunctionName", "false", UtINI::Value::vt_bool},

    // Editor settings
    {"Editor", "enableEditorMode", "true", UtINI::Value::vt_bool},
    {"Editor", "defaultPluginPanel", "Main Controls", UtINI::Value::vt_string},
    {"Editor", "autoOpenLogWindow", "false", UtINI::Value::vt_bool},
    {"Editor", "width", "1200", UtINI::Value::vt_int},
    {"Editor", "height", "500", UtINI::Value::vt_int},

};

namespace
{
// Whitespace-safe predicate; cast to unsigned char avoids the MSVC debug assert when
// std::isspace is handed a negative signed char (same hack the legacy INI library documented).
bool isWs(char c)
{
    return std::isspace(static_cast<unsigned char>(c)) != 0;
}

std::string trimCopy(const std::string& s)
{
    size_t b = 0;
    size_t e = s.size();
    while (b < e && isWs(s[b]))
    {
        ++b;
    }
    while (e > b && isWs(s[e - 1]))
    {
        --e;
    }
    return s.substr(b, e - b);
}

bool isCommentChar(char c)
{
    return c == ';' || c == '#';
}

// A bare value needs quoting on canonical re-emit if it would not parse back to itself:
// leading/trailing whitespace, an embedded comment char, or a leading quote.
bool needsQuoting(const std::string& v)
{
    if (v.empty())
    {
        return false;
    }
    if (isWs(v.front()) || isWs(v.back()))
    {
        return true;
    }
    if (v.front() == '"' || v.front() == '\'')
    {
        return true;
    }
    for (char c : v)
    {
        if (isCommentChar(c))
        {
            return true;
        }
    }
    return false;
}
} // namespace

// PIMPL body: the raw-line INI store + the hand-rolled parser, all private to this TU.
struct UtINI::Impl
{
    enum class LineKind
    {
        Blank,
        Comment,
        Section,
        Entry,
        Opaque
    };

    struct Line
    {
        LineKind kind = LineKind::Blank;
        std::string section;       // owning section (Entry) or section name (Section)
        std::string key;           // Entry key
        std::string value;         // Entry value, decoded (quotes stripped, trimmed)
        std::string inlineComment; // trailing ;/# comment on an Entry, delimiter excluded
        char commentChar = ';';    // which delimiter introduced the inline comment
        std::string raw;           // verbatim original text (no line terminator)
        bool dirty = false;        // Entry value was changed/created -> re-emit canonically
        bool superseded = false;   // earlier duplicate of a (section,key) -> not emitted
    };

    std::vector<Line> lines;
    // section -> key -> index of the live Line in `lines`
    std::unordered_map<std::string, std::unordered_map<std::string, size_t>> index;
    bool endsWithNewline = true;

    std::vector<Value> settings; // default-settings registry backing the public surface

    // ---- store API (member methods of Impl per CON-O-06 design) ----
    bool load(const std::string& filename);
    void save(const std::string& filename) const;
    bool hasValue(const std::string& section, const std::string& key) const;
    std::string getValue(const std::string& section, const std::string& key) const;
    void setValue(const std::string& section, const std::string& key, const std::string& value);
    void deleteSection(const std::string& section);

    // ---- legacy coercion helpers (mirror the legacy INI library AsBool/AsInt/AsDouble) ----
    static bool toBool(const std::string& v);
    static int toInt(const std::string& v);
    static double toDouble(const std::string& v);

private:
    void rebuildIndex();
    bool sectionExists(const std::string& section) const;
    size_t lastIndexOfSection(const std::string& section) const;
    size_t firstSectionHeaderIndex() const;
    static std::string canonicalEntry(const Line& ln);
};

// the legacy INI library's string_to_t<bool>: empty -> false; first char in {1,t,T,Y,y} -> true.
bool UtINI::Impl::toBool(const std::string& v)
{
    if (v.empty())
    {
        return false;
    }
    const char c = v[0];
    return c == '1' || c == 't' || c == 'T' || c == 'Y' || c == 'y';
}

// the legacy INI library's Get<int> (stringstream extraction). C++11 sets the target to 0 on failure,
// so empty/garbage -> 0 and "3.14" -> 3 (stops at the dot), matching the old behaviour.
int UtINI::Impl::toInt(const std::string& v)
{
    std::istringstream iss(v);
    int out = 0;
    iss >> out;
    return out;
}

double UtINI::Impl::toDouble(const std::string& v)
{
    std::istringstream iss(v);
    double out = 0.0;
    iss >> out;
    return out;
}

std::string UtINI::Impl::canonicalEntry(const Line& ln)
{
    std::string out = ln.key + " = ";
    if (needsQuoting(ln.value))
    {
        out += '"' + ln.value + '"';
    }
    else
    {
        out += ln.value;
    }
    if (!ln.inlineComment.empty())
    {
        out += ' ';
        out += ln.commentChar;
        out += ' ';
        out += ln.inlineComment;
    }
    return out;
}

bool UtINI::Impl::load(const std::string& filename)
{
    lines.clear();
    index.clear();
    endsWithNewline = true;

    std::ifstream file(filename.c_str(), std::ios::binary);
    if (!file.is_open())
    {
        return false;
    }

    const std::string content((std::istreambuf_iterator<char>(file)),
                              std::istreambuf_iterator<char>());
    file.close();

    endsWithNewline = content.empty() || content.back() == '\n';

    std::string currentSection;
    size_t pos = 0;
    while (pos <= content.size())
    {
        // Pull the next physical line (split on '\n', drop a trailing '\r' -> LF-normalised).
        size_t nl = content.find('\n', pos);
        bool lastSegment = (nl == std::string::npos);
        std::string rawLine = content.substr(pos, lastSegment ? std::string::npos : nl - pos);
        if (!rawLine.empty() && rawLine.back() == '\r')
        {
            rawLine.pop_back();
        }
        // A trailing '\n' produces an empty final segment we must not turn into a line.
        if (lastSegment && rawLine.empty() && content.size() > 0 && content.back() == '\n')
        {
            break;
        }

        Line ln;
        ln.raw = rawLine;
        const std::string t = trimCopy(rawLine);

        if (t.empty())
        {
            ln.kind = LineKind::Blank;
        }
        else if (isCommentChar(t[0]))
        {
            ln.kind = LineKind::Comment;
        }
        else if (t[0] == '[')
        {
            const size_t close = t.find(']');
            if (close == std::string::npos)
            {
                ln.kind = LineKind::Opaque; // malformed section header -> preserved verbatim
            }
            else
            {
                ln.kind = LineKind::Section;
                ln.section = trimCopy(t.substr(1, close - 1));
                currentSection = ln.section;
            }
        }
        else
        {
            const size_t eq = t.find('=');
            if (eq == std::string::npos)
            {
                ln.kind = LineKind::Opaque; // missing '=' -> preserved verbatim, not indexed
            }
            else
            {
                ln.kind = LineKind::Entry;
                ln.section = currentSection;
                ln.key = trimCopy(t.substr(0, eq));

                std::string vr = t.substr(eq + 1);
                // skip leading whitespace before inspecting for a quote
                size_t vs = 0;
                while (vs < vr.size() && isWs(vr[vs]))
                {
                    ++vs;
                }

                if (vs < vr.size() && (vr[vs] == '"' || vr[vs] == '\''))
                {
                    const char q = vr[vs];
                    const size_t closeQ = vr.find(q, vs + 1);
                    if (closeQ != std::string::npos)
                    {
                        // quoted: interior preserved exactly (not trimmed)
                        ln.value = vr.substr(vs + 1, closeQ - vs - 1);
                        const std::string rest = trimCopy(vr.substr(closeQ + 1));
                        if (!rest.empty() && isCommentChar(rest[0]))
                        {
                            ln.commentChar = rest[0];
                            ln.inlineComment = trimCopy(rest.substr(1));
                        }
                    }
                    else
                    {
                        // runaway quote: keep the remainder verbatim (quote included), no crash
                        ln.value = trimCopy(vr);
                    }
                }
                else
                {
                    // unquoted: a ';' or '#' starts an inline comment
                    size_t cpos = std::string::npos;
                    for (size_t i = 0; i < vr.size(); ++i)
                    {
                        if (isCommentChar(vr[i]))
                        {
                            cpos = i;
                            break;
                        }
                    }
                    if (cpos == std::string::npos)
                    {
                        ln.value = trimCopy(vr);
                    }
                    else
                    {
                        ln.value = trimCopy(vr.substr(0, cpos));
                        ln.commentChar = vr[cpos];
                        ln.inlineComment = trimCopy(vr.substr(cpos + 1));
                    }
                }
            }
        }

        lines.push_back(ln);

        if (lastSegment)
        {
            break;
        }
        pos = nl + 1;
    }

    rebuildIndex();
    return true;
}

void UtINI::Impl::save(const std::string& filename) const
{
    std::ofstream file(filename.c_str(), std::ios::binary);
    if (!file.is_open())
    {
        return;
    }

    std::vector<std::string> out;
    out.reserve(lines.size());
    for (const Line& ln : lines)
    {
        if (ln.superseded)
        {
            continue;
        }
        if (ln.kind == LineKind::Entry && ln.dirty)
        {
            out.push_back(canonicalEntry(ln));
        }
        else
        {
            out.push_back(ln.raw); // verbatim for Blank/Comment/Section/Opaque + untouched Entry
        }
    }

    for (size_t i = 0; i < out.size(); ++i)
    {
        file << out[i];
        const bool isLast = (i + 1 == out.size());
        if (!isLast || endsWithNewline)
        {
            file << '\n';
        }
    }
}

bool UtINI::Impl::hasValue(const std::string& section, const std::string& key) const
{
    auto sit = index.find(section);
    if (sit == index.end())
    {
        return false;
    }
    return sit->second.find(key) != sit->second.end();
}

std::string UtINI::Impl::getValue(const std::string& section, const std::string& key) const
{
    auto sit = index.find(section);
    if (sit == index.end())
    {
        return std::string();
    }
    auto kit = sit->second.find(key);
    if (kit == sit->second.end())
    {
        return std::string();
    }
    return lines[kit->second].value;
}

void UtINI::Impl::setValue(const std::string& section, const std::string& key, const std::string& value)
{
    // Update in place when the key already exists (last-write-wins slot).
    auto sit = index.find(section);
    if (sit != index.end())
    {
        auto kit = sit->second.find(key);
        if (kit != sit->second.end())
        {
            Line& ln = lines[kit->second];
            ln.value = value;
            ln.dirty = true;
            return;
        }
    }

    Line entry;
    entry.kind = LineKind::Entry;
    entry.section = section;
    entry.key = key;
    entry.value = value;
    entry.dirty = true;

    if (sectionExists(section))
    {
        const size_t at = lastIndexOfSection(section) + 1;
        lines.insert(lines.begin() + at, entry);
    }
    else if (section.empty())
    {
        // sectionless key must precede the first section header
        const size_t at = firstSectionHeaderIndex();
        lines.insert(lines.begin() + at, entry);
    }
    else
    {
        // brand-new named section appended at EOF, separated by a blank line for readability
        if (!lines.empty() && lines.back().kind != LineKind::Blank)
        {
            Line blank;
            blank.kind = LineKind::Blank;
            lines.push_back(blank);
        }
        Line header;
        header.kind = LineKind::Section;
        header.section = section;
        header.raw = "[" + section + "]";
        lines.push_back(header);
        lines.push_back(entry);
    }

    rebuildIndex();
}

void UtINI::Impl::deleteSection(const std::string& section)
{
    lines.erase(
        std::remove_if(lines.begin(), lines.end(),
                       [&](const Line& ln)
                       {
                           if (ln.kind == LineKind::Section)
                           {
                               return ln.section == section;
                           }
                           if (ln.kind == LineKind::Entry)
                           {
                               return ln.section == section;
                           }
                           return false;
                       }),
        lines.end());
    rebuildIndex();
}

void UtINI::Impl::rebuildIndex()
{
    index.clear();
    for (Line& ln : lines)
    {
        if (ln.kind == LineKind::Entry)
        {
            ln.superseded = false;
        }
    }
    for (size_t i = 0; i < lines.size(); ++i)
    {
        if (lines[i].kind != LineKind::Entry)
        {
            continue;
        }
        auto& keyMap = index[lines[i].section];
        auto it = keyMap.find(lines[i].key);
        if (it != keyMap.end())
        {
            lines[it->second].superseded = true; // earlier duplicate is dropped on save
        }
        keyMap[lines[i].key] = i; // last occurrence wins
    }
}

bool UtINI::Impl::sectionExists(const std::string& section) const
{
    for (const Line& ln : lines)
    {
        if (ln.kind == LineKind::Section && ln.section == section)
        {
            return true;
        }
        if (ln.kind == LineKind::Entry && ln.section == section)
        {
            return true;
        }
    }
    return false;
}

size_t UtINI::Impl::lastIndexOfSection(const std::string& section) const
{
    size_t last = 0;
    for (size_t i = 0; i < lines.size(); ++i)
    {
        const Line& ln = lines[i];
        if ((ln.kind == LineKind::Section && ln.section == section) ||
            (ln.kind == LineKind::Entry && ln.section == section))
        {
            last = i;
        }
    }
    return last;
}

size_t UtINI::Impl::firstSectionHeaderIndex() const
{
    for (size_t i = 0; i < lines.size(); ++i)
    {
        if (lines[i].kind == LineKind::Section)
        {
            return i;
        }
    }
    return lines.size();
}

UtINI::UtINI() : pImpl(new Impl) {}

UtINI::UtINI(const std::string& filename) : pImpl(new Impl)
{
    iniFilename = filename;
}

UtINI::~UtINI()
{
    delete pImpl;
}

void UtINI::load() const
{
    if (!pImpl->load(iniFilename))
    {
        // Save the default values to file
        for (const auto& iniValue : pImpl->settings)
        {
            pImpl->setValue(iniValue.sectionName, iniValue.valueName, iniValue.value);
        }

        save();
    }
    else
    {
        // Validate the loaded values to make sure all needed values exist, if not, create them
        bool missingValues = false;
        for (const auto& iniValue : pImpl->settings)
        {
            if (pImpl->getValue(iniValue.sectionName, iniValue.valueName).empty())
            {
                pImpl->setValue(iniValue.sectionName, iniValue.valueName, iniValue.value);
                missingValues = true;
            }
        }

        if (missingValues)
        {
            save();
        }
    }
}

void UtINI::load(const std::string& filename)
{
    iniFilename = filename;
    load();
}

void UtINI::save() const
{
    pImpl->save(iniFilename);
}

void UtINI::createUtinniSettings() const
{
    for (const Value& value : utinniSettings)
    {
        pImpl->settings.emplace_back(value);
    }
}

void UtINI::addSetting(const Value& value) const
{
    pImpl->settings.emplace_back(value);
}

void UtINI::addSetting(const char* sectionName, const char* valueName, const char* value, Value::Types type) const
{
    Value val = {sectionName, valueName, value, type};
    pImpl->settings.emplace_back(val);
}

void UtINI::DeleteSection(const char* sectionName)
{
    pImpl->deleteSection(sectionName);
}

std::string UtINI::getString(const char* sectionName, const char* valueName) const
{
    return pImpl->getValue(sectionName, valueName);
}

bool UtINI::getBool(const char* sectionName, const char* valueName) const
{
    return Impl::toBool(pImpl->getValue(sectionName, valueName));
}

int UtINI::getInt(const char* sectionName, const char* valueName) const
{
    return Impl::toInt(pImpl->getValue(sectionName, valueName));
}

float UtINI::getFloat(const char* sectionName, const char* valueName) const
{
    return static_cast<float>(Impl::toDouble(pImpl->getValue(sectionName, valueName)));
}

void UtINI::setString(const char* sectionName, const char* valueName, const char* value) const
{
    pImpl->setValue(sectionName, valueName, value);
}

void UtINI::setBool(const char* sectionName, const char* valueName, bool value) const
{
    pImpl->setValue(sectionName, valueName, value ? "true" : "false");
}

void UtINI::setInt(const char* sectionName, const char* valueName, int value) const
{
    pImpl->setValue(sectionName, valueName, std::to_string(value));
}

void UtINI::setFloat(const char* sectionName, const char* valueName, float value) const
{
    pImpl->setValue(sectionName, valueName, std::to_string(value));
}

} // namespace utinni
