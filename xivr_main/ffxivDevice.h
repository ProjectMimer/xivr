#pragma once
#include <d3d11_4.h>
#include "ffxivSwapChain.h"

struct stDevice
{
	/* 0x000 */ byte spcr1[0x8];
	/* 0x008 */ unsigned long long ContextArray;
	/* 0x010 */ unsigned long long RenderThread;
	/* 0x018 */ byte spcr2[0x70 - 0x18];
	/* 0x070 */ stSwapChain* SwapChain;
	/* 0x078 */ byte spcr3[0x2];
	/* 0x07A */ byte RequestResolutionChange;
	/* 0x07B */ byte spcr4[0x88 - 0x7B];
	/* 0x088 */ unsigned int width;
	/* 0x08C */ unsigned int height;
	/* 0x090 */ byte spcr5[0xF0 - 0x90];
	/* 0x0F0 */ unsigned long long ListOfCommands;
	/* 0x0F8 */ unsigned int NumCommands;
	/* 0x0FC */ byte spcr6[0x1C0 - 0xFC];
	/* 0x1C0 */ unsigned int newWidth;
	/* 0x1C4 */ unsigned int newHeight;
	/* 0x1C8 */ byte spcr7[0x220 - 0x1C8];
	/* 0x220 */ unsigned int D3DFeatureLevel;
	/* 0x224 */ unsigned int uk1;
	/* 0x228 */ IDXGIFactory4* IDXGIFactory;
	/* 0x230 */ IDXGIOutput4* IDXGIOutput;
	/* 0x238 */ ID3D11Device4* Device;
	/* 0x240 */ ID3D11DeviceContext4* DeviceContext;
	/* 0x248 */ unsigned long long uk2;
	/* 0x250 */ unsigned long long ImmediateContext;
};