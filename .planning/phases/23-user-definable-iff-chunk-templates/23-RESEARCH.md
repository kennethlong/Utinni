# Phase 23: User-Definable IFF Chunk Templates - Research

**Researched:** 2026-06-20
**Domain:** Schema-driven user-definable IFF leaf-chunk codec (pure-managed net4.7.2 + net10 MCP) on Utinni's existing IFF byte-exact DOM
**Confidence:** HIGH (the make-or-break encode-parity mechanism is verified against live code; D-09 composite layouts pinned to swg-client-v2 source with file+line citations)

## Summary

This phase is a composition exercise, not a greenfield codec. Every load-bearing mechanism already
exists in Utinni's tree and was verified this session: the byte-exact re-emit engine
(`MutableIffNode`/`IffWriter` captured-slice DOM), the variable-length LE-scalar + NUL-C-string codec
precedent (`ClefFieldCodec`, Phase 22), the verbs-first + `apply-save-*` + thin-MCP machinery
(29 verbs already past the old 16-arity ceiling via the `Type[]` overload), and the `FormIffEditor`
host with its hex leaf pane + `IffEditController` undo + provenance-gated Save▾ matrix. The new
engine is a **kernel codec + JSON template (de)serializer + fit-check** that compiles into the
headless `UtinniCoreDotNet/Formats/` (the load-bearing D-17.1 decision — no WinForms dependency), a
verb family in `utinni-cli`, a thin MCP read tool, and a Tier-B hex-driven authoring pane in TJT.

**The single make-or-break mechanism (verified):** byte-exact re-encode of a *length-changing* edit
already works at the IFF framing level. `apply-save-iff --mutate-leaf` replaces a whole leaf payload
with arbitrary-length bytes today; `MutableIffNode.SetPayload` marks the leaf dirty + invalidates
ancestors, and `IffWriter.WriteLeafFresh`/`WriteContainerFresh` re-stamps the leaf length AND rolls
up every parent FORM `innerLen` bottom-up under checked arithmetic. So the **only** new encode-parity
obligation that lives in *this* phase's engine is the D-10 count-from-prior-field auto-recompute
*inside the payload bytes* (the template encoder recomputes the count field before serializing the
array). The IFF-level length ripple is free; the in-payload count recompute is the part the plan must
build and golden-test.

**Primary recommendation:** Build a `UtinniCoreDotNet/Formats/Template/` engine = (1) a JSON template
model with a `version` field (D-13), (2) a kernel codec that decode→edit→encode a leaf payload
byte-exact (extend the `IffPayloadCursor` LE/CString primitives on read; mirror `ClefFieldCodec`'s
`MemoryStream` + LE/CString writers on encode), (3) a `FitReport` pure function (D-17.2). Wire it to
3 verbs (`decode-with-template` as a `decode-iff --template` branch + `roundtrip-template` +
`apply-save-template`) and 1 thin MCP read tool. Author through a new pane mode in `FormIffEditor`'s
`pnlLeafEditor` that calls the SAME engine for live decode + continuous round-trip indication.

## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** A template is a portable JSON file = canonical source of truth (diffable/shareable/
  version-controllable); carries its own `version` field (D-13). A GUI is a *view over* the file.
- **D-02:** Primary authoring = **Tier B in-place hex-driven builder** inside `FormIffEditor`. Select
  byte range → right-click → assign type+name → template grows, decoded preview updates live, a
  byte-exact round-trip check runs continuously, un-annotated bytes stay visibly raw. Emits the D-01 JSON.
- **D-03 (fallback only):** Tier-A grid field-builder is the scope-bite fallback if Tier B is too
  large — NOT a parallel deliverable. Do not build both; Tier B is the target.
- **D-04:** Match key = **ancestor-FORM-path + leaf tag**, auto-captured at Tier-B authoring time,
  author-widenable to tag-only. **Version-FORM awareness is REQUIRED** (CLEF `CPAP` has 3 byte layouts
  across version FORMs `0001/0002/0003`).
- **D-05:** Built-ins win; templates fill otherwise-hex leaves. Falls out of the altitude difference
  (built-ins claim a whole *file format* at root-FORM; a template describes a single *leaf payload*).
- **D-06 (forward-compat):** Template decode/encode contract mirrors the built-in decoder interface so
  "built-ins win" is a precedence ordering, not an architecture wall. Build the ordering; do NOT build
  the override path (deferred).
- **D-07:** The round-trip check doubles as a match-fit confidence signal (consumes-payload-exactly +
  plausible values → green, auto-applied silently; key-match but no round-trip → show + flag). Not
  inference. Multi-match ties → most-specific key wins; genuine tie → small picker.
- **D-08:** Two-layer type system. KERNEL (the only thing the engine decodes/encodes): sized ints
  (signed/unsigned, **LE default**), `f32`/`f64`, **NUL-terminated C-string + fixed `char[n]`** (never
  length-prefixed; encoding attribute, ASCII default), raw bytes + **explicit padding**, **struct**,
  **array**. PRESETS (`color`/`vector`/`quaternion`/`matrix`/`stringId`) are pure sugar = pre-built
  structs over the kernel. Byte-exactness lives ENTIRELY in the kernel.
- **D-09 (flagged research):** Starter presets need EXACT SWG byte layouts from swg-client-v2, not
  guessed. SWG uses multiple conventions → presets are editable data. **(PINNED below.)**
- **D-10:** Ship all three array kinds: (1) fixed-count, (2) **count-from-prior-field — encoder
  AUTO-RECOMPUTES the count field on write** (the core encode-parity mechanism), (3) until-end/
  trailing-remainder. Sentinel-terminated arrays = planner discretion / defer.
- **D-11:** Enum/bitfield display sugar IS in — optional named-value-map attribute on an int field,
  rendering `active(0)` / `walk|run`. Mirrors the SOE DataTable `e(a=0,b=1)` / `v(walk=1,run=2)` convention.
- **D-12:** Templates persist across a **scanned list of dirs** (shipped + app-data + project-local
  pack); share = clone a pack. Dirs auto-scanned; UI offers import/export.
- **D-13:** A template is a single self-contained JSON file carrying its own format `version` field.
- **D-14:** Ship presets + a couple of worked example chunk templates; the examples double as golden
  byte-exact fixtures AND teaching artifacts.
- **D-15:** Verbs-first for the engine (a `utinni-cli` template verb family) + golden byte-exact
  round-trip gate (DEC-C3) + a thin MCP read tool that shells `utinni-cli` (zero format logic in `Utinni.Mcp`).
- **D-16:** Interactive hex-authoring (Tier B) stays UI-only — a legitimate non-exception to
  verbs-first (an interaction, not a batch capability). The decode/encode/validate/roundtrip
  capabilities are all verbs.
- **D-17:** Tier-C readiness guardrails baked into HOW B is built (zero speculative cost): (1) engine
  headless in `Formats/` [load-bearing], (2) fit-check as a pure function `(template, payloadBytes) →
  FitReport`, (3) type-plausibility predicates as a standalone reusable library, (4) match key as a
  corpus query.

### Claude's Discretion

- Exact `utinni-cli` template verb names + flag shapes (D-15); whether `decode-with-template` is
  standalone or a `decode-iff --template` branch (follow the `DecodeTrnCommand`/`decode-iff`
  alias-delegation precedent).
- JSON envelope shape for decode output + the JSON template schema itself (field record:
  name/type/repeat-spec/optional enum-map/encoding); include the D-13 `version` field.
- Multi-match tie-break UI (D-07); interior Tier-B builder controls (honor Pitfall 8 — Dock.Fill
  front-most / nested `SplitContainer`, size before splitter distance; MEF-safe `IEditorPlugin` ctor).
- Whether template authoring rides the existing `IffEditController` undo stack or gets its own; the
  exact app-data + project-local dir paths and scan order (D-12).
- Sentinel-terminated array support (D-10) if cheap once the three locked kinds exist.

### Deferred Ideas (OUT OF SCOPE)

- **Tier C corpus inference** (own future phase). Tier B lays its substrate only; it does NOT build
  the inference pass, the constant-vs-varying differ, the array-boundary detector, or an
  `infer-template` verb.
- **Templates overriding built-in codecs** (deliberately shadowing CLEF/datatable/OT/terrain) —
  architecture left open via D-06; the override path + precedence UI deferred.
- **Sentinel-terminated arrays** (read-until-a-marker-value) — planner discretion / defer; the three
  locked array kinds cover the real SWG chunk shapes.
- Standalone renderer / any non-IFF concern (DEC-A3); the Tier-A grid-form builder (scope-bite
  fallback to Tier B only).

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PROD-IFFT-01 | Describe an arbitrary IFF chunk's binary layout (primitives, colors, vectors, quaternions, matrices, arrays, structs) as a named, reusable template. | D-08 kernel grounded against the SOE `.tdf` vocabulary (TemplateData.cpp); D-09 composite layouts PINNED below; D-10 three array kinds. JSON model = D-01/D-13. |
| PROD-IFFT-02 | Auto-apply a matching template to decode/display an otherwise-hex chunk and re-encode edits byte-exact (round-trip verified — the encode-parity risk). | Encode-parity mechanism VERIFIED (`MutableIffNode.SetPayload` + `IffWriter` length ripple); the ONLY new obligation is the D-10 in-payload count recompute. D-04 match key (version-FORM aware); D-05 altitude precedence. |
| PROD-IFFT-03 | Templates manageable (create/edit/save/select) from the IFF Editor UI. | Tier-B pane attaches to `FormIffEditor.pnlLeafEditor`; D-12 scanned-dirs storage; D-15/D-16 verbs + UI-only authoring. |
</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Template JSON (de)serialize | `UtinniCoreDotNet/Formats/Template` (headless net4.7.2) | — | D-17.1 load-bearing: engine must be UI-free for verbs + future Tier C. |
| Kernel decode/encode (byte-exact) | `Formats/Template` codec | extends `IffPayloadCursor` (read) / mirrors `ClefFieldCodec` (write) | Byte-exactness lives entirely in the kernel (D-08). |
| Fit-check `(template, bytes)→FitReport` | `Formats/Template` pure fn | — | D-17.2; reused by both Tier-B live indicator and (future) Tier-C scoring. |
| Match index (ancestor-path + tag) | `Formats/Template` resolver over `IffReader` tree | — | D-04; D-17.4 corpus-query substrate. |
| `template-*` verbs + golden gate | `Utinni.Cli/Commands` | shells engine | D-15 verbs-first / DEC-V2-VERBS-FIRST. |
| Thin MCP read tool | `Utinni.Mcp/Tools` (net10, OOP) | shells `utinni-cli` | D-16 / DEC-V2-MCP-OOP; zero format logic. |
| Tier-B hex authoring pane | `TheJawaToolboxDotNet/UI` (sibling repo) | calls headless engine + `MutableIffNode` | D-02 / DEC-C4 (editor inside TJT). |
| Loose-override save flow | existing `IffSaveTargets`/`LooseOverridePath` | — | Template-applied edit re-uses the proven save matrix. |

## Standard Stack

### Core (all already present — NO new packages)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `UtinniCoreDotNet.Formats.Iff` (`MutableIffNode`/`IffWriter`/`IffReader`) | in-repo | Captured-slice byte-exact DOM; the length-ripple engine | Already proven across Phases 8/11/15/20/22 for byte-exact leaf edits. [VERIFIED: codebase] |
| `UtinniCoreDotNet.Formats.Decoders.IffPayloadCursor` | in-repo | LE scalars / NUL-CString / raw bytes, bounds-checked | The phase's dependency anchor; extend with kernel primitives. [VERIFIED: codebase] |
| `Newtonsoft.Json` (`Newtonsoft.Json.Linq`) | already referenced by `Utinni.Cli` | Template JSON (de)serialize + decode envelope | Already the project's JSON lib (`ApplySaveIffCommand` uses `JObject`/`JValue`). [VERIFIED: codebase, `using Newtonsoft.Json.Linq` in ApplySaveIffCommand.cs] |
| `CommandLine` (CommandLineParser) | already referenced | Verb registration (`Type[]` overload, no arity cap) | 29 verbs registered today via `parser.ParseArguments(args, typeof(...)...)`. [VERIFIED: codebase, Program.cs] |
| `ModelContextProtocol` (`McpServerTool`) | net10, already referenced | Thin MCP read tool | The `ReadTools.cs` `summarize_*` shell-out pattern. [VERIFIED: codebase] |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `UtinniCoreDotNet.Formats.ClientEffect.ClefFieldCodec` | in-repo | Reference codec: variable-length LE + NUL-C-string encode | Model the kernel encoder on its `MemoryStream` + `WriteFloatLe`/`WriteInt32Le`/`WriteCString`/`WriteUInt8` writers. [VERIFIED: codebase] |
| `UtinniCoreDotNet.Saving` (`LooseOverridePath`, `IffSaveTargets`) | in-repo | `--root`-contained atomic loose-override save | The `apply-save-template` write path. [VERIFIED: codebase] |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| JSON template model | Custom C-like text DSL (010/ImHex/.tdf style) | Rejected by CONTEXT D-01 — bespoke parser is heavy for a quick win; JSON is the project idiom and maps 1:1 onto the field==byte-range model. |
| Newtonsoft.Json | `System.Text.Json` | Newtonsoft is already the project's JSON dependency and is net472-compatible (STJ on net472 needs an added package). Stay on Newtonsoft. |
| Fixed-span field encoder | `TrnFieldEncoder` (Phase 20) | WRONG tool — it rejects length changes; the whole point of a template edit is variable-length. Use the `ClefFieldCodec` variable-length model. [VERIFIED: 20-CONTEXT contrast] |

**Installation:** None. No new external packages. (D-domain explicitly: "No new external packages;
no native/bridge dependency.")

## Package Legitimacy Audit

This phase installs **zero external packages** — all dependencies are in-repo or already-referenced
project libraries (Newtonsoft.Json, CommandLineParser, ModelContextProtocol). The Package Legitimacy
Gate is therefore N/A. No `npm`/`pip`/`cargo`/NuGet install step is introduced. Confirm during
planning that no task adds a NuGet reference; if one is proposed (it should not be), gate it behind a
`checkpoint:human-verify`.

## Architecture Patterns

### System Architecture Diagram

```
                          ┌─────────────────────────── TEMPLATE PACKS (D-12) ───────────────────────────┐
                          │  shipped/ (built-in presets + worked examples)                              │
                          │  %APPDATA%/Utinni/templates/ (per-user)                                     │
   scan (load-order)  ◄───┤  <project>/templates/ (git-versioned shareable pack)                        │
                          └─────────────────────────────────────────────────────────────────────────────┘
                                    │  JSON template files (D-01/D-13: version + match-key + field records)
                                    ▼
  raw .iff bytes ──► IffReader ──► IFF tree ──► [D-05 altitude gate]
                                                   │
                          root-FORM has built-in?  ├─ YES ─► built-in codec claims whole file (templates never engage)
                                                   │
                                                   └─ NO ──► per leaf chunk: TemplateResolver
                                                               │  match key = (ancestor-FORM-path + tag),
                                                               │  version-FORM aware (D-04)
                                                               ▼
                                            ┌──────────  KERNEL CODEC + FIT-CHECK (headless, D-17.1)  ──────────┐
                                            │  decode: IffPayloadCursor-extended → typed field values          │
                                            │  FitReport (D-17.2): consumedExactly + perFieldPlausibility[]     │
                                            │  encode: MemoryStream LE/CString writers (ClefFieldCodec model)   │
                                            │          + D-10 count-from-prior AUTO-RECOMPUTE before array      │
                                            └──────────────────────────────────────────────────────────────────┘
                                                               │                         │
                          ┌────────────────────────────────────┘                         │
                          ▼  (read path)                                                  ▼  (edit path)
            decode envelope JSON                                          MutableIffNode.SetPayload(newPayloadBytes)
                 │       │                                                              │
        utinni-cli       MCP thin tool                                       IffWriter.Write  ──► leaf len re-stamp
   (decode-with-template) (shell-out)                                          + parent FORM innerLen ripple (free)
                                                                                       │
                                                                            LooseOverridePath atomic write
   ╔═══════════════════════ TIER-B HEX AUTHORING (TJT, UI-only D-16) ═══════════════════════╗
   ║ FormIffEditor.pnlLeafEditor: hex pane ──select byte range──► assign type+name           ║
   ║   ──► template grows ──► live decode preview ──► continuous FitReport indicator (green)  ║
   ║   un-annotated bytes stay raw ──► emits the SAME D-01 JSON ──► saves into a pack (D-12)  ║
   ╚════════════════════════════════════════════════════════════════════════════════════════╝
```

### Recommended Project Structure

```
UtinniCoreDotNet/Formats/Template/        # NEW — headless engine (D-17.1); Generated/UtinniCore.cs UNTOUCHED
├── TemplateModel.cs                       # JSON-backed: version, matchKey, FieldRecord[] (name/type/repeat/enum-map/encoding)
├── TemplateJson.cs                        # Newtonsoft (de)serialize (sorted-key for diffability)
├── KernelCodec.cs                         # decode (cursor) + encode (MemoryStream); count auto-recompute lives here
├── FitReport.cs                           # pure (template, payloadBytes) → { consumedExactly, perFieldPlausibility[] }
├── TypePlausibility.cs                    # looksLikeFloat / looksLikeCStringRun / looksLikeCount (D-17.3 standalone lib)
├── TemplateResolver.cs                    # match-key index over an IffReader tree (D-04 / D-17.4 corpus-query substrate)
└── Presets/                               # vector/quaternion/color/matrix/stringId — JSON sugar, NOT engine code (D-08)

Utinni.Cli/Commands/
├── DecodeWithTemplateCommand.cs (or decode-iff --template branch)   # D-15 + alias-delegation precedent
├── RoundtripTemplateCommand.cs                                       # golden byte-exact gate (DEC-C3)
└── ApplySaveTemplateCommand.cs                                       # --root-contained atomic write

Utinni.Mcp/Tools/ReadTools.cs            # + one thin summarize_with_template tool (shell-out, zero logic)

# sibling repo D:/Code/UtinniPlugins
The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs   # + Tier-B pane mode in pnlLeafEditor
```

### Pattern 1: Length-changing leaf edit (the encode-parity backbone) — ALREADY EXISTS

**What:** A whole-payload replacement of arbitrary length re-stamps the leaf length and ripples every
parent FORM length automatically. The template encoder produces new payload bytes; the IFF DOM does the rest.
**When to use:** Every template-driven edit (array grow/shrink, string length change).
**Example (verified from `ApplySaveIffCommand` + `IffWriter`):**

```csharp
// Source: ApplySaveIffCommand.cs (verified) — the template edit path clones this exactly,
// only differing in HOW newPayload is produced (kernel encoder vs --mutate-hex literal bytes).
MutableIffNode mutableLeaf = FindMutableLeafByStableId(mutable, leafId);
byte[] newPayload = kernelCodec.Encode(template, editedFieldValues);  // <-- the only new step
mutableLeaf.SetPayload(newPayload);            // marks leaf dirty + invalidates ancestors
byte[] mutatedBytes = IffWriter.Write(mutable); // WriteLeafFresh re-stamps len;
                                                // WriteContainerFresh rolls up parent innerLen (checked)
```

### Pattern 2: D-10 count-from-prior-field auto-recompute (the ONLY genuinely new encode obligation)

**What:** When a field is declared `array { count = <named prior field> }`, the encoder must write the
*current element count* into the named prior field's bytes BEFORE/as it serializes the array — never
trust the count value the user typed. This is the documented gap that makes Kaitai not byte-safe.
**When to use:** count-then-N-records chunks (the dominant SWG variable-array shape).
**Example (model on `ClefFieldCodec.EncodeCpap`'s sequential `MemoryStream` writes):**

```csharp
// Source: pattern derived from ClefFieldCodec.cs (verified writer idiom)
// Field order in the template is authoritative. When emitting the count field that
// an array's repeat-spec references, write elements.Count, not the decoded-then-stale value.
using (var ms = new MemoryStream()) {
    foreach (var f in template.Fields) {
        if (f.IsCountFieldFor(out var arrayField))
            WriteIntLe(ms, values[arrayField].Elements.Count, f.ByteWidth); // RECOMPUTE
        else if (f.IsArray)
            foreach (var el in values[f].Elements) EncodeStruct(ms, f.ElementType, el);
        else
            EncodeKernelField(ms, f, values[f]);
    }
    return ms.ToArray();
}
```

### Pattern 3: Verb alias-delegation (D-15 discretion)

**What:** A `decode-trn`-style alias delegates to the same builder as the `decode-iff` branch so the
two provably cannot drift. Recommend `decode-with-template` follow this (or be a `decode-iff
--template <path>` branch).
**Example:** `DecodeTrnCommand.Run` → `TerrainDocument.FromBytes` + `DecodeIffCommand.BuildTerrainResult`. [VERIFIED: codebase]

### Anti-Patterns to Avoid

- **Re-implementing IFF framing in the kernel.** `IffWriter` already omits the SWG pad and rolls up
  FORM lengths. The kernel codec produces ONLY the leaf payload bytes; never touch tag/length framing.
- **Fixed-span field encoder for variable-length edits.** `TrnFieldEncoder` rejects length changes —
  the wrong tool. Use the `ClefFieldCodec` variable-length `MemoryStream` model.
- **Length-prefixed strings.** SWG strings are NUL-terminated C-strings, `strlen+1` on disk, NEVER
  length-prefixed (Pitfall 2). [VERIFIED: ClefFieldCodec + Iff.cpp `insertChunkString` = `istrlen(string)+1`]
- **Tag-only matching where version-FORMs diverge.** CLEF CPAP has 3 byte layouts across version FORMs
  0001/0002/0003 — tag-only matching silently mis-decodes and breaks byte-exact (D-04 makes
  version-FORM awareness REQUIRED). [VERIFIED: ClefFieldCodec.EncodeCpap]
- **Welding the engine to WinForms.** That is the one thing that forecloses Tier C (D-17.1). Engine in
  `Formats/`, UI in TJT only.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Byte-exact leaf re-emit + parent length ripple | A custom serializer | `MutableIffNode.SetPayload` + `IffWriter.Write` | Already does checked bottom-up FORM roll-up + verbatim re-emit of untouched nodes. [VERIFIED] |
| Bounds-checked LE scalar / CString read | A new cursor | extend `IffPayloadCursor` | Already LE-default, never allocates on attacker counts, throws `Truncated`. [VERIFIED] |
| Variable-length payload encode | Hand-rolled byte math | mirror `ClefFieldCodec` writers | Proven LE/CString/uint8 writers with host-endian guards. [VERIFIED] |
| `--root`-contained atomic write | New save plumbing | `LooseOverridePath.Resolve` + `SaveCommandIo.WriteAtomic` | Path-containment + atomic commit + fail-closed verify already proven. [VERIFIED] |
| JSON envelope + parsing | New serializer | `Newtonsoft.Json.Linq` (`JObject`) | Already the project's JSON lib on net472. [VERIFIED] |
| Verb dispatch past 16 verbs | Refactor parser | `Type[]` `ParseArguments` overload | Already in `Program.cs` with 29 verbs. [VERIFIED] |

**Key insight:** The only genuinely new code is (a) the JSON template model, (b) the kernel
decode/encode with the D-10 count recompute, (c) the fit-check + plausibility predicates, and (d) the
Tier-B pane. Everything that touches *bytes on disk* is reuse.

## Runtime State Inventory

This is a **greenfield additive feature**, not a rename/refactor/migration. No stored data, live
service config, OS-registered state, secrets/env vars, or build artifacts carry a renamed string.
The one new on-disk surface is the template-pack directories (D-12), which are created fresh — there
is no pre-existing state to migrate. **Section otherwise omitted (verified: phase introduces new
files only; no string-rename or data-migration obligations).**

## Common Pitfalls

### Pitfall 1: Treating the IFF-level length ripple as the encode-parity risk

**What goes wrong:** Planning effort spent re-proving that growing a leaf re-stamps parent FORM lengths.
**Why it happens:** "Byte-exact re-encode of length-changing edits" sounds like the hard part.
**How to avoid:** It is already done and verified (`apply-save-iff --mutate-leaf` ships today). The
real risk is the **in-payload count-field recompute** (D-10 kind 2). Golden-test THAT: decode a
count-then-N chunk, add/remove an element, confirm the count field's bytes update and the whole file
round-trips byte-exact across both lineages.
**Warning signs:** A plan task that says "verify FORM length re-stamp" — that's free; redirect to the count recompute.

### Pitfall 2: stringId preset has no single on-wire form

**What goes wrong:** Shipping a `stringId` preset as one fixed struct; some chunks store it as two
NUL C-strings (table + text), others differently.
**Why it happens:** `StringId` is logically a `table:text` pair (`getCanonicalRepresentation = table + ":" + text`),
but its IFF serialization is consumer-specific. [VERIFIED: StringId.cpp + TemplateData.cpp codegen]
**How to avoid:** Ship `stringId` as the *most common* form (two NUL C-strings) but document it as the
preset most likely to need a per-chunk override — which is exactly the D-09 "presets are editable
data" rationale. Do NOT block the phase on a universal stringId layout.
**Warning signs:** A worked-example chunk whose stringId field won't round-trip — fix it by editing
the preset, not the engine.

### Pitfall 3: Dock.Fill / SplitContainer layout in the Tier-B pane (Pitfall 8)

**What goes wrong:** A `Dock.Fill` control sent to back starves Top/Bottom siblings (07-04b: a
structured region went empty).
**Why it happens:** A Dock.Fill control docks FIRST when at the back of the z-order and grabs the rect.
**How to avoid:** Keep Fill at front (add first / `BringToFront`), never `SendToBack`; for multi-section
panes prefer nested `SplitContainer` (set Size before `SplitterDistance`). Guard the `IEditorPlugin`/
form ctor against MEF silent-reject. [VERIFIED: `[[feedback_winforms_dockfill_zorder]]`, `[[feedback_caller_attrs_binary_compat]]`]
**Warning signs:** The new live-decode pane renders empty or zero-height at runtime.

### Pitfall 4: Enum `e()` vs bit-vector `v()` value semantics

**What goes wrong:** Treating the bit-vector named value as a mask when SOE stores it as a bit *position*.
**Why it happens:** `e(a=0,b=1)` maps label→literal int; `v(walk=1,run=2)` maps label→`1 << (bit-1)`
(bit is 1..32). [VERIFIED: DataTableColumnType.cpp lines 136-194]
**How to avoke:** The named-value-map attribute should support BOTH conventions explicitly: an enum map
(label→value) and a flags map (label→bit-position, rendered as OR'd `walk|run`). Mirror the modder's
mental model exactly.
**Warning signs:** Flags rendering shows `2` where the modder expects `run` at bit 2 → value 2.

### Pitfall 5: Grep-gate hygiene on a new token-heavy feature

**What goes wrong:** A plan acceptance "grep X returns zero matches" is literal and trips on source comments.
**Why it happens:** Template/codec code mentions many SWG type names in comments.
**How to avoid:** Reword source comments to avoid any gated token; keep historical names only in
non-gated docs. [VERIFIED: `[[feedback_gsd_grep_gate_hygiene]]`]

## Code Examples

### Verified IFF composite write order (the D-09 source of truth)

```cpp
// Source: swg-client-v2 .../sharedFile/src/shared/Iff.cpp (READ-ONLY reference; port understanding only)
// Iff.cpp:826  insertChunkFloatVector       -> x, y, z                       (3 × f32 LE)
// Iff.cpp:843  insertChunkFloatVectorArgb   -> a, r, g, b                    (4 × f32 LE)  [VectorArgb]
// Iff.cpp:861  insertChunkFloatTransform    -> matrix[y][x], y:0..2 x:0..3   (12 × f32 LE, row-major 3×4)
// Iff.cpp:876  insertChunkFloatQuaternion   -> w, x, y, z                    (4 × f32 LE)  [w FIRST]
// Iff.cpp:893  insertChunkString            -> istrlen(string)+1            (NUL C-string, no length prefix)
// Iff.cpp:1425 read_misc = plain memcpy, NO byte-swap -> the client is LITTLE-endian on Win32 (Pitfall 6)
```

### Verified kernel encoder idiom (mirror this)

```csharp
// Source: ClefFieldCodec.cs (verified) — host-endian-safe LE float write
public static void WriteFloatLe(Stream s, float value) {
    byte[] four = BitConverter.GetBytes(value);
    if (!BitConverter.IsLittleEndian) Array.Reverse(four);
    s.Write(four, 0, four.Length);
}
// NUL C-string, no length prefix (Pitfall 2):
byte[] bytes = Encoding.ASCII.GetBytes(value); s.Write(bytes,0,bytes.Length); s.WriteByte(0x00);
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `.tab` spreadsheet type-header (flat only) | JSON template + kernel/preset two-layer | this phase | Expresses nested structs + variable arrays the `.tab` model never could. |
| `.tdf` text DSL (transcribe a known layout) | Tier-B hex-driven builder (discover from bytes) | this phase | Binds authoring to live bytes; the "better mousetrap" reframe. |
| Kaitai `.ksy` (not byte-safe on count fields) | kernel D-10 count auto-recompute | this phase | Byte-exact re-encode where Kaitai cannot. |

**Deprecated/outdated:**
- A spreadsheet importer for flat chunks was considered and NOT adopted (D-specifics) — the `.tdf`
  text DSL was always the arbitrary-layout path; JSON-file-canonical + hex-builder supersedes both.

## D-08 Kernel ↔ SOE `.tdf` Vocabulary Map (GROUNDING)

The SOE `.tdf` type vocabulary (TemplateData.cpp STATE_TYPE, lines 528-622) is: `int` (with
min/max int limits), `float` (min/max float limits), `bool`, `string`, `filename`, `stringId`,
`vector`, `objvar` (server-only DynamicVar), `template<X>`, `enum<X>`, `struct<Y>`, `triggerVolume`,
plus `list` framing (LIST_LIST). [VERIFIED: TemplateData.cpp:528-622]

| SOE `.tdf` type | D-08 kernel mapping | Notes |
|-----------------|---------------------|-------|
| `int` | sized signed int (LE) | `.tdf` carries min/max limits; kernel deliberately omits limits (validation is a plausibility concern, not a byte-layout one — safe omission). |
| `float` | `f32` | `.tdf` float == 32-bit. |
| `bool` | uint8 (1 byte) | matches `ClefFieldCodec` bool8 handling. |
| `string` | NUL C-string | never length-prefixed. |
| `filename` | NUL C-string | same on-wire form as string; a display hint only. |
| `stringId` | preset (struct over kernel) | consumer-specific on-wire form — see Pitfall 2. |
| `vector` | preset (3×f32) | see D-09. |
| `enum<X>` | int + named-value-map attribute (D-11) | display sugar, not a kernel type. |
| `struct<Y>` | kernel `struct` | nested layout. |
| `template<Z>` | NOT in scope | reference to another template file; the SOE indirection is not an IFF leaf-payload shape. **Safely omitted** — out of leaf-chunk scope. |
| `objvar` / DynamicVar | NOT in scope | server-only data type (the parser literally rejects it for lists). **Safely omitted.** |
| `triggerVolume` | NOT in scope | server/gameplay composite, not a generic leaf payload. **Safely omitted.** |
| `list` (var/fixed array) | D-10 array kinds | `.tdf` supports fixed + var arrays; kernel adds the third (trailing-remainder) as the byte-exact safety net. |

**Confirmed safe omissions:** `template<>`, `objvar`, `triggerVolume` are SOE *template-graph*
concepts (cross-file references / server gameplay), not raw IFF leaf-payload byte shapes. The locked
kernel deliberately omits them and that is safe for the SWG chunk shapes in scope (leaf-chunk
payloads of int/float/bool/string/struct/array). f64 is a kernel ADDITION over `.tdf` (which has no
64-bit float) — harmless, covers any chunk that stores doubles.

## D-09 SWG Composite Byte Layouts (PINNED — do NOT guess)

All multi-byte scalars are **little-endian** on the Win32 client (`Iff::read_misc` is a plain
`memcpy`, no byte-swap — Iff.cpp:1425). [VERIFIED: swg-client-v2 source]

| Preset | Exact on-wire layout | Order | Width | Source (file:line) |
|--------|----------------------|-------|-------|--------------------|
| `vector` | 3 × f32 LE | **x, y, z** | 12 bytes | Vector.h (x,y,z public floats); Iff.cpp:826 `insertChunkFloatVector` writes x,y,z. [VERIFIED] |
| `quaternion` | 4 × f32 LE | **w, x, y, z (w FIRST)** | 16 bytes | Quaternion.h (w,x,y,z, w declared first); Iff.cpp:876 writes w,x,y,z; Iff.cpp:1512 reads w,x,y,z. [VERIFIED] |
| `matrix` (Transform) | 12 × f32 LE | **row-major 3×4** (`matrix[y][x]`, y:0..2, x:0..3) | 48 bytes | Iff.cpp:861 `insertChunkFloatTransform` + Iff.cpp:1495 `read_floatTransform` both iterate `for y in 0..2 { for x in 0..3 }`. NOT 4×4 — a 3×4 affine (rotation 3×3 + translation column). [VERIFIED] |
| `color` (3 conventions — THIS is why presets are editable data) | see below | — | — | — |
| `color` / PackedRgb | 3 × uint8 | **r, g, b** | 3 bytes | PackedRgb.h (r,g,b public uint8); AffectorColor.cpp:170-172 reads r,g,b; :192-194 writes r,g,b. [VERIFIED] |
| `color` / PackedArgb | 1 × uint32 LE | **ARGB packed** `A<<24 \| R<<16 \| G<<8 \| B` | 4 bytes | PackedArgb.h:72 `uint32 m_argb`; :77-84 `convert` packs A<<24,R<<16,G<<8,B<<0. [VERIFIED] |
| `color` / VectorArgb | 4 × f32 LE | **a, r, g, b** | 16 bytes | Iff.cpp:843 `insertChunkFloatVectorArgb` writes a,r,g,b. [VERIFIED] |
| `stringId` | preset (consumer-specific) | typically table-cstring then text-cstring | variable | StringId.cpp (`table:text` pair); on-wire form varies by chunk — ship the two-cstring form, document as override-prone (Pitfall 2). [VERIFIED layout-logical; ASSUMED universal on-wire form] |

**The multiplicity is the whole point (D-09):** SWG stores color three different ways (3×u8, packed
u32-ARGB, 4×f32-ARGB). A chunk that disagrees with the shipped default `color` preset is a one-click
preset edit, not an engine change. Ship `color` as PackedRgb (3×u8 r,g,b — the most common leaf form);
ship `colorArgb32` (packed u32) and `colorArgbF` (4×f32) as additional named presets so the modder
picks the right one.

## D-11 Enum/Bitfield Grammar (GROUNDING)

The SOE DataTable convention (DataTableColumnType.cpp:84-230) modders already know:

- **Enum** `e(label=val,...)[default]` — maps label → literal int (`strtol` base-0, so hex `0x..` is
  allowed). [VERIFIED: lines 136-160]
- **Bit-vector** `v(label=bit,...)[default|NONE]` — maps label → `1 << (bit-1)` where **bit is a
  position 1..32**, NOT the mask. Render OR'd as `walk|run`. Default may be `NONE`. [VERIFIED: lines 161-194]
- Single-char type codes: `i`/`f`/`s`/`b`/`h`(hash-string→int)/`e`/`v`/`p`(packed objvars)/`z`(enum
  from external datatable)/`c`(comment). [VERIFIED: lines 99-229]

The D-11 named-value-map attribute should mirror BOTH `e()` (label→value) and `v()` (label→bit-position)
so the rendering matches the modder's `e(a=0,b=1)` / `v(walk=1,run=2)` muscle memory.

## Match & Precedence (D-04 / D-05) grounding

- **D-05 altitude gate is already structural:** `DecodeIffCommand` dispatches built-ins by **root-FORM
  sub-type** (PEFT/TGEN/CLEF/datatable/OT) BEFORE any leaf-level logic. A template engages only when a
  file has no root-FORM built-in and a leaf has no typed decoder — exactly the raw-fallback site every
  decoder already has. [VERIFIED: DecodeIffCommand.cs:92-117]
- **D-04 match key:** the stable-id path format (`FORM:CLEF/0/FORM:0003/0/CPAP:CPAP/0`) already encodes
  ancestor-FORM-path + version-FORM + leaf tag via `MutableIffDocument.DeriveStableId`. The match key is
  a prefix/predicate over that same path — reuse `DeriveStableId`, do not invent a new addressing scheme.
  [VERIFIED: MutableIffDocument.cs:161-177]
- **Version-FORM awareness** is naturally available because the version FORM (`0001`/`0002`/`0003`) is a
  container node in the path. The CLEF CPAP 3-layout case is the canonical reason this is REQUIRED, not
  optional. [VERIFIED: ClefFieldCodec.EncodeCpap version branches]

## Verb + MCP Surface (D-15 / D-16) grounding

- **Verb-count ceiling is NOT a risk.** `Program.cs` already registers **29** verbs via the `Type[]`
  `ParseArguments` overload (the comment notes the old 16-arity cap; the `Type[]` overload has no cap).
  Adding 2-3 `template-*` verbs is mechanical: add the `*Options` type to the `ParseArguments(...)`
  list + a `case` in `Dispatch`. [VERIFIED: Program.cs]
- **`apply-save-template` shape** clones `ApplySaveIffCommand`: `--root` containment via
  `LooseOverridePath.Resolve` (exit 2 on PathContainment), re-parse-for-validity, byte-identity verify
  on untouched leaves, atomic commit on clean verify, fail-closed (exit 2, no write) otherwise. The
  ONLY difference: the mutated payload comes from the kernel encoder + template, not `--mutate-hex`.
  [VERIFIED: ApplySaveIffCommand.cs]
- **`decode-with-template`** should follow `DecodeTrnCommand`'s alias-delegation (delegate to the same
  builder the `decode-iff --template` branch uses, so they cannot drift). Exit codes: 0 ok / 2
  parse|decode / 3 not-found. [VERIFIED: DecodeTrnCommand.cs]
- **MCP tool** = one `[McpServerTool(ReadOnly=true,Idempotent=true)]` method that `root.Resolve`s the
  path and shells `decode-with-template` via `CliDispatcher`, returning `CliResultMapper.ToCallToolResult`
  verbatim. ZERO format logic (MCP-OOP). [VERIFIED: ReadTools.cs `summarize_*` pattern]

## TJT Host Surface (D-02 Tier-B) grounding

- The Tier-B pane attaches in `FormIffEditor.pnlLeafEditor` as a **new pane mode** alongside the
  existing hex (`txtHex`) / text (`txtText`) modes. Tree selection already routes through a
  `BindLeaf(MutableIffNode leaf)`-style handler that calls `leaf.GetPayloadCopy()`; the edit commit
  path calls `leaf.SetPayload(...)`. The new mode reads the same `GetPayloadCopy()` bytes, runs the
  headless decode + FitReport, renders typed fields + the live round-trip indicator, and on edit calls
  `SetPayload` with the kernel-encoded bytes. [VERIFIED: FormIffEditor.cs:400-545, IffChunkTree.cs]
- **Undo:** `IffEditController` already wraps `MutableIffDocument` edits with `ProcessCmdKey` Ctrl+Z/Y
  capture regardless of focused control. Recommend riding the existing controller (Claude's discretion
  per CONTEXT) — a template field edit is just a payload `SetPayload`, which the controller already
  tracks. [VERIFIED: FormIffEditor.cs:54-64, 216-230]
- **Save▾ provenance matrix** (`miSaveInPlace`/`miSaveLooseOverride`/`miSaveAs`/`miPatchLive`/
  `miRepackTre`, Source-gated via `RefreshSaveMenuEnabledState`) is reused unchanged — a
  template-applied edit is a normal dirty `MutableIffDocument`. [VERIFIED: FormIffEditor.cs:91-200]
- **Cross-repo:** the pane lands in the sibling `UtinniPlugins` repo (standing write authority; paired
  commit, no human checkpoint except a live smoke). [VERIFIED: CLAUDE.md standing authorities]

## Validation Architecture

> nyquist_validation is enabled (config.json has no `nyquist_validation: false`; the
> `gsd-nyquist-auditor` key is present). This section is REQUIRED.

The byte-exact round-trip gate (DEC-C3) is validated through **synthesize-through-the-writer**
fixtures (canonical-by-construction) across BOTH SWGEmu and Restoration/Infinity lineages, mirroring
the proven Phase 20/22 fixture idiom.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit (net472 lanes `UtinniCoreDotNet.Tests`, `Utinni.Cli.Tests`; net10 `Utinni.Mcp.Tests`) |
| Config file | none — xUnit auto-discovers; build the solution with MSBuild first |
| Quick run command | `dotnet test Utinni.Cli.Tests --no-build --filter "FullyQualifiedName~Template"` |
| Full suite command | MSBuild `Utinni.sln /p:Configuration=Release /p:Platform=x86` then `dotnet test --no-build` (per AGENTS.md: never `dotnet build` — MSB3823 on WinForms .resx) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PROD-IFFT-01 | Kernel decodes every type (ints/f32/f64/cstring/char[n]/raw/pad/struct/3 array kinds) + presets | unit | `dotnet test Utinni.Cli.Tests --no-build --filter "Template&KernelCodec"` | ❌ Wave 0 |
| PROD-IFFT-01 | D-09 presets decode to exact values (vector x,y,z; quat w,x,y,z; matrix 3×4 row-major; 3 color forms) | unit | `...--filter "Template&Preset"` | ❌ Wave 0 |
| PROD-IFFT-02 | **count-from-prior array grow/shrink round-trips byte-exact + count field recomputed** | golden | `...--filter "Template&CountRecompute&Roundtrip"` | ❌ Wave 0 (CRITICAL) |
| PROD-IFFT-02 | trailing-remainder + fixed-count arrays round-trip byte-exact | golden | `...--filter "Template&Array&Roundtrip"` | ❌ Wave 0 |
| PROD-IFFT-02 | version-FORM-aware match (CLEF-CPAP-style 3-layout) picks the right layout | unit | `...--filter "Template&VersionMatch"` | ❌ Wave 0 |
| PROD-IFFT-02 | D-05 altitude: a template never engages on a built-in root-FORM file | unit | `...--filter "Template&Precedence"` | ❌ Wave 0 |
| PROD-IFFT-02 | `apply-save-template` fails closed on a failed untouched-leaf verify (no write) | integration | `...--filter "ApplySaveTemplate&FailClosed"` | ❌ Wave 0 |
| PROD-IFFT-03 | D-14 worked-example chunk templates round-trip byte-exact (double as goldens) | golden | `...--filter "Template&WorkedExample&Roundtrip"` | ❌ Wave 0 |
| PROD-IFFT-03 | MCP thin tool shells decode-with-template, zero format logic | integration | `dotnet test Utinni.Mcp.Tests --no-build --filter "Template"` | ❌ Wave 0 |

### Dual-lineage fixture matrix (DEC-C3)

Per the Phase 20 Wave-0 grounding ([VERIFIED: TgenEraVersions.cs]): the two real lineages are
**SWGEmu** and **SWG Infinity** (maintainer directive: "use SWG Infinity and SWGEmu, no Restoration"
— Restoration's proprietary TRE encryption makes its payloads unreachable; v6000+ is enumerate-only).
For templates the lineage axis is **version-FORM divergence**, not encryption: build each worked-example
fixture at a low ("SWGEmu-era") AND a high ("Infinity-era") version FORM where they differ; where the
versions are identical, document "no observed lineage drift" and keep one fixture. The CLEF
CPAP-style 3-layout case is the canonical version-divergence exemplar to mirror.

### Sampling Rate

- **Per task commit:** quick run (`--filter "Template"`) — sub-30s.
- **Per wave merge:** full `dotnet test --no-build` (after an MSBuild build).
- **Phase gate:** full suite green before `/gsd:verify-work`; the count-recompute + worked-example
  goldens are the DEC-C3 byte-exact gate.

### Wave 0 Gaps

- [ ] `Utinni.Cli.Tests/Template/TemplateTestFixtures.cs` — synthesize-through-`IffWriter` fixtures
  (clone `ClefTestFixtures.cs` idiom: `MutableIffNode.NewContainer` + `AddContainer` + `AddLeaf` with
  kernel-encoded payloads). Covers PROD-IFFT-01/02/03.
- [ ] `Utinni.Cli.Tests/Template/KernelCodecTests.cs` — per-type decode/encode + the count-recompute
  golden (the CRITICAL test).
- [ ] `Utinni.Cli.Tests/Template/RoundtripTemplateCommandTests.cs` — verb-level byte-exact gate.
- [ ] `Utinni.Cli.Tests/Template/ApplySaveTemplateTests.cs` — fail-closed + atomic write.
- [ ] `Utinni.Mcp.Tests/Template/...` — thin-tool shell-out shape.
- [ ] Worked-example template JSON files (D-14) committed under the shipped pack dir + referenced as fixtures.
- Framework install: none — xUnit already present in all three test projects.

## Security Domain

> `security_enforcement` is not set to `false` in config.json (absent = enabled).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Local offline tool; no auth surface. |
| V3 Session Management | no | No sessions. |
| V4 Access Control | yes (path) | `LooseOverridePath.Resolve` `--root` containment (throws on escape) — reuse, do not reinvent. The MCP `ResolvedRoot.Resolve` throws on escape → SDK tool error. [VERIFIED] |
| V5 Input Validation | yes | `IffPayloadCursor` bounds-checks every read + the division-form guard "never allocate on attacker-controlled counts"; `IffWriter` 64 MB `MaxChunkSize` cap. A user-authored template is attacker-controllable input — apply the SAME guards: bound array element counts, reject negative/overflowing widths, cap total decoded size. [VERIFIED: IffPayloadCursor.cs Need(); IffWriter.cs MaxChunkSize] |
| V6 Cryptography | no | No crypto; v6000+ encrypted TRE payloads are enumerate-only (out of template scope). |

### Known Threat Patterns for the template engine

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malicious template declares a huge fixed-count array → OOM | Denial of Service | Bound counts; the cursor's "never allocate on attacker-controlled counts" rule + the 64 MB cap. [VERIFIED idiom] |
| Template path escape (load a pack from outside the scanned dirs) | Tampering / Elevation | Scan only the D-12 dir allow-list; resolve template paths within those roots. |
| Count-from-prior field claims more elements than the payload holds | Tampering | The cursor throws `Truncated` past end-of-payload; surface as a FitReport "does not fit" rather than over-read. [VERIFIED] |
| `apply-save-template` writes outside the client root | Tampering | `LooseOverridePath.Resolve` containment + atomic write + fail-closed verify. [VERIFIED] |
| JSON deserialization of a hostile template (Newtonsoft type-handling) | Tampering | Deserialize to a fixed POCO model; do NOT enable `TypeNameHandling`. |

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| VS2026 MSBuild (v145/x86) | build | ✓ | Dev18 | — |
| dotnet (net10 + net472 test lanes) | tests | ✓ | per toolchain | — |
| Newtonsoft.Json | template JSON | ✓ (referenced) | project-pinned | — |
| CommandLineParser | verbs | ✓ (referenced) | project-pinned | — |
| ModelContextProtocol | MCP tool | ✓ (referenced) | net10 | — |
| swg-client-v2 source | D-09 layout reference | ✓ (read-only) | pinned corpus | — |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** none.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `stringId` ships as two NUL C-strings (table then text) as its default preset on-wire form | D-09 / Pitfall 2 | LOW — it's an editable preset by design (D-09); a non-matching chunk is a one-click preset override, and the worked examples don't have to use stringId. |
| A2 | The D-14 worked examples can be drawn from SWG leaf chunks that are genuinely template-eligible (no root-FORM built-in) | D-14 / Validation | LOW — synthesize-through-the-writer fixtures don't require a real asset; if a real example is wanted, pick a leaf under a FORM Utinni doesn't decode. |
| A3 | Riding the existing `IffEditController` undo stack is acceptable for template field edits | D-02 grounding | LOW — explicitly Claude's discretion; a template edit is a normal `SetPayload`, already tracked. |
| A4 | No worked-example chunk needs f64 (the kernel addition over `.tdf`) to be exercised by a real SWG shape | D-08 map | LOW — f64 is included regardless; this only affects whether a golden exercises it. |

**All four assumptions are LOW-risk and align with the D-09 "presets are editable data" design — none
block planning.** Everything load-bearing (the encode-parity mechanism, the composite byte layouts,
the grammar conventions, the verb/host surfaces) is VERIFIED.

## Open Questions (RESOLVED)

1. **stringId universal on-wire form** (A1)
   - What we know: logically `table:text`; serialization is consumer-specific.
   - What's unclear: the single most common leaf-chunk encoding (two cstrings vs crc+string).
   - Recommendation: ship two-cstring as default; document as override-prone (D-09 makes this cheap).
   - **RESOLVED:** plan 23-02 ships stringId as two NUL C-strings, documented override-prone. LOW-risk (A1).
2. **Which two worked-example chunks to ship (D-14)**
   - What we know: they double as goldens + teaching artifacts.
   - What's unclear: exact chunk choice — planner's discretion.
   - Recommendation: one count-from-prior-array chunk (exercises the critical D-10 recompute) + one
     flat composite chunk (exercises vector/quaternion/color presets). That pairing maximizes golden
     coverage of the encode-parity risk.
   - **RESOLVED:** plan 23-05 ships `counted_records.json` (count-from-prior) + `flat_composite.json`
     (vector/quaternion/color presets) per the recommendation. LOW-risk (A2).

## Sources

### Primary (HIGH confidence — VERIFIED this session)

- `swg-client-v2/.../sharedFile/src/shared/Iff.cpp` (lines 826-882, 1425-1520) — composite write/read
  order + memcpy-no-byteswap LE confirmation (D-09).
- `swg-client-v2/.../sharedMath/src/shared/{Vector,Quaternion,PackedArgb,PackedRgb}.h` — struct field order/widths.
- `swg-client-v2/.../sharedTerrain/.../AffectorColor.cpp` (lines 170-194) — PackedRgb 3×u8 r,g,b on-wire.
- `swg-client-v2/.../sharedTemplateDefinition/.../core/TemplateData.cpp` (lines 528-622, 1781-1788) — `.tdf` type vocabulary (D-08).
- `swg-client-v2/.../sharedUtility/.../DataTableColumnType.cpp` (lines 84-230) — `e()`/`v()` grammar (D-11).
- `UtinniCoreDotNet/Formats/Iff/{MutableIffNode,IffWriter,MutableIffDocument}.cs` — byte-exact length-ripple engine (PROD-IFFT-02).
- `UtinniCoreDotNet/Formats/ClientEffect/ClefFieldCodec.cs` — variable-length codec precedent + CPAP 3-version layout (D-04).
- `UtinniCoreDotNet/Formats/Decoders/IffPayloadCursor.cs` — kernel-primitive anchor + V5 bounds guards.
- `Utinni.Cli/{Program.cs, Commands/DecodeIffCommand.cs, ApplySaveIffCommand.cs, DecodeTrnCommand.cs}` — verb surface, altitude dispatch, alias-delegation, verb-count reality.
- `Utinni.Mcp/Tools/ReadTools.cs` — thin shell-out MCP pattern.
- `UtinniPlugins/.../UI/Forms/FormIffEditor.cs` + `UI/Controls/IffChunkTree.cs` — Tier-B host surface.
- `Utinni.Cli.Tests/ClientEffect/ClefTestFixtures.cs` + `Fixtures/trn/TgenEraVersions.cs` — fixture idiom + dual-lineage grounding.

### Secondary (MEDIUM confidence)

- Auto-memory `[[feedback_winforms_dockfill_zorder]]`, `[[feedback_caller_attrs_binary_compat]]`,
  `[[feedback_gsd_grep_gate_hygiene]]`, `[[project_swg_iff_no_pad]]` — Pitfall 8 + grep-gate hygiene + IFF no-pad.

### Tertiary (LOW confidence)

- stringId universal on-wire form (A1) — logical structure verified; single canonical encoding not pinned.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all in-repo / already-referenced; verified against live code.
- Architecture (encode-parity mechanism): HIGH — verified end-to-end (`apply-save-iff` ships
  length-changing edits today; the only new obligation is the in-payload count recompute).
- D-09 composite layouts: HIGH — pinned to swg-client-v2 source with file:line citations.
- D-08/D-11 grammar grounding: HIGH — pinned to TemplateData.cpp / DataTableColumnType.cpp.
- Pitfalls: HIGH — drawn from verified prior-phase lessons.
- stringId on-wire form: LOW — flagged A1.

**Research date:** 2026-06-20
**Valid until:** 2026-07-20 (stable — pure-managed, no fast-moving external deps; swg-client-v2 is a pinned read-only corpus)
