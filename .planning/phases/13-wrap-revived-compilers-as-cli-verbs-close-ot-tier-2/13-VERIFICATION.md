---
phase: 13-wrap-revived-compilers-as-cli-verbs-close-ot-tier-2
verified: 2026-06-04T00:00:00Z
status: human_needed
score: 5/5 success criteria verified in code (2 MET, 3 MET-WITH-GATE-FINDING)
overrides_applied: 0
re_verification: # none — initial verification
human_verification:
  - test: "OT editor typed list/struct param display against a live client (RESID-01 Tier-4 visual)"
    expected: "Open a draft-schematic (slots/attributes) or creature/hair template; slots/attributes/hair render as STRUCTURED typed rows (named sub-fields), NOT a Consolas hex blob; the rare/exotic multi-chunk tail shows a typed LABEL + hex (graceful), not bare hex; editing+saving a scalar still byte-exact round-trips (no Phase-11 codec regression)."
    why_human: "WinForms host visual rendering; FlaUI deliberately skipped (CON-TT-03 documented maintainer-in-the-loop residual). Harvested from PLAN 13-06 task 13-06-03 checkpoint:human-verify gate=blocking."
---

# Phase 13: Wrap revived compilers as CLI verbs + close OT Tier-2 — Verification Report

**Phase Goal:** Wrap the Phase-12 revived native tools + managed writers as golden-tested `utinni-cli` BUILD/SAVE/schema verbs (the thin-dispatch surface Phase-14's MCP server wraps), and close the RESID-01 Object Template Editor typed-display residual. Coexistence-by-verb-ownership preserved: BUILD-from-source uses revived compilers (`compile-*`/`build-*`); EDIT-existing-binary uses byte-exact managed writers (`roundtrip-*`/the new `save`).
**Verified:** 2026-06-04
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (the 5 ROADMAP Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | compile a `.tpf` → byte-correct OT `.iff` via verb wrapping `TemplateCompiler` (AUTH-03), golden coverage | MET-WITH-GATE-FINDING | `CompileTemplateCommand.cs` real + wired (`NativeToolResolver.Resolve` → `NativeToolRunner.Run -compile`, expected `.iff`, exit 3 on missing). Verb + error-path goldens ship (`CompileTemplateGoldenTests.cs`, 2 facts). Byte-correct SUCCESS golden deferred — a compilable `.tpf` needs a registered compiled-in template class + canonical SOE `.tdf` (zero such assets). Documented gate-finding (13-04-SUMMARY). |
| 2 | build a `.tre` archive from a source tree via verb wrapping `TreeFileBuilder` (AUTH-04) | MET | `BuildTreCommand.cs` (105 ln) wires `RspSynthesizer` + `NativeToolRunner`. `RspSynthesizer.cs` emits builder-format records from a real `.tre` via the Phase-7 reader (`TreFile.Records`/`CompressionKind`→`@u`). `BuildTre_UncompressedSynthRsp_ByteExact` test present (byte-exact CONFIRMED for uncompressed; compressed = structural fallback, D-06). |
| 3 | `TemplateDefinitionCompiler` `.tdf`→per-class param→type schema surfaced via verb (AUTH-02); OT editor displays list/struct typed not raw (RESID-01 closed) | MET-WITH-GATE-FINDING | `CompileDefinitionCommand.cs` (213 ln) emits schema via deterministic managed `.tdf` parse (`TdfSchemaExtractor`, the consumable deliverable; `--skip-native` for tests). Cross-repo `FormObjectTemplateEditor.cs` loads `ObjectTemplateSchemaLoader.LoadCommon()` (line 94) and renders typed labels `[Type listLabel, N elems · hex]` (531-540) / "STRUCT (list)" (610-616), rare tail degrades to typed-label+hex (D-07), DISPLAY-ONLY (codec untouched). Full native run over canonical SOE `.tdf` set deferred (assets absent) — gate-finding. **Visual confirm = human-check (below).** |
| 4 | `save` verb writes edited asset (loose-override or repack) with structured envelope `{written, path, bytesWritten, backupPath, validated}` (AUTH-05) | MET | `SaveCommand.cs` (276 ln): 4-format sniff (IFF/datatable/stf/OT) over framework writers (`IffWriter`/`DataTableWriter`/`StringTableWriter`/`MutableObjectTemplate.Serialize`); loose-override default via `LooseOverridePath.Resolve` (104); exact envelope emitted (135-142); `.tre` rejected → routed to `repack-tre` (D-10). Separate `RepackTreCommand.cs` (145 ln) populates `backupPath` (`TreBackupPath.NextAvailable` + `File.Copy`), refuses V6000/encrypted. `SaveCommandTests.cs` asserts all 5 envelope keys (89-93). |
| 5 | compile a datatable from CSV/XML + run `ArmorExporterTool`/`WeaponExporterTool` via verbs (AUTH-06), each ≥1 golden fixture | MET-WITH-GATE-FINDING | `tools/Utinni.Tools.sln` = **35 projects** incl. DataTableTool/ArmorExporterTool/CoreWeaponExporterTool/sharedXml (all 4 present as `Project()` entries). `CompileDatatableCommand.cs` managed-oracle cross-check CONFIRMED (`DataTableWriter` key-link verified). `ExportArmor/WeaponCommand.cs` → `ExportCommandShared.Run` → `NativeToolRunner` w/ staged WorkingDirectory + T-13-03 injection guard + Perforce-stub. Golden tests present (`CompileDatatableGoldenTests` 3 facts, `ExporterGoldenTests` 3 facts). Full datatable.iff→.tpf→TemplateCompiler chain SUCCESS golden deferred (same `.tpf` gate-finding). |

**Score:** 5/5 success criteria verified in code — 2 fully MET, 3 MET-WITH-GATE-FINDING (honest, surfaced native-asset limitations following the Phase-12 gate-finding precedent; none stall the phase).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Utinni.Cli/Program.cs` | 17 verbs registered (ParseArguments Type[] + Dispatch switch) | VERIFIED | All 8 new verbs (Save, RepackTre, CompileTemplate, BuildTre, CompileDefinition, CompileDatatable, ExportArmor, ExportWeapon) in both the Type[] list (48-65) and the Dispatch switch (76-92). |
| `Utinni.Cli/Commands/Subprocess/NativeToolRunner.cs` | subprocess seam + locked envelope | VERIFIED | 291 ln; `Process.Start` UseShellExecute=false, captures exit/stderr, `JsonOutput.EmitSuccess/EmitError`, banner-normalize, timeout→exit 2, missing-exe→exit 3. |
| `Utinni.Cli/Commands/Subprocess/RspSynthesizer.cs` | `.rsp` from real `.tre` | VERIFIED | 93 ln; `TreFile.Records` order + `CompressionKind`→`@u`. |
| `Utinni.Cli/Commands/SaveCommand.cs` | AUTH-05 4-format save | VERIFIED | 276 ln; see SC4. |
| `Utinni.Cli/Commands/RepackTreCommand.cs` | D-10 separate repack | VERIFIED | 145 ln; backupPath populated, V6000 refused. |
| `Utinni.Cli/Commands/CompileTemplateCommand.cs` | AUTH-03 verb | VERIFIED | 71 ln; wired to NativeToolRunner. |
| `Utinni.Cli/Commands/BuildTreCommand.cs` | AUTH-04 verb | VERIFIED | 105 ln; RspSynthesizer+NativeToolRunner. |
| `Utinni.Cli/Commands/CompileDefinitionCommand.cs` | AUTH-02 schema-emit | VERIFIED | 213 ln; TdfSchemaExtractor. |
| `Utinni.Cli/Commands/CompileDatatableCommand.cs` | AUTH-06 datatable | VERIFIED | 77 ln; managed-oracle cross-check. |
| `Utinni.Cli/Commands/ExportArmorCommand.cs` / `ExportWeaponCommand.cs` / `ExportCommandShared.cs` | AUTH-06 exporters | VERIFIED | Both delegate to shared runner → NativeToolRunner w/ staged WorkingDirectory + T-13-03 guard. |
| `UtinniCoreDotNet/Formats/ObjectTemplate/ObjectTemplateSchema.cs` | param→type model | VERIFIED | 108 ln; ParamType(14)+ListType(4) enums, `IsStructured`. |
| `UtinniCoreDotNet/Formats/ObjectTemplate/ObjectTemplateSchemaLoader.cs` | loads committed schema | VERIFIED | 153 ln; `LoadCommon()` reads embedded resource, graceful no-throw on absent/malformed (T-13-16). |
| `.../schema/object-template-common.schema.json` + `mintest.schema.json` | committed schema artifacts | VERIFIED | Both present + real content (SharedDraftSchematic/Tangible/Weapon classes w/ TYPE_STRUCT/LIST_LIST params). Embedded copy in source tree + `<EmbeddedResource>` in csproj (line 93). |
| `tools/DEPENDENCY-MANIFEST.md` | AUTH-06 lift deltas | VERIFIED | Documents sharedXml/libxml2, Perforce-stub, /SAFESEH:NO, TemplateCompiler-chain, all 3 exes green @ v145. |

Note: `verify.artifacts` reported the `schema/` directory path (trailing slash) as "File not found" for PLANs 05/06 — a tool limitation resolving directory paths, NOT a real gap. Both JSON files confirmed present via Glob.

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| Program.cs | 8 new commands | Dispatch switch | WIRED | All 8 cases present. |
| SaveCommand | LooseOverridePath.Resolve | path-containment | WIRED | Line 104 (`verify.key-links` false-neg — confirmed by Read). |
| SaveCommand | framework writers | serialize | WIRED | IffWriter/DataTableWriter/StringTableWriter/ObjectTemplateWriter all referenced. |
| NativeToolRunner | JsonOutput.EmitSuccess/EmitError | envelope | WIRED | Lines 93/105/117 (`verify.key-links` false-neg — confirmed by grep). |
| RspSynthesizer | TreFile.Records/CompressionKind | data-block order | WIRED | verified. |
| CompileTemplate/BuildTre | NativeToolRunner/RspSynthesizer | Run | WIRED | verified. |
| Export*Command | NativeToolRunner + staged dir | system() chain | WIRED | via ExportCommandShared (wildcard path defeated the tool — confirmed by Read). |
| FormObjectTemplateEditor.cs | ObjectTemplateSchemaLoader → committed schema | LoadCommon at open | WIRED | Cross-repo file (tool can't reach D:/Code/UtinniPlugins) — confirmed by grep: line 94 LoadCommon, 531-616 typed render. |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| All 17 verbs registered | Read Program.cs Dispatch | 17 cases incl. 8 new | PASS |
| tools.sln project count | grep `^Project\(` | 35 | PASS |
| 4 new tool projects in sln | grep DataTableTool/Armor/CoreWeapon/sharedXml | all 4 Project() entries | PASS |
| Schema JSON real content | head object-template-common.schema.json | multi-class typed params | PASS |
| EmbeddedResource wired | grep csproj | line 93 present | PASS |
| Save envelope keys asserted | grep SaveCommandTests | all 5 keys | PASS |
| Test files exist | ls 10 new test files | all 10 present | PASS |

Native-exe-dependent goldens (compile-template success, exporter chain, build-tre byte-exact) carry a documented "skip-if-exe-not-found" guard — consistent with the gate-findings; the verb+error paths run unconditionally. Builds NOT re-run (per instruction; SUMMARYs record: tools.sln 35 green @ v145/Win32, Utinni.Cli.Tests 206 passed/2 skipped/0 failed, UtinniCoreDotNet.Tests 637 passed/0 failed, UtinniCoreDotNet+TJT Debug+Release|x86 green).

### Probe Execution

No probe scripts in repo (`scripts/*/tests/probe-*.sh` absent). This is a .NET golden-test + native-build-gate phase, not a probe-driven migration phase. Probe step: N/A.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| AUTH-02 | 13-05 | TemplateDefinitionCompiler `.tdf`→param→type schema drives OT typed display | SATISFIED (gate-finding) | compile-definition schema-emit + editor wiring; canonical SOE `.tdf` set deferred. |
| AUTH-03 | 13-04 | compile `.tpf`→byte-correct `.iff` verb | SATISFIED (gate-finding) | compile-template verb+error paths; byte-correct golden deferred (no compilable `.tpf` asset). |
| AUTH-04 | 13-02, 13-04 | build-`.tre` verb wrapping TreeFileBuilder | SATISFIED | build-tre + RspSynthesizer; byte-exact uncompressed CONFIRMED. |
| AUTH-05 | 13-03 | utinni-cli SAVE verb | SATISFIED | save (4-format, loose-override, locked envelope) + repack-tre. |
| AUTH-06 | 13-01, 13-05 | datatable compile + item exporters via verbs | SATISFIED (gate-finding) | 35-proj tools.sln + compile-datatable (oracle CONFIRMED) + export-armor/weapon; full chain golden deferred. |
| RESID-01 | 13-06 | OT editor list/struct typed not raw | SATISFIED (needs human visual) | schema model+loader+committed artifact+editor wiring; Tier-4 visual = human-check. |

No ORPHANED requirements — all 6 phase-mapped IDs (AUTH-02..06, RESID-01) are claimed by plans.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (none) | — | — | — | Clean: zero TBD/FIXME/XXX in any new Phase-13 CLI/schema source or the cross-repo editor; zero "Not implemented"/placeholder/NotImplementedException stubs. |

### Human Verification Required

#### 1. OT editor typed list/struct display (RESID-01 Tier-4 visual)

**Test:** Build TJT + UtinniCore (VS2026 MSBuild, Debug|x86). Open an object template carrying list/struct params — a draft schematic (slots/attributes) or creature/hair template (hair tint list) — via the editor host / TRE Browser / IFF Editor hand-off.
**Expected:** (a) slots/attributes/hair render as STRUCTURED typed rows (named sub-fields), NOT a single Consolas hex blob; (b) an exotic/rare multi-chunk param shows a typed LABEL + hex (graceful degradation), not bare hex; (c) editing+saving a scalar still byte-exact round-trips (no Phase-11 codec regression). ("Naked after TJT-driven scene change" is the documented baseline, not a failure signal.)
**Why human:** WinForms host visual rendering; FlaUI deliberately skipped (CON-TT-03 documented maintainer-in-the-loop residual). This is PLAN 13-06 task 13-06-03, a `checkpoint:human-verify gate="blocking"` the planner declared — it is the ONLY non-automatable item in the phase.

### Gaps Summary

No blocking gaps. All 5 ROADMAP success criteria are achieved in the codebase: every capability is a real, wired `utinni-cli` verb (17 total registered), the SAVE envelope matches the ROADMAP lock exactly, coexistence-by-verb-ownership is preserved (compile-*/build-* native BUILD vs roundtrip-*/save managed EDIT, with `.tre` repack carved out as a separate destructive verb), the 35-project tools.sln carries the 3 AUTH-06 natives + sharedXml, and the RESID-01 schema model/loader/committed-artifact/editor-wiring are all present and DISPLAY-ONLY (codec untouched).

The 3 MET-WITH-GATE-FINDING criteria (compile-template byte-correct golden, compile-definition full SOE `.tdf` run, exporter full datatable→`.tpf`→TemplateCompiler chain golden) are honest, surfaced limitations rooted in the absence of canonical SOE `.tpf`/`.tdf` assets — they follow the established Phase-12 gate-finding precedent, the verbs + error-path coverage ship, and the consumable deliverables (managed schema-emit, managed-oracle datatable cross-check, byte-exact uncompressed build-tre) are CONFIRMED. These are NOT deferred to a later phase; they are documented asset constraints. Phase 14 (MCP server) wraps these same verbs.

Status is `human_needed` (not `passed`) solely because PLAN 13-06 carries a deliberate Tier-4 maintainer-in-the-loop visual checkpoint (13-06-03) for the RESID-01 typed display, which by design cannot be verified programmatically. All code-level truths verify.

---

_Verified: 2026-06-04_
_Verifier: Claude (gsd-verifier)_
