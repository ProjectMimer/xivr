#include "framework.h"
#include "ffxivDevice.h"

#include "BasicRenderer.h"
#include "simpleVR.h"
#include "stDX11.h"

stDevice* device = nullptr;

D3D11_TEXTURE2D_DESC BackBufferDesc;

BasicRenderer* rend = new BasicRenderer();
stScreenLayout screenLayout = stScreenLayout();
stTexture uiRenderTexture[2] = { stTexture(), stTexture() };

stBasicTexture BackBuffer = stBasicTexture();
stBasicTexture BackBufferCopy[2] = { stBasicTexture(), stBasicTexture() };
stBasicTexture BackBufferCopyShared[2] = { stBasicTexture(), stBasicTexture() };
stBasicTexture uiRenderTarget = stBasicTexture();

stDX11 devDX11;

D3D11_VIEWPORT viewport;
bool enabled = false;
int threadedEye = 0;
bool logging = false;

#include <iostream>
#include <fstream>
std::ofstream myfile;

simpleVR svr = simpleVR();

void InitInstance(HANDLE);
void ExitInstance();
void forceFlush();
void CreateBackbufferClone();
void DestroyBackbufferClone();

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
		if (uiRenderTarget.pSharedHandle == nullptr)
			myfile << "Error creating RT shared handle" << std::endl;
	}
}

void DestroyBackbufferClone()
{
	for (int i = 0; i < 2; i++)
	{
		//----
		// Close the shared backbuffer copy
		// and the backbuffer copy
		//----
		BackBufferCopyShared[i].Release();
		BackBufferCopy[i].Release();
	}
	//----
	// Close the ui render target
	//----
	uiRenderTarget.Release();
}

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

		if (enableVR)
		{
			projectionMatrix = (DirectX::XMMATRIX)(svr.GetFramePose(poseType::Projection, threadedEye)._m);
			viewMatrix = (DirectX::XMMATRIX)(svr.GetFramePose(poseType::EyeOffset, threadedEye)._m) * (DirectX::XMMATRIX)(svr.GetFramePose(poseType::hmdPosition, threadedEye)._m);
			viewMatrix = DirectX::XMMatrixTranspose(viewMatrix);
			viewMatrix = DirectX::XMMatrixInverse(0, viewMatrix);

			if (enableFloatingHUD)
			{
				rend->SetMousePosition(screenLayout.hwnd, screenLayout.width, screenLayout.height);
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
				uMatrix tProj = svr.GetFramePose(poseType::Projection, threadedEye);
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