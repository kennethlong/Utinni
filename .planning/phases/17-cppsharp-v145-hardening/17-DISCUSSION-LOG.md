# Phase 17: CppSharp / v145 Hardening - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-14
**Phase:** 17-cppsharp-v145-hardening
**Areas discussed:** Re-bless ergonomics (CPPS-04a), Frozen plugin DLL (CPPS-04b), clang-20 tripwire mechanism (CPPS-03b), CI tripwire severity (CPPS-03a/b)

Framing: requirements are locked by REQUIREMENTS.md (CPPS-01..04) and the spike outcome is near-certain (→ harden-the-redirect), so discussion covered only the open HOW decisions the research flagged. Each took the research-recommended default.

---

## Re-bless ergonomics (CPPS-04a)

| Option | Description | Selected |
|--------|-------------|----------|
| `--rebless` tool mode + doc | ABI-diff tool gets a `--rebless` flag that regenerates the baseline hash file AND prints the lockstep checklist; documented in regen-bindings.md | ✓ |
| Documented manual steps only | No tool flag; maintainer runs regen/hash/freeze/commit by hand from a checklist | |
| Script wrapper (rebless + cross-repo rebuild) | One script that also drives the TJT+Sytner Release\|x86 rebuild | |

**User's choice:** `--rebless` tool mode + doc
**Notes:** Mitigates the #1 risk — gate becoming a permanent red light on legitimate API additions. One command, hard to forget a step.

---

## Frozen plugin DLL (CPPS-04b)

| Option | Description | Selected |
|--------|-------------|----------|
| Inspect both, freeze the widest | Inspect TJT host + Sytner for UtinniCore.* breadth, freeze the widest | ✓ |
| SytnersUtinniPlugin | Freeze Sytner specifically | |
| The Jawa Toolbox host | Freeze TJT host specifically | |
| Both (two fixtures) | Freeze both as separate fixtures | |

**User's choice:** Inspect both, freeze the widest → **resolved to The Jawa Toolbox host**
**Notes:** Inspection performed during discussion. TJT = 778 UtinniCore.* references across 72 files, broadest surface. **SytnersUtinniPlugin is not a C# plugin** — only a single 27-line C++ header (`sup.h`), zero `.cs`, no buildable DLL; cannot be a fixture (corrects research Open-Q #2 / assumption A5). "TJT/Sytner lockstep" effectively = TJT today.

---

## clang-20 tripwire mechanism (CPPS-03b)

| Option | Description | Selected |
|--------|-------------|----------|
| Committed pin + manual refresh | CI asserts a committed 'last-known-latest = v1.2/clang 19' pin; separate job bumps it; no egress dependency | ✓ |
| Live probe, degrade-to-warn | CI probes NuGet/GitHub, warns on network failure | |
| Live probe, hard requirement | CI hard-fails if it can't probe | |

**User's choice:** Committed pin + manual refresh
**Notes:** No self-hosted-runner egress dependency, deterministic, can't be spoofed. Preferred over trusting a live registry response even though vcpkg-from-GitHub shows some egress exists.

---

## CI tripwire severity (CPPS-03a/b)

| Option | Description | Selected |
|--------|-------------|----------|
| C++23 scan HARD-FAIL, clang-20 WARN-loud | Asymmetric: real-break scan blocks; unblock-signal probe only warns | ✓ |
| Both hard-fail | Any tripwire → red build | |
| Both warn-only | Both warn, never block | |

**User's choice:** C++23 scan HARD-FAIL, clang-20 WARN-loud
**Notes:** C++23 header adoption is a real impending break the 14.29 redirect can't parse → block. clang-20 release is good news (redirect can finally retire) → must not stall master; warn for human review.

---

## Claude's Discretion

- ABI-diff block extraction tech: BCL-only (SHA256) default; Roslyn only if line/regex extraction proves brittle (tooling project, not shipped DLLs).
- Block-hash baseline file format/location: single committed sorted-key text file; exact name/path discretionary.
- CI step placement/scripting within the existing self-hosted push-only PowerShell-5.1 verify-only model (no `pull_request`-from-fork trigger).
- Spike script shape (the `grep '__clang_major__' yvals_core.h` tabulation).

## Deferred Ideas

- CppSharp v1.2 (clang 19) upgrade + net9 generator migration — only reaches v143, still needs a redirect; deferred to a future milestone, gated by the CPPS-03b tripwire.
- Roslyn-based block extraction — only if BCL extraction proves brittle.
- A second frozen fixture for Sytner — moot until Sytner becomes a buildable C# plugin.
- Off-domain todo matches (Phase 9 datatable warnings, Phase 10 live-reload, window-resize) — reviewed, not folded; belong to their own phases.
