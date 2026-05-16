# Test Harness Plan

**Status:** Draft — captured 2026-05-16, not yet planned as a GSD phase.
**Purpose:** Reduce manual UAT load on the maintainer by building harnesses that let Claude self-verify changes without launching the live SWG client for every loop.

## Why this matters

Utinni is a desktop GUI tool (WinForms C#) plus a native shim (UtinniCore C++ DLL) that hooks D3D9 inside the live SWG client process. The default verification loop is:

1. Build everything.
2. Launch the patched SWG client.
3. Click around in the WinForms tool.
4. The maintainer eyeballs the result.

That loop is slow, only the maintainer can close it, and it does not regress-protect anything. The plan below trades it for tiered automation.

## Testing philosophy: pragmatic, not dogmatic TDD

Blanket TDD does not fit a project where half the surface is D3D9 detours, in-process injection, and WinForms UI. TDD shines on the **pure-logic** + **file-format** layers (parsers, plugin loader, settings, transforms, data models) — and those layers currently have zero coverage. Native and UI layers get smoke/integration tests, not red-green-refactor.

## Four tiers

### Tier 1 — Pure unit tests (fully autonomous)

- **C# side:** xUnit (or NUnit) test project alongside Utinni's main solution. `dotnet test` runs in CI and locally without a game client.
- **C++ side:** Catch2 (header-only, easy to drop into UtinniCore's solution) wired through `ctest` or a `vcpkg`-managed dependency.
- **Targets:**
  - TRE / IFF parsers
  - Plugin manifest loading and discovery
  - Settings serialization / migration
  - Math helpers (transforms, quaternions, vector ops)
  - Pure data-model logic
- **Wins:** Catches every regression in pure logic with no manual loop.

### Tier 2 — CLI shim around the core (highest-leverage)

Expose the same operations the WinForms UI calls into a thin `utinni-cli` executable:

```
utinni-cli parse-tre <path>          # dumps parsed TRE structure as JSON
utinni-cli list-objects <ws.iff>     # lists world-snapshot entries
utinni-cli validate-plugin <dir>     # checks plugin manifest + exports
utinni-cli inspect-iff <path>        # readable IFF tree
```

Pair every command with golden-file tests: run the CLI against checked-in fixtures, diff against checked-in expected output. Estimated to convert **~60–70% of "Kenny please verify" loops** into something Claude can run unattended.

**Engineering shape:** the CLI lives in the same solution and references the same core libraries the WinForms tool does. The UI becomes one of two consumers, not the only consumer — which is also a structural win for the codebase.

### Tier 3 — Recorded fixtures + mock D3D9 device

For the hook code in UtinniCore that detours `IDirect3DDevice9` calls:

- **One-time capture** (requires maintainer + live SWG client): record real TRE/IFF samples to `tests/fixtures/` and a captured D3D9 call trace.
- **Replay forever:** a stub `IDirect3DDevice9` implementation that scripts recorded calls back through the hook code. Claude can regression-test depth-buffer / post-process detours **without the game running**.
- Add golden screenshots / pixel hashes for any deterministic render output (Object Explorer, depth buffer visualizers, etc).

### Tier 4 — What still requires the maintainer

Be explicit about the boundary so we do not over-promise:

- Actual injection into a running `SWG.exe`.
- Visual "does it look right" judgment for new UI.
- GPU-driver-specific bugs.
- WinForms UI smoke testing — FlaUI is *technically* possible but flaky enough we are deliberately skipping it for now.

## Suggested phase order

When this becomes a GSD phase (or phases), the natural sequence is:

1. **Tier 1 C# unit-test project** — smallest, unblocks everything else. Pick 2–3 parsers as the first targets.
2. **CLI shim (Tier 2)** — depends on tier 1 having extracted testable seams. Biggest payoff.
3. **Tier 1 C++ unit tests** — fold in once UtinniCore has refactored seams (the prior audit flagged native code quality, so this likely pairs with cleanup).
4. **Tier 3 fixtures + mock D3D9** — only worth doing when we are touching hook code intentionally.

## Open questions to resolve at planning time

- Test project layout: single `Utinni.Tests.sln` vs per-project `*.Tests` folders.
- Fixture storage: in-repo (small) vs Git LFS (binary TRE samples can be big).
- CI: GitHub Actions Windows runners (Utinni is Windows-only — no Linux fallback).
- Whether the CLI shim ships as a public artifact or stays internal to the test harness.

## Cross-refs

- Vision: `docs/ai/vision.md`
- Audit / current state: `docs/ai/assessment.md`
- Codebase map (in progress): `.planning/codebase/`
