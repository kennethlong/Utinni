# Phase 12: Revive-feasibility spike (HARD GATE) + intro-skip crash - Context

**Gathered:** 2026-06-02
**Status:** Ready for planning

<domain>
## Phase Boundary

Two independent deliverables in one phase:

1. **AUTH-01 (HARD GATE)** — Prove the entire revive+wrap strategy is viable *before* anything in Phase 13–14 depends on it. Lift-and-shift `TemplateCompiler` + `TemplateDefinitionCompiler` + `TreeFileBuilder` from `swg-client-v2` into a Utinni-owned `tools/` tree, verify each builds + links **standalone at v145** (or the documented v143 fallback), strip the dead Perforce/Alienbrain/transitive dependency graph, produce a **per-tool dependency manifest** + a **pinned `swg-client-v2` source SHA**, and prove each tool runs headless against a real asset.

2. **RESID-02** — Reproduce → diagnose (via the VEH crash-logger already deployed in `d1096ac`) → **root-cause fix** the dump-less intro-skip scene-transition crash on a live injected session.

**In scope:** the three named compilers' standalone build/link verification, dead-dep pruning, the manifest + SHA artifacts, a real-asset byte-exact smoke per tool, and the crash repro/diagnose/fix.

**Out of scope (Phase 13+):** wrapping the tools as `utinni-cli` verbs, the SAVE verb, OT Tier-2 typed display (consumes this phase's `TemplateDefinitionCompiler` output but is a Phase 13 deliverable), datatable compile + item exporters, and any MCP work.

</domain>

<decisions>
## Implementation Decisions

### Carried forward — inherited locks (do NOT re-litigate)
- **D-01:** Lift-and-shift LOCKED — copy source + required shared libs into the repo-local `tools/` tree; never `#include` or `ProjectReference` across into the live `swg-client-v2` tree (it is mid-D3D9→D3D11 migration on `koogie-msvc-cpp20-base`). Source: PROJECT.md "Lift-and-shift constraint (locked)".
- **D-02:** v145 is the shared target toolset (matches Utinni; `swg-client-v2` is *already* v145/`stdcpp20`, STLport453 is gone). v143 is the **documented per-tool fallback** for any tool that refuses v145 — the subprocess seam is toolset-agnostic; the lift-and-shift constraint forbids building *in* `swg-client-v2`, not building at a given toolset in our own tree.
- **D-03:** x86 / `Win32` only (CON-P-02). Record the **exact lifted-from x86 SHA** (a SHA, not a branch HEAD); watch the upstream `x64bit-Upgrade` branch as a divergence risk.
- **D-04:** Prune dead `perforce`/`alienbrain` include paths; keep only the real leaf externals — `zlib` (TreeFileBuilder) and `pcre/4.1` (TemplateCompiler). No renderer/D3D/D3DX (these tools are headless).
- **D-05:** All **three** tools are in scope, including `TemplateDefinitionCompiler` (`.tdf`/`.tpd` → per-class param→type schema) — it is the cheap path that unblocks OT Tier-2 in Phase 13. Do not assume uniform status: `TemplateCompiler.vcxproj` has v145 Debug objects on disk (likely-green); `TreeFileBuilder.vcxproj` is v145-configured but has no build output (unverified); treat "is v145 in the vcxproj" and "actually builds + links" as different facts, resolved by a build pass not by reading.
- **D-06:** The VEH crash-logger is **already deployed** (Utinni `d1096ac`, extends the VEH to log fatal-class exceptions with module+RVA). This phase is the capture+diagnose+fix run, not a re-instrumentation run.

### Tools tree & build seam
- **D-07:** `tools/Utinni.Tools.sln` is a **separate, Utinni-owned solution** (standalone, not folded into the managed `Utinni.sln` build matrix), and its build is **wired into the self-hosted v145 CI runner THIS phase**. The hard gate is continuously enforced from day one — no "green on my box only" rot before Phase 13 wraps the tools. (Self-hosted runner because v145/VS2026 is Insiders-only, not on GitHub-hosted images — see `project_self_hosted_ci`.)
- **D-08:** The **per-tool dependency manifest** and the **pinned `swg-client-v2` SHA** are in-repo deliverables recorded as markdown under `tools/` (exact filename/format = Claude's discretion). The manifest enumerates each tool's real `#include` closure + required shared libs, with dead include paths shown as pruned.

### Per-tool "pass" bar (headless smoke)
- **D-09:** Each tool must run headless against a **real shipped `swg-client-v2` asset** and produce output that is **byte-exact** against a known-good reference. **Byte-exact is mandatory — there is NO structural/round-trip fallback.** If a tool's transform proves cross-toolset non-deterministic (archive record ordering, padding, header build-stamps), that is a **gate finding to surface and resolve**, not a free pass. (RESEARCH must validate per tool: do matching source→known-good-output pairs exist in `swg-client-v2`, and is the transform deterministic enough for byte-exact at all? This is the single biggest feasibility unknown in the phase — flag loudly if a tool's output cannot be made byte-exact.)

### v145 fallback & failure policy
- **D-10:** **Stop-and-ask before each v143 fallback.** Surface every v145 build/link failure to the maintainer *before* dropping that tool to v143 — the toolset decision is made per tool, with the maintainer in the loop, not auto-fallen-back. (Note: this tightens the ROADMAP's "documented fallback" — fallback is still allowed, but it is a maintainer decision per failure, not automatic.)

### RESID-02 intro-skip crash
- **D-11:** **Full root-cause fix — no masking guard/detour shortcut.** Reproduce via the TJT-driven scene-change path (the known trigger; see `project_scene_change_via_tjt`), capture faulting module+RVA from the deployed VEH logger, and fix the underlying defect:
  - If the fault is in Utinni's own injection/detour/callback surface → fix it there properly.
  - If the faulting module+RVA resolves into **`SWG.exe` game code Utinni does not own** → the deliverable is the **documented root-cause analysis** (module + RVA + mechanism); you cannot root-cause-fix code you don't control. (This is the honest disposition: it may mean the live-session success criterion is met by analysis rather than a code fix if the defect is purely game-side — accepted.)
  - VEH logger **stays deployed** after capture.
  - Baseline reminder: landing naked after a TJT-driven scene change is the *expected* baseline, NOT a crash signal (see `project_tjt_scene_change_naked_baseline`). "Naked, but in world" = success.

### Claude's Discretion
- Exact `tools/Utinni.Tools.sln` internal project layout and the manifest/SHA file naming + format.
- Precise self-hosted-CI step wiring (verify-only build step shape).
- Which specific real asset(s) are used for each tool's byte-exact smoke (subject to availability surfaced by research).
- Order of the build pass across the three tools (TemplateCompiler likely-green → verify first; TreeFileBuilder unverified → the real risk).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope & requirements
- `.planning/ROADMAP.md` § "Phase 12" — goal, success criteria, hard-gate note, constraint guard-rails.
- `.planning/REQUIREMENTS.md` — AUTH-01 (hard gate) + RESID-02 statements/acceptance.
- `.planning/PROJECT.md` § "Lift-and-shift constraint (locked)" + "Toolchain (v145)" + Constraints (CON-P-02 x86).

### Revive-feasibility research (the corrected effort picture)
- `.planning/research/SUMMARY.md` — the single most important correction: revive is verify-build + strip-dead-deps + manifest, NOT a v143→v145 port; per-tool status is uneven; lifted-from-SHA + x64 watch.
- `.planning/research/PITFALLS.md` — transitive/dead dependency graph, mis-judging the port, coupling to a moving checkout.
- `.planning/research/FEATURES.md`, `.planning/research/ARCHITECTURE.md` — revive sequencing context.
- `docs/ai/toolchain-inventory.md` — the ~60-tool cross-walk + revive/replace rationale (interactive-editors-REPLACE vs build-chain-CLIs-REVIVE+WRAP).

### Lift-from source (sibling reference corpus — read-only, no runtime dep)
- `D:/Code/swg-client-v2/src/engine/shared/application/TemplateCompiler/build/win32/TemplateCompiler.vcxproj` — v145, stdcpp20, Win32, ~25 ProjectReferences, built Debug objs present (likely-green).
- `D:/Code/swg-client-v2/src/engine/shared/application/TemplateDefinitionCompiler/build/win32/TemplateDefinitionCompiler.vcxproj`
- `D:/Code/swg-client-v2/src/engine/shared/application/TreeFileBuilder/build/win32/TreeFileBuilder.vcxproj` — v145, NO build output (unverified — the real gating risk).
- `D:/Code/swg-client-v2/docs/research/swg-tools-and-likely-studio-toolchain.md` — 653-line tool census: TemplateCompiler vs TemplateDefinitionCompiler distinction, dependency map.
- The dead-dep vestige: `sharedTemplate.vcxproj` carries a `perforce/include` path that is present but never `#included`.

### RESID-02 crash
- `.planning/phases/11-tjt-subpanel-object-template-editor/11-05-SUMMARY.md` (line ~38, ~55) — VEH crash-logger deployment record (commit `d1096ac`, "diagnostic, pending repro").
- `.planning/phases/11-tjt-subpanel-object-template-editor/11-V1-RELEASE-GATE.md` (line ~73).
- `UtinniCore/utinni.cpp` — the VEH handler source (the `Vectored…` install site).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **Self-hosted v145 CI runner** (`C:\actions-runner`, push-trigger, verify-only steps) — already green; D-07's `tools/Utinni.Tools.sln` build step bolts onto this. See `project_self_hosted_ci`.
- **Tier-2 golden-fixture harness** (`Utinni.Cli.Tests`, in-repo synth fixtures ≤200 bytes, no LFS) — the established pattern for deterministic artifact comparison; the byte-exact smoke (D-09) is conceptually a golden compare, though D-09 deliberately uses *real* assets not synth.
- **Deployed VEH crash-logger** (`d1096ac`) — already logs fatal-class exceptions with module+RVA; the capture mechanism for D-11 is in place.

### Established Patterns
- **Detour-table pattern** (`using pX = …; pX x = (pX)0xRVA; Detour::Create(…)`, RVAs single-sourced via `UTINNI_API`, CON-H-03) — relevant if the D-11 fix lands in Utinni's injection surface.
- **No `tools/` dir exists yet** — this phase creates it from scratch.

### Integration Points
- `tools/Utinni.Tools.sln` → self-hosted CI workflow (`.github/workflows/ci.yml`-class file) — new build lane.
- The manifest + pinned SHA become the contract Phase 13 (`utinni-cli` wrap verbs) and Phase 14 (MCP) consume.

</code_context>

<specifics>
## Specific Ideas

- The maintainer chose the **high-rigor** option in every gray area: CI-enforced now, real-asset byte-exact (no fallback), stop-and-ask per fallback, full root-cause fix. Plan accordingly — this hard gate is to be done thoroughly, not as a cheap spike.
- `TreeFileBuilder` is the prime unknown (v145-configured, no build output) — front-load the empirical build risk there; `TemplateCompiler` is likely-green and can be verified quickly to bank an early win.

</specifics>

<deferred>
## Deferred Ideas

- Wrapping the revived tools as `utinni-cli` verbs (`compile-*`/`build-*`), the SAVE verb, OT Tier-2 typed display — all **Phase 13** (AUTH-02..06, RESID-01).
- Per-dep vcpkg migration commits (plan 06-02b) — unrelated post-V1 follow-on, not this phase.

### Reviewed Todos (not folded)
- `swg-window-resize-fullscreen-edge-cases.md` — that's **RESID-04 / Phase 15**, not Phase 12. Keyword match only.
- `phase09-datatable-editor-review-warnings.md`, `phase10-stringtable-sc3-live-reload-residual.md` — prior-phase residuals (RESID-03 → Phase 15); not this scope.
- `gamecallbacks-gc-av-flake-fix.md`, `loader-lock-harness-flake-fix.md` — CI-stability flakes; unrelated to AUTH-01/RESID-02. (De-flake in CI-covered code only — see `project_loader_lock_harness_ci_flake`.)

</deferred>

---

*Phase: 12-revive-feasibility-spike-hard-gate-intro-skip-crash*
*Context gathered: 2026-06-02*
