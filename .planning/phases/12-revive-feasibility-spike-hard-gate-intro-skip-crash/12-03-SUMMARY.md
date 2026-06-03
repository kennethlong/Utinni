---
phase: 12-revive-feasibility-spike-hard-gate-intro-skip-crash
plan: 03
subsystem: tools/ (AUTH-01 manifest + CI + byte-exact)
tags: [auth-01, dependency-manifest, ci, byte-exact, d-09, gate-finding]
requires: ["12-01", "12-02"]
provides:
  - "tools/DEPENDENCY-MANIFEST.md (per-tool closures + Perforce + zlib pin + revival deltas)"
  - "tools/smoke/byte-exact-smoke.ps1 (reusable D-09 harness)"
  - ".github/workflows/ci.yml AUTH-01 tools build-lane (self-hosted v145)"
affects: [13]
tech-stack:
  added: []
  patterns: [standalone-sln CI build-lane, reference-pair-driven byte-exact harness, D-09 gate-finding]
key-files:
  created:
    - tools/DEPENDENCY-MANIFEST.md
    - tools/smoke/byte-exact-smoke.ps1
  modified:
    - .github/workflows/ci.yml
    - tools/src/engine/shared/library/sharedTemplate/build/win32/sharedTemplate.vcxproj
key-decisions:
  - "A1 reference-pair gate resolved as per-tool GATE-FINDINGS — no compatible source->known-good pair exists for any tool."
  - "TreeFileBuilder byte-exact blocked by asset reality: retail .tre corpus is Restoration v6000 (encrypted, wrong format); the one 0005 asset has no source/.rsp."
  - "AUTH-01 build hard gate enforced in CI as a standalone tools build-lane (D-07), separate from Utinni.sln."
requirements-completed: [AUTH-01]
duration: ~1h
completed: 2026-06-02
---

# Phase 12 Plan 03: Dependency manifest + CI + byte-exact — Summary

Closes the AUTH-01 hard gate's **documentation + enforcement** half (the **build** half landed in 12-01/12-02). Produces the per-tool dependency manifest, wires the standalone tools build into the self-hosted v145 CI runner, and resolves the byte-exact A1 gate honestly via per-tool gate-findings + a reusable harness. **Tasks:** 3 (Task 1 = the A1 checkpoint, resolved with the maintainer). **Files:** 2 created + 2 modified.

## What shipped
- **`tools/DEPENDENCY-MANIFEST.md`** (D-08): per-tool ProjectReference closures (12 / 25 / 26) + the `archive`-PCH header-dep note; the Perforce **keep-link** decision per template tool; the zlib **1.1.4** pin with the byte-exact-vs-CVE tension (T-12-02); pcre 4.1 / Perforce pins; the pruned dead-dep; and the full revival-delta list.
- **D-04 dead-dep prune:** removed the present-but-never-`#included` `perforce\include` path from `sharedTemplate.vcxproj` (all 3 configs); TemplateCompiler re-verified green after the prune.
- **CI (D-07):** `ci.yml` gains **"Build tools solution (Debug|Win32) — AUTH-01 hard gate"** on `[self-hosted, windows, x64, utinni-v145]`, standalone from the `Utinni.sln` lanes. Non-zero MSBuild exit fails the job → the revive gate is enforced continuously.
- **`tools/smoke/byte-exact-smoke.ps1`** (D-09): reusable harness — SHA256 `Get-FileHash` (binary) or narrowest-approved-banner-normalized text, dump-both-on-mismatch, **NO** structural/round-trip fallback. Parameterized; activates the instant a compatible reference pair is supplied.

## A1 reference-pair checkpoint (Task 1) — resolved as GATE-FINDINGS
The maintainer pointed at `D:\Sample-TRE-Files` (46 retail `.tre`). Survey result: **all are Restoration v6000** (`EERT6000`, encrypted payloads) — a *newer* format than the 2002-era TreeFileBuilder emits (`0005`/`0006`) and not source-extractable, so they **cannot** serve as byte-exact references. The only format-matching asset (`swg-client-v2`'s `retail_mini_0005.tre`, `EERT0005`) has **no source tree and no `.rsp`**, and the original `.rsp` (file order + per-file compression) is unrecoverable from a `.tre`, so a byte-exact rebuild cannot be constructed. There are **zero** `.tpf`/`.tdf` for the template tools.

Disposition (per D-09, "surface and resolve, not a free pass"):
- **TreeFileBuilder** → gate-finding (v6000-incompatible corpus + 0005-asset-has-no-source). Resolution: a 0005/0006 source set + `.rsp`.
- **TemplateCompiler** → gate-finding (no `.tpf`+`.iff`). Resolution: a `.tpf` + SOE-produced `.iff`.
- **TemplateDefinitionCompiler** → gate-finding (no `.tdf`+C++; banner-normalization TBD). Resolution: a `.tdf` + generated C++ + the approved Pitfall-6 regex.

All three retire in **Phase 13** (revive+wrap), where the tools become `utinni-cli` verbs and real assets flow through the existing golden-fixture harness.

## Deviations from Plan

**[Plan-anticipated] Byte-exact smokes are gate-findings, not green smokes.** The plan's Task 2 explicitly provided for this ("for any tool marked gate-finding in Task 1, write the finding into DEPENDENCY-MANIFEST.md instead of a passing smoke"). No real reference pair was committable, so no live smoke runs in CI yet; the build hard gate carries the continuous enforcement, and the harness is staged. **No CI smoke steps added** (no green smoke to wire) — also plan-anticipated.

**Total deviations:** 0 unplanned. The gate-finding outcome is the plan's designed D-09 branch.

## Self-Check: PASSED
- `tools\Utinni.Tools.sln` builds green at Debug|Win32 (all 3 `*_d.exe`) — re-verified after the `sharedTemplate` dead-dep prune.
- Manifest verify: contains all 3 tools + zlib/1.1.4/Perforce. CI verify: `tools\Utinni.Tools.sln` + `AUTH-01` + `Platform=Win32` present. Smoke harness: `Get-FileHash`, dump-on-mismatch, no fallback.
- `tools/compile/` git-ignored; no build outputs committed.

## Phase 12 build-track (AUTH-01) — COMPLETE
All three SOE build CLIs build + link green at v145/Win32 from a standalone Utinni-owned `tools/` tree, CI-enforced. **The hard-gate finding:** these tools are revivable but require real *porting* (engine-API drift, C++20 conformance, SAFESEH, CRT-compat) and a `Directory.Build.props` standalone-build shim — not clean lift-and-shift. Byte-exact verification is parked behind documented, resolvable gate-findings (no compatible reference assets yet). **AUTH-01 build gate: PASSED.** Remaining phase work: **12-04 (RESID-02 live intro-skip crash)** — deferred to a live injected SWGEmu session.
