---
phase: 13-wrap-revived-compilers-as-cli-verbs-close-ot-tier-2
plan: 02
subsystem: api
tags: [cli, subprocess, process-start, rsp-synthesis, tre, banner-normalization]

requires:
  - phase: 07-tjt-subpanel-tre-browser
    provides: TreFile reader (Records order, GetRecordData, CompressionKind)
  - phase: 08-tjt-subpanel-iff-editor
    provides: LooseOverridePath.Resolve (path-containment guard)
provides:
  - NativeToolRunner — the first Process.Start subprocess seam in utinni-cli (shared by all BUILD verbs)
  - NormalizeBanner (idempotent __DATE__/__TIME__/abs-path strip) on both the runner and the golden harness
  - RspSynthesizer — builder-format .rsp recipe synthesis from a real .tre (AUTH-04 byte-exact engine)
affects: [13-04, 13-05]

tech-stack:
  added: []
  patterns: [Process.Start subprocess seam with CommandLineToArgvW arg-quoting, banner normalization]

key-files:
  created:
    - Utinni.Cli/Commands/Subprocess/NativeToolRunner.cs
    - Utinni.Cli/Commands/Subprocess/RspSynthesizer.cs
    - Utinni.Cli.Tests/Subprocess/NativeToolRunnerTests.cs
    - Utinni.Cli.Tests/Subprocess/RspSynthesizerTests.cs
  modified:
    - Utinni.Cli.Tests/Infrastructure/GoldenTestRunner.cs

key-decisions:
  - "net472 ProcessStartInfo has no ArgumentList; the seam takes an explicit string[] args and quotes each via the canonical CommandLineToArgvW algorithm — deliberate per-arg quoting, NOT naive concat. UseShellExecute=false means no shell, so shell-injection is structurally impossible."
  - "NormalizeBanner lives in production (NativeToolRunner) so the runner normalizes its own stderr; GoldenTestRunner delegates to it (one source of truth)."
  - "Abs-path regex stops at whitespace (does not greedily swallow trailing words); paths with spaces strip up to the first space — sufficient to stabilise the banner."

patterns-established:
  - "Subprocess seam: ProcessStartInfo(UseShellExecute=false, redirect stdout/stderr, CreateNoWindow, WorkingDirectory) + read-both-streams-before-WaitForExit (deadlock-safe) + JsonOutput envelope."
  - "RspSynthesizer guards untrusted tree-paths via LooseOverridePath.Resolve (reused framework primitive)."

requirements-completed: [AUTH-04]

duration: ~25min
completed: 2026-06-04
---

# Phase 13 Plan 02: NativeToolRunner + RspSynthesizer Summary

**The first subprocess seam in utinni-cli (NativeToolRunner) and the .rsp recipe synthesizer (RspSynthesizer) now exist and are unit-tested in isolation — the two primitives every Wave-2/3 BUILD verb composes, built with zero dependency on the native exes.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-06-04
- **Tasks:** 2
- **Files:** 4 created + 1 modified

## Accomplishments
- `NativeToolRunner.Run` — the lone `Process.Start` seam; exit-code mapping 0→0 / non-zero→2 / missing-exe→3 with the locked `{tool, exitCode, outputPath, produced, stderr}` envelope.
- `NormalizeBanner` — idempotent `__DATE__`/`__TIME__`/abs-path strip; shared by the runner and the golden harness (`MatchesNormalizedText`).
- `RspSynthesizer.Synthesize` — builder-format disk-first `.rsp` from a real `.tre` via the Phase-7 reader, record-order preserved (no pre-sort), `@u` from the public `CompressionKind`.
- 11 tests green (7 NativeToolRunner + 4 RspSynthesizer).

## Task Commits

1. **Task 1: NativeToolRunner subprocess seam + NormalizeBanner** — `66aed7d` (feat)
2. **Task 2: RspSynthesizer — .rsp recipe from a real .tre** — `1d66d04` (feat)

## Deviations from plan

- **net472 has no `ProcessStartInfo.ArgumentList`** (that is .NET Core 2.1+). The plan's "explicit arg array (NOT a concatenated command string)" intent is honored by taking a `string[] args` at the API and quoting each element via the canonical CommandLineToArgvW algorithm — the threat (T-13-04 arg-boundary injection) is mitigated, and `UseShellExecute=false` removes the shell entirely. Documented inline.
- Initial test build failed: `TreFile` is not `IDisposable` (lazy re-open reads), so the `using (TreFile ...)` blocks were invalid — removed.

## Verification

- `MSBuild Utinni.Cli.Tests.csproj /p:Configuration=Debug /p:Platform=x86` → green.
- `dotnet test --no-build --filter NativeToolRunner|RspSynthesizer` → **11 passed, 0 failed**.
- Source assertions: `NativeToolRunner.cs` has no `UseShellExecute = true` and no naive arg concat (dedicated `AppendArgument` quoter); `RspSynthesizer` uses the public `CompressionKind` and iterates `Records` directly (no pre-sort).
