using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;

namespace xivr.Structures
{
    [StructLayout(LayoutKind.Explicit, Size = 168)]
    public struct Texture
    {
        [FieldOffset(0)]
        public unsafe void* vtbl;

        [FieldOffset(24)]
        public long uk5;

        [FieldOffset(32)]
        public Notifier Notifier;

        [FieldOffset(56)]
        public uint Width;

        [FieldOffset(60)]
        public uint Height;

        [FieldOffset(64)]
        public uint Width1;

        [FieldOffset(68)]
        public uint Height1;

        [FieldOffset(72)]
        public uint Depth;

        [FieldOffset(76)]
        public byte MipLevel;

        [FieldOffset(77)]
        public byte Unk_35;

        [FieldOffset(78)]
        public byte Unk_36;

        [FieldOffset(79)]
        public byte Unk_37;

        [FieldOffset(80)]
        public TextureFormat TextureFormat;

        [FieldOffset(84)]
        public uint Flags;

        [FieldOffset(88)]
        public unsafe void* D3D11Texture2D;

        [FieldOffset(96)]
        public unsafe void* D3D11ShaderResourceView;

        [FieldOffset(112)]
        public UInt64 D3D11RenderTargetPtr;
    }
}