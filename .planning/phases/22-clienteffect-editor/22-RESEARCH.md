# Phase 22: ClientEffect Editor - Research

**Researched:** 2026-06-17
**Domain:** SWG ClientEffect (`FORM CLEF`) command-list codec + verbs + MCP + TJT SubPanel — reuse-by-composition on the shipped IFF DOM / Particle / Terrain stacks.
**Confidence:** HIGH (format truth read from swg-client-v2 source line-by-line; every reuse target read in full; DOM length-ripple mechanism verified in code, not assumed).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Variable-length string edits ARE in scope (appearance / sound / FF refs) PLUS scalar/flag/color edits, all byte-exact. The captured-slice DOM re-stamps parent FORM lengths — planner must VERIFY this holds for a length-changing leaf. *(VERIFIED below — it holds; see § The Length-Ripple Mechanism.)*
- **D-02:** Full list authoring — edit + add + delete + reorder commands. Add/remove requires byte-exact child-chunk insertion/removal + version-FORM length re-stamp. Reorder is cosmetic (SOE: no timing/sequence semantics — confirmed in `ClientEffectTemplate.h:28-32`).
- **D-03:** Preserve the source CLEF version verbatim on save — NEVER upgrade. v0001 CPAP shows name+time only; v0003 exposes scale/rate. Added commands emit at the file's existing version. Do NOT normalize all CPAP to v0003. *(CRITICAL: the SOE `ClientEffectTemplateRW::save` ALWAYS writes TAG_0003 — Utinni must NOT mirror that. See § State of the Art.)*
- **D-04:** Thin docked `EffectsSubPanel` (`IEditorPlugin`) launches a roomy standalone `FormClientEffectEditor` (the `TerrainSubPanel` → `FormTerrainEditor` idiom).
- **D-05:** Name it `EffectsSubPanel` (future growth) but scope to ClientEffect ONLY — no Lightning/Swoosh seams (YAGNI).
- **D-06 (planner's discretion):** Interior controls are planner's. Locked invariants: a flat command-list control + a per-command typed field editor; 5 known commands display typed, unknown tags/versions degrade to raw/hex — never a hard failure. Honor Pitfall 8 (Dock.Fill front-most / nested SplitContainer, size before splitter distance).
- **D-07:** Manual "Preview in client" replay action (NOT auto-on-save). Reuse the Particle editor's existing preview mechanism. *(REALITY CHECK: the Particle "Preview" found NO reachable native hot-retrigger hook — it is honest-candor-only. See § Common Pitfalls / Pitfall 3.)*
- **D-08:** Replay path = game-thread dispatch via `GameCallbacks.AddMainLoopCall`, gated on `Game.IsRunning` first, heap-free hot path (`0x0051fb0a` guard), honest reload candor.
- **D-09:** Entry points = open read-only from TRE Browser → edits write to loose override, AND direct open of an existing loose-override `.iff`.
- **D-10:** Both ClientEffect AND terrain saves land under `<root>/loose/` via `LooseOverridePath.Resolve(resolvedRoot, looseOverrideSubDir)` with `looseOverrideSubDir` defaulting to `"loose"`. Same fail-closed `--root` containment + atomic write as `ApplySaveIffCommand`.
- **D-11:** Surface = `effect-*` verbs (verbs-first) + a `decode-iff` CLEF branch + a thin MCP `summarize_clienteffect` tool that shells `utinni-cli` (ZERO format logic in MCP).
- **D-12:** The 16-verb `CommandLineParser` ceiling is solved (`Type[]` overload + `object` MapResult → Dispatch switch). Adding `effect-*` is a Wave-0 smoke check. *(VERIFIED — CLI is at 26 verbs today; `Program.cs:48-77`.)*
- **D-13:** Synthesized small CLEF fixtures (hand-emitted via `IffWriter`) = committed golden corpus. Matrix: all 3 CPAP versions × each of 5 commands + unknown-version + unknown-command-tag (raw-fallback).
- **D-14:** Additionally source a real per-lineage CLEF `.iff` pair via Utinni's own revived TRE verbs (dogfooding) to pin the exact CLEF versions each client ships. Keep real assets OUT of committed goldens unless small + unencrypted.

### Claude's Discretion
- Internal codec class layout under `UtinniCoreDotNet/Formats/` (precedent: a `ClientEffect/` model subdir + a `Decoders/ClefDecoder.cs`).
- Exact `effect-*` verb names + the `apply-save-effect` flag shape (D-11).
- JSON envelope shape for `decode-effect` / `decode-iff` CLEF output (follow particle/terrain conventions).
- Interior `EffectsSubPanel`/`FormClientEffectEditor` control choice (D-06).
- Whether `decode-effect` is standalone or a thin alias delegating to `DecodeIffCommand` (the `decode-iff` branch is required regardless).

### Deferred Ideas (OUT OF SCOPE)
- Generic multi-format Effects container (Lightning/Swoosh) — future milestone (D-05).
- Auto-replay on save — deliberately not built (D-07).
- Resolving appearance/sound/FF template references — SWG-side fetch, out of the codec's lane.
- Version normalization / upgrade — preserve source version (D-03).
- Standalone renderer of any kind (DEC-A3).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PROD-W2-CFX-01 | Open a ClientEffect `.iff`, view/edit its command list (CPAP/PSND/CLGT/CAMS/FFBK), save byte-exact via loose-override matrix; `EffectsSubPanel` ships inside TJT (DEC-C4). | § CLEF Codec Spec (full per-version field table); § The Length-Ripple Mechanism (byte-exact verified); § TJT SubPanel→Form Idiom; § Loose-Override Save. |
| PROD-W2-CFX-02 | ClientEffect decode/edit/save exposed as golden-tested `utinni-cli` `effect-*` verbs + MCP read tool, reference-validated against the load order, across both lineages. | § Verb / Read Surface; § Reuse Map; § Validation Architecture (both-lineage matrix D-13/D-14). |
</phase_requirements>

## Summary

Phase 22 is a **reuse-by-composition** phase. Every architectural decision is locked in CONTEXT.md; the research value is concrete pinning. Three facts dominate:

1. **The CLEF format is fully known and small.** `FORM CLEF { FORM 0001|0002|0003 { N flat command-chunks } }`. Five command tags (CPAP/PSND/CLGT/CAMS/FFBK). Only CPAP changes across versions (v0002 adds a 1-byte bool, v0003 adds 4 more floats). All scalars are **little-endian** payloads (Pitfall 6); strings are **NUL-terminated C-strings** (verified in `Iff.cpp:1621` `read_string(std::string&)` — searches for a `0x00`, no length prefix). [VERIFIED: swg-client-v2 source]

2. **The byte-exact length-ripple (D-01, "the only new bit") is ALREADY a solved DOM mechanism.** `MutableIffNode.SetPayload` (length-changing) and `AddLeaf`/`Remove`/`Reorder*`/`InsertChildAtInternal` (D-02) all call `MarkDirtyAndInvalidateAncestors` → every ancestor's captured slice is cleared → `IffWriter.WriteContainerFresh` recomputes `innerLen = 4 + Σ child sizes` from actual child bytes. There is NO stored length to keep in sync. The planner does not need to *build* the ripple — only to *exercise* it with a roundtrip golden. [VERIFIED: `MutableIffNode.cs:279-495`, `IffWriter.cs:144-187`]

3. **Two reality checks contradict CONTEXT.md assumptions** (flagged below, not re-decided):
   - The SOE save reference (`ClientEffectTemplateRW.cpp`) **normalizes to TAG_0003 on every write**. D-03 mandates the *opposite* (preserve source version). The Utinni codec must NOT port the SOE writer's version-stamp — it re-emits the captured version FORM verbatim (which the hybrid DOM gives for free).
   - The Particle editor's "Preview in client" (the D-07 reuse target) is **honest-candor-only** — Phase 15's spike found NO reachable native hot-retrigger hook (`FormParticleEditor.cs:710-715` `IsRetriggerHookReachable()` returns hardcoded `false`). "Reuse the Particle preview path" therefore means reuse the *observe-then-label disabled-button discipline*, not a working replay. The ClientEffect Preview should be identically honest.
   - The folded terrain `loose/` todo is **already substantially closed** by Phase 21-06 (R2): `TerrainSaveTargets.SaveLooseOverride` already takes a `looseOverrideSubDir` param defaulting (via `FormTerrainEditor.ResolveLooseOverrideSubDir`) to `"loose"`. The todo file is stale. See § Runtime State Inventory.

**Primary recommendation:** Clone the Particle three-layer stack (`ClientEffect/` model + `Decoders/ClefDecoder.cs` + `effect-*` verbs + `summarize_clienteffect` MCP tool) and the `TerrainSubPanel`→`FormTerrainEditor` UI idiom near-1:1. The ONE genuinely new codec asset is a CLEF field-encoder that supports **variable-length** rewrites (unlike `TrnFieldEncoder`, which is fixed-span-only and explicitly rejects length changes) — the DOM ripple makes this safe.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| CLEF decode/encode (codec) | `UtinniCoreDotNet.dll` (net4.7.2 managed) | — | Pure-managed format logic; compiles into the existing assembly, no `Generated/UtinniCore.cs` touch (pure-managed). |
| Byte-exact framing (FORM length roll-up, no-pad, captured slice) | Shared IFF DOM (`Formats/Iff/*`) | — | Already owns it; CLEF composes, never reimplements (Don't Hand-Roll). |
| `effect-*` verbs (decode/roundtrip/apply-save) | `utinni-cli.exe` (x86) | codec | Verbs-first (DEC-V2-VERBS-FIRST); the golden-test harness. |
| `summarize_clienteffect` MCP tool | `Utinni.Mcp` (net10, OOP) | shells `utinni-cli` | MCP-OOP lock (DEC-V2-MCP-OOP) — ZERO format logic; the subprocess boundary IS the architecture boundary. |
| `EffectsSubPanel` + `FormClientEffectEditor` | `TheJawaToolboxDotNet` (sibling repo, net4.7.2) | codec + save targets | DEC-C4 (editors inside TJT). Thin SubPanel → roomy Form (D-04). |
| Loose-override save | `TJT/Saving/*` (in-proc) + `LooseOverridePath` (framework) | codec | In-proc is the established TJT save idiom; fail-closed `--root` containment. |
| Live preview (replay) | `ClientReloadDispatcher` / honest-candor seam | game thread | Live-in-client only (DEC-A3); honest tier when no hook reachable. |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| (none new) | — | — | **No new external packages.** D-04/CONTEXT explicitly: pure net4.7.2 + the existing net10 MCP. The entire phase composes shipped Utinni assemblies. |

### Supporting (existing in-repo assets the phase composes)
| Asset | Location | Purpose | When to Use |
|-------|----------|---------|-------------|
| `IffReader` / `IffWriter` | `UtinniCoreDotNet/Formats/Iff/{IffReader,IffWriter}.cs` | Tree-walk read + EA-IFF-85 write with SWG no-pad quirk + FORM-length roll-up. | All CLEF parse + serialize. |
| `MutableIffDocument` / `MutableIffNode` | same dir | Captured-slice hybrid DOM; verbatim re-emit of clean nodes; ancestor invalidation on edit. | The byte-exact edit engine — D-01 + D-02 ride this directly. |
| `IffPayloadCursor` | `Formats/Decoders/IffPayloadCursor.cs` | Bounds-checked LE scalar reader: `ReadInt32Le`, `ReadInt8`, `ReadFloatLe`, `ReadCString(Encoding)`. | The CLEF decoder reads each command chunk's payload through this — every primitive CLEF needs already exists. |
| `MutableParticleEffect` / `ParticleEffectDocument` | `Formats/Particle/*` | The model+codec shape (FromBytes → IffReader → MutableIffDocument → typed model; `Serialize()`; raw-preserve on unknown version). | The CLEF model clones this near-1:1. |
| `LooseOverridePath.Resolve` | `UtinniCoreDotNet/Saving/` | Fail-closed `<root>` containment (rooted/`..` rejection). | The save path (D-10). |
| `ClientReloadDispatcher` | `TJT/Saving/ClientReloadDispatcher.cs` | Game-thread-dispatched, `Game.IsRunning`-gated reload tier + honest candor. | The D-07/D-08 preview path (degrades honestly). |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `TrnFieldEncoder` for the CLEF encode side | (reuse it directly) | REJECTED — `TrnFieldEncoder` is **fixed-span-only** and rejects any length change (`ApplySaveTrnCommand.cs:178-183`). CLEF's primary edit IS length-changing (D-01). The CLEF encoder must be a new class that rewrites a whole command-chunk payload (LE scalars + NUL C-string), reusing the LE primitive idiom (`BitConverter.GetBytes(f)` + reverse on big-endian host, `TrnFieldEncoder.cs:159-160`) but NOT the span-replacement contract. |
| New `decode-effect` standalone command | thin alias delegating to `DecodeIffCommand.BuildClefResult` | Either is fine (D-discretion). `DecodeTrnCommand.cs` is the alias-delegation precedent (delegates to `DecodeIffCommand.BuildTerrainResult`). The `decode-iff` CLEF branch is required regardless. |

**Installation:** none — `git`-tracked source only.

## Package Legitimacy Audit

> Not applicable — this phase installs ZERO external packages (pure-managed composition of in-repo assemblies). No npm/PyPI/NuGet/crates additions. slopcheck N/A.

## CLEF Codec Spec (the per-command, per-version field table)

**Container shape** (`ClientEffectTemplate.cpp:211-263`): root is `FORM CLEF`; its single child is a version FORM `0001` | `0002` | `0003`; that FORM holds N flat command chunks in any order (the load loop is a `while(!atEndOfForm)` switch — `ClientEffectTemplate.cpp:275`). A 4th-or-higher version `DEBUG_FATAL`s in SOE — Utinni raw-fallbacks instead (D-13). [VERIFIED: swg-client-v2]

**Byte framing** (all command-chunk payloads):
- Chunk tag (4 bytes) + length (4 bytes) = **big-endian** (the IFF framing; `IffReader.ReadInt32Be`).
- Payload scalars (`int32`, `float`, `uint8`, `bool8`) = **little-endian** (Pitfall 6; SOE `read_misc` is a raw memcpy → native LE on the x86 Win32 client; `Iff.h:575-656`). [VERIFIED]
- Strings = **NUL-terminated C-string**, no length prefix, no pad (`Iff.cpp:1621-1646` `read_string(std::string&)` scans for `0x00`, consumes the terminator). The on-disk byte count = `strlen + 1`. [VERIFIED]
- `bool8` = 1 byte (`Iff.h:518`); `uint8` = 1 byte; `float` = 4 bytes IEEE-754; `int32` = 4 bytes.

### CPAP — CreateAppearance (the ONLY version-variant command)
| Field | Type | Bytes | v0001 | v0002 | v0003 | Source line |
|-------|------|-------|:---:|:---:|:---:|-------------|
| appearanceTemplateName | C-string (NUL-term) | strlen+1 | ✓ | ✓ | ✓ | `.cpp:283,378,474` |
| timeInSeconds | float LE | 4 | ✓ | ✓ | ✓ | `.cpp:284,379,475` |
| softParticleTerminate | bool8 LE | 1 | — | ✓ | ✓ | `.cpp:380,476` |
| minScale | float LE | 4 | — | — | ✓ | `.cpp:477` |
| maxScale | float LE | 4 | — | — | ✓ | `.cpp:478` |
| minPlaybackRate | float LE | 4 | — | — | ✓ | `.cpp:479` |
| maxPlaybackRate | float LE | 4 | — | — | ✓ | `.cpp:480` |

CPAP payload sizes: v0001 = `strlen+1+4`; v0002 = `strlen+1+4+1`; v0003 = `strlen+1+4+1+16`. The trailing `appearanceTemplate` fetch (`.cpp:285` etc.) and `ignoreDuration` (`.h:56`) are RUNTIME-only (resolved pointer / runtime flag) — NOT on disk; the codec ignores them.

### PSND — PlaySound (stable across all 3 versions)
| Field | Type | Bytes | Source |
|-------|------|-------|--------|
| soundTemplateName | C-string (NUL-term) | strlen+1 | `.cpp:295,391,491` |

### CLGT — CreateLight (stable)
| Field | Type | Bytes | Source |
|-------|------|-------|--------|
| r | uint8 LE | 1 | `.cpp:306` |
| g | uint8 LE | 1 | `.cpp:307` |
| b | uint8 LE | 1 | `.cpp:308` |
| timeInSeconds | float LE | 4 | `.cpp:309` |
| constantAttenuation | float LE | 4 | `.cpp:310` |
| linearAttenuation | float LE | 4 | `.cpp:311` |
| quadraticAttenuation | float LE | 4 | `.cpp:312` |
| range | float LE | 4 | `.cpp:313` |

CLGT payload = 3 + 5×4 = **23 bytes** (odd → the no-pad quirk matters; `IffWriter` emits no pad, `IffReader` detects-but-doesn't-require one).

### CAMS — CameraShake (stable)
| Field | Type | Bytes | Source |
|-------|------|-------|--------|
| magnitudeInMeters | float LE | 4 | `.cpp:323` |
| frequencyInHz | float LE | 4 | `.cpp:324` |
| timeInSeconds | float LE | 4 | `.cpp:325` |
| falloffRadius | float LE | 4 | `.cpp:326` |

CAMS payload = **16 bytes**.

### FFBK — ForceFeedback (stable)
| Field | Type | Bytes | Source |
|-------|------|-------|--------|
| forceFeedbackFile | C-string (NUL-term) | strlen+1 | `.cpp:336` |
| iterations | int32 LE | 4 | `.cpp:338` |
| range | float LE | 4 | `.cpp:339` |

**Read order matters for FFBK and CPAP** (string THEN scalars). The codec must read in the exact `.cpp` order to land at the right offsets.

### Unknown command tag (raw-fallback)
SOE `default:` branch (`.cpp:345-353`) `enterChunk()/exitChunk(true)` to skip an unknown command. Utinni must **raw-preserve** the unknown chunk's captured bytes and re-emit verbatim (D-13 unknown-command-tag case) — the hybrid DOM gives this for free (a leaf whose payload is never edited re-emits its captured slice).

## The Length-Ripple Mechanism (D-01 — VERIFIED, the "only new bit")

**Claim to verify (D-01):** the captured-slice DOM re-stamps ancestor FORM lengths correctly when (a) a LEAF chunk changes length (string edit) and (b) a child chunk is inserted/removed (D-02).

**Finding: VERIFIED in code — it holds for both.** [VERIFIED: source]

(a) **Length-changing leaf edit.** `MutableIffNode.SetPayload(byte[])` (`MutableIffNode.cs:279-286`) replaces the payload (any length) and calls `MarkDirtyAndInvalidateAncestors()` (`:484-495`) which sets `IsDirty` and **clears `capturedSlice`** on the node AND every ancestor up the parent chain. On write, `IffWriter.WriteNode` (`IffWriter.cs:98-121`) sees the cleared slice → reserializes fresh; the container path `WriteContainerFresh` (`:144-187`) recomputes `innerLen = 4 + Σ child serialized sizes` under a `checked` block from the ACTUAL child bytes — there is no stored length to keep stale. So a longer/shorter appearance string ripples the version FORM and the CLEF FORM lengths automatically.

(b) **Add / remove / reorder.** `AddLeaf` (`:306-314`), `Remove` (`:332-342`), `ReorderUp`/`ReorderDown` (`:381-404`), `InsertChildAtInternal` (`:467-478, the undo-restore path`) all call `MarkDirtyAndInvalidateAncestors`. Same roll-up applies. A new command is a fresh dirty leaf (no captured slice) → reserialized fresh; the version FORM length re-rolls to include it.

**No gap to close. The planner's job is to EXERCISE it, not build it:**
- A `roundtrip-effect` golden (no edit) proving byte-exact identity on every D-13 fixture (clean nodes re-emit verbatim — the strongest byte-exact gate; same idiom as `RoundtripParticleCommand.cs:74-95`).
- A length-changing string-edit test asserting: (i) output re-parses, (ii) the edited chunk's payload = the new string framing, (iii) the version FORM + CLEF FORM lengths equal the recomputed totals, (iv) all UNTOUCHED command chunks are byte-identical.

**One nuance to flag (not a blocker):** the `apply-save-trn` verify strategy (`OnlyTargetSpanDiffers`, `ApplySaveTrnCommand.cs:383-392`) CANNOT be reused verbatim — it asserts a fixed-length span and explicitly rejects length changes (`:178-183`). The `apply-save-effect` verify must instead assert "untouched command chunks byte-identical + output re-parses + the edited command decodes to the requested value" (a structural verify, not a byte-span verify). This is the single design delta the planner must spell out.

## Architecture Patterns

### System Architecture Diagram
```
            ┌──────────────────────────────────────────────────────────┐
  .iff      │  IffReader.Read(bytes)  ──►  IffDocument (immutable tree)  │
  bytes ───►│         │                                                 │
            │         ▼                                                 │
            │  MutableIffDocument.FromDocument(doc, bytes)              │
            │   (captures verbatim slice per node)                      │
            └─────────┬────────────────────────────────────────────────┘
                      ▼
        ┌──────────────────────────────────────────┐   root FORM == CLEF?
        │  ClefDocument.FromMutableIff(...)         │◄──┐
        │   • find version FORM (0001/0002/0003)    │   │ decode-iff branch
        │   • known? → typed CommandView list       │   │ (SubTypeId=="CLEF")
        │   • unknown version → raw-preserve whole  │   │
        │   • per command: known tag? typed : raw   │   │
        └───────┬───────────────────────────┬───────┘
                │                           │
   edit string/scalar              add/remove/reorder
   (SetPayload, len-change)        (AddLeaf/Remove/Reorder)
                │                           │
                └────────────┬──────────────┘
                             ▼  MarkDirtyAndInvalidateAncestors (ancestor slices cleared)
                   IffWriter.Write(mutable)  → FORM lengths re-rolled from child bytes
                             │
        ┌────────────────────┼─────────────────────────────────┐
        ▼                    ▼                                  ▼
  roundtrip-effect    apply-save-effect                  FormClientEffectEditor
  (byte-exact gate)   → LooseOverridePath.Resolve(root,"loose")   (TJT, DEC-C4)
                      → atomic write → ClientReloadDispatcher
                                                          (honest-candor Preview, D-07)
   decode-effect / decode-iff(CLEF) ─► JSON envelope ◄── summarize_clienteffect (MCP, shells utinni-cli)
```

### Recommended Project Structure (Claude's-discretion precedent)
```
UtinniCoreDotNet/Formats/ClientEffect/      # mirrors Formats/Particle/
├── ClientEffectDocument.cs                 # FromBytes/FromIff entry (clone ParticleEffectDocument)
├── MutableClientEffect.cs                  # typed model over MutableIffDocument (clone MutableParticleEffect)
├── ClientEffectCommand.cs                  # per-command typed view (5 kinds + raw)
├── ClefFieldCodec.cs                       # variable-length LE/cstring encode+decode (NEW — not TrnFieldEncoder)
└── ClientEffectParseException.cs           # clone ParticleParseException
UtinniCoreDotNet/Formats/Decoders/
└── ClefDecoder.cs                          # LooksLikeClientEffect + Decode dispatch (clone TgenDecoder shape)
Utinni.Cli/Commands/
├── DecodeEffectCommand.cs                  # alias delegating to DecodeIffCommand.BuildClefResult (cf DecodeTrnCommand)
├── RoundtripEffectCommand.cs              # clone RoundtripParticleCommand
└── ApplySaveEffectCommand.cs              # clone ApplySaveTrnCommand BUT variable-length verify
Utinni.Mcp/Tools/ReadTools.cs              # +summarize_clienteffect (clone summarize_terrain)
The Jawa Toolbox/.../UI/SubPanels/EffectsSubPanel.cs   # clone TerrainSubPanel
The Jawa Toolbox/.../UI/Forms/FormClientEffectEditor.cs # clone FormTerrainEditor/FormParticleEditor
The Jawa Toolbox/.../Saving/ClientEffectSaveTargets.cs  # clone IffSaveTargets (in-proc, looseOverrideSubDir)
```

### Pattern 1: Decode = compose, never re-frame
```csharp
// Source: ParticleEffectDocument.cs:49-59 (clone for CLEF)
public static MutableClientEffect FromBytes(byte[] bytes) {
    IffDocument iffDoc;
    using (var ms = new MemoryStream(bytes, writable: false))
        iffDoc = IffReader.Read(ms);                 // shared tree-walk reader
    MutableIffDocument mutableIff = MutableIffDocument.FromDocument(iffDoc, bytes);
    return MutableClientEffect.FromMutableIff(mutableIff);  // typed overlay; raw-preserve unknown
}
```

### Pattern 2: decode-iff CLEF branch (MCP routing for free, D-11)
```csharp
// Source: DecodeIffCommand.cs:92-97 (PEFT branch is the exact template)
if ((doc.Root as IffContainerChunk)?.SubTypeId == "CLEF")
    return JsonOutput.EmitSuccess("decode-iff",
        BuildClefResult(ClientEffectDocument.FromIff(doc, bytes), o.Path));
```
Add a `catch (ClientEffectParseException ex)` to the existing catch ladder (`DecodeIffCommand.cs:117-136`).

### Pattern 3: MCP read tool (ZERO format logic)
```csharp
// Source: ReadTools.cs:112-127 (summarize_terrain) — copy verbatim, change Name/Description, dispatch "decode-iff"
[McpServerTool(Name = "summarize_clienteffect", ReadOnly = true, Idempotent = true)]
public static async Task<CallToolResult> SummarizeClientEffect(ResolvedRoot root, CliDispatcher cli,
    [Description(PathParamDescription)] string relativePath) {
    string abs = root.Resolve(relativePath);
    var r = await cli.RunAsync("decode-iff", new[] { abs }).ConfigureAwait(false);
    return CliResultMapper.ToCallToolResult(r);
}
```

### Anti-Patterns to Avoid
- **Porting `ClientEffectTemplateRW::save`.** It hard-stamps TAG_0003 (`ClientEffectTemplateRW.cpp:86,156`). Mirroring it VIOLATES D-03. The Utinni save re-emits the captured version FORM verbatim via the hybrid DOM — never re-versions.
- **Reusing `TrnFieldEncoder` for CLEF.** It is fixed-span and rejects length changes — wrong tool for D-01.
- **A real "live retrigger" Preview.** No native hook exists (see Pitfall 3); promising one violates the honest-candor discipline.
- **Re-implementing IFF framing / no-pad / endianness.** All in the shared primitives (Don't Hand-Roll).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| FORM length re-stamp on edit | manual length bookkeeping | `MutableIffNode` ancestor invalidation + `IffWriter` roll-up | Already correct; manual sync is the classic byte-exact bug. |
| LE scalar / C-string read | hand byte-shift | `IffPayloadCursor` (`ReadInt32Le`/`ReadInt8`/`ReadFloatLe`/`ReadCString`) | Bounds-checked, host-endian-independent, golden-tested. |
| LE scalar write | `BinaryWriter` (LE by luck) | `BitConverter.GetBytes(x)` + reverse-on-BE-host idiom | `TrnFieldEncoder.cs:159-160` — host-independent, matches the read assembly. |
| no-pad framing | pad logic | `IffWriter` (omits pad) + `IffReader` (detects) | SWG no-pad quirk already handled. |
| path containment | manual `..` checks | `LooseOverridePath.Resolve` | Fail-closed, security-reviewed. |
| atomic write + reload-race barrier | bare `File.Write` | `IffSaveTargets.WriteAtomic` (`Flush(true)`) | MEDIUM-9 stale-bytes reload race. |
| MEF-safe SubPanel ctor | throwing ctor | `TerrainSubPanel`'s try/catch + state-label pattern | Pitfall 8 — a throwing ctor silently drops the whole IEditorPlugin. |

**Key insight:** the byte-exact bar that *looks* scary (variable-length string edits, add/remove) is the one thing already fully solved by the DOM. The actual new code is a small typed field codec + UI glue.

## Runtime State Inventory

> This phase touches a rename-adjacent concern only via the folded terrain todo. ClientEffect work is greenfield-codec + new files. The relevant inventory is the **folded-todo state**:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — CLEF assets live in TRE/loose `.iff`; no DB/datastore keys involved. | None. |
| Live service config | None. | None. |
| OS-registered state | None. | None. |
| Secrets/env vars | None. | None. |
| Build artifacts | None new. New `.cs` files compile into existing `UtinniCoreDotNet.dll` / `utinni-cli.exe` / `Utinni.Mcp` / `TheJawaToolboxDotNet.dll`; `Generated/UtinniCore.cs` is NOT touched (pure-managed). | Standard MSBuild + cross-repo paired commit. |
| **Folded-todo reality** | `TerrainSaveTargets.SaveLooseOverride` **already takes `looseOverrideSubDir`** (`TerrainSaveTargets.cs:288-297`, added 21-06 R2) and `FormTerrainEditor.ResolveLooseOverrideSubDir()` (`:1205-1221`) already defaults to `"loose"`. The todo file `phase21-terrain-override-loose-subdir.md` still says `status: OPEN` — **STALE**. | Planner: (1) verify a test asserts the resolved terrain destination is under `<root>\loose\` (the todo's 3rd "to close" item — confirm it exists, else add it); (2) confirm `apply-save-trn` CLI uses the same subdir convention (the CLI `ApplySaveTrnCommand.cs:102` resolves `LooseOverridePath.Resolve(o.Root, o.RelAsset)` with NO subdir — **the CLI half may still be off**; align if the caller does not pre-compose `loose/`); (3) move the todo file pending→done. |

**The folded-todo is NOT a from-scratch fix.** Most of it shipped in 21-06. The residual is verification + the CLI-vs-editor subdir-convention alignment + closing the stale todo. Flag for the planner: confirm whether `apply-save-trn`'s caller (MCP/test) passes a `loose/`-prefixed `--root` or rel-asset, since the CLI itself does not append a subdir.

## Common Pitfalls

### Pitfall 1: Stamping the write version (D-03 violation)
**What goes wrong:** porting the SOE `save()` shape re-versions every CLEF to 0003 on write.
**Why it happens:** `ClientEffectTemplateRW.cpp:86` literally writes `TAG_0003`.
**How to avoid:** never reserialize the version FORM from scratch with a chosen version. Edit command chunks in place; the version FORM re-emits its (captured or recomputed) tag unchanged. A v0001 file stays v0001.
**Warning signs:** a v0001 roundtrip golden comes back as v0003 / different length.

### Pitfall 2: NUL-terminated string framing (not length-prefixed)
**What goes wrong:** writing a 4-byte length prefix before the string (the datatable idiom) corrupts the chunk.
**Why it happens:** other SWG formats DO length-prefix; CLEF does not.
**How to avoid:** on-disk string = raw bytes + single `0x00`. On-disk byte count = `strlen+1`. Use `IffPayloadCursor.ReadCString` to read; write `Encoding.ASCII.GetBytes(s)` + `0x00`. [VERIFIED: `Iff.cpp:1621-1646`]
**Warning signs:** string roundtrip off-by-the-length-prefix; trailing garbage.

### Pitfall 3: Preview promises a live retrigger that does not exist
**What goes wrong:** wiring a "Re-trigger live instance" path that silently no-ops, or implying one exists.
**Why it happens:** D-07 says "reuse the Particle preview" — but the Particle preview's `IsRetriggerHookReachable()` is hardcoded `false` (`FormParticleEditor.cs:710-715`); Phase 15's spike found NO reachable native hot-retrigger hook.
**How to avoid:** clone the **honest-candor seam** exactly: button enabled when a doc is open; on click, branch on `Game.IsRunning && IsRetriggerHookReachable()`; if false, surface the LOCKED degraded copy ("Live preview isn't wired this build — edits show on the next scene change or relog") with dimmed styling and perform NO action. Keep the no-client and no-hook messages distinct. The `ClientReloadDispatcher.Dispatch` path (`ClientReloadDispatcher.cs:80-128`) is reachable for *file reload* but `.iff`/ClientEffect classifies to `PendingNextSceneChange` (no live binding) — so the honest tier is the correct one.
**Warning signs:** a Preview button that claims success but nothing fires in-client; over-promising copy.

### Pitfall 4: WinForms Dock.Fill / SplitContainer ordering (Pitfall 8)
**What goes wrong:** a Dock.Fill control sent to back starves Top/Bottom siblings; a SplitContainer splitter set before Size throws.
**How to avoid:** add Dock.Bottom/Top strips FIRST (they claim edges), keep Fill front-most; for the list+field split use a nested `SplitContainer`, set Size before SplitterDistance. `TerrainSubPanel.BuildContent` (`:128-214`) is the exact template.
**Warning signs:** empty editor body; `InvalidOperationException` on SplitterDistance.

### Pitfall 5: A throwing IEditorPlugin/SubPanel ctor silently drops the whole TJT plugin (MEF)
**How to avoid:** wrap the WHOLE SubPanel ctor build in try/catch and surface a read-only state label on failure (`TerrainSubPanel.cs:110-121, 217-236`). The undo seam is NULL until FormMain wires it — every call site null-checks via `?.Invoke()`.

### Pitfall 6: Endianness split (tags BE, payload LE)
**How to avoid:** IFF tags+lengths are big-endian (`IffReader.ReadInt32Be`); chunk PAYLOAD scalars are little-endian (`IffPayloadCursor` reads/writes LE). Never read a payload float big-endian. [VERIFIED]

## Code Examples

### Reading a CPAP command by version (decode side)
```csharp
// Pattern from IffPayloadCursor.cs:62-153 + the CLEF version table above.
var cur = new IffPayloadCursor(cpapLeaf.GetPayloadCopy());
string name = cur.ReadCString(Encoding.ASCII);   // NUL-terminated, strlen+1 bytes
float time   = cur.ReadFloatLe();                // v0001 ends here
if (version != "0001") {
    bool softTerminate = cur.ReadInt8() != 0;     // bool8, v0002+
    if (version == "0003") {
        float minScale = cur.ReadFloatLe();
        float maxScale = cur.ReadFloatLe();
        float minRate  = cur.ReadFloatLe();
        float maxRate  = cur.ReadFloatLe();
    }
}
```

### Writing a command-chunk payload (encode side — variable length, the NEW codec)
```csharp
// LE primitives mirror TrnFieldEncoder.cs:159-160 (host-independent).
static byte[] FloatLe(float f){ var b=BitConverter.GetBytes(f); if(!BitConverter.IsLittleEndian) Array.Reverse(b); return b; }
using (var ms = new MemoryStream()) {
    byte[] s = Encoding.ASCII.GetBytes(name); ms.Write(s,0,s.Length); ms.WriteByte(0x00); // C-string
    ms.Write(FloatLe(time),0,4);
    // ... version-conditional fields ...
    cpapLeaf.SetPayload(ms.ToArray());  // length change ripples FORM lengths automatically (verified)
}
```

### Roundtrip byte-exact gate
```csharp
// Source: RoundtripParticleCommand.cs:74-95 — clone for effect.
byte[] loaded = File.ReadAllBytes(o.Path);
var model = ClientEffectDocument.FromBytes(loaded);
byte[] rt = model.Serialize();                       // = IffWriter.Write(mutable)
bool identical = loaded.Length==rt.Length && loaded.SequenceEqual(rt);
```

## State of the Art

| Old Approach (SOE / Phase 20) | Current Approach (Phase 22) | Why Changed | Impact |
|--------------|------------------|--------------|--------|
| `ClientEffectTemplateRW::save` re-stamps TAG_0003 | preserve source version verbatim (D-03) | both lineages ship different versions; byte-exact gate | Utinni does NOT port the SOE writer; hybrid DOM re-emits the captured version FORM. |
| `apply-save-trn` fixed-span verify (rejects length changes) | `apply-save-effect` variable-length structural verify | CLEF's primary edit IS a length change (D-01) | New verify logic: untouched-chunks-identical + re-parse + decoded-value-matches, not `OnlyTargetSpanDiffers`. |
| Particle Preview implies a (future) live retrigger | honest-candor-only Preview, no-op when no hook | no reachable native hook this build | The ClientEffect Preview is a labelled disabled-path, not a working replay. |

**Deprecated/outdated:**
- `phase21-terrain-override-loose-subdir.md` (still `status: OPEN`) — the code fix mostly shipped in 21-06; the file is stale (see Runtime State Inventory).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Each live client (SWGEmu / Restoration) ships CLEF at specific versions — to be PINNED via D-14 real-asset extraction, not assumed here. | CLEF Codec Spec / Validation | If a lineage ships v0004+, the codec raw-fallbacks it (safe by D-13) but the typed-edit coverage gap is unknown until extracted. The codec degrade-don't-abort posture means no hard failure either way. |
| A2 | The `apply-save-trn` CLI half of the loose-`/` subdir convention may still omit the subdir (`ApplySaveTrnCommand.cs:102` resolves with no subdir). | Runtime State Inventory | If the CLI's caller doesn't pre-compose `loose/`, the CLI-written terrain override still lands off the searchPath. Planner must confirm the actual CLI call shape. [ASSUMED — inferred from the CLI source; the caller convention not traced to a test.] |
| A3 | CLEF assets in the modded clients are small + unencrypted enough to extract via `utinni-cli` TRE verbs (retail high-era TRE is v6000/encrypted → enumerate-only). | Validation (D-14) | If the only available CLEF lives in an encrypted v6000 TRE, D-14's real-asset pair is unreachable from that lineage; fall back to the SWGEmu lineage + synthesized goldens (D-13 still fully covers the version×command matrix). [ASSUMED] |

## Open Questions (RESOLVED)

1. **Does an `apply-save-trn` byte-parity test already assert `<root>\loose\`?**
   - **RESOLVED (Plan 22-03):** The framework-side `UtinniCoreDotNet.Tests/SavingTests/TerrainLooseOverridePathTests.cs` already asserts the `\loose\` segment (21-06 R2). The residual — the CLI half (`apply-save-trn` resolved with NO subdir, A2) — is fixed in Plan 22-03: a `--loose-subdir` (default `"loose"`) two-step compose lands the CLI override under `<root>/loose/`, a new test asserts it, and the stale folded todo is closed pending→done.
   - What we knew: `TerrainSaveTargets` threads `looseOverrideSubDir`; the todo's 3rd close-item asks for such a test.
   - Original recommendation: planner greps the test suite for an assertion on `\loose\` in the terrain save path; add one if absent (cheap, closes the folded todo cleanly).

2. **Exact `effect-*` verb names + `apply-save-effect` flag shape (Claude's discretion, D-11).**
   - **RESOLVED (Plan 22-02 `<verb_decisions>`):** verbs are `decode-effect` / `roundtrip-effect` / `apply-save-effect`. `apply-save-effect` field edits use `--leaf/--field/--value` (length-changing allowed; the fixed-length guard is removed); list authoring uses `--add-command <tag>` / `--remove-leaf <stableId>` / `--reorder <stableId> up|down`, exactly ONE mutation per invocation. `decode-effect` is a thin alias delegating to `DecodeIffCommand.BuildClefResult`.
   - What we knew: precedent is `apply-save-trn --root --leaf --field --value`. CLEF needs list-mutation (add/remove/reorder) which `--leaf/--field/--value` does not express.
   - Original recommendation: keep field-edit on `--leaf/--field/--value`; express add/remove/reorder as separate flags or scope CLI to field edits while the in-app editor owns list authoring. Decided in PLAN (both: the CLI expresses all of it; the DOM supports it).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| VS2026 MSBuild (v145/x86) | managed build of all 4 assemblies | ✓ | Dev18 | — (AGENTS.md: never `dotnet build` — MSB3823 on WinForms .resx) |
| `dotnet test --no-build` | xUnit codec + verb goldens | ✓ | net | — |
| `utinni-cli` TRE verbs | D-14 real-asset extraction | ✓ | v145 | SWGEmu lineage + synthesized goldens if a lineage's CLEF is encrypted |
| Live SWG client | D-07 Preview smoke (maintainer-only) | n/a here | — | honest-candor degrade (no live hook this build anyway) |
| sibling `UtinniPlugins` repo | `EffectsSubPanel`/`FormClientEffectEditor` | ✓ | `D:/Code/UtinniPlugins` | standing cross-repo write authority; paired commit, no checkpoint except live smoke |

**Missing dependencies with no fallback:** none.

## Validation Architecture

> nyquist_validation is not disabled — section included.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (`UtinniCoreDotNet.Tests`, `Utinni.Cli.Tests`) |
| Config file | existing test projects (no new config) |
| Quick run command | `dotnet test --no-build --filter "FullyQualifiedName~ClientEffect"` |
| Full suite command | MSBuild `Utinni.sln /p:Configuration=Release /p:Platform=x86` then `dotnet test --no-build` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PROD-W2-CFX-01 | byte-exact roundtrip, all D-13 fixtures (no edit) | unit | `dotnet test --no-build --filter "Name~Roundtrip&Name~ClientEffect"` | ❌ Wave 0 |
| PROD-W2-CFX-01 | length-changing string edit re-parses + untouched chunks identical | unit | `... --filter "Name~ClefStringEdit"` | ❌ Wave 0 |
| PROD-W2-CFX-01 | add / remove / reorder command → version FORM length re-rolls, re-parses | unit | `... --filter "Name~ClefListMutation"` | ❌ Wave 0 |
| PROD-W2-CFX-01 | scalar/flag/color CPAP/CLGT/CAMS edit byte-exact except target | unit | `... --filter "Name~ClefFieldEdit"` | ❌ Wave 0 |
| PROD-W2-CFX-01 | unknown version + unknown command tag → raw-fallback, roundtrip byte-exact | unit | `... --filter "Name~ClefRawFallback"` | ❌ Wave 0 |
| PROD-W2-CFX-01 | loose-override save lands under `<root>\loose\`, fail-closed containment | unit | `... --filter "Name~ClefLooseOverride"` | ❌ Wave 0 |
| (folded) | terrain override save lands under `<root>\loose\` | unit | `... --filter "Name~TerrainLooseSubdir"` | ❓ verify exists |
| PROD-W2-CFX-02 | `decode-effect` / `decode-iff` CLEF branch JSON envelope shape | integration | `... --filter "Name~DecodeEffect"` | ❌ Wave 0 |
| PROD-W2-CFX-02 | `roundtrip-effect` exit codes (0/2/3) | integration | `... --filter "Name~RoundtripEffectCommand"` | ❌ Wave 0 |
| PROD-W2-CFX-02 | `apply-save-effect` verify + atomic commit + fail-closed | integration | `... --filter "Name~ApplySaveEffect"` | ❌ Wave 0 |
| PROD-W2-CFX-02 | CLI `--help` enumerates the new `effect-*` verbs (D-12 smoke) | smoke | `... --filter "Name~CliHelpEnumerates"` | ❓ verify pattern |
| PROD-W2-CFX-02 | reference-validation against the SOE load order (decode reproduces the field order/values) | unit | `... --filter "Name~ClefLoadOrder"` | ❌ Wave 0 |

### Both-Lineage Golden Fixture Matrix (D-13 + D-14)
Synthesized, hand-emitted via `IffWriter` (deterministic, tiny), committed:

| Fixture | CPAP version | Commands present | Purpose |
|---------|:---:|---|---|
| `clef_v0001_cpap.iff` | 0001 | CPAP | name+time only field set |
| `clef_v0002_cpap.iff` | 0002 | CPAP | + softParticleTerminate bool8 |
| `clef_v0003_cpap.iff` | 0003 | CPAP | + min/max scale + min/max rate |
| `clef_v0003_all5.iff` | 0003 | CPAP+PSND+CLGT+CAMS+FFBK | full command coverage, ordering |
| `clef_v0001_all5.iff` | 0001 | all 5 | stable-command coverage at oldest version |
| `clef_unknown_version.iff` | 9999 | — | raw-fallback whole CLEF FORM |
| `clef_unknown_command.iff` | 0003 | CPAP + `XXXX` unknown tag | raw-preserve unknown chunk, re-emit verbatim |
| `clef_empty.iff` | 0003 | (none) | empty version FORM edge case |

D-14: extract ONE real CLEF `.iff` per reachable lineage via `utinni-cli` TRE verbs; run `roundtrip-effect` as an extra byte-exact check; use it to confirm which versions feed the synthesized matrix. Keep OUT of committed goldens unless small + unencrypted.

### Sampling Rate
- **Per task commit:** `dotnet test --no-build --filter "FullyQualifiedName~ClientEffect"` (codec/verb subset, <30s).
- **Per wave merge:** full `dotnet test --no-build`.
- **Phase gate:** full suite green + maintainer live-smoke (Preview honest-candor + a real save→reload) before `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] CLEF golden fixtures (the 8 synthesized `.iff` above) — covers PROD-W2-CFX-01/02
- [ ] `ClientEffectCodecTests.cs` — roundtrip / string-edit / list-mutation / field-edit / raw-fallback / load-order
- [ ] `ApplySaveEffectCommandTests.cs`, `RoundtripEffectCommandTests.cs`, `DecodeEffectTests.cs`
- [ ] `ClefLooseOverrideTests.cs` — assert `<root>\loose\` destination + containment
- [ ] Verify/add a terrain `<root>\loose\` destination test (folded-todo close-item #3)
- [ ] Confirm an existing CLI `--help`-enumerates pattern test, or add one for the `effect-*` verbs (D-12)

## Security Domain

> `security_enforcement` not explicitly false — section included.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V5 Input Validation | yes | `IffReader` 64MB chunk cap + negative/overflow length guards; `IffPayloadCursor.Need()` bounds-checks every read; forged-count caps (cf `MutableParticleEffect.MaxNodeCount`). |
| V12 File / Resource | yes | `LooseOverridePath.Resolve` fail-closed `..`/rooted rejection + normalized StartsWith; atomic write with `Flush(true)`. MCP `ResolvedRoot.Resolve` rejects path escape (T-14-01). |
| V6 Cryptography | no | no crypto in this phase (encrypted v6000 TRE is enumerate-only, not decoded here). |
| V2/V3/V4 Auth/Session/Access | no | local offline editing tool; no auth surface. |

### Known Threat Patterns for a binary IFF codec
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Forged chunk length / count → OOM | DoS | 64MB cap (`IffReader`/`IffWriter`), `MaxNodeCount`-style guard, bounds-checked cursor. |
| Path traversal on loose-override save | Tampering | `LooseOverridePath.Resolve` + `ResolvedRoot.Resolve` fail-closed containment. |
| Half-understood payload rewrite corrupts a file | Tampering | gate edits on a decoded/editable node; raw-fallback nodes are NON-editable (reject before write, cf `ApplySaveTrnCommand` `TargetTypedNodeIsEditable`). |
| Malformed/truncated CLEF crashes the editor | DoS | raw-fallback never hard-aborts (D-13); decode exceptions caught → exit 2 / red status, never a throw out of a MEF ctor. |

## Sources

### Primary (HIGH confidence)
- `D:/Code/swg-client-v2/.../clientEffect/ClientEffectTemplate.cpp` (load/version dispatch + per-command parse) — the codec spec.
- `D:/Code/swg-client-v2/.../clientEffect/ClientEffectTemplate.h` (command struct layouts).
- `D:/Code/swg-client-v2/.../clientEffect/ClientEffectTemplateRW.cpp` (SOE save path — the TAG_0003 normalization to AVOID).
- `D:/Code/swg-client-v2/.../sharedFile/Iff.cpp:1539-1646` + `Iff.h:518-656` (string NUL-term framing; LE scalar memcpy reads).
- `UtinniCoreDotNet/Formats/Iff/{IffReader,IffWriter,MutableIffDocument,MutableIffNode,IffDocument}.cs` (the verified length-ripple DOM).
- `UtinniCoreDotNet/Formats/Decoders/IffPayloadCursor.cs` (LE/cstring primitives).
- `UtinniCoreDotNet/Formats/Particle/{ParticleEffectDocument,MutableParticleEffect}.cs` (model/codec reuse template).
- `Utinni.Cli/Commands/{DecodeIffCommand,DecodeTrnCommand,ApplySaveTrnCommand,RoundtripParticleCommand}.cs` + `Program.cs` (verb dispatch reuse template).
- `Utinni.Mcp/Tools/ReadTools.cs:97-127` (MCP read-tool reuse template).
- `D:/Code/UtinniPlugins/.../UI/SubPanels/TerrainSubPanel.cs`, `.../UI/Forms/FormParticleEditor.cs:700-783`, `.../Saving/{TerrainSaveTargets,IffSaveTargets,ClientReloadDispatcher}.cs` (UI + preview + save reuse templates).
- `.planning/todos/pending/phase21-terrain-override-loose-subdir.md` (folded todo — verified stale against code).

### Secondary
- `.planning/ROADMAP.md` §Phase 22; `.planning/REQUIREMENTS.md` PROD-W2-CFX-01/02; `.planning/phases/22-clienteffect-editor/22-CONTEXT.md`.

### Tertiary
- (none — all claims grounded in source or CONTEXT.)

## Metadata

**Confidence breakdown:**
- CLEF format/codec spec: HIGH — read swg-client-v2 source line-by-line; field tables cite exact `.cpp` lines.
- Length-ripple mechanism (D-01): HIGH — verified in `MutableIffNode`/`IffWriter` source, not assumed.
- Reuse template mapping: HIGH — every named target read in full; file:line analogs pinned.
- Folded-todo reality: HIGH (code) / MEDIUM (the CLI-half subdir-convention gap A2 inferred, not test-traced).
- Real-lineage CLEF versions (D-14): LOW until extracted — A1/A3 flagged.

**Research date:** 2026-06-17
**Valid until:** 2026-07-17 (stable in-repo composition; refresh if the IFF DOM or Particle/Terrain templates change).
