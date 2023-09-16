using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;

namespace xivr.Structures
{
    [StructLayout(LayoutKind.Explicit, Size = 168)]
    public unsafe struct Texture
    {
        [FieldOffset(0x00)] public void* vtbl;
        [FieldOffset(0x08)] public int RefCount;
        [FieldOffset(0x18)] public long uk5;
        [FieldOffset(0x20)] public Notifier Notifier;
        [FieldOffset(0x38)] public uint Width;
        [FieldOffset(0x3C)] public uint Height;
        [FieldOffset(0x40)] public uint Width1;
        [FieldOffset(0x44)] public uint Height1;
        [FieldOffset(0x48)] public uint Width2;
        [FieldOffset(0x4C)] public uint Height2;
        [FieldOffset(0x50)] public uint Depth;
        [FieldOffset(0x54)] public byte MipLevel;
        [FieldOffset(0x58)] public TextureFormat TextureFormat;
        [FieldOffset(0x5C)] public uint Flags;
        [FieldOffset(0x68)] public unsafe void* D3D11Texture2D;
        [FieldOffset(0x70)] public unsafe void* D3D11ShaderResourceView;
        [FieldOffset(0x80)] public UInt64 D3D11RenderTargetPtr;
    }
}