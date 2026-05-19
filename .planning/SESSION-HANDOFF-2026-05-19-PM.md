# Session Handoff: 2026-05-19 (Evening)

> Written 2026-05-19 evening to wrap up a long working session that started from the morning handoff's recommended d3d9 fix and uncovered five additional issue layers. Four real fixes shipped, three layers identified and documented, one currently-blocking issue with a concrete investigation plan. Supersedes the morning handoff (`SESSION-HANDOFF-2026-05-19.md`) for what's next.

## TL;DR

**The morning's d3d9 fix landed (commit `2c57d38`)** and unblocked SWG's render path through to `getVtbl`. But D3D9 detours installing cleanly was only the first of several layered issues. We bisected, instrumented, and fixed three more native-side bugs along the way. The current blocker is a **SWGEmu-applied runtime memory patch** at `0x0131DC7A` that puts a `jmp $` (infinite self-loop) over the function entry of what's almost certainly `Os::install` / SharedFoundation's OS-version-detection routine. SWGEmu patched it to neutralize the OS version check (which would otherwise bail on modern Win11). With Utinni injected, **some hook in our chain re-activates a code path that calls this neutralized function**, and the SWG main thread halts there. Without Utinni, SWG never invokes the patched function and the game runs normally.

**Status**: editor host displays correctly; SWG runs through audio → dPVS → preloading; SWG main thread then parks in the `EB FE` infinite loop and never reaches its render loop. SWG's window remains invisible during runtime (briefly flashes on editor-host close, when the process is finally tearing down). No CLR exception, no error dialog, no crash — silent halt.

**Next-session entry point**: find which Utinni hook (or modified code path) is calling into `0x0131DC7A`. Three concrete investigation paths documented in §"Next session" below.

## What we shipped this session

| Commit | Layer | What |
|---|---|---|
| `2c57d38` | D3D9 hook installer | Replaced broken d3d9.dll pattern scan with dummy-device vtable harvest (the morning's recommended fix). `IDirect3D9::CreateDevice(HAL)` on a hidden 1×1 window, snapshot 119 vtable entries, release. Works against modern d3d9.dll (Win11 24H2 6.2.26100.8328) which allocates IDirect3DDevice9 vtables per-instance on the heap — no static `.rdata` table for pattern scanning. |
| `18c5e22` | Client startup hook | Removed `pStartupData->createOwnWindow=false` override in `hkSetupStartInstall`. SWGEmu's `setupStartDataInstall` rejects that path and silently hangs SWG after audio init. Bisection (13 rounds!) narrowed to this single line. |
| `74f64fc` | DirectInput hook | Removed HWND override in `hkSetupInstall`. The original code walked up to the editor's top-level HWND and passed it to DirectInput's `SetCooperativeLevel`, which returned `DIERR_INVALIDPARAM` because the editor HWND is on the CLR thread (`Application.Run`), not SWG's main thread. SWG's ExceptionHandler caught the fatal. |
| `88b88c6` | DetourXS vendored library | Three real bugs surfaced by VS Debug build's strict heap checker: (1) line 166 `delete pbPatchBuf` was scalar delete on `new BYTE[detLen]` array — UB, fires on every `Detour::Create` call (~25 per injection); (2) line 169 `VirtualProtect(..., new DWORD)` leaks a DWORD; (3) line 206 same leak in `Remove`. All fixed. In Release the corruption was silent but degrading the heap — likely root cause of intermittent symptoms across past sessions. |
| `0ebc428` | Build infrastructure | Added DXSDK June 2010 `IncludePath` / `LibraryPath` to `UtinniCore.vcxproj` Debug|Win32 PropertyGroup (was only in Release|Win32 and RelWithDbgInfo|Win32). VS Debug builds now succeed. Same Debug build is what surfaced the DetourXS heap corruption. |

Plus diagnostic instrumentation commits (kept in tree pending the next investigation): `70a23d2` (hkBeginScene/hkPresent first-fire), `c4901e3` (hkSetupStartInstall state logging), `f4592c2` (hkInstall + directX::detour entry/exit), `1ec9519` (VEH for int 3 events), `ba0ed2a` (STATE.md documentation update).

## What's open after this session

Documented in detail in `STATE.md` Blockers/Concerns items 1–6. Summary:

### Issue 6 — THE BLOCKER (newly discovered)
**SWG main thread halts in `EB FE` infinite loop at `0x0131DC7A` in SWGEmu memory.**

- VS attach to running SWGEmu (post-preloading) showed Main Thread (TID 60040) with `EIP=0x0131DC7B`, `ECX=EDX=ESI=EDI=0x0131DC7A` (the function entry address splattered into all GP registers — characteristic of the bytes BEFORE the patch having set those, or of catching the thread mid-iteration of the tiny loop). Multiple Break-All cycles showed EIP unchanged — definitively in an infinite 2-byte loop.

- **File bytes** at `0xF1DC7A` are `55 8B EC 6A FF 68 ...` — normal MSVC function prologue (`push ebp; mov ebp, esp; push -1; push <SEH handler>; ...`).

- **Memory bytes** at `0x0131DC7A` are `EB FE EC 6A FF 68 ...` — first two prologue bytes overwritten with `EB FE` = `jmp 0x0131DC7A` (jmp self). Everything from byte 3 onwards matches the file. **The patch is in memory only, applied at process load time.**

- The patch is **NOT from Utinni**: none of our hardcoded RVAs target this address, and `EB FE` is not an encoding `DetourXS` emits (it uses 5-byte `E9` for JMP and `68 ?? ?? ?? ?? C3` for PUSH/RET).

- **Function role identified by disassembling everything past the patch**: the function calls `[0x015DC0E0]` (IAT slot, resolved to runtime address `0x75381500` = kernel32.dll/kernelbase.dll range — almost certainly `GetVersion` or `GetVersionExA`). The return value is then decomposed:
  - `mov dl, ah` — minor version byte → store at `0x019AA13C`
  - `mov ecx, eax; and ecx, 0xFF` — major version byte → store at `0x019AA138`
  - `shl ecx, 8; add ecx, edx; store at 0x019AA134` — packed major:minor
  - `shr eax, 16; store at 0x019AA130` — build number
  - Then validates via two `test eax, eax; jnz +8` branches with error codes `0x10` / `0x1C`.
- This is the standard `GetVersion()` packed-DWORD decomposition. The function is **almost certainly `Os::install`** in SWG's `SharedFoundation` library (or equivalent OS-detection initializer). SWG's 2003 logic would bail on Win10/11 reporting version 10.0+; SWGEmu neutralized the bail by overwriting the function entry to halt callers instead.

- SWGEmu presumably also patched out (or branched around) the original call sites — that's why standalone SWGEmu works fine. **With Utinni's hooks active, some path re-invokes the function**, and the main thread halts forever. The process stays alive because Utinni's WinForms editor host (FormMain) is running `Application.Run` on a separate CLR thread.

- Other symptoms now explained as consequential:
  - Issue 4 (CLR exception 0xE0434352 during Zabrak template load) — observed only in bisection round 13 with reduced detour set; possibly an exception that fires when SWG progresses past the normally-halted point but lands in a partially-initialized state. May disappear entirely once Issue 6 is fixed.
  - Issue 5 (SWG window invisible during runtime, flashes on editor close) — direct consequence of `clientMain` never returning to enter the render loop. When editor closes, the process tears down and SWG's window briefly paints during the shutdown sequence.

- WR-03 exit dialog (Issue 1) was also re-observed during this session — it disappears in passthrough-everything builds and reappears with any detour active. Probably another consequence of the cascade, will likely resolve when Issue 6 does.

### Issues 1–5 (older)
See `STATE.md` for full descriptions. Briefly:
- **#1 WR-03 exit dialog** — pre-existing, deferred. Likely consequential to #6.
- **#2 D3D9 vtable pattern** — RESOLVED (commit `2c57d38`).
- **#3 HWND-override hooks** — RESOLVED (commits `18c5e22`, `74f64fc`).
- **#4 CLR exception during template load** — likely consequential to #6.
- **#5 SWG window invisible** — likely consequential to #6.

## Diagnostic data captured

### Confirmed via memory read in VS (live SWGEmu)
- Bytes at `0x0131DC7A` (file offset `0xF1DC7A` in SWGEmu.exe):
  - **File**: `55 8B EC 6A FF 68 90 20 60 01 68 40 0B 32 01 64 A1 00 00 00 00 50 64 89 25 00 00 00 00 83 EC 58 53 56 57 89 65 E8 FF 15 E0 C0 5D 01 33 D2 8A D4 89 15 3C`
  - **Memory**: `EB FE EC 6A FF 68 90 20 60 01 ...` (same from byte 3 onwards — patch is exactly 2 bytes overwriting `55 8B`)
- IAT slot at `0x015DC0E0` resolves at runtime to `0x75381500` (kernel32/kernelbase range, GetVersion-class API).
- The function's indirect-call thunks at `0x0144B640` etc. show MSVC adjustor-thunk patterns (`mov ecx, [ebp-0x18]; add ecx, 0xC; jmp <far>`) — vtable for a multi-inheritance class. The function calls an OS-abstraction class's virtual method, consistent with `Os::install` calling into a `Win32::GetSystemVersion` member.

### SWGEmu.exe identity (confirmed unchanged from morning handoff)
- Path: `D:\SWGEmu-Client\SWGEmu\SWGEmu.exe`
- Size: 22,061,142 bytes
- SHA256: `58012E57CEBC499454812BA7ED96B1289DB01E520963B4FC364EDB41C322B2A8`
- Modified: 2026-05-17 12:17:54 (unchanged since pre-session)

### Other Claude's source-level analysis (this session)
The other Claude with the buildable SWG source identified the matching source pattern:

```
src/engine/shared/library/sharedFoundation/src/shared/Fatal.cpp:156

static void InternalFatal(const char *format, va_list va) {
    if (ms_throwExceptions)
        throw FatalException(ms_buffer, FatalException::ZeroSourceString);
#ifdef _WIN32
    { __asm int 3; }            // line 174
#endif
    DEBUG_OUTPUT_CHANNEL("Foundation\\Fatal", ("%s", ms_buffer));
    ExitChain::fatal();
    REPORT(true, RF_fatal | RF_dialog, ("%s", ms_buffer));
    Os::abort();
}
```

The other Claude pointed at `InternalFatal` initially, but our VEH instrumentation (commit `1ec9519`) caught zero `EXCEPTION_BREAKPOINT` events during a run — so the halt is NOT happening through a real `int 3`. SWGEmu's binary patch replaced the natural-bytecode path with `EB FE` directly. The function we're halted in is not `InternalFatal` itself but something else that has the GetVersion-decompose pattern. Almost certainly `Os::install` proper.

### Post-preload control flow (per other Claude's source dive)
```cpp
SetupClientGame::install(data);             // Game::install → preloadAssets ("Preloading took…" log)
CuiManager::setImplementationInstallFunctions(SwgCuiManager::install, ...);
SetupClientBugReporting::install();
SetupSharedIoWin::install();
SetupSwgClientUserInterface::install();     // CUI mediators, character templates, splash
SwgCuiG15Lcd::initializeLcd();
rootInstallTimer.manualExit();
SetupSharedFoundation::callbackWithExceptionHandling(Game::run);
```

The halt happens after `Preloading took …` and before any of these progress further. Whichever of these install steps (or sub-step) reaches the `Os::install` neutralized stub is the trigger.

## Next session

Pick up at Issue 6. Three concrete investigation paths, in order of expected signal:

### Path A — Pattern-search SWGEmu.exe for callers of `0x0131DC7A`
- An x86 relative `call` to `0x0131DC7A` from caller address `C` encodes as `E8 (0x0131DC7A - C - 5)` (4 bytes signed offset). Pattern-scan SWGEmu.exe `.text` for `E8 ?? ?? ?? ??` where the offset resolves to `0x0131DC7A`.
- For each hit, check the bytes immediately before — if they're NOPs or another control-flow modification, SWGEmu has patched out that call. If they're a normal calling convention setup, the call site is "live" and could be reached if Utinni's hooks change the path.
- Cross-reference live call sites against Utinni's hook RVAs (`Client::detour`, `Graphics::detour`, etc.) and trace which hook chains lead there.

### Path B — Source cross-reference via other Claude
- Have the other Claude search the SWG source for the function matching the GetVersion-decompose pattern (member call → `mov dl, ah` → store major/minor/build at four close globals → validate). That gives us the source function name (almost certainly `Os::install` or `SetupSharedFoundation::install`'s OS branch).
- Then find all source-level callers of that function. The caller path most likely reached is the SubsystemSetup chain post-preloading.
- This won't translate directly to SWGEmu RVAs (different binary) but tells us at the source level what subsystem is responsible — which we can then map to one of Utinni's existing hooks.

### Path C — Targeted ProcMon / runtime trace
- Run SWGEmu standalone (no Utinni) under Process Monitor with stack-tracing enabled. Capture what kernel32/kernelbase functions are called and from what stack frames.
- Run with Utinni injected. Diff. The new call into `kernel32!GetVersion` (or whatever is at `0x75381500`) from a frame in the `0x0131DCXX` range will be the smoking gun.
- Heavier setup than A or B but produces incontrovertible proof.

### Recommended order
**Path B first** (cheapest — the other Claude already has the source loaded). If that names a single function, jump to checking which Utinni hook touches its caller chain. **Path A as a parallel sanity check** — confirms the source-level identification by listing the binary-level call sites. Path C only if A and B both produce ambiguous results.

## Sanity-check: things ruled out this session

- **Our probe code's `GetFileVersionInfoA` calls are NOT the trigger.** The probe lives in the SWG Source build (different binary, never loaded into the SWGEmu process Utinni injects into). It also uses a different API family (version.lib for file resource queries, not kernel32 for OS version). Confirmed `version.lib` is referenced in Utinni only in `Launcher\main.cpp:46` (pre-flight check; runs in the Launcher process, not SWGEmu).
- **SWGEmu.exe hasn't been modified** since the morning handoff (SHA256 match) — the `EB FE` patch is applied by SWGEmu's loader at runtime, not by edits to the on-disk binary.
- **The halt is not exception-driven.** Utinni's VEH (registered FirstHandler=1) caught zero `EXCEPTION_BREAKPOINT` events; the thread literally executes the 2-byte `EB FE` instruction in a CPU-level loop.
- **The DetourXS heap corruption was real but independent.** Fixed in `88b88c6` but Release-run symptoms remained unchanged — the heap corruption was a separate latent bug, not the cause of #6.

## Files referenced this session

- `UtinniCore/swg/graphics/directx9.cpp` — `getVtbl()` rewrite (commit `2c57d38`)
- `UtinniCore/swg/client/client.cpp` — `hkSetupStartInstall` passthrough (commit `18c5e22`)
- `UtinniCore/swg/misc/direct_input.cpp` — `hkSetupInstall` passthrough (commit `74f64fc`)
- `external/DetourXS/detourxs.cpp` — heap fixes (commit `88b88c6`)
- `UtinniCore/UtinniCore.vcxproj` — Debug-config DXSDK paths (commit `0ebc428`)
- `UtinniCore/utinni.cpp` — VEH registration (commit `1ec9519`)
- `UtinniCore/swg/graphics/graphics.cpp` — hkInstall instrumentation (commit `f4592c2`)
- `.planning/STATE.md` — items 1–6 (commit `ba0ed2a`)
- `.planning/SESSION-HANDOFF-2026-05-19.md` — morning handoff (preserved for record)
- `bin/Release/utinni.log` — last live-run diagnostic log (rotated each launch)
- `D:\SWGEmu-Client\SWGEmu\logs\SWGEmu.exe-stage.119798-20260519170719.{txt,mdmp}` — crash dump from Round 13 bisection state (CLR exception, likely consequential to Issue 6)

Master at this session's end is `ba0ed2a` (this handoff doc will be the next commit).
