---
phase: 12-revive-feasibility-spike-hard-gate-intro-skip-crash
plan: 02
subsystem: tools/ (revived SOE build CLIs)
tags: [auth-01, lift-and-shift, v145, templatecompiler, templatedefinitioncompiler, perforce, cpp20-port]
requires: ["12-01"]
provides:
  - "TemplateCompiler_d.exe / _r.exe (builds + links at v145/Win32)"
  - "TemplateDefinitionCompiler_d.exe / _r.exe (builds + links at v145/Win32)"
  - "tools/Utinni.Tools.sln (31 projects: 3 apps + 28-lib closure)"
  - "Per-tool Perforce disposition = KEEP-LINK (for 12-03 manifest)"
affects: [12-03]
tech-stack:
  added: [pcre 4.1 (libpcre.a), Perforce ClientAPI (libclient/librpc/libsupp), legacy_stdio_definitions.lib]
  patterns: [char16_t->wchar_t reinterpret_cast for Win32 W-APIs, CRT-compat shim for legacy prebuilt libs, keep-link Perforce]
key-files:
  created:
    - tools/src/engine/shared/application/TemplateCompiler/** (lifted)
    - tools/src/engine/shared/application/TemplateDefinitionCompiler/** (lifted)
    - tools/src/** (28-lib closure + pcre + perforce, lifted verbatim)
    - tools/src/external/3rd/library/perforce/UtinniP4CrtCompat.cpp (__tzname shim)
  modified:
    - tools/Utinni.Tools.sln (regenerated: 31 projects)
    - tools/Directory.Build.props (closure include roots + legacy_stdio_definitions.lib link)
    - tools/src/engine/shared/library/sharedTemplateDefinition/src/shared/core/Filename.cpp
    - tools/src/engine/shared/library/sharedTemplateDefinition/src/shared/core/TpfFile.cpp
    - tools/src/engine/shared/library/sharedTemplateDefinition/src/shared/core/TemplateData.cpp
    - tools/src/engine/shared/application/TemplateCompiler/build/win32/TemplateCompiler.vcxproj (SAFESEH:NO + shim)
    - tools/src/engine/shared/application/TemplateDefinitionCompiler/build/win32/TemplateDefinitionCompiler.vcxproj (SAFESEH:NO + shim)
key-decisions:
  - "Perforce = KEEP-LINK for both tools (it links at v145 with only CRT-compat shims; no stub needed; -compile path stays P4-free at runtime)."
  - "v145 retained for both tools (NO v143 fallback needed) — the only blockers were fixable source/CRT-compat deltas, which v143 would not have changed."
  - "sharedTemplateDefinition needed C++20 source ports (char16_t/wchar_t, const-correctness) — it is a tool-only lib never covered by the client's C++20 port."
requirements-completed: [AUTH-01]
duration: ~2h
completed: 2026-06-02
---

# Phase 12 Plan 02: TemplateCompiler + TemplateDefinitionCompiler at v145 — Summary

Completes the **AUTH-01 build hard gate**: all **three** SOE build CLIs (TreeFileBuilder from 12-01, plus TemplateCompiler and TemplateDefinitionCompiler) now build and link standalone at v145/Win32 from `tools/Utinni.Tools.sln`. The full solution (31 projects: 3 apps + a 28-lib shared/game serialization closure) builds clean; all three `*_d.exe` produced. **Tasks:** 4 (2 checkpoints auto-resolved under the maintainer's autonomous grant). **Files:** ~2700 lifted + 7 build/glue/port files.

## Hard-gate finding (deepened)
The template tools sit atop **nearly the whole shared+game serialization layer**: `archive`'s forced-PCH `FirstArchive.h` → `ArchiveUserRegistry.h` registers every game archive type (`sharedGame`, `sharedObject`, `sharedSkillSystem`, `swgSharedNetworkMessages`, …), so the *compile* closure is ~28 libs even though the apps are small. Reviving them required **real C++20 source porting** of a tool-only lib — confirming the 12-01 conclusion that these CLIs need porting, not clean lift-and-shift. (Note: the `../../../../../` includes in `ArchiveUserRegistry.h` are NOT broken — MSVC resolves them via `archive`'s own `..\..\include` `/I` entry; they resolved once the game libs were lifted at their correct relative positions.)

## Checkpoint resolutions (autonomous, per maintainer grant)

**Task 2 — Perforce keep-or-stub → KEEP-LINK (both tools).** Per the plan, I attempted the v145 link with the vendored P4 libs first. It links — the only failures were SAFESEH metadata and 2 legacy CRT symbols (below), all fixable without stubbing. Keep-link is the plan's lowest-effort option, keeps the apps verbatim, and satisfies threat T-12-04 (Utinni never invokes `-edit`/`-submit`; the byte-exact `-compile` path is P4-free at runtime).

**Task 3 — D-10 v145 stop-and-ask → v145 held, NO v143 fallback.** No tool dropped to v143. The blockers were fixable source/CRT-compat deltas (a v143 fallback would not have changed the char16_t/wchar_t language-level issues anyway).

## Deviations from Plan (all approved under autonomous grant)

**[Rule 4 — Source port] sharedTemplateDefinition C++20 conformance.** This tool-only lib was never part of the client's C++20 port, so it retained pre-C++20 patterns that v145 rejects:
- `Filename.cpp` / `TpfFile.cpp`: `Unicode::String` is `char16_t`-based under C++20; passing `.c_str()` to Win32 `SetCurrentDirectoryW`/`CreateDirectoryW` (which take `wchar_t* LPCWSTR`) → `reinterpret_cast<LPCWSTR>(...)` (both 16-bit on Windows). Building `Unicode::String` from `L"..."` (wchar_t) → `Unicode::narrowToWide("...")`.
- `TemplateData.cpp`: string-literal → `char*` is now ill-formed → `const char*`.

**[Rule 2 — Build config] /SAFESEH:NO on both template EXEs.** The 2002-era Perforce libs + `zlib.lib` (1.1.4) predate Safe-SEH → `LNK2026`. Same fix as 12-01's TreeFileBuilder. x86 image metadata only; no output-byte impact.

**[Rule 1 — CRT compat] legacy_stdio_definitions.lib + __tzname shim.** Linking the 24-year-old Perforce `libsupp.lib` against modern UCRT left 2 unresolved externals:
- `_fscanf` (UCRT made scanf inline) → `legacy_stdio_definitions.lib` (added globally in `Directory.Build.props` `<Link>`; harmless to TreeFileBuilder which doesn't reference it).
- `__tzname` (UCRT dropped the legacy TZ data export) → benign per-EXE shim `UtinniP4CrtCompat.cpp` (PCH-off). Only affects Perforce-side log-timestamp TZ text, which Utinni never exercises.

**[Rule 1 — Build glue] Directory.Build.props extended.** Added the full closure's `include/public` roots + pcre/perforce/boost/libxml include dirs (the lifted vcxprojs' `$(SolutionDir)..\..\` paths are wrong for `tools/`). Kept the 12-01 `UTINNI_TOOLS_NO_SHAREDLOG` decouple (benefits these tools too).

**Total deviations:** 4 (1 source port across 3 files, 1 build-config, 2 build-glue/compat). **Impact:** all three AUTH-01 tools green at v145; Perforce kept-linked; no v143 fallback.

## Inputs for 12-03's DEPENDENCY-MANIFEST.md
- **Per-tool toolset:** all three at **v145/Win32** (no v143 fallback).
- **Perforce disposition:** **keep-link** (both template tools); P4 reached only via unused `-edit`/`-submit`.
- **Pinned external versions:** zlib **1.1.4** (linked, byte determinant) + zlib 1.2.3 (headers), pcre **4.1** (libpcre.a, PCRE_STATIC), Perforce ClientAPI (libclient/librpc/libsupp, vendored).
- **Revival deltas to document:** borrowCompressor→ZlibCompressor port (12-01); sharedTemplateDefinition C++20 ports; /SAFESEH:NO (all 3 EXEs); legacy_stdio_definitions.lib + __tzname shim (template tools); the standalone `Directory.Build.props` shim.

## Self-Check: PASSED
- `MSBuild tools\Utinni.Tools.sln /p:Configuration=Debug /p:Platform=Win32 /m` exits 0; all three `*_d.exe` present.
- Per-tool builds verified individually green; both template tools also confirmed at the full-solution level.
- No lifted vcxproj references the live swg-client-v2 tree (D-01).
- `tools/compile/` git-ignored; no build outputs committed.

**Ready for 12-03** (DEPENDENCY-MANIFEST.md + CI wiring + byte-exact smoke — the latter needs maintainer-supplied known-good reference assets).
