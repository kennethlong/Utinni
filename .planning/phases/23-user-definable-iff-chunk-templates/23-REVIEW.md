---
phase: 23-user-definable-iff-chunk-templates
reviewed: 2026-06-21T21:43:09Z
depth: standard
files_reviewed: 18
files_reviewed_list:
  - UtinniCoreDotNet/Formats/Template/KernelCodec.cs
  - UtinniCoreDotNet/Formats/Template/TemplateResolver.cs
  - UtinniCoreDotNet/Formats/Template/TemplatePackStore.cs
  - UtinniCoreDotNet/Formats/Template/TemplateJson.cs
  - UtinniCoreDotNet/Formats/Template/TemplateModel.cs
  - UtinniCoreDotNet/Formats/Template/FitReport.cs
  - UtinniCoreDotNet/Formats/Template/TypePlausibility.cs
  - UtinniCoreDotNet/Formats/Template/ITemplateContracts.cs
  - UtinniCoreDotNet/Formats/Decoders/BuiltinRootForms.cs
  - UtinniCoreDotNet/Formats/Decoders/IffPayloadCursor.cs
  - Utinni.Cli/Commands/ApplySaveTemplateCommand.cs
  - Utinni.Cli/Commands/DecodeWithTemplateCommand.cs
  - Utinni.Cli/Commands/ListTemplatesCommand.cs
  - Utinni.Cli/Commands/RoundtripTemplateCommand.cs
  - Utinni.Cli/Program.cs
  - Utinni.Mcp/Tools/ReadTools.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TemplateBuilderPane.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormAssignFieldDialog.cs
findings:
  critical: 2
  warning: 6
  info: 5
  total: 13
status: issues_found
---

# Phase 23: Code Review Report

**Reviewed:** 2026-06-21T21:43:09Z
**Depth:** standard
**Files Reviewed:** 18
**Status:** issues_found

## Summary

Reviewed the schema-driven IFF chunk-template codec (KernelCodec/IffPayloadCursor), the match +
precedence resolver, the JSON (de)serializer, the scanned-dir pack store, the four CLI verbs, the MCP
dispatcher, and the WinForms builder/assign-field UI. The security backbone is genuinely strong: path
containment (`LooseOverridePath`) is single-sourced and segment-checks `..` before canonicalizing;
`TypeNameHandling = None` is explicitly set on both read and write Newtonsoft settings (gadget-chain
mitigation holds); decode loop-reads count-from-prior arrays through the bounds-checked cursor (no
pre-allocation on attacker counts); fixed counts are capped; the MCP tools carry zero format logic and
resolve the root before dispatch. The byte-exact encode discipline (raw-span re-emit for FixedChar/
RawBytes/Pad, D-10 count auto-recompute) is sound for the common path.

Two correctness defects rise to BLOCKER: (1) the resolver does NOT enforce the version-FORM-required
match constraint that its own comments and the D-04 design repeatedly promise — the load-bearing guard
against the CLEF-CPAP multi-layout mis-decode is missing, and this resolver feeds the `apply-save-template`
write path; (2) the D-10 count auto-recompute writes the live element count into the count field's
declared width with a silent `unchecked` truncation, so growing an array past the field's range writes a
corrupt count with no error. Several WARNING-class robustness gaps round out the set.

## Critical Issues

### CR-01: Resolver does not enforce the version-FORM-required match it promises (D-04 mis-decode guard missing)

**File:** `UtinniCoreDotNet/Formats/Template/TemplateResolver.cs:242-260`
**Issue:** `KeyMatches` computes `versionSegment = ExtractVersionFormSegment(leafStableId)` and sets the
`versionFormAware` diagnostic flag, but then returns `true` for both the tag-only branch (line 250) and
the empty-ancestor branch (line 259) **without ever testing `versionSegment != null`**. The surrounding
comments are explicit and repeated that this is required:
- line 236-238: "REQUIRED: even a tag-only widen keeps the version-FORM"
- line 247-248: "the match still REQUIRES the leaf carry a version-FORM in its path"
- line 256-257: "still respects version-FORM awareness (the leaf must have a version segment)"

The file header (line 30-31) states version-FORM awareness is required "so the CLEF-CPAP 3-layout case
cannot silently mis-decode." As written, a `MatchTagOnly` or empty-`MatchAncestorPath` template matches a
leaf whose stable-id has no enclosing FORM/LIST/CAT sub-type (`versionSegment == null`) — exactly the
silent mis-decode the design forbids. Because `TemplateResolver.Resolve` is the resolver behind
`apply-save-template` (`ApplySaveTemplateCommand.cs:130`), a wrong template can decode/re-encode and write
a corrupted leaf. No test exercises the no-version-segment case (TemplateResolverTests only asserts
`VersionFormAware == true` on fixtures that already carry a version-FORM), so the gap is unguarded.
**Fix:** Require the version segment before returning a widened match:
```csharp
if (t.MatchTagOnly)
{
    if (versionSegment == null) return false; // D-04: never drop version-FORM awareness
    specificity = 0;
    return true;
}

string ancestor = (t.MatchAncestorPath ?? "").Trim();
if (ancestor.Length == 0)
{
    if (versionSegment == null) return false;
    specificity = 0;
    return true;
}
```
Add a TemplateResolverTests case: a tag-only template against a leaf whose path has no FORM/LIST/CAT
sub-type must NOT match.

### CR-02: D-10 count auto-recompute silently truncates a too-large live count into the count field width

**File:** `UtinniCoreDotNet/Formats/Template/KernelCodec.cs:278-279, 361-381`
**Issue:** On encode, when a field is the count source for a count-from-prior array, `EncodeFields` writes
`LiveElementCount(boundArray, values)` (an `int` element count, lines 465-474) through
`WriteIntegerField`. `WriteIntegerField` writes the value at the field's declared width with
`unchecked((byte)value)` for I8/U8 (line 367) and truncating LE writes for I16/U16/I32/U32 — with NO
range check. If the count field is declared `U8` (or `U16`) and the array grows beyond 255 (or 65535),
the encoder writes a wrapped, wrong count and the produced bytes silently describe a different array
length than was actually written. This corrupts the file with no exception. The same class of silent
truncation applies to a user `--set` on an integer field whose value exceeds the field width
(`ApplySaveTemplateCommand.CoerceTo` parses a full `long`/`uint`, then `WriteIntegerField` truncates).
This defeats the byte-exact contract whenever the count outgrows its on-wire width.
**Fix:** Range-check before writing in `WriteIntegerField` and throw a typed `TemplateException`:
```csharp
private static void WriteIntegerField(FieldRecord f, long value, Stream s)
{
    switch (f.Type)
    {
        case KernelType.I8:  RequireRange(f, value, sbyte.MinValue, sbyte.MaxValue); s.WriteByte(unchecked((byte)value)); break;
        case KernelType.U8:  RequireRange(f, value, byte.MinValue,  byte.MaxValue);  s.WriteByte(unchecked((byte)value)); break;
        case KernelType.I16: RequireRange(f, value, short.MinValue, short.MaxValue); WriteInt16Le(s, value); break;
        case KernelType.U16: RequireRange(f, value, ushort.MinValue, ushort.MaxValue); WriteInt16Le(s, value); break;
        case KernelType.I32: RequireRange(f, value, int.MinValue,  int.MaxValue);  WriteInt32Le(s, value); break;
        case KernelType.U32: RequireRange(f, value, uint.MinValue, uint.MaxValue);  WriteInt32Le(s, value); break;
        default: throw new TemplateException(...);
    }
}
// RequireRange throws TemplateException("count/value " + value + " does not fit field '" + f.Name + "' (" + f.Type + ").")
```

## Warnings

### WR-01: `summarize_with_template` MCP tool cannot reach a multi-eligible-leaf file (no --leaf passthrough)

**File:** `Utinni.Mcp/Tools/ReadTools.cs:154-162`
**Issue:** `SummarizeWithTemplate` dispatches `decode-with-template` with only the asset path. The CLI
auto-picks a leaf only when exactly one is eligible; when more than one matches, the CLI returns a
no-match envelope with reason "pass --leaf <stableId>" (`DecodeWithTemplateCommand`/`PickLeafStableId`,
DecodeWithTemplateCommand.cs:169-171). The MCP tool exposes no way to supply `--leaf`, so a perfectly
valid multi-template file is unreachable via MCP — the agent gets a dead-end "ambiguous" envelope with no
recourse. (Mechanism only — the boundary itself is correct.)
**Fix:** Add an optional `[Description(...)] string leafStableId = null` parameter and append
`new[] { abs, "--leaf", leafStableId }` when non-empty (still no format logic; pure passthrough).

### WR-02: `TemplateBuilderPane.SetPayload` / `ReplaceFields` mutate the caller's shared TemplateModel in place

**File:** `The Jawa Toolbox/.../UI/Controls/TemplateBuilderPane.cs:253-281`
**Issue:** `SetPayload`, `AppendField`, and `ReplaceFields` store and mutate the passed-in `TemplateModel`
(and its `Fields` list) directly — no defensive clone. `OnTemplateSelected` clones before handing a pack
template to the pane, but other call paths (e.g. an auto-resolved template, or any future caller) would
have the pane mutate the shared instance. `TemplateModelDefaults.Clone` exists for exactly this defensive
discipline (TemplateModel.cs:86) but is not used here. A pack-loaded template that is later re-listed
would reflect the user's in-progress edits.
**Fix:** Clone on seed: `template = TemplateModelDefaults.Clone(model) ?? new TemplateModel { ... }` in
`SetPayload`, and have the host pass a clone (or clone here) before `AppendField`/`ReplaceFields` mutate.

### WR-03: `UntilEnd` array of fixed-width raw spans can over-read into the next field's bytes (greedy, no boundary)

**File:** `UtinniCoreDotNet/Formats/Template/KernelCodec.cs:203-207`
**Issue:** `RepeatKind.UntilEnd` loops `while (cursor.Remaining > 0)`. If an UntilEnd array is NOT the
last field in its container (e.g. a struct with an UntilEnd array followed by a trailing field), it
greedily consumes all remaining bytes and the trailing field then throws Truncated — or worse, if the
element width does not evenly divide the remaining bytes, the final element read throws and the whole
decode fails even though the bytes are well-formed for a correctly-authored layout. There is no guard that
UntilEnd is terminal, and no "remaining not a multiple of element width" diagnostic. This is reachable
from user-authored templates (the assign-field dialog offers "Until end of chunk" for any field).
**Fix:** Either reject a non-terminal UntilEnd array at template-validation time (a structural
`TemplateException`), or document/validate that UntilEnd must be the last field of its (sub)record. At
minimum, surface a clear error when `cursor.Remaining` is non-zero but smaller than one element width.

### WR-04: FixedChar `Encoding` is silently ignored on decode (decode returns raw bytes, never a string)

**File:** `UtinniCoreDotNet/Formats/Template/KernelCodec.cs:152` (vs IffPayloadCursor.cs:169-180 / ITemplateContracts.cs:68-69)
**Issue:** `DecodeField` decodes `KernelType.FixedChar` via `cursor.ReadRawBytes(width)` (raw `byte[]`),
not the purpose-built `ReadFixedChar(n, encoding)`. This is correct for byte-exact round-trip, but it
means the `Encoding` field on a FixedChar is completely inert on decode — the contract (ITemplateContracts
"fixed-width char[n]") and the assign-field dialog (which offers an Encoding for string kinds) imply a
decoded string. The value surfaces as hex in the UI and JSON envelope, and `--set name=text` cannot edit a
FixedChar (CoerceTo has no `byte[]` branch). The dedicated `ReadFixedChar` is now dead code.
**Fix:** Decide the contract: either treat FixedChar as opaque bytes (then drop `ReadFixedChar`, remove
the Encoding affordance for FixedChar in the dialog, and document it as a byte span), or carry both a
display string and the raw span so the value is editable. Do not leave the Encoding silently inert.

### WR-05: `FitReport.ConsumedExactly` is true for a zero-field template over an empty payload (vacuous fit)

**File:** `UtinniCoreDotNet/Formats/Template/FitReport.cs:60-72`
**Issue:** A template with no fields decoded against an empty payload yields `consumed == total == 0`, so
`ConsumedExactly` is `true`. Combined with CR-01's missing version guard, an empty-`Fields`,
empty-`MatchAncestorPath`, tag-only-style template would report a confident "fits" on any empty/zero-byte
leaf of the right tag. Even with CR-01 fixed, a fields-empty template reporting a green round-trip is
misleading for the builder's live indicator.
**Fix:** Treat a fields-empty template as not-a-fit (or surface a distinct "no fields defined" state in
`FitCheck`/`UpdateIndicator`) rather than `ConsumedExactly = true`.

### WR-06: `RefreshDecode` swallows all exceptions, leaving the indicator green-stale on an inconsistent template

**File:** `The Jawa Toolbox/.../UI/Controls/TemplateBuilderPane.cs:708-729`
**Issue:** The MEF-safe catch-all is appropriate, but the `catch (Exception)` block is empty and the only
state passed to `UpdateIndicator` is the `fit` from the FIRST call (`FitChecker.FitCheck`). If
`FitCheck` succeeds (returns a report) but the subsequent `KernelCodec.Decode` throws (e.g. an
inconsistency the pure fit-check tolerates but the decode does not), the field rows are cleared yet the
indicator still renders whatever `fit` reported — potentially a green "Round-trip OK" over an empty field
list. The two engine calls can disagree.
**Fix:** Drive the indicator from a single decode attempt, or set `fit = null` in the catch so
`UpdateIndicator` shows the honest does-not-fit state when the decode half fails.

## Info

### IN-01: O(n²) count-source lookup per encoded field

**File:** `UtinniCoreDotNet/Formats/Template/KernelCodec.cs:275, 451-463`
**Issue:** `EncodeFields` calls `FindCountFromPriorArrayFor` (a linear scan of the sibling field list) for
every field, making encode O(n²) in field count. Not in v1 perf scope and field counts are small, but a
one-pass pre-map of count-field-name -> array would simplify the logic.
**Fix:** Build a `Dictionary<string, FieldRecord>` of count-bound arrays once before the field loop.

### IN-02: Newtonsoft `NullValueHandling.Ignore` on write but no symmetric default-handling note

**File:** `UtinniCoreDotNet/Formats/Template/TemplateJson.cs:65`
**Issue:** Write settings drop nulls; a field with `ByteWidth = 0` or `MatchTagOnly = false` serializes
the default (these are value types, not null), so round-trip is stable. This is fine, but the D-01
"two equal models produce byte-identical text" claim relies on default value-type members always
serializing — worth a test asserting a model with all-default scalar members round-trips byte-identically.
**Fix:** Add a TemplateJsonTests assertion for an all-defaults model.

### IN-03: `DecodeArrayElement` single sub-field discards sibling context for count-from-prior elements

**File:** `UtinniCoreDotNet/Formats/Template/KernelCodec.cs:228-233`
**Issue:** A single-sub-field array element decodes with a fresh empty `Dictionary` as siblings, so an
element that is itself a count-from-prior array referencing a prior element sub-field cannot resolve its
count. Not reachable today (single-sub-field elements have no prior siblings), but the asymmetry with the
multi-field branch (line 235-237, which builds a proper `inner`) is a latent trap if element shapes grow.
**Fix:** Document the single-sub-field element as "scalar only, no intra-element references," or unify both
branches through the multi-field path.

### IN-04: `DecorateNamedValue` flags bit math can shift by a negative amount on a bit position of 0

**File:** `The Jawa Toolbox/.../UI/Controls/TemplateBuilderPane.cs:908-912`
**Issue:** `1L << (int)(e.Value - 1)` assumes flag entries are bit positions 1..32. If a malformed/typo'd
template stores a flags entry with value 0, the shift count is -1 (in C# a negative shift count is masked
to `31 & -1 = 31`, so it does not throw but produces a wrong bit). The assign-field dialog does not
validate the 1..32 range on flags entries (FormAssignFieldDialog.cs:259-281 accepts any long).
**Fix:** Validate flags entry values are in 1..32 in `BuildValueMap` (skip/clamp out-of-range), and guard
`DecorateNamedValue` against `e.Value < 1`.

### IN-05: Match key composition differs between store-shadow and list-templates display

**File:** `Utinni.Cli/Commands/ListTemplatesCommand.cs:105-111` vs `TemplatePackStore.cs:286-292`
**Issue:** `TemplatePackStore.MatchKeyOf` (the shadow key) and `ListTemplatesCommand.MatchKeyOf` (the
display key) are two separate implementations of "the match key." They will not drift in behavior today
(display vs shadow are different purposes), but the duplicated concept invites future divergence — a
reader could assume the listed key is the shadow key.
**Fix:** Name them distinctly (e.g. `ShadowKey` vs `DisplayKey`) or derive the display string from the
single shadow-key source to make the relationship explicit.

---

_Reviewed: 2026-06-21T21:43:09Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
