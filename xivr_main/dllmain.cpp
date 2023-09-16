#include "framework.h"
#include "OSK.h"
#include <algorithm>
#include <iostream>
#include <sstream>
#include <list>

using namespace DirectX;

std::stringstream outputLog;
stDevice* device = nullptr;
stRenderTargetManager* rtManager = nullptr;
stCameraManager* csCameraManager = nullptr;
D3D11_TEXTURE2D_DESC BackBufferDesc;
D3D11_TEXTURE2D_DESC DepthBufferDesc;

stScreenLayout screenLayout = stScreenLayout();
stScreenLayout* oskLayout = nullptr;

stTexture RenderTexture[6] =	{ stTexture(), stTexture(), stTexture(), stTexture(), stTexture(), stTexture() };
stTexture DepthTexture[6] =		{ stTexture(), stTexture(), stTexture(), stTexture(), stTexture(), stTexture() };
stTexture uiRenderTexture[2] =  { stTexture(), stTexture() };
stTexture* gameRenderTexture = nullptr;
stTexture* gameDepthTexture = nullptr;
stTexture gameRenderRaw = stTexture();
stTexture gameDepthRaw = stTexture();

stBasicTexture BackBufferCopy[6] =	{ stBasicTexture(), stBasicTexture(), stBasicTexture(), stBasicTexture(), stBasicTexture(), stBasicTexture() };
stBasicTexture DepthBufferCopy[6] =	{ stBasicTexture(), stBasicTexture(), stBasicTexture(), stBasicTexture(), stBasicTexture(), stBasicTexture() };
stBasicTexture RenderTarget[6] =	{ stBasicTexture(), stBasicTexture(), stBasicTexture(), stBasicTexture(), stBasicTexture(), stBasicTexture() };
stBasicTexture uiRenderTarget[2] =  { stBasicTexture(), stBasicTexture() };

stBasicTexture BackBuffer = stBasicTexture();
stBasicTexture DepthBuffer = stBasicTexture();
stBasicTexture dalamudBuffer = stBasicTexture();

OSK osk = OSK();
stBasicTexture oskTexture = stBasicTexture();

stBasicTexture handWatchList[] = {
	stBasicTexture(), stBasicTexture(),
	stBasicTexture(), stBasicTexture(),
	stBasicTexture(), stBasicTexture(),
	stBasicTexture(), stBasicTexture(),
	stBasicTexture(), stBasicTexture(),
	stBasicTexture(), stBasicTexture(),
	stBasicTexture(), stBasicTexture(),
	stBasicTexture(), stBasicTexture(),
	stBasicTexture(), stBasicTexture(),
};
int handWatchCount = (sizeof(handWatchList) / sizeof(stBasicTexture)) / 2;

stMatrixSet matrixSet;
stMonitorLayout monitors;
inputController steamInput = {};
std::vector<std::vector<float>> LineRender = std::vector<std::vector<float>>();

int worldID = 70;
int depthID = 8;

HWND oskHWND;
HANDLE oskSurface = 0;
D3D11_VIEWPORT viewport = D3D11_VIEWPORT();
D3D11_VIEWPORT viewports[] = { D3D11_VIEWPORT(), D3D11_VIEWPORT(), D3D11_VIEWPORT() };
int threadedEyeIndexCount = 0;
int backbufferSwap = 0;
bool enabled = false;
int threadedEye = 0;
bool logging = false;
int swapEyes[] = { 1, 0 };
bool useBackBuffer = false;
bool isFloating = false;
bool showOSK = false;
bool oldOSK = true;
bool showUI = true;
int oskRenderCount = -1;


stConfiguration cfg = stConfiguration();
simpleVR* svr = new simpleVR(&cfg);
BasicRenderer* rend = new BasicRenderer(&cfg);

void InitInstance(HANDLE);
void ExitInstance();
//void forceFlush(); 
bool CreateBackbufferClone();
void DestroyBackbufferClone();

typedef void(__stdcall* UpdateControllerInput)(buttonLayout buttonId, vr::InputAnalogActionData_t analog, vr::InputDigitalActionData_t digital);

typedef void(__stdcall* InternalLogging)(const char* value);

InternalLogging PluginLog;

extern "C"
{
	__declspec(dllexport) bool SetDX11(unsigned long long struct_device, unsigned long long rtm, const char* dllPath);
	__declspec(dllexport) void UnsetDX11();
	__declspec(dllexport) stTexture* GetRenderTexture(int curEye);
	__declspec(dllexport) stTexture* GetDepthTexture(int curEye);
	__declspec(dllexport) stTexture* GetUIRenderTexture(int curEye);
	__declspec(dllexport) void Recenter();
	__declspec(dllexport) void UpdateConfiguration(stConfiguration newConfig);
	__declspec(dllexport) void SetFramePose();
	__declspec(dllexport) void WaitGetPoses();
	__declspec(dllexport) uMatrix GetFramePose(poseType poseType, int eye);
	__declspec(dllexport) fingerHandLayout GetSkeletalPose(poseType poseType);
	__declspec(dllexport) void SetThreadedEye(int eye);
	__declspec(dllexport) void RenderVR(XMMATRIX curProjection, XMMATRIX curViewMatrixWithoutHMD, XMMATRIX rayMatrix, XMMATRIX watchMatrix, POINT virtualMouse, bool dalamudMode, bool floatingUI);
	__declspec(dllexport) void RenderUI();
	__declspec(dllexport) void RenderUID(unsigned long long struct_deivce, XMMATRIX curProjection, XMMATRIX curViewMatrixWithoutHMD);
	__declspec(dllexport) POINT GetBufferSize();
	__declspec(dllexport) void ResizeWindow(HWND hwnd, int width, int height);
	__declspec(dllexport) void MoveWindowPos(HWND hwnd, int adapterId, bool reset);

	__declspec(dllexport) bool SetActiveJSON(const char*, int size);
	__declspec(dllexport) void UpdateController(UpdateControllerInput controllerCallback);
	__declspec(dllexport) void HapticFeedback(buttonLayout side, float time, float freq, float amp);

	__declspec(dllexport) void SetLogFunction(InternalLogging internalLogging);
	__declspec(dllexport) void SetRayCoordinate(float* posFrom, float* posTo);
}



BOOL APIENTRY DllMain(HMODULE hModule,
	DWORD  ul_reason_for_call,
	LPVOID lpReserved
)
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH: InitInstance(hModule); break;
	case DLL_PROCESS_DETACH: ExitInstance(); break;
	case DLL_THREAD_ATTACH:  break;
	case DLL_THREAD_DETACH:  break;
	}
	return TRUE;
}

void InitInstance(HANDLE hModule)
{
	device = nullptr;
	rtManager = nullptr;
	outputLog.str("");
}

void ExitInstance()
{
	UnsetDX11();
}

void forceFlush()
{
	PluginLog(outputLog.str().c_str());
	outputLog.str("");
}

void RunOSKEnable()
{
	showOSK = false;
	if (device)
	{
		oskRenderCount = 50;
		RECT position = { 100, 100, 700, 200 };
		if (!osk.LoadOSK(device->Device, &oskTexture, position))
		{
			outputLog << "Error creating/finding the OnScreen Keyboard" << std::endl;
		}
		oskLayout = osk.GetOSKLayout();
	}
}

void RunOSKDisable()
{
	showOSK = false;
	osk.ShowHide(true);
	osk.UnloadOSK();
	oskTexture.Release();
	oskLayout = nullptr;
	oskRenderCount = -1;
}

void RunOSKUpdate()
{
	//----
	// Hide keyboard if shown after a few frames
	//----
	if (cfg.osk && oskRenderCount <= 0 && oldOSK != showOSK)
	{
		oldOSK = showOSK;
		osk.ShowHide(showOSK);
	}
	if (oskLayout != nullptr && oskLayout->haveLayout && oskRenderCount > 0)
		oskRenderCount--;
	else if (cfg.osk && oskLayout != nullptr && !oskLayout->haveLayout)
	{
		RunOSKDisable();
		RunOSKEnable();
	}

	if (cfg.osk && showOSK)
		osk.CopyOSKTexture(device->Device, device->DeviceContext, &oskTexture);
}

bool CreateBackbufferClone()
{
	bool retVal = true;
	HRESULT result = S_OK;
	DestroyBackbufferClone();

	for (int i = 0; i < 6; i++)
	{
		//----
		// Create the backbuffer copy based on the backbuffer description
		//----
		gameRenderTexture->Texture->GetDesc(&BackBufferCopy[i].textureDesc);
		//BackBufferCopy[i].textureDesc.Width *= 2;
		BackBufferCopy[i].textureDesc.BindFlags |= D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
		BackBufferCopy[i].textureDesc.MiscFlags |= D3D11_RESOURCE_MISC_SHARED;
		if (!BackBufferCopy[i].Create(device->Device, true, true, true))
		{
			outputLog << BackBufferCopy[i].GetErrors();
			retVal = false;
		}
		
		if (BackBufferCopy[i].pTexture == nullptr)
			outputLog << "Error creating BackbufferCopy " << i << std::endl;
		if (BackBufferCopy[i].pRenderTarget == nullptr)
			outputLog << "Error creating BackbufferCopyRTV " << i << std::endl;
		if (BackBufferCopy[i].pShaderResource == nullptr)
			outputLog << "Error creating BackbufferCopySRV " << i << std::endl;
		if (BackBufferCopy[i].pSharedHandle == nullptr)
			outputLog << "Error creating shared handle " << i << std::endl;
		if (retVal == false)
			return false;

		//----
		// Create the depthbuffer copy based on the depthbuffer description
		//----
		gameDepthTexture->Texture->GetDesc(&DepthBufferCopy[i].textureDesc);
		//DepthBufferCopy[i].textureDesc.Width *= 2;
		DepthBufferCopy[i].textureDesc.MiscFlags |= D3D11_RESOURCE_MISC_SHARED;
		if (!DepthBufferCopy[i].Create(device->Device, false, false, true))
		{
			outputLog << DepthBufferCopy[i].GetErrors();
			retVal = false;
		}
		if (!DepthBufferCopy[i].CreateDepthStencilView(device->Device, DXGI_FORMAT_D24_UNORM_S8_UINT))
		{
			outputLog << DepthBufferCopy[i].GetErrors();
			retVal = false;
		}
		if (DepthBufferCopy[i].pTexture == nullptr)
			outputLog << "Error creating DepthBufferCopy " << i << std::endl;
		if (DepthBufferCopy[i].pDepthStencilView == nullptr)
			outputLog << "Error creating DepthStencilView " << i << std::endl;
		if (DepthBufferCopy[i].pSharedHandle == nullptr)
			outputLog << "Error creating shared handle " << i << std::endl;
		if (retVal == false)
			return false;

		//----
		// Create the render target based on the backbuffer description
		//----
		gameRenderTexture->Texture->GetDesc(&RenderTarget[i].textureDesc);
		RenderTarget[i].textureDesc.BindFlags |= D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
		RenderTarget[i].textureDesc.MiscFlags |= D3D11_RESOURCE_MISC_SHARED;
		if (!RenderTarget[i].Create(device->Device, true, true, true))
		{
			outputLog << RenderTarget[i].GetErrors();
			retVal = false;
		}

		if (RenderTarget[i].pTexture == nullptr)
			outputLog << "Error creating RenderTarget " << i << std::endl;
		if (RenderTarget[i].pRenderTarget == nullptr)
			outputLog << "Error creating RenderTarget RTV " << i << std::endl;
		if (RenderTarget[i].pShaderResource == nullptr)
			outputLog << "Error creating RenderTarget SRV " << i << std::endl;
		if (retVal == false)
			return false;

	}
	for (int i = 0; i < 2; i++)
	{
		//----
		// Create the ui render target based on the backbuffer description
		//----
		gameRenderTexture->Texture->GetDesc(&uiRenderTarget[i].textureDesc);
		uiRenderTarget[i].textureDesc.BindFlags |= D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
		if (!uiRenderTarget[i].Create(device->Device, true, true, true))
		{
			outputLog << uiRenderTarget[i].GetErrors();
			retVal = false;
		}

		if (uiRenderTarget[i].pTexture == nullptr)
			outputLog << "Error creating uiRenderTarget " << i << std::endl;
		if (uiRenderTarget[i].pRenderTarget == nullptr)
			outputLog << "Error creating uiRenderTarget RTV " << i << std::endl;
		if (uiRenderTarget[i].pShaderResource == nullptr)
			outputLog << "Error creating uiRenderTarget SRV " << i << std::endl;
		if (retVal == false)
			return false;
	}

	BackBuffer.pTexture->GetDesc(&dalamudBuffer.textureDesc);
	dalamudBuffer.textureDesc.BindFlags |= D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
	dalamudBuffer.textureDesc.MiscFlags |= D3D11_RESOURCE_MISC_SHARED;
	if (!dalamudBuffer.Create(device->Device, true, true, true))
	{
		outputLog << dalamudBuffer.GetErrors();
		retVal = false;
	}

	if (dalamudBuffer.pTexture == nullptr)
		outputLog << "Error creating dalamudBuffer " << std::endl;
	if (dalamudBuffer.pShaderResource == nullptr)
		outputLog << "Error creating dalamudBufferSRV " << std::endl;
	if (retVal == false)
		return false;

	return retVal;
}

void DestroyBackbufferClone()
{
	for (int i = 0; i < 6; i++)
	{
		//----
		// Release the backbuffer copy
		//----
		BackBufferCopy[i].Release();
		DepthBufferCopy[i].Release();
		RenderTarget[i].Release();

	}
	for (int i = 0; i < 2; i++)
	{
		uiRenderTarget[i].Release();
	}

	dalamudBuffer.Release();
}

__declspec(dllexport) bool SetDX11(unsigned long long struct_device, unsigned long long rtm, const char* dllPath)
{
	if(cfg.vLog)
		outputLog << std::endl << "SetDX11:" << std::endl;
	if (device == nullptr && enabled == false)
	{
		device = (stDevice*)struct_device;
		rtManager = (stRenderTargetManager*)rtm;

		if (device == nullptr)
		{
			outputLog << "ERROR: Can not find device" << std::endl;
			return false;
		}

		if (rtManager == nullptr)
		{
			outputLog << "ERROR: Can not find rtManager" << std::endl;
			return false;
		}

		if (device->IDXGIFactory == nullptr)
		{
			outputLog << "ERROR: Can not find IDXGIFactory" << std::endl;
			return false;
		}

		if (device->Device == nullptr)
		{
			outputLog << "ERROR: Can not find dx Device" << std::endl;
			return false;
		}

		if (device->DeviceContext == nullptr)
		{
			outputLog << "ERROR: Can not find DeviceContext" << std::endl;
			return false;
		}

		if (device->SwapChain == nullptr)
		{
			outputLog << "ERROR: Can not find SwapChain" << std::endl;
			return false;
		}

		if (device->SwapChain->DXGISwapChain == nullptr)
		{
			outputLog << "ERROR: Can not find SwapChain 1" << std::endl;
			return false;
		}

		if (device->SwapChain->BackBuffer == nullptr)
		{
			outputLog << "ERROR: Can not find SwapChain BackBuffer" << std::endl;
			return false;
		}

		if (device->SwapChain->BackBuffer->Texture == nullptr)
		{
			outputLog << "ERROR: Can not find SwapChain BackBuffer Texture" << std::endl;
			return false;
		}

		if (cfg.vLog)
		{
			for (int monitorIndex = 0; monitorIndex < monitors.iMonitors.size(); monitorIndex++)
			{
				outputLog << std::dec << "Screen id: " << monitorIndex << std::endl;
				outputLog << "-----------------------------------------------------" << std::endl;
				outputLog << " - screen left-top corner coordinates : (" << monitors.rcMonitors[monitorIndex].left << "," << monitors.rcMonitors[monitorIndex].top << ")" << std::endl;
				outputLog << " - screen dimensions (width x height) : (" << std::abs(monitors.rcMonitors[monitorIndex].right - monitors.rcMonitors[monitorIndex].left) << "," << std::abs(monitors.rcMonitors[monitorIndex].top - monitors.rcMonitors[monitorIndex].bottom) << ")" << std::endl;
				outputLog << "-----------------------------------------------------" << std::endl;
			}
		}

		if (cfg.vLog)
		{
			outputLog << std::hex << "SetDX Dx:" << struct_device << " RndTrg:" << rtm << std::endl;
			outputLog << "factory: " << device->IDXGIFactory << std::endl;
			outputLog << "Dev: " << device->Device << std::endl;
			outputLog << "DevCon: " << device->DeviceContext << std::endl;
			outputLog << "Swap: " << device->SwapChain->DXGISwapChain << std::endl;
			outputLog << "BackBuffer: " << device->SwapChain->BackBuffer << std::endl;
			outputLog << std::dec << "Device Size: " << device->width << "x" << device->height << " : " << device->newWidth << "x" << device->newHeight << std::endl;
			forceFlush();
		}

		std::string imgFilePaths[] = {
			"\\images\\blank.png",		    "\\images\\blank.png",
			"\\images\\weapon_off.png",     "\\images\\weapon_on.png",
			"\\images\\recenter_off.png",   "\\images\\recenter_on.png",
			"\\images\\keyboard_off.png",   "\\images\\keyboard_on.png",
			"\\images\\blank.png",          "\\images\\blank.png",
			"\\images\\occlusion_off.png",  "\\images\\occlusion_on.png",
			"\\images\\xivr_off.png",       "\\images\\xivr_on.png",
			"\\images\\dalamud_off.png",    "\\images\\dalamud_on.png",
			"\\images\\hide_ui_off.png",    "\\images\\hide_ui_on.png"
		};

		for (int i = 0; i < (handWatchCount * 2); i++)
		{
			struct stat buffer;
			std::string fullPath = dllPath + imgFilePaths[i];
			if (stat(fullPath.c_str(), &buffer) == 0)
			{
				if (!handWatchList[i].CreateFromFile(device->Device, false, true, false, fullPath.c_str()))
					outputLog << handWatchList[i].GetErrors() << std::endl;
			}
			else
				outputLog << "Image not found " << fullPath << std::endl;
		}
		screenLayout.SetFromSwapchain(device->SwapChain->DXGISwapChain);
		device->SwapChain->BackBuffer->Texture->GetDesc(&BackBufferDesc);
		BackBufferDesc.BindFlags |= D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;

		BackBuffer.textureDesc = BackBufferDesc;
		BackBuffer.creationType = 2;
		BackBuffer.pTexture = device->SwapChain->BackBuffer->Texture;
		BackBuffer.pRenderTarget = device->SwapChain->BackBuffer->itemPtr->RenderTargetView;

		if (cfg.vLog)
		{
			outputLog << std::dec << "BB Desc: u:" << BackBufferDesc.Usage << " f:" << BackBufferDesc.Format << " w:" << BackBufferDesc.Width << " h:" << BackBufferDesc.Height << std::hex << " rendTrgt:" << BackBuffer.pRenderTarget << std::endl;
			outputLog << std::hex << "BackBuffer: " << device->SwapChain->BackBuffer << " : " << device->SwapChain->BackBuffer->Texture << std::endl;
			outputLog << std::dec << "BackBuff: " << device->SwapChain->BackBuffer->Width << " : " << device->SwapChain->BackBuffer->Height << " : " << device->SwapChain->BackBuffer->TextureFormat << " : " << std::hex << device->SwapChain->BackBuffer->Flags << std::endl;
			forceFlush();
		}

		// 0x1403dc651 | [[ffxiv_dx11.exe+20A9FD0] + 0x3DB8]
		// sig for 48 8b 0d ?? ?? ?? ?? 8B 81 E4 3F 00 00

		if (useBackBuffer)
		{
			gameRenderTexture = new stTexture();
			gameRenderTexture->Texture = BackBuffer.pTexture;
			gameRenderTexture->ShaderResourceView = BackBuffer.pShaderResource;
			gameRenderTexture->itemPtr = new unTexturePtr();
			gameRenderTexture->itemPtr->RenderTargetView = BackBuffer.pRenderTarget;
		}
		else
			gameRenderTexture = rtManager->RenderTextureArray1[worldID];
		gameRenderRaw = *gameRenderTexture;

		gameDepthTexture = rtManager->RenderTextureArray1[depthID];
		gameDepthRaw = *gameDepthTexture;

		gameDepthTexture->Texture->GetDesc(&DepthBufferDesc);

		DepthBuffer.creationType = 2;
		DepthBuffer.pTexture = rtManager->RenderTextureArray1[depthID]->Texture;
		DepthBuffer.pDepthStencilView = rtManager->RenderTextureArray1[depthID]->itemPtr->DepthStencilView;

		if (cfg.vLog)
		{
			outputLog << std::dec << "Depth Desc: u:" << DepthBufferDesc.Usage << " f:" << DepthBufferDesc.Format << " w:" << DepthBufferDesc.Width << " h:" << DepthBufferDesc.Height << " b:" << DepthBufferDesc.BindFlags << std::endl;
			forceFlush();
		}

		if (cfg.vLog)
		{
			outputLog << "Starting VR ..";
			forceFlush();
		}
		if (!svr->StartVR())
		{
			outputLog << ".. Error starting VR";
			outputLog << svr->GetErrors();
			forceFlush();
			return false;
		}
		if (svr->HasErrors())
		{
			outputLog << svr->GetErrors();
			forceFlush();
		}
		if (cfg.vLog)
		{
			outputLog << ".. Done" << std::endl;
			forceFlush();
		}

		if (cfg.vLog)
		{
			outputLog << "Starting Renderer ..";
			forceFlush();
		}
		if (!rend->SetDevice(device->Device, device->DeviceContext))
		{
			outputLog << ".. Error starting Renderer";
			outputLog << rend->GetErrors();
			forceFlush();
			return false;
		}
		if (rend->HasErrors())
		{
			outputLog << rend->GetErrors();
			forceFlush();
		}
		if (cfg.vLog)
		{
			outputLog << ".. Done" << std::endl;
			forceFlush();
		}

		if (cfg.vLog)
		{
			outputLog << "Creating BackBufferClone ..";
			forceFlush();
		}

		if (!CreateBackbufferClone())
		{
			outputLog << ".. Error creating BackBufferClone" << std::endl;
			forceFlush();
			return false;
		}
		if (cfg.vLog)
		{
			outputLog << ".. Done" << std::endl;
			forceFlush();
		}

		if (cfg.vLog)
		{
			outputLog << "Creating Textures ..";
			forceFlush();
		}
		for (int i = 0; i < 2; i++)
		{
			uiRenderTexture[i] = (*gameRenderTexture);
			uiRenderTexture[i].uk5 = 0x90000000 + i;
			uiRenderTexture[i].Texture = uiRenderTarget[i].pTexture;
			uiRenderTexture[i].ShaderResourceView = uiRenderTarget[i].pShaderResource;
			uiRenderTexture[i].itemPtr = new unTexturePtr();
			uiRenderTexture[i].itemPtr->RenderTargetView = uiRenderTarget[i].pRenderTarget;
		}
		for (int i = 0; i < 6; i++)
		{
			RenderTexture[i] = (*gameRenderTexture);
			RenderTexture[i].RefCount = 0;
			RenderTexture[i].Texture = RenderTarget[i].pTexture;
			RenderTexture[i].ShaderResourceView = RenderTarget[i].pShaderResource;
			RenderTexture[i].itemPtr = new unTexturePtr();
			RenderTexture[i].itemPtr->RenderTargetView = RenderTarget[i].pRenderTarget;
			/*
			RenderTexture[i] = (*gameRenderTexture);
			RenderTexture[i].RefCount = 0;
			RenderTexture[i].Texture = BackBufferCopy[i].pTexture;
			RenderTexture[i].ShaderResourceView = BackBufferCopy[i].pShaderResource;
			RenderTexture[i].itemPtr = new unTexturePtr();
			RenderTexture[i].itemPtr->RenderTargetView = BackBufferCopy[i].pRenderTarget;
			*/
			DepthTexture[i] = (*gameDepthTexture);
			DepthTexture[i].RefCount = 0;
			DepthTexture[i].Texture = DepthBufferCopy[i].pTexture;
			DepthTexture[i].ShaderResourceView = DepthBufferCopy[i].pShaderResource;
			DepthTexture[i].itemPtr = new unTexturePtr();
			DepthTexture[i].itemPtr->DepthStencilView = DepthBufferCopy[i].pDepthStencilView;
		}
		if (cfg.vLog)
		{
			outputLog << ".. Done" << std::endl;
			forceFlush();
		}

		if (cfg.vLog)
		{
			outputLog << "Creating Viewport ..";
			forceFlush();
		}

		ZeroMemory(&viewports, sizeof(D3D11_VIEWPORT) * 2);
		viewports[0].TopLeftX = 0.5f;
		viewports[0].TopLeftY = 0.5f;
		viewports[0].Width = (float)screenLayout.width;
		viewports[0].Height = (float)screenLayout.height;
		viewports[0].MinDepth = 0.0f;
		viewports[0].MaxDepth = 1.0f;

		viewports[1].TopLeftX = 0.5f + (float)screenLayout.width;
		viewports[1].TopLeftY = 0.5f;
		viewports[1].Width = (float)screenLayout.width;
		viewports[1].Height = (float)screenLayout.height;
		viewports[1].MinDepth = 0.0f;
		viewports[1].MaxDepth = 1.0f;

		ZeroMemory(&viewport, sizeof(D3D11_VIEWPORT));
		viewports[2].TopLeftX = 0.5f;
		viewports[2].TopLeftY = 0.5f;
		viewports[2].Width = (float)screenLayout.width;
		viewports[2].Height = (float)screenLayout.height;
		viewports[2].MinDepth = 0.0f;
		viewports[2].MaxDepth = 1.0f;

		if (cfg.vLog)
		{
			outputLog << ".. Done" << std::endl;
			forceFlush();
		}

		XMVECTOR eyePos = { 0, 0, 0 };
		XMVECTOR lookAt = { 0, 0, -1 };
		XMVECTOR viewUp = { 0, 1, 0 };

		matrixSet.gameWorldMatrixFloating = XMMatrixLookAtRH(eyePos, lookAt, viewUp);
		matrixSet.gameWorldMatrix = matrixSet.gameWorldMatrixFloating;

		setActionHandlesGame(&steamInput);
		if (cfg.osk)
			RunOSKEnable();
		enabled = true;
	}
	if (cfg.vLog)
	{
		outputLog << "SetDX11 .. Done:" << std::endl;
		forceFlush();
	}

	return enabled;
}



__declspec(dllexport) void UnsetDX11()
{
	RunOSKDisable();

	for (int i = 0; i < (handWatchCount * 2); i++)
		handWatchList[i].Release();

	if (cfg.vLog)
		outputLog << std::endl << "StopDX11" << std::endl;
	
	if (cfg.vLog)
		outputLog << "Destroying BackBufferClone ..";
	DestroyBackbufferClone();
	if (cfg.vLog)
		outputLog << ".. Done" << std::endl;

	if (cfg.vLog)
		outputLog << "Releasing Renderer ..";
	rend->Release();
	if (cfg.vLog)
		outputLog << ".. Done" << std::endl;

	if (cfg.vLog)
		outputLog << ".. Done" << std::endl;

	if (cfg.vLog)
		outputLog << "Stopping VR ..";
	svr->StopVR();
	if (cfg.vLog)
		outputLog << ".. Done" << std::endl;

	device = nullptr;
	if (cfg.vLog)
		outputLog << "StopDX11" << std::endl;
	forceFlush();

	enabled = false;
}

__declspec(dllexport) stTexture* GetRenderTexture(int curEye)
{
	//int sId = ((backbufferSwap + 1) % 3) + (curEye * 3);
	int sId = backbufferSwap + (curEye * 3);
	return &RenderTexture[sId];
}

__declspec(dllexport) stTexture* GetDepthTexture(int curEye)
{
	//int sId = ((backbufferSwap + 1) % 3) + (curEye * 3);
	int sId = backbufferSwap + (curEye * 3);
	return &DepthTexture[sId];
}

__declspec(dllexport) stTexture* GetUIRenderTexture(int curEye)
{
	int sId = ((backbufferSwap + 1) % 3) + (curEye * 3);
	return &uiRenderTexture[curEye];
}

__declspec(dllexport) void Recenter()
{
	svr->Recenter();
}

__declspec(dllexport) void UpdateConfiguration(stConfiguration newConfig)
{
	if (newConfig.osk == true && cfg.osk == false)
	{
		showOSK = false;
		oldOSK = true;
		RunOSKEnable();
	}
	else if (newConfig.osk == false && cfg.osk == true)
	{
		RunOSKDisable();
	}

	cfg = newConfig;
	if (svr->isEnabled())
		svr->MakeIPDOffset();

}

__declspec(dllexport) void SetFramePose()
{
	svr->SetFramePose();
}

__declspec(dllexport) void WaitGetPoses()
{
	svr->WaitGetPoses();
}

__declspec(dllexport) uMatrix GetFramePose(poseType poseType, int eye)
{
	return svr->GetFramePose(poseType, eye);
}

__declspec(dllexport) fingerHandLayout GetSkeletalPose(poseType poseType)
{
	return svr->GetSkeletalPose(poseType);
}

__declspec(dllexport) void SetThreadedEye(int eye)
{
	threadedEye = eye;
	if ((threadedEyeIndexCount % 2) == 0)
	{
		int sId = backbufferSwap;
		if(isFloating)
			device->DeviceContext->CopyResource(RenderTarget[sId + (eye * 3)].pTexture, gameRenderTexture->Texture);
		else
			device->DeviceContext->CopyResource(BackBufferCopy[sId + (eye * 3)].pTexture, gameRenderTexture->Texture);
		device->DeviceContext->CopyResource(DepthBufferCopy[sId + (eye * 3)].pTexture, gameDepthTexture->Texture);
	}
	threadedEyeIndexCount++;
}

__declspec(dllexport) void RenderVR(XMMATRIX curProjection, XMMATRIX curViewMatrixWithoutHMD, XMMATRIX rayMatrix, XMMATRIX watchMatrix, POINT virtualMouse, bool dalamudMode, bool floatingUI)
{
	if (enabled)// && (threadedEye == 1 || cfg.mode2d == true))
	{
		int sId = backbufferSwap;
		int sIdL = sId + 0;
		int sIdR = sId + 3;
		backbufferSwap = (backbufferSwap + 1) % 3;

		matrixSet.hmdMatrix = (XMMATRIX)(svr->GetFramePose(poseType::hmdPosition, -1)._m);
		matrixSet.lhcMatrix = watchMatrix;// (XMMATRIX)(svr->GetFramePose(poseType::LeftHand, -1)._m);
		matrixSet.rhcMatrix = rayMatrix;// (XMMATRIX)(svr->GetFramePose(poseType::RightHand, -1)._m);

		isFloating = floatingUI;
		if (isFloating)
			matrixSet.gameWorldMatrix = matrixSet.gameWorldMatrixFloating;
		else
			matrixSet.gameWorldMatrix = curViewMatrixWithoutHMD;

		//----
		// Sets the mouse and ray for the next frame with the current tracking data
		//----
		if (cfg.motioncontrol)
			rend->RunFrameUpdate(&screenLayout, oskLayout, matrixSet.rhcMatrix, matrixSet.oskOffset, poseType::RightHand, dalamudMode, showOSK);
		else if (cfg.hmdPointing)
			rend->RunFrameUpdate(&screenLayout, oskLayout, matrixSet.hmdMatrix, matrixSet.oskOffset, poseType::hmdPosition, dalamudMode, showOSK);
		else
			rend->RunFrameUpdate(&screenLayout, oskLayout, XMMatrixIdentity(), matrixSet.oskOffset, poseType::None, dalamudMode, showOSK);
		rend->RenderLines(LineRender);

		rend->SetMouseBuffer(screenLayout.hwnd, screenLayout.width, screenLayout.height, virtualMouse.x, virtualMouse.y, dalamudMode);

		if (rend->HasErrors())
		{
			outputLog << rend->GetErrors();
			forceFlush();
		}
		

		ID3D11ShaderResourceView* watchShaderView[18] =
		{
			handWatchList[ 0].pShaderResource, handWatchList[ 1].pShaderResource,
			handWatchList[ 2].pShaderResource, handWatchList[ 3].pShaderResource,
			handWatchList[ 4].pShaderResource, handWatchList[ 5].pShaderResource,
			handWatchList[ 6].pShaderResource, handWatchList[ 7].pShaderResource,
			handWatchList[ 8].pShaderResource, handWatchList[ 9].pShaderResource,
			handWatchList[10].pShaderResource, handWatchList[11].pShaderResource,
			handWatchList[12].pShaderResource, handWatchList[13].pShaderResource,
			handWatchList[14].pShaderResource, handWatchList[15].pShaderResource,
			handWatchList[16].pShaderResource, handWatchList[17].pShaderResource,
		};

		for (int i = 0; i < 2; i++)
		{
			int curEyeView = (cfg.swapEyesUI) ? swapEyes[i] : i;
			int vp = 2;

			matrixSet.projectionMatrix = (XMMATRIX)(svr->GetFramePose(poseType::Projection, curEyeView)._m);
			matrixSet.eyeMatrix = (cfg.mode2d) ? XMMatrixIdentity() : (XMMATRIX)(svr->GetFramePose(poseType::EyeOffset, curEyeView)._m);

			if (isFloating)
			{
				rend->SetClearColor(BackBufferCopy[sId + (i * 3)].pRenderTarget, DepthBufferCopy[sId + (i * 3)].pDepthStencilView, new float[4] { 0.0f, 0.0f, 0.0f, 0.f }, isFloating);
				rend->SetRenderTarget(BackBufferCopy[sId + (i * 3)].pRenderTarget, DepthBufferCopy[sId + (i * 3)].pDepthStencilView);
				rend->DoRender(viewports[vp], RenderTarget[sId + (i * 3)].pShaderResource, &matrixSet, 1, isFloating, !isFloating);
			}
			else
			{
				//rend->SetClearColor(BackBufferCopy[sId + (i * 3)].pRenderTarget, DepthBufferCopy[sId + (i * 3)].pDepthStencilView, new float[4] { 0.0f, 0.0f, 0.0f, 0.f }, isFloating);
				rend->SetRenderTarget(BackBufferCopy[sId + (i * 3)].pRenderTarget, DepthBufferCopy[sId + (i * 3)].pDepthStencilView);
				//rend->DoRender(viewports[vp], RenderTarget[sId + (i * 3)].pShaderResource, &matrixSet, 1, isFloating, !isFloating);
			}
			rend->DoRenderLine(viewports[vp], &matrixSet);
			rend->DoRenderRay(viewports[vp], &matrixSet);
			if (showUI)
			{
				//----
				// ui uses left eye only so ui elements line up properly
				//----
				rend->DoRender(viewports[vp], uiRenderTarget[0].pShaderResource, &matrixSet, 0, cfg.uiDepth);
				if (!useBackBuffer)
					rend->DoRender(viewports[vp], dalamudBuffer.pShaderResource, &matrixSet, 2, cfg.uiDepth);
			}
			rend->DoRenderOSK(viewports[vp], oskTexture.pShaderResource, &matrixSet, 0, cfg.uiDepth);
			
			//matrixSet.lhcMatrix
			rend->DoRenderWatch(viewports[vp], watchShaderView, &matrixSet, 0);
		}

		svr->Render(BackBufferCopy[sIdL].pTexture, DepthBufferCopy[sIdL].pTexture, BackBufferCopy[sIdR].pTexture, DepthBufferCopy[sIdR].pTexture);
		if (svr->HasErrors())
		{
			outputLog << svr->GetErrors();
			forceFlush();
		}

		device->DeviceContext->CopyResource(dalamudBuffer.pTexture, BackBuffer.pTexture);
		RunOSKUpdate();
	}
	LineRender.clear();
	threadedEyeIndexCount = 0;
}

__declspec(dllexport) void RenderUI()
{
	if (enabled)
	{
		if (useBackBuffer == false)
			device->DeviceContext->ClearRenderTargetView(BackBuffer.pRenderTarget, new float[4] { 0.f, 0.f, 0.f, 1.f });
	}
}

__declspec(dllexport) void RenderUID(unsigned long long struct_device, XMMATRIX curProjection, XMMATRIX curViewMatrixWithoutHMD)
{
	device = (stDevice*)struct_device;
	if (device != nullptr && device->SwapChain->BackBuffer->itemPtr->RenderTargetView != nullptr)
	{
		matrixSet.projectionMatrix = curProjection;
		matrixSet.gameWorldMatrix = curViewMatrixWithoutHMD;
		matrixSet.eyeMatrix = XMMatrixIdentity();
		matrixSet.hmdMatrix = XMMatrixIdentity();

		D3D11_VIEWPORT viewport;
		viewport.TopLeftX = 0.5f;
		viewport.TopLeftY = 0.5f;
		viewport.Width = device->SwapChain->BackBuffer->Width;
		viewport.Height = device->SwapChain->BackBuffer->Height;
		viewport.MinDepth = 0.0f;
		viewport.MaxDepth = 1.0f;
		
		rend->SetDevice(device->Device, device->DeviceContext);
		//rend->SetClearColor(device->SwapChain->BackBuffer->itemPtr->RenderTargetView, NULL, new float[4] { 0.0f, 0.0f, 0.0f, 0.f }, true);
		rend->SetRenderTarget(device->SwapChain->BackBuffer->itemPtr->RenderTargetView, NULL);
		rend->RenderLines(LineRender);
		rend->DoRenderLine(viewport, &matrixSet);
		LineRender.clear();
	}
}

__declspec(dllexport) POINT GetBufferSize()
{
	svr->PreloadVR();
	return svr->GetBufferSize();
}

__declspec(dllexport) void ResizeWindow(HWND hwnd, int width, int height)
{
	if (hwnd != 0)
	{
		RECT clientRect = RECT();
		clientRect.top = 0;
		clientRect.left = 0;
		clientRect.bottom = height;
		clientRect.right = width;

		RECT rcClient, rcWind;
		POINT diff;
		GetClientRect(hwnd, &rcClient);
		GetWindowRect(hwnd, &rcWind);
		diff.x = (rcWind.right - rcWind.left) - rcClient.right;
		diff.y = (rcWind.bottom - rcWind.top) - rcClient.bottom;

		AdjustWindowRect(&clientRect, GetWindowLongA(hwnd, GWL_STYLE), false);
		SetWindowPos(hwnd, 0, 0, 0, width + diff.x, height + diff.y, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOZORDER | SWP_FRAMECHANGED);
		SendMessageA(hwnd, WM_EXITSIZEMOVE, WPARAM(0), LPARAM(0));
	}
}

__declspec(dllexport) void MoveWindowPos(HWND hwnd, int adapterId, bool reset)
{
	if (hwnd != 0)
	{
		RECT clientRect = RECT();
		GetClientRect(hwnd, &clientRect);

		int xOffset = 0;
		int yOffset = 0;

		if (reset == false)
		{
			int cWidth = (clientRect.right - clientRect.left) / 2;
			int cHeight = (clientRect.bottom - clientRect.top) / 2;
			int sWidth = (monitors.rcMonitors[adapterId].right - monitors.rcMonitors[adapterId].left) / 2;
			int sHeight = (monitors.rcMonitors[adapterId].bottom - monitors.rcMonitors[adapterId].top) / 2;

			xOffset = sWidth - cWidth;
			yOffset = sHeight - cHeight;

			//outputLog << cWidth << " : " << cHeight << "  :  " << sWidth << " : " << sHeight << "  :  " << xOffset << " : " << yOffset << std::endl;
		}

		SetWindowPos(hwnd, 0, xOffset, yOffset, 0, 0, SWP_NOACTIVATE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
	}
}

__declspec(dllexport) bool SetActiveJSON(const char* filePath, int size)
{
	if (svr->isEnabled())
	{
		vr::EVRInputError iError = vr::VRInputError_None;
		if (size > 0) {
			iError = vr::VRInput()->SetActionManifestPath(filePath);
			if (iError)
				return false;
		}
		return true;
	}
	return false;
}

int GlobalBoneCounter = 0;
int GlobalBoneCounterMax = 5500;

bool rightTriggerClick_Current = false;
bool rightTriggerClick_Changed = false;
bool rightBumperClick_Current = false;
bool rightBumperClick_Changed = false;
bool LeftMouseDown = false;
bool RightBumperDown = false;
XMVECTOR controllerValueDown = { 0, 0, 0, 0 };

void RightBumperCheck(UpdateControllerInput controllerCallback, float curRightBumperValue)
{
	bool clickCurrent = (curRightBumperValue > 0.75f);

	rightBumperClick_Changed = false;
	if (rightBumperClick_Current != clickCurrent)
	{
		rightBumperClick_Current = clickCurrent;
		rightBumperClick_Changed = true;
	}

	if (rightBumperClick_Current && rightBumperClick_Changed)
	{
		RightBumperDown = true;
		controllerValueDown = matrixSet.rhcMatrix.r[3];
	}
	else if (!rightBumperClick_Current && rightBumperClick_Changed)
	{
		RightBumperDown = false;
		controllerValueDown = { 0, 0, 0, 0 };
	}

	if (RightBumperDown)
	{
		XMVECTOR diff = controllerValueDown - matrixSet.rhcMatrix.r[3];
		controllerValueDown = matrixSet.rhcMatrix.r[3];

		bool status[11];
		for (int i = 0; i < handWatchCount + 2; i++)
			status[i] = false;

		rend->GetUIStatus(status, handWatchCount + 2);
		if (status[handWatchCount + 0])
		{
			matrixSet.oskOffset.x -= diff.m128_f32[0] * 2;
			matrixSet.oskOffset.y -= diff.m128_f32[1] * 2;
			//matrixSet.oskOffset.z -= diff.m128_f32[2];
			matrixSet.oskOffset.w = 0;
		}
	}
}

void RightTriggerCheck(UpdateControllerInput controllerCallback, float curRightTriggerValue)
{
	buttonLayout watchButtons[] =
	{
		buttonLayout::watch_audio,
		buttonLayout::watch_weapon,
		buttonLayout::watch_recenter,
		buttonLayout::watch_keyboard,
		buttonLayout::watch_none,
		buttonLayout::watch_occlusion,
		buttonLayout::watch_xivr,
		buttonLayout::watch_dalamud,
		buttonLayout::watch_ui
	};

	rightTriggerClick_Changed = false;
	bool clickCurrent = (curRightTriggerValue > 0.75f);

	if (rightTriggerClick_Current != clickCurrent)
	{
		rightTriggerClick_Current = clickCurrent;
		rightTriggerClick_Changed = true;
	}

	if (rightTriggerClick_Current && rightTriggerClick_Changed)
	{
		LeftMouseDown = true;
	}
	else if (!rightTriggerClick_Current && rightTriggerClick_Changed)
	{
		LeftMouseDown = false;
 		vr::InputDigitalActionData_t digitalActionData = { 0 };
		vr::InputAnalogActionData_t analogActionData = { 0 };

		bool status[11];
		for (int i = 0; i < handWatchCount + 2; i++)
			status[i] = false;

		rend->GetUIStatus(status, handWatchCount + 2);

		if (status[0]) //buttonLayout::watch_audio
		{
			//digitalActionData.bActive = true;
			//controllerCallback(buttonLayout::watch_audio, analogActionData, digitalActionData);
		}
		
		if (status[1]) //buttonLayout::watch_weapon
		{
			digitalActionData.bActive = true;
			controllerCallback(buttonLayout::watch_weapon, analogActionData, digitalActionData);
		}

		if (status[2]) //buttonLayout::watch_recenter
		{
			Recenter();
		}

		if (status[3]) //buttonLayout::watch_keyboard
		{
			if (cfg.osk)
				showOSK = !showOSK;
		}

		if (status[4]) //buttonLayout::watch_none)
		{
			digitalActionData.bActive = true;
			controllerCallback(buttonLayout::watch_none, analogActionData, digitalActionData);
		}

		if (status[5]) //buttonLayout::watch_occlusion)
		{
			digitalActionData.bActive = true;
			controllerCallback(buttonLayout::watch_occlusion, analogActionData, digitalActionData);
		}

		if (status[6]) //buttonLayout::watch_xivr)
		{
			digitalActionData.bActive = true;
			controllerCallback(buttonLayout::watch_xivr, analogActionData, digitalActionData);
		}

		if (status[7]) //buttonLayout::watch_dalamud)
		{
			digitalActionData.bActive = true;
			controllerCallback(buttonLayout::watch_dalamud, analogActionData, digitalActionData);
		}

		if (status[8]) //buttonLayout::watch_ui
		{
			showUI = !showUI;
		}
	}
}

__declspec(dllexport) void UpdateController(UpdateControllerInput controllerCallback)
{
	if (svr->isEnabled())
	{
		SetFramePose();
		vr::VRActiveActionSet_t actionSet = { 0 };
		actionSet.ulActionSet = steamInput.game.setHandle;
		uint32_t setSize = sizeof(actionSet);
		uint32_t setCount = setSize / sizeof(vr::VRActiveActionSet_t);

		if (vr::VRInput()->UpdateActionState(&actionSet, setSize, setCount) == vr::VRInputError_None)
		{
			vr::InputDigitalActionData_t digitalActionData = { 0 };
			vr::InputAnalogActionData_t analogActionData = { 0 };
			vr::InputPoseActionData_t poseActionData = { 0 };
			vr::InputSkeletalActionData_t skeletalActionData = { 0 };
			vr::ETrackingUniverseOrigin eOrigin = vr::TrackingUniverseSeated;

			//----
			// Controllers
			//----
			
			if (vr::VRInput()->GetPoseActionDataForNextFrame(steamInput.game.lefthand_tip, eOrigin, &poseActionData, sizeof(poseActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && poseActionData.bActive == true)
				svr->SetActionPose(poseActionData.pose.mDeviceToAbsoluteTracking, poseType::LeftHand);
			if (vr::VRInput()->GetPoseActionDataForNextFrame(steamInput.game.righthand_tip, eOrigin, &poseActionData, sizeof(poseActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && poseActionData.bActive == true)
				svr->SetActionPose(poseActionData.pose.mDeviceToAbsoluteTracking, poseType::RightHand);
			
			if (vr::VRInput()->GetPoseActionDataForNextFrame(steamInput.game.lefthand_palm, eOrigin, &poseActionData, sizeof(poseActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && poseActionData.bActive == true)
				svr->SetActionPose(poseActionData.pose.mDeviceToAbsoluteTracking, poseType::LeftHandPalm);
			if (vr::VRInput()->GetPoseActionDataForNextFrame(steamInput.game.righthand_palm, eOrigin, &poseActionData, sizeof(poseActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && poseActionData.bActive == true)
				svr->SetActionPose(poseActionData.pose.mDeviceToAbsoluteTracking, poseType::RightHandPalm);

			if (vr::VRInput()->GetSkeletalActionData(steamInput.game.lefthand_anim, &skeletalActionData, sizeof(skeletalActionData)) == vr::VRInputError_None && skeletalActionData.bActive == true)
			{
				uint32_t boneCount = 0;
				vr::VRInput()->GetBoneCount(steamInput.game.lefthand_anim, &boneCount);
				vr::VRBoneTransform_t* boneArray = new vr::VRBoneTransform_t[boneCount];
				vr::VRInput()->GetSkeletalBoneData(steamInput.game.lefthand_anim, vr::VRSkeletalTransformSpace_Model, vr::VRSkeletalMotionRange_WithoutController, boneArray, boneCount);
				svr->SetSkeletalPose(boneArray, boneCount, poseType::LeftHand);
			}
			if (vr::VRInput()->GetSkeletalActionData(steamInput.game.righthand_anim, &skeletalActionData, sizeof(skeletalActionData)) == vr::VRInputError_None && skeletalActionData.bActive == true)
			{
				uint32_t boneCount = 0;
				vr::VRInput()->GetBoneCount(steamInput.game.righthand_anim, &boneCount);
				vr::VRBoneTransform_t* boneArray = new vr::VRBoneTransform_t[boneCount];
				vr::VRInput()->GetSkeletalBoneData(steamInput.game.righthand_anim, vr::VRSkeletalTransformSpace_Model, vr::VRSkeletalMotionRange_WithoutController, boneArray, boneCount);
				svr->SetSkeletalPose(boneArray, boneCount, poseType::RightHand);
			}

			//----
			// Movement
			//----

			if (vr::VRInput()->GetAnalogActionData(steamInput.game.movement, &analogActionData, sizeof(analogActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && analogActionData.bActive == true)
				controllerCallback(buttonLayout::movement, analogActionData, digitalActionData);
			if (vr::VRInput()->GetAnalogActionData(steamInput.game.rotation, &analogActionData, sizeof(analogActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && analogActionData.bActive == true)
				controllerCallback(buttonLayout::rotation, analogActionData, digitalActionData);

			//----
			// Mouse
			//----

			if (vr::VRInput()->GetDigitalActionData(steamInput.game.leftclick, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::leftClick, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.rightclick, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::rightClick, analogActionData, digitalActionData);

			//----
			// Keys
			//----

			if (vr::VRInput()->GetDigitalActionData(steamInput.game.recenter, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::recenter, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.shift, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::shift, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.alt, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::alt, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.control, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::control, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.escape, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::escape, analogActionData, digitalActionData);

			//----
			// F Keys
			//----
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.button01, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::button01, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.button02, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::button02, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.button03, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::button03, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.button04, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::button04, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.button05, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::button05, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.button06, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::button06, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.button07, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::button07, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.button08, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::button08, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.button09, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::button09, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.button10, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::button10, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.button11, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::button11, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.button12, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::button12, analogActionData, digitalActionData);

			//----
			// XBoox buttons
			//----
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.xbox_button_y, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::xbox_button_y, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.xbox_button_x, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::xbox_button_x, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.xbox_button_a, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::xbox_button_a, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.xbox_button_b, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::xbox_button_b, analogActionData, digitalActionData);
			if (vr::VRInput()->GetAnalogActionData(steamInput.game.xbox_left_trigger, &analogActionData, sizeof(analogActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && analogActionData.bActive == true)
				controllerCallback(buttonLayout::xbox_left_trigger, analogActionData, digitalActionData);
			if (vr::VRInput()->GetAnalogActionData(steamInput.game.xbox_left_bumper, &analogActionData, sizeof(analogActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && analogActionData.bActive == true)
				controllerCallback(buttonLayout::xbox_left_bumper, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.xbox_left_stick_click, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::xbox_left_stick_click, analogActionData, digitalActionData);
			if (vr::VRInput()->GetAnalogActionData(steamInput.game.xbox_right_trigger, &analogActionData, sizeof(analogActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && analogActionData.bActive == true)
			{
				RightTriggerCheck(controllerCallback, analogActionData.x);
				controllerCallback(buttonLayout::xbox_right_trigger, analogActionData, digitalActionData);
			}

			if (vr::VRInput()->GetAnalogActionData(steamInput.game.xbox_right_bumper, &analogActionData, sizeof(analogActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && analogActionData.bActive == true)
			{
				RightBumperCheck(controllerCallback, analogActionData.x);
				controllerCallback(buttonLayout::xbox_right_bumper, analogActionData, digitalActionData);
			}
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.xbox_right_stick_click, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::xbox_right_stick_click, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.xbox_pad_up, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::xbox_pad_up, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.xbox_pad_down, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::xbox_pad_down, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.xbox_pad_left, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::xbox_pad_left, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.xbox_pad_right, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::xbox_pad_right, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.xbox_start, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::xbox_start, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.xbox_select, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::xbox_select, analogActionData, digitalActionData);

			if (vr::VRInput()->GetDigitalActionData(steamInput.game.thumbrest_left, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::thumbrest_left, analogActionData, digitalActionData);
			if (vr::VRInput()->GetDigitalActionData(steamInput.game.thumbrest_right, &digitalActionData, sizeof(digitalActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && digitalActionData.bActive == true)
				controllerCallback(buttonLayout::thumbrest_right, analogActionData, digitalActionData);
		}
	}
}

__declspec(dllexport) void HapticFeedback(buttonLayout side, float time, float freq, float amp)
{
	if (side == buttonLayout::haptic_left)
		vr::VRInput()->TriggerHapticVibrationAction(steamInput.game.haptic_left, 0, time, freq, amp, vr::k_ulInvalidInputValueHandle);
	else if (side == buttonLayout::haptic_right)
		vr::VRInput()->TriggerHapticVibrationAction(steamInput.game.haptic_right, 0, time, freq, amp, vr::k_ulInvalidInputValueHandle);
}

__declspec(dllexport) void SetLogFunction(InternalLogging internalLogging)
{
	PluginLog = internalLogging;
}

__declspec(dllexport) void SetRayCoordinate(float* posFrom, float* posTo)
{
	if (posFrom != NULL && posTo != NULL)
	{
		LineRender.push_back({ posFrom[0], posFrom[1], posFrom[2], posTo[0], posTo[1], posTo[2] });
	}

}