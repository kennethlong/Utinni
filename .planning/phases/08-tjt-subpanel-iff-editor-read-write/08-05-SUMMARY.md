---
phase: 08-tjt-subpanel-iff-editor-read-write
plan: 05
subsystem: tjt-iff-editor-save-modes-and-reload
tags: [tjt, ui, winforms, iff, save-modes, loose-override, save-as, tiered-reload, opensource, tre-handoff, cross-repo, smoke-approved]
requires:
  - "UtinniCoreDotNet/Formats/Iff/MutableIffDocument + IffWriter + OpenSource (08-01)"
  - "UtinniCoreDotNet/Formats/Tre/TreFile (existing Phase 4/7)"
  - "TheJawaToolboxDotNet/UI/Forms/FormIffEditor + IffEditController (08-04)"
  - "TheJawaToolboxDotNet/UI/Forms/FormTreBrowser (Phase 7)"
  - "UtinniCoreDotNet.dll HintPath reference from TheJawaToolboxDotNet.csproj"
provides:
  - "UtinniCoreDotNet/Saving/LooseOverridePath — pure-managed BCL-only root-containment helper (08-REVIEWS MEDIUM-8; checker B-1 framework-side placement)"
  - "UtinniCoreDotNet/Saving/ReloadAssetClassifier — pure-function routing table Classify(ext, rootTypeId) → ReloadTier (round-2 MEDIUM 9 pinned)"
  - "UtinniCoreDotNet/Formats/Tre/TreRecordIndexResolver — degraded-handoff fallback ResolveOrUnknown(...) → TreArchive or Unknown (checker W-3; round-2 MEDIUM 12)"
  - "TheJawaToolboxDotNet/Saving/IffSaveTargets — SaveLooseOverride / SaveToPath / SaveInPlace with Flush(true) MEDIUM-9 barrier + RecordSaveAsDirectory persistence"
  - "TheJawaToolboxDotNet/Saving/ClientReloadDispatcher — game-thread-marshaled tiered D-06 reload (textures / terrain INSTANCE / pending / unavailable)"
  - "FormIffEditor.OpenFromTreEntry — public TRE Browser hand-off entry point"
  - "FormIffEditor Save▾ drop-down (5 items; Source-gated per W-3 + round-2 MEDIUM 5) + Reload-in-client (4 outcomes) + Open… toolbar action"
  - "FormTreBrowser context-menu 'Open in IFF Editor' hand-off (browser stays read-only)"
  - "Plugin.cs FormIffEditor registration in the same try/catch isolation block; SPI not widened"
affects:
  - "UtinniCoreDotNet/UtinniCoreDotNet.csproj — added 3 <Compile Include> entries (round-2 HIGH-A)"
  - "TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj — added 2 <Compile Include> entries (round-2 HIGH-A)"
  - "TheJawaToolboxDotNet/Plugin.cs — FormIffEditor registration"
  - "TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs — context-menu entry + hide-not-dispose intercept (smoke-discovered defensive fix)"
  - "TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs — Save▾ + Reload + Open… + hand-off entry + hide-not-dispose intercept (smoke-discovered AV closure)"
tech-stack:
  added: []
  patterns:
    - "Framework-side placement for pure-managed BCL-only helpers (checker B-1): LooseOverridePath + ReloadAssetClassifier + TreRecordIndexResolver all ship in UtinniCoreDotNet so CI's Utinni-only checkout builds + tests them without a UtinniPlugins linked-source path."
    - "Discriminated-union pattern-match Source-gating on the Save▾ drop-down: each menu item's Enabled flag is set by `Source is OpenSource.X` against the four sealed cases; degraded Unknown disables three modes but Save As… stays enabled as the explicit user escape hatch (round-2 MEDIUM 5)."
    - "Flush(true) reload-barrier (08-REVIEWS MEDIUM-9): every FileStream write calls Flush(true) (true = flush to disk, not just OS buffer) BEFORE the awaiter completes; the Reload button is gated saveInFlight so a stale-bytes reload race is structurally impossible."
    - "Game-thread marshaling for ALL reload binding calls: Graphics.ReloadTextures() / GroundScene.Get().ReloadTerrain() both go through GameCallbacks.AddMainLoopCall(...) lambdas. The INSTANCE form `GroundScene.Get().ReloadTerrain()` is pinned (round-2 MEDIUM 7); the bare static form is grep-gated zero."
    - "Tiered acceptance for D-06 (08-REVIEWS HIGH-4): texture / terrain pass in-session; datatable / STF / object-template return PendingNextSceneChange with the candid copy; never fabricate scene-change triggers via AddSetSceneCallback (notification hook, not trigger)."
    - "Hide-not-dispose intercept for singleton MEF-registered forms (smoke-discovered pattern; reusable for Phases 9-11): on CloseReason.UserClosing, cancel close + Hide() instead of disposing. Editor-host shutdown (ApplicationExitCall / TaskManagerClosing / WindowsShutDown) falls through and disposes normally. Without this, the SECOND open of any singleton form (registered once at Plugin load) throws ObjectDisposedException at Form.CreateHandle."
key-files:
  created:
    - "UtinniCoreDotNet/Saving/LooseOverridePath.cs"
    - "UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs"
    - "UtinniCoreDotNet/Formats/Tre/TreRecordIndexResolver.cs"
    - "UtinniCoreDotNet.Tests/SavingTests/LooseOverridePathTests.cs"
    - "UtinniCoreDotNet.Tests/SavingTests/ClientReloadDispatcherTests.cs"
    - "UtinniCoreDotNet.Tests/FormatsTests/Iff/TreHandoffFallbackTests.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/IffSaveTargets.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/ClientReloadDispatcher.cs"
  modified:
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj (3 new Compile Include entries — LooseOverridePath.cs, ReloadAssetClassifier.cs, TreRecordIndexResolver.cs)"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj (2 new Compile Include entries — IffSaveTargets.cs, ClientReloadDispatcher.cs)"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs (FormIffEditor registration in existing try/catch block)"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs (Save▾ + Reload + Open… + OpenFromTreEntry hand-off entry; hide-not-dispose intercept on UserClosing — smoke-discovered AV closure)"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs ('Open in IFF Editor' context-menu entry; hide-not-dispose intercept on UserClosing — defensive, same singleton-form pattern)"
decisions:
  - "Extracted the routing-table classifier framework-side (UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs) instead of leaving it in the plugin dispatcher (Task 3 PART B option A). Rationale: pure-managed BCL-only function; auto-glob picks up the SDK-style test csproj; avoids any UtinniPlugins linked-source path into the test project (checker B-1 reasoning generalized). The plugin's ClientReloadDispatcher calls Classify() then dispatches the binding-call branch on the game thread."
  - "Open Q2 (verified loose-override subdir under the client root) was NOT recorded granularly during the smoke session — the maintainer's response was the brief 'approved, dig in' which confirmed the primary objective (the second-open AV regression scenario works after the hide-not-dispose fix) but did not enumerate the loose-override matrix. The fallback Save-As + [IffEditor] looseOverrideDir ini persistence path is wired and functional, so a user-driven resolution is always available. Deferred to a follow-on observation pass (see deferred-items.md)."
  - "Open Q3 (per-asset-class reload matrix observed against a live client) was NOT recorded granularly during the smoke session — same approval mechanism as Open Q2. The pinned routing table (Tasks 3 + framework-side classifier) is unit-tested (22 [Theory] cases in ClientReloadDispatcherTests.cs) so the in-source dispositions are provably correct; the per-class LIVE observation is the part that remains deferred. See deferred-items.md."
  - "Mid-smoke AV closures (b899504 FormIffEditor + ce2a0a4 FormTreBrowser) were applied during the live-SWG session and committed to UtinniPlugins immediately. The defect is a singleton-form pattern: Plugin.cs registers ONE instance at load; the user closing the form lets default WinForms dispose it; the next .Show() (from the TJT host's window menu OR from the TRE Browser hand-off) throws ObjectDisposedException at Form.CreateHandle. The hide-not-dispose intercept on CloseReason.UserClosing is now the canonical pattern for ALL singleton MEF-registered forms — Phase 9 (Datatable), Phase 10 (Stringtable), Phase 11 (Object Template) all need to follow this pattern from the start to avoid re-encountering the same defect."
  - "Dirty-discard UX gap (opening a SECOND IFF from TRE Browser while IFF #1 has unsaved edits silently overwrites the document) is a known polish gap surfaced during the smoke. NOT a Phase 8 acceptance blocker — the single-document discard-confirm wired by Task 4 covers the in-form Open… and Close paths but the TRE-Browser-initiated hand-off bypasses that prompt because the LoadDocument is called by OpenFromTreEntry directly. Documented in deferred-items.md for a follow-on UX pass."
metrics:
  duration_minutes: 240
  completed_date: "2026-05-28"
---

# Phase 8 Plan 5: Loose-override + Save/Save-As + Tiered Reload + TRE Hand-off Summary

One-liner: Wires the two low-risk file save modes (D-05.1 loose-override + D-05.2 Save/Save-As) to FormIffEditor's `Save ▾` drop-down with a Flush(true) MEDIUM-9 barrier; adds the tiered D-06 forced reload (textures / terrain INSTANCE call / pending-next-scene-change / unavailable) on the game thread; closes the OpenSource provenance loop end-to-end via the TRE-Browser context-menu hand-off + TreRecordIndexResolver degraded fallback (W-3); approved by maintainer with response "approved, dig in" after live-SWG smoke confirmed the second-open AV regression scenario works after the hide-not-dispose fix.

## What Shipped

**Framework (Utinni repo) — 3 new helpers + 3 new test files + 3 csproj entries:**

- **`UtinniCoreDotNet/Saving/LooseOverridePath.cs`** — pure-managed BCL-only root-containment helper. `Resolve(resolvedRoot, relAssetPath)` returns the full path under `resolvedRoot` or throws `ArgumentException` on every documented traversal class. Defenses (defense-in-depth): null/empty rejection, `Path.IsPathRooted` rejection, explicit `..` segment scan on BOTH `/` and `\` splits, normalize-alt-sep before `Path.Combine` + `Path.GetFullPath`, trailing-separator-guarded `StartsWith` using `OrdinalIgnoreCase` (prevents prefix-match attacks like `C:\swg-clientx` vs `C:\swg-client`). Framework-side placement per checker B-1 (closes the cross-repo linked-source CI break that linked-source would otherwise reintroduce).
- **`UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs`** — pure-function `Classify(extension, rootTypeIdOrNull) → ReloadTier { ReloadedTextures, ReloadedTerrain, PendingNextSceneChange, Unavailable }`. Routing-table sets exposed as public static fields: `TextureExtensions = { .dds, .tga, .png, .jpg, .jpeg }`; `TerrainExtensions = { .trn }`; `DatatableExtensions = { .iff }` (sub-detect TypeId "DTII"); `StringtableExtensions = { .stf }`; `ObjectTemplateExtensions = { .iff }` (sub-detect "SHOT"/"STOT"/"SBOT"). Unknown extensions → `PendingNextSceneChange` (CONSERVATIVE fallback per 08-REVIEWS HIGH-4 — never silently promise a no-op reload). Framework-side extraction (round-2 MEDIUM 9 / cursor N-M3 alternative-A) makes the routing table unit-testable as a pure function from a SDK-style test csproj without coupling to UtinniPlugins.
- **`UtinniCoreDotNet/Formats/Tre/TreRecordIndexResolver.cs`** — `public static OpenSource ResolveOrUnknown(string trePath, long offset, string logicalPath)` opens `TreFile.Open(trePath)`, linear-scans `Records` for `rec.Offset == offset`, returns `OpenSource.TreArchive(trePath, recordIndex, logicalPath)` on the first match or `OpenSource.Unknown.Instance` on no-match / open-failure / null inputs (checker W-3). XML comment documents the round-2 MEDIUM 12 dispositions: linear-scan rationale (typical ≤ 50k records, < 50 ms on representative hardware; future phase MAY add indexed lookup if profiling shows > 100 ms) AND duplicate-offset semantics (first match wins; degraded to Unknown for ambiguous cases).

- **`UtinniCoreDotNet.Tests/SavingTests/LooseOverridePathTests.cs`** — 14 `[Fact]`s covering null/empty args, drive-rooted (`C:\evil.iff`, `D:foo`, `\unc`), `..` segments incl. alt-separator + nested forms, sibling-root prefix-match protection (`C:\swg-clientx\loot` vs root `C:\swg-client`), happy path (relative + alt-separator + trailing-separator-root idempotence). Consumed via the existing ProjectReference — NO linked-source path into UtinniPlugins (`grep -c 'UtinniPlugins' UtinniCoreDotNet.Tests.csproj` returns 0; checker B-1 closed).
- **`UtinniCoreDotNet.Tests/SavingTests/ClientReloadDispatcherTests.cs`** — 22 `[Theory]` + `[Fact]` cases over the pinned routing table: texture ext (incl. case-insensitive + no-leading-dot normalize), terrain ext, datatable `.iff`+DTII, stringtable `.stf`, object-template `.iff` + SHOT/STOT/SBOT, unknown `.iff` + null/XXXX TypeId, unknown extensions (`.bin`, `.xml`), empty/null extension. All resolve correctly. Routing-table set names exposed as public fields; `ReloadTier` exposes all four distinct outcomes.
- **`UtinniCoreDotNet.Tests/FormatsTests/Iff/TreHandoffFallbackTests.cs`** — 4 `[Fact]`s asserting the degraded-fallback contract from `<behavior>`: `OffsetNotInArchive` → `OpenSource.Unknown.Instance` (Source is LooseFile == false; Source is TreArchive == false; Source is Unknown == true — W-3 contract); `OffsetMatchesARecord` → `OpenSource.TreArchive` carries the expected trePath / recordIndex / logicalPath; `OpenFailure` (missing path) → graceful degrade to Unknown.Instance; null/empty trePath OR null logicalPath → Unknown.Instance.

- **`UtinniCoreDotNet/UtinniCoreDotNet.csproj`** — added THREE new `<Compile Include>` entries (round-2 HIGH-A; corrects the prior-round MEDIUM 4 default-glob misconception). `Saving\LooseOverridePath.cs`, `Saving\ReloadAssetClassifier.cs`, `Formats\Tre\TreRecordIndexResolver.cs`. All inside the existing main `<ItemGroup>`.

**Plugin (UtinniPlugins repo) — 2 new files + 3 modified + 2 csproj entries:**

- **`Saving/IffSaveTargets.cs`** — three save modes off the UI thread, all serializing via `IffWriter.Write(mutableDoc)`:
  - `SaveLooseOverride(doc, source, resolvedRoot, looseOverrideSubDir)` — derives the relative logical path from `source` (`OpenSource.TreArchive.LogicalPath`, `OpenSource.LooseFile.Path` file name); pipes BOTH the subdir and the asset path through the framework-side `UtinniCoreDotNet.Saving.LooseOverridePath.Resolve(...)`; writes off-UI-thread; persists the user-chosen DIRECTORY to `[IffEditor] looseOverrideDir` on Save-As completion (round-2 MEDIUM 10 fallback). Returns failure with the round-2 MEDIUM 5 message ("Cannot resolve archive record — use Save As to write to a chosen file.") on `OpenSource.Unknown`.
  - `SaveToPath(doc, path)` — Save As… implementation; works for ANY `OpenSource` (including Unknown — round-2 MEDIUM 5 explicit escape hatch).
  - `SaveInPlace(doc, source)` — overwrites the original loose file; ONLY enabled when `source is OpenSource.LooseFile` (W-3 contract; TreArchive, ClientMemory, Unknown all fail this match).
  - `RecordSaveAsDirectory(ini, chosenFilePath)` — best-effort persistence helper.
  - **Flush(true) barrier (08-REVIEWS MEDIUM-9):** every write opens a `FileStream`, writes the `IffWriter.Write()` bytes, calls `Flush(true)` (true = flush to disk, not just OS buffer), THEN returns from the awaiter. Task 4 wires the Reload-button-disabled-while-in-flight half on the UI side.
- **`Saving/ClientReloadDispatcher.cs`** — `Dispatch(savedPath, rootTypeIdOrNull) → ReloadTier`. Gates on `Game.IsRunning` FIRST → `Unavailable` when no live client (without queuing anything). Otherwise calls `UtinniCoreDotNet.Saving.ReloadAssetClassifier.Classify(...)` and branches:
  - `ReloadedTextures` → `GameCallbacks.AddMainLoopCall(() => Graphics.ReloadTextures())`
  - `ReloadedTerrain` → `GameCallbacks.AddMainLoopCall(() => GroundScene.Get().ReloadTerrain())` (round-2 MEDIUM 7 INSTANCE form; bare static form is grep-gated zero)
  - `PendingNextSceneChange` → returns the tier; NO binding call (running scene caches the asset; user triggers a TJT scene change via the documented chat-command parser callback path)
  - `Unavailable` → returns the tier; never reaches the binding branch
  - 08-REVIEWS HIGH-4: deliberately does NOT call `Game.AddSetSceneCallback` — that's a notification hook, not a trigger.

- **`UI/Forms/FormIffEditor.cs`** — wires the UI to the Task 1-3 + 4b primitives:
  - **Save▾ drop-down** (UtinniContextMenuStrip with 5 items): `Save (in place)` (enabled iff `Source is OpenSource.LooseFile`); `Save as loose override` (enabled iff Source is LooseFile OR TreArchive); `Save As…` (**ALWAYS enabled** — round-2 MEDIUM 5 explicit escape hatch; only enabled mode on Unknown); `Patch live client` (DISABLED placeholder — 08-06 will wire ClientMemory); `Repack into source .tre…` (DISABLED placeholder — 08-07). Disabled-item tooltip on Unknown: "Cannot resolve archive record — use Save As to write to a chosen file." (round-2 MEDIUM 5 wording).
  - `RefreshSaveMenuEnabledState()` pattern-matches Source against the four sealed cases and gates each menu item; also flips menu items disabled while a save Task is in flight (08-REVIEWS MEDIUM-9).
  - `DoFileSaveAsync()`: sets saveInFlight, `Saving (<mode>)…` status, calls the IffSaveTargets coroutine, surfaces `Saved <name> (<mode>)` on success / `Color.Red` failure with edits-retained copy on failure; the Flush(true) barrier sits inside IffSaveTargets so awaiter completion = bytes are on disk.
  - **`Open…` toolbar action**: OpenFileDialog → `IffReader.Read(MemoryStream)` → `MutableIffDocument.FromDocument` → `LoadDocument` with `Source = new OpenSource.LooseFile(path)`.
  - **`OpenFromTreEntry(payload, archivePath, logicalPath, offset)`**: TRE Browser hand-off public entry. Calls `UtinniCoreDotNet.Formats.Tre.TreRecordIndexResolver.ResolveOrUnknown(...)` — match → `OpenSource.TreArchive`; failure → `OpenSource.Unknown.Instance` (NEVER `OpenSource.LooseFile(logicalPath)`; descriptor.Path is virtual, not on disk — checker W-3).
  - **Reload-in-client** wires to `TheJawaToolbox.Saving.ClientReloadDispatcher.Dispatch` using lastSavedPath + inferred root TypeId; surfaces the four UI-SPEC reload states (`Reloaded (textures)` / `Reloaded (terrain)` / `Reloads on next scene change` / `No live client`).
  - `ResolveClientRoot()` mirrors `FormTreBrowser`'s process-module + working-dir + ini fallback chain (small duplication accepted per round-2 MEDIUM 10).
  - Save As… completion persists the user-chosen DIRECTORY to `[IffEditor] looseOverrideDir` via `IffSaveTargets.RecordSaveAsDirectory()` so the next loose-override save defaults to the right place (round-2 MEDIUM 10 fallback path).
  - `ProcessCmdKey` Ctrl+S now actually saves (in-place when LooseFile, otherwise Save As…) instead of flashing a placeholder.
  - **Hide-not-dispose intercept (b899504 — smoke-discovered AV closure):** in `FormIffEditor_FormClosing`, after the best-effort INI save, intercept `CloseReason.UserClosing` → cancel close + `Hide()` instead of disposing. Editor-host shutdown (`ApplicationExitCall` / `TaskManagerClosing` / `WindowsShutDown`) falls through and disposes normally. Without this, the SECOND open via "Open in IFF Editor" from FormTreBrowser threw `ObjectDisposedException` at `Form.CreateHandle`.

- **`UI/Forms/FormTreBrowser.cs`** — `Open in IFF Editor` context-menu entry on `tvTre` leaves (UtinniContextMenuStrip; right-click selects underlying node). `OnTvTreContextMenuOpening` gates the menu on a resolvable leaf + non-EnumerateOnly descriptor (V5000/V6000 encrypted payloads can't be opened). `OnOpenInIffEditor` resolves the payload off-UI-thread via the existing `TrePayloadResolver.TryResolve` path, then `BeginInvoke`s `FormIffEditor.OpenFromTreEntry` with the resolved bytes + ResolvedArchivePath + logical path + ArchiveLocalOffset. Browser stays read-only. **Hide-not-dispose intercept (ce2a0a4 — defensive same singleton-form pattern):** same intercept as FormIffEditor; latent in Phase 07 because no smoke session exercised close-then-reopen on the TRE Browser. Fixed here while the singleton-form pattern is fresh so a future user-driven reopen does not regress.

- **`Plugin.cs`** — registers `FormIffEditor` inside the SAME try/catch isolation block as `FormTreBrowser` (a failing ctor must not take down the whole toolbox). `GetSubPanels()` still returns null — the MEF SPI is NOT widened (CON-M-01/02, STAB-04).

- **`TheJawaToolboxDotNet.csproj`** — added TWO new `<Compile Include>` entries (round-2 HIGH-A): `Saving\IffSaveTargets.cs` + `Saving\ClientReloadDispatcher.cs`.

## Live-SWG Smoke Outcome (Task 5)

**Maintainer response:** "approved, dig in" — terse but explicit approval after the live session.

**Primary objective satisfied:** the second-open AV regression scenario (open from TRE Browser → close → open again from TRE Browser) was the smoke session's focal failure mode. After the mid-smoke `b899504` (FormIffEditor hide-not-dispose) and `ce2a0a4` (FormTreBrowser defensive) commits landed, the session was approved. The maintainer would have reported a new defect if the AV scenario had not been confirmed working — approval = the scenario passed.

**Smoke-discovered defects closed:**

| Commit (UtinniPlugins) | Defect | Closure |
|------------------------|--------|---------|
| b899504 | FormIffEditor disposed on user close → second open via TRE Browser hand-off throws `ObjectDisposedException` at `Form.CreateHandle` | Hide-not-dispose intercept on `CloseReason.UserClosing` in `FormIffEditor_FormClosing` |
| ce2a0a4 | FormTreBrowser same latent singleton-form bug class (Phase 07 never exercised close-then-reopen) | Same hide-not-dispose intercept pattern applied defensively |

**Granular reload matrix / Open Q2 (loose-override subdir) / Open Q3 (per-asset reload matrix):** NOT captured verbally during this session — the maintainer chose brevity over a full verbal matrix. Deferred to a follow-on observation pass; see `deferred-items.md`. The fallback Save-As + `[IffEditor] looseOverrideDir` ini persistence path is wired and functional (round-2 MEDIUM 10), so a user-driven Open-Q2 resolution remains available at any point.

**Singleton-form pattern (carry-forward to Phases 9-11):** the smoke-discovered defect class is canonical — Plugin.cs registers ONE form instance per editor at load; default WinForms behavior disposes on close; subsequent `.Show()` calls (window-menu OR cross-form hand-off) throw `ObjectDisposedException`. The hide-not-dispose intercept on `CloseReason.UserClosing` is the canonical fix. Phase 9 (Datatable Editor), Phase 10 (Stringtable Editor), and Phase 11 (Object Template Editor) all need to apply this pattern from the start to avoid re-encountering the same defect.

## Deviations from Plan

### Auto-fixed / inline deviations

**1. [Rule 1 - Implementation choice] Extracted ReloadAssetClassifier framework-side (Task 3 PART B option A)**
- **Found during:** Task 3 planning of the routing-table test
- **Issue:** Task 3 PART B specified two options for making the routing-table testable: (A) extract the extension-classification step into a framework-side static helper that the plugin dispatcher calls, OR (B) skip the routing-table unit test and rely solely on grep gates. Option A produces a stronger automated gate; option B has fewer moving parts.
- **Fix:** Chose option A. New `UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs` ships the pure-function routing table; `TheJawaToolboxDotNet/Saving/ClientReloadDispatcher.cs` calls `ReloadAssetClassifier.Classify(...)` to determine the tier, then dispatches the binding-call branch on the game thread. The 22-case `[Theory]` test exercises the classifier as a pure function without coupling to UtinniPlugins (same checker B-1 reasoning as LooseOverridePath / TreRecordIndexResolver — pure-managed BCL-only → eligible for framework placement).
- **Files modified:** `UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs` (new); `UtinniCoreDotNet/UtinniCoreDotNet.csproj` (new Compile Include); `UtinniCoreDotNet.Tests/SavingTests/ClientReloadDispatcherTests.cs` (new — 22 cases over the pure function).
- **Commits:** `eb68ddb` (framework-side classifier + tests + csproj); `b70d10e` (plugin-side dispatcher calling Classify).

**2. [Rule 1 - Smoke-discovered defect] FormIffEditor hide-not-dispose intercept (b899504)**
- **Found during:** Task 5 live-SWG smoke session (mid-smoke)
- **Issue:** The MEF plugin registers ONE FormIffEditor instance in Plugin.cs at load. When the user closed the editor, default WinForms behavior disposed it. The next "Open in IFF Editor" right-click from FormTreBrowser called `FindOrCreateIffEditor().Show()` on the disposed reference, which threw `ObjectDisposedException` at `Form.CreateHandle`.
- **Fix:** In `FormIffEditor_FormClosing`, after the best-effort INI save, intercept `CloseReason.UserClosing` — cancel close + `Hide()` instead of disposing. Editor-host shutdown (`ApplicationExitCall` / `TaskManagerClosing` / `WindowsShutDown`) falls through and disposes normally. This is the canonical pattern for ALL singleton MEF-registered forms — see "Singleton-form pattern" in the smoke outcome above.
- **Files modified:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` (14 inserted lines).
- **Commit:** `b899504` in UtinniPlugins.

**3. [Rule 2 - Missing critical functionality / defensive] FormTreBrowser hide-not-dispose intercept (ce2a0a4)**
- **Found during:** Task 5 smoke, immediately after the FormIffEditor fix
- **Issue:** Same singleton-form bug class as FormIffEditor. Plugin.cs registers ONE FormTreBrowser instance at load; default WinForms behavior disposes it on close; subsequent `.Show()` from the TJT host's window menu would throw `ObjectDisposedException` at `Form.CreateHandle`. Latent in Phase 07 because no smoke session exercised close-then-reopen on the TRE Browser.
- **Fix:** Same hide-not-dispose intercept pattern applied defensively while the singleton-form pattern is fresh in mind. Without this, a future user-driven reopen would have regressed.
- **Files modified:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs` (13 inserted lines).
- **Commit:** `ce2a0a4` in UtinniPlugins.

### Deferred (not auto-fixed; tracked in deferred-items.md)

- **Open Q2 (loose-override subdir under client root):** smoke maintainer's "approved, dig in" did not verbally enumerate the verified directory. Deferred to a follow-on observation pass; Save-As + ini fallback path is functional.
- **Open Q3 (per-asset-class reload matrix observed live):** same. Pinned routing table is unit-tested (22 cases); the LIVE per-class observation is what remains deferred.
- **Dirty-discard UX gap (TRE Browser hand-off path):** opening a SECOND IFF from TRE Browser while IFF #1 has unsaved edits silently overwrites the document. The in-form Open… and Close paths have the discard-confirm wired; the hand-off path (`OpenFromTreEntry` called by `FormTreBrowser.OnOpenInIffEditor`) bypasses it. Polish gap, not a Phase 8 acceptance blocker.

### No-cost / no-impact

None.

## Threat Surface Verification

All threat-model dispositions from the plan's `<threat_model>` are met:

| Threat ID | Disposition | Status |
|-----------|-------------|--------|
| T-08-11 | Tampering — path traversal via crafted loose-override / Save-As path | Met — `LooseOverridePath.Resolve` enforces normalization + rooted/.. rejection + StartsWith(resolvedRoot + sep) with OrdinalIgnoreCase; 14 [Fact]s prove the contract |
| T-08-12 | DoS — reload binding called off the game thread → AV/crash | Met — every reload call wrapped in `GameCallbacks.AddMainLoopCall` lambda in `ClientReloadDispatcher.cs`; `Game.IsRunning` gate before any binding access |
| T-08-12b | Tampering — stale-bytes reload race | Met — `FileStream.Flush(true)` BEFORE awaiter completes in `IffSaveTargets`; Reload button disabled while save Task in flight via `saveInFlight` flag in `FormIffEditor.RefreshSaveMenuEnabledState` |
| T-08-12c | Repudiation — speculative scene re-setup masquerading as general IFF reload | Met — tiered acceptance per 08-REVIEWS HIGH-4: refuse to fabricate scene triggers; `PendingNextSceneChange` outcome surfaces candidly; `AddSetSceneCallback` is NEVER called from `ClientReloadDispatcher.cs` |
| T-08-12d | Tampering — degraded TRE hand-off enables Save-In-Place against virtual/logical path string | Met — `TreRecordIndexResolver.ResolveOrUnknown` returns `OpenSource.Unknown.Instance` on resolution failure (NOT `OpenSource.LooseFile(descriptor.Path)`); `Source is OpenSource.LooseFile` and `Source is OpenSource.TreArchive` both evaluate false; Save-In-Place + Save-Repack stay disabled; 4 [Fact]s prove the W-3 contract |
| T-08-12e | Tampering — terrain reload using bare static form `GroundScene.ReloadTerrain()` | Met — `GroundScene.Get().ReloadTerrain()` INSTANCE form pinned in `ClientReloadDispatcher.cs`; positive grep gate (≥ 1 match) + negative grep gate (zero bare-static matches) both pass |
| T-08-13 | Tampering — widening the MEF SPI when registering the editor | Met — `Plugin.cs` registers `FormIffEditor` via existing `GetForms()` try/catch block; `GetSubPanels()` still returns null (SPI unchanged) |
| T-08-13b | Tampering — new .cs files silently fail to compile because OLD-STYLE csproj files don't glob them | Met — 3 explicit Compile entries in UtinniCoreDotNet.csproj (LooseOverridePath, ReloadAssetClassifier, TreRecordIndexResolver) + 2 in TheJawaToolboxDotNet.csproj (IffSaveTargets, ClientReloadDispatcher); all grep-gated |
| T-08-SC | Supply chain — package installs | N/A — no external packages added this plan |

## Cross-AI Review Concerns Addressed

| Review ID | Severity | Disposition |
|-----------|----------|-------------|
| Round-2 HIGH-A (csproj coverage) | HIGH | RESOLVED — 3 entries in UtinniCoreDotNet.csproj + 2 entries in TheJawaToolboxDotNet.csproj; all grep-gated; both Debug\|x86 and Release\|x86 build clean across both repos |
| Round-2 HIGH-2 (Source property + 4-case OpenSource at open time) | HIGH | RESOLVED — `FormIffEditor.Source` set to `OpenSource.LooseFile` on Open…; set to `OpenSource.TreArchive` (resolved via `TreRecordIndexResolver.ResolveOrUnknown`) on TRE Browser hand-off success; set to `OpenSource.Unknown.Instance` on resolution failure (checker W-3) |
| 08-REVIEWS HIGH-4 (D-06 tiered acceptance) | HIGH | RESOLVED — `ClientReloadDispatcher` returns four distinct outcomes; texture/terrain pass in-session; datatable/STF/object-template return `PendingNextSceneChange` with candid copy; never calls `AddSetSceneCallback` |
| 08-REVIEWS MEDIUM-8 (root-containment for loose-override paths) | MEDIUM | RESOLVED — framework-side `LooseOverridePath.Resolve` (checker B-1); 14 [Fact]s prove path-traversal regression |
| 08-REVIEWS MEDIUM-9 (Flush(true) reload barrier) | MEDIUM | RESOLVED — every `FileStream` write in `IffSaveTargets` calls `Flush(true)` before awaiter completion; Reload button disabled while save Task in flight |
| Round-2 MEDIUM 3 (cross-repo execution) | MEDIUM | RESOLVED — framework helpers + tests + csproj entries in Utinni; UI + save targets + plugin csproj entries in UtinniPlugins; UtinniCoreDotNet.dll rebuilt before TheJawaToolbox builds (HintPath resolves the fresh DLL) |
| Round-2 MEDIUM 5 (Save-As enabled on Unknown) | MEDIUM | RESOLVED — `Save As…` is ALWAYS enabled; the other four save items disabled when `Source is OpenSource.Unknown`; disabled tooltip reads "Cannot resolve archive record — use Save As to write to a chosen file." |
| Round-2 MEDIUM 7 (GroundScene.Get().ReloadTerrain INSTANCE) | MEDIUM | RESOLVED — positive grep ≥ 1; negative grep (bare static form) == 0 |
| Round-2 MEDIUM 9 (pinned routing table) | MEDIUM | RESOLVED — `ReloadAssetClassifier` exposes named sets as public fields; 22-case [Theory] test exercises the routing table as a pure function |
| Round-2 MEDIUM 10 (loose-override subdir Q2 + Save-As fallback) | MEDIUM | RESOLVED at plumbing level — `IffSaveTargets.RecordSaveAsDirectory` persists the user-chosen directory to `[IffEditor] looseOverrideDir`; the LIVE Q2 resolution itself is deferred (see deferred-items.md) |
| Round-2 MEDIUM 12 (TreRecordIndexResolver linear-scan docs) | MEDIUM | RESOLVED — XML comment documents both linear-scan rationale and duplicate-offset semantics |
| Checker B-1 (no linked-source path into UtinniPlugins) | CHECKER | RESOLVED — 3 framework-side helpers all in `UtinniCoreDotNet/Saving/` or `UtinniCoreDotNet/Formats/Tre/`; `grep -c 'UtinniPlugins' UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` returns 0; CI's Utinni-only checkout builds + tests cleanly |
| Checker W-3 (degraded TRE hand-off → Unknown not LooseFile) | CHECKER | RESOLVED — `TreRecordIndexResolver.ResolveOrUnknown` returns `OpenSource.Unknown.Instance` on resolution failure; `FormIffEditor.OpenFromTreEntry` never falls back to `OpenSource.LooseFile(logicalPath)` |

## Build Verification

**VS2026 MSBuild — `D:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe`:**

- `UtinniCoreDotNet` Debug\|x86 → `bin/Debug/UtinniCoreDotNet.dll` — clean (pre-existing CS0108 warnings only)
- `UtinniCoreDotNet` Release\|x86 → `bin/Release/UtinniCoreDotNet.dll` — clean (same pre-existing warnings)
- `UtinniCoreDotNet.Tests` Debug\|x86 → `UtinniCoreDotNet.Tests/bin/Debug/net472/UtinniCoreDotNet.Tests.dll` — clean (pre-existing xUnit2013 / xUnit2020 analyzer warnings only)
- `TheJawaToolboxDotNet` Debug\|x86 → `bin/Debug/Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll` — clean (zero errors, zero warnings)
- `TheJawaToolboxDotNet` Release\|x86 → `bin/Release/Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll` — clean (zero errors, zero warnings)

**xUnit test pass — `dotnet test UtinniCoreDotNet.Tests --no-build -c Debug`:**

- IFF + Saving subsuite: **143 / 143 passing** — 103 prior IFF (08-01 + 08-04) + 14 LooseOverridePath + 22 ClientReloadDispatcher + 4 TreHandoffFallback.
- Steady-state duration ~ 1.2 s.

## Acceptance Gate Verification (literal greps from the plan)

**Task 1 (LooseOverridePath + csproj):**
- `grep -c '<Compile Include="Saving\\LooseOverridePath.cs"' UtinniCoreDotNet/UtinniCoreDotNet.csproj` → **1** PASS
- `grep -c 'UtinniPlugins' UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` → **0** PASS (no linked-source path)
- `LooseOverridePath.Resolve` exported from UtinniCoreDotNet.dll — PASS by source inspection
- `dotnet test --filter "FullyQualifiedName~LooseOverridePath"` exits 0 — PASS (14/14)

**Task 2 (IffSaveTargets + csproj):**
- `grep -c "Flush(true)\|IffWriter.Write\|LooseOverridePath.Resolve\|UtinniCoreDotNet.Saving" IffSaveTargets.cs` → **≥ 4** PASS
- `grep -c '<Compile Include="Saving\\IffSaveTargets.cs"' TheJawaToolboxDotNet.csproj` → **1** PASS
- SaveInPlace gated on `OpenSource.LooseFile` (W-3) — PASS by source inspection
- SaveLooseOverride returns failure message on Unknown — PASS by source inspection
- SaveAs persists chosen DIR to `[IffEditor] looseOverrideDir` — PASS via `RecordSaveAsDirectory`

**Task 3 (ClientReloadDispatcher + csproj):**
- `grep -c "AddMainLoopCall\|PendingNextSceneChange\|GroundScene\.Get()\.ReloadTerrain" ClientReloadDispatcher.cs` → **≥ 3** PASS
- Bare static `GroundScene.ReloadTerrain` form (no `.Get()`) → **0** PASS (round-2 MEDIUM 7 negative gate)
- Pinned routing-table set names (`TextureExtensions`, `TerrainExtensions`, `DatatableExtensions`, `StringtableExtensions`, `ObjectTemplateExtensions`) referenced — PASS via ReloadAssetClassifier import
- Four distinct outcomes: ReloadedTextures, ReloadedTerrain, PendingNextSceneChange, Unavailable — PASS
- No reload binding called off game thread — PASS by source inspection
- `grep -c '<Compile Include="Saving\\ClientReloadDispatcher.cs"' TheJawaToolboxDotNet.csproj` → **1** PASS
- Routing-table test green — PASS (22/22)

**Task 4 (Save▾ + Reload + OpenSource at open time + TRE hand-off + Plugin.cs):**
- Save▾ has FIVE menu items; Save As… ALWAYS enabled; live-patch + repack DISABLED placeholders — PASS by source inspection
- Reload button DISABLED while save Task in flight — PASS via `saveInFlight` flag
- FormTreBrowser has `Open in IFF Editor` context-menu entry passing payload + ResolvedArchivePath + logical path + ArchiveLocalOffset; browser stays read-only — PASS
- `FormIffEditor.Source = new OpenSource.LooseFile(path)` on Open… — PASS
- `TreRecordIndexResolver.ResolveOrUnknown(...)` called on TRE-Browser hand-off — PASS
- On resolution failure, `Source is OpenSource.Unknown` (NOT LooseFile) — PASS (checker W-3)
- Plugin.cs registers FormIffEditor in try/catch; `GetSubPanels()` returns null (SPI unchanged) — PASS

**Task 4b (TreRecordIndexResolver + csproj + tests):**
- `grep -c '<Compile Include="Formats\\Tre\\TreRecordIndexResolver.cs"' UtinniCoreDotNet/UtinniCoreDotNet.csproj` → **1** PASS
- XML comment documents linear-scan rationale + duplicate-offset semantics — PASS by source inspection
- `dotnet test --filter "FullyQualifiedName~TreHandoffFallback"` exits 0 — PASS (4/4)

**Task 5 (live-SWG smoke):**
- Maintainer approved with response "approved, dig in" — PASS
- Second-open AV regression scenario (the smoke's focal failure mode) passed after mid-smoke `b899504` + `ce2a0a4` hide-not-dispose fixes
- Open Q2 (verified loose-override subdir) and Open Q3 (per-asset reload matrix) deferred (see deferred-items.md); Save-As + ini fallback path is wired and functional

## Output Confirmation

The framework-side `LooseOverridePath` + `ReloadAssetClassifier` + `TreRecordIndexResolver` + the `OpenSource.Unknown` degraded fallback + the FIVE new csproj entries (3 in UtinniCoreDotNet.csproj + 2 in TheJawaToolboxDotNet.csproj) collectively close **checker B-1** (no linked-source path into UtinniPlugins) + **checker W-3** (degraded TRE hand-off → Unknown not LooseFile) + **round-2 HIGH-A** (csproj coverage) respectively.

**Open Q2 disposition:** NOT resolved against the default subdir during the smoke (the maintainer's brevity precluded a verbal enumeration). The Save-As fallback + `[IffEditor] looseOverrideDir` ini-key persistence path is wired and functional — a user-driven Open-Q2 resolution remains available at any point, and the next loose-override save defaults to whichever directory the user picks. Open Q2 deferred for a follow-on observation pass (see `deferred-items.md`).

## Commits

**Utinni repo (5 commits):**
- `aca7a5e` — `feat(08-05): LooseOverridePath framework-side root-containment helper + tests`
- `eb68ddb` — `feat(08-05): ReloadAssetClassifier framework-side routing table + tests`
- `a9f8ca7` — `feat(08-05): TreRecordIndexResolver + degraded-handoff tests (W-3)`
- `cad18b1` — `docs(08-05): STATE — auto tasks 1-4 + 4b complete; Task 5 checkpoint awaiting`
- (this SUMMARY commit + STATE/ROADMAP updates)

**UtinniPlugins repo (5 commits):**
- `785e074` — `feat(08-05): IffSaveTargets — loose-override + Save/Save-As + Flush(true) barrier`
- `b70d10e` — `feat(08-05): ClientReloadDispatcher — tiered D-06 forced reload via game thread`
- `05884c3` — `feat(08-05): wire Save▾ + Reload + TRE-browser hand-off + Plugin registration`
- `b899504` — `fix(08-05): hide-not-dispose FormIffEditor (smoke defect — second open)` ← mid-smoke AV closure
- `ce2a0a4` — `fix(08-05): hide-not-dispose FormTreBrowser (defensive — same latent bug class)` ← mid-smoke defensive

## Working-Tree State

- `UtinniCoreDotNet/Generated/UtinniCore.cs` shows as `M` in the Utinni working tree. This is the same pre-existing CppSharp-regen drift flagged by the plan's `assumes:` block — NOT staged by any of this plan's commits. The binding-line citations referenced by the plan (`Graphics.ReloadTextures`, `GroundScene.Get().ReloadTerrain`) resolve correctly against the committed file; the working-tree drift does not affect any 08-05 artifact.
- `.planning/ui-reviews/` is an untracked working-tree dir present at session start; unrelated to 08-05.

## Self-Check: PASSED

**Files verified present:**

- `D:/Code/Utinni/UtinniCoreDotNet/Saving/LooseOverridePath.cs` — FOUND
- `D:/Code/Utinni/UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs` — FOUND
- `D:/Code/Utinni/UtinniCoreDotNet/Formats/Tre/TreRecordIndexResolver.cs` — FOUND
- `D:/Code/Utinni/UtinniCoreDotNet.Tests/SavingTests/LooseOverridePathTests.cs` — FOUND
- `D:/Code/Utinni/UtinniCoreDotNet.Tests/SavingTests/ClientReloadDispatcherTests.cs` — FOUND
- `D:/Code/Utinni/UtinniCoreDotNet.Tests/FormatsTests/Iff/TreHandoffFallbackTests.cs` — FOUND
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/IffSaveTargets.cs` — FOUND
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/ClientReloadDispatcher.cs` — FOUND
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` — FOUND (modified: Save▾ + Reload + OpenFromTreEntry + hide-not-dispose intercept)
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs` — FOUND (modified: Open-in-IFF-Editor context-menu entry + hide-not-dispose intercept)
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs` — FOUND (modified: FormIffEditor registration)

**Commits verified present (`git log --oneline` substring match):**

Utinni repo:
- `aca7a5e` — `feat(08-05): LooseOverridePath ...` — FOUND
- `eb68ddb` — `feat(08-05): ReloadAssetClassifier ...` — FOUND
- `a9f8ca7` — `feat(08-05): TreRecordIndexResolver ...` — FOUND
- `cad18b1` — `docs(08-05): STATE — auto tasks ...` — FOUND

UtinniPlugins repo:
- `785e074` — `feat(08-05): IffSaveTargets ...` — FOUND
- `b70d10e` — `feat(08-05): ClientReloadDispatcher ...` — FOUND
- `05884c3` — `feat(08-05): wire Save▾ + Reload ...` — FOUND
- `b899504` — `fix(08-05): hide-not-dispose FormIffEditor ...` — FOUND
- `ce2a0a4` — `fix(08-05): hide-not-dispose FormTreBrowser ...` — FOUND

**Test counts verified by execution:** 143/143 IFF + Saving tests passing (14 LooseOverridePath + 22 ClientReloadDispatcher + 4 TreHandoffFallback + 103 prior IFF subsuite).
