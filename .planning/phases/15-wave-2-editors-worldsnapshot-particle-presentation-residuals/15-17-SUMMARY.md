---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 17
subsystem: build-handoff
tags: [gap-closure, release-gate, injection-build, content-verification, netstandard-facade, utinni-cli, wave-5]
requires: ["15-12", "15-13", "15-14", "15-15", "15-16"]
provides:
  - "Reassembled + COMPLETE injection build at D:/Code/Utinni/bin/Release/ carrying every wave-5 fix, now shipping the two previously-missing files (netstandard.dll façade + utinni-cli.exe + net472 closure)"
  - "Green full automated gate (managed + native) with zero regression after the wave-5 gap fixes"
  - "Wave-5 gap-closure note + 15-18 live re-smoke pointer recorded in 15-SMOKE.md (annotated, original defect evidence preserved)"
affects:
  - "15-18 maintainer live-SWG re-smoke (B5–B8, Checklist C full incl C3, Checklist D resume against the fixed + complete binaries)"
tech-stack:
  added: []
  patterns:
    - "Content-of-fix gate via reflection-only type/method enumeration + a PS5-safe byte-string grep of the DEPLOYED PEs (anti-stale; mtime is only a weak secondary signal)"
    - "Tolerant ReflectionOnlyLoadFrom enumeration: catch ReflectionTypeLoadException and read its .Types (deps not auto-resolved under reflection-only load)"
key-files:
  created:
    - ".planning/phases/15-wave-2-editors-worldsnapshot-particle-presentation-residuals/15-17-SUMMARY.md"
  modified:
    - ".planning/phases/15-wave-2-editors-worldsnapshot-particle-presentation-residuals/15-SMOKE.md"
  artifacts:
    - "D:/Code/Utinni/bin/Release/UtinniCoreDotNet.dll (rebuilt — defines InjectedAssemblyResolver + WorldSnapshotCommandGuard + UndoRedoManager.Clear; no A9-diag)"
    - "D:/Code/Utinni/bin/Release/netstandard.dll (B5 façade, deployed via 15-12 csproj item)"
    - "D:/Code/Utinni/bin/Release/utinni-cli.exe + net472 dependency closure (B7)"
    - "D:/Code/Utinni/bin/Release/Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll (rebuilt — carries 15-15 LocateCli probe + 15-16 polish)"
decisions:
  - "Task 1 produces gitignored build output (bin/Release/) + the utinni-cli closure copy; the deliverable is the on-disk content-verified layout, so there is no source commit for Task 1 — only Task 2's 15-SMOKE.md annotation is committed alongside STATE.md/ROADMAP.md."
  - "Content-of-fix gate via .NET Framework ReflectionOnlyLoadFrom (+ tolerant ReflectionTypeLoadException.Types) + an ASCII byte-string grep, because Windows PowerShell 5.x lacks System.Reflection.Metadata and [Text.Encoding]::Latin1; mirrors the 15-11 precedent (the plan explicitly permits MetadataReader OR ildasm/strings)."
  - "Did NOT overwrite bin/Release/UtinniCoreDotNet.dll (or PathContainment.dll/netstandard.dll) with the utinni-cli net472 copies — the inject-root canonical binaries are built directly to bin/Release by the csproj OutputPath; only the genuinely-new CLI exe + its non-overlapping deps were copied in."
metrics:
  duration: ~22 min
  completed: 2026-06-13
---

# Phase 15 Plan 17: Wave-5 Gap-Closure Release Gate + Reassembled COMPLETE Injection Build Summary

Reassembled the deployable Release|x86 injection build at `D:/Code/Utinni/bin/Release/` after the wave-5 gap fixes (15-12 netstandard resolver, 15-13 C3 embed re-assert watchdog, 15-14 finalized A9 revert, 15-15 LocateCli inject-root probe, 15-16 managed polish), re-ran the full automated gate green with zero regression, **completed the build with the two previously-missing files** (`netstandard.dll` façade for B5; `utinni-cli.exe` + its net472 dependency closure for B7), content-verified the DEPLOYED PEs carry every fix (not just a fresh mtime), and recorded the wave-5 gap-closure + 15-18 live re-smoke pointer in `15-SMOKE.md`.

## What Shipped

**Task 1 — Full Release gate + reassembled, content-verified, COMPLETE injection build:**
- `Utinni.sln` Release|x86 (VS2026 MSBuild v145) → **Build succeeded, 0 errors**. `UtinniCoreDotNet` builds directly into `bin/Release/` (csproj `OutputPath`); the 15-12 csproj `Content` item drops `netstandard.dll` next to it; `Utinni.Cli` builds `utinni-cli.exe` + closure to `Utinni.Cli/bin/Release/net472/`.
- `TheJawaToolbox.sln` Release|x86 (UtinniPlugins paired rebuild) → **Build succeeded, 0 errors**. The managed `TheJawaToolboxDotNet.dll` (carrying 15-15 + 15-16) rebuilt into `bin/Release/Plugins/TheJawaToolbox/`; the C++ `TheJawaToolbox.dll` had no wave-5 source change (15-15/15-16 were C#-only) so MSBuild correctly skipped it.
- Full automated gate, **zero regression** vs the baseline (the wave-5 facts are additive):
  - `UtinniCoreDotNet.Tests` (Release): **706 passed / 0 failed** (≥ 697 baseline + the new `InjectedAssemblyResolver` + node-only guard facts).
  - `Utinni.Cli.Tests` (Release): **249 passed / 2 skipped (fixture-gated) / 0 failed**.
  - `Utinni.Mcp.Tests` (net10, Release): **77 passed / 0 failed**.
  - Native `UtinniCore.Tests.exe` (full): **84 assertions / 27 cases**.
  - Native `UtinniCore.Tests.exe [resid04]`: **8 assertions / 1 case** (the D-13 no-Reset gate held after the 15-13 window watchdog).
- `Generated/UtinniCore.cs` CppSharp churn reverted (`git checkout --`; never committed). Working tree clean except `15-SMOKE.md` (the plan-directed annotation target).
- `bin/Release/` reassembled and **completed**: base layout (`Launcher.exe`, `UtinniCore.dll`, `UtinniCoreDotNet.dll`, `UtinniCoreDotNet.PathContainment.dll`, `Plugins/TheJawaToolbox/{TheJawaToolbox.dll, TheJawaToolboxDotNet.dll, Resources/, input.ini, settings.ini}`) PLUS the two new files: `netstandard.dll` (B5 façade, next to `UtinniCoreDotNet.dll`) and `utinni-cli.exe` + its full net472 dependency closure (`CommandLine.dll`, `Newtonsoft.Json.dll`, `System.Collections.Immutable.dll`, `System.Reflection.Metadata.dll`, `utinni-cli.exe.config`).
- **Content-of-fix gate PASSED (binding anti-stale gate, T-15-17-01 + T-15-17-02 mitigation):** via reflection-only type/method enumeration + a PS5-safe byte-string grep of the DEPLOYED PEs —
  - deployed `UtinniCoreDotNet.dll` defines `InjectedAssemblyResolver` (15-12), AND defines `WorldSnapshotCommandGuard` (15-09 carry-over), AND `UndoRedoManager` exposes a public `Clear` method (15-10 carry-over), AND contains **no** `A9-diag` string (15-14 stripped the diagnostics);
  - `netstandard.dll`, `utinni-cli.exe` + its net472 closure (T-15-17-02: the whole payload, not just the exe), and the deployed `Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll` are all present.

**Task 2 — Wave-5 gap-closure note + 15-18 re-smoke pointer in 15-SMOKE.md:**
- Appended a dated `WAVE-5 GAP-CLOSURE 2026-06-13 (plans 15-12 … 15-17)` section just before the Maintainer Sign-Off block, stating: B5 fixed in 15-12 (inject-root `AssemblyResolve` handler + `netstandard.dll` shipped, re-enables saves for every editor + unblocks Checklist D); C3 fixed in 15-13 (250 ms `embedWatchdogTimer` re-asserts the owned-popup embed, window-side only, no device Reset, `[resid04]` green); A9 revert finalized in 15-14 (`[A9-diag]` stripped; live-node-by-id + optional-obj); B7 fixed in 15-15 (inject-root `LocateCli` probe) + 15-17 (utinni-cli.exe + closure shipped); B4/B5 grid-rebind, B6 no-hook preview tooltip, A7 delete-confirm candor + `BulkDelete` `DetailLevelChanged` folded into 15-16; the COMPLETE build is reassembled + content-verified at `bin/Release/`; the live re-smoke (B5–B8, Checklist C full incl C3, Checklist D) is the maintainer's continuation under 15-18, results back in this same `15-SMOKE.md`.
- The note **annotates** — it does not rewrite. Original defect evidence preserved verbatim (5 original `0xC0000005` records intact; the B5 "Could not load file or assembly" string intact; the B5/B7/C3 defect blocks + the A9 RE-VERIFY PASS section untouched). The Maintainer Sign-Off block is unchanged (still unsigned). This plan does NOT sign off the phase.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Content-of-fix verify command engine swapped for a PowerShell 5.x-compatible path**
- **Found during:** Task 1, step 5 (content verification).
- **Issue:** The plan's `<automated>` verify command uses `[Text.Encoding]::Latin1` (not present in Windows PowerShell 5.x → `ArgumentNullException`) and a naked `ReflectionOnlyLoadFrom().GetTypes()` (throws `ReflectionTypeLoadException` because reflection-only load does not auto-resolve `UtinniCoreDotNet`'s dependencies). The plan's strings-grep for a bare `Clear` token also conflates the `UndoRedoManager.Clear` member with the unrelated common token.
- **Fix:** Implemented the gate via `ReflectionOnlyLoadFrom` + a tolerant `catch [ReflectionTypeLoadException] { $_.Exception.Types }` (the standard pattern — successfully-loaded types remain available), exact-name matching of the `InjectedAssemblyResolver` / `WorldSnapshotCommandGuard` types and the `UndoRedoManager.Clear` method (precise, stronger than a strings grep), plus an `[Text.Encoding]::ASCII` byte-string grep for the `A9-diag` absence check. This is the same engine the 15-11 reassembly used and is explicitly permitted by the plan ("MetadataReader OR ildasm/strings grep").
- **Files modified:** none committed (verification-only temp script `/tmp/verify-content.ps1`, removed after use).
- **Commit:** n/a (gate logic, not a source change).

### Scope/commit note (not a deviation)

- Task 1's outputs are the gitignored `bin/Release/` build layout + the copied `utinni-cli.exe` net472 closure. The deliverable is the on-disk content-verified layout, so there is no source commit for Task 1 — consistent with the plan's `files_modified: [D:/Code/Utinni/bin/Release/]` and the 15-11 precedent. The only committable artifacts are the SUMMARY, the `15-SMOKE.md` annotation, and the `STATE.md`/`ROADMAP.md`/`REQUIREMENTS.md` state updates.
- Native `UtinniCore.dll` was not recompiled (no native source change since `b26e4bd`; MSBuild correctly skipped it — mtime 2026-06-13 09:48, the b26e4bd build). The wave-5 managed fixes live in `UtinniCoreDotNet.dll` + `TheJawaToolboxDotNet.dll`, both rebuilt (12:38) — confirmed by the content-of-fix gate.

## Known Stubs

None. This plan is a build/handoff seam; it adds no new feature code or data wiring.

## Authentication Gates

None.

## Verification Summary

| Check | Result |
|-------|--------|
| `Utinni.sln` Release\|x86 (MSBuild v145) | Build succeeded / 0 errors |
| `TheJawaToolbox.sln` Release\|x86 | Build succeeded / 0 errors |
| `UtinniCoreDotNet.Tests` (Release) | 706 passed / 0 failed |
| `Utinni.Cli.Tests` (Release) | 249 passed / 2 skipped / 0 failed |
| `Utinni.Mcp.Tests` (net10, Release) | 77 passed / 0 failed |
| Native `UtinniCore.Tests.exe` full | 84 assertions / 27 cases |
| Native `UtinniCore.Tests.exe [resid04]` | 8 assertions / 1 case |
| `Generated/UtinniCore.cs` churn | reverted (clean) |
| bin/Release base layout | all required files present |
| bin/Release `netstandard.dll` (B5) | PRESENT |
| bin/Release `utinni-cli.exe` + net472 closure (B7) | PRESENT |
| Content-of-fix: core `InjectedAssemblyResolver` type | PRESENT |
| Content-of-fix: core `WorldSnapshotCommandGuard` type | PRESENT |
| Content-of-fix: core `UndoRedoManager.Clear` method | PRESENT |
| Content-of-fix: core `A9-diag` string | ABSENT (correct) |
| Content-of-fix: deployed `TheJawaToolboxDotNet.dll` | PRESENT |
| 15-SMOKE.md wave-5 note (15-12..15-17) | present (all 6 plan refs) |
| 15-SMOKE.md original crash evidence (`0xC0000005`) | intact (5 original lines) |
| 15-SMOKE.md B5 "Could not load file or assembly" | intact |
| Maintainer Sign-Off block | unchanged (unsigned) |

## Handoff

The maintainer can now resume the **15-18** live-SWG re-smoke against the fixed + COMPLETE, content-verified `D:/Code/Utinni/bin/Release/` build: inject it (a relaunch is required to clear any cached failed netstandard bind) and re-run the still-open smoke — **B5–B8** (loose-override Save now works; Explain-effect shells `decode-iff` via the shipped `utinni-cli.exe`; preview disabled-reason tooltip), **Checklist C full matrix INCLUDING the C3 windowed→fullscreen re-verify** (the 15-13 watchdog should re-assert the embed; confirm no detach / no input lockup / no device Reset) plus C4–C15, and **Checklist D** `.stf`/`.ot` loose-override saves (unblocked by the B5 façade fix) — recording results back in `15-SMOKE.md`. The phase gate is the still-unsigned Maintainer Sign-Off block.

## Self-Check: PASSED

- FOUND: `.planning/phases/15-wave-2-.../15-17-SUMMARY.md`
- FOUND: deployed `bin/Release/UtinniCoreDotNet.dll` (content-verified: InjectedAssemblyResolver/WorldSnapshotCommandGuard/Clear present, no A9-diag)
- FOUND: deployed `bin/Release/netstandard.dll` + `bin/Release/utinni-cli.exe` + net472 closure
- FOUND: deployed `bin/Release/Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll`
- FOUND: `15-SMOKE.md` wave-5 note (15-12..15-17 referenced; original evidence intact; sign-off unsigned)
- Task 1 has no source commit by design (gitignored build output); commit hash recorded below is the Task 2 + state-update metadata commit
