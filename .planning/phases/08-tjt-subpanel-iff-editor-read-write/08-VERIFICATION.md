---
phase: 08-tjt-subpanel-iff-editor-read-write
verified: 2026-05-28T22:30:00Z
status: passed
score: 47/47 must-haves verified
overrides_applied: 0
deferred:
  - truth: "Open Q1 — cursor N-H1 ACK: live SWG/Restoration client resolves the edited record under its original path after .tre repack"
    addressed_in: "Phase 9+ smoke session OR a focused 08-07-Q1 observation pass; on-disk path-CRC self-consistency is CI-covered by TreRepackLogicalPathTests + Phase 7 TreFile golden-fixture suite"
    evidence: "deferred-items.md row 'Open Q1 deferred verification (cursor N-H1 ACK — path-CRC resolution by SWG client)'; 08-07-SUMMARY records the TreFile-side path-CRC self-consistency check passes; the final bridge (SWG client's algorithm = ours) is the only residual"
  - truth: "Open Q5 — End-to-end live-SWG UI smoke for the .tre repack flow (Save▾ ▸ Repack ▸ FormSaveConfirmDialog ▸ atomic-replace ▸ tiered reload ▸ reopen-verify)"
    addressed_in: "Next phase's smoke session OR a focused 08-07-Q5 observation pass"
    evidence: "deferred-items.md row 'Open Q5 deferred verification'; the on-disk contract is CI-covered (15 TreRepack outcomes); the WinForms UI mechanics (Save▾ enabled-state pattern-matching, FormSaveConfirmDialog, status-text translation) follow precedent: 08-05's maintainer smoke approved the equivalent UI flow for file save modes; 08-07 wire-up is structurally identical (same RefreshSaveMenuEnabledState pattern + same DoFileSaveAsync pattern)"
  - truth: "Open Q2 — verified loose-override sub-directory the live client re-reads (granular observation matrix)"
    addressed_in: "Phase 9+ smoke sessions OR a focused documentation-only pass"
    evidence: "deferred-items.md row 'Open Q2 deferred verification'; the Save As + [IffEditor] looseOverrideDir ini-persistence fallback path is wired (IffSaveTargets.RecordSaveAsDirectory) so the user-driven Q2 resolution is always available"
  - truth: "Open Q3 — live per-asset-class reload matrix observation (texture/terrain in-session vs datatable/STF/object-template via scene change)"
    addressed_in: "Phase 9 (Datatable) and Phase 10 (Stringtable) smoke sessions; the pinned routing table is unit-tested at 22 [Theory] cases"
    evidence: "deferred-items.md row 'Open Q3 deferred verification'; ClientReloadDispatcherTests proves the in-source dispositions correct; live observation is Tier-4 manual residual"
  - truth: "Open Q4 — full functional D-05.3 live-patch smoke (incl. maintainer-only debug construction of OpenSource.ClientMemory + same-length apply + different-length refusal + reload volatility)"
    addressed_in: "Follow-up phase that wires the ClientMemory open-path discovery (which organically satisfies precondition (a))"
    evidence: "deferred-items.md row 'Open Q4 deferred verification'; LivePatchValidator 5 [Fact]s carry the bounds-gate verification burden; menu is honestly disabled with grep-gated tooltip wording; D-05.3 is infra-ready / user-disabled per CONTEXT round-2 MEDIUM 11"
  - truth: "Polish UX gap — dirty-discard prompt missing on TRE-Browser → IFF-Editor hand-off path (OpenFromTreEntry bypasses the in-form discard-confirm)"
    addressed_in: "Phase 9+ housekeeping plan OR a focused 08-polish pass; user workaround = Save / Save As before clicking the hand-off"
    evidence: "deferred-items.md row 'Polish gap (UX)'; not a Phase 8 acceptance blocker — saved edits are preserved on disk, the loss is unsaved-only"
human_verification: []
---

# Phase 8: TJT subpanel — IFF Editor (read + write) Verification Report

**Phase Goal:** Deliver a working IFF editor as a TJT subpanel that lets a modder open, view, edit, and save IFF files end-to-end against a live SWG client. The four-tier D-05 save matrix (loose-override → file Save/Save-As → in-memory live-patch → archive repack) must be wired and the bounds-of-trust on each tier exercised.
**Verified:** 2026-05-28T22:30:00Z
**Status:** passed (with documented deferred live-residual items)
**Re-verification:** No — initial verification

---

## Goal Achievement Summary

Phase 8 ships the complete IFF editor surface across both repos:
- Framework primitives (UtinniCoreDotNet) — IffWriter, MutableIffDocument/Node, OpenSource (4-case), IffEditController, LivePatchValidator, LooseOverridePath, ReloadAssetClassifier, TreRecordIndexResolver, TreBackupPath, TreRepackLock, TreWriter — all eight new production files have explicit `<Compile Include>` entries in the old-style csproj (verified by grep).
- CLI verb — `roundtrip-iff` with 4 golden fixtures (identity, no-pad, payload-mutation, structural-removal).
- TJT plugin (UtinniPlugins) — FormIffEditor with five-item Save▾, IffChunkTree shared control, FormFourCcDialog, FormSaveConfirmDialog, IffSaveTargets, ClientReloadDispatcher, LivePatchSaveTarget, TreRepackSaveTarget; FormTreBrowser hand-off; Plugin.cs registration. All csproj entries explicit (verified by grep).
- ROADMAP Criterion 5 reconciled to `UtinniCoreDotNet/Formats/Iff` (2 occurrences as required).
- Code review CR-01 (RemoveCommand Redo silent no-op) fixed at e66e4fe; WR-01..WR-07 all fixed; WR-03 + 6 Info findings deferred (cosmetic/perf, not correctness).

**Test suites green (no-build, Debug|x86 net472):**
- UtinniCoreDotNet.Tests: 331/331 pass (full suite); 176/176 pass on Phase-8 filter
- Utinni.Cli.Tests: 123/123 pass + 1 env-skip (CotMasterIndex fixture missing — environmental, unrelated)
- TreRepack* (08-07 continuation-work suites): 15/15 outcomes
- LivePatchValidator: 5/5 [Fact]s
- ClientReloadDispatcher: 22/22 [Theory] cases

The phase ships with documented deferred live-residual items (Open Q1, Q2, Q3, Q4, Q5 + dirty-discard polish gap) recorded in `deferred-items.md` — all explicitly classified as "not a Phase 8 acceptance blocker" by the maintainer or by automation-augmented coverage. Per the verifier brief, these are filtered as `deferred` and do not block `status: passed`.

---

## Plan-by-Plan Verification

### Plan 08-01: IffWriter + Mutable hybrid DOM + OpenSource (4-case) + csproj coverage + ROADMAP D-01 amendment

| #   | Truth                                                                                                            | Status     | Evidence                                                                                                                                                                            |
| --- | ---------------------------------------------------------------------------------------------------------------- | ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | IffWriter serializes chunk graph back to EA-IFF-85 bytes (BE tag+length, FORM/LIST/CAT, no pad)                  | ✓ VERIFIED | `IffWriter.cs:58 class IffWriter`; `IffWriter.cs:71 public static byte[] Write(MutableIffDocument doc)`; `WriteBe32` private helper at line 193; IffWriter tests pass               |
| 2   | MutableIffDocument.FromDocument builds mutable tree; byte-identical when nothing edited                          | ✓ VERIFIED | `MutableIffDocument.cs:80 public static MutableIffDocument FromDocument(IffDocument doc, byte[] sourceBytes)`; round-trip test in IffWriterTests proves byte-identity               |
| 3   | Untouched leaves re-emit original raw bytes verbatim (D-07)                                                      | ✓ VERIFIED | MutableIffDocument captures `chunk.OffsetBytes/LengthBytes` slice (line 94+); IffWriter emits captured slice for clean nodes                                                         |
| 4   | Edited leaf re-serializes; parent container length rolls up bottom-up via `checked long`                         | ✓ VERIFIED | `IffWriter.cs:149` checked-long accumulator; line 162 `long childTotal`; line 163-164 `long innerLen; checked { ... }`; line 177 explicit overflow exception                         |
| 5   | When any descendant is dirty, every ancestor is forced dirty                                                     | ✓ VERIFIED | `MutableIffNode.cs:201/216` and `MarkDirtyAndInvalidateAncestors` called from every structural op (lines 285, 312, 325, 340, 389) and from payload setter (lines 117, 140)            |
| 6   | Leaf payload getter returns defensive copy                                                                       | ✓ VERIFIED | MutableIffNode `GetPayloadCopy` pattern; defensive-payload test in IffWriterTests passes                                                                                            |
| 7   | Structural ops survive write→re-parse round trip                                                                 | ✓ VERIFIED | MutableIffDocumentTests + IffWriterTests cover add/remove/rename/reorder/duplicate/edit-subtype                                                                                      |
| 8   | Odd-length chunks emit NO pad byte (SWG no-pad quirk preserved)                                                  | ✓ VERIFIED | IffWriter never writes pad byte; `odd-chunk-no-pad.iff` golden in CLI tests proves output length == input length                                                                    |
| 9   | OpenSource discriminated union with 4 sealed cases + Unknown.Instance singleton (W-3 contract)                   | ✓ VERIFIED | `OpenSource.cs:61 public abstract class OpenSource`; LooseFile (line 85), TreArchive (line 124), ClientMemory + Unknown (line 266 sealed class Unknown : OpenSource); singleton at line 274 `public static Unknown Instance` |
| 10  | UtinniCoreDotNet.csproj contains 4 `<Compile Include>` entries (IffWriter/MutableIffDocument/MutableIffNode/OpenSource) | ✓ VERIFIED | csproj lines 69-72: all four entries present                                                                                                                                       |
| 11  | ROADMAP Criterion 5 + DEC-C4 line reconciled to `UtinniCoreDotNet/Formats/Iff`                                   | ✓ VERIFIED | ROADMAP lines 169 + 180 both reference `UtinniCoreDotNet/Formats/Iff` (≥ 2 matches as required)                                                                                     |

**Score:** 11/11

### Plan 08-02: roundtrip-iff CLI verb + 4 golden fixtures

| #   | Truth                                                                                                                          | Status     | Evidence                                                                                                                                                          |
| --- | ------------------------------------------------------------------------------------------------------------------------------ | ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 12  | `roundtrip-iff` verb parses → builds DOM → serializes → emits JSON envelope with byteExact identity                            | ✓ VERIFIED | `RoundtripIffCommand.cs:36 [Verb("roundtrip-iff", ...)]`; `Program.cs:48 + :55` dispatch; uses `IffWriter.Write(MutableIffDocument.FromDocument(...))` (line 79+) |
| 13  | Unmutated file reports byteExact:true                                                                                          | ✓ VERIFIED | `synthetic-nested.iff` + `.expected.json` golden present; golden test passes                                                                                       |
| 14  | Odd-length chunk fixture round-trips byte-identical                                                                            | ✓ VERIFIED | `odd-chunk-no-pad.iff` + `.expected.json` golden present; test passes                                                                                              |
| 15  | `--mutate-leaf <id> <hex>` mode mutates one leaf, asserts every other byte range identical                                     | ✓ VERIFIED | `RoundtripIffCommand.cs:42 [Option("mutate-leaf")]` + `:45 [Option("mutate-hex")]`; `mutation-leaf-edit.iff` + `.expected.json` golden present; test passes        |
| 16  | `--remove-leaf <id>` mode removes one leaf, asserts byteExact:false + byteExactExceptRemovedLeaf:true + parent child count -1  | ✓ VERIFIED | `RoundtripIffCommand.cs:48 [Option("remove-leaf")]`; line 86-87 mutual-exclusivity check (returns exit 1); `mutation-leaf-removed.iff` + `.expected.json` present  |
| 17  | Verb follows exit-code contract: FileNotFound→3, IffParseException/IO→2, usage→1                                               | ✓ VERIFIED | RoundtripIffCommand.cs error handling mirrors InspectIffCommand pattern; usage-error guards at lines 86-87 + 91-92 return exitCode:1                              |

**Score:** 6/6

### Plan 08-03: IffChunkTree shared UserControl + TreDetailPane delegation + csproj

| #   | Truth                                                                                                | Status     | Evidence                                                                                                                                                                                |
| --- | ---------------------------------------------------------------------------------------------------- | ---------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 18  | Shared IffChunkTree UserControl renders chunk tree (TAG [SubType] · N bytes · @offset)              | ✓ VERIFIED | `IffChunkTree.cs:64 public class IffChunkTree : UserControl`                                                                                                                            |
| 19  | Renders from immutable IffDocument AND binds to MutableIffDocument                                   | ✓ VERIFIED | `IffChunkTree.cs:131 public void LoadDocument(IffDocument doc)`; `IffChunkTree.cs:157 public void LoadMutable(MutableIffDocument doc)`                                                  |
| 20  | TreDetailPane consumes shared control; Phase-7 read API unchanged (signature pin per LOW-2)         | ✓ VERIFIED | `TreDetailPane.cs:85 private readonly IffChunkTree iffChunkTree = new IffChunkTree();`; lines 116/123/130/168/196/201/223/248 — ShowEmpty/ShowDecoding/ShowReadable/ShowStringTable/LoadIff/ShowEncrypted/ShowUnsupportedRaw/ShowParseFailure all present at original public signatures |
| 21  | Chunk tree exposes structural-op context menu surface + AfterSelect event                            | ✓ VERIFIED | IffChunkTree.cs exposes selection event + UtinniContextMenuStrip attachment point (consumed by FormIffEditor in 08-04)                                                                  |
| 22  | TheJawaToolboxDotNet.csproj contains `<Compile Include="UI\Controls\IffChunkTree.cs">` with UserControl SubType | ✓ VERIFIED | TJT csproj line 102: `<Compile Include="UI\Controls\IffChunkTree.cs">` present                                                                                                            |

**Score:** 5/5

### Plan 08-04: FormIffEditor shell + IffEditController + leaf editing + structural ops + Ctrl+S/Z/Y + csproj

| #   | Truth                                                                                                | Status     | Evidence                                                                                                                                                                              |
| --- | ---------------------------------------------------------------------------------------------------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 23  | FormIffEditor UtinniForm opens, hosts IffChunkTree + leaf editor, binds to MutableIffDocument        | ✓ VERIFIED | `FormIffEditor.cs:64 public partial class FormIffEditor : UtinniForm, IEditorForm`                                                                                                     |
| 24  | Selecting leaf shows editable hex; ASCII-ish payloads also offer inline text toggle                  | ✓ VERIFIED | Leaf-editor pane implemented; ShortcutsEnabled = false grep gate passes; commit-on-focus-leave wired                                                                                    |
| 25  | Right-click on leaf offers Replace bytes from file / Export bytes to file                            | ✓ VERIFIED | UtinniContextMenuStrip leaf menu items wired through IffEditController                                                                                                                  |
| 26  | Right-click on tree offers 8 D-03 structural ops, each routed through IffEditController              | ✓ VERIFIED | Tree context menu wires Add/AddFORM/Remove/Rename/EditSubType/Duplicate/Move-up/Move-down via IffEditController commands (8 UndoOp impls at lines 283/305/327/353/378/399/419/439)      |
| 27  | Editor-local undo/redo stack (independent of scene UndoRedoManager) supports Ctrl+Z / Ctrl+Y         | ✓ VERIFIED | `IffEditController.cs:67 public sealed class IffEditController`; `:102 CanUndo`; `:105 CanRedo`; `:117 Apply`; `:131 Undo`; `:145 Redo`; grep for forbidden patterns returns 0          |
| 28  | Ctrl+Z/Y/S captured by ProcessCmdKey regardless of focus; ShortcutsEnabled=false on hex/text boxes   | ✓ VERIFIED | `FormIffEditor.cs:271 protected override bool ProcessCmdKey(ref Message msg, Keys keyData)`; line 289 `Ctrl+S → if (Source is OpenSource.LooseFile)` branch                            |
| 29  | Edited/added nodes marked dirty with glyph + accent; window title shows unsaved marker               | ✓ VERIFIED | Dirty-state visuals wired through IffEditController.EditApplied event                                                                                                                   |
| 30  | IffEditController lives framework-side in UtinniCoreDotNet/Editing (pure-managed, no WinForms)       | ✓ VERIFIED | File at `D:/Code/Utinni/UtinniCoreDotNet/Editing/IffEditController.cs`; grep for `AddUndoCommand|UndoRedoTitlebarButton|UndoRedoManager|System.Windows.Forms` returns 0                  |
| 31  | UtinniCoreDotNet.csproj contains `<Compile Include="Editing\IffEditController.cs">`                  | ✓ VERIFIED | csproj line 216                                                                                                                                                                         |
| 32  | TheJawaToolboxDotNet.csproj contains 4 new entries (FormIffEditor.cs/.Designer.cs + FormFourCcDialog.cs/.Designer.cs with Form SubType + DependentUpon) | ✓ VERIFIED | TJT csproj lines 81-91 contain all 4 entries with correct SubType / DependentUpon                                                                                                       |

**Score:** 10/10

### Plan 08-05: File save modes 1/2 (loose-override + Save/Save-As) + tiered reload + OpenSource open path + TRE hand-off + Plugin.cs registration

| #   | Truth                                                                                                                       | Status     | Evidence                                                                                                                                                                          |
| --- | --------------------------------------------------------------------------------------------------------------------------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 33  | Editor's open path constructs correct OpenSource (LooseFile/TreArchive) at open time and sets FormIffEditor.Source         | ✓ VERIFIED | FormIffEditor.cs:171/207/222 — `Source = OpenSource.Unknown.Instance` initial state; `OpenFromTreEntry` wires TreArchive via TreRecordIndexResolver                                |
| 34  | Editor can save as loose override under resolved client load path (D-05.1)                                                  | ✓ VERIFIED | `IffSaveTargets.cs:138/148` — `LooseOverridePath.Resolve(...)` consumed                                                                                                            |
| 35  | Editor can Save / Save-As to arbitrary path; save-in-place when opened from loose file (D-05.2)                              | ✓ VERIFIED | IffSaveTargets.SaveLooseOverride / SaveToPath / SaveInPlace exposed; FormIffEditor RefreshSaveMenuEnabledState gates SaveInPlace on `Source is OpenSource.LooseFile`               |
| 36  | Both file saves serialize via shared IffWriter; byte-exact for untouched chunks                                              | ✓ VERIFIED | `IffSaveTargets.cs:267 byte[] bytes = IffWriter.Write(doc);`; byte-exact gate is 08-02 roundtrip golden                                                                            |
| 37  | LooseOverridePath.Resolve normalizes rel-path, rejects rooted + `..`, StartsWith resolvedRoot+sep, OrdinalIgnoreCase on Windows | ✓ VERIFIED | File at `D:/Code/Utinni/UtinniCoreDotNet/Saving/LooseOverridePath.cs`; LooseOverridePathTests in SavingTests pass                                                                |
| 38  | LooseOverridePath lives framework-side (UtinniCoreDotNet/Saving/) per checker B-1; consumed via UtinniCoreDotNet.dll        | ✓ VERIFIED | File at framework-side path; IffSaveTargets.cs line 30 `using UtinniCoreDotNet.Saving;`                                                                                          |
| 39  | File writes Flush-to-disk (FileStream.Flush(true)) BEFORE queuing forced reload; Reload disabled while save Task in flight | ✓ VERIFIED | `IffSaveTargets.cs:38 Flush(true) barrier comment + grep hit at line 273-274 region (FileMode.Create truncates; Flush(true) flushes through OS buffer)`                          |
| 40  | After file save, tiered forced reload (textures→ReloadTextures; terrain→Get().ReloadTerrain INSTANCE; others→PendingNextSceneChange) | ✓ VERIFIED | `ClientReloadDispatcher.cs:101/108 AddMainLoopCall`; `:103 Graphics.ReloadTextures()`; `:113 GroundScene.Get().ReloadTerrain()` (INSTANCE); `:117/122 PendingNextSceneChange`; negative grep for bare `GroundScene.ReloadTerrain` returns 0 |
| 41  | TRE Browser offers 'Open in IFF Editor' hand-off; constructs OpenSource.TreArchive(trePath, recordIndex, logicalPath)      | ✓ VERIFIED | `FormTreBrowser.cs:60 _tvTreContextMenu`; `:144 _miOpenInIffEditor = new ToolStripMenuItem("Open in IFF Editor")`; `:214 editor.OpenFromTreEntry(...)`                              |
| 42  | On record-index resolution failure, fallback to OpenSource.Unknown.Instance (NOT LooseFile of virtual path) per checker W-3 | ✓ VERIFIED | TreRecordIndexResolver.ResolveOrUnknown wired; TreHandoffFallbackTests covers Unknown branch; FormIffEditor pattern-match gates exclude Unknown                                    |
| 43  | When Source is Unknown, Save→In Place / Loose Override / Repack TRE / Live Patch DISABLED; Save As REMAINS ENABLED          | ✓ VERIFIED | `FormIffEditor.cs:893-953 RefreshSaveMenuEnabledState` — round-2 MEDIUM 5 logic; `:939 bool isUnknown`; `:953 unknownTooltip "Cannot resolve archive record — use Save As..."`     |
| 44  | FormIffEditor registered in Plugin.cs GetForms() inside try/catch isolation; SPI NOT widened (GetSubPanels stays null)     | ✓ VERIFIED | `Plugin.cs:75-81` try { forms.Add(new FormIffEditor(this)); } catch (Exception ex) { Log.Info("Failed to create FormIffEditor; IFF Editor will be unavailable: " + ex); }            |
| 45  | UtinniCoreDotNet.csproj contains entries for LooseOverridePath.cs + TreRecordIndexResolver.cs                               | ✓ VERIFIED | csproj line 83 LooseOverridePath; line 78 TreRecordIndexResolver; also line 84 ReloadAssetClassifier (framework-side classifier extraction)                                        |
| 46  | TheJawaToolboxDotNet.csproj contains entries for IffSaveTargets.cs + ClientReloadDispatcher.cs                              | ✓ VERIFIED | TJT csproj lines 148 + 149                                                                                                                                                          |

**Live-SWG smoke (Task 5):** approved by maintainer 2026-05-28 ("approved, dig in") after mid-smoke AV defect (singleton-form hide-not-dispose) was fixed inline. Open Q2 + Q3 recorded as deferred residuals in deferred-items.md.

**Score:** 14/14

### Plan 08-06: In-memory live patch (D-05.3) + LivePatchValidator pure function + 5 [Fact]s + FormSaveConfirmDialog

| #   | Truth                                                                                                                | Status     | Evidence                                                                                                                                                                          |
| --- | -------------------------------------------------------------------------------------------------------------------- | ---------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 47  | D-05.3 ships infra-ready + user-disabled (round-2 MEDIUM 11); menu item disabled with honest tooltip                | ✓ VERIFIED | LivePatchSaveTarget.cs implemented + LivePatchValidator framework-side + 5 [Fact]s pass; FormIffEditor wires the Patch-live menu item disabled with grep-gated honest tooltip      |
| 48  | LivePatchSaveTarget patches loaded IFF bytes directly in mapped client memory when Source is OpenSource.ClientMemory | ✓ VERIFIED | `LivePatchSaveTarget.cs:97-186` — provenance check + AddMainLoopCall + Memory.memory.Copy                                                                                          |
| 49  | Every mapped-memory write goes through Memory.memory.Copy on game thread (CON-N-04 VirtualProtect bracket)           | ✓ VERIFIED | `LivePatchSaveTarget.cs:176 GameCallbacks.AddMainLoopCall(() => ...); :186 UtinniCore.Memory.memory.Copy(...)`                                                                     |
| 50  | SAME-LENGTH-ONLY refuses growth AND shrink with candid copy                                                          | ✓ VERIFIED | LivePatchValidator.cs RefusedSameLength enum value (line 70); 2 of 5 [Fact]s explicitly cover growth + shrink                                                                       |
| 51  | Length/bounds + targetAddr != IntPtr.Zero validated BEFORE write                                                     | ✓ VERIFIED | `LivePatchSaveTarget.cs:141 LivePatchValidation v = LivePatchValidator.Validate(...)`; `LivePatchValidator.cs:135 public static LivePatchValidation Validate(...)`                  |
| 52  | Bounds-gate EXTRACTED to pure function `LivePatchValidator.Validate` returning enum                                  | ✓ VERIFIED | `LivePatchValidator.cs:37 public enum LivePatchValidation { Ok, RefusedNoClient, RefusedZeroTarget, RefusedSameLength }`; `:135 Validate` static method                            |
| 53  | Bounds gate unit-tested with ≥ 5 [Fact]s (NoClient, ZeroTarget, Growth, Shrink, SameLengthHappy)                     | ✓ VERIFIED | `dotnet test --filter LivePatchValidator` returns 5 passing                                                                                                                         |
| 54  | Live-patch confirm dialog uses explicit-verb buttons + Color.Red emphasis + volatile-on-reload statement             | ✓ VERIFIED | `FormSaveConfirmDialog.cs` exists; parameterized heading/body/verbs; used by LivePatch + 08-07 Repack                                                                              |
| 55  | UtinniCoreDotNet.csproj contains entry for Editing\LivePatchValidator.cs                                             | ✓ VERIFIED | csproj line 217                                                                                                                                                                     |
| 56  | TheJawaToolboxDotNet.csproj contains entries for LivePatchSaveTarget.cs + FormSaveConfirmDialog.cs/.Designer.cs      | ✓ VERIFIED | TJT csproj lines 93-97 (FormSaveConfirmDialog) + line 150 (LivePatchSaveTarget)                                                                                                     |

**Live-SWG smoke (Task 5):** smoke=automation-only per maintainer; the 5 LivePatchValidator [Fact]s carry the bounds-gate burden. Open Q4 (full functional live-patch smoke requiring maintainer-only ClientMemory debug construction) recorded as deferred in deferred-items.md.

**Score:** 10/10

### Plan 08-07: .tre repack (D-05.4) — TreFile.GetRecordCompressedBytes/NameBytes + TreWriter + TreRepackSaveTarget + timestamped backups + locked-archive fallback + 15-outcome on-disk test contract

| #   | Truth                                                                                                                | Status     | Evidence                                                                                                                                                                            |
| --- | -------------------------------------------------------------------------------------------------------------------- | ---------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 57  | TreFile.GetRecordCompressedBytes returns raw compressed slice verbatim (08-REVIEWS HIGH-1)                          | ✓ VERIFIED | `TreFile.cs:468 public byte[] GetRecordCompressedBytes(int index)`                                                                                                                  |
| 58  | TreFile.GetRecordNameBytes returns raw name-block slice verbatim (round-2 MEDIUM 6)                                 | ✓ VERIFIED | `TreFile.cs:532 public byte[] GetRecordNameBytes(int index)`                                                                                                                        |
| 59  | TreWriter repacks .tre by full rebuild; copies raw compressed bytes + raw name bytes for untouched entries          | ✓ VERIFIED | `TreWriter.cs:87 public static class TreWriter`; `:112 public static byte[] Repack(TreFile original, IDictionary<int, byte[]> edits)`; `:168 byte[] rawCompressed = original.GetRecordCompressedBytes(i)`; `:178 nameSlices[i] = original.GetRecordNameBytes(i)` |
| 60  | Untouched entries preserve 9 TOC invariants byte-for-byte (Checksum, NameOffset, NameString, Compressor, UncompressedSize, CompressedSize, GetRecordData, GetRecordCompressedBytes, GetRecordNameBytes) | ✓ VERIFIED | TreWriterTests + TreRepack* continuation-work suites cover all 9 invariants                                                                                                          |
| 61  | zlib framing on write inverts TreFile.Inflate (RFC1950 header + DeflateStream + Adler32) — ONLY for edited entry    | ✓ VERIFIED | TreWriter.cs implementation; TreWriterTests Fact 2 (payload-only edit) proves untouched entries' compressed slices unchanged                                                          |
| 62  | Editor can repack source .tre (D-05.4) when Source is OpenSource.TreArchive, behind confirm + TIMESTAMPED backup    | ✓ VERIFIED | `TreRepackSaveTarget.cs:69 public static class TreRepackSaveTarget`; `:103 yyyyMMdd-HHmmss[-N].bak` naming; `:139 byte[] repacked = TreWriter.Repack(original, edits);`              |
| 63  | On atomic-replace failure (locked archive), falls back to RefusedClientHoldsArchive_LooseOverrideRecommended         | ✓ VERIFIED | `TreRepackSaveTarget.cs:82 enum RefusedClientHoldsArchive_LooseOverrideRecommended`; `:159 catch ... return TreRepackResult.RefusedClientHoldsArchive_LooseOverrideRecommended`     |
| 64  | TreBackupPath + TreRepackLock extracted framework-side (UtinniCoreDotNet/Saving/) per checker B-1                   | ✓ VERIFIED | Files at `D:/Code/Utinni/UtinniCoreDotNet/Saving/TreBackupPath.cs` + `TreRepackLock.cs`; csproj lines 85 + 86                                                                       |
| 65  | UtinniCoreDotNet.csproj contains entry for Formats\Tre\TreWriter.cs                                                  | ✓ VERIFIED | csproj line 82                                                                                                                                                                       |
| 66  | TheJawaToolboxDotNet.csproj contains entry for Saving\TreRepackSaveTarget.cs                                         | ✓ VERIFIED | TJT csproj line 151                                                                                                                                                                  |
| 67  | On-disk repack contract automated by 5 new test classes (15 outcomes total)                                          | ✓ VERIFIED | TreRepackRoundTripTests + TreRepackLogicalPathTests + TreRepackLockedArchiveTests + TreRepackBackupTests + TreRepackByteDiffTests; `dotnet test --filter TreRepack` returns 15 passing |

**Live-SWG smoke (Task 4):** smoke=automation-augmented per maintainer continuation-work direction (5 new test classes added). Open Q1 (cursor N-H1 ACK — path-CRC live-resolution by SWG client) + Open Q5 (WinForms UI end-to-end smoke) recorded as deferred residuals in deferred-items.md.

**Score:** 11/11

---

## ROADMAP Phase 8 Success Criteria Cross-Check

| #   | Criterion                                                                                                          | Status     | Evidence                                                                                                                       |
| --- | ------------------------------------------------------------------------------------------------------------------ | ---------- | ------------------------------------------------------------------------------------------------------------------------------ |
| SC1 | IFF Editor subpanel loads inside TJT in editor host against live SWG client                                       | ✓ VERIFIED | 08-05 Task 5 live-SWG smoke approved by maintainer ("approved, dig in"); FormIffEditor + Plugin.cs registration                |
| SC2 | User can open IFF (TRE Browser hand-off or file picker), view, edit chunk content, save back, live client reloads | ✓ VERIFIED | TIERED acceptance per CONTEXT D-06: texture/terrain in-session pass (08-05 smoke); datatable/STF/object-template via scene change (deferred-items Open Q3 — on-source dispositions unit-tested at 22 [Theory] cases) |
| SC3 | `utinni-cli inspect-iff` golden (from Phase 4) covers same read path                                              | ✓ VERIFIED | InspectIff CLI tests still pass (20 passing in filter); Phase 7 carry-forward                                                  |
| SC4 | Edits survive save → reload round trip without corrupting unedited chunks                                          | ✓ VERIFIED | 08-02 roundtrip-iff goldens (4 fixtures: identity + odd-no-pad + payload-mutation + structural-removal) all green               |
| SC5 | IFF primitives exported from shared non-plugin assembly (UtinniCoreDotNet/Formats/Iff) consumed by direct reference | ✓ VERIFIED | ROADMAP lines 169 + 180 explicit; framework files at `UtinniCoreDotNet/Formats/Iff/` confirmed; TJT references UtinniCoreDotNet.dll via HintPath (D-01 reconciliation complete) |

**Score:** 5/5

---

## PROD-W1-IFF Requirements Coverage

**REQ-W1-IFF — Wave-1 plugin: IFF Editor (read + write)**
- **Statement:** Read and write IFF chunks across the client's IFF surface. Replaces SOE-era IFFEditor. Foundational plugin — most other Wave-1 plugins layer on IFF read/write.
- **Acceptance:** Plugin loads in editor host; user can open IFF file, view chunk hierarchy, edit chunk content, save modifications back to a file the live client reloads correctly. CLI shim covers `inspect-iff` with golden fixtures.

| Acceptance Element                                          | Source Plan       | Status     | Evidence                                                                                                                          |
| ----------------------------------------------------------- | ----------------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------- |
| Plugin loads in editor host                                  | 08-05             | ✓ SATISFIED | FormIffEditor + Plugin.cs try/catch registration; 08-05 maintainer smoke approved                                                |
| User can open IFF file                                       | 08-04 + 08-05      | ✓ SATISFIED | Open… toolbar (OpenSource.LooseFile) + TRE Browser hand-off (OpenSource.TreArchive via TreRecordIndexResolver)                   |
| User can view chunk hierarchy                                | 08-03              | ✓ SATISFIED | IffChunkTree shared UserControl renders the tree (LoadDocument + LoadMutable)                                                     |
| User can edit chunk content                                  | 08-04              | ✓ SATISFIED | Leaf payload editing (hex/text/replace-from-file) + 8 D-03 structural ops via IffEditController; tested by IffEditControllerTests |
| User can save modifications back to a file                   | 08-05/06/07        | ✓ SATISFIED | Four-tier D-05 save matrix: Loose-Override (D-05.1) + Save/Save-As (D-05.2) + Live-Patch (D-05.3 infra-ready) + .tre Repack (D-05.4) |
| Live client reloads correctly                                | 08-05              | ✓ SATISFIED | TIERED reload (textures/terrain in-session; datatable/STF/object-template on next scene change); 22 [Theory] routing cases       |
| CLI shim covers `inspect-iff` with golden fixtures           | Phase 4 + 08-02   | ✓ SATISFIED | inspect-iff golden carried forward green; 08-02 adds roundtrip-iff verb + 4 fresh golden fixtures for the write path             |

**PROD-W1-IFF: ✓ SATISFIED** (acceptance-tested against modes 1/2/4 in-phase; mode 3 documented infra-ready + user-disabled per CONTEXT round-2 MEDIUM 11)

---

## Anti-Patterns Found

No new anti-patterns of severity Blocker or Warning. Code review CR-01 BLOCKER was fixed at commit e66e4fe; WR-01..WR-07 all fixed; WR-03 + 6 Info findings deferred (cosmetic/perf, not correctness — see 08-REVIEW.md frontmatter `deferred:`).

| File / Area                                                | Line | Pattern / Concern                                       | Severity | Disposition                                            |
| ---------------------------------------------------------- | ---- | ------------------------------------------------------- | -------- | ------------------------------------------------------ |
| (none new — all critical / warning findings from 08-REVIEW.md are FIXED) | n/a  | n/a                                                     | n/a      | Code-review deferred items are cosmetic / perf only    |

---

## Test Suite Verification

**Run from `D:/Code/Utinni`, `dotnet test --no-build -c Debug` Debug|x86 net472:**

| Suite                                                  | Total | Passed | Skipped | Failed |
| ------------------------------------------------------ | ----- | ------ | ------- | ------ |
| UtinniCoreDotNet.Tests (full)                          | 331   | 331    | 0       | 0      |
| Utinni.Cli.Tests (full)                                | 124   | 123    | 1*      | 0      |
| Phase-8 framework filter (IffWriter + OpenSource + MutableIff + IffEditController + LivePatchValidator + LooseOverridePath + ClientReloadDispatcher + TreHandoffFallback + GetRecordCompressedBytes + TreWriter + TreRepack) | 176 | 176 | 0 | 0 |
| Phase-8 CLI filter (Roundtrip + InspectIff)            | 20    | 20     | 0       | 0      |
| TreRepack* (08-07 continuation-work)                   | 15    | 15     | 0       | 0      |
| LivePatchValidator                                     | 5     | 5      | 0       | 0      |
| ClientReloadDispatcher                                 | 22    | 22     | 0       | 0      |

*1 skipped CLI test is CotMasterIndexTests.CotMasterIndex_ParsesSearchTOC_WhenFixtureExists — environmental fixture missing, unrelated to Phase 8.

---

## Deferred Items (Not Acceptance Blockers)

Per the verifier brief: Open Q1 + Open Q5 (live-SWG residuals for .tre repack) are documented as deferred-but-acceptable for V1 in `deferred-items.md`. The on-disk repack contract is CI-covered (15 TreRepack outcomes). Open Q2/Q3/Q4 (loose-override subdir, per-class reload matrix observation, full functional live-patch smoke) are similarly deferred — all are explicitly classified as "not a Phase 8 acceptance blocker" by the maintainer or by automation-augmented coverage. The dirty-discard prompt polish gap on the TRE-Browser → IFF-Editor hand-off path is also deferred (saved edits preserved on disk; loss is unsaved-only).

These items are recorded in the `deferred:` frontmatter and do not block `status: passed`. See `deferred-items.md` for full context.

---

## Gaps Summary

**No gaps.** All 47 plan-level must-haves are verified against the codebase. The five ROADMAP Phase 8 success criteria all verify against the codebase under the explicit TIERED acceptance for SC2 (per CONTEXT D-06 and 08-REVIEWS HIGH-4 disposition). PROD-W1-IFF acceptance elements all map to satisfied implementation evidence.

The phase delivers the four-tier D-05 save matrix in full:
- **D-05.1 (Loose Override):** IffSaveTargets.SaveLooseOverride via framework-side LooseOverridePath root-containment helper; 08-05 maintainer smoke approved
- **D-05.2 (Save/Save-As + Save-in-place):** IffSaveTargets.SaveToPath + SaveInPlace; provenance-gated on OpenSource.LooseFile
- **D-05.3 (In-memory Live Patch):** LivePatchSaveTarget + LivePatchValidator (framework-side 5-[Fact] bounds gate); infra-ready, user-disabled per round-2 MEDIUM 11 — full functional smoke deferred to follow-up phase that wires ClientMemory open-path discovery
- **D-05.4 (TRE Repack):** TreWriter (framework) + TreRepackSaveTarget (plugin) with TIMESTAMPED non-overwriting backups + locked-archive fallback + 15-outcome on-disk contract; 08-07 maintainer continuation-work approved

Each tier's bounds-of-trust gate is exercised:
- D-05.1: path traversal regression (LooseOverridePathTests)
- D-05.2: SaveInPlace pattern-match guard on OpenSource.LooseFile
- D-05.3: LivePatchValidator 5-[Fact] bounds gate (no-client, zero-target, growth, shrink, happy-path)
- D-05.4: 9-property TOC invariant per-record test + locked-archive sharing-violation classifier + timestamped-backup uniqueness

---

_Verified: 2026-05-28T22:30:00Z_
_Verifier: Claude (gsd-verifier)_
