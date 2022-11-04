#pragma once
#include <d3d11_4.h>
#include "ffxivTexture.h"

struct stSwapChain
{
	/* 0x000 */ byte uk1[0x38];
	/* 0x038 */ unsigned int Width;
	/* 0x03C */ unsigned int Height;
	/* 0x040 */ byte uk2[0x18];
	/* 0x058 */ stTexture* BackBuffer;
	/* 0x060 */ stTexture* DepthStencil;
	/* 0x068 */ IDXGISwapChain4* DXGISwapChain;
};