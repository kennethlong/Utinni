# Phase 24 — Session Handoff (2026-07-09): Goal A+ opened — lookAt-target accessor request + WS guards

Resume pointer after the 07-09 session that opened **Goal A+** (the cheap rung of the 07-03 handoff's
§6 ladder, user-selected): drafted + adversarially reviewed + **delivered** the v16 provider request
for the player lookAt-target id accessor, and landed the consumer-side blast-radius prerequisite
(WorldSnapshot `Node*`-walk guards + a CI audit invariant). Everything committed + pushed on Utinni;
working tree clean. **The session ended AWAITING the provider round-trip — resume at §5.**

**Live pointers:**
- **The request (source of truth):** `24-PROVIDER-REQUEST-lookattarget-accessor.md` (rev-2) — read it
  in full before doing the bind wave; §2 is the consumer plan this handoff summarizes.
- **Delivered copy:** `D:/Code/swg-client-v2/.planning/handoff/2026-07-09-utinni-lookattarget-accessor.md`
  (UNTRACKED in his tree — the provider instance commits it with its delivery, per convention).
- **Running arc ledger:** `project_phase24_editor_unlock_inflight` memory (07-09 entry appended).
- **Prior handoff:** `24-SESSION-HANDOFF-2026-07-03-sysmsg-worldsnapshot.md` (all its arcs CLOSED).

---

## 0. STATUS in one paragraph

Post-v2.1 editor-unlock arc, Goal A+ in flight. Wave 1 (target-change) is live on the advertised
client but the target is invisible downstream: the only lookAt-target read is the raw
`CreatureObject + 1432` 2002-layout offset, degraded to 0 on advertised. The v16 ask advertises the
real read as an `extern "C"` int64 shim; combined with a consumer reroute through the v12
`network::getObjectById` row, `Game.PlayerLookAtTargetObject` starts returning real objects there →
target-aware affordances (inspector auto-refresh first). The dangerous half — wave-1's stability
argument was "the WorldSnapshot raw walk is unreachable because the target resolves null", which this
feature deletes — is ALREADY neutralized: all `Node*`-producing walks are gated and a CI invariant
keeps them gated. Contract UNCHANGED this session: **v15 / 120 names / 118 bound** (the v16 bump
happens only in the post-delivery bind wave). SWGEmu byte-unchanged throughout (D-00).

## 1. What landed this session (all committed + pushed, Utinni `master`)

| Commit | What |
|--------|------|
| `17ff235` | docs: the provider request rev-2 (post 2-AI adversarial review — Codex + in-harness Sonnet) |
| `750d213` | **feat: WorldSnapshot Node*-walk guards + CI audit invariant** (the §2.3 hard prerequisite, landed FIRST) |
| `cfb3ba8` | docs: request §2.3 updated — guards are a landed fact, harness = the audit check (not the originally-promised Catch2 case) |

Plus (not a commit): the request copied to the provider handoff dir (see Live pointers).
UtinniPlugins: untouched this session. swg-client-v2: untouched except the untracked handoff copy.

## 2. The ask (one row, v15 → v16) — condensed

| Field | Value |
|-------|-------|
| Contract name | `game::getPlayerLookAtTargetId` |
| Provider symbol | `extern "C" __int64 __cdecl utinni_getPlayerLookAtTargetId(void)` (nothrow) |
| Body | `Game::getPlayerCreature()` → `getLookAtTarget().getValue()`, else 0 |
| Return | full 64-bit NetworkId value; 0 = no player / no target (`cms_invalid` is `NetworkId(0)`) |
| Mechanics | constant `&fn`; 1 NAME ADD → **121 names / 119 bound**; `ENGINE_HOOKPOINTS_VERSION` 15→**16**; `.h/.inc` byte-identical resync + sha256 both repos |

**Why a shim, not the member (do not re-litigate):** provider `CreatureObject::getLookAtTarget()` is
INLINE (clientGame CreatureObject.h:882) returning `const CachedNetworkId&`; `CachedNetworkId` embeds
`mutable Watcher<Object>` (unmodeled consumer-side) — reading through that reference is the 07-03
sysmsg ABI trap in the READ direction. Only a primitive (int64 in EDX:EAX) crosses. Player-scoped =
no `this`, no MI real-entry subtlety. Semantics are strictly **lookAt/selection** target (the slot
the advertised `setTarget`→`setLookAtTarget` row writes) — NOT NGE intended/combat target.

## 3. THE TWO REVIEW-CAUGHT TRAPS (bake these into the bind wave, they are the whole point of rev-2)

1. **The shim alone changes NOTHING.** `Game::getPlayerLookAtTargetObject()` (game.cpp:736) resolves
   via `Object::getObjectById` → `Network::getCachedObjectById`, which wave 1 **nulls on advertised**
   (network.cpp:74, unadvertised 0x00B30160 literal). The advertised branch must call
   **`Network::getObjectById(id)` DIRECTLY** — the v12 row, already bound + smoke-proven in
   `hkSetTarget` (creature_object.cpp:145). Without the reroute, the acceptance smoke fails silently
   (compiles fine, returns null forever). (Sonnet HIGH, verified against the tree.)
2. **64-bit id discipline.** `swgptr` is `uint32_t`; NGE NetworkIds carry high bits (cluster-id
   mask). The shim's value lives in an **`int64_t` local** and is passed to
   `Network::getObjectById(const int64_t&)` — never round-tripped through the `swgptr`-returning
   `getPlayerLookAtTargetObjectNetworkId()` (that function keeps its advertised-returns-0 degrade;
   its return is a *pointer into the creature*, meaningless on advertised).

## 4. The landed prerequisite (`750d213`) — what it is, how it's enforced

- All **8 `Node*`-producing functions** in `UtinniCore/swg/scene/world_snapshot.cpp` now open with
  `if (offlineSnapshotUnavailable()) return nullptr;` (first statement, before ANY member touch):
  `getNodeById(int)`, `getNodeById(int, Object*)` (dispatcher — gates before the raw `parentObject`
  deref chain), `findChildNode`, `getNodeByIdWithParent`, `getLastNode`, `Node::getChildById`,
  `Node::getChildAt`, `Node::getLastChild`; plus `Node::getChildCount` → 0. Rationale: the
  reader/writer singleton is the hardcoded SWGEmu RVA `0x1913E94` — garbage base on advertised.
- **Invariant established:** every `Node*`-producing entry point is gated (`getNodeAt`/
  `getNodeByNetworkId`/`addNode`/load-path already were) → no `Node*` can exist on advertised → the
  `Node` methods and the ~11 managed `WorldSnapshotImpl` call sites all degrade to their existing
  null-node branches (verified: `OnTarget` → gizmo off, panel cleared — same UX as today).
- **Harness:** `scripts/audit-advertised-rva-safety.ps1` **§2b** — fails CI on any `Node*`-returning
  body in world_snapshot.cpp that doesn't open with the gate. Negative-tested against the pre-guard
  tree (8/8 flagged, exit 1). Auto-catches future additions. Runs in the existing ci.yml audit step.
- Behavior-neutral on BOTH clients today (gate false on SWGEmu; gated paths unreachable on advertised
  until the slot lights up). Full Catch2 suite green post-change: **634 assertions / 43 cases**
  (endpoints subset 420/10). clang-format clean, RVA audit 323 (no new literals), no ABI rebless
  (no public-surface change — .cpp bodies + a script only).
- **Audit findings that DISSOLVED on facts** (don't re-chase): "audit all onTarget subscribers" —
  there is exactly ONE managed subscriber (`WorldSnapshotImpl.OnTarget`; MiscPanel deliberately polls
  pure getters, MiscPanel.cs:42,283), and native/plugin subscribers already receive a resolved
  non-null `Object*` today via `hkSetTarget` (wave 1) — no new exposure class. Managed raw
  `Object.NetworkId`/`.ParentObject` property reads at the WS call sites are value-garbage-but-
  memory-safe (in-bounds reads of a live object) and flow only into the gated natives.

## 5. RESUME HERE — the ladder

1. **Provider round-trip (maintainer action):** run the provider Claude instance on
   `D:/Code/swg-client-v2` against `.planning/handoff/2026-07-09-utinni-lookattarget-accessor.md`.
   Expect: v16 table (121 names), the shim exported, `.h/.inc` resynced, HANDBACK file written.
2. **VERIFY delivery before binding** (the v14 lesson — provider left the tree uncommitted once):
   version bump present, row target correct, exe rebuilt (`dumpbin /exports` shows
   `utinni_getPlayerLookAtTargetId` — or confirm via the GetEngineHookPoints table row), tree
   COMMITTED, sha256 of `engine_hookpoints.{h,inc}` identical across repos.
3. **Consumer bind wave** (one commit, per request §2):
   - New null-starting slot: `namespace swg::game { using pGetPlayerLookAtTargetId =
     int64_t(__cdecl*)(); extern ... }` in endpoints_bindings.cpp (+ definition in game.cpp),
     binding row `{"game::getPlayerLookAtTargetId", ...}` + the kBindingNames entry.
   - Re-sync `engine_hookpoints.{h,inc}` byte-identical from the provider tree + bump
     `ENGINE_HOOKPOINTS_VERSION` to 16 (comes with the resync).
   - **Reroute** `Game::getPlayerLookAtTargetObject()` per §3 above (advertised: shim → null-check →
     `Network::getObjectById(int64 local)`; SWGEmu byte-unchanged). Log once on advertised if the
     slot stays null ("row not advertised — provider < v16") — distinct from the no-target case.
   - Bump the drift gates: `endpoints_bindings.cpp:801-802` static_asserts and
     `endpoints_tests.cpp:219/230/257` REQUIREs → **121 / 119**.
   - Headless gates: MSBuild UtinniCore → Catch2 full suite → clang-format → RVA audit → managed
     `dotnet test --no-build`. `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs` after.
4. **Affordance wave (managed, after 3 smokes clean):** inspector auto-refresh on target change —
   subscribe an argless managed onTarget consumer that re-reads `Game.PlayerLookAtTargetObject` and
   re-renders the MiscPanel readout via the advertised-safe getters (the Bucket A-2 set:
   ObjectTemplateName / NetworkIdValue / SharedAppearanceFilename / Type / ParentCellName /
   Transform). Do NOT add parameters to existing public managed methods (binary-compat rule) — new
   additive API only if a target-carrying callback is ever wanted.
5. **Maintainer live smoke (request §3):** advertised NGE client → target an NPC → log shows the id
   + non-null `Object*` via the v12 resolve; inspector populates; un-target → clean 0/null; target
   then walk out of range → id non-zero, resolve null, NO crash (staleness case); Snapshot panel
   still shows no-node (expected — the working Snapshot editor on advertised remains **Goal B**);
   no crash across scene changes with a target held. SWGEmu quick pass: snapshot editor node
   selection + gizmo unchanged (D-00).

If the provider comes back with pushback on the row shape, the alternatives ladder is in the request
§1 ("why a shim") — hold the primitives-only line; a `const CachedNetworkId&`-returning member row is
NOT acceptable (the §3 trap is the reason this is rev-2 and not rev-3).

## 6. OPERATIONAL FACTS (carried forward + this session's additions)

- **Contract:** v15 / 120 names / 118 bound (2 carve-outs) — UNCHANGED until the bind wave. Drift
  gates as listed in §5.3. Re-sync `.h/.inc` byte-identical + sha256-verify both repos on any change.
- **Build (native):** `MSBuild Utinni.sln -t:UtinniCore -p:Configuration=Release -p:Platform=x86 -m
  -nologo -v:minimal -nodeReuse:false` (MSBuild at
  `D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`). Always
  `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs` after (only AFTER any ABI test runs).
- **⚠️ DLL-lock:** a running injected `SwgClient_r.exe` locks `bin/Release/UtinniCore.dll` — close
  the client before rebuilding (checked clear this session).
- **Headless gates:** Catch2 `bin/Release/UtinniCore.Tests.exe` full = 634/43 (endpoints subset
  420/10); clang-format-20 at `VC/Tools/Llvm/x64/bin/clang-format.exe` (NOT the ARM64 one); RVA
  audit `scripts/audit-advertised-rva-safety.ps1` = 323 sites baselined **+ the new §2b
  world_snapshot guard invariant** (run via the PowerShell tool — `$PSScriptRoot` breaks under bash
  `powershell -File`); managed `dotnet test UtinniCoreDotNet.Tests --no-build -c Release` (855/855
  +1 skip).
- **ABI rebless:** none needed this session; the bind wave shouldn't need one either UNLESS the
  reroute adds/changes `utinni::` public methods (a body-only branch change in
  getPlayerLookAtTargetObject does not).
- **⚠️ GSD hook misfire (new, this session):** something stamped `.planning/STATE.md` mid-session
  with a bogus regression (shipped→executing, "Phase 05 planning complete", 100%→95%) — same
  mangling class as the known `phase.complete` bug. Restored from HEAD. **Check `git status` for a
  dirty STATE.md before every commit and restore it if mangled; do not commit it.**
- **Live smoke = maintainer only** (advertised client from `D:\Code\swg-client-v2\stage\`; .tre/.toc
  from `D:\Code\SWGSource Client v3.0\`).
- **Cross-AI crew:** Codex reliable (`codex exec --skip-git-repo-check -`, pipe prompt on stdin);
  Agent-tool `sonnet` (bare alias) reliable — its tree-verifying review caught the §3 traps;
  cursor-agent deprioritized (flaked on prompt delivery).
- **CI:** self-hosted runner, push-only; the 3 pushed commits exercise the new audit check in the
  next run — if CI reds on the audit step, read the §2b failure list (it names the ungated function
  and line).
