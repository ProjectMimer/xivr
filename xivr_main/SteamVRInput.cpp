#include "SteamVRInput.h"

void setActionHandlesGame(inputController* input)
{
	vr::EVRInputError iError = vr::VRInputError_None;
	iError = vr::VRInput()->GetActionSetHandle("/actions/game", &input->game.setHandle);
	//if (iError != 0)
	//{
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/movement", &input->game.movement);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/rotation", &input->game.rotation);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/lefthand", &input->game.lefthand);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/righthand", &input->game.righthand);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/leftclick", &input->game.leftclick);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/rightclick", &input->game.rightclick);

		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/recenter", &input->game.recenter);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/shift", &input->game.shift);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/alt", &input->game.alt);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/control", &input->game.control);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/escape", &input->game.escape);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/scrollup", &input->game.scrollup);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/scrolldown", &input->game.scrolldown);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/button01", &input->game.button01);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/button02", &input->game.button02);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/button03", &input->game.button03);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/button04", &input->game.button04);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/button05", &input->game.button05);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/button06", &input->game.button06);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/button07", &input->game.button07);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/button08", &input->game.button08);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/button09", &input->game.button09);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/button10", &input->game.button10);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/button11", &input->game.button11);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/button12", &input->game.button12);

		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/xbox_button_y", &input->game.xbox_button_y);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/xbox_button_x", &input->game.xbox_button_x);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/xbox_button_a", &input->game.xbox_button_a);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/xbox_button_b", &input->game.xbox_button_b);

		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/xbox_left_trigger", &input->game.xbox_left_trigger);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/xbox_left_bumper", &input->game.xbox_left_bumper);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/xbox_left_stick_click", &input->game.xbox_left_stick_click);

		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/xbox_right_trigger", &input->game.xbox_right_trigger);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/xbox_right_bumper", &input->game.xbox_right_bumper);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/xbox_right_stick_click", &input->game.xbox_right_stick_click);

		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/xbox_pad_up", &input->game.xbox_pad_up);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/xbox_pad_down", &input->game.xbox_pad_down);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/xbox_pad_left", &input->game.xbox_pad_left);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/xbox_pad_right", &input->game.xbox_pad_right);

		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/xbox_start", &input->game.xbox_start);
		iError = vr::VRInput()->GetActionSetHandle("/actions/game/in/xbox_select", &input->game.xbox_select);
	//}
}

