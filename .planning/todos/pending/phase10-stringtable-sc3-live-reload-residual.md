---
resolves_phase: 15
title: Phase 10 — SC3 live-reload + CF-05 reload-semantics residual
area: live-reload
---

# Phase 10 — SC3 live-reload + CF-05 reload-semantics residual

**Status:** OPEN (deferred-but-acceptable for V1; carried from the 10-06 smoke sign-off)
**Opened:** 2026-05-30 (Phase 10 V1 sign-off — APPROVED-WITH-DEFERRED-RESIDUAL, Option C)
**Owner:** maintainer (needs an injected live SWG session)
**Severity:** non-blocking for V1 (Phase 8 P06 / Phase 9 09-07 precedent); SC3 cannot be closed by automation (F5b)

## What's open

**SC3 — "live client renders edited strings on reload"** was NOT signed off by the 10-06 automation-only
disposition. It requires a live observation that automation cannot reach without a mock SWG client
(V2 — REQ-V2-tier-3-mock-d3d9):

1. **Scene-change reload (Step 7):** with an edited `.stf` saved, trigger a TJT chat-command scene change
   (`project_scene_change_via_tjt`) and observe whether the edited string renders in-game.
   - If it renders → SC3 confirmed; the CF-05 badge copy `Reloads on next scene change.` is correct as-shipped.
   - If it does NOT (LocalizationManager caches until relog) → SC3 is relog-only: **amend the CF-05 badge
     copy** to honest relog wording (a small 10-07 inline fix / gap-closure) AND note ROADMAP SC3 is read
     as relog. Do NOT loosen the badge to over-promise.

2. **Explicit stale-crc check (F5b):** the edited entry's `sourceCrc` is left stale (preserved per D-02b).
   Confirm the edited text still renders with the stale crc. The 10-01 F5a source finding predicts this is
   harmless (the runtime lookup does not consult `sourceCrc`), so the expected result is "edit renders."
   If it does NOT render because of the stale crc, flip the entry's crc to `int(time(0))`, re-save, and
   record that the preserve-crc policy had to be amended.

## Standing automated proxies (already green)

- `roundtrip-stf` João SC4 byte-exact golden (10-02) — the edit→save→re-parse fidelity proof.
- `StringTableReloadRoutingTests.Stf_ClassifiesAsPendingNextSceneChange` (10-05) — `Classify(".stf", null)
  == PendingNextSceneChange` (tier-(b) routing; the badge wording is honest either way).

## Disposition when run

Record the Step-7 result + the badge disposition in
`.planning/phases/10-tjt-subpanel-string-table-editor-stf/10-06-SMOKE-LOG.md` (the CF-05 finding section +
the V1 sign-off block), and close this todo. If the badge is amended, ship the 10-07 inline fix.

## 2026-06-13 — Phase 15 Checklist D attempt (Claude-driven via windows-mcp, maintainer watching)

**Outcome: STILL OPEN — deferred again (Option B).** SC3 live render-on-reload was attempted against the
15-17 reassembled build and could NOT be observed, for a reason upstream of the reload-tier question:

- **Root blocker — the loose searchPath is DISABLED.** There is no active `searchPath` in any client cfg.
  `swgemu_live.cfg`'s priority-27 `searchPath_00_27=…\loose` lines are commented out (the 2026-06-12
  phantom-walk mitigation, see `[[project_swg_client_loose_overrides]]`), and `ut.ini` has
  `useSwgOverrideCfg=false` so `utinni.cfg` is not applied. The SWG client therefore never searches
  `D:\SWGEmu-Client\SWGEmu\loose\`, so **no loose override (`.stf` or `.ot`) is picked up** regardless of
  reload tier. Re-enabling it re-introduces the machine-wide retail-data shadow that caused the phantom
  walk, so the maintainer chose **not** to re-enable + relaunch for this smoke (Option B).
- **The editor save tier itself works** — drove `ui_auc.stf` → edited `accept_bid` → "Accept Bid (EDITED)"
  → Save as loose override succeeded, file on disk (+18 b = the UTF-16 " (EDITED)"). The badge copy
  "Reloads on next scene change or relog." is honest (does not over-promise) and the 15-07 classifier proxy
  (`.stf`/`.ot` → tier-(b) `PendingNextSceneChange`) stands.
- **Adjacent defect found — loose-override subdir flatten (Phase-8 Open Q2):** an `.stf` opened via the raw
  `Open…` file dialog saved **flat** (`loose\ui_auc.stf`) instead of `loose\string\en\ui_auc.stf`; the
  TRE-Browser "Open in String-table Editor" hand-off (which carries the logical path) is the path that
  preserves the subdir. Routed to the Phase-15 next `--gaps` round.

**To close this todo in a future session:** (1) re-enable the priority-27 `…\loose` searchPath (accept the
shadow for a scoped session) **or** stand up the V2 tier-3 mock-D3D9 harness; (2) save the `.stf` via the
TRE-Browser hand-off (not the raw dialog) so the loose subpath is correct; (3) relaunch; (4) observe
render-on-scene-change vs relog-only and amend the badge only if it over-promises.
