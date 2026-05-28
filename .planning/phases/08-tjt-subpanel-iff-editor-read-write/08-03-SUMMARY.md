---
phase: 08-tjt-subpanel-iff-editor-read-write
plan: 03
subsystem: tjt-shared-iff-chunk-tree-control
tags: [tjt, ui, winforms, usercontrol, iff, chunk-tree, shared-control, d-09, phase-7-preservation]
requires:
  - "TheJawaToolboxDotNet (existing TJT plugin assembly in UtinniPlugins)"
  - "UtinniCoreDotNet/Formats/Iff/IffDocument + IffChunk + IffContainerChunk (Phase 7 read model)"
  - "UtinniCoreDotNet/Formats/Iff/MutableIffDocument + MutableIffNode (08-01 mutable model)"
  - "UtinniCoreDotNet/UI/Controls/UtinniContextMenuStrip (themed menu base)"
  - "UtinniCoreDotNet/UI/Theme/Colors (Colors.*() accessors)"
provides:
  - "IffChunkTree UserControl (shared, themed) — read-only LoadDocument(IffDocument) AND editable LoadMutable(MutableIffDocument) — the D-09 reusable chunk-tree surface 08-04 (and Phases 9-11) will consume"
  - "TreDetailPane (Phase 7) refactored to delegate its chunk tree to the shared IffChunkTree — public read API signature-pinned and unchanged"
affects:
  - "TheJawaToolboxDotNet.csproj — added one <Compile Include> entry for IffChunkTree.cs (round-2 HIGH-A csproj coverage)"
  - "(no Utinni-repo files modified by this plan — wholly in the UtinniPlugins sibling)"
tech-stack:
  added: []
  patterns:
    - "Extract-and-delegate refactor: move inline TreeView + BuildChunkNode into a shared UserControl; host (TreDetailPane) only forwards LoadIff → LoadDocument; zero public-API breakage"
    - "Two-binding-mode UserControl: LoadDocument(IffDocument) for read-only; LoadMutable(MutableIffDocument) for editable; each TreeNode.Tag references its bound model node so the host maps selection back to either model"
    - "Signature-pin comment block (08-REVIEWS LOW-2): inline contract documentation of the eight pinned public read-API methods (ShowEmpty/ShowDecoding/ShowReadable/ShowParseFailure/ShowEncrypted/ShowStringTable/ShowUnsupportedRaw/LoadIff) to make a future signature drift visible at the source"
    - "Verbatim Phase 7 label-format preservation: 'TAG [SubType]  ·  N bytes  ·  @offset' — kept byte-identical in IffChunkTree.BuildChunkNode (read-only path) so the TRE Browser surface is unchanged"
    - "Old-style explicit-compile csproj coverage: every new .cs file under TheJawaToolboxDotNet gets an explicit <Compile Include> with <SubType>UserControl</SubType> (round-2 HIGH-A pattern from 08-01 — same rule applied here in the sibling repo)"
key-files:
  created:
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/IffChunkTree.cs"
  modified:
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj"
decisions:
  - "IffChunkTree exposes a TreeViewEventHandler AfterSelect event + a SelectedNode pass-through property (rather than a custom args type) so the editor host (08-04) consumes selection with zero new types — keeps the shared-control surface minimal and uses the WinForms idiom every host already knows."
  - "StructuralOpMenu is a settable ContextMenuStrip property (get/set forwards to the inner TreeView's ContextMenuStrip) rather than a builder method or event-based attach. Plan allows planner's discretion; this is the simplest no-allocation idiom and lets the editor host wire the themed UtinniContextMenuStrip + items + click handlers in 08-04 without IffChunkTree owning any item logic."
  - "Mutable-mode node label is 'TAG [SubType]  ·  N bytes' (NO @offset) — the mutable model has no fixed file offset once edited (a moved/duplicated node has no canonical position). Read-only label keeps Phase 7's '… ·  @offset' verbatim for parity. The two labels diverge intentionally."
  - "BuildChunkNode was removed from TreDetailPane (moved into IffChunkTree). A 4-line comment was left at the prior site documenting where it went, so future Phase-7 spelunking immediately finds the new home (avoids a 'where did it go' grep)."
  - "Signature-pin block (08-REVIEWS LOW-2) is an inline XML/comment block at the END of TreDetailPane.cs (not as a separate doc) so any future contributor who changes a pinned signature sees the contract in the same file. A doc-only pin would be invisible at the edit site."
  - "MSBuild Debug+Release|x86 build needed UtinniCoreDotNet to be rebuilt first (the bin/Debug/UtinniCoreDotNet.dll was from 2026-05-19 — pre-Formats/Iff). Rebuilt UtinniCoreDotNet inline; documented as a Rule 3 ordering issue (not a deviation from the plan)."
metrics:
  duration_minutes: 8
  completed_date: "2026-05-28"
---

# Phase 8 Plan 3: Shared IffChunkTree UserControl Extraction Summary

One-liner: Extracted Phase 7's chunk-tree TreeView + `BuildChunkNode` from `TreDetailPane` into a shared themed `IffChunkTree` UserControl (D-09) that binds to either an `IffDocument` (read-only) or a `MutableIffDocument` (editable) — `TreDetailPane` delegates `LoadIff` to it with zero public read-API change.

## What Shipped

**1 production file (new)** in `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/`:

- **`IffChunkTree.cs`** — themed `UserControl` wrapping a `TreeView` (`BackColor = Colors.PrimaryHighlight()`, `ForeColor = Colors.Font()`, `BorderStyle = None`, `HideSelection = false`, `ShowLines = true` — extracted verbatim from `TreDetailPane.tvChunks` lines 670-675). Two binding modes:
  - **`public void LoadDocument(IffDocument doc)`** — read-only path. Each `TreeNode.Tag` references its bound `IffChunk`. Node labels use the EXACT Phase 7 format `TAG [SubType]  ·  N bytes  ·  @offset` (preserved verbatim from the prior `TreDetailPane.BuildChunkNode` — byte-identical for parity).
  - **`public void LoadMutable(MutableIffDocument doc)`** — editable path. Each `TreeNode.Tag` references its bound `MutableIffNode` so the editor host (08-04) maps selection back to the model for structural ops and leaf-payload edits. Labels are `TAG [SubType]  ·  N bytes` (no `@offset` — the mutable model has no fixed file offset once edited).
  - **`public void RefreshMutable(MutableIffDocument doc)`** — convenience alias for `LoadMutable` for use after edit ops (08-04 dirties the tree on every structural op).
  - **`public event TreeViewEventHandler AfterSelect`** — selection event forwarded from the inner `TreeView` so editor hosts can react to leaf/container selection.
  - **`public ContextMenuStrip StructuralOpMenu { get; set; }`** — attachment point for the structural-op context menu. Get/set forwards to the inner `TreeView`'s `ContextMenuStrip`. The menu items + click handlers are owned by the editor host (08-04). Read-only consumers leave it null.
  - **`public TreeNode SelectedNode { get; set; }`** — pass-through to the underlying `TreeView` (lets a host programmatically restore selection after a refresh).

**2 production files (modified)** in the same sibling repo:

- **`UI/Controls/TreDetailPane.cs`** — refactored to host an `IffChunkTree` instance (`private readonly IffChunkTree iffChunkTree`) wired into `splitOuter.Panel1`. `LoadIff(IffDocument doc)` now delegates to `iffChunkTree.LoadDocument(doc)` (one line). The inline `tvChunks` `TreeView` field and the private `BuildChunkNode` method were removed (they live in `IffChunkTree` now). The public read API (`ShowEmpty`, `ShowDecoding`, `ShowReadable`, `ShowParseFailure`, `ShowEncrypted`, `ShowStringTable`, `ShowUnsupportedRaw`, `LoadIff`) is **byte-identical at the signature level**; an inline signature-pin comment block at the end of the file documents the pinned contract (08-REVIEWS LOW-2). `txtHex` (read-only hex+ASCII dump), the four-state degradation logic, and the `splitOuter`/`splitInner` `SplitContainer` discipline (Size-before-SplitterDistance; Dock.Fill added first) are unchanged.
- **`TheJawaToolboxDotNet.csproj`** — explicit `<Compile Include="UI\Controls\IffChunkTree.cs"><SubType>UserControl</SubType></Compile>` entry added immediately after the existing `TreDetailPane.cs` entry (inside the same `<ItemGroup>` block — round-2 HIGH-A csproj coverage gate). Without this entry the old-style explicit-compile csproj would silently omit the new file.

**No new tests added.** The acceptance gate for read-only-preservation is the signature pin + the FormTreBrowser unedited-and-still-compiles invariant + the VS2026 MSBuild Debug|x86 + Release|x86 clean build of TJT (which the integration test). Unit tests of the IffChunkTree binding paths would require a hosted WinForms test rig the TJT project does not have; the same pragma applies as Phase 7 (TreDetailPane has no unit tests). 08-04 will add UI tests against the editable path when the editor host lands.

## Deviations from Plan

### Auto-fixed / inline deviations

**1. [Rule 3 - Blocking ordering issue] Rebuilt `UtinniCoreDotNet` Debug|x86 + Release|x86 inline before TJT to refresh the stale HintPath DLL**

- **Found during:** Task 1 MSBuild verification (first build attempt failed with 26 `CS0234`/`CS0246` errors against the entire `UtinniCoreDotNet.Formats.*` namespace).
- **Issue:** `D:/Code/Utinni/bin/Debug/UtinniCoreDotNet.dll` and `bin/Release/UtinniCoreDotNet.dll` were both built on 2026-05-19 (before Phase 7 finished and before 08-01's `Formats/Iff` additions). TJT's csproj references these DLLs via `<HintPath>..\..\..\Utinni\bin\$(Configuration)\UtinniCoreDotNet.dll</HintPath>`, so the build saw the stale assembly and couldn't resolve `IffDocument`, `MutableIffDocument`, `TreArchiveIndex`, etc.
- **Fix:** Ran `MSBuild Utinni.sln -target:UtinniCoreDotNet -p:Configuration=Debug -p:Platform=x86` followed by the Release|x86 variant before re-running the TJT build. Both rebuilds completed cleanly (warnings only — pre-existing CS0108 hiding warnings in `Generated/UtinniCore.cs`, unrelated to this plan). After the refresh, both TJT configs compiled clean.
- **Files modified:** None in source; rebuilt only the binary outputs at `D:/Code/Utinni/bin/{Debug,Release}/UtinniCoreDotNet.dll`. Not committed (build outputs are gitignored).
- **Commit:** N/A — pre-task ordering fix, not a code change.

### No-cost / no-impact

None.

## Threat Surface Verification

All threat-model dispositions from the plan's `<threat_model>` are met:

| Threat ID | Disposition | Status |
|-----------|-------------|--------|
| T-08-06 (DoS — malformed tree rendering) | mitigate via four-state degradation + upstream reader caps | Met — `IffChunkTree.LoadDocument`/`LoadMutable` cannot throw on an empty tree (Nodes stays empty); failure-to-parse is the host's responsibility and `TreDetailPane`'s existing `ShowParseFailure` four-state pattern is unchanged |
| T-08-07 (Tampering — silent public-API break) | mitigate via signature pin + rebuild-in-same-commit | Met — public read API unchanged at signature level (8/8 methods present 2× each: declaration + pin); FormTreBrowser unedited and still compiles; TJT Debug+Release|x86 both compile clean in the same commit chain |
| T-08-07b (Tampering — silent compile omission) | mitigate via explicit `<Compile Include>` entry | Met — `TheJawaToolboxDotNet.csproj` contains the new entry with `<SubType>UserControl</SubType>` adjacent to the TreDetailPane block; grep-gated |
| T-08-SC (Supply chain — package installs) | accept (none added) | N/A — no external packages added |

## Cross-AI Review Concerns Addressed

| Round-2 / Round-3 ID | Severity | Disposition |
|----------------------|----------|-------------|
| Round-2 HIGH-A (csproj coverage) | HIGH | RESOLVED — explicit `<Compile Include="UI\Controls\IffChunkTree.cs"><SubType>UserControl</SubType></Compile>` entry landed adjacent to the TreDetailPane entry in the same `<ItemGroup>`; grep-gated acceptance passed |
| Round-2 MEDIUM 3 (cross-repo) | MEDIUM | RESOLVED — plan executed wholly in the sibling UtinniPlugins repo with no Utinni-repo source files touched; TJT consumes UtinniCoreDotNet.dll via HintPath as designed; the working-tree `M UtinniCoreDotNet/Generated/UtinniCore.cs` from before the plan started was NOT referenced by either commit (left as-is — same state the planner saw) |
| Round-2 HIGH-A check (same-wave file collisions) | HIGH | RESOLVED — 08-02 stayed in the Utinni repo (`Utinni.Cli.csproj` + `Utinni.Cli.Tests.csproj`); 08-03 stayed in the UtinniPlugins repo (`TheJawaToolboxDotNet.csproj`); zero overlap as the planner predicted |
| 08-REVIEWS LOW-2 (signature pin) | LOW | RESOLVED — inline signature-pin comment block added at the end of TreDetailPane.cs documenting the eight pinned public read-API methods; FormTreBrowser unedited and every `_detail.<name>(` call site still resolves (verified by grep) |

## Build Verification

**VS2026 MSBuild — `D:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe`:**

- `UtinniCoreDotNet` Debug|x86 → `bin/Debug/UtinniCoreDotNet.dll` (clean; warnings only)
- `UtinniCoreDotNet` Release|x86 → `bin/Release/UtinniCoreDotNet.dll` (clean; warnings only)
- `TheJawaToolboxDotNet` Debug|x86 → `bin/Debug/Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll` (clean; zero errors, zero warnings)
- `TheJawaToolboxDotNet` Release|x86 → `bin/Release/Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll` (clean; zero errors, zero warnings)

No xUnit test changes were in scope. The full Utinni xUnit suite was NOT re-run for this plan (no `UtinniCoreDotNet` source touched — the TJT-only changes don't affect the test surfaces). The pre-existing `NativeCallbacksHandleTests.Subscribe_DuringDispatch_NotInvokedInCurrentIteration_InvokedInNext` flake noted in `deferred-items.md` is unaffected.

## Acceptance Gate Verification (literal greps from the plan)

**Task 1 (IffChunkTree.cs + csproj):**

- `grep -c 'class IffChunkTree' IffChunkTree.cs` → **1** PASS
- `grep -c 'public void LoadDocument(IffDocument' IffChunkTree.cs` → **1** PASS
- `grep -c 'public void LoadMutable(MutableIffDocument' IffChunkTree.cs` → **1** PASS
- `grep -c 'Color.FromArgb' IffChunkTree.cs` → **0** PASS
- `grep -cF '<Compile Include="UI\Controls\IffChunkTree.cs"' TheJawaToolboxDotNet.csproj` → **1** PASS
- nested `<SubType>UserControl</SubType>` adjacent to the IffChunkTree entry → **1** PASS
- Node label format `TAG [SubType]  ·  N bytes  ·  @offset` in `BuildChunkNode` (read-only path) — preserved verbatim from Phase 7 — PASS by source inspection.
- New entry lives inside the existing main `<ItemGroup>` (lines 64-132 of the csproj) adjacent to TreDetailPane.cs — PASS by source inspection (immediately after).

**Task 2 (TreDetailPane.cs):**

- `grep -c 'IffChunkTree' TreDetailPane.cs` → **8** PASS (field declaration + ctor wire + Dock.Fill + Panel1.Controls.Add + LoadIff delegate + new comment refs + 2 in the moved-method note + pin block)
- Each pinned public method present twice (declaration + signature-pin block) → **2** each PASS (verified: ShowEmpty, ShowDecoding, ShowReadable, ShowStringTable, LoadIff, ShowEncrypted, ShowUnsupportedRaw, ShowParseFailure)
- `grep -c 'Color.FromArgb' TreDetailPane.cs` → **0** PASS
- FormTreBrowser.cs unedited by this plan (verified — no changes staged); every `_detail.<name>(` call site resolves (verified: ShowEmpty=1, ShowDecoding=1, ShowReadable=1, ShowStringTable=1, ShowEncrypted=1, ShowUnsupportedRaw=1, ShowParseFailure=3, LoadIff=0 because LoadIff is called by TreDetailPane.ShowReadable internally, not by FormTreBrowser) — PASS
- VS2026 MSBuild Debug|x86 + Release|x86 of TJT — both clean — PASS

## Output Confirmation

The explicit `<Compile Include="UI\Controls\IffChunkTree.cs"><SubType>UserControl</SubType></Compile>` entry in `TheJawaToolboxDotNet.csproj` closes **round-2 HIGH-A** for this plan.

## Self-Check: PASSED

**Files verified present:**

- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/IffChunkTree.cs` — FOUND
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs` — FOUND (modified)
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj` — FOUND (modified)

**Commits verified present in UtinniPlugins (`cd D:/Code/UtinniPlugins && git log --oneline`):**

- `cbbcf58` — `feat(08-03): shared IffChunkTree UserControl (D-09) + csproj entry` — FOUND
- `db469a1` — `refactor(08-03): TreDetailPane delegates chunk tree to shared IffChunkTree` — FOUND

**Build artifacts verified present:**

- `D:/Code/Utinni/bin/Debug/Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll` — FRESH
- `D:/Code/Utinni/bin/Release/Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll` — FRESH
