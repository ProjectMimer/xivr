using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xivr.Structures
{
	public enum ActionButtonLayout
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
	}


	public struct InputDigitalActionData
	{
		public bool bActive;
		public UInt64 activeOrigin;
		public bool bState;
		public bool bChanged;
		public float fUpdateTime;
	};

	public struct InputAnalogActionData
	{
		public bool bActive;
		public UInt64 activeOrigin;
		public float x;
		public float y;
		public float z;
		public float deltaX;
		public float deltaY;
		public float deltaZ;
		public float fUpdateTime;
	};
}
