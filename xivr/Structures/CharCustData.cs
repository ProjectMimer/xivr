using System;
using System.Runtime.InteropServices;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace xivr.Structures
{
    [StructLayout(LayoutKind.Explicit, Size = 26)]
    public unsafe struct CharCustData
    {
        [FieldOffset(0x00)] public fixed byte Data[26];
        [FieldOffset(0x00)] public byte Race;
        [FieldOffset(0x01)] public byte Gender;
        [FieldOffset(0x02)] public byte ModelType;
        [FieldOffset(0x03)] public byte Height;
        [FieldOffset(0x04)] public byte Tribe;
        [FieldOffset(0x05)] public byte FaceType;
        [FieldOffset(0x06)] public byte HairStyle;
        [FieldOffset(0x07)] public byte HasHighlights;
        [FieldOffset(0x08)] public byte SkinColor;
        [FieldOffset(0x09)] public byte EyeColor;
        [FieldOffset(0x0A)] public byte HairColor;
        [FieldOffset(0x0B)] public byte HairColor2;
        [FieldOffset(0x0C)] public byte FaceFeatures;
        [FieldOffset(0x0D)] public byte FaceFeaturesColor;
        [FieldOffset(0x0E)] public byte Eyebrows;
        [FieldOffset(0x0F)] public byte EyeColor2;
        [FieldOffset(0x10)] public byte EyeShape;
        [FieldOffset(0x11)] public byte NoseShape;
        [FieldOffset(0x12)] public byte JawShape;
        [FieldOffset(0x13)] public byte LipStyle;
        [FieldOffset(0x14)] public byte LipColor;
        [FieldOffset(0x15)] public byte RaceFeatureSize;
        [FieldOffset(0x16)] public byte RaceFeatureType;
        [FieldOffset(0x17)] public byte BustSize;
        [FieldOffset(0x18)] public byte Facepaint;
        [FieldOffset(0x19)] public byte FacepaintColor;

        public void Save(Character* character)
        {
            for (int i = 0; i < 26; i++)
                Data[i] = character->DrawData.CustomizeData[i];
        }
    }
}
