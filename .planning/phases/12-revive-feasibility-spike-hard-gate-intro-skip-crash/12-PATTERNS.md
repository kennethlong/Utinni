# Phase 12: Revive-feasibility spike (HARD GATE) + intro-skip crash - Pattern Map

**Mapped:** 2026-06-02
**Files analyzed:** 11 new/modified surfaces (3 lifted compilers + their copied dep tree, 1 solution, 2 manifest docs, 1 CI step, 1 gitignore edit, 1 byte-exact smoke, 1 RESID-02 fix surface)
**Analogs found:** 4 Utinni-side analogs (CI, golden harness, VEH/detour, in-repo docs) / 4 Utinni-precedent surfaces. The lifted-source surfaces (the three compilers + their #include closure + leaf externals) have **NO in-repo analog by design** — they are copied verbatim from `swg-client-v2` @ `5fce7bb8`.

> **Phase shape (read this first).** This phase is unlike a normal feature phase. Most "new files" are **lift-verbatim copies** from the read-only sibling corpus `D:/Code/swg-client-v2` @ pinned SHA `5fce7bb8368c86d5a2330a0173d1541866786196` (branch `koogie-msvc-cpp20-base`). `tools/` does not exist in Utinni yet, so there is nothing in-repo to copy a *pattern* from for those — the "pattern" IS "copy the exact bytes + preserve the relative ProjectReference graph (D-01)." Real analog-finding effort therefore concentrates on the four surfaces that DO have Utinni precedent: the CI build-lane step, the byte-exact golden-compare smoke, the RESID-02 VEH/detour fix surface, and the manifest/SHA markdown docs. Those four are mapped concretely below; the lifted surfaces are mapped as "lift verbatim, here is the exact source path + the watch-outs."

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `tools/.../TreeFileBuilder/...` (source + `.vcxproj`) | lifted-tool | file-I/O (`.rsp`→`.tre` pack) | `swg-client-v2` @ `5fce7bb8` (verbatim) | lift-verbatim (no in-repo analog) |
| `tools/.../TemplateCompiler/...` (source + `.vcxproj`) | lifted-tool | transform (`.tpf`→`.iff`) | `swg-client-v2` @ `5fce7bb8` (verbatim) | lift-verbatim (no in-repo analog) |
| `tools/.../TemplateDefinitionCompiler/...` (source + `.vcxproj`) | lifted-tool | transform (`.tdf`/`.tpd`→gen C++) | `swg-client-v2` @ `5fce7bb8` (verbatim) | lift-verbatim (no in-repo analog) |
| `tools/.../library/shared*/` (12–28 ProjectReference projects) | lifted-lib | n/a (transitive deps) | `swg-client-v2` @ `5fce7bb8` (verbatim) | lift-verbatim (no in-repo analog) |
| `tools/external/{zlib 1.1.4, pcre 4.1, perforce-or-stub}` | lifted-external | n/a (leaf static libs) | `swg-client-v2` @ `5fce7bb8` (verbatim) | lift-verbatim (no in-repo analog) |
| `tools/Utinni.Tools.sln` | solution | build | `Utinni.sln` (multi-project sln convention) | role-match (new standalone sln, D-07) |
| `tools/DEPENDENCY-MANIFEST.md` | doc/manifest | n/a | `docs/ai/toolchain-inventory.md` (census-table md) | role-match |
| `tools/PINNED-SHA.md` | doc/manifest | n/a | `docs/ai/*.md` provenance convention | role-match |
| `.github/workflows/ci.yml` (NEW build-lane step) | config/CI | build-gate | existing v145 verify+`msbuild Utinni.sln` steps in same file | exact (same file, same pattern) |
| `.gitignore` (NEW `tools/compile/` entry) | config | n/a | existing `.gitignore` build-output rules | exact |
| byte-exact smoke (PowerShell `Get-FileHash` compare) | test | golden-compare | `Utinni.Cli.Tests` `GoldenTestRunner` + the CI golden-tests step | role-match (real assets, not synth — D-09) |
| RESID-02 fix surface (IF Utinni-side) | fix (detour/VEH) | event-driven (scene-change callback) | `UtinniCore/utinni.cpp` VEH + `ground_scene.cpp` detour table | exact (same class of fault) |

---

## Pattern Assignments

### Lifted compilers + their dep tree (lifted-tool / lifted-lib / lifted-external)

**Analog:** NONE in-repo. **Lift verbatim** from `D:/Code/swg-client-v2` @ `5fce7bb8368c86d5a2330a0173d1541866786196`.

**Exact source paths to copy (D-01 — copy, never `#include`/`ProjectReference` across into the live tree):**
```
src/engine/shared/application/TreeFileBuilder/            (FirstTreeFileBuilder.{cpp,h}, TreeFileBuilder.{cpp,h}, .dox + build/win32/TreeFileBuilder.vcxproj) — 835 LOC
src/engine/shared/application/TemplateCompiler/           (TemplateCompiler.cpp, FirstTemplateCompiler.{cpp,h} + build/win32/TemplateCompiler.vcxproj) — 634 LOC
src/engine/shared/application/TemplateDefinitionCompiler/ (TemplateDefinitionCompiler.cpp, FirstTemplateDefinitionCompiler.{cpp,h} + .vcxproj) — 778 LOC
src/engine/shared/library/shared*/                        (the 12 / 26 / 28 ProjectReference targets — see RESEARCH §Standard Stack for exact per-tool sets)
src/external/3rd/library/zlib/    (zlib 1.1.4 — lib/win32/zlib.lib)   ← byte-exact determinant, do NOT substitute (Pitfall 3)
src/external/3rd/library/pcre/4.1/ (libpcre.a — TemplateCompiler + TemplateDefinitionCompiler)
src/external/3rd/library/perforce/ (libclient/librpc/libsupp.lib — KEEP-OR-STUB decision, see below)
```

**Pattern to preserve (RESEARCH §Pattern 1):** copy the app + transitive shared-lib `.vcxproj`s + leaf externals into `tools/`, **preserving the relative directory shape** (`application/<Tool>/build/win32`, `library/shared*/build/win32`, `external/…`) so the `..\..\..\..\..\..\external\…` relative `ProjectReference`/include paths resolve **within the copy**. Re-home all lifted GUIDs under one `Utinni.Tools.sln` and keep them internally consistent.

**Per-tool keep-or-stub decision (corrects D-04 premise — RESEARCH Pitfall 1 / Pattern 2):**
`TemplateCompiler.cpp` and `TemplateDefinitionCompiler.cpp` actively `#include "clientapi.h"` and subclass `ClientUser`/`StrBuf` — Perforce is **live code**, NOT dead, for both apps. The byte-exact `-compile` verb is P4-free at runtime (P4 only behind `-edit`/`-submit`). Decide per tool: keep the P4 link (libs present) OR stub. Stub option (drops `libclient/librpc/libsupp/ws2_32`):
```cpp
int checkOut(const char *) { fprintf(stderr, "checkOut: Perforce disabled in Utinni lift\n"); return -1; }
int checkIn (const char *) { fprintf(stderr, "checkIn:  Perforce disabled in Utinni lift\n"); return -1; }
// and delete the `#include "clientapi.h"` + MyPerforceUser/StrBufFixed classes.
```
**TreeFileBuilder has NO Perforce** (12 refs, zlib-only) — front-load it for the first green.

**v145 conformance deltas to expect (narrow):** `std::auto_ptr` removal (`_HAS_AUTO_PTR_ETC`), two-phase lookup (`/Zc:twoPhase-`), stricter enum/template rules. The shared libs are already v145/C++20 on this branch. Do NOT confuse with the CppSharp v145 block (unrelated, clang-11 vs MSVC STL).

---

### `tools/Utinni.Tools.sln` (solution, build)

**Analog:** `Utinni.sln` (the existing multi-project solution at repo root) — for the `.sln` project-list/GUID/configuration-matrix convention only.

**Key divergence from the analog (D-07):** This is a **separate, standalone, Utinni-owned solution**, NOT folded into `Utinni.sln`'s build matrix. It builds `Debug|Win32` / `Release|Win32` (x86 only — CON-P-02). Build invocation mirrors the research code example:
```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  "D:\Code\Utinni\tools\Utinni.Tools.sln" `
  /p:Configuration=Debug /p:Platform=Win32 /m /nologo /v:minimal
# Front-load TreeFileBuilder (smallest closure, no P4): /t:TreeFileBuilder
```
Outputs `*_d.exe` (Debug) under the vcxprojs' OutDir (`compile/win32/<Tool>/<Config>/`).

---

### `.github/workflows/ci.yml` — NEW `Utinni.Tools.sln` build-lane step (config/CI, build-gate)

**Analog:** the SAME file's existing v145 build steps — `.github/workflows/ci.yml` (see lines below). This is the **strongest, exact-match analog** in the phase: a new step bolts onto the already-green self-hosted v145 runner (`project_self_hosted_ci`).

**Runner targeting** (`.github/workflows/ci.yml:23`) — reuse the same `runs-on` label set, no new runner registration (A6):
```yaml
runs-on: [self-hosted, windows, x64, utinni-v145]   # this machine: VS 2026 (v145) + VS 2019 (v142/14.29)
defaults:
  run:
    shell: powershell    # runner has Windows PowerShell 5.1 only — 5.1-safe syntax
```

**Verify-v145-then-build pattern** (`.github/workflows/ci.yml:119-132` + `:165-171`) — the new tools step follows this exact shape (the `Verify v145 build tools` step already runs earlier and gates the whole job, so the tools step just needs the `msbuild` invocation):
```yaml
- name: Setup MSBuild
  uses: microsoft/setup-msbuild@v2

- name: Build solution (Release|x86)
  run: msbuild Utinni.sln /m /restore /p:Configuration=Release /p:Platform=x86 ...
```
**New step to add (D-07), modeled on the line above but Win32/standalone-sln:**
```yaml
- name: Build tools solution (Debug|Win32) — AUTH-01 hard gate
  run: msbuild tools\Utinni.Tools.sln /m /p:Configuration=Debug /p:Platform=Win32
  # Front-load TreeFileBuilder is a per-task local concern; the gate builds all three.
```

**Targeted-build precedent** (`.github/workflows/ci.yml:205`, `:211`) — the native-test steps already use `/t:<Project>` targeted builds rather than full-solution rebuilds; reuse `/t:TreeFileBuilder` for the per-task "first green" pattern.

**Note:** the byte-exact smoke is a SEPARATE step from the build step (the build is the AUTH-01 build gate; the smoke is the D-09 gate). The smoke is gated behind reference-pair availability (A1) — see "No Analog / Gate Findings" below.

---

### Byte-exact golden-compare smoke (test, golden-compare)

**Analog:** `Utinni.Cli.Tests/Infrastructure/GoldenTestRunner.cs` (the in-repo Tier-2 golden harness) + the `Run CLI golden tests` CI step (`.github/workflows/ci.yml:187-190`).

**The analog's core compare pattern** (`GoldenTestRunner.cs:40-70`) — exact-bytes comparison with a dump-on-mismatch escape hatch:
```csharp
// expected vs actual, exact compare after normalization, dump both halves on fail:
if (!string.Equals(expectedText, normalizedActual, StringComparison.Ordinal))
{
    DumpMismatch(fixtureKey, expectedText, normalizedActual, ".txt");  // writes expected+actual to TestResults/, throws XunitException
}
```

**Key divergences D-09 imposes (do NOT copy the analog blindly):**
1. **Real assets, not synth fixtures.** The analog uses in-repo synth fixtures (≤200 bytes, no LFS). D-09 mandates a **real shipped `swg-client-v2`/SWG-client asset** and a known-good reference — these are NOT in the repo (see Gate Findings).
2. **Binary byte-exact, not normalized-text.** For `.tre`/`.iff` the compare must be **raw bytes** (no CRLF normalization — that's only valid for the generated-C++ text case). Use hashing, per the research code example:
```powershell
& .\TreeFileBuilder_d.exe -r build.rsp out.tre
$ref = Get-FileHash known_good_0005.tre -Algorithm SHA256
$got = Get-FileHash out.tre              -Algorithm SHA256
if ($ref.Hash -ne $got.Hash) { Write-Error "BYTE-EXACT FAIL (D-09 gate finding)"; exit 1 }
```
3. **NO structural/round-trip fallback** (D-09). Cross-toolset non-determinism (zlib 1.1.4 deflate, generated-C++ banner timestamp) is a gate finding to surface+resolve, not a pass.

The `DumpMismatch`→`TestResults/` artifact pattern (`GoldenTestRunner.cs:72-89`) is worth mirroring so a byte-exact failure leaves the expected+actual bytes on disk for triage (the CI `upload-artifact@v4 if: failure()` steps at `.github/workflows/ci.yml:192-201` are the precedent for uploading those).

---

### `tools/DEPENDENCY-MANIFEST.md` + `tools/PINNED-SHA.md` (doc/manifest)

**Analog:** `docs/ai/toolchain-inventory.md` (the ~60-tool census markdown — table-driven, status-annotated) for `DEPENDENCY-MANIFEST.md`; the `docs/ai/*.md` provenance/`[VERIFIED: …]` convention for `PINNED-SHA.md`.

**`DEPENDENCY-MANIFEST.md` content (D-08):** per-tool `#include` closure + required shared libs, with **dead include paths shown as pruned** and the **keep-or-stub Perforce decision recorded per tool**. The exact per-tool dep sets are pre-computed in RESEARCH §Standard Stack (12 / 26 / 28 ProjectReferences) — copy those tables. Pin the zlib version (1.1.4) explicitly as part of the dependency set with the byte-exact-tension note (Security Domain row).

**`PINNED-SHA.md` content (D-08/D-03):**
```
Repo:    D:/Code/swg-client-v2
Branch:  koogie-msvc-cpp20-base
SHA:     5fce7bb8368c86d5a2330a0173d1541866786196   [VERIFIED: git rev-parse, 2026-06-02]
Watch:   x64bit-Upgrade, MSVC-CPP20-Upgrade  (x64 migration collides with CON-P-02 — re-sync deliberately)
```

---

### `.gitignore` — NEW tools OutDir entry (config)

**Analog:** the existing `.gitignore` build-output rules (`Release/`, `Debug/`, `obj/`, `/vcpkg_installed/`).

**Pattern:** add a `tools/compile/` (or chosen OutDir) ignore so the `*_d.exe`/`*_r.exe` binaries never get committed — only the project config + manifests are tracked. Note the existing file already has `Debug/`/`Release/` rules; the tools' OutDir is `compile/win32/...` per the vcxprojs, which is NOT covered by the existing rules, so an explicit entry is needed. Follow the file's convention of a comment explaining WHY (the file is heavily commented — e.g. lines 5-9, 35-39).

---

### RESID-02 fix surface — IF Utinni-side (fix: detour/VEH, event-driven)

**Analog:** `UtinniCore/utinni.cpp` (VEH capture site) + `UtinniCore/swg/scene/ground_scene.cpp` (detour-table pattern for scene-change callbacks — the SAME class of fault per `project_rh_snapshot_no_heap_alloc`).

**VEH capture is ALREADY deployed (D-06 — do NOT re-instrument).** The handler `utinniBreakpointVEH` (`utinni.cpp:136-245`) logs fatal-class exceptions. The deliverable is reading its output, not editing it. Install site (`utinni.cpp:289-291`):
```cpp
g_utinniModule = handle;                 // UtinniCore.dll (from &createDetours)
g_swgModule = GetModuleHandleA(nullptr); // the injected SWG client exe
AddVectoredExceptionHandler(1, utinniBreakpointVEH);   // FirstHandler=1 → runs before other VEHs
```
**The line to grep after the repro** (`utinni.cpp:238-243`) — the `rva` field is the deliverable:
```
VEH FATAL: code=0x%08X EIP=0x%08X module=%s base=0x%08X rva=0x%08X ESP=0x%08X [WRITE|EXEC|READ target=0x%08X]
```
Sink = `utinni::log::warning` → Utinni's spdlog (`flush_on(info)`, capped at 64 fatal lines). Confirm the concrete log-file path from the `log::create()` spdlog setup during planning (Wave-0 gap).

**IF the fix lands in Utinni's detour surface (D-11 branch 1):** copy the detour-table pattern from `ground_scene.cpp:46-69` — RVAs single-sourced as typed function pointers, hooked via `Detour::Create` (CON-H-03):
```cpp
// ground_scene.cpp:46-68 — the established RVA single-sourcing shape:
using pInit = void(__thiscall*)(utinni::GroundScene* pThis, const char* terrain, utinni::Object* playerObj, float time);
pInit init = (pInit)0x00518EB0;
// ...installed at ground_scene.cpp:412-414:
swg::groundScene::draw = (swg::groundScene::pDraw)Detour::Create(swg::groundScene::draw, hkDrawLoop, DETOUR_TYPE_PUSH_RET);
```
**Prior-art for this exact fault class:** `project_rh_snapshot_no_heap_alloc` — a prior scene-change crash at `0x0051fb0a` was a per-frame `std::vector::reserve` fragmenting SWG's allocator; fixed with a stack-allocated fixed-size snapshot (`dispatchSnapshot` template in `ground_scene.cpp`). **Check the snapshot-dispatch migration status of the scene-change callback path FIRST** — the intro-skip crash may be the same class. Also `project_swg_context_routing` (Enter dispatches as `chatEnter` not `openChat` under injection) if the fault is input/context routing.

**IF the fault resolves into `SWG.exe` code Utinni does not own (D-11 branch 2):** deliverable is a **documented root-cause analysis** (module + RVA + mechanism) — no code fix. Use DETOUR_LEN_AUTO if any detour is involved (memory `feedback_detourxs_explicit_len`).

---

## Shared Patterns

### Self-hosted v145 CI runner (build lane)
**Source:** `.github/workflows/ci.yml` (lines 23, 119-132, 165-171, 205)
**Apply to:** `Utinni.Tools.sln` build step + byte-exact smoke step.
- Reuse `runs-on: [self-hosted, windows, x64, utinni-v145]` — no new runner registration (A6).
- The `Verify v145 build tools` step (`:119-132`) already gates the job; the tools build step just adds the `msbuild tools\Utinni.Tools.sln /p:Platform=Win32` invocation.
- `defaults.run.shell: powershell` (5.1-safe syntax only) and `upload-artifact@v4 if: failure()` for byte-exact dumps.

### Golden-compare (deterministic artifact verification)
**Source:** `Utinni.Cli.Tests/Infrastructure/GoldenTestRunner.cs` (lines 40-89)
**Apply to:** all three byte-exact smokes — BUT against real assets (D-09), binary hash for `.tre`/`.iff`, text-normalized only for generated C++.
- Mirror the dump-expected+actual-on-mismatch escape hatch so a failure leaves bytes for triage.

### VEH crash capture + detour single-sourcing
**Source:** `UtinniCore/utinni.cpp:136-291`, `UtinniCore/swg/scene/ground_scene.cpp:46-69, 412-414`
**Apply to:** RESID-02 capture (already deployed — read only) and any Utinni-side fix (detour table, RVAs single-sourced, DETOUR_LEN_AUTO).

### In-repo census/provenance markdown
**Source:** `docs/ai/toolchain-inventory.md` (table-driven census), `docs/ai/*.md` `[VERIFIED: …]` provenance convention
**Apply to:** `tools/DEPENDENCY-MANIFEST.md` + `tools/PINNED-SHA.md`.

---

## No Analog Found / Gate Findings

Surfaces with no close in-repo match (planner uses RESEARCH.md / lift-verbatim instead):

| File / Surface | Role | Data Flow | Reason |
|------|------|-----------|--------|
| The three compilers + their #include closure + leaf externals | lifted-tool/lib/external | transform / file-I/O | `tools/` does not exist yet; **lift verbatim** from `swg-client-v2` @ `5fce7bb8`. No Utinni precedent — the "pattern" is copy-exact-bytes + preserve relative ProjectReference graph (D-01). |
| Byte-exact REFERENCE assets (`.tpf`+`.iff`, source-tree+`.tre`, `.tdf`+gen-C++) | test fixture | golden input | **Largely absent from `swg-client-v2`** (0 `.tpf`/`.tdf`, 1 `.tre`). Must come from a real SWG client install / maintainer authoring corpus (A1). **GATE: add a maintainer reference-pair availability checkpoint BEFORE any byte-exact smoke task** (Open Question 1 / Pitfall 4). |

**Three loud D-09 gate findings the planner must carry (RESEARCH §Byte-Exact Feasibility):**
1. **`.tre` byte-exact is zlib-1.1.4-locked** — link the vendored 1.1.4, never a modern zlib (Pitfall 3). The one golden `.tre` (`retail_mini_0005.tre`) has no recorded source `.rsp` (Open Q 3).
2. **TemplateDefinitionCompiler generated-C++ banner** likely embeds a timestamp/path — a byte-exact breaker; inspect + pin/normalize (Pitfall 6).
3. **Reference-pair availability** is itself the gate's biggest unknown (A1, HIGH risk) — confirm with maintainer first.

---

## Metadata

**Analog search scope:** `.github/workflows/`, `Utinni.Cli.Tests/`, `UtinniCore/` (utinni.cpp + swg/scene/), `docs/ai/`, repo-root `.gitignore` + `Utinni.sln`; lifted-source provenance at `D:/Code/swg-client-v2` (read-only, per RESEARCH).
**Files scanned (Utinni-side analogs):** `.github/workflows/ci.yml`, `Utinni.Cli.Tests/Infrastructure/GoldenTestRunner.cs`, `UtinniCore/utinni.cpp`, `UtinniCore/swg/scene/ground_scene.cpp`, `.gitignore`, `docs/ai/` index.
**`tools/` directory:** confirmed ABSENT (created from scratch this phase).
**Pinned lift SHA:** `5fce7bb8368c86d5a2330a0173d1541866786196`
**Pattern extraction date:** 2026-06-02
