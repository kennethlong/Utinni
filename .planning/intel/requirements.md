# Requirements (Candidate, derived from DOCs)

> No PRDs were ingested. The vision doc names product capabilities; the assessment doc names concrete remediation items; the test-harness plan names test capabilities. The requirements below are **candidates** derived from this prose for the roadmapper to consider when drafting REQUIREMENTS.md. None carry acceptance criteria — the source docs are DOC (lowest precedence) and prose-only. A real PRD would replace these with first-class entries.

---

## Product capability requirements (from vision.md)

### REQ-one-stop-tool
- **Source:** `docs/ai/vision.md`
- **Description:** A modder downloads Utinni, installs once, and from a single application can see, edit, preview, author, package, and share SWG mods.
- **Acceptance:** Not specified — captured as strategic goal in vision.md.
- **Scope:** Whole product

### REQ-see-everything-the-client-loads
- **Source:** `docs/ai/vision.md` — "The goal"
- **Description:** From inside Utinni, a user can browse every `.tre`, every IFF, every datatable, every template, every UI page, every shader, and every string-table entry the running client can load.
- **Acceptance:** Not specified.
- **Scope:** Asset browser surface (powered by `treefile::getAllFilenames` hook)

### REQ-edit-major-asset-types
- **Source:** `docs/ai/vision.md` — "The goal"
- **Description:** Utinni provides editors for the asset types a modder realistically edits by hand today: world snapshots, object templates, datatables, conversations, quests, particles, UI pages, strings, and shader uniforms.
- **Acceptance:** Not specified.
- **Scope:** Editor plugins, surfaced through `IEditorPlugin`

### REQ-live-preview-edits
- **Source:** `docs/ai/vision.md` — "The goal"
- **Description:** Edits made inside Utinni apply in-place to the running client process via existing reload paths (and a small number of new ones to be added).
- **Acceptance:** Not specified.
- **Scope:** Plugin-to-client reload contract

### REQ-author-new-content
- **Source:** `docs/ai/vision.md` — "The goal"
- **Description:** Users can introduce new meshes (via plugin exporters), new scripts, and new quests, tagged as belonging to a named mod.
- **Acceptance:** Not specified.
- **Scope:** Mod-authoring workflow

### REQ-one-click-package
- **Source:** `docs/ai/vision.md` — "The goal"
- **Description:** A user packages their mod into a single shippable archive (e.g. one `.tre` plus a manifest) with one button.
- **Acceptance:** Not specified.
- **Scope:** Mod Packager / Builder plugin (Wave 3)

### REQ-share-to-hub
- **Source:** `docs/ai/vision.md` — "The goal"
- **Description:** Users can publish to and consume from a community hub the way game launchers share user-made content today.
- **Acceptance:** Not specified.
- **Scope:** Community Hub Browser plugin (Wave 3, optional)

### REQ-wave-1-plugins
- **Source:** `docs/ai/vision.md` — "Wave 1 — round out what we have"
- **Description:** Ship five Wave-1 plugins as the first feature wave after the 1.0 framework cut: TRE Browser (read-only), IFF Editor (read+write), Datatable Editor (`.tab`), String-table Editor (`.stf`), Object Template Editor.
- **Acceptance:** Each plugin replaces the listed legacy SOE tool (per vision.md table).
- **Scope:** Plugin wave 1

### REQ-wave-2-plugins
- **Source:** `docs/ai/vision.md` — "Wave 2 — content authoring"
- **Description:** Conversation Tree Editor, Quest Editor, Buildout / World Editor, Particle Editor, UI Page Editor, Shader Inspector / Editor.
- **Acceptance:** Replace listed legacy tools.
- **Scope:** Plugin wave 2 (post-Wave-1)

### REQ-wave-3-plugins
- **Source:** `docs/ai/vision.md` — "Wave 3 — workflow"
- **Description:** Mod Manager, Mod Packager / Builder, Community Hub Browser (optional), Asset Diff / Compare.
- **Acceptance:** Mod packaging + distribution workflow end-to-end.
- **Scope:** Plugin wave 3

---

## Framework-stability remediation requirements (from assessment.md)

These are explicit work items the assessment lists as required for a 1.0 cut. Each is a candidate requirement; the roadmapper will likely express them as phase tasks rather than first-class REQs.

### REQ-fix-critical-bugs (C-01 through C-15)
- **Source:** `docs/ai/assessment.md` — Critical issues section
- **Description:** 15 enumerated critical issues that cause crashes, silent failures, or data loss. Items C-01 (DllMain loader-lock), C-02 (cross-CRT delete[]), C-03 (Network::cast uninitialized), C-04 (post-draw queue), C-05 (drag-drop static field), C-06 (PluginLoader exception swallowing), C-07 (UndoRedoManager thread-safety + dead AllowMerge), C-08 (Hotkey.ProcessString throws), C-09 (UI/game thread busy-wait), C-10 (clr::stop null deref), C-11 (DirectX findPattern null check), C-12 (VSIX VS 2019 pin), C-13 (TJT Debug path), C-14 (utinni.cfg login default), C-15 (CppSharp slnDir brittle).
- **Acceptance:** All 15 items show `done` in assessment.md status table; framework no longer exhibits silent failures.
- **Scope:** UtinniCore + UtinniCoreDotNet + SDK + Jawa Toolbox

### REQ-strategic-reworks (R-A through R-H)
- **Source:** `docs/ai/assessment.md` — Strategic reworks section
- **Description:** 8 strategic reworks: R-A (symmetric callback Add/Remove), R-B (plugin lifecycle contract — symmetric createPlugin/destroyPlugin, init() actually called, LoadLibrary failures logged), R-C (single-source-of-truth RVAs exposed via UTINNI_API), R-D (CI build workflow on GitHub Actions Windows runner), R-E (`[CallerMemberName]` logging), R-F (CppSharp header auto-discovery), R-G (idempotent `Directory.Build.props` wizard), R-H (snapshot-iteration over callback collections).
- **Acceptance:** All 8 items show `done`; plugin authoring is "genuinely pleasant"; CI catches regressions.
- **Scope:** Framework ergonomics + tooling

### REQ-cleanups
- **Source:** `docs/ai/assessment.md` — Easy cleanups section
- **Description:** ~30 quick wins: ~250 lines of dead code deletion, typo fixes (`detatch→detach`, `AddOuputSink→AddOutputSink`, `redose→redoes`, `Jo�o→João`), DetourXS + nvapi entries in licenses.txt, `.clang-format` pass, unified Windows SDK target version, DXSDK include paths fixed in Debug/Release, `Prefer32Bit` removed, ExampleEditorPlugin Release output path fixed, `.gitignore` Std/StdEdited convention documented, `TJT.ico` removed from framework, `Native.SendMessage` `int→IntPtr`.
- **Acceptance:** Codebase is "modern toolchain, no dead code".
- **Scope:** Cross-cutting

### REQ-preserve-foundations
- **Source:** `docs/ai/assessment.md` — Solid foundations section (24 items)
- **Description:** 24 enumerated load-bearing design elements must be preserved unchanged through the 1.0 work. See `constraints.md` for the full list (these are negative constraints: do-not-refactor).
- **Acceptance:** All 24 items still intact at 1.0 tag.
- **Scope:** Native + managed + tooling architecture

### REQ-resolve-open-questions
- **Source:** `docs/ai/assessment.md` — Open questions section (8 items)
- **Description:** 8 questions needing someone-who-knows-the-history to answer. Listed in `context.md` and `constraints.md` (as unresolved-context items). Resolution may change the shape of certain fixes.
- **Acceptance:** Each answered or explicitly deferred.
- **Scope:** Cross-cutting

---

## Testing infrastructure requirements (from test-harness-plan.md)

### REQ-tier-1-csharp-unit-tests
- **Source:** `docs/ai/test-harness-plan.md` — Tier 1
- **Description:** Stand up a C# xUnit (or NUnit) test project alongside Utinni's main solution; `dotnet test` runs in CI and locally without a game client. First targets: TRE/IFF parsers, plugin manifest loader, settings serialization/migration, math helpers, data-model logic.
- **Acceptance:** Test project compiles in CI; 2–3 parsers have non-trivial coverage.
- **Scope:** Test infrastructure

### REQ-tier-1-cpp-unit-tests
- **Source:** `docs/ai/test-harness-plan.md` — Tier 1
- **Description:** Catch2 (header-only) wired through `ctest` or `vcpkg` for UtinniCore. Folded in once native code has refactored seams (pairs with strategic reworks).
- **Acceptance:** Catch2 builds in CI; at least one native parser has coverage.
- **Scope:** UtinniCore test infrastructure

### REQ-tier-2-cli-shim
- **Source:** `docs/ai/test-harness-plan.md` — Tier 2
- **Description:** A `utinni-cli` executable in the same solution, referencing the same core libraries as the WinForms tool, exposing the operations the UI calls. Commands include: `parse-tre`, `list-objects`, `validate-plugin`, `inspect-iff`. Paired with golden-file tests against checked-in fixtures.
- **Acceptance:** CLI builds; at least one command has a golden test; CLI converts an estimated 60–70% of manual "Kenny please verify" loops into unattended runs.
- **Scope:** Test infrastructure + structural refactor (UI becomes one of two consumers, not the sole consumer)

### REQ-tier-3-mock-d3d9-replay
- **Source:** `docs/ai/test-harness-plan.md` — Tier 3
- **Description:** One-time maintainer capture of real TRE/IFF samples to `tests/fixtures/` plus a D3D9 call trace. Stub `IDirect3DDevice9` implementation replays the trace through the hook code so depth-buffer / post-process detours can be regressed without the game running. Optional: golden screenshots / pixel hashes for deterministic render output.
- **Acceptance:** Recorded trace exists; mock device replays it; at least one detour has a regression test.
- **Scope:** UtinniCore hook test infrastructure

### REQ-explicit-tier-4-boundary
- **Source:** `docs/ai/test-harness-plan.md` — Tier 4
- **Description:** Document the explicit residual that still requires the maintainer: actual injection into a running `SWG.exe`, visual "does it look right" judgment, GPU-driver-specific bugs, WinForms UI smoke testing (FlaUI is deliberately skipped for now as too flaky).
- **Acceptance:** Tier 4 boundary documented in testing.md or CONVENTIONS.md.
- **Scope:** Process documentation

---

## Notes for the roadmapper

- None of these carry first-class acceptance criteria — that's a function of being derived from DOCs, not PRDs. If any of these need to gate downstream work with measurable AC, promote them to a PRD first.
- The product-capability REQs (REQ-one-stop-tool through REQ-share-to-hub) are very long-horizon — most resolve as plugin work in Wave 1–3 (each plugin = a REQ in its own right when written up). The roadmapper may prefer to express the product surface as ROADMAP phases keyed to waves rather than as REQs.
- REQ-fix-critical-bugs and REQ-strategic-reworks decompose naturally into per-item phases (one per C-NN / R-N) in ROADMAP.md.
