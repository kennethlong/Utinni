---
phase: 06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut
plan: 06
subsystem: infra
tags: [wix, msi, installer, release, github-actions, tier-4, imgui, winforms, rc]

requires:
  - phase: 06-02
    provides: vcpkg manifest build, imgui 1.92.8 (docking), v145 toolset
  - phase: 06-05
    provides: clang-format gate, STAB-03 cleanups, STAB-04 preservation audit
provides:
  - TESTING.md Tier-4 manual residual enumeration (a)-(h) + CONVENTIONS.md cross-ref (TEST-04)
  - WiX 5 MSI installer (installer/) with opt-in SwgPathDetector custom action (CON-D-01 preserved)
  - release.yml tag-triggered (v1.0*) MSI build + GitHub Pre-release, TJT bundled at pinned SHA
  - maintainer-signed Tier-4 UAT (06-VERIFICATION.md, D-24)
  - v1.0.0-rc.1 tag + GitHub Pre-release
affects: [release, packaging, wave-1-tjt-subpanels, bake-period]

tech-stack:
  added: [WixToolset.Sdk 5.0.2, WixToolset.UI.wixext 5.0.2, WixToolset.Dtf.CustomAction 5.0.2, softprops/action-gh-release@v2]
  patterns: [WiX 5 SDK-style wixproj, DTF managed custom action, IncludeTjt preprocessor-gated payload, cross-repo checkout-with-ref pinning]

key-files:
  created:
    - installer/Utinni.Installer.wixproj
    - installer/Product.wxs
    - installer/CustomActions/SwgPathDetector.cs
    - installer/CustomActions/SwgPathDetector.csproj
    - installer/License.rtf
    - installer/README.md
    - .github/workflows/release.yml
    - .planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-06-MSI-TJT-PINNING.md
    - .planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-VERIFICATION.md
  modified:
    - .planning/codebase/TESTING.md
    - .planning/codebase/CONVENTIONS.md
    - Utinni.sln
    - .gitignore
    - UtinniCore/swg/ui/imgui_impl.cpp
    - UtinniCoreDotNet/UI/Controls/PanelGame.cs

key-decisions:
  - "OPT-B (checkout-with-ref) over submodule for TJT pinning — MSI builds only on release tags"
  - "release.yml runs on the self-hosted utinni-v145 runner, NOT windows-2022 (v145/VS2026 + VS2019 14.29 not on hosted images)"
  - "Installer projects added to Utinni.sln with ActiveCfg-only (no Build.0) so CI's x86 solution build never builds the MSI"
  - "UtINI.dll NOT bundled (it is a static lib, no DLL); input.ini NOT bundled (runtime-generated)"
  - "Clean-VM MSI round-trip, right-edge cursor-clip, and 2nd GPU vendor deferred to rc bake — not pre-release blockers"

patterns-established:
  - "WiX 5 DTF custom action: Microsoft.NET.Sdk net472 lib + WixToolset.Dtf.CustomAction PackageReference -> SfxCA .CA.dll"
  - "IncludeTjt preprocessor-gated payload via -p:IncludeTjt=true (NOT -p:DefineConstants, which clobbers RepoRoot)"
  - "Overlay game-input arbitration on io.WantCaptureMouse (not IsWindowHovered) so drags don't leak to SWG"

requirements-completed: [TEST-04, STAB-03, STAB-04, STAB-05]

duration: ~1 session
completed: 2026-05-25
---

# Phase 06 / Plan 06-06: v1.0.0-rc.1 Cut Summary

**Shipped the Utinni 1.0 release candidate: WiX 5 MSI installer (TJT bundled), tag-triggered GitHub release pipeline, the full Tier-4 manual-residual doc, and a maintainer-signed Tier-4 UAT that caught and fixed two real overlay bugs before tagging `v1.0.0-rc.1`.**

## Accomplishments

- **TEST-04 closed** — `TESTING.md` now carries the full eight-scenario (a)-(h) Tier-4 manual residual enumeration (each with procedure, success criterion, last-verified SHA, failure-mode escalation); `CONVENTIONS.md` cross-references it as the canonical boundary doc.
- **D-20 MSI** — `installer/` ships a WiX 5 MSI installing to `C:\Program Files (x86)\Utinni\` with a default-OFF opt-in `SwgPathDetector` custom action that preserves CON-D-01 (ships blank `ut.ini`/`utinni.cfg`; seeds `[Launcher]swgClientPath` only on a single unambiguous SWG match).
- **D-21 / D-22 release pipeline** — `release.yml` triggers on `v1.0*` tags, builds Release|x86 + TheJawaToolbox (pinned `kennethlong/UtinniPlugins@c9cfa9d`), bundles TJT into the MSI, and publishes a GitHub Pre-release. Local end-to-end validation of the full path caught two release.yml bugs before any tag push.
- **D-24 maintainer-signed Tier-4 UAT** — all 8 scenarios PASS on the dev machine; the manual walk caught two real overlay input/window bugs that CI could not (see below) and they were fixed CI-green pre-tag.
- **v1.0.0-rc.1 tagged + pushed** at the verified commit, triggering the Pre-release build.

## Task Commits

1. **Task 1: Tier-4 enumeration + CONVENTIONS xref (TEST-04)** — `ebad483` (docs)
2. **Task 2: WiX 5 MSI installer scaffold** — `c2d989c` (feat)
3. **Task 3: release.yml + TJT pinning** — `0f563de` (ci), corrected by `04f1e75` (fix — local-validated TJT staging path + IncludeTjt property)
4. **Task 4: Tier-4 UAT signoff** — `6f51b26` (docs); tag `v1.0.0-rc.1` at that commit

**UAT-found fixes (Task 4):**
- `061f4ad` — `fix(06-06)`: overlay game-input suspend keyed on `WantCaptureMouse` (drag-leak → SWG marquee-select)
- `29a128e` — `fix(06-06)`: guard `PanelGame` reposition against the minimized off-screen (-32000) origin

## Deviations & Findings

- **Plan file-list vs reality:** `UtINI.dll` does not exist (UtINI is a static lib) — not bundled; `input.ini` is runtime-generated — not bundled. SWG client path lives in `ut.ini` `[Launcher]swgClientPath`, not `utinni.cfg`.
- **release.yml bugs caught by local validation** (per [[feedback-max-harness]]): TJT's OutputPath targets the sibling `Utinni/bin/Release/Plugins/TheJawaToolbox`, not the UtinniPlugins tree; and `-p:DefineConstants=IncludeTjt` clobbered the `RepoRoot` WiX define (switched to `-p:IncludeTjt=true`).
- **Two overlay bugs caught by Tier-4 UAT** (drag-leak, minimize off-screen) — fixed + CI-green.
- **Deferred to rc bake:** (1) embedded right-edge cursor-clip dead zone (SWG clips cursor to its backbuffer rect vs the stretched panel — memory `swg-cursor-clip-stretch-deadzone`); (2) second GPU vendor coverage (no Intel/AMD hardware); (3) live MSI install/uninstall round-trip on a clean VM (validated non-invasively via `msiexec /a`).

## Self-Check: PASSED

- TESTING.md grep `Scenario \([a-h]\)` = 8; CONVENTIONS.md references TESTING.md (TEST-04 acceptance met).
- Local MSI build (no-TJT and TJT-bundled) produces `installer/bin/x86/Release/Utinni.msi`; File table = 19 files incl. both TJT DLLs; extracted configs CON-D-01 blank.
- `Utinni.sln` references `installer\Utinni.Installer.wixproj`; installer projects excluded from the Release|x86 build set (CI protected, verified).
- release.yml: valid YAML, triggers on `v1.0*`, bundles `kennethlong/UtinniPlugins`, prerelease on `-rc`.
- All 8 Tier-4 scenarios signed PASS in 06-VERIFICATION.md; CI green on master at every fix.
- v1.0.0-rc.1 tag pushed. **GitHub Pre-release artifact verification is appended to 06-VERIFICATION.md once release.yml completes (post-tag entry).**
