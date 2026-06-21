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
