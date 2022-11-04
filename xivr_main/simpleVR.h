#pragma once
#define WIN32_LEAN_AND_MEAN
// Windows Header Files
#include <windows.h>
#include <d3d11_4.h>

#include <openvr.h>
#include <openvr_capi.h>

#include <fstream>
#include "stCommon.h"

class simpleVR
{
	vr::IVRSystem* openVRSession = nullptr;
	vr::IVRChaperone* openVRChaperone = nullptr;
	vr::IVRRenderModels* openVRModels = nullptr;
	vr::TrackedDevicePose_t rTrackedDevicePose[vr::k_unMaxTrackedDeviceCount];
	bool _isConnected = false;
	POINT bufferSize;
	POINT resolution;
	uMatrix projMatrixRaw[3];
	uMatrix eyeViewMatrixRaw[3];
	uMatrix identMatrix;
	uMatrix hmdMatrix;
	uMatrix controllerLeftMatrix;
	uMatrix controllerRightMatrix;
	uMatrix genericMatrix[3];
	int gTrackCount;
	float currentIPD;
	std::ofstream* myfile;

	void InitalizeVR();
	
public:
	simpleVR();
	~simpleVR();
	bool StartVR();
	bool StopVR();
	void Recenter();
	void SetFramePose();
	uMatrix GetFramePose(poseType pose_type, int eye);
	void Render(ID3D11Texture2D* leftEye, ID3D11Texture2D* rightEye);
};
