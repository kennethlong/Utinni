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

#include "particle_preview.h"

#include "swg/game/game.h"
#include "utility/log.h"

#include <string>

// ----------------------------------------------------------------------------
// Bucket B (v7) advertised endpoint: the cooperative particle retrigger.
// Storage cell for particlePreview::retrigger -> the provider's friend free fn
// utinni_retriggerClientEffect(char const*) over ClientEffectManager::
// m_particleSystems. NULL on SWGEmu (no RVA literal -- accessor-style, the D-04
// class). The swg::endpoints resolver overwrites this BY NAME at utinni_init on
// the advertised client; the consumer seam below null-checks before calling. The
// extern re-declaration in endpoints_bindings.cpp MUST keep this exact typedef.
// ----------------------------------------------------------------------------
namespace swg::particlePreview
{
using pRetrigger = void(__cdecl*)(const char* logicalName);
pRetrigger retrigger = nullptr;
// v8 (Bucket B-2): live .cef RE-PLAY -> provider bool utinni_replayClientEffect(char const*).
// NULL on SWGEmu (advertised-only); the resolver fills it by name on the advertised client.
using pReplay = bool(__cdecl*)(const char* clientEffectName);
pReplay replay = nullptr;
} // namespace swg::particlePreview

// ----------------------------------------------------------------------------
// Particle live-in-client preview -- EARLY SPIKE STUB (plan 15-03, D-09).
//
// See particle_preview.h and .planning/.../15-PARTICLE-PREVIEW-HOOK.md for the
// full reachability decision. Summary: no clean reachable native hot-retrigger
// entry exists this phase, so retriggerLiveEffectInstances() is a documented
// no-op that returns NotReachable. The managed editor seam (15-06) branches on
// that result to disable "Preview in client" and degrade the reload badge to the
// honest tier-(b) candor.
//
// When the real hook is built (15-08 / follow-on, once a live injected session
// confirms the RVAs), wire a `swg::particlePreview` detour-table namespace here
// exactly like world_snapshot.cpp / terrain.cpp:
//   1) AppearanceTemplateList::fetch(editedPrt)  -- reload the edited template
//      into the engine cache so new appearances pick up the edit.
//   2) enumerate the live ParticleEffectAppearance instances of the effect
//      (ClientEffectManager::m_particleSystems, or walk scene objects'
//       appearances via ParticleEffectAppearance::asParticleEffectAppearance).
//   3) call ParticleEffectAppearance::restart() on each.
// ALL of the above inside ONE GroundSceneCallbacks game-thread call, heap-free
// (project_rh_snapshot_no_heap_alloc -- never per frame; stack-snapshot with
// kInlineCap if a per-frame path is ever unavoidable). Then flip the return from
// NotReachable to Retriggered and the editor button/badge light up automatically.
//
// HARD CONSTRAINT: this seam must NEVER drive IDirect3DDevice9::Reset
// (feedback_d3d9_reset_third_party) -- live preview is a scene-instance
// retrigger, not a device reset.
// ----------------------------------------------------------------------------

namespace utinni
{
bool ParticlePreview::isRetriggerAvailable()
{
    // Available iff the advertised client resolved particlePreview::retrigger
    // (utinni_retriggerClientEffect) AND there is a live, usable scene. On SWGEmu the
    // export is absent -> the slot stays null -> false -> the editor keeps the honest
    // tier-(b) reload-candor badge ("Reloads on next scene change or relog."). On the
    // advertised client post-resolve it is true when a scene is safe to touch, so the
    // editor lights up the live "Preview in client" affordance.
    return swg::particlePreview::retrigger != nullptr && Game::isSafeToUse();
}

ParticlePreviewResult ParticlePreview::retriggerLiveEffectInstances(const char* effectName)
{
    // Honest status: distinguish "client not injected/safe" from "hook not advertised"
    // so the editor can show the right disabled-state reason.
    if (!Game::isSafeToUse())
    {
        return ParticlePreviewResult::NotInjected;
    }

    // No cooperative provider retrigger advertised (SWGEmu, or a pre-v7 advertised
    // client) -> degrade honestly to tier-(b); the editor surfaces the reload-candor
    // badge. Allocation-free, no per-frame callback (project_rh_snapshot_no_heap_alloc).
    if (swg::particlePreview::retrigger == nullptr)
    {
        log::info("ParticlePreview::retriggerLiveEffectInstances: particlePreview::retrigger not "
                  "advertised -- degrading to tier-(b) reload candor.");
        return ParticlePreviewResult::NotReachable;
    }

    // A null/empty logical name has nothing to match -- skip the provider walk rather
    // than hand its case-insensitive name compare a null (defensive; the managed caller
    // always passes the just-saved .prt name).
    if (effectName == nullptr || effectName[0] == '\0')
    {
        log::info("ParticlePreview::retriggerLiveEffectInstances: empty effect name -- nothing to retrigger.");
        return ParticlePreviewResult::NotReachable;
    }

    // Real cooperative hook: hand the just-saved logical name to the provider's friend
    // free fn, which walks ClientEffectManager::m_particleSystems and restarts matching
    // live instances (+ a balanced AppearanceTemplateList::fetch refresh). MUST already
    // be on the game thread -- the managed caller marshals via GameCallbacks.AddMainLoopCall
    // (the terrain-reload pattern), fires once per save/reload, never per frame, so this
    // path allocates nothing (project_rh_snapshot_no_heap_alloc).
    {
        // Not a per-frame path: a small std::string for the log line is safe here.
        std::string message = "ParticlePreview::retriggerLiveEffectInstances: retriggering live instances of '";
        message += effectName;
        message += "'";
        log::info(message.c_str());
    }

    swg::particlePreview::retrigger(effectName);
    return ParticlePreviewResult::Retriggered;
}

bool ParticlePreview::isReplayAvailable()
{
    // Available iff the advertised client resolved particlePreview::replayClientEffect
    // (utinni_replayClientEffect) AND a scene is safe to touch. Null on SWGEmu -> false ->
    // the .cef editor keeps the honest degraded "next scene change / relog" candor.
    return swg::particlePreview::replay != nullptr && Game::isSafeToUse();
}

bool ParticlePreview::replayClientEffect(const char* clientEffectName)
{
    if (!Game::isSafeToUse())
    {
        return false;
    }
    // Not advertised (SWGEmu / pre-v8) -> nothing to play; the editor degrades honestly.
    if (swg::particlePreview::replay == nullptr)
    {
        log::info("ParticlePreview::replayClientEffect: particlePreview::replayClientEffect not advertised "
                  "-- degrading to tier-(b) reload candor.");
        return false;
    }
    if (clientEffectName == nullptr || clientEffectName[0] == '\0')
    {
        log::info("ParticlePreview::replayClientEffect: empty client-effect name -- nothing to play.");
        return false;
    }

    // Real cooperative hook: hand the just-saved .cef logical name to the provider, which
    // re-fetches the .cef + referenced particle/sound templates (so the edit shows) and
    // re-plays it FRESH on the local player via the public ClientEffectManager::playClientEffect.
    // MUST already be on the game thread -- the managed caller marshals via
    // GameCallbacks.AddMainLoopCall, once per preview, so this path allocates nothing per frame.
    {
        std::string message = "ParticlePreview::replayClientEffect: replaying client effect '";
        message += clientEffectName;
        message += "'";
        log::info(message.c_str());
    }

    return swg::particlePreview::replay(clientEffectName);
}
} // namespace utinni
