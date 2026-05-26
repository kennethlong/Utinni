# 06-06 — Maintainer-Signed Tier-4 UAT (pre-tag, D-24)

> **STATUS: AWAITING MAINTAINER SIGN-OFF.** This file is a scaffold prepared by the
> 06-06 orchestrator after Tasks 1–3. All results below are `[ PENDING ]` until the
> maintainer hand-walks each scenario. Per D-24 + [[feedback-max-harness]], CI-green is
> necessary but NOT sufficient — fill in PASS/FAIL, replace the TESTING.md
> `Last-verified SHA` placeholders with this commit's SHA, then commit as
> `docs(06-06): Tier-4 UAT signoff + TESTING.md last-verified SHAs (pre-tag)`.

**Plan:** 06-06 (Phase 6 close, v1.0.0-rc.1 cut)
**Verification date:** 2026-05-25
**Maintainer signature:** Verified by Kenneth Long (kenny.alan.long@gmail.com), 2026-05-25 — Tier-4 UAT signed off (D-24); v1.0.0-rc.1 cleared to push.

## Machine Identifiers

- **Dev machine OS:** [ PENDING — e.g. Windows 11 Home 10.0.26200 ]
- **Dev machine GPU vendor + driver:** NVIDIA GeForce RTX 3060, driver 32.0.15.9186 (2026-01-19)
- **Clean VM OS:** N/A — no clean VM available at rc.1 time; live MSI round-trip deferred to bake (see Deferred items). MSI validated non-invasively via `msiexec /a`.
- **Virtualization platform:** N/A (deferred)

## Pre-tag build artifacts validated by the orchestrator (automated, before this UAT)

- Tasks 1–3 committed: `ebad483` (Tier-4 doc), `c2d989c` (WiX MSI scaffold), `0f563de` (release.yml), `04f1e75` (release.yml TJT-path fix).
- Local **no-TJT** MSI build: PASS → `installer/bin/x86/Release/Utinni.msi` (1.87 MB).
- Local **TJT-bundled** MSI build (the release.yml path, `-p:IncludeTjt=true`): PASS → 1.90 MB; MSI File table contains 19 files incl. `TheJawaToolbox.dll` + `TheJawaToolboxDotNet.dll`.
- TheJawaToolbox built from `kennethlong/UtinniPlugins@c9cfa9d01417bea772142136b69ec333dd30fa3f` (the pinned SHA in release.yml).

## Tier-4 Scenario Results (a)–(h)

| Scenario | Result | Notes (maintainer) |
|----------|--------|--------------------|
| (a) Imgui overlay Demo screen (menus/sliders/buttons/tabs/plots/popups/drag-drop) | **PASS** | Demo screen rendered end-to-end over live SWG; all 7 widget categories OK. (Initial miss was a stale probe=false build, not a regression — d3d9 hooks + imgui setup/render confirmed healthy in utinni.log; rebuilt UtinniCore Release x86 with g_showDemoWindowProbe=true.) |
| (b) PanelGame.WndProc forwarding (alphanumeric/arrows/Tab → SWG chat) | **PASS** | Keystrokes (alphanumeric/arrows/Tab) reach SWG; chat receives input, no keystrokes swallowed by the WinForms host. |
| (c) hkPresent + MMO render lifecycle (3+ scene transitions, single-cycle callbacks) | **PASS** | 3+ scene transitions; utinni.log shows one clean setup/cleanup cycle per transition, callbacks fire once, no allocator-fragmentation crash. |
| (d) D3D9 device-loss/reset (alt-tab / resolution change; no D3DERR_INVALIDCALL) | **PASS** | Win+L lock → unlock triggered SWG's device Reset (hkReset → ImGui_ImplDX9 Invalidate/Create); overlay recovered and re-rendered cleanly, no D3DERR_INVALIDCALL/fatal in utinni.log. (Windowed alt-tab/minimize does not lose the device; in-game Options route unavailable since Esc=untarget in SWG, not gameMenuActivate.) |
| (e) Plugin loader vs TJT (loads as panel, no utinni.log exceptions) | **PASS** | TheJawaToolbox loaded as a panel in the editor host; no exceptions in utinni.log, no MEF compose failure. (Dev-build path; re-confirmed against MSI-installed Plugins\TheJawaToolbox\ in the clean-VM task.) |
| (f) Drag-drop + WinForms STA (FormObjectBrowser → world panel, commit on drop) | **PASS** | Editor drag-drop into the world panel works (away from the right-edge cursor-clip dead zone, which is deferred — see Deferred items). |
| (g) GPU-driver-specific (Nvidia + Intel/AMD if available; else DEFER to bake) | **PASS (Nvidia) / DEFERRED (2nd vendor)** | Overlay render + device-loss recovery verified on NVIDIA GeForce RTX 3060 (driver 32.0.15.9186) via scenarios (a)-(d). No Intel/AMD hardware available locally → second-vendor coverage explicitly deferred to the rc bake (see Deferred items). |
| (h) WinForms UI smoke (open/resize/min/restore every form; FlaUI EXCLUDED) | **PASS** | Editor forms open and behave on resize/minimize/restore; no UI hangs or unhandled exceptions. (FlaUI automation excluded per CON-TT-03 — manual smoke walk.) |

## MSI install / uninstall round-trip (clean VM)

Use the TJT-bundled MSI: `installer/bin/x86/Release/Utinni.msi` (built locally with `-p:IncludeTjt=true`).

No clean VM was available at rc.1 time. The MSI was validated **non-invasively** via an
administrative-install extraction (`msiexec /a`, exit 0) on the dev machine; the live
install/uninstall round-trip on a pristine VM is **deferred to the rc bake** (see Deferred items).

| Check | Result | Notes |
|-------|--------|-------|
| MSI well-formed (package opens, lays out payload) | **PASS** | `msiexec /a` administrative install exit 0; File table = 19 files. |
| Payload tree correct (Launcher.exe, UtinniCore/UtinniCoreDotNet/UtinniCore-Symbols .dll, ut.ini, utinni.cfg, licenses.txt, Icons\, Plugins\TheJawaToolbox\*.dll) | **PASS** | Extracted tree mirrors `PFiles\Utinni\...` incl. both TJT DLLs. |
| `ut.ini` ships BLANK swgClientPath (CON-D-01); no leaked local path | **PASS** | Extracted `ut.ini` swgClientPath/swgClientName blank; `utinni.cfg` login fields blank. Harvests `data/` (blank), not `bin/Release` (dev). |
| Install runs on clean VM; SmartScreen accepted (unsigned, D-23) | **DEFERRED → bake** | No clean VM at rc.1 time. |
| Start Menu "Utinni" shortcut launches Launcher.exe | **DEFERRED → bake** | Requires live install. |
| Opt-IN checkbox seeds `[Launcher]swgClientPath` when one SWG client present | **DEFERRED → bake** | Requires live install + SWG present. |
| Uninstall removes folder + `HKCU\Software\Utinni`; reinstall clean | **DEFERRED → bake** | Requires live install/uninstall. |

## UAT Findings & Fixes (caught during this Tier-4 pass)

Per [[feedback-max-harness]], CI-green is not sufficient — this hand-walk caught a real
overlay input bug that no automated lane covers:

- **Overlay drag-leak → SWG marquee-select.** Dragging inside an imgui window (repro: text
  selection in the Demo "Basic Inputs" `InputText`; also slider/window-move/drag-drop)
  leaked to SWG and triggered the game's marquee drag-select with a cursor jump whenever the
  cursor strayed outside the window mid-drag. Root cause: `render()` keyed `DirectInput::suspend`
  on `ImGui::IsWindowHovered(AnyWindow)` (drops the instant the cursor exits) instead of
  `io.WantCaptureMouse` (stays true for the whole drag). **Fixed in `061f4ad`** — verified
  live (drags now stay captured, no marquee-select). CI green on master.

## Deferred items (bake period follow-up)

- **Embedded overlay right-edge cursor-clip dead zone (deferred to rc-bake / Wave-1).** When the
  editor panel is wider than SWG's backbuffer (e.g. maximized → panel 1455 vs backbuffer 1280),
  the OS cursor is confined to the left ~1280px and the rightmost ~175px is cursor-dead (cursor +
  any imgui window-drag stop there). The SWG *image* fills the panel correctly. Root cause
  (confirmed live via a one-shot render() diag: `rt=1280x1024 clientRect=panelWxH, DisplaySize ==
  clientRect`): **SWG clips the OS cursor to its backbuffer rect via its own ClipCursor — there is
  no ClipCursor in UtinniCore — while Utinni stretches the reparented window to the panel.** The
  imgui RT-space mouse mapping is correct; the limit is the OS cursor clip. NOT a regression from
  06-06 and not a crash. Fix candidates (bake/Wave-1): detour/override SWG's ClipCursor to the
  stretched rect (or release it while the overlay has capture); or reparent at native backbuffer
  size + letterbox; never Reset the third-party device. Captured in memory `swg-cursor-clip-stretch-deadzone`.
- **Second GPU vendor (Intel/AMD) deferred to rc-bake.** Only NVIDIA hardware (RTX 3060) is available locally; Tier-4 scenario (g) was verified on Nvidia. Intel/AMD overlay + depth-resolve coverage will be picked up during the bake period if such hardware becomes available. Optional extra confidence on Nvidia: tick "Show Depth Window" in the Tests panel to exercise the RESZ depth-resolve path.
- **MSI live install/uninstall round-trip on a clean VM (deferred to rc-bake).** No clean VM was available at rc.1 time. The MSI was validated non-invasively (`msiexec /a` admin-install extraction: well-formed, correct payload tree incl. TJT, CON-D-01 blank configs). The live round-trip on a pristine Win10/11 VM — UAC install + SmartScreen, Start Menu shortcut, opt-in SWG-path detector, uninstall registry/folder cleanup, clean reinstall — is a bake-period item.

## Final Disposition

**All 8 Tier-4 scenarios (a)-(h) PASS** on the dev machine (NVIDIA RTX 3060). The MSI is
well-formed and CON-D-01 compliant (validated via `msiexec /a` extraction). Two real overlay
bugs were caught by this UAT and fixed CI-green before the tag: the drag-leak (`061f4ad`) and the
minimize off-screen reposition (`29a128e`). Deferred to the rc bake (not blockers for a pre-release):
(1) the embedded right-edge cursor-clip dead zone, (2) second GPU vendor coverage, (3) the live MSI
install/uninstall round-trip on a clean VM.

**Verified code state: master `29a128e`** (this sign-off commit is doc-only; code is identical).

**v1.0.0-rc.1 tag clear to push.** Maintainer signed off 2026-05-25 (see signature above).

## Post-tag entry (Task 5, after the tag push)

**v1.0.0-rc.1 tag pushed 2026-05-25** (annotated, at sign-off commit `6f51b26`). `release.yml`
run 26429522655 completed **success** on the self-hosted utinni-v145 runner: built Release|x86 +
TheJawaToolbox (pinned `kennethlong/UtinniPlugins@c9cfa9d`), bundled TJT into the MSI, and
published the GitHub Release.

Verified:
- `gh release view v1.0.0-rc.1 --json isPrerelease` → **true** (Pre-release).
- Asset **`Utinni.msi`** attached.
- Release URL: https://github.com/kennethlong/Utinni/releases/tag/v1.0.0-rc.1

The cross-repo TJT build + stage + IncludeTjt MSI path (validated locally pre-tag) held in CI on
a clean checkout. **Bake period begins (N≈10 days); promotion to v1.0.0 final is post-phase per D-22.**
Bake follow-ups: right-edge cursor-clip, second GPU vendor, live MSI clean-VM round-trip (see Deferred items).
