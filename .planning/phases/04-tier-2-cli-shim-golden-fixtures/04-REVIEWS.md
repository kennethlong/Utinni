---
phase: 4
reviewers: [codex, cursor]
reviewed_at: 2026-05-22T20:54:43Z
plans_reviewed: [04-01-PLAN.md, 04-02-PLAN.md, 04-03-PLAN.md, 04-04-PLAN.md]
unavailable_reviewers:
  - gemini: not installed
  - coderabbit: not installed
  - opencode: not installed
  - qwen: not installed
  - claude: skipped (self — this session runs in claude-code)
  - ollama / lm_studio / llama_cpp: no local server detected on default ports
verdict: FAIL (both reviewers independently) — 8 HIGH-severity blockers (5 from Codex, 3 new from Cursor) + 11 MEDIUM concerns must be addressed before /gsd:execute-phase 4
---

# Cross-AI Plan Review — Phase 4

## Codex Review

## Summary

The Phase 4 plan is directionally strong: it decomposes the CLI, parser, fixture, and golden-test work into sensible waves, preserves Tier-1 vs Tier-2 separation, and treats JSON as a real contract. However, several execution-level issues are serious enough that the plans are not ready to run as written. The biggest blockers are in 04-01's scaffold/test harness, 04-02's lazy TRE data model, 04-03's malformed IFF handling/legal fixture risk, and 04-04's native export probing plus drift away from the stated PluginLoader reuse contract.

## Strengths

- Clear phased structure: 04-01 establishes the harness, then TRE/IFF/plugin commands build on it.
- Good test layering: parser unit tests are separate from CLI golden tests.
- Strong fixture discipline: small in-repo fixtures, expected JSON, artifact dumps on mismatch.
- Security is explicitly modeled, especially malformed parser inputs and plugin inspection risk.
- D-01 clean-room intent is explicit and repeated; no plan directly instructs copying SOE/AGPL code.
- JSON contract thinking is mature: sorted keys, schema versioning, stable IDs, path masking.
- React Flow portability is considered early, especially 04-03's tree + flat IFF projection.

## Concerns

- **HIGH: 04-01 Task 1.1 likely will not build as written.** An SDK-style `OutputType=Exe` project with no `Main` generally fails with CS5001. Task 1.1 claims the empty exe project compiles, but `Program.cs` lands later in Task 1.4.

- **HIGH: 04-01 JsonOutput bypasses test stdout capture.** `JsonOutput.EmitSuccess` writes to `Console.OpenStandardOutput()`, while `InProcessCliRunner` captures via `Console.SetOut`. Tests will likely see empty stdout for JSON commands. Use `Console.Out` or redesign the runner.

- **HIGH: D-10 JSON schema placement is inconsistent.** D-10 says output includes a top-level `{ "schemaVersion": 1, ... }`, but 04-01's helper emits `{ command, result }` and pushes `schemaVersion` into `result`/`error`. 04-02's own contract examples conflict with its implementation notes.

- **HIGH: 04-02 lazy `GetRecordData` conflicts with `Open(string)` disposing the FileStream.** The plan says `Open(string)` uses `using` and delegates, while `GetRecordData` lazily seeks later. That cannot work unless the parser copies the file bytes, stores the path, or owns a live stream.

- **HIGH: 04-04 NativeExportProbe is probably invalid.** `LoadLibraryExW(..., LOAD_LIBRARY_AS_DATAFILE)` handles are not suitable for normal `GetProcAddress` export lookup. This may make all native export checks fail. Use PE export-table parsing or a documented load mode that supports export lookup without running code.

- **HIGH: 04-04 drifts from the stated PluginLoader reuse requirement.** Context says `validate-plugin` consumes `PluginLoader.cs`; key_links mention `new PluginLoader(autoLoad:false)`, but Task 4.1 switches to `ReflectionOnlyLoadFrom` and custom attribute inspection. That may not validate the same behavior the real loader uses.

- **MEDIUM: 04-04 managed plugin detection is fragile.** `ReflectionOnlyLoadFrom` requires dependency resolution and `GetCustomAttributesData` may not reflect MEF inherited export semantics the same way MEF composition does. This risks false negatives for valid managed plugins.

- **MEDIUM: 04-04 JSON contract allows only `managed|native|mixed`, but implementation introduces `unknown`.** Either document `unknown` in the stable schema or map unknown DLLs to a supported kind with failing checks.

- **MEDIUM: 04-03 pad-byte handling may hide malformed IFF.** The parser is instructed not to throw if an odd-length chunk reaches EOF without a pad byte. For strict regression tooling, missing pad should probably be `Truncated`.

- **MEDIUM: 04-03 `real-sample.iff` is a redistribution/legal risk.** The fallback helps, but the plan still encourages committing a real SWG asset sample. Given the project's D-01/legal posture, default to synthesized fixtures unless a sample is confirmed legally safe.

- **MEDIUM: 04-02 `list-objects` duplicates a partial IFF scan.** It intentionally avoids 04-03, but the raw `OBJS` byte scan is brittle and creates a second mini-IFF parser. This weakens the "same core libraries" goal and may break on real world snapshots.

- **MEDIUM: WinForms preservation criterion is weakly verified.** The roadmap says the WinForms UI continues to function. The plans rely on existing tests/proxy coverage and defer real UI smoke to Phase 6. That may be acceptable, but it should be called a residual risk.

- **LOW: xUnit parallelization can make `Console.SetOut` tests flaky.** The in-process CLI runner mutates global console state. Disable test parallelization for `Utinni.Cli.Tests` or serialize runner calls.

- **LOW: Several verify snippets use Unix tools or `/tmp` paths in a PowerShell/Windows project.** This is plan polish, but replacing with PowerShell-native commands will reduce executor friction.

## Suggestions

- **04-01 Task 1.1:** Land a minimal `Program.cs` in the first scaffold commit, or temporarily build `Utinni.Cli` as a library until Task 1.4.

- **04-01 Task 1.2 / 1.3:** Change `JsonOutput` to write through `Console.Out`, or make `JsonOutput` accept a `TextWriter`. Add an assembly-level xUnit collection/parallelization setting for CLI tests.

- **04-01 / all plans:** Decide one JSON envelope shape and enforce it everywhere. Recommended:
  `{ "schemaVersion": 1, "command": "...", "result": ... }`
  and `{ "schemaVersion": 1, "command": "...", "error": ... }`.

- **04-02 Task 2.1:** Make `TreFile` own immutable bytes or keep a safe file handle lifecycle. Do not return a lazy reader backed by a disposed stream.

- **04-02 Task 2.2:** Prefer making `list-objects` depend on the IFF reader once 04-03 lands, or explicitly mark the byte scan as temporary and add a Phase 4 cleanup task after 04-03.

- **04-03 Task 3.1:** Treat missing odd-length pad bytes as malformed/truncated unless a documented SWG variant requires leniency.

- **04-03 Task 3.3:** Make the "real sample" optional and default to a second synthesized fixture. Only commit real SWG-derived bytes after an explicit license note.

- **04-04 Task 4.1:** Replace `LoadLibraryExW + GetProcAddress` with PE export parsing, or test the chosen Windows API approach before baking it into the plan.

- **04-04 Task 4.1:** Either actually call `PluginLoader(autoLoad:false).Load(dir)` as a validation signal, or update the plan/context to say `validate-plugin` is static inspection only and does not assert loader equivalence.

- **04-04 Task 4.1 / 4.3:** Commit source for the three fixture DLLs, not only binaries. Binary-only fixtures are harder to audit and reproduce.

- **04-04 JSON contract:** Add `unknown` to the documented `kind` enum or remove the implementation branch.

## Risk Assessment

Overall risk: **HIGH** as written. The architecture and testing strategy are solid, but the current plans contain multiple likely build/test blockers and one major Windows API correctness issue. D-01 clean-room discipline looks acceptable, but the "real sample" fixture path needs tightening. The phase should be executable after targeted revisions to 04-01's scaffold/output capture, 04-02's TRE stream ownership, 04-03's fixture/legal and malformed-pad behavior, and 04-04's plugin inspection/export probing design.

## REVIEW VERDICT: FAIL

---

## Cursor Review (Independent — saw Codex's findings, instructed to cross-check + add new)

## Summary

The four plans are well structured: wave-0 scaffold, parser/CLI split, golden harness, threat models, and D-01 clean-room posture are all sound. Execution detail is where they break. Codex's core technical findings hold up — especially `LoadLibraryExW` + `GetProcAddress`, `Console.OpenStandardOutput` vs test capture, and `TreFile` stream lifetime. This review adds **contract drift inside 04-02** (load-bearing React Flow field names documented but not implemented), **internal contradictions in 04-01 Task 1.1 verify**, and **04-04's false claim that `PluginLoader` is exercised**. Plans are not safe to execute as written; a focused replan pass (~half a day) fixes most blockers without re-architecting the phase.

## Codex Cross-Check

| # | Codex HIGH | Verdict | Evidence |
|---|------------|---------|----------|
| 1 | **04-01 Task 1.1 won't build** (Exe without `Main` → CS5001) | **CONFIRM** | Task 1.1 sets `OutputType=Exe` but `Program.cs` lands in Task 1.4. SDK-style net472 exe projects require an entry point. Task 1.1 `<done>` claims "empty exe … is fine" — false for `Exe`. Worse: Task 1.1 `<verify>` requires `bin\Release\utinni-cli.exe` while `<done>` says "No code yet" — self-contradictory. |
| 2 | **JsonOutput bypasses `Console.SetOut` capture** | **CONFIRM** | Task 1.2 writes via `Console.OpenStandardOutput()`, which bypasses `Console.SetOut` redirection used by `InProcessCliRunner`. JsonOutputTests use `SetOut` directly (Task 1.2 behavior), so unit tests pass while golden/CLI integration tests see empty stdout. Classic .NET pitfall. |
| 3 | **D-10 `schemaVersion` placement inconsistent** | **CONFIRM with NUANCE** | D-10 / some must_haves imply top-level `schemaVersion`. Plan 04-01 Task 1.2 (Pitfall 7) nests it in `result`/`error`; JsonOutput wraps `{ command, result \| error }` only. 04-02 `<json_contract>` shows *both* top-level and nested `schemaVersion` plus fields outside the JsonOutput envelope. Not unfixable — pick one envelope and update D-10 — but goldens will thrash until aligned. |
| 4 | **04-02 `TreFile` stream lifecycle broken** | **CONFIRM** | Task 2.1: `Open(string)` uses `using var fs = …` then delegates to `Open(Stream)`; `GetRecordData` lazy-seeks later. Stream is disposed before lazy read. Fix: own bytes/stream on `TreFile` or eager-read in `Open`. |
| 5 | **`LOAD_LIBRARY_AS_DATAFILE` + `GetProcAddress` invalid** | **CONFIRM** | [Microsoft `GetProcAddress` remarks](https://learn.microsoft.com/en-us/windows/win32/api/libloaderapi/nf-libloaderapi-getprocaddress): handles from `LOAD_LIBRARY_AS_DATAFILE` must not be passed to `GetProcAddress` — call fails. Same restriction in `LoadLibraryEx` docs. Plan 04-04 Task 4.1 explicitly pairs these; Tier-1 tests expecting `HasExport(createPlugin)==true` would fail on Windows. **Fix:** PE export-table parse (`System.Reflection.Metadata` on net472 — needs NuGet, see New Findings #8), or `LoadLibraryExW` with `DONT_RESOLVE_DLL_REFERENCES` (0x1) — DllMain not called, `GetProcAddress` works per MSDN. Codex's suggested flag change is valid; PE parsing is safer for "no code execution." |
| 6 | **04-04 drifts from `PluginLoader` reuse** | **CONFIRM** | CONTEXT/key_links promise `new PluginLoader(autoLoad:false).Load(dir)`. Task 4.1 uses `ReflectionOnlyLoadFrom` + custom attribute scraping; `PluginLoader` never invoked. Native path uses `NativeExportProbe` only. "Same behavior as real loader" is not asserted — only partially equivalent static inspection. |

**Codex MEDIUM items:** Mostly valid. `kind: "unknown"` vs JSON contract, pad-byte leniency, `list-objects` byte-scan brittleness, and `ReflectionOnlyLoadFrom` MEF fidelity are real. WinForms preservation gap is acceptable per D-09 but should stay explicit in must_haves.

## New Findings (Cursor — not surfaced by Codex)

### HIGH

1. **04-02 `<json_contract>` vs Task 2.2 implementation — React Flow field drift.** Contract locks load-bearing keys: `records[].id`, `compressionKind`, `header.recordCount`, `objects[].id`. Task 2.2 `BuildResult` emits `name`, `dataCompression`, `resourceCount`, and list-objects objects have only `templateName` (no `id`). Executor will either violate the contract or re-baseline goldens against the wrong shape. **This is worse than D-10 envelope placement — it breaks the stated React Flow portability goal.** (Note: this drift was introduced by the post-plan-checker revision pass that added the `<json_contract>` block without updating Task 2.2's BuildResult call.)

2. **04-01 Task 1.1 verify/done contradiction** (adjacent to Codex's CS5001 finding). Verify demands `utinni-cli.exe` exists; done says no source files yet. First commit cannot satisfy its own gate.

3. **04-04 threat model / key_links misrepresent runtime behavior.** Threat table says managed path uses `PluginLoader.Load(dir)`; Task 4.1 does not. Documentation promises loader equivalence that tests won't enforce.

### MEDIUM

4. **`list-objects` OBJS sentinel scan** — `IndexOf("OBJS")` over raw bytes can false-positive inside payloads; no FORM boundary validation. Acceptable for synthesized fixture, risky for "tiny real" samples under D-03.

5. **04-03 PROP listed as container** — SWG/EA-IFF edge cases around PROP semantics; worth validating against reference reads before locking Tier-1 tests.

6. **Source-path grep test in CI** (`PluginInspectionTests` Test 5) — reads `Utinni.Cli/Commands/PluginInspection.cs` from repo via relative walk from `BaseDirectory`. Fragile if working directory/layout differs; prefer compiling against the built assembly or a `[assembly:]` attribute marker.

7. **Phase-closure ordering** — 04-04 `depends_on: [04-01]` only, but DEC-C3 promotion + "Phase 4 close" assumes all four commands shipped. D-05 CI gates help, but nothing prevents 04-04 landing before 04-02/03 if waves misfire.

8. **PEReader fix needs a net472 package** — if replan chooses PE export parsing, `System.Reflection.Metadata` is not in-framework on net472; add explicit NuGet (Codex suggested PEReader but omitted this dependency).

### LOW

9. **04-02 supports v0006 in artifacts but Tier-1 tests only exercise v0005** — version coverage gap.

10. **04-03 "5-chunk" naming vs 7 leaf nodes** — documented but confusing for fixture maintenance.

11. **Dispatch goldens mask assembly version** — good; but CommandLineParser help on `--help` vs `validate-plugin --help` differs — 04-04 tests both patterns correctly, worth keeping consistent across commands.

## Codex Over-Claims (Cursor's False-Positive Calibration)

| Codex item | Cursor assessment |
|------------|-------------------|
| **04-03 real-sample legal risk as default path** | **Partial over-claim.** Plan Task 3.3 includes synthesized fallback + `checkpoint:human-action`. Risk is real if executor commits SWG bytes without review, but plan already mitigates. Still worth defaulting to synth-only in executor instructions. |
| **`list-objects` duplication "weakens same core libraries"** | **Nuanced, not a blocker.** Plan explicitly documents parallel-wave independence and Phase 6+ refactor. Architectural debt, not an execution failure — Codex severity as MEDIUM is fair; calling it a phase blocker would be overreach. |
| **Pad-byte leniency hides corruption** | **Valid for strict tooling, not a build breaker.** Tier-1 can encode chosen behavior; pick strict vs lenient and document in goldens. |
| **WinForms preservation weakly verified** | **Not over-claimed** — correctly scoped; D-09 explicitly allows this. |

**Not a false positive:** Codex's `LOAD_LIBRARY_AS_DATAFILE` finding. This is documented Windows behavior, not folklore.

## Risk Assessment

**Overall: HIGH**

| Area | Rating | Notes |
|------|--------|-------|
| Architecture / phasing | MEDIUM | Sound decomposition; parallel 02/03 is intentional |
| 04-01 scaffold | **HIGH** | CS5001, stdout capture, verify contradiction |
| 04-02 TRE | **HIGH** | Stream bug + JSON contract/implementation mismatch |
| 04-03 IFF | MEDIUM | Parser plan is strong; PROP semantics + real-sample sourcing |
| 04-04 validate-plugin | **HIGH** | Native export probe broken as specified; loader drift |
| D-01 clean-room | LOW–MEDIUM | Discipline is explicit; executor compliance is the risk |
| CI / golden harness | MEDIUM | Fixable once stdout + contracts align |

**Minimum replan fixes before `/gsd:execute-phase 4`:**

1. Stub `Program.cs` in Task 1.1 (or Library until 1.4); write JsonOutput to `Console.Out` or inject `TextWriter`; disable parallelization in `Utinni.Cli.Tests`.
2. Freeze one JSON envelope spec; align 04-02 `<json_contract>`, Task 2.2, and goldens (especially load-bearing `id` fields).
3. Fix `TreFile` ownership (byte buffer or `IDisposable` with live stream).
4. Replace `LOAD_LIBRARY_AS_DATAFILE` probe with PE export parse or `DONT_RESOLVE_DLL_REFERENCES`; add a Windows API smoke test in Task 4.1 before goldens.
5. Either call `PluginLoader` for managed validation or rewrite CONTEXT/key_links to "static inspection only — no loader equivalence."

## REVIEW VERDICT: FAIL

---

## Consensus Summary (Codex + Cursor)

### Agreed Strengths

- Phased structure with clean Wave 1 scaffold → parallel Waves 2/3/4 commands.
- Tier-1 (parser) vs Tier-2 (CLI golden) test separation is principled and matches CONTEXT.md D-08.
- D-01 clean-room intent is explicit; no plan instructs copying SOE/AGPL code.
- Stable JSON contract thinking (sorted keys, schemaVersion, stable IDs) — well-intentioned even where execution drifts.
- Threat models present in every plan.
- React Flow portability lens is baked in (04-03 tree+flat dual projection, 04-04 plugin→checks shape).

### Agreed Concerns — Priority-Ordered

**HIGH — all 8 must be addressed before /gsd:execute-phase 4:**

1. **04-04 NativeExportProbe Windows API bug** [Codex + Cursor, both with MSDN evidence]. `LoadLibraryExW(..., LOAD_LIBRARY_AS_DATAFILE)` returns a handle that **cannot be passed to `GetProcAddress`** (MSDN: "must not pass"). The planner added `LOAD_LIBRARY_AS_DATAFILE` as a T-04-EoP mitigation (avoid DllMain) but it breaks the export-lookup mechanism the Tier-1 tests assert. **Two viable fixes:**
   - (a) **PE export-table parsing** via `System.Reflection.Metadata` `PEReader` (Cursor: needs explicit NuGet on net472 — Codex omitted this); safest, no DLL load.
   - (b) `LoadLibraryExW(path, NULL, DONT_RESOLVE_DLL_REFERENCES)` (0x1); DllMain not called, `GetProcAddress` works per MSDN; one-flag change but still loads code pages.

2. **04-01 Task 1.1 won't compile + has self-contradictory verify** [Codex: CS5001; Cursor: verify demands `utinni-cli.exe` while done says "no code yet"]. Stub `static void Main() {}` in Task 1.1, OR ship as Library until Task 1.4 flips OutputType.

3. **04-01 JsonOutput bypasses test stdout capture** [Codex + Cursor confirm]. `Console.OpenStandardOutput()` writes past `Console.SetOut`. Fix: route through `Console.Out` or accept explicit `TextWriter`. Unit tests pass via direct SetOut; golden tests will see empty stdout.

4. **04-02 TreFile stream lifecycle is broken** [Codex + Cursor confirm]. `Open(string)`'s `using FileStream` is disposed before `GetRecordData` lazy-seeks. Fix: eager-read bytes (D-08 fixtures <128KB make this trivial) or own the stream + `IDisposable`.

5. **04-04 drifts from PluginLoader.cs reuse contract** [Codex + Cursor confirm + Cursor adds threat-model false claim]. CONTEXT/key_links AND the `<threat_model>` block promise `new PluginLoader(autoLoad:false).Load(dir)`; Task 4.1 uses only `ReflectionOnlyLoadFrom`. Fix: either actually invoke `PluginLoader.Load(dir)` OR rewrite CONTEXT + threat_model + key_links to "static inspection only — loader equivalence NOT asserted."

6. **D-10 schema placement inconsistency** [Codex + Cursor confirm]. D-10 implies top-level `schemaVersion`; 04-01 JsonOutput pushes it into `result`/`error`; 04-02 `<json_contract>` shows BOTH placements. Fix: pick one shape — recommend top-level `{ "schemaVersion": 1, "command": "...", "result": ... | "error": ... }` — and patch every plan that documents an alternative.

7. **[NEW from Cursor] 04-02 `<json_contract>` vs Task 2.2 React Flow field drift.** Contract locks `records[].id`, `compressionKind`, `header.recordCount`, `objects[].id`; Task 2.2 BuildResult uses `name`, `dataCompression`, `resourceCount`, with no `id` on objects. **This is a real regression introduced by the post-plan-checker revision pass — the planner added the `<json_contract>` block without re-aligning Task 2.2's emission code.** Fix: align Task 2.2 BuildResult to the contract field names, OR amend the contract to match the implementation (the contract is correct per the React Flow goal — Task 2.2 is what should change).

8. **[NEW from Cursor] 04-04 threat_model misrepresents runtime.** The threat-model block claims managed path runs through `PluginLoader.Load(dir)` (relying on Phase 2 C-06 per-plugin try/catch). It does not. Fix: align with #5 — either invoke PluginLoader OR remove the threat-model claim.

**MEDIUM (8 items, revise if time permits; document otherwise):**

9. **04-04 `kind: "unknown"` drift** [Codex] — undocumented fourth kind. Add to schema or map to failing checks.
10. **04-04 managed plugin reflection fragility** [Codex] — `ReflectionOnlyLoadFrom` + `GetCustomAttributesData` may miss MEF inherited Exports. Runtime smoke test recommended.
11. **04-03 odd-length-chunk pad-byte leniency** [Codex; Cursor: "not a build breaker but pick one"] — lenient = silently-acceptable corruption.
12. **04-03 real-sample.iff legal posture** [Codex; Cursor partial over-claim — fallback exists] — strengthen by defaulting executor to synth-only.
13. **04-02 list-objects OBJS-sentinel scan brittleness** [Codex; Cursor: "architectural debt, not blocker"] — `IndexOf("OBJS")` can false-positive inside payloads on real samples.
14. **[NEW from Cursor] 04-03 PROP listed as container** — SWG/EA-IFF edge cases; validate against reference reads before locking Tier-1.
15. **[NEW from Cursor] Source-path grep test fragility** — `PluginInspectionTests` Test 5 reads `PluginInspection.cs` via relative walk; flaky if working dir differs. Prefer `[assembly:]` attribute marker.
16. **[NEW from Cursor] Phase-closure ordering** — 04-04's `depends_on: [04-01]` doesn't prevent 04-04 landing before 02/03. DEC-C3 promotion assumes all four commands shipped. Either add `depends_on: [04-01, 04-02, 04-03]` to 04-04 OR enforce wave order via D-05 CI gate.
17. **[NEW from Cursor] PEReader needs `System.Reflection.Metadata` NuGet on net472** — important addendum to HIGH #1 fix path (a).

**LOW (5 items, polish):**

18. **xUnit parallelization + Console.SetOut** [Codex] — global state mutation races. `[assembly: CollectionBehavior(DisableTestParallelization = true)]` on `Utinni.Cli.Tests`.
19. **`/tmp` + Unix tools in `<verify>` snippets** [Codex] — translate to PowerShell-native where appropriate.
20. **[NEW from Cursor] 04-02 v0006 in artifacts but Tier-1 only tests v0005** — version coverage gap.
21. **[NEW from Cursor] 04-03 "5-chunk" naming vs 7 leaf nodes** — fixture-maintenance confusion.
22. **[NEW from Cursor] Dispatch goldens vs `validate-plugin --help` consistency** — keep CommandLineParser help shape consistent across commands.

### Divergent Views (Cursor refines Codex severity)

- **04-03 real-sample legal risk:** Codex flagged HIGH-ish; Cursor downgrades to "partial over-claim" because the plan already includes synthesized fallback + checkpoint. Net: MEDIUM with executor-side default change.
- **04-02 list-objects byte-scan:** Codex framed as a goal-weakening concern; Cursor: "architectural debt, not blocker." Net: MEDIUM as architectural debt; document Phase 6+ refactor.
- **04-03 pad-byte leniency:** Codex implied "should throw"; Cursor: "pick a behavior, document, lock in goldens" — either is defensible.

---

## Recommended Path Forward

**Option A (recommended) — Replan with `--reviews`:**
```
/gsd:plan-phase 4 --reviews
```
The planner re-reads REVIEWS.md as input and produces a revision pass addressing all 8 HIGH concerns + 8 MEDIUM concerns systematically. Estimated ~half-day per Cursor; bounded scope (no re-architecting — only fixing execution-detail issues).

**Option B — Surgical patch:** Use the Edit tool against the specific tasks for the 8 HIGH concerns only; defer MEDIUM/LOW to execute-phase deviation log. Faster (~45 min) but spotty MEDIUM coverage.

**Option C — Ship as-is:** Not recommended. Two independent AI reviewers reached FAIL with overlapping HIGH findings plus a verified Windows API correctness bug. Execute-phase would surface these as test failures, wasting executor time on retroactive plan fixes.

**Two-reviewer adversarial coverage achieved this run:** Codex (Anthropic-trained baseline) + Cursor (different model family). Cursor confirmed 6 of 6 Codex HIGH findings and surfaced 3 NEW HIGH findings Codex missed — exactly the adversarial signal the cross-AI review pattern is designed to produce.
