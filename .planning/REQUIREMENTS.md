# Requirements — Utinni v2.1 "Wave-2 Editors + Foundation Hardening"

**Milestone:** v2.1 (Phases 17–23) · **Defined:** 2026-06-14 · **Source:** `.planning/research/SUMMARY.md`
(HIGH confidence) + 2026-06-14 scoping decisions.

**Goal:** Ship the Terrain `.trn` editor (Wave-2 #1) + a ClientEffect editor on a hardened
rendering/toolchain base, so the live-preview editors survive SWG Source's D3D9→D3D11 flip — plus the
user-definable IFF chunk-templates quick win. Foundation-before-features: the CppSharp/v145 hardening and
the D3D11 render-path land first.

REQ-IDs continue the project's `CATEGORY-NN` scheme. 18 requirements across 5 categories → 7 phases.

---

## v2.1 Requirements

### Foundation — Toolchain hardening (CPPS)

> Scope correction (research, HIGH): no released CppSharp ships clang 20 (what v145's 14.5x STL
> requires), so a *native* v145 parse is NOT achievable in v2.1. The deliverable is harden-the-redirect,
> not retire-it. ClangSharp/Biohazrd/C++-CLI ruled out (advisor, 2026-06-14): they don't reproduce the
> C++-class→C#-class bridge without a large hand-written shim + plugin-ABI break.

- [x] **CPPS-01**: The binding generator's clang-parse capability against the MSVC v145 (14.5x) STL is
  empirically determined by a spike and documented, so the team knows native-v145 parse is unreachable
  with current tooling and why.
- [x] **CPPS-02**: The VS2019-14.29 parser-include redirect is hardened and documented as the *supported*
  binding-generation configuration (explicit, not silently load-bearing).
- [x] **CPPS-03**: CI fails fast on the two unblock/regression signals — (a) UtinniCore C++ adopting a
  C++23 STL header the 14.29 redirect can't parse, and (b) a CppSharp release shipping clang ≥20.
- [x] **CPPS-04**: A binding regen cannot silently break pre-built plugin DLLs — a per-block-hash ABI
  diff + a frozen-DLL MEF-compose fixture gate the generated surface, and TJT/Sytner rebuild in lockstep.

### Foundation — Render-path (RNDR)

- [x] **RNDR-01**: The ImGui overlay renders through a single `IRenderBackend` seam with the existing
  D3D9 path behaviorally unchanged — overlay still renders and takes input in a live D3D9 session
  (verified by the existing live-smoke).
- [x] **RNDR-02**: The overlay renders and maps input correctly (render-target space) when the SWG client
  runs Direct3D 11 — `Dx11Backend` hooks the DXGI swapchain `Present`.
- [x] **RNDR-03**: Exactly one render backend installs per session, auto-detected from the loaded SWG
  renderer DLL (`gl%02d_r.dll`), with a one-shot diagnostic log — no doubled input or dual ImGui contexts.
- [x] **RNDR-04**: The overlay survives a window resize under D3D11 — the RTV is released/recreated inside
  the `ResizeBuffers` hook (no `DXGI_ERROR_INVALID_CALL`, the DXGI analog of the forbidden D3D9 `Reset`).

### Foundation — Client entry-point advertisement (EPA)

> Promotes the advertisement half of Backlog 999.7. Unblocks the Phase 18/19 live-smokes on the
> from-source D3D11 client (`swg-client-v2/SwgClient_r.exe`), whose memory layout no hardcoded RVA matches.
> x86-only; x64 (999.7's other half) stays deferred.

- [ ] **EPA-01**: The SWG-Source client (`SwgClient_r.exe`) advertises its engine entry points to UtinniCore
  via a versioned, well-known export (`GetEngineHookPoints`) sourced at compile time by symbol (`&fn`) in the
  **executable** module — covering the ~198 entry points UtinniCore currently hardcodes. (The renderer DLL's
  `gl11_r.dll!GetHookPoints` can only see graphics symbols; this is its exe-side companion for game logic.)
- [ ] **EPA-02**: UtinniCore consumes the advertised contract on the SWG-Source client — a single resolver
  populates the `swg::*` function pointers, retiring the hardcoded `0x00xxxxxx` literals on that client. The
  old SWGEmu (Pre-CU) client keeps the hardcoded-RVA path; discovery is dual-path, auto-selected by detecting
  the export (no config toggle). No hardcoded literal is dereferenced on the advertised client.
- [ ] **EPA-03**: The DX11 overlay kickoff is decoupled from the SWGEmu-addressed `graphics::install` hook —
  `directX11::kickoff()` is driven from the advertised contract / a binary-agnostic trigger, so the Phase-19
  DX11 path starts on the advertised client without relying on any hardcoded install detour.
- [ ] **EPA-04**: A client missing the export (older/unpatched) or shipping a partial contract is detected and
  logged, and UtinniCore degrades cleanly — SWGEmu falls back to hardcoded discovery; an unknown client bails
  without crashing (mirrors the gl11 `GetHookPoints` graceful-bail). A coverage check flags any UtinniCore-hooked
  endpoint absent from the advertised struct.

### Terrain editor (PROD-W2-TRN)

- [x] **PROD-W2-TRN-01**: A modder can open a `.trn` (from a TRE archive or loose override) and navigate
  its procedural layer tree (TGEN → Layers → Boundaries/Filters/Affectors/sub-layers) with names + active
  flags, and view the six shared palettes read-only.
- [x] **PROD-W2-TRN-02**: Common terrain tags (height/shader/color/flora affectors; circle/rect
  boundaries; height/slope filters) display as typed fields; unknown/long-tail tags degrade to a generic
  field list — never a hard decode failure.
- [x] **PROD-W2-TRN-03**: A modder can edit + save scalar/enum leaf values and toggle a layer/affector
  active flag, byte-exact, via the loose-override save matrix.
- [x] **PROD-W2-TRN-04**: `.trn` decode/edit/save is exposed as golden-tested `utinni-cli` verbs
  (decode / roundtrip / apply-save) + an MCP read tool, validated across BOTH SWGEmu and Infinity
  fixtures.
- [x] **PROD-W2-TRN-05**: On save, the terrain change previews live in-client where a heap-free hot-path
  regen is reachable; where it is not (this build), it degrades to save-then-reload with explicit candor —
  never a standalone Utinni renderer.

### Effects editor — ClientEffect (PROD-W2-CFX)

- [x] **PROD-W2-CFX-01**: A modder can open a ClientEffect `.iff`, view/edit its command list
  (CreateAppearance / PlaySound / CreateLight / CameraShake / ForceFeedback / …), and save byte-exact via
  the loose-override matrix.
- [x] **PROD-W2-CFX-02**: ClientEffect decode/edit/save is exposed as golden-tested `utinni-cli` verbs +
  an MCP read tool, with reference-validation against the load order, across both lineages.

### IFF chunk templates — quick win 999.2 (PROD-IFFT)

- [x] **PROD-IFFT-01**: A modder can describe an arbitrary IFF chunk's binary layout (primitives, colors,
  vectors, quaternions, matrices, arrays, structs) as a named, reusable template.
- [x] **PROD-IFFT-02**: Utinni auto-applies a matching template to decode/display an otherwise-hex chunk
  and re-encodes edits byte-exact (round-trip verified — the hidden encode-parity risk).
- [x] **PROD-IFFT-03**: Templates are manageable (create / edit / save / select) from the IFF Editor UI.

---

## Future Requirements (deferred to v2.1.x / v2.2+)

- **Terrain 2D sampled-map preview** — needs the `Sampler` port; high value, too big for v1 (System.Drawing.Bitmap suffices when it lands, no new dep).
- **Terrain structural authoring / boundary painting** — the full SOE 100-file TerrainEditor surface; a milestone of its own.
- **Terrain long-tail affector typed coverage** — river/road/ribbon/environment/exclude/passable.
- **Lightning + Swoosh effects editors** — finish the `clientParticle` family later (same pattern, no stack divergence).
- **999.3 TRE override/version-history view** — deferred to leverage the in-progress separate TRE diff tool (~1 day from MVP, 2026-06-14); revisit at its MVP. See backlog + `[[project_tre_diff_tool_wip]]`.
- **net9/10 generator-pipeline modernization** — buys no v145-native capability (still needs a redirect to 14.4x); scored separately, deferred.
- **ClangSharp / native-v145 binding migration** — only if a concrete v145 STL feature ever forces UtinniCore C++ past what the 14.29 redirect can parse; LARGE/multi-week, plugin-ABI break (advisor 2026-06-14).

## Out of Scope (permanent — anti-features)

- **Standalone Utinni terrain renderer / 3D fly-through** — violates the locked live-in-client preview decision + DEC-A3.
- **C# reimplementation of the procedural generator for preview** — preview is the real engine, in-client, or save-then-reload.
- **Editing baked heightmaps** — terrain is procedural; there is no stored heightmap to edit.
- **Server-side terrain regen *orchestration*** — DEC-A1 fences the server app/management layer, NOT the
  shared asset. Editing the `.trn` in a TRE/loose-override IS authoritative on both client and server (both
  load the same data); what's out of scope is Utinni driving the server's regen/restart or managing server
  config — that stays SWG-Source / swg-main's domain.

## Milestone risks / assumptions (confirm during execution)

- **x64 is OUT of scope for v2.1 (32-bit only; user-locked 2026-06-14).** v2.1 targets the 32-bit client;
  Phase 19's D3D11 work is for the 32-bit SWG-Source client. swg-client-v2's `x64bit-Upgrade` is a
  deliberate later milestone paired with the entry-point-advertisement mechanism (Backlog 999.7) — NOT a
  v2.1 risk to confirm. Entry-point discovery in v2.1 stays today's RVA/pattern-scan model on both clients
  (the advertisement contract is future, and SWG-Source-only; the SWGEmu client always keeps RE discovery).
- **swg-client-v2 D3D11 DLL contract is churning:** confirm the final module name (`gl11_r.dll`
  source-grounded vs `Direct3d11.dll`) and hard-cutover-vs-runtime-switch before Phase 19 design lock.
- **CommandLineParser verb-count ceiling:** the CLI is at 23 `*Command.cs` files (prior cap was 16);
  confirm clean registration before adding `trn-*`/`effect-*` verbs.

---

## Traceability

Each requirement maps to exactly one phase (Phases 17–23). 18/18 mapped — no orphans, no duplicates.

| Requirement | Phase | Status |
|-------------|-------|--------|
| CPPS-01 | Phase 17 — CppSharp / v145 Hardening | Complete |
| CPPS-02 | Phase 17 — CppSharp / v145 Hardening | Complete |
| CPPS-03 | Phase 17 — CppSharp / v145 Hardening | Complete |
| CPPS-04 | Phase 17 — CppSharp / v145 Hardening | Complete |
| RNDR-01 | Phase 18 — Render-Backend Seam + Dx9Backend | Complete (CI-green; final acceptance pending the D-08 maintainer live-smoke — 18-02 Task 3) |
| RNDR-02 | Phase 19 — Dx11Backend + Config Detection + Resize | Complete (CI-green; live D3D11 acceptance deferred to Phase 24 — RVA crash on `SwgClient_r.exe` blocks inject until entry-point advertisement) |
| RNDR-03 | Phase 19 — Dx11Backend + Config Detection + Resize | Complete (CI-green; live D3D11 acceptance deferred to Phase 24) |
| RNDR-04 | Phase 19 — Dx11Backend + Config Detection + Resize | Complete (CI-green; live D3D11 acceptance deferred to Phase 24) |
| EPA-01 | Phase 24 — Client Entry-Point Advertisement (`GetEngineHookPoints`) | Pending |
| EPA-02 | Phase 24 — Client Entry-Point Advertisement (`GetEngineHookPoints`) | Pending |
| EPA-03 | Phase 24 — Client Entry-Point Advertisement (`GetEngineHookPoints`) | Pending |
| EPA-04 | Phase 24 — Client Entry-Point Advertisement (`GetEngineHookPoints`) | Pending |
| PROD-W2-TRN-01 | Phase 20 — Terrain `.trn` Codec + Verbs + MCP | Complete |
| PROD-W2-TRN-02 | Phase 20 — Terrain `.trn` Codec + Verbs + MCP | Complete |
| PROD-W2-TRN-03 | Phase 20 — Terrain `.trn` Codec + Verbs + MCP | Complete |
| PROD-W2-TRN-04 | Phase 20 — Terrain `.trn` Codec + Verbs + MCP | Complete |
| PROD-W2-TRN-05 | Phase 21 — Terrain TJT SubPanel (+ live preview) | Complete |
| PROD-W2-CFX-01 | Phase 22 — ClientEffect Editor | Complete |
| PROD-W2-CFX-02 | Phase 22 — ClientEffect Editor | Complete |
| PROD-IFFT-01 | Phase 23 — User-Definable IFF Chunk Templates | Complete |
| PROD-IFFT-02 | Phase 23 — User-Definable IFF Chunk Templates | Complete |
| PROD-IFFT-03 | Phase 23 — User-Definable IFF Chunk Templates | Complete |

**Coverage:** 18/18 v2.1 requirements mapped ✓ · no orphans · no duplicates.
