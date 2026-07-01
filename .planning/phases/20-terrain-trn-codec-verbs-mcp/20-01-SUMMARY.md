---
phase: 20-terrain-trn-codec-verbs-mcp
plan: 01
subsystem: terrain-trn-codec
tags: [terrain, trn, tgen, fixtures, era-versions, wave-0, dogfood]
requires:
  - "utinni-cli parse-tre + inspect-iff verbs (Phases 12/13) — used to dogfood the real-asset observation"
  - "IffReader / IffWriter / MutableIffDocument IFF DOM (the synthesizer emit primitive)"
provides:
  - "TgenEraVersions: per-tag FORM-version constants PINNED to observed SWGEmu + SWG Infinity .trn assets (low/high era maps)"
  - "TgenFixtureSynthesizer: ≤200-byte FORM TGEN both-lineage fixture matrix (every Tier-1 tag low+high)"
  - "Four Skip-marked terrain test classes collected by xUnit (TgenDecoder/RoundtripTrn/ApplySaveTrn/TerrainReadTool)"
  - "Test-only verb-ceiling registration assertion (D-11 smoke, zero Program.cs churn)"
  - "Grounded real-asset version observation (SWGEmu == Infinity for all observed tags; BREC is the one drift pair)"
affects:
  - "Plans 20-02/03/04 MUST read TgenEraVersions.InfinityEra (NOT RestorationEra — relabeled)"
  - "DEC-C3 authoritative pin still lands at Plan 04 Task 3; this is the Wave-0 grounding"
tech-stack:
  added: []
  patterns:
    - "Dogfood real-asset observation via parse-tre (locate) + zlib-extract (offsets) + inspect-iff (FORM-version dump)"
key-files:
  created: []
  modified:
    - "Utinni.Cli.Tests/Fixtures/trn/TgenEraVersions.cs (pinned to observed values; Restoration→Infinity relabel)"
    - "Utinni.Cli.Tests/Fixtures/trn/TgenFixtureSynthesizer.cs (Restoration→Infinity relabel in docs/comments)"
    - "Utinni.Cli.Tests/Terrain/RoundtripTrnTests.cs (Restoration→Infinity relabel in a TODO comment)"
decisions:
  - "Maintainer directive: validate against SWGEmu + SWG Infinity, NOT Restoration → relabel RestorationEra → InfinityEra"
  - "Real client assets stay OUT of the committed corpus (D-14); observation recorded here + in a non-committed scratch note"
requirements-completed: []   # Wave-0 scaffold (fixtures/era-versions/skip-stubs); no requirement completed. Field added 2026-06-30 (v2.1 audit hygiene)
metrics:
  duration: "~5h15m wall (Tasks 1-2 prior commit 07:46 → Task 3 + finalize ~13:00 UTC)"
  completed: "2026-06-16"
  tasks_completed: 3
  files_modified: 3
---

# Phase 20 Plan 01: TGEN Both-Lineage Fixture Scaffold + Real-Asset Version Grounding Summary

The Wave-0 Nyquist scaffold for the terrain `.trn` codec: a complete per-Tier-1-tag low+high
synthesized `FORM TGEN` fixture matrix, pinnable era constants, four collected-but-Skipped test
classes, and a test-only verb-ceiling assertion — now GROUNDED in real client assets. Tasks 1 & 2
landed in prior commit `4a2c9bb`; this continuation completed Task 3 (the maintainer-checkpointed
real-asset version observation) by dogfooding `utinni-cli`, pinned `TgenEraVersions` to the observed
SWGEmu + SWG Infinity values, and relabeled the high-era lineage from "Restoration" to "Infinity"
per the maintainer directive.

## What Was Built

- **Tasks 1 & 2 (prior commit `4a2c9bb`, verified present):** `TgenFixtureSynthesizer` (IffWriter-based
  ≤200-byte FORM TGEN builder covering every Tier-1 tag at low+high version plus minimal/unknown/DEAD/
  truncated/compositional fixtures with a non-Skipped self-test); `TgenEraVersions` pinnable era maps;
  four Skip-marked xUnit terrain classes incl. the concern-#17 negative battery; and the D-11 verb-ceiling
  smoke as a TEST-ONLY `[Verb("trn-smoke")]` registration assertion (no shipped no-op verb).
- **Task 3 (this continuation):** Real-asset FORM-version observation via dogfooding, then a pin + relabel
  of the era constants to the observed values.

## Task 3 — Real-Asset Version Observation (dogfood, D-13/D-14)

Done entirely through Utinni's own shipped verbs (no terrain decoder exists yet — that's Wave 1):
1. `utinni-cli parse-tre` located `terrain/*.trn` records + their TRE payload offsets/sizes.
2. The records were zlib-extracted to a **non-committed scratch dir** (real assets stay OUT of git — D-14).
3. `utinni-cli inspect-iff` dumped the structural FORM tree; the per-tag version sub-FORMs were read out.

**Clients observed (per maintainer directive "use SWG Infinity and SWGEmu, no Restoration"):**
- **SWGEmu** — `E:\SWGEmu-Client\SWGEmu\patch_00.tre` (TRE v5000, NOT encrypted): tutorial (990 B minimal),
  tatooine, naboo, corellia, dathomir (full planets).
- **SWG Infinity** — `D:\SWG Infinity\SWG Infinity\Live\mtg_planets.tre` (TRE v5000, NOT encrypted):
  taanab, mustafar (Infinity custom planets).

Neither client's `.trn` payload was v6000+/encrypted — both decoded cleanly via the revived TRE reader.
**The concern-#15 "encrypted/unreachable" branch did NOT trigger** (so no "Infinity stays assumed" caveat
was needed for the structural format; see AFCN note below for the one genuinely-unobserved tag).

**Root format (both lineages):** `FORM PTAT` version `0014` (ProceduralTerrainAppearanceTemplate) wrapping
the `FORM TGEN` version `0000` graph. (The plan/research wrote "FORM TGEN"; the real `.trn` root is PTAT/0014
with TGEN nested inside — the synthesizer correctly emits the TGEN subtree.)

### Observed vs. previously-ASSUMED per-tag FORM versions

| Tag  | OBSERVED SWGEmu | OBSERVED Infinity | Prior ASSUMED (low/high) | Disposition |
|------|-----------------|-------------------|--------------------------|-------------|
| TGEN | 0000 | 0000 | 0000/0000 | match |
| LAYR | 0003 | 0003 | 0000/0004 | **corrected → 0003** |
| SGRP | 0006 | 0006 | 0000/0006 | both 0006 |
| FGRP | 0008 | 0008 | 0001/0008 | both 0008 |
| RGRP | 0003 | 0003 | 0000/0004 | **corrected → 0003** |
| EGRP | 0002 | 0002 | 0000/0002 | both 0002 |
| MGRP | 0000 | 0000 | 0000/0000 | match |
| AHCN | 0000 | 0000 | 0000/0001 | **corrected → 0000** |
| AHTR | 0004 | 0004 | 0000/0001 | **corrected → 0004** |
| ACCN | 0000 | 0000 | 0000/0001 | **corrected → 0000** |
| ACRH | 0000 | 0000 | 0000/0001 | **corrected → 0000** |
| ASCN | 0001 | 0001 | 0000/0001 | both 0001 |
| ASRP | 0001 | 0001 | 0000/0001 | both 0001 |
| AFCN | (absent) | (absent) | 0000/0001 | **NOT observed → stays ASSUMED 0000** |
| AFSC | 0004 | 0004 | 0000/0001 | **corrected → 0004** |
| AFSN | 0004 | 0004 | 0000/0001 | **corrected → 0004** |
| BCIR | 0002 | 0002 | 0000/0001 | **corrected → 0002** |
| BREC | 0003 | 0002–0003 | 0000/0001 | **corrected; REAL drift: low 0002 / high 0003** |
| FHGT | 0002 | 0002 | 0000/0001 | **corrected → 0002** |
| FSLP | 0002 | 0002 | 0000/0001 | **corrected → 0002** |

### Key finding

**SWGEmu and SWG Infinity ship the SAME `PTAT/0014` terrain format with IDENTICAL per-tag FORM versions
for every tag observed in both.** There is no version-word lineage divergence between these two clients.
The "low vs high" arms therefore collapse to the same observed version for almost every tag. The ONE genuine
intra-corpus drift observed is **BREC (v0002 low … v0003 high)** — preserved as a real low/high pair so the
fixture matrix keeps exercising the version-divergence dispatch path. For every other Tier-1 tag the low/high
entries are now equal and documented as "no observed lineage drift."

`AFCN` did not appear in any sampled planet of either client; its value (`0000`) is kept but annotated
**still-ASSUMED** in `TgenEraVersions.cs` — Plan 04 Task 3 should confirm it or fall back to raw-only coverage.

## Lineage Relabel: Restoration → Infinity

Per the maintainer directive, the high-era lineage was renamed throughout the test code:
`TgenEraVersions.RestorationEra` → `TgenEraVersions.InfinityEra`, and all "Restoration"-labeled
doc-comments in `TgenFixtureSynthesizer.cs` + a TODO in `RoundtripTrnTests.cs` were reworded to "Infinity".
A repo-wide grep for `RestorationEra` / `Restoration-era` in `**/*.cs` now returns zero matches
(grep-gate hygiene). The format-capability support range from AGENTS.md (SWGEmu 0004/0005/0006 + newer
5000/6000/COT2000) is UNCHANGED — this relabel only grounds which real client the era constants observe.

## Downstream Notes (IMPORTANT)

- **Plans 20-02 / 20-03 / 20-04 MUST use `TgenEraVersions.InfinityEra`** (not the now-removed
  `RestorationEra`). The synthesizer's `High(tag)` already routes to `InfinityEra`.
- The authoritative **DEC-C3 version pin/confirm still lives at Plan 04 Task 3** — this Wave-0 step is the
  front-loaded grounding only. Plan 04 should additionally confirm/resolve the unobserved `AFCN`.
- **REQUIREMENTS.md PROD-W2-TRN-04** acceptance text still reads "BOTH SWGEmu and Restoration fixtures."
  Recommend the maintainer reword "Restoration" → "Infinity" there to match the validated client. NOT edited
  here (requirement-text changes are a maintainer-scope artifact; the checkbox stays unchecked because the
  full verbs+MCP requirement lands at Plan 04, not this scaffold plan).

## Deviations from Plan

### Auto-fixed / clarifications (no architectural change)

**1. [Rule 3 — Naming] Restoration → Infinity relabel driven by the maintainer checkpoint reply**
- **Found during:** Task 3 (the checkpoint the plan front-loaded).
- **Issue:** The plan + Task-1 code labeled the high lineage "Restoration" (`RestorationEra`). The maintainer
  directed validation against SWG Infinity, not Restoration.
- **Fix:** Renamed the era map + all comments/doc-refs to Infinity; re-ran the suite GREEN.
- **Files:** `TgenEraVersions.cs`, `TgenFixtureSynthesizer.cs`, `RoundtripTrnTests.cs`.

**2. [Observation] Era constants corrected to observed values; low/high collapse for non-BREC tags**
- The prior ASSUMED low/high spread (0000/0001 for most affectors) did not match reality. Real SWGEmu +
  Infinity ship the same higher versions (e.g. AHTR 0004, AFSC/AFSN 0004, BCIR 0002, FHGT/FSLP 0002,
  LAYR 0003, RGRP 0003). Constants pinned accordingly. The both-arms matrix still emits both arms per tag
  (proving the version-argument plumbing); only BREC keeps a genuinely-different low/high pair.

No architectural deviations. No new packages (threat T-20-SC: accept — pure managed, zero installs).

## Authentication Gates

None.

## Known Stubs

The four terrain test classes are intentionally Skip-marked Wave 1/2/3 stubs (the explicit purpose of this
scaffold plan, D-12). They are COLLECTED by the xUnit runner (28 Cli + 4 Mcp methods reported Skipped, not
absent), not failures. Each is wired to the production component it will exercise in its wave. Not blocking —
these are the planned scaffold, resolved by Plans 02/03/04.

## Verification

- **MSBuild** `Utinni.sln /p:Configuration=Release /p:Platform=x86` → built with no errors (PowerShell-invoked).
- `Generated/UtinniCore.cs` reverted via `git checkout --` (not committed).
- `dotnet test Utinni.Cli.Tests --no-build --filter "TgenDecode|TgenRawFallback|RoundtripTrn|ApplySaveTrn"`
  → **2 passed, 26 skipped, 0 failed** (synthesizer self-test + verb-ceiling assertion GREEN after the
  version/label edits — every Tier-1 tag still emits a low+high ≤200-byte re-parsing fixture).
- `dotnet test Utinni.Mcp.Tests --no-build --filter Terrain` → **4 skipped, 0 failed**.
- No real client assets tracked (`git ls-files | grep .trn` → none); scratch extraction stayed under a
  non-committed temp dir.

## Self-Check: PASSED

- SUMMARY.md, TgenEraVersions.cs, TgenFixtureSynthesizer.cs all present on disk.
- Prior commit `4a2c9bb` (Tasks 1-2) present in git log.
- `git grep RestorationEra -- '*.cs'` → zero matches (Restoration→Infinity relabel complete; grep-gate clean).
