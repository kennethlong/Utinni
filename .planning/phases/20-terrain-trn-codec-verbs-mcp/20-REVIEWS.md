---
phase: 20
reviewers: [codex, cursor]
reviewed_at: 2026-06-16T04:38:11Z
plans_reviewed: [20-01-PLAN.md, 20-02-PLAN.md, 20-03-PLAN.md, 20-04-PLAN.md]
self_skipped: claude (running inside Claude Code — skipped for independence)
not_available: [gemini, coderabbit, opencode, qwen]
---

# Cross-AI Plan Review — Phase 20: Terrain `.trn` Codec + Verbs + MCP

Two independent external reviewers (Codex / ChatGPT-authed, Cursor agent) reviewed the four phase
plans against the goal, the four success criteria, the CONTEXT decisions (D-01..D-14), and the seven
RESEARCH pitfalls. Both rated overall execution risk **MEDIUM–HIGH** — the architecture is sound, but
several plan-coherence and byte-exact-correctness gaps should be closed before execution.

---

## Codex Review

**Summary**

The four waves mostly line up with the Phase 20 goal and the four success criteria: they preserve the
verbs-first/MCP-thin architecture, avoid native dependencies, plan for raw fallback, and make
fixed-length packed-DATA edits explicit. The plan is strongest where it constrains save scope to
fixed-length leaf edits. The main risk is that the validation strategy can produce false confidence:
synthetic tiny fixtures may not reflect real `.trn` version/layout combinations, positional palette
decoding is underspecified when optional forms are absent, and byte-exact single-field replacement is
only safe if the decoder/encoder preserves exact field width, signedness, enum representation, float
bit patterns, and per-version payload layout.

**Strengths**

- `DEC-V2-VERBS-FIRST` and `DEC-V2-MCP-OOP` are respected: CLI verbs own behavior; MCP shells the CLI.
- Fixed-length-only edits in D-05/D-06 are the right containment boundary for v1 byte-exact saves.
- Field-aware edits in D-09 correctly address the packed-DATA pitfall; whole-leaf hex replacement would be unsafe.
- Version-first decode plus unknown-version whole-chunk fallback is a good default for RE formats.
- DEAD tags being recognized, skipped, and re-emitted verbatim is safer than exposing them as editable raw nodes.
- Single-sourced decoder/encoder layout table is the right design if it is actually enforced by tests.
- `--root` containment + atomic write reuse from `ApplySaveIffCommand` is the correct save-path posture.

**Concerns**

- **HIGH:** Synthetic `<=200-byte` fixtures are not enough to prove both-lineage correctness. D-12 claims a committed golden corpus across SWGEmu/Restoration, but D-13/D-14 only pin real versions in Wave 3. Until then, the matrix can be green against invented versions/layouts that no real `.trn` uses.
- **HIGH:** Positional palette decoding is underdefined when optional palettes are absent. D-04 says decode six palettes positionally in fixed load order, but Pitfall 3 says palettes are optional. If one `MGRP` or earlier palette is absent, naive "next palette slot" assignment can silently mislabel fractal vs bitmap or shift later groups.
- **HIGH:** Single-field packed-DATA re-encode is safe only if the field table records exact binary representation, not just semantic type. The plan mentions scalar float/int/enum but not enum width, signedness, bool encoding, reserved bytes, fixed arrays, padding, or version-specific absent fields. A semantic `float.Parse`/`WriteSingle` edit can also canonicalize NaN payloads if the old value is round-tripped through text.
- **HIGH:** "Known tag + known version" can still misread if the layout table is incomplete or version ranges are guessed. `Need` prevents buffer over-read, but fallback after partially reading must not leave a malformed typed node with wrong stable IDs.
- **MEDIUM:** "Truncated payloads raw-fall-back rather than over-read" is good for decode, but dangerous for edit. Any node that fell back due to truncation/unknown trailing bytes/inconsistent consumed length must be marked non-editable. Otherwise `apply-save-trn` could rewrite part of a payload it did not fully understand.
- **MEDIUM:** Byte-exact save verification needs to compare unmodified bytes around the edited field, not just "untouched leaves." If an edited leaf is reserialized wholesale, unchanged fields inside the same packed DATA payload may be normalized. Assert byte equality for all byte ranges except the exact target field span.
- **MEDIUM:** Float handling needs bit-level policy. Non-edited floats in the same payload must preserve exact bits; specify accepted input format and rejection of NaN/Infinity unless intentionally supported.
- **MEDIUM:** Stable IDs derived from ordinal paths are fragile across decode modes. If unknown/DEAD nodes are skipped from the visible tree, ordinals can shift vs raw DOM order. D-09 needs stable IDs derived from physical FORM/chunk path including raw/dead siblings, or explicit guarantees that skipped nodes still occupy ordinal slots.
- **MEDIUM:** DEAD-tag captured-slice re-emit is byte-safe only because v1 forbids variable-length edits and parent FORM length changes. Make this invariant executable: reject any operation that changes payload length anywhere in an ancestor, and test DEAD tags adjacent to edited siblings.
- **MEDIUM:** `decode-iff TGEN branch` and `decode-trn alias` may produce divergent JSON/envelope shapes unless explicitly specified. Tests should assert schema compatibility, exit code mapping, stderr behavior, and malformed-input behavior.
- **MEDIUM:** The Wave 0 stub verb adds churn and possible dispatch ambiguity. A no-op `trn` verb later superseded is weak value unless it exercises the exact final verb registration shape.
- **LOW:** `ReadCString` being variable-length conflicts with fixed-length edit scope for names, but names still influence decode offsets. Tests should include names with empty string, no terminator, embedded non-ASCII bytes, and long strings to prove cursor positioning.
- **LOW:** "Unknown/long-tail tags degrade to generic field list" conflicts slightly with D-01 raw fallback `{tag, version, hex}`. A generic field list implies parsing; raw hex implies not parsing. The public contract should distinguish "raw preserved node" from "generic parsed field list."
- **LOW:** No explicit mention of Restoration encrypted `v6000+` TRE behavior at this phase boundary. Tests should confirm encrypted payloads are enumerate-only and fail clearly before decode attempts.

**Suggestions**

- Move real-asset version discovery earlier, before finalizing the fixture matrix. Wave 0/1 should run `parse-tre` on one real SWGEmu and one Restoration `.trn`, record observed FORM/tag versions in a non-committed note, then synthesize fixtures from those facts.
- Define a binary field descriptor table with: tag, version range, offset, width, signedness, endian, enum storage width, display type, edit parser, and editability. Decoder and encoder consume this exact table.
- For `apply-save-trn`, compute the exact byte span of the target field and assert the output differs only in that span. For enum/active edits, assert width-specific replacement, not semantic reserialization.
- Make raw-fallback states explicitly non-editable unless the node has a complete known descriptor and consumed payload length equals payload size.
- For palettes, decode by a state machine over expected load order with explicit optional handling. If ambiguity exists (especially missing `MGRP`), mark palette identity as ambiguous instead of guessing fractal vs bitmap.
- Add negative tests: truncated known tag, known tag with trailing bytes, unknown version, missing `LYRS`, missing each palette slot, only one `MGRP`, DEAD tag before/after edited sibling, malformed CString.
- Replace the Wave 0 no-op verb with either the final verb names returning "not implemented" through the real dispatch path, or skip the stub and add a direct dispatch registration test.
- Specify JSON output schemas for `decode-iff` TGEN, `decode-trn`, and MCP `summarize_terrain`; test that MCP resolves the just-built x86 Release CLI and propagates nonzero exit codes as tool errors.

**Risk Assessment: MEDIUM-HIGH.** The architecture direction is sound and the fixed-length edit
boundary is appropriately conservative. The risk is not scope explosion; it is false byte-exact
confidence from synthetic fixtures and underspecified binary layout details. If real version pinning,
exact field-span verification, and optional palette ambiguity handling are tightened, this drops to
medium.

---

## Cursor Review

**Summary**

The four-plan wave structure is directionally sound: it reuses proven Utinni machinery
(`IffReader`/`MutableIffDocument`/`IffWriter`, `apply-save-iff` containment, `summarize_particle` MCP
shell, `Type[]` verb dispatch at 23 verbs today) and correctly identifies the two genuinely new pieces
— `TgenDecoder` and field-aware `TrnFieldEncoder`. Executed strictly as written, however, the plans
have **internal ordering contradictions** (Tier-1 fixture matrix vs. wave boundaries),
**acceptance-criteria gaps** (navigation tree vs. summary-only JSON envelope), and **under-specified
edit-path mechanics** (active-flag location, tag/version discovery for `TrnFieldEncoder`, ancestor
context for non-`DATA`-only fields). D-09 single-field re-encode is *mostly* safe under D-05/D-06
fixed-length scope, but only if layout tables are truly single-sourced and the encoder resolves
tag/version from the DOM — neither is fully nailed down. **Verdict:** the plans can reach the phase
goal, but not without rework on wave-1 fixture coverage, JSON surface for TRN-01, and explicit
encoder/DOM wiring tasks; overall execution risk is **MEDIUM–HIGH**.

**Strengths**

- **Correct architectural bet:** Compose-on-DOM (`TerrainDocument.FromIff` holding `MutableIffDocument`) mirrors the proven particle path and is the only credible way to hit byte-exact without re-serializing from a typed model.
- **Pitfall coverage is unusually good:** Research and plans explicitly address packed `DATA` leaves, DEAD tags, optional palettes/LYRS, dual `MGRP`, version-first reads, no-pad, and deferred name edits.
- **D-05/D-06 scoping is disciplined:** Restricting v1 to fixed-length scalar/enum/active edits sidesteps FORM length ripple and makes D-09 byte-exact reasoning tractable.
- **Verbs-first + MCP-OOP are consistent with repo reality:** `summarize_particle` is a literal copy template; `Program.cs` already uses `Type[]` dispatch — D-11 smoke is cheap insurance.
- **Threat model reuse:** Fail-closed `--root`, atomic write, untouched-leaf verify copied from `ApplySaveIffCommand` is the right pattern for TRN-03.
- **Wave 0 Nyquist intent:** Skip stubs + synthesizer-first gives downstream tasks a place to land tests.

**Concerns**

*Byte-exact / D-09 (`TrnFieldEncoder`)*
- **HIGH — Tag/version discovery for field edits is unspecified (20-03 Task 3, D-09).** `TrnFieldEncoder` is `(payload, tag, version, fieldName, value)`, but `apply-save-trn` addresses a `DATA` leaf by stable-id. The plan never requires walking ancestors to recover the parent FORM FourCC and version form. Without that, field layout lookup will be wrong for most targets — especially the active-flag edit, which likely lives under `IHDR`, not affector `DATA`.
- **HIGH — "Layer/affector active flag" is ambiguous (20-03 Task 1/3, criterion 3).** Research places active+name on `IHDR` under `LAYR`; the model exposes `TerrainLayer.Active` at layer level. The plan does not define which DOM leaf `--field active` mutates. Classic "green tests, wrong bytes in real `.trn`."
- **MEDIUM — Layout table "single-sourced" is aspirational, not mandated (20-03 Task 1).** Text says "ideally shared with `TgenDecoder`" but there is no artifact/task for a shared `TgenFieldLayout`. Duplicated offset tables are a classic byte-exact failure mode.
- **MEDIUM — Float edits via decimal `--value` strings (20-03, D-09).** `float.Parse`/`TryParse` can produce different IEEE representations and won't preserve NaN payload bits. Acceptable for v1 if documented; not if tests compare full-file identity after round-trip *through* parse/format.
- **LOW — Enum width / unsigned fields.** Plan doesn't require explicit width typing per field (int8 vs int32 vs uint32) in the shared layout table — easy to mis-port one tag.

*Version-first decode (D-02 / Pitfall 5)*
- **MEDIUM — Truncated-payload handling is stated but not structurally enforced (20-02 Task 2).** Typed decode must **catch** `DecoderException(Truncated)` at the tag dispatch boundary and convert to whole-chunk raw-fallback. The plan assumes this without a test for "known tag, truncated mid-field → `IsRawPreserved`, no throw." Threat T-20-02 claims this test exists; 20-02 `<behavior>` does not list it.
- **MEDIUM — Known tag + known version can still mis-read if fixture omits optional v0001 fields.** BCIR v0000 vs v0001 is covered; other Tier-1 tags with multi-version field growth need the same rigor across the full D-01 set — currently under-fixtured.
- **LOW — Whole-chunk raw-fallback on unknown version is byte-safe for read/roundtrip, but opaque for edit.** Criterion 2 "degrade to generic field list" vs D-02 "whole chunk hex" is slightly inconsistent for agents expecting field granularity.

*Positional palettes (D-04 / Pitfall 4)*
- **MEDIUM — Absent-palette disambiguation is under-tested (20-02 Task 2).** Plan tests "both MGRP present → second is Bitmap." Missing: **Fractal absent, only one `MGRP` present** — does it bind to slot 5 or slot 6? C++ `load_0000` sequential optional `enterForm` semantics must be mirrored exactly.
- **HIGH — ≤200-byte budget conflicts with palette matrix (20-04 Task 2 vs D-12).** Task 2 asks for a "palette-bearing TGEN exercising ShaderGroup/FloraGroup high version" while D-12 locks all fixtures to ≤200 bytes. Real palette forms with `ReadCString` names cannot fit that budget; executor will either violate D-12 or ship a palette test that doesn't exercise lineage divergence.

*DEAD-tag verbatim re-emit (D-03)*
- **LOW — Reasoning is sound under D-05.** No concern if D-06 holds.
- **MEDIUM — DEAD tags nested inside a dirty subtree (future scope).** Worth a one-line invariant in 20-02 that DEAD skip nodes re-emit exact form framing.

*Fixture realism (D-12 / D-13 / D-14)*
- **HIGH — Wave ordering bug: 20-02 acceptance requires full Tier-1 matrix before 20-04 builds it.** 20-02 acceptance: "Every Tier-1 tag decodes … BOTH a low and high FORM version." But 20-01's synthesizer only provides `WithAffector("AHCN")`, `WithBoundary("BCIR")`, plus unknown/dead helpers; the full per-tag low/high matrix is deferred to **20-04 Task 2**. 20-02 cannot honestly pass its own acceptance criteria on schedule.
- **HIGH — "Both lineages" gate can go green before reality check (20-03 Task 1 vs 20-04 Task 3).** `RoundtripTrnTests` turn green in 20-03 against assumed `SwgEmuEraVersions`/`RestorationEraVersions` not introduced until 20-04 — and pinning happens *after* DEC-C3 is declared satisfied. False-green window if assumed ≠ shipped versions.
- **MEDIUM — Real-asset checkpoint is soft-failable (20-04 Task 3).** "If Restoration encrypted, rely on synthesized matrix" means TRN-04 "both lineages" may never be validated against a real Restoration `.trn`.

*Wave / dependency ordering*
- **MEDIUM — 20-02 Task 1 verify is weak ("may still Skip").** Model-only task has no behavioral assertion — only compile/collect. Borderline Nyquist-compliant, low signal.
- **LOW — Wave-0 `trn` stub verb churn (20-01 → 20-03).** Harmless but a throwaway `Program.cs` diff.
- **MEDIUM — Verb golden coverage gap (20-03 Task 2).** `roundtrip-trn` / `decode-iff` TGEN branch rely on manual CLI verify strings, not xUnit goldens (unlike `RoundtripTrnTests` which bypass the verb). Verb wiring regressions won't be caught by CI filters.

*MCP-OOP (D-10) / decode-iff routing (D-08)*
- **MEDIUM — TRN-01 "navigate tree" is not exposed through MCP/CLI JSON (20-03 Task 2).** `BuildTerrainResult` is summary-only (`layerCount`, `paletteCounts`, `rawFallbackCount`) — same shape as `BuildParticleResult`. An AI agent calling `summarize_terrain`/`decode_iff` **cannot navigate names/active flags/typed fields** — only counts. TRN-01 is tested via unit tests on `TerrainDocument`, not via the stated agent/verb surface. Criteria vs implementation mismatch.
- **LOW — Plan 20-04 MCP test "must resolve freshly-built utinni-cli.exe" overstates Phase 15 precedent.** `ParticleReadToolTests` uses `RecordingDispatcher` stubs — no real CLI spawn. Mixing stub tests (particle) with real-CLI tests (terrain) creates inconsistent gates.
- **LOW — `summarize_terrain` vs existing `decode_iff` redundancy.** Once TGEN branches in `decode-iff`, `summarize_terrain` adds no capability — only naming symmetry. Fine.

*Scope*
- **MEDIUM — Missing `TerrainDocument.Serialize()` task.** 20-02 builds `FromBytes`/`FromIff` but PATTERNS/20-03 assume `model.Serialize()` / `IffWriter.Write(mutable)`. Not listed in 20-02 files or tasks — executor must invent it.
- **LOW — `decode-iff` HelpText still omits terrain (20-03).** Cosmetic.
- **LOW — No `UtinniCoreDotNet.Tests` lane for pure codec.** Core layout drift won't be caught outside CLI assembly.

*Error handling / edge cases*
- **MEDIUM — No golden for "edit then roundtrip real multi-layer file with DEAD + raw-fallback siblings adjacent to edited leaf."** Fixture matrix lacks compositional complexity (multi-node LAYR with DEAD + unknown + Tier-1 siblings).
- **LOW — `.trn` path vs `.iff` extension.** No explicit guard that root FORM is `TGEN` on roundtrip beyond parse exception.
- **MEDIUM — LAYR version progression (0000–0004) under-decoded for navigation (20-02).** TRN-01 requires name+active; LAYR v0001+ adds invertFilters/expanded/notes. Layer header version dispatch across LAYR versions is thin in `<behavior>`.

**Suggestions** (top picks)
1. Split fixture-matrix construction across waves correctly: move "every Tier-1 tag × low/high version" builders to **20-01 / early 20-02** (minimal payloads); reserve 20-04 for pinning constants + optional palette-heavy fixtures **without** the ≤200-byte constraint (separate `LargeFixtures` opt-in, not committed goldens).
2. Add `TgenFieldLayouts.cs` (shared read/write table) as an explicit 20-02 artifact, referenced by both `TgenDecoder` and `TrnFieldEncoder` — with unit tests asserting decoder offsets == encoder offsets per tag/version.
3. Specify the active-flag edit path in one decision block (which DOM leaf `--field active` mutates).
4. Add `ApplySaveTrnCommand` helper `ResolveFieldContext(leaf) → (tag, version)` by walking the parent chain — task it explicitly in 20-03 Task 3.
5. Expand TRN-01 JSON (navigable tree: layers/children/typed fields/palette names) **or** narrow criterion 1 to "codec model + unit tests" and demote MCP to summary-only. Don't leave the ambiguity.
6. Add palette-absence tests + a truncated-payload golden + CLI golden tests for the TGEN verbs.
7. Reorder DEC-C3 sign-off: treat 20-04 Task 3 version-pin as a **prerequisite** to declaring DEC-C3 closed, not a post-hoc checkpoint.
8. Drop/repurpose the Wave-0 `trn` stub; document float edit semantics; add a compositional fixture (one LAYR with AHCN + `BALL` DEAD + unknown + BCIR).

**Risk Assessment: MEDIUM–HIGH.** The architectural foundation is strong and aligns with code that
already works. The phase failure modes are **plan-coherence bugs**: fixture-matrix timing,
summary-only JSON vs navigation criteria, and under-specified edit addressing for packed multi-field
payloads. Highest-impact pre-execution fixes: (1) resolve 20-02 vs 20-04 fixture-matrix ordering,
(2) nail active-flag + `TrnFieldEncoder` context resolution, (3) decide TRN-01 JSON depth for agents,
(4) relax or split the palette fixture size constraint. With those addressed, risk drops to MEDIUM.

---

## Consensus Summary

Both reviewers independently rate the phase **MEDIUM–HIGH risk** and agree the architecture is right
(compose-on-DOM, verbs-first, MCP-OOP, fixed-length edit scope) — the failures, if any, will be
**plan-coherence and byte-exact-correctness gaps**, not exotic IFF bugs. The single loudest theme:
**the both-lineage byte-exact gate risks a "false green"** because synthesized fixtures are validated
against *assumed* FORM versions and the real-asset version pin lands last (20-04 Task 3) and is
explicitly soft-failable.

### Agreed Strengths (2+ reviewers)
- DEC-V2-VERBS-FIRST / DEC-V2-MCP-OOP respected — CLI owns behavior, MCP shells it.
- D-05/D-06 fixed-length-only edit scope is the correct conservative boundary for v1 byte-exact saves.
- Field-aware single-field re-encode (D-09) correctly addresses the packed-DATA pitfall.
- Version-first decode + whole-chunk raw-fallback on unknown version is the right RE default.
- DEAD-tag recognize-and-skip (verbatim re-emit) is safer than editable raw nodes.
- `--root` containment + atomic write + untouched-leaf verify reuse from `ApplySaveIffCommand`.

### Agreed Concerns (raised by both — highest priority)
1. **HIGH — False byte-exact confidence from synthesized fixtures + late/soft real-asset pin.**
   The DEC-C3 both-lineage matrix goes green in 20-03 against assumed version constants; the real
   `.trn` pin is 20-04 Task 3 and may skip Restoration if encrypted. → *Move real-asset version
   discovery earlier (Wave 0/1) and make the version-pin a prerequisite to declaring DEC-C3 closed,
   not a post-hoc checkpoint.*
2. **HIGH — Single-field re-encode is only safe with an exact binary field-descriptor table that is
   genuinely single-sourced.** Both flag that the layout table is "aspirational" and lacks explicit
   width/signedness/enum-width/float-bit/version-absent-field specification. → *Make a shared
   `TgenFieldLayouts` an explicit artifact consumed by both decoder and encoder, with a test asserting
   offsets match; assert the output differs only in the target field's exact byte span.*
3. **HIGH/MEDIUM — Positional palette decode is underspecified when optional palettes are absent.**
   Absence could shift slots and silently mis-assign Fractal vs Bitmap (the two MGRP). → *Implement as
   a sequential optional-slot state machine mirroring C++ `load_0000`; add absence tests (missing each
   palette, single MGRP); mark identity ambiguous rather than guess.*
4. **MEDIUM — Raw-fallback / truncated nodes must be explicitly non-editable.** Edit must never rewrite
   a payload the decoder did not fully understand. → *Gate editability on a complete descriptor +
   consumed-length == payload-length.*
5. **MEDIUM — JSON envelope schemas for decode-iff TGEN / decode-trn / summarize_terrain are
   unspecified.** → *Specify and test schema compatibility, exit-code mapping, malformed-input behavior.*
6. **MEDIUM — Float edit semantics need a bit-level policy** (untouched floats preserve exact bits;
   define accepted input + NaN/Inf handling).
7. **LOW/MEDIUM — Wave-0 no-op `trn` stub verb is churn** (registered then removed in 20-03). → *Smoke
   via the first real verb or a test-only registration instead.*
8. **LOW — Criterion 2 wording "generic field list" vs D-01 "raw {tag,version,hex}"** should be
   reconciled in the public contract (raw-preserved ≠ parsed-field-list).

### Divergent / Single-Reviewer Views (worth investigating)
- **Cursor only — HIGH: TRN-01 "navigate tree" is not actually exposed through the MCP/CLI JSON
  surface.** `BuildTerrainResult` is summary-only (counts), so an AI agent "cannot navigate
  names/active/typed fields" — criterion 1 is met by unit tests on the model, not the stated agent
  surface. *This is the most consequential single finding* — either extend the envelope to a navigable
  tree or formally narrow criterion 1. (Codex did not raise this.)
- **Cursor only — HIGH: active-flag DOM location + `TrnFieldEncoder` tag/version discovery
  unspecified.** Encoder takes `(tag, version)` but the command addresses by stable-id and must walk
  ancestors to recover them; active likely lives on `IHDR`, not affector `DATA`. Add an explicit
  `ResolveFieldContext` task.
- **Cursor only — HIGH: ≤200-byte fixture budget (D-12) conflicts with the palette-bearing
  high-version fixture (20-04 Task 2).** Palette forms with CString names won't fit 200 bytes →
  relax/split the size constraint or the palette test won't exercise lineage divergence.
- **Cursor only — MEDIUM: missing `TerrainDocument.Serialize()` task** (20-03 assumes it; 20-02 doesn't
  build it).
- **Cursor only — MEDIUM: 20-02 vs 20-04 fixture-matrix ordering contradiction** (20-02 acceptance
  demands the full Tier-1 low/high matrix that 20-01 doesn't build and 20-04 only completes later).
- **Codex only — MEDIUM: stable-id ordinal fragility across decode modes.** If DEAD/unknown nodes are
  skipped from the visible tree, ordinal stable-ids can drift vs raw DOM order → derive stable-ids from
  the physical FORM/chunk path including raw/dead siblings.
- **Codex only — LOW: Restoration encrypted `v6000+` TRE** should fail clearly (enumerate-only) before
  any decode attempt during real-asset sourcing.

### Recommended pre-execution fixes (both reviewers' top asks, merged)
1. Resolve the fixture-matrix ordering: build the per-tag low/high synthesizer in 20-01/early-20-02;
   discover + pin real versions in Wave 0/1; make the version-pin a DEC-C3 prerequisite.
2. Add a shared `TgenFieldLayouts` artifact (decoder + encoder single source) with an offset-parity
   test and exact field-span byte-equality assertion.
3. Nail the active-flag DOM location and add `TrnFieldEncoder` ancestor tag/version resolution.
4. Decide TRN-01 JSON depth: navigable tree in the envelope, or narrow criterion 1 + demote MCP to
   summary-only.
5. Make raw-fallback/truncated nodes non-editable; add palette-absence + truncated-payload + verb
   golden tests; relax/split the palette fixture size constraint.

To incorporate this feedback into planning:
  `/gsd:plan-phase 20 --reviews`
