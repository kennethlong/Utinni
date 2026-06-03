# Utinni `tools/` — Per-Tool Dependency Manifest (AUTH-01)

Provenance: every project here is lifted **verbatim** from `swg-client-v2 @5fce7bb8`
(see [`PINNED-SHA.md`](PINNED-SHA.md)). Toolset: **v145 / Win32 only** (CON-P-02).
Solution: [`Utinni.Tools.sln`](Utinni.Tools.sln) — 31 projects (3 apps + 28-lib closure),
`Debug|Win32` + `Release|Win32`. Standalone build glue lives in
[`Directory.Build.props`](Directory.Build.props).

Status: all three tools **build + link green at v145/Win32** (12-01, 12-02).
Byte-exact verification (D-09) is tracked in [§ Byte-exact status](#byte-exact-status-d-09).

---

## Per-tool summary

| Tool | Toolset | Direct ProjectRefs | Perforce | pcre | Output | Build status |
|------|---------|-------------------|----------|------|--------|--------------|
| TreeFileBuilder | v145/Win32 | 12 | — (none) | — | `.tre` archive | ✅ green |
| TemplateCompiler | v145/Win32 | 25 | **keep-link** | 4.1 | object-template `.iff` | ✅ green |
| TemplateDefinitionCompiler | v145/Win32 | 26 | **keep-link** | — | generated C++ `.cpp/.h` | ✅ green |

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
