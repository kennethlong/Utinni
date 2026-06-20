---
phase: 22-clienteffect-editor
plan: 04
subsystem: clienteffect-editor
tags: [clienteffect, clef, tjt-subpanel, editor, tre-handoff, loose-override, live-smoke]
requires:
  - "Plan 01 CLEF codec (MutableClientEffect / ClientEffectDocument / ClefFieldCodec / ClefCommandDefaults)"
  - "Plan 02 effect-* CLI verbs (decode-effect / roundtrip-effect / apply-save-effect)"
  - "LooseOverridePath.Resolve (two-step fail-closed compose, D-10)"
  - "TerrainSubPanel -> FormTerrainEditor launcher idiom (D-04) + FormTreBrowser hand-off (D-09)"
provides:
  - "EffectsSubPanel (docked) launches the roomy singleton FormClientEffectEditor (DEC-C4, D-04)"
  - "FormClientEffectEditor: read-only TRE/loose open, flat command list (rows carry StableId), version-aware typed field editor, raw/hex degrade, add/remove/reorder, byte-exact in-proc loose-override save, honest-candor Preview (D-07)"
  - "TRE Browser 'Open in Effects Editor' hand-off for .cef, gated by EffectHandoffPolicy"
  - "EffectHandoffPolicy (framework-leg, unit-tested) — the TRE->Effects .cef/FORM-CLEF gate"
affects:
  - "PROD-W2-CFX-01 (in-app ClientEffect editor) delivered"
  - "PROD-W2-CFX-02 'both lineages' real-asset CI coverage — partially caveated (see below)"
tech-stack:
  added: []
  patterns:
    - "Deferred SplitContainer SplitterDistance to OnShown (a Dock.Fill split ignores an explicit Size while unparented)"
    - "Handoff gate extracted to a framework-leg *HandoffPolicy class (mirrors Particle/Datatable/StringTable/OT)"
key-files:
  created:
    - "UtinniCoreDotNet/UI/EffectHandoffPolicy.cs"
    - "UtinniCoreDotNet.Tests/UITests/EffectHandoffPolicyTests.cs"
    - "[UtinniPlugins] The Jawa Toolbox/.../Saving/ClientEffectSaveTargets.cs (Task 1)"
    - "[UtinniPlugins] The Jawa Toolbox/.../UI/SubPanels/EffectsSubPanel.cs (Task 1)"
    - "[UtinniPlugins] The Jawa Toolbox/.../UI/Forms/FormClientEffectEditor.cs (Task 1)"
  modified:
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj (register EffectHandoffPolicy)"
    - "[UtinniPlugins] The Jawa Toolbox/.../UI/Forms/FormTreBrowser.cs (Open in Effects Editor + policy)"
    - "[UtinniPlugins] The Jawa Toolbox/.../Plugin.cs (register editor + SubPanel)"
decisions:
  - "Deferred SplitterDistance + panel min-sizes to OnShown with a clamp; the at-construction set threw and dropped the editor to its red failure surface (the live-smoke 'blank window')."
  - "Open/Save-As dialogs default to the loose-override dir and filter on .cef (the real ClientEffect extension), not .iff."
  - "Extracted the TRE->Effects gate into EffectHandoffPolicy so it is CI-testable in the existing UITests lane (the only CI-reachable seam — the form itself lives in UtinniPlugins, which has no test project/CI)."
metrics:
  duration: "Tasks 1-2 pre-pause; Task 3 live smoke + fixes this session"
  completed: 2026-06-19
---

# Phase 22 Plan 04: ClientEffect Editor (EffectsSubPanel + FormClientEffectEditor) Summary

Delivered PROD-W2-CFX-01: a docked **Effects** SubPanel inside The Jawa Toolbox that launches a roomy
singleton `FormClientEffectEditor` (the `TerrainSubPanel → FormTerrainEditor` idiom, D-04). The editor
opens a ClientEffect `.iff`/`.cef` read-only from the TRE Browser (D-09) or an existing loose override
directly, renders a flat command list (each row carrying its `StableId`, REVIEWS HIGH #1) + a version-aware
typed field editor (unknown/truncated commands degrade to read-only raw/hex, never a hard failure —
D-06/D-13), edits string/scalar/flag fields and add/remove/reorders commands, saves a byte-exact loose
override under `<root>/loose/` via the in-proc `ClientEffectSaveTargets` (the source TRE is never modified,
D-09/D-10), and surfaces an honest-candor "Preview in client" (no retrigger this build — D-07).

Tasks 1–2 (SubPanel/Form/SaveTargets + in-proc↔CLI save-parity test + `Plugin.cs`/csproj) were committed
before the mid-phase pause (`b30245d`, `4298e48`, `6361c80`). **Task 3 — the maintainer live-SWG smoke —
was run this session and PASSED**, surfacing and fixing four form-internal bugs the automated gates never
exercised, plus a CI-gated handoff-policy test and a real-asset roundtrip dogfood.

## What Shipped (Tasks 1–2, pre-pause)

- **`ClientEffectSaveTargets`** — off-UI-thread loose-override save: takes the already-serialized CLEF
  bytes + provenance, composes `<root>/<loose-subdir>/<logical>` via the two-step fail-closed
  `LooseOverridePath.Resolve` (T-22-path), writes atomically with `Flush(true)`. Zero format logic.
- **`EffectsSubPanel`** — thin docked launcher (no-throw ctor + `BuildContentSafe`); `OpenFromTre` /
  `OpenLooseOverride` entries forward to the singleton host.
- **`FormClientEffectEditor`** — the roomy host (command list + typed field grid), no-throw ctor +
  guarded build, singleton hide-not-dispose, in-proc save parity with `apply-save-effect` (REVIEWS #9).
- **In-proc↔CLI save-parity test** (`6361c80`) + `Plugin.cs` registration (`4298e48`).

## Task 3 — Live smoke (PASSED) + fixes

The live smoke exercised the editor end-to-end against the local Core3/SWGEmu client and found four
form-internal regressions invisible to the codec/save-parity unit tests, each fixed and re-verified:

| Commit | Bug | Root cause / fix |
|--------|-----|------------------|
| `b7a6878` | **Blank editor window** | `BuildContent` threw `SplitterDistance must be between Panel1MinSize and Width - Panel2MinSize` → caught by the red failure surface (read as "blank"). A `Dock.Fill` SplitContainer ignores an explicit `Size` while unparented, so its width stayed ~150px and any SplitterDistance was out of range. Fix: defer SplitterDistance + min-sizes to `OnShown` (real client width) with a clamp. |
| `e494267` | **Open/Save-As dialogs wrong** | No `InitialDirectory` (inherited the client's `string/en`) and an `*.iff` filter though ClientEffect files are `.cef`. Fix: default both dialogs to `<root>/loose`, lead the filter with `*.cef`. |
| `233d93e` | **No "Open in Effects Editor" in the TRE Browser** | The hand-off entry was never wired into `FormTreBrowser`'s context menu, so `.cef` files offered nothing. Fix: add the menu item mirroring the Terrain hand-off (off-UI payload resolve → `EffectsSubPanel.OpenFromTre`). |
| `c68df5a` | **Move up/down deselected the row** | `RefreshAfterMutation` left the reorder path's selection cleared (the reselect-by-leaf branch was dead — leaf identity is lost across the reparse), so the moved row lost its highlight and the Move buttons grayed out. Fix: reselect by the new index (clamped), so the row stays highlighted and visibly jumps. |

Then the TRE→Effects gate was extracted into a framework-leg **`EffectHandoffPolicy`** (`47086d4` Utinni /
`22a19aa` UtinniPlugins) — `ShouldOfferEffectsEditor(.cef, !enumerateOnly)` + a defensive `FORM CLEF`
sniff — so the gate is unit-testable in the existing `UtinniCoreDotNet.Tests` UITests lane (mirrors
`ParticleHandoffPolicy`). `FormTreBrowser` now calls the policy instead of an inline extension check.

## Verification

- **Live smoke (Task 3) — PASSED**: editor opens with full layout; TRE Browser → `data_other_00.tre` →
  `clienteffect/*.cef` → "Open in Effects Editor" opens read-only; edit + add/remove/reorder/undo/redo;
  **save loose override → in-client reload round-tripped** (the maintainer confirmed the edited effect
  reloaded). Preview-in-client honest-candor messaging; raw/hex degrade on unknown-version files.
- **Process-isolated interactive harness — 26/26 paths green**: load (5-command fixture), reorder up/down
  with selection retained + Move buttons enabled + dirty set, boundary no-ops keep selection, add (+1),
  remove (−1), undo/redo restore/reapply, field-edit (grid populates + dirty), unknown-version raw degrade
  (no failure label, Save disabled). (Harness was a dev-time tool; promotion to CI is a follow-up — see
  Known Caveats.)
- **`EffectHandoffPolicyTests` — 18/18 passed** (`dotnet test --no-build -c Release --filter
  FullyQualifiedName~EffectHandoffPolicyTests`). Now in the CI UITests lane.
- **D-14 real-asset roundtrip — PASSED**: `roundtrip-effect` on the editor-saved real override
  `clienteffect/cbt_bolt_hit_asteroid_ion_cannon_acid.cef` → `bytesIdentical: true, commandCount: 3,
  rootType: CLEF, rawPreserved: false`. A real, editor-produced asset round-trips byte-exact.
- `Generated/UtinniCore.cs` restored after every build (CppSharp churn rule); not committed.

## Deviations from Plan

### [Live-smoke fixes] Four form-internal bugs not anticipated by the plan
- The plan's automated coverage (codec golden tests + in-proc↔CLI save parity) exercised the CLEF bytes
  and the save path, but nothing constructed or drove the WinForms host — so the blank-window,
  dialog-defaults, missing-TRE-menu, and reorder-reselection bugs all reached the live smoke. Each is
  fixed and verified (table above). The handoff gate is now CI-tested; the form-internal paths are
  harness-validated and tracked for CI promotion (deferred-items.md).

### [D-14 / REVIEWS #6] Real-asset roundtrip done against an editor-produced override (not a TRE extract)
- The plan envisioned extracting a per-lineage real CLEF via `utinni-cli` TRE verbs. There is no
  single-verb TRE-record extract in the current CLI (the effect verbs are file-path based), so the
  real-asset roundtrip was run against the `.cef` the **editor itself saved** during steps 4–5 — a
  stronger dogfood (it proves the editor's own output round-trips byte-exact). The optional
  `RoundtripEffect_RealAsset_WhenPresent` fixture test was NOT added (no clean extract path); the
  synthetic-fixture byte-exact coverage (43 codec tests + in-proc save parity) plus this real-asset CLI
  roundtrip stand in for it. See the PROD-W2-CFX-02 note below.

## Known Caveats / Stubs

- **PROD-W2-CFX-02 "both lineages" CI coverage**: validated byte-exact on SWGEmu-lineage `.cef`
  (unencrypted, reachable). High-era Restoration `.cef` remain enumerate-only (v6000 encryption), so the
  automated "both lineages" real-asset matrix stays caveated — unchanged from the plan's allowance.
- **Form-internal regression tests are not CI-gated** — the form lives in UtinniPlugins (no test project /
  no CI); the session harness validated 26/26 paths but needs a UtinniPlugins test lane to promote.
  Tracked in `deferred-items.md`.
- **Root `bin/Release/utinni-cli.exe` is stale (pre-22-02)** — surfaced during the D-14 dogfood; the
  current CLI is the project output. Tracked in `deferred-items.md` (verify the MCP shell path).
- **Preview in client** is honest-candor-only this build (no retrigger — D-07); the live-capable seam
  stays gated behind `IsRetriggerHookReachable() == false` for the future native hook (D-08).

## Self-Check: PASSED

- FOUND: `UtinniCoreDotNet/UI/EffectHandoffPolicy.cs`
- FOUND: `UtinniCoreDotNet.Tests/UITests/EffectHandoffPolicyTests.cs` (18 tests green)
- FOUND: `[UtinniPlugins] FormClientEffectEditor.cs` / `EffectsSubPanel.cs` / `ClientEffectSaveTargets.cs`
- FOUND: "Open in Effects Editor" wired in `FormTreBrowser.cs` (policy-gated)
- FOUND commits — Utinni: `b7a6878`, `e494267`, `233d93e`, `c68df5a`, `47086d4`; UtinniPlugins:
  `b30245d`, `4298e48`, `22a19aa`; (Task 2 Utinni half `6361c80`)
- D-14 real-asset roundtrip: `bytesIdentical: true`
- `Generated/UtinniCore.cs`: unmodified
