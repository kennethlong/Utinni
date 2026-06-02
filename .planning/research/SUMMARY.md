# Project Research Summary

**Project:** Utinni - milestone v2.0 "AI-Assisted SWG Tools"
**Domain:** AI-drivable (MCP) modding-tool authoring pipeline + legacy-C++ build-tool revival + first DCC-style editors, layered onto shipped Utinni V1 (`v1.0.0`)
**Researched:** 2026-06-01
**Confidence:** HIGH

> Detailed research lives in `STACK.md`, `FEATURES.md`, `ARCHITECTURE.md`, `PITFALLS.md` (this folder). This file synthesizes them for the requirements + roadmap consumers. The existing V1 stack/architecture (UtinniCore x86 + CppSharp bridge + net472 WinForms/MEF host + `UtinniCoreDotNet` byte-exact codecs + `Utinni.Cli` JSON verbs) is **validated and out of scope** - do not re-research or change it.

## Executive Summary

v2.0 turns Utinni from a tool that *edits* SWG assets into one that *authors* them, and makes the whole pipeline drivable by an AI agent. The research converges hard on a single shape: **a separate modern-.NET (net10.0) MCP server process (`Utinni.Mcp`) that shells out to the already-shipped `utinni-cli.exe` and a small set of revived SWG build CLIs.** The MCP server owns no format logic - every capability is a `utinni-cli` verb first (golden-tested, the DEC-C3 Tier-2 pattern), then a one-line MCP dispatcher. This is the "thin shim over byte-exact verbs" the milestone describes, and it keeps the modern-.NET transport loop off the fragile net472/x86 injected surface. Headless-first (file edits with no client running) captures ~90% of the agent value; live-injected MCP is an explicit, optional, later increment behind a named-pipe IPC.

The single most important correction the research surfaces is the **revive-feasibility picture**: PROJECT.md and `toolchain-inventory.md` frame the build work as a "v143->v145 port" Utinni must do itself, anticipating CppSharp-style modern-STL pain. On-disk inspection of `swg-client-v2` contradicts this. The revive targets are **already** `PlatformToolset=v145` + `LanguageStandard=stdcpp20` with native MSVC STL (STLport 4.5.3 is *gone from disk*, surviving only as a dead `.rsp` line). The actual remaining work is small and mechanical - verify a standalone build, strip the dead Perforce/Alienbrain vestige, produce a per-tool dependency manifest - **not** a toolset port. Critically, the status is **not uniform per tool**: `TemplateCompiler.vcxproj` has built v145 Debug objects on disk (likely-green), while `TreeFileBuilder.vcxproj` is also v145 but has *no build output* (unverified). The CppSharp v145 block is a *vendored-clang-parsing-MSVC-STL* problem that does **not** predict native-tool difficulty - do not conflate them. **This correction must flow back into PROJECT.md / toolchain-inventory.md, which currently overstate the effort.**

The dominant risk is no longer the build - it is **write safety**. An MCP write surface lets an agent loop, and a byte-exact writer producing a *well-formed-but-wrong* archive is invisible to every parser. The mitigation is a 5-layer defense-in-depth model (advisory annotations -> elicitation/human-in-loop -> loose-override-by-default -> byte-exact verify-before-commit -> backup/recovery), and the structural advantage is that **4 of the 5 layers are already-shipped V1 primitives** (`LooseOverridePath`, `TreBackupPath`, `TreRepackLock`, the roundtrip goldens). The net-new code is small but the *security contract must be first-class at design time*, not a later hardening pass - and the one genuinely new write surface is a **SAVE verb the CLI lacks today** (`Utinni.Cli` is read+roundtrip only).

## Key Findings

### Recommended Stack

The v2.0 additions are deliberately dependency-flat. The MCP server is the only place new packages land; the revived CLIs add *no* package installs (pure lift-and-shift of MSVC C++); the new editors need *format codecs inside `UtinniCoreDotNet`*, not new NuGet/vcpkg packages. See `STACK.md`.

**Core technologies (NEW for v2.0):**
- **ModelContextProtocol 1.3.0** (C# MCP SDK) - exposes Utinni's verbs as agent tools - official Microsoft-collaborated SDK; `[McpServerTool]` discovery + `WithStdioServerTransport()` is exactly the thin-shim shape. **Targets net8/9/10 + netstandard2.0 - NO net4xx target.** This single fact drives the separate-process decision.
- **.NET 10.0 (LTS) host** for `Utinni.Mcp` - matches the `Microsoft.Extensions.* 10.x` floor the SDK pulls anyway (avoids binding-redirect skew); free to run x64, crash-isolated from the live game. (net8.0 is an acceptable wider-floor alternative.)
- **stdio transport** - canonical for a local single-client desktop tool; HTTP/SSE adds a network + auth surface this use case does not need (SSE is deprecated - never pick it).
- **MSVC v145 / `/std:c++20`** for the revived CLIs - *already* the upstream config on the lifted source; match it, do not downgrade. v140-v145 are ABI-compatible (link newest).
- **Vendored leaf externals only** - `zlib` (TreeFileBuilder), `pcre/4.1` (TemplateCompiler). Do **not** drag STLport453 (gone), Perforce/Alienbrain (dead SCM vestige), or the renderer (these tools are headless - no D3D/D3DX).

### Expected Features

The theme is *Utinni authors, not just edits*. Four capability groups: (A) MCP server, (B) revive+wrap compile CLIs, (C) DCC-style editors, (D) formalize the Blender boundary. See `FEATURES.md`.

**Must have (table stakes):**
- stdio MCP server skeleton + read tools wrapping the 9 existing `Utinni.Cli` verbs - agents must *see* assets first; lowest cost.
- MCP write tools defaulting to **loose-override**, with the full safety model (annotations + elicitation gate + verify-before-commit + backup) - the corruption defense; the centerpiece differentiator. **Needs a new CLI SAVE verb.**
- Revive `TemplateDefinitionCompiler` (`.tdf`/`.tpd` -> param->type schema) and `TemplateCompiler` (`.tpf` -> object-template `.iff`) - author-new-template gap; the definition compiler also unblocks OT Tier-2.
- OT Tier-2 typed list-param display - closes the carried residual, cheaply, once the definition compiler's param->type map exists.

**Should have (competitive / differentiators):**
- **Byte-exact verify-before-commit as a first-class guarantee** - "the agent cannot return a corrupt success"; most MCP servers can't claim this. Infra already exists.
- **Loose-override-by-default write model** - agent edits never touch source archives unless explicitly escalated; structural, not bolted-on, safety.
- Revive `TreeFileBuilder` (build-from-source `.tre`, not just repack) wired as an MCP pack tool.
- First DCC-style editor SubPanel (Terrain or Particle, modder-demand-driven) + its new `UtinniCoreDotNet` codec; WorldSnapshot editor by growing the existing Snapshot panel (lowest-risk, injection-native already).
- MCP prompts for canned pipelines (edit -> compile -> repack -> validate).

**Defer (post-v2.0):**
- Live-in-client preview via MCP (live-patch tier is infra-ready but user-disabled - high risk, opt-in).
- Animation live-preview coordinated with the Blender suite.
- DataTableTool compile path, item-exporter wrappers, second/third DCC editors.

**Anti-features (locked):** HTTP/SSE remote transport; a raw "exec arbitrary CLI" tool; auto-approving writes; trusting `destructiveHint` for *enforcement* (spec says hints are advisory); 3D mesh/skeleton/animation/texture authoring (DEC-A3 - Blender's lane).

### Architecture Approach

Four integration forks, all resolved (see `ARCHITECTURE.md`). The load-bearing fact that makes headless-first possible: `UtinniCoreDotNet`'s `Formats/`+`Editing/`+`Saving/` are pure-managed and run with **no native DLL load** - `utinni-cli` already exercises them headless. The MCP server bolts on without disturbing the injected client at all in v2.0.

**Major components:**
1. **`Utinni.Mcp`** (NEW, separate net10.0 process, x64) - MCP stdio server; tool schema + arg validation; maps each tool call to a `Process.Start` of `utinni-cli.exe` (or a revived CLI), parses the JSON envelope back. Owns *zero* format/business logic.
2. **`utinni-cli.exe`** (EXISTING, net472 x86, extended) - headless READ + byte-exact EDIT verbs over `UtinniCoreDotNet`; **gains new verbs**: compile-template, build-tre, and a **SAVE verb** (the key net-new write surface).
3. **Revived build CLIs in repo-local `tools/`** (NEW, lift-and-shift @ v145) - `TemplateCompiler`, `TemplateDefinitionCompiler`, `TreeFileBuilder`; source->binary transforms emitting `.iff`/`.tre` the existing readers consume.
4. **New TJT SubPanels** (NEW, UtinniPlugins repo) - Terrain/Particle/WorldSnapshot as `IEditorPlugin.GetSubPanels()` MEF parts - the *unchanged* Wave-1 (DEC-C4) seam; no new mechanism.

**Two resolved forks worth restating for requirements:**
- **Compiler vs writer = coexistence by verb ownership (no double implementation).** BUILD-from-source -> revived compiler; EDIT-existing-binary -> byte-exact `UtinniCoreDotNet` writer. They write at different lifecycle stages; name the MCP tools `compile_*`/`build_*` vs `edit_*` so the LLM picks correctly.
- **Two distinct template compilers, frequently conflated.** `TemplateDefinitionCompiler` (`.tdf`/`.tpd` -> per-class param->type *schema*) is the **cheap path to OT Tier-2**. `TemplateCompiler` (`.tpf` -> object-template `.iff`) is the author-new-template path and depends on the definition compiler's output.

### Critical Pitfalls

Top items from `PITFALLS.md` (anchored to *this* shipped system, not generic mistakes):

1. **Agent writes a well-formed-but-semantically-wrong asset into a live archive** - byte-exactness is *correctness, not safety*; no parser flags it. Avoid: default every write tool to loose-override; gate `.tre` repack behind a distinct, off-by-default, `destructiveHint`+`dry_run`-annotated tool; always write through `TreBackupPath`.
2. **Over-broad write scope (one `write_asset(path, bytes)` tool)** - gives the agent disk-wide authority; un-retrofittable once agents depend on the tool shape. Avoid: split read/write into per-format scoped tools; **pin `resolvedRoot` at server startup, canonicalize once, route every write through `LooseOverridePath.Resolve` - never accept an absolute path from the agent; fail closed if no root configured.**
3. **Tool-poisoning / prompt-injection via untrusted asset content** - mod files are attacker-influenceable; an LLM doesn't distinguish data from instructions the way a ListView does. Avoid: write tools take *typed structured args only* (record index, column id, typed value), never "apply the change you inferred"; audit-log every write invocation.
4. **Lift-and-shift drags the transitive + dead dependency graph** - `TemplateCompiler.vcxproj` carries ~25 ProjectReferences and a *dead* `perforce/include` path (present, not `#included`). Avoid: spike the real `#include` closure, prune dead include dirs, ship a per-tool dependency manifest as a deliverable.
5. **Mis-judging the v143->v145 port (both directions)** - assuming it's unsolved wastes weeks; assuming all tools are equally done is wrong (`TemplateCompiler` likely-green, `TreeFileBuilder` unverified). Avoid: per-tool "compiles + links + round-trips at v145" gate; treat "is v145 in the vcxproj" and "actually builds" as different facts.
6. **Coupling the build to a moving `swg-client-v2` checkout** - it's actively churning on branch `koogie-msvc-cpp20-base` mid-D3D9->D3D11 migration, with a live `x64bit-Upgrade` branch. Avoid: copy into a Utinni-owned location, **record the exact lifted-from SHA** (pin a SHA, not a branch HEAD), never `#include`/ProjectReference across into the live tree. **Watch x64 vs Utinni's hard x86 constraint (CON-P-02)** - the tools are `Win32`/x86 today matching Utinni; an upstream x64 migration would diverge.
7. **Scope creep into 3D mesh/skel/anim authoring (DEC-A3 anti-goal)** - "preview the animation" quietly becomes "author it." Avoid: encode a one-sentence preview-vs-author test per editor; Animation deliverable is read-only live-preview of a Blender-exported anim.

## Implications for Roadmap

The combined research yields an unusually clear build order: a **hard-gate revive-feasibility spike first**, then cheap revive+wrap (which also unblocks OT Tier-2), then the headless MCP centerpiece, then the meatier editors, with live-injected MCP explicitly last and optional. This front-loads the highest-leverage, lowest-cost work and de-risks the one genuine unknown (the build) before anything depends on it.

### Phase 1: Revive-Feasibility Spike (hard gate - Wave 0)
**Rationale:** The entire revive+wrap strategy is contingent on the lifted tools actually building standalone at v145. This is the *named* spike in PROJECT.md and the single biggest gating risk. It is cheap and must run before any wrap design. **Corrects the overstated "v143->v145 port" framing** - the real work is verify-build + strip-dead-deps + manifest, not a toolset port.
**Delivers:** Per-tool build-status verification (`TemplateCompiler` likely-green from on-disk objs; `TreeFileBuilder` unverified - build it independently); a verified per-tool **dependency manifest** with dead `perforce`/`alienbrain` paths pruned; the **recorded lifted-from `swg-client-v2` SHA** (pin SHA, not branch); a Utinni-owned `tools/Utinni.Tools.sln`.
**Addresses:** Authoring-pipeline table stakes (the revive prerequisite).
**Avoids:** Pitfalls 4 (transitive/dead deps), 5 (mis-judged port), 6 (moving-checkout coupling, x64 watch).

### Phase 2: Wrap Revived Compilers as CLI Verbs + OT Tier-2
**Rationale:** Once the tools compile, wrapping them as `utinni-cli` verbs is pure, low-risk wrapping over golden fixtures (DEC-C3 Tier-2). `TemplateDefinitionCompiler`'s param->type map is the cheapest route to closing the OT Tier-2 carried residual - so this phase pays a centerpiece feature *and* a residual in one.
**Delivers:** `compile-template`, `compile-definition`, `build-tre` CLI verbs + goldens; the param->type schema surfaced; OT Tier-2 typed list-param display closed.
**Uses:** Revived `TemplateDefinitionCompiler`/`TemplateCompiler`/`TreeFileBuilder` (Phase 1); existing `Utinni.Cli` golden-fixture harness.
**Implements:** Architecture Pattern 2 (revive-and-wrap) + Pattern 3 (coexistence by verb ownership).

### Phase 3: Headless MCP Server (`Utinni.Mcp`) - read + edit + build + SAVE
**Rationale:** The centerpiece. Everything it dispatches now exists as a CLI verb. It is a thin dispatcher with no business logic. The one net-new code item - a **SAVE verb** - lands here, because `Utinni.Cli` is read+roundtrip only today and the write surface is what "authors, not just edits" requires.
**Delivers:** net10.0 stdio MCP server; read tools over the 9 existing verbs; build tools over Phase-2 verbs; **write tools defaulting to loose-override with the full 5-layer safety model** (annotations + elicitation + verify-before-commit + backup); a first-class `MCP-SECURITY.md` threat register mirroring Phase-7's.
**Addresses:** MCP table stakes + the two headline differentiators (byte-exact verify, loose-override-default).
**Avoids:** Pitfalls 1 (live-archive corruption), 2 (over-broad scope - pin root, per-format tools, fail-closed), 3 (tool-poisoning - typed args + audit log).
**Uses:** ModelContextProtocol 1.3.0, net10.0, stdio (STACK.md).

### Phase 4: First DCC Editor - WorldSnapshot, then Terrain/Particle
**Rationale:** Editors are the meatier lift (UI + format depth), so they come after the cheap headless base. **WorldSnapshot first** - it grows the existing Snapshot panel, is injection-native already (Utinni's origin), and needs **zero new deps**. Terrain/Particle follow because they require *new format codecs* (`.trn`/`.prt`) in `UtinniCoreDotNet`.
**Delivers:** WorldSnapshot/object-placement SubPanel (extend Snapshot panel, transform gizmo, four-tier save); then Terrain or Particle SubPanel + its new codec; matching MCP edit/save tools.
**Addresses:** "New editor (replace)" features; mirrors the Wave-1 MEF SubPanel seam (DEC-C4) unchanged.
**Avoids:** Pitfall 7 (preview-vs-author boundary gate per editor).

### Phase 5 (optional, last): Live-Injected MCP Bridge
**Rationale:** Only after headless MCP proves the tool ergonomics. Requires a NEW named-pipe IPC into the x86 injected process - the biggest new-mechanism risk - and reconciling the modern-.NET MCP host with the in-proc x86 client.
**Delivers:** Live in-client preview as an MCP tool (live-patch tier, gated/opt-in).
**Avoids:** Anti-Pattern 1 (never host the SDK in-proc; cross only via narrow IPC).

### Parallel (any wave): Formalize the Blender Boundary
**Rationale:** Pure documentation + reuse of existing readers; runs alongside any phase.
**Delivers:** Documented `.iff`/`.tre` format-version contract; open/preview verbs for Blender exports; cross-test `UtinniCoreDotNet` (C#) and `swg_iff` (Python) against shared golden fixtures.
**Avoids:** Pitfall 7 (the boundary is a *file-format seam*, not a shared authoring surface - DEC-A3 ratified).

### Phase Ordering Rationale

- **Hard dependency chain:** Phase 1 gates Phase 2 (can't wrap a tool that won't compile) -> Phase 2 gates Phase 3's build tools *and* unblocks OT Tier-2 -> Phase 3 gates the optional Phase 5.
- **Cheap-before-meaty:** revive+wrap and headless MCP are low-cost/high-leverage; editors are costlier (UI + new codecs) and sequenced after a stable base - they're *expensive*, not *blocked*.
- **Lowest-risk editor first:** WorldSnapshot (zero new deps, injection-native) precedes Terrain/Particle (new codecs).
- **Safety is design-time:** the MCP security contract (Pitfalls 1-3) is a Phase-3 first-class deliverable, not a later pass - over-broad tool shapes are un-retrofittable once agents depend on them.

### Research Flags

Phases likely needing `/gsd:plan-phase --research-phase <N>` during planning:
- **Phase 1 (revive spike):** the genuine unknown - actual dependency closure + per-tool build status. The research here is *empirical (a build pass)*, not literature; budget the spike accordingly.
- **Phase 4 (Terrain/Particle codecs):** `.trn`/`.prt` format depth is MEDIUM-confidence; `swg-client-v2` is the format-spec reference but no Utinni fixtures exist yet.
- **Phase 5 (live-injected MCP):** named-pipe-vs-socket IPC mechanism + reconciling modern-.NET host with x86 in-proc client - deferred, open.

Phases with standard patterns (skip research-phase):
- **Phase 2 (wrap as CLI verbs):** well-trodden DEC-C3 Tier-2 golden-fixture pattern; pure wrapping.
- **Phase 3 (MCP server):** HIGH-confidence - SDK shape, transport, and safety model are all verified; the thin-dispatcher pattern is settled.
- **Phase 4 (WorldSnapshot):** extends an existing panel on the unchanged MEF SubPanel seam; no new mechanism.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | MCP SDK runtime decision + revive build surface verified against current NuGet metadata *and* direct on-disk `swg-client-v2` `.vcxproj`/`.rsp` inspection. MEDIUM only for which editor codec lands first. |
| Features | HIGH | MCP model + safety grounded in MCP spec 2025-11-25 (Context7) + multiple security sources; compiler semantics from on-disk source census. MEDIUM for editor UX expectations (no Context7 anchor). |
| Architecture | HIGH | Existing topology read from source; the four integration forks resolved against verified SDK framework support; new editors reuse a shipped+demoed V1 seam. MEDIUM only on the v145 revive feasibility (the named Phase-1 spike). |
| Pitfalls | HIGH | Anchored to direct codebase + `swg-client-v2`-tree inspection (vcxproj toolset, dead include paths, build-output presence, active branches). MCP guidance MEDIUM but consistent across OWASP/MCP-spec/multiple sources. |

**Overall confidence:** HIGH

### Gaps to Address

- **Per-tool v145 build status (the prime gate):** `TemplateCompiler` likely-green (objs on disk), `TreeFileBuilder` unverified (no objs). Resolve empirically in Phase 1 - a build pass, not more reading. Fallback if a tool refuses v145: build *that* tool at v143 in `tools/` and still wrap it (the subprocess seam is toolset-agnostic; the lift-and-shift constraint forbids building *in* `swg-client-v2`, not building at v143 in our own tree).
- **Doc correction owed:** PROJECT.md "Toolchain bump (v145)" + `toolchain-inventory.md` revive note both overstate the port as a v143->v145 delta Utinni must author. The targets are already v145/stdcpp20. Flag for correction when v2.0 requirements are written.
- **The new SAVE verb:** the single biggest net-new code item for the MCP write surface; `Utinni.Cli` is read+roundtrip today. Specify its shape (per-format, loose-override-default, structured `{written,path,bytesWritten,backupPath,validated}` result) in Phase 3 requirements.
- **`resolvedRoot` provenance headless:** existing `LooseOverridePath.Resolve` requires a correct root, and TJT callers feed raw non-canonical paths. A headless MCP server has *no* injected client to harvest a root from - require explicit config, canonicalize once at startup, fail closed.
- **Lifted-from SHA + x64 watch:** `swg-client-v2` is churning on `koogie-msvc-cpp20-base` with a live `x64bit-Upgrade` branch. Record the exact x86 SHA; an upstream x64 migration collides with CON-P-02.
- **Blender `.rsp`/search-path manifest contract:** exact published-bundle location + discovery is deferred to the Blender-boundary work; `swg_pipeline/rsp_builder.py` is the reference.

## Sources

### Primary (HIGH confidence)
- On-disk `D:/Code/swg-client-v2`: `swg.sln` (VS2026/`VisualStudioVersion = 18.1`); `TemplateCompiler.vcxproj` (v145, `stdcpp20`, Win32, ~25 ProjectReferences, **built Debug objs present**); `sharedTemplate.vcxproj` (**dead** `perforce/include` path, not `#included`); `TreeFileBuilder.vcxproj` (v145, **no build output**); STLport453 **absent** from `external/3rd/library/`; per-target `.rsp` lib closures; git log (Phase-18 D3D9 to D3D11 churn; branches `koogie-msvc-cpp20-base`, `MSVC-CPP20-Upgrade`, `x64bit-Upgrade`). **Direct file evidence.**
- ModelContextProtocol on NuGet (`/1.3.0`, 2026-05-08) - target frameworks net8/9/10 + netstandard2.0, **no net4xx**; deps `Microsoft.Extensions.* >= 10.0.7`.
- MCP specification 2025-11-25 (Context7 `/websites/modelcontextprotocol_io_specification_2025-11-25`) - ToolAnnotations (+ "hints are advisory/untrusted" caveat), Tools/Resources/Prompts, Elicitation, stdio transport.
- `/modelcontextprotocol/csharp-sdk` (Context7 + GitHub) + .NET Blog "Build an MCP server in C#" - `Host.CreateApplicationBuilder` + `WithStdioServerTransport().WithToolsFromAssembly()` + `[McpServerTool]` bootstrap.
- C++ Team Blog "C++ Language Updates in MSVC v14.50" + MS Learn upgrade-issues - v145 conformance deltas (`/Zc:enumEncoding`, mandatory `template` keyword, `std::auto_ptr` removal `_HAS_AUTO_PTR_ETC`, two-phase lookup `/Zc:twoPhase-`); v140-v145 ABI-compatible.
- `swg-client-v2/docs/research/` - `swg-tools-and-likely-studio-toolchain.md` (653-line census: TemplateCompiler vs TemplateDefinitionCompiler, dependency map), `blender-mcp-vs-addon.md` (Utinni to Blender boundary, "format knowledge in shared lib / MCP as thin shell", "always IFF, Viewer is ground truth").
- Utinni repo: `docs/ai/architecture.md`/`toolchain-inventory.md`, `Utinni.Cli/**` (verb surface, headless pipeline, PE-probe-without-LoadLibrary), `UtinniCoreDotNet/Saving/{LooseOverridePath,TreRepackLock,TreBackupPath}.cs`, `.planning/phases/07-.../07-SECURITY.md`, `.planning/PROJECT.md`.

### Secondary (MEDIUM confidence)
- MCP write-safety / security patterns (multiple sources agree): human-in-the-loop elicitation, fail-closed destructive ops, read/write split, least-privilege scoping, annotation-driven UX vs server-side enforcement - Zeo, 4sysops, WRITER, Towards Data Science, PolicyLayer, OWASP (MCP Tool Poisoning), SOC Prime, isMalicious, MCP security best-practices.
- stdio-as-correct-local-transport / SSE-deprecated - apigene.ai, mcpcat.io, padiso.co.

### Tertiary (LOW confidence)
- DCC-editor UX expectations for Terrain/Particle/WorldSnapshot - training data + SWG tool census, no Context7 anchor; validate against `swg-client-v2` format specs during Phase-4 planning.

---
*Research completed: 2026-06-01*
*Ready for roadmap: yes*
