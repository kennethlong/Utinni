---
phase: 07
slug: tjt-subpanel-tre-browser-read-only
status: verified
threats_open: 0
asvs_level: 1
created: 2026-05-27
---

# Phase 7 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> TRE Browser (read-only): version-dispatch TRE reader + shared facade, IFF chunk reader,
> per-type decoders, decode/inspect/parse/list CLI verbs, and the TJT WinForms browser shell
> + detail pane. The register below is plan-time-authored across 07-00..07-04b; each threat is
> verified to have its declared mitigation present in implemented code (file:line evidence).

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| disk `.tre`/`.toc`/`ws.iff`/`.stf` → TreFile / CotMasterIndex / IffReader / decoders | Untrusted attacker-influenceable binary (a mod file a user downloaded). All header counts, offsets, sizes, compressed blocks, cell/field counts are adversary-controlled. | binary container bytes |
| CLI argv (`parse-tre`/`list-objects`/`inspect-iff`/`decode-iff`) → readers | User-supplied path; FileNotFound (exit 3) + exception envelope (exit 2) contract. | filesystem path |
| `TreEntryDescriptor.ResolvedArchivePath` → `TrePayloadResolver` file open | Path composed from master-index base dir + adversary-influenceable tree-file name. | composed filesystem path |
| injected client working dir → `ResolveClientTreDir` | A path read from the injected client; only enumerates files, never executes. | directory path |
| live `Game.Repository` (in-process) → Form overlay | In-process harvested set; read-only via FilenameCount + GetFilenameAt (CON-N-02). | string filename list |
| game thread ↔ UI thread | Cross-thread WinForms control + game-state access; node mutations marshaled; harvest read matches the established FormObjectBrowser background-read pattern. | control mutations / snapshot |
| test-fixture bytes on disk → 07-01 reader tests | The fixtures ARE the adversarial inputs; builder runs in the test assembly only. | synthetic binary |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation (verified evidence) | Status |
|-----------|----------|-----------|-------------|--------------------------------|--------|
| T-07-00-01 | Tampering | committed fixture drift from the builder | mitigate | Regenerate-and-compare self-test asserts committed fixture bytes byte-identical to builder output. `Utinni.Cli.Tests/Infrastructure/TreFixtureBuilderTests.cs` (07-00-SUMMARY: "hand-edited committed fixture fails CI (threat T-07-00-01)"). | closed |
| T-07-00-02 | Information Disclosure | `SWG_SAMPLE_TRE_DIR` pointing outside the repo | accept | Test-only path hint; resolver returns the raw env value for env-gated test reads only; no shipping code consumes it. Logged in Accepted Risks. `FixturePath.SampleTreDir()`. | closed |
| T-07-01 | Denial of Service | TreFile/CotMasterIndex header `count*stride` allocation | mitigate | Division-form guard `recordCount > MaxBlockSize / recordStride` (cannot overflow) before any allocation — `TreFile.cs:210`; COT2000 `numFiles > MaxBlockSize / 32` — `CotMasterIndex.cs:187`; `ValidateLength` caps every count/size — `TreFile.cs:217-220,598`. | closed |
| T-07-02 | Denial of Service | zlib/deflate inflate (decompression bomb) | mitigate | Inflate output capped at min(256 MB, declared size); over-expansion throws `DeflateExpansionExceedsCap` — `TreFile.cs:408-411,535`; resolver cap — `TrePayloadResolver.cs:171,187`; CotMasterIndex cap — `CotMasterIndex.cs:311-314`. | closed |
| T-07-03 | Tampering | out-of-bounds offsets (info/name/fileName/treeFileIndex/record/ArchiveLocalOffset) | mitigate | Subtraction-form `offset > streamLength - compressedSize` (never `offset+length`) — `TreFile.cs:315`; `treeFileIndex < numTreeFiles` — `CotMasterIndex.cs:228`; cumulative name-offset bound — `CotMasterIndex.cs:238`; resolver re-validates ArchiveLocalOffset+CompressedLength vs opened archive length — `TrePayloadResolver.cs:101`. | closed |
| T-07-04 | Denial of Service | truncated / malformed archive / bad zlib frame | mitigate | Short-read → Truncated — `TreFile.cs:463-467,391`; zlib `%31==0` header validated before strip — `TreFile.cs:499`; truncated/invalid frame → `InvalidZlibTrailer` inflate-side (not Adler-only) — `TreFile.cs:494,501,558`; unknown compressor → `UnknownCompressor` — `TreFile.cs:440,514`. Kinds exist in `TreParseException.cs:44,50`. | closed |
| T-07-05 | Tampering | path traversal via crafted virtual path / crafted tree-file name escaping the base dir | mitigate | Virtual paths surfaced as data only, never resolved against FS (browser `BuildTrie`/`MakeNode`). Resolver rejects `..`/`Path.IsPathRooted` tree-file names and confirms `Path.GetFullPath(resolved).StartsWith(base+sep)` before opening — `TrePayloadResolver.cs:66-85`. No extraction in V1. | closed |
| T-07-06 | Denial of Service | UI freeze/OOM: 213k tree nodes / per-tick filter re-walk / broad-filter full rebuild | mitigate | Lazy tree (top-level eager, `BeforeExpand` children, dummy-child glyph) — `FormTreBrowser.cs:373,377`; enumeration on background `Task.Run` — `FormTreBrowser.cs:181`; filter scans flat `_allPaths` once per debounced tick — `FormTreBrowser.cs:537`; `FilterMatchCap=5000` → flat ListView + refine status — `FormTreBrowser.cs:49,549-563`. | closed |
| T-07-07 | Tampering | crafted virtual path (`../`) surfaced in the tree | mitigate | Paths displayed as `TreeNode` text only; never resolved against the filesystem (read-only browse, no extraction). `FormTreBrowser.cs` `BuildTrie`/`MakeNode` use the path purely as a label/key. | closed |
| T-07-08 | Information Disclosure / crash | stale or empty Repository harvest snapshot | accept/mitigate | Best-effort install-time snapshot; disk enumeration is source of truth; failed/empty read degrades to null → all-full-color + no-client legend — `FormTreBrowser.cs:335-356,504-509`; snapshot limitation documented in legend copy. | closed |
| T-07-09 | Denial of Service | pathological IFF nesting / huge chunk in the selected payload | mitigate | IffReader enforces 64 MB `MaxChunkSize` — `IffReader.cs:71,175`, `NestedChunkOverflow` — `IffReader.cs:190`, Truncated short-read — `IffReader.cs:300,348`; detail pane consumes bounded parse output only and wraps the parse in try/catch → ShowParseFailure — `TreDetailPane.cs:139-148`. | closed |
| T-07-10 | Denial of Service | large readable payload freezing the UI thread | mitigate | On-demand resolve runs on background `Task.Run`, marshaled via `BeginInvoke` — `FormTreBrowser.cs:432-448`; metadata shows immediately via `ShowDecoding` — `FormTreBrowser.cs:427`, `TreDetailPane.cs:119`. | closed |
| T-07-11 | Tampering | corrupt descriptor / truncated payload | mitigate | `TreParseException` + `IOException` from `TryResolve` caught and routed to `ShowParseFailure` — `FormTreBrowser.cs:440-447`; detail-pane parse failures isolated to one file — `TreDetailPane.cs:139-148,246-253`. | closed |
| T-07-12 | Information Disclosure | clipboard "Copy path / Copy CRC" | accept | Read-only clipboard copy of displayed text; no asset write/export — `TreDetailPane.cs:601-607`. Benign for a local desktop tool. Logged in Accepted Risks. | closed |
| T-07-13 | Denial of Service | forged numCols/numRows/entry/joint/frame/child counts driving over-allocation | mitigate | Division-form `count > Data.Length / stride` before allocation: datatable `numCols`/`numRows` — `DataTableDecoder.cs:146,190`; STF entry/char counts — `StringTableDecoder.cs:120,134`; object-template paramCount — `ObjectTemplateDecoder.cs:175`; skeleton joint count — `AppearanceSummary.cs:138`. Throws `DecoderException`, not OOM. | closed |
| T-07-14 | Tampering | out-of-bounds cell/field reads within a chunk payload | mitigate | All scalar reads via the bounds-checked `IffPayloadCursor.Need()` — `IffPayloadCursor.cs:142-150`; unterminated C-string → Truncated — `IffPayloadCursor.cs:131`. | closed |
| T-07-15 | Denial of Service | pathological IFF nesting reaching the decoders | mitigate | Shared `IffReader` enforces 64 MB cap + `NestedChunkOverflow` before decoders run — `IffReader.cs:71,175,190`; decoders consume the bounded parse output only (no own walker — grep confirms no IffReader.Read / GetRecordData in `Formats/Decoders`). | closed |
| T-07-16 | Tampering | malformed STF encoding / surrogate abuse | mitigate | Text decoded via `Encoding.Unicode` (UTF-16LE) with replacement-on-invalid (does not throw on bad units) — `StringTableDecoder.cs:128,140`; displayed as data only. | closed |
| T-07-17 | Denial of Service | UI freeze rendering a huge structured ListView | mitigate | `StructuredRowCap=5000` applied to every structured view + `"… {total} rows — showing first {N}"` truncation label — `TreDetailPane.cs:62,304,321,340,365,415-419`; decode in the off-thread AfterSelect path. | closed |
| T-07-18 | Tampering | misclassifying a readable non-IFF asset as "encrypted" (misleading UX) | mitigate | Encrypted state gated ONLY on `TryResolve` returning false (enumerate-only) — `FormTreBrowser.cs:453-459`; a readable non-FORM payload routes to the distinct `ShowUnsupportedRaw` — `FormTreBrowser.cs:476-481`, `TreDetailPane.cs:221-244`. | closed |
| T-07-19 | Tampering | object-template base reference pointing at an arbitrary path | mitigate | Bounded posture: base reference DISPLAYED as an opaque name string only, never resolved against FS/TRE — `ObjectTemplateDecoder.cs:138-148`; grep confirms NO `TreArchiveIndex`/`TrePayloadResolver`/`GetRecordData`/recursive walk anywhere in `Formats/Decoders`. | closed |
| T-07-SC | Tampering | npm/pip/cargo/NuGet installs (supply-chain) | mitigate | No package installs across any 07-plan: BCL + in-repo only (Adler32 computed inline; no new dep). Confirmed in every plan's SUMMARY and by absence of new package references. | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-07-01 | T-07-00-02 | `SWG_SAMPLE_TRE_DIR` is a test-only path hint consumed exclusively by env-gated test reads; no shipping code reads it; resolver returns null/raw value, never throws. No production information-disclosure surface. | Kenneth Long | 2026-05-27 |
| AR-07-02 | T-07-08 | Repository overlay is a best-effort install-time snapshot; disk enumeration remains source of truth and a failed/empty read degrades to a documented full-color + no-client legend. Stale/empty overlay is a cosmetic, non-crashing degradation acceptable for a local desktop modding tool. | Kenneth Long | 2026-05-27 |
| AR-07-03 | T-07-12 | "Copy path / Copy CRC" performs a read-only clipboard copy of already-displayed text only — no asset write/export and no DEC-A3 surface. Benign for a single-user local desktop tool. | Kenneth Long | 2026-05-27 |

*Accepted risks do not resurface in future audit runs.*

---

## Unregistered Flags

No `## Threat Flags` section appears in any 07-* SUMMARY (07-00..07-04b), so no new attack
surface was declared during implementation. The one issue flagged-not-fixed in 07-04a (the
`IffReader` EA-IFF-85 pad-strictness vs SWG's no-pad reality, `07-04a-SUMMARY.md` Issues) is a
functional parse defect — NOT new attack surface — and was subsequently resolved in 07-04b via
detect-not-assume pad handling (`IffReader.cs:307-327`). No unregistered flags.

---

## Audit Notes (verification-time observations, non-blocking)

- **V5000 reclassified readable, not enumerate-only.** Plans 07-00/07-01 declared V5000 as
  recognized-but-enumerate-empty; the implementation reclassified V5000 as the readable SWGEmu
  Pre-CU format (size-first 24-byte stride, zlib TOC/name) flowing through the same bounds-checked
  parse path (`TreFile.cs:189-194`, git commit `d75c701`). This is a functional/format-reality
  change documented in the SUMMARYs; it does NOT weaken any threat mitigation — the same
  division/subtraction-form guards (T-07-01/03), inflate cap (T-07-02), and zlib validation
  (T-07-04) apply to the V5000 path. The detail-pane encrypted branch (`TreDetailPane.cs:199-219`)
  now reaches only V6000, consistent with the reclassification.
- **Detail-pane structured decode runs on the UI thread inside `ShowReadable`** (the heavy payload
  *resolve* is off-thread per T-07-10; the subsequent `IffReader.Read` + decoder parse is on the UI
  thread after `BeginInvoke`). The bounded IFF cap (64 MB, T-07-09/T-07-15) + row cap (T-07-17)
  keep the parse magnitude bounded, so the T-07-10 mitigation intent (no unbounded UI-thread work)
  holds; noted for the maintainer, not a gap.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-05-27 | 21 | 21 | 0 | gsd-security-auditor (Claude Opus 4.7) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-05-27
