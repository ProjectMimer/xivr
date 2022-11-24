#pragma once
#include "ffxivTexture.h"

struct stRenderTargetManager
{
	/* 0x000 */ void* vtbl;
	/* 0x008 */ unsigned long long Notifier;
	/* 0x010 */ byte uk1[0x10];
	/* 0x020 */ stTexture* RenderTextureArray1[69];
	/* 0x248 */ unsigned int ResolutionWidth;
	/* 0x24C */ unsigned int ResolutionHeight;
	/* 0x250 */ unsigned int ShadowMapWidth;
	/* 0x254 */ unsigned int ShadowMapHeight;
	/* 0x258 */ unsigned int NearShadowMapWidth;
	/* 0x25C */ unsigned int NearShadowMapHeight;
	/* 0x260 */ unsigned int FarShadowMapWidth;
	/* 0x264 */ unsigned int FarShadowMapHeight;
	/* 0x268 */ byte uk3[0x18];
	/* 0x280 */ stTexture* RenderTextureArray2[49];
};

