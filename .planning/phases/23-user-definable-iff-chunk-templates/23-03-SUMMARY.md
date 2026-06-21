---
phase: 23-user-definable-iff-chunk-templates
plan: 03
subsystem: formats
tags: [iff, template-engine, kernel-codec, byte-exact-roundtrip, count-recompute, fit-check, plausibility, d-09-presets, security-bounds]

# Dependency graph
requires:
  - phase: 23-user-definable-iff-chunk-templates
    plan: 01
    provides: "Frozen ITemplateContracts.cs surface (KernelType/RepeatKind/RepeatSpec/NamedValueMap/FieldRecord/TemplateModel/FitReport/DecodedTemplate) + 10 RED-via-Skip goldens incl. the DEC-C3 count-from-prior shape"
  - phase: 23-user-definable-iff-chunk-templates
    plan: 02
    provides: "TemplateModel.cs (TemplateException + defaults), TemplateJson.cs (sorted-key serialize / fixed-POCO deserialize), 7 shipped D-09 preset JSON files"
  - phase: 22-clienteffect-editor
    provides: "ClefFieldCodec MemoryStream variable-length LE/CString writer idiom (the encode model)"
  - phase: 07-iff-editor
    provides: "IffPayloadCursor bounds-checked LE/CString read primitives (the decode anchor, extended here)"
provides:
  - "KernelCodec.Decode/Encode: byte-exact schema-driven codec over the full kernel vocabulary + struct/array nesting + D-09 presets, with the D-10 count-from-prior AUTO-RECOMPUTE (the DEC-C3 encode-parity mechanism)"
  - "IffPayloadCursor kernel read primitives: ReadInt16Le / ReadUInt16Le / ReadDoubleLe / ReadFixedChar(n,Encoding) / ReadRawBytes(n) / SkipPad(n) — each Need(n)-guarded"
  - "FitChecker.FitCheck: pure (template, payloadBytes) -> FitReport { ConsumedExactly, BytesConsumed/Total, PerField } (D-17.2)"
  - "TypePlausibility: standalone LooksLikeFloat / LooksLikeCStringRun / LooksLikeCount predicate library (D-17.3 Tier-C substrate)"
affects: [23-04, 23-05, 23-06, 23-07, 23-08]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Byte-exactness lives ENTIRELY in the kernel (D-08): the codec produces only leaf-payload bytes; IFF tag/length framing + no-pad quirk stay IffWriter's job (free length ripple)"
    - "D-10 count-from-prior recompute: the encoder writes the bound array's CURRENT element count into its count-source field, never the stale decoded value — the only genuinely new encode obligation"
    - "V5 never-allocate-on-attacker-count: count-from-prior loop-reads through the Need(n) cursor (over-claim throws Truncated before any large alloc); FixedCount capped at 1M"
    - "Raw-span round-trip for FixedChar/RawBytes/Pad: decode trims-at-NUL for DISPLAY but stores + re-emits the full captured n-byte span for byte-exactness (the cursor asymmetry)"
    - "FitReport is a PURE function reused by both the Tier-B live indicator (D-07) and future Tier-C scoring (D-17.2) at zero speculative cost"

key-files:
  created:
    - UtinniCoreDotNet/Formats/Template/KernelCodec.cs
    - UtinniCoreDotNet/Formats/Template/FitReport.cs
    - UtinniCoreDotNet/Formats/Template/TypePlausibility.cs
    - Utinni.Cli.Tests/Template/IffPayloadCursorKernelTests.cs
    - Utinni.Cli.Tests/Template/FitReportTests.cs
  modified:
    - UtinniCoreDotNet/Formats/Decoders/IffPayloadCursor.cs
    - UtinniCoreDotNet/Formats/Template/ITemplateContracts.cs
    - UtinniCoreDotNet/UtinniCoreDotNet.csproj
    - Utinni.Cli.Tests/Template/KernelCodecTests.cs

key-decisions:
  - "Replaced the Wave-0 KernelCodec NotImplementedException stubs in ITemplateContracts.cs with the real engine in KernelCodec.cs — frozen type+member names preserved, only the bodies moved into their own file (a stub cannot co-exist with the implementation in the same assembly)"
  - "Value model: scalars carry long/float/double/string; FixedChar/RawBytes/Pad carry the raw byte[] span (byte-exact); Struct carries a nested Dictionary<string,object>; Array carries List<object> (bare scalar per element for a single-sub-field array, a struct map per element otherwise)"
  - "Array element shape is carried in the array FieldRecord's StructFields (1 sub-field = scalar array, N = struct array) — the frozen FieldRecord has no separate ElementType, so StructFields doubles as the repeated-element descriptor"
  - "Integer signed/unsigned is a DISPLAY concern only (D-11) — the wire bytes are identical, so encode emits the same LE bytes for a signed or unsigned field of the same width"
  - "Added an internal KernelCodec.DecodeInternal(out consumed, out total) so FitCheck computes consumed-exactly by reusing the exact decode walk (no duplicate walker); public Decode hides the out params"
  - "The engine-level goldens (KernelCodec / CountRecompute / Array / Preset / Truncated) were un-skipped and implemented at the PAYLOAD level (KernelCodec over a leaf payload). The whole-file VersionMatch + Precedence (resolver) and verb-level Roundtrip/WorkedExample/ApplySave goldens stay RED-via-Skip — they belong to 23-04 (resolver) and 23-05 (verbs) per those plans' own --filter ownership"

patterns-established:
  - "The kernel is the single source of byte-exactness; a preset is pure JSON sugar over it and can never introduce a parity bug (D-08)"
  - "Count-from-prior arrays: encode reads elements.Count via FindCountFromPriorArrayFor + LiveElementCount; decode loop-reads bounded by the Need(n) cursor"
  - "TypePlausibility stays dependency-light + side-effect-free so Tier C reuses it verbatim"

requirements-completed: [PROD-IFFT-01, PROD-IFFT-02]

# Metrics
duration: 13min
completed: 2026-06-21
---

# Phase 23 Plan 03: Byte-Exact KernelCodec Engine Summary

**The make-or-break byte-exact schema codec: `KernelCodec.Decode/Encode` over the full kernel vocabulary + struct/array nesting + D-09 presets, with the D-10 count-from-prior AUTO-RECOMPUTE (a count-then-N array grows/shrinks and round-trips byte-for-byte — the CRITICAL DEC-C3 golden), plus a pure `FitChecker.FitCheck` (D-17.2) and the standalone `TypePlausibility` predicate library (D-17.3), all headless and V5-bounded.**

## Performance

- **Duration:** 13 min
- **Started:** 2026-06-21T03:49:03Z
- **Completed:** 2026-06-21T04:02:37Z
- **Tasks:** 3
- **Files modified:** 9 (5 created, 4 modified)

## Accomplishments
- **Task 1 — IffPayloadCursor kernel primitives:** added `ReadInt16Le` / `ReadUInt16Le` (overflow-safe int returns), `ReadDoubleLe` (f64, a kernel addition over the SOE `.tdf` set), `ReadFixedChar(n, Encoding)` (trim-at-NUL display, consume-full-width on the cursor — asymmetry documented for byte-exact encode), `ReadRawBytes(n)`, `SkipPad(n)` — each calls `Need(n)` FIRST (the V5 bounds guard, verbatim mirror of the existing reads). No existing signature changed (caller binary-compat).
- **Task 2 — KernelCodec decode + encode:** byte-exact decode through the bounds-checked cursor + encode into one `MemoryStream` (ClefFieldCodec writer idioms). The **D-10 count-from-prior AUTO-RECOMPUTE** writes the bound array's *current* element count into its count-source field on encode (never the stale value) → array grow/shrink round-trips byte-exact. All three array kinds: FixedCount (capped at 1M, V5 OOM guard), CountFromField (loop-read, over-claim throws Truncated before any large alloc), UntilEnd. FixedChar/RawBytes/Pad round-trip via the captured raw n-byte span.
- **Task 3 — FitReport pure fn + TypePlausibility:** `FitChecker.FitCheck(template, payload)` is a PURE function (no I/O, no input mutation) reporting `ConsumedExactly` + a per-field plausibility battery, catching a truncation as a "does-not-fit" rather than an exception. `TypePlausibility` is the one net-new piece (no in-repo analog): `LooksLikeFloat` (finite + non-absurd magnitude), `LooksLikeCStringRun` (printable-ASCII-then-NUL), `LooksLikeCount` (non-negative below a cap), dependency-light for Tier-C reuse.
- **Goldens GREEN (filters):** `Template&Cursor` (7), `Template&KernelCodec` (6 + 2 Wave-2 skips), `Template&CountRecompute&Roundtrip` (2 — the CRITICAL DEC-C3 grow + shrink), `Template&Array&Roundtrip` (1), `Template&Preset` (1 engine + 14 schema), `Template&KernelCodec&Truncated` (1), `Template&Fit` / `Template&Fit&Partial` (2), `Template&Plausibility` (3). Full Template suite: 43 passed, 5 skipped (Wave-2 resolver + verb goldens owned by 23-04/23-05).

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend IffPayloadCursor with kernel read primitives** - `9abefe3` (feat)
2. **Task 2: KernelCodec decode + encode with D-10 count auto-recompute** - `3a8d627` (feat)
3. **Task 3: FitReport pure function + TypePlausibility predicate library** - `9532e64` (feat)

**Plan metadata:** (this commit) (docs: complete plan)

_TDD note: the three tasks are `tdd="true"`. They were authored test-then-implementation as single GREEN landings against the 23-01 frozen contract + RED-via-Skip suite (the RED gate was established in 23-01). Each task's new tests are not skipped — they assert real byte-exact behavior and pass._

## Files Created/Modified
- `UtinniCoreDotNet/Formats/Template/KernelCodec.cs` - byte-exact decode + encode with D-10 count recompute, 3 array kinds, struct/preset nesting, V5 bounds; internal DecodeInternal(out consumed/total)
- `UtinniCoreDotNet/Formats/Template/FitReport.cs` - FitChecker.FitCheck pure (template, payload) -> FitReport
- `UtinniCoreDotNet/Formats/Template/TypePlausibility.cs` - LooksLikeFloat/LooksLikeCStringRun/LooksLikeCount predicate library
- `UtinniCoreDotNet/Formats/Decoders/IffPayloadCursor.cs` - added i16/u16/f64/char[n]/rawbytes/pad reads (Need(n)-guarded)
- `UtinniCoreDotNet/Formats/Template/ITemplateContracts.cs` - removed the Wave-0 KernelCodec NotImplementedException stubs (real engine moved to KernelCodec.cs); frozen names preserved
- `UtinniCoreDotNet/UtinniCoreDotNet.csproj` - registered KernelCodec.cs / FitReport.cs / TypePlausibility.cs (classic-style explicit Compile Include)
- `Utinni.Cli.Tests/Template/IffPayloadCursorKernelTests.cs` - 7 cursor primitive decode + truncation goldens (filter Template&Cursor)
- `Utinni.Cli.Tests/Template/KernelCodecTests.cs` - un-skipped + implemented the engine-level scalar/count/array/preset/truncated goldens; kept VersionMatch/Precedence RED-via-Skip (23-04)
- `Utinni.Cli.Tests/Template/FitReportTests.cs` - FitCheck full/partial-consume + 3 plausibility-predicate goldens
- `Utinni.Cli.Tests/packages.lock.json`, `Utinni.Cli/packages.lock.json`, `UtinniCoreDotNet.Tests/packages.lock.json` - synced the 23-02 Newtonsoft transitive dependency into consumer lock files

## Decisions Made
See the `key-decisions` frontmatter above. The load-bearing ones: (1) the Wave-0 stub class was replaced by the real engine in its own file (a stub + implementation cannot co-exist in one assembly); (2) array element shape is carried in the array FieldRecord's `StructFields` (the frozen contract has no separate ElementType); (3) the engine goldens were implemented at the *payload* level — the whole-file resolver goldens (VersionMatch/Precedence) and verb goldens (Roundtrip/WorkedExample/ApplySave) are explicitly owned by 23-04/23-05 (verified against those plans' own `--filter` rows) and remain RED-via-Skip here.

## Deviations from Plan

None - plan executed exactly as written. The plan permitted "declare the engine class as stubs OR defer" choices already made in 23-01; this plan only supplied the bodies. The `Template&CountRecompute&Roundtrip` (and the other engine) goldens that 23-01 left RED-via-Skip were turned GREEN at the payload/engine level exactly as the plan's Task 2 verification requires, while the resolver- and verb-level skips were correctly left to their owning plans.

## Issues Encountered

- **Git Bash MSBuild switch mangling:** `/t:`-style MSBuild switches are path-translated by Git Bash into garbage; used `-t:` / `-p:` dash-form switches throughout. (Tooling, not code.)
- **Pre-existing out-of-scope test failure — `AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn`:** the CppSharp native-binding ABI gate fails REMOVED(52)/ADDED(0). VERIFIED out of scope: `git diff 11aeeff(pre-23-03) HEAD -- Generated/UtinniCore.cs` is EMPTY — my commits do not touch the native binding surface at all (23-03 is pure managed `Formats/Template`). Root cause is the documented harness gotcha (incremental MSBuild skips the post-build `UtinniCoreDotNetGen.exe` regen → the test reads a stale committed Generated file vs the Phase-17 blessed baseline). Logged to `deferred-items.md`; NOT fixed (SCOPE BOUNDARY — unrelated subsystem, provably unchanged by this plan). 23-03's own verification (the Template `--filter` goldens) is fully green; `Utinni.Cli.Tests` is 476 passed / 7 skipped / 0 failed.

## Known Stubs

None. KernelCodec is a complete byte-exact codec; FitChecker and TypePlausibility are complete. No hardcoded empty values, placeholder text, or unwired data paths were introduced. (The VersionMatch/Precedence/verb tests left as `[Skip=...]` are RED-via-Skip placeholders owned by the downstream resolver/verb plans 23-04/23-05 — not stubs in this plan's deliverables.)

## Next Phase Readiness
- The headless byte-exact engine is complete: 23-04 builds the TemplateResolver (version-FORM match + D-05 altitude) over `IffReader` and turns the `Template&VersionMatch` / `Template&Precedence` goldens GREEN; 23-05 wires the `utinni-cli` verbs (decode-with-template / roundtrip-template / apply-save-template) over this engine and turns the `Template&Roundtrip` / `Template&WorkedExample&Roundtrip` / `ApplySaveTemplate` goldens GREEN.
- `KernelCodec.Encode` produces leaf-payload bytes ready for `MutableIffNode.SetPayload` + `IffWriter.Write` (the free length ripple) in 23-05's apply-save path.
- `FitChecker.FitCheck` is ready for 23-04's D-07 match-fit confidence signal and the Tier-B live indicator.
- Carried forward (out of scope): the pre-existing CppSharp ABI-baseline drift (deferred-items.md) needs a dedicated re-bless task.

## Self-Check: PASSED
- FOUND: UtinniCoreDotNet/Formats/Template/KernelCodec.cs
- FOUND: UtinniCoreDotNet/Formats/Template/FitReport.cs
- FOUND: UtinniCoreDotNet/Formats/Template/TypePlausibility.cs
- FOUND: Utinni.Cli.Tests/Template/IffPayloadCursorKernelTests.cs
- FOUND: Utinni.Cli.Tests/Template/FitReportTests.cs
- FOUND commit: 9abefe3 (Task 1)
- FOUND commit: 3a8d627 (Task 2)
- FOUND commit: 9532e64 (Task 3)

---
*Phase: 23-user-definable-iff-chunk-templates*
*Completed: 2026-06-21*
