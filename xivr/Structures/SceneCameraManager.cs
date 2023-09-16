using System.Runtime.InteropServices;

namespace xivr.Structures
{
    [StructLayout(LayoutKind.Explicit, Size = 0xF0)]
    public unsafe struct SceneCameraManager
    {

        [FieldOffset(0x50)] public int CameraIndex;
        [FieldOffset(0x58)] public RawGameCamera* Camera0;
        [FieldOffset(0x60)] public RawGameCamera* Camera1;
        [FieldOffset(0x68)] public RawGameCamera* Camera2;
        [FieldOffset(0x70)] public RawGameCamera* Camera3;
        [FieldOffset(0x78)] public RawGameCamera* Camera4;

        public RawGameCamera* GetActive(SceneCameraManager* scCameraManager = null)
        {
            if (scCameraManager != null)
            {
                CameraIndex = scCameraManager->CameraIndex;
            }

            if (CameraIndex == 0)
                return Camera0;
            else if (CameraIndex == 1)
                return Camera1;
            else if (CameraIndex == 2)
                return Camera2;
            else if (CameraIndex == 3)
                return Camera3;
            else if (CameraIndex == 4)
                return Camera4;
            else
                return Camera0;
        }
    }
}