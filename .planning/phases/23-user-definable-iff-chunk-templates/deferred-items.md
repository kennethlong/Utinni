# Phase 23 — Deferred / Out-of-Scope Items

Items discovered during execution that are NOT caused by this phase's changes and are deferred per the
SCOPE BOUNDARY rule (only auto-fix issues directly caused by the current task's changes).

## 23-03 (Wave 2, KernelCodec engine)

### Pre-existing: `AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn` fails

- **Where:** `UtinniCoreDotNet.Tests` (the CppSharp native-binding ABI gate, Phase 17 CPPS-04a).
- **Symptom:** REMOVED(52) / ADDED(0) — the on-disk committed `Generated/UtinniCore.cs` block-hash set
  is missing 52 blocks relative to the blessed baseline fixture (`Fixtures/abi-baseline-blockhashes.txt`).
- **Why it is out of scope (verified):**
  - `git diff --stat 11aeeff(pre-23-03) HEAD -- UtinniCoreDotNet/Generated/UtinniCore.cs` is EMPTY —
    my three 23-03 commits do NOT touch the CppSharp-generated native binding surface at all.
  - 23-03 added only managed `UtinniCoreDotNet/Formats/Template/*.cs` + extended `IffPayloadCursor.cs`;
    none of these are part of the CppSharp `__Internal` / DllImport / enum surface the ABI gate hashes.
- **Root cause (documented harness gotcha, auto-memory `project_phase17_cppsharp_v145_hardening`):** the
  ABI test reads the on-disk `Generated/UtinniCore.cs`, but an incremental `msbuild /t:Build` skips the
  post-build `UtinniCoreDotNetGen.exe` regen step, so the test compares a STALE committed Generated file
  against the fully-regenerated baseline. The committed Generated file (last regenerated in 06-02
  `d69988d`) has drifted from the Phase-17 baseline fixture. Resolving it requires running
  `UtinniCoreDotNetGen.exe` (a native-build-dependent regen) and re-freezing the fixture — a CppSharp /
  toolchain concern, not a Template-engine concern.
- **Disposition:** DEFERRED. Not introduced by and not affected by 23-03. The 23-03 plan's own
  verification (the Template `--filter` goldens) is fully green. Re-bless the ABI baseline in a dedicated
  CppSharp-toolchain task (run the gen, `AbiBlockHash.Rebless`, rebuild TJT, re-freeze the fixture,
  commit together) — out of scope here.

## 23-08 (Wave 5, template pack management + live-smoke)

### Live-smoke UX polish: byte-grid selection readout (non-blocking)

- **Where:** `TemplateBuilderPane.cs` (UtinniPlugins) — the Tier-B raw-byte grid selection in the IFF
  Editor Template mode.
- **Symptom (cosmetic):** the byte-grid selection is a tolerant TextBox text-selection whose visual
  highlight can visually span the offset column + the ASCII gutter, so the user cannot see at a glance
  exactly which bytes are captured.
- **Why it is NOT a bug:** the selection is FUNCTIONALLY correct — `CaptureSelection` /
  `CaretToByteIndex` clamp the captured range to whole bytes (the offsets-are-selections D-02 contract),
  so the assigned span is always the right bytes. The 23-08 maintainer live-smoke confirmed PASS with
  byte-exact round-trip and no sibling-chunk corruption.
- **Polish ask:** add a "selected: 0xNN–0xNN (N bytes)" readout and/or restrict the visual highlight to
  the hex columns so the captured bytes are visible.
- **Disposition:** DEFERRED (presentational only). Maintainer-acknowledged as non-blocking at the
  23-08 live-smoke; PROD-IFFT-03 is met as shipped. A small UI follow-up.
