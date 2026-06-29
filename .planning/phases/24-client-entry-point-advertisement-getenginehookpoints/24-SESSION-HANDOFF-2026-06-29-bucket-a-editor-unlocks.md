# Phase 24 — Session Handoff (2026-06-29): BUCKET A/A-2/A-3 EDITOR UNLOCKS + WORLD-PICK

Resume pointer for the advertised-client editor-unlock arc. This session drove **Effects (B/B-2),
Chat, Radial/menus, and World-pick** to smoke-passed on the advertised NGE client, and learned
(the hard way) which unlock shapes are safe vs blast-radius-prone. Read top-to-bottom before resuming.

**Live pointers after this session:**
- **This doc** — the current state of the editor-unlock arc.
- **Consumer plan:** `24-FOLLOWUPS-PLAN.md` (status banner updated through Effects).
- **Provider ledger:** `24-PROVIDER-HANDOFF-outstanding-editor-unlock.md` (§2.B marked DONE; A/C/D/E/F open).
- **Prior handoff:** `24-SESSION-HANDOFF-2026-06-26-provider-consolidation.md` (where this picked up).
- **Memory:** `project_phase24_editor_unlock_inflight.md` (the running blow-by-blow).

---

## 0. STATUS in one paragraph

The advertised DX11/NGE client (`SwgClient_r.exe`) now has **four editor features live and smoke-passed**:
the Effects editor (live `.cef` re-play), the Chat editor (input/send/display), Radial/context menus
(NPC-click choices), and World-pick (read the selected world object's position from a TJT panel). The
engine-hookpoint contract advanced **v6 → v12 (113 names; consumer binds 111/113)** across seven provider
waves, each re-synced byte-identical + sha256-verified. Two unlocks were **reverted** (sysmsg observe-hook =
wrong-`&`; target-change = its callback dispatch wakes a stale-RVA editor chain) and two remain **parked**
(free-cam = GroundScene-null entanglement; the rest of Bucket A's editors). The session produced the
**durable architectural rule** for this client: *pure getters (poll) are safe; callback-dispatching hooks
wake editor subscriber chains full of unadvertised RVAs and can't un-gate until that whole chain is
advertised-safe.* All work is committed + pushed on both repos; CI is green-pathed.

---

## 1. What landed this session (committed + pushed)

### Utinni (`master`, a04c712 → bc09025)
| Commit | What |
|--------|------|
| `1b21255` | bind v7 Bucket B endpoints + wire `ParticlePreview` live-retrigger seam |
| `0a56994` | bind v8 Bucket B-2 endpoint + wire `ParticlePreview` `.cef` re-play seam |
| `367e7e0` | docs: mark Effects editor live preview DONE (B + B-2 smoke-passed) |
| `a7ef1db` | bind v9 Bucket A per-editor endpoints (behavior-neutral foundation, 6 rows) |
| `a51f046` | Bucket A **chat-editor unlock** (createNewWindow publish) |
| `493d24f` | chore: log the advertised-client chat publish (smoke diagnostic) |
| `3dbd5d5` | Bucket A un-gate **radial / sysmsg / menu-cursor** |
| `449d141` | **re-sync v11** — drop sysmsg wrong-`&`, add world-pick bindings |
| `701e222` | **world-pick accessor** (originally `utinni::CuiHud::getSelectedObject`) |
| `c6c1429` | un-gate **target-change** (v12 id-resolver bound) |
| `f67e170` | chore: log the target-change id-resolve (diagnostic) |
| `364448c` | **REVERT target-change un-gate** (onTarget dispatch wakes stale-RVA editor chain) |
| `9b62ae0` | fix: move world-pick getter to `CuiManager` (CuiHud namespace clash) |
| `bc09025` | chore: log world-pick hud/target ptrs in `CuiManager::getSelectedObject` |

### UtinniPlugins (`master`, → e5f1ed0)
| Commit | What |
|--------|------|
| `f50f6ed` | wire Particle editor live-preview to the Bucket B native retrigger |
| `fec1fbe` | wire ClientEffect editor Preview-in-client to Bucket B-2 `.cef` re-play |
| `ab425e7` | **wire world-pick into the Misc panel** ("Read Selected Obj") |
| `e5f1ed0` | fix: don't scene-gate Read Selected Obj (advertised client never delivers the signal) |

### Provider (swg-client-v2) — delivered the contract waves (their commits, for reference)
`db3ca5895` v7 Bucket B · `33c2a7081` v8 B-2 · `ef073dca2` v9 Bucket A · `cc1933001` v10 A-2 world-pick ·
`1693f8099` v11 A-2.1 (revert sysmsg) · `901398013` v12 A-3 network::getObjectById. Their own
context-clear checkpoint: `a225dd484`.

---

## 2. Contract state

- **`ENGINE_HOOKPOINTS_VERSION = 12`, 113 names. Consumer binds 111/113** (carve-outs:
  `consoleHelper::sendInput` + `client::wndProc`). Drift gates: `kIncCount==113`, `kBindingCount==111`
  in both `endpoints_bindings.cpp` and `endpoints_tests.cpp`.
- Re-sync discipline (every wave): copy `engine_hookpoints.{h,inc}` byte-identical from
  `D:/Code/swg-client-v2/src/game/client/application/SwgClient/src/shared/` into `UtinniCore/swg/`,
  **sha256-verify against the HANDBACK** (LF working-tree bytes, not a CRLF checkout).
- v12 sha256: `h=61586631d0883f38…`, `inc=c68d55c72652e6fd…`.

---

## 3. SMOKE-PASSED on the advertised NGE client

| Feature | Mechanism | Proof |
|---|---|---|
| **Effects editor** | `.cef` re-play via `particlePreview::replayClientEffect` (B-2), `.prt` retrigger via `particlePreview::retrigger` (B) | multiple `.cef` (medic_heal, lightsaber, dot_apply_poison, e3_atst_fire) re-played fresh on the player, visibly/audibly |
| **Chat editor** | publish the live instance via the advertised v4 `createNewWindow` funnel (the MI ctor is un-addressable → gated OFF) | typed in chat, saw it echo; `hkCreateNewWindow: published pCuiChatWindow=…` |
| **Radial / context menus** | `cuiRadialMenuManager::update` + `cuiMenu::infoTypesFindDefaultCursor` (clean advertised-clean lift-outs) | NPC-click → list of choices works |
| **World-pick** | `CuiManager.SelectedObject` → `cuiHud::g_instance` → `cuiHud::getTarget` → `Object.Transform` (all advertised); wired into Misc panel "Read Selected Obj" | selected a terminal → `Selected object @ world (-3.2, 0.6, 50.1)` |

---

## 4. REVERTED / PARKED (do not naively retry)

- **sysmsg observe-hook — REVERTED (wrong-`&`).** Bound `systemMessageManager::receiveMessage` (v9) was a
  1-arg static (`receiveSystemMessage`), but the consumer `hkReceiveMessage` is a 2-arg
  `MessageDispatch::Receiver::receiveMessage(emitter,message)` byte-stream receiver → arg misread →
  `c0000005` on world-load (region-enter fires `sendFakeSystemMessage`). Provider OMIT'd it (v11). The real
  receiver is a file-local anon `Listener` (un-advertisable). **Alts (provider-offered, not yet taken):**
  vtable-resolve the `ChatSystemMessage` Listener, OR advertise the static `sendFakeSystemMessage` as an
  INJECT row. Detour back to SWGEmu-only.

- **target-change — REVERTED (blast radius).** `creatureObject::setTarget` (→ `setLookAtTarget`, v9) and the
  v12 `network::getObjectById` resolver are **both correct** (smoke logged a non-null resolve
  `id=0x599e21ea → Object*=0x42146190`). But un-gating it makes `hkSetTarget` fire DURING LOAD → its
  `onTarget` dispatch wakes the dormant editor subscriber `WorldSnapshotImpl.OnTarget` →
  `Game.PlayerLookAtTargetObject` → `Object::getObjectById(cachedNetworkId)` → the STILL-unadvertised
  `cachedNetworkIdGetObject` RVA (`0x00B30160`) → `c0000005`. `OnTarget` also walks the unadvertised
  `WorldSnapshotReaderWriter`. Reverted to SWGEmu-only; v12 binding KEPT (inert/harmless). **This is the
  GroundScene blast-radius lesson: can't un-gate until the whole consumer chain is advertised-safe.**

- **free-cam — PARKED.** `debugCamera::alter` is virtual (consumer vtable-resolves `FreeCamera::alter`,
  index 4, via `swg::vtbl::slot`), BUT it's entangled: `processIoEvent` + the managed `FreeCamImpl` loop
  depend on `GroundScene::get()` which is deliberately NULL on the advertised client (WS-1/WS-4), and
  `hkAlter` reads RE'd offsets (`pThis+0x248`, `objectToParent`) unverified on NGE. Multi-step effort
  (extend the WS-4 GroundScene instance-latch + vtable-resolve + offset validation). Not a single row.

- **Bucket A remaining rows** — `cuiHud::actionPerformAction`/`update`, `cuiLoginScreen::activate` (SKIP
  virtuals → consumer vtable-resolve), login ctor (un-addressable). World-pick getter (cuiHud::getTarget +
  g_instance) is the one bound + USED; the rest are documented OMIT/SKIP in the A handback.

---

## 5. THE DURABLE LESSON (carry this into every future unlock)

**On the advertised client, prefer GETTERS over callback-dispatching HOOKS.**
- ✅ **Pure getters (poll)** — `CuiManager.SelectedObject` reads on demand and wakes nothing. Safe. This is
  the clean template for consuming advertised data.
- ❌ **Callback-dispatching hooks** — sysmsg (Listener), target-change (`onTarget` → WorldSnapshot editor)
  both wake editor subscriber chains written against unadvertised SWGEmu RVAs. They crash not at the hook
  but DEEP in the woken chain. Can't un-gate until that whole chain is advertised-safe.

**Advertised-safety archaeology for managed accessors** (world-pick taught this): only read accessors backed
by ADVERTISED rows. `Object.Transform` = advertised CALL (`getTransform_o2w`) → safe. `Object.NetworkId` =
raw struct-offset field → offset-fragile on NGE → AVOID. `Object.TemplateFilename` → `getTemplateFilename`
is NOT advertised (the advertised one is `getObjectTemplateName`) → AVOID. **Verify the managed property's
native EntryPoint maps to an advertised row before reading it on the advertised client.**

**MISMATCH-row discipline** (the A-2.1 sysmsg crash): a consumer `receiveMessage` hook means the
`MessageDispatch::Receiver(emitter,message)` virtual, NOT a static handler. Verify arg-count + signature of
the mapped symbol BEFORE un-gating. Still-flagged: `messageQueue::appendMessage[Data]` (bound, inert, never
un-gated → safe).

---

## 6. OPERATIONAL FACTS

- **Build (native):** `MSBuild Utinni.sln -t:UtinniCore -p:Configuration=Release -p:Platform=x86 -m -nologo
  -v:minimal -nodeReuse:false` (MSBuild at `…/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/`).
  **Always `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs`** after (CppSharp regen churn).
- **TJT build:** `MSBuild "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolbox.sln" …` → outputs to
  `bin/Release/Plugins/TheJawaToolbox/`. UtinniCoreDotNet must build first (regens the binding with new
  CppSharp surface, e.g. `CuiManager.SelectedObject`).
- **⚠️ DLL-LOCK DANCE:** a running injected `SwgClient_r.exe` LOCKS `bin/Release/UtinniCore.dll` AND
  `…/Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll` (LNK1104 / MSB3027). **The maintainer must CLOSE the
  client before any rebuild that touches those dlls**, then relaunch. This bit us 2× this session.
- **Headless gates (before any consumer commit):** `bin/Release/UtinniCore.Tests.exe "[endpoints]"`
  (currently 399 assertions / 10 cases); clang-format-20 dry-run-Werror (x64 binary at
  `…/VC/Tools/Llvm/x64/bin/clang-format.exe` — the ARM64 one can't run); RVA audit
  `scripts/audit-advertised-rva-safety.ps1` (322 sites). Managed: `dotnet test --no-build` (95/95).
- **clang-format comment-alignment ripple:** adding a row to a `{…}, // comment` table re-aligns the
  consecutive comment block → run `clang-format -i` on the file, the diff is alignment-only.
- **Live smoke = maintainer only.** Launch via Utinni `Launcher.exe` (injects `SwgClient_r.exe` from
  `D:\Code\swg-client-v2\stage\`); read `bin/Release/utinni.log`. Advertised .tre/.toc load from
  `D:\Code\SWGSource Client v3.0\`.
- **Provider report log:** `REPORT_LOG` only reaches a file if `[SharedLog] logReportLogs=1` +
  `logTarget0=file:SwgClient_report.log` are in `stage/client.cfg` (read at startup). Else it's
  OutputDebugString only (DebugView). Used this for the Effects retrigger diagnosis.
- **NGE-client caveat:** the advertised client is NGE-era; Pre-CU-RE'd hooks may target functions used
  differently (e.g. radial menus exist but inert hooks are harmless). Inert = safe; only crashes matter.

---

## 7. KNOWN NON-ISSUES (don't chase as consumer regressions)

- **Tatooine spaceport zone-in crash** — `c0000005` READ `0x4A2550A5` at `SwgClient_r.exe+0xC850B4` while
  loading `ilm_extract/.../spaceport_*_tato` shaders, ~1s in. **UtinniCore absent from the fault.
  Non-reproducible under cdb** (timing-dependent race). The PROVIDER logged it under their async
  StaticShaderTemplate / MeshAppearanceTemplate heap-corruption todo (`f78df545a`). Provider-side; re-zone
  usually loads clean.
- **transient nvwgf2um in-world CUI-render crash** (prior handoffs) — NV-driver-layer, not a regression.

---

## 8. NEXT ACTIONS (resume here)

1. **More getter-shaped consumables (the safe pattern).** World-pick is the template: surface more advertised
   reads as TJT affordances (e.g. richer selected-object inspector — but ONLY advertised accessors:
   Transform/Position works; NetworkId/TemplateFilename do NOT — see §5). No provider dependency.
2. **Unlock sysmsg / target-change PROPERLY** (if wanted) — these need their CONSUMER CHAINS advertised-safe,
   not one row: sysmsg → vtable-resolve the Listener OR add a `sendFakeSystemMessage` inject row;
   target-change → advertise `cachedNetworkIdGetObject` AND make `WorldSnapshotImpl.OnTarget` advertised-safe
   (or gate the managed subscriber off on advertised). Both are multi-part.
3. **free-cam** — the dedicated multi-step effort (GroundScene instance-latch + vtable-resolve + offset
   validation). Biggest remaining single editor.
4. **Provider buckets C/D/E/F** (virtual vtable rows, mid-function toggles, WS-5 scene-ready, crash-log) —
   still open in `24-PROVIDER-HANDOFF-outstanding-editor-unlock.md`; lower priority.
5. **Optional cleanup:** the per-click diagnostic logs in `CuiManager::getSelectedObject` (bc09025) and
   `hkSetTarget` (f67e170, now inert) can stay or be trimmed — both are bounded/user-initiated.

---

## 9. THE BIG PICTURE

The advertised-client editor-unlock arc went from "RENDER-only + provider-bound" (start of session) to
**four smoke-passed editor features** plus a clear, reusable map of what's safe (getters) vs what's blast-
radius-prone (callback hooks). The contract is a well-oiled per-wave cadence (provider advertises → consumer
re-syncs/binds/wires → maintainer smokes), now at v12. The remaining work is well-characterized and parked
with reasons, not mysteries. Resume from §8.
