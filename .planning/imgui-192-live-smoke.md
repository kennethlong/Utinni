# Live SWG Smoke — imgui 1.76 → 1.92.8 (commit `d71feb2`)

**Why this exists:** the imgui bump's render + input path only runs in an injected
live SWGEmu session — CI can't exercise it. The riskiest change is the
`hkWndProcHandler` rewrite in `UtinniCore/swg/ui/imgui_impl.cpp`: the pre-1.87 manual
`io.MouseDown[]` / `io.KeysDown[]` / `io.AddInputCharacter()` poking was **deleted** and
replaced with a single `ImGui_ImplWin32_WndProcHandler()` call that translates Win32
messages into imgui's new `io.Add*Event()` queue. If that one call is wired wrong, the
overlay renders but **eats no input** (or the wrong input). This smoke proves it works.

## Smoke build (already prepared this session)

- `g_showDemoWindowProbe` flipped `false → true` (`imgui_impl.cpp:136`). This is **temporary
  smoke instrumentation** — REVERT after. The demo window renders **unconditionally**
  (outside the `enableUi`/INI gate), so it's a deterministic surface regardless of
  `ut.ini`'s `enableInternalUi`, and it's the **only** path with an `InputText` field —
  which is exactly what the new `WM_CHAR → backend` route needs to be tested against.
- Rebuilt: `bin/Release/UtinniCore.dll` (native-only target → no `Generated/*.cs` churn).

## How to run

1. Launch SWGEmu via the Launcher **from `bin\Release\`** (it injects the `UtinniCore.dll`
   sitting next to it — `Launcher/main.cpp:181`).
2. Get to a point where the overlay is up. The **"Dear ImGui Demo"** window should appear
   on its own (demo probe).
3. Keep `utinni.log` open (next to the injected DLL / SWG client dir). Diagnostics in
   `hkWndProcHandler` log at **info** level, so they show without debug logging.

## Checklist — observe and report each

### 1. Render (NewFrame + static-lib link + new includes)
- [ ] "Dear ImGui Demo" window is visible and **not garbled**.
- [ ] Text is **legible glyphs**, not boxes/tofu (confirms the `micross.ttf` font atlas
      built — `imgui_impl.cpp:281`).
- [ ] Dark theme + **blue active title bar** applied (the custom style block still takes).
- [ ] No crash / no corrupt textures on first paint.

### 2. Mouse (was `io.MouseDown[]`/`MousePos`/`MouseWheel`, now backend-routed)
- [ ] **Hover** the demo window → game camera/WASD stops, the cursor appears.
      (`render()` `imguiHasHover` → `DirectInput::suspend`, lines 475–489.)
- [ ] Expand a **collapsing header** (e.g. "Widgets") by clicking it.
- [ ] **Drag a slider** (Widgets → "Basic" → a float slider) — value tracks the drag.
- [ ] **Mouse-wheel** scrolls the demo window content.
- [ ] **Drag the title bar** to move the window.
- [ ] Move the cursor **off** all imgui windows → game input resumes (camera/WASD back,
      game cursor returns).

### 3. Keyboard / text — THE critical path (`WM_KEYDOWN`/`WM_CHAR` → `io.Add*Event`)
- [ ] Widgets → "Text Input" → click the **"input text"** field and type letters/digits.
      **Characters appear in the field.** ← this is the headline assertion; it's the path
      that was fully rewritten.
- [ ] **Backspace**, **Left/Right arrows**, **Home/End** behave inside the field
      (confirms `WM_KEYDOWN` routing, not just `WM_CHAR`).
- [ ] In `utinni.log`, typing printable chars emits
      `hkWndProcHandler: WM_CHAR '<c>' (0x..)` lines (diag preserved).
- [ ] While the field is focused, the log shows `io.WantTextInput=1` /
      `io.WantCaptureKeyboard=1` on a `WM_KEYDOWN vk=...` line if you press Enter/Esc there.

### 4. Esc-to-cancel gizmo (secondary — needs an object + gizmo) — `imgui_impl.cpp:877`
> `ImGui::IsKeyDown(ImGui::GetKeyIndex(ImGuiKey_Escape))` → `ImGui::IsKeyDown(ImGuiKey_Escape)`.
- [ ] If easy in your scene: select an object so the transform gizmo shows, begin a drag,
      press **Esc** → transform **reverts** to its pre-drag value. (Skip if no quick repro.)

### 5. Focus diagnostics (preserved logging)
- [ ] **Alt-Tab** away and back. `utinni.log` shows `WM_KILLFOCUS` / `WM_SETFOCUS` /
      `WM_ACTIVATE` lines, and input **still works** after refocus.

## Pass criteria

PASS = §1–§3 all green. §4/§5 are confirmatory. A scene change landing you **naked but
in-world** is the known baseline, **not** a failure (see `project_tjt_scene_change_naked_baseline`).
A crash, blank/garbled overlay, dead mouse, or **typed characters not appearing** = FAIL —
capture the `utinni.log` tail and the crash address if any.

## After smoke (either outcome)

- **Revert the instrumentation:** set `g_showDemoWindowProbe` back to `false`
  (`imgui_impl.cpp:136`). It must not be committed `true`.
- If PASS: the bump's live path is verified; the `[[project_vcpkg_migration_complete]]`
  watch item closes. Note it in the next handoff.
- If FAIL: first suspect is the `ImGui_ImplWin32_WndProcHandler` forward-decl / call wiring
  in `hkWndProcHandler`; see `[[feedback_d3d9_hook_diagnosis]]` for the order-of-suspicion rule.
