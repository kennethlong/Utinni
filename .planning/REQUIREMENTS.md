# Requirements: Utinni

**Defined:** 2026-05-16
**Core Value:** A modder downloads Utinni, installs once, and from a single application can see, edit, and live-preview every asset the SWG client loads — replacing the fragmented 15-year-old editor zoo with one stable, plugin-driven tool.

Requirements derived from the 2026-05-16 ingest of `docs/ai/vision.md`, `docs/ai/assessment.md`, and `docs/ai/test-harness-plan.md`. See `.planning/intel/requirements.md` for source attribution. The product-capability requirements were prose-only in the source DOCs (no formal AC), so V1 acceptance criteria are derived from the user-supplied "Demo + CI green" success metric.

## v1 Requirements

### Framework Stability (from assessment.md)

- [ ] **STAB-01 — Fix 15 critical bugs (C-01..C-15)**
  - **Source:** assessment.md "Critical issues"
  - **Statement:** All 15 enumerated critical issues — C-01 (DllMain loader-lock), C-02 (cross-CRT delete[]), C-03 (Network::cast uninitialized), C-04 (post-draw queue broken since 2020), C-05 (drag-drop static field), C-06 (PluginLoader exception swallowing), C-07 (UndoRedoManager thread-safety + dead AllowMerge), C-08 (Hotkey.ProcessString throws on bad input.ini), C-09 (UI/game-thread busy-wait), C-10 (clr::stop null deref), C-11 (DirectX findPattern null check), C-12 (VSIX VS 2019 pin), C-13 (TJT Debug path), C-14 (utinni.cfg login default), C-15 (CppSharp slnDir brittle) — must be closed.
  - **Acceptance:** Every C-NN item shows `done` in assessment.md status table; framework no longer exhibits the listed silent failures, crashes, or data losses. Class-of-bug constraints CON-H-01..-05, CON-L-01..-04, CON-B-04, CON-D-01 are honoured going forward.
  - **Milestone:** V1

- [ ] **STAB-02 — Land 8 strategic reworks (R-A..R-H)**
  - **Source:** assessment.md "Strategic reworks"
  - **Statement:** R-A (symmetric callback Add/Remove on every callback), R-B (plugin lifecycle contract: symmetric `createPlugin`/`destroyPlugin`, `init()` actually called, LoadLibrary failures logged), R-C (single-source-of-truth RVAs exposed via `UTINNI_API`), R-D (CI build workflow on GitHub Actions Windows runner), R-E (`[CallerMemberName]` logging), R-F (CppSharp header auto-discovery), R-G (idempotent `Directory.Build.props` wizard), R-H (snapshot-iteration over callback collections).
  - **Acceptance:** All 8 R-X items show `done`; plugin authoring is "genuinely pleasant" per assessment.md; CI catches regressions on every PR.
  - **Milestone:** V1

- [x] **STAB-03 — Complete ~30 cleanups + dependency bumps**
  - **Source:** assessment.md "Easy cleanups"
  - **Statement:** ~250 lines of dead code deleted; typo fixes (`detatch→detach`, `AddOuputSink→AddOutputSink`, `redose→redoes`, `Jo�o→João`); DetourXS + nvapi added to licenses.txt; `.clang-format` pass; unified Windows SDK target version (CON-B-02); DXSDK include paths fixed for Debug/Release (CON-B-03); `Prefer32Bit` removed; ExampleEditorPlugin Release output path fixed; `.gitignore` Std/StdEdited convention documented; `TJT.ico` removed from framework; `Native.SendMessage` `int→IntPtr`. Dependency bumps: imgui to docking branch, spdlog 1.14, ImGuizmo latest.
  - **Acceptance:** Codebase audit confirms "modern toolchain, no dead code"; all enumerated cleanup items reflected in commits.
  - **Milestone:** V1

- [x] **STAB-04 — Preserve 24 load-bearing foundations**
  - **Source:** assessment.md "Solid foundations"; `.planning/intel/constraints.md` families CON-N-* / CON-M-* / CON-T-*
  - **Statement:** Negative requirement. The 24 enumerated load-bearing design elements (9 native, 9 managed, 5 process/tooling) must remain intact through V1. Any phase plan touching a preserved item requires explicit justification.
  - **Acceptance:** Post-V1 audit confirms each of CON-N-01..-09, CON-M-01..-09, CON-T-01..-05 is still in place; deviations are documented with phase-plan justification.
  - **Milestone:** V1 (cross-cutting; verified at every phase boundary)

- [ ] **STAB-05 — Resolve 8 open questions from assessment.md (CON-O-01..CON-O-08)**
  - **Source:** assessment.md "Open questions for project history"
  - **Statement:** Eight questions need a someone-who-was-there answer or a documented "deferred" disposition: CON-O-01 `isSafeToUse` operator, CON-O-02 `AddPostDrawLoopCall` usage, CON-O-03 native delegate corruption smell (likely missing `GCHandle.Alloc`), CON-O-04 VS 2019 pin rationale, CON-O-05 `StdEdited.cs` curation criteria, CON-O-06 LeksysINI replacement plan, CON-O-07 Sytner's plugin status, CON-O-08 DXSDK vs Windows 10 SDK replaceability.
  - **Acceptance:** Each question carries either a documented answer (committed in `docs/ai/`) or an explicit "deferred to V2 with reason" note. Some questions unblock specific bug fixes (CON-O-01 ↔ C-04 area, CON-O-04 ↔ C-12, CON-O-08 ↔ DXSDK modernisation in STAB-03).
  - **Milestone:** V1

### Test Infrastructure (from test-harness-plan.md)

- [ ] **TEST-01 — Tier 1 C# unit-test scaffold**
  - **Source:** test-harness-plan.md "Tier 1"
  - **Statement:** Stand up a C# xUnit (or NUnit) test project alongside Utinni's main solution. `dotnet test` runs in CI (GitHub Actions Windows runner) and locally without a game client. First targets: TRE/IFF parsers, plugin manifest loader, settings serialization/migration, math helpers, data-model logic.
  - **Acceptance:** Test project compiles in CI; at least 2–3 file-format parsers have non-trivial coverage; CI status badge green on master.
  - **Milestone:** V1

- [ ] **TEST-02 — Tier 1 C++ unit-test scaffold**
  - **Source:** test-harness-plan.md "Tier 1"
  - **Statement:** Catch2 (header-only amalgamated) wired through MSBuild + direct exe invocation for UtinniCore per Phase 5 D-03 (no CMake, no package manager). Folds in once UtinniCore has the refactored seams produced by STAB-02 (R-A..R-H).
  - **Acceptance:** Catch2 builds in CI under all three native configs (Debug + Release + RelWithDbgInfo); at least one native parser or helper has coverage; CI runs both `dotnet test` and the native test exe `UtinniCore.Tests.exe` (Catch2 self-runner per Phase 5 D-03) and is green on master.
  - **Milestone:** V1

- [x] **TEST-03 — Tier 2 CLI shim with golden fixtures** (Validated in Phase 4, 2026-05-23)
  - **Source:** test-harness-plan.md "Tier 2"
  - **Statement:** A `utinni-cli` executable in the same solution, referencing the same core libraries as the WinForms tool, exposing the operations the UI calls. Commands include: `parse-tre`, `list-objects`, `validate-plugin`, `inspect-iff`. Paired with golden-file tests against checked-in fixtures. Resolves CON-O-09 (fixture storage) and CON-O-10 (test project layout) and CON-O-11 (CLI public vs internal) as part of this requirement.
  - **Acceptance:** CLI builds in CI; at least one command per surface (`parse-tre`, `list-objects`, `validate-plugin`, `inspect-iff`) has a golden test; CLI converts an estimated 60–70% of manual "Kenny please verify" loops into unattended runs; UI becomes one of two consumers of the core (not the sole consumer).
  - **Milestone:** V1
  - **Evidence:** `utinni-cli.exe` ships 4 verbs with stable sorted-key JSON envelopes; 50 Utinni.Cli.Tests + new Tier-1 parser tests (25 TRE + IFF) green; `.github/workflows/ci.yml` runs both test lanes per push; CON-O-09 (fixture storage = in-repo synth, no LFS) + CON-O-11 (CLI public vs internal) dispositioned in `docs/ai/assessment.md`; DEC-C3 (tiered testing strategy) promoted to LOCKED in PROJECT.md.

- [ ] **TEST-04 — Explicit Tier 4 boundary documented**
  - **Source:** test-harness-plan.md "Tier 4"
  - **Statement:** Document the explicit residual that still requires maintainer-in-the-loop after V1: real-`SWG.exe`-injection smoke run, visual "does it look right" judgment, GPU-driver-specific bugs, WinForms UI smoke (FlaUI deliberately skipped — CON-TT-03). The residual is scoped and bounded.
  - **Acceptance:** Tier 4 boundary documented in `.planning/codebase/TESTING.md` or a sibling file; documentation referenced from CONVENTIONS.md; V1 ships with the documented residual rather than attempting to automate it.
  - **Milestone:** V1

### Product Capability — Wave 1 Plugins (from vision.md)

- [ ] **PROD-01 — See everything the client loads**
  - **Source:** vision.md "The goal"
  - **Statement:** From inside Utinni, a user can browse every `.tre`, every IFF, every datatable, every template, every UI page, every shader, and every string-table entry the running client can load. Powered by the existing `treefile::getAllFilenames` hook.
  - **Acceptance (V1 scope):** TRE Browser plugin (PROD-W1-TRE) demos end-to-end against a live SWG client, walking the full `.tre` mount set and surfacing each IFF, datatable, template, UI page, shader, and string-table entry reachable through the client.
  - **Milestone:** V1 (covered through PROD-W1-TRE)

- [ ] **PROD-02 — Edit major asset types (Wave 1 subset)**
  - **Source:** vision.md "The goal" + "Wave 1 — round out what we have"
  - **Statement:** Utinni provides editors for the Wave-1 asset types: IFF (read+write), datatable (`.tab`), string-table (`.stf`), and object template. (Full "major asset types" — conversations, quests, particles, UI pages, shader uniforms, world snapshots — is V2+; only the Wave-1 four are V1.)
  - **Acceptance (V1 scope):** Wave-1 IFF Editor (PROD-W1-IFF), Datatable Editor (PROD-W1-DT), String-table Editor (PROD-W1-STF), and Object Template Editor (PROD-W1-OT) each demo end-to-end against a live SWG client with both view and edit operations.
  - **Milestone:** V1 (covered through PROD-W1-IFF/DT/STF/OT)

- [ ] **PROD-W1-TRE — Wave-1 plugin: TRE Browser (read-only)**
  - **Source:** vision.md "Wave 1"
  - **Statement:** Read-only browser over `.tre` virtual filesystem. Replaces SOE-era `TreeFileExtractor`. Surfaces the asset graph the client loads (per PROD-01).
  - **Acceptance:** Plugin loads in editor host against a live SWG client; user can navigate the `.tre` mount set, expand subtrees, and view individual file metadata. CLI shim (TEST-03) covers `parse-tre` and `list-objects` with golden fixtures.
  - **Milestone:** V1

- [ ] **PROD-W1-IFF — Wave-1 plugin: IFF Editor (read + write)**
  - **Source:** vision.md "Wave 1"
  - **Statement:** Read and write IFF chunks across the client's IFF surface. Replaces SOE-era `IFFEditor`. Foundational plugin — most other Wave-1 plugins layer on IFF read/write.
  - **Acceptance:** Plugin loads in editor host; user can open an IFF file, view chunk hierarchy, edit chunk content, save modifications back to a file the live client reloads correctly. CLI shim covers `inspect-iff` with golden fixtures.
  - **Milestone:** V1

- [ ] **PROD-W1-DT — Wave-1 plugin: Datatable Editor (`.tab`)**
  - **Source:** vision.md "Wave 1"
  - **Statement:** View and edit `.tab` datatables (the client's tabular data format). Replaces SOE-era `SwgDataTableTool`.
  - **Acceptance:** Plugin loads in editor host; user can open a `.tab` file, view rows/columns, edit cell values, save back; live SWG client picks up the edit on the relevant reload path.
  - **Milestone:** V1

- [ ] **PROD-W1-STF — Wave-1 plugin: String-table Editor (`.stf`)**
  - **Source:** vision.md "Wave 1"
  - **Statement:** View and edit `.stf` string tables (localised text). Replaces SOE-era `SwgStringEditor`.
  - **Acceptance:** Plugin loads in editor host; user can open a `.stf` file, view string entries, edit text, save back; live SWG client renders edited strings on reload.
  - **Milestone:** V1

- [ ] **PROD-W1-OT — Wave-1 plugin: Object Template Editor**
  - **Source:** vision.md "Wave 1"
  - **Statement:** Edit object templates (the `.iff`-based template hierarchy that drives in-world object behaviour and appearance).
  - **Acceptance:** Plugin loads in editor host; user can open an object template, view inherited fields, edit overrideable fields, save back; live SWG client reflects the edit when the object respawns or reloads.
  - **Milestone:** V1

## v2.0 Requirements — "AI-Assisted SWG Tools" (ACTIVE)

Theme: *Utinni authors, not just edits.* Ship an MCP server so AI agents can drive the full SWG asset pipeline; revive+wrap the SOE build-chain compilers (lift-and-shift, shared v145 target); land the first Wave-2 editors. Sourced from `.planning/research/SUMMARY.md` + `docs/ai/toolchain-inventory.md`. Scope: **full slate** (maintainer 2026-06-01).

### AI / MCP interface

- [x] **MCP-01**: An AI agent can READ any supported SWG asset (TRE/IFF/datatable/`.tab`/`.stf`/object-template) through a headless `Utinni.Mcp` server — a separate modern-.NET (net10) process, stdio transport, wrapping the existing `utinni-cli` JSON verbs.
- [x] **MCP-02**: An AI agent can EDIT + SAVE assets via MCP write tools that default to the loose-override tier, with byte-exact verify-before-commit, a `dry_run` gate on destructive repack, and a `resolvedRoot` pinned fail-closed at server start — documented in an `MCP-SECURITY.md` threat register. No agent write can corrupt a source archive or escape the resolved root.
- [x] **MCP-03**: An AI agent can drive the LIVE-injected client over an MCP bridge (named-pipe IPC into the x86 host) to preview an edit in-client. *(Ambitious; sequenced last.)*

### Authoring & compile pipeline (revive + wrap; lift-and-shift)

- [ ] **AUTH-01**: A revive-feasibility spike confirms `TreeFileBuilder` and `TemplateCompiler` / `TemplateDefinitionCompiler` build + link STANDALONE at v145 (lift-shifted into a Utinni-owned `tools/` tree, dead Perforce/transitive deps stripped), producing a per-tool dependency manifest and a pinned `swg-client-v2` source SHA. *(Hard gate for the rest of AUTH.)*
- [ ] **AUTH-02**: `TemplateDefinitionCompiler` is revived and its `.tdf`/`.tpd` → per-class param→type schema drives the Object Template Editor's typed list-param display (closes RESID-01).
- [ ] **AUTH-03**: A modder can compile a `.tpf` object-template source to a byte-correct object-template `.iff` via a `utinni-cli` verb wrapping the revived `TemplateCompiler`.
- [ ] **AUTH-04**: A modder can build a `.tre` archive from a source tree via a `utinni-cli` verb wrapping the revived `TreeFileBuilder`.
- [x] **AUTH-05**: `utinni-cli` gains a SAVE verb that writes an edited asset (loose-override or repack) — the net-new write surface MCP-02 wraps (today the CLI is read + roundtrip only).
- [ ] **AUTH-06**: A modder can compile a datatable from CSV/XML source and run the item exporters (`ArmorExporterTool`/`WeaponExporterTool`) via `utinni-cli` verbs.

### Product Capability — Wave-2 editors

- [x] **PROD-W2-WS**: A modder can view + edit object placements in a world snapshot via a Utinni SubPanel (extends the existing Snapshot panel; reuses shipped codecs — zero new format work).
- [x] **PROD-W2-PRT**: A modder can open + edit a particle / client-effect asset in a Utinni SubPanel, with live in-client preview when injected. *(The flashy AI-assist showcase; Terrain `PROD-W2-TRN` deferred to v2.1.)* — **DELIVERED + Validated 2026-06-13:** headless `.prt`/FORM PEFT codec (15-02, byte-exact + degrade-don't-abort) + FormParticleEditor SubPanel (emitter tree / typed grid / hex / AI Explain-effect) + honest live-capable-vs-degraded Preview candor reachable by hover+click (B6 closed 15-19); live preview hook itself remains honestly degraded (no reachable native hot-retrigger this build, per the 15-03 spike).

### Ecosystem

- [x] **ECO-01**: The Utinni ↔ `swg-blender-plugin` boundary is formalized as a documented file-format / `.rsp` search-path contract — Utinni opens/previews what Blender exports; no runtime coupling (honors DEC-A3, the no-3D-authoring anti-goal). *(Validated Phase 16: docs/ai/blender-boundary-contract.md 4-surface contract + `validate-bundle` verb + pinned Blender goldens cross-validated; cross-repo pointer note in swg-blender-plugin.)*

### Residuals (carried from V1)

- [ ] **RESID-01**: The Object Template Editor displays list/struct params typed, not raw (closed via AUTH-02).
- [ ] **RESID-02**: The intro-skip scene-transition crash is diagnosed (VEH faulting address/module) and fixed.
- [x] **RESID-03**: SC3 live-reload semantics are confirmed/honest for string-table + object-template reload candor.
- [x] **RESID-04**: The SWG window-resize / windowed↔fullscreen edge cases are enumerated and fixed.

## Future Requirements (beyond v2.0)

- **PROD-W2-TRN** (Terrain editor `.trn`) — the v2.1 editor lead (heavier codec than Particle).
- **REQ-V2-one-click-package** / **REQ-V2-share-to-hub** — Wave-3 packaging + community hub.
- **REQ-V2-wave-2-plugins** (remaining) — Conversation, Quest, Buildout/World, UI Page, Shader editors.
- **REQ-V2-wave-3-plugins** — Mod Manager, Packager, Community Hub, Asset Diff.
- **REQ-V2-tier-3-mock-d3d9** — Tier-3 mock `IDirect3DDevice9` replay (still deferred; revisit if MCP-03 / editor live-preview testing demands it).

## Out of Scope

Permanent exclusions. Locked product-scope boundaries from vision.md anti-goals (DEC-A1..A4 in PROJECT.md).

| Feature | Reason |
|---------|--------|
| Server-side mod management | SWG-Source / swg-main own that (CON-S-01 / DEC-A1) |
| Launcher / patcher | SWGEmu and community launchers own that (CON-S-02 / DEC-A2) |
| Mesh / animation / texture authoring (Maya / 3ds Max replacement) | DCC tools have decades-deep UX; Utinni plugs into export pipelines instead (CON-S-03 / DEC-A3) |
| Multiplayer-cheat enabling | All editing is local/offline; shards may detect and reject modified clients — accepted (CON-S-04 / DEC-A4) |
| FlaUI WinForms UI automation | Too flaky; not a current investment (CON-TT-03) |
| Wave-4 candidates (mesh viewer, animation previewer, server-side script editor, texture authoring) | Realistically the territory of separate tools Utinni hands off to (vision.md "Wave 4 — maybe-someday") |

## Traceability

Each requirement maps to exactly one phase. Phase numbers refer to ROADMAP.md.

### V1 (Phases 1–11) — SHIPPED `v1.0.0` 2026-06-01

| Requirement | Phase | Status |
|-------------|-------|--------|
| TEST-01 (Tier 1 C# scaffold) | Phase 1 | Pending |
| STAB-01 (Fix C-01..C-15) | Phase 2 | Pending |
| STAB-02 (R-A..R-H reworks) | Phase 3 | Pending |
| TEST-03 (Tier 2 CLI shim) | Phase 4 | Validated 2026-05-23 |
| TEST-02 (Tier 1 C++ unit tests) | Phase 5 | Pending |
| STAB-03 (Cleanups + dep bumps) | Phase 6 | Complete (06-05) 2026-05-25 |
| TEST-04 (Tier 4 boundary doc) | Phase 6 | Pending |
| STAB-05 (Open questions CON-O-01..-08) | Phase 6 | Pending |
| STAB-04 (Preserve 24 foundations) | All phases (cross-cutting) | Complete (06-05 audit) 2026-05-25 |
| PROD-W1-TRE (TRE Browser) | Phase 7 | Pending |
| PROD-01 (See everything the client loads) | Phase 7 | Pending |
| PROD-W1-IFF (IFF Editor) | Phase 8 | Pending |
| PROD-W1-DT (Datatable Editor) | Phase 9 | Pending |
| PROD-W1-STF (String-table Editor) | Phase 10 | Pending |
| PROD-W1-OT (Object Template Editor) | Phase 11 | Pending |
| PROD-02 (Edit major asset types — W1 subset) | Phases 8–11 (aggregate) | Pending |

### v2.0 (Phases 12–16) — "AI-Assisted SWG Tools" (ACTIVE)

| Requirement | Phase | Status |
|-------------|-------|--------|
| AUTH-01 (Revive-feasibility spike — HARD GATE) | Phase 12 | Pending |
| RESID-02 (Intro-skip scene-transition crash) | Phase 12 | Pending |
| AUTH-02 (TemplateDefinitionCompiler → param→type schema) | Phase 13 | Pending |
| AUTH-03 (TemplateCompiler `.tpf` → `.iff` verb) | Phase 13 | Pending |
| AUTH-04 (TreeFileBuilder build-`.tre` verb) | Phase 13 | Pending |
| AUTH-05 (utinni-cli SAVE verb) | Phase 13 (write surface completed Phase 14 P03a: apply-save-* verbs) | Complete |
| AUTH-06 (Datatable compile + item exporters) | Phase 13 | Pending |
| RESID-01 (OT Tier-2 typed list-param display) | Phase 13 | Pending |
| MCP-01 (Headless MCP READ tools) | Phase 14 | Complete |
| MCP-02 (MCP write/SAVE + MCP-SECURITY.md threat register) | Phase 14 | Complete |
| PROD-W2-WS (WorldSnapshot / object-placement editor) | Phase 15 | Validated (2026-06-13 live sign-off) |
| PROD-W2-PRT (Particle / client-effect editor) | Phase 15 | Validated (2026-06-13; editor + Preview candor live-verified, B6 closed) |
| RESID-03 (SC3 live-reload candor) | Phase 15 | Validated save-tier (2026-06-13, D-ii closed); live render-on-reload tracked DEFERRED (loose searchPath disabled) |
| RESID-04 (Window-resize / fullscreen edge cases) | Phase 15 | Validated (2026-06-13; C3 PASS, no device Reset) |
| MCP-03 (Live-injected MCP bridge) | Phase 16 | Validated (2026-06-14; Tier-2 cross-impl bridge proven, fail-closed gated; live in-client ping = Tier-4 manual residual per D-01) |
| ECO-01 (Blender file-format / `.rsp` boundary) | Phase 16 | Validated (2026-06-14; 4-surface contract + validate-bundle + cross-validated goldens) |

**Coverage:**
- v1 requirements: 15 total — mapped 15, unmapped 0 ✓
- v2.0 requirements: 16 total (MCP-01..03, AUTH-01..06, PROD-W2-WS, PROD-W2-PRT, ECO-01, RESID-01..04) — mapped 16, unmapped 0 ✓
- No requirement maps to more than one phase ✓

---
*Requirements defined: 2026-05-16 via `/gsd:new-project` after `/gsd:ingest-docs` synthesis.*
*Last updated: 2026-06-01 — v2.0 "AI-Assisted SWG Tools" requirements traced to Phases 12–16 (16 reqs, 100% coverage). Previous: 2026-05-16 — initial creation.*
