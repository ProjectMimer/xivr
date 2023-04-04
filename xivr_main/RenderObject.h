#pragma once
#include <D3D11.h>
#include <DirectXMath.h>
#include <iostream>
#include <vector>
#include <sstream>

using namespace DirectX;

class RenderObject
{
	ID3D11Device* dev;
	ID3D11DeviceContext* devcon;

	ID3D11InputLayout* structLayout = nullptr;
	ID3D11VertexShader* vertexShader = nullptr;
	ID3D11PixelShader* pixelShader = nullptr;
	

	ID3D11Buffer* vertexBuffer = nullptr;
	ID3D11Buffer* indexBuffer = nullptr;

	std::vector<float> vertexList = std::vector<float>();
	std::vector<short> indexList = std::vector<short>();

	XMMATRIX objMatrix = XMMatrixIdentity();
	unsigned int stride = 0;
	unsigned int byteStride = 0;
	int vertexCount = 0;
	int indexCount = 0;

	bool vertexSet = false;
	bool indexSet = false;

public:
	RenderObject();
	RenderObject(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon);

	bool SetVertexBuffer(std::vector<float> vertices, int itmStride, D3D11_USAGE usage = D3D11_USAGE_DEFAULT);
	int GetVertexCount();
	bool SetIndexBuffer(std::vector<short> indices, D3D11_USAGE usage = D3D11_USAGE_DEFAULT);
	void SetShadersLayout(ID3D11InputLayout* layout, ID3D11VertexShader* vertex, ID3D11PixelShader* pixelShader);
	void MapResource(void* data, int size);

	void SetObjectMatrix(DirectX::XMMATRIX matrix);
	XMMATRIX GetObjectMatrix(bool inverse = false, bool transpose = false);

	bool RayIntersection(XMVECTOR origin, XMVECTOR direction, XMVECTOR* intersection, float* distance, std::stringstream *logError);
	bool RayTest(XMVECTOR origin, XMVECTOR direction, XMVECTOR v0, XMVECTOR v1, XMVECTOR v2, float* barycentricU, float* barycentricV, float* barycentricW, float* distance, std::stringstream* logError);

	void Render();

	void Release();
};






class RenderCurvedUI : public RenderObject
{

public:
	RenderCurvedUI(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon);
};


class RenderSquare : public RenderObject
{

public:
	RenderSquare(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon);
};


class RenderCube : public RenderObject
{

public:
	RenderCube(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon);
};


class RenderRayLine : public RenderObject
{

public:
	RenderRayLine(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon);
};
