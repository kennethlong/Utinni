---
phase: 17-cppsharp-v145-hardening
verified: 2026-06-15T00:00:00Z
resolved: 2026-06-15T04:30:00Z
status: passed
score: 7/7 must-haves verified
overrides_applied: 0
re_verification:
  previous_status: gaps_found
  previous_score: 6/7
  resolution: >
    The sole blocking gap (CPPS-04a defense-layer-1) was resolved by satisfying the verifier's
    own criterion (a): a completed GREEN CI run on the final commits proving the regen-then-test
    contract. CI run 27523293929 (commit 8cc05b4) succeeded end-to-end — msbuild Release|x86 fired
    the UtinniCore post-build CppSharp generator (regenerating Generated/UtinniCore.cs in the
    working tree), then `dotnet test --no-build` read that fresh surface and AbiSurface +
    FrozenPluginCompose passed alongside the full net472 + native Catch2 + MCP lanes. The 20-block
    delta is confirmed BENIGN staleness of the 2026-05-24 committed bindings (the never-commit
    invariant means the committed Generated file is always stale; CI always regens first), NOT a
    real surface drift. Reproduced locally before the CI run: built UtinniCore Release|x86, ran
    UtinniCoreDotNetGen.exe, then `dotnet test --no-build --filter AbiSurface` → 8/8 green.
    A pre-existing master-red condition (clang-format 20.1.8 drift on directx9.cpp +
    direct_input.cpp, unrelated to phase 17 — gated before the build) was fixed in commit 8cc05b4
    to let CI reach the ABI lane.
gaps: []
deferred: []
---

# Phase 17: CppSharp / v145 Hardening Verification Report

**Phase Goal:** The binding-generation toolchain is an explicit, documented, CI-guarded configuration — and a binding regen can never silently break a pre-built plugin DLL.
**Verified:** 2026-06-15
**Status:** passed (initial verdict gaps_found → resolved via green CI run 8cc05b4; see Resolution Addendum)
**Re-verification:** Yes — gap closed by orchestrator investigation + CI confirmation

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | CPPS-01: A runnable, read-only spike script tabulates each MSVC `__clang_major__` STL gate vs CppSharp's bundled clang and concludes no released CppSharp parses the v145 STL → harden-the-redirect | ✓ VERIFIED | `tools/cppsharp-clang-capability-spike.ps1` (171 lines): `__clang_major__\s*<\s*(\d+)` extraction (line 96), vswhere/`UTINNI_VS2019_ROOT`/probe resolution (no hard-coded path, lines 46-61), zero write ops (read-only), prints "RE-SET from retire ... to harden the redirect" (line 167). Summary records live exit 0. |
| 2 | CPPS-02: A discoverable in-repo doc names the VS2019-14.29 parser-include redirect as the SUPPORTED config; regen-bindings.md de-staled; rationale lifted out of source comments | ✓ VERIFIED | `docs/ai/cppsharp-v145-redirect.md` (118 lines, ≥40) exists. `docs/ai/regen-bindings.md`: `grep -c 5000`=0, `grep -ci "version line"`=0, links `cppsharp-v145-redirect`. `Program.cs` carries a `cppsharp-v145-redirect` doc-pointer comment (review confirms comment-only change). |
| 3 | CPPS-03: TWO CI tripwires — (a) HARD-FAIL C++23-STL-header scan scoped to UtinniCore/, (b) WARN-loud clang-20 pin that never blocks (asymmetric severity, D-04) | ✓ VERIFIED | ci.yml:196-230 HARD-FAIL scan `throw`s on a denylisted header (line 228), scoped to `UtinniCore/` with `Generated`/external/build-dir excludes (line 207), reads `tools/allowed-cpp-stl-headers.txt` (24 headers). ci.yml:246-259 WARN-loud step has **0 `throw`**, committed pin (`$pinnedCppSharpClangMajor=19`), emits `::warning::` only. No `pull_request` trigger (line 10 is a documenting comment, not a key). |
| 4 | CPPS-04a: per-block-hash ABI diff ignores reorder churn but TRIPS on a real public-surface change (member/sig/EntryPoint/enum/layout) with a --rebless path | ✗ FAILED (partial) | TRIP half VERIFIED: negative [Fact]s `ReorderedEnumMember_IsReportedAsChanged`, `RenamedEnumMember_IsReportedAsChanged`, `AddedMember`, `RemovedMember`, `ChangedDllImportEntryPointMangledString` all PASS. PASS-on-committed-surface half FAILS: primary `GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn` is RED (20 REMOVED) on the committed tree; green only after a full-build regen. See Gaps. `--rebless` present (AbiBlockHash.Rebless + lockstep checklist, lines 548-590). |
| 5 | CPPS-04 CR-01 fix: enum members are captured (renumber/rename trips the gate) — the false-negative the review caught | ✓ VERIFIED | `AbiBlockHash.cs`: `EnumMemberRegex` (line 106) + `scopeIsEnum` scope stack (line 164) emit `ENUMMEMBER|<fqn>name=value` only inside enum scope (lines 201-213). Negative tests prove renumber 1→2 trips (removed≥1 AND added≥1) while pure reorder stays invisible (AbiSurfaceTests.cs:187-239), and rename trips (241-265). Baseline re-blessed 4386→4456 hashes (matches review). |
| 6 | CPPS-04b: a committed, NEVER-rebuilt frozen TJT DLL MEF-composes against the freshly-built bindings with zero LoadErrors | ✓ VERIFIED | `Fixtures/FrozenPlugin/TheJawaToolboxDotNet.dll` committed (360 KB), wired as `<Content CopyToOutputDirectory>` (csproj:64) NOT `<Compile>`, no rebuild csproj. `FrozenPluginComposeTests` PASSES (1/1) — `PluginLoader(autoLoad:false).Load` + `LoadErrors.Count==0` + `Assert.NotEmpty(Plugins)`. No Sytner fixture (D-02a honored). |
| 7 | Locked invariant: Generated/UtinniCore.cs is NOT committed by this phase | ✓ VERIFIED | None of the 9 phase-17 commits (0543ae6, 5fe2893, 7102491, 608bd26, edd48cc, 993f26f, f098586, aa12a29, 1bd2d84) touch `UtinniCoreDotNet/Generated/UtinniCore.cs`; its last-touching commit is d69988d (phase 06-02, 2026-05-24). checkout-- policy honored. |

**Score:** 6/7 truths verified (truth 4 partial → counted as not-verified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `tools/cppsharp-clang-capability-spike.ps1` | CPPS-01 spike | ✓ VERIFIED | 171 lines, `__clang_major__` gate, read-only, no hard-coded path |
| `docs/ai/cppsharp-v145-redirect.md` | CPPS-02 supported-config doc | ✓ VERIFIED | 118 lines (≥40), names 14.29 redirect as supported config |
| `docs/ai/regen-bindings.md` | de-staled regen doc | ✓ VERIFIED | 304 lines, no "5000"/no "version line", links the new doc |
| `tools/allowed-cpp-stl-headers.txt` | CPPS-03a denylist | ✓ VERIFIED | 48 lines, 24 C++23/C++20-late headers, outside scanned root |
| `.github/workflows/ci.yml` | CPPS-03a+b tripwires | ✓ VERIFIED | both steps present, asymmetric severity correct |
| `UtinniCoreDotNet.Tests/AbiBlockHash.cs` | ABI diff + enum + --rebless | ✓ VERIFIED | 665 lines, SHA256, enum/EntryPoint/FieldOffset/StructLayout keyed |
| `UtinniCoreDotNet.Tests/AbiSurfaceTests.cs` | [Fact]s incl. negatives | ⚠️ ORPHANED | 285 lines; negatives pass; primary baseline [Fact] FAILS on committed tree |
| `UtinniCoreDotNet.Tests/Fixtures/abi-baseline-blockhashes.txt` | blessed baseline | ⚠️ HOLLOW | 4456 hashes but 20 absent from committed surface (bless-vs-committed mismatch) |
| `UtinniCoreDotNet.Tests/FrozenPluginComposeTests.cs` | MEF-compose [Fact] | ✓ VERIFIED | 125 lines, `LoadErrors`, passes 1/1 |
| `UtinniCoreDotNet.Tests/Fixtures/FrozenPlugin/TheJawaToolboxDotNet.dll` | frozen fixture | ✓ VERIFIED | committed binary, Content-wired |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| regen-bindings.md | cppsharp-v145-redirect.md | markdown link | ✓ WIRED | `cppsharp-v145-redirect` grep hit |
| Program.cs | cppsharp-v145-redirect.md | header comment | ✓ WIRED | doc-pointer comment present |
| ci.yml | allowed-cpp-stl-headers.txt | scan reads denylist | ✓ WIRED | `$denyFile = "tools/allowed-cpp-stl-headers.txt"` (ci.yml:199) |
| AbiSurfaceTests.cs | Generated/UtinniCore.cs | ResolveGeneratedPath walk-up | ✓ WIRED | resolver reads the committed file — and that exposes the gap |
| FrozenPluginComposeTests.cs | PluginLoader.cs | Load + LoadErrors | ✓ WIRED | test passes |
| Tests.csproj | FrozenPlugin DLL | `<Content>` not Compile | ✓ WIRED | csproj:64 |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Frozen TJT MEF-composes | `dotnet test --no-build --filter FrozenPluginCompose` | Passed 1/1 | ✓ PASS |
| ABI diff trips on real change (negatives) | `dotnet test --no-build --filter AbiSurface` | 7 of 8 pass (negatives all green) | ✓ PASS |
| ABI diff passes on committed surface | `dotnet test --no-build --filter GeneratedSurface_MatchesBlessedBaseline` | FAILED — 20 REMOVED | ✗ FAIL |
| Full managed lane | `dotnet test --no-build UtinniCoreDotNet.Tests` | Failed:1 Passed:772 Total:773 | ✗ FAIL (1) |
| Generated file == committed HEAD | `git hash-object` vs `git rev-parse HEAD:...` | both 863d228 | ✓ PASS (clean) |

Note: the full lane was run `--no-build` against the committed tree. CI runs `msbuild` (which regenerates Generated/UtinniCore.cs) BEFORE `dotnet test --no-build`, so CI would read the fresh-regen surface the baseline encodes — but no completed green CI run on the final commits exists to confirm this.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| CPPS-01 | 17-01 | clang-capability spike documented negative result | ✓ SATISFIED | spike script + conclusion |
| CPPS-02 | 17-01 | 14.29 redirect documented as supported config | ✓ SATISFIED | new doc + de-staled regen-bindings + Program.cs pointer |
| CPPS-03 | 17-02 | CI fails fast on both unblock/regression signals | ✓ SATISFIED | two asymmetric-severity tripwires |
| CPPS-04 | 17-03 | regen cannot silently break a pre-built plugin DLL | ⚠️ PARTIAL | enum fix + frozen-compose verified; primary ABI baseline [Fact] red on committed tree |

All four declared requirement IDs (CPPS-01..04) are accounted for in plan frontmatter and REQUIREMENTS.md (lines 24-31, 118-121, all marked Complete). No orphaned requirements.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (none) | — | No TBD/FIXME/XXX in modified source | ℹ️ Info | clean |
| WR-04 / IN-01/02/03 (17-REVIEW.md) | — | deferred review items | ℹ️ Info | advisory, tracked for a future pass; not goal-blocking |

### Human Verification Required

The maintainer live-smoke (Task 4 of 17-03) was APPROVED by the maintainer (TJT MEF-composes live in an injected SWG.exe with no compose errors). Per the verification brief, treat as satisfied. No new human-only items beyond the gap below, which is a maintainer DECISION, not a UAT.

### Gaps Summary

The phase delivers strong, substantive work on three of four requirements and most of the fourth:
the spike (CPPS-01), the supported-config docs (CPPS-02), and both CI tripwires (CPPS-03) are
fully verified. The CR-01 enum-capture fix the review flagged is genuinely present and proven by
two negative facts that trip on enum renumber and rename — the exact false-negative the goal cares
about. The frozen-TJT MEF-compose gate (CPPS-04b, defense layer 2) passes.

The one BLOCKING gap is in CPPS-04a (defense layer 1): the committed ABI baseline
(`abi-baseline-blockhashes.txt`, 4456 hashes, blessed 2026-06-14 from a fresh regen) does NOT
match the committed `Generated/UtinniCore.cs` (blob 863d228, generated 2026-05-24). The primary
assertion `GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn` therefore FAILS
deterministically by 20 REMOVED blocks on a clean checkout running the documented
`dotnet test --no-build`. It passes ONLY when a full `msbuild Utinni.sln` regenerates the
working-tree Generated file first (the CI path: ci.yml:267 build regenerates → ci.yml:282
`--no-build` test reads the fresh surface). Because committing Generated/UtinniCore.cs is a locked
never-commit invariant, the gate's correctness rests entirely on the CI regen-then-test contract —
but there is NO completed/green CI run on the final phase-17 commits (the runs are queued). The
SUMMARY's and REVIEW's "773/773" was achieved after a full-build regen, not on the committed tree.

Resolution requires either (a) a confirmed green CI run on the final commit proving the
regen-then-test path is green, or (b) reconciling the 20-block bless-vs-committed-surface delta so
the contract is auditable. Until one of these is shown, the must-have "the ABI diff passes on
reorder-only churn" is not demonstrably true for the artifact set as committed, so the goal —
"a regen can never SILENTLY break a pre-built plugin DLL" — is not fully proven (a permanently-red
or regen-dependent layer-1 gate cannot reliably surface a real break to a maintainer who runs the
documented no-build test).

---

## Resolution Addendum (orchestrator, 2026-06-15)

The initial verdict was `gaps_found` (6/7) on a single blocking gap: CPPS-04a defense-layer-1
appeared RED because the primary `GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn`
[Fact] failed by 20 REMOVED hashes when run `--no-build` against the committed tree. The verifier
correctly identified the resolution criteria: prove the CI regen-then-test contract green, OR
reconcile the 20-block delta.

**Root cause (confirmed, not speculated):** The committed `Generated/UtinniCore.cs` (blob 863d228,
2026-05-24) is intentionally stale — committing it is a locked never-commit invariant. The ABI test
reads the on-disk generated file, so it MUST run after the generator regenerates it. The generator
is `UtinniCoreDotNetGen.exe`, fired by UtinniCore's post-build event — a plain `msbuild /t:Build`
on an already-built tree skips it (incremental build), which is why both the verifier's `--no-build`
run and an initial incremental build tested the stale file. The 20 blocks are real surface present
in the CURRENT headers (post-05-24 native additions) that the stale committed file lacks — benign
staleness, exactly what CI's regen-first contract is designed to absorb.

**Proof — local (CI contract reproduced by hand):** built `UtinniCore` Release|x86 → ran
`UtinniCoreDotNetGen.exe` (hardened VS2019-14.29 redirect, exit 0) → working-tree
`Generated/UtinniCore.cs` regenerated → `dotnet test --no-build --filter AbiSurface` → **8/8 PASS**.
Then `git checkout --` restored the generated file (invariant honored).

**Proof — CI (definitive):** A pre-existing master-red clang-format gate (clang-format 20.1.8 from
the VS2026/Dev18 toolchain flagging `directx9.cpp:595-596` + `direct_input.cpp:136,280-281` — recent
D-12/D-13 lines an older clang-format wrapped; the previous tip `4555384` failed identically) gated
before the build, so the ABI lane never ran. Fixed in commit **8cc05b4** (formatting only, no
behavior change). CI run **27523293929 (8cc05b4)** then completed **SUCCESS** (~13 min): build fired
the generator → `dotnet test --no-build` read the fresh surface → AbiSurface + FrozenPluginCompose
green alongside the full net472 + native Catch2 + MCP lanes. The regen-then-test contract is proven
green on the final commit. Criterion (a) satisfied; the 20-block delta is dispositioned benign.

**Updated truth 4:** ✓ VERIFIED. **Updated CPPS-04:** ✓ SATISFIED. **Final score: 7/7.**

_Verified: 2026-06-15_
_Verifier: Claude (gsd-verifier)_
_Resolution: Claude (execute-phase orchestrator) — CI 8cc05b4 green_
