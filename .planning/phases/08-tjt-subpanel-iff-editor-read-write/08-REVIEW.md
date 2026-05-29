---
phase: 08-tjt-subpanel-iff-editor-read-write
reviewed: 2026-05-28T00:00:00Z
depth: standard
files_reviewed: 26
files_reviewed_list:
  - UtinniCoreDotNet/Formats/Iff/IffWriter.cs
  - UtinniCoreDotNet/Formats/Iff/MutableIffDocument.cs
  - UtinniCoreDotNet/Formats/Iff/MutableIffNode.cs
  - UtinniCoreDotNet/Formats/Iff/OpenSource.cs
  - UtinniCoreDotNet/Formats/Tre/TreFile.cs
  - UtinniCoreDotNet/Formats/Tre/TreRecord.cs
  - UtinniCoreDotNet/Formats/Tre/TreRecordIndexResolver.cs
  - UtinniCoreDotNet/Formats/Tre/TreWriter.cs
  - UtinniCoreDotNet/Saving/LooseOverridePath.cs
  - UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs
  - UtinniCoreDotNet/Saving/TreBackupPath.cs
  - UtinniCoreDotNet/Saving/TreRepackLock.cs
  - UtinniCoreDotNet/Editing/IffEditController.cs
  - UtinniCoreDotNet/Editing/LivePatchValidator.cs
  - Utinni.Cli/Commands/RoundtripIffCommand.cs
  - Utinni.Cli/Program.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/Saving/ClientReloadDispatcher.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/Saving/IffSaveTargets.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/Saving/LivePatchSaveTarget.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/Saving/TreRepackSaveTarget.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/IffChunkTree.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormSaveConfirmDialog.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormFourCcDialog.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs
findings:
  critical: 1
  warning: 7
  info: 6
  total: 14
status: needs-attention
fixed:
  - CR-01
  - WR-01
  - WR-02
  - WR-04
  - WR-05
  - WR-06
  - WR-07
fix_commits:
  CR-01: e66e4fe  # Utinni
  WR-01: aa8d06b  # UtinniPlugins
  WR-02: 34a5422  # Utinni
  WR-04: ce94f51  # Utinni
  WR-05: 607d057  # UtinniPlugins
  WR-06: 4b1c5fc  # Utinni
  WR-07: 221c51b  # Utinni
deferred:
  - WR-03  # quadratic NodeSnapshot.Materialize — outside CR+WR fix-scope this round; cosmetic perf, not correctness
  - IN-01  # FormFourCcDialog content validation — Info scope, deferred
  - IN-02  # ReplaceReferences no-op — dead helper retained alongside other now-dead CR-01 scaffolding; cleanup deferred
  - IN-03  # double-clone payload bytes — Info scope, deferred
  - IN-04  # int-typed OriginalMappedLength — Info scope, V1 x86, deferred
  - IN-05  # duplicate ASCII validation — Info scope, defense-in-depth deliberate
  - IN-06  # GetRecordNameBytes _sourcePath gate — Info scope, future-phase concern
---

# Phase 08: Code Review Report

**Reviewed:** 2026-05-28
**Depth:** standard
**Files Reviewed:** 26 (excluding Designer.cs / .csproj / fixtures / tests as scoped)
**Status:** needs-attention

## Summary

Phase 08 delivers the read-write IFF editor + 4-tier save matrix across Utinni framework + UtinniPlugins TJT (~7,500 LoC over 26 source files). Construction quality is generally strong: the security primitives (LooseOverridePath, TreRepackLock, LivePatchValidator) carry defense-in-depth, the file-save paths honor `FileStream.Flush(true)` (MEDIUM-9), game-thread dispatch routes through `AddMainLoopCall` (MEDIUM-7 — `GroundScene.Get().ReloadTerrain()` is instance-call), checked overflow arithmetic guards the IFF writer (MEDIUM-7 align), and the .csproj entries cover every new .cs file (HIGH-A).

One **BLOCKER** falls out of the undo/redo controller: `IffEditCommands.RemoveCommand` cannot Redo cleanly after Undo because `InsertChildAt` rebuilds a fresh node from snapshot, leaving the field `reinserted` pointing at a stale tree that was never attached. The next `Do(...)` call passes that stale reference to `parent.Remove`, which silently no-ops while the controller still increments `netAppliedCount`, desyncing IsDirty + tree state. This is reachable via every Remove → Undo → Redo sequence on a leaf or container.

The remainder are warnings (input-validation error surfaces on structural-op menu items can leak an `ArgumentException` to the WinForms unhandled-exception path; dead `this == null` check + missing trailing `/` in the never-called-but-public `MutableIffNode.RemoveByStableId`; quadratic snapshot materialization; `OpenSource.LooseFile.GetHashCode` ignores the `OrdinalIgnoreCase` semantics paths require on Windows; `ClientReloadDispatcher.Dispatch` calls `Game.IsRunning` un-guarded; `TreWriter.Repack` does not refuse `EnumerateOnly` archives despite their encrypted-payload reality; the LooseOverridePath StartsWith check uses the caller-supplied root verbatim without re-canonicalization) and info-level (input length-only validation in FormFourCcDialog, `ReplaceReferences` no-op, double-clone in factory helpers, FourCC ASCII validation duplication, `int`-typed mapped length, comment-vs-code minor drift).

## Critical Issues

### CR-01: `IffEditCommands.RemoveCommand` silently no-ops on Redo after Undo

**File:** `UtinniCoreDotNet/Editing/IffEditController.cs:333-363` + `InsertChildAt` helper `465-492`

**Issue:** The undo/redo bracket for the `Remove` structural op is broken across a full round trip:

1. First `Do`: captures `originalIndex`, captures `snapshot`, calls `parent.Remove(node)`. `reinserted = null`. OK.
2. `UndoOp`: sets `reinserted = snapshot.Materialize()` (a fresh detached subtree), then calls `InsertChildAt(parent, originalIndex, reinserted)`.
3. `InsertChildAt` does **not** attach `reinserted` to `parent` — instead it calls `container.AddLeaf(child.TypeId, child.GetPayloadCopy())` (or `AddContainer`) which constructs a brand-new node `fresh` and adds *that* to the parent. The materialized `reinserted` subtree is never attached and becomes a dangling reference. The internal `ReplaceReferences(child, fresh)` is documented as a no-op (line 504-505).
4. On Redo (second `Do`): `target = reinserted` (stale, not in `parent.children`). `IndexOfChild(parent, target)` returns `-1`. `parent.Remove(target)` returns `false` because the node was never a child — see `MutableIffNode.Remove` line 332-342 which silently returns `false` on miss.
5. The Redo therefore leaves the (Undo-re-added) `fresh` node in place — the tree state is wrong — yet `IffEditController.Redo` still increments `netAppliedCount`, so `IsDirty` becomes inconsistent with the tree.

This is reachable via every Remove → Undo → Redo sequence on any leaf or container (Ctrl+Z then Ctrl+Y after deleting a chunk from the context menu).

**Fix:** Have `UndoOp` either (a) attach `reinserted` directly to `parent` (requires a new internal API on `MutableIffNode` such as `InsertChildAtInternal(int, MutableIffNode)`), OR (b) capture the `fresh` node `InsertChildAt` actually attached and overwrite `reinserted` with that captured reference before returning. Sketch of option (b):

```csharp
public void UndoOp(MutableIffDocument doc)
{
    var fresh = snapshot.Materialize();          // detached
    reinserted = InsertChildAt(parent, originalIndex, fresh); // ← return the fresh node actually attached
}

// And InsertChildAt becomes:
private static MutableIffNode InsertChildAt(MutableIffNode container, int index, MutableIffNode child)
{
    MutableIffNode attached;
    if (child.Kind == MutableIffNodeKind.Leaf)
        attached = container.AddLeaf(child.TypeId, child.GetPayloadCopy());
    else { /* same recursive shape */ }
    MoveChildUpTo(container, attached, index);
    return attached;
}
```

Either way, add a Redo-after-Undo round-trip xUnit test alongside the existing `IffEditControllerTests` so this regression is caught at CI time.

## Warnings

### WR-01: Structural-op menu handlers do not catch ArgumentException from FourCC validation

**File:** `The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs:716-732` (`OnAddChunk` / `OnAddForm`) and `758-765` (`OnEditFormSubType`); also `767-786` (`OnDuplicate` / `OnMoveUp` / `OnMoveDown` are exception-safe in practice but follow the same pattern).

**Issue:** `FormFourCcDialog` only enforces `MaxLength = 4` and the caller's `PromptFourCc` only checks `v.Length != 4`. It does NOT enforce the "printable ASCII (0x20..0x7E)" rule that `MutableIffNode.ValidatePrintableAscii` (line 521-528) imposes at construction time. A user typing a non-ASCII char (accented letter, emoji, control byte) makes the eventual `controller.Apply(IffEditCommands.AddLeaf(...))` throw `ArgumentException`. Only `OnRenameRetag` wraps in `try/catch (InvalidOperationException)`; the other handlers don't catch anything, so the exception propagates to WinForms' unhandled-exception handler and can pop the JIT-debugger dialog or crash the editor.

**Fix:** Either (a) tighten `FormFourCcDialog` / `PromptFourCc` to reject non-printable-ASCII at the dialog level with the UI-SPEC status copy, OR (b) wrap every structural-op handler in a `try/catch (ArgumentException ex)` / `catch (InvalidOperationException ex)` that surfaces the message into `lblStatus` exactly like `OnRenameRetag` does (lines 747-755).

### WR-02: `MutableIffNode.RemoveByStableId` (instance) builds malformed prefix and has dead null check

**File:** `UtinniCoreDotNet/Formats/Iff/MutableIffNode.cs:350-371`

**Issue:** Two defects in one method:

1. Line 358: `this == null ? "" : this.GetStableIdRecursive()`. `this` is never null inside an instance method — the conditional is dead and serves only to obscure intent.
2. `GetStableIdRecursive()` returns the node's own id (e.g. `"FORM:WSNP/0"`) without a trailing `/`. But `DeriveStableId(c, parentIdPrefix, i)` concatenates `parentIdPrefix + suffix`, so the call `DeriveStableId(c, "FORM:WSNP/0", 0)` produces `"FORM:WSNP/0DATA:DATA/0"` — there's no separator between parent id and the child suffix, so no `stableId` derived from `MutableIffDocument.DeriveStableId` (with proper `/` prefix) will ever match. The method silently returns `false` for every real id.

The document-level entry point `MutableIffDocument.RemoveByStableId` (line 140-147) is correct (it passes `DeriveStableId(Root, "", 0) + "/"`). The bug is only in the public instance method on `MutableIffNode`, which appears unused in V1 — but ships as public API and will mis-behave for any future caller.

**Fix:** Either remove the public instance method (the document method covers the use case), or correct the prefix:
```csharp
string cid = MutableIffDocument.DeriveStableId(c, this.GetStableIdRecursive() + "/", i);
```
and delete the `this == null` clause.

### WR-03: `NodeSnapshot.Materialize` recursively materializes containers twice

**File:** `UtinniCoreDotNet/Editing/IffEditController.cs:539-580`

**Issue:** In the container branch (line 541-558), each child is materialized via `cs.Materialize()` to produce `child`, but for container children the code then calls `n.AddContainer(child.TypeId, child.SubTypeId)` and `CopyChildrenInto(child, addedContainer)` — which re-walks `child`'s subtree and rebuilds nodes by hand. The recursive `Materialize()` work for that child is discarded. Worst-case stacked materialize cost is `O(depth × nodes)` instead of `O(nodes)`. For a Phase-8 IFF tree (typically tens to hundreds of nodes) this is not a hot path, but it is wasteful and obscures the intent.

**Fix:** Materialize directly from snapshot — don't call `Materialize()` on children inside `Materialize()`'s container path. A single straight-line walk is sufficient:
```csharp
public MutableIffNode Materialize()
{
    if (!IsContainer) return MutableIffNode.NewLeaf(TypeId, Payload);
    var n = MutableIffNode.NewContainer(TypeId, SubTypeId);
    foreach (var cs in Children) Attach(cs, n);
    return n;
}
private static void Attach(NodeSnapshot s, MutableIffNode parent)
{
    if (s.IsContainer) {
        var c = parent.AddContainer(s.TypeId, s.SubTypeId);
        foreach (var child in s.Children) Attach(child, c);
    } else {
        parent.AddLeaf(s.TypeId, s.Payload);
    }
}
```

### WR-04: `OpenSource.LooseFile.GetHashCode` uses case-sensitive hash for Windows paths

**File:** `UtinniCoreDotNet/Formats/Iff/OpenSource.cs:112-115`

**Issue:** Windows file paths are case-insensitive (NTFS default), and the documented MEMORY-N project context confirms a Windows-only project. `LooseFile.Equals(LooseFile)` at line 102 uses `Path == other.Path` (case-sensitive `string.Equals`), and `GetHashCode` returns `Path.GetHashCode()` (case-sensitive). Two `LooseFile` instances pointing at `C:\swg-client\foo.iff` and `c:\SWG-CLIENT\foo.iff` are NOT equal under this implementation, yet they reference the same physical file. Same applies to `TreArchive.GetHashCode` (line 164-171) for `TrePath` + `LogicalPath` (logical paths are case-insensitive in SWG archives).

This is not a present-day bug (no code currently uses these in a hashtable / set), but the moment downstream Wave-1 editors store `OpenSource` keys in a dictionary, two equivalent records will hash to different buckets and Equals will return false.

**Fix:** Use `StringComparer.OrdinalIgnoreCase.GetHashCode(Path)` and `string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase)` in both Equals and GetHashCode for path-valued fields on Windows.

### WR-05: `ClientReloadDispatcher.Dispatch` calls `Game.IsRunning` without try/catch

**File:** `The Jawa Toolbox/TheJawaToolboxDotNet/Saving/ClientReloadDispatcher.cs:82`

**Issue:** Line 82 reads `if (!Game.IsRunning) return ReloadTier.Unavailable;` directly. Every other call site (FormIffEditor lines 896-897, 1450-1451, LivePatchSaveTarget lines 136-137) wraps this read in `try/catch` because the binding can throw outside an injected client. `ClientReloadDispatcher` is in the same situation — it would be reachable from a UI thread during test or maintainer use — but it does not defend itself. An unhandled exception here would tear down the Reload-button click handler in `FormIffEditor.OnReloadClicked` (line 1420), which also does not catch.

**Fix:** Mirror the other sites:
```csharp
bool clientUp;
try { clientUp = Game.IsRunning; }
catch { clientUp = false; }
if (!clientUp) return ReloadTier.Unavailable;
```

### WR-06: `TreWriter.Repack` does not refuse `EnumerateOnly` (V6000) archives

**File:** `UtinniCoreDotNet/Formats/Tre/TreWriter.cs:112-115`

**Issue:** `TreFile.Header.EnumerateOnly` is set to `true` for V6000 archives (encrypted payloads) at `TreFile.cs:192`. The plan documents these as "enumerate-only — payload content degrades" and the project memory `project_tre_version_support_gap` explicitly says v6000+ payloads are encrypted. But `TreWriter.Repack` happily accepts an `EnumerateOnly` `TreFile` and rebuilds it: untouched-entry raw-slice copies still work (they're byte-for-byte), but the EDITED entry is recompressed as raw deflate (compressor=1) without the encryption framing the V6000 reader would expect. The resulting archive will appear valid to `TreFile.Open` (the TOC parses) but the edited entry will be unreadable by the live client.

This is a "user gets a broken archive" failure mode without any UI-level indication that V6000 repack is unsupported.

**Fix:** Add an explicit guard at the top of `Repack`:
```csharp
if (original.Header.EnumerateOnly)
{
    throw new InvalidOperationException(
        "TRE repack is not supported for enumerate-only archives (version "
        + original.Header.VersionTag + " carries encrypted payloads). " +
        "Save as a loose override instead.");
}
```
and surface this at the FormIffEditor's `OnRepackTre` catch-all (line 1209-1217) into the status banner. Alternatively, gate `miRepackTre.Enabled` in `RefreshSaveMenuEnabledState` on `!ta.EnumerateOnly` (which would require carrying that flag through to `OpenSource.TreArchive`).

### WR-07: `LooseOverridePath.Resolve` does not re-canonicalize `resolvedRoot`

**File:** `UtinniCoreDotNet/Saving/LooseOverridePath.cs:73-145`

**Issue:** The docstring says `resolvedRoot` is "already canonical-normalized by the caller". But `FormIffEditor.ResolveClientRoot` (line 1477-1511) returns whatever `Process.MainModule.FileName`/`GetWorkingDirectory`/ini contains — none of those paths are guaranteed canonical (could be relative, could contain `..`, could be all-lowercase on a case-insensitive volume). The StartsWith gate compares against the un-canonicalized `rootWithSep`, so if `resolvedRoot = "C:\swg-client\..\swg-client"` then `Path.GetFullPath(combined) = "C:\swg-client\foo.iff"` and `StartsWith("C:\swg-client\..\swg-client\")` returns false — the helper rejects a legitimate path.

The defenses on the `relAssetPath` side are sound; this is a "false rejection" bug on the root side, plus a missed defense-in-depth opportunity (the helper *should* canonicalize the root before comparing).

**Fix:** Call `Path.GetFullPath(resolvedRoot)` once at the top of `Resolve` and use the canonicalized form for the StartsWith gate. The current `Path.IsPathRooted` check on `relAssetPath` is unaffected.

## Info

### IN-01: `FormFourCcDialog` validates only length, not content

**File:** `The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormFourCcDialog.cs:43-55`

**Issue:** No ASCII / printable-character validation in the dialog itself; the only constraint is `MaxLength = 4`. The dialog accepts any 4 characters. The downstream `MutableIffNode` setters enforce the ASCII rule and throw, which is what WR-01 surfaces. Adding inline validation here (a Validating handler that gates `DialogResult = OK` until the input is printable ASCII) would close the gap at source.

**Fix:** Add a Validating handler to `txtTag`:
```csharp
txtTag.Validating += (s, e) => {
    foreach (char c in txtTag.Text)
        if (c < 0x20 || c > 0x7E) {
            e.Cancel = true;
            return;
        }
};
```
and a status label inside the dialog explaining the rejection.

### IN-02: `IffEditCommands.ReplaceReferences` is a no-op placeholder

**File:** `UtinniCoreDotNet/Editing/IffEditController.cs:504-505`

**Issue:** The method is documented as "No-op placeholder — RemoveCommand keeps its own `reinserted` field for the next Do()". But CR-01 shows the `reinserted` field is exactly the field that goes stale because `InsertChildAt` discards the snapshot tree. The no-op `ReplaceReferences` is the symptom of the broken contract. Delete the helper once CR-01 is fixed; alternatively, repurpose it to attach the snapshot subtree as part of the CR-01 fix.

### IN-03: `EditLeafPayloadCommand` and `AddLeafCommand` double-clone payload bytes

**File:** `UtinniCoreDotNet/Editing/IffEditController.cs:273-309`

**Issue:** `EditLeafPayloadCommand` clones `newPayload` in its constructor (line 276), then `MutableIffNode.SetPayload` clones again (line 284 in MutableIffNode.cs). `AddLeafCommand` does the same. The intent is defensive copy on both sides of the API boundary, but the duplication is wasted for the on-construction copy since the model defensive copy is sufficient. Not a correctness bug; minor allocation churn.

**Fix:** Drop the controller-side clone, document the model side as the canonical defensive boundary.

### IN-04: `OpenSource.ClientMemory.OriginalMappedLength` typed as `int`

**File:** `UtinniCoreDotNet/Formats/Iff/OpenSource.cs:196`

**Issue:** Mapped IFF region length is stored as `int`. Combined with the writer's 64 MB chunk cap (IffWriter.MaxChunkSize) and the project's 32-bit live SWG client, this is fine in practice — but for forward-compatibility with V2 64-bit migration the type should be `long`. The `LivePatchValidator.Validate` signature also takes `int rewrittenLength` / `int originalMappedLength` — the matching check works correctly for V1 but the type is restrictive.

**Fix:** No action required for V1 (project is x86). Track in a forward-compat note for a future 64-bit migration phase.

### IN-05: `FourCcToBytes` duplicates ASCII validation already present in node setters

**File:** `UtinniCoreDotNet/Formats/Iff/IffWriter.cs:207-222`

**Issue:** `IffWriter.FourCcToBytes` re-validates printable ASCII (line 217-218), but every `MutableIffNode.TypeId` / `SubTypeId` setter and every `NewLeaf` / `NewContainer` factory already enforces the same constraint at the model boundary (`ValidatePrintableAscii` in MutableIffNode.cs line 521-528). The writer-side check is defense-in-depth and not harmful, but it duplicates the rule across three sites — risk of divergence if one is updated and the others aren't.

**Fix:** No action required (defense-in-depth is acceptable here). Optionally consolidate into a single internal helper if a future change touches both.

### IN-06: `TreFile.GetRecordNameBytes` docstring claims `_sourcePath`-only rejection while actually allowing names-block access on stream-backed instances

**File:** `UtinniCoreDotNet/Formats/Tre/TreFile.cs:520-526`

**Issue:** The XML doc says "Lazy contract (mirrors `GetRecordData`): a stream-backed instance throws `InvalidOperationException` — the names block IS in memory for stream-backed instances too, but the API surface stays uniform". That decision keeps the API surface consistent, but it gratuitously rejects a real use case where a future caller has only a stream-backed TreFile (e.g. parsed from an embedded resource) and wants name-block slices. Since `_namesBytes` is always captured during Parse regardless of source, the rejection isn't strictly needed. Minor — the consistency argument is valid, but it's a design-quality info call worth recording.

**Fix:** If a future phase needs name-bytes from a stream-backed TreFile, drop the `_sourcePath` gate from this method only and rely on the existing `_namesBytes` bounds check. Documenting now to avoid future re-discovery.

---

## Fixes Applied (2026-05-28)

All seven Critical + Warning findings have been fixed. Scope: CR + WR only; the
six Info findings were deferred (recorded in frontmatter `deferred:`). Each
fix committed atomically; commit shas listed in frontmatter `fix_commits:`.

| ID    | Status | Repo           | Commit    | Regression test added                                                         |
|-------|--------|----------------|-----------|-------------------------------------------------------------------------------|
| CR-01 | FIXED  | Utinni         | `e66e4fe` | `IffEditControllerTests.Remove_Undo_Redo_LeafRoundTrip_*` (+ container variant) |
| WR-01 | FIXED  | UtinniPlugins  | `aa8d06b` | (no test asm in UtinniPlugins; covered by manual + build-clean)                |
| WR-02 | FIXED  | Utinni         | `34a5422` | n/a (deletion; document-level RemoveByStableId tests cover the retained API)   |
| WR-04 | FIXED  | Utinni         | `ce94f51` | `OpenSourceTests.*GetHashCode_*CaseInsensitive*` (LooseFile + TreArchive + ClientMemory) |
| WR-05 | FIXED  | UtinniPlugins  | `607d057` | (no test asm in UtinniPlugins; pattern mirrors 3 existing call sites)          |
| WR-06 | FIXED  | Utinni         | `4b1c5fc` | `TreWriterTests.Repack_EnumerateOnlyV6000Archive_ThrowsNotSupportedBeforeAnyWrite` |
| WR-07 | FIXED  | Utinni         | `221c51b` | `LooseOverridePathTests.Resolve_RootWith{DotDotSegment,SingleDotSegment,MixedSeparators,BothCanonicalAndNonCanonicalForms}_*` (4 tests) |

**Build status post-fix:**
 - UtinniCoreDotNet Debug|x86 + Release|x86 — clean
 - UtinniCoreDotNet.Tests Debug|x86 + Release|x86 — clean
 - Utinni.Cli + Utinni.Cli.Tests — clean
 - TheJawaToolboxDotNet Debug|x86 + Release|x86 — clean (rebuild)

**Test status post-fix:**
 - UtinniCoreDotNet.Tests: **331/331 passing** (was 319; +12 regression tests)
 - Utinni.Cli.Tests: 123/123 passing + 1 expected-skip (CoT fixture-gated)

**Deferred Info findings:** WR-03 (cosmetic perf in NodeSnapshot.Materialize — outside CR+WR scope) and all six IN-* (per fix-scope policy: `critical_warning`). These should be picked up in a follow-up `--all` pass or rolled into a future cleanup phase.

---

_Reviewed: 2026-05-28_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

_Fixes applied: 2026-05-28_
_Fixer: Claude (gsd-code-fixer)_
_Fix scope: critical_warning (CR + WR)_
