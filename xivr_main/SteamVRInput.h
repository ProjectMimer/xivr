#pragma once
#define WIN32_LEAN_AND_MEAN
// Windows Header Files
#include <windows.h>
#include <openvr.h>

struct inputActionGame
{
	vr::VRActionSetHandle_t setHandle;

	vr::VRActionHandle_t movement;
	vr::VRActionHandle_t rotation;
	vr::VRActionHandle_t lefthand;
	vr::VRActionHandle_t righthand;
	vr::VRActionHandle_t leftclick;
	vr::VRActionHandle_t rightclick;

	vr::VRActionHandle_t recenter;
	vr::VRActionHandle_t shift;
	vr::VRActionHandle_t alt;
	vr::VRActionHandle_t control;
	vr::VRActionHandle_t escape;
	vr::VRActionHandle_t scrollup;
	vr::VRActionHandle_t scrolldown;
	vr::VRActionHandle_t button01;
	vr::VRActionHandle_t button02;
	vr::VRActionHandle_t button03;
	vr::VRActionHandle_t button04;
	vr::VRActionHandle_t button05;
	vr::VRActionHandle_t button06;
	vr::VRActionHandle_t button07;
	vr::VRActionHandle_t button08;
	vr::VRActionHandle_t button09;
	vr::VRActionHandle_t button10;
	vr::VRActionHandle_t button11;
	vr::VRActionHandle_t button12;

	vr::VRActionHandle_t xbox_button_y;
	vr::VRActionHandle_t xbox_button_x;
	vr::VRActionHandle_t xbox_button_a;
	vr::VRActionHandle_t xbox_button_b;

	vr::VRActionHandle_t xbox_left_trigger;
	vr::VRActionHandle_t xbox_left_bumper;
	vr::VRActionHandle_t xbox_left_stick_click;

	vr::VRActionHandle_t xbox_right_trigger;
	vr::VRActionHandle_t xbox_right_bumper;
	vr::VRActionHandle_t xbox_right_stick_click;

	vr::VRActionHandle_t xbox_pad_up;
	vr::VRActionHandle_t xbox_pad_down;
	vr::VRActionHandle_t xbox_pad_left;
	vr::VRActionHandle_t xbox_pad_right;

	vr::VRActionHandle_t xbox_start;
	vr::VRActionHandle_t xbox_select;

};

struct inputController
{
	struct inputActionGame game;
};

enum buttonLayout
{
	movement,
	rotation,
	leftClick,
	rightClick,
	recenter,
	shift,
	alt,
	control,
	escape,
	button01,
	button02,
	button03,
	button04,
	button05,
	button06,
	button07,
	button08,
	button09,
	button10,
	button11,
	button12,
	xbox_button_y,
	xbox_button_x,
	xbox_button_a,
	xbox_button_b,
	xbox_left_trigger,
	xbox_left_bumper,
	xbox_left_stick_click,
	xbox_right_trigger,
	xbox_right_bumper,
	xbox_right_stick_click,
	xbox_pad_up,
	xbox_pad_down,
	xbox_pad_left,
	xbox_pad_right,
	xbox_start,
	xbox_select
};

void setActionHandlesGame(inputController* input);
