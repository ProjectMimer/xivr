﻿using System;
using System.IO;
using System.Drawing;
using System.Numerics;
using System.Diagnostics;
using System.Windows;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Game.ClientState.Objects.Enums;
using xivr.Structures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;


/* v2 
 * controller support
 * - basic - xbox emulation
 * - advanced - onward style movement + pointer ui + pointer ingame
 * vignett
 * floating screen scaling / offset
 * optoin updates
 * - set movement type legacy
 * - set window mode
 * - update resolution in config file
 * - enable controller mode
 * remove head
 * */


namespace xivr
{
    public enum attribFnType
    {
        Initalize = 0,
        Status = 1,
    }

    public delegate void HandleDelegte(bool status);
    public delegate void HandleInputDelegte(InputAnalogActionData analog, InputDigitalActionData digital);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void UpdateControllerInput(ActionButtonLayout buttonId, InputAnalogActionData analog, InputDigitalActionData digital);


    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class HandleAttribute : System.Attribute
    {
        public string fnName { get; private set; }
        public attribFnType fnType { get; private set; }
        public HandleAttribute(string name, attribFnType type)
        {
            fnName = name;
            fnType = type;
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class HandleInputAttribute : System.Attribute
    {
        public ActionButtonLayout inputId { get; private set; }
        public HandleInputAttribute(ActionButtonLayout buttonId) => inputId = buttonId;
    }


    enum poseType
    {
        Projection = 0,
        EyeOffset = 1,
        hmdPosition = 10,
        LeftHand = 20,
        RightHand = 30,
    }

    internal unsafe class xivr_hooks
    {
        protected Dictionary<string, HandleDelegte[]> functionList = new Dictionary<string, HandleDelegte[]>();
        protected Dictionary<ActionButtonLayout, HandleInputDelegte> inputList = new Dictionary<ActionButtonLayout, HandleInputDelegte>();

        [DllImport("kernel32.dll")]
        private static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll")]
        private static extern bool FlushInstructionCache(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize);

        [DllImport("openvr_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool VR_IsHmdPresent();

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetDX11(IntPtr Device);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void UnsetDX11();

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern Structures.Texture* GetUIRenderTexture(int curEye);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Recenter();

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetFramePose();

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern Matrix4x4 GetFramePose(poseType posetype, int eye);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetThreadedEye(int eye);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RenderVR();

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RenderUI(bool enableVR, bool enableFloatingHUD);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RenderFloatingScreen();

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetTexture();

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void UpdateZScale(float z, float scale);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetActiveJSON([In, MarshalAs(UnmanagedType.LPUTF8Str)] string filePath, int size);

        [DllImport("xivr_main.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void UpdateController(UpdateControllerInput controllerCallback);




        byte[] GetThreadedDataASM =
            {
                0x55, // push rbp
                0x65, 0x48, 0x8B, 0x04, 0x25, 0x58, 0x00, 0x00, 0x00, // mov rax,gs:[00000058]
                0x5D, // pop rbp
                0xC3  // ret
            };


        private const int FLAG_INVIS = (1 << 1) | (1 << 11);
        private const byte NamePlateCount = 50;
        private int curEye = 0;
        private UInt64 BaseAddress = 0;
        private bool hooksSet = false;
        private bool initalized = false;
        private int[] nextEye = { 1, 0 };
        private GCHandle getThreadedDataHandle;
        private bool enableVR = true;
        private bool enableFloatingHUD = true;
        private bool forceFloatingScreen = false;
        private bool horizontalLock = false;
        private bool verticalLock = false;
        private bool horizonLock = false;
        private Vector2 rotateAmount = new Vector2(0.0f, 0.0f);
        private Vector2 offsetAmount = new Vector2(0.0f, 0.0f);
        private Vector3 onwardAngle = new Vector3(0.0f, 0.0f, 0.0f);
        private Vector3 onwardDiff = new Vector3(0.0f, 0.0f, 0.0f);
        private Vector2 snapRotateAmount = new Vector2(0.0f, 0.0f);
        private float cameraZoom = 0.0f;
        private Stack<bool> overrideFromParent = new Stack<bool>();
        private Point HMDSize = new Point(2404, 2104);
        //private Point HMDSize = new Point(3740, 2160);
        private int[] runCount = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private float RadianConversion = MathF.PI / 180.0f;
        private Dictionary<ActionButtonLayout, bool> inputState = new Dictionary<ActionButtonLayout, bool>();
        private bool doOnward = false;

        UpdateControllerInput controllerCallback;

        Dictionary<ConfigOption, int> SavedSettings = new Dictionary<ConfigOption, int>();


        Matrix4x4 fixedProjection = Matrix4x4.Identity;

        Matrix4x4[] gameProjectionMatrix = {
                    Matrix4x4.Identity,
                    Matrix4x4.Identity
                };

        Matrix4x4[] eyeOffsetMatrix = {
                    Matrix4x4.Identity,
                    Matrix4x4.Identity
                };

        Matrix4x4 curViewMatrix = Matrix4x4.Identity;

        RenderTargetManager* renderTargetManager = null; //FFXIVClientStructs.FFXIV.Client.Graphics.Render.RenderTargetManager;
        CameraManagerInstance* camInst = null;
        UInt64 renderTargetManagerAddr = 0;


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
                foreach (System.Attribute attribute in method.GetCustomAttributes(typeof(HandleAttribute), false))
                {
                    string key = ((HandleAttribute)attribute).fnName;
                    attribFnType type = ((HandleAttribute)attribute).fnType;
                    HandleDelegte handle = (HandleDelegte)HandleDelegte.CreateDelegate(typeof(HandleDelegte), this, method);

                    if (!functionList.ContainsKey(key))
                        functionList.Add(key, new HandleDelegte[2]);
                    functionList[key][(int)type] = handle;
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
                    HandleInputDelegte handle = (HandleInputDelegte)HandleInputDelegte.CreateDelegate(typeof(HandleInputDelegte), this, method);

                    if (!inputList.ContainsKey(key))
                    {
                        inputList.Add(key, handle);
                        inputState.Add(key, false);
                    }
                }
            }
        }


        public bool Initialize()
        {
            if (initalized == false)
            {
                BaseAddress = (UInt64)Process.GetCurrentProcess()?.MainModule?.BaseAddress;
                PluginLog.Log($"Initialize {BaseAddress:X}");

                renderTargetManagerAddr = (UInt64)DalamudApi.SigScanner.GetStaticAddressFromSig("48 8B 05 ?? ?? ?? ?? 49 63 C8");
                if (renderTargetManagerAddr != 0)
                {
                    renderTargetManagerAddr = *(UInt64*)renderTargetManagerAddr;
                    renderTargetManager = (RenderTargetManager*)(*(UInt64*)renderTargetManagerAddr);
                }
                PluginLog.Log($"renderTargetManager: {*(UInt64*)renderTargetManagerAddr:X} {(*(UInt64*)renderTargetManagerAddr - BaseAddress):X}");

                IntPtr tmpAddress = DalamudApi.SigScanner.GetStaticAddressFromSig("48 8B 05 ?? ?? ?? ?? 83 78 50 00 75 22");
                PluginLog.Log($"CameraManagerInstance: {*(UInt64*)tmpAddress:X} {(*(UInt64*)tmpAddress - BaseAddress):X}");
                camInst = (CameraManagerInstance*)(*(UInt64*)tmpAddress);

                SetFunctionHandles();
                SetInputHandles();


                //----
                // Initalize all sigs
                //----
                foreach (KeyValuePair<string, HandleDelegte[]> attrib in functionList)
                {
                    attrib.Value[(int)attribFnType.Initalize](false);
                }

                controllerCallback = (buttonId, analog, digital) =>
                {
                    if (inputList.ContainsKey(buttonId))
                        inputList[buttonId](analog, digital);
                };

                initalized = true;
            }
            return initalized;
        }



        public void Start()
        {
            if (initalized == true && hooksSet == false && VR_IsHmdPresent())
            {
                PluginLog.Log($"VRInit {(IntPtr)FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance():X}");
                SetDX11((IntPtr)FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance());

                //string filePath = DalamudApi.PluginInterface.AssemblyLocation.DirectoryName + "\\config\\actions.json";
                string filePath = Path.Join(DalamudApi.PluginInterface.AssemblyLocation.DirectoryName, "config", "actions.json");
                if (SetActiveJSON(filePath, filePath.Length) == false)
                {
                    PluginLog.LogError($"Error loading Json file : {filePath}");
                }

                gameProjectionMatrix[0] = Matrix4x4.Transpose(GetFramePose(poseType.Projection, 0));
                gameProjectionMatrix[1] = Matrix4x4.Transpose(GetFramePose(poseType.Projection, 1));
                gameProjectionMatrix[0].M43 *= -1;
                gameProjectionMatrix[1].M43 *= -1;

                Matrix4x4.Invert(GetFramePose(poseType.EyeOffset, 0), out eyeOffsetMatrix[0]);
                Matrix4x4.Invert(GetFramePose(poseType.EyeOffset, 1), out eyeOffsetMatrix[1]);

                //SavedSettings[ConfigOption.MoveMode] = ConfigModule.Instance()->GetIntValue(ConfigOption.MoveMode);
                //SavedSettings[ConfigOption.ScreenMode] = ConfigModule.Instance()->GetIntValue(ConfigOption.ScreenMode);
                //SavedSettings[ConfigOption.ScreenTop] = ConfigModule.Instance()->GetIntValue(ConfigOption.ScreenTop);
                //SavedSettings[ConfigOption.ScreenLeft] = ConfigModule.Instance()->GetIntValue(ConfigOption.ScreenLeft);
                //SavedSettings[ConfigOption.ScreenWidth] = ConfigModule.Instance()->GetIntValue(ConfigOption.ScreenWidth);
                //SavedSettings[ConfigOption.ScreenHeight] = ConfigModule.Instance()->GetIntValue(ConfigOption.ScreenHeight);

                //ConfigModule.Instance()->SetOption(ConfigOption.MoveMode, 1);
                //ConfigModule.Instance()->SetOption(ConfigOption.ScreenMode, 0);
                //ConfigModule.Instance()->SetOption(ConfigOption.ScreenTop, 0);
                //ConfigModule.Instance()->SetOption(ConfigOption.ScreenLeft, 0);
                //ConfigModule.Instance()->SetOption(ConfigOption.ScreenWidth, HMDSize.X);
                //ConfigModule.Instance()->SetOption(ConfigOption.ScreenHeight, HMDSize.Y);

                //----
                // Enable all hooks
                //----
                foreach (KeyValuePair<string, HandleDelegte[]> attrib in functionList)
                {
                    attrib.Value[(int)attribFnType.Status](true);
                }

                snapRotateAmount.X = 45 * RadianConversion;
                snapRotateAmount.Y = 15 * RadianConversion;

                hooksSet = true;
                PrintEcho("Starting Hooks.");
            }
        }

        public void Stop()
        {
            if (hooksSet == true)
            {
                //----
                // Disable all hooks
                //----
                foreach (KeyValuePair<string, HandleDelegte[]> attrib in functionList)
                {
                    attrib.Value[(int)attribFnType.Status](false);
                }

                //ConfigModule.Instance()->SetOption(ConfigOption.MoveMode, SavedSettings[ConfigOption.MoveMode]);
                //ConfigModule.Instance()->SetOption(ConfigOption.ScreenMode, SavedSettings[ConfigOption.ScreenMode]);
                //ConfigModule.Instance()->SetOption(ConfigOption.ScreenTop, SavedSettings[ConfigOption.ScreenTop]);
                //ConfigModule.Instance()->SetOption(ConfigOption.ScreenLeft, SavedSettings[ConfigOption.ScreenLeft]);
                //ConfigModule.Instance()->SetOption(ConfigOption.ScreenWidth, SavedSettings[ConfigOption.ScreenWidth]);
                //ConfigModule.Instance()->SetOption(ConfigOption.ScreenHeight, SavedSettings[ConfigOption.ScreenHeight]);

                gameProjectionMatrix[0] = Matrix4x4.Identity;
                gameProjectionMatrix[1] = Matrix4x4.Identity;
                eyeOffsetMatrix[0] = Matrix4x4.Identity;
                eyeOffsetMatrix[1] = Matrix4x4.Identity;

                UnsetDX11();

                hooksSet = false;
                PrintEcho("Stopping Hooks.");
            }
        }

        public int PlayerRedrawCount = 0;

        public void Update(Dalamud.Game.Framework framework_)
        {
            if (hooksSet)
            {
                UpdateController(controllerCallback);

                AtkUnitBase* CharSelectAddon = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("_CharaSelectTitle", 1);
                AtkUnitBase* CharMakeAddon = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("_CharaMakeTitle", 1);

                if (CharSelectAddon == null && CharMakeAddon == null && DalamudApi.ClientState.LocalPlayer == null)
                    forceFloatingScreen = true;

                /*
                PlayerCharacter? player = DalamudApi.ClientState.LocalPlayer;
                if (player != null)
                {
                    IntPtr playerBase = player.Address;
                    *(int*)(playerBase + 0x104) = 8;

                    if(PlayerRedrawCount == 3)
                        *(int*)(playerBase + 0x104) |= FLAG_INVIS;
                    else if(PlayerRedrawCount == 1)
                        *(int*)(playerBase + 0x104) &= ~FLAG_INVIS;

                    if (PlayerRedrawCount > 0)
                    {
                        PlayerRedrawCount--;

                    }


                    //byte[] customPlayer = player.Customize;
                    //PluginLog.Log($"Custom {player.Address:X}");
                    //for (int i = 0; i < 10; i++)
                    //    PluginLog.Log($"{customPlayer[i]:X}");
                    //Dalamud.Game.ClientState.Objects.Enums.CustomizeIndex

                    if (camInst != null)
                    {
                        UInt64 camAddress = (camInst->CameraOffset + (camInst->CameraIndex * 8));
                        if (camAddress > 0)
                        {
                            //((GameCamera*)camAddress)->CurrentHRotation = 0.0f;
                        }
                    }
                }
                */

                curEye = nextEye[curEye];
                SetFramePose();
            }

        }

        public void ForceFloatingScreen(bool forceFloating)
        {
            forceFloatingScreen = forceFloating;
        }

        public void SetLocks(bool horizontal, bool vertical, bool horizon)
        {
            horizontalLock = horizontal;
            verticalLock = vertical;
            horizonLock = horizon;
        }

        public void SetRotateAmount(float x, float y)
        {
            rotateAmount.X = (x * RadianConversion);
            rotateAmount.Y = (y * RadianConversion);
        }

        public void SetOffsetAmount(float x, float y)
        {
            if (x != 0) offsetAmount.X = (x / 100.0f) * -1;
            if (y != 0) offsetAmount.Y = (y / 100.0f) * -1;
        }

        public void SetSnapAmount(float x, float y)
        {
            if (x != 0) snapRotateAmount.X = (x * RadianConversion);
            if (x != 0) snapRotateAmount.Y = (y * RadianConversion);
        }

        public void SetZScale(float z, float scale)
        {
            UpdateZScale(z, scale);
        }

        public void Draw()
        {

        }

        public void Dispose()
        {
            Stop();
            //getFixedProjectionHandle.Free();
            getThreadedDataHandle.Free();
            initalized = false;
        }


        private void AddClearCommand()
        {
            UInt64 threadedOffset = GetThreadedOffset();
            if (threadedOffset != 0)
            {
                UInt64 queueData = AllocateQueueMemmoryFn(threadedOffset, 0x38);
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

        [HandleAttribute("GetThreadedData", attribFnType.Initalize)]
        public void GetThreadedDataInit(bool status)
        {
            //----
            // Used to access gs:[00000058] until i can do it in c#
            //----
            getThreadedDataHandle = GCHandle.Alloc(GetThreadedDataASM, GCHandleType.Pinned);
            if (!VirtualProtectEx(Process.GetCurrentProcess().Handle, getThreadedDataHandle.AddrOfPinnedObject(), (UIntPtr)GetThreadedDataASM.Length, 0x40 /* EXECUTE_READWRITE */, out uint _))
                return;
            else
                if (!FlushInstructionCache(Process.GetCurrentProcess().Handle, getThreadedDataHandle.AddrOfPinnedObject(), (UIntPtr)GetThreadedDataASM.Length))
                return;

            GetThreadedDataFn = Marshal.GetDelegateForFunctionPointer<GetThreadedDataDg>(getThreadedDataHandle.AddrOfPinnedObject());
        }

        [HandleAttribute("GetThreadedData", attribFnType.Status)]
        public void GetThreadedDataStatus(bool status)
        {
        }

        private UInt64 GetThreadedOffset()
        {
            UInt64 threadedData = GetThreadedDataFn();
            if (threadedData != 0)
            {
                int offset = (*(int*)(BaseAddress + 0x21B0AF4)); //20DA974
                threadedData = *(UInt64*)(threadedData + (UInt64)(offset * 8));
                threadedData = *(UInt64*)(threadedData + 0x250);
                *(uint*)(threadedData + 0x8) = *(uint*)(threadedData + 8) & 0xfff80000;
            }
            return threadedData;
        }





        //----
        // DisableLeftClick
        //----
        private delegate void DisableLeftClickDg(void** a, byte* b, bool c);
        private Hook<DisableLeftClickDg> DisableLeftClickHook;

        [HandleAttribute("DisableLeftClick", attribFnType.Initalize)]
        public void DisableLeftClickInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 74 16");
            PluginLog.Log($"DisableLeftClick: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            DisableLeftClickHook = Hook<DisableLeftClickDg>.FromAddress(tmpAddress, DisableLeftClickFn);
        }

        [HandleAttribute("DisableLeftClick", attribFnType.Status)]
        public void DisableLeftClickStatus(bool status)
        {
            if (status == true)
                DisableLeftClickHook.Enable();
            else
                DisableLeftClickHook.Disable();
        }

        private void DisableLeftClickFn(void** a, byte* b, bool c)
        {
            if (b != null && b == a[16]) DisableLeftClickHook.Original(a, b, c);
        }




        //----
        // DisableRightClick
        //----
        private delegate void DisableRightClickDg(void** a, byte* b, bool c);
        private Hook<DisableRightClickDg> DisableRightClickHook;

        [HandleAttribute("DisableRightClick", attribFnType.Initalize)]
        public void DisableRightClickInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? 48 85 C0 74 1B");
            PluginLog.Log($"DisableRightClick: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            DisableRightClickHook = Hook<DisableRightClickDg>.FromAddress(tmpAddress, DisableRightClickFn);
        }

        [HandleAttribute("DisableRightClick", attribFnType.Status)]
        public void DisableRightClickStatus(bool status)
        {
            if (status == true)
                DisableRightClickHook.Enable();
            else
                DisableRightClickHook.Disable();
        }

        private void DisableRightClickFn(void** a, byte* b, bool c)
        {
            if (b != null && b == a[16]) DisableRightClickHook.Original(a, b, c);
        }




        //----
        // SetRenderTarget
        //----
        private delegate void SetRenderTargetDg(UInt64 a, UInt64 b, Structures.Texture** c, UInt64 d, UInt64 e, UInt64 f);
        private SetRenderTargetDg SetRenderTargetFn;

        [HandleAttribute("SetRenderTarget", attribFnType.Initalize)]
        public void SetRenderTargetInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 40 38 BC 24 00 02 00 00");
            PluginLog.Log($"SetRenderTarget: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            SetRenderTargetFn = Marshal.GetDelegateForFunctionPointer<SetRenderTargetDg>(tmpAddress);
        }

        [HandleAttribute("SetRenderTarget", attribFnType.Status)]
        public void SetRenderTargetStatus(bool status)
        {
        }




        //----
        // AllocateQueueMemory
        //----
        private delegate UInt64 AllocateQueueMemoryDg(UInt64 a, UInt64 b);
        private AllocateQueueMemoryDg AllocateQueueMemmoryFn;

        [HandleAttribute("AllocateQueueMemory", attribFnType.Initalize)]
        public void AllocateQueueMemoryInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 85 C0 74 ?? C7 00 04 00 00 00");
            PluginLog.Log($"AllocateQueueMemmory: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            AllocateQueueMemmoryFn = Marshal.GetDelegateForFunctionPointer<AllocateQueueMemoryDg>(tmpAddress);
        }

        [HandleAttribute("AllocateQueueMemory", attribFnType.Status)]
        public void AllocateQueueMemoryStatus(bool status)
        {
        }




        //----
        // Pushback
        //----
        private delegate void PushbackDg(UInt64 a, UInt64 b, UInt64 c);
        private PushbackDg PushbackFn;

        [HandleAttribute("Pushback", attribFnType.Initalize)]
        public void PushbackInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? EB ?? 8B 87 6C 04 00 00");
            PluginLog.Log($"Pushback: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            PushbackFn = Marshal.GetDelegateForFunctionPointer<PushbackDg>(tmpAddress);
        }

        [HandleAttribute("Pushback", attribFnType.Status)]
        public void PushbackStatus(bool status)
        {
        }




        //----
        // PushbackUI
        //----
        private delegate void PushbackUIDg(UInt64 a, UInt64 b);
        private Hook<PushbackUIDg> PushbackUIHook;

        [HandleAttribute("PushbackUI", attribFnType.Initalize)]
        public void PushbackUIInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? EB 05 E8 ?? ?? ?? ?? 48 8B 5C 24 78");
            PluginLog.Log($"PushbackUI: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            PushbackUIHook = Hook<PushbackUIDg>.FromAddress(tmpAddress, PushbackUIFn);
        }

        [HandleAttribute("PushbackUI", attribFnType.Status)]
        public void PushbackUIStatus(bool status)
        {
            if (status == true)
                PushbackUIHook.Enable();
            else
                PushbackUIHook.Disable();
        }
        private void PushbackUIFn(UInt64 a, UInt64 b)
        {
            Structures.Texture* texture = GetUIRenderTexture(curEye);
            UInt64 threadedOffset = GetThreadedOffset();
            SetRenderTargetFn(threadedOffset, 1, &texture, 0, 0, 0);

            //UInt64 tAddr = *(UInt64*)(renderTargetManagerAddr + rndrOffset);
            //Structures.Texture* texture1 = (Structures.Texture*)(tAddr);
            //SetRenderTargetFn(threadedOffset, 1, &texture1, 0, 0, 0);

            AddClearCommand();

            overrideFromParent.Push(true);
            PushbackUIHook.Original(a, b);
            overrideFromParent.Pop();
        }



        //----
        // NEED TO FIX SIG
        //----
        // AddonNamePlate OnRequestedUpdate
        //----
        private delegate void OnRequestedUpdateDg(UInt64 a, UInt64 b, UInt64 c);
        private Hook<OnRequestedUpdateDg> OnRequestedUpdateHook;

        [HandleAttribute("OnRequestedUpdate", attribFnType.Initalize)]
        public void OnRequestedUpdateInit(bool status)
        {
            IntPtr tmpAddress = (IntPtr)BaseAddress + 0xF2BC60; //  DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 8B 83 90 1A 01 00");
            PluginLog.Log($"OnRequestedUpdate: {tmpAddress:X}");
            OnRequestedUpdateHook = Hook<OnRequestedUpdateDg>.FromAddress(tmpAddress, OnRequestedUpdateFn);
        }

        [HandleAttribute("OnRequestedUpdate", attribFnType.Status)]
        public void OnRequestedUpdateStatus(bool status)
        {
            if (status == true)
                OnRequestedUpdateHook.Enable();
            else
                OnRequestedUpdateHook.Disable();
        }

        void OnRequestedUpdateFn(UInt64 a, UInt64 b, UInt64 c)
        {
            UInt64 globalScaleAddress = (BaseAddress + 0x1FE1A78);
            float globalScale = *(float*)globalScaleAddress;
            *(float*)globalScaleAddress = 1;
            OnRequestedUpdateHook.Original(a, b, c);
            *(float*)globalScaleAddress = globalScale;
        }




        //----
        // DXGIPresent
        //----
        private delegate void DXGIPresentDg(UInt64 a, UInt64 b);
        private Hook<DXGIPresentDg> DXGIPresentHook;

        [HandleAttribute("DXGIPresent", attribFnType.Initalize)]
        public void DXGIPresentInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? C6 47 79 00 48 8B 8F");
            PluginLog.Log($"DXGIPresent: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            DXGIPresentHook = Hook<DXGIPresentDg>.FromAddress(tmpAddress, DXGIPresentFn);
        }

        [HandleAttribute("DXGIPresent", attribFnType.Status)]
        public void DXGIPresentStatus(bool status)
        {
            if (status == true)
                DXGIPresentHook.Enable();
            else
                DXGIPresentHook.Disable();
        }

        private void DXGIPresentFn(UInt64 a, UInt64 b)
        {
            if (forceFloatingScreen)
            {
                RenderUI(false, false);
                DXGIPresentHook.Original(a, b);
                RenderFloatingScreen();
                RenderVR();
            }
            else
            {
                RenderUI(enableVR, enableFloatingHUD);
                DXGIPresentHook.Original(a, b);
                SetTexture();
                RenderVR();
            }
        }



        //----
        // NEED TO FIX SIG
        //----
        // CameraManager Setup??
        //----
        private delegate void CamManagerSetMatrixDg(UInt64 a);
        private Hook<CamManagerSetMatrixDg> CamManagerSetMatrixHook;

        [HandleAttribute("CamManagerSetMatrix", attribFnType.Initalize)]
        public void CamManagerSetMatrixInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("E9 74 0A 3D 00");
            PluginLog.Log($"CamManagerSetMatrix: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            CamManagerSetMatrixHook = Hook<CamManagerSetMatrixDg>.FromAddress(tmpAddress, CamManagerSetMatrixFn);
        }

        [HandleAttribute("CamManagerSetMatrix", attribFnType.Status)]
        public void CamManagerSetMatrixStatus(bool status)
        {
            if (status == true)
                CamManagerSetMatrixHook.Enable();
            else
                CamManagerSetMatrixHook.Disable();
        }

        private void CamManagerSetMatrixFn(UInt64 a)
        {
            overrideFromParent.Push(true);
            CamManagerSetMatrixHook.Original(a);
            overrideFromParent.Pop();
        }




        //----
        // SetUIProj
        //----
        private delegate void SetUIProjDg(UInt64 a, UInt64 b);
        private Hook<SetUIProjDg> SetUIProjHook;

        [HandleAttribute("SetUIProj", attribFnType.Initalize)]
        public void SetUIProjInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 8B 0D ?? ?? ?? ?? 48 8D 94 24");
            PluginLog.Log($"SetUIProj: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            SetUIProjHook = Hook<SetUIProjDg>.FromAddress(tmpAddress, SetUIProjFn);
        }

        [HandleAttribute("SetUIProj", attribFnType.Status)]
        public void SetUIProjStatus(bool status)
        {
            if (status == true)
                SetUIProjHook.Enable();
            else
                SetUIProjHook.Disable();
        }

        private void SetUIProjFn(UInt64 a, UInt64 b)
        {
            bool overrideFn = (overrideFromParent.Count == 0) ? false : overrideFromParent.Peek();
            if (overrideFn)
            {
                Structures.Texture* texture = GetUIRenderTexture(curEye);
                UInt64 threadedOffset = GetThreadedOffset();
                SetRenderTargetFn(threadedOffset, 1, &texture, 0, 0, 0);
            }

            SetUIProjHook.Original(a, b);
        }




        //----
        // Camera CalculateViewMatrix
        //----
        private delegate void CalculateViewMatrixDg(UInt64 a);
        private Hook<CalculateViewMatrixDg> CalculateViewMatrixHook;

        [HandleAttribute("CalculateViewMatrix", attribFnType.Initalize)]
        public void CalculateViewMatrixInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 8B 83 EC 00 00 00 D1 E8 A8 01 74 1B");
            PluginLog.Log($"CalculateViewMatrix: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            CalculateViewMatrixHook = Hook<CalculateViewMatrixDg>.FromAddress(tmpAddress, CalculateViewMatrixFn);
        }

        [HandleAttribute("CalculateViewMatrix", attribFnType.Status)]
        public void CalculateViewMatrixStatus(bool status)
        {
            if (status == true)
                CalculateViewMatrixHook.Enable();
            else
                CalculateViewMatrixHook.Disable();
        }
        
        private void CalculateViewMatrixFn(UInt64 a)
        {
            IntPtr gameViewMatrixAddr = (IntPtr)(a + 0xA0);
            SafeMemory.Write<Matrix4x4>(gameViewMatrixAddr, Matrix4x4.Identity);
            CalculateViewMatrixHook.Original(a);
            SafeMemory.Read<Matrix4x4>(gameViewMatrixAddr, out curViewMatrix);

            if (enableVR && enableFloatingHUD && forceFloatingScreen == false)
            {
                Matrix4x4 gameViewMatrix = new Matrix4x4();
                Matrix4x4 hmdMatrix = GetFramePose(poseType.hmdPosition, -1);
                Matrix4x4.Invert(hmdMatrix, out hmdMatrix);

                Matrix4x4 horizonLockMatrix = Matrix4x4.Identity;
                if (camInst != null && horizonLock)
                {
                    GameCamera* gameCamera = (GameCamera*)(camInst->CameraOffset + (camInst->CameraIndex * 8));
                    if (gameCamera != null)
                        horizonLockMatrix = Matrix4x4.CreateFromAxisAngle(new Vector3(1, 0, 0), gameCamera->CurrentVRotation);

                    //PluginLog.Log($"gameCamera {gameCamera->X} {gameCamera->Y} {gameCamera->Z}");
                }
                horizonLockMatrix.M41 = offsetAmount.X;
                horizonLockMatrix.M42 = offsetAmount.Y;

                Matrix4x4 invGameViewMatrixAddr;
                Matrix4x4 lhcMatrix = GetFramePose(poseType.LeftHand, -1);
                
                Vector3 angles = GetAngles(lhcMatrix);
                Matrix4x4 revOnward = Matrix4x4.CreateFromAxisAngle(new Vector3(0, 1, 0), angles.Y);
                Matrix4x4 zoom = Matrix4x4.CreateTranslation(0, 0, -cameraZoom);
                revOnward = revOnward * zoom;
                Matrix4x4.Invert(revOnward, out revOnward);
                if (doOnward == false)
                    revOnward = Matrix4x4.Identity;

                
                hmdMatrix = hmdMatrix * eyeOffsetMatrix[curEye];
                SafeMemory.Read<Matrix4x4>(gameViewMatrixAddr, out gameViewMatrix);
                gameViewMatrix = gameViewMatrix * horizonLockMatrix * revOnward * hmdMatrix;
                SafeMemory.Write<Matrix4x4>(gameViewMatrixAddr, gameViewMatrix);
            }
        }




        //----
        // Camera UpdateRotation
        //----
        private delegate void UpdateRotationDg(UInt64 a);
        private Hook<UpdateRotationDg> UpdateRotationHook;

        [HandleAttribute("UpdateRotation", attribFnType.Initalize)]
        public void UpdateRotationInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F B6 93 20 02 00 00 48 8B CB");
            PluginLog.Log($"UpdateRotation: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            UpdateRotationHook = Hook<UpdateRotationDg>.FromAddress(tmpAddress, UpdateRotationFn);
        }

        [HandleAttribute("UpdateRotation", attribFnType.Status)]
        public void UpdateRotationStatus(bool status)
        {
            if (status == true)
                UpdateRotationHook.Enable();
            else
                UpdateRotationHook.Disable();
        }

        private void UpdateRotationFn(UInt64 a)
        {
            GameCamera* gameCamera = (GameCamera*)(a + 0x10);
            if (gameCamera != null && forceFloatingScreen == false)
            {
                Matrix4x4 lhcMatrix = GetFramePose(poseType.LeftHand, -1);
                //Matrix4x4.Invert(lhcMatrix, out lhcMatrix);
                Vector3 angles = GetAngles(lhcMatrix);
                angles.Y *= -1;

                onwardDiff = angles - onwardAngle;
                onwardAngle = angles;
                
                if (horizontalLock)
                    gameCamera->HRotationThisFrame2 = 0;
                if (verticalLock)
                    gameCamera->VRotationThisFrame2 = 0;
                if (doOnward == false)
                    onwardDiff.Y = 0;

                float curH = gameCamera->CurrentHRotation;
                float curV = gameCamera->CurrentVRotation;
                //gameCamera->HRotationThisFrame1 += onwardDiff.Y + rotateAmount.X;
                gameCamera->HRotationThisFrame2 += onwardDiff.Y + rotateAmount.X;
                //gameCamera->VRotationThisFrame1 += onwardDiff.X + rotateAmount.Y;
                //gameCamera->VRotationThisFrame2 += onwardDiff.X + rotateAmount.Y;
                gameCamera->VRotationThisFrame2 += rotateAmount.Y;
                rotateAmount.X = 0;
                rotateAmount.Y = 0;

                cameraZoom = gameCamera->CurrentZoom;
                UpdateRotationHook.Original(a);
            }
            else
            {
                UpdateRotationHook.Original(a);
            }
        }




        //----
        // MakeProjectionMatrix2
        //----
        private delegate float* MakeProjectionMatrix2Dg(UInt64 a, float b, float c, float d, float e);
        private Hook<MakeProjectionMatrix2Dg> MakeProjectionMatrix2Hook;

        [HandleAttribute("MakeProjectionMatrix2", attribFnType.Initalize)]
        public void MakeProjectionMatrix2Init(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 4C 8B 2D ?? ?? ?? ?? 41 0F 28 C2");
            PluginLog.Log($"MakeProjectionMatrix2: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            MakeProjectionMatrix2Hook = Hook<MakeProjectionMatrix2Dg>.FromAddress(tmpAddress, MakeProjectionMatrix2Fn);
        }

        [HandleAttribute("MakeProjectionMatrix2", attribFnType.Status)]
        public void MakeProjectionMatrix2Status(bool status)
        {
            if (status == true)
                MakeProjectionMatrix2Hook.Enable();
            else
                MakeProjectionMatrix2Hook.Disable();
        }

        private float* MakeProjectionMatrix2Fn(UInt64 a, float b, float c, float d, float e)
        {
            bool overrideMatrix = (overrideFromParent.Count == 0) ? false : overrideFromParent.Peek();
            float* retVal = MakeProjectionMatrix2Hook.Original(a, b, c, d, e);
            if (enableVR && enableFloatingHUD && overrideMatrix && forceFloatingScreen == false)
            {
                SafeMemory.Read<float>((IntPtr)(a + 0x38), out gameProjectionMatrix[curEye].M43);
                SafeMemory.Write<Matrix4x4>((IntPtr)retVal, gameProjectionMatrix[curEye]);
            }
            return retVal;
        }




        //----
        // RenderThreadSetRenderTarget
        //----
        private delegate void RenderThreadSetRenderTargetDg(UInt64 a, UInt64 b);
        private Hook<RenderThreadSetRenderTargetDg> RenderThreadSetRenderTargetHook;

        [HandleAttribute("RenderThreadSetRenderTarget", attribFnType.Initalize)]
        public void RenderThreadSetRenderTargetInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? F3 41 0F 10 5A 18");
            PluginLog.Log($"RenderThreadSetRenderTarget: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            RenderThreadSetRenderTargetHook = Hook<RenderThreadSetRenderTargetDg>.FromAddress(tmpAddress, RenderThreadSetRenderTargetFn);
        }

        [HandleAttribute("RenderThreadSetRenderTarget", attribFnType.Status)]
        public void RenderThreadSetRenderTargetStatus(bool status)
        {
            if (status == true)
                RenderThreadSetRenderTargetHook.Enable();
            else
                RenderThreadSetRenderTargetHook.Disable();
        }

        private void RenderThreadSetRenderTargetFn(UInt64 a, UInt64 b)
        {
            if ((b + 0x8) != 0)
            {
                Structures.Texture* rendTrg = *(Structures.Texture**)(b + 0x8);
                if (rendTrg->uk5 == 0x990F0F0)
                    SetThreadedEye(0);
                else if (rendTrg->uk5 == 0x990F0F0F)
                    SetThreadedEye(1);
            }
            RenderThreadSetRenderTargetHook.Original(a, b);
        }




        //----
        // NamePlateDraw
        //----
        private delegate void NamePlateDrawDg(AddonNamePlate* a);
        private Hook<NamePlateDrawDg> NamePlateDrawHook;

        [HandleAttribute("NamePlateDraw", attribFnType.Initalize)]
        public void NamePlateDrawInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("0F B7 81 ?? ?? ?? ?? 4C 8B C1 66 C1 E0 06");
            PluginLog.Log($"NamePlateDraw: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            NamePlateDrawHook = Hook<NamePlateDrawDg>.FromAddress(tmpAddress, NamePlateDrawFn);
        }

        [HandleAttribute("NamePlateDraw", attribFnType.Status)]
        public void NamePlateDrawStatus(bool status)
        {
            if (status == true)
                NamePlateDrawHook.Enable();
            else
                NamePlateDrawHook.Disable();
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
                    targetAddon->Hide(true);
                    //targetAddon->RootNode->SetUseDepthBasedPriority(true);
                }

                for (byte i = 0; i < NamePlateCount; i++)
                {
                    AddonNamePlate.NamePlateObject* npObj = &a->NamePlateObjectArray[i];
                    AtkComponentBase* npComponent = npObj->RootNode->Component;

                    for (int j = 0; j < npComponent->UldManager.NodeListCount; j++)
                    {
                        AtkResNode* child = npComponent->UldManager.NodeList[j];
                        child->SetUseDepthBasedPriority(true);
                    }

                    npObj->RootNode->Component->UldManager.UpdateDrawNodeList();
                }
            }

            NamePlateDrawHook.Original(a);
        }




        //----
        // LoadCharacter
        //----
        private delegate UInt64 LoadCharacterDg(UInt64 a, UInt64 b, UInt64 c, UInt64 d, UInt64 e, UInt64 f);
        private Hook<LoadCharacterDg> LoadCharacterHook;

        [HandleAttribute("LoadCharacter", attribFnType.Initalize)]
        public void LoadCharacterInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("48 89 5C 24 10 48 89 6C 24 18 56 57 41 57 48 83 EC 30 48 8B F9 4D 8B F9 8B CA 49 8B D8 8B EA");
            PluginLog.Log($"LoadCharacter: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            //LoadCharacterFn = Marshal.GetDelegateForFunctionPointer<LoadCharacterDg>(LoadCharacterAddr);
            LoadCharacterHook = Hook<LoadCharacterDg>.FromAddress(tmpAddress, LoadCharacterFn);
        }

        [HandleAttribute("LoadCharacter", attribFnType.Status)]
        public void LoadCharacterStatus(bool status)
        {
            if (status == true)
                LoadCharacterHook.Enable();
            else
                LoadCharacterHook.Disable();
        }

        private UInt64 LoadCharacterFn(UInt64 a, UInt64 b, UInt64 c, UInt64 d, UInt64 e, UInt64 f)
        {
            //head 279 (EmpNewHeadgear)
            //ears  53 (EmpNewEaring)
            //CharaEquipSlotData.cs

            //head gear - 6121 makes your head go inivisble - model head id to 256
            //variant - 256
            PlayerCharacter? player = DalamudApi.ClientState.LocalPlayer;
            if (player != null && (UInt64)player.Address == a)
            {
                //CharCustData* cData = (CharCustData*)c;
                CharCustData cData = new CharCustData(c);
                CharEquipData eData = new CharEquipData(d);
                //Dalamud.Game.ClientState.Objects.Enums.CustomizeIndex
                //PluginLog.Log($"LoadCharacter {a:X} {b:X} {c:X} {d:X} {e:X} {f:X}");
                //cData.FaceType = 69;
                //cData.HairStyle = 69;
                //PluginLog.Log($"LoadCharacter {cData[0]} {cData[1]} {cData[2]} {cData[3]} {cData[4]} {cData[5]}");
                //PluginLog.Log($"LoadCharacter {cData.Race} {cData.Gender} {cData.ModelType} {cData.Height} {cData.Tribe} {cData.FaceType}");
                //PluginLog.Log($"LoadCharacter {eData.Head} {eData.Body} {eData.Hands} {eData.Legs} {eData.Feet}");
                //PluginLog.Log($"LoadCharacter {cData->Race} {cData->Gender} {cData->ModelType} {cData->Height} {cData->Tribe} {cData->FaceType}");

                //*(ushort*)(d + 0) = 6121;
                //*(byte*)(d + 0 + 2) = 255;
            }
            //PluginLog.Log($"LoadCharacter {a:X} {b:X} {c:X} {d:X} {e:X} {f:X}");
            return LoadCharacterHook.Original(a, b, c, d, e, f);
        }





        //----
        // Input.GetAnalogueValue
        //----
        private delegate Int32 GetAnalogueValueDg(UInt64 a, UInt64 b);
        private Hook<GetAnalogueValueDg> GetAnalogueValueHook;

        [HandleAttribute("GetAnalogueValue", attribFnType.Initalize)]
        public void GetAnalogueValueInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 66 44 0F 6E C3");
            PluginLog.Log($"GetAnalogueValue: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            GetAnalogueValueHook = Hook<GetAnalogueValueDg>.FromAddress(tmpAddress, GetAnalogueValueFn);
        }

        [HandleAttribute("GetAnalogueValue", attribFnType.Status)]
        public void GetAnalogueValueStatus(bool status)
        {
            if (status == true)
                GetAnalogueValueHook.Enable();
            else
                GetAnalogueValueHook.Disable();
        }



        // 0 mouse left right
        // 1 mouse up down
        // 3 left | left right
        // 4 left | up down
        // 5 right | left right
        // 6 right | up down

        private Int32 GetAnalogueValueFn(UInt64 a, UInt64 b)
        {
            Int32 retVal = GetAnalogueValueHook.Original(a, b);

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
                        if (horizontalLock)
                        {
                            if (MathF.Abs(retVal) > 75 && rightHorizontalCenter)
                            {
                                rightHorizontalCenter = false;
                                rotateAmount.X -= snapRotateAmount.X * MathF.Sign(retVal);
                            }
                            retVal = 0;
                        }
                        break;
                    case 6:
                        //PluginLog.Log($"GetAnalogueValueFn: {retVal}");
                        if (MathF.Abs(retVal) >= 0 && MathF.Abs(retVal) < 15) rightVerticalCenter = true;
                        if (verticalLock)
                        {
                            if (MathF.Abs(retVal) > 75 && rightVerticalCenter)
                            {
                                rightVerticalCenter = false;
                                rotateAmount.Y -= snapRotateAmount.Y * MathF.Sign(retVal);
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
        //----
        private delegate void ControllerInputDg(UInt64 a, UInt64 b, uint c);
        private Hook<ControllerInputDg> ControllerInputHook;

        [HandleAttribute("ControllerInput", attribFnType.Initalize)]
        public void ControllerInputInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 41 8B 86 3C 04 00 00");
            PluginLog.Log($"ControllerInput: {tmpAddress:X}");
            ControllerInputHook = Hook<ControllerInputDg>.FromAddress(tmpAddress, ControllerInputFn);
        }

        [HandleAttribute("ControllerInput", attribFnType.Status)]
        public void ControllerInputStatus(bool status)
        {
            if (status == true)
                ControllerInputHook.Enable();
            else
                ControllerInputHook.Disable();
        }

        public void ControllerInputFn(UInt64 a, UInt64 b, uint c)
        {
            UInt64 controllerBase = *(UInt64*)(a + 0x70);
            UInt64 controllerIndex = *(byte*)(a + 0x434);

            UInt64 controllerAddress = controllerBase + 0x30 + ((controllerIndex * 0x1E6) * 4);
            XBoxButtonOffsets* offsets = (XBoxButtonOffsets*)((controllerIndex * 0x798) + controllerBase);

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

            ControllerInputHook.Original(a, b, c);
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
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

        const int MOUSEEVENTF_LEFTDOWN = 0x02;
        const int MOUSEEVENTF_LEFTUP = 0x04;
        const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        const int MOUSEEVENTF_RIGHTUP = 0x10;

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

            if (analog.x < 0)
                xboxStatus.left_stick_left.Set(true, MathF.Abs(analog.x));
            else if (analog.x > 0)
                xboxStatus.left_stick_right.Set(true, MathF.Abs(analog.x));

            if (analog.y > 0)
                xboxStatus.left_stick_up.Set(true, MathF.Abs(analog.y));
            else if (analog.y < 0)
                xboxStatus.left_stick_down.Set(true, MathF.Abs(analog.y));
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

            if (analog.y > 0)
                xboxStatus.right_stick_up.Set(true, MathF.Abs(analog.y));
            else if (analog.y < 0)
                xboxStatus.right_stick_down.Set(true, MathF.Abs(analog.y));
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
                Recenter();
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
            if (digital.bState == true && inputState[ActionButtonLayout.xbox_button_y] == false)
            {
                inputState[ActionButtonLayout.xbox_button_y] = true;
                xboxStatus.button_y.Set(true, 1.0f);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.xbox_button_y] == true)
            {
                inputState[ActionButtonLayout.xbox_button_y] = false;
                xboxStatus.button_y.Set();
            }
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_button_x)]
        public void inputXBoxButtonX(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.xbox_button_x] == false)
            {
                inputState[ActionButtonLayout.xbox_button_x] = true;
                xboxStatus.button_x.Set(true, 1.0f);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.xbox_button_x] == true)
            {
                inputState[ActionButtonLayout.xbox_button_x] = false;
                xboxStatus.button_x.Set();
            }
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_button_a)]
        public void inputXBoxButtonA(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.xbox_button_a] == false)
            {
                inputState[ActionButtonLayout.xbox_button_a] = true;
                xboxStatus.button_a.Set(true, 1.0f);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.xbox_button_a] == true)
            {
                inputState[ActionButtonLayout.xbox_button_a] = false;
                xboxStatus.button_a.Set();
            }
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_button_b)]
        public void inputXBoxButtonB(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.xbox_button_b] == false)
            {
                inputState[ActionButtonLayout.xbox_button_b] = true;
                xboxStatus.button_b.Set(true, 1.0f);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.xbox_button_b] == true)
            {
                inputState[ActionButtonLayout.xbox_button_b] = false;
                xboxStatus.button_b.Set();
            }
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_left_trigger)]
        public void inputXBoxLeftTrigger(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            xboxStatus.left_trigger.Set();
            if (analog.x > 0)
                xboxStatus.left_trigger.Set(true, analog.x);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_left_bumper)]
        public void inputXBoxLeftBumper(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            xboxStatus.left_bumper.Set();
            if (analog.x > 0)
                xboxStatus.left_bumper.Set(true, analog.x);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_left_stick_click)]
        public void inputXBoxLeftStickClick(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.xbox_left_stick_click] == false)
            {
                inputState[ActionButtonLayout.xbox_left_stick_click] = true;
                xboxStatus.left_stick_click.Set(true, 1.0f);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.xbox_left_stick_click] == true)
            {
                inputState[ActionButtonLayout.xbox_left_stick_click] = false;
                xboxStatus.left_stick_click.Set();
            }
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_right_trigger)]
        public void inputXBoxRightTrigger(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            xboxStatus.right_trigger.Set();
            if (analog.x > 0)
                xboxStatus.right_trigger.Set(true, analog.x);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_right_bumper)]
        public void inputXBoxRightBumper(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            xboxStatus.right_bumper.Set();
            if (analog.x > 0)
                xboxStatus.right_bumper.Set(true, analog.x);
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_right_stick_click)]
        public void inputXBoxRightStickClick(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.xbox_right_stick_click] == false)
            {
                inputState[ActionButtonLayout.xbox_right_stick_click] = true;
                xboxStatus.right_stick_click.Set(true, 1.0f);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.xbox_right_stick_click] == true)
            {
                inputState[ActionButtonLayout.xbox_right_stick_click] = false;
                xboxStatus.right_stick_click.Set();
            }
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_pad_up)]
        public void inputXBoxPadUp(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.xbox_pad_up] == false)
            {
                inputState[ActionButtonLayout.xbox_pad_up] = true;
                xboxStatus.dpad_up.Set(true, 1.0f);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.xbox_pad_up] == true)
            {
                inputState[ActionButtonLayout.xbox_pad_up] = false;
                xboxStatus.dpad_up.Set();
            }
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_pad_down)]
        public void inputXBoxPadDown(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.xbox_pad_down] == false)
            {
                inputState[ActionButtonLayout.xbox_pad_down] = true;
                xboxStatus.dpad_down.Set(true, 1.0f);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.xbox_pad_down] == true)
            {
                inputState[ActionButtonLayout.xbox_pad_down] = false;
                xboxStatus.dpad_down.Set();
            }
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_pad_left)]
        public void inputXBoxPadLeft(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.xbox_pad_left] == false)
            {
                inputState[ActionButtonLayout.xbox_pad_left] = true;
                xboxStatus.dpad_left.Set(true, 1.0f);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.xbox_pad_left] == true)
            {
                inputState[ActionButtonLayout.xbox_pad_left] = false;
                xboxStatus.dpad_left.Set();
            }
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_pad_right)]
        public void inputXBoxPadRight(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.xbox_pad_right] == false)
            {
                inputState[ActionButtonLayout.xbox_pad_right] = true;
                xboxStatus.dpad_right.Set(true, 1.0f);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.xbox_pad_right] == true)
            {
                inputState[ActionButtonLayout.xbox_pad_right] = false;
                xboxStatus.dpad_right.Set();
            }
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_start)]
        public void inputXBoxStart(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.xbox_start] == false)
            {
                inputState[ActionButtonLayout.xbox_start] = true;
                xboxStatus.start.Set(true, 1.0f);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.xbox_start] == true)
            {
                inputState[ActionButtonLayout.xbox_start] = false;
                xboxStatus.start.Set();
            }
        }

        [HandleInputAttribute(ActionButtonLayout.xbox_select)]
        public void inputXBoxSelect(InputAnalogActionData analog, InputDigitalActionData digital)
        {
            if (digital.bState == true && inputState[ActionButtonLayout.xbox_select] == false)
            {
                inputState[ActionButtonLayout.xbox_select] = true;
                xboxStatus.select.Set(true, 1.0f);
            }
            else if (digital.bState == false && inputState[ActionButtonLayout.xbox_select] == true)
            {
                inputState[ActionButtonLayout.xbox_select] = false;
                xboxStatus.select.Set();
            }
        }
    }
}