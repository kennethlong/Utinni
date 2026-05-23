# Phase 6: Cleanups, dep bumps, open questions, Tier 4 doc, 1.0 cut - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in `06-CONTEXT.md` — this log preserves the alternatives considered.

**Date:** 2026-05-23
**Phase:** 06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut
**Areas discussed:** Open questions disposition, Dep-bumps + toolchain (incl. VS 2026), Cleanup scope ambition, 1.0 release surface

---

## Folded Todos (cross-reference)

| Todo | Score | Title | Selected |
|------|-------|-------|----------|
| `loader-lock-harness-flake-fix` | 0.6 | DllMain 50ms threshold flakes on shared windows-2022 runners | ✓ Fold |
| `gamecallbacks-gc-av-flake-fix` | 0.4 | GameCallbacksTests.RegisterCallback_ForceGCCollect AV under CI | ✓ Fold |
| (option: leave for separate phase) | — | — | |

**User's choice:** Fold both. Both lands in 06-04 CI flake plan, gated before v1.0.0-rc.1 tag (1.0 success criterion requires CI green on master).

---

## Gray-Area Selection

| Area | Selected |
|------|----------|
| Open questions (CON-O-06 + CON-O-08) | ✓ |
| Dep-bumps + toolchain (incl. VS 2026) | ✓ |
| Cleanup scope ambition | ✓ |
| 1.0 release surface | ✓ |

**User's choice:** All four. Roadmap pins WHAT ships; gray-area discussion clarifies HOW.

---

## Open questions disposition

### Q1: CON-O-08 (DXSDK June 2010) disposition

| Option | Description | Selected |
|--------|-------------|----------|
| Remove DXSDK | Replace D3DXVECTOR3 with local 3-float struct; strip DXSDK paths from all .vcxproj. Closes CON-B-03 structurally. | ✓ |
| Keep DXSDK, fix CON-B-03 paths | Add DXSDK paths to Debug + Release configs; future Wave-1 work keeps d3dx9 math helpers | |
| Defer to V2 | Document disposition; ship 1.0 with existing 'works if DXSDK_DIR set' posture | |

**User's choice:** Remove DXSDK cleanly. Sole consumer is `depth_texture.cpp` (1 #include + D3DXVECTOR3 + sizeof). Side effect: closes CON-B-03 structurally.

### Q2: CON-O-06 (LeksysINI) disposition

| Option | Description | Selected |
|--------|-------------|----------|
| Keep + close question | PIMPL means 'temporary' is cosmetic; zero code change | |
| Replace with mINI or inih | Vendored INI lib alternative inside UtINI's Impl | |
| Replace with 200-LOC custom parser | Hand-rolled inside UtINI/utini.cpp; eliminates LeksysINI dep entirely | ✓ |
| Defer to V2 | README comment lives on; CON-O-06 stays open | |

**User's choice:** Replace with 200-LOC custom parser. external/leksysini/ deleted; not migrated to vcpkg.

### Q3: Test home for new INI parser

| Option | Description | Selected |
|--------|-------------|----------|
| Catch2 in UtinniCore.Tests.exe | Phase 5's native test infra; UtinniCore.Tests/UtINI/IniParserTests.cpp | ✓ |
| Both Catch2 (native) AND xUnit (managed via CppSharp) | Belt-and-suspenders; covers CppSharp boundary | |
| xUnit only via CppSharp surface | Misses parser-internals coverage | |

**User's choice:** Catch2 in UtinniCore.Tests.exe. Per [[feedback-max-harness]].

### Q4: DXSDK removal future concern

| Option | Description | Selected |
|--------|-------------|----------|
| Remove cleanly | Wave-1 doesn't need GPU math | ✓ |
| Remove with DirectXMath breadcrumb | Add documentation note for future GPU math substitution | |
| Defer removal | Reverts to keep option | |

**User's choice:** Remove cleanly. Note: my CONTEXT writeup folded in the DirectXMath breadcrumb anyway since it's near-free; preserved as a documentation note in D-01.

**Notes:** Both areas were tight after Q1-Q4. User chose bolder options for both open questions (Remove DXSDK, custom parser) — both options that *do work* rather than defer. CON-O-08 picks up CON-B-03 closure as a structural side effect.

---

## Dep-bumps + toolchain (incl. VS 2026)

### Q1: vcpkg vs stay-vendored

| Option | Description | Selected |
|--------|-------------|----------|
| Stay vendored | Phase 1-5 zero-pkg-manager posture preserved | |
| Introduce vcpkg now | Manifest mode; pays back in Phase 7-11; new CI dep | ✓ |
| Hybrid (vcpkg for new bumps only) | Two systems to understand | |

**User's choice:** Introduce vcpkg now.

### Q2: imgui docking branch

| Option | Description | Selected |
|--------|-------------|----------|
| Switch to docking branch | Required for TJT dockable subpanels per DEC-C4 | ✓ |
| Stay on imgui master | Bump 1.76 → 1.91; lose imgui docking primitives | |
| Defer to Wave-1 | Bump master in Phase 6; docking switch later | |

**User's choice:** Switch to docking branch. Gated on 06-01 overlay-debug per follow-up Q5.

### Q3: spdlog approach

| Option | Description | Selected |
|--------|-------------|----------|
| Bump with regression-test fence | spdlog 1.14 + Catch2 OutputSink round-trip test | ✓ |
| Pin to spdlog 1.11 (pre-1.12 fmt migration) | Less rework on log callsites | |
| Defer spdlog to V2 | Misses 'modernise deps' Phase-6 success criterion | |

**User's choice:** Bump with regression-test fence on OutputSink. Per CON-N-09 + [[feedback-max-harness]].

### Q4: PlatformToolset bump

| Option | Description | Selected |
|--------|-------------|----------|
| Bump to v143 (VS 2022) | Conservative single hop | |
| Bump to v144 (VS 2026) | Matches [[project-vs2026-toolchain]] local default | ✓ |
| Defer toolset bump | Stays on v142 in Phase 6 | |

**User's choice:** Bump to v144 (VS 2026 / Dev18).

### Q5: Overlay-never-displayed risk fold-in

| Option | Description | Selected |
|--------|-------------|----------|
| Capture as risk, keep locked decisions | Risk note; imgui bump proceeds | |
| Fold overlay-debug investigation into Phase 6 | Investigation precedes imgui bump | ✓ |
| Defer imgui bump until overlay verified | Skip imgui in Phase 6 | |
| Re-open imgui docking decision only | Bump master not docking | |

**User's choice:** Fold investigation. Becomes 06-01 plan.

**User-supplied context:** "Note, we have never seen the overlay display" — imgui in-game overlay rendered by `UtinniCore/swg/ui/imgui_impl.cpp` via D3D9 detour has never been visible in Utinni-injected sessions. This shaped both the docking-branch gating and the swg/ui/ dead-code cleanup deferral.

### Q6: Overlay-debug exit criterion

| Option | Description | Selected |
|--------|-------------|----------|
| Any imgui widget visible in-game | Minimum bar; render-only proof | |
| Interactive widget (button click) | Render + input proof | |
| Full Demo screen from imgui examples | Render + input + state-mgmt proof end-to-end | ✓ |
| Just RCA documented, no fix required | Cheapest gate; bump proceeds against documented baseline | |

**User's choice:** Full Demo screen (ImGui::ShowDemoWindow). Highest bar.

### Q7: Overlay-debug plan placement

| Option | Description | Selected |
|--------|-------------|----------|
| Its own plan: 06-01 | Clean dependency order, CI-gated | ✓ |
| Investigation task inside 06-02 dep-bumps plan | Tighter coupling | |
| Side-channel (no plan slot) | Findings don't land in tracked artifact | |

**User's choice:** Own plan (06-01).

### Q8: Risky-deps disposition (Catch2 + CppSharp)

| Option | Description | Selected |
|--------|-------------|----------|
| Catch2 + CppSharp stay vendored; other 5 migrate | Pragmatic carve-out (Phase 5 D-02 respected) | |
| Catch2 migrates, CppSharp stays vendored | 6 deps migrate | |
| Full migration as originally chosen (7 deps) | CppSharp port quality = planner research item; fallback to vendored if broken | ✓ |
| Defer vcpkg entirely | Reverts to stay-vendored | |

**User's choice:** Full migration (7 deps). CppSharp vcpkg-port quality becomes planner-research item.

### Q9: VSIX range update for v144

| Option | Description | Selected |
|--------|-------------|----------|
| Widen to [16.0,19.0) | Adds VS 2026 (18.0) support per CON-O-04 audit-then-widen | ✓ |
| Pin to [17.0,19.0) (drop VS 2019) | Loses [[project-vs2026-toolchain]] VS 2019 fallback | |
| Leave VSIX range, just bump toolset | Verify-then-widen-if-needed | |

**User's choice:** Widen to [16.0,19.0).

**Notes:** Bold answers throughout. Full vcpkg migration including just-vendored-Phase-5 Catch2 is a deliberate partial undo per Phase 5 D-02's explicit "broader vcpkg call deferred here" handoff. User flagged the overlay-never-displayed mid-area, which reshaped imgui-bump risk and swg/ui/ dead-code cleanup scope.

---

## Cleanup scope ambition

### Q1: `.clang-format` adoption

| Option | Description | Selected |
|--------|-------------|----------|
| Full-repo run, single commit | One giant cosmetic diff + .git-blame-ignore-revs | ✓ |
| New-code-only CI gate, no historical reformat | Preserves blame fidelity; org. resolution slow | |
| Full-repo run, one commit per directory | Less blame impact per-PR, more commits | |
| Defer .clang-format to V2 | Skip adoption in Phase 6 | |

**User's choice:** Full-repo run, single commit + .git-blame-ignore-revs entry.

### Q2: `Add*Callback` vs `Add*Call` naming consolidation

| Option | Description | Selected |
|--------|-------------|----------|
| Defer to V2 | Per [[feedback-caller-attrs-binary-compat]] + Phase 3 D-10 source-compat | ✓ |
| Rename in framework, paired commit in UtinniPlugins | Breaks Sytner (Phase 3 D-25 declared legacy) | |
| Rename only most-egregious (e.g. log Add typo) | Phase 3 R-E pattern extended | |

**User's choice:** Defer to V2. Phase 3 D-10 precedent preserved.

### Q3: swg/ui/ dead-code cleanup order

| Option | Description | Selected |
|--------|-------------|----------|
| swg/ui/ cleanups gated on 06-01 | Don't delete WIP-adjacent code before fix | |
| Skip swg/ui/ dead-code cleanup in Phase 6 entirely | Belt-and-suspenders; defer all of it | ✓ |
| Delete all dead code in any order | Trust git history as recovery | |

**User's choice:** Skip swg/ui/ entirely. Other dead-code targets (Launcher attachToVisualStudio block, utinni.cpp commented detours, render_world/client_world/io_win disabled bodies, empty stub files) land normally.

### Q4: TJT.ico ejection

| Option | Description | Selected |
|--------|-------------|----------|
| Eject in paired Utinni/UtinniPlugins commits | Cross-repo move per [[feedback-utinniplugins-authority]] | ✓ |
| Eject + add a neutral framework icon | Same as above + safety net for plugins without one | |
| Defer to V2 | Cosmetic; doesn't affect functionality | |

**User's choice:** Eject in paired commits. Note: my CONTEXT writeup folded in the "neutral framework icon" replacement anyway since plugins without their own icon would otherwise have no fallback; preserved as D-15.

### Q5: Build/config polish bundle

| Option | Description | Selected |
|--------|-------------|----------|
| All of them | All isolated single-line fixes; CI catches regressions | ✓ |
| All except Native.SendMessage int→IntPtr | Binary-compat risk per [[feedback-caller-attrs-binary-compat]] | |
| Typos + licenses only; defer Windows SDK + Prefer32Bit | Conservative scope | |

**User's choice:** All of them. Note: Native.SendMessage int→IntPtr flagged in CONTEXT `<specifics>` — planner must verify if `internal` (ship as-is) or `public` (add 1-arg shim).

### Q6: STAB-04 audit mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Doc walk + automated grep checks | Hybrid; CI gates future preservation regressions | ✓ |
| Pure doc walk, no automation | Cheaper, no 1.0→V2 regression protection | |
| Defer to phase verification step | No dedicated plan; cursory | |

**User's choice:** Doc walk + automated grep checks. `06-AUDIT.md` + grep tests in new or existing test project.

### Q7: Folded CI-flake todos plan placement

| Option | Description | Selected |
|--------|-------------|----------|
| Dedicated plan (06-XX) gated before 1.0 cut | CI-stability focus; gates 1.0-CI-green criterion | ✓ |
| Fold into 06-04 cleanups plan | Bundled with cleanups | |
| Fold into 06-01 overlay-debug (CI-investigation theme) | Coupled with overlay investigation | |
| Fold into 06-VERIFICATION.md (no fix) | Documented as known at 1.0, not fixed | |

**User's choice:** Dedicated plan. Now 06-04 in the plan structure (after 06-03 open-Qs and before 06-05 cleanups+audit).

**Notes:** Conservative on swg/ui/ dead-code; preserves potential overlay-debug breadcrumbs. Bold on polish bundle (ships all items). Audit mechanism is the most rigorous of the three options offered.

---

## 1.0 release surface

### Q1: Packaging shape

| Option | Description | Selected |
|--------|-------------|----------|
| CI-built ZIP via release workflow | Lowest friction; reproducible | |
| MSI installer (WiX or InstallShield) | Production-grade install experience | ✓ |
| Just the tag; users build from source | Lowest-effort; no artifact | |
| ZIP + packaging PowerShell script (no CI) | Manual artifact assembly | |

**User's choice:** MSI installer.

### Q2: Cross-repo bundling

| Option | Description | Selected |
|--------|-------------|----------|
| Bundle TJT inside Utinni release | Per [[project-wave1-tjt-subpanels]] + DEC-C4 "pair distribution" | ✓ |
| Separate TJT release in UtinniPlugins | Two downloads for users | |
| Bundle TJT only for Phase 11 V1 release | Phase 6 = framework only | |

**User's choice:** Bundle TJT inside Utinni MSI.

### Q3: MSI authoring tool

| Option | Description | Selected |
|--------|-------------|----------|
| WiX (open-source, MIT) | Matches Utinni's MIT distribution | ✓ |
| InstallShield (commercial) | License cost; overkill for OSS modding tool | |
| MSIX (modern Microsoft package) | Unproven for Win32-injection use case | |
| Inno Setup (alternative installer engine) | Free, simpler authoring; not technically MSI | |

**User's choice:** WiX.

### Q4: Installer behaviour

| Option | Description | Selected |
|--------|-------------|----------|
| Copy files only | Minimum bar; user configures cfg manually | |
| Copy + detect SWG client path + offer to seed utinni.cfg | Opt-in detection (default-off preserves CON-D-01 "ships blank") | ✓ |
| Copy + register file associations | Premature: Wave-1 plugins haven't shipped | |

**User's choice:** Copy + detect SWG client path + offer to seed (default-OFF checkbox). Preserves CON-D-01 spirit.

### Q5: TEST-04 Tier-4 doc shape

| Option | Description | Selected |
|--------|-------------|----------|
| Full residual enumeration | Per-scenario procedure + success criterion + last-verified SHA | ✓ |
| Minimal disposition note | Cheap; loses procedural value | |
| Per-area docs in docs/ai/ | Verbose; high maintenance | |

**User's choice:** Full residual enumeration. Extends `.planning/codebase/TESTING.md`.

### Q6: Version scheme + release-notes

| Option | Description | Selected |
|--------|-------------|----------|
| 1.0.0 + auto-generated release notes from git log | SemVer; conservative | ✓ |
| 1.0.0-beta for Phase-6, 1.0.0 final at Phase 11 V1 | Two GitHub Releases | |
| 1.0.0 only at Phase 11; Phase 6 has no release | Skips 'framework 1.0' moment entirely | |

**User's choice:** 1.0.0 SemVer. Note: superseded by Q8 (v1.0.0-rc.1 first, then promote to v1.0.0 after bake).

### Q7: Release sign-off

| Option | Description | Selected |
|--------|-------------|----------|
| Maintainer runs Tier-4 checklist + signs in 06-VERIFICATION.md | Manual UAT before tag push | |
| Auto-tag on master green after Phase-6 plans land | Trusts automation; skips Tier-4 manual | |
| Pre-release v1.0.0-rc.1 + bake period | Modern OSS conventions; adds calendar time | ✓ |

**User's choice:** v1.0.0-rc.1 + bake period. Maintainer-signed Tier-4 UAT lands in 06-VERIFICATION.md before the rc.1 tag (folded into CONTEXT D-24).

### Q8: MSI code signing

| Option | Description | Selected |
|--------|-------------|----------|
| Unsigned for V1 | SmartScreen warning acceptable; revisit in V2 | ✓ |
| Self-signed certificate | Doesn't bypass SmartScreen; signals 'we tried' | |
| Purchase Authenticode cert | $200-700/year; revisit when funding model clearer | |

**User's choice:** Unsigned for V1.

**Notes:** MSI choice was the bold one (despite my "overkill" framing); user wants production-grade install. WiX honours Utinni's MIT posture. Pre-release + bake is the modern OSS convention.

---

## Claude's Discretion

- Exact directory layout under `installer/` (flat vs structured).
- WiX 4 vs WiX 5 (latest stable at plan-time).
- vcpkg baseline pin (specific microsoft/vcpkg commit SHA).
- Bake period N days (7-14, planner picks).
- STAB-04 audit test home (single-test-project vs sibling).
- ImGuizmo target commit/tag.
- TJT pinning strategy in release.yml (submodule vs checkout-with-ref).
- UtinniForm default icon image (gear/wrench/lambda/generic).
- vcpkg-configuration.json registry strategy.
- MSI UpgradeCode GUID (stable across versions per WiX convention).

## Deferred Ideas

- Add*Callback / Add*Call naming consolidation (V2).
- swg/ui/ commented-detour cleanups (deferred pending post-overlay-fix audit).
- MSI file-type associations (V1 release at Phase 11 closure or later).
- Authenticode code-signing (V2).
- v1.0.0 final tag promotion (post-Phase-6 bake follow-up).
- D3D11 migration ([[project-d3d11-migration]] — future R-letter).
- TRE 0005/0006 support gap ([[project-tre-version-support-gap]] — Phase 7).
- Plugin hot-reload runtime flow (V2).
- isSafeToUse RVAs via UTINNI_API (when needed).
- Coverage tooling (V2).
- Tier 3 mock-D3D9 + recorded fixtures (V2 per ROADMAP).
- Author-new-content / mod packaging / community hub (Wave-3, V2+).
- MSIX modernization (rejected at D-20).
- MSI ProductCode versioning strategy (Claude's discretion in D-20).
