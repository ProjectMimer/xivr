#pragma once
#include <d3d11_4.h>
#include "ffxivSwapChain.h"

struct stDevice
{
	/* 0x000 */ byte uk1[0x8];
	/* 0x008 */ unsigned long long ContextArray;
	/* 0x010 */ unsigned long long RenderThread;
	/* 0x018 */ byte uk2[0x58];
	/* 0x070 */ stSwapChain* SwapChain;
	/* 0x078 */ byte uk3[0x1A8];
	/* 0x220 */ unsigned int D3DFeatureLevel;
	/* 0x224 */ byte uk4[0x4];
	/* 0x228 */ IDXGIFactory4* IDXGIFactory;
	/* 0x230 */ IDXGIOutput4* IDXGIOutput;
	/* 0x238 */ ID3D11Device4* Device;
	/* 0x240 */ ID3D11DeviceContext4* DeviceContext;
	/* 0x248 */ byte uk5[0x8];
	/* 0x250 */ unsigned long long ImmediateContext;
};