# Stack Research

**Domain:** v2.0 "AI-Assisted SWG Tools" — MCP server + revive/wrap SWG build-chain CLIs + first DCC-style SubPanels, layered onto the shipped Utinni V1 (x86 injected `UtinniCore.dll` + .NET FW 4.7.2 WinForms host + `UtinniCoreDotNet` codecs + `Utinni.Cli`).
**Researched:** 2026-06-01
**Confidence:** HIGH for the MCP-runtime decision and the revive-build surface (both verified against current NuGet metadata + the on-disk `swg-client-v2` tree); MEDIUM for the new-editor deps (depends on which editor lands first).

> **Scope note.** This file covers ONLY the NEW v2.0 capabilities. The existing V1 stack
> (UtinniCore C++ x86, CppSharp bridge, .NET FW 4.7.2 WinForms, MEF, vcpkg catch2/spdlog/imgui/imguizmo,
> VS2026/v145, self-hosted CI) is validated and out of scope — do NOT re-research or change it.

---

## Headline decisions (read these first)

1. **The MCP server must be a SEPARATE process targeting modern .NET (net10.0), NOT in-process in the
   .NET Framework 4.7.2 WinForms host, and NOT in the x86-injected DLL.** The official
   `ModelContextProtocol` SDK ships no `net4xx` target and transitively requires
   `Microsoft.Extensions.*` 10.x. It is testable/supported only on .NET 8+. (Verified — see below.)

2. **Transport = stdio.** Local, single-client desktop tool driving a local CLI/library. stdio is the
   canonical choice; HTTP/SSE adds a network stack and auth surface you don't need. (Verified.)

3. **The "v143 → v145 port" in the milestone framing is largely ALREADY DONE upstream.** The on-disk
   `swg-client-v2` revive targets are *already* at `PlatformToolset = v145`, `LanguageStandard = stdcpp20`,
   native MSVC STL (STLport dropped). This materially shrinks the revive-build effort and corrects an
   assumption in `PROJECT.md`/`toolchain-inventory.md` (which assumed swg-client-v2 sits at VS2022/v143).
   Lift-and-shift still applies for D3D11-churn decoupling — but you're lifting *already-modernized* source.

---

## Recommended Stack

### Core Technologies (NEW for v2.0)

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| **ModelContextProtocol** (C# SDK) | **1.3.0** (stable, 2026-05-08) | The MCP server: exposes Utinni's read+edit+save verbs as agent tools | Official Microsoft-collaborated SDK; `[McpServerTool]` attribute discovery + `WithStdioServerTransport()` is exactly the "thin shim over verbs" shape we want. Latest stable confirmed on NuGet. |
| **ModelContextProtocol.Core** | 1.3.0 | Minimal-dependency MCP primitives if you want to skip the `Microsoft.Extensions.Hosting` generic host | Use only if you deliberately avoid the generic host; the full `ModelContextProtocol` meta-package is the normal path. |
| **.NET runtime for the MCP server** | **net10.0** (LTS) | Host process for the MCP server | SDK targets net8/9/10 + netstandard2.0; net10.0 is current LTS, matches the `Microsoft.Extensions.* 10.x` the SDK pulls anyway, and avoids version-skew binding redirects. (net8.0 also fine if you want a wider-installed-base floor.) |
| **Microsoft.Extensions.Hosting** | 10.0.x (tracks SDK) | Generic host / DI / logging for the MCP server `Program.cs` | The canonical MCP server bootstrap (`Host.CreateApplicationBuilder` → `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`). Brought in transitively; pin to the SDK's floor. |
| **MSVC v145 toolset** (already Utinni's) | VS2026 / 14.5x | Compile the lifted-and-shifted revive CLIs | Same toolset Utinni already builds on; **and** the same toolset `swg-client-v2` already applied to these targets — so the lifted source compiles as-is, modulo path/lib fixups. |
| **C++20 (`/std:c++20`)** | — | Language standard for the revive CLIs | The upstream revive `.vcxproj`s are already `stdcpp20`; match it. Don't downgrade. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| **System.Text.Json** | (in-box on net10.0) | Parse the structured sorted-key JSON the existing `Utinni.Cli` verbs already emit | The MCP tool handlers shell out to `utinni-cli.exe` (or P/Invoke `UtinniCoreDotNet`) and re-surface its JSON. In-box; no extra package on net10.0. |
| **zlib** | (vendored in swg-client-v2 `external/3rd/library/zlib`) | TRE compression for `TreeFileBuilder` | Required lib for the TRE-build revive target. Lift the prebuilt `zlib.lib` alongside the source. |
| **PCRE 4.1** | (vendored, `external/3rd/library/pcre/4.1`) | Regex used by `TemplateCompiler` (`sharedRegex`) | Needed only by the `TemplateCompiler` revive target, not `TreeFileBuilder`. |
| **(Editors) ImGui + ImGuizmo** | (already vendored via vcpkg: imgui 1.92.8) | Terrain/Particle/WorldSnapshot SubPanel rendering if done as in-overlay HUD | Only if a DCC editor is built as an injected ImGui overlay rather than a WinForms SubPanel. Reuse the existing vcpkg deps; add nothing. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| `swg.sln` (swg-client-v2, VS2026) | Reference build for the revive targets | Open `src/build/win32/swg.sln` to see the already-v145 `.vcxproj`s. Copy the per-tool `.vcxproj` + source + needed `shared*` libs into a Utinni-owned `tools/` solution; do NOT build in-place. |
| MCP Inspector (`@modelcontextprotocol/inspector`) | Manual smoke of the MCP server over stdio | Lets you exercise tool list/call without wiring a full agent host. Fits the project's "invent a harness over manual smoke" preference. |
| Existing self-hosted CI (v145, VS2026) | Builds the revive CLIs + runs MCP server golden tests | Add a CLI-golden lane for the MCP tools mirroring the existing Tier-2 `Utinni.Cli.Tests` pattern. |

## Installation

```bash
# --- MCP server project (NEW, separate net10.0 process) ---
dotnet new console -n Utinni.Mcp -f net10.0
dotnet add Utinni.Mcp package ModelContextProtocol --version 1.3.0
dotnet add Utinni.Mcp package Microsoft.Extensions.Hosting   # tracks SDK's 10.0.x floor
# System.Text.Json is in-box on net10.0 — no package needed.

# --- Revive CLIs: NO package install ---
# Lift-and-shift: copy TreeFileBuilder/TemplateCompiler source + shared* libs + vendored
# zlib/pcre out of swg-client-v2 into a Utinni-owned tools/ tree; build with v145 (already applied upstream).
```

Minimal MCP server shape (the "thin shim"):

```csharp
// Program.cs (net10.0)
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace); // stdout is the protocol channel
builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();
await builder.Build().RunAsync();

[McpServerToolType]
public static class UtinniTools
{
    [McpServerTool, Description("Decode an .iff to structured JSON.")]
    public static string DecodeIff(string path) => RunCli("decode-iff", path); // shells out to utinni-cli.exe
}
```

---

## The MCP-server-runtime decision (the architecture fork — flagged explicitly)

**Verdict: separate `net10.0` process. NOT in-process in the WinForms host (net472). NOT in the injected x86 DLL.**

### Evidence

- The `ModelContextProtocol` 1.3.0 NuGet package lists target frameworks **net8.0 / net9.0 / net10.0 / netstandard2.0** — **no `net4xx` target**; NuGet's framework-compatibility table shows no .NET Framework support. (nuget.org, verified 2026-06-01.)
- Its dependencies are `Microsoft.Extensions.Caching.Abstractions >= 10.0.7`, `Microsoft.Extensions.Hosting.Abstractions >= 10.0.7`, `ModelContextProtocol.Core >= 1.3.0`. Those `Microsoft.Extensions.* 10.x` assemblies are built/tested for modern .NET; the SDK itself carries a `TimeProvider.Testing net472` warning-suppression, i.e. net472 is a known-rough edge, not a supported config.
- The SDK hit v1.0 (2026-03-05) and 1.3.0 (2026-05-08) targeting modern .NET; the canonical bootstrap is the `Microsoft.Extensions.Hosting` generic host.

### Why not force it in-process on .NET FW 4.7.2

The netstandard2.0 face means it *can technically restore* against net472, but you'd be fighting
`Microsoft.Extensions.* 10.x` binding redirects, shipping a config the SDK doesn't test, and bolting an
async generic host into a 32-bit WinForms message-pump that's already injected into a live game. High
fragility, zero upside.

### Why a separate process is actually *better* here (not just forced)

| Concern | In-proc (net472 host / x86 DLL) | Separate net10.0 process (recommended) |
|---------|--------------------------------|----------------------------------------|
| SDK support | Unsupported / binding-redirect hell | First-class, tested |
| Bitness | Locked to x86 (SWG constraint) | Free to be x64; no 32-bit memory ceiling |
| Crash isolation | An agent-driven tool fault could take down the injected session | MCP server crash can't touch the live game |
| Coupling to existing code | Tight | Loose — it shells out to the **already-shipped** `utinni-cli.exe`, the exact "thin shim" the milestone describes |
| Lifecycle | Tied to game/host lifetime | Independent; agent can drive it with SWG not even running (pure file edits) |

The existing `Utinni.Cli` is the perfect seam: the MCP server is a thin net10.0 wrapper that invokes
`utinni-cli.exe <verb>` and forwards its sorted-key JSON. No new codec code, no CppSharp, no x86.
**Tradeoff to accept:** any MCP tool that needs *live* in-client editing (live-patch / live-reload) can't
P/Invoke the injected DLL directly across the process boundary — it must go through a Utinni-side IPC
(named pipe / localhost) or be deferred. For v2.0's read+edit+**save-to-disk** pipeline that's a non-issue;
flag live-preview-over-MCP as a later increment.

---

## The revive build (lift-and-shift) — approach & surface

### Separate `.sln` vs CMake: use a **separate VS `.sln`**, not CMake

`swg-client-v2` builds with `swg.sln` / per-tool `.vcxproj` (MSBuild), already on v145. CMake would mean
re-authoring the build graph for ~6 shared static libs — pure cost. Lift the relevant `.vcxproj`s into a
new Utinni-owned `tools/Utinni.Tools.sln` and fix up relative paths. (Core3 uses CMake, but that's the
*server*, not these client tools.)

### Per-target shared-lib pull-in (verified from on-disk `.rsp`/`.vcxproj`)

| Revive target | External libs | Shared static libs (the lift-and-shift cargo) | Renderer? |
|---------------|---------------|-----------------------------------------------|-----------|
| **`TreeFileBuilder`** (source→`.tre`) | `zlib.lib` only | `sharedCompression, sharedDebug, sharedFile, sharedFoundation, sharedFoundationTypes, sharedIoWin, sharedMemoryManager, sharedSynchronization, sharedThread` + `fileInterface` | **No** — clean console tool |
| **`TemplateCompiler` / `TemplateDefinitionCompiler`** (`.tpf`/`.tpd`→`.iff` + param→type map) | `ws2_32, libpcre, zlib`, SOE `libclient/librpc/libsupp` | the above **plus** `sharedMath(+Archive), sharedObject, sharedRandom, sharedRegex, sharedTemplate, sharedTemplateDefinition, sharedUtility`; vendored `archive, localization, unicode, pcre/4.1`, and a **Perforce** include (`p4` in settings) | **No**, but heavier dep cone + a P4 vestige to stub/strip |

**Recommended order:** `TreeFileBuilder` first (smallest cone, unblocks build-from-source `.tre`), then
`TemplateCompiler` (heavier, but it's the one that yields the OT Tier-2 param→type map and the
`.tpf`/`.tpd` compile). Both are headless — no D3D/D3DX dependency to drag along (confirms the
toolchain-inventory "no renderer" claim).

### The realistic v143→v145 port surface (CORRECTED — much smaller than assumed)

The milestone docs assume you port a v143→v145 delta yourself. **On-disk evidence contradicts that:** the
revive `.vcxproj`s are *already* `v145` + `stdcpp20` + native MSVC STL. swg-client-v2 has already absorbed:

- **STLport 4.5.3 removal.** The `external/3rd/library/stlport453` dir is **absent from disk**; the
  `stlport` entry survives only as a dead line in legacy `.rsp` files. The modern `.vcxproj` uses native
  `<stdcpp20>` STL. (This was historically the single biggest 2003-era-SWG port risk — it's already done.)
- The general "2003 code on modern MSVC" modernization (the thing the milestone wanted to *borrow*).

So the **actual** remaining surface for Utinni is small and mechanical:
1. **Path/structure fixups** when lifting `.vcxproj`s out of the deep `swg-client-v2` tree into `tools/`.
2. **Strip/stub the Perforce vestige** in `TemplateCompiler` (don't drag `external/3rd/library/perforce`/Alienbrain into a sovereign build).
3. **Vendor the leaf externals** you actually use: `zlib`, `pcre/4.1`. (zlib only for TreeFileBuilder.)
4. **Watch the v14.5x conformance tightening** (real but narrow): `/Zc:enumEncoding`, mandatory `template`
   keyword on dependent template-ids, rejected ill-formed friend explicit specializations, constexpr
   overflow now rejected. These bite legacy code — but if upstream already compiles these targets at v145,
   they're already paid; budget only for path/lib drift.

> **Carry-over caution (from MEMORY):** the CppSharp/clang-11 STL-pin pain (`project-vs2026-cppsharp-block`)
> is a *CppSharp parser* problem and does NOT apply here — these revive CLIs are pure MSVC C++ with no
> CppSharp in the loop. Don't conflate the two.

---

## New-editor dependencies (Terrain / Particle / WorldSnapshot)

| Editor | Likely new dep | Notes |
|--------|----------------|-------|
| WorldSnapshot / object-placement | **None new** | Extends the existing Snapshot WinForms SubPanel; reuses `UtinniCoreDotNet` IFF/object-template codecs already shipped. Lowest-risk "replace" target — start here. |
| Terrain (`.trn`) | **None new** (format work, not lib work) | Needs a `.trn` codec in `UtinniCoreDotNet` (new format support, not a new third-party lib). Rendering reuses existing ImGui/imguizmo (vcpkg) if done as overlay, or WinForms GDI for a 2D heightmap. |
| Particle / client-effects (`.prt`, effect `.iff`) | **None new** | Same: codec work over existing IFF infra; preview reuses existing render path. |

**Net:** the editors need **format codecs inside `UtinniCoreDotNet`, not new NuGet/vcpkg packages.** Keep
the dependency surface flat. The `swg-blender-plugin` boundary stays a *file-format* contract (`.iff`/`.tre`),
not a runtime dependency — Utinni opens/previews what Blender's Python suite exports; no Python interop,
no shared process.

---

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| Separate `net10.0` MCP process | In-proc MCP on net472 host via netstandard2.0 + binding redirects | Never for v2.0. Only if a hard requirement forced single-process — then expect to fight `Microsoft.Extensions 10.x` redirects and run an unsupported config. |
| `net10.0` (LTS) for MCP server | `net8.0` | If you need the widest already-installed runtime floor on modder machines and don't want to bundle the net10 runtime. SDK supports both. |
| stdio transport | Streamable HTTP | Only if the MCP server must serve a *remote* agent or multiple concurrent clients. Not the local-desktop use case. SSE is deprecated — never pick SSE for new work. |
| Hand-written MCP tool handlers shelling to `utinni-cli.exe` | P/Invoke `UtinniCoreDotNet` directly from the MCP process | Use direct library calls if CLI process-spawn latency per tool-call becomes a bottleneck; costs you the clean shim boundary and re-introduces bitness coupling. Start with shell-out. |
| Separate VS `.sln` for revive CLIs | CMake | If you later unify with Core3's CMake world or want non-MSVC builds. Not worth it for v2.0 — upstream is already MSBuild+v145. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Hosting the MCP server inside the x86 injected `UtinniCore.dll` | Locks MCP to 32-bit, no SDK support, an agent fault could crash the live game | Separate net10.0 (x64) process shelling to `utinni-cli.exe` |
| MCP SSE transport | Deprecated in favor of Streamable HTTP; two-connection model, no resumability | stdio (local) |
| Building revive CLIs in-place against `swg-client-v2` | That tree has an active D3D9→D3D11 migration; in-place builds couple you to their churn (lift-and-shift is LOCKED) | Copy source + `shared*` libs into a Utinni-owned `tools/` solution |
| Re-doing a "v143→v145 port" from scratch | The targets are **already** v145/stdcpp20 upstream; you'd be redoing solved work | Borrow the already-modernized source; budget only path/lib/Perforce-stub fixups |
| Dragging STLport453 into the revive build | It's gone from disk; the modern `.vcxproj` uses native STL; the `.rsp` reference is dead | Native MSVC `/std:c++20` STL |
| Dragging Perforce/Alienbrain includes into `TemplateCompiler` | SOE-internal SCM vestige; irrelevant to a sovereign build | Stub/strip the P4 include; it's not needed for the compile transform |
| Adding Python interop for the Blender boundary | The Utinni↔Blender contract is file formats, not a runtime link | `.iff`/`.tre` file interchange only |

## Stack Patterns by Variant

**If the first MCP tools are read+edit+save-to-disk only (v2.0 baseline):**
- Separate net10.0 process shelling to `utinni-cli.exe`; SWG need not be running.
- Because all the codec/save logic already exists in `UtinniCoreDotNet` behind stable JSON verbs.

**If an MCP tool later needs live in-client editing (live-patch/reload):**
- Add a Utinni-side IPC endpoint (named pipe / localhost) the MCP process calls; do NOT move the MCP server into the injected DLL.
- Because crossing into the x86 injected session must stay an explicit, isolated boundary.

**If a DCC editor ships as an injected ImGui overlay rather than a WinForms SubPanel:**
- Reuse the existing vcpkg `imgui`/`imguizmo`; add no new render dep.
- Because the V1 overlay path (RT-space mapping) is already validated against the live client.

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| ModelContextProtocol 1.3.0 | net8.0 / net9.0 / **net10.0** / netstandard2.0 | **No net4xx target** — this is the load-bearing fact driving the separate-process decision. |
| ModelContextProtocol 1.3.0 | Microsoft.Extensions.Hosting.Abstractions >= 10.0.7, ModelContextProtocol.Core >= 1.3.0 | The 10.x floor is why net10.0 avoids binding-redirect skew vs. older runtimes. |
| Revive CLIs | v145 (VS2026) + `/std:c++20`, native MSVC STL | Already the upstream config on disk; v140–v145 are ABI-compatible (link with the newest). |
| Revive CLIs | `swg-client-v2` `shared*` static libs | Lift the matching libs; they're plain C++ console deps, no renderer. |

## Sources

- `/modelcontextprotocol/csharp-sdk` (Context7 resolve, reputation High) — official C# SDK identity/scope.
- https://www.nuget.org/packages/ModelContextProtocol/ and `/1.3.0` — version 1.3.0 (2026-05-08), target frameworks (net8/9/10 + netstandard2.0, **no net4xx**), deps `Microsoft.Extensions.* >= 10.0.7`. **HIGH.**
- https://github.com/modelcontextprotocol/csharp-sdk — package split (`.Core` / meta / `.AspNetCore`), maintained with Microsoft. **HIGH.**
- https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/ — canonical `Host.CreateApplicationBuilder` + `WithStdioServerTransport().WithToolsFromAssembly()` + `[McpServerTool]` bootstrap. **HIGH.**
- apigene.ai / mcpcat.io / padiso.co (2026) — stdio is the recommended transport for local single-client; SSE deprecated. **MEDIUM** (multiple sources agree).
- https://devblogs.microsoft.com/cppblog/c-language-updates-in-msvc-build-tools-v14-50/ — v14.50 (v145) conformance tightening: `/Zc:enumEncoding`, mandatory `template` keyword, friend-specialization diagnostics, constexpr overflow rejection. **HIGH.**
- learn.microsoft.com C++ binary-compat — v140–v145 ABI compatible; link newest. **HIGH.**
- On-disk `D:/Code/swg-client-v2`: `swg.sln` (`VisualStudioVersion = 18.1` / VS2026); `TreeFileBuilder.vcxproj` + `sharedFoundation` `.vcxproj` = **`PlatformToolset=v145`, `LanguageStandard=stdcpp20`**; STLport453 **absent** from `external/3rd/library/`; per-target `libraries.rsp`/`includePaths.rsp` (TreeFileBuilder→zlib only; TemplateCompiler→pcre/zlib/SOE libs + Perforce vestige). **HIGH — direct file evidence.**

---
*Stack research for: Utinni v2.0 AI-Assisted SWG Tools (MCP server + revive/wrap CLIs + first DCC SubPanels)*
*Researched: 2026-06-01*
