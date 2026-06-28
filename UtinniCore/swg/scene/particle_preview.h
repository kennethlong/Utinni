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

// ----------------------------------------------------------------------------
// Particle live-in-client preview seam (D-09 / PROD-W2-PRT).
//
// EARLY SPIKE STUB (plan 15-03). The live in-client "hot-retrigger loaded
// instances" preview is the one greenfield No-Analog item. The reachability
// spike (.planning/.../15-PARTICLE-PREVIEW-HOOK.md) found NO clean reachable
// native retrigger entry this phase:
//   * ParticleManager is a static debug/config singleton -- it holds NO live
//     instances and exposes no retrigger entry (rejected).
//   * ClientEffectManager DOES hold the live instances (m_particleSystems), but
//     its add/remove/list surface is private; there is no public
//     enumerate-and-restart entry, and Utinni has not yet established RVAs for
//     the engine-level retrigger path (AppearanceTemplateList::fetch +
//     ParticleEffectAppearance::restart()).
//
// So this file ships a STABLE, named seam now -- retriggerLiveEffectInstances()
// returns RetriggerResult::NotReachable -- so the Particle editor (15-06) wires
// the degraded tier-(b) reload-candor badge honestly and the live smoke (15-08)
// is not blocked. The real native hook lands later behind this SAME signature.
//
// Format/lifecycle understanding was obtained by READING the read-only SOE
// swg-client-v2 corpus (clientParticle / clientEffect / sharedObject). No SOE
// code or identifiers are copied; this implementation is original under MIT.
//
// Hot-path discipline (project_rh_snapshot_no_heap_alloc): when the real hook
// is implemented it MUST marshal ONCE per save/reload (not per frame) and stay
// heap-free on any path that could run per frame -- a per-frame
// std::vector::reserve() previously fragmented SWG's allocator and crashed
// scene change at 0x0051fb0a. The stub honors this by allocating nothing and
// installing no per-frame callback.
// ----------------------------------------------------------------------------

#pragma once

#include "utinni.h"

namespace utinni
{
// Outcome of a hot-retrigger request. CppSharp-exposable plain enum so the
// managed editor seam can branch on reachability without parsing strings.
enum class ParticlePreviewResult
{
    // No reachable native retrigger hook this phase (the documented spike
    // outcome). The editor degrades: "Preview in client" stays disabled and the
    // reload badge shows the tier-(b) candor "Reloads on next scene change or relog."
    NotReachable = 0,

    // The client is not currently injected / safe to use (no live scene).
    NotInjected = 1,

    // Reserved for the real hook: the edited effect's template was re-fetched and
    // its live scene instances were restarted. NOT returned by the stub.
    Retriggered = 2,
};

// Live in-client particle preview seam. Static facade mirroring the
// utinni::WorldSnapshot / utinni::Terrain scene-side export shape so the eventual
// real hook drops in behind the same surface (CON-N-01 detour-table pattern,
// CON-H-03 single-source RVAs via UTINNI_API).
class UTINNI_API ParticlePreview
{
public:
    // Returns true when a real native retrigger hook is wired AND the client is
    // safe to use. The stub always returns false -- the editor uses this to
    // state-encode the "Preview in client" button (disabled) and pick the honest
    // degraded reload badge copy.
    static bool isRetriggerAvailable();

    // Re-trigger the live scene instances of effectName after a save/reload.
    // Marshals ONCE per call onto the game thread (never per frame) when wired;
    // the stub performs no work and allocates nothing.
    //
    // effectName: the .prt / client-effect logical name just saved/reloaded
    //             (e.g. "appearance/pt_smoke.prt"); may be null/empty for the stub.
    // Returns ParticlePreviewResult::NotReachable in this phase (15-03).
    static ParticlePreviewResult retriggerLiveEffectInstances(const char* effectName);

    // Bucket B-2 (v8): live .cef RE-PLAY. True when the advertised provider entry
    // particlePreview::replayClientEffect (utinni_replayClientEffect) is resolved AND the
    // client is safe to use. Drives the ClientEffect editor's "Preview in client" affordance.
    static bool isReplayAvailable();

    // Re-play a client effect FRESH on the local player (the transient-.cef case the
    // restart-based retrigger above cannot cover -- muzzle/hit/explosion finish before the
    // editor saves). Provider re-fetches the .cef + referenced templates so the edit is
    // visible. MUST be called on the game thread (the managed caller marshals via
    // GameCallbacks.AddMainLoopCall, once per preview).
    //
    // clientEffectName: the .cef logical name just saved (e.g. "clienteffect/foo.cef").
    // Returns true iff the provider played the effect; false if not advertised / unsafe /
    // null name / the provider's playClientEffect failed (no player, bad name).
    static bool replayClientEffect(const char* clientEffectName);
};
} // namespace utinni
