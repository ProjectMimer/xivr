using System;
using System.Runtime.InteropServices;

namespace xivr.Structures
{
    [StructLayout(LayoutKind.Explicit, Size = 0xF0)]
    public unsafe struct CameraManagerInstance
    {
        [FieldOffset(0x50)] public UInt64 CameraIndex;
        [FieldOffset(0x58)] public UInt64 CameraOffset;
    }
}