/**
 * MIT License
 *
 * Copyright (c) 2020 Philip Klatt
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 **/

#include "imgui_impl.h"
#include <imgui.h>
#include <imgui_impl_win32.h>
#include <ImGuizmo.h>
// Phase 18 / RNDR-01: the runtime-polymorphic render seam (replaces the former
// Direct3D bindings). Included here in the .cpp -- NOT in imgui_impl.h -- so its
// <imgui.h> dependency stays out of the CppSharp parse graph.
#include "swg/graphics/render_backend.h"
// Phase 19 / RNDR-03 (D-15/D-17): the PURE backend-selection decision. This
// header names ZERO concrete DX11/DXGI symbol (it models the decision as two
// booleans), so it passes the extended D-06 neutrality gate -- the DX11 hook
// tier header (the directX11 tier .h) deliberately stays OUT of this TU.
#include "swg/graphics/backend_select.h"

#include <atomic>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <vector>

// v19 (Goal B Wave 3 / rider 4C): advertised live-camera matrix accessors (defined in camera.cpp).
// Local extern decls -- kept out of camera.h so the fn-ptr typedefs stay out of the CppSharp parse
// (the endpoints_bindings.cpp pattern). Null on SWGEmu; the gizmo draw() branches on that.
namespace swg::camera
{
using pGetProjectionMatrix = int(__cdecl*)(float* out16);
using pGetTransformO2W = int(__cdecl*)(float* out12);
extern pGetProjectionMatrix getProjectionMatrix;
extern pGetTransformO2W getTransformO2W;
} // namespace swg::camera

#include "utility/log.h"
#include "swg/ui/cui_chat_window.h"
#include "swg/game/game.h"
#include "swg/scene/ground_scene.h"
#include "swg/client/client.h"
#include "swg/endpoints.h" // Wave-2 §5.6: gizmo draw() advertised-client guard

#include "swg/graphics/graphics.h"
#include "swg/misc/direct_input.h"
#include "swg/scene/ground_scene.h"
#include "swg/misc/network.h"
#include "swg/misc/config.h"
#include "command_parser.h"
#include "cui_io.h"
// Phase 18 / RNDR-01 (D-05 purge): the former depth-texture reach-in into the
// DX9 tier is gone -- imgui_impl now routes scene depth/color through the render
// seam (render_backend.h, pulled in via imgui_impl.h). No Direct3D binding header
// is included here.
#include "swg/game/game.h"
#include "swg/scene/render_world.h"

// imgui 1.92 deliberately wraps this declaration in a `#if 0` block inside
// imgui_impl_win32.h (so the helper header doesn't drag in <windows.h>); per its
// own instructions, forward-declare it here to call it from our wndproc subclass.
extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);

using namespace utinni;
using namespace swg::math;

// 2026-05-22 follow-up to ground_scene fix (commit 7201700): R-H snapshot
// dispatch via std::vector + stack-allocated fixed-size snapshot. Replaces
// std::unordered_map storage + per-call heap-allocated snapshot. See
// [[project-rh-snapshot-no-heap-alloc]]. Helper lives at file scope so both
// `namespace imgui_impl` (renderCallbacks) and the file-scope gizmo registries
// below can dispatch through it.
namespace
{
template <typename Fn>
struct CallbackEntry
{
    int handle;
    Fn func;
};

template <typename Fn, typename Invoke>
void dispatchSnapshot(
    const std::vector<CallbackEntry<Fn>>& registry,
    std::mutex& mutex,
    Invoke&& invoke)
{
    constexpr size_t kInlineCap = 16;
    Fn stackSnap[kInlineCap];
    Fn* snapshot = stackSnap;
    std::vector<Fn> heapSnap;
    size_t count = 0;
    {
        std::lock_guard<std::mutex> guard(mutex);
        const size_t total = registry.size();
        if (total <= kInlineCap)
        {
            count = total;
            for (size_t i = 0; i < count; ++i)
            {
                stackSnap[i] = registry[i].func;
            }
        }
        else
        {
            heapSnap.reserve(total);
            for (const auto& e : registry)
            {
                heapSnap.push_back(e.func);
            }
            snapshot = heapSnap.data();
            count = total;
        }
    }
    for (size_t i = 0; i < count; ++i)
    {
        invoke(snapshot[i]);
    }
}
} // namespace

namespace imgui_impl
{

// Phase 3 R-A native-side (per 03-CONTEXT D-08/D-09): handle-based registry
// backed by insertion-order std::vector<{handle, fn_ptr}>.
// CR-01 (03-REVIEW): per-registry mutex protects Subscribe / Unsubscribe / snapshot.
static std::vector<CallbackEntry<void (*)()>> renderCallbacks;
static std::mutex renderCallbacksMutex;
static int s_nextRenderId = 1;

bool enableUi;
bool rendering;

// DIAG 2026-05-23 Plan 06-01 Task 2: additive ShowDemoWindow probe gated
// behind this flag so it can be flipped off without recompiling. Lives at
// file scope inside namespace imgui_impl so future Wave-1 work can grep
// `g_showDemoWindowProbe` deterministically and re-enable as needed.
// Task 3 sign-off (2026-05-23) flipped this to false after the maintainer
// verified the Demo screen rendered end-to-end (menus + sliders + buttons +
// tabs + plots + popups + drag-and-drop) over live SWG. Call site retained
// so a one-line edit re-enables for future Wave-1 plugin styling work.
static bool g_showDemoWindowProbe = false;

void enableInternalUi(bool enable)
{
    enableUi = enable;
}

WNDPROC originalWndProcHandler = nullptr;

// 2026-06-25 (Enter->fullscreen embed fix): the SWG client toggles a WINDOW-LEVEL fullscreen via its
// own DirectInput->keymap path (not a Win32 message we can intercept as a keypress -- input arrives via
// DirectInput polling, see the WM_KEYDOWN note below). When embedded that grows the SWG window over the
// editor; the 250ms watchdog only re-docks it reactively (a visible flicker/toggle). Instead, clamp the
// SWG window's WM_WINDOWPOSCHANGING to the editor embed rect so it physically cannot grow past the panel.
// The managed PanelGame.RepositionSwgWindow pushes the current embed rect here (screen-space) right
// before each SetWindowPos, so the clamp tracks form resize/drag/maximize. Only GROWTH beyond the embed
// is clamped -- shrink/minimize/move and the managed reposition itself (rect updated first) pass through.
static int g_embedClampX = 0, g_embedClampY = 0, g_embedClampW = 0, g_embedClampH = 0;
static bool g_embedClampValid = false;

extern "C" __declspec(dllexport) void __cdecl utinni_setEmbedClampRect(int x, int y, int w, int h)
{
    if (w > 0 && h > 0)
    {
        g_embedClampX = x;
        g_embedClampY = y;
        g_embedClampW = w;
        g_embedClampH = h;
        g_embedClampValid = true;
    }
}

IMGUI_API LRESULT hkWndProcHandler(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    ImGuiIO& io = ImGui::GetIO();

    // imgui 1.92: route every Win32 message through the Win32 backend, which
    // translates them into the new io.Add*Event() queue (mouse buttons/move/wheel,
    // keyboard, characters). Replaces the pre-1.87 manual io.MouseDown[]/io.KeysDown[]
    // poking -- io.KeysDown[] was removed in 1.87. We deliberately do NOT early-return
    // on the handler result: this is an injected overlay that shares input with SWG, so
    // every message still falls through to SWG's original wndproc below. Capture
    // arbitration (suspending game input while hovering imgui) lives in render() via
    // DirectInput::suspend, not here.
    ImGui_ImplWin32_WndProcHandler(hwnd, msg, wParam, lParam);

    switch (msg)
    {
    case WM_KEYDOWN:
        // DIAG 2026-05-20 Issue #11: in-game Return + Esc dead while WASD
        // works. WASD comes from SWG's DirectInput keyboard polling; Return
        // / Esc must arrive via WM_KEYDOWN through this subclass. Log every
        // VK_RETURN / VK_ESCAPE that reaches us, with HWND + wantCaptureKbd
        // + foreground-HWND so we can tell (a) message reached subclass and
        // (b) what focus / imgui-capture state was at the time.
        if (wParam == VK_RETURN || wParam == VK_ESCAPE)
        {
            HWND fg = GetForegroundWindow();
            char m[160];
            snprintf(m, sizeof(m),
                     "hkWndProcHandler: WM_KEYDOWN vk=0x%02X hwnd=0x%p fg=0x%p io.WantCaptureKeyboard=%d io.WantTextInput=%d",
                     (unsigned)wParam, (void*)hwnd, (void*)fg,
                     io.WantCaptureKeyboard ? 1 : 0, io.WantTextInput ? 1 : 0);
            utinni::log::info(m);
        }
        // DIAG 2026-05-20 Issue #11 Phase D (per CODEX consult): F11
        // force-opens SWG's chat input via direct native call. Definitive
        // A/B: if F11 opens chat, chat mediator works and the bug is
        // upstream of it (in whatever normally translates in-game Enter
        // into chat-open). If F11 does nothing either, the mediator/window
        // itself is broken. F11 (0x7A) chosen because no SWG/TJT binding
        // uses it.
        if (wParam == VK_F11)
        {
            utinni::log::info("hkWndProcHandler: F11 pressed -- invoking CuiChatWindow::forceOpenChatInputFromCpp");
            utinni::CuiChatWindow::forceOpenChatInputFromCpp();
        }
        if (wParam == VK_F12)
        {
            // DIAG Phase F: dump the .bss action-name string slots of
            // SwgCuiChatWindow's dispatcher to identify which action
            // triggers each handler.
            utinni::log::info("hkWndProcHandler: F12 pressed -- dumping action string slots");
            utinni::CuiChatWindow::dumpActionStringSlotsFromCpp();
        }
        // PHASE G WORKAROUND REMOVED 2026-05-20: WM_KEYDOWN consumption
        // proved ineffective because SWG generates the Enter CUI event
        // via DirectInput keyboard polling, not via WM_KEYDOWN. Consuming
        // the Win32 message had no effect on SWG's input pipeline, so the
        // chatEnter dispatch fired anyway and closed our forceOpen.
        // Phase H replacement: detour the chatEnter handler itself
        // (0x00F3E420 in cui_chat_window.cpp) and override its display-
        // mode behavior to open chat instead of attempting a no-op
        // submit+close. That's the right semantic level -- we replace
        // SWG's broken context-routing behavior at the handler.
        break;
    case WM_CHAR:
        // DIAG 2026-05-20 Issue #11: log WM_CHAR for printable ASCII so we
        // can tell if typing letters into a (presumed-open) chat box works.
        // Filter to printable range to avoid log spam from CR/LF/etc.
        if (wParam >= 0x20 && wParam < 0x7F)
        {
            char m[96];
            snprintf(m, sizeof(m), "hkWndProcHandler: WM_CHAR '%c' (0x%X)",
                     (int)wParam, (unsigned)wParam);
            utinni::log::info(m);
        }
        break;
    case WM_ACTIVATE:
    {
        // DIAG 2026-05-20 Issue #11: who's focused. wParam LOWORD is WA_INACTIVE(0)
        // / WA_ACTIVE(1) / WA_CLICKACTIVE(2). If SWG window goes inactive when
        // user clicks it, that explains dead WM_KEYDOWN.
        unsigned wa = (unsigned)LOWORD(wParam);
        const char* name = (wa == 0) ? "INACTIVE" : (wa == 1) ? "ACTIVE"
                                                : (wa == 2)   ? "CLICKACTIVE"
                                                              : "?";
        char m[128];
        snprintf(m, sizeof(m), "hkWndProcHandler: WM_ACTIVATE %s (wParam=0x%X) hwnd=0x%p",
                 name, (unsigned)wParam, (void*)hwnd);
        utinni::log::info(m);
        break;
    }
    case WM_SETFOCUS:
    {
        char m[96];
        snprintf(m, sizeof(m), "hkWndProcHandler: WM_SETFOCUS hwnd=0x%p (lost-focus-from=0x%p)",
                 (void*)hwnd, (void*)wParam);
        utinni::log::info(m);
        break;
    }
    case WM_KILLFOCUS:
    {
        char m[96];
        snprintf(m, sizeof(m), "hkWndProcHandler: WM_KILLFOCUS hwnd=0x%p (gained-focus-to=0x%p)",
                 (void*)hwnd, (void*)wParam);
        utinni::log::info(m);
        break;
    }
    case WM_WINDOWPOSCHANGING:
    {
        // Constrain the embedded SWG window to the editor panel: SWG's own fullscreen toggle tries to
        // grow it to the screen; clamp any growth past the cached embed rect back to it so the panel
        // can't be covered. Only GROWTH is clamped (cx/cy beyond the embed) -- shrink/minimize and the
        // managed reposition (which pushes the rect first, so cx==embed) fall through. Rewrites the
        // caller's WINDOWPOS in place, then forwards to the original WndProc so the (clamped) move applies.
        if (g_embedClampValid)
        {
            WINDOWPOS* wp = (WINDOWPOS*)lParam;
            if (wp != nullptr && !(wp->flags & SWP_NOSIZE) &&
                (wp->cx > g_embedClampW || wp->cy > g_embedClampH))
            {
                wp->x = g_embedClampX;
                wp->y = g_embedClampY;
                wp->cx = g_embedClampW;
                wp->cy = g_embedClampH;
                static int s_clampLogCount = 0;
                if (s_clampLogCount < 8)
                {
                    s_clampLogCount++;
                    char m[128];
                    snprintf(m, sizeof(m),
                             "hkWndProcHandler: WM_WINDOWPOSCHANGING clamped SWG to embed (%d,%d %dx%d) -- blocked fullscreen grow",
                             g_embedClampX, g_embedClampY, g_embedClampW, g_embedClampH);
                    utinni::log::info(m);
                }
            }
        }
        break;
    }
    }

    return CallWindowProc(originalWndProcHandler, hwnd, msg, wParam, lParam);
}

bool isSetup = false;
void setup(HWND hwnd)
{
    if (isSetup)
        return;

    // Phase 18 / RNDR-01 (D-05 carve, review amendments 2/6/9): imgui_impl is now
    // DX9-API-neutral. The device-bearing GetCreationParameters + the imgui DX9
    // backend init moved into render_backend::Dx9Backend::init() (Plan 01). This
    // setup() takes a Win32 HWND (passed by hkPresent, extracted from its live
    // pDevice) and runs the LOCKED step order below. Guard: if hkPresent could not
    // produce a window or the backend cannot acquire a device, bail without
    // installing (no overlay rather than a crash).
    if (hwnd == nullptr)
        return;

    IMGUI_CHECKVERSION();
    // Idempotent: the DX11 hook-tier path (render_backend_dx11.cpp::Dx11Backend::init)
    // creates the ImGui context BEFORE setup() runs (ImGui_ImplDX11_Init needs it), so
    // only create here if one does not already exist -- avoids a double CreateContext on
    // the DX11 path while preserving the DX9 path (which reaches setup() with no context).
    if (ImGui::GetCurrentContext() == nullptr)
    {
        ImGui::CreateContext();
    }

    // (1) Install the active backend BEFORE isSetup flips true, so no seam call
    //     site can fire through the isSetup gate with a null backend.
    //
    //     Phase 19 / RNDR-03: one-backend-per-session detection (D-15/D-17). TWO
    //     entry paths reach setup(), and they are distinguished by whether a
    //     backend is ALREADY installed:
    //       * DX9 entry (directx9.cpp::hkPresent): arrives with NO backend
    //         installed (get() == nullptr). We run the selection here. The gl11
    //         module-presence check is a plain Win32 string call (passes the D-06
    //         gate). The advertised DXGI GetHookPoints contract can only be
    //         resolved by the DX11 hook tier (which would require the DX11 tier
    //         header -- banned in this TU), so on THIS entry path the contract bit is
    //         necessarily false: had the live DXGI swapchain been ready, the DX11
    //         hook tier's tryInstall() would have already latched the dx11
    //         backend and entered setup() via the DX11 path below. Routing the
    //         decision through the pure selectBackend() keeps it honest + the
    //         testable contract (Dx11DetectionTests) the live path also obeys.
    //       * DX11 entry (directx11.cpp::tryInstall, Plan 02): tryInstall() has
    //         ALREADY done render_backend::set(dx11Singleton()) +
    //         dx11Singleton()->init(device,context) from the hook tier BEFORE
    //         calling setup(hwnd).
    //         So get() is non-null and != dx9Singleton(); we must NOT re-set and
    //         must SKIP the DX9 step-2 init below.
    //     The one-shot diagnostic names the chosen backend (RNDR-03).
    static bool sLoggedBackend = false;
    if (render_backend::get() == nullptr)
    {
        // DX9 entry path: resolve the selection booleans WITHOUT naming any DX11
        // symbol. gl11Present is a module-presence check; the contract bit is
        // false here (see rationale above -- a ready DXGI swapchain would have
        // entered via the DX11 path, not this one).
        const bool gl11Present =
            (GetModuleHandleA("gl11_r.dll") != nullptr) || (GetModuleHandleA("gl11_d.dll") != nullptr);
        const render_backend::BackendChoice choice =
            render_backend::selectBackend(gl11Present, /*getHookPointsResolved=*/false);

        // selectBackend can only return Dx11 when BOTH booleans are true; the
        // contract bit is false on this entry path, so this resolves to Dx9. The
        // dx11 install is owned entirely by the DX11 hook tier (directx11.cpp),
        // kicked off from graphics.cpp::hkInstall (Plan 02) -- never from here.
        render_backend::set(render_backend::dx9Singleton());
        if (!sLoggedBackend)
        {
            sLoggedBackend = true;
            if (choice == render_backend::BackendChoice::Dx11)
            {
                // Unreachable on this entry path (contract bit false) -- present
                // for completeness so the selection is single-sourced.
                utinni::log::info("Render backend: D3D11 (gl11 detected, GetHookPoints advertised)");
            }
            else if (gl11Present)
            {
                utinni::log::warning("gl11 detected but GetHookPoints absent/not-ready; defaulting D3D9");
            }
            else
            {
                utinni::log::info("Render backend: D3D9 (default; no gl11 detected)");
            }
        }
    }
    else if (!sLoggedBackend)
    {
        // DX11 entry path: the hook tier already installed the dx11 backend.
        // Emit the one-shot diagnostic naming it; do NOT re-set the backend.
        sLoggedBackend = true;
        utinni::log::info("Render backend: D3D11 (gl11 detected, GetHookPoints advertised)");
    }

    // (2) Drive the non-virtual device-bearing init -- ONLY when the installed
    //     backend is the DX9 singleton. The live device that hkPresent stashed on
    //     the singleton is init()'s PRIMARY source (review amendment 6); the
    //     captured-device fallback is used only if none stashed. init() does the
    //     creation-parameters query + the single imgui DX9 renderer init
    //     internally and returns hFocusWindow. If it returns null no device was
    //     available -- undo the partial install and bail (no overlay rather than a
    //     half-installed one).
    //
    //     Phase 19 hand-off (Plan 02): on the DX11 entry path the device init
    //     ALREADY ran inside the hook tier's install poll (dx11Singleton()->init(
    //     device, context)) and no D3D9 device exists in a D3D11 client -- calling
    //     dx9Singleton()->init(nullptr) would bail at the null guard and leave a
    //     dead overlay. So this step is GUARDED on the installed backend being the
    //     dx9 singleton; the DX11 path skips it and runs the HWND-bearing tail
    //     with the passed-in hwnd (the swapchain-derived window).
    if (render_backend::get() == render_backend::dx9Singleton())
    {
        const HWND backendWindow = render_backend::dx9Singleton()->init(nullptr);
        if (backendWindow == nullptr)
        {
            // WR-01: symmetric partial-init teardown. At this bail point the only
            // imgui-side state that has been created is the ImGuiContext from the
            // CreateContext() above (Win32 platform init is step (4), still ahead
            // of us, so there is no ImGui_ImplWin32_Shutdown to issue yet).
            // Destroying the context here -- and undoing the step (1) backend
            // install -- means a later frame re-enters setup() from a CLEAN slate
            // instead of stacking a second CreateContext() on a leaked first one
            // (isSetup stays false on bail).
            render_backend::set(nullptr);
            ImGui::DestroyContext();
            return;
        }
    }

    // (3) Issue #10 Phase A: publish SWG's actual top-level HWND to the managed
    //     side so PanelGame can find it for SetParent reparenting. This MUST
    //     survive the carve (consumed managed-side by PanelGame).
    utinni::Client::setSwgHwnd(hwnd);

    // (4) Win32 platform backend init -- MUST come before the DX9 renderer init
    //     (imgui requires platform-before-renderer). The DX9 renderer init already
    //     completed inside step 2's init(); never reorder Win32 ahead of set/init.
    ImGui_ImplWin32_Init(hwnd);

    ImGui::GetIO().ConfigFlags |= ImGuiConfigFlags_DockingEnable;

    ImGui::GetIO().WantCaptureKeyboard = true;
    ImGui::GetIO().WantCaptureMouse = true;
    ImGui::GetIO().WantTextInput = true;
    // (6) WndProc subclass install (Issue #11 chat-context routing) -- verbatim.
    originalWndProcHandler = (WNDPROC)SetWindowLongPtr(hwnd, GWL_WNDPROC, (LONG)hkWndProcHandler);

    ImGui::GetIO().Fonts->AddFontFromFileTTF("C:/Windows/Fonts/micross.ttf", 14);

    ImGuiStyle& style = ImGui::GetStyle();
    style.Colors[ImGuiCol_Text] = ImVec4(1.00f, 1.00f, 1.00f, 1.00f); // ImVec4(0.78f, 0.78f, 0.78f, 1.00f);
    style.Colors[ImGuiCol_TextDisabled] = ImVec4(0.59f, 0.59f, 0.59f, 1.00f);
    style.Colors[ImGuiCol_WindowBg] = ImVec4(0.13f, 0.13f, 0.13f, 1.00f);
    style.Colors[ImGuiCol_ChildBg] = ImVec4(0.11f, 0.11f, 0.11f, 1.00f);
    style.Colors[ImGuiCol_Border] = ImVec4(0.20f, 0.20f, 0.20f, 1.00f);
    style.Colors[ImGuiCol_FrameBg] = ImVec4(0.80f, 0.80f, 0.80f, 0.09f);
    style.Colors[ImGuiCol_FrameBgHovered] = ImVec4(0.39f, 0.39f, 0.39f, 1.00f);
    style.Colors[ImGuiCol_FrameBgActive] = ImVec4(0.04f, 0.04f, 0.04f, 0.88f);
    style.Colors[ImGuiCol_TitleBg] = ImVec4(0.00f, 0.00f, 0.00f, 1.00f);
    style.Colors[ImGuiCol_TitleBgCollapsed] = ImVec4(0.00f, 0.00f, 0.00f, 0.20f);
    style.Colors[ImGuiCol_TitleBgActive] = ImVec4(0.00f, 0.48f, 0.80f, 1.00f); // ImVec4(0.00f, 0.00f, 0.00f, 1.00f);
    style.Colors[ImGuiCol_MenuBarBg] = ImVec4(0.35f, 0.35f, 0.35f, 1.00f);
    style.Colors[ImGuiCol_ScrollbarBg] = ImVec4(0.13f, 0.13f, 0.13f, 1.00f);
    style.Colors[ImGuiCol_ScrollbarGrab] = ImVec4(0.80f, 0.80f, 0.80f, 0.09f);        // ImVec4(0.24f, 0.40f, 0.95f, 1.00f);
    style.Colors[ImGuiCol_ScrollbarGrabHovered] = ImVec4(0.80f, 0.80f, 0.80f, 0.09f); // ImVec4(0.24f, 0.40f, 0.95f, 0.59f);
    style.Colors[ImGuiCol_ScrollbarGrabActive] = ImVec4(0.00f, 0.00f, 0.00f, 1.00f);
    style.Colors[ImGuiCol_SliderGrab] = ImVec4(1.00f, 1.00f, 1.00f, 0.59f);
    style.Colors[ImGuiCol_SliderGrabActive] = ImVec4(0.80f, 0.80f, 0.80f, 0.09f); // ImVec4(0.24f, 0.40f, 0.95f, 1.00f);
    style.Colors[ImGuiCol_Button] = ImVec4(0.80f, 0.80f, 0.80f, 0.09f);           // ImVec4(0.24f, 0.40f, 0.95f, 1.00f);
    style.Colors[ImGuiCol_ButtonHovered] = ImVec4(0.39f, 0.39f, 0.39f, 1.00f);    // ImVec4(0.24f, 0.40f, 0.95f, 0.59f);
    style.Colors[ImGuiCol_ButtonActive] = ImVec4(0.04f, 0.04f, 0.04f, 0.88f);     // ImVec4(0.39f, 0.39f, 0.39f, 1.00f);
    style.Colors[ImGuiCol_Header] = ImVec4(0.00f, 0.48f, 0.80f, 1.00f);           // ImVec4(0.24f, 0.40f, 0.95f, 1.00f);
    style.Colors[ImGuiCol_HeaderHovered] = ImVec4(0.80f, 0.80f, 0.80f, 0.09f);    // ImVec4(0.24f, 0.40f, 0.95f, 0.59f);
    style.Colors[ImGuiCol_HeaderActive] = ImVec4(0.39f, 0.39f, 0.39f, 1.00f);

    style.WindowRounding = 0.f;
    style.FramePadding = ImVec2(4, 1);
    style.ScrollbarSize = 10.f;
    style.ScrollbarRounding = 0.f;
    style.GrabMinSize = 5.f;

    isSetup = true;

    // DIAG 2026-05-23 Plan 06-01 Task 1: one-shot probe that fires the first
    // time isSetup flips true. Pair with the render() probe below to confirm
    // both the imgui setup path AND the per-frame render-gate are reached in
    // a live SWG-injected session. Static-bool guarded — fires exactly once.
    // Per 06-01-DEMO-PROBE-NOTES.md: the d3d9 pattern-scan suspicion is
    // resolved (Phase 02.1 commit 2c57d38 replaced it with the dummy-device
    // approach), so this probe is the next link in the chain.
    // Task 3 sign-off (2026-05-23): demoted from info -> debug; kept behind
    // the one-shot guard so the diag stays dormant in normal runs but
    // surfaces in debug logs if the regression returns.
    static bool sLoggedOnce = false;
    if (!sLoggedOnce)
    {
        sLoggedOnce = true;
        utinni::log::debug("imgui_impl::setup complete, isSetup=true");
    }
}

int GetWidth()
{
    return Graphics::getCurrentRenderTargetWidth() / 3;
}

int GetHeight()
{
    return Graphics::getCurrentRenderTargetHeight() / 3;
}

void DrawDepthWindow()
{
    // D-05 carve: the former depth-texture reach-in folds into the seam accessor.
    // sceneDepthTexture() returns 0 when no live depth+color texture exists (the
    // accessor already encodes the old null guard), so a 0 handle is the "no
    // texture" signal here. Null-guard the nullable seam first.
    if (render_backend::get() == nullptr || render_backend::get()->sceneDepthTexture() == 0 || utinni::Game::getPlayer() == nullptr)
        return;

    ImVec2 size2(GetWidth() + 5, GetHeight() + 31);
    ImGui::SetNextWindowSize(size2);
    if (ImGui::Begin("Depth", 0, ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoCollapse))
    {
        ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(0, 0));
        const ImVec2 pos = ImGui::GetCursorScreenPos();
        const ImVec2 posMax(pos.x + GetWidth(), pos.y + GetHeight());

        ImGui::SetNextWindowSize(size2);
        ImGui::BeginChild("GameWindow");

        ImGui::GetWindowDrawList()->AddImage(render_backend::get()->sceneDepthTexture(), pos, posMax);

        ImGui::EndChild();

        ImGui::SetWindowSize(size2);
        ImGui::PopStyleVar();
    }
    ImGui::End(); // Begin() must always be paired with End(), even when Begin() returns false
}

void DrawColorWindow()
{
    // D-05 carve (A2): the former color-texture reach-in folds into the seam
    // accessor. sceneColorTexture() returns 0 when no live texture exists.
    // Null-guard the nullable seam first.
    if (render_backend::get() == nullptr || render_backend::get()->sceneColorTexture() == 0 || utinni::Game::getPlayer() == nullptr)
        return;

    ImVec2 size2(GetWidth() + 5, GetHeight() + 31);
    ImGui::SetNextWindowSize(size2);
    if (ImGui::Begin("Color", 0, ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoCollapse))
    {
        ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(0, 0));
        const ImVec2 pos = ImGui::GetCursorScreenPos();
        const ImVec2 posMax(pos.x + GetWidth(), pos.y + GetHeight());

        ImGui::SetNextWindowSize(size2);
        ImGui::BeginChild("GameWindow");
        ImGui::GetWindowDrawList()->AddImage(render_backend::get()->sceneColorTexture(), pos, posMax);
        ImGui::EndChild();

        ImGui::SetWindowSize(size2);
        ImGui::PopStyleVar();
    }
    ImGui::End(); // Begin() must always be paired with End(), even when Begin() returns false
}

static bool imguiHasHover = false;
bool gameInputSuspended = false;
void render()
{
    if (isSetup)
    {
        // DIAG 2026-05-23 Plan 06-01 Task 1: one-shot probe that fires the
        // first time render() actually crosses the isSetup gate. Static-
        // bool guarded; debug-level so it never floods utinni.log. Pair
        // with the setup-complete probe above to confirm the per-frame
        // render path is alive in live SWG sessions. See
        // 06-01-DEMO-PROBE-NOTES.md for the d3d9 pattern-scan disposition.
        static bool sLoggedOnceRender = false;
        if (!sLoggedOnceRender)
        {
            sLoggedOnceRender = true;
            utinni::log::debug("imgui_impl::render entered isSetup branch");
        }

        rendering = true;
        // D-05 carve: route the renderer new-frame through the seam (null-guarded).
        if (auto* b = render_backend::get())
            b->newFrame();
        ImGui_ImplWin32_NewFrame();

        // Issue #10 reparent/stretch fix: the embedded SWG window is resized to fit
        // the editor panel, but SWG's D3D9 backbuffer keeps its created size (we do
        // NOT Reset a third-party device -- see feedback-d3d9-reset-third-party), so
        // windowed Present UNIFORMLY stretches the backbuffer up to the client
        // (scale = client / renderTarget). imgui can only draw into that backbuffer,
        // so we make imgui operate ENTIRELY in render-target space -- both the layout
        // (DisplaySize) and the mouse -- and let the present-stretch map the whole UI
        // to the client uniformly. This keeps hit-testing, mouse-anchored UI (popups),
        // ImGuizmo (already RT-space via SetRect), and the reachable extent all in one
        // consistent space. Two halves, both required:
        //   1. DisplaySize = render target: lay out in backbuffer space so the entire
        //      UI is visible (not clipped past the backbuffer width) AND reachable
        //      (the cursor below also maxes at the render-target width).
        //   2. Mouse = cursor scaled into render-target space: cancels the present-
        //      stretch so the cursor lands under what the user sees.
        // No-op when client == render target (standalone, non-embedded window).
        //
        // imgui 1.87+ is event-queue based: ImGui_ImplWin32_NewFrame() only QUEUES the
        // raw mouse-pos event; io.MousePos isn't written until ImGui::NewFrame() drains
        // the queue. So poking io.MousePos here would be clobbered. Instead we queue a
        // corrected event AFTER the backend's (last-event-wins), built from the raw
        // cursor scaled into RT space. Only override while the cursor is inside the
        // client, so the backend's WM_MOUSELEAVE handling still works at the edges.
        // (DisplaySize, by contrast, is set directly by the backend, so overriding it
        // here sticks.)
        {
            ImGuiIO& io = ImGui::GetIO();
            HWND hwnd = utinni::Client::getSwgHwnd();
            const float rtw = (float)Graphics::getCurrentRenderTargetWidth();
            const float rth = (float)Graphics::getCurrentRenderTargetHeight();
            const float clientW = io.DisplaySize.x; // client px, set directly by ImGui_ImplWin32_NewFrame
            const float clientH = io.DisplaySize.y;
            if (rtw > 0.0f && rth > 0.0f && clientW > 0.0f && clientH > 0.0f)
            {
                POINT p;
                if (hwnd != nullptr && GetCursorPos(&p) && ScreenToClient(hwnd, &p) && p.x >= 0 && p.y >= 0 && (float)p.x <= clientW && (float)p.y <= clientH)
                {
                    io.AddMousePosEvent((float)p.x * rtw / clientW, (float)p.y * rth / clientH);

                    // Wave-3 gizmo hit-test calibration diag (advertised only): the RT-space mouse
                    // scale + SetRect were written for the D3D9 backbuffer-stretch; the DX11 embed's
                    // render-target/window relationship differs (gizmo moves vertically but H-axis
                    // hit-test is off + vanishes near the bottom). Log the exact dims/scale while the
                    // gizmo is active so the next session calibrates on numbers, not guesses. Bounded.
                    if (swg::endpoints::isAdvertisedClient() && imgui_gizmo::isEnabled())
                    {
                        static std::atomic<int> s_gizmoDiagCount{0};
                        const int n = s_gizmoDiagCount.fetch_add(1, std::memory_order_relaxed);
                        if (n < 20 && (n % 4) == 0)
                        {
                            // Window-handle audit: the mouse is ScreenToClient'd against
                            // Client::getSwgHwnd(); ImGui measures DisplaySize (=clientW/H) from the
                            // swapchain's own HWND. If those windows differ in the embedded/reparented
                            // maximized-Utinni setup, the cursor space and the layout space don't line
                            // up -> hit-test off, machine-independently. Log both hwnds + swgHwnd's own
                            // client rect to compare against DisplaySize (clientW/H). No guessing.
                            RECT swgRc = {0, 0, 0, 0};
                            if (hwnd != nullptr)
                            {
                                GetClientRect(hwnd, &swgRc);
                            }
                            char m[320];
                            std::snprintf(m, sizeof(m),
                                          "gizmo-diag: RT=%.0fx%.0f DisplaySize=%.0fx%.0f swgHwnd=0x%p swgClient=%ldx%ld rawCursor=(%ld,%ld) scaledMouse=(%.1f,%.1f)",
                                          rtw, rth, clientW, clientH, (void*)hwnd,
                                          swgRc.right - swgRc.left, swgRc.bottom - swgRc.top, p.x, p.y,
                                          (float)p.x * rtw / clientW, (float)p.y * rth / clientH);
                            utinni::log::info(m);
                        }
                    }
                }
                io.DisplaySize = ImVec2(rtw, rth); // lay out in render-target space (see above)
            }
        }

        ImGui::NewFrame();

        // DIAG 2026-05-23 Plan 06-01 Task 2: additive ShowDemoWindow probe
        // gated behind g_showDemoWindowProbe (file-scope). Exercises the
        // full imgui Demo screen (menus + sliders + buttons + tabs + plots
        // + popups + drag-and-drop) over live SWG to confirm render + input
        // + state-mgmt end-to-end. Additive — the renderCallbacks dispatch
        // below (in the !enableUi branch) is untouched.
        if (g_showDemoWindowProbe)
        {
            ImGui::ShowDemoWindow(nullptr);
        }

        static bool showDepthWindow = false;
        static bool showColorWindow = false;
        if (!enableUi)
        {
            ImGui::Begin("Tests", 0, ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoCollapse); // ImVec2(250, 300), 0.9f,  ImGuiWindowFlags_NoResize |
            {
                if (ImGui::Button("Test"))
                {
                    // CuiMediatorFactory::activate("Testzzz");
                }

                // D-05 carve (A2 + review amendment 4): the former depth-texture
                // reach-in folds into the seam. The seam's sceneDepthStage() returns
                // an int and cannot signal "no depth texture", so this dev-only block
                // is gated on sceneDepthTexture() != 0 instead. This is INTENTIONALLY
                // STRICTER than the pre-carve `depthTex != nullptr` guard (the
                // accessor also requires a live COLOR texture), so the block stays
                // hidden in more cases than before -- a DELIBERATE dev-only behavior
                // change, NOT verbatim. Release (enableInternalUi=true) is unaffected:
                // this whole block is behind `if (!enableUi)` above.
                if (render_backend::get() != nullptr && render_backend::get()->sceneDepthTexture() != 0)
                {
                    if (ImGui::Checkbox("Show Depth Window", &showDepthWindow))
                    {
                    }
                    if (ImGui::Checkbox("Show Color Window", &showColorWindow))
                    {
                    }

                    int stage = render_backend::get()->sceneDepthStage();
                    if (ImGui::SliderInt("Stage", &stage, 0, 11))
                    {
                        render_backend::get()->setSceneDepthStage(stage);
                    }
                }
            }

            // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot.
            // Stack-snapshot via dispatchSnapshot keeps the per-frame path
            // heap-free.
            // ToDo add an additional callback to host controls in the
            // future main ImGui window.
            dispatchSnapshot(renderCallbacks, renderCallbacksMutex,
                             [](void (*func)())
                             { func(); });
        }

        if (showDepthWindow)
        {
            DrawDepthWindow();
        }

        if (showColorWindow)
        {
            DrawColorWindow();
        }

        imgui_gizmo::draw();

        // Use io.WantCaptureMouse, NOT IsWindowHovered(AnyWindow), to arbitrate game-input
        // suspend. WantCaptureMouse stays true for the FULL duration of an active drag
        // (slider, text selection, window move, drag-and-drop) even after the cursor leaves
        // the window bounds; IsWindowHovered drops the instant the cursor exits. The old
        // hover gate therefore resumed SWG's DirectInput mouse polling mid-drag, letting the
        // still-held button drive SWG's marquee drag-select ("select everything to the left
        // of the cursor"). WantCaptureMouse also covers open popups/modals. (Tier-4 06-06 UAT finding.)
        imguiHasHover = ImGui::GetIO().WantCaptureMouse;
        if (imguiHasHover && !gameInputSuspended)
        {
            gameInputSuspended = true;
            DirectInput::suspend();
            Graphics::showMouseCursor(false);
            SetCursor(LoadCursor(nullptr, IDC_ARROW));
        }
        else if (!imguiHasHover && gameInputSuspended)
        {
            gameInputSuspended = false;
            DirectInput::resume();
            Graphics::showMouseCursor(true);
            SetCursor(nullptr);
        }

        // The "Tests" window's Begin() above is gated on !enableUi, so its End()
        // must be gated identically. With enableInternalUi=true (e.g. Release
        // ut.ini) Begin() is skipped, so an unconditional End() here pops the
        // window stack too far -> imgui 1.92 error recovery reports "Calling End()
        // too many times!" against the implicit Debug##Default window.
        if (!enableUi)
            ImGui::End();

        ImGui::EndFrame();
        ImGui::Render();
        // D-05 carve: route draw-data submit through the seam (null-guarded).
        if (auto* b = render_backend::get())
            b->renderDrawData(ImGui::GetDrawData());
        rendering = false;
    }
}

bool isRendering()
{
    return rendering;
}

// WR-04: one-shot setup gate accessor. hkPresent uses this to stop re-running
// the per-frame device-stash + GetCreationParameters COM call once the overlay
// has been installed. Reflects the same `isSetup` latch setup() flips true.
bool isReady()
{
    return isSetup;
}

// Phase 3 R-A: handle-based Subscribe/Unsubscribe per D-08/D-09.
int subscribeRenderCallback(void (*func)())
{
    std::lock_guard<std::mutex> guard(renderCallbacksMutex);
    int id = s_nextRenderId++;
    if (id == 0)
    {
        id = s_nextRenderId++;
    } // WR-04 skip-zero
    renderCallbacks.push_back({id, func});
    return id;
}

bool unsubscribeRenderCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(renderCallbacksMutex);
    for (auto it = renderCallbacks.begin(); it != renderCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            renderCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

void addRenderCallback(void (*func)())
{
    subscribeRenderCallback(func);
}

bool isInternalUiHovered()
{
    return imguiHasHover;
}

} // namespace imgui_impl

// Phase 3 R-A native-side (per 03-CONTEXT D-08/D-09): handle-based registries
// backed by insertion-order std::vector<{handle, fn_ptr}>.
// CR-01 (03-REVIEW): per-registry mutex protects Subscribe / Unsubscribe / snapshot.
static std::vector<CallbackEntry<void (*)()>> onGizmoEnabledCallbacks;
static std::vector<CallbackEntry<void (*)()>> onGizmoDisabledCallbacks;
static std::vector<CallbackEntry<void (*)()>> onGizmoPositionChangedCallbacks;
static std::vector<CallbackEntry<void (*)()>> onGizmoRotationChangedCallbacks;
static std::mutex onGizmoEnabledCallbacksMutex;
static std::mutex onGizmoDisabledCallbacksMutex;
static std::mutex onGizmoPositionChangedCallbacksMutex;
static std::mutex onGizmoRotationChangedCallbacksMutex;
static int s_nextGizmoEnabledId = 1;
static int s_nextGizmoDisabledId = 1;
static int s_nextGizmoPositionChangedId = 1;
static int s_nextGizmoRotationChangedId = 1;

namespace imgui_gizmo
{
bool enabled = false;
bool gizmoHasMouseHover = false;
bool wasUsed = false;

static Transform originalTransform;
static Object* object = nullptr;

static ImGuizmo::MODE gizmoMode(ImGuizmo::MODE::LOCAL);
static ImGuizmo::OPERATION operationMode(ImGuizmo::TRANSLATE);

static bool useSnap = false;
static float snap[3] = {1, 1, 1};

void enable(Object* obj)
{
    object = obj;
    enabled = true;

    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot. Stack-snapshot
    // via dispatchSnapshot keeps the path heap-free.
    dispatchSnapshot(onGizmoEnabledCallbacks, onGizmoEnabledCallbacksMutex,
                     [](void (*func)())
                     { func(); });
}

void disable()
{
    enabled = false;
    object = nullptr;

    // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot. Stack-snapshot
    // via dispatchSnapshot keeps the path heap-free.
    dispatchSnapshot(onGizmoDisabledCallbacks, onGizmoDisabledCallbacksMutex,
                     [](void (*func)())
                     { func(); });

    // Ensure it's set to false in case the gizmo is disabled with mouse hovered
    gizmoHasMouseHover = false;
}

bool isEnabled()
{
    return enabled;
}

bool hasMouseHover()
{
    return gizmoHasMouseHover;
}

// Phase 3 R-A: handle-based Subscribe/Unsubscribe per D-08/D-09.
int subscribeOnEnabledCallback(void (*func)())
{
    std::lock_guard<std::mutex> guard(onGizmoEnabledCallbacksMutex);
    int id = s_nextGizmoEnabledId++;
    if (id == 0)
    {
        id = s_nextGizmoEnabledId++;
    } // WR-04 skip-zero
    onGizmoEnabledCallbacks.push_back({id, func});
    return id;
}

bool unsubscribeOnEnabledCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(onGizmoEnabledCallbacksMutex);
    for (auto it = onGizmoEnabledCallbacks.begin(); it != onGizmoEnabledCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            onGizmoEnabledCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

int subscribeOnDisabledCallback(void (*func)())
{
    std::lock_guard<std::mutex> guard(onGizmoDisabledCallbacksMutex);
    int id = s_nextGizmoDisabledId++;
    if (id == 0)
    {
        id = s_nextGizmoDisabledId++;
    } // WR-04 skip-zero
    onGizmoDisabledCallbacks.push_back({id, func});
    return id;
}

bool unsubscribeOnDisabledCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(onGizmoDisabledCallbacksMutex);
    for (auto it = onGizmoDisabledCallbacks.begin(); it != onGizmoDisabledCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            onGizmoDisabledCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

int subscribeOnPositionChangedCallback(void (*func)())
{
    std::lock_guard<std::mutex> guard(onGizmoPositionChangedCallbacksMutex);
    int id = s_nextGizmoPositionChangedId++;
    if (id == 0)
    {
        id = s_nextGizmoPositionChangedId++;
    } // WR-04 skip-zero
    onGizmoPositionChangedCallbacks.push_back({id, func});
    return id;
}

bool unsubscribeOnPositionChangedCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(onGizmoPositionChangedCallbacksMutex);
    for (auto it = onGizmoPositionChangedCallbacks.begin(); it != onGizmoPositionChangedCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            onGizmoPositionChangedCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

int subscribeOnRotationChangedCallback(void (*func)())
{
    std::lock_guard<std::mutex> guard(onGizmoRotationChangedCallbacksMutex);
    int id = s_nextGizmoRotationChangedId++;
    if (id == 0)
    {
        id = s_nextGizmoRotationChangedId++;
    } // WR-04 skip-zero
    onGizmoRotationChangedCallbacks.push_back({id, func});
    return id;
}

bool unsubscribeOnRotationChangedCallback(int handle)
{
    if (handle == 0)
    {
        return false;
    }
    std::lock_guard<std::mutex> guard(onGizmoRotationChangedCallbacksMutex);
    for (auto it = onGizmoRotationChangedCallbacks.begin(); it != onGizmoRotationChangedCallbacks.end(); ++it)
    {
        if (it->handle == handle)
        {
            onGizmoRotationChangedCallbacks.erase(it);
            return true;
        }
    }
    return false;
}

void addOnEnabledCallback(void (*func)())
{
    subscribeOnEnabledCallback(func);
}

void addOnDisabledCallback(void (*func)())
{
    subscribeOnDisabledCallback(func);
}

void addOnPositionChangedCallback(void (*func)())
{
    subscribeOnPositionChangedCallback(func);
}

void addOnRotationChangedCallback(void (*func)())
{
    subscribeOnRotationChangedCallback(func);
}

void toggleGizmoMode()
{
    if (gizmoMode != ImGuizmo::MODE::LOCAL)
    {
        gizmoMode = ImGuizmo::MODE::LOCAL;
    }
    else
    {
        gizmoMode = ImGuizmo::MODE::WORLD;
    }
}

void toggleOperationMode()
{
    if (operationMode != ImGuizmo::TRANSLATE)
    {
        operationMode = ImGuizmo::TRANSLATE;
    }
    else
    {
        operationMode = ImGuizmo::ROTATE;
    }
}

void setGizmoModeToWorld()
{
    gizmoMode = ImGuizmo::MODE::WORLD;
}

void setGizmoModeToLocal()
{
    gizmoMode = ImGuizmo::MODE::LOCAL;
}

bool isGizmoModeToLocal()
{
    return gizmoMode == ImGuizmo::MODE::LOCAL;
}

bool isGizmoModeToWorld()
{
    return gizmoMode == ImGuizmo::MODE::WORLD;
}

void setOperationModeToTranslate()
{
    operationMode = ImGuizmo::TRANSLATE;
}

void setOperationModeToRotate()
{
    operationMode = ImGuizmo::ROTATE;
}

bool isOperationModeTransform()
{
    return operationMode == ImGuizmo::TRANSLATE;
}

bool isOperationModeRotate()
{
    return operationMode == ImGuizmo::ROTATE;
}

void toggleSnap()
{
    useSnap = !useSnap;
}

void enableSnap(bool value)
{
    useSnap = value;
}

bool isSnapOn()
{
    return useSnap;
}

void setSnapSize(float value)
{
    snap[0] = value;
    snap[1] = value;
    snap[2] = value;
}

void editTransform(const float* cameraView, float* cameraProjection, float* matrix)
{
    static float bounds[] = {-0.5f, -0.5f, -0.5f, 0.5f, 0.5f, 0.5f};
    static float boundsSnap[] = {0.1f, 0.1f, 0.1f};
    static bool boundSizing = false;
    static bool boundSizingSnap = false;

    ImGuizmo::Manipulate(cameraView, cameraProjection, operationMode, gizmoMode, matrix, nullptr, useSnap ? &snap[0] : nullptr, boundSizing ? bounds : nullptr, boundSizingSnap ? boundsSnap : nullptr);
}

void draw()
{
    if (object == nullptr || !enabled)
    {
        return;
    }

    // Camera matrices: on the advertised client the SWGEmu path (GroundScene::get()->getCurrentCamera()
    // + camera->projectionMatrix RAW struct-offset read) is NGE-unsafe -- the Wave-2 §5.6 probe crashed
    // executing heap data from the mis-offset projection field (cdb-confirmed). v19 riders replace those
    // two reads with advertised accessors (camera::getTransformO2W / getProjectionMatrix, both off
    // Game::getConstCamera). The OBJECT transform stays object->getTransform_o2w() (advertised row).
    Transform cameraO2W;
    Matrix4x4 projection;
    if (swg::endpoints::isAdvertisedClient())
    {
        if (swg::camera::getTransformO2W == nullptr || swg::camera::getProjectionMatrix == nullptr)
        {
            return; // rows unresolved -> gizmo stays dark rather than reading raw NGE layout
        }

        float o2w12[12];
        float proj16[16];
        if (swg::camera::getTransformO2W(o2w12) == 0 || swg::camera::getProjectionMatrix(proj16) == 0)
        {
            return; // no current camera this frame
        }

        static_assert(sizeof(cameraO2W.matrix) == sizeof(o2w12), "camera o2w must be the 12-float 3x4 contract");
        memcpy(cameraO2W.matrix, o2w12, sizeof(o2w12));
        static_assert(sizeof(projection.matrix) == sizeof(proj16), "projection must be the 16-float 4x4 contract");
        memcpy(projection.matrix, proj16, sizeof(proj16));
    }
    else
    {
        if (GroundScene::get() == nullptr)
        {
            return;
        }
        Camera* camera = GroundScene::get()->getCurrentCamera();
        cameraO2W = Transform(*camera->getTransform_o2w());
        projection = camera->projectionMatrix;
    }

    // Set up the matrices for the gizmo
    Transform w2c;
    w2c.invert(cameraO2W);
    const Matrix4x4 view = Matrix4x4(w2c);
    const Matrix4x4 objectMatrix = Matrix4x4(*object->getTransform_o2w());

    float* viewMatrix = &Matrix4x4().matrix[0][0];
    float* projMatrix = &Matrix4x4().matrix[0][0];
    float* objMatrix = &Matrix4x4().matrix[0][0];

    Matrix4x4::transpose(&view.matrix[0][0], viewMatrix);
    Matrix4x4::transpose(&projection.matrix[0][0], projMatrix);
    Matrix4x4::transpose(&objectMatrix.matrix[0][0], objMatrix);

    // Enable and draw the gizmo. NOTE (2026-07-18): the advertised DX11 embed has an unresolved
    // window-scaling problem between the backing canvas (render target) and the window client area;
    // the mouse↔gizmo mapping is being fixed EXACTLY (not by derived-aspect guesses) -- see
    // [[feedback_gizmo_no_half_working]]. Baseline full-RT SetRect restored here while the exact
    // coordinate pipeline is instrumented (below) + a provider camera::getViewport row is requested.
    const float setRectW = Graphics::getCurrentRenderTargetWidth();
    const float setRectH = Graphics::getCurrentRenderTargetHeight();

    ImGuizmo::SetRect(0, 0, setRectW, setRectH);
    ImGuizmo::BeginFrame();
    ImGuizmo::Enable(true);
    editTransform(viewMatrix, projMatrix, objMatrix);

    gizmoHasMouseHover = ImGuizmo::IsOver();

    if (ImGuizmo::IsUsing())
    {
        // If the mouse went up or it's the initial use, store the original transform
        if (!wasUsed)
        {
            originalTransform = Transform(*object->getTransform_o2w());
        }

        if (ImGui::IsKeyDown(ImGuiKey_Escape))
        {
            object->setTransform_o2w(originalTransform);
            Vector originalPos = originalTransform.getPosition();
            object->positionAndRotationChanged(false, originalPos);
            wasUsed = false;
            ImGuizmo::Enable(false);
            ImGuizmo::Enable(true);
            return;
        }

        // Pass the updated matrix back to the object
        float* updatedObjMatrix = &Matrix4x4().matrix[0][0];
        Matrix4x4::transpose(objMatrix, updatedObjMatrix);

        Transform previousTransform = Transform(*object->getTransform_o2w());
        object->setTransform_o2w(*(Transform*)updatedObjMatrix);
        Vector oldPos = previousTransform.getPosition();
        object->positionAndRotationChanged(false, oldPos);

        wasUsed = true;
    }
    else
    {
        // If there the gizmo was used and there was a change, notify the callbacks
        if (wasUsed)
        {
            if (originalTransform.getPosition() != object->getTransform_o2w()->getPosition())
            {
                // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot.
                dispatchSnapshot(onGizmoPositionChangedCallbacks, onGizmoPositionChangedCallbacksMutex,
                                 [](void (*func)())
                                 { func(); });
            }

            if (!object->getTransform_o2w()->isRotationEqual(originalTransform))
            {
                // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot.
                dispatchSnapshot(onGizmoRotationChangedCallbacks, onGizmoRotationChangedCallbacksMutex,
                                 [](void (*func)())
                                 { func(); });
            }
        }
        wasUsed = false;
    }
}
} // namespace imgui_gizmo
