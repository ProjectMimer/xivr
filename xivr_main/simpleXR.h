#pragma once
#define WIN32_LEAN_AND_MEAN
#define _USE_MATH_DEFINES
// Windows Header Files
#include <windows.h>
#include <d3d11_4.h>
#include "Configuration.h"
#include <sstream>
#include <vector>
#include "stCommon.h"
#include <directxmath.h>

// Tell OpenXR what platform code we'll be using
#define XR_USE_PLATFORM_WIN32
#define XR_USE_GRAPHICS_API_D3D11

#include <openxr/openxr.h>
#include <openxr/openxr_platform.h>

using namespace DirectX;

struct swapchain_surfdata_t {
	ID3D11DepthStencilView* depth_view;
	ID3D11RenderTargetView* target_view;
};

struct swapchain_t {
	XrSwapchain handle;
	int32_t     width;
	int32_t     height;
	std::vector<XrSwapchainImageD3D11KHR> surface_images;
	std::vector<swapchain_surfdata_t>     surface_data;
};

struct input_state_t {
	XrActionSet actionSet;
	XrAction    poseAction;
	XrAction    selectAction;
	XrPath   handSubactionPath[2];
	XrSpace  handSpace[2];
	XrPosef  handPose[2];
	XrBool32 renderHand[2];
	XrBool32 handSelect[2];
};

class simpleXR
{
	XrInstance m_instance{ XR_NULL_HANDLE };
	XrSession m_session{ XR_NULL_HANDLE };
	XrSystemId m_system_id = XR_NULL_SYSTEM_ID;
	XrSpace m_app_space = {};
	XrSessionState m_session_state = XR_SESSION_STATE_UNKNOWN;
	input_state_t  m_input = { };

	// Function pointers for some OpenXR extension methods we'll use.
	PFN_xrGetD3D11GraphicsRequirementsKHR ext_xrGetD3D11GraphicsRequirementsKHR = nullptr;
	PFN_xrCreateDebugUtilsMessengerEXT    ext_xrCreateDebugUtilsMessengerEXT = nullptr;
	PFN_xrDestroyDebugUtilsMessengerEXT   ext_xrDestroyDebugUtilsMessengerEXT = nullptr;

	XrFormFactor            app_config_form = XR_FORM_FACTOR_HEAD_MOUNTED_DISPLAY;
	XrViewConfigurationType app_config_view = XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO;

	XrEnvironmentBlendMode   m_blend = {};
	XrDebugUtilsMessengerEXT m_debug = {};

	const XrPosef  m_pose_identity = { {0,0,0,1}, {0,0,0} };
	std::vector<XrView> m_views;
	std::vector<XrViewConfigurationView> m_config_views;
	std::vector<swapchain_t> m_swapchains;

	DXGI_FORMAT swapchain_format = DXGI_FORMAT_R8G8B8A8_UNORM_SRGB;

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

	stConfiguration* cfg;
	std::stringstream logError;

private:
	void InitalizeVR();
	swapchain_surfdata_t d3d_make_surface_data(ID3D11Device* device, XrBaseInStructure& swapchain_img);
	void d3d_swapchain_destroy(swapchain_t& swapchain);

public:
	simpleXR(stConfiguration* config);
	~simpleXR();

	XrInstance GetInstance();
	XrSession GetSession();
	XrSpace GetSpace();
	XrSessionState GetSessionState();
	XrEnvironmentBlendMode GetBlend();
	std::vector<swapchain_t> GetSwapChain();
	std::vector<XrView> GetViews();
	input_state_t GetInput();


	bool StartVR(ID3D11Device* device);
	bool StopVR();
	bool isEnabled();
	void Recenter();
	POINT GetBufferSize();
	uMatrix GetFramePose(poseType pose_type, int eye);
	void PollFrameEvents(bool& exit);
	void SetFramePose();
	void Render(void (*callback)(int, XrCompositionLayerProjectionView&, swapchain_surfdata_t&));
	void WaitGetPoses(XrTime predictedTime);
	void SetActions();
	bool HasErrors();
	std::string GetErrors();


	/*
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
	*/
};
