# Pinned Source Provenance — Utinni `tools/` Lift

The entire `tools/` tree is a **lift-and-shift** of the SOE build CLIs and their
dependency closure from the sibling `swg-client-v2` reference corpus. It is
Utinni-owned and standalone: nothing under `tools/` `#include`s or
`ProjectReference`s back into the live `swg-client-v2` working tree (D-01).

## Pinned lift point

| Field | Value |
|-------|-------|
| Repo | `D:/Code/swg-client-v2` (Restoration reference corpus) |
| Branch (at lift time) | `koogie-msvc-cpp20-base` |
| **Pinned SHA** | `5fce7bb8368c86d5a2330a0173d1541866786196` |
| Lift method | `git archive 5fce7bb8 <paths> \| tar -x` — byte-exact to the commit |
| Provenance | [VERIFIED: `git rev-parse`, 2026-06-02] |

The lift point is recorded as an **immutable SHA, never a branch HEAD** (D-03):
`koogie-msvc-cpp20-base` can move under us (upstream force-push, T-12-01); only
the SHA is the integrity anchor. Re-syncing to a newer upstream is a deliberate,
reviewed action — bump this SHA explicitly, never silently track the branch.

## What was lifted (12-01)

- **App:** `TreeFileBuilder` (the cheapest first-green: zlib-only, no Perforce, no PCRE)
- **11 shared libraries** (ProjectReferences / link closure): `sharedCompression`,
  `sharedDebug`, `sharedFile`, `sharedFoundation`, `sharedFoundationTypes`,
  `sharedIoWin`, `sharedMath`, `sharedMemoryManager`, `sharedRandom`,
  `sharedSynchronization`, `sharedThread`
- **`fileInterface`** (`external/ours`, ProjectReferenced)
- **External *compile* closure** (include-only, NOT ProjectReferenced):
  `3rd/library/{zlib, zlib-1.2.3, debugHelp, vtune}`,
  `ours/library/{archive, localization, localizationArchive, unicode, unicodeArchive}`

### zlib version note (Pitfall 3, corrected)

Two zlib trees are intentionally present and serve different roles:

- `external/3rd/library/zlib/` — **zlib 1.1.4**. Provides the *prebuilt* static
  lib `lib/win32/zlib.lib` that `TreeFileBuilder` **links**. This is the
  **byte-exact determinant** of compressed `.tre` output — do NOT silently
  upgrade it (would change output bytes; breaks the 12-03 byte-exact smoke).
- `external/3rd/library/zlib-1.2.3/` — **zlib 1.2.3** source. Provides the
  `zlib.h` header that `sharedCompression/ZlibCompressor.cpp` `#include`s at
  **compile** time (its include path points here). Headers are declarations
  only; the linked 1.1.4 lib still determines runtime compression. This dir is
  required to *compile* sharedCompression — the original 12-01 plan guard
  forbidding it was incorrect and was relaxed by maintainer decision (2026-06-02).

## Divergence watch

`swg-client-v2` carries sibling branches that diverge from this x86/v145 lift:

- **`*x64bit-Upgrade*`** — an x64 retarget. **Collides with CON-P-02** (Utinni
  tools are x86/Win32 only — the byte-exact `.tre`/template outputs and the
  injected-DLL ABI are 32-bit). Do NOT re-sync onto an x64 branch without
  re-deciding CON-P-02.
- **`*MSVC-CPP20-Upgrade*`** — the C++20/v145 conformance work this lift already
  rides on. Future re-syncs should track this lineage, not the x64 one.

Re-sync = bump the SHA above deliberately, re-run the 12-03 byte-exact smoke,
and re-confirm the x86-only constraint still holds.
