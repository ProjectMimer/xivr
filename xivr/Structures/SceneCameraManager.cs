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

    }
}