---
phase: 22
slug: clienteffect-editor
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-17
---

# Phase 22 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from `22-RESEARCH.md` §"Validation Architecture". Per-task rows below are
> requirement/behavior-keyed; the planner refines them to concrete task IDs.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (`UtinniCoreDotNet.Tests`, `Utinni.Cli.Tests`) |
| **Config file** | none — existing test projects, no new config |
| **Quick run command** | `dotnet test --no-build --filter "FullyQualifiedName~ClientEffect"` |
| **Full suite command** | MSBuild `Utinni.sln /p:Configuration=Release /p:Platform=x86` then `dotnet test --no-build` |
| **Estimated runtime** | quick subset ~<30s; full suite ~2–4 min |

> **Reminder (AGENTS.md):** build with VS2026 MSBuild, never `dotnet build` (MSB3823 on WinForms `.resx`).
> Run xUnit with `--no-build` after the MSBuild pass.

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --no-build --filter "FullyQualifiedName~ClientEffect"` (codec/verb subset, <30s)
- **After every plan wave:** Run full `dotnet test --no-build`
- **Before `/gsd:verify-work`:** Full suite green + maintainer live-smoke (Preview honest-candor + a real save→reload)
- **Max feedback latency:** ~30 seconds (quick subset)

---

## Per-Task Verification Map

> Behavior/requirement-keyed (tasks not yet planned). Planner maps each to a concrete `22-PP-TT` task ID.

| Behavior | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|----------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| Byte-exact roundtrip, all D-13 fixtures (no edit) | PROD-W2-CFX-01 | — | N/A | unit | `dotnet test --no-build --filter "Name~Roundtrip&Name~ClientEffect"` | ❌ W0 | ⬜ pending |
| Length-changing string edit re-parses; untouched chunks identical | PROD-W2-CFX-01 | T-22-codec | half-understood rewrite rejected | unit | `... --filter "Name~ClefStringEdit"` | ❌ W0 | ⬜ pending |
| Add / remove / reorder command → version FORM length re-rolls, re-parses | PROD-W2-CFX-01 | T-22-codec | N/A | unit | `... --filter "Name~ClefListMutation"` | ❌ W0 | ⬜ pending |
| Scalar/flag/color CPAP/CLGT/CAMS edit byte-exact except target | PROD-W2-CFX-01 | — | N/A | unit | `... --filter "Name~ClefFieldEdit"` | ❌ W0 | ⬜ pending |
| Unknown version + unknown command tag → raw-fallback, roundtrip byte-exact | PROD-W2-CFX-01 | T-22-malformed | never hard-abort / throw | unit | `... --filter "Name~ClefRawFallback"` | ❌ W0 | ⬜ pending |
| Loose-override save lands under `<root>\loose\`, fail-closed containment | PROD-W2-CFX-01 | T-22-path | path traversal rejected | unit | `... --filter "Name~ClefLooseOverride"` | ❌ W0 | ⬜ pending |
| (folded) terrain override save lands under `<root>\loose\` | folded todo | T-22-path | path traversal rejected | unit | `... --filter "Name~TerrainLooseSubdir"` | ❓ verify | ⬜ pending |
| `decode-effect` / `decode-iff` CLEF branch JSON envelope shape | PROD-W2-CFX-02 | — | N/A | integration | `... --filter "Name~DecodeEffect"` | ❌ W0 | ⬜ pending |
| `roundtrip-effect` exit codes (0/2/3) | PROD-W2-CFX-02 | — | N/A | integration | `... --filter "Name~RoundtripEffectCommand"` | ❌ W0 | ⬜ pending |
| `apply-save-effect` verify + atomic commit + fail-closed | PROD-W2-CFX-02 | T-22-path | path traversal rejected; non-editable node rejected | integration | `... --filter "Name~ApplySaveEffect"` | ❌ W0 | ⬜ pending |
| CLI `--help` enumerates the new `effect-*` verbs (D-12 smoke) | PROD-W2-CFX-02 | — | N/A | smoke | `... --filter "Name~CliHelpEnumerates"` | ❓ verify | ⬜ pending |
| Reference-validation against SOE load order (field order/values) | PROD-W2-CFX-02 | — | N/A | unit | `... --filter "Name~ClefLoadOrder"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Both-Lineage Golden Fixture Matrix (D-13 + D-14)

Synthesized, hand-emitted via `IffWriter` (deterministic, tiny), committed:

| Fixture | CPAP version | Commands present | Purpose |
|---------|:---:|---|---|
| `clef_v0001_cpap.iff` | 0001 | CPAP | name+time only field set |
| `clef_v0002_cpap.iff` | 0002 | CPAP | + softParticleTerminate bool8 |
| `clef_v0003_cpap.iff` | 0003 | CPAP | + min/max scale + min/max rate |
| `clef_v0003_all5.iff` | 0003 | CPAP+PSND+CLGT+CAMS+FFBK | full command coverage, ordering |
| `clef_v0001_all5.iff` | 0001 | all 5 | stable-command coverage at oldest version |
| `clef_unknown_version.iff` | 9999 | — | raw-fallback whole CLEF FORM |
| `clef_unknown_command.iff` | 0003 | CPAP + `XXXX` unknown tag | raw-preserve unknown chunk, re-emit verbatim |
| `clef_empty.iff` | 0003 | (none) | empty version FORM edge case |

**D-14:** extract ONE real CLEF `.iff` per reachable lineage via `utinni-cli` TRE verbs (dogfood);
run `roundtrip-effect` as an extra byte-exact check; confirm which versions feed the synthesized
matrix. Keep real assets OUT of committed goldens unless small + unencrypted.

---

## Wave 0 Requirements

- [ ] CLEF golden fixtures (the 8 synthesized `.iff` above) — covers PROD-W2-CFX-01/02
- [ ] `ClientEffectCodecTests.cs` — roundtrip / string-edit / list-mutation / field-edit / raw-fallback / load-order
- [ ] `ApplySaveEffectCommandTests.cs`, `RoundtripEffectCommandTests.cs`, `DecodeEffectTests.cs`
- [ ] `ClefLooseOverrideTests.cs` — assert `<root>\loose\` destination + fail-closed containment
- [ ] Verify/add a terrain `<root>\loose\` destination test (folded-todo close-item #3 — may already exist from 21-06)
- [ ] Confirm an existing CLI `--help`-enumerates pattern test, or add one for the `effect-*` verbs (D-12)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| "Preview in client" replay action (D-07/D-08) | PROD-W2-CFX-01 (opt-in) | Requires a live injected SWG session; no headless path. **Research A-finding:** the Particle preview hook (`IsRetriggerHookReachable()`) is hardcoded `false` this build → expect honest-candor (disabled button + honest tier), not a firing effect. | Inject into live SWG; open a ClientEffect; click Preview; confirm honest-candor messaging (no over-promise) and no crash on the game-thread dispatch path. |
| Real save → client reload picks up `<root>\loose\` override | PROD-W2-CFX-01 | Live-SWG eyeball; maintainer-only checkpoint. | Save an edited CLEF to loose override; trigger a scene change; confirm the edited effect loads (or honest "next scene change" candor). |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
