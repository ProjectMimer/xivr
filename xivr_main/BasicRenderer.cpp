#include "BasicRenderer.h"

BasicRenderer::BasicRenderer(Configuration* config) : dev(nullptr), devcon(nullptr), clearColor(), cfg(config)
{
}

BasicRenderer::BasicRenderer(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon) : dev(tdev), devcon(tdevcon), clearColor()
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

bool BasicRenderer::CreateShaders()
{
	HRESULT result = S_OK;
	ID3D10Blob* errorMessage;
	ID3D10Blob* VS;
	ID3D10Blob* PS;

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

	const char* defaultPixelShaderWithMouseDotSrc = R"""(
Texture2D shaderTexture;
SamplerState sampleType;
static const float PI = 3.14159265;
cbuffer mousePos
{
	float2 radius;
	float2 coord;
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
	float2 diff = input.tex - coord;
	float distance = length(diff);
	float4 pixel = float4(0.0, 0.0, 0.0, 0.0);
	if (distance <= radius.x)
	{
		pixel = float4(1.0, 0.0, 0.0, 1.0);
	}
	else
	{
		pixel = tex2Dmultisample(sampleType, input.tex);
	}
	return pixel;
}
	)""";

	// load and compile the two shaders
	result = D3DCompile(defaultVertexShaderWithProjectionSrc, strlen(defaultVertexShaderWithProjectionSrc), 0, 0, 0, "VShader", "vs_4_0", 0, 0, &VS, &errorMessage);
	//result = D3DX11CompileFromFileA("d:\\projects\\Effects.fx", 0, 0, "VShader", "vs_4_0", 0, 0, 0, &VS, &errorMessage, 0);
	if (FAILED(result)) {
		if (errorMessage) {
			//myfile << "VSD: " << errorMessage << std::endl;
			MessageBoxA(0, "Error with VertexShaderData", "Missing Shader File", MB_OK);
			return false;
		}
		else {
			MessageBoxA(0, "Missing Shader file", "Missing Shader File", MB_OK);
			return false;
		}
	}
	result = D3DCompile(defaultPixelShaderWithMouseDotSrc, strlen(defaultPixelShaderWithMouseDotSrc), 0, 0, 0, "PShader", "ps_4_0", 0, 0, &PS, &errorMessage);
	//result = D3DX11CompileFromFileA("d:\\projects\\Effects.fx", 0, 0, "PShader", "ps_4_0", 0, 0, 0, &PS, &errorMessage, 0);
	if (FAILED(result)) {
		if (errorMessage) {
			//myfile << "PSD: " << errorMessage << std::endl;
			MessageBoxA(0, "Error with PixelShaderData", "Missing Shader File", MB_OK);
			return false;
		}
		else {
			MessageBoxA(0, "Missing Shader file", "Missing Shader File", MB_OK);
			return false;
		}
	}

	// encapsulate both shaders into shader objects
	result = dev->CreateVertexShader(VS->GetBufferPointer(), VS->GetBufferSize(), NULL, &pVS);
	if (FAILED(result)) {
		MessageBoxA(0, "Error Creating VertexShader", "Error", MB_OK);
		return false;
	}
	result = dev->CreatePixelShader(PS->GetBufferPointer(), PS->GetBufferSize(), NULL, &pPS);
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
		MessageBoxA(0, "Error creating blend State", "Error", MB_OK);
		return false;
	}

	return true;
}


void BasicRenderer::DestroyShaders()
{
	if (pVS) { pVS->Release(); pVS = nullptr; }
	if (pPS) { pPS->Release(); pPS = nullptr; }
	if (pLayout) { pLayout->Release(); pLayout = nullptr; }
	if (pSampleState) { pSampleState->Release(); pSampleState = nullptr; }
	if (pBlendState[0]) { pBlendState[0]->Release(); pBlendState[0] = nullptr; }
	if (pBlendState[1]) { pBlendState[1]->Release(); pBlendState[1] = nullptr; }
}

bool BasicRenderer::CreateBuffers()
{
	HRESULT result = S_OK;

	VertexType cvertices[] =
	{
		{ -1, -1, 0, 0, 1 },
		{ -1,  1, 0, 0, 0 },
		{  1,  1, 0, 1, 0 },
		{  1, -1, 0, 1, 1 },
	};
	int numPoints = ARRAYSIZE(cvertices);

	D3D11_BUFFER_DESC vertexBufferDesc;
	ZeroMemory(&vertexBufferDesc, sizeof(D3D11_BUFFER_DESC));
	vertexBufferDesc.Usage = D3D11_USAGE_DYNAMIC;
	vertexBufferDesc.ByteWidth = sizeof(VertexType) * numPoints;
	vertexBufferDesc.BindFlags = D3D11_BIND_VERTEX_BUFFER;
	vertexBufferDesc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
	vertexBufferDesc.MiscFlags = 0;
	vertexBufferDesc.StructureByteStride = 0;
	result = dev->CreateBuffer(&vertexBufferDesc, NULL, &pCVBuffer);
	if (FAILED(result)) {
		MessageBoxA(0, "Error creating vertex buffer", "Error", MB_OK);
		return false;
	}
	MapResource(pCVBuffer, cvertices, sizeof(cvertices));

	short cindices[] =
	{
		0, 1, 2,
		2, 3, 0
	};
	numPoints = ARRAYSIZE(cindices);

	D3D11_BUFFER_DESC indexBufferDesc;
	ZeroMemory(&indexBufferDesc, sizeof(D3D11_BUFFER_DESC));
	indexBufferDesc.Usage = D3D11_USAGE_DYNAMIC;
	indexBufferDesc.ByteWidth = sizeof(short) * numPoints;
	indexBufferDesc.BindFlags = D3D11_BIND_INDEX_BUFFER;
	indexBufferDesc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
	indexBufferDesc.MiscFlags = 0;
	indexBufferDesc.StructureByteStride = 0;
	result = dev->CreateBuffer(&indexBufferDesc, NULL, &pCIBuffer);
	if (FAILED(result)) {
		MessageBoxA(0, "Error creating index buffer", "Error", MB_OK);
		return false;
	}
	MapResource(pCIBuffer, cindices, sizeof(cindices));


	VertexType vertices[] =
	{
		{ -5.000000, -5.000000,  1.456540, 0.000016, 1.000000 },
		{ -5.000000,  5.000000,  1.456540, 0.000018, 0.000000 },
		{ -4.903928, -5.000000,  0.980761, 0.040409, 1.000000 },
		{ -4.903928,  5.000000,  0.980761, 0.040410, 0.000000 },
		{ -4.619398, -5.000000,  0.523266, 0.085244, 1.000000 },
		{ -4.619398,  5.000000,  0.523266, 0.085245, 0.000000 },
		{ -4.157348, -5.000000,  0.101635, 0.137297, 1.000000 },
		{ -4.157348,  5.000000,  0.101635, 0.137299, 0.000000 },
		{ -3.535534, -5.000000, -0.267926, 0.197492, 1.000000 },
		{ -3.535534,  5.000000, -0.267926, 0.197494, 0.000000 },
		{ -2.777852, -5.000000, -0.571218, 0.265409, 1.000000 },
		{ -2.777852,  5.000000, -0.571218, 0.265411, 0.000000 },
		{ -1.913418, -5.000000, -0.796584, 0.339750, 1.000000 },
		{ -1.913418,  5.000000, -0.796584, 0.339752, 0.000000 },
		{ -0.975452, -5.000000, -0.935364, 0.418655, 1.000000 },
		{ -0.975452,  5.000000, -0.935364, 0.418658, 0.000000 },
		{  0.000000, -5.000000, -0.982224, 0.499924, 1.000000 },
		{  0.000000,  5.000000, -0.982224, 0.499927, 0.000000 },
		{  0.975452, -5.000000, -0.935363, 0.581193, 1.000000 },
		{  0.975452,  5.000000, -0.935363, 0.581195, 0.000000 },
		{  1.913418, -5.000000, -0.796584, 0.660098, 1.000000 },
		{  1.913418,  5.000000, -0.796584, 0.660101, 0.000000 },
		{  2.777851, -5.000000, -0.571218, 0.734438, 1.000000 },
		{  2.777851,  5.000000, -0.571218, 0.734442, 0.000000 },
		{  3.535533, -5.000000, -0.267926, 0.802356, 1.000000 },
		{  3.535533,  5.000000, -0.267926, 0.802359, 0.000000 },
		{  4.157349, -5.000000,  0.101636, 0.862551, 1.000000 },
		{  4.157349,  5.000000,  0.101636, 0.862554, 0.000000 },
		{  4.619398, -5.000000,  0.523266, 0.914604, 1.000000 },
		{  4.619398,  5.000000,  0.523266, 0.914608, 0.000000 },
		{  4.903927, -5.000000,  0.980762, 0.959439, 1.000000 },
		{  4.903927,  5.000000,  0.980762, 0.959443, 0.000000 },
		{  5.000000, -5.000000,  1.456541, 0.999832, 1.000000 },
		{  5.000000,  5.000000,  1.456541, 0.999835, 0.000000 },
	};

	numPoints = ARRAYSIZE(vertices);

	ZeroMemory(&vertexBufferDesc, sizeof(D3D11_BUFFER_DESC));
	vertexBufferDesc.Usage = D3D11_USAGE_DYNAMIC;
	vertexBufferDesc.ByteWidth = sizeof(VertexType) * numPoints;
	vertexBufferDesc.BindFlags = D3D11_BIND_VERTEX_BUFFER;
	vertexBufferDesc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
	vertexBufferDesc.MiscFlags = 0;
	vertexBufferDesc.StructureByteStride = 0;
	result = dev->CreateBuffer(&vertexBufferDesc, NULL, &pVBuffer);
	if (FAILED(result)) {
		MessageBoxA(0, "Error creating vertex buffer", "Error", MB_OK);
		return false;
	}
	MapResource(pVBuffer, vertices, sizeof(vertices));

	short indices[] =
	{
		  0,  1,  3,
		  3,  2,  0,
		  2,  3,  5,
		  5,  4,  2,
		  4,  5,  7,
		  7,  6,  4,
		  6,  7,  9,
		  9,  8,  6,
		  8,  9, 11,
		 11, 10,  8,
		 10, 11, 13,
		 13, 12, 10,
		 12, 13, 15,
		 15, 14, 12,
		 14, 15, 17,
		 17, 16, 14,
		 16, 17, 19,
		 19, 18, 16,
		 18, 19, 21,
		 21, 20, 18,
		 20, 21, 23,
		 23, 22, 20,
		 22, 23, 25,
		 25, 24, 22,
		 24, 25, 27,
		 27, 26, 24,
		 26, 27, 29,
		 29, 28, 26,
		 28, 29, 31,
		 31, 30, 28,
		 30, 31, 33,
		 33, 32, 30,
		 32, 33, 35,
		 35, 34, 32
	};

	numPoints = ARRAYSIZE(indices);

	ZeroMemory(&indexBufferDesc, sizeof(D3D11_BUFFER_DESC));
	indexBufferDesc.Usage = D3D11_USAGE_DYNAMIC;
	indexBufferDesc.ByteWidth = sizeof(short) * numPoints;
	indexBufferDesc.BindFlags = D3D11_BIND_INDEX_BUFFER;
	indexBufferDesc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
	indexBufferDesc.MiscFlags = 0;
	indexBufferDesc.StructureByteStride = 0;
	result = dev->CreateBuffer(&indexBufferDesc, NULL, &pIBuffer);
	if (FAILED(result)) {
		MessageBoxA(0, "Error creating index buffer", "Error", MB_OK);
		return false;
	}
	MapResource(pIBuffer, indices, sizeof(indices));


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
	if (pVBuffer) { pVBuffer->Release(); pVBuffer = nullptr; }
	if (pIBuffer) { pIBuffer->Release(); pIBuffer = nullptr; }
	if (pMatrixBuffer) { pMatrixBuffer->Release(); pMatrixBuffer = nullptr; }
	if (pMouseBuffer) { pMouseBuffer->Release(); pMouseBuffer = nullptr; }
}

void BasicRenderer::MapResource(ID3D11Buffer* buffer, void* data, int size)
{
	D3D11_MAPPED_SUBRESOURCE mappedResource = D3D11_MAPPED_SUBRESOURCE();
	devcon->Map(buffer, 0, D3D11_MAP_WRITE_DISCARD, 0, &mappedResource);
	memcpy(mappedResource.pData, data, size);
	devcon->Unmap(buffer, 0);
}

void BasicRenderer::SetClearColor(float color[])
{
	clearColor[0] = color[0];
	clearColor[1] = color[1];
	clearColor[2] = color[2];
	clearColor[3] = color[3];
	doClear = true;
}

void BasicRenderer::SetBlendIndex(int index)
{
	blendIndex = index;
}

void BasicRenderer::SetMousePosition(HWND hwnd, int width, int height)
{
	POINT p;
	RECT rect;
	if (GetCursorPos(&p))
	{
		if (hwnd && ScreenToClient(hwnd, &p) && GetWindowRect(hwnd, &rect))
		{
			mouseBuffer.radius.x = 0.0025f;
			mouseBuffer.radius.y = 0.0f;
			mouseBuffer.coords.x = p.x / (float)width;
			mouseBuffer.coords.y = p.y / (float)height;
			MapResource(pMouseBuffer, &mouseBuffer, sizeof(stMouseBuffer));
		}
	}
}

void BasicRenderer::DoRender(D3D11_VIEWPORT viewport, ID3D11RenderTargetView* rtv, ID3D11ShaderResourceView* srv, DirectX::XMMATRIX projectionMatrix, DirectX::XMMATRIX viewMatrix, bool isOrthog)
{
	UINT Stride = sizeof(VertexType);
	UINT Offset = 0;

	if (doClear)
	{
		doClear = false;
		devcon->ClearRenderTargetView(rtv, clearColor);
	}

	float aspect = viewport.Width / viewport.Height;

	FLOAT blendFactor[4] = { 0.f, 0.f, 0.f, 0.f };
	devcon->OMSetBlendState(pBlendState[blendIndex], blendFactor, 1);
	blendIndex = 0;

	devcon->OMSetRenderTargets(1, &rtv, NULL);
	devcon->RSSetViewports(1, &viewport);
	devcon->IASetInputLayout(pLayout);
	devcon->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);

	devcon->VSSetShader(pVS, 0, 0);
	devcon->VSSetConstantBuffers(0, 1, &pMatrixBuffer);

	devcon->PSSetShader(pPS, 0, 0);
	devcon->PSSetShaderResources(0, 1, &srv);
	devcon->PSSetSamplers(0, 1, &pSampleState);
	devcon->PSSetConstantBuffers(0, 1, &pMouseBuffer);

	if (isOrthog)
	{
		matrixBuffer.world = DirectX::XMMatrixIdentity();
		matrixBuffer.view = DirectX::XMMatrixIdentity();
		matrixBuffer.projection = DirectX::XMMatrixIdentity();
		MapResource(pMatrixBuffer, &matrixBuffer, sizeof(stMatrixBuffer));

		devcon->IASetVertexBuffers(0, 1, &pCVBuffer, &Stride, &Offset);
		devcon->IASetIndexBuffer(pCIBuffer, DXGI_FORMAT_R16_UINT, 0);

		devcon->DrawIndexed(6, 0, 0);
	}
	else
	{
		DirectX::XMMATRIX uiScaleMatrix = DirectX::XMMatrixScaling(cfg->uiOffsetScale, cfg->uiOffsetScale, cfg->uiOffsetScale);
		DirectX::XMMATRIX uiZMatrix = DirectX::XMMatrixTranslation(0.0f, 0.0f, (cfg->uiOffsetZ / 100.0f));

		DirectX::XMMATRIX scaleMatrix = DirectX::XMMatrixScaling(0.125f * aspect, 0.125f, 0.125f);
		DirectX::XMMATRIX moveMatrix = DirectX::XMMatrixTranslation(0, -1.0f, -8.0f);
		DirectX::XMMATRIX worldMatrix = moveMatrix * scaleMatrix * uiZMatrix * uiScaleMatrix;
		worldMatrix = DirectX::XMMatrixTranspose(worldMatrix);

		matrixBuffer.world = worldMatrix;
		matrixBuffer.view = viewMatrix;
		matrixBuffer.projection = projectionMatrix;
		MapResource(pMatrixBuffer, &matrixBuffer, sizeof(stMatrixBuffer));

		devcon->IASetVertexBuffers(0, 1, &pVBuffer, &Stride, &Offset);
		devcon->IASetIndexBuffer(pIBuffer, DXGI_FORMAT_R16_UINT, 0);
		devcon->DrawIndexed(102, 0, 0);
	}
}

void BasicRenderer::Release()
{
	DestroyBuffers();
	DestroyShaders();

	dev = nullptr;
	devcon = nullptr;
}
