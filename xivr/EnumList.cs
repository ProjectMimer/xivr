using System;

namespace xivr
{
    [Flags]
    public enum poseType
    {
        Projection = 0,
        EyeOffset = 1,
        hmdPosition = 10,
        LeftHand = 20,
        RightHand = 30,
    }

    [Flags]
    public enum CharEquipSlots
    {
        Head = 0,
        Body = 1,
        Hands = 2,
        Legs = 3,
        Feet = 4,
        Ears = 5,
        Neck = 6,
        Wrist = 7,
        RRing = 8,
        LRing = 9
    }

    [Flags]
    public enum CharWeaponSlots
    { 
        MainHand = 0,
        OffHand = 1,
        uk3 = 2
    }

    [Flags]
    public enum CameraModes
    {
        FirstPerson = 0,
        ThirdPerson = 1,
    }

    [Flags]
    public enum VisibilityFlags
    {
        None = 0,
        Unknown0 = 1 << 0,
        Model = 1 << 1,
        Unknown2 = 1 << 2,
        Unknown3 = 1 << 3,
        Unknown4 = 1 << 4,
        Unknown5 = 1 << 5,
        Unknown6 = 1 << 6,
        Unknown7 = 1 << 7,
        Unknown8 = 1 << 8,
        Unknown9 = 1 << 9,
        Unknown10 = 1 << 10,
        Nameplate = 1 << 11,
        Unknown12 = 1 << 12,
        Unknown13 = 1 << 13,
        Unknown14 = 1 << 14,
        Unknown15 = 1 << 15,
        Unknown16 = 1 << 16,
        Unknown17 = 1 << 17,
        Unknown18 = 1 << 18,
        Unknown19 = 1 << 19,
        Unknown20 = 1 << 20,
        Unknown21 = 1 << 21,
        Unknown22 = 1 << 22,
        Unknown23 = 1 << 23,
        Unknown24 = 1 << 24,
        Unknown25 = 1 << 25,
        Unknown26 = 1 << 26,
        Unknown27 = 1 << 27,
        Unknown28 = 1 << 28,
        Unknown39 = 1 << 29,
        Unknown30 = 1 << 30,
        Unknown31 = 1 << 31,
        Invisible = Model | Nameplate,
    }
}
