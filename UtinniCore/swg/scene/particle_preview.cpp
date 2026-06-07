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
    // No native hook is wired this phase, so the retrigger is never available --
    // regardless of injection state. The editor uses this to keep
    // "Preview in client" state-encoded-disabled and to select the honest
    // tier-(b) reload-candor badge ("Reloads on next scene change or relog.").
    //
    // When the real hook lands, this becomes: `return <hook wired> && Game::isSafeToUse();`
    return false;
}

ParticlePreviewResult ParticlePreview::retriggerLiveEffectInstances(const char* effectName)
{
    // Documented no-op stub. Performs no work and allocates nothing -- this keeps
    // the seam heap-free by construction and installs no per-frame callback, so it
    // cannot regress SWG's allocator (project_rh_snapshot_no_heap_alloc).

    // Honest status: distinguish "client not injected/safe" from "hook not
    // implemented" so the editor can show the right disabled-state reason. Neither
    // branch does any retrigger work this phase.
    if (!Game::isSafeToUse())
    {
        return ParticlePreviewResult::NotInjected;
    }

    // Not a per-frame path: this fires once per save/reload, so building a small
    // std::string for the log line is safe (no hot-path allocation concern).
    std::string message =
        "ParticlePreview::retriggerLiveEffectInstances: live in-client hot-retrigger is not wired "
        "this phase (15-03 spike: no reachable native hook) -- editor degrades to tier-(b) reload "
        "candor. effect='";
    message += (effectName != nullptr) ? effectName : "";
    message += "'";
    log::info(message.c_str());

    return ParticlePreviewResult::NotReachable;
}
} // namespace utinni
