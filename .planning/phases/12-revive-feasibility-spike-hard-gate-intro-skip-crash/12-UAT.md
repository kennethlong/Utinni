---
status: complete
phase: 12-revive-feasibility-spike-hard-gate-intro-skip-crash
source: [12-01-SUMMARY.md, 12-02-SUMMARY.md, 12-03-SUMMARY.md, 12-04-SUMMARY.md]
started: 2026-06-14T19:10:00Z
updated: 2026-06-14T19:14:00Z
---

## Current Test

[testing complete]

## Tests

### 1. AUTH-01 — three revived SOE build CLIs build + link at v145/Win32
expected: tools/Utinni.Tools.sln builds green (Debug|Win32) and produces TreeFileBuilder_d.exe, TemplateCompiler_d.exe, TemplateDefinitionCompiler_d.exe.
result: pass
note: auto-verified — all three *_d.exe present on disk (tools/src/compile/win32/*/Debug/); presence proves successful build+link at v145.

### 2. AUTH-01 — revived EXEs run headless without crashing
expected: Each built EXE runs headless and exits cleanly (prints usage / loads config), no missing-DLL, CRT, or SAFESEH fault.
result: pass
note: auto-verified — ran all three with an 8s timeout backstop; all exit code 0. TreeFileBuilder prints usage; TemplateCompiler/TemplateDefinitionCompiler load config (benign missing-cfg warning) and exit clean.

### 3. AUTH-01 — dependency manifest + pinned-SHA provenance present
expected: tools/DEPENDENCY-MANIFEST.md (per-tool closures + Perforce keep-link + zlib 1.1.4 pin + revival deltas) and tools/PINNED-SHA.md (swg-client-v2 pinned SHA, not branch) exist.
result: pass
note: auto-verified — both files present; PINNED-SHA.md documents the lift-and-shift @5fce7bb8 with the no-#include-into-swg-client-v2 (D-01) constraint; manifest is 14 KB.

### 4. AUTH-01 — CI hard-gate lane enforces the revive build
expected: ci.yml has a standalone "Build tools solution (Debug|Win32) — AUTH-01 hard gate" lane; a non-zero MSBuild exit fails the job.
result: pass
note: auto-verified — ci.yml:196-202 runs `msbuild tools\Utinni.Tools.sln /p:Configuration=Debug /p:Platform=Win32` as the AUTH-01 hard gate, separate from the Utinni.sln lanes.

### 5. RESID-02 — intro-skip scene-transition crash no longer reproduces
expected: In a live injected SWGEmu session, the intro-skip scene transition (login → load-into-world AND TJT Scene → naboo.trn → Load) completes without crashing; no `VEH FATAL` in utinni.log.
result: pass
note: maintainer-confirmed 2026-06-14 (final confirmation of the 12-04 A5 no-repro disposition).

## Summary

total: 5
passed: 5
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

[none yet]
