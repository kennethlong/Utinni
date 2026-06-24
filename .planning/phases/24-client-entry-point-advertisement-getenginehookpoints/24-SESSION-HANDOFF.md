# Phase 24 — session handoff (2026-06-24)

Tight resume pointer. Full detail lives in `.planning/STATE.md` (loaded each session) + the docs linked below.

## Status: DONE + verified. Both repos clean + pushed.

- **Advertised DX11 client (SwgClient_r.exe + gl11_r.dll) is FULLY FUNCTIONAL under injection:** boots →
  renders login → loads world (Mos Eisley) → embed scales on startup + maximize. Closes DX11 acceptance
  (Checkpoint 2) + embed-resize (RNDR-04).
- **Hookpoint harness genericized to "EngineHook"** (dropped Utinni branding so any app can mod a client).
  Live-smoke verified: `resolved 39/92 by name`, overlay installs, renders + scales, no crash.
- **Commits (pushed):** Utinni `b321e94`; provider swg-client-v2 `734299751`. No uncommitted work either side.

## The ONE open follow-on (orthogonal to render)

**Per-hook editor unlock on the advertised client** — light up the MISC/INPUT subsystems (scene/chat
editors). The bindings + per-target `installable()` gating infra are in place; the work is per-hook ABI
verification before dropping the MISC/INPUT group-skip in `utinni.cpp createDetours()`. A wholesale drop
is UNSAFE (game::mainLoop→Game::run ABI mismatch crashes; CuiManager render+findObjectUnderCursor needs
per-target gating). Each hook needs a maintainer LIVE smoke. NOT started.

## How to resume / key operational facts

- **Live smoke:** launch via Utinni `Launcher.exe` (starts `SwgClient_r.exe` suspended + injects). `ut.ini`
  already points `swgClientPath=D:\Code\swg-client-v2\stage\`. Read `bin/Release/utinni.log` after.
- **Cross-repo:** the contract `engine_hookpoints.{h,inc}` is SHARED-VERBATIM with swg-client-v2. The
  PROVIDER instance owns swg-client-v2 edits (I write Utinni; hand the provider a prompt for its repo).
  Do NOT sweep the provider's unrelated `.planning/` WIP into commits (stage exact paths).
- **Cosmetic divergence (non-blocking):** Utinni's `engine_hookpoints.h` doc-comment prose is genericized;
  the provider's keeps "Utinni" prose (identifier-only rename). Contract identifiers/structs/version/POD
  all match → byte-compatible. Reconcile (provider adopts the genericized prose) anytime, optional.

## Hard-won guardrails (also in memory)

- Advertised-client window/detour/render changes need a maintainer LIVE smoke BEFORE commit — installable()
  / headless builds / ABI gates cannot catch ABI / ASLR / embed-state / render-correctness failures.
- Non-advertised detours that target hardcoded SWGEmu addresses must be GATED OFF on the advertised client
  (installable() wrongly passes when the stale address lands in the relocated module → corruption).
- ALWAYS `DETOUR_LEN_AUTO` (explicit length corrupts the trampoline on recompiled prologues).
- Verify a crash dump's date/build before triage; verify commit contents (`git show --stat`) after a rename.
- ABI re-bless: run `UtinniCoreDotNetGen.exe "<repo>\"` explicitly (incremental build skips the gen), test
  the FRESH gen against the fixture, then `git checkout -- Generated` before commit; never commit Generated.

## Detail docs

- `.planning/STATE.md` — full state (the canonical entry).
- `24-DX11-ADVERTISED-CLIENT-GAP.md` — the crash/resize chain + resolution.
- `24-PROVIDER-REQUEST-*.md` — the provider asks (detour-vs-call, gl11 startup, §1 injection regression,
  EngineHook rename). The stale-dump one (`...gl11-startup-crash.md`) is SUPERSEDED/withdrawn.
- memory `feedback_verify_dump_freshness`, `feedback_verify_commit_contents_after_rename`,
  `feedback_detourxs_explicit_len`, `feedback_d3d9_hook_diagnosis`.
