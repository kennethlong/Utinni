---
phase: 22
reviewers: [codex, cursor]
reviewed_at: 2026-06-17
plans_reviewed: [22-01-PLAN.md, 22-02-PLAN.md, 22-03-PLAN.md, 22-04-PLAN.md]
---

# Cross-AI Plan Review — Phase 22 (ClientEffect Editor)

> Reviewers: **Codex** (GPT, ChatGPT-authed) and **Cursor** (`cursor-agent`). Claude was skipped
> (this review was driven from inside Claude Code — independence rule). Each got the same prompt:
> PROJECT excerpt, ROADMAP §22, REQUIREMENTS (CFX), 22-CONTEXT.md, and all four PLAN.md files.

## Codex Review

## Summary

Overall, the Phase 22 plans are strong and unusually explicit about the hard parts: variable-length CLEF payload edits, source-version preservation, raw fallback, loose-override containment, and MCP-OOP boundaries are all treated as first-class risks. The plan set likely achieves `PROD-W2-CFX-01` and `PROD-W2-CFX-02` if implemented literally. The main weaknesses are around identity/stable addressing for command leaves, incomplete proof that reordered/added dirty nodes preserve unrelated bytes, ambiguity in raw/truncated handling, and some inconsistent loose-subdir details between `22-02` and `22-03`.

## Strengths

- `22-01 Task 1/2` correctly identifies the key technical risk: variable-length strings force parent FORM length recomputation, so tests must prove dirty-node ancestor invalidation actually works.
- `D-03` is well protected in principle: the plan says CPAP encode emits only fields for the source version and explicitly rejects mirroring SOE's `TAG_0003` save path.
- Raw fallback is consistently called out for both unknown CLEF versions and unknown command tags in `22-01`, `22-02`, and `22-04`.
- `22-02` correctly refuses to reuse terrain's fixed-span verification. Structural verification is the right direction for CLEF.
- MCP design honors `DEC-V2-MCP-OOP`: `summarize_clienteffect` shells `utinni-cli decode-iff` and keeps zero format logic in `Utinni.Mcp`.
- `22-04` is appropriately conservative on preview. Hardcoding `IsRetriggerHookReachable() == false` is better than shipping a fake "preview" button that implies live replay.
- The UI plan respects `DEC-C4`: `EffectsSubPanel` inside TJT, with a roomy form for actual editing.

## Concerns

- **HIGH: Stable leaf identity is underspecified across decode, mutate, save, and CLI.** `22-02 Task 2` uses `--leaf <stableId>`, compares untouched command chunks "per stableId," and supports add/remove/reorder. But `22-01` does not define how `ClientEffectCommand` stable IDs are assigned. If IDs are index-based, reorder/remove invalidates identity and structural verify can compare the wrong chunks. If IDs are path-based, add/remove shifts paths. This is the biggest correctness gap.
- **HIGH: Structural verify may allow malformed semantic rewrites.** `22-02 Task 2` checks: output reparses, untouched chunks identical, edited/added command decodes to requested value. That does not prove the entire edited command payload is valid and canonical for the file version. Example risks: extra trailing bytes after known fields, bool values other than `0/1`, NaN/Infinity floats, invalid color ranges from string parsing, or CPAP v0001 accidentally carrying v0002/v0003 bytes that the decoder ignores. The verify should assert exact payload length and exact field set for known commands.
- **HIGH: Raw fallback vs truncated known command is ambiguous.** `22-01` says unknown command raw-preserves, unknown version raw-preserves, but malformed/truncated known command handling is mixed: threat model says parse exception, UI says raw/hex degrade for malformed/unknown. Decide whether a truncated known `CPAP` becomes a raw read-only command or a parse error. Current language could lead to CLI hard exit while UI expects graceful raw display.
- **MEDIUM: Add-command defaults are not specified enough for byte-exact/version-safe output.** `22-01` and `22-02` say added commands emit at the file's existing version, but do not define default field values per command/version. A v0001 CPAP add must not include `softParticleTerminate`; v0002 must include it; v0003 must include all scale/rate fields. Tests should assert the raw payload bytes and length for each version, not only that it reparses.
- **MEDIUM: Endianness/no-pad coverage is mostly implicit.** The CLGT odd payload size is mentioned, but the tests in `22-01 Task 2` should explicitly assert the raw CLGT leaf length is exactly `23` and that the next sibling starts immediately after those 23 bytes. Otherwise a hidden pad bug could survive if both fixture builder and codec share the same wrong writer behavior.
- **MEDIUM: Fixture builder may hide writer bugs.** `22-01 Task 2` builds fixtures with `IffWriter`, then serializes with `IffWriter`. That proves internal consistency, not necessarily conformance to real SOE bytes. `D-14` real-asset checks are deferred to live smoke/best effort. At least one tiny hand-authored byte array fixture with known big-endian FORM lengths and little-endian payload scalars would reduce this blind spot.
- **MEDIUM: Version preservation is not airtight in `decode-iff` / `apply-save-effect` tests.** The plan asserts v0001 no-upgrade in `22-01`, but `22-02` should also test `apply-save-effect --add-command CPAP` and field edits against v0001/v0002/v0003 fixtures. CLI save is a separate path and should prove it does not normalize.
- **MEDIUM: `22-02` and `22-03` disagree on `apply-save-effect` loose-subdir flag.** `22-03` says `apply-save-effect` uses the same optional `--loose-subdir` default `"loose"`, but `22-02`'s verb decisions do not include that flag. This should be resolved before implementation to avoid terrain/effect CLI inconsistency.
- **MEDIUM: `22-04` says wrap the whole Form constructor in try/catch, but WinForms constructors cannot fully recover from partially built controls.** MEF safety is correctly identified, but catching inside the form ctor can still leave a malformed instance. Prefer a minimal no-throw constructor plus an explicit `BuildContentSafe()` failure state, mirroring the SubPanel approach.
- **LOW: `22-04` scope is large for one plan.** `FormClientEffectEditor` includes command list, typed grid, undo/redo, save menu, direct/TRE open, raw hex view, preview candor, INI splitter persistence, and plugin registration. This is feasible, but it is the most likely plan to produce brittle UI code without automated coverage.
- **LOW: Requirement mapping for `22-03` is misleading.** The terrain folded todo is useful, but tagging it `PROD-W2-CFX-01` is indirect. It should be framed as save-matrix consistency, not ClientEffect editor delivery.

## Suggestions

- In `22-01 Task 1`, define a stable command identity contract. Prefer immutable object identity backed by the original `MutableIffNode` plus a generated session ID, not index/path. For CLI addressing, expose a deterministic `stableId` derived from original preorder plus tag plus occurrence, and document that newly added commands get returned by decode output before further mutation.
- In `22-01 Task 2`, add tests that compare raw byte slices before and after: length-changing CPAP edit in a multi-command file; add command at beginning/middle/end; remove first/middle/last; reorder first/middle/last. Assert sibling chunk headers and payloads are unchanged, not just decoded field values.
- In `22-01 Task 2`, add explicit no-pad/endian assertions: `CLGT` payload length is `23`; RGB bytes are exactly one byte each; floats appear little-endian; FORM/chunk lengths are big-endian and recomputed.
- In `22-01 Task 1`, make known-command decode reject trailing bytes unless deliberately preserved. If preserving trailing bytes is desired, model them explicitly as `UnknownTrailingBytes` and make edits refuse or preserve them consciously.
- In `22-02 Task 2`, strengthen structural verify: known edited command payload length equals the exact encoder length for that command/version; all fields decode exactly; no trailing bytes remain unless explicitly allowed; source CLEF version after reparse equals original version; command count and order match the requested mutation.
- In `22-02`, add `--loose-subdir` to `apply-save-effect` or remove the claim from `22-03`. The effect and terrain CLI contracts should be identical.
- In `22-02 Task 2`, test `apply-save-effect` on v0001, v0002, and v0003 CPAP fixtures, especially `--add-command CPAP`, to prove `D-03` through the CLI path.
- In `22-01`, add one hand-authored fixture as a literal byte array or hex file, independent of `IffWriter`, to guard against fixture-builder/writer shared mistakes.
- In `22-04`, split UI implementation internally even if the plan remains one document: first save/open/editor shell, then command editing, then undo/preview polish. This reduces MEF and layout failure risk.
- In `22-04`, add a non-live smoke test or debug harness where possible for `ClientEffectSaveTargets`: path lands under `<root>/loose`, TRE source is never overwritten, path escape returns a save failure.

## Risk Assessment

**Overall risk: MEDIUM.** The architecture is sound and the plans target the right failure modes, but CLEF editing is deceptively stateful: variable-length payloads plus add/remove/reorder make stable identity and structural verification critical. If those are tightened, the risk drops toward low. Without those changes, the most likely shipping defect is not a crash, but a "successful" edit that silently rewrites or misaddresses the wrong command bytes.

---

## Cursor Review

## 1. Summary

Phase 22 is a well-scoped **reuse-by-composition** phase with strong precedent anchoring (Particle codec, Terrain SubPanel→Form, `apply-save-trn` scaffold, MCP thin-shell). The length-ripple DOM mechanism is real in code (`MutableIffNode.SetPayload` / `IffWriter.WriteContainerFresh`), and the plans correctly treat CLEF as the one new variable-length encoder problem. The main gaps are **integration contracts** (stable IDs, `--loose-subdir`, structural verify for list mutations), **decode degrade discipline** for truncated *known* commands (Particle does per-node try/catch; CLEF plan does not), and **PROD-W2-CFX-02 "both lineages"** proof resting on manual D-14 smoke rather than committed automation. Fix those before execution and the phase should deliver both requirements; without them, Wave 2 verbs and the editor save path are the highest regression risk.

## 2. Strengths

- **Correct architectural layering** — Codec in `UtinniCoreDotNet`, verbs-first CLI, MCP shells `decode-iff` with zero format logic; matches DEC-V2-VERBS-FIRST / DEC-V2-MCP-OOP / DEC-C4.
- **Length-ripple treated as "exercise, don't build"** (22-01 Task 1/2) — Aligns with verified `IffWriter` hybrid-DOM behavior; codec tests specify the right four assertions (re-parse, edited value, untouched chunks, FORM lengths).
- **D-03 version preservation is explicit and correct** — Research correctly flags SOE `ClientEffectTemplateRW` always stamping 0003; plan forbids mirroring that and ties CPAP field visibility to source version.
- **Structural verify delta is identified** (22-02) — Removing `apply-save-trn`'s fixed-length guard (`ApplySaveTrnCommand.cs:178-183`) is the right call for D-01.
- **Raw-fallback fixture matrix** (22-01 Task 2, D-13) — Unknown version + unknown tag + empty list are good edge cases; `clef_v0001_all5.iff` pins stable commands at oldest CPAP version.
- **Honest-candor Preview** (22-04 Task 2) — Hardcoding `IsRetriggerHookReachable() => false` matches Particle reality; avoids DEC-A3 / over-promise failure mode.
- **MEF-safety discipline** — SubPanel/Form ctor try/catch + SplitContainer `Size` before `SplitterDistance` directly addresses Pitfall 8.
- **Folded terrain `loose/` fix** (22-03) — Correctly targets the real residual (`ApplySaveTrnCommand.cs:102` single-step resolve); editor side already documented in `TerrainLooseOverridePathTests`.
- **Parallel Wave 1** — 22-03 independent of codec is sensible; doesn't block CLEF foundation.

## 3. Concerns

| Tag | Concern |
|-----|---------|
| **HIGH** | **`apply-save-effect` structural verify copied from terrain is wrong for add/remove/reorder** (22-02 Task 2). `UntouchedLeavesByteIdentical` keys on ordinal-based `IffLeafChunk.Id` (`IffReader.cs:293`). Reorder/add-at-front **changes stable IDs for unchanged payloads**. Verify will false-fail or, worse, if implemented naïvely, false-pass. Field-edit-only comparison-by-stableId is fine; list mutations need **position/multiset** logic. |
| **HIGH** | **`BuildClefResult` JSON omits `stableId`** (22-02 Task 1 `verb_decisions`). Terrain envelope includes `stableId` per child (`DecodeIffCommand.cs:339`). Plan lists `{index, tag, isRaw, fields}` only, but `apply-save-effect --leaf` requires stable IDs. Editor (`FormClientEffectEditor`) also needs leaf addressing for save — unspecified in 22-04. |
| **HIGH** | **`--loose-subdir` specified for `apply-save-trn` (22-03) but not for `apply-save-effect` (22-02)**. 22-03 `convention_decision` claims both verbs share the flag; 22-02 `verb_decisions` and `ClefLooseOverrideTests` expect `<root>/loose/` without defining how `apply-save-effect` composes it. Executor may mirror `ApplySaveIffCommand.cs:100` (no subdir) and fail acceptance tests. |
| **MEDIUM** | **Truncated *known-tag* commands vs "never hard-fail"** (22-01 Task 1, D-06/D-13). Particle uses per-emitter `try/catch` → raw preserve (`MutableParticleEffect.cs:187-201`). CLEF plan only raw-falls back **unknown tags**; threat model says truncated CPAP → parse exception. Editor criterion says degrade to hex, never crash. Gap between codec strictness and UI contract. |
| **MEDIUM** | **PROD-W2-CFX-02 "both lineages" is mostly synthetic** (D-13/D-14). Eight hand-built fixtures don't prove SWGEmu vs Restoration byte quirks. D-14 real-asset `roundtrip-effect` is **manual-only** (22-04 Task 3, 22-VALIDATION manual table). Success criterion 2 is only half-automated. |
| **MEDIUM** | **`--add-command` defaults unspecified** (22-02 `verb_decisions`). No default appearance string, floats, CLGT RGB, FFBK iterations. Executor invention risks non-deterministic goldens and editor/CLI mismatch. |
| **MEDIUM** | **No in-proc ↔ CLI save parity test** for ClientEffect. Terrain has `TerrainInProcSaveParityTests.cs` pinning editor vs `apply-save-trn`. 22-04 uses `ClientEffectSaveTargets` + direct `Serialize()`; 22-02 uses `apply-save-effect` — two save paths can diverge silently. |
| **MEDIUM** | **Typed payload "consume all bytes" not required** (22-01). If `ClientEffectCommand` decode doesn't assert `IffPayloadCursor.Remaining == 0`, slack/trailing bytes could survive edits while "value matches" on re-parse. Weakens byte-exact gate for structural verify. |
| **LOW** | **Wave label drift** — Roadmap/22-VALIDATION say 22-01 is Wave 0; plan frontmatter says `wave: 1`. Cosmetic but confuses wave gates. |
| **LOW** | **22-03 test name vs 22-VALIDATION** — Validation expects `Name~TerrainLooseSubdir`; 22-03 doesn't require that filter token. |
| **LOW** | **Remaining `apply-save-*` verbs** (`iff`, `stf`, `ot`, `tab`) still single-step resolve without `loose/` — matrix still inconsistent outside folded scope. |

## 4. Suggestions

1. **22-02 Task 2 — Split structural verify by mutation mode**
   - **Field edit:** Keep stableId-based untouched-leaf compare (mirror `ApplySaveTrnCommand.UntouchedLeavesByteIdentical`, excluding edited leaf).
   - **Reorder:** Verify command-count unchanged + **multiset of (tag, payload bytes)** equal, or round-trip undo (already in 22-01) — do **not** use stableId equality.
   - **Add:** Verify count+1, all **original** payloads present, new command decodes with fixture defaults.
   - **Remove:** Verify count−1, removed payload absent, survivors' payloads byte-identical (by content, not ID).
2. **22-02 Task 1 — Extend `BuildClefResult`**: add `stableId` per command (from `MutableIffDocument.DeriveStableId` on each command leaf), matching terrain's navigable envelope. Update `DecodeEffectTests` to assert `stableId` presence — required for `--leaf` and MCP consumers.
3. **22-02 Task 2 — Add `--loose-subdir` (default `"loose"`) to `ApplySaveEffectOptions`**: mirror 22-03 two-step `LooseOverridePath.Resolve(root, looseSubDir)` → `Resolve(base, relAsset)`. Document in 22-02 `verb_decisions`; add cross-reference acceptance grep in 22-03 close note. **Wave ordering:** Either add `depends_on: ["22-03"]` to 22-02 Task 2, or land `--loose-subdir` in 22-02 first so `ClefLooseOverrideTests` isn't flaky.
4. **22-01 Task 1 — Adopt Particle per-command try/catch**: Known tag + `DecoderException` → `ClientEffectCommand` raw view (`IsRaw=true`), not `ClientEffectParseException` abort. Add `clef_truncated_cpap.iff` (or truncated FFBK) golden: editor-safe degrade + roundtrip byte-exact.
5. **22-01 Task 1 — D-03 encode guards**: `ClefFieldCodec` CPAP encoder asserts caller cannot pass v0003-only fields when `Version == "0001"`. Add test: v0001 fixture + attempt v0003 field edit in codec tests → refuse or no-op at API boundary.
6. **22-02 `verb_decisions` — Lock `--add-command` defaults**: e.g. CPAP@file-version: `"appearance/default.prt"`, `time=1.0f`, v0002 `softParticleTerminate=0`, v0003 scales/rates `1.0f`. Same constants in `ClefFixtureBuilder` and `FormClientEffectEditor` "Add command".
7. **22-01 Task 2 — Strengthen length-ripple proof**: After string edit, assert **full-file** `SequenceEqual` only when no other dirty ops; otherwise assert per-leaf payload bytes + outer FORM length fields. Add **multi-command** string edit (edit middle command; first/last leaf payloads `SequenceEqual`).
8. **22-04 Task 2 — Wire stable IDs in UI**: each list row stores backing `MutableIffNode` + derived stableId (for undo and save). Explicitly state Form save uses in-proc `ClientEffectSaveTargets`, not `apply-save-effect` — add optional `ClientEffectInProcSaveParityTests` cloning terrain pattern.
9. **D-14 / PROD-W2-CFX-02 — Automate lineage check if possible**: If TRE extract yields small unencrypted CLEFs, add **optional** `RoundtripEffect_RealAsset_*` tests (skipped when fixtures absent), or document requirement downgrade in 22-04 SUMMARY when v6000 encrypted.
10. **22-01 Task 1 — Payload exhaustiveness**: After typed field decode, `if (cursor.Remaining != 0) → raw-fallback` (or parse error for CLI, raw for editor).

## 5. Risk Assessment

**Overall: MEDIUM.** The codec foundation (22-01) is on solid ground — the IFF DOM length-ripple is implemented and tested elsewhere. Variable-length CLEF is the right "one new asset." Risk concentrates in **22-02 integration**: structural verify for list mutations, missing `stableId` in JSON, and the `--loose-subdir` cross-plan gap can produce green codec tests but broken verbs/editor save paths. UI risk is **lower** thanks to honest Preview candor and MEF guards. Live-smoke (22-04 Task 3) appropriately gates what automation cannot cover, but **both-lineage** and **reload pickup** remain human-only — acceptable per AGENTS.md, but leaves PROD-W2-CFX-02 partially unproven in CI.

### Focus-area verdicts (brief)

| Focus | Verdict |
|-------|---------|
| Length-ripple / byte-exact | Codec tests (22-01) sufficient if strengthened; add/remove/reorder won't break verbatim re-emit of clean siblings; reorder intentionally changes file bytes — plan correctly tests reversibility, not identity. |
| D-03 version preservation | Sound in plan; add explicit encode-side guards + test. |
| Endianness / CLGT 23-byte no-pad | Handled via `IffPayloadCursor` + `IffWriter` no-pad; fixture `clef_v0003_all5.iff` covers CLGT. |
| Raw-fallback | Good for unknown tag/version; gap for **truncated known** commands. |
| Structural verify | **Unsound for list mutations as specified** — needs mode-specific logic (HIGH). |
| `<root>/loose/` containment | Editor + 22-03 clear; **22-02 incomplete** until `--loose-subdir` added. |
| MEF-safety | Well specified in 22-04. |
| Honest Preview | Correctly scoped — best part of 22-04 vs original D-07 wording. |
| Wave deps | Mostly fine; align 22-02 loose convention with 22-03 before verb tests land. |
| PROD-W2-CFX-01 / -02 | -01 achievable with 22-01+04; -02 achievable for verbs/MCP/load-order, but "both lineages" needs D-14 automation or explicit manual sign-off caveat. |

---

## Consensus Summary

Both reviewers rate the phase **MEDIUM risk** and agree the architecture is sound — the failure mode is not a crash but a *silently-wrong byte edit* that addresses or rewrites the wrong command. The two reviews converge on the same short list of fixes to make before execution.

### Agreed Strengths (both reviewers)
- Architectural layering is correct — codec in `UtinniCoreDotNet`, verbs-first CLI, MCP shells `decode-iff` with zero format logic (DEC-V2-VERBS-FIRST / DEC-V2-MCP-OOP / DEC-C4 all honored).
- Length-ripple is correctly framed as "exercise, don't build" — the four byte-exact assertions are the right ones.
- D-03 version preservation is explicit and correctly refuses to mirror SOE's `TAG_0003` hard-stamp.
- Raw-fallback for unknown version + unknown command tag is consistently specified.
- Replacing `apply-save-trn`'s fixed-span verify with a structural verify is the right call.
- Honest-candor Preview (`IsRetriggerHookReachable() => false`) is the best part of 22-04 — no over-promise.
- MEF-safety discipline (ctor try/catch + SplitContainer Size-before-SplitterDistance) is well specified.

### Agreed Concerns (raised by BOTH — highest priority)
1. **HIGH — Stable leaf identity / `stableId` is underspecified.** 22-02 addresses commands by `--leaf <stableId>` and compares untouched chunks "per stableId," but 22-01 never defines how IDs are assigned and `BuildClefResult`'s envelope omits `stableId` entirely. Index- or path-based IDs break under add/remove/reorder. The editor (22-04) needs the same leaf addressing for save — also unspecified. *(Codex Concern #1; Cursor Concerns #1+#2.)*
2. **HIGH — Structural verify is unsound / too weak for list mutations.** "Untouched chunks identical by stableId + re-parses + value matches" false-fails or false-passes once IDs shift on reorder/add-at-front, and does not prove the edited payload is *canonical* for the version (trailing bytes, NaN/Inf floats, non-0/1 bools, stray v0002/v0003 bytes in a v0001 CPAP could slip through). Fix: split the verify by mutation mode (field-edit = stableId compare; reorder = multiset of (tag, payload); add = count+1 + originals present; remove = count−1 by content) **and** assert exact payload length + full field decode + version-unchanged. *(Codex Concern #2; Cursor Concern #1 + Suggestion #1.)*
3. **HIGH — `--loose-subdir` cross-plan inconsistency.** 22-03 claims `apply-save-effect` shares the `--loose-subdir` (default `"loose"`) flag, but 22-02's `verb_decisions` never defines it — yet `ClefLooseOverrideTests` expects `<root>/loose/`. An executor mirroring `ApplySaveIffCommand` (no subdir) fails acceptance. Resolve before execution; consider a wave/dep ordering so 22-02's loose convention lands before its verb tests. *(Both, MEDIUM→HIGH.)*
4. **MEDIUM — Truncated *known-tag* command handling is ambiguous.** Codec/threat-model says a truncated CPAP throws a parse exception; the editor criterion says degrade to raw/hex and never crash. These conflict. Both suggest adopting Particle's per-command try/catch → raw view and adding a `clef_truncated_*` golden.
5. **MEDIUM — `--add-command` defaults unspecified.** No default appearance string / floats / CLGT RGB / FFBK iterations defined; executor invention risks non-deterministic goldens and editor↔CLI mismatch. Lock the constants once, shared by `ClefFixtureBuilder`, the CLI, and the Form.
6. **MEDIUM — "Both lineages" (PROD-W2-CFX-02) is mostly synthetic.** Eight `IffWriter`-built fixtures prove internal consistency, not real SWGEmu/Restoration bytes; D-14 real-asset roundtrip is manual-only. Both suggest at least one hand-authored byte/hex fixture independent of `IffWriter`, and an optional skipped real-asset roundtrip test, else an explicit CI-coverage caveat in the SUMMARY.
7. **MEDIUM — Payload exhaustiveness not enforced.** Neither plan requires `IffPayloadCursor.Remaining == 0` after typed decode, so trailing/slack bytes could survive an edit. Both want a consume-all-bytes assertion.
8. **MEDIUM — No in-proc ↔ CLI save-parity test (Cursor).** 22-04's `ClientEffectSaveTargets.Serialize()` and 22-02's `apply-save-effect` are two save paths that can diverge silently; terrain has `TerrainInProcSaveParityTests` as precedent.

### Divergent / single-reviewer Views (worth a look)
- **Codex only:** WinForms ctor try/catch can't fully recover a partially-built form — prefer a minimal no-throw ctor + explicit `BuildContentSafe()` failure state (mirror the SubPanel). Also: 22-03's `PROD-W2-CFX-01` requirement tag is misleading (it's save-matrix consistency, not CFX delivery); and 22-04 is the largest/most-brittle plan — split internally.
- **Cursor only:** Low-severity hygiene — wave-label drift (22-01 frontmatter `wave: 1` vs Roadmap/VALIDATION "Wave 0"), 22-03 test-name token vs VALIDATION's `Name~TerrainLooseSubdir` filter, and the broader matrix gap that `iff`/`stf`/`ot`/`tab` apply-save verbs still single-step resolve (outside folded scope). Also explicit encode-side D-03 guard test (reject v0003-only fields when version is 0001).

### Recommended pre-execution actions (synthesis)
Before executing, tighten three contracts that both reviewers flag as the difference between MEDIUM and LOW risk:
1. **Define the `stableId` contract** in 22-01 and surface it in `BuildClefResult` (22-02) + the UI rows (22-04).
2. **Rewrite the structural verify per mutation mode** + add canonical-payload assertions (exact length, full field decode, version-unchanged, consume-all-bytes).
3. **Reconcile `--loose-subdir`** across 22-02/22-03 with correct wave/dep ordering.
Then the smaller MEDIUMs: lock `--add-command` defaults, decide truncated-known-command policy (raw view, with a golden), add one writer-independent fixture + an optional real-asset roundtrip, and add an in-proc↔CLI save-parity test.

To incorporate this feedback into planning:
  `/gsd-plan-phase 22 --reviews`
