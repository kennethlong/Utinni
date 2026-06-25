# Provider Request — TreeFile file-enumeration + in-world CUI reflow (advertised client)

**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Date:** 2026-06-24
**Status:** REQUEST — two independent asks, both surfaced by the live GAME-subsystem editor-unlock smoke
on `SwgClient_r.exe`. Neither is a regression in the v4 contract; both are advertised-client gaps.
**Source of truth:** this file in the Utinni repo. Copy into `swg-client-v2/.planning/handoff/`
as `2026-06-24-utinni-treefile-enum-and-inworld-reflow.md`.

---

## 0. Context — where the editor unlock is

The GAME subsystem is now unlocked + live-verified on the advertised DX11 client: it boots, loads into
the world, and is stable (Utinni `11f7805`, UtinniPlugins `0a793d8`). Two things block actually *using*
the editor in-world, and both are on your side:

1. **The Jawa Toolbox scene/terrain/object lists are empty** — Utinni's `Repository` (its file index)
   has no filenames to enumerate (Ask A).
2. **The SWG CUI is clipped in-world** — minimap cut off at the top, toolbar + chat cut off at the
   bottom — after the engine's window-level fullscreen restyle on entering the world (Ask B).

Ask A is a NEW endpoint; Ask B extends your existing RNDR-04 reflow. They are independent — land them in
either order.

---

## Ask A — a flat TreeFile file-enumeration endpoint

### The problem
Utinni's `Repository` (UtinniCore `swg/misc/repository.cpp`) indexes **every filename the client knows**
(grouped by top-level dir: `terrain/*.trn`, `snapshot/*.ws`, `object/**/*.iff`, …) to populate the
editors' pickers. On SWGEmu it built this by **intercepting the engine's instance**
`searchTree(this, priority, treeFilename)` and reading the loaded `SearchTree` object's TOC filename
table (`this+0x14` count, `this+0x18` buffer). That hook can't work on your client:

- Your v4 `treeFile::searchTree → &TreeFile::addSearchTree` is a **static** `__cdecl(fileName, priority)`
  that *registers* a tree — it has no `this` and never exposes the loaded TOC.
- The public `TreeFile` API (verified in `sharedFile/.../TreeFile.h`) has **no flat file enumeration** —
  only per-file (`exists`/`getFileSize`/`open`/`find`) and search-**path** enumeration
  (`getNumberOfSearchPaths`/`getSearchPath`). The per-tree `SearchTree`/`SearchTOC` nodes that hold the
  TOC filenames are **private** (`TreeFile::SearchNode` and subclasses), so Utinni cannot walk them.

So the file list is genuinely unreachable from outside `TreeFile` on your client. We need you to expose it.

### The ask
Add a `TreeFile` static that walks `ms_searchNodes` and yields **every filename across all registered
search trees/TOCs** (the union the client can `open()`), then advertise it. Preferred shape — a callback,
so no giant `std::vector` crosses the DLL boundary and Utinni controls allocation:

```cpp
// Calls cb once per filename across all SearchTree/SearchTOC nodes (engine-relative paths,
// e.g. "terrain/tatooine.trn"). Order/dedup unimportant -- Utinni groups + dedups its side.
static void TreeFile::enumerateFiles(void (*cb)(const char* fileName, void* context), void* context);
```

Contract row (suggested name; advertise the real `&fn`):
```cpp
ENGINE_HOOKPOINT(treeFile, enumerateFiles)   // -> &TreeFile::enumerateFiles
```
Utinni-side typedef to match: `void(__cdecl*)(void(__cdecl* cb)(const char*, void*), void* ctx)`.

**Alternative if a callback is awkward:** a count + index pair mirroring your existing
`getNumberOfSearchPaths`/`getSearchPath` —
`static int TreeFile::getFileCount();` + `static const char* TreeFile::getFileName(int index);`
(Utinni loops). Either works; the callback is one round-trip vs. N. Your call.

### How Utinni consumes it (so you can sanity-check the shape)
Once at `Game::install` (our `hkInstall`), on the advertised client Utinni calls `enumerateFiles` and
pushes each name into the `Repository`, replacing the dead searchTree-harvest. The editors then populate
exactly as on SWGEmu. (Consumer scaffolding for this is going in now, gated on the endpoint resolving —
null/absent endpoint just leaves the lists empty, as today.)

**Performance note:** the union is large (tens of thousands of files). The callback form lets Utinni
reserve + move-append without an intermediate engine-side container; please don't pre-build a returned
vector. If `enumerateFiles` is non-trivial to assemble, building it lazily once and caching is fine —
Utinni calls it a single time.

---

## Ask B — make the RNDR-04 reflow fire on the in-world fullscreen restyle

### The problem
Your RNDR-04 embed-resize fix (`e2d5cf1c7`) re-lays-out the CUI from `CuiManager::render()` by comparing
`getCurrentRenderTargetWidth/Height` against the last seen size and calling `CuiManager::setSize`, plus a
`Graphics::beginScene()` poll that drives `Graphics::resize` when the window client rect != back-buffer.
That fixed **startup + maximize**. It does **not** cover **entering the world**:

- On entering the world the SWG client does a **window-level fullscreen restyle** (it mutates its own
  `GWL_STYLE`/`GWLP_HWNDPARENT` to fullscreen — confirmed in Utinni's log:
  `PanelGame.ReassertEmbed: window-level fullscreen restyle detected`). Utinni's watchdog re-strips the
  popup style and re-sizes the window back to the editor panel (window-side only, no device Reset).
- **Result (screenshot attached in the Utinni issue):** the 3D world renders + scales correctly, but the
  **CUI canvas stays at the fullscreen resolution** — minimap clipped off the top, toolbar + chat clipped
  off the bottom. So the CUI `setSize` reflow did NOT track back down to the embed size after the restyle.

The 3D RT rescales (your `Graphics::resize` path runs), but the **CUI layout size is stale** — the same
class of "nothing re-lays-out the UI root on resize" you fixed for maximize, except this trigger
(fullscreen restyle → window re-size) doesn't drive your `CuiManager::render` size-compare back to the
embed dimensions.

### The ask
Ensure the CUI reflow tracks the **embed/window client size**, not the transient fullscreen resolution,
across the in-world restyle. Concretely (you know this code; these are directions, not a patch):

- Have `CuiManager::render()`'s size-compare key off the **back-buffer / window-client** size that the
  embed actually presents at (the same dims your `Graphics::beginScene` poll already computes), so when
  the window is re-sized back to the panel the CUI re-lays-out to the panel — not to the fullscreen res it
  briefly switched to.
- Verify the `beginScene` poll fallback fires through the **fullscreen-restyle** transition (during the
  restyle the back-buffer may momentarily match the fullscreen res; confirm the poll re-compares once the
  watchdog has re-sized the window back to the panel — it may need to not latch the fullscreen dims as the
  "settled" size).
- If a single authoritative "embed client size" is cleaner, Utinni can **push** it: we already detect the
  restyle in the watchdog and know the panel size. If you advertise a `Graphics`/`CuiManager`
  "set embed size" entry (or we drive the already-advertised `Graphics::resize` + `CuiManager::setSize`
  from the watchdog), we'll call it on every re-assert. Tell us which you prefer — provider-detected
  (your reflow tracks the right size) or consumer-driven (we call setSize/resize on re-assert). Consumer-
  driven needs `cuiManager::setSize` + `graphics::resize` to resolve on this client (they're advertised;
  we'll confirm coverage on our side).

### Why it's yours-first
The reflow logic + the "what size is authoritative" decision live in your `CuiManager::render` /
`Graphics` resize path (RNDR-04). Utinni can drive `setSize`/`resize` from the watchdog if you'd rather
keep the consumer in control — but the size-tracking-through-restyle correctness is the provider call.

---

## Coordination
- Both asks are **additive** (a new TreeFile endpoint; a reflow-trigger refinement). Ask A is a NAME ADD →
  bump `ENGINE_HOOKPOINTS_VERSION` and re-sync the `.h`/`.inc` byte-identical (sha256-verify) as usual.
  Ask B is behavior-only, no contract change (unless you pick the "advertise a set-embed-size entry"
  option, which would be a NAME ADD).
- Live verification is the maintainer's inject-smoke on `SwgClient_r.exe` (Ask A: scene list populates;
  Ask B: in-world minimap/toolbar/chat are no longer clipped).
- Consumer side in parallel: Utinni is scaffolding the Repository-population call (Ask A) now, gated on
  the endpoint resolving, and will confirm `cuiManager::setSize`/`graphics::resize` resolution for the
  consumer-driven Ask-B option.

## Pointers
- Repository + harvest: Utinni `UtinniCore/swg/misc/repository.cpp`, `swg/misc/tree_file.cpp`
  (`hkSearchTree`, `getAllFilenames`).
- Provider TreeFile: `src/engine/shared/library/sharedFile/src/shared/TreeFile.h` (no flat enumeration).
- Provider RNDR-04: commit `e2d5cf1c7` (`CuiManager::render` reflow + `Graphics::beginScene` poll).
- The GAME-unlock handoff this follows: `24-PROVIDER-REQUEST-misc-input-editor-unlock.md` + its handback.
