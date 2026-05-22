# Phase 4: Tier 2 CLI shim + golden fixtures — Research

**Researched:** 2026-05-22
**Domain:** Greenfield parser work (TRE container, IFF chunk reader, plugin manifest reader) + a managed CLI shim around them, gated by golden-file regression tests in a second CI lane on `master`.
**Confidence:** HIGH for the CLI surface (CommandLineParser + Newtonsoft.Json are both mature net472-compatible MIT libraries); HIGH for IFF (EA-IFF-85 is a 40-year-old public spec); MEDIUM for TRE (open-source format docs are partial — Kenneth Sewell's File Formats doc and the SWGANH wiki are the public records, but the wiki has flaked offline and the doc has redistribution restrictions); HIGH for the harness shape and CI integration (Phase 1-3 precedent + existing infrastructure).

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01 — Clean-room parsers.** TRE, IFF, plugin manifest readers are all clean reimplementations under Utinni's MIT. `swg-client-v2/src/engine/shared/library/sharedFile/src/shared/{TreeFile,Iff}.{h,cpp}` (SOE/Bootprint, All Rights Reserved, ~4.2k LOC) and `Core3/MMOCoreORB/src/tre3/{TreeFile,*}.{h,cpp}` (AGPLv3 — viral) are **format reference only** — zero derivative code copied. Walk to understand binary layout; write fresh implementations in Utinni's idiom. Each parser source file carries the MIT header plus a "format understood by reading … no code copied" disposition note.
- **D-02 — Managed C# sibling `Utinni.Cli` (net472/x86), public artifact.** Sits next to `UtinniCoreDotNet.Tests`, `Utinni.LoaderLockHarness`, `Utinni.CrtMatchPlugin`, `Utinni.LegacyPlugin`, `Utinni.CrossCrtFreeFixture`. CommandLineParser 2.x + Newtonsoft.Json. Public artifact alongside `Launcher.exe`. Exit codes: `0` ok / `1` usage / `2` parse-error / `3` fixture-not-found. CON-O-11 disposition: **public**.
- **D-03 — Fixtures in-repo: synthesized minimal + tiny (<256KB) real samples. No LFS.** Location: `Utinni.Cli.Tests/Fixtures/{tre,iff,plugins,world-snapshot}/`. Synthesized fixtures are primary; small real samples supplement only where synthesized would miss real-world edge cases. CON-O-09 disposition: **in-repo synth + tiny real, no LFS**.
- **D-04 — Stable JSON output + `JToken.DeepEquals` diff.** Every command emits sorted-key indented JSON to stdout. Tests load `expected.json`, run CLI, `JToken.DeepEquals`. On failure: dump both as CI artifacts via `actions/upload-artifact@v4` + emit unified diff to xUnit output. Format change = breaking change = goldens re-baselined in same PR.
- **D-05 — 4 plans by concern, CI-gated.** `04-01` Scaffold → `04-02` TRE + parse-tre + list-objects → `04-03` IFF + inspect-iff → `04-04` validate-plugin. Plan boundaries gated on green CI. ~10-14 commits total.
- **D-06 — Parsers managed in `UtinniCoreDotNet/Formats/{Tre,Iff,PluginManifest}/`.** Pure C#; Tier-1 unit-testable directly. **DEC-C4 NOT violated** — DEC-C4 locks IFF read/write for editor use to TJT; Phase 4's read-only Iff parser is a CLI-tier seam. Tier-1 parser tests live in `UtinniCoreDotNet.Tests/FormatsTests/`; Tier-2 golden tests live in `Utinni.Cli.Tests/`.
- **D-07 — Promote DEC-C3 to LOCKED ✓ at phase close.** PROJECT.md row update lands as final commit of Plan 04-04 or as roll-up at phase verification.
- **D-08 — Max-harness posture preserved.** Every command has a golden test + a Tier-1 parser unit test that would each fail if reverted. Per-command harness shape defined in CONTEXT.md (synthesized 3-record TRE + small real .tre <128KB; synthesized 5-chunk IFF + small real .iff <128KB; four sub-fixtures under `Fixtures/plugins/` for validate-plugin).
- **D-09 — No new Tier-4 carve-outs.** CLI runs without SWG; everything in scope is statically verifiable.
- **D-10 — Stable JSON contract: sorted-key, 2-space indent, UTF-8 no BOM, LF line endings, top-level `{ "schemaVersion": 1, ... }` envelope.** Test-side CR/LF normalisation pre-DeepEquals.
- **D-11 — Extend existing `.github/workflows/ci.yml`.** New step after `dotnet test`. `if: failure()` artifact upload of `actual.json` + `TestResults`.
- **D-12 — No cross-repo work.** All Phase 4 deliverables stay in `kennethlong/Utinni`. UtinniPlugins untouched.

### Claude's Discretion

- xUnit test naming (continue Phase 1 D-04 `[Method]_[Scenario]_[ExpectedOutcome]`).
- Task ordering within a plan.
- Final `Utinni.Cli` namespace shape (`Utinni.Cli`, `Utinni.Cli.Commands.*`).
- Golden harness invocation: `Process.Start("Utinni.Cli.exe", ...)` (true E2E) vs `Program.Main(argv)` in-process (faster). Both meet D-04 — see §"In-process vs subprocess CLI testing".
- Whether parser unit tests live as `FormatsTests/{Tre,Iff,PluginManifest}/<P>Tests.cs` or flat — see §Pitfall 4.
- Whether `schemaVersion: 1` envelope is per-command-shape or shared at top — see §Pitfall 7.
- Whether plugin-manifest validation re-reads `ut.ini` directly or relies on `PluginLoader.cs`'s existing discovery — likely the latter, planner audits at task time (see §"Plugin manifest format").
- Exact CommandLineParser nuget pin (latest stable 2.x — see §"Standard Stack").
- Whether to introduce a `Utinni.Cli.Common` library (probably not — flat).

### Deferred Ideas (OUT OF SCOPE)

- IFF write path (Phase 8).
- LFS / 'live-snapshot' fixture tier.
- CLI surface stability ADR (semver-style guarantee — Phase 6 / V2).
- Verify.Xunit-style approval snapshots.
- Native C++ CLI variant.
- Internal-only CLI distribution (D-02 locks public).
- `Utinni.Cli.Common` shared library.
- `System.CommandLine` modernisation (requires net6+; V2-class).
- Coverage tooling (coverlet, ReportGenerator) — broader Phase 5/6.
- Tier 3 mock-D3D9 (V2 per ROADMAP).
- Tier 4 boundary documentation (Phase 6 STAB-03 / TEST-04).
- CON-O-06 (LeksysINI), CON-O-08 (DXSDK) — Phase 6 STAB-03.
- All Wave-1 subpanels (Phases 7-11).

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TEST-03 | Tier 2 CLI shim with golden fixtures: `utinni-cli` exe in the solution with at least the four commands (`parse-tre`, `list-objects`, `validate-plugin`, `inspect-iff`), each paired with golden-file regression tests. Resolves CON-O-09 (fixture storage) and CON-O-11 (CLI distribution). Acceptance per REQUIREMENTS.md: CLI builds in CI; one golden test per command surface; ~60-70% manual-loop conversion; UI is one of two consumers of core. | Standard Stack (CommandLineParser 2.9.1 + Newtonsoft.Json 13.0.3); Architecture Patterns §"Verb-based command dispatch", §"Golden harness layering"; Common Pitfalls 1-10 cover the verified failure modes; Code Examples cover all four commands' shape; Architectural Responsibility Map confirms parsers belong in the managed core (D-06) and CLI surface in a sibling project (D-02). |

</phase_requirements>

## Summary

Phase 4 is greenfield parser work paired with a managed CLI shim that converts manual UAT into unattended CI runs. The phase is unusually well-scoped: 12 of 12 design decisions are locked in CONTEXT.md, two of the three open-question dispositions (CON-O-09, CON-O-11) are resolved on entry, and the existing CI infrastructure already runs `windows-2022` with the `dotnet test` step Phase 4 will extend.

Three things bear on planning. **First**, the SWG community has 15+ years of TRE/IFF prior art, **but every public reference is license-incompatible with Utinni's MIT clean-room mandate (D-01)** — swg-client-v2 is SOE All Rights Reserved, Core3 and the `swg_tre` Rust crate are AGPL/AGPLv3 (viral), and Swg.Explorer has no detected license at all. The two MIT-licensed prior-art candidates (Wasted Potential Studios' VS Code extensions, MTGUli/TREExplorer) either don't publish source or don't declare a license. The clean-room route is the **only** legally defensible path, and the format itself is well-enough documented in public sources (SWGANH wiki, file-format wikis, Kenneth Sewell's documentation, EA-IFF-85 spec) that no upstream code needs to be touched. **Second**, the JSON-output-as-stable-contract framing (D-04 + D-10) is load-bearing: any drift between command output and committed `expected.json` is a breaking change, and the goldens are exactly the regression guard that breaks loud. **Third**, the test-home split (parser units in `UtinniCoreDotNet.Tests`, golden tests in new sibling `Utinni.Cli.Tests`) is an explicit override of the Phase 3 D-07 single-test-project pattern — the planner must reflect that override in csproj structure (the existing `Utinni.LoaderLockHarness` precedent confirms how to add the second project to `Utinni.sln`).

**Primary recommendation:** Pin **CommandLineParser 2.9.1** and **Newtonsoft.Json 13.0.3**, write parsers in `UtinniCoreDotNet/Formats/{Tre,Iff,PluginManifest}/` against verified-public format documentation only, invoke the CLI **in-process via `Program.Main(argv)` with `Console.SetOut` redirection** for speed and isolation (with one subprocess smoke per command to confirm the exe shape is reachable), and ship `{ "schemaVersion": 1, ... }` as a per-command-shape envelope so future schema bumps don't cascade across the four commands.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| TRE binary container parsing | UtinniCoreDotNet/Formats/Tre/ (managed core) | — | Pure-logic + file-format layer per CON-TT-01; D-06 locks parser home in core; reusable by Cli + WinForms + Phase 7 TJT subpanel. |
| IFF chunk reading (read-only) | UtinniCoreDotNet/Formats/Iff/ (managed core) | TheJawaToolboxDotNet (Phase 8 read+write) | Same reasoning as TRE; DEC-C4 places editor-use read+write primitives in Phase 8 TJT. The two parsers don't compete — Phase 4's reader is CLI-scope, Phase 8 either consumes it or owns its own. |
| Plugin manifest parsing | UtinniCoreDotNet/Formats/PluginManifest/ (managed core) | UtinniCoreDotNet/PluginFramework/PluginLoader.cs (existing) | Existing PluginLoader already does discovery (Phase 3 R-B updated); `validate-plugin` reflects on assemblies via existing loader — the "new" code is the reflection helper, not a new manifest parser. |
| Verb-based CLI command dispatch | Utinni.Cli/ (new sibling exe) | UtinniCoreDotNet/Formats/ (consumed) | Public artifact (D-02). CommandLineParser 2.9.1's verb/MapResult pattern handles dispatch + exit codes in <50 LOC. |
| Stable JSON output serialization | Utinni.Cli/Output/JsonOutput.cs | Newtonsoft.Json 13.0.3 | D-10 envelope (schemaVersion, sorted-key, indented, LF, UTF-8 no BOM). Sorted-key needs a custom `IContractResolver` — see Code Examples §3. |
| Tier-1 parser unit tests | UtinniCoreDotNet.Tests/FormatsTests/ (existing test project) | xUnit 2.9.3 + Microsoft.NET.Test.Sdk 17.13.0 (already pinned) | D-06 explicit: parser tests stay in the existing test project; no project split for Tier 1. |
| Tier-2 CLI golden tests | Utinni.Cli.Tests/ (new sibling test project) | xUnit 2.9.3 + Newtonsoft.Json.Linq | D-06 explicit override: golden tests run against the built CLI artifact, separate sibling. |
| Golden fixture storage | Utinni.Cli.Tests/Fixtures/ | (in-repo, no LFS) | D-03 locked. |
| CI lane 2 (golden suite) | .github/workflows/ci.yml (extend existing) | actions/upload-artifact@v4 (already used) | D-11 explicit: extend, don't replace; one workflow, two test jobs. |
| Plugin assembly reflection (validate-plugin) | Utinni.Cli/ (consumes existing PluginLoader.cs) | System.Reflection (BCL) + MEF (already referenced) | Phase 3 R-B's per-plugin DirectoryCatalog + ImportMany already isolates load failures; `validate-plugin` adds reflection over the loaded assemblies' types to report compliance. No new manifest-parsing code needed for the load path. |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `CommandLineParser` | **2.9.1** | Verb-based command dispatch, `--help` auto-gen, argument parsing, exit-code propagation via `MapResult` | Mature MIT library (Giacomo Stelluti Scala & Contributors); ships .NET Standard 2.0 + net461 TFMs, both consumed by net472. Verb pattern (`[Verb("parse-tre", ...)]` + `ParseArguments<T1,T2,T3,T4>().MapResult(...)`) matches Phase 4's four-command surface exactly. ~30M downloads on NuGet. `[VERIFIED: NuGet flatcontainer nuspec — TFMs net40/net45/net461/netstandard2.0, MIT license confirmed via github.com/commandlineparser/commandline/blob/master/License.md]` |
| `Newtonsoft.Json` | **13.0.3** | JSON serialization (sorted-key, indented), `JToken` for `DeepEquals` comparison in golden tests, `JObject` for assembling envelopes | MIT license; net472 consumes its `net45` TFM. `JToken.DeepEquals` is the canonical golden-file diff primitive on this stack — comparing parsed JTokens rather than raw strings sidesteps whitespace/encoding noise. The `System.Text.Json` alternative requires net6+. `[VERIFIED: NuGet flatcontainer nuspec — net20/net35/net40/net45/netstandard1.0/1.3/2.0/net6.0, MIT license]` |
| `xunit` | **2.9.3** | Test framework | Already pinned in `UtinniCoreDotNet.Tests` (xUnit v3 requires .NET 6+, blocked by net472). `[VERIFIED: existing `UtinniCoreDotNet.Tests.csproj` line 29]` |
| `xunit.runner.visualstudio` | **3.1.5** | xUnit VS test adapter | Already pinned. `[VERIFIED: existing `UtinniCoreDotNet.Tests.csproj` line 30]` |
| `Microsoft.NET.Test.Sdk` | **17.13.0** | Test SDK for `dotnet test` discovery | Already pinned. `[VERIFIED: existing `UtinniCoreDotNet.Tests.csproj` line 28]` |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.IO.Compression` (BCL) | (built-in net472) | Deflate / GZip decompression for TRE compressed records | Only if TRE samples in the fixture suite include compressed records. Synthesized fixtures can use uncompressed records and skip this entirely. `[VERIFIED: BCL]` |
| `System.Reflection` (BCL) | (built-in net472) | Walk plugin assemblies for `IPlugin` / `IEditorPlugin` shape compliance in `validate-plugin` | Plan 04-04. Reuses existing MEF [InheritedExport] discovery from `PluginLoader.cs`. `[VERIFIED: BCL]` |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| CommandLineParser 2.9.1 | `System.CommandLine` 2.0.0 | Microsoft-published, modern, async-aware. **Blocker:** requires .NET 6+, not net472. V2-class decision per Phase 1 D-03 net472 pin. |
| CommandLineParser 2.9.1 | `Mono.Options` (vendored) | Trivially compatible with net472; license-friendly. **Tradeoff:** no verb support out of the box; would re-implement verb routing manually. Reject for Phase 4's four-command surface. |
| Newtonsoft.Json 13.0.3 | `System.Text.Json` 8.x | Modern, lower allocations, sorted-key via `JsonSerializerOptions`. **Blocker:** requires net6+ unless backporting via `System.Text.Json` polyfill nuget (which has caveats on net472 around `Span<T>` performance). V2-class. |
| Newtonsoft.Json 13.0.3 | `Verify.Xunit` snapshot lib | Auto-diff on failure, "approve" workflow. **Tradeoff:** D-04 explicitly rejected as overweight for net472. Verify.Xunit also requires more buy-in (verified files, approval workflow) than the Phase 4 budget supports. |
| `Process.Start("Utinni.Cli.exe")` (true E2E) | `Program.Main(argv)` in-process with `Console.SetOut` redirection | See §"In-process vs subprocess CLI testing" pitfall. Recommendation: **default in-process** for speed + isolation, **one subprocess smoke per command** to verify the exe shape. |

**Installation:**

```xml
<!-- Utinni.Cli/Utinni.Cli.csproj -->
<ItemGroup>
  <PackageReference Include="CommandLineParser" Version="2.9.1" />
  <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="..\UtinniCoreDotNet\UtinniCoreDotNet.csproj" />
</ItemGroup>

<!-- Utinni.Cli.Tests/Utinni.Cli.Tests.csproj -->
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
  <PackageReference Include="xunit" Version="2.9.3" />
  <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="..\Utinni.Cli\Utinni.Cli.csproj" />
  <ProjectReference Include="..\UtinniCoreDotNet\UtinniCoreDotNet.csproj" />
</ItemGroup>
```

**Version verification (run before lock):**

```bash
curl -s "https://api.nuget.org/v3-flatcontainer/commandlineparser/index.json" | head -c 4000   # 2.9.1 confirmed latest stable; 2.9.2-ci-210 is a CI artifact
curl -s "https://api.nuget.org/v3-flatcontainer/newtonsoft.json/index.json"     | head -c 4000  # 13.0.3 stable; 13.0.4 also stable; 13.0.5-beta1 pre-release
```

## Package Legitimacy Audit

> slopcheck installation attempted via `pip install slopcheck --break-system-packages`; tool was not present on this Python install and the install completed without producing a `slopcheck` binary. Per protocol Step 4 graceful-degradation rule, all recommended packages are tagged `[ASSUMED]` and the planner SHOULD gate each install behind a `checkpoint:human-verify` task — except where the package is already in use in the existing repo, in which case the precedent is the verification.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| CommandLineParser 2.9.1 | NuGet | 4+ years (2.9.1 released 2021; v2 line since 2018) | ~50M total | github.com/commandlineparser/commandline | unavailable | **Approved** — verified MIT, nuspec confirms net461 TFM (net472 consumes), used by Roslyn/dotnet-monitor/many MS sample apps. License: file-based MIT verified via raw GitHub README/License.md. |
| Newtonsoft.Json 13.0.3 | NuGet | 3+ years (13.0.3 released 2023; library since 2008) | ~5B total | github.com/JamesNK/Newtonsoft.Json | unavailable | **Approved** — verified MIT, James Newton-King, ubiquitous in net472 ecosystem, no postinstall scripts (it's a managed nuget). |
| xunit 2.9.3 | NuGet | Recent | ~1B total | github.com/xunit/xunit | unavailable | **Already in use** — pinned in existing `UtinniCoreDotNet.Tests.csproj` from Phase 1 D-03. No new install. |
| xunit.runner.visualstudio 3.1.5 | NuGet | Recent | ~600M total | github.com/xunit/visualstudio.xunit | unavailable | **Already in use**. No new install. |
| Microsoft.NET.Test.Sdk 17.13.0 | NuGet | Recent | Microsoft-published | github.com/microsoft/vstest | unavailable | **Already in use**. No new install. |

**Packages removed due to slopcheck [SLOP] verdict:** none (slopcheck unavailable).
**Packages flagged as suspicious [SUS]:** none (slopcheck unavailable). All packages above are widely used, source-repository-backed, and have multi-year track records; the [ASSUMED] tag reflects tool unavailability, not actual suspicion.

**Cross-ecosystem sanity check:** Both `CommandLineParser` and `Newtonsoft.Json` are .NET NuGet packages — not npm/PyPI/crates. Verified via `https://api.nuget.org/v3-flatcontainer/<id>/index.json` (returns version list). NuGet does not have npm-style postinstall scripts; install-time risk is bounded to the .NET assembly being mainboarded into the build.

**Planner directive:** Add a single `checkpoint:human-verify` task after the first `dotnet restore` in Plan 04-01 that confirms `packages.lock.json` lists `CommandLineParser 2.9.1` and `Newtonsoft.Json 13.0.3` (or 13.0.4 if planner bumps) with matching SHA-256 hashes. After that lock-file commit, subsequent plans don't need to re-verify.

## Prior Art Surveyed

> **Required per CONTEXT.md "Research targets for plan-phase researcher (per user direction 2026-05-23)".** This section documents every open-source TRE/IFF/SWG-asset tool surveyed for implementation patterns, its license, and the **explicit "code copied: none" disposition** that protects D-01's clean-reimplementation contract.

| Project | URL | License | Read for | Code Copied |
|---------|-----|---------|----------|-------------|
| **swg-client-v2** sharedFile/TreeFile, sharedFile/Iff | Local: `D:/Code/swg-client-v2/.../sharedFile/src/shared/` | **SOE / Bootprint, "All Rights Reserved"** — incompatible with MIT | Binary layout of TRE container, IFF FORM/PROP/blob semantics, big-endian length parsing, version differences. **For implementer only; researcher did not read.** | **none** |
| **Core3** MMOCoreORB tre3 | github.com/TheAnswer/Core3 (or local clone) | **AGPLv3** — viral, would force Utinni to relicense if any code copied | TRE format cross-check. **For implementer only; researcher did not read.** | **none** |
| **wverkley/Swg.Explorer** (C#, .NET, archived 2022) | github.com/wverkley/Swg.Explorer | **No license detected** (`gh api repos/wverkley/Swg.Explorer/license` returns 404). README acknowledges deps (Be.HexEditor, DDSLib, NAudio, SharpZipLib) but contains no LICENSE statement. | Structural reference for what a C# TRE/IFF parser looks like at the type level. `Wxv.Swg.Common/Files/{TREFile,TREFileReader,IFFFile,IFFFileReader,IFFExtentions}.cs` exist. WebFetch summary of `TREFile.cs` reveals the field set (ResourceCount/InfoOffset/InfoCompression/InfoCompressedSize/InfoSize/NameCompression/NameCompressedSize/NameSize header; TreInfo per-record Name/Checksum/DataSize/DataOffset/DataCompression/DataCompressedSize/NameOffset). `IFFFile.cs` exposes a Node tree with NodeType (Form/Record), Type, Size, Data, Children. These confirm the field set documented elsewhere — useful as a sanity check, **but with no LICENSE we treat it as effectively proprietary and copy nothing.** | **none** |
| **MTGUli/TREExplorer** (C# 98.4%, archived 2022) | github.com/MTGUli/TREExplorer | **No license detected** (`gh api repos/MTGUli/TREExplorer/license` returns 404) | Confirms TRE-Explorer-style read-only TRE viewer was prior art. | **none** |
| **swg_tre** (Rust crate) | lib.rs/crates/swg_tre · docs.rs/swg_tre | **AGPLv3** — viral | Confirms a Rust implementation exists, uses flate2/zlib for deflate. **Not read for any purpose** — AGPL viral. | **none** |
| **Wasted Potential Studios / SWG IFF Viewer** (VS Code ext) | marketplace.visualstudio.com/items?itemName=WastedPotentialStudiosLLC.swg-iff-viewer · swgnb.com/dev-tools/ | **Marketplace listing claims MIT;** but `github.com/Wasted-Potential-Studios/SWG-IFF-Viewer` returns 404 (repo private or different org); WPS dev-tools page provides no GitHub link. | If source were public, would be the only MIT-licensed prior art in the catalogue. As surfaced, **source unavailable**. Format understanding comes from the marketplace description (chunk hierarchy + offsets, DataTable support, embedded strings). | **none** |
| **WPS toolchain** (SWG IFF Deconstructor, Creator, DataTable Editor, String File Editor, TRE Packager — VS Code extensions) | swgnb.com/dev-tools/ | Marketplace metadata; **source not publicly linked** | Confirms a full WPS toolchain exists in the modern community; replicates the same broad scope Utinni's Wave-1 will reach. | **none** |
| **SWG-Source/swg-main** | github.com/SWG-Source/swg-main | (server-side; mixed; check before any copy) | Documented as having `tools/BuildTreeFileVersion.btm` (TreeFileBuilder). Server-side; **not** consulted for client-side TRE/IFF parser code. | **none** |
| **Geit/swg-map-viewer** | github.com/Geit/swg-map-viewer | (not investigated; out of scope — map viewer, not TRE/IFF) | — | **none** |
| **SWGANH Wiki: TRE:TRE Breakdown** | wiki.swganh.org/index.php/TRE:TRE_Breakdown | Wiki documentation (community/CC) — **knowledge is free to use; document derived from this is documentation, not derivative code** | Cited as the format-specification primary source. Wiki was inaccessible during this research session (HTTP 526; Wayback also unavailable); the format details below are cross-confirmed from Swg.Explorer field names + community wiki summary in search results + file-format wikis. | **none** (knowledge only, no code) |
| **EA-IFF-85 specification** | etwright.org/lwsdk/docs/filefmts/eaiff85.html · wiki.amigaos.net/wiki/EA_IFF_85_Standard_for_Interchange_Format_Files · martinreddy.net/gfx/2d/IFF.txt · moddingwiki.shikadi.net/wiki/Interchange_File_Format_(IFF) | Documentation / public standard (1985, Electronic Arts) | **Primary format spec for IFF chunk layout, FORM/LIST/CAT/PROP semantics, big-endian length, alignment/padding.** Public knowledge; **freely citable, not derivative.** | **none** (spec only) |
| **fileformats.archiveteam.org SWG entry** | fileformats.archiveteam.org/wiki/SWG | Wiki documentation (community) | Cross-reference for SWG TRE/IFF format documentation. (Inaccessible during this session — ECONNREFUSED — but used as a documented public source per CONTEXT.) | **none** |
| **Mod the Galaxy** community forum | modthegalaxy.com | Forum / community knowledge | Community discussion archive ("Consolidating .tre files" thread referenced in search). Not read for code; cited as a knowledge source. | **none** |

### Disposition statement (D-01 clean-reimplementation contract)

**Code copied: none.**

Every parser file under `UtinniCoreDotNet/Formats/{Tre,Iff,PluginManifest}/` is original to Utinni and licensed MIT. The implementer reads format references (swg-client-v2, Core3) only for binary-layout understanding; the per-file MIT header gets the standardised disposition line:

```
// Format understood by reading swg-client-v2/src/engine/shared/library/sharedFile/src/shared/{TreeFile,Iff}.{h,cpp}
// (SOE/Bootprint, All Rights Reserved) and the EA-IFF-85 public standard. No code,
// comments, identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.
```

The planner SHOULD add a one-off `checkpoint:human-verify` task at the end of Plan 04-02 and 04-03 confirming the disposition line is present on every new file in `Formats/Tre/` and `Formats/Iff/`. Plan 04-04's `Formats/PluginManifest/` either omits the line (it's not derived from any external source) or carries a simpler "Implementation original to Utinni under MIT" line.

## Format-Specific Knowledge

### TRE container format

> Cross-confirmed from `[CITED: SWGANH wiki TRE:TRE Breakdown summary in search results]` + `[CITED: file-format archive entries]` + Swg.Explorer `TREFile.cs` field names visible via GitHub UI summary (used as documentation cross-reference only — no code copied).

**Magic bytes (header offset 0):** `45 45 52 54` ASCII = `EERT` (the string `TREE` little-endian: `T`(0x54),`R`(0x52),`E`(0x45),`E`(0x45) read as 32-bit LE → `0x45455254`, which spells "EERT" when read byte-by-byte). `[CITED: SWG community sources, multiple]`

**Version field (offset 4, 4 bytes ASCII):** known values include `0005` (v5000), `0006` (v6000), and the variant-handling `0004` (v4000) flagged as "different one that posed additional challenges." `[CITED: SWGANH wiki community summary]` Phase 4 fixtures should at minimum cover **v5000 (the workhorse) + one v4000 + one malformed-version**.

**Byte order:** **little-endian** for integer fields in TRE (`[ASSUMED]` — strongly implied by the magic-bytes endianness and by Swg.Explorer using `BitConverter.ToInt32` without a `BE` suffix in `IOExtensions.cs`; should be confirmed by the implementer against the `swg-client-v2` source). Note this differs from IFF (which is big-endian).

**Header layout (post-magic, post-version):**
1. `int ResourceCount` — number of records in the archive.
2. `int InfoOffset` — byte offset to the records-info block.
3. `int InfoCompression` — compression flag for the info block (0 = uncompressed).
4. `int InfoCompressedSize` — compressed byte size of the info block (= `InfoSize` when uncompressed).
5. `int NameCompression` — compression flag for the names block.
6. `int NameCompressedSize` — compressed byte size of the names block.
7. `int NameSize` — uncompressed byte size of the names block.

(`InfoSize` is **derived**, not stored: `ResourceCount * sizeof(TreInfo) = ResourceCount * 24`. `TreInfo` is 24 bytes wide = 6 ints.)

**Per-record TreInfo struct (24 bytes):**
1. `int DataSize` — uncompressed size of the resource data.
2. `int DataOffset` — byte offset in the file where the resource data begins.
3. `int DataCompression` — compression flag for this record.
4. `int DataCompressedSize` — compressed size (= DataSize when uncompressed).
5. `int Checksum` — data validation value (CRC-like; cross-confirmation needed for the algorithm — `[ASSUMED]` until implementer reads the reference).
6. `int NameOffset` — offset into the names block where this record's filename starts.

**Names block:** Concatenated null-terminated ASCII filenames. Each `TreInfo.NameOffset` points into this block.

**Compression algorithm (per Swg.Explorer dependency on `SharpZipLib`, swg_tre using `flate2/zlib`):** **deflate / zlib** (RFC 1951/1950). When `*Compression == 1`, the block is zlib-deflated; the `*CompressedSize` is the bytes in the file, the `*Size` is the inflated size. `[VERIFIED: cross-confirmed across two independent implementations referencing SharpZipLib and flate2 zlib feature]`

**Lookup semantics:** Case-insensitive filename match (per Swg.Explorer source-file summary). Implementer should default to case-insensitive lookup in the parser API.

**Edge cases the synthesized 3-record fixture should cover:**
1. **Malformed magic bytes** — file beginning with `XXXX` instead of `EERT` should produce a clear failure (`exit 2 parse-error` + JSON error envelope).
2. **Unsupported version** — header version `9999` (or `0000`) should error gracefully, not throw.
3. **Truncated record table** — `ResourceCount = 3` but file ends mid-record-2.
4. **Mixed compression** — record 1 uncompressed, record 2 deflated, record 3 deflated. Confirms the deflate path works without requiring it on every record.

**Edge cases the negative-path fixtures should cover (per D-08):**
- **chunk-length-exceeds-file** (technically an IFF concept, but TRE's `DataSize`/`DataOffset` arithmetic has the same shape).
- **truncated record table** — covered above.
- **unsupported version** — covered above.

### IFF chunk format

> EA-IFF-85 public specification. `[CITED: etwright.org/lwsdk/docs/filefmts/eaiff85.html · wiki.amigaos.net · martinreddy.net/gfx/2d/IFF.txt · moddingwiki.shikadi.net]`

**Chunk shape (all chunks):**
```
+--------+-------------+----------------+-----------+
| 4 bytes| 4 bytes BE  | <length> bytes | 0/1 byte  |
| TypeID | Length      | Data           | pad if odd|
+--------+-------------+----------------+-----------+
```

- **TypeID:** 4 ASCII characters, left-padded with space if the conceptual ID is shorter (e.g. `INFO`, `FORM`, `PROP`, `BODY`). `[CITED: EA-IFF-85]`
- **Length:** 32-bit **big-endian** signed integer specifying byte size of the data, NOT including the 8-byte header. `[CITED: EA-IFF-85 spec, multiple sources]`
- **Data:** raw chunk payload.
- **Pad byte:** If `Length` is odd, a single zero byte follows the data so the next chunk starts on an even file offset (Motorola 68000 word alignment, preserved in the spec). `[CITED: EA-IFF-85 alignment rules]`

**Container vs leaf chunks:**
- **`FORM`** — record-structure container. Data begins with a 4-char sub-type ID (`FORM SHIP` would mean a FORM with sub-ID `SHIP`), then a sequence of nested chunks.
- **`LIST`** — factoring container. Data begins with a 4-char sub-type ID, then a sequence of `PROP` chunks (defaulted properties), then nested group chunks the properties apply to.
- **`CAT `** (note trailing space) — untyped collection container. Data begins with a 4-char sub-type ID, then nested chunks.
- **`PROP`** — properties / defaults shared across a `LIST`'s siblings. Data begins with a 4-char sub-type ID.
- **All other 4-char IDs** are **leaf chunks** (data is opaque to the IFF reader; meaning is application-specific). SWG-specific examples include `INFO`, `DATA`, `BODY`, `NODS`, `NODE`, `OBJT`, etc.

**Parsing algorithm (recursive descent):**

```
def parse_chunk(stream, file_end):
    type_id = stream.read(4)                              # 4 ASCII bytes
    length  = read_int32_big_endian(stream)               # 4 bytes BE
    end_of_chunk = stream.position + length
    if end_of_chunk > file_end:
        raise IffParseError("chunk length exceeds file")
    if type_id in {"FORM", "LIST", "CAT ", "PROP"}:
        sub_id = stream.read(4)                           # container sub-type
        children = []
        while stream.position < end_of_chunk:
            children.append(parse_chunk(stream, end_of_chunk))
        chunk = ContainerChunk(type_id, sub_id, children)
    else:
        data = stream.read(length - 0)                    # leaf payload
        chunk = LeafChunk(type_id, data)
    if length % 2 == 1:
        stream.read(1)                                    # pad byte
    return chunk
```

**Edge cases the synthesized 5-chunk IFF fixture should cover (per D-08):**
1. **Top-level FORM** with two nested leaf chunks + one nested FORM with two leaf children (5 chunks total — exercises recursion).
2. **Odd-length leaf chunk** — confirms the pad byte is consumed correctly so the next chunk's TypeID parses cleanly.
3. **`CAT ` with trailing space** — confirms the four-byte type ID isn't trimmed.

**Negative-path fixtures (per D-08):**
- **chunk-length-exceeds-file** — `FORM` with length 0x7FFFFFFF in a 50-byte file. Parser must error, not allocate/read out of bounds.
- **nested-chunk-overflow** — outer FORM length 40, but inner chunks consume 60 bytes; parser must detect at the recursive step.
- **unterminated form** — outer FORM length 100, file truncates at offset 50; parser must error.

**Endianness pitfall:** TRE is little-endian, IFF is big-endian. Sharing a `BinaryReader` between the two parsers is a bug pattern; the planner should require separate `IffReader` and `TreReader` types with no shared BinaryReader subclass. `[CITED: EA-IFF-85 explicit big-endian + Swg.Explorer's `ReadInt32BE` vs `ReadInt32` naming]`

### Plugin manifest format

> Cross-confirmed from in-repo `data/ut.ini` + `UtinniCore/plugin_framework/plugin_manager.cpp` line 57-83 + existing `UtinniCoreDotNet/PluginFramework/PluginLoader.cs`.

**`[Plugins]` section of `ut.ini`:**
```ini
[Plugins]
plugin_00 = true, TheJawaToolbox
plugin_01 = false, SytnersPlugin
plugin_02 = true, MyOtherPlugin
```

- Key shape: `plugin_NN` where NN is a zero-padded ordinal (defines load order).
- Value shape: `<enabled>, <directoryName>` — comma-separated. `enabled ∈ {true, false}`; `directoryName` is the subdirectory name under `Plugins/`.
- Per-plugin directory contains `<dir>/<dir>.dll`; may contain `settings.ini` and `input.ini` (per CON-M-04 HotkeyManager).

**For `validate-plugin`:** D-02 + CONTEXT.md §"Plugin manifest" both note that **the existing `PluginLoader.cs` already does manifest discovery** (Phase 3 R-B updated). The `validate-plugin` command should:

1. Take a `<dir>` argument pointing at a plugin directory (e.g. `Plugins/MyPlugin/`).
2. Invoke `PluginLoader.Load(<dir>)` (the existing test seam — see `PluginLoader.cs:72`).
3. Read `PluginLoader.LoadErrors` (Phase 2 C-06 testability surface) for any load failures.
4. Use **`System.Reflection`** on the loaded `IEnumerable<IPlugin>` to determine each plugin's:
   - `IPlugin` vs `IEditorPlugin` shape (`IsAssignableFrom`).
   - `[InheritedExport]` attribute presence (sanity check; MEF found them so it must be there).
   - Whether `createPlugin` / `destroyPlugin` exports exist in the native part if it's a hybrid plugin (via `kernel32!GetProcAddress` on the corresponding native DLL — exists from Phase 3 R-B contract D-13/D-14).
5. Emit one JSON object per discovered plugin: `{name, description, author, kind: "managed|hybrid|native", iEditorPluginCompliance: "ok|missing-forms|...", nativeAbiCompliance: "ok|missing-createPlugin|missing-destroyPlugin|n/a"}`.

The four sub-fixtures under `Utinni.Cli.Tests/Fixtures/plugins/` (per D-08):
- **`valid-plugin/`** — reuses `Utinni.CrtMatchPlugin` (Phase 3 R-B fixture; has both `createPlugin` + `destroyPlugin`).
- **`missing-createplugin/`** — fixture DLL that has only `destroyPlugin` (or neither).
- **`missing-destroyplugin/`** — fixture DLL with only `createPlugin` (regression for D-13 ABI contract).
- **`wrong-iplugin-shape/`** — fixture DLL with a class claiming `IEditorPlugin` but missing `GetForms()` or with a throwing ctor.

Each fixture has an `expected.json` showing pass/fail breakdown.

## Architecture Patterns

### System Architecture Diagram

```
USER -- cmd line --> Utinni.Cli.exe
                         |
                         | Parser.Default.ParseArguments<...>(args).MapResult(...)
                         |
              +----------+----------+----------+----------+
              |          |          |          |          |
        ParseTreCmd  ListObjsCmd  InspectIff  ValidatePlugin
              |          |          |          |
              |          |          |          | <- consumes existing PluginLoader.cs from Phase 3 R-B
              v          v          v          |
        UtinniCoreDotNet.Formats.Tre        UtinniCoreDotNet.PluginFramework.PluginLoader
        UtinniCoreDotNet.Formats.Iff   <----
                         |
                         | JObject envelope { "schemaVersion": 1, "command": "...", "result": {...} }
                         v
                  Utinni.Cli.Output.JsonOutput
                  (sorted-key, indented, LF, UTF-8 no BOM)
                         |
                         v
                       stdout
                         |
                         | (test path)
                         v
              JToken.Parse(actual) vs JToken.Parse(expected.json)
              JToken.DeepEquals -> pass/fail
              on fail: dump actual.json + expected.json -> CI artifact

Tier-1 (parser units, independent of CLI):
   UtinniCoreDotNet.Tests/FormatsTests/{Tre,Iff,PluginManifest}/<Parser>Tests.cs
   -> exercises UtinniCoreDotNet.Formats.* directly, no CLI involvement
```

### Recommended Project Structure

```
Utinni.Cli/                                         (new sibling project, net472/x86)
├── Utinni.Cli.csproj
├── Program.cs                                       Entry point + Parser.Default.ParseArguments + MapResult
├── Commands/
│   ├── ParseTreCommand.cs                           [Verb("parse-tre")]
│   ├── ListObjectsCommand.cs                        [Verb("list-objects")]
│   ├── InspectIffCommand.cs                         [Verb("inspect-iff")]
│   └── ValidatePluginCommand.cs                     [Verb("validate-plugin")]
└── Output/
    ├── JsonOutput.cs                                Stable-JSON helpers (envelope, contract resolver)
    └── SortedKeyContractResolver.cs                 IContractResolver for alphabetical key emission

Utinni.Cli.Tests/                                    (new sibling project, net472/x86)
├── Utinni.Cli.Tests.csproj
├── Commands/
│   ├── ParseTreCommandTests.cs                      Tier-2 golden tests (one per command)
│   ├── ListObjectsCommandTests.cs
│   ├── InspectIffCommandTests.cs
│   └── ValidatePluginCommandTests.cs
├── Infrastructure/
│   ├── GoldenTestRunner.cs                          Helper: runs CLI (in-process or subprocess),
│   │                                                normalises CRLF, parses to JToken, DeepEquals,
│   │                                                dumps actual.json on failure for artifact upload.
│   └── FixturePath.cs                               Path helper using AppContext.BaseDirectory
└── Fixtures/                                        Copied to output via `<Content>` with CopyToOutputDirectory="PreserveNewest"
    ├── tre/
    │   ├── synthesized-3record.tre                  hand-crafted 3-record archive
    │   ├── synthesized-3record.expected.json
    │   ├── real-tiny.tre                             <128KB real .tre sample
    │   ├── real-tiny.expected.json
    │   ├── malformed-magic.tre                      negative case (header magic = "XXXX")
    │   ├── malformed-magic.expected.json            { "schemaVersion":1, "error":{ "kind":"BadMagic" } }
    │   ├── truncated.tre
    │   └── truncated.expected.json
    ├── iff/
    │   ├── synthesized-5chunk.iff
    │   ├── synthesized-5chunk.expected.json
    │   ├── real-tiny.iff                             <128KB real .iff sample
    │   ├── real-tiny.expected.json
    │   ├── overflow.iff                              chunk length > file
    │   ├── overflow.expected.json
    │   └── ...
    ├── plugins/
    │   ├── valid-plugin/                             reuses Utinni.CrtMatchPlugin DLL
    │   │   └── expected.json
    │   ├── missing-createplugin/
    │   ├── missing-destroyplugin/
    │   └── wrong-iplugin-shape/
    └── world-snapshot/
        ├── synthesized-ws.iff                       minimal NODS/NODE chunks
        └── synthesized-ws.expected.json

UtinniCoreDotNet/                                    (existing project, extended)
└── Formats/                                         (new — per D-06)
    ├── Tre/
    │   ├── TreFile.cs                                public class — Open(path), Records, GetRecord(name), GetData(name)
    │   ├── TreHeader.cs                              internal struct mirroring the binary header
    │   ├── TreRecord.cs                              internal record metadata
    │   └── TreParseException.cs                      structured error type (kind: BadMagic, UnsupportedVersion, Truncated, ...)
    ├── Iff/
    │   ├── IffReader.cs                              public — Read(path) returns root IffChunk
    │   ├── IffChunk.cs                               base; subclasses ContainerChunk + LeafChunk
    │   └── IffParseException.cs
    └── PluginManifest/
        └── (may be empty if validate-plugin reuses PluginLoader.cs entirely)

UtinniCoreDotNet.Tests/                              (existing project, extended)
└── FormatsTests/                                    (new — Tier-1 parser tests)
    ├── Tre/
    │   ├── TreFileTests.cs                          parser unit tests — hand-crafted byte sequences,
    │   │                                            verify record offsets, decompression
    │   └── TreFileFixtures.cs                       byte-array builders for tests
    ├── Iff/
    │   ├── IffReaderTests.cs                        verify FORM/PROP/leaf classification,
    │   │                                            big-endian length parsing, pad byte handling,
    │   │                                            recursive descent
    │   └── IffReaderFixtures.cs
    └── PluginManifest/
        └── (may be unused if validate-plugin tests live in Utinni.Cli.Tests)
```

### Pattern 1: Verb-based command dispatch with exit-code propagation

**What:** CommandLineParser 2.x's recommended verb pattern: one `[Verb("name", HelpText="...")]` options class per command, `ParseArguments<T1,...,Tn>(args)` returns a `ParserResult`, `MapResult(...)` routes each parsed verb to its handler and produces an `int` exit code.

**When to use:** Phase 4's four-command surface. Locks the public CLI shape.

**Example:**

```csharp
// Source: github.com/commandlineparser/commandline/wiki/Verbs (CommandLineParser 2.9.x README)
using CommandLine;

namespace Utinni.Cli
{
    [Verb("parse-tre", HelpText = "Parse a .tre archive and emit sorted-key JSON to stdout.")]
    public class ParseTreOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .tre file.")]
        public string Path { get; set; }
    }

    [Verb("list-objects", HelpText = "List world-snapshot objects from a ws.iff via the TRE reader.")]
    public class ListObjectsOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to a ws.iff.")]
        public string Path { get; set; }
    }

    [Verb("inspect-iff", HelpText = "Emit the chunk tree of an IFF file as JSON.")]
    public class InspectIffOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .iff file.")]
        public string Path { get; set; }
    }

    [Verb("validate-plugin", HelpText = "Reflect on a plugin directory and report compliance.")]
    public class ValidatePluginOptions
    {
        [Value(0, MetaName = "dir", Required = true, HelpText = "Plugin directory.")]
        public string Dir { get; set; }
    }

    public static class Program
    {
        public static int Main(string[] args)
        {
            return Parser.Default
                .ParseArguments<ParseTreOptions, ListObjectsOptions, InspectIffOptions, ValidatePluginOptions>(args)
                .MapResult(
                    (ParseTreOptions       o) => Commands.ParseTreCommand.Run(o),
                    (ListObjectsOptions    o) => Commands.ListObjectsCommand.Run(o),
                    (InspectIffOptions     o) => Commands.InspectIffCommand.Run(o),
                    (ValidatePluginOptions o) => Commands.ValidatePluginCommand.Run(o),
                    errs => 1);  // exit 1 on usage error
        }
    }
}
```

Each `Commands.*.Run(o)` returns `0` on success, `2` on parse error, `3` on fixture-not-found.

### Pattern 2: Stable-JSON envelope + sorted-key contract resolver

**What:** All command output is wrapped in `{ "schemaVersion": 1, "command": "...", "result": {...} }` (or `{ "schemaVersion": 1, "command": "...", "error": {...} }` on failure). Keys are emitted in alphabetical order via a custom `DefaultContractResolver` that sorts `CreateProperties`'s output. LF line endings, UTF-8 no BOM.

**When to use:** Every command output. Tested via `JToken.DeepEquals` against committed `expected.json`.

**Example:**

```csharp
// Source: newtonsoft.com/json/help/html/contractresolver.htm + code-maze.com/csharp-property-ordering-json-serialization
// + makolyte.com/csharp-serialize-to-json-in-alphabetical-order
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Utinni.Cli.Output
{
    public sealed class SortedKeyContractResolver : DefaultContractResolver
    {
        protected override System.Collections.Generic.IList<JsonProperty>
            CreateProperties(System.Type type, MemberSerialization memberSerialization)
        {
            var props = base.CreateProperties(type, memberSerialization);
            return props.OrderBy(p => p.PropertyName, System.StringComparer.Ordinal).ToList();
        }
    }

    public static class JsonOutput
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new SortedKeyContractResolver(),
        };

        public static int EmitSuccess(string command, object result)
        {
            var env = new JObject
            {
                ["schemaVersion"] = 1,
                ["command"]       = command,
                ["result"]        = JToken.FromObject(result, JsonSerializer.Create(Settings)),
            };
            // SortedKeyContractResolver doesn't sort JObject keys directly (it sorts CLR
            // type properties), so we sort JObject keys explicitly after the fact:
            var sorted = SortJObjectKeys(env);
            // LF line endings, UTF-8 no BOM:
            using (var writer = new System.IO.StreamWriter(System.Console.OpenStandardOutput(),
                                                            new System.Text.UTF8Encoding(false))
                                { NewLine = "\n" })
            {
                writer.Write(sorted.ToString(Formatting.Indented).Replace("\r\n", "\n"));
                writer.Write("\n");
            }
            return 0;
        }

        // JObject sorted recursively
        private static JObject SortJObjectKeys(JObject obj)
        {
            var sorted = new JObject();
            foreach (var kv in obj.Properties().OrderBy(p => p.Name, System.StringComparer.Ordinal))
            {
                JToken v = kv.Value;
                if (v is JObject child)  v = SortJObjectKeys(child);
                if (v is JArray  arr)    v = SortJArrayChildren(arr);
                sorted[kv.Name] = v;
            }
            return sorted;
        }
        // (SortJArrayChildren walks arrays and recurses into JObject children.)
    }
}
```

**Note:** Newtonsoft.Json does NOT have a built-in `SortKeys = true` option (`[VERIFIED: JamesNK/Newtonsoft.Json issue #2270 — feature request open since 2019]`). The `IContractResolver` approach sorts **CLR class properties**; it does NOT sort `JObject` keys built dynamically. For stable output we need both — `IContractResolver` for typed-object serialization paths, and the recursive `SortJObjectKeys` for dynamically-constructed envelopes. Tests should cover both code paths.

### Pattern 3: In-process CLI invocation with stdout redirection

**What:** Tier-2 tests invoke the CLI's `Program.Main(string[] args)` directly inside the test process with `Console.SetOut` redirection to capture stdout. Faster than `Process.Start` (no process startup cost ×30ms × N commands × N fixtures), better stack traces on failure.

**When to use:** All Tier-2 golden tests, EXCEPT for one "smoke" test per command that verifies the actual `.exe` is producible and reachable.

**Example:**

```csharp
public sealed class InProcessCliRunner
{
    public sealed class CliResult
    {
        public int ExitCode { get; set; }
        public string Stdout { get; set; }
        public string Stderr { get; set; }
    }

    public static CliResult Run(params string[] args)
    {
        var prevOut = System.Console.Out;
        var prevErr = System.Console.Error;
        try
        {
            using (var swOut = new System.IO.StringWriter())
            using (var swErr = new System.IO.StringWriter())
            {
                System.Console.SetOut(swOut);
                System.Console.SetError(swErr);
                int code = Utinni.Cli.Program.Main(args);
                return new CliResult
                {
                    ExitCode = code,
                    // Test-side LF normalisation (D-10):
                    Stdout   = swOut.ToString().Replace("\r\n", "\n"),
                    Stderr   = swErr.ToString().Replace("\r\n", "\n"),
                };
            }
        }
        finally
        {
            System.Console.SetOut(prevOut);
            System.Console.SetError(prevErr);
        }
    }
}
```

### Pattern 4: Golden-file comparison via `JToken.DeepEquals` with diff dump

**What:** Load `expected.json` from `Fixtures/<command>/`, run CLI, parse stdout to JToken, deep-compare. On failure, dump both as files in `TestResults/<test-name>/` so `actions/upload-artifact@v4` picks them up.

**When to use:** Every Tier-2 golden test.

**Example:**

```csharp
// Source: newtonsoft.com/json/help/html/M_Newtonsoft_Json_Linq_JToken_DeepEquals.htm
public static class GoldenAssert
{
    public static void Matches(string fixtureName, string actualJson)
    {
        var fixtureDir = System.IO.Path.Combine(
            System.AppContext.BaseDirectory,    // copied to test bin/ via <Content> in csproj
            "Fixtures", fixtureName);
        var expectedPath = System.IO.Path.Combine(fixtureDir, "expected.json");
        var expected = JToken.Parse(System.IO.File.ReadAllText(expectedPath)
                                     .Replace("\r\n", "\n"));
        var actual = JToken.Parse(actualJson);

        if (!JToken.DeepEquals(expected, actual))
        {
            // Dump actual.json next to expected.json for artifact triage
            var dumpDir = System.IO.Path.Combine(
                System.AppContext.BaseDirectory, "TestResults", fixtureName);
            System.IO.Directory.CreateDirectory(dumpDir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dumpDir, "actual.json"), actualJson);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dumpDir, "expected.json"), expected.ToString());

            // Best-effort diff in xUnit output for fast triage
            throw new Xunit.Sdk.XunitException(
                "Golden mismatch for " + fixtureName + ".\n" +
                "Expected:\n" + expected.ToString().Substring(0, System.Math.Min(2000, expected.ToString().Length)) + "\n\n" +
                "Actual:\n"   + actual.ToString().Substring(0,   System.Math.Min(2000, actual.ToString().Length))   + "\n\n" +
                "Full output in TestResults/" + fixtureName + "/{actual,expected}.json — uploaded as CI artifact.");
        }
    }
}
```

### Anti-Patterns to Avoid

- **String-comparing JSON output directly** (`Assert.Equal(expectedString, actualString)`): defeats whitespace tolerance + key-order tolerance + line-ending tolerance. Always parse to `JToken` first.
- **Spawning `Utinni.Cli.exe` via `Process.Start` for every golden test:** ×30ms × N tests = visible CI delay; loses xUnit stack traces; harder to debug. Reserve subprocess for **one smoke per command** that verifies the exe is producible.
- **Reading native TRE/IFF code from `swg-client-v2` and porting "with edits":** violates D-01. Read for understanding, write fresh.
- **Hard-coding fixture paths via `[assembly:...].Location` or `Assembly.GetExecutingAssembly().Location`:** unreliable across net472 deployment scenarios. Use `AppContext.BaseDirectory` + `Fixtures/` (with `<Content CopyToOutputDirectory="PreserveNewest">` in csproj).
- **Sharing a `BinaryReader` between TRE and IFF parsers:** TRE is little-endian, IFF is big-endian. Sharing risks reading TRE field as BE or vice versa. Separate types.
- **Allowing `Newtonsoft.Json` to emit unsorted JObject keys:** the IContractResolver sorts CLR typed properties only. For envelopes built dynamically with `JObject`, run a recursive `SortJObjectKeys` pass before serialization.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Argument parsing + verbs + help text | Custom argv walker with switch/case | **CommandLineParser 2.9.1** | Mature, MIT, handles `--help`, abbreviations, defaults, ParserResult.MapResult exit-code routing. Phase 4 with custom parser is a multi-day reinvention. |
| JSON serialization | `string.Format("{{ \"k\": \"{0}\" }}", v)` | **Newtonsoft.Json 13.0.3** + IContractResolver + recursive JObject sort | Custom emission breaks on escape characters, Unicode, nested structures. JToken.DeepEquals is the golden-test primitive on this stack. |
| Plugin manifest discovery | Hand-rolled `ut.ini [Plugins]` scanner | **Existing `PluginLoader.cs`** (Phase 3 R-B) | Already does per-plugin DirectoryCatalog isolation, MEF discovery, exception isolation, LoadErrors surface. `validate-plugin` consumes it via `Load(<dir>)` overload. |
| Stable-key JSON output | Re-implement key sorting in custom emitter | `DefaultContractResolver.CreateProperties.OrderBy(p => p.PropertyName)` + recursive JObject sort | One-line override; well-known idiom; works against net472. |
| Deflate decompression for compressed TRE records | Vendor `zlib` source | **`System.IO.Compression.DeflateStream`** (BCL) | Built into .NET Framework; matches the wire format swg_tre uses (`flate2/zlib`) and Swg.Explorer uses (SharpZipLib). No nuget needed. |
| CI artifact upload | Custom curl-to-S3 step | **`actions/upload-artifact@v4`** | Already used in existing `.github/workflows/ci.yml`; native GitHub Actions surface; auto-handles retention. |
| Golden file comparison helper | xUnit `Assert.Equal` on raw strings | **`JToken.DeepEquals`** + custom assertion + artifact dump | Key-order and whitespace tolerance for free; better failure messages with our wrapper. |

**Key insight:** Phase 4's actual scope is **format-aware fresh parsers + glue**. Every other concern (argv parsing, JSON, MEF discovery, deflate, artifact upload) has a mature dependency. Resist the urge to "just write a few lines" for any of them — net472 + x86 + MIT compatibility is restrictive enough that re-discovering working choices costs more than depending.

## Common Pitfalls

### Pitfall 1: net472 consumes net461 TFM, not "net472" directly

**What goes wrong:** New developer adds `<PackageReference Include="CommandLineParser" Version="2.9.1" />`, sees the nuspec only lists `net40 / net45 / net461 / netstandard2.0`, panics, switches to a different parser.

**Why it happens:** NuGet's TFM compatibility rules aren't intuitive. **net472 is binary-compatible with the net461 TFM** — net461 is a forward-compatible subset.

**How to avoid:** Trust the TFM list. CommandLineParser 2.9.1's `net461` flavour works in net472 projects. Same for Newtonsoft.Json's `net45` flavour. `[VERIFIED: Microsoft docs on TFM compatibility + existing Utinni precedent (Newtonsoft is implicitly consumed through xunit packages in net472 today via the same compatibility rule)]`.

**Warning signs:** "Type X could not be loaded" exceptions at test discovery time → check binding-redirect file or `<PackageReference>` is correctly set.

### Pitfall 2: `dotnet test` on the mixed C++/C# solution fails

**What goes wrong:** Running `dotnet test Utinni.sln` fails because `dotnet` can't restore `UtinniCore.vcxproj`. This is documented in `dotnet/sdk#9007` and `microsoft/vstest#1129` and was hit by Phase 1.

**Why it happens:** `dotnet` only understands SDK-style csproj/vbproj/fsproj. The `Utinni.sln` has six C++ projects.

**How to avoid:** Invoke `dotnet test` against the specific test project, not the solution: `dotnet test Utinni.Cli.Tests/Utinni.Cli.Tests.csproj`. Phase 1's CI workflow comment (lines 82-83 of `.github/workflows/ci.yml`) documents this explicitly. Phase 4's new CI step follows the same pattern.

**Warning signs:** CI log shows `error : The project file ...UtinniCore.vcxproj cannot be loaded by NuGet (NU1102)` or similar.

### Pitfall 3: Fixture files not copied to `bin/Release/net472/Fixtures/`

**What goes wrong:** Test runs locally pass but CI fails because `expected.json` is not found at `AppContext.BaseDirectory + "/Fixtures/tre/synthesized-3record.expected.json"`.

**Why it happens:** SDK-style csproj does NOT automatically copy non-code files to output. Files must be declared with `<Content>` + `CopyToOutputDirectory="PreserveNewest"` (or via the `<None>` + `CopyToOutputDirectory` pattern). `AppendPlatformToOutputPath=false` (already set in existing `UtinniCoreDotNet.Tests.csproj` line 10) controls path shape; the file-copy directive is orthogonal.

**How to avoid:**

```xml
<ItemGroup>
  <Content Include="Fixtures\**\*" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Be sure the `Fixtures\**` exclude pattern (precedent from existing `UtinniCoreDotNet.Tests.csproj` lines 16-25) is **not** applied to the new `Utinni.Cli.Tests` project — that exclude was specific to fixture-DLL csprojs nested inside `UtinniCoreDotNet.Tests/Fixtures/`. The Phase 4 fixtures are static files (`.tre`, `.iff`, `.json`), not csprojs, and should be included.

**Warning signs:** `FileNotFoundException: 'Could not find file ...\bin\Release\net472\Fixtures\...'`.

### Pitfall 4: Test class layout for parser tests — flat or nested?

**What goes wrong:** Planner discretion item — flat `TreParserTests.cs` / `IffParserTests.cs` vs nested `FormatsTests/Tre/TreFileTests.cs`. Picking the wrong one regrets at Phase 8 / 9 when more parser tests land.

**Why it happens:** Existing `UtinniCoreDotNet.Tests` is flat (everything at the root, e.g. `HotkeyTests.cs`, `PluginLoaderTests.cs`, `PluginManagerLifecycleTests.cs`). Phase 4 adds **at minimum** TRE + IFF + plugin-manifest test families; Phase 8 adds IFF write + format-specific subsystems; Phase 9 adds DataTable. The flat root will sprawl.

**How to avoid:** Use **nested per-format subdirs** (`FormatsTests/Tre/`, `FormatsTests/Iff/`, `FormatsTests/PluginManifest/`) so Phases 8-9 just add sibling folders. Existing Phase 3 tests stay flat; nesting is for the new Formats-prefixed tests only. Folder is a logical grouping (and xUnit doesn't care); namespaces follow.

**Warning signs:** A growing flat root with `Tre*Tests.cs`, `Iff*Tests.cs`, `IffWrite*Tests.cs`, `DataTable*Tests.cs`, ... is the canonical "should have nested 6 months ago" smell.

### Pitfall 5: `JToken.DeepEquals` is too lenient about types

**What goes wrong:** `JToken.DeepEquals` treats `{"n": 1}` and `{"n": "1"}` as different, but it treats `{"n": 1}` and `{"n": 1.0}` as **equal** (both `JTokenType.Float` after JSON.NET's numeric normalization). For format-stability assertions, the type difference matters.

**Why it happens:** JSON's number type is fuzzy; Newtonsoft.Json picks `long` or `double` based on the value at parse time.

**How to avoid:** For Phase 4's stable-output contract, this is **acceptable** — the only int-vs-float concerns are in error envelopes where everything's a string anyway. If a future schema bump adds a sensitive numeric field, the planner can introduce a stricter `JTokenEqualityComparer` (the FluentAssertions.Json project does this). For now, DeepEquals is the right primitive.

**Warning signs:** A golden test passes but a downstream consumer parses the value with the wrong type. (Will not occur in Phase 4's scope.)

### Pitfall 6: CR/LF normalization is mandatory cross-machine

**What goes wrong:** Test passes on developer's WSL/Linux, fails on Windows CI. Or vice versa. The `expected.json` was committed with CRLF, the CLI emits LF, raw string-compare diffs.

**Why it happens:** Git's `core.autocrlf` does CRLF↔LF translation at checkout. Different developers have different settings. Different OS-default line endings.

**How to avoid:** **D-10 already locks this:** CLI emits LF, tests normalize CRLF→LF before `JToken.Parse`. Add a `.gitattributes` rule for `.expected.json` files: `*.expected.json text eol=lf` to force LF in the repo at commit time. CI normalizes regardless.

```gitattributes
# Force LF on golden fixtures (Phase 4 D-10)
Utinni.Cli.Tests/Fixtures/**/*.expected.json text eol=lf
Utinni.Cli.Tests/Fixtures/**/*.json          text eol=lf
```

`.gitattributes` already exists in the repo; the planner adds these lines as part of Plan 04-01.

**Warning signs:** A test passes locally, fails on CI, with the diff being entirely whitespace. Or: the diff window shows `\r\n` ghost characters.

### Pitfall 7: `schemaVersion` envelope shape — per-command vs top-level shared?

**What goes wrong:** Planner picks a top-level shared envelope (`{ "schemaVersion": 1, "command": "...", "result": {...} }`) — and then Phase 8 adds a field to one command's output. Now `schemaVersion = 2` cascades across all four commands and every fixture re-bases.

**Why it happens:** "schemaVersion" is one of those things that always feels global until you actually need to bump it.

**How to avoid:** Use **per-command-shape schemaVersion**. Each command's result has its own `schemaVersion`:

```json
{
  "command": "parse-tre",
  "result": {
    "schemaVersion": 1,
    "header": { ... },
    "records": [ ... ]
  }
}
```

Now Phase 8 bumping `inspect-iff`'s schema bumps only `inspect-iff`'s `schemaVersion` to 2; the other three stay at 1. The `schemaVersion: 1` and `"command"` keys ALWAYS appear; **everything else is in `result`**. This is per CONTEXT.md "Claude's Discretion" item.

**Warning signs:** A second-Phase fixture re-base forced because of a field addition in one command.

### Pitfall 8: `Process.Start` for golden tests inflates CI time and adds flake surface

**What goes wrong:** Planner picks the subprocess pattern for all goldens; CI time grows by ×100ms × ~10 fixtures = noticeable; transient process-start failures (Windows-side `CreateProcess` flake) cause spurious red CI runs.

**Why it happens:** Subprocess "feels more real" — it's actually launching the exe — but for stdout-comparison testing it adds zero verification value over in-process invocation. (Verifying the EXE actually builds and is launchable is what `dotnet build` already does.)

**How to avoid:** Default to **in-process** via `Program.Main(argv)` + `Console.SetOut` redirection. Keep **one subprocess smoke test per command** that calls `Process.Start("Utinni.Cli.exe", "parse-tre <synthesized-path>")` and asserts exit code 0 + stdout begins with `{`. That covers the "is the exe reachable" concern without paying subprocess tax on every fixture.

**Warning signs:** CI wall-clock increases ~5+ seconds on Phase 4; occasional intermittent test failures with "process exited unexpectedly" messages.

### Pitfall 9: Plugin DLL bitness mismatch in fixture loading

**What goes wrong:** `validate-plugin` test loads `Utinni.CrtMatchPlugin.dll` (x86, from Phase 3) — works because `Utinni.Cli.exe` is x86. But a planner trying to share `validate-plugin` test fixtures with `UtinniCoreDotNetGen.exe` (AnyCPU/x64) fails with `BadImageFormatException`.

**Why it happens:** Existing comment in `UtinniCoreDotNet.Tests.csproj` lines 36-43 explicitly documents this: AnyCPU/x64 vs x86 mismatch at JIT-load time.

**How to avoid:** `Utinni.Cli.exe` and `Utinni.Cli.Tests` are both net472/x86. Stay there. Don't reach for `UtinniCoreDotNetGen` (`AnyCPU/x64`) from Phase 4 tests.

**Warning signs:** `BadImageFormatException: Could not load file or assembly` at runtime.

### Pitfall 10: Newtonsoft.Json's sorted-key contract resolver doesn't sort `JObject` keys

**What goes wrong:** Tests assert sorted keys via `IContractResolver`, but the CLI is building output via `new JObject() { ["b"] = 1, ["a"] = 2 }` (dynamic). The resolver sorts CLR-class properties only; `JObject` keys preserve insertion order.

**Why it happens:** It's not documented prominently — `IContractResolver.CreateProperties` is reflection-based, only fires for typed object serialization.

**How to avoid:** When building output via `JObject`, run a recursive `SortJObjectKeys` pass before `.ToString()`. Pattern shown in Code Examples §2 above. Add a Tier-1 test that builds a JObject with unsorted keys and asserts the output is sorted.

**Warning signs:** Two consecutive runs of the same command produce different stdout strings (CLR's `Dictionary<string, ...>` hash randomization manifesting as key-order drift); golden tests are flaky on rebuild.

## Runtime State Inventory

> Phase 4 is greenfield code creation, not a rename / refactor / migration. **This section omitted per protocol.**

(Confirmed nothing in CONTEXT.md or REQUIREMENTS.md triggers a rename pass; no stored database/service/registered-state migration is in scope. The plugin-manifest field set is documented above for `validate-plugin` reflection, not as a renamed contract.)

## Code Examples

> All code examples are paraphrased / synthesized from the cited sources. They illustrate idioms — the planner / executor writes the canonical Utinni-flavoured implementation under MIT.

### Example 1: TRE header parsing (idiom only)

```csharp
// Idiom — implementer rewrites against the canonical Utinni style.
// Format reference: SWGANH wiki + EA-IFF-85 + cross-confirmed from Swg.Explorer field names.
// No code copied from any reference source.
namespace UtinniCoreDotNet.Formats.Tre
{
    internal sealed class TreHeaderReader
    {
        private const uint Magic = 0x45455254; // "EERT" little-endian = 0x45 0x45 0x52 0x54

        public static TreHeader Read(System.IO.BinaryReader br)
        {
            uint magic = br.ReadUInt32();
            if (magic != Magic) throw new TreParseException(TreParseError.BadMagic, "Expected EERT magic.");
            string version = System.Text.Encoding.ASCII.GetString(br.ReadBytes(4));
            if (version != "0005" && version != "0006")
                throw new TreParseException(TreParseError.UnsupportedVersion, $"Version '{version}' not supported by Phase 4.");
            return new TreHeader
            {
                Version = version,
                ResourceCount       = br.ReadInt32(),
                InfoOffset          = br.ReadInt32(),
                InfoCompression     = br.ReadInt32(),
                InfoCompressedSize  = br.ReadInt32(),
                NameCompression     = br.ReadInt32(),
                NameCompressedSize  = br.ReadInt32(),
                NameSize            = br.ReadInt32(),
            };
        }
    }
}
```

### Example 2: IFF chunk recursive descent (idiom only)

```csharp
// Idiom — format reference EA-IFF-85 public spec.
namespace UtinniCoreDotNet.Formats.Iff
{
    public abstract class IffChunk
    {
        public string TypeId { get; internal set; }
    }
    public sealed class ContainerChunk : IffChunk
    {
        public string SubTypeId { get; internal set; }
        public System.Collections.Generic.IReadOnlyList<IffChunk> Children { get; internal set; }
    }
    public sealed class LeafChunk : IffChunk
    {
        public byte[] Data { get; internal set; }
    }

    public sealed class IffReader
    {
        private static readonly System.Collections.Generic.HashSet<string> Containers =
            new System.Collections.Generic.HashSet<string> { "FORM", "LIST", "CAT ", "PROP" };

        public IffChunk Read(System.IO.Stream stream)
        {
            using (var br = new System.IO.BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true))
                return ReadChunk(br, stream.Length);
        }

        private IffChunk ReadChunk(System.IO.BinaryReader br, long fileEnd)
        {
            string typeId = System.Text.Encoding.ASCII.GetString(br.ReadBytes(4));
            int    length = ReadInt32BE(br);
            long   end    = br.BaseStream.Position + length;
            if (end > fileEnd) throw new IffParseException(IffParseError.ChunkLengthExceedsFile,
                $"Chunk '{typeId}' length {length} exceeds file by {end - fileEnd} bytes.");

            IffChunk result;
            if (Containers.Contains(typeId))
            {
                string subId = System.Text.Encoding.ASCII.GetString(br.ReadBytes(4));
                var children = new System.Collections.Generic.List<IffChunk>();
                while (br.BaseStream.Position < end) children.Add(ReadChunk(br, end));
                result = new ContainerChunk { TypeId = typeId, SubTypeId = subId, Children = children };
            }
            else
            {
                byte[] data = br.ReadBytes(length);
                result = new LeafChunk { TypeId = typeId, Data = data };
            }
            if (length % 2 == 1) br.ReadByte();   // pad byte
            return result;
        }

        private static int ReadInt32BE(System.IO.BinaryReader br)
        {
            byte[] b = br.ReadBytes(4);
            return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
        }
    }
}
```

### Example 3: validate-plugin core via existing PluginLoader

```csharp
// Reuses Phase 3 R-B PluginLoader.cs's Load(string pluginDir) test seam.
namespace Utinni.Cli.Commands
{
    public static class ValidatePluginCommand
    {
        public static int Run(ValidatePluginOptions o)
        {
            if (!System.IO.Directory.Exists(o.Dir))
                return Output.JsonOutput.EmitError("validate-plugin", "DirectoryNotFound", "Plugin directory does not exist: " + o.Dir, exitCode: 3);

            var loader = new UtinniCoreDotNet.PluginFramework.PluginLoader(autoLoad: false);
            loader.Load(o.Dir);

            var perPlugin = new System.Collections.Generic.List<object>();
            foreach (var plugin in loader.Plugins)
            {
                var t = plugin.GetType();
                bool isEditor = typeof(UtinniCoreDotNet.PluginFramework.IEditorPlugin).IsAssignableFrom(t);
                // ... reflect on getForms / GetSubPanels for IEditorPlugin compliance ...
                perPlugin.Add(new
                {
                    name           = plugin.Information?.Name        ?? t.Name,
                    description    = plugin.Information?.Description ?? "",
                    author         = plugin.Information?.Author      ?? "",
                    kind           = isEditor ? "editor" : "runtime",
                    iEditorPluginCompliance = isEditor ? CheckEditorShape(plugin) : "n/a",
                });
            }

            return Output.JsonOutput.EmitSuccess("validate-plugin", new
            {
                schemaVersion = 1,
                directory     = o.Dir,
                loadErrors    = loader.LoadErrors,
                plugins       = perPlugin,
            });
        }

        private static string CheckEditorShape(UtinniCoreDotNet.PluginFramework.IPlugin p)
        {
            var ep = p as UtinniCoreDotNet.PluginFramework.IEditorPlugin;
            if (ep == null) return "not-editor-plugin";
            if (ep.GetForms() == null) return "missing-forms";
            // ... more checks per D-08 fixture set ...
            return "ok";
        }
    }
}
```

### Example 4: CI workflow extension (per D-11)

```yaml
# .github/workflows/ci.yml — Phase 4 D-11 extension
# Existing job step "Run tests (net472 / x86)" stays as-is; Phase 4 adds a SECOND step
# invoking the new Utinni.Cli.Tests project. On failure, BOTH test result sets upload
# alongside any actual.json dumps from GoldenAssert.

      - name: Run CLI golden tests (net472 / x86)
        run: dotnet test Utinni.Cli.Tests/Utinni.Cli.Tests.csproj --no-build --configuration Release --logger "console;verbosity=normal" --logger "trx;LogFileName=cli-test-results.trx"
        # Phase 4 D-11: extends existing single CI workflow; same `--no-build` rule
        # (msbuild already produced bin/Release/net472/Utinni.Cli.exe + Utinni.Cli.Tests.dll).

      - name: Upload CLI test artifacts (on failure)
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: cli-test-results
          path: |
            Utinni.Cli.Tests/TestResults/**/*.trx
            Utinni.Cli.Tests/TestResults/**/*.json
            Utinni.Cli.Tests/bin/Release/net472/TestResults/**/*.json
          if-no-files-found: warn
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Manual `argv[1]` switch/case dispatch | `CommandLineParser` `[Verb]` + `MapResult` | 2017-ish for .NET ecosystem; 2.x line stable since 2018 | Eliminates verb-dispatch boilerplate; consistent `--help` autogen; exit-code propagation by pattern. |
| String-comparing JSON output in tests | `JToken.DeepEquals` from Newtonsoft.Json | Universal in modern .NET test suites | Key-order tolerance + whitespace tolerance for free. |
| Hand-rolled IFF parsers per game | EA-IFF-85 + per-game type table | 1985 (literally) | The standard is older than most current engineers. Use it. |
| `System.Configuration` for app config (Microsoft.NET v1) | `appsettings.json` / DI / typed options (.NET Core era) | Doesn't apply — Phase 4 has no app config; ut.ini's [Plugins] section is a domain config, not a host config. | — |
| `Process.Start` for every CLI test | In-process `Program.Main(argv)` with `Console.SetOut` redirection | Best practice in modern .NET test suites | ×30ms × N test savings; better stack traces. |
| `Verify.Xunit` snapshot testing | Stayed with JToken.DeepEquals (D-04) | 2026 onward (Verify ships net6+ primarily) | Verify is overweight for net472; explicit reject per D-04. |
| `System.Text.Json` for JSON | Newtonsoft.Json for net472 (D-02) | net6+ for STJ; net472 stays Newtonsoft | Future migration path is V2-class. |

**Deprecated/outdated:**

- **`Microsoft.VisualStudio.TestTools.UnitTesting`** (MSTest v1): replaced by xUnit / NUnit / MSTest v2. Phase 1 chose xUnit; Phase 4 follows.
- **`System.Configuration` for app config**: not in scope.
- **`packages.config` style nuget**: Phase 1 D-01 chose `<PackageReference>`; Phase 4 follows.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | TRE integers are little-endian | "Format-Specific Knowledge §TRE container format" | Parser misreads every int in the header; tests fail loudly; planner discovers in Plan 04-02. **Mitigation:** synthesized fixture covers a known byte sequence; LE vs BE mistake will fail on the first parsed integer. |
| A2 | TRE's `Checksum` field is CRC-like; algorithm not specified here | "Per-record TreInfo struct" | If implementer needs to **validate** checksums, must determine algorithm from `swg-client-v2/TreeFile.cpp`. **For Phase 4 scope** (parse + dump), Checksum can be reported as a raw uint32 without validation. **Mitigation:** Phase 4 doesn't claim to verify checksums — `parse-tre` reports them; downstream tools can. |
| A3 | TRE supports version `0005` and `0006`; `0004` is a "different" variant | "Format-Specific Knowledge §TRE container format" | Phase 4 may need a v4000 fixture for completeness. **Mitigation:** planner adds a v4000 negative-case fixture (`unsupported-version`); when/if v4000 support is needed downstream, Plan-class work in Phase 7+. |
| A4 | `validate-plugin` reuses existing `PluginLoader.cs` via the `Load(string)` test seam (Phase 3 R-B) without modification | "Plugin manifest format" + Code Examples §3 | If the seam doesn't behave correctly when called from a non-`UtinniCore.dll`-initialized context (e.g. `utinni.GetPath()` returning null), the validation may fail differently than expected. **Mitigation:** PluginLoader.cs's `Load(pluginDir)` overload (lines 121-141) bypasses PluginManager entirely; the production path is only hit when `pluginDir == null`. The test path is the validate-plugin path. CONTEXT.md "Claude's Discretion" item explicitly flags this for planner audit. |
| A5 | The marketplace claim of "MIT" for SWG IFF Viewer is accurate, but source is not retrievable | "Prior Art Surveyed" | None — Phase 4 copies nothing. |
| A6 | CommandLineParser 2.9.1 verb dispatch's exit-code semantics on net472 match the documented behavior on netstandard2.0 | "Standard Stack" + "Architecture Patterns §Pattern 1" | If a verb-dispatch bug surfaces only on net472 (e.g. via the `net461` TFM compatibility shim), tests catch it in Plan 04-01 scaffold smoke. **Mitigation:** Plan 04-01 ships `--help` / no-args / unknown-command smoke tests as Task 2-3, which exercises the verb pipeline end-to-end before any real command is added. |
| A7 | Wikitext format documentation on the SWGANH wiki — when accessible — is more authoritative than Swg.Explorer's reverse-engineered field names | "Prior Art Surveyed" | Discrepancy would only matter if implementer chose to use a field name documented in the wiki that contradicts the binary order. **Mitigation:** Binary order trumps name. Implementer reads the binary layout from the reference sources (swg-client-v2) and uses Utinni-native field names. |
| A8 | The `JToken.DeepEquals` numeric-type fuzziness is acceptable for Phase 4's stable-output contract | "Common Pitfalls §Pitfall 5" | Future schema change introducing a sensitive numeric field could leak through. **Mitigation:** Phase 4's command outputs are dominated by strings and offsets (which DeepEquals treats correctly); numeric type fuzziness is bounded to count fields that are unlikely to surface bugs. |
| A9 | Recommended versions (CommandLineParser 2.9.1, Newtonsoft.Json 13.0.3) are still current at plan-execution time | "Standard Stack" | Pinned versions might be superseded by 13.0.4 / 2.9.2 by Plan 04-01 execution. **Mitigation:** Plan 04-01's first task includes the `npm view` equivalent (`curl https://api.nuget.org/v3-flatcontainer/...`) and updates the pin if newer stable exists. **Both 13.0.3 and 13.0.4 are MIT.** |

**Mitigation summary:** All assumptions are bounded to first-task-of-relevant-plan discovery. None blocks planning. The format-level assumptions (A1, A2, A3) are validated by the synthesized fixture being a known-byte sequence — any LE/BE or algorithm mistake fails the very first parser test.

## Open Questions

1. **TRE v4000 support scope in Phase 4.**
   - What we know: v4000 is documented as "a different one that posed additional challenges" in public summary. v5000 and v6000 are well-understood.
   - What's unclear: Whether Phase 4's fixtures should support v4000 reading, or only document its existence as a negative case.
   - Recommendation: **Negative case only for Phase 4** — `unsupported-version` fixture using version `0004` returns the structured error. v4000 support, if needed for any Phase 7+ TRE Browser use case, becomes a downstream Plan-class concern. Documenting in CONTEXT.md Deferred Ideas section is the right disposition.

2. **`Checksum` validation in `parse-tre`.**
   - What we know: TRE TreInfo has a Checksum field. Algorithm not enumerated in this research.
   - What's unclear: Should `parse-tre` validate Checksums by default, or report them raw?
   - Recommendation: **Report raw** for Phase 4. Validating requires knowing the algorithm (likely a CRC variant or SOE's own hash — see `swg-client-v2/TreeFile.cpp`); adding validation later is a non-breaking feature add (the field becomes "verified": "ok|mismatch" in the JSON output). Phase 4 puts the field through to JSON unmodified.

3. **Should the SWG IFF Viewer MIT extension's source be requested directly?**
   - What we know: Marketplace listing claims MIT; GitHub repo at expected location 404s; WPS dev-tools page provides no link.
   - What's unclear: Whether the extension is actually open source or merely uses MIT-licensed deps.
   - Recommendation: **Don't depend on it for Phase 4.** Format references are sufficient. If a future phase wants to compare implementation approaches, a Discord/email reach-out is the right channel — outside Phase 4 scope.

## Environment Availability

> Phase 4 introduces two new packages (CommandLineParser, Newtonsoft.Json) on top of existing infrastructure. Below is the dependency table.

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET Framework 4.7.2 dev pack | Compile target for Utinni.Cli + Utinni.Cli.Tests | ✓ | (already pinned in Phase 1) | none — non-negotiable |
| net472 runtime (Windows 10+) | Local exec | ✓ | shipped with Win10 1803+ | none |
| msbuild + VS2022 toolset v142 | CI build (windows-2022) | ✓ | (verified in existing ci.yml) | windows-2025 (already future-flagged) |
| `dotnet` CLI | CI + local test invocation | ✓ | (verified in existing ci.yml) | none |
| `dotnet restore` for NuGet packages | Restore CommandLineParser + Newtonsoft.Json on fresh checkout | ✓ | NuGet cached in CI (`actions/cache@v4`) | offline restore from `packages.lock.json` |
| `actions/upload-artifact@v4` | CI artifact upload on golden-test failure | ✓ | (already in workflow) | none |
| `actions/checkout@v4` + `actions/cache@v4` + `microsoft/setup-msbuild@v2` | CI primitives | ✓ | (already pinned) | none |
| `Utinni.CrtMatchPlugin.dll` + `Utinni.LegacyPlugin.dll` (Phase 3 R-B fixtures) | `validate-plugin` golden tests | ✓ | (in existing CopyNativeArtifactsForTests Target — same pattern Phase 4 will reuse) | none — Phase 3 R-B prerequisite is met |
| Live SWG client | (nothing in Phase 4 scope) | ✗ | n/a | n/a — Phase 4 runs without SWG by design (TEST-03 acceptance criteria) |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** none.

## Validation Architecture

> Including this section per protocol — `workflow.nyquist_validation` not explicitly set to `false`; treat as enabled.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (already pinned in `UtinniCoreDotNet.Tests` per Phase 1 D-03) |
| Config file | None — SDK-style csproj + xUnit auto-discovery |
| Quick run command (Tier-1 parser tests) | `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --filter "FullyQualifiedName~FormatsTests" --no-build --configuration Release` |
| Quick run command (Tier-2 golden tests) | `dotnet test Utinni.Cli.Tests/Utinni.Cli.Tests.csproj --no-build --configuration Release` |
| Full suite command | `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release && dotnet test Utinni.Cli.Tests/Utinni.Cli.Tests.csproj --no-build --configuration Release` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| TEST-03 | `utinni-cli` builds in CI and ships at least four commands (`parse-tre`, `list-objects`, `validate-plugin`, `inspect-iff`) | smoke (CLI built) + per-command verb-dispatch tests | `dotnet test Utinni.Cli.Tests --filter "FullyQualifiedName~CommandDispatch"` | ❌ Wave 0 (Plan 04-01) |
| TEST-03 | At least one golden-file regression test per command runs in CI | golden (in-process Main + JToken.DeepEquals) | `dotnet test Utinni.Cli.Tests --filter "FullyQualifiedName~Golden"` | ❌ Plans 04-02 / 04-03 / 04-04 |
| TEST-03 | WinForms UI continues to function | manual smoke (covered by existing tests + Phase 6 Tier-4 doc) | n/a — no new test; preservation of CON-N-02 + CON-M-01 confirmed via existing test suite | n/a |
| TEST-03 | CI runs both `dotnet test` and the CLI golden suite on every push | CI workflow lane | both `dotnet test` invocations in the workflow | ❌ Plan 04-01 (CI extension) |
| TEST-03 (Tier-1 layer) | TRE parser correctness | unit (Tier 1) | `dotnet test UtinniCoreDotNet.Tests --filter "FullyQualifiedName~TreFileTests"` | ❌ Plan 04-02 |
| TEST-03 (Tier-1 layer) | IFF parser correctness | unit (Tier 1) | `dotnet test UtinniCoreDotNet.Tests --filter "FullyQualifiedName~IffReaderTests"` | ❌ Plan 04-03 |
| TEST-03 (Tier-1 layer) | Plugin reflection correctness for `validate-plugin` | unit (Tier 1) | covered by reuse of existing `PluginLoader.cs` tests + Tier-2 golden | ✓ existing PluginLoader tests + ❌ Plan 04-04 |
| TEST-03 (negative paths) | Malformed-magic / truncated TRE / unsupported-version | unit + golden negative case | both Tier-1 and Tier-2 | ❌ Plan 04-02 |
| TEST-03 (negative paths) | Chunk-length-exceeds-file / nested-chunk-overflow / unterminated-form | unit + golden negative case | both Tier-1 and Tier-2 | ❌ Plan 04-03 |
| TEST-03 (CR/LF normalization) | Cross-platform line ending neutrality | golden harness internal | covered by `GoldenAssert.Matches` test-side normalization | ❌ Plan 04-01 |

### Sampling Rate

- **Per task commit:** `dotnet test <project-targeted>` for whichever project the commit touches (Tier 1 for parser commits, Tier 2 for CLI surface commits).
- **Per plan boundary:** full suite (both projects). CI runs both jobs on every PR push to master (D-11).
- **Phase gate (`/gsd:verify-work`):** full suite green; both CI lanes green; `actions/upload-artifact@v4` produces no artifacts on the gate-PR run (an artifact-on-failure means a test failed).

### Wave 0 Gaps

- [ ] `Utinni.Cli/Utinni.Cli.csproj` — new project — Plan 04-01.
- [ ] `Utinni.Cli.Tests/Utinni.Cli.Tests.csproj` — new project — Plan 04-01.
- [ ] `Utinni.Cli.Tests/Infrastructure/{GoldenTestRunner,FixturePath,InProcessCliRunner}.cs` — golden infrastructure — Plan 04-01.
- [ ] `UtinniCoreDotNet/Formats/{Tre,Iff,PluginManifest}/` — new namespace + parsers — Plans 04-02 / 04-03.
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/{Tre,Iff,PluginManifest}/` — new namespace + Tier-1 tests — Plans 04-02 / 04-03 / 04-04.
- [ ] `Utinni.Cli.Tests/Fixtures/{tre,iff,plugins,world-snapshot}/` — golden fixtures — Plans 04-02 / 04-03 / 04-04.
- [ ] `.github/workflows/ci.yml` — extension for second `dotnet test` step + new `actions/upload-artifact@v4` block — Plan 04-01.
- [ ] `.gitattributes` — `.expected.json` text eol=lf rule — Plan 04-01.
- [ ] Framework install: none — `dotnet test`, `xunit`, `Microsoft.NET.Test.Sdk` already pinned.

## Security Domain

> CONTEXT.md does not set `security_enforcement: false` explicitly; treat as enabled. Phase 4 has limited security surface because the CLI reads local file paths supplied by the operator and emits JSON to stdout — no network, no auth, no secrets.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|------------------|
| V2 Authentication | no | n/a — CLI is local-user tool |
| V3 Session Management | no | n/a |
| V4 Access Control | no | n/a — file-system permission inheritance only |
| V5 Input Validation | **yes** | TRE / IFF binary inputs are untrusted; parser must defend against malformed input. **Path argument validation:** reject relative `..` traversal in `parse-tre <path>` only if the binary requires a sandbox (Phase 4: no sandbox; rely on OS file-permission semantics). |
| V6 Cryptography | no | n/a — TRE Checksum is reported as a raw int, not validated as crypto |
| V8 Data Protection | minimal | The CLI writes only to stdout (and to `TestResults/` in test mode). No data at rest. |
| V12 File and Resources | **yes** | File-system access via `File.OpenRead(path)` is the CLI's primary I/O. Stream-size limits + chunk-length sanity checks (Pitfall §IFF and §TRE) are the user-facing controls. |

### Known Threat Patterns for {C# + net472 + binary file parsing}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malformed TRE/IFF triggering allocation of attacker-controlled gigabyte chunk | Denial of Service | Cap chunk/record allocations at a sane upper bound (e.g. 64 MB per chunk; fail with `IffParseException` if a single chunk's length field exceeds that). Phase 4 fixtures cover the "claimed length > file size" case explicitly. |
| Integer overflow in `length` field causing read-past-EOF or negative-size allocation | Tampering / DoS | Use signed `int` for IFF length (per spec — big-endian signed); explicitly check `length < 0 → IffParseException`. Same for TRE. |
| Deflate "zip bomb" — attacker-controlled compressed TRE record with extreme expansion ratio | DoS | Cap deflated output at `<configurable-max-record-size>` (default 256 MB). Phase 4 fixtures don't exercise this (synthesized = uncompressed, real-tiny <128 KB), but the protection in the parser is documentation-supported best practice. |
| Path traversal in `parse-tre ../../../etc/passwd` (Unix) or `..\\..\\etc\\passwd` (Windows) | Information Disclosure | Phase 4: CLI is run by the operator with their own privileges; no sandbox. File system permissions are the boundary. If an external embedding ever needs sandboxed CLI invocation, that's a Phase 6+ feature. |
| Plugin DLL load executes attacker-controlled code via `validate-plugin <attacker-dir>` | Elevation of Privilege | **Significant.** `LoadLibrary` of an untrusted DLL runs that DLL's `DllMain` (native) or its module ctors / static initializers (managed). Mitigation: `validate-plugin` MUST document this in its `--help` text: "loads each .dll under the given directory; only run against trusted plugin directories." Phase 4 doesn't sandbox this. **Optional Phase 6+ uplift:** Add a `--sandbox` flag that uses `AssemblyLoadContext` (or ReflectionOnlyLoadFrom for net472 backport) to inspect without executing. |
| Newtonsoft.Json polymorphic deserialization (`TypeNameHandling`) | Code Execution | Phase 4 **never deserializes untrusted JSON input** — it only emits JSON. Newtonsoft's `TypeNameHandling.None` (default) is what we use. `[VERIFIED: JamesNK/Newtonsoft.Json security advisory history — TypeNameHandling.None is safe]`. |

**Planner directive:** Include a one-line warning in `validate-plugin`'s `--help` text noting that the command executes plugin code as a side effect of MEF discovery. Existing `PluginLoader.cs` has per-plugin try/catch isolation (Phase 2 C-06), so a throwing-on-ctor plugin is logged and skipped, but DLL load is still happening. The warning is documentation; the behavior is intentional (mirrors live editor).

## Sources

### Primary (HIGH confidence)

- **EA-IFF-85 specification** (multiple authoritative mirrors):
  - http://www.etwright.org/lwsdk/docs/filefmts/eaiff85.html
  - https://wiki.amigaos.net/wiki/EA_IFF_85_Standard_for_Interchange_Format_Files
  - https://www.martinreddy.net/gfx/2d/IFF.txt
  - https://moddingwiki.shikadi.net/wiki/Interchange_File_Format_(IFF)
- **NuGet flatcontainer (verified package metadata):**
  - https://api.nuget.org/v3-flatcontainer/commandlineparser/2.9.1/commandlineparser.nuspec
  - https://api.nuget.org/v3-flatcontainer/newtonsoft.json/13.0.3/newtonsoft.json.nuspec
- **CommandLineParser docs:**
  - https://github.com/commandlineparser/commandline/wiki/Verbs
  - https://github.com/commandlineparser/commandline/wiki/Getting-Started
  - https://github.com/commandlineparser/commandline/blob/master/License.md (MIT confirmed)
- **Newtonsoft.Json docs:**
  - https://www.newtonsoft.com/json/help/html/DeepEquals.htm
  - https://www.newtonsoft.com/json/help/html/M_Newtonsoft_Json_Linq_JToken_DeepEquals.htm
  - https://www.newtonsoft.com/json/help/html/contractresolver.htm
  - https://github.com/JamesNK/Newtonsoft.Json/issues/2270 — sorted-key feature request open since 2019; confirms manual contract-resolver / JObject pass is the canonical approach
- **xUnit docs:** https://xunit.net/docs/theory-data-stability-in-vs (Theory + MemberData stability), https://xunit.net/docs/getting-started/v3/microsoft-testing-platform (v3 only — Phase 4 uses v2.9.x per CON-TT pin)
- **Existing Utinni infrastructure (in-repo, MIT, verified):**
  - `.github/workflows/ci.yml` (Phase 1 D-07; provides the CI extension target)
  - `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` (existing pin precedent for xunit + Microsoft.NET.Test.Sdk + CopyNativeArtifactsForTests pattern)
  - `UtinniCoreDotNet/PluginFramework/PluginLoader.cs` (Phase 2/3 R-B updated; test-seam `Load(string)` overload)
  - `UtinniCore/plugin_framework/utinni_plugin.h` + `plugin_manager.cpp` (Phase 3 R-B contract D-13/D-14 for `createPlugin`/`destroyPlugin`)
  - `data/ut.ini` ([Plugins] section format)

### Secondary (MEDIUM confidence — community wikis + cross-referenced WebSearch summary)

- SWGANH wiki (currently HTTP 526; cited via search-summary cross-reference): http://wiki.swganh.org/index.php/TRE:TRE_Breakdown
- File-format archive: http://fileformats.archiveteam.org/wiki/SWG
- Mod the Galaxy community forum: https://modthegalaxy.com/index.php
- Code-maze article on property ordering: https://code-maze.com/csharp-property-ordering-json-serialization/
- makolyte article on sorted-key JSON: https://makolyte.com/csharp-serialize-to-json-in-alphabetical-order/
- damirscorner article on comparing JSON in tests: https://www.damirscorner.com/blog/posts/20220520-ComparingJsonStringsInUnitTests.html

### Tertiary (LOW confidence — structural reference; no code copied)

- Swg.Explorer (no license detected — treated as effectively proprietary): https://github.com/wverkley/Swg.Explorer
- MTGUli/TREExplorer (no license detected): https://github.com/MTGUli/TREExplorer
- swg_tre (AGPL — never read for content): https://lib.rs/crates/swg_tre

### Reference reads (LICENSE-EXCLUDED, for implementer only — NOT read by researcher)

- `D:/Code/swg-client-v2/src/engine/shared/library/sharedFile/src/shared/{TreeFile,Iff}.{h,cpp}` — SOE All Rights Reserved, format reference only, no code copied
- `D:/Code/Core3/MMOCoreORB/src/tre3/*.{h,cpp}` — AGPLv3, format reference only, no code copied

## Metadata

**Confidence breakdown:**

- Standard stack (CommandLineParser + Newtonsoft.Json): **HIGH** — verified versions via NuGet flatcontainer; license + TFM compatibility confirmed via nuspec.
- Architecture / harness patterns: **HIGH** — derived from Phase 1-3 precedent + cross-confirmed against community articles.
- Pitfalls 1-3, 6, 8-10: **HIGH** — derived from in-repo existing code (Phase 1 ci.yml comments, Phase 2/3 test infrastructure), cross-confirmed against vendor docs.
- Pitfall 4 (test class layout): **HIGH** as a recommendation; planner discretion.
- Pitfall 5 (DeepEquals numeric fuzziness): **HIGH** — verified via JamesNK/Newtonsoft.Json issue tracker.
- Pitfall 7 (schemaVersion per-command vs top-level): **MEDIUM** — preference based on schema-evolution literature; planner discretion.
- TRE format details: **MEDIUM** — multiple corroborating but partial sources; SWGANH wiki currently inaccessible. Implementer cross-confirms against `swg-client-v2` source (reference read).
- IFF format details: **HIGH** — EA-IFF-85 is a 40-year-old public spec with multiple authoritative mirrors.
- Plugin manifest reuse pattern: **HIGH** — existing in-repo code (`PluginLoader.cs`).
- Security domain (V5, V12 applicability, plugin-load EoP): **HIGH** — well-understood threat model for binary-format parsers + DLL loaders.

**Research date:** 2026-05-22
**Valid until:** 2026-06-21 (30 days for stable choices; CommandLineParser/Newtonsoft.Json are slow-moving)

## RESEARCH COMPLETE

**Phase:** 4 — Tier 2 CLI shim + golden fixtures
**Confidence:** HIGH

### Key Findings

- **CommandLineParser 2.9.1 + Newtonsoft.Json 13.0.3** are both MIT, both net472-compatible (via `net461` and `net45` TFMs respectively), and both verified via NuGet flatcontainer metadata. Pin these.
- **EA-IFF-85 is a 40-year-old public spec with multiple authoritative mirrors** — IFF reading is greenfield in Utinni's namespace but textbook code against public knowledge. TRE format details are partial in public sources (SWGANH wiki) but cross-confirmed across Swg.Explorer field names and Rust `swg_tre` crate's deflate usage; the implementer's reference read of `swg-client-v2/TreeFile.cpp` closes the gap.
- **Every TRE/IFF prior-art codebase surveyed is license-incompatible with Utinni's MIT** — swg-client-v2 is SOE All Rights Reserved, Core3 + swg_tre are AGPL/AGPLv3 (viral), Swg.Explorer + TREExplorer have no detected license. **D-01's clean-room mandate is the only legally defensible path**, and the "code copied: none" disposition statement protects it.
- **In-process `Program.Main(argv)` with `Console.SetOut` redirection is the right golden-test invocation pattern**, with one subprocess smoke per command as a separate guard. JToken.DeepEquals + CR/LF normalization + per-command-shape `schemaVersion: 1` envelope is the stable-output contract.
- **The `validate-plugin` command reuses existing `PluginLoader.cs`'s `Load(string)` test seam from Phase 2 C-06 + Phase 3 R-B** — no new plugin-manifest parser code needed; the new work is reflection over discovered assemblies.

### File Created

`D:\Code\Utinni\.planning\phases\04-tier-2-cli-shim-golden-fixtures\04-RESEARCH.md`

### Confidence Assessment

| Area | Level | Reason |
|------|-------|--------|
| Standard Stack | HIGH | NuGet flatcontainer metadata verified; both packages have multi-year track records + active source repos. |
| Architecture | HIGH | Cleanly derived from Phase 1-3 precedent + cross-confirmed against community articles. |
| Pitfalls | HIGH | Drawn from in-repo Phase 1/2/3 comments, vendor docs, and direct read of existing csproj/ci.yml. |
| TRE format | MEDIUM | Public sources are partial; SWGANH wiki was inaccessible during this session. Implementer fills the gap via the reference reads. |
| IFF format | HIGH | EA-IFF-85 is public, mature, well-documented. |
| Plugin manifest | HIGH | Reuses existing `PluginLoader.cs` — pure reflection layer needed. |
| Security domain | HIGH | Standard binary-format-parser + DLL-loader threat model; mitigations follow well-known patterns. |

### Open Questions

1. TRE v4000 support scope in Phase 4 (recommendation: negative case only).
2. Checksum validation in `parse-tre` (recommendation: report raw uint32; validate-later as non-breaking feature add).
3. SWG IFF Viewer (Wasted Potential Studios) source request — out of Phase 4 scope; defer.

### Most surprising finding

**Every single open-source SWG TRE/IFF parser is license-incompatible with Utinni's MIT mandate.** swg-client-v2 (SOE All Rights Reserved), Core3 (AGPLv3 viral), swg_tre (AGPLv3 viral), Swg.Explorer (no detected license — `gh api repos/.../license` returns 404), TREExplorer (same), Wasted Potential Studios SWG IFF Viewer (claims MIT but source repo 404s). The "look at how everyone else parses TRE" research instinct dead-ends entirely; D-01's clean-room reimplementation is not an aesthetic preference — it's the only legally defensible path.

### Ready for Planning

Research complete. Planner can now create the four PLAN.md files (04-01 Scaffold, 04-02 TRE + parse-tre + list-objects, 04-03 IFF + inspect-iff, 04-04 validate-plugin) with confidence in stack, architecture, pitfalls, and test strategy.
