#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <DirectXMath.h>
#include <D3Dcompiler.h>

#include "stCommon.h"
#include "Configuration.h"

struct VertexType
{
	float position[3];
	float texture[2];
};

struct stMatrixBuffer
{
	DirectX::XMMATRIX world;
	DirectX::XMMATRIX view;
	DirectX::XMMATRIX projection;
};

struct stMouseBuffer
{
	DirectX::XMFLOAT2 radius;
	DirectX::XMFLOAT2 coords;
};

class BasicRenderer
{
	ID3D11Device* dev;
	ID3D11DeviceContext* devcon;

	ID3D11VertexShader* pVS = nullptr;
	ID3D11PixelShader* pPS = nullptr;
	ID3D11InputLayout* pLayout = nullptr;
	ID3D11Buffer* pCVBuffer = nullptr;
	ID3D11Buffer* pCIBuffer = nullptr;
	ID3D11Buffer* pVBuffer = nullptr;
	ID3D11Buffer* pIBuffer = nullptr;
	ID3D11Buffer* pMatrixBuffer = nullptr;
	ID3D11Buffer* pMouseBuffer = nullptr;
	ID3D11SamplerState* pSampleState = nullptr;
	ID3D11BlendState* pBlendState[2] = { nullptr, nullptr };

	stMatrixBuffer matrixBuffer = stMatrixBuffer();
	stMouseBuffer mouseBuffer = stMouseBuffer();

	Configuration* cfg;
	float clearColor[4];
	bool doClear = false;
	bool disableBlend = false;
	int blendIndex = 0;

	bool CreateShaders();
	void DestroyShaders();
	bool CreateBuffers();
	void DestroyBuffers();

private:
	void MapResource(ID3D11Buffer* buffer, void* data, int size);

public:
	BasicRenderer(Configuration* config);
	BasicRenderer(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon);
	~BasicRenderer();
	bool SetDevice(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon);
	void SetClearColor(float color[]);
	void SetBlendIndex(int index);
	void SetMousePosition(HWND hwnd, int width, int height);
	void DoRender(D3D11_VIEWPORT viewport, ID3D11RenderTargetView* rtv, ID3D11ShaderResourceView* srv, DirectX::XMMATRIX projectionMatrix, DirectX::XMMATRIX viewMatrix, bool isOrthog = false);
	void Release();
};