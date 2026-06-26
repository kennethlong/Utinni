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

#include "player_object.h"

#include "swg/game/game.h"
#include "swg/camera/camera.h"
#include "swg/endpoints.h"

namespace swg::teleportHelper // ToDo implement proper, dirty taken from IDA
{
using pTeleportPlayer = int(__thiscall*)(swgptr pThis, swg::math::Transform* position);

pTeleportPlayer teleportPlayer = (pTeleportPlayer)0x0062A8B0; // Controller function, do proper later
} // namespace swg::teleportHelper

namespace
{
// WS-1 (advertised-client RVA-safety). The player-state accessors below dereference HARDCODED SWGEmu
// globals/RVAs that are NOT in the GetEngineHookPoints catalog: the player-object pointer global
// 0x0191BFB4 (getSpeed read / setSpeed write of +0x674) and the Controller teleport thunk 0x0062A8B0
// -- garbage on the advertised DX11 client. They never bit before because the editor's scene-active
// readers (UtinniPlugins PlayerObjectImpl/FreeCamImpl UpdateSpeed) only ran once setSceneCallbacks fired,
// which did not happen on the advertised client until the WS-1 notify shim. getSpeed in particular reads
// the global regardless of any C# `Game.Player != null` guard, so the crash (0xC0000005 in getSpeed) must
// be stopped HERE, where the unbound RVA lives -- one native guard covers every C# caller. Same class as
// the WS-3 world_snapshot sweep; SWGEmu is byte-for-byte unchanged (isAdvertisedClient() is false -- D-00).
// Until the player-state accessors are advertised, these editor reads/writes degrade to a no-op there.
inline bool playerStateUnavailable()
{
    return swg::endpoints::isAdvertisedClient();
}
} // namespace

namespace utinni::playerObject
{
bool hidePlayerAppearance;
void togglePlayerAppearance()
{
    if (playerStateUnavailable())
    {
        return;
    }

    Object* playerCreatureObj = Game::getPlayerCreatureObject();
    if (playerCreatureObj == nullptr)
    {
        return;
    }

    RenderWorldCamera::clearExcludedObjects();

    hidePlayerAppearance = !hidePlayerAppearance;
    if (hidePlayerAppearance)
    {
        RenderWorldCamera::addExcludedObject(playerCreatureObj);
    }
}

float getSpeed()
{
    if (playerStateUnavailable())
    {
        return 0.0f; // advertised: 0x0191BFB4 is an unbound SWGEmu RVA -> degrade (no player-state API yet)
    }

    return memory::read<float>(0x0191BFB4, 0x674);
}

void setSpeed(float value)
{
    if (playerStateUnavailable()) // advertised: guard the WRITE to 0x0191BFB4 (isSafeToUse() is true in-world there)
    {
        return;
    }

    if (!Game::isSafeToUse())
    {
        return;
    }

    memory::write<float>(0x0191BFB4, 0x674, value);
}

void teleport(float x, float y, float z) // ToDo do more proper in the future
{
    if (playerStateUnavailable()) // advertised: teleportPlayer thunk 0x0062A8B0 is unbound
    {
        return;
    }

    if (!Game::isSafeToUse())
    {
        return;
    }

    swg::math::Transform destPos(x, y, z);
    swg::teleportHelper::teleportPlayer(memory::read<swgptr>((swgptr)Game::getPlayerCreatureObject() + 0x2C), &destPos);
}
} // namespace utinni::playerObject
