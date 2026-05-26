---
phase: 07
slug: tjt-subpanel-tre-browser-read-only
status: approved
nyquist_compliant: true
wave_0_complete: false
created: 2026-05-26
---

# Phase 07 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit / golden-fixture asserts in `Utinni.Cli.Tests` (existing from Phase 4) + `msbuild` build gate for the TJT WinForms host |
| **Config file** | `Utinni.Cli.Tests/Utinni.Cli.Tests.csproj` (existing); `.github/workflows/ci.yml` runs the `dotnet test` lanes per push (self-hosted v145 runner) |
| **Quick run command** | `dotnet test Utinni.Cli.Tests --filter "ParseTre\|CotMasterIndex\|Decoder\|DecodeIff"` |
| **Full suite command** | `dotnet test Utinni.Cli.Tests` |
| **Estimated runtime** | ~10–30 s for the filtered TRE/decoder lane; full `Utinni.Cli.Tests` lane is the existing Phase-4 suite. Exact number measured during Wave 0 (`dotnet test` reports elapsed). Large COT2000/v6000 goldens are env-gated on `SWG_SAMPLE_TRE_DIR` and add a few seconds only when that var points at `D:\Sample-TRE-Files`. |

> The TRE/IFF/decoder read path is shared between `Utinni.Cli` (golden-tested) and the
> TJT browser (success criterion #4), so the CLI golden tests validate the same `Formats/`
> code the TJT Form calls. The TJT UI host itself (Form load, tree expand, overlay color)
> has no headless harness — it is exercised by the Release/x86 build gate (structural) plus
> the Tier-4 live-SWG manual smoke. See Manual-Only Verifications.

---

## Sampling Rate

- **After every framework task commit (plans 01, 04):** Run `dotnet test Utinni.Cli.Tests --filter "ParseTre\|CotMasterIndex\|Decoder\|DecodeIff"`
- **After every TJT-host task commit (plans 02, 03):** Run the Release/x86 `msbuild` build gate on `TheJawaToolboxDotNet.csproj`
- **After every plan wave:** Run `dotnet test Utinni.Cli.Tests` (full lane) and rebuild both repos Release/x86
- **Before `/gsd:verify-work`:** Full `dotnet test` lane green + both repos build Release/x86; the three blocking-human live-SWG smokes signed off
- **Max feedback latency:** < 30 s for the filtered CLI lane; the TJT build gate is a single `msbuild` invocation

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 07-01-01 | 01 | 1 | PROD-W1-TRE, PROD-01 | T-07-01..04 | Version-dispatch + zlib-header `%31==0` validation before strip; bounds-checked TOC stride | unit/golden | `dotnet test Utinni.Cli.Tests --filter ParseTre` | ❌ W0 | ⬜ pending |
| 07-01-02 | 01 | 1 | PROD-W1-TRE, PROD-01 | T-07-01..05 | Lazy TOC-only enumeration (no eager 5.5 GB read); `treeFileIndex < numTreeFiles`, block-size-vs-stream caps | unit/golden | `dotnet test Utinni.Cli.Tests --filter "ParseTre\|CotMasterIndex"` | ❌ W0 | ⬜ pending |
| 07-02-01 | 02 | 2 | PROD-W1-TRE | T-07-06 | Themed shell, no `Color.FromArgb` literals; 250 ms debounce timer | build/structural | `msbuild TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj /p:Configuration=Release /p:Platform=x86 /t:Build /v:minimal` | ❌ W0 | ⬜ pending |
| 07-02-02 | 02 | 2 | PROD-W1-TRE, PROD-01 | T-07-06..08 | Off-thread enumeration + `Control.Invoke`; lazy `BeforeExpand`; read-only `Game.Repository` (no `getAllFilenames`) | build/structural | `msbuild TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj /p:Configuration=Release /p:Platform=x86 /t:Build /v:minimal` | ❌ W0 | ⬜ pending |
| 07-02-03 | 02 | 2 | PROD-W1-TRE (criteria 1+2) | T-07-06..08 | Live-host smoke: shell + tree + overlay | manual (Tier 4, blocking-human) | MANUAL — no headless harness for TJT UI host (see Manual-Only Verifications) | n/a | ⬜ pending |
| 07-03-01 | 03 | 3 | PROD-W1-TRE, PROD-01 | T-07-09 | Detail pane consumes `IffReader` output only (no byte-scan); parse wrapped try/catch → `ShowParseFailure` | build/structural | `msbuild TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj /p:Configuration=Release /p:Platform=x86 /t:Build /v:minimal` | ❌ W0 | ⬜ pending |
| 07-03-02 | 03 | 3 | PROD-W1-TRE, PROD-01 | T-07-10..11 | On-demand single-payload read off-thread; `TreParseException`/`IOException` caught → `ShowParseFailure`; enumerate-only → `ShowEncrypted` | build/structural | `msbuild TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj /p:Configuration=Release /p:Platform=x86 /t:Build /v:minimal` | ❌ W0 | ⬜ pending |
| 07-03-03 | 03 | 3 | PROD-01 | T-07-09..12 | Live-host smoke: metadata + IFF chunk tree + encrypted/parse-fail states | manual (Tier 4, blocking-human) | MANUAL — no headless harness for TJT UI host (see Manual-Only Verifications) | n/a | ⬜ pending |
| 07-04-01 | 04 | 4 | PROD-01, PROD-W1-TRE | T-07-13..14, T-07-16 | Pure decoders (no JSON/console/file-write); LE scalars (numCols≠16777216); forged-count → `DecoderException` not OOM; STF non-ASCII round-trip | unit/golden | `dotnet test Utinni.Cli.Tests --filter "Decoder\|DecodeIff"` | ❌ W0 | ⬜ pending |
| 07-04-02 | 04 | 4 | PROD-01, PROD-W1-TRE | T-07-13..15 | AppearanceSummary pure + bounds-checked; unrecognized tag returns empty (no throw); structured views call shared decoders | unit/golden + build | `dotnet test Utinni.Cli.Tests --filter "Decoder\|DecodeIff"` (and TJT Release/x86 build) | ❌ W0 | ⬜ pending |
| 07-04-03 | 04 | 4 | PROD-01 (every-asset-class) | T-07-13..16 | Live-host smoke: per-type structured views + decode-iff CLI parity | manual (Tier 4, blocking-human) | MANUAL — no headless harness for TJT UI host (see Manual-Only Verifications) | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

> File Exists ❌ W0 = the test/fixtures are created in the task itself (TDD tasks author their
> failing tests first; the existing `ParseTre` lane provides the 0005/0006 goldens that must
> stay green). Wave 0 fixture wiring is listed below.
>
> Key validation targets surfaced by research:
> - **TRE version dispatch** (0004/0005/0006 size-first vs 6000/COT2000 crc-first) — golden assert against real fixtures in `D:\Sample-TRE-Files\` (env-gated on `SWG_SAMPLE_TRE_DIR`).
> - **zlib RFC1950 framing** (`0x78 0x9c` strip, `%31==0` validate) — decode-roundtrip assert on a real v6000 block + raw-deflate fallback.
> - **5000 defensive path** — assert "recognized tag → enumerate-only, no layout assertion" (no fixture available; structural-sibling-of-6000 behavior, gated).
> - **COT2000 master index** — count assert (213086 paths / 45 tre names) gated on `SWG_SAMPLE_TRE_DIR`.
> - **Per-type decoders** (datatable/STF/object-template/mesh) — structural asserts vs known/synthesized fixtures; LE-scalar + forged-count + non-ASCII guards.

---

## Wave 0 Requirements

- [ ] TRE golden fixtures wired from `D:\Sample-TRE-Files\` (real v6000/COT2000 set + Utinni's synthesized 0005 fixture); env-gate on `SWG_SAMPLE_TRE_DIR`
- [ ] zlib-vs-raw-deflate inflate test fixture (a `0x78 0x9c`-framed block + the existing raw-deflate fixture)
- [ ] Synthesized 5000-header fixture + gated enumerate-only test
- [ ] Lazy-enumeration assertion (enumerate without reading payloads)
- [ ] COT2000 master-index golden (count=213086, 45 tre names), gated on `SWG_SAMPLE_TRE_DIR`
- [ ] Per-type decoder fixtures: small datatable / STF / object-template `.iff` — probe `D:/Code/swg-main/serverdata`, else author in-repo synth under `Utinni.Cli.Tests/Fixtures/iff/` (open question #2)
- [ ] Source a real SWGEmu 0004/0005/0006 fixture if available (open question #1 — until then keep size-first for those versions; the existing CLI goldens already cover the size-first path)
- [ ] Confirm `Utinni.Cli.Tests` framework + quick-run timing (record the measured elapsed into the Estimated runtime row)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| TRE Browser Form loads inside TJT against a live SWG client | PROD-W1-TRE (criterion 1) | Requires injected live SWG session; no headless harness for the TJT UI host | Inject Utinni+TJT into SWG, open the TRE Browser from the TJT forms menu, confirm it opens as a resizable window (per 07-02 Task 3) |
| Navigate full `.tre` mount set, expand subtrees lazily, debounced filter, loaded/dimmed overlay | PROD-W1-TRE (criterion 2) | UI interaction in live host | Expand virtual-path tree, filter, confirm overlay coloring + legend (per 07-02 Task 3) |
| Detail pane: metadata + universal IFF chunk tree + hex peek + encrypted/parse-fail states | PROD-01 | UI rendering in live host | Select readable IFF, v6000 encrypted, and corrupt entries; confirm states (per 07-03 Task 3) |
| Per-type structured views (datatable/STF/object-template/mesh) + decode-iff CLI parity | PROD-01 (every asset class) | UI rendering in live host; decoders themselves are golden-tested headlessly via decode-iff | Select each asset class, confirm structured views; spot-check `decode-iff <fixture>` JSON matches the panel (per 07-04 Task 3) |

> All three blocking-human checkpoints (07-02-03, 07-03-03, 07-04-03) are the documented
> Tier-4 manual residual (TEST-04). They verify AFTER the auto-verifiable build/grep/golden
> work in tasks 1-2 of each plan is green — they do not substitute for automation. The
> framework decode logic underneath every UI view IS headlessly covered by the CLI golden
> lane, so the manual residual is confined to the WinForms host presentation, which has no
> headless harness.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or are documented Tier-4 manual residual (the three blocking-human live-SWG smokes; framework logic under them is golden-tested via the CLI lane)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify — every framework task (01-01, 01-02, 04-01, 04-02) has a `dotnet test` command; every TJT-host task (02-01, 02-02, 03-01, 03-02) has the Release/x86 build gate; only the per-plan trailing checkpoint is manual
- [x] Wave 0 covers all MISSING references (fixtures + framework timing above)
- [x] No watch-mode flags (all commands are single-shot `dotnet test` / `msbuild`)
- [x] Feedback latency < 30 s for the filtered CLI lane
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved
