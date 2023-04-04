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
	 XMMATRIX gameWorldMatrix;
	 XMMATRIX hmdMatrix;
	 XMMATRIX eyeMatrix;
	 XMMATRIX rhcMatrix;
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
	bool controllerAtUI = false;
	RenderObject orthogSquare;
	RenderObject curvedUI; 
	RenderObject colorCube;
	RenderObject rayLine;

	RenderObject lineObj;

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
	void SetClearColor(ID3D11RenderTargetView* rtv, ID3D11DepthStencilView* dsv, float color[], bool clearDepth = false);
	void SetBlendIndex(int index);
	void SetMousePosition(HWND hwnd, int mouseX, int mouseY);
	void SetMouseBuffer(HWND hwnd, int width, int height, int mouseX, int mouseY, bool dalamudMode);
	void RunFrameUpdate(stScreenLayout screenLayout, XMMATRIX rayMatrix, poseType inputType, bool dalamudMode);
	void RenderLines(std::vector<std::vector<float>> LineRender);
	void DoRender(D3D11_VIEWPORT viewport, ID3D11RenderTargetView* rtv, ID3D11ShaderResourceView* srv, ID3D11DepthStencilView* dsv, stMatrixSet* matrixSet, bool isOrthog = false);
	void Release();
	bool HasErrors();
	std::string GetErrors();
};