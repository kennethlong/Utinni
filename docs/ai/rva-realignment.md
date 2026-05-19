# RVA Realignment: Porting Utinni to a New SWGEmu Build

> Research compiled 2026-05-18 after the Phase 02.1 live UAT confirmed that
> Utinni's hardcoded native addresses don't match SWGEmu Launchpad's current
> output (build `0.0.119.798`). This doc captures the tools, techniques, and
> recommended workflow for re-calibrating Utinni against the user's specific
> client.
>
> Audience: anyone porting Utinni to a new SWGEmu drop. Adjacent to
> `internals.md` (the canonical RVA table that needs updating).

## Why this matters

Utinni's `swg/<subsystem>/<subsystem>.cpp::detour()` functions install hooks
at hardcoded addresses inside `SwgClient_r.exe`. The full table lives in
[`internals.md`](internals.md) — roughly 40-50 RVAs across 15-20 subsystems.

These addresses were calibrated against the SWG client `ptklatt/Utinni` v1.0
(Oct 30, 2020) was developed against. The user's SWGEmu Launchpad shipped a
newer build (`0.0.119.798`, see `D:\SWGEmu-Client\SWGEmu\SWGEmu.exe`
VersionInfo). The addresses don't match. The injection succeeds (managed-side
fine), but the `D3D9` detours don't fire (no `hkPresent`, no ImGui overlay),
`Game::install` likely doesn't fire either, and SWG sits in a half-hooked
state where it never reaches the main loop.

**The user-facing symptom:** editor host loads with TJT visible, SWG client
window is solid black, TJT panels are grayed (`Game.Repository` empty
because `hkInstall` never fired to populate it).

**The fix is multi-hour but straightforward:** find the new addresses,
update the detour table.

## The realistic landscape

This drift is **structural** for Utinni's current design — every SWGEmu
client rebuild can shift addresses, and SWGEmu has rebuilt many times since
2020. Three architectural responses, increasing in scope:

1. **One-time recalibration** — find the new addresses for your binary,
   patch `internals.md` and every `detour()` call. Brittle: next SWGEmu
   rebuild breaks you again. **~2-4 hours of work for one client.**
2. **Pattern scanning** — replace hardcoded RVAs with byte-pattern scans
   (already done for some sites; `memory::findPattern` exists). Patterns
   are more resilient than fixed addresses. **~1-2 days work; lasts across
   most rebuilds.**
3. **Externalized RVA manifest** — a per-build `rvas.json` (or per-version
   header) loaded at startup. Utinni picks the right manifest based on
   client build hash. **~3-5 days; truly future-proof; this is what the
   ROADMAP's Phase 03 R-C aspires to.**

For unblocking your live testing tonight/tomorrow: **option 1** is fastest.
For the V1 framework's long-term maintainability: **option 2** during
Phase 03 R-C is the right pick. **Option 3** is V2 territory.

## Tool stack

### Required (free)

- **Ghidra** — NSA's open-source disassembler/decompiler. Modern, actively
  maintained, free forever. Latest: 11.x (2026). Download from
  https://ghidra-sre.org/ . Java-based, runs anywhere.
- **BinDiff** — Zynamics tool (Google-owned, free). Compares two binaries
  function-by-function, identifies which functions moved/renamed/changed.
  Download from https://www.zynamics.com/bindiff.html . Works with Ghidra
  via the [BinDiffHelper](https://github.com/ubfx/BinDiffHelper) plugin.
- **`dumpbin`** — Ships with Visual Studio. Already on your machine.
  Quick PE-format inspection (exports, imports, headers).
- **PE-bear** — Free PE inspector with hex/disassembly view; useful for
  quick checks without spinning up Ghidra. https://github.com/hasherezade/pe-bear

### Strongly recommended

- **x32dbg** — Free runtime debugger. Attach to a running SWGEmu.exe and
  walk the call stack, set breakpoints, step through. Invaluable for
  verifying that a candidate RVA is the right function. https://x64dbg.com/
- **IDA Free 9.x** — Optional alternative to Ghidra. Free version is
  fine for SWG-scale binaries. https://hex-rays.com/ida-free/ . Diaphora
  (open-source IDA plugin) is a BinDiff alternative if you prefer
  IDA over Ghidra. https://github.com/joxeankoret/diaphora

### Optional

- **Cheat Engine** — Surprisingly useful for finding addresses by runtime
  memory scanning when you don't have a known-good reference binary.
  https://cheatengine.org/
- **SigScan** — Library/tool for byte-pattern scans. Pattern strings are
  IDA-style ("`48 8B ?? ?? 48 89`" etc.). Utinni already has
  `utility/memory::findPattern` which is the same idea — what you'd
  use long-term to replace hardcoded RVAs.
  https://github.com/luk1337/SigScan

## The workflow

### Phase A: Gather both binaries

You need TWO `SwgClient_r.exe` files: the new one (your current SWGEmu
Launchpad output) and the **calibration baseline** Utinni was developed
against.

The current binary is yours: `D:\SWGEmu-Client\SWGEmu\SWGEmu.exe`
(Launchpad-renamed; `OriginalFilename=SwgClient_r.exe`).

The calibration baseline is **the hard part**. Utinni's v1.0 (2020-10-30)
matches some SWGEmu drop from around that time. Sources to try:

- **SWGEmu Discord** (#dev or #archive) — community usually keeps old
  Launchpad outputs for exactly this reason. Ask for "the SwgClient_r.exe
  Utinni v1.0 was built against."
- **ptklatt/Utinni issue tracker** — someone may have posted a hash.
- **Wayback Machine** — snapshots of SWGEmu's CDN; may have older patches.
- **modthegalaxy forums** — community archive.

If you can't get the calibration baseline, you can still do recalibration
via **structural matching** (strings, vtables, imports) — just slower,
more manual. BinDiff is much faster if you have both binaries.

### Phase B: Map the binaries

**With BinDiff (if you have both binaries):**

1. Open both `SwgClient_r.exe` files in Ghidra. Let auto-analysis finish
   on each (15-30 min per binary; SWG is big).
2. Install [BinDiffHelper](https://github.com/ubfx/BinDiffHelper) into
   Ghidra (Plugin Manager).
3. Run BinDiff between the two Ghidra projects. Outputs a table:
   `old_addr → new_addr` for each matched function, with a confidence
   score. Sort by score descending.
4. Export the matches as CSV.

**Without BinDiff (manual matching):**

For each hardcoded RVA in `internals.md`, find the same function in the
new binary by structural cues:

- **Strings**: many functions reference logged strings (e.g.,
  `"Network::cast"`, `"Loading scene"`). In Ghidra: `Search > For Strings`
  in the new binary, then `Find References (Ctrl+Shift+F)` from the
  string to find the function that uses it.
- **Imports**: functions calling Win32/D3D9 APIs (e.g., `CreateWindowEx`,
  `Direct3DCreate9`) are easy to find via the Symbol Tree → Imports.
- **Vtables**: D3D9 vtable pointers are findable by pattern
  (Utinni already does this for `getVtbl()` at runtime — use the same
  pattern to locate in static analysis).
- **Function signatures**: arg count, return type, calling convention.
  Cross-reference with the type hints in `internals.md` and
  `swg/<subsystem>/<subsystem>.h`.

### Phase C: Verify candidate addresses

Before mass-updating Utinni's source, **verify ONE candidate end-to-end**
to make sure your tooling is correct:

1. Pick a representative RVA — say `Network::cast` at `0xAA4900` (old).
2. Find the new address in your binary (call it `N`).
3. Open the new binary in x32dbg. Set breakpoint at `N`. Launch SWG via
   SWGEmu Launchpad (NOT via Utinni). Trigger an action that should hit
   `Network::cast` (in-game NPC interaction, mission, etc.).
4. If the breakpoint fires → `N` is correct. If not → wrong address.

If you can't get into the game easily, use a simpler RVA to verify first
— `setupStartDataInstall` at `0x00A9F970` (old) is one of the FIRST hooks
to fire; it'll hit during launch before any login flow.

### Phase D: Patch Utinni

For each `swg/<subsystem>/<subsystem>.cpp::detour()` and `patch()`:

1. Open the file.
2. Find every `(LPVOID)0xXXXXX` literal.
3. Replace with the corresponding address from your BinDiff output.
4. Also update `internals.md` so the doc table reflects reality.
5. Rebuild Utinni Release|x86.
6. Inject. Verify via `enableInternalUi=true` that ImGui overlay appears
   (proves `hkPresent` is firing).
7. If it does → `Game.Repository` should populate, TJT panels should
   activate, Scene panel dropdown should list `.trn` files.

### Phase E: Long-term pattern-scan migration (Phase 03 R-C territory)

For each RVA, write a pattern that uniquely identifies the function. Tools:

1. In Ghidra, navigate to the function. Look at its prologue bytes.
2. Identify a 16-32 byte sequence that's unique across the binary
   (check `Search > Memory` to confirm uniqueness).
3. Replace `(LPVOID)0xRVA` with a `findPattern("PATTERN", "MASK")` call.

Utinni already uses this pattern for `getVtbl()` (see
`UtinniCore/swg/graphics/directx9.cpp:297-320`). Generalize that approach
to every hardcoded RVA. The result: Utinni adapts automatically to most
SWGEmu rebuilds because byte patterns are more stable than addresses.

This is what Phase 03 R-C ("Single-source RVAs") should aim for if scoped
correctly. The ROADMAP says R-C is about "Hard-coded RVAs that were
duplicated between native and managed are exposed once via `UTINNI_API`
and consumed from a single source" — that's only half the win. Extending
R-C to mean "RVAs are byte-patterns, not addresses" is the durable fix.

## Worked example: finding `Network::cast`

The old RVA is `0xAA4900` (from `network.cpp:44`). The function takes
`(int64_t* networkId, int low, int high)` per `__thiscall` ABI and writes
through `networkId`.

Without BinDiff, here's the trail:

1. **Logged strings**: `Network::cast` isn't a logged string (it's an SWG
   internal function with no `report()` calls). Strike that approach.

2. **Type signature**: `__thiscall` 3-arg function returning `int64_t` is
   common; not distinctive enough alone.

3. **Caller analysis**: `WorldSnapshotReaderWriter::Node::getNodeNetworkId`
   calls it (per assessment.md C-03 entry). If you can find
   `WorldSnapshotReaderWriter` in the new binary (its name might be in
   strings — try `"WorldSnapshot"`), trace its calls — one of them should
   match the `(int64_t*, int, int)` signature.

4. **Pattern matching against old**: dump bytes around `0xAA4900` in the
   old binary (using `dumpbin /disasm` or Ghidra), find a unique 24-byte
   sequence in the prologue, search the new binary for the same sequence.
   Most likely hits the same function despite address change.

5. **Verify**: set breakpoint in x32dbg, launch SWG, log into character,
   interact with anything that resolves a network ID (NPC click, inventory
   item) → breakpoint should fire.

## Time estimate

Realistic ranges for someone with reverse-engineering experience:

- **Option 1 (one-time recalibration, with BinDiff and baseline)**:
  4-8 hours. Most of it is BinDiff setup + Ghidra auto-analysis + manual
  verification of ~50 addresses.
- **Option 1 (one-time, no baseline binary, all manual)**: 1-3 days.
- **Option 2 (pattern-scan migration during Phase 03 R-C)**: 3-5 days.
  This is the right time investment if you plan to keep using Utinni.
- **Option 3 (externalized RVA manifest per build)**: 1-2 weeks. V2 work.

## Recommendation

For **tonight + tomorrow**: don't recalibrate. The framework is V1-complete
on its own terms; the live-scene-rendering gap is real but it's a
maintenance task, not a framework gap. Sleep on it.

For **the next development push**: include "RVA realignment for current
SWGEmu Launchpad" as a Phase 02.2 mini-effort OR fold it into Phase 03 R-C.
Pattern-scan migration (option 2) is the right scope — gives durability
without a full V2 effort.

For **V1 ship**: live-rendering against this user's SWGEmu build is a
nice-to-have, not a V1 success criterion. The framework UAT passed (C-01 +
C-09 live-verified); CI is green; code-review is clean. V1's bug-burn-down
half is done.

## See also

- [`internals.md`](internals.md) — the canonical RVA table that needs
  updating
- [`assessment.md`](assessment.md) — Phase 02 code review with C-NN bug
  catalog and post-Phase-02.1 closure status
- ROADMAP Phase 03 "Strategic reworks" section, particularly **R-C
  Single-source RVAs** — natural home for option 2

## Tools — quick links

| Tool          | Free? | Purpose                                | URL                                                      |
| ------------- | ----- | -------------------------------------- | -------------------------------------------------------- |
| Ghidra        | Y     | Disassembler/decompiler                | https://ghidra-sre.org/                                  |
| BinDiff       | Y     | Binary diffing / function matching     | https://www.zynamics.com/bindiff.html                    |
| BinDiffHelper | Y     | Ghidra plugin for BinDiff integration  | https://github.com/ubfx/BinDiffHelper                    |
| x32dbg        | Y     | Runtime debugger (32-bit)              | https://x64dbg.com/                                      |
| PE-bear       | Y     | PE inspector with hex/disasm           | https://github.com/hasherezade/pe-bear                   |
| dumpbin       | Y     | MS PE inspector (ships with VS)        | (already on your machine)                                |
| IDA Free 9.x  | Y     | Alternative disassembler               | https://hex-rays.com/ida-free/                           |
| Diaphora      | Y     | IDA plugin for binary diffing          | https://github.com/joxeankoret/diaphora                  |
| Ghidriff      | Y     | Pure-Ghidra binary diff engine         | https://github.com/clearbluejar/ghidriff                 |
| SigScan       | Y     | Standalone byte-pattern scanner        | https://github.com/luk1337/SigScan                       |
| Cheat Engine  | Y     | Runtime memory scanning                | https://cheatengine.org/                                 |
