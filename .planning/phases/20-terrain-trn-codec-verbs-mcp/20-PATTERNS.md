# Phase 20: Terrain `.trn` Codec + Verbs + MCP - Pattern Map

**Mapped:** 2026-06-15
**Files analyzed:** 13 new + 2 modified
**Analogs found:** 15 / 15 (every new file has an exact or strong role-match analog — this phase is ~80% composition)

## Orientation

Phase 20 is a **pure-managed C# composition phase**: no new project, no native/bridge change, `Generated/UtinniCore.cs` untouched. New code lands in three places:
- `UtinniCoreDotNet/Formats/` — a new `Terrain/` model subdir + a `Decoders/TgenDecoder.cs` (mirrors `Particle/` + the existing `Decoders/`).
- `Utinni.Cli/Commands/` — three verbs + a `decode-iff` TGEN branch.
- `Utinni.Mcp/Tools/ReadTools.cs` — one thin read tool.

Every analog below is in the live repo (read this session). Reference-source porting (`swg-client-v2/sharedTerrain`) is **format-understanding only** — never copy code/identifiers (DEC-V2-LIFT-SHIFT). Note all existing files carry a literal "Implementation original to Utinni under MIT." / "no code/identifiers copied" header comment when they port understanding from the reference corpus — **replicate that header on every new decoder/model file** (see `IffPayloadCursor.cs:24-28`, `ObjectTemplateDecoder.cs:24-30`, `ParticleEffectDocument.cs:24-28`).

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `UtinniCoreDotNet/Formats/Decoders/TgenDecoder.cs` | decoder (dispatch + recurse) | transform (bytes→typed model) | `Formats/Decoders/ObjectTemplateDecoder.cs` | role + data-flow match (version dispatch, raw-fallback, `LooksLike*` sniff) |
| `UtinniCoreDotNet/Formats/Terrain/TerrainDocument.cs` | model (decode-result root + parse entry) | transform | `Formats/Particle/ParticleEffectDocument.cs` | exact (FromBytes/FromIff entry idiom) |
| `UtinniCoreDotNet/Formats/Terrain/TerrainLayer.cs` | model (immutable view) | — | `Formats/Decoders/ObjectTemplateDecoder.cs` `ObjectTemplateView` | role-match (immutable read view) |
| `UtinniCoreDotNet/Formats/Terrain/TerrainNode.cs` | model (typed + raw-fallback leaf) | — | `ObjectTemplateField` + Particle raw-preserve | role-match |
| `UtinniCoreDotNet/Formats/Terrain/TerrainPalettes.cs` | model (six palette DTOs) | — | `ObjectTemplateView` (DTO container) | role-match |
| `UtinniCoreDotNet/Formats/Terrain/TrnFieldEncoder.cs` | encoder (one DATA field re-pack) | transform (payload→payload) | `Formats/Decoders/IffPayloadCursor.cs` (read mirror) | data-flow match (LE field cursor) |
| `Utinni.Cli/Commands/DecodeTrnCommand.cs` (optional alias) | command (verb) | request-response | `Commands/DecodeIffCommand.cs` | exact |
| `Utinni.Cli/Commands/RoundtripTrnCommand.cs` | command (verb) | request-response | `Commands/RoundtripParticleCommand.cs` | exact |
| `Utinni.Cli/Commands/ApplySaveTrnCommand.cs` | command (verb, save) | file-I/O (contained write) | `Commands/ApplySaveIffCommand.cs` | exact (field-aware variant) |
| `Utinni.Cli/Commands/DecodeIffCommand.cs` | command (modify — add TGEN branch) | request-response | self (PEFT branch `:88-93`) | exact precedent in-file |
| `Utinni.Cli/Program.cs` | config (modify — register verbs) | — | self (`:43-74` Type[] + Dispatch) | exact precedent in-file |
| `Utinni.Mcp/Tools/ReadTools.cs` | command (modify — add `summarize_terrain`) | request-response (shell) | self (`SummarizeParticle` `:97-110`) | exact precedent in-file |
| `Utinni.Cli.Tests/Fixtures/trn/*.trn` + test classes | test (golden fixtures) | — | `Fixtures/iff/roundtrip/*` | role-match |

## Pattern Assignments

### `UtinniCoreDotNet/Formats/Decoders/TgenDecoder.cs` (decoder, transform)

**Analog:** `UtinniCoreDotNet/Formats/Decoders/ObjectTemplateDecoder.cs` (version-form dispatch + raw-fallback + `LooksLike*` sniff for the `decode-iff` dispatcher). Secondary: `ParticleEffectDocument.cs` for the compose-on-DOM entry.

**Header convention** — port-understanding files lead with a provenance comment, NOT just the MIT block (`ObjectTemplateDecoder.cs:24-30`):
```csharp
// Terrain (.trn / FORM TGEN) layout understood by reading swg-client-v2
// .../sharedTerrain/.../generator/TerrainGenerator.cpp + TerrainGeneratorLoader.cpp (SOE/Bootprint,
// All Rights Reserved): TGEN -> 0000 -> [6 palettes in fixed load order] -> LYRS? -> LAYR* ...
// Only the on-disk layout was studied — no code, comments, identifier names, or test fixtures copied
// from any reference source. Implementation original to Utinni under MIT.
```

**Namespace + class shape** (`ObjectTemplateDecoder.cs:36, 95`):
```csharp
namespace UtinniCoreDotNet.Formats.Decoders
{
    public static class TgenDecoder   // static Decode(IffDocument) + a LooksLikeTerrain(root) sniff
}
```

**`LooksLike*` sniff for the dispatcher** (copy shape from `ObjectTemplateDecoder.cs:103-113`) — TGEN's is trivial (root `SubTypeId == "TGEN"`), but provide the method so `DecodeIffCommand` stays symmetric:
```csharp
public static bool LooksLikeTerrain(IffChunk root)
{
    return (root as IffContainerChunk)?.SubTypeId == "TGEN";
}
```

**Version-first dispatch + raw-fallback** is the core pattern (RESEARCH Pitfall 5 + D-01/D-02). The decoder reads the FORM version, then reads EXACTLY that version's field list; an unrecognized version → raw-fallback `{tag, version, hex}`, NEVER throw (RESEARCH §Code Examples "Typed leaf decode with raw-fallback"):
```csharp
switch (tag) {
    case "AHCN": return DecodeHeightConstant(version, dataBytes);   // Tier-1 typed
    case "BCIR": return DecodeBoundaryCircle(version, dataBytes);
    // ... Tier-1 set (D-01) ...
    default:     return RawFallback(tag, version, dataBytes);       // never throw (criterion 2)
}
```

**Field reads go through `IffPayloadCursor`** (see `TrnFieldEncoder` below) — do NOT hand-roll endianness. Payload scalars are LITTLE-endian (`IffPayloadCursor.cs:25-27`), FORM/chunk framing is big-endian (already handled by `IffReader`).

**Palette decode is POSITIONAL, not by tag** (D-04 / Pitfall 4 — the two `MGRP` palettes collide). Decode the six palettes in fixed load order (shader, flora, radial, environment, fractal, bitmap), each optional (Pitfall 3). The `enterForm(TAG, true)` optional-form reality means a minimal TGEN may omit `LYRS` and every palette — treat absent → empty list (so ≤200-byte fixtures are legal).

**DEAD-tag set** (D-03 / Pitfall 2): `BALL`,`BSPL`,`AHSM`,`AHBM`,`ACBM`,`ASBM`,`AFBM` are recognized-and-skipped ("obsolete, ignored"), never editable, re-emitted verbatim via captured-slice. Do NOT raw-fallback them as editable nodes.

---

### `UtinniCoreDotNet/Formats/Terrain/TerrainDocument.cs` (model, transform)

**Analog:** `UtinniCoreDotNet/Formats/Particle/ParticleEffectDocument.cs` — the EXACT compose-on-DOM parse entry. Copy the `FromBytes` + `FromIff` dual-entry idiom verbatim (the `FromIff` overload is what `DecodeIffCommand` calls so it can pass the already-parsed doc + source bytes).

**Parse entry (`ParticleEffectDocument.cs:49-70`):**
```csharp
public static TerrainDocument FromBytes(byte[] bytes)
{
    if (bytes == null) throw new System.ArgumentNullException("bytes");
    IffDocument iffDoc;
    using (var ms = new MemoryStream(bytes, writable: false))
        iffDoc = IffReader.Read(ms);                 // tree-walk reader, no hand-rolled framing
    MutableIffDocument mutableIff = MutableIffDocument.FromDocument(iffDoc, bytes);
    return /* TgenDecoder over mutableIff */ ;
}

public static TerrainDocument FromIff(IffDocument iffDoc, byte[] sourceBytes) { /* same, no re-read */ }
```

Note the model holds the `MutableIffDocument` (captured-slice DOM) so the edit/save path (`apply-save-trn`, `roundtrip-trn`) re-emits clean nodes verbatim. Mirror how `MutableParticleEffect` wraps the mutable DOM.

---

### `UtinniCoreDotNet/Formats/Terrain/{TerrainLayer,TerrainNode,TerrainPalettes}.cs` (models)

**Analog:** the immutable-view DTOs in `ObjectTemplateDecoder.cs:38-81` (`ObjectTemplateField`, `ObjectTemplateView` — sealed, constructor-injected, get-only properties).

**Pattern (`ObjectTemplateDecoder.cs:67-81`):**
```csharp
public sealed class TerrainLayer
{
    public string Name { get; }
    public bool Active { get; }
    public IReadOnlyList<TerrainNode> Children { get; }   // boundaries/filters/affectors/sub-LAYR
    public TerrainLayer(string name, bool active, IReadOnlyList<TerrainNode> children) { ... }
}
```

`TerrainNode` carries `Tag`, `Version`, typed fields (Tier-1) OR raw bytes (Tier-2/unknown) — mirror the Particle raw-preserve disposition (a node is either typed or `IsRawPreserved` with `{tag,version,hex}`). `TerrainPalettes` is a DTO of six `familyId → name` lists; **never renumber familyIds on save** (D-04).

---

### `UtinniCoreDotNet/Formats/Terrain/TrnFieldEncoder.cs` (encoder, transform)

**Analog:** `UtinniCoreDotNet/Formats/Decoders/IffPayloadCursor.cs` — the bounds-checked LE field reader is the READ mirror; the encoder is its write inverse for ONE field. Re-uses the same LE convention and bounds discipline.

**Why this file exists (RESEARCH Pitfall 1 / D-09):** a TGEN `DATA` leaf packs 2-6 scalars (e.g. `AHCN` = `[int32 operation][float height]`). The generic `apply-save-iff --mutate-hex` replaces the WHOLE leaf payload by hex, pushing re-packing onto the caller. `TrnFieldEncoder` reads the tag's known layout, replaces ONE field by name, re-emits the whole packed payload.

**Read side — reuse `IffPayloadCursor` directly** (`IffPayloadCursor.cs:62, 113, 94`): `ReadInt32Le()`, `ReadFloatLe()`, `ReadInt8()`. **Write side — mirror the LE assembly** from `IffPayloadCursor.cs:113-129` (`ReadFloatLe`) inverted:
```csharp
// host-independent LE float write (inverse of IffPayloadCursor.ReadFloatLe:113-129)
byte[] four = BitConverter.GetBytes(value);
if (!BitConverter.IsLittleEndian) Array.Reverse(four);
Array.Copy(four, 0, payload, offset, 4);    // replace ONLY the target field
```

**Edit flow (RESEARCH §Code Examples "Byte-exact single-field edit"):**
```csharp
byte[] payload = dataLeaf.GetPayloadCopy();     // MutableIffNode.cs:255 — defensive copy
WriteFieldBE/LE(payload, offset, newValue);     // replace ONE field in place
dataLeaf.SetPayload(payload);                    // MutableIffNode.cs:279 — marks IsDirty, ancestors invalidate
```
`SetPayload` (`MutableIffNode.cs:279`) marks only that leaf dirty; `IffWriter` re-emits every clean node verbatim from its captured slice (`IffWriter.cs:103-110`) and recomputes ancestor FORM lengths — byte-exact is automatic for fixed-length edits (D-05). NO trailing pad byte (`IffWriter.cs:141`).

---

### `Utinni.Cli/Commands/DecodeTrnCommand.cs` (verb — optional alias) + the `decode-iff` TGEN branch

**Analog:** `Utinni.Cli/Commands/DecodeIffCommand.cs`. The REQUIRED change (D-08) is a TGEN branch in `DecodeIffCommand`; the standalone `decode-trn` is the cheap symmetry alias (Claude's Discretion — can delegate to the same `TgenDecoder` + `BuildTerrainResult`).

**Add the TGEN branch beside the PEFT precedent** (`DecodeIffCommand.cs:88-93`) — this is the verbatim template, add a TGEN twin:
```csharp
// existing PEFT branch — add a TGEN twin immediately after it
if ((doc.Root as IffContainerChunk)?.SubTypeId == "TGEN")
{
    return JsonOutput.EmitSuccess("decode-iff",
        BuildTerrainResult(TerrainDocument.FromIff(doc, bytes), o.Path));
}
```

**JSON envelope shape** — follow the particle `BuildParticleResult` convention (`DecodeIffCommand.cs:225-259`): anonymous object, **alphabetically-sorted keys**, a `type` discriminator (`type = "terrain"`), `rootType = "TGEN"`, `source`, `version`, plus counts (layerCount, paletteCounts, rawFallbackCount). The sorted-key + schemaVersion:1 envelope is emitted by `JsonOutput.EmitSuccess` (`JsonOutput.cs:50`).

**Exit-code + exception taxonomy** (`DecodeIffCommand.cs:54-120`): FileNotFound → exit 3; decoder/parse exceptions → exit 2; **generic `Exception` intentionally NOT caught** (bubbles for diagnosis — replicate this comment). Catch `IffParseException` + a new `TerrainParseException` (model after `ParticleParseException.cs`).

---

### `Utinni.Cli/Commands/RoundtripTrnCommand.cs` (verb)

**Analog:** `Utinni.Cli/Commands/RoundtripParticleCommand.cs` — copy the whole structure.

**Verb attribute + options (`RoundtripParticleCommand.cs:35-40`):**
```csharp
[Verb("roundtrip-trn", HelpText = "Parse -> serialize -> re-parse a terrain .trn (FORM TGEN); assert byte-exact identity on the whole file.")]
public class RoundtripTrnOptions { [Value(0, MetaName="path", Required=true, ...)] public string Path { get; set; } }
```

**Body (`RoundtripParticleCommand.cs:71-95`):** `ReadAllBytes` → `TerrainDocument.FromBytes` → `model.Serialize()` (via `IffWriter.Write` on the mutable DOM) → re-parse for validity → `loadedBytes.SequenceEqual(roundtripped)` → `JObject { bytesIdentical, comparisonGranularity="whole-file", rootType="TGEN", source, version, ... }`. Exit codes 0/1/2/3 exactly as the particle verb (`:57-113`); generic Exception NOT caught.

---

### `Utinni.Cli/Commands/ApplySaveTrnCommand.cs` (verb, file-I/O)

**Analog:** `Utinni.Cli/Commands/ApplySaveIffCommand.cs` — the `apply-save-*` family member. This is a **field-aware** variant (D-09): instead of `--mutate-leaf/--mutate-hex`, it takes `--leaf <stable-id> --field <name> --value <v>`.

**Options shape — keep `--root` + relAsset containment (`ApplySaveIffCommand.cs:41-44`):**
```csharp
[Value(0, MetaName="relAsset", Required=true, ...)] public string RelAsset { get; set; }
[Option("root", Required=true, ...)]  public string Root { get; set; }    // fail-closed containment
[Option("leaf", ...)]  public string LeafId { get; set; }                 // DeriveStableId path
[Option("field", ...)] public string Field { get; set; }
[Option("value", ...)] public string Value { get; set; }
```

**Path containment + atomic write (`ApplySaveIffCommand.cs:97-105, 200`):**
```csharp
destPath = LooseOverridePath.Resolve(o.Root, o.RelAsset);   // throws ArgumentException on escape → exit 2 PathContainment
// ... edit via TrnFieldEncoder + SetPayload ...
byte[] mutatedBytes = IffWriter.Write(mutable);
// re-parse for validity + verify untouched leaves byte-identical (CompareUntouchedLeaves :236-275)
SaveCommandIo.WriteAtomic(destPath, mutatedBytes);          // Utinni.Cli/Commands/SaveCommandIo.cs
```
`LooseOverridePath.Resolve` lives at `UtinniCoreDotNet/Saving/LooseOverridePath.cs` (also `UtinniCoreDotNet.PathContainment/LooseOverridePath.cs`); `SaveCommandIo.WriteAtomic` at `Utinni.Cli/Commands/SaveCommandIo.cs`. Reuse the leaf-lookup helpers `FindMutableLeafByStableId` / `FindMutableLeafRecursive` (`ApplySaveIffCommand.cs:288-307`) verbatim — they walk `MutableIffDocument.DeriveStableId` (`MutableIffDocument.cs:161-177`). Reuse the untouched-leaf byte-identity verify (`CompareUntouchedLeaves` `:236-275`) and the `TestPerturbSerialized` test seam (`:74`).

**Result JObject + exit codes (`ApplySaveIffCommand.cs:202-223, 69`):** `{ bytesEqualUntouched, bytesWritten, mutationApplied, path, validated, written }`; exits 0 ok / 1 usage / 2 verify|parse|path-containment / 3 file-not-found. Reject `.tre` magic up front (`IsTreMagic` `:227-230, 117-121`).

---

### `Utinni.Cli/Program.cs` (modify — register verbs)

**Analog:** self, `Program.cs:48-74` (`Type[]` ParseArguments) + `:78-107` (Dispatch switch). The 16-verb ceiling is already solved at 23 verbs (D-11) — adding `trn` verbs is two-line registration each:
```csharp
// in the ParseArguments Type[] (:48-71):
typeof(Commands.DecodeTrnOptions), typeof(Commands.RoundtripTrnOptions), typeof(Commands.ApplySaveTrnOptions),
// in Dispatch (:80-106):
case Commands.DecodeTrnOptions o:    return Commands.DecodeTrnCommand.Run(o);
case Commands.RoundtripTrnOptions o: return Commands.RoundtripTrnCommand.Run(o);
case Commands.ApplySaveTrnOptions o: return Commands.ApplySaveTrnCommand.Run(o);
```
**Wave-0 smoke (criterion 4 / D-11):** add a no-op `trn` verb first, confirm `--help` enumerates + parses, then build out.

---

### `Utinni.Mcp/Tools/ReadTools.cs` (modify — add `summarize_terrain`)

**Analog:** self, `SummarizeParticle` (`ReadTools.cs:97-110`) — copy verbatim, retarget to terrain. MCP-OOP lock (D-10): ZERO format logic, shells `decode-iff` (which now routes TGEN for free via the `DecodeIffCommand` branch).
```csharp
[McpServerTool(Name = "summarize_terrain", ReadOnly = true, Idempotent = true)]
[Description("Summarize a terrain .trn (FORM TGEN) — layer-tree + palette + raw-fallback counts — as the utinni-cli JSON envelope. Read-only; dispatches decode-iff, which auto-routes a FORM TGEN root.")]
public static async Task<CallToolResult> SummarizeTerrain(ResolvedRoot root, CliDispatcher cli,
    [Description(PathParamDescription)] string relativePath)
{
    string abs = root.Resolve(relativePath);                 // throws on escape → SDK tool error
    CliInvocationResult r = await cli.RunAsync("decode-iff", new[] { abs }).ConfigureAwait(false);
    return CliResultMapper.ToCallToolResult(r);              // verbatim envelope pass-through
}
```

---

### Test fixtures + golden tests

**Analog:** `Utinni.Cli.Tests/Fixtures/iff/roundtrip/*` (committed synthesized `.iff` + `.expected.json` pairs) + the `tre/synthesized-*` corpus. Mirror the directory convention with a new `Utinni.Cli.Tests/Fixtures/trn/`.

**Fixture synthesizer (D-12):** hand-emit ≤200-byte `TGEN` bytes via `IffWriter` (same primitive the codec saves through). Matrix: low-version ("SWGEmu-era") + high-version ("Restoration-era") per Tier-1 tag; minimal/no-palette TGEN; an unknown-tag (raw-fallback) case; a DEAD-tag (skip) case. Per RESEARCH §Wave 0: `TgenDecoderTests.cs`, `RoundtripTrnTests.cs`, `ApplySaveTrnTests.cs`, and `Utinni.Mcp.Tests/TerrainReadToolTests.cs`. Real per-lineage `.trn` pair (D-13/D-14) stays OUT of the committed corpus (large / v6000+ encrypted) — used only to pin versions + an extra roundtrip check.

## Shared Patterns

### IFF DOM compose (read + byte-exact edit)
**Source:** `UtinniCoreDotNet/Formats/Iff/{IffReader,MutableIffDocument,MutableIffNode,IffWriter}.cs`
**Apply to:** `TerrainDocument`, `TrnFieldEncoder`, `RoundtripTrnCommand`, `ApplySaveTrnCommand`
- Read: `IffReader.Read(stream)` → `MutableIffDocument.FromDocument(doc, bytes)`. Never hand-roll FORM/chunk framing (Don't Hand-Roll).
- Edit: `node.GetPayloadCopy()` (`MutableIffNode.cs:255`, defensive copy) → mutate → `node.SetPayload(bytes)` (`:279`, marks dirty + ancestor-invalidates).
- Save: `IffWriter.Write(mutable)` — clean nodes re-emit captured slice verbatim (`IffWriter.cs:103-110`), dirty nodes re-frame with NO trailing pad (`:141`), ancestors roll up lengths under `checked` arithmetic.

### Stable-id leaf addressing (no byte offsets)
**Source:** `MutableIffDocument.DeriveStableId` (`MutableIffDocument.cs:161-177`) + `ApplySaveIffCommand.FindMutableLeafRecursive` (`:288-307`)
**Apply to:** `ApplySaveTrnCommand --leaf`
- Hierarchical ordinal path (`FORM:TGEN/0/FORM:LYRS/.../DATA:DATA/0`) uniquely addresses the many identically-named `DATA` chunks. Reuse the recursive finder verbatim.

### LE payload field cursor (bounds-checked)
**Source:** `Formats/Decoders/IffPayloadCursor.cs` (`ReadInt32Le:62`, `ReadFloatLe:113`, `ReadInt8:94`, `ReadCString:136`, `Need:155`)
**Apply to:** `TgenDecoder` field reads, `TrnFieldEncoder` field re-pack
- Payload scalars are LITTLE-endian (`:25-27`); every read bounds-checks via `Need` → `DecoderException(Truncated)` rather than over-reading (Pitfall 5, V5 input validation). Inverse the `ReadFloatLe` byte-assembly for the write side.

### Verbs-first JSON envelope + exit-code taxonomy
**Source:** `Utinni.Cli/Output/JsonOutput.cs` (`EmitSuccess:50`, `EmitError:70`); applied across all `Decode*/Roundtrip*/ApplySave*` commands
**Apply to:** all three new verbs
- Success: `JsonOutput.EmitSuccess("<verb>", resultObject)` (sorted-key schemaVersion:1). Errors: `EmitError("<verb>", "<Kind>", msg, exitCode)`.
- Exit codes: 0 ok / 1 usage / 2 parse|verify|path-containment / 3 file-not-found. **Generic `Exception` intentionally NOT caught** (replicate the comment).

### Path containment + atomic save
**Source:** `LooseOverridePath.Resolve` (`UtinniCoreDotNet/Saving/LooseOverridePath.cs`) + `SaveCommandIo.WriteAtomic` (`Utinni.Cli/Commands/SaveCommandIo.cs`)
**Apply to:** `ApplySaveTrnCommand`
- Fail-closed `--root` containment (throws on escape → exit 2 PathContainment), atomic write only on a clean untouched-leaf verify (V12 file/path).

### MCP thin-shell (MCP-OOP)
**Source:** `Utinni.Mcp/Tools/ReadTools.cs:97-110` (`SummarizeParticle`)
**Apply to:** `summarize_terrain`
- `[McpServerTool(ReadOnly=true, Idempotent=true)]`, `root.Resolve(rel)` (throws on escape), `cli.RunAsync("decode-iff", ...)`, `CliResultMapper.ToCallToolResult(r)`. ZERO format logic in net10.

### Port-understanding provenance header
**Source:** `IffPayloadCursor.cs:24-28`, `ObjectTemplateDecoder.cs:24-30`, `ParticleEffectDocument.cs:24-28`
**Apply to:** `TgenDecoder.cs`, all `Terrain/*.cs`
- Every file that ports format understanding from `swg-client-v2` leads with a "layout understood by reading … no code/identifiers copied … Implementation original to Utinni under MIT." comment (DEC-V2-LIFT-SHIFT discipline).

## No Analog Found

None. Every new file maps to an exact or strong role-match analog in the live repo. The only genuinely-new logic (per RESEARCH §"Key insight") is internal to two files:
- `TgenDecoder.cs` — the `TGEN→LYRS→LAYR` recursion + per-tag Tier-1 field readers (no existing recursive layer-tree decoder; closest structural precedent is `ObjectTemplateDecoder`'s version-form walk).
- `TrnFieldEncoder.cs` — the multi-field `DATA` re-pack (no existing single-field-in-packed-payload encoder; `IffPayloadCursor` is the read-side mirror to invert).

Both compose existing primitives; neither needs a new project or a RESEARCH.md fallback pattern.

## Metadata

**Analog search scope:** `Utinni.Cli/Commands/`, `Utinni.Cli/Output/`, `Utinni.Mcp/Tools/`, `UtinniCoreDotNet/Formats/{Iff,Decoders,Particle}/`, `UtinniCoreDotNet/Saving/`, `Utinni.Cli.Tests/Fixtures/`.
**Files scanned (read this session):** DecodeIffCommand, ApplySaveIffCommand, RoundtripParticleCommand, Program, ReadTools, MutableIffDocument, MutableIffNode (grep), IffWriter, IffPayloadCursor, ObjectTemplateDecoder, ParticleEffectDocument + directory listings of Particle/Decoders/Saving/Fixtures.
**Pattern extraction date:** 2026-06-15
