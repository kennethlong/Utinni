---
phase: 23-user-definable-iff-chunk-templates
verified: 2026-06-21T00:00:00Z
status: passed
score: 3/3 must-haves verified
overrides_applied: 0
re_verification:
  previous_status: none
human_verification: []
---

# Phase 23: User-Definable IFF Chunk Templates Verification Report

**Phase Goal:** A modder can describe an arbitrary IFF chunk's binary layout once and have Utinni auto-decode, display, edit, and byte-exactly re-encode any matching chunk — turning Utinni from "the formats we coded" into "any format a modder can describe."
**Verified:** 2026-06-21
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

The phase goal decomposes into the three ROADMAP success criteria (= REQUIREMENTS PROD-IFFT-01/02/03). All three are observably true in the codebase: a kernel codec + JSON model + presets let a modder describe an arbitrary chunk layout; a version-FORM-aware resolver auto-applies a matching template and the kernel re-encodes byte-exact (78 green engine/verb goldens incl. the critical DEC-C3 count-from-prior grow/shrink round-trip, plus a maintainer live-smoke PASS); and the IFF Editor UI (TJT SubPanel, sibling repo) carries the full create/edit/save/select/import/export surface wired to the headless engine.

### Observable Truths

| # | Truth (Success Criterion) | Status | Evidence |
| --- | --- | --- | --- |
| 1 | A modder can describe an arbitrary IFF chunk's binary layout (primitives, colors, vectors, quaternions, matrices, arrays, structs) as a named, reusable template (PROD-IFFT-01) | ✓ VERIFIED | `TemplateModel.cs` (167L) + `TemplateJson.cs` (148L, `TypeNameHandling=None`) define a portable POCO JSON model; `KernelCodec.cs` (635L) decodes/encodes all kernel types + Array (FixedCount/CountFromField/UntilEnd) + Struct; 7 D-09 presets (vector/quaternion/matrix/3 colors/stringId) ship as pure JSON sugar; `list-templates` verb enumerates 9 templates live. Tests: `TemplateJsonTests`, `TemplatePresetTests` green (in 78-test suite). |
| 2 | Utinni auto-applies a matching template to decode/display an otherwise-hex chunk and re-encodes edits byte-exact — round-trip verified (PROD-IFFT-02) | ✓ VERIFIED | `TemplateResolver.cs` (406L) matches by ancestor-FORM-path + version-FORM + leaf tag, with the D-05 altitude gate (`BuiltinRootForms.HasBuiltinDecoder`) and D-07 fit-confidence; CR-01 version-FORM guard present (TemplateResolver.cs:249,259 `return false`). `KernelCodec.Encode` auto-recomputes count-from-prior (D-10) with CR-02 range-check before write (KernelCodec.cs:402-437). The CRITICAL DEC-C3 grow/shrink byte-exact golden is GREEN (KernelCodecTests.cs:83,106). 78 CLI template tests pass, 0 skipped; `roundtrip-template`/`apply-save-template` verbs runnable. |
| 3 | Templates are manageable (create / edit / save / select) from the IFF Editor UI (PROD-IFFT-03) | ✓ VERIFIED | `TemplateBuilderPane.cs` (1073L, sibling repo) — select-byte-range→assign gesture (SelectionStart/Length), live byte-exact round-trip indicator (green/red via `KernelCodec`+`FitReport`), Save/Select/Import/Export menus with exact UI-SPEC copy ("Save template to pack…", "Shipped (read-only)" disabled, "Import template pack…", "Export this template…"), save via `TemplateJson`→`TemplatePackStore` writable pack. `FormIffEditor.cs` (2178L) wires Template-mode toggle, altitude-tooltip disable on built-in-claimed files, auto-apply, GetPayloadCopy/SetPayload leaf binding. MCP `summarize_with_template` tool (shell-out, zero format logic). Maintainer live-smoke PASS (23-08, game_music_manager.iff GMUS/WATR). |

**Score:** 3/3 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
| --- | --- | --- | --- |
| `UtinniCoreDotNet/Formats/Template/ITemplateContracts.cs` | Frozen contracts (≥40L) | ✓ VERIFIED | 244L |
| `UtinniCoreDotNet/Formats/Template/TemplateModel.cs` | POCO model | ✓ VERIFIED | 167L |
| `UtinniCoreDotNet/Formats/Template/TemplateJson.cs` | (de)serializer, TypeNameHandling=None | ✓ VERIFIED | 148L; gadget mitigation confirmed |
| `UtinniCoreDotNet/Formats/Template/KernelCodec.cs` | byte-exact decode/encode + D-10 (≥120L) | ✓ VERIFIED | 635L; CR-02 guard at 402-437 |
| `UtinniCoreDotNet/Formats/Template/FitReport.cs` | pure fit function (≥30L) | ✓ VERIFIED | 152L |
| `UtinniCoreDotNet/Formats/Template/TypePlausibility.cs` | predicate library | ✓ VERIFIED | 117L |
| `UtinniCoreDotNet/Formats/Template/TemplateResolver.cs` | match + altitude + fit (≥60L) | ✓ VERIFIED | 406L; CR-01 guard at 249,259 |
| `UtinniCoreDotNet/Formats/Template/TemplatePackStore.cs` | scanned allow-list (≥40L) | ✓ VERIFIED | 300L; LooseOverridePath containment |
| `UtinniCoreDotNet/Formats/Decoders/IffPayloadCursor.cs` | bounds-checked cursor | ✓ VERIFIED | 243L |
| Presets (7 JSON) | D-09 byte-exact sugar | ✓ VERIFIED | vector/quaternion/matrix/color/colorArgb32/colorArgbF/stringId |
| Examples (2 JSON) | worked-example goldens | ✓ VERIFIED | counted_records, flat_composite |
| `Utinni.Cli/Commands/{DecodeWithTemplate,RoundtripTemplate,ApplySaveTemplate,ListTemplates}Command.cs` | 4 verbs | ✓ VERIFIED | 309/153/301/113L; all registered in Program.cs:78-81,121-124; runnable |
| `Utinni.Mcp/Tools/ReadTools.cs` | thin summarize_with_template | ✓ VERIFIED | summarize_with_template at :146; shells decode-with-template, zero format logic |
| `…/UI/Controls/TemplateBuilderPane.cs` (sibling) | builder + pack mgmt (≥40L) | ✓ VERIFIED | 1073L |
| `…/UI/Forms/FormIffEditor.cs` (sibling) | template-mode integration | ✓ VERIFIED | 2178L |
| `…/UI/Forms/FormAssignFieldDialog.cs` (sibling) | assign type+name dialog | ✓ VERIFIED | 302L |

### Key Link Verification

| From | To | Via | Status | Details |
| --- | --- | --- | --- | --- |
| TemplateBuilderPane | KernelCodec / FitReport / TemplateResolver | live decode + SetPayload | ✓ WIRED | KernelCodec.Decode/Encode + FitReport referenced (TemplateBuilderPane.cs:70,108,235,716,720) |
| TemplateBuilderPane | TemplatePackStore / TemplateJson | save serialize + select scanned packs | ✓ WIRED | TemplateJson.Serialize/Deserialize + TemplatePackStore.DefaultRoots().LoadAll() (:432,452,594,612,642,661) |
| FormIffEditor | MutableIffNode.GetPayloadCopy / SetPayload | template mode reads/commits leaf | ✓ WIRED | GetPayloadCopy at :459; SetPayload bound |
| ReadTools (MCP) | CliDispatcher | RunAsync("decode-with-template") | ✓ WIRED | ReadTools.cs:171, verbatim envelope pass-through |
| TemplateResolver | BuiltinRootForms | D-05 altitude gate | ✓ WIRED | HasBuiltinDecoder single-source gate |
| Program.cs | 4 template command verbs | option types + dispatch | ✓ WIRED | registered + dispatched; `list-templates` confirmed runnable (9 templates) |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
| --- | --- | --- | --- |
| Engine + verb goldens (incl. DEC-C3) pass | `dotnet test Utinni.Cli.Tests --no-build -c Release --filter ~Template` | 78 passed, 0 failed, 0 skipped | ✓ PASS |
| MCP template tool tests pass | `dotnet test Utinni.Mcp.Tests --no-build -c Release --filter ~Template` | 10 passed, 0 failed, 0 skipped | ✓ PASS |
| Template verbs registered & runnable | `utinni-cli --help` / `list-templates` | 4 verbs present; list-templates enumerates 9 templates across 3 scanned roots | ✓ PASS |

Note: the stale `bin/Release/utinni-cli.exe` lacks the verbs; the current `Utinni.Cli/bin/Release/net472/utinni-cli.exe` has them. Not a phase defect (output-staging artifact).

### Requirements Coverage

| Requirement | Source Plan(s) | Description | Status | Evidence |
| --- | --- | --- | --- | --- |
| PROD-IFFT-01 | 23-01,02,03 | Describe arbitrary chunk layout as named reusable template | ✓ SATISFIED | Truth 1 |
| PROD-IFFT-02 | 23-01,03,04,05 | Auto-apply matching template + byte-exact re-encode (round-trip verified) | ✓ SATISFIED | Truth 2; DEC-C3 golden green; CR-01/CR-02 guards |
| PROD-IFFT-03 | 23-01,05,06,07,08 | Manage (create/edit/save/select) from IFF Editor UI | ✓ SATISFIED | Truth 3; live-smoke PASS |

No orphaned requirements — all three IDs in REQUIREMENTS.md (lines 95-99, 162-164, mapped to Phase 23) are claimed by plans and verified.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
| --- | --- | --- | --- | --- |
| TemplateBuilderPane.cs | 102 | "XXXXXXXX" in comment | ℹ️ Info | Literal hex-format example string in a comment ("8 hex + ':'"), not an XXX debt marker — no action |

No TBD/FIXME/XXX debt markers, no TODO/HACK/PLACEHOLDER, no stub returns in any phase-modified engine/CLI/MCP/UI file.

### Locked-Invariant Compliance

- **Byte-exact round-trip (codec gate):** ✓ DEC-C3 grow/shrink golden green; CR-02 encode range-check; live-smoke byte-exact on GMUS/WATR.
- **DEC-V2-VERBS-FIRST:** ✓ engine lands as 4 utinni-cli verbs gated by byte-exact golden before MCP/UI consume it (D-15). UI hex-authoring is the sanctioned D-16 UI-only non-exception.
- **DEC-V2-MCP-OOP:** ✓ `summarize_with_template` shells decode-with-template; ZERO format logic in MCP process (ReadTools.cs:36,151).
- **DEC-C4:** ✓ builder + pack-management ship as a TJT SubPanel (TemplateBuilderPane inside FormIffEditor), not a separate plugin.

### Code Review Closure (23-REVIEW.md)

2 critical + 6 warning findings, all status: resolved. Independently verified — all 10 fix commits exist:
- Utinni: `1db5496` (CR-01), `d50b06a` (CR-02), `aabb341`+`524dd11` (WR-01), `763ff74` (WR-03), `9065baa` (WR-04), `a0bda36` (WR-05).
- UtinniPlugins: `c5677d8` (WR-02), `9cf94e8` (WR-04), `ced0056` (WR-06).
The CR-01 version-FORM guard and CR-02 overflow throw are present in source (confirmed above), not just claimed.

### Deferred / Out-of-Scope (not gaps)

| Item | Disposition |
| --- | --- |
| `AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline` failing | Known Phase-17 stale-Generated CppSharp harness gotcha; `git diff` confirms phase-23 touched ZERO of the CppSharp binding surface. NOT a phase-23 defect. |
| Byte-grid selection visual-readout polish | Cosmetic only; selection is functionally correct (clamped to whole bytes); maintainer-acknowledged non-blocking at 23-08 live-smoke. |

### Human Verification Required

None. The one manual UAT (create→edit→save→byte-exact re-encode in a live injected IFF Editor session) was already run and APPROVED/PASS by the maintainer at the 23-08 checkpoint (2026-06-21, game_music_manager.iff GMUS/WATR, no sibling-chunk corruption). No new human-verification needs surfaced — the WinForms SubPanel has no CI UIA harness, but the deferred end-of-phase items list is empty and the live-smoke covered the full loop.

### Gaps Summary

No gaps. All 3 success criteria / requirement IDs are observably satisfied: the engine (model + kernel codec + presets + resolver) is implemented, headless, and byte-exact (78+10 green tests incl. the critical DEC-C3 golden and CR-01/CR-02 regression tests); the 4 CLI verbs are registered and runnable; the MCP tool is a thin zero-logic shell; the IFF Editor UI (sibling repo) carries the full create/edit/save/select/import/export surface wired to the engine; all locked invariants (byte-exact gate, verbs-first, MCP-OOP, TJT SubPanel) hold; all review findings are fixed with verified commits; and the maintainer live-smoke passed end-to-end.

---

_Verified: 2026-06-21_
_Verifier: Claude (gsd-verifier)_
