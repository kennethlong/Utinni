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

#include "world_snapshot.h"
#include <cstring>
#include <filesystem>
#include "ground_scene.h"
#include "swg/appearance/appearance.h"
#include "swg/misc/network.h"
#include "swg/object/object.h"
#include "swg/object/client_object.h"
#include "swg/game/game.h"
#include "swg/appearance/portal.h"
#include "swg/endpoints.h"
#include "utility/string_utility.h"

namespace swg::worldSnapshotReaderWriter
{
using pOpenFile = bool(__thiscall*)(utinni::WorldSnapshotReaderWriter* pThis, const char* filename);
using pSaveFile = bool(__thiscall*)(utinni::WorldSnapshotReaderWriter* pThis, const char* filename);
using pClear = void(__thiscall*)(utinni::WorldSnapshotReaderWriter* pThis);

using pGetObjectTemplateName = const char*(__thiscall*)(utinni::WorldSnapshotReaderWriter * pThis, int objectTemplateNameIndex);

using pNodeCount = int(__thiscall*)(utinni::WorldSnapshotReaderWriter* pThis);
using pNodeCountTotal = int(__thiscall*)(utinni::WorldSnapshotReaderWriter* pThis);

using pGetNodeByNetworkId = utinni::WorldSnapshotReaderWriter::Node*(__thiscall*)(utinni::WorldSnapshotReaderWriter * pThis, swgptr networkId);
using pGetNodeByIndex = utinni::WorldSnapshotReaderWriter::Node*(__thiscall*)(utinni::WorldSnapshotReaderWriter * pThis, int nodeId);
using pAddNode = swgptr(__thiscall*)(utinni::WorldSnapshotReaderWriter* pThis, int nodeId, int parentNodeId, const utinni::CrcString& objectFilenameCrcString, int cellId, const swg::math::Transform& transform, float radius, unsigned int pobCrc);
using pRemoveNode = void(__thiscall*)(utinni::WorldSnapshotReaderWriter* pThis, int nodeId);

pOpenFile openFile = (pOpenFile)0x00B97D90;
pSaveFile saveFile = (pSaveFile)0x00B98120;
pClear clear = (pClear)0x00B98290;

pGetObjectTemplateName getObjectTemplateName = (pGetObjectTemplateName)0x00B98720;

pNodeCount nodeCount = (pNodeCount)0x00B986A0;
pNodeCountTotal nodeCountTotal = (pNodeCountTotal)0x00B986D0;

pGetNodeByNetworkId getNodeByNetworkId = (pGetNodeByNetworkId)0x00B98740;
pGetNodeByIndex getNodeByIndex = (pGetNodeByIndex)0x00B986B0;
pAddNode addNode = (pAddNode)0x00B98410;
pRemoveNode removeNode = (pRemoveNode)0x00B98780;

namespace node
{
using pGetNodeNetworkId = int(__thiscall*)(utinni::WorldSnapshotReaderWriter::Node* pThis);
using pGetNodeSpatialSubdivisionHandle = swgptr(__thiscall*)(utinni::WorldSnapshotReaderWriter::Node* pThis);
using pSetNodeSpatialSubdivisionHandle = void(__thiscall*)(utinni::WorldSnapshotReaderWriter::Node* pThis, swgptr handle);

using pRemoveFromWorld = void(__thiscall*)(utinni::WorldSnapshotReaderWriter::Node* pThis);

pGetNodeNetworkId getNodeNetworkId = (pGetNodeNetworkId)0x00B971D0;
pGetNodeSpatialSubdivisionHandle getNodeSpatialSubdivisionHandle = (pGetNodeSpatialSubdivisionHandle)0x00B97390;
pSetNodeSpatialSubdivisionHandle setNodeSpatialSubdivisionHandle = (pSetNodeSpatialSubdivisionHandle)0x00B973A0;

pRemoveFromWorld removeFromWorld = (pRemoveFromWorld)0x00B97440;

} // namespace node
} // namespace swg::worldSnapshotReaderWriter

namespace swg::worldsnapshot
{
using pLoad = void(__cdecl*)(const char* name);
using pUnload = void(__cdecl*)();

using pClearPreloadList = char(__cdecl*)(swgptr, swgptr, swgptr);

using pCreateObject = swgptr(__cdecl*)(utinni::WorldSnapshotReaderWriter* reader, utinni::WorldSnapshotReaderWriter::Node* node, swgptr result);
using pAddObject = void(__cdecl*)(swgptr object, swgptr node);

using pDetailLevelChanged = void(__cdecl*)();

pLoad load = (pLoad)0x0059C380;
pUnload unload = (pUnload)0x0059C1D0;

pClearPreloadList clearPreloadList = (pClearPreloadList)0x00404D50;

pCreateObject createObject = (pCreateObject)0x0059BBA0;
pAddObject addObject = (pAddObject)0x0059BF20;

pDetailLevelChanged detailLevelChanged = (pDetailLevelChanged)0x0059DC30;

// Phase 24 / D-01 full-catalog rows the consumer did not previously hook. The
// provider advertises these static WorldSnapshot members (removeObject, moveObject,
// getLoadingPercent); the consumer had no literal for them. They have NO SWGEmu RVA
// (no existing consumer call-site), so the slot starts null and resolves only on the
// advertised client -- bound for catalog completeness (D-01). Inert on SWGEmu.
using pRemoveObject = void(__cdecl*)(swgptr networkId);
using pMoveObject = void(__cdecl*)(swgptr networkId, const swg::math::Transform& transform);
using pGetLoadingPercent = int(__cdecl*)();

pRemoveObject removeObject = nullptr;
pMoveObject moveObject = nullptr;
pGetLoadingPercent getLoadingPercent = nullptr;

// v17 (Goal B Wave 1): id-keyed READ shims over the provider's live snapshot reader
// (rev-3 frozen row table, 24-PROVIDER-REQUEST-goalB-wave1-rows.md). No SWGEmu RVA
// exists for any of these (the SWGEmu editor path stays on the raw Node* walks below),
// so every slot starts null and resolves only on the advertised client. All are
// game-thread-only; the node reads force-finish a pending incremental parse
// provider-side; wsGetGeneration is a pure counter read (pollable during loading).
using pWsGetNodeCount = int(__cdecl*)();
using pWsGetTopNodeIdAt = int64_t(__cdecl*)(int index);
using pWsGetChildCount = int(__cdecl*)(int64_t id);
using pWsGetChildIdAt = int64_t(__cdecl*)(int64_t id, int index);
using pWsGetNodeInfo = int(__cdecl*)(int64_t id, UtinniWsNodeInfo* out);
using pWsGetNodeTemplateName = int(__cdecl*)(int64_t id, char* buf, int cap);
using pWsGetGeneration = int(__cdecl*)();

pWsGetNodeCount wsGetNodeCount = nullptr;
pWsGetTopNodeIdAt wsGetTopNodeIdAt = nullptr;
pWsGetChildCount wsGetChildCount = nullptr;
pWsGetChildIdAt wsGetChildIdAt = nullptr;
pWsGetNodeInfo wsGetNodeInfo = nullptr;
pWsGetNodeTemplateName wsGetNodeTemplateName = nullptr;
pWsGetGeneration wsGetGeneration = nullptr;
} // namespace swg::worldsnapshot

namespace
{
// WS-3 (advertised-client RVA-safety sweep). The offline WorldSnapshotReaderWriter
// (swg::worldSnapshotReaderWriter::*), its static instance ptr (0x1913E94), the unbound
// runtime helpers (swg::worldsnapshot::{unload,clearPreloadList,createObject}), and the raw
// snapshot-state literals (0x191113C preload flag, 0x0059C3F3 nop, the removeNodeFull 0x005A*/
// 0x19BB7* block) are all hardcoded SWGEmu RVAs that are NOT in the GetEngineHookPoints catalog
// -> garbage on the advertised DX11 client. Only the runtime worldSnapshot::{load,addObject,
// removeObject,moveObject,getLoadingPercent,detailLevelChanged} resolve there (the resolver
// overwrites their literal slots). It never bit before because the Repository was empty; now
// treeFile::enumerateFiles populates it, so the editor's snapshot paths run and fault (same class
// as the already-guarded generateHighestId() -> worldSnapshotReaderWriter::openFile crash). Until
// the offline reader/writer is advertised, every editor entry point that reaches it degrades to a
// no-op here (no crash). SWGEmu is byte-for-byte unchanged -- isAdvertisedClient() is false there
// (D-00). Centralized so the sweep is one gate, not scattered isAdvertisedClient() reads.
inline bool offlineSnapshotUnavailable()
{
    return swg::endpoints::isAdvertisedClient();
}
} // namespace

namespace utinni
{
WorldSnapshotReaderWriter* WorldSnapshotReaderWriter::get()
{
    return (WorldSnapshotReaderWriter*)0x1913E94;
} // Static WorldSnapshotReaderWriter ptr

void WorldSnapshotReaderWriter::clear()
{
    if (offlineSnapshotUnavailable())
        return;
    swg::worldSnapshotReaderWriter::clear(this);
}

const char* WorldSnapshotReaderWriter::getObjectTemplateName(int objectTemplateNameIndex)
{
    if (offlineSnapshotUnavailable())
        return nullptr;
    return swg::worldSnapshotReaderWriter::getObjectTemplateName(this, objectTemplateNameIndex);
}

int WorldSnapshotReaderWriter::getNodeCount()
{
    if (offlineSnapshotUnavailable())
        return 0;
    return swg::worldSnapshotReaderWriter::nodeCount(this);
}

int WorldSnapshotReaderWriter::getNodeCountTotal()
{
    if (offlineSnapshotUnavailable())
        return 0;
    return swg::worldSnapshotReaderWriter::nodeCountTotal(this);
}

WorldSnapshotReaderWriter::Node* WorldSnapshotReaderWriter::getNodeById(int id)
{
    // Goal A+ prerequisite (2026-07-09): the raw nodeList/children walks below run on `this` ==
    // the hardcoded 0x1913E94 SWGEmu singleton -- garbage on the advertised client. They were
    // unreachable there only because the player's lookAt target resolved null (wave 1); the v16
    // lookAt-target accessor removes that property, so every Node*-producing function in this
    // file gates FIRST, before any member touch (audit-enforced: the world_snapshot guard check
    // in scripts/audit-advertised-rva-safety.ps1 fails CI on an ungated Node*-returning body).
    if (offlineSnapshotUnavailable())
        return nullptr;

    Node* result = nullptr;
    for (int i = 0; i < nodeList->size(); ++i)
    {
        Node* node = nodeList->at(i);
        if (node->id == id)
        {
            result = node;
            break;
        }
    }
    return result;
}

WorldSnapshotReaderWriter::Node* WorldSnapshotReaderWriter::getNodeById(int id, Object* parentObject)
{
    // Gate at the dispatcher too: parentObject arrives as a raw 2002-layout field read from
    // managed callers -- on the advertised client it must not be dereferenced downstream.
    if (offlineSnapshotUnavailable())
        return nullptr;

    if (parentObject == nullptr)
    {
        return getNodeById(id);
    }
    else
    {
        return getNodeByIdWithParent(parentObject, id);
    }
}

WorldSnapshotReaderWriter::Node* WorldSnapshotReaderWriter::findChildNode(Node* parentNode, int id)
{
    if (offlineSnapshotUnavailable())
        return nullptr;

    Node* result = nullptr;

    for (int i = 0; i < parentNode->children->size(); ++i)
    {
        Node* child = parentNode->children->at(i);
        if (child->id == id)
        {
            result = child;
            break;
        }
    }

    if (result == nullptr)
    {
        for (int i = 0; i < parentNode->children->size(); ++i)
        {
            Node* child = parentNode->children->at(i);

            if (child->children != nullptr && !child->children->empty())
            {
                Node* childResult = findChildNode(child, id);
                if (childResult != nullptr && childResult->id == id)
                {
                    result = childResult;
                    break;
                }
            }
        }
    }

    return result;
}

WorldSnapshotReaderWriter::Node* WorldSnapshotReaderWriter::getNodeByIdWithParent(Object* parentObject, int id)
{
    if (offlineSnapshotUnavailable())
        return nullptr;

    Object* topParent = parentObject;

    while (true)
    {
        if (topParent->parentObject == nullptr)
        {
            break;
        }

        topParent = topParent->parentObject;
    }

    Node* parentNode = getNodeById(topParent->networkId);

    if (Network::isServerId(topParent->networkId) || parentNode == nullptr)
    {
        return nullptr;
    }

    return findChildNode(parentNode, id);
}

WorldSnapshotReaderWriter::Node* WorldSnapshotReaderWriter::getNodeByNetworkId(int networkId)
{
    if (offlineSnapshotUnavailable())
        return nullptr;
    return swg::worldSnapshotReaderWriter::getNodeByNetworkId(this, networkId);
}

WorldSnapshotReaderWriter::Node* WorldSnapshotReaderWriter::getNodeAt(int index)
{
    if (offlineSnapshotUnavailable())
        return nullptr;
    return swg::worldSnapshotReaderWriter::getNodeByIndex(this, index);
}

WorldSnapshotReaderWriter::Node* WorldSnapshotReaderWriter::getLastNode()
{
    if (offlineSnapshotUnavailable())
        return nullptr;
    return nodeList->back();
}

WorldSnapshotReaderWriter::Node* WorldSnapshotReaderWriter::addNode(int nodeId, int parentNodeId, const char* objectFilename, int cellId, const swg::math::Transform& transform, float radius, unsigned int pobCrc)
{
    if (offlineSnapshotUnavailable())
        return nullptr;

    // For some reason, the ptr is wrong if parentNodeId is 0 and it's actually 'result - 4' to get the accurate pointer

    swgptr node;
    if (parentNodeId == 0)
    {
        node = swg::worldSnapshotReaderWriter::addNode(this, nodeId, parentNodeId, *ConstCharCrcString::ctor(objectFilename), cellId, transform, radius, pobCrc) - 4; // That's why we subtract 4 here
    }
    else
    {
        node = swg::worldSnapshotReaderWriter::addNode(this, nodeId, parentNodeId, *ConstCharCrcString::ctor(objectFilename), cellId, transform, radius, pobCrc); // If it's added to a parentNode, it seems fine
    }

    // Upon further testing, the ptr returned isn't reliable, unsure why
    // return memory::read<Node*>(node);
    return nullptr;
}

void WorldSnapshotReaderWriter::Node::removeNode()
{
    if (offlineSnapshotUnavailable())
        return;

    if (!Game::isSafeToUse())
    {
        return;
    }

    if (parentId == 0)
    {
        removeNodeFull();

        const auto reader = get();
        for (int i = 0; i < get()->getNodeCount(); ++i)
        {
            if (reader->nodeList->at(i)->id == id)
            {
                reader->nodeList->erase(reader->nodeList->begin() + i);
                break;
            }
        }
    }
    else
    {
        // ToDo check if removeNodeFull can replace the below remove obj

        for (int i = 0; i < parentNode->children->size(); ++i)
        {
            if (parentNode->children->at(i)->id == id)
            {
                parentNode->children->erase(parentNode->children->begin() + i);
                break;
            }
        }
    }

    Object* nodeObject = Network::getObjectById(id);

    // Need to nullptr check because only loaded objects are non null, ie in range or previously 'seen'
    // and the loop goes through all nodes in the entire .WS
    if (nodeObject != nullptr)
    {
        nodeObject->remove();
    }

    swg::worldSnapshotReaderWriter::removeNode(WorldSnapshotReaderWriter::get(), id);
}

void WorldSnapshotReaderWriter::Node::removeNodeFull() // WIP - Messy IDA pseudo code
{
    if (offlineSnapshotUnavailable())
        return;

    if (!Game::isSafeToUse())
    {
        return;
    }

    using Void1 = int(__thiscall*)(swgptr, int);
    using Void2 = int(__thiscall*)(swgptr);
    using Void3 = void(__cdecl*)();

    Void1 void1 = (Void1)0x005A25A0;
    Void1 void2 = (Void1)0x005A08D0;
    Void3 void5 = (Void3)0x005A09C0;
    Void2 call1 = (Void2)0x005A2D90;

    if (getNodeSpatialSubdivisionHandle())
    {
        swgptr nodeSpatialSubdivisionHandle1 = getNodeSpatialSubdivisionHandle();
        swgptr nodeSpatialSubdivisionHandle2 = nodeSpatialSubdivisionHandle1;

        if (nodeSpatialSubdivisionHandle1)
        {
            swgptr nodeSpatialSubdivisionHandle3 = *(swgptr*)(nodeSpatialSubdivisionHandle1 + 4);
            if (nodeSpatialSubdivisionHandle3)
            {
                void1(nodeSpatialSubdivisionHandle3, nodeSpatialSubdivisionHandle1);
                swgptr nodeSpatialSubdivisionHandle4 = nodeSpatialSubdivisionHandle2;

                if (!(*(byte*)(0x19BB7DC) & 1))
                {
                    *(byte*)(0x19BB7DC) |= 1u;

                    call1(memory::read<swgptr>(0x19BB7E0));
                    void5();
                }
                void2(memory::read<swgptr>(0x19BB7E0), (int)&nodeSpatialSubdivisionHandle4);
            }
        }
        setNodeSpatialSubdivisionHandle(0);
    }

    Object* nodeObject = Network::getObjectById(id);

    // Need to nullptr check because only loaded objects are non null, ie in range or previously 'seen'
    // and the loop goes through all nodes in the entire .WS
    if (nodeObject != nullptr)
    {
        nodeObject->remove();
    }
    swg::worldSnapshotReaderWriter::node::removeFromWorld(this);
}

int64_t WorldSnapshotReaderWriter::Node::getNodeNetworkId()
{
    return Network::cast(id);
}

swgptr WorldSnapshotReaderWriter::Node::getNodeSpatialSubdivisionHandle()
{
    if (offlineSnapshotUnavailable())
        return 0;
    return swg::worldSnapshotReaderWriter::node::getNodeSpatialSubdivisionHandle(this);
}

void WorldSnapshotReaderWriter::Node::setNodeSpatialSubdivisionHandle(swgptr handle)
{
    if (offlineSnapshotUnavailable())
        return;
    swg::worldSnapshotReaderWriter::node::setNodeSpatialSubdivisionHandle(this, handle);
}

const char* WorldSnapshotReaderWriter::Node::getObjectTemplateName() const
{
    return WorldSnapshotReaderWriter::get()->getObjectTemplateName(objectTemplateNameIndex);
}

int WorldSnapshotReaderWriter::Node::getChildCount() const
{
    if (offlineSnapshotUnavailable())
        return 0;
    return children->size();
}

WorldSnapshotReaderWriter::Node* WorldSnapshotReaderWriter::Node::getChildById(int id)
{
    if (offlineSnapshotUnavailable())
        return nullptr;

    Node* result = nullptr;

    for (int i = 0; i < children->size(); ++i)
    {
        Node* node = children->at(i);
        if (node->id == id)
        {
            result = node;
            break;
        }
    }

    return result;
}

WorldSnapshotReaderWriter::Node* WorldSnapshotReaderWriter::Node::getChildAt(int index)
{
    if (offlineSnapshotUnavailable())
        return nullptr;
    return children->at(index);
}

WorldSnapshotReaderWriter::Node* WorldSnapshotReaderWriter::Node::getLastChild()
{
    if (offlineSnapshotUnavailable())
        return nullptr;
    return children->back();
}

void WorldSnapshot::load(const std::string& name)
{
    if (name.empty())
    {
        return;
    }

    // WS-3: swg::worldsnapshot::load IS advertised, but the nopAddress patch below writes a
    // hardcoded SWGEmu RVA (0x0059C3F3) -> garbage on the advertised client. .ws snapshot loading
    // is not yet smoke-verified there, so degrade the whole method to a no-op. Follow-up to enable
    // it on advertised: skip just the nop and confirm load() works without the .trn-name suppression.
    if (offlineSnapshotUnavailable())
        return;

    memory::nopAddress(0x0059C3F3, 6); // Removes the grabbing of current .trn name to allow the loading of any .ws

    swg::worldsnapshot::load(name.c_str());
}

void WorldSnapshot::unload()
{
    if (offlineSnapshotUnavailable())
        return;

    if (!Game::isSafeToUse())
    {
        return;
    }

    auto readerWriter = WorldSnapshotReaderWriter::get();
    for (int i = 0; i < readerWriter->getNodeCount(); ++i)
    {
        readerWriter->getNodeAt(i)->removeNodeFull();
    }

    swg::worldsnapshot::unload();
}

void WorldSnapshot::reload()
{
    if (offlineSnapshotUnavailable())
        return;

    unload();

    load(GroundScene::get()->getName());
}

void WorldSnapshotReaderWriter::clearPreloadList(swgptr unk1, swgptr unk2, swgptr unk3)
{
    if (offlineSnapshotUnavailable())
        return;
    swg::worldsnapshot::clearPreloadList(unk1, unk2, unk3);
}

void WorldSnapshotReaderWriter::saveFile(const char* snapshotName)
{
    if (offlineSnapshotUnavailable())
        return;

    CreateDirectory((utility::getWorkingDirectory() + "/snapshot/").c_str(), nullptr);

    if (constCharUtility::isEmpty(snapshotName))
    {
        swg::worldSnapshotReaderWriter::saveFile(this, ("snapshot/" + GroundScene::get()->getName() + ".ws").c_str());
    }
    else
    {
        swg::worldSnapshotReaderWriter::saveFile(this, ("snapshot/" + std::string(snapshotName) + ".ws").c_str());
    }
}

bool WorldSnapshot::getPreloadSnapshot()
{
    if (offlineSnapshotUnavailable())
        return false;
    return memory::read<bool>(0x191113C);
}

void WorldSnapshot::setPreloadSnapshot(bool preloadSnapshot)
{
    if (offlineSnapshotUnavailable())
        return;
    memory::write<bool>(0x191113C, preloadSnapshot);
}

void WorldSnapshot::detailLevelChanged()
{
    swg::worldsnapshot::detailLevelChanged();
}

static int highestId = 0;
int getHighestIdFromNode(int currentHighestId, const WorldSnapshotReaderWriter::Node* node)
{
    int result = max(currentHighestId, node->id);
    if (node->children && !node->children->empty())
    {
        for (const WorldSnapshotReaderWriter::Node* childNode : *node->children)
        {
            result = getHighestIdFromNode(result, childNode);
        }
    }

    return result;
}

int WorldSnapshot::generateHighestId()
{
    int newId = 0;

    // Phase 24: the scan below reads each snapshot via WorldSnapshotReaderWriter::get() +
    // swg::worldSnapshotReaderWriter::openFile -- the OFFLINE reader, whose addresses are hardcoded
    // SWGEmu RVAs NOT in the advertised catalog (only the runtime worldSnapshot::* is advertised). On
    // the advertised client those RVAs are garbage -> crash. It never bit before because the Repository
    // was empty (no "snapshot" dir -> empty loop); now treeFile::enumerateFiles populates it, so the
    // loop runs and faults. Skip on the advertised client -- the WorldSnapshot editor's new-ID
    // generation degrades there (no crash) until worldSnapshotReaderWriter is advertised. SWGEmu
    // unchanged (the RVAs are valid; isAdvertisedClient() is false).
    if (swg::endpoints::isAdvertisedClient())
    {
        highestId = newId;
        return newId;
    }

    auto snapshotFilenames = Game::getRepository()->getDirectoryFilenames("snapshot");
    for (const auto& filename : snapshotFilenames)
    {
        swg::worldSnapshotReaderWriter::openFile(WorldSnapshotReaderWriter::get(), std::filesystem::path(filename).filename().replace_extension("").string().c_str());

        const auto reader = WorldSnapshotReaderWriter::get();
        for (int i = 0; i < reader->getNodeCount(); ++i)
        {
            newId = getHighestIdFromNode(newId, reader->nodeList->at(i));
        }
    }

    highestId = newId;
    return newId;
}

Object* createObject(WorldSnapshotReaderWriter::Node* node)
{
    if (offlineSnapshotUnavailable())
        return nullptr;
    DWORD errorCode = 0;
    return (Object*)swg::worldsnapshot::createObject(WorldSnapshotReaderWriter::get(), node, errorCode);
}

bool WorldSnapshot::isValidObject(const char* objectFilename)
{
    if (ObjectTemplateList::getObjectTemplateByFilename(objectFilename) == nullptr)
    {
        return false;
    }

    if (strstr(objectFilename, "/base/"))
    {
        return false;
    }

    ClientObject* cobj = ObjectTemplate::createObject(objectFilename)->getClientObject();

    if (cobj == 0 || cobj->getCreatureObject() != 0 || cobj->getShipObject() != 0 || (cobj != 0 && cobj->getTangibleObject() == 0 && cobj->getStaticObject() == 0))
    {
        return false;
    }

    cobj->remove();
    cobj = nullptr; // ToDo does this need to be dealloc instead?

    return true;
}

// ToDo move duplicated code in the following functions to shared function

WorldSnapshotReaderWriter::Node* WorldSnapshot::createAddNode(const char* objectFilename, swg::math::Transform& transform)
{
    if (offlineSnapshotUnavailable())
        return nullptr;

    /*if (!isValidObject(objectFilename))
    {
        return nullptr;
    }*/

    const auto reader = WorldSnapshotReaderWriter::get();

    WorldSnapshotReaderWriter::Node* parentNode = nullptr;

    // Temporary check to get parent, make this better
    int parentNodeId = 0;
    Camera* camera = GroundScene::get()->getCurrentCamera(); // If camera is outside of the POB and the new node to be created is inside, it crashes as parentObject is nullptr
    if (camera->parentObject != nullptr)
    {
        parentNode = reader->getNodeById(camera->parentObject->networkId, camera->parentObject->parentObject);
        if (parentNode == nullptr)
        {
            return nullptr;
        }

        parentNodeId = parentNode->id;
    }

    if (camera->parentObject != nullptr && parentNode == nullptr)
    {
        return nullptr;
    }

    auto objectTemplate = ObjectTemplateList::getObjectTemplateByFilename(objectFilename);

    if (objectTemplate == nullptr)
    {
        return nullptr;
    }

    const char* pobFilename = ObjectTemplateList::getObjectTemplateByFilename(objectFilename)->getPortalLayoutFilename();
    if (!objectTemplate->getAppearanceFilename() && !objectTemplate->getClientDataFilename() && !pobFilename)
    {
        return nullptr;
    }

    // Check if the object contains cells
    int pobCrc = 0;
    int pobCellCount = 0;
    if (pobFilename != nullptr && pobFilename[0] != '\0')
    {
        PortalPropertyTemplate* pPob = PortalPropertyTemplateList::getPobByCrcString(PersistentCrcString::ctor(pobFilename));
        pobCrc = pPob->getCrc();
        pobCellCount = pPob->getCellCount() - 1;
    }

    highestId++;
    const int id = highestId;
    reader->addNode(id, parentNodeId, objectFilename, 0, transform, 512, pobCrc); // ToDo Make radius a customizable variable

    // If the object contains cells, create them
    for (int i = 0; i < pobCellCount; ++i)
    {
        highestId = id + i + 1;
        reader->addNode(highestId, id, "object/cell/shared_cell.iff", i + 1, swg::math::Transform::getIdentity(), 0, 0);
    }

    // Workaround to the unreliable ptr return of reader->addNode
    WorldSnapshotReaderWriter::Node* node;
    if (parentNode == nullptr)
    {
        node = reader->nodeList->back();
    }
    else
    {
        node = parentNode->children->back();
    }

    Object* obj = createObject(node);
    if (obj)
    {
        obj->addToWorld();
    }

    return node;
}

WorldSnapshotReaderWriter::Node* WorldSnapshot::createNodeCopy(WorldSnapshotReaderWriter::Node* originalNode, swg::math::Transform& transform)
{
    if (offlineSnapshotUnavailable())
        return nullptr;

    const auto reader = WorldSnapshotReaderWriter::get();

    highestId++;
    const int id = highestId;
    reader->addNode(id, originalNode->parentId, originalNode->getObjectTemplateName(), originalNode->cellIndex, transform, originalNode->radius, originalNode->pobCRC);

    if (originalNode->children != nullptr)
    {
        for (int i = 0; i < originalNode->children->size(); ++i)
        {
            highestId = id + i + 1;
            const auto childNode = originalNode->children->at(i);
            reader->addNode(highestId, id, childNode->getObjectTemplateName(), childNode->cellIndex, swg::math::Transform(childNode->transform), childNode->radius, childNode->pobCRC);
        }
    }

    // Workaround to the unreliable ptr return of reader->addNode
    WorldSnapshotReaderWriter::Node* node;
    if (originalNode->parentNode == nullptr)
    {
        node = reader->nodeList->back();
    }
    else
    {
        node = originalNode->parentNode->children->back();
    }

    Object* obj = createObject(node);
    if (obj)
    {
        obj->addToWorld();
    }

    return node;
}

Object* WorldSnapshot::addNode(WorldSnapshotReaderWriter::Node* node)
{
    if (offlineSnapshotUnavailable())
        return nullptr;

    const auto reader = WorldSnapshotReaderWriter::get();

    reader->addNode(node->id, node->parentId, node->getObjectTemplateName(), node->cellIndex, node->transform, node->radius, node->pobCRC);

    if (node->children != nullptr)
    {
        for (int i = 0; i < node->children->size(); ++i)
        {
            const auto childNode = node->children->at(i);
            reader->addNode(childNode->id, childNode->parentId, childNode->getObjectTemplateName(), childNode->cellIndex, childNode->transform, childNode->radius, childNode->pobCRC);
        }
    }

    Object* obj = createObject(node);
    if (obj)
    {
        obj->addToWorld();
    }

    return obj;
}

void WorldSnapshot::removeNode(WorldSnapshotReaderWriter::Node* node)
{
    if (offlineSnapshotUnavailable())
        return;

    node->removeNode();

    detailLevelChanged(); // Hack to update the .WS
}

// ---------------------------------------------------------------------------
// WorldSnapshotLive (Goal B Wave 1 / v17): thin null-checked veneer over the
// swg::worldsnapshot::wsGet* slots. Slots are null on SWGEmu (no RVA exists),
// so every call degrades miss-safe there without touching isAdvertisedClient().
// ---------------------------------------------------------------------------

bool WorldSnapshotLive::isAvailable()
{
    using namespace swg::worldsnapshot;
    return wsGetNodeCount != nullptr && wsGetTopNodeIdAt != nullptr && wsGetChildCount != nullptr &&
           wsGetChildIdAt != nullptr && wsGetNodeInfo != nullptr && wsGetNodeTemplateName != nullptr &&
           wsGetGeneration != nullptr;
}

int WorldSnapshotLive::getGeneration()
{
    return swg::worldsnapshot::wsGetGeneration != nullptr ? swg::worldsnapshot::wsGetGeneration() : 0;
}

int WorldSnapshotLive::getTopNodeCount()
{
    return swg::worldsnapshot::wsGetNodeCount != nullptr ? swg::worldsnapshot::wsGetNodeCount() : 0;
}

int64_t WorldSnapshotLive::getTopNodeIdAt(int index)
{
    return swg::worldsnapshot::wsGetTopNodeIdAt != nullptr ? swg::worldsnapshot::wsGetTopNodeIdAt(index) : 0;
}

int WorldSnapshotLive::getChildCount(int64_t id)
{
    return swg::worldsnapshot::wsGetChildCount != nullptr ? swg::worldsnapshot::wsGetChildCount(id) : 0;
}

int64_t WorldSnapshotLive::getChildIdAt(int64_t id, int index)
{
    return swg::worldsnapshot::wsGetChildIdAt != nullptr ? swg::worldsnapshot::wsGetChildIdAt(id, index) : 0;
}

bool WorldSnapshotLive::getNodeInfo(int64_t id, WorldSnapshotNodeInfo& out)
{
    if (swg::worldsnapshot::wsGetNodeInfo == nullptr)
    {
        return false;
    }

    UtinniWsNodeInfo info = {};
    info.size = sizeof(UtinniWsNodeInfo); // size-first protocol: caller fills size BEFORE the call
    if (swg::worldsnapshot::wsGetNodeInfo(id, &info) == 0)
    {
        return false;
    }

    out.containedById = info.containedById;
    out.cellIndex = info.cellIndex;
    out.portalLayoutCrc = info.portalLayoutCrc;
    out.radius = info.radius;
    out.childCount = info.childCount;
    static_assert(sizeof(out.transform.matrix) == sizeof(info.transform),
                  "swg::math::Transform must stay the 12-float row-major 3x4 the v17 contract carries");
    memcpy(out.transform.matrix, info.transform, sizeof(info.transform));
    return true;
}

std::string WorldSnapshotLive::getNodeTemplateName(int64_t id)
{
    if (swg::worldsnapshot::wsGetNodeTemplateName == nullptr)
    {
        return {};
    }

    // Contract: returns needed length INCLUDING the NUL; 0 = miss. Null buf = pure size query.
    const int needed = swg::worldsnapshot::wsGetNodeTemplateName(id, nullptr, 0);
    if (needed <= 1)
    {
        return {};
    }

    std::string name(static_cast<size_t>(needed) - 1, '\0');
    swg::worldsnapshot::wsGetNodeTemplateName(id, name.data(), needed);
    return name;
}
} // namespace utinni
