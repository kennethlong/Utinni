# Phase 24 — Session Handoff (2026-07-03): Inspect Camera + sysmsg SEND arc + WorldSnapshot wave 1

Resume pointer after a 3-day session arc (07-01 → 07-03) that (1) shipped the **Inspect Camera**
inspector row, (2) ran the **sysmsg SEND** capability end-to-end — request → v14 crash → ABI-class
discovery → v15 shim → **smoke-PASSED**, and (3) opened the **WorldSnapshot chain** work with wave 1
(target-change un-gated, **SMOKE PENDING** — the immediate resume point). Everything committed + pushed
on both repos; both working trees clean. Read top-to-bottom.

**Live pointers:**
- **This doc** — session recap + the resume point (§5) + next-move ladder (§6).
- **Running arc ledger:** `project_phase24_editor_unlock_inflight` memory (has the full detail).
- **New durable rule:** `feedback_advertised_cpp_object_params` memory (the §2 ABI lesson).
- **Provider outstanding:** `24-PROVIDER-HANDOFF-outstanding-editor-unlock.md` (buckets C/D/E unchanged).
- **Prior handoff:** `24-SESSION-HANDOFF-2026-07-01-milestone-closeout.md`.

---

## 0. STATUS in one paragraph

Post-v2.1 editor-unlock arc continues. **Inspect Camera** (UtinniPlugins `c1f995b`) closed out the
pure-getter inspector vein — managed-only, smoke deferred-trivial. **Sysmsg SEND is COMPLETE and
smoke-passed**: the TJT broadcast box + per-save editor feedback inject system messages on BOTH clients
(advertised via the v15 `sendMessageUtf8` extern-C shim, SWGEmu via the untouched WString literal); the
v14 direct-`&fn` attempt crashed and yielded the durable **C++-object-param ABI rule** (§2), now baked
into both repos' contract comments. **WorldSnapshot wave 1** (Utinni `e7eec33`) mapped the whole
OnTarget chain, guarded its two unadvertised natives, and **un-gated `creatureObject::setTarget` on the
advertised client — the maintainer live smoke of that un-gate is the immediate next action (§5).**
SWGEmu byte-unchanged throughout (D-00). Contract now **v15 / 120 names / 118 bound**.

---

## 1. What landed this session (all committed + pushed)

### Utinni (`master`)
| Commit | What |
|--------|------|
| `5974337` | docs: sysmsg SEND provider request rev-1 (direct `&fn` — later proven wrong) |
| `a78083d` | bind v14 `systemMessageManager::sendMessage` (+skew-guard groundwork; drift gates 119→120/117→118) |
| `de336bf` | version-skew guard on the SEND wrapper (first advertised CALL row with a pre-existing SWGEmu literal) |
| `bc4005b` | **HOTFIX after smoke crash:** block ALL WString-passing advertised calls (sendMessage + writeToAllTabs/writeToCurrentTab) |
| `c5d2d2b` | docs: rev-2 request — extern-C utf8 shim, name-REPLACE (v15) |
| `23e250a` | bind v15 `sendMessageUtf8` shim; wrapper splits advertised→shim / SWGEmu→WString literal. **SMOKE PASSED** |
| `e7eec33` | **WorldSnapshot wave 1**: un-gate target-change on advertised (§4). **SMOKE PENDING** |

### UtinniPlugins (`master`)
| Commit | What |
|--------|------|
| `c1f995b` | MiscPanel **Inspect Camera** button — Position/Yaw/Pitch advertised-safe; lens fields SWGEmu-only (§3) |
| `e93567f` | **sysmsg SEND wiring**: `TJT.SWG.SysMsg` helper + ClientReloadDispatcher per-save feedback + MiscPanel broadcast row |

### swg-client-v2 (provider, his instance — for reference)
| Commit | What |
|--------|------|
| `d33f34e2d` | v14 direct row (delivered to rev-1 spec; the spec was wrong) |
| `bd653dccf` | **v15 rev-2**: name-REPLACE → `extern "C" utinni_sendFakeSystemMessage(const char*, bool)` (widens via `Unicode::narrowToWide` provider-side) |
| `f344d1035` | (unrelated bonus) zone-in heap corruption fix — terrain ShaderCache lock |

---

## 2. THE BIG LESSON — C++-object params across the advertised boundary

The v14 sysmsg row crashed live smoke (WRITE-AV, VEH rva `0xDBA770`) despite resolving correctly and
targeting the right function. **Signature-level ABI match ≠ layout-level match:**

- Consumer `swg::WString` models the **2002 SWGEmu** string: 3 pointers (`begin/end/allocEnd`, 12 B).
- Provider v145 `Unicode::String` is a modern MSVC `basic_string`: SSO union @0, `_Mysize` @16,
  `_Myres` @20 (24 B). Engine read a garbage size past our object → wild write.

**RULE (memory `feedback_advertised_cpp_object_params`, also in both repos' contract comments):**
- Only **primitives and pointers** cross an advertised consumer→engine CALL raw.
- Any C++ object param needs a **provider `extern "C"` shim** (char*/primitives, object constructed
  provider-side) — the `utinni_replayClientEffect` precedent.
- Prefer **name-REPLACE** when changing a row's ABI (skewed pairings miss-by-name and degrade).
- **Detoured** rows are unaffected (consumer passes engine-built args through untouched); reading INTO
  engine objects from consumer code is the same trap in reverse.
- Consumer-blocked latent twins (same trap, no shim yet): `CuiChatWindow::writeToAllTabs` /
  `writeToCurrentTab` (blocked in `bc4005b`; rev-2 request §3 offers the optional shim pattern).

Also of note from the v14 wave (still true, though the shim made it moot here): a NEW advertised CALL
row whose slot carries a **pre-existing SWGEmu literal** (instead of starting null) needs a version-skew
story — a null-starting slot + null-check is the clean pattern (what v15 uses).

## 3. Sysmsg SEND — final shape (COMPLETE, smoke-passed 2026-07-03)

- **Contract:** v15, `systemMessageManager::sendMessageUtf8` → `utinni_sendFakeSystemMessage(const
  char*, bool chatBoxOnly)`; the v14 `sendMessage` name is GONE (replace). RECEIVE stays OMIT (the real
  inbound receiver is a file-local anonymous `MessageDispatch::Receiver` Listener — un-advertisable;
  do NOT map the adjacent `receiveSystemMessage` static, that was the v9→v11 crash).
- **Native:** `SystemMessageManager::sendMessage(const char*, bool)` splits: advertised → null-checked
  utf8 shim slot; SWGEmu → the WString literal (0x008AC250), byte-unchanged.
- **Managed:** `TJT.SWG.SysMsg` — `Notify(msg)` = quiet "[Utinni] …" chat-box-only confirmation;
  `Broadcast(msg)` = full system-message treatment. Both main-loop-marshaled + `Game.IsRunning`-gated.
  Wired: `ClientReloadDispatcher` per-tier save feedback ("saved X — terrain reloaded / textures
  reloaded / reloads on next scene change") + MiscPanel broadcast row (textbox + "Send Sysmsg").
- **Inspect Camera** (same MiscPanel): Position/Yaw/Pitch via the advertised transform chain; lens
  fields (FOV/clip/viewport/projection) are raw Camera struct reads → SWGEmu-only, "(n/a on
  advertised)" there. FOV is stored in radians (shown in degrees). Decision: lens fields stay
  SWGEmu-only (option 3) — not modder-critical; a v-next camera-getter accessor ask exists as an
  option but is NOT planned.

## 4. WorldSnapshot chain — wave 1 (the mapped attack)

**The full OnTarget chain map** (the 06-29 `364448c` revert autopsy — the hook was never the problem):

```
hkSetTarget (fine; passes the v12-resolved Object* to native subscribers)
  └→ onTarget dispatch → WorldSnapshotImpl.OnTarget (managed callbacks are ARGLESS → it re-reads)
       ├→ Game.PlayerLookAtTargetObject
       │    ├→ +1432 raw CreatureObject offset      ← 2002 layout, wrong field on NGE (§5-fragile)
       │    └→ getCachedObjectById 0x00B30160        ← unadvertised literal = THE 06-29 crash frame
       └→ WorldSnapshotReaderWriter.Get().GetNodeById(target…)
            └→ get() = raw 0x1913E94 static ptr + raw nodeList walks   ← needs non-null target
```

**Key insight:** `OnTarget` already has a safe null-branch, and all ~19 managed
`WorldSnapshotReaderWriter.Get()` sites require a non-null target / an enabled gizmo / panel
interaction. Guarding the two natives to null on advertised makes the entire chain degrade — zero
managed changes, zero provider rows.

**Wave 1 (`e7eec33`):**
- `Game::getPlayerLookAtTargetObjectNetworkId` → 0 on advertised (kills the +1432 offset read).
- `Network::getCachedObjectById` → null on advertised (kills the 0x00B30160 call).
- `creatureObject::detour()` lifted out of the `!advertised` INPUT block into its own `!skipInput`
  gate (installable() self-gate; setTarget = the v9 advertised `setLookAtTarget` real entry) —
  **target-change is UN-GATED on the advertised client.**
- WS-3's `offlineSnapshotUnavailable()` (world_snapshot.cpp:135) still gates the __thiscall wrappers;
  `WorldSnapshotReaderWriter::get()` itself remains an unguarded raw literal — acceptable because
  nothing reaches it on advertised (Goal-B territory).

## 5. RESUME HERE — the pending smoke

**Maintainer live smoke of wave 1 (advertised NGE client):**
1. Relaunch the advertised client (stage exe is current; Utinni `e7eec33` built + pushed — rebuild NOT
   needed unless bin/ was cleaned; remember the DLL-lock: close the client before any rebuild).
2. Load a world → **target several NPCs/objects**, including during/right after scene load (the exact
   timing that crashed on 06-29).
3. Watch `bin/Release/utinni.log`: `hkSetTarget: id=0x… -> network::getObjectById resolved
   Object*=0x…` (bounded, first 10) — and **NO crash**. Snapshot panel shows no-target (expected).
4. Quick SWGEmu pass: with the Snapshot editor open, target a snapshot object — node selection +
   gizmo must still work (guards are advertised-only; SWGEmu is byte-identical).
5. Also still nominally pending (trivial): Inspect Camera smoke (`c1f995b`) — click it once on each
   client; and the sysmsg editor-feedback line on a `.trn` save (the broadcast box itself already
   smoke-passed).

If the wave-1 smoke crashes: cdb the dump (`cdb -z <dump> -y <bin/Release> -c ".reload /f
UtinniCore.dll; .ecxr; kp 16; q"`), and suspect a subscriber path not on the §4 map.

## 6. NEXT-MOVE LADDER (after the smoke)

1. **Goal A+ (cheap, 1 provider row):** advertise a lookAt-target id accessor (e.g.
   `creatureObject::getLookAtTargetId` returning the NetworkId value/ptr); combine with the
   already-bound v12 `network::getObjectById` → `Game.PlayerLookAtTargetObject` returns real objects
   on advertised → target-aware affordances (inspector auto-refresh on target change, etc.). Draft the
   request off the wave-1 map; note the managed onTarget callbacks are ARGLESS — a target-carrying
   managed callback would be an ADDITIVE new API (binary-compat lesson: never add params to existing
   public methods).
2. **Goal B (milestone-scale, provider-heavy):** the Snapshot editor actually working on the
   advertised client. The reader/writer singleton (0x1913E94) + every `Node` field are raw 2002-layout
   — the §2 ABI rule at struct scale. Do NOT hand-port offsets. Needs a provider design consult first:
   handle-based node accessor API vs serialized snapshot exchange. Phone the crew on the design too
   (the WS-4 latch lesson: prefer dedicated exports over widening shared accessors).
3. **Or pivot to v2.2** (`/gsd:new-milestone`): backlog candidates 999.8 (remaining Wave-2 editors),
   999.10 (installer/onboarding), 999.9 (Wave-3 plugins).

## 7. OPERATIONAL FACTS (carried forward + this session's additions)

- **Contract:** v15 / 120 names / 118 bound (2 carve-outs). Drift gates: `endpoints_bindings.cpp`
  static_asserts + `endpoints_tests.cpp` REQUIREs (120/118) — bump BOTH on any contract change.
  Re-sync `engine_hookpoints.{h,inc}` byte-identical + sha256-verify both repos.
- **Build (native):** `MSBuild Utinni.sln -t:UtinniCore -p:Configuration=Release -p:Platform=x86 -m
  -nologo -v:minimal -nodeReuse:false`. Always `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs`
  after (only AFTER any ABI test runs against the fresh cs).
- **TJT build:** `MSBuild "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolbox.sln" …
  -t:TheJawaToolboxDotNet` (UtinniCoreDotNet builds first via the Utinni solution).
- **⚠️ DLL-lock:** a running injected `SwgClient_r.exe` locks `bin/Release/UtinniCore.dll` +
  `TheJawaToolboxDotNet.dll` — close the client before rebuilding.
- **Headless gates:** Catch2 `bin/Release/UtinniCore.Tests.exe "[endpoints]"` (currently 420
  assertions / 10 cases); clang-format-20 at
  `VC/Tools/Llvm/x64/bin/clang-format.exe` (NOT the ARM64 one that `find` hits first); RVA audit
  `scripts/audit-advertised-rva-safety.ps1 -SwgRoot UtinniCore/swg` (323 sites; run via the PowerShell
  tool — `$PSScriptRoot` breaks under bash `powershell -File`); managed `dotnet test
  UtinniCoreDotNet.Tests --no-build -c Release` (855/855 +1 skip).
- **ABI rebless** only when the `utinni::` public surface changes (this whole session needed none).
- **VEH crash triage:** `utinni.log` VEH lines give code/EIP/rva/stack first — check before cdb.
- **Live smoke = maintainer only** (advertised client from `D:\Code\swg-client-v2\stage\`; .tre/.toc
  from `D:\Code\SWGSource Client v3.0\`).
- **Provider round-trip discipline:** VERIFY delivery before binding (version bump, row target, exe
  rebuilt via `dumpbin /exports`, tree COMMITTED — the v14 wave was left uncommitted until nudged).
- **Cross-AI crew:** Codex reliable; Agent-tool `sonnet`/`opus` (bare aliases) reliable; cursor-agent
  deprioritized (flaked on prompt delivery).
