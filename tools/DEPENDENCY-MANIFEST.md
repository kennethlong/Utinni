# Utinni `tools/` — Per-Tool Dependency Manifest (AUTH-01)

Provenance: every project here is lifted **verbatim** from `swg-client-v2 @5fce7bb8`
(see [`PINNED-SHA.md`](PINNED-SHA.md)). Toolset: **v145 / Win32 only** (CON-P-02).
Solution: [`Utinni.Tools.sln`](Utinni.Tools.sln) — 35 projects (6 apps + 29-lib closure),
`Debug|Win32` + `Release|Win32`. Standalone build glue lives in
[`Directory.Build.props`](Directory.Build.props).

Status: all six tools **build + link green at v145/Win32** — the three Phase-12 build
CLIs (12-01, 12-02) plus the three Phase-13 AUTH-06 natives (13-01, see
[§ AUTH-06 item-build tools](#auth-06-item-build-tools-13-01)).
Byte-exact verification (D-09) is tracked in [§ Byte-exact status](#byte-exact-status-d-09).

---

## Per-tool summary

| Tool | Toolset | Direct ProjectRefs | Perforce | pcre | Output | Build status |
|------|---------|-------------------|----------|------|--------|--------------|
| TreeFileBuilder | v145/Win32 | 12 | — (none) | — | `.tre` archive | ✅ green |
| TemplateCompiler | v145/Win32 | 25 | **keep-link** | 4.1 | object-template `.iff` | ✅ green |
| TemplateDefinitionCompiler | v145/Win32 | 26 | **keep-link** | — | generated C++ `.cpp/.h` | ✅ green |
| DataTableTool | v145/Win32 | 19 | — (none) | — | datatable `.iff` (tab/XML in) | ✅ green (13-01) |
| ArmorExporterTool | v145/Win32 | 13 | **source-stub** | — | armor `.tpf` + chained `.iff` | ✅ green (13-01) |
| CoreWeaponExporterTool | v145/Win32 | 18 | **source-stub** | — | weapon `.tpf` + chained `.iff` | ✅ green (13-01) |

No tool fell back to v143 — every v145 blocker was a fixable source/CRT-compat delta
(a v143 fallback would not have changed the C++20 `char16_t`/`wchar_t` issues).

---

## TreeFileBuilder

- **Link closure (12 ProjectReferences):** `fileInterface` + `sharedCompression`,
  `sharedDebug`, `sharedFile`, `sharedFoundation`, `sharedFoundationTypes`,
  `sharedIoWin`, `sharedMath`, `sharedMemoryManager`, `sharedRandom`,
  `sharedSynchronization`, `sharedThread`.
- **Externals:** zlib **1.1.4** (linked `zlib.lib` — see [§ zlib](#zlib-pin-d-08--t-12-02)); zlib-1.2.3 headers (compile of `sharedCompression`).
- **Perforce:** none.
- **Revival delta:** `borrowCompressor`/`returnCompressor` (removed from `sharedFile::SearchTree` on this branch) ported to the concrete `ZlibCompressor`; `/SAFESEH:NO` (zlib.lib predates Safe-SEH); `sharedLog` crash-handler tail-log flush decoupled (`UTINNI_TOOLS_NO_SHAREDLOG`) to avoid the logging→network→game-registry cascade.
- **CLI:** `TreeFileBuilder_d.exe -r <build.rsp> <out.tre>`

## TemplateCompiler

- **Direct ProjectReferences (25):** `archive`, `fileInterface`, `localizationArchive`, `localization`, `unicodeArchive`, `unicode`, `sharedCompression`, `sharedDebug`, `sharedFile`, `sharedFoundation`, `sharedFoundationTypes`, `sharedIoWin`, `sharedLog`, `sharedMath`, `sharedMemoryManager`, `sharedMessageDispatch`, `sharedNetworkMessages`, `sharedRandom`, `sharedRegex`, `sharedSynchronization`, `sharedTemplate`, `sharedTemplateDefinition`, `sharedTerrain`, `sharedThread`, `sharedUtility`.
- **Compile-closure note:** `archive`'s forced PCH (`FirstArchive.h` → `ArchiveUserRegistry.h`) registers the whole game archive surface, pulling header-only deps `sharedGame`, `sharedObject`, `sharedSkillSystem`, `swgSharedNetworkMessages`, `sharedCollision`, `sharedPathfinding`, `sharedImage`, `sharedFractal`, `sharedXml`, `sharedNetwork`, `swgSharedUtility`, `singleton`, `boost`, `libxml` — lifted for headers, not linked as projects.
- **Externals:** pcre **4.1** (`libpcre.a`, `PCRE_STATIC`); Perforce ClientAPI (`libclient`/`librpc`/`libsupp` + `ws2_32`); zlib 1.1.4.
- **Perforce decision: KEEP-LINK.** The vendored P4 libs link at v145 (only `/SAFESEH` + 2 legacy CRT symbols tripped). Byte-exact `-compile` path is **P4-free at runtime**; P4 is reached only via `-edit`/`-submit`, which Utinni never invokes (threat T-12-04). CRT-compat for the 2002-era `libsupp.lib` under UCRT: `legacy_stdio_definitions.lib` (`_fscanf`) + `UtinniP4CrtCompat.cpp` (`__tzname` shim).
- **CLI:** `TemplateCompiler_d.exe -compile <input.tpf>` → object-template `.iff`

## TemplateDefinitionCompiler

- **Direct ProjectReferences (26):** TemplateCompiler's set + `sharedMathArchive`, `sharedObject`, `sharedSwitcher` (minus `sharedTemplate`/`sharedTerrain` which it does not reference).
- **Externals:** Perforce ClientAPI (keep-link, as above); zlib 1.1.4. (No pcre direct dep beyond the shared closure.)
- **Perforce decision: KEEP-LINK** (same rationale as TemplateCompiler).
- **Revival delta (shared, applies to both template tools):** `sharedTemplateDefinition` C++20 ports — `Filename.cpp`/`TpfFile.cpp` pass `char16_t` `Unicode::String` to Win32 `*W` APIs (`reinterpret_cast<LPCWSTR>`) and build `Unicode::String` from narrow literals (`narrowToWide`, not `L"..."`); `TemplateData.cpp` string-literal → `const char*`. `/SAFESEH:NO` on both EXEs.
- **CLI:** `TemplateDefinitionCompiler_d.exe -compile <input.tdf>` → generated C++ `.cpp/.h` (inspect the header banner for embedded `__DATE__`/time/absolute-path before byte-exact comparison — Pitfall 6).

---

## AUTH-06 item-build tools (13-01)

Lifted in Phase 13 from the same pinned SHA `@5fce7bb8` (a mini-Phase-12 lift: dependency
closure + v145 build + the SAFESEH/CRT-compat delta pattern). These are the **BUILD-from-source**
natives the `compile-datatable` / `export-armor` / `export-weapon` verbs (Plan 13-05) wrap.

### sharedXml (new leaf lib — the lone genuinely-new build artifact)

- **ProjectReferences:** 0 (leaf lib). Backed by the prebuilt `libxml2-2.6.7.win32` static
  lib (`lib/libxml2-win32-{debug,release}.lib`) — header-compiled against `libxml2-2.6.7.win32/include`.
- **Externals:** `libxml2` **2.6.7** prebuilt (vendored; CVEs accepted offline/trusted-input,
  re-evaluated in Phase 14). Linked into DataTableTool, not sharedXml (static lib).
- **Build status:** ✅ green at v145/Win32 (static lib, `stdcpp20`). LNK4217 (`_xmlFree` import) is benign.

### DataTableTool

- **Direct ProjectReferences (19):** `archive`, `fileInterface`, `unicodeArchive`,
  `sharedCompression`, `sharedDebug`, `sharedFile`, `sharedFoundationTypes`, `sharedFoundation`,
  `sharedIoWin`, `sharedLog`, `sharedMath`, `sharedMemoryManager`, `sharedMessageDispatch`,
  `sharedNetworkMessages`, `sharedRandom`, `sharedSynchronization`, `sharedThread`,
  `sharedUtility`, **`sharedXml`** (the 19th, the new leaf lib).
- **Perforce:** none. **Runtime chain:** none — pure spreadsheet(tab)/XML → `.iff` compiler
  (`DataTableWriter::isXmlFile` switch decides tab-delimited vs XML; NOT comma-CSV).
- **Revival delta:** `/SAFESEH:NO` (zlib 1.1.4 predates Safe-SEH) — already present on the lifted vcxproj.
- **CLI:** `DataTableTool_d.exe <input.tab|.xml> <output.iff>`

### ArmorExporterTool / CoreWeaponExporterTool (item exporters)

- **Direct ProjectReferences:** 13 (Armor) / 18 (CoreWeapon) — all already in the sln; **no
  `serverGame` / `sharedTemplate` / `sharedGame` ProjectReference** (the cross-tree usage is
  compile-time enum constants only: `ArmorCategory_Last`, `ArmorLevel_Last`, `XP_crafting`,
  `XP_craftingClothingArmor`, `CT_weapon`, `CT_lightsaber`).
- **Perforce decision: SOURCE-STUB (not keep-link).** Unlike the template tools (which keep-link
  the P4 ClientAPI but never invoke it), the exporters call `popen("p4 fstat/edit/add")` +
  `FATAL(p4==NULL, "Cannot access Perforce")` directly on their `run()` path. The
  **Perforce-stub** revival delta guards `getFileFromPerforce`/`addFileToPerforce` with
  `UTINNI_TOOLS_NO_PERFORCE` (a global `Directory.Build.props` define) → no-op returning `true`,
  so the FATAL is unreachable headless. p4 is build-farm VCS bookkeeping (checkout-before-write /
  add-after-write), NOT data — the `.tpf`/`.iff` outputs are written by `fopen` independent of p4.
  Mirrors the `UTINNI_TOOLS_NO_SHAREDLOG` decouple precedent.
- **serverGame dead-alias redirect:** both exporters `#include "serverGame/ServerObjectTemplate.h"`,
  but the client-only corpus has no server tree (the original `src/engine/server/library/serverGame`
  include root is absent). A redirect shim at `tools/src/_compat/serverGame/ServerObjectTemplate.h`
  points the dead alias at the Phase-12-lifted `sharedTemplate/ServerObjectTemplate.h` (where the
  enums physically live). `tools/src/_compat` is on the global `Directory.Build.props` include path.
- **Revival delta:** `/SAFESEH:NO` (`ImageHasSafeExceptionHandlers=false`) on both EXEs (linked
  zlib 1.1.4 predates Safe-SEH → LNK2026 under v145).
- **Runtime chain (handled by Plan 13-05's wrapper, NOT this lift):** each exporter reads a
  datatable `.iff`, emits a `.tpf`, then `system("TemplateCompiler -compile <tpf>")` (bare name,
  no path/suffix) → the verb wrapper stages `TemplateCompiler.exe` + a `tools.cfg` fixture beside
  the exporter exe and sets `WorkingDirectory` so the chain + config-load resolve.
- **CLI:** `ArmorExporterTool_d.exe -i <datatable.iff>` / `CoreWeaponExporterTool_d.exe -i <datatable.iff>`

### D-02 escape-hatch disposition

No tool fell back. All 3 are clean client lifts (the "server-taint" was an include-path alias, not
a server-side closure). The escape-hatch (managed `DataTableWriter` fallback / defer an exporter)
stayed unused. The real (small) work was the Perforce-stub + serverGame redirect + SAFESEH delta —
build config + a 1-include redirect, not multi-day porting.

---

## Dead include paths (pruned — D-04)

| Project | Dead path | Disposition |
|---------|-----------|-------------|
| `sharedTemplate.vcxproj` | `..\..\..\..\..\..\external\3rd\library\perforce\include` (all 3 configs) | **PRUNED** — no `sharedTemplate` source `#include`s Perforce/`clientapi.h`; it was a present-but-never-used include dir. The template *apps'* Perforce usage is LIVE and kept (see keep-link above) — only this one shared-lib include dir was dead. |

---

## zlib pin (D-08) + T-12-02 tension

- **Linked:** `external/3rd/library/zlib/lib/win32/zlib.lib` = **zlib 1.1.4** (2002 prebuilt). This is the **byte-exact `.tre`-output determinant** (Pitfall 3) — do **not** upgrade it silently; doing so changes compressed output and breaks the `.tre` byte-exact smoke.
- **Compiled-against:** `external/3rd/library/zlib-1.2.3/zlib.h` provides the header `sharedCompression/ZlibCompressor.cpp` includes (declarations only; the linked 1.1.4 lib determines runtime behavior).
- **Tension (T-12-02):** zlib 1.1.4 carries known CVEs. Accepted for this phase — the tools run **offline on trusted maintainer inputs**; no agent/network surface is added here. Re-evaluate when inputs become agent-influenceable (Phase 14 MCP threat register).

## pcre + Perforce pins

| Lib | Version | Form | Notes |
|-----|---------|------|-------|
| pcre | **4.1** | `libpcre.a` (GNU archive, `PCRE_STATIC`) | both template tools; vendored, offline/trusted-input (T-12-05) |
| Perforce ClientAPI | vendored (2002-era) | `libclient.lib`/`librpc.lib`/`libsupp.lib` + `ws2_32` | keep-link; reached only via unused `-edit`/`-submit` (T-12-04); CRT-compat shims required under UCRT |

---

## Byte-exact status (D-09)

**A1 reference-pair gate resolved (12-03 Task 1): per-tool GATE-FINDINGS recorded.**
No byte-exact *source → known-good* pair is currently available for any of the three
tools. Per D-09 this is an explicit, surfaced gate-finding (NOT a free pass and NOT a
structural/round-trip fallback). A reusable byte-exact harness — `tools/smoke/byte-exact-smoke.ps1`
(SHA256 `Get-FileHash`, dump-on-mismatch, no fallback) — is in place and activates the
moment a compatible pair is supplied. The AUTH-01 **build** hard gate (all three tools
green at v145 + CI enforcement) is independently satisfied; this section is the
**verification** half's honest status.

| Tool | Reference availability | Disposition |
|------|------------------------|-------------|
| TreeFileBuilder | ❌ no compatible pair | **GATE-FINDING.** The maintainer's retail `.tre` corpus (`D:\Sample-TRE-Files`, 46 files) is **Restoration v6000** (`EERT6000`, encrypted payloads) — a *newer* format than this 2002-era tool emits (`0005`/`0006`) and not source-extractable; it cannot serve as a reference. The only format-matching asset, `swg-client-v2`'s `retail_mini_0005.tre` (`EERT0005`), has **no source tree and no `.rsp`**, and the original `.rsp` (file order + per-file compression flags) is not recoverable from a `.tre` alone, so a byte-exact rebuild cannot be constructed. **Resolution path:** supply a `0005`/`0006`-era source file-set + its `.rsp` (or derive one via the sibling `TreeFileRspBuilder`), then run `byte-exact-smoke.ps1`. |
| TemplateCompiler | ❌ no pair | **GATE-FINDING.** Zero `.tpf` source assets in either repo. **Resolution path:** supply a `.tpf` + its SOE-produced known-good object-template `.iff`, then run the harness (binary `Get-FileHash`). |
| TemplateDefinitionCompiler | ❌ no pair | **GATE-FINDING.** Zero `.tdf` source assets in either repo. **Resolution path:** supply a `.tdf` + its SOE-generated `.cpp/.h`; confirm the narrowest banner-normalization regex (Pitfall 6 — embedded `__DATE__`/time/abs-path) and pass it to the harness via `-NormalizeBanner`. |

These findings feed Phase 13 (revive+wrap): when the tools become `utinni-cli` verbs,
real assets flow through the existing golden-fixture harness, the natural place to
retire each gate-finding with a live byte-exact pass.

## CI enforcement (D-07)

`.github/workflows/ci.yml` builds `tools\Utinni.Tools.sln` (`Debug|Win32`) on the
self-hosted `[self-hosted, windows, x64, utinni-v145]` runner as the
**"Build tools solution (Debug|Win32) — AUTH-01 hard gate"** step — kept separate from
the `Utinni.sln` lanes (D-07). A non-zero MSBuild exit fails the job. Per-tool byte-exact
smoke steps are added once the A1 reference pairs land.

The lane builds the **whole** solution (`tools\Utinni.Tools.sln`), so the hard gate
**auto-extended** to the 3 new AUTH-06 exes + `sharedXml` in 13-01 with **no ci.yml edit**
(confirmed: ci.yml:176 targets the sln, not specific projects).
