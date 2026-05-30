---
phase: 10
type: cross-ai-plan-review
reviewers: [codex, cursor-agent]
date: 2026-05-30
verdict: ACCEPT-WITH-CHANGES
status: applied
---

# Phase 10 Plans — Cross-AI Peer Review ("the friends")

Two independent external AI CLIs reviewed the committed Phase 10 plan set
(`10-01` … `10-06-PLAN.md`) before execution, same as the Phase 2 / Phase 6
pre-merge review precedent.

- **CODEX** (`codex exec`, gpt-5.1-codex-max, read-only sandbox)
- **cursor-agent** (`cursor-agent.cmd -p --mode ask --trust`)

**Both verdicts: ACCEPT-WITH-CHANGES.** The two reviews converged independently
on the same core findings with nearly identical ranking — high signal. The first
pass surfaced 5 headline findings (F1–F5); the deeper second pass surfaced the
remaining correctness-hardening items (F6–F12). All 12 are dispositioned below;
F1 was fixed at review time (frontmatter), F2–F12 are now folded into the plans.

This is the authoritative review record. Each finding lists: reviewer(s),
severity, the concrete issue, the fix, and disposition.

---

## F1 — 10-02 `depends_on` / `wave` bug — BLOCKING (build correctness)
- **Reviewer(s):** both (CODEX + cursor-agent)
- **Severity:** BLOCKING
- **Issue:** `10-02-PLAN.md` declared `depends_on: []` / `wave: 1`, but its Task 1
  consumes 10-01's public API (`StringTableDocument.FromBytes`,
  `StringTableWriter.Serialize`). "Different project so parallel-safe" is wrong —
  different project, **same API dependency**. A wave scheduler deriving order from
  frontmatter would launch 10-02 in Wave 1 against types that don't exist yet →
  clean-run build failure.
- **Fix:** `10-02` → `wave: 2`, `depends_on: ["10-01"]`.
- **Disposition:** ✅ FIXED (frontmatter). 10-02 frontmatter set to `wave: 2`,
  `depends_on: ["10-01"]`.

## F2 — Canonical-ordering byte-exactness assumed, not proven vs real files — HIGH (SC4)
The "full re-serialize == byte-exact" claim rests on real client `.stf` being
stored strings-id-ascending / names-name-ascending (Ordinal). RESEARCH A6 lists
this UNCONFIRMED; synthetic builder output always agrees with itself and cannot
catch this bug class. Split into three concrete sub-fixes:

### F2a — writer verbatim short-circuit
- **Reviewer(s):** both
- **Severity:** HIGH
- **Issue:** A no-edit round-trip should be byte-exact unconditionally, not
  dependent on the A6 ordering assumption.
- **Fix:** `StringTableWriter.Serialize` — when ZERO entries are dirty/added, emit
  the captured ORIGINAL whole-file bytes VERBATIM and skip re-serialization. The
  Document captures the full original `byte[]` at `FromBytes`.
- **Disposition:** ✅ APPLIED to 10-01 (truths + writer behavior + FromBytes
  capture + a WriterTests fact).

### F2b — original-byte preservation (no re-encode)
- **Reviewer(s):** both
- **Severity:** HIGH
- **Issue:** When some entries are dirty, untouched entries (including
  malformed-UTF-16 / U+FFFD text) must not be lost by re-encoding.
- **Fix:** Untouched entries serialize from captured ORIGINAL string-block bytes,
  NEVER via `Encoding.Unicode.GetString(...).GetBytes(...)`; only dirty/added
  entries re-encode. Acceptance: a malformed-UTF-16 entry round-trips
  byte-identically when untouched (with another entry dirty).
- **Disposition:** ✅ APPLIED to 10-01 (truths + writer behavior + WriterTests fact
  + a BuildV1MalformedUtf16 fixture).

### F2c — real `.stf` golden in the CLI gate
- **Reviewer(s):** both
- **Severity:** HIGH
- **Issue:** Synthetic builder output cannot fire the A6 assumption; only a real
  engine-produced file can.
- **Fix:** Commit ≥1 real extracted `.stf` (e.g. TRE-extracted) as a whole-file
  byte-exact no-mutate golden so A6 fires in CI. If it can't be committed for
  licensing, the executor MUST source one / document the blocker — it is an SC4
  gate prerequisite, not optional.
- **Disposition:** ✅ APPLIED to 10-02 (truths + a named real-extracted golden fact
  + files_modified entries for the real `.stf` + its golden JSON).

## F3 — Name validation is a subset of the engine rules — MEDIUM (SC2)
SOE `validateStringName` is stricter than no-leading-digit + no-duplicate. Split
into three sub-fixes:

### F3a — Wave-0 blocking source read
- **Reviewer(s):** both
- **Severity:** MEDIUM
- **Issue:** RESEARCH A7 (the exact engine validation/auto-name rules) is open;
  writing `ValidateName` before reading the source guesses the ruleset.
- **Fix:** Make reading SOE `validateStringName`, `rename`, and `addString`
  (`%03ld_default`) in `LocalizedStringTable{,ReaderWriter}.cpp` a BLOCKING first
  step before `ValidateName`/`AddEntry` are written.
- **Disposition:** ✅ APPLIED to 10-01 (Task 1 `read_first` now leads with a
  BLOCKING source-read step; resolves A7).

### F3b — full name-validation ruleset
- **Reviewer(s):** both
- **Severity:** MEDIUM
- **Issue:** The plan said "ASCII identifier / no leading digit"; the engine rule
  is stricter (first char `[a-z]`, remaining `[a-z0-9_+]`). The editor would accept
  `Foo` / `foo bar` the client can't resolve → silent per-entry SC2/SC3 failure.
- **Fix:** `ValidateName` enforces the FULL rule: non-empty; first char `[a-z]`;
  remaining `[a-z0-9_+]`; duplicate rejected. (`rename()` is weaker but the editor
  enforces the stricter `validateStringName` on add AND rename.) Acceptance tests:
  ACCEPT `creature_name`, `foo+bar`; REJECT `3foo`, `Foo`, `foo bar`, ``, duplicate.
- **Disposition:** ✅ APPLIED to 10-01 (truths + ValidateName behavior + acceptance
  cases in the controller-tests provides).

### F3c — form delegates, no subset re-implementation
- **Reviewer(s):** cursor-agent
- **Severity:** MEDIUM
- **Issue:** The WinForms Key-cell validator could re-implement a subset of the
  rules and drift from the framework predicate.
- **Fix:** The Key-column cell validator must DELEGATE to the framework
  `ValidateName` (no subset re-implementation in the form). Verify step greps the
  form for `ValidateName` usage.
- **Disposition:** ✅ APPLIED to 10-03 (truths + CellValidating behavior +
  the existing `grep ValidateName` verify step retained as the F3c gate).

## F4 — UTF-16 fidelity golden is BMP-only + name-block edge cases — MEDIUM (SC4)
`charCount` is a UTF-16 **code-unit** count, not a code-point count; `João` is
all-BMP and won't catch a surrogate-pair off-by-one. Split:

### F4a — fixtures
- **Reviewer(s):** both
- **Severity:** MEDIUM
- **Issue:** Missing fixtures for malformed-UTF-16, no-name-block, partial-names,
  and non-BMP surrogate pairs (the surrogate case guards the code-unit off-by-one).
- **Fix:** Add those four fixtures to the writer/round-trip tests.
- **Disposition:** ✅ APPLIED to 10-01 (StringTableFixtures + WriterTests facts).

### F4b — name-block emit + read rule
- **Reviewer(s):** CODEX
- **Severity:** MEDIUM
- **Issue:** Emit/read behavior for name-less entries was unspecified.
- **Fix:** Writer emits a name row only for entries that HAVE a name; header
  `count` = string-entry count (= SOE `m_map.size()`); define read behavior when
  `nameMap.size() < count` (entries with no matching name row are tolerated by the
  decoder).
- **Disposition:** ✅ APPLIED to 10-01 (writer behavior + FromBytes read behavior).

### F4c — CLI goldens mirror the new fixtures
- **Reviewer(s):** both
- **Severity:** MEDIUM
- **Issue:** The CLI gate must exercise the same edge cases as the framework.
- **Fix:** Add CLI goldens mirroring 10-01's new fixtures (malformed-UTF-16,
  no-name-block, partial-names, non-BMP surrogate).
- **Disposition:** ✅ APPLIED to 10-02 (fixture-builder + named goldens + facts).

## F5 — sourceCrc-on-edit policy unverified vs client — MEDIUM (SC3)
"Preserve on edit, 0 on add" is fine for deterministic goldens, but RESEARCH A5
("does the client read sourceCrc at lookup?") was open. Split:

### F5a — document the read semantics now
- **Reviewer(s):** both
- **Severity:** MEDIUM
- **Issue:** The decision was being deferred to the planner.
- **Fix:** Resolve now — read SOE `LocalizationManager::getLocalizedStringValue` to
  confirm lookup is by table/name/id and does NOT consult `sourceCrc` (review
  consensus: it does NOT), so preserve-on-edit + 0-on-add is correct. Record in the
  SUMMARY rather than deferring.
- **Disposition:** ✅ APPLIED to 10-01 (a new sourceCrc-read-semantics truth + the
  `getLocalizedStringValue` read folded into the F3a blocking read + SUMMARY output).

### F5b — SC3 sign-off cannot be automation-only
- **Reviewer(s):** both
- **Severity:** MEDIUM
- **Issue:** SC3 ("live client renders edited strings on reload") can't be signed
  off by automation alone; a stale preserved crc on an edited entry could
  (theoretically) make the edit not render.
- **Fix:** Require EITHER live Step 7 (edit → reload/scene-change → confirm the
  edited text renders) OR, if reload is relog-only (A8), an honest relog-badge
  amendment plus a ROADMAP-SC3=relog interpretation note. Add an explicit stale-crc
  check: edit an entry (leaving sourceCrc stale), confirm it renders; if not, flip
  edited-entry crc to `int(time(0))` and record it. Automation-only is insufficient
  for SC3 specifically (acceptable for SC1/SC2/SC4 per Phase 8/9 precedent).
- **Disposition:** ✅ APPLIED to 10-06 (Step 7 stale-crc + SC3-requires-live wording;
  Option C carve-out; success-criteria + threat + resume-signal updated).

## F6 — Golden-contract split (canonical vs non-canonical) — MEDIUM (SC4 honesty)
- **Reviewer(s):** both
- **Severity:** MEDIUM
- **Issue:** The canonical no-mutate goldens assert whole-file byte equality vs the
  original, but `v1-reorder` (non-canonical input) normalizes on save — it cannot
  also be `whole-file` vs its original bytes.
- **Fix:** Split the contract: canonical/no-mutate → whole-file equality vs original;
  non-canonical → assert output equals a separately-computed canonical serialization
  with `comparisonGranularity="canonical-normalized"`.
- **Disposition:** ✅ APPLIED to 10-02 (truths + the reorder fact + envelope
  granularity labels + verification item 4).

## F7 — `unfubar` grep contradiction — LOW (verify-gate correctness)
- **Reviewer(s):** CODEX
- **Severity:** LOW
- **Issue:** 10-01's verify both (a) tells the writer to carry an explanatory comment
  mentioning `unfubar` AND (b) asserts `grep -c unfubar == 0` — contradictory.
- **Fix:** Change the grep to match only an actual implementation CALL
  (`unfubar[A-Za-z]*\(`) so a comment mention is allowed but no smart-quote rewrite
  is invoked. Intent: NO smart-quote rewrite implemented; explanatory comment OK.
- **Disposition:** ✅ APPLIED to 10-01 (verify grep + the `<done>` + verification
  item 3 reworded).

## F8 — CSV import key validation must block before apply — MEDIUM (SC2)
- **Reviewer(s):** both
- **Severity:** MEDIUM
- **Issue:** `PlanImport` could let invalid/empty/duplicate CSV keys through and the
  invalidity would only surface inside `ApplyCsvImport` (entries created then found
  invalid).
- **Fix:** `PlanImport` produces an `Invalid` list (bad-charset / empty / duplicate
  CSV row / duplicate-after-fixup) and the preview modal BLOCKS Import (disabled
  button + red invalid-key list) when it's non-empty, BEFORE applying.
  `ApplyCsvImport` is only ever handed a clean plan (defensive precondition). Add an
  acceptance test.
- **Disposition:** ✅ APPLIED to 10-04 (truths + CsvImportPlan `Invalid` field +
  PlanImport behavior + ApplyCsvImport precondition + modal block + CsvCoercionTests
  facts + success-criteria).

## F9 — Null-name entries decision — LOW (robustness)
- **Reviewer(s):** CODEX
- **Severity:** LOW
- **Issue:** The decoder tolerates entries with no name; the model needed a decision
  for them.
- **Fix:** Default to reject-at-load-with-warning ("corrupt — save will normalize");
  id-based remove is a possible future affordance. Add an acceptance note.
- **Disposition:** ✅ APPLIED to 10-01 (FromBytes null-name policy + SUMMARY output).

## F10 — MarkSaved rebaseline algorithm — MEDIUM (undo correctness)
- **Reviewer(s):** both
- **Severity:** MEDIUM
- **Issue:** The MarkSaved rebaseline was left as "OR an equivalent rebaseline",
  risking an undo after save that returns pre-first-edit bytes.
- **Fix:** Pin the algorithm: after save, re-capture each entry's string slice +
  the whole-file bytes as the new baseline, clear `IsDirty`/`IsAdded`, reset the
  controller baseline. Test: edit → save → edit same cell → undo returns the
  POST-save bytes (not the pre-first-edit bytes).
- **Disposition:** ✅ APPLIED to 10-01 (pinned MarkSaved behavior + the F10 test in
  the controller-tests behavior/provides).

## F11 — Magic-sniff hand-off liveness — MEDIUM (SC2 entry point)
- **Reviewer(s):** cursor-agent
- **Severity:** MEDIUM
- **Issue:** The TRE-Browser "Open in String-table Editor" gate uses a
  `LooksLikeStf` magic sniff, but the call site may not have the file payload at
  context-menu-Opening time — shipping a path the tests can't exercise.
- **Fix:** Add a Wave-0 verification that the call site HAS the payload at
  menu-Opening. If null/lazy, fall back to extension-based detection (`.stf`) so
  extension-less entries still get the menu and the magic-sniff path is only a
  secondary affordance unit-tested in isolation. Don't ship an unexercisable sniff.
- **Disposition:** ✅ APPLIED to 10-05 (a magic-sniff-liveness truth + the FormTreBrowser
  Wave-0 check behavior + a payload==null extension-only policy fact + the SUMMARY note).

## F12 — Grid revert on undo/redo — LOW (UI/model sync)
- **Reviewer(s):** cursor-agent
- **Severity:** LOW
- **Issue:** Undo/Redo could refresh only dirty visuals, letting the grid desync
  from the model (e.g. an undone Add leaving a stale grid row).
- **Fix:** Undo/Redo performs a FULL grid row revert (re-sync affected rows / added
  / removed) from the model, not just a dirty-visual refresh.
- **Disposition:** ✅ APPLIED to 10-03 (Undo/Redo behavior + the Task 1 `<done>`).

---

## Net
Ship-ready after F1 (the only hard build bug — fixed at review time). F2–F12 are
correctness-hardening that both reviewers recommended folding in BEFORE execution
rather than rediscovering at smoke. None are architectural; all were surgical
additions to 10-01 / 10-02 / 10-03 / 10-04 / 10-05 / 10-06. All 12 findings are now
dispositioned and applied — `status: applied`.
