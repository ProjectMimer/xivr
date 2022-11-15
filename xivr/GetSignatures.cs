using System;
using Dalamud;

namespace xivr
{
    interface IGetSignatures
    {
        IntPtr CameraManagerInstance { get; }
        IntPtr GlobalScale { get; }

        IntPtr DisableLeftClick { get; }
        IntPtr DisableRightClick { get; }
        IntPtr SetRenderTarget { get; }
        IntPtr AllocateQueueMemory { get; }
        IntPtr Pushback { get; }
        IntPtr PushbackUI { get; }
        IntPtr OnRequestedUpdate { get; }
        IntPtr DXGIPresent { get; }
        IntPtr CamManagerSetMatrix { get; }
        IntPtr CSUpdateConstBuf { get; }
        IntPtr SetUIProj { get; }
        IntPtr CalculateViewMatrix { get; }
        IntPtr UpdateRotation { get; }
        IntPtr MakeProjectionMatrix2 { get; }
        IntPtr CSMakeProjectionMatrix { get; }
        IntPtr RenderThreadSetRenderTarget { get; }
        IntPtr NamePlateDraw { get; }
        IntPtr LoadCharacter { get; }
        IntPtr GetAnalogueValue { get; }
        IntPtr ControllerInput { get; }

    }

    struct SigAddresses : IGetSignatures
    {
        public IntPtr CameraManagerInstance { get {
                return DalamudApi.SigScanner.GetStaticAddressFromSig("48 8B 05 ?? ?? ?? ?? 83 78 50 00 75 22");
            } }

        public IntPtr GlobalScale { get {
                return DalamudApi.SigScanner.GetStaticAddressFromSig("F3 0F 10 0D ?? ?? ?? ?? F3 0F 10 40 4C");
            } }

        public IntPtr DisableLeftClick { get {
                return DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 74 16");
            } }
        public IntPtr DisableRightClick { get {
                return DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? 48 85 C0 74 1B");
            } }
        public IntPtr SetRenderTarget { get { 
                return DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 40 38 BC 24 00 02 00 00");
            } }
        public IntPtr AllocateQueueMemory { get {
                return DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 85 C0 74 ?? C7 00 04 00 00 00");
            } }
        public IntPtr Pushback { get {
                return DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? EB ?? 8B 87 6C 04 00 00");
            } }
        public IntPtr PushbackUI {
            get {
                return DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? EB 05 E8 ?? ?? ?? ?? 48 8B 5C 24 78");
            } }
        public IntPtr OnRequestedUpdate {
            get {
                return IntPtr.Zero;
            } }
        public IntPtr DXGIPresent {
            get {
                return DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? C6 47 79 00 48 8B 8F");
            } }
        public IntPtr CamManagerSetMatrix {
            get {
                return DalamudApi.SigScanner.ScanText("E9 64 0B 3D 00");
            } }
        public IntPtr CSUpdateConstBuf {
            get {
                return IntPtr.Zero;
            } }
        public IntPtr SetUIProj {
            get {
                return DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 8B 0D ?? ?? ?? ?? 48 8D 94 24");
            } }
        public IntPtr CalculateViewMatrix { get {
                return DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 8B 83 EC 00 00 00 D1 E8 A8 01 74 1B");
            } }
        public IntPtr UpdateRotation { get {
                return DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F B6 93 20 02 00 00 48 8B CB");
            } }
        public IntPtr MakeProjectionMatrix2 { get {
                return DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 4C 8B 2D ?? ?? ?? ?? 41 0F 28 C2");
            } }
        public IntPtr CSMakeProjectionMatrix { get {
                return DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F 28 46 10 4C 8D 7E 10");
            } }
        public IntPtr RenderThreadSetRenderTarget { get {
                return DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? F3 41 0F 10 5A 18");
            } }
        public IntPtr NamePlateDraw { get {
                return DalamudApi.SigScanner.ScanText("0F B7 81 ?? ?? ?? ?? 4C 8B C1 66 C1 E0 06");
            } }
        public IntPtr LoadCharacter { get {
                return DalamudApi.SigScanner.ScanText("48 89 5C 24 10 48 89 6C 24 18 56 57 41 57 48 83 EC 30 48 8B F9 4D 8B F9 8B CA 49 8B D8 8B EA");
            } }
        public IntPtr GetAnalogueValue { get {
                return DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 66 44 0F 6E C3");
            } }
        public IntPtr ControllerInput { get {
                return DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 41 8B 86 3C 04 00 00");
            } }
    }
    /*
    struct SigAddressesAlternate : IGetSignatures
    {
        public IntPtr DisableLeftClickAddress = IntPtr.Zero;
        public IntPtr DisableLeftClick { get { return DisableLeftClickAddress; } }

        public SigAddressesAlternate()
        {
        }

    }
    */
}
