# Milestones — Utinni

Historical record of shipped versions. Newest first. Full per-milestone detail in
`.planning/milestones/v[X.Y]-ROADMAP.md` + `v[X.Y]-REQUIREMENTS.md`.

---

## v2.0 — "AI-Assisted SWG Tools" — ✅ SHIPPED 2026-06-14

**Phases:** 12–16 · **Plans:** 39 · **Requirements:** 16/16 satisfied
**Git range:** `feat(12-01)` → `test(16)` · 202 commits · 2026-05-31 → 2026-06-14 (15 days)
**Tag:** `v2.0`

Turned Utinni from a tool that *edits* assets into one that *authors* them, and made the whole
pipeline AI-drivable.

**Key accomplishments:**

1. **Revived 3 SOE build CLIs** (`TreeFileBuilder`, `TemplateCompiler`, `TemplateDefinitionCompiler`) building + linking standalone at v145 in a Utinni-owned `tools/` tree via REAL porting (engine-API drift, C++20, SAFESEH, CRT-compat) — CI-enforced AUTH-01 hard gate. (Phase 12)
2. **Wrapped the revived compilers as 17 `utinni-cli` verbs** (`compile-*`/`build-*`/`apply-save-*`) and closed the Object Template Editor's Tier-2 typed list/struct display (RESID-01). (Phase 13)
3. **Shipped the headless net10 `Utinni.Mcp` stdio MCP server** (11 tools, ZERO format logic) — AI agents read+edit+save the full SWG asset pipeline with byte-exact verify-before-commit, fail-closed `resolvedRoot`, and a 17-threat `MCP-SECURITY.md`. (Phase 14)
4. **Shipped the first Wave-2 DCC-style editors** as TJT SubPanels — WorldSnapshot/object-placement + Particle/`.prt` client-effect — and fixed the exclusive-fullscreen embed-detach (RESID-04). (Phase 15)
5. **Built the live-injected MCP bridge** (named-pipe IPC, SDK out-of-proc) for in-client edit preview (MCP-03) and **formalized the Utinni ↔ swg-blender-plugin file-format contract** (ECO-01). (Phase 16)

**Verification:** `v2.0-MILESTONE-AUDIT.md` — 16/16 requirements satisfied, integration PASS (5/5 seams
WIRED, 2/2 E2E flows COMPLETE), Nyquist 5/5 compliant.

**Known deferred items at close: 12** (recorded in STATE.md → Deferred Items). All bookkeeping or
documented accepted-debt: AUTH-03/AUTH-06 byte-exact *success* goldens (A1 gate-findings, no canonical
SOE assets); RESID-03 live render-on-reload (config-gated loose searchPath); Particle live-preview hook
(honestly degraded); MCP-03 Tier-4 live in-client ping (non-gating, D-01); ECO-01 cross-repo pointer note
(third-party repo); CV-1 Blender crc-first vs Utinni size-first TOC; Phase 09 review warnings; Phase 10
SC3 live-reload; swg-window-resize fullscreen mouse-offset; 3 already-passed UAT files + 1 v1.0-era
CONTEXT question set (detector artifacts).

---

## v1.0 — MVP "Demo + CI green" — ✅ SHIPPED 2026-06-01

**Phases:** 1–11 (+ 02.1 inserted) · **Plans:** 55 · **Requirements:** 15/15 satisfied
**Tags:** `v1.0.0`, `v1.0.0-rc.1` · GSD planning bootstrapped 2026-05-16

> Retroactively recorded during the v2.0 milestone close (2026-06-14) — v1.0 was tagged but never run
> through `/gsd:complete-milestone`. See `milestones/v1.0-ROADMAP.md` for full phase detail.

Stabilised the injected-DLL framework and shipped the first five editors.

**Key accomplishments:**

1. **Closed all 15 critical bugs** (C-01..C-15) — incl. re-architecting the C-01 DllMain loader-lock to an exported `utinni_init` + CreateRemoteThread — plus the 8 strategic reworks (R-A..R-H). (Phases 2/02.1/3)
2. **Stood up the 4-tier test harness** — Tier-1 C# (xUnit) + Tier-1 C++ (Catch2) + Tier-2 `utinni-cli` golden fixtures, all gating master via GitHub Actions on a self-hosted v145 runner. (Phases 1/4/5)
3. **Modernised the toolchain** — vcpkg migration, PlatformToolset v142→v145, DXSDK retired, LeksysINI replaced; 24-foundation preservation audit. (Phase 6)
4. **Shipped all five Wave-1 editors as Jawa Toolbox subpanels** (DEC-C4) — TRE Browser, IFF Editor (read+write, four-tier save matrix), Datatable, String-table, Object Template — each byte-exact and demoed end-to-end against a live SWG client. (Phases 7–11)

**Carried residuals (all closed in v2.0):** RESID-01 OT typed display, RESID-02 intro-skip crash,
RESID-03 SC3 live-reload, RESID-04 window-resize/fullscreen.
