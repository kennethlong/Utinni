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
#include <imgui_impl_dx9.h>
#include <ImGuizmo.h>

#include <cstdio>
#include <mutex>
#include <vector>

#include "utility/log.h"
#include "swg/ui/cui_chat_window.h"
#include "swg/game/game.h"
#include "swg/scene/ground_scene.h"
#include "swg/client/client.h"

#include "swg/graphics/graphics.h"
#include "swg/misc/direct_input.h"
#include "swg/scene/ground_scene.h"
#include "swg/misc/network.h"
#include "swg/misc/config.h"
#include "command_parser.h"
#include "cui_io.h"
#include "swg/graphics/directx9.h"
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
    }

    return CallWindowProc(originalWndProcHandler, hwnd, msg, wParam, lParam);
}

bool isSetup = false;
void setup(IDirect3DDevice9* pDevice)
{
    if (isSetup)
        return;

    D3DDEVICE_CREATION_PARAMETERS cParam;

    pDevice->GetCreationParameters(&cParam);

    // 2026-05-20 Issue #10 Phase A: publish SWG's actual top-level HWND
    // (D3D9's focus window) to the managed side so PanelGame can find it
    // for SetParent reparenting. Pure plumbing -- no behavior change yet.
    utinni::Client::setSwgHwnd(cParam.hFocusWindow);

    IMGUI_CHECKVERSION();
    ImGui::CreateContext();
    ImGui_ImplWin32_Init(cParam.hFocusWindow);
    ImGui_ImplDX9_Init(pDevice);

    ImGui::GetIO().ConfigFlags |= ImGuiConfigFlags_DockingEnable;

    ImGui::GetIO().WantCaptureKeyboard = true;
    ImGui::GetIO().WantCaptureMouse = true;
    ImGui::GetIO().WantTextInput = true;
    originalWndProcHandler = (WNDPROC)SetWindowLongPtr(cParam.hFocusWindow, GWL_WNDPROC, (LONG)hkWndProcHandler);

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
    auto depthTex = directX::getDepthTexture();
    if (depthTex == nullptr || depthTex->getTextureColor() == nullptr || utinni::Game::getPlayer() == nullptr)
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

        ImGui::GetWindowDrawList()->AddImage((void*)depthTex->getTextureDepth(), pos, posMax);

        ImGui::EndChild();

        ImGui::SetWindowSize(size2);
        ImGui::PopStyleVar();
    }
    ImGui::End(); // Begin() must always be paired with End(), even when Begin() returns false
}

void DrawColorWindow()
{
    auto colorTex = directX::getDepthTexture();
    if (colorTex == nullptr || colorTex->getTextureColor() == nullptr || utinni::Game::getPlayer() == nullptr)
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
        ImGui::GetWindowDrawList()->AddImage((void*)colorTex->getTextureColor(), pos, posMax);
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
        ImGui_ImplDX9_NewFrame();
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

                auto depthTex = directX::getDepthTexture();
                if (depthTex != nullptr)
                {
                    if (ImGui::Checkbox("Show Depth Window", &showDepthWindow))
                    {
                    }
                    if (ImGui::Checkbox("Show Color Window", &showColorWindow))
                    {
                    }

                    int stage = depthTex->getStage();
                    if (ImGui::SliderInt("Stage", &stage, 0, 11))
                    {
                        depthTex->setStage(stage);
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

        imguiHasHover = ImGui::IsWindowHovered(ImGuiHoveredFlags_AnyWindow);
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
        ImGui_ImplDX9_RenderDrawData(ImGui::GetDrawData());
        rendering = false;
    }
}

bool isRendering()
{
    return rendering;
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
    if (GroundScene::get() == nullptr || object == nullptr || !enabled)
    {
        return;
    }

    Camera* camera = GroundScene::get()->getCurrentCamera();

    // Set up the matrices for the gizmo
    Transform w2c;
    w2c.invert(*camera->getTransform_o2w());
    const Matrix4x4 view = Matrix4x4(w2c);
    const Matrix4x4 objectMatrix = Matrix4x4(*object->getTransform_o2w());

    float* viewMatrix = &Matrix4x4().matrix[0][0];
    float* projMatrix = &Matrix4x4().matrix[0][0];
    float* objMatrix = &Matrix4x4().matrix[0][0];

    Matrix4x4::transpose(&view.matrix[0][0], viewMatrix);
    Matrix4x4::transpose(&camera->projectionMatrix.matrix[0][0], projMatrix);
    Matrix4x4::transpose(&objectMatrix.matrix[0][0], objMatrix);

    // Enable and draw the gizmo
    ImGuizmo::SetRect(0, 0, Graphics::getCurrentRenderTargetWidth(), Graphics::getCurrentRenderTargetHeight());
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
