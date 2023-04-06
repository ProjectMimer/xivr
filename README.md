*Throw wide the gates, that we may pass!*

**XIVR** is an open-source project that aims to add VR support to the critically acclaimed Japanese MMORPG which has an expanded free trial with no restrictions on playtime. This open-source mod is purely based on original code, and does not include any copyrighted materials. While, to our knowledge, the mod is undetectable and fully usable on live servers, using this mod is against the TOS. Use at your own risk; we are not liable for damages caused.

XIVR is currently in an **alpha state**. It will be released as a minimum viable product to gauge user interest and attract other developers interested in working with us. The code is open-source, and we are willing to merge in new features! If you're interested, please let us know!


**• List of chat/macro commands •**

`/xivr` opens the VR settings

`/xivr on` enables VR

`/xivr off` disables VR

`/xivr recenter` recenters the camera

`/xivr screen` renders the game in 2D on a floating screen whenever you need it (sniping minigame, etc)

`/xivr uiz [amount]` sets the distance from the floating screen, where [amount] is a positive or negative value. The default is a value of 0.

`/xivr uiscale [amount]`  sets the sets the size of the floating screen,  where [amount] is a positive or negative value. The default is a value of 1.

`/xivr uireset` resets the distance and size of the floating screen

`/xivr conloc` enables controller locomotion (aka onward locomotion) for first person

`/xivr horizon` makes it so the camera angle is corrected if rotated vertically so the horizon never changes, preventing vr sickness

`/xivr hsnap` enables horizontal snap turning via analogue sticks

`/xivr vsnap` enables vertical snap turning via analogue sticks

`/xivr snapanglex [degrees]` sets the amount of degrees for horizontal snap turning using the analogue stick, where [degrees] is a positive number

`/xivr snapangley [degrees]` sets the amount of degrees for vertical snap turning using the analogue stick, where [degrees] is a positive number

`/xivr rotatex [degrees]` immediate X axis snap turning, where [degrees] is a positive or negative number

`/xivr rotatey [degrees]` immediate Y axis snap turning, where [degrees] is a positive or negative number

`/xivr offsetx [amount]` moves the camera directly along the X axis, where [amount] is a positive or negative value. This can be used for an over-the-shoulder view. 

`/xivr offsety [amount]` moves the camera directly along the Y axis, where [amount] is a positive or negative value. This can be used for an over-the-shoulder view.


**• Changelog •**

v0.0.0.2 released now installing via the repo should work again

v0.0.0.3 released now the UI should no longer hide randomly, if you have downloaded a previous version please delete \AppData\Roaming\XIVLauncher\pluginConfigs\ xivr.json should the error have been saved in the file

v0.0.0.4 released, "band aid edition" Fix cross-eyed world via `/xivr swapeyes` and ui via `/xivr swapeyesui`  If your normal (non vr) controller isnt working please try `/xivr motcontoggle`

v0.0.5 experimental release

Fixed floating UI scaling curvature bug

Fixed shadow glitching at edge of vision

Changes to loading order if it crashed previously please try this and report back

If started from the title screen settings will auto apply if started from the game please run `/xivr loadcon` after starting vr


v0.0.0.6

Fixed controller zoom not working

Updated to new game version


v0.0.0.8

Implemented automatic resolution resize (wont work on first load because it needs to fetch headset data once from Steam VR eye resolution)

Fixed certain UI elements displaying differently in each eye

Added multiple new menu entries for existing chat commands 

Changed loading order, might crash more or less please report back 


v0.0.0.9

Fixed Dresser Camera

Added IPD slider

New VR settings menu

Added verbose logging, logs are saved to dalamud.log in the XIVLauncher folder

Less Crashing


v0.0.0.10 

Support for 6.3

Added first person locomotion based on head tracking, look in the direction you want to go

Added vertical support for first person locomotion, point or look in the direction you want to fly/swim

Fixed unintended first person camera behavior 


v0.0.1.0 HANDTRACKING UPDATE

Disabled culling in first person

Mounts now are visible in first person

All combat effects now are visible in first person

Added toggle to show player body in first person without head

Added hand tracking in first person (if controllers and body is enabled)

Added Z-Offset slider

Removed flickering UI target arrow

Added in world target arrow (by @AJBats)

Added size slider for target arrow (by @AJBats)



Known Issues

First person camera location might be off, please use the offset sliders

If headgear is hidden using the toggle in the menu then the player head wont hide

While in first person with tracking the arms of some NPCs might randomly break

Gear polygon stretching from hand

Its hard to target the exits of player houses in first person


v0.0.3.0 Now actually playable edition

UI

→added toggle to render UI inside the game world with occlusion /xivr uidepth

→added UI z-offset and scale sliders

→fixed UI z-offset and scale chat commands

→while VR is active the mouse will be locked to the game window, you can alt tab to click on other windows

→added ability to click on UI elements that are off screen because the game resolution is too high for the users display (up to 3x the res)

→Dalamud (imgUI) is now visible in the headset



controls

→added ability to point at UI using motion controls to click on UI elements

→added ability to point into the game world using motion controllers to target, interact and place AOE attacks

→added ability to target, interact with objects and place AOE attacks via head tracking when using xbox controllers or kbm

→added ability to move housing furniture via motion controls

→added ability to click the mouse via the right trigger on both normal and motion controllers

→the outline of targets you are looking or pointing at will highlight 

→added native two button controller mappings (WMR, Vive Focus3/XR, Pico, Touch, Quest etc.)

→long pressing a stick down on motion controllers now changes its mode, the left stick switches from movement to dpad while the right stick it switches to a scroll wheel

→fixed being unable to zoom in on the map using controllers in first person

→added option to display weapons in the hands in first person

→added controller haptics for stick mode switching and highlighting potential targets with motion controllers 

→fixed being unable to target certain things like housing doors in first person

→pressing the right stick while holding the right bumper switches into a mode for controlling Dalamud, in this mode the face buttons are replaced by esc, F10-12 so you can remap them freely



tracking

→in first person mode the camera now gets recentered to the head location of the player model

→in first person mode there no longer is a gap in the neck

→the upper body of the player model is now tracked in accordance to head movement when unmounted

→fixed a issue with the arms of certain NPCs breaking when using motion controls

→removed option to hide body in first person

→added Y- & Z-Offset slider for first person

→reenabled head tracking in cutscenes (still as buggy as before)

→fixed player head not hiding correctly when headgear is set to invisible

→upper torso tracking is disabled when using controller or head turning and instead the torso is turning to counteract the turning of the player model

Note: for now all tracking code is disable when mounted



rendering

→added 2D mode /xivr mode2d , recommended if you are unable to get double the FPS (not compatible with asymmetric projection)

→added asymmetric projection toggle, enabling this will improve the performance (requires XIVR restart)

→fixed a DirectX bug that stopped SteamVR from initializing (Thanks to @joko for helping us debug)

→the desktop screen no longer flickers and instead displays black

→implemented ASW 2.0

→optimized shadow map for 100°FOV headsets

→added ultra wide shadow map toggle, for use with widescreen headsets such as the Pimax and Xtal

→fixed internal render resolution slightly decreasing every time you turn VR on and off

→fixed a stereo mismatch in the rendering of the UI

→removed broken post processing effects

Note: changing resolution in SteamVR now requiers a full game restart



other

→the refresh rate limit now gets set to unlimited automatically but requires a restart of the game to take effect

→fixed a bug with under water fx appearing above water

→the mod now is available in 日本語 (Japanese)
