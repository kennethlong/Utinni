---
phase: 8
reviewers: [codex, cursor]
reviewed_at: 2026-05-28T07:30:00Z
plans_reviewed: [08-01-PLAN.md, 08-02-PLAN.md, 08-03-PLAN.md, 08-04-PLAN.md, 08-05-PLAN.md, 08-06-PLAN.md, 08-07-PLAN.md]
overall_risk_codex: HIGH
overall_risk_cursor: HIGH
self_reviewer_skipped: claude (CLAUDE_CODE_ENTRYPOINT=cli)
---

# Cross-AI Plan Review — Phase 8 (TJT subpanel — IFF Editor read + write)

Two independent reviewers (Codex / `gpt-5.5` and Cursor agent) reviewed all 7 PLAN.md files plus CONTEXT.md, RESEARCH.md, PATTERNS.md, and UI-SPEC.md. Both landed on **HIGH overall risk**, with strong overlap on three "design hole" surfaces that should be resolved in planning before execution.

---

## Codex Review

## Summary

The plan set covers the Phase 8 goal in breadth: shared IFF write primitives, CI round-trip coverage, editable TJT UI, file save modes, live patch, TRE repack, and manual Tier-4 validation. The decomposition is mostly sound, but the risky parts are under-specified in ways that could produce false confidence: `.tre` repack correctness, live-patch address provenance, forced reload semantics, and cross-repo build/test coupling all need sharper gates before this is executable safely.

## Strengths

- Clear separation between framework primitives and TJT UI. 08-01 correctly honors D-01 by putting `IffWriter` / mutable DOM in `UtinniCoreDotNet/Formats/Iff`.
- The D-07 hybrid DOM approach is the right default for byte-preservation. Verbatim clean-node emission is much safer than reconstructing the whole file.
- 08-02 adds a useful CI gate through `roundtrip-iff`, including the no-pad regression case.
- 08-06 isolates mapped-memory writes behind a separate plan, confirm dialog, `Game.IsRunning`, same-size-or-smaller validation, and `Memory.memory.Copy`.
- 08-07 correctly treats `.tre` save as full rebuild, not in-place mutation, and calls out stored CRC preservation instead of guessing the path CRC.
- Manual Tier-4 gates are explicit where CI cannot cover live-client behavior.

## Concerns

- **HIGH — 08-07 Task 1: "untouched entries are re-emitted byte-for-byte" is probably overstated.** Rebuilding the archive with `GetRecordData(i)` plus fresh compression cannot preserve original compressed payload bytes, compressed sizes, block offsets, or TOC layout. It can preserve logical payloads, but not archive-byte identity. The plan's "untouched-entry bytes compare identical to the original archive bytes" acceptance is likely impossible for compressed entries unless it copies original raw compressed blobs and TOC metadata directly.

- **HIGH — 08-07 Task 1: preserving `TreRecord.Checksum` may be necessary but not sufficient.** Path CRC is only one resolution field. Repack can still break clients via compression flags, name-block offsets, TOC ordering, version-specific record stride, duplicate CRC collision ordering, alignment assumptions, or header count/offset mistakes. The plan focuses heavily on CRC and underweights these other TOC invariants.

- **HIGH — 08-06 Task 2: live-patch target address provenance is hand-waved.** The plan says "given the target address + original mapped length" and depends on 08-05 summary for tracking, but earlier plans do not clearly establish how an IFF opened from TRE/file maps to a live client memory address. Most editor-opened payloads are file/TRE bytes, not necessarily a live mapped region. Without a reliable source-address resolver, the feature risks being enabled for the wrong buffer.

- **HIGH — 08-06 Task 2: same-size-or-smaller patch can still corrupt container semantics.** If the serialized IFF becomes smaller than the original mapped region, copying fewer bytes leaves stale trailing bytes in memory unless the live structure has an external length field updated elsewhere. Refusing growth is good, but shrinking is not automatically safe. Same-size only may be the only defensible V1 live-patch constraint unless the mapped object's length is also patched.

- **HIGH — 08-05 Task 2: "scene-change-style re-setup" is not defined enough to implement safely.** The plan references `Game.AddSetSceneCallback(...)`, but callbacks are usually notification hooks, not a command to reload a scene. If this is wrong, an implementer may wire a no-op or create reentrancy around scene setup. This needs a concrete existing TJT scene-change invocation path, not a vague fallback.

- **MEDIUM — 08-01 Task 1/2: clean container verbatim emission conflicts with edited descendants unless dirty propagation is perfect.** The plan states a clean node re-emits its captured slice and a container is clean iff no descendant changed. That is correct, but the implementation has to make dirty propagation impossible to bypass. Directly mutating child collections or payload arrays could leave ancestors clean and cause edited children to be discarded.

- **MEDIUM — 08-01 Task 1: source slice calculation assumes `OffsetBytes + 8 + LengthBytes` is always the full serialized node.** That is true only for SWG no-pad inputs. The reader "detects" pad handling, so if it accepts a padded standards-compliant IFF, the captured slice may exclude pad bytes. The plan says writer emits no pad, but byte-exact round-trip for non-SWG padded fixtures may fail or leave unmodeled bytes.

- **MEDIUM — 08-01 Task 2: integer overflow risk is acknowledged only as a size cap.** Container roll-up uses sums of child serialized sizes. A hostile deep/wide tree can overflow `int` before the 64 MB cap check if sums are not done in `long`/checked arithmetic. The plan should require checked accumulation for `8 + length`, container inner length, and stream offsets.

- **MEDIUM — 08-04 Task 4: linked-source testing of `IffEditController` across repos is brittle.** Compiling a TJT source file into `UtinniCoreDotNet.Tests` creates a hidden dependency on namespace/usings and can easily drift if the controller later touches WinForms or plugin types. Better to keep the pure command model in `UtinniCoreDotNet` or a small shared assembly if it needs framework tests.

- **MEDIUM — 08-04 Task 1: undo/redo based on inverse commands may miss dirty-state semantics.** Undoing back to original bytes should probably clear dirty for that subtree and maybe the document, but the plan only says dirty propagates upward. Without baseline comparison or command-count tracking, undo can leave false dirty markers and bad save prompts.

- **MEDIUM — 08-05 Task 1: loose override path safety is underspecified.** "Preserve the asset's relative path under that root" needs explicit normalization and root containment checks after `Path.GetFullPath`. A crafted TRE path with `..`, rooted drive syntax, or alternate separators could escape the client directory if the implementation only uses `Path.Combine`.

- **MEDIUM — 08-05 Task 4: manual smoke is asked to edit "one structural op" then save loose override.** For arbitrary IFFs, structural edits can produce semantically invalid assets even if chunk framing is valid. This may conflate writer correctness with game asset validity. Smoke should include a known safe fixture/edit recipe.

- **MEDIUM — 08-07 Task 2: atomic replace of `.tre` while the client is running can fail or produce unclear state.** If SWG holds the archive open, `File.Replace`/move may fail. If it succeeds while the client has cached some archive state, reload behavior may be inconsistent. The plan should define "close client or ensure archive handle released" for repack smoke, or force Save-As/repack-copy first.

- **MEDIUM — 08-07 Task 2: backup naming can overwrite previous backups.** `<name>.tre.bak` is not enough for high-blast-radius archive writes. A failed second attempt can destroy the last known-good backup. Use timestamped or non-overwriting backups.

- **LOW — 08-03 Task 1: "four-state degradation" belongs mostly to `TreDetailPane`, not `IffChunkTree`.** A tree control rendering already-parsed docs cannot meaningfully catch parse failures unless its API includes error states. This may lead to duplicated or confused state handling.

- **LOW — 08-04 Task 3: editable hex dump in a plain textbox is a usability trap.** Inverting a formatted hex+ASCII dump requires ignoring offsets/ASCII columns or enforcing a strict format. The plan needs to specify accepted input shape, otherwise users can edit the ASCII column and expect bytes to change.

- **LOW — 08-05/08-06/08-07: repeated `FormIffEditor.cs` edits remain a merge hotspot.** 08-07 depends on 08-06, but 08-04, 08-05, 08-06, and 08-07 all grow the same form. The sequencing avoids parallel collision, but the final file may become an oversized god form.

## Suggestions

- In 08-07, change the TRE preservation claim from "archive bytes identical" to two separate guarantees: logical payload identity for all untouched entries, and raw compressed blob preservation where possible. Prefer copying original compressed payload blobs for untouched entries instead of decompressing/recompressing them.

- Add a TRE invariant test that compares TOC fields for untouched entries: checksum, name offset/name string, compression flag, uncompressed size, payload bytes after `GetRecordData`, and lookup by logical path/CRC if such API exists.

- For 08-06, require live patch to be enabled only when the editor has a verified memory-backed source descriptor: `address`, `length`, asset logical path, and proof it corresponds to the currently loaded asset. Otherwise keep the menu disabled.

- Consider making live patch **same-length only** for V1. If smaller patches are allowed, require explicit zero-fill or a proven length-field update path.

- Replace the vague D-06 scene fallback with a named existing method/path from TJT. If none exists, mark general reload as unavailable and document "next scene change" instead of implementing speculative scene manipulation.

- In 08-01, require mutable payload getters to return copies or controlled spans so dirty propagation cannot be bypassed by mutating an array in place.

- In 08-01, require checked `long` arithmetic for all serialized sizes, then validate against `uint.MaxValue`, `int.MaxValue`, and the project's 64 MB chunk cap before writing headers.

- In 08-05, add a root-containment helper for loose overrides: normalize relative asset path, reject rooted paths and `..`, combine, `GetFullPath`, then verify it starts with the resolved client override root.

- In 08-07, use timestamped backups and write the repacked archive to a sibling temp file on the same volume before replace.

- Split save/reload/live-patch/repack logic out of `FormIffEditor` aggressively. The plans already introduce helper classes; keep the form as orchestration only.

## Risk Assessment

**Overall risk: HIGH.** The IFF writer and CLI round-trip portions are reasonable, but the phase includes two dangerous surfaces: mapped process-memory writes and full `.tre` archive rebuilds. The current plans reduce some risk through isolation and manual gates, but still rely on unproven assumptions about live memory address tracking, shrink-safe patching, scene reload behavior, and TRE byte preservation. Tightening those gates before implementation would materially lower the chance of corrupting a live client session or a user's archive.

---

## Cursor Review

## Summary

The seven-plan split is structurally sound: framework writer + CLI gate first (08-01/02), shared UI extraction (08-03), editor core (08-04), low-risk saves + reload (08-05), then isolated high-risk modes (08-06/07). That sequencing matches D-05's PLAN-SPLIT FLAG. The set will **not** fully close Phase 8 as written: **in-memory live patch (08-06) has no designed source for `targetAddr`**, D-06 reload is likely to ship as a best-effort stub, and **TreWriter's recompress-everything approach conflicts with its own byte-identity acceptance tests**. CI can also break on 08-04's UtinniPlugins linked-source unless the workflow is extended.

---

## Strengths

- **08-01 D-07 hybrid DOM** is the right fidelity mechanism; verbatim slices keyed on `IffChunk.OffsetBytes` align with the reader (`IffReader.cs` sets `chunkStart` at the TypeID header).
- **08-02 `roundtrip-iff`** correctly mirrors the Phase 4 CLI envelope/exit-code contract and gives a real unattended gate for Criterion 4 on the no-mutation path.
- **08-05/06/07 plan-split** with explicit `FormIffEditor.cs` ownership (`08-07` `depends_on: ["08-05","08-06"]`) avoids the obvious merge collision.
- **08-06 bounds gate** (refuse `newLength > originalMappedLength`, `targetAddr == 0`) is appropriate for CON-N-04 writes.
- **08-07 CRC sidestep** (preserve `TreRecord.Checksum` for payload-only edits) is a reasonable mitigation given the unverified path-CRC algorithm.
- **Editor-local undo (D-08)** with an explicit grep ban on `UndoRedoManager` / `AddUndoCommand` is the right CON-M-05 guard.
- **Tier-4 human gates** on 08-05/06/07 honestly reflect what CI cannot prove.

---

## Concerns

### HIGH

- **08-06: No plan for obtaining mapped IFF address/length.** Task 2 (`08-06-PLAN.md` ~L117–L129) and Task 3 (~L157) require "document opened from a known client-memory address," but **08-05 Task 3** only hands off `payload bytes + logical path` from `FormTreBrowser.DispatchDetail` — Phase 7 resolves via `TrePayloadResolver`, not client memory. Nothing in 08-01–08-05 defines discovery/storage of `targetAddr` + `originalMappedLength`. Live patch may ship permanently disabled while still counting as a D-05 must-have.

- **08-06: Shrinking patches leave stale tail bytes.** Copy uses `rewritten.Length` with cap `<= originalMappedLength` (`08-06-PLAN.md` ~L126–L128). If the serialized IFF **shrinks** (structural remove, shorter leaf), copying fewer bytes than the original mapped region leaves **old bytes after the new EOF** — in-memory IFF corruption. No zero-fill/truncate strategy is specified.

- **08-07: "Byte-identical untouched entries" vs recompress-all design.** Task 1 behavior (`08-07-PLAN.md` ~L109–L141) recompresses every entry via `GetRecordData` + inverted `Inflate`, but acceptance requires untouched entries **byte-identical** to the original archive. `GetRecordData` returns **decompressed** payloads (`TreFile.cs` ~L363–L424); re-zlib/deflate will not reproduce original on-disk compressed blobs, compressor flags (0/1/2), or offsets. Tests claiming `fc /b` identity are likely to fail or test the wrong thing unless TreWriter copies **raw compressed slices** at `rec.Offset` for untouched records (new API needed).

- **08-07 / 08-05: Repack provenance not wired.** `TreRepackSaveTarget` needs source `.tre` path **and record index** (`08-07-PLAN.md` Task 2 ~L163–L168). **08-05 Task 3** passes "logical path" only — insufficient when `TreArchiveIndex` resolves across master index + multiple archives (`TrePayloadResolver.cs`). Repack menu may enable on ambiguous metadata or fail at runtime.

- **08-04 Task 4: CI will break without sibling checkout.** Linked-source path `..\..\UtinniPlugins\...\IffEditController.cs` (`08-04-PLAN.md` ~L248–L252) assumes repo layout; **`ci.yml` checks out Utinni only** (no UtinniPlugins step, unlike `release.yml`). `dotnet test UtinniCoreDotNet.Tests` fails on missing file unless CI is updated or the controller moves framework-side.

- **D-06 forced reload (`08-05` Task 2): Scene-change fallback is unspecified.** Plan says "attempt scene-change-style re-setup via the set-scene path" but does not name a concrete trigger (e.g. `GameCallbacks` setup-scene subscriber replay, existing TJT hook). Without a defined call site, implementers will ship **texture/terrain-only + tooltip fallback** — datatables/STF/templates/object templates will not reload in-session, which is most of Wave 1's downstream phases.

### MEDIUM

- **08-01: Container length roll-up overflow unchecked.** Task 2 rolls up `innerLen = 4 + Σ child sizes` with `MaxChunkSize` on leaf payload only (`08-01-PLAN.md` ~L98–L106). Summing many children can **overflow `int`** before the cap check; `IffReader` uses checked bounds elsewhere. Hostile/large trees could emit negative/wrapped container lengths.

- **08-01 D-07: Clean container with dirty descendant must not re-emit verbatim slice.** Plan states this implicitly via dirty propagation, but Task 1 behavior (~L72–L74) should explicitly require **ancestor invalidation of captured slices** when any descendant dirty — otherwise a bug in dirty propagation silently re-emits stale container bytes including wrong child lengths.

- **08-02: CLI gate covers identity-only round-trip.** `RoundtripIffCommand` (`08-02-PLAN.md` Task 1) never mutates before write. Criterion 4 for **edited** trees relies solely on 08-01 unit tests; no CLI golden for "edit fixture → write → byte-exact on untouched subtrees." Regression in editor-only structural paths won't surface in `Utinni.Cli.Tests`.

- **08-04: Ctrl+Z / TextBox undo collision.** UI-SPEC requires editor-local `Ctrl+Z`/`Ctrl+Y`; editable `txtHex` is a standard `TextBox` with **built-in edit undo** (`08-04-PLAN.md` Task 3). Plan does not override `ProcessCmdKey` on `FormIffEditor` or disable `TextBox.ShortcutsEnabled`. Hex edits may undo locally without touching `IffEditController`, desyncing tree dirty state from payload.

- **08-04: Hex-in-TextBox parse model is fragile.** Inverse of `HexDump` (offset columns + ASCII gutter) is underspecified; paste/edit will produce validation failures or wrong byte alignment (UI-SPEC Assumption 2 deferred risk). No plan B if V1 hex editing is unusable.

- **08-05: Loose-override dir still a guess until Tier-4.** `IffSaveTargets` derives base from `ResolveClientTreDir` + ini fallback (`08-05-PLAN.md` Task 1 ~L108–L110). SWG loose-file priority is path-relative inside client data roots — **wrong subdir = silent "saved but never loaded."** Open Q2 is "resolved" only by maintainer smoke, not by code research in-plan.

- **08-05: Save → reload race.** File save on `Task.Run` then immediate `ClientReloadDispatcher` on game thread (`08-05` Tasks 1–3) has no flush/fsync or "write completed" barrier before reload. Client may re-read stale bytes from OS cache or incomplete write.

- **08-03: Full tree rebuild on every edit.** `LoadMutable` + refresh after each `IffEditController` op (`08-04` Task 3) with no incremental `TreeNode` update — large IFFs (datatables) may UI-stutter; no perf guard despite RESEARCH "deferred large-file guards."

- **Cross-repo build coupling.** 08-03/04/05/06/07 require rebuilt `UtinniCoreDotNet.dll` in TJT `HintPath`; no plan step pins/framework-builds both repos in one commit. Uncommitted `Generated/UtinniCore.cs` in the working tree adds binding drift risk for `Memory.memory.Copy` line citations.

- **08-07: Atomic replace on Windows.** `File.Replace`/temp-move (`08-07-PLAN.md` Task 2) can fail if SWG client holds the `.tre` open (likely while injected). No "close handle / copy loose override instead" fallback.

### LOW

- **08-01 Task 3 grep verify** (`grep -c "UtinniCoreDotNet/Formats/Iff"`) is weak — proves string presence, not correct reconciliation of Architecture + Criterion 5.

- **08-03 verification** is grep + manual TRE Browser check; no automated regression that `TreDetailPane.LoadIff` public API unchanged beyond compile.

- **ROADMAP title still says "subpanel"** while UI-SPEC/08-04 ship `FormIffEditor` via `GetForms()` — documentation drift, not functional.

- **08-06 `autonomous: false`** with blocking Tier-4 on 08-05/06/07 means phase completion is **maintainer-gated**; schedule risk if smoke fails late on repack or live patch.

- **Preserved CRC assumption (A1)** remains unproven until live repack smoke — if client validates CRC against uncompressed payload (not path-only), preserving `Checksum` on edited entries would break resolution.

---

## Suggestions

1. **Add an explicit "document provenance" model in 08-05** (before 08-06): `OpenSource { LooseFile | TreArchive(path, recordIndex, logicalPath) | ClientMemory(addr, mappedLen) }`. TRE hand-off must populate `TreArchive`; live patch requires `ClientMemory` or stays disabled with honest UI copy.

2. **08-06 live patch:** Specify `copyLen = rewritten.Length`; if `copyLen < originalMappedLength`, **zero-fill** `[targetAddr+copyLen .. targetAddr+originalMappedLength)` on the game thread, or refuse shrink patches. Document that structural ops may be file-save-only.

3. **08-07 TreWriter:** For untouched records, add `GetRecordCompressedBytes(index)` (read `rec.Offset`/`rec.CompressedSize` verbatim from source file) and **preserve** compressor flag + checksum + compressed blob; only recompress the edited entry. Align tests with that definition of "byte-identical."

4. **08-05 `ClientReloadDispatcher`:** Name the concrete scene-reload mechanism (grep TJT for existing setup-scene subscriber; document asset-class matrix in `TESTING.md` as acceptance output, not optional SUMMARY note).

5. **08-04:** Override `ProcessCmdKey` on `FormIffEditor`; set `ShortcutsEnabled = false` on hex/text boxes; route all undo through `IffEditController`. Commit payload to model on focus leave, not per keystroke.

6. **08-04 Task 4:** Move `IffEditController` to `UtinniCoreDotNet` (no WinForms) **or** add UtinniPlugins checkout to `ci.yml` for linked-source — prefer framework placement for Phases 9–11 reuse anyway.

7. **08-02:** Add one golden fixture that applies a deterministic mutation (edit one leaf via test hook) and asserts untouched byte ranges unchanged — stronger Criterion 4 gate.

8. **08-01 `IffWriter`:** Use `checked` arithmetic for container roll-up; throw `IffParseException`-analog on overflow before write.

9. **08-05 save pipeline:** `await` file write + optional `FileStream.Flush(true)` before queuing reload; disable Reload button until write task completes.

10. **Phase sign-off:** Treat PROD-W1-IFF Criterion 2 "client reloads correctly" as **tiered acceptance** (textures/terrain = pass; datatable/STF/template = scene-change or documented deferral) to avoid false "phase complete" on 08-07 Task 3.

---

## Risk Assessment

**Overall: HIGH**

Justification: The **write path + CLI round-trip (08-01/02)** and **file-save loop (08-05 modes 1/2)** are well de-risked and likely shippable. Phase completion hinges on three surfaces the plans under-specify: **(1) live patch without a memory-address discovery design and with shrink-patch corruption risk, (2) `.tre` repack whose implementation strategy contradicts its verification contract and may fail on locked archives, (3) D-06 reload that cannot honestly satisfy "reloads correctly" for general IFF assets without a concrete scene-reload implementation.** Tier-4 gates catch some of this late, but 08-06's missing provenance model is a **design hole**, not a smoke-test detail — it should be fixed in planning before execution, not discovered at Task 4 smoke.

---

## Consensus Summary

Both reviewers landed on **HIGH overall risk** with strong overlap on three "design hole" surfaces. The write path (08-01) + CLI gate (08-02) + low-risk file-save loop (08-05.1/.2) are well-de-risked and shippable as planned. The three high-risk save modes (D-05.3, D-05.4) and the forced-reload (D-06) need plan-level fixes before execute-phase.

### Agreed Strengths (both reviewers cited)

- **D-07 hybrid DOM** is the right fidelity mechanism — verbatim slices via `IffChunk.OffsetBytes`.
- **`roundtrip-iff` CLI verb (08-02)** is a sound max-harness gate for Criterion 4 on the no-mutation path.
- **D-05 plan-split** (08-06 / 08-07 isolated, sequenced through `depends_on`) avoids the obvious file-conflict landmine on `FormIffEditor.cs`.
- **Editor-local undo (D-08)** with explicit grep ban on `UndoRedoManager` / `AddUndoCommand` is the right CON-M-05 guard.
- **Tier-4 human gates** on 08-05 / 06 / 07 honestly reflect what CI cannot prove.

### Agreed Concerns (raised by both — highest priority)

| Severity | Concern | Plans |
|----------|---------|-------|
| **HIGH** | **TRE repack acceptance contradicts implementation.** Recompressing every entry via `GetRecordData` → re-Inflate cannot reproduce original on-disk compressed blobs, compressor flags, or offsets. Fix: TreWriter must copy raw compressed slices for untouched records (new API: `GetRecordCompressedBytes(index)` reading `rec.Offset`/`rec.CompressedSize` verbatim). | 08-07 |
| **HIGH** | **08-06 live patch has no source for `targetAddr` / `originalMappedLength`.** Nothing in 08-01..08-05 defines discovery/storage of mapped-memory address for an opened IFF; 08-05 Task 3 hands off `payload bytes + logical path` from `FormTreBrowser`, not a memory descriptor. Risk: feature ships permanently disabled while counting as a D-05 must-have. Fix: explicit `OpenSource` discriminated union (`LooseFile` / `TreArchive(path, recordIndex)` / `ClientMemory(addr, mappedLen)`) populated by 08-05 open path. | 08-06 (08-05 hand-off too) |
| **HIGH** | **Shrinking live-patch leaves stale tail bytes.** Refusing growth is good; copying fewer bytes than `originalMappedLength` is **not** automatically safe — old bytes after the new EOF remain in the mapped region. Fix: same-length-only for V1, OR zero-fill `[targetAddr+copyLen .. targetAddr+originalMappedLength)` on the game thread. | 08-06 |
| **HIGH** | **D-06 scene-change fallback unspecified.** No concrete trigger named (e.g. existing TJT setup-scene subscriber). Implementers will ship texture/terrain + tooltip-only fallback; datatables/STF/templates won't reload in-session — most of Wave 1's downstream phases depend on this. Fix: name the concrete scene-reload call site OR redefine D-06 as **tiered acceptance** (textures/terrain = pass; datatable/STF/template = scene-change or documented deferral). | 08-05 |
| **MEDIUM** | **`IffWriter` container length roll-up overflow.** Bottom-up `innerLen = 4 + Σ child sizes` with `MaxChunkSize` capped only at leaf payload. Hostile/deep trees can overflow `int` before the cap check. Fix: `checked` long arithmetic; validate against `int.MaxValue`/64 MB cap before writing headers. | 08-01 |
| **MEDIUM** | **Loose-override path safety underspecified.** Needs explicit normalization + root-containment after `Path.GetFullPath`. Fix: root-containment helper — normalize, reject rooted/`..`, `Path.GetFullPath`, verify `StartsWith(resolvedRoot)`. | 08-05 |

### Divergent / Unique Findings (single-reviewer, still worth investigating)

| Reviewer | Concern | Plan |
|----------|---------|------|
| **Cursor (UNIQUE — CI-critical)** | **CI will break on 08-04 Task 4 linked-source.** Path `..\..\UtinniPlugins\…\IffEditController.cs` assumes sibling checkout, but `ci.yml` only checks out Utinni (no UtinniPlugins step; only `release.yml` does). Fix: move `IffEditController` to `UtinniCoreDotNet` (no WinForms — it's pure-managed by design; Phases 9-11 reuse it anyway) OR add UtinniPlugins checkout to `ci.yml`. Cursor recommends framework placement. | 08-04 |
| **Cursor (UNIQUE)** | **08-07 / 08-05 repack provenance.** `TreRepackSaveTarget` needs source `.tre` path **and record index**; 08-05 Task 3 passes "logical path" only — insufficient when `TreArchiveIndex` resolves across master index + multiple archives. (Same root cause as the 08-06 provenance gap.) | 08-05 / 08-07 |
| **Cursor (UNIQUE)** | **Clean container with dirty descendant must explicitly invalidate captured slices** — currently implicit via dirty propagation; a bug there silently re-emits stale container bytes with wrong child lengths. | 08-01 |
| **Cursor (UNIQUE)** | **08-02 CLI gate covers identity-only round-trip.** No CLI golden for "edit fixture → write → byte-exact on untouched subtrees" — regression in editor-only structural paths won't surface in `Utinni.Cli.Tests`. | 08-02 |
| **Cursor (UNIQUE)** | **Ctrl+Z / TextBox undo collision.** Editable `txtHex` is a standard `TextBox` with built-in edit undo; plan does not override `ProcessCmdKey` on `FormIffEditor` or disable `TextBox.ShortcutsEnabled`. Fix: override `ProcessCmdKey`; set `ShortcutsEnabled = false`; commit payload to model on focus-leave, not per-keystroke. | 08-04 |
| **Cursor (UNIQUE)** | **Save → reload race.** File save on `Task.Run` then immediate `ClientReloadDispatcher` on game thread has no flush/fsync barrier — client may re-read stale bytes from OS cache. Fix: `await` write + `FileStream.Flush(true)` before queuing reload; disable Reload button until write completes. | 08-05 |
| **Cursor (UNIQUE)** | **Loose-override dir still a guess until Tier-4.** `IffSaveTargets` derives base from `ResolveClientTreDir` + ini fallback — wrong subdir = silent "saved but never loaded." Open Q2 is "resolved" only by maintainer smoke, not by code research in-plan. | 08-05 |
| **Cursor (UNIQUE)** | **Cross-repo build coupling not pinned.** No plan step pins/framework-builds both repos in one commit. Uncommitted `Generated/UtinniCore.cs` in the working tree adds binding drift risk for `Memory.memory.Copy` line citations. | cross |
| **Codex (UNIQUE)** | **Source-slice + standards-compliant padded IFF.** Reader detects pad handling; if a non-SWG padded IFF is loaded, the captured slice may exclude pad bytes while the writer emits no pad — byte-exact round-trip for non-SWG padded fixtures may leak unmodeled bytes. | 08-01 |
| **Codex (UNIQUE)** | **TRE TOC invariants beyond CRC.** Preserving `TreRecord.Checksum` is necessary but not sufficient — compression flags, name-block offsets, TOC ordering, version-specific record stride, duplicate CRC collision ordering, alignment can all still break the client. | 08-07 |
| **Codex (UNIQUE)** | **Undo doesn't clear dirty.** Inverse-command undo without baseline comparison or command-count tracking can leave false dirty markers and bad save prompts. | 08-04 |
| **Codex (UNIQUE)** | **Smoke recipe.** Tier-4 smoke "edit one structural op then save loose override" conflates writer correctness with game-asset validity. Smoke needs a known-safe fixture/edit recipe. | 08-05 |
| **Codex (UNIQUE)** | **Backup naming overwrites previous backups.** `<name>.tre.bak` is not enough for high-blast-radius archive writes. Use timestamped or non-overwriting backups. | 08-07 |
| **Codex (UNIQUE)** | **Hex textbox usability trap.** Inverting a formatted hex+ASCII dump requires strict input format; users can edit the ASCII column and expect bytes to change. Plan doesn't specify accepted input shape. | 08-04 |

### Recommended Disposition

Both reviewers explicitly stated the four HIGH design-hole items (TRE byte-identity, live-patch provenance, shrink-patch corruption, D-06 fallback) should be **fixed at the plan level before execute-phase**, not discovered at Tier-4 smoke. The medium-severity items (overflow, path-traversal) are also worth folding into the existing threat models.

Recommended next step:

```text
/gsd:plan-phase 8 --reviews
```

The planner will read this REVIEWS.md and produce a surgical revision targeting the agreed-HIGH items first. Suggested revision priorities:

1. **08-07 Task 1** — rewrite the byte-identity acceptance: (a) logical-payload identity for untouched entries (`GetRecordData` equality) AND (b) raw-compressed-slice copy from `rec.Offset` for untouched records (add `TreFile.GetRecordCompressedBytes(index)`). Drop the misleading "archive bytes identical" claim. Also: TOC invariant test (checksum, name-offset, compression flag, uncompressed size, payload bytes).
2. **08-05/06 (provenance)** — add explicit `OpenSource` discriminated union populated by 08-05's open path: `LooseFile(path)` | `TreArchive(path, recordIndex, logicalPath)` | `ClientMemory(addr, mappedLen)`. Live patch gated on `ClientMemory`; repack gated on `TreArchive`.
3. **08-06 (shrink)** — lock V1 to **same-length-only** patches OR specify zero-fill `[copyLen .. originalMappedLength)` on the game thread.
4. **08-05 Task 2** — name the concrete scene-reload call site OR redefine D-06 as **tiered acceptance** in PROD-W1-IFF Criterion 2 (textures/terrain = pass; datatable/STF/template = scene-change or documented deferral).
5. **08-04 Task 4** — move `IffEditController` to `UtinniCoreDotNet` (it's pure-managed; Phases 9-11 reuse it; eliminates CI break, no `ci.yml` change needed).
6. **08-04 Task 3** — override `ProcessCmdKey` on `FormIffEditor`; set `ShortcutsEnabled = false` on hex/text boxes; commit payload to model on focus-leave.
7. **08-01 Task 2** — `checked` long arithmetic for container roll-up; ancestor-invalidation of captured slices on descendant-dirty; mutable payload getters return copies/spans (prevent dirty bypass).
8. **08-05 Task 1** — root-containment helper for loose-override path (normalize + reject `..` + `Path.GetFullPath` + `StartsWith` check).
9. **08-05 save pipeline** — `await` write + `FileStream.Flush(true)` before reload; disable Reload until write completes.
10. **08-07 Task 2** — timestamped backups (avoid `.tre.bak` overwrite); specify "close handle / copy loose override instead" fallback when client holds the archive open.

---

*Generated 2026-05-28 by /gsd:review --phase 8 --all. Reviewers: codex (`gpt-5.5` via `codex exec --skip-git-repo-check`), cursor (default model via `cursor-agent.cmd -p --mode ask --trust`). Self-reviewer claude skipped per CLAUDE_CODE_ENTRYPOINT=cli.*
