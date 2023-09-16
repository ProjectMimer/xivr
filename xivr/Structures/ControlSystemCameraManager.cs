using System;
using System.Runtime.InteropServices;

namespace xivr.Structures
{
    [StructLayout(LayoutKind.Explicit, Size = 0x300)]
    public unsafe struct ControlSystemCameraManager
    {
        [FieldOffset(0x0)] public GameCamera* GameCamera;
        [FieldOffset(0x8)] public LowCutCamera* LowCutCamera;
        [FieldOffset(0x10)] public LobbyCamera* LobbyCamera;
        [FieldOffset(0x18)] public Camera3* Camera3;
        [FieldOffset(0x20)] public Camera4* Camera4;

        [FieldOffset(0x48)] public int ActiveCameraIndex;
        [FieldOffset(0x4C)] public int PreviousCameraIndex;

        [FieldOffset(0x60)] public UInt64 CameraBase;
        [FieldOffset(0x70)] public UInt64 ukn1;
        [FieldOffset(0x90)] public UInt64 ukn2;
        [FieldOffset(0x98)] public UInt64 ukn3;

        public RawGameCamera* GetActive(ControlSystemCameraManager* csCameraManager = null)
        {
            if (csCameraManager != null)
            {
                ActiveCameraIndex = csCameraManager->ActiveCameraIndex;
                PreviousCameraIndex = csCameraManager->PreviousCameraIndex;
            }

            if (ActiveCameraIndex == 1)
                return &LowCutCamera->Camera;
            else if (ActiveCameraIndex == 2)
                return &LobbyCamera->Camera;
            else if (ActiveCameraIndex == 3)
                return &Camera3->Camera;
            else if (ActiveCameraIndex == 4)
                return &Camera4->Camera;
            else
                return &GameCamera->Camera;
        }
    }


    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    public unsafe struct CameraBase
    {
        [FieldOffset(0x00)] public UInt64* vtbl;
        [FieldOffset(0x08)] public UInt64 uk1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x2B0)]
    public unsafe struct GameCamera
    {
        [FieldOffset(0x00)] public CameraBase CameraBase;
        [FieldOffset(0x10)] public RawGameCamera Camera;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x2E0)]
    public struct LowCutCamera
    {
        [FieldOffset(0x00)] public CameraBase CameraBase;
        [FieldOffset(0x10)] public RawGameCamera Camera;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x300)]
    public unsafe struct LobbyCamera
    {
        [FieldOffset(0x00)] public CameraBase CameraBase;
        [FieldOffset(0x10)] public RawGameCamera Camera;
        [FieldOffset(0x2F8)] public void* LobbyExcelSheet;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x300)]
    public struct Camera3
    {
        [FieldOffset(0x00)] public CameraBase CameraBase;
        [FieldOffset(0x10)] public RawGameCamera Camera;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x350)]
    public struct Camera4
    {
        [FieldOffset(0x00)] public CameraBase CameraBase;
        [FieldOffset(0x10)] public RawGameCamera Camera;
    }
}
