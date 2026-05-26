---
phase: 07
slug: tjt-subpanel-tre-browser-read-only
status: approved
nyquist_compliant: true
wave_0_complete: false
created: 2026-05-26
revised: 2026-05-26
revision_note: "Reconciled against cross-AI review (07-REVIEWS.md). Added 07-00 Wave-0 fixtures plan; split 07-04 into 07-04a/07-04b; per-task map + Wave 0 items updated."
---

# Phase 07 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit / golden-fixture asserts in `Utinni.Cli.Tests` (existing from Phase 4) + `msbuild` build gate for the TJT WinForms host |
| **Config file** | `Utinni.Cli.Tests/Utinni.Cli.Tests.csproj` (existing); `.github/workflows/ci.yml` runs the `dotnet test` lanes per push (self-hosted v145 runner) |
| **Quick run command** | `dotnet test Utinni.Cli.Tests --filter "TreFixtureBuilder\|ParseTre\|CotMasterIndex\|TreArchiveIndex\|ListObjects\|InspectIff\|Decoder\|DecodeIff"` |
| **Full suite command** | `dotnet test Utinni.Cli.Tests` |
| **Estimated runtime** | ~10–30 s for the filtered TRE/decoder lane; full `Utinni.Cli.Tests` lane is the existing Phase-4 suite. Exact number measured during Wave 0 (`dotnet test` reports elapsed). The in-repo synthetic fixtures (07-00) add ms only; the large COT2000/v6000 goldens are env-gated on `SWG_SAMPLE_TRE_DIR` and add a few seconds only when that var points at `D:\Sample-TRE-Files`. |

> The TRE/IFF/decoder read path is shared between `Utinni.Cli` (golden-tested) and the
> TJT browser (success criterion #4) via the framework `TreArchiveIndex` / `TrePayloadResolver`
> facade (07-01), so the CLI golden tests validate the same `Formats/` code the TJT Form calls.
> `parse-tre` AND `list-objects` (migrated off the OBJS byte-scan in 07-01) plus `inspect-iff`
> (IffChunk.OffsetBytes, 07-03) and `decode-iff` (07-04a/b) cover the browser code paths. The
> TJT UI host itself (Form load, tree expand, overlay color) has no headless harness — it is
> exercised by the Release/x86 build gate (structural) plus the Tier-4 live-SWG manual smoke.
> See Manual-Only Verifications.

---

## Sampling Rate

- **After every framework task commit (plans 07-00, 07-01, 07-04a, 07-04b framework tasks):** Run the filtered `dotnet test` lane
- **After every TJT-host task commit (plans 07-02, 07-03, 07-04b UI task):** Run the Release/x86 `msbuild` build gate on `TheJawaToolboxDotNet.csproj`
- **After every plan wave:** Run `dotnet test Utinni.Cli.Tests` (full lane) and rebuild both repos Release/x86
- **Before `/gsd:verify-work`:** Full `dotnet test` lane green + both repos build Release/x86; the three blocking-human live-SWG smokes signed off
- **Max feedback latency:** < 30 s for the filtered CLI lane; the TJT build gate is a single `msbuild` invocation

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 07-00-01 | 00 | 0 | PROD-W1-TRE, PROD-01 | T-07-00-01 | Deterministic in-repo synthetic fixtures (v6000/COT2000/5000/0004/zlib) + env resolver; regenerate-and-compare self-test prevents drift | unit/fixture-gen | `dotnet test Utinni.Cli.Tests --filter TreFixtureBuilder` | created in task | ⬜ pending |
| 07-00-02 | 00 | 0 | PROD-W1-TRE, PROD-01 | T-07-00-01 | Malformed fixtures (count×stride, offset+length, bad-Adler, unknown compressor) proven to be the intended malformed shapes | unit/fixture-gen | `dotnet test Utinni.Cli.Tests --filter TreFixtureBuilder` | created in task | ⬜ pending |
| 07-01-01 | 01 | 1 | PROD-W1-TRE, PROD-01 | T-07-01..04 | Version-dispatch + zlib `%31==0` validate + `[2..^4]` slice; 5000 enumerate-empty (no v6000-stride routing); named division/subtraction checked arithmetic | unit/golden | `dotnet test Utinni.Cli.Tests --filter ParseTre` | created in task (07-00 fixtures) | ⬜ pending |
| 07-01-02 | 01 | 1 | PROD-W1-TRE, PROD-01 | T-07-01..05 | Lazy TOC-only + deterministic PayloadReadCount + explicit Open(Stream) contract; `treeFileIndex < numTreeFiles`; TreArchiveIndex/TrePayloadResolver shared facade | unit/golden | `dotnet test Utinni.Cli.Tests --filter "ParseTre\|CotMasterIndex\|TreArchiveIndex"` | created in task | ⬜ pending |
| 07-01-03 | 01 | 1 | PROD-W1-TRE (criterion #4) | T-07-04 | list-objects migrated onto shared IffReader (OBJS byte-scan retired); locked schemaVersion:1 contract preserved | unit/golden | `dotnet test Utinni.Cli.Tests --filter ListObjects` | exists | ⬜ pending |
| 07-02-01 | 02 | 2 | PROD-W1-TRE | T-07-06 | Themed shell, no `Color.FromArgb`; 250 ms debounce; filter scans the flat AllPaths index (not a per-tick re-walk); whole-node bold | build/structural | `msbuild TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj /p:Configuration=Release /p:Platform=x86 /t:Build /v:minimal` | created in task | ⬜ pending |
| 07-02-02 | 02 | 2 | PROD-W1-TRE, PROD-01 | T-07-06..08 | Off-thread enumeration via shared TreArchiveIndex + `Control.Invoke`; lazy `BeforeExpand`; overlay via `FilenameCount`+`GetFilenameAt` install-time snapshot (limitation documented); no `getAllFilenames` | build/structural | `msbuild TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj /p:Configuration=Release /p:Platform=x86 /t:Build /v:minimal` | created in task | ⬜ pending |
| 07-02-03 | 02 | 2 | PROD-W1-TRE (criteria 1+2) | T-07-06..08 | Live-host smoke: shell + tree + flat-index filter + overlay | manual (Tier 4, blocking-human) | MANUAL — no headless harness for TJT UI host | n/a | ⬜ pending |
| 07-03-01 | 03 | 3 | PROD-W1-TRE, PROD-01 | T-07-09 | IffChunk.OffsetBytes added + populated from existing parser position; inspect-iff lane stays green (flat view preserved) | unit/golden | `dotnet test Utinni.Cli.Tests --filter InspectIff` | exists (goldens) | ⬜ pending |
| 07-03-02 | 03 | 3 | PROD-W1-TRE, PROD-01 | T-07-09 | Detail pane consumes `IffReader` output only (no byte-scan); renders `@offset`; parse wrapped try/catch → `ShowParseFailure`; version-accurate encrypted banner (v6000 vs v5000) | build/structural | `msbuild TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj /p:Configuration=Release /p:Platform=x86 /t:Build /v:minimal` | created in task | ⬜ pending |
| 07-03-03 | 03 | 3 | PROD-W1-TRE, PROD-01 | T-07-10..11 | On-demand single-payload resolve off-thread via shared TrePayloadResolver (no direct GetRecordData in UI); enumerate-only → `ShowEncrypted`; Pitfall-4 obfuscation guard | build/structural | `msbuild TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj /p:Configuration=Release /p:Platform=x86 /t:Build /v:minimal` | created in task | ⬜ pending |
| 07-03-04 | 03 | 3 | PROD-01 | T-07-09..12 | Live-host smoke: metadata + IFF chunk tree (@offset) + encrypted/parse-fail states | manual (Tier 4, blocking-human) | MANUAL — no headless harness for TJT UI host | n/a | ⬜ pending |
| 07-04a-01 | 04a | 4 | PROD-01, PROD-W1-TRE | T-07-13..14 | DataTableDecoder pure (no JSON/console/file-write); LE scalars (numCols≠16777216); forged-count → `DecoderException` via division-form guard; decode-iff verb + golden | unit/golden | `dotnet test Utinni.Cli.Tests --filter "Decoder\|DecodeIff"` | created in task | ⬜ pending |
| 07-04a-02 | 04a | 4 | PROD-01, PROD-W1-TRE | T-07-13..14, T-07-16 | StringTable + ObjectTemplate decoders pure + bounds-checked; STF non-ASCII round-trip; decode-iff dispatch | unit/golden | `dotnet test Utinni.Cli.Tests --filter "Decoder\|DecodeIff"` | created in task | ⬜ pending |
| 07-04b-01 | 04b | 5 | PROD-01 (every-asset-class) | T-07-13..15 | AppearanceSummary (mesh family) + IffStructureSummary (shader/UI-page) pure + bounds-checked; unrecognized tag returns empty (no throw); decode-iff dispatch + golden for shader/UI-page | unit/golden | `dotnet test Utinni.Cli.Tests --filter "Decoder\|DecodeIff"` | created in task | ⬜ pending |
| 07-04b-02 | 04b | 5 | PROD-01 (every-asset-class) | T-07-13..17 | All five structured views (datatable/STF/template/mesh + shader/UI-page summary) call shared decoders (no UI-only decode) | build/structural | `msbuild TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj /p:Configuration=Release /p:Platform=x86 /t:Build /v:minimal` | created in task | ⬜ pending |
| 07-04b-03 | 04b | 5 | PROD-01 (every-asset-class incl. UI page + shader) | T-07-13..17 | Live-host smoke: per-type structured views (incl. shader + UI page) + decode-iff CLI parity | manual (Tier 4, blocking-human) | MANUAL — no headless harness for TJT UI host | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

> **Wave-0 gate (review consensus #4):** `07-00` (wave 0) MUST be green before `07-01` (wave 1) runs.
> `07-01 depends_on: ["07-00"]`. The synthetic fixtures the 07-01 TDD tasks assert against are
> produced by 07-00, not assumed.
>
> Key validation targets surfaced by research + sharpened by the cross-AI review:
> - **TRE version dispatch** (0004/0005/0006 size-first vs 6000/COT2000 crc-first) — in-repo synthetic golden (07-00) + env-gated real golden against `D:\Sample-TRE-Files\`.
> - **5000 enumerate-only** — recognized tag → `EnumerateOnly`, ZERO records, NO v6000-stride routing; the synthetic non-6000-layout 5000 fixture must enumerate-empty and NOT throw (review consensus #1).
> - **zlib RFC1950 framing** (`0x78 0x9c` strip, `%31==0` validate, `[2..^4]` slice) — decode-roundtrip on the synthetic v6000 block + raw-deflate fallback; bad-Adler/unknown-compressor raise documented kinds.
> - **Lazy enumeration** — deterministic `PayloadReadCount==0` for parse-tre (replaces the timing/IO proxy, review consensus #2).
> - **Open(Stream) contract** — stream-backed GetRecordData throws documented `InvalidOperationException` (review consensus #2).
> - **Checked arithmetic** — division-form `recordCount > (streamLength - headerSize) / stride` + subtraction-form `offset <= streamLength - length` before allocation (review consensus #4).
> - **COT2000 master index** — in-repo synthetic (>=2 tree files) + env-gated real count assert (213086 paths / 45 tre names).
> - **Shared facade** — `TreArchiveIndex`/`TrePayloadResolver` consumed by both CLI tests and the browser (review consensus #7); `list-objects` migrated onto `IffReader`.
> - **IffChunk.OffsetBytes** — root==0, child==header position; inspect-iff lane green (review codex MEDIUM #12).
> - **Per-type decoders** (datatable/STF/object-template/mesh + shader/UI-page summary) — structural asserts vs synthesized fixtures; LE-scalar + forged-count + non-ASCII guards; shader + UI-page headless structured-view proof (review item 8).

---

## Wave 0 Requirements

> **RECONCILED (cross-AI review, 2026-05-26):** Wave 0 is now its own gating plan **`07-00-PLAN.md`**
> (wave 0; `07-01 depends_on: ["07-00"]`). The synthetic fixtures below are no longer `[ ]`
> assumptions consumed inside 07-01 — they are explicit deliverables of 07-00, produced by a
> deterministic in-repo byte builder (`TreFixtureBuilder`) so the real-archive code paths run on
> CI **without** depending on the env-gated `D:\Sample-TRE-Files` set (review consensus #3/#4).

- [x] **In-repo synthetic fixtures** (07-00) — `synthetic-v6000-2record.tre`, `synthetic-cot2000-2tree.toc` (>=2 tree files) + its companion `cot2000/tree0.tre`+`tree1.tre` archives (self-contained resolver path, review consensus #2), `synthetic-5000-header.tre` (deliberately NON-6000 layout), `synthetic-0004-header.tre`, `zlib-framed-1record-v6000.tre` (renamed from v0006 for naming consistency, review item 5), plus four malformed fixtures (count×stride, offset+length, detectable-bad zlib frame, unknown compressor). Run on every CI invocation.
- [x] **`FixturePath.SampleTreDir()` env resolver** (07-00) — real `D:\Sample-TRE-Files\` v6000/COT2000 + the synthesized 0005 fixture; the `SWG_SAMPLE_TRE_DIR` env-gate SUPPLEMENTS the in-repo synthetic coverage (does not replace it).
- [x] zlib-vs-raw-deflate inflate test fixture (07-00 `zlib-framed-1record-v6000.tre` `0x78 0x9c`-framed + the existing raw-deflate 0005 fixture)
- [x] Synthetic 5000-header fixture + enumerate-empty/no-throw test (07-00 fixture; 07-01 Task 1 asserts EnumerateOnly + zero records + no throw against the non-6000 layout)
- [x] Lazy-enumeration assertion via deterministic `PayloadReadCount` counter (07-01 Task 2 — replaces the prior timing/IO proxy, review consensus #2)
- [x] COT2000 master-index golden — in-repo synthetic (07-00 `synthetic-cot2000-2tree.toc`) + env-gated real count=213086/45-tre-names (07-01 Task 2, Skip when `SWG_SAMPLE_TRE_DIR` unset)
- [ ] Per-type decoder fixtures: small datatable / STF / object-template / mesh / shader / UI-page `.iff` — probe `D:/Code/swg-main/serverdata`, else author in-repo synth under `Utinni.Cli.Tests/Fixtures/iff/` (open question #2; produced inside 07-04a/07-04b Task 1 as the decoder TDD fixtures)
- [ ] Source a real SWGEmu 0004/0005/0006 fixture if available (open question #1 — until then keep size-first for those versions; the existing CLI goldens already cover the size-first path; 07-00 ships a synthetic 0004 header marked real-layout-unverified)
- [ ] Confirm `Utinni.Cli.Tests` framework + quick-run timing (record the measured elapsed into the Estimated runtime row)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| TRE Browser Form loads inside TJT against a live SWG client | PROD-W1-TRE (criterion 1) | Requires injected live SWG session; no headless harness for the TJT UI host | Inject Utinni+TJT into SWG, open the TRE Browser, confirm a resizable window (per 07-02 Task 3) |
| Navigate full `.tre` mount set, expand subtrees lazily, debounced flat-index filter, loaded/dimmed snapshot overlay | PROD-W1-TRE (criterion 2) | UI interaction in live host | Expand virtual-path tree, filter (no freeze on 100k+), confirm overlay coloring + legend (per 07-02 Task 3) |
| Detail pane: metadata + universal IFF chunk tree (@offset) + hex peek + encrypted (version-accurate)/parse-fail states | PROD-01 | UI rendering in live host | Select readable IFF, v6000 encrypted, v5000 (if available), and corrupt entries; confirm states (per 07-03 Task 4) |
| Per-type structured views (datatable/STF/object-template/mesh + shader + UI page) + decode-iff CLI parity | PROD-01 (every asset class incl. UI page + shader) | UI rendering in live host; decoders themselves are golden-tested headlessly via decode-iff | Select each asset class incl. shader + UI page, confirm structured views; spot-check `decode-iff <fixture>` JSON matches the panel (per 07-04b Task 3) |

> All three blocking-human checkpoints (07-02-03, 07-03-04, 07-04b-03) are the documented
> Tier-4 manual residual (TEST-04). They verify AFTER the auto-verifiable build/grep/golden
> work in the earlier tasks of each plan is green — they do not substitute for automation. The
> framework decode logic underneath every UI view IS headlessly covered by the CLI golden
> lane (including the shader/UI-page summary, review item 8), so the manual residual is confined
> to the WinForms host presentation, which has no headless harness.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or are documented Tier-4 manual residual (the three blocking-human live-SWG smokes; framework logic under them is golden-tested via the CLI lane)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify — every framework task (07-00, 07-01, 07-04a, 07-04b Task 1) has a `dotnet test` command; every TJT-host task has the Release/x86 build gate; only the per-plan trailing checkpoint is manual
- [x] Wave 0 covers all MISSING references and is now a gating plan (07-00) that 07-01 depends on
- [x] No watch-mode flags (all commands are single-shot `dotnet test` / `msbuild`)
- [x] Feedback latency < 30 s for the filtered CLI lane
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved (original 2026-05-26); revised to reconcile with cross-AI review 2026-05-26 (07-00 gate, 07-04 split, fixture deliverables, shared facade, list-objects + OffsetBytes lanes)
