using System;
using System.IO;
using System.Drawing;
using System.Numerics;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using Dalamud;
using Dalamud.Utility.Signatures;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using static FFXIVClientStructs.FFXIV.Client.UI.AddonNamePlate;
using FFXIVClientStructs.Havok;
using FFXIVClientStructs.Interop.Attributes;

using xivr.Structures;


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

        byte[] GetThreadedDataASM =
            {
                0x55, // push rbp
                0x65, 0x48, 0x8B, 0x04, 0x25, 0x58, 0x00, 0x00, 0x00, // mov rax,gs:[00000058]
                0x5D, // pop rbp
                0xC3  // ret
            };



        public bool enableVR = true;
        private bool initalized = false;
        private bool hooksSet = false;
        private bool forceFloatingScreen = false;

        private bool isMounted = false;
        private bool dalamudMode = false;
        private bool isCharMake = false;
        private bool isCharSelect = false;
        private bool isHousing = false;
        private byte targetAddonAlpha = 0;
        private RenderModes curRenderMode = RenderModes.None;
        private int curEye = 0;
        private int[] nextEye = { 1, 0 };
        private int[] swapEyes = { 1, 0 };
        private float Deg2Rad = MathF.PI / 180.0f;
        private float Rad2Deg = 180.0f / MathF.PI;
        private float cameraZoom = 0.0f;
        private float leftBumperValue = 0.0f;
        private float BridgeBoneHeight = 0.0f;
        private float armLength = 1.0f;
        private ChangedTypeBool mouseoverUI = new ChangedTypeBool();
        private ChangedTypeBool mouseoverTarget = new ChangedTypeBool();
        private ChangedTypeBool inCutscene = new ChangedTypeBool();
        private Vector2 rotateAmount = new Vector2(0.0f, 0.0f);
        private Point virtualMouse = new Point(0, 0);
        private Point actualMouse = new Point(0, 0);
        private Dictionary<ActionButtonLayout, bool> inputState = new Dictionary<ActionButtonLayout, bool>();
        private Dictionary<ActionButtonLayout, ChangedType<bool>> inputStatus = new Dictionary<ActionButtonLayout, ChangedType<bool>>();
        private Dictionary<ConfigOption, int> SavedSettings = new Dictionary<ConfigOption, int>();
        private Stack<bool> overrideFromParent = new Stack<bool>();
        private bool frfCalculateViewMatrix = false; // frf first run this frame
        private int ScreenMode = 0;
        private UInt64 selectScreenMouseOver = 0;
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

        private Queue<stMultiIK>[] multiIK = { new Queue<stMultiIK>(), new Queue<stMultiIK>() };
        private MovingAverage neckOffsetAvg = new MovingAverage();

        private bool isSetProjection = false;
        private Matrix4x4 curProjection = Matrix4x4.Identity;
        private Matrix4x4 curViewMatrixWithoutHMD = Matrix4x4.Identity;
        private Matrix4x4 curViewMatrixWithoutHMDI = Matrix4x4.Identity;
        private Matrix4x4 hmdMatrix = Matrix4x4.Identity;
        private Matrix4x4 hmdMatrixI = Matrix4x4.Identity;
        private Matrix4x4 lhcMatrix = Matrix4x4.Identity;
        private Matrix4x4 lhcPalmMatrix = Matrix4x4.Identity;
        private Matrix4x4 lhcMatrixI = Matrix4x4.Identity;
        private Matrix4x4 rhcMatrix = Matrix4x4.Identity;
        private Matrix4x4 rhcPalmMatrix = Matrix4x4.Identity;
        private Matrix4x4 rhcMatrixI = Matrix4x4.Identity;
        private Matrix4x4 fixedProjection = Matrix4x4.Identity;
        private Matrix4x4 hmdOffsetFirstPerson = Matrix4x4.Identity;
        private Matrix4x4 hmdOffsetThirdPerson = Matrix4x4.Identity;
        private Matrix4x4 hmdOffsetMountedFirstPerson = Matrix4x4.Identity;
        private Matrix4x4 hmdWorldScale = Matrix4x4.CreateScale(1.0f);
        private Matrix4x4 handWatch = Matrix4x4.Identity;
        private Matrix4x4 handBoneRay = Matrix4x4.Identity;
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

        private SceneCameraManager* scCameraManager = null;
        private ControlSystemCameraManager* csCameraManager = null;
        private ResourceManager* resourceManager = null;

        private Structures.RenderTargetManager* renderTargetManager = null;
        private FFXIVClientStructs.FFXIV.Client.System.Framework.Framework* frameworkInstance = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        private Device* dx11DeviceInstance = Device.Instance();
        private TargetSystem* targetSystem = TargetSystem.Instance();

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
            internal const string g_SelectScreenMouseOver = "48 8b 0D ?? ?? ?? ?? 48 85 C9 74 ?? BA 03 00 00 00 48 81 C1 20 09 00 00 45 33 C0 E8";
            internal const string g_DisableSetCursorPosAddr = "FF ?? ?? ?? ?? 00 C6 05 ?? ?? ?? ?? 00 0F B6 43 38";
            internal const string g_ResourceManagerInstance = "48 8B 05 ?? ?? ?? ?? 48 8B 08 48 8B 01 48 8B 40 08";

            internal const string GetCutsceneCameraOffset = "E8 ?? ?? ?? ?? 48 8B 70 48 48 85 F6";
            internal const string GameObjectGetPosition = "83 79 7C 00 75 09 F6 81 ?? ?? ?? ?? ?? 74 2A";
            internal const string GetTargetFromRay = "E8 ?? ?? ?? ?? 84 C0 74 ?? 48 8B F3";
            internal const string GetMouseOverTarget = "E8 ?? ?? ?? ?? 48 8B D8 48 85 DB 74 ?? 48 8B CB";
            internal const string ScreenPointToRay = "E8 ?? ?? ?? ?? 4C 8B E0 48 8B EB";
            internal const string ScreenPointToRay1 = "E8 ?? ?? ?? ?? F3 0F 10 45 A7 F3 0f 10 4D AB";
            internal const string MousePointScreenToClient = "E8 ?? ?? ?? ?? 0f B7 44 24 50 66 89 83 98 09 00 00";
            internal const string DisableCinemaBars = "E8 ?? ?? ?? ?? 48 8B 5F 10 48 8b 4B 30";

            internal const string SetRenderTarget = "E8 ?? ?? ?? ?? 40 38 BC 24 00 02 00 00";
            internal const string AllocateQueueMemory = "E8 ?? ?? ?? ?? 48 85 C0 74 ?? C7 00 04 00 00 00";
            internal const string Pushback = "E8 ?? ?? ?? ?? EB ?? 8B 87 6C 04 00 00";
            internal const string PushbackUI = "E8 ?? ?? ?? ?? EB 05 E8 ?? ?? ?? ?? 4C 8D 5C 24 50";
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
            internal const string FrameworkTick = "40 53 48 83 EC 20 FF 81 C8 16 00 00 48 8B D9 48 8D 4C 24 30";

            internal const string syncModelSpace = "48 83 EC 18 80 79 38 00 0F 85 ?? ?? ?? ?? 48 8B 01";
            internal const string GetBoneIndexFromName = "E8 ?? ?? ?? ?? 66 89 83 BC 01 00 00";
            internal const string twoBoneIK = "48 89 54 24 10 48 89 4C 24 08 55 53 56 41 57 48 8D AC 24 38 FC FF FF";
            internal const string threadedLookAtParent = "40 57 41 54 41 57 48 83 EC 30 4D 63 E0";
            internal const string lookAtIK = "48 8B C4 48 89 58 08 48 89 70 10 F3 0F 11 58 ??";

            internal const string RenderSkeletonList = "E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 48 85 C9 74 ?? 0F 28 CE";
            internal const string RenderSkeletonListSkeleton = "E8 ?? ?? ?? ?? 48 FF C3 48 83 C7 10 48 3B DE";
            internal const string RenderSkeletonListAnimation = "E8 ?? ?? ?? ?? 44 39 64 24 28";
            internal const string RenderSkeletonListPartialSkeleton = "E8 ?? ?? ?? ?? 48 8B CF E8 ?? ?? ?? ?? 48 81 C3 C0 01 00 00";

        }

        public static void PrintEcho(string message) => xivr.ChatGui!.Print($"[xivr] {message}");
        public static void PrintError(string message) => xivr.ChatGui!.PrintError($"[xivr] {message}");


        public void SetFunctionHandles()
        {
            //----
            // Gets a list of all the methods this class contains that are public and instanced (non static)
            // then looks for a specific attirbute attached to the class
            // Once found, create a delegate and add both the attribute and delegate to a dictionary
            //----
            functionList.Clear();
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            foreach (MethodInfo method in this.GetType().GetMethods(flags))
            {
                foreach (System.Attribute attribute in method.GetCustomAttributes(typeof(HandleStatus), false))
                {
                    string key = ((HandleStatus)attribute).fnName;
                    HandleStatusDelegate handle = (HandleStatusDelegate)HandleStatusDelegate.CreateDelegate(typeof(HandleStatusDelegate), this, method);

                    if (!functionList.ContainsKey(key))
                    {
                        if (xivr.cfg!.data.vLog)
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
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            foreach (MethodInfo method in this.GetType().GetMethods(flags))
            {
                foreach (System.Attribute attribute in method.GetCustomAttributes(typeof(HandleInputAttribute), false))
                {
                    ActionButtonLayout key = ((HandleInputAttribute)attribute).inputId;
                    HandleInputDelegate handle = (HandleInputDelegate)HandleInputDelegate.CreateDelegate(typeof(HandleInputDelegate), this, method);

                    if (!inputList.ContainsKey(key))
                    {
                        if (xivr.cfg!.data.vLog)
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
            if (xivr.cfg!.data.vLog)
                PluginLog.Log($"Initialize A {initalized} {hooksSet}");

            if (initalized == false)
            {
                SignatureHelper.Initialise(this);

                BaseAddress = (UInt64)Process.GetCurrentProcess()?.MainModule?.BaseAddress;
                frameworkInstance = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();

                IntPtr tmpAddress = xivr.SigScanner!.GetStaticAddressFromSig(Signatures.g_SceneCameraManagerInstance);
                scCameraManager = (SceneCameraManager*)(*(UInt64*)tmpAddress);

                tls_index = (UInt64)xivr.SigScanner!.GetStaticAddressFromSig(Signatures.g_tls_index);
                globalScaleAddress = (UInt64)xivr.SigScanner!.GetStaticAddressFromSig(Signatures.g_TextScale);
                RenderTargetManagerAddress = (UInt64)xivr.SigScanner!.GetStaticAddressFromSig(Signatures.g_RenderTargetManagerInstance);
                csCameraManager = (ControlSystemCameraManager*)xivr.SigScanner!.GetStaticAddressFromSig(Signatures.g_ControlSystemCameraManager);
                charList = (CharSelectionCharList*)xivr.SigScanner!.GetStaticAddressFromSig(Signatures.g_SelectScreenCharacterList);
                selectScreenMouseOver = (UInt64)xivr.SigScanner!.GetStaticAddressFromSig(Signatures.g_SelectScreenMouseOver);

                if (dx11DeviceInstance == null)
                    dx11DeviceInstance = Device.Instance();

                /*
                resourceManager = *(ResourceManager**)xivr.SigScanner!.GetStaticAddressFromSig(Signatures.g_ResourceManagerInstance);
                if (resourceManager != null)
                {
                    foreach(StdPair<uint, Pointer<StdMap<uint, Pointer<ResourceHandle>>>> item in *resourceManager->ResourceGraph->CharaContainer.MainMap)
                    {
                        foreach (StdPair<uint, Pointer<ResourceHandle>> inner in *item.Item2.Value)
                        {
                            SkeletonResourceHandle* skelResource = (SkeletonResourceHandle*)inner.Item2.Value;
                            if (skelResource->ResourceHandle.FileType == 0x736B6C62) // blks
                            {
                                string filename = "" + skelResource->ResourceHandle.FileName;
                                string[] parts = filename.Split('/');
                                if(parts.Length > 4)
                                    PluginLog.Log($"{skelResource->ResourceHandle.Id} {skelResource->ResourceHandle.Category} {parts[1]} {parts[4]}");
                                else
                                    PluginLog.Log($"{skelResource->ResourceHandle.Id} {skelResource->ResourceHandle.Category} {filename}");
                            }
                        }
                    }
                }
                */

                DisableSetCursorPosAddr = (UInt64)xivr.SigScanner!.ScanText(Signatures.g_DisableSetCursorPosAddr);
                DisableSetCursorPosOrig = *(UInt64*)DisableSetCursorPosAddr;
                renderTargetManager = *(Structures.RenderTargetManager**)RenderTargetManagerAddress;

                curRenderMode = RenderModes.None;
                GetThreadedDataInit();
                SetFunctionHandles();
                SetInputHandles();

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
            if (xivr.cfg!.data.vLog)
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

            if (dx11DeviceInstance == null)
                dx11DeviceInstance = Device.Instance();

            if (initalized && !hooksSet && xivr.VR_IsHmdPresent())
            {
                if (xivr.cfg.data.vLog)
                    PluginLog.Log($"SetDX Dx: {(IntPtr)dx11DeviceInstance:x} | RndTrg:{*(IntPtr*)RenderTargetManagerAddress:x}");

                if (!Imports.SetDX11((IntPtr)dx11DeviceInstance, *(IntPtr*)RenderTargetManagerAddress, xivr.PluginInterface!.AssemblyLocation.DirectoryName!))
                    return false;

                string filePath = Path.Join(xivr.PluginInterface!.AssemblyLocation.DirectoryName, "config", "actions.json");
                if (Imports.SetActiveJSON(filePath, filePath.Length) == false)
                    PluginLog.LogError($"Error loading Json file : {filePath}");

                SetRenderingMode();

                SavedSettings[ConfigOption.MouseOpeLimit] = ConfigModule.Instance()->GetIntValue(ConfigOption.MouseOpeLimit);
                if (ConfigModule.Instance()->GetIntValue(ConfigOption.Gamma) == 50)
                    ConfigModule.Instance()->SetOption(ConfigOption.Gamma, 49);
                ConfigModule.Instance()->SetOption(ConfigOption.Fps, 0);
                ConfigModule.Instance()->SetOption(ConfigOption.MouseOpeLimit, 1);

                if (DisableSetCursorPosAddr != 0)
                    SafeMemory.Write<UInt64>((IntPtr)DisableSetCursorPosAddr, DisableSetCursorPosOverride);

                //----
                // Set the near clip
                //----
                csCameraManager->GameCamera->Camera.BufferData->NearClip = 0.05f;

                neckOffsetAvg.Reset();

                //----
                // Loop though the bone enum list and convert it to a dict
                //----
                int j = 0;
                BoneOutput.boneNameToEnum.Clear();
                foreach (string i in Enum.GetNames(typeof(BoneList)))
                {
                    BoneOutput.boneNameToEnum.Add(i, (BoneList)j);
                    j++;
                }

                //----
                // Enable all hooks
                //----
                foreach (KeyValuePair<string, HandleStatusDelegate> attrib in functionList)
                    attrib.Value(true);

                hooksSet = true;
                if (xivr.ClientState!.LocalPlayer)
                    OnLogin(null, new EventArgs());
                PrintEcho("Starting VR.");
            }
            if (xivr.cfg.data.vLog)
                PluginLog.Log($"Start B {initalized} {hooksSet}");

            return hooksSet;
        }



        public void Stop()
        {
            if (xivr.cfg!.data.vLog)
                PluginLog.Log($"Stop A {initalized} {hooksSet}");
            if (hooksSet)
            {
                //----
                // Disable all hooks
                //----
                foreach (KeyValuePair<string, HandleStatusDelegate> attrib in functionList)
                    attrib.Value(false);

                BoneOutput.boneNameToEnum.Clear();

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

                ConfigModule.Instance()->SetOption(ConfigOption.Gamma, 0);
                ConfigModule.Instance()->SetOption(ConfigOption.Fps, 0);
                ConfigModule.Instance()->SetOption(ConfigOption.MouseOpeLimit, 0);

                //----
                // Reset the FOV values
                //----
                RawGameCamera* rgc = csCameraManager->GetActive();
                rgc->MinFoV = 0.680f;
                rgc->CurrentFoV = 0.780f;
                rgc->MaxFoV = 0.780f;

                //----
                // Reset the near clip
                //----
                csCameraManager->GameCamera->Camera.BufferData->NearClip = 0.1f;

                //----
                // Restores the target arrow alpha and remove the vr cursor
                //----
                fixed (AtkTextNode** pvrTargetCursor = &vrTargetCursor)
                    VRCursor.FreeVRTargetCursor(pvrTargetCursor);

                AtkUnitBase* targetAddon = (AtkUnitBase*)xivr.GameGui!.GetAddonByName("_TargetCursor", 1);
                if (targetAddon != null)
                    targetAddon->Alpha = targetAddonAlpha;


                FirstToThirdPersonView();
                if (DisableSetCursorPosAddr != 0)
                    SafeMemory.Write<UInt64>((IntPtr)DisableSetCursorPosAddr, DisableSetCursorPosOrig);

                Imports.UnsetDX11();
                OnLogout(null, new EventArgs());
                hooksSet = false;
                PrintEcho("Stopping VR.");
            }
            if (xivr.cfg.data.vLog)
                PluginLog.Log($"Stop B {initalized} {hooksSet}");
        }

        private void FirstToThirdPersonView()
        {
            Imports.Recenter();
            //----
            // Set the near clip
            //----
            csCameraManager->GameCamera->Camera.BufferData->NearClip = 0.05f;
            neckOffsetAvg.Reset();
            PlayerCharacter? player = xivr.ClientState!.LocalPlayer;
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
            //----
            // Set the near clip
            //----
            csCameraManager->GameCamera->Camera.BufferData->NearClip = 0.05f;
            neckOffsetAvg.Reset();
            PlayerCharacter? player = xivr.ClientState!.LocalPlayer;
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


        Dictionary<ushort, Bone> boneLayoutA = new Dictionary<ushort, Bone>();
        Dictionary<UInt64, Dictionary<BoneList, short>> boneLayout = new Dictionary<UInt64, Dictionary<BoneList, short>>();
        Dictionary<UInt64, Bone[]> rawBoneList = new Dictionary<UInt64, Bone[]>();
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



        Matrix4x4 plrSkeletonPosition = Matrix4x4.Identity;
        Matrix4x4 plrSkeletonPositionI = Matrix4x4.Identity;
        Matrix4x4 mntSkeletonPosition = Matrix4x4.Identity;
        Matrix4x4 mntSkeletonPositionI = Matrix4x4.Identity;
        //Matrix4x4[] headBoneMatrix = { Matrix4x4.Identity, Matrix4x4.Identity };
        Matrix4x4 headBoneMatrix = Matrix4x4.Identity;
        Matrix4x4 headBoneMatrixI = Matrix4x4.Identity;
        Vector3 eyeMidPoint = new Vector3(0, 0, 0);
        Vector3[] hmdOffsetPerEye = { new Vector3(0, 0, 0), new Vector3(0, 0, 0),
                                      new Vector3(0, 0, 0), new Vector3(0, 0, 0) };
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
        GameObject* curMouseOverTarget = null;
        System.Timers.Timer RayOverFeedbackTimer = new System.Timers.Timer();
        public void RayOverFeedbackTick(System.Timers.Timer timer)
        {
            Imports.HapticFeedback(ActionButtonLayout.haptics_right, 0.1f, 1.0f, 0.25f);
            timer.Enabled = false;
        }


        public void Update(Dalamud.Game.Framework framework_)
        {
        }
        public void RunUpdate()
        {
            if (hooksSet && enableVR)
            {
                Imports.UpdateController(controllerCallback);
                hmdMatrix = Imports.GetFramePose(poseType.hmdPosition, -1);// * hmdWorldScale;
                lhcMatrix = Imports.GetFramePose(poseType.LeftHand, -1);// * hmdWorldScale;
                lhcPalmMatrix = Imports.GetFramePose(poseType.LeftHandPalm, -1);// * hmdWorldScale;
                rhcMatrix = Imports.GetFramePose(poseType.RightHand, -1);// * hmdWorldScale;
                rhcPalmMatrix = Imports.GetFramePose(poseType.RightHandPalm, -1);// * hmdWorldScale;
                //hmdMatrix.Translation = headBoneMatrix.Translation;
                Matrix4x4.Invert(hmdMatrix, out hmdMatrixI);

                handWatch = lhcMatrix * hmdWorldScale;
                handBoneRay = rhcMatrix * hmdWorldScale;

                frfCalculateViewMatrix = false;

                Point currentMouse = new Point();
                Point halfScreen = new Point();
                if (dx11DeviceInstance != null && dx11DeviceInstance->SwapChain != null)
                {
                    halfScreen.X = ((int)dx11DeviceInstance->SwapChain->Width / 2);
                    halfScreen.Y = ((int)dx11DeviceInstance->SwapChain->Height / 2);
                }

                ScreenSettings* screenSettings = *(ScreenSettings**)((UInt64)frameworkInstance + 0x7A8);
                //PluginLog.Log($"{(int)dx11DeviceInstance->SwapChain->Height} {(int)dx11DeviceInstance->SwapChain->Width}");
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

                //----
                // Changes to 3rd person when a cutscene is triggered
                // and back when it ends
                //----
                if (inCutscene.Changed)
                    if (inCutscene.Current)
                        FirstToThirdPersonView();
                    else
                        if (gameMode.Current == CameraModes.FirstPerson)
                        ThirdToFirstPersonView();

                isMounted = false;

                PlayerCharacter? player = xivr.ClientState!.LocalPlayer;
                if (player != null)
                {
                    Character* bonedCharacter = (Character*)player.Address;
                    isMounted = bonedCharacter->IsMounted();
                }

                //----
                // Haptics if mouse over target changes
                //----
                mouseoverTarget.Current = (targetSystem->MouseOverTarget == curMouseOverTarget && targetSystem->MouseOverTarget != null);
                curMouseOverTarget = targetSystem->MouseOverTarget;
                if (mouseoverTarget.Current && mouseoverTarget.Changed)
                {
                    RayOverFeedbackTimer.Interval = 250;
                    RayOverFeedbackTimer.Elapsed += (sender, e) =>
                    {
                        RayOverFeedbackTick(RayOverFeedbackTimer);
                    };
                    RayOverFeedbackTimer.Enabled = true;
                }
                else if (!mouseoverTarget.Current && mouseoverTarget.Changed)
                {
                    RayOverFeedbackTimer.Enabled = false;
                }

                //----
                // Saves the target arrow alpha
                //----
                if (targetAddonAlpha == 0)
                {
                    AtkUnitBase* targetAddon = (AtkUnitBase*)xivr.GameGui!.GetAddonByName("_TargetCursor", 1);
                    if (targetAddon != null)
                        targetAddonAlpha = targetAddon->Alpha;
                }

                isCharMake = (AtkUnitBase*)xivr.GameGui!.GetAddonByName("_CharaMakeTitle", 1) != null;
                isCharSelect = (AtkUnitBase*)xivr.GameGui!.GetAddonByName("_CharaSelectTitle", 1) != null;
                isHousing = (AtkUnitBase*)xivr.GameGui!.GetAddonByName("HousingGoods", 1) != null;

                if (isCharSelect || isCharMake)
                    gameMode.Current = CameraModes.ThirdPerson;

                if (!isCharMake && !isCharSelect && xivr.ClientState!.LocalPlayer == null)
                    timer = 100;

                if (timer > 0)
                {
                    forceFloatingScreen = true;
                    timer--;
                }
                else if (timer == 0)
                {
                    timer = -1;
                }

                //if (curRenderMode == RenderModes.TwoD)
                //    curEye = 0;
                //else
                //    curEye = nextEye[curEye];
                //curEye = 0;
            }

            //xivr.cfg!.data.immersiveMovement = true;
            //isMounted = false;
            //SetFramePose();
            //PluginLog.Log($"-- Update --  {curEye}");
        }

        public void toggleDalamudMode()
        {
            dalamudMode = !dalamudMode;
        }

        public void ForceFloatingScreen(bool forceFloating, bool isCutscene)
        {
            forceFloatingScreen = forceFloating;
            inCutscene.Current = isCutscene;
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
            if (dx11DeviceInstance == null)
                dx11DeviceInstance = Device.Instance();

            dx11DeviceInstance->NewWidth = (uint)width;
            dx11DeviceInstance->NewHeight = (uint)height;
            dx11DeviceInstance->RequestResolutionChange = 1;

            //----
            // Resizes the client window to match the internal buffers
            //----
            ScreenSettings* screenSettings = *(ScreenSettings**)((UInt64)frameworkInstance + 0x7A8);
            if (screenSettings != null && screenSettings->hWnd != 0)
                Imports.ResizeWindow((IntPtr)screenSettings->hWnd, width, height);
        }

        public void WindowMove(bool reset)
        {
            int mainScreenAdapter = ConfigModule.Instance()->GetIntValue(ConfigOption.MainAdapter);
            ScreenSettings* screenSettings = *(ScreenSettings**)((UInt64)frameworkInstance + 0x7A8);
            Imports.MoveWindowPos((IntPtr)screenSettings->hWnd, mainScreenAdapter, reset);
        }

        public void SetRenderingMode()
        {
            if (hooksSet && enableVR)
            {
                RenderModes rMode = curRenderMode;
                if (xivr.cfg!.data.mode2d)
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
                hmdWorldScale = Matrix4x4.Identity;
                if (gameMode.Current == CameraModes.FirstPerson)
                    hmdWorldScale = Matrix4x4.CreateScale((armLength / 0.5f) * (xivr.cfg!.data.armMultiplier / 100.0f));

                gameProjectionMatrix[0] = Imports.GetFramePose(poseType.Projection, 0);
                gameProjectionMatrix[1] = Imports.GetFramePose(poseType.Projection, 1);
                gameProjectionMatrix[0].M43 *= -1;
                gameProjectionMatrix[1].M43 *= -1;

                hmdOffsetFirstPerson = Matrix4x4.CreateTranslation(0, (xivr.cfg.data.offsetAmountYFPS / 100), (xivr.cfg.data.offsetAmountZFPS / 100));
                //hmdOffsetFirstPerson *= hmdWorldScale;

                hmdOffsetThirdPerson = Matrix4x4.CreateTranslation((xivr.cfg.data.offsetAmountX / 100), (xivr.cfg.data.offsetAmountY / 100), (xivr.cfg.data.offsetAmountZ / 100));
                //hmdOffsetThirdPerson *= hmdWorldScale;

                hmdOffsetMountedFirstPerson = Matrix4x4.CreateTranslation(0, (xivr.cfg.data.offsetAmountYFPSMount / 100), (xivr.cfg.data.offsetAmountZFPSMount / 100));
                //hmdOffsetMountedFirstPerson *= hmdWorldScale;
            }
        }

        public void OnLogin(object? sender, EventArgs e)
        {
            if (hooksSet && enableVR)
            {
                SetRenderingMode();

                //if (DisableCameraCollisionAddr != 0)
                //    SafeMemory.Write<UInt64>((IntPtr)DisableCameraCollisionAddr, DisableCameraCollisionOverride);
            }
        }

        public void OnLogout(object? sender, EventArgs e)
        {
            //----
            // Sets the lengths of the TargetSystem to 0 as they keep their size
            // even though the data is reset
            //----
            targetSystem->ObjectFilterArray0.Length = 0;
            targetSystem->ObjectFilterArray1.Length = 0;
            targetSystem->ObjectFilterArray2.Length = 0;
            targetSystem->ObjectFilterArray3.Length = 0;

            if (hooksSet && enableVR)
            {
                //if (DisableCameraCollisionAddr != 0)
                //    SafeMemory.Write<UInt64>((IntPtr)DisableCameraCollisionAddr, DisableCameraCollisionOrig);
            }
        }

        public void Dispose()
        {
            if (xivr.cfg!.data.vLog)
                PluginLog.Log($"Dispose A {initalized} {hooksSet}");
            if (getThreadedDataHandle.IsAllocated)
                getThreadedDataHandle.Free();
            initalized = false;
            if (xivr.cfg!.data.vLog)
                PluginLog.Log($"Dispose B {initalized} {hooksSet}");
        }

        private void AddClearCommand(Structures.Texture* rendTexture, Structures.Texture* depthTexture, bool depth = false, float r = 0, float g = 0, float b = 0, float a = 0)
        {
            UInt64 threadedOffset = GetThreadedOffset();
            if (threadedOffset != 0)
            {
                SetRenderTargetFn!(threadedOffset, 1, &rendTexture, depthTexture, 0, 0);
                AddClearCommand(depth, r, g, b, a);
            }
        }

        private void AddClearCommand(bool depth = false, float r = 0, float g = 0, float b = 0, float a = 0)
        {
            UInt64 threadedOffset = GetThreadedOffset();
            if (threadedOffset != 0)
            {
                UInt64 queueData = AllocateQueueMemmoryFn!(threadedOffset, 0x38);
                if (queueData != 0)
                {
                    Imports.ZeroMemory((byte*)queueData, 0x38);
                    cmdType4* cmd = (cmdType4*)queueData;
                    cmd->SwitchType = 4;
                    cmd->clearType = ((depth) ? 7 : 1);
                    cmd->colorR = r;
                    cmd->colorG = g;
                    cmd->colorB = b;
                    cmd->colorA = a;
                    cmd->clearDepth = 1;
                    cmd->clearStencil = 0;
                    cmd->clearCheck = 0;
                    PushbackFn((threadedOffset + 0x18), (UInt64)(*(int*)(threadedOffset + 0x8)), queueData);
                }
            }
        }


        private void AddCopyResourceCommand(Structures.Texture* destination, Structures.Texture* source, int eye = -1)
        {
            UInt64 threadedOffset = GetThreadedOffset();
            if (threadedOffset != 0)
            {
                UInt64 queueData = AllocateQueueMemmoryFn!(threadedOffset, 0x48);
                if (queueData != 0)
                {
                    Imports.ZeroMemory((byte*)queueData, 0x48);
                    cmdType10* cmd = (cmdType10*)queueData;
                    cmd->SwitchType = 10;
                    cmd->uk1 = 0;
                    cmd->Destination = destination;
                    cmd->subResourceDestination = 0;
                    cmd->X = 0;
                    cmd->Y = 0;
                    cmd->Z = 0;
                    cmd->Source = source;
                    cmd->subResourceSource = 0;
                    cmd->useRect = 0;
                    cmd->rectTop = 0;
                    cmd->rectLeft = 0;
                    cmd->rectBottom = 0;
                    cmd->rectLeft = 0;
                    PushbackFn((threadedOffset + 0x18), (UInt64)(*(int*)(threadedOffset + 0x8)), queueData);
                }
            }
        }

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
        private delegate void SetRenderTargetDg(UInt64 a, UInt64 b, Structures.Texture** c, Structures.Texture* d, UInt64 e, UInt64 f);
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
            if (hooksSet && enableVR)
            {
                Structures.Texture* texture = Imports.GetUIRenderTexture(curEye);
                UInt64 threadedOffset = GetThreadedOffset();
                SetRenderTargetFn!(threadedOffset, 1, &texture, null, 0, 0);
                AddClearCommand();

                overrideFromParent.Push(true);
                PushbackUIHook!.Original(a, b);
                overrideFromParent.Pop();
            }
            else
                PushbackUIHook!.Original(a, b);
        }

        //----
        // ScreenPointToRay
        //----
        private delegate Ray* ScreenPointToRayDg(RawGameCamera* gameCamera, Ray* ray, int mousePosX, int mousePosY);
        [Signature(Signatures.ScreenPointToRay, DetourName = nameof(ScreenPointToRayFn))]
        private Hook<ScreenPointToRayDg> ScreenPointToRayHook = null;

        [HandleStatus("ScreenPointToRay")]
        public void ScreenPointToRayStatus(bool status)
        {
            if (status == true)
                ScreenPointToRayHook?.Enable();
            else
                ScreenPointToRayHook?.Disable();
        }
        private Ray* ScreenPointToRayFn(RawGameCamera* gameCamera, Ray* ray, int mousePosX, int mousePosY)
        {
            if (hooksSet && enableVR)
            {
                if (xivr.cfg!.data.motioncontrol)
                {
                    Matrix4x4 rayPos = handBoneRay * curViewMatrixWithoutHMDI;
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
            }
            else
                ScreenPointToRayHook!.Original(gameCamera, ray, mousePosX, mousePosY);
            return ray;
        }


        //----
        // ScreenPointToRay1
        //----
        private delegate void ScreenPointToRay1Dg(Ray* ray, float* mousePos);
        [Signature(Signatures.ScreenPointToRay1, DetourName = nameof(ScreenPointToRay1Fn))]
        private Hook<ScreenPointToRay1Dg> ScreenPointToRay1Hook = null;

        [HandleStatus("ScreenPointToRay")]
        public void ScreenPointToRay1Status(bool status)
        {
            if (status == true)
                ScreenPointToRay1Hook?.Enable();
            else
                ScreenPointToRay1Hook?.Disable();
        }
        private void ScreenPointToRay1Fn(Ray* ray, float* mousePos)
        {
            if (hooksSet && enableVR)
            {
                if (xivr.cfg!.data.motioncontrol)
                {
                    Matrix4x4 rayPos = handBoneRay * curViewMatrixWithoutHMDI;
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
            }
            else
                ScreenPointToRay1Hook!.Original(ray, mousePos);
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
            if (hooksSet && enableVR)
                *mousePos = virtualMouse;
            else
                MousePointScreenToClientHook!.Original(frameworkInstance, mousePos);
        }


        //----
        // DisableCinemaBars
        //----
        private delegate void DisableCinemaBarsDg(UInt64 a1);
        [Signature(Signatures.DisableCinemaBars, DetourName = nameof(DisableCinemaBarsFn))]
        private Hook<DisableCinemaBarsDg> DisableCinemaBarsHook = null;

        [HandleStatus("DisableCinemaBars")]
        public void DisableCinemaBarsStatus(bool status)
        {
            if (status == true)
                DisableCinemaBarsHook?.Enable();
            else
                DisableCinemaBarsHook?.Disable();
        }
        private void DisableCinemaBarsFn(UInt64 a1)
        {
            return;
            //DisableCinemaBarsHook!.Original(a1);
        }



        //----
        // AtkUnitBase OnRequestedUpdate
        //----
        private delegate void OnRequestedUpdateDg(UInt64 a, UInt64 b, UInt64 c);
        [Signature(Signatures.OnRequestedUpdate, DetourName = nameof(OnRequestedUpdateFn))]
        private Hook<OnRequestedUpdateDg>? OnRequestedUpdateHook = null;

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
            if (hooksSet && enableVR)
            {
                float globalScale = *(float*)globalScaleAddress;
                *(float*)globalScaleAddress = 1;
                OnRequestedUpdateHook!.Original(a, b, c);
                *(float*)globalScaleAddress = globalScale;
            }
            else
                OnRequestedUpdateHook!.Original(a, b, c);
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
            if (hooksSet && enableVR && curEye == 1)
            {
                Imports.RenderUI();
                DXGIPresentHook!.Original(a, b);
                Imports.RenderVR(curProjection, curViewMatrixWithoutHMD, handBoneRay, handWatch, virtualMouse, dalamudMode, forceFloatingScreen);
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
            if (hooksSet && enableVR && (b + 0x8) != 0)
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
            if (hooksSet && enableVR)
            {
                overrideFromParent.Push(true);
                CamManagerSetMatrixHook!.Original(camMngrInstance);
                overrideFromParent.Pop();
            }
            else
                CamManagerSetMatrixHook!.Original(camMngrInstance);
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
            if (hooksSet && enableVR)
            {
                overrideFromParent.Push(true);
                CSUpdateConstBufHook!.Original(a, b);
                overrideFromParent.Pop();
            }
            else
                CSUpdateConstBufHook!.Original(a, b);
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
            if (hooksSet && enableVR && overrideFn)
            {
                Structures.Texture* texture = Imports.GetUIRenderTexture(curEye);
                UInt64 threadedOffset = GetThreadedOffset();
                SetRenderTargetFn!(threadedOffset, 1, &texture, null, 0, 0);
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
        private void CalculateViewMatrixFn(RawGameCamera* rawGameCamera)
        {
            if (hooksSet && enableVR && (!frfCalculateViewMatrix || inCutscene.Current))
            {
                //if (scCameraManager->CameraIndex == 0 && csCameraManager->ActiveCameraIndex == 0)
                if (!inCutscene.Current)
                {
                    if (xivr.cfg!.data.ultrawideshadows == true)
                        rawGameCamera->CurrentFoV = 2.54f; // ultra wide
                    else
                        rawGameCamera->CurrentFoV = 1.65f;
                    rawGameCamera->MinFoV = rawGameCamera->CurrentFoV;
                    rawGameCamera->MaxFoV = rawGameCamera->CurrentFoV;
                }

                Matrix4x4 horizonLockMatrix = Matrix4x4.Identity;
                frfCalculateViewMatrix = true;

                if (inCutscene.Current || gameMode.Current == CameraModes.ThirdPerson || (!xivr.cfg!.data.immersiveMovement && !isMounted))
                {
                    if (gameMode.Current == CameraModes.ThirdPerson)
                        neckOffsetAvg.AddNew(rawGameCamera->Position.Y);
                }
                else
                {
                    UpdateBoneCamera();
                    Vector3 frontBackDiff = rawGameCamera->LookAt - rawGameCamera->Position;
                    //rawGameCamera->Position = headBoneMatrix[curEye].Translation;
                    rawGameCamera->Position = headBoneMatrix.Translation;
                    rawGameCamera->LookAt = rawGameCamera->Position + frontBackDiff;
                }
                //PluginLog.Log($"{}");
                rawGameCamera->ViewMatrix = Matrix4x4.Identity;
                CalculateViewMatrixHook!.Original(rawGameCamera);

                if (!forceFloatingScreen)
                {
                    PlayerCharacter? player = xivr.ClientState!.LocalPlayer;
                    if (player != null)
                    {
                        if ((xivr.cfg!.data.horizonLock || gameMode.Current == CameraModes.FirstPerson))
                        {
                            horizonLockMatrix = Matrix4x4.CreateFromAxisAngle(new Vector3(1, 0, 0), rawGameCamera->CurrentVRotation);
                            rawGameCamera->LookAt.Y = rawGameCamera->Position.Y;
                        }

                        if (gameMode.Current == CameraModes.FirstPerson)
                            if (!isMounted)
                                horizonLockMatrix = hmdOffsetFirstPerson * horizonLockMatrix;
                            else
                                horizonLockMatrix = hmdOffsetMountedFirstPerson * horizonLockMatrix;
                        else
                            horizonLockMatrix = hmdOffsetThirdPerson * horizonLockMatrix;

                        //horizonLockMatrix.M42 -= camNeckDiffA;//.Average;
                    }

                    if (inCutscene.Current)
                        curViewMatrixWithoutHMD = rawGameCamera->ViewMatrix;
                    else
                        curViewMatrixWithoutHMD = rawGameCamera->ViewMatrix * horizonLockMatrix;
                    Matrix4x4.Invert(curViewMatrixWithoutHMD, out curViewMatrixWithoutHMDI);

                    //curViewMatrix = rawGameCamera->ViewMatrix * hmdMatrix;
                    //PluginLog.Log($"hmdMatrix: {rawGameCamera->X} {rawGameCamera->Y} {rawGameCamera->Z} | {rawGameCamera->ViewMatrix.M41}, {rawGameCamera->ViewMatrix.M42}, {rawGameCamera->ViewMatrix.M43} | {hmdMatrix.M41}, {hmdMatrix.M42},  {hmdMatrix.M43}");

                    if (xivr.cfg!.data.swapEyes)
                        rawGameCamera->ViewMatrix = curViewMatrixWithoutHMD * hmdMatrixI * eyeOffsetMatrix[swapEyes[curEye]];
                    else
                        rawGameCamera->ViewMatrix = curViewMatrixWithoutHMD * hmdMatrixI * eyeOffsetMatrix[curEye];
                    //if (!inCutscene && gameMode == CameraModes.FirstPerson)
                    //    rawGameCamera->ViewMatrix = hmdMatrixI * eyeOffsetMatrix[curEye];
                }
                else
                {
                    curViewMatrixWithoutHMD = rawGameCamera->ViewMatrix;
                    Matrix4x4.Invert(curViewMatrixWithoutHMD, out curViewMatrixWithoutHMDI);
                }
            }
            else
            {

                rawGameCamera->ViewMatrix = Matrix4x4.Identity;
                CalculateViewMatrixHook!.Original(rawGameCamera);
                curViewMatrixWithoutHMD = rawGameCamera->ViewMatrix;
                Matrix4x4.Invert(curViewMatrixWithoutHMD, out curViewMatrixWithoutHMDI);
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
            if (hooksSet && enableVR && !forceFloatingScreen && !inCutscene.Current)
            {
                gameMode.Current = gameCamera->Camera.Mode;

                if (xivr.cfg!.data.horizontalLock)
                    gameCamera->Camera.HRotationThisFrame2 = 0;
                if (xivr.cfg!.data.verticalLock)
                    gameCamera->Camera.VRotationThisFrame2 = 0;

                gameCamera->Camera.HRotationThisFrame2 += rotateAmount.X;

                if (gameMode.Current == CameraModes.FirstPerson)
                {
                    gameCamera->Camera.VRotationThisFrame1 = 0.0f;
                    gameCamera->Camera.VRotationThisFrame2 = 0.0f;
                }

                if (xivr.cfg!.data.vertloc)
                    gameCamera->Camera.VRotationThisFrame2 += rotateAmount.Y;
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
            overrideMatrix |= inCutscene.Current;

            Matrix4x4 retVal = MakeProjectionMatrix2Hook!.Original(projMatrix, b, c, d, e);
            if (overrideMatrix)
                curProjection = retVal;
            if (hooksSet && enableVR && overrideMatrix && !forceFloatingScreen)
            {
                if (xivr.cfg!.data.swapEyes)
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
            if (hooksSet && enableVR)
            {
                //----
                // Disables the target arrow until it can be put in the world
                //----
                AtkUnitBase* targetAddon = (AtkUnitBase*)xivr.GameGui!.GetAddonByName("_TargetCursor", 1);
                if (targetAddon != null)
                {
                    targetAddon->Alpha = 1;
                    targetAddon->Hide(true);
                    //targetAddon->RootNode->SetUseDepthBasedPriority(true);
                }

                fixed (AtkTextNode** pvrTargetCursor = &vrTargetCursor)
                    VRCursor.SetupVRTargetCursor(pvrTargetCursor, xivr.cfg!.data.targetCursorSize);

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

                    TargetSystem* targSys = (TargetSystem*)xivr.TargetManager!.Address;
                    if (objectInfo->GameObject == targSys->Target)
                    {
                        selectedNamePlate = &a->NamePlateObjectArray[objectInfo->NamePlateIndex];
                        break;
                    }
                }

                fixed (AtkTextNode** pvrTargetCursor = &vrTargetCursor)
                {
                    VRCursor.UpdateVRCursorSize(pvrTargetCursor, xivr.cfg!.data.targetCursorSize);
                    VRCursor.SetVRCursor(pvrTargetCursor, selectedNamePlate);
                }
            }

            NamePlateDrawHook!.Original(a);
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
            PlayerCharacter? player = xivr.ClientState!.LocalPlayer;
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
            if (hooksSet && enableVR)
            {
                PlayerCharacter? player = xivr.ClientState!.LocalPlayer;
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
            if (hooksSet && enableVR)
            {
                PlayerCharacter? player = xivr.ClientState!.LocalPlayer;
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

            if (hooksSet && enableVR)
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
                        //PluginLog.Log($"GetAnalogueValueFn: {retVal} {leftBumperValue}");
                        if (MathF.Abs(retVal) >= 0 && MathF.Abs(retVal) < 15) rightHorizontalCenter = true;
                        if (xivr.cfg!.data.horizontalLock && MathF.Abs(leftBumperValue) < 0.5)
                        {
                            if (MathF.Abs(retVal) > 75 && rightHorizontalCenter)
                            {
                                rightHorizontalCenter = false;
                                rotateAmount.X -= (xivr.cfg!.data.snapRotateAmountX * Deg2Rad) * MathF.Sign(retVal);
                            }
                            retVal = 0;
                        }
                        break;
                    case 6:
                        //PluginLog.Log($"GetAnalogueValueFn: {retVal}");
                        if (MathF.Abs(retVal) >= 0 && MathF.Abs(retVal) < 15) rightVerticalCenter = true;
                        if (xivr.cfg!.data.verticalLock && MathF.Abs(leftBumperValue) < 0.5)
                        {
                            if (MathF.Abs(retVal) > 75 && rightVerticalCenter && gameMode.Current == CameraModes.ThirdPerson)
                            {
                                rightVerticalCenter = false;
                                rotateAmount.Y -= (xivr.cfg!.data.snapRotateAmountY * Deg2Rad) * MathF.Sign(retVal);
                            }
                            retVal = 0;
                        }
                        break;
                }
            }
            return retVal;
        }

        //private delegate void SwapClothesDg(UInt64 DrawData, uint index, uint item, byte d);
        //[Signature("E8 ?? ?? ?? ?? FF C3 4D 8D 76 04", Fallibility = Fallibility.Fallible)]
        //private SwapClothesDg? SwapClothesFn = null;

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
        ChangedTypeBool rightBumperClick = new ChangedTypeBool();
        ChangedTypeBool rightTriggerClick = new ChangedTypeBool();
        float leftStickOrig = 0;
        Stopwatch leftStickTimer = new Stopwatch();
        ChangedTypeBool leftStickTimerHaptic = new ChangedTypeBool();
        float rightStickOrig = 0;
        Stopwatch rightStickTimer = new Stopwatch();
        ChangedTypeBool rightStickTimerHaptic = new ChangedTypeBool();

        public void ControllerInputFn(UInt64 a, UInt64 b, uint c)
        {
            UInt64 controllerBase = *(UInt64*)(a + 0x70);
            UInt64 controllerIndex = *(byte*)(a + 0x434);

            UInt64 controllerAddress = controllerBase + 0x30 + ((controllerIndex * 0x1E6) * 4);
            XBoxButtonOffsets* offsets = (XBoxButtonOffsets*)((controllerIndex * 0x798) + controllerBase);

            leftBumperValue = *(float*)(controllerAddress + (UInt64)(offsets->left_bumper * 4));


            if (hooksSet && enableVR && xivr.cfg!.data.motioncontrol)
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



            bool doLocomotion = false;
            Vector3 angles = new Vector3();
            if (xivr.cfg!.data.conloc)
            {
                angles = GetAngles(lhcMatrix);
                doLocomotion = true;
            }
            else if (xivr.cfg!.data.hmdloc)
            {
                angles = GetAngles(hmdMatrix);
                doLocomotion = true;
            }
            if (doLocomotion)
            {
                float up_down = (*(float*)(controllerAddress + (UInt64)(offsets->left_stick_up * 4))) + -(*(float*)(controllerAddress + (UInt64)(offsets->left_stick_down * 4)));
                float left_right = -(*(float*)(controllerAddress + (UInt64)(offsets->left_stick_left * 4))) + (*(float*)(controllerAddress + (UInt64)(offsets->left_stick_right * 4)));

                float stickAngle = MathF.Atan2(left_right, up_down);
                if (left_right == -1) stickAngle = -90 * Deg2Rad;
                else if (left_right == 1) stickAngle = 90 * Deg2Rad;
                stickAngle += angles.Y;

                Vector2 newValue = new Vector2(MathF.Sin(stickAngle), MathF.Cos(stickAngle));
                float hyp = MathF.Sqrt(up_down * up_down + left_right * left_right);
                newValue.X *= hyp;
                newValue.Y *= hyp;

                //PluginLog.Log($"{angles.Y * Rad2Deg} {newValue.Y} | {newValue.X} | {stickAngle * Rad2Deg}");
                if (newValue.Y > 0)
                {
                    (*(float*)(controllerAddress + (UInt64)(offsets->left_stick_up * 4))) = MathF.Abs(newValue.Y);
                    (*(float*)(controllerAddress + (UInt64)(offsets->left_stick_down * 4))) = 0;
                }
                else
                {
                    (*(float*)(controllerAddress + (UInt64)(offsets->left_stick_up * 4))) = 0;
                    (*(float*)(controllerAddress + (UInt64)(offsets->left_stick_down * 4))) = MathF.Abs(newValue.Y);
                }

                if (newValue.X > 0)
                {
                    (*(float*)(controllerAddress + (UInt64)(offsets->left_stick_left * 4))) = 0;
                    (*(float*)(controllerAddress + (UInt64)(offsets->left_stick_right * 4))) = MathF.Abs(newValue.X);
                }
                else
                {
                    (*(float*)(controllerAddress + (UInt64)(offsets->left_stick_left * 4))) = MathF.Abs(newValue.X);
                    (*(float*)(controllerAddress + (UInt64)(offsets->left_stick_right * 4))) = 0;
                }
            }
            if (hooksSet && enableVR && xivr.cfg!.data.motioncontrol)
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

                if (isHousing)
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
                            toggleDalamudMode();
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

                if (isCharMake)
                {
                    *(float*)(controllerAddress + (UInt64)(offsets->left_trigger * 4)) = 0;
                    *(float*)(controllerAddress + (UInt64)(offsets->right_trigger * 4)) = 0;
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

        public static Vector3 GetAngles(Quaternion q)
        {
            float pitch, yaw, roll = 0.0f;
            float sqw = q.W * q.W;
            float sqx = q.X * q.X;
            float sqy = q.Y * q.Y;
            float sqz = q.Z * q.Z;
            float unit = sqx + sqy + sqz + sqw; // if normalised is one, otherwise is correction factor
            float test = q.X * q.W - q.Y * q.Z;

            if (test > 0.49975f * unit)
            {   // singularity at north pole
                yaw = 2f * MathF.Atan2(q.Y, q.X);
                pitch = MathF.PI / 2f;
                roll = 0;
                return new Vector3(pitch, yaw, roll);
            }
            if (test < -0.49975f * unit)
            {   // singularity at south pole
                yaw = -2f * MathF.Atan2(q.Y, q.X);
                pitch = -MathF.PI / 2f;
                roll = 0;
                return new Vector3(pitch, yaw, roll);
            }

            Quaternion q1 = new Quaternion(q.W, q.Z, q.X, q.Y);
            yaw = 1 * MathF.Atan2(2f * (q1.X * q1.W + q1.Y * q1.Z), 1f - 2f * (q1.Z * q1.Z + q1.W * q1.W));   // Yaw
            pitch = 1 * MathF.Asin(2f * (q1.X * q1.Z - q1.W * q1.Y));                                         // Pitch
            roll = 1 * MathF.Atan2(2f * (q1.X * q1.Y + q1.Z * q1.W), 1f - 2f * (q1.Y * q1.Y + q1.Z * q1.Z));  // Roll
            return new Vector3(pitch, yaw, roll);
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
        bool showKeyboard = false;
        [HandleInputAttribute(ActionButtonLayout.xbox_button_y)]
        public void inputXBoxButtonY(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            float value = (digital.bState == true) ? 1.0f : 0.0f;
            if (rightStickAltMode)
                inputButton11(analog, digital);
            else
                xboxStatus.button_y.Set(digital.bState, value);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_button_x)]
        public void inputXBoxButtonX(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            float value = (digital.bState == true) ? 1.0f : 0.0f;
            if (dalamudMode)
                inputXBoxStart(analog, digital);
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
            if (rightStickAltMode)
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


        [HandleInputAttribute(ActionButtonLayout.thumbrest_left)]
        public void inputThumbrestLeft(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bChanged)
                rightStickAltMode = digital.bState;
        }

        [HandleInputAttribute(ActionButtonLayout.thumbrest_right)]
        public void inputThumbrestRight(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bChanged)
            {
                leftStickAltMode = digital.bState;
                dalamudMode = digital.bState;
            }
        }

        //----
        // Watch Buttons
        //----

        [HandleInputAttribute(ActionButtonLayout.watch_audio)]
        public void inputWatchAudio(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bActive)
            {
                xivr.CommandManager!.ProcessCommand("/bgm");
                xivr.CommandManager!.ProcessCommand("/soundeffects");
                xivr.CommandManager!.ProcessCommand("/voice");
                xivr.CommandManager!.ProcessCommand("/ambientsounds");
                xivr.CommandManager!.ProcessCommand("/performsounds");
            }
        }

        [HandleInputAttribute(ActionButtonLayout.watch_dalamud)]
        public void inputWatchDalamud(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bActive)
                xivr.CommandManager!.ProcessCommand("/xlplugins");
        }

        [HandleInputAttribute(ActionButtonLayout.watch_ui)]
        public void inputWatchUI(InputAnalogActionData analog, InputDigitalActionData digital)
        {
        }

        [HandleInputAttribute(ActionButtonLayout.watch_keyboard)]
        public void inputWatchKeyboard(InputAnalogActionData analog, InputDigitalActionData digital)
        {
        }

        [HandleInputAttribute(ActionButtonLayout.watch_none)]
        public void inputWatchNone(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bActive)
                xivr.CommandManager!.ProcessCommand("/chillframes toggle");
        }

        [HandleInputAttribute(ActionButtonLayout.watch_occlusion)]
        public void inputWatchOcclusion(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bActive)
                xivr.CommandManager!.ProcessCommand("/xivr uidepth");
        }

        [HandleInputAttribute(ActionButtonLayout.watch_recenter)]
        public void inputWatchRecenter(InputAnalogActionData analog, InputDigitalActionData digital)
        {
        }

        [HandleInputAttribute(ActionButtonLayout.watch_weapon)]
        public void inputWatchWeapon(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bActive)
                xivr.CommandManager!.ProcessCommand("/xivr weapon");
        }

        [HandleInputAttribute(ActionButtonLayout.watch_xivr)]
        public void inputWatchXIVR(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bActive)
                xivr.CommandManager!.ProcessCommand("/xivr");
        }


        Dictionary<UInt64, stCommonSkelBoneList> commonBones = new Dictionary<UInt64, stCommonSkelBoneList>();

        /*
         
        Transform float* hkaPose.?calculateBoneModelSpace@hkaPose@@AEBAAEBVhkQsTransformf@@H@Z(Pose, Count)
        141796b00
        hkQsTransformf.?setAxisAngle@hkQuaternionf@@QEAAXAEBVhkVector4f@@AEBVhkSimdFloat32@@@Z(local_118,local_e8,local_f8);
        141796780
        hkVector4f.?setRotatedDir@hkVector4f@@QEAAXAEBVhkQuaternionf@@AEBV1@@Z(local_c8,&local_88,local_108);
        1417532b0
        hkaPose.?setToReferencePose@hkaPose@@QEAAXXZ(&local_168);
        141752700
        hkaPose.?syncModelSpace@hkaPose@@QEBAXXZ(&local_168);
        1417986d0
        hkQsTransformf.?get4x4ColumnMajor@hkQsTransformf@@QEBAXPEIAM@Z(undefined4 *param_1,undefined8 param_2)
        */

        //----
        // GetBoneIndexFromName
        //----
        public delegate short GetBoneIndexFromNameDg(Skeleton* skeleton, String name);
        [Signature(Signatures.GetBoneIndexFromName, Fallibility = Fallibility.Fallible)]
        public static GetBoneIndexFromNameDg? GetBoneIndexFromNameFn = null;

        //----
        // twoBoneIK
        //----
        private delegate void twoBoneIKDg(byte* a1, hkIKSetup a2, hkaPose* pose);
        [Signature(Signatures.twoBoneIK, Fallibility = Fallibility.Fallible)]
        private twoBoneIKDg? twoBoneIKFn = null;

        //----
        // threadedLookAtParent
        //----
        private delegate void threadedLookAtParentDg(UInt64* a1, UInt64 a2, uint a3);
        [Signature(Signatures.threadedLookAtParent, DetourName = nameof(threadedLookAtParentFn))]
        private Hook<threadedLookAtParentDg>? threadedLookAtParentHook = null;

        //[HandleStatus("threadedLookAtParent")]
        public void threadedLookAtParentStatus(bool status)
        {
            if (status == true)
                threadedLookAtParentHook?.Enable();
            else
                threadedLookAtParentHook?.Disable();
        }

        private unsafe void threadedLookAtParentFn(UInt64* a1, UInt64 a2, uint a3)
        {
            //PluginLog.Log("threadedLookAtParentFn");

            threadedLookAtParentHook?.Original(a1, a2, a3);
        }

        //----
        // lookAtIK
        //----
        private delegate byte* lookAtIKDg(byte* a1, float* a2, Vector4* targetPosition, float a4, Vector4* offsetHeadPosition, float* a6);
        [Signature(Signatures.lookAtIK, DetourName = nameof(lookAtIKFn))]
        private Hook<lookAtIKDg>? lookAtIKHook = null;

        //[HandleStatus("lookAtIK")]
        public void lookAtIKStatus(bool status)
        {
            if (status == true)
                lookAtIKHook?.Enable();
            else
                lookAtIKHook?.Disable();
        }
        private unsafe byte* lookAtIKFn(byte* a1, float* a2, Vector4* targetPosition, float a4, Vector4* offsetHeadPosition, float* a6)
        {
            //Vector3 angles = GetAngles(hmdMatrix);
            //Matrix4x4 headMatrix = hmdMatrix * Matrix4x4.CreateScale(1, -1, 1);
            //headMatrix
            //headMatrix.M41 = hmdMatrix.M41;
            //headMatrix.M42 += 1.4f; //hmdMatrix.M42 + 1.4f;
            //headMatrix.M43 = hmdMatrix.M43;

            //PluginLog.Log($"float1 {a2[0]} {a2[1]} {a2[2]} {a2[3]}");
            //PluginLog.Log($"float1 {a2[4]} {a2[5]} {a2[6]} {a2[7]}");
            //PluginLog.Log($"float1 {a2[8]} {a2[9]} {a2[10]} {a2[11]}");
            //PluginLog.Log($"float1 {a2[12]} {a2[13]} {a2[14]} {a2[15]}");

            //PluginLog.Log($"float2 {a3[0]} {a3[1]} {a3[2]} {a3[3]}");
            //PluginLog.Log($"float2 {a3[4]} {a3[5]} {a3[6]} {a3[7]}");
            //PluginLog.Log($"float2 {a3[8]} {a3[9]} {a3[10]} {a3[11]}");
            //PluginLog.Log($"float2 {a3[12]} {a3[13]} {a3[14]} {a3[15]}");
            //PluginLog.Log($"item {z.Translation.X},{z.Translation.Y},{z.Translation.Z},{z.Translation.W} | {z.Rotation.X}, {z.Rotation.Y}, {z.Rotation.Z}, {z.Rotation.W}");
            //extraChange += 0.001f;
            //Quaternion q = Quaternion.CreateFromYawPitchRoll(0, 0, xivr.cfg!.data.offsetAmountYFPS * Deg2Rad);
            //a2[0-3] rotation quat
            //a2[0] = q.X;
            //a2[1] = q.Y;
            //a2[2] = q.Z;
            //a2[3] = q.W;

            //Matrix4x4 m = Matrix4x4.Identity;
            //a3[0] = m.M11; a3[1] = m.M11; a3[2] = m.M11; a3[3] = m.M11;
            //a3[4] = m.M11; a3[5] = m.M11; a3[6] = m.M11; a3[7] = m.M11;
            //a3[8] = m.M11; a3[9] = m.M11; a3[10] = m.M11; a3[11] = m.M11;
            //a3[12] = m.M11; a3[13] = m.M11; a3[14] = m.M11; a3[15] = m.M11;
            //Vector4 forward = new Vector4(0, 0, 1, 0);
            //forward.Y -= 1.4f;
            //Matrix4x4 rot = Matrix4x4.CreateFromYawPitchRoll(xivr.cfg!.data.offsetAmountZFPS * Deg2Rad, 0, 0);

            //*targetPosition = Vector4.Transform(forward, headMatrix);

            //a3[0] = forward.X;
            //a3[1] = forward.Y;
            //a3[2] = forward.Z;
            //a3[3] = forward.W;

            //*headPosition = forward;
            //*forcedHeadRotation = q;


            //a4 = xivr.cfg!.data.offsetAmountZFPS / 100f;

            //a3[0] = 4; a3[1] = 4; a3[2] = 4; a3[3] = 0;
            //a3[4] = 0; a3[5] = 0; a3[6] = 0; a3[7] = 0;
            //a3[8] = 0; a3[9] = 0; a3[10] = 0; a3[11] = 0;
            //a3[12] = q.X; a3[13] = q.Y; a3[14] = q.Z; a3[15] = q.W;

            return lookAtIKHook!.Original(a1, a2, targetPosition, a4, offsetHeadPosition, a6);

            //return (byte*)nint.Zero;
        }

        //----
        // RenderSkeletonList
        //----
        private delegate void RenderSkeletonListDg(UInt64 RenderSkeletonLinkedList, float frameTiming);
        [Signature(Signatures.RenderSkeletonList, DetourName = nameof(RenderSkeletonListFn))]
        private Hook<RenderSkeletonListDg>? RenderSkeletonListHook = null;

        [HandleStatus("RenderSkeletonList")]
        public void RenderSkeletonListStatus(bool status)
        {
            if (status == true)
                RenderSkeletonListHook?.Enable();
            else
                RenderSkeletonListHook?.Disable();
        }
        private unsafe void RenderSkeletonListFn(UInt64 RenderSkeletonLinkedList, float frameTiming)
        {
            //PluginLog.Log($"RenderSkeletonListFn {(UInt64)RenderSkeletonLinkedList:x} {curEye}");
            RenderSkeletonListHook!.Original(RenderSkeletonLinkedList, frameTiming);

            Character* bonedCharacter = GetCharacterOrMouseover();
            if (bonedCharacter == null)
                return;

            Structures.Model* model = (Structures.Model*)bonedCharacter->GameObject.DrawObject;
            if (model == null)
                return;

            Skeleton* skeleton = model->skeleton;
            if (skeleton == null)
                return;

            //UpdateBoneCamera();
            UpdateBoneScales();


            while (multiIK[curEye].Count > 0)
            {
                //PluginLog.Log($"{(UInt64)skeleton:x} {(UInt64)itmSkeleton:x} {multiIK.Count}");
                stMultiIK ikElement = multiIK[curEye].Dequeue();
                RunIKElement(&ikElement);
            }
        }

        //----
        // RenderSkeletonListSkeleton
        //----
        private delegate void RenderSkeletonListSkeletonDg(Skeleton* skeleton, float frameTiming);
        [Signature(Signatures.RenderSkeletonListSkeleton, DetourName = nameof(RenderSkeletonListSkeletonFn))]
        private Hook<RenderSkeletonListSkeletonDg>? RenderSkeletonListSkeletonHook = null;

        //[HandleStatus("RenderSkeletonListSkeleton")]
        public void RenderSkeletonListSkeletonStatus(bool status)
        {
            if (status == true)
                RenderSkeletonListSkeletonHook?.Enable();
            else
                RenderSkeletonListSkeletonHook?.Disable();
        }
        private unsafe void RenderSkeletonListSkeletonFn(Skeleton* skeleton, float frameTiming)
        {
            RenderSkeletonListSkeletonHook!.Original(skeleton, frameTiming);
        }

        private float prevFrameTiming = 0;

        //----
        // RenderSkeletonListAnimation
        //----
        private delegate void RenderSkeletonListAnimationDg(UInt64 RenderSkeletonLinkedList, float frameTiming, UInt64 c);
        [Signature(Signatures.RenderSkeletonListAnimation, DetourName = nameof(RenderSkeletonListAnimationFn))]
        private Hook<RenderSkeletonListAnimationDg>? RenderSkeletonListAnimationHook = null;

        [HandleStatus("RenderSkeletonListAnimation")]
        public void RenderSkeletonListAnimationStatus(bool status)
        {
            if (status == true)
                RenderSkeletonListAnimationHook?.Enable();
            else
                RenderSkeletonListAnimationHook?.Disable();
        }
        private unsafe void RenderSkeletonListAnimationFn(UInt64 RenderSkeletonLinkedList, float frameTiming, UInt64 c)
        {
            //_expectedFrameTime = (long)((1 / (Service.Settings.TargetFPS)) * TimeSpan.TicksPerSecond);
            frameTiming *= 0.615f;// (xivr.cfg!.data.offsetAmountZFPSMount / 100.0f);
            if (curEye == 0)
                prevFrameTiming = frameTiming;
            else
                frameTiming = prevFrameTiming;

            //PluginLog.Log($"RenderSkeletonListAnimationFn {(UInt64)RenderSkeletonLinkedList:x}");
            RenderSkeletonListAnimationHook!.Original(RenderSkeletonLinkedList, frameTiming, c);
        }

        //----
        // RenderSkeletonListSkeleton
        //----
        private delegate void RenderSkeletonListPartialSkeletonDg(PartialSkeleton* skeleton, float frameTiming);
        [Signature(Signatures.RenderSkeletonListPartialSkeleton, DetourName = nameof(RenderSkeletonListPartialSkeletonFn))]
        private Hook<RenderSkeletonListPartialSkeletonDg>? RenderSkeletonListPartialSkeletonHook = null;

        //[HandleStatus("RenderSkeletonListPartialSkeleton")]
        public void RenderSkeletonListPartialSkeletonStatus(bool status)
        {
            if (status == true)
                RenderSkeletonListPartialSkeletonHook?.Enable();
            else
                RenderSkeletonListPartialSkeletonHook?.Disable();
        }
        private unsafe void RenderSkeletonListPartialSkeletonFn(PartialSkeleton* partialSkeleton, float frameTiming)
        {
            //PluginLog.Log($"RenderSkeletonListPartialSkeletonFn {(UInt64)partialSkeleton:x}");
            RenderSkeletonListPartialSkeletonHook!.Original(partialSkeleton, frameTiming);
        }

        private unsafe void RunIKElement(stMultiIK* ikElement)
        {
            //PluginLog.Log($"remove {curEye} {multiIKEye.Count} {(UInt64)curIK.objAddress:x}");
            if (ikElement->objCharacter == null)
                return;

            Structures.Model* model = (Structures.Model*)ikElement->objCharacter->GameObject.DrawObject;
            if (model == null)
                return;

            Skeleton* skeleton = model->skeleton;
            if (skeleton == null)
                return;

            SkeletonResourceHandle* srh = skeleton->SkeletonResourceHandles[0];
            if (srh == null)
                return;

            hkaSkeleton* hkaSkel = srh->HavokSkeleton;
            if (hkaSkel == null)
                return;

            if (!commonBones.ContainsKey((UInt64)hkaSkel))
                return;

            stCommonSkelBoneList csb = commonBones[(UInt64)hkaSkel];

            Matrix4x4 matrixHead = (Matrix4x4)ikElement->hmdMatrix;
            Matrix4x4 matrixLHC = (Matrix4x4)ikElement->lhcMatrix;
            Matrix4x4 matrixRHC = (Matrix4x4)ikElement->rhcMatrix;

            Matrix4x4 objSkeletonPosition = skeleton->Transform.ToMatrix();
            //Matrix4x4.Invert(objSkeletonPosition, out Matrix4x4 objSkeletonPositionI);

            Matrix4x4 objMountSkeletonPosition = Matrix4x4.Identity;
            Structures.Model* modelMount = (Structures.Model*)model->mountedObject;
            if (modelMount != null)
                objMountSkeletonPosition = modelMount->basePosition.ToMatrix();
            Matrix4x4.Invert(objMountSkeletonPosition, out Matrix4x4 objMountSkeletonPositionI);

            byte lockItem = 0;

            float armLength = csb.armLength * skeleton->Transform.Scale.Y;
            Matrix4x4 hmdLocalScale = Matrix4x4.CreateScale((armLength / 0.5f) * (ikElement->armMultiplier / 100.0f));
            Matrix4x4 hmdRotate = Matrix4x4.CreateFromYawPitchRoll(90 * Deg2Rad, 180 * Deg2Rad, 0 * Deg2Rad);
            Matrix4x4 hmdFlipScale = Matrix4x4.CreateScale(-1, 1, -1);

            hkIKSetup ikSetupHead = new hkIKSetup();
            bool runIKHead = false;
            if (csb.e_spine_b >= 0 && csb.e_spine_c >= 0 && csb.e_neck >= 0 && ikElement->doHandIK)
            {
                runIKHead = true;
                Matrix4x4 head = Matrix4x4.CreateFromYawPitchRoll(-90 * Deg2Rad, 0 * Deg2Rad, 90 * Deg2Rad) * matrixHead * hmdFlipScale;
                ikSetupHead.m_firstJointIdx = csb.e_spine_b;
                ikSetupHead.m_secondJointIdx = csb.e_spine_c;
                ikSetupHead.m_endBoneIdx = csb.e_neck;
                ikSetupHead.m_hingeAxisLS = new Vector4(0.0f, 0.0f, -1.0f, 0.0f);
                ikSetupHead.m_endTargetMS = new Vector4((head * hmdLocalScale).Translation, 0.0f);
                ikSetupHead.m_endTargetRotationMS = Quaternion.CreateFromRotationMatrix(head);
                ikSetupHead.m_enforceEndPosition = true;
                ikSetupHead.m_enforceEndRotation = true;
            }

            hkIKSetup ikSetupL = new hkIKSetup();
            bool runIKL = false;
            if (csb.e_arm_l >= 0 && csb.e_forearm_l >= 0 && csb.e_hand_l >= 0 && ikElement->doHandIK)
            {
                runIKL = true;
                Matrix4x4 palmL = hmdRotate * matrixLHC * hmdFlipScale;
                ikSetupL.m_firstJointIdx = csb.e_arm_l;
                ikSetupL.m_secondJointIdx = csb.e_forearm_l;
                ikSetupL.m_endBoneIdx = csb.e_hand_l;
                ikSetupL.m_hingeAxisLS = new Vector4(0.0f, 0.0f, 1.0f, 0.0f);
                ikSetupL.m_endTargetMS = new Vector4((palmL * hmdLocalScale).Translation, 0.0f);
                ikSetupL.m_endTargetRotationMS = Quaternion.CreateFromRotationMatrix(palmL);
                ikSetupL.m_enforceEndPosition = true;
                ikSetupL.m_enforceEndRotation = true;
            }

            hkIKSetup ikSetupR = new hkIKSetup();
            bool runIKR = false;
            if (csb.e_arm_r >= 0 && csb.e_forearm_r >= 0 && csb.e_hand_r >= 0 && ikElement->doHandIK)
            {
                runIKR = true;
                Matrix4x4 palmR = hmdRotate * matrixRHC * hmdFlipScale;
                ikSetupR.m_firstJointIdx = csb.e_arm_r;
                ikSetupR.m_secondJointIdx = csb.e_forearm_r;
                ikSetupR.m_endBoneIdx = csb.e_hand_r;
                ikSetupR.m_hingeAxisLS = new Vector4(0.0f, 0.0f, 1.0f, 0.0f);
                ikSetupR.m_endTargetMS = new Vector4((palmR * hmdLocalScale).Translation, 0.0f);
                ikSetupR.m_endTargetRotationMS = Quaternion.CreateFromRotationMatrix(palmR);
                ikSetupR.m_enforceEndPosition = true;
                ikSetupR.m_enforceEndRotation = true;
            }

            hkQsTransformf transform;
            for (ushort p = 0; p < skeleton->PartialSkeletonCount; p++)
            {
                hkaPose* objPose = skeleton->PartialSkeletons[p].GetHavokPose(0);
                if (objPose == null)
                    continue;


                if (p == 0 && csb.e_neck >= 0)
                {
                    float diffHeadNeck = MathF.Abs(objPose->ModelPose[csb.e_neck].Translation.Y - objPose->ModelPose[csb.e_head].Translation.Y);
                    //PluginLog.Log($"Neck Y {objPose->ModelPose[csb.e_neck].Translation.Y}");
                    neckOffsetAvg.AddNew(objPose->ModelPose[csb.e_neck].Translation.Y + diffHeadNeck);
                    //PluginLog.Log($"Neck {csb.e_neck} | {objPose->ModelPose[csb.e_neck].Translation.Y} Head {csb.e_head} | {objPose->ModelPose[csb.e_head].Translation.Y} = {diffHeadNeck}");

                    if (runIKHead)
                    {
                        //PluginLog.Log($"ik Y {ikElement->avgHeadHeight}");
                        ikSetupHead.m_endTargetMS.Y += neckOffsetAvg.Average + 0.25f;
                        twoBoneIKFn!(&lockItem, ikSetupHead, objPose);
                    }

                    if (runIKL)
                    {
                        ikSetupL.m_endTargetMS.Y += neckOffsetAvg.Average;

                        Vector3 angles = GetAngles(matrixLHC);
                        transform = objPose->LocalPose[csb.e_wrist_l];
                        transform.Rotation = Quaternion.CreateFromYawPitchRoll(0, (angles.Z / 2.0f), 0).Convert();
                        objPose->LocalPose[csb.e_wrist_l] = transform;

                        twoBoneIKFn!(&lockItem, ikSetupL, objPose);
                    }

                    if (runIKR)
                    {
                        ikSetupR.m_endTargetMS.Y += neckOffsetAvg.Average;

                        Vector3 angles = GetAngles(matrixRHC);
                        transform = objPose->LocalPose[csb.e_wrist_r];
                        transform.Rotation = Quaternion.CreateFromYawPitchRoll(0, (angles.Z / 2.0f), 0).Convert();
                        objPose->LocalPose[csb.e_wrist_r] = transform;

                        twoBoneIKFn!(&lockItem, ikSetupR, objPose);
                    }
                }
            }
        }

        private void UpdateBoneCamera()
        {
            if (inCutscene.Current || gameMode.Current == CameraModes.ThirdPerson)
                return;

            Character* bonedCharacter = GetCharacterOrMouseover();
            if (bonedCharacter == null)
                return;

            Structures.Model* model = (Structures.Model*)bonedCharacter->GameObject.DrawObject;
            if (model == null)
                return;

            Skeleton* skeleton = model->skeleton;
            if (skeleton == null)
                return;

            SkeletonResourceHandle* srh = skeleton->SkeletonResourceHandles[0];
            if (srh == null)
                return;

            hkaSkeleton* hkaSkel = srh->HavokSkeleton;
            if (hkaSkel == null)
                return;

            if (!commonBones.ContainsKey((UInt64)hkaSkel))
                return;

            plrSkeletonPosition = model->basePosition.ToMatrix();
            Matrix4x4.Invert(plrSkeletonPosition, out plrSkeletonPositionI);

            mntSkeletonPosition = Matrix4x4.Identity;
            Structures.Model* modelMount = (Structures.Model*)model->mountedObject;
            if (modelMount != null)
                mntSkeletonPosition = modelMount->basePosition.ToMatrix();
            Matrix4x4.Invert(mntSkeletonPosition, out mntSkeletonPositionI);

            stCommonSkelBoneList csb = commonBones[(UInt64)hkaSkel];
            if (skeleton->PartialSkeletonCount > 1)
            {
                hkaPose* objPose = skeleton->PartialSkeletons[0].GetHavokPose(0);
                if (objPose != null)
                {
                    float diffHeadNeck = MathF.Abs(objPose->ModelPose[csb.e_neck].Translation.Y - objPose->ModelPose[csb.e_head].Translation.Y);
                    headBoneMatrix = objPose->ModelPose[csb.e_neck].ToMatrix() * plrSkeletonPosition;
                    headBoneMatrix.M42 += diffHeadNeck;
                }
            }
        }

        private void UpdateBoneScales()
        {
            if (inCutscene.Current || gameMode.Current == CameraModes.ThirdPerson)
                return;

            Character* bonedCharacter = GetCharacterOrMouseover();
            if (bonedCharacter == null)
                return;

            Structures.Model* model = (Structures.Model*)bonedCharacter->GameObject.DrawObject;
            if (model == null)
                return;

            Skeleton* skeleton = model->skeleton;
            if (skeleton == null)
                return;

            SkeletonResourceHandle* srh = skeleton->SkeletonResourceHandles[0];
            if (srh == null)
                return;

            hkaSkeleton* hkaSkel = srh->HavokSkeleton;
            if (hkaSkel == null)
                return;

            if (!commonBones.ContainsKey((UInt64)hkaSkel))
                return;

            stCommonSkelBoneList csb = commonBones[(UInt64)hkaSkel];

            Transform transformS = skeleton->Transform;
            transformS.Rotation = Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), bonedCharacter->GameObject.Rotation);
            skeleton->Transform = transformS;

            float armLength = csb.armLength * skeleton->Transform.Scale.Y;
            hmdWorldScale = Matrix4x4.Identity;
            if (gameMode.Current == CameraModes.FirstPerson)
                hmdWorldScale = Matrix4x4.CreateScale((armLength / 0.5f) * (xivr.cfg!.data.armMultiplier / 100.0f));
            hkQsTransformf transform;

            //----
            // Gets the rotation of the mount bone of the current mount
            //----
            Vector3 anglesMount = new Vector3(0, 0, 0);
            Structures.Model* modelMount = (Structures.Model*)model->mountedObject;
            if (modelMount != null && xivr.cfg!.data.motioncontrol)
            {
                Skeleton* skeletonMount = modelMount->skeleton;
                if (skeletonMount != null)
                {
                    //----
                    // Keeps the mount the same rotation as the character so the hands are always correct
                    //----
                    transformS = skeletonMount->Transform;
                    transformS.Rotation = Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), bonedCharacter->GameObject.Rotation);
                    skeletonMount->Transform = transformS;

                    short mntMountId = GetBoneIndexFromNameFn!(skeletonMount, "n_mount");
                    short mntMountIdA = GetBoneIndexFromNameFn!(skeletonMount, "n_mount_a");
                    short mntMountIdB = GetBoneIndexFromNameFn!(skeletonMount, "n_mount_b");
                    short mntMountIdC = GetBoneIndexFromNameFn!(skeletonMount, "n_mount_c");
                    short mntAbdomenId = GetBoneIndexFromNameFn!(skeletonMount, "n_hara");

                    if (mntAbdomenId > 0 && skeletonMount->PartialSkeletonCount == 1)
                    {
                        hkaPose* objPose = skeletonMount->PartialSkeletons[0].GetHavokPose(0);
                        if (objPose != null)
                        {
                            //anglesMount = GetAngles(objPose->ModelPose[mntAbdomenId].Rotation.Convert());

                            //----
                            // Keeps the mount bones the same as the character during animations
                            // so the hands are always correct
                            //----
                            transform = objPose->LocalPose[mntAbdomenId];
                            transform.Rotation = Quaternion.Identity.Convert();
                            objPose->LocalPose[mntAbdomenId] = transform;
                        }
                    }
                }
            }

            //----
            // Add the scabard and sheathes to the ToShrink list
            // as well as the weapon if its not shown
            //----
            List<KeyValuePair<short, Vector3>> scaleList = new List<KeyValuePair<short, Vector3>>();
            scaleList.Add(new KeyValuePair<short, Vector3>(csb.e_scabbard_l, new Vector3(0.0001f, 0.0001f, 0.0001f)));
            scaleList.Add(new KeyValuePair<short, Vector3>(csb.e_scabbard_r, new Vector3(0.0001f, 0.0001f, 0.0001f)));
            scaleList.Add(new KeyValuePair<short, Vector3>(csb.e_sheathe_l, new Vector3(0.0001f, 0.0001f, 0.0001f)));
            scaleList.Add(new KeyValuePair<short, Vector3>(csb.e_sheathe_r, new Vector3(0.0001f, 0.0001f, 0.0001f)));

            if (!xivr.cfg!.data.showWeaponInHand)
            {
                scaleList.Add(new KeyValuePair<short, Vector3>(csb.e_weapon_l, new Vector3(0.0001f, 0.0001f, 0.0001f)));
                scaleList.Add(new KeyValuePair<short, Vector3>(csb.e_weapon_r, new Vector3(0.0001f, 0.0001f, 0.0001f)));
            }

            for (ushort p = 0; p < skeleton->PartialSkeletonCount; p++)
            {
                hkaPose* objPose = skeleton->PartialSkeletons[p].GetHavokPose(0);
                if (objPose == null)
                    continue;

                if (p == 0)
                {
                    //----
                    // Set the spine to the reverse of the abdomen to keep the upper torso stable
                    // while immersive mode is off
                    // Set the reference pose for anything above the waste
                    //----
                    if (csb.e_spine_a >= 0 && !xivr.cfg!.data.immersiveMovement && !isMounted && xivr.cfg!.data.motioncontrol)
                    {
                        Vector3 angles = GetAngles(objPose->ModelPose[csb.e_abdomen].Rotation.Convert());
                        //angles = anglesMount;
                        transform = objPose->LocalPose[csb.e_spine_a];
                        transform.Rotation = Quaternion.CreateFromYawPitchRoll(-angles.Y + (90 * Deg2Rad), 0 * Deg2Rad, 90 * Deg2Rad).Convert();
                        objPose->LocalPose[csb.e_spine_a] = transform;

                        HashSet<short> children = csb.layout[csb.e_spine_a].Value;
                        foreach (short child in children)
                            objPose->LocalPose[child] = hkaSkel->ReferencePose[child];
                    }
                    else if (isMounted && xivr.cfg!.data.motioncontrol)
                    {
                        HashSet<short> children = csb.layout[csb.e_spine_c].Value;
                        foreach (short child in children)
                            objPose->LocalPose[child] = hkaSkel->ReferencePose[child];
                    }

                    //----
                    // Shrink the scabbards, sheathes and weapons if hidden
                    //----
                    foreach (KeyValuePair<short, Vector3> item in scaleList)
                    {
                        if (item.Key >= 0)
                        {
                            transform = objPose->LocalPose[item.Key];
                            transform.Scale = item.Value.Convert();
                            objPose->LocalPose[item.Key] = transform;
                        }
                    }

                    if (csb.e_neck >= 0)
                    {
                        //----
                        // Shrink the head and all child bones
                        //----
                        foreach (short id in csb.layout[csb.e_neck].Value)
                        {
                            transform = objPose->LocalPose[id];
                            transform.Translation = (transform.Translation.Convert() * -1).Convert();
                            transform.Scale = new Vector3(0.0001f, 0.0001f, 0.0001f).Convert();
                            objPose->LocalPose[id] = transform;
                        }

                        //----
                        // Rotate the neck to hide the head
                        //----
                        transform = objPose->LocalPose[csb.e_neck];
                        //transform.Rotation = Quaternion.CreateFromYawPitchRoll(0 * Deg2Rad, 0 * Deg2Rad, 180 * Deg2Rad).Convert();
                        transform.Scale = new Vector3(0.0001f, 0.0001f, 0.0001f).Convert();
                        objPose->LocalPose[csb.e_neck] = transform;
                    }
                }
                else
                {
                    //----
                    // shrink any other poses
                    //----
                    for (int i = 0; i < objPose->LocalPose.Length; i++)
                    {
                        transform = objPose->LocalPose[i];
                        //transform.Translation = (transform.Translation.Convert() * -1).Convert();
                        transform.Scale = new Vector3(0.0001f, 0.0001f, 0.0001f).Convert();
                        objPose->LocalPose[i] = transform;
                    }
                }
            }
        }

        private void UpdateBoneLayout()
        {
            Matrix4x4 hmdFlip = Matrix4x4.CreateFromAxisAngle(new Vector3(0, 1, 0), 90 * Deg2Rad) * Matrix4x4.CreateFromAxisAngle(new Vector3(0, 0, 1), -90 * Deg2Rad);
            Matrix4x4 hmdMatrixBody = hmdFlip * hmdMatrixI;
            Matrix4x4 lhcMatrixCXZ = convertXZ * lhcMatrix * convertXZ;
            Matrix4x4 rhcMatrixCXZ = convertXZ * rhcMatrix * convertXZ;

            PlayerCharacter? player = xivr.ClientState!.LocalPlayer;
            //if (player != null && curEye == 0 && gameMode.Current == CameraModes.FirstPerson)
            //if (player != null && gameMode.Current == CameraModes.FirstPerson && !inCutscene.Current)

            Character* bonedCharacter = GetCharacterOrMouseover();
            if (bonedCharacter == null)
                return;

            //----
            // Gets the skeletal system
            //----
            Structures.Model* model = (Structures.Model*)bonedCharacter->GameObject.DrawObject;
            if (model == null)
                return;

            Skeleton* skeleton = model->skeleton;
            if (skeleton == null)
                return;

            //----
            // Sets the main skeletal bone names used
            //----
            SkeletonResourceHandle* srh = skeleton->SkeletonResourceHandles[0];
            if (srh == null)
                return;

            hkaSkeleton* hkaSkel = srh->HavokSkeleton;
            if (hkaSkel == null)
                return;

            //if (!commonBones.ContainsKey((UInt64)hkaSkel))
            //    return;

            //stCommonSkelBoneList csb = commonBones[(UInt64)hkaSkel];

            //skeleton->Transform.Rotation = Quaternion.Identity;
            //skeleton->Transform.Position = Vector3.Zero;
            plrSkeletonPosition = skeleton->Transform.ToMatrix();
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

                /*if(p == 0)
                { 
                    if (cb.e_head >= 0)
                    {
                        foreach (short id in cb.layout[cb.e_neck].Value)
                        {
                            hkQsTransformf identTransI = new hkQsTransformf();
                            identTransI.Translation = objPose->LocalPose[id].Translation;
                            identTransI.Rotation = Quaternion.Identity.Convert();
                            identTransI.Scale = new Vector3(0.0001f, 0.0001f, 0.0001f).Convert();
                            objPose->LocalPose[id] = identTransI;
                        }

                        hkQsTransformf identTrans = new hkQsTransformf();
                        identTrans.Translation = objPose->LocalPose[cb.e_neck].Translation;
                        identTrans.Rotation = Quaternion.CreateFromYawPitchRoll(0, 0, 180 * Deg2Rad).Convert();
                        objPose->LocalPose[cb.e_neck] = identTrans;
                    }
                }
                else
                {
                    for (int i = 0; i < objPose->LocalPose.Length; i++)
                    {
                        hkQsTransformf transformN = objPose->LocalPose[i];
                        transformN.Translation = new Vector3(0, 0, 0).Convert();
                        transformN.Rotation = Quaternion.Identity.Convert();
                        transformN.Scale = new Vector3(0.0001f, 0.0001f, 0.0001f).Convert();
                        objPose->LocalPose[i] = transformN;
                    }
                }
                */
                UInt64 objPose64 = (UInt64)objPose;
                boneLayout.Add(objPose64, new Dictionary<BoneList, short>());

                //----
                // Loops though the pose bones and updates the ones that have tracking
                //----
                Bone[] boneArray = new Bone[objPose->LocalPose.Length];
                for (short i = 0; i < objPose->LocalPose.Length; i++)
                {
                    string boneName = objPose->Skeleton->Bones[i].Name.String!;
                    short parentId = objPose->Skeleton->ParentIndices[i];

                    BoneList boneKey = BoneOutput.boneNameToEnum.GetValueOrDefault<string, BoneList>(boneName, BoneList._unknown_);
                    //PluginLog.Log($"{boneName} | {boneKey}");
                    if (boneKey == BoneList._unknown_)
                    {
                        if (!BoneOutput.reportedBones.ContainsKey(boneName))
                        {
                            PluginLog.Log($"{p} {objPose64:X} {i} : Error finding bone {boneName}");
                            BoneOutput.reportedBones.Add(boneName, true);
                        }
                        boneName = "_unknown_";
                    }
                    else
                        boneLayout[objPose64][boneKey] = i;

                    //PluginLog.Log($"{p} {(UInt64)objPose:X} {i} : {boneName} {boneKey} {parentId}");

                    if (parentId < 0)
                        boneArray[i] = new Bone(boneKey, i, parentId, null, objPose->LocalPose[i], objPose->Skeleton->ReferencePose[i]);
                    else
                        boneArray[i] = new Bone(boneKey, i, parentId, boneArray[parentId], objPose->LocalPose[i], objPose->Skeleton->ReferencePose[i]);


                    //PluginLog.Log($"Bone {i}/{objPose->LocalPose.Length} Name {(BoneListEn)boneKey} | {boneName}");

                    //boneArray[i].updatePosition = true;
                    //boneArray[i].updateRotation = true;
                    //boneArray[i].updateScale = true;
                    //boneArray[i].SetReference(false);
                    //boneArray[i].setLocal(); 
                    //boneArray[i].CalculateMatrix();
                    //boneArray[i].boneMatrix = plrSkeletonPosition * boneArray[i].boneMatrix;
                    //boneArray[i].SetTransformFromLocalBase(false);
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
                /*
                short rootBone = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_root, -1);
                short headBone = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_head, -1);

                if(rootBone >= 0)
                {
                    //short wristR = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_wrist_r, -1);
                    //short handR = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_hand_r, -1);

                    short neck = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_neck, -1);

                    if(neck >= 0)
                    {
                        //Matrix4x4 thmdMatrix = hmdMatrix * hmdFlip * headBoneMatrix * curViewMatrixWithoutHMD;
                        //Matrix4x4 thmdMatrix = hmdMatrix;// * curViewMatrixWithoutHMD;// * hmdFlip * headBoneMatrix * curViewMatrixWithoutHMD;
                        //thmdMatrix.M42 -= (xivr.cfg!.data.offsetAmountYFPSMount / 100);
                        //thmdMatrix.M43 += (xivr.cfg!.data.offsetAmountZFPSMount / 100);
                        //hmdMatrix = thmdMatrix;

                    }

                    /*if (wristR >= 0 && handR >= 0)
                    {
                        Matrix4x4 rhcLocal = rhcPalmMatrix * Matrix4x4.CreateScale(-1, 1, -1);
                        hkQsTransformf curModel = objPose->ModelPose[handR];
                        curModel.Translation = rhcLocal.Translation.Convert();
                        curModel.Translation.Y += 1.0f;

                        curModel.Rotation = Quaternion.CreateFromRotationMatrix(rhcLocal).Convert();
                        curModel.Scale = rhcLocal.GetScale().Convert();
                        objPose->ModelPose[handR] = curModel;


                    }*//*
                }
                */
                //objPose->ModelInSync = 0;
                //objPose->LocalInSync = 0;
                //syncModelSpaceFn!(objPose);

                //if (waist >= 0) boneArray[waist].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));
                //if (cubb.e_scabbard_l.boneId >= 0) boneArray[cubb.e_scabbard_l.boneId].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));
                //if (cubb.e_scabbard_r.boneId >= 0) boneArray[cubb.e_scabbard_r.boneId].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));
                //if (cubb.e_sheathe_l.boneId >= 0) boneArray[cubb.e_sheathe_l.boneId].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));
                //if (cubb.e_sheathe_r.boneId >= 0) boneArray[cubb.e_sheathe_r.boneId].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));


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

                    outputBonesOnce = true;
                    //boneArray[0].SetReference(true, false);
                    //boneArray[0].Output();
                    //boneArray[0].Output(0, true);
                }
                //rawBoneList[objPose64][0].ScaleAll(rawBoneList[objPose64], 0, 0, 0);




                /*
                hkIKSetup ikSetup1 = new hkIKSetup();
                ikSetup1.m_firstJointIdx = 1;
                ikSetup1.m_secondJointIdx = 11;
                ikSetup1.m_endBoneIdx = 33;
                ikSetup1.m_hingeAxisLS = new Vector4(0f, 0f, 0f, 0f);
                ikSetup1.m_endTargetMS = new Vector4(hmdMatrix.Translation.X, hmdMatrix.Translation.Y, hmdMatrix.Translation.Z, 0.0f);
                //ikSetup1.m_endTargetRotationMS = q;
                //ikSetup1.m_endBoneOffsetLS = new Vector4(q.X, q.Y, q.Z, q.W);
                //ikSetup1.m_endBoneRotationOffsetLS = q;
                ikSetup1.m_enforceEndPosition = true;
                ikSetup1.m_enforceEndRotation = false;

                twoBoneIKFn!(&lockItem, ikSetup1, curUsePose);*/

            }

            outputBonesOnce = true;
        }


        private int tCount = 0;
        bool firstRunBones = false;

        private void UpdateBoneAnimation()
        {
            Matrix4x4 lhcMatrixCXZ = convertXZ * lhcMatrix * convertXZ;
            Matrix4x4 rhcMatrixCXZ = convertXZ * rhcMatrix * convertXZ;
            //Dictionary < UInt64, Dictionary<BoneList, short> >
            foreach (KeyValuePair<UInt64, Dictionary<BoneList, short>> boneData in boneLayout)
            {
                UInt64 objPose64 = boneData.Key;
                Bone[] boneArray = rawBoneList.GetValueOrDefault<ulong, Bone[]>(objPose64, new Bone[0]);

                if (boneArray.Length > 0)
                {
                    short rootBone = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_root, -1);
                    short headBone = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_head, -1);

                    if (rootBone >= 0)
                    {
                        //rootBonePos = boneArray[rootBone].boneFinish;
                        //boneArray[rootBone].SetReference(true, true);
                        short abdomen = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_abdomen, -1);
                        short waist = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_waist, -1);

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


                        //PluginLog.Log($"e_collarbone_l = {collarboneL} {ubb.e_collarbone_l.boneId}");
                        //PluginLog.Log($"e_collarbone_r = {collarboneR} {ubb.e_collarbone_r.boneId}");
                        //PluginLog.Log($"e_spine_a = {spineA} {ubb.e_spine_a.boneId}");
                        //PluginLog.Log($"e_spine_b = {spineB} {ubb.e_spine_b.boneId}");
                        //PluginLog.Log($"e_spine_c = {spineC} {ubb.e_spine_c.boneId}");
                        //PluginLog.Log($"e_scabbard_l = {scabbardL} {ubb.e_scabbard_l.boneId}");
                        //PluginLog.Log($"e_scabbard_r = {scabbardR} {ubb.e_scabbard_r.boneId}");

                        //short vr0l = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.vr0l, -1);
                        //short vr0r = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.vr0r, -1);


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
                        //if (isMounted == false && abdomen >= 0)
                        if (abdomen >= 0 && !isMounted)
                        {
                            //boneArray[rootBone].SetReference(false, true);
                            //boneArray[rootBone].setWorld(true, true);
                            //boneArray[rootBone].setLocal(true, true);
                            /*boneArray[rootBone].SetTransformFromLocalBase(false);
                            Matrix4x4 l = boneArray[rootBone].localMatrix;
                            Matrix4x4 a = boneArray[rootBone].boneMatrix;
                            Matrix4x4 w = plrSkeletonPosition * a;
                            Matrix4x4 t = plrSkeletonPositionI * w;
                            */
                            //PluginLog.Log($"{l.Translation.X} {l.Translation.Y} {l.Translation.Z} | {w.Translation.X} {w.Translation.Y} {w.Translation.Z} | {t.Translation.X} {t.Translation.Y} {t.Translation.Z}");

                            //boneArray[rootBone].SetReference(true, true);
                            if (!xivr.cfg!.data.immersiveMovement && !xivr.cfg!.data.immersiveFull && xivr.cfg!.data.motioncontrol && false)
                            {
                                Vector3 angles = new Vector3(0, 0, 0);
                                //if (xivr.cfg.data.conloc)
                                //    angles = GetAngles(lhcMatrix);
                                //boneArray[abdomen].SetReference(true, false);
                                //boneArray[spineA].SetReference(true, true);
                                boneArray[rootBone].SetReference(true, true);

                                boneArray[spineA].updateRotation = true;
                                boneArray[spineA].transform.Rotation = (boneArray[spineA].transform.Rotation.Convert() * Quaternion.CreateFromAxisAngle(new Vector3(1, 0, 0), angles.Y)).Convert();

                                boneArray[spineA].updatePosition = true;
                                boneArray[spineA].transform.Translation.X = -(hmdMatrix.Translation.X * 0.5f);
                                boneArray[spineA].transform.Translation.Y = (hmdMatrix.Translation.Y * 0.5f);
                                boneArray[spineA].transform.Translation.Z = -(hmdMatrix.Translation.Z * 0.5f);
                                boneArray[spineA].transform.Translation.W = 0;
                                boneArray[spineA].CalculateMatrix(true);
                            }
                        }

                        //if (waist >= 0) boneArray[waist].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));
                        if (scabbardL >= 0) boneArray[scabbardL].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));
                        if (scabbardR >= 0) boneArray[scabbardR].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));
                        if (sheatheL >= 0) boneArray[sheatheL].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));
                        if (sheatheR >= 0) boneArray[sheatheR].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));

                        if (handL >= 0 && xivr.cfg!.data.motioncontrol)
                        {
                            //armLength = boneArray[armL].transform.Translation.Convert().Length() + boneArray[forearmL].transform.Translation.Convert().Length();
                            //armLength *= plrSkeletonPosition.GetScale().Y;
                            //PluginLog.Log($"armLen = {armLength} {plrSkeletonPosition.GetScale()}");

                            //boneArray[armL].SetReference(false, true);
                            //boneArray[armL].transform.Rotation = Quaternion.Identity.Convert();// Quaternion.CreateFromYawPitchRoll(-90 * Deg2Rad, 180 * Deg2Rad, 90 * Deg2Rad).Convert();
                            //boneArray[forearmL].transform.Rotation = Quaternion.Identity.Convert();// Quaternion.CreateFromYawPitchRoll(0, 0, -90 * Deg2Rad).Convert();
                            //boneArray[handL].transform.Rotation = Quaternion.Identity.Convert();
                            //boneArray[wristL].transform.Rotation = Quaternion.Identity.Convert();
                            //boneArray[collarboneL].CalculateMatrix(true);

                            Vector3 p1 = Vector3.Zero;
                            Vector3 p2 = Vector3.Zero;

                            ShowHandBoneLayout(poseType.LeftHand, lhcMatrix);
                            fingerHandLayout hand = Imports.GetSkeletalPose(poseType.LeftHand);
                            Bone[] handArray = new Bone[12];
                            short bI = 0;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_root, bI, 0, null, new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.root.Position;
                            handArray[bI].worldBase.Rotation = hand.root.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            //handArray[bI].boneMatrix = Matrix4x4.CreateFromYawPitchRoll(180 * Deg2Rad, 0 * Deg2Rad, 0 * Deg2Rad) * rhcMatrix * Matrix4x4.CreateScale(-1, 1, -1);
                            handArray[bI].boneMatrix = lhcPalmMatrix * Matrix4x4.CreateScale(-1, 1, -1);
                            handArray[bI].boneMatrix.M42 += boneArray[neck].worldBase.Translation.Y;
                            handArray[bI].SetWorldFromBoneMatrix();
                            handArray[bI].setLocal(false, false, true);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_wrist_l, bI, handArray[0].id, handArray[0], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.wrist.Position;
                            handArray[bI].worldBase.Rotation = (Quaternion.CreateFromYawPitchRoll(-90 * Deg2Rad, 0 * Deg2Rad, 0 * Deg2Rad) * hand.wrist.Rotation.Convert()).Convert();
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false, false, true);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_thumb_a_l, bI, handArray[1].id, handArray[1], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.thumb1Proximal.Position;
                            handArray[bI].worldBase.Rotation = hand.thumb1Proximal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_thumb_b_l, bI, handArray[2].id, handArray[2], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.thumb3Distal.Position;
                            handArray[bI].worldBase.Rotation = hand.thumb3Distal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_finger_index_a_l, bI, handArray[1].id, handArray[1], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.index1Proximal.Position;
                            handArray[bI].worldBase.Rotation = hand.index1Proximal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_finger_index_b_l, bI, handArray[4].id, handArray[4], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.index3Distal.Position;
                            handArray[bI].worldBase.Rotation = hand.index3Distal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_finger_middle_a_l, bI, handArray[1].id, handArray[1], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.middle1Proximal.Position;
                            handArray[bI].worldBase.Rotation = hand.middle1Proximal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_finger_middle_b_l, bI, handArray[6].id, handArray[6], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.middle3Distal.Position;
                            handArray[bI].worldBase.Rotation = hand.middle3Distal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_finger_ring_a_l, bI, handArray[1].id, handArray[1], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.ring1Proximal.Position;
                            handArray[bI].worldBase.Rotation = hand.ring1Proximal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_finger_ring_b_l, bI, handArray[8].id, handArray[8], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.ring3Distal.Position;
                            handArray[bI].worldBase.Rotation = hand.ring3Distal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_finger_pinky_a_l, bI, handArray[1].id, handArray[1], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.pinky1Proximal.Position;
                            handArray[bI].worldBase.Rotation = hand.pinky1Proximal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_finger_pinky_b_l, bI, handArray[10].id, handArray[10], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.pinky3Distal.Position;
                            handArray[bI].worldBase.Rotation = hand.pinky3Distal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[0].CalculateMatrix(true);

                            Matrix4x4 trnsOff = Matrix4x4.CreateTranslation(0.25f, 0.0f, 0);

                            for (int i = 0; i < 12; i++)
                            {
                                if (handArray[i].parent == null)
                                    p1 = Vector3.Zero;
                                else
                                    p1 = Vector3.Transform(handArray[i].parent!.boneMatrix.Translation, trnsOff);
                                p2 = Vector3.Transform(handArray[i].boneMatrix.Translation, trnsOff);
                                Imports.SetRayCoordinate((float*)&p1, (float*)&p2);
                            }

                            /*
                            Bone elbowPole = new Bone((BoneList)BoneListEn.e_arm_l, 999, boneArray[collarboneL].id, boneArray[collarboneL], new hkQsTransformf(), new hkQsTransformf());
                            elbowPole.boneMatrix.M41 += 0.15f;
                            elbowPole.boneMatrix.M42 -= 0.2f;
                            elbowPole.boneMatrix.M43 -= 0.25f;
                            elbowPole.SetWorldFromBoneMatrix();
                            p1 = Vector3.Transform(boneArray[collarboneL].boneMatrix.Translation, plrSkeletonPosition);
                            p2 = Vector3.Transform(elbowPole.boneMatrix.Translation, plrSkeletonPosition);
                            Imports.SetRayCoordinate((float*)&p1, (float*)&p2);

                            p1 = Vector3.Zero; // Transform(boneArray[collarboneL].boneMatrix.Translation, plrSkeletonPosition);
                            p2 = Vector3.Transform(handArray[1].boneMatrix.Translation, plrSkeletonPosition);
                            Imports.SetRayCoordinate((float*)&p1, (float*)&p2);

                            Vector3 angles = GetAngles(lhcPalmMatrix);
                            IK ik = new IK();
                            ik.lowerElbowRotation = -(90 + (angles[2] / -2.0f * Rad2Deg));
                            ik.upperElbowRotation = -90.0f;
                            ik.target = handArray[1];
                            ik.pole = elbowPole;
                            ik.start = boneArray[armL];
                            ik.joint = boneArray[forearmL];
                            ik.end = boneArray[wristL];
                            ik.Update();

                            boneArray[handL].boneMatrix = handArray[1].boneMatrix;
                            boneArray[handL].SetWorldFromBoneMatrix();
                            boneArray[handL].setLocal(false);
                            */
                            List<Tuple<short, Bone>> fingers = new List<Tuple<short, Bone>>();
                            //fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_hand_l, -1), handArray[1]));
                            fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_thumb_a_l, -1), handArray[2]));
                            fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_thumb_b_l, -1), handArray[3]));
                            fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_finger_index_a_l, -1), handArray[4]));
                            fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_finger_index_b_l, -1), handArray[5]));
                            fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_finger_middle_a_l, -1), handArray[6]));
                            fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_finger_middle_b_l, -1), handArray[7]));
                            fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_finger_ring_a_l, -1), handArray[8]));
                            fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_finger_ring_b_l, -1), handArray[9]));
                            fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_finger_pinky_a_l, -1), handArray[10]));
                            fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_finger_pinky_b_l, -1), handArray[11]));

                            foreach (Tuple<short, Bone> item in fingers)
                            {
                                //boneArray[item.Item1].setUpdates(true, true, true);
                                //boneArray[item.Item1].worldBase.Translation = (boneArray[handL].worldBase.Translation.Convert() + Vector3.Transform(item.Item3.Convert(), boneArray[handL].transform.Rotation.Convert())).Convert();
                                //boneArray[item.Item1].worldBase.Rotation = (boneArray[handL].worldBase.Rotation.Convert() * item.Item2.Convert()).Convert();
                                //boneArray[item.Item1].worldBase.Translation = item.Item3; // (item.Item3.Convert() * -1).Convert();
                                //boneArray[item.Item1].transform.Rotation = (Quaternion.CreateFromYawPitchRoll(0, 0, 90 * Deg2Rad) * item.Item2.Convert()).Convert();
                                //boneArray[item.Item1].transform.Translation = item.Item3;
                                //boneArray[item.Item1].transform.Rotation = Quaternion.Inverse(item.Item2.Convert()).Convert();
                                //boneArray[item.Item1].transform.Rotation = item.Item2;
                                //boneArray[item.Item1].transform.Rotation = (Quaternion.CreateFromYawPitchRoll(-90 * Deg2Rad, 0, 0) * item.Item2.Convert()).Convert();
                                //boneArray[item.Item1].transform.Scale = item.Item4;
                                //boneArray[item.Item1].setWorldMatrix();
                                //boneArray[item.Item1].setLocal();
                                //boneArray[item.Item1].setWorld();
                                //boneArray[item.Item1].CalculateMatrix();

                            }
                            boneArray[handL].CalculateMatrix(true);
                            boneArray[handL].setUpdates(true, true, true, true);

                        }
                        if (handR >= 0 && xivr.cfg!.data.motioncontrol)
                        {
                            //boneArray[armR].SetReference(false, true);
                            //boneArray[armR].transform.Rotation = Quaternion.Identity.Convert();//Quaternion.CreateFromYawPitchRoll(-90 * Deg2Rad, 180 * Deg2Rad, 90 * Deg2Rad).Convert();
                            //boneArray[forearmR].transform.Rotation = Quaternion.Identity.Convert();//Quaternion.CreateFromYawPitchRoll(0, 0, -90 * Deg2Rad).Convert();
                            //boneArray[handR].transform.Rotation = Quaternion.Identity.Convert();
                            //boneArray[wristR].transform.Rotation = Quaternion.Identity.Convert();
                            //boneArray[collarboneR].CalculateMatrix(true);

                            Vector3 p1 = Vector3.Zero;
                            Vector3 p2 = Vector3.Zero;

                            ShowHandBoneLayout(poseType.RightHand, rhcPalmMatrix);
                            fingerHandLayout hand = Imports.GetSkeletalPose(poseType.RightHand);
                            Bone[] handArray = new Bone[12];
                            short bI = 0;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_root, bI, 0, null, new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.root.Position;
                            handArray[bI].worldBase.Rotation = hand.root.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].boneMatrix = rhcPalmMatrix * Matrix4x4.CreateScale(-1, 1, -1);
                            handArray[bI].boneMatrix.M42 += boneArray[neck].worldBase.Translation.Y;
                            handArray[bI].SetWorldFromBoneMatrix();
                            handArray[bI].setLocal(false, false, true);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_wrist_r, bI, handArray[0].id, handArray[0], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.wrist.Position;
                            handArray[bI].worldBase.Rotation = (Quaternion.CreateFromYawPitchRoll(-90 * Deg2Rad, 0 * Deg2Rad, 0 * Deg2Rad) * hand.wrist.Rotation.Convert()).Convert();
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false, false, true);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_thumb_a_r, bI, handArray[1].id, handArray[1], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.thumb0Metacarpal.Position;
                            handArray[bI].worldBase.Rotation = hand.thumb0Metacarpal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_thumb_b_r, bI, handArray[2].id, handArray[2], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.thumb3Distal.Position;
                            handArray[bI].worldBase.Rotation = hand.thumb3Distal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_finger_index_a_r, bI, handArray[1].id, handArray[1], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.index1Proximal.Position;
                            handArray[bI].worldBase.Rotation = hand.index1Proximal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_finger_index_b_r, bI, handArray[4].id, handArray[4], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.index3Distal.Position;
                            handArray[bI].worldBase.Rotation = hand.index3Distal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_finger_middle_a_r, bI, handArray[1].id, handArray[1], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.middle1Proximal.Position;
                            handArray[bI].worldBase.Rotation = hand.middle1Proximal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_finger_middle_b_r, bI, handArray[6].id, handArray[6], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.middle3Distal.Position;
                            handArray[bI].worldBase.Rotation = hand.middle3Distal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_finger_ring_a_r, bI, handArray[1].id, handArray[1], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.ring1Proximal.Position;
                            handArray[bI].worldBase.Rotation = hand.ring1Proximal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_finger_ring_b_r, bI, handArray[8].id, handArray[8], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.ring3Distal.Position;
                            handArray[bI].worldBase.Rotation = hand.ring3Distal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_finger_pinky_a_r, bI, handArray[1].id, handArray[1], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.pinky1Proximal.Position;
                            handArray[bI].worldBase.Rotation = hand.pinky1Proximal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[bI] = new Bone((BoneList)BoneListEn.e_finger_pinky_b_r, bI, handArray[10].id, handArray[10], new hkQsTransformf(), new hkQsTransformf());
                            handArray[bI].worldBase.Translation = hand.pinky3Distal.Position;
                            handArray[bI].worldBase.Rotation = hand.pinky3Distal.Rotation;
                            handArray[bI].worldBase.Scale = Vector3.One.Convert();
                            handArray[bI].setLocal(false);
                            bI++;
                            handArray[0].CalculateMatrix(true);


                            Matrix4x4 trnsOff = Matrix4x4.CreateTranslation(-0.25f, 0.0f, 0);

                            for (int i = 0; i < 12; i++)
                            {
                                if (handArray[i].parent == null)
                                    p1 = Vector3.Zero;
                                else
                                    p1 = Vector3.Transform(handArray[i].parent!.boneMatrix.Translation, trnsOff);
                                p2 = Vector3.Transform(handArray[i].boneMatrix.Translation, trnsOff);
                                Imports.SetRayCoordinate((float*)&p1, (float*)&p2);
                            }
                            /*
                            Bone elbowPole = new Bone((BoneList)BoneListEn.e_arm_r, 999, boneArray[collarboneR].id, boneArray[collarboneR], new hkQsTransformf(), new hkQsTransformf());
                            elbowPole.boneMatrix.M41 -= 0.15f;
                            elbowPole.boneMatrix.M42 -= 0.2f;
                            elbowPole.boneMatrix.M43 -= 0.25f;
                            elbowPole.SetWorldFromBoneMatrix();
                            p1 = Vector3.Transform(boneArray[collarboneR].boneMatrix.Translation, plrSkeletonPosition);
                            p2 = Vector3.Transform(elbowPole.boneMatrix.Translation, plrSkeletonPosition);
                            Imports.SetRayCoordinate((float*)&p1, (float*)&p2);

                            p1 = Vector3.Zero; // Transform(boneArray[collarboneL].boneMatrix.Translation, plrSkeletonPosition);
                            p2 = Vector3.Transform(handArray[1].boneMatrix.Translation, plrSkeletonPosition);
                            Imports.SetRayCoordinate((float*)&p1, (float*)&p2);

                            Vector3 angles = GetAngles(rhcPalmMatrix);
                            IK ik = new IK();
                            ik.lowerElbowRotation = (90 + (angles[2] / 2.0f * Rad2Deg));
                            ik.upperElbowRotation = 90.0f;
                            ik.target = handArray[1];
                            ik.pole = elbowPole;
                            ik.start = boneArray[armR];
                            ik.joint = boneArray[forearmR];
                            ik.end = boneArray[wristR];
                            ik.Update(true);
                            
                            boneArray[handR].boneMatrix = handArray[1].boneMatrix;
                            boneArray[handR].SetWorldFromBoneMatrix();
                            boneArray[handR].setLocal(false);
                            */
                            List<Tuple<short, Bone>> fingers = new List<Tuple<short, Bone>>();
                            //fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_hand_r, -1), handArray[1]));
                            fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_thumb_a_r, -1), handArray[2]));
                            //fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_thumb_b_r, -1), handArray[3]));
                            fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_finger_index_a_r, -1), handArray[4]));
                            //fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_finger_index_b_r, -1), handArray[5]));
                            fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_finger_middle_a_r, -1), handArray[6]));
                            //fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_finger_middle_b_r, -1), handArray[7]));
                            fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_finger_ring_a_r, -1), handArray[8]));
                            //fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_finger_ring_b_r, -1), handArray[9]));
                            fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_finger_pinky_a_r, -1), handArray[10]));
                            //fingers.Add(new Tuple<short, Bone>(boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_finger_pinky_b_r, -1), handArray[11]));

                            Matrix4x4 convert = Matrix4x4.CreateFromYawPitchRoll(xivr.cfg!.data.offsetAmountX * Deg2Rad, xivr.cfg!.data.offsetAmountY * Deg2Rad, xivr.cfg!.data.offsetAmountZ * Deg2Rad);
                            foreach (Tuple<short, Bone> item in fingers)
                            {
                                //boneArray[item.Item1].boneMatrix = convert;// * boneArray[item.Item1].boneMatrix;// item.Item2.boneMatrix;
                                //boneArray[item.Item1].SetWorldFromBoneMatrix();
                                //boneArray[item.Item1].setLocal(false);
                                //Vector3 anglesA = boneArray[item.Item1].ToEulerAngles(boneArray[item.Item1].transform.Rotation);
                                //Vector3 anglesB = item.Item2.ToEulerAngles(item.Item2.transform.Rotation);
                                //PluginLog.Log($"{(BoneListEn)item.Item1} - {item.Item1} | {anglesA} | {anglesB}");

                                //boneArray[item.Item1].transform.Rotation = Quaternion.Identity.Convert();
                                //boneArray[item.Item1].transform.Rotation = item.Item2.transform.Rotation;
                                //boneArray[item.Item1].boneMatrix = convert * item.Item2.boneMatrix;
                                //boneArray[item.Item1].SetWorldFromBoneMatrix();
                                //boneArray[item.Item1].setLocal(false, false, false);
                                //boneArray[item.Item1].transform = item.Item2.transform;
                                //boneArray[item.Item1].setUpdates(false, true, false);

                                //boneArray[item.Item1].worldBase.Translation = (boneArray[handL].worldBase.Translation.Convert() + Vector3.Transform(item.Item3.Convert(), boneArray[handL].transform.Rotation.Convert())).Convert();
                                //boneArray[item.Item1].worldBase.Rotation = (boneArray[handL].worldBase.Rotation.Convert() * item.Item2.Convert()).Convert();
                                //boneArray[item.Item1].worldBase.Translation = item.Item3; // (item.Item3.Convert() * -1).Convert();
                                //boneArray[item.Item1].transform.Rotation = (Quaternion.CreateFromYawPitchRoll(0, 0, 90 * Deg2Rad) * item.Item2.Convert()).Convert();
                                //boneArray[item.Item1].transform.Rotation = Quaternion.Inverse(item.Item2.Convert()).Convert();
                                //boneArray[item.Item1].transform.Rotation = (Quaternion.CreateFromYawPitchRoll(-90 * Deg2Rad, 0, 0) * item.Item2.Convert()).Convert();
                                //boneArray[item.Item1].setWorldMatrix();
                                //boneArray[item.Item1].setLocal();
                                //boneArray[item.Item1].setWorld();
                                //boneArray[item.Item1].CalculateMatrix();
                            }

                            /*if (vr0r > 0)
                            {
                                boneArray[vr0r].boneMatrix = boneArray[handR].boneMatrix;// * plrSkeletonPositionI;
                                boneArray[vr0r].SetWorldFromBoneMatrix();
                                boneArray[vr0r].setLocal(false, false, true);
                                boneArray[vr0r].setUpdates(true, true, true);

                                vr0rMatrix = boneArray[vr0r].boneMatrix;// * plrSkeletonPosition;

                            }*/
                            boneArray[handR].CalculateMatrix(true);
                            boneArray[handR].setUpdates(true, true, true, true);


                            //short leg = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_right_leg, -1);
                            //boneArray[leg].boneMatrix = boneArray[handR].boneMatrix;
                            //boneArray[leg].SetWorldFromBoneMatrix();
                            //boneArray[leg].setLocal(false);
                            //boneArray[leg].CalculateMatrix(true);
                            //boneArray[leg].setUpdates(true, true, true, true);

                            /*boneArray[fingers[0].Item1].boneMatrix = handArray[3].boneMatrix;
                            boneArray[fingers[0].Item1].SetTransformFromWorldBase();
                            boneArray[fingers[1].Item1].boneMatrix = handArray[4].boneMatrix;
                            boneArray[fingers[1].Item1].SetTransformFromWorldBase();

                            boneArray[fingers[2].Item1].boneMatrix = handArray[7].boneMatrix;
                            boneArray[fingers[2].Item1].SetTransformFromWorldBase();
                            boneArray[fingers[3].Item1].boneMatrix = handArray[8].boneMatrix;
                            boneArray[fingers[3].Item1].SetTransformFromWorldBase();

                            boneArray[fingers[4].Item1].boneMatrix = handArray[11].boneMatrix;
                            boneArray[fingers[4].Item1].SetTransformFromWorldBase();
                            boneArray[fingers[5].Item1].boneMatrix = handArray[12].boneMatrix;
                            boneArray[fingers[5].Item1].SetTransformFromWorldBase();
                            //PluginLog.Log($"{lHand.thumb0Metacarpal.quat.x} {lHand.thumb0Metacarpal.quat.y} {lHand.thumb0Metacarpal.quat.z} {lHand.thumb0Metacarpal.quat.w} | {lHand.thumb2Middle.quat.x} {lHand.thumb2Middle.quat.y} {lHand.thumb2Middle.quat.z} {lHand.thumb2Middle.quat.w}");
                            */

                        }
                        if (weaponL >= 0 && weaponR >= 0 && xivr.cfg!.data.motioncontrol)
                        {
                            if (!xivr.cfg!.data.showWeaponInHand)
                            {
                                boneArray[weaponL].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));
                                boneArray[weaponR].SetScale(new Vector3(0.0001f, 0.0001f, 0.0001f));
                            }
                        }
                    }
                }
            }
            /*
            int objCount = 4;// xivr.ObjectTable!.Length;
            for (int i = 1; i < objCount; i++)
            {
                Dalamud.Game.ClientState.Objects.Types.GameObject? tmpObj = xivr.ObjectTable![i];
                if (tmpObj == null)
                    continue;

                GameObject* bonedObject = (GameObject*)tmpObj.Address;
                Character* bonedCharacter = (Character*)tmpObj.Address;
                if (bonedObject == null)
                    continue;

                Structures.Model* model = (Structures.Model*)bonedObject->DrawObject;
                if (model == null)
                    continue;

                Skeleton* skeleton = model->skeleton;
                if (skeleton == null)
                    continue;

                Matrix4x4 objSkeletonPosition = Matrix4x4.CreateFromQuaternion(skeleton->Transform.Rotation);
                objSkeletonPosition.Translation = skeleton->Transform.Position;
                objSkeletonPosition.SetScale(skeleton->Transform.Scale);
                Matrix4x4.Invert(objSkeletonPosition, out Matrix4x4 objSkeletonPositionI);
                //PluginLog.Log($"{objSkeletonPosition.Translation} {objSkeletonPositionI.Translation}");

                for (ushort p = 0; p < skeleton->PartialSkeletonCount; p++)
                {
                    hkaPose* objPose = skeleton->PartialSkeletons[p].GetHavokPose(0);
                    if (objPose == null)
                        continue;

                    UInt64 objPose64 = (UInt64)objPose;
                    if(!boneLayout.ContainsKey(objPose64))
                        boneLayout.Add(objPose64, new Dictionary<BoneList, short>());

                    //----
                    // Loops though the pose bones and updates the ones that have tracking
                    //----
                    Bone[] boneArray = new Bone[objPose->LocalPose.Length];
                    for (short j = 0; j < objPose->LocalPose.Length; j++)
                    {
                        string boneName = objPose->Skeleton->Bones[j].Name.String!;
                        short parentId = objPose->Skeleton->ParentIndices[j];

                        BoneList boneKey = BoneOutput.boneNameToEnum.GetValueOrDefault<string, BoneList>(boneName, BoneList._unknown_);
                        //PluginLog.Log($"{boneName} | {boneKey}");
                        if (boneKey == BoneList._unknown_)
                        {
                            if (!BoneOutput.reportedBones.ContainsKey(boneName))
                            {
                                PluginLog.Log($"{p} {objPose64:X} {j} : Error finding bone {boneName}");
                                BoneOutput.reportedBones.Add(boneName, true);
                            }
                            boneName = "_unknown_";
                        }
                        else
                            boneLayout[objPose64][boneKey] = j;

                        //PluginLog.Log($"{p} {(UInt64)objPose:X} {i} : {boneName} {boneKey} {parentId}");

                        if (parentId < 0)
                            boneArray[j] = new Bone(boneKey, j, parentId, null, objPose->LocalPose[j], objPose->Skeleton->ReferencePose[j]);
                        else
                            boneArray[j] = new Bone(boneKey, j, parentId, boneArray[parentId], objPose->LocalPose[j], objPose->Skeleton->ReferencePose[j]);

                        if (boneName == "iv_ochinko_f")
                        {
                            boneArray[j].boneMatrix = vr0rMatrix * (plrSkeletonPosition - objSkeletonPosition);
                            //PluginLog.Log($"{plrSkeletonPosition.Translation} - {objSkeletonPosition.Translation} = {boneArray[j].boneMatrix.Translation}");
                            boneArray[j].SetWorldFromBoneMatrix();
                            boneArray[j].setLocal(false, false, true);
                            boneArray[j].setUpdates(true, true, true);
                        }
                    }
                    rawBoneList[objPose64] = boneArray;
                }
            }*/
        }

        Matrix4x4 vr0rMatrix = Matrix4x4.Identity;

        private void UpdateBoneCamera2()
        {
            Matrix4x4 hmdFlip = Matrix4x4.CreateFromYawPitchRoll(90 * Deg2Rad, 0, 0);// * Matrix4x4.CreateFromAxisAngle(new Vector3(0, 0, 1), -90 * Deg2Rad);

            foreach (KeyValuePair<UInt64, Dictionary<BoneList, short>> boneData in boneLayout)
            {
                UInt64 objPose64 = boneData.Key;
                Bone[] boneArray = rawBoneList.GetValueOrDefault<ulong, Bone[]>(objPose64, new Bone[0]);

                if (boneArray.Length > 0)
                {
                    short rootBone = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_root, -1);
                    short headBone = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_head, -1);

                    if (rootBone >= 0)
                    {
                        short neck = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_neck, -1);
                        short collarboneL = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_collarbone_l, -1);
                        short armL = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_arm_l, -1);
                        short forearmL = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_forearm_l, -1);
                        short elbowL = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_elbow_l, -1);
                        short handL = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_hand_l, -1);
                        short wristL = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_wrist_l, -1);

                        short handR = boneLayout[objPose64].GetValueOrDefault<BoneList, short>((BoneList)BoneListEn.e_hand_r, -1);

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
                            //headBoneMatrix = boneArray[headBone].localMatrix * boneArray[neck].localMatrixI;
                            //headBoneMatrix.Translation *= eyeMidPoint;
                            //headBoneMatrix *= boneArray[neck].boneMatrix;
                            //headBoneMatrix *= plrSkeletonPosition;

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

                        if (bridge >= 0)
                        {
                            if (xivr.cfg!.data.immersiveFull || xivr.cfg!.data.immersiveMovement || isMounted)
                            {
                                //Matrix4x4 thmdMatrix = hmdMatrix * hmdFlip * headBoneMatrix * curViewMatrixWithoutHMD;
                                //Matrix4x4 thmdMatrix = headBoneMatrix[curEye];// * curViewMatrixWithoutHMD;// * hmdFlip * headBoneMatrix * curViewMatrixWithoutHMD;
                                //thmdMatrix.M42 -= (xivr.cfg.data.offsetAmountYFPSMount / 100);
                                //thmdMatrix.M43 += (xivr.cfg.data.offsetAmountZFPSMount / 100);
                                //hmdMatrix = thmdMatrix;
                                //if (xivr.cfg.data.immersiveMovement || isMounted)
                                //    hmdMatrix.Translation = thmdMatrix.Translation;
                                //Matrix4x4.Invert(hmdMatrix, out hmdMatrixI);

                                hmdOffsetPerEye[curEye + 2] = hmdMatrix.Translation - hmdOffsetPerEye[curEye];
                                hmdOffsetPerEye[curEye] = hmdMatrix.Translation;
                            }
                        }

                        hkQsTransformf identTrans = new hkQsTransformf();
                        identTrans.Translation = new Vector3(0, 0, 0).Convert();
                        identTrans.Rotation = Quaternion.Identity.Convert();
                        identTrans.Scale = new Vector3(0.0001f, 0.0001f, 0.0001f).Convert();
                        boneArray[0].SetTransform(identTrans, false, true);
                    }
                }
            }
        }


        Vector3 diffPlrCam = new Vector3(0, 0, 0);


        [StructLayout(LayoutKind.Explicit)]
        public struct vtblTask
        {
            [FieldOffset(0x8)]
            public unsafe delegate* unmanaged[Stdcall]<Task*, float*, void> vf1;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct Task
        {
            [FieldOffset(0x0)] public vtblTask* vtbl;

            [VirtualFunction(1)]
            public unsafe void vf1(float* taskItem)
            {
                fixed (Task* ptr = &this)
                {
                    vtbl->vf1(ptr, taskItem);
                }
            }
        }

        //----
        // RunGameTasks
        //----
        private delegate void RunGameTasksDg(UInt64 a, float* frameTiming);
        [Signature(Signatures.RunGameTasks, DetourName = nameof(RunGameTasksFn))]
        private Hook<RunGameTasksDg>? RunGameTasksHook = null;

        [HandleStatus("RunGameTasks")]
        public void RunGameTasksStatus(bool status)
        {
            if (status == true)
                RunGameTasksHook?.Enable();
            else
                RunGameTasksHook?.Disable();
        }

        public void RunGameTasksFn(UInt64 a, float* frameTiming)
        {
            //*frameTiming = 0;
            //PluginLog.Log($"RunGameTasksFn Start {curEye} | {a:x} {*frameTiming}");
            RunUpdate();
            if (hooksSet && enableVR)
            {
                for (int i = 0; i < 40; i++)
                {
                    if (i == 18)
                        CheckVisibility();

                    if (i == 23 && gameMode.Current == CameraModes.FirstPerson)
                    {
                        bool orgFRF = frfCalculateViewMatrix;
                        frfCalculateViewMatrix = false;
                        Task* task1 = (Task*)((UInt64)(18 * 0x78) + *(UInt64*)(a + 0x58)); task1->vf1(frameTiming);
                        frfCalculateViewMatrix = orgFRF;
                    }

                    Task* task = (Task*)((UInt64)(i * 0x78) + *(UInt64*)(a + 0x58)); task->vf1(frameTiming);
                }
            }
            else
                RunGameTasksHook!.Original(a, frameTiming);
        }

        //----
        // FrameworkTick
        //----
        private delegate UInt64 FrameworkTickDg(Framework* FrameworkInstance);
        [Signature(Signatures.FrameworkTick, DetourName = nameof(FrameworkTickFn))]
        private Hook<FrameworkTickDg>? FrameworkTickHook = null;

        [HandleStatus("FrameworkTick")]
        public void FrameworkTickStatus(bool status)
        {
            if (status == true)
                FrameworkTickHook?.Enable();
            else
                FrameworkTickHook?.Disable();
        }

        public UInt64 FrameworkTickFn(Framework* FrameworkInstance)
        {
            if (hooksSet && enableVR)
            {
                //*(float*)(a + 0x16B8) = 0;
                //PluginLog.Log($"{(UInt64)FrameworkInstance:x} {((UInt64)FrameworkInstance + 0x16C0):x} {*(float*)(FrameworkInstance + 0x16C0)}");
                GetMultiplayerIKData();
                //ShowBoneLayout();

                UInt64 retVal = 0;
                curEye = 0;
                retVal = FrameworkTickHook!.Original(FrameworkInstance);
                curEye = 1;
                retVal = FrameworkTickHook!.Original(FrameworkInstance);
                //ShowBoneLayout();
                return retVal;
            }
            else
                return FrameworkTickHook!.Original(FrameworkInstance);
        }









        private Character* GetCharacterOrMouseover(byte charFrom = 3)
        {
            PlayerCharacter? player = xivr.ClientState!.LocalPlayer;
            UInt64 selectMouseOver = *(UInt64*)selectScreenMouseOver;

            if (player == null && selectMouseOver == 0)
                return null;

            if (selectMouseOver != 0 && (charFrom & 1) == 1)
                return (Character*)selectMouseOver;
            else if (player != null && (charFrom & 2) == 2)
                return (Character*)player!.Address;
            else
                return null;
        }

        private void CheckVisibilityInner(Character* character)
        {
            if (character == null)
                return;

            if ((ObjectKind)character->GameObject.ObjectKind == ObjectKind.Pc ||
                (ObjectKind)character->GameObject.ObjectKind == ObjectKind.BattleNpc ||
                (ObjectKind)character->GameObject.ObjectKind == ObjectKind.EventNpc ||
                (ObjectKind)character->GameObject.ObjectKind == ObjectKind.Mount ||
                (ObjectKind)character->GameObject.ObjectKind == ObjectKind.Companion ||
                (ObjectKind)character->GameObject.ObjectKind == ObjectKind.Retainer)
            {
                Structures.Model* model = (Structures.Model*)character->GameObject.DrawObject;
                if (model == null)
                    return;

                if (model->CullType == ModelCullTypes.InsideCamera && (byte)character->GameObject.TargetableStatus == 255)
                    model->CullType = ModelCullTypes.Visible;

                DrawDataContainer* drawData = &character->DrawData;
                if (drawData != null)
                {
                    UInt64 mhOffset = (UInt64)(&drawData->MainHand);
                    if (mhOffset != 0)
                    {
                        Structures.Model* mhWeap = *(Structures.Model**)(mhOffset + 0x8);
                        if (mhWeap != null)
                            mhWeap->CullType = ModelCullTypes.Visible;
                    }

                    UInt64 ohOffset = (UInt64)(&drawData->OffHand);
                    if (ohOffset != 0)
                    {
                        Structures.Model* ohWeap = *(Structures.Model**)(ohOffset + 0x8);
                        if (ohWeap != null)
                            ohWeap->CullType = ModelCullTypes.Visible;
                    }

                    UInt64 fOffset = (UInt64)(&drawData->UnkF0);
                    if (fOffset != 0)
                    {
                        Structures.Model* fWeap = *(Structures.Model**)(fOffset + 0x8);
                        if (fWeap != null)
                            fWeap->CullType = ModelCullTypes.Visible;
                    }
                }

                Structures.Model* mount = (Structures.Model*)model->mountedObject;
                if (mount != null)
                    mount->CullType = ModelCullTypes.Visible;

                Character.OrnamentContainer* oCont = &character->Ornament;
                if (oCont != null)
                {
                    GameObject* bonedOrnament = (GameObject*)oCont->OrnamentObject;
                    if (bonedOrnament != null)
                    {
                        Structures.Model* ornament = (Structures.Model*)bonedOrnament->DrawObject;
                        if (ornament != null)
                            ornament->CullType = ModelCullTypes.Visible;
                    }
                }
            }
        }
        private void CheckVisibility()
        {
            if (inCutscene.Current)
                return;

            //----
            // Check the player
            //----
            Character* character = GetCharacterOrMouseover(2);
            if (character != null && character != targetSystem->ObjectFilterArray0[0])
                CheckVisibilityInner(character);

            for (int i = 0; i < xivr.PartyList!.Length; i++)
            {
                Dalamud.Game.ClientState.Objects.Types.GameObject partyMember = xivr.PartyList[i]!.GameObject!;
                if (partyMember != null)
                {
                    Character* partyCharacter = (Character*)partyMember.Address;
                    if (character != null)
                        CheckVisibilityInner(partyCharacter);
                }
            }

            //----
            // Check anyone in sight
            //----
            for (int i = 0; i < targetSystem->ObjectFilterArray1.Length; i++)
                CheckVisibilityInner((Character*)targetSystem->ObjectFilterArray1[i]);
        }

        private void GetMultiplayerIKDataInner(bool isPlayer, Character* character, Matrix4x4 hmd, Matrix4x4 lhc, Matrix4x4 rhc)
        {
            if (character == null)
                return;

            if ((ObjectKind)character->GameObject.ObjectKind != ObjectKind.Pc)
                return;

            Structures.Model* model = (Structures.Model*)character->GameObject.DrawObject;
            if (model == null)
                return;

            Skeleton* skeleton = model->skeleton;
            if (skeleton == null)
                return;

            SkeletonResourceHandle* srh = skeleton->SkeletonResourceHandles[0];
            if (srh != null)
            {
                hkaSkeleton* hkaSkel = srh->HavokSkeleton;
                if (hkaSkel != null)
                    if (!commonBones.ContainsKey((UInt64)hkaSkel))
                    {
                        commonBones.Add((UInt64)hkaSkel, new stCommonSkelBoneList(skeleton));
                        PluginLog.Log($"commonBoneCount {commonBones.Count} {commonBones[(UInt64)hkaSkel].armLength}");
                    }
            }

            float armMultiplier = 100.0f;
            if (gameMode.Current == CameraModes.FirstPerson)
                armMultiplier = xivr.cfg!.data.armMultiplier;
            bool motionControls = xivr.cfg!.data.motioncontrol;

            multiIK[0].Enqueue(new stMultiIK(
                character->CurrentWorld,
                character->GameObject.ObjectID,
                character,
                skeleton,
                isPlayer,
                hmd,
                lhc,
                rhc,
                motionControls,
                armMultiplier
                ));
            multiIK[1].Enqueue(new stMultiIK(
                character->CurrentWorld,
                character->GameObject.ObjectID,
                character,
                skeleton,
                isPlayer,
                hmd,
                lhc,
                rhc,
                motionControls,
                armMultiplier
                ));
        }
        private void GetMultiplayerIKData()
        {
            if (inCutscene.Current || gameMode.Current == CameraModes.ThirdPerson)
                return;

            Character* character = GetCharacterOrMouseover(2);
            if (character != null && character != targetSystem->ObjectFilterArray0[0])
                GetMultiplayerIKDataInner(true, character, hmdMatrix, lhcMatrix, rhcMatrix);
            //else
            //    for (int i = 0; i < 1; i++)
            //        GetMultiplayerIKDataInner((Character*)targetSystem->ObjectFilterArray0[i], hmdMatrix, lhcMatrix, rhcMatrix);
            //targetSystem->ObjectFilterArray0.Length
        }

        private unsafe void ShowBoneLayoutInner(Character* character)
        {
            if (character == null)
                return;

            if ((ObjectKind)character->GameObject.ObjectKind == ObjectKind.Pc ||
                (ObjectKind)character->GameObject.ObjectKind == ObjectKind.BattleNpc ||
                (ObjectKind)character->GameObject.ObjectKind == ObjectKind.EventNpc ||
                (ObjectKind)character->GameObject.ObjectKind == ObjectKind.Mount ||
                (ObjectKind)character->GameObject.ObjectKind == ObjectKind.Companion ||
                (ObjectKind)character->GameObject.ObjectKind == ObjectKind.Retainer)
            {
                Structures.Model* model = (Structures.Model*)character->GameObject.DrawObject;
                if (model == null)
                    return;

                BoneOutput.DrawBones(model->skeleton);
            }
        }
        private unsafe void ShowBoneLayout()
        {
            //----
            // Draws Skeletal overlay for all models
            // to get the full bone list
            //----
            //Character* character = GetCharacterOrMouseover();
            //if (character != null && character != targetSystem->ObjectFilterArray1[0])
            //    ShowBoneLayoutInner(character);

            for (int i = 0; i < targetSystem->ObjectFilterArray1.Length; i++)
                ShowBoneLayoutInner((Character*)targetSystem->ObjectFilterArray1[i]);
        }

        private void ShowHandBoneLayout(poseType tPose, Matrix4x4 controller, float heightOffset = 1.0f)
        {
            fingerHandLayout hand = Imports.GetSkeletalPose(tPose);
            Bone[] handArray = new Bone[31];

            handArray[0] = new Bone((BoneList)BoneListEn.e_root, 0, 0, null, new hkQsTransformf(), new hkQsTransformf());
            handArray[0].boneMatrix = hand.root.Convert().ToMatrix() * controller;// * Matrix4x4.CreateScale(-1, 1, -1);
            handArray[1] = new Bone((BoneList)BoneListEn.e_wrist_l, 1, handArray[0].id, handArray[0], new hkQsTransformf(), new hkQsTransformf());
            handArray[1].boneMatrix = hand.wrist.Convert().ToMatrix() * controller;
            handArray[2] = new Bone((BoneList)BoneListEn.e_thumb_a_l, 2, handArray[1].id, handArray[1], new hkQsTransformf(), new hkQsTransformf());
            handArray[2].boneMatrix = hand.thumb0Metacarpal.Convert().ToMatrix() * controller;
            handArray[3] = new Bone((BoneList)BoneListEn.e_thumb_a_l, 3, handArray[2].id, handArray[2], new hkQsTransformf(), new hkQsTransformf());
            handArray[3].boneMatrix = hand.thumb1Proximal.Convert().ToMatrix() * controller;
            handArray[4] = new Bone((BoneList)BoneListEn.e_thumb_a_l, 4, handArray[2].id, handArray[2], new hkQsTransformf(), new hkQsTransformf());
            handArray[4].boneMatrix = hand.thumb2Middle.Convert().ToMatrix() * controller;
            handArray[5] = new Bone((BoneList)BoneListEn.e_thumb_a_l, 5, handArray[3].id, handArray[3], new hkQsTransformf(), new hkQsTransformf());
            handArray[5].boneMatrix = hand.thumb3Distal.Convert().ToMatrix() * controller;
            handArray[6] = new Bone((BoneList)BoneListEn.e_finger_index_a_l, 6, handArray[1].id, handArray[1], new hkQsTransformf(), new hkQsTransformf());
            handArray[6].boneMatrix = hand.index0Metacarpal.Convert().ToMatrix() * controller;
            handArray[7] = new Bone((BoneList)BoneListEn.e_finger_index_a_l, 7, handArray[6].id, handArray[6], new hkQsTransformf(), new hkQsTransformf());
            handArray[7].boneMatrix = hand.index1Proximal.Convert().ToMatrix() * controller;
            handArray[8] = new Bone((BoneList)BoneListEn.e_finger_index_a_l, 8, handArray[7].id, handArray[7], new hkQsTransformf(), new hkQsTransformf());
            handArray[8].boneMatrix = hand.index2Middle.Convert().ToMatrix() * controller;
            handArray[9] = new Bone((BoneList)BoneListEn.e_finger_index_a_l, 9, handArray[8].id, handArray[8], new hkQsTransformf(), new hkQsTransformf());
            handArray[9].boneMatrix = hand.index3Distal.Convert().ToMatrix() * controller;
            handArray[10] = new Bone((BoneList)BoneListEn.e_finger_middle_a_l, 10, handArray[1].id, handArray[1], new hkQsTransformf(), new hkQsTransformf());
            handArray[10].boneMatrix = hand.middle0Metacarpal.Convert().ToMatrix() * controller;
            handArray[11] = new Bone((BoneList)BoneListEn.e_finger_middle_a_l, 11, handArray[10].id, handArray[10], new hkQsTransformf(), new hkQsTransformf());
            handArray[11].boneMatrix = hand.middle1Proximal.Convert().ToMatrix() * controller;
            handArray[12] = new Bone((BoneList)BoneListEn.e_finger_middle_a_l, 12, handArray[11].id, handArray[11], new hkQsTransformf(), new hkQsTransformf());
            handArray[12].boneMatrix = hand.middle2Middle.Convert().ToMatrix() * controller;
            handArray[13] = new Bone((BoneList)BoneListEn.e_finger_middle_a_l, 13, handArray[12].id, handArray[12], new hkQsTransformf(), new hkQsTransformf());
            handArray[13].boneMatrix = hand.middle3Distal.Convert().ToMatrix() * controller;
            handArray[14] = new Bone((BoneList)BoneListEn.e_finger_ring_a_l, 14, handArray[1].id, handArray[1], new hkQsTransformf(), new hkQsTransformf());
            handArray[14].boneMatrix = hand.ring0Metacarpal.Convert().ToMatrix() * controller;
            handArray[15] = new Bone((BoneList)BoneListEn.e_finger_ring_a_l, 15, handArray[14].id, handArray[14], new hkQsTransformf(), new hkQsTransformf());
            handArray[15].boneMatrix = hand.ring1Proximal.Convert().ToMatrix() * controller;
            handArray[16] = new Bone((BoneList)BoneListEn.e_finger_ring_a_l, 16, handArray[15].id, handArray[15], new hkQsTransformf(), new hkQsTransformf());
            handArray[16].boneMatrix = hand.ring2Middle.Convert().ToMatrix() * controller;
            handArray[17] = new Bone((BoneList)BoneListEn.e_finger_ring_a_l, 17, handArray[16].id, handArray[16], new hkQsTransformf(), new hkQsTransformf());
            handArray[17].boneMatrix = hand.ring3Distal.Convert().ToMatrix() * controller;
            handArray[18] = new Bone((BoneList)BoneListEn.e_finger_pinky_a_l, 18, handArray[1].id, handArray[1], new hkQsTransformf(), new hkQsTransformf());
            handArray[18].boneMatrix = hand.pinky0Metacarpal.Convert().ToMatrix() * controller;
            handArray[19] = new Bone((BoneList)BoneListEn.e_finger_pinky_a_l, 19, handArray[18].id, handArray[18], new hkQsTransformf(), new hkQsTransformf());
            handArray[19].boneMatrix = hand.pinky1Proximal.Convert().ToMatrix() * controller;
            handArray[20] = new Bone((BoneList)BoneListEn.e_finger_pinky_a_l, 20, handArray[19].id, handArray[19], new hkQsTransformf(), new hkQsTransformf());
            handArray[20].boneMatrix = hand.pinky2Middle.Convert().ToMatrix() * controller;
            handArray[21] = new Bone((BoneList)BoneListEn.e_finger_pinky_a_l, 21, handArray[20].id, handArray[20], new hkQsTransformf(), new hkQsTransformf());
            handArray[21].boneMatrix = hand.pinky3Distal.Convert().ToMatrix() * controller;
            handArray[0].SetWorldFromBoneMatrix(true);

            Matrix4x4 trnsOff = Matrix4x4.Identity;
            Vector3 p1 = Vector3.Zero;
            Vector3 p2 = Vector3.Zero;

            if (tPose == poseType.LeftHand)
                trnsOff = Matrix4x4.CreateTranslation(0.5f, heightOffset, 0);
            else if (tPose == poseType.RightHand)
                trnsOff = Matrix4x4.CreateTranslation(-0.5f, heightOffset, 0);

            for (int i = 0; i < 22; i++)
            {
                if (handArray[i].parent == null)
                    p1 = Vector3.Zero;
                else
                    p1 = Vector3.Transform(handArray[i].parent!.boneMatrix.Translation, trnsOff);
                p2 = Vector3.Transform(handArray[i].boneMatrix.Translation, trnsOff);
                Imports.SetRayCoordinate((float*)&p1, (float*)&p2);
            }
        }
    }
}

