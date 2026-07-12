# Phase 24 — Session Handoff (2026-07-12): Goal A+ CLOSED — lookAt-target read live end-to-end

Closure record for **Goal A+** (the 07-09 handoff's in-flight arc): the provider delivered v16 the
same day the request was picked up, the consumer bind + affordance waves landed, and the maintainer
live smoke **PASSED** ("target is working and the display is accurate, no glitches"). The advertised
NGE client now has a working target-aware inspector: target anything in-world and the TJT Misc-panel
readout auto-populates with the full advertised-safe getter set.

**Supersedes:** `24-SESSION-HANDOFF-2026-07-09-goal-aplus-lookattarget.md` (its §5 ladder is fully
executed; §3's two review-caught traps were honored in the bind wave and are re-documented below only
as landed facts).

---

## 0. What is now true

- **Contract v16 / 121 names / 119 bound.** Provider swg-client-v2 `75107abfd` (HANDBACK
  `2026-07-12-utinni-lookattarget-accessor-HANDBACK.md`); consumer Utinni `c506c6d`; TJT affordance
  UtinniPlugins `9c5845c`. All pushed; CI green (incl. the §2b WorldSnapshot guard invariant).
- `Game::getPlayerLookAtTargetObject()` returns REAL objects on the advertised client: v16 shim
  (`utinni_getPlayerLookAtTargetId`, int64 NetworkId value) → null-check → **v12
  `Network::getObjectById(int64 local)`** (the reroute; `Object::getObjectById`'s cached path stays
  wave-1-nulled). SWGEmu path byte-unchanged (D-00).
- **Inspector auto-refresh:** MiscPanel subscribes an argless onTarget consumer → re-reads
  `Game.PlayerLookAtTargetObject` → re-renders the readout (`[Target]` header) via the Bucket A-2
  advertised-safe getters. Engine reads stay on the game thread (the v16 shim is game-thread-only);
  only the finished string BeginInvokes to the UI. MiscPanel is now the SECOND managed onTarget
  subscriber (the 07-09 "exactly one" blast-radius fact is superseded — both degrade clean on null).
- **Smoke evidence** (utinni.log 14:12–14:14): endpoints 119/119 + WS-3 clean; ~10 target cycles,
  real ids → non-null resolves, un-target 0x0 → clean null; full orderly teardown, no crash, no new
  dump. Maintainer confirmed the readout populates accurately on target change.

## 1. Commits this session

| Repo | Commit | What |
|------|--------|------|
| swg-client-v2 | `75107abfd` | provider v16: shim + row + version bump (delivered exactly per rev-2, one location-only deviation: shim lives in CreatureObject.cpp — exe-TU include constraint, the setTarget precedent) |
| Utinni | `c506c6d` | bind wave: .h/.inc resync (sha-verified), slot, reroute, 121/119 gates |
| UtinniPlugins | `9c5845c` | affordance wave: MiscPanel onTarget auto-refresh + BuildReadoutText split |

## 2. Operational notes worth carrying forward

- **Stale-test-exe trap:** `MSBuild -t:UtinniCore` does NOT rebuild `UtinniCore.Tests.exe`. The tell:
  the Catch2 assertion count doesn't move after a contract bump. Rebuild `-t:UtinniCore_Tests`
  before trusting the gate. (Fresh counts: full **637/43**, endpoints subset **423/10**.)
- The RVA audit script must be invoked directly in a PowerShell session (`& script.ps1`) — nested
  `powershell -File` breaks `$PSScriptRoot` (known; bit again this session).
- The 07-09 CI "failure" on `0389a40` was a phantom — the run was canceled when the machine (and
  self-hosted runner) went down at end of session. Re-run passed. Check for `The operation was
  canceled` before diagnosing a red run that coincides with a session end.
- STATE.md hook misfire did NOT recur this session.

## 3. Residual / follow-on ladder (nothing in flight)

- **SWGEmu D-00 quick pass** (snapshot editor node selection + gizmo) — nominally still open from
  the smoke list; native SWGEmu paths are byte-unchanged so risk is nil, but the MiscPanel
  auto-refresh DOES also run on SWGEmu now (harmless getter reads). Fold into the next SWGEmu smoke.
- **Goal B** (Snapshot editor on advertised): unchanged, milestone-scale, needs the provider
  accessor-API design consult before attempting (see the 07-03 handoff §6).
- Other §6 ladder options (v2.2 pivot via `/gsd:new-milestone`, MISC/INPUT coverage completion)
  remain as listed in the 07-03/07-09 handoffs.
