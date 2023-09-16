#pragma once
#define WIN32_LEAN_AND_MEAN
// Windows Header Files
#include <windows.h>
#include <tlhelp32.h>
#include <string>
#include "stCommon.h"

class OSK
{
	stScreenLayout oskLayout = stScreenLayout();
	stBasicTexture oskSharedTexture = stBasicTexture();
	RECT displayRect = { 0, 0, 0, 0 };
	PROCESSENTRY32 FindProcess(std::wstring toFind);

public:
	void ToggleOSK();
	void CreateOSKTexture(ID3D11Device* device, stBasicTexture* oskTexture);
	void CopyOSKTexture(ID3D11Device* device, ID3D11DeviceContext* devCon, stBasicTexture* oskTexture);
	bool LoadOSK(ID3D11Device* device, stBasicTexture* oskTexture, RECT position);
	stScreenLayout* GetOSKLayout();
	void ShowHide(bool show);
	void UnloadOSK();
};

