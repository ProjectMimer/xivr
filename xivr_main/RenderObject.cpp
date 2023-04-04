#include "RenderObject.h"


RenderObject::RenderObject() : dev(nullptr), devcon(nullptr)
{
}

RenderObject::RenderObject(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon) : dev(tdev), devcon(tdevcon)
{
}

bool RenderObject::SetVertexBuffer(std::vector<float> vertices, int itmStride, D3D11_USAGE usage)
{
	vertexList = vertices;
	stride = itmStride;
	byteStride = stride * sizeof(float);
	vertexCount = vertices.size() / stride;
	int byteWidth = vertexCount * byteStride;

	vertexSet = false;
	D3D11_BUFFER_DESC vertexBufferDesc = D3D11_BUFFER_DESC();
	vertexBufferDesc.Usage = D3D11_USAGE_DYNAMIC;
	vertexBufferDesc.ByteWidth = byteWidth;
	vertexBufferDesc.BindFlags = D3D11_BIND_VERTEX_BUFFER;
	vertexBufferDesc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
	vertexBufferDesc.MiscFlags = 0;
	vertexBufferDesc.StructureByteStride = 0;

	D3D11_SUBRESOURCE_DATA initData = D3D11_SUBRESOURCE_DATA();
	initData.pSysMem = &vertices[0];

	HRESULT result = dev->CreateBuffer(&vertexBufferDesc, &initData, &vertexBuffer);
	if (FAILED(result)) {
		return false;
	}

	vertexSet = true;
	return vertexSet;
}

int RenderObject::GetVertexCount()
{
	return vertexCount;
}

bool RenderObject::SetIndexBuffer(std::vector<short> indices, D3D11_USAGE usage)
{
	indexList = indices;
	indexCount = indices.size();
	int byteWidth = indexCount * sizeof(short);
	
	indexSet = false;
	D3D11_BUFFER_DESC indexBufferDesc = D3D11_BUFFER_DESC();
	indexBufferDesc.Usage = D3D11_USAGE_DYNAMIC;
	indexBufferDesc.ByteWidth = byteWidth;
	indexBufferDesc.BindFlags = D3D11_BIND_INDEX_BUFFER;
	indexBufferDesc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
	indexBufferDesc.MiscFlags = 0;
	indexBufferDesc.StructureByteStride = 0;

	D3D11_SUBRESOURCE_DATA initData = D3D11_SUBRESOURCE_DATA();
	initData.pSysMem = &indices[0];

	HRESULT result = dev->CreateBuffer(&indexBufferDesc, &initData, &indexBuffer);
	if (FAILED(result)) {
		return false;
	}

	indexSet = true;
	return indexSet;
}

void RenderObject::SetShadersLayout(ID3D11InputLayout* layout, ID3D11VertexShader* vertex, ID3D11PixelShader* pixel)
{
	structLayout = layout;
	vertexShader = vertex;
	pixelShader = pixel;
}

void RenderObject::MapResource(void* data, int size)
{
	D3D11_MAPPED_SUBRESOURCE mappedResource = D3D11_MAPPED_SUBRESOURCE();
	devcon->Map(vertexBuffer, 0, D3D11_MAP_WRITE_DISCARD, 0, &mappedResource);
	memcpy(mappedResource.pData, data, size);
	devcon->Unmap(vertexBuffer, 0);
}


bool RenderObject::RayIntersection(XMVECTOR origin, XMVECTOR direction, XMVECTOR *intersection, float *distance, std::stringstream* logError)
{
	bool intersected = false;
	//XMMATRIX objMatrixI = XMMatrixInverse(0, objMatrix);
	for (int i = 0; i < indexCount; i += 3)
	{
		int i0 = indexList[i + 0];
		int i1 = indexList[i + 1];
		int i2 = indexList[i + 2];

		XMVECTOR v0 = { vertexList[i0 * stride + 0], vertexList[i0 * stride + 1], vertexList[i0 * stride + 2] };
		XMVECTOR v1 = { vertexList[i1 * stride + 0], vertexList[i1 * stride + 1], vertexList[i1 * stride + 2] };
		XMVECTOR v2 = { vertexList[i2 * stride + 0], vertexList[i2 * stride + 1], vertexList[i2 * stride + 2] };

		XMVECTOR v0uv = { vertexList[i0 * stride + 3], vertexList[i0 * stride + 4], 1.0f };
		XMVECTOR v1uv = { vertexList[i1 * stride + 3], vertexList[i1 * stride + 4], 1.0f };
		XMVECTOR v2uv = { vertexList[i2 * stride + 3], vertexList[i2 * stride + 4], 1.0f };

		v0 = XMVector3Transform(v0, objMatrix);
		v1 = XMVector3Transform(v1, objMatrix);
		v2 = XMVector3Transform(v2, objMatrix);

		float pickU = 0.0f;
		float pickV = 0.0f;
		float pickW = 0.0f;

		bool rayHit = RayTest(origin, direction, v0, v1, v2, &pickU, &pickV, &pickW, distance, logError);
		if (rayHit)
		{
			intersected = true;
			*intersection = pickU * v1uv + pickV * v2uv + pickW * v0uv;
			//(*logError) << pickU << " : " << pickV << " : " << pickW << " : " << (*distance) << " -- " << intersection->m128_f32[0] << ", " <<intersection->m128_f32[1] << ", " << intersection->m128_f32[2] << std::endl;
		}
	}
	return intersected;
}

bool RenderObject::RayTest(XMVECTOR origin, XMVECTOR direction, XMVECTOR v0, XMVECTOR v1, XMVECTOR v2, float* barycentricU, float* barycentricV, float* barycentricW, float* distance, std::stringstream* logError)
{
	XMVECTOR v1v0 = v1 - v0;
	XMVECTOR v2v0 = v2 - v0;
	XMVECTOR vOv0 = origin - v0;

	// Begin calculating determinant - also used to calculate barycentricU parameter
	XMVECTOR pvec = XMVector3Cross(direction, v2v0);

	// If determinant is near zero, ray lies in plane of triangle
	float det = 0;
	DirectX::XMStoreFloat(&det, XMVector3Dot(v1v0, pvec));
	if (det < 0.0001f && det > -0.0001f)
		return false;
	float fInvDet = 1.0f / det;

	// Calculate barycentricU parameter and test bounds
	DirectX::XMStoreFloat(barycentricU, XMVector3Dot(vOv0, pvec) * fInvDet);
	if (*barycentricU < 0.0f || *barycentricU > 1.0f)
		return false;

	// Prepare to test barycentricV parameter
	XMVECTOR qvec = XMVector3Cross(vOv0, v1v0);

	// Calculate barycentricV parameter and test bounds
	DirectX::XMStoreFloat(barycentricV, XMVector3Dot(direction, qvec) * fInvDet);
	if (*barycentricV < 0.0f || (*barycentricU + *barycentricV) > 1.0f)
		return false;

	// Calculate pickDistance
	DirectX::XMStoreFloat(distance, XMVector3Dot(v2v0, qvec) * fInvDet);
	if (*distance > 0)
		return false;
	(*barycentricW) = 1.f - (*barycentricU) - (*barycentricV);

	//(*logError) << det << " : " << fInvDet << " : " << (*barycentricU) << " : " << (*barycentricV) << " : " << (*distance) << std::endl;
	return true;

}

void RenderObject::Render()
{
	unsigned int offset = 0;

	devcon->IASetInputLayout(structLayout);
	devcon->VSSetShader(vertexShader, 0, 0);
	devcon->PSSetShader(pixelShader, 0, 0);
	
	if(vertexSet)
		devcon->IASetVertexBuffers(0, 1, &vertexBuffer, &byteStride, &offset);
	
	if (indexSet)
	{
		devcon->IASetIndexBuffer(indexBuffer, DXGI_FORMAT_R16_UINT, 0);
		devcon->DrawIndexed(indexCount, 0, 0);
	}
	else
	{
		devcon->Draw(vertexCount, 0);
	}
}


void RenderObject::SetObjectMatrix(XMMATRIX matrix)
{
	objMatrix = matrix;
}

XMMATRIX RenderObject::GetObjectMatrix(bool inverse, bool transpose)
{
	if (transpose && inverse)
		return XMMatrixTranspose(XMMatrixInverse(0, objMatrix));
	else if (transpose)
		return XMMatrixTranspose(objMatrix);
	else if (inverse)
		return XMMatrixInverse(0, objMatrix);
	else
		return objMatrix;
}

void RenderObject::Release()
{
	if (vertexBuffer) { vertexBuffer->Release(); vertexBuffer = nullptr; }
	if (indexBuffer) { indexBuffer->Release(); indexBuffer = nullptr; }
	
	if (structLayout) { structLayout = nullptr; }
	if (vertexShader) { vertexShader = nullptr; }
	if (pixelShader) { pixelShader = nullptr; }

	vertexCount = 0;
	indexCount = 0;

	dev = nullptr;
	devcon = nullptr;
}












RenderCurvedUI::RenderCurvedUI(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon) : RenderObject(tdev, tdevcon)
{
	// x,y,z,  u,v
	std::vector<float> vertices = 
	{
		 -0.5000000f, -0.5000000f,  0.1456540f,		0.000016f, 1.000000f,
		 -0.5000000f,  0.5000000f,  0.1456540f,		0.000018f, 0.000000f,
		 -0.4903928f, -0.5000000f,  0.0980761f,		0.040409f, 1.000000f,
		 -0.4903928f,  0.5000000f,  0.0980761f,		0.040410f, 0.000000f,
		 -0.4619398f, -0.5000000f,  0.0523266f,		0.085244f, 1.000000f,
		 -0.4619398f,  0.5000000f,  0.0523266f,		0.085245f, 0.000000f,
		 -0.4157348f, -0.5000000f,  0.0101635f,		0.137297f, 1.000000f,
		 -0.4157348f,  0.5000000f,  0.0101635f,		0.137299f, 0.000000f,
		 -0.3535534f, -0.5000000f, -0.0267926f,		0.197492f, 1.000000f,
		 -0.3535534f,  0.5000000f, -0.0267926f,		0.197494f, 0.000000f,
		 -0.2777852f, -0.5000000f, -0.0571218f,		0.265409f, 1.000000f,
		 -0.2777852f,  0.5000000f, -0.0571218f,		0.265411f, 0.000000f,
		 -0.1913418f, -0.5000000f, -0.0796584f,		0.339750f, 1.000000f,
		 -0.1913418f,  0.5000000f, -0.0796584f,		0.339752f, 0.000000f,
		 -0.0975452f, -0.5000000f, -0.0935364f,		0.418655f, 1.000000f,
		 -0.0975452f,  0.5000000f, -0.0935364f,		0.418658f, 0.000000f,
		  0.0000000f, -0.5000000f, -0.0982224f,		0.499924f, 1.000000f,
		  0.0000000f,  0.5000000f, -0.0982224f,		0.499927f, 0.000000f,
		  0.0975452f, -0.5000000f, -0.0935363f,		0.581193f, 1.000000f,
		  0.0975452f,  0.5000000f, -0.0935363f,		0.581195f, 0.000000f,
		  0.1913418f, -0.5000000f, -0.0796584f,		0.660098f, 1.000000f,
		  0.1913418f,  0.5000000f, -0.0796584f,		0.660101f, 0.000000f,
		  0.2777851f, -0.5000000f, -0.0571218f,		0.734438f, 1.000000f,
		  0.2777851f,  0.5000000f, -0.0571218f,		0.734442f, 0.000000f,
		  0.3535533f, -0.5000000f, -0.0267926f,		0.802356f, 1.000000f,
		  0.3535533f,  0.5000000f, -0.0267926f,		0.802359f, 0.000000f,
		  0.4157349f, -0.5000000f,  0.0101636f,		0.862551f, 1.000000f,
		  0.4157349f,  0.5000000f,  0.0101636f,		0.862554f, 0.000000f,
		  0.4619398f, -0.5000000f,  0.0523266f,		0.914604f, 1.000000f,
		  0.4619398f,  0.5000000f,  0.0523266f,		0.914608f, 0.000000f,
		  0.4903927f, -0.5000000f,  0.0980762f,		0.959439f, 1.000000f,
		  0.4903927f,  0.5000000f,  0.0980762f,		0.959443f, 0.000000f,
		  0.5000000f, -0.5000000f,  0.1456541f,		0.999832f, 1.000000f,
		  0.5000000f,  0.5000000f,  0.1456541f,		0.999835f, 0.000000f,
	};

	std::vector<float> vertices1 =
	{
		 -1, -1, 0,	 1.0f, 0.0f, 0.0f, 1.0f, //	0, 1,
		 -1,  1, 0,	 0.0f, 0.0f, 1.0f, 1.0f, //	0, 0,
		  1,  1, 0,	 0.0f, 1.0f, 0.0f, 1.0f, //	1, 0,
		  1, -1, 0,  1.0f, 1.0f, 1.0f, 1.0f, // 1, 1,
	};

	std::vector<float> vertices2 =
	{
		 -1, -1, 0,		0, 1,
		 -1,  1, 0,		0, 0,
		  1,  1, 0,		1, 0,
		  1, -1, 0,		1, 1,
	};
	SetVertexBuffer(vertices, 5);
	//SetVertexBuffer(vertices1, 7);
	//SetVertexBuffer(vertices2, 5);
	

	std::vector<short> indices =
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
	};

	std::vector<short> indices1 =
	{
		0, 1, 2,
		2, 3, 0
	};
	SetIndexBuffer(indices);
}

RenderSquare::RenderSquare(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon) : RenderObject(tdev, tdevcon)
{
	std::vector<float> vertices = 
	{
		 -1, -1, 0,		0, 1,
		 -1,  1, 0,		0, 0,
		  1,  1, 0,		1, 0,
		  1, -1, 0,		1, 1,
	};
	SetVertexBuffer(vertices, 5);


	std::vector<short> indices =
	{
		0, 1, 2,
		2, 3, 0
	};
	SetIndexBuffer(indices);
}


RenderCube::RenderCube(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon) : RenderObject(tdev, tdevcon)
{
	std::vector<float> vertices =
	{
		 -0.55f, -0.55f, -0.55f,   0.0f, 0.0f, 0.0f, 1.0f,
		 -0.55f,  0.55f, -0.55f,   0.0f, 0.0f, 1.0f, 1.0f,
		  0.55f,  0.55f, -0.55f,   0.0f, 1.0f, 0.0f, 1.0f,
		  0.55f, -0.55f, -0.55f,   0.0f, 1.0f, 1.0f, 1.0f,
		 -0.55f, -0.55f,  0.55f,   1.0f, 0.0f, 0.0f, 1.0f,
		 -0.55f,  0.55f,  0.55f,   1.0f, 0.0f, 1.0f, 1.0f,
		  0.55f,  0.55f,  0.55f,   1.0f, 1.0f, 0.0f, 1.0f,
		  0.55f, -0.55f,  0.55f,   1.0f, 1.0f, 1.0f, 1.0f,
	};
	SetVertexBuffer(vertices, 7);

	//   5----6
	//  /|   /|
	// 1----2 |
	// | 4--|-7
	// |/   |/
	// 0----3
	//

	std::vector<short> indices =
	{
		// front face
		0, 1, 2,
		0, 2, 3,

		// back face
		4, 6, 5,
		4, 7, 6,

		// left face
		4, 5, 1,
		4, 1, 0,

		// right face
		3, 2, 6,
		3, 6, 7,

		// top face
		1, 5, 6,
		1, 6, 2,

		// bottom face
		4, 0, 3,
		4, 3, 7
	};
	SetIndexBuffer(indices);
}


RenderRayLine::RenderRayLine(ID3D11Device* tdev, ID3D11DeviceContext* tdevcon) : RenderObject(tdev, tdevcon)
{
	std::vector<float> vertices =
	{
		0, 0, 1,		1.0f, 0.0f, 0.0f, 0.75f,
		0, 0,-1,		0.0f, 0.0f, 1.0f, 0.25f
	};
	SetVertexBuffer(vertices, 7, D3D11_USAGE_DYNAMIC);
}