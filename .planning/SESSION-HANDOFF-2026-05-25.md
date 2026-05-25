# Session Handoff: 2026-05-25 — imgui 1.92.8 live smoke PASSED + 2 latent bugs fixed; Wave 3 next

> Picks up from `SESSION-HANDOFF-2026-05-24-NIGHT.md`, whose top carry-forward was: *"Live SWG imgui smoke REQUIRED before relying on the [1.92.8] bump."* **This session did that smoke — it PASSED** — and along the way the demo-window probe exposed two PRE-EXISTING latent bugs in the overlay, both now fixed, committed, and CI-green. Master advanced `38601c9` → `157b017` (1 fix commit + this handoff). The vcpkg migration's last open risk is now closed.

---

## TL;DR

- **imgui 1.92.8 live-injected smoke: PASSED.** Verified in a live SWGEmu session via a temporary `ImGui::ShowDemoWindow` probe (`g_showDemoWindowProbe`): render (fonts/styling), mouse buttons (click/drag/wheel), and **keyboard/text input** all work. The riskiest change — `hkWndProcHandler` rewritten to `ImGui_ImplWin32_WndProcHandler()` — is correct; a coordinate-space diag confirmed `io.MousePos == ScreenToClient`.
- **Found + fixed 2 pre-existing bugs** (NOT bump regressions — 1.76 had the same latent defects), in one commit **`157b017`**:
  1. **Embedded-window mouse offset + right-edge dead zone (Issue #10).** The SWG window is reparented/resized into the editor panel, but its D3D9 backbuffer is never `Reset` (deliberate — `[[feedback-d3d9-reset-third-party]]`), so windowed `Present` stretches backbuffer(1280×1024) → client(1455×1040). imgui ran in client space while drawing into the smaller backbuffer → cursor drifted right, proportional to x (~12% at the right edge), and the right ~12% was unreachable. **Fix:** run imgui entirely in **render-target space** — set `io.DisplaySize` to the render target AND scale the cursor into it via `io.AddMousePosEvent`. No-op when client == render target.
  2. **Begin/End imbalances (3 sites).** `DrawDepthWindow`/`DrawColorWindow` omitted the mandatory `End()`; `render()`'s `"Tests"` `End()` ran unconditionally though its `Begin()` is gated on `!enableUi` (so with `enableInternalUi=true` in `bin/Release/ut.ini` it popped the window stack too far). 1.76 corrupted silently; 1.92 error-recovery surfaced it as on-screen "**Calling End() too many times!**". Balanced all three.
- **CI GREEN** — run `26407430824` on the self-hosted v145 runner, Build(Release|x86)+Test(net472), 6m16s, success.
- **Memory:** updated `[[project-vcpkg-migration-complete]]` (smoke DONE/PASSED); added `[[feedback-imgui-embedded-d3d9-rt-space]]` (RT-space mapping rule + the imgui 1.87+ `AddMousePosEvent` gotcha).

---

## The two key learnings (reusable)

### imgui 1.87+ is event-queue based — a direct `io.MousePos` poke is clobbered
`ImGui_ImplWin32_NewFrame()` only **queues** the raw mouse-pos event; `io.MousePos` isn't written until `ImGui::NewFrame()` drains the queue. So setting `io.MousePos` between the two backend calls is silently overwritten (this cost a rebuild cycle this session). Inject a corrected position with `io.AddMousePosEvent(x, y)` **after** the backend call (last-event-wins). `io.DisplaySize`, by contrast, is set **directly** by the backend, so overriding it there sticks. Same 1.87+ shift that removed `io.KeysDown[]`/`AddInputCharacter`.

### Embedded-D3D9 overlay must operate in render-target space
When the host (SWG) window is resized but its swapchain isn't (`Reset` avoided), `Present` stretches backbuffer→client. imgui can only draw into the backbuffer, so map BOTH layout (`DisplaySize`) and input into render-target space, then let the present-stretch do the rest. Diagnose visual-vs-hittest offsets by drawing a crosshair at `io.MousePos` (foreground draw list) and comparing to the OS cursor — that crosshair pinned the bug in one observation after several wrong geometric models.

---

## What this session did NOT change

- No production behavior beyond the two fixes. The demo-window probe / crosshair / coordinate-log were all temporary smoke instrumentation, fully removed; `g_showDemoWindowProbe` is back to `false` (its call site retained for future Wave-1 styling work, per its original comment).
- No dependency or build changes. The 1.92.8 bump itself was the prior session (`d71feb2`).

---

## State checkpoint

```
Branch:        master
HEAD:          157b017  fix(imgui): RT-space mouse mapping for embedded window + balance Begin/End pairs
Prev:          38601c9  docs(state): SESSION-HANDOFF-2026-05-24-NIGHT
Origin sync:   yes (157b017 pushed)
Working tree:  clean except untracked .planning/imgui-192-live-smoke.md (smoke checklist; commit with this handoff or leave)
CI:            GREEN at 157b017 (run 26407430824, self-hosted v145, 6m16s)

Self-hosted runner:
  name:  poppops-windows-utinni @ C:\actions-runner
  state: was ONLINE this session (picked up the push) — SESSION-TIED, dies when the session ends

Phase 6 progress:
  06-01 ✅  06-02 ✅  06-02b(vcpkg per-dep) ✅  imgui-smoke ✅ (this session)
  06-03 ⬜ READY (STAB-05)   06-04/05/06 ⬜ blocked-by-deps
```

---

## ⚠️ Watch items / carry-forward

| Item | Detail |
|---|---|
| **Runner is session-tied** | Restart `C:\actions-runner\run.cmd` if a pushed run queues. For durability: reconfigure as a Windows service from an admin shell (`svc.cmd install`). See `[[project-self-hosted-ci]]`. |
| **CI annotations (non-fatal, pre-existing)** | (1) Node.js 20 deprecation on `actions/cache@v4` / `checkout@v4` / `setup-msbuild@v2` — GitHub forces Node 24 **June 2 2026**, removes Node 20 **Sept 16 2026**; bump those action versions eventually. (2) "Failed to save cache" `tar.exe exit code 2` on the `C:\Program Files` path — vcpkg cache just isn't persisted; cosmetic. Neither fails the build. |
| **`.planning/imgui-192-live-smoke.md`** | The smoke checklist doc (now executed + passed). Untracked. Folding it into this handoff commit. |
| **Chat-open D3D9 fullscreen** | `.planning/debug/chat-open-d3d9-fullscreen.md` — still queued, untouched (carried). |
| **`[[project-loader-lock-harness-ci-flake]]` may be stale** | Dedicated runner ≠ shared contention; re-evaluate only if it recurs. (Carried.) |

---

## What's next — Wave 3 / 06-03 (STAB-05)

`06-03-PLAN.md` exists and is READY to execute. It closes the **last two STAB-05 open questions**:

- **CON-O-08 — DXSDK June 2010 removal.** Replace the sole `D3DXVECTOR3` use in `UtinniCore/swg/graphics/depth_texture.cpp` with a local 3-float struct, then strip DXSDK include/lib paths from **every** `.vcxproj`. Side-effect: structurally closes CON-B-03. Once it lands, the CI **"Verify DirectX SDK"** step can be deleted too.
- **CON-O-06 — LeksysINI replacement.** Hand-roll a ~200-LOC INI parser inside `UtINI`'s PIMPL `Impl` (in `UtINI/utini.cpp`), **preserving the existing public ABI** so all 15+ callsites across Launcher/UtinniCore/UtinniCoreDotNet stay untouched.
- **Catch2 "fences"** — both changes are guarded by Catch2 regression tests per `[[feedback-max-harness]]`: **12+ Catch2 INI fence cases** (round-trip parse/write, sections/keys, comments, malformed input) that pin the new parser's output to LeksysINI's so the swap provably can't drift; plus a small struct fence for the depth_texture change. (A "fence" here = a regression test that locks behavior so a future change can't silently break it — same pattern as the CON-N-09 spdlog OutputSink fence from 06-02.)

### Kickoff (after `/clear`)
```
# 1. Confirm runner online (CI is push-triggered):  C:\actions-runner\run.cmd   (if needed)
# 2. Confirm green baseline:  gh run list --branch master --limit 1
# 3. Execute Wave 3:  /gsd-execute-phase 6 --wave 3
#    (06-03 is already planned — 06-03-PLAN.md. If the orchestrator prefers, /gsd-progress will route.)
```

> Build recipe reminder (this machine): vcpkg isn't bootstrapped by default. `vcpkg_installed/` already exists from prior sessions; if it's gone, re-run the bootstrap+install from `SESSION-HANDOFF-2026-05-24-NIGHT.md`. MSBuild: `D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`. **Build via PowerShell, not Bash** — Bash mangles `/m` `/p:` switches (MSYS path conversion). The CppSharp post-build churns `UtinniCoreDotNet/Generated/UtinniCore.cs` (~5674± reordering) on every full build — `git checkout --` it before committing unrelated work.

---

*Session closed: 2026-05-25. imgui 1.92.8 live smoke PASSED end-to-end; two pre-existing overlay bugs (embedded mouse-mapping offset + Begin/End imbalances) found via the demo probe and fixed in `157b017`; CI green. The editor-panel overlay is now genuinely usable — accurate clicks full-width, full reach, no error spam. Wave 3 (06-03 STAB-05) is planned and ready.*
