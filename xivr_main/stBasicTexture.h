#pragma once
#include <d3d11_4.h>
#include <sstream>

struct stBasicTexture
{
	ID3D11Texture2D* pTexture;
	ID3D11RenderTargetView* pRenderTarget;
	ID3D11ShaderResourceView* pShaderResource;
	HANDLE pSharedHandle;
	int creationType;
	std::stringstream logError;

	D3D11_TEXTURE2D_DESC textureDesc;

	stBasicTexture()
	{
		pTexture = nullptr;
		pRenderTarget = nullptr;
		pShaderResource = nullptr;
		pSharedHandle = nullptr;
		creationType = 0;
		logError.str("");

		//----
		// Creates a generic texture desc
		//----
		ZeroMemory(&textureDesc, sizeof(D3D11_TEXTURE2D_DESC));
		textureDesc.MipLevels = 1;
		textureDesc.ArraySize = 1;
		textureDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
		textureDesc.SampleDesc.Count = 1;
		textureDesc.SampleDesc.Quality = 0;
		textureDesc.Usage = D3D11_USAGE_DEFAULT;
		textureDesc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
		textureDesc.CPUAccessFlags = 0;
		textureDesc.MiscFlags = D3D11_RESOURCE_MISC_SHARED;
	}

	void SetWidthHeight(int tWidth, int tHeight)
	{
		textureDesc.Width = tWidth;
		textureDesc.Height = tHeight;
	}

	bool Create(ID3D11Device* dev, bool rtv, bool srv)
	{
		bool retVal = true;
		if (pSharedHandle)
			retVal = CreateShared(dev, rtv, srv);
		else
			retVal = CreateNew(dev, rtv, srv);
		return retVal;
	}

	bool CreateNew(ID3D11Device* dev, bool rtv, bool srv)
	{
		bool retVal = true;
		HRESULT result = dev->CreateTexture2D(&textureDesc, NULL, &pTexture);
		if (FAILED(result)) {
			logError << "Failed to create new Texture2D" << std::endl;
			retVal = false;
		}
		creationType = 1;

		GetSharedHandle();

		if (retVal && rtv)
			retVal = CreateRenderTargetView(dev);
		if (retVal && srv)
			retVal = CreateShaderResourceView(dev);
		return retVal;
	}

	bool CreateShared(ID3D11Device* dev, bool rtv, bool srv)
	{
		bool retVal = true;
		HRESULT result = dev->OpenSharedResource(pSharedHandle, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&pTexture));
		if (FAILED(result)) {
			logError << "Failed to create shared Texture2D" << std::endl;
			retVal = false;
		}
		creationType = 2;

		if (retVal && rtv)
			retVal = CreateRenderTargetView(dev);
		if (retVal && srv)
			retVal = CreateShaderResourceView(dev);
		return retVal;
	}

	bool GetSharedHandle()
	{
		if (pTexture)
		{
			IDXGIResource* renderResource(NULL);
			HRESULT result = pTexture->QueryInterface(__uuidof(IDXGIResource), (LPVOID*)&renderResource);
			if (FAILED(result)) {
				logError << "Failed to get Shared Resource" << std::endl;
				return false;
			}
			renderResource->GetSharedHandle(&pSharedHandle);
			renderResource->Release();
			renderResource = nullptr;
		}
		return false;
	}

	bool CreateRenderTargetView(ID3D11Device* dev)
	{
		//----
		// Creates a generic render target desc
		//----
		D3D11_RENDER_TARGET_VIEW_DESC renderTargetViewDesc;
		ZeroMemory(&renderTargetViewDesc, sizeof(D3D11_RENDER_TARGET_VIEW_DESC));
		renderTargetViewDesc.Format = textureDesc.Format;
		renderTargetViewDesc.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2D;
		renderTargetViewDesc.Texture2D.MipSlice = 0;

		HRESULT result = dev->CreateRenderTargetView(pTexture, &renderTargetViewDesc, &pRenderTarget);
		if (FAILED(result)) {
			logError << "Failed to create RenderTarget View" << std::endl;
			return false;
		}
		return true;
	}

	bool CreateShaderResourceView(ID3D11Device* dev)
	{
		//----
		// Creates a generic shader resource desc
		//----
		D3D11_SHADER_RESOURCE_VIEW_DESC shaderResourceViewDesc;
		ZeroMemory(&shaderResourceViewDesc, sizeof(D3D11_SHADER_RESOURCE_VIEW_DESC));
		shaderResourceViewDesc.Format = textureDesc.Format;
		shaderResourceViewDesc.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
		shaderResourceViewDesc.Texture2D.MostDetailedMip = 0;
		shaderResourceViewDesc.Texture2D.MipLevels = 1;

		HRESULT result = dev->CreateShaderResourceView(pTexture, &shaderResourceViewDesc, &pShaderResource);
		if (FAILED(result)) {
			logError << "Failed to create ShaderResource View" << std::endl;
			return false;
		}
		return true;
	}

	void Release()
	{
		if (creationType == 2) { pTexture = nullptr; }
		if (pShaderResource) { pShaderResource->Release(); pShaderResource = nullptr; }
		if (pRenderTarget) { pRenderTarget->Release(); pRenderTarget = nullptr; }
		if (pTexture) { pTexture->Release();  pTexture = nullptr; }
		pSharedHandle = nullptr;
		creationType = 0;
	}

	std::string GetErrors()
	{
		std::string curLog = logError.str();
		logError.str("");
		return curLog;
	}
};

