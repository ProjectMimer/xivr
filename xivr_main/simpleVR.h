#pragma once
#define WIN32_LEAN_AND_MEAN
// Windows Header Files
#include <windows.h>
#include <d3d11_4.h>
#include <DirectXMath.h>
#include <openvr.h>
#include <openvr_capi.h>

#include "stCommon.h"
#include "clsController.h"
#include "Configuration.h"

class simpleVR
{
	vr::IVRSystem* openVRSession = nullptr;
	vr::IVRChaperone* openVRChaperone = nullptr;
	vr::IVRRenderModels* openVRModels = nullptr;
	vr::TrackedDevicePose_t rTrackedDevicePose[vr::k_unMaxTrackedDeviceCount];
	bool _isConnected = false;
	POINT bufferSize;
	POINT resolution;
	uMatrix projMatrixRaw[2];
	uMatrix eyeViewMatrixRaw[2];
	uMatrix eyeViewMatrix[2];
	uMatrix identMatrix;
	uMatrix hmdMatrix;
	uMatrix controllerLeftMatrix;
	uMatrix controllerRightMatrix;
	uMatrix genericMatrix[3];
	int gTrackCount;
	float currentIPD;
	ControllerList controllerID;
	clsController controller;
	Configuration* cfg;

	void InitalizeVR();

public:
	simpleVR(Configuration* config);
	~simpleVR();
	bool StartVR();
	bool StopVR();
	bool isEnabled();
	void Recenter();
	POINT GetBufferSize();
	void SetFramePose();
	uMatrix GetFramePose(poseType pose_type, int eye);
	void Render(ID3D11Texture2D* leftEye, ID3D11Texture2D* rightEye);
	void WaitGetPoses();
	void MakeIPDOffset();
};
