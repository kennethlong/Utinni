# Provider Request — Player lookAt-target id accessor (Goal A+, one shim row)

**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Date:** 2026-07-09
**Status:** REQUEST rev. 2 — incorporates a 2-AI adversarial review (Codex, Sonnet). The cheap rung
of the post-wave-1 ladder ("Goal A+" in the 2026-07-03 handoff). One `extern "C"` shim row, one v16
bump, ~5 lines on your side (plus the usual contract mechanics, §1).
**Source of truth:** this file in the Utinni repo. Copy into `swg-client-v2/.planning/handoff/` as
`2026-07-09-utinni-lookattarget-accessor.md`.
**Self-contained:** act on this without reading the Utinni Phase-24 plans. It cites exact sites on
both sides (review-verified).

---

## 0. TL;DR

Wave 1 (Utinni `e7eec33`, smoke-PASSED 07-03) un-gated target-change on the advertised client by
making the player's lookAt-target read **degrade to "no target"**: the consumer's only source for it
is a raw `CreatureObject + 1432` byte-offset from the 2002 SWGEmu layout — unusable on your NGE
build, so it returns 0 there. Targeting works, but nothing downstream can *see* the target.

This request advertises the real read. Paired with a consumer wave (§2) that reroutes the resolve
through the already-delivered v12 `network::getObjectById` row,
`Game::getPlayerLookAtTargetObject()` starts returning real `Object*`s on the advertised client →
target-aware affordances light up (selected-object inspector auto-refresh on target change, target
readouts, follow-on editor UX).

**Shape: one `extern "C"` shim returning the raw 64-bit id value.** Per the rule both sides adopted
after the 07-03 sysmsg WRITE-AV: only primitives and pointers cross the advertised boundary raw; any
C++ object needs a provider-side shim. The natural member (`CreatureObject::getLookAtTarget()`)
fails that test twice — see §1.

**Semantics (deliberate):** this is strictly the **lookAt/selection target** — the same slot the
already-advertised `creatureObject::setTarget` row writes (your `CreatureObject::setLookAtTarget`,
`engine_hookpoints.inc:276`) and the same slot the consumer's SWGEmu `+1432` read models. It is NOT
the NGE intended/combat target (`getIntendedTarget`, a distinct member) — if an editor ever needs
that, it will be a separate request, not a re-point of this row.

## 1. The row requested (one CALLED row, v15 → v16)

| Field | Value |
|-------|-------|
| **Contract name** | `game::getPlayerLookAtTargetId` |
| **Provider symbol** | new `extern "C"` shim, e.g. `utinni_getPlayerLookAtTargetId` |
| **Shim signature** | `extern "C" __int64 __cdecl utinni_getPlayerLookAtTargetId(void)` — `noexcept`/nothrow; no C++ objects or exceptions cross the boundary |
| **Shim body** | `CreatureObject const * const player = Game::getPlayerCreature(); return player ? player->getLookAtTarget().getValue() : 0;` |
| **Return** | the player's lookAt-target `NetworkId` **value** (full 64 bits — cluster-id bits included); `0` when no player / no target (`NetworkId::cms_invalid` is `NetworkId(0)` — `NetworkId.cpp:17`) |
| **Mechanism** | constant `&fn` of the shim (plain static, C linkage, primitive-only signature) |
| **Unblocks** | `Game::getPlayerLookAtTargetObject()` on the advertised client → target-aware consumer affordances (§2.6) |

**Why a shim and not a `creatureObject::getLookAtTargetId` member row:**

- `CreatureObject::getLookAtTarget()` is **inline** (`clientGame/.../CreatureObject.h:882` on your
  tree) — no reliable out-of-line address to advertise without forcing an instantiation anyway.
- It returns `const CachedNetworkId&`, and `CachedNetworkId` embeds a `mutable Watcher<Object>`
  (`sharedObject/.../CachedNetworkId.h:67`). The consumer does not model `Watcher`; reading through
  that reference is the sysmsg layout trap in the read direction. The shim collapses it to an
  `__int64` in EDX:EAX — nothing but a primitive crosses (both reviewers confirmed this is
  ABI-clean x86 cdecl on every MSVC vintage involved).
- Player-scoped means no `this` parameter and no MI real-entry subtlety (contrast
  `creatureObject::setTarget`, which needed `pmfRealEntry`). The consumer only ever wants the
  *player's* lookAt target here; ids of arbitrary creatures already arrive via the `setTarget`
  detour argument.

**Contract mechanics (the real "done", not just the 5 lines):** 1 NAME ADD → **121 names / 119
bound consumer-side**; bump `ENGINE_HOOKPOINTS_VERSION` 15 → **16**; re-sync
`engine_hookpoints.{h,inc}` byte-identical + sha256-verify both repos; your coverage self-check
green at 121. Consumer-side the slot starts **null** (no pre-existing SWGEmu literal in this slot,
so no version-skew guard needed; a plain null-check gates it — and the consumer logs
"row not advertised (provider < v16)" once on advertised when the slot stays null, so a skewed
pairing is diagnosable and distinct from "no target").

**Threading / staleness:** consumer calls it on the game thread only (target-change callback +
panel refresh), on demand — not per-frame. The returned id is treated as a *hint*: it is
immediately resolved via the v12 row, and a null resolve (object unloaded/out of range) is a normal
outcome, not an error.

## 2. Paired consumer plan (context — does not block you)

Why the row will actually be reached, and why it can't destabilize what wave 1 stabilized. Items
2–4 are **hard prerequisites that land before (or in the same commit as) the slot binding** — the
review flagged that wave 1's safety argument ("the WorldSnapshot walk is unreachable because the
target resolves null") is exactly the property this request removes.

1. **New null-starting slot** `swg::game::getPlayerLookAtTargetId`
   (`using pGetPlayerLookAtTargetId = int64_t(__cdecl*)();`), bound to `game::getPlayerLookAtTargetId`.
2. **`Game::getPlayerLookAtTargetObject()` reroute** (`game.cpp:736`) — review-caught correction:
   today it resolves via `Object::getObjectById` → `Network::getCachedObjectById`, which wave 1
   **nulls on advertised** (`network.cpp:74`) — the shim alone would change nothing. The advertised
   branch therefore calls **`Network::getObjectById(id)` directly** (the v12 row, already bound +
   smoke-proven in `hkSetTarget`), with the shim's id in an **`int64_t` local** — never narrowed
   through the `swgptr`-returning `getPlayerLookAtTargetObjectNetworkId()` (`swgptr` is 32-bit;
   NGE ids carry high bits). That function keeps its advertised-returns-0 degrade (its return is a
   *pointer into the creature* — meaningless on advertised). SWGEmu path byte-unchanged (D-00).
3. **Blast-radius guards — LANDED (Utinni `750d213`, ahead of this request being actioned).**
   Every raw 2002-layout walk that becomes reachable now opens with an
   `offlineSnapshotUnavailable()` gate as the **first statement of the body**, before any
   `nodeList`/`parentObject` touch: `WorldSnapshotReaderWriter::getNodeById(int)` (raw `nodeList`
   walk off the hardcoded `0x1913E94` singleton — garbage base on advertised),
   `getNodeById(int, Object*)` (gate at the dispatcher before the `parentObject` deref chain),
   `findChildNode`, `getNodeByIdWithParent`, `getLastNode()`, plus the `Node` child-walkers
   (`getChildById`/`getChildAt`/`getLastChild`/`getChildCount`) for defense in depth.
   **Invariant established:** every `Node*`-producing entry point is gated
   (`getNodeAt`/`getNodeByNetworkId`/`addNode`/load-path already were), so no `Node*` can exist on
   advertised. **Harness:** a CI source-audit check
   (`scripts/audit-advertised-rva-safety.ps1` §2b) fails the build on any `Node*`-returning body
   in `world_snapshot.cpp` that does not open with the gate — negative-tested against the
   pre-guard tree (8/8 flagged); it auto-catches future additions.
4. **Managed call-site audit (done, review-verified).** The only `onTarget` subscriber is
   `WorldSnapshotImpl.OnTarget` (the inspector deliberately polls pure getters —
   `MiscPanel.cs:42,283`); native/plugin subscribers already receive a resolved non-null `Object*`
   today via `hkSetTarget` (wave 1), so this wave adds no new exposure class there. All ~11
   `WorldSnapshotImpl` sites follow `obj != null → GetNodeById(...) → node != null → act`; with §2.3
   the node is always null on advertised → clean no-op (gizmo off, panel cleared — the same UX as
   today). The raw managed `Object.NetworkId`/`.ParentObject` property reads at those sites are
   value-garbage-but-memory-safe on advertised (in-bounds reads of a live object) and flow only
   into the §2.3-gated natives; they are NOT claimed safe via accessor routing. **New** affordance
   code (§2.6) uses the advertised accessor set (`object::getNetworkId`/`getParentCell`/
   `getObjectTemplateName` — the Bucket A-2 inspector precedent) instead of raw fields. The working
   Snapshot editor on advertised stays **Goal B** (your future design consult on the reader/writer
   surface) — nothing here pre-empts it.
5. **Consumer test gates:** `endpoints_bindings.cpp:801-802` static_asserts and
   `endpoints_tests.cpp:219/230/257` REQUIREs bump 120/118 → 121/119 in the binding commit.
6. **Affordances this lights up:** selected-object inspector auto-refresh on target change; target
   id/name readouts. The managed `onTarget` callbacks stay ARGLESS (consumers re-read via
   `Game.PlayerLookAtTargetObject`) — a target-carrying callback would be a NEW additive managed
   API, never added parameters on the existing one (plugin binary-compat rule).

## 3. Acceptance ("done")

`game::getPlayerLookAtTargetId` in the v16 table (**121 names**), the shim resolves non-null and
returns 0 with no target / the target's id with one, `.inc/.h` sha256-identical at v16 in both
repos, coverage self-check green at 121. Maintainer live smoke (advertised NGE client): target an
NPC → consumer log shows the id and a non-null `Object*` from the v12-row resolve (per §2.2 — NOT
the `getCachedObjectById` path); inspector target readout populates; un-target → clean 0/null
degrade; target something then walk out of range → id non-zero, resolve null, no crash (staleness
case); Snapshot panel still shows no-node (expected, Goal B); no crash across scene changes with a
target held. SWGEmu D3D9 smoke unchanged (D-00 — nothing in this wave touches a SWGEmu code path).

## 4. Priority

Low-medium, same class as the sysmsg shim: ~5 provider lines plus the standard v16 contract
mechanics, and the consumer affordances are already designed around it. It is the enabling read for
every future target-aware editor feature on the advertised client, so earlier compounds.
