# Phase 24 — Session Handoff (2026-07-01): inspector affordances + v2.1 milestone closeout + model-alias cleanup

Resume pointer after a session that (1) shipped more advertised-client inspector affordances, (2) formally
closed out the **v2.1 milestone** (tag + audit + reconciliations), and (3) settled the GSD/phone-a-friend
model-alias policy. Everything is committed + pushed on both repos; working trees clean. Read top-to-bottom.

**Live pointers:**
- **This doc** — session recap + the next-move menu (§4).
- **Running arc ledger:** `project_phase24_editor_unlock_inflight` memory.
- **Provider outstanding:** `24-PROVIDER-HANDOFF-outstanding-editor-unlock.md`.
- **Prior handoff:** `24-SESSION-HANDOFF-2026-06-30-freecam-mousefly.md`.
- **Milestone audit:** `.planning/milestones/v2.1-MILESTONE-AUDIT.md`.

---

## 0. STATUS in one paragraph

The advertised NGE client's Misc-panel **selected-object inspector** grew to a complete advertised-safe
getter set (Template / Appearance / Portal / Client Data / Type / Active / Network ID / Cell / Position /
Yaw) plus a new **"Inspect Player"** button (runs the same readout on `Game.PlayerCreatureObject`) — all
smoke-passed. Separately, the **v2.1 milestone is now formally closed**: tagged `v2.1` at `194481c`, audited
(**PASS — tech-debt**, 22/22 requirements delivered + verified, 5/5 cross-phase flows wired, Nyquist 8/8),
with all documentation-hygiene reconciliations applied. Finally, the GSD + phone-a-friend model policy was
settled: **use the bare `sonnet`/`opus` aliases (always latest), no dated pins** — an initial "pin to
Sonnet 5" attempt was reverted. SWGEmu byte-unchanged throughout (D-00).

---

## 1. What landed this session (committed + pushed)

### Utinni (`master`)
| Commit | What |
|--------|------|
| `e0e14b0` | inspector: +Portal/ClientData/Active native wrappers + Inspect Player (advertised-safe getters); ABI rebless +4 additive |
| `52eb666` | v2.1 bookkeeping: ROADMAP/STATE/MILESTONES reconciled → v2.1 SHIPPED, 24-04 checked |
| `e501d62` | record the `v2.1` tag in MILESTONES + STATE |
| `067ded1` | v2.1 milestone audit → PASS (tech-debt) |
| `61e6ef8` | apply audit §5 reconciliations (REQUIREMENTS header, retro-VERIFICATION 22/24, VALIDATION flips, SUMMARY tags) |
| `5634316` → `7938cb8` | model-alias: pin attempt, then **corrected to bare `sonnet` alias**; Cursor left as-is |

**Tag:** `v2.1` @ `194481c` (annotated, pushed) — the milestone-close commit, before the post-v2.1 follow-on.

### UtinniPlugins (`master`)
| Commit | What |
|--------|------|
| `0681ba5` | MiscPanel: richer inspector readout + Inspect Player button (paired with Utinni `e0e14b0`) |

---

## 2. v2.1 milestone — CLOSED

- **Tag** `v2.1` @ `194481c`. **Audit** = PASS (tech-debt): 22/22 delivered + verified, 0 unsatisfied, all 5
  cross-phase flows wired, ABI/MEF-compose gate proven, Nyquist 8/8.
- **Key audit finding (resolved):** `24-04-SUMMARY` had frozen the DX11 acceptance at its 06-22 *deferred*
  state, but DX11 was actually **resolved 2026-06-23 — at/before the tag** → those live legs (RNDR-02/04,
  EPA-03 DX11) are closed at the tagged milestone. Reconciled everywhere.
- **Retro-VERIFICATION.md generated** for Phases 22 & 24 (were missing). All phases now have one.
- **Residual (non-blocking, in the audit):** clean the stale root `bin/Release/utinni-cli.exe`; guard the 2
  latent ABI-trap slots (`worldSnapshot::addObject`, `treeFile::open`) before any future consumer call.

## 3. Model-alias policy (settled)

Use the **bare aliases** — `sonnet`/`opus` resolve to the latest model. No dated pins anywhere.
- Phone-a-friend crew (Agent tool): spawn with model `sonnet` (latest) / `opus`. **Cursor untouched.**
- GSD: alias-passthrough (**`resolve_model_ids` stays `false`** — enabling it maps to the catalog's dated
  ids, pinning sonnet AND downgrading opus to 4-7 vs the 4.8 in use). GSD catalog reverted to pristine.
- Captured in the `reference_gsd_model_aliases` memory + `CLAUDE.md` "Phone a friend".

---

## 4. NEXT-MOVE MENU (resume here)

Two broad directions — pick per appetite:

### A. Continue the advertised-client editor-unlock arc (the active follow-on)
1. **More getter-shaped affordances** — the safe §5 pattern (pure getters, no callback blast radius). The
   inspector set is nearly tapped; a **camera readout** (`game::getCamera`, advertised) is one clean option.
2. **Parked, harder (multi-part, not one row):**
   - **sysmsg** — needs vtable-resolving the `ChatSystemMessage` Listener OR an advertised
     `sendFakeSystemMessage` inject row (the 1-arg static was a wrong-`&` → reverted).
   - **target-change** — needs the `WorldSnapshotImpl.OnTarget` subscriber chain made advertised-safe (or
     gated off on advertised) before un-gating `creatureObject::setTarget` — its dispatch wakes unadvertised
     RVAs (the GroundScene blast-radius lesson).
3. **Provider buckets C/D/E/F** (virtual vtable rows, mid-function toggles, WS-5 scene-ready, crash-log) —
   `24-PROVIDER-HANDOFF-outstanding-editor-unlock.md`; lower priority.

### B. Cut the next milestone (v2.2+)
Backlog candidates (ROADMAP §Backlog), needs a scope decision:
- **999.8 — remaining Wave-2 editors:** Animation → Shaders/Textures → Sound → UI + Effects family
  (Lightning/Swoosh). The natural "more editors" path.
- **999.10 — Utinni installer / one-click onboarding** (the "maintainer tool → distributable tool" gate).
- **999.9 — Wave-3 plugins** (Mod Manager, Packager, Community Hub, Asset Diff).
- Smaller: 999.3 (TRE version-history), 999.4 (native CppSharp v145), 999.6 (Maya-write re-eval), 999.7 (x64).

> To start B cleanly: `/gsd:new-milestone` (or `/gsd:review-backlog` to promote a 999.x item first).

---

## 5. OPERATIONAL FACTS (unchanged, carried forward)

- **Build (native):** `MSBuild Utinni.sln -t:UtinniCore -p:Configuration=Release -p:Platform=x86 -m -nologo
  -v:minimal -nodeReuse:false`. **Always `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs`** after —
  but only AFTER any ABI test runs against the fresh cs.
- **TJT build:** `MSBuild "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolbox.sln" …` (build UtinniCoreDotNet first).
- **⚠️ DLL-lock:** a running injected `SwgClient_r.exe` locks `bin/Release/UtinniCore.dll` + `TheJawaToolboxDotNet.dll` — close the client before rebuilding those.
- **ABI rebless dance (when a `utinni::` public method changes):** build (regen) → run `AbiSurfaceTests` on
  the fresh cs → temp env-gated `Rebless_WhenEnvSet` `[Fact]` with `UTINNI_REBLESS=1` (writes the SOURCE
  baseline directly) → re-freeze `Fixtures/FrozenPlugin/TheJawaToolboxDotNet.dll` → revert temp fact → checkout cs.
- **Headless gates:** endpoints Catch2 `UtinniCore.Tests.exe "[endpoints]"`; clang-format-20 (x64 binary);
  RVA audit `scripts/audit-advertised-rva-safety.ps1 -SwgRoot UtinniCore/swg`; managed `dotnet test --no-build`.
- **Live smoke = maintainer only** (advertised client from `D:\Code\swg-client-v2\stage\`; .tre/.toc from `D:\Code\SWGSource Client v3.0\`).
- **Cross-AI crew:** Codex (`codex exec --skip-git-repo-check -`) reliable; Agent-tool `sonnet`/`opus` reliable; cursor-agent deprioritized (flaked on prompt delivery).
