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

#include "directx9.h"
#include <d3d9.h>
#include <d3d9types.h>
#include <imgui/imgui_impl_dx9.h>
#include "utinni.h"
#include "swg/ui/imgui_impl.h"
#include "swg/ui/cui_manager.h"
#include "depth_texture.h"
#include "graphics.h"

// C-09: Win32 manual-reset event signaller for UI/game-thread synchronization.
// The managed FormMain.WndProc waits on this event instead of spinning on IsPresentBlocked().
// ownsHandle: false on the managed SafeWaitHandle wrapper — native owns the lifetime.
// CON-N-01: no new Detour::Create calls; this is additive signal plumbing only.
// CON-N-04: memory::copy (VirtualProtect bracket) NOT touched.
static HANDLE hPresentBlockedEvent = nullptr;

// getPresentBlockedEvent — production export consumed by FormMain.cs via P/Invoke.
// CR-04: hPresentBlockedEvent is eagerly created by directX::initPresentBlockedEvent()
// in utinni_init before any detour fires. This function is now a pure reader.
// TOCTOU race window has been eliminated — no lazy CreateEvent here.
// extern "C" + __cdecl ensures the symbol is unmangled so DllImport resolves it directly.
extern "C" __declspec(dllexport) HANDLE __cdecl getPresentBlockedEvent()
{
    return hPresentBlockedEvent;
}

// CR-04: Eagerly creates hPresentBlockedEvent so hkPresent (render thread) always
// finds a valid HANDLE. Called from utinni_init before createDetours() — the launcher
// remote thread runs this synchronously and hkPresent cannot fire until SWG's main loop
// is running (main loop is parked at WaitForSingleObject until Launcher/main.cpp:232 returns).
// No concurrency is possible at init time; no TOCTOU race.
// CON-N-01: this is NOT a Detour::Create call; it is additive Win32 event plumbing.
// CON-H-01: called from utinni_init (launcher remote thread), NOT DllMain.
void directX::initPresentBlockedEvent()
{
    // TRUE = manual-reset; FALSE = initially non-signalled.
    hPresentBlockedEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);
}

namespace directX
{
LPDIRECT3DDEVICE9 pDirectXDevice = nullptr;
swgptr dllBaseAddress = 0;

DepthTexture* depthTexture = nullptr;

static bool blockPresentCall = false;

// WR-03: Eagerly constructs DepthTexture so hkPresent (render thread) always finds a
// valid pointer. DepthTexture() ctor only calls NvAPI_Initialize() — does NOT require a
// live D3D9 device (verified in Phase 02.1 RESEARCH §WR-03). createTexture() still fires
// on the first render frame inside hkPresent when pDevice is available.
// Called from utinni_init before createDetours() — single-threaded, no race possible.
// CON-N-01: this is NOT a Detour::Create call; it is a plain C++ new.
// CON-H-01: called from utinni_init (launcher remote thread), NOT DllMain.
void initDepthTexture()
{
    depthTexture = new DepthTexture();
}

static bool isPresenting = false;
bool enableWireframe = false;

using pBeginScene = HRESULT(__stdcall*)(LPDIRECT3DDEVICE9 pDevice);
using pEndScene = HRESULT(__stdcall*)(LPDIRECT3DDEVICE9 pDevice);
using pPresent = HRESULT(__stdcall*)(LPDIRECT3DDEVICE9 pDevice, const RECT* pSourceRect, const RECT* pDestRect, HWND hDestWindowOverride, const RGNDATA* pDirtyRegion);
using pReset = HRESULT(__stdcall*)(LPDIRECT3DDEVICE9 pDevice, D3DPRESENT_PARAMETERS* pPresentationParameters);
using pDrawIndexedPrimitive = HRESULT(__stdcall*)(LPDIRECT3DDEVICE9 pDevice, D3DPRIMITIVETYPE type, int baseVertexIndex, unsigned int minIndex, unsigned int numVertices, unsigned int startIndex, unsigned int primitiveCount);
using pSetRenderTarget = HRESULT(__stdcall*)(LPDIRECT3DDEVICE9 pDevice, DWORD index, IDirect3DSurface9* surface);
using pSetDepthStencil = HRESULT(__stdcall*)(LPDIRECT3DDEVICE9 pDevice, IDirect3DSurface9* surface);
using pSetRenderState = HRESULT(__stdcall*) (LPDIRECT3DDEVICE9 pDevice, D3DRENDERSTATETYPE State, DWORD Value);

using pCompileShader = HRESULT(__stdcall*)(LPCSTR pSrcData, UINT srcDataLen, LPVOID* pDefines, LPVOID pInclude, LPCSTR pFunctionName, LPCSTR pProfile, DWORD Flags, LPVOID* ppShader, LPVOID* ppErrorMsgs, LPVOID* ppConstantTable);

pBeginScene beginScene;
pEndScene endScene;
pPresent present;
pReset reset;
pDrawIndexedPrimitive drawIndexedPrimitive;
pSetRenderTarget setRenderTarget;
pSetDepthStencil setDepthStencil;
pSetRenderState setRenderState;

pCompileShader compileShader = (pCompileShader)0x62A4F9DB; // from s207_r.dll

enum D3DInformation
{
	 d3di_Queryinterface_Index = 0,
	 d3di_AddRef_Index = 1,
	 d3di_Release_Index = 2,
	 d3di_TestCooperativeLevel_Index = 3,
	 d3di_GetAvailableTextureMem_Index = 4,
	 d3di_EvictManagedResources_Index = 5,
	 d3di_GetDirect3D_Index = 6,
	 d3di_GetDeviceCaps_Index = 7,
	 d3di_GetDisplayMode_Index = 8,
	 d3di_GetCreationParameters_Index = 9,
	 d3di_SetCursorProperties_Index = 10,
	 d3di_SetCursorPosition_Index = 11,
	 d3di_ShowCursor_Index = 12,
	 d3di_CreateAdditionalSwapChain_Index = 13,
	 d3di_GetSwapChain_Index = 14,
	 d3di_GetNumberOfSwapChains_Index = 15,
	 d3di_Reset_Index = 16,
	 d3di_Present_Index = 17,
	 d3di_GetBackBuffer_Index = 18,
	 d3di_GetRasterStatus_Index = 19,
	 d3di_SetDialogBoxMode_Index = 20,
	 d3di_SetGammaRamp_Index = 21,
	 d3di_GetGammaRamp_Index = 22,
	 d3di_CreateTexture_Index = 23,
	 d3di_CreateVolumeTexture_Index = 24,
	 d3di_CreateCubeTexture_Index = 25,
	 d3di_CreateVertexBuffer_Index = 26,
	 d3di_CreateIndexBuffer_Index = 27,
	 d3di_CreateRenderTarget_Index = 28,
	 d3di_CreateDepthStencilSurface_Index = 29,
	 d3di_UpdateSurface_Index = 30,
	 d3di_UpdateTexture_Index = 31,
	 d3di_GetRenderTargetData_Index = 32,
	 d3di_GetFrontBufferData_Index = 33,
	 d3di_StretchRect_Index = 34,
	 d3di_ColorFill_Index = 35,
	 d3di_CreateOffscreenPlainSurface_Index = 36,
	 d3di_SetRenderTarget_Index = 37,
	 d3di_GetRenderTarget_Index = 38,
	 d3di_SetDepthStencilSurface_Index = 39,
	 d3di_GetDepthStencilSurface_Index = 40,
	 d3di_BeginScene_Index = 41,
	 d3di_EndScene_Index = 42,
	 d3di_Clear_Index = 43,
	 d3di_SetTransform_Index = 44,
	 d3di_GetTransform_Index = 45,
	 d3di_MultiplyTransform_Index = 46,
	 d3di_SetViewport_Index = 47,
	 d3di_GetViewport_Index = 48,
	 d3di_SetMaterial_Index = 49,
	 d3di_GetMaterial_Index = 50,
	 d3di_SetLight_Index = 51,
	 d3di_GetLight_Index = 52,
	 d3di_LightEnable_Index = 53,
	 d3di_GetLightEnable_Index = 54,
	 d3di_SetClipPlane_Index = 55,
	 d3di_GetClipPlane_Index = 56,
	 d3di_SetRenderState_Index = 57,
	 d3di_GetRenderState_Index = 58,
	 d3di_CreateStateBlock_Index = 59,
	 d3di_BeginStateBlock_Index = 60,
	 d3di_EndStateBlock_Index = 61,
	 d3di_SetClipStatus_Index = 62,
	 d3di_GetClipStatus_Index = 63,
	 d3di_GetTexture_Index = 64,
	 d3di_SetTexture_Index = 65,
	 d3di_GetTextureStageState_Index = 66,
	 d3di_SetTextureStageState_Index = 67,
	 d3di_GetSamplerState_Index = 68,
	 d3di_SetSamplerState_Index = 69,
	 d3di_ValidateDevice_Index = 70,
	 d3di_SetPaletteEntries_Index = 71,
	 d3di_GetPaletteEntries_Index = 72,
	 d3di_SetCurrentTexturePalette_Index = 73,
	 d3di_GetCurrentTexturePalette_Index = 74,
	 d3di_SetScissorRect_Index = 75,
	 d3di_GetScissorRect_Index = 76,
	 d3di_SetSoftwareVertexProcessing_Index = 77,
	 d3di_GetSoftwareVertexProcessing_Index = 78,
	 d3di_SetNPatchMode_Index = 79,
	 d3di_GetNPatchMode_Index = 80,
	 d3di_DrawPrimitive_Index = 81,
	 d3di_DrawIndexedPrimitive_Index = 82,
	 d3di_DrawPrimitiveUP_Index = 83,
	 d3di_DrawIndexedPrimitiveUP_Index = 84,
	 d3di_ProcessVertices_Index = 85,
	 d3di_CreateVertexDeclaration_Index = 86,
	 d3di_SetVertexDeclaration_Index = 87,
	 d3di_GetVertexDeclaration_Index = 88,
	 d3di_SetFVF_Index = 89,
	 d3di_GetFVF_Index = 90,
	 d3di_CreateVertexShader_Index = 91,
	 d3di_SetVertexShader_Index = 92,
	 d3di_GetVertexShader_Index = 93,
	 d3di_SetVertexShaderConstantF_Index = 94,
	 d3di_GetVertexShaderConstantF_Index = 95,
	 d3di_SetVertexShaderConstantI_Index = 96,
	 d3di_GetVertexShaderConstantI_Index = 97,
	 d3di_SetVertexShaderConstantB_Index = 98,
	 d3di_GetVertexShaderConstantB_Index = 99,
	 d3di_SetStreamSource_Index = 100,
	 d3di_GetStreamSource_Index = 101,
	 d3di_SetStreamSourceFreq_Index = 102,
	 d3di_GetStreamSourceFreq_Index = 103,
	 d3di_SetIndices_Index = 104,
	 d3di_GetIndices_Index = 105,
	 d3di_CreatePixelShader_Index = 106,
	 d3di_SetPixelShader_Index = 107,
	 d3di_GetPixelShader_Index = 108,
	 d3di_SetPixelShaderConstantF_Index = 109,
	 d3di_GetPixelShaderConstantF_Index = 110,
	 d3di_SetPixelShaderConstantI_Index = 111,
	 d3di_GetPixelShaderConstantI_Index = 112,
	 d3di_SetPixelShaderConstantB_Index = 113,
	 d3di_GetPixelShaderConstantB_Index = 114,
	 d3di_DrawRectPatch_Index = 115,
	 d3di_DrawTriPatch_Index = 116,
	 d3di_DeletePatch_Index = 117,
	 d3di_CreateQuery_Index = 118,
	 d3di_NumberOfFunctions = 118
};

HRESULT __stdcall hkBeginScene(LPDIRECT3DDEVICE9 pDevice)
{
    // DIAG 2026-05-19: one-shot info log on first BeginScene fire.
    // Confirms SWG's render thread reached the scene-begin stage AND our detour
    // is wired correctly. Pair with hkPresent's one-shot below to triangulate
    // where rendering is stalling. Remove after the play-window-stays-black
    // investigation closes.
    static bool s_firstBeginScene = true;
    if (s_firstBeginScene)
    {
        s_firstBeginScene = false;
        utinni::log::info("directX::hkBeginScene: first fire (D3D9 detour confirmed)");
    }

	 if (pDirectXDevice == nullptr)
	 {
		  pDirectXDevice = pDevice;
	 }

    HRESULT result = beginScene(pDevice);

    return result;
}

HRESULT __stdcall hkEndScene(LPDIRECT3DDEVICE9 pDevice)
{
    HRESULT result = endScene(pDevice);
    return result;
}

HRESULT __stdcall hkPresent(LPDIRECT3DDEVICE9 pDevice, const RECT* pSourceRect, const RECT* pDestRect, HWND hDestWindowOverride, const RGNDATA* pDirtyRegion)
{
    // DIAG 2026-05-19: one-shot info logs on first Present fire.
    // Captures the device's hwnd state and blockPresentCall flag so we can tell
    // whether SWG is rendering at all and whether C-09's block flag is stuck.
    static bool s_firstPresent = true;
    if (s_firstPresent)
    {
        s_firstPresent = false;
        char msg[256];
        snprintf(msg, sizeof(msg),
                 "directX::hkPresent: first fire (block=%d, destHwndOverride=0x%p)",
                 blockPresentCall ? 1 : 0, (void*)hDestWindowOverride);
        utinni::log::info(msg);
    }

	 HRESULT result = 0;

	 imgui_impl::render();

	 // Workaround for WinForms crashes on maximize and minimize/restore, something breaks inside of Present when either occur.
    // ToDo: Find better solution in the future
	 if (!blockPresentCall)
	 {
		  isPresenting = true;
		  result = present(pDevice, pSourceRect, pDestRect, hDestWindowOverride, pDirtyRegion);
	 }
	 else
	 {
		  isPresenting = false;
		  // C-09: Signal the UI thread waiting in WaitForPresentBlock. The managed
		  // EventWaitHandle wraps this HANDLE via SafeWaitHandle(ownsHandle: false).
		  if (hPresentBlockedEvent)
		  {
			  SetEvent(hPresentBlockedEvent);
		  }
	 }
	
	 if (depthTexture == nullptr)
	 {
		  // WR-03: This branch is unreachable in production after utinni_init calls initDepthTexture().
		  // If reached, the render thread has raced with cleanup() — a threading contract violation.
		  // Creating here as a defensive fallback to avoid a null-deref crash; the log::critical
		  // call ensures any regression is immediately visible in the utinni log.
		  utinni::log::critical("directX::hkPresent: depthTexture null on render thread — initDepthTexture() was not called from utinni_init (WR-03 regression).");
		  depthTexture = new DepthTexture();
	 }

	 if (utinni::Graphics::getCurrentRenderTargetWidth() > 0 && depthTexture->getTextureDepth() == nullptr)
	 {
		  depthTexture->createTexture(pDevice, utinni::Graphics::getCurrentRenderTargetWidth(), utinni::Graphics::getCurrentRenderTargetHeight());
		  //utinni::log::info("Creating Texture");
	 }

	 imgui_impl::setup(pDevice);
    return result;
}

HRESULT __stdcall hkReset(LPDIRECT3DDEVICE9 pDevice, D3DPRESENT_PARAMETERS* pPresentationParameters)
{
	 if (depthTexture != nullptr && depthTexture->getTextureDepth() != nullptr)
	 {
		  depthTexture->release();
		  //utinni::log::info("Releasing Texture");
	 }

	 ImGui_ImplDX9_InvalidateDeviceObjects();
    HRESULT result = reset(pDevice, pPresentationParameters);
	 ImGui_ImplDX9_CreateDeviceObjects();
	
    return result;
}

HRESULT __stdcall hkDrawIndexedPrimitive(LPDIRECT3DDEVICE9 pDevice, D3DPRIMITIVETYPE type, int baseVertexIndex, unsigned int minVertexIndex, unsigned int numVertices, unsigned int startIndex, unsigned int primitiveCount)
{
    if (pDevice != nullptr && ((enableWireframe && utinni::CuiManager::isRenderingUi()) || (enableWireframe && imgui_impl::isRendering()) || !enableWireframe))
    {
        pDevice->SetRenderState(D3DRS_FILLMODE, D3DFILL_SOLID);
    }
    else if (pDevice != nullptr && enableWireframe && !utinni::CuiManager::isRenderingUi() && !imgui_impl::isRendering())
    {
        pDevice->SetRenderState(D3DRS_FILLMODE, D3DFILL_WIREFRAME);
    }

    HRESULT result = drawIndexedPrimitive(pDevice, type, baseVertexIndex, minVertexIndex, numVertices, startIndex, primitiveCount);
    return result;
}

HRESULT _stdcall hkSetRenderTarget(LPDIRECT3DDEVICE9 pDevice, DWORD index, IDirect3DSurface9* surface)
{
    pDevice->SetRenderState(D3DRS_FILLMODE, D3DFILL_SOLID); // Sets the FillMode to Solid before post processing for Wireframe to work
    HRESULT result = setRenderTarget(pDevice, index, surface);
    return result;
}

HRESULT __stdcall hkSetDepthStencil(LPDIRECT3DDEVICE9 pDevice, IDirect3DSurface9* surface)
{
	 HRESULT result = setDepthStencil(pDevice, surface);
    return result;
}

HRESULT __stdcall hkSetRenderState(LPDIRECT3DDEVICE9 pDevice, D3DRENDERSTATETYPE State, DWORD Value)
{
    return setRenderState(pDevice, State, Value);
}

HRESULT __stdcall hkD3DXCompileShader(LPCSTR pSrcData, UINT srcDataLen, LPVOID* pDefines, LPVOID pInclude, LPCSTR pFunctionName, LPCSTR pProfile, DWORD Flags, LPVOID* ppShader, LPVOID* ppErrorMsgs, LPVOID* ppConstantTable)
{
	 // pixel shaders are precompiled, so it's safe to hard override this as vertex (vs) only
	 return compileShader(pSrcData, srcDataLen, pDefines, pInclude, pFunctionName, "vs_3_0", Flags, ppShader, ppErrorMsgs, ppConstantTable);
}

// IDirect3DDevice9 has 119 vtable entries (3 IUnknown + 116 D3D9-specific).
// The enum d3di_NumberOfFunctions above is set to 118 (matches the last index,
// d3di_CreateQuery_Index) — that is the historical labeling and is intentionally
// not touched here. Use this literal for the actual array length.
static const size_t kD3D9VtblEntries = 119;

// 2026-05-19 — Replaced the d3d9.dll code-pattern scan that broke on modern
// Windows (probe of Win11 24H2 d3d9.dll 6.2.26100.8328 showed the IDirect3DDevice9
// vtable is allocated per-instance on the heap, NOT as a static array in
// d3d9.dll's read-only data — modern d3d9 ships without an .rdata section at all).
// The new approach creates a throwaway IDirect3DDevice9 via the public D3D9 API,
// snapshots its vtable, and releases. The method addresses inside the vtable point
// into d3d9.dll's .text section (verified 119/119 entries) and remain valid after
// the dummy device is released, because we patch the function bodies there
// rather than mutating any vtable. This works identically against the SWG Source
// build and the stock SWGEmu client because both load the OS-provided d3d9.dll.
swgptr* getVtbl()
{
    static swgptr s_vtbl[kD3D9VtblEntries];
    static bool s_initialized = false;
    if (s_initialized) return s_vtbl;

    // Dynamic load of Direct3DCreate9 — avoids adding d3d9.lib to the link line.
    // d3d9.dll is loaded by SWG before utinni_init runs (the launcher injects after
    // the game has bootstrapped its render subsystem); in the test process the
    // xUnit harness LoadLibraryAs it explicitly.
    HMODULE hD3d9 = GetModuleHandleA("d3d9.dll");
    if (hD3d9 == nullptr)
    {
        utinni::log::critical("DirectX9 hook installation failed: d3d9.dll not loaded");
        return nullptr;
    }

    typedef IDirect3D9* (WINAPI *PFN_Direct3DCreate9)(UINT);
    auto pfnDirect3DCreate9 =
        (PFN_Direct3DCreate9)GetProcAddress(hD3d9, "Direct3DCreate9");
    if (pfnDirect3DCreate9 == nullptr)
    {
        utinni::log::critical("DirectX9 hook installation failed: Direct3DCreate9 not exported by d3d9.dll");
        return nullptr;
    }

    IDirect3D9* pD3D = pfnDirect3DCreate9(D3D_SDK_VERSION);
    if (pD3D == nullptr)
    {
        utinni::log::critical("DirectX9 hook installation failed: Direct3DCreate9 returned null");
        return nullptr;
    }

    // Hidden 1x1 window — required as the hDeviceWindow. Never shown, never pumped.
    HWND hwnd = CreateWindowExA(0, "STATIC", nullptr, WS_POPUP, 0, 0, 1, 1,
                                nullptr, nullptr, GetModuleHandleA(nullptr), nullptr);
    if (hwnd == nullptr)
    {
        char msg[160];
        snprintf(msg, sizeof(msg),
                 "DirectX9 hook installation failed: dummy window creation failed (GetLastError=0x%08lX)",
                 GetLastError());
        pD3D->Release();
        utinni::log::critical(msg);
        return nullptr;
    }

    D3DPRESENT_PARAMETERS pp = {};
    pp.BackBufferWidth = 1;
    pp.BackBufferHeight = 1;
    pp.BackBufferFormat = D3DFMT_X8R8G8B8;
    pp.SwapEffect = D3DSWAPEFFECT_DISCARD;
    pp.Windowed = TRUE;
    pp.hDeviceWindow = hwnd;
    pp.PresentationInterval = D3DPRESENT_INTERVAL_IMMEDIATE;

    // HAL is mandatory: SWG uses HAL, so HAL's vtable is what we need to harvest.
    // NULLREF/REF can return different IDirect3DDevice9 implementations whose
    // function addresses don't intercept HAL Present calls — falling back to them
    // would be a silent miss in production.
    IDirect3DDevice9* pDevice = nullptr;
    HRESULT hr = pD3D->CreateDevice(
        D3DADAPTER_DEFAULT,
        D3DDEVTYPE_HAL,
        hwnd,
        D3DCREATE_SOFTWARE_VERTEXPROCESSING | D3DCREATE_DISABLE_DRIVER_MANAGEMENT,
        &pp,
        &pDevice);

    if (FAILED(hr) || pDevice == nullptr)
    {
        char msg[160];
        snprintf(msg, sizeof(msg),
                 "DirectX9 hook installation failed: CreateDevice(HAL) returned 0x%08lX",
                 (unsigned long)hr);
        DestroyWindow(hwnd);
        pD3D->Release();
        utinni::log::critical(msg);
        return nullptr;
    }

    swgptr* liveVtbl = *(swgptr**)pDevice;
    memcpy(s_vtbl, liveVtbl, sizeof(swgptr) * kD3D9VtblEntries);

    pDevice->Release();
    DestroyWindow(hwnd);
    pD3D->Release();

    s_initialized = true;
    return s_vtbl;
}

void detour()
{
    auto vtbl = getVtbl();
    if (vtbl == nullptr)
    {
        return;
    }

	 swgptr BeginSceneAddress = Detour::CheckPointer(vtbl[d3di_BeginScene_Index]);
    beginScene = (pBeginScene)Detour::Create((LPVOID)BeginSceneAddress, hkBeginScene, DETOUR_TYPE_PUSH_RET);

	 swgptr EndSceneAddress = Detour::CheckPointer(vtbl[d3di_EndScene_Index]);
    endScene = (pBeginScene)Detour::Create((LPVOID)EndSceneAddress, hkEndScene, DETOUR_TYPE_PUSH_RET);

	 swgptr PresentAddress = Detour::CheckPointer(vtbl[d3di_Present_Index]);
    present = (pPresent)Detour::Create((LPVOID)PresentAddress, hkPresent, DETOUR_TYPE_PUSH_RET);

	 swgptr ResetAddress = Detour::CheckPointer(vtbl[d3di_Reset_Index]);
    reset = (pReset)Detour::Create((LPVOID)ResetAddress, hkReset, DETOUR_TYPE_PUSH_RET);

	 swgptr DrawIndexedPrimitiveAddress = Detour::CheckPointer(vtbl[d3di_DrawIndexedPrimitive_Index]);
    drawIndexedPrimitive = (pDrawIndexedPrimitive)Detour::Create((LPVOID)DrawIndexedPrimitiveAddress, hkDrawIndexedPrimitive, DETOUR_TYPE_PUSH_RET);

	 swgptr SetRenderTargetAddress = Detour::CheckPointer(vtbl[d3di_SetRenderTarget_Index]);
    setRenderTarget = (pSetRenderTarget)Detour::Create((LPVOID)SetRenderTargetAddress, hkSetRenderTarget, DETOUR_TYPE_PUSH_RET);

	 swgptr SetDepthStencilAddress = Detour::CheckPointer(vtbl[d3di_SetDepthStencilSurface_Index]);
    setDepthStencil = (pSetDepthStencil)Detour::Create((LPVOID)SetDepthStencilAddress, hkSetDepthStencil, DETOUR_TYPE_PUSH_RET);

	 // ToDo Potentially make this an option, in case it creates issues
	 compileShader = (pCompileShader)Detour::Create((LPVOID)compileShader, hkD3DXCompileShader, DETOUR_TYPE_PUSH_RET);

}

void cleanup()
{
    // WR-03: Threading contract — cleanup() MUST only run after the SWG render thread
    // is fully quiesced (not mid-frame). DLL_PROCESS_DETACH runs on a separate thread;
    // if hkPresent is mid-frame when cleanup() is called, deleting depthTexture here
    // is a use-after-free. Confirmed live on 2026-05-18: injected-session SWG exit fires
    // a "Direct3D could not be correctly initialized" dialog caused by exactly this UAF.
    //
    // Conventional Win32 DllMain teardown pattern: on process exit, the OS reclaims all
    // heap memory. Skipping delete here avoids the cleanup-side UAF at the cost of a
    // bounded "leak" (bounded by process lifetime — not a real leak). This is the correct
    // tradeoff for DLL_PROCESS_DETACH teardown (Raymond Chen / Win32 documentation).
    // The caller (detatch() in utinni.cpp) must ensure the render thread is quiesced
    // before calling cleanup() if this function is ever invoked outside of process exit.
    //
    // WR-03: delete depthTexture intentionally OMITTED — UAF on exit is worse than leak.
    // depthTexture = nullptr omitted too (moot on exit; no caller reads it after cleanup).
}

DepthTexture* getDepthTexture()
{
	 return depthTexture;
}

void toggleWireframe()
{
    enableWireframe = !enableWireframe;
}

void blockPresent(bool value)
{
    blockPresentCall = value;
    if (!value && hPresentBlockedEvent)
    {
        // C-09: Re-arm the event for the next minimize cycle when Present is re-enabled.
        ResetEvent(hPresentBlockedEvent);
    }
}

bool isPresentBlocked()
{
	 return !isPresenting;
}

IDirect3DDevice9* getDevice()
{
	 return pDirectXDevice;
}
}


