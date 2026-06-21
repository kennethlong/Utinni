# Phase 23: User-Definable IFF Chunk Templates - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-20
**Phase:** 23-user-definable-iff-chunk-templates
**Areas discussed:** Authoring surface, Match & auto-apply (+ Tier-C readiness), Type system depth, Storage/share/CLI

**Session character:** User-driven brainstorm. Two clarification redirects reshaped the phase
substantially: (1) "research how modders actually need this" → two parallel research threads (SOE
historical pipeline + modern RE tooling); (2) "are they using paper because the tools suck — can we
build a better mousetrap?" → reframed the authoring surface from transcription tool to a live,
hex-driven builder (Tier B). User explicitly noted brainstorming "leads to better outcomes."

---

## Authoring surface

| Option | Description | Selected |
|--------|-------------|----------|
| JSON file (canonical) | Project idiom; diffable/shareable; field-list maps 1:1 onto the MutableIffDocument field==byte-range encode model | ✓ |
| Custom C-like text DSL | 010/ImHex/.tdf-style struct DSL; expressive but bespoke parser = heavy for a quick win | |
| Tier A — file + grid form | Conventional add-rows grid; functional, undifferentiated | (fallback only) |
| **Tier B — in-place hex-driven builder** | Select byte range → assign type+name → template grows → live decode + continuous byte-exact round-trip check; un-annotated bytes stay raw | ✓ |
| Tier C — + corpus inference | Sample all instances of a tag → propose structure | (deferred) |

**User's choice:** JSON file canonical + **Tier B** hex-driven builder; Tier A as scope-bite
fallback only; **Tier C inference deferred to its own phase.**
**Notes:** Reframe originated from the user's "better mousetrap" challenge. Research (both threads)
confirmed: serious RE tools (010/Kaitai/ImHex) + the SOE schema path (`.tdf`) all make the template
a hand-authorable *file*; GUIs are views over the file. The expensive part is *discovering* a layout
from bytes, not transcribing it — so bind authoring to the live bytes. Utinni uniquely positioned
(MutableIffDocument field==byte-range DOM + existing FormIffEditor hex pane).

## Match & auto-apply (+ Tier-C readiness)

| Option | Description | Selected |
|--------|-------------|----------|
| Tag + optional FORM-path | Key auto-captured at authoring, widenable to tag-only; version-aware | ✓ |
| Tag only (4CC) | Matches any chunk with the tag; can't distinguish same-tag-different-layout | |
| Manual-select only | No auto-apply; fails criterion 2 | |
| Built-ins win / templates fill gaps | Templates apply only to otherwise-hex leaves | ✓ |
| Templates can override built-ins | Template shadows a built-in codec | (deferred) |

**User's choice:** Tag + optional FORM-path (version-aware, **required** — the CLEF CPAP
0001/0002/0003 case); built-ins win / templates fill otherwise-hex leaves; override deferred.
**Notes:** Key insight surfaced in brainstorm — built-ins are *whole-format* codecs (root-FORM),
templates are *per-leaf-chunk* payload decoders; the altitude difference makes "built-ins win" fall
out for free, minimal collision. Forward-compat: template contract mirrors the built-in decoder
interface so boundary = precedence not architecture. Round-trip check doubles as a match-fit
confidence signal. Separately agreed the **Tier-C on-ramp guardrails** (headless engine [load-bearing];
fit-check as a pure function; type-plausibility predicate library powering Tier-B select→suggest;
match-key as a corpus query) — each also improves B, so zero speculative cost.

## Type system depth (the encode-parity crux)

| Option | Description | Selected |
|--------|-------------|----------|
| Flat-only | Primitives + fixed composites; no arrays/structs (fails criterion 1) | |
| **Kernel + presets-as-sugar** | Tiny kernel (ints/floats/strings/bytes+padding/struct/array) + presets (color/vector/quat/matrix/stringId) = structs over the kernel | ✓ |
| Array kinds: fixed only | Constant counts | |
| Array kinds: all three | fixed / count-from-prior (auto-recompute on encode) / trailing-remainder | ✓ |
| Enum/bitfield sugar | Optional named-value map on int fields (SOE e()/v() convention) | ✓ (in) |

**User's choice:** Kernel + presets-as-sugar; **all three array kinds**; enum/bitfield display sugar
**in**.
**Notes:** Byte-exactness lives entirely in the kernel — a preset can't introduce a parity bug, and
fixing/adding a preset never touches the engine. Count-from-prior-field with engine auto-recompute on
write is THE encode-parity mechanism (the documented gap that makes Kaitai not byte-safe). Flagged
research line item: exact SWG composite byte layouts (color packing, quaternion order, matrix
dims/major-ness) MUST come from the swg-client-v2 engine reference, not guessed.

## Storage, share & CLI

| Option | Description | Selected |
|--------|-------------|----------|
| Single app-data dir | Simplest, always-discoverable | |
| **Scanned list of dirs** | Shipped + app-data + project-local packs; mirrors SWG searchPath; share = clone a pack | ✓ |
| Presets only | Just the type presets | |
| **Presets + worked examples** | Examples double as golden fixtures + teaching artifacts | ✓ |

**User's choice:** Scanned list of dirs (shareable packs); presets + a couple of worked examples.
**Notes:** Verbs-first for the engine + golden byte-exact gate + thin MCP read tool follows from the
standing DEC-V2-VERBS-FIRST/MCP-OOP locks and the already-chosen headless engine (nearly free). The
interactive hex-authoring stays UI-only — a legitimate non-exception (an interaction, not a batch
capability). Template JSON carries its own version field for forward migration.

## Claude's Discretion

- Exact `utinni-cli` template verb names + flag shapes; `decode-with-template` standalone vs a
  `decode-iff --template` branch (alias-delegation precedent).
- JSON envelope + the JSON template-schema field-record shape (name/type/repeat/enum-map/encoding +
  version field).
- Multi-match tie-break UI; interior Tier-B builder controls (Pitfall 8 layout; MEF-safe ctor).
- Whether authoring rides the existing IffEditController undo stack; exact dir paths + scan order.
- Sentinel-terminated array support if cheap once the three locked kinds exist.

## Deferred Ideas

- Tier C corpus inference (own phase) — substrate laid by Tier B's Tier-C guardrails.
- Templates overriding built-in codecs (architecture left open; override path/UI deferred).
- Sentinel-terminated arrays (planner discretion / later type-system extension).
- Tier-A grid field-builder (scope-bite fallback only).
- Reviewed-not-folded todos: phase09 datatable review warnings, phase10 stringtable live-reload,
  swg window-resize edge cases, phase21 terrain IHDR nesting (all off-domain).
