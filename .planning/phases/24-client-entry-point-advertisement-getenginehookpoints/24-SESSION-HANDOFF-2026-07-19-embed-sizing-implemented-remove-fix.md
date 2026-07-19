# Phase 24 — Session Handoff (2026-07-19): embed render-sizing IMPLEMENTED ("very close"); SWGEmu remove regression root-caused + fixed (re-smoke pending)

Continuation of the 2026-07-18 handoff. Two arcs this session: (1) the gizmo-aspect/embed
render-sizing fix went from crew consult → full implementation, and the maintainer's first
advertised smoke says **"very close"** — a comparison pass against SWGEmu was started to identify
the residual tweak, but got interrupted by (2) a **SWGEmu remove regression**, which was
root-caused (an old `isSafeToUse` OR→AND change, NOT this session's work), fixed, and awaits its
confirmation smoke. Restart begins at §3 (the two pending smokes).

**Repo state:** Utinni `80cd9a4` (pushed through `4944883`; `80cd9a4` + handoff local at write
time) · UtinniPlugins `461c2c1` (local). Trees clean apart from this doc. `bin/Release` staged
binaries are CURRENT (both repos rebuilt after every change; all gates green).

**`ut.ini` is currently pointed at SWGEmu** (`D:\SWGEmu-Client\SWGEmu\SWGEmu.exe`); the advertised
values are kept as a comment right above (`SwgClient_r.exe` / `D:\Code\swg-client-v2\stage\`).

---

## 1. Embed render-sizing (gizmo aspect) — IMPLEMENTED, smoke says "very close"

Full arc: crew consult (4 reviewers: Codex, Cursor, Opus, Fable — unanimous **Design A**, spec at
`24-CREW-CONSULT-embed-render-sizing.md`, commit `d9bb55b`) → implementation `4944883` (pushed):

- **Launcher** (`Launcher/main.cpp`): SEC_IMAGE-maps the target exe, walks its export table for
  `GetEngineHookPoints`; on the advertised client ONLY, deletes any stale
  `<clientDir>utinni_embed.cfg` and merge-aware-appends ` @utinni_embed.cfg` into the post-`--`
  region (creates the ` -- ` only if absent; token is CWD-relative because the engine's `@` parser
  has no quote support and the client dir has spaces). SWGEmu launches get a byte-identical
  command line.
- **Managed** (`main.cs` + `Utility/EmbedResolution.cs` + `FormMain.GamePanelClientSize`
  internal): advertised+editorMode path constructs FormMain, forces `Maximized`, `Show()`s,
  measures the INNER `PanelGame.ClientSize`, validates (≥1024×768, strictly < desktop both axes),
  atomically writes `[ClientGraphics] screenWidth/screenHeight/borderlessWindow=true` — all
  BEFORE `SignalLauncherReady()` (game spin-parked at entry → guaranteed to parse the fresh file
  after client.cfg → override wins → backbuffer created at embed size). `borderlessWindow=true`
  defuses the checkDisplayMode axis-equality forced-fullscreen trap + makes AdjustWindowRect
  exact. Failure clears the cfg → engine falls back to client.cfg (stretched but alive). SWGEmu
  ordering unchanged (form still constructed inside `Application.Run`).
- **Native**: advertised-only first-Present assert in `directx9.cpp` hkPresent one-shot
  (`embed-aspect first-present: bb=WxH window-client=WxH windowed=N -> OK|MISMATCH`, critical on
  mismatch) + per-frame `g_embedAspectOk` latch in `imgui_impl.cpp` newFrame (RT vs client ±1px,
  edge-transition logs) that **hard-disables the gizmo** in `imgui_gizmo::draw()` when broken
  (fail-closed, no-half-working). The old `gizmo-diag` block was stripped; the RT-space
  mouse/DisplaySize overrides KEPT (identity when RT==client; SWGEmu still needs them; 3–1 crew
  vote, Fable's decisive argument: the block isn't advertised-gated so removal would diff SWGEmu).

**OPEN: the "very close" residual.** The maintainer's advertised smoke after `4944883` reported
"very close" and wanted the SWGEmu side-by-side to articulate the tweak — the remove regression
interrupted before details landed. NEXT SESSION: get the comparison verdict + the exact symptom.
Diagnose from numbers, not guesses: `utinni.log` `embed-aspect first-present` line (is bb ==
window-client == expected embed size? windowed=1?) and any `embed-aspect ... MISMATCH`
transitions. Suspects if a small residual exists: title-chrome/UtinniForm 32px padding in the
measured panel vs repositioned rect; a 1–2px AdjustWindowRect delta; the panel measured before
final layout (check the logged `EmbedResolution: wrote ...` dims vs
`PanelGame.RepositionSwgWindow` dims — 07-19 SWGEmu log shows reposition to 1455x1040, panel
{0,0,1455,1040}, so compare the advertised equivalents).

## 2. SWGEmu remove regression — ROOT-CAUSED + FIXED (`80cd9a4`), re-smoke pending

- **Symptom:** on SWGEmu, snapshot Remove silently did nothing to the in-world object.
- **NOT caused by** Goal B or the embed work: triage diag (managed + native step logging) painted
  the path in ONE attempt: `RemoveNode: ENTRY EnableNodeEditing=True IsMutationAvailable=False`
  → `RemoveNode[legacy]: target networkId=9995372 parent=False node=FOUND` →
  **`Node::removeNode: isSafeToUse=FALSE -> no-op`**.
- **Root cause:** the 24-02 bind wave (`1b5ea51`) changed `Game::isSafeToUse()`'s SWGEmu two-flag
  read from `||` to `&&` on internals.md's "both must be true" model (CON-O-01: doc as source of
  truth). Today was the FIRST live SWGEmu snapshot mutation since; in a fully-loaded in-world
  Core3 session one flag stays unset → the AND fenced EVERY world-snapshot mutation (add/radius
  equally dead, not just remove).
- **Fix (`80cd9a4`):** reverted to `||` (field-proven semantics), corrected
  `docs/ai/internals.md:230-231`, recorded the counter-evidence in the code comment.
- **Diag kept for the confirmation smoke** (bounded, fires only on user action): managed
  entry/branch/target/node lines (UtinniPlugins `461c2c1`) + native
  ENTRY/`safeFlags={a,b}`/getObjectById/EXIT lines (`world_snapshot.cpp`). The `safeFlags` dump
  will record WHICH flag is unset — capture it, put it in internals.md, then STRIP all diag
  (marked "DIAG 2026-07-19", grep `2026-07-19` in `world_snapshot.cpp` + `WorldSnapshotImpl.cs`).
  The 2 diag literals are baselined in `advertised-rva-baseline.tsv` (325) — remove those rows
  when stripping.
- **CONFIRMATION SMOKE:** relaunch SWGEmu → target snapshot object → Remove → object despawns +
  log shows `Node::removeNode: ENTRY ... safeFlags={a,b}` → `getObjectById -> 0x... -> remove()`.
  Also spot-check ADD still works (same gate).

## 3. Restart checklist (in order)

1. **SWGEmu confirmation smoke** (§2): remove despawns; record `safeFlags` values.
2. **Strip the triage diag** (both repos) + internals.md flag note + drop the 2 baseline rows
   (325→323) + paired commits.
3. **Advertised comparison** (§1): flip `ut.ini` back (comment block has the values), get the
   "very close" residual articulated + the `embed-aspect first-present` numbers; tweak from
   evidence. Gizmo standard unchanged: ships only when a drag on every axis anywhere tracks
   perfectly ([[feedback_gizmo_no_half_working]]).
4. Then the still-owed items: **Wave-3 save/reload live smoke** (advertised;
   `wsSelfTestSaveOnLoad=1` key), rest of **SWGEmu D-00** (this session already re-validated
   inject/render/overlay/targeting/chat on SWGEmu incidentally).

## 4. Enter key / fullscreen restyle — now confirmed CROSS-CLIENT (own focused pass)

Maintainer confirms SWGEmu has the same Enter problem as the advertised embed: the app sometimes
swallows Enter, and SWG's Enter→window-level-fullscreen restyle fires. 07-19 log evidence
(SWGEmu): `WM_KEYDOWN vk=0x0D` → `hkChatEnter: chat is in display mode -- overriding to open chat
input` → `PanelGame.ReassertEmbed: window-level fullscreen restyle detected ... re-asserted
owned-popup embed` — the watchdog re-docks it (safety net works) but the root cause is untreated.
Existing pieces: WS-2 Enter-mask is ADVERTISED-only (`[Editor] advertisedEnterMask`, DI-buffer
mask, scene-gated); [[project_swg_context_routing]] has the SWGEmu-side lead (Utinni breaks SWG's
CUI key-context selector; in-game Enter→chatEnter not openChat; fix sketch = detour the
wrong-context handler 0x00F3E420). On advertised, a restyle-induced size change also can't resize
the backbuffer (never-Reset) — the new gizmo fail-closed gate + embed clamp handle it gracefully,
but the keystroke itself should never reach the fullscreen toggle while embedded. SCOPE for the
pass: one Enter-routing design covering BOTH clients (mask/route at the shared layer, not two
client-specific patches), + verify the restyle can no longer fire while embedded.

## 5. Durable notes from this session

- **Probe-first paid off again:** one instrumented attempt beat hours of static diff-hunting; the
  managed+native paired step-diag pattern (entry/branch/target/node/gate/despawn) is reusable for
  any silent-no-op editor regression.
- **Doc-model vs field-truth:** a doc-driven semantic "correction" (OR→AND) sat latent for weeks
  because no SWGEmu mutation smoke ran after it. When a doc model contradicts years of field
  behavior, validate live before enforcing it. internals.md now carries the empirical note.
- **Cross-client smoke debt is real:** Goal B's advertised waves were all smoke-passed, but the
  shared-path change that actually broke SWGEmu came from an unrelated earlier wave. The D-00
  discipline (SWGEmu smoke after advertised-heavy phases) caught it exactly as designed.
- Crew-consult mechanics (for reuse): self-contained brief file + Codex stdin, cursor-agent short
  pointer prompt (long args overflow cmd), in-harness Agent(opus/fable) with verify-the-brief
  instructions. Fable caught what the panel's other three missed (spaced-path @file blocker,
  axis-equality fullscreen trap, SWGEmu cmdline gating, DPI manifest answer).
