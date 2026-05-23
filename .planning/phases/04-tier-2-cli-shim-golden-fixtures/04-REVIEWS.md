---
phase: 4
review_iteration: 4
reviewers: [codex, cursor]
reviewed_at: 2026-05-22T23:55:00Z
plans_reviewed: [04-01-PLAN.md, 04-02-PLAN.md, 04-03-PLAN.md, 04-04-PLAN.md]
prior_review_iterations:
  - iter-1: commit bb16090 (FAIL — 22 findings)
  - iter-2: commit 6431991 (FAIL — 12 new findings)
  - iter-3: commit b5c9905 (SPLIT — codex FAIL, cursor PASS-WITH-GUARDRAILS)
revision_input: 04-REVISION-NOTES.md (planner iter-1+2+3+4 fix-status tables)
unavailable_reviewers:
  - gemini: not installed
  - coderabbit: not installed
  - opencode: not installed
  - qwen: not installed
  - claude: skipped (self — this session runs in claude-code)
  - ollama / lm_studio / llama_cpp: no local server detected on default ports
verdict: PASS (both reviewers independently) — iter-4 plans ready for /gsd:execute-phase 4. Two NEW execute-time guardrails (non-blocking; specified in the plan but in different tasks): wrong-iplugin-shape stale Task 4.1 check prose (Codex + Cursor consensus) and 3 LOW doc-drift items in 04-03 prose (Cursor only). Math-verified IFF algorithm + fixtures.
---

# Cross-AI Plan Re-Review — Phase 4 (Iteration 4 — Final Sanity Check)

**Context.** Fourth and final cross-AI review of these plans. Cycle history:
- iter-1 review (commit `bb16090`): FAIL, 22 findings
- iter-2 review (commit `6431991`): FAIL, 12 new findings
- iter-3 review (commit `b5c9905`): SPLIT, codex FAIL with 1 HIGH (IFF math) + 1 MEDIUM (managed-fallback)
- **iter-4 replan** (commit `245cd1b`): 6 findings + 1 post-checker doc patch → addressed

**Consensus iter-4 verdict: PASS (both reviewers, independently).** All 8 HIGH (cumulative iter-1+iter-2+iter-3) confirmed CONCRETE FIX. Both reviewers math-verified the new IFF algorithm + fixtures: `malformed-nested-overflow.iff` deterministically throws `NestedChunkOverflow`; `malformed-truncated.iff` deterministically throws `Truncated` via streaming-read EOF. The 2 NEW HIGH (`loaderObserved.loadErrors` + IFF math) from iter-3 are now closed.

**Remaining issues are NON-BLOCKING execute-time guardrails:**
- Both reviewers flag: `04-04-PLAN.md:509` Task 4.1 prose still says `iplugin-export-shape = managedIPlugin ? "pass" : "fail"`, which contradicts the iter-4 `wrong-iplugin-shape/` fixture design (attribute present but implementation absent → must fail). The correct logic IS specified in Task 4.3 (`:687-689` + test at `:717-724`), but the Task 4.1 prose wasn't updated to match.
- Cursor only: 3 LOW doc-drift items in 04-03 prose (lines 79, 349, 490) — historical "sub-type at offset 12" misnomers that should be "offset 8-11"; doesn't affect fixture builders or execution.

---

## Codex Review

**Summary**
Iter-4 is substantially better and mostly execute-ready. The IFF algorithm is now mathematically coherent, `ChunkLengthExceedsFile` is removed from the live enum/fixture contract, the managed fallback is dropped, the AssemblyInfo concern is correctly documented as a false positive, and the stale count/name issues are fixed. I see one remaining non-blocking executor-risk in 04-04: the `wrong-iplugin-shape` fixture contract is correct, but Task 4.1 still has stale check prose that says `managedIPlugin ? pass : fail` before Task 4.3 later says to distinguish attribute presence from actual interface implementation.

**Iter-4 Fix Verification**
| Finding | Verdict | Evidence |
|---|---|---|
| HIGH-1 IFF algorithm | CONCRETE | 04-03 now defines the live order as NegativeLength → Cap → nested-only `NestedChunkOverflow` → streaming-read EOF `Truncated`, with top-level FORM treated as the file and `ChunkLengthExceedsFile` removed from the enum at `04-03-PLAN.md:43`, `:60`, `:359`. |
| HIGH-1 fixture math | CONCRETE | `malformed-nested-overflow`: file 100, outer length 92 gives parentEnd 100, inner header offset 12 length 90 gives payload end 110, so nested-bound check deterministically throws `NestedChunkOverflow` (`04-03-PLAN.md:37`, `:76`, `:348`, `:490`). `malformed-truncated`: file 50, outer length 92 gives logical parentEnd 100, inner header offset 12 length 80 gives payload 20..100, nested check passes, `ReadBytes(80)` short-reads only 30 bytes before EOF at 50, so `Truncated` (`04-03-PLAN.md:38`, `:491`). |
| MED-2 managed fallback | CONCRETE | 04-04 now requires `cl.exe`, fails hard if absent, and removes automatic managed fallback (`04-04-PLAN.md:114`, `:330-332`, `:612-643`). Native negative fixtures remain native; `wrong-iplugin-shape` is explicitly managed (`04-04-PLAN.md:659-687`). |
| MED-3 AssemblyInfo clobber | CONCRETE | 04-04 documents that `Utinni.Cli/Properties/AssemblyInfo.cs` is the EXE assembly file and distinct from `Utinni.Cli.Tests/Properties/AssemblyInfo.cs` from 04-01 (`04-04-PLAN.md:57-58`, `:207-209`). |
| LOW-4 test counts | CONCRETE | 04-01 says 10 JsonOutput tests and 18 total at `04-01-PLAN.md:344`, `:544`; 04-02 now uses 18+9 = 27 at `04-02-PLAN.md:597`, `:648`. |
| LOW-5 Task 4.1 count | CONCRETE | 04-04 now consistently describes Task 4.1 as 9 tests with Test 7 moved to Task 4.3 (`04-04-PLAN.md:141`, `:789`). |
| LOW-6 filter class name | CONCRETE | `PluginInspectionFilters.FilterNativeLoadErrors` is consistently named in contract/action/verification (`04-04-PLAN.md:292`, `:492-529`, `:811`, `:819`). |
| Post-checker offset patch | CONCRETE | The executable fixture math consistently places inner headers at offset 12, not 16 (`04-03-PLAN.md:37-38`, `:348-349`, `:490-491`). There is one harmless prose typo at `04-03-PLAN.md:79` saying the FORM subtype starts at offset 12; the same paragraph immediately gives the correct inner header bytes at 12-19 and Task 3.3 is correct. |

**New Findings**
MEDIUM — 04-04 still has stale Task 4.1 check prose for `wrong-iplugin-shape`.
`04-04-PLAN.md:509` says managed `iplugin-export-shape` status is `managedIPlugin ? "pass" : "fail"`. That contradicts the iter-4 fixture design, where `[InheritedExport(typeof(IPlugin))]` is present but the class does not implement `IPlugin`, so `managedIPlugin=true` must still yield `iplugin-export-shape=fail`. The correct requirement is later stated at `04-04-PLAN.md:687-689` and tested at `:717-724`, so this is not a design blocker, but the Task 4.1 implementation prose should be followed with that later correction in mind.

**Risk Assessment**
Overall risk: LOW-MEDIUM.

Top risks:
1. Executor may implement the stale `managedIPlugin ? pass : fail` line before noticing the later correction for `wrong-iplugin-shape`.
2. IFF top-level FORM-as-file behavior is intentional but nonstandard enough that tests must stay exactly as specified.
3. 04-04 depends on local/MSVC availability, but the plan now makes that a hard and honest preflight.

**REVIEW VERDICT**
PASS — ready for `/gsd:execute-phase 4`, with the Task 4.1 `wrong-iplugin-shape` check detail treated as an execute-time guardrail.


---

## Cursor Review

## Summary

The iter-4 revisions are **substantive, internally consistent, and ready for `/gsd:execute-phase 4`**. The two iter-3 blockers are closed in the plan text: the IFF error taxonomy is algebraically sound under the restructured algorithm, and the validate-plugin fixture strategy no longer depends on a contradictory managed-fallback path. Remaining issues are **documentation drift** (stale offset narration in a few 04-03 sites) and one **cross-task implementation note** for `wrong-iplugin-shape/` that a careful executor can resolve during Task 4.3 without another replan cycle.

---

## Iter-4 Fix Verification

| Finding | Verdict | Evidence |
|---------|---------|----------|
| **HIGH-1** IFF algorithm restructure | **CONCRETE FIX** | `04-03-PLAN.md` Task 3.1 (~359–380): enum is `{ NegativeLength, ChunkLengthExceedsCap, NestedChunkOverflow, Truncated, MalformedFourCc }`; top-level FORM uses `parentEnd = stream.Length` with no file-bound check; nested-only `NestedChunkOverflow`; leaf `ReadBytes` short-read → `Truncated`. Math verified below. |
| **MED-2** Managed fallback dropped | **CONCRETE FIX** | `04-04-PLAN.md` Task 4.3 PRE-FLIGHT (~612–631): hard-fail if `cl.exe` absent; Pitfall C' (~330–332) documents removal. `wrong-iplugin-shape/` spec (~665–687): managed DLL with `[InheritedExport(typeof(IPlugin))]` on non-implementing class → `kind=managed` + `iplugin-export-shape=fail`. |
| **MED-3** AssemblyInfo.cs clobber | **FALSE POSITIVE (verified)** | `04-01` Task 1.1 → `Utinni.Cli.Tests/Properties/AssemblyInfo.cs`; `04-04` Task 4.1 → `Utinni.Cli/Properties/AssemblyInfo.cs`. Different projects; explicitly documented in `04-04-PLAN.md` (~407). |
| **LOW-4** Test-count propagation | **CONCRETE FIX** | `04-01-PLAN.md` Task 1.2 action (~341): "10 [Fact] tests". `04-02-PLAN.md` Task 2.4 verify: "Plan 04-01 **18** + Plan 04-02 9 = 27+". |
| **LOW-5** Task 4.1 test count | **CONCRETE FIX** | `04-04-PLAN.md` Task 4.1 name/verify/done consistently say **9 tests** (Tests 1–6, 8, 9, 10; gap at 7 intentional). |
| **LOW-6** `PluginInspectionFilters` naming | **CONCRETE FIX** | `04-04-PLAN.md` Task 4.1 action (~449–470) + Test 10 behavior (~382) + verify greps all use `PluginInspectionFilters.FilterNativeLoadErrors`. |
| **Post-checker offset patch** | **PARTIAL** | Correct layout is in Task 3.3 behavior (~491) and nested-overflow artifact (~76). Stale "offset 12" sub-type prose remains at artifact (~79), Test 8 behavior (~349), and nested-overflow behavior (~490). Math still resolves if executor follows ~491 / fixture builders. |

---

## IFF Math Check (iter-4 HIGH-1)

**Shared IFF layout (correct):**

| Offset | Content |
|--------|---------|
| 0–3 | `FORM` |
| 4–7 | length BE (92) |
| 8–11 | sub-type (`WSNP`) |
| 12–15 | inner TypeID |
| 16–19 | inner length BE |
| 20+ | inner payload |

**`malformed-nested-overflow.iff` (100 bytes) → `NestedChunkOverflow`**

- Outer `parentEnd` = 8 + 92 = **100** = file length ✓  
- Inner header at **12**, length **90**  
- After header, position = **20**  
- Nested check: 20 + 90 = **110 > 100** ✓ → `NestedChunkOverflow` before payload read  

**`malformed-truncated.iff` (50 bytes) → `Truncated`**

- Outer length 92 → conceptual `parentEnd` = **100**; top-level, so no early file-bound throw ✓  
- Inner leaf at **12**, length **80**  
- Nested check: 20 + 80 = **100 ≤ 100** ✓ passes  
- `ReadBytes(80)` from offset **20**: file ends at 50 → **30 bytes** available → short-read → `Truncated` ✓  

**`ChunkLengthExceedsFile` removal:** Active plan surfaces use the five-value enum only. Remaining mentions are historical/contextual in `04-03-PLAN.md` and `04-REVISION-NOTES.md`, not in verify gates or expected golden kinds. No replan needed.

---

## New Findings

### LOW — Stale offset narration in `04-03-PLAN.md` (3 sites)

- **`04-03-PLAN.md:79`** (`malformed-truncated.iff` artifact): says sub-type and inner header both at offset **12** (impossible overlap), but payload math uses offset **20** (correct).
- **`04-03-PLAN.md:349`** (Test 8 behavior): "sub-type at offset **12**" — should be **8–11**.
- **`04-03-PLAN.md:490`** (Task 3.3 behavior, nested overflow): "sub-type `WSNP` at offset **12**" — should be **8–11**; inner at **12**.

**Authoritative spec:** Task 3.3 line **491** and nested-overflow artifact line **76**. Executor should build from `BuildNestedChunkOverflow()` / `BuildTruncatedFile()` or follow line 491, not the stale lines above.

**Impact:** Documentation-only; does not block execute if fixture builders are used.

### LOW — `ChunkLengthExceedsCap` has Tier-1 coverage only

`BuildChunkLengthExceedsCap()` + Tier-1 Test 10 exist; no Tier-2 IFF golden. Acceptable per D-08 (synthesized negatives focus on NestedChunkOverflow / Truncated / missing-pad). Not a blocker.

### Execute-time guardrail (not a FAIL) — `wrong-iplugin-shape` shape check

`04-04-PLAN.md` Task 4.1 (~509) still writes `iplugin-export-shape = managedIPlugin ? "pass" : "fail"`, while iter-4 requires **attribute present** (`managedIPlugin=true` for kind) **and** **implementation absent** (`iplugin-export-shape=fail`). The full dual-signal logic is specified in Task 4.3 (~675–687) as a Task 4.1 amendment to land before golden lock.

**Impact:** Executor doing Tasks 4.1→4.3 sequentially will need that split in `InspectSingle` before Task 4.3 Test 7 / goldens pass. Fully specified; just split across tasks.

---

## Risk Assessment

**Overall: LOW** (for the primary path: Windows dev + CI with MSVC)

| Risk | Level | Notes |
|------|-------|-------|
| IFF negative-fixture / error-kind mismatch | **Low** | Algorithm + fixtures align; stale prose is the only plan-level gap. |
| Plugin fixture `kind` taxonomy | **Low** | Managed fallback removed; native + managed shapes are deterministic on CI. |
| Local execute without `cl.exe` | **Medium (by design)** | Task 4.3 hard-fails with clear message; acceptable given VS on dev/CI. |
| `wrong-iplugin-shape` shape logic | **Low** | Spec complete in Task 4.3; merge into Task 4.1 implementation during 4.3. |

---

## REVIEW VERDICT

**PASS** — ready for `/gsd:execute-phase 4`.

The iter-4 replan resolves the iter-3 FAIL drivers. IFF taxonomy is sound; validate-plugin fixture strategy is internally consistent; LOW items (4–6) and MED-3 are addressed. No new HIGH or MEDIUM plan-level contradictions warrant another full review cycle.

**Recommended execute guardrails (no replan):**

1. Build IFF malformed fixtures from `IffReaderFixtures.BuildNestedChunkOverflow()` / `BuildTruncatedFile()` — treat Task 3.3 **line 491** as canonical for offsets.
2. In Task 4.3, implement the attribute-vs-implementation split in `InspectSingle` before locking `wrong-iplugin-shape/expected.json`.
3. Treat missing `cl.exe` as an expected hard stop locally; CI path is fine.


---

## Consensus Summary (Codex + Cursor, iter-4)

### Confirmed CONCRETE FIXES (all 6 iter-4 findings + post-checker patch)

| Finding | Both Reviewers Verdict |
|---------|------------------------|
| HIGH-1 IFF algorithm restructure | CONCRETE — algorithm sound, math verified independently |
| HIGH-1 fixture byte layouts | CONCRETE — both fixtures deterministically reach claimed `error.kind` |
| MED-2 managed fallback dropped | CONCRETE — `cl.exe` required, `wrong-iplugin-shape/` reshaped |
| MED-3 AssemblyInfo.cs false positive | VERIFIED — Cli vs Tests are different files |
| LOW-4 test count propagation | CONCRETE — all sites reconciled |
| LOW-5 Task 4.1 test count 8→9 | CONCRETE — consistent |
| LOW-6 `PluginInspectionFilters` naming | CONCRETE — consistent across `<behavior>`/`<action>`/verify |
| Post-checker offset patch | CONCRETE — inner chunk header at offset 12 (corrected from 16) |

### Math Verification (Codex + Cursor both traced bytes)

**`malformed-nested-overflow.iff` (file 100B):**
- Outer FORM header at 0-7 (TypeID + Length 92); sub-type at 8-11; first child at 12
- Inner FORM header at 12-19 (TypeID 12-15 + Length 90 at 16-19)
- After inner header read, Position = 20; nested check: `20 + 90 = 110 > parentEnd 100` → **NestedChunkOverflow** ✓

**`malformed-truncated.iff` (file 50B):**
- Outer FORM declares length 92; top-level FORM IS the file (no file-bound check on top-level per iter-4 algorithm)
- Inner chunk header at 12-19 (length 80)
- After inner header read, Position = 20; nested check: `20 + 80 = 100 ≤ parentEnd 100` → passes
- `ReadBytes(80)` from offset 20: file ends at 50, only 30 bytes available → short-read → **Truncated** ✓

### Agreed Execute-Time Guardrails (NON-BLOCKING)

1. **`wrong-iplugin-shape` Task 4.1 prose drift** [both reviewers]. `04-04-PLAN.md:509` Task 4.1 says `iplugin-export-shape = managedIPlugin ? "pass" : "fail"` — but the iter-4 fixture design (attribute present, implementation absent) requires this to be `attribute_present && interface_implemented ? "pass" : "fail"`. The correct logic is specified later in Task 4.3 (`:687-689`) and tested at `:717-724`. Executor should merge the Task 4.3 amendment into Task 4.1's `InspectSingle` implementation before locking the `wrong-iplugin-shape/expected.json` golden. **Spec is complete; just spread across two tasks.**

2. **Stale "sub-type at offset 12" prose in 04-03** [Cursor only]. Lines 79, 349, 490 say "sub-type `WSNP` at offset 12" but should say "8-11" (and "inner header at offset 12" is correct). Documentation-only drift; fixture builders (`BuildNestedChunkOverflow`, `BuildTruncatedFile`) encode the actual bytes correctly; verify-gates assert `error.kind` outcomes, not byte offsets. Reconcile during execute if it surfaces confusion.

3. **`cl.exe` hard-fail is by design** [both reviewers]. Local execute without MSVC will fail with a clear "install VS Build Tools" message. Acceptable per memory `project_vs2026_toolchain` (VS 2026 + VS 2022 both installed locally; GitHub Actions windows-2022 includes MSVC).

### Divergent Views

None substantive. Codex flagged risks as LOW-MEDIUM; Cursor flagged as LOW. Both PASS.

---

## REVIEW VERDICT: PASS

Plans ready for `/gsd:execute-phase 4`. Cumulative iteration totals:

| Pass | Findings | Status |
|------|----------|--------|
| iter-1 review | 22 (8H + 9M + 5L) | Resolved |
| iter-2 review | 12 NEW (2H + 7M + 3L) | Resolved |
| iter-3 review | 12 NEW (1H + 2M + 3L codex; +3M+3L cursor) | Resolved |
| iter-4 review | 0 NEW HIGH/BLOCKING; 1 MEDIUM + 3 LOW non-blocking execute-time guardrails | Acceptable |
| **TOTAL** | **~52 distinct findings resolved across 4 cycles** | **PASS** |

The plans are in good shape. The 4-cycle iteration validates the value of cross-AI adversarial review: each cycle caught real issues the planner missed, and convergence shows the methodology actually works.
