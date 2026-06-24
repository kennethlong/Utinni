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
