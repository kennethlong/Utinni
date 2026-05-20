# Session Handoff: 2026-05-19 (Night)

> Written 2026-05-19 night to wrap a session that closed the three-session "main thread halts in `EB FE` at `0x0131DC7A`" investigation. Supersedes `SESSION-HANDOFF-2026-05-19-PM.md` for what's next. SWG now boots all the way to the login screen + intro under Utinni injection — first time in this codebase that's worked since the d3d9 saga began.

## TL;DR

The morning handoff's Issue #6 (jmp-self halt at `0x0131DC7A`) is **resolved**. Root cause was **Utinni's own Launcher** writing `EB FE` to SWGEmu's PE entry as a stall mechanism while UtinniCore.dll was injected — the matching restore code never ran because of an `INFINITE` wait on a thread that blocks for the editor's lifetime. CODEX's contribution: identifying the missing `FlushInstructionCache` calls that explained the inter-session variance.

**Status**: Editor + SWG render side both work. Scene load reaches `hkSetScene` with a valid scene pointer. Five atomic commits pushed (`dad9845..20fbad5` on master). Follow-up issues identified for next session — none blocking the core breakthrough.

## What we shipped this session

| Commit | Layer | What |
|---|---|---|
| `dad9845` | DetourXS + cui_hud | Dropped the bad `, 5` explicit length parameter from `cui_hud.cpp:166`'s `DETOUR_TYPE_PUSH_RET` (needs 6 bytes; passing 5 caused a 1-byte heap overflow at `pbPatchBuf[5] = 0xC3` AND the `ret` opcode never made it into the target). Added defensive guard `if (detLen < minDetLen) return nullptr;` in the explicit-length path of `external/DetourXS/detourxs.cpp` to prevent the bug-class from recurring. Bug present since 2020 upstream commit `57b6b7e`. |
| `488ff0c` | Launcher | Replaced `WaitForSingleObject(hInitThread, INFINITE)` (deadlocked behind `Application.Run`) with a named-event wait on `Local\UtinniReady_<pid>` (manual-reset, 30s timeout). Allocates remote memory for the event-name C string and passes the remote pointer to `utinni_init` via `lpThreadParam`. Adds `FlushInstructionCache` after both the EB FE write and the OEP restore. Diagnostic `OutputDebugStringA` byte-readback logs around patch/restore. |
| `7bf42b6` | UtinniCore | New cdecl export `utinni_signal_launcher_ready()` — opens the named event by name and `SetEvent`s it, with critical-log error paths. `utinni_init` now captures the event name from `lpThreadParam` into a static `g_readyEventName` (defensive: empty-name = silent no-op so tests invoking utinni_init directly don't crash). `test_exports.cpp` `kExpectedExports` grew to 13 entries; `ExportResolutionTests.cs` `ExpectedExportCount` bumped 12→13. |
| `2c3b2f6` | UtinniCoreDotNet | `Native.SignalLauncherReady()` P/Invoke declaration in `Utility/Native.cs`. `Startup.EntryPoint()` now calls it after all four `*Callbacks.Initialize()` calls and immediately before `Application.Run`. This is the only safe slot — earlier would race managed callback registration; later would never fire (Application.Run blocks). |
| `20fbad5` | Game hooks | Entry/exit logs on `hkInstall`, `hkSetScene`, `hkCleanupScene`. Conditional logs around the `loadNewScene`/`sceneCleaned` two-phase state machine in `hkMainLoop` (only fires on state transitions, not per-frame). Already paid for itself diagnosing the Lok crash. |

## Diagnosis chain (brief)

Followed three threads, two false:

1. **Initial morning hypothesis** (continuing from PM handoff): SWGEmu's runtime patcher writes `EB FE` as an OS-version-check neutralization. CODEX corrected this — the function at `0x0131DC7A` is **MSVC `__tmainCRTStartup`** (PE entry), not SWG's `Os::install`. CRT identification via four-global `_osver/_winver/_winmajor/_winminor` pattern + `_RT_THREAD` / `_RT_HEAPINIT` fatal codes (0x10 / 0x1C).

2. **CODEX's first fix recommendation** — trampoline corruption in `cuiHud::getTarget` due to `DETOUR_TYPE_PUSH_RET, 5`. Real bug, applied as `dad9845`. **Did not affect the halt.**

3. **User's correct intuition**: "Maybe Utinni does this to put the main thread on hold while it sets everything up." `grep "0xEB"` in our own repo immediately surfaced `Launcher/main.cpp:351`. Three sessions of "SWGEmu's mysterious patcher" theorizing had been wrong direction.

4. **CODEX's key insight**: `WriteProcessMemory` does not flush the target's instruction cache. The variance ("sometimes halts immediately, sometimes runs to preloading then halts later") was CPU I-cache nondeterminism — the patch was *visible in memory* but not necessarily executed when the bytes were already prefetched into I-cache. `FlushInstructionCache(hProcess, entry, 2)` after both writes eliminates the variance entirely.

5. **Signal-event sync** replaces the broken wait pattern. Architecture documented in commit `488ff0c` body and in the four memory files updated this session.

## What's open after this session

Documented as four discrete follow-ups, in rough priority order. None blocking the core boot+render pipeline.

### Issue 7 (NEW) — Lok scene-load: second-cycle access violation

Lok scene loads cleanly the first time (`hkSetScene: ENTRY (scene=234F5EC0) → setupScene returned → setSceneCallbacks complete`). Then ~3 seconds later a **second** scene-load cycle starts (`loadNewScene=true` again, `Game::cleanupScene` fires, `hkSetScene` fires with `scene=00000000` — SWG's internal teardown call). SWG then access-violates at `0x00b3f620` while touching `cargo_freighter_l0.msh`. Crash dump: `D:\SWGEmu-Client\SWGEmu\logs\SWGEmu.exe-stage.119798-20260520022742.{txt,mdmp}`.

**Open questions:**
- What triggers the second cycle? Editor UI action? A `setSceneCallbacks` callback that flips `loadNewScene` back to true? SWG-internal logic?
- Is `0x00b3f620` a fault address or EIP? (SWG dump format is ambiguous.) Resolving via VS attach + disassembly would name the SWG function involved.

**Next steps:** (a) attach VS to a re-launched session, trigger Lok load, after first load completes set a breakpoint on `swg::game::setupScene` and on `Game::cleanupScene`, observe what calls them the second time and what state has changed; (b) check the TJT plugin's `setSceneCallback` (the one logged "firing 1 setSceneCallbacks") for any side effect that retriggers load.

### Issue 8 (NEW) — Naboo scene-load: SWG memory pool exhaustion

`terrain/naboo.trn` exhausts SWG's 750 MB internal allocator at ~300 MB used (`BytesAllocated: 196M → 300M`), fataling with `b0780503: failed allocation attempt for 38048 (38017 actual)` while loading `shared_frn_all_bed_sm_s1.iff`. Crash dump: `D:\SWGEmu-Client\SWGEmu\logs\SWGEmu.exe-stage.119798-20260520021056.{txt,mdmp}`.

**Open questions:**
- Why is SWG's pool capping at ~300 MB when the report says 750 MB total? Sub-pool / size-bucket exhaustion?
- Standalone SWG presumably loads Naboo fine (players run on Naboo all the time). What's different when Utinni is injected? CLR + WinForms + plugins consume ~hundreds of MB of address space — if SWG's allocator reserves a contiguous 750 MB vaddr region at process startup, the presence of Utinni's mappings could force a smaller allocation.
- Is SWGEmu.exe `/LARGEADDRESSAWARE`? 32-bit non-LAA caps at 2 GB total user vaddr.

**Next steps:** (a) `dumpbin /headers SWGEmu.exe | findstr /i large` to check LAA flag; (b) if not set, patch PE characteristics to add `IMAGE_FILE_LARGE_ADDRESS_AWARE`; (c) failing that, find SWG's `Memory size` config key (not in any `.cfg` we control — likely hardcoded in SWG's `[SharedFoundation]` defaults; RVA-patch via Utinni); (d) audit Utinni allocations for trimming.

### Issue 9 (NEW) — Cursor doesn't display + special keys don't work

User report from first successful boot: regular character keys typed into the login screen username field worked, but **delete / tab / return / mouse cursor visibility** did not. `hkSetupInstall` log line: `(passthrough HWND; cursor side-effects retained)` — meaning SWG's `ShowCursor(false)` calls take effect even though we don't override the HWND.

**Open questions:**
- Is the keyboard input flowing through `WM_CHAR` only? If so, that bypasses DirectInput's keyboard device, which would handle special keys.
- Does DirectInput have proper focus on SWG's HWND vs the editor's parent HWND? `DIERR_INVALIDPARAM` was a thing in the previous session.
- Cursor: is SWG hiding the system cursor and trying to show its own custom cursor that's never appearing? Could be the texture-not-loaded issue.

**Next steps:** (a) examine DirectInput device acquisition in SWG via VS — is `SetCooperativeLevel` failing now under the passthrough model?; (b) hook `ShowCursor` to log calls + return values; (c) compare WM_KEYDOWN routing between standalone SWG and Utinni-injected.

### Issue 10 (NEW, low priority) — Window reparenting not yet implemented

Confirmed: SWG creates its own top-level window (per the HWND-override removal from earlier today). The plan was to `SetParent` + `WS_CHILD` style change to reparent into the editor's PanelGame. This is documented as the "reparent-after-creation model" in `hkSetupStartInstall`'s log line, but the managed side never actually does the reparent. SWG currently displays as a separate window alongside the editor.

**Next steps:** managed-side hook on `Client::setHwnd` (already exists per `client.cpp`) — when fired, the editor's PanelGame should grab the HWND and `SetParent` it into itself. Defer until Issues 7-9 are stabilized.

### Issues 1-6 (older)

- **#1 WR-03 exit dialog** — DEFERRED, may resurface with new state.
- **#2 D3D9 vtable pattern** — RESOLVED 2026-05-19 morning (commit `2c57d38`).
- **#3 HWND-override hooks** — RESOLVED 2026-05-19 morning (commits `18c5e22`, `74f64fc`).
- **#4 CLR exception during template load** — LIKELY OBSOLETE (was a downstream consequence of the now-fixed Issue #6 halt; not observed in tonight's successful runs).
- **#5 SWG window invisible during runtime** — RESOLVED IMPLICITLY (window appears now that the main thread runs). Reparenting is Issue 10.
- **#6 SWG main thread halts in jmp-self** — **RESOLVED** by commits `dad9845..20fbad5`. Root cause: Utinni's own Launcher writing EB FE without a working restore path. See [eb-fe-patch-origin memory](C:\Users\kenne\.claude\projects\D--Code-Utinni\memory\project_eb_fe_patch_origin.md) for the full mechanics.

## Diagnostic data captured

### Two crash dumps (Issues 7 and 8)

Both kept for next session:
- `D:\SWGEmu-Client\SWGEmu\logs\SWGEmu.exe-stage.119798-20260520021056.{txt,mdmp}` — Naboo OOM
- `D:\SWGEmu-Client\SWGEmu\logs\SWGEmu.exe-stage.119798-20260520022742.{txt,mdmp}` — Lok access violation

### utinni.log successful boot trace (20:09 run)

The log line sequence after the fix:

```
Creating detours / patches / plugins
utinni_signal_launcher_ready: signaled 'Local\UtinniReady_<pid>'  ← new
hkSetupStartInstall: first fire
[SWG] ClientMain: Command Line = ""
... audio init, dPVS, graphics::install, directX::detour ...
hkInstall: ENTRY (Game::install) -> calling swg::game::install trampoline  ← new diag
[SWG] Preloading took [0.38] seconds
hkInstall: swg::game::install returned; constructing Repository  ← new diag
hkInstall: Repository constructed; WorldSnapshot::generateHighestId()
hkInstall: firing 1 installCallbacks
hkInstall: installCallbacks complete
hkInstall: autoLoadScene=false; EXIT
directX::hkBeginScene: first fire (D3D9 detour confirmed)
directX::hkPresent: first fire (block=0, destHwndOverride=0x...)
[SWG] Intro::leaveIntro()                                          ← reached login
```

### Diagnostic byte-readback logs

`Launcher/main.cpp` emits via `OutputDebugStringA` (visible in DebugView or VS):

```
[LAUNCHER] post-patch bytes at 0x0131DC7A: EB FE (expected EB FE)
[LAUNCHER] post-restore bytes at 0x0131DC7A: 55 8B (expected 55 8B)
```

Keep these in place until Issues 7-9 stabilize; remove once everything is solid.

## Files referenced this session

- `Launcher/main.cpp` — signal-event sync, FlushInstructionCache, byte logs (commit `488ff0c`)
- `UtinniCore/utinni.cpp` — `g_readyEventName` capture, `utinni_signal_launcher_ready` export (commit `7bf42b6`)
- `UtinniCore/test_exports.cpp` — export list expanded
- `UtinniCore/swg/ui/cui_hud.cpp` — explicit-length removed (commit `dad9845`)
- `UtinniCore/swg/game/game.cpp` — diagnostic logs (commit `20fbad5`)
- `external/DetourXS/detourxs.cpp` — defensive guard (commit `dad9845`)
- `UtinniCoreDotNet/Utility/Native.cs` — P/Invoke declaration (commit `2c3b2f6`)
- `UtinniCoreDotNet/main.cs` — `SignalLauncherReady()` call before `Application.Run` (commit `2c3b2f6`)
- `UtinniCoreDotNet.Tests/ExportResolutionTests.cs` — `ExpectedExportCount` 12 → 13 (commit `7bf42b6`)

## Memory updates

Four memory files written/updated this session, all under `C:\Users\kenne\.claude\projects\D--Code-Utinni\memory\`:

- **`project_eb_fe_patch_origin.md`** — full mechanics of why the Launcher writes EB FE, why the restore was broken, why the signal-event fix is the right architecture. Includes the I-cache-variance explanation.
- **`feedback_detourxs_explicit_len.md`** — the bug class: `Detour::Create` with explicit `detourLen < minDetLen` silently corrupts the target AND overflows `pbPatchBuf`. Always prefer `DETOUR_LEN_AUTO`.
- **`feedback_crt_vs_swg_fingerprint.md`** — how to identify MSVC CRT `__tmainCRTStartup` / `__setosversion` vs SWG `Os::install` from a hex dump. Saves multi-day source-side wild-goose chases.
- **`feedback_codex_peer_review.md`** — CODEX is available as a peer AI for verification. Draft self-contained prompts when a second opinion would help.

Master at session end: `20fbad5`. Pushed to `origin/master` at `kennethlong/Utinni`.

## Next-session entry point

Pick up at **Issue 7 (Lok second-cycle crash)** or **Issue 9 (cursor + special keys)**. Both are short investigations; Issue 7 has a crash dump ready to go, Issue 9 needs a VS attach session to characterize DirectInput state.

Suggested order: Issue 9 first (input is a usability blocker; if special keys don't work the editor isn't really usable), then Issue 7, then Issue 8 (Naboo memory — likely needs LAA flag flip on SWGEmu.exe).
