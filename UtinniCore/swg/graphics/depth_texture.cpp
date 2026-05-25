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

#include "utinni.h"
#include "depth_texture.h"

#include <d3d9types.h>
#include <mutex>
#include <vector>
#include "external/nvapi/nvapi.h"

#pragma comment(lib, "nvapi/x86/nvapi.lib")

#define FOURCC_INTZ ((D3DFORMAT)(MAKEFOURCC('I','N','T','Z')))
#define RESZ_CODE 0x7FA05000

// Phase 3 R-A native-side (per 03-CONTEXT D-08/D-09): handle-based registry
// backed by insertion-order std::vector<{handle, fn_ptr}>.
// D-08 noted that depth_texture's addDepthResolveCallback is a class-static
// member of DepthTexture::, not a free function. Per the D-08 planner-discretion
// clause ("handle stored per-instance OR static-class registry, planner's
// discretion") we keep the static-class storage shape — every existing call
// site treats this as a process-wide registry, not per-instance.
// CR-01 (03-REVIEW): per-registry mutex protects Subscribe / Unsubscribe / snapshot.
//
// 2026-05-22 follow-up to ground_scene fix (commit 7201700): switched from
// std::unordered_map to insertion-order vector with stack-allocated fixed-size
// snapshot in dispatch sites. See [[project-rh-snapshot-no-heap-alloc]] memory.
namespace
{
// Local 3-float vertex struct replacing the legacy DXSDK vector type (DXSDK
// June 2010 retired in Phase 6, CON-O-08). Layout matches the old type
// byte-for-byte (three floats, no padding) so the DrawPrimitiveUP stride math
// is unchanged.
struct Vec3
{
    float x;
    float y;
    float z;
};

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

static std::vector<CallbackEntry<void(*)()>> depthResolveCallbacks;
static std::mutex depthResolveCallbacksMutex;
static int s_nextDepthResolveId = 1;

namespace directX
{

// ?getBuffer@Commander@DPVS@@IBE?AW4BufferType@LibraryDefs@2@AAPBEAAH1@Z

DepthTexture::DepthTexture()
    : pTextureDepth(nullptr)
	 , m_isNVAPI(false)
	 , m_isSupported(false)
	 , pRegisteredDSS(nullptr)
	 , stage(5)
{
	 if (NvAPI_Initialize() == NVAPI_OK)
	 {
		  m_isNVAPI = true;
	 }
	 m_isRESZ = !m_isNVAPI;

	 m_isSupported = (m_isNVAPI || m_isRESZ); // determine if RESZ(ATI) or NVAPI supported
}

//--------------------------------------------------------------------------------------
void DepthTexture::createTexture(LPDIRECT3DDEVICE9 pDevice, int width, int height)
{
	 if (m_isSupported)
	 {
		  _pDevice = pDevice;
		  D3DFORMAT format = FOURCC_INTZ; //m_isNVAPI ? FOURCC_INTZ : FOURCC_RAWZ;
		  pDevice->CreateTexture(width, height, 1, D3DUSAGE_DEPTHSTENCIL, format, D3DPOOL_DEFAULT, &pTextureDepth, nullptr);
		  pDevice->CreateTexture(width, height, 1, D3DUSAGE_RENDERTARGET, D3DFMT_X8R8G8B8, D3DPOOL_DEFAULT, &pTextureColor, nullptr);


		  if (m_isNVAPI)
		  {
				//utinni::log::info("nVidia GPU Selected");
				NvAPI_D3D9_RegisterResource(pTextureDepth);
				NvAPI_D3D9_RegisterResource(pTextureColor);
		  }
		  /*else
		  {
				utinni::log::info("ATI or Intel GPU Selected");
		  }*/

		  //pTexture->GetLevelDesc(0, &textureDesc);
	 }
}

void DepthTexture::release()
{
	 if (pTextureDepth)
	 {
		  if (m_isNVAPI)
		  {
				NvAPI_D3D9_UnregisterResource(pTextureDepth);
		  }
		  if (pTextureDepth != nullptr)
		  {
				pTextureDepth->Release();
				pTextureDepth = nullptr;
		  }
	 }

	 if (pRegisteredDSS)
	 {
		  if (m_isNVAPI)
		  {
				NvAPI_D3D9_UnregisterResource(pRegisteredDSS);
		  }
		  if (pRegisteredDSS != nullptr)
		  {
				pRegisteredDSS->Release();
				pRegisteredDSS = nullptr;
		  }
	 }

	 if (pTextureColor)
	 {
		  if (m_isNVAPI)
		  {
				NvAPI_D3D9_UnregisterResource(pTextureColor);
		  }
		  if (pTextureColor != nullptr)
		  {
				pTextureColor->Release();
				pTextureColor = nullptr;
		  }
	 }
}


DepthTexture::~DepthTexture()
{
	 release();
}

void copyTextureData(LPDIRECT3DTEXTURE9 pTexture, D3DSURFACE_DESC textureDesc)
{
	 D3DLOCKED_RECT lockedRect;
	 RECT rect;
	 rect.top = 0;
	 rect.left = 0;
	 rect.right = textureDesc.Width;
	 rect.bottom = textureDesc.Height;

	 pTexture->LockRect(0, &lockedRect, &rect, 0);
	 const size_t size = textureDesc.Width * textureDesc.Height * lockedRect.Pitch;
	 std::vector<byte> textureData(size);
	 memcpy(textureData.data(), lockedRect.pBits, size);
	 pTexture->UnlockRect(0);
}

void resolveDepthWithResz(const LPDIRECT3DDEVICE9 pDevice, LPDIRECT3DTEXTURE9 pTexture)
{
	 pDevice->SetVertexShader(nullptr);
	 pDevice->SetPixelShader(nullptr);
	 pDevice->SetFVF(D3DFVF_XYZ);

	 // Bind depth stencil texture to texture sampler 0
	 pDevice->SetTexture(0, pTexture);

	 // Perform a dummy draw call to ensure texture sampler 0 is set before the // resolve is triggered
	 // Vertex declaration and shaders may need to be adjusted to ensure no debug
	 // error message is produced
	 Vec3 vDummyPoint{ 0.0f, 0.0f, 0.0f };
	 pDevice->SetRenderState(D3DRS_ZENABLE, FALSE);
	 pDevice->SetRenderState(D3DRS_ZWRITEENABLE, FALSE);
	 pDevice->SetRenderState(D3DRS_COLORWRITEENABLE, 0);
	 pDevice->DrawPrimitiveUP(D3DPT_POINTLIST, 1, &vDummyPoint, sizeof(Vec3));
	 pDevice->SetRenderState(D3DRS_ZWRITEENABLE, TRUE);
	 pDevice->SetRenderState(D3DRS_ZENABLE, TRUE);
	 pDevice->SetRenderState(D3DRS_COLORWRITEENABLE, 0x0F);

	 // Trigger the depth buffer resolve; after this call texture sampler 0
	 // will contain the contents of the resolve operation
	 pDevice->SetRenderState(D3DRS_POINTSIZE, RESZ_CODE);

	 // This hack to fix resz hack, has been found by Maksym Bezus!!!
	 // Without this line resz will be resolved only for first frame
	 pDevice->SetRenderState(D3DRS_POINTSIZE, 0);
}

void DepthTexture::resolveDepth(const LPDIRECT3DDEVICE9 pDevice, IDirect3DSurface9* pSurface)
{
	 if (m_isNVAPI)
	 {
		  //Safety catch just incase
		  if (pRegisteredDSS != pSurface)
		  {
				//Registering Fails, something is up with the depth stencil surface
				NvAPI_D3D9_RegisterResource(pSurface);
				if (pRegisteredDSS != nullptr)
				{
					 // Unregister old one if there is any
					 NvAPI_D3D9_UnregisterResource(pRegisteredDSS);
				}
				pRegisteredDSS = pSurface;
		  }
		  DWORD result = NvAPI_D3D9_StretchRectEx(pDevice, pSurface, nullptr, pTextureDepth, nullptr, D3DTEXF_NONE);
	 }
    else if (m_isRESZ)
	 {
		  resolveDepthWithResz(pDevice, pTextureDepth);
	 }

	 //copyTextureData(pTexture, textureDesc);
}

// Phase 3 R-A: handle-based Subscribe/Unsubscribe per D-08/D-09.
int DepthTexture::subscribeDepthResolveCallback(void(*func)())
{
	 std::lock_guard<std::mutex> guard(depthResolveCallbacksMutex);
	 int id = s_nextDepthResolveId++;
	 if (id == 0) { id = s_nextDepthResolveId++; } // WR-04 skip-zero
	 depthResolveCallbacks.push_back({id, func});
	 return id;
}

bool DepthTexture::unsubscribeDepthResolveCallback(int handle)
{
	 if (handle == 0)
	 {
		  return false;
	 }
	 std::lock_guard<std::mutex> guard(depthResolveCallbacksMutex);
	 for (auto it = depthResolveCallbacks.begin(); it != depthResolveCallbacks.end(); ++it)
	 {
		  if (it->handle == handle)
		  {
			   depthResolveCallbacks.erase(it);
			   return true;
		  }
	 }
	 return false;
}

void DepthTexture::addDepthResolveCallback(void(* func)())
{
	 subscribeDepthResolveCallback(func);
}

void DepthTexture::resolveDepth()
{
	 if (m_isNVAPI)
	 {
		  IDirect3DSurface9* pDSS = nullptr;
		  _pDevice->GetDepthStencilSurface(&pDSS);

		  if (pRegisteredDSS != pDSS)
		  {
				//Registering Fails, something is up with the depth stencil surface
				NvAPI_D3D9_RegisterResource(pDSS);
				if (pRegisteredDSS != nullptr)
				{
					 // Unregister old one if there is any
					 NvAPI_D3D9_UnregisterResource(pRegisteredDSS);
				}
				pRegisteredDSS = pDSS;
		  }
		  DWORD result = NvAPI_D3D9_StretchRectEx(_pDevice, pDSS, nullptr, pTextureDepth, nullptr, D3DTEXF_NONE);
		  pDSS->Release();

		  LPDIRECT3DSURFACE9 pRenderTarget;
		  LPDIRECT3DSURFACE9 pSurface;
	     pTextureColor->GetSurfaceLevel(0, &pSurface);
	     _pDevice->GetRenderTarget(0, &pRenderTarget);
	     _pDevice->StretchRect(pRenderTarget, nullptr, pSurface, nullptr, D3DTEXF_NONE);
		  pRenderTarget->Release();
		  pSurface->Release();
	 }
	 else if (m_isRESZ)
	 {
		  resolveDepthWithResz(_pDevice, pTextureDepth);
	 }

	 _pDevice->SetTexture(14, pTextureColor);
	 _pDevice->SetTexture(15, pTextureDepth);

	 // R-H snapshot dispatch per D-12. CR-01: lock-around-snapshot. Stack-snapshot
	 // via dispatchSnapshot keeps the path heap-free.
	 dispatchSnapshot(depthResolveCallbacks, depthResolveCallbacksMutex,
		  [](void(*func)()) { func(); });
}
}