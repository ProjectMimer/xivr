#pragma once
#include "stBasicTexture.h"

union uMatrix
{
	float matrix[4][4];
	float _m[16];
};

enum poseType
{
	Projection = 0,
	EyeOffset = 1,
	hmdPosition = 10,
	prevHmdPosition = 20,
	LeftHand = 30,
	RightHand = 40,
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
