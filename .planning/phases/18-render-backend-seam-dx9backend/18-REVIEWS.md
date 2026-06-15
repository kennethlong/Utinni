---
phase: 18
reviewers: [codex, cursor]
reviewed_at: 2026-06-15
plans_reviewed: [18-01-PLAN.md, 18-02-PLAN.md]
note: claude CLI skipped — review was run from inside Claude Code (self-review excluded for independence)
---

# Cross-AI Plan Review — Phase 18 (Render-Backend Seam + Dx9Backend)

Two independent external reviewers (OpenAI Codex, Cursor agent) reviewed plans 18-01 and 18-02.
Cursor grounded its review in live source reads (`imgui_impl.cpp/h`, `directx9.cpp`,
`NoDeviceResetTests.cpp`, `utinni.cpp`); Codex reviewed against the plan corpus. The two converge
strongly on the same top concerns.

---

## Codex Review

## Overall

The phase direction is sound: a small vtable seam, static Dx9 backend, and source gate are the right tools for this refactor. The biggest issue is that the two plans do not quite compose: Plan 02 depends on a backend `init` path that Plan 01 never defines, and both plans under-specify null/device ordering around first `hkPresent`. Those are high-risk because this code runs inside SWG's render thread and the first-frame ordering is exactly where injected overlays tend to fail.

Risk Assessment: **MEDIUM-HIGH** as written. The seam itself is reasonable, but the setup/init gap and null-backend first-frame path could turn a refactor into a startup crash.

## Plan 18-01

### Summary

Plan 18-01 is strong as a seam-definition wave, but it overstates completeness. It creates dispatch members and a Dx9 wrapper, yet does not define the initialization operation that Plan 18-02 later requires for `ImGui_ImplDX9_Init` and `GetCreationParameters`. It also has a repeated "8 members" wording error while listing 10 virtual functions, which is not fatal technically but is a planning smell for executor confusion.

### Strengths

- Good "wrap, don't rewrite" posture around `directX::` internals, especially lines 21, 149-155.
- Correctly keeps `onPreResize` / `onPostResize` empty for D3D9, lines 22, 125, 151.
- Static singleton and raw vtable dispatch are appropriate for the allocator-sensitive hot path, lines 24, 128, 154, 167.
- Avoids `UTINNI_API` on the seam, line 142 and acceptance criteria line 166, which protects CppSharp/plugin ABI.
- D-07 mock-dispatch test is useful CI coverage for seam mechanics, lines 174-215.

### Concerns

- **HIGH: Missing backend initialization contract.** Plan 02 says the backend does `GetCreationParameters + ImGui_ImplDX9_Init` and is "triggered" from `setup`, but Plan 01 defines no `init`, `initialize`, `windowHandle`, or device-bind method. See Plan 01 lines 137-142 for the interface and 149-155 for the implementation scope. Plan 02 depends on this at lines 117-118 and 145-149. As written, Plan 02 cannot implement the carve without inventing new API surface during Wave 2.

- **HIGH: First-frame null backend is not handled.** Plan 01 exposes `get()` returning nullable `s_active`, line 154, and Plan 02 will call `render_backend::get()->newFrame()`. If `imgui_impl::render()` can execute before `setup()` installs the backend, this becomes a null dereference. The plans do not require an assert, guard, early return, or install-before-render ordering proof.

- **MEDIUM: "8 members" is internally inconsistent.** Lines 20, 124, 137-140 list `newFrame`, `renderDrawData`, `onPreResize`, `onPostResize`, width, height, depth, color, stage get, stage set. That is 10 virtual methods, not 8. The mock test line 188 also says "ALL 8 members" while listing 10 counters. This increases the chance that either stage methods or color accessors get skipped.

- **MEDIUM: A2 amendment may be scope creep.** Adding color texture and stage get/set preserves dev UI, but it expands the roadmap's seam from 6+1 to 10 methods. That may be defensible, but the plan should explicitly say "A2 intentionally changes the seam from 7 to 10 methods" rather than calling it 8.

- **MEDIUM: `renderTargetWidth/Height` ownership is muddled.** The plan says dimensions are "sourced from present-stretch math already living in imgui_impl" at line 25, but implementation routes them to `utinni::Graphics::getCurrentRenderTargetWidth/Height` in `Dx9Backend`, lines 151-152. If Phase 19 expects backend-specific RT dimensions, that is fine; if D-04 means the mapping logic remains in `imgui_impl`, the test should assert behavior, not just forwarding.

- **LOW: Mock-dispatch test proves the accessor pair, not live render routing.** That is acceptable for Wave 1, but wording at lines 26 and 215 is stronger than the proof. It does not prove the "live render path would call them" until Plan 02.

### Suggestions

- Add an explicit initialization API in Plan 01 before execution. Prefer a DX9-tier API, not an imgui-tier API, for example:
  - `IRenderBackend::initialize(HWND hwnd)` if no DX9 type is needed by the interface, or
  - non-interface `initializeDx9Backend(IDirect3DDevice9* device)` callable only from `directx9.cpp`.
- Avoid backend self-sourcing via `directX::getDevice()` unless there is a hard assertion it is non-null. `hkPresent` already has the device pointer; using it in the DX9 tier is more deterministic.
- Rename the seam count everywhere: "10 virtual methods" or "6 roadmap methods + 4 A2 diagnostic methods."
- In the D-07 test, assert `render_backend::get() == nullptr` after restore to catch leaked global state.
- Add a test or static grep that `render_backend.h` does not include `directx9.h`, `depth_texture.h`, or `<d3d9.h>`.

Risk Assessment: **MEDIUM**. Low risk as an additive wave if the missing init API is fixed before Plan 02. High risk if left for Plan 02 to improvise.

## Plan 18-02

### Summary

Plan 18-02 targets the right carve points and explicitly protects the WndProc, Issue #11, RT-space input, and no-Reset behavior. The plan's main weakness is setup ordering: it assumes a backend initialization mechanism and live device state that Plan 01 does not define. The Tests-window block guard is recognized, but its proposed replacement via `sceneDepthTexture() != 0` is not a faithful reproduction if the original guard was `DepthTexture* != nullptr` rather than "depth/color texture is ready."

### Strengths

- Correctly names the six DX9 touch points, lines 109-111 and 145-158.
- Explicitly preserves Issue #11 routing and RT-space `AddMousePosEvent`, lines 162-164 and acceptance line 181.
- Keeps `hkReset` DX9 device-object handling in `directx9.cpp`, lines 164-166 and 180.
- Adds a D-06 structural source gate on concrete symbols instead of bare `D3D9` / `DX9`, lines 204-211 and 225.
- Includes a blocking maintainer live-smoke, lines 233-239 and 291, which is necessary for RNDR-01.

### Concerns

- **HIGH: Plan 02 depends on an undefined backend init.** Lines 145-149 say setup should "trigger its init," but no such method exists in Plan 01. This is the top execution hazard.

- **HIGH: `hkPresent -> setup(HWND)` ordering is under-specified.** Lines 164-165 say extract `hFocusWindow` from the captured device and call `setup(hwnd)`. The plan should define what happens if `GetCreationParameters` fails, `hFocusWindow` is null, or `directX::getDevice()` is null. Current language assumes the device is live.

- **HIGH: `render_backend::get()` null handling is missing in render and diagnostic helpers.** After the carve, `DrawDepthWindow`, `DrawColorWindow`, Tests window, `newFrame`, and `renderDrawData` all dereference the active backend. The plan should mandate a local `auto* backend = render_backend::get(); if (!backend) return;` or prove setup always precedes render.

- **MEDIUM: Tests-window guard may not reproduce original behavior.** Lines 159-162 gate the whole block on `sceneDepthTexture() != 0`. Plan 01 defines `sceneDepthTexture()` to return 0 when `getDepthTexture()` is null **or its color texture is null**. If the old block-level guard was only `depthTex != nullptr`, the new guard hides the whole Tests block in more cases than before. That is behavior drift in the A2 edge case the prompt called out.

- **MEDIUM: D-06 source gate can false-pass some DX9 coupling.** It gates `#include <d3d9.h>`, `IDirect3DDevice9`, `ImGui_ImplDX9_`, `directX::`, lines 204-211. It may miss aliases, wrapper includes, `LPDIRECT3DDEVICE9`, `D3DDEVICE_CREATION_PARAMETERS`, or indirect inclusion of `imgui_impl_dx9.h`. It also strips comments but likely not string literals, so it can false-trip on diagnostic strings.

- **MEDIUM: Refactoring `SourceGateUtil.h` could destabilize existing RESID-04 coverage.** Lines 197-202 allow either including the new header in `NoDeviceResetTests.cpp` or leaving copies. For this phase, leaving existing file-local copies is safer. Sharing test utilities is fine, but not worth touching the already-proven no-Reset gate unless necessary.

- **LOW: `grep -L` verification is weak.** Line 171 uses grep-style matching that does not strip comments and may behave differently on PowerShell/Windows depending on grep availability. The Catch2 gate should be the real acceptance check.

### Suggestions

- Define exact setup order:
  1. In `hkPresent`, use the live `pDevice` argument to obtain `HWND`.
  2. Initialize/install `Dx9Backend` while still in `directx9.cpp`.
  3. Call `imgui_impl::setup(hwnd)` only after backend install succeeds.
  4. `imgui_impl::render()` returns early if no backend is installed.
- Split texture availability methods to preserve behavior exactly:
  - `bool hasSceneDepthResources()` or `bool hasDepthTextureObject()` for the Tests-window block guard.
  - `sceneDepthTexture()` and `sceneColorTexture()` for image handles.
  This avoids overloading `ImTextureID != 0` as both "object exists" and "preview image can draw."
- Strengthen D-06 patterns to include `imgui_impl_dx9`, `D3DDEVICE_CREATION_PARAMETERS`, `LPDIRECT3D`, and maybe `d3d9` include variants after comment/string stripping.
- Do not refactor `NoDeviceResetTests.cpp` in this phase. Add `SourceGateUtil.h` for new tests only, or copy helpers locally.
- Add explicit live-smoke checklist item for "first overlay open after injection" and "first frame after scene load," because those are most likely to expose setup-order bugs.

Risk Assessment: **HIGH** until the setup/init/null-backend path is specified. After that, **MEDIUM** due to the unavoidable live-smoke dependency.

## Overall Suggestions

- Fix the seam contract before execution: make the initialization method real, named, and tested.
- Stop describing the A2 seam as "8 members"; use the real method count.
- Add a "backend unavailable" behavior rule for every seam call site.
- Preserve the Tests-window guard against the same semantic condition as before, not against a derived `ImTextureID`.
- Keep Phase 18 narrow: no D3D11 detection, no resize behavior, no cleanup rewrite, no detour relocation.

With those changes, the plans should achieve RNDR-01 with a defensible proof story: D-06 for source isolation, D-07 for dispatch mechanics, existing RESID-04 for no Reset, and D-08 for live behavior.

---

## Cursor Review

# Adversarial Peer Review — Phase 18 (Render-Backend Seam + Dx9Backend)

Review grounded in live source reads (`imgui_impl.cpp/h`, `directx9.cpp`, `NoDeviceResetTests.cpp`, `utinni.cpp`) cross-checked against plans 18-01 and 18-02.

---

## Plan 18-01 — Define seam + Dx9Backend + D-07 mock test

### 1. Summary

Plan 18-01 is a sound Wave-1 contract-first approach: add an internal `IRenderBackend` vtable, wrap existing `directX::` verbatim, keep resize hooks as honest no-ops, and prove dispatch with a Catch2 mock. The wrap-don't-rewrite discipline, Pitfall 2 (leave `initPresentBlockedEvent`/`initDepthTexture` in `utinni_init`), and no-Reset acceptance gates are well aligned with live code. The main gaps are **underspecified backend initialization** (no `init()` in the interface or Plan 01 artifacts), **sloppy member accounting** ("8 members" vs 10 pure virtuals), and **D-04 wording that misstates where RT dimensions come from**. Wave 1 adds dead code until 18-02 wires it — acceptable, but D-07 only proves the vtable exists, not that production will call it correctly.

### 2. Strengths

- **Wrap-don't-rewrite is correct.** Forwarding to `directX::getDepthTexture()`, `Graphics::getCurrentRenderTargetWidth/Height()`, and `ImGui_ImplDX9_*` matches the actual bodies at `directx9.cpp:637–665` and avoids touching fragile detour/vtable-harvest code.
- **No-Reset contract is gated.** Acceptance criteria mirror `NoDeviceResetTests.cpp` (`->Reset(` / `.Reset(` == 0) and D-03 `{}` resize hooks — consistent with `hkReset`'s pass-through at `directx9.cpp:373–375`.
- **Heap-free hot path.** Static-storage singleton + plain virtual dispatch satisfies the R-H lesson; explicitly forbids `std::function`/`new Dx9Backend`.
- **Internal seam, no ABI surface.** Zero `UTINNI_API` on `render_backend` — correct per CPPS-04; exported `directX::*` signatures untouched.
- **Test-seam via `set()`/`get()`.** Enables D-07 without a live device — good max-harness pattern.
- **A2 amendment is necessary.** `DrawColorWindow` at `imgui_impl.cpp:376–390` and the stage slider at `:499–503` genuinely need `sceneColorTexture()` and neutral stage accessors; dropping them would silently lose dev diagnostics.

### 3. Concerns

| Severity | Concern |
|----------|---------|
| **HIGH** | **No `init()` contract in Plan 01.** Plan 02 Task 1 says setup will "trigger backend init" doing `GetCreationParameters` + `ImGui_ImplDX9_Init`, but 18-01 defines only 10 vtable members — no `init()`, no `Dx9Backend::init()` in artifacts, no acceptance criteria. Executor must invent this out-of-band; easy to get wrong or omit. |
| **HIGH** | **Device sourcing weaker than today.** Current `setup(pDevice)` at `directx9.cpp:361` uses the Present hook's `pDevice` directly. Plan 02 assumes backend self-sources via `directX::getDevice()` (`pDirectXDevice`, set in `hkBeginScene` at `:249–251`). That *usually* works (BeginScene → Present), but is strictly weaker than passing `pDevice` from `hkPresent`. No null-device guard or fallback is specified. |
| **MEDIUM** | **Member count is internally inconsistent.** must_haves say "8 members" but list 10 pure virtuals (6 roadmap + depth + color + stage pair). D-07 Task 2 says "all 8 members" but requires ≥10 `override`s. Sloppy accounting invites incomplete mocks/interfaces. |
| **MEDIUM** | **D-04 truth is misstated.** must_haves claim RT dims are "sourced from the existing present-stretch math already living in imgui_impl.cpp." Implementation (PATTERNS/research) sources from `Graphics::getCurrentRenderTargetWidth/Height()` — same values the present-stretch block already reads at `imgui_impl.cpp:450–451`, but the seam members are **unused in Phase 18** (imgui_impl keeps calling `Graphics::` directly). Misleading for executors; harmless for behavior if left unused. |
| **LOW** | **D-07 doesn't prove production wiring.** Mock test drives `render_backend::get()` directly — it cannot catch Plan 02 mistakes (null `get()`, wrong init order, missing `#include` purge). Appropriate for Wave 1, insufficient alone for RNDR-01. |
| **LOW** | **Wave-1-only seam is inert.** No compile-time proof that `imgui_impl` will link against the seam until 18-02. Acceptable split, but merge 18-01 alone delivers zero user-visible value. |

### 4. Suggestions

1. **Add an explicit non-virtual init to Plan 01:** e.g. `void Dx9Backend::init(IDirect3DDevice9* device)` (or `initFromPresentDevice`) declared in `render_backend.h`, implemented only in `.cpp`, called from `imgui_impl::setup()` — **not** a vtable member (Phase 19 Dx11 init differs). Acceptance: `grep ImGui_ImplDX9_Init render_backend.cpp == 1`.
2. **Prefer passing `pDevice` from `hkPresent` into init** rather than `getDevice()` alone — preserves today's invariant that setup always has the live Present device. `getDevice()` can be assert/fallback.
3. **Fix member accounting everywhere:** "10 pure virtuals (8 ROADMAP-named capabilities + A2 color + A2 stage pair)" — update D-07 mock counters accordingly.
4. **Clarify D-04:** seam exposes RT dims for Phase 19; Phase 18 imgui_impl continues reading `Graphics::` directly in the RT-space block (`:447–463`). Optionally add a comment in `render_backend.h` that these are forward-compat hooks.
5. **Add acceptance:** `render_backend.h` must not `#include <imgui_impl_dx9.h>` (only `.cpp` may).

### 5. Risk Assessment

**MEDIUM** — Plan 01 is low behavioral risk (seam unused until 18-02), but the missing `init()` contract and ambiguous device acquisition are ordering bugs waiting to happen in Plan 02. Mitigate by locking init shape in 18-01 before execution.

---

## Plan 18-02 — Carve imgui_impl + D-06 gate + live-smoke

### 1. Summary

Plan 18-02 correctly identifies the six DX9 touch-points, preserves the API-neutral bulk (WndProc/Issue #11, RT-space `AddMousePosEvent`, gizmo, renderCallbacks), and adds a maintainer live-smoke as the real RNDR-01 gate. The hkPresent → `setup(HWND)` carve is directionally right. However, there are **concrete behavioral and robustness gaps**: the proposed Tests-window guard is **stricter than today's** for `!enableUi` dev config, **D-06 can false-pass** on lingering DX9 includes, **ImGui Win32/DX9 init order is not locked**, and **`render()` calls `get()` without null checks**. The hkPresent ordering (`render()` at `:325` before `setup()` at `:361`) is preserved by the existing `isSetup` gate — good — but plans should state this explicitly so executors don't "fix" it.

### 2. Strengths

- **Touch-point inventory matches source.** Verified lines: `ImGui_ImplDX9_Init` `:274`, `NewFrame` `:419`, `RenderDrawData` `:562`, depth reach-ins `:349`, `:376`, `:489`.
- **Preserves load-bearing API-neutral code.** WndProc subclass (`:144–254`), RT-space block (`:447–463`), DirectInput arbitration (`:537–550`), `dispatchSnapshot` (`:512–514`), gizmo namespace (`:631+`) — all correctly marked hands-off.
- **hkPresent callsite update is minimal.** Extract HWND from `pDevice`, call `imgui_impl::setup(hwnd)` — keeps `Client::setSwgHwnd` plumbing (Issue #10) without exposing D3D9 types to imgui_impl.
- **Tests-window guard intent is documented.** Plan 02 Task 1 explicitly addresses the A2 `sceneDepthStage()` vs block-level guard problem — shows the planner read the edge case.
- **D-06 clones proven infrastructure.** `NoDeviceResetTests.cpp:106–145` comment stripper + self-check SECTION (`:174–188`) is the right pattern; gating concrete symbols not bare `"D3D9"` avoids false-trips on rationale comments at `imgui_impl.cpp:422–446`.
- **Blocking human live-smoke (D-08).** Correct — CI cannot inject; overlay + Issue #11 + resize/scene-change are the only proof of "behaviorally unchanged."
- **hkReset stays in DX9 tier.** `ImGui_ImplDX9_InvalidateDeviceObjects`/`CreateDeviceObjects` at `directx9.cpp:373–375` correctly not moved — D-05 purges imgui_impl, not directx9.

### 3. Concerns

| Severity | Concern |
|----------|---------|
| **HIGH** | **D-06 gate incomplete — false-pass risk.** Today `imgui_impl.cpp` includes `<imgui_impl_dx9.h>` (`:28`) and `"swg/graphics/directx9.h"` (`:48`). D-06 gates `#include <d3d9.h>`, `IDirect3DDevice9`, `ImGui_ImplDX9_`, `directX::` — but **not** `#include <imgui_impl_dx9.h>`, `#include "swg/graphics/directx9.h"`, or `D3DDEVICE_CREATION_PARAMETERS`. Executor could leave DX9-binding includes and pass D-06 while violating D-05 spirit. |
| **HIGH** | **Backend `init()` still undefined at carve time.** Task 1 says "trigger its init per Plan 01" but Plan 01 doesn't deliver it. Combined with ambiguous Win32/DX9 ordering (below), first-frame init failure is plausible. |
| **HIGH** | **ImGui_ImplWin32_Init vs ImGui_ImplDX9_Init order not locked.** Today: Win32 `:273` then DX9 `:274`. Plan 02 action lists backend init and "Keep ImGui_ImplWin32_Init" but doesn't mandate **Win32 before DX9**. ImGui backend docs expect platform backend first; reversing causes subtle init failures. |
| **MEDIUM** | **Tests-window block guard is NOT verbatim.** Today (`:489–490`): `if (depthTex != nullptr)` — always true in production after `initDepthTexture()` (`directx9.cpp:81–84`) even before GPU textures exist (`createTexture` runs later at `:355–357`). Proposed: `sceneDepthTexture() != 0`, which also requires `getTextureColor() != nullptr`. **Behavior change for `!enableUi` dev config** — Tests UI hidden until textures are fully live. **Release (`enableInternalUi=true`) unaffected** — block is behind `if (!enableUi)` at `:480`. Plan claims "preserving exactly" but this is stricter. |
| **MEDIUM** | **No null-check on `render_backend::get()` in hot path.** After carve, `render()` will call `get()->newFrame()` when `isSetup`. If setup sets `isSetup=true` before `set(dx9Singleton())`, or `set(nullptr)` leaks from D-07 test pollution, this crashes on the render thread. Plan should require: `set()` before `isSetup=true`, and optionally `if (auto* b = get()) b->newFrame();`. |
| **MEDIUM** | **D-07 + D-06 don't prove imgui_impl routes correctly.** No test asserts `grep 'render_backend::get\(\)->' imgui_impl.cpp >= N`. Structural gate proves absence of DX9 symbols, not presence of seam dispatch. Regression could compile with empty stubs. |
| **MEDIUM** | **`render()` before `setup()` in hkPresent is subtle.** `directx9.cpp:325` calls `render()` before `:361` `setup()`. Safe today because `isSetup` gate at `imgui_impl.cpp:403`. Plans should **explicitly forbid moving setup before render** or merging them — executors "cleaning up" hkPresent could break first-frame semantics. |
| **LOW** | **DrawDepth/DrawColor extra guards differ slightly.** Windows also guard `getPlayer() == nullptr` (`:350`, `:377`) — correctly left in imgui_impl. Seam accessors fold color-null into depth handle — matches DrawDepth/Color, not Tests block. |
| **LOW** | **SourceGateUtil refactor risk.** Lifting helpers from `NoDeviceResetTests.cpp` could break `[resid04]` if done carelessly. Plan allows leaving file-local copies — prefer that for minimal diff. |
| **LOW** | **ROADMAP SC#3 vs D-04 tension.** Success criterion 3 says "only the seam dispatch sites call through the seam" but RT-space math stays on `Graphics::` directly — correct per research, slightly at odds with literal SC wording. Not a bug if documented. |

### 4. Suggestions

1. **Extend D-06 tokens:**
   - `#include <imgui_impl_dx9.h>`
   - `#include "swg/graphics/directx9.h"` (and angle-bracket variant)
   - Optionally `D3DDEVICE_CREATION_PARAMETERS`
2. **Lock setup() ordering explicitly:**
   ```text
   render_backend::set(dx9Singleton());
   backend->init(pDevice_from_hkPresent);  // NOT getDevice() alone
   Client::setSwgHwnd(hwnd);
   ImGui_ImplWin32_Init(hwnd);            // BEFORE DX9
   backend init completes ImGui_ImplDX9_Init
   SetWindowLongPtr(...);
   isSetup = true;                        // LAST
   ```
3. **Tests-window guard — pick one intentionally:**
   - **Verbatim:** gate on a new `hasDepthTextureObject()` or keep checking wrapper existence API-neutrally (e.g. `sceneDepthTextureAvailable()` returning bool without requiring color tex).
   - **Stricter (recommended for dev UX):** keep `sceneDepthTexture() != 0` but document as intentional dev-only behavior change, not "verbatim."
4. **Add carve acceptance grep:** `render_backend::get\(\)->` count ≥ 6 in `imgui_impl.cpp`; zero `ImGui_ImplDX9_` / `directX::` after strip.
5. **hkPresent extraction snippet in plan:** require `D3DDEVICE_CREATION_PARAMETERS` + `GetCreationParameters` **in directx9.cpp only** (not imgui_impl), pass `cParam.hFocusWindow` to `setup()`.
6. **Live-smoke add dev-config check** if maintainer uses `enableInternalUi=false`: verify Tests window + stage slider still behave as expected post-guard change.
7. **Do not refactor `NoDeviceResetTests.cpp` helpers in Phase 18** unless necessary — duplicate in `SourceGateUtil.h` or include header from both; zero churn to `[resid04]`.

### 5. Risk Assessment

**MEDIUM–HIGH** — This plan touches the live render path. Automated gates are good but incomplete (D-06 false-pass, no integration dispatch test). The init-order and Tests-window guard issues are the most likely silent regressions. Live-smoke (D-08) is the right final gate; Release behavior is likely preserved, dev-config behavior may not be verbatim.

---

## Overall Phase Assessment (Cursor)

### Summary

Phase 18 plans are **well-researched and appropriately scoped** as a move-not-rewrite carve. The two-wave split (contract → consumer) is sound. The plans will **likely achieve RNDR-01 for the shipped Release config** (`enableInternalUi=true`) if live-smoke passes. They are **not yet tight enough** to guarantee verbatim behavior across all configs or to prevent executor-invented init/order bugs. The biggest blind spots are: **undefined backend init**, **incomplete D-06 coverage**, **ImGui init ordering**, and the **Tests-window guard semantic drift**.

### Dependency ordering checklist

| Question | Verdict |
|----------|---------|
| Does `setup(HWND)` break hkPresent → setup install ordering? | **No**, if hkPresent still calls setup once at `:361` after first-frame depth create. Explicitly document: do not move setup before render. |
| Is device guaranteed live when backend self-sources? | **Mostly yes** (BeginScene captures device before Present), but **today uses Present's `pDevice` directly** — plan should preserve that. |
| Is `pDirectXDevice` guaranteed non-null at setup? | **Not proven** — add null assert + fallback to Present parameter. |
| ImGui Win32 vs DX9 init order? | **At risk** — must lock Win32 first. |
| `render()` before `setup()` same frame? | **Safe** via `isSetup` gate — document as intentional. |

### Recommended plan amendments (priority order)

1. Add `Dx9Backend::init(IDirect3DDevice9*)` to **18-01** with acceptance criteria; call from **18-02** `setup()`.
2. Extend **D-06** to gate `#include <imgui_impl_dx9.h>` and `#include "swg/graphics/directx9.h"`.
3. Lock **setup() step order** (set backend → Win32 init → DX9 init → WndProc → `isSetup=true`).
4. Resolve **Tests-window guard** explicitly (verbatim vs intentional stricter).
5. Pass **`pDevice` from hkPresent** into backend init; don't rely solely on `getDevice()`.
6. Add **dispatch presence grep** to 18-02 acceptance: `render_backend::get()->` in imgui_impl.
7. Fix **"8 members" → "10 pure virtuals"** throughout both plans.

### Overall risk assessment

**MEDIUM** — Architecture and research are strong; execution hazards concentrate in **init ordering**, **incomplete structural gate**, and a **dev-only behavioral drift** in the Tests window. None are likely to break Release overlay rendering if live-smoke is thorough, but any one could produce a hard-to-bisect first-frame blank overlay or a `[rndr01]` false green.

*Reviewer note: `render_backend.{h,cpp}` do not exist in the repo yet (expected pre-execution). All line citations above are from current `master` source.*

---

## Consensus Summary

Both reviewers independently rate the **architecture as sound** (small internal vtable seam, static
Dx9 backend, wrap-don't-rewrite, two-wave contract→consumer split, the D-06/D-07/[resid04]/D-08
verification pyramid) and confirm the A2 amendment (color + stage accessors) is **justified, not scope
creep** — the call sites at `imgui_impl.cpp:376-390` and `:499-503` genuinely need them. Overall
phase risk: **MEDIUM** (both), trending MEDIUM-HIGH on Plan 02 because it touches the live render path.

### Agreed Strengths (2+ reviewers)

- Wrap-don't-rewrite of `directX::` internals; no fragile detour/vtable-harvest code touched.
- No-Reset contract treated as first-class (D-03 `{}` no-ops, `->Reset(`/`.Reset(` == 0 gate, hkReset left in directx9.cpp).
- Heap-free hot path (static-storage singleton + plain virtual dispatch; `std::function`/`new` forbidden).
- Internal seam with zero `UTINNI_API` — correct CPPS-04 ABI protection; `setup(HWND)` is ABI-safe.
- Issue #11 routing + RT-space `AddMousePosEvent` + gizmo + renderCallbacks explicitly protected.
- D-06 gates concrete symbol forms (not bare "D3D9") — good grep-gate hygiene.
- Blocking maintainer live-smoke (D-08) is the right final RNDR-01 gate.

### Agreed Concerns (raised by BOTH — highest priority)

1. **[HIGH] Missing backend `init()` contract.** Plan 02 requires the backend to do
   `GetCreationParameters` + `ImGui_ImplDX9_Init`, but Plan 01 defines no init method — only the 10
   vtable members. Plan 02 would have to invent API surface mid-execution. **Both recommend: add an
   explicit non-virtual `Dx9Backend::init(IDirect3DDevice9*)` to 18-01 with its own acceptance
   criteria (`grep ImGui_ImplDX9_Init render_backend.cpp == 1`), declared in the header, called from
   18-02 `setup()`. Keep it OFF the vtable (Phase 19's Dx11 init differs).**

2. **[HIGH] Null-backend / first-frame ordering not handled.** `get()` returns nullable `s_active`;
   every carved call site (`newFrame`, `renderDrawData`, DrawDepth/Color, Tests window) will
   dereference it. **Both recommend: mandate `set()` before `isSetup=true`, and a
   `if (auto* b = get()) ...` guard / early-return rule for every seam call site. Cursor notes the
   existing `isSetup` gate currently protects render()-before-setup() ordering — plans should
   explicitly forbid executors from "fixing"/reordering hkPresent.**

3. **[HIGH/MEDIUM] D-06 source gate can false-pass.** It does not gate the actual DX9-binding
   includes present today — `#include <imgui_impl_dx9.h>` and `#include "swg/graphics/directx9.h"` —
   nor `D3DDEVICE_CREATION_PARAMETERS`. An executor could leave DX9 includes and still pass D-06.
   **Both recommend: extend the gated token set to those includes + the creation-params struct.**

4. **[MEDIUM] Tests-window guard drift.** Replacing `if (depthTex != nullptr)` with
   `sceneDepthTexture() != 0` is **stricter** (the accessor also requires a live color texture), so it
   hides the `!enableUi` Tests block in more cases than today. The plan calls this "preserving
   exactly" — it is not. **Both recommend: either add a dedicated `hasDepthTextureObject()`/
   `sceneDepthTextureAvailable()` bool for the block guard (true verbatim), or keep the stricter guard
   but document it as an intentional dev-only change. Cursor confirms Release `enableInternalUi=true`
   is unaffected (block is behind `if (!enableUi)`).**

5. **[MEDIUM] Member-count documentation debt.** "8 members" appears throughout while the interface
   lists 10 pure virtuals. **Both recommend: rename to "10 pure virtuals (6 ROADMAP + A2 color + A2
   stage pair)" everywhere, and align the D-07 mock counters.**

6. **[MEDIUM] Device sourcing weaker than today.** Self-sourcing via `directX::getDevice()` is
   strictly weaker than passing the live `pDevice` from `hkPresent`. **Both recommend: pass
   `pDevice` from hkPresent into `init()`; treat `getDevice()` as assert/fallback only.**

7. **[MEDIUM] D-04 `renderTargetWidth/Height` wording overclaims.** The must_haves say the dims come
   from "present-stretch math in imgui_impl," but the seam members forward to
   `Graphics::getCurrentRenderTargetWidth/Height()` and are **unused in Phase 18** (imgui_impl keeps
   reading `Graphics::` directly). Harmless for behavior; misleading for executors. **Both recommend:
   clarify they are forward-compat hooks for Phase 19.**

8. **[MEDIUM] No automated proof imgui_impl actually dispatches through the seam.** D-06 proves
   absence of DX9 symbols, D-07 proves the vtable works — neither proves the carve wired the seam in.
   **Both recommend: add a 18-02 acceptance grep `render_backend::get()->` count ≥ 5–6 in imgui_impl.cpp.**

9. **[LOW] Don't refactor `NoDeviceResetTests.cpp`.** Lifting its helpers into `SourceGateUtil.h`
   risks the proven `[resid04]` gate. **Both recommend: leave file-local copies; add SourceGateUtil.h
   for the new test only (zero churn to the existing gate).**

Also agreed: **keep Phase 18 narrow** — no D3D11 detection, no resize behavior, no cleanup/detour
relocation.

### Divergent / single-reviewer Views

- **Cursor (additional):** ImGui platform-vs-renderer **init order must be locked Win32-before-DX9**;
  reversing causes subtle init failures (ImGui backend docs). Codex did not raise this.
- **Cursor (additional, forward-looking):** hkReset's DX9 ImGui device-object calls bypass the seam —
  fine for D3D9 Phase 18, but Phase 19 will need a device-loss story for D3D11 (out of scope, noted).
- **Codex (additional):** suggests the D-07 test also assert `get() == nullptr` after restore to catch
  leaked global state; and a static check that `render_backend.h` excludes `directx9.h`/`depth_texture.h`/`<d3d9.h>`.
- **Minor disagreement on D-04 framing:** Codex calls RT-dim ownership "muddled" and wants a behavior
  assert; Cursor reads it as acceptable forward-compat if documented. Both want the wording clarified.

### Recommended next step

Feed these back into planning before execution:

```
/gsd-plan-phase 18 --reviews
```

Priority amendments (both reviewers' consensus order): (1) add `Dx9Backend::init()` to 18-01;
(2) extend D-06 to gate the DX9-binding includes; (3) lock setup() step order incl. Win32-before-DX9;
(4) resolve the Tests-window guard (verbatim bool vs documented stricter); (5) pass hkPresent `pDevice`
into init; (6) add the `render_backend::get()->` dispatch-presence grep to 18-02; (7) fix "8 members"→10.
