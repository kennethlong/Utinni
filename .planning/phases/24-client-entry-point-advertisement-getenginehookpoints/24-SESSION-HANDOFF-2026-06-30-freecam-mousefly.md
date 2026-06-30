# Phase 24 — Session Handoff (2026-06-30): FREE-CAM (mouse-fly) SHIPPED + richer inspector

Resume pointer for the advertised-client editor-unlock arc. This session shipped the **selected-object
inspector** (4 more fields) and the **free-camera** on the advertised NGE client (`SwgClient_r.exe`),
driving the contract to **v13 (119 names)**. Free-cam landed as **mouse-fly** (right-mouse + look);
**WASD was explicitly descoped** by the maintainer. Read top-to-bottom before resuming.

**Live pointers after this session:**
- **This doc** — current state of the editor-unlock arc.
- **Memory:** `project_phase24_editor_unlock_inflight.md` (running blow-by-blow).
- **Provider request (free-cam):** `24-PROVIDER-REQUEST-freecam-accessors.md` (rev.2; consumed → v13 delivered).
- **Provider HANDBACK (v13):** `D:/Code/swg-client-v2/.planning/handoff/2026-06-29-utinni-freecam-accessors-HANDBACK.md`.
- **Prior handoff:** `24-SESSION-HANDOFF-2026-06-29-bucket-a-editor-unlocks.md`.

---

## 0. STATUS in one paragraph

The advertised NGE client now has **five editor features live** (Effects, Chat, Radial/menus, World-pick
**inspector**, and **Free-cam**). This session: (1) expanded the Misc-panel **selected-object inspector**
from world-position to **Template / Appearance / Type(FOURCC) / Network-ID / Cell / Position / Yaw** — all
advertised-safe getters, smoke-passed; (2) unlocked **free-cam** via a provider round-trip (v12→**v13**, 6
new advertised accessor rows) + consumer wiring. The **big lesson of the session**: the SWGEmu free-cam
model (Utinni intercepts input + hand-rolls camera movement) is **wrong for NGE** — the engine's
`DebugPortalCamera::alter` flies the camera natively; we only switch the view. Free-cam is **mouse-fly**
(right-mouse=forward + mouse-look + noclip); **WASD is descoped** (the engine's debug input map doesn't bind
it, and the keyboard-capture release we tried broke the UI's chat). Everything is committed + pushed on both
repos; headless gates green; SWGEmu byte-unchanged throughout (D-00).

---

## 1. What landed this session (committed + pushed)

### Utinni (`master`, bc09025 → 598f25d)
| Commit | What |
|--------|------|
| `ce4f3fb` | richer inspector: 3 native advertised-safe `Object` accessors (ObjectTemplateName/NetworkIdValue/SharedAppearanceFilename) |
| `00569da` | inspector +ObjectType(FOURCC) +ParentCell |
| `618e517` | free-cam **offset probe** (sized the provider ask) |
| `aefe19c`/`ba678ae` | free-cam **provider request** (rev.2 after 4-AI review) |
| `4acb228` | **bind v13** (6 accessor rows, 113→119, behavior-neutral) |
| `12c4f8c` | free-cam native wiring (accessor-route + vtable-resolve alter) |
| `d3ba0ff` | native wiring **hardening** (4-AI pre-smoke review) |
| `debbb59` | **Wave-4** native exports (latch-backed toggle/state) |
| `4ebe82b` | keyboard-capture release + native alter (consult fix) |
| `9fbeb45` | **toggle-crash fix** (skip cameraChangeCallbacks dispatch on advertised) |
| `ad1dbad` | **drop keyboard-capture release** (broke chat, no WASD benefit) → mouse-fly |
| `598f25d` | **tidy** (remove dead `ensureAdvertisedAlterDetour`) + ABI rebless |

### UtinniPlugins (`master`, e5f1ed0 → e1c2a10)
| Commit | What |
|--------|------|
| `9dc1152`/`dce639f` | inspector: multi-field readout (template/appearance/type/networkid/cell/pos/yaw) |
| `643929d` | free-cam latch-path (FreeCamImpl toggle/state via native exports) |
| `e1c2a10` | **enable the FreeCam panel checkbox** on advertised (UpdateSceneAvailability never fires there) |

### Provider (swg-client-v2)
v12→**v13** (`2026-06-29-utinni-freecam-accessors-HANDBACK`): 6 CALLED-accessor rows. KEY FACT they
established: the consumer's "free camera" IS their **`DebugPortalCamera`** (`cm_Free=5 == CI_debugPortal=5`).

---

## 2. Contract state

- **`ENGINE_HOOKPOINTS_VERSION = 13`, 119 names. Consumer binds 117/119** (carve-outs `consoleHelper::sendInput`
  + `client::wndProc`). Drift gates `kIncCount==119` / `kBindingCount==117` in `endpoints_bindings.cpp` +
  `endpoints_tests.cpp`.
- v13 sha256: `inc=2fdd77c523658cb13f17edbe5822282a42a124ffacea8359e1744545668fd96d`,
  `h=bbd02175e820f12294e56d339afbc639ad6072c3bc6a3d814d2f7785d8aaf5ec` (re-sync byte-identical, LF).
- The 6 v13 rows: `groundScene::{isFreeCameraActive,getDebugPortalCameraMessageQueue}`,
  `gameCamera::getMessageQueue`, `messageQueue::{getCount,getMessage}`, `object::isActive`.

---

## 3. FREE-CAM — how it actually works (the architecture)

**On the advertised NGE client, free-cam = the engine's native DebugPortalCamera.** We do NOT replicate
movement (the SWGEmu model). The flow:

1. **Toggle** (FreeCam panel checkbox → `FreeCamImpl.ToggleFreeCam` → `utinni_toggleFreeCamera` native export
   → `GroundScene::toggleFreeCamera`): on advertised, toggle on **our own `s_advFreeCamEngaged` flag** (NOT
   `isFreeCameraActive()` — that reads true from scene-load because the loading screen forces view
   `CI_debugPortal`). ON = `changeCamera(cm_Free)`; OFF = `changeCamera(cm_FreeChase)`. `changeCamera` =
   `setView`, which selects the debug-portal input map.
2. **Movement** = the engine's `DebugPortalCamera::alter` (swg-client-v2
   `clientGame/.../camera/DebugPortalCamera.cpp:104`): drains its own message queue
   (`CM_mouseWalk`/`CM_cameraYawMouse`/`CM_cameraPitchMouse`), fed by `groundinputmap_debugportalcamera.iff`.
   **Right-mouse = forward, mouse-look = aim.** Noclip (fly through ground) is normal for this camera.
3. We install **NO** detour for movement on advertised (no `hkAlter`), touch **NO** keyboard capture.

**SWGEmu path is unchanged** (D-00): `isAdvertisedClient()` is false there → the original intercept-and-move
free-cam (`processIoEvent` + `hkAlter` + startup alter detour + the slot-4 self-check) all run as before.

### The hard-won lessons (carry these forward)
- **The engine already does free-fly on NGE.** Don't port the SWGEmu "append WASD to a queue + hand-roll
  movement in a detoured alter" model. Switch the view; let `DebugPortalCamera::alter` fly it.
- **`CuiIoWin` swallows `IOET_KeyDown`** when `m_keyboardInputActive==true` (`IOR_Block`,
  `CuiIoWin.cpp:955/1154`) — that's why keyboard never reaches `GroundScene`'s input map on the editor
  client. Mouse passes (`IOR_Pass`). (This is why mouse-fly works and keyboard didn't.)
- **Don't globally release `CuiIoWin` keyboard capture** to feed the camera keyboard: it kills the UI's
  keyboard (chat/Enter), the restore fights a mediator refcount, and the user gets stuck (had to force-kill).
  We tried it (`4ebe82b`), then removed it (`ad1dbad`). If WASD is ever wanted: feed the engine's
  `CM_walk/down/left/right` to the camera queue (`getDebugPortalCameraMessageQueue`, advertised) from a
  **non-UI-disruptive input read** (the `hkWndProcHandler` WM_KEYDOWN hook Utinni already has). Need the NGE
  `CM_*` command VALUES (used in `DebugPortalCamera.cpp:132-139`, defined elsewhere in clientGame — not found
  in a quick grep). **Currently DESCOPED.**
- **Toggle-direction must use our own engaged flag**, not `isFreeCameraActive()` (true-on-load on the editor
  client → inverts the toggle).
- **`dispatchSnapshot(cameraChangeCallbacks)` crashed on advertised** (`9fbeb45`): it fires the managed
  `OnCameraChangeCallback`, and that path's delegate thunk was a dangling CLR-JIT pointer the first time our
  toggle reached it on advertised. Skipped on advertised. (Cost: panel speed/teleport controls stay disabled
  there — cosmetic.)

---

## 4. OPERATIONAL FACTS

- **Build (native):** `MSBuild Utinni.sln -t:UtinniCore -p:Configuration=Release -p:Platform=x86 -m
  -nologo -v:minimal -nodeReuse:false` (MSBuild at `…/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/`).
  **Always `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs`** after — BUT only AFTER any ABI test
  runs (see the gotcha below).
- **TJT build:** `MSBuild "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolbox.sln" …` → outputs to
  `bin/Release/Plugins/TheJawaToolbox/`.
- **⚠️ DLL-LOCK DANCE:** a running injected `SwgClient_r.exe` locks `bin/Release/UtinniCore.dll` +
  `…/TheJawaToolboxDotNet.dll`. Close the client before rebuilding those dlls.
- **⚠️ ABI-test ordering gotcha (bit us twice this session):** the committed `Generated/UtinniCore.cs` is
  STALE (we never commit its churn). The `AbiSurfaceTests` reads the SOURCE `Generated/UtinniCore.cs`, so it
  must run against the **freshly-regenerated** cs. If you `git checkout` the cs BEFORE running the test, you
  get a bogus `REMOVED (~98)` failure. **Order: build (regen) → run ABI test → THEN checkout the cs.**
- **ABI rebless (when a `utinni::` public method is added/removed/re-signatured):** re-add the temp
  `Rebless_WhenEnvSet` `[Fact]` to `AbiSurfaceTests.cs`, build the test project, run it with
  `$env:UTINNI_REBLESS=1` (filter `~Rebless_WhenEnvSet`), then **copy the regenerated output baseline to the
  SOURCE** (`cp bin/.../net472/Fixtures/abi-baseline-blockhashes.txt UtinniCoreDotNet.Tests/Fixtures/…`),
  re-freeze the fixture (`cp the new TheJawaToolboxDotNet.dll into Fixtures/FrozenPlugin/`), revert the temp
  fact, rebuild test, run full managed suite. (extern "C" exports + hand-written `Native.cs` P/Invokes are
  NOT CppSharp-surfaced → no rebless.)
- **Headless gates:** `bin/Release/UtinniCore.Tests.exe "[endpoints]"` (417 assertions / 117-of-117);
  clang-format-20 dry-run-Werror (x64 binary at `…/VC/Tools/Llvm/x64/bin/clang-format.exe`); RVA audit
  `scripts/audit-advertised-rva-safety.ps1` (323 sites); managed `dotnet test UtinniCoreDotNet.Tests/… --no-build`
  (855/855, 1 skip = live-D3D9 vtbl).
- **Crash dump → cdb recipe (worked this session):** `cdb -z <dump.mdmp> -y "D:/Code/Utinni/bin/Release"
  -c ".reload /f UtinniCore.dll; .ecxr; kp 16; q"`. UtinniCore.pdb checksum-mismatches → nearest-EXPORT
  symbols (intermediate frames unreliable), but real exports (e.g. `GroundScene::toggleFreeCamera`) resolve.
  Dumps: `D:/Code/swg-client-v2/stage/SwgClient_r.exe-unknown.0-*.mdmp`.
- **Live smoke = maintainer only.** Launch via Utinni `Launcher.exe` (injects `SwgClient_r.exe` from
  `D:\Code\swg-client-v2\stage\`); read `bin/Release/utinni.log`. Advertised .tre/.toc from
  `D:\Code\SWGSource Client v3.0\`.
- **Cross-AI crew:** Codex (`codex exec --skip-git-repo-check -`, stdin) + Opus/Sonnet (Agent tool, model
  override) all reliable for review/consult. **`cursor-agent` flaked twice on prompt delivery this session —
  it received empty prompts; deprioritize it.**

---

## 5. Free-cam smoke recipe (for re-verification)

Relaunch advertised client → load a world → open the **FreeCam panel** → tick the **FreeCam checkbox**.
Expect: log `freecam[advertised]: ON -- changeCamera(cm_Free) (mouse-fly...)`; **right-mouse flies forward**,
mouse moves the look; untick → back to normal play; chat/Enter still work (we don't touch keyboard capture).

---

## 6. KNOWN LIMITATIONS / residual (all intentional, documented)
- **WASD descoped** (mouse-fly only). Re-enabling = feed `CM_*` via the wndproc hook (§3).
- **Panel speed/teleport controls disabled on advertised** (the `cameraChangeCallbacks` dispatch that enables
  them is skipped — it crashed). Cosmetic; the camera flies regardless.
- **Inert v13 contract rows on advertised** (bound but unused): `getDebugPortalCameraMessageQueue`,
  `gameCamera::getMessageQueue`. Harmless; removing them = a provider re-sync, not worth it.
- **`hkAlter` advertised branches** (the accessor/fail-closed code) are dead-but-guarded on advertised
  (`hkAlter` isn't installed there). Left intact to avoid touching the working SWGEmu movement path.

---

## 7. NEXT ACTIONS (resume here)
1. **More getter-shaped inspector affordances** — the safe, no-provider-dependency pattern (the inspector is
   the template). Only advertised accessors (see the §5 archaeology in the prior handoff).
2. **WASD free-cam** (if ever wanted) — the §3 wndproc-feed approach; needs the NGE `CM_*` values.
3. **Provider buckets C/D/E/F** (virtual vtable rows, mid-function toggles, WS-5 scene-ready, crash-log) —
   still open in `24-PROVIDER-HANDOFF-outstanding-editor-unlock.md`, lower priority.
4. **Reverted/parked from prior sessions** — sysmsg + target-change (need their consumer chains
   advertised-safe, not one row each); see the 2026-06-29 handoff §4.
