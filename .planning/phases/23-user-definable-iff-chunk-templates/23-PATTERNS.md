# Phase 23: User-Definable IFF Chunk Templates - Pattern Map

**Mapped:** 2026-06-20
**Files analyzed:** 16 new/modified (7 engine, 3 CLI verbs, 1 MCP tool, 1 TJT pane mode, 5 test files)
**Analogs found:** 16 / 16 (every load-bearing mechanism has a verified in-repo analog — this is a composition phase, not greenfield)

> **Planner note:** CONTEXT.md / RESEARCH.md / UI-SPEC.md already cite most analogs with file:line.
> This map turns those citations into copy-from excerpts and adds the role/data-flow classification +
> the per-file analog selection. All analog line numbers below were re-verified this session.

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `UtinniCoreDotNet/Formats/Template/TemplateModel.cs` | model | transform | `Formats/ClientEffect/ClientEffectCommand.cs` (POCO model) + `MutableIffNode` field discipline | role-match |
| `UtinniCoreDotNet/Formats/Template/TemplateJson.cs` | utility (serializer) | transform | `ApplySaveIffCommand` `JObject`/`JValue` usage (Newtonsoft idiom) | partial (no whole-file analog; clone the `JObject` build idiom) |
| `UtinniCoreDotNet/Formats/Template/KernelCodec.cs` | service (codec) | transform / file-I/O | `Formats/ClientEffect/ClefFieldCodec.cs` (encode) + `Formats/Decoders/IffPayloadCursor.cs` (decode) | **exact** |
| `UtinniCoreDotNet/Formats/Template/FitReport.cs` | service (pure fn) | transform | `IffPayloadCursor.Need()`/`Remaining` consumed-exactly accounting | partial |
| `UtinniCoreDotNet/Formats/Template/TypePlausibility.cs` | utility (predicate lib) | transform | (no analog — Wave-0 net-new; see No Analog Found) | none |
| `UtinniCoreDotNet/Formats/Template/TemplateResolver.cs` | service (match index) | event-driven (resolve-on-leaf) | `MutableIffDocument.DeriveStableId` + `DecodeIffCommand` altitude dispatch | role-match |
| `UtinniCoreDotNet/Formats/Template/Presets/*.json` | config (data) | n/a | `ClefFieldCodec.EncodeClgt`/`EncodeCams` byte-order constants (D-09 layouts) | partial (data not code) |
| `Utinni.Cli/Commands/DecodeWithTemplateCommand.cs` (or `decode-iff --template` branch) | controller (verb) | request-response | `Utinni.Cli/Commands/DecodeTrnCommand.cs` (alias-delegation) | **exact** |
| `Utinni.Cli/Commands/RoundtripTemplateCommand.cs` | controller (verb) | request-response | `Utinni.Cli/Commands/RoundtripIffCommand.cs` / `ApplySaveIffCommand` verify path | role-match |
| `Utinni.Cli/Commands/ApplySaveTemplateCommand.cs` | controller (verb) | file-I/O / CRUD | `Utinni.Cli/Commands/ApplySaveIffCommand.cs` | **exact** |
| `Utinni.Cli/Program.cs` (modify: register verbs) | route | request-response | `Program.cs` `ParseArguments(Type[])` + `Dispatch` switch | **exact** (self) |
| `Utinni.Mcp/Tools/ReadTools.cs` (modify: add thin tool) | controller (MCP tool) | request-response (shell-out) | `ReadTools.SummarizeClientEffect` | **exact** |
| TJT `UI/Forms/FormIffEditor.cs` (modify: Tier-B pane mode) | component (WinForms) | event-driven | `FormIffEditor` hex/text mode (`txtHex`/`textModeActive`/`btnHexMode`) | **exact** (self-extension) |
| `Utinni.Cli.Tests/Template/TemplateTestFixtures.cs` | test (fixture) | transform | `Utinni.Cli.Tests/ClientEffect/ClefTestFixtures.cs` | **exact** |
| `Utinni.Cli.Tests/Template/KernelCodecTests.cs` + `RoundtripTemplateCommandTests.cs` + `ApplySaveTemplateTests.cs` | test | transform | existing `ClientEffect/*Tests.cs` + `ApplySaveIffCommand.TestPerturbSerialized` seam | role-match |
| `Utinni.Mcp.Tests/Template/...` | test | request-response | existing `Utinni.Mcp.Tests` MCP-tool shell-out tests | role-match |

---

## Pattern Assignments

### `Formats/Template/KernelCodec.cs` — ENCODE side (service, transform)

**Analog:** `UtinniCoreDotNet/Formats/ClientEffect/ClefFieldCodec.cs` (THE closest model — Phase 22 variable-length codec)

**License header + provenance-comment idiom** — every `Formats/*` file opens with the MIT block + a
"Format understood by reading swg-client-v2 … No code … copied … Implementation original to Utinni
under MIT" line. Clone `ClefFieldCodec.cs` lines 1-29 verbatim (swap the cited reference path for
`sharedFile/.../Iff.cpp` composite write order, RESEARCH §"Verified IFF composite write order").

**LE / cstring primitive writers** (`ClefFieldCodec.cs` lines 56-91) — copy these EXACTLY; they are
the kernel's host-endian-safe primitives (Pitfall 6 / Pitfall 2):
```csharp
public static void WriteCString(Stream s, string value) {
    if (value == null) value = string.Empty;
    byte[] bytes = StringEncoding.GetBytes(value);   // StringEncoding = Encoding.ASCII (line 54)
    s.Write(bytes, 0, bytes.Length);
    s.WriteByte(0x00);                               // NUL terminator, NO length prefix (Pitfall 2)
}
public static void WriteFloatLe(Stream s, float value) {
    byte[] four = BitConverter.GetBytes(value);
    if (!BitConverter.IsLittleEndian) Array.Reverse(four);   // host-endian guard (Pitfall 6)
    s.Write(four, 0, four.Length);
}
public static void WriteInt32Le(Stream s, int value) { /* same Array.Reverse guard */ }
public static void WriteUInt8(Stream s, byte value) { s.WriteByte(value); }
```
The kernel adds `WriteInt16Le` / `WriteUInt32Le` / `WriteDoubleLe` / `WriteFixedChar(n)` /
`WritePadding(n)` following the identical `BitConverter.GetBytes` + `Array.Reverse`-on-BE-host shape.

**Whole-payload `MemoryStream` build idiom** (`ClefFieldCodec.EncodeCpap` lines 132-148) — the kernel
encoder loops `template.Fields` writing into one `MemoryStream` then `ms.ToArray()`. The D-10
count-from-prior recompute is the ONE new branch inside this loop (RESEARCH Pattern 2):
```csharp
using (var ms = new MemoryStream()) {
    foreach (var f in template.Fields) {
        if (f.IsCountFieldFor(out var arrayField))
            WriteIntLe(ms, values[arrayField].Elements.Count, f.ByteWidth); // RECOMPUTE — never the stale value
        else if (f.IsArray) foreach (var el in values[f].Elements) EncodeStruct(ms, f.ElementType, el);
        else EncodeKernelField(ms, f, values[f]);
    }
    return ms.ToArray();
}
```

**Version-mismatch throw discipline** (`ClefFieldCodec.EncodeCpap` lines 118-130) — the encoder throws
a typed parse exception (`ClientEffectParseException(VersionFieldMismatch)`) rather than silently
emitting wrong bytes. The kernel's analog: throw a typed `TemplateException` when a count field
references an undefined prior field or a width overruns (UI-SPEC error copy lines 197-198).

---

### `Formats/Template/KernelCodec.cs` — DECODE side (service, transform)

**Analog:** `UtinniCoreDotNet/Formats/Decoders/IffPayloadCursor.cs` (the phase's dependency anchor — EXTEND it)

**Bounds-checked LE-scalar / CString / raw cursor** (`IffPayloadCursor.cs` lines 44-164) — the kernel
decoder reads through this exact cursor. Existing primitives to reuse as-is: `ReadInt32Le` (62-71),
`ReadUInt32Le`→long (78-87, the overflow-safe unsigned idiom), `ReadInt8` (94-100), `ReadBytes`
(103-110), `ReadFloatLe` (113-129), `ReadCString(Encoding)` (136-153).

**The `Need(int)` guard** (lines 155-163) is the V5 security control RESEARCH flags — every read
bounds-checks and throws `DecoderException(Truncated)` past end-of-payload. NEW kernel reads
(`ReadInt16Le`, `ReadUInt16Le`, `ReadDoubleLe`, `ReadFixedChar(n)`, padding skip) MUST call `Need(n)`
first, mirroring the existing methods verbatim:
```csharp
public int ReadInt32Le() {
    Need(4);                                          // <-- always first
    int v = _data[_pos] | (_data[_pos+1]<<8) | (_data[_pos+2]<<16) | (_data[_pos+3]<<24);
    _pos += 4; return v;
}
```
**Consumed-exactly accounting for FitReport:** `Remaining` (line 56) and `Length` (line 59) give the
"`N of M bytes consumed`" the D-07 indicator + FitReport need — after decode, `consumedExactly =
(cursor.Remaining == 0)`. Do NOT add a new position API; `Remaining` already exposes it.

> **Anti-pattern (RESEARCH):** do NOT model the encoder on `TrnFieldEncoder` (fixed-span, rejects
> length changes). `ClefFieldCodec` is the variable-length model; the whole point is variable-length edits.

---

### `Formats/Template/TemplateResolver.cs` (service, match index)

**Analog:** `MutableIffDocument.DeriveStableId` (lines 161-177) + `DecodeIffCommand` altitude dispatch (lines 92-117)

**Match key = the existing stable-id path** (D-04). `DeriveStableId` (MutableIffDocument.cs:161-177)
already produces `FORM:CLEF/0/FORM:0003/0/CPAP:CPAP/0` — ancestor-FORM-path + version-FORM + leaf tag.
The resolver's match key is a **prefix/predicate over that same string** — reuse `DeriveStableId`, do
NOT invent a new addressing scheme:
```csharp
// container: typeTrimmed + ":" + subTrimmed + "/" + ordinal   (e.g. "FORM:0003/0" carries the version-FORM)
// leaf:      typeTrimmed + ":" + typeTrimmed + "/" + ordinal   (e.g. "CPAP:CPAP/0")
```
Tag-only widening (D-04) = drop the ancestor prefix, keep the leaf-tag segment. Version-FORM stays in
the path (REQUIRED — CLEF CPAP 3-layout case, RESEARCH Anti-Patterns).

**Altitude gate (D-05) is structural, already exists** — `DecodeIffCommand.Run` (lines 92-117) dispatches
built-ins by root-FORM sub-type (`PEFT`/`TGEN`/`CLEF`, plus STF/UI sniffs at 69-80) BEFORE any
leaf logic. A template engages only at the `TryDecode` fallthrough (line 120) where no root-FORM
built-in claimed the file. The resolver sits at that altitude — it is the "no built-in → template
eligible" branch. Mirror the `(doc.Root as IffContainerChunk)?.SubTypeId == "XXXX"` branch idiom.

---

### `Utinni.Cli/Commands/DecodeWithTemplateCommand.cs` (controller verb, request-response)

**Analog:** `Utinni.Cli/Commands/DecodeTrnCommand.cs` (the alias-delegation precedent)

**Whole-file shape** (DecodeTrnCommand.cs lines 35-85) — copy verbatim, swap the codec call. The
`[Verb]` attr + `[Value(0)]` path option (35-40), the `File.Exists` → exit-3 guard (55-59), the
`try { decode; EmitSuccess } catch (TypedEx) { EmitError exit 2 } catch (IOException)` ladder (61-83),
and the load-bearing tail comment `// NOTE: Generic Exception intentionally NOT caught.` (83).

**Alias-delegation (D-15 discretion)** — DecodeTrnCommand delegates to the SAME builder the `decode-iff`
branch uses (`DecodeIffCommand.BuildTerrainResult`, line 65) so the two provably cannot drift. Follow
this: either a `decode-iff --template <path>` branch OR a `decode-with-template` alias that calls the
same template-result builder. Exit-code taxonomy: `0 ok / 2 parse|decode / 3 not-found` (lines 48-49).

---

### `Utinni.Cli/Commands/ApplySaveTemplateCommand.cs` (controller verb, file-I/O / CRUD)

**Analog:** `Utinni.Cli/Commands/ApplySaveIffCommand.cs` (THE member of the `apply-save-*` family to clone)

**Options + the `--root` containment idiom** (ApplySaveIffCommand.cs lines 38-55, 97-111):
```csharp
[Option("root", Required = true, ...)] public string Root { get; set; }      // line 44
// ...
try { destPath = LooseOverridePath.Resolve(o.Root, o.RelAsset); }            // line 100
catch (ArgumentException ex) { return JsonOutput.EmitError(verb, "PathContainment", ex.Message, exitCode: 2); }
if (!File.Exists(destPath)) return JsonOutput.EmitError(verb, "FileNotFound", ..., exitCode: 3);
```

**The full apply-save backbone** (lines 113-225) — clone this control flow exactly; the ONLY
difference is HOW `newPayload` is produced (RESEARCH Pattern 1):
```csharp
IffDocument originalDoc = IffReader.Read(ms);
MutableIffDocument mutable = MutableIffDocument.FromDocument(originalDoc, loadedBytes);  // line 128
MutableIffNode mutableLeaf = FindMutableLeafByStableId(mutable, o.MutateLeafId);        // line 146
byte[] newPayload = kernelCodec.Encode(template, editedFieldValues);   // <-- ONLY new step (vs ParseHex at 140)
mutableLeaf.SetPayload(newPayload);                                                     // line 152
byte[] mutatedBytes = IffWriter.Write(mutable);                                        // line 171 — leaf len + parent FORM ripple FREE
// re-parse-for-validity (178-190) → CompareUntouchedLeaves (192-198) → fail-closed exit 2
SaveCommandIo.WriteAtomic(destPath, mutatedBytes);                                      // line 200 — only on clean verify
```

**Test seam to copy** — `internal static Func<byte[],byte[]> TestPerturbSerialized;` (line 74,
invoked 173-176) lets `ApplySaveTemplateTests` corrupt the serialized bytes and assert the
fail-closed (exit 2, no write) path. Reuse this exact seam.

**Untouched-leaf verify** (`CompareUntouchedLeaves` lines 236-275) — reuse verbatim; a template edit
is a `--mutate-leaf`-equivalent pure payload edit, so the by-stable-id branch (239-256) applies.

---

### `Utinni.Cli/Program.cs` (route — MODIFY)

**Analog:** itself (lines 48-119). Adding 2-3 verbs is mechanical (RESEARCH: 29 verbs already, no arity cap):
1. Add each `*Options` `typeof(...)` to the `parser.ParseArguments(args, …)` list (lines 49-77).
2. Add a `case Commands.XxxOptions o: return Commands.XxxCommand.Run(o);` to `Dispatch` (lines 88-117).
No parser refactor — the `Type[]` overload + object-typed `MapResult` already scale.

---

### `Utinni.Mcp/Tools/ReadTools.cs` (controller MCP tool — MODIFY, add ONE thin tool)

**Analog:** `ReadTools.SummarizeClientEffect` (lines 129-144) — the most recent thin-shell read tool

**Copy the 4-line body verbatim** (zero format logic — MCP-OOP, D-16):
```csharp
[McpServerTool(Name = "summarize_with_template", ReadOnly = true, Idempotent = true)]
[Description("... ZERO format logic here — the named pipe / subprocess boundary to the x86 utinni-cli IS the boundary (MCP-OOP).")]
public static async Task<CallToolResult> SummarizeWithTemplate(ResolvedRoot root, CliDispatcher cli,
    [Description(PathParamDescription)] string relativePath) {
    string abs = root.Resolve(relativePath);                          // throws on escape → SDK tool error (V4 control)
    CliInvocationResult r = await cli.RunAsync("decode-with-template", new[] { abs }).ConfigureAwait(false);
    return CliResultMapper.ToCallToolResult(r);                       // verbatim envelope pass-through
}
```
`PathParamDescription` constant (line 57) + the `ResolvedRoot root, CliDispatcher cli` DI signature are
fixed by the tool-type contract — keep them identical.

---

### TJT `UI/Forms/FormIffEditor.cs` (component — MODIFY, add Tier-B pane mode)

**Analog:** the existing hex/text mode machinery in the SAME file (self-extension)

**Mode-toggle state field** (FormIffEditor.cs line 83): `private bool textModeActive;` — add a parallel
`templateModeActive` (or a 3-way enum). **Bound leaf field** (line 81): `private MutableIffNode
currentLeaf;` — the Tier-B pane reads `currentLeaf.GetPayloadCopy()` (MutableIffNode.cs:255) for live
decode and commits via `currentLeaf.SetPayload(kernelEncodedBytes)` (MutableIffNode.cs:279).

**Undo (DECIDED in UI-SPEC):** ride the existing `IffEditController controller` field (line 68); a
template value edit is the same `SetPayload` the controller already tracks via `ProcessCmdKey`
Ctrl+Z/Y/S (lines 59-62). Template-SHAPE edits (assign-type gestures) get a SEPARATE builder-local
stack — do NOT push them onto `IffEditController`.

**Provenance Save▾ matrix** (fields lines 91-96: `miSaveInPlace`/`miSaveLooseOverride`/`miSaveAs`/
`miPatchLive`/`miRepackTre` + `RefreshSaveMenuEnabledState`) — reuse UNCHANGED; a template-applied
edit is a normal dirty `MutableIffDocument`.

**Pitfall 8 (LOCKED, UI-SPEC + `[[feedback_winforms_dockfill_zorder]]`):** the new `pnlTemplate`
Dock.Fill child is added to `pnlLeafEditor.Controls` FIRST (front-most); the nested `splitTemplate`
SplitContainer sets `Size` BEFORE `SplitterDistance`; the ctor cannot throw (MEF silent-reject —
`[[feedback_caller_attrs_binary_compat]]`: no new defaulted `[Caller*]` params on public methods the
pre-built plugin DLLs call). Reuse `UtinniContextMenuStrip`, `FormSaveConfirmDialog`, `Colors.*()`,
`txtHex` Consolas-9pt + `HexDump` per UI-SPEC Reuse Manifest.

**Cross-repo:** this file lives in `D:/Code/UtinniPlugins` — standing write authority; paired commit,
no human checkpoint except the live-SWG smoke.

---

### `Utinni.Cli.Tests/Template/TemplateTestFixtures.cs` (test fixture, transform)

**Analog:** `Utinni.Cli.Tests/ClientEffect/ClefTestFixtures.cs` (THE synthesize-through-the-writer idiom)

**Compose-through-the-writer = canonical-by-construction** (ClefTestFixtures.cs lines 56-66) — clone
this exact builder shape, substituting kernel-encoded payloads for the `ClefFieldCodec` calls:
```csharp
MutableIffNode clef = MutableIffNode.NewContainer("FORM", "CLEF");   // root
MutableIffNode ver  = clef.AddContainer("FORM", version);           // version-FORM (D-04 axis)
ver.AddLeaf("CPAP", CpapPayload(version));                          // leaf w/ codec-encoded payload
return IffWriter.Write(new MutableIffDocument(clef));               // canonical bytes
```
For templates: build a FORM whose root sub-type is one Utinni does NOT decode (so the altitude gate
leaves the leaf template-eligible — RESEARCH A2), and `AddLeaf(tag, kernelCodec.Encode(template, vals))`.
The **dual-lineage axis is version-FORM divergence** (low/high version FORM), mirroring
`CpapPayload(version)` (lines 45-53) and the CLEF 3-layout exemplar — NOT encryption (RESEARCH
§Dual-lineage fixture matrix). The D-14 worked examples double as these goldens.

**CRITICAL golden** (RESEARCH Pitfall 1 + Validation): the count-from-prior array grow/shrink test —
decode a count-then-N chunk, add/remove an element, assert the count field's bytes updated AND the
whole file round-trips byte-exact. This is the ONE genuinely new encode obligation; everything else
(FORM length ripple) is free.

---

## Shared Patterns

### MIT header + "no code copied" provenance comment
**Source:** every `Formats/*.cs` (e.g. `ClefFieldCodec.cs` lines 1-29, `IffPayloadCursor.cs` lines 1-28)
**Apply to:** all 7 new `Formats/Template/*.cs` files. The provenance line cites which swg-client-v2
file grounded the layout understanding and asserts "No code … copied … original to Utinni under MIT."

### Byte-exact leaf re-emit + parent-FORM length ripple (DON'T hand-roll)
**Source:** `MutableIffNode.SetPayload` (line 279) → `MarkDirtyAndInvalidateAncestors` (484-495) → `IffWriter.Write`
**Apply to:** `ApplySaveTemplateCommand`, the TJT pane commit path, every round-trip test. The leaf
length re-stamp + bottom-up parent `innerLen` roll-up are FREE — the kernel produces ONLY leaf payload
bytes; never touch tag/length framing or the SWG no-pad quirk.

### `--root` containment + atomic write + fail-closed verify (V4/path security)
**Source:** `ApplySaveIffCommand` lines 100-104 (`LooseOverridePath.Resolve` → exit 2 on PathContainment),
line 200 (`SaveCommandIo.WriteAtomic`), lines 192-198 (untouched-leaf verify → fail-closed exit 2)
**Apply to:** `ApplySaveTemplateCommand`. MCP side: `ResolvedRoot.Resolve` (ReadTools.cs line 67) throws on escape.

### JSON envelope via Newtonsoft (no `TypeNameHandling`)
**Source:** `ApplySaveIffCommand` lines 31, 202-214 (`JObject`/`JValue.CreateNull()` build idiom)
**Apply to:** `TemplateJson.cs` + every verb's `EmitSuccess` result. Deserialize templates to a fixed
POCO model — do NOT enable `TypeNameHandling` (RESEARCH Security: hostile-template gadget mitigation).

### V5 input-validation: never allocate on attacker-controlled counts
**Source:** `IffPayloadCursor.Need()` (lines 155-163) + the class doc "never allocates based on
attacker-controlled counts" (lines 41-42); `IffWriter` 64 MB `MaxChunkSize` cap
**Apply to:** `KernelCodec` decode (bound array element counts, reject negative/overflowing widths) and
`TemplateModel` (a user-authored template is attacker-controllable input). A count-from-prior claiming
more elements than the payload holds must throw `Truncated` → surface as a FitReport "does not fit",
never over-read.

### Typed-exception → exit-code ladder + "Generic Exception NOT caught"
**Source:** `DecodeTrnCommand` lines 61-84; `DecodeIffCommand` lines 127-149
**Apply to:** all 3 new verbs. Catch each typed parse/decoder exception → `EmitError(verb, ex.Kind, …,
exitCode: 2)`; `FileNotFound` → exit 3; leave generic `Exception` uncaught (the load-bearing tail comment).

### Grep-gate hygiene (`[[feedback_gsd_grep_gate_hygiene]]`)
**Apply to:** all source comments in this token-heavy feature. Plan acceptance "grep X returns zero
matches" is literal — reword comments to avoid any gated SWG type token; keep historical names only in
non-gated docs.

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `Formats/Template/TypePlausibility.cs` | utility (predicate lib) | transform | No `looksLikeFloat`/`looksLikeCStringRun`/`looksLikeCount` heuristic library exists in-repo. Net-new (D-17.3) — the one small genuine addition to Tier B. Build against RESEARCH §D-09 layouts + `IffPayloadCursor` read primitives; no codec analog to copy. |
| `Formats/Template/TemplateModel.cs` (the JSON schema shape) | model | transform | The field-record schema (name/type/repeat-spec/enum-map/encoding + `version`) is net-new — no existing POCO matches. Ground the type vocabulary against RESEARCH §"D-08 Kernel ↔ SOE `.tdf` Vocabulary Map"; follow the `ClientEffectCommand` POCO discipline (immutable-ish, defensive copies) for style only. |
| `Formats/Template/Presets/*.json` | config (data) | n/a | Data files, not code. The EXACT byte layouts come from RESEARCH §"D-09 SWG Composite Byte Layouts (PINNED)" — vector (x,y,z 3×f32), quaternion (**w,x,y,z** w-first 4×f32), matrix (row-major 3×4, 12×f32), color (3 forms: PackedRgb 3×u8 / PackedArgb u32-ARGB / VectorArgb 4×f32). Do NOT guess — the table has file:line citations. |

**Planner guidance for no-analog files:** these are exactly where RESEARCH.md's pinned tables (D-08
vocabulary, D-09 byte layouts, D-11 enum/flags grammar) are the authority instead of a code analog.
TypePlausibility is the only logic with no precedent — keep it a standalone pure-predicate library
(D-17.3) so Tier C reuses it verbatim.

---

## Metadata

**Analog search scope:** `UtinniCoreDotNet/Formats/{Decoders,ClientEffect,Iff}`, `Utinni.Cli/Commands`,
`Utinni.Cli/Program.cs`, `Utinni.Mcp/Tools`, `Utinni.Cli.Tests/ClientEffect`,
`UtinniCoreDotNet/PluginFramework`, and the sibling `D:/Code/UtinniPlugins/.../UI/Forms/FormIffEditor.cs`.
**Files scanned:** 11 analog files read in full or in targeted ranges (all line numbers re-verified this session).
**Pattern extraction date:** 2026-06-20
