---
phase: 13-wrap-revived-compilers-as-cli-verbs-close-ot-tier-2
plan: 01
subsystem: infra
tags: [native-lift, v145, msvc, datatable, exporter, libxml2, perforce-stub, safeseh]

requires:
  - phase: 12-revive-feasibility-spike
    provides: tools/ lift pattern (git archive @5fce7bb8, Directory.Build.props shim, v145 build-lane, /SAFESEH:NO + CRT-compat deltas)
provides:
  - DataTableTool / ArmorExporterTool / CoreWeaponExporterTool exes green at v145/Win32 in tools/
  - sharedXml leaf lib + libxml2-2.6.7 prebuilt wired into Utinni.Tools.sln (31 -> 35 projects)
  - UTINNI_TOOLS_NO_PERFORCE Perforce source-stub + serverGame dead-alias redirect shim
affects: [13-04, 13-05, 13-06]

tech-stack:
  added: [libxml2-2.6.7 prebuilt static lib]
  patterns: [Perforce source-stub via global define, dead-alias include redirect shim]

key-files:
  created:
    - tools/src/_compat/serverGame/ServerObjectTemplate.h
    - tools/src/external/3rd/library/libxml2-2.6.7.win32/ (lifted)
    - tools/src/engine/shared/application/{DataTableTool,ArmorExporterTool,CoreWeaponExporterTool}/ (lifted)
  modified:
    - tools/Utinni.Tools.sln
    - tools/Directory.Build.props
    - tools/DEPENDENCY-MANIFEST.md
    - tools/src/engine/shared/application/ArmorExporterTool/src/shared/ArmorExporterTool.cpp (p4 stub)
    - tools/src/engine/shared/application/CoreWeaponExporterTool/src/shared/CoreWeaponExporterTool.cpp (p4 stub)

key-decisions:
  - "Perforce SOURCE-STUB (not keep-link): exporters call popen(p4) on their run() path, so the FATAL must be neutralized at source, unlike the template tools which keep-link but never invoke."
  - "serverGame is a dead include alias, not a server-side closure: the enums live in the Phase-12-lifted sharedTemplate; a 1-include redirect shim resolves it (D-02 escape-hatch stayed unused)."
  - "Debug exes carry the _d suffix (DataTableTool_d.exe etc.) — 13-04/13-05 wrappers must map verb -> actual exe name."

patterns-established:
  - "Dead-alias include redirect: tools/src/_compat/<deadroot>/<header>.h #includes the real lifted header; _compat is on the global Directory.Build.props include path."
  - "Perforce source-stub: #ifdef UTINNI_TOOLS_NO_PERFORCE early-return in p4 helpers (mirrors UTINNI_TOOLS_NO_SHAREDLOG)."

requirements-completed: [AUTH-06]

duration: ~75min
completed: 2026-06-04
---

# Phase 13 Plan 01: AUTH-06 native lift Summary

**The 3 AUTH-06 item-build natives (DataTableTool + the two item exporters) plus the new sharedXml/libxml2 leaf lib now build green at v145/Win32 in tools/Utinni.Tools.sln (35 projects) — the build foundation the Plan 13-05 BUILD verbs wrap.**

## Performance

- **Duration:** ~75 min (dominated by ~3× full-closure MSBuild rebuilds: the first session build + the Directory.Build.props-triggered rebuild)
- **Completed:** 2026-06-04
- **Tasks:** 3
- **Files modified/created:** 49 (lifted source trees + sln + props + manifest)

## Accomplishments
- All 6 tool exes (3 Phase-12 + 3 new AUTH-06) build + link green; the full `tools/Utinni.Tools.sln` is green (0 errors, 42 warnings) at v145/Win32.
- sharedXml leaf lib (already lifted in Phase 12, never wired) added to the sln + the libxml2-2.6.7 prebuilt closure lifted; sharedXml builds green standalone.
- The D-02 escape-hatch did **not** fire — all 3 are clean client lifts. The "server-taint" was an include-path alias, resolved with a redirect shim, not a multi-day server closure.

## Task Commits

1. **Task 1: lift sharedXml + libxml2 prebuilt; add to sln** — `3286f03` (build)
2. **Task 2: lift the 3 app vcxprojs; source-stub Perforce; build all green** — `f3ab5fa` (build)
3. **Task 3: update DEPENDENCY-MANIFEST.md; confirm CI auto-extends** — `7b4558d` (docs)

## Revival deltas applied (the real work — not a clean lift-and-drop)

1. **Perforce source-stub** (`UTINNI_TOOLS_NO_PERFORCE`): both exporters' `getFileFromPerforce`/`addFileToPerforce` no-op (return `true`) so the `FATAL("Cannot access Perforce")` on the headless `run()` path is unreachable. p4 = VCS bookkeeping (checkout/add), not data; the `.tpf`/`.iff` outputs are written by `fopen` independent of p4.
2. **serverGame dead-alias redirect** (`tools/src/_compat/serverGame/ServerObjectTemplate.h`): the exporters `#include "serverGame/ServerObjectTemplate.h"` for compile-time enum constants (`ArmorCategory_Last`, `XP_crafting`, `CT_weapon`, ...). The client-only corpus has no server tree; the shim redirects to the Phase-12-lifted `sharedTemplate/ServerObjectTemplate.h` where those enums physically live. Enum-only use → no serverGame link symbol.
3. **/SAFESEH:NO** (`ImageHasSafeExceptionHandlers=false`) on both exporter EXEs — the linked zlib 1.1.4 prebuilt predates Safe-SEH → LNK2026 under v145. (DataTableTool already carried it.)

## Deviations from plan

- The plan assumed the Directory.Build.props include-path shim already redirected `serverGame/...` (per RESEARCH). It did **not** — `serverGame/ServerObjectTemplate.h` failed C1083. Resolved by adding the explicit redirect shim + `tools/src/_compat` include root (a small, in-scope extension of the documented "include-path redirect shim" pattern; the enums were confirmed present in the lifted `sharedTemplate` exactly as RESEARCH predicted).
- The plan listed ArmorExporterTool at 14 ProjectReferences; the actual vcxproj has 13. Immaterial — all are in the sln, no serverGame ref.

## Gate-findings / notes for downstream

- **Debug exe naming:** all tools emit a `_d` debug suffix (`DataTableTool_d.exe`, etc.); Release would be `_r`. The exporters' internal `system("TemplateCompiler")` calls the **bare** name — Plan 13-05's wrapper must stage a correctly-named `TemplateCompiler.exe` (no suffix) + `tools.cfg` in the WorkingDirectory.
- **Byte-exact (D-09):** unchanged — no source→known-good reference pairs lifted here; the AUTH-06 byte-exact / cross-check gate is Plan 13-04/13-05's golden-fixture work.

## Verification

- `MSBuild tools/Utinni.Tools.sln /t:sharedXml /p:Configuration=Debug;Platform=Win32` → green (sharedXml.lib).
- `MSBuild tools/Utinni.Tools.sln /p:Configuration=Debug;Platform=Win32` → **Build succeeded, 0 errors** (all 35 projects); the 3 AUTH-06 `_d.exe`s produced.
- Grep gate: `grep -v '^#' DEPENDENCY-MANIFEST.md | grep -c -E "DataTableTool|ArmorExporterTool|CoreWeaponExporterTool|sharedXml|Perforce-stub"` = 10 (≥ 5).
- Zero ProjectReference/#include resolves into the lift-source working tree (the lone "swg-client-v2" token was a prose comment in the shim, reworded for grep-gate hygiene).
- CI AUTH-01 hard gate (whole-sln build) auto-extends — no ci.yml edit (ci.yml:176).
