#pragma once
#include "stBasicTexture.h"

union uMatrix
{
	float matrix[4][4];
	float _m[16];
};

enum poseType
{
	None = 0,
	Projection = 1,
	EyeOffset = 5,
	hmdPosition = 10,
	LeftHand = 20,
	RightHand = 30,
};

struct stScreenLayout
{
	HWND hwnd;
	int width;
	int height;

	void SetFromSwapchain(IDXGISwapChain4* swapchain)
	{
		DXGI_SWAP_CHAIN_DESC swapDesc;
		swapchain->GetDesc(&swapDesc);
		hwnd = swapDesc.OutputWindow;
		width = swapDesc.BufferDesc.Width;
		height = swapDesc.BufferDesc.Height;
	}
};
