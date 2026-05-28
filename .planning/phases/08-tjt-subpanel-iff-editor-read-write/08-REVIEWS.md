---
phase: 8
review_round: 2
reviewers: [codex, cursor]
reviewed_at: 2026-05-28T18:00:00Z
plans_reviewed: [08-01-PLAN.md, 08-02-PLAN.md, 08-03-PLAN.md, 08-04-PLAN.md, 08-05-PLAN.md, 08-06-PLAN.md, 08-07-PLAN.md]
overall_risk_codex: HIGH
overall_risk_cursor: MEDIUM-HIGH
prior_round_commit: 52468b3
prior_round_overall_risk_codex: HIGH
prior_round_overall_risk_cursor: HIGH
self_reviewer_skipped: claude (CLAUDE_CODE_ENTRYPOINT=cli)
---

# Cross-AI Plan Review — Phase 8 (round 2, post-replan)

Both reviewers (Codex / `gpt-5.5` and Cursor agent) re-reviewed all 7 PLAN.md files plus the uplifted `08-CONTEXT.md`, `08-RESEARCH.md`, `08-PATTERNS.md`, `08-UI-SPEC.md`, **and the round-1 `08-REVIEWS.md`** (preserved at git commit `52468b3`) — so the reviewers could verify whether the prior round's HIGH dispositions actually held up in the revised plans.

**Verdict on the four prior agreed-HIGH design holes from round 1: all four RESOLVED at the plan level.** Neither reviewer re-flagged any of them as unresolved (cursor flagged HIGH-2 and HIGH-4 as PARTIALLY-RESOLVED with non-blocking deltas).

**New round-2 verdict:** the replan trade is "four underspecified design holes" → "execute with known residual execution risk." The remaining concerns are mostly **build-system mismatches, missing automated tests, and verification gaps** — not new design holes.

---

## Codex Review (round 2)

### Summary

The replan substantially addresses the four prior agreed-HIGH design holes: `.tre` repack now copies raw compressed slices for untouched entries, provenance is explicit via `OpenSource`, live patch is same-length-only, and reload semantics are honestly tiered. I would not re-flag those original HIGHs as unresolved. **However, the revision introduced a new execution blocker:** several plans add new `.cs` files but do not consistently update old-style explicit-compile `.csproj` files, so the new code may not build or ship.

### HIGH-Item Verification Matrix

| Prior HIGH item | Disposition | Evidence |
|---|---|---|
| TRE byte-identity contradiction | **RESOLVED** | 08-07 replaces the full-archive byte-identity claim with logical payload identity + raw compressed slice preservation, and requires `TreFile.GetRecordCompressedBytes` plus untouched-entry TOC invariant tests. See `08-07-PLAN.md:19-27, 204-221, 226-275`. |
| Missing live-patch / repack provenance | **RESOLVED, with reduced-mode live patch** | `OpenSource` has `LooseFile`, `TreArchive`, `ClientMemory`, and `Unknown`; TRE hand-off resolves `recordIndex`; live patch is gated on `ClientMemory`; `Unknown` excludes in-place/repack gates. See `08-01-PLAN.md:21-32, 245-252; 08-05-PLAN.md:332-399; 08-07-PLAN.md:319-344`. |
| Shrinking live-patch stale-tail corruption | **RESOLVED** | 08-06 requires validation before write and refuses `rewritten.Length != target.OriginalMappedLength`, covering both growth and shrink before `Memory.memory.Copy`. See `08-06-PLAN.md:15-24, 142-153, 167-171`. |
| D-06 scene-change fallback unspecified | **RESOLVED** | 08-05 now refuses speculative `AddSetSceneCallback` reloads; textures call `Graphics.ReloadTextures`, terrain calls `GroundScene.ReloadTerrain`, all other IFFs return `PendingNextSceneChange`. Bindings verified in code: `GraphicsImpl.ReloadTextures` uses `GameCallbacks.AddMainLoopCall` and `Graphics.ReloadTextures`; `GroundSceneImpl.Reload` uses `GroundScene.Get().ReloadTerrain`. See `08-05-PLAN.md:274-317`; `Generated/UtinniCore.cs:12060, 14428`. |

### NEW Concerns

- **HIGH — New source files are not consistently added to old-style explicit-compile project files.**
  `UtinniCoreDotNet.csproj` is not SDK-style and explicitly lists files (e.g. `Formats\Iff\IffReader.cs` and `Formats\Tre\TreFile.cs` at `UtinniCoreDotNet.csproj:68-69`). `TheJawaToolboxDotNet.csproj` is also explicit, listing current forms/controls at lines `65-131`. But 08-01 adds `IffWriter.cs`, `MutableIff*.cs`, `OpenSource.cs` without listing `UtinniCoreDotNet.csproj`; 08-03/04/06/07 add many TJT files without listing `TheJawaToolboxDotNet.csproj`; 08-07 adds `TreWriter.cs` without listing `UtinniCoreDotNet.csproj`. This will compile-test only files that are explicitly included, so the phase can silently omit implementation or fail at compile time. **This should block execution until every plan adding production `.cs` files includes the owning `.csproj`.**

- **MEDIUM — 08-05 incorrectly describes `UtinniCoreDotNet.csproj` as having default compile glob behavior.** 08-05 says to "confirm the default Compile glob covers `Saving/*.cs`," but the actual project is old-style explicit compile. The fallback says add an explicit `<Compile>` if excluded, but the premise is wrong and likely to be missed. Fold this into the HIGH csproj fix.

- **MEDIUM — 08-06 says the disabled live-patch path is "unit-testable," but no automated test is planned.** The uplifted CONTEXT says D-05.3 reduced-mode acceptance is "implementation-complete and unit-tested for its bounds gate." 08-06 only has grep verification plus human/debug smoke. Add a pure validator or extracted bounds method with xUnit coverage for no-client / zero-target / wrong-length / same-length-allowed before execution.

- **MEDIUM — `OpenSource.Unknown` save-mode wording is internally inconsistent.** 08-05 first says when `Source is OpenSource.Unknown`, "ALL FOUR save items are disabled," then says `Save As…` may remain enabled (`08-05-PLAN.md:339-343`). That does not undermine the required in-place/repack disablement, but the implementer needs one unambiguous rule: disable loose / in-place / repack / live-patch on `Unknown`; explicitly decide whether user-chosen Save-As remains enabled.

- **MEDIUM — `TreWriter` must explicitly preserve or reconstruct the name block layout to make `NameOffset` preservation true.** 08-07 requires `NameOffset` and `NameString` preservation (`08-07-PLAN.md:214-221, 226-228`), but the writer instructions mostly discuss payload blobs and TOC fields. If the name block is rebuilt in a different order/layout, exact `NameOffset` preservation fails. Add an explicit requirement to copy the original name block or rebuild it byte-identically for untouched entries.

### Remaining Risk

Block `/gsd:execute-phase 8` until the csproj issue is corrected across all plans. This is not a coding nuance; it is a build-system mismatch with the existing projects. I would also amend 08-06 to add automated bounds tests, because the current reduced-mode live patch acceptance depends on that gate.

### Overall Risk Assessment: HIGH

The original HIGH design holes are mostly resolved at the planning level, and the revised safety posture is much better. The overall phase risk remains **HIGH** because execution can currently produce non-building or non-included code due to missing explicit `.csproj` updates across both repos, and because live patch still has only reduced-mode functionality. Once the csproj coverage and 08-06 bounds tests are fixed, I would lower this to **MEDIUM-HIGH** due to the inherent `.tre` repack and live-client smoke risk.

---

## Cursor Review (round 2)

### Summary

The replan **materially addresses all four agreed-HIGH design holes** from round-1 `08-REVIEWS.md`: TRE repack acceptance is rewritten around `GetRecordCompressedBytes` + raw-slice copy (08-07), provenance is centralized in `OpenSource` with a `TreArchive` hand-off and `Unknown` sentinel (08-01/08-05), live patch shrink corruption is closed via **same-length-only** before any `Memory.memory.Copy` (08-06), and D-06 is honestly **tiered** with no fabricated scene triggers (08-05 + uplifted `08-CONTEXT.md`). CONTEXT uplift on 2026-05-28 aligns with the plans on D-05.3 (reduced-mode, menu disabled) and D-06 (tiered matrix).

The revision also **folds in several prior MEDIUM items** (IffEditController framework-side, `LooseOverridePath` framework-side, mutation golden, Ctrl+Z collision, flush-before-reload, timestamped backups, `TreHandoffFallbackTests`).

**New/regression concerns remain**, mostly execution and verification gaps rather than missing design: edited-entry **Checksum preservation** is still an unproven live-client assumption; `GroundScene.ReloadTerrain()` needs an instance call site the plan does not name; **08-06 lacks the unit tests CONTEXT claims** for the bounds gate; CI still **does not build UtinniPlugins**; and phase completion semantics for D-05.3 (disabled menu vs "four save modes must-have") need explicit sign-off language.

**Overall:** safe to **proceed with execute-phase** on the de-risked 08-01→08-05 spine; treat 08-06/08-07 as **maintainer-gated** until Tier-4 smoke passes.

### HIGH-Item Verification Matrix

| Prior HIGH (08-REVIEWS round 1) | Disposition | Evidence |
|-------------------------|-------------|----------|
| **HIGH-1 — TRE repack "byte-identical untouched entries" contradicts recompress-all** | **RESOLVED** (plan + test contract) | `08-07-PLAN.md` Task 1 adds `TreFile.GetRecordCompressedBytes(int)`; Task 2 `TreWriter` branches untouched → verbatim raw slice, edited-only recompress; drops full-file "archive bytes identical"; two-guarantee + per-record TOC invariant tests. |
| **HIGH-2 — Live patch / repack lack `targetAddr` / `recordIndex` provenance** | **PARTIALLY-RESOLVED** | **Resolved for file/repack:** `08-01-PLAN.md` Task 3 (`OpenSource` four cases); `08-04` `FormIffEditor.Source`; `08-05` Task 4 hand-off → `OpenSource.TreArchive(trePath, recordIndex, logicalPath)`; Task 4b `TreRecordIndexResolver.ResolveOrUnknown` + unit test; `08-07` Task 3 gates repack on `Source is OpenSource.TreArchive`. **Deferred by design:** `ClientMemory` never constructed in 08-04/08-05; `08-06` menu disabled until follow-up. |
| **HIGH-3 — Shrinking live patch leaves stale tail bytes** | **RESOLVED** | `08-06-PLAN.md` Task 2: refuse `rewritten.Length != target.OriginalMappedLength` **before** `AddMainLoopCall` / `Memory.memory.Copy`; matches uplifted `08-CONTEXT.md` D-05.3 same-length-only. |
| **HIGH-4 — D-06 scene-change fallback unspecified / over-promised** | **PARTIALLY-RESOLVED** | **Resolved at product level:** uplifted `08-CONTEXT.md` D-06 tier matrix; `08-05` Task 3 `ClientReloadDispatcher` returns `ReloadedTextures` / `ReloadedTerrain` / `PendingNextSceneChange` / `Unavailable`; explicitly refuses `AddSetSceneCallback` as a trigger. **Gap:** texture hook is concrete (`Graphics.ReloadTextures()` exists in `Generated/UtinniCore.cs` ~12060); terrain hook is **instance** (`GroundScene.ReloadTerrain()` ~14428) but plan text reads like a static call — implementer must use `GroundScene.Get().ReloadTerrain()`. |

### NEW Concerns

#### HIGH

| ID | Concern | Where |
|----|---------|-------|
| **N-H1** | **Edited-entry `Checksum` preservation may break client resolution.** 08-07 Task 2 preserves path CRC for the edited record because path is unchanged (Open Q1/A1). If the client validates CRC against payload (not path-only), live repack smoke can fail silently or at runtime. Prior A1 remains unproven until Tier-4. | `08-07-PLAN.md` Task 2 |
| **N-H2** | **`TreWriter` repack is still the highest-blast-radius path** with many moving parts (header layout, name block, TOC stride v24/v32, compressor flags, new payload offsets). Plan tests are good but synthetic; live archive + locked-handle fallback is the real gate. One wrong invariant → broken `.tre`. | `08-07` Tasks 2–4 |
| **N-H3** | **UtinniPlugins is not in CI** (`ci.yml` builds `Utinni.sln` only). Framework-side fixes green CI; **all WinForms/TJT wiring is unguarded in CI** until maintainer MSBuild + Tier-4. | `.github/workflows/ci.yml`; cross-plan |

#### MEDIUM

| ID | Concern | Where |
|----|---------|-------|
| **N-M1** | **08-06 "unit-tested bounds gate" vs plan:** CONTEXT D-05.3 says "implementation-complete and unit-tested for its bounds gate," but `08-06-PLAN.md` has no xUnit task for `LivePatchSaveTarget` — only grep gates + Tier-4. Extract validation to a pure function and test refusal paths without a live client. | `08-06` Task 2; `08-CONTEXT.md` D-05.3 |
| **N-M2** | **`GroundScene.ReloadTerrain()` call site underspecified.** Native binding is instance `ThisCall`; TJT uses `GroundScene.Get()` elsewhere. Plan should name that pattern or reload-terrain tier may ship as no-op / compile error. | `08-05` Task 3 |
| **N-M3** | **Asset-class routing is "planner's discretion" (extension sniff).** Texture vs datatable vs template misclassification → wrong reload tier (false "Reloaded (textures)" or spurious `PendingNextSceneChange`). | `08-05` Task 3 `<action>` |
| **N-M4** | **08-02 mutation golden covers one-leaf edit only**, not structural ops (add / remove / reorder). Criterion 4 for structural paths still relies on `08-01` unit tests + editor manual path. | `08-02` Task 2 |
| **N-M5** | **Open Q2 (loose-override subdirectory) still Tier-4-only.** `LooseOverridePath` fixes traversal; wrong subdir under client root = "saved but never loaded" remains until Task 5 smoke. | `08-05` Task 1/5 |
| **N-M6** | **Cross-repo coupling:** 08-03–08-07 modify `UtinniPlugins` while 08-01/02/07 modify `Utinni`; no plan step pins **same-commit / rebuild TJT against new DLL** beyond narrative. Drift risk if only one repo is built. | cross-plan |
| **N-M7** | **D-05.3 completion semantics:** CONTEXT still lists all four save modes as "hard V1 must-haves," but D-05.3 acceptance is **reduced-mode + disabled menu**. Honest for engineering, but PROD-W1-IFF literal acceptance is file-save + reload (REQUIREMENTS.md); phase sign-off should explicitly record live patch as **infra-ready, user-disabled** to avoid false "100% D-05" claims. | `08-CONTEXT.md` D-05; `08-06` objective |
| **N-M8** | **Record-index resolution by linear `Offset == ArchiveLocalOffset` scan** (`TreRecordIndexResolver`) — correct for normal archives, but plan does not handle **duplicate offsets** or document O(n) per open on huge `.tre` files. Low probability, easy to miss in smoke. | `08-05` Task 4/4b |
| **N-M9** | **08-07 links `TreFixtureBuilder` from `Utinni.Cli.Tests`** into `UtinniCoreDotNet.Tests` — valid CI pattern (matches existing linked-source block), but adds **test-assembly coupling**; breakage if `TreFixtureBuilder` moves. | `08-07` Task 2 |

#### LOW

| ID | Concern | Where |
|----|---------|-------|
| **N-L1** | `08-01`/`08-05` `assumes:` still say `CONTEXT-UPLIFT-NEEDED` though CONTEXT was uplifted 2026-05-28 — stale metadata only. | plan frontmatter |
| **N-L2** | `08-03` signature-pin via grep + compile, not automated API snapshot test. | `08-03` Task 2 |
| **N-L3** | ROADMAP title "subpanel" vs `FormIffEditor` via `GetForms()` — doc drift. | ROADMAP vs UI-SPEC |
| **N-L4** | `08-RESEARCH.md` user_constraints excerpt in the review packet still shows **pre-tier D-06** wording; canonical source is uplifted `08-CONTEXT.md`. | research staleness |

### Focus-Area Checks

**OpenSource consistency (08-01 / 04 / 05 / 06 / 07):** mostly consistent. Single union in `UtinniCoreDotNet/Formats/Iff/OpenSource.cs` with four cases. W-3 check: on `Unknown`, **both SaveInPlace and Repack are disabled** (neither `is LooseFile` nor `is TreArchive`). Plan also disables loose override; **Save As… may stay enabled** as escape hatch — consistent with checker intent.

**08-07 raw-slice strategy vs recompress-all:** strategy is replaced, not cosmetic. Task 2 explicitly branches: untouched → `GetRecordCompressedBytes` verbatim + preserve TOC fields; edited → recompress only that index. Directly fixes the prior HIGH-1 contradiction.

**08-06 same-length-only before VirtualProtect bracket:** yes. Task 2 validates `Game.IsRunning`, zero target, and `rewritten.Length != OriginalMappedLength` **before** queuing `AddMainLoopCall` → `Memory.memory.Copy`. Growth and shrink both refused.

**08-05 tiered reload + named call sites:** tiered UI/outcomes accurate per uplifted D-06. Texture/shaders use static `Graphics.ReloadTextures()`. Terrain uses **instance** `GroundScene.Get().ReloadTerrain()` (TJT precedent — plan should be amended to say this explicitly). Datatable/STF/template → `PendingNextSceneChange` + candid copy.

**LooseOverridePath → UtinniCoreDotNet/Saving/ fixes CI break:** yes for the specific prior CI break. `ci.yml` checks out Utinni only (line 35); runs `UtinniCoreDotNet.Tests` + `Utinni.Cli.Tests`. Framework-side relocation eliminates the prior 08-04 MEDIUM-5 break. **Does not** CI-gate TJT/plugin UI.

**D-05.3 disabled vs phase completion:** honest if sign-off uses the uplifted contract (D-05.3 = code path + bounds gate + disabled menu + reduced Tier-4). **Not** end-user demo of live patch. Align phase completion checklist with `08-CONTEXT.md` D-05.3 reduced-mode wording.

**08-05 → 08-07 `recordIndex` contract:** aligned. 08-05 Task 4 resolves index via `TreRecordIndexResolver`; 08-07 `TreRepackSaveTarget.Apply(OpenSource.TreArchive target, …)` uses `target.RecordIndex`. Task 4b unit test closes the degraded path.

### Remaining Risk — What Should Still Block Execute?

| Gate | Recommendation |
|------|------------------|
| **`/gsd:execute-phase 8` (start implementation)** | **Do not block.** Prior HIGH design holes are plan-level closed. |
| **Wave 4+ merge without maintainer** | **08-05 Task 5** is correctly blocking (`autonomous: false`). |
| **08-06 / 08-07 phase sign-off** | **Block** until Tier-4 smoke passes (repack resolution, locked-archive fallback, tiered reload matrix, loose-override dir). |
| **Optional pre-execute hardening** (not blocking, but cheap) | (1) Add `LivePatchSaveTarget` pure unit tests in 08-06; (2) name `GroundScene.Get().ReloadTerrain()` in 08-05 Task 3; (3) strip stale `CONTEXT-UPLIFT-NEEDED` from plan assumes. |

### Overall Risk Assessment: MEDIUM-HIGH

The replan successfully downgrades the phase from "four underspecified design holes" to "execute with known residual execution risk." The **08-01 / 08-02 / 08-04 / 08-05.1–2 spine** (writer, CLI gate, editor, file saves, tiered reload UX) is **LOW–MEDIUM** risk and CI-heavy. **08-07 repack** and **08-06 live patch** remain **HIGH blast-radius** at runtime, but are now **isolated, provenance-gated, honestly scoped**, and backed by stronger automated tests than round 1.

What keeps overall risk above MEDIUM: **edited-entry CRC assumption (N-H1)**, **no CI for TJT (N-H3)**, **maintainer-only Tier-4 gates on criteria 1–2**, and **terrain reload call-site ambiguity (N-M2)**. None of these re-open the original four HIGH *planning* holes; they are **implementation and verification** risks appropriate for a phased execute with blocking human checkpoints on 08-05 / 06 / 07.

---

## Consensus Summary — Round 2

### Agreed Strengths (both reviewers)

- **All four prior agreed-HIGH design holes are resolved at the plan level.** Neither reviewer re-flagged HIGH-1 (TRE byte-identity), HIGH-2 (provenance), HIGH-3 (shrink-safety), or HIGH-4 (D-06 reload) as unresolved. Cursor flagged HIGH-2 and HIGH-4 as PARTIALLY-RESOLVED with non-blocking deltas (ClientMemory deferred by design; terrain call-site needs `GroundScene.Get().ReloadTerrain()`).
- **`OpenSource` discriminated union is consistently wired** across 08-01 / 04 / 05 / 06 / 07 — single union owner, four cases, pattern-match gates on each consumer.
- **08-07's `GetRecordCompressedBytes` + raw-slice strategy actually REPLACES the recompress-all approach** (not just adds a new API). Codex and cursor both verified the branch in Task 2.
- **08-06's SAME-LENGTH-ONLY gate runs BEFORE the VirtualProtect bracket** — growth and shrink both refused; matches the uplifted D-05.3 wording.
- **08-05 tiered reload disposition is honest and matches the uplifted D-06** — no speculative `AddSetSceneCallback` triggers.
- **LooseOverridePath relocation to UtinniCoreDotNet/Saving/ closes the prior MED-5 CI break.**
- **OpenSource.Unknown sentinel correctly disables both SaveInPlace AND SaveRepack** on the degraded TRE hand-off (no false-enable bug from round-1 checker's W-3).

### Agreed Concerns (raised by both — highest priority)

| Severity | Concern | Both reviewer IDs | Plans |
|----------|---------|---|-------|
| **HIGH (codex) / HIGH (cursor)** | **CI does not build UtinniPlugins.** `ci.yml` checks out Utinni only (line 35), runs `UtinniCoreDotNet.Tests` + `Utinni.Cli.Tests`. Framework-side fixes pass CI; all WinForms/TJT wiring (08-03/04/05/06/07) is unguarded in CI until maintainer MSBuild + Tier-4 smoke. | codex (implicit in HIGH csproj); cursor N-H3 | cross-plan |
| **MEDIUM (both)** | **08-06 bounds gate has no automated test, but CONTEXT D-05.3 claims it is "unit-tested for its bounds gate."** Plan has grep gates + Tier-4 only — needs `LivePatchSaveTarget` pure unit tests (refusal paths: no-client, zero-target, wrong-length, same-length-allowed). | codex MEDIUM N-3; cursor N-M1 | `08-06` Task 2; `08-CONTEXT.md` D-05.3 |
| **MEDIUM (both, different framing)** | **Cross-repo build coupling not pinned at plan level.** No plan step explicitly pins same-commit Utinni + UtinniPlugins build; drift risk on uncommitted `Generated/UtinniCore.cs` for `Memory.memory.Copy` line citations. | codex (implicit in HIGH csproj — explicit-compile makes drift more dangerous); cursor N-M6 | cross-plan |

### Divergent / Unique Findings (single-reviewer, still worth investigating)

| Reviewer | Concern | Severity | Plan |
|----------|---------|----------|------|
| **Codex (UNIQUE — execution-blocking)** | **csproj explicit-compile coverage.** `UtinniCoreDotNet.csproj` (lines 68-69 list `Formats\Iff\IffReader.cs`, `Formats\Tre\TreFile.cs`) and `TheJawaToolboxDotNet.csproj` (lines 65-131) are old-style explicit-compile, not SDK-style default-glob. 08-01 adds `IffWriter.cs`/`MutableIff*.cs`/`OpenSource.cs` without csproj edits; 08-03/04/06/07 add many TJT files without `TheJawaToolboxDotNet.csproj` edits; 08-07 adds `TreWriter.cs` without csproj edit. **Fix:** every plan adding production `.cs` files must list the owning `.csproj` in `files_modified` and add the `<Compile Include="...">` entries explicitly. | HIGH | 08-01, 03, 04, 06, 07 |
| **Codex (UNIQUE)** | **08-05 incorrectly describes default Compile glob behavior** — the actual csproj is explicit-compile. Fold into the HIGH csproj fix. | MEDIUM | 08-05 |
| **Codex (UNIQUE)** | **`OpenSource.Unknown` Save-As wording inconsistency:** 08-05 says "ALL FOUR save items disabled" then says "Save As… may remain enabled" — disambiguate. | MEDIUM | 08-05 (around `08-05-PLAN.md:339-343`) |
| **Codex (UNIQUE)** | **TreWriter name block layout preservation underspecified.** 08-07 requires `NameOffset`/`NameString` preservation but writer instructions focus on payload blobs and TOC fields. If name block is rebuilt in different order, NameOffset preservation fails. Add explicit requirement: copy original name block OR rebuild byte-identically for untouched entries. | MEDIUM | 08-07 |
| **Cursor (UNIQUE)** | **Edited-entry `Checksum` preservation may break client resolution.** 08-07 preserves path CRC because path is unchanged, but if client validates CRC against payload (not path-only), live repack can fail. Unproven until Tier-4. | HIGH | 08-07 |
| **Cursor (UNIQUE)** | **TreWriter repack is highest-blast-radius regardless of plan quality** — header layout, name block, TOC stride v24/v32, compressor flags, new payload offsets all interact. Live archive + locked-handle fallback is the real gate. | HIGH | 08-07 |
| **Cursor (UNIQUE)** | **`GroundScene.ReloadTerrain()` is instance, not static.** Plan text reads like static call; implementer must use `GroundScene.Get().ReloadTerrain()` (TJT precedent in `FormObjectBrowser.cs`). Risk: tier ships as no-op / compile error. | MEDIUM | 08-05 Task 3 |
| **Cursor (UNIQUE)** | **Asset-class routing is "planner's discretion" (extension sniff).** Misclassification → wrong reload tier. | MEDIUM | 08-05 Task 3 |
| **Cursor (UNIQUE)** | **08-02 mutation golden is leaf-edit-only**, not structural ops. Criterion 4 for structural paths still relies on 08-01 unit tests + editor manual path. | MEDIUM | 08-02 |
| **Cursor (UNIQUE)** | **Open Q2 (loose-override subdirectory)** still Tier-4-only — wrong subdir = "saved but never loaded." | MEDIUM | 08-05 |
| **Cursor (UNIQUE)** | **D-05.3 completion semantics drift:** CONTEXT lists all four save modes as "hard V1 must-haves," but D-05.3 acceptance is reduced-mode. Phase sign-off should explicitly record live patch as **infra-ready, user-disabled** to avoid false "100% D-05" claims. | MEDIUM | `08-CONTEXT.md` D-05; `08-06` objective |
| **Cursor (UNIQUE)** | **Linear `Offset == ArchiveLocalOffset` scan in `TreRecordIndexResolver`** — doesn't handle duplicate offsets; O(n) per open on huge `.tre`. Low probability, easy to miss in smoke. | MEDIUM | 08-05 Task 4 |
| **Cursor (UNIQUE)** | **08-07 links `TreFixtureBuilder` from `Utinni.Cli.Tests` into `UtinniCoreDotNet.Tests`** — valid CI pattern but adds test-assembly coupling. | MEDIUM | 08-07 |
| **Cursor (UNIQUE)** | **Stale `CONTEXT-UPLIFT-NEEDED` tags in 08-01/05 `assumes` blocks** — CONTEXT was uplifted 2026-05-28; tags are now stale metadata. | LOW | 08-01, 08-05 frontmatter |
| **Cursor (UNIQUE)** | **08-03 signature-pin via grep + compile**, not automated API snapshot test. | LOW | 08-03 |
| **Cursor (UNIQUE)** | **ROADMAP title "subpanel"** vs `FormIffEditor` via `GetForms()` — doc drift. | LOW | ROADMAP vs UI-SPEC |

### Recommended Disposition

The two reviewers **disagree on overall threshold** (codex HIGH = block; cursor MEDIUM-HIGH = proceed with maintainer-gated 08-06/07 smoke). The disagreement boils down to **how to weigh the csproj explicit-compile coverage finding**:

- **Codex argues:** explicit-compile means new `.cs` files **must** be listed in csproj or they will not compile. The phase can silently produce non-building code if every plan does not update the owning `.csproj`. This is execution-blocking.
- **Cursor implicitly accepts** that csproj edits will be added during execute-phase Task 1 of each plan (the "modify csproj as needed" task), so doesn't flag it as separately blocking.

**Both views are defensible.** If you trust execute-phase to catch the csproj omission on the first compile-fail of each wave, cursor's MEDIUM-HIGH is the right read. If you want zero compile-fail surprises during execute-phase, codex's HIGH is the right read.

The **agreed MEDIUM items (08-06 unit tests, terrain instance call, OpenSource.Unknown Save-As wording, name-block preservation, mutation golden scope, asset-class routing, stale CONTEXT-UPLIFT-NEEDED tags)** are all cheap fixes that materially raise confidence. Whether to address them in another `/gsd:plan-phase 8 --reviews` cycle or fold into execute-phase first-task corrections is a workflow preference.

Recommended next step (your choice):

```text
# Option A — fold round-2 concerns at plan level first (codex's HIGH-block view)
/gsd:plan-phase 8 --reviews

# Option B — proceed to execute (cursor's MEDIUM-HIGH proceed-with-caution view)
/gsd:execute-phase 8
```

Either path is defensible. If you pick **Option A**, the planner will read this REVIEWS.md and produce surgical edits targeting the agreed-MEDIUM items first plus codex's HIGH csproj. If you pick **Option B**, you accept compile-fail-as-feedback on the first wave and trust the maintainer-gated checkpoints on 08-05/06/07 Tier-4 smoke to catch the rest.

---

*Generated 2026-05-28 by /gsd:review --phase 8 --all (round 2). Reviewers: codex (`gpt-5.5` via `codex exec --skip-git-repo-check`), cursor (default model via `cursor-agent.cmd -p --mode ask --trust`). Self-reviewer claude skipped per CLAUDE_CODE_ENTRYPOINT=cli. Round-1 review at git commit `52468b3`.*
