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

#include "utinni.h"
#include "swg/misc/swg_math.h"
#include "swg/object/object.h"

namespace utinni
{
class UTINNI_API WorldSnapshotReaderWriter
{
public:
    class UTINNI_API Node
    {
    public:
        bool isDeleted;
        int id;
        int parentId;
        int objectTemplateNameIndex;
        int cellIndex;
        swg::math::Transform transform;
        float radius;
        int pobCRC;
        Node* parentNode;
        std::vector<Node*>* children;
        swgptr pSpatialSubdivisionHandle;
        float distanceSquaredTo;
        bool isInWorld;

        void removeNode();
        void removeNodeFull();

        int64_t getNodeNetworkId();
        swgptr getNodeSpatialSubdivisionHandle();
        void setNodeSpatialSubdivisionHandle(swgptr handle);

        const char* getObjectTemplateName() const;

        int getChildCount() const;
        Node* getChildById(int id);
        Node* getChildAt(int index);
        Node* getLastChild();
    };

    std::vector<Node*>* nodeList;
    std::vector<char*>* objectTemplateNameList;
    std::unordered_map<int, int>* objectTemplateCrcMap;
    std::map<int, Node*>* networkIdMap;

    static WorldSnapshotReaderWriter* get();

    void clear();
    static void clearPreloadList(swgptr unk1, swgptr unk2, swgptr unk3);
    void saveFile(const char* snapshotName = "");

    const char* getObjectTemplateName(int objectTemplateNameIndex);

    int getNodeCount();
    int getNodeCountTotal();

    Node* getNodeById(int id);
    Node* getNodeById(int id, Object* parentObject);
    Node* findChildNode(Node* parentNode, int id);
    Node* getNodeByIdWithParent(Object* parentObject, int id);
    Node* getNodeByNetworkId(int networkId);
    Node* getNodeAt(int index);
    Node* getLastNode();

    Node* addNode(int nodeId, int parentNodeId, const char* objectFilename, int cellId, const swg::math::Transform& transform, float radius, unsigned int pobCrc);
};

} // namespace utinni

namespace utinni
{
// Goal B Wave 1 (contract v17): the id-keyed READ view of the CURRENT scene's live
// snapshot on the advertised client. Enumeration is authored-only (tombstoned and
// buildout-provenance rows never appear) and counts are therefore SMALLER than the
// SWGEmu-side raw Node walks — that is the provenance contract working, not data loss.
// All reads are game-thread-only (marshal managed callers via the main-loop pattern);
// every id-keyed call answers miss-safe (false/0/empty) for an unknown id. On SWGEmu
// the slots never resolve and every call degrades the same miss-safe way — the editor
// keeps its raw WorldSnapshotReaderWriter path there.
struct UTINNI_API WorldSnapshotNodeInfo
{
    int64_t containedById = 0; // 0 = top-level
    int cellIndex = 0;
    unsigned int portalLayoutCrc = 0;
    float radius = 0.0f;
    int childCount = 0;             // enumerable (non-tombstone) direct children
    swg::math::Transform transform; // parent-relative, row-major 3x4, position = column 3
};

class UTINNI_API WorldSnapshotLive
{
public:
    static bool isAvailable(); // all 7 read rows resolved (advertised client only)

    // Bumps on the engine's snapshot load/unload/clear ONLY. One scene change may bump
    // it more than once — invalidate caches on !=, never on +1. Pure counter read:
    // safe to poll while a snapshot load is still in flight.
    static int getGeneration();

    static int getTopNodeCount();
    static int64_t getTopNodeIdAt(int index);
    static int getChildCount(int64_t id);
    static int64_t getChildIdAt(int64_t id, int index);
    static bool getNodeInfo(int64_t id, WorldSnapshotNodeInfo& out);
    static std::string getNodeTemplateName(int64_t id);

    // ── Wave 2 (contract v18): LIVE-ONLY mutation — nothing persists until Wave 3. ──
    static bool isMutationAvailable(); // all 5 mutation rows resolved (advertised only)

    // Interactive add: provider mints the id (+ atomic POB cell expansion), pre-validates
    // everything BEFORE mutating (origin guards, template, container live+in-world when
    // containedById != 0, full id range free), spawns immediately via the streamed-create
    // path. Default radius 512 (provider flag #1) — call setNodeRadius after to tune.
    // POB into a container is refused. Returns the new top id; 0 = fail-closed.
    static int64_t addObject(const char* sharedTemplateFilename, const swg::math::Transform& transform, int64_t containedById);

    // Undo-replay primitive: pure data re-add at the EXPLICIT id (top-level dirties the
    // streaming diff; a child under a live spawned parent spawns immediately). HARD
    // CONTRACT: replay a recorded subtree — top node AND all children — in ONE game-thread
    // batch, before the next update tick. Returns 1 ok, 0 fail-closed (nothing added).
    static bool addNodeAt(int64_t explicitId, int64_t containedById, const char* templateFilename, int cellIndex, const swg::math::Transform& transform, float radius, unsigned int portalLayoutCrc);

    // Full teardown (despawn + subtree tombstone). TRI-STATE: 1 removed · 0 miss ·
    // -1 OCCUPIED (non-client-cached object inside the containment subtree — surface
    // "step out of the building first"; nothing was touched).
    static int removeNode(int64_t id);

    static bool setNodeRadius(int64_t id, float radius);

    // One-time-by-discipline allocator band (install-scan floor lands here; 0 = keep
    // default for either param). Returns false on an invalid band (nothing changed).
    static bool configureIdAllocator(int64_t floorId, int64_t ceilingId);
};

class UTINNI_API WorldSnapshot
{
public:
    static void load(const std::string& name);
    static void unload();
    static void reload();

    static bool getPreloadSnapshot();
    static void setPreloadSnapshot(bool preloadSnapshot);

    static void detailLevelChanged();

    static int generateHighestId();

    static bool isValidObject(const char* objectFilename);
    static WorldSnapshotReaderWriter::Node* createAddNode(const char* objectFilename, swg::math::Transform& transform);
    static WorldSnapshotReaderWriter::Node* createNodeCopy(WorldSnapshotReaderWriter::Node* originalNode, swg::math::Transform& transform);

    static Object* addNode(WorldSnapshotReaderWriter::Node* node);
    static void removeNode(WorldSnapshotReaderWriter::Node* node);
};
} // namespace utinni
