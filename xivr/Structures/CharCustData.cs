using System;
using System.Runtime.InteropServices;

namespace xivr.Structures
{
    [StructLayout(LayoutKind.Sequential, Size = 26)]
    public unsafe struct CharCustData
    {
        public byte Race;
        public byte Gender;
        public byte ModelType;
        public byte Height;
        public byte Tribe;
        public byte FaceType;
        public byte HairStyle;
        public byte HasHighlights;
        public byte SkinColor;
        public byte EyeColor;
        public byte HairColor;
        public byte HairColor2;
        public byte FaceFeatures;
        public byte FaceFeaturesColor;
        public byte Eyebrows;
        public byte EyeColor2;
        public byte EyeShape;
        public byte NoseShape;
        public byte JawShape;
        public byte LipStyle;
        public byte LipColor;
        public byte RaceFeatureSize;
        public byte RaceFeatureType;
        public byte BustSize;
        public byte Facepaint;
        public byte FacepaintColor;

        public CharCustData(UInt64 offset)
        {
            Race = *(byte*)(offset + 0);
            Gender = *(byte*)(offset + 1);
            ModelType = *(byte*)(offset + 2);
            Height = *(byte*)(offset + 3);
            Tribe = *(byte*)(offset + 4);
            FaceType = *(byte*)(offset + 5);
            HairStyle = *(byte*)(offset + 6);
            HasHighlights = *(byte*)(offset + 7);
            SkinColor = *(byte*)(offset + 8);
            EyeColor = *(byte*)(offset + 9);
            HairColor = *(byte*)(offset + 10);
            HairColor2 = *(byte*)(offset + 11);
            FaceFeatures = *(byte*)(offset + 12);
            FaceFeaturesColor = *(byte*)(offset + 13);
            Eyebrows = *(byte*)(offset + 14);
            EyeColor2 = *(byte*)(offset + 15);
            EyeShape = *(byte*)(offset + 16);
            NoseShape = *(byte*)(offset + 17);
            JawShape = *(byte*)(offset + 18);
            LipStyle = *(byte*)(offset + 19);
            LipColor = *(byte*)(offset + 20);
            RaceFeatureSize = *(byte*)(offset + 21);
            RaceFeatureType = *(byte*)(offset + 22);
            BustSize = *(byte*)(offset + 23);
            Facepaint = *(byte*)(offset + 24);
            FacepaintColor = *(byte*)(offset + 25);
        }
    }
}
