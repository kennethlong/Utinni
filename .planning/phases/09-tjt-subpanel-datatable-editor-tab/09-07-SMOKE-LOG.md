# Phase 9 Plan 07 — Datatable Editor Tier-4 Live-SWG Smoke Log

**Plan:** 09-07 (Wave 6, single checkpoint task)
**Outcome framing:** `smoke=automation-augmented; live ACK deferred-but-acceptable for V1`
(Phase 8 Plan 07 precedent — 08-05/06/07 maintainer-approved automation-augmented sign-off)
**Status:** AUTOMATION PRE-CHECKS COMPLETE — awaiting maintainer live session.
**Consolidation note:** This log folds in the **deferred Plan 09-03 editor-host live smoke**
(batched per the user's 2026-05-29 decision in `.deferred-live-uat.md`) so the maintainer runs
ONE combined session. The two surfaces are sectioned separately below:
- **Part A** — 09-03 editor-host smoke (NO live SWG required; open-from-disk path).
- **Part B** — 09-07 Tier-4 live-SWG smoke (requires an injected SWGEmu / Restoration client).

---

## Section 0 — Automation-Augmented Pre-Checks (executor-run, 2026-05-30)

These are the gates the plan's `<verification>` block requires to be green BEFORE the live smoke
runs. The executor (Claude) ran them verbatim on this Windows host. The bulk of Phase 9's testable
surface (on-disk save contracts, parse/serialize round-trip, SC4 byte-exact-on-untouched, V6000
refusal, repack atomic-replace, reload-routing) is covered by these ~120+ automated facts across
Plans 09-01..06 — the live smoke residual covers ONLY the MEF + injection + scene-change + cell-edit
UX surface that cannot be automated without a mock SWG client (deferred to V2 per
REQ-V2-tier-3-mock-d3d9).

| # | Pre-check | Command | Result |
|---|-----------|---------|--------|
| P0.1 | Utinni.sln Debug\|x86 build | `MSBuild Utinni.sln /p:Configuration=Debug /p:Platform=x86` | **PASS** — all projects built; UtinniCore.dll + UtinniCoreDotNet.dll + utinni-cli.exe + test DLLs emitted |
| P0.2 | Utinni.sln Release\|x86 build | `MSBuild Utinni.sln /p:Configuration=Release /p:Platform=x86` | **PASS** — exit 0 (only pre-existing xUnit-analyzer warnings, out of scope per SCOPE BOUNDARY) |
| P0.3 | TJT solution Debug\|x86 build | `MSBuild 'The Jawa Toolbox/TheJawaToolbox.sln' /p:Configuration=Debug /p:Platform=x86` | **PASS** — `TheJawaToolbox.dll` + `TheJawaToolboxDotNet.dll` deployed to `bin\Debug\Plugins\TheJawaToolbox\` (where the host loads it) |
| P0.4 | TJT solution Release\|x86 build | `MSBuild 'The Jawa Toolbox/TheJawaToolbox.sln' /p:Configuration=Release /p:Platform=x86` | **PASS** — exit 0 |
| P0.5 | UtinniCoreDotNet.Tests | `dotnet test UtinniCoreDotNet.Tests --no-build -c Debug` | **PASS — 475 / 475** (0 failed, 0 skipped) |
| P0.6 | Utinni.Cli.Tests | `dotnet test Utinni.Cli.Tests --no-build -c Debug` | **PASS — 139 passed / 1 skipped** (the 1 skip = CotMasterIndex search-TOC fixture, known optional-fixture skip, pre-existing) |
| P0.7 | PreservationAudit subset | `dotnet test UtinniCoreDotNet.Tests --filter FullyQualifiedName~PreservationAudit` | **PASS — 23 / 23** |
| P0.8 | TJT datatable source compiled into deployed assembly | source presence: `FormDatatableEditor(.Designer).cs`, `FormCsvImportPreviewDialog.cs`, `FormTypeChangeCascadeDialog.cs`, `FormAddColumnDialog.cs`, `DatatableColumnFactory.cs`, `DatatableHashStringEditor.cs`, `DatatableSaveTargets.cs`, `DatatableCsvSerializer.cs` all present + compiled (DLL emitted) | **PASS** — all Phase 9 datatable forms/widgets/save-targets present in `TheJawaToolboxDotNet.dll` |

**Pre-check verdict:** ALL GREEN. The automation surface that Plan 09-07's `must_haves.truths[0]`
("All Phase 9 automation gates are green on CI before the smoke runs") requires is satisfied on
both `Debug|x86` and `Release|x86` across both repos. The live smoke (Parts A + B below) is the
only remaining residual.

> Note on the x86/x64 reflection probe: a PowerShell `Assembly.LoadFile` of the x86 plugin DLL from
> a 64-bit PowerShell host throws `BadImageFormatException (0x800700C1)` — this is a host-bitness
> mismatch in the inspector, NOT a build defect. The DLL is a valid x86 image (MSBuild emitted it;
> the source files are all present and compiled). Type-presence is therefore confirmed via source +
> successful compile, not runtime reflection.

---

## Part A — Plan 09-03 Editor-Host Live Smoke (NO live SWG required)

> Deferred from Plan 09-03 (Task 4 live-host checkpoint) and batched here per the user's
> 2026-05-29 decision. This is the Phase-8 `FormIffEditor` open-from-disk pattern: the editor host
> runs WITHOUT an injected SWG client. Human-verify only — code is built + tested green in both repos.

**Setup:** Launch the Utinni editor host WITHOUT live SWG (standalone editor-host process, the same
way the Phase 8 IFF Editor open-from-disk path is exercised).

| Step | What to verify | Result (maintainer) |
|------|----------------|---------------------|
| A1 | Open the Datatable Editor from the TJT menu — window is **1200×760**, dark theme, toolbar docked top, status strip docked bottom, empty-state copy renders. | ☐ pending |
| A2 | Open a real `.tab` — OR synthesize one: `File.WriteAllBytes(@"C:\temp\test.tab", DatatableFixtures.BuildV1AllTypes())`. Grid populates. | ☐ pending |
| A3 | Per-type columns render correctly: ints = numeric, bool = checkbox, enum = dropdown, **DT_HashString = stored int32 + floating Consolas-9pt `{source} → 0x{hash:X8}` preview** when a cell is edited. | ☐ pending |
| A4 | Edit a text / int / bool cell, Tab away — change persists (commit-on-`CellEndEdit`, NOT per-keystroke). | ☐ pending |
| A5 | Close via the X button, re-open from the menu — **NO `ObjectDisposedException`** (hide-not-dispose under live MEF; RESEARCH § Pitfall 5). | ☐ pending |
| A6 | Reload badge reads the LOCKED CF-05 copy `Reloads on next scene change.`; Reload-in-client writes the locked CF-05 copy. | ☐ pending |
| A7 | With no document loaded, Save▾ / Add row / Import CSV… / Find are disabled **with tooltip** (honest-disabled, NOT throwing). | ☐ pending |

**Part A pass condition:** A1–A7 all pass (or document defects per step). This is a pure WinForms /
open-from-disk surface; no SWG dependency. A defect here likely indicates a Plan 09-03 Designer /
Plugin.cs regression.

---

## Part B — Plan 09-07 Tier-4 Maintainer Live-SWG Smoke

> Requires an injected SWGEmu / Restoration client per the standard injection workflow
> (memory `project_swg_context_routing` for the input pipeline + `project_scene_change_via_tjt`
> for the inject-and-load loop). Per memory `project_tjt_scene_change_naked_baseline.md`, the user
> lands naked after a TJT-driven scene change — this is BASELINE, not a regression.

### B.0 — Smoke disposition (choose ONE; record rationale)

- **Option A (full live observation):** Run Steps B1–B13 against an injected session.
- **Option B (automation-augmented):** Review the automation surface (P0 pre-checks all green) +
  run the minimum live subset (Steps B1–B3 + B5 + B6 + B8 + B13); defer B4/B7/B9/B10/B11/B12 with
  the deferred-acceptable vocabulary. **← recommended, mirrors Plan 08-07.**
- **Option C (automation-only):** Accept the P0 automation surface alone as sufficient for V1
  sign-off (Plan 08-06 Task 5 precedent — maintainer "approved, dig in" 2026-05-28); defer the full
  live smoke to a later observation pass.

**Chosen option:** ☐ _____  **Rationale:** _________________________________________________

### B-step checklist

| Step | What to verify | Result |
|------|----------------|--------|
| B1 | **Build + launch:** both repos Debug\|x86 (done in P0); launch editor host alongside an injected SWGEmu/Restoration client. No JIT debugger pop on startup; TJT loads; Datatable Editor menu item is registered in GetForms (Plugin.cs try/catch). | ☐ pending |
| B2 | **Entry point 1 — File picker (D-10.1):** open editor via TJT menu → `Open…` → pick a real `.tab` (extract via Phase 7 TRE Browser if needed). Grid populates; per-type widgets render; DT_HashString floating hash preview shows in Consolas 9pt; reload-status badge reads `Reloads on next scene change.` | ☐ pending |
| B3 | **Entry point 2 — TRE Browser hand-off (D-10.2):** TRE Browser → right-click a `.tab` entry → `Open in Datatable Editor`. Editor opens (or activates the singleton); grid populates; provenance = `OpenSource.TreArchive` (verify indirectly: Save loose override + Save As + Repack ENABLED; Save in place DISABLED; Patch live client DISABLED-inherited). **Disposition the known D-10 visibility gap (iter-2 item 13): a DTII-rooted payload under a NON-`.tab` entry name does NOT surface this context item — the user must use the IFF Editor `Switch to typed datatable view` detour (Step B4). Record as acceptable V1 UX (matches D-10 manual-hand-off-only scope), NOT a defect.** | ☐ pending |
| B4 | **Entry point 3 — IFF Editor hand-off (D-10.3):** IFF Editor → load a `.tab` / DTII-rooted IFF → new menu item `Switch to typed datatable view` appears (predicate: `Root.TypeId == "DTII"`). Click → FormDatatableEditor opens with the same document; IFF Editor stays open (manual hand-off, NOT auto-close). | ☐ pending |
| B5 | **Edit + structural ops:** edit a cell (commit-on-`CellEndEdit`) → row-header ● glyph + leading ● in title + `lblDirty` reads `Unsaved changes` at `Colors.Secondary()`. Ctrl+Z undo / Ctrl+Y redo both work. `Add row` → new row with ＋ glyph. `Add column…` → FormAddColumnDialog → pick name+type → `Add column` → new column appears. | ☐ pending |
| B6 | **Type-change cascade (D-04 — high-leverage):** right-click column header → `Change column type` → pick a type that fails mangling on some cells (e.g. DT_String→DT_Int on non-integer strings). FormTypeChangeCascadeDialog opens; affected cells listed; banner reads `Save is disabled until every cell is resolved.` at `Colors.Secondary()`; **verify Save▾ button face AND EVERY menu item are disabled** with tooltip `Resolve N cell(s) that need review before saving.` (UI-SPEC R-04 — all items, not just the top button). `Revert type change` → cascade reverts; save items re-enable. | ☐ pending |
| B7 | **Column reorder safety-net (D-02):** drag a column header → FormSaveConfirmDialog opens with LOCKED body `This may break runtime consumers that read columns by index. Proceed?` + Color.Red + Proceed/Cancel + `Don't ask again this session`. Tick checkbox + Proceed. Drag another column → modal does NOT re-appear (session-suppress works). | ☐ pending |
| B8 | **Save modes (Plan 09-05):** with a `.tab` loaded from a `.tre` (Step B3 provenance): `Save loose override` → green `Saved <name> (loose override)` + file under resolved loose-override dir. `Save As…` → green `Saved <name> (save as)` + file at picked path. `Repack into source .tre…` → FormSaveConfirmDialog → Proceed → `Saved <name> (repack)` OR the V6000 refusal copy if archive is V6000. `Save in place` → DISABLED (Source = TreArchive ≠ LooseFile) with explaining tooltip. | ☐ pending |
| B9 | **TJT scene-change reload (CF-05 / PROD-W1-DT(3)):** with an edit saved (Step B8), trigger a TJT chat-command scene change (e.g. `loadScene Naboo` per `project_scene_change_via_tjt`). After load, the edited datatable value is visible in-game. (Landing naked = baseline per `project_tjt_scene_change_naked_baseline`.) | ☐ pending |
| B10 | **CSV import (D-08 byte-exact-on-untouched):** `Export CSV…` → file created (header row + optional `#`-prefixed type row + data rows + UTF-8 BOM). Modify ONE cell in the CSV (pick a **DT_Int / DT_Float / DT_String** column — NOT DT_HashString; the on-disk int32 hash means a saved HashString cannot CSV-round-trip a source string, iter-2 item 9/12). `Import CSV…` → FormCsvImportPreviewDialog with locked D-08 wording `1 cell will change. N cells will stay as original bytes. 0 cells in the CSV would be type-invalid and will be skipped.` → `Import` → single cell changes, others stay clean (no ●). Save → bytes equal direct single-cell edit. | ☐ pending |
| B11 | **Find/Replace (D-07):** Ctrl+F → Find pane slides in; type query → matches highlight (Colors.Secondary @40%); `lblFindCount` reads `{i} / {n}`; F3 next / Shift+F3 prev. Ctrl+H → Replace pane; `Replace` applies one (undoable). `Replace all` with a type-invalid value in a DT_Int column → status strip surfaces red validation copy AND no invalid replacements applied (valid ones were). | ☐ pending |
| B12 | **Column-click sort (D-09 view-only):** click header → ascending + ▲; again → descending. Hover glyph → LOCKED tooltip `View order only — save serializes physical row order.`. Save → on-disk row order is the ORIGINAL physical order (verify by reopening OR `utinni-cli roundtrip-tab`). | ☐ pending |
| B13 | **Singleton hide-not-dispose (RESEARCH § Pitfall 5):** close FormDatatableEditor via X; re-open from any of the 3 entry points → NO `ObjectDisposedException`; form re-opens with empty OR previously-loaded state (either acceptable per Plan 09-03 discretion). | ☐ pending |

### Pass conditions

- **Option A pass:** B1–B13 ALL pass (or document defects).
- **Option B pass:** B1–B3 + B5 + B6 + B8 + B13 pass (minimum V1 surface); B4/B7/B9/B10/B11/B12 deferred-acceptable.
- **Option C pass:** maintainer accepts P0 automation alone (CI green + ~120 facts); document the live-ACK deferral citing Plan 08-06 Task 5 precedent.

### Fail (blocking) conditions

- **B1–B3 critical** (form doesn't load at all) → BLOCKING; likely Plan 09-03 Plugin.cs/csproj regression → Plan 09-08 inline fix.
- **B6 critical** (cascade doesn't surface OR Save▾ items don't disable) → BLOCKING; Plan 09-04 NeedsReview gate regression → Plan 09-08 fix.
- **B8 critical** (saves don't write OR write corrupted bytes) → BLOCKING; Plan 09-05 dispatch OR Plan 09-01 writer bug → investigation + gap-closure.
- **Any other defect** → deferred-acceptable per Phase 8 precedent UNLESS it affects SC4 (byte-exact correctness) — those are ALWAYS blocking.

---

## Open Question Dispositions

| Open Q | Subject | Disposition (to be finalized at maintainer sign-off) |
|--------|---------|-------------------------------------------------------|
| OQ1 | CRC reference values — was the `Crc::normalizeAndCalculate` port port-exact? | Automated coverage: Plan 09-01 ports SOE `crcNull = 0` (corrected from the speculative 0xFFFFFFFF per Crc.cpp:19,73-76) + DataTableHashCrc facts; a live DT_HashString round-trip (Step B10's HashString-aware note) would confirm parity end-to-end. **Deferred-but-acceptable for V1** if the live HashString observation is not run (Phase 8 Open Q1 precedent — cursor N-H1 ACK). |
| OQ2 | PackedObjVars / BitVector validator depth | Automated structural coverage at the framework cell layer; full live validation deferred-but-acceptable (Phase 8 precedent). |
| OQ5 | V6000 archive repack refusal under live client | Automated: V6000 refusal = `TreRepackResult.Failed` (TreWriter.Repack throws NotSupportedException), inherited from Phase 8 + Plan 09-05 composition shim. Live confirmation folds into Step B8. **Deferred-but-acceptable** if not run live (Phase 8 Open Q5 precedent). |

---

## V1 Sign-Off

**Automation pre-check status:** ALL GREEN (Section 0 — P0.1–P0.8).
**Live smoke status:** ☐ pending maintainer session.

**V1 sign-off recommendation:** ☐ APPROVED / ☐ APPROVED-WITH-DEFERRED-RESIDUAL / ☐ REQUIRES-GAP-CLOSURE

**Maintainer signature:** `Smoke approved by: ____________ on ____________; outcome: ____________`

**Resume signal (from the plan):** Type `approved` if the smoke passes per Options A/B/C; describe
any blocking defect(s). Include a one-line disposition (e.g. _"approved — Option B; live ACK deferred
for Steps B9–B12 per Phase 8 precedent; Open Q1 CRC parity confirmed via Step B10 CSV round-trip"_).

---

## Executor Notes (2026-05-30)

- This plan is `autonomous: false` (single `checkpoint:human-verify` task, gate `blocking-human`).
  Per the orchestrator letter + Phase 8 P05/P06/P07 precedent, the live ACK is deferred-but-acceptable
  for V1; the executor produced this consolidated artifact + ran the automation-augmented pre-checks
  and returned a checkpoint awaiting the maintainer's live session.
- ROADMAP Phase-9 plan progress is intentionally **left pending** the human live ACK — the
  orchestrator handles the deferred-but-acceptable completion decision with the user.
- IGNORED `UtinniCoreDotNet/Generated/UtinniCore.cs` churn (non-deterministic CppSharp regen noise;
  never staged/committed) per repo build rules.
