---
phase: 17-cppsharp-v145-hardening
plan: 01
subsystem: build-toolchain
tags: [cppsharp, v145, clang, binding-generation, redirect, docs, spike]
requires: []
provides:
  - CPPS-01 clang-capability spike script (documented negative result)
  - CPPS-02 supported-config doc (14.29 redirect = supported binding-gen config)
  - de-staled regen-bindings.md (corrected line count, version-line claim removed)
  - Program.cs self-describing doc-pointer comment above ConfigureCppSharpParserStl
affects:
  - .github/workflows/ci.yml (Wave-2 tripwires depend on this doc as accepted config)
  - Plan 03 ABI gate + --rebless tool (re-bless procedure stub points forward to it)
tech-stack:
  added: []   # no new dependency — pure docs + PowerShell report
  patterns:
    - "env-var / vswhere / default-path probe resolver discipline (mirrored from Program.cs)"
    - "read-only reporting script (never throws, never modifies)"
key-files:
  created:
    - tools/cppsharp-clang-capability-spike.ps1
    - docs/ai/cppsharp-v145-redirect.md
  modified:
    - docs/ai/regen-bindings.md
    - UtinniCoreDotNetGen/Program.cs
decisions:
  - "Spike confirms empirically: vendored clang 11 and latest released clang 19 both < the clang-20 the v145 STL requires → no released CppSharp parses v145 → harden-the-redirect (acceptance re-set)."
  - "The 14.29 parser-include redirect is documented as the SUPPORTED config, not a workaround; retiring it is gated on a future clang-20-bearing CppSharp release."
requirements-completed: [CPPS-01, CPPS-02]   # added 2026-06-30 (v2.1 audit hygiene; covered by 17-VERIFICATION)
metrics:
  duration: ~3 min
  completed: 2026-06-15
---

# Phase 17 Plan 01: CppSharp clang-capability spike + supported-config doc Summary

Ran and documented the CPPS-01 clang-capability spike (empirical negative result: no
released CppSharp parses the v145 STL), then lifted the parser-include redirect's
rationale out of `Program.cs` comments into a discoverable in-repo supported-config doc
(CPPS-02), de-staled `regen-bindings.md`, and added a self-describing doc-pointer comment
to `Program.cs` — stopping the binding-generation config being silently load-bearing.

## What was built

### Task 1 — CPPS-01 spike script (`tools/cppsharp-clang-capability-spike.ps1`, commit `0543ae6`)

A PowerShell-5.1-safe, read-only reporting script that:
- Enumerates installed MSVC toolsets via env (`UTINNI_VS2019_ROOT`) → vswhere
  (`-prerelease -products *`) → default-path probe — mirroring the `Program.cs` resolver
  discipline; **no hard-coded MSVC install path**.
- For each located `yvals_core.h`, extracts the `#if __clang_major__ < N` gate value.
- Tabulates N against CppSharp's bundled clang (11 vendored / 19 latest released v1.2).
- Prints the mechanical conclusion (`11 < 20` and `19 < 20` → no released CppSharp parses
  the v145 STL → harden-the-redirect; the 14.29 redirect is the supported config).
- Never throws, never modifies; exits 0.

Verified live output on this box: 14.29 → clang 11 (vendored parses YES), 14.51/14.52 →
clang 20 (vendored + latest both NO). Exit code 0. `__clang_major__` present; no hard-coded
MSVC path; no C++23 STL header token in comments (grep-gate hygiene, Pitfall 4).

### Task 2 — CPPS-02 supported-config doc + de-stale + Program.cs pointer (commit `5fe2893`)

- **`docs/ai/cppsharp-v145-redirect.md` (NEW, 118 lines):** records the spike result table,
  why the 14.29 redirect is load-bearing, the `_MSVC_STL_VERSION = 143` ABI-stability
  assumption (14.29-parsed bindings stay layout-correct for v145 builds), the toolchain
  prerequisites (VS 2019 14.29.30133, VS 2026 v145 14.51/14.52, Win10 SDK 19041), the
  `UTINNI_VS2019_ROOT` / `UTINNI_SLN_DIR` env overrides, and the retire-the-redirect exit
  (gated on a future clang-20 CppSharp).
- **`docs/ai/regen-bindings.md` (de-staled):** corrected "~5000+ lines" → "~27,600";
  removed the false "CppSharp version line" banner claim (the auto-generated banner carries
  no version string); replaced the raw-diff guidance with the per-block-hash ABI gate +
  `--rebless` lockstep re-bless procedure (pointing forward to Plan 03); cross-links the new
  supported-config doc.
- **`UtinniCoreDotNetGen/Program.cs` (comment-only):** self-describing header comment above
  `ConfigureCppSharpParserStl` pointing at the supported-config doc. `git diff` confirms ONLY
  added comment lines — resolver/`AddSystemIncludeDirs`/vswhere logic byte-identical.

## Verification

- `powershell -ExecutionPolicy Bypass -File tools/cppsharp-clang-capability-spike.ps1` → exit 0,
  prints per-MSVC clang-gate table + harden-the-redirect conclusion.
- Task-2 automated verify command (`! grep version line` && `grep -c 5000 == 0` &&
  `grep cppsharp-v145-redirect` in regen-bindings.md + Program.cs && doc exists) → `OK`.
- New doc = 118 lines (≥40); contains `_MSVC_STL_VERSION = 143`, `UTINNI_VS2019_ROOT`,
  and 14.29/v145/19041 prerequisites.
- `regen-bindings.md`: `grep -c 5000` = 0; `grep -ci "version line"` = 0.
- `git diff UtinniCoreDotNetGen/Program.cs` = added comment lines only.
- No file deletions in either commit; `Generated/UtinniCore.cs` never staged.

## Deviations from Plan

None — plan executed exactly as written. (The two `git` warnings about LF→CRLF on the new
files are cosmetic line-ending normalization, not content changes.)

## Notes for downstream

- The spike's documented negative result is the empirical record that formally re-sets the
  phase acceptance from "retire the redirect" to "harden the redirect" — Wave-2 plans (the
  CI tripwires, the ABI gate) build on this doc being the accepted, documented config.
- The re-bless procedure in `regen-bindings.md` is a forward-pointer stub: the `--rebless`
  diff tool and the frozen-plugin compose fixture it describes are built in **Plan 03**.

## Self-Check: PASSED
- FOUND: tools/cppsharp-clang-capability-spike.ps1
- FOUND: docs/ai/cppsharp-v145-redirect.md
- FOUND (modified): docs/ai/regen-bindings.md
- FOUND (modified): UtinniCoreDotNetGen/Program.cs
- FOUND: commit 0543ae6 (Task 1)
- FOUND: commit 5fe2893 (Task 2)
