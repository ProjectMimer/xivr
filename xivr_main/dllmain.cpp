#include "framework.h"
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
stTexture uiRenderTexture[2] = { stTexture(), stTexture() };
stTexture* gameRenderTexture = nullptr;
stTexture* gameDepthTexture = nullptr;
stTexture gameRenderRaw = stTexture();
stTexture gameDepthRaw = stTexture();

stBasicTexture BackBuffer = stBasicTexture();
stBasicTexture BackBufferCopy[2] = { stBasicTexture(), stBasicTexture() };
stBasicTexture BackBufferCopyShared[2] = { stBasicTexture(), stBasicTexture() };
stBasicTexture DepthBuffer = stBasicTexture();
stBasicTexture DepthBufferCopy[2] = { stBasicTexture(), stBasicTexture() };
stBasicTexture DepthBufferCopyShared[2] = { stBasicTexture(), stBasicTexture() };
stBasicTexture uiRenderTarget[2] = { stBasicTexture(), stBasicTexture() };
stMatrixSet matrixSet;

stBasicTexture dalamudBuffer = stBasicTexture();

inputController steamInput = {};
std::vector<std::vector<float>> LineRender = std::vector<std::vector<float>>();

stDX11 devDX11;
D3D11_VIEWPORT viewport;
bool enabled = false;
int threadedEye = 0;
bool logging = false;
int swapEyes[] = { 1, 0 };

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
	__declspec(dllexport) bool SetDX11(unsigned long long struct_deivce, unsigned long long rtm);
	__declspec(dllexport) void UnsetDX11();
	__declspec(dllexport) stTexture* GetUIRenderTexture(int curEye);
	__declspec(dllexport) void Recenter();
	__declspec(dllexport) void UpdateConfiguration(stConfiguration newConfig);
	__declspec(dllexport) void SetFramePose();
	__declspec(dllexport) void WaitGetPoses();
	__declspec(dllexport) uMatrix GetFramePose(poseType posetype, int eye);
	__declspec(dllexport) void SetThreadedEye(int eye);
	__declspec(dllexport) void RenderVR();
	__declspec(dllexport) void RenderUI(bool enableVR, bool enableFloatingHUD, XMMATRIX curViewMatrixWithoutHMD, POINT virtualMouse, bool dalamudMode);
	__declspec(dllexport) void RenderFloatingScreen(POINT virtualMouse, bool dalamudMode);
	__declspec(dllexport) void SetTexture();
	__declspec(dllexport) POINT GetBufferSize();
	__declspec(dllexport) void ResizeWindow(HWND hwnd, int width, int height);

	__declspec(dllexport) bool SetActiveJSON(const char*, int size);
	__declspec(dllexport) void UpdateController(UpdateControllerInput controllerCallback);
	__declspec(dllexport) void HapticFeedback(buttonLayout side, float time, float freq, float amp);

	__declspec(dllexport) void SetLogFunction(InternalLogging internalLogging);
	__declspec(dllexport) void SetRayCoordinate(float* posFrom, float* posTo);
}



BOOL APIENTRY DllMain( HMODULE hModule,
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

bool CreateBackbufferClone()
{
	bool retVal = true;
	HRESULT result = S_OK;
	DestroyBackbufferClone();

	for (int i = 0; i < 2; i++)
	{
		//----
		// Create the backbuffer copy based on the backbuffer description
		//----
		gameRenderTexture->Texture->GetDesc(&BackBufferCopy[i].textureDesc);
		BackBufferCopy[i].textureDesc.BindFlags |= D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
		BackBufferCopy[i].textureDesc.MiscFlags |= D3D11_RESOURCE_MISC_SHARED;
		if (!BackBufferCopy[i].Create(device->Device, true, true))
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
		// Set the shared handle and create a shared texture based off of it
		//----
		BackBufferCopyShared[i].textureDesc = BackBufferCopy[i].textureDesc;
		BackBufferCopyShared[i].pSharedHandle = BackBufferCopy[i].pSharedHandle;
		if (!BackBufferCopyShared[i].Create(devDX11.dev, true, true))
		{
			outputLog << BackBufferCopyShared[i].GetErrors();
			retVal = false;
		}

		if (BackBufferCopyShared[i].pTexture == nullptr)
			outputLog << "Error creating BackBufferCopyShared " << i << std::endl;
		if (BackBufferCopyShared[i].pRenderTarget == nullptr)
			outputLog << "Error creating BackBufferCopySharedRTV " << i << std::endl;
		if (BackBufferCopyShared[i].pShaderResource == nullptr)
			outputLog << "Error creating BackBufferCopySharedSRV " << i << std::endl;
		if (retVal == false)
			return false;

		//----
		// Create the ui render target based on the backbuffer description
		//----
		gameRenderTexture->Texture->GetDesc(&uiRenderTarget[i].textureDesc);
		uiRenderTarget[i].textureDesc.BindFlags |= D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
		if (!uiRenderTarget[i].Create(device->Device, true, true))
		{
			outputLog << uiRenderTarget[i].GetErrors();
			retVal = false;
		}

		if (uiRenderTarget[i].pTexture == nullptr)
			outputLog << "Error creating uiRenderTarget" << std::endl;
		if (uiRenderTarget[i].pRenderTarget == nullptr)
			outputLog << "Error creating uiRenderTarget RTV" << std::endl;
		if (uiRenderTarget[i].pShaderResource == nullptr)
			outputLog << "Error creating uiRenderTarget SRV" << std::endl;
		if (retVal == false)
			return false;

		//----
		// Create the depthbuffer copy based on the depthbuffer description
		//----
		gameDepthTexture->Texture->GetDesc(&DepthBufferCopy[i].textureDesc);
		DepthBufferCopy[i].textureDesc.MiscFlags |= D3D11_RESOURCE_MISC_SHARED;
		if (!DepthBufferCopy[i].Create(device->Device, false, false))
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
		// Set the shared handle and create a shared texture based off of it
		//----
		DepthBufferCopyShared[i].textureDesc = DepthBufferCopy[i].textureDesc;
		DepthBufferCopyShared[i].pSharedHandle = DepthBufferCopy[i].pSharedHandle;
		if (!DepthBufferCopyShared[i].Create(devDX11.dev, false, false))
		{
			outputLog << DepthBufferCopyShared[i].GetErrors();
			retVal = false;
		}
		if (!DepthBufferCopyShared[i].CreateDepthStencilView(devDX11.dev, DXGI_FORMAT_D24_UNORM_S8_UINT))
		{
			outputLog << DepthBufferCopyShared[i].GetErrors();
			retVal = false;
		}
		if (DepthBufferCopyShared[i].pTexture == nullptr)
			outputLog << "Error creating DepthBufferCopyShared " << i << std::endl;
		if (DepthBufferCopyShared[i].pDepthStencilView == nullptr)
			outputLog << "Error creating DepthBufferCopySharedView " << i << std::endl;
		if (retVal == false)
			return false;
	}

	BackBuffer.pTexture->GetDesc(&dalamudBuffer.textureDesc);
	dalamudBuffer.textureDesc.BindFlags |= D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
	if (!dalamudBuffer.Create(device->Device, true, true))
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
	for (int i = 0; i < 2; i++)
	{
		//----
		// Release the shared backbuffer copy
		// and the backbuffer copy
		//----
		BackBufferCopyShared[i].Release();
		BackBufferCopy[i].Release();

		uiRenderTarget[i].Release();

		DepthBufferCopyShared[i].Release();
		DepthBufferCopy[i].Release();
	}

	dalamudBuffer.Release();
}

__declspec(dllexport) bool SetDX11(unsigned long long struct_device, unsigned long long rtm)
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
			outputLog << std::hex << "SetDX Dx:" << struct_device << " RndTrg:" << rtm << std::endl;
			outputLog << "factory: " << device->IDXGIFactory << std::endl;
			outputLog << "Dev: " << device->Device << std::endl;
			outputLog << "DevCon: " << device->DeviceContext << std::endl;
			outputLog << "Swap: " << device->SwapChain->DXGISwapChain << std::endl;
			outputLog << "BackBuffer: " << device->SwapChain->BackBuffer << std::endl;
			outputLog << std::dec << "Device Size: " << device->width << "x" << device->height << " : " << device->newWidth << "x" << device->newHeight << std::endl;
			forceFlush();
		}

		device->SwapChain->BackBuffer->Texture->GetDesc(&BackBufferDesc);
		if (cfg.vLog)
		{
			outputLog << std::dec << "BB Desc: u:" << BackBufferDesc.Usage << " f:" << BackBufferDesc.Format << " w:" << BackBufferDesc.Width << " h:" << BackBufferDesc.Height << std::endl;
			forceFlush();
		}

		screenLayout.SetFromSwapchain(device->SwapChain->DXGISwapChain);
		BackBufferDesc.BindFlags |= D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
		BackBufferDesc.MiscFlags |= D3D11_RESOURCE_MISC_SHARED;

		BackBuffer.textureDesc = BackBufferDesc;
		BackBuffer.creationType = 2;
		BackBuffer.pTexture = device->SwapChain->BackBuffer->Texture;
		if (!BackBuffer.CreateRenderTargetView(device->Device))
		{
			outputLog << "Error creating BackBuffer RenderTargetView" << std::endl;
			outputLog << BackBuffer.GetErrors();
			forceFlush();
			return false;
		}

		if (cfg.vLog)
		{
			outputLog << std::hex << "BackBuffer: " << device->SwapChain->BackBuffer << " : " << device->SwapChain->BackBuffer->Texture << std::endl;
			outputLog << std::dec << "BackBuff: " << device->SwapChain->BackBuffer->Width << " : " << device->SwapChain->BackBuffer->Height << " : " << device->SwapChain->BackBuffer->TextureFormat << " : " << std::hex << device->SwapChain->BackBuffer->Flags << std::endl;
			forceFlush();
		}

		int worldID = 69;
		gameRenderTexture = rtManager->RenderTextureArray1[worldID];

		gameRenderRaw.Texture = gameRenderTexture->Texture;
		gameRenderRaw.ShaderResourceView = gameRenderTexture->ShaderResourceView;
		gameRenderRaw.RenderTargetPtr = gameRenderTexture->RenderTargetPtr;


		//----
		// 8 or 18?
		//----
		int depthID = 8;
		gameDepthTexture = rtManager->RenderTextureArray1[depthID];
		gameDepthTexture->Texture->GetDesc(&DepthBufferDesc);

		gameDepthRaw.Texture = gameDepthTexture->Texture;
		gameDepthRaw.ShaderResourceView = gameDepthTexture->ShaderResourceView;
		gameDepthRaw.RenderTargetPtr = gameDepthTexture->RenderTargetPtr;

		DepthBuffer.creationType = 2;
		DepthBuffer.pTexture = rtManager->RenderTextureArray1[depthID]->Texture;
		DepthBuffer.pDepthStencilView = (*(ID3D11DepthStencilView**)rtManager->RenderTextureArray1[depthID]->RenderTargetPtr);

		DepthBuffer.pTexture->GetDesc(&DepthBufferDesc);
		//DepthBufferDesc.BindFlags |= D3D11_BIND_DEPTH_STENCIL | D3D11_BIND_SHADER_RESOURCE;
		DepthBufferDesc.MiscFlags |= D3D11_RESOURCE_MISC_SHARED;

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
			outputLog << "Starting Second DX11 ..";
			forceFlush();
		}
		if (!devDX11.createDevice(&outputLog))
		{
			outputLog << ".. Error starting second DX11" << std::endl;
			outputLog << devDX11.GetErrors();
			forceFlush();
			return false;
		}
		if (cfg.vLog)
		{
			outputLog << devDX11.GetErrors();
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
			uiRenderTexture[i].uk1 = gameRenderTexture->uk1;
			uiRenderTexture[i].uk5 = 0x90000000 + i;
			uiRenderTexture[i].Notifier = gameRenderTexture->Notifier;
			uiRenderTexture[i].Width = uiRenderTarget[i].textureDesc.Width;
			uiRenderTexture[i].Height = uiRenderTarget[i].textureDesc.Height;
			uiRenderTexture[i].Width1 = uiRenderTarget[i].textureDesc.Width;
			uiRenderTexture[i].Height1 = uiRenderTarget[i].textureDesc.Height;
			uiRenderTexture[i].Width2 = uiRenderTarget[i].textureDesc.Width;
			uiRenderTexture[i].Height2 = uiRenderTarget[i].textureDesc.Height;
			uiRenderTexture[i].Texture = uiRenderTarget[i].pTexture;
			uiRenderTexture[i].ShaderResourceView = uiRenderTarget[i].pShaderResource;
			uiRenderTexture[i].RenderTargetPtr = (unsigned long long)&uiRenderTarget[i].pRenderTarget;
		}
		if (cfg.vLog)
		{
			outputLog << ".. Done" << std::endl;
			forceFlush();
		}

		if (cfg.vLog)
			outputLog << "Creating Viewport ..";
		ZeroMemory(&viewport, sizeof(D3D11_VIEWPORT));
		viewport.TopLeftX = 0.5f;
		viewport.TopLeftY = 0.5f;
		viewport.Width = (float)screenLayout.width;
		viewport.Height = (float)screenLayout.height;
		viewport.MinDepth = 0.0f;
		viewport.MaxDepth = 1.0f;
		if (cfg.vLog)
		{
			outputLog << ".. Done" << std::endl;
		}
		

		setActionHandlesGame(&steamInput);
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
		outputLog << "Releasing Second DX11 ..";
	devDX11.Release();
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

__declspec(dllexport) stTexture* GetUIRenderTexture(int curEye)
{
	return &uiRenderTexture[curEye];
}

__declspec(dllexport) void Recenter()
{
	svr->Recenter();
}

__declspec(dllexport) void UpdateConfiguration(stConfiguration newConfig)
{
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

__declspec(dllexport) uMatrix GetFramePose(poseType posetype, int eye)
{
	return svr->GetFramePose(posetype, eye);
}

__declspec(dllexport) void SetThreadedEye(int eye)
{
	threadedEye = eye;
}

__declspec(dllexport) void RenderVR()
{
	if (enabled && (threadedEye == 1 || cfg.mode2d == true))
	{
		svr->Render(BackBufferCopyShared[0].pTexture, DepthBufferCopyShared[0].pTexture, BackBufferCopyShared[1].pTexture, DepthBufferCopyShared[1].pTexture);
		if (svr->HasErrors())
		{
			outputLog << svr->GetErrors();
			forceFlush();
		}

		matrixSet.hmdMatrix = (XMMATRIX)(svr->GetFramePose(poseType::hmdPosition, -1)._m);
		matrixSet.rhcMatrix = (XMMATRIX)(svr->GetFramePose(poseType::RightHand, -1)._m);

		device->DeviceContext->CopyResource(dalamudBuffer.pTexture, BackBuffer.pTexture);
	}
	LineRender.clear();
}

__declspec(dllexport) void RenderUI(bool enableVR, bool enableFloatingHUD, XMMATRIX curViewMatrixWithoutHMD, POINT virtualMouse, bool dalamudMode)
{
	if (enabled)
	{
		if (enableVR)
		{
			if (enableFloatingHUD)
			{

				matrixSet.gameWorldMatrix = curViewMatrixWithoutHMD;

				if (cfg.motioncontrol)
					rend->RunFrameUpdate(screenLayout, matrixSet.rhcMatrix, poseType::RightHand, dalamudMode);
				else if (cfg.hmdPointing)
					rend->RunFrameUpdate(screenLayout, matrixSet.hmdMatrix, poseType::hmdPosition, dalamudMode);
				else
					rend->RunFrameUpdate(screenLayout, XMMatrixIdentity(), poseType::None, dalamudMode);

				rend->RenderLines(LineRender);
				if (rend->HasErrors())
				{
					outputLog << rend->GetErrors();
					forceFlush();
				}
				int curEyeView = (cfg.swapEyesUI) ? swapEyes[threadedEye] : threadedEye;

				matrixSet.projectionMatrix = (XMMATRIX)(svr->GetFramePose(poseType::Projection, curEyeView)._m);
				matrixSet.eyeMatrix = (cfg.mode2d) ? XMMatrixIdentity() : (XMMATRIX)(svr->GetFramePose(poseType::EyeOffset, curEyeView)._m);

				rend->SetMouseBuffer(screenLayout.hwnd, screenLayout.width, screenLayout.height, virtualMouse.x, virtualMouse.y, dalamudMode);

				//rend->SetClearColor(BackBufferCopy[threadedEye].pRenderTarget, DepthBuffer.pDepthStencilView, new float[4] { 0.f, 0.f, 0.f, 0.f }, false);
				
				//rend->SetBlendIndex(1);
				//rend->DoRender(viewport, BackBufferCopy[threadedEye].pRenderTarget, gameRenderTexture->ShaderResourceView, NULL, &matrixSet, true);
				//rend->SetBlendIndex(2);
				//rend->DoRender(viewport, BackBufferCopy[threadedEye].pRenderTarget, dalamudBuffer.pShaderResource, DepthBuffer.pDepthStencilView, &matrixSet);
				//rend->SetBlendIndex(0);
				//rend->DoRender(viewport, BackBufferCopy[threadedEye].pRenderTarget, uiRenderTarget[0].pShaderResource, DepthBuffer.pDepthStencilView, &matrixSet);
				
				rend->SetBlendIndex(0);
				rend->DoRender(viewport, *(ID3D11RenderTargetView**)gameRenderTexture->RenderTargetPtr, uiRenderTarget[0].pShaderResource, DepthBuffer.pDepthStencilView, &matrixSet);
				rend->SetBlendIndex(2);
				rend->DoRender(viewport, *(ID3D11RenderTargetView**)gameRenderTexture->RenderTargetPtr, dalamudBuffer.pShaderResource, DepthBuffer.pDepthStencilView, &matrixSet);
				
				//device->DeviceContext->CopyResource(BackBufferCopy[threadedEye].pTexture, BackBuffer.pTexture);
				//float clearColor[] = { 0.0f, 1.0f, 0.0f, 1.0f };
				//device->DeviceContext->ClearRenderTargetView(BackBuffer.pRenderTarget, clearColor);
				//device->DeviceContext->CopyResource(BackBuffer.pTexture, BackBufferCopy[0].pTexture);
			}
			else
			{
				rend->DoRender(viewport, *(ID3D11RenderTargetView**)gameRenderTexture->RenderTargetPtr, uiRenderTarget[0].pShaderResource, DepthBuffer.pDepthStencilView, &matrixSet, true);
			}
		}
		else
		{
			if (enableFloatingHUD)
			{
				rend->DoRender(viewport, *(ID3D11RenderTargetView**)gameRenderTexture->RenderTargetPtr, uiRenderTarget[0].pShaderResource, DepthBuffer.pDepthStencilView, &matrixSet);
			}
			else
			{
				rend->DoRender(viewport, *(ID3D11RenderTargetView**)gameRenderTexture->RenderTargetPtr, uiRenderTarget[0].pShaderResource, DepthBuffer.pDepthStencilView, &matrixSet, true);
			}
		}

		//rend->DoRender(viewport, BackBuffer.pRenderTarget, gameRenderTexture->ShaderResourceView, DepthBuffer.pDepthStencilView, &matrixSet);

		device->DeviceContext->ClearRenderTargetView(BackBuffer.pRenderTarget, new float[4] { 0.f, 0.f, 0.f, 1.f });
	}
}

__declspec(dllexport) void RenderFloatingScreen(POINT virtualMouse, bool dalamudMode)
{
	if (enabled)
	{
		XMVECTOR eyePos = { 0, 0, 0 };
		XMVECTOR lookAt = { 0, 0, -1 };
		XMVECTOR viewUp = { 0, 1, 0 };

		matrixSet.gameWorldMatrix = XMMatrixLookAtRH(eyePos, lookAt, viewUp);

		if (cfg.motioncontrol)
			rend->RunFrameUpdate(screenLayout, matrixSet.rhcMatrix, poseType::RightHand, dalamudMode);
		else if (cfg.hmdPointing)
			rend->RunFrameUpdate(screenLayout, matrixSet.hmdMatrix, poseType::hmdPosition, dalamudMode);
		else
			rend->RunFrameUpdate(screenLayout, XMMatrixIdentity(), poseType::None, dalamudMode);

		if (rend->HasErrors())
		{
			outputLog << rend->GetErrors();
			forceFlush();
		}

		rend->SetMouseBuffer(screenLayout.hwnd, screenLayout.width, screenLayout.height, virtualMouse.x, virtualMouse.y, dalamudMode);
		
		if (threadedEye == 0)
			device->DeviceContext->CopyResource(uiRenderTarget[0].pTexture, gameRenderTexture->Texture);
			
		for (int i = 0; i < 2; i++)
		{
			int curEyeView = (cfg.swapEyesUI) ? swapEyes[i] : i;

			matrixSet.projectionMatrix = (XMMATRIX)(svr->GetFramePose(poseType::Projection, curEyeView)._m);
			matrixSet.eyeMatrix = (cfg.mode2d) ? XMMatrixIdentity() : (XMMATRIX)(svr->GetFramePose(poseType::EyeOffset, curEyeView)._m);

			rend->SetClearColor(BackBufferCopy[i].pRenderTarget, DepthBuffer.pDepthStencilView, new float[4] { 0.f, 0.f, 0.f, 0.f }, true);
			
			//rend->SetBlendIndex(1);
			//rend->DoRender(viewport, BackBufferCopy[i].pRenderTarget, gameRenderTexture->ShaderResourceView, NULL, &matrixSet, false);
			//rend->SetBlendIndex(2);
			//rend->DoRender(viewport, BackBufferCopy[i].pRenderTarget, dalamudBuffer.pShaderResource, DepthBuffer.pDepthStencilView, &matrixSet);
			//rend->SetBlendIndex(0);
			//rend->DoRender(viewport, BackBufferCopy[i].pRenderTarget, uiRenderTarget[0].pShaderResource, DepthBuffer.pDepthStencilView, &matrixSet);

			rend->SetBlendIndex(1);
			rend->DoRender(viewport, BackBufferCopy[i].pRenderTarget, uiRenderTarget[0].pShaderResource, DepthBuffer.pDepthStencilView, &matrixSet);
			rend->SetBlendIndex(2);
			rend->DoRender(viewport, BackBufferCopy[i].pRenderTarget, dalamudBuffer.pShaderResource, DepthBuffer.pDepthStencilView, &matrixSet);

			device->DeviceContext->CopyResource(DepthBufferCopy[i].pTexture, DepthBuffer.pTexture);
		}

		threadedEye = 1;
	}
}

__declspec(dllexport) void SetTexture()
{
	if (enabled)
	{
		if (cfg.mode2d)
		{
			device->DeviceContext->CopyResource(BackBufferCopy[0].pTexture, gameRenderTexture->Texture);
			device->DeviceContext->CopyResource(BackBufferCopy[1].pTexture, gameRenderTexture->Texture);
		}
		else
		{
			device->DeviceContext->CopyResource(BackBufferCopy[threadedEye].pTexture, gameRenderTexture->Texture);
			device->DeviceContext->CopyResource(DepthBufferCopy[threadedEye].pTexture, DepthBuffer.pTexture);
		}
	}
}

__declspec(dllexport) POINT GetBufferSize()
{
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

__declspec(dllexport) void UpdateController(UpdateControllerInput controllerCallback)
{
	if (svr->isEnabled())
	{
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
			
			if (vr::VRInput()->GetPoseActionDataForNextFrame(steamInput.game.lefthand, eOrigin, &poseActionData, sizeof(poseActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && poseActionData.bActive == true)
				svr->SetActionPose(poseActionData.pose.mDeviceToAbsoluteTracking, poseType::LeftHand);
			if (vr::VRInput()->GetPoseActionDataForNextFrame(steamInput.game.righthand, eOrigin, &poseActionData, sizeof(poseActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && poseActionData.bActive == true)
				svr->SetActionPose(poseActionData.pose.mDeviceToAbsoluteTracking, poseType::RightHand);

			/*
			if (vr::VRInput()->GetSkeletalActionData(steamInput.game.lefthand_anim, &skeletalActionData, sizeof(skeletalActionData)) == vr::VRInputError_None && skeletalActionData.bActive == true)
			{
				uint32_t boneCount = 0;
				vr::VRInput()->GetBoneCount(steamInput.game.lefthand_anim, &boneCount);
				vr::VRBoneTransform_t* boneArray = new vr::VRBoneTransform_t[boneCount];
				vr::VRInput()->GetSkeletalBoneData(steamInput.game.lefthand_anim, vr::VRSkeletalTransformSpace_Parent, vr::VRSkeletalMotionRange_WithoutController,boneArray, boneCount);
				//for(int i = 0; i < boneCount; i++)
				//	outputLog << boneArray[i].position.v[0] << "," << boneArray[i].position.v[1] << "," << boneArray[i].position.v[2] << "," << boneArray[i].position.v[3] << std::endl;

				byte* compressedBuffer = new byte[100];
				uint32_t compressedSize = 0;
				vr::VRInput()->GetSkeletalBoneDataCompressed(steamInput.game.lefthand_anim, vr::VRSkeletalMotionRange_WithoutController, boneArray, 100, &compressedSize);
				//outputLog << compressedSize << " | " << compressedBuffer << std::endl;
				//forceFlush();
			}
			//controllerCallback(buttonLayout::leftHandAnim, analogActionData, digitalActionData, poseActionData, skeletalActionData);
			*/

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
				controllerCallback(buttonLayout::xbox_right_trigger, analogActionData, digitalActionData);
			if (vr::VRInput()->GetAnalogActionData(steamInput.game.xbox_right_bumper, &analogActionData, sizeof(analogActionData), vr::k_ulInvalidInputValueHandle) == vr::VRInputError_None && analogActionData.bActive == true)
				controllerCallback(buttonLayout::xbox_right_bumper, analogActionData, digitalActionData);
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