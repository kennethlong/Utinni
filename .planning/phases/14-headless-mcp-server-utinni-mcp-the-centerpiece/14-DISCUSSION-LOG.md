# Phase 14: Headless MCP server (`Utinni.Mcp`) — the centerpiece - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-05
**Phase:** 14-Headless MCP server (`Utinni.Mcp`) — the centerpiece
**Areas discussed:** Tool surface shape, MCP SDK vs hand-rolled, Root config + safety UX, CLI result/error mapping

---

## Framing

Most of the WHAT and the architecture for Phase 14 was already locked by the ROADMAP Phase-14 constraint guard-rails and the Phase-13 CONTEXT carry-forwards (net10 + stdio-only, no in-proc SDK, `resolvedRoot` fail-closed, typed-args-only, `save` loose-override default + envelope, repack as its own `dry_run`-gated tool, `MCP-SECURITY.md` as a design-time deliverable, thin-dispatcher-zero-logic). The discussion surfaced the four genuinely-open HOW decisions below.

---

## Tool surface shape

| Option | Description | Selected |
|--------|-------------|----------|
| Fine-grained per-verb tools | `read_tre`/`read_iff`/`save_iff`/… — typed per-format arg schemas, ~1:1 with CLI verbs | (research) |
| Coarse dispatcher tools | `read_asset(path)` auto-detects format and routes internally | (research) |

**User's choice:** Delegated to research.
**Notes:** Recorded as D-01 with a recommended lean toward fine-grained format×intent tools (roadmap guard-rail: over-broad shapes are un-retrofittable). Research decides exact count/naming and whether build verbs ship as MCP tools this phase.

---

## MCP SDK vs hand-rolled

| Option | Description | Selected |
|--------|-------------|----------|
| Official `ModelContextProtocol` C# SDK | Stdio transport + tool registration + schema/annotation plumbing for free | (research) |
| Hand-rolled JSON-RPC-over-stdio | Fully owned, no bleeding-edge dep, reimplements the handshake | (research) |

**User's choice:** Delegated to research.
**Notes:** Recorded as D-02 with a recommended lean toward the official SDK *if* net10-compatible/stable and able to express tool annotations; hand-rolled is the fallback. Research pins package+version and confirms net10 on the self-hosted runner.

---

## Root config + safety UX

| Option | Description | Selected |
|--------|-------------|----------|
| `--root` CLI arg (+ env fallback) | Client launch command supplies root; fail closed if absent | (research) |
| Config file | Root read from a config file at startup | (research) |
| Real MCP elicitation vs advisory-only | Whether the 5-layer "elicitation" step is an interactive prompt or advisory annotations in a headless loop | (research) |

**User's choice:** Delegated to research.
**Notes:** Recorded as D-03. Lean: `--root` arg + env fallback, fail-closed, never accept absolute agent paths, canonicalize via `LooseOverridePath.Resolve`; elicitation = real when client supports it, structural layers (resolved-root, loose-override-default, verify-before-commit, backup) as the always-on enforcement otherwise.

---

## CLI result/error mapping

| Option | Description | Selected |
|--------|-------------|----------|
| Pass CLI JSON envelope through | Forward the sorted-key envelope as the tool result; no re-shaping | (research) |
| Re-shape into MCP structured content | Translate the envelope into MCP content blocks | (research) |

**User's choice:** Delegated to research.
**Notes:** Recorded as D-04. Lean: pass the envelope through (re-shaping would re-add forbidden business logic); transport/exec failures + SOE hang-on-error → hard MCP error after a timeout backstop; expected in-band CLI failures → return the envelope. Research decides exact content-block shaping + timeout strategy.

---

## Claude's Discretion

- Final resolution of all four areas above (user explicitly delegated to research).
- `Utinni.Mcp` project layout / solution placement / CI build lane / how it locates `utinni-cli.exe`.
- Test approach for the real-MCP-client round-trip success criterion (scripted client vs recorded transcript), per DEC-C3.

## Deferred Ideas

- Live-injected MCP bridge (named-pipe IPC for in-client preview) — MCP-03, already roadmapped to Phase 16.
- Exposing the build/authoring verbs as MCP tools — research (D-01) decides whether any ship this phase; otherwise a later increment.
