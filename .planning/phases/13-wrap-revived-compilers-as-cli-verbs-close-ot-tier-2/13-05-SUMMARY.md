---
phase: 13-wrap-revived-compilers-as-cli-verbs-close-ot-tier-2
plan: 05
subsystem: api
tags: [cli, build-verb, compile-definition, compile-datatable, export, schema, auth-06]

requires:
  - plan: 13-01
    provides: DataTableTool + ArmorExporterTool + CoreWeaponExporterTool green natives
  - plan: 13-02
    provides: NativeToolRunner subprocess seam
provides:
  - compile-definition verb (AUTH-02) + per-class param->type schema artifact (D-08)
  - compile-datatable verb (AUTH-06) with confirmed managed-oracle cross-check (D-03)
  - export-armor + export-weapon verbs (AUTH-06) with the T-13-03 injection guard
  - committed mintest.schema.json (the ParamType/ListType vocabulary 13-06 consumes)
affects: [13-06, 14-mcp]

tech-stack:
  added: []
  patterns: [managed .tdf schema extractor, native-vs-managed datatable cross-check]

key-files:
  created:
    - Utinni.Cli/Commands/CompileDefinitionCommand.cs (+ TdfSchemaExtractor)
    - Utinni.Cli/Commands/CompileDatatableCommand.cs
    - Utinni.Cli/Commands/ExportArmorCommand.cs
    - Utinni.Cli/Commands/ExportWeaponCommand.cs
    - Utinni.Cli/Commands/ExportCommandShared.cs
    - Utinni.Cli.Tests/Infrastructure/NativeToolLocator.cs
    - Utinni.Cli.Tests/Commands/{CompileDefinition,CompileDatatable,Exporter}GoldenTests.cs
    - Utinni.Cli.Tests/Fixtures/tdf/mintest.tdf
    - .planning/phases/13-.../schema/mintest.schema.json
  modified:
    - Utinni.Cli/Program.cs (Type[] dispatch — 17 verbs)
    - Utinni.Cli/Commands/Subprocess/NativeToolRunner.cs (RunCaptured)
    - Utinni.Cli.Tests/Fixtures/dispatch/ (goldens refreshed for 4 verbs)

key-decisions:
  - "DataTableTool uses named options -i/-o (NOT positional args) — discovered by probing; the verb passes -i <in> -o <out>."
  - "compile-definition emits the schema by a deterministic managed .tdf parse (TdfSchemaExtractor); the native compiler run is best-effort (--skip-native for tests) — the schema is the consumable deliverable."
  - "CommandLineParser caps at 16 verbs in both ParseArguments<T..> and MapResult; 17 verbs forced the Type[] overload + object-typed Dispatch switch."

patterns-established:
  - "Managed .tdf schema extractor: <type> [limits] <name> param lines -> ParamType/ListType JSON vocabulary."
  - "Datatable D-03 cross-check: native DataTableTool .iff decoded typed-correct by the managed reader."

requirements-completed: [AUTH-02, AUTH-06]

duration: ~65min
completed: 2026-06-04
---

# Phase 13 Plan 05: compile-definition + compile-datatable + exporters Summary

**Four AUTH-02/AUTH-06 verbs ship: compile-datatable (with a CONFIRMED native-vs-managed cross-check), compile-definition (emitting the committed per-class schema the OT editor consumes), and the two item exporters (with the injection guard). The full SOE-compiler chains that need canonical .tpf/.tdf/tools.cfg assets are documented gate-findings.**

## Performance

- **Duration:** ~65 min
- **Completed:** 2026-06-04
- **Tasks:** 3 (+ a Program.cs dispatch refactor)
- **Files:** 10 created + 3 modified

## Accomplishments
- **compile-datatable (AUTH-06):** wraps DataTableTool (`-i`/`-o`); **D-03 cross-check CONFIRMED** — the native compiles a `.tab` to a `.iff` the managed reader decodes typed-correct.
- **compile-definition (AUTH-02, D-08):** emits the per-class `ParamType/ListType` schema from a minimal authored `.tdf` (managed parse) + runs the native best-effort; ships the committed `mintest.schema.json` (incl. a `LIST_LIST` param).
- **export-armor / export-weapon (AUTH-06):** the item-exporter verbs with the T-13-03 shell-meta/`..` input guard before the exporter's internal `system()`.
- 11 plan tests green; full suite **206 passed**.

## Task Commits

- **Refactor:** `c4805e6` — RunCaptured + 17-verb Type[] dispatch
- **Task 1: compile-definition + schema** — `6a917a6` (feat)
- **Task 2: compile-datatable + cross-check** — `82c5a8e` (feat)
- **Task 3: export-armor/weapon + injection guard** — `6769332` (feat)

## Deviations from plan

- **DataTableTool args:** named options `-i <in> -o <out>`, not positional (the positional form silently derived a default output path → `produced=false`). Fixed after probing.
- **CommandLineParser 16-verb cap:** both `ParseArguments<T..>` and `MapResult(lambdas)` top out at 16; the 17th verb forced the `Type[]` overload + an object-typed `Dispatch` switch.
- **compile-definition schema source:** the deterministic deliverable is a managed `.tdf` parse (TdfSchemaExtractor), with the native compiler run kept best-effort (`--skip-native` in tests). The plan's "capture its TemplateData model" is satisfied by parsing the same `.tdf` the compiler consumes — robust to the native's P4-link hang risk.

## Gate-findings

- **compile-definition full native run + canonical SOE `.tdf` set** — Open Q1: no canonical `.tdf` assets; the minimal fixture proves the verb + vocabulary; the full set is deferred.
- **export-* full chain** (`datatable.iff → .tpf → system("TemplateCompiler")`) — needs a registered template class + canonical `.tdf` (the compile-template gate-finding) + populated `tools.cfg`. The verbs + injection guard + Perforce-stub ship; the produced-artifact golden is deferred.

## Verification

- `dotnet test --no-build` full suite → **206 passed, 2 skipped, 0 failed**.
- compile-datatable native cross-check ran against the real `DataTableTool_d.exe` (typed-correct decode).
- compile-definition schema is deterministic + carries a `ListType != LIST_NONE` param; artifact committed under the phase `schema/` dir.
- All 4 verbs registered in Program.cs; dispatch goldens refreshed (17 verbs total).
