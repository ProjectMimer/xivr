using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.Interop.Attributes;

namespace xivr.Structures
{
    [StructLayout(LayoutKind.Explicit, Size = 1152)]
    public struct RenderTargetManager
    {
        public static class Addresses
        {
            public static readonly Address Instance = new StaticAddress("RenderTargetManager.Instance", "48 8B 0D ?? ?? ?? ?? 48 8B B1 ?? ?? ?? ?? ?? ??", new ulong[2] { 5188146770731699016uL, 45451uL }, new ulong[2] { 18374686479688400895uL, 65535uL }, (nuint)0u, 3);
        }

        public static class StaticAddressPointers
        {
            public unsafe static RenderTargetManager** ppInstance => (RenderTargetManager**)Addresses.Instance.Value;
        }

        [FieldOffset(0)]
        public unsafe void* vtbl;

        [FieldOffset(8)]
        public Notifier Notifier;

        [FieldOffset(32)]
        [FixedSizeArray<Pointer<Texture>>(65)]
        public unsafe fixed byte RenderTargetArray[520];

        [FieldOffset(96)]
        public unsafe Texture* MainDepthBuffer;

        [FieldOffset(480)]
        public unsafe Texture* OffscreenRenderTarget_1;

        [FieldOffset(488)]
        public unsafe Texture* OffscreenRenderTarget_2;

        [FieldOffset(496)]
        public unsafe Texture* OffscreenRenderTarget_3;

        [FieldOffset(504)]
        public unsafe Texture* OffscreenRenderTarget_4;

        [FieldOffset(512)]
        public unsafe Texture* OffscreenGBuffer;

        [FieldOffset(520)]
        public unsafe Texture* OffscreenDepthStencil;

        [FieldOffset(528)]
        public unsafe Texture* OffscreenRenderTarget_Unk1;

        [FieldOffset(536)]
        public unsafe Texture* OffscreenRenderTarget_Unk2;

        [FieldOffset(544)]
        public unsafe Texture* OffscreenRenderTarget_Unk3;

        [FieldOffset(584)]
        public unsafe Texture* MainRenderTarget;

        [FieldOffset(616)]
        public uint Resolution_Width;

        [FieldOffset(620)]
        public uint Resolution_Height;

        [FieldOffset(624)]
        public uint ShadowMap_Width;

        [FieldOffset(628)]
        public uint ShadowMap_Height;

        [FieldOffset(632)]
        public uint NearShadowMap_Width;

        [FieldOffset(636)]
        public uint NearShadowMap_Height;

        [FieldOffset(640)]
        public uint FarShadowMap_Width;

        [FieldOffset(644)]
        public uint FarShadowMap_Height;

        [FieldOffset(648)]
        public bool UnkBool_1;

        [FieldOffset(672)]
        [FixedSizeArray<Pointer<Texture>>(49)]
        public unsafe fixed byte RenderTargetArray2[392];

        public unsafe Span<Pointer<Texture>> RenderTargetArraySpan => new Span<Pointer<Texture>>(Unsafe.AsPointer(ref RenderTargetArray[0]), 65);

        public unsafe Span<Pointer<Texture>> RenderTargetArray2Span => new Span<Pointer<Texture>>(Unsafe.AsPointer(ref RenderTargetArray2[0]), 49);

        [StaticAddress("48 8B 0D ?? ?? ?? ?? 48 8B B1 ?? ?? ?? ??", 3, true)]
        public unsafe static RenderTargetManager* Instance()
        {
            if (StaticAddressPointers.ppInstance == null)
            {
                throw new InvalidOperationException("Pointer for RenderTargetManager.Instance is null. The resolver was either uninitialized or failed to resolve address with signature 48 8B 0D ?? ?? ?? ?? 48 8B B1 ?? ?? ?? ?? ?? ??.");
            }

            return *StaticAddressPointers.ppInstance;
        }
    }
}
