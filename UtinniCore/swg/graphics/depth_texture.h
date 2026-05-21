#pragma once

#include <d3d9.h>

namespace directX
{
class DepthTexture
{
private:
	 LPDIRECT3DTEXTURE9 pTextureDepth;
	 IDirect3DTexture9* pTextureColor;
	 bool m_isRESZ;
	 bool m_isINTZ;
	 bool m_isNVAPI;
	 bool m_isSupported;
	 IDirect3DSurface9* pRegisteredDSS;
	 LPDIRECT3DDEVICE9 _pDevice;
	 int stage;

public:

	 DepthTexture();
	 ~DepthTexture();
	 void release();
	 void createTexture(LPDIRECT3DDEVICE9 pDevice, int width, int height);
	 void resolveDepth();
	 void resolveDepth(LPDIRECT3DDEVICE9 pDevice, IDirect3DSurface9* surface);

	 LPDIRECT3DTEXTURE9 getTextureDepth() { return pTextureDepth; }
	 LPDIRECT3DTEXTURE9 getTextureColor() { return pTextureColor; }
	 bool isINTZ() { return m_isINTZ; }
	 bool isSupported() { return m_isSupported; }
	 bool isNvidia() { return m_isNVAPI; }

	 int getStage() { return stage; }
	 void setStage(int value) { stage = value; }

	 // Phase 3 R-A primary API (per 03-CONTEXT D-08/D-09).
	 UTINNI_API static int subscribeDepthResolveCallback(void (*func)());
	 UTINNI_API static bool unsubscribeDepthResolveCallback(int handle);
	 // Legacy add* (D-10): thin wrapper.
	 UTINNI_API static void addDepthResolveCallback(void (*func)());
};

}
