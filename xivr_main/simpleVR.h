#pragma once
#define WIN32_LEAN_AND_MEAN
// Windows Header Files
#include <windows.h>
#include <d3d11_4.h>

#include <openvr.h>
#include <openvr_capi.h>

#include "stCommon.h"
#include "clsController.h"

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
	ControllerList controllerID;
	clsController controller;
	
	void InitalizeVR();
	
public:
	simpleVR();
	~simpleVR();
	bool StartVR();
	bool StopVR();
	void Recenter();
	POINT GetBufferSize();
	void SetFramePose();
	uMatrix GetFramePose(poseType pose_type, int eye);
	void Render(ID3D11Texture2D* leftEye, ID3D11Texture2D* rightEye);

	bool GetButtonHasChanged(ButtonList buttonID, ControllerType controllerType);
	bool GetButtonIsTouched(ButtonList buttonID, ControllerType controllerType);
	bool GetButtonIsPressed(ButtonList buttonID, ControllerType controllerType);
	bool GetButtonIsDownFrame(ButtonList buttonID, ControllerType controllerType);
	bool GetButtonIsUpFrame(ButtonList buttonID, ControllerType controllerType);
	float GetButtonValue(ButtonList buttonID, ControllerType controllerType);
};
