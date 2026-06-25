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

#include "tree_file.h"
#include "swg/endpoints.h"
#include <set>

namespace swg::treefile
{
using pSearchTree = swgptr(__thiscall*)(swgptr pThis, int priority, const char* treeFilename);

pSearchTree searchTree = (pSearchTree)0xA992E0;

// Phase 24 v4: the v3 "treeFile::open" row mapped the static file-open by mistake (a name
// collision with this __thiscall search-path registration). The provider resolved it with a
// distinct treeFile::searchTree -> &TreeFile::addSearchTree, a STATIC __cdecl(const char*
// fileName, int priority) -- NO pThis, and the args are REVERSED vs the SWGEmu __thiscall
// searchTree above. Advertised-only: null on SWGEmu (the __thiscall literal drives the Pre-CU
// path). The advertised-client hook rewrite to this ABI is the smoke-gated treefile unlock.
using pAddSearchTree = void(__cdecl*)(const char* fileName, int priority);
pAddSearchTree addSearchTree = nullptr;

// Phase 24 v5: provider advertises treeFile::enumerateFiles -> &TreeFile::enumerateFiles, a STATIC
// __cdecl that invokes a callback once per filename across all registered TRE/TOC search nodes (the
// engine's full file union). Utinni calls it once when the Repository is built on the advertised
// client to populate its file index, replacing the hkSearchTree harvest (which never runs there).
// Advertised-only: null on SWGEmu (the hook drives the Pre-CU path). NOTE the callback fires WHILE
// TreeFile's critical section is held -- the callback must only allocate/append, NEVER re-enter
// TreeFile (no open/exists/addSearchTree/...).
using pEnumerateFiles = void(__cdecl*)(void(__cdecl* cb)(const char* fileName, void* ctx), void* ctx);
pEnumerateFiles enumerateFiles = nullptr;
} // namespace swg::treefile

namespace utinni::treefile
{
static std::set<std::string> filenames;

// Callback for swg::treefile::enumerateFiles (advertised client). Append each engine-relative
// filename into the file set. Allocation-only (std::set::emplace) -- safe to run under TreeFile's
// held critical section per the provider's re-entrancy contract; does NOT call back into TreeFile.
static void __cdecl enumerateFilesCallback(const char* fileName, void* ctx)
{
    if (fileName != nullptr && ctx != nullptr)
    {
        static_cast<std::set<std::string>*>(ctx)->emplace(fileName);
    }
}

std::vector<std::string> getAllFilenames()
{
    // Phase 24 v5: on the advertised client hkSearchTree never installs (treefile subsystem skipped),
    // so the harvested set is empty. If the provider advertised treeFile::enumerateFiles, populate the
    // set from the engine's full TRE/TOC union now. Null slot (SWGEmu / Pre-CU) -> the hook-harvested
    // set is used unchanged (D-00). std::set dedups + sorts, so the Repository's by-directory grouping
    // (consecutive same-dir entries) holds regardless of the engine's yield order.
    if (filenames.empty() && swg::treefile::enumerateFiles != nullptr)
    {
        swg::treefile::enumerateFiles(&enumerateFilesCallback, &filenames);
    }

    std::vector<std::string> result;
    result.reserve(filenames.size());
    for (auto it = filenames.begin(); it != filenames.end();)
    {
        result.push_back(std::move(filenames.extract(it++).value()));
    }
    filenames.clear();
    return result;
}

swgptr __fastcall hkSearchTree(swgptr pThis, DWORD EDX, int priority, const char* treeFilename)
{
    swg::treefile::searchTree(pThis, priority, treeFilename);

    const int fileCount = memory::read<int>(pThis + 0x14);
    char* filenamesBuffer = memory::read<char*>(pThis + 0x18);

    uint32_t offset = 0;
    for (int i = 0; i < fileCount; ++i)
    {
        std::string filename = std::string(filenamesBuffer + offset);
        offset += filename.length() + 1;

        filenames.emplace(filename);
    }

    return pThis;
}

void detour()
{
    // Phase 24: treeFile::open resolves on the advertised client, but the provider symbol
    // is __cdecl while this hook (hkSearchTree) is __thiscall (24-02 flagged ABI mismatch).
    // Installing it corrupts the stack when the engine calls TreeFile::open during login
    // asset loading -> 0xC0000096 privileged-instruction crash in game code. The primary is
    // mapped, so installable() can't catch this; skip the whole hook on the advertised client
    // until the provider advertises an ABI-compatible symbol. No-op on SWGEmu (D-00).
    if (swg::endpoints::isAdvertisedClient())
        return;

    swg::treefile::searchTree = (swg::treefile::pSearchTree)Detour::Create(swg::treefile::searchTree, hkSearchTree, DETOUR_TYPE_PUSH_RET);
}
} // namespace utinni::treefile
