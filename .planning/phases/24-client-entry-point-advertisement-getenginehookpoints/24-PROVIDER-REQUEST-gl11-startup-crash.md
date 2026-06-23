# Provider Request — gl11 (DX11) startup crash on the advertised client (likely §1 resize regression)

**From:** Utinni (consumer) · **To:** swg-client-v2 (provider)
**Date:** 2026-06-23 · **Status:** QUEUED handback-request — relay to the provider.
**Artifact:** `D:\Code\swg-client-v2\stage\release-gl11-crash.dmp` (≈562 MB full dump, crash time 19:01).

---

## Context

After the §1 DX11 resize fix was rebuilt + restaged (16:35), the maintainer re-smoked the advertised
client (`SwgClient_r.exe` + `gl11_r.dll`, injected by Utinni). It **crashed ~6 seconds after start** —
before reaching anything useful ("hiccup on start"). A full minidump was written (above).

## Reliable crash facts

Read from the dump's registers/memory by the Utinni dev (cdb x86) — NOT from symbols (see the PDB
caveat). These are build-independent anchors:

- AV `0xC0000005` in **`gl11_r.dll`**, ~6 s after process start (process uptime 0:06).
- Faulting instruction: `mov edi, dword ptr [eax+44h]` with **`eax = 0x00000001`** → deref of `0x45` →
  AV. An object pointer that is `1` (a flag/bool/uninitialized value used where a real `this`/struct
  pointer is expected); it reads the member at `+0x44`.
- Fault address: **`gl11_r.dll + 0x28f60`** (module base `0x60fa0000`, `eip 0x60fc8f60`). Immediate
  caller: **`gl11_r.dll + 0x27611`**. The frame above is in `SwgClient_r.exe`. Stack-unwind info was
  absent (Release/FPO), so only those two gl11 frames are solid.
- Crash is in the provider's DLL; **no Utinni code in the stack.**

## PDB caveat — symbolize with the matching build

On the maintainer's machine, cdb reports BOTH `stage\gl11_r.pdb` and `stage\SwgClient_r.pdb` are
**mismatched** to the binaries in the dump (`Loaded mismatched pdb … unmatched`), so every symbol name
it printed is garbage and was NOT relayed. Symbolize `release-gl11-crash.dmp` with the **exact PDBs from
the §1 build** that produced the crashing `gl11_r.dll` (`src\compile\win32\Direct3d11\Release\gl11_r.pdb`).
Use the anchors above to confirm you are on the right function. Also fix whatever caused the staged
binary/PDB to drift, so the next dump symbolizes cleanly.

## Leading hypothesis — §1 resize regression at startup

§1 is the only recent gl11 change and it touches the startup display-mode/resize path
(`GraphicsNamespace::displayModeChanged` → `Graphics::resize` for `rasterMajor==11`; the new
`Direct3d11` `resize_impl`; the device-lost/restored callback fan-out to `PostProcessingEffectsManager` /
`Bloom` / `BinkVideo`). A 6-second-in AV on `obj->[+0x44]` with `obj==1` is consistent with the
resize/device-restored path firing **during startup before those callback targets (or their screen RTs)
are constructed**, or a callback/slot holding `1`. Check: does the new resize/`displayModeChanged` path
run on the *initial* display-mode set during startup, and are all 3 registered callbacks (and their
screen RTs) valid at that point? The §1 note itself said "revert this commit if a boot/render regresses,
one change per smoke" — this is that case.

## Deliverable

1. Symbolize the dump with matching PDBs → name the exact function + line at `gl11_r+0x28f60` and
   `+0x27611`.
2. Root-cause the `eax=1` deref — do NOT mask; find why the pointer is `1`.
3. Fix it (guard/ordering so the resize/device-restored path is safe during startup, or gate it until
   the device + callback targets exist). Mirror the tested D3D9 ordering if applicable.
4. Rebuild + restage `SwgClient_r.exe` + `gl11_r.dll` (Release/Win32) with matching PDBs; confirm 0
   unresolved externals; record a handback.

## Constraints

- Do **NOT** write to `D:/Code/Utinni`. No Utinni contract change expected (pure renderer fix).
- Cross-check the root cause with the crew (codex/cursor) if useful, as with the prior crashes.
- The live re-smoke is maintainer-only — hand back with the fix staged + what to verify.
