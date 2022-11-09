#include "clsController.h"

clsControllerBase::clsControllerBase() : controllerLayout(std::vector<stControllerButtonView>(ButtonsListCount)),
controllerGestuers(0)
{
}

clsControllerBase::~clsControllerBase()
{
}

bool clsControllerBase::HasChanged(ButtonList buttonID)
{
	if (buttonID >= 0 && buttonID < ButtonsListCount) {
		return controllerLayout[buttonID]._hasChanged;
	}
	else {
		return false;
	}
}

bool clsControllerBase::IsTouched(ButtonList buttonID)
{
	if (buttonID >= 0 && buttonID < ButtonsListCount) {
		return controllerLayout[buttonID]._isTouched;
	}
	else {
		return false;
	}
}

bool clsControllerBase::IsPressed(ButtonList buttonID)
{
	if (buttonID >= 0 && buttonID < ButtonsListCount) {
		return controllerLayout[buttonID]._isPressed;
	}
	else {
		return false;
	}
}

bool clsControllerBase::IsDownFrame(ButtonList buttonID)
{
	if (buttonID >= 0 && buttonID < ButtonsListCount) {
		return controllerLayout[buttonID]._isDownFrame;
	}
	else {
		return false;
	}
}

bool clsControllerBase::IsUpFrame(ButtonList buttonID)
{
	if (buttonID >= 0 && buttonID < ButtonsListCount) {
		return controllerLayout[buttonID]._isUpFrame;
	}
	else {
		return false;
	}
}

float clsControllerBase::GetValue(ButtonList buttonID)
{
	if (buttonID >= 0 && buttonID < ButtonsListCount) {
		return controllerLayout[buttonID]._value;
	}
	else {
		return false;
	}
}



clsController::clsController()
{
	controllerNum = 0;
}

clsController::~clsController()
{

}

void clsController::Set(sdkType sdkID, ControllerList controllerID)
{
	cVirtual = new clsControllerSteamVR();
}

void clsController::SetTracking(vr::ETrackedControllerRole controllerRole, vr::VRControllerState_t cState)
{
	cVirtual->SetTracking(controllerRole, cState);
}

void clsController::SetTracking()
{
	//cVirtual->SetTracking(inputState);
}

void clsController::ResetTracking()
{
	cVirtual->ResetTracking();
}

bool clsController::HasChanged(ButtonList buttonID, ControllerType controllerType)
{
	if (controllerType == ControllerTypes::controllerType_XBox) {
		return cXBox->HasChanged(buttonID);
	}
	else if (controllerType == ControllerTypes::controllerType_Virtual) {
		return cVirtual->HasChanged(buttonID);
	}
	else {
		return false;
	}
}

bool clsController::IsTouched(ButtonList buttonID, ControllerType controllerType)
{
	if (controllerType == ControllerTypes::controllerType_XBox) {
		return cXBox->IsTouched(buttonID);
	}
	else if (controllerType == ControllerTypes::controllerType_Virtual) {
		return cVirtual->IsTouched(buttonID);
	}
	else {
		return false;
	}
}

bool clsController::IsPressed(ButtonList buttonID, ControllerType controllerType)
{
	if (controllerType == ControllerTypes::controllerType_XBox) {
		return cXBox->IsPressed(buttonID);
	}
	else if (controllerType == ControllerTypes::controllerType_Virtual) {
		return cVirtual->IsPressed(buttonID);
	}
	else {
		return false;
	}
}

bool clsController::IsDownFrame(ButtonList buttonID, ControllerType controllerType)
{
	if (controllerType == ControllerTypes::controllerType_XBox) {
		return cXBox->IsDownFrame(buttonID);
	}
	else if (controllerType == ControllerTypes::controllerType_Virtual) {
		return cVirtual->IsDownFrame(buttonID);
	}
	else {
		return false;
	}
}

bool clsController::IsUpFrame(ButtonList buttonID, ControllerType controllerType)
{
	if (controllerType == ControllerTypes::controllerType_XBox) {
		return cXBox->IsUpFrame(buttonID);
	}
	else if (controllerType == ControllerTypes::controllerType_Virtual) {
		return cVirtual->IsUpFrame(buttonID);
	}
	else {
		return false;
	}
}

float clsController::GetValue(ButtonList buttonID, ControllerType controllerType)
{
	if (controllerType == ControllerTypes::controllerType_XBox) {
		return cXBox->GetValue(buttonID);
	}
	else if (controllerType == ControllerTypes::controllerType_Virtual) {
		return cVirtual->GetValue(buttonID);
	}
	else {
		return 0.0f;
	}
}
