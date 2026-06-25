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

#include "repository.h"
#include "tree_file.h"

namespace utinni
{
static std::vector<std::string> filenames;
static std::map<std::string, Repository::DirectoryInfo> directories;

Repository::Repository()
{
    filenames = treefile::getAllFilenames();

    int startIndex = 0;
    int count = 0;
    std::string previousDir;

    for (int i = 0; i < filenames.size(); ++i)
    {
        std::string filename = filenames[i];
        std::string dir = filename.substr(0, filename.find_first_of('/'));

        if (previousDir != dir)
        {
            startIndex = i;
            count = 0;
        }
        previousDir = dir;

        count++;
        directories.insert_or_assign(dir, DirectoryInfo{startIndex, count});
    }
}

std::vector<std::string>* Repository::getAllFilenames()
{
    return &filenames;
}

int Repository::getFilenameCount()
{
    log::info(std::to_string(filenames.size()).c_str());
    return filenames.size();
}

std::string Repository::getFilenameAt(int index)
{
    // Phase 24 v4: bounds-safe. An empty/partial Repository (e.g. the advertised client before
    // the treefile subsystem is unlocked -> no filenames harvested) must degrade, not throw a
    // std::out_of_range across the native<-managed callback boundary (that surfaces as a fatal).
    if (index < 0 || static_cast<size_t>(index) >= filenames.size())
    {
        return std::string();
    }
    return filenames[index];
}

std::vector<std::string> Repository::getDirectoryFilenames(const char* directoryName)
{
    // Phase 24 v4: missing-directory safe. find() returning end() then dereferencing ->second
    // is UB (it bit the advertised client's empty Repository as an int3 in WorldSnapshot init);
    // return an empty list instead.
    const auto dirInfo = directories.find(directoryName);
    if (dirInfo == directories.end())
    {
        return std::vector<std::string>();
    }
    return std::vector<std::string>(&filenames[dirInfo->second.startIndex], &filenames[dirInfo->second.startIndex + dirInfo->second.size]);
}

Repository::DirectoryInfo* Repository::getDirectoryInfo(const char* directoryName)
{
    // Phase 24 v4: return nullptr for a missing directory instead of dereferencing end()->second
    // (the root cause of the advertised-client install-callback crash -- GroundSceneImpl queried
    // "terrain" on the empty Repository). Managed callers null-check the result.
    const auto dirInfo = directories.find(directoryName);
    if (dirInfo == directories.end())
    {
        return nullptr;
    }
    return &dirInfo->second;
}
} // namespace utinni
