#include "framework.h"

stDevice* device = nullptr;

D3D11_TEXTURE2D_DESC BackBufferDesc;

BasicRenderer* rend = new BasicRenderer();
stScreenLayout screenLayout = stScreenLayout();
stTexture uiRenderTexture[2] = { stTexture(), stTexture() };

stBasicTexture BackBuffer = stBasicTexture();
stBasicTexture BackBufferCopy[2] = { stBasicTexture(), stBasicTexture() };
stBasicTexture BackBufferCopyShared[2] = { stBasicTexture(), stBasicTexture() };
stBasicTexture uiRenderTarget = stBasicTexture();

inputController steamInput = {};

stDX11 devDX11;

D3D11_VIEWPORT viewport;
bool enabled = false;
int threadedEye = 0;
bool logging = false;
bool doSwapEye = false;
int swapEyes[] = { 1, 0 };

#include <iostream>
#include <fstream>
std::ofstream myfile;

simpleVR svr = simpleVR();

void InitInstance(HANDLE);
void ExitInstance();
void forceFlush();
void CreateBackbufferClone();
void DestroyBackbufferClone();

enum buttonLayout
{
	movement,
	rotation,
	leftClick,
	rightClick,
	recenter,
	shift,
	alt,
	control,
	escape,
	button01,
	button02,
	button03,
	button04,
	button05,
	button06,
	button07,
	button08,
	button09,
	button10,
	button11,
	button12,
	xbox_button_y,
	xbox_button_x,
	xbox_button_a,
	xbox_button_b,
	xbox_left_trigger,
	xbox_left_bumper,
	xbox_left_stick_click,
	xbox_right_trigger,
	xbox_right_bumper,
	xbox_right_stick_click,
	xbox_pad_up,
	xbox_pad_down,
	xbox_pad_left,
	xbox_pad_right,
	xbox_start,
	xbox_select
};


typedef void(__stdcall* UpdateControllerInput)(buttonLayout buttonId, vr::InputAnalogActionData_t analog, vr::InputDigitalActionData_t digital);

extern "C"
{
	__declspec(dllexport) void SetDX11(unsigned long long struct_deivce);
	__declspec(dllexport) void UnsetDX11();
	__declspec(dllexport) stTexture* GetUIRenderTexture(int curEye);
	__declspec(dllexport) void Recenter();
	__declspec(dllexport) void SetFramePose();
	__declspec(dllexport) uMatrix GetFramePose(poseType posetype, int eye);
	__declspec(dllexport) void SetThreadedEye(int eye);
	__declspec(dllexport) void RenderVR();
	__declspec(dllexport) void RenderUI(bool enableVR, bool enableFloatingHUD);
	__declspec(dllexport) void RenderFloatingScreen();
	__declspec(dllexport) void SetTexture();
	__declspec(dllexport) void UpdateZScale(float z, float scale);
	__declspec(dllexport) void SwapEyesUI(bool swapEyesUI);

	__declspec(dllexport) void UpdateController(UpdateControllerInput controllerCallback);
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

void OpenLogFile()
{
	if(logging)
		myfile.open("d:\\Projects\\output.txt", std::ios::out | std::ios::app);
}

void CloseLogFile()
{
	if (myfile.is_open())
		myfile.close();
}

void InitInstance(HANDLE hModule)
{
	OpenLogFile();
}

void ExitInstance()
{
	CloseLogFile();
}

void forceFlush()
{
	CloseLogFile();
	OpenLogFile();
}

void CreateBackbufferClone()
{
	HRESULT result = S_OK;
	DestroyBackbufferClone();

	for (int i = 0; i < 2; i++)
	{
		//----
		// Create the backbuffer copy based on the backbuffer description
		//----
		BackBufferCopy[i].textureDesc = BackBufferDesc;
		BackBufferCopy[i].Create(device->Device, true, true);

		if (myfile.is_open())
		{
			if (BackBufferCopy[i].pTexture == nullptr)
				myfile << "Error creating backbufferCopy " << i << std::endl;
			if (BackBufferCopy[i].pRenderTarget == nullptr)
				myfile << "Error creating backbufferCopyRTV " << i << std::endl;
			if (BackBufferCopy[i].pShaderResource == nullptr)
				myfile << "Error creating backbufferCopySRV " << i << std::endl;
			if (BackBufferCopy[i].pSharedHandle == nullptr)
				myfile << "Error creating shared handle " << i << std::endl;
		}

		//----
		// Set the shared handle and create a shared texture based off of it
		//----
		BackBufferCopyShared[i].textureDesc = BackBufferCopy[i].textureDesc;
		BackBufferCopyShared[i].pSharedHandle = BackBufferCopy[i].pSharedHandle;
		BackBufferCopyShared[i].Create(devDX11.dev, true, true);

		if (myfile.is_open())
		{
			if (BackBufferCopyShared[i].pTexture == nullptr)
				myfile << "Error creating BackBufferCopyShared " << i << std::endl;
			if (BackBufferCopyShared[i].pRenderTarget == nullptr)
				myfile << "Error creating BackBufferCopySharedRTV " << i << std::endl;
			if (BackBufferCopyShared[i].pShaderResource == nullptr)
				myfile << "Error creating BackBufferCopySharedSRV " << i << std::endl;
		}
	}

	//----
	// Create the ui render target based on the backbuffer description
	//----
	uiRenderTarget.textureDesc = BackBufferDesc;
	uiRenderTarget.textureDesc.Width = BackBufferDesc.Width;
	uiRenderTarget.textureDesc.Height = BackBufferDesc.Height;
	uiRenderTarget.Create(device->Device, true, true);

	if (myfile.is_open())
	{
		if (uiRenderTarget.pTexture == nullptr)
			myfile << "Error creating RT" << std::endl;
		if (uiRenderTarget.pRenderTarget == nullptr)
			myfile << "Error creating RT RTV" << std::endl;
		if (uiRenderTarget.pShaderResource == nullptr)
			myfile << "Error creating RT SRV" << std::endl;
		if (uiRenderTarget.pSharedHandle == nullptr)
			myfile << "Error creating RT shared handle" << std::endl;
	}

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
	}
	uiRenderTarget.Release();
}
#include <algorithm>
__declspec(dllexport) void SetDX11(unsigned long long struct_device)
{
	myfile << "SetDX11:\n";
	if (device == nullptr)
	{
		device = (stDevice*)struct_device;
		if (myfile.is_open())
		{
			myfile << "Device:\n";
			myfile << std::hex << struct_device << "\n";
			myfile << "factory: " << device->IDXGIFactory << std::endl;
			myfile << "Dev: " << device->Device << std::endl;
			myfile << "DevCon: " << device->DeviceContext << std::endl;
			myfile << "Swap: " << device->SwapChain->DXGISwapChain << std::endl;
			myfile << "BackBuffer: " << device->SwapChain->BackBuffer << std::endl;
		}
		
		device->SwapChain->BackBuffer->Texture->GetDesc(&BackBufferDesc);
		if (myfile.is_open())
			myfile << "BB Desc: u:" << std::dec << BackBufferDesc.Usage << " f:" << BackBufferDesc.Format << " w:" << BackBufferDesc.Width << " h:" << BackBufferDesc.Height << std::endl;
		forceFlush();

		screenLayout.SetFromSwapchain(device->SwapChain->DXGISwapChain);
		BackBufferDesc.BindFlags |= D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
		BackBufferDesc.MiscFlags |= D3D11_RESOURCE_MISC_SHARED;

		BackBuffer.textureDesc = BackBufferDesc;
		BackBuffer.creationType = 2;
		BackBuffer.pTexture = device->SwapChain->BackBuffer->Texture;
		BackBuffer.CreateRenderTargetView(device->Device);
		
		if (myfile.is_open())
		{
			myfile << std::hex;
			myfile << "BackBuffer: " << device->SwapChain->BackBuffer << " : " << device->SwapChain->BackBuffer->Texture << std::endl;
			myfile << "BackBuff: " << std::dec << device->SwapChain->BackBuffer->Width << " : " << device->SwapChain->BackBuffer->Height << " : " << device->SwapChain->BackBuffer->TextureFormat << " : " << std::hex << device->SwapChain->BackBuffer->Flags << std::endl;
		}
		
		forceFlush();

		if (myfile.is_open())
			myfile << "Starting VR .." << std::endl;
		svr.StartVR();
		if (myfile.is_open())
			myfile << ".. Done" << std::endl;

		if (myfile.is_open())
			myfile << "Starting Renderer ..";
		rend->SetDevice(device->Device, device->DeviceContext);
		if (myfile.is_open())
			myfile << ".. Done" << std::endl;

		if (myfile.is_open())
			myfile << "Starting Second DX11 ..";
		devDX11.createDevice();
		if (myfile.is_open())
			myfile << ".. Done" << std::endl;

		if (myfile.is_open())
			myfile << "Creating BackBufferClone ..";
		CreateBackbufferClone();
		if (myfile.is_open())
			myfile << ".. Done" << std::endl;
		
		forceFlush();

		if (myfile.is_open())
			myfile << "Creating Textures ..";
		uiRenderTexture[0].uk1 = device->SwapChain->BackBuffer->uk1;
		uiRenderTexture[0].uk5 = 0x990F0F0;
		uiRenderTexture[0].Notifier = device->SwapChain->BackBuffer->Notifier;
		uiRenderTexture[0].Width = uiRenderTarget.textureDesc.Width;
		uiRenderTexture[0].Height = uiRenderTarget.textureDesc.Height;
		uiRenderTexture[0].Width1 = uiRenderTarget.textureDesc.Width;
		uiRenderTexture[0].Height1 = uiRenderTarget.textureDesc.Height;
		uiRenderTexture[0].Texture = uiRenderTarget.pTexture;
		uiRenderTexture[0].ShaderResourceView = uiRenderTarget.pShaderResource;
		uiRenderTexture[0].RenderTargetPtr = (unsigned long long)&uiRenderTarget.pRenderTarget;

		uiRenderTexture[1].uk1 = device->SwapChain->BackBuffer->uk1;
		uiRenderTexture[1].uk5 = 0x990F0F0F;
		uiRenderTexture[1].Notifier = device->SwapChain->BackBuffer->Notifier;
		uiRenderTexture[1].Width = uiRenderTarget.textureDesc.Width;
		uiRenderTexture[1].Height = uiRenderTarget.textureDesc.Height;
		uiRenderTexture[1].Width1 = uiRenderTarget.textureDesc.Width;
		uiRenderTexture[1].Height1 = uiRenderTarget.textureDesc.Height;
		uiRenderTexture[1].Texture = uiRenderTarget.pTexture;
		uiRenderTexture[1].ShaderResourceView = uiRenderTarget.pShaderResource;
		uiRenderTexture[1].RenderTargetPtr = (unsigned long long)&uiRenderTarget.pRenderTarget;
		if (myfile.is_open())
			myfile << ".. Done" << std::endl;
		forceFlush();

		if (myfile.is_open())
			myfile << "Creating Viewport ..";
		ZeroMemory(&viewport, sizeof(D3D11_VIEWPORT));
		viewport.TopLeftX = 0;
		viewport.TopLeftY = 0;
		viewport.Width = BackBuffer.textureDesc.Width;
		viewport.Height = BackBuffer.textureDesc.Height;
		viewport.MinDepth = 0.0f;
		viewport.MaxDepth = 0.0f;
		if (myfile.is_open())
			myfile << ".. Done" << std::endl;

		enabled = true;
	}
	if (myfile.is_open())
		myfile << "SetDX11 .. Done:\n";


	setActionHandlesGame(&steamInput);
}



__declspec(dllexport) void UnsetDX11()
{
	enabled = false;
	if (myfile.is_open())
		myfile << "### StopDX11" << std::endl;
	
	if (myfile.is_open())
		myfile << "Destroying BackBufferClone ..";
	DestroyBackbufferClone();
	if (myfile.is_open())
		myfile << ".. Done" << std::endl;

	if (myfile.is_open())
		myfile << "Releasing Renderer ..";
	rend->Release();
	if (myfile.is_open())
		myfile << ".. Done" << std::endl;

	if (myfile.is_open())
		myfile << "Releasing Second DX11 ..";
	devDX11.Release();
	if (myfile.is_open())
		myfile << ".. Done" << std::endl;

	if (myfile.is_open())
		myfile << "Stopping VR ..";
	svr.StopVR();
	if (myfile.is_open())
		myfile << ".. Done" << std::endl;

	device = nullptr;
	if (myfile.is_open())
		myfile << "StopDX11 ###" << std::endl;
	forceFlush();

}

__declspec(dllexport) stTexture* GetUIRenderTexture(int curEye)
{
	return &uiRenderTexture[curEye];
}

__declspec(dllexport) void Recenter()
{
	svr.Recenter();
}

__declspec(dllexport) void SetFramePose()
{
	svr.SetFramePose();
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
		if (doSwapEye)
			curEyeView = swapEyes[threadedEye];
		else
			curEyeView = threadedEye;


		//myfile << "Eye" << threadedEye << " : " << curEyeView << " : " << swapEyes[threadedEye] << std::endl;

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
				rend->DoRender(viewport, BackBuffer.pRenderTarget, uiRenderTarget.pShaderResource, projectionMatrix, viewMatrix);
			}
			else
			{
				rend->DoRender(viewport, BackBuffer.pRenderTarget, uiRenderTarget.pShaderResource, projectionMatrix, viewMatrix, true);

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
				rend->DoRender(viewport, BackBuffer.pRenderTarget, uiRenderTarget.pShaderResource, projectionMatrix, viewMatrix);
			}
			else
			{
				projectionMatrix = DirectX::XMMatrixIdentity();
				viewMatrix = DirectX::XMMatrixIdentity();

				rend->SetMousePosition(screenLayout.hwnd, screenLayout.width, screenLayout.height);
				rend->DoRender(viewport, BackBuffer.pRenderTarget, uiRenderTarget.pShaderResource, projectionMatrix, viewMatrix, true);
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
		uiRenderTarget.pRenderTarget->GetResource(&bbcResource);
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
		rend->DoRender(viewport, BackBufferCopy[threadedEye].pRenderTarget, uiRenderTarget.pShaderResource, projectionMatrix, viewMatrix);
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

__declspec(dllexport) void UpdateZScale(float z, float scale)
{
	rend->UpdateZScale(z, scale);
}

__declspec(dllexport) void SwapEyesUI(bool swapEyesUI)
{
	doSwapEye = swapEyesUI;
}

__declspec(dllexport) void UpdateController(UpdateControllerInput controllerCallback)
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






__declspec(dllexport) bool hmdGetButtonHasChanged(ButtonList buttonID, ControllerType controllerType)
{
	return svr.GetButtonHasChanged(buttonID, controllerType);
}

__declspec(dllexport) bool hmdGetButtonIsTouched(ButtonList buttonID, ControllerType controllerType)
{
	return svr.GetButtonIsTouched(buttonID, controllerType);
}

__declspec(dllexport) bool hmdGetButtonIsPressed(ButtonList buttonID, ControllerType controllerType)
{
	return svr.GetButtonIsPressed(buttonID, controllerType);
}

__declspec(dllexport) bool hmdGetButtonIsDownFrame(ButtonList buttonID, ControllerType controllerType)
{
	return svr.GetButtonIsDownFrame(buttonID, controllerType);
}

__declspec(dllexport) bool hmdGetButtonIsUpFrame(ButtonList buttonID, ControllerType controllerType)
{
	return svr.GetButtonIsUpFrame(buttonID, controllerType);
}

__declspec(dllexport) float hmdGetButtonValue(ButtonList buttonID, ControllerType controllerType)
{
	return svr.GetButtonValue(buttonID, controllerType);
}
