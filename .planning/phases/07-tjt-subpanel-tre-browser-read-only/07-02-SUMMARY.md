---
phase: 07-tjt-subpanel-tre-browser-read-only
plan: 02
subsystem: ui
tags: [tjt, winforms, tre-browser, treeview, splitcontainer, plugin, cross-repo, swgemu-5000]

requires:
  - phase: 07-tjt-subpanel-tre-browser-read-only
    provides: "07-01 shared TreArchiveIndex.Build/AllPaths/TryGetDescriptor + TrePayloadResolver facade; version-dispatching reader"
provides:
  - "FormTreBrowser: TRE Browser window in The Jawa Toolbox (resizable UtinniForm via GetForms())"
  - "Lazy SWG virtual-path TreeView (top-level eager, children on BeforeExpand) built off-thread from the shared TreArchiveIndex"
  - "Debounced (250ms) case-insensitive flat-index filter with whole-node bold + 5000-match cap → flat ListView fallback"
  - "Concrete client-dir resolution from the injected process module dir (+ working dir + [TreBrowser] clientDir fallback)"
  - "Game.Repository install-time-snapshot loaded/dimmed overlay (FilenameCount+GetFilenameAt)"
affects: [07-03, 07-04b]

tech-stack:
  added: []
  patterns:
    - "TJT forms must drive their load from the CONSTRUCTOR (async Task), not the Shown event — Shown does not fire reliably for forms shown inside SWG's injected message loop"
    - "Heavy work in await Task.Run(...); apply to WinForms controls on the await continuation (captured UI SynchronizationContext) — no Control.Invoke needed"
    - "Resolve the SWG client dir from Process.MainModule's directory (install root), NOT utility.GetWorkingDirectory() (which is GetCurrentDirectory — the CWD, not the install root)"
    - "SplitContainer.Size must be set before SplitterDistance in hand-written Designer code, or the ctor throws and fails the plugin's MEF load"
    - "Isolate each plugin form's construction in try/catch + Log so one form's failure can't remove the whole toolbox from the menu"

key-files:
  created:
    - "UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs"
    - "UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.Designer.cs"
  modified:
    - "UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs"
    - "UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj"
    - "Utinni/UtinniCoreDotNet/Formats/Tre/TreVersion.cs (5000 reclassified readable — see Deviations)"
    - "Utinni/UtinniCoreDotNet/Formats/Tre/TreFile.cs (5000 crc-first-24 parse)"
    - "Utinni/Utinni.Cli.Tests/* (5000 fixture + tests updated)"

key-decisions:
  - "5000 is the READABLE SWGEmu Pre-CU client format (crc-first 24-byte stride, zlib blocks) — NOT enumerate-only. Reverses planning assumption D-06b; discovered because the live client is 100% EERT5000 (see Deviations + [[project-tre-version-support-gap]])."
  - "Client .tre dir resolves from Process.MainModule directory (the injected SWG install root); utility.GetWorkingDirectory() is GetCurrentDirectory and is unreliable."
  - "Forms load from the ctor (FormObjectBrowser pattern), not Shown — Shown does not fire in the injected message loop."
  - "cbTypeFacet kept as a single 'All types' stub (V1-optional per plan); filtering is the text box + tree."

patterns-established:
  - "utinni.log (bin/Release/utinni.log via native spdlog) is the reliable cross-repo diagnostic channel for injected-runtime debugging — readable directly from the build tree."

requirements-completed: [PROD-W1-TRE, PROD-01]

duration: ~2h (incl. live-smoke debug + 5000 discovery)
completed: 2026-05-27
---

# Phase 7 Plan 02: TRE Browser Shell Summary

**A TRE Browser window in The Jawa Toolbox — resizable, lazy SWG virtual-path tree built off-thread from the shared TreArchiveIndex, debounced capped filter, and a Game.Repository loaded/dimmed overlay — verified live enumerating 125,572 paths from the SWGEmu Pre-CU client.**

## Live-smoke result (PROD-W1-TRE criteria 1+2)
- TRE Browser opens as a resizable `UtinniForm` from the TJT menu; tree **auto-populates with 125,572 paths** (no manual path entry) from `D:\SWGEmu-Client\SWGEmu`.
- Tree, lazy expand, debounced filter (narrow bold + broad → flat ListView), and overlay confirmed by the user ("approved").
- Both repos build Release/x86 (cross-repo build gate, review item 10).

## Task Commits (cross-repo)
**UtinniPlugins:**
1. **Shell** (Task 1+2) — `740b677` (feat)
2. SplitContainer ctor-crash + plugin-load isolation — `339158e` (fix)
3. Client-dir from process module dir + logging — `5b0b72e` (fix)
4. Load state in window title — `d81b400` (fix)
5. Ctor-driven load (Shown doesn't fire) — `b789bbb` (fix)

**Utinni:** 5000 reclassified readable — `d75c701` (fix; lands in 07-01's files)

## Deviations from Plan

The auto-tasks (1–2) matched the plan; the human-verify checkpoint (Task 3) surfaced **five integration defects**, fixed in order. All are Rule-1/2 (the live integration exposed them; none are scope creep):

**1. [Rule 1] SplitContainer ctor crash failed the whole plugin's MEF load.** Setting `SplitterDistance=360` in the Designer while the container was still 150px wide threw `InvalidOperationException` from the ctor → TJT vanished from the menu. Fix: set `Size` before `SplitterDistance`. Also wrapped `new FormTreBrowser(this)` in Plugin.cs in try/catch + Log so a form ctor can never again remove the whole toolbox.

**2. [Rule 1] Client dir resolution was unreliable.** `utility.GetWorkingDirectory()` is `GetCurrentDirectory()` (the CWD), not the install root → resolved to a dir without `.tre` → empty tree. Fix: resolve from `Process.GetCurrentProcess().MainModule` directory first (the SWG install root), then working dir, then the `[TreBrowser] clientDir` ini fallback. Review item 7 explicitly allowed the process directory.

**3. [Rule 1] Status labels invisible / Shown never fired.** The docked status labels collapsed (no visible feedback), then `utinni.log` proved the load **never ran** — the `Shown` event the load was wired to does not fire for forms shown in SWG's injected message loop. Fix: drive the load from the ctor (FormObjectBrowser's proven pattern) via `await Task.Run(...)` + UI-thread continuation; route status into the window title (with `Invalidate()`).

**4. [Rule 1 - MAJOR, reverses D-06b] 5000 is the readable SWGEmu Pre-CU format, not enumerate-only.** The live client is 100% `EERT5000` (53 archives) — ALL returning 0 records because 07-01 treated 5000 as enumerate-empty (the plan's "5000 = unknown/encrypted Restoration layout" assumption). Reverse-engineered from `default_patch.tre`/`bottom.tre`: 5000 = size-first HEADER + **crc-first 24-byte record stride** (6000's field order minus the 8-byte pad) + zlib blocks. Verified via `parse-tre` (default_patch → 3 records, bottom → 808). Fixed the reader (`IsCrcFirst(V5000)=true`, `IsEnumerateOnly(V5000)=false`, pad only for 32-byte V6000), rebuilt the 07-00 synthetic-5000 fixture + 07-00/07-01 tests from enumerate-empty to enumerates-records. Full suites green (82+1 / 155). Memory [[project-tre-version-support-gap]] updated.

**Total deviations:** 5 (4 cross-repo UI integration + 1 reader-format reclassification). **Impact:** all load-bearing for a working browser; the 5000 finding is the difference between 0 and 125,572 browsable paths. The two `dotnet build` vs MSBuild + utinni.log diagnostic patterns are captured in memory.

## Issues Encountered
- The status/legend labels at the bottom-left of Panel1 are cramped/hard to see (docked UtinniLabel). Cosmetic only (state is mirrored in the title + log). The detail pane (07-03) reworks this panel and will tidy the status row.

## User Setup Required
None. (Optional: set `[TreBrowser] clientDir` in TJT settings.ini if auto-detection ever fails; the primary source is the injected client's install root.)

## Next Phase Readiness
- 07-03 plugs the detail pane into `pnlDetail` (Panel2) and reads selected entries via `TrePayloadResolver.TryResolve` over the same descriptor the tree carries.
- The 5000 reader correction means the browser/CLI now read the real SWGEmu client; 07-03/07-04 inherit it.
- No blockers.

---
*Phase: 07-tjt-subpanel-tre-browser-read-only*
*Completed: 2026-05-27*
