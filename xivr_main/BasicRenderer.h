#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <DirectXMath.h>
#include <D3Dcompiler.h>
#include <iostream>

#include "stCommon.h"
#include "Configuration.h"
#include "RenderObject.h"

using namespace DirectX;

struct stMatrixSet
{
	XMMATRIX projectionMatrix;
	XMMATRIX gameWorldMatrixFloating;
	XMMATRIX gameWorldMatrix;
	XMMATRIX hmdMatrix;
	XMMATRIX eyeMatrix;
	XMMATRIX rhcMatrix;
	XMMATRIX lhcMatrix;
	Vector4 oskOffset;
};

struct VertexType
{
	float position[3];
	float texture[2];
};

struct VertexTypeColor
{
	float position[3];
	float color[4];
};

struct stMatrixBuffer
{
	 XMMATRIX world;
	 XMMATRIX view;
	 XMMATRIX projection;
};

struct stMouseBuffer
{
	 XMFLOAT2 radiusR;
	 XMFLOAT2 coordsR;
	 XMFLOAT2 radiusB;
	 XMFLOAT2 coordsB;
};

class BasicRenderer
{
	ID3D11Device* dev;
	ID3D11DeviceContext* devcon;

	ID3D11VertexShader* pVS = nullptr;
	ID3D11PixelShader* pPS = nullptr;
	ID3D11PixelShader* pPS1 = nullptr;
	ID3D11VertexShader* pVSColor = nullptr;
	ID3D11PixelShader* pPSColor = nullptr;
	ID3D11InputLayout* pLayout = nullptr;
	ID3D11InputLayout* pLayoutColor = nullptr;
	ID3D11Buffer* pMatrixBuffer = nullptr;
	ID3D11Buffer* pMouseBuffer = nullptr;
	ID3D11SamplerState* pSampleState = nullptr;
	ID3D11BlendState* pBlendState[3] = { nullptr, nullptr, nullptr };
	ID3D11DepthStencilState* pDepthStateOff = nullptr;
	ID3D11DepthStencilState* pDepthStateOn = nullptr;
	ID3D11RasterizerState* pRasterizerState = nullptr;

	stMatrixBuffer matrixBuffer = stMatrixBuffer();
	stMouseBuffer mouseBuffer = stMouseBuffer();
	std::stringstream logError;

	VertexType uiVertices[32][3] = {};
	short uiIndices[32][3] = {};
	float uiNormals[32][3] = {};
	XMMATRIX uiMatrix = XMMatrixIdentity();

	stConfiguration* cfg;
	bool disableBlend = false;
	int blendIndex = 0;
	RenderObject orthogSquare;
	RenderObject curvedUI;
	RenderObject osk;
	RenderObject colorCube;
	RenderObject rayLine;
	RenderObject handSquare[9];
	RenderObject lineObj;

	bool curvedUIAtUI = false;
	bool oskAtUI = false;
	bool handSquareAtUI[9] = {};
	int handSquareCount = sizeof(handSquare) / sizeof(RenderObject);
	bool needsRecenter = true;

	bool haveSaved = false;
	ID3D11BlendState* savedBlendState = nullptr;
	float savedBlendFactor[4] = { 0.f, 0.f, 0.f, 0.f };
	UINT savedSampleMask = 0;
	ID3D11DepthStencilState* savedDepthStencilState = nullptr;
	UINT savedStencilRef = 0;
	ID3D11RenderTargetView* savedRenderTargetView = nullptr;
	ID3D11DepthStencilView* savedDepthStencilView = nullptr;
	ID3D11SamplerState* savedSampleState = nullptr;
	D3D_PRIMITIVE_TOPOLOGY savedPrimitiveTopology = D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST;
	UINT savedNumViewports;
	D3D11_VIEWPORT savedViewport;
	ID3D11InputLayout* savedInputLayout = nullptr;
	ID3D11VertexShader* savedVertexShader = nullptr;
	ID3D11ClassInstance* savedVertexClassInstance = nullptr;
	UINT savedVertexNumClassInstance = 0;
	ID3D11PixelShader* savedPixelShader = nullptr;
	ID3D11ClassInstance* savedPixelClassInstance = nullptr;
	UINT savedPixelNumClassInstance = 0;
	ID3D11Buffer* savedVSBuffer = nullptr;
	ID3D11Buffer* savedPSBuffer = nullptr;
	ID3D11Buffer* savedVertexBuffer = nullptr;
	UINT savedVertexStride = 0;
	UINT savedVertexOffset = 0;
	ID3D11Buffer* savedIndexBuffer = nullptr;
	DXGI_FORMAT	savedIndexFormat = DXGI_FORMAT_R16_UINT;
	UINT savedIndexOffset = 0;

	bool CompileShaderFromString(std::string shaderSource, std::string shaderName, std::string target, ID3D10Blob** shader);
	bool CreateShaders();
	void DestroyShaders();
	bool CreateBuffers();
	void DestroyBuffers();
	void MapResource(ID3D11Buffer* buffer, void* data, int size);

public:
	BasicRenderer(stConfiguration* config);
	BasicRenderer(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon);
	~BasicRenderer();
	bool SetDevice(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon);
	void SetMousePosition(HWND hwnd, int mouseX, int mouseY, bool forceMouse = false);
	void SetMouseBuffer(HWND hwnd, int width, int height, int mouseX, int mouseY, bool dalamudMode);
	void RunFrameUpdate(stScreenLayout* screenLayout, stScreenLayout* oskLayout, XMMATRIX rayMatrix, Vector4 oskOffset, poseType inputType, bool dalamudMode, bool showOSK);
	void RenderLines(std::vector<std::vector<float>> LineRender);
	void DoRender(D3D11_VIEWPORT viewport, ID3D11RenderTargetView* rtv, ID3D11ShaderResourceView* srv, ID3D11DepthStencilView* dsv, stMatrixSet* matrixSet, bool isOrthog = false);
	void SaveSettings();
	void LoadSettings();
	void SetRenderTarget(ID3D11RenderTargetView* rtv, ID3D11DepthStencilView* dsv);
	void SetClearColor(ID3D11RenderTargetView* rtv, ID3D11DepthStencilView* dsv, float color[], bool clearDepth = false);
	void SetBlendIndex(int index);
	void DoRenderRay(D3D11_VIEWPORT viewport, stMatrixSet* matrixSet);
	void DoRenderLine(D3D11_VIEWPORT viewport, stMatrixSet* matrixSet);
	void DoRender(D3D11_VIEWPORT viewport, ID3D11ShaderResourceView* srv, stMatrixSet* matrixSet, int blendIndex, bool useDepth, bool isOrthog = false, bool moveOrthog = false);
	void DoRenderOSK(D3D11_VIEWPORT viewport, ID3D11ShaderResourceView* srv, stMatrixSet* matrixSet, int blendIndex, bool useDepth);
	void DoRenderWatch(D3D11_VIEWPORT viewport, ID3D11ShaderResourceView* srv[], stMatrixSet* matrixSet, int blendIndex);
	void GetUIStatus(bool* status, int count);
	void Release();
	bool HasErrors();
	std::string GetErrors();
};