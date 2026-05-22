# Phase 4: Tier 2 CLI shim + golden fixtures - Context

**Gathered:** 2026-05-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Stand up a `utinni-cli` executable in the Utinni solution that consumes the same managed core libraries as the WinForms editor, ship at least four commands (`parse-tre`, `list-objects`, `validate-plugin`, `inspect-iff`) backed by checked-in golden-file regression tests, and gate `master` on the golden suite as a second CI lane alongside `dotnet test`. Phase exists to convert ~60-70% of "Kenny please verify" loops into unattended CI runs — the test-harness multiplier this project needs before Wave-1 subpanels (Phases 7-11) can ride on top of a regression-protected framework. Also resolves CON-O-09 (fixture storage) and CON-O-11 (CLI distribution).

Phase 4 produces 4 plans by concern (D-05):

- **Plan 04-01 — Scaffold.** `Utinni.Cli` sibling C# project in `Utinni.sln` (net472/x86 per D-02); `Utinni.Cli.Tests` sibling test project for the golden harness; CommandLineParser 2.x + Newtonsoft.Json nuget pulls; minimal `--help` / no-args / unknown-command surface ships with passing tests; CI workflow update to invoke the golden suite on every push.
- **Plan 04-02 — TRE parser + parse-tre + list-objects.** Pure-C# TRE container reader in `UtinniCoreDotNet/Formats/Tre/` (D-06); `parse-tre <path>` emits sorted-key indented JSON dump (D-04); `list-objects <ws.iff>` uses the TRE reader to walk world-snapshot file paths from a synthesized fixture and emits JSON; golden fixtures + expected JSON committed in-repo (D-03).
- **Plan 04-03 — IFF parser + inspect-iff.** Pure-C# read-only IFF chunk reader in `UtinniCoreDotNet/Formats/Iff/`; `inspect-iff <path>` emits hierarchical chunk-tree JSON; golden fixtures + expected JSON committed in-repo. Phase 8 (per DEC-C4) still owns IFF read/write for editor use — this phase's Iff is the read-only CLI-tier seam.
- **Plan 04-04 — validate-plugin command.** `validate-plugin <dir>` consumes `UtinniCoreDotNet/PluginFramework/PluginLoader.cs` (already exists from Phase 3 R-B; symmetric createPlugin/destroyPlugin lifecycle) to inspect a plugin directory off-process, emits manifest + exports + IPlugin/IEditorPlugin compliance as JSON. Closes the four-command roadmap commitment.

Plan boundaries are CI-gated (same `windows-2022` workflow as Phases 1-3; each plan green on `master` before next starts — Phase 3 D-04 precedent).

**In scope (this phase):**
- The four roadmap commands above with stable JSON output (D-04).
- One or more golden-file regression tests per command (ROADMAP §"Phase 4" success criterion #2).
- Pure-C# parsers for TRE, IFF (read-only), and plugin manifests in `UtinniCoreDotNet/Formats/` (D-06).
- `Utinni.Cli` shipped as a public artifact alongside `Launcher.exe` (D-02; resolves CON-O-11).
- In-repo synthesized fixtures + small (<256KB) real samples in `Utinni.Cli.Tests/Fixtures/` (D-03; resolves CON-O-09).
- CI gate: `master` PRs run both `dotnet test` (Phase 1) and the CLI golden suite (this phase) — ROADMAP §"Phase 4" success criterion #4.
- `docs/ai/assessment.md` "Open questions" rows for CON-O-09 + CON-O-11 updated with disposition pointers to this phase's plan SHAs.
- Per-command Tier-1 xUnit tests against the parser libraries directly (independent of the CLI surface; CON-TT-01 "TDD applies to pure-logic and file-format layers").
- Promote DEC-C3 (tiered testing strategy) from candidate to LOCKED ✓ in PROJECT.md Key Decisions table — Phase 4 closing is the trigger PROJECT.md notes for that promotion.
- Code-review at phase end (`/gsd:code-review 04`) to confirm no new critical findings.

**Out of scope (this phase):**
- **Vendoring swg-client-v2 sources** (D-01) — SOE "All Rights Reserved" headers create a license conflict with Utinni's MIT. swg-client-v2 + Core3 (AGPLv3 — more restrictive, viral) are reference reads only. Zero derivative code.
- **IFF write path** — DEC-C4 locks IFF read/write primitives in `TheJawaToolboxDotNet`/`TheJawaToolbox` (Phase 8); Phase 4's IFF is read-only for CLI inspection. Phase 8 either consumes Phase 4's reader or owns its own — that call belongs to Phase 8.
- **Live-SWG-injection coverage** — CLI runs without SWG; Tier-4 manual residual (TEST-04, Phase 6) covers in-process scenarios.
- **D3D9 / GPU / WinForms surface** — Tier 3 (deferred to V2 per ROADMAP) + Tier 4 (Phase 6 doc).
- **Tier 1 C++ unit tests (Catch2)** — Phase 5 (TEST-02); depends on R-A..R-H seams from Phase 3 plus CI breadth from this phase.
- **The two remaining inherited opens (CON-O-06, CON-O-08)** — Phase 6 STAB-03.
- **LFS infrastructure / 'live-snapshot' fixture tier** — synthesized + tiny real samples carry V1; LFS revisit is a future call (deferred).
- **Verify.Xunit-style approval snapshots** — D-04 chose JSON+DeepEquals; approval workflow rejected as overweight for net472.
- **CommandLine surface contract (semver-style stability guarantee)** — Phase 4 ships a stable JSON output format and a first-draft command surface; promoting to a versioned-API contract is Phase 6 or V2 territory.
- **Cross-repo touch to UtinniPlugins** — Phase 4 has no UtinniPlugins-side work. The next cross-repo touch is Wave-1 (Phases 7+).

</domain>

<decisions>
## Implementation Decisions

### Parser Strategy (resolves cross-repo licensing question)

- **D-01:** **All parsers (TRE, IFF, plugin manifest) are clean reimplementations under Utinni's MIT.** `swg-client-v2/src/engine/shared/library/sharedFile/src/shared/{TreeFile,Iff}.{h,cpp}` (~4.2k LOC, "Portions copyright 1998 Bootprint / 2001-2002 Sony Online Entertainment / All Rights Reserved") and Core3 `MMOCoreORB/src/tre3/TreeFile.cpp` (AGPLv3 — viral, more restrictive than SOE) are read **only as format references**. **Zero derivative code copied.** Walk the source to understand binary layout, edge cases, version differences; write fresh implementations in Utinni's idiom. Implementation owners stay in-fork; no upstream coupling. Largest cost path (~1-2k LOC of new code across both parsers) but the only legally clean path for an MIT fork. Documented in commit messages and `Formats/` directory README.

### CLI Shape + Distribution (resolves CON-O-11)

- **D-02:** **Managed C# sibling project `Utinni.Cli` targeting net472/x86**, matching `UtinniCoreDotNet` exactly (CON-P-02 reaffirms x86; CON-P-01 reaffirms net472). Added to `Utinni.sln` next to `UtinniCoreDotNet.Tests`, `Utinni.LoaderLockHarness`, `Utinni.CrtMatchPlugin`, `Utinni.LegacyPlugin`, `Utinni.CrossCrtFreeFixture` — Phase 1 D-01 + Phase 2 sibling-project pattern. Argv parsing via `CommandLineParser` 2.x (net472-compatible, mature, no exotic feature need). JSON via `Newtonsoft.Json` (already in widespread net472 ecosystem; `System.Text.Json` requires net6+ and is unavailable). **PUBLIC artifact** — ships in release alongside `Launcher.exe` so modders + plugin authors get `validate-plugin` + `inspect-iff` as standalone tools without injecting SWG. Polish budget: real `--help`, exit codes (`0`=ok, `1`=usage, `2`=parse-error, `3`=fixture-not-found), stable JSON surface. CON-O-11 disposition: **public**.

### Fixture Storage (resolves CON-O-09)

- **D-03:** **In-repo synthesized minimal fixtures + small (<256KB) real samples.** Location: `Utinni.Cli.Tests/Fixtures/{tre,iff,plugins}/`. Synthesized fixtures (hand-crafted minimal cases — 3-record TRE, 5-chunk IFF, malformed plugin manifest) are the primary suite and always-run in CI. A handful of small real samples (<256KB each, ripped from a clean SWG install where the format would otherwise be unrealistic) supplement the suite when synthesized-only would miss real-world edge cases — but cap at 256KB per file to keep repo lean. No LFS; no env-var-driven 'bring-your-own' path; no opt-in 'live-snapshot' tier. Synthesized-first policy sidesteps redistribution-rights questions on SWG game assets; the small real samples we do commit must be format-trivial enough to be defensible as "minimal reproducer" rather than "redistributed game content". CON-O-09 disposition: **in-repo synthesized + tiny real samples, no LFS**.

### Golden-File Diff Mechanism

- **D-04:** **Stable JSON output from every CLI command + JToken.DeepEquals diff in tests.** Every command emits sorted-key indented JSON to stdout. Tests load `expected.json` from `Fixtures/<command>/`, run the CLI command (via `Process.Start` for true end-to-end OR direct in-process Main(argv) invocation for speed — planner picks), deserialize both into `JToken`, assert via `JToken.DeepEquals`. On failure: dump both `expected.json` + `actual.json` as CI artifacts via `actions/upload-artifact@v4` + emit a unified diff to xUnit output for fast triage. **Locks the JSON output format** — this is desirable for a public artifact (D-02): output stability is a feature. Format change = breaking change = goldens get re-baselined in the same PR.

### Plan Structuring (D-05) — 4 plans by concern, CI-gated

- **D-05:** Plans grouped by **concern**, not by command. **04-01 scaffold** (project + nuget + CI wire-up + smoke commands) — lowest blast-radius first, validates the structure. **04-02 TRE parser + `parse-tre` + `list-objects`** (commands share the parser). **04-03 IFF parser + `inspect-iff`** (separate parser, single command). **04-04 `validate-plugin`** (no new parser — consumes `PluginLoader.cs` from Phase 3 R-B). CI gate at every plan boundary on `windows-2022` (Phase 1/2/2.1/3 precedent). Plan 04-02 doesn't start until 04-01 is green on `master`; same for 04-03, 04-04. Each command's first commit is the parser-with-tests; second commit is the CLI surface; third commit is the golden fixtures + tests. ~10-14 commits total across the four plans.

### Parser Home (D-06) — Managed, in UtinniCoreDotNet

- **D-06:** **Parsers live managed in `UtinniCoreDotNet/Formats/{Tre,Iff,PluginManifest}/`.** Pure C#; Tier-1 unit-testable directly (no native boundary; CON-TT-01 "TDD applies to pure-logic and file-format layers"). Reusable from `Utinni.Cli` + WinForms editor + future Wave-1 subpanels. **DEC-C4 not violated** — DEC-C4 locks IFF read/write primitives in `TheJawaToolboxDotNet`/`TheJawaToolbox` for editor use; Phase 4's read-only Iff parser is a CLI-tier seam. Phase 8 either consumes Phase 4's reader or owns its own; that call belongs to Phase 8's discuss-phase. **`UtinniCoreDotNet.Tests/FormatsTests/`** absorbs the parser unit tests (Tier 1) — same project as every other managed test in this project (Phase 1 D-01 single-test-project convention). `Utinni.Cli.Tests/` contains only the golden-driven Tier-2 surface tests.

### DEC-C3 Promotion

- **D-07:** **DEC-C3 (tiered testing strategy) promotes from candidate to LOCKED ✓** in `.planning/PROJECT.md` Key Decisions table at Phase 4 close. PROJECT.md row already notes "Promote to ADR when Tier 2 CLI shim lands (Phase 4)." Disposition update commit lands as the final commit of Plan 04-04 or as a roll-up at phase verification. Captures the V1 testing-strategy commitment for V2 consumers.

### Verification Posture (max-harness preserved)

- **D-08:** **Max-harness posture preserved from Phase 2 D-05 / Phase 02.1 D-04 / Phase 3 D-05.** Every command ships with a golden test that would fail if the parser or CLI surface were reverted. Tier-1 unit tests against the parser libraries also ship — independent layer of protection. Per-command harness shape:
  - **parse-tre / list-objects:** synthesized 3-record TRE fixture + small real .tre sample (<128KB); expected JSON committed. Negative cases: malformed magic bytes, truncated record table, unsupported version. Tier-1 parser tests: round-trip a hand-crafted byte sequence; verify record offsets; verify decompression where applicable.
  - **inspect-iff:** synthesized 5-chunk IFF fixture + small real .iff sample (<128KB); expected JSON committed. Negative cases: chunk-length-exceeds-file, nested-chunk-overflow, unterminated form. Tier-1 parser tests: verify FORM/PROP/blob chunk classification; verify big-endian length parsing (IFF is big-endian); verify recursive descent.
  - **validate-plugin:** four sub-fixtures under `Utinni.Cli.Tests/Fixtures/plugins/`: `valid-plugin/` (real `Utinni.CrtMatchPlugin` from Phase 3 R-B), `missing-createplugin/`, `missing-destroyplugin/` (regression for the Phase 3 D-13 ABI contract), `wrong-iplugin-shape/`. Each fixture has an `expected.json` showing pass/fail breakdown. Pure-managed assertion via reflection on assembly metadata — no native loading required.
- **D-09:** **No new Tier-4 carve-outs.** The CLI runs without SWG; everything in scope is statically verifiable. The two existing Tier-4 items (Phase 2 C-01 live injection, Phase 2 C-09 minimize/restore) remain at their original disposition; Phase 4 doesn't introduce new manual-verification residuals.

### Stable JSON Output Contract

- **D-10:** **All JSON output is sorted-key, indented (2 spaces), UTF-8 without BOM, LF line endings, and includes a top-level `{ "schemaVersion": 1, ... }`** key for forward compatibility. CR/LF normalisation happens in the parser before JToken.DeepEquals (test-side) to avoid Windows-vs-Linux line-ending false positives — but the CLI itself always emits LF. The `schemaVersion` field gives us a forward escape hatch if a later phase needs to bump the JSON contract (e.g., adding fields when Wave-1 plugins land).

### CI Integration

- **D-11:** **Extend the existing `.github/workflows/ci.yml`** (Phase 1 D-07), don't add a parallel workflow. New job step after `dotnet test`: `dotnet test Utinni.Cli.Tests/Utinni.Cli.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal"`. On failure: an `if: failure()` step uploads `Utinni.Cli.Tests/TestResults/**/*.json` + any `actual.json` produced by failing tests via `actions/upload-artifact@v4` for triage. Both jobs (xUnit + CLI golden) must be green for `master` to be green. ROADMAP success criterion #4 met.

### Cross-Repo Concerns

- **D-12:** **No cross-repo work.** All Phase 4 deliverables stay in `kennethlong/Utinni`. UtinniPlugins is untouched. The next cross-repo touch is the first Wave-1 plugin in Phase 7 (TJT subpanel — TRE Browser).

### Claude's Discretion

- Exact xUnit test naming (continue Phase 1 D-04 `[Method]_[Scenario]_[ExpectedOutcome]` convention).
- Task ordering WITHIN a plan (planner picks based on dependency).
- Exact `Utinni.Cli` namespace shape (`Utinni.Cli`, `Utinni.Cli.Commands.{ParseTre,ListObjects,ValidatePlugin,InspectIff}`, etc.) — planner names final.
- Whether the golden harness invokes the CLI as `Process.Start` (true end-to-end) or directly through `Program.Main(argv)` in-process (faster, fewer moving parts). Both meet D-04; planner picks based on test-isolation trade-offs.
- Whether each parser's unit tests live in `UtinniCoreDotNet.Tests/FormatsTests/{Tre,Iff,PluginManifest}/` or as flat `TreParserTests.cs` / `IffParserTests.cs` files — planner picks based on test-class growth expectations.
- Whether the `schemaVersion: 1` JSON envelope is per-command-shape or shared at the top level — planner picks at scaffold time.
- Whether plugin manifest validation re-reads `ut.ini` plugin entries (`plugin_NN = enabled, dir`) directly or relies on `PluginLoader.cs`'s existing discovery — likely the latter, but planner audits at task time.
- Exact CommandLineParser nuget version pin (latest stable 2.x at planning time).
- Whether to introduce a `Utinni.Cli.Common` library between `Utinni.Cli` and `UtinniCoreDotNet` (probably not — keep it flat), or to put shared output helpers directly in `Utinni.Cli`.

### Folded Todos

None — `gsd-sdk query todo.match-phase 4` returned zero matches.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project context (locked decisions, requirements, constraints)
- `.planning/PROJECT.md` — V1 milestone scope; anti-goals (DEC-A1..A4); preservation guard-rails (24 load-bearing foundations); DEC-C3 (tiered testing strategy — promotes to LOCKED at Phase 4 close per D-07); DEC-C4 (Wave-1 ships as TJT subpanels — IFF primitives belong to Phase 8 for editor use, but Phase 4's read-only Iff parser is CLI-tier and does not violate the lock).
- `.planning/REQUIREMENTS.md` §TEST-03 — Tier 2 CLI shim with golden fixtures requirement (the one this phase delivers); §STAB-04 — preservation cross-cutting; §STAB-05 — open-question dispositions (CON-O-09 + CON-O-11 mapped to this phase).
- `.planning/ROADMAP.md` §"Phase 4" — phase goal, four-command success criterion, preservation guard-rails (CON-N-02 thin-wrapper firewall stays intact under two consumers; CON-M-01 IPlugin SPI intact under CLI + UI both consuming).
- `.planning/intel/constraints.md` — CON-P-01 (Windows-only desktop), CON-P-02 (x86 in-process), CON-TT-01 (TDD applies to pure-logic + file-format layers — directly governs D-06 Tier-1 parser tests), CON-TT-02 (fixture storage TBD — resolved by D-03), CON-O-09 (resolved by D-03), CON-O-11 (resolved by D-02), CON-L-04 (plugin-side exceptions must not bubble — informs validate-plugin's failure-mode assertions).
- `.planning/intel/decisions.md` — D-08 (tiered testing strategy candidate — Phase 4 close promotes it to ADR per PROJECT.md note).

### Prior-phase carry-forward
- `.planning/phases/01-ci-tier-1-c-scaffold/01-CONTEXT.md` — D-01 sibling test project convention; D-02 `net472`/x86 platform pin; D-03 xUnit 2.9.x; D-04 `[Method]_[Scenario]_[ExpectedOutcome]` test naming; D-07 single `.github/workflows/ci.yml` workflow that this phase extends; D-11 bare-formatting `.editorconfig` (already in place).
- `.planning/phases/02-critical-bug-burn-down-c-01-c-15/02-CONTEXT.md` — D-04 max-harness posture; D-07 single test project absorbs everything; D-09 cross-repo posture (Phase 4 has no cross-repo work, but the precedent stays valid).
- `.planning/phases/02.1-phase-02-gap-closure-critical-correctness-harness-quality-fr/02.1-CONTEXT.md` — D-04 max-harness preserved; D-09 build-real-harnesses-not-Tier-4 rule (directly governs D-08 for Phase 4).
- `.planning/phases/03-strategic-reworks-r-a-r-h/03-CONTEXT.md` — D-04 CI gate at every plan boundary (Phase 4 D-05 inherits this); D-07 single test project absorbs everything (Phase 4 splits into `UtinniCoreDotNet.Tests` for parser units + `Utinni.Cli.Tests` for golden — explicit override); D-13 + D-14 R-B createPlugin/destroyPlugin lifecycle (the contract `validate-plugin` verifies in Plan 04-04); D-22 R-F CppSharp header auto-discovery (not needed by Phase 4 since parsers are managed-only, but explains why we don't bother projecting parsers to native).

### Source documents (immutable inputs from ingest)
- `docs/ai/test-harness-plan.md` §"Tier 2 — CLI shim around the core" — the primary source for this phase; defines the four commands, golden-file pairing, ~60-70% manual-loop conversion target; §"Open questions to resolve at planning time" — origin of CON-O-09 + CON-O-11.
- `docs/ai/assessment.md` §"Open questions" — CON-O-09 + CON-O-11 dispositions land here at phase close.
- `docs/ai/vision.md` — anti-goals (informs validate-plugin's failure modes: a plugin that tries to act as a launcher / server / DCC replacement is out-of-scope per DEC-A1..A4 and validate-plugin may flag it; planner's call).

### Research targets for plan-phase researcher (per user direction 2026-05-23)

**The researcher MUST survey open-source TRE / IFF / SWG-asset tooling for implementation patterns and landmines BEFORE writing RESEARCH.md.** Phase 4 is greenfield parser work and the SWG community has 15+ years of prior art. Targets to look up (non-exhaustive):

- **SWG-Source/swg-main tools** — server-side TRE/IFF tooling; look for non-AGPL utility licenses; identify clean-room references.
- **SWGEmu community tools** — `SwgDataTableTool`, `SwgStringEditor`, `IFFEditor`, `TreeFileExtractor` source if any survives in public repos.
- **ModTheGalaxy / ProjectSWG asset tooling** — community forks may have MIT-licensed parsers worth cross-referencing.
- **Generic IFF readers** — IFF/EA-IFF-85 is a documented format; cross-reference how non-SWG codebases (e.g., classic Amiga/EA tools, image-format projects that consume IFF) handle chunk-tree recursion, length-encoding edge cases, and malformed-input defensiveness.
- **Recent C# implementations** — search for `class IffReader` / `class IffFile` / TRE reader patterns on GitHub; document any net472-compatible ones found.
- **Known landmines** — search for bug reports / postmortems on TRE corruption, IFF chunk-overflow CVE-equivalents, version-skew issues across SWG client builds.

**Output:** RESEARCH.md should include a "Prior art surveyed" section enumerating each candidate codebase / library found, its license, what we can learn from it, and an explicit "code copied: none" disposition statement. This protects D-01's clean-reimplementation contract.

### Reference reads (LICENSE-EXCLUDED — read for format understanding only, copy zero code)

- **`D:/Code/swg-client-v2/src/engine/shared/library/sharedFile/src/shared/`** — SOE/Bootprint sources with "All Rights Reserved" headers. Read `TreeFile.h` (143 LOC), `TreeFile.cpp` (971 LOC), `Iff.h` (1353 LOC), `Iff.cpp` (1753 LOC) as **format reference only**. **DO NOT copy any code, comments, identifier names, or test fixtures from this tree.** Walk the source to understand the binary layout (TRE record table format, IFF FORM/PROP/blob chunk semantics, big-endian length encoding, version differences); write fresh implementations in `UtinniCoreDotNet/Formats/`. Document the read-as-reference disposition in the parser source files' MIT header block: `// Format understood by reading swg-client-v2/src/engine/shared/library/sharedFile/src/shared/{TreeFile,Iff}.{h,cpp} (SOE/Bootprint, All Rights Reserved). No code, comments, or identifier names copied. Implementation original to Utinni under MIT.`
- **`D:/Code/Core3/MMOCoreORB/src/tre3/{TreeFile,TreeFileRecord,TreeDataBlock}.{h,cpp}`** — AGPLv3 (viral, more restrictive than SOE; would force Utinni to relicense if any code copied). Read **only** as a cross-check / secondary format reference. **DO NOT copy any code.** Same MIT-header disposition note as swg-client-v2.

### Codebase intel (read-only reference)
- `.planning/codebase/TESTING.md` — verified zero-baseline + Phase 1 + Phase 2 + Phase 02.1 + Phase 3 incremental adds; Phase 4 continues the trajectory; "Tier 2 — CLI shim around the core" is exactly this phase.
- `.planning/codebase/STRUCTURE.md` §"Directory Layout" + §"Where to Add New Code" — flat-root project layout convention (where `Utinni.Cli/` and `Utinni.Cli.Tests/` go).
- `.planning/codebase/STACK.md` §"Runtime" + §"Testing" — net472/x86 pin; xUnit 2.x.
- `.planning/codebase/INTEGRATIONS.md` §"Native Process Integration" — confirms the CLI does NOT inject; runs off-process. §"C++ ↔ .NET Interop" — confirms CLI consumes managed CppSharp surface, not native directly.
- `.planning/codebase/CONVENTIONS.md` — Allman braces, 4-space indent, PascalCase, MIT header on every C# file; applies to new test code + every new file under `Utinni.Cli/` and `UtinniCoreDotNet/Formats/`.
- `.planning/codebase/CONCERNS.md` — no Phase-4-specific concern; the parser surface is new code, not an existing concern.

### Surface this phase touches

**New projects (sibling-project precedent from Phase 1/2/3):**
- `Utinni.Cli/Utinni.Cli.csproj` — sibling C# project (net472/x86); references `UtinniCoreDotNet` + `CommandLineParser` 2.x + `Newtonsoft.Json`.
- `Utinni.Cli.Tests/Utinni.Cli.Tests.csproj` — sibling xUnit project for golden harness; references `Utinni.Cli` + xUnit 2.9.x.
- `Utinni.sln` — adds two new project entries + Debug|x86 / Release|x86 configuration mappings.

**Existing files modified:**
- `.github/workflows/ci.yml` (Phase 1) — add `dotnet test Utinni.Cli.Tests/...` step + `if: failure()` artifact upload.
- `.planning/PROJECT.md` — DEC-C3 promotes from "Candidate — non-locked" to "LOCKED ✓" at Phase 4 close (D-07).
- `docs/ai/assessment.md` §"Open questions" — CON-O-09 + CON-O-11 disposition rows updated.

**New files (parsers):**
- `UtinniCoreDotNet/Formats/Tre/TreFile.cs` (+ supporting types — record, header, search-priority) — pure C# TRE container reader.
- `UtinniCoreDotNet/Formats/Iff/IffReader.cs` (+ chunk types) — pure C# read-only IFF chunk reader.
- `UtinniCoreDotNet/Formats/PluginManifest/PluginManifestReader.cs` — pure C# reader for `plugin_NN = enabled, dir` entries from `ut.ini`-shaped manifest; consumes from `PluginLoader.cs` discovery where possible.

**New files (CLI surface):**
- `Utinni.Cli/Program.cs` — entry point + CommandLineParser wiring.
- `Utinni.Cli/Commands/{ParseTreCommand,ListObjectsCommand,ValidatePluginCommand,InspectIffCommand}.cs` — one file per command (planner names final).
- `Utinni.Cli/Output/JsonOutput.cs` — sorted-key indented JSON helper with `schemaVersion: 1` envelope.

**New files (parser unit tests — Tier 1):**
- `UtinniCoreDotNet.Tests/FormatsTests/{Tre,Iff,PluginManifest}/<Parser>Tests.cs` — Tier-1 unit tests directly against the parser libraries (no CLI surface).

**New files (golden tests — Tier 2):**
- `Utinni.Cli.Tests/Commands/{ParseTreCommandTests,ListObjectsCommandTests,ValidatePluginCommandTests,InspectIffCommandTests}.cs` — golden-driven Tier-2 surface tests.

**New fixture directories (in-repo per D-03):**
- `Utinni.Cli.Tests/Fixtures/tre/` — synthesized 3-record TRE + small real <128KB sample + `expected.json` files.
- `Utinni.Cli.Tests/Fixtures/iff/` — synthesized 5-chunk IFF + small real <128KB sample + `expected.json` files.
- `Utinni.Cli.Tests/Fixtures/plugins/{valid-plugin,missing-createplugin,missing-destroyplugin,wrong-iplugin-shape}/` — four sub-fixtures + per-fixture `expected.json`.
- `Utinni.Cli.Tests/Fixtures/world-snapshot/` — synthesized minimal ws.iff for `list-objects`.

**Test home split (explicit override of Phase 3 D-07 single-test-project pattern):**
- Tier-1 parser tests → `UtinniCoreDotNet.Tests/FormatsTests/` (same project as every other Tier-1 test).
- Tier-2 CLI golden tests → `Utinni.Cli.Tests/` (sibling project; runs against the built `Utinni.Cli.exe` artifact).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`UtinniCoreDotNet/PluginFramework/PluginLoader.cs`** (Phase 3 R-B updated) — `validate-plugin` reuses the discovery surface; no new manifest-parsing code needed for the directory-walk path.
- **`UtinniCoreDotNet/PluginFramework/{IPlugin,IEditorPlugin}.cs`** — `validate-plugin` reflects on candidate assemblies for these MEF-Export attributes to assert plugin shape compliance.
- **`UtinniCoreDotNet.Tests`** (Phase 1/2/2.1/3) — Tier-1 parser tests land here; `Fixtures/` subdir already exists for plugin fixtures (Phase 3 R-B added `CrtMatchPlugin`, `LegacyPlugin`, `GoodPlugin`, `BrokenPlugin`), so the precedent is established.
- **Phase 3 R-B fixture projects** (`Utinni.CrtMatchPlugin`, `Utinni.LegacyPlugin`) — `validate-plugin` Plan 04-04 can reuse one of these as its "valid plugin" fixture, eliminating duplicate fixture maintenance.
- **CI workflow** (`.github/workflows/ci.yml` from Phase 1) — already runs `msbuild` + `dotnet test` on `windows-2022`; Phase 4 adds a second `dotnet test` invocation for the CLI golden suite. No new workflow needed.
- **Sibling-project pattern** (Phase 1 D-01; Phases 2/2.1/3 sibling fixtures) — `Utinni.Cli/` and `Utinni.Cli.Tests/` slot in cleanly next to existing siblings.

### Established Patterns
- **Atomic commit per task** — GSD executor default; each command spans ~3 commits (parser+tests, CLI surface, golden fixtures).
- **MIT license header on every C# file** — new `Formats/`, `Utinni.Cli/`, `Utinni.Cli.Tests/` files must include the 23-line block.
- **PascalCase project + file naming** (CONVENTIONS.md) — `Utinni.Cli.csproj`, `TreFile.cs`, `ParseTreCommand.cs`, `IffReaderTests.cs`.
- **Allman braces, 4-space, no `_` prefix** — applies to all new code.
- **`net472`/x86 platform pin** (Phase 1 D-02) — non-negotiable for `Utinni.Cli` because it consumes `UtinniCoreDotNet` which is x86-only.
- **`PackageReference` style nuget** (Phase 1) — `Utinni.Cli` and `Utinni.Cli.Tests` both use `<PackageReference>`, not `packages.config`.
- **Failing-test-as-regression-guard** (Phase 1 D-05 precedent with the C-08 Hotkey test) — if any of the parser tests need to ship red ahead of the parser landing (TDD-style), they may; planner's call.

### Integration Points
- **`Utinni.sln`** — adds two new project entries (`Utinni.Cli`, `Utinni.Cli.Tests`) with Debug|x86 / Release|x86 configuration mappings. Project dependency graph: `Utinni.Cli → UtinniCoreDotNet → UtinniCore` (post-build chain CON-T-01 ensures `Generated/UtinniCore.cs` is fresh).
- **`Newtonsoft.Json`** — first nuget dep added to a non-test project in Utinni's main repo. Sets the precedent for managed package consumption outside `UtinniCoreDotNet.Tests`.
- **CI workflow extension** — new `dotnet test Utinni.Cli.Tests` step + `actions/upload-artifact@v4` on failure for `actual.json` triage.
- **`docs/ai/assessment.md`** — CON-O-09 + CON-O-11 disposition rows updated alongside the closing commits of Plans 04-02 (CON-O-09 — fixtures shipped) and 04-01 (CON-O-11 — CLI distribution policy decided at scaffold time).
- **`UtinniPlugins` sister repo** — NO touch in Phase 4. Validation of plugin shape happens against this-repo fixtures only.

### Format-specific knowledge (from reference reads of swg-client-v2 + Core3)
- **TRE container format** is little-endian (verify against swg-client-v2 source); supports multiple versions; record table sits after a fixed header; filenames are deflate-compressed in some versions.
- **IFF chunk format** is big-endian for chunk lengths (4-byte big-endian); FORM/CAT/LIST are container chunks (recurse); PROP and named-blob chunks are leaves. Chunk names are 4 ASCII characters left-padded.
- **Plugin manifest** is the `[Plugins]` section of `ut.ini` with `plugin_NN = enabled, dir` entries (NN zero-padded, sorted ordinal); per-plugin directory must contain `<dir>/<dir>.dll` and may contain `settings.ini` / `input.ini` (Phase 3 R-G `Directory.Build.props` wizard doesn't apply at runtime).

</code_context>

<specifics>
## Specific Ideas

- **CI MUST gate `master` on both lanes (xUnit + CLI golden) before plan close.** Each plan boundary requires both lanes green. ROADMAP §"Phase 4" success criterion #4 makes this explicit.
- **The "JSON output as the contract" framing is load-bearing.** D-04 + D-10 lock the surface; format change = breaking change = goldens re-baselined in the same PR. This is the only way the golden suite stays useful long-term — if the format drifts silently, the goldens become noise.
- **Plan 04-04 (`validate-plugin`) is the smallest by LOC and leans heaviest on Phase 3 R-B's plumbing.** Likely a single-session plan. It also closes the four-command roadmap commitment, so save it for last — landing the long-tail commands first reduces the risk of "we ran out of time before ship".
- **Parser unit tests are independent of golden tests.** A bug in the JSON serialization layer that the goldens catch shouldn't tell us "the parser is broken" — the Tier-1 parser tests must independently verify parser correctness. The two layers protect different surfaces.
- **The schemaVersion envelope is cheap forward compatibility.** Adding it now (1 line of JSON per command) gives every future phase that touches the CLI output a clean way to bump the contract without breaking existing consumers.
- **Tier-1 parser tests against the parser libraries should ship in the SAME commit as the parser itself.** Don't split parser code from parser unit tests across commits — they're one atomic unit. Golden tests for the CLI surface get their own commit.

</specifics>

<deferred>
## Deferred Ideas

- **IFF write path** — Phase 8 (per DEC-C4); read-only Iff in Phase 4 is intentionally narrow.
- **LFS infrastructure + 'live-snapshot' fixture tier** — synthesized + tiny real samples carry V1; if Phase 5 (Catch2) or Phase 7 (TRE Browser) surface needs that synthesized fixtures can't satisfy, revisit then.
- **CLI surface stability ADR** — Phase 4 ships a first-draft surface; promoting to a semver-versioned API contract is Phase 6 / V2 territory.
- **Verify.Xunit-style approval snapshots** — D-04 chose JSON+DeepEquals as the simpler net472-compatible path; revisit if a future phase wants the auto-diff-on-failure UX badly enough.
- **Native C++ CLI variant** — D-02 chose managed; if a future use case demands sub-100ms startup or self-contained binary distribution, native CLI is a Phase 6+ option.
- **Test-harness-internal CLI distribution** — D-02 chose public; if downstream feedback shows the public-surface burden is too high, the CLI can quietly retreat to internal in Phase 6 (lower-risk reversal than the other direction).
- **`Utinni.Cli.Common` shared library** — keep things flat in Phase 4; revisit if Phase 5/6 grows additional CLIs that need shared output helpers.
- **System.CommandLine modernisation** — requires net6+; bundled with the broader net472 → net6+ migration question (V2-class decision per Phase 1 D-03).
- **Coverage tooling (coverlet, ReportGenerator)** — revisit after Phase 5 lands C++ test breadth; Phase 4 alone isn't broad enough to drive that infra.
- **Tier 3 (mock D3D9 + recorded fixtures)** — V2 per ROADMAP.
- **Tier 4 boundary documentation** — Phase 6 STAB-03 (TEST-04 requirement).
- **CON-O-06 (LeksysINI replacement), CON-O-08 (DXSDK vs Windows 10 SDK)** — Phase 6 STAB-03.
- **All Wave-1 subpanels (Phases 7-11)** — downstream of this phase. TRE Browser (Phase 7) and IFF Editor (Phase 8) will consume Phase 4's parsers or own theirs; that's their respective discuss-phase calls.

</deferred>

---

*Phase: 04-tier-2-cli-shim-golden-fixtures*
*Context gathered: 2026-05-23*
