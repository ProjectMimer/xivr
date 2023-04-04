#pragma once
#define WIN32_LEAN_AND_MEAN
#define _USE_MATH_DEFINES
// Windows Header Files
#include <windows.h>
#include <d3d11_4.h>
#include <DirectXMath.h>
#include <openvr.h>
#include <openvr_capi.h>
#include <iostream>

#include "stCommon.h"
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
	stConfiguration* cfg;
	std::stringstream logError;
	vr::HmdVector2_t depthRange;
	vr::VRTextureBounds_t textureBounds[2];

private:
	void InitalizeVR();

public:
	simpleVR(stConfiguration* config);
	~simpleVR();
	
	bool StartVR();
	bool StopVR();
	bool isEnabled();
	void Recenter();
	POINT GetBufferSize();
	void SetActionPose(vr::HmdMatrix34_t matPose, poseType pose);
	void SetFramePose();
	uMatrix GetFramePose(poseType pose_type, int eye);
	void Render(ID3D11Texture2D* leftEye, ID3D11Texture2D* leftDepth, ID3D11Texture2D* rightEye, ID3D11Texture2D* rightDepth);
	void WaitGetPoses();
	void MakeIPDOffset();
	bool HasErrors();
	std::string GetErrors();
};
