using System;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Enums;

namespace xivr.Structures
{
    [StructLayout(LayoutKind.Sequential, Size = 40)]
    public unsafe struct CharEquipData
    {
        public ushort Head;
        public byte HeadVariant;
        public byte HeadDye;

        public ushort Body;
        public byte BodyVariant;
        public byte BodyDye;

        public ushort Hands;
        public byte HandsVariant;
        public byte HandsDye;

        public ushort Legs;
        public byte LegsVariant;
        public byte LegsDye;

        public ushort Feet;
        public byte FeetVariant;
        public byte FeetDye;

        public ushort Ears;
        public byte EarsVariant;
        public byte EarsDye;

        public ushort Neck;
        public byte NeckVariant;
        public byte NeckDye;

        public ushort Wrist;
        public byte WristVariant;
        public byte WristDye;

        public ushort RRing;
        public byte RRingVariant;
        public byte RRingDye;

        public ushort LRing;
        public byte LRingVariant;
        public byte LRingDye;

        public CharEquipData(UInt64 offset)
        {
            Head = *(ushort*)(offset + 0);
            HeadVariant = *(byte*)(offset + 0 + 2);
            HeadDye = *(byte*)(offset + 0 + 3);

            Body = *(ushort*)(offset + 4);
            BodyVariant = *(byte*)(offset + 4 + 2);
            BodyDye = *(byte*)(offset + 4 + 3);

            Hands = *(ushort*)(offset + 8);
            HandsVariant = *(byte*)(offset + 8 + 2);
            HandsDye = *(byte*)(offset + 8 + 3);

            Legs = *(ushort*)(offset + 12);
            LegsVariant = *(byte*)(offset + 12 + 2);
            LegsDye = *(byte*)(offset + 12 + 3);

            Feet = *(ushort*)(offset + 16);
            FeetVariant = *(byte*)(offset + 16 + 2);
            FeetDye = *(byte*)(offset + 16 + 3);

            Ears = *(ushort*)(offset + 20);
            EarsVariant = *(byte*)(offset + 20 + 2);
            EarsDye = *(byte*)(offset + 20 + 3);

            Neck = *(ushort*)(offset + 24);
            NeckVariant = *(byte*)(offset + 24 + 2);
            NeckDye = *(byte*)(offset + 24 + 3);

            Wrist = *(ushort*)(offset + 28);
            WristVariant = *(byte*)(offset + 28 + 2);
            WristDye = *(byte*)(offset + 28 + 3);

            RRing = *(ushort*)(offset + 32);
            RRingVariant = *(byte*)(offset + 32 + 2);
            RRingDye = *(byte*)(offset + 32 + 3);

            LRing = *(ushort*)(offset + 36);
            LRingVariant = *(byte*)(offset + 36 + 2);
            LRingDye = *(byte*)(offset + 36 + 3);
        }
    }
}
