---
phase: 17-cppsharp-v145-hardening
plan: 02
subsystem: build-toolchain
tags: [cppsharp, v145, clang, ci, tripwire, grep-gate, security]
requires:
  - "17-01 harden-the-redirect acceptance (the supported 14.29 redirect config these tripwires guard)"
provides:
  - CPPS-03a C++23-STL-header HARD-FAIL CI scan (throws if UtinniCore C++ adopts a 14.29-unparseable header)
  - CPPS-03b clang-20 CppSharp pin tripwire (WARN-loud, never blocks; committed pin, not a live probe)
  - tools/allowed-cpp-stl-headers.txt denylist (out-of-band-refreshed, lives outside the scanned root)
affects:
  - .github/workflows/ci.yml (two new verify-only steps slotted beside the redirect-verify steps)
  - Plan 03 ABI gate (shares the same self-hosted, push-only, verify-only CI lane)
tech-stack:
  added: []   # no new dependency — pure CI YAML + a denylist text file
  patterns:
    - "verify-only CI step idiom (PowerShell 5.1, throw==hard-fail / Write-Host ::warning::==warn-loud)"
    - "clang-format style-gate include/exclude scan idiom (Get-ChildItem -Recurse + $excludePattern)"
    - "committed pin asserted in CI instead of a live (spoofable) network probe (D-03)"
    - "grep-gate hygiene: denylist tokens kept OUTSIDE the scanned root (tools/, not UtinniCore/)"
key-files:
  created:
    - tools/allowed-cpp-stl-headers.txt
  modified:
    - .github/workflows/ci.yml
decisions:
  - "D-04 asymmetric severity honored: C++23-header scan HARD-FAILS (throw); clang-20 pin tripwire WARN-loud (::warning::, never throws)."
  - "D-03 honored: clang-20 tripwire asserts a COMMITTED inline pin (CppSharp v1.2 / clang 19), no live network probe / NuGet / GitHub-API egress."
  - "Scan scoped to #include lines under UtinniCore/ only, excluding external/, Generated, and build-output dirs (grep-gate hygiene, RESEARCH Pitfall 4); denylist lives under tools/ so it does not self-trip."
requirements-completed: [CPPS-03]   # added 2026-06-30 (v2.1 audit hygiene; covered by 17-VERIFICATION)
metrics:
  duration: ~18 min
  completed: 2026-06-15
---

# Phase 17 Plan 02: CPPS-03 fail-fast CI tripwires Summary

Added the two CPPS-03 fail-fast CI tripwires beside the existing redirect-verify steps in
`.github/workflows/ci.yml`: (a) a HARD-FAIL scan that throws if UtinniCore C++ adopts a
C++23 STL header the VS 2019 14.29 parser-include redirect cannot parse, and (b) a
WARN-loud clang-20 pin tripwire that fires as good-news when a CppSharp release newer than
the committed clang-19 baseline would make native-v145 binding generation reachable. The
severity split is asymmetric and locked by D-04: the header scan blocks master; the
clang-20 pin never blocks master.

## What was built

### Task 1 — CPPS-03a C++23-STL-header HARD-FAIL scan + denylist (commit `7102491`)

- **`tools/allowed-cpp-stl-headers.txt` (NEW, 48 lines):** a denylist of 9 C++23 STL
  headers + 15 C++20-late STL headers the 14.29/clang-11 redirect cannot parse, with a
  comment header documenting the grep-gate-hygiene rationale (the file lives under
  `tools/`, OUTSIDE the scanned root, so naming the header tokens here does NOT self-trip
  the scan — RESEARCH Pitfall 4) and the out-of-band refresh procedure.
- **`.github/workflows/ci.yml` (new step):** "Scan UtinniCore for 14.29-unparseable C++23
  STL headers (HARD-FAIL)". Loads the denylist (skips `#`/blank lines), enumerates
  `*.cpp/*.h` under `UtinniCore/` ONLY via the clang-format step's
  `Get-ChildItem -Recurse` + `$excludePattern` idiom (excludes `external/`, `Generated`,
  `bin/obj/Release/Debug/RelWithDbgInfo`, `.vs`, `vcpkg_installed`, `packages`,
  `TestResults`), matches `^\s*#\s*include\s*<NAME>` against the denylist, and `throw`s
  (HARD-FAIL, non-zero exit) on any hit — mirroring the style-gate throw. Paths read via
  `-LiteralPath` (T-V5 path-injection mitigation). Placed after the 14.29-verify step,
  before "Setup MSBuild".
- **RED-PATH PROOF:** dropped a throwaway `UtinniCore/.abi-scan-selftest.tmp.h` containing
  `#include <format>`, ran the scan logic → it flagged the file and reported
  `VIOLATIONS=1` (gate is live, not a dead regex); on the clean tree (temp removed) the
  scan reports `VIOLATIONS=0` / `SCAN CLEAN`. Both the temp header and the temp self-test
  script were deleted and **never committed** (verified absent before staging).

### Task 2 — CPPS-03b clang-20 CppSharp pin tripwire (WARN-loud) (commit `608bd26`)

- **`.github/workflows/ci.yml` (new step):** "Tripwire — CppSharp clang-20 release pin
  (WARN-loud, never blocks)". Asserts a COMMITTED inline pin
  (`$pinnedCppSharpClangMajor = 19`, CppSharp v1.2) against the redirect-retirement
  threshold (`$redirectRetiredThreshold = 20`, the v145 STL `yvals_core.h` clang gate) —
  NO live network probe, NO NuGet/GitHub-API call, NO new egress (D-03). On the unblock
  path (pin >= 20) it emits `Write-Host "::warning::..."` signaling that native-v145 is now
  reachable and the redirect can be reviewed for retirement; otherwise it prints a benign
  "no clang-20 release pinned yet" line. The step contains NO `throw` — a clang>=20 release
  is good news and must never turn master red (D-04). An inline comment documents that
  refreshing the pin is an out-of-band manual/scheduled maintainer edit (D-03). Placed
  beside CPPS-03a, before "Setup MSBuild".

## Verification

- **CPPS-03a scan, clean tree:** simulated run reports `SCAN CLEAN` / `VIOLATIONS=0` — the
  step PASSES on today's tree (RESEARCH Pitfall 5: zero C++23-risky headers in UtinniCore/;
  confirmed independently by a Grep of `UtinniCore/**/*.{cpp,h}` for all 24 denylist tokens
  → no matches).
- **CPPS-03a RED-path:** simulated run with a temp `#include <format>` under a scanned path
  reports `SCAN FLAGGED` + `VIOLATIONS=1` and names the offending file — proving the gate
  is not dead.
- **CPPS-03b:** `grep -c "::warning::"` = 3 (the new emit + 2 pre-existing vcpkg-retry
  warnings); the new step body (ci.yml:246-259) contains `::warning::` and NO `throw`;
  current-path simulation (pin 19 < 20) prints the benign line and exits clean.
- **No-fork-PR invariant preserved:** the `on:` block has no `pull_request:` key (verified
  via a regex assert on the raw YAML). The single literal `pull_request` occurrence in the
  file (line 10) is the pre-existing locked-invariant *comment* documenting the deliberate
  absence of the trigger — not a trigger key (see Deviations).
- **Self-Check:** both committed files exist; both commit hashes resolve in `git log`.
- No file deletions in either commit (`git diff --diff-filter=D HEAD~1 HEAD` empty for
  both). `Generated/UtinniCore.cs` never staged. No temp/self-test artifacts left in the
  tree.

## Deviations from Plan

None functional — plan executed as written. One documented note on a verify command:

- **Task 2 acceptance "`grep -c "pull_request" .github/workflows/ci.yml` returns 0":** the
  file already carries ONE `pull_request` token at line 10, in a pre-existing comment
  ("No pull_request trigger by design...") that documents the locked no-fork-PR RCE
  invariant. That comment predates this plan and is NOT a trigger. The substantive
  invariant — "no `pull_request` *trigger* / no `pull_request:` key in the `on:` block" —
  IS satisfied (verified by a regex assert that the `on:` block has no `pull_request:`
  key). I did not delete the invariant-documenting comment, as removing it would erase the
  rationale for the locked security posture. The literal grep-count=0 phrasing was not
  achievable without deleting that comment; the underlying threat-model intent (T-17-04)
  is fully met.

## Threat Model Coverage

| Threat ID | Mitigation delivered |
|-----------|----------------------|
| T-17-04 (RCE / untrusted PR on self-hosted runner) | No `pull_request` trigger added; `on:` block remains push-only + workflow_dispatch. |
| T-17-05 (header-scan path-injection) | Scan scoped to a fixed root (`UtinniCore/`) with an explicit exclude set; file reads via `-LiteralPath`. |
| T-17-06 (clang-20 tripwire trusting a spoofed registry response) | Committed inline pin asserted (D-03); no live network probe / egress introduced. |

## Notes for downstream

- Plan 03's ABI gate + frozen-DLL MEF-compose tests run in the same self-hosted,
  push-only, verify-only CI lane these two steps now live in; they should slot near these
  steps or in the net472 test lane.
- Refreshing `tools/allowed-cpp-stl-headers.txt` (or the `$pinnedCppSharpClangMajor` value)
  is an out-of-band maintainer edit — kept in lockstep with the redirect's STL version in
  `docs/ai/cppsharp-v145-redirect.md` (created in 17-01).

## Self-Check: PASSED
- FOUND: tools/allowed-cpp-stl-headers.txt
- FOUND (modified): .github/workflows/ci.yml
- FOUND: commit 7102491 (Task 1)
- FOUND: commit 608bd26 (Task 2)
- No C++23 STL header token under UtinniCore/ (Grep of all 24 denylist tokens → 0 matches; no grep-gate self-trip)
