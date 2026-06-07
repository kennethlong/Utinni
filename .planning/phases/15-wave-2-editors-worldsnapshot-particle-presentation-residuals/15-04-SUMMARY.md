---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 04
subsystem: cli-mcp
tags: [PROD-W2-PRT, particle, prt, peft, cli, mcp, decode-iff, roundtrip, read-assist, byte-exact, headless]

requires:
  - phase: 15-02
    provides: ".prt / FORM PEFT typed codec (ParticleEffectDocument.FromBytes/FromIff + ParticleEffectWriter.Serialize + MutableParticleEffect)"
  - phase: 14
    provides: "ReadTools thin dispatch pattern, ResolvedRoot fail-closed root, CliDispatcher seam, CliResultMapper verbatim envelope pass-through, exact-tool-surface integration test"
  - phase: 13
    provides: "Utinni.Cli verb conventions (JsonOutput envelope, exit-code taxonomy, 16-verb cap Type[]/Dispatch pattern)"
provides:
  - "decode-iff auto-dispatches a FORM PEFT root into the 15-02 particle codec (emitter-group/emitter counts + raw-preserve tally)"
  - "roundtrip-particle CLI verb — byte-exact whole-file round-trip gate over the .prt codec"
  - "summarize_particle MCP read tool (ReadOnly, thin exit-code dispatch, zero format logic) — the D-08 read path the in-app Explain effect button reuses"
affects: [15-06, 15-08, particle-editor, particle-live-preview]

tech-stack:
  added: []
  patterns: [decode-iff-root-form-auto-dispatch, cli-json-envelope-exit-code-taxonomy, mcp-thin-dispatch-by-exit-code, fail-closed-root-resolve-before-spawn, synth-fixture-through-iff-primitives]

key-files:
  created:
    - "D:/Code/Utinni/Utinni.Cli/Commands/RoundtripParticleCommand.cs"
    - "D:/Code/Utinni/Utinni.Cli.Tests/Commands/RoundtripParticleCommandTests.cs"
    - "D:/Code/Utinni/Utinni.Cli.Tests/Commands/DecodeParticleDispatchTests.cs"
    - "D:/Code/Utinni/Utinni.Cli.Tests/Commands/ParticleCliFixtures.cs"
    - "D:/Code/Utinni/Utinni.Mcp.Tests/ParticleReadToolTests.cs"
  modified:
    - "D:/Code/Utinni/Utinni.Cli/Commands/DecodeIffCommand.cs"
    - "D:/Code/Utinni/Utinni.Cli/Program.cs"
    - "D:/Code/Utinni/Utinni.Mcp/Tools/ReadTools.cs"
    - "D:/Code/Utinni/Utinni.Mcp.Tests/RoundTripTests.cs"
    - "D:/Code/Utinni/Utinni.Cli.Tests/Fixtures/dispatch/help.expected.txt"
    - "D:/Code/Utinni/Utinni.Cli.Tests/Fixtures/dispatch/no-args.expected.txt"

key-decisions:
  - "decode-iff PEFT auto-dispatch is handled in Run() (where the source bytes are in scope) rather than in TryDecode(), because the particle typed model captures the raw IFF tree for byte-exact re-emit and needs the bytes — preferred over a standalone decode verb per PATTERNS.md MCP note."
  - "Added a dedicated summarize_particle MCP tool (rather than relying solely on the existing decode_iff tool) so the .prt read-assist surface is a named, self-documented D-08 read path; it still dispatches decode-iff, keeping ZERO format logic in the server (D-06)."
  - "roundtrip-particle is a no-mutation whole-file byte-exact gate (the codec exposes EditLeafPayload/RewriteCount but no named-field CLI edit), the analog of roundtrip-ot's no-mutation whole-file branch; degraded fixtures (unknown root version, truncated WVFM) round-trip byte-identical, proving D-05 holds through the verb."

patterns-established:
  - "decode-iff root-FORM auto-dispatch: a new typed format is exposed headless + over MCP by adding ONE root-FORM branch to decode-iff Run() — no new top-level verb, no new MCP read tool required (summarize_* is an optional named alias)."
  - "MCP read-assist tool = clone DecodeIff EXACTLY: root.Resolve (fail-closed, throws on escape BEFORE any spawn) -> cli.RunAsync(verb) -> CliResultMapper verbatim pass-through; zero format logic."

requirements-completed: [PROD-W2-PRT]

duration: ~40min
completed: 2026-06-07
---

# Phase 15 Plan 04: Particle (.prt) headless read-assist (CLI + MCP) Summary

**`.prt` / FORM PEFT read-assist shipped headless — `decode-iff` auto-dispatches PEFT, a new `roundtrip-particle` byte-exact gate, and a thin `summarize_particle` MCP read tool — all wrapping the 15-02 codec with zero new format logic and zero new format logic in the MCP server (D-06).**

## Performance

- **Duration:** ~40 min
- **Tasks:** 2 (both TDD, auto)
- **Files modified:** 11 (5 created + 6 modified)

## Accomplishments

- `decode-iff` now auto-dispatches a `FORM PEFT` root into the 15-02 particle codec, emitting `{type:"particle", rootType:"PEFT", version, emitterGroupCount, emitterCount, groups[], rawPreserved, rawPreservedEmitters}` — the typed read both the MCP client and the in-app button consume.
- New `roundtrip-particle` CLI verb: byte-exact whole-file round-trip gate (`ParticleEffectDocument.FromBytes` -> `Serialize` -> re-parse -> assert identical), with the standard JSON envelope + 0/1/2/3 exit-code taxonomy; generic `Exception` intentionally NOT caught. Registered in `Program.Type[]` + `Dispatch` switch (16-verb cap already solved).
- New `summarize_particle` MCP read tool (`ReadOnly=true, Idempotent=true`): resolves under the pinned `ResolvedRoot` (throws on escape before any spawn), dispatches `decode-iff`, and returns the verbatim envelope via `CliResultMapper`. Read-assist ONLY (D-07); ZERO format logic in the server (D-06); the same `.prt` read path the in-app `Explain effect` button reuses (D-08).
- Golden coverage: 10 CLI Particle facts (clean + degraded-root + truncated-WVFM byte-identity, FileNotFound/malformed/non-PEFT exit codes, PEFT auto-dispatch, Program.Dispatch wiring, help registration) + 5 MCP Particle facts (dispatch verb+resolved-path, 3 path-escape zero-spawn Theory rows, verbatim pass-through). Fixtures synthesized through public IFF primitives (no real `.prt` fixtures exist today).

## Task Commits

1. **Task 1: decode-iff PEFT dispatch + roundtrip-particle byte-exact golden verb** — `a868d4f` (feat)
2. **Task 2: summarize_particle MCP read tool (thin dispatch-by-exit-code)** — `470dd28` (feat)

_Both tasks are tdd="true"; implementation wraps the already-tested 15-02 codec, so each shipped as one feat commit with its golden tests (the codec's RED/GREEN was 15-02)._

## Files Created/Modified

- `Utinni.Cli/Commands/RoundtripParticleCommand.cs` — byte-exact `.prt` round-trip verb (JsonOutput envelope, exit codes 0/1/2/3).
- `Utinni.Cli/Commands/DecodeIffCommand.cs` — added the `FORM PEFT` root branch + `BuildParticleResult` summary + `ParticleParseException` catch.
- `Utinni.Cli/Program.cs` — registered `RoundtripParticleOptions` in the `Type[]` + a `Dispatch` case.
- `Utinni.Cli.Tests/Commands/{RoundtripParticleCommandTests,DecodeParticleDispatchTests,ParticleCliFixtures}.cs` — goldens + synth fixtures.
- `Utinni.Cli.Tests/Fixtures/dispatch/{help,no-args}.expected.txt` — refreshed for the new verb (Rule 1).
- `Utinni.Mcp/Tools/ReadTools.cs` — added `summarize_particle` (thin clone of `DecodeIff`).
- `Utinni.Mcp.Tests/ParticleReadToolTests.cs` — 3 behaviors (dispatch / path-escape / verbatim).
- `Utinni.Mcp.Tests/RoundTripTests.cs` — exact-tool-surface gate 11 -> 12 (Rule 1).

## Decisions Made

See `key-decisions` frontmatter. In short: PEFT dispatch lives in `decode-iff Run()` (needs source bytes); `summarize_particle` is a named D-08 alias over `decode-iff`; `roundtrip-particle` is a no-mutation whole-file byte-exact gate (degraded fixtures still round-trip identical).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Refreshed `decode-iff`/dispatch goldens for the new verb**
- **Found during:** Task 1 (CommandDispatchTests after registering `roundtrip-particle`).
- **Issue:** `dispatch/help.expected.txt` + `dispatch/no-args.expected.txt` goldens listed the prior verb set; adding `roundtrip-particle` changed the help/no-args output (documented Rule-1 precedent from 11-02 / 14).
- **Fix:** Re-baselined both golden files from the freshly-masked actual output.
- **Files modified:** `Utinni.Cli.Tests/Fixtures/dispatch/{help,no-args}.expected.txt`.
- **Verification:** `CommandDispatchTests` 4/4 green.
- **Committed in:** `a868d4f` (Task 1 commit).

**2. [Rule 1 - Bug] Updated the MCP exact-tool-surface integration gate 11 -> 12**
- **Found during:** Task 2 (`RoundTripTests.Handshake_ListsExactly...` failed: Expected 11, Actual 12).
- **Issue:** Adding `summarize_particle` grew the tool surface; the SC5 exact-set gate (intentionally no "tolerate N optional" fudge) must enumerate the new tool.
- **Fix:** Added `summarize_particle` to `ExpectedTools` and renamed the test to `...TheTwelveNamedTools`.
- **Files modified:** `Utinni.Mcp.Tests/RoundTripTests.cs`.
- **Verification:** Full MCP suite 77/77 green (end-to-end against the built `Utinni.Mcp.exe` + `utinni-cli.exe`).
- **Committed in:** `470dd28` (Task 2 commit).

---

**Total deviations:** 2 auto-fixed (both Rule 1 — golden/expectation re-baselines necessitated by the new verb + tool, the documented precedent).
**Impact on plan:** No scope creep; both are mechanical expectation refreshes for intentionally-added surface.

## Issues Encountered

- Git Bash mangled MSBuild `/p:` switches into a path; switched to `-p:` dash-prefixed flags. Generated `UtinniCore.cs` churned on the Cli build and was reverted with `git checkout --` per `project_utinnicore_cs_regen_churn` (no managed-codec change touched native types).

## Verification

- `dotnet test Utinni.Cli.Tests --no-build -c Release --filter Particle`: **10/10** pass.
- `dotnet test Utinni.Mcp.Tests --no-build -c Release --filter Particle`: **5/5** pass.
- Full `Utinni.Cli.Tests`: **249 pass / 2 skip** (no regressions). Full `Utinni.Mcp.Tests`: **77/77** pass (end-to-end real-client integration).
- Grep gates: no `ParticleEffect` / `Formats.Particle` anywhere in `Utinni.Mcp/` (D-06 holds); `summarize_particle` is `ReadOnly = true` (D-07); no generic `catch (Exception)` in `RoundtripParticleCommand`; the new option type is in both `Program.Type[]` and `Dispatch`.

## Known Stubs

None. This plan is the CLI/MCP half of PROD-W2-PRT; the in-app `FormParticleEditor` shell + live-preview hot-retrigger remain downstream Wave-2 plans (15-06 / 15-08+) and reuse this same read path (D-08) — that is the documented split, not a stub.

## Threat Flags

None. The two trust boundaries in the plan's threat model are both mitigated: T-15-04 (path escape) is closed by `ResolvedRoot.Resolve` BEFORE any spawn (proven by the 3 zero-spawn Theory rows); T-15-01 (untrusted `.prt`) is closed by the 15-02 codec's truncation-safe cursor + exit-2-on-parse-failure (proven by the malformed/non-PEFT exit-2 facts). No new security surface introduced.

## Next Phase Readiness

- `.prt` read-assist is available headless (CLI) and over MCP — ready for the in-app `Explain effect` button (15-06) to reuse the identical path.
- The particle editor shell + live-preview hot-retrigger (15-06 / 15-08+) are the remaining PROD-W2-PRT surface; the codec + read path they compose are in place.

## Self-Check: PASSED

All 5 created source files + the SUMMARY exist on disk; both per-task commits (`a868d4f`, `470dd28`) exist in history. 10 CLI + 5 MCP Particle facts green; full Utinni.Cli.Tests 249/2-skip and full Utinni.Mcp.Tests 77/77 green (Release|x86 + net10).

---
*Phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals*
*Completed: 2026-06-07*
