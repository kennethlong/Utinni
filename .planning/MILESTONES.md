# Milestones — Utinni

Historical record of shipped versions. Newest first. Full per-milestone detail in
`.planning/milestones/v[X.Y]-ROADMAP.md` + `v[X.Y]-REQUIREMENTS.md`.

---

## v2.1 — "Wave-2 Editors + Foundation Hardening" — ✅ SHIPPED 2026-06-23

**Phases:** 17–24 · **Plans:** 35 · **Requirements:** 22/22 satisfied
**Window:** 2026-06-15 → 2026-06-23 · **Tag:** _pending_ (`v2.1` not yet cut)

Shipped the Terrain `.trn` and ClientEffect editors on a hardened rendering/toolchain base so the
live-preview editors survive SWG Source's D3D9→D3D11 flip — foundation (17–19) before features (20–23),
with the client-advertised entry-point contract (24) retiring hardcoded RVAs on the from-source client.

**Key accomplishments:**

1. **Hardened the CppSharp/v145 binding toolchain** — documented the VS2019-14.29 parser redirect as the
   supported config, added CI tripwires (C++23-header hard-fail, clang-20 warn-pin), and a per-block-hash
   ABI diff + frozen-DLL MEF-compose gate so a binding regen can never silently break a pre-built plugin.
   (Phase 17, CPPS-01..04)
2. **Carved the `IRenderBackend` seam + Dx9Backend** — single-sourced the ~1000-line API-neutral overlay
   logic, D3D9 path behaviorally unchanged (no-Reset/Present-stretch contract preserved). (Phase 18, RNDR-01)
3. **Added the Dx11Backend** — DXGI `Present`/`ResizeBuffers` hooks, one-backend-per-session `gl%02d_r.dll`
   detection, per-frame RTV rebind + DXGI resize. (Phase 19, RNDR-02/03/04)
4. **Shipped the Terrain `.trn` editor** — version-dispatched typed codec with raw-fallback + navigable
   layer tree, byte-exact scalar-leaf edit/save, verbs-first + MCP, across both SWGEmu and Restoration
   lineages, plus the TJT SubPanel with honest save-then-reload candor. (Phases 20–21, PROD-W2-TRN-01..05)
5. **Shipped the ClientEffect editor** (`.iff` command list, verbs + MCP + SubPanel) and **user-definable
   IFF chunk templates** — schema-driven byte-exact decode/encode of any modder-described chunk,
   manageable from the IFF Editor UI. (Phases 22–23, PROD-W2-CFX-01/02, PROD-IFFT-01..03)
6. **Landed client entry-point advertisement (`GetEngineHookPoints`)** — the from-source SWG-Source client
   advertises its own engine entry points; UtinniCore consumes them dual-path (advertised on the DX11
   client, hardcoded-RVA on SWGEmu, auto-detected), retiring hardcoded RVAs on that client and unblocking
   the Phase 18/19 DX11 live-smokes (D-08/D-22). The advertised DX11 client boots → login → loads worlds →
   embed-scales; SWGEmu byte-for-byte unchanged (D-00). (Phase 24, EPA-01..04)

**Verification:** milestone audit not yet run (`/gsd:audit-milestone` optional follow-on). All 22
requirements code-complete; each feature phase maintainer-live-smoke-verified at its close.

**Active follow-on (out of v2.1 scope):** the advertised-client **editor-unlock arc** — grew the
contract from RENDER-only (~77/230) to **v13 / 119 names** and delivered five live editor features on the
DX11 client (Effects live-preview, Chat, Radial/menus, World-pick inspector, Free-cam mouse-fly). Full
ledger in the Phase 24 session handoffs + `project_phase24_editor_unlock_inflight` memory. The x64 half of
Backlog 999.7 and full ~198/230 hook coverage remain deferred.

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
