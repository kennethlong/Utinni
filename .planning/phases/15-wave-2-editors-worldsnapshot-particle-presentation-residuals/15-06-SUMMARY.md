---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 06
subsystem: particle-editor (TJT WinForms) + framework hand-off gate
tags: [PROD-W2-PRT, particle, prt, peft, editor, winforms, hex-fallback, ai-read-assist, live-preview, D-05, D-07, D-08, D-09, D-11, cross-repo]
requires:
  - phase: 15-02
    provides: ".prt / FORM PEFT typed codec (ParticleEffectDocument.FromBytes + MutableParticleEffect.EditLeafPayload/Serialize + ParticleEmitterDescription typed view + SourceIff byte-source)"
  - phase: 15-04
    provides: "decode-iff PEFT auto-dispatch (the D-08 read path the Explain effect button reuses)"
  - phase: 15-03
    provides: "ParticlePreview seam (isRetriggerAvailable() == false this phase → editor degrades to tier-(b) reload candor)"
  - phase: 11
    provides: "FormObjectTemplateEditor shell + OtHandoffPolicy + ParticleSaveTargets analog (ObjectTemplateSaveTargets) + IffChunkTree + ThemedDataGridView + SingletonFormClosePolicy + FormSaveConfirmDialog + FormParamHexEditor"
provides:
  - "ParticleHandoffPolicy (framework, UtinniCoreDotNet/UI) — ShouldOfferParticleEditor (.prt gate) + IsParticlePayload (FORM PEFT sniff, never throws)"
  - "FormParticleEditor (TJT) — the new Wave-2 .prt editor: emitter tree + typed param grid + D-05 hex fallback + AI read-assist + state-encoded D-09 preview + DEC-A3 boundary"
  - "ParticleSaveTargets (TJT) — thin shim forwarding effect.SourceIff to Phase-8 IffSaveTargets/TreRepackSaveTarget"
  - "ParticleReadAssist (TJT) — read-only decode-iff dispatch for the Explain effect button (D-07/D-08), zero in-process format logic"
  - "TRE Browser 'Open in Particle Editor' hand-off, gated on ParticleHandoffPolicy"
affects: [15-08]
tech-stack:
  added: []
  patterns: [clone-wave1-editor-shell, singleton-hide-not-dispose, cf-09-fill-first-add-order, degrade-dont-abort-hex-fallback-visible, read-assist-reuses-cli-read-path, state-encoded-preview-honest-candor, mef-registration-try-catch, framework-handoff-policy-gate]
key-files:
  created:
    - "D:/Code/Utinni/UtinniCoreDotNet/UI/ParticleHandoffPolicy.cs"
    - "D:/Code/Utinni/UtinniCoreDotNet.Tests/UITests/ParticleHandoffPolicyTests.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormParticleEditor.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormParticleEditor.Designer.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/ParticleReadAssist.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/ParticleSaveTargets.cs"
  modified:
    - "D:/Code/Utinni/UtinniCoreDotNet/UtinniCoreDotNet.csproj"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs"
decisions:
  - "ParticleHandoffPolicy.IsParticlePayload uses a cheap byte-level FORM PEFT header sniff (bytes[0..4]=='FORM', bytes[8..12]=='PEFT'), mirroring DatatableHandoffPolicy.IsDtiiPayload — NOT a full codec parse. The editor's open path runs the full codec (which degrades on unknown versions); the sniff only gates the context-menu click and must never throw."
  - "The param grid is read-only as a surface this phase: typed scalar inline editing is NOT wired (the 15-02 codec exposes EditLeafPayload + the ParticleEmitterDescription read view, but no per-field-name typed edit seam). The editable surface is raw-leaf hex editing via double-click → FormParamHexEditor → EditLeafPayload (byte-safe, editor-local undoable). The typed waveform/colorramp fields render as read-only control-point summaries (UI-SPEC § value-cell states, Assumption 4 — no graphical curve editor this phase). This keeps every edit byte-exact and honest while showing the full typed surface + the D-05 hex fallback."
  - "Editor-local undo/redo is a lightweight leaf-payload Before/After stack (CON-M-05: independent of the scene UndoRedoManager), not a ParticleEditController (none exists; the codec has no command layer). Each entry restores one captured leaf — undoable, redoable, dirty-tracked."
  - "Preview in client (D-09): IsRetriggerHookReachable() is a single seam returning false this phase (15-03 honest finding — the native ParticlePreview.isRetriggerAvailable() returns false). The button is state-encoded-disabled and the reload badge degrades to the LOCKED tier-(b) copy. 15-08 flips this seam to read the real native predicate once a live session confirms the hook — no button-logic change needed."
  - "Explain effect shells utinni-cli decode-iff (the SAME verb summarize_particle dispatches, D-08) via ParticleReadAssist with a 15s timeout backstop (SOE-tools-hang gotcha) — read-assist ONLY (D-07): no codec call in the AI handler, no write path, no prompt-to-mutate. If utinni-cli.exe is absent it degrades to an honest error string rather than re-deriving a decode in-process (D-06)."
metrics:
  duration: ~70 min
  completed: 2026-06-07
---

# Phase 15 Plan 06: FormParticleEditor (.prt / FORM PEFT editor shell) Summary

Built the flashy AI-assist showcase editor — a new `FormParticleEditor : UtinniForm, IEditorForm` over a `.prt` (`FORM PEFT`) asset (PROD-W2-PRT, UI half) — composing the 15-02 codec, the 15-04 read path, and the 15-03 preview seam into a single Wave-1-shaped editor cloned from the Phase-11 Object Template editor shell: emitter tree (left, `IffChunkTree`) + typed param grid (right, `ThemedDataGridView`) with the **honest greyed-out Consolas hex fallback for unknowns (the visible surface of D-05)**, the inherited Phase-8 `Save ▾` + provenance gating, editor-local leaf-edit undo/redo (CON-M-05), an in-app `Explain effect` AI read-assist button reusing the **SAME `.prt` CLI read path** (read-assist only, D-07/D-08), and a state-encoded `Preview in client` hot-retrigger button (D-09) with **honest live-capable-vs-degraded reload candor**. The DEC-A3 preview-vs-author boundary (D-11) is surfaced verbatim as a dimmed footer.

## What shipped

**Task 1 — `ParticleHandoffPolicy` framework gate (commit `26af8e6`, Utinni repo, TDD):**
- New `UtinniCoreDotNet/UI/ParticleHandoffPolicy.cs` mirroring `OtHandoffPolicy`/`DatatableHandoffPolicy`: `ShouldOfferParticleEditor(logicalPath, enumerateOnly)` extension gate (`.prt` only, enumerate-only/null safe) + `IsParticlePayload(byte[])` cheap `FORM PEFT` byte sniff that NEVER throws (try/catch returns false on malformed/truncated/garbage).
- `ParticleHandoffPolicyTests.cs` (`--filter ParticleHandoff`) — **18 facts** covering the extension gate (8 Theory rows), the content sniff (3 facts: PEFT true / non-PEFT false / non-FORM false), and the never-throws behavior (4 short/null Theory rows + 1 garbage/truncated fact). Far exceeds the planned ≥3.
- Added the explicit `<Compile Include="UI\ParticleHandoffPolicy.cs" />` to the old-style `UtinniCoreDotNet.csproj`.

**Task 2 — `FormParticleEditor` shell + emitter tree + param grid + hex fallback + AI assist + preview + registration (commit `589f206`, UtinniPlugins repo):**
- `FormParticleEditor.cs` (1112 lines) + `.Designer.cs` — a direct clone of the `FormObjectTemplateEditor` shell: resizable `UtinniForm`, custom titlebar (`DrawName = true`, TJT icon), title `Particle Editor` with leading `● ` dirty marker, default 1100×760 / min 880×560, persisted `[ParticleEditor] width/height/splitterDistance`, singleton hide-not-dispose via `SingletonFormClosePolicy` (D-03, from commit 1).
- **Layout (CF-09 Fill-first add order):** toolbar (Top, 28px: `Open…` · `Save ▾` · sep · `Undo` · `Redo` · sep · `Explain effect` · sep · `Preview in client` · `Reload in client` + right cluster `lblReloadBadge` 240px + `lblDirty` 140px); the main `SplitContainer` (Fill, **added FIRST**, Size set before SplitterDistance) = emitter tree left (≈280px) + a nested horizontal split right (param grid on top, AI-assist `UtinniTextbox` Consolas-9pt pane below); a status strip (Bottom, 22px) with `lblCounters` (`{groups} groups · {emitters} emitters · {raw} raw-preserved`); and the DEC-A3 footer (Bottom, 22px, `FontDisabled()`).
- **Emitter tree** reuses `IffChunkTree.LoadMutable(effect.SourceIff)` showing `PEFT → version → EMGP → EMTR → …`; selection drives the param grid off the node `Tag` (`MutableIffNode`).
- **Param grid (Field/Value/Type):** EMTR nodes show their typed `ParticleEmitterDescription` waveform/colorramp fields (control-point summaries); leaf chunks + raw-preserved emitters/effects render greyed `Colors.FontDisabled()` + `Consolas 9pt` + read-only + the LOCKED tooltip `This field isn't typed yet — its original bytes are preserved exactly and saved unchanged.` (D-05 made visible). Raw-leaf double-click opens `FormParamHexEditor` → `EditLeafPayload` (byte-safe, editor-local undoable).
- **Explain effect** (D-07/D-08): `ParticleReadAssist.ExplainAsync` shells `utinni-cli decode-iff <path>` (the same verb `summarize_particle` dispatches) with a 15s timeout backstop; results fill the read-only pane; error → `Couldn't read this effect — {reason}.` at `Color.Red`. NO codec call in the AI handler; no write path.
- **Preview in client** (D-09): state-encoded on `Game.IsRunning` AND `IsRetriggerHookReachable()` (= false this phase per 15-03); disabled with tooltip `No live client — start SWG to preview in-scene.`; reload badge degrades to LOCKED `Reloads on next scene change or relog.`. The live-capable copy `Re-triggers live instances on Preview.` is wired for the reachable path (15-08+).
- **DEC-A3 (D-11):** the boundary sentence surfaced verbatim as the dimmed footer.
- `ParticleSaveTargets` shim (modes 1/2/4 forwarding `effect.SourceIff` to `IffSaveTargets`/`TreRepackSaveTarget`; mode 3 disabled CF-03 with the LOCKED tooltip).
- Registered in `Plugin.cs` `GetForms()` inside try/catch; `GetSubPanels()` NOT widened (stays null, CON-M-01/02).
- TRE Browser `Open in Particle Editor` hand-off (`FormTreBrowser.cs`): visibility gated on `ParticleHandoffPolicy.ShouldOfferParticleEditor` at menu-Opening, content-gated on `IsParticlePayload` on click, mirroring the OT hand-off verbatim.

## Verification

- `dotnet test UtinniCoreDotNet.Tests --no-build -c Debug --filter ParticleHandoff`: **18/18** pass.
- UtinniPlugins **TJT solution builds Debug|x86 green via VS2026 MSBuild (exit 0)** — both `TheJawaToolbox.dll` (C++) and `TheJawaToolboxDotNet.dll` produced.
- Grep gates (all PASS): the 5 LOCKED copy strings verbatim (raw-preserved tooltip, live-capable badge, degraded badge, preview-unavailable tooltip, live-patch CF-03 tooltip) — each 1 hit; the DEC-A3 D-11 sentence — 1 hit; `SingletonFormClosePolicy.ShouldHideInsteadOfDispose` in FormClosing; `Game.IsRunning` enable guard (3 hits); the AI handler invokes `ParticleReadAssist.ExplainAsync` and contains NO `FromBytes` codec call (the two `FromBytes` hits are in the OPEN paths only); CF-09 add-order (`mainSplit` Fill added first, toolbar last); `GetSubPanels()` still `return null` (line 171, unchanged); FormParticleEditor.cs is 1112 lines (≥200).
- `Generated/UtinniCore.cs` regen churn from the C++ side of the TJT build reverted via `git checkout --` (never committed), per `project_utinnicore_cs_regen_churn`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `OpenSource.LooseFile.Path`, not `.FilePath`**
- **Found during:** Task 2 first build (CS error on `lf.FilePath`).
- **Issue:** The plan-implied property name was `FilePath`; the shipped `OpenSource.LooseFile` exposes `Path`.
- **Fix:** Used `lf.Path` at both call sites (LoadDocument + the Explain effect path-resolve).

**2. [Rule 3 - Blocking] `TreRecordIndexResolver` namespace**
- **Found during:** Task 2 build (CS0103: name does not exist).
- **Issue:** `TreRecordIndexResolver` lives in `UtinniCoreDotNet.Formats.Tre`, not `UtinniCoreDotNet.Saving`.
- **Fix:** Added `using UtinniCoreDotNet.Formats.Tre;` to FormParticleEditor.cs.

**3. [Rule 3 - Blocking] `ProcessStartInfo.ArgumentList` unavailable on .NET Framework 4.7.2**
- **Found during:** Writing `ParticleReadAssist`.
- **Issue:** `ArgumentList` is a .NET Core+ API; the TJT plugin targets .NET Framework 4.7.2.
- **Fix:** Built a quoted `Arguments` string (`decode-iff "<path>"`) so a path with spaces passes as one argument.

No other deviations — the plan executed as written (the read-only param-grid-this-phase + leaf-hex-edit + false-this-phase preview seam are all the documented intent from 15-02/15-03 and UI-SPEC Assumption 4, not scope cuts).

## Known Stubs

- **Per-field typed scalar inline editing is not wired this phase** (the param grid is a read-only surface; raw-leaf hex editing via the sub-editor IS wired through `EditLeafPayload`). This is the documented split: the 15-02 codec exposes `EditLeafPayload` (generic per-leaf) but no per-field-name typed edit seam, and UI-SPEC Assumption 4 scopes WaveForm/ColorRamp to a read-only summary + sub-editor floor (no graphical curve editor). The typed surface is fully VISIBLE (waveform/colorramp control-point counts, typed scalars) + the honest D-05 hex fallback is editable — this satisfies the plan's "shows the typed grid with honest D-05 hex fallback" goal. A future plan wires per-field typed scalar widgets atop `EditLeafPayload` + a re-encoded WaveForm/ColorRamp payload (exactly as the OT editor composes its codec).
- **`Preview in client` live retrigger is degraded by design** (`IsRetriggerHookReachable()` returns false) — this is the 15-03 honest finding (no reachable native hook this phase), surfaced as the LOCKED tier-(b) candor, NOT an accidental stub. 15-08 flips the single seam method to read the native `ParticlePreview.isRetriggerAvailable()` predicate.

## Threat Flags

None. The plan's three trust boundaries are all mitigated as designed: T-15-01 (untrusted `.prt`) — open composes the 15-02 truncation-safe/degrade-don't-abort codec, so a malformed file opens degraded rather than crashing; T-15-05 (preview hot-retrigger) — the button is state-encoded-disabled (no game-thread work this phase); T-15-07 (save escaping the root) — saves forward to the inherited Phase-8 `LooseOverridePath` containment + atomic write + `FormSaveConfirmDialog` for repack/discard. No new security surface introduced (no new endpoints, auth paths, file access patterns, or schema changes beyond the inherited save layer).

## Self-Check: PASSED

- FOUND: UtinniCoreDotNet/UI/ParticleHandoffPolicy.cs
- FOUND: UtinniCoreDotNet.Tests/UITests/ParticleHandoffPolicyTests.cs
- FOUND: The Jawa Toolbox/.../UI/Forms/FormParticleEditor.cs (+ .Designer.cs)
- FOUND: The Jawa Toolbox/.../UI/ParticleReadAssist.cs
- FOUND: The Jawa Toolbox/.../Saving/ParticleSaveTargets.cs
- FOUND: commit 26af8e6 (Task 1, Utinni) + commit 589f206 (Task 2, UtinniPlugins)
- 18/18 ParticleHandoff facts green; TJT solution Debug|x86 green (MSBuild exit 0).

---
*Phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals*
*Completed: 2026-06-07*
