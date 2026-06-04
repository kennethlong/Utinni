# Phase 13: Wrap revived compilers as CLI verbs + close OT Tier-2 - Context

**Gathered:** 2026-06-03
**Status:** Ready for planning

<domain>
## Phase Boundary

Turn the Phase-12 revived native tools (plus the managed UtinniCoreDotNet writers) into golden-tested `utinni-cli` BUILD/SAVE/schema verbs that Phase 14's MCP server wraps thinly. Delivers AUTH-02..06 + RESID-01. Coexistence-by-verb-ownership is preserved: BUILD-from-source uses the revived native compilers (`compile-*`/`build-*`); EDIT-existing-binary uses the byte-exact managed writers (`roundtrip-*`/the new `save`).

**In scope:** CLI verbs wrapping `TemplateCompiler` (AUTH-03), `TreeFileBuilder` (AUTH-04), `TemplateDefinitionCompiler` → param→type schema (AUTH-02); OT Editor typed-display close (RESID-01); a net-new `save` verb (AUTH-05); datatable compile + item exporters via lifted natives (AUTH-06).
**Out of scope:** the MCP server itself (Phase 14); Wave-2 editors (Phase 15); window/presentation residuals incl. RESID-04 (Phase 15).
</domain>

<decisions>
## Implementation Decisions

### AUTH-06 — datatable compile + item exporters
- **D-01:** **Lift all 3 natives** (`DataTableTool`, `ArmorExporterTool`, `CoreWeaponExporterTool`) into `tools/` and wrap as BUILD verbs — strict coexistence-by-verb-ownership, byte-exact to SOE. These exist in swg-client-v2 `src/compile/win32/` but were NOT lifted in Phase 12 (only the 3 build CLIs were), so this is a mini-Phase-12 lift (dependency closure + v145 build + the SAFESEH/CRT/C++20 delta pattern) on top of the verb-wrapping.
- **D-02 (escape-hatch):** The client-only build CLIs lifted cleanly in Phase 12; the item exporters are riskier (may pull swgServer-side closures). Commit to lifting all 3, BUT if research finds an exporter's closure turns it into a multi-day lift, THAT ONE tool falls back — managed `DataTableWriter` for `DataTableTool`, or defer that exporter as a tracked residual — rather than blocking the phase. **Research MUST scope each tool's dependency closure first** (per-tool, before committing the lift). Phase-12's v143 fallback (build the troublesome tool at v143 in `tools/`; subprocess seam is toolset-agnostic) is available but secondary to the managed/defer escape-hatch.

### Golden-test contract for BUILD verbs
- **D-03:** With no SOE source→known-good reference pairs (Phase-12 finding), correctness gate = **native-vs-managed cross-check + committed regression goldens**. Where a managed twin exists, use it as an independent oracle: native `compile-datatable` output byte-compared against managed `DataTableWriter`; native `compile-template` `.iff` parsed by the managed OT reader and asserted typed-correct. Commit native outputs as regression goldens.
- **D-04:** **When native and managed disagree, native output is SOE-authoritative** (it IS the SOE tool) — a mismatch files a managed-reader bug, it does NOT stall the phase.
- **D-05 (real reference data — user-contributed):** The live SWGEmu client at `D:\SWGEmu-Client\SWGEmu\` ships **53 real pre-CU `.tre` archives (2003/2006, 0005/0006 format)** — the format our tools target, already enumerable by the Phase-7 reader (125k paths). These are **authentic SOE-authored `.iff` outputs** (OT, datatable, etc.) usable as the cross-check oracle's real inputs — graduating Area-2 from self-authored fixtures to real SOE data. NOTE: this is a *different corpus* from Phase-12's gate-finding (which was about the v6000/encrypted retail corpus) — the 0005/0006 pre-CU corpus is usable. Client `.tre` hold compiled `.iff` only (no `.tpf`/`.tab` source — those live server-side in `dsrc`).

### build-tre byte-exact (AUTH-04) — `.rsp` synthesis
- **D-06:** `TreeFileBuilder` requires an `.rsp` response file (`<output.tre> -r <rsp> [-f]`); the `.rsp` is a text recipe of `diskPath@treePath` lines in pack order (plus a secondary `TF::open(...)` log-parse mode). An authoritative SOE `.rsp` is a server-side data-build artifact — not in the client or swg-client-v2 (the 591 `.rsp` there are MSVC compiler files), realistically not findable. **Instead, SYNTHESIZE the `.rsp` from a real `.tre`** via the Phase-7 reader (recovers logical paths + order + per-file compression), extract entries to disk, emit the `.rsp`, rebuild with `build-tre`, byte-compare to the original. Phase-12 pinned **zlib 1.1.4** (the original compressor), so compression is determinism-matched. This reframes Phase-12's "byte-exact blocked by missing `.rsp`" finding: the `.rsp` is reconstructable. Byte-exact success still hinges on TOC layout + compression determinism — treat as a testable hypothesis, fall back to structural compare if byte-identity proves unreachable.

### OT Tier-2 typed display (RESID-01)
- **D-07:** Depth = **structured typed editors for the common list/struct params modders actually edit** (draft-schematic slots, attributes, hair); the exotic/rare multi-chunk tail degrades gracefully to a **typed LABEL (from schema) + hex value**. Closes RESID-01 for the 80/20 without chasing the long tail. (Scalars are already typed in the Phase-11 editor; the residual is the ~17% multi-chunk params currently shown as raw hex — see `project_ot_multichunk_list_params`.)
- **D-08:** Schema delivery = **static generated artifact**. The `compile-definition` verb (AUTH-02) runs `TemplateDefinitionCompiler` once and emits a per-class param→type schema file (JSON, committed/cached). The OT editor loads that artifact at runtime — zero native-tool dependency on the editor open path, deterministic, diffable. The SAME schema artifact feeds the Area-2 cross-check tests (one schema, two consumers). Regenerate when the `.tdf`/`.tpd` changes. (Fits Phase-11's existing off-thread background-resolve editor pattern.)

### SAVE verb (AUTH-05)
- **D-09:** `save` covers **all 4 formats** (IFF / datatable / stf / OT) by wrapping the existing managed save targets (Phase-8 `IffSaveTargets`, Phase-9 `DatatableSaveTargets`, Phase-10 StringTable, Phase-11 `ObjectTemplateSaveTargets`) — cheap since all four already exist. Loose-override default + explicit `--path`. Result envelope is the ROADMAP-locked `{written, path, bytesWritten, backupPath, validated}`.
- **D-10:** The destructive `.tre` **repack is NOT reachable through `save`** — it's its own explicit verb (repack already exists as `TreRepackSaveTarget`, routed through `TreBackupPath`). Keeps the default write surface safe-by-construction; Phase-14 MCP wraps repack as its own off-by-default `dry_run`-gated tool.

### Locked by ROADMAP / prior phases (carried forward — not re-discussed)
- Verb naming: `compile-*`/`build-*` (BUILD, native) vs `roundtrip-*`/`save` (EDIT, managed). Every capability is a CLI verb FIRST (golden-tested) so Phase-14 MCP stays a thin dispatcher with zero business logic.
- Lift-and-shift constraint: build in the Utinni-owned `tools/` tree, NEVER in swg-client-v2.
- Reuse the existing `Utinni.Cli` CommandLineParser verb harness + `JsonOutput` (sorted-key) + the DEC-C3 Tier-2 golden-fixture pattern.

### Claude's Discretion
- Exact verb names within the locked `compile-*`/`build-*` convention (e.g., `compile-template`, `build-tre`, `compile-definition`, `compile-datatable`, exporter verb names) — planner/researcher choose, consistent with existing verbs.
- CSV-vs-XML (or both) as the datatable/exporter source input format — resolve in research against what the lifted `DataTableTool`/exporters actually accept (their native input format wins, since D-01 lifts the natives).
- Subprocess invocation mechanics for wrapping native exes (the BUILD verbs shell out to the `tools/` `.exe`s; mirrors the Phase-14 MCP→`utinni-cli` seam one layer down).
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + requirements
- `.planning/ROADMAP.md` §"Phase 13" (lines ~291-303) — goal, the 5 success criteria, constraint guard-rails (verb naming, SAVE envelope, coexistence-by-verb-ownership).
- `.planning/REQUIREMENTS.md` — AUTH-02, AUTH-03, AUTH-04, AUTH-05, AUTH-06, RESID-01 statements + acceptance.

### Phase-12 lift pattern (reused for the AUTH-06 native lifts)
- `tools/DEPENDENCY-MANIFEST.md` — per-tool dependency closures + revival deltas (engine-API drift, C++20, SAFESEH, CRT-compat) + zlib 1.1.4 pin + Perforce keep-link.
- `tools/PINNED-SHA.md` — swg-client-v2 lift SHA (`@5fce7bb8`).
- `tools/Directory.Build.props` — the load-bearing standalone-build shim (include-path redirect + P4 CRT libs).
- `.planning/phases/12-revive-feasibility-spike-hard-gate-intro-skip-crash/12-03-SUMMARY.md` — byte-exact gate-findings (the v6000 corpus story D-05 supersedes for the pre-CU case).

### Lift sources (AUTH-06 natives — scope dependency closures FIRST per D-02)
- `D:/Code/swg-client-v2/src/compile/win32/DataTableTool/` — native datatable compiler.
- `D:/Code/swg-client-v2/src/compile/win32/ArmorExporterTool/` — armor item exporter.
- `D:/Code/swg-client-v2/src/compile/win32/CoreWeaponExporterTool/` — weapon item exporter (NOTE: actual name is `CoreWeaponExporterTool`, not the roadmap's `WeaponExporterTool`).

### TreeFileBuilder .rsp (AUTH-04 byte-exact, D-06)
- `tools/src/engine/shared/application/TreeFileBuilder/src/shared/TreeFileBuilder.cpp` — `addResponseFile` (`diskPath@treePath` line format, `TF::open` log mode), `-r`/`-f` CLI.

### Real reference data (D-05)
- `D:/SWGEmu-Client/SWGEmu/*.tre` — 53 pre-CU 0005/0006 archives (the cross-check oracle's real input corpus).

### Existing CLI + managed assets to reuse
- `Utinni.Cli/Program.cs` + `Utinni.Cli/Commands/*` — the CommandLineParser verb-dispatch pattern (9 existing read/edit verbs) + `Output/JsonOutput.cs` (sorted-key contract).
- `UtinniCoreDotNet/Formats/Datatable/{DataTableWriter,CsvCellCoercion}.cs` — managed datatable compile/oracle (Phase 9).
- Managed save targets: Phase-8 `IffSaveTargets`, Phase-9 `DatatableSaveTargets`, Phase-10 StringTable, Phase-11 `ObjectTemplateSaveTargets`, `TreRepackSaveTarget`/`TreBackupPath` (Phase 8).
- OT editor + codec: `UtinniCoreDotNet/Formats/ObjectTemplate/*` (Phase 11) + `FormObjectTemplateEditor` (UtinniPlugins) — RESID-01 typed-display target.

### Memories
- `project_ot_multichunk_list_params` (RESID-01 — the raw-fallback ~17% this phase types), `project_auth01_revive_build_track` (Phase-12 lift mechanics), `project_swg_client_v2_reference` (lift-source rules), `project_tre_version_support_gap` (0004/0005/0006 vs v6000), `project_self_hosted_ci` (CI runner for the new native build-lane), `project_gsd_worktrees_off` (run C++ build waves inline).
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`Utinni.Cli` verb harness** — CommandLineParser `MapResult` dispatch (`Program.cs`) + per-verb `Commands/*Command.cs` + `Run(options)` + `JsonOutput` sorted-key envelope. New verbs slot in as additional `*Options`/`*Command` pairs.
- **Managed `DataTableWriter` + `CsvCellCoercion`** — already produce datatable `.iff` from CSV (Phase 9), byte-validated by `roundtrip-tab` goldens; serve as the AUTH-06 cross-check oracle (D-03) and the escape-hatch fallback (D-02).
- **Managed save targets (all 4 formats)** — `save` verb (D-09) is mostly thin wrapping of these existing targets; the repack/backup primitives (`TreRepackSaveTarget`, `TreBackupPath`) back the separate repack verb (D-10).
- **Phase-7 TRE reader** — recovers logical paths + order + per-file compression; the engine of the `.rsp`-synthesis byte-exact path (D-06) and the real-corpus extraction (D-05).
- **Phase-11 OT codec + editor** — typed scalar display already shipped; RESID-01 (D-07/D-08) extends it with schema-driven structured list/struct widgets.

### Established Patterns
- **Coexistence-by-verb-ownership** — BUILD=native, EDIT=managed; the verb name is the LLM's routing signal. New BUILD verbs must wrap the `tools/` natives, not re-implement in managed.
- **Phase-12 native-lift pattern** — `git archive` from the pinned SHA + dependency-closure vcxprojs + `Directory.Build.props` shim + per-tool revival deltas; the standalone `tools/Utinni.Tools.sln` CI build-lane (self-hosted v145). AUTH-06's lifts follow this exactly.
- **DEC-C3 Tier-2 golden harness** — `Utinni.Cli.Tests` golden fixtures; new verbs add fixtures here.

### Integration Points
- New native tools join `tools/Utinni.Tools.sln` + the CI AUTH-01 build-lane (extend the hard-gate to the 3 new exes).
- BUILD verbs in `Utinni.Cli` `Process.Start` the `tools/` exes; SAVE/schema verbs call into `UtinniCoreDotNet`.
- The `compile-definition` static schema artifact is consumed by BOTH `FormObjectTemplateEditor` (runtime typed display) and the Area-2 cross-check tests.
</code_context>

<specifics>
## Specific Ideas

- **User-contributed research threads (HIGH value — treat as primary seeds):**
  1. The live SWGEmu client (`D:\SWGEmu-Client\SWGEmu\`) is a **real pre-CU 0005/0006 asset corpus** for cross-check reference data (D-05).
  2. **`.rsp` synthesis** from a real `.tre` via the Phase-7 reader makes `build-tre` byte-exact a testable hypothesis (D-06).
- The escape-hatch (D-02) is explicit user intent: ambition (lift all 3) tempered by a no-schedule-blow-up guard. Research's dependency-closure scoping per exporter is the gating step that decides lift-vs-fallback.
</specifics>

<deferred>
## Deferred Ideas

None raised that belong to other phases — discussion stayed within Phase-13 scope. (RESID-04 window edge-cases were already routed to Phase 15 during the Phase-12 closeout.)

### Reviewed Todos (not folded)
The `todo.match-phase 13` query surfaced 5 weak keyword matches (score 0.4–0.6), NONE in Phase-13's CLI-verb scope — all reviewed and left for their proper homes:
- `gamecallbacks-gc-av-flake-fix.md` (CI-stability flake) — not CLI scope.
- `loader-lock-harness-flake-fix.md` (CI-stability flake) — not CLI scope.
- `phase09-datatable-editor-review-warnings.md` (Phase-9 editor code-quality) — editor-side, not CLI.
- `phase10-stringtable-sc3-live-reload-residual.md` (live-reload candor) — editor reload path; roadmapped near Phase-15 (RESID-03).
- `swg-window-resize-fullscreen-edge-cases.md` (RESID-04) — already deferred to Phase 15.
</deferred>

---

*Phase: 13-Wrap revived compilers as CLI verbs + close OT Tier-2*
*Context gathered: 2026-06-03*
