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
	LeftHandPalm = 21,
	RightHand = 30,
	RightHandPalm = 31,
};

struct stScreenLayout
{
	UINT64 pid = 0;
	HWND hwnd = 0;
	int width = 0;
	int height = 0;
	bool haveLayout = false;

	void SetFromSwapchain(IDXGISwapChain4* swapchain)
	{
		DXGI_SWAP_CHAIN_DESC swapDesc;
		swapchain->GetDesc(&swapDesc);
		hwnd = swapDesc.OutputWindow;
		width = swapDesc.BufferDesc.Width;
		height = swapDesc.BufferDesc.Height;
		haveLayout = true;
	}
};

struct Vector4
{
	float x;
	float y;
	float z;
	float w;
};

struct Quat4
{
	float w;
	float x;
	float y;
	float z;
};

struct BoneData
{
	Vector4 Translation;
	Quat4 Rotation;
};

struct fingerHandLayout
{
	BoneData root;
	BoneData wrist;

	BoneData thumb0Metacarpal;
	union
	{
		BoneData thumb1Proximal;
		BoneData thumb2Middle;
	};
	BoneData thumb3Distal;
	BoneData thumb4Tip;
	
	BoneData index0Metacarpal;
	BoneData index1Proximal;
	BoneData index2Middle;
	BoneData index3Distal;
	BoneData index4Tip;

	BoneData middle0Metacarpal;
	BoneData middle1Proximal;
	BoneData middle2Middle;
	BoneData middle3Distal;
	BoneData middle4Tip;

	BoneData ring0Metacarpal;
	BoneData ring1Proximal;
	BoneData ring2Middle;
	BoneData ring3Distal;
	BoneData ring4Tip;

	BoneData pinky0Metacarpal;
	BoneData pinky1Proximal;
	BoneData pinky2Middle;
	BoneData pinky3Distal;
	BoneData pinky4Tip;

	BoneData thumbAux;
	BoneData indexAux;
	BoneData middleAux;
	BoneData ringAux;
	BoneData pinkyAux;
};