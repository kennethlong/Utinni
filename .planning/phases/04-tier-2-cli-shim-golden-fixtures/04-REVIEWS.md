---
phase: 4
reviewers: [codex]
reviewed_at: 2026-05-22T20:54:43Z
plans_reviewed: [04-01-PLAN.md, 04-02-PLAN.md, 04-03-PLAN.md, 04-04-PLAN.md]
unavailable_reviewers:
  - gemini: not installed
  - coderabbit: not installed
  - opencode: not installed
  - qwen: not installed
  - claude: skipped (self — this session runs in claude-code)
  - cursor: binary on this machine is the IDE entrypoint (3.2.16), not the headless `cursor agent` CLI — no --mode flag accepted; opens the editor instead. Effectively unavailable.
  - ollama / lm_studio / llama_cpp: no local server detected on default ports
verdict: FAIL (per Codex) — 5 HIGH-severity blockers + 6 MEDIUM concerns must be addressed before /gsd:execute-phase 4
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

## Consensus Summary

> **Note: only one reviewer (Codex) was available this run.** The "consensus" sections below reflect Codex's findings only. To get true adversarial cross-AI coverage, install `gemini` or the headless `cursor-agent` CLI and re-run `/gsd:review --gemini` or `--cursor`.

### Agreed Strengths (Codex)

- Phased structure with clean Wave 1 scaffold → parallel Waves 2/3/4 commands.
- Tier-1 (parser) vs Tier-2 (CLI golden) test separation is principled and matches CONTEXT.md D-08.
- D-01 clean-room intent is explicit; no plan instructs copying SOE/AGPL code.
- Stable JSON contract thinking (sorted keys, schemaVersion, stable IDs).
- Threat models present in every plan.
- React Flow portability lens is baked in (04-03 tree+flat dual projection, 04-04 plugin→checks shape).

### Agreed Concerns — Priority-Ordered (5 HIGH, 6 MEDIUM, 2 LOW)

**HIGH (revise before execute-phase):**

1. **04-04 NativeExportProbe Windows API bug** — `LoadLibraryExW(..., LOAD_LIBRARY_AS_DATAFILE)` returns a handle that **cannot be used with `GetProcAddress`** (returns `ERROR_PROC_NOT_FOUND` per MSDN). The planner added this flag as a T-04-EoP mitigation (avoid DllMain execution) but it breaks the export-lookup mechanism. **Fix:** either (a) switch to `DONT_RESOLVE_DLL_REFERENCES` (0x1) which DOES allow GetProcAddress without running DllMain, or (b) parse the PE export table directly via `System.Reflection.PortableExecutable.PEReader`. Option (b) is safer + more portable but adds code; option (a) is a one-flag change.

2. **04-01 Task 1.1 won't compile** — SDK-style `OutputType=Exe` with no `Main` fails CS5001. Either land a stub `Program.cs` with `static void Main() {}` in Task 1.1 (recommended; cheap), or temporarily ship `Utinni.Cli` as a library and flip to Exe in Task 1.4.

3. **04-01 JsonOutput bypasses test capture** — Writes via `Console.OpenStandardOutput()` while `InProcessCliRunner` redirects via `Console.SetOut(StringWriter)`. The `OpenStandardOutput` path reaches the raw OS handle and skips the `SetOut` redirection. **Fix:** route through `Console.Out` (or `Console.Out.Write(jsonString)`), OR accept an explicit `TextWriter` parameter so tests can pass their own writer.

4. **04-02 TreFile stream lifecycle is broken** — `Open(string path)` uses `using var fs = ...` and returns a `TreFile`, but `GetRecordData(record)` later seeks back into the same stream. The stream is disposed by the time the consumer calls `GetRecordData`. **Fix:** either eagerly read all record bytes during `Open()` (simpler; bounded by D-08's <128KB fixture cap), or have `TreFile` own the stream + implement `IDisposable` (correct for arbitrary-size production use).

5. **04-04 drifts from `PluginLoader.cs` reuse contract** — CONTEXT.md and key_links promise `new PluginLoader(autoLoad:false).Load(dir)` consumption, but Task 4.1 switches to `ReflectionOnlyLoadFrom` + custom attribute scanning. The two paths can give different answers (MEF composition vs raw reflection). **Fix:** either actually invoke `PluginLoader.Load(dir)` as the contract claims and inspect its outputs, OR update CONTEXT.md/plan text to declare "validate-plugin is static inspection only; equivalence with PluginLoader is NOT asserted."

6. **D-10 schema placement inconsistency** — D-10 specifies a top-level `{ "schemaVersion": 1, ... }` envelope, but 04-01's `JsonOutput.EmitSuccess` emits `{ command, result }` and pushes `schemaVersion` into the nested `result` block. 04-02's `<json_contract>` examples then mix both shapes. **Fix:** pick one envelope shape — recommend top-level `{ "schemaVersion": 1, "command": "...", "result": ... | "error": ... }` — and patch every plan that documents an alternative.

**MEDIUM (revise if time permits; document otherwise):**

7. **04-04 `kind: "unknown"` drift** — JSON contract allows `managed|native|mixed`; the planner-discovered fourth kind for DLLs that can't be classified surfaces as `unknown`. Either add it to the schema or map unknown→failing checks.

8. **04-04 managed plugin reflection fragility** — `ReflectionOnlyLoadFrom` requires dep resolution; `GetCustomAttributesData` may miss MEF inherited Export semantics. False negatives possible. Consider a runtime test with a known-good managed plugin.

9. **04-03 odd-length-chunk pad-byte handling** — Currently lenient (don't throw on missing pad at EOF). For strict regression tooling, lenient = silently-acceptable corruption. Codex recommends treating missing pad as `Truncated`.

10. **04-03 real-sample.iff legal posture** — Even with the operator-declined fallback, the *default* path encourages committing real SWG asset bytes. D-03 caps at <128KB but doesn't address derivative-work risk. Strengthen: default to synthesized; commit real samples only after explicit case-by-case approval.

11. **04-02 list-objects byte-scan brittleness** — The OBJS-sentinel scan is a parallel-wave workaround. Codex notes it weakens the "same core libraries" goal. Either commit to refactoring to IffReader in Phase 6, OR mark the byte-scan as deliberate and document why the duplication is acceptable.

12. **WinForms preservation criterion is weakly verified** — Phase 4 success criterion #3 ("WinForms UI continues to function") is asserted via "existing tests are the proxy + Phase 6 covers real smoke." Acceptable per D-09 (no new Tier-4 carve-outs), but the residual risk should be explicit in `must_haves`.

**LOW (polish):**

13. **xUnit parallelization + `Console.SetOut`** — Global console state mutation can race in parallel test execution. Add `[assembly: CollectionBehavior(DisableTestParallelization = true)]` to `Utinni.Cli.Tests`, OR serialize through a per-runner lock.

14. **`/tmp` + Unix tools in `<verify>` snippets** — Plans target a Windows/PowerShell-first project; some verify snippets use Bash-isms. Translate to PowerShell-native commands where appropriate (cosmetic).

### Divergent Views

(N/A — single reviewer this run.)

---

## Recommended Path Forward

**Option A (recommended) — Replan with `--reviews`:**
```
/gsd:plan-phase 4 --reviews
```
This re-runs the planner with REVIEWS.md as input. The planner will produce a revision pass addressing the 5 HIGH concerns + 6 MEDIUM concerns systematically.

**Option B — Manual patch:** Apply targeted Edits to specific tasks across the 4 plans for just the HIGH concerns; defer MEDIUM/LOW. Faster (~30 min) but spotty coverage.

**Option C — Ship as-is:** Not recommended given 5 HIGH severity findings including a Windows API correctness bug (LoadLibraryExW + GetProcAddress incompatibility) that would surface as test failures during execute-phase rather than at plan time.

**For better cross-AI signal next time:** Install `gemini` CLI (`npm i -g @google/gemini-cli`) or the headless `cursor-agent` and re-run `/gsd:review 4 --gemini` to get a second adversarial reviewer.
