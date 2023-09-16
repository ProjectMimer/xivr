#include "BasicRenderer.h"
#define _USE_MATH_DEFINES
#include <math.h>
#include <list>

BasicRenderer::BasicRenderer(stConfiguration* config) : dev(nullptr), devcon(nullptr), cfg(config)
{
}

BasicRenderer::BasicRenderer(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon) : dev(tdev), devcon(tdevcon)
{
	if (dev != nullptr && devcon != nullptr)
	{
		CreateShaders();
		CreateBuffers();
	}
}

BasicRenderer::~BasicRenderer()
{
	Release();
}

bool BasicRenderer::SetDevice(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon)
{
	dev = tdev;
	devcon = tdevcon;
	bool retVal = true;
	if (dev != nullptr && devcon != nullptr)
	{
		retVal &= CreateShaders();
		retVal &= CreateBuffers();
	}
	else
		retVal = false;
	return retVal;
}


bool BasicRenderer::CompileShaderFromString(std::string shaderSource, std::string shaderName, std::string target, ID3D10Blob** shader)
{
	HRESULT result = S_OK;
	ID3D10Blob* errorMessage;

	result = D3DCompile(shaderSource.c_str(), strlen(shaderSource.c_str()), 0, 0, 0, shaderName.c_str(), target.c_str(), 0, 0, shader, &errorMessage);
	if (FAILED(result)) {
		if (errorMessage) {
			std::string errorMsg = "Error compiling shader '" + shaderName + "'";
			MessageBoxA(0, errorMsg.c_str(), "CompileShaderFromString", MB_OK);
			return false;
		}
		else {
			std::string errorMsg = "Error compiling shader '" + shaderName + "'";
			MessageBoxA(0, errorMsg.c_str(), "CompileShaderFromString", MB_OK);
			return false;
		}
	}
	return true;
}


bool BasicRenderer::CreateShaders()
{

	const char* defaultVertexShaderSrc = R"""(
struct VOut
{
	float4 position : SV_POSITION;
	float2 tex : TEXCOORD0;
};
		
VOut VShader(float4 position : POSITION, float2 tex : TEXCOORD0)
{
	VOut output;
	output.position = float4(position.xyz, 1.0f);
	output.tex = tex;
	return output;
}
		)""";

	const char* VertexShaderColorSrc = R"""(
struct VOutColor
{
	float4 position : SV_POSITION;
	float4 color : COLOR;
};
cbuffer MatrixBuffer
{
	matrix worldMatrix;
	matrix viewMatrix;
	matrix projectionMatrix;
};
VOutColor VShaderColor(float4 position : POSITION, float4 color : COLOR)
{
	VOutColor output;
    output.position = float4(position.xyz, 1.0f);
	output.position = mul(output.position, worldMatrix);
	output.position = mul(output.position, viewMatrix);
	output.position = mul(output.position, projectionMatrix);
	output.color = color;
	return output;
}
		)""";

	const char* defaultVertexShaderWithProjectionSrc = R"""(
struct VOut
{
	float4 position : SV_POSITION;
	float2 tex : TEXCOORD0;
};
		
cbuffer MatrixBuffer
{
	matrix worldMatrix;
	matrix viewMatrix;
	matrix projectionMatrix;
};
		
VOut VShader(float4 position : POSITION, float2 tex : TEXCOORD0)
{
	VOut output;
	output.position = float4(position.xyz, 1.0f);
	output.position = mul(output.position, worldMatrix);
	output.position = mul(output.position, viewMatrix);
	output.position = mul(output.position, projectionMatrix);
	output.tex = tex;
	return output;
}
		)""";

	const char* defaultPixelShaderSrc = R"""(
Texture2D shaderTexture;
SamplerState sampleType;
struct VOut
{
	float4 position : SV_POSITION;
	float2 tex : TEXCOORD0;
};

float4 PShader(VOut input) : SV_TARGET
{
	return shaderTexture.Sample(sampleType, input.tex);
}
		)""";

	const char* PixelShaderColorSrc = R"""(
struct VOutColor
{
	float4 position : SV_POSITION;
	float4 color : COLOR;
};

float4 PShaderColor(VOutColor input) : SV_TARGET
{
	return input.color;
}
		)""";

	const char* defaultPixelShaderWithoutMouseDotSrc = R"""(
Texture2D shaderTexture;
SamplerState sampleType;
static const float PI = 3.14159265;
cbuffer mousePos
{
	float2 radiusR;
	float2 coordsR;
	float2 radiusB;
	float2 coordsB;
};
struct VOut
{
	float4 position : SV_POSITION;
	float2 tex : TEXCOORD0;
};
float4 tex2Dmultisample(SamplerState tex, float2 uv)
{
	float2 dx = ddx(uv) * 0.25;
	float2 dy = ddy(uv) * 0.25;

	float4 sample0 = shaderTexture.Sample(tex, uv + dx + dy);
	float4 sample1 = shaderTexture.Sample(tex, uv + dx - dy);
	float4 sample2 = shaderTexture.Sample(tex, uv - dx + dy);
	float4 sample3 = shaderTexture.Sample(tex, uv - dx - dy);
    
	return (sample0 + sample1 + sample2 + sample3) * 0.25;
}
float4 PShader(VOut input) : SV_TARGET
{
	float4 pixel = tex2Dmultisample(sampleType, input.tex);
	return float4(pixel.xyz, 0.75f);
}
	)""";

	const char* defaultPixelShaderWithMouseDotSrc = R"""(
Texture2D shaderTexture;
SamplerState sampleType;
static const float PI = 3.14159265;
cbuffer mousePos
{
	float2 radiusR;
	float2 coordsR;
	float2 radiusB;
	float2 coordsB;
};
struct VOut
{
	float4 position : SV_POSITION;
	float2 tex : TEXCOORD0;
};
float4 tex2Dmultisample(SamplerState tex, float2 uv)
{
	float2 dx = ddx(uv) * 0.25;
	float2 dy = ddy(uv) * 0.25;

	float4 sample0 = shaderTexture.Sample(tex, uv + dx + dy);
	float4 sample1 = shaderTexture.Sample(tex, uv + dx - dy);
	float4 sample2 = shaderTexture.Sample(tex, uv - dx + dy);
	float4 sample3 = shaderTexture.Sample(tex, uv - dx - dy);
    
	return (sample0 + sample1 + sample2 + sample3) * 0.25;
}
float4 PShader(VOut input) : SV_TARGET
{
	float distanceR = length(input.tex - coordsR);
	float distanceB = length(input.tex - coordsB);
	float4 pixel = float4(0.0, 0.0, 0.0, 0.0);
	if (distanceR <= radiusR.x)
	{
		pixel = float4(1.0, 0.0, 0.0, 1.0);
	}
	else if (distanceB <= radiusB.x)
	{
		pixel = float4(0.498, 0.0, 1.0, 1.0);
	}
	else
	{
		pixel = tex2Dmultisample(sampleType, input.tex);
	}
	return pixel;
}
	)""";

	HRESULT result = S_OK;
	ID3D10Blob* VS;
	ID3D10Blob* PS;

	CompileShaderFromString(defaultVertexShaderWithProjectionSrc, "VShader", "vs_4_0", &VS);
	result = dev->CreateVertexShader(VS->GetBufferPointer(), VS->GetBufferSize(), NULL, &pVS);
	if (FAILED(result)) {
		MessageBoxA(0, "Error Creating VertexShader", "Error", MB_OK);
		return false;
	}

	CompileShaderFromString(defaultPixelShaderWithMouseDotSrc, "PShader", "ps_4_0", &PS);
	result = dev->CreatePixelShader(PS->GetBufferPointer(), PS->GetBufferSize(), NULL, &pPS);
	if (FAILED(result)) {
		MessageBoxA(0, "Error Creating PixelShader", "Error", MB_OK);
		return false;
	}

	CompileShaderFromString(defaultPixelShaderWithoutMouseDotSrc, "PShader", "ps_4_0", &PS);
	result = dev->CreatePixelShader(PS->GetBufferPointer(), PS->GetBufferSize(), NULL, &pPS1);
	if (FAILED(result)) {
		MessageBoxA(0, "Error Creating PixelShader", "Error", MB_OK);
		return false;
	}

	// create the input layout object
	D3D11_INPUT_ELEMENT_DESC polygonLayout[] = {
		{"POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0},
		{"TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 12, D3D11_INPUT_PER_VERTEX_DATA, 0}
	};
	int numElements = sizeof(polygonLayout) / sizeof(polygonLayout[0]);

	result = dev->CreateInputLayout(polygonLayout, numElements, VS->GetBufferPointer(), VS->GetBufferSize(), &pLayout);
	if (FAILED(result)) {
		MessageBoxA(0, "Error Creating layout", "Error", MB_OK);
		return false;
	}

	VS->Release();
	PS->Release();


	VS = NULL;
	PS = NULL;
	CompileShaderFromString(VertexShaderColorSrc, "VShaderColor", "vs_4_0", &VS);
	result = dev->CreateVertexShader(VS->GetBufferPointer(), VS->GetBufferSize(), NULL, &pVSColor);
	if (FAILED(result)) {
		MessageBoxA(0, "Error Creating VertexShader Color", "Error", MB_OK);
		return false;
	}

	CompileShaderFromString(PixelShaderColorSrc, "PShaderColor", "ps_4_0", &PS);
	result = dev->CreatePixelShader(PS->GetBufferPointer(), PS->GetBufferSize(), NULL, &pPSColor);
	if (FAILED(result)) {
		MessageBoxA(0, "Error Creating PixelShader Color", "Error", MB_OK);
		return false;
	}

	// create the input layout object
	D3D11_INPUT_ELEMENT_DESC polygonLayoutColor[] = {
		{"POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0},
		{"COLOR", 0, DXGI_FORMAT_R32G32B32A32_FLOAT, 0, 12, D3D11_INPUT_PER_VERTEX_DATA, 0}
	};
	numElements = sizeof(polygonLayoutColor) / sizeof(polygonLayoutColor[0]);

	result = dev->CreateInputLayout(polygonLayoutColor, numElements, VS->GetBufferPointer(), VS->GetBufferSize(), &pLayoutColor);
	if (FAILED(result)) {
		MessageBoxA(0, "Error Creating layout Color", "Error", MB_OK);
		return false;
	}

	VS->Release();
	PS->Release();
	// Create the texture sampler state.
	D3D11_SAMPLER_DESC samplerDesc;
	ZeroMemory(&samplerDesc, sizeof(D3D11_SAMPLER_DESC));
	samplerDesc.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
	samplerDesc.AddressU = D3D11_TEXTURE_ADDRESS_WRAP;
	samplerDesc.AddressV = D3D11_TEXTURE_ADDRESS_WRAP;
	samplerDesc.AddressW = D3D11_TEXTURE_ADDRESS_WRAP;
	samplerDesc.MipLODBias = 0.0f;
	samplerDesc.MaxAnisotropy = 1;
	samplerDesc.ComparisonFunc = D3D11_COMPARISON_ALWAYS;
	samplerDesc.BorderColor[0] = 0;
	samplerDesc.BorderColor[1] = 0;
	samplerDesc.BorderColor[2] = 0;
	samplerDesc.BorderColor[3] = 0;
	samplerDesc.MinLOD = 0;
	samplerDesc.MaxLOD = D3D11_FLOAT32_MAX;
	result = dev->CreateSamplerState(&samplerDesc, &pSampleState);
	if (FAILED(result)) {
		MessageBoxA(0, "Error creating sampler state", "Error", MB_OK);
		return false;
	}

	// Create the blend state
	D3D11_BLEND_DESC BlendStateDesc;
	ZeroMemory(&BlendStateDesc, sizeof(D3D11_BLEND_DESC));
	BlendStateDesc.AlphaToCoverageEnable = FALSE;
	BlendStateDesc.IndependentBlendEnable = FALSE;
	BlendStateDesc.RenderTarget[0].BlendEnable = TRUE;
	BlendStateDesc.RenderTarget[0].SrcBlend = D3D11_BLEND_SRC_ALPHA;
	BlendStateDesc.RenderTarget[0].DestBlend = D3D11_BLEND_INV_SRC_ALPHA;
	BlendStateDesc.RenderTarget[0].BlendOp = D3D11_BLEND_OP_ADD;
	BlendStateDesc.RenderTarget[0].SrcBlendAlpha = D3D11_BLEND_ZERO;
	BlendStateDesc.RenderTarget[0].DestBlendAlpha = D3D11_BLEND_ZERO;
	BlendStateDesc.RenderTarget[0].BlendOpAlpha = D3D11_BLEND_OP_ADD;
	BlendStateDesc.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
	result = dev->CreateBlendState(&BlendStateDesc, &pBlendState[0]);
	if (FAILED(result)) {
		MessageBoxA(0, "Error creating blend State", "Error", MB_OK);
		return false;
	}


	ZeroMemory(&BlendStateDesc, sizeof(D3D11_BLEND_DESC));
	BlendStateDesc.AlphaToCoverageEnable = FALSE;
	BlendStateDesc.IndependentBlendEnable = FALSE;
	BlendStateDesc.RenderTarget[0].BlendEnable = FALSE;
	BlendStateDesc.RenderTarget[0].SrcBlend = D3D11_BLEND_SRC_COLOR;
	BlendStateDesc.RenderTarget[0].DestBlend = D3D11_BLEND_ZERO;
	BlendStateDesc.RenderTarget[0].BlendOp = D3D11_BLEND_OP_ADD;
	BlendStateDesc.RenderTarget[0].SrcBlendAlpha = D3D11_BLEND_SRC_ALPHA;
	BlendStateDesc.RenderTarget[0].DestBlendAlpha = D3D11_BLEND_ZERO;
	BlendStateDesc.RenderTarget[0].BlendOpAlpha = D3D11_BLEND_OP_ADD;
	BlendStateDesc.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
	result = dev->CreateBlendState(&BlendStateDesc, &pBlendState[1]);
	if (FAILED(result)) {
		MessageBoxA(0, "Error creating blend state", "Error", MB_OK);
		return false;
	}

	ZeroMemory(&BlendStateDesc, sizeof(D3D11_BLEND_DESC));
	BlendStateDesc.AlphaToCoverageEnable = FALSE;
	BlendStateDesc.IndependentBlendEnable = FALSE;
	BlendStateDesc.RenderTarget[0].BlendEnable = TRUE;
	BlendStateDesc.RenderTarget[0].SrcBlend = D3D11_BLEND_SRC_COLOR;
	BlendStateDesc.RenderTarget[0].DestBlend = D3D11_BLEND_SRC_ALPHA;
	BlendStateDesc.RenderTarget[0].BlendOp = D3D11_BLEND_OP_ADD;
	BlendStateDesc.RenderTarget[0].SrcBlendAlpha = D3D11_BLEND_ONE;
	BlendStateDesc.RenderTarget[0].DestBlendAlpha = D3D11_BLEND_ZERO;
	BlendStateDesc.RenderTarget[0].BlendOpAlpha = D3D11_BLEND_OP_ADD;
	BlendStateDesc.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
	result = dev->CreateBlendState(&BlendStateDesc, &pBlendState[2]);
	if (FAILED(result)) {
		MessageBoxA(0, "Error creating blend state", "Error", MB_OK);
		return false;
	}


	D3D11_DEPTH_STENCIL_DESC depthStencilDesc;
	ZeroMemory(&depthStencilDesc, sizeof(D3D11_DEPTH_STENCIL_DESC));
	depthStencilDesc.DepthEnable = FALSE;
	depthStencilDesc.DepthWriteMask = D3D11_DEPTH_WRITE_MASK_ALL;
	depthStencilDesc.DepthFunc = D3D11_COMPARISON_LESS_EQUAL;
	depthStencilDesc.StencilEnable = TRUE;
	depthStencilDesc.StencilReadMask = 0xFF;
	depthStencilDesc.StencilWriteMask = 0xFF;
	depthStencilDesc.FrontFace.StencilFailOp = D3D11_STENCIL_OP_KEEP;
	depthStencilDesc.FrontFace.StencilDepthFailOp = D3D11_STENCIL_OP_KEEP;
	depthStencilDesc.FrontFace.StencilPassOp = D3D11_STENCIL_OP_KEEP;
	depthStencilDesc.FrontFace.StencilFunc = D3D11_COMPARISON_ALWAYS;
	depthStencilDesc.BackFace.StencilFailOp = D3D11_STENCIL_OP_KEEP;
	depthStencilDesc.BackFace.StencilDepthFailOp = D3D11_STENCIL_OP_KEEP;
	depthStencilDesc.BackFace.StencilPassOp = D3D11_STENCIL_OP_KEEP;
	depthStencilDesc.BackFace.StencilFunc = D3D11_COMPARISON_ALWAYS;
	result = dev->CreateDepthStencilState(&depthStencilDesc, &pDepthStateOff);
	if (FAILED(result)) {
		MessageBoxA(0, "Error creating depth State", "Error", MB_OK);
		return false;
	}

	ZeroMemory(&depthStencilDesc, sizeof(D3D11_DEPTH_STENCIL_DESC));
	depthStencilDesc.DepthEnable = TRUE;
	depthStencilDesc.DepthWriteMask = D3D11_DEPTH_WRITE_MASK_ALL;
	depthStencilDesc.DepthFunc = D3D11_COMPARISON_LESS_EQUAL;
	depthStencilDesc.StencilEnable = TRUE;
	depthStencilDesc.StencilReadMask = 0xFF;
	depthStencilDesc.StencilWriteMask = 0xFF;
	depthStencilDesc.FrontFace.StencilFailOp = D3D11_STENCIL_OP_KEEP;
	depthStencilDesc.FrontFace.StencilDepthFailOp = D3D11_STENCIL_OP_KEEP;
	depthStencilDesc.FrontFace.StencilPassOp = D3D11_STENCIL_OP_KEEP;
	depthStencilDesc.FrontFace.StencilFunc = D3D11_COMPARISON_ALWAYS;
	depthStencilDesc.BackFace.StencilFailOp = D3D11_STENCIL_OP_KEEP;
	depthStencilDesc.BackFace.StencilDepthFailOp = D3D11_STENCIL_OP_KEEP;
	depthStencilDesc.BackFace.StencilPassOp = D3D11_STENCIL_OP_KEEP;
	depthStencilDesc.BackFace.StencilFunc = D3D11_COMPARISON_ALWAYS;
	result = dev->CreateDepthStencilState(&depthStencilDesc, &pDepthStateOn);
	if (FAILED(result)) {
		MessageBoxA(0, "Error creating depth State", "Error", MB_OK);
		return false;
	}

	D3D11_RASTERIZER_DESC rasterDesc;
	ZeroMemory(&rasterDesc, sizeof(D3D11_RASTERIZER_DESC));
	rasterDesc.FillMode = D3D11_FILL_WIREFRAME;
	rasterDesc.CullMode = D3D11_CULL_NONE;
	rasterDesc.FrontCounterClockwise = TRUE;
	rasterDesc.DepthBias = 0;
	rasterDesc.DepthBiasClamp = 100.0f;
	rasterDesc.SlopeScaledDepthBias = 0.0f;
	rasterDesc.DepthClipEnable = FALSE;
	rasterDesc.ScissorEnable = FALSE;
	rasterDesc.MultisampleEnable = FALSE;
	rasterDesc.AntialiasedLineEnable = FALSE;

	result = dev->CreateRasterizerState(&rasterDesc, &pRasterizerState);
	if (FAILED(result)) {
		MessageBoxA(0, "Error creating raster State", "Error", MB_OK);
		return false;
	}

	return true;
}


void BasicRenderer::DestroyShaders()
{
	if (pVS) { pVS->Release(); pVS = nullptr; }
	if (pPS) { pPS->Release(); pPS = nullptr; }
	if (pVSColor) { pVSColor->Release(); pVSColor = nullptr; }
	if (pPSColor) { pPSColor->Release(); pPSColor = nullptr; }
	if (pLayout) { pLayout->Release(); pLayout = nullptr; }
	if (pLayoutColor) { pLayoutColor->Release(); pLayoutColor = nullptr; }
	if (pSampleState) { pSampleState->Release(); pSampleState = nullptr; }
	if (pDepthStateOff) { pDepthStateOff->Release(); pDepthStateOff = nullptr; }
	if (pDepthStateOn) { pDepthStateOn->Release(); pDepthStateOn = nullptr; }
	if (pRasterizerState) { pRasterizerState->Release(); pRasterizerState = nullptr; }
	if (pBlendState[0]) { pBlendState[0]->Release(); pBlendState[0] = nullptr; }
	if (pBlendState[1]) { pBlendState[1]->Release(); pBlendState[1] = nullptr; }
	if (pBlendState[2]) { pBlendState[2]->Release(); pBlendState[2] = nullptr; }
}

bool BasicRenderer::CreateBuffers()
{
	HRESULT result = S_OK;

	orthogSquare = RenderSquare(dev, devcon);
	orthogSquare.SetShadersLayout(pLayout, pVS, pPS1);

	curvedUI = RenderCurvedUI(dev, devcon);
	curvedUI.SetShadersLayout(pLayout, pVS, pPS);
	//curvedUI.SetShadersLayout(pLayoutColor, pVSColor, pPSColor);

	osk = RenderOSK(dev, devcon);
	osk.SetShadersLayout(pLayout, pVS, pPS1);
	
	colorCube = RenderCube(dev, devcon);
	colorCube.SetShadersLayout(pLayoutColor, pVSColor, pPSColor);

	rayLine = RenderRayLine(dev, devcon);
	rayLine.SetShadersLayout(pLayoutColor, pVSColor, pPSColor);

	for (int i = 0; i < handSquareCount; i++)
	{
		handSquare[i] = RenderSquare(dev, devcon);
		handSquare[i].SetShadersLayout(pLayout, pVS, pPS1);
	}


	D3D11_BUFFER_DESC matrixBufferDesc;
	ZeroMemory(&matrixBufferDesc, sizeof(D3D11_BUFFER_DESC));
	matrixBufferDesc.Usage = D3D11_USAGE_DYNAMIC;
	matrixBufferDesc.ByteWidth = sizeof(stMatrixBuffer);
	matrixBufferDesc.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
	matrixBufferDesc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
	matrixBufferDesc.MiscFlags = 0;
	matrixBufferDesc.StructureByteStride = 0;
	result = dev->CreateBuffer(&matrixBufferDesc, NULL, &pMatrixBuffer);
	if (FAILED(result)) {
		MessageBoxA(0, "Error creating matrix buffer", "Error", MB_OK);
		return false;
	}

	D3D11_BUFFER_DESC mouseBufferDesc;
	ZeroMemory(&mouseBufferDesc, sizeof(D3D11_BUFFER_DESC));
	mouseBufferDesc.Usage = D3D11_USAGE_DYNAMIC;
	mouseBufferDesc.ByteWidth = sizeof(stMouseBuffer);
	mouseBufferDesc.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
	mouseBufferDesc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
	mouseBufferDesc.MiscFlags = 0;
	mouseBufferDesc.StructureByteStride = 0;
	result = dev->CreateBuffer(&mouseBufferDesc, NULL, &pMouseBuffer);
	if (FAILED(result)) {
		MessageBoxA(0, "Error creating mouse buffer", "Error", MB_OK);
		return false;
	}

	return true;
}

void BasicRenderer::DestroyBuffers()
{
	if (pMatrixBuffer) { pMatrixBuffer->Release(); pMatrixBuffer = nullptr; }
	if (pMouseBuffer) { pMouseBuffer->Release(); pMouseBuffer = nullptr; }

	curvedUI.Release();
	osk.Release();
	orthogSquare.Release();
	colorCube.Release();
	rayLine.Release();

	for (int i = 0; i < handSquareCount; i++)
		handSquare[i].Release();
}

void BasicRenderer::MapResource(ID3D11Buffer* buffer, void* data, int size)
{
	D3D11_MAPPED_SUBRESOURCE mappedResource = D3D11_MAPPED_SUBRESOURCE();
	devcon->Map(buffer, 0, D3D11_MAP_WRITE_DISCARD, 0, &mappedResource);
	memcpy(mappedResource.pData, data, size);
	devcon->Unmap(buffer, 0);
}

void BasicRenderer::SetMousePosition(HWND hwnd, int mouseX, int mouseY, bool forceMouse)
{
	HWND active = GetActiveWindow();
	POINT p;

	if (active == hwnd || forceMouse == true)
	{
		p.x = mouseX;
		p.y = mouseY;
		ClientToScreen(hwnd, &p);
		SetCursorPos(p.x, p.y);
	}
}

void BasicRenderer::SetMouseBuffer(HWND hwnd, int width, int height, int mouseX, int mouseY, bool dalamudMode)
{
	//----
	// check and see if the main ui is the only object being interacted with
	// and only show the dot on the main ui if the ray is over the main ui
	//----
	bool showOnUI = curvedUIAtUI;
	if (oskAtUI)
		showOnUI |= false;
	for (int i = 0; i < handSquareCount; i++)
		if(handSquareAtUI[i])
			showOnUI |= false;

	if (mouseX == width / 2 && mouseY == height / 2 || showOnUI != curvedUIAtUI)
	{
		mouseBuffer.radiusR.x = 0.000f;
		mouseBuffer.radiusB.x = 0.000f;
	}
	else
	{
		mouseBuffer.radiusR.x = 0.0025f;
		mouseBuffer.radiusB.x = 0.0025f;
		if (dalamudMode == false)
			mouseBuffer.radiusB.x = 0.0f;
		else if (dalamudMode == true)
			mouseBuffer.radiusR.x = 0.0f;
	}

	POINT p;
	GetCursorPos(&p);
	ScreenToClient(hwnd, &p);

	mouseBuffer.radiusR.y = 0.0f;
	mouseBuffer.coordsR.x = mouseX / (float)width;
	mouseBuffer.coordsR.y = mouseY / (float)height;
	mouseBuffer.radiusB.y = 0.0f;
	mouseBuffer.coordsB.x = p.x / (float)width;
	mouseBuffer.coordsB.y = p.y / (float)height;
	MapResource(pMouseBuffer, &mouseBuffer, sizeof(stMouseBuffer));
}

void BasicRenderer::RunFrameUpdate(stScreenLayout* screenLayout, stScreenLayout* oskLayout, XMMATRIX rayMatrix, Vector4 oskOffset, poseType inputRayType, bool dalamudMode, bool showOSK)
{
	struct intersectLayout
	{
		RenderObject* item;
		bool* atUI;
		stScreenLayout* layout;
		XMVECTOR intersection;
		float dist;
		float multiplyer;
		bool updateDistance;
		bool forceMouse;
		bool fromCenter;
	};

	for (int i = 0; i < handSquareCount; i++) { handSquareAtUI[i] = false; }
	oskAtUI = false;
	curvedUIAtUI = false;
	
	float aspect = (float)screenLayout->width / (float)screenLayout->height;

	XMMATRIX aspectScaleMatrix = XMMatrixScaling(aspect, 1, 1);
	XMMATRIX uiScaleMatrix = XMMatrixScaling(cfg->uiOffsetScale, cfg->uiOffsetScale, cfg->uiOffsetScale);
	XMMATRIX uiZMatrix = XMMatrixTranslation(0.0f, 0.0f, (cfg->uiOffsetZ / 100.0f));
	XMMATRIX moveMatrix = XMMatrixTranslation(0.0f, 0.0f, -1.0f);
	curvedUI.SetObjectMatrix(aspectScaleMatrix * uiScaleMatrix * uiZMatrix * moveMatrix);

	aspect = 1;
	if (oskLayout != nullptr && oskLayout->haveLayout)
		aspect = (float)oskLayout->width / (float)oskLayout->height;
	XMMATRIX aspectScaleMatrixOSK = XMMatrixScaling(aspect, 1, 1);
	XMMATRIX rotateMatrixOSK = XMMatrixRotationX(-30.0f * ((float)M_PI / 180.0f));
	XMMATRIX moveMatrixOSK = XMMatrixTranslation(0.0f, -0.4f, -0.99f);
	XMMATRIX scaleMatrixOSK = XMMatrixScaling(0.08f, 0.08f, 0.08f);
	XMMATRIX scaleOSK = (oskLayout != nullptr && oskLayout->haveLayout && showOSK) ? XMMatrixScaling(1.0f, 1.0f, 1.0f) : XMMatrixScaling(0.0001f, 0.0001f, 0.0001f);
	XMMATRIX offsetMatrixOSK = XMMatrixTranslation(oskOffset.x, oskOffset.y, oskOffset.z);
	//XMMATRIX offsetMatrixOSK = XMMatrixRotationY(-oskOffset.x * 3);
	osk.SetObjectMatrix(aspectScaleMatrixOSK * scaleMatrixOSK * rotateMatrixOSK * uiZMatrix * moveMatrixOSK * offsetMatrixOSK * scaleOSK);
	//osk.SetObjectMatrix(aspectScaleMatrixOSK * scaleMatrixOSK * uiZMatrix * offsetMatrixOSK * scaleOSK);

	XMMATRIX scaleMatrixRHC = XMMatrixScaling(0.025f, 0.025f, 0.05f);
	colorCube.SetObjectMatrix(scaleMatrixRHC * rayMatrix);

	std::vector<float> lineData =
	{
		0, 0, 0,	0.0f, 0.0f, 0.0f, 0.0f,
		0, 0, 0,	0.0f, 0.0f, 0.0f, 0.0f,
	};

	if (inputRayType == poseType::hmdPosition || inputRayType == poseType::RightHand)
	{
		XMVECTOR origin = { rayMatrix.r[3].m128_f32[0], rayMatrix.r[3].m128_f32[1], rayMatrix.r[3].m128_f32[2] };
		XMVECTOR frwd = { rayMatrix.r[2].m128_f32[0], rayMatrix.r[2].m128_f32[1], rayMatrix.r[2].m128_f32[2] };
		XMVECTOR end = origin + (frwd * -1) * 10.f;
		XMVECTOR norm = XMVector3Normalize(frwd);

		lineData =
		{
			origin.m128_f32[0], origin.m128_f32[1], origin.m128_f32[2], 1.0f, 0.0f, 0.0f, 0.75f,
			end.m128_f32[0], end.m128_f32[1], end.m128_f32[2],			1.0f, 0.0f, 0.0f, 0.25f
		};

		if (dalamudMode)
		{
			lineData[ 3] = 0.498f; lineData[ 4] = 0.0f; lineData[ 5] = 1.0f;
			lineData[10] = 0.498f; lineData[11] = 0.0f; lineData[12] = 1.0f;
		}

		//----
		// Add all the interactable items to the intersect list
		//----
		std::list<intersectLayout> intersectList = std::list<intersectLayout>();
		for (int i = 0; i < handSquareCount; i++)
			intersectList.push_back({ &handSquare[i], &handSquareAtUI[i], nullptr, { 0, 0, 0 }, 0, 0, true, false, false });
		intersectList.push_back({ &osk, &oskAtUI, oskLayout, { 0, 0, 0 }, 0, 1, true, true, false });
		if(dalamudMode)
			intersectList.push_back({ &curvedUI, &curvedUIAtUI, screenLayout, { 0, 0, 0 }, 0, 1, false, false, true });
		else
			intersectList.push_back({ &curvedUI, &curvedUIAtUI, screenLayout, { 0, 0, 0 }, 0, 3, false, false, true });

		//----
		// Go though all interactable items and check to see if the ray interacts with something
		//----
		
		float dist = -9999;
		intersectLayout closest = intersectLayout();
		for (std::list<intersectLayout>::iterator it = intersectList.begin(); it != intersectList.end(); ++it)
		{
			if (it->layout == nullptr || it->layout->haveLayout)
			{
				*(it->atUI) = it->item->RayIntersection(origin, norm, &it->intersection, &it->dist, &logError);
				if (*(it->atUI) == true && it->dist >= dist)
				{
					dist = it->dist;
					closest = *it;
				}
			}
		}

		if (closest.item != nullptr)
		{
			HWND useHWND = 0;
			if (closest.layout != nullptr)
			{
				POINT halfScreen = POINT();
				halfScreen.x = (closest.layout->width / 2);
				halfScreen.y = (closest.layout->height / 2);

				//----
				// converts uv (0.0->1.0) to screen coords | width/height
				//----
				closest.intersection.m128_f32[0] = closest.intersection.m128_f32[0] * closest.layout->width;
				closest.intersection.m128_f32[1] = closest.intersection.m128_f32[1] * closest.layout->height;

				//----
				// Changes anchor from top left corner to middle of screen
				//----
				if (closest.fromCenter)
				{
					closest.intersection.m128_f32[0] = halfScreen.x + ((closest.intersection.m128_f32[0] - halfScreen.x) / closest.multiplyer);
					closest.intersection.m128_f32[1] = halfScreen.y + ((closest.intersection.m128_f32[1] - halfScreen.y) / closest.multiplyer);
				}
				useHWND = closest.layout->hwnd;
			}

			end = origin + (norm * dist);
			if (closest.updateDistance)
			{
				lineData[7] = end.m128_f32[0];
				lineData[8] = end.m128_f32[1];
				lineData[9] = end.m128_f32[2];
			}
			lineData[10] = 1.0f;
			lineData[11] = 1.0f;
			lineData[12] = 1.0f;

			if (inputRayType == poseType::RightHand)
			{
				needsRecenter = true;
				SetMousePosition(useHWND, (int)closest.intersection.m128_f32[0], (int)closest.intersection.m128_f32[1], closest.forceMouse);
			}
		}
		else
		{
			if (needsRecenter)
			{
				POINT halfScreen = POINT();
				halfScreen.x = (screenLayout->width / 2);
				halfScreen.y = (screenLayout->height / 2);

				needsRecenter = false;
				SetMousePosition(screenLayout->hwnd, halfScreen.x, halfScreen.y, false);
			}
		}
		if (inputRayType == poseType::RightHand)
		{
			rayLine.MapResource(&lineData[0], (int)lineData.size() * sizeof(float));
		}
		else
		{
			lineData =
			{
				0, 0, 0,	0.0f, 0.0f, 0.0f, 0.0f,
				0, 0, 0,	0.0f, 0.0f, 0.0f, 0.0f,
			};
			rayLine.MapResource(&lineData[0], (int)lineData.size() * sizeof(float));
		}
	}
	else
	{
		rayLine.MapResource(&lineData[0], (int)lineData.size() * sizeof(float));
	}
}

void BasicRenderer::RenderLines(std::vector<std::vector<float>> LineRender)
{
	std::vector<float> vertices = std::vector<float>();
	for (int i = 0; i < LineRender.size(); i++)
	{
		std::vector<float> p0 = { LineRender[i][0], LineRender[i][1], LineRender[i][2], 1.0f, 1.0f, 0.0f, 1.0f };
		std::vector<float> p1 = { LineRender[i][3], LineRender[i][4], LineRender[i][5],	1.0f, 0.0f, 1.0f, 1.0f };

		vertices.insert(vertices.end(), p0.begin(), p0.end());
		vertices.insert(vertices.end(), p1.begin(), p1.end());
	}

	lineObj.Release();
	lineObj = RenderObject(dev, devcon);
	lineObj.SetShadersLayout(pLayoutColor, pVSColor, pPSColor);
	lineObj.SetVertexBuffer(vertices, 7, D3D11_USAGE_DYNAMIC);
}

void BasicRenderer::DoRender(D3D11_VIEWPORT viewport, ID3D11RenderTargetView* rtv, ID3D11ShaderResourceView* srv, ID3D11DepthStencilView* dsv, stMatrixSet* matrixSet, bool isOrthog)
{
	XMMATRIX gameWorldMatrix = matrixSet->gameWorldMatrix * XMMatrixInverse(0, matrixSet->eyeMatrix * matrixSet->hmdMatrix);

	UINT Stride = sizeof(VertexType);
	UINT Offset = 0;

	FLOAT blendFactor[4] = { 0.f, 0.f, 0.f, 0.f };

	devcon->OMSetBlendState(pBlendState[blendIndex], blendFactor, 1);
	blendIndex = 0;

	devcon->OMSetRenderTargets(1, &rtv, dsv);
	//devcon->OMSetRenderTargets(1, &rtv, NULL);

	devcon->RSSetViewports(1, &viewport);
	devcon->VSSetConstantBuffers(0, 1, &pMatrixBuffer);
	devcon->PSSetConstantBuffers(0, 1, &pMouseBuffer);

	devcon->PSSetShaderResources(0, 1, &srv);
	devcon->PSSetSamplers(0, 1, &pSampleState);


	if (isOrthog)
	{
		matrixBuffer.world = XMMatrixIdentity();
		matrixBuffer.view = XMMatrixIdentity();
		matrixBuffer.projection = XMMatrixIdentity();
		MapResource(pMatrixBuffer, &matrixBuffer, sizeof(stMatrixBuffer));

		devcon->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
		devcon->OMSetDepthStencilState(pDepthStateOn, 0);
		orthogSquare.Render();
	}
	else
	{
		matrixBuffer.world = rayLine.GetObjectMatrix(false, true);
		//matrixBuffer.view = XMMatrixTranspose(gameWorldMatrix);
		matrixBuffer.view = XMMatrixTranspose(XMMatrixInverse(0, matrixSet->eyeMatrix * matrixSet->hmdMatrix));
		matrixBuffer.projection = XMMatrixTranspose(matrixSet->projectionMatrix);
		MapResource(pMatrixBuffer, &matrixBuffer, sizeof(stMatrixBuffer));

		devcon->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_LINELIST);
		devcon->OMSetDepthStencilState(pDepthStateOn, 0);
		rayLine.Render();


		matrixBuffer.world = curvedUI.GetObjectMatrix(false, true);
		//matrixBuffer.view = XMMatrixTranspose(gameWorldMatrix);
		matrixBuffer.view = XMMatrixTranspose(XMMatrixInverse(0, matrixSet->eyeMatrix * matrixSet->hmdMatrix));
		matrixBuffer.projection = XMMatrixTranspose(matrixSet->projectionMatrix);
		MapResource(pMatrixBuffer, &matrixBuffer, sizeof(stMatrixBuffer));

		devcon->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
		if (cfg->uiDepth == false)
			devcon->OMSetDepthStencilState(pDepthStateOff, 0);
		else
			devcon->OMSetDepthStencilState(pDepthStateOn, 0);
		curvedUI.Render();


		devcon->OMSetDepthStencilState(pDepthStateOn, 0);

		matrixBuffer.world = colorCube.GetObjectMatrix(false, true);
		//matrixBuffer.view = XMMatrixTranspose(gameWorldMatrix);
		matrixBuffer.view = XMMatrixTranspose(XMMatrixInverse(0, matrixSet->eyeMatrix * matrixSet->hmdMatrix));
		matrixBuffer.projection = XMMatrixTranspose(matrixSet->projectionMatrix);
		MapResource(pMatrixBuffer, &matrixBuffer, sizeof(stMatrixBuffer));

		//colorCube.Render();


		devcon->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_LINELIST);
		devcon->OMSetDepthStencilState(pDepthStateOff, 0);
		if (lineObj.GetVertexCount() > 0)
		{
			matrixBuffer.world = lineObj.GetObjectMatrix(false, true);
			matrixBuffer.view = XMMatrixTranspose(gameWorldMatrix);
			//matrixBuffer.view = XMMatrixTranspose(XMMatrixInverse(0, matrixSet->eyeMatrix * matrixSet->hmdMatrix));
			matrixBuffer.projection = XMMatrixTranspose(matrixSet->projectionMatrix);
			MapResource(pMatrixBuffer, &matrixBuffer, sizeof(stMatrixBuffer));

			lineObj.Render();
		}

	}
}

void BasicRenderer::SaveSettings()
{
	haveSaved = true;
	devcon->OMGetBlendState(&savedBlendState, savedBlendFactor, &savedSampleMask);
	devcon->OMGetDepthStencilState(&savedDepthStencilState, &savedStencilRef);
	devcon->OMGetRenderTargets(1, &savedRenderTargetView, &savedDepthStencilView);
	devcon->PSGetSamplers(0, 1, &savedSampleState);
	devcon->IAGetPrimitiveTopology(&savedPrimitiveTopology);
	devcon->IAGetInputLayout(&savedInputLayout);
	devcon->IAGetVertexBuffers(0, 1, &savedVertexBuffer, &savedVertexStride, &savedVertexOffset);
	devcon->IAGetIndexBuffer(&savedIndexBuffer, &savedIndexFormat, &savedIndexOffset);
	//devcon->RSGetViewports(&savedNumViewports, &savedViewport);
	//devcon->VSGetShader(&savedVertexShader, &savedVertexClassInstance, &savedVertexNumClassInstance);
	//devcon->VSGetConstantBuffers(0, 5, &savedVSBuffer);
	//devcon->PSGetShader(&savedPixelShader, &savedPixelClassInstance, &savedPixelNumClassInstance);
	//devcon->PSGetConstantBuffers(0, 5, &savedPSBuffer);
}

void BasicRenderer::LoadSettings()
{
	if (haveSaved)
	{
		devcon->OMSetBlendState(savedBlendState, savedBlendFactor, savedSampleMask);
		devcon->OMSetDepthStencilState(savedDepthStencilState, savedStencilRef);
		devcon->OMSetRenderTargets(1, &savedRenderTargetView, savedDepthStencilView);
		devcon->PSSetSamplers(0, 1, &savedSampleState);
		devcon->IASetPrimitiveTopology(savedPrimitiveTopology);
		devcon->IASetInputLayout(savedInputLayout);
		devcon->IASetVertexBuffers(0, 1, &savedVertexBuffer, &savedVertexStride, &savedVertexOffset);
		devcon->IASetIndexBuffer(savedIndexBuffer, savedIndexFormat, savedIndexOffset);
		//devcon->RSSetViewports(savedNumViewports, &savedViewport);
		//devcon->VSSetShader(savedVertexShader, &savedVertexClassInstance, savedVertexNumClassInstance);
		//devcon->VSSetConstantBuffers(0, 1, &savedVSBuffer);
		//devcon->PSSetShader(savedPixelShader, &savedPixelClassInstance, savedPixelNumClassInstance);
		//devcon->PSSetConstantBuffers(0, 1, &savedPSBuffer);

		haveSaved = false;
	}
}


void BasicRenderer::SetRenderTarget(ID3D11RenderTargetView* rtv, ID3D11DepthStencilView* dsv)
{
	devcon->OMSetRenderTargets(1, &rtv, dsv);

	devcon->VSSetConstantBuffers(0, 1, &pMatrixBuffer);
	devcon->PSSetConstantBuffers(0, 1, &pMouseBuffer);
	devcon->PSSetSamplers(0, 1, &pSampleState);
}

void BasicRenderer::SetClearColor(ID3D11RenderTargetView* rtv, ID3D11DepthStencilView* dsv, float color[], bool clearDepth)
{
	devcon->ClearRenderTargetView(rtv, color);
	if (clearDepth)
		devcon->ClearDepthStencilView(dsv, D3D11_CLEAR_DEPTH | D3D11_CLEAR_STENCIL, 1.0f, 0);
}

void BasicRenderer::SetBlendIndex(int index)
{
	blendIndex = index;
}

void BasicRenderer::DoRenderRay(D3D11_VIEWPORT viewport, stMatrixSet* matrixSet)
{
	XMMATRIX gameWorldMatrix = matrixSet->gameWorldMatrix * XMMatrixInverse(0, matrixSet->eyeMatrix * matrixSet->hmdMatrix);

	//----
	// Renders the ray
	//----
	matrixBuffer.world = rayLine.GetObjectMatrix(false, true);
	//matrixBuffer.view = XMMatrixTranspose(gameWorldMatrix);
	matrixBuffer.view = XMMatrixTranspose(XMMatrixInverse(0, matrixSet->eyeMatrix * matrixSet->hmdMatrix));
	matrixBuffer.projection = XMMatrixTranspose(matrixSet->projectionMatrix);
	MapResource(pMatrixBuffer, &matrixBuffer, sizeof(stMatrixBuffer));

	devcon->RSSetViewports(1, &viewport);
	devcon->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_LINELIST);
	devcon->OMSetDepthStencilState(pDepthStateOn, 0);
	rayLine.Render();
}

void BasicRenderer::DoRenderLine(D3D11_VIEWPORT viewport, stMatrixSet* matrixSet)
{
	XMMATRIX gameWorldMatrix = matrixSet->gameWorldMatrix * XMMatrixInverse(0, matrixSet->eyeMatrix * matrixSet->hmdMatrix);
	float blendFactor[4] = { 0.f, 0.f, 0.f, 0.f };

	//----
	// Renders other lines
	//----
	if (lineObj.GetVertexCount() > 0)
	{
		matrixBuffer.world = lineObj.GetObjectMatrix(false, true);
		matrixBuffer.view = XMMatrixTranspose(gameWorldMatrix);
		//matrixBuffer.view = XMMatrixTranspose(XMMatrixInverse(0, matrixSet->eyeMatrix * matrixSet->hmdMatrix));
		matrixBuffer.projection = XMMatrixTranspose(matrixSet->projectionMatrix);
		MapResource(pMatrixBuffer, &matrixBuffer, sizeof(stMatrixBuffer));

		devcon->RSSetViewports(1, &viewport);
		devcon->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_LINELIST);
		devcon->OMSetDepthStencilState(pDepthStateOff, 0);
		lineObj.Render();
	}

}

void BasicRenderer::DoRender(D3D11_VIEWPORT viewport, ID3D11ShaderResourceView* srv, stMatrixSet* matrixSet, int blendIndex, bool useDepth, bool isOrthog, bool moveOrthog)
{
	XMMATRIX gameWorldMatrix = matrixSet->gameWorldMatrix * XMMatrixInverse(0, matrixSet->eyeMatrix * matrixSet->hmdMatrix);
	float blendFactor[4] = { 0.f, 0.f, 0.f, 0.f };

	devcon->RSSetViewports(1, &viewport);
	devcon->PSSetShaderResources(0, 1, &srv);
	devcon->OMSetBlendState(pBlendState[blendIndex], blendFactor, 0xffffffff);
	if (isOrthog)
	{
		matrixBuffer.world = XMMatrixIdentity();
		matrixBuffer.view = XMMatrixIdentity();
		matrixBuffer.projection = XMMatrixIdentity();
		MapResource(pMatrixBuffer, &matrixBuffer, sizeof(stMatrixBuffer));

		devcon->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
		devcon->OMSetDepthStencilState((useDepth) ? pDepthStateOn : pDepthStateOff, 0);
		orthogSquare.Render();
	}
	else
	{
		//----
		// Renders the curved UI
		//----
		matrixBuffer.world = curvedUI.GetObjectMatrix(false, true);
		//matrixBuffer.view = XMMatrixTranspose(gameWorldMatrix);
		matrixBuffer.view = XMMatrixTranspose(XMMatrixInverse(0, matrixSet->eyeMatrix * matrixSet->hmdMatrix));
		matrixBuffer.projection = XMMatrixTranspose(matrixSet->projectionMatrix);
		MapResource(pMatrixBuffer, &matrixBuffer, sizeof(stMatrixBuffer));

		devcon->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
		devcon->OMSetDepthStencilState((useDepth) ? pDepthStateOn : pDepthStateOff, 0);
		curvedUI.Render();

		/*
		devcon->OMSetDepthStencilState(pDepthStateOn, 0);
		matrixBuffer.world = colorCube.GetObjectMatrix(false, true);
		//matrixBuffer.view = XMMatrixTranspose(gameWorldMatrix);
		matrixBuffer.view = XMMatrixTranspose(XMMatrixInverse(0, matrixSet->eyeMatrix * matrixSet->hmdMatrix));
		matrixBuffer.projection = XMMatrixTranspose(matrixSet->projectionMatrix);
		MapResource(pMatrixBuffer, &matrixBuffer, sizeof(stMatrixBuffer));
		colorCube.Render();
		*/
	}
}

void BasicRenderer::DoRenderOSK(D3D11_VIEWPORT viewport, ID3D11ShaderResourceView* srv, stMatrixSet* matrixSet, int blendIndex, bool useDepth)
{
	XMMATRIX gameWorldMatrix = matrixSet->gameWorldMatrix * XMMatrixInverse(0, matrixSet->eyeMatrix * matrixSet->hmdMatrix);
	float blendFactor[4] = { 0.f, 0.f, 0.f, 0.f };

	devcon->RSSetViewports(1, &viewport);
	devcon->PSSetShaderResources(0, 1, &srv);
	devcon->OMSetBlendState(pBlendState[blendIndex], blendFactor, 0xffffffff);
	
	//----
	// Renders the curved UI
	//----
	matrixBuffer.world = osk.GetObjectMatrix(false, true);
	//matrixBuffer.view = XMMatrixTranspose(gameWorldMatrix);
	matrixBuffer.view = XMMatrixTranspose(XMMatrixInverse(0, matrixSet->eyeMatrix * matrixSet->hmdMatrix));
	matrixBuffer.projection = XMMatrixTranspose(matrixSet->projectionMatrix);
	MapResource(pMatrixBuffer, &matrixBuffer, sizeof(stMatrixBuffer));

	devcon->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
	devcon->OMSetDepthStencilState((useDepth) ? pDepthStateOn : pDepthStateOff, 0);
	osk.Render();
}



void BasicRenderer::DoRenderWatch(D3D11_VIEWPORT viewport, ID3D11ShaderResourceView* srv[], stMatrixSet* matrixSet, int blendIndex)
{
	XMMATRIX gameWorldMatrix = matrixSet->gameWorldMatrixFloating * XMMatrixInverse(0, matrixSet->eyeMatrix * matrixSet->hmdMatrix);
	float blendFactor[4] = { 0.f, 0.f, 0.f, 0.f };

	devcon->RSSetViewports(1, &viewport);
	devcon->OMSetBlendState(pBlendState[blendIndex], blendFactor, 0xffffffff);

	int x = 0;
	int y = 0;
	for (int i = 0; i < handSquareCount; i++)
	{
		XMMATRIX ScaleMatrix = XMMatrixScaling(0.015f, 0.015f, 0.015f);
		XMMATRIX moveMatrix = XMMatrixTranslation((x * 2) * 0.015f, (y * 2) * 0.015f, 0.02f);
		XMMATRIX rotateMatrix = XMMatrixRotationY(90.0f * ((float)M_PI / 180.0f)) * XMMatrixRotationZ(180.0f * ((float)M_PI / 180.0f));

		handSquare[i].SetObjectMatrix(ScaleMatrix * moveMatrix * rotateMatrix * matrixSet->lhcMatrix);
		//handSquare[i].SetObjectMatrix(moveMatrix * matrixSet->lhcMatrix);

		matrixBuffer.world = handSquare[i].GetObjectMatrix(false, true);
		matrixBuffer.view = XMMatrixTranspose(gameWorldMatrix);
		//matrixBuffer.view = XMMatrixTranspose(XMMatrixInverse(0, matrixSet->eyeMatrix * matrixSet->hmdMatrix));
		matrixBuffer.projection = XMMatrixTranspose(matrixSet->projectionMatrix);
		MapResource(pMatrixBuffer, &matrixBuffer, sizeof(stMatrixBuffer));

		int activeOffset = ((handSquareAtUI[i]) ? 1 : 0);
		devcon->PSSetShaderResources(0, 1, &srv[(i * 2) + activeOffset]);
		devcon->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
		devcon->OMSetDepthStencilState(pDepthStateOn, 0);
		handSquare[i].Render();

		x++;
		if (x >= 3)
		{
			y++;
			x = 0;
		}
	}
}

void BasicRenderer::GetUIStatus(bool* status, int count)
{
	if (count == (handSquareCount + 2))
	{
		for (int i = 0; i < handSquareCount; i++)
			status[i] = handSquareAtUI[i];
		status[handSquareCount + 0] = oskAtUI;
		status[handSquareCount + 1] = curvedUIAtUI;
	}
}

void BasicRenderer::Release()
{
	DestroyBuffers();
	DestroyShaders();

	dev = nullptr;
	devcon = nullptr;
}


bool BasicRenderer::HasErrors()
{
	return ((logError.rdbuf()->in_avail() == 0) ? false : true);
}

std::string BasicRenderer::GetErrors()
{
	std::string curLog = logError.str();
	logError.str("");
	return curLog;
}
