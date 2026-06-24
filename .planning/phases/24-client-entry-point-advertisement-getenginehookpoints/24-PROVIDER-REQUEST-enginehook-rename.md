# Provider Request — paired EngineHook rename of the shared hookpoint contract

**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Date:** 2026-06-23

Goal: the GetEngineHookPoints advertise+resolve harness is now generically named (drop "Utinni"
branding) so any app can mod a client this way — swg-client-v2 is the proving provider. The consumer
side landed in Utinni HEAD (commit `304b5a8`). The shared `.h/.inc` are SHARED-VERBATIM, so they must
match byte-for-byte — please apply the SAME rename on the provider side.

## Binary contract is UNCHANGED — no rush, no functional break

This is a **source-cosmetic** rename: the export name `GetEngineHookPoints` and the POD **layout** are
unchanged. The renamed Utinni consumer already works against your CURRENT staged `SwgClient_r.exe`
(it still reads `GetEngineHookPoints` → same POD). So rebuild + restage at your convenience; nothing
breaks in the meantime. The rename just keeps the "shared verbatim" headers from drifting.

## Exact mapping (identical to the consumer side)

Token replacements (whole-word):
- `UtinniEngineHookPoint`  → `EngineHookPoint`   (handles `UtinniEngineHookPoints` → `EngineHookPoints` too)
- `UTINNI_HOOKPOINT`       → `ENGINE_HOOKPOINT`   (handles `UTINNI_HOOKPOINTS_VERSION` → `ENGINE_HOOKPOINTS_VERSION` too)
- `UtinniDx11HookPoints`   → `EngineDx11HookPoints`
- `utinni_engine_hookpoints` → `engine_hookpoints`  (file names, includes, header guard `INCLUDED_..._H`)

File renames (git mv):
- `src/game/client/application/SwgClient/src/shared/utinni_engine_hookpoints.h`  → `engine_hookpoints.h`
- `src/game/client/application/SwgClient/src/shared/utinni_engine_hookpoints.inc` → `engine_hookpoints.inc`
- `src/game/client/application/SwgClient/src/win32/utinni_advertise.cpp` → `engine_advertise.cpp`
  (the advertisement TU — the consumer header doc now references `engine_advertise.cpp`)

In-place edits:
- `src/engine/client/application/Direct3d11/src/win32/Direct3d11.cpp`: `UtinniDx11HookPoints` →
  `EngineDx11HookPoints` (the gl11 acquisition POD returned by `GetHookPoints` — layout unchanged).
- Update the provider project/vcxproj/filters entries that reference `utinni_engine_hookpoints.h` and
  `utinni_advertise.cpp`.
- Genericize the doc-comment prose in `engine_hookpoints.h` (no longer frame "Utinni" as THE consumer)
  so the two repos' copies stay byte-identical. Easiest: copy Utinni HEAD's
  `UtinniCore/swg/engine_hookpoints.h` + `.inc` verbatim (commit `304b5a8`) — they ARE the shared
  contract.

## Keep as-is (NOT in scope)

`GetEngineHookPoints` / `GetHookPoints` export names (already generic); your internal class/namespace
names that aren't the shared contract; the contract VERSION value (`ENGINE_HOOKPOINTS_VERSION 3`).

## Build gate + handback

- Build Release/Win32 (`Direct3d11;SwgClient`), 0 unresolved externals; dumpbin confirms
  `GetEngineHookPoints` (exe) + `GetHookPoints` (gl11) exports still present (the binary contract).
- Rebuild + restage `SwgClient_r.exe` + `gl11_r.dll` with matching PDBs when convenient.
- Handback noting the rename done + the contract version unchanged. The `.h/.inc` must match Utinni
  HEAD `304b5a8` byte-for-byte.

## Constraints

Do NOT write to `D:/Code/Utinni`. No contract VERSION bump (pure rename). Cross-check with the crew if
useful. Live re-smoke is maintainer-only.
