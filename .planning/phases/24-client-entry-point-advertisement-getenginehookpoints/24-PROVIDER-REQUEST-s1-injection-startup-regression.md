# Provider Request — §1 build renders fine STANDALONE but fails to render UNDER INJECTION

**From:** Utinni (consumer) · **To:** swg-client-v2 (provider)
**Date:** 2026-06-23 · **Status:** QUEUED — relay to the provider.

## Key facts (maintainer-confirmed)

- The 16:35 build (§1 resize + 38-07/08/09) **boots + renders fine when run directly (standalone).**
- **Under Utinni injection it does NOT render** — both recent re-smokes failed to reach first render
  (no DX11 overlay-install line), produced **no crash dump**, and were **intermittent** (one died earlier
  than the other; neither rendered).
- The **pre-§1** client rendered fine under the **same** Utinni build (reached in-game earlier today). So
  §1 is what regressed the injected path.
- Utinni's own DIAG instrumentation is ruled out (it's in the present hook, which the failing runs never
  reach).

## Mechanism (hypothesis, well-supported by standalone-OK / injected-fails)

Under injection, Utinni reparents the SWG window into the TJT panel and `SetWindowPos`-resizes it to the
panel **during startup** → delivers `WM_SIZE` to the client **early, before its first frame**. §1 turned
that `WM_SIZE` into real work:

`Os::WindowProc` `WM_SIZE` → `displayModeChanged` → `Graphics::resize` (`rasterMajor==11`) → `resize_impl`
→ device-lost/restored fan-out (`PostProcessingEffectsManager`/`Bloom`/`BinkVideo`) + backbuffer resize.

Fired before the device / screen RTs / callback targets are fully constructed → crash or hang → no
render. **Standalone never receives that early embed-driven resize, so it's fine.** The old (pre-§1)
deferred `checkDisplayMode` path handled the same early `WM_SIZE` trivially, which is why the pre-§1
client rendered under injection.

## Deliverable

1. **A/B confirm:** rebuild with §1 **reverted** (keep 38-07/08/09); maintainer re-smokes injected
   startup. Renders reliably → §1 confirmed.
2. **Make §1's resize path safe when invoked early** (before the first successful frame): guard so
   `Graphics::resize` / `resize_impl` / the device-restored fan-out is a no-op-or-deferred until the
   device + all 3 device-restored callback targets + their screen RTs exist. Practically: **ignore/queue
   any resize that arrives during startup before the first present, then apply the latest size once
   render state is ready.** Verify whether `displayModeChanged` is even supposed to run on resizes that
   arrive pre-first-frame.
3. **Get an artifact next time:** one-shot log at the top of the resize path
   (`displayModeChanged w=.. h=.. rasterMajor=.. firstSet=.. deviceReady=..`) + ensure the crash-dump
   handler is installed **before** graphics init so an early crash actually dumps.
4. Rebuild + restage with matching PDBs; handback with what to verify (injected startup renders AND the
   embed-resize crop from §1 is fixed).

## Parallel Utinni-side mitigation (consumer will evaluate)

Utinni can **defer the embed reparent/`SetWindowPos`-resize until after the client's first present**, so
the fragile startup window isn't hit by an embed-driven `WM_SIZE`. This is a mitigation, not the root fix
— the provider-side robustness (item 2) is still wanted so the client survives an early resize from any
embedder. Consumer will smoke this independently if useful.

## Constraints

- Do NOT write to `D:/Code/Utinni`. No Utinni contract change expected (pure renderer/startup).
- Cross-check the root cause with the crew (codex/cursor) if useful. Live re-smoke is maintainer-only.

---

## MITIGATION RESULT (2026-06-23 20:02) — reparent-resize RULED OUT; client dies before first present

Utinni shipped the consumer mitigation (commit 8df6f20): the advertised-client embed reparent +
its SetWindowPos-resize is now deferred until the client's first present. **Re-smoke result: the
client STILL fails to render under injection** — TJT (hosted in the injected client process)
appeared for ~1-2s, then the client process died before any first present (no DX11 overlay-install,
no DIAG, no dump, no fresh client output; died EARLIER than the 18:35 run that reached char-list).

**This is a sharper finding:** with the reparent (and its resize) deferred, **nothing of Utinni's
embed resize ran** — yet the client still died before first present. So **the embed-reparent-resize
is NOT the trigger.** The §1 client dies during its OWN early startup under injection, before it
presents — intermittently (one earlier run reached char-list; two later runs died early → a startup
race). Standalone still renders fine; the pre-§1 client rendered fine under the SAME Utinni build.

**Refined hypothesis:** §1's `displayModeChanged`→`Graphics::resize`→`resize_impl`→device-restored
fan-out fires during the client's **own initial display-mode set at startup** (not from Utinni's
reparent), and under injection that races with Utinni's overlay-install (`ImGui_ImplDX11_Init` on the
borrowed device/context) and/or the RENDER-group detours → intermittent early death before first
present. The embed-resize was a red herring; the regression is in the startup display-mode/resize path.

**This makes the A/B revert (item 1) the critical isolation, and items 3 (startup log + dump handler
before graphics init) essential** — the client dies before any artifact is written, so we are blind
without provider-side startup logging. Specifically please confirm: does §1's `displayModeChanged`
run on the FIRST/initial display-mode set during boot (before the first present)? If so, gate it to
no-op until after the first successful present (item 2), independent of who triggers the resize.

The Utinni mitigation is harmless + retained (it correctly defers the reparent and will matter once
startup is fixed); it did not cause this and has now served as the diagnostic that exonerates the
embed-resize.

---

## ROOT CAUSE FOUND — Utinni-side (NOT §1). Fixed consumer-side (commit d2040ca, 2026-06-23 21:xx)

The injected-startup crash was a **Utinni bug, not the provider's §1**. A fresh re-smoke (this time on
the matching 20:20 build, so it symbolized cleanly) produced `VEH FATAL code=0xC0000096`
(privileged instruction) at `SwgClient_r.exe` rva `0x182D61` — the **CuiStringIds static-init region** —
and **no `displayModeChanged DEFERRED` WARNING fired**, so the client died BEFORE ever reaching the
§1 resize path the provider gated.

Cause: Utinni's `createDetours` RENDER group unconditionally ran 5 detours that target **hardcoded
SWGEmu absolute addresses** and are **NOT in the advertised .inc** (so the resolver never overwrites
them): shaderPrimitiveSorter `0x00773E39`, renderWorld `0x00766DE0`, postProcessing/bloom `0x0064B500`,
ParticleEffectAppearance, skeletalAppearance. On the relocated advertised client all three live-symbolize
to the §1 client's **CuiStringIds** dynamic initializers (committed+executable), so `installable()`
wrongly passed and `Detour::Create` wrote JMPs into CuiStringIds code → corruption → the 0xC0000096
during CuiStringIds dynamic-init. Same failure class as the earlier `getSwgWndProcExport` stale-RVA bug;
the §1 build's new layout is what shifted those stale addresses onto committed code (hence intermittent /
ASLR-dependent, and why the pre-§1 client sometimes survived).

**Fix (Utinni, d2040ca):** gate those 5 non-advertised RENDER detours on `!advertised`. `graphics::*` are
advertised (resolver overwrites the literals) so the overlay kickoff stays and is safe.

**Provider takeaway:** your §1 resize-defer fix is correct and was simply never reached — the Utinni
corruption killed the client first. After this consumer fix the client should reach the render path and
your `displayModeChanged ... DEFERRED` → `applying deferred gl11 resize` WARNINGs should finally appear.
No provider action needed for THIS crash; §1 stays.
