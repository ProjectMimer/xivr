using System;
using System.Numerics;
using System.Diagnostics;
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
using FFXIVClientStructs.FFXIV.Component.GUI;


namespace xivr
{
    public enum attribFnType
    {
        Initalize = 0,
        Status = 1,
    }

    public delegate void HandleDelegte(bool status);

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


    enum poseType
    {
        Projection = 0,
        EyeOffset = 1,
        hmdPosition = 10
    }

    internal unsafe class xivr_hooks
    {
        protected Dictionary<string, HandleDelegte[]> functionList = new Dictionary<string, HandleDelegte[]>();


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
        private int countTrace = 0;
        private int genericCounter = 5;
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
        private Stack<bool> overrideFromParent = new Stack<bool>();
        private Dictionary<UInt64, float[]> SavedVMinMaxRotation = new Dictionary<UInt64, float[]>();
        
        private int[] runCount = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

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


        public void SetHandles()
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

                    PluginLog.LogError($"Handles {key} {type}");
                }
            }
        }


        public void Initialize()
        {
            BaseAddress = (UInt64)Process.GetCurrentProcess()?.MainModule?.BaseAddress;
            PluginLog.LogError($"Initialize {BaseAddress:X}");

            renderTargetManagerAddr = (UInt64)DalamudApi.SigScanner.GetStaticAddressFromSig("48 8B 05 ?? ?? ?? ?? 49 63 C8");
            if (renderTargetManagerAddr != 0)
            {
                renderTargetManagerAddr = *(UInt64*)renderTargetManagerAddr;
                renderTargetManager = (RenderTargetManager*)(*(UInt64*)renderTargetManagerAddr);
            }
            PluginLog.LogError($"renderTargetManager: {*(UInt64*)renderTargetManagerAddr:X} {(*(UInt64*)renderTargetManagerAddr - BaseAddress):X}");

            IntPtr tmpAddress = DalamudApi.SigScanner.GetStaticAddressFromSig("48 8B 05 ?? ?? ?? ?? 83 78 50 00 75 22");
            PluginLog.LogError($"CameraManagerInstance: {*(UInt64*)tmpAddress:X} {(*(UInt64*)tmpAddress - BaseAddress):X}");
            camInst = (CameraManagerInstance*)(*(UInt64*)tmpAddress);


            SetHandles();

            //----
            // Initalize all sigs
            //----
            foreach (KeyValuePair<string, HandleDelegte[]> attrib in functionList)
            {
                attrib.Value[(int)attribFnType.Initalize](false);
            }

            initalized = true;
        }

        public void Start()
        {
            if (initalized == true && hooksSet == false && VR_IsHmdPresent())
            {
                PluginLog.LogError($"VRInit {(IntPtr)FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance():X}");
                SetDX11((IntPtr)FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance());

                gameProjectionMatrix[0] = Matrix4x4.Transpose(GetFramePose(poseType.Projection, 0));
                gameProjectionMatrix[1] = Matrix4x4.Transpose(GetFramePose(poseType.Projection, 1));
                gameProjectionMatrix[0].M43 *= -1;
                gameProjectionMatrix[1].M43 *= -1;

                Matrix4x4.Invert(GetFramePose(poseType.EyeOffset, 0), out eyeOffsetMatrix[0]);
                Matrix4x4.Invert(GetFramePose(poseType.EyeOffset, 1), out eyeOffsetMatrix[1]);

                //----
                // Enable all hooks
                //----
                foreach (KeyValuePair<string, HandleDelegte[]> attrib in functionList)
                {
                    attrib.Value[(int)attribFnType.Status](true);
                }

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

                gameProjectionMatrix[0] = Matrix4x4.Identity;
                gameProjectionMatrix[1] = Matrix4x4.Identity;
                eyeOffsetMatrix[0] = Matrix4x4.Identity;
                eyeOffsetMatrix[1] = Matrix4x4.Identity;

                UnsetDX11();

                hooksSet = false;
                PrintEcho("Stopping Hooks.");
            }
        }

        public void runTrace()
        {
            countTrace = 5;
        }

        public int PlayerRedrawCount = 0;

        public void Update(Dalamud.Game.Framework framework_)
        {
            if (hooksSet)
            {
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
                    //PluginLog.LogError($"Custom {player.Address:X}");
                    //for (int i = 0; i < 10; i++)
                    //    PluginLog.LogError($"{customPlayer[i]:X}");
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
            if (countTrace > 0)
                countTrace--;

            if (genericCounter > 0)
                genericCounter--;

            
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
            rotateAmount.X = x;
            rotateAmount.Y = y;
        }

        public void SetOffsetAmount(float x, float y)
        {
            if(x != 0) offsetAmount.X = (x / 100.0f) * -1;
            if(y != 0) offsetAmount.Y = (y / 100.0f) * -1;
        }

        public void Draw()
        {

        }

        public void ResizeBuffers()
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
            PluginLog.LogError($"DisableLeftClick: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
            DisableLeftClickHook = Hook<DisableLeftClickDg>.FromAddress(tmpAddress, DisableLeftClickFn);
        }

        [HandleAttribute("DisableLeftClick", attribFnType.Status)]
        public void DisableLeftClickStatus(bool status)
        {
            if(status == true)
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
            PluginLog.LogError($"DisableRightClick: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
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
            PluginLog.LogError($"SetRenderTarget: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
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
            PluginLog.LogError($"AllocateQueueMemmory: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
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
            PluginLog.LogError($"Pushback: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
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
            PluginLog.LogError($"PushbackUI: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
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
        // AddonNamePlate OnRequestedUpdate
        //----
        private delegate void OnRequestedUpdateDg(UInt64 a, UInt64 b, UInt64 c);
        private Hook<OnRequestedUpdateDg> OnRequestedUpdateHook;

        [HandleAttribute("OnRequestedUpdate", attribFnType.Initalize)]
        public void OnRequestedUpdateInit(bool status)
        {
            IntPtr tmpAddress = (IntPtr)BaseAddress + 0xF2BC60; //  DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 8B 83 90 1A 01 00");
            PluginLog.LogError($"OnRequestedUpdate: {tmpAddress:X}");
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
            PluginLog.LogError($"DXGIPresent: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
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
        // CameraManager Setup??
        //----
        private delegate void CamManagerSetMatrixDg(UInt64 a);
        private Hook<CamManagerSetMatrixDg> CamManagerSetMatrixHook;

        [HandleAttribute("CamManagerSetMatrix", attribFnType.Initalize)]
        public void CamManagerSetMatrixInit(bool status)
        {
            IntPtr tmpAddress = DalamudApi.SigScanner.ScanText("E9 74 0A 3D 00");
            PluginLog.LogError($"CamManagerSetMatrix: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
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
            PluginLog.LogError($"SetUIProj: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
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
            PluginLog.LogError($"CalculateViewMatrix: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
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
                }
                horizonLockMatrix.M41 = offsetAmount.X;
                horizonLockMatrix.M42 = offsetAmount.Y;

                SafeMemory.Read<Matrix4x4>(gameViewMatrixAddr, out gameViewMatrix);
                gameViewMatrix = gameViewMatrix * horizonLockMatrix * hmdMatrix * eyeOffsetMatrix[curEye];
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
            PluginLog.LogError($"UpdateRotation: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
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
            if (gameCamera != null)
            {
                if (rotateAmount.X != 0 || rotateAmount.Y != 0)
                {
                    gameCamera->CurrentHRotation += (0.0175f * rotateAmount.X);
                    gameCamera->CurrentVRotation += (0.0175f * rotateAmount.Y);

                    rotateAmount.X = 0;
                    rotateAmount.Y = 0;
                }

                float curH = gameCamera->CurrentHRotation;
                float curV = gameCamera->CurrentVRotation;
                UpdateRotationHook.Original(a);

                if (horizontalLock)
                    gameCamera->CurrentHRotation = curH;
                if (verticalLock)
                    gameCamera->CurrentVRotation = curV;
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
            PluginLog.LogError($"MakeProjectionMatrix2: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
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
            PluginLog.LogError($"RenderThreadSetRenderTarget: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
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
            PluginLog.LogError($"NamePlateDraw: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
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
            PluginLog.LogError($"LoadCharacter: {tmpAddress:X} {((UInt64)tmpAddress - BaseAddress):X}");
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
                //PluginLog.LogError($"LoadCharacter {a:X} {b:X} {c:X} {d:X} {e:X} {f:X}");
                //cData.FaceType = 69;
                //cData.HairStyle = 69;
                //PluginLog.LogError($"LoadCharacter {cData[0]} {cData[1]} {cData[2]} {cData[3]} {cData[4]} {cData[5]}");
                //PluginLog.LogError($"LoadCharacter {cData.Race} {cData.Gender} {cData.ModelType} {cData.Height} {cData.Tribe} {cData.FaceType}");
                //PluginLog.LogError($"LoadCharacter {eData.Head} {eData.Body} {eData.Hands} {eData.Legs} {eData.Feet}");
                //PluginLog.LogError($"LoadCharacter {cData->Race} {cData->Gender} {cData->ModelType} {cData->Height} {cData->Tribe} {cData->FaceType}");

                //*(ushort*)(d + 0) = 6121;
                //*(byte*)(d + 0 + 2) = 255;
            }
            //PluginLog.LogError($"LoadCharacter {a:X} {b:X} {c:X} {d:X} {e:X} {f:X}");
            return LoadCharacterHook.Original(a, b, c, d, e, f);
        }
    }
}
