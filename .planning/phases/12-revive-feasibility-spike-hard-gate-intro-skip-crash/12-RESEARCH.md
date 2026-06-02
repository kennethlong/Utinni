# Phase 12: Revive-feasibility spike (HARD GATE) + intro-skip crash - Research

**Researched:** 2026-06-02
**Domain:** Legacy-C++ build-tool revival (lift-and-shift @ v145) + byte-exact transform determinism + live-injection crash root-cause (VEH)
**Confidence:** HIGH for build mechanics / dependency closure (direct on-disk inspection); MEDIUM-LOW for byte-exact reference-pair availability (a gate finding — flagged loudly below)

> **Methodology note.** Every claim about `swg-client-v2` below was verified by reading the actual
> files on the **current checkout** (`koogie-msvc-cpp20-base` @ SHA `5fce7bb8368c86d5a2330a0173d1541866786196`,
> read 2026-06-02). Where this research **contradicts** the project-level research
> (`.planning/research/SUMMARY.md` + `PITFALLS.md`, dated 2026-06-01), the contradiction is called
> out explicitly — the project research was correct in spirit but several specifics have drifted or
> were wrong, and those drifts change the plan.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions (inherited locks — do NOT re-litigate)
- **D-01:** Lift-and-shift LOCKED — copy source + required shared libs into the repo-local `tools/` tree; **never** `#include` or `ProjectReference` across into the live `swg-client-v2` tree (it is mid-D3D9→D3D11 migration on `koogie-msvc-cpp20-base`).
- **D-02:** v145 is the shared target toolset (matches Utinni; `swg-client-v2` is already v145/`stdcpp20`, STLport453 gone). v143 is the **documented per-tool fallback** for any tool that refuses v145 — the subprocess seam is toolset-agnostic; the constraint forbids building *in* `swg-client-v2`, not building at a given toolset in our own tree.
- **D-03:** x86 / `Win32` only (CON-P-02). Record the **exact lifted-from x86 SHA** (a SHA, not a branch HEAD); watch the upstream `x64bit-Upgrade` branch as a divergence risk.
- **D-04:** Prune dead `perforce`/`alienbrain` include paths; keep only the real leaf externals — `zlib` (TreeFileBuilder) and `pcre/4.1` (TemplateCompiler). No renderer/D3D/D3DX (these tools are headless). **⚠️ See Pitfall 1 — this decision is partly based on a wrong premise: Perforce is NOT dead for the two template tools.**
- **D-05:** All **three** tools in scope incl. `TemplateDefinitionCompiler`. Do not assume uniform status; treat "is v145 in the vcxproj" and "actually builds + links" as different facts, resolved by a build pass not by reading.
- **D-06:** VEH crash-logger **already deployed** (Utinni `d1096ac`); this phase is the capture+diagnose+fix run, not re-instrumentation.

### Tools tree & build seam
- **D-07:** `tools/Utinni.Tools.sln` is a **separate, Utinni-owned solution** (not folded into `Utinni.sln`); its build is **wired into the self-hosted v145 CI runner THIS phase**.
- **D-08:** Per-tool **dependency manifest** + pinned `swg-client-v2` SHA are in-repo markdown deliverables under `tools/` (filename/format = Claude's discretion).

### Per-tool "pass" bar (headless smoke)
- **D-09:** Each tool runs headless against a **real shipped asset** and produces **byte-exact** output vs a known-good reference. **Byte-exact is mandatory — NO structural/round-trip fallback.** Cross-toolset non-determinism is a **gate finding to surface and resolve**, not a free pass. RESEARCH must validate per tool whether matching source→known-good pairs exist and whether the transform is deterministic enough. **This is the single biggest feasibility unknown — flag loudly if a tool's output cannot be made byte-exact.**

### v145 fallback & failure policy
- **D-10:** **Stop-and-ask before each v143 fallback.** Per-tool, maintainer-in-the-loop, not automatic.

### RESID-02 intro-skip crash
- **D-11:** **Full root-cause fix — no masking guard/detour shortcut.** Reproduce via the TJT-driven scene-change path; capture faulting module+RVA from the deployed VEH logger; fix the underlying defect. If the fault resolves into `SWG.exe` game code Utinni does not own → deliverable is a **documented root-cause analysis** (module + RVA + mechanism). VEH logger stays deployed. Landing naked after scene change is the **expected baseline, NOT a crash signal**.

### Claude's Discretion
- `tools/Utinni.Tools.sln` internal project layout; manifest/SHA file naming + format.
- Self-hosted-CI step wiring (verify-only build step shape).
- Which specific real asset(s) per tool's byte-exact smoke (subject to availability surfaced by research).
- Order of the build pass across the three tools.

### Deferred Ideas (OUT OF SCOPE — Phase 13+)
- Wrapping tools as `utinni-cli` verbs, the SAVE verb, OT Tier-2 typed display, datatable compile + item exporters, all MCP work.
- Per-dep vcpkg migration commits.
- RESID-03/RESID-04, prior-phase residuals, CI-stability flakes.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| **AUTH-01** (HARD GATE) | `TreeFileBuilder` + `TemplateCompiler` + `TemplateDefinitionCompiler` build + link STANDALONE at v145 (lift-shifted into Utinni-owned `tools/`, dead deps stripped), producing a per-tool dependency manifest + pinned `swg-client-v2` SHA. | Full dependency closure mapped per tool (§Standard Stack, §Dependency Closure). Build mechanics + v145 conformance deltas (§Architecture). Byte-exact determinism assessed per tool (§Byte-Exact Feasibility — the gate). Pinned SHA captured: `5fce7bb8` (§Pinned Source). |
| **RESID-02** | Intro-skip scene-transition crash diagnosed (VEH faulting module+RVA) and root-cause-fixed (or documented if game-side). | VEH logger anatomy + output format + log sink located (§RESID-02). Repro path + naked baseline documented. Detour single-sourcing pattern for a Utinni-side fix (§RESID-02). |
</phase_requirements>

## Summary

This phase has two unrelated deliverables. The **AUTH-01 hard gate** is the high-rigor one and the
research below front-loads it.

**The good news (build mechanics):** All three tools are genuinely `PlatformToolset=v145` +
`LanguageStandard=stdcpp20` + `Win32` in their `.vcxproj`. The required externals (`zlib`,
`pcre/4.1`, Perforce libs) and all ~12–28 shared-library `ProjectReference` targets **exist on disk**
under `src/external/` and `src/engine/shared/library/`. The v145 toolset (14.51 / 14.52) and VS2026
MSBuild are installed locally. The dependency *graph* is intact and liftable.

**The corrections that change the plan (every one verified on-disk, contradicting the 2026-06-01 project research):**

1. **No build output exists for ANY of the three tools.** The project research claimed
   "`TemplateCompiler` has built v145 Debug objects on disk (likely-green)." There is **no `compile/`
   directory at all** on the current checkout (`compile/win32` is gitignored and absent). All three
   tools are **equally unverified** empirically. The "TemplateCompiler = likely-green, bank an early
   win" sequencing assumption is **no longer supported by evidence** — do the build pass.
2. **Perforce is NOT a dead dependency for the two template tools.** Both `TemplateCompiler.cpp` and
   `TemplateDefinitionCompiler.cpp` actively `#include "clientapi.h"` and subclass `ClientUser` /
   `StrBuf`, and link `libclient.lib`/`librpc.lib`/`libsupp.lib`. The "dead perforce path" is real
   **only** for `sharedTemplate.vcxproj`'s *include directory* — the apps' Perforce usage is live
   code. **D-04 ("prune dead perforce") is built on a partly-wrong premise.** The mitigation is
   simple and confirmed by the census: the byte-exact-producing verb (`-compile`) **never touches
   Perforce** (P4 is only behind `-edit`/`-submit`), so the link can either keep the real P4 libs
   (present on disk) or stub the `checkOut`/`checkIn` functions. Either way this is a **per-tool
   decision the plan must make explicitly**, not an automatic prune.
3. **The byte-exact reference assets the maintainer assumed live inside `swg-client-v2` mostly do NOT
   exist there.** There are **zero `.tpf`, `.tdf`, `.tpd` source files** in the entire tree, and
   exactly **one `.tre`** (`tools/swg_blender/tests/golden/retail_mini_0005.tre`, a blender golden).
   Byte-exact references must be sourced from a **real SWG client install** (the live `.tre` mount
   set Utinni already reads) — and even then, byte-exactness is **not guaranteed** because the tools
   embed a 24-year-old zlib 1.1.4 whose exact deflate output is the determinant (see the gate).

**Primary recommendation:** Run the AUTH-01 build pass against a **lifted, P4-link-preserved (or
P4-stubbed) `tools/` tree pinned at SHA `5fce7bb8`**, building all three tools fresh (none have prior
output). Treat byte-exact determinism as the **gate's true risk**, not the build — specifically the
zlib-1.1.4-dependent `.tre` compression and the embedded `time(NULL)`/build-stamp behavior. For
RESID-02, run the live repro through the TJT scene-change path with the already-deployed VEH logger,
read the `module+rva` line out of Utinni's spdlog sink, and root-cause per D-11's two-branch
disposition.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `.tpf` → object-template `.iff` compile | Headless CLI (`tools/`) | — | Deterministic source→binary transform; `TpfFile::makeIffFiles`, no renderer, no P4 at runtime |
| `.tdf` → generated C++ template classes | Headless CLI (`tools/`) | — | Code-generation, not asset; output is `.cpp`/`.h`, P4-free under `-compile` |
| source tree (`.rsp`) → `.tre` archive | Headless CLI (`tools/`) | — | Deterministic pack; only zlib + sharedFile; **zlib determinism is the byte-exact risk** |
| v145 standalone build + link | Build (MSBuild + self-hosted CI) | — | D-07 wires `Utinni.Tools.sln` into the v145 runner this phase |
| Byte-exact verification harness | Test (golden-compare) | Build (CI gate) | Conceptually the DEC-C3 Tier-2 pattern, but against **real** assets (D-09), not synth |
| Intro-skip crash capture | Injected x86 client (VEH in `UtinniCore.dll`) | — | Already deployed (`d1096ac`); logs module+RVA to spdlog |
| Intro-skip root-cause fix | Injection/detour surface (Utinni) **OR** documented analysis (if `SWG.exe`-side) | — | D-11 two-branch disposition |

## Standard Stack

This phase installs **no new packages**. It lifts existing MSVC C++ source and links pre-existing
static libs. The "stack" is the toolchain + the exact lifted dependency set.

### Core (build toolchain — verified present locally)
| Component | Version | Purpose | Status |
|-----------|---------|---------|--------|
| MSVC PlatformToolset | **v145** (14.51.36231 / 14.52.36328) | Compile/link the lifted tools | `[VERIFIED: on-disk]` at `D:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\` |
| MSBuild | VS2026 Dev18 Current | Build `Utinni.Tools.sln` | `[VERIFIED: on-disk]` `…\MSBuild\Current\Bin\MSBuild.exe` |
| LanguageStandard | `stdcpp20` (`/std:c++20`) | Matches upstream; inherit their conformance fixes | `[VERIFIED]` all 3 vcxproj |
| Platform | `Win32` (x86) | CON-P-02; matches injected SWG.exe | `[VERIFIED]` all 3 vcxproj |

### Leaf externals (3rd-party static libs — verified present on-disk under `src/external/3rd/library/`)
| Lib | Version | Needed by | On-disk | Notes |
|-----|---------|-----------|---------|-------|
| `zlib.lib` | **1.1.4** (`ZLIB_VERSION "1.1.4"`) | **All three** (compression) | ✓ `…/zlib/lib/win32/zlib.lib` | **Byte-exact determinant for `.tre`** — see gate. Ancient; deflate output is version-specific. |
| `libpcre.a` | 4.1 | TemplateCompiler, TemplateDefinitionCompiler | ✓ `…/pcre/4.1/win32/lib/libpcre.a` | `.a` (GNU archive) format; `PCRE_STATIC` defined; "old static lib may be compiler-sensitive" per census |
| `libclient.lib`, `librpc.lib`, `libsupp.lib` | Perforce ClientAPI | TemplateCompiler, TemplateDefinitionCompiler | ✓ `…/perforce/lib/win32/` | **LIVE dependency** (not dead — see Pitfall 1). Only used at runtime by `-edit`/`-submit`. |
| `ws2_32.lib` | Windows SDK | TemplateCompiler, TemplateDefinitionCompiler | system | Winsock (pulled by Perforce ClientAPI) |

### Supporting (shared-library ProjectReferences — all verified present under `src/engine/shared/library/`)
The three tools collapse to lifted source + these shared libs. Counts are exact from the vcxprojs:

| Tool | ProjectReference count | The set |
|------|------------------------|---------|
| **TreeFileBuilder** | **12** (smallest) | fileInterface, sharedCompression, sharedDebug, sharedFile, sharedFoundationTypes, sharedFoundation, sharedIoWin, sharedMath, sharedMemoryManager, sharedRandom, sharedSynchronization, sharedThread |
| **TemplateCompiler** | **26** | + archive, localizationArchive, localization, unicodeArchive, unicode, sharedLog, sharedMessageDispatch, sharedNetworkMessages, sharedRegex, sharedTemplateDefinition, sharedTemplate, sharedTerrain, sharedUtility (and the 12 above minus fileInterface overlap) |
| **TemplateDefinitionCompiler** | **28** (largest) | TemplateCompiler's set + sharedMathArchive, sharedObject, sharedSwitcher |

> **Sequencing correction:** the prior research said "TemplateCompiler likely-green → verify first to
> bank an early win." But (a) it has **no** build output now, and (b) it carries the **2nd-largest**
> dependency set + live Perforce. **`TreeFileBuilder` is the genuinely cheapest to land**: 12 refs,
> only `zlib` external, **no Perforce, no pcre**. Recommend front-loading `TreeFileBuilder` for the
> *first green* (it is both the prior "prime unknown" AND the smallest closure), then TemplateCompiler,
> then TemplateDefinitionCompiler (largest + generates C++, not an `.iff`).

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Keep Perforce link (libs present) | Stub `checkOut`/`checkIn` to `return -1` | Stub drops 3 P4 libs + ws2_32 from the link and removes a 24-yr-old binary-compat risk; cost = `-edit`/`-submit` verbs become no-ops (irrelevant — Utinni never submits to P4). **Recommend stub** unless the link "just works." Per-tool maintainer call (aligns with D-10 spirit). |
| v145 | v143 per-tool fallback (D-02/D-10) | Only if a tool refuses v145; stop-and-ask first. The subprocess seam is toolset-agnostic. |
| ProjectReference the 12–28 shared libs from the lifted tree | Pre-build them once into `.lib`s and link | ProjectReference is simpler and matches upstream; physical-copy keeps the lift self-contained per D-01. Recommend ProjectReference **within the Utinni-owned copied tree** (never across into live `swg-client-v2`). |

**Installation:** No `npm`/`pip`/`cargo` installs. The "install" is a source+lib copy from
`swg-client-v2` @ `5fce7bb8` into `tools/`, then `MSBuild Utinni.Tools.sln /p:Configuration=Debug;Platform=Win32`.

## Package Legitimacy Audit

**Not applicable** — this phase installs **zero** packages from npm/PyPI/crates. All code is
lift-and-shifted MSVC C++ from a pinned local sibling checkout (`swg-client-v2` @ `5fce7bb8`), and all
linked libraries are pre-existing static `.lib`/`.a` files vendored in that checkout under
`src/external/3rd/library/`. There is no registry surface and therefore no slopcheck/registry
verification to run. The supply-chain control that *does* apply is **pinning the exact source SHA**
(D-03/D-08) so an upstream force-push cannot move the lifted source under us.

## Dependency Closure & Lift Mechanics (the AUTH-01 core)

### Pinned source
- **Repo:** `D:/Code/swg-client-v2`
- **Branch:** `koogie-msvc-cpp20-base`
- **SHA to pin (D-03/D-08):** `5fce7bb8368c86d5a2330a0173d1541866786196` `[VERIFIED: git rev-parse, 2026-06-02]`
- **Divergence watch:** live branches `x64bit-Upgrade` (remote `koogie/`) and `MSVC-CPP20-Upgrade`. An upstream x64 migration collides with CON-P-02 — pin the x86 SHA, re-sync deliberately.

### What's on disk vs what the vcxproj paths say
The vcxproj relative paths (`..\..\..\..\..\..\external\…` from `<Tool>/build/win32/`) resolve to
`src/external/…`, **not** repo-root `external/` (which does not exist). Path arithmetic verified: 6×
`..` from `build/win32` lands on `src/`. **All referenced externals and shared-lib projects exist.**
`[VERIFIED: on-disk ls, 2026-06-02]`

### Per-tool source files (tiny — the apps themselves are small)
| Tool | Source files | LOC |
|------|--------------|-----|
| TreeFileBuilder | `FirstTreeFileBuilder.{cpp,h}`, `TreeFileBuilder.{cpp,h}`, `TreeFileBuilder.dox` | 835 |
| TemplateCompiler | `TemplateCompiler.cpp`, `FirstTemplateCompiler.{cpp,h}` | 634 |
| TemplateDefinitionCompiler | `TemplateDefinitionCompiler.cpp`, `FirstTemplateDefinitionCompiler.{cpp,h}` | 778 |

The weight is in the shared libraries, not the app source.

### The genuine v145 conformance deltas to expect (narrow, well-understood)
These tools were authored 2001–2002. Under `/std:c++20` + v145 `/permissive`-ish defaults, expect at
most: `std::auto_ptr` removal (re-enable transitionally via `_HAS_AUTO_PTR_ETC`), two-phase name
lookup tightening (escape hatch `/Zc:twoPhase-`), stricter `enum`/`template`-keyword rules. `[CITED:
learn.microsoft.com/.../overview-of-potential-upgrade-issues-visual-cpp]` `[CITED:
devblogs.microsoft.com/cppblog/c-language-updates-in-msvc-build-tools-v14-50]`. **Critically:** the
upstream `koogie-msvc-cpp20-base` branch already carried the shared libraries to v145/C++20 for the
*renderer* build, so the shared-lib headers these tools pull are already conformance-fixed. The
CppSharp v145 block (vendored clang-11 parsing MSVC STL) is **unrelated** and not a predictor — do not
budget "STL modernization" weeks without first attempting a build.

## Architecture Patterns

### System Architecture Diagram (AUTH-01 build + verify flow)

```
swg-client-v2 @ 5fce7bb8 (READ-ONLY sibling)
  src/engine/shared/application/{TreeFileBuilder,TemplateCompiler,TemplateDefinitionCompiler}/
  src/engine/shared/library/shared*/        (12–28 ProjectReference targets)
  src/external/{3rd/library/{zlib,pcre,perforce}, ours/library/{archive,unicode,...}}
                          │
                          │  LIFT (copy, never #include across — D-01)
                          ▼
Utinni/tools/                              ← created from scratch this phase
  Utinni.Tools.sln                         (D-07: standalone, Utinni-owned)
    ├─ TreeFileBuilder.vcxproj      (+ its 12 shared-lib vcxproj, copied)
    ├─ TemplateCompiler.vcxproj     (+ P4 link OR stub decision — Pitfall 1)
    ├─ TemplateDefinitionCompiler.vcxproj
    └─ external/ (zlib, pcre, perforce libs copied)
  DEPENDENCY-MANIFEST.md                    (D-08: per-tool #include closure + pruned/kept deps)
  PINNED-SHA.md                             (D-08: 5fce7bb8 + branch + x64 watch)
                          │
                          │  MSBuild /p:Platform=Win32 (v145, or v143 fallback per D-10)
                          ▼
  TreeFileBuilder_d.exe   TemplateCompiler_d.exe   TemplateDefinitionCompiler_d.exe
                          │
                          │  HEADLESS SMOKE (D-09) — byte-exact vs known-good reference
                          ▼
  Real SWG asset  ──►  tool  ──►  output  ══(byte-compare)══  known-good reference
   (.rsp/source)                  (.tre/.iff/.cpp)              (from real client install)
                          │
                          ▼
  Self-hosted v145 CI runner (D-07): build step + (verify-only) green gate
```

### Pattern 1: Lift-and-shift with ProjectReferences preserved inside the copied tree
**What:** Copy the app + its transitive shared-lib `.vcxproj`s + leaf externals into `tools/`,
preserving the relative `ProjectReference` graph **within** the copy. The vcxproj relative paths
already work if the directory shape (`application/<Tool>/build/win32`, `library/shared*/build/win32`,
`external/…`) is preserved.
**When to use:** Always (D-01). Never convert to cross-repo references.
**Watch:** the GUIDs in the lifted vcxprojs must stay internally consistent within `Utinni.Tools.sln`;
re-home them under one solution.

### Pattern 2: Decouple the byte-exact verb from Perforce at lift time
**What:** Both template tools' `-compile` verb (`compileTemplate`→`TpfFile::makeIffFiles`;
`parseTemplateDefinitionFile`) is **P4-free at runtime**. P4 is reached only via `-edit`/`-submit`.
**When to use:** For TemplateCompiler + TemplateDefinitionCompiler. Decide per-tool: keep the P4 link
(libs present) or stub `checkOut`/`checkIn`. Stubbing removes `libclient/librpc/libsupp/ws2_32` and a
binary-compat risk; it costs nothing Utinni uses.
```cpp
// Lift-time stub option (removes Perforce link entirely):
int checkOut(const char *) { fprintf(stderr, "checkOut: Perforce disabled in Utinni lift\n"); return -1; }
int checkIn (const char *) { fprintf(stderr, "checkIn:  Perforce disabled in Utinni lift\n"); return -1; }
// and delete the `#include "clientapi.h"` + MyPerforceUser/StrBufFixed classes.
```

### Anti-Patterns to Avoid
- **Reading vcxproj toolset to infer build status.** All three say `v145`; none have built. Build it.
- **Auto-pruning "dead perforce."** Dead only for `sharedTemplate` *include dir*; LIVE for the two app's code. Decide per Pattern 2.
- **`#include`/`ProjectReference` across into live `swg-client-v2`.** Violates D-01; breaks on every upstream D3D11/x64 churn.
- **Assuming byte-exact is free because the writer is "correct."** `.tre` byte-exactness is gated by the **zlib 1.1.4** deflate output and embedded build-stamps, not by writer correctness.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| `.tpf`→`.iff` template compile | A new C# template compiler | The lifted `TemplateCompiler` (`TpfFile::makeIffFiles`) | The whole point of revive+wrap; reimplementing the definition-driven serializer is a large, error-prone port |
| `.tre` build-from-source | A C# TRE writer | The lifted `TreeFileBuilder` | Utinni's `UtinniCoreDotNet` only *repacks*; build-from-source (CRC sort, md5 block, smallest-compressor) is exactly this tool |
| `.tdf`→param/type schema | Hand-parsing `.tdf` | The lifted `TemplateDefinitionCompiler` | Generates the canonical C++ template classes whose param→type map feeds OT Tier-2 (Phase 13) |
| zlib deflate for `.tre` | A modern zlib/zlib-ng | The **vendored zlib 1.1.4** | Byte-exact `.tre` requires the *exact* deflate stream the reference was built with — a newer zlib produces different bytes (gate) |
| Crash faulting-address capture | A new dump handler | The already-deployed VEH logger (`d1096ac`) | D-06: instrumentation is done; this is the capture run |

**Key insight:** This phase is a *feasibility gate*, not a build-from-scratch. Every hand-roll
temptation here is a Phase-13+ deliverable in disguise — keep them out (Deferred).

## Runtime State Inventory

> This is a build-tool revival + a crash diagnosis, not a rename/migration. There is no stored data,
> service config, or OS-registered state being renamed. The one "stateful" surface is the **lifted
> source provenance** and **CI registration**.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — no datastore keys/collections renamed. | None — verified: phase creates `tools/` from scratch, edits no existing data. |
| Live service config | **Self-hosted CI runner** (`C:\actions-runner`) gains a new build lane (D-07). The workflow file (`.github/workflows/ci.yml`-class) is in git; the runner registration is **machine-local, not in git**. | Add the `Utinni.Tools.sln` build step to the workflow (in-git); confirm the existing runner picks it up (no new runner registration needed — same machine, same v145). |
| OS-registered state | None new. (Existing runner service registration is unchanged.) | None — verified. |
| Secrets/env vars | None — the tools take file args, no secrets. P4 connection settings (`templateCompiler.cfg`, `P4PORT` env) are read **only** by the `-edit`/`-submit` paths we are not using. | None (if stubbing P4) / document `templateCompiler.cfg` ignored (if keeping link). |
| Build artifacts / installed packages | The lifted tools produce `*_d.exe` / `*_r.exe` under `compile/win32/<Tool>/<Config>/` (per the OutDir in the vcxprojs). **No prior build artifacts exist** for any tool (no `compile/` dir on the source checkout). The pinned-SHA + manifest markdown become the contract Phase 13/14 consume. | Wire `compile/` (or chosen OutDir) into `.gitignore`; commit only the `.exe`-producing project config + manifests, never the binaries. |

**Nothing found in Stored data / OS-registered state:** confirmed by inspection — this phase adds a
build lane and three executables; it renames/migrates no runtime state.

## Byte-Exact Feasibility (D-09 — THE GATE) ⚠️

This is the loudest finding. **D-09 mandates byte-exact with no fallback, and byte-exactness is at
real risk for at least `TreeFileBuilder`, and the reference assets are largely absent from
`swg-client-v2`.** Per-tool assessment:

### TreeFileBuilder → `.tre`  — **byte-exact is HARD / at-risk**
- **Reference pairs in swg-client-v2:** essentially **none**. Zero `.rsp` build response files for TRE
  (the 593 `.rsp` hits are MSBuild compiler-arg files, not TreeFileBuilder input). Exactly **one**
  `.tre` exists: `tools/swg_blender/tests/golden/retail_mini_0005.tre` (version `0005` — and
  TreeFileBuilder *does* write `TAG_0005`, so version matches). `[VERIFIED: find, 2026-06-02]`
- **Determinism risks (from reading `TreeFileBuilder.cpp`):**
  1. **zlib 1.1.4 deflate output** — `compressAndWrite` tries `CT_zlib` and keeps the smallest. The
     exact compressed bytes are a function of the **linked zlib version**. The vendored zlib is
     **1.1.4** `[VERIFIED]`. If the reference `.tre` was packed with a *different* zlib, byte-exact is
     impossible without matching it. **Linking the vendored 1.1.4 is mandatory for any hope of
     byte-exact** — do NOT substitute a modern zlib.
  2. **TOC record ordering** — `addFile` inserts via `std::lower_bound` on `treeFileEntry` (the path
     string), so ordering is deterministic *given the same response-file content and the same string
     comparison*. Stable. ✓
  3. **md5 block** — deterministic given identical file bytes. ✓
  4. **No timestamp in the header** — header is `{token, version, numberOfFiles, tocOffset, …}`; no
     build-stamp field observed. ✓ (lower risk than the template tools)
- **Verdict:** Byte-exact is **achievable only if** (a) the vendored zlib 1.1.4 is linked, and (b) a
  reference `.tre` is paired with its exact source tree + `.rsp` + compression flags. The `retail_mini`
  golden has no recorded source `.rsp`. **GATE ACTION:** the plan must either (i) locate/produce a
  matched source→`.tre` pair (most likely by re-deriving the `.rsp` from a real client `.tre`'s file
  list), or (ii) surface to the maintainer that no byte-exact reference pair exists for TRE and
  resolve per D-09 ("a gate finding to surface and resolve, not a free pass").

### TemplateCompiler → object-template `.iff`  — **byte-exact PLAUSIBLE, references absent**
- **Reference pairs in swg-client-v2:** **zero `.tpf`** sources on disk `[VERIFIED]`. Compiled OT
  `.iff`s also absent from the tree (only 2 stray `.iff` in `stage/`, unrelated profile data).
- **Determinism (from reading `TemplateCompiler.cpp`):** `-compile` → `TpfFile::makeIffFiles`. The IFF
  serializer is definition-driven and does **not** embed `time(NULL)` in the output (the only
  `time(NULL)` is the RNG seed in `main`, which does not affect a deterministic template compile).
  Output determinism is plausible **if** the same `.tdf` schema set drives both. No zlib in the OT
  `.iff` path. Lower determinism risk than `.tre`.
- **Verdict:** Byte-exact is plausible. **GATE ACTION:** references must come from a **real SWG client
  install** — pair a known `.tpf` (if the maintainer has dsrc) with its shipped `.iff`, OR (more
  realistically) decompile a shipped `.iff` is NOT available; the honest path is to obtain a `.tpf`
  +`.iff` pair from the maintainer's authoring corpus. **If none exists, this is a gate finding.**

### TemplateDefinitionCompiler → generated C++ (`.cpp`/`.h`)  — **byte-exact = compare TEXT, references absent**
- **Reference pairs:** **zero `.tdf`/`.tpd`** on disk `[VERIFIED]`.
- **Key subtlety (from reading the source):** `-compile` does **not** emit a runtime asset — it
  **generates C++ template class source** (`writeSourceLoadedFlagInit`, writes `.cpp`/`.h`). The
  "byte-exact" comparison is therefore against **generated text files**, and generated C++ commonly
  embeds a **header comment with a generation timestamp / source path** — a classic byte-exact
  breaker. The plan must inspect the generator's header banner for embedded date/path before asserting
  byte-exact is achievable.
- **Verdict:** Byte-exact is comparison-of-generated-source, and likely needs a **normalization step
  or a determinism fix** (strip/pin the generated banner). **GATE ACTION:** confirm the generated-file
  banner content; if it embeds `__DATE__`/`time`/absolute paths, that's a gate finding to resolve
  (pin or normalize) per D-09.

> **Bottom-line gate statement for the planner:** The build (AUTH-01's named risk) is *tractable*. The
> **harder, under-appreciated risk is D-09 byte-exactness** — driven by (1) the absence of
> source→known-good reference pairs inside `swg-client-v2` (they must come from a real client
> install / the maintainer's authoring corpus), (2) zlib-1.1.4-version-locked `.tre` compression, and
> (3) a probable generation-timestamp banner in TemplateDefinitionCompiler's C++ output. Each is a
> "surface and resolve" gate finding under D-09, not a free pass. Plan a checkpoint to confirm
> reference-pair availability **before** committing to the byte-exact smoke per tool.

## RESID-02: Intro-skip scene-transition crash

### What's already deployed (D-06)
The VEH handler `utinniBreakpointVEH` in `UtinniCore/utinni.cpp` (commit `d1096ac`, 2026-05-31)
already captures fatal-class exceptions. `[VERIFIED: git show + source read]`

- **Triggers it logs:** `EXCEPTION_ACCESS_VIOLATION`, `IN_PAGE_ERROR`, `ILLEGAL_INSTRUCTION`,
  `PRIV_INSTRUCTION`, `STACK_OVERFLOW`, `DATATYPE_MISALIGNMENT`, `0x4000001F` (WX86 breakpoint).
- **Module filter:** logs only if EIP is inside `SWGEmu.exe` (`g_swgModule`) or `UtinniCore.dll`
  (`g_utinniModule`), **or** an unmapped EIP (wild jump). CLR/system AVs are skipped so the real
  native site isn't buried.
- **Output line format** (the thing to grep for after the repro):
  ```
  VEH FATAL: code=0x%08X EIP=0x%08X module=%s base=0x%08X rva=0x%08X ESP=0x%08X [WRITE|EXEC|READ target=0x%08X]
  ```
  The **`rva`** field (`EIP - moduleBase`) is the deliverable — directly matchable against SWG's
  hardcoded RVAs and Utinni's hook addresses.
- **Sink:** `utinni::log::warning(fmsg)` — i.e. Utinni's existing **spdlog** sink with
  `flush_on(info)`, so the line reaches disk before the process dies. Capped at 64 fatal lines.
  (Find the concrete log file path from the spdlog setup in `UtinniCore` — it's the same log Utinni
  already writes; confirm path during planning.)
- **Behavior:** returns `EXCEPTION_CONTINUE_SEARCH` — pure observation, crash handling unchanged. Stays
  deployed after capture (D-11). ✓

### Repro path (the known trigger)
- Scene changes are **TJT-driven** via the chat-command parser
  (`addCreateCommandParserCallback`) — see memory `project_scene_change_via_tjt`. **Disabling TJT
  loses the repro path** — do not bisect by disabling TJT.
- The crash: pressing **Return skips the intro cinematic** on the intro→login scene transition;
  produces **no SWG stage dump and no WER event** (SWG's own SEH bypassed). That dump-lessness is
  *why* the VEH was added.
- **Baseline reminder (NOT a crash signal):** landing **naked** after a TJT-driven scene change is the
  expected baseline (`project_tjt_scene_change_naked_baseline`). "Naked, but in world" = success.

### Root-cause disposition (D-11 — two branches)
1. **Fault in Utinni's own injection/detour/callback surface** → fix it properly there. The
   established fix shape is the **detour-table pattern** with RVAs single-sourced via `UTINNI_API`
   (CON-H-03): `using pX = …; pX x = (pX)0xRVA; Detour::Create(…)`. Relevant memories for context if
   the fault is in input/context routing: `project_swg_context_routing` (the in-game key-context
   selector breaks under injection — Enter dispatches as `chatEnter` not `openChat`), and the R-H
   snapshot-dispatch heap-free rule (`project_rh_snapshot_no_heap_alloc` — a prior scene-change crash
   at `0x0051fb0a` was a per-frame `std::vector::reserve` fragmenting SWG's allocator; the fix was a
   stack-allocated fixed-size snapshot in `ground_scene.cpp`). **The intro-skip crash may be the same
   class of callback-dispatch fault** — worth checking the snapshot-dispatch migration status of the
   scene-change callback path first.
3. **Fault resolves into `SWG.exe` game code Utinni does not own** → deliverable is a **documented
   root-cause analysis** (module + RVA + mechanism). Cannot fix code you don't control; the
   live-session success criterion is then met by analysis (accepted per D-11).

### Verification (RESID-02)
- Run the live-injected repro (TJT scene change → Return-skip), capture the `VEH FATAL` line, resolve
  `module+rva`. If `module=UtinniCore.dll` → branch 1. If `module=SWGEmu.exe` or `<unmapped-EIP>` →
  inspect the RVA against known SWG RVAs; likely branch 2 (documented analysis).
- This is a **Tier-4 manual smoke** (live `SWG.exe` injection) — inherently maintainer-in-the-loop per
  TEST-04 / `max-harness` preference. The harness here is the VEH logger itself (already the invented
  harness for a non-unit-testable fault).

## Common Pitfalls

### Pitfall 1: Treating Perforce as a dead dependency for the template tools ⚠️ (corrects D-04 premise)
**What goes wrong:** D-04 and the prior research say "prune dead perforce." If the plan literally
deletes the perforce include/lib *and* leaves the app source untouched, **TemplateCompiler and
TemplateDefinitionCompiler fail to compile** — they actively `#include "clientapi.h"` and subclass
`ClientUser`/`StrBuf`. `[VERIFIED: source read — both .cpp]`
**Why it happens:** The "dead perforce path" is real but applies to **`sharedTemplate.vcxproj`'s
include directory only** (confirmed: no `clientapi`/`ClientUser` references in `sharedTemplate` or
`sharedTemplateDefinition` *library* sources). The two compiler *apps* are a different story.
**How to avoid:** Per-tool decision (Pattern 2): either keep the P4 link (libs present on disk) **or**
stub `checkOut`/`checkIn` + drop the `#include`. The byte-exact `-compile` path needs neither at
runtime. **TreeFileBuilder has no Perforce at all** — clean.
**Warning signs:** Build error `cannot open include file 'clientapi.h'`; unresolved externals
`ClientApi::*`, `StrBuf::*`, `ClientUser::*`.

### Pitfall 2: Inferring build status from the vcxproj toolset (corrects "TemplateCompiler likely-green")
**What goes wrong:** Reading `<PlatformToolset>v145` and concluding the tool builds. **No tool has any
build output** on the current checkout (no `compile/` dir). `[VERIFIED: ls — absent]`
**How to avoid:** D-05 already mandates this — build all three fresh; bank no "early win" on
TemplateCompiler. The cheapest *first green* is **TreeFileBuilder** (12 refs, zlib-only, no P4), not
TemplateCompiler.

### Pitfall 3: Byte-exact `.tre` with a modern zlib
**What goes wrong:** Linking a current zlib/zlib-ng makes `TreeFileBuilder` produce a *correct but
byte-different* `.tre` — fails D-09.
**How to avoid:** Link the **vendored zlib 1.1.4** (`src/external/3rd/library/zlib/lib/win32/zlib.lib`).
Treat the zlib version as part of the pinned dependency set in the manifest.

### Pitfall 4: Missing reference pairs surface only at the smoke step
**What goes wrong:** Plan assumes `swg-client-v2` holds source→known-good pairs; it largely doesn't
(0 `.tpf`/`.tdf`, 1 `.tre`). The smoke task then has nothing to compare against.
**How to avoid:** Add a **reference-pair availability checkpoint** *before* the byte-exact smoke tasks
(per tool). Source references from the real SWG client install / maintainer authoring corpus. If a
pair can't be obtained for a tool, that's the D-09 gate finding to surface.

### Pitfall 5: Coupling to the moving `swg-client-v2` checkout
**What goes wrong:** Building in-place or pinning a branch HEAD; an upstream D3D11/x64 force-push
breaks the tool build even though the tools are headless.
**How to avoid:** D-01/D-03 — copy into `tools/`, pin SHA `5fce7bb8`, never cross-reference. Watch
`x64bit-Upgrade`.

### Pitfall 6: TemplateDefinitionCompiler's generated-source banner breaks byte-exact
**What goes wrong:** Its `-compile` emits C++ source; generated banners often embed a date/path →
non-byte-exact across runs.
**How to avoid:** Inspect the generated header banner during planning; if it embeds `time`/`__DATE__`/
absolute paths, pin or normalize it (a determinism fix is a legitimate D-09 "resolve").

## Code Examples

### Building the lifted solution (verify-only, mirrors the self-hosted CI step shape, D-07)
```powershell
# Source: vcxproj OutDir/Config inspection (Debug|Win32 → *_d.exe). PowerShell syntax (Windows).
& "D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  "D:\Code\Utinni\tools\Utinni.Tools.sln" `
  /p:Configuration=Debug /p:Platform=Win32 /m /nologo /v:minimal
# Front-load TreeFileBuilder (smallest closure, no P4) for the first green:
#   /t:TreeFileBuilder
```

### The byte-exact compare (conceptually the DEC-C3 golden pattern, but real assets — D-09)
```powershell
# Source: TreeFileBuilder.cpp run() — takes -responseFile <rsp> and an output tree name.
& .\TreeFileBuilder_d.exe -r build.rsp out.tre
$ref = Get-FileHash known_good_0005.tre -Algorithm SHA256
$got = Get-FileHash out.tre              -Algorithm SHA256
if ($ref.Hash -ne $got.Hash) { Write-Error "BYTE-EXACT FAIL (D-09 gate finding)"; exit 1 }
```

### VEH fatal line to capture after the live repro (RESID-02)
```
# Source: utinni.cpp:239 snprintf — grep Utinni's spdlog log for:
VEH FATAL: code=0xC0000005 EIP=0x... module=SWGEmu.exe base=0x... rva=0x... ESP=0x... READ target=0x...
#                                                                  ^^^^^^^ the deliverable
```

## State of the Art

| Old (prior research, 2026-06-01) | Current (on-disk, 2026-06-02) | Impact |
|----------------------------------|-------------------------------|--------|
| "TemplateCompiler has v145 Debug objs on disk (likely-green)" | **No `compile/` dir exists; zero build output for all 3 tools** | All three equally unverified; build all fresh; no early-win sequencing on TemplateCompiler |
| "perforce path is dead, present but never #included" | **Live `#include "clientapi.h"` + `ClientUser` subclass in BOTH template app sources**; dead only for `sharedTemplate` *include dir* | D-04 "prune dead perforce" needs a per-tool keep-link-or-stub decision (Pattern 2) |
| "byte-exact reference pairs exist in swg-client-v2" (implied by D-09 framing) | **0 `.tpf`/`.tdf`/`.tpd`, 1 `.tre`** in the whole tree | References must come from a real client install; reference-pair availability is itself a gate checkpoint |
| "TreeFileBuilder = prime unknown / hardest" | TreeFileBuilder = **smallest** closure (12 refs, zlib-only, no P4) but byte-exact `.tre` is the **hardest determinism** (zlib 1.1.4) | Build TreeFileBuilder first for the green; but its D-09 smoke is the riskiest |
| SHA: pin `koogie-msvc-cpp20-base` HEAD | Pin **`5fce7bb8368c86d5a2330a0173d1541866786196`** (HEAD as of 2026-06-02) | Concrete SHA for D-08 |

**Deprecated/outdated:** the "TemplateCompiler likely-green, bank an early win" plan-sequencing
guidance from the project research — superseded above.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The maintainer has a **real SWG client install / authoring corpus** that can supply byte-exact reference pairs (a known `.tpf`+`.iff`, a source-tree+`.tre`). swg-client-v2 supplies almost none. | Byte-Exact Feasibility | If no references exist anywhere, D-09's byte-exact smoke cannot run for that tool → the gate cannot be passed by smoke; must be resolved with maintainer (the D-09 "surface and resolve" path). **HIGH risk — confirm first.** |
| A2 | Linking the vendored **zlib 1.1.4** reproduces the exact deflate stream of the reference `.tre`. | Byte-Exact / Pitfall 3 | If the reference was packed with a different zlib build, `.tre` byte-exact is impossible without matching it → gate finding. |
| A3 | The `-compile` verbs build+link without a live Perforce **server** (P4 only at `-edit`/`-submit` runtime). Confirmed by code path; assumes the **link** resolves with the vendored P4 libs OR the stub compiles cleanly. | Pattern 2 / Pitfall 1 | If P4 libs have v145 binary-compat issues, must stub (low cost). |
| A4 | TemplateDefinitionCompiler's generated C++ banner is the main byte-exact breaker for that tool (timestamp/path). | Pitfall 6 | If the generator also embeds non-deterministic ordering, normalization is harder. |
| A5 | The intro-skip crash repro is still reliably reproducible via the current TJT scene-change path on the maintainer's box. | RESID-02 | If intermittent, capture may need multiple runs; VEH is already deployed so cost is low. |
| A6 | Adding a build lane to the existing self-hosted runner needs **no new runner registration** (same machine/toolset). | Runtime State Inventory | If the runner needs a distinct job/label, a small workflow + possibly runner-label change is needed (machine-local, not in git). |

## Open Questions

1. **Do byte-exact reference pairs exist for each tool?** (A1)
   - Known: swg-client-v2 has ~none (0 `.tpf`/`.tdf`, 1 `.tre`).
   - Unclear: what the maintainer's real client install / dsrc corpus contains.
   - Recommendation: **first planning task** = a maintainer checkpoint confirming reference-pair
     availability per tool, before any byte-exact smoke task is committed.

2. **Keep Perforce link or stub it, per template tool?**
   - Known: P4 is live code but runtime-only on `-edit`/`-submit`; libs present.
   - Recommendation: attempt the link first; if v145 binary-compat fails, stub (Pattern 2). Maintainer-
     in-the-loop per D-10 spirit.

3. **What is TreeFileBuilder's reference `.rsp` for `retail_mini_0005.tre`?**
   - Known: the one `.tre` golden has no recorded source response file.
   - Recommendation: either derive an `.rsp` from a real client `.tre`'s file listing (Utinni already
     enumerates `.tre` contents) + the loose source files, or treat as a gate finding for TRE.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| MSVC v145 toolset | Build all 3 tools at v145 | ✓ | 14.51.36231 / 14.52.36328 | v143 per-tool (D-10, stop-and-ask) |
| VS2026 MSBuild | Build `Utinni.Tools.sln` | ✓ | Dev18 Current | — |
| zlib 1.1.4 static lib | TreeFileBuilder, template tools | ✓ | 1.1.4 (vendored) | **None** — required for `.tre` byte-exact |
| pcre 4.1 (`libpcre.a`) | TemplateCompiler, TemplateDefinitionCompiler | ✓ | 4.1 (vendored) | — |
| Perforce libs (`libclient/librpc/libsupp`) | template tools (link, runtime only on `-edit`/`-submit`) | ✓ | vendored | **Stub** `checkOut`/`checkIn` (Pattern 2) |
| 12–28 shared-lib ProjectReference sources | each tool | ✓ | @ `5fce7bb8` | — |
| Self-hosted v145 CI runner | D-07 build lane | ✓ | `C:\actions-runner`, green | — |
| **Byte-exact reference assets** | D-09 smoke per tool | **✗ (in swg-client-v2)** | — | **Real SWG client install / maintainer corpus (A1) — else gate finding** |
| Live `SWG.exe` (SWGEmu) injected session | RESID-02 repro | maintainer machine | — | None (Tier-4 manual smoke) |

**Missing dependencies with no fallback:**
- Byte-exact reference pairs for TemplateCompiler / TemplateDefinitionCompiler (and a matched
  source+`.tre` for TreeFileBuilder) are **not in swg-client-v2** — must be supplied from a real client
  install or resolved as a D-09 gate finding. **This blocks the byte-exact smoke until resolved.**

**Missing dependencies with fallback:**
- Perforce link → stub `checkOut`/`checkIn` (Pattern 2).
- v145 per-tool refusal → v143 fallback (D-10, stop-and-ask).

## Validation Architecture

> `nyquist_validation` is not present in `.planning/config.json` → treated as **enabled**. This phase
> is unusual: AUTH-01's validation is a **build + byte-exact-binary-compare gate** (not unit tests),
> and RESID-02 is an inherently **Tier-4 manual** live-injection smoke. Map accordingly.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | (AUTH-01) MSBuild build-success + binary hash-compare; (existing) xUnit `Utinni.Cli.Tests` golden harness for the *conceptual* pattern; Catch2 for native (not used here) |
| Config file | `tools/Utinni.Tools.sln` (created this phase); CI workflow `.github/workflows/ci.yml`-class |
| Quick run command | `MSBuild tools\Utinni.Tools.sln /p:Configuration=Debug /p:Platform=Win32 /t:TreeFileBuilder` |
| Full suite command | `MSBuild tools\Utinni.Tools.sln /p:Configuration=Debug /p:Platform=Win32` + per-tool byte-exact hash-compare smoke |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| AUTH-01 | TreeFileBuilder builds+links @ v145 standalone | build | `MSBuild …/t:TreeFileBuilder /p:Platform=Win32` | ❌ Wave 0 (lift `tools/`) |
| AUTH-01 | TemplateCompiler builds+links @ v145 (P4 kept or stubbed) | build | `MSBuild …/t:TemplateCompiler` | ❌ Wave 0 |
| AUTH-01 | TemplateDefinitionCompiler builds+links @ v145 | build | `MSBuild …/t:TemplateDefinitionCompiler` | ❌ Wave 0 |
| AUTH-01 | TreeFileBuilder `.tre` is byte-exact vs known-good | golden (real asset) | run + `Get-FileHash` compare | ❌ Wave 0 (needs reference pair — A1) |
| AUTH-01 | TemplateCompiler `.iff` byte-exact | golden (real asset) | run `-compile` + hash compare | ❌ Wave 0 (needs `.tpf`+`.iff` pair — A1) |
| AUTH-01 | TemplateDefinitionCompiler generated C++ byte-exact (or normalized) | golden (text) | run `-compile` + diff (banner pinned) | ❌ Wave 0 (needs `.tdf` + ref — A1, banner fix Pitfall 6) |
| AUTH-01 | Build lane green on self-hosted v145 CI | CI gate | push → runner build step | ❌ Wave 0 (D-07 wiring) |
| AUTH-01 | Dependency manifest + pinned SHA recorded | artifact check | file presence + content | ❌ Wave 0 (D-08) |
| RESID-02 | Faulting module+RVA captured via VEH on live repro | manual Tier-4 | live inject → grep `VEH FATAL` | ✅ VEH deployed (`d1096ac`); repro manual |
| RESID-02 | Root-cause fixed (Utinni-side) OR documented (game-side) | manual + code/doc | re-run repro / analysis doc | ❌ depends on capture |

### Sampling Rate
- **Per task commit:** `MSBuild …/t:<Tool> /p:Platform=Win32` (the tool touched).
- **Per wave merge:** full `Utinni.Tools.sln` build + available byte-exact smokes.
- **Phase gate:** all three build+link green on the self-hosted v145 runner; every available byte-exact
  smoke green (or the D-09 gate finding explicitly surfaced+resolved per tool); RESID-02 captured and
  dispositioned.

### Wave 0 Gaps
- [ ] `tools/Utinni.Tools.sln` + lifted vcxprojs (TreeFileBuilder first) — covers AUTH-01 build
- [ ] `tools/external/` (zlib 1.1.4, pcre 4.1, perforce-or-stub) — link deps
- [ ] `tools/DEPENDENCY-MANIFEST.md` + `tools/PINNED-SHA.md` (`5fce7bb8`) — D-08 artifacts
- [ ] CI workflow build-lane step for the self-hosted runner — D-07
- [ ] **Reference-pair availability checkpoint (maintainer)** — gates all byte-exact smokes (A1)
- [ ] `.gitignore` entry for the tools' `compile/` OutDir
- [ ] (RESID-02) confirm the concrete spdlog log-file path the VEH line lands in

## Security Domain

> `security_enforcement` is not set to `false` → included. This phase's security surface is **narrow**:
> it builds offline CLI tools and diagnoses a crash; there is no network, auth, or user-input surface
> *added* this phase (the MCP write-safety threat model is Phase 14, explicitly Deferred).

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | — (no auth surface) |
| V3 Session Management | no | — |
| V4 Access Control | no | — |
| V5 Input Validation | partial | The lifted tools parse attacker-influenceable inputs (`.rsp`, `.tpf`, `.tdf`). These run **offline on maintainer-controlled inputs** this phase; the existing SWG parsers' bounds behavior is unchanged. No new untrusted-input surface is *exposed* (no MCP/agent yet). |
| V6 Cryptography | no | md5 here is a content checksum in the TRE format, not a security control — do not treat as such. |

### Known Threat Patterns for {legacy C++ build tools, lift-and-shift}
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Supply-chain: lifted source moves under us (upstream force-push) | Tampering | **Pin exact SHA `5fce7bb8`** (D-03/D-08); never branch HEAD; deliberate re-sync only |
| Stale/ancient vendored libs (zlib 1.1.4, pcre 4.1, P4 API) carry known CVEs | Tampering/DoS | Tools run **offline on trusted inputs**; document the pinned versions in the manifest; the MCP exposure that would matter is Phase-14 Deferred. **Do not** silently upgrade zlib (breaks byte-exact) — note the tension in the manifest. |
| Buffer overflow in 2002-era parsers (fixed `char[10*1024]` buffers in `TreeFileBuilder::addResponseFile`) | Elevation/DoS | Offline/trusted-input this phase; flag for the Phase-14 MCP threat register (when inputs become agent-influenceable) — out of scope here |
| Perforce `-edit`/`-submit` reaching a live P4 server | Info disclosure | **Stub or never invoke** those verbs (Pattern 2); Utinni's byte-exact path is `-compile` only |

## Sources

### Primary (HIGH confidence — direct on-disk inspection, 2026-06-02)
- `swg-client-v2` @ `5fce7bb8368c86d5a2330a0173d1541866786196` (`koogie-msvc-cpp20-base`):
  - `…/application/{TreeFileBuilder,TemplateCompiler,TemplateDefinitionCompiler}/build/win32/*.vcxproj` — v145/stdcpp20/Win32; exact ProjectReference + AdditionalDependencies + include-path lists.
  - `…/TemplateCompiler/src/shared/TemplateCompiler.cpp` (634 LOC) — live `#include "clientapi.h"`, `MyPerforceUser`, `-compile`→`TpfFile::makeIffFiles` (P4-free), `processArgs`.
  - `…/TemplateDefinitionCompiler/src/shared/core/TemplateDefinitionCompiler.cpp` (778 LOC) — live Perforce, `-compile` generates C++ source (not asset), P4-free compile.
  - `…/TreeFileBuilder/src/shared/TreeFileBuilder.cpp` (835 LOC) — zlib-only, no Perforce, `TAG_0005`, smallest-compressor selection, md5 block, no header timestamp.
  - `src/external/3rd/library/{zlib (1.1.4),pcre/4.1,perforce}/` + `src/engine/shared/library/shared*/` — presence verified.
  - `git rev-parse HEAD`, `git branch -a` — SHA + branch/`x64bit-Upgrade` watch.
  - `docs/research/swg-tools-and-likely-studio-toolchain.md` — census confirming Perforce link + "runtime compile decoupled from submit" + `.tpf`→`.iff` pipeline.
- Utinni repo:
  - `UtinniCore/utinni.cpp` — `utinniBreakpointVEH` (the deployed VEH logger, exact log-line format).
  - `git show d1096ac` — VEH deployment commit message + scope.
  - `Utinni.Cli.Tests/Fixtures/**` — existing golden-fixture pattern (the conceptual Tier-2 model).
  - `.planning/research/{SUMMARY,PITFALLS}.md`, `.planning/REQUIREMENTS.md`, `12-CONTEXT.md`, `docs/ai/toolchain-inventory.md`.
  - Local toolchain: VS2026 v145 (14.51/14.52) + MSBuild presence.

### Secondary (HIGH — official MS docs)
- `[CITED: learn.microsoft.com/.../overview-of-potential-upgrade-issues-visual-cpp]` — `std::auto_ptr` (`_HAS_AUTO_PTR_ETC`), two-phase lookup (`/Zc:twoPhase-`).
- `[CITED: devblogs.microsoft.com/cppblog/c-language-updates-in-msvc-build-tools-v14-50]` — v145 conformance deltas; v140–v145 ABI-compatible.

### Tertiary (LOW — assumptions needing maintainer confirmation)
- Availability of real byte-exact reference pairs outside swg-client-v2 (A1) — the dominant open question.

## Metadata

**Confidence breakdown:**
- Dependency closure / build mechanics: **HIGH** — every path, lib, ProjectReference, and toolset verified on-disk.
- v145 buildability: **MEDIUM-HIGH** — toolset present, conformance deltas narrow, shared libs already v145 on this branch, BUT no tool has actually been built (D-05's empirical task remains).
- Byte-exact feasibility (D-09 gate): **LOW-MEDIUM** — reference pairs largely absent from swg-client-v2; zlib-version + generated-banner determinism risks identified but unresolved until a real build+compare runs against real references.
- Perforce disposition: **HIGH** — live-vs-dead verified by source read; mitigation (compile-path P4-free) confirmed by census + code.
- RESID-02 mechanics: **HIGH** — VEH logger read directly; repro path + baseline documented from memory + commit.

**Research date:** 2026-06-02
**Valid until:** ~2026-06-16 for swg-client-v2 specifics (it churns on `koogie-msvc-cpp20-base`; re-verify the pinned SHA if planning slips). Toolchain/MS-docs facts: 30+ days.
