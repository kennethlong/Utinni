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
#include "swg/misc/crc_string.h"
#include "swg/misc/swg_math.h"
#include "swg/misc/swg_misc.h"
#include "swg/appearance/appearance.h"
#include "swg/appearance/portal.h"

namespace utinni
{
class Appearance;
class Object;
class ClientObject;
struct Controller;
struct CellProperty;
struct ConstCharCrcString;

class UTINNI_API ObjectTemplate : DataResource
{
public:
    ObjectTemplate* baseData;
    static Object* createObject(const char* filename);
};

class UTINNI_API SharedObjectTemplate
{
public:
    const char* getAppearanceFilename();
    const char* getPortalLayoutFilename();
    const char* getClientDataFilename();
    int getGameObjectType();
};

class UTINNI_API SharedBuildingObjectTemplate
{
public:
    const char* getTerrainLayerFilename();
    const char* getInteriorLayoutFilename();
};

class UTINNI_API ObjectTemplateList
{
public:
    static SharedObjectTemplate* getObjectTemplateByFilename(const char* filename);
    static SharedObjectTemplate* getObjectTemplateByIff(swgptr iff);
    static const ConstCharCrcString getCrcStringByCrc(unsigned int crc);
    static ConstCharCrcString getCrcStringByName(const char* name);
    static swgptr getCrcStringByNameAsPtr(const char* name);
};

class UTINNI_API Object
{
public:
    swgptr vtbl;
    uint32_t unk01;
    uint32_t unk02;
    uint32_t unk03;
    ObjectTemplate* objectTemplate;
    uint32_t unk04;
    char* name;
    int64_t networkId;
    Appearance* appearance;
    Controller* controller;
    uint32_t unk07;
    Object* parentObject;
    uint32_t unk08;
    swgptr dpvsObjects;
    int rotations;
    swg::math::Vector scale;
    swg::math::Transform objectToParent;
    swg::math::Transform* objectToWorld;
    uint32_t unk09;
    swgptr container;
    swgptr CollisionProperty;
    swgptr SpatialSubdivisionHandle;
    uint8_t unk10; // might not need
    uint8_t unk11; // might not need
    uint8_t unk12; // might not need
    uint8_t unk13;
    uint32_t unk14;
    swgptr containedBy;

    static Object* ctor();
    static Object* getObjectById(swgptr networkIdPointer);

    void remove();

    bool isActive();

    swg::math::Transform* getTransform();
    swg::math::Transform* getTransform_o2w();
    void setTransform_o2w(swg::math::Transform& object2world);
    const swg::math::Vector rotate_o2w(const swg::math::Vector* o2w, const swg::math::Vector* pointInSpace);

    void move(const swg::math::Vector& vector);

    void addToWorld();
    void removeFromWorld();
    void setAppearance(Appearance* appearance);

    CellProperty* getParentCell();
    void addNotification(swgptr notification, bool allowWhenInWorld);
    void removeNotification(swgptr notification, bool allowWhenInWorld);

    void setObjectToWorldDirty(bool isDirty);

    void positionAndRotationChanged(bool dueToParentChange, swg::math::Vector& oldPosition);

    ClientObject* getClientObject();

    const char* getTemplateFilename();
    const char* getAppearanceFilename();

    // Advertised-safe inspector accessors (Bucket A-2 richer selected-object readout). Each calls a
    // D-01 advertised row (getObjectTemplateName / getNetworkId / getObjectTemplate) that the endpoint
    // resolver re-points on the advertised NGE client, and is null-checked so it degrades to
    // nullptr/0 on SWGEmu / pre-advertised (the slot is null). Prefer these over the raw `networkId`
    // struct field and getTemplateFilename()/getAppearanceFilename(), whose SWGEmu paths are
    // offset-/RVA-fragile on the advertised client (see the 2026-06-29 editor-unlock handoff).
    const char* getObjectTemplateName();
    int64_t getNetworkIdValue();
    const char* getSharedAppearanceFilename();
    const char* getSharedPortalLayoutFilename();
    const char* getSharedClientDataFilename();

    // Object class Tag (uint32 FOURCC) via the advertised object::getObjectType row (remapped onto
    // getType, re-pointed on the advertised client). getParentCellName(): nullptr = outdoors / no
    // containing cell; "" = in a cell but the name isn't safely readable (the CellProperty::name raw
    // field is offset-fragile on the NGE advertised layout, §5 -- read only on SWGEmu); else the name.
    unsigned int getObjectType();
    const char* getParentCellName();
};

struct AutoVariableBase
{
    swgptr vtbl;
};

struct AutoDeltaVariableBase : AutoVariableBase
{
    unsigned short index;
    swgptr parent;
};

template <typename T>
struct AutoDeltaVariable : AutoDeltaVariableBase
{
    T value;
    T previousValue;
};

template <typename T>
struct AutoDeltaVariableCallback : AutoDeltaVariable<T>
{
    swgptr callback;
    Object* sourceObject;
};

struct AutoDeltaByteStream
{
    uint32_t unk01;
    uint32_t unk02;
    uint32_t unk03;
    uint32_t unk04;
    uint32_t unk05;
    uint32_t unk06;
    uint32_t unk07;
    uint32_t unk08;
};

} // namespace utinni