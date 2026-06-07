# Phase 15: Wave-2 editors (WorldSnapshot, Particle) + presentation residuals - Research

**Researched:** 2026-06-07
**Domain:** SWG `.prt` particle-effect IFF codec (new), TJT MEF SubPanel editor surface, D3D9 windowed/fullscreen presentation, live-reload candor
**Confidence:** HIGH for WorldSnapshot + the MEF seam + RESID-04 mechanism; MEDIUM for the `.prt` codec depth (large nested format, no Utinni fixtures); MEDIUM for the live-preview runtime hook (D-09 open item)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**WorldSnapshot editor delta (PROD-W2-WS)**
- **D-01:** Wave-2 delta = a **flat placements list/table + multi-select bulk operations** layered on top of the shipped in-world gizmo panel. Browsable table of ALL nodes (id, object-template name, cell, position) with search/filter and click-to-select driving the existing gizmo, PLUS multi-select bulk move / delete / retemplate.
- **D-02:** Reuse the shipped `WorldSnapshotReaderWriter` / `WorldSnapshotImpl` — **zero new format work**. Table is a new view over existing data; bulk ops compose existing per-node edit commands.
- **D-03:** Conform to the unchanged Wave-1 `IEditorPlugin.GetSubPanels()` seam + the canonical singleton **hide-not-dispose** pattern from Phase 8 (CON-T-05 `*Impl` separation).

**Particle `.prt` codec depth (PROD-W2-PRT)**
- **D-04:** **Full typed decode** of the `.prt` / client-effect format — type emitter / wave / timing / color fields, modeled on the `swg-client-v2` `clientParticle` C++ reference.
- **D-05:** **Degrade, never abort.** On a `.prt` variant/field not covered by the reference, preserve the unrecognized chunk/field as **raw bytes** so save round-trips byte-safe; editor greys out what it cannot type. Mirrors the OT-multichunk degrade-don't-abort precedent.
- **D-06:** Codec lives in `UtinniCoreDotNet` alongside other `Formats/*` codecs (format logic out of TJT and out of MCP server, per Phase 14).

**Particle editor surface, preview, AI-assist (PROD-W2-PRT)**
- **D-07:** **AI is read-assist only** in V1 — explains/summarizes the loaded effect and suggests changes as text; modder applies edits manually. AI never writes `.prt` bytes. No prompt-to-mutate this phase.
- **D-08:** AI read-assist on **both surfaces**: (a) `.prt` codec exposed via new `utinni-cli` verbs + MCP read/summarize tools, AND (b) an in-TJT assist button in the Particle SubPanel reusing that same read path inline while injected. The in-app button reuses the CLI/MCP path — no independent format or AI path.
- **D-09:** **Live in-client preview = hot-retrigger loaded instances.** When injected, after save/reload re-trigger the effect instances already live in the scene (hook into the running client-effect/particle manager). *(Heavier preview option — the exact runtime hook is the open implementation question.)*

**Preview-vs-author boundary (DEC-A3 — MANDATORY one sentence per editor)**
- **D-10 — WorldSnapshot:** "Utinni places / transforms / retemplates **existing** object templates; creating the templates or their meshes is the DCC (Blender) lane."
- **D-11 — Particle:** "Utinni edits emitter / timing / color parameters and swaps texture / mesh **references**; authoring the referenced meshes / textures stays in Blender."

**RESID-04 — window-resize / windowed↔fullscreen**
- **D-12:** **Enumerate live, then fix root cause.** Reproduce against a live injected session, fill the edge-case matrix, confirm whether the cluster is one root cause (prime suspect: SWG's exclusive-fullscreen mode switch detaching the embed), then apply the **targeted intercept/suppress-the-mode-switch** fix to keep SWG windowed-embedded.
- **D-13:** **Hard constraint — no `IDirect3DDevice9::Reset` on SWG's device** (untracked default-pool resources → `D3DERR_INVALIDCALL` → DEVICELOST → crash). Resize the window and let windowed COPY `Present` self-stretch the backbuffer↔window mismatch. Keep RT-space mouse mapping correct across any resize.

**RESID-03 — SC3 live-reload candor**
- **D-14:** **Live-observe + honest badges.** Observe SC3 reload for `.stf` (Step-7 scene-change + stale-`sourceCrc` checks) and object-template reload; set honest reload-candor badge copy; apply the same candor pattern to the new WorldSnapshot / Particle reload paths. Do NOT loosen badge copy to over-promise; relog-only reloads must say so.

### Claude's Discretion
- WorldSnapshot bulk-edit undo/command integration (compose existing per-node edit commands; exact command/undo wiring is the planner's call).
- Exact `.prt` field taxonomy and `UtinniCoreDotNet/Formats/Particle/` layout (follow existing `Formats/*` codec structure).
- Table control choice / column layout for the placements list (follow existing TJT SubPanel UI conventions, e.g. the Datatable editor's grid).
- CLI verb naming for the `.prt` read/summarize tools (follow Phase-13 verb conventions; the 16-verb CommandLineParser cap is already worked around — see Architecture Patterns).

### Deferred Ideas (OUT OF SCOPE)
- **Terrain editor `PROD-W2-TRN` (`.trn`)** — deferred to v2.1 (heavier codec).
- **Particle prompt-to-mutate AI** (AI writes `.prt` params directly) — deferred; V1 is read-assist only.
- **Deliberate detached-fullscreen mode with clean re-attach** — the RESID-04 alternative to intercept/suppress; only revisit if the targeted suppress approach proves wrong.
- **`.prt` fixture corpus / golden round-trip tests** beyond what's needed to validate the typed decode — Tier-2 follow-up (no fixtures exist today).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PROD-W2-WS | Modder can view + edit object placements in a world snapshot via a Utinni SubPanel (extends existing Snapshot panel; reuses shipped codecs — zero new format work) | Existing `WorldSnapshotReaderWriter` native API (`nodeList`, `getNodeCount`, `getNodeAt`, `getObjectTemplateName`) supplies all table columns; `WorldSnapshotImpl` + `WorldSnapshotCommands` supply all edit ops. See Standard Stack + WorldSnapshot Architecture. |
| PROD-W2-PRT | Modder can open + edit a particle / client-effect asset in a Utinni SubPanel, with live in-client preview when injected, backed by a new `.prt` codec in `UtinniCoreDotNet` | `.prt` = `FORM PEFT` format fully traced in `swg-client-v2/clientParticle`; codec structure mirrors `Formats/ObjectTemplate`. See `.prt` Format section + Code Examples. Live-preview hook is an Open Question (D-09). |
| RESID-03 | SC3 live-reload semantics confirmed/honest for string-table + object-template reload candor | Existing CF-05 tier-(b) badge pattern in `ReloadAssetClassifier`; the two folded todos define the exact live-observation checklist. See RESID-03 section. |
| RESID-04 | SWG window-resize / windowed↔fullscreen edge cases enumerated and fixed | Prime suspect = exclusive-fullscreen mode switch via DirectInput `SetCooperativeLevel` shim (already in `direct_input.cpp`); fix in `PanelGame.cs` reparent + `hkPresent`. No-Reset constraint enforced. See RESID-04 section. |
</phase_requirements>

## Summary

Phase 15 is two editors plus two presentation residuals, and they decompose into **one cheap, high-confidence task (WorldSnapshot)** and **one expensive, medium-confidence task (Particle `.prt` codec)**, bracketed by two live-session-dependent residuals.

WorldSnapshot is nearly all reuse. The native `WorldSnapshotReaderWriter` (CppSharp-exposed under `UtinniCore.Utinni`, NOT a managed `Formats/*` codec) already exposes a full node list (`nodeList`, `getNodeCount()`, `getNodeAt(i)`, `getObjectTemplateName(idx)`, `getNodeById`) and the `WorldSnapshotImpl` already wraps every per-node edit operation as a `GroundSceneCallbacks`-marshalled command with undo. The Wave-2 delta (D-01) is a new flat table view + multi-select bulk ops that compose those existing commands — zero new format code, exactly as D-02 states. The structural risk is purely UI/threading (game-thread marshalling, selection-sync feedback loops).

Particle is the real format investment. `.prt` files are `FORM PEFT` (Particle Effect Template) — a deeply-nested IFF: `PEFT` → versioned `FORM 0000/0001/0002` → `ParticleTiming` (`PTIM`) + an emitter-group-count chunk + N `ParticleEmitterGroupDescription` (`EMGP`) → N `ParticleEmitterDescription` (`EMTR`, **15 format versions 0000-0014**) → a `ParticleDescriptionQuad` (`PTQD`) or `ParticleDescriptionMesh` (`PTMH`) leaf. The emitter description alone has ~468 IFF read/write calls, dominated by `WaveForm` (`sharedMath`, 3 versions) and `ColorRamp` members. This is large enough that D-04 (full typed decode) + D-05 (degrade-don't-abort raw-preserve) are the right calls: type the common path, raw-preserve every unrecognized version/field for byte-safe round-trip. The new codec mirrors the shipped `Formats/ObjectTemplate` structure (ParamValue/Codec/Mutable/Writer files) and consumes the existing tree-based `IffReader`/`IffWriter` + `IffPayloadCursor` (little-endian payload scalars).

RESID-04's mechanism is well-understood from prior art: the embed detaches because SWG flips to **true D3D9 exclusive fullscreen**, most likely triggered through the DirectInput `SetCooperativeLevel(DISCL_EXCLUSIVE|DISCL_FOREGROUND)` path that the existing `direct_input.cpp` vtable shim already intercepts and logs. The fix is to suppress/redirect that mode switch to keep SWG windowed-embedded — NOT to call `Reset`. RESID-03 is a pure live-observation + honest-badge-copy task using the existing `ReloadAssetClassifier` tier-(b) pattern.

**Primary recommendation:** Sequence WorldSnapshot first (cheap, de-risks the SubPanel/bulk-op UI machinery), then the `.prt` codec (port `PEFT`/`EMGP`/`EMTR`/`PTQD`/`PTMH`/`WaveForm`/`ColorRamp` load+write into a new `Formats/Particle/` codec with strict tree-walk + raw-byte fallback), then the Particle editor + CLI/MCP read tools, then slot RESID-03 + RESID-04 into the same live session that demos the editors. Treat the D-09 live-preview hook and the `.prt` byte-exact round-trip as the two highest-risk items.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| WorldSnapshot placements table (read all nodes) | TJT SubPanel (WinForms) | Native `WorldSnapshotReaderWriter` (CppSharp) | Node data lives in the running client's snapshot reader; the table is a managed view that reads it on the game thread |
| WorldSnapshot per-node + bulk edit ops | TJT `WorldSnapshotImpl` | `UtinniCoreDotNet/Commands/WorldSnapshotCommands` (undo) | Edit ops must marshal to the SWG ground-scene update loop (`GroundSceneCallbacks`); undo composes existing commands |
| `.prt` typed decode / encode | `UtinniCoreDotNet/Formats/Particle` (managed, headless) | `Formats/Iff` reader/writer + `IffPayloadCursor` | Format logic stays headless + CI-testable; reuses shipped IFF primitives. NEVER in TJT or MCP server (D-06, Phase-14 rule) |
| `.prt` read/summarize CLI verbs | `Utinni.Cli/Commands` | `Formats/Particle` | CLI wraps the codec; same pattern as `decode-iff`/`roundtrip-ot` |
| `.prt` MCP read/summarize tools | `Utinni.Mcp` (net10, out-of-proc) | `Utinni.Cli` verbs (dispatch by exit code) | Phase-14 rule: MCP dispatches to CLI, ZERO format logic in server |
| Particle SubPanel editor + in-app AI button | TJT SubPanel (WinForms) | `Utinni.Cli`/MCP read path (D-08 reuse) | In-app assist reuses the headless read path; no independent AI/format path |
| Live in-client particle preview (hot-retrigger) | Native UtinniCore (effect/particle manager hook) | TJT `*Impl` (game-thread marshalling) | Re-triggering live scene instances is a native runtime hook reachable only under injection |
| Window reparent / resize / fullscreen suppress (RESID-04) | Native UtinniCore D3D9 + DirectInput hooks | `UtinniCoreDotNet/UI/Controls/PanelGame.cs` (managed reparent) | Mode switch originates in SWG's D3D9/DirectInput; window placement is managed-side `SetWindowPos` |
| Reload-candor badges (RESID-03) | `UtinniCoreDotNet` `ReloadAssetClassifier` | TJT SubPanel badge UI | Classification is headless + CI-testable; badge copy is UI |

## Standard Stack

This phase adds **no new external packages.** Every dependency is already in the solution; the work is in-house porting + UI. The "stack" below is the set of existing internal components the two editors build on.

### Core (existing internal components — reuse, do not re-create)
| Component | Location | Purpose | Why Standard |
|-----------|----------|---------|--------------|
| `WorldSnapshotReaderWriter` / `WorldSnapshot` | `UtinniCore/swg/scene/world_snapshot.h` (CppSharp → `UtinniCore.Utinni`) | Native node list + per-node CRUD + save | Shipped; the entire WorldSnapshot editing core (D-02) |
| `WorldSnapshotImpl` | `UtinniPlugins/.../SWG/WorldSnapshotImpl.cs` | Game-thread-marshalled wrapper over the native reader; gizmo, add/remove, rotate, save | Shipped; the table layers on this |
| `WorldSnapshotCommands` | `UtinniCoreDotNet/Commands/WorldSnapshotCommands.cs` | `IUndoCommand` set (Add/Remove/PositionChanged/RotationChanged) | Bulk ops compose these (D-01) |
| `Formats/Iff` (`IffReader`, `IffWriter`, `MutableIffDocument`, `IffPayloadCursor`) | `UtinniCoreDotNet/Formats/Iff/` + `Formats/Decoders/IffPayloadCursor.cs` | Tree-based IFF read/write + little-endian payload cursor | The `.prt` codec composes these (proven by Datatable/OT/STF codecs) |
| `Formats/ObjectTemplate/*` | `UtinniCoreDotNet/Formats/ObjectTemplate/` | The closest structural analog for the new `.prt` codec (ParamValue/Codec/Mutable/Writer + degrade-to-hex) | Same degrade-don't-abort philosophy D-05 requires |
| `ReloadAssetClassifier` | `UtinniCoreDotNet` (Phase 8 P05) | 4-tier reload routing + tier-(b) candor badge | RESID-03 reuses this classification |
| `IEditorPlugin` / `Plugin.cs` MEF seam | `UtinniPlugins/.../Plugin.cs` | SubPanel + form registration (try/catch isolated, `GetSubPanels()` stays null) | The unchanged Wave-1 mechanism (D-03) |
| `direct_input.cpp` `SetCooperativeLevel` vtable shim | `UtinniCore/swg/misc/direct_input.cpp` | Already intercepts + logs `DISCL_*` flags per call | The RESID-04 intercept point already exists |
| `directx9.cpp` `hkPresent` / `hkReset` | `UtinniCore/swg/graphics/directx9.cpp` | Present hook (windowed COPY stretch) + Reset pass-through | RESID-04 present-path ground truth |

### Supporting (reference-only, read from `swg-client-v2` — NO runtime dependency)
| Component | Location (read-only) | Purpose | When to Use |
|-----------|---------------------|---------|-------------|
| `ParticleEffectDescription` | `swg-client-v2/.../clientParticle/src/shared/ParticleEffectDescription.cpp` | `PEFT` root load/write — the codec entry point | Port the top-level structure |
| `ParticleEmitterGroupDescription` | `.../ParticleEmitterGroupDescription.cpp` | `EMGP` group node (timing + emitter list) | Port the group layer |
| `ParticleEmitterDescription` | `.../ParticleEmitterDescription.cpp` (468 IFF calls, 15 versions) | `EMTR` — the heavy emitter leaf | Port the bulk of typed fields |
| `ParticleDescriptionQuad` / `ParticleDescriptionMesh` | `.../ParticleDescriptionQuad.cpp`, `.../ParticleDescriptionMesh.cpp` | `PTQD` / `PTMH` particle render leaves (texture/mesh refs — D-11 swap targets) | Port the render-description leaves |
| `ParticleTiming` / `ParticleTexture` | `.../ParticleTiming.cpp` (`PTIM`), `.../ParticleTexture.cpp` (`PTEX`) | Timing + texture-reference sub-chunks | Port sub-chunks |
| `WaveForm` | `swg-client-v2/.../sharedMath/src/shared/WaveForm.cpp` (3 versions) | The recurring control-point curve type used by nearly every emitter field | Port once, reuse everywhere in the codec |
| `ColorRamp` | `swg-client-v2/.../clientParticle/src/shared/ColorRamp.h/.cpp` | Recurring color-over-life ramp | Port once, reuse |
| `ParticleEditor` / `ClientEffectEditor` (Qt) | `swg-client-v2/.../application/ParticleEditor/`, `.../ClientEffectEditor/` | Reference for which fields are author-relevant (D-04 taxonomy) | Field-selection guidance for the typed UI |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Tree-walk `IffReader` + `IffPayloadCursor` (Utinni's model) | A streaming `enterForm`/`enterChunk` cursor mirroring SOE's `Iff` class | SOE's C++ is streaming; Utinni's shipped codecs are tree+cursor. Mirroring SOE 1:1 would diverge from every existing codec and re-implement bounds-checking. **Use the existing tree model** — proven across 5 Wave-1 codecs. |
| New `Formats/Particle/` folder | Extending `Formats/ObjectTemplate` | `.prt` is `PEFT`, not an object template; it deserves its own folder, mirroring the OT layout (discretion area confirms `Formats/Particle/`). |
| Hot-retrigger live preview (D-09) | Play-at-camera-on-demand | D-09 locked hot-retrigger; the runtime hook is heavier but is the chosen path (Open Question). |
| Suppress the fullscreen mode switch (D-12) | Support a deliberate detached-fullscreen with clean re-attach | Locked: suppress first; detached mode is a deferred fallback. |

**Installation:** None. No `npm`/`pip`/`cargo`/NuGet package is added by this phase.

**Version verification:** N/A — no external packages. Internal components verified by direct file inspection (paths above) on 2026-06-07.

## Package Legitimacy Audit

> **Not applicable.** This phase installs **zero** external packages. All work is in-house C#/C++ porting and WinForms UI against components already present in `D:/Code/Utinni` and `D:/Code/UtinniPlugins`, using `swg-client-v2` as a read-only format reference (no runtime dependency, per `project_swg_client_v2_reference`).

**Packages removed due to slopcheck [SLOP] verdict:** none (no packages)
**Packages flagged as suspicious [SUS]:** none (no packages)

## Architecture Patterns

### System Architecture Diagram

```
WORLDSNAPSHOT EDITOR (PROD-W2-WS) — all reuse, no new format code
────────────────────────────────────────────────────────────────
  [TJT SnapshotPanel]  ──grows──►  [+ Placements Table + multi-select bulk ops]
        │                                   │
        │ click-to-select / bulk-select     │ bulk move / delete / retemplate
        ▼                                   ▼
  [WorldSnapshotImpl] ──GroundSceneCallbacks.AddUpdateLoopCall──► (SWG game thread)
        │                                   │ compose per-op
        ▼                                   ▼
  [native WorldSnapshotReaderWriter]   [WorldSnapshotCommands (IUndoCommand)]
   nodeList / getNodeAt / getObjectTemplateName / getNodeById       │
        │                                                            ▼
        └──── reads node rows for the table        [editorPlugin.AddUndoCommand]

PARTICLE EDITOR (PROD-W2-PRT) — new .prt codec + headless read path + in-app reuse
──────────────────────────────────────────────────────────────────────────────────
  .prt bytes (FORM PEFT)
        │
        ▼
  [Formats/Iff IffReader]  ──tree──►  IffDocument (FORM/CHUNK nodes, raw leaf bytes)
        │
        ▼
  [Formats/Particle codec]  ── typed decode (PEFT→EMGP→EMTR→PTQD/PTMH, WaveForm, ColorRamp)
        │                      └─ degrade: unrecognized version/field → raw-byte preserve (D-05)
        ▼
  MutableParticleEffect (typed fields + raw-preserved unknowns)
        │                                  ▲
   ┌────┴───────────────┐                  │ edit emitter/timing/color params; swap tex/mesh refs (D-11)
   ▼                    ▼                  │
[Utinni.Cli verbs]  [Particle SubPanel] ──┘
   decode/summarize     │  in-app AI assist button ──reuses──► [Utinni.Cli/MCP read path] (D-08)
        │               │  save ──► IffWriter ──► .prt bytes (byte-exact round-trip target)
        ▼               ▼
[Utinni.Mcp tools]   (when injected) hot-retrigger live scene instances (D-09, OPEN HOOK)
 (read/summarize,        └──► native effect/particle manager
  dispatch-by-exit-code)

RESID-04 (window/fullscreen)            RESID-03 (reload candor)
─────────────────────────────          ────────────────────────
 SWG flips to D3D9 exclusive FS         edit .stf/.ot → save (loose override)
   ▲ prime suspect                              │
 [DirectInput SetCooperativeLevel              ▼
  shim, direct_input.cpp]  ──intercept/   [ReloadAssetClassifier tier-(b)]
   suppress mode switch──►  keep windowed         │  honest badge copy
        │                                         ▼
 [PanelGame.cs reparent + SetWindowPos]    "Reloads on next scene change" / "relog only"
   resize window, NO Reset (D-13)          (live-observe Step-7 to confirm which)
 [hkPresent windowed COPY self-stretch]
```

### Recommended Project Structure
```
UtinniCoreDotNet/Formats/Particle/        # NEW — mirrors Formats/ObjectTemplate/
├── ParticleEffectDocument.cs             # PEFT root: FromIff(IffDocument) typed reader
├── ParticleFieldValue.cs                 # typed union (float/int/bool/enum/WaveForm/ColorRamp/RawBytesHexFallback)
├── WaveFormCodec.cs                      # WaveForm decode/encode (3 versions) — reused everywhere
├── ColorRampCodec.cs                     # ColorRamp decode/encode
├── ParticleEmitterDescription.cs         # EMTR (15 versions) typed decode + raw-preserve unknowns
├── MutableParticleEffect.cs              # mutable model over MutableIffDocument (edit + raw-preserve)
├── ParticleEffectWriter.cs               # composes IffWriter.Write
└── ParticleParseException.cs

Utinni.Cli/Commands/                       # NEW verbs (slot into existing object-typed Dispatch)
├── DecodeParticleCommand.cs              # read/summarize .prt as JSON
├── RoundtripParticleCommand.cs           # byte-exact round-trip gate (no fixtures yet — synth)
└── (optional ApplySavePrtCommand.cs)     # if write surface is wired this phase

Utinni.Mcp/                                # NEW read tool wrapping the decode/summarize verb (dispatch-by-exit-code)

UtinniPlugins/The Jawa Toolbox/.../UI/SubPanels/
├── SnapshotPanel.cs                      # GROW: add placements table + multi-select bulk ops
└── (new) ParticlePanel.cs or FormParticleEditor.cs  # new Particle SubPanel (follow Datatable grid conventions)
```

### Pattern 1: Game-thread marshalling for all live-client edits
**What:** Every operation that touches the running client's snapshot or scene must be queued onto the SWG update loop, never run on the WinForms UI thread.
**When to use:** All WorldSnapshot table/bulk ops; the Particle live-preview hot-retrigger.
**Example:**
```csharp
// Source: UtinniPlugins/.../SWG/WorldSnapshotImpl.cs (shipped pattern)
public void SetSelectedNodePosition(float x, float y, float z)
{
    GroundSceneCallbacks.AddUpdateLoopCall(() =>   // marshal to SWG game thread
    {
        var obj = Game.PlayerLookAtTargetObject;
        if (obj != null) { /* mutate node + push undo command */ }
    });
}
```
Bulk ops (D-01) iterate the multi-selection and enqueue one composed command per node inside a single `AddUpdateLoopCall` so the whole bulk operation lands atomically on one game-frame.

### Pattern 2: Event-handler add/remove around ValueChanged to avoid feedback loops
**What:** When the table selection drives the gizmo controls (and vice-versa), detach the change handler before programmatically setting a control value, then reattach.
**When to use:** Selection-sync between the new placements table and the existing gizmo/position controls.
**Example:**
```csharp
// Source: UtinniPlugins/.../UI/SubPanels/SnapshotPanel.cs:246 (shipped pattern)
public void UpdateSelectedNodeControlsPosition(Vector position)
{
    nudNodePosX.ValueChanged -= nudNodePos_ValueChanged;   // detach
    nudNodePosX.Value = (decimal)position.X;               // set programmatically
    nudNodePosX.ValueChanged += nudNodePos_ValueChanged;   // reattach
}
```

### Pattern 3: Degrade-don't-abort typed codec (the OT-multichunk precedent)
**What:** Decode known versions/fields into typed values; when a version tag or field is unrecognized, capture the exact original bytes and preserve them verbatim so save round-trips byte-identical. The editor greys out what it cannot type.
**When to use:** The entire `.prt` codec (D-05). `EMTR` has 15 versions — type the common ones, raw-preserve the rest.
**Example:**
```csharp
// Source: UtinniCoreDotNet/Formats/ObjectTemplate/ObjectTemplateParamCodec.cs (shipped precedent)
// WEIGHTED_LIST / RANGE / DIE_ROLL / short / long → "consume-exactly-or-hex" defensive posture:
// if the typed decode cannot consume the leaf cleanly, store RawBytesHexFallback and the
// writer re-emits the captured bytes verbatim. Byte-exactness holds either way.
```
For `.prt`, the unit of raw-preservation is the unrecognized `FORM <version>` sub-form (preserve the whole sub-tree's bytes) or an over-length leaf chunk.

### Pattern 4: MEF SubPanel registration — unchanged Wave-1 seam (D-03)
**What:** Register new forms/panels in `Plugin.cs` inside try/catch isolation; `GetSubPanels()` stays `null` (SPI NOT widened, CON-M-01/02). Singleton forms use hide-not-dispose.
**Example:**
```csharp
// Source: UtinniPlugins/.../Plugin.cs (shipped pattern for all 5 Wave-1 editors)
try { forms.Add(new FormParticleEditor(this)); }
catch (Exception ex) { Log.Info("Failed to create FormParticleEditor; ... " + ex); }
// The new SnapshotPanel table is an in-place grow of the existing panel already in the
// "Controls" SubPanelContainer — no new registration needed for the table itself.
```
**Singleton hide-not-dispose** (Phase 8 canonical): on `CloseReason.UserClosing`, cancel close + `Hide()` instead of disposing; editor-host shutdown reasons fall through normally. Use the framework `SingletonFormClosePolicy` helper (shipped Phase 9).

### Pattern 5: CLI verb dispatch past the 16-verb cap (already solved)
**What:** `CommandLineParser`'s `ParseArguments<T..>`/`MapResult` top out at 16 type args. The CLI already uses the `Type[] ParseArguments` overload + a single object-typed `MapResult` that switches on the concrete parsed option type.
**When to use:** Adding the new `.prt` verbs — just add `case Commands.DecodeParticleOptions o: return ...;` to `Dispatch` and the option type to the `Type[]`.
**Example:**
```csharp
// Source: Utinni.Cli/Program.cs:76-100 (shipped — already 25 command files)
private static int Dispatch(object opts) {
    switch (opts) {
        case Commands.DecodeIffOptions o: return Commands.DecodeIffCommand.Run(o);
        // ... add: case Commands.DecodeParticleOptions o: return Commands.DecodeParticleCommand.Run(o);
    }
}
```
The 16-verb cap noted in `project_phase13_cli_verbs` is **historical context, not a live blocker** — verified resolved in `Program.cs`.

### Anti-Patterns to Avoid
- **Calling `IDirect3DDevice9::Reset` on SWG's device (RESID-04):** untracked default-pool resources → `D3DERR_INVALIDCALL` → DEVICELOST → crash. Live-verified. Resize the window; let windowed COPY `Present` self-stretch (D-13).
- **`WS_CHILD` reparenting:** breaks DirectInput — `SetCooperativeLevel` requires a top-level HWND. Keep the owned-popup model (`WS_POPUP` + `GWLP_HWNDPARENT`).
- **Running snapshot/scene edits on the UI thread:** corrupts the allocator / races the render thread. Always `GroundSceneCallbacks.AddUpdateLoopCall`.
- **Per-frame heap allocation on hot callback paths:** see `project_rh_snapshot_no_heap_alloc` — `std::vector::reserve()` in dispatch fragmented SWG's allocator and crashed scene change. Relevant if the live-preview hook touches a per-frame callback.
- **Aborting the `.prt` codec on an unrecognized version:** the SOE C++ `FATAL`s on unknown versions; Utinni must NOT — degrade to raw-preserve (D-05).
- **Loosening reload-candor badge copy to over-promise (RESID-03):** if a reload is relog-only, the badge must say so.
- **Putting format logic in TJT or the MCP server:** D-06 + Phase-14 rule — codec lives in `UtinniCoreDotNet`; CLI wraps; MCP dispatches by exit code.
- **Committing `Generated/UtinniCore.cs`:** if the `.prt` work touches any CppSharp-exposed native type, the regen reorders the file — `git checkout --` it, never commit (`project_utinnicore_cs_regen_churn`).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| IFF tree parsing / writing | A bespoke `.prt` byte reader | `Formats/Iff` `IffReader`/`IffWriter`/`MutableIffDocument` | Pad-byte handling, FORM/CHUNK nesting, big-endian tags already solved + CI-tested across 5 codecs |
| Little-endian payload scalar reads with bounds checks | Manual `BitConverter` + index math | `IffPayloadCursor` | Truncation-safe, DoS-bounded, the established Pitfall-6 little-endian payload reader |
| Node list / object-template name lookup | Re-parsing the `.ws` file | native `WorldSnapshotReaderWriter` (`getNodeAt`, `getObjectTemplateName`, `getNodeById`) | The running client already holds the parsed snapshot — D-02 zero-new-format |
| Per-node undo for bulk ops | A new undo stack | Compose `WorldSnapshotCommands` `IUndoCommand`s | Shipped; bulk = N existing commands in one update-loop call |
| Reload classification + candor badges | New routing logic | `ReloadAssetClassifier` tier-(b) | Shipped Phase 8; RESID-03 reuses it |
| Singleton form close behavior | Per-form close handling | `SingletonFormClosePolicy` | Shipped Phase 9; canonical hide-not-dispose |
| Window reparent / Z-order / reposition | New SetWindowPos logic | `PanelGame.cs` reparent + reposition | Shipped owned-popup model with the minimize/sentinel guards already in place |
| DirectInput cooperative-level interception | A new DirectInput hook | The existing `direct_input.cpp` `SetCooperativeLevel` vtable shim | Already patched + logging DISCL flags — extend it to suppress, don't re-hook |
| CLI verb dispatch scaling | A new arg parser | The existing `Type[] ParseArguments` + object `MapResult` | Already scales past 16 verbs |

**Key insight:** This phase is overwhelmingly a **reuse + port** exercise. The only genuinely new code is the `.prt` typed-decode logic (which still composes existing IFF primitives) and the placements-table/Particle-panel UI. Every cross-cutting concern (threading, undo, reload, window management, CLI dispatch, MCP dispatch) already has a shipped, CI-tested solution.

## Runtime State Inventory

> Partial-applicability: this phase is mostly greenfield editor code, but the Particle **live-preview** (D-09) and RESID-04 touch runtime state. The two editors do not rename anything.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | **None for the editors.** `.prt` and `.ws` are file assets read/written via loose-override (Phase 8 path-defense), not a datastore. WorldSnapshot writes via the native `saveFile`. | none |
| Live service config | **None.** No external service config carries phase strings. | none |
| OS-registered state | **RESID-04 only:** SWG's top-level HWND is OS-registered and reparented as an owned popup (`GWLP_HWNDPARENT`). The fullscreen mode switch re-registers exclusive-fullscreen display state at the OS/driver level — this is exactly what must be suppressed. | intercept/suppress the mode switch (D-12); no new registration |
| Secrets/env vars | **None.** | none |
| Build artifacts / live runtime instances | **D-09 live-preview:** the running client holds **live particle/effect instances** in the scene that must be hot-retriggered after save/reload. These are runtime objects in the native effect/particle manager, not files — the codec edit alone won't update them; the runtime hook must re-trigger them. **If the `.prt` work touches a CppSharp-exposed native type, `Generated/UtinniCore.cs` will reorder** (stale artifact — `git checkout --` it). | implement the runtime re-trigger hook (Open Question); never commit `UtinniCore.cs` regen |

## Common Pitfalls

### Pitfall 1: Two different "particle" formats — `.prt` (PEFT) vs `.cef` (CLEF)
**What goes wrong:** SWG has TWO related formats. `.prt` = `FORM PEFT` (the actual particle systems, `clientParticle` library). `.cef` = `FORM CLEF` (`clientGame/clientEffect` — a thin *bundle* that references a `.prt`/appearance + sound + light + camera-shake + force-feedback). Conflating them wastes effort or scopes the wrong codec.
**Why it happens:** The phase title says "Particle / client-effect" and the maintainer-facing language uses both. CONTEXT D-04 + the canonical ref both point at `clientParticle`/`clientParticle` → **`.prt`/PEFT is the primary codec target.**
**How to avoid:** Build the `.prt`/PEFT codec. `.cef`/CLEF is a much smaller, flatter format (versions 0001/0002, string + scalar chunks) and is OPTIONAL — only add it if the editor needs to open the bundle wrapper. Flag the choice to the planner; recommend `.prt` first, `.cef` as a possible stretch.
**Warning signs:** Reading `ClientEffectTemplate.cpp` (CLEF) when you meant `ParticleEffectDescription.cpp` (PEFT).

### Pitfall 2: `EMTR` has 15 format versions; the SOE loader `FATAL`s on unknown
**What goes wrong:** A naive 1:1 port replicates SOE's `FATAL(true, "Unsupported data version")` and aborts on any `.prt` the reference doesn't cover — violating D-05.
**Why it happens:** The C++ reference is a game client that controls its own asset versions; Utinni opens arbitrary community `.prt` files.
**How to avoid:** Type the versions present in your test corpus; for any unrecognized `EMTR`/`PEFT`/leaf version, raw-preserve the sub-form bytes (Pattern 3). Never `FATAL`.
**Warning signs:** A `switch` with a `default` that throws instead of capturing raw bytes.

### Pitfall 3: WaveForm and ColorRamp recur everywhere — port them once, first
**What goes wrong:** Nearly every emitter field (`m_emitterTranslationX`, `m_particleLifeTime`, `m_particleWeight`, …) is a `WaveForm` (control-point curve), and color is a `ColorRamp`. Inlining their decode per-field explodes the codec and guarantees drift.
**Why it happens:** The 468 IFF calls in `EMTR` are mostly repeated `WaveForm::load` invocations.
**How to avoid:** Implement `WaveFormCodec` (3 versions) and `ColorRampCodec` as standalone, reused units first; then `EMTR` decode is a sequence of `WaveFormCodec.Read(cursor)` calls. Note `m_particleWeight.scaleAll(0.28f)` in `load_0000` — a version-specific transform that must NOT be applied on write-back (it's a load-time normalization; round-trip must preserve original bytes, which is another reason to favor raw-preserve where the transform is lossy).
**Warning signs:** Byte-exact round-trip failing on weight/scale fields.

### Pitfall 4: Utinni's IFF model is tree+cursor, not SOE's streaming `enterForm`/`enterChunk`
**What goes wrong:** Trying to mirror the SOE `Iff` streaming API (`iff.enterForm`, `iff.read_float`, `iff.exitForm`) directly fights Utinni's shipped model, where `IffReader.Read` returns a full `IffDocument` tree and leaf payloads are read with `IffPayloadCursor` (little-endian).
**Why it happens:** The reference source reads as you parse the file.
**How to avoid:** Walk the `IffDocument` tree (find the `PEFT` form, its version sub-form, the `EMGP`/`EMTR` children), and for each leaf chunk run an `IffPayloadCursor` over the raw payload. This is exactly how `Formats/ObjectTemplate` and `Formats/Datatable` work — follow them.
**Warning signs:** Re-implementing chunk-length/pad-byte handling that `IffReader` already does.

### Pitfall 5: RESID-04 — the fullscreen switch is reachable from normal login, not just chat-open
**What goes wrong:** Scoping RESID-04 to only the chat-open Enter path misses the login→load-into-world trigger (live-confirmed 2026-06-03).
**Why it happens:** The original `chat-open-d3d9-fullscreen.md` session only saw it via Enter; the 12-04 re-run widened it.
**How to avoid:** Fill the full edge-case matrix (windowed→fullscreen, fullscreen→windowed, maximize/restore, minimize/restore, free resize, multi-cycle, alt-tab, DPI change). Confirm the single-root-cause hypothesis (exclusive-fullscreen mode switch) before fixing. The `direct_input.cpp` `SetCooperativeLevel` shim already logs DISCL flags + caller PC per call — use that log to confirm the exclusive-fullscreen trigger and the calling context.
**Warning signs:** Fix works for chat-open but login still detaches.

### Pitfall 6: IFF payload scalars are little-endian even though tags/lengths are big-endian
**What goes wrong:** Reading emitter floats/ints big-endian yields garbage.
**Why it happens:** EA-IFF-85 tags + chunk lengths are big-endian, but the original Win32 client wrote payload scalars little-endian.
**How to avoid:** Use `IffPayloadCursor` (already little-endian by contract — see its header comment) for all payload scalars.
**Warning signs:** Float fields decode to absurd magnitudes.

### Pitfall 7: Live-preview hot-retrigger may run on a per-frame callback — keep it heap-free
**What goes wrong:** If the D-09 runtime hook re-triggers effects via a per-frame `GroundSceneCallbacks` path that allocates, it can fragment SWG's allocator and crash scene change (the `0x0051fb0a` precedent).
**How to avoid:** Marshal once per save/reload, not per frame; if a per-frame path is unavoidable, use stack-allocated fixed-size snapshots (`project_rh_snapshot_no_heap_alloc`).
**Warning signs:** Crash near scene transitions after enabling live preview.

## Code Examples

### `.prt` root structure (PEFT) — the codec entry point
```cpp
// Source: swg-client-v2/.../clientParticle/src/shared/ParticleEffectDescription.cpp:247
// Root tag: FORM PEFT  →  versioned FORM 0000/0001/0002
iff.enterForm(PEFT);            // ParticleEffectAppearanceTemplate::getTag() == TAG(P,E,F,T)
  switch (iff.getCurrentName()) {
    case TAG_0000: load_0000;   // emitterGroupCount chunk + N EMGP
    case TAG_0001: load_0001;   // ParticleTiming (PTIM) + count + N EMGP
    case TAG_0002: load_0002;   // PTIM + count + 4 floats
                                //   (initialPlayBackRate, initialPlayBackRateTime,
                                //    playBackRate, scale) + N EMGP   ← current write version
  }
iff.exitForm(PEFT);
// write() always emits FORM 0002 (newest). Round-trip a 0000/0001 file => either
// re-emit its original version OR accept up-conversion — DECIDE + document (Open Q).
```

### Nested taxonomy (the typed-decode tree to port)
```
FORM PEFT
└── FORM 0002                                  ParticleEffectDescription
    ├── (PTIM)  ParticleTiming                 timing
    ├── CHUNK 0000  int32 count + 4 floats
    └── N × FORM EMGP                          ParticleEmitterGroupDescription
        ├── (PTIM) timing
        └── N × FORM EMTR                      ParticleEmitterDescription (15 versions 0000-0014)
            ├── many WaveForm fields           (sharedMath WaveForm, 3 versions) ← port once
            ├── CHUNK 0000  emitter scalars/enums/bools
            └── FORM PTQD | FORM PTMH          ParticleDescriptionQuad | ...Mesh (4 versions)
                ├── ColorRamp                  (color over life) ← port once
                └── (PTEX) ParticleTexture     texture reference  ← D-11 swap target
```

### Game-thread bulk op (compose existing per-node commands)
```csharp
// Pattern for D-01 bulk move/delete/retemplate — one atomic update-loop call:
GroundSceneCallbacks.AddUpdateLoopCall(() =>
{
    foreach (var nodeId in selectedNodeIds)
    {
        var node = WorldSnapshotReaderWriter.Get().GetNodeById(nodeId);
        if (node == null) continue;
        // compose the existing IUndoCommand (e.g. RemoveWorldSnapshotNodeCommand)
        editorPlugin.AddUndoCommand(this, new AddUndoCommandEventArgs(
            new RemoveWorldSnapshotNodeCommand(node)));
        WorldSnapshot.RemoveNode(node);
    }
});
// Undo/redo of a bulk op: planner's discretion — either one composite command wrapping
// the N child commands, or N commands pushed in order (CONTEXT discretion area).
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Object-template list-params aborted at 17% | Raw-fallback degrade (degrade-don't-abort) | d68387f (Phase 11) | The blueprint for `.prt` D-05 |
| CLI capped at 16 verbs (`MapResult`) | `Type[] ParseArguments` + object `MapResult` dispatch | Phase 13/14 | `.prt` verbs slot in freely (cap is historical) |
| SWG was a standalone top-level window | Owned-popup reparent into PanelGame (`WS_POPUP` + `GWLP_HWNDPARENT`) | Issue #10 Phase B | RESID-04 builds on this; fullscreen switch is the remaining gap |
| `IDirect3DDevice9::Reset` to resize embed | Resize window + windowed COPY Present self-stretch | Phase B-bis (live-verified) | The hard no-Reset constraint (D-13) |

**Deprecated/outdated:**
- The `chat-open-d3d9-fullscreen.md` scoping ("only via Enter"): superseded by the 2026-06-03 finding that login→world also triggers it. Treat the todo's matrix as the spec, but widen the trigger surface.
- The 16-verb CommandLineParser cap as a *blocker*: already worked around; do not re-solve.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `.prt` (FORM PEFT) is the primary codec target; `.cef` (FORM CLEF) is optional/stretch | Pitfall 1, Summary | If the maintainer actually wants the CLEF bundle editor first, the codec scope shifts. LOW risk — CONTEXT D-04 + canonical ref both point at `clientParticle`. Flag at planning. |
| A2 | `write()` re-emitting only FORM 0002 means round-tripping a 0000/0001 file would up-convert its version unless the codec preserves the original version form | Code Examples | If up-conversion is acceptable, simpler; if byte-exact is required for old-version files, the codec must preserve the original version sub-form. Needs a decision (Open Q1). [ASSUMED] — based on reading the SOE `write()`, not on a Utinni round-trip test (no fixtures). |
| A3 | The live-preview hot-retrigger (D-09) hooks the native effect/particle manager via a CppSharp-exposed runtime call that does not yet exist in `UtinniCore` | D-09, Runtime State | If no such hook is reachable, D-09 may need a new native export (larger scope). MEDIUM risk — this is the explicit Open Question the maintainer flagged. |
| A4 | The exclusive-fullscreen switch is triggered through the DirectInput `SetCooperativeLevel(DISCL_EXCLUSIVE)` path | RESID-04, Pitfall 5 | If the mode switch is actually a direct D3D9 device reset by SWG (not DI-driven), the intercept point differs. MEDIUM — the existing shim's DISCL logging will confirm/deny on first live run. [ASSUMED from prior-art suspicion in chat-open-d3d9-fullscreen.md, not yet confirmed.] |
| A5 | `m_particleWeight.scaleAll(0.28f)` in `EMTR load_0000` is a load-time normalization that is NOT reversed on write | Pitfall 3 | If the writer is expected to re-apply/reverse it, round-trip math differs. LOW — but a concrete byte-exact-round-trip risk; verify against a real file. |
| A6 | No new external packages are required | Standard Stack, Package Audit | If the Particle UI needs a charting/curve-edit control not already in the solution, a package may be needed. LOW — WaveForm editing can reuse existing WinForms controls; flag if a curve editor is desired. |

**Note:** A2, A4, A5 are byte-exactness / live-behavior assumptions that can only be closed by a real `.prt` file + a live injected session — both of which this phase will have. Plan the codec with raw-preserve as the safety net so these assumptions failing degrades gracefully rather than corrupting saves.

## Open Questions

1. **`.prt` version up-convert vs preserve on round-trip.**
   - What we know: SOE `write()` always emits FORM 0002; load handles 0000/0001/0002.
   - What's unclear: Whether opening + saving a 0000/0001 `.prt` should up-convert to 0002 (lossy to original bytes) or preserve the original version for byte-exact round-trip.
   - Recommendation: Preserve the original version form (raw-preserve the version sub-tree) for byte-exact round-trip; only re-serialize typed fields the user actually edited. Confirm with maintainer; this is the central round-trip-fidelity decision.

2. **The D-09 live-preview runtime hook.**
   - What we know: Hot-retrigger requires re-triggering live scene instances via the native effect/particle manager.
   - What's unclear: Whether a reachable native export exists, or a new `UtinniCore` hook must be added (and on which manager — `ParticleManager` vs `ClientEffectManager`).
   - Recommendation: Spike the native side early. If no hook exists, scope a small native export (CppSharp-exposed) and budget for the `Generated/UtinniCore.cs` regen churn. This is the riskiest open item — the maintainer already flagged it.

3. **RESID-04 suppress mechanism specifics.**
   - What we know: Intercept point is the DirectInput `SetCooperativeLevel` shim (or, if A4 is wrong, a D3D9 path).
   - What's unclear: Whether suppressing exclusive cooperative level (forcing `DISCL_NONEXCLUSIVE`) keeps chat/input working AND keeps the device windowed, or whether SWG forces the device mode independently of DI.
   - Recommendation: First live run — log DISCL flags + present-params `Windowed` before/after the trigger; then decide between (a) force non-exclusive DI, (b) intercept the D3D9 mode change, or (c) the deferred detached-fullscreen-with-reattach fallback.

4. **`.cef` (CLEF) inclusion.**
   - What we know: CLEF is a small bundle format referencing `.prt`/appearance/sound/light.
   - What's unclear: Whether the Particle editor should also open the `.cef` wrapper.
   - Recommendation: Ship `.prt` first; treat `.cef` as an optional follow-on within the phase only if time permits.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `swg-client-v2` source corpus (read-only) | `.prt` format spec | ✓ | pinned SHA (per AUTH-01) | none needed (read-only reference) |
| `UtinniPlugins` repo (TJT) | Both editors' UI | ✓ | `D:/Code/UtinniPlugins` | none — standing write authority |
| VS 2026 MSBuild (v145) | Build TJT WinForms + UtinniCore | ✓ | Dev18 v145 | VS 2022 fallback on disk |
| Live injected SWGEmu/Restoration client | PROD-W2-WS/PRT demo, RESID-03 live-observe, RESID-04 repro | ✓ (maintainer-driven) | — | None — Tier-4 manual residual; automation cannot reach (CON-TT-03, REQ-V2-tier-3-mock-d3d9 deferred) |
| `.prt` test asset(s) | Codec round-trip validation | ✗ (no Utinni fixtures today) | — | Extract a real `.prt` from a client `.tre` via the TRE Browser, OR synthesize a minimal PEFT via `IffWriter` (the Datatable/STF synth-fixture precedent) |
| `Utinni.Mcp` (net10) host | `.prt` MCP read tools (D-08) | ✓ | net10, ModelContextProtocol 1.4.0 | none |

**Missing dependencies with no fallback:**
- Live injected client for the three live-dependent deliverables (WS/PRT demo, RESID-03, RESID-04) — this is the expected Tier-4 maintainer-in-the-loop residual, not a blocker for the automatable codec/CLI/UI-build work.

**Missing dependencies with fallback:**
- `.prt` fixtures: extract from a client `.tre` (TRE Browser ships) or synth a minimal PEFT via `IffWriter` for the round-trip golden. Broader fixture coverage is explicitly deferred (Tier-2 follow-up).

## Validation Architecture

> nyquist_validation is not disabled in config.json (key absent → enabled).

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (managed) — `UtinniCoreDotNet.Tests`, `Utinni.Cli.Tests`, `Utinni.Mcp.Tests`; Catch2 (native) — `UtinniCore.Tests` |
| Config file | `.csproj` per test project (verified present) |
| Quick run command | `dotnet test UtinniCoreDotNet.Tests --no-build` (Debug x86) |
| Full suite command | VS2026 MSBuild Debug+Release\|x86, then `dotnet test --no-build` across the three managed test projects + native `UtinniCore.Tests.exe` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PROD-W2-PRT | `.prt` typed decode of a known PEFT | unit | `dotnet test UtinniCoreDotNet.Tests --filter Particle` | ❌ Wave 0 |
| PROD-W2-PRT | `.prt` byte-exact round-trip (edit one typed field, untouched bytes identical) | unit + CLI golden | `roundtrip-particle` verb golden (mirror `roundtrip-ot`) | ❌ Wave 0 |
| PROD-W2-PRT | unrecognized EMTR version → raw-preserve, round-trip byte-exact (D-05) | unit | `dotnet test UtinniCoreDotNet.Tests --filter ParticleDegrade` | ❌ Wave 0 |
| PROD-W2-PRT | MCP read/summarize tool dispatches the decode verb by exit code | integration | `dotnet test Utinni.Mcp.Tests --filter Particle` | ❌ Wave 0 |
| PROD-W2-WS | placements table reflects native node list (count/columns) | unit (framework-leg, per Phase-8/9 precedent) | `dotnet test UtinniCoreDotNet.Tests --filter Snapshot` | ❌ Wave 0 (WinForms host verified by MSBuild-green build; table logic factored to a testable framework helper if possible) |
| PROD-W2-WS | bulk op composes N undo commands atomically | unit | framework-leg test on the command-composition helper | ❌ Wave 0 |
| RESID-03 | `.stf`/`.ot` classify as tier-(b) pending-next-scene-change (badge honesty) | unit | `dotnet test UtinniCoreDotNet.Tests --filter ReloadRouting` | ✅ (`StringTableReloadRoutingTests`, extend for new editors) |
| RESID-03 | SC3 live render-on-reload | manual (Tier-4) | maintainer live session — record in smoke log | n/a (cannot automate without mock-D3D9) |
| RESID-04 | edge-case matrix per-transition behavior; no-Reset held | manual (Tier-4) | maintainer live session — fill matrix | n/a (Tier-4) |
| RESID-04 | `hkPresent`/reparent code asserts no Utinni-initiated `Reset` | unit/grep (native) | Catch2 or grep-gate asserting Utinni never calls `pDevice->Reset` | ❌ Wave 0 (optional regression gate) |

### Sampling Rate
- **Per task commit:** `dotnet test UtinniCoreDotNet.Tests --no-build` (the codec + framework-leg suite)
- **Per wave merge:** full managed suite + native `UtinniCore.Tests.exe`, both Debug+Release\|x86
- **Phase gate:** full suite green + Tier-4 maintainer smoke for the three live-dependent items before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `UtinniCoreDotNet.Tests/Formats/Particle/ParticleCodecTests.cs` — covers PROD-W2-PRT typed decode + degrade
- [ ] `Utinni.Cli.Tests` `roundtrip-particle` golden(s) — byte-exact round-trip (synth or extracted fixture)
- [ ] `Utinni.Mcp.Tests` particle read-tool dispatch test
- [ ] `.prt` fixture: extract-from-`.tre` or synth-via-`IffWriter` (no fixtures exist today)
- [ ] WorldSnapshot table/bulk-op framework-leg helper + test (WinForms host itself stays MSBuild-green-verified per precedent)
- [ ] Extend `ReloadRoutingTests` to assert the new WS/Particle reload paths classify with honest tier-(b) copy

*(WinForms-host UI for both editors is verified by MSBuild-green build, not project-reference tests — the established Phase 8-11 precedent, since the x86 WinForms/native TJT assembly is not project-referenceable from the x86 test project. Factor as much logic as possible into testable `UtinniCoreDotNet` helpers.)*

## Security Domain

> `security_enforcement` is not present in config.json. Treating the relevant subset as applicable — this is a desktop modding tool processing local untrusted asset files, so input-validation and resource-safety are the live concerns; auth/session/network are not.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Local desktop tool; no auth surface |
| V3 Session Management | no | No sessions |
| V4 Access Control | partial | MCP write path is fail-closed to `resolvedRoot` (Phase 14, already enforced); `.prt` read tools inherit it |
| V5 Input Validation | **yes** | `.prt` is untrusted input — `IffPayloadCursor` bounds-checks every read; counts bounded with the division-form guard before looping (DoS cap); degrade-don't-abort on malformed structure (D-05) |
| V6 Cryptography | no | No crypto in this phase (TRE v6000 encryption is out of scope — `.prt` are loose/decrypted assets) |
| V12 File / Resource | **yes** | Loose-override path-containment (shipped `LooseOverridePath`); no path escape on save; backup-before-repack (shipped) |

### Known Threat Patterns for {.prt codec + injected client}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malicious/oversized `.prt` causing OOB read | Tampering / DoS | `IffPayloadCursor` truncation-safe reads → `DecoderException`, never OOB |
| Attacker-controlled emitter/group count → huge allocation | DoS | Division-form count guard before looping reads (the established codec pattern); 16M-cell-style cap |
| Codec abort corrupting a save / refusing legit files | Availability | Degrade-don't-abort raw-preserve (D-05) — never `FATAL` like the SOE reference |
| Save escaping the resolved root | Tampering | Shipped `LooseOverridePath` containment + atomic write (Phase 8/14) |
| Live-preview hook crashing the client | DoS | Game-thread marshalling + heap-free hot path (`project_rh_snapshot_no_heap_alloc`) |
| RESID-04 device Reset crashing the process | DoS | Hard no-Reset constraint (D-13); window-resize-only |

## Sources

### Primary (HIGH confidence — direct file inspection 2026-06-07)
- `UtinniCore/swg/scene/world_snapshot.h` — native `WorldSnapshotReaderWriter`/`WorldSnapshot` API (node list, CRUD, save)
- `UtinniPlugins/.../UI/SubPanels/SnapshotPanel.cs` + `.../SWG/WorldSnapshotImpl.cs` — shipped panel + game-thread-marshalled edit ops
- `UtinniCoreDotNet/Commands/WorldSnapshotCommands.cs` — shipped `IUndoCommand` set
- `UtinniPlugins/.../Plugin.cs` — MEF SubPanel/form registration seam (try/catch, `GetSubPanels()` null)
- `UtinniCoreDotNet/Formats/ObjectTemplate/*`, `Formats/Iff/*`, `Formats/Decoders/IffPayloadCursor.cs` — codec structure + IFF primitives the `.prt` codec follows
- `swg-client-v2/.../clientParticle/src/shared/ParticleEffectDescription.cpp`, `ParticleEmitterGroupDescription.cpp`, `ParticleEmitterDescription.cpp` (15 versions, 468 IFF calls), `ParticleDescriptionQuad/Mesh.cpp`, `ParticleTiming.cpp`, `ParticleTexture.cpp`; `sharedMath/.../WaveForm.cpp` (3 versions); `clientParticle/.../ColorRamp.h` — the `.prt`/PEFT format spec
- `swg-client-v2/.../clientGame/.../clientEffect/ClientEffectTemplate.cpp` — CLEF (`.cef`) format (distinct from `.prt`)
- `UtinniCore/swg/graphics/directx9.cpp` (`hkPresent`/`hkReset`, present-param probe) + `swg/misc/direct_input.cpp` (`SetCooperativeLevel` DISCL-logging vtable shim) + `UtinniCoreDotNet/UI/Controls/PanelGame.cs` (owned-popup reparent, no-Reset comments) — RESID-04 ground truth
- `Utinni.Cli/Program.cs` (object-typed Dispatch past 16-verb cap) + `Utinni.Cli/Commands/*` (25 verb files) — CLI extension pattern
- `.planning/todos/pending/swg-window-resize-fullscreen-edge-cases.md` + `phase10-stringtable-sc3-live-reload-residual.md` + `.planning/debug/chat-open-d3d9-fullscreen.md` — RESID-04/03 specs

### Secondary (MEDIUM confidence — project memory / prior phase records)
- STATE.md decision log (Phase 8-14): hide-not-dispose, `ReloadAssetClassifier` tier-(b), apply-save verbs, MCP dispatch-by-exit-code
- Auto-memory: `project_ot_multichunk_list_params` (degrade-don't-abort), `feedback_d3d9_reset_third_party`, `feedback_imgui_embedded_d3d9_rt_space`, `project_swg_cursor_clip_deadzone`, `project_rh_snapshot_no_heap_alloc`, `project_phase13_cli_verbs`, `project_phase14_mcp_server`, `project_utinnicore_cs_regen_churn`

### Tertiary (LOW confidence — needs live validation)
- A2/A4/A5 byte-exactness + fullscreen-trigger assumptions — confirmable only with a real `.prt` file + live injected session (both available this phase)

## Metadata

**Confidence breakdown:**
- WorldSnapshot editor (PROD-W2-WS): HIGH — entire editing core ships; delta is UI + composition of existing commands
- `.prt` codec (PROD-W2-PRT): MEDIUM — format fully traced in `swg-client-v2`, but large/deeply-nested (15 EMTR versions), no Utinni fixtures, round-trip fidelity unverified; degrade-don't-abort de-risks it
- Particle editor UI + AI read-assist (PROD-W2-PRT): HIGH on the CLI/MCP read-path reuse; MEDIUM on the in-app panel layout (follow Datatable grid)
- Live-preview hook (D-09): MEDIUM-LOW — the explicit open implementation question; native hook may not yet exist
- RESID-04: HIGH on mechanism + constraints (prime suspect identified, intercept point exists, no-Reset enforced); MEDIUM on exact suppress implementation (needs first live run to confirm A4)
- RESID-03: HIGH — pure live-observation + existing tier-(b) badge pattern
- MEF seam / threading / undo / reload / CLI / MCP plumbing: HIGH — all shipped + CI-tested

**Research date:** 2026-06-07
**Valid until:** 2026-07-07 (stable internal codebase; the only volatile input is the live-session findings, which this phase will generate)
