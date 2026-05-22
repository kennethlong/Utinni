---
phase: 4
slug: tier-2-cli-shim-golden-fixtures
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-22
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Derived from `04-RESEARCH.md` §"Validation Architecture".

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (pinned via Phase 1 D-03) |
| **Config file** | None — SDK-style csproj + xUnit auto-discovery |
| **Quick run command (Tier-1 parsers)** | `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --filter "FullyQualifiedName~FormatsTests" --no-build --configuration Release` |
| **Quick run command (Tier-2 golden)** | `dotnet test Utinni.Cli.Tests/Utinni.Cli.Tests.csproj --no-build --configuration Release` |
| **Full suite command** | `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release && dotnet test Utinni.Cli.Tests/Utinni.Cli.Tests.csproj --no-build --configuration Release` |
| **Estimated runtime** | ~25-40 seconds (Tier 1 ~10s, Tier 2 ~15-30s with golden enumeration) |

---

## Sampling Rate

- **After every task commit:** Run the project-targeted quick command (Tier 1 for parser commits, Tier 2 for CLI surface / fixture commits).
- **After every plan wave:** Run the full suite command (both projects).
- **Before `/gsd:verify-work`:** Full suite green; both CI lanes green on `master`; `actions/upload-artifact@v4` produces no failure artifacts on the gate-PR run.
- **Max feedback latency:** ~40s (full suite). Per-task targeted runs land under 15s.

---

## Per-Task Verification Map

> Tasks are not yet planned — this map populates during planner output and the plan-check loop. The cells below trace each TEST-03 success criterion to its eventual plan + test type. The planner MUST fill the Task ID column when emitting plans.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD-04-01-* | 04-01 | 1 | TEST-03 | T-04-V12 (file IO) | CLI shell + `--help` + unknown-command exit codes | smoke (verb dispatch + exit codes) | `dotnet test Utinni.Cli.Tests --filter "FullyQualifiedName~CommandDispatch"` | ❌ W0 (Plan 04-01) | ⬜ pending |
| TBD-04-01-* | 04-01 | 1 | TEST-03 | — | Stable JSON contract (sorted keys, LF, UTF-8 no BOM, `schemaVersion: 1`) | golden-infrastructure unit | `dotnet test Utinni.Cli.Tests --filter "FullyQualifiedName~JsonOutput"` | ❌ W0 (Plan 04-01) | ⬜ pending |
| TBD-04-01-* | 04-01 | 1 | TEST-03 | — | CI second lane (CLI golden) runs on every push | CI workflow lane | `.github/workflows/ci.yml` job step | ❌ W0 (Plan 04-01) | ⬜ pending |
| TBD-04-02-* | 04-02 | 2 | TEST-03 | T-04-V5 (malformed input), T-04-V12 (file IO), T-04-DoS (chunk-bomb) | TRE parser correctness + bounds defence | unit (Tier 1) | `dotnet test UtinniCoreDotNet.Tests --filter "FullyQualifiedName~TreFileTests"` | ❌ (Plan 04-02) | ⬜ pending |
| TBD-04-02-* | 04-02 | 2 | TEST-03 | — | `parse-tre <path>` stable JSON output | golden (in-process Main + DeepEquals) | `dotnet test Utinni.Cli.Tests --filter "FullyQualifiedName~ParseTreGolden"` | ❌ (Plan 04-02) | ⬜ pending |
| TBD-04-02-* | 04-02 | 2 | TEST-03 | — | `list-objects <ws.iff>` stable JSON output | golden | `dotnet test Utinni.Cli.Tests --filter "FullyQualifiedName~ListObjectsGolden"` | ❌ (Plan 04-02) | ⬜ pending |
| TBD-04-02-* | 04-02 | 2 | TEST-03 (negative) | T-04-V5 | Malformed-magic / truncated record table / unsupported-version → non-zero exit | unit + golden negative case | filter `~TreFileTests~Malformed` + `~ParseTreGolden~Negative` | ❌ (Plan 04-02) | ⬜ pending |
| TBD-04-03-* | 04-03 | 3 | TEST-03 | T-04-V5, T-04-DoS | IFF reader correctness + chunk-bounds defence | unit (Tier 1) | `dotnet test UtinniCoreDotNet.Tests --filter "FullyQualifiedName~IffReaderTests"` | ❌ (Plan 04-03) | ⬜ pending |
| TBD-04-03-* | 04-03 | 3 | TEST-03 | — | `inspect-iff <path>` hierarchical chunk-tree JSON | golden | `dotnet test Utinni.Cli.Tests --filter "FullyQualifiedName~InspectIffGolden"` | ❌ (Plan 04-03) | ⬜ pending |
| TBD-04-03-* | 04-03 | 3 | TEST-03 (negative) | T-04-V5 | Chunk-length-exceeds-file / nested-overflow / unterminated-form → non-zero exit | unit + golden negative case | filter `~IffReaderTests~Malformed` + `~InspectIffGolden~Negative` | ❌ (Plan 04-03) | ⬜ pending |
| TBD-04-04-* | 04-04 | 4 | TEST-03 | T-04-EoP (DLL load) | `validate-plugin <dir>` IPlugin/IEditorPlugin shape compliance | unit (reflection) + golden | filter `~PluginManifestTests` + `~ValidatePluginGolden` | ❌ (Plan 04-04) | ⬜ pending |
| TBD-04-04-* | 04-04 | 4 | TEST-03 | — | createPlugin/destroyPlugin symmetry (Phase 3 R-B D-13/D-14 contract) | golden across 4 sub-fixtures | filter `~ValidatePluginGolden` | ❌ (Plan 04-04) | ⬜ pending |
| n/a | n/a | n/a | TEST-03 | — | WinForms UI continues to function | manual smoke (preserved by CON-N-02 + CON-M-01) | covered by existing Phase 1-3 tests + Phase 6 Tier-4 doc | n/a (preservation) | n/a |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Wave 0 = Plan 04-01 (scaffold). Plan 04-01 MUST land all of the following before any of 04-02/03/04 may start.

- [ ] `Utinni.Cli/Utinni.Cli.csproj` — new sibling project (net472/x86), `<PackageReference>` for CommandLineParser 2.9.1 + Newtonsoft.Json 13.0.3.
- [ ] `Utinni.Cli.Tests/Utinni.Cli.Tests.csproj` — new sibling xUnit 2.9.x project, references `Utinni.Cli` + parser projects.
- [ ] `Utinni.Cli.Tests/Infrastructure/GoldenTestRunner.cs` — JToken.DeepEquals helper with CR/LF normalisation + `actual.json` dump on failure.
- [ ] `Utinni.Cli.Tests/Infrastructure/InProcessCliRunner.cs` — `Console.SetOut` redirect + `Program.Main(argv)` invocation.
- [ ] `Utinni.Cli.Tests/Infrastructure/FixturePath.cs` — `Path.Combine(AppContext.BaseDirectory, "Fixtures", ...)` helper.
- [ ] `Utinni.Cli/Output/JsonOutput.cs` — sorted-key indented JSON (custom `IContractResolver`), `schemaVersion: 1` envelope, LF normalisation on output, UTF-8 without BOM.
- [ ] `Utinni.sln` — adds both project entries with Debug|x86 / Release|x86 configuration mappings.
- [ ] `.github/workflows/ci.yml` — second `dotnet test Utinni.Cli.Tests/...` step + `if: failure()` `actions/upload-artifact@v4` block for `TestResults/**/*.json` + `actual.json`.
- [ ] `.gitattributes` — `*.expected.json text eol=lf` rule (prevents Windows checkout CRLF mangling of golden files).
- [ ] `Utinni.Cli/Program.cs` — CommandLineParser verb dispatch (`parse-tre`, `list-objects`, `inspect-iff`, `validate-plugin` stubs that exit `0`/`1`/`2`/`3` per D-02), `--help` works.
- [ ] Smoke goldens (Plan 04-01): `--help` golden + unknown-command golden + no-args golden.

Framework install: **none** — `dotnet test`, `xunit`, `Microsoft.NET.Test.Sdk` already pinned in `UtinniCoreDotNet.Tests`. Plan 04-01 copies the pin set.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| WinForms UI continues to function under CLI consumer coexistence | TEST-03 (success criterion #3) | UI requires SWG-injected runtime; covered statically by CON-N-02 (`utinni::` thin-wrapper firewall) + CON-M-01 (`IPlugin` SPI) preservation. No new manual carve-out for Phase 4 (D-09). | Existing Phase 1-3 test suite acts as the proxy. Phase 6 Tier-4 doc enumerates the in-process smoke for V1 release. |

*All other phase behaviors have automated verification (Tier 1 parser unit + Tier 2 CLI golden).*

---

## Security Domain Summary

> Full STRIDE table lives in `04-RESEARCH.md` §"Security Domain". The planner MUST include a `<threat_model>` block in each PLAN.md that references the threats below.

| Threat ID | STRIDE | Description | Mitigation Location |
|-----------|--------|-------------|---------------------|
| T-04-V5 | Tampering / DoS | Malformed TRE / IFF (truncated, claimed-length > file, integer overflow in length field) triggers crash or read-past-EOF | Parser bounds checks (`length < 0 → ParseException`, chunk cap at 64 MB) — Plans 04-02 + 04-03 |
| T-04-DoS | DoS | Deflate "zip bomb" in compressed TRE record with extreme expansion ratio | Cap deflated output at 256 MB; document in parser source — Plan 04-02 |
| T-04-V12 | Information Disclosure | Path traversal in `parse-tre`/`inspect-iff` (`../../etc/passwd`, etc.) | Operator privilege boundary (no sandbox in Phase 4); document in `--help`. Future `--sandbox` flag is Phase 6+. |
| T-04-EoP | Elevation of Privilege | `validate-plugin <attacker-dir>` causes `LoadLibrary` of untrusted DLL → executes `DllMain` / static initializers | `validate-plugin --help` warns: "loads each .dll under the given directory; only run against trusted plugin directories." Existing `PluginLoader.cs` per-plugin try/catch (Phase 2 C-06) catches throw-on-ctor. — Plan 04-04 |
| T-04-Json | Code Execution | Newtonsoft.Json `TypeNameHandling.Auto` deserialisation gadget | Phase 4 never deserialises untrusted JSON; CLI emits only. `TypeNameHandling.None` (default) — Plan 04-01 (`JsonOutput.cs`) |

**ASVS Level:** L1 (matches CONTEXT.md / project default).

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies (populates after planner emits plans)
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (CI extension, golden infrastructure, JsonOutput helper, sln + project entries, `.gitattributes`)
- [ ] No watch-mode flags (`dotnet test` runs once and exits)
- [ ] Feedback latency < 40s (full suite estimate)
- [ ] `nyquist_compliant: true` set in frontmatter after planner + plan-checker pass

**Approval:** pending
