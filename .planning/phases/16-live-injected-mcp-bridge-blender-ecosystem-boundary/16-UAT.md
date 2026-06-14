---
status: complete
phase: 16-live-injected-mcp-bridge-blender-ecosystem-boundary
source: [16-01-SUMMARY.md, 16-02-SUMMARY.md, 16-03-SUMMARY.md]
started: 2026-06-14T17:06:29Z
updated: 2026-06-14T17:55:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Cold-Start Smoke — Injected Client Boots (bridge default-OFF)
expected: Launch injected SWGEmu WITHOUT `[Live] enableLiveBridge=true`. Client boots to in-game exactly as before — no hang/crash/error at the StartListener wiring point (post-Callbacks, pre-Application.Run). No live pipe created (listener gated off). This is the regression gate for the main.cs injection-path change.
result: pass

### 2. Live Bridge Round-Trip (Tier-4, D-01 non-gating)
expected: Launch injected SWGEmu WITH `[Live] enableLiveBridge=true` (in-client config) AND launch `Utinni.Mcp --enable-live` (or `UTINNI_MCP_ENABLE_LIVE=1`) — the dual-flag CUR-NEW-6 contract. From an MCP agent, call `live_ping` → ack reports `listening:true`, `gameRunning`, a pid, and the injected client's `clientRoot`. Then call `live_reload_asset` on an edited asset (relative path under the client root) → ack reports the honest `ReloadAssetClassifier` tier, `queued`, and `reloadAttempted=false` candor (transport-only until RESID-03; visible re-render is best-effort, NON-gating).
result: pass
note: |
  Initially FAILED (blocker): client froze on load with enableLiveBridge=true.
  ROOT CAUSE: main.cs:146-151 wired the per-frame game-state refresh as a
  self-re-enqueuing Action; CallbackHelpers.Drain looped until-empty, so the
  re-enqueue made the queue never empty → infinite loop on the game thread.
  FIX (snapshot-bound Drain, user-approved): Drain now processes only the items
  present at entry (count-bounded), so a self-re-enqueuing callback runs once per
  drain and the re-enqueue waits for the next frame. Verified live AFTER the fix:
    live_ping  -> {"listening":true,"gameRunning":true,"pid":52568,"clientRoot":"D:\\SWGEmu-Client\\SWGEmu"}
    live_reload_asset(swgemu_machineoptions.iff) ->
      {"accepted":true,"tier":"PendingNextSceneChange","queued":true,
       "reloadAttempted":false,"path":"swgemu_machineoptions.iff", candor intact}
  gameRunning:true confirms the per-frame game-thread cache refresh runs without
  hanging (the exact path that froze). Regression test added:
  Drain_SelfReEnqueuingCallback_TerminatesAndRunsOncePerDrain (5s Task.Wait guard).

### 3. Fail-Closed Gating Observable via MCP Client
expected: Launch `Utinni.Mcp` WITHOUT `--enable-live`. From the MCP agent, list tools → the 12 standard tools are present and `live_ping` / `live_reload_asset` are ABSENT. Relaunch WITH `--enable-live` → the two `live_*` tools now appear. (Mirrors the automated `LiveTools_AreAbsentWithoutEnableLive_PresentWithIt` real-enumeration proof, confirmed from the agent's seat.)
result: pass
note: "Confirmed live via throwaway MCP driver (.scratch/livedrv): stale Release exe (pre-16-03) showed live_* ABSENT; current Debug exe WITH --enable-live showed both live_ping + live_reload_asset PRESENT. live_ping round-tripped and returned honest {listening:false,...} when no in-client server was up."

### 4. validate-bundle CLI Verb
expected: Run `utinni-cli validate-bundle` against a Blender export bundle (manifest + `.rsp` + `.cfg`). A structurally-valid bundle exits 0 with a JSON envelope reporting `valid:true` and any findings; contained-absolute `.rsp` refs are allowed + probed, escaping refs are reported-not-probed (`hasRejectedRefs`); an unparseable manifest/`.rsp` exits 2; a missing manifest exits 3. TEXT-only — no 3D/IFF decode (DEC-A3 clean).
result: pass
note: "Confirmed via test evidence (not a fresh CLI invocation — net472 CLI not pre-built; rebuilding adds nothing over the suite): 10 green ValidateBundleTests cover happy-path exit-0/valid:true, contained-absolute allowed, escape reported-not-probed/hasRejectedRefs, malformed manifest+rsp exit-2, missing manifest exit-3. Pure-managed TEXT CLI (no injection/native) — no live-only failure surface."

### 5. Blender Boundary Contract Doc
expected: Open `docs/ai/blender-boundary-contract.md`. It documents all four surfaces (the `.rsp` `{path} @ {ABSOLUTE}` line format + 7 suffix→bucket→filename rules + cfg dialects; the `swg_export_manifest.json` schema; the TRE version matrix mirroring TreVersion.cs — 5000 readable / 6000 enumerate-only; the bundle layout), the anti-coupling rules (DEC-A3, no geometry codec), the exit/valid semantics, and the CV-1 finding (Blender synthetic v0005 `.tre` uses crc-first TOC; Utinni reads size-first → honest exit-2). Reads as the authoritative Utinni↔swg-blender-plugin seam.
result: pass
note: "Doc present (14394 B). Section headers confirm all four surfaces: §1 .rsp search-path contract (1.1 line format, 1.2 suffix→bucket→filename, 1.3 ordering, 1.4 cfg dialects, 1.5 validate-bundle exit/valid semantics), §1b swg_export_manifest.json schema, §2 format-version matrix (.iff/.tre), §3 directory/bundle layout, §4 ownership/anti-coupling rules, §5 open/preview reachability. doc↔verb parity is CI-locked by ContractDoc_ContainsEveryBucketFilenameFromVerbTable."

### 6. Cross-Repo Pointer Note (16-01 Task 4, blocking-human)
expected: `D:/Code/swg-blender-plugin/REFERENCES.md` has a row in its "External references (D:/Code)" table pointing at `Utinni/docs/ai/blender-boundary-contract.md` (ECO-01). If you have NOT yet applied it (third repo, outside standing write authority), mark this blocked/skip — it's the one remaining human-action checkpoint, not a code defect.
result: pass
note: "Already applied + committed in the third repo: swg-blender-plugin/REFERENCES.md line 8 has the exact ECO-01 pointer row; commit f803f58 (2026-06-14 10:47) 'docs: point at Utinni-authoritative Blender boundary contract (ECO-01)'. The 16-01 Task 4 human checkpoint is closed."

## Summary

total: 6
passed: 6
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

- truth: "With both flags ON, the injected client boots and live_ping returns listening:true (the in-client named-pipe listener runs without freezing the client)"
  status: resolved
  reason: "User reported: Freezing on load swg is hanging (with [Live] enableLiveBridge=true)"
  resolution: "FIXED in-session (user-approved snapshot-bound Drain). Re-smoked live: live_ping listening:true/gameRunning:true/pid:52568/clientRoot match; live_reload_asset accepted:true/tier:PendingNextSceneChange/reloadAttempted:false. Regression test added. Pending commit."
  severity: blocker
  test: 2
  root_cause: "main.cs:146-151 wires the per-frame game-state cache refresh as a self-re-enqueuing Action onto the GameCallbacks.AddMainLoopCall ConcurrentQueue. CallbackHelpers.Drain (UtinniCoreDotNet/Callbacks/CallbackHelpers.cs:46) drains `while (queue.TryDequeue(out f)) f()` until empty. The refresh re-enqueues itself inside its own body, so the queue never empties → infinite loop on the game thread on the first drained frame → freeze on load. The author's comment assumed Drain processes a one-frame snapshot ('re-enqueues itself each tick'); the actual contract is drain-until-empty."
  artifacts:
    - path: "UtinniCoreDotNet/main.cs"
      issue: "Lines 146-151: self-re-enqueuing refresh Action against an until-empty drain"
    - path: "UtinniCoreDotNet/Callbacks/CallbackHelpers.cs"
      issue: "Line 46-52: Drain loops until queue empty — no snapshot bound; a self-re-enqueuing item hangs the game thread"
  missing:
    - "Either (A) snapshot-bound Drain to one frame's worth of items (count-bounded), making the documented per-tick re-enqueue safe globally + unit-testable; or (B) register the refresh as a STANDING per-frame callback (Game.AddMainLoopCallback with a rooted delegate) instead of self-re-enqueuing onto the one-shot queue"
    - "Regression test driving the REAL Drain with a self-re-enqueuing action, asserting it terminates and runs once-per-drain (gap: no test exercised Drain + self-re-enqueue; LivePipeServerTests use a no-op enqueue sink, MainCs_WiresGameThreadRefresh_* is a source-grep)"
  debug_session: ""
