---
phase: 08-tjt-subpanel-iff-editor-read-write
plan: 07
subsystem: tjt-iff-editor-tre-repack
tags: [tjt, ui, winforms, iff, tre, repack, save-modes, byte-identity, locked-archive-fallback, timestamped-backup, cross-repo, automation-augmented, deferred-live-residual]
requires:
  - "UtinniCoreDotNet/Formats/Tre/TreFile (existing Phase 4/7, extended with GetRecordCompressedBytes + GetRecordNameBytes in Task 1)"
  - "UtinniCoreDotNet/Formats/Iff/{IffWriter, MutableIffDocument, OpenSource} (08-01)"
  - "UtinniCoreDotNet/Saving/LooseOverridePath + ReloadAssetClassifier (08-05)"
  - "UtinniCoreDotNet/Saving/LivePatchValidator (08-06)"
  - "TheJawaToolboxDotNet/Saving/{IffSaveTargets, ClientReloadDispatcher} (08-05)"
  - "TheJawaToolboxDotNet/Saving/LivePatchSaveTarget + UI/Forms/FormSaveConfirmDialog (08-06)"
  - "TheJawaToolboxDotNet/UI/Forms/FormIffEditor (08-04, extended via 08-05/06)"
  - "UtinniCoreDotNet.Tests linked-source TreFixtureBuilder + local TreFileFixtures helpers"
provides:
  - "UtinniCoreDotNet/Formats/Tre/TreFile.GetRecordCompressedBytes(int) — verbatim raw-slice API (08-REVIEWS HIGH-1)"
  - "UtinniCoreDotNet/Formats/Tre/TreFile.GetRecordNameBytes(int) — verbatim raw-name-bytes API (round-2 MEDIUM 6)"
  - "UtinniCoreDotNet/Formats/Tre/TreWriter — full .tre repack copying raw compressed + raw name bytes for untouched entries"
  - "UtinniCoreDotNet/Saving/TreBackupPath — pure-managed BCL-only timestamped-backup-path helper (continuation-work framework extraction; 08-REVIEWS MEDIUM-10)"
  - "UtinniCoreDotNet/Saving/TreRepackLock — pure-managed BCL-only locked-archive probe (continuation-work framework extraction; 08-REVIEWS MEDIUM-10)"
  - "TheJawaToolboxDotNet/Saving/TreRepackSaveTarget — D-05.4 save target consuming TreBackupPath + TreRepackLock from the framework"
  - "FormIffEditor Save▾ ▸ Repack into source .tre… menu wire (provenance-gated on Source is OpenSource.TreArchive)"
  - "Five new test classes covering the on-disk repack contract (15 [Fact] + [Theory] outcomes): TreRepackRoundTripTests + TreRepackLogicalPathTests + TreRepackLockedArchiveTests + TreRepackBackupTests + TreRepackByteDiffTests"
  - "TreFileFixtures.BuildValidV0005FiveRecord() — new 5-record synthetic fixture mixing compressed/uncompressed entries for the new repack contract tests"
affects:
  - "UtinniCoreDotNet/UtinniCoreDotNet.csproj — added 3 <Compile Include> entries (TreWriter.cs Task 2, TreBackupPath.cs + TreRepackLock.cs continuation-work refactor) (round-2 HIGH-A)"
  - "UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj — added 1 linked-source entry (TreFixtureBuilder.cs) in Task 2"
  - "TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj — added 1 <Compile Include> entry (TreRepackSaveTarget.cs in Task 3) (round-2 HIGH-A)"
  - "TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs — wired Save▾ ▸ Repack into source .tre… (Task 3)"
  - "TheJawaToolboxDotNet/Saving/TreRepackSaveTarget.cs — refactored to delegate to framework TreBackupPath + TreRepackLock (continuation-work refactor)"
tech-stack:
  added: []
  patterns:
    - "Framework-side placement for pure-managed BCL-only helpers (checker B-1, mirrors 08-05 LooseOverridePath + 08-06 LivePatchValidator): TreBackupPath + TreRepackLock both ship in UtinniCoreDotNet so CI's Utinni-only checkout builds + tests them without a UtinniPlugins linked-source path."
    - "Automation-augmented smoke (continuation-work pattern in response to maintainer's 'automate this against test assets' request): on-disk repack contract automated against a synthetic 5-record fixture; live-client residual (cursor N-H1 ACK + UI end-to-end) shrunk to ONLY what genuinely requires SWG to be running."
    - "Two-guarantee + name-byte-identity contract (08-REVIEWS HIGH-1 + round-2 MEDIUM 6) — supersedes the misleading 'full-file byte-identical' claim: (a) logical payload identity via GetRecordData; (b) raw compressed slice identity via GetRecordCompressedBytes; (c) raw name bytes identity via GetRecordNameBytes."
    - "Timestamped backup uniqueness (08-REVIEWS MEDIUM-10): <name>.tre.<yyyyMMdd-HHmmss>.bak with -N sequence-suffix disambiguation on same-second collision. NEVER overwrites a prior known-good backup."
    - "Locked-archive probe + refusal: TreRepackLock.Probe returns SharingViolation when FileShare.None blocked; save target maps this to RefusedClientHoldsArchive_LooseOverrideRecommended with NO partial-write."
key-files:
  created:
    - "UtinniCoreDotNet/Formats/Tre/TreWriter.cs (Task 2)"
    - "UtinniCoreDotNet/Saving/TreBackupPath.cs (continuation-work refactor)"
    - "UtinniCoreDotNet/Saving/TreRepackLock.cs (continuation-work refactor)"
    - "UtinniCoreDotNet.Tests/FormatsTests/Tre/TreFileGetRecordCompressedBytesTests.cs (Task 1)"
    - "UtinniCoreDotNet.Tests/FormatsTests/Tre/TreWriterTests.cs (Task 2)"
    - "UtinniCoreDotNet.Tests/FormatsTests/Tre/TreRepackRoundTripTests.cs (continuation-work, 2 [Fact]s)"
    - "UtinniCoreDotNet.Tests/FormatsTests/Tre/TreRepackLogicalPathTests.cs (continuation-work, 1 [Fact])"
    - "UtinniCoreDotNet.Tests/FormatsTests/Tre/TreRepackLockedArchiveTests.cs (continuation-work, 4 [Fact]s)"
    - "UtinniCoreDotNet.Tests/FormatsTests/Tre/TreRepackBackupTests.cs (continuation-work, 3 [Fact]s)"
    - "UtinniCoreDotNet.Tests/FormatsTests/Tre/TreRepackByteDiffTests.cs (continuation-work, 1 [Fact] + 1 [Theory]×4 = 5 outcomes)"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/TreRepackSaveTarget.cs (Task 3, refactored continuation-work)"
  modified:
    - "UtinniCoreDotNet/Formats/Tre/TreFile.cs (Task 1 — added GetRecordCompressedBytes + GetRecordNameBytes)"
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj (3 new Compile entries: TreWriter.cs + TreBackupPath.cs + TreRepackLock.cs — round-2 HIGH-A)"
    - "UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj (1 new linked-source entry for TreFixtureBuilder.cs from Utinni.Cli.Tests)"
    - "UtinniCoreDotNet.Tests/FormatsTests/Tre/TreFileFixtures.cs (added BuildValidV0005FiveRecord helper)"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj (1 new Compile entry: TreRepackSaveTarget.cs — round-2 HIGH-A)"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs (Task 3 — wired Save▾ ▸ Repack into source .tre…)"
decisions:
  - "Task 4 outcome (continuation-work): Automation-augmented per maintainer direction. 5 new test classes cover the on-disk repack contract (untouched-record byte-identity, edit→repack→reopen path-CRC self-consistency, locked-archive refusal + original byte-unchanged, timestamped backup uniqueness, overall bytes-differ + untouched-slice-identical). Live-client residual (cursor N-H1 ACK = path-CRC resolution by SWG client + tiered reload after scene change + WinForms UI end-to-end) genuinely cannot be automated this round and is deferred to Open Q1 + Open Q5 in deferred-items.md."
  - "Extracted TreBackupPath + TreRepackLock to UtinniCoreDotNet/Saving/ (continuation-work refactor): the timestamped-backup-naming logic and the sharing-violation HResult detector are now framework-side so the 08-REVIEWS MEDIUM-10 contracts are CI-coverable. Mirrors the 08-05 (LooseOverridePath + ReloadAssetClassifier + TreRecordIndexResolver) and 08-06 (LivePatchValidator) checker-B-1 framework-side-placement pattern. The plugin's TreRepackSaveTarget now delegates to these helpers; behavior under live SWG is unchanged from the pre-refactor implementation that the prior Task 3 shipped."
  - "Round-2 MEDIUM 6 disposition (codex unique): the writer copies raw name bytes verbatim from the source via TreFile.GetRecordNameBytes(i), AND the per-record TOC invariant test asserts dst.GetRecordNameBytes(i).SequenceEqual(src.GetRecordNameBytes(i)) for every untouched record. The name-byte-identity guarantee is what makes 'untouched entry preservation' rigorous at the byte layout level, not just the logical-name level."
  - "08-REVIEWS HIGH-1 disposition: the misleading 'archive bytes identical' full-file claim is dropped. Replaced by the two-guarantee contract: (a) GetRecordData identity for untouched entries; (b) GetRecordCompressedBytes identity for untouched entries. Both are asserted in TreWriterTests.cs (9-property TOC invariant helper) AND the continuation-work test classes."
  - "08-REVIEWS HIGH-2 disposition (provenance gate): FormIffEditor's Save▾ ▸ Repack item is enabled ONLY when `Source is OpenSource.TreArchive ta`. Other OpenSource cases (LooseFile, ClientMemory, Unknown) disable Repack with the round-2 MEDIUM 5 wording 'Cannot resolve archive record — use Save As to write to a chosen file.'"
  - "08-REVIEWS MEDIUM-10 disposition (timestamped backup + locked-archive fallback): TreBackupPath.NextAvailable produces the timestamped path with -N sequence-suffix disambiguation on same-second collision; TreRepackLock.Probe + the save target's catch-when filters return RefusedClientHoldsArchive_LooseOverrideRecommended on sharing violation. Both contracts are CI-coverable framework-side."
metrics:
  duration_minutes: 360
  completed_date: "2026-05-28"
---

# Phase 8 Plan 7: TRE Repack (D-05.4) Summary

One-liner: Ships the highest-risk file save mode — full `.tre` archive repack — behind a provenance-gated `Save ▾ ▸ Repack into source .tre…` menu with timestamped backup + locked-archive fallback; the on-disk repack contract (untouched-record byte-identity at compressed-slice AND name-block layers, logical-path + path-CRC round-trip, backup uniqueness, locked-archive refusal) is CI-covered by 5 new test classes (15 outcomes) against a synthetic 5-record fixture per the maintainer's continuation-work direction; live-SWG residual deferred to Open Q1 + Open Q5.

## What Shipped

**Framework (Utinni repo) — 3 new production files + 1 modification + 5 new test classes + 1 fixture extension:**

- **`UtinniCoreDotNet/Formats/Tre/TreFile.cs`** (Task 1, modified) — added `public byte[] GetRecordCompressedBytes(int index)` (08-REVIEWS HIGH-1) and `public byte[] GetRecordNameBytes(int index)` (round-2 MEDIUM 6). Both APIs mirror `GetRecordData`'s lazy / stream-backed / bounds / fresh-copy / Truncated contracts. Verbatim raw slices — no decompression, no validation — for TreWriter's untouched-entry copy path. 13 [Fact]s in `TreFileGetRecordCompressedBytesTests.cs` cover bounds, stream-backed rejection, equality to file bytes, zero-length, fresh-copy, truncated.

- **`UtinniCoreDotNet/Formats/Tre/TreWriter.cs`** (Task 2, new) — full `.tre` rebuild copying raw compressed bytes via `GetRecordCompressedBytes` AND raw name bytes via `GetRecordNameBytes` for untouched entries. Only the edited entry recompresses (raw `deflate`, compressor=1 as V1 default). Layout: `[36-byte header][payload region][TOC][name block]`; all little-endian; header fields written last once block sizes are known. Supports both size-first (V0004/V0005/V0006, 24-byte stride) and crc-first (V5000/V6000, 24/32-byte stride). 3 [Fact]s in `TreWriterTests.cs` exercise edit-free repack + payload-only edit + the explicit 9-property TOC invariant per-record table (Checksum, NameOffset's name bytes, NameString, Compressor, UncompressedSize, CompressedSize, GetRecordData, GetRecordCompressedBytes, GetRecordNameBytes).

- **`UtinniCoreDotNet/Saving/TreBackupPath.cs`** (continuation-work, new) — pure-managed BCL-only timestamped-backup-path resolver. `NextAvailable(string treFilePath, DateTime now)` returns `<treFilePath>.<yyyyMMdd-HHmmss>.bak` on first try; appends `-N` disambiguator (1..999) on existing-file collision; GUID fallback at the bound. DateTime is an explicit parameter so production passes `DateTime.UtcNow` and tests pin a known instant. 08-REVIEWS MEDIUM-10 "never overwrite a prior backup" contract is now framework-side.

- **`UtinniCoreDotNet/Saving/TreRepackLock.cs`** (continuation-work, new) — pure-managed BCL-only locked-archive probe. `Probe(string treFilePath)` → `ProbeResult { Available, SharingViolation, OtherIoError }`. Opens with `FileShare.None` to detect whether the live client holds the archive; closes the stream before returning (probe, not held lock). Also exposes `IsSharingViolation(IOException)` as the canonical HResult-based detector (`ERROR_SHARING_VIOLATION 0x80070020` + `ERROR_LOCK_VIOLATION 0x80070021`) that `TreRepackSaveTarget` calls via a 1-line shim. 08-REVIEWS MEDIUM-10 locked-archive fallback contract is now framework-side.

- **`UtinniCoreDotNet/UtinniCoreDotNet.csproj`** — added THREE new `<Compile Include>` entries (round-2 HIGH-A): `Formats\Tre\TreWriter.cs` (Task 2), `Saving\TreBackupPath.cs` + `Saving\TreRepackLock.cs` (continuation-work refactor).

- **`UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj`** — added 1 linked-source `<Compile Include>` for `..\Utinni.Cli.Tests\Infrastructure\TreFixtureBuilder.cs` (Task 2 PART C; mirrors existing `SlnDirResolver`/`HeaderDiscovery`/`Props` linked-source pattern at lines 44-55).

- **`UtinniCoreDotNet.Tests/FormatsTests/Tre/TreFileFixtures.cs`** (continuation-work, modified) — added `BuildValidV0005FiveRecord()` mixing compressed records (1, 3) with uncompressed records (0, 2, 4) so the new repack contract tests can edit index 2 and assert byte-identity for all four untouched records on both compressor sides. Logical paths use a `datatable/foo/` prefix for the path-CRC self-consistency test.

- **5 new test classes covering the on-disk repack contract (15 outcomes total):**
  - `UtinniCoreDotNet.Tests/FormatsTests/Tre/TreRepackRoundTripTests.cs` (2 [Fact]s): same-length + different-length payload swaps; untouched records 0, 1, 3, 4 byte-identical at compressed slice + name slice; edited record 2 round-trips to new payload.
  - `UtinniCoreDotNet.Tests/FormatsTests/Tre/TreRepackLogicalPathTests.cs` (1 [Fact]): after edit→repack→reopen, `Records[2].Name` is preserved AND `Records[2].Checksum` (stored path CRC) is preserved AND `GetRecordData(2)` returns the new payload bytes. **The TreFile-side path-CRC self-consistency check — NOT the SWG-client ACK.** XML doc documents the distinction.
  - `UtinniCoreDotNet.Tests/FormatsTests/Tre/TreRepackLockedArchiveTests.cs` (4 [Fact]s): probe returns SharingViolation on locked file; SHA-256 + length of the original file is byte-unchanged after the failed attempt; probe returns Available on unlocked file; `IsSharingViolation` correctly classifies the two canonical HResults + rejects unrelated I/O HResults + null.
  - `UtinniCoreDotNet.Tests/FormatsTests/Tre/TreRepackBackupTests.cs` (3 [Fact]s): two distinct timestamps → two distinct paths; same-second collision → `-N` disambiguator (1, 2, 3, …); existing backup at proposed timestamp is detected AND its bytes (length + SHA-256 + content) are byte-unchanged.
  - `UtinniCoreDotNet.Tests/FormatsTests/Tre/TreRepackByteDiffTests.cs` (1 [Fact] + 1 [Theory]×4 = 5 outcomes): post-edit, the repacked archive's bytes differ from the original; for each untouched index (parameterized 0, 1, 3, 4) `GetRecordCompressedBytes` on the repacked archive equals the same call on the original.

**Plugin (UtinniPlugins repo) — 1 new file + 2 modifications + 1 csproj entry:**

- **`Saving/TreRepackSaveTarget.cs`** (Task 3, new; continuation-work refactor) — D-05.4 save target. Public surface: `public static async Task<TreRepackResult> Apply(OpenSource.TreArchive target, byte[] rewrittenIffBytes, bool createBackup)` returning `TreRepackResult { Replaced, BackedUpThenReplaced, RefusedClientHoldsArchive_LooseOverrideRecommended, Failed }`. Steps on a background Task:
  1. Open via `TreFile.Open(target.TrePath)`; build edits `{ target.RecordIndex: rewrittenIffBytes }`; call `TreWriter.Repack(original, edits)`.
  2. Temp-write to `<trePath>.tmp-<guid>` on the same volume (`FileMode.Create`, `FileAccess.Write`, `FileShare.None`, `Flush(true)`).
  3. Round-trip sanity gate: `TreFile.Open(tempPath)` to verify the bytes parse back; deletes temp + returns `Failed` on any parse error.
  4. Optional timestamped backup via `TreBackupPath.NextAvailable(trePath, DateTime.UtcNow)` → `File.Copy(trePath, backupPath, overwrite: false)`. On sharing violation: delete temp, return `RefusedClientHoldsArchive_LooseOverrideRecommended`.
  5. Atomic replace via `File.Replace(tempPath, trePath, destinationBackupFileName: null, ignoreMetadataErrors: true)`. On sharing violation: delete temp + (if created) backup, return `RefusedClientHoldsArchive_LooseOverrideRecommended`.

  All sharing-violation catches use the framework-side `TreRepackLock.IsSharingViolation(IOException)` detector (matched via a 1-line shim so the C# catch-when filter stays readable).

- **`UI/Forms/FormIffEditor.cs`** (Task 3, modified) — wired `Save ▾ ▸ Repack into source .tre…` menu item. Provenance-gated on `Source is OpenSource.TreArchive ta`. On click: show `FormSaveConfirmDialog` with heading `Repack <archive>.tre?`, body explaining the full-rebuild + atomic-replace path, opt-in `Create a timestamped backup (<archive>.<yyyyMMdd-HHmmss>.bak) first` checkbox DEFAULTED ON; on Accept, serialize via `IffWriter.Write(document)` then `await TreRepackSaveTarget.Apply(ta, rewritten, backupRequested)`. Result translation:
  - `Replaced` / `BackedUpThenReplaced` → success status with `lastSavedPath = ta.TrePath` so the Reload button knows the asset to classify.
  - `RefusedClientHoldsArchive_LooseOverrideRecommended` → status with the candid copy AND pre-select the `Save as loose override` Save▾ item (do NOT auto-save — user must consent).
  - `Failed` → status "Repack failed — your edits are retained. See log."

- **`TheJawaToolboxDotNet.csproj`** (Task 3, modified) — added 1 new `<Compile Include="Saving\TreRepackSaveTarget.cs" />` entry adjacent to the existing Saving cluster (round-2 HIGH-A).

## Decisions Made

- **Task 4 outcome (continuation-work in response to maintainer 'automate this against test assets'):** Automation-augmented. 5 new test classes (15 outcomes) cover the on-disk repack contract; live-SWG residual (cursor N-H1 ACK + UI end-to-end) deferred to Open Q1 + Open Q5.
- **Framework extraction (continuation-work refactor):** TreBackupPath + TreRepackLock moved to `UtinniCoreDotNet/Saving/` from the plugin's TreRepackSaveTarget. Mirrors 08-05 LooseOverridePath + 08-06 LivePatchValidator checker-B-1 pattern. The 08-REVIEWS MEDIUM-10 timestamped-backup-uniqueness + locked-archive-fallback contracts are now CI-coverable from the Utinni-only checkout.
- **TOC + name block compressed on output:** the rebuilt archive emits both the TOC and the name block UNCOMPRESSED (block-compression = 0). TreFile handles compressor 0/1/2 uniformly so the rebuilt archive parses correctly regardless of the original's TOC/name-block compression mode.
- **Edited entry recompresses as raw `deflate` (compressor=1):** documented V1 default; the edited entry's `Checksum` is preserved from the original because the path is unchanged (Open Q1/A1).
- **Path-CRC SELF-consistency vs CLIENT ACK:** the TreFile-side path-CRC round-trip is CI-covered (TreRepackLogicalPathTests asserts `repacked.Records[2].Checksum == original.Records[2].Checksum` after the edit). The SWG-client ACK (cursor N-H1: the client RESOLVES the edited record via its path CRC lookup) genuinely requires a running client and stays in the Tier-4 smoke. CI proves NECESSARY-but-not-SUFFICIENT.

## Verification Gate Matrix

| Gate | Status | Evidence |
|------|--------|----------|
| `dotnet test UtinniCoreDotNet.Tests --no-build -c Debug` | **PASS — 319/319** | 304 baseline + 15 new outcomes (2 + 1 + 4 + 3 + 5) |
| `dotnet test UtinniCoreDotNet.Tests --no-build -c Release` | **PASS — 319/319** | Both configurations green |
| MSBuild UtinniCoreDotNet Debug\|x86 | PASS | Pre-existing CS0108 generated-binding warnings only |
| MSBuild UtinniCoreDotNet Release\|x86 | PASS | Pre-existing CS0108 generated-binding warnings only |
| MSBuild TheJawaToolboxDotNet Debug\|x86 | PASS | clean |
| MSBuild TheJawaToolboxDotNet Release\|x86 | PASS | clean |
| Round-2 HIGH-A: `Formats\Tre\TreWriter.cs` in UtinniCoreDotNet.csproj | PASS | `grep -c` returns 1 |
| Round-2 HIGH-A: `Saving\TreBackupPath.cs` in UtinniCoreDotNet.csproj | PASS | `grep -c` returns 1 (continuation-work) |
| Round-2 HIGH-A: `Saving\TreRepackLock.cs` in UtinniCoreDotNet.csproj | PASS | `grep -c` returns 1 (continuation-work) |
| Round-2 HIGH-A: `Saving\TreRepackSaveTarget.cs` in TheJawaToolboxDotNet.csproj | PASS | `grep -c` returns 1 |
| Plugin's TreRepackSaveTarget consumes framework TreBackupPath | PASS | direct call to `TreBackupPath.NextAvailable(trePath, DateTime.UtcNow)` |
| Plugin's TreRepackSaveTarget consumes framework TreRepackLock | PASS | shim delegates to `TreRepackLock.IsSharingViolation` |

## Phase 8 Consolidated Success Criteria (PROD-W1-IFF)

Phase 8 ROADMAP-stated criteria 1-5 status:

1. **IFF Editor subpanel loads inside TJT against a live SWG client.** — Automation-covered: 08-04 + 08-05 + 08-06 + 08-07 ship the editor. Plugin builds clean in both Debug & Release. Live-load verification was performed in 08-05 Task 5 smoke (maintainer "approved, dig in" after the second-open AV regression scenario worked post-hide-not-dispose fix). **No live-client retest needed for 08-07's additive Repack menu** — provenance-gating disables it when not on a TreArchive source, and the gate is unit-tested via the OpenSource pattern-match logic from 08-01.
2. **User can open an IFF, view chunks, edit, and save back to a file the live client reloads.** — Automation-covered for the FILE save modes (08-05 loose override + Save As; this plan adds the .tre repack mode). The live client RELOAD path is CI-covered for textures + terrain (08-05 Tasks 3-4 — `GroundScene.Get().ReloadTerrain()` + `Graphics.ReloadTextures()`). The reload AFTER repack would route through the same dispatcher; CI proves the routing-table classification. **Live-observation of post-repack reload is deferred to Open Q5.**
3. **`utinni-cli inspect-iff` golden test covers the same read path.** — Covered by 08-02 (`utinni-cli roundtrip-iff` verb + golden fixtures).
4. **Edits survive a save→reload round trip without corrupting unedited chunks.** — **AUTOMATION-COVERED THIS PLAN** for the .tre repack save mode: TreWriterTests (3 facts) + TreRepackRoundTripTests (2 facts) + TreRepackLogicalPathTests (1 fact) + TreRepackByteDiffTests (5 outcomes) collectively prove that for every UNTOUCHED record across an edit-and-repack cycle, the raw compressed slice + raw name bytes + GetRecordData are byte-identical to the original. Loose-override + Save-As corruption-free behavior is similarly covered by 08-05's IffWriter + MutableIffDocument round-trip tests (143/143 IFF + Saving tests).
5. **IFF primitives are exported from a shared, non-plugin assembly.** — Covered by 08-01 (D-01 reconciliation: IffReader, IffWriter, MutableIffDocument, OpenSource all live in `UtinniCoreDotNet/Formats/Iff/`). 08-07 extends `UtinniCoreDotNet/Formats/Tre/` with TreWriter + the two new TreFile APIs and `UtinniCoreDotNet/Saving/` with TreBackupPath + TreRepackLock — all framework-side.

## Deviations from Plan

### Continuation-work additions (in response to maintainer direction)

Task 4 of the original plan was the live-SWG smoke checkpoint. The prior executor stopped at the checkpoint and the maintainer responded with: **"Can you write tests that automate this process against test assets, this should be automated if possible."**

The continuation-work that this SUMMARY documents (Tasks 5 + 5a-5e in the continuation work):

**Task 5 — Framework extraction:**
- Extracted `TreBackupPath` (timestamped backup uniqueness helper) to `UtinniCoreDotNet/Saving/TreBackupPath.cs`
- Extracted `TreRepackLock` (locked-archive probe + canonical HResult detector) to `UtinniCoreDotNet/Saving/TreRepackLock.cs`
- Refactored `TheJawaToolboxDotNet/Saving/TreRepackSaveTarget.cs` to delegate to the framework helpers; behavior under live SWG is unchanged
- Added 2 new `<Compile Include>` entries to `UtinniCoreDotNet.csproj` (round-2 HIGH-A)
- Atomic commits: Utinni `50ae57c` + UtinniPlugins `8d3c3ad`

**Tasks 5a-5e — 5 new test classes (15 outcomes):**
- Task 5a: `TreRepackRoundTripTests.cs` (2 [Fact]s) — commit `07e3671`
- Task 5b: `TreRepackLogicalPathTests.cs` (1 [Fact]) — commit `8bf9c3e`
- Task 5c: `TreRepackLockedArchiveTests.cs` (4 [Fact]s) — commit `2341874`
- Task 5d: `TreRepackBackupTests.cs` (3 [Fact]s) — commit `d2c4987`
- Task 5e: `TreRepackByteDiffTests.cs` (1 [Fact] + 1 [Theory]×4 = 5 outcomes) — commit `739b519`

### Inline deviations during auto Tasks 1-3 (logged 2026-05-28 by prior executor)

None — see the inline `<reasoning>` blocks in commits `6d51cb6` / `afe0e65` / `4e7084a` / `9bd8bb9` / `ee1c4a2`. Tasks 1-3 executed exactly as planned.

### Continuation-work IOException.HResult discovery

Continuation Task 5c initially attempted to construct `IOException` with `{ HResult = ... }` object-initializer syntax. `IOException.HResult` setter is `protected` on net472 — produces CS0272 at compile. Fixed before the test class commit by switching to the public `IOException(string message, int hresult)` ctor. No production code affected.

## Commit Table

| Task | Commit (Utinni) | Commit (UtinniPlugins) | Files |
|------|-----------------|------------------------|-------|
| 1 — TreFile RED + GREEN | `6d51cb6` (test) + `afe0e65` (feat) | — | `TreFile.cs`, `TreFileGetRecordCompressedBytesTests.cs` |
| 2 — TreWriter RED + GREEN | `4e7084a` (test) + `9bd8bb9` (feat) | — | `TreWriter.cs`, `TreWriterTests.cs`, `UtinniCoreDotNet.csproj`, `UtinniCoreDotNet.Tests.csproj` |
| 3 — Plugin save target + Save▾ wire | — | `ee1c4a2` | `TreRepackSaveTarget.cs`, `FormIffEditor.cs`, `TheJawaToolboxDotNet.csproj` |
| (state snapshot mid-checkpoint) | `ac92999` | — | `STATE.md` |
| 5 — Framework refactor (continuation) | `50ae57c` | `8d3c3ad` | `TreBackupPath.cs`, `TreRepackLock.cs`, `UtinniCoreDotNet.csproj`, `TreRepackSaveTarget.cs` |
| 5a — Round-trip byte-identity | `07e3671` | — | `TreRepackRoundTripTests.cs`, `TreFileFixtures.cs` |
| 5b — Logical-path self-consistency | `8bf9c3e` | — | `TreRepackLogicalPathTests.cs` |
| 5c — Locked-archive refusal | `2341874` | — | `TreRepackLockedArchiveTests.cs` |
| 5d — Backup uniqueness | `d2c4987` | — | `TreRepackBackupTests.cs` |
| 5e — Byte-diff + slice identity | `739b519` | — | `TreRepackByteDiffTests.cs` |

## Self-Check: PASSED

All claimed artifacts verified to exist:
- `D:/Code/Utinni/UtinniCoreDotNet/Formats/Tre/TreWriter.cs` — FOUND
- `D:/Code/Utinni/UtinniCoreDotNet/Saving/TreBackupPath.cs` — FOUND
- `D:/Code/Utinni/UtinniCoreDotNet/Saving/TreRepackLock.cs` — FOUND
- `D:/Code/Utinni/UtinniCoreDotNet.Tests/FormatsTests/Tre/TreRepackRoundTripTests.cs` — FOUND
- `D:/Code/Utinni/UtinniCoreDotNet.Tests/FormatsTests/Tre/TreRepackLogicalPathTests.cs` — FOUND
- `D:/Code/Utinni/UtinniCoreDotNet.Tests/FormatsTests/Tre/TreRepackLockedArchiveTests.cs` — FOUND
- `D:/Code/Utinni/UtinniCoreDotNet.Tests/FormatsTests/Tre/TreRepackBackupTests.cs` — FOUND
- `D:/Code/Utinni/UtinniCoreDotNet.Tests/FormatsTests/Tre/TreRepackByteDiffTests.cs` — FOUND
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/TreRepackSaveTarget.cs` — FOUND

All claimed commits verified to exist in `git log --oneline --all`:
- Auto Tasks 1-3: `6d51cb6`, `afe0e65`, `4e7084a`, `9bd8bb9`, `ee1c4a2` (UtinniPlugins), `ac92999`
- Continuation-work: `50ae57c`, `8d3c3ad` (UtinniPlugins), `07e3671`, `8bf9c3e`, `2341874`, `d2c4987`, `739b519`

Round-2 HIGH-A csproj gates: `grep -c` returns 1 for each of TreWriter.cs / TreBackupPath.cs / TreRepackLock.cs entries in `UtinniCoreDotNet.csproj`; `grep -c` returns 1 for `TreRepackSaveTarget.cs` entry in `TheJawaToolboxDotNet.csproj`.

`dotnet test --no-build -c Debug` AND `dotnet test --no-build -c Release` both report **319/319 passing**.
