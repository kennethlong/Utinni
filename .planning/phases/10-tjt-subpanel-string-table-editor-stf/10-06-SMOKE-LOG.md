# Phase 10 Plan 06 — String-table Editor Tier-4 Live-SWG Smoke Log

**Plan:** 10-06 (Wave 5, single `checkpoint:human-verify` task, gate `blocking-human`)
**Outcome framing:** `smoke=automation-augmented; live ACK deferred-but-acceptable for V1`
(Phase 8 P05/P06/P07 + Phase 9 09-07 precedent)
**Status:** AUTOMATION PRE-CHECKS COMPLETE — awaiting maintainer live session.

> This plan is `autonomous: false`. The executor (Claude) cannot drive an injected SWG client, so it
> ran the automation-augmented pre-checks (Section 0) and laid out the live steps below as a maintainer
> checklist. The live steps are **pending** — the executor did NOT observe them and did NOT sign off.
> SC3 specifically (F5b) CANNOT be signed off by automation alone — it needs the Step 7 live observation
> or the honest relog-badge amendment.

---

## Section 0 — Automation-Augmented Pre-Checks (executor-run, 2026-05-30)

The gates the plan's `<verification>` block + `must_haves.truths[0]` require green BEFORE the live smoke.
The bulk of Phase 10's testable surface (flat-binary parse/serialize round-trip, SC4 byte-exact incl.
João, the F8 CSV invalid-key guard, PO export, name validation, view-only on-disk-order, the hand-off
policy gate, reload-tier routing) is covered by these automated facts across Plans 10-01..05 — the live
residual covers ONLY the MEF + injection + scene-change + manual-edit UX that needs a mock SWG client
(V2 — REQ-V2-tier-3-mock-d3d9).

| # | Pre-check | Result |
|---|-----------|--------|
| P0.1 | UtinniCoreDotNet (managed) Debug build | **PASS** — `bin\Debug\UtinniCoreDotNet.dll` emitted |
| P0.2 | UtinniCoreDotNet (managed) Release build | **PASS** — `bin\Release\UtinniCoreDotNet.dll` emitted |
| P0.3 | TJT plugin Debug\|x86 build | **PASS** — `bin\Debug\Plugins\TheJawaToolbox\TheJawaToolboxDotNet.dll` deployed |
| P0.4 | TJT plugin Release\|x86 build | **PASS** — `bin\Release\Plugins\TheJawaToolbox\TheJawaToolboxDotNet.dll` deployed |
| P0.5 | UtinniCoreDotNet.Tests (Release) | **PASS — 588 / 588** (0 failed, 0 skipped) |
| P0.6 | UtinniCoreDotNet.Tests (Debug) | **587 / 588** — the 1 failure is `NativeCallbacksHandleTests.Subscribe_DuringDispatch_NotInvokedInCurrentIteration_InvokedInNext`, a **pre-existing non-deterministic concurrency flake** in the native-callbacks dispatch suite (UNRELATED to Phase 10): it passes **7/7 in isolation** and passed clean in the Release full run. Not a Phase 10 gate failure. |
| P0.7 | Utinni.Cli.Tests (Release) | **PASS — 156 passed / 2 skipped** (skips = the F2c real-extracted `.stf` golden [no copyrighted fixture in-repo, CON-O-09] + a pre-existing optional-fixture skip) |
| P0.8 | Utinni.Cli.Tests (Debug) | **PASS — 156 passed / 2 skipped** |
| P0.9 | PreservationAudit subset | **PASS — 23 / 23** |
| P0.10 | TJT Phase-10 source compiled into the deployed assembly | **PASS** — `FormStringTableEditor(.Designer).cs`, `FormStfCsvImportPreviewDialog(.Designer).cs`, `StringTableCsvSerializer.cs`, `StringTableSaveTargets.cs` all present + compiled (DLL emitted); `Plugin.cs` registers `FormStringTableEditor` in `GetForms()`; `FormTreBrowser` carries the `Open in String-table Editor` item |

**Pre-check verdict:** GREEN (the 1 Debug-run failure is the documented pre-existing native-callbacks
flake, not a Phase 10 regression). The automation surface `must_haves.truths[0]` requires is satisfied.

> **Native `Utinni.sln` (C++) build note:** the full native solution build (UtinniCore.dll etc.) runs on
> the self-hosted CI / maintainer host (memory `project_self_hosted_ci`; v145/VS2026 is Insiders-only and
> the vendored CppSharp blocks a full clean regen — `project_vs2026_cppsharp_block`). The executor built
> the **managed** projects + the TJT plugin (both configs) that Phase 10 actually touches. The maintainer
> confirms the native host builds + launches as Step 1.
>
> Per repo build rules, `UtinniCoreDotNet/Generated/UtinniCore.cs` CppSharp regen churn was IGNORED
> (never staged/committed).

---

## Smoke disposition (choose ONE; record rationale)

- **Option A (full live observation):** run Steps 1–11 against an injected session.
- **Option B (automation-augmented):** review the Section-0 pre-checks (all green) + run the minimum live
  subset (Steps 1–5 + 6 + 7 + 11); defer 8/9/10 with the deferred-acceptable vocabulary.
  **← recommended (mirrors Plan 09-07 / 08-07).** NOTE: Step 7 is REQUIRED even under Option B/C — SC3
  (F5b) cannot be closed by automation alone.
- **Option C (automation-only):** accept the Section-0 automation surface alone for SC1/SC2/SC4 V1
  sign-off (Phase 8 P06 / Phase 9 09-07 precedent); **SC3 stays an explicit open residual** until Step 7
  (or the relog amendment) is recorded.

**Chosen option:** ☑ **Option C (automation-only)** — maintainer accepted the Section-0 automation
surface (744 facts across both suites + both-config builds green) for the V1 sign-off of SC1/SC2/SC4,
citing the Phase 8 P06 ("approved, dig in" on automation alone) + Phase 9 09-07 precedent.
**Rationale:** the full Phase 10 testable surface is automated and green; the live-SWG residual (MEF +
injection + scene-change UX) is deferred-but-acceptable for V1. **SC3 (Step 7) is NOT closed by this
sign-off** — it remains an explicit open residual (see below) per F5b.

---

## Live smoke step checklist (maintainer)

> Requires an injected SWGEmu / Restoration client per the standard injection workflow (memory
> `project_swg_context_routing` for the input pipeline + `project_scene_change_via_tjt` for the
> inject-and-load loop). Landing naked after a TJT-driven scene change is BASELINE, not a regression
> (`project_tjt_scene_change_naked_baseline`).

| Step | What to verify | Result |
|------|----------------|--------|
| 1 | **Build + launch:** native host + injected client; no JIT-debugger pop; TJT loads; the String-table Editor is registered (Plugin.cs try/catch `GetForms`). | ☐ pending |
| 2 | **Entry point 1 — File picker:** `Open…` → pick a real `.stf`; two-column grid populates (Key + Text); multi-line text legible; reload badge reads the LOCKED `Reloads on next scene change.`; `Show id` toggles the read-only id column. | ☐ pending |
| 3 | **Entry point 2 — TRE Browser hand-off (D-04):** right-click a `.stf` entry → `Open in String-table Editor` appears (gated on `StringTableHandoffPolicy`) → click → editor opens/activates with the entry loaded; provenance = TreArchive (Save loose override + Save As + Repack enabled; Save in place DISABLED). NO IFF-Editor hand-off for `.stf`. | ☐ pending |
| 4 | **T4 edit + validation:** edit a Text cell → ● row glyph + title ● + `lblDirty`; Ctrl+Z/Ctrl+Y undo/redo; `Add entry` → `{NNN}_default` row, Key cell in edit mode; rename to a valid ASCII key; INVALID key (leading digit / duplicate / empty) → Color.Red cell + red status copy + edit reverts; non-ASCII KEY rejected; non-ASCII TEXT allowed; `Remove entry` (undoable). | ☐ pending |
| 5 | **Save modes (10-05):** with a `.stf` from a `.tre` (Step 3): `Save loose override` (green; file under override dir); `Save As…` (file at picked path); `Repack into source .tre…` (confirm modal; Replaced/backup OR V6000 refusal copy); `Save in place` DISABLED (TreArchive ≠ LooseFile) with tooltip. | ☐ pending |
| 6 | **SC4 Unicode fidelity (live confirm of the automated João golden):** open/add a non-ASCII entry (`João`, or paste a smart-quote `"curly"` + ellipsis `…`); save; re-open. Text survives EXACTLY — NO smart-quote, NO `…`→`...`/`--` normalization, NO `Jo�o`→`João` typo-fix (STAB-03). **SC4 corruption = ALWAYS BLOCKING.** | ☐ pending |
| 7 | **CF-05 reload semantics + stale-crc (SC3 — REQUIRED, NOT automatable, F5b):** with an edit saved, trigger a TJT chat-command scene change. Observe whether the edited string renders. **If yes → SC3 confirmed, badge correct. If no (LocalizationManager relog-only) → record the finding + AMEND the CF-05 badge copy to honest relog wording (10-07 inline fix) + note ROADMAP SC3 read as relog.** **STALE-CRC CHECK:** the edited entry's `sourceCrc` is left stale (preserved, D-02b) — confirm the edited text still renders. If it renders, that confirms the 10-01 F5a finding (lookup does not consult sourceCrc). If it does NOT render because of the stale crc, flip the crc to `int(time(0))`, re-save, and record that the preserve-crc policy had to be amended. | ☐ pending |
| 8 | **CSV round-trip (D-03b):** `Export CSV…` → UTF-8 BOM + `key,text` header + non-ASCII survives; modify ONE text cell; `Import CSV…` → preview modal shows the LOCKED copy `{N} entries will change. {M} entries will stay as original bytes. New keys in the CSV will be added; missing keys are left untouched.` → Import → one entry changes, others clean; save → bytes match a direct single-cell edit. | ☐ pending |
| 9 | **Find/Replace + Filter + sort (D-03a/c):** Ctrl+F over key+text (case + regex, F3/Shift+F3); Replace into a key re-validates (invalid blocked). Ctrl+L filter hides non-matching rows (`{shown} / {total}`, view-only). Column-click sort ▲/▼ + locked tooltip `View order only — save serializes strings by id and names alphabetically.`; save → on-disk order unchanged. | ☐ pending |
| 10 | **PO export (D-03d):** `Export PO…` → `.po` with `msgid`=key / `msgstr`=text; non-ASCII survives. | ☐ pending |
| 11 | **Singleton hide-not-dispose:** close (X) → re-open from file picker / TRE Browser → NO `ObjectDisposedException`; re-opens cleanly. | ☐ pending |

### Pass conditions
- **Option A:** Steps 1–11 pass (or per-step defects documented).
- **Option B:** Steps 1–5 + 6 + 7 + 11 pass (minimum V1 surface); 8/9/10 deferred-acceptable.
- **Option C:** maintainer accepts Section-0 automation alone for SC1/SC2/SC4; **SC3 (Step 7) still required** or named as an explicit open residual.

### Fail (blocking) conditions
- Steps 1–3 (form does not load) → BLOCKING (Plugin.cs/csproj regression → 10-07 fix).
- Step 5 (saves write corrupted bytes) → BLOCKING (10-01 writer / 10-05 dispatch).
- Step 6 (SC4 corruption) → ALWAYS BLOCKING.
- Any other defect → deferred-acceptable UNLESS it affects an SC.

---

## CF-05 reload-semantics finding (the one open RESEARCH confirmation)

**Observed (Step 7):** ☐ scene-change reload works → badge copy KEPT  ☐ relog-only → badge copy AMENDED (10-07) + ROADMAP SC3 read as relog  ☑ **not yet run (SC3 = open residual)**

**Stale-crc result (Step 7):** ☐ edited text rendered with the stale (preserved) sourceCrc → confirms 10-01 F5a  ☐ required crc flip to render (preserve-crc policy amended)  ☑ **not yet run**

**Notes:** Step 7 deferred under the Option-C automation-only sign-off. The CF-05 badge copy is KEPT
as-shipped (`Reloads on next scene change.`) PENDING the live observation — it is not yet confirmed nor
amended. The automated `roundtrip-stf` João golden (10-02) + the `StringTableReloadRoutingTests`
(`Classify(".stf", null) == PendingNextSceneChange`) are the standing proxies; the live scene-change-vs-relog
confirmation + the explicit stale-crc check are the named residual. The 10-01 F5a source finding predicts
the stale (preserved) sourceCrc is harmless (lookup does not consult it), so the expected Step-7 result is
"edit renders" — but this is NOT yet live-confirmed.

---

## V1 Sign-Off

**Automation pre-check status:** GREEN (Section 0 — P0.1–P0.10; the P0.6 Debug-run flake is the documented pre-existing native-callbacks test, not a Phase 10 regression).
**Live smoke status:** DEFERRED (Option C — automation-only sign-off; live Steps 1–11 not run).

**V1 sign-off recommendation:** ☑ **APPROVED-WITH-DEFERRED-RESIDUAL**
- SC1 (subpanel loads), SC2 (open/edit/save), SC4 (non-ASCII round-trips, João) — **signed off** on the
  automation surface (744 facts; the SC4 `roundtrip-stf` João golden is the load-bearing proof) per the
  Phase 8 P06 / Phase 9 09-07 precedent.
- **SC3 (live client renders edited strings on reload) — OPEN RESIDUAL.** Per F5b it cannot be closed by
  automation; it needs the live Step-7 observation (scene-change reload vs LocalizationManager relog-only)
  + the explicit stale-crc check, OR the honest relog-badge amendment. The CF-05 badge copy is KEPT
  pending that observation.

**Maintainer signature:** `Smoke approved by: Kenneth Long on 2026-05-30; outcome: smoke=automation-augmented; live ACK deferred-but-acceptable for V1 (Option C); SC1/SC2/SC4 signed off (SC4 João via the automated roundtrip-stf golden); SC3 = OPEN RESIDUAL pending live Step-7 scene-change/relog observation + stale-crc check; CF-05 badge copy KEPT pending that observation.`

**Resume signal (from the plan):** Type `approved` if the smoke passes per Options A/B/C; describe any
blocking defect(s). Include a one-line disposition citing the option + the CF-05 finding + the F5b
SC3/stale-crc result (e.g. _"approved — Option B; SC4 João confirmed; SC3 = relog-only (badge amended via
10-07, ROADMAP SC3 read as relog); stale-crc harmless (edit rendered) confirming F5a; live ACK deferred
for Steps 8–10 per Phase 9 precedent"_). SC3 cannot be signed off by automation alone (F5b) — name it as
an open residual if Step 7 was not run.
