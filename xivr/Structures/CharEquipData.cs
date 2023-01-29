using System;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace xivr.Structures
{
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public unsafe struct CharEquipData
    {
        [FieldOffset(0x00)] public fixed uint Data[10];
        [FieldOffset(0x00)] public CharEquipSlotData Head;
        [FieldOffset(0x04)] public CharEquipSlotData Body;
        [FieldOffset(0x08)] public CharEquipSlotData Hands;
        [FieldOffset(0x0C)] public CharEquipSlotData Legs;
        [FieldOffset(0x10)] public CharEquipSlotData Feet;
        [FieldOffset(0x14)] public CharEquipSlotData Ears;
        [FieldOffset(0x18)] public CharEquipSlotData Neck;
        [FieldOffset(0x1C)] public CharEquipSlotData Wrist;
        [FieldOffset(0x20)] public CharEquipSlotData RRing;
        [FieldOffset(0x24)] public CharEquipSlotData LRing;

        public void Save(Character* character)
        {
            Head.Save(character->DrawData.Head);
            Body.Save(character->DrawData.Top);
            Hands.Save(character->DrawData.Arms);
            Legs.Save(character->DrawData.Legs);
            Feet.Save(character->DrawData.Feet);
            Ears.Save(character->DrawData.Ear);
            Neck.Save(character->DrawData.Neck);
            Wrist.Save(character->DrawData.Wrist);
            RRing.Save(character->DrawData.RFinger);
            LRing.Save(character->DrawData.LFinger);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x4)]
    public unsafe struct CharEquipSlotData
    {
        [FieldOffset(0)] public uint Data;
        [FieldOffset(0)] public ushort Id;
        [FieldOffset(2)] public byte Variant;
        [FieldOffset(3)] public byte Dye;
        public CharEquipSlotData(ushort id, byte variant, byte dye)
        {
            Id = id;
            Variant = variant;
            Dye = dye;
        }
        public void Save(EquipmentModelId data)
        {
            Id = data.Id;
            Variant = data.Variant;
            Dye = data.Stain;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct CharWeaponData
    {
        [FieldOffset(0x00)] public fixed UInt64 Data[3];
        [FieldOffset(0x00)] public CharWeaponSlotData MainHand;
        [FieldOffset(0x08)] public CharWeaponSlotData OffHand;
        [FieldOffset(0x0C)] public CharWeaponSlotData Uk3;

        public void Save(Character* character)
        {
            MainHand.Save(character->DrawData.MainHandModel);
            OffHand.Save(character->DrawData.OffHandModel);
            Uk3.Save(character->DrawData.UnkE0Model);
        }

    }

    [StructLayout(LayoutKind.Explicit, Size = 0x8)]
    public unsafe struct CharWeaponSlotData
    {
        [FieldOffset(0)] public UInt64 Data;
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(2)] public ushort Id;
        [FieldOffset(4)] public ushort Variant;
        [FieldOffset(6)] public byte Dye;
        [FieldOffset(7)] public byte uk1;

        public CharWeaponSlotData(ushort type, ushort id, byte variant, byte dye)
        {
            Type = type;
            Id = id;
            Variant = variant;
            Dye = dye;
        }

        public void Save(WeaponModelId data)
        {
            Type = data.Type;
            Id = data.Id;
            Variant = data.Variant;
            Dye = data.Stain;
        }
    }
}
