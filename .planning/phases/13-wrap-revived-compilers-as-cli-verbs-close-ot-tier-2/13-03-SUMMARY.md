---
phase: 13-wrap-revived-compilers-as-cli-verbs-close-ot-tier-2
plan: 03
subsystem: api
tags: [cli, save, write-surface, repack, tre, loose-override, four-format]

requires:
  - phase: 08-tjt-subpanel-iff-editor
    provides: LooseOverridePath, TreBackupPath, TreRepackLock, TreWriter.Repack, IffWriter
  - phase: 09-tjt-subpanel-datatable-editor
    provides: DataTableWriter
  - phase: 10-tjt-subpanel-string-table-editor
    provides: StringTableWriter
  - phase: 11-tjt-subpanel-object-template-editor
    provides: MutableObjectTemplate.Serialize
provides:
  - save verb (AUTH-05) — 4-format in-process write over framework primitives, loose-override default
  - repack-tre verb (D-10) — separate backup-routed destructive repack, V6000-refused
affects: [14-mcp]

tech-stack:
  added: []
  patterns: [content-sniff format detection, atomic FileStream write with Flush(true), framework-leg-only save]

key-files:
  created:
    - Utinni.Cli/Commands/SaveCommand.cs
    - Utinni.Cli/Commands/RepackTreCommand.cs
    - Utinni.Cli.Tests/Commands/SaveCommandTests.cs
    - Utinni.Cli.Tests/Commands/RepackTreCommandTests.cs
  modified:
    - Utinni.Cli/Program.cs

key-decisions:
  - "save modes (the plan left relPath sourcing implicit): --path = explicit (load asset, write dest); --root = loose-override (resolve LooseOverridePath.Resolve(root, asset), re-serialize in place). Cwd-independent + testable."
  - "Framework-leg-only (PATTERNS Option 1): reimplement the thin write over IffWriter/DataTableWriter/StringTableWriter/MutableObjectTemplate.Serialize + LooseOverridePath; NO reference to the WinForms TJT.Saving assembly."
  - "OT detected structurally (try FromMutableIff, catch DecoderException -> plain IFF) after the DTII datatable check; harmless if borderline (both round-trip byte-exact)."
  - "repack-tre repacks into memory FIRST so V6000 refusal happens before any backup/write (no stray backup on refusal)."

patterns-established:
  - "Content-sniff format detection: EERT->.tre, 0xABCD u32 LE->stf, FORM IFF root SubTypeId DTII->datatable / structurally-OT->OT / else plain IFF."
  - "Atomic write core: Directory.CreateDirectory + FileStream(Create/Write/Read) + Flush(true) (reimplemented from IffSaveTargets WriteAtomic, not referenced)."

requirements-completed: [AUTH-05]

duration: ~35min
completed: 2026-06-04
---

# Phase 13 Plan 03: save + repack-tre verbs Summary

**utinni-cli gains its first write surface: a 4-format `save` verb (loose-override-by-default, framework-leg-only) returning the ROADMAP-locked envelope, plus a SEPARATE backup-routed `repack-tre` verb that keeps the destructive .tre rebuild off the default path (D-10).**

## Performance

- **Duration:** ~35 min
- **Completed:** 2026-06-04
- **Tasks:** 2
- **Files:** 4 created + 1 modified

## Accomplishments
- `save` — loads + content-sniffs (IFF/datatable/stf/OT), re-serializes via the matching framework writer, writes atomically, reports `{written, path, bytesWritten, backupPath, validated}`. Loose-override default (`--root`) with `LooseOverridePath.Resolve` containment; explicit `--path`. `.tre` input → usage error → `repack-tre` (D-10).
- `repack-tre` — full-rebuild repack into memory first (V6000 refused before any FS write), `TreRepackLock` probe, timestamped backup before overwrite, `backupPath` populated in the envelope.
- Both verbs registered in `Program.cs` (ParseArguments + MapResult).
- 13 tests green (9 Save + 4 RepackTre).

## Task Commits

1. **Task 1: save verb — 4-format in-process write** — `3ed09fc` (feat)
2. **Task 2: repack-tre separate verb + register both verbs** — `eefd356` (feat)

## Deviations from plan

- The plan's loose-override `relPath` sourcing was implicit. Resolved cleanly: `--path` = explicit source→dest; `--root` = loose-override where the asset path is the relative-to-root path, re-serialized in place via the containment-checked resolved path. This is cwd-independent (clean for tests) and matches the SWG loose-override concept.
- `DecoderException` lives in `UtinniCoreDotNet.Formats.Decoders` (not `.ObjectTemplate`) — added the using (initial build error, fixed).

## Verification

- `MSBuild Utinni.Cli.Tests.csproj /p:Configuration=Debug /p:Platform=x86` → green.
- `dotnet test --no-build --filter Save|RepackTre` → **13 passed, 0 failed**.
- Source assertions: `SaveCommand` has no `using TJT.Saving` / no plugin-assembly reference (framework-leg only); both `SaveOptions` + `RepackTreOptions` appear in BOTH the `ParseArguments` list and the `MapResult` lambdas.
