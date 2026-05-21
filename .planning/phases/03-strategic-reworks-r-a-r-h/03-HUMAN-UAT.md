---
status: partial
phase: 03-strategic-reworks-r-a-r-h
source: [03-VERIFICATION.md]
started: 2026-05-21T20:54:00Z
updated: 2026-05-21T20:54:00Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. Live SWG TJT smoke — post-R-B symmetric plugin lifecycle

**Context:** Phase 3 Plan 03-02 extended the `UTINNI_PLUGIN` macro to require both `createPlugin` AND `destroyPlugin` exports; PluginManager now (a) calls `destroyPlugin` on shutdown so the plugin frees its own allocation in its own CRT, (b) refuses-to-load any plugin missing the `destroyPlugin` export (CR-02 fix, commit `bc2b4ad`). The Jawa Toolbox was updated in the paired cross-repo commit `kennethlong/UtinniPlugins@73b1856` to export the symmetric `destroyPlugin`.

This UAT confirms the loader + lifecycle work end-to-end in a live SWG injection — which can't be exercised from xUnit / CI.

**Steps:**
1. Build the latest `UtinniPlugins` solution Release|x86 (or copy your existing `bin/Release/Plugins/TheJawaToolbox/TheJawaToolbox.dll` over the deployed plugin).
2. Launch SWG with Utinni injection + TJT loaded (existing launcher flow).
3. Verify TJT panels open from the editor as before (object browser, command parser, etc. — whatever subpanels you normally exercise).
4. Exercise at least one TJT subpanel through a typical action (e.g., open an object, click a command).
5. Cleanly quit SWG via `/quit` or window-close.
6. Inspect `utinni.log` for a clean shutdown sequence:
   - No `LoadLibrary` errors at startup (R-B's GetLastError path)
   - No `init() threw` warnings (per-plugin try/catch isolation worked)
   - Shutdown completes without an `SWGEmu.exe-stage.*.{txt,mdmp}` dump
7. **Optionally:** if you can drop a `Utinni.LegacyPlugin.dll` into the plugins dir alongside TJT, confirm Utinni logs that the legacy plugin is **rejected** at load (CR-02 refuse-to-load policy) while TJT still loads normally. Skip this if it's annoying to set up — the unit test already covers it.

expected: TJT loads and behaves identically to before Phase 3; clean exit; log shows no errors/warnings related to plugin lifecycle. If the optional legacy-rejection check is run, Utinni log shows the legacy plugin was refused with a clear `log::error` line.
result: [pending]

## Summary

total: 1
passed: 0
issues: 0
pending: 1
skipped: 0
blocked: 0

## Gaps
