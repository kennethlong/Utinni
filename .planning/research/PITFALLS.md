# Pitfalls Research

**Domain:** AI-assisted (MCP) modding-tool authoring pipeline + legacy-C++ tool revival for Star Wars Galaxies (Utinni v2.0)
**Researched:** 2026-06-01
**Confidence:** HIGH (codebase + swg-client-v2 tree inspected directly; MCP guidance MEDIUM, verified across OWASP/MCP-spec/multiple sources)

> Scope note: These are pitfalls of **adding the v2.0 features to *this* shipped system**, not generic project mistakes. Each is anchored to Utinni's existing defenses (the byte-exact writers, `LooseOverridePath`, `TreRepackLock`, the Phase-7 threat register) and to the concrete state of the `swg-client-v2` revive tree as it exists on disk today.

---

## Critical Pitfalls

### Pitfall 1: An AI agent writes a syntactically-valid but semantically-wrong asset into a live, shared archive

**What goes wrong:**
The MCP server exposes `save`/`repack` over the byte-exact writers. The byte-exactness guarantees the *bytes round-trip* — it does **not** guarantee the agent picked the right record, the right `.tre`, or that the new payload is a coherent game asset. An agent "fixing" a datatable can blow away a column, repack the live `*.tre`, and corrupt the only copy. Because Utinni's `TreWriter`/`IffWriter` are *correct*, the corruption is a perfectly well-formed archive — no parser will flag it.

**Why it happens:**
MCP tools default to "non-read-only, potentially destructive, non-idempotent, open-world" unless annotated otherwise (MCP spec). A thin shim over `Utinni.Cli` verbs inherits the CLI's "do exactly what argv says" contract, which is fine for a human typing one command and catastrophic for an agent looping. The existing path defenses (`LooseOverridePath`, Phase-7 T-07-05) stop *path traversal* but say nothing about *wrong-but-in-bounds* writes.

**How to avoid:**
- **Default every write tool to loose-override, never in-place repack.** Utinni already has the four-tier save matrix (loose-override → Save/Save-As → live-patch → `.tre` repack). The MCP write surface should expose only the **loose-override tier** by default — it writes a sidecar file the client overlays, leaving the source `.tre` byte-for-byte untouched, so any agent mistake is trivially reversible by deleting the override. Gate `.tre` repack behind a separate, explicitly-annotated, off-by-default tool.
- **Mark tools with MCP annotations** (`readOnlyHint`, `destructiveHint`, `idempotentHint`). Read verbs (`parse-tre`, `inspect-iff`, `list-objects`) → `readOnlyHint: true`. `repack` → `destructiveHint: true`, requiring host confirmation.
- **Make destructive tools require a `dry_run` round-trip first** that returns a structured diff (records touched, byte-delta, columns changed) the agent/host must echo back before the real write executes. This is policy-as-code, not prompt-hoping.
- **Always write through `TreBackupPath`** (already exists) on any repack so there is a `.bak` to restore.

**Warning signs:**
MCP tool list where read and write share a verb; a `save` tool with no `dry_run` sibling; the agent able to reach repack without a distinct confirmation hop; no backup file appearing next to a repacked archive.

**Phase to address:** MCP server phase (the centerpiece) — write-tool **safety contract** must be a first-class deliverable, not a later hardening pass. Author a dedicated `MCP-SECURITY.md` threat register mirroring Phase-7's `07-SECURITY.md`.

---

### Pitfall 2: The MCP write surface inherits an over-broad scope — one tool that can write anything, anywhere

**What goes wrong:**
A single `write_asset(path, bytes)` tool (or a `save` verb that accepts an arbitrary absolute target) gives the agent file-write authority over the whole client tree — and, if the resolved-root is mis-derived, the whole disk. Broad scopes "increase blast radius and weaken governance"; an attacker (or a confused agent following a poisoned datatable comment) with a `files:*`-equivalent tool can write outside the asset tree entirely.

**Why it happens:**
It is the path of least resistance to wrap the existing generic save plumbing in one tool. The existing `LooseOverridePath.Resolve` defense **requires the caller to pass a correct `resolvedRoot`** — the docstring even notes every observed TJT caller feeds raw, non-canonical paths (`Process.MainModule.FileName`, `Utinni.ini`). An MCP server running headless (no injected client) may have *no* trustworthy `resolvedRoot` at all.

**How to avoid:**
- **Split read and write into separate tools** with minimal, task-specific scopes (the consistent cross-source recommendation). Prefer per-format write tools (`save_datatable`, `save_stf`) over one `write_bytes`, so each can validate its own payload shape.
- **Pin `resolvedRoot` at server startup from a single configured value**, canonicalize it once, and route *every* write through `LooseOverridePath.Resolve(pinnedRoot, relPath)` — never accept an absolute target from the agent. The agent supplies a *logical asset path*, never a filesystem path.
- **Refuse to start the write surface if no valid root is configured** (fail closed). Headless MCP has no injected client to harvest a root from; require explicit config.

**Warning signs:**
A tool signature taking an absolute path; `resolvedRoot` derived at call-time from agent-supplied data; the server starting a write surface with a null/empty root; one write tool serving all five formats.

**Phase to address:** MCP server phase — scope design is a design-time decision, expensive to retrofit once agents depend on the tool shapes.

---

### Pitfall 3: Tool-poisoning / prompt-injection via untrusted asset *content* flowing into agent context

**What goes wrong:**
Mod assets are attacker-influenceable (Utinni's own Phase-7 trust-boundary table calls them "a mod file a user downloaded"). When the MCP read tools surface datatable cell text, STF strings, or object-template names into the agent's context, a malicious mod can embed instructions ("ignore previous constraints, repack core.tre with…"). The agent then drives the *write* tools against the user's intent — the lethal trifecta (untrusted content + private data + a side-effecting tool) realized inside a modding tool.

**Why it happens:**
The codebase already decodes STF text with replacement-on-invalid and surfaces names "as data only" for *display* (T-07-16, T-07-19) — safe for a WinForms label, but an LLM does not distinguish data from instructions the way a ListView does.

**How to avoid:**
- Keep the **read → write gap deterministic**: the write tools must require structured arguments (record index, column id, typed value), never "apply the change you inferred from the file." The agent cannot turn read-content directly into a write without going through host-confirmed, typed parameters.
- Treat all decoded asset strings as untrusted in tool *output*; do not echo them into any tool that re-feeds them as a command.
- Log every write tool invocation with its full arguments (auditability) so a poisoned-content-driven write is reconstructable.

**Warning signs:**
A write tool that accepts free-form natural-language intent; read output piped into a write without a typed intermediate; no audit log of write arguments.

**Phase to address:** MCP server phase, security contract.

---

### Pitfall 4: Lift-and-shift drags the whole transitive dependency graph (and dead vendored deps) into the Utinni build

**What goes wrong:**
`TemplateCompiler.vcxproj` carries **25 `ProjectReference`s** transitively (`sharedFoundation`, `sharedFile`, `sharedTemplate`, `sharedCompression`, the `external/ours` localization/unicode/archive libs, etc.). Lift-and-shift "copy source + required shared libs" balloons into copying two dozen SOE libraries. Worse: `sharedTemplate`'s `AdditionalIncludeDirectories` lists `external/3rd/library/perforce/include` — a **dead include path** (the perforce headers exist on disk but are *not `#included`* by any `sharedTemplate` source). Blindly copying the vcxproj into a lift location where that directory doesn't exist breaks the build for a dependency that isn't actually used.

**Why it happens:**
The clean-sounding "it's just a headless console tool" hides that SWG's `sharedFoundation` is the everything-library. The include paths are encrusted with 2003-era studio-infra references (Perforce, regex, localization) that are inert but present.

**How to avoid:**
- **Spike the dependency closure first.** Before committing to lift-and-shift, run a build of `TemplateCompiler` and chase its actual `ProjectReference` + real `#include` closure (not the advertised include paths). Distinguish *referenced* libs from *dead include-path* entries — prune the latter (perforce, alienbrain) on copy.
- **Prefer building the revived tools by ProjectReference against a *pinned, read-only checkout* of swg-client-v2's libs** rather than physically copying 25 projects — *if* that can be done without coupling to the renderer. If physical copy is mandated, copy the minimal closure and strip dead include dirs in the same commit.
- **Build the dependency manifest as a deliverable** (which exact libs, which are real). This is the "revive feasibility spike" PROJECT.md already flags.

**Warning signs:**
A copy step that grabs whole `external/3rd`; build errors about a missing `perforce`/`alienbrain` include dir (means you copied a dead path); the lib count climbing past ~6–8 (you've grabbed the rendering/networking transitive set you don't need).

**Phase to address:** **Revive-feasibility spike phase (first authoring phase, before any wrap work).** Output = a verified dependency manifest per tool.

---

### Pitfall 5: Assuming the v143→v145 modern-STL port is unsolved — when swg-client-v2 has *already done it for the headless tools*

**What goes wrong:**
PROJECT.md frames the work as "borrow swg-client-v2's modernization, then port the v143→v145 delta ourselves," anticipating CppSharp-style clang-11/STL pain. But inspection shows **`TemplateCompiler.vcxproj` is already `<PlatformToolset>v145</PlatformToolset>` with `LanguageStandard>stdcpp20`, and has built Debug objects on disk.** The upstream `koogie-msvc-cpp20-base` branch already carried the SOE code to v145 + C++20 for at least this tool. The pitfall is **redoing work that's done** — or, inversely, **assuming all tools are equally done** when `TreeFileBuilder.vcxproj` is also v145 but has **no build output** (unverified).

**Why it happens:**
The toolchain-inventory doc honestly marks build status "unverified"; the easy assumption in either direction (all broken / all working) is wrong. The real distribution is mixed per-tool.

**How to avoid:**
- **Per-tool build-status verification is the spike's first task.** Treat "is at v145 in the vcxproj" and "actually compiles+links at v145" as different facts. `TemplateCompiler` = likely-green (objs present); `TreeFileBuilder` = unverified (no objs); MayaExporter = known-dead (Maya 7 + Alienbrain).
- **The genuine v145 deltas are well-understood and narrow** when they appear: `std::auto_ptr` removal under `/std:c++17+` (re-enable transitionally via `_HAS_AUTO_PTR_ETC`), two-phase name lookup under `/permissive-` (escape hatch `/Zc:twoPhase-`), and stricter conformance. These are *source tweaks*, not the clang-11 STL-parser wall that blocked CppSharp — that wall was specific to CppSharp's *vendored clang parsing MSVC's STL*, which the revived **native** tools do not involve. **Do not conflate the two**; the CppSharp block does not predict native-tool revival difficulty.
- Pin to the **same `/std` swg-client-v2 used** (c++20) to inherit their conformance fixes rather than fighting a different standard level.

**Warning signs:**
A plan that budgets weeks for "STL modernization" without first attempting a build; treating the CppSharp v145 block as evidence the tools won't compile; assuming `TreeFileBuilder` is green because `TemplateCompiler` is.

**Phase to address:** Revive-feasibility spike phase. The spike's pass/fail gate is "tool X compiles + links + round-trips a known `.tpf`/`.tre` at v145," per tool.

---

### Pitfall 6: Coupling the Utinni build to a moving swg-client-v2 checkout mid-D3D11-migration

**What goes wrong:**
swg-client-v2 is **actively churning**: the on-disk checkout is on branch `koogie-msvc-cpp20-base` (not `master`), recent commits are Phase-18 D3D9↔D3D11 seam fixes and `D3DXCompileShader` SEH guards, and there are live branches `d3d9-fixb-d3dcompile-wip`, `MSVC-CPP20-Upgrade`, `x64bit-Upgrade`. If Utinni builds *in-place* against this tree (or pins to a branch HEAD), a rebase/force-push or a renderer refactor upstream silently breaks Utinni's tool build — even though the revived tools are headless and don't touch the renderer.

**Why it happens:**
It's convenient to `#include` straight from the sibling tree. The shared libraries (`sharedFoundation` et al.) are shared between the headless tools *and* the renderer-side code, so renderer-driven edits to a shared header ripple into the tool build.

**How to avoid:**
- **Honor the locked lift-and-shift constraint literally**: copy source into a **Utinni-owned, version-pinned** location; never `ProjectReference` or `#include` across into the live swg-client-v2 working tree. Record the **exact source commit SHA** you lifted from.
- **Lift from a tag/SHA, not a branch HEAD.** `koogie-msvc-cpp20-base` is a branch — pin the SHA so an upstream force-push can't move it under you.
- Re-sync deliberately (a tracked "upstream pull" task), never ambiently.
- Watch the **`x64bit-Upgrade` branch specifically**: if swg-client-v2 migrates to x64, lifted source from an x86 era and Utinni's hard x86 constraint (CON-P-02) diverge — the revived tools are currently `Win32`/x86, matching Utinni, but only on this branch.

**Warning signs:**
A vcxproj with `..\..\swg-client-v2\...` relative paths reaching outside Utinni's repo; build green today, red tomorrow with no Utinni-side change; lifted-from SHA not recorded anywhere.

**Phase to address:** Revive-feasibility spike phase (establishes the lift mechanism) + every subsequent wrap phase (re-affirms no in-place coupling).

---

### Pitfall 7: Scope creep into 3D mesh/skeleton/animation authoring that belongs to Blender

**What goes wrong:**
The new DCC-style editors (terrain, particle, world-snapshot) and "Animation live-in-client preview" sit one slip away from re-implementing mesh/skel/anim *authoring* — which is the locked anti-goal DEC-A3 (Utinni is NOT a Maya/3ds Max replacement) and is already owned, in-progress, by `swg-blender-plugin` (`export_skeletal.py`, `export_animation.py`, `export_static.py`, the `.msh/.mgn/.skt/.lod/.pob/.sat/.ans` formats). "Preview the animation" quietly becomes "tweak the animation," then "author the animation," then a forked, half-baked DCC.

**Why it happens:**
The line between *preview* and *edit* is genuinely fuzzy in a live-injected tool — once you can show a skeleton you can be tempted to drag a joint. Terrain and particle editors are legitimately Utinni's (binary/format + live-injection), but they border the mesh/material world.

**How to avoid:**
- **Encode the boundary as a one-sentence test per editor**: *does a human manipulate 3D mesh/skeleton/animation geometry?* → Blender. *Is it binary-format edit + live-preview of an already-authored asset?* → Utinni. (This mirrors the toolchain-inventory's revive-vs-replace litmus.)
- For Animation: Utinni's deliverable is **live-in-client preview of a Blender-exported anim**, explicitly *read-only on the geometry*. No joint/keyframe editing UI.
- **Formalize the boundary as an interface contract**: Utinni opens/previews what Blender exports; the meeting point is the file formats (`.iff`/`.tre`), not a shared authoring surface. Write it down where both repos can see it.

**Warning signs:**
A terrain/particle/anim editor spec growing a "vertex"/"joint"/"keyframe" edit verb; UI requests to "just let me nudge the mesh"; new code parsing `.msh`/`.mgn`/`.skt` for *write*; overlap with a `swg_blender` exporter.

**Phase to address:** Each new-editor phase (terrain, particle, world-snapshot, anim-preview) — gate the editor's design contract on the preview-vs-author test. The ecosystem/boundary-formalization phase ratifies it.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| One generic `write_asset(path, bytes)` MCP tool | Fastest shim over existing CLI | Over-broad scope; blast radius = whole disk; un-retrofittable once agents depend on it | **Never** for the write surface |
| Expose `.tre` repack as a default MCP write tool | "Authoring is complete" demo | One agent loop corrupts the live shared archive | Only behind a distinct off-by-default, `destructiveHint`+`dry_run`-gated tool |
| `ProjectReference`/`#include` directly into swg-client-v2 tree | No copy step | Breaks on every upstream D3D11/x64 churn; violates locked lift-and-shift | **Never** (constraint is locked) |
| Copy whole `external/3rd` + all 25 project refs on lift | "Just make it build" | Drags dead perforce/alienbrain paths + renderer-adjacent libs; bloated build | Only as a throwaway spike to *measure* the real closure, then prune |
| Skip the per-tool build-status spike; assume v145-in-vcxproj == green | Saves the spike phase | `TreeFileBuilder` (no objs) surprises mid-wrap; wasted wrap work on an unbuildable tool | Never — the spike is cheap and the inventory explicitly flags status unverified |
| Reuse Phase-7 path defenses as the *whole* MCP safety story | Path-traversal already solved | Path defenses don't stop wrong-but-in-bounds writes or scope/poisoning | Never as the complete story; they are one layer |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| MCP host ↔ Utinni write tools | Auto-approving all tools from a "trusted" server | Annotate per-tool; only `readOnlyHint` tools auto-approve; `destructiveHint` always confirms |
| MCP server ↔ live SWG client | Repacking a `.tre` the running client holds open | Use the existing `TreRepackLock.Probe` (FileShare.None probe) → refuse + recommend loose-override on `SharingViolation` (already built for Phase 8) |
| Revived CLI ↔ swg-client-v2 shared libs | Lifting headless tool but pulling renderer-coupled shared headers | Verify the headless tools don't transitively need renderer libs; they're console + don't link the renderer — keep it that way |
| Utinni ↔ swg-blender-plugin | Both repos re-implementing `.iff`/`.tre` parsing and drifting | Treat the file format as the contract; align on byte-exact round-trip; `swg_iff` (Python) and `UtinniCoreDotNet` (C#) are independent impls of the *same* spec — cross-test against shared golden fixtures |
| Revived tool bitness | Assuming x64 to match a modern host | Tools are `Win32`/x86 today, matching Utinni's x86 SWG.exe (CON-P-02); the upstream `x64bit-Upgrade` branch would break this — pin to the x86 SHA |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Agent looping read→edit→repack on a real `.tre` | Each repack rewrites/recompresses a multi-MB archive; minutes per loop; disk churn | Default to loose-override (sidecar write, no repack); batch repack as an explicit terminal step | As soon as an agent iterates more than a few times |
| MCP read tools surfacing a 213k-node TRE listing into agent context | Token blowup, slow tool calls | Reuse Phase-7's lazy/bounded enumeration + caps (T-07-06, `FilterMatchCap`); paginate tool output | Real client `.tre` set (200k+ files) |
| Decompression-bomb / over-allocation from adversarial mod feeding the MCP read path | Memory spike / OOM in the server process | The existing T-07-01/02/13 division-form + inflate-cap guards already apply — ensure the MCP path goes *through* `UtinniCoreDotNet`, not around it | Crafted hostile mod file |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Write tools without MCP destructive/read-only annotations | Host auto-approves a destructive repack | Annotate every tool; default-deny posture matches MCP spec (unannotated == destructive) |
| Deriving `resolvedRoot` from agent-controlled input at call time | Path escape outside the client tree | Pin + canonicalize root once at server start; route all writes through `LooseOverridePath.Resolve` |
| No audit log of write-tool invocations | Poisoned-content-driven write is unreconstructable | Log tool name + full typed args for every write; human-reviewable trail |
| Echoing decoded asset strings back into a side-effecting tool | Tool-poisoning / prompt injection drives writes | Keep read-output and write-input separated by typed, host-confirmed parameters |
| Trusting byte-exactness as a *safety* property | Well-formed-but-wrong archive corrupts content silently | Byte-exactness is correctness, not safety; layer dry-run diff + backup + loose-override default on top |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Agent silently repacks the user's only copy of an asset | Irreversible data loss, no undo | Loose-override default (deletable), `TreBackupPath` `.bak` on repack, dry-run diff surfaced to the human |
| Misclassifying a readable asset as "encrypted" to the agent | Agent gives up on a v5000 asset it could edit | Already solved for the UI (T-07-18); ensure the MCP read path inherits the same v5000-readable / v6000-enumerate-only classification |
| A terrain/particle editor that looks like a Blender replacement | Users expect mesh editing Utinni won't deliver; scope confusion | Keep editor scope visibly format/preview-bound; document the Blender handoff in-product |

## "Looks Done But Isn't" Checklist

- [ ] **MCP write surface:** Often missing the `dry_run` sibling and `destructiveHint` annotation — verify every mutating tool has both before exposing it.
- [ ] **MCP server root:** Often missing fail-closed-on-no-root — verify the server refuses to start the write surface without a configured, canonicalized `resolvedRoot`.
- [ ] **Revived tool:** Often "compiles" but never round-trip-verified — verify it produces a byte-identical (or game-loadable) `.iff`/`.tre` from a known source, not just that it links.
- [ ] **Lift-and-shift:** Often missing the recorded source SHA + pruned dead include paths — verify the lifted-from commit is pinned and `perforce`/`alienbrain` include dirs are removed.
- [ ] **`TreeFileBuilder` revive:** Often assumed green from `TemplateCompiler` — verify it independently (no build objects on disk today).
- [ ] **New editor:** Often missing the preview-vs-author boundary statement — verify each editor's contract passes the "does a human manipulate 3D geometry?" test.
- [ ] **Repack safety:** Often missing the live-client lock probe on the MCP path — verify `TreRepackLock.Probe` runs before any agent-driven repack, not just the WinForms one.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Agent corrupted a `.tre` via repack | LOW (if defended) / HIGH (if not) | Restore from `TreBackupPath` `.bak`; if loose-override was the default, just delete the sidecar — source `.tre` never touched |
| Lift-and-shift pulled renderer/dead deps | MEDIUM | Re-run the dependency-closure spike; prune to the real `#include` set; strip dead include dirs |
| Built against moving swg-client-v2 branch, now broken | MEDIUM | Re-pin to the recorded SHA; convert in-place refs to copied-in source per the locked constraint |
| Editor crept into Blender's authoring domain | MEDIUM–HIGH | Excise the authoring verbs; redefine as preview-only; defer authoring to swg-blender-plugin |
| Over-broad MCP write tool already shipped | HIGH | Split into per-format scoped tools; agents/configs referencing the old tool must migrate — expensive once depended upon |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| 1. Agent corrupts live archive | MCP server phase (security contract) | Write tools default to loose-override; repack is separately gated + dry-run + backup |
| 2. Over-broad write scope | MCP server phase (tool design) | Read/write split; per-format tools; root pinned at startup; fail-closed on no root |
| 3. Tool-poisoning via asset content | MCP server phase (security contract) | Typed write args only; write-invocation audit log present |
| 4. Lift drags transitive/dead deps | Revive-feasibility spike (first authoring phase) | Verified per-tool dependency manifest; dead include paths pruned |
| 5. Redoing / misjudging v143→v145 port | Revive-feasibility spike | Per-tool "compiles+links+round-trips at v145" gate; CppSharp block explicitly not used as predictor |
| 6. Coupling to moving swg-client-v2 | Revive-feasibility spike + every wrap phase | No cross-repo `#include`/ProjectReference; lifted-from SHA recorded |
| 7. Scope creep into 3D authoring | Each new-editor phase + boundary-formalization phase | Each editor passes the preview-vs-author test; no `.msh`/`.skt` *write* code |

## Sources

- Utinni codebase (direct inspection): `UtinniCoreDotNet/Saving/LooseOverridePath.cs`, `TreRepackLock.cs`, `TreBackupPath.cs`; `.planning/phases/07-.../07-SECURITY.md` threat register (T-07-01..T-07-19); `.planning/PROJECT.md`; `docs/ai/toolchain-inventory.md`.
- swg-client-v2 tree (direct inspection): `TemplateCompiler.vcxproj` (v145, `stdcpp20`, Win32, 25 ProjectReferences, built Debug objs); `sharedTemplate.vcxproj` (dead `perforce/include` path, not `#included`); `TreeFileBuilder.vcxproj` (v145, no build output); `MayaExporter.vcxproj`; git log (Phase-18 D3D9↔D3D11 churn, `koogie-msvc-cpp20-base`/`x64bit-Upgrade` branches).
- swg-blender-plugin tree: `swg_blender/export_{skeletal,animation,static}.py`, `swg_iff/` — confirms 3D-authoring ownership.
- [MCP Security Best Practices](https://modelcontextprotocol.io/docs/tutorials/security/security_best_practices) — read/write split, least privilege, explicit consent. (MEDIUM)
- [MCP tool annotations: securing against the lethal trifecta – 4sysops](https://4sysops.com/archives/mcp-tool-annotations-securing-mcp-servers-against-the-lethal-trifecta/) — unannotated tools default to destructive; `readOnlyHint`/`destructiveHint`. (MEDIUM)
- [MCP Tool Poisoning | OWASP](https://owasp.org/www-community/attacks/MCP_Tool_Poisoning) — untrusted content as injection vector. (MEDIUM)
- [MCP Security Risks: Tool Poisoning, Prompt Injection | isMalicious](https://ismalicious.com/posts/mcp-security-risks-tool-poisoning-ai-agents) — lethal trifecta, agent attack surface. (MEDIUM)
- [Model Context Protocol: Security Risks & Mitigations | SOC Prime](https://socprime.com/blog/mcp-security-risks-and-mitigations/) — over-broad scope blast radius; split read/write. (MEDIUM)
- [C++ Language Updates in MSVC Build Tools v14.50 | C++ Team Blog](https://devblogs.microsoft.com/cppblog/c-language-updates-in-msvc-build-tools-v14-50/) — v14.5x (v145) conformance changes. (HIGH)
- [Overview of potential upgrade issues (Microsoft C++) | MS Learn](https://learn.microsoft.com/en-us/cpp/porting/overview-of-potential-upgrade-issues-visual-cpp) — `std::auto_ptr` removal (`_HAS_AUTO_PTR_ETC`), two-phase lookup (`/Zc:twoPhase-`), conformance drift. (HIGH)

---
*Pitfalls research for: Utinni v2.0 AI-Assisted SWG Tools (MCP write-safety + legacy-C++ revival + DCC-editor boundary)*
*Researched: 2026-06-01*
