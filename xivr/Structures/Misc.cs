using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.Havok;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using Dalamud.Logging;

namespace xivr.Structures
{

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct Model
    {
        [FieldOffset(0x50)] public hkQsTransformf basePosition;
        [FieldOffset(0xA0)] public Skeleton* skeleton;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ScreenSettings
    {
        [FieldOffset(0x18)] public UInt64 hWnd;
        [FieldOffset(0x20)] public int Width;
        [FieldOffset(0x24)] public int Height;
        [FieldOffset(0x30)] public int ScreenStatus;
        [FieldOffset(0x58)] public int MinWidth;
        [FieldOffset(0x5C)] public int MinHeight;
        [FieldOffset(0x70)] public int FullWidth;
        [FieldOffset(0x74)] public int FullHeight;
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct CharSelectionCharList
    {
        [FieldOffset(0x00)] public GameObject* Character0;
        [FieldOffset(0x08)] public GameObject* Character1;
        [FieldOffset(0x10)] public GameObject* Character2;
        [FieldOffset(0x18)] public GameObject* Character3;
        [FieldOffset(0x20)] public GameObject* Character4;
        [FieldOffset(0x28)] public GameObject* Character5;
        [FieldOffset(0x30)] public GameObject* Character6;
        [FieldOffset(0x38)] public GameObject* Character7;
    }

    public static class ExtendedData
    {
        public unsafe static Model* GetModel(this PlayerCharacter playerCharacter)
        {
            return *(Model**)(playerCharacter.Address + 0x100);
        }


        public unsafe static Vector3 GetScale(this Matrix4x4 matrix)
        {
            return new Vector3(matrix.M14, matrix.M24, matrix.M34);
        }

        public unsafe static Matrix4x4 SetScale(this Matrix4x4 matrix, hkVector4f scale)
        {
            matrix.M14 = scale.X;
            matrix.M24 = scale.Y;
            matrix.M34 = scale.Z;
            return matrix;
        }

        public unsafe static Matrix4x4 SetScale(this Matrix4x4 matrix, Vector3 scale)
        {
            matrix.M14 = scale.X;
            matrix.M24 = scale.Y;
            matrix.M34 = scale.Z;
            return matrix;
        }


        public unsafe static float Magnitude(this Vector3 vector)
        {
            return vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z;
        }

        public unsafe static hkVector4f hkVector4f(this hkVector4f vector, float X, float Y, float Z, float W)
        {
            vector.X = X;
            vector.Y = Y;
            vector.Z = Z;
            vector.W = W;
            return vector;
        }

        public static Transform Convert(this hkQsTransformf transform)
        {
            Transform retVal = new Transform();
            retVal.Position = transform.Translation.Convert();
            retVal.Rotation = transform.Rotation.Convert();
            retVal.Scale = transform.Scale.Convert();
            return retVal;
        }

        public static hkQsTransformf Convert(this Transform transform)
        {
            hkQsTransformf retVal = new hkQsTransformf();
            retVal.Translation = transform.Position.Convert();
            retVal.Rotation = transform.Rotation.Convert();
            retVal.Scale = transform.Scale.Convert();
            return retVal;
        }

        public static Quaternion Convert(this hkQuaternionf quaternion)
        {
            return new Quaternion(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
        }

        public static hkQuaternionf Convert(this Quaternion quaternion)
        {
            hkQuaternionf retVal = new hkQuaternionf();
            retVal.X = quaternion.X;
            retVal.Y = quaternion.Y;
            retVal.Z = quaternion.Z;
            retVal.W = quaternion.W;
            return retVal;
        }

        public static hkQuaternionf Convert(this FFXIVClientStructs.FFXIV.Common.Math.Quaternion quaternion)
        {
            hkQuaternionf retVal = new hkQuaternionf();
            retVal.X = quaternion.X;
            retVal.Y = quaternion.Y;
            retVal.Z = quaternion.Z;
            retVal.W = quaternion.W;
            return retVal;
        }

        public static Vector3 Convert(this hkVector4f vector)
        {
            return new Vector3(vector.X, vector.Y, vector.Z);
        }

        public static hkVector4f Convert(this Vector3 vector)
        {
            hkVector4f retVal = new hkVector4f();
            retVal.X = vector.X;
            retVal.Y = vector.Y;
            retVal.Z = vector.Z;
            retVal.W = 1.0f;
            return retVal;
        }

        public static hkVector4f Convert(this FFXIVClientStructs.FFXIV.Common.Math.Vector3 vector)
        {
            hkVector4f retVal = new hkVector4f();
            retVal.X = vector.X;
            retVal.Y = vector.Y;
            retVal.Z = vector.Z;
            retVal.W = 1.0f;
            return retVal;
        }


    }
}
