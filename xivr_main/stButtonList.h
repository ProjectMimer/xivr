#pragma once

namespace ButtonsList
{
	enum _
	{
		left_Menu,
		left_Trigger,
		left_Bumper,
		left_ButtonA,
		left_ButtonB,
		left_Pad,
		left_PadXAxis,
		left_PadYAxis,
		left_DPad_Up,
		left_DPad_Down,
		left_DPad_Left,
		left_DPad_Right,

		right_Menu,
		right_Trigger,
		right_Bumper,
		right_ButtonA,
		right_ButtonB,
		right_Pad,
		right_PadXAxis,
		right_PadYAxis,
		right_DPad_Up,
		right_DPad_Down,
		right_DPad_Left,
		right_DPad_Right,

		left_GestureIndexPoint,
		left_GestureThumbUp,
		left_GestureFist,

		right_GestureIndexPoint,
		right_GestureThumbUp,
		right_GestureFist
	};
};

namespace ControllerTypes
{
	enum _
	{
		controllerType_NA = 0,
		controllerType_XBox = 1,
		controllerType_Virtual = 2,
		//controllerType_Both = 3
	};
};

namespace ControllerID
{
	enum _
	{
		NA = 0,
		openvrVive = 1,
		openvrTouch = 2,
		oculusTouch = 3,
	};
};

namespace HeadsetIDs
{
	enum _
	{
		NA = 0,
		vive = 1,
		rift = 2,
	};
};

namespace MouseButtons
{
	enum _
	{
		left,
		middle,
		right,
	};
};

const int ButtonsListCount = 30;
typedef ButtonsList::_ ButtonList;
typedef ControllerTypes::_ ControllerType;
typedef ControllerID::_ ControllerList;
typedef HeadsetIDs::_ HeadsetList;
typedef MouseButtons::_ MouseButton;
