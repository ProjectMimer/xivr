#pragma once
#include <d3d11_4.h>

union unTexturePtr
{
	ID3D11RenderTargetView* RenderTargetView;
	ID3D11DepthStencilView* DepthStencilView;
};

struct stTexture
{
	/* 0x000 */ unsigned long long uk1;
	/* 0x008 */ unsigned int RefCount;
	/* 0x00C */ unsigned int uk3;
	/* 0x010 */ unsigned long long uk4;
	/* 0x018 */ unsigned long long uk5;
	/* 0x020 */ unsigned long long Notifier;
	/* 0x028 */ unsigned long long uk7;
	/* 0x030 */ unsigned long long uk8;
	/* 0x038 */ unsigned int Width;
	/* 0x03C */ unsigned int Height;
	/* 0x040 */ unsigned int Width1;
	/* 0x044 */ unsigned int Height1;
	/* 0x048 */ unsigned int Width2;
	/* 0x04C */ unsigned int Height2;
	/* 0x050 */ unsigned int Depth;
	/* 0x054 */ unsigned int MipLevel;
	/* 0x058 */ unsigned int TextureFormat;
	/* 0x05C */ unsigned int Flags;
	/* 0x060 */ unsigned int uk9;
	/* 0x064 */ unsigned int ukA;
	/* 0x068 */ ID3D11Texture2D* Texture;
	/* 0x070 */ ID3D11ShaderResourceView* ShaderResourceView;
	/* 0x078 */ unsigned long long ukB;
	/* 0x080 */ unTexturePtr* itemPtr;
	/* 0x088 */ byte spcr5[0xF8 - 0x80];
};