---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 02
subsystem: particle-codec
tags: [PROD-W2-PRT, particle, prt, peft, codec, iff, degrade-dont-abort, byte-exact, headless]
requires:
  - IffReader.Read / IffWriter.Write (Phase 8 tree-based IFF primitives)
  - MutableIffDocument.FromDocument / MutableIffNode (Phase 8 hybrid-DOM)
  - IffPayloadCursor (little-endian, truncation-safe payload cursor)
  - DecoderException (shared structured decode-failure type)
  - "ObjectTemplate codec (structural analog: ParamValue/ParamCodec/MutableObjectTemplate/Writer)"
provides:
  - ParticleFieldValue (typed union Float/Int/Bool/Enum/WaveForm/ColorRamp/RawBytesHexFallback)
  - WaveFormCodec (FORM WVFM 0000/0001/0002 byte-exact decode/encode + raw-preserve)
  - ColorRampCodec (FORM CLRR 0000/0001 byte-exact decode/encode + raw-preserve)
  - ParticleEmitterDescription (typed EMTR walk; raw-preserve unknown version)
  - MutableParticleEffect (mutable PEFT model; EditLeafPayload; RewriteCount; DoS-guarded)
  - ParticleEffectDocument.FromBytes/FromIff (PEFT root typed-decode entry)
  - ParticleEffectWriter.Serialize (byte-exact composes IffWriter.Write)
  - ParticleParseException / ParticleParseError (UnexpectedForm/NegativeCount/CountExceedsCap)
affects:
  - UtinniCoreDotNet.csproj (8 explicit Compile items for Formats/Particle/*.cs)
tech-stack:
  added: []
  patterns: [iff-tree-walk-dont-hand-roll, little-endian-payload-cursor, degrade-dont-abort-raw-preserve, consume-exactly-or-hex, hybrid-dom-in-place-edit, machine-managed-count-rederive, division-form-count-guard, formats-provenance-header]
key-files:
  created:
    - "D:/Code/Utinni/UtinniCoreDotNet/Formats/Particle/ParticleParseException.cs"
    - "D:/Code/Utinni/UtinniCoreDotNet/Formats/Particle/ParticleFieldValue.cs"
    - "D:/Code/Utinni/UtinniCoreDotNet/Formats/Particle/WaveFormCodec.cs"
    - "D:/Code/Utinni/UtinniCoreDotNet/Formats/Particle/ColorRampCodec.cs"
    - "D:/Code/Utinni/UtinniCoreDotNet/Formats/Particle/ParticleEmitterDescription.cs"
    - "D:/Code/Utinni/UtinniCoreDotNet/Formats/Particle/MutableParticleEffect.cs"
    - "D:/Code/Utinni/UtinniCoreDotNet/Formats/Particle/ParticleEffectDocument.cs"
    - "D:/Code/Utinni/UtinniCoreDotNet/Formats/Particle/ParticleEffectWriter.cs"
    - "D:/Code/Utinni/UtinniCoreDotNet.Tests/Formats/Particle/ParticleFixtureBuilder.cs"
    - "D:/Code/Utinni/UtinniCoreDotNet.Tests/Formats/Particle/ParticleCodecTests.cs"
    - "D:/Code/Utinni/UtinniCoreDotNet.Tests/Formats/Particle/ParticleDecodeTests.cs"
    - "D:/Code/Utinni/UtinniCoreDotNet.Tests/Formats/Particle/ParticleDegradeTests.cs"
  modified:
    - "D:/Code/Utinni/UtinniCoreDotNet/UtinniCoreDotNet.csproj"
decisions:
  - "Byte-exactness is enforced by consume-exactly-or-hex: a typed leaf decode is accepted ONLY when it re-encodes to the IDENTICAL bytes; otherwise raw-preserve. This makes any layout-assumption error fail safe (raw-preserve) instead of corrupting on write."
  - "The authoritative bytes always live in the captured MutableIffDocument tree; the typed views (ParticleEmitterDescription/ParticleWaveForm/ParticleColorRamp) are a non-destructive overlay, so the byte-exact round-trip never depends on the typed walk being complete."
  - "Unit of raw-preservation is the unrecognized FORM <version> sub-tree (whole-sub-tree captured-byte re-emit), layered: unknown PEFT root, unknown EMTR version, AND unknown WVFM/CLRR leaf version each degrade independently."
  - "WaveForm 0002 swaps the on-disk randomMin/randomMax slot order vs 0000/0001; the codec reads + re-emits the four floats in on-disk order, so byte-exactness holds without disambiguating which slot the engine treats as min vs max."
  - "RewriteCount preserves trailing bytes after the leading int32 (e.g. the 4 effect-level floats in the PEFT 0002 group-count chunk), unlike MutableObjectTemplate.RewriteCount which owns the whole 4-byte leaf."
  - "Count DoS guard is division-form (count > remaining/elementSize) + absolute 16M cap + present-sub-form bound, applied BEFORE every read loop."
metrics:
  duration: ~95 min
  completed: 2026-06-07
---

# Phase 15 Plan 02: Particle (.prt / FORM PEFT) Typed Codec Summary

Built the new headless, CI-tested `.prt` / `FORM PEFT` typed codec in `UtinniCoreDotNet/Formats/Particle/` (PROD-W2-PRT, format half) — a structural clone of `Formats/ObjectTemplate/` composing the shipped tree-based `IffReader`/`IffWriter` + `IffPayloadCursor`, with full typed decode of the recurring WaveForm/ColorRamp leaves + the PEFT→EMGP→EMTR tree, byte-exact round-trip via the Phase-8 hybrid-DOM, and mandatory degrade-don't-abort raw-byte preservation (D-05) at the unrecognized-FORM-version granularity that NEVER hard-aborts the way the SOE reference does (Pitfall 2).

## What shipped

**Task 1 — recurring leaves first (commit `cfb09b0`):**
- `ParticleFieldValue` typed union: `Float/Int/Bool/Enum/WaveForm/ColorRamp/RawBytesHexFallback`, with `FromRawBytes` capturing the verbatim original bytes (defensive copy on get + set).
- `WaveFormCodec` (FORM WVFM, 3 versions) + `ColorRampCodec` (FORM CLRR, 2 versions): all payload reads via `IffPayloadCursor` (little-endian, Pitfall 6); `TryDecode` accepts a typed decode only when it re-encodes byte-identical, else raw-preserves; unknown version → raw-preserve (never throws).
- `ParticleParseException`/`ParticleParseError` structured taxonomy.
- `ParticleFixtureBuilder` synthesizes `FORM PEFT/WVFM/CLRR` fixtures THROUGH `IffWriter` (no `.prt` fixtures exist today). 9 facts (`--filter ParticleWaveForm`), incl. the no-`scaleAll(0.28f)`-on-write guard (Pitfall 3).

**Task 2 — PEFT typed-decode tree (commit `7242701`):**
- `ParticleEffectDocument.FromBytes`: `bytes → IffReader.Read → MutableIffDocument.FromDocument → MutableParticleEffect` (the RoundtripOt `ParseOt` idiom; no hand-rolled chunk/pad/endianness handling).
- `MutableParticleEffect`: walks `FORM PEFT → FORM <version> → [PTIM] + count chunk + N × FORM EMGP`, each EMGP `→ [PTIM] + count chunk + N × FORM EMTR`. `EditLeafPayload` mutates a captured leaf in place; `RewriteCount` re-derives the machine-managed int32 count little-endian while preserving trailing bytes (D-04).
- `ParticleEmitterDescription` types the EMTR sub-tree's WVFM/CLRR leaves via the leaf codecs.
- `ParticleEffectWriter.Serialize` composes `IffWriter.Write`; no-edit round-trip is byte-identical. 7 facts (`--filter ParticleDecode`), incl. the no-edit byte-identity round-trip and the forged-count rejection.

**Task 3 — degrade-don't-abort (commit `cdbf428`):**
- Loop-level `catch (DecoderException) → ParticleEmitterDescription.RawPreserved(...)` in the emitter loop, copied from `MutableObjectTemplate.FromMutableIff`. Unknown EMTR/PEFT-root version raw-preserves the whole sub-tree; a truncated count chunk surfaces as `ParticleParseException` (no leaked `DecoderException`); a truncated/over-length WVFM leaf falls to consume-exactly-or-hex raw-preserve.
- A typed edit on a raw-preserved effect is refused with a clean `InvalidOperationException` (no corruption). 5 facts (`--filter ParticleDegrade`), incl. the negative no-hard-abort assertion + byte-exact degraded round-trip.

## Verification

- `dotnet test ... --filter Particle`: **21/21** facts pass (9 ParticleWaveForm + 7 ParticleDecode + 5 ParticleDegrade), Debug AND Release|x86.
- Full `UtinniCoreDotNet.Tests`: **663/663** pass (642 baseline + 21 new), no regressions.
- Grep gates: provenance header (`original to Utinni` + `swg-client-v2`) present in all 8 `Formats/Particle/*.cs`; **no `BitConverter`** in the codecs; **no `FATAL` token** anywhere in the codec; `IffReader.Read` composed in `ParticleEffectDocument`, `IffWriter` in `ParticleEffectWriter`; division-form/cap count guard before every read loop; every version `switch` has a degrading `default:`.
- `Generated/UtinniCore.cs` unchanged (pure-managed codec; `git checkout --` applied after each MSBuild per `project_utinnicore_cs_regen_churn`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Reworded degrade comments to drop the `FATAL` token**
- **Found during:** Task 2 grep-gate verification (Task 3 acceptance: "grep the codec for `FATAL` returns NONE").
- **Issue:** Explanatory comments ("NEVER FATAL", "the SOE reference FATALs") contained the literally-gated token `FATAL`, which would fail the Task 3 grep gate (the documented grep-gate-hygiene trap).
- **Fix:** Reworded every occurrence to "hard-abort"/"hard-aborts" (same meaning, no gated token). The codec never had an actual `FATAL` call — it only ever raw-preserves.
- **Files modified:** `ParticleFieldValue.cs`, `ParticleParseException.cs`, `WaveFormCodec.cs`, `ParticleEmitterDescription.cs`, `MutableParticleEffect.cs`.
- **Commit:** `7242701` (rewording) finalized in `cdbf428`.

**2. [Rule 3 - Blocking] No-`BitConverter` float emit**
- **Found during:** Task 1 grep-gate verification ("NO `BitConverter` index math").
- **Issue:** The first cut of `WriteFloatLe` used `BitConverter.GetBytes(float)`. Although not index math, the gate is treated literally.
- **Fix:** Emit floats via `BinaryWriter.Write(float)` (little-endian on the supported x86 platform) — no `BitConverter` token, no byte-buffer index math.
- **Files modified:** `WaveFormCodec.cs`, `ColorRampCodec.cs`.
- **Commit:** `cfb09b0`.

**3. [Rule 2 - Missing functionality] One extra negative fact per task beyond the planned count**
- Added `ParticleDecode_NonPeftRoot_RejectedWithUnexpectedForm` (Task 2) so the non-PEFT root rejection is covered alongside the forged-count rejection. Counts as the ">= N facts" acceptance, not a deviation in spirit.

## Known Stubs

None that block the plan's goal. The typed PTIM/PTQD/PTMH/PTEX leaf interiors are intentionally NOT typed in V1 — they are preserved verbatim through the hybrid-DOM (byte-exact), and the plan scopes the typed common path to WaveForm/ColorRamp + the EMGP/EMTR tree. Fully typed timing/quad/mesh/texture decode and the per-field editor wiring are downstream-plan work (the editor shell `FormParticleEditor` is a separate Wave-2 plan). This is the documented degrade posture (D-05), not an accidental stub.

## Notes for downstream plans

- The codec is pure-managed and headless (D-06): no WinForms, no native, no MCP/CLI coupling. The CLI verb (`roundtrip-particle` / `decode-iff` PEFT branch) and the MCP read tool are a separate plan; they wrap this codec's `FromBytes` + `Serialize`.
- `IffPayloadCursor` is `internal` to `UtinniCoreDotNet`, so the codecs that read payloads must live in that assembly (they do).
- `MutableParticleEffect.EditLeafPayload` is the generic per-leaf edit seam; a future per-field editor composes it with a re-encoded `WaveFormCodec.Encode`/`ColorRampCodec.Encode` payload, exactly as the OT editor composes `ObjectTemplateParamCodec.Encode`.

## Self-Check: PASSED

All 12 created files exist on disk; all 3 per-task commits (`cfb09b0`, `7242701`, `cdbf428`) exist in history. 21/21 Particle facts + 663/663 full suite green Debug+Release|x86.
