#include "framework.h"
#include <algorithm>
#include <iostream>
#include <sstream>

std::stringstream outputLog;
stDevice* device = nullptr;
stRenderTargetManager* rtManager = nullptr;
D3D11_TEXTURE2D_DESC BackBufferDesc;


stScreenLayout screenLayout = stScreenLayout();
stTexture uiRenderTexture[2] = { stTexture(), stTexture() };

stBasicTexture BackBuffer = stBasicTexture();
stBasicTexture BackBufferCopy[2] = { stBasicTexture(), stBasicTexture() };
stBasicTexture BackBufferCopyShared[2] = { stBasicTexture(), stBasicTexture() };
stBasicTexture uiRenderTarget[2] = { stBasicTexture(), stBasicTexture() };

inputController steamInput = {};

stDX11 devDX11;

D3D11_VIEWPORT viewport;
bool enabled = false;
int threadedEye = 0;
bool logging = false;
int swapEyes[] = { 1, 0 };

Configuration cfg = Configuration();
simpleVR svr = simpleVR(&cfg);
BasicRenderer* rend = new BasicRenderer(&cfg);

void InitInstance(HANDLE);
void ExitInstance();
void forceFlush();
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
	__declspec(dllexport) void UpdateConfiguration(Configuration newConfig);
	__declspec(dllexport) void SetFramePose();
	__declspec(dllexport) void WaitGetPoses();
	__declspec(dllexport) uMatrix GetFramePose(poseType posetype, int eye);
	__declspec(dllexport) void SetThreadedEye(int eye);
	__declspec(dllexport) void RenderVR();
	__declspec(dllexport) void RenderUI(bool enableVR, bool enableFloatingHUD);
	__declspec(dllexport) void RenderFloatingScreen();
	__declspec(dllexport) void SetTexture();
	__declspec(dllexport) POINT GetBufferSize();
	__declspec(dllexport) void ResizeWindow(HWND hwnd, int width, int height);

	__declspec(dllexport) bool SetActiveJSON(const char*, int size);
	__declspec(dllexport) void UpdateController(UpdateControllerInput controllerCallback);

	__declspec(dllexport) void SetLogFunction(InternalLogging internalLogging);
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
		BackBufferCopy[i].textureDesc = BackBufferDesc;
		if (!BackBufferCopy[i].Create(device->Device, true, true))
		{
			outputLog << BackBufferCopy[i].GetErrors();
			retVal = false;
		}

		if (cfg.vLog)
		{
			if (BackBufferCopy[i].pTexture == nullptr)
				outputLog << "Error creating backbufferCopy " << i << std::endl;
			if (BackBufferCopy[i].pRenderTarget == nullptr)
				outputLog << "Error creating backbufferCopyRTV " << i << std::endl;
			if (BackBufferCopy[i].pShaderResource == nullptr)
				outputLog << "Error creating backbufferCopySRV " << i << std::endl;
			if (BackBufferCopy[i].pSharedHandle == nullptr)
				outputLog << "Error creating shared handle " << i << std::endl;
		}

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

		if (cfg.vLog)
		{
			if (BackBufferCopyShared[i].pTexture == nullptr)
				outputLog << "Error creating BackBufferCopyShared " << i << std::endl;
			if (BackBufferCopyShared[i].pRenderTarget == nullptr)
				outputLog << "Error creating BackBufferCopySharedRTV " << i << std::endl;
			if (BackBufferCopyShared[i].pShaderResource == nullptr)
				outputLog << "Error creating BackBufferCopySharedSRV " << i << std::endl;
		}

		//----
		// Create the ui render target based on the backbuffer description
		//----
		uiRenderTarget[i].textureDesc = BackBufferDesc;
		uiRenderTarget[i].textureDesc.Width = BackBufferDesc.Width;
		uiRenderTarget[i].textureDesc.Height = BackBufferDesc.Height;
		if (!uiRenderTarget[i].Create(device->Device, true, true))
		{
			outputLog << uiRenderTarget[i].GetErrors();
			retVal = false;
		}

		if (cfg.vLog)
		{
			if (uiRenderTarget[i].pTexture == nullptr)
				outputLog << "Error creating RT" << std::endl;
			if (uiRenderTarget[i].pRenderTarget == nullptr)
				outputLog << "Error creating RT RTV" << std::endl;
			if (uiRenderTarget[i].pShaderResource == nullptr)
				outputLog << "Error creating RT SRV" << std::endl;
			if (uiRenderTarget[i].pSharedHandle == nullptr)
				outputLog << "Error creating RT shared handle" << std::endl;
		}
	}
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
	}
}

__declspec(dllexport) bool SetDX11(unsigned long long struct_device, unsigned long long rtm)
{
	if(cfg.vLog)
		outputLog << std::endl << "SetDX11:" << std::endl;
	if (device == nullptr && enabled == false)
	{
		device = (stDevice*)struct_device;
		rtManager = (stRenderTargetManager*)rtm;
		if (cfg.vLog)
		{
			outputLog << std::hex << "Device:" << struct_device << std::endl;
			outputLog << "factory: " << device->IDXGIFactory << std::endl;
			outputLog << "Dev: " << device->Device << std::endl;
			outputLog << "DevCon: " << device->DeviceContext << std::endl;
			outputLog << "Swap: " << device->SwapChain->DXGISwapChain << std::endl;
			outputLog << "BackBuffer: " << device->SwapChain->BackBuffer << std::endl;
			outputLog << std::dec << "Device Size: " << device->width << "x" << device->height << " : " << device->newWidth << "x" << device->newHeight << std::endl;
		}

		device->SwapChain->BackBuffer->Texture->GetDesc(&BackBufferDesc);
		if (cfg.vLog)
		{
			outputLog << std::dec << "BB Desc: u:" << BackBufferDesc.Usage << " f:" << BackBufferDesc.Format << " w:" << BackBufferDesc.Width << " h:" << BackBufferDesc.Height << std::endl;
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
		}

		if (cfg.vLog)
			outputLog << "Starting VR ..";
		if (!svr.StartVR())
		{
			outputLog << ".. Error starting VR";
			forceFlush();
			return false;
		}
		if (cfg.vLog)
		{
			outputLog << ".. Done" << std::endl;
		}

		if (cfg.vLog)
			outputLog << "Starting Renderer ..";
		if (!rend->SetDevice(device->Device, device->DeviceContext))
		{
			outputLog << ".. Error starting Renderer";
			forceFlush();
			return false;
		}
		if (cfg.vLog)
		{
			outputLog << ".. Done" << std::endl;
		}

		if (cfg.vLog)
			outputLog << "Starting Second DX11 ..";
		if (!devDX11.createDevice())
		{
			outputLog << ".. Error starting second DX11" << std::endl;
			outputLog << devDX11.GetErrors();
			forceFlush();
			return false;
		}
		if (cfg.vLog)
		{
			outputLog << ".. Done" << std::endl;
		}

		if (cfg.vLog)
			outputLog << "Creating BackBufferClone ..";
		if (!CreateBackbufferClone())
		{
			outputLog << ".. Error creating BackBufferClone" << std::endl;
			return false;
		}
		if (cfg.vLog)
		{
			outputLog << ".. Done" << std::endl;
		}

		if (cfg.vLog)
			outputLog << "Creating Textures ..";
		uiRenderTexture[0].uk1 = device->SwapChain->BackBuffer->uk1;
		uiRenderTexture[0].uk5 = 0x990F0F0;
		uiRenderTexture[0].Notifier = device->SwapChain->BackBuffer->Notifier;
		uiRenderTexture[0].Width = uiRenderTarget[0].textureDesc.Width;
		uiRenderTexture[0].Height = uiRenderTarget[0].textureDesc.Height;
		uiRenderTexture[0].Width1 = uiRenderTarget[0].textureDesc.Width;
		uiRenderTexture[0].Height1 = uiRenderTarget[0].textureDesc.Height;
		uiRenderTexture[0].Texture = uiRenderTarget[0].pTexture;
		uiRenderTexture[0].ShaderResourceView = uiRenderTarget[0].pShaderResource;
		uiRenderTexture[0].RenderTargetPtr = (unsigned long long)&uiRenderTarget[0].pRenderTarget;

		uiRenderTexture[1].uk1 = device->SwapChain->BackBuffer->uk1;
		uiRenderTexture[1].uk5 = 0x990F0F0F;
		uiRenderTexture[1].Notifier = device->SwapChain->BackBuffer->Notifier;
		uiRenderTexture[1].Width = uiRenderTarget[1].textureDesc.Width;
		uiRenderTexture[1].Height = uiRenderTarget[1].textureDesc.Height;
		uiRenderTexture[1].Width1 = uiRenderTarget[1].textureDesc.Width;
		uiRenderTexture[1].Height1 = uiRenderTarget[1].textureDesc.Height;
		uiRenderTexture[1].Texture = uiRenderTarget[1].pTexture;
		uiRenderTexture[1].ShaderResourceView = uiRenderTarget[1].pShaderResource;
		uiRenderTexture[1].RenderTargetPtr = (unsigned long long)&uiRenderTarget[1].pRenderTarget;
		if (cfg.vLog)
		{
			outputLog << ".. Done" << std::endl;
		}

		if (cfg.vLog)
			outputLog << "Creating Viewport ..";
		ZeroMemory(&viewport, sizeof(D3D11_VIEWPORT));
		viewport.TopLeftX = 0;
		viewport.TopLeftY = 0;
		viewport.Width = BackBuffer.textureDesc.Width;
		viewport.Height = BackBuffer.textureDesc.Height;
		viewport.MinDepth = 0.0f;
		viewport.MaxDepth = 0.0f;
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
	svr.StopVR();
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
	svr.Recenter();
}

__declspec(dllexport) void UpdateConfiguration(Configuration newConfig)
{
	cfg = newConfig;
	if (svr.isEnabled())
		svr.MakeIPDOffset();
}

__declspec(dllexport) void SetFramePose()
{
	svr.SetFramePose();
}

__declspec(dllexport) void WaitGetPoses()
{
	svr.WaitGetPoses();
}

__declspec(dllexport) uMatrix GetFramePose(poseType posetype, int eye)
{
	return svr.GetFramePose(posetype, eye);
}

__declspec(dllexport) void SetThreadedEye(int eye)
{
	threadedEye = eye;
}

__declspec(dllexport) void RenderVR()
{
	if(enabled && threadedEye == 1)
		svr.Render(BackBufferCopyShared[0].pTexture, BackBufferCopyShared[1].pTexture);
}

__declspec(dllexport) void RenderUI(bool enableVR, bool enableFloatingHUD)
{
	if (enabled)
	{
		DirectX::XMMATRIX projectionMatrix;
		DirectX::XMMATRIX viewMatrix;

		int curEyeView = 0;
		if (cfg.swapEyesUI)
			curEyeView = swapEyes[threadedEye];
		else
			curEyeView = threadedEye;

		if (enableVR)
		{
			projectionMatrix = (DirectX::XMMATRIX)(svr.GetFramePose(poseType::Projection, curEyeView)._m);
			viewMatrix = (DirectX::XMMATRIX)(svr.GetFramePose(poseType::EyeOffset, curEyeView)._m) * (DirectX::XMMATRIX)(svr.GetFramePose(poseType::hmdPosition, curEyeView)._m);
			viewMatrix = DirectX::XMMatrixTranspose(viewMatrix);
			viewMatrix = DirectX::XMMatrixInverse(0, viewMatrix);

			if (enableFloatingHUD)
			{
				rend->SetMousePosition(screenLayout.hwnd, screenLayout.width, screenLayout.height);
				//rend->SetBlendIndex(1);
				rend->DoRender(viewport, BackBuffer.pRenderTarget, uiRenderTarget[0].pShaderResource, projectionMatrix, viewMatrix);
			}
			else
			{
				rend->DoRender(viewport, BackBuffer.pRenderTarget, uiRenderTarget[0].pShaderResource, projectionMatrix, viewMatrix, true);

				//SetTexture();

				//rend->SetClearColor(new float[4]{ 0, 0, 0, 0 });
				//rend->DoRender(viewport, BackBuffer.pRenderTarget, BackBufferCopy[threadedEye].pShaderResource, projectionMatrix, viewMatrix);
			}
		}
		else
		{
			if (enableFloatingHUD)
			{
				uMatrix tProj = svr.GetFramePose(poseType::Projection, curEyeView);
				tProj._m[2] = 0.0f;
				projectionMatrix = (DirectX::XMMATRIX)(tProj._m);
				viewMatrix = DirectX::XMMatrixIdentity();

				rend->SetMousePosition(screenLayout.hwnd, screenLayout.width, screenLayout.height);
				rend->DoRender(viewport, BackBuffer.pRenderTarget, uiRenderTarget[0].pShaderResource, projectionMatrix, viewMatrix);
			}
			else
			{
				projectionMatrix = DirectX::XMMatrixIdentity();
				viewMatrix = DirectX::XMMatrixIdentity();

				rend->SetMousePosition(screenLayout.hwnd, screenLayout.width, screenLayout.height);
				rend->DoRender(viewport, BackBuffer.pRenderTarget, uiRenderTarget[0].pShaderResource, projectionMatrix, viewMatrix, true);
			}
		}

		//rend->DoRender(viewport, BackBufferCopy[threadedEye].pRenderTarget, uiRenderTarget.pShaderResource, projectionMatrix, viewMatrix);
		//rend->DoRender(viewport, BackBuffer.pRenderTarget, uiRenderTarget.pShaderResource, projectionMatrix, viewMatrix);
	}
}

__declspec(dllexport) void RenderFloatingScreen()
{
	if (enabled)
	{
		ID3D11Resource* bbResource = nullptr;
		ID3D11Resource* bbcResource = nullptr;
		BackBuffer.pRenderTarget->GetResource(&bbResource);
		uiRenderTarget[threadedEye].pRenderTarget->GetResource(&bbcResource);
		device->DeviceContext->CopyResource(bbcResource, bbResource);
		if (bbcResource) { bbcResource->Release(); bbcResource = nullptr; }
		if (bbResource) { bbResource->Release(); bbResource = nullptr; }

		DirectX::XMMATRIX projectionMatrix = (DirectX::XMMATRIX)(svr.GetFramePose(poseType::Projection, threadedEye)._m);
		DirectX::XMMATRIX viewMatrix = (DirectX::XMMATRIX)(svr.GetFramePose(poseType::EyeOffset, threadedEye)._m) * (DirectX::XMMATRIX)(svr.GetFramePose(poseType::hmdPosition, threadedEye)._m);
		viewMatrix = DirectX::XMMatrixTranspose(viewMatrix);
		viewMatrix = DirectX::XMMatrixInverse(0, viewMatrix);

		int offEye[] = { 1, 0 };
		int eyeT = offEye[threadedEye];

		rend->SetClearColor(new float[4]{ 0.f, 0.f, 0.f, 0.f });
		rend->SetBlendIndex(1);
		//rend->DoRender(viewport, BackBufferCopy[threadedEye].pRenderTarget, uiGradiant.pShaderResource, projectionMatrix, viewMatrix);
		rend->DoRender(viewport, BackBufferCopy[threadedEye].pRenderTarget, uiRenderTarget[0].pShaderResource, projectionMatrix, viewMatrix);
	}
}


__declspec(dllexport) void SetTexture()
{
	if (enabled)
	{
		ID3D11Resource* bbResource = nullptr;
		ID3D11Resource* bbcResource = nullptr;
		BackBuffer.pRenderTarget->GetResource(&bbResource);
		BackBufferCopy[threadedEye].pRenderTarget->GetResource(&bbcResource);
		device->DeviceContext->CopyResource(bbcResource, bbResource);
		if (bbcResource) { bbcResource->Release(); bbcResource = nullptr; }
		if (bbResource) { bbResource->Release(); bbResource = nullptr; }
	}
}

__declspec(dllexport) POINT GetBufferSize()
{
	return svr.GetBufferSize();
}

__declspec(dllexport) void ResizeWindow(HWND hwnd, int width, int height)
{
	RECT clientRect = RECT();
	clientRect.top = 0;
	clientRect.left = 0;
	clientRect.bottom = height;
	clientRect.right = width;

	if (hwnd != 0)
	{
		int width = clientRect.right - clientRect.left;
		int height = clientRect.bottom - clientRect.top;

		AdjustWindowRect(&clientRect, GetWindowLongA(hwnd, GWL_STYLE), false);
		SetWindowPos(hwnd, 0, 0, 0, width, height, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOZORDER | SWP_FRAMECHANGED);
		SendMessageA(hwnd, WM_EXITSIZEMOVE, WPARAM(0), LPARAM(0));
	}
}

__declspec(dllexport) bool SetActiveJSON(const char* filePath, int size)
{
	if (svr.isEnabled())
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
	if (svr.isEnabled())
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
			vr::ETrackingUniverseOrigin eOrigin = vr::TrackingUniverseSeated;

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

__declspec(dllexport) void SetLogFunction(InternalLogging internalLogging)
{
	PluginLog = internalLogging;
}