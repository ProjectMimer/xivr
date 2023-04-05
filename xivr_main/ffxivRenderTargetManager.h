#pragma once
#include "ffxivTexture.h"

struct stRenderTargetManager
{
	/* 0x000 */ void* vtbl;
	/* 0x008 */ unsigned long long Notifier;
	/* 0x010 */ byte spcr1[0x10];
	/* 0x020 */ stTexture* RenderTextureArray1[73];
	/* 0x268 */ unsigned int ResolutionWidth;
	/* 0x26C */ unsigned int ResolutionHeight;
	/* 0x270 */ unsigned int ShadowMapWidth;
	/* 0x274 */ unsigned int ShadowMapHeight;
	/* 0x278 */ unsigned int NearShadowMapWidth;
	/* 0x27C */ unsigned int NearShadowMapHeight;
	/* 0x280 */ unsigned int FarShadowMapWidth;
	/* 0x284 */ unsigned int FarShadowMapHeight;
	/* 0x288 */ byte spcr3[0x18];
	/* 0x2A0 */ stTexture* RenderTextureArray2[49];
};
