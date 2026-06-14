# AGENTS.md — Utinni Project Runbook

Tool-agnostic operating guide for any coding agent (Claude Code, Codex, Cursor, …) or human
contributor working in this repo. Claude-specific mechanics live in [`CLAUDE.md`](./CLAUDE.md), which
references this file. Hard-won engineering lessons live in [`docs/ai/lessons.md`](./docs/ai/lessons.md).

## What this is

Utinni is an in-process modding tool + framework for Star Wars Galaxies (SWG): a **32-bit (x86)** native
`UtinniCore.dll` injected into a live `SWG.exe`, a **.NET Framework (net4.7.2) WinForms** editor host
(The Jawa Toolbox), and an **MEF-discovered C# plugin** pipeline. As of v2.0 it also ships a Utinni-owned
`tools/` tree of revived SOE build CLIs (`utinni-cli`) and a headless **net10** `Utinni.Mcp` stdio MCP
server. v2.1 (current) adds Wave-2 editors (Terrain, one effects editor) on a hardened render/toolchain
base. See `.planning/PROJECT.md`, `.planning/ROADMAP.md`, `.planning/MILESTONES.md`.

## Repo layout (orientation)

| Path | What |
|------|------|
| `UtinniCore/` | Native C++ injected core (`swg::*` RE shim → `utinni::*` façade → D3D9 hook + ImGui overlay). x86. |
| `UtinniCoreDotNet/` | CLR bridge (CppSharp-generated `Generated/UtinniCore.cs`) + `Formats/{Iff,Tre,…}` codecs. net4.7.2. |
| `UtinniCoreDotNetGen/` | Build-time CppSharp binding generator. Produces `Generated/UtinniCore.cs`. |
| `TheJawaToolbox*/` | The WinForms editor host + `IEditorPlugin` SubPanels (all Wave-1/2 editors ship inside TJT). |
| `tools/` | Revived SOE build CLIs (`TreeFileBuilder`/`TemplateCompiler`/…) + `Utinni.Cli` verbs. v145. |
| `Utinni.Mcp/` | Headless net10 stdio MCP server. Shells `utinni-cli`; owns ZERO format logic. |
| `docs/ai/` | Plain-markdown reference docs (grep/AI-friendly). HTML mirror under `docs/`. |
| `.planning/` | GSD planning corpus (ROADMAP, STATE, phases, research, milestones, todos). |
| `D:/Code/UtinniPlugins/` | **Sibling repo** (separate). Canonical plugins; builds copy into `Plugins/<name>/`. |
| `D:/Code/swg-client-v2/` | **Read-only reference corpus** — SWG asset-format spec source. No runtime dep. |

## Build & test

> **Use MSBuild, not `dotnet build`.** `dotnet build` fails on the WinForms `.resx` image resources
> (MSB3823). Build the solution with VS2026 MSBuild; run xUnit with `dotnet test --no-build`.

- **Managed build:** VS2026 MSBuild on `Utinni.sln` (`/p:Configuration=Release /p:Platform=x86`).
- **C# tests:** `dotnet test --no-build` (xUnit: `UtinniCoreDotNet.Tests`, `Utinni.Cli.Tests`).
- **Native tests:** Catch2 suite in `UtinniCore.Tests` (built via MSBuild).
- **Tools:** `tools/Utinni.Tools.sln` builds the revived CLIs at **v145 / Win32**.
- **`Generated/UtinniCore.cs` churn:** CppSharp reorders this file on every build — a huge symmetric
  no-op diff. **Always `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs`; never commit it.**

## Toolchain

- **VS 2026 (Dev18)** is the default — `D:\Program Files\Microsoft Visual Studio\18\Community`.
  VS 2022 remains on disk as fallback. PlatformToolset is **v145** (v144 skipped).
- **x86 / 32-bit** throughout the injected stack (target process is 32-bit `SWG.exe`).
- **vcpkg manifest mode** for native deps (catch2, spdlog, imgui 1.92.x, imguizmo). The D3D11 overlay
  path uses imgui's `dx11-binding` feature — no new dependency.
- **CppSharp 0.10.5 (clang 11)** generates the bridge; its *parser* is redirected to the VS2019 14.29
  STL while the *build* uses v145 (no released CppSharp ships a clang new enough for v145's STL — see
  `[[project_vs2026_cppsharp_block]]` and the v2.1 foundation-phase decision record).
- **Resolved tool paths:** `gh`, `gsd-sdk` in `C:\Users\kenne\bin` (on PATH); MSBuild, clang-format,
  vswhere, dotnet under the Dev18 install. See `docs/ai/` and `[[reference_windows_toolchain_paths]]`.

## CI

- Runs on a **self-hosted runner** on the maintainer's machine (`C:\actions-runner`) — v145/VS2026 is
  Insiders-only, not on GitHub-hosted images. **Push-only trigger.** Verify-only toolchain steps.
- Gates master: the managed test lanes + the native Catch2 suite + the `tools/` build lane.
- **`Debug/` gitignore trap:** the `Debug/` rule case-insensitively swallows lifted
  `tools/.../src/shared/debug/` source on Windows → C1083 in CI (local green). Don't reintroduce it.

## Build-wave constraints (important)

- **Worktrees are OFF for this repo** (`.planning/config.json` `workflow.use_worktrees=false`). A fresh
  worktree lacks `vcpkg_installed/` → a 20–40 min cold rebuild, and the executor has no PowerShell/MSBuild
  path there. **Run C++ build waves INLINE on the main tree.**
- Live-SWG verification (inject + eyeball in WinForms) is a **maintainer-only human checkpoint** — no
  headless/subagent path exists. Cross-repo paired commits do NOT need a checkpoint; only the live smoke does.

## Locked invariants — do not silently override

Product scope (anti-goals, all LOCKED — `DEC-A1..A4`):
- NOT a server-side mod manager (SWG-Source / swg-main own that).
- NOT a launcher / patcher (SWGEmu + community launchers own that).
- NOT a Maya/3ds Max replacement — DCC mesh/anim/texture authoring is **Blender's lane**
  (`swg-blender-plugin`). Utinni's appearance preview is **live-in-client via the real engine**, never a
  standalone renderer.
- NOT a multiplayer-cheat enabler (all editing is local/offline).

Architecture (LOCKED in v2.0):
- **DEC-V2-LIFT-SHIFT** — revive third-party build tools by copying source + shared libs into the
  Utinni-owned `tools/` tree at v145, pinned to an exact `swg-client-v2` SHA. Never `#include`/
  ProjectReference into the live upstream tree (it has an active D3D9→D3D11 migration).
- **DEC-V2-VERBS-FIRST** — every capability lands as a golden-tested `utinni-cli` verb FIRST; the MCP
  layer is a thin dispatcher with zero business logic. Named exceptions only.
- **DEC-V2-MCP-OOP** — the MCP server is a separate net10 stdio process; never host the MCP SDK in the
  net472/x86 injected client. The named pipe IS the boundary.
- **DEC-C4** — Wave-1/2 editors ship as `IEditorPlugin` SubPanels INSIDE The Jawa Toolbox, not as
  separate plugins.

Code-safety classes (from `docs/ai/assessment.md`, `CON-H-*`):
- `DllMain` must do NO heavy startup (no `LoadLibrary`/CLR bring-up in `DLL_PROCESS_ATTACH`); defer to an
  exported `utinni_init` / first SWG callback.
- Pattern-scan results are null-checked before use; hard-coded RVAs have a single `UTINNI_API` source.
- Callback subscriber lists are snapshotted under lock before dispatch; every `Add` has a symmetric
  `Remove`; cross-CRT allocations free with the originator's allocator.

The 24 `CON-N/M/T-*` preservation elements (`.planning/intel/constraints.md`) are do-not-refactor without
explicit justification in the plan.

## Format support reality

- **TRE:** support BOTH SWGEmu `0004/0005/0006` AND newer Restoration `5000/6000/COT2000` (the user mods
  both clients). `5000` recognized but no fixture/spec yet; **`v6000+` payloads are encrypted →
  enumerate-only**. `swg-client-v2` is the reference impl. See `[[project_tre_version_support_gap]]`.
- **Byte-exact round-trip** is the codec gate — assert across both SWGEmu and Restoration fixtures.
- Datatable/STF `.iff` chunks are NOT word-padded (real SWG); the IFF reader detects the pad.

## Reference index

- `docs/ai/toolchain-inventory.md` — the ~60-tool SWG cross-walk + Wave-2 editor priority order
  (Terrain → Effects → Animation → Shaders → Sound → UI) + the SIE comparison.
- `docs/ai/assessment.md` — code-quality audit + the `CON-*` constraint origins.
- `docs/ai/{build,injection,core,bridge,callbacks,plugin-framework,regen-bindings}.md` — subsystem docs.
- `docs/ai/lessons.md` — distilled hard-won engineering lessons (rendering, injection, WinForms, SWG-RE, build).
- `.planning/codebase/{ARCHITECTURE,STRUCTURE,…}.md` — codebase map (read-only context).
