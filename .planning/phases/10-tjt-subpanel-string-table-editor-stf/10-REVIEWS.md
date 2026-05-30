---
phase: 10
type: cross-ai-plan-review
reviewers: [codex, cursor-agent]
date: 2026-05-30
verdict: ACCEPT-WITH-CHANGES
status: dispositioned
---

# Phase 10 Plans — Cross-AI Peer Review ("the friends")

Two independent external AI CLIs reviewed the committed Phase 10 plan set
(`10-01` … `10-06-PLAN.md`) before execution, same as the Phase 2 / Phase 6
pre-merge review precedent.

- **CODEX** (`codex exec`, gpt-5.1-codex-max, read-only sandbox)
- **cursor-agent** (`cursor-agent.cmd -p --mode ask --trust`)

**Both verdicts: ACCEPT-WITH-CHANGES.** The two reviews converged independently
on the same five findings with nearly identical ranking — high signal. Notably,
both elevated finding #1 (a real clean-parallel-build bug) that the internal
gsd-plan-checker had rated only INFO (I-1).

## Findings (merged, ranked) + disposition

### F1 — 10-02 `depends_on` wave bug — BLOCKING (build correctness)
Both reviewers: `10-02-PLAN.md` declared `depends_on: []` / `wave: 1`, but its
Task 1 consumes 10-01's public API (`StringTableDocument.FromBytes`,
`StringTableWriter.Serialize`). The "different project so parallel-safe"
rationale is wrong — different project, **same API dependency**. A wave
scheduler deriving order from frontmatter launches 10-02 in Wave 1 against types
that don't exist yet → clean-run build failure.
**Disposition: ✅ FIXED NOW.** `10-02` → `wave: 2`, `depends_on: ["10-01"]`.

### F2 — Canonical-ordering byte-exactness assumed, not proven vs real files — HIGH (SC4)
The "full re-serialize == byte-exact" claim rests on real client `.stf` being
stored strings-id-ascending / names-name-ascending (Ordinal). RESEARCH A6 lists
this UNCONFIRMED. Synthetic builder output always agrees with itself and cannot
catch this class of bug. If a real file deviates, save mutates untouched bytes →
SC4 "no corruption" breaks on real input.
**Fix (both):** (a) `StringTableWriter` short-circuit — **if zero entries dirty,
emit the original file bytes verbatim** and skip re-serialization (makes the
no-edit round-trip byte-exact UNCONDITIONALLY, neutralizing the A6 risk); (b)
add ≥1 **real extracted `.stf`** to the 10-02 golden corpus, not only synthetic.
**Disposition: ⏳ RECOMMENDED for 10-01 + 10-02 before execution.**

### F3 — Name validation is a subset of the engine rules — MEDIUM (SC2)
Plans enforce no-leading-digit + no-duplicate. SOE `validateStringName` /
`fixupStringName` is stricter (non-empty, lowercase normalization, restricted
charset). The editor would accept e.g. `"Foo Bar"` / `"ABC"` that the client
can't resolve → silent per-entry SC2/SC3 failure on rename/add.
**Fix:** 10-01 `ValidateName` mirrors the FULL engine ruleset; acceptance test
asserts every rule (the source is already in 10-01 Task 1 `read_first`).
**Disposition: ⏳ RECOMMENDED for 10-01.**

### F4 — UTF-16 fidelity golden is BMP-only — MEDIUM (SC4)
`charCount` is a UTF-16 **code-unit** count, not a code-point count. `João` is
all-BMP and won't catch a surrogate-pair off-by-one (which would corrupt every
entry after the first non-BMP char).
**Fix:** add a non-BMP fixture (CJK ext-B or emoji) to the 10-02 roundtrip golden.
**Disposition: ⏳ RECOMMENDED for 10-02.**

### F5 — sourceCrc-on-edit policy unverified vs client — MEDIUM (SC3)
"Preserve on edit, 0 on add" is fine for deterministic goldens, but RESEARCH A5
("does the client read sourceCrc at lookup?") is unresolved. If the client keys
on it, a stale preserved crc on an edited entry could make the edit not render.
**Fix:** keep the deterministic policy for goldens, but add an explicit
10-06 smoke step that edits an entry (leaving crc stale) and confirms the edited
text renders in-client; flip edited-entry crc to `int(time(0))` if it doesn't.
**Disposition: ⏳ RECOMMENDED for 10-06** (joins the existing A8 reload-vs-relog
check already folded into the smoke).

## Net
Ship-ready after F1 (the only hard build bug — now fixed). F2–F5 are
correctness-hardening that both reviewers strongly recommend folding in BEFORE
execution rather than rediscovering at smoke. None are architectural; all are
surgical additions to 10-01 / 10-02 / 10-06.
