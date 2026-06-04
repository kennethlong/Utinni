# Phase 13: Wrap revived compilers as CLI verbs + close OT Tier-2 — Research

**Researched:** 2026-06-03
**Domain:** Native build-CLI revival (lift-and-shift v145/Win32) + managed `utinni-cli` verb wrapping (net472 CommandLineParser) + OT typed-schema generation + `.tre` byte-exact synthesis
**Confidence:** HIGH (every load-bearing claim verified against the actual lift source at `swg-client-v2@5fce7bb8` and the existing Utinni codebase)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions (research HOW, not WHETHER)
- **D-01 (AUTH-06):** Lift all 3 natives (`DataTableTool`, `ArmorExporterTool`, `CoreWeaponExporterTool`) into `tools/`, wrap as BUILD verbs, byte-exact to SOE, following the Phase-12 lift pattern.
- **D-02 (escape-hatch):** Scope each tool's dependency closure FIRST. If an exporter pulls a multi-day server-side closure, THAT one tool falls back (managed `DataTableWriter` for DataTableTool, or defer that exporter as tracked residual) rather than blocking the phase. v143 fallback is secondary.
- **D-03 (golden contract):** Correctness gate = native-vs-managed cross-check + committed regression goldens. Managed twin = independent oracle.
- **D-04:** Native output is SOE-authoritative on disagreement; a mismatch files a managed-reader bug, does NOT stall the phase.
- **D-05 (real reference data):** `D:\SWGEmu-Client\SWGEmu\` ships 53 real pre-CU 0005/0006 `.tre` archives — authentic SOE `.iff` outputs usable as cross-check oracle inputs. Client `.tre` hold compiled `.iff` only (no `.tpf`/`.tab`/`.tdf` source — those are server-side `dsrc`).
- **D-06 (build-tre byte-exact):** Synthesize a `.rsp` from a real `.tre` via the Phase-7 reader (paths + order + per-file compression), rebuild with `build-tre`, byte-compare. zlib 1.1.4 determinism-matched. Fall back to structural compare if byte-identity proves unreachable.
- **D-07 (RESID-01):** Structured typed editors for COMMON list/struct OT params (slots/attributes/hair); labeled-hex for the rare tail.
- **D-08 (schema delivery):** `compile-definition` verb runs `TemplateDefinitionCompiler` once, emits a per-class param→type schema (JSON, committed/cached). OT editor + Area-2 tests both consume the SAME artifact.
- **D-09 (SAVE):** `save` covers all 4 formats (IFF/datatable/stf/OT) wrapping existing managed save targets. Loose-override default + explicit `--path`. Envelope `{written, path, bytesWritten, backupPath, validated}`.
- **D-10:** `.tre` repack is NOT reachable through `save` — it's its own explicit verb (`TreRepackSaveTarget`/`TreBackupPath`).

### Claude's Discretion
- Exact verb names within `compile-*`/`build-*` convention.
- CSV-vs-XML datatable/exporter source input format — **resolve against what the lifted natives actually accept** (resolved below: native is tab-delimited spreadsheet OR XML; see § AUTH-06 Input Formats).
- Subprocess invocation mechanics for wrapping native exes.

### Deferred Ideas (OUT OF SCOPE)
- The MCP server itself (Phase 14); Wave-2 editors (Phase 15); window/presentation residuals incl. RESID-04 (Phase 15). No deferred ideas belong to other phases — discussion stayed in Phase-13 scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| AUTH-02 | `TemplateDefinitionCompiler` `.tdf`/`.tpd` → per-class param→type schema drives OT typed display | § AUTH-02: schema vocabulary is the `TemplateData::ParamType` (14 values) + `ListType` (4 values) enums; `compile-definition` verb emits per-class JSON. Gate-finding: NO `.tdf` source assets exist in either repo — schema generation needs a supplied `.tdf` OR is derived from the compiler's built-in definitions. |
| AUTH-03 | Compile `.tpf` → byte-correct OT `.iff` via verb wrapping `TemplateCompiler` | § BUILD verb wrapping; native CLI `TemplateCompiler_d.exe -compile <input.tpf>`. Gate-finding: zero `.tpf` source assets — golden fixtures must be authored or supplied. |
| AUTH-04 | Build `.tre` from source tree via verb wrapping `TreeFileBuilder` | § AUTH-04 `.rsp` synthesis (HIGH detail). Native CLI `TreeFileBuilder_d.exe -r <build.rsp> <out.tre>`. |
| AUTH-05 | SAVE verb writes edited asset (loose-override or repack) with structured envelope | § AUTH-05: thin wrap of 4 existing managed save targets; repack stays separate (D-10). |
| AUTH-06 | Compile datatable from CSV/XML + run item exporters via verbs | § AUTH-06 lift-cost verdict (the centerpiece research task). |
| RESID-01 | OT Editor displays list/struct params typed, not raw | § RESID-01: extends Phase-11 codec's `RawBytesHexFallback` (~17%) with schema-driven structured widgets. |
</phase_requirements>

## Summary

This phase is **mechanically low-risk and front-loaded by Phase 12.** Every native tool wrapped here either already builds green in `tools/Utinni.Tools.sln` (TemplateCompiler, TreeFileBuilder, TemplateDefinitionCompiler) or — for the AUTH-06 trio — depends almost entirely on libraries already lifted in Phase 12. The single biggest unknown the planner asked me to resolve — the per-tool dependency-closure verdict for the 3 AUTH-06 exporters (D-02 escape-hatch) — resolves **decisively in favor of "all 3 are clean client lifts."**

**The "server-taint" risk is a phantom.** All three exporters `#include "serverGame/ServerObjectTemplate.h"` and `sharedGame/CraftingData.h`, which at first glance looks like a server-side dependency. But (1) the client repo has **no server tree at all** (`src/engine/server` does not exist) — the include path is a dead alias; (2) the only symbols they touch from those headers are **enum constants** (`ArmorCategory_Last`, `XP_crafting`, `CT_weapon`, `IngredientType_Last`), and those enums physically live in `sharedTemplate/ServerObjectTemplate.h` and `sharedGame/CraftingData.h` — **both already lifted in Phase 12**; (3) **none of the three lists serverGame in its `ProjectReferences`** — they link only shared libs. The lift reduces to: add `sharedXml` (one leaf lib, the lone new project, for DataTableTool) + an include-path redirect shim (the Phase-12 `Directory.Build.props` pattern) + the same SAFESEH/CRT-compat deltas Phase 12 already established. **No multi-day server closure exists for any of the three.** The escape-hatch (D-02) is available but, on the evidence, will not need to fire.

**Primary recommendation:** Lift all 3 AUTH-06 natives as planned (D-01); they are subsets of the already-proven Phase-12 closure plus one leaf lib (`sharedXml`). Treat the **runtime** behaviors of the exporters — `system("TemplateCompiler -compile …")` and `popen("p4 …")` Perforce calls — as the real (small) work item, not the build. Wrap BUILD verbs as `Process.Start` of the `tools/*.exe` with a structured JSON envelope mirroring the existing `roundtrip-*` pattern. `.rsp` synthesis for byte-exact `.tre` is viable: the TOC is CRC-sorted (derivable from filenames alone) and only the data-block order + per-file compression flag must be recovered from the Phase-7 reader (which already exposes both).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Compile `.tpf`→`.iff` (AUTH-03) | Native CLI (`tools/`) | `utinni-cli` subprocess wrapper | BUILD-from-source = SOE compiler (coexistence-by-verb-ownership). |
| Build `.tre` (AUTH-04) | Native CLI (`tools/`) | `utinni-cli` + Phase-7 reader (`.rsp` synth) | Compression determinism is native-only (zlib 1.1.4). |
| `.tdf`→schema (AUTH-02) | Native CLI (one-shot) | Static JSON artifact + OT editor + Area-2 tests | D-08: zero native dep on editor open path. |
| Datatable compile (AUTH-06) | Native CLI (`tools/`) | managed `DataTableWriter` (oracle, D-03) | Native authoritative; managed cross-checks. |
| Item exporters (AUTH-06) | Native CLI (`tools/`) | `utinni-cli` subprocess wrapper | Server-side dsrc inputs; exporters emit `.tpf` then chain TemplateCompiler. |
| SAVE (AUTH-05) | Managed (`UtinniCoreDotNet`) | `utinni-cli` verb | EDIT-existing-binary = byte-exact managed writers. |
| OT typed display (RESID-01) | Managed (codec + editor) | schema artifact (from AUTH-02) | Pure managed display layer over Phase-11 codec. |

## Standard Stack

This phase adds **no new third-party packages.** All dependencies already exist in the repo or are vendored prebuilt libs lifted alongside their consumer projects.

### Managed side (`utinni-cli`, net472)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| CommandLineParser | 2.9.1 | verb dispatch (`MapResult`) | already the CLI's dispatcher [VERIFIED: Utinni.Cli/Utinni.Cli.csproj] |
| Newtonsoft.Json | 13.0.3 | sorted-key JSON envelopes | already the `JsonOutput` backbone [VERIFIED: csproj] |
| System.Reflection.Metadata | 1.6.0 | PE export probe (existing) | already present [VERIFIED: csproj] |

### Native side (`tools/Utinni.Tools.sln`, v145/Win32)
| Component | Version | Purpose | Source |
|-----------|---------|---------|--------|
| zlib (linked) | **1.1.4** | byte-exact `.tre` compression determinant | `external/3rd/library/zlib/lib/win32/zlib.lib` [VERIFIED: tools/PINNED-SHA.md] |
| libxml2 (prebuilt) | **2.6.7** | `sharedXml` backing (DataTableTool XML input) | `external/3rd/library/libxml2-2.6.7.win32/lib/libxml2-win32-{debug,release}.lib` [VERIFIED: ls in swg-client-v2] |
| pcre | 4.1 | template tools (already lifted) | [VERIFIED: tools/DEPENDENCY-MANIFEST.md] |
| Perforce ClientAPI | 2002-era | template tools keep-link (already lifted) | [VERIFIED: tools/DEPENDENCY-MANIFEST.md] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Lifting `DataTableTool` native | managed `DataTableWriter` (D-02 fallback) | Loses byte-exact SOE authority for datatable compile; only fire if the `sharedXml`/libxml2 lift unexpectedly fails. Evidence says it won't. |
| Synthesized `.rsp` byte-exact | structural `.tre` compare (D-06 fallback) | Acceptable degraded gate if TOC/compression determinism proves unreachable. |

**Installation (native lib lift — sharedXml only new project):**
```bash
# git archive the one new lib + its prebuilt backing from the pinned SHA (Phase-12 method)
git -C D:/Code/swg-client-v2 archive 5fce7bb8 \
  src/engine/shared/library/sharedXml \
  src/external/3rd/library/libxml2-2.6.7.win32 | tar -x -C D:/Code/Utinni/tools
```

**Version verification (done this session):**
- zlib 1.1.4 pin confirmed [VERIFIED: tools/PINNED-SHA.md, tools/DEPENDENCY-MANIFEST.md].
- libxml2 2.6.7 prebuilt `.lib` present in swg-client-v2 [VERIFIED: `ls src/external/3rd/library/libxml2-2.6.7.win32/lib`].
- CommandLineParser 2.9.1 / Newtonsoft 13.0.3 [VERIFIED: Utinni.Cli.csproj].

## Package Legitimacy Audit

> No new external/registry packages are installed in this phase. Every native dependency is a verbatim lift from the pinned, in-repo `swg-client-v2@5fce7bb8` corpus (provenance: `tools/PINNED-SHA.md`, `[VERIFIED: git rev-parse]`). Every managed dependency already ships in `Utinni.Cli.csproj` and was vetted in Phase 4. slopcheck is **not applicable** — there is no registry-resolution surface to slop-squat.

| Dependency | Origin | Disposition |
|------------|--------|-------------|
| `sharedXml` (+ libxml2-2.6.7 prebuilt) | lift from `swg-client-v2@5fce7bb8` | Approved — in-repo, SHA-pinned |
| CommandLineParser 2.9.1 / Newtonsoft.Json 13.0.3 | existing `Utinni.Cli.csproj` (Phase 4) | Approved — no change |

## AUTH-06 Per-Tool Dependency-Closure Verdict (THE centerpiece research task — D-02)

**Method:** read each exporter's `main()`/run() source + its `.vcxproj` ProjectReferences + verified every cross-tree include against actual file existence in `swg-client-v2@5fce7bb8`. Source paths: `src/engine/shared/application/{Tool}/src/shared/{Tool}.cpp`.

### Shared structural facts (apply to all 3 exporters)
- **There is NO server tree in the client repo.** `ls src/engine/server` → does not exist [VERIFIED: find]. The `#include "serverGame/ServerObjectTemplate.h"` + the `.vcxproj` include path `..\..\..\..\..\server\library\serverGame\include\public` are **dead aliases** that resolve, via include-path redirect, to the `sharedTemplate` copy.
- **The referenced enums exist in already-lifted libs:** `ServerObjectTemplate::{ArmorCategory_Last, ArmorLevel_Last, XP_crafting, XP_craftingClothingArmor, CT_weapon}` all live in `sharedTemplate/src/shared/template/ServerObjectTemplate.h` (lines 45–250) [VERIFIED: grep]. `Crafting::IngredientType_Last` lives in `sharedGame/src/shared/core/CraftingData.h` (line 171) [VERIFIED: grep]. Both `sharedTemplate` and `sharedGame` were lifted in Phase 12 (`sharedTemplate` as a built project; `sharedGame` header-only) [VERIFIED: tools/DEPENDENCY-MANIFEST.md + ls tools/].
- **No exporter calls a serverGame *function*** — every cross-tree usage is a compile-time enum constant. serverGame appears in **zero** ProjectReferences lists [VERIFIED: grep of each `.vcxproj`].

### Per-tool verdict

| Tool | ProjectRefs | New libs needed | serverGame link? | Runtime tarpits | **VERDICT** |
|------|-------------|-----------------|------------------|-----------------|-------------|
| **DataTableTool** | 19 | **`sharedXml` (1 leaf lib)** | none (no enum use either) | none — pure spreadsheet→`.iff` | **CLEAN CLIENT LIFT** |
| **CoreWeaponExporterTool** | 18 | **none** (all 18 in sln) | enum-only (`CT_weapon`) | `system("TemplateCompiler -compile")`, `popen("p4 …")` | **CLEAN CLIENT LIFT** (handle runtime) |
| **ArmorExporterTool** | 14 | **none** (all 14 in sln) | enum-only (`ArmorCategory_Last`, `XP_crafting`, `IngredientType_Last`) | `system("TemplateCompiler -compile")`, `popen("p4 …")` | **CLEAN CLIENT LIFT** (handle runtime) |

**DataTableTool's 19 ProjectReferences** (all in `tools/Utinni.Tools.sln` except `sharedXml`): archive, fileInterface, unicodeArchive, sharedCompression, sharedDebug, sharedFile, sharedFoundationTypes, sharedFoundation, sharedIoWin, sharedLog, sharedMath, sharedMemoryManager, sharedMessageDispatch, sharedNetworkMessages, sharedRandom, sharedSynchronization, sharedThread, sharedUtility, **sharedXml** [VERIFIED: grep of DataTableTool.vcxproj cross-checked against `Utinni.Tools.sln`].

**`sharedXml` closure is trivial:** 0 ProjectReferences (leaf lib), backed by the prebuilt `libxml2-2.6.7.win32` static lib already vendored in swg-client-v2 [VERIFIED: grep sharedXml.vcxproj → 0 refs + libxml2 lib present]. This is the lone genuinely-new build artifact in the entire phase.

**CoreWeaponExporterTool / WeaponExporterTool naming:** the roadmap's "WeaponExporterTool" exists as a separate, older tool; CONTEXT correctly pins **`CoreWeaponExporterTool`** as the target (CW has 18 refs and the richer column set incl. core/power-bit experimentation columns). `WeaponExporterTool` (older, 38-column) can be ignored unless the planner wants both — recommend CoreWeaponExporterTool only, per CONTEXT.

### Runtime tarpit (the actual — small — work, not a build blocker)
The two exporters do **not** just emit `.iff`. Their `run()` (verified in source):
1. Read a **datatable `.iff`** input (`DataTableManager::getTable`), iterate rows.
2. `fopen`/`fprintf` a **server `.tpf` + a shared `.tpf`** text template to disk (the dsrc tree).
3. `getFileFromPerforce`/`addFileToPerforce` via `popen("p4 fstat/edit/add …")` — **Perforce shell-outs**.
4. `system("TemplateCompiler -compile <tpf>")` — **chains the sibling compiler** to turn `.tpf`→`.iff`.

Implications the planner MUST design around (these are the exporters' real character, not build risk):
- **Input is a datatable `.iff`, output is `.tpf` + compiled `.iff`** — the exporter is a *schematic generator*, not a leaf compiler. AUTH-06's "run the item exporters" verb wraps this whole chain.
- **Perforce calls will fail** in a Utinni context (no p4 client). `getFileFromPerforce` does `FATAL` if `popen` returns NULL ("Cannot access Perforce"). **This must be neutralized** — either (a) a small source delta stubbing the p4 helpers to no-op (mirrors Phase-12's `UTINNI_TOOLS_NO_SHAREDLOG` decouple precedent), or (b) ensure `popen("p4 …")` returns gracefully. Recommend the source-stub approach (consistent with the keep-link-but-never-invoke Perforce posture in DEPENDENCY-MANIFEST.md).
- **`system("TemplateCompiler …")`** needs `TemplateCompiler.exe` on PATH (or CWD) at exporter runtime — the `utinni-cli` BUILD verb must stage both exes together, or the wrapper supplies the path. Note the source calls bare `"TemplateCompiler"` (no `_d` suffix, no path) — a PATH/CWD concern for the subprocess wrapper.
- **`tools.cfg`**: ArmorExporterTool loads `<exeDir>/tools.cfg` (`ConfigArmorExporterTool` reads `schematicTemplatePath`, `slotNameTable`, etc.) [VERIFIED: ArmorExporterTool.cpp main + ConfigArmorExporterTool]. A `tools.cfg` fixture must ship beside the exe. CoreWeaponExporterTool similarly uses `ConfigCoreWeaponExporterTool`.

**D-02 disposition:** No tool falls back. All 3 are clean client lifts. The escape-hatch stays unused. The *exporters'* real cost is the Perforce-stub + the TemplateCompiler-chain plumbing + a `tools.cfg`/datatable input fixture — design work, not multi-day porting. If the planner wants to de-risk, sequence DataTableTool first (zero runtime tarpits, only the `sharedXml` lib lift), then the two exporters together (shared Perforce-stub + chain pattern).

## AUTH-06 Input Formats (Claude's-discretion resolution: CSV vs XML vs tab)

**Resolved against the native tool's actual acceptance** (per CONTEXT: "their native input format wins"):
- `DataTableTool` accepts a **spreadsheet** — `DataTableWriter::loadFromSpreadsheet(inputFile)`. `DataTableWriter::isXmlFile()` branches: `.xml` → XML path (needs `sharedXml`/libxml2); otherwise **tab-delimited text** (the SOE `.tab` "spreadsheet" export, NOT comma-CSV) [VERIFIED: DataTableTool.cpp:160,176 + DataTableWriter.cpp includes sharedXml].
- **Recommendation:** the `compile-datatable` verb accepts **both** a tab-delimited spreadsheet and `.xml`, exactly as the native tool does — the verb is a thin pass-through; the native tool's own `isXmlFile` switch decides. Do NOT invent a comma-CSV front-end; the native expects tab-delimited. (The managed `CsvCellCoercion` is a *different* path used by the editor — keep them distinct.)
- The **exporters' input is a datatable `.iff`** (already-compiled), not a spreadsheet — they read `DataTableManager::getTable(inputFile.iff)` [VERIFIED: ArmorExporterTool.cpp:254].

## BUILD-verb subprocess wrapping (Claude's-discretion: subprocess mechanics)

The existing CLI is **fully in-process** (managed readers/writers); there is no prior `Process.Start` precedent in a command — `NativeExportProbe.cs` parses PE export tables in-process but does NOT spawn anything [VERIFIED: read NativeExportProbe.cs — no Process/ProcessStartInfo]. So the BUILD verbs introduce the **first subprocess seam** in `utinni-cli`. Recommended shape (mirrors the existing `roundtrip-*` envelope + exit-code contract):

```csharp
// Source: synthesized from RoundtripTabCommand.cs envelope pattern + Process.Start best practice
[Verb("compile-template", HelpText="Compile a .tpf object-template source to .iff via the revived TemplateCompiler.")]
public class CompileTemplateOptions {
    [Value(0, MetaName="path", Required=true)] public string Path { get; set; }
    [Option("tool-path", HelpText="Override path to TemplateCompiler exe (default: resolved beside utinni-cli).")] public string ToolPath { get; set; }
}
// Run(): resolve exe path (D: tool-dir convention) → FileNotFound→exit 3 if missing →
//   var psi = new ProcessStartInfo(exe, $"-compile \"{o.Path}\"") {
//       RedirectStandardOutput=true, RedirectStandardError=true,
//       UseShellExecute=false, CreateNoWindow=true, WorkingDirectory=<staged tool dir> };
//   capture stdout/stderr/exitcode → emit envelope:
//   { "tool":"TemplateCompiler", "exitCode":N, "outputPath":..., "stdout":..., "stderr":..., "produced":bool }
```

**Subprocess design rules for the planner:**
- **Exit-code passthrough**: native non-zero → CLI exit 2 (build error) with captured stderr; preserve the existing 0/1/2/3 contract (0 ok, 1 usage, 2 tool/parse error, 3 file-not-found) [VERIFIED: RoundtripTabCommand exit-code convention].
- **No shell**: `UseShellExecute=false` + explicit arg array — never string-concatenate user paths into a shell command (the exporters' own `system()`/`popen()` are a cautionary anti-pattern, not a model to copy).
- **Working directory**: the exporters chain `system("TemplateCompiler")` and read `tools.cfg` from the exe dir — stage all tool exes + `tools.cfg` in one dir and set `WorkingDirectory` to it.
- **Banner normalization (Pitfall)**: native tools print `__DATE__ " " __TIME__` banners (DataTableTool.cpp:141, ArmorExporterTool.cpp:217) and TemplateDefinitionCompiler embeds `__DATE__`/abs-path in generated `.cpp/.h` — **strip these before any golden compare** (carried from Phase-12 Pitfall 6 / DEPENDENCY-MANIFEST byte-exact note).
- **stdout pollution**: native tools call `getchar()` in `usage()`/test paths — ensure the wrapper never triggers an interactive path (always pass a valid input; never the `-h`/no-arg branch in automation).

## AUTH-04 `.rsp` synthesis + byte-exact determinism (D-06)

**`.rsp` line format (verified in BOTH the builder and the sibling rsp-builder):**
- The builder `TreeFileBuilder::addResponseFile` parses lines of the form **`<diskPath> @ <treePath>`** (disk-first), with an **uncompressed marker** variant **`<diskPath> @u <treePath>`** — the `u` immediately after `@` flags "do not compress this file" [VERIFIED: tools/.../TreeFileBuilder.cpp:387-405]. (CONTEXT's `diskPath@treePath` is correct on order.)
- Lines without `@` are skipped; a `TF::open(x), …` log-prefix mode is also supported (the secondary log-parse mode) [VERIFIED: TreeFileBuilder.cpp:346-364].
- The sibling `TreeFileRspBuilder` emits `<treePath> @ <diskPath>` (the *reverse*) bucketed by extension into compressed/uncompressed maps — it is NOT the format the builder's `-r` consumes directly, so do not use it as the synthesis template. Synthesis emits **builder-format** lines [VERIFIED: TreeFileRspBuilder.cpp:179 vs TreeFileBuilder.cpp:389].

**Byte-exact determinism — the decisive finding:** the `.tre` layout has TWO independent orderings:
1. **TOC order = CRC-of-treename sorted.** `addFile` inserts each entry via `std::lower_bound(tocOrder, …, LessFileEntryCrcNameCompare)` [VERIFIED: TreeFileBuilder.cpp:495,538]. **The TOC order is therefore fully derivable from the filename set alone — synthesis does NOT need to recover original TOC order.**
2. **Data-block order = `.rsp` input order.** A separate `responseFileOrder` vector preserves insertion (= `.rsp` line) order [VERIFIED: TreeFileBuilder.cpp:539]. **This is the order the synthesized `.rsp` MUST reproduce** for byte-identity of the file-data region.

**What the Phase-7 reader must supply for synthesis (it already does):**
- **Record order** (= original data-block order) → `TreFile.Records` is an ordered `IReadOnlyList<TreRecord>` [VERIFIED: TreFile.cs:86].
- **Per-file tree name** → `TreArchiveIndex.TreeFileName` / record name bytes [VERIFIED: TreArchiveIndex.cs:63].
- **Per-file compression flag** → `Compressor` (0=none, 1=deflate, 2=zlib) [VERIFIED: TreArchiveIndex.cs:52-53]. Compressor==0 → emit the `@u` uncompressed marker; else default (compressed) line.

**Determinism risks the planner must treat as a testable hypothesis (D-06):**
- **zlib level / strategy:** the linked zlib 1.1.4 lib determines compressed bytes; same input + same zlib + default level should be byte-stable, but the original may have used a non-default level. If byte-identity fails on *compressed* files but holds on uncompressed, that isolates the compression-parameter mismatch.
- **TOC/name-block compression:** `-noTOCCompression`/`-noFileCompression` flags exist [VERIFIED: TreeFileBuilder.cpp:46-47]; the original `.tre`'s TOC compressor (readable via the Phase-7 reader's `InfoCompressedSize`/`Compressor`) must be matched.
- **Recommended test ladder:** (1) round-trip an **uncompressed-only** synthetic `.tre` first (eliminates zlib variance) → expect byte-exact; (2) then a compressed real archive from the 53-file corpus → if mismatch, structural-compare fallback (D-06) + record the zlib-param gap as a gate-finding. Do NOT block the phase on full compressed byte-identity.

## AUTH-02 / RESID-01: TemplateDefinitionCompiler schema → OT typed display

**What `TemplateDefinitionCompiler` consumes and emits:**
- **Input:** a `.tdf` (template-definition file) — a text grammar describing one object-template *class*: its parameters, each with a `ParamType`, optional list-ness, struct/enum sub-definitions [VERIFIED: TemplateData.h struct `Parameter` + `ParseState`].
- **Output:** **generated C++ `.cpp/.h`** (not a data file) — `TemplateDefinitionCompiler_d.exe -compile <input.tdf>` [VERIFIED: tools/DEPENDENCY-MANIFEST.md:52]. The compiler's *internal model* (`TemplateData`) is the schema; AUTH-02's job is to surface that model as JSON rather than as C++.

**The schema vocabulary (the param→type map the OT editor needs):**
- `enum ParamType` (14 values): `TYPE_NONE, TYPE_COMMENT, TYPE_INTEGER, TYPE_FLOAT, TYPE_BOOL, TYPE_STRING, TYPE_STRINGID, TYPE_VECTOR, TYPE_DYNAMIC_VAR, TYPE_TEMPLATE, TYPE_ENUM, TYPE_STRUCT, TYPE_TRIGGER_VOLUME, TYPE_FILENAME` [VERIFIED: TemplateData.h:36-52].
- `enum ListType` (4 values): `LIST_NONE, LIST_LIST, LIST_INT_ARRAY, LIST_ENUM_ARRAY` [VERIFIED: TemplateData.h:54-60]. **This is the flag that distinguishes the multi-chunk list/struct params (slots/attributes/hair) from scalars.**
- Per-`Parameter`: `name`, `type` (ParamType), `extendedName` (for template/enum/struct), `list_type` (ListType), `list_size`, `enum_list_name` [VERIFIED: TemplateData.h:63-75].

**Mapping to the OT codec's raw-fallback (RESID-01):** The Phase-11 `ObjectTemplateParamCodec` decodes scalar params typed but routes anything with interior structure (WEIGHTED_LIST / RANGE / DIE_ROLL / **struct / list**) to `RawBytesHexFallback` carrying verbatim bytes [VERIFIED: ObjectTemplateParamCodec.cs:46-50,188,259]. `MutableObjectTemplate.cs:201-202` explicitly names the residual: "list/array params (e.g. draft-schematic 'slots'/'attributes', hair tint lists) as a header chunk followed by nameless element chunks" [VERIFIED: grep]. **The schema's `ListType != LIST_NONE` params ARE exactly this residual.** D-07's "common params modders edit" = the `LIST_LIST`/`LIST_*` params of type `TYPE_STRUCT` (slots, attributes) and the array params (hair). The OT editor consumes the per-class JSON schema, looks up a param's `(type, list_type)`, and renders a structured widget for the common ones / a typed-label+hex for the rare tail.

**AUTH-02 gate-finding (carried from Phase 12):** **Zero `.tdf` source assets exist** in either repo [VERIFIED: `find -name "*.tdf"` → 0]. The compiler needs a `.tdf` input to emit a schema. Two paths for the planner: (a) supply/author the object-template `.tdf` definitions (the canonical SOE set), or (b) the compiler may carry built-in/default definitions — investigate `TemplateGlobals.cpp`/`TemplateDefinitionFile.cpp` at plan time. The schema artifact (D-08) is committed once generated, so this is a one-time generation cost, not a per-build dependency.

## AUTH-05: SAVE verb (thin wrap of existing managed targets)

All four save targets already exist and are byte-validated by prior phases — `save` is genuinely "cheap" (D-09):
- IFF → `IffSaveTargets` (Phase 8: `SaveLooseOverride`/`SaveToPath`/`SaveInPlace`) [VERIFIED: STATE.md Phase-08 P05].
- datatable → `DatatableSaveTargets` (Phase 9, forwards to IffSaveTargets) [VERIFIED: STATE.md Phase-09 P05].
- stf → StringTable save targets (Phase 10).
- OT → `ObjectTemplateSaveTargets` (Phase 11, modes 1/2/4) [VERIFIED: STATE.md Phase-11 P04].
- **Envelope** `{written, path, bytesWritten, backupPath, validated}` is ROADMAP-locked [VERIFIED: ROADMAP.md:296].
- **Repack stays OUT of `save`** (D-10): the destructive `.tre` repack is `TreRepackSaveTarget` routed through `TreBackupPath`/`TreRepackLock` (both already extracted to `UtinniCoreDotNet/Saving/`) [VERIFIED: ls UtinniCoreDotNet/Saving + STATE.md Phase-08 P07]. The repack verb is a *separate* verb; Phase-14 MCP gates it behind `dry_run`.

**Design note:** the save targets live in the **plugin/TJT assembly** (WinForms-coupled) for some modes, but the framework primitives (`LooseOverridePath`, `TreBackupPath`, `ReloadAssetClassifier`) are in `UtinniCoreDotNet` and ARE project-referenceable from the net472 CLI/test projects. Verify at plan time which save-target legs are framework vs plugin (Phase 8/9 split the path-defense into framework, the WinForms wiring into plugin) — the `save` verb should call the **framework** legs, not the WinForms host.

## Architecture Patterns

### System data flow
```
                       ┌──────────────────────── utinni-cli (net472) ────────────────────────┐
 .tpf / .tdf / spreadsheet ──▶ BUILD verb (compile-*/build-*) ──Process.Start──▶ tools/*.exe (v145/Win32)
                       │                                          (native, SOE-authoritative)   │
                       │                                                   │                     │
 edited asset (IFF/tab/ ──▶ SAVE verb (save) ──in-process──▶ UtinniCoreDotNet managed save targets
   stf/OT)             │                                          (byte-exact EDIT path)         │
                       │                                                   │                     │
 .tdf ──▶ compile-definition ──Process.Start──▶ TemplateDefinitionCompiler ──▶ per-class JSON schema
                       └───────────────────────────────────────────────────────────┬──────────┘
                                                                                     │
                            ┌────────────────────────────────────────────────────────┘
                            ▼                                  ▼
              FormObjectTemplateEditor (typed widgets)   Area-2 cross-check tests (oracle)
                  (RESID-01: ListType→widget)            (native .iff vs managed reader/writer, D-03)

 real .tre (53-file corpus) ──Phase-7 reader──▶ {records order, treeName, compressor} ──▶ synth .rsp ──▶ build-tre ──▶ byte-compare (D-06)
```

### Recommended structure (extends existing)
```
Utinni.Cli/Commands/
├── CompileTemplateCommand.cs      # AUTH-03  (Process.Start TemplateCompiler)
├── BuildTreCommand.cs             # AUTH-04  (Process.Start TreeFileBuilder + .rsp synth helper)
├── CompileDefinitionCommand.cs    # AUTH-02  (Process.Start TemplateDefinitionCompiler → JSON schema)
├── CompileDatatableCommand.cs     # AUTH-06  (Process.Start DataTableTool)
├── ExportArmorCommand.cs          # AUTH-06  (Process.Start ArmorExporterTool)
├── ExportWeaponCommand.cs         # AUTH-06  (Process.Start CoreWeaponExporterTool)
├── SaveCommand.cs                 # AUTH-05  (in-process; 4-format dispatch)
├── RepackTreCommand.cs            # D-10     (in-process; separate from save)
└── Subprocess/NativeToolRunner.cs # shared Process.Start + envelope + banner-strip helper
```

### Anti-Patterns to Avoid
- **Re-implementing a BUILD path in managed.** Coexistence-by-verb-ownership: `compile-*`/`build-*` MUST wrap natives. Managed `DataTableWriter` is the *oracle*, not the verb implementation (unless D-02 fallback fires — it won't).
- **Routing `.tre` repack through `save`.** D-10 — keep the default write surface safe-by-construction.
- **Copying the exporters' `system()`/`popen()` shell-out style** into the wrapper. Use `Process.Start` with arg arrays, `UseShellExecute=false`.
- **Golden-comparing native output without banner normalization.** `__DATE__`/`__TIME__`/abs-path leak into output → false mismatches.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| datatable spreadsheet→`.iff` | a managed compiler | lifted `DataTableTool` | SOE-authoritative bytes; managed is the oracle only |
| `.tre` compression | a managed zlib repack | native `TreeFileBuilder` + zlib 1.1.4 | byte-exact determinant is the 2002 zlib lib |
| `.rsp` recovery | guessing file order | Phase-7 reader `Records`/`Compressor` | order + compression flag already exposed |
| param→type schema | hand-maintained JSON | `TemplateDefinitionCompiler` → emit | SOE is the schema source of truth (D-08) |
| save path-defense | new containment logic | framework `LooseOverridePath`/`TreBackupPath` | already built + tested (Phase 8) |
| subprocess JSON envelope | ad-hoc per verb | shared `NativeToolRunner` + `JsonOutput` | sorted-key contract consistency |

**Key insight:** this phase is ~80% *wrapping and wiring already-built things*; the only genuinely new native artifact is the `sharedXml` leaf-lib lift, and the only genuinely new managed pattern is the subprocess seam.

## Runtime State Inventory

> This is a wrapping/build phase, not a rename/migration. Most categories are N/A, but the exporters' runtime behaviors and the staged-tool layout warrant an explicit inventory.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — verified: no datastore keys are renamed; CLI is stateless. | none |
| Live service config | The exporters shell out to **Perforce** (`popen("p4 fstat/edit/add")`) and **`system("TemplateCompiler")`** at runtime [VERIFIED: ArmorExporterTool.cpp:300,326,564-621]. | Neutralize p4 (source-stub to no-op) + stage TemplateCompiler.exe + `tools.cfg` beside the exporter exe. |
| OS-registered state | None — no Task Scheduler/service registration. | none |
| Secrets/env vars | None new. | none |
| Build artifacts | New `tools/` exes (DataTableTool, ArmorExporterTool, CoreWeaponExporterTool) join `Utinni.Tools.sln`; **CI hard-gate auto-extends** because the lane builds the whole sln [VERIFIED: ci.yml:176 builds `tools\Utinni.Tools.sln`]. The `sharedXml` lib + libxml2 prebuilt are new on-disk. | Add 3 app vcxprojs + sharedXml to the sln; commit libxml2 prebuilt + sharedXml source. |

## Common Pitfalls

### Pitfall 1: The exporter Perforce `FATAL`
**What goes wrong:** `getFileFromPerforce` calls `FATAL(p4 == NULL, ("Cannot access Perforce"))` — in a Utinni context with no p4 client, the exporter aborts.
**Why:** the exporters were authored for the SOE build farm with Perforce always present.
**How to avoid:** source-stub the p4 helpers to no-op (mirrors Phase-12's `UTINNI_TOOLS_NO_SHAREDLOG` decouple); document as an AUTH-06 revival delta in DEPENDENCY-MANIFEST.md.
**Warning sign:** "Cannot access Perforce" in captured stderr.

### Pitfall 2: `system("TemplateCompiler")` path resolution
**What goes wrong:** the exporter calls bare `"TemplateCompiler"` (no path, no `_d` suffix); if not on PATH/CWD it fails the `FATAL(compileResult != 0)`.
**How to avoid:** stage all tool exes in one dir and set the subprocess `WorkingDirectory`; or patch the exporter to use a resolvable path.

### Pitfall 3: Banner/timestamp leakage into goldens
**What goes wrong:** `__DATE__ " " __TIME__` banners + TemplateDefinitionCompiler's embedded date/abs-path break byte-exact compares.
**How to avoid:** normalize before compare (carried Phase-12 Pitfall 6). Provide a `NormalizeBanner` regex in the golden harness.

### Pitfall 4: `.rsp` direction confusion
**What goes wrong:** using `TreeFileRspBuilder`'s `treePath @ diskPath` output as the synthesis template — the builder's `-r` expects `diskPath @ treePath` (reverse).
**How to avoid:** emit builder-format lines (disk-first); use `@u` for compressor==0 files.

### Pitfall 5: Compressed byte-identity overreach
**What goes wrong:** blocking the phase on full compressed-`.tre` byte-identity when zlib-param drift makes it unreachable.
**How to avoid:** test uncompressed-first; structural-compare fallback for compressed (D-06); record as gate-finding, don't stall.

### Pitfall 6: `save` calling WinForms-coupled save legs
**What goes wrong:** some save-target modes live in the TJT/plugin (WinForms) assembly, not project-referenceable from net472 CLI/tests.
**How to avoid:** route `save` through the **framework** legs (`UtinniCoreDotNet/Saving/*`); verify the framework-vs-plugin split per format at plan time (Phase 8/9 precedent).

## Code Examples

### `.rsp` synthesis from a real `.tre` (D-06 — synthesized from verified reader + builder behavior)
```csharp
// Phase-7 reader exposes ordered Records + per-record Compressor + TreeFileName.
// Emit builder-format lines (diskPath @ treePath, @u for uncompressed), in Records order.
foreach (var rec in treFile.Records)                 // data-block order == .rsp order (responseFileOrder)
{
    File.WriteAllBytes(diskPath(rec), treFile.GetRecordData(rec));  // extract to disk
    string marker = rec.Compressor == 0 ? "@u" : "@";              // compressor 0 => uncompressed
    rsp.AppendLine($"{diskPath(rec)} {marker} {rec.TreeFileName}");
}
// TOC order is CRC-sorted by the builder itself (LessFileEntryCrcNameCompare) — do NOT pre-sort the .rsp.
// Then: TreeFileBuilder_d.exe -r build.rsp out.tre ; SHA256-compare to original (uncompressed-first ladder).
```

### Native BUILD verb envelope (synthesized from RoundtripTabCommand pattern)
```csharp
var psi = new ProcessStartInfo(toolExe, BuildArgs(o)) {
    RedirectStandardOutput = true, RedirectStandardError = true,
    UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = stagedToolDir };
using (var p = Process.Start(psi)) {
    string outp = p.StandardOutput.ReadToEnd(); string err = p.StandardError.ReadToEnd(); p.WaitForExit();
    var result = new JObject {
        ["tool"] = toolName, ["exitCode"] = p.ExitCode,
        ["outputPath"] = expectedOut, ["produced"] = File.Exists(expectedOut),
        ["stderr"] = NormalizeBanner(err) };
    return p.ExitCode == 0 ? JsonOutput.EmitSuccess(verb, result)
                           : JsonOutput.EmitError(verb, "ToolError", err, exitCode: 2);
}
```

## State of the Art

| Old Approach | Current Approach | When | Impact |
|--------------|------------------|------|--------|
| "byte-exact blocked by missing `.rsp`" (Phase-12 gate-finding) | `.rsp` is synthesizable from a real `.tre` via Phase-7 reader (TOC CRC-derived, data-order recoverable) | Phase 13 (D-06) | reframes the gate-finding as a testable hypothesis |
| exporters assumed "server-tainted, may need multi-day lift" | exporters use serverGame enums ONLY; enums live in already-lifted sharedTemplate/sharedGame; zero serverGame ProjectRefs | this research | D-02 escape-hatch will not fire |
| self-authored Area-2 fixtures | 53-file real pre-CU 0005/0006 SOE corpus as oracle inputs (D-05) | Phase 13 | graduates correctness gate to real SOE data |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `sharedXml` lifts as cleanly as Phase-12 leaf libs (0 ProjectRefs, libxml2 prebuilt links at v145) | Stack / AUTH-06 | If libxml2-2.6.7 prebuilt needs CRT-compat shims (like Perforce did), DataTableTool's lift gains a small delta — still not multi-day; D-02 managed fallback remains. `[ASSUMED]` — not yet built at v145. |
| A2 | Source-stubbing the exporters' Perforce helpers is sufficient to make them run headless | AUTH-06 runtime | If the exporters depend on p4 for *correct output* (not just bookkeeping), stubbing changes behavior. Source review says p4 is pure VCS bookkeeping (edit/add), not data — low risk. `[ASSUMED]` pending a run. |
| A3 | `TemplateDefinitionCompiler` can emit a usable schema from supplied/built-in `.tdf` (no `.tdf` assets exist) | AUTH-02 | If no `.tdf` is supplied AND the compiler has no built-in definitions, AUTH-02 needs an authored `.tdf` set first. `[ASSUMED]` — `TemplateGlobals.cpp`/`TemplateDefinitionFile.cpp` not yet read for built-in defs. |
| A4 | Synthesized-`.rsp` byte-exact holds for uncompressed files; compressed may need structural fallback | AUTH-04 | If even uncompressed mismatches, a deeper TOC/name-block layout difference exists — structural compare still satisfies D-06's fallback. `[ASSUMED]` pending a build. |
| A5 | The 4 managed save-target framework legs are net472-project-referenceable from the CLI | AUTH-05 | If a format's only save leg is WinForms-coupled, `save` for that format needs a framework extraction first (small). `[ASSUMED]` — per-format split not exhaustively verified. |

## Open Questions (RESOLVED)

1. **Does `TemplateDefinitionCompiler` carry built-in object-template definitions, or does it strictly require a `.tdf` input?**
   - **RESOLVED (2026-06-03, orchestrator source-read):** It **strictly requires a `.tdf` input** — `TemplateDefinitionCompiler.cpp:438 parseTemplateDefinitionFile(File &fp)` reads a `TemplateDefinitionFile` from a file path, then `writeTemplate(tdfFile, path)` generates C++ header/source + the param→type schema. **No built-in defs.** And **zero `.tdf` assets exist** in swg-client-v2 or `tools/` (`find -iname '*.tdf'` = 0) — same gate-finding class as Phase-12's byte-exact (no source assets).
   - **DISPOSITION (bounds scope — no wholesale `.tdf` authoring):**
     - **AUTH-02 `compile-definition`:** ships and is golden-tested against a **small authored minimal `.tdf` fixture** (representative — proves `.tdf → schema-artifact`); the absence of the canonical SOE `.tdf` set is a **documented gate-finding** (mirrors Phase-12; real `.tdf` becomes flowable later). Do NOT author the full SOE `.tdf` set in this phase.
     - **RESID-01 typed display:** sources the common slot/attribute/hair **struct layouts from the already-present generated `Shared*ObjectTemplate` classes** — the compiler's historical OUTPUT, verified present at `tools/src/engine/shared/library/sharedGame/.../objectTemplate/Shared{Tangible,Weapon,DraftSchematic,...}ObjectTemplate.{h,cpp}` — NOT from re-running the compiler on absent `.tdf`. The schema-artifact for the editor (D-08) is produced from these for the common classes; the rare tail stays labeled-hex.
   - **Consequence for plans:** 13-05 `compile-definition` does NOT carry a `.tdf`-sourcing unknown into execution; 13-06 RESID-01 reads the generated `Shared*ObjectTemplate` classes for struct layouts. **No `.tdf` authoring task — 13-05 does NOT need splitting** (checker WARNING-2 dispositioned: the heavy-authoring trigger does not fire).

2. **Which save-target legs per format are framework (net472-referenceable) vs WinForms-plugin?**
   - **RESOLVED (PATTERNS finding):** all 4 `*SaveTargets` live in the WinForms plugin `TJT.Saving` (NOT net472-referenceable). `save` reimplements the thin write over framework primitives directly (`LooseOverridePath.Resolve` + framework serializers + atomic `Flush(true)`) — Option 1, no framework extraction (13-03 implements this).

3. **zlib 1.1.4 default compression level vs the original `.tre` corpus's level.**
   - **RESOLVED (dispositioned):** uncompressed-first test ladder isolates this; structural-compare fallback if compressed byte-identity is unreachable (D-06). No blocker.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| v145 / VS 2026 toolset | native tool builds | ✓ | 14.5x | — (CI gate already requires it) |
| `tools/Utinni.Tools.sln` (Phase-12 lift tree) | all BUILD verbs | ✓ | green @ v145 | — |
| zlib 1.1.4 prebuilt | `.tre` compression determinant | ✓ | 1.1.4 | — |
| libxml2-2.6.7 prebuilt | `sharedXml` (DataTableTool XML) | ✓ | 2.6.7 | tab-delimited input path doesn't need it |
| 53-file SWGEmu pre-CU corpus | D-05 cross-check oracle inputs | ✓ | 0005/0006 | self-authored fixtures (degraded) |
| `.tpf` source assets | AUTH-03 golden fixtures | ✗ | — | author/supply a `.tpf` + known-good `.iff` |
| `.tdf` source assets | AUTH-02 schema generation | ✗ | — | author/supply, or use built-in defs (Open Q1) |
| `.tab`/spreadsheet source | AUTH-06 datatable compile fixture | ✗ | — | author a tab-delimited fixture |
| Perforce client (`p4`) | exporters' runtime (as-authored) | ✗ | — | **source-stub to no-op** (Pitfall 1) |
| FlaUI / WinForms UI automation | (none — out of scope CON-TT-03) | ✗ | — | manual Tier-4 (RESID-01 editor visual) |

**Missing with no in-repo fallback (must be addressed by the plan):** `.tpf`/`.tdf`/`.tab` golden source assets (author or supply — same class as Phase-12's gate-findings, now resolvable because the verbs make real assets flowable).
**Missing with fallback:** Perforce client → source-stub; XML input → tab-delimited path.

## Validation Architecture

> `workflow.nyquist_validation` not set to false → section included.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (net472) — `Utinni.Cli.Tests` + `UtinniCoreDotNet.Tests` [VERIFIED: ls] |
| Config file | none — `dotnet test --no-build` per Windows/WinForms build recipe |
| Golden harness | `Utinni.Cli.Tests/Infrastructure/GoldenTestRunner.cs` (`Matches`/`MatchesText`, dump-on-mismatch) [VERIFIED: read] |
| In-proc runner | `InProcessCliRunner.cs` (stdout/stderr capture; works for in-process verbs) |
| Native build gate | `tools\Utinni.Tools.sln` built in CI `Build tools solution — AUTH-01 hard gate` step [VERIFIED: ci.yml:174-176] |

### Phase Requirements → Test Map
| Req | Behavior | Test Type | Command | Exists? |
|-----|----------|-----------|---------|---------|
| AUTH-03 | `.tpf`→`.iff` byte-correct + managed-OT-reader typed-asserts output | golden + cross-check | `dotnet test --no-build --filter CompileTemplate` | ❌ Wave 0 (+ `.tpf` fixture) |
| AUTH-04 | synth-`.rsp`→`build-tre`→byte/structural compare | golden | `dotnet test --no-build --filter BuildTre` | ❌ Wave 0 |
| AUTH-02 | `.tdf`→per-class JSON schema; schema shape stable | golden (JSON) | `dotnet test --no-build --filter CompileDefinition` | ❌ Wave 0 (+ schema golden) |
| AUTH-05 | `save` per-format envelope `{written,…}` correct | golden + roundtrip | `dotnet test --no-build --filter Save` | ❌ Wave 0 |
| AUTH-06 | `compile-datatable` native vs managed `DataTableWriter` byte-compare | cross-check (D-03) | `dotnet test --no-build --filter CompileDatatable` | ❌ Wave 0 (+ `.tab` fixture) |
| AUTH-06 | exporters run headless, emit `.tpf`+`.iff` | golden | `dotnet test --no-build --filter Export` | ❌ Wave 0 (+ datatable `.iff` + `tools.cfg` fixtures) |
| RESID-01 | OT editor renders `ListType != LIST_NONE` params structured | unit (codec) + Tier-4 visual | `dotnet test --no-build --filter ObjectTemplate` (visual = manual) | ⚠ codec unit automatable; visual = Tier-4 |

### Sampling Rate
- **Per task commit:** targeted filter (e.g. `--filter CompileTemplate`).
- **Per wave merge:** full `dotnet test --no-build` (both test projects) + `msbuild tools\Utinni.Tools.sln`.
- **Phase gate:** full suite green + native sln green before `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `Subprocess/NativeToolRunner.cs` + its golden harness extension (banner-normalize) — shared by all BUILD verbs.
- [ ] `.tpf` fixture + known-good `.iff` (AUTH-03) — author/supply.
- [ ] `.tab`/spreadsheet fixture + `tools.cfg` + datatable `.iff` fixture (AUTH-06).
- [ ] schema-JSON golden (AUTH-02) — depends on Open Q1 (`.tdf` source).
- [ ] `sharedXml` + libxml2 added to `Utinni.Tools.sln`; 3 exporter vcxprojs added (CI gate auto-extends).
- [ ] Perforce-stub source delta for the 2 exporters.

## Security Domain

> `security_enforcement` absent = enabled. This phase adds a **subprocess seam** and a **write surface** — both security-relevant.

### Applicable ASVS Categories
| ASVS | Applies | Standard Control |
|------|---------|------------------|
| V5 Input Validation | yes | Validate `--path` args; never pass agent/user strings into a shell — `Process.Start` with arg arrays, `UseShellExecute=false`. |
| V12 File / Resource | yes | `save` defaults loose-override; path-containment via framework `LooseOverridePath` (root-fail-closed is Phase-14's job, but `save` must not write outside the asset tree). Repack gated as separate verb (D-10). |
| V6 Cryptography | no | none (no crypto introduced). |
| V2/V3/V4 Auth/Session/Access | no | local offline CLI; no auth surface. |

### Known Threat Patterns for this stack
| Pattern | STRIDE | Mitigation |
|---------|--------|------------|
| Command injection via crafted asset path into `system()`/`popen()` | Tampering/EoP | The wrapper uses `Process.Start` arg arrays (NOT the exporters' own `system()`). The exporters' internal `system("TemplateCompiler "+path)` IS injectable — sanitize/validate the path the wrapper feeds them, or patch the exporter to arg-array exec. **Flag for the planner: the exporters' `system()` shell-out is a latent injection vector; the wrapper must validate inputs before invoking.** |
| Path traversal on `save --path` | Tampering | framework `LooseOverridePath` containment; repack-only-via-explicit-verb (D-10). |
| zlib 1.1.4 known CVEs | DoS | accepted offline/trusted-input for this phase (T-12-02 tension, DEPENDENCY-MANIFEST.md); re-evaluate when inputs become agent-influenceable (Phase 14). |
| Banner/abs-path info leak in generated output | Info disclosure | normalize before commit (Pitfall 3) — also avoids leaking the maintainer's absolute paths into committed goldens. |

## Sources

### Primary (HIGH confidence — direct source/codebase inspection this session)
- `swg-client-v2@5fce7bb8` exporter sources: `DataTableTool.cpp`, `ArmorExporterTool.cpp`, `CoreWeaponExporterTool.cpp`, `WeaponExporterTool.cpp` + their `.vcxproj`/`includePaths.rsp`/`libraries.rsp` — dependency-closure verdict.
- `sharedTemplate/.../ServerObjectTemplate.h`, `sharedGame/.../CraftingData.h`, `sharedTemplateDefinition/.../TemplateData.h` — enum/schema vocabulary.
- `tools/.../TreeFileBuilder/src/shared/TreeFileBuilder.cpp` (addResponseFile/addFile/write) + `TreeFileRspBuilder.cpp` — `.rsp` format + TOC/data ordering.
- `Utinni.Cli/Program.cs`, `Commands/RoundtripTabCommand.cs`, `Commands/NativeExportProbe.cs`, `Output/JsonOutput.cs`, `Tests/Infrastructure/GoldenTestRunner.cs` — CLI verb + golden patterns.
- `UtinniCoreDotNet/Formats/Tre/{TreFile,TreArchiveIndex}.cs`, `Formats/ObjectTemplate/{ObjectTemplateParamCodec,MutableObjectTemplate}.cs` — reader API + OT raw-fallback.
- `tools/{DEPENDENCY-MANIFEST.md,PINNED-SHA.md,Directory.Build.props}`, `.github/workflows/ci.yml`, `.planning/{REQUIREMENTS,ROADMAP,STATE}.md`, `13-CONTEXT.md`.
- `D:\SWGEmu-Client\SWGEmu\*.tre` — 53-file corpus existence [VERIFIED: ls → 53].

### Secondary / Tertiary
- None — every claim is from direct inspection; no WebSearch/Context7 needed (this is a closed, in-repo native-revival + managed-wrapping problem with no external-library surface).

## Metadata

**Confidence breakdown:**
- AUTH-06 dependency-closure verdict (centerpiece): **HIGH** — verified against actual source, vcxprojs, and file existence; the "server-taint" disproven three independent ways.
- `.rsp` synthesis + byte-exact (D-06): **HIGH** on the mechanism (TOC CRC-sorted, data-order = rsp-order, reader exposes both); **MEDIUM** on full compressed byte-identity (zlib-param risk — A4).
- AUTH-02 schema shape: **HIGH** on the vocabulary (ParamType/ListType enums); **MEDIUM** on generation path (no `.tdf` assets — Open Q1/A3).
- CLI wrapping + SAVE: **HIGH** — patterns are established; the subprocess seam is new but standard.
- RESID-01 mapping: **HIGH** — codec fallback + ListType correspondence verified in both source trees.

**Research date:** 2026-06-03
**Valid until:** stable (in-repo native + pinned SHA; no fast-moving external deps) — re-verify only if the `swg-client-v2` pinned SHA is bumped or `tools/` is re-lifted.
