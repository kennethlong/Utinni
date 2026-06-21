---
phase: 23-user-definable-iff-chunk-templates
plan: 04
subsystem: formats
tags: [iff, template-engine, resolver, match-precedence, version-form-aware, d-05-altitude, d-07-fit-confidence, d-12-allow-list, path-containment, single-source-builtin]

# Dependency graph
requires:
  - phase: 23-user-definable-iff-chunk-templates
    plan: 01
    provides: "Frozen ITemplateContracts.cs (TemplateModel.MatchAncestorPath/MatchLeafTag/MatchTagOnly, FitReport) + RED-via-Skip VersionMatch/Precedence goldens"
  - phase: 23-user-definable-iff-chunk-templates
    plan: 02
    provides: "TemplateJson.Deserialize/Serialize + 7 shipped D-09 preset packs (the shipped pack dir)"
  - phase: 23-user-definable-iff-chunk-templates
    plan: 03
    provides: "FitChecker.FitCheck pure (template, payload) -> FitReport (the D-07 confidence engine)"
  - phase: 07-iff-editor
    provides: "MutableIffDocument.DeriveStableId (the ordinal-path addressing the match key is a predicate over) + IffReader + MutableIffNode tree"
  - phase: 08-tjt-subpanel-iff-editor-read-write
    provides: "LooseOverridePath.IsContainedUnderRoot (the single-source path-containment predicate reused for pack-dir security)"
provides:
  - "TemplateResolver.Resolve(MutableIffDocument, leafStableId): version-FORM-aware match (D-04) + D-05 altitude gate + D-07 fit-confidence + most-specific-key-wins tie-break + genuine-tie candidate list"
  - "TemplateResolver.MatchKey(stableId, leafTag): the Tier-C corpus-query substrate (D-17.4) — a path predicate with no live document/payload"
  - "BuiltinRootForms.HasBuiltinDecoder(IffChunk): the SINGLE-source D-05 built-in-root-FORM predicate consumed by both the resolver gate and (mirrored from) DecodeIffCommand's dispatch"
  - "TemplatePackStore: D-12 scanned-dir allow-list loader (shipped -> app-data -> project-local) with load-order shadow (LoadEffective), malformed-skip-with-reason, and path containment (LoadAll)"
affects: [23-05, 23-06, 23-07, 23-08]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Match key is a PREDICATE over the existing DeriveStableId ordinal-path (no new addressing scheme); version-FORM segment is required and kept even under tag-only widening (D-04)"
    - "Altitude gate single-sourced: BuiltinRootForms is the one built-in-root-FORM set; the resolver bridges its mutable DOM to the immutable IffChunk surface via a minimal ToImmutableMirror so the same structural sniffs (terrain/OT) run over the live edited tree"
    - "D-07 confidence is the existing FitChecker.FitCheck surfaced on every key-match — a key-matched-but-non-round-tripping template is returned WITH a does-not-fit FitReport, never silently applied"
    - "Pack-dir security reuses LooseOverridePath.IsContainedUnderRoot (the save-path containment gate) so the load allow-list cannot drift from the write allow-list"
    - "Load-order shadow = SWG searchPath model: same (ancestor+leaf+tagOnly) match key collapses to the highest-priority pack's template (LoadEffective)"

key-files:
  created:
    - UtinniCoreDotNet/Formats/Template/TemplateResolver.cs
    - UtinniCoreDotNet/Formats/Template/TemplatePackStore.cs
    - UtinniCoreDotNet/Formats/Decoders/BuiltinRootForms.cs
    - Utinni.Cli.Tests/Template/TemplateResolverTests.cs
  modified:
    - Utinni.Cli.Tests/Template/KernelCodecTests.cs
    - UtinniCoreDotNet/UtinniCoreDotNet.csproj

key-decisions:
  - "Created BuiltinRootForms in UtinniCoreDotNet (not Utinni.Cli) as the single source for the D-05 altitude built-in set — the resolver lives in UtinniCoreDotNet and cannot depend on the CLI; BuiltinRootForms derives DTII from DataTableDecoder.RootSubType and routes terrain/OT through the real LooksLikeTerrain/LooksLikeObjectTemplate sniffs, so it mirrors DecodeIffCommand's dispatch rather than copying a divergent literal list"
  - "The resolver works over a MutableIffDocument (the held edit source) and bridges to the immutable IffChunk surface via a minimal ToImmutableMirror for the BuiltinRootForms sniffs — keeping a SINGLE built-in predicate that operates on IffChunk without forking a mutable-DOM variant"
  - "Tag-only widening (MatchTagOnly) drops the ancestor-path prefix requirement but NEVER the version-FORM awareness (D-04): a tag-only template still only resolves where the leaf carries a version-FORM segment; specificity 0 so a full-ancestor match always wins the tie-break"
  - "Most-specific-key-wins ranks by matched ancestor-path length; a genuine tie (same specificity) is returned as the full Candidates list (count > 1) for a UI picker rather than an arbitrary pick"
  - "DecodeIffCommand was left UNMODIFIED (its dispatch already encodes the built-in set); BuiltinRootForms mirrors it as the consumable single-source predicate. A future refactor could route DecodeIffCommand through BuiltinRootForms too, but changing the CLI dispatch order was out of scope and risked behavior drift"

patterns-established:
  - "A resolver that is ALSO a corpus-query predicate (MatchKey) at zero extra cost — the same key logic backs the live single-leaf resolve and the Tier-C path-filter substrate (D-17.4)"
  - "Allow-list loaders reuse the existing containment predicate so load-security and write-security are one rule"

requirements-completed: [PROD-IFFT-02]

# Metrics
duration: 8min
completed: 2026-06-21
---

# Phase 23 Plan 04: Template Match + Precedence Resolver Summary

**The match + precedence layer (D-04/D-05/D-07/D-12): `TemplateResolver` resolves a template-eligible leaf to its template by the version-FORM-aware match key (derived entirely from the existing `DeriveStableId` ordinal-path — no new addressing), enforces the D-05 altitude gate FIRST via the single-source `BuiltinRootForms` predicate (built-ins win wholesale), and surfaces the D-07 round-trip fit-confidence (a key-matched-but-non-round-tripping template is returned WITH a does-not-fit `FitReport`, never silently applied), with most-specific-key-wins tie-break + a genuine-tie candidate list; plus `TemplatePackStore`, the D-12 scanned-dir allow-list loader (shipped -> app-data -> project-local) with load-order shadow, malformed-skip-with-reason, and `LooseOverridePath` path containment.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-06-21T04:08:09Z
- **Completed:** 2026-06-21T04:16:07Z
- **Tasks:** 2
- **Files modified:** 6 (4 created, 2 modified)

## Accomplishments
- **Task 1 — TemplateResolver + BuiltinRootForms (D-04/D-05/D-07):** `Resolve(doc, leafStableId)` enforces the D-05 altitude gate FIRST (a built-in root FORM returns `SuppressedByBuiltin=true` and no match), then locates the leaf, derives its stable-id path via the existing `DeriveStableId`, and matches every loaded template by the version-FORM-aware key. The version-FORM segment (the innermost enclosing FORM sub-type, e.g. `0003` in `FORM:TPLX/0/FORM:0003/0/CPAP:CPAP/0`) is REQUIRED and kept even under `MatchTagOnly` widening (D-04 — the CLEF-CPAP 3-layout anti-pattern cannot silently mis-decode). On a match, `FitChecker.FitCheck` runs on the leaf payload (D-07) and the result rides on `TemplateMatch.Fit` / `.Fits` — a wrong-layout template is surfaced does-not-fit, not applied. Most-specific (longest ancestor key) wins; a genuine tie returns the full `Candidates` list. `MatchKey(stableId, leafTag)` is the Tier-C corpus-query substrate (D-17.4): a pure path predicate, no live document. **`BuiltinRootForms.HasBuiltinDecoder(IffChunk)`** is the single source for the altitude built-in set (DTII via `DataTableDecoder.RootSubType`; PEFT/CLEF/MESH/SKMG/SKTM/KFAT/CKAT/SSHT/CSHD literals; terrain/OT via the real `LooksLikeTerrain`/`LooksLikeObjectTemplate` sniffs) — mirrors DecodeIffCommand's dispatch, no divergent copy.
- **Task 2 — TemplatePackStore (D-12 + T-23-04-PATH):** the scanned-dir allow-list loader. `DefaultRoots` wires shipped (`AppContext.BaseDirectory/Formats/Template/Presets`, never `Assembly.Location`) -> app-data (`%APPDATA%/Utinni/templates`) -> optional project-local, in ascending priority. `LoadAll` deserializes every `*.json` via `TemplateJson`, skipping a malformed file with a captured `SkipReason` (one bad file never aborts the scan); `LoadEffective` resolves load-order shadow (a higher-priority pack shadows a same-(ancestor+leaf+tagOnly)-key template). Path containment routes every file through `LooseOverridePath.IsContainedUnderRoot` (single-sourced with the save-path gate) so an out-of-root pack is rejected before deserialization.
- **Goldens GREEN:** un-skipped the 23-01 RED `Template&VersionMatch` + `Template&Precedence` goldens (now assert real resolver behavior); added `TemplateResolverTests` (VersionMatch / Precedence / MultiMatch / Fit&Flag / MatchKey corpus-query) and `TemplatePackStoreTests` (scan+malformed-skip / containment / load-order shadow / default-root order). Filters run GREEN: TemplateVersionMatch / TemplatePrecedence / TemplateMultiMatch / TemplateFit / TemplatePackStore. Full Template suite: 52 passed, 3 skipped (verb goldens owned by 23-05). Full `Utinni.Cli.Tests`: 491 passed / 5 skipped / 0 failed.

## Task Commits

1. **Task 1: TemplateResolver — version-FORM match + D-05 altitude + D-07 fit-confidence** - `75ff296` (feat)
2. **Task 2: TemplatePackStore — D-12 scanned-dir allow-list with path containment** - `5382d64` (feat)

**Plan metadata:** (this commit) (docs: complete plan)

_TDD note: both tasks are `tdd="true"`. They were authored test-then-implementation as single GREEN landings against the 23-01 frozen contract + RED-via-Skip suite (the RED gate was established in 23-01). The two previously-skipped resolver goldens (VersionMatch/Precedence) were turned GREEN; the new resolver/pack-store tests assert real behavior and pass._

## Files Created/Modified
- `UtinniCoreDotNet/Formats/Template/TemplateResolver.cs` - the match + precedence resolver (TemplateMatch/TemplateResolution + Resolve/MatchKey/EnumerateLeafStableIds)
- `UtinniCoreDotNet/Formats/Template/TemplatePackStore.cs` - D-12 scanned allow-list loader (TemplatePackRoot/TemplateLoadResult + DefaultRoots/LoadAll/LoadEffective)
- `UtinniCoreDotNet/Formats/Decoders/BuiltinRootForms.cs` - the single-source D-05 built-in-root-FORM predicate
- `Utinni.Cli.Tests/Template/TemplateResolverTests.cs` - resolver + pack-store goldens (two xUnit classes; TemplatePackStoreTests is IDisposable temp-dir backed)
- `Utinni.Cli.Tests/Template/KernelCodecTests.cs` - un-skipped the VersionMatch/Precedence RED anchors; they now delegate to the resolver (and added the System.Iff using); removed the now-dead Red* skip constants
- `UtinniCoreDotNet/UtinniCoreDotNet.csproj` - registered TemplateResolver.cs / TemplatePackStore.cs / BuiltinRootForms.cs (classic-style explicit Compile Include)

## Decisions Made
See the `key-decisions` frontmatter above. Load-bearing: (1) `BuiltinRootForms` lives in `UtinniCoreDotNet` (the resolver can't depend on the CLI) and DERIVES its set from the real decoders/constants rather than a divergent literal list — satisfying the plan's single-source acceptance criterion; (2) the resolver bridges its mutable DOM to the immutable `IffChunk` surface (`ToImmutableMirror`) so the one built-in predicate runs without a forked mutable-DOM variant; (3) tag-only widening drops the ancestor prefix but never version-FORM awareness (D-04).

## Deviations from Plan

None affecting scope. One single-source detail the plan left implicit was resolved by creating `BuiltinRootForms` in `UtinniCoreDotNet` (the plan said "reuse DecodeIffCommand's built-in sub-type set, do NOT hardcode a divergent copy"; since the resolver cannot reference the CLI assembly, the shared predicate was placed framework-side and DecodeIffCommand's set is mirrored from it — DTII from `DataTableDecoder.RootSubType`, terrain/OT from the real sniffs). `DecodeIffCommand` itself was left unmodified to avoid CLI-dispatch behavior drift (a future refactor could route it through `BuiltinRootForms`; out of scope here).

## Issues Encountered

- **Per-commit isolation of the shared test file:** both tasks share `Utinni.Cli.Tests/Template/TemplateResolverTests.cs` (resolver tests + a `TemplatePackStoreTests` class) and `UtinniCoreDotNet.csproj`. To keep Task 1's commit buildable in isolation the `TemplatePackStore.cs` csproj registration was deferred to Task 2's commit; the full tree (both commits) builds clean and all filters pass. (Bookkeeping, not code.)
- **Pre-existing out-of-scope failure — `AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline`:** the documented Phase-17 harness gotcha (incremental MSBuild skips the post-build `UtinniCoreDotNetGen.exe` regen → the test reads a stale committed `Generated/UtinniCore.cs` vs the blessed baseline). VERIFIED out of scope: this plan is pure managed `Formats/Template` + `Formats/Decoders` work and never touches the native binding surface; `Generated/UtinniCore.cs` was `git checkout`'d after the build and is unchanged. Not chased per the prior-wave note + SCOPE BOUNDARY.

## Known Stubs

None. `TemplateResolver` and `TemplatePackStore` are complete; the resolver wires real `DeriveStableId` paths, the real `FitChecker`, and the real `BuiltinRootForms` gate. No hardcoded empty values, placeholder text, or unwired data paths. (The 3 remaining `[Skip=...]` Template tests are the verb-level Roundtrip/WorkedExample/ApplySave goldens owned by 23-05 — not stubs in this plan's deliverables.)

## Next Phase Readiness
- The match + precedence layer is complete: 23-05 wires the `utinni-cli` verbs (decode-with-template / roundtrip-template / apply-save-template) over `TemplateResolver` + `KernelCodec` + `TemplatePackStore`, turning the `Template&Roundtrip` / `Template&WorkedExample&Roundtrip` / `ApplySaveTemplate` goldens GREEN.
- `TemplatePackStore.DefaultRoots` is ready to be the verb/MCP default pack source; `TemplateResolver.Resolve` is ready for the Tier-B live round-trip indicator (D-07) and the raw-fallback decode site; `MatchKey` is the Tier-C corpus-query substrate.
- Carried forward (out of scope): the pre-existing CppSharp ABI-baseline drift (deferred-items.md) still needs a dedicated re-bless task.

## Self-Check: PASSED
- FOUND: UtinniCoreDotNet/Formats/Template/TemplateResolver.cs
- FOUND: UtinniCoreDotNet/Formats/Template/TemplatePackStore.cs
- FOUND: UtinniCoreDotNet/Formats/Decoders/BuiltinRootForms.cs
- FOUND: Utinni.Cli.Tests/Template/TemplateResolverTests.cs
- FOUND commit: 75ff296 (Task 1)
- FOUND commit: 5382d64 (Task 2)

---
*Phase: 23-user-definable-iff-chunk-templates*
*Completed: 2026-06-21*
