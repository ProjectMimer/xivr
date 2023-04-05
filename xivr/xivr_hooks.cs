using System;
using System.IO;
using System.Drawing;
using System.Numerics;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud;
using Dalamud.Utility.Signatures;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Logging;
using xivr.Structures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static FFXIVClientStructs.FFXIV.Client.UI.AddonNamePlate;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.Havok;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using System.Threading.Tasks;
using FFXIVClientStructs.Interop.Attributes;

namespace xivr
{
    public delegate void HandleStatusDelegate(bool status);
    public delegate void HandleInputDelegate(InputAnalogActionData analog, InputDigitalActionData digital);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void UpdateControllerInput(ActionButtonLayout buttonId, InputAnalogActionData analog, InputDigitalActionData digital);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void InternalLogging(String value);




    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class HandleStatus : System.Attribute
    {
        public string fnName { get; private set; }
        public HandleStatus(string name)
        {
            fnName = name;
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class HandleInputAttribute : System.Attribute
    {
        public ActionButtonLayout inputId { get; private set; }
        public HandleInputAttribute(ActionButtonLayout buttonId) => inputId = buttonId;
    }


    internal unsafe class xivr_hooks
    {
        protected Dictionary<string, HandleStatusDelegate> functionList = new Dictionary<string, HandleStatusDelegate>();
        protected Dictionary<ActionButtonLayout, HandleInputDelegate> inputList = new Dictionary<ActionButtonLayout, HandleInputDelegate>();

        //----
        // Required here to load openvr_api, if its not then openvr_api isnt loaded and
        // xivr_main isnt loaded either
        //----
        [DllImport("openvr_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool VR_IsHmdPresent();


        byte[] GetThreadedDataASM =
            {
                0x55, // push rbp
                0x65, 0x48, 0x8B, 0x04, 0x25, 0x58, 0x00, 0x00, 0x00, // mov rax,gs:[00000058]
                0x5D, // pop rbp
                0xC3  // ret
            };


        private bool initalized = false;
        private bool hooksSet = false;
        private bool enableVR = true;
        private bool enableFloatingHUD = true;
        private bool forceFloatingScreen = false;
        private bool inCutscene = false;
        private bool isMounted = false;
        private bool housingMode = false;
        private bool dalamudMode = false;
        private byte targetAddonAlpha = 0;
        private RenderModes curRenderMode = RenderModes.None;
        private int curEye = 0;
        private int[] nextEye = { 1, 0 };
        private int[] swapEyes = { 1, 0 };
        private float Deg2Rad = MathF.PI / 180.0f;
        private float Rad2Deg = 180.0f / MathF.PI;
        private float cameraZoom = 0.0f;
        private float leftBumperValue = 0.0f;
        private float firstPersonCameraHeight = 0.0f;
        private float BridgeBoneHeight = 0.0f;
        private ChangedTypeBool mouseoverUI = new ChangedTypeBool();
        private ChangedTypeBool mouseoverTarget = new ChangedTypeBool();
        private Vector2 rotateAmount = new Vector2(0.0f, 0.0f);
        private Vector3 onwardAngle = new Vector3(0.0f, 0.0f, 0.0f);
        private Vector3 onwardDiff = new Vector3(0.0f, 0.0f, 0.0f);
        private Point virtualMouse = new Point(0, 0);
        private Point actualMouse = new Point(0, 0);
        private Dictionary<ActionButtonLayout, bool> inputState = new Dictionary<ActionButtonLayout, bool>();
        private Dictionary<ActionButtonLayout, ChangedType<bool>> inputStatus = new Dictionary<ActionButtonLayout, ChangedType<bool>>();
        private Dictionary<ConfigOption, int> SavedSettings = new Dictionary<ConfigOption, int>();
        private Stack<bool> overrideFromParent = new Stack<bool>();
        private bool frfCalculateViewMatrix = false; // frf first run this frame
        private int ScreenMode = 0;
        private UInt64 DisableSetCursorPosOrig = 0;
        private UInt64 DisableSetCursorPosOverride = 0x05C6909090909090;
        private UInt64 DisableSetCursorPosAddr = 0;

        private const int FLAG_INVIS = (1 << 1) | (1 << 11);
        private const byte NamePlateCount = 50;
        private UInt64 BaseAddress = 0;
        private UInt64 globalScaleAddress = 0;
        private UInt64 RenderTargetManagerAddress = 0;
        private GCHandle getThreadedDataHandle;
        private int[] runCount = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private UInt64 tls_index = 0;
        private UpdateControllerInput controllerCallback;
        private InternalLogging internalLogging;

        private Matrix4x4 curViewMatrixWithoutHMD = Matrix4x4.Identity;
        private Matrix4x4 curViewMatrixWithoutHMDI = Matrix4x4.Identity;
        private Matrix4x4 hmdMatrix = Matrix4x4.Identity;
        private Matrix4x4 hmdMatrixI = Matrix4x4.Identity;
        private Matrix4x4 lhcMatrix = Matrix4x4.Identity;
        private Matrix4x4 lhcMatrixI = Matrix4x4.Identity;
        private Matrix4x4 rhcMatrix = Matrix4x4.Identity;
        private Matrix4x4 rhcMatrixI = Matrix4x4.Identity;
        private Matrix4x4 fixedProjection = Matrix4x4.Identity;
        private Matrix4x4[] gameProjectionMatrix = {
                    Matrix4x4.Identity,
                    Matrix4x4.Identity
                };
        private Matrix4x4[] eyeOffsetMatrix = {
                    Matrix4x4.Identity,
                    Matrix4x4.Identity
                };

        private Matrix4x4 convertXZ = new Matrix4x4(0, 0, -1, 0,
                                                         0, -1, 0, 0,
                                                        -1, 0, 0, 0,
                                                         0, 0, 0, 1);

        private SceneCameraManager* camInst = null;
        private ControlSystemCameraManager* csCameraManager = null;
        private TargetSystem* targetSystem = TargetSystem.Instance();
        private Structures.RenderTargetManager* renderTargetManager = null;
        private FFXIVClientStructs.FFXIV.Client.System.Framework.Framework* frameworkInstance = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        private AtkTextNode* vrTargetCursor = null;
        private CharSelectionCharList* charList = null;

        private static class Signatures
        {
            internal const string g_tls_index = "8B 15 ?? ?? ?? ?? 45 33 E4";
            internal const string g_TextScale = "F3 0F 10 0D ?? ?? ?? ?? F3 0F 10 40 4C";
            internal const string g_SceneCameraManagerInstance = "48 8B 05 ?? ?? ?? ?? 83 78 50 00 75 22";
            internal const string g_RenderTargetManagerInstance = "48 8B 05 ?? ?? ?? ?? 49 63 C8";
            internal const string g_ControlSystemCameraManager = "48 8D 0D ?? ?? ?? ?? F3 0F 10 4B ??";
            internal const string g_SelectScreenCharacterList = "4C 8D 35 ?? ?? ?? ?? BF C8 00 00 00";
            internal const string g_DisableSetCursorPosAddr = "FF ?? ?? ?? ?? 00 C6 05 ?? ?? ?? ?? 00 0F B6 43 38";

            internal const string GetCutsceneCameraOffset = "E8 ?? ?? ?? ?? 48 8B 70 48 48 85 F6";
            internal const string GameObjectGetPosition = "83 79 7C 00 75 09 F6 81 ?? ?? ?? ?? ?? 74 2A";
            internal const string GetTargetFromRay = "E8 ?? ?? ?? ?? 84 C0 74 ?? 48 8B F3";
            internal const string GetMouseOverTarget = "E8 ?? ?? ?? ?? 48 8B D8 48 85 DB 74 ?? 48 8B CB";
            internal const string ScreenPointToRay = "E8 ?? ?? ?? ?? 4C 8B E0 48 8B EB";
            internal const string ScreenPointToRay1 = "E8 ?? ?? ?? ?? 80 BF 74 01 00 00 00";
            internal const string MousePointScreenToClient = "E8 ?? ?? ?? ?? 0f B7 44 24 50 66 89 83 98 09 00 00";


            internal const string DisableLeftClick = "E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 74 16";
            internal const string DisableRightClick = "E8 ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? 48 85 C0 74 1B";
            internal const string SetRenderTarget = "E8 ?? ?? ?? ?? 40 38 BC 24 00 02 00 00";
            internal const string AllocateQueueMemory = "E8 ?? ?? ?? ?? 48 85 C0 74 ?? C7 00 04 00 00 00";
            internal const string Pushback = "E8 ?? ?? ?? ?? EB ?? 8B 87 6C 04 00 00";
            internal const string PushbackUI = "E8 ?? ?? ?? ?? EB 05 E8 ?? ?? ?? ?? 48 8B 5C 24 78";
            internal const string OnRequestedUpdate = "48 8B C4 41 56 48 81 EC ?? ?? ?? ?? 48 89 58 F0";
            internal const string DXGIPresent = "E8 ?? ?? ?? ?? C6 47 79 00 48 8B 8F";
            internal const string RenderThreadSetRenderTarget = "E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? F3 41 0F 10 5A 18";
            internal const string CamManagerSetMatrix = "4C 8B DC 49 89 5B 10 49 89 73 18 49 89 7B 20 55 49 8D AB";
            internal const string CSUpdateConstBuf = "4C 8B DC 49 89 5B 20 55 57 41 56 49 8D AB";
            internal const string SetUIProj = "E8 ?? ?? ?? ?? 8B 0D ?? ?? ?? ?? 48 8D 94 24";
            internal const string CalculateViewMatrix = "E8 ?? ?? ?? ?? 8B 83 EC 00 00 00 D1 E8 A8 01 74 1B";
            internal const string CutsceneViewMatrix = "E8 ?? ?? ?? ?? 80 BB 98 00 00 00 01 75 ??";
            internal const string UpdateRotation = "E8 ?? ?? ?? ?? 0F B6 93 20 02 00 00 48 8B CB";
            internal const string MakeProjectionMatrix2 = "E8 ?? ?? ?? ?? 4C 8B 2D ?? ?? ?? ?? 41 0F 28 C2";
            internal const string CSMakeProjectionMatrix = "E8 ?? ?? ?? ?? 0F 28 46 10 4C 8D 7E 10";
            internal const string NamePlateDraw = "0F B7 81 ?? ?? ?? ?? 4C 8B C1 66 C1 E0 06";
            internal const string RunBoneMath = "E8 ?? ?? ?? ?? 44 0F 28 58 10";
            internal const string CalculateHeadAnimation = "48 89 6C 24 20 41 56 48 83 EC 30 48 8B EA";
            internal const string LoadCharacter = "E8 ?? ?? ?? ?? 4D 85 F6 74 ?? 49 8B CE E8 ?? ?? ?? ?? 84 C0 75 ?? 4D 8B 46 20";
            internal const string ChangeEquipment = "E8 ?? ?? ?? ?? 41 B5 01 FF C6";
            internal const string ChangeWeapon = "E8 ?? ?? ?? ?? 80 7F 25 00";
            internal const string EquipGearsetInternal = "E8 ?? ?? ?? ?? C7 87 08 01 00 00 00 00 00 00 C6 46 08 01 E9 ?? ?? ?? ?? 41 8B 4E 04";
            internal const string GetAnalogueValue = "E8 ?? ?? ?? ?? 66 44 0F 6E C3";
            internal const string ControllerInput = "E8 ?? ?? ?? ?? 41 8B 86 3C 04 00 00";

            internal const string PhysicsBoneUpdate = "E8 ?? ?? ?? ?? 48 8D 93 90 00 00 00 4C 8D 43 40";
            internal const string RunGameTasks = "E8 ?? ?? ?? ?? 48 8B 8B B8 35 00 00";
        }

        public static void PrintEcho(string message) => DalamudApi.ChatGui.Print($"[xivr] {message}");
        public static void PrintError(string message) => DalamudApi.ChatGui.PrintError($"[xivr] {message}");


        public void SetFunctionHandles()
        {
            //----
            // Gets a list of all the methods this class contains that are public and instanced (non static)
            // then looks for a specific attirbute attached to the class
            // Once found, create a delegate and add both the attribute and delegate to a dictionary
            //----
            functionList.Clear();
            System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            foreach (System.Reflection.MethodInfo method in this.GetType().GetMethods(flags))
            {
                foreach (System.Attribute attribute in method.GetCustomAttributes(typeof(HandleStatus), false))
                {
                    string key = ((HandleStatus)attribute).fnName;
                    HandleStatusDelegate handle = (HandleStatusDelegate)HandleStatusDelegate.CreateDelegate(typeof(HandleStatusDelegate), this, method);

                    if (!functionList.ContainsKey(key))
                    {
                        if (xivr.cfg.data.vLog)
                            PluginLog.Log($"SetFunctionHandles Adding {key}");
                        functionList.Add(key, handle);
                    }
                }
            }
        }


        public void SetInputHandles()
        {
            //----
            // Gets a list of all the methods this class contains that are public and instanced (non static)
            // then looks for a specific attirbute attached to the class
            // Once found, create a delegate and add both the attribute and delegate to a dictionary
            //----
            inputList.Clear();
            System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            foreach (System.Reflection.MethodInfo method in this.GetType().GetMethods(flags))
            {
                foreach (System.Attribute attribute in method.GetCustomAttributes(typeof(HandleInputAttribute), false))
                {
                    ActionButtonLayout key = ((HandleInputAttribute)attribute).inputId;
                    HandleInputDelegate handle = (HandleInputDelegate)HandleInputDelegate.CreateDelegate(typeof(HandleInputDelegate), this, method);

                    if (!inputList.ContainsKey(key))
                    {
                        if (xivr.cfg.data.vLog)
                            PluginLog.Log($"SetInputHandles Adding {key}");
                        inputList.Add(key, handle);
                        inputState.Add(key, false);
                        inputStatus.Add(key, new ChangedType<bool>());
                    }
                }
            }
        }



        public bool Initialize()
        {
            if (xivr.cfg.data.vLog)
                PluginLog.Log($"Initialize A {initalized} {hooksSet}");

            if (initalized == false)
            {
                SignatureHelper.Initialise(this);

                BaseAddress = (UInt64)Process.GetCurrentProcess()?.MainModule?.BaseAddress;

                IntPtr tmpAddress = DalamudApi.SigScanner.GetStaticAddressFromSig(Signatures.g_SceneCameraManagerInstance);
                camInst = (SceneCameraManager*)(*(UInt64*)tmpAddress);

                csCameraManager = (ControlSystemCameraManager*)DalamudApi.SigScanner.GetStaticAddressFromSig(Signatures.g_ControlSystemCameraManager);
                globalScaleAddress = (UInt64)DalamudApi.SigScanner.GetStaticAddressFromSig(Signatures.g_TextScale);
                RenderTargetManagerAddress = (UInt64)DalamudApi.SigScanner.GetStaticAddressFromSig(Signatures.g_RenderTargetManagerInstance);
                tls_index = (UInt64)DalamudApi.SigScanner.GetStaticAddressFromSig(Signatures.g_tls_index);
                csCameraManager = (ControlSystemCameraManager*)DalamudApi.SigScanner.GetStaticAddressFromSig(Signatures.g_ControlSystemCameraManager);
                charList = (CharSelectionCharList*)DalamudApi.SigScanner.GetStaticAddressFromSig(Signatures.g_SelectScreenCharacterList);
                DisableSetCursorPosAddr = (UInt64)DalamudApi.SigScanner.ScanText(Signatures.g_DisableSetCursorPosAddr);
                DisableSetCursorPosOrig = *(UInt64*)DisableSetCursorPosAddr;

                renderTargetManager = *(Structures.RenderTargetManager**)RenderTargetManagerAddress;

                curRenderMode = RenderModes.None;
                GetThreadedDataInit();
                SetFunctionHandles();
                SetInputHandles();

                SavedSettings[ConfigOption.MouseOpeLimit] = ConfigModule.Instance()->GetIntValue(ConfigOption.MouseOpeLimit);
                SavedSettings[ConfigOption.ObjectBorderingType] = ConfigModule.Instance()->GetIntValue(ConfigOption.ObjectBorderingType);
                ConfigModule.Instance()->SetOption(ConfigOption.Fps, 0);
                ConfigModule.Instance()->SetOption(ConfigOption.MouseOpeLimit, 0);

                controllerCallback = (buttonId, analog, digital) =>
                {
                    if (inputList.ContainsKey(buttonId))
                        inputList[buttonId](analog, digital);
                };

                internalLogging = (value) =>
                {
                    PluginLog.Log($"xivr_main: {value}");
                };

                Imports.SetLogFunction(internalLogging);
                initalized = true;
            }
            if (xivr.cfg.data.vLog)
                PluginLog.Log($"Initialize B {initalized} {hooksSet}");

            return initalized;
        }

        public bool Start()
        {
            if (xivr.cfg.data.vLog)
            {
                PluginLog.Log($"Settings:");
                PluginLog.Log($"-- isEnabled = {xivr.cfg.data.isEnabled}");
                PluginLog.Log($"-- isAutoEnabled = {xivr.cfg.data.isAutoEnabled}");
                PluginLog.Log($"-- forceFloatingScreen = {xivr.cfg.data.forceFloatingScreen}");
                PluginLog.Log($"-- forceFloatingInCutscene = {xivr.cfg.data.forceFloatingInCutscene}");
                PluginLog.Log($"-- horizontalLock = {xivr.cfg.data.horizontalLock}");
                PluginLog.Log($"-- verticalLock = {xivr.cfg.data.verticalLock}");
                PluginLog.Log($"-- horizonLock = {xivr.cfg.data.horizonLock}");
                PluginLog.Log($"-- runRecenter = {xivr.cfg.data.runRecenter}");
                PluginLog.Log($"-- offsetAmountX = {xivr.cfg.data.offsetAmountX}");
                PluginLog.Log($"-- offsetAmountY = {xivr.cfg.data.offsetAmountY}");
                PluginLog.Log($"-- snapRotateAmountX = {xivr.cfg.data.snapRotateAmountX}");
                PluginLog.Log($"-- snapRotateAmountY = {xivr.cfg.data.snapRotateAmountY}");
                PluginLog.Log($"-- uiOffsetZ = {xivr.cfg.data.uiOffsetZ}");
                PluginLog.Log($"-- uiOffsetScale = {xivr.cfg.data.uiOffsetScale}");
                PluginLog.Log($"-- conloc = {xivr.cfg.data.conloc}");
                PluginLog.Log($"-- swapEyes = {xivr.cfg.data.swapEyes}");
                PluginLog.Log($"-- swapEyesUI = {xivr.cfg.data.swapEyesUI}");
                PluginLog.Log($"-- motioncontrol = {xivr.cfg.data.motioncontrol}");
                PluginLog.Log($"-- hmdWidth = {xivr.cfg.data.hmdWidth}");
                PluginLog.Log($"-- hmdHeight = {xivr.cfg.data.hmdHeight}");
                PluginLog.Log($"-- autoResize = {xivr.cfg.data.autoResize}");
                PluginLog.Log($"-- ipdOffset = {xivr.cfg.data.ipdOffset}");
                PluginLog.Log($"-- vLog = {xivr.cfg.data.vLog}");
                PluginLog.Log($"-- hmdloc = {xivr.cfg.data.hmdloc}");
                PluginLog.Log($"-- vertloc = {xivr.cfg.data.vertloc}");
                PluginLog.Log($"-- targetCursorSize = {xivr.cfg.data.targetCursorSize}");
                PluginLog.Log($"-- offsetAmountZ = {xivr.cfg.data.offsetAmountZ}");
                PluginLog.Log($"-- uiDepth = {xivr.cfg.data.uiDepth}");
                PluginLog.Log($"-- hmdPointing = {xivr.cfg.data.hmdPointing}");
                PluginLog.Log($"-- mode2d = {xivr.cfg.data.mode2d}");
                PluginLog.Log($"-- asymmetricProjection = {xivr.cfg.data.asymmetricProjection}");
                PluginLog.Log($"-- immersiveMovement = {xivr.cfg.data.immersiveMovement}");
                PluginLog.Log($"-- immersiveFull = {xivr.cfg.data.immersiveFull}");
                PluginLog.Log($"Start A {initalized} {hooksSet}");
            }
            if (initalized == true && hooksSet == false && VR_IsHmdPresent())
            {
                if (xivr.cfg.data.vLog)
                    PluginLog.Log($"SetDX Dx: {(IntPtr)Device.Instance():X} | RndTrg:{*(IntPtr*)RenderTargetManagerAddress:X}");

                if (!Imports.SetDX11((IntPtr)Device.Instance(), *(IntPtr*)RenderTargetManagerAddress))
                    return false;

                string filePath = Path.Join(DalamudApi.PluginInterface.AssemblyLocation.DirectoryName, "config", "actions.json");
                if (Imports.SetActiveJSON(filePath, filePath.Length) == false)
                    PluginLog.LogError($"Error loading Json file : {filePath}");


                gameProjectionMatrix[0] = Imports.GetFramePose(poseType.Projection, 0);
                gameProjectionMatrix[1] = Imports.GetFramePose(poseType.Projection, 1);
                gameProjectionMatrix[0].M43 *= -1;
                gameProjectionMatrix[1].M43 *= -1;
                SetRenderingMode();

                ConfigModule.Instance()->SetOption(ConfigOption.Fps, 0);
                ConfigModule.Instance()->SetOption(ConfigOption.MouseOpeLimit, 1);

                if (DisableSetCursorPosAddr != 0)
                    SafeMemory.Write<UInt64>((IntPtr)DisableSetCursorPosAddr, DisableSetCursorPosOverride);

                //----
                // Enable all hooks
                //----
                foreach (KeyValuePair<string, HandleStatusDelegate> attrib in functionList)
                    attrib.Value(true);

                hooksSet = true;
                PrintEcho("Starting VR.");
            }
            if (xivr.cfg.data.vLog)
                PluginLog.Log($"Start B {initalized} {hooksSet}");



            //----
            // Loop though the bone enum list and convert it to a dict
            //----
            int j = 0;
            boneNameToEnum.Clear();
            foreach (string i in Enum.GetNames(typeof(BoneList)))
            {
                boneNameToEnum.Add(i, (BoneList)j);
                j++;
            }

            return hooksSet;
        }



        public void Stop()
        {
            if (xivr.cfg.data.vLog)
                PluginLog.Log($"Stop A {initalized} {hooksSet}");
            if (hooksSet == true)
            {
                //----
                // Disable all hooks
                //----
                foreach (KeyValuePair<string, HandleStatusDelegate> attrib in functionList)
                    attrib.Value(false);

                //----
                // Disable any input that might still be on
                //----
                InputAnalogActionData analog = new InputAnalogActionData();
                InputDigitalActionData digital = new InputDigitalActionData();
                analog.bActive = false;
                digital.bActive = false;
                foreach (KeyValuePair<ActionButtonLayout, HandleInputDelegate> input in inputList)
                    input.Value(analog, digital);


                gameProjectionMatrix[0] = Matrix4x4.Identity;
                gameProjectionMatrix[1] = Matrix4x4.Identity;
                eyeOffsetMatrix[0] = Matrix4x4.Identity;
                eyeOffsetMatrix[1] = Matrix4x4.Identity;
                curRenderMode = RenderModes.None;

                ConfigModule.Instance()->SetOption(ConfigOption.Fps, 0);
                ConfigModule.Instance()->SetOption(ConfigOption.MouseOpeLimit, 0);
                ConfigModule.Instance()->SetOption(ConfigOption.ObjectBorderingType, SavedSettings[ConfigOption.ObjectBorderingType]);



                //----
                // Restores the target arrow alpha and remove the vr cursor
                //----
                fixed (AtkTextNode** pvrTargetCursor = &vrTargetCursor)
                    VRCursor.FreeVRTargetCursor(pvrTargetCursor);

                AtkUnitBase* targetAddon = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("_TargetCursor", 1);
                if (targetAddon != null)
                    targetAddon->Alpha = targetAddonAlpha;


                FirstToThirdPersonView();
                if (DisableSetCursorPosAddr != 0)
                    SafeMemory.Write<UInt64>((IntPtr)DisableSetCursorPosAddr, DisableSetCursorPosOrig);

                /*
                Dictionary<string, bool> singleNames = new Dictionary<string, bool>();
                foreach (KeyValuePair<hkaPose, Dictionary<string, int>> names in boneNames)
                {
                    foreach (KeyValuePair<string, int> innernames in names.Value)
                    {
                        if(!singleNames.ContainsKey(innernames.Key))
                            singleNames.Add(innernames.Key, true);
                    }
                }

                foreach (KeyValuePair<string, bool> innernames in singleNames)
                {
                    PluginLog.Log($"{innernames.Key}");
                }
                */

                Imports.UnsetDX11();

                hooksSet = false;
                PrintEcho("Stopping VR.");
            }
            if (xivr.cfg.data.vLog)
                PluginLog.Log($"Stop B {initalized} {hooksSet}");
        }

        private void FirstToThirdPersonView()
        {
            Imports.Recenter();

            PlayerCharacter? player = DalamudApi.ClientState.LocalPlayer;
            if (player != null)
            {
                GameObject* bonedObject = (GameObject*)player.Address;
                Character* bonedCharacter = (Character*)player.Address;

                if (bonedCharacter != null)
                {
                    //bonedCharacter->DrawData.Flags1 = HideHeadValue;

                    UInt64 equipOffset = (UInt64)(UInt64*)&bonedCharacter->DrawData;
                    ChangeEquipmentHook!.Original(equipOffset, CharEquipSlots.Head, currentEquipmentSet.Head);
                    ChangeEquipmentHook!.Original(equipOffset, CharEquipSlots.Ears, currentEquipmentSet.Ears);
                    ChangeEquipmentHook!.Original(equipOffset, CharEquipSlots.Neck, currentEquipmentSet.Neck);

                    //ChangeWeaponHook!.Original(equipOffset, CharWeaponSlots.MainHand, currentWeaponSet.MainHand, 0, 1, 0, 0);
                    //ChangeWeaponHook!.Original(equipOffset, CharWeaponSlots.OffHand, currentWeaponSet.OffHand, 0, 1, 0, 0);
                    //ChangeWeaponHook!.Original(equipOffset, CharWeaponSlots.uk3, currentWeaponSet.Uk3, 0, 1, 0, 0);

                    RefreshObject((GameObject*)player.Address);
                }

                haveSavedEquipmentSet = false;
            }
        }

        private void ThirdToFirstPersonView()
        {
            Imports.Recenter();

            PlayerCharacter? player = DalamudApi.ClientState.LocalPlayer;
            if (player != null)
            {
                GameObject* bonedObject = (GameObject*)player.Address;
                Character* bonedCharacter = (Character*)player.Address;

                if (bonedCharacter != null)
                {
                    if (haveSavedEquipmentSet == false)
                    {
                        currentEquipmentSet.Save(bonedCharacter);
                        haveSavedEquipmentSet = true;
                    }

                    UInt64 equipOffset = (UInt64)(UInt64*)&bonedCharacter->DrawData;
                    //----
                    // override the head neck and earing
                    //----
                    if (bonedCharacter->DrawData.Head.Variant != 99)
                    {
                        HideHeadValue = bonedCharacter->DrawData.Flags1;
                        bonedCharacter->DrawData.Flags1 = 0;

                        ChangeEquipmentHook!.Original(equipOffset, CharEquipSlots.Head, hiddenEquipHead);
                        ChangeEquipmentHook!.Original(equipOffset, CharEquipSlots.Neck, hiddenEquipNeck);
                        ChangeEquipmentHook!.Original(equipOffset, CharEquipSlots.Ears, hiddenEquipEars);

                        RefreshObject((GameObject*)player.Address);
                    }
                }
            }
        }

        Dictionary<string, BoneList> boneNameToEnum = new Dictionary<string, BoneList>();
        Dictionary<ushort, Bone> boneLayoutA = new Dictionary<ushort, Bone>();

        Dictionary<UInt64, Dictionary<BoneList, short>> boneLayout = new Dictionary<UInt64, Dictionary<BoneList, short>>();
        Dictionary<UInt64, Bone[]> rawBoneList = new Dictionary<UInt64, Bone[]>();
        SortedList<string, bool> reportedBones = new SortedList<string, bool>();
        Dictionary<BoneList, KeyValuePair<int, short>> BoneParentOverrideList = new Dictionary<BoneList, KeyValuePair<int, short>>();

        Dictionary<UInt64, List<KeyValuePair<Vector3, hkQsTransformf>>> boneLayoutT = new Dictionary<UInt64, List<KeyValuePair<Vector3, hkQsTransformf>>>();

        int timer = 100;
        CharEquipSlotData hiddenEquipHead = new CharEquipSlotData(6154, 99, 0);
        CharEquipSlotData hiddenEquipEars = new CharEquipSlotData(0, 0, 0);
        CharEquipSlotData hiddenEquipNeck = new CharEquipSlotData(0, 0, 0);
        //CharWeaponSlotData hiddenEquipWeaponMainHand = new CharWeaponSlotData(0, 0, 0, 0);
        //CharWeaponSlotData hiddenEquipWeaponOffHand = new CharWeaponSlotData(0, 0, 0, 0);

        bool haveSavedEquipmentSet = false;
        CharEquipData currentEquipmentSet = new CharEquipData();
        //CharWeaponData currentWeaponSet = new CharWeaponData();

        //private Dictionary<UInt64, List<KeyValuePair<Vector3, hkQsTransformf>>> boneLayout = new Dictionary<UInt64, List<KeyValuePair<Vector3, hkQsTransformf>>>();
        Dictionary<hkaPose, Dictionary<string, int>> boneNames = new Dictionary<hkaPose, Dictionary<string, int>>();
        Matrix4x4 bridgeLocal = Matrix4x4.Identity;
        Vector3 neckPosition = new Vector3(0, 0, 0);

        public void DrawBoneRay(Matrix4x4 baseMatrix, Bone bone)
        {
            Vector3 vFrom = Vector3.Transform(bone.boneStart, baseMatrix);
            Vector3 vTo = Vector3.Transform(bone.boneFinish, baseMatrix);
            Imports.SetRayCoordinate((float*)&vFrom, (float*)&vTo);
        }

        public void DrawBones(Skeleton* skeleton)
        {
            boneLayout.Clear();
            if (skeleton == null)
                return;

            Matrix4x4 curSkeletonPosition = Matrix4x4.CreateFromQuaternion(skeleton->Transform.Rotation);
            curSkeletonPosition.Translation = skeleton->Transform.Position;
            curSkeletonPosition.SetScale(skeleton->Transform.Scale);

            //----
            // Loops though the skeletal parts and gets the pose layouts
            //----
            for (ushort p = 0; p < skeleton->PartialSkeletonCount; p++)
            {
                hkaPose* objPose = skeleton->PartialSkeletons[p].GetHavokPose(0);
                if (objPose == null)
                    continue;

                UInt64 objPose64 = (UInt64)objPose;
                boneLayout.Add(objPose64, new Dictionary<BoneList, short>());

                //----
                // Loops though the pose bones and updates the ones that have tracking
                //----
                Bone[] boneArray = new Bone[objPose->LocalPose.Length];
                for (short i = 0; i < objPose->LocalPose.Length; i++)
                {
                    string boneName = objPose->Skeleton->Bones[i].Name.String;
                    short parentId = objPose->Skeleton->ParentIndices[i];

                    if (!boneNameToEnum.ContainsKey(boneName))
                    {
                        if (!reportedBones.ContainsKey(boneName))
                        {
                            PluginLog.Log($"{p} {objPose64:X} {i} : Error finding bone {boneName}");
                            reportedBones.Add(boneName, true);
                        }
                        continue;
                    }

                    BoneList boneKey = boneNameToEnum.GetValueOrDefault<string, BoneList>(boneName, BoneList._root_);
                    boneLayout[objPose64].Add(boneKey, i);
                    //PluginLog.Log($"{p} {(UInt64)objPose:X} {i} : {boneName} {boneKey} {parentId}");

                    if (parentId < 0)
                        boneArray[i] = new Bone(boneKey, i, parentId, null, objPose->LocalPose[i], objPose->Skeleton->ReferencePose[i]);
                    else
                        boneArray[i] = new Bone(boneKey, i, parentId, boneArray[parentId], objPose->LocalPose[i], objPose->Skeleton->ReferencePose[i]);

                    DrawBoneRay(curSkeletonPosition, boneArray[i]);
                }
            }
        }

        Matrix4x4 plrSkeletonPosition = Matrix4x4.Identity;
        Matrix4x4 plrSkeletonPositionI = Matrix4x4.Identity;
        Matrix4x4 headBoneMatrix = Matrix4x4.Identity;
        Matrix4x4 headBoneMatrixI = Matrix4x4.Identity;
        Vector3 eyeMidPoint = new Vector3(0, 0, 0);
        Matrix4x4 eyeMidPointM = Matrix4x4.Identity;
        byte HideHeadValue = 0;
        bool hideWeapons = true;

        public void RefreshObject(GameObject* obj2refresh)
        {
            obj2refresh->RenderFlags = 2;
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Interval = 500;
            timer.Elapsed += (sender, e) => { RefreshObjectTick(timer, obj2refresh); };
            timer.Enabled = true;
        }

        public void RefreshObjectTick(System.Timers.Timer timer, GameObject* obj2refresh)
        {
            obj2refresh->RenderFlags = 0;
            timer.Enabled = false;
        }

        class ChangedType<T>
        {
            private T old = default(T);
            public T Current
            {
                get => old;
                set
                {
                    Changed = false;
                    if (!EqualityComparer<T>.Default.Equals(value, old))
                    {
                        old = value;
                        Changed = true;
                    }
                }
            }
            public bool Changed { get; private set; }
            public ChangedType(T newVal = default(T))
            {
                old = newVal;
                Current = newVal;
                Changed = false;
            }
            public ChangedType<T> Set(T newVal)
            {
                Current = newVal;
                return this;
            }
        }

        class ChangedTypeBool
        {
            private bool old = false;
            public bool Current
            {
                get => old;
                set
                {
                    Changed = !(value == old);
                    old = value;
                }
            }
            public bool Changed { get; private set; }
            public ChangedTypeBool(bool newVal = false)
            {
                old = newVal;
                Current = newVal;
                Changed = false;
            }
            public ChangedTypeBool Set(bool newVal)
            {
                Current = newVal;
                return this;
            }
        }

        private ChangedType<CameraModes> gameMode = new ChangedType<CameraModes>(CameraModes.None);


        bool outputBonesOnce = false;
        public void Update(Dalamud.Game.Framework framework_)
        {
            if (hooksSet)
            {
                Imports.UpdateController(controllerCallback);

                if (curEye == 0)
                {
                    hmdMatrix = Imports.GetFramePose(poseType.hmdPosition, -1);
                    lhcMatrix = Imports.GetFramePose(poseType.LeftHand, -1);
                    rhcMatrix = Imports.GetFramePose(poseType.RightHand, -1);
                    Matrix4x4.Invert(hmdMatrix, out hmdMatrixI);
                }
                Matrix4x4 hmdFlip = Matrix4x4.CreateFromAxisAngle(new Vector3(0, 1, 0), 90 * Deg2Rad) * Matrix4x4.CreateFromAxisAngle(new Vector3(0, 0, 1), -90 * Deg2Rad);
                Matrix4x4 hmdMatrixBody = hmdFlip * hmdMatrixI;
                Matrix4x4 lhcMatrixCXZ = convertXZ * lhcMatrix * convertXZ;
                Matrix4x4 rhcMatrixCXZ = convertXZ * rhcMatrix * convertXZ;

                frfCalculateViewMatrix = false;


                ScreenSettings* screenSettings = *(ScreenSettings**)((UInt64)frameworkInstance + 0x7A8);
                Point halfScreen = new Point();
                halfScreen.X = ((int)Device.Instance()->SwapChain->Width / 2);
                halfScreen.Y = ((int)Device.Instance()->SwapChain->Height / 2);
                //PluginLog.Log($"{(int)Device.Instance()->SwapChain->Height} {(int)Device.Instance()->SwapChain->Width}");
                Point currentMouse = new Point();
                Imports.GetCursorPos(out currentMouse);
                Imports.ScreenToClient((IntPtr)screenSettings->hWnd, out currentMouse);

                int mouseMultiplyer = 3;
                //if (dalamudMode)
                //    mouseMultiplyer = 1;

                //----
                // Changes anchor from top left corner to middle of screen
                //----
                virtualMouse.X = halfScreen.X + ((currentMouse.X - halfScreen.X) * mouseMultiplyer);
                virtualMouse.Y = halfScreen.Y + ((currentMouse.Y - halfScreen.Y) * mouseMultiplyer);


                if (gameMode.Current == CameraModes.ThirdPerson && gameMode.Changed == true)
                    FirstToThirdPersonView();
                else if (gameMode.Current == CameraModes.FirstPerson && gameMode.Changed == true)
                    ThirdToFirstPersonView();

                isMounted = false;
                housingMode = false;

                if (curEye == 0)
                    rawBoneList.Clear();

                PlayerCharacter? player = DalamudApi.ClientState.LocalPlayer;
                //GameObject* bonedObject = charList->Character0;
                //PluginLog.Log($"BoneObject {(UInt64)bonedObject:X}");
                if (player != null)
                {
                    GameObject* bonedObject = (GameObject*)player.Address;
                    Character* bonedCharacter = (Character*)player.Address;
                    isMounted = bonedCharacter->IsMounted();
                }

                //if (player != null && curEye == 0 && gameMode.Current == CameraModes.FirstPerson)
                if (player != null && gameMode.Current == CameraModes.FirstPerson)
                {
                    GameObject* bonedObject = (GameObject*)player.Address;
                    Character* bonedCharacter = (Character*)player.Address;


                    if (bonedObject != null)
                    {
                        Vector3* objectModelPos = GameObjectGetPositionFn!(bonedObject);
                        float bodyHeight = firstPersonCameraHeight - objectModelPos->Y;

                        //----
                        // Gets the skeletal system
                        //----
                        Model* model = (Model*)bonedObject->DrawObject;
                        if (model != null)
                        {
                            Skeleton* skeleton = model->skeleton;
                            if (skeleton != null)
                            {
                                plrSkeletonPosition = Matrix4x4.CreateFromQuaternion(skeleton->Transform.Rotation);
                                plrSkeletonPosition.Translation = skeleton->Transform.Position;
                                plrSkeletonPosition.SetScale(skeleton->Transform.Scale);
                                Matrix4x4.Invert(plrSkeletonPosition, out plrSkeletonPositionI);
                                boneLayout.Clear();

                                //----
                                // Loops though the skeletal parts and gets the pose layouts
                                //----
                                for (ushort p = 0; p < skeleton->PartialSkeletonCount; p++)
                                {
                                    hkaPose* objPose = skeleton->PartialSkeletons[p].GetHavokPose(0);
                                    if (objPose == null)
                                        continue;

                                    UInt64 objPose64 = (UInt64)objPose;
                                    boneLayout.Add(objPose64, new Dictionary<BoneList, short>());

                                    //----
                                    // Loops though the pose bones and updates the ones that have tracking
                                    //----
                                    Bone[] boneArray = new Bone[objPose->LocalPose.Length];
                                    for (short i = 0; i < objPose->LocalPose.Length; i++)
                                    {
                                        string boneName = objPose->Skeleton->Bones[i].Name.String;
                                        short parentId = objPose->Skeleton->ParentIndices[i];

                                        if (!boneNameToEnum.ContainsKey(boneName))
                                        {
                                            if (!reportedBones.ContainsKey(boneName))
                                            {
                                                PluginLog.Log($"{p} {objPose64:X} {i} : Error finding bone {boneName}");
                                                reportedBones.Add(boneName, true);
                                            }
                                            continue;
                                        }

                                        BoneList boneKey = boneNameToEnum.GetValueOrDefault<string, BoneList>(boneName, BoneList._root_);
                                        boneLayout[objPose64].Add(boneKey, i);
                                        //PluginLog.Log($"{p} {(UInt64)objPose:X} {i} : {boneName} {boneKey} {parentId}");

                                        if (parentId < 0)
                                            boneArray[i] = new Bone(boneKey, i, parentId, null, objPose->LocalPose[i], objPose->Skeleton->ReferencePose[i]);
                                        else
                                            boneArray[i] = new Bone(boneKey, i, parentId, boneArray[parentId], objPose->LocalPose[i], objPose->Skeleton->ReferencePose[i]);

                                        //boneArray[i].SetTransformFromLocalBase();

                                        if (outputBonesOnce == false)
                                        {
                                            //boneArray[i].SetReference(false);
                                            //boneArray[i].OutputToParent(false);

                                            /*
                                            PluginLog.Log($"Bone {i}/{objPose->LocalPose.Length} Name {boneName}");
                                            PluginLog.Log($"{boneArray[i].transform.Rotation.X}, {boneArray[i].transform.Rotation.Y}, {boneArray[i].transform.Rotation.Z}, {boneArray[i].transform.Rotation.W}");
                                            PluginLog.Log($"{boneArray[i].transform.Translation.X}, {boneArray[i].transform.Translation.Y}, {boneArray[i].transform.Translation.Z}, {boneArray[i].transform.Translation.W}");
                                            PluginLog.Log($"-");
                                            PluginLog.Log($"{boneMatrix.M11}, {boneMatrix.M12}, {boneMatrix.M13}, {boneMatrix.M14}");
                                            PluginLog.Log($"{boneMatrix.M21}, {boneMatrix.M22}, {boneMatrix.M23}, {boneMatrix.M24}");
                                            PluginLog.Log($"{boneMatrix.M31}, {boneMatrix.M32}, {boneMatrix.M33}, {boneMatrix.M34}");
                                            PluginLog.Log($"{boneMatrix.M41}, {boneMatrix.M42}, {boneMatrix.M43}, {boneMatrix.M44}");
                                            PluginLog.Log($"-");
                                            PluginLog.Log($"{quatMat.X}, {quatMat.Y}, {quatMat.Z}, {quatMat.W}");
                                            PluginLog.Log($"{vecMat.X}, {vecMat.Y}, {vecMat.Z}, 0");
                                            PluginLog.Log($"-");
                                            */
                                        }
                                    }
                                    rawBoneList[objPose64] = boneArray;

                                    if (outputBonesOnce == false)
                                    {
                                        //Matrix4x4 boneMatI = boneArray[0].ConvertToLocal(rhcMatrix);
                                        /*
                                        PluginLog.Log($"-");
                                        PluginLog.Log($"{rhcMatrix.M11}, {rhcMatrix.M12}, {rhcMatrix.M13}, {rhcMatrix.M14}");
                                        PluginLog.Log($"{rhcMatrix.M21}, {rhcMatrix.M22}, {rhcMatrix.M23}, {rhcMatrix.M24}");
                                        PluginLog.Log($"{rhcMatrix.M31}, {rhcMatrix.M32}, {rhcMatrix.M33}, {rhcMatrix.M34}");
                                        PluginLog.Log($"{rhcMatrix.M41}, {rhcMatrix.M42}, {rhcMatrix.M43}, {rhcMatrix.M44}");
                                        PluginLog.Log($"-");
                                        PluginLog.Log($"{boneMatI.M11}, {boneMatI.M12}, {boneMatI.M13}, {boneMatI.M14}");
                                        PluginLog.Log($"{boneMatI.M21}, {boneMatI.M22}, {boneMatI.M23}, {boneMatI.M24}");
                                        PluginLog.Log($"{boneMatI.M31}, {boneMatI.M32}, {boneMatI.M33}, {boneMatI.M34}");
                                        PluginLog.Log($"{boneMatI.M41}, {boneMatI.M42}, {boneMatI.M43}, {boneMatI.M44}");
                                        PluginLog.Log($"-");
                                        */

                                        //outputBonesOnce = true;
                                        //boneArray[0].SetReferenceChildren();
                                        //boneArray[0].Output();
                                        //boneArray[0].Output();
                                    }
                                    //rawBoneList[objPose64][0].ScaleAll(rawBoneList[objPose64], 0, 0, 0);

                                    short rootBone = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_root, -1);
                                    short headBone = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_head, -1);

                                    if (rootBone >= 0)
                                    {

                                        //rootBonePos = boneArray[rootBone].boneFinish;
                                        //rootBonePos.Y -= firstPersonCameraHeight;
                                        short abdomen = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_abdomen, -1);

                                        short spineA = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_spine_a, -1);
                                        short spineB = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_spine_b, -1);
                                        short spineC = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_spine_c, -1);
                                        short neck = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_neck, -1);

                                        short collarboneL = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_collarbone_l, -1);
                                        short armL = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_arm_l, -1);
                                        short forearmL = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_forearm_l, -1);
                                        short elbowL = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_elbow_l, -1);
                                        short handL = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_hand_l, -1);
                                        short wristL = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_wrist_l, -1);

                                        short collarboneR = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_collarbone_r, -1);
                                        short armR = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_arm_r, -1);
                                        short forearmR = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_forearm_r, -1);
                                        short elbowR = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_elbow_r, -1);
                                        short handR = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_hand_r, -1);
                                        short wristR = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_wrist_r, -1);


                                        short scabbardL = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_scabbard_l, -1);
                                        short sheatheL = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_sheathe_l, -1);
                                        short scabbardR = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_scabbard_r, -1);
                                        short sheatheR = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_sheathe_r, -1);

                                        short weaponL = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_weapon_l, -1);
                                        short weaponR = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_weapon_r, -1);

                                        //if (neck >= 0)
                                        //    boneArray[neck].SetReference(true, true);

                                        //DrawBoneRay(curSkeletonPosition, boneArray[headBone]);

                                        //PluginLog.Log($"{boneArray[rootBone].boneFinish.X} {boneArray[rootBone].boneFinish.Y} {boneArray[rootBone].boneFinish.Z} || {boneArray[rootBone].transform.Translation.X} {boneArray[rootBone].transform.Translation.Y} {boneArray[rootBone].transform.Translation.Z}");

                                        //Quaternion q = Quaternion.CreateFromRotationMatrix(boneArray[clothBABone].boneMatrix);
                                        //Vector3 v = boneArray[clothBABone].boneMatrix.Translation;

                                        //Matrix4x4.Invert(boneArray[clothBABone].parent.boneMatrix, out Matrix4x4 invP);
                                        //Matrix4x4.Invert(boneArray[clothBABone].boneMatrix, out Matrix4x4 inv);
                                        //Matrix4x4 itm = invP * inv;
                                        //Matrix4x4 itm = boneArray[clothBABone].boneMatrix * invP;
                                        //Matrix4x4.Invert(itm, out Matrix4x4 itmI);
                                        //q = Quaternion.CreateFromRotationMatrix(itmI);
                                        //v = itmI.Translation;
                                        //Vector4 t = Vector4.Transform(Vector4.One, itmI);
                                        //PluginLog.Log($"A {boneArray[clothBABone].transform.Rotation.X} {boneArray[clothBABone].transform.Rotation.Y} {boneArray[clothBABone].transform.Rotation.Z} {boneArray[clothBABone].transform.Rotation.W} -- {boneArray[clothBABone].transform.Translation.X} {boneArray[clothBABone].transform.Translation.Y} {boneArray[clothBABone].transform.Translation.Z} {boneArray[clothBABone].transform.Translation.W}");
                                        //PluginLog.Log($"B {q.X} {q.Y} {q.Z} {q.W} -- {v.X} {v.Y} {v.Z} 0");

                                        //0x141733460 - hkQsTransformf.?setAxisAngle@hkQuaternionf@@QEAAXAEBVhkVector4f@@AEBVhkSimdFloat32@@@Z

                                        if (isMounted == false && abdomen >= 0)
                                        {
                                            if (xivr.cfg.data.immersiveMovement == false && xivr.cfg.data.immersiveFull == false)
                                            {
                                                Vector3 angles = new Vector3(0, 0, 0);
                                                if (xivr.cfg.data.conloc)
                                                    angles = GetAngles(lhcMatrix);
                                                Matrix4x4 revOnward = Matrix4x4.CreateFromAxisAngle(new Vector3(0, 1, 0), -angles.Y);

                                                boneArray[abdomen].SetReference(true, false);
                                                boneArray[spineA].SetReference(true, true);

                                                boneArray[spineA].updateRotation = true;
                                                boneArray[spineA].transform.Rotation = (boneArray[spineA].transform.Rotation.Convert() * Quaternion.CreateFromAxisAngle(new Vector3(1, 0, 0), angles.Y)).Convert();

                                                if (xivr.cfg.data.conloc == false && xivr.cfg.data.hmdloc == false)
                                                {
                                                    boneArray[spineA].updatePosition = true;
                                                    boneArray[spineA].transform.Translation.X = -(hmdMatrix.Translation.X * 0.5f);
                                                    boneArray[spineA].transform.Translation.Y = (hmdMatrix.Translation.Y * 0.5f);
                                                    boneArray[spineA].transform.Translation.Z = -(hmdMatrix.Translation.Z * 0.5f);
                                                    boneArray[spineA].transform.Translation.W = 0;
                                                }
                                            }
                                        }


                                        if (scabbardL >= 0) boneArray[scabbardL].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));
                                        if (scabbardR >= 0) boneArray[scabbardR].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));
                                        if (sheatheL >= 0) boneArray[sheatheL].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));
                                        if (sheatheR >= 0) boneArray[sheatheR].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));

                                        if (isMounted == false && handL >= 0 && xivr.cfg.data.motioncontrol)
                                        {
                                            boneArray[armL].SetReference(false, true);
                                            boneArray[armL].transform.Rotation = Quaternion.CreateFromYawPitchRoll(-90 * Deg2Rad, 180 * Deg2Rad, 90 * Deg2Rad).Convert();
                                            boneArray[forearmL].transform.Rotation = Quaternion.CreateFromYawPitchRoll(0, 0, -90 * Deg2Rad).Convert();
                                            boneArray[handL].transform.Rotation = Quaternion.Identity.Convert();
                                            boneArray[collarboneL].CalculateMatrix(true);

                                            Matrix4x4 lhc = lhcMatrixCXZ;
                                            //lhc *= bridgeLocal;
                                            lhc *= boneArray[headBone].localMatrix;
                                            lhc *= boneArray[neck].localMatrix;
                                            lhc *= boneArray[collarboneL].localMatrixI;
                                            lhc *= boneArray[armL].localMatrixI;
                                            lhc *= boneArray[forearmL].localMatrixI;
                                            lhc = lhc.SetScale(boneArray[handL].transform.Scale);
                                            boneArray[armL].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));

                                            boneArray[handL].SetTransform(lhc);
                                            boneArray[wristL].SetTransform(lhc);
                                            boneArray[handL].SetScale(new Vector3(10000f, 10000f, 10000f));
                                        }
                                        if (isMounted == false && handR >= 0 && xivr.cfg.data.motioncontrol)
                                        {
                                            boneArray[armR].SetReference(false, true);
                                            boneArray[armR].transform.Rotation = Quaternion.CreateFromYawPitchRoll(90 * Deg2Rad, 180 * Deg2Rad, 90 * Deg2Rad).Convert();
                                            boneArray[forearmR].transform.Rotation = Quaternion.CreateFromYawPitchRoll(0, 0, -90 * Deg2Rad).Convert();
                                            boneArray[handR].transform.Rotation = Quaternion.Identity.Convert();
                                            boneArray[collarboneR].CalculateMatrix(true);

                                            Matrix4x4 rhc = rhcMatrixCXZ;
                                            //rhc *= bridgeLocal;
                                            rhc *= boneArray[headBone].localMatrix;
                                            rhc *= boneArray[neck].localMatrix;
                                            rhc *= boneArray[collarboneR].localMatrixI;
                                            rhc *= boneArray[armR].localMatrixI;
                                            rhc *= boneArray[forearmR].localMatrixI;
                                            rhc = rhc.SetScale(boneArray[handR].transform.Scale);
                                            boneArray[armR].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));

                                            boneArray[handR].SetTransform(rhc);
                                            boneArray[wristR].SetTransform(rhc);
                                            boneArray[handR].SetScale(new Vector3(10000f, 10000f, 10000f));
                                        }
                                        if (weaponL >= 0 && weaponR >= 0 && xivr.cfg.data.showWeaponInHand == false && xivr.cfg.data.motioncontrol)
                                        {
                                            boneArray[weaponL].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));
                                            boneArray[weaponR].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));
                                        }
                                        if (neck >= 0)
                                        {
                                            neckPosition = boneArray[neck].boneMatrix.Translation;// Vector3.Transform(boneArray[neck].boneStart, plrSkeletonPosition);
                                            //Matrix4x4.Invert(headBoneMatrix * curViewMatrixWithoutHMD, out eyeMidPointM);
                                            //Matrix4x4 fullNeck = plrSkeletonPosition;// * boneArray[neck].boneMatrix;
                                            //neckPosition = fullNeck.Translation; //boneArray[neck].boneMatrix.Translation;
                                            //boneArray[neck].SetReference();
                                            //boneArray[neck].transform.Translation = new Vector3(0, 0, 0).Convert();
                                            //boneArray[neck].transform.Rotation = Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), 180 * Deg2Rad).Convert();
                                            //boneArray[neck].transform.Scale = new Vector3(0.0001f, 0.0001f, 0.0001f).Convert();
                                        }
                                        if (headBone >= 0)
                                        {
                                            //headBoneMatrix = boneArray[headBone].boneMatrix;// * plrSkeletonPosition;
                                            headBoneMatrix = boneArray[headBone].localMatrix * boneArray[neck].localMatrixI;
                                            //headBoneMatrix.Translation *= eyeMidPoint;
                                            headBoneMatrix *= boneArray[neck].boneMatrix;
                                            headBoneMatrix *= plrSkeletonPosition;
                                            hkQsTransformf identTrans = new hkQsTransformf();
                                            identTrans.Translation = boneArray[headBone].transform.Translation;
                                            identTrans.Rotation = Quaternion.Identity.Convert();
                                            identTrans.Scale = new Vector3(0.0001f, 0.0001f, 0.0001f).Convert();
                                            boneArray[headBone].SetTransform(identTrans, false, true);

                                            boneArray[neck].updateRotation = true;
                                            boneArray[neck].transform.Rotation = Quaternion.CreateFromYawPitchRoll(0, 0, 180 * Deg2Rad).Convert();
                                        }
                                    }
                                    else if (headBone >= 0)
                                    {
                                        short bridge = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_bridge, -1);
                                        short eyeL = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_eye_l, -1);
                                        short eyeR = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_eye_r, -1);

                                        boneArray[headBone].SetReference(true, true);
                                        if (eyeL >= 0 && eyeR >= 0)
                                        {
                                            eyeMidPoint = ((boneArray[eyeL].boneMatrix.Translation + boneArray[eyeR].boneMatrix.Translation) / 2.0f) - boneArray[headBone].boneMatrix.Translation;
                                        }


                                        if (bridge >= 0)
                                        {
                                            bridgeLocal = boneArray[bridge].localMatrix;
                                            //headBoneMatrix = boneArray[headBone].boneMatrix;// * plrSkeletonPosition * hmdMatrix;
                                            //headBoneMatrix = headBoneMatrix * plrSkeletonPosition; //.Translation + headBoneMatrix.Translation;// boneArray[bridge].boneMatrix.Translation;
                                            //Matrix4x4 eyeMidM = boneArray[headBone].boneMatrix;
                                            //eyeMidM.Translation = headBonePosition;
                                            //PluginLog.Log($"{boneArray[eyeL].boneMatrix.Translation.X} {boneArray[eyeR].boneMatrix.Translation.X} - {eyeMid.X.ToString("0.00000000")} {eyeMid.Y} {eyeMid.Z}");
                                            //headBoneMatrixI = boneArray[bridge].boneMatrixI;

                                            //hmdMatrix = plrSkeletonPositionI * headBoneMatrix;
                                            //Matrix4x4.Invert(hmdMatrix, out hmdMatrixI);
                                            if (xivr.cfg.data.immersiveFull)
                                            {
                                                hmdMatrix.M42 = (xivr.cfg.data.offsetAmountYFPS / 100);
                                                hmdMatrix.M43 = (xivr.cfg.data.offsetAmountZFPS / 100);
                                                hmdMatrix = hmdMatrix * hmdFlip * headBoneMatrix * curViewMatrixWithoutHMD;
                                                Matrix4x4.Invert(hmdMatrix, out hmdMatrixI);
                                            }
                                        }

                                        //DrawBoneRay(curSkeletonPosition, boneArray[eyeLeftBone]);
                                        //DrawBoneRay(curSkeletonPosition, boneArray[eyeRightBone]);

                                        hkQsTransformf identTrans = new hkQsTransformf();
                                        identTrans.Translation = new Vector3(0, 0, 0).Convert();
                                        identTrans.Rotation = Quaternion.Identity.Convert();
                                        identTrans.Scale = new Vector3(0.0001f, 0.0001f, 0.0001f).Convert();
                                        boneArray[0].SetTransform(identTrans, false, true);
                                    }
                                }
                            }


                            /*
                            //----
                            // Change hair to bald
                            //----
                            if(outputBonesOnce == false)
                            {
                                CharCustData* customData = (CharCustData*)((Character*)bonedObject)->CustomizeData;
                                customData->HairStyle = 255;
                                customData->FaceType = 255;
                                
                                bool done = ((Human*)bonedObject->DrawObject)->UpdateDrawData(customData->Data, true);
                                //bonedObject->DisableDraw();
                                //bonedObject->EnableDraw();

                                //PluginLog.Log($"{done} {(UInt64)customData->Data:X} {customData->Race} {customData->Height} {customData->FaceType}");
                            }*/
                            outputBonesOnce = true;
                            //DrawBones(skeleton);
                        }
                    }


                }

                if (gameMode.Current == CameraModes.FirstPerson)
                {
                    //----
                    // Draws Skeletal overlay for all models
                    // to get the full bone list
                    //----
                    int objCount = DalamudApi.ObjectTable.Length;
                    for (int i = 0; i < objCount; i++)
                    {
                        Dalamud.Game.ClientState.Objects.Types.GameObject? tmpObj = DalamudApi.ObjectTable[i];
                        if (tmpObj == null)
                            continue;

                        if ((int)tmpObj.ObjectKind == 1 || //player
                            (int)tmpObj.ObjectKind == 2 || // BattleNpc
                            (int)tmpObj.ObjectKind == 3 || // EventNpc
                            (int)tmpObj.ObjectKind == 8 || // mount
                            (int)tmpObj.ObjectKind == 9 || // Companion
                            (int)tmpObj.ObjectKind == 10) // Retainer
                        {
                            GameObject* bonedObject = (GameObject*)tmpObj.Address;
                            if (bonedObject == null)
                                continue;

                            Model* model = (Model*)bonedObject->DrawObject;
                            if (model == null)
                                continue;

                            //DrawBones(model->skeleton);
                        }
                    }
                }
                /*
                //----
                // Detects if over a ui element by checking inputdata isnt 0
                // and run haptics if a change occurs
                //----
                UInt64 framework = (UInt64)frameworkInstance;
                UIModule* uiModule = frameworkInstance->GetUiModule();
                UInt64 inputData = (UInt64)uiModule->GetUIInputData();
                int gameXLeft = *(int*)(framework + 0x9F8);
                int uiXLeft = *(int*)(inputData + 0x498);

                mouseoverUI.Current = (gameXLeft == uiXLeft);
                if (mouseoverUI.Changed)
                    Imports.HapticFeedback(ActionButtonLayout.haptics_right, 0.1f, 1.0f, 0.25f);
                */

                //----
                // Haptics if mouse over target changes
                //----
                mouseoverTarget.Current = (targetSystem->MouseOverTarget != null);
                if (mouseoverTarget.Current && mouseoverTarget.Changed)
                    Imports.HapticFeedback(ActionButtonLayout.haptics_right, 0.1f, 1.0f, 0.25f);
            }


            //----
            // Saves the target arrow alpha
            //----
            if (targetAddonAlpha == 0)
            {
                AtkUnitBase* targetAddon = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("_TargetCursor", 1);
                if (targetAddon != null)
                    targetAddonAlpha = targetAddon->Alpha;
            }

            AtkUnitBase* CharSelectAddon = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("_CharaSelectTitle", 1);
            AtkUnitBase* CharMakeAddon = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("_CharaMakeTitle", 1);
            AtkUnitBase* HousingGoods = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("HousingGoods", 1);

            if (CharSelectAddon == null && CharMakeAddon == null && DalamudApi.ClientState.LocalPlayer == null)
                timer = 100;

            if (timer > 0)
            {
                forceFloatingScreen = true;
                timer--;
            }
            else if (timer == 0)
            {
                timer = -1;

                int objBorder = ConfigModule.Instance()->GetIntValue(ConfigOption.ObjectBorderingType);
                if (objBorder == 0)
                    ConfigModule.Instance()->SetOption(ConfigOption.ObjectBorderingType, 1);
            }


            if (HousingGoods != null)
                housingMode = true;

            if (curRenderMode == RenderModes.TwoD)
                curEye = 0;
            else
                curEye = nextEye[curEye];
            //SetFramePose();
            //PluginLog.Log($"-- Update --  {curEye}");
        }

        public void ForceFloatingScreen(bool forceFloating, bool isCutscene)
        {
            forceFloatingScreen = forceFloating;
            inCutscene = isCutscene;
        }

        public void SetRotateAmount(float x, float y)
        {
            rotateAmount.X = (x * Deg2Rad);
            rotateAmount.Y = (y * Deg2Rad);
        }

        public Point GetWindowSize()
        {
            Rectangle rectangle = new Rectangle();
            ScreenSettings* screenSettings = *(ScreenSettings**)((UInt64)frameworkInstance + 0x7A8);
            Imports.GetClientRect((IntPtr)screenSettings->hWnd, out rectangle);
            return new Point(rectangle.Width, rectangle.Height);
        }

        public void WindowResize(int width, int height)
        {
            //----
            // Resizes the internal buffers
            //----
            Device* dev = Device.Instance();
            dev->NewWidth = (uint)width;
            dev->NewHeight = (uint)height;
            dev->RequestResolutionChange = 1;

            //----
            // Resizes the client window to match the internal buffers
            //----
            ScreenSettings* screenSettings = *(ScreenSettings**)((UInt64)frameworkInstance + 0x7A8);
            Imports.ResizeWindow((IntPtr)screenSettings->hWnd, width, height);
        }


        public void SetRenderingMode()
        {
            if (hooksSet == true)
            {
                RenderModes rMode = curRenderMode;
                if (xivr.cfg.data.mode2d)
                    rMode = RenderModes.TwoD;
                else
                    rMode = RenderModes.AlternatEye;

                if (rMode != curRenderMode)
                {
                    curRenderMode = rMode;

                    if (curRenderMode == RenderModes.TwoD)
                    {
                        eyeOffsetMatrix[0] = Matrix4x4.Identity;
                        eyeOffsetMatrix[1] = Matrix4x4.Identity;
                    }
                    else
                    {
                        Matrix4x4.Invert(Imports.GetFramePose(poseType.EyeOffset, 0), out eyeOffsetMatrix[0]);
                        Matrix4x4.Invert(Imports.GetFramePose(poseType.EyeOffset, 1), out eyeOffsetMatrix[1]);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (xivr.cfg.data.vLog)
                PluginLog.Log($"Dispose A {initalized} {hooksSet}");
            getThreadedDataHandle.Free();
            initalized = false;
            if (xivr.cfg.data.vLog)
                PluginLog.Log($"Dispose B {initalized} {hooksSet}");
        }

        private void AddClearCommand()
        {
            UInt64 threadedOffset = GetThreadedOffset();
            if (threadedOffset != 0)
            {
                UInt64 queueData = AllocateQueueMemmoryFn!(threadedOffset, 0x38);
                if (queueData != 0)
                {
                    stRenderQueueCommandClear* cmd = (stRenderQueueCommandClear*)queueData;
                    cmd->Clear();
                    cmd->clearType = 1;
                    cmd->colorR = 0;
                    cmd->colorG = 0;
                    cmd->colorB = 0;
                    cmd->colorA = 0;
                    cmd->unkn1 = 1;
                    PushbackFn!((threadedOffset + 0x18), (UInt64)(*(int*)(threadedOffset + 0x8)), queueData);
                }
            }
        }


        /*
        //----
        // CreateHuman Create
        //---- 0x300960 - create battle character
        private delegate void CreateHumanCreateDg(UInt64 a, UInt64 b, uint* c, byte d);
        [Signature("E8 ?? ?? ?? ?? 48 8B F8 48 85 C0 74 28 48 8D 55 D7", DetourName = nameof(CreateHumanCreateFn))]
        private Hook<CreateHumanCreateDg>? CreateHumanCreateHook = null;

        [HandleStatus("CreateHumanCreate")]
        public void CreateHumanCreateStatus(bool status)
        {
            if (status == true)
                CreateHumanCreateHook?.Enable();
            else
                CreateHumanCreateHook?.Disable();
        }
        private void CreateHumanCreateFn(UInt64 a, UInt64 b, uint* c, byte d)
        {
            PluginLog.Log($"CreateHumanCreateFn {a:X} {b} {(UInt64)c} {d}");
            CreateHumanCreateHook!.Original(a, b, c, d);
        }
        */



        //----
        // GetThreadedData
        //----
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate UInt64 GetThreadedDataDg();
        GetThreadedDataDg GetThreadedDataFn;

        public void GetThreadedDataInit()
        {
            //----
            // Used to access gs:[00000058] until i can do it in c#
            //----
            getThreadedDataHandle = GCHandle.Alloc(GetThreadedDataASM, GCHandleType.Pinned);
            if (!Imports.VirtualProtectEx(Process.GetCurrentProcess().Handle, getThreadedDataHandle.AddrOfPinnedObject(), (UIntPtr)GetThreadedDataASM.Length, 0x40 /* EXECUTE_READWRITE */, out uint _))
                return;
            else
                if (!Imports.FlushInstructionCache(Process.GetCurrentProcess().Handle, getThreadedDataHandle.AddrOfPinnedObject(), (UIntPtr)GetThreadedDataASM.Length))
                return;

            GetThreadedDataFn = Marshal.GetDelegateForFunctionPointer<GetThreadedDataDg>(getThreadedDataHandle.AddrOfPinnedObject());
        }

        private UInt64 GetThreadedOffset()
        {
            UInt64 threadedData = GetThreadedDataFn();
            if (threadedData != 0)
            {
                threadedData = *(UInt64*)(threadedData + (UInt64)((*(int*)tls_index) * 8));
                threadedData = *(UInt64*)(threadedData + 0x250);
            }
            return threadedData;
        }

        //----
        // GameObjectGetPosition
        //----
        private delegate Vector3* GameObjectGetPositionDg(GameObject* item);
        [Signature(Signatures.GameObjectGetPosition, Fallibility = Fallibility.Fallible)]
        private GameObjectGetPositionDg? GameObjectGetPositionFn = null;

        //----
        // TargetSystem.GetMouseOverTarget
        //----
        private delegate UInt64 GetMouseOverTargetDg(TargetSystem* targetSystem, int CoordX, int CoordY, int* TargetsInRange, UInt64 targetList);
        [Signature(Signatures.GetMouseOverTarget, Fallibility = Fallibility.Fallible)]
        private GetMouseOverTargetDg? GetMouseOverTargetFn = null;

        //----
        // SetRenderTarget
        //----
        private delegate void SetRenderTargetDg(UInt64 a, UInt64 b, Structures.Texture** c, UInt64 d, UInt64 e, UInt64 f);
        [Signature(Signatures.SetRenderTarget, Fallibility = Fallibility.Fallible)]
        private SetRenderTargetDg? SetRenderTargetFn = null;

        //----
        // AllocateQueueMemory
        //----
        private delegate UInt64 AllocateQueueMemoryDg(UInt64 a, UInt64 b);
        [Signature(Signatures.AllocateQueueMemory, Fallibility = Fallibility.Fallible)]
        private AllocateQueueMemoryDg? AllocateQueueMemmoryFn = null;

        //----
        // GetCutsceneCameraOffset
        //----
        private delegate UInt64 GetCutsceneCameraOffsetDg(UInt64 a);
        [Signature(Signatures.GetCutsceneCameraOffset, Fallibility = Fallibility.Fallible)]
        private GetCutsceneCameraOffsetDg? GetCutsceneCameraOffsetFn = null;

        //----
        // Pushback
        //----
        private delegate void PushbackDg(UInt64 a, UInt64 b, UInt64 c);
        [Signature(Signatures.Pushback, Fallibility = Fallibility.Fallible)]
        private PushbackDg? PushbackFn = null;

        //----
        // PushbackUI
        //----
        private delegate void PushbackUIDg(UInt64 a, UInt64 b);
        [Signature(Signatures.PushbackUI, DetourName = nameof(PushbackUIFn))]
        private Hook<PushbackUIDg>? PushbackUIHook = null;

        [HandleStatus("PushbackUI")]
        public void PushbackUIStatus(bool status)
        {
            if (status == true)
                PushbackUIHook?.Enable();
            else
                PushbackUIHook?.Disable();
        }
        private void PushbackUIFn(UInt64 a, UInt64 b)
        {
            Structures.Texture* texture = Imports.GetUIRenderTexture(curEye);
            UInt64 threadedOffset = GetThreadedOffset();
            SetRenderTargetFn!(threadedOffset, 1, &texture, 0, 0, 0);
            AddClearCommand();

            overrideFromParent.Push(true);
            PushbackUIHook!.Original(a, b);
            overrideFromParent.Pop();
        }


        /*
        //----
        // GetTargetFromRay
        //----
        private delegate bool GetTargetFromRayDg(TargetSystem* targetSystem, Vector3* ray, Vector3* position, UInt64* target);
        //[Signature(Signatures.GetTargetFromRay, Fallibility = Fallibility.Fallible)]
        //private GetTargetFromRayDg? GetTargetFromRayFn = null;
        [Signature(Signatures.GetTargetFromRay, DetourName = nameof(GetTargetFromRayFn))]
        private Hook<GetTargetFromRayDg>? GetTargetFromRayHook = null;

        [HandleStatus("GetTargetFromRay")]
        public void GetTargetFromRayStatus(bool status)
        {
            if (status == true)
                GetTargetFromRayHook?.Enable();
            else
                GetTargetFromRayHook?.Disable();
        }

        private bool GetTargetFromRayFn(TargetSystem* targetSystem, Vector3* position1, Vector3* position2, UInt64* target)
        {
            //PluginLog.Log($"GetTargetFromRayFn {(UInt64)targetSystem:X} | {position1[0]}, {position1[1]}, {position1[2]} | {position2[0]}, {position2[1]}, {position2[2]} | {(UInt64)target:X}");
            bool retVal = GetTargetFromRayHook!.Original(targetSystem, position1, position2, target);
            //if (position2->Y > 0)
            //    Imports.SetRayCoordinate((float*)position1, (float*)position2);
            //PluginLog.Log($"GetTargetFromRayFn {(UInt64)target:X} | {position1->X}, {position1->Y}, {position1->Z} | {position2->X}, {position2->Y}, {position2->Z} | {retVal}");
            return retVal;
        }
        */

        //----
        // ScreenPonitToRay
        //----
        private delegate Ray* ScreenPointToRayDg(RawGameCamera* gameCamera, Ray* ray, int mousePosX, int mousePosY);
        [Signature(Signatures.ScreenPointToRay, DetourName = nameof(ScreenPointToRayFn))]
        private Hook<ScreenPointToRayDg> ScreenPointToRayHook = null;

        [HandleStatus("ScreenPonitToRay")]
        public void ScreenPonitToRayStatus(bool status)
        {
            if (status == true)
                ScreenPointToRayHook?.Enable();
            else
                ScreenPointToRayHook?.Disable();
        }
        private Ray* ScreenPointToRayFn(RawGameCamera* gameCamera, Ray* ray, int mousePosX, int mousePosY)
        {
            if (xivr.cfg.data.motioncontrol)
            {
                Matrix4x4 rayPos = rhcMatrix * curViewMatrixWithoutHMDI;
                Vector3 frwdFar = new Vector3(rayPos.M31, rayPos.M32, rayPos.M33) * -1;
                ray->Origin = rayPos.Translation;
                ray->Direction = Vector3.Normalize(frwdFar);
            }
            else //if (xivr.cfg.data.hmdPointing)
            {
                Matrix4x4 rayPos = hmdMatrix * curViewMatrixWithoutHMDI;
                Vector3 frwdFar = new Vector3(rayPos.M31, rayPos.M32, rayPos.M33) * -1;
                ray->Origin = rayPos.Translation;
                ray->Direction = Vector3.Normalize(frwdFar);
            }
            //else
            //    ScreenPointToRayHook!.Original(gameCamera, ray, mousePosX, mousePosY);

            return ray;
        }


        //----
        // ScreenPonitToRay1
        //----
        private delegate void ScreenPointToRay1Dg(Ray* ray, float* mousePos);
        [Signature(Signatures.ScreenPointToRay1, DetourName = nameof(ScreenPointToRay1Fn))]
        private Hook<ScreenPointToRay1Dg> ScreenPointToRay1Hook = null;

        [HandleStatus("ScreenPonitToRay1")]
        public void ScreenPonitToRay1Status(bool status)
        {
            if (status == true)
                ScreenPointToRay1Hook?.Enable();
            else
                ScreenPointToRay1Hook?.Disable();
        }
        private void ScreenPointToRay1Fn(Ray* ray, float* mousePos)
        {
            if (xivr.cfg.data.motioncontrol)
            {
                Matrix4x4 rayPos = rhcMatrix * curViewMatrixWithoutHMDI;
                Vector3 frwdFar = new Vector3(rayPos.M31, rayPos.M32, rayPos.M33) * -1;
                ray->Origin = rayPos.Translation;
                ray->Direction = Vector3.Normalize(frwdFar);
            }
            else //if (xivr.cfg.data.hmdPointing)
            {
                Matrix4x4 rayPos = hmdMatrix * curViewMatrixWithoutHMDI;
                Vector3 frwdFar = new Vector3(rayPos.M31, rayPos.M32, rayPos.M33) * -1;
                ray->Origin = rayPos.Translation;
                ray->Direction = Vector3.Normalize(frwdFar);
            }
            //else
            //    ScreenPointToRay1Hook!.Original(ray, mousePos);
        }


        //----
        // MousePointScreenToClient
        //----
        private delegate void MousePointScreenToClientDg(UInt64 frameworkInstance, Point* mousePos);
        [Signature(Signatures.MousePointScreenToClient, DetourName = nameof(MousePointScreenToClientFn))]
        private Hook<MousePointScreenToClientDg> MousePointScreenToClientHook = null;

        [HandleStatus("MousePointScreenToClient")]
        public void MousePointScreenToClientStatus(bool status)
        {
            if (status == true)
                MousePointScreenToClientHook?.Enable();
            else
                MousePointScreenToClientHook?.Disable();
        }
        private void MousePointScreenToClientFn(UInt64 frameworkInstance, Point* mousePos)
        {
            *mousePos = virtualMouse;
            //MousePointScreenToClientHook!.Original(frameworkInstance, mousePos);
        }





        /*
        //----
        // DisableLeftClick
        //----
        private delegate void DisableLeftClickDg(void** a, byte* b, bool c);
        [Signature(Signatures.DisableLeftClick, DetourName = nameof(DisableLeftClickFn))]
        private readonly Hook<DisableLeftClickDg>? DisableLeftClickHook = null;

        [HandleStatus("DisableLeftClick")]
        public void DisableLeftClickStatus(bool status)
        {
            if (status == true)
                DisableLeftClickHook?.Enable();
            else
                DisableLeftClickHook?.Disable();
        }

        private void DisableLeftClickFn(void** a, byte* b, bool c)
        {
            if (b != null && b == a[16]) DisableLeftClickHook!.Original(a, b, c);
        }



        //----
        // DisableRightClick
        //----
        private delegate void DisableRightClickDg(void** a, byte* b, bool c);
        [Signature(Signatures.DisableRightClick, DetourName = nameof(DisableRightClickFn))]
        private Hook<DisableRightClickDg>? DisableRightClickHook = null;

        [HandleStatus("DisableRightClick")]
        public void DisableRightClickStatus(bool status)
        {
            if (status == true)
                DisableRightClickHook?.Enable();
            else
                DisableRightClickHook?.Disable();
        }

        private void DisableRightClickFn(void** a, byte* b, bool c)
        {
            if (b != null && b == a[16]) DisableRightClickHook!.Original(a, b, c);
        }
        */


        //----
        // AtkUnitBase OnRequestedUpdate
        //----
        private delegate void OnRequestedUpdateDg(UInt64 a, UInt64 b, UInt64 c);
        [Signature(Signatures.OnRequestedUpdate, DetourName = nameof(OnRequestedUpdateFn))]
        private Hook<OnRequestedUpdateDg>? OnRequestedUpdateHook { get; set; } = null;

        [HandleStatus("OnRequestedUpdate")]
        public void OnRequestedUpdateStatus(bool status)
        {
            if (status == true)
                OnRequestedUpdateHook?.Enable();
            else
                OnRequestedUpdateHook?.Disable();
        }

        void OnRequestedUpdateFn(UInt64 a, UInt64 b, UInt64 c)
        {
            float globalScale = *(float*)globalScaleAddress;
            *(float*)globalScaleAddress = 1;
            OnRequestedUpdateHook!.Original(a, b, c);
            *(float*)globalScaleAddress = globalScale;
        }




        //----
        // DXGIPresent
        //----
        private delegate void DXGIPresentDg(UInt64 a, UInt64 b);
        [Signature(Signatures.DXGIPresent, DetourName = nameof(DXGIPresentFn))]
        private Hook<DXGIPresentDg>? DXGIPresentHook = null;

        [HandleStatus("DXGIPresent")]
        public void DXGIPresentStatus(bool status)
        {
            if (status == true)
                DXGIPresentHook?.Enable();
            else
                DXGIPresentHook?.Disable();
        }

        private unsafe void DXGIPresentFn(UInt64 a, UInt64 b)
        {
            if (forceFloatingScreen)
            {
                Imports.RenderUI(false, false, curViewMatrixWithoutHMD, virtualMouse, dalamudMode);
                DXGIPresentHook!.Original(a, b);
                Imports.RenderFloatingScreen(virtualMouse, dalamudMode);
                Imports.RenderVR();
            }
            else
            {
                Imports.RenderUI(enableVR, enableFloatingHUD, curViewMatrixWithoutHMD, virtualMouse, dalamudMode);
                DXGIPresentHook!.Original(a, b);
                Imports.SetTexture();
                Imports.RenderVR();
            }
        }



        //----
        // RenderThreadSetRenderTarget
        //----
        private delegate void RenderThreadSetRenderTargetDg(UInt64 a, UInt64 b);
        [Signature(Signatures.RenderThreadSetRenderTarget, DetourName = nameof(RenderThreadSetRenderTargetFn))]
        private Hook<RenderThreadSetRenderTargetDg>? RenderThreadSetRenderTargetHook = null;

        [HandleStatus("RenderThreadSetRenderTarget")]
        public void RenderThreadSetRenderTargetStatus(bool status)
        {
            if (status == true)
                RenderThreadSetRenderTargetHook?.Enable();
            else
                RenderThreadSetRenderTargetHook?.Disable();
        }

        private void RenderThreadSetRenderTargetFn(UInt64 a, UInt64 b)
        {
            if ((b + 0x8) != 0)
            {
                Structures.Texture* rendTrg = *(Structures.Texture**)(b + 0x8);
                if ((rendTrg->uk5 & 0x90000000) == 0x90000000)
                    Imports.SetThreadedEye((int)(rendTrg->uk5 - 0x90000000));
            }
            RenderThreadSetRenderTargetHook!.Original(a, b);
        }



        //----
        // CameraManager Setup??
        //----
        private delegate void CamManagerSetMatrixDg(SceneCameraManager* camMngrInstance);
        [Signature(Signatures.CamManagerSetMatrix, DetourName = nameof(CamManagerSetMatrixFn))]
        private Hook<CamManagerSetMatrixDg>? CamManagerSetMatrixHook = null;

        [HandleStatus("CamManagerSetMatrix")]
        public void CamManagerSetMatrixStatus(bool status)
        {
            if (status == true)
                CamManagerSetMatrixHook?.Enable();
            else
                CamManagerSetMatrixHook?.Disable();
        }

        private void CamManagerSetMatrixFn(SceneCameraManager* camMngrInstance)
        {
            overrideFromParent.Push(true);
            CamManagerSetMatrixHook!.Original(camMngrInstance);
            overrideFromParent.Pop();
        }



        //----
        // CascadeShadow_UpdateConstantBuffer
        //----
        private delegate void CSUpdateConstBufDg(UInt64 a, UInt64 b);
        [Signature(Signatures.CSUpdateConstBuf, DetourName = nameof(CSUpdateConstBufFn))]
        private Hook<CSUpdateConstBufDg>? CSUpdateConstBufHook = null;

        [HandleStatus("CSUpdateConstBuf")]
        public void CSUpdateConstBufStatus(bool status)
        {
            if (status == true)
                CSUpdateConstBufHook?.Enable();
            else
                CSUpdateConstBufHook?.Disable();
        }

        private void CSUpdateConstBufFn(UInt64 a, UInt64 b)
        {
            overrideFromParent.Push(true);
            CSUpdateConstBufHook!.Original(a, b);
            overrideFromParent.Pop();
        }



        //----
        // SetUIProj
        //----
        private delegate void SetUIProjDg(UInt64 a, UInt64 b);
        [Signature(Signatures.SetUIProj, DetourName = nameof(SetUIProjFn))]
        private Hook<SetUIProjDg>? SetUIProjHook = null;

        [HandleStatus("SetUIProj")]
        public void SetUIProjStatus(bool status)
        {
            if (status == true)
                SetUIProjHook?.Enable();
            else
                SetUIProjHook?.Disable();
        }

        private void SetUIProjFn(UInt64 a, UInt64 b)
        {
            bool overrideFn = (overrideFromParent.Count == 0) ? false : overrideFromParent.Peek();
            if (overrideFn)
            {
                Structures.Texture* texture = Imports.GetUIRenderTexture(curEye);
                UInt64 threadedOffset = GetThreadedOffset();
                SetRenderTargetFn!(threadedOffset, 1, &texture, 0, 0, 0);
            }

            SetUIProjHook!.Original(a, b);
        }

        //----
        // Camera CalculateViewMatrix
        //----
        private delegate void CalculateViewMatrixDg(RawGameCamera* a);
        [Signature(Signatures.CalculateViewMatrix, DetourName = nameof(CalculateViewMatrixFn))]
        private Hook<CalculateViewMatrixDg>? CalculateViewMatrixHook = null;

        [HandleStatus("CalculateViewMatrix")]
        public void CalculateViewMatrixStatus(bool status)
        {
            if (status == true)
                CalculateViewMatrixHook?.Enable();
            else
                CalculateViewMatrixHook?.Disable();
        }

        //----
        // This function is also called for ui character stuff so only
        // act on it the first time its run per frame
        //----
        float CameraHitBoxOffset = 0.0f;
        float rawCameraHitBoxOffset = -10.0f;
        private void CalculateViewMatrixFn(RawGameCamera* rawGameCamera)
        {
            if (enableVR && frfCalculateViewMatrix == false)
            {
                Matrix4x4 horizonLockMatrix = Matrix4x4.Identity;
                frfCalculateViewMatrix = true;

                //----
                // Restore the camera to its prooper spot if disabled for collisions in first person
                //----
                if (csCameraManager->ActiveCameraIndex == 0 && gameMode.Current == CameraModes.FirstPerson)
                {
                    rawGameCamera->Position.Y += CameraHitBoxOffset;
                    rawGameCamera->LookAt.Y += CameraHitBoxOffset;
                }

                rawGameCamera->ViewMatrix = Matrix4x4.Identity;
                CalculateViewMatrixHook!.Original(rawGameCamera);

                if (csCameraManager->ActiveCameraIndex == 0 && gameMode.Current == CameraModes.FirstPerson)
                {
                    rawGameCamera->Position.Y -= CameraHitBoxOffset;
                    rawGameCamera->LookAt.Y -= CameraHitBoxOffset;
                }
                firstPersonCameraHeight = rawGameCamera->Position.Y;

                if (enableFloatingHUD && forceFloatingScreen == false)
                {
                    if (camInst->CameraIndex == 1)
                    {
                        curViewMatrixWithoutHMD = rawGameCamera->ViewMatrix;
                        Matrix4x4.Invert(curViewMatrixWithoutHMD, out curViewMatrixWithoutHMDI);
                        if (xivr.cfg.data.swapEyes)
                            rawGameCamera->ViewMatrix = curViewMatrixWithoutHMD; // hmdMatrixI * eyeOffsetMatrix[swapEyes[curEye]];
                        else
                            rawGameCamera->ViewMatrix = curViewMatrixWithoutHMD; // hmdMatrixI * eyeOffsetMatrix[curEye];
                    }
                    else
                    {
                        if (xivr.cfg.data.immersiveMovement || isMounted)
                        {
                            //rawGameCamera->ViewMatrix = eyeMidPointM;
                            //Vector3 headCamDiff = neckPosition - rawGameCamera->ViewMatrix.Translation;
                            //Vector3 frontBackDiff = rawGameCamera->LookAt - rawGameCamera->Position;
                            //rawGameCamera->ViewMatrix.Translation -= neckPosition;
                            //rawGameCamera->LookAt = frontBackDiff + neckPosition;
                            //rawGameCamera->Position = headBoneMatrix.Translation;
                            //rawGameCamera->LookAt = frontBackDiff + headBoneMatrix.Translation;
                        }

                        if (xivr.cfg.data.horizonLock || gameMode.Current == CameraModes.FirstPerson)
                        {
                            horizonLockMatrix = Matrix4x4.CreateFromAxisAngle(new Vector3(1, 0, 0), rawGameCamera->CurrentVRotation);
                            rawGameCamera->LookAt.Y = rawGameCamera->Position.Y;
                        }
                        if (gameMode.Current == CameraModes.FirstPerson)
                        {
                            horizonLockMatrix.M42 = (xivr.cfg.data.offsetAmountYFPS / 100);
                            horizonLockMatrix.M43 = (xivr.cfg.data.offsetAmountZFPS / 100);
                        }
                        else
                        {
                            horizonLockMatrix.M41 = (-xivr.cfg.data.offsetAmountX / 100);
                            horizonLockMatrix.M42 = (xivr.cfg.data.offsetAmountY / 100);
                            horizonLockMatrix.M43 = (xivr.cfg.data.offsetAmountZ / 100);
                        }

                        Matrix4x4 invGameViewMatrixAddr;
                        Vector3 angles = new Vector3();
                        if (xivr.cfg.data.conloc)
                        {
                            angles = GetAngles(lhcMatrix);
                        }
                        else if (xivr.cfg.data.hmdloc)
                        {
                            angles = GetAngles(hmdMatrixI);
                            angles.Y *= -1;
                        }

                        Matrix4x4 revOnward = Matrix4x4.CreateFromAxisAngle(new Vector3(0, 1, 0), -angles.Y);
                        Matrix4x4 zoom = Matrix4x4.CreateTranslation(0, 0, -cameraZoom);
                        //revOnward = revOnward * zoom;
                        //Matrix4x4.Invert(revOnward, out revOnward);

                        if ((xivr.cfg.data.conloc == false && xivr.cfg.data.hmdloc == false) || gameMode.Current == CameraModes.ThirdPerson)
                            revOnward = Matrix4x4.Identity;

                        curViewMatrixWithoutHMD = rawGameCamera->ViewMatrix * horizonLockMatrix * revOnward;
                        Matrix4x4.Invert(curViewMatrixWithoutHMD, out curViewMatrixWithoutHMDI);

                        //curViewMatrix = rawGameCamera->ViewMatrix * hmdMatrix;
                        //PluginLog.Log($"hmdMatrix: {rawGameCamera->X} {rawGameCamera->Y} {rawGameCamera->Z} | {rawGameCamera->ViewMatrix.M41}, {rawGameCamera->ViewMatrix.M42}, {rawGameCamera->ViewMatrix.M43} | {hmdMatrix.M41}, {hmdMatrix.M42},  {hmdMatrix.M43}");

                        if (xivr.cfg.data.swapEyes)
                            rawGameCamera->ViewMatrix = curViewMatrixWithoutHMD * hmdMatrixI * eyeOffsetMatrix[swapEyes[curEye]];
                        else
                            rawGameCamera->ViewMatrix = curViewMatrixWithoutHMD * hmdMatrixI * eyeOffsetMatrix[curEye];

                        //if (!inCutscene && gameMode == CameraModes.FirstPerson)
                        //    rawGameCamera->ViewMatrix = hmdMatrixI * eyeOffsetMatrix[curEye];
                    }
                }
            }
            else
            {
                rawGameCamera->ViewMatrix = Matrix4x4.Identity;
                CalculateViewMatrixHook!.Original(rawGameCamera);
            }
        }


        //----
        // GetCameraPosition
        //----
        private delegate void GetCameraPositionDg(GameCamera* gameCamera, IntPtr target, Vector3* vectorPosition, bool swapPerson);
        private Hook<GetCameraPositionDg>? GetCameraPositionHook = null;

        [HandleStatus("GetCameraPosition")]
        public void GetCameraPositionStatus(bool status)
        {
            if (status == true)
            {
                if (GetCameraPositionHook == null)
                    GetCameraPositionHook = Hook<GetCameraPositionDg>.FromAddress((IntPtr)csCameraManager->GameCamera->CameraBase.vtbl[15], GetCameraPositionFn);
                GetCameraPositionHook?.Enable();
            }
            else
                GetCameraPositionHook?.Disable();
        }

        private void GetCameraPositionFn(GameCamera* gameCamera, IntPtr target, Vector3* vectorPosition, bool swapPerson)
        {
            GetCameraPositionHook!.Original(gameCamera, target, vectorPosition, swapPerson);

            //----
            // Move the camera position to disable collisions in first person
            //----
            if (csCameraManager->ActiveCameraIndex == 0 && gameMode.Current == CameraModes.FirstPerson)
            {
                if (isMounted)
                    CameraHitBoxOffset = rawCameraHitBoxOffset;
                else
                    CameraHitBoxOffset = neckPosition.Y + 0.5f;
                vectorPosition->Y -= CameraHitBoxOffset;
            }
        }



        //----
        // Camera UpdateRotation
        //----
        private delegate void UpdateRotationDg(GameCamera* gameCamera);
        [Signature(Signatures.UpdateRotation, DetourName = nameof(UpdateRotationFn))]
        private Hook<UpdateRotationDg>? UpdateRotationHook = null;

        [HandleStatus("UpdateRotation")]
        public void UpdateRotationStatus(bool status)
        {
            if (status == true)
                UpdateRotationHook?.Enable();
            else
                UpdateRotationHook?.Disable();
        }

        private void UpdateRotationFn(GameCamera* gameCamera)
        {
            if (forceFloatingScreen == false)
            {
                gameMode.Current = gameCamera->Camera.Mode;
                Vector3 angles = new Vector3();

                if (xivr.cfg.data.conloc)
                {
                    angles = GetAngles(lhcMatrix);
                    angles.Y *= -1;
                }
                else if (xivr.cfg.data.hmdloc)
                {
                    angles = GetAngles(hmdMatrixI);
                    angles.X *= -1;
                }

                onwardDiff = angles - onwardAngle;
                onwardAngle = angles;

                if (xivr.cfg.data.horizontalLock)
                    gameCamera->Camera.HRotationThisFrame2 = 0;
                if (xivr.cfg.data.verticalLock)
                    gameCamera->Camera.VRotationThisFrame2 = 0;
                if ((xivr.cfg.data.conloc == false && xivr.cfg.data.hmdloc == false) || gameMode.Current == CameraModes.ThirdPerson)
                {
                    onwardDiff.Y = 0;
                    onwardDiff.X = 0;
                    onwardDiff.Z = 0;
                }

                //gameCamera->Camera.HRotationThisFrame1 += onwardDiff.Y + rotateAmount.X;
                gameCamera->Camera.HRotationThisFrame2 += onwardDiff.Y + rotateAmount.X;
                //gameCamera->Camera.VRotationThisFrame1 += onwardDiff.X + rotateAmount.Y;
                //gameCamera->Camera.VRotationThisFrame2 += onwardDiff.X + rotateAmount.Y;

                if (gameMode.Current == CameraModes.FirstPerson)
                {
                    gameCamera->Camera.VRotationThisFrame1 = 0.0f;
                    gameCamera->Camera.VRotationThisFrame2 = 0.0f;
                }

                if (xivr.cfg.data.vertloc)
                    gameCamera->Camera.VRotationThisFrame2 += onwardDiff.X + rotateAmount.Y;
                else
                    gameCamera->Camera.VRotationThisFrame2 += rotateAmount.Y;

                rotateAmount.X = 0;
                rotateAmount.Y = 0;

                cameraZoom = gameCamera->Camera.CurrentZoom;
                UpdateRotationHook!.Original(gameCamera);
            }
            else
            {
                UpdateRotationHook!.Original(gameCamera);
            }
        }


        //----
        // CutScene View Matrix
        //----
        private delegate void CutsceneViewMatrixDg(UInt64 a, UInt64 b);
        [Signature(Signatures.CutsceneViewMatrix, DetourName = nameof(CutsceneViewMatrixFn))]
        private Hook<CutsceneViewMatrixDg>? CutsceneViewMatrixHook = null;

        [HandleStatus("CutsceneViewMatrix")]
        public void CutsceneViewMatrixStatus(bool status)
        {
            if (status == true)
                CutsceneViewMatrixHook?.Enable();
            else
                CutsceneViewMatrixHook?.Disable();
        }

        private void CutsceneViewMatrixFn(UInt64 a, UInt64 b)
        {
            //PluginLog.Log($"{a:X} {b:X}");
            CutsceneViewMatrixHook!.Original(a, b);
        }


        //----
        // MakeProjectionMatrix2
        //----
        private delegate Matrix4x4 MakeProjectionMatrix2Dg(Matrix4x4 projMatrix, float b, float c, float d, float e);
        [Signature(Signatures.MakeProjectionMatrix2, DetourName = nameof(MakeProjectionMatrix2Fn))]
        private Hook<MakeProjectionMatrix2Dg>? MakeProjectionMatrix2Hook = null;

        [HandleStatus("MakeProjectionMatrix2")]
        public void MakeProjectionMatrix2Status(bool status)
        {
            if (status == true)
                MakeProjectionMatrix2Hook?.Enable();
            else
                MakeProjectionMatrix2Hook?.Disable();
        }

        private Matrix4x4 MakeProjectionMatrix2Fn(Matrix4x4 projMatrix, float b, float c, float d, float e)
        {
            bool overrideMatrix = (overrideFromParent.Count == 0) ? false : overrideFromParent.Peek();
            Matrix4x4 retVal = MakeProjectionMatrix2Hook!.Original(projMatrix, b, c, d, e);
            if (enableVR && enableFloatingHUD && overrideMatrix && forceFloatingScreen == false)
            {
                if (xivr.cfg.data.swapEyes)
                {
                    gameProjectionMatrix[swapEyes[curEye]].M43 = retVal.M43;
                    retVal = gameProjectionMatrix[swapEyes[curEye]];
                }
                else
                {
                    gameProjectionMatrix[curEye].M43 = retVal.M43;
                    retVal = gameProjectionMatrix[curEye];
                }
            }
            return retVal;
        }



        //----
        // CascadeShadow MakeProjectionMatrix
        //----
        private delegate Matrix4x4 CSMakeProjectionMatrixDg(Matrix4x4 projMatrix, float b, float c, float d, float e);
        [Signature(Signatures.CSMakeProjectionMatrix, DetourName = nameof(CSMakeProjectionMatrixFn))]
        private Hook<CSMakeProjectionMatrixDg>? CSMakeProjectionMatrixHook = null;

        [HandleStatus("CSMakeProjectionMatrix")]
        public void CSMakeProjectionMatrixStatus(bool status)
        {
            if (status == true)
                CSMakeProjectionMatrixHook?.Enable();
            else
                CSMakeProjectionMatrixHook?.Disable();
        }

        private Matrix4x4 CSMakeProjectionMatrixFn(Matrix4x4 projMatrix, float b, float c, float d, float e)
        {
            bool overrideMatrix = (overrideFromParent.Count == 0) ? false : overrideFromParent.Peek();
            if (enableVR && enableFloatingHUD && overrideMatrix && forceFloatingScreen == false)
            {
                if (xivr.cfg.data.ultrawideshadows == true)
                    b = 2.54f; // ultra wide
                else
                    b = 1.65f;
            }
            Matrix4x4 retVal = CSMakeProjectionMatrixHook!.Original(projMatrix, b, c, d, e);
            return retVal;
        }





        //----
        // NamePlateDraw
        //----
        private delegate void NamePlateDrawDg(AddonNamePlate* a);
        [Signature(Signatures.NamePlateDraw, DetourName = nameof(NamePlateDrawFn))]
        private Hook<NamePlateDrawDg>? NamePlateDrawHook = null;

        [HandleStatus("NamePlateDraw")]
        public void NamePlateDrawStatus(bool status)
        {
            if (status == true)
                NamePlateDrawHook?.Enable();
            else
                NamePlateDrawHook?.Disable();
        }

        private void NamePlateDrawFn(AddonNamePlate* a)
        {
            if (enableVR)
            {
                //----
                // Disables the target arrow until it can be put in the world
                //----
                AtkUnitBase* targetAddon = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("_TargetCursor", 1);
                if (targetAddon != null)
                {
                    targetAddon->Alpha = 1;
                    targetAddon->Hide(true);
                    //targetAddon->RootNode->SetUseDepthBasedPriority(true);
                }

                fixed (AtkTextNode** pvrTargetCursor = &vrTargetCursor)
                    VRCursor.SetupVRTargetCursor(pvrTargetCursor);

                for (byte i = 0; i < NamePlateCount; i++)
                {
                    NamePlateObject* npObj = &a->NamePlateObjectArray[i];
                    AtkComponentBase* npComponent = npObj->RootNode->Component;

                    for (int j = 0; j < npComponent->UldManager.NodeListCount; j++)
                    {
                        AtkResNode* child = npComponent->UldManager.NodeList[j];
                        child->SetUseDepthBasedPriority(true);
                    }

                    npObj->RootNode->Component->UldManager.UpdateDrawNodeList();
                }

                NamePlateObject* selectedNamePlate = null;
                var framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
                var ui3DModule = framework->GetUiModule()->GetUI3DModule();

                for (int i = 0; i < ui3DModule->NamePlateObjectInfoCount; i++)
                {
                    var objectInfo = ((UI3DModule.ObjectInfo**)ui3DModule->NamePlateObjectInfoPointerArray)[i];

                    TargetSystem* targSys = (TargetSystem*)DalamudApi.TargetManager.Address;
                    if (objectInfo->GameObject == targSys->Target)
                    {
                        selectedNamePlate = &a->NamePlateObjectArray[objectInfo->NamePlateIndex];
                        break;
                    }
                }

                fixed (AtkTextNode** pvrTargetCursor = &vrTargetCursor)
                {
                    VRCursor.UpdateVRCursorSize(pvrTargetCursor);
                    VRCursor.SetVRCursor(pvrTargetCursor, selectedNamePlate);
                }
            }

            NamePlateDrawHook!.Original(a);
        }



        //----
        // RunBoneMath
        //----
        private delegate UInt64 RunBoneMathDg(hkaPose* a, int b);
        [Signature(Signatures.RunBoneMath, DetourName = nameof(RunBoneMathFn))]
        private Hook<RunBoneMathDg>? RunBoneMathHook = null;

        [HandleStatus("RunBoneMath")]
        public void RunBoneMathStatus(bool status)
        {
            if (status == true)
                RunBoneMathHook?.Enable();
            else
                RunBoneMathHook?.Disable();
        }

        private UInt64 RunBoneMathFn(hkaPose* pose, int b)
        {
            UInt64 retVal = 0;
            if (gameMode.Current == CameraModes.FirstPerson && rawBoneList.ContainsKey((UInt64)pose))
            {
                Dictionary<UInt64, Bone> parentList = new Dictionary<UInt64, Bone>();
                foreach (Bone item in rawBoneList[(UInt64)pose])
                {
                    if (item.disableParent)
                    {
                        parentList.Add((UInt64)pose, item);
                        pose->Skeleton->ParentIndices[item.id] = -1;
                    }

                    hkQsTransformf transform = pose->LocalPose[item.id];
                    if (item.useReference)
                        transform = item.reference;
                    if (item.updatePosition)
                        transform.Translation = item.transform.Translation;
                    if (item.updateRotation)
                        transform.Rotation = item.transform.Rotation;
                    if (item.updateScale)
                        transform.Scale = item.transform.Scale;
                    pose->LocalPose[item.id] = transform;
                }

                retVal = RunBoneMathHook!.Original(pose, b);
            }
            else
            {
                retVal = RunBoneMathHook!.Original(pose, b);
            }

            return retVal;
        }



        //----
        // PhysicsBoneUpdate
        //----
        private delegate UInt64 PhysicsBoneUpdateDg(UInt64 a, UInt64 b, short c);
        [Signature(Signatures.PhysicsBoneUpdate, DetourName = nameof(PhysicsBoneUpdateFn))]
        private Hook<PhysicsBoneUpdateDg>? PhysicsBoneUpdateHook = null;

        [HandleStatus("PhysicsBoneUpdate")]
        public void PhysicsBoneUpdateStatus(bool status)
        {
            if (status == true)
                PhysicsBoneUpdateHook?.Enable();
            else
                PhysicsBoneUpdateHook?.Disable();
        }

        private UInt64 PhysicsBoneUpdateFn(UInt64 a, UInt64 b, short c)
        {
            /*PluginLog.Log($"{a:0} {b:X} {c}");
            PlayerCharacter? player = DalamudApi.ClientState.LocalPlayer;
            if (player != null)
            {
                GameObject* bonedObject = (GameObject*)player.Address;
                if (bonedObject != null)
                {
                    Model* model = (Model*)bonedObject->DrawObject;
                    if (model != null)
                    {
                        UInt64 skel = (UInt64)model->skeleton;
                        if(a == skel)
                        {
                            PluginLog.Log("Is Player");
                        }
                    }
                }
            }*/

            return PhysicsBoneUpdateHook!.Original(a, b, c);
        }





        //----
        // CalculateHeadAnimation
        //----
        private delegate void CalculateHeadAnimationDg(UInt64 a, UInt64* b);
        [Signature(Signatures.CalculateHeadAnimation, DetourName = nameof(CalculateHeadAnimationFn))]
        private Hook<CalculateHeadAnimationDg>? CalculateHeadAnimationHook = null;

        [HandleStatus("CalculateHeadAnimation")]
        public void CalculateHeadAnimationStatus(bool status)
        {
            if (status == true)
                CalculateHeadAnimationHook?.Enable();
            else
                CalculateHeadAnimationHook?.Disable();
        }

        private void CalculateHeadAnimationFn(UInt64 a, UInt64* b)
        {
            CalculateHeadAnimationHook!.Original(a, b);
        }






        //----
        // LoadCharacter
        //----
        private delegate UInt64 LoadCharacterDg(UInt64 a, UInt64 b, UInt64 c, UInt64 d, UInt64 e, UInt64 f);
        [Signature(Signatures.LoadCharacter, DetourName = nameof(LoadCharacterFn))]
        private Hook<LoadCharacterDg>? LoadCharacterHook = null;

        [HandleStatus("LoadCharacter")]
        public void LoadCharacterStatus(bool status)
        {
            if (status == true)
                LoadCharacterHook?.Enable();
            else
                LoadCharacterHook?.Disable();
        }

        private UInt64 LoadCharacterFn(UInt64 a, UInt64 b, UInt64 c, UInt64 d, UInt64 e, UInt64 f)
        {
            PlayerCharacter? player = DalamudApi.ClientState.LocalPlayer;
            if (player != null && (UInt64)player.Address == a)
            {
                CharCustData* cData = (CharCustData*)c;
                CharEquipData* eData = (CharEquipData*)d;
            }
            //PluginLog.Log($"LoadCharacterFn {a:X} {b:X} {c:X} {d:X} {e:X} {f:X}");
            return LoadCharacterHook!.Original(a, b, c, d, e, f);
        }


        //----
        // ChangeEquipment
        //----
        private delegate void ChangeEquipmentDg(UInt64 address, CharEquipSlots index, CharEquipSlotData item);
        [Signature(Signatures.ChangeEquipment, DetourName = nameof(ChangeEquipmentFn))]
        private Hook<ChangeEquipmentDg>? ChangeEquipmentHook = null;

        [HandleStatus("ChangeEquipment")]
        public void ChangeEquipmentStatus(bool status)
        {
            if (status == true)
                ChangeEquipmentHook?.Enable();
            else
                ChangeEquipmentHook?.Disable();
        }

        private void ChangeEquipmentFn(UInt64 address, CharEquipSlots index, CharEquipSlotData item)
        {
            PlayerCharacter? player = DalamudApi.ClientState.LocalPlayer;
            if (player != null)
            {
                Character* bonedCharacter = (Character*)player.Address;
                if (bonedCharacter != null)
                {
                    UInt64 equipOffset = (UInt64)(UInt64*)&bonedCharacter->DrawData;
                    if (equipOffset == address)
                    {
                        haveSavedEquipmentSet = true;
                        currentEquipmentSet.Data[(int)index] = item.Data;
                    }
                }
            }
            //PluginLog.Log($"ChangeEquipmentFn {address:X} {index} {item.Id}, {item.Variant}, {item.Dye}");
            ChangeEquipmentHook!.Original(address, index, item);
        }

        //----
        // ChangeWeapon
        //----
        private delegate void ChangeWeaponDg(UInt64 address, CharWeaponSlots index, CharWeaponSlotData item, byte d, byte e, byte f, byte g);
        [Signature(Signatures.ChangeWeapon, DetourName = nameof(ChangeWeaponFn))]
        private Hook<ChangeWeaponDg>? ChangeWeaponHook = null;

        [HandleStatus("ChangeWeapon")]
        public void ChangeWeaponStatus(bool status)
        {
            if (status == true)
                ChangeWeaponHook?.Enable();
            else
                ChangeWeaponHook?.Disable();
        }

        private void ChangeWeaponFn(UInt64 address, CharWeaponSlots index, CharWeaponSlotData item, byte d, byte e, byte f, byte g)
        {
            PlayerCharacter? player = DalamudApi.ClientState.LocalPlayer;
            if (player != null)
            {
                Character* bonedCharacter = (Character*)player.Address;
                if (bonedCharacter != null)
                {
                    UInt64 equipOffset = (UInt64)(UInt64*)&bonedCharacter->DrawData;
                    if (equipOffset == address)
                    {
                        haveSavedEquipmentSet = true;
                        //currentWeaponSet.Data[(int)index] = item.Data;
                    }
                }
            }
            //PluginLog.Log($"ChangeWeaponFn {address:X} {index} | {item.Type}, {item.Id}, {item.Variant}, {item.Dye} | {d}, {e}, {f}, {g}");
            ChangeWeaponHook!.Original(address, index, item, d, e, f, g);
        }


        //----
        // EquipGearsetInternal
        //----
        private delegate void EquipGearsetInternalDg(UInt64 address, int b, byte c);
        [Signature(Signatures.EquipGearsetInternal, DetourName = nameof(EquipGearsetInternalFn))]
        private Hook<EquipGearsetInternalDg>? EquipGearsetInternalHook = null;

        [HandleStatus("EquipGearsetInternal")]
        public void EquipGearsetInternalStatus(bool status)
        {
            if (status == true)
                EquipGearsetInternalHook?.Enable();
            else
                EquipGearsetInternalHook?.Disable();
        }

        private void EquipGearsetInternalFn(UInt64 address, int b, byte c)
        {
            //PluginLog.Log($"EquipGearsetInternalFn {address:X} {b} {c}");
            EquipGearsetInternalHook!.Original(address, b, c);
        }



        //----
        // Input.GetAnalogueValue
        //----
        private delegate Int32 GetAnalogueValueDg(UInt64 a, UInt64 b);
        [Signature(Signatures.GetAnalogueValue, DetourName = nameof(GetAnalogueValueFn))]
        private Hook<GetAnalogueValueDg>? GetAnalogueValueHook = null;

        [HandleStatus("GetAnalogueValue")]
        public void GetAnalogueValueStatus(bool status)
        {
            if (status == true)
                GetAnalogueValueHook?.Enable();
            else
                GetAnalogueValueHook?.Disable();
        }



        // 0 mouse left right
        // 1 mouse up down
        // 3 left | left right
        // 4 left | up down
        // 5 right | left right
        // 6 right | up down

        private Int32 GetAnalogueValueFn(UInt64 a, UInt64 b)
        {
            Int32 retVal = GetAnalogueValueHook!.Original(a, b);

            if (enableVR)
            {
                switch (b)
                {
                    case 0:
                    case 1:
                    case 2:
                        break;
                    case 3:
                        break;
                    case 4:
                        break;
                    case 5:
                        //PluginLog.Log($"GetAnalogueValueFn: {retVal}");
                        if (MathF.Abs(retVal) >= 0 && MathF.Abs(retVal) < 15) rightHorizontalCenter = true;
                        if (xivr.cfg.data.horizontalLock && MathF.Abs(leftBumperValue) < 0.5)
                        {
                            if (MathF.Abs(retVal) > 75 && rightHorizontalCenter)
                            {
                                rightHorizontalCenter = false;
                                rotateAmount.X -= (xivr.cfg.data.snapRotateAmountX * Deg2Rad) * MathF.Sign(retVal);
                            }
                            retVal = 0;
                        }
                        break;
                    case 6:
                        //PluginLog.Log($"GetAnalogueValueFn: {retVal}");
                        if (MathF.Abs(retVal) >= 0 && MathF.Abs(retVal) < 15) rightVerticalCenter = true;
                        if (xivr.cfg.data.verticalLock && MathF.Abs(leftBumperValue) < 0.5)
                        {
                            if (MathF.Abs(retVal) > 75 && rightVerticalCenter && gameMode.Current == CameraModes.ThirdPerson)
                            {
                                rightVerticalCenter = false;
                                rotateAmount.Y -= (xivr.cfg.data.snapRotateAmountY * Deg2Rad) * MathF.Sign(retVal);
                            }
                            retVal = 0;
                        }
                        break;
                }
            }
            return retVal;
        }

        //----
        // Controller Input
        //---- BaseAddress + 0x4E37F0
        private delegate void ControllerInputDg(UInt64 a, UInt64 b, uint c);
        [Signature(Signatures.ControllerInput, DetourName = nameof(ControllerInputFn))]
        private Hook<ControllerInputDg>? ControllerInputHook = null;

        [HandleStatus("ControllerInput")]
        public void ControllerInputStatus(bool status)
        {
            if (status == true)
                ControllerInputHook?.Enable();
            else
                ControllerInputHook?.Disable();
        }

        float rightTriggerValue = 0;
        bool leftClickActive = false;
        bool rightClickActive = false;
        float leftStickOrig = 0;
        ChangedTypeBool rightBumperClick = new ChangedTypeBool();
        ChangedTypeBool rightTriggerClick = new ChangedTypeBool();
        Stopwatch leftStickTimer = new Stopwatch();
        ChangedTypeBool leftStickTimerHaptic = new ChangedTypeBool();
        float rightStickOrig = 0;
        Stopwatch rightStickTimer = new Stopwatch();
        ChangedTypeBool rightStickTimerHaptic = new ChangedTypeBool();

        public void ControllerInputFn(UInt64 a, UInt64 b, uint c)
        {
            bool runController = true;

            UInt64 controllerBase = *(UInt64*)(a + 0x70);
            UInt64 controllerIndex = *(byte*)(a + 0x434);

            UInt64 controllerAddress = controllerBase + 0x30 + ((controllerIndex * 0x1E6) * 4);
            XBoxButtonOffsets* offsets = (XBoxButtonOffsets*)((controllerIndex * 0x798) + controllerBase);

            if (xivr.cfg.data.motioncontrol)
            {
                if (xboxStatus.dpad_up.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->dpad_up * 4)) = xboxStatus.dpad_up.value;
                if (xboxStatus.dpad_down.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->dpad_down * 4)) = xboxStatus.dpad_down.value;
                if (xboxStatus.dpad_left.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->dpad_left * 4)) = xboxStatus.dpad_left.value;
                if (xboxStatus.dpad_right.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->dpad_right * 4)) = xboxStatus.dpad_right.value;
                if (xboxStatus.left_stick_down.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->left_stick_down * 4)) = xboxStatus.left_stick_down.value;
                if (xboxStatus.left_stick_up.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->left_stick_up * 4)) = xboxStatus.left_stick_up.value;
                if (xboxStatus.left_stick_left.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->left_stick_left * 4)) = xboxStatus.left_stick_left.value;
                if (xboxStatus.left_stick_right.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->left_stick_right * 4)) = xboxStatus.left_stick_right.value;
                if (xboxStatus.right_stick_down.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->right_stick_down * 4)) = xboxStatus.right_stick_down.value;
                if (xboxStatus.right_stick_up.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->right_stick_up * 4)) = xboxStatus.right_stick_up.value;
                if (xboxStatus.right_stick_left.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->right_stick_left * 4)) = xboxStatus.right_stick_left.value;
                if (xboxStatus.right_stick_right.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->right_stick_right * 4)) = xboxStatus.right_stick_right.value;
                if (xboxStatus.button_y.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->button_y * 4)) = xboxStatus.button_y.value;
                if (xboxStatus.button_b.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->button_b * 4)) = xboxStatus.button_b.value;
                if (xboxStatus.button_a.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->button_a * 4)) = xboxStatus.button_a.value;
                if (xboxStatus.button_x.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->button_x * 4)) = xboxStatus.button_x.value;
                if (xboxStatus.left_bumper.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->left_bumper * 4)) = xboxStatus.left_bumper.value;
                if (xboxStatus.left_trigger.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->left_trigger * 4)) = xboxStatus.left_trigger.value;
                if (xboxStatus.left_stick_click.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->left_stick_click * 4)) = xboxStatus.left_stick_click.value;
                if (xboxStatus.right_bumper.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->right_bumper * 4)) = xboxStatus.right_bumper.value;
                if (xboxStatus.right_trigger.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->right_trigger * 4)) = xboxStatus.right_trigger.value;
                if (xboxStatus.right_stick_click.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->right_stick_click * 4)) = xboxStatus.right_stick_click.value;
                if (xboxStatus.start.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->start * 4)) = xboxStatus.start.value;
                if (xboxStatus.select.active)
                    *(float*)(controllerAddress + (UInt64)(offsets->select * 4)) = xboxStatus.select.value;
            }

            if (xivr.cfg.data.motioncontrol)
            {
                leftBumperValue = *(float*)(controllerAddress + (UInt64)(offsets->left_bumper * 4));
                float curRightTriggerValue = *(float*)(controllerAddress + (UInt64)(offsets->right_trigger * 4));
                float curRightBumperValue = *(float*)(controllerAddress + (UInt64)(offsets->right_bumper * 4));

                rightTriggerClick.Current = (curRightTriggerValue > 0.75f);
                rightBumperClick.Current = (curRightBumperValue > 0.75f);

                InputAnalogActionData analog = new InputAnalogActionData();
                InputDigitalActionData digital = new InputDigitalActionData();

                //----
                // Right Click if trigger and bumper pressed
                //----
                if (leftClickActive == false && rightTriggerClick.Current == true && rightTriggerClick.Changed == true && rightBumperClick.Current == true)
                {
                    rightClickActive = true;
                    digital.bState = true;
                    inputRightClick(analog, digital);
                }
                else if (leftClickActive == false && rightTriggerClick.Current == false && rightTriggerClick.Changed == true && rightBumperClick.Current == true)
                {
                    rightClickActive = false;
                    digital.bState = false;
                    inputRightClick(analog, digital);
                }

                //----
                // Left Click if only trigger pressed
                //----
                if (rightClickActive == false && rightTriggerClick.Current == true && rightTriggerClick.Changed == true && rightBumperClick.Current == false)
                {
                    leftClickActive = true;
                    digital.bState = true;
                    inputLeftClick(analog, digital);
                }
                else if (rightClickActive == false && rightTriggerClick.Current == false && rightTriggerClick.Changed == true && rightBumperClick.Current == false)
                {
                    leftClickActive = false;
                    digital.bState = false;
                    inputLeftClick(analog, digital);
                }

                if (housingMode)
                    *(float*)(controllerAddress + (UInt64)(offsets->right_trigger * 4)) = 0;

                //----
                // Left Stick Pressed
                //----
                bool updateLeftAfterInput = false;
                if (xboxStatus.left_stick_click.active == true && xboxStatus.left_stick_click.ChangedStatus == true)
                {
                    leftStickOrig = *(float*)(controllerAddress + (UInt64)(offsets->left_stick_click * 4));
                    leftStickTimer = Stopwatch.StartNew();
                    leftStickTimerHaptic.Current = false;
                }
                //----
                // Left Stick Released
                //----
                else if (xboxStatus.left_stick_click.active == false && xboxStatus.left_stick_click.ChangedStatus == true)
                {
                    leftStickTimer.Stop();
                    if (leftStickTimer.ElapsedMilliseconds > 1000)
                        leftStickAltMode = ((leftStickAltMode) ? false : true);
                    else
                        *(float*)(controllerAddress + (UInt64)(offsets->left_stick_click * 4)) = leftStickOrig;

                    updateLeftAfterInput = true;
                    leftStickOrig = 0;
                }

                if (leftStickTimer.IsRunning)
                {
                    leftStickTimerHaptic.Current = (leftStickTimer.ElapsedMilliseconds >= 1000);
                    if (leftStickTimerHaptic.Changed == true)
                        Imports.HapticFeedback(ActionButtonLayout.haptics_left, 0.1f, 100.0f, 50.0f);
                    *(float*)(controllerAddress + (UInt64)(offsets->left_stick_click * 4)) = 0;
                }


                //----
                // Right Stick Pressed
                //----
                bool updateRightAfterInput = false;
                if (xboxStatus.right_stick_click.active == true && xboxStatus.right_stick_click.ChangedStatus == true)
                {
                    rightStickOrig = *(float*)(controllerAddress + (UInt64)(offsets->right_stick_click * 4));
                    rightStickTimer = Stopwatch.StartNew();
                    rightStickTimerHaptic.Current = false;
                }
                //----
                // Right Stick Released
                //----
                else if (xboxStatus.right_stick_click.active == false && xboxStatus.right_stick_click.ChangedStatus == true)
                {
                    rightStickTimer.Stop();
                    if (rightStickTimer.ElapsedMilliseconds > 1000)
                        rightStickAltMode = ((rightStickAltMode) ? false : true);
                    else
                    {
                        if (xboxStatus.right_bumper.active == true)
                        {
                            dalamudMode = !dalamudMode;
                            Imports.HapticFeedback(ActionButtonLayout.haptics_right, 0.1f, 50.0f, 100.0f);
                        }
                        else
                            *(float*)(controllerAddress + (UInt64)(offsets->right_stick_click * 4)) = rightStickOrig;
                    }

                    updateRightAfterInput = true;
                    rightStickOrig = 0;
                }

                if (rightStickTimer.IsRunning)
                {
                    rightStickTimerHaptic.Current = (rightStickTimer.ElapsedMilliseconds >= 1000);
                    if (rightStickTimerHaptic.Changed == true)
                        Imports.HapticFeedback(ActionButtonLayout.haptics_right, 0.1f, 100.0f, 50.0f);
                    *(float*)(controllerAddress + (UInt64)(offsets->right_stick_click * 4)) = 0;
                }

                ControllerInputHook!.Original(a, b, c);

                if (updateLeftAfterInput)
                    *(float*)(controllerAddress + (UInt64)(offsets->left_stick_click * 4)) = leftStickOrig;
                if (updateRightAfterInput)
                    *(float*)(controllerAddress + (UInt64)(offsets->right_stick_click * 4)) = rightStickOrig;
            }
            else
            {
                ControllerInputHook!.Original(a, b, c);
            }
        }



        public static Vector3 GetAngles(Matrix4x4 source)
        {
            float thetaX, thetaY, thetaZ = 0.0f;
            thetaX = MathF.Asin(source.M32);

            if (thetaX < (Math.PI / 2))
            {
                if (thetaX > (-Math.PI / 2))
                {
                    thetaZ = MathF.Atan2(-source.M12, source.M22);
                    thetaY = MathF.Atan2(-source.M31, source.M33);
                }
                else
                {
                    thetaZ = -MathF.Atan2(-source.M13, source.M11);
                    thetaY = 0;
                }
            }
            else
            {
                thetaZ = MathF.Atan2(source.M13, source.M11);
                thetaY = 0;
            }
            Vector3 angles = new Vector3(thetaX, thetaY, thetaZ);
            return angles;
        }






        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void keybd_event(uint bVk, uint bScan, uint dwFlags, uint dwExtraInfo);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, int cButtons, int dwExtraInfo);

        const int MOUSEEVENTF_LEFTDOWN = 0x02;
        const int MOUSEEVENTF_LEFTUP = 0x04;
        const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        const int MOUSEEVENTF_RIGHTUP = 0x10;
        const int MOUSEEVENTF_WHEEL = 0x0800;

        const int KEYEVENTF_KEYDOWN = 0x0000;
        const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        const int KEYEVENTF_KEYUP = 0x0002;

        const int VK_SHIFT = 0xA0;
        const int VK_ALT = 0xA4;
        const int VK_CONTROL = 0xA2;
        const int VK_ESCAPE = 0x1B;

        const int VK_F1 = 0x70;
        const int VK_F2 = 0x71;
        const int VK_F3 = 0x72;
        const int VK_F4 = 0x73;
        const int VK_F5 = 0x74;
        const int VK_F6 = 0x75;
        const int VK_F7 = 0x76;
        const int VK_F8 = 0x77;
        const int VK_F9 = 0x78;
        const int VK_F10 = 0x79;
        const int VK_F11 = 0x7A;
        const int VK_F12 = 0x7B;

        public XBoxStatus xboxStatus = new XBoxStatus();
        bool rightHorizontalCenter = false;
        bool rightVerticalCenter = false;
        bool leftStickAltMode = false;
        bool rightStickAltMode = false;

        //----
        // Movement
        //----
        [HandleInputAttribute(ActionButtonLayout.movement)]
        public void inputMovement(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            xboxStatus.left_stick_left.Set();
            xboxStatus.left_stick_right.Set();
            xboxStatus.left_stick_up.Set();
            xboxStatus.left_stick_down.Set();

            float deadzone = 0.5f;
            if (leftStickAltMode)
            {
                InputAnalogActionData analogRedirect = new InputAnalogActionData();
                InputDigitalActionData digitalRedirect = new InputDigitalActionData();

                if (analog.x > deadzone)
                {
                    digitalRedirect.bState = true;
                    inputXBoxPadRight(analogRedirect, digitalRedirect);
                }
                else if (analog.x < deadzone && analog.x >= 0)
                {
                    digitalRedirect.bState = false;
                    inputXBoxPadRight(analogRedirect, digitalRedirect);
                }

                if (analog.x < -deadzone)
                {
                    digitalRedirect.bState = true;
                    inputXBoxPadLeft(analogRedirect, digitalRedirect);
                }
                else if (analog.x > -deadzone && analog.x <= 0)
                {
                    digitalRedirect.bState = false;
                    inputXBoxPadLeft(analogRedirect, digitalRedirect);
                }

                if (analog.y > deadzone)
                {
                    digitalRedirect.bState = true;
                    inputXBoxPadUp(analogRedirect, digitalRedirect);
                }
                else if (analog.y < deadzone && analog.y >= 0)
                {
                    digitalRedirect.bState = false;
                    inputXBoxPadUp(analogRedirect, digitalRedirect);
                }

                if (analog.y < -deadzone)
                {
                    digitalRedirect.bState = true;
                    inputXBoxPadDown(analogRedirect, digitalRedirect);
                }
                else if (analog.y > -deadzone && analog.y <= 0)
                {
                    digitalRedirect.bState = false;
                    inputXBoxPadDown(analogRedirect, digitalRedirect);
                }
            }
            else
            {
                if (analog.x < 0)
                    xboxStatus.left_stick_left.Set(true, MathF.Abs(analog.x));
                else if (analog.x > 0)
                    xboxStatus.left_stick_right.Set(true, MathF.Abs(analog.x));

                if (analog.y > 0)
                    xboxStatus.left_stick_up.Set(true, MathF.Abs(analog.y));
                else if (analog.y < 0)
                    xboxStatus.left_stick_down.Set(true, MathF.Abs(analog.y));
            }
        }

        [HandleInputAttribute(ActionButtonLayout.rotation)]
        public void inputRotation(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            xboxStatus.right_stick_left.Set();
            xboxStatus.right_stick_right.Set();
            xboxStatus.right_stick_up.Set();
            xboxStatus.right_stick_down.Set();

            if (analog.x < 0)
                xboxStatus.right_stick_left.Set(true, MathF.Abs(analog.x));
            else if (analog.x > 0)
                xboxStatus.right_stick_right.Set(true, MathF.Abs(analog.x));

            if (rightStickAltMode)
            {
                if (analog.y > 0.75f)
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, 90, 0);
                else if (analog.y > 0.25f)
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, 30, 0);
                else if (analog.y < -0.75f)
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, -90, 0);
                else if (analog.y < -0.25f)
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, -30, 0);
            }
            else
            {
                if (analog.y > 0)
                    xboxStatus.right_stick_up.Set(true, MathF.Abs(analog.y));
                else if (analog.y < 0)
                    xboxStatus.right_stick_down.Set(true, MathF.Abs(analog.y));
            }
        }

        //----
        // Mouse
        //----

        [HandleInputAttribute(ActionButtonLayout.leftClick)]
        public void inputLeftClick(InputAnalogActionData analog, InputDigitalActionData digital)
        {

            if (digital.bState == true && inputState[ActionButtonLayout.leftClick] == false)
            {
                inputState[ActionButtonLayout.leftClick] = true;
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.leftClick] == true)
            {
                inputState[ActionButtonLayout.leftClick] = false;
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
            }
        }

        [HandleInputAttribute(ActionButtonLayout.rightClick)]
        public void inputRightClick(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.rightClick] == false)
            {
                inputState[ActionButtonLayout.rightClick] = true;
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.rightClick] == true)
            {
                inputState[ActionButtonLayout.rightClick] = false;
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
            }
        }


        //----
        // Keys
        //----

        [HandleInputAttribute(ActionButtonLayout.recenter)]
        public void inputRecenter(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.recenter] == false)
            {
                inputState[ActionButtonLayout.recenter] = true;
                Imports.Recenter();
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.recenter] == true)
            {
                inputState[ActionButtonLayout.recenter] = false;
            }
        }

        [HandleInputAttribute(ActionButtonLayout.shift)]
        public void inputShift(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.shift] == false)
            {
                inputState[ActionButtonLayout.shift] = true;
                keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYDOWN, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.shift] == true)
            {
                inputState[ActionButtonLayout.shift] = false;
                keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
            }
        }

        [HandleInputAttribute(ActionButtonLayout.alt)]
        public void inputAlt(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.alt] == false)
            {
                inputState[ActionButtonLayout.alt] = true;
                keybd_event(VK_ALT, 0, KEYEVENTF_KEYDOWN, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.alt] == true)
            {
                inputState[ActionButtonLayout.alt] = false;
                keybd_event(VK_ALT, 0, KEYEVENTF_KEYUP, 0);
            }
        }

        [HandleInputAttribute(ActionButtonLayout.control)]
        public void inputControl(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.control] == false)
            {
                inputState[ActionButtonLayout.control] = true;
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYDOWN, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.control] == true)
            {
                inputState[ActionButtonLayout.control] = false;
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
            }
        }

        [HandleInputAttribute(ActionButtonLayout.escape)]
        public void inputEscape(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.escape] == false)
            {
                inputState[ActionButtonLayout.escape] = true;
                keybd_event(VK_ESCAPE, 0, KEYEVENTF_KEYDOWN, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.escape] == true)
            {
                inputState[ActionButtonLayout.escape] = false;
                keybd_event(VK_ESCAPE, 0, KEYEVENTF_KEYUP, 0);
            }
        }

        //----
        // F Keys
        //----

        [HandleInputAttribute(ActionButtonLayout.button01)]
        public void inputButton01(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.button01] == false)
            {
                inputState[ActionButtonLayout.button01] = true;
                keybd_event(VK_F1, 0, KEYEVENTF_KEYDOWN, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.button01] == true)
            {
                inputState[ActionButtonLayout.button01] = false;
                keybd_event(VK_F1, 0, KEYEVENTF_KEYUP, 0);
            }
        }

        [HandleInputAttribute(ActionButtonLayout.button02)]
        public void inputButton02(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.button02] == false)
            {
                inputState[ActionButtonLayout.button02] = true;
                keybd_event(VK_F2, 0, KEYEVENTF_KEYDOWN, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.button02] == true)
            {
                inputState[ActionButtonLayout.button02] = false;
                keybd_event(VK_F2, 0, KEYEVENTF_KEYUP, 0);
            }
        }

        [HandleInputAttribute(ActionButtonLayout.button03)]
        public void inputButton03(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.button03] == false)
            {
                inputState[ActionButtonLayout.button03] = true;
                keybd_event(VK_F3, 0, KEYEVENTF_KEYDOWN, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.button03] == true)
            {
                inputState[ActionButtonLayout.button03] = false;
                keybd_event(VK_F3, 0, KEYEVENTF_KEYUP, 0);
            }
        }

        [HandleInputAttribute(ActionButtonLayout.button04)]
        public void inputButton04(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.button04] == false)
            {
                inputState[ActionButtonLayout.button04] = true;
                keybd_event(VK_F4, 0, KEYEVENTF_KEYDOWN, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.button04] == true)
            {
                inputState[ActionButtonLayout.button04] = false;
                keybd_event(VK_F4, 0, KEYEVENTF_KEYUP, 0);
            }
        }

        [HandleInputAttribute(ActionButtonLayout.button05)]
        public void inputButton05(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.button05] == false)
            {
                inputState[ActionButtonLayout.button05] = true;
                keybd_event(VK_F5, 0, KEYEVENTF_KEYDOWN, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.button05] == true)
            {
                inputState[ActionButtonLayout.button05] = false;
                keybd_event(VK_F5, 0, KEYEVENTF_KEYUP, 0);
            }
        }

        [HandleInputAttribute(ActionButtonLayout.button06)]
        public void inputButton06(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.button06] == false)
            {
                inputState[ActionButtonLayout.button06] = true;
                keybd_event(VK_F6, 0, KEYEVENTF_KEYDOWN, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.button06] == true)
            {
                inputState[ActionButtonLayout.button06] = false;
                keybd_event(VK_F6, 0, KEYEVENTF_KEYUP, 0);
            }
        }

        [HandleInputAttribute(ActionButtonLayout.button07)]
        public void inputButton07(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.button07] == false)
            {
                inputState[ActionButtonLayout.button07] = true;
                keybd_event(VK_F7, 0, KEYEVENTF_KEYDOWN, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.button07] == true)
            {
                inputState[ActionButtonLayout.button07] = false;
                keybd_event(VK_F7, 0, KEYEVENTF_KEYUP, 0);
            }
        }

        [HandleInputAttribute(ActionButtonLayout.button08)]
        public void inputButton08(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.button08] == false)
            {
                inputState[ActionButtonLayout.button08] = true;
                keybd_event(VK_F8, 0, KEYEVENTF_KEYDOWN, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.button08] == true)
            {
                inputState[ActionButtonLayout.button08] = false;
                keybd_event(VK_F8, 0, KEYEVENTF_KEYUP, 0);
            }
        }

        [HandleInputAttribute(ActionButtonLayout.button09)]
        public void inputButton09(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.button09] == false)
            {
                inputState[ActionButtonLayout.button09] = true;
                keybd_event(VK_F9, 0, KEYEVENTF_KEYDOWN, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.button09] == true)
            {
                inputState[ActionButtonLayout.button09] = false;
                keybd_event(VK_F9, 0, KEYEVENTF_KEYUP, 0);
            }
        }

        [HandleInputAttribute(ActionButtonLayout.button10)]
        public void inputButton10(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.button10] == false)
            {
                inputState[ActionButtonLayout.button10] = true;
                keybd_event(VK_F10, 0, KEYEVENTF_KEYDOWN, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.button10] == true)
            {
                inputState[ActionButtonLayout.button10] = false;
                keybd_event(VK_F10, 0, KEYEVENTF_KEYUP, 0);
            }
        }

        [HandleInputAttribute(ActionButtonLayout.button11)]
        public void inputButton11(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.button11] == false)
            {
                inputState[ActionButtonLayout.button11] = true;
                keybd_event(VK_F11, 0, KEYEVENTF_KEYDOWN, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.button11] == true)
            {
                inputState[ActionButtonLayout.button11] = false;
                keybd_event(VK_F11, 0, KEYEVENTF_KEYUP, 0);
            }
        }

        [HandleInputAttribute(ActionButtonLayout.button12)]
        public void inputButton12(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.button12] == false)
            {
                inputState[ActionButtonLayout.button12] = true;
                keybd_event(VK_F12, 0, KEYEVENTF_KEYDOWN, 0);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.button12] == true)
            {
                inputState[ActionButtonLayout.button12] = false;
                keybd_event(VK_F12, 0, KEYEVENTF_KEYUP, 0);
            }
        }


        //----
        // XBox Buttons
        //----

        [HandleInputAttribute(ActionButtonLayout.xbox_button_y)]
        public void inputXBoxButtonY(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            float value = (digital.bState == true) ? 1.0f : 0.0f;
            if (dalamudMode)
                inputButton11(analog, digital);
            else
                xboxStatus.button_y.Set(digital.bState, value);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_button_x)]
        public void inputXBoxButtonX(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            float value = (digital.bState == true) ? 1.0f : 0.0f;
            if (dalamudMode)
                inputButton10(analog, digital);
            else
                xboxStatus.button_x.Set(digital.bState, value);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_button_a)]
        public void inputXBoxButtonA(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            float value = (digital.bState == true) ? 1.0f : 0.0f;
            if (dalamudMode)
                inputEscape(analog, digital);
            else
                xboxStatus.button_a.Set(digital.bState, value);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_button_b)]
        public void inputXBoxButtonB(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            float value = (digital.bState == true) ? 1.0f : 0.0f;
            if (dalamudMode)
                inputButton12(analog, digital);
            else
                xboxStatus.button_b.Set(digital.bState, value);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_left_trigger)]
        public void inputXBoxLeftTrigger(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            bool status = (analog.x > 0) ? true : false;
            xboxStatus.left_trigger.Set(status, analog.x);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_left_bumper)]
        public void inputXBoxLeftBumper(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            bool status = (analog.x > 0) ? true : false;
            xboxStatus.left_bumper.Set(status, analog.x);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_left_stick_click)]
        public void inputXBoxLeftStickClick(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            float value = (digital.bState == true) ? 1.0f : 0.0f;
            xboxStatus.left_stick_click.Set(digital.bState, value);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_right_trigger)]
        public void inputXBoxRightTrigger(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            bool status = (analog.x > 0) ? true : false;
            xboxStatus.right_trigger.Set(status, analog.x);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_right_bumper)]
        public void inputXBoxRightBumper(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            bool status = (analog.x > 0) ? true : false;
            xboxStatus.right_bumper.Set(status, analog.x);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_right_stick_click)]
        public void inputXBoxRightStickClick(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            float value = (digital.bState == true) ? 1.0f : 0.0f;
            xboxStatus.right_stick_click.Set(digital.bState, value);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_pad_up)]
        public void inputXBoxPadUp(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            float value = (digital.bState == true) ? 1.0f : 0.0f;
            xboxStatus.dpad_up.Set(digital.bState, value);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_pad_down)]
        public void inputXBoxPadDown(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            float value = (digital.bState == true) ? 1.0f : 0.0f;
            xboxStatus.dpad_down.Set(digital.bState, value);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_pad_left)]
        public void inputXBoxPadLeft(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            float value = (digital.bState == true) ? 1.0f : 0.0f;
            xboxStatus.dpad_left.Set(digital.bState, value);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_pad_right)]
        public void inputXBoxPadRight(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            float value = (digital.bState == true) ? 1.0f : 0.0f;
            xboxStatus.dpad_right.Set(digital.bState, value);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_start)]
        public void inputXBoxStart(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            float value = (digital.bState == true) ? 1.0f : 0.0f;
            xboxStatus.start.Set(digital.bState, value);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_select)]
        public void inputXBoxSelect(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            float value = (digital.bState == true) ? 1.0f : 0.0f;
            xboxStatus.select.Set(digital.bState, value);
        }
    }
}

