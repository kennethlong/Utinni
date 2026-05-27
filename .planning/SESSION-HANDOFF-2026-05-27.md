# Session Handoff — 2026-05-27 — Phase 7 (TRE Browser) mid-execution

**Command in flight:** `/gsd-execute-phase 7` — running **ALL INLINE** (user chose "All inline" — do NOT spawn gsd-executor subagents; the orchestrator executes each plan directly because the executor lacks PowerShell/MSBuild and this phase is cross-repo + has human checkpoints).

## Where we are

Phase 7 = `.planning/phases/07-tjt-subpanel-tre-browser-read-only/`, 6 plans across 5 waves.

| Plan | Status |
|------|--------|
| 07-00 fixtures | ✅ complete (SUMMARY written, tracking updated) |
| 07-01 reader + facade | ✅ complete (SUMMARY written) |
| 07-02 TRE Browser shell | ✅ complete (live smoke **approved**) |
| **07-03 detail pane** | **auto-work DONE + committed; AWAITING the human-verify live-smoke checkpoint (Task 4). User is verifying now.** |
| 07-04a decoders + decode-iff CLI | ⏳ pending (autonomous, Utinni-only) |
| 07-04b deep decoders + structured views | ⏳ pending (cross-repo + final human smoke) |

### IMMEDIATE NEXT ACTION
The user is doing the **07-03 live-SWG smoke** (reinject + open TRE Browser, check metadata + IFF chunk tree with real `@offset` + hex peek).
- **If they reply "approved":** finalize 07-03 → write `07-03-SUMMARY.md`, run `state.advance-plan` / `state.update-progress` / `state.record-metric --phase 07 --plan 03` / `roadmap.update-plan-progress 07`, commit metadata (`docs(07-03): …`), then start **07-04a**.
- **If they report an issue:** read `D:\Code\Utinni\bin\Release\utinni.log` (grep `[TreBrowser]`) — I can read it directly. Common WinForms gotchas already solved (see below).

When finalizing 07-03 SUMMARY, record these **deviations**: (a) path reconstruction uses `pn.FullPath` from the trie, not a `TreeNode.Parent` walk (cleaner, correct); (b) the planned v5000 "layout not yet verified" encrypted-banner string was omitted because **5000 is now readable** (see discovery below) — `ShowEncrypted` keeps a `meta.Version` branch but uses truthful copy.

## 🔑 Critical discoveries / operational facts (do not re-learn these)

1. **5000 = THE readable SWGEmu Pre-CU format** (NOT enumerate-only — reverses planning assumption D-06b). The live client `D:\SWGEmu-Client\SWGEmu` is 100% `EERT5000` (53 archives). Layout: size-first HEADER + **crc-first 24-byte record stride** (6000's field order minus the 8-byte pad) + zlib (compressor=2) blocks. Reader fixed in commit `d75c701` (`IsCrcFirst(V5000)=true`, `IsEnumerateOnly(V5000)=false`, pad only for 32-byte V6000). Browser now enumerates **125,572 paths**. Memory `project_tre_version_support_gap.md` updated.

2. **Build with MSBuild, NOT `dotnet build`.** `dotnet build` fails `MSB3823` on UtinniCoreDotNet/TJT WinForms image resources. MSBuild: `D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`. Pattern: build with MSBuild (`/p:Configuration=Release /p:Platform=x86 /t:Build`), then run tests with `dotnet test <proj> -c Release -p:Platform=x86 --no-build`. (Memory: `feedback_dotnet_build_msbuild_resources.md`.)

3. **TJT forms must load from the CONSTRUCTOR**, not the `Shown` event — `Shown` does NOT fire for forms shown inside SWG's injected message loop. Pattern: `StartLoad()` in ctor → `await Task.Run(heavyWork)` → apply to UI on the await continuation (no `Control.Invoke` needed there).

4. **Client `.tre` dir** resolves from `Process.GetCurrentProcess().MainModule` directory (the SWG install root), NOT `utility.GetWorkingDirectory()` (which is `GetCurrentDirectory` — the CWD).

5. **Diagnostics:** `D:\Code\Utinni\bin\Release\utinni.log` (native spdlog) — readable directly from the build tree; the TRE Browser logs `[TreBrowser] …` lines (dirs tried, path count, failures). Window title is the always-visible status channel (UtinniForm.OnPaint draws `Text`; call `Invalidate()` after setting it).

6. **WinForms layout:** add the `Dock.Fill` content control FIRST, then `Dock.Top`/`Bottom` edge controls. SplitContainer: set `Size` before `SplitterDistance` in hand-written Designer (else ctor throws → fails the plugin's MEF load). Wrap each plugin form's ctor in try/catch + Log in `Plugin.cs` so one form can't take down the whole toolbox.

## Build / test / deploy reference

- **Repos:** Utinni `D:\Code\Utinni` (this repo, `.planning/` lives here); TJT `D:\Code\UtinniPlugins\The Jawa Toolbox\TheJawaToolboxDotNet` (sibling — **old-style csproj, new `.cs` files need explicit `<Compile Include>`**; standing authority to commit/push).
- **Build order:** Utinni first (TJT references `..\..\..\Utinni\bin\$(Configuration)\UtinniCoreDotNet.dll`).
- **Deploy = build output.** Runtime root `D:\Code\Utinni\bin\Release\`; TJT plugin → `D:\Code\Utinni\bin\Release\Plugins\TheJawaToolbox\` (TJT `OutputPath` points there). User injects from `bin\Release`. "Redeploy" = just rebuild; no copy needed.
- **Test status (all green):** Utinni.Cli.Tests 82 passed / 1 skipped (SearchTOC fixture-gated); UtinniCoreDotNet.Tests 155 passed.
- **Test filters used:** `--filter "FullyQualifiedName~ParseTre|...~TreFixtureBuilder|...~CotMasterIndex|...~TreArchiveIndex|...~ListObjects|...~InspectIff|...~TreFile"`.
- **Fixtures:** committed under `Utinni.Cli.Tests/Fixtures/tre/`; regenerate via env-gated generator: `GSD_EMIT_FIXTURES=1 dotnet test Utinni.Cli.Tests ... --no-build --filter EmitCommittedFixtures` (writes to the source tree), then rebuild to copy to bin.

## Commits (all LOCAL — nothing pushed yet; standing push permission exists, held during iteration)

**Utinni repo (`git log`):**
- `d75c701` fix(07-01): 5000 readable crc-first-24
- `feat(07-03)` IffChunk.OffsetBytes (after d75c701; check `git log --oneline`)
- 07-00/07-01/07-02 feat + docs commits; 07-02 metadata commit
- ⚠️ `UtinniCoreDotNet/Generated/UtinniCore.cs` is **modified in the working tree (pre-existing, NOT ours)** — never stage it.

**UtinniPlugins repo:** `740b677` shell, `339158e` SplitContainer fix, `5b0b72e` dir-resolution, `d81b400` title-status, `b789bbb` ctor-load, + `feat(tre-browser): detail pane …` (07-03). All on `master`.

## Remaining plan scope (read the PLAN.md files for detail)

- **07-04a** (autonomous, Utinni-only, dotnet-testable): `Formats/Decoders/` — DataTableDecoder, StringTableDecoder, ObjectTemplateDecoder (bounded posture, no recursive inherited-field walk), `DecoderException`; `decode-iff` CLI verb + golden tests. Pure consumers of `Formats/Iff` output.
- **07-04b** (cross-repo + final human smoke): AppearanceSummary (mesh/skeleton/anim) + IffStructureSummary (shader/UI-page); extend `decode-iff`; render row-capped structured views in `TreDetailPane.pnlStructured` (the placeholder Panel already stubbed in 07-03).

## Known/accepted cosmetic issues (not blockers)
- TRE Browser bottom-left status/legend labels are cramped (state mirrored in title + log). 07-03's detail pane reworks the right region; left-panel label tidy can come with 07-04b.
- `cbTypeFacet` shows only "All types" — intentional V1 stub (plan-scoped).

## Task tracker (TaskCreate IDs 1–6 = the 6 plans): #1–3 completed, #4 (07-03) in_progress, #5–6 pending.
