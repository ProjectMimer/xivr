#include "simpleVR.h"


simpleVR::simpleVR(Configuration* config) : cfg(config)
{
	InitalizeVR();
}

simpleVR::~simpleVR()
{
}

void simpleVR::InitalizeVR()
{
	float tIdentMat[4][4] = {
						{1, 0, 0, 0},
						{0, 1, 0, 0},
						{0, 0, 1, 0},
						{0, 0, 0, 1}
	};

	//----
	// Sets a default identity matrix, and sets all other matrices to it
	//----
	memcpy(&identMatrix, tIdentMat, sizeof(uMatrix));
	memcpy(&projMatrixRaw[0], identMatrix._m, sizeof(uMatrix));
	memcpy(&projMatrixRaw[1], identMatrix._m, sizeof(uMatrix));
	memcpy(&eyeViewMatrixRaw[0], identMatrix._m, sizeof(uMatrix));
	memcpy(&eyeViewMatrixRaw[1], identMatrix._m, sizeof(uMatrix));
	memcpy(&eyeViewMatrix[0], identMatrix._m, sizeof(uMatrix));
	memcpy(&eyeViewMatrix[1], identMatrix._m, sizeof(uMatrix));
	memcpy(&hmdMatrix, identMatrix._m, sizeof(uMatrix));
	memcpy(&controllerLeftMatrix, identMatrix._m, sizeof(uMatrix));
	memcpy(&controllerRightMatrix, identMatrix._m, sizeof(uMatrix));

	controllerID = ControllerID::NA;
	controller = clsController();
}


bool simpleVR::StartVR()
{
	if (!vr::VR_IsHmdPresent())
	{
		InitalizeVR();
		_isConnected = false;
		return _isConnected;
	}

	if (_isConnected == false)
	{
		vr::EVRInitError eError = vr::VRInitError_None;
		openVRSession = vr::VR_Init(&eError, vr::VRApplication_Scene);
		openVRChaperone = vr::VRChaperone();
		openVRModels = (vr::IVRRenderModels*)vr::VR_GetGenericInterface(vr::IVRRenderModels_Version, &eError);
		vr::VRCompositor()->SetTrackingSpace(vr::TrackingUniverseSeated);

		//----
		// Gets the left and right eye projection matricies
		//----
		vr::HmdMatrix44_t pMat[] = {
					openVRSession->GetProjectionMatrix(vr::Eye_Left, 0.001f, 1000.0f),
					openVRSession->GetProjectionMatrix(vr::Eye_Right, 0.001f, 1000.0f)
		};
		memcpy(&projMatrixRaw[0], &pMat[0].m, sizeof(float) * 4 * 4);
		memcpy(&projMatrixRaw[1], &pMat[1].m, sizeof(float) * 4 * 4);

		vr::HmdMatrix34_t pView[] = {
					openVRSession->GetEyeToHeadTransform(vr::Eye_Left),
					openVRSession->GetEyeToHeadTransform(vr::Eye_Right)
		};

		//----
		// Gets the left and right eye offset view matricies
		//----
		float eyeView[][16] = {
			{
				pView[0].m[0][0], pView[0].m[1][0], pView[0].m[2][0], 0.0f,
				pView[0].m[0][1], pView[0].m[1][1], pView[0].m[2][1], 0.0f,
				pView[0].m[0][2], pView[0].m[1][2], pView[0].m[2][2], 0.0f,
				pView[0].m[0][3], pView[0].m[1][3], pView[0].m[2][3], 1.0f
			},
			{
				pView[1].m[0][0], pView[1].m[1][0], pView[1].m[2][0], 0.0f,
				pView[1].m[0][1], pView[1].m[1][1], pView[1].m[2][1], 0.0f,
				pView[1].m[0][2], pView[1].m[1][2], pView[1].m[2][2], 0.0f,
				pView[1].m[0][3], pView[1].m[1][3], pView[1].m[2][3], 1.0f
			}
		};

		memcpy(&eyeViewMatrixRaw[0], &eyeView[0], sizeof(uMatrix));
		memcpy(&eyeViewMatrixRaw[1], &eyeView[1], sizeof(uMatrix));

		memcpy(&eyeViewMatrix[0], &eyeViewMatrixRaw[0], sizeof(uMatrix));
		memcpy(&eyeViewMatrix[1], &eyeViewMatrixRaw[1], sizeof(uMatrix));

		//----
		// Gets the buffer and resolution sizes
		//----
		int32_t rX = 0;
		int32_t rY = 0;
		uint32_t rWidth = 0;
		uint32_t rHeight = 0;

		vr::IVRExtendedDisplay* d = vr::VRExtendedDisplay();
		d->GetWindowBounds(&rX, &rY, &rWidth, &rHeight);
		resolution.x = rWidth;
		resolution.y = rHeight;

		openVRSession->GetRecommendedRenderTargetSize(&rWidth, &rHeight);
		bufferSize.x = rWidth;
		bufferSize.y = rHeight;

		controller.Set(sdkType::openvr, ControllerList::openvrVive);

		_isConnected = true;
	}
	
	Recenter();

	return _isConnected;
}

bool simpleVR::StopVR()
{
	if (_isConnected == true)
	{
		_isConnected = false;
		vr::VR_Shutdown();
		openVRSession = nullptr;

		return true;
	}
	else
	{
		return false;
	}
}

bool simpleVR::isEnabled()
{
	return _isConnected;
}

void simpleVR::Recenter()
{
	if (openVRSession && openVRChaperone)
		openVRChaperone->ResetZeroPose(vr::ETrackingUniverseOrigin::TrackingUniverseSeated);
}

POINT simpleVR::GetBufferSize()
{
	return resolution;
}

void simpleVR::SetFramePose()
{
	if (openVRSession && _isConnected)
	{
		static int lContNum = 0;
		static int rContNum = 0;

		vr::ETrackingUniverseOrigin universeOrigin = vr::VRCompositor()->GetTrackingSpace();
		//vr::VRCompositor()->WaitGetPoses(rTrackedDevicePose, vr::k_unMaxTrackedDeviceCount, NULL, 0);
		vr::VRCompositor()->GetLastPoses(rTrackedDevicePose, vr::k_unMaxTrackedDeviceCount, NULL, 0);

		vr::TrackedDevicePose_t hmdPose = vr::TrackedDevicePose_t();
		vr::TrackedDevicePose_t controllerPose[2] = { 0, 0 };
		vr::TrackedDevicePose_t GenericTracker[3] = { 0, 0, 0 };
		int gTrackIndex = 0;

		//----
		// Loops though all (MaxDeviceCount(16)) available positions
		//----
		for (uint32_t i = 0; i < vr::k_unMaxTrackedDeviceCount; i++) {
			//----
			// Determines if there is a device connected to this index
			//----
			if (rTrackedDevicePose[i].bDeviceIsConnected) {
				//----
				// If there is a device connected, check and see if it is tracking and we have a valid pose
				//----
				if (rTrackedDevicePose[i].bPoseIsValid) {
					//----
					// Get the pose of the HMD itself
					//----
					vr::ETrackedDeviceClass classType = openVRSession->GetTrackedDeviceClass(i);
					if (classType == vr::TrackedDeviceClass_HMD) {
						hmdPose = rTrackedDevicePose[i];

						//----
						// Get the pose of the controllers
						//----
					}
					else if (classType == vr::TrackedDeviceClass_Controller) {
						//----
						// Check and see if we are dealing with the left or right controller
						//----
						vr::ETrackedControllerRole controllerRole = openVRSession->GetControllerRoleForTrackedDeviceIndex(i);
						if (controllerRole == vr::TrackedControllerRole_LeftHand) {
							controllerPose[0] = rTrackedDevicePose[i];

							//----
							// Gets the controller button information
							//----
							vr::VRControllerState_t cState;
							ZeroMemory(&cState, sizeof(vr::VRControllerState_t));
							if (openVRSession->GetControllerState(i, &cState, sizeof(vr::VRControllerState_t))) {
								controller.SetTracking(controllerRole, cState);
							}

						}
						else if (controllerRole == vr::TrackedControllerRole_RightHand) {
							controllerPose[1] = rTrackedDevicePose[i];

							//----
							// Gets the controller button information
							//----
							vr::VRControllerState_t cState;
							ZeroMemory(&cState, sizeof(vr::VRControllerState_t));
							if (openVRSession->GetControllerState(i, &cState, sizeof(vr::VRControllerState_t))) {
								controller.SetTracking(controllerRole, cState);
							}
						}
					}
					else if (classType == vr::TrackedDeviceClass_GenericTracker) {
						if (gTrackIndex < gTrackCount) {
							GenericTracker[gTrackIndex] = rTrackedDevicePose[i];
							gTrackIndex++;
						}
					}
				}
			}
		}

		vr::HmdMatrix34_t matPose;
		//----
		// Convert the HMD Pose into 4x4 Matrix
		//----
		if (hmdPose.bPoseIsValid) {
			matPose = hmdPose.mDeviceToAbsoluteTracking;
			float hMatrix[4][4] = {
				matPose.m[0][0], matPose.m[1][0], matPose.m[2][0], 0.0f,
				matPose.m[0][1], matPose.m[1][1], matPose.m[2][1], 0.0f,
				matPose.m[0][2], matPose.m[1][2], matPose.m[2][2], 0.0f,
				matPose.m[0][3], matPose.m[1][3], matPose.m[2][3], 1.0f
			};
			memcpy(hmdMatrix.matrix, hMatrix, sizeof(float) * 4 * 4);
		}

		//----
		// Convert the Left Controller Pose into 4x4 Matrix
		//----
		if (controllerPose[0].bPoseIsValid) {
			matPose = controllerPose[0].mDeviceToAbsoluteTracking;
			float lcMatrix[] = {
				matPose.m[0][0], matPose.m[1][0], matPose.m[2][0], 0.0f,
				matPose.m[0][1], matPose.m[1][1], matPose.m[2][1], 0.0f,
				matPose.m[0][2], matPose.m[1][2], matPose.m[2][2], 0.0f,
				matPose.m[0][3], matPose.m[1][3], matPose.m[2][3], 1.0f
			};
			memcpy(controllerLeftMatrix.matrix, lcMatrix, sizeof(float) * 4 * 4);
		}

		//----
		// Convert the Right Controller Pose into 4x4 Matrix
		//----
		if (controllerPose[1].bPoseIsValid) {
			matPose = controllerPose[1].mDeviceToAbsoluteTracking;
			float rcMatrix[] = {
				matPose.m[0][0], matPose.m[1][0], matPose.m[2][0], 0.0f,
				matPose.m[0][1], matPose.m[1][1], matPose.m[2][1], 0.0f,
				matPose.m[0][2], matPose.m[1][2], matPose.m[2][2], 0.0f,
				matPose.m[0][3], matPose.m[1][3], matPose.m[2][3], 1.0f
			};
			memcpy(controllerRightMatrix.matrix, rcMatrix, sizeof(float) * 4 * 4);
		}

		controller.SetTracking();
	}
}

uMatrix simpleVR::GetFramePose(poseType pose_type, int eye)
{
	switch (pose_type)
	{
	case poseType::Projection:
		return projMatrixRaw[eye];
		break;
	case poseType::EyeOffset:
		return eyeViewMatrix[eye];
		break;
	case poseType::hmdPosition:
		return hmdMatrix;
		break;
	case poseType::LeftHand:
		return controllerLeftMatrix;
		break;
	case poseType::RightHand:
		return controllerRightMatrix;
		break;
	default:
		return identMatrix;
		break;
	}
}

void simpleVR::Render(ID3D11Texture2D* leftEye, ID3D11Texture2D* rightEye)
{
	if (openVRSession && _isConnected)
	{
		float colorA[] = { 0, 1, 0, 1 };

		vr::Texture_t completeTexture[] = {
					{ leftEye, vr::TextureType_DirectX, vr::ColorSpace_Gamma},
					{ rightEye, vr::TextureType_DirectX, vr::ColorSpace_Gamma},
		};

		vr::VRTextureBounds_t _bound = { 0.0f, 0.0f,  1.0f, 1.0f };
		vr::VRTextureBounds_t _leftbound = { 0.0f, 0.0f,  0.5f, 1.0f };
		vr::VRTextureBounds_t _rightbound = { 0.5f, 0.0f,  1.0f, 1.0f };

		WaitGetPoses();

		vr::EVRCompositorError error = vr::VRCompositorError_None;
		error = vr::VRCompositor()->Submit(vr::Eye_Left, &completeTexture[0], &_bound, vr::Submit_Default);
		if (error) {
			int a = 1;
		}

		error = vr::VRCompositor()->Submit(vr::Eye_Right, &completeTexture[1], &_bound, vr::Submit_Default);
		if (error) {
			int a = 1;
		}

		SetFramePose();
	}
}

void simpleVR::WaitGetPoses()
{
	vr::VRCompositor()->WaitGetPoses(rTrackedDevicePose, vr::k_unMaxTrackedDeviceCount, NULL, 0);
}

void simpleVR::MakeIPDOffset()
{
	memcpy(&eyeViewMatrix[0], &eyeViewMatrixRaw[0], sizeof(uMatrix));
	memcpy(&eyeViewMatrix[1], &eyeViewMatrixRaw[1], sizeof(uMatrix));

	eyeViewMatrix[0]._m[12] += -cfg->ipdOffset;
	eyeViewMatrix[1]._m[12] += +cfg->ipdOffset;
}