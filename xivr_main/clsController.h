#pragma once
#include <openvr.h>
#include <openvr_capi.h>
#include <vector>
#include "stsdkTypes.h"
#include "stButtonList.h"

typedef unsigned long       DWORD;

struct stControllerButtonView
{
	bool _isTouched;
	bool _isPressed;
	bool _isPressedOld;
	bool _hasChanged;
	bool _isDownFrame;
	bool _isUpFrame;
	float _value;

	stControllerButtonView()
	{
		_isTouched = false;
		_isPressed = false;
		_isPressedOld = false;
		_hasChanged = false;
		_isDownFrame = false;
		_isUpFrame = false;
		_value = 0.0f;
	}

	void SetStatus()
	{
		_isDownFrame = false;
		_isUpFrame = false;
		if (_isPressed == true && _isPressedOld == false) {
			_isDownFrame = true;
			_isUpFrame = false;

		}
		else if (_isPressed == false && _isPressedOld == true) {
			_isDownFrame = false;
			_isUpFrame = true;
		}
		_isPressedOld = _isPressed;
	}

	void SetChanged(bool hasChanged)
	{
		_hasChanged = hasChanged;
		SetStatus();
	}

	void Set(bool isTouched, bool isPressed, float value)
	{
		_isTouched = isTouched;
		_isPressed = isPressed;
		_hasChanged = ((_value == value) ? false : true);
		_value = value;
		SetStatus();
	}

	void SetMinMax(uint64_t buttonID, uint64_t touchedList, uint64_t pressedList, float valueA, float valueB)
	{
		_isTouched = (((touchedList & buttonID) == buttonID) ? true : false);
		_isPressed = (((pressedList & buttonID) == buttonID) ? true : false);
		if (_isPressed) {
			_hasChanged = ((_value == valueA) ? false : true);
			_value = valueA;
		}
		else {
			_hasChanged = ((_value == valueB) ? false : true);
			_value = valueB;
		}
		SetStatus();
	}

	void SetDeadzone(uint64_t buttonID, uint64_t touchedList, uint64_t pressedList, float value, float deadzone)
	{
		if (abs(value) < deadzone) {
			value = 0;
		}
		else {
			value = ((value > 0) ? value - deadzone : value + deadzone);
		}
		_isTouched = (((touchedList & buttonID) == buttonID) ? true : false);
		_isPressed = (((pressedList & buttonID) == buttonID) ? true : false);
		_hasChanged = ((_value == value) ? false : true);
		_value = value;
		SetStatus();
	}

	void SetDeadzone(uint64_t buttonID, uint64_t touchedList, float value, float deadzone) {
		if (abs(value) < deadzone) {
			value = 0;
		}
		else {
			value = ((value > 0) ? value - deadzone : value + deadzone);
		}
		_isTouched = (((touchedList & buttonID) == buttonID) ? true : false);
		_isPressed = ((value == 0.0f) ? false : true);
		_hasChanged = ((_value == value) ? false : true);
		_value = value;
		SetStatus();
	}

	void SetDeadzone(uint64_t buttonID, float value, float deadzone) {
		if (abs(value) < deadzone) {
			value = 0;
		}
		else {
			value = ((value > 0) ? value - deadzone : value + deadzone);
		}
		_isTouched = ((value == 0.0f) ? false : true);
		_isPressed = ((value == 0.0f) ? false : true);
		_hasChanged = ((_value == value) ? false : true);
		_value = value;
		SetStatus();
	}

	void Set(uint64_t buttonID, uint64_t touchedList, short value)
	{
		_isTouched = (((touchedList & buttonID) == buttonID) ? true : false);
		_isPressed = (((touchedList & buttonID) == buttonID) ? true : false);
		//_isPressed = (((float)value == 0.0f) ? false : true);
		_hasChanged = ((_value == (float)value) ? false : true);
		_value = (float)value;
		SetStatus();
	}

};

//----
// Base Controller Data
//----
class clsControllerBase
{
public:
	clsControllerBase();
	~clsControllerBase();
	virtual void SetTracking(vr::ETrackedControllerRole controllerRole, vr::VRControllerState_t cState) = 0;
	virtual void SetTracking() = 0;
	virtual void ResetTracking() = 0;

	bool HasChanged(ButtonList buttonID);
	bool IsTouched(ButtonList buttonID);
	bool IsPressed(ButtonList buttonID);
	bool IsDownFrame(ButtonList buttonID);
	bool IsUpFrame(ButtonList buttonID);
	float GetValue(ButtonList buttonID);

protected:
	std::vector<stControllerButtonView> controllerLayout;
	DWORD controllerGestuers;
};

//----
// Overload for SteamVR Controllers
//----
class clsControllerSteamVR : public clsControllerBase
{
public:
	clsControllerSteamVR();
	~clsControllerSteamVR();
	void SetTracking(vr::ETrackedControllerRole controllerRole, vr::VRControllerState_t cState);
	void SetTracking();
	void ResetTrackingL();
	void ResetTrackingR();
	void ResetTracking();
private:
	int lContNum;
	int rContNum;
};


//----
// Base controller class
//----
class clsController
{
public:
	clsController();
	~clsController();
	void Set(sdkType sdkID, ControllerList controllerID);
	void SetTracking(vr::ETrackedControllerRole controllerRole, vr::VRControllerState_t cState);
	void SetTracking();
	void ResetTracking();

	bool HasChanged(ButtonList buttonID, ControllerType controllerType);
	bool IsTouched(ButtonList buttonID, ControllerType controllerType);
	bool IsPressed(ButtonList buttonID, ControllerType controllerType);
	bool IsDownFrame(ButtonList buttonID, ControllerType controllerType);
	bool IsUpFrame(ButtonList buttonID, ControllerType controllerType);
	float GetValue(ButtonList buttonID, ControllerType controllerType);

private:
	clsControllerBase* cXBox;
	clsControllerBase* cVirtual;
	sdkType _sdkID;
	ControllerList _controllerID;
	unsigned long controllerNum;
};
#pragma once
