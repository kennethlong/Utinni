---
phase: 08-tjt-subpanel-iff-editor-read-write
plan: 06
subsystem: tjt-iff-editor-in-memory-live-patch
tags: [tjt, ui, winforms, iff, save-modes, live-patch, mapped-memory, con-n-04, cross-repo, smoke-automation-only]
requires:
  - "UtinniCoreDotNet/Formats/Iff/IffWriter + OpenSource.ClientMemory (08-01)"
  - "TheJawaToolboxDotNet/UI/Forms/FormIffEditor + IffEditController (08-04)"
  - "TheJawaToolboxDotNet/Saving/IffSaveTargets (08-05)"
  - "UtinniCore.Memory.memory.Copy (Generated/UtinniCore.cs ~702; native ?copy@memory@@YAXIII@Z) CON-N-04"
  - "UtinniCoreDotNet/Callbacks/GameCallbacks.AddMainLoopCall (game-thread marshaling)"
provides:
  - "UtinniCoreDotNet/Editing/LivePatchValidator — pure-function bounds gate (round-2 HIGH-B)"
  - "UtinniCoreDotNet.Tests/EditingTests/LivePatchValidatorTests — 5 [Fact]s closing CONTEXT D-05.3 'unit-tested for its bounds gate' claim"
  - "TheJawaToolboxDotNet/UI/Forms/FormSaveConfirmDialog — risk-proportional confirm modal (Color.Red emphasis + explicit-verb buttons; reusable by 08-07 repack)"
  - "TheJawaToolboxDotNet/Saving/LivePatchSaveTarget — game-thread CON-N-04 mapped-memory write; consumes LivePatchValidator (round-2 HIGH-B)"
  - "FormIffEditor Save▾ ▸ Patch live client — provenance-gated (`Source is OpenSource.ClientMemory`) + Game.IsRunning + FormSaveConfirmDialog gate; honest-disabled tooltip otherwise (round-2 MEDIUM 11)"
affects:
  - "UtinniCoreDotNet/UtinniCoreDotNet.csproj — added 1 <Compile Include> (Editing\\LivePatchValidator.cs — round-2 HIGH-A)"
  - "TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj — added 3 <Compile Include> entries (Saving\\LivePatchSaveTarget.cs + UI\\Forms\\FormSaveConfirmDialog.cs Form SubType + .Designer.cs DependentUpon — round-2 HIGH-A)"
  - "TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs — Patch live client menu wire (Save▾) + provenance gate + confirm + honest-disabled tooltip + Applied/Refused status copy"
tech-stack:
  added: []
  patterns:
    - "Framework-side pure-function bounds gate (round-2 HIGH-B): LivePatchValidator is BCL-only, no WinForms, no native interop, no GameCallbacks, no TJT reference — same disposition reasoning as LooseOverridePath / ReloadAssetClassifier / TreRecordIndexResolver (checker B-1). Unit-testable from the CI runner without an injected client."
    - "Deterministic refusal-check order (NoClient → ZeroTarget → SameLength): caller learns the most-blocking failure mode first; a no-client + zero-target + wrong-length input is unambiguously RefusedNoClient. Each branch is asserted by a dedicated [Fact]."
    - "Same-length-only V1 (08-REVIEWS HIGH-3): refuses BOTH growth (off-end write) AND shrink (stale-tail-bytes corruption — the reader would still read the tail as part of the document). Follow-up phase MAY relax via zero-fill tail; V1 ships the strict gate."
    - "Game-thread marshaling for the mapped-memory write (T-08-14 / CON-N-04): the entire pin → memory.Copy → unpin sequence runs inside a GameCallbacks.AddMainLoopCall lambda. The VirtualProtect save/restore is INSIDE the native memory::copy — never hand-rolled. GCHandle.Alloc(payload, Pinned) is freed in finally on the SAME thread that pinned it."
    - "Per-call modal lifecycle for FormSaveConfirmDialog (NOT singleton): instantiated via `using (var dlg = new FormSaveConfirmDialog(...))` per call site and disposed by the using block. Default WinForms dispose-on-close is CORRECT here. The 08-05 hide-not-dispose singleton-form pattern (FormIffEditor / FormTreBrowser) applies only to plugin-registered GetForms() instances; per-call modals do not have the second-open AV failure mode."
    - "Honest-disabled tooltip wording (round-2 MEDIUM 11): the Save▾ ▸ Patch live client item ships disabled in this phase with `Live patch requires opening from client memory — not wired in this phase.` — same wording at three sites (initial state, refresh, and the defensive RefusedNoClient status). PROD-W1-IFF Criterion 2 is acceptance-tested against modes 1/2/4 in Phase 8 with mode 3 documented as infra-ready."
key-files:
  created:
    - "UtinniCoreDotNet/Editing/LivePatchValidator.cs"
    - "UtinniCoreDotNet.Tests/EditingTests/LivePatchValidatorTests.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/LivePatchSaveTarget.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormSaveConfirmDialog.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormSaveConfirmDialog.Designer.cs"
  modified:
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj (1 new Compile Include — Editing\\LivePatchValidator.cs at line 214; round-2 HIGH-A)"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj (3 new Compile Include entries — Saving\\LivePatchSaveTarget.cs at line 150, UI\\Forms\\FormSaveConfirmDialog.cs at line 93 Form SubType + .Designer.cs at line 96 DependentUpon; round-2 HIGH-A)"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs (Patch live client Save▾ wiring: provenance + Game.IsRunning gate; confirm modal handoff; Applied / RefusedSameLength / RefusedZeroTarget / RefusedNoClient status copy; honest-disabled tooltip at three sites)"
decisions:
  - "FormSaveConfirmDialog is per-call modal (`using (var dlg = new ...)` → ShowDialog → using-disposes). Default WinForms dispose-on-close is CORRECT for per-call modals. The 08-05 hide-not-dispose singleton-form pattern (FormIffEditor / FormTreBrowser) applies ONLY to plugin-registered GetForms() instances that the MEF host keeps a single reference to — per-call modals do not have the second-open AV failure mode by construction. Future per-call modals (08-07 repack confirm, etc.) follow the same lifecycle."
  - "Task 5 (live-SWG smoke) — APPROVED BY MAINTAINER ON AUTOMATION ALONE (option 3): smoke=automation-only. The 5 LivePatchValidator [Fact]s (round-2 HIGH-B) carry the verification burden — the bounds gate is automation-gated regardless of Tier-4 coverage. Open Q4 (full functional smoke including SAME-LENGTH leaf edit + Patch live + stability observation + DIFFERENT-LENGTH refusal observation) is deferred to a later observation/doc pass (see deferred-items.md). PROD-W1-IFF Criterion 2 is acceptance-tested against modes 1/2/4 in Phase 8 with mode 3 documented as infra-ready, user-disabled."
  - "Round-2 HIGH-B (CONTEXT D-05.3 'unit-tested for its bounds gate' claim) is now factually accurate: the bounds gate is extracted into the framework-side pure function LivePatchValidator (BCL-only; no WinForms, no native interop, no GameCallbacks, no TJT reference) and covered by 5 [Fact]s (NoClient / ZeroTarget / Growth / Shrink / SameLengthHappyPath). LivePatchSaveTarget consumes LivePatchValidator.Validate before any AddMainLoopCall queue."
metrics:
  duration_minutes: 30
  completed_date: "2026-05-28"
---

# Phase 8 Plan 6: In-Memory Live Patch (D-05.3) — Bounds Gate + Save▾ Wiring Summary

One-liner: Ships D-05.3 as infra-ready, user-disabled — a framework-side pure-function bounds gate (LivePatchValidator + 5 [Fact]s closing round-2 HIGH-B / CONTEXT D-05.3 "unit-tested" claim), a game-thread CON-N-04 mapped-memory save target (LivePatchSaveTarget consuming LivePatchValidator), a risk-proportional confirm modal (FormSaveConfirmDialog with Color.Red emphasis + explicit-verb buttons), and FormIffEditor's Patch live client menu wire — provenance-gated on `Source is OpenSource.ClientMemory` AND Game.IsRunning, otherwise disabled with the honest "Live patch requires opening from client memory — not wired in this phase." tooltip; approved by maintainer on automation alone (option 3 — smoke=automation-only).

## What Shipped

**Framework (Utinni repo) — 1 new helper + 1 new test file + 1 csproj entry:**

- **`UtinniCoreDotNet/Editing/LivePatchValidator.cs`** — pure-function bounds gate. `public enum LivePatchValidation { Ok, RefusedNoClient, RefusedZeroTarget, RefusedSameLength }` and `public static LivePatchValidation Validate(IntPtr targetAddr, int originalMappedLength, int rewrittenLength, bool gameIsRunning)`. Deterministic refusal-check ordering: NoClient → ZeroTarget → SameLength. Same-length-only gate (08-REVIEWS HIGH-3) refuses BOTH growth AND shrink. Framework-side placement per checker B-1 (same disposition reasoning as 08-04 IffEditController / 08-05 LooseOverridePath / 08-05 ReloadAssetClassifier / 08-05 TreRecordIndexResolver): BCL-only, no I/O, no native interop, no WinForms, no GameCallbacks reference, no TJT reference. Unit-testable from the CI runner without an injected client.

- **`UtinniCoreDotNet.Tests/EditingTests/LivePatchValidatorTests.cs`** — 5 `[Fact]`s closing the round-2 HIGH-B / CONTEXT D-05.3 "unit-tested for its bounds gate" claim:
  - `Validate_NoClient_RefusedNoClient` — gameIsRunning=false → RefusedNoClient (even with valid target + length).
  - `Validate_ZeroTarget_RefusedZeroTarget` — gameIsRunning=true AND targetAddr=IntPtr.Zero → RefusedZeroTarget.
  - `Validate_GrowthLength_RefusedSameLength` — rewrittenLength > originalMappedLength → RefusedSameLength (off-end write closure).
  - `Validate_ShrinkLength_RefusedSameLength` — rewrittenLength < originalMappedLength → RefusedSameLength (stale-tail-bytes corruption closure).
  - `Validate_SameLengthHappyPath_Ok` — all preconditions met → Ok.
  Auto-globbed by the SDK-style test csproj's default `**/*.cs` glob; the file lives under `EditingTests/` which is NOT in the `Fixtures\**` exclusion. NO test-csproj edit needed.

- **`UtinniCoreDotNet/UtinniCoreDotNet.csproj`** — added ONE new `<Compile Include="Editing\LivePatchValidator.cs" />` entry at line 214 (round-2 HIGH-A). Old-style explicit-compile project requires explicit listings; the test build cannot discover the production type otherwise.

**Plugin (UtinniPlugins repo) — 3 new files + 1 modified + 3 csproj entries:**

- **`Saving/LivePatchSaveTarget.cs`** — in-memory live-patch save target. `public static LivePatchResult Apply(OpenSource.ClientMemory target, byte[] rewritten)` with `public enum LivePatchResult { Applied, RefusedNoClient, RefusedZeroTarget, RefusedSameLength }` (mirrors `LivePatchValidation` 1:1 + the success state).
  - Round-2 HIGH-B fold: bounds gate EXTRACTED to `UtinniCoreDotNet.Editing.LivePatchValidator.Validate(target.TargetAddr, target.OriginalMappedLength, rewritten?.Length ?? 0, Game.IsRunning)`. Apply only proceeds to `GameCallbacks.AddMainLoopCall` on `LivePatchValidation.Ok`; the refusal branches map 1:1 to `LivePatchResult.RefusedNoClient` / `RefusedZeroTarget` / `RefusedSameLength`.
  - Null-safety: `rewritten?.Length ?? 0` fed to the validator. A null `rewritten` is treated as length-0 and refuses as `RefusedSameLength` for any non-zero `OriginalMappedLength`. The pure validator never dereferences the array; the dereference happens only AFTER the gate clears.
  - CON-N-04 (T-08-14): the actual write is the native `memory::copy` binding (`UtinniCore.Memory.memory.Copy`) which contains its OWN VirtualProtect save/Copy/restore — no hand-rolled write that skips it. The `Apply` lambda is queued via `GameCallbacks.AddMainLoopCall` so the write happens on the SWG game thread — NEVER the UI thread. `GCHandle.Alloc(payload, Pinned)` is acquired AND freed inside the lambda (same thread for pin + write + free); released in `finally`.
  - Volatile by design (T-08-16): the patch is lost on reload / scene change — the intended D-05.3 behavior. The candid UI copy surfaces this in the confirm dialog.

- **`UI/Forms/FormSaveConfirmDialog.cs` + `.Designer.cs`** — risk-proportional confirm modal. Parameterized by heading, body, and the two explicit-verb button captions (plus an optional checkbox reserved for 08-07's repack "Back up the source first" use case). Body emphasis rendered in `Color.Red` per UI-SPEC §Destructive; backgrounds via `Colors.*()` accessors. Per-call modal lifecycle — instantiated via `using (var dlg = new ...)` and disposed by the using block; default WinForms dispose-on-close is CORRECT for per-call modals (NOT the 08-05 singleton-form hide-not-dispose pattern, which applies only to plugin-registered GetForms() instances). Live-patch caller copy: heading `Patch the live client in memory?`; body `This writes your edits straight into the running client. The change is temporary (lost on reload) and can destabilize the session. Continue?`; buttons `Patch live` / `Cancel`. Reusable for 08-07's repack confirm.

- **`UI/Forms/FormIffEditor.cs`** (modified) — Save▾ ▸ `Patch live client (in memory)` wiring:
  - **Provenance gate (08-REVIEWS HIGH-2):** the menu item is ENABLED ONLY when `this.Source is OpenSource.ClientMemory cm` AND `Game.IsRunning`. Otherwise DISABLED.
  - **Honest-disabled tooltip (round-2 MEDIUM 11):** when disabled, the tooltip reads `Live patch requires opening from client memory — not wired in this phase.` — appearing at THREE sites (initial state at line 860, refresh at line 939, defensive RefusedNoClient status at line 1039). Matches the `must_haves` entry for D-05.3 reduced-mode completion semantics.
  - **Confirm dialog on click:** when enabled, shows `FormSaveConfirmDialog` with the live-patch heading/body/buttons. On `Patch live`, serializes via `IffWriter.Write(mutableDoc)` then calls `LivePatchSaveTarget.Apply(cm, rewritten)`.
  - **Result→status copy mapping:**
    - `Applied` → `Saving (live patch)… Applied.` (volatile — does NOT clear the dirty marker because a live patch is not a file save)
    - `RefusedSameLength` → `Live patch requires the rewritten IFF to be the same length as the original. Save to file/repack instead.` (UI-SPEC candid copy)
    - `RefusedZeroTarget` → `Live patch target address is invalid. Save to file/repack instead.`
    - `RefusedNoClient` → `No live client.` (defensive — button should not have been clickable)
  - **No-side-effect on file save modes:** 08-05's loose-override / Save / Save-As / Reload wiring is untouched.

- **`TheJawaToolboxDotNet.csproj`** — added THREE new `<Compile Include>` entries (round-2 HIGH-A): `Saving\LivePatchSaveTarget.cs` at line 150 (no SubType — plain class); `UI\Forms\FormSaveConfirmDialog.cs` at line 93 with `<SubType>Form</SubType>`; `UI\Forms\FormSaveConfirmDialog.Designer.cs` at line 96 with `<DependentUpon>FormSaveConfirmDialog.cs</DependentUpon>`. No `<EmbeddedResource>` — same as FormTreBrowser / FormFourCcDialog to avoid MSB3823.

## Task 5 Outcome (Live-SWG Smoke)

**Approved by maintainer on automation alone (option 3); smoke=automation-only.**

The 5 LivePatchValidator `[Fact]`s (round-2 HIGH-B) carry the verification burden — the bounds gate is automation-gated regardless of Tier-4 coverage. PROD-W1-IFF Criterion 2 is acceptance-tested against modes 1/2/4 in Phase 8 with mode 3 documented as infra-ready, user-disabled per round-2 MEDIUM 11.

**Live-SWG functional smoke (full Open Q4) is deferred** to a later observation/doc pass:
- (a) Open an IFF that resolves to a known client-memory region (requires a maintainer-only debug construction of `OpenSource.ClientMemory` — no current Phase-8 open path constructs one);
- (b) Edit a SAME-LENGTH leaf payload and exercise `Save ▾ ▸ Patch live client (in memory)`;
- (c) Observe live-patch stability (no crash / no AV);
- (d) Observe DIFFERENT-LENGTH refusal copy (`Live patch requires the rewritten IFF to be the same length as the original. Save to file/repack instead.`);
- (e) Observe volatility (patch is lost on reload / scene change).

The disabled-state behavior (the only path reachable today without a debug-only `ClientMemory` construction) is automation-gated by the honest-disabled tooltip wording grep (round-2 MEDIUM 11 — 3 hits at lines 860 / 939 / 1039) and by source-inspection of the `RefreshSaveMenuEnabledState` provenance gate.

See `deferred-items.md` for the Open Q4 entry.

## Verification Gate Matrix

| Gate | Status | Detail |
|------|--------|--------|
| LivePatchValidator [Fact]s (round-2 HIGH-B / CONTEXT D-05.3 "unit-tested" claim) | PASS | 5/5 passing — Validate_NoClient_RefusedNoClient, Validate_ZeroTarget_RefusedZeroTarget, Validate_GrowthLength_RefusedSameLength, Validate_ShrinkLength_RefusedSameLength, Validate_SameLengthHappyPath_Ok |
| Full test suite | PASS | 148 / 148 passing (143 prior + 5 new LivePatchValidator) |
| VS2026 MSBuild Debug\|x86 (Utinni: UtinniCoreDotNet + UtinniCoreDotNet.Tests) | PASS | clean |
| VS2026 MSBuild Release\|x86 (Utinni: UtinniCoreDotNet + UtinniCoreDotNet.Tests) | PASS | clean |
| VS2026 MSBuild Debug\|x86 (UtinniPlugins: TheJawaToolboxDotNet) | PASS | clean |
| VS2026 MSBuild Release\|x86 (UtinniPlugins: TheJawaToolboxDotNet) | PASS | clean |
| Round-2 HIGH-A csproj grep gates (4 new entries) | PASS | `<Compile Include="Editing\LivePatchValidator.cs" />` in UtinniCoreDotNet.csproj (1 hit); `<Compile Include="Saving\LivePatchSaveTarget.cs" />` in TheJawaToolboxDotNet.csproj (1 hit); `<Compile Include="UI\Forms\FormSaveConfirmDialog.cs"><SubType>Form</SubType>` (1 hit); `<Compile Include="UI\Forms\FormSaveConfirmDialog.Designer.cs"><DependentUpon>FormSaveConfirmDialog.cs</DependentUpon>` (1 hit) |
| Round-2 HIGH-B framework-side purity (LivePatchValidator references neither System.Windows.Forms nor UtinniCore.Memory nor GameCallbacks nor any TJT type) | PASS | grep returns 0 for all four token classes in `UtinniCoreDotNet/Editing/LivePatchValidator.cs` |
| Round-2 MEDIUM 11 honest-disabled tooltip wording (`Live patch requires opening from client memory — not wired in this phase.`) | PASS | 3 hits in FormIffEditor.cs (initial state line 860, refresh line 939, defensive RefusedNoClient status line 1039) |
| LivePatchSaveTarget consumes LivePatchValidator.Validate before AddMainLoopCall (round-2 HIGH-B grep) | PASS | both `LivePatchValidator.Validate` and `AddMainLoopCall` referenced in `Saving/LivePatchSaveTarget.cs` |
| Game-thread CON-N-04 (T-08-14): Memory.memory.Copy inside AddMainLoopCall lambda | PASS by source inspection — the entire pin → memory.Copy → unpin runs inside the lambda; GCHandle.Alloc(payload, Pinned) freed in finally on the same thread |
| Same-length-only V1 (T-08-15 / 08-REVIEWS HIGH-3) | PASS by [Fact] coverage — Growth + Shrink both refuse as RefusedSameLength; happy path requires equality |
| Provenance gate (T-08-16b / 08-REVIEWS HIGH-2) | PASS by source inspection — `Source is OpenSource.ClientMemory cm` AND `Game.IsRunning` enables the menu item; otherwise DISABLED with the honest tooltip |

## Singleton-Form Pattern Note (Contrast with 08-05)

**FormSaveConfirmDialog is per-call modal — default WinForms dispose-on-close is CORRECT here.**

The 08-05 hide-not-dispose intercept (FormIffEditor + FormTreBrowser, commits `b899504` + `ce2a0a4` in UtinniPlugins) applies ONLY to plugin-registered `GetForms()` instances that the MEF host keeps a SINGLE reference to. The failure mode is: Plugin.cs registers ONE form instance at load → user closes the form → default WinForms disposes it → next `.Show()` (from window menu OR cross-form hand-off) throws `ObjectDisposedException` at `Form.CreateHandle`. The intercept on `CloseReason.UserClosing` cancels close + `Hide()` instead of disposing.

FormSaveConfirmDialog has NEITHER of these properties:
- It is NOT registered via `GetForms()` in Plugin.cs — it has no MEF presence.
- It is constructed PER CALL via `using (var dlg = new FormSaveConfirmDialog(heading, body, primaryVerb, secondaryVerb)) { … }` and the using block disposes it after `ShowDialog` returns.

Each invocation gets a fresh instance, so the second-open `ObjectDisposedException` failure mode is structurally absent. Default WinForms dispose-on-close is the correct lifecycle. Phases 9-11 follow the same distinction: per-call modals dispose normally; singleton MEF-registered editor forms apply the hide-not-dispose intercept.

## Deferred (Open Q4 — appended to deferred-items.md)

**Open Q4 — full functional D-05.3 live-patch smoke** (DEFERRED to later observation/doc pass):

The Task 5 maintainer choice was option 3 ("Skip smoke and approve on automation alone"). The 5 LivePatchValidator [Fact]s carry the verification burden for the bounds gate. The remaining Tier-4 residuals — (a) maintainer-only debug construction of `OpenSource.ClientMemory` to enable the menu, (b) same-length leaf edit + Patch live + stability observation, (c) different-length patch attempt + refusal-copy observation, (d) reload/scene-change volatility observation — are deferred to a later observation/doc pass against a live client. Not blocking Phase 8 acceptance: D-05.3 is recorded in the plan's `must_haves` as **infra-ready, user-disabled** (round-2 MEDIUM 11) and PROD-W1-IFF Criterion 2 is acceptance-tested against modes 1/2/4 with mode 3 documented at the infra-ready level.

## Deviations from Plan

None — plan executed exactly as written. The 4 auto tasks (FormSaveConfirmDialog modal + LivePatchValidator RED/GREEN + LivePatchSaveTarget + FormIffEditor wiring) shipped per the plan's `<action>` blocks. Task 5 (live-SWG smoke) was approved on automation alone via maintainer option 3 — within the plan's `<verify>` envelope (which explicitly allowed for the reduced-functional path, here further reduced to automation-only by maintainer choice).

## Threat Surface Verification

All threat-model dispositions from the plan's `<threat_model>` are met:

| Threat ID | Disposition | Status |
|-----------|-------------|--------|
| T-08-14 | Tampering / DoS — mapped-memory write off the game thread or bypassing memory::copy | Met — LivePatchSaveTarget queues the entire pin → Memory.memory.Copy → unpin sequence inside `GameCallbacks.AddMainLoopCall`; VirtualProtect save/restore is inside the native `memory::copy`; NEVER the UI thread |
| T-08-15 | DoS — wrong-length write (grow or shrink) into client memory → AV/corruption/stale-tail | Met — same-length-only V1 gate enforced by LivePatchValidator (pure-function, framework-side, unit-tested with 5 [Fact]s — round-2 HIGH-B); refuses both growth AND shrink; also refuses IntPtr.Zero target |
| T-08-16 | Tampering — user unaware the patch is volatile / destabilizing | Met — FormSaveConfirmDialog modal with Color.Red emphasis + explicit `Patch live` verb (NOT bare OK); body states "The change is temporary (lost on reload) and can destabilize the session." |
| T-08-16b | Tampering — menu enabled on wrong provenance | Met — provenance gate: enabled only when `Source is OpenSource.ClientMemory` AND `Game.IsRunning`; otherwise disabled with honest tooltip (round-2 MEDIUM 11) |
| T-08-16c | Tampering — bounds gate inlined in LivePatchSaveTarget cannot be unit-tested without an injected client → regressions slip past CI | Met — bounds gate EXTRACTED to LivePatchValidator (pure function, framework-side, no native interop); unit-tested with 5 [Fact]s in CI without a live client (round-2 HIGH-B) |
| T-08-16d | Tampering — new .cs files silently fail to compile because OLD-STYLE csproj files do not glob them | Met — explicit `<Compile Include>` entries: LivePatchValidator.cs in UtinniCoreDotNet.csproj; LivePatchSaveTarget.cs + FormSaveConfirmDialog.cs/Designer.cs in TheJawaToolboxDotNet.csproj; grep-gated (round-2 HIGH-A) |
| T-08-SC | Supply chain — package installs | N/A — no external packages added this plan |

## Cross-AI Review Concerns Addressed

| Review ID | Severity | Disposition |
|-----------|----------|-------------|
| Round-2 HIGH-A (csproj coverage) | HIGH | RESOLVED — 1 entry in UtinniCoreDotNet.csproj + 3 entries in TheJawaToolboxDotNet.csproj; all grep-gated; both Debug\|x86 and Release\|x86 build clean across both repos |
| Round-2 HIGH-B (CONTEXT D-05.3 "unit-tested for its bounds gate" claim) | HIGH | RESOLVED — bounds gate extracted to framework-side `LivePatchValidator.Validate` pure function (BCL-only; no WinForms / UtinniCore.Memory / GameCallbacks / TJT references — grep proves zero hits); 5 [Fact]s cover NoClient / ZeroTarget / Growth / Shrink / SameLengthHappyPath; LivePatchSaveTarget consumes the validator BEFORE AddMainLoopCall |
| Round-2 MEDIUM 11 (D-05.3 reduced-mode completion semantics) | MEDIUM | RESOLVED — `must_haves` records D-05.3 as **infra-ready, user-disabled**; menu ships disabled with honest tooltip `Live patch requires opening from client memory — not wired in this phase.` (3 hits in FormIffEditor.cs); PROD-W1-IFF Criterion 2 acceptance-tested against modes 1/2/4 with mode 3 at infra-ready |
| 08-REVIEWS HIGH-2 (Source-property provenance gate) | HIGH | RESOLVED — menu item enabled ONLY when `Source is OpenSource.ClientMemory cm` AND `Game.IsRunning`; disabled otherwise. Provenance gate is the only path that lets the menu enable |
| 08-REVIEWS HIGH-3 (same-length-only V1) | HIGH | RESOLVED — LivePatchValidator refuses both growth (off-end write) AND shrink (stale-tail-bytes corruption); LivePatchResult.RefusedSameLength returned for both; candid copy `Live patch requires the rewritten IFF to be the same length as the original. Save to file/repack instead.` surfaces in the status line |
| Checker B-1 (no linked-source path into UtinniPlugins) | CHECKER | RESOLVED — LivePatchValidator ships framework-side; consumed via the existing ProjectReference from UtinniCoreDotNet.Tests.csproj. `grep -c 'UtinniPlugins' UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` returns 0 (already established by 08-04 / 08-05; preserved here) |

## Build Verification

**VS2026 MSBuild — `D:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe`:**

- `UtinniCoreDotNet` Debug\|x86 → `bin/Debug/UtinniCoreDotNet.dll` — clean (pre-existing CS0108 warnings only)
- `UtinniCoreDotNet` Release\|x86 → `bin/Release/UtinniCoreDotNet.dll` — clean (same pre-existing warnings)
- `UtinniCoreDotNet.Tests` Debug\|x86 → `bin/Debug/net472/UtinniCoreDotNet.Tests.dll` — clean (pre-existing xUnit2013/xUnit2020 analyzer warnings only)
- `TheJawaToolboxDotNet` Debug\|x86 → `bin/Debug/Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll` — clean (zero errors, zero warnings)
- `TheJawaToolboxDotNet` Release\|x86 → `bin/Release/Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll` — clean (zero errors, zero warnings)

**xUnit test pass — `dotnet test UtinniCoreDotNet.Tests --no-build -c Debug`:**

- Editing + Saving + IFF subsuite: **148 / 148 passing** — 143 prior (103 IFF from 08-01/08-04 + 14 LooseOverridePath + 22 ClientReloadDispatcher + 4 TreHandoffFallback from 08-05) + 5 new LivePatchValidator from 08-06.

## Acceptance Gate Verification (literal greps from the plan)

**Task 1 (FormSaveConfirmDialog + csproj):**
- `grep -c "class FormSaveConfirmDialog" ../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormSaveConfirmDialog.cs` → **≥ 1** PASS
- `grep -c '<Compile Include="UI\\Forms\\FormSaveConfirmDialog.cs"' ../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj` → **1** PASS
- `<SubType>Form</SubType>` on .cs entry — PASS by source inspection
- `<DependentUpon>FormSaveConfirmDialog.cs</DependentUpon>` on .Designer.cs entry — PASS by source inspection
- Body emphasis uses Color.Red; buttons are explicit verbs — PASS by source inspection
- VS2026 MSBuild of TheJawaToolboxDotNet compiles clean — PASS

**Task 2 (LivePatchValidator + tests + csproj):**
- `dotnet test UtinniCoreDotNet.Tests --no-build --filter "FullyQualifiedName~LivePatchValidator"` exits 0 with 5 tests passing — PASS (5/5)
- `grep -c '<Compile Include="Editing\\LivePatchValidator.cs"' UtinniCoreDotNet/UtinniCoreDotNet.csproj` → **1** PASS (line 214)
- Framework-side purity: `grep -c 'System\.Windows\.Forms\|UtinniCore\.Memory\|GameCallbacks\|TheJawaToolbox' UtinniCoreDotNet/Editing/LivePatchValidator.cs` → **0** PASS (all four token classes zero hits)
- 5 [Fact]s with exact names from `<action>` Part C — PASS by source inspection

**Task 3 (LivePatchSaveTarget + csproj):**
- `grep -c "AddMainLoopCall\|LivePatchValidator\.Validate" ../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/LivePatchSaveTarget.cs` → **≥ 2** PASS
- `grep -c '<Compile Include="Saving\\LivePatchSaveTarget.cs"' ../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj` → **1** PASS (line 150)
- Memory.memory.Copy inside AddMainLoopCall lambda — PASS by source inspection
- Same-length-only refusal via validator — PASS by [Fact] coverage
- Pinned source GCHandle freed in finally — PASS by source inspection

**Task 4 (FormIffEditor Save▾ wiring):**
- `grep -c "LivePatchSaveTarget\|OpenSource\.ClientMemory" ../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` → **≥ 2** PASS
- Provenance gate on `Source is OpenSource.ClientMemory cm` AND `Game.IsRunning` — PASS by source inspection
- Honest-disabled tooltip `Live patch requires opening from client memory — not wired in this phase.` — PASS (3 hits at lines 860 / 939 / 1039)
- Confirm modal shown on click + Patch live verb only — PASS by source inspection
- Applied / RefusedSameLength / RefusedZeroTarget / RefusedNoClient status mapping — PASS by source inspection

**Task 5 (live-SWG smoke):**
- Maintainer approved on automation alone (option 3); smoke=automation-only — PASS
- 5 LivePatchValidator [Fact]s green in CI (round-2 HIGH-B) — PASS (5/5)
- Open Q4 (full functional D-05.3 smoke) deferred to later observation/doc pass — see `deferred-items.md`

## Output Confirmation

(a) The 5 LivePatchValidator xUnit [Fact]s close round-2 HIGH-B and make CONTEXT D-05.3's "implementation-complete and unit-tested for its bounds gate" claim factually accurate.

(b) The 4 new csproj entries (1 in UtinniCoreDotNet.csproj — `Editing\LivePatchValidator.cs`; 3 in TheJawaToolboxDotNet.csproj — `Saving\LivePatchSaveTarget.cs` + `UI\Forms\FormSaveConfirmDialog.cs` Form SubType + `.Designer.cs` DependentUpon) close round-2 HIGH-A for this plan.

## Commits

**Utinni repo (3 commits):**
- `1904eae` — `test(08-06): add failing tests for LivePatchValidator bounds gate` (RED — 5 [Fact]s)
- `8e329e5` — `feat(08-06): LivePatchValidator framework-side pure-function bounds gate` (GREEN — round-2 HIGH-A + HIGH-B)
- `0e403e4` — `docs(08-06): STATE — auto tasks 1-4 complete; Task 5 smoke checkpoint awaiting`
- (this SUMMARY commit + STATE/ROADMAP updates)

**UtinniPlugins repo (3 commits):**
- `947aeff` — `feat(08-06): FormSaveConfirmDialog — risk-proportional confirm modal` (+ 2 csproj entries)
- `463c1d3` — `feat(08-06): LivePatchSaveTarget — game-thread CON-N-04 mapped-memory write` (+ 1 csproj entry)
- `9686596` — `feat(08-06): wire Save▾ ▸ Patch live client — provenance gate + confirm + honest disabled tooltip`

## Working-Tree State

- `UtinniCoreDotNet/Generated/UtinniCore.cs` shows as `M` in the Utinni working tree (same pre-existing CppSharp-regen drift carried since 08-01 onward; NOT staged by any 08-06 commit). The binding-line citation referenced by the plan (`Memory.memory.Copy` at ~line 702) resolves correctly against the committed file.
- `.planning/ui-reviews/` is an untracked working-tree dir present at session start; unrelated to 08-06.
- `D:\Code\UtinniPlugins\The Jawa Toolbox\TheJawaToolboxDotNet\UI\Forms\FormIffEditor.cs` may show maintainer-intentional changes in the UtinniPlugins working tree post-session — explicitly NOT staged or committed as part of 08-06 closeout (per the closeout objective's working-tree note).

## Self-Check: PASSED

**Files verified present:**

- `D:/Code/Utinni/UtinniCoreDotNet/Editing/LivePatchValidator.cs` — FOUND
- `D:/Code/Utinni/UtinniCoreDotNet.Tests/EditingTests/LivePatchValidatorTests.cs` — FOUND
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/LivePatchSaveTarget.cs` — FOUND
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormSaveConfirmDialog.cs` — FOUND
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormSaveConfirmDialog.Designer.cs` — FOUND
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` — FOUND (modified: Save▾ ▸ Patch live client wiring)

**Csproj entries verified present (grep):**

- `UtinniCoreDotNet/UtinniCoreDotNet.csproj` line 214: `<Compile Include="Editing\LivePatchValidator.cs" />` — FOUND
- `TheJawaToolboxDotNet.csproj` line 150: `<Compile Include="Saving\LivePatchSaveTarget.cs" />` — FOUND
- `TheJawaToolboxDotNet.csproj` line 93: `<Compile Include="UI\Forms\FormSaveConfirmDialog.cs"><SubType>Form</SubType>` — FOUND
- `TheJawaToolboxDotNet.csproj` line 96: `<Compile Include="UI\Forms\FormSaveConfirmDialog.Designer.cs"><DependentUpon>FormSaveConfirmDialog.cs</DependentUpon>` — FOUND

**Commits verified present (`git log --oneline` substring match):**

Utinni repo:
- `1904eae` — `test(08-06): add failing tests for LivePatchValidator ...` — FOUND
- `8e329e5` — `feat(08-06): LivePatchValidator framework-side ...` — FOUND
- `0e403e4` — `docs(08-06): STATE — auto tasks ...` — FOUND

UtinniPlugins repo:
- `947aeff` — `feat(08-06): FormSaveConfirmDialog ...` — FOUND
- `463c1d3` — `feat(08-06): LivePatchSaveTarget ...` — FOUND
- `9686596` — `feat(08-06): wire Save▾ ▸ Patch live client ...` — FOUND

**Test counts verified by execution:** 148/148 passing (143 prior + 5 new LivePatchValidator). Bounds-gate filter `dotnet test --filter "FullyQualifiedName~LivePatchValidator"` exits 0 with 5 tests passing.

**Round-2 HIGH-B framework-side purity verified:** `grep -c 'System\.Windows\.Forms\|UtinniCore\.Memory\|GameCallbacks\|TheJawaToolbox' UtinniCoreDotNet/Editing/LivePatchValidator.cs` returns 0 (all four token classes).

**Round-2 MEDIUM 11 honest-disabled tooltip wording verified:** 3 hits of `Live patch requires opening from client memory — not wired in this phase.` in `FormIffEditor.cs` (lines 860, 939, 1039).
