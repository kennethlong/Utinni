# Phase 4: Tier 2 CLI shim + golden fixtures - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-23
**Phase:** 04-tier-2-cli-shim-golden-fixtures
**Areas discussed:** Command surface + parser strategy, CLI shape + distribution (CON-O-11), Fixture storage (CON-O-09), Golden-file diff mechanism, Plan structuring, Parser home

---

## Initial gray-area menu

| Option | Description | Selected |
|--------|-------------|----------|
| Command surface + parser strategy | What ships AND how parsers exist (vendor swg-client-v2 / port / reimplement). Pivotal scope question. | ✓ |
| CLI shape + distribution (CON-O-11) | Managed C# vs native C++; public artifact vs internal-only. | ✓ |
| Fixture storage (CON-O-09) | In-repo / LFS / synthesized / BYO. | ✓ |
| Golden-file diff mechanism | JSON+text diff / Verify approval / structural xUnit / byte-equal. | ✓ |

**User's choice:** All four areas.
**Notes:** Initial menu was reformulated after user surfaced that `D:/Code/swg-client-v2` and `D:/Code/Core3` both contain TRE/IFF reader implementations — this changed scope question from "build from scratch" to "what's the parser-source strategy". Initial 5-option menu folded into 4 (max AskUserQuestion options); "CLI shape" and "Distribution (CON-O-11)" were combined.

---

## Command surface + parser strategy (D-01)

| Option | Description | Selected |
|--------|-------------|----------|
| Reimplement clean from format spec | Walk swg-client-v2 + Core3 as references; zero code copied; fresh MIT parsers; ships all 4 commands. | ✓ |
| Vendor swg-shared-file, strip deps | Copy TreeFile.cpp + Iff.cpp + minimal foundation stubs under SOE "All Rights Reserved" provenance. | |
| Ship 2 commands, defer TRE/IFF | validate-plugin + list-objects only; TRE → Phase 7, IFF → Phase 8. | |
| Ship TRE this phase, defer IFF to Phase 8 | Hybrid; parse-tre + list-objects + validate-plugin here; inspect-iff in Phase 8. | |

**User's choice:** Reimplement clean (option 1).
**Notes:** User flagged that Core3 (`D:/Code/Core3`) has TRE reader as alternative. Investigation showed Core3 is **GNU AGPLv3** — viral, more restrictive than SOE's All-Rights-Reserved + community-norm. AGPLv3 would force Utinni to relicense, killing the MIT fork. swg-client-v2 carries "Portions copyright 1998 Bootprint Entertainment / Portions copyright 2001-2002 Sony Online Entertainment / All Rights Reserved" — no LICENSE file in repo root, community-norm only. Both are read-only references. User direction: "if not better, go with 1". Verdict locked: option 1.

---

## CLI shape + distribution / CON-O-11 (D-02)

| Option | Description | Selected |
|--------|-------------|----------|
| Managed C# / net472 / x86, PUBLIC | Sibling project, CommandLineParser 2.x + Newtonsoft.Json, ships in release. | ✓ |
| Managed C# / net472 / x86, INTERNAL | Same shape, tests-only, never shipped. | |
| Native C++ / x86, PUBLIC | vcxproj, CLI11 / nlohmann, no CLR boot. | |
| Native C++ / x86, INTERNAL | Same as above, tests-only. | |

**User's choice:** Managed C# net472/x86, PUBLIC (Recommended).
**Notes:** Matches existing Phase 1/2/3 sibling-project pattern; reuses UtinniCoreDotNet bindings without CppSharp regen; modders + plugin authors get standalone validation workflow. CON-O-11 disposition: public.

---

## Fixture storage / CON-O-09 (D-03)

| Option | Description | Selected |
|--------|-------------|----------|
| In-repo synthesized + tiny real samples | Hand-crafted minimal + <256KB real samples committed; no LFS. | ✓ |
| Git LFS for binary samples | Multi-MB real samples via LFS; runner setup cost. | |
| Hybrid: synthesized in-repo + LFS-gated 'live' tier | Two-tier suite; LFS for opt-in fidelity tests. | |
| Bring-your-own + checked-in expected outputs | No binaries committed; tests skip without env-var pointing to fixtures. | |

**User's choice:** In-repo synthesized + tiny real samples (Recommended).
**Notes:** Synthesized-first sidesteps redistribution-rights questions on SWG game assets. <256KB cap per fixture keeps repo lean. CON-O-09 disposition: in-repo, no LFS.

---

## Golden-file diff mechanism (D-04)

| Option | Description | Selected |
|--------|-------------|----------|
| JSON output + textual diff | Sorted-key indented JSON; JToken.DeepEquals; artifact upload on failure. | ✓ |
| Approval-style snapshot files (Verify.Xunit) | First-run-captures-received, reviewer copies to verified, auto-diff-on-failure. | |
| Hand-written structural xUnit asserts | No snapshot files; per-property asserts; most flexible, most code. | |
| Byte-equal diff against committed expected.txt | Simplest; fragile to whitespace/CRLF/culture drift. | |

**User's choice:** JSON output + textual diff (Recommended).
**Notes:** Locks output format intentionally — desirable for a public artifact (D-02). Format change = breaking change = goldens re-baselined in same PR.

---

## Plan structuring (D-05)

| Option | Description | Selected |
|--------|-------------|----------|
| By concern (4 plans) | 04-01 scaffold + 04-02 TRE+parse-tre+list-objects + 04-03 IFF+inspect-iff + 04-04 validate-plugin. | ✓ |
| By command (5 plans) | Scaffold + one plan per command; cleaner traceability, artificial parser-sharing dep. | |
| Two plans (parsers + CLI/CI) | Parsers as pure Tier-1 lib first, CLI surface on top. | |
| Single mega-plan | Everything in one plan; not recommended. | |

**User's choice:** By concern (Recommended).
**Notes:** Mirrors Phase 3 D-01 'by category/risk-class' structure. CI-gated boundaries (each plan green on master before next starts).

---

## Parser home (D-06)

| Option | Description | Selected |
|--------|-------------|----------|
| Managed in UtinniCoreDotNet | Pure C# in `UtinniCoreDotNet/Formats/`; Tier-1 unit-testable. | ✓ |
| Native in UtinniCore, CppSharp-projected | `UtinniCore/swg/file/`, UTINNI_API + auto-discovery from Phase 3 R-F. | |
| Split: TRE managed, IFF native | Asymmetric. | |

**User's choice:** Managed in UtinniCoreDotNet (Recommended).
**Notes:** CON-TT-01 ("TDD applies to pure-logic and file-format layers") directly governs. DEC-C4 not violated — DEC-C4 locks IFF read/write for editor use; Phase 4's Iff is CLI-tier and read-only. Phase 8 either consumes or owns its own — that's Phase 8's call.

---

## Mid-discussion direction: Open-source editor survey

**User question (post-decisions):** "Should we look for opensource Tree and IFF editors to see how they implemented them or will that happen in research phase?"

**Disposition:** Right phase is plan-phase research (gsd-phase-researcher). discuss-phase captures decisions; research captures how-to + prior art. CONTEXT.md updated with explicit "Research targets for plan-phase researcher" section instructing the researcher to survey SWGEmu / ModTheGalaxy / ProjectSWG community tooling, generic IFF readers, and known landmine reports — and to produce a "Prior art surveyed" section in RESEARCH.md with explicit "code copied: none" disposition statements to protect D-01's clean-reimplementation contract.

---

## Claude's Discretion (deferred to planner)

- Exact xUnit test naming (Phase 1 D-04 `[Method]_[Scenario]_[ExpectedOutcome]` convention).
- Task ordering WITHIN a plan.
- Exact `Utinni.Cli` namespace shape.
- Whether the golden harness invokes the CLI as `Process.Start` (true e2e) or via `Program.Main(argv)` (faster).
- Whether per-parser unit tests live in `UtinniCoreDotNet.Tests/FormatsTests/{Tre,Iff,PluginManifest}/` or flat `<Parser>Tests.cs`.
- Whether `schemaVersion: 1` JSON envelope is per-command or shared at top level.
- Whether plugin-manifest validation re-reads ut.ini entries directly or relies on PluginLoader.cs discovery.
- Exact CommandLineParser nuget version pin.
- Whether to introduce `Utinni.Cli.Common` library (probably not).

## Deferred Ideas

- IFF write path → Phase 8 (DEC-C4).
- LFS infrastructure + 'live-snapshot' fixture tier → revisit if Phase 5 or Phase 7 demand it.
- CLI surface stability ADR / semver-versioned API → Phase 6 / V2.
- Verify.Xunit-style approval snapshots → revisit only if UX gap surfaces.
- Native C++ CLI variant → Phase 6+ if sub-100ms startup or self-contained binary demanded.
- Test-harness-internal CLI distribution → reversible to internal in Phase 6 if public-surface burden too high.
- `Utinni.Cli.Common` shared library → revisit when 2nd CLI exists.
- System.CommandLine modernisation → V2 (requires net6+; bundled with broader net472 migration).
- Coverage tooling (coverlet, ReportGenerator) → after Phase 5 lands C++ test breadth.
- Tier 3 (mock D3D9) → V2 per ROADMAP.
- Tier 4 boundary doc → Phase 6 STAB-03 / TEST-04.
- CON-O-06 (LeksysINI), CON-O-08 (DXSDK) → Phase 6 STAB-03.
- Wave-1 subpanel reuse-vs-own-parser calls → respective discuss-phases (Phases 7-11).
