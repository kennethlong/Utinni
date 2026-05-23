---
phase: 04-tier-2-cli-shim-golden-fixtures
fixed_at: 2026-05-23T00:00:00Z
review_path: .planning/phases/04-tier-2-cli-shim-golden-fixtures/04-REVIEW.md
iteration: 1
findings_in_scope: 11
fixed: 11
skipped: 0
status: all_fixed
---

# Phase 4: Code Review Fix Report

**Fixed at:** 2026-05-23T00:00:00Z
**Source review:** `.planning/phases/04-tier-2-cli-shim-golden-fixtures/04-REVIEW.md`
**Iteration:** 1

**Summary:**

- Findings in scope: 11 (5 Critical + 6 Warning; Info findings out of scope per `fix_scope: critical_warning`)
- Fixed: 11 (all CR-* and all WR-*; WR-05 folded into CR-01 per the reviewer's note)
- Skipped: 0
- Status: `all_fixed`

**Verification:**

- Build (`MSBuild Utinni.sln /m /p:Configuration=Release`): SUCCEEDED on VS-2026 toolchain (`D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`). All four C# projects produced their output assemblies (`Utinni.Cli` -> `utinni-cli.exe`, `UtinniCoreDotNet.dll`, `Utinni.Cli.Tests.dll`, `UtinniCoreDotNet.Tests.dll`). C++ projects and Launcher.exe also built. Only pre-existing warnings (xUnit2013 style, MSB8029 temp-dir, MSVC dll-interface) — no new errors or warnings introduced by the fixes.
- Tests (`dotnet test ... --no-build -c Release`): PASSED.
  - `Utinni.Cli.Tests.dll`: 50/50 passed, 0 failed, 0 skipped.
  - `UtinniCoreDotNet.Tests.dll`: 131/131 passed, 0 failed, 0 skipped (on the fixer's environment with full VS-2026 build outputs available; see Caveat below).
  - Total: 181/181 across both assemblies.

**Caveat — test-count environment dependence:** A peer-review pass (Cursor) running `dotnet test --no-build` on the same tree observed `LoaderLockHarness_LoadsUtinniCoreUnderThreshold` in `UtinniCoreDotNet.Tests` fail (130/131). That harness exercises native-load timing on `UtinniCore.dll` and is sensitive to the presence and layout of native artifacts under the test bin output (it is unrelated to any of the 11 Phase 4 fixes). Treat the 131/131 number above as environment-specific; the load-bearing claim is that no Phase 4 fix introduced a regression in `Utinni.Cli.Tests` (50/50) and in the affected `UtinniCoreDotNet.Tests` TRE/IFF subtree.

**Peer review pass (Cursor + Codex):** Both reviewers independently flagged a real bug in the initial CR-03 fix (lock scope too narrow — see CR-03 section for follow-up commit `82aae52`). All other 10 fixes signed off. Out-of-scope follow-ups they recommended: targeted regression fixtures for CR-01 cross-section PE, CR-02 `infoOffset == 0`, and CR-03 concurrent-resolver stress; plus IN-01..IN-04 via a future `--all` pass.

## Fixed Issues

### CR-01: NativeExportProbe per-RVA section resolution

**Files modified:** `Utinni.Cli/Commands/NativeExportProbe.cs`
**Commit:** `3177e84` — `fix(04): CR-01/WR-05 per-RVA section resolution in NativeExportProbe`
**Applied fix:** Introduced a `RvaToFileOffset(PEHeaders, int)` helper that calls `peHeaders.GetContainingSectionIndex(rva)` per RVA and converts using THAT section's `PointerToRawData` / `VirtualAddress`. Replaced the cached `sectionFileOffset + (rva - section.VirtualAddress)` formula at three call sites: (1) the export directory RVA itself (still single-section by spec, but moved through the helper for consistency); (2) the `AddressOfNames` array RVA (line 154); (3) each individual name-string RVA inside the loop (line 171). Dropped the `int sectionIndex` / `var section` / `int sectionFileOffset` caching block. Added a block comment explaining why per-RVA lookup is required (AddressOfNames in .edata vs. name strings in .rdata is a common MSVC layout). WR-05 is subsumed because the new resolver also bounds-checks the names array offset against its own section, eliminating the cross-section-confusion failure mode WR-05 described.
**Verification:** Build passed; `Utinni.Cli.Tests` Tests 1-3 (HasExport positive, HasExport negative, HasExport missing file) and Test 4 (managed DLL — runs against the real `UtinniCoreDotNet.dll` after CR-04) all pass under the new code path. The directory-level test `InspectDirectory_CrtMatchPlugin_ClassifiesAsNativeAndFiltersLoadErrors` (which exercises `createPlugin`/`destroyPlugin` detection on the real CRT plugin DLL) also passes — confirming the helper produces correct offsets for the production layout where the export directory and name table are typically co-located in `.edata`.

### CR-02: drop misleading `infoOffset > 0` guard in TreFile.Open

**Files modified:** `UtinniCoreDotNet/Formats/Tre/TreFile.cs`
**Commit:** `bf43f2d` — `fix(04): CR-02 drop misleading infoOffset>0 guard in TreFile`
**Applied fix:** Changed `if (infoOffset > 0 && infoEnd > streamLength)` to `if (infoEnd > streamLength)`. Now matches the analogous `namesEnd > streamLength` check on line 200-204 (no bypass on offset==0). A malformed TRE with `InfoOffset == 0` and non-zero `infoCompressedSize` (which would overlap the magic/version header) now throws `TreParseException(Truncated)` instead of silently parsing. Added a block comment explaining the regression motive.
**Verification:** Build passed; existing `UtinniCoreDotNet.Tests` TRE truncation fixtures (`TreFileTests`) all pass — no fixture exercised the previously-shadowed `infoOffset == 0` path, but no fixture relied on the bypass either.

### CR-03: ReflectionOnlyAssemblyResolve outermost finally + lock (initial pass + peer-review follow-up)

**Files modified:** `Utinni.Cli/Commands/PluginInspection.cs`
**Commits:**
- `6080066` — initial pass: `fix(04): CR-03 outermost finally + lock for ReflectionOnlyAssemblyResolve handler`
- `82aae52` — peer-review follow-up: `fix(04): CR-03 widen lock to cover full ReflectionOnlyAssemblyResolve probe`

**Initial fix (`6080066`):** Added a `private static readonly object _reflectionOnlyResolveLock = new object();` field on `PluginInspection`. Restructured `InspectSingle` so that (1) the resolver lambda is constructed BEFORE entering any `try` block, (2) `AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += resolver` is wrapped under `lock (_reflectionOnlyResolveLock)`, (3) the reflection body sits inside an outer `try { ... } finally { lock { -= resolver } }`. This addressed the **leak** half of CR-03 (the `-=` now runs on every exit path).

**Peer-review finding (Cursor + Codex):** Both reviewers independently flagged that the initial fix did NOT address the **race** half of CR-03. The lock was held only across `+=` and `-=` as point-in-time operations; the reflection probe between them ran unlocked. Two concurrent `InspectSingle` calls could therefore both have their resolvers installed during the probe. Because `ReflectionOnlyAssemblyResolve` is a multicast event, dependency resolution would dispatch to both handlers — violating the "each test only sees its own resolver" contract CR-03 was meant to establish. Today the resolver lambdas are identical so there is no functional damage, but the contract was false.

**Peer-review follow-up fix (`82aae52`):** Widened the lock to cover the entire add/use/remove span — `+=`, reflection body, and `-=` all sit inside a single `lock (_reflectionOnlyResolveLock) { ... }` critical section. This fully serializes concurrent `InspectSingle` calls under the lock. Reflection-only probes are short (<100 ms typically), so the throughput cost is negligible. The outermost-finally guarantee from the initial pass is preserved (the `-=` finally is now inside the lock, so it still runs on every exit path).

**Verification:** Build passed on both passes. `Utinni.Cli.Tests` Test 7 (`InspectDirectory_WrongIPluginShapeFixture_ManagedWithShapeFail`) exercises the ReflectionOnly path on a real managed fixture DLL and passes after both commits — confirming the resolver registration/unregistration still functions and dependency resolution still proceeds correctly. **Residual caveat:** semantic correctness under cross-collection parallel xunit runs cannot be empirically validated by the current single-collection test runner; the lock prevents a race the runner does not trigger, so the proof is by inspection.

### CR-04: replace silent return with assertion in PluginInspectionTests.Test4

**Files modified:** `Utinni.Cli.Tests/Commands/PluginInspectionTests.cs`
**Commit:** `0128e08` — `fix(04): CR-04 replace silent return with explicit assertion in PluginInspectionTests.Test4`
**Applied fix:** Replaced the `if (!File.Exists(managedDll)) { return; }` block with `Assert.True(File.Exists(managedDll), "Prerequisite UtinniCoreDotNet.dll not found at " + managedDll + ". Ensure the solution is built before running tests.")`. xunit now reports the test as FAILED (not vacuous-PASSED) on any environment where the managed DLL is missing. Test method name retained (the typo `Flase` is IN-01 and is out of scope for this fix pass).
**Verification:** Build passed; `Utinni.Cli.Tests` runs against the Release build where `UtinniCoreDotNet.dll` IS present — the new `Assert.True` passes, then the original `Assert.False(HasExport(...))` runs and passes. Test 4 is now non-vacuous: in the 50/50 run, it executed both assertions.

### CR-05: gate InProcessCliRunner.Run with a static console lock

**Files modified:** `Utinni.Cli.Tests/Infrastructure/InProcessCliRunner.cs`
**Commit:** `c0e3bc3` — `fix(04): CR-05 gate InProcessCliRunner.Run with static console lock`
**Applied fix:** Added `private static readonly object _consoleLock = new object();` and wrapped the entire body of `public static CliResult Run(params string[] args)` in `lock (_consoleLock) { ... }`. The lock spans save-prevOut/prevErr through to return, so the `Console.SetOut`/`Console.SetError` redirection plus the subsequent `Program.Main` call plus the restore are atomic with respect to any other thread also running `Run`. Block comment explains the process-global-singleton race motive.
**Verification:** Build passed; all 17 tests in `Utinni.Cli.Tests` that use `InProcessCliRunner.Run` (`ValidatePluginCommandTests`, `CommandDispatchTests`, `InspectIffCommandTests`, `ListObjectsCommandTests`, `ParseTreCommandTests`, `JsonOutputTests`) pass under the locked path — confirming the lock does not deadlock and the in-process CLI invocation continues to capture stdout/stderr correctly.

### WR-01: defensive copy for uncompressed records in TreFile.GetRecordData

**Files modified:** `UtinniCoreDotNet/Formats/Tre/TreFile.cs`
**Commit:** `bb97146` — `fix(04): WR-01 defensive copy for uncompressed records in TreFile.GetRecordData`
**Applied fix:** Replaced `return compressed;` (which returned the cached `RecordCompressedBytes[index]` directly) with a `Buffer.BlockCopy`-based copy: `var copy = new byte[compressed.Length]; Buffer.BlockCopy(compressed, 0, copy, 0, compressed.Length); return copy;`. Updated the `<para>` xmldoc tag to say "Every call returns a fresh byte[] — callers may mutate the returned array freely" (was: "Callers MUST NOT mutate the returned byte[]. For uncompressed records, the cached byte[] is returned directly").
**Verification:** Build passed; `UtinniCoreDotNet.Tests` TreFile tests (16 tests including the `BuildClaimedGigabyteDeflate` exercise) all pass — the extra `Buffer.BlockCopy` is invisible to callers because they were already required not to mutate the array. The IFF parser (`IffLeafChunk` consumer) also continued to work because its own defensive copy is now redundant but still correct.

### WR-02: remove misleading endianness guard from IffReader.Read

**Files modified:** `UtinniCoreDotNet/Formats/Iff/IffReader.cs`
**Commit:** `5084e2a` — `fix(04): WR-02 remove misleading endianness guard from IffReader.Read`
**Applied fix:** Removed the `if (!BitConverter.IsLittleEndian) throw new NotSupportedException(...)` block at the top of `Read(Stream input)`. Replaced with a block comment explaining that the IFF parser's big-endian reads are explicit via `ReadInt32Be` (manual byte-array shift-and-OR), which is host-endianness-independent — and that the analogous guard in `TreFile.Open` IS load-bearing there because that parser uses `BinaryReader.ReadInt32` (LE). The `TreFile.Open` guard was left untouched.
**Verification:** Build passed; `UtinniCoreDotNet.Tests` IFF parser tests all pass on this little-endian Windows x86 host. The behavior change is only observable on a hypothetical big-endian host (which is out of Phase 4 scope) — the parser there will now succeed instead of throwing.

### WR-03: use `\S` regex to detect non-empty loadErrors array in MaskLoadErrors

**Files modified:** `Utinni.Cli.Tests/Commands/ValidatePluginCommandTests.cs`
**Commit:** `5348fb9` — `fix(04): WR-03 use \\S regex to detect non-empty loadErrors array in MaskLoadErrors`
**Applied fix:** Replaced `string content = match.Groups[1].Value.Trim(); if (string.IsNullOrEmpty(content) || content == "")` with `string content = match.Groups[1].Value; if (!Regex.IsMatch(content, @"\S"))`. Now correctly skips masking for arrays whose entire interior is whitespace-only (e.g., a pretty-printed `[\n      ]`), where the old code's `Trim` + `IsNullOrEmpty` chain would have failed to detect emptiness if any trailing-non-whitespace garbage existed inside the brackets. Block comment notes the redundancy of the old `content == ""` check (strict subset of `IsNullOrEmpty`).
**Verification:** Build passed; `Utinni.Cli.Tests.ValidatePluginCommandTests` (5 tests including all four `[Theory]` sub-fixtures and the `Run_AgainstValidPlugin_LoaderObservedAtTopLevelResult` fact) all pass. The wrong-iplugin-shape fixture row exercises the non-empty masking branch and the other rows exercise the empty-array branch (which now uses `\S` detection).

### WR-04: relax `--help` exit code assertion for CommandLineParser version variance

**Files modified:** `Utinni.Cli.Tests/Commands/CommandDispatchTests.cs`
**Commit:** `718edc2` — `fix(04): WR-04 relax --help exit code assertion to 0 or 1 for CLP version variance`
**Applied fix:** In `Run_WithHelpFlag_ExitsOneAndMatchesHelpGolden`, replaced `Assert.Equal(1, result.ExitCode)` with `Assert.True(result.ExitCode == 0 || result.ExitCode == 1, "Expected --help exit code 0 (CLP success path) or 1 (CLP error path); got " + result.ExitCode)`. The golden file comparison via `GoldenTestRunner.MatchesText("dispatch/help", combined)` remains the load-bearing assertion. Block comment explains the CommandLineParser version inconsistency motive.
**Verification:** Build passed; `CommandDispatchTests` (4 tests) all pass. The installed CommandLineParser version in this checkout produces exit 1 for `--help`, so the disjunction's right branch matches — the test continues to pass on the current toolchain while remaining robust to a future CLP version bump that routes `--help` through the success path.

### WR-05: subsumed by CR-01

**Files modified:** (none directly — already covered by `Utinni.Cli/Commands/NativeExportProbe.cs` in CR-01's commit)
**Commit:** `3177e84` (CR-01's commit)
**Applied fix:** WR-05 ("PeReaderHasExport does not validate that the names array offset fits within the section") is exactly the same code path as CR-01 — the per-RVA `RvaToFileOffset` helper resolves both concerns. The names-array offset is now derived from its OWN containing section's `PointerToRawData` / `VirtualAddress`, with a `GetContainingSectionIndex` check that returns -1 (causing `PeReaderHasExport` to return false) if the RVA points outside every section. No separate commit was needed; the reviewer's `WR-05` note explicitly flagged that it should be folded into the CR-01 fix.
**Verification:** Covered by CR-01's verification — the same test path that exercises CR-01 also exercises the WR-05 concern.

### WR-06: materialize loader.Plugins once before reading Count

**Files modified:** `Utinni.Cli/Commands/PluginInspection.cs`
**Commit:** `0466e7d` — `fix(04): WR-06 materialize loader.Plugins once before reading Count`
**Applied fix:** Verified via grep that `PluginLoader.Plugins` is declared as `IEnumerable<IPlugin>` (a bare IEnumerable field at `UtinniCoreDotNet/PluginFramework/PluginLoader.cs:39`), confirming the materialize-once branch of WR-06 applies (not the "leave as-is" branch). Replaced `PluginsCount = loader.Plugins == null ? 0 : loader.Plugins.Count()` with a two-step materialization: `var pluginsMaterialized = loader.Plugins?.ToList(); ... PluginsCount = pluginsMaterialized?.Count ?? 0`. The `?.ToList()` materializes the sequence exactly once and preserves the null-on-null contract. Block comment notes why bare IEnumerable can be a hazard (lazy or stateful sequence; second enumeration could yield a different count or trigger side effects).
**Verification:** Build passed; `Utinni.Cli.Tests` directory-inspection tests (Tests 5, 6, 7 in `PluginInspectionTests` and the `ValidatePluginCommandTests` theory rows) all pass — confirming `PluginsCount` continues to report the correct value through the new materialization path.

## Skipped Issues

None — all 11 in-scope findings (5 Critical + 6 Warning) were fixed.

The 4 Info-tier findings (IN-01 through IN-04) are explicitly out of scope per `fix_scope: critical_warning`:

- IN-01: typo `Flase` -> `False` in test method name (no behavioral impact).
- IN-02: `TreFileFixtures.BuildClaimedGigabyteDeflate` test fixture-construction pattern (test code only, no production impact).
- IN-03: `SortedKeyContractResolver` caching note (reviewer confirmed correct, no action needed).
- IN-04: `app.config` binding redirect upper bound (latent fragility, not currently triggered).

These can be picked up in a follow-up `--fix --info` pass if desired.

---

_Fixed: 2026-05-23T00:00:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
