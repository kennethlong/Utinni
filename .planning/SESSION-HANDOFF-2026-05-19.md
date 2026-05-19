# Session Handoff: 2026-05-18 → 2026-05-19

> Written 2026-05-19 morning to capture the corrected diagnosis of why live SWG rendering fails in Utinni-injected sessions on the user's current SWGEmu Launchpad build. Supersedes (and corrects) the over-broad "RVA drift" framing in `docs/ai/rva-realignment.md`.

## TL;DR

**Root cause of black play window + no ImGui + grayed TJT panels:**
Utinni's D3D9 vtable finder (`directx9.cpp::getVtbl()`) uses a byte-pattern scan
of the user's `SysWOW64\d3d9.dll`. **The pattern does not match modern
`d3d9.dll`.** Direct test confirmed 0 hits in the first 1.2 MB of the user's
`d3d9.dll` (~1.5 MB total). Consequence: `getVtbl()` returns `nullptr`,
`directX::detour()` early-returns, zero D3D9 detours install, `hkPresent`
never fires, ImGui can't draw, SWG runs unhooked → black-screen-from-Utinni's-
perspective (SWG itself IS rendering, but bypassing Utinni entirely).

**Not RVA drift on SWG itself.** Decisively ruled out:
- SWG binary's PE link timestamp = **`Thu Apr 14 15:36:40 2005`** — that's
  Sony's original SOE compile, preserved across SWGEmu's in-place patches.
  SWGEmu does NOT rebuild the binary.
- Most of Utinni's hardcoded SWG RVAs are probably still correct.
- The one early-fire SWG RVA we can verify is live (editor host comes up,
  drag-drop works, TJT loads, plugin pipeline runs) — proves Utinni's
  managed-side + early-native-side detours fire correctly.

**Recommended fix (the right one, durable):** replace `getVtbl()`'s
pattern-scan with the conventional dummy-device vtable approach:

```cpp
IDirect3D9* d3d = Direct3DCreate9(D3D_SDK_VERSION);
D3DPRESENT_PARAMETERS pp = { /* minimal valid */ };
IDirect3DDevice9* dummyDevice;
d3d->CreateDevice(D3DADAPTER_DEFAULT, D3DDEVTYPE_NULLREF, /*hwnd*/, /*flags*/, &pp, &dummyDevice);
swgptr* vtbl = *(swgptr**)dummyDevice;  // first 4 bytes of any COM object = vtbl ptr
// extract Present, Reset, BeginScene, etc. by index from vtbl
dummyDevice->Release();
d3d->Release();
```

Works against EVERY Windows version (the public D3D9 API materializes a
device, then we read its vtable directly — no fragile byte patterns). This
is what every modern D3D9 hooking framework does (MinHook samples, ReShade,
etc.). Estimated effort: ~2-4 hours including tests.

## What we shipped 2026-05-17/18

- **Phase 02** complete: 15 critical bugs + KB-05 + C-16 closed. CI green
  (run #25992534696). Manual UATs C-01 (live SWG injection) + C-09
  (minimize/restore) PASSED on 2026-05-18.
- **Phase 02.1** complete: 8 code-review findings closed (CR-02, CR-03,
  CR-04, WR-01, WR-02 partial-proof, WR-03, WR-05, WR-09) + NEW
  utinni_test_resolveExports harness. CI green (run #26067903389). 22
  commits.
- **DEC-C4 locked**: Wave-1 deliverables (Phases 7-11: TRE Browser, IFF,
  Datatable, Stringtable, Object Template) ship as TJT subpanels, not
  separate plugins. PROJECT.md Key Decisions table + ROADMAP rewording.
- **`scripts/local-test-setup.ps1`**: one-shot DXSDK + build + seed cfg
  + TJT + launch. Lives in repo for next session.

## What's open after this session

### Issue 1 — D3D9 pattern doesn't match modern d3d9.dll (THE BLOCKER)

**Status**: root-caused 2026-05-19. Not yet fixed.

**Fix**: dummy-device vtable approach in `directx9.cpp::getVtbl()`. See TL;DR.

**Time estimate**: 2-4 hours, single function rewrite + 1 test update.
Lives entirely in `UtinniCore/swg/graphics/directx9.cpp` + the WR-05
`FindPatternHarnessTests.cs` (which becomes meaningful again).

**Test design**: WR-05's `GetVtbl_WithD3d9Loaded_ReturnsNonZero` currently
passes on `windows-2022` CI only because the runner happens to ship an
older `d3d9.dll` where the pattern matches. After this fix, the test
becomes a true regression guard — should pass on any Windows version
with d3d9 installed.

**Open in**: STATE.md Blockers/Concerns + this handoff doc.

### Issue 2 — WR-03 exit dialog still fires

**Status**: Phase 02.1 Plan 02.1-02 successfully removed the
`delete depthTexture` UAF in `directX::cleanup()` (verified empty body
at `directx9.cpp:410-427`). But the **"Direct3D could not be correctly
initialized" dialog still appears on exit** in the user's injected
sessions. Different teardown path than the one fixed.

**Likely culprits** (untested):
1. `clr::stop()` (called immediately after `cleanup()` in `detatch()` at
   `utinni.cpp:153-157`) — CLR teardown noticing D3D state weirdness.
2. SWG's own D3D9 device release at process exit reporting the leftover
   `depthTexture` we intentionally don't delete.
3. The leftover `pTextureDepth` resource inside the leaked DepthTexture
   that DOES need explicit release (we only skipped delete on the
   DepthTexture wrapper; the inner D3D9 texture may need Release).

**Time estimate**: 1-3 hours investigation. Probably ~30 min once root
caused. Lives in `directX::cleanup()` or earlier in `hkPresent`'s
texture creation path.

**Priority**: lower than Issue 1. Exit-only nuisance. Doesn't block
testing or development.

### Issue 3 — RVA verification across all hardcoded sites

**Status**: untested. After Issue 1's fix, when ImGui actually renders
and we can see what state SWG is in, individual RVA mismatches will
surface as specific errors (rather than total black screen).

**Likely outcome**: most are fine. SWGEmu doesn't relink. The 0xAA4900
`Network::cast` site we sampled IS suspicious (bytes look like a small
wrapper, not a real cast function — but the original C-03 commit
admits the function was always "broken" so this may be the calibrated
state, not drift). Other RVAs need spot-checking only if they cause
visible errors after Issue 1 is fixed.

**Time estimate**: 0-2 days, depending on how many sites actually drift.

**Priority**: defer until after Issue 1 lands and we can see what
actually breaks.

## Diagnostic data captured

### User's SWGEmu binary
- Path: `D:\SWGEmu-Client\SWGEmu\SWGEmu.exe`
- SHA256: `58012E57CEBC499454812BA7ED96B1289DB01E520963B4FC364EDB41C322B2A8`
- Size: 22,061,142 bytes
- PE link timestamp: `Thu Apr 14 15:36:40 2005` (preserved from Sony's compile)
- Linker version: 6.00 (MSVC 6.0 era — original SOE build)
- ImageBase: `0x00400000`
- VersionInfo: ProductName=Star Wars Galaxies / Version 0.0.119.798 /
  OriginalFilename=SwgClient_r.exe / signed Sony Online Entertainment
- Section layout:
  - `.text` VA=`0x00001000` (1.18 MB) FileOff=`0x1000`
  - `.rdata` VA=`0x011DC000` (2.5 MB)
  - `.data` VA=`0x0144D000` (1.4 MB)
  - `.rsrc` VA=`0x015BD000` (small)

### User's d3d9.dll
- Path: `C:\Windows\SysWOW64\d3d9.dll`
- Size: 1,535,160 bytes
- Pattern `C7 06 00 00 00 00 89 86 ?? ?? ?? ?? 89 86`:
  **0 hits** in the first 1.2 MB scan range used by `directx9.cpp::getVtbl()`

### Sample RVA inspection
- Network::cast at file offset `0xAA4900` (same as RVA, since `.text` maps
  VA `0x1000` → FileOff `0x1000` 1:1):
  ```
  E8 2B 0F 8B FF F7 D8 1B C0 40 C3 90 90 90 90 90
  32 C0 C3 90 90 90 90 90 90 90 90 90 90 90 90 90
  ```
- Decodes to: short wrapper (`call X; neg eax; sbb eax,eax; inc eax; ret`)
  followed by NOP padding then another tiny function (`xor al,al; ret`).
- This is NOT a `__thiscall(int64_t*, int, int) → int64_t` function. Either
  Utinni's original RVA label was wrong (the C-03 commit comment "This is
  broken" supports this — original author wasn't 100% certain), OR
  SWGEmu's in-place patches replaced this specific function with a stub.
  Either way: investigate AFTER Issue 1 is fixed.

## Recommended next session

**Start with Issue 1, fast path:**

1. `git pull` (this handoff and prior session work)
2. Read this doc (the TL;DR is enough)
3. Edit `UtinniCore/swg/graphics/directx9.cpp::getVtbl()`:
   - Replace pattern-scan with `Direct3DCreate9` + `CreateDevice(NULLREF)` +
     read vtable from first 4 bytes of device pointer
   - The function should still return `swgptr*` so downstream code is
     unchanged
   - Keep the null-check pattern from CR-11 (defensive programming)
4. Update `FindPatternHarnessTests.cs`'s `GetVtbl_WithD3d9Loaded_ReturnsNonZero`:
   - Old test asserted `LoadLibraryA("d3d9.dll")` + `Utinni_GetVtbl() != 0`
   - New test still works because `getVtbl()` will succeed via the
     dummy-device path. Optionally add a comment explaining the
     architecture change.
5. Build + push + CI verify
6. Manual UAT: run `bin\Release\Launcher.exe`, observe ImGui overlay
   appears on the SWG window. This is the success criterion.
7. If ImGui appears → Issue 1 is closed. TJT panels should activate.
   The play window may STILL be black if other detours/scenes don't
   load — but that's now a separate, narrower set of problems we can
   diagnose one-at-a-time.

**Process call**: this is small enough to skip `gsd:plan-phase` machinery.
Single fix, single commit, single CI verify. Treat as a `fix(02.1-04):`
follow-up or open as Phase 02.2 if you want formal tracking.

## Corrected status of prior research

The `docs/ai/rva-realignment.md` doc written 2026-05-18 evening assumed
broad RVA drift. **That hypothesis was incorrect.** The doc is still
useful as reference for the eventual Phase 03 R-C work (when we
externalize RVAs for build-version resilience), but the immediate
problem doesn't require its workflow. Recommend leaving the doc in
place + adding a corrigendum pointing at this handoff.

## Open question for product direction

Once Issue 1 lands and you can actually drive the editor against a
loaded scene, the next decision is:

- **Continue Phase 03 (Strategic reworks R-A..R-H)**, including R-C
  RVA hardening + the d3d9-vtable approach generalized
- **Skip to Phase 04 (CLI shim)** for unattended testing
- **Start a Wave-1 subpanel** (Phase 7+ — TRE Browser) to validate the
  full plugin pipeline against actual content

DEC-C4 means Phase 7+ deliverables ship as TJT subpanels not separate
plugins; that decision is locked. The question is timing.

## Memory update

Worth saving as a `feedback` memory: **"don't jump to RVA drift as
explanation for D3D9 hook failures; check the dll-scan pattern first."**
This session burned ~2 hours on the wrong hypothesis. The right
diagnostic was 30 seconds of `PowerShell.ReadAllBytes + scan-for-pattern`.

## Files referenced in this handoff

- `UtinniCore/swg/graphics/directx9.cpp` — `getVtbl()` is the fix site
- `UtinniCore/swg/misc/network.cpp` — sample RVA inspection site
- `UtinniCoreDotNet.Tests/FindPatternHarnessTests.cs` — WR-05 test, becomes
  meaningful after Issue 1 fix
- `docs/ai/internals.md` — canonical RVA table (mostly correct, spot-check
  after Issue 1)
- `docs/ai/rva-realignment.md` — last night's research, over-scoped
- `.planning/STATE.md` — Blockers/Concerns section
- `.planning/phases/02-critical-bug-burn-down-c-01-c-15/02-HUMAN-UAT.md` —
  WR-03 partial-fix disposition
- `scripts/local-test-setup.ps1` — one-shot test runner; works fine
- `scripts/find-hidden-error.ps1` — diagnostic tool for hidden dialogs

Master at this session's end is at `09a7a87` (RVA realignment doc) +
will be at this handoff's commit when pushed.
