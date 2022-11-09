#include "clsController.h"


clsControllerSteamVR::clsControllerSteamVR() : lContNum(0),
rContNum(0)
{

}

clsControllerSteamVR::~clsControllerSteamVR()
{

}

void clsControllerSteamVR::SetTracking(vr::ETrackedControllerRole controllerRole, vr::VRControllerState_t cState)
{
	//----
	// Gets the controller button information
	//----
	if (controllerRole == vr::TrackedControllerRole_LeftHand) {
		if (lContNum != cState.unPacketNum) {
			lContNum = cState.unPacketNum;
			controllerLayout[ButtonsList::left_Menu].Set(false, false, 0.0);
			controllerLayout[ButtonsList::left_ButtonA].SetMinMax(0x80, cState.ulButtonTouched, cState.ulButtonPressed, 1.0f, 0.0f);
			controllerLayout[ButtonsList::left_ButtonB].SetMinMax(0x02, cState.ulButtonTouched, cState.ulButtonPressed, 1.0f, 0.0f);
			controllerLayout[ButtonsList::left_Pad].SetMinMax(0x100000000, cState.ulButtonTouched, cState.ulButtonPressed, 1.0f, 0.0f);
			controllerLayout[ButtonsList::left_PadXAxis].SetDeadzone(0x100000000, cState.ulButtonTouched, cState.rAxis[0].x, 0.1f);
			controllerLayout[ButtonsList::left_PadYAxis].SetDeadzone(0x100000000, cState.ulButtonTouched, cState.rAxis[0].y, 0.1f);
			controllerLayout[ButtonsList::left_Trigger].SetDeadzone(-1, cState.ulButtonTouched, cState.rAxis[1].x, 0.0f);
			controllerLayout[ButtonsList::left_Bumper].SetMinMax(0x4, cState.ulButtonTouched, cState.ulButtonPressed, 1.0f, 0.0f);
		}
		else {
			this->ResetTrackingL();
		}
	}
	else if (controllerRole == vr::TrackedControllerRole_RightHand) {
		if (rContNum != cState.unPacketNum) {
			rContNum = cState.unPacketNum;
			controllerLayout[ButtonsList::right_Menu].Set(false, false, 0.0);
			controllerLayout[ButtonsList::right_ButtonA].SetMinMax(0x80, cState.ulButtonTouched, cState.ulButtonPressed, 1.0f, 0.0f);
			controllerLayout[ButtonsList::right_ButtonB].SetMinMax(0x02, cState.ulButtonTouched, cState.ulButtonPressed, 1.0f, 0.0f);
			controllerLayout[ButtonsList::right_Pad].SetMinMax(0x100000000, cState.ulButtonTouched, cState.ulButtonPressed, 1.0f, 0.0f);
			controllerLayout[ButtonsList::right_PadXAxis].SetDeadzone(0x100000000, cState.ulButtonTouched, cState.rAxis[0].x, 0.1f);
			controllerLayout[ButtonsList::right_PadYAxis].SetDeadzone(0x100000000, cState.ulButtonTouched, cState.rAxis[0].y, 0.1f);
			controllerLayout[ButtonsList::right_Trigger].SetDeadzone(-1, cState.ulButtonTouched, cState.rAxis[1].x, 0.0f);
			controllerLayout[ButtonsList::right_Bumper].SetMinMax(0x4, cState.ulButtonTouched, cState.ulButtonPressed, 1.0f, 0.0f);
		}
		else {
			this->ResetTrackingR();
		}
	}
}

void clsControllerSteamVR::SetTracking()
{
}

void clsControllerSteamVR::ResetTrackingL() {
	controllerLayout[ButtonsList::left_Menu].SetChanged(false);
	controllerLayout[ButtonsList::left_ButtonA].SetChanged(false);
	controllerLayout[ButtonsList::left_ButtonB].SetChanged(false);
	controllerLayout[ButtonsList::left_Pad].SetChanged(false);
	controllerLayout[ButtonsList::left_PadXAxis].SetChanged(false);
	controllerLayout[ButtonsList::left_PadYAxis].SetChanged(false);
	controllerLayout[ButtonsList::left_Trigger].SetChanged(false);
	controllerLayout[ButtonsList::left_Bumper].SetChanged(false);
}

void clsControllerSteamVR::ResetTrackingR() {
	controllerLayout[ButtonsList::right_Menu].SetChanged(false);
	controllerLayout[ButtonsList::right_ButtonA].SetChanged(false);
	controllerLayout[ButtonsList::right_ButtonB].SetChanged(false);
	controllerLayout[ButtonsList::right_Pad].SetChanged(false);
	controllerLayout[ButtonsList::right_PadXAxis].SetChanged(false);
	controllerLayout[ButtonsList::right_PadYAxis].SetChanged(false);
	controllerLayout[ButtonsList::right_Trigger].SetChanged(false);
	controllerLayout[ButtonsList::right_Bumper].SetChanged(false);
}

void clsControllerSteamVR::ResetTracking()
{
	this->ResetTrackingL();
	this->ResetTrackingR();
}
