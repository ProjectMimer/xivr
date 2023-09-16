using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVClientStructs.Havok;

namespace xivr.Structures
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct hkIKSetup
    {
        [FieldOffset(0x00)] public short m_firstJointIdx;
        [FieldOffset(0x02)] public short m_secondJointIdx;
        [FieldOffset(0x04)] public short m_endBoneIdx;
        [FieldOffset(0x06)] public short m_firstJointTwistIdx;
        [FieldOffset(0x08)] public short m_secondJointTwistIdx;
        [FieldOffset(0x0A)] public short m_spacer;
        [FieldOffset(0x0C)] public float m_spacer1;
        [FieldOffset(0x10)] public Vector4 m_hingeAxisLS;
        [FieldOffset(0x20)] public float m_cosineMaxHingeAngle;
        [FieldOffset(0x24)] public float m_cosineMinHingeAngle;
        [FieldOffset(0x28)] public float m_firstJointIkGain;
        [FieldOffset(0x2C)] public float m_secondJointIkGain;
        [FieldOffset(0x30)] public float m_endJointIkGain;
        [FieldOffset(0x34)] public float m_spacer2;
        [FieldOffset(0x38)] public float m_spacer3;
        [FieldOffset(0x3C)] public float m_spacer4;
        [FieldOffset(0x40)] public Vector4 m_endTargetMS;
        [FieldOffset(0x50)] public Quaternion m_endTargetRotationMS;
        [FieldOffset(0x60)] public Vector4 m_endBoneOffsetLS;
        [FieldOffset(0x70)] public Quaternion m_endBoneRotationOffsetLS;
        [FieldOffset(0x80)] public bool m_enforceEndPosition;
        [FieldOffset(0x81)] public bool m_enforceEndRotation;

        public hkIKSetup()
        {
            m_firstJointIdx = -1;
            m_secondJointIdx = -1;
            m_endBoneIdx = -1;
            m_firstJointTwistIdx = -1;
            m_secondJointTwistIdx = -1;
            m_spacer = 0;
            m_spacer1 = 0.0f;
            m_hingeAxisLS = Vector4.Zero;
            m_cosineMaxHingeAngle = -1.0f;
            m_cosineMinHingeAngle = 1.0f;
            m_firstJointIkGain = 1.0f;
            m_secondJointIkGain = 1.0f;
            m_endJointIkGain = 1.0f;
            m_spacer2 = 0.0f;
            m_spacer3 = 0.0f;
            m_spacer4 = 0.0f;
            m_endTargetMS = Vector4.Zero;
            m_endTargetRotationMS = Quaternion.Identity;
            m_endBoneOffsetLS = Vector4.Zero;
            m_endBoneRotationOffsetLS = Quaternion.Identity;
            m_enforceEndPosition = true;
            m_enforceEndRotation = false;
        }
    }


    public unsafe struct stMultiIK
    {
        public UInt32 worldId;
        public UInt32 objId;
        public Character* objCharacter;
        public Skeleton* objSkeleton;
        public bool isPlayer;
        public Matrix4x4 hmdMatrix;
        public Matrix4x4 lhcMatrix;
        public Matrix4x4 rhcMatrix;
        public bool doHandIK;
        public float armMultiplier;
        public UInt64 lastUpdate;
        public bool doUpdate;

        public stMultiIK(UInt32 wId, UInt32 id, Character* character, Skeleton* skeleton, bool player, Matrix4x4 hmd, Matrix4x4 lhc, Matrix4x4 rhc, bool runHandIK, float armMulti)
        {
            worldId = wId;
            objId = id;
            objCharacter = character;
            objSkeleton = skeleton;
            isPlayer = player;
            hmdMatrix = hmd;
            lhcMatrix = lhc;
            rhcMatrix = rhc;
            doHandIK = runHandIK;
            armMultiplier = armMulti;
            doUpdate = true;
        }

        public void SetTracking(bool player, Matrix4x4 hmd, Matrix4x4 lhc, Matrix4x4 rhc, bool runHandIK, float armMulti)
        {
            isPlayer = player;
            hmdMatrix = hmd;
            lhcMatrix = lhc;
            rhcMatrix = rhc;
            doHandIK = runHandIK;
            armMultiplier = armMulti;
            doUpdate = true;
        }
    }


































    public class IK
    {
        private float Deg2Rad = MathF.PI / 180.0f;
        private float Rad2Deg = 180.0f / MathF.PI;

        private Vector3 en;

        public Bone target, start, joint, end, pole;
        public hkQsTransformf upVector;
        //public Transform target, pole, upVector;
        public float blendPct = 1.0f;
        public float upperElbowRotation = 0;
        public float lowerElbowRotation = 0;

        public unsafe void Update(bool mirror = false)
        {
            float a = joint.transform.Translation.Magnitude();
            float b = end.transform.Translation.Magnitude();
            float c = Vector3.Distance(target.worldBase.Translation.Convert(), start.worldBase.Translation.Convert());

            Vector3 targetStart = target.worldBase.Translation.Convert() - start.worldBase.Translation.Convert();
            Vector3 poleStart = pole.worldBase.Translation.Convert() - start.worldBase.Translation.Convert();
            Vector3 jointStart = joint.worldBase.Translation.Convert() - start.worldBase.Translation.Convert();

            en = Vector3.Cross(targetStart, poleStart);

            //Set the rotation of the upper arm
            if (mirror)
            {
                start.worldBase.Rotation = LookRotation(-targetStart, -Transform(en, -AngleAxis(upperElbowRotation, jointStart))).Convert();
                start.worldBase.Rotation = (start.worldBase.Rotation.Convert() * Inverse(FromToRotation(Vector3.Back, joint.transform.Translation.Convert()))).Convert();
            }
            else
            {
                start.worldBase.Rotation = LookRotation(targetStart, Transform(en, AngleAxis(upperElbowRotation, jointStart))).Convert();
                start.worldBase.Rotation = (start.worldBase.Rotation.Convert() * Inverse(FromToRotation(Vector3.Forward, joint.transform.Translation.Convert()))).Convert();
            }
            start.worldBase.Rotation = (AngleAxis(CosAngle(a, c, b), en) * start.worldBase.Rotation.Convert()).Convert();
            start.setLocal(false);
            start.CalculateMatrix(true);
            start.setUpdates(true, true, true);

            //set the rotation of the lower arm
            Vector3 targetJoint = target.worldBase.Translation.Convert() - joint.worldBase.Translation.Convert();
            Vector3 endJoint = end.worldBase.Translation.Convert() - joint.worldBase.Translation.Convert();

            if (mirror)
            {
                joint.worldBase.Rotation = LookRotation(-targetJoint, -Transform(en, -AngleAxis(lowerElbowRotation, endJoint))).Convert();
                joint.worldBase.Rotation = (joint.worldBase.Rotation.Convert() * Inverse(FromToRotation(Vector3.Back, end.transform.Translation.Convert()))).Convert();
            }
            else
            {
                joint.worldBase.Rotation = LookRotation(targetJoint, Transform(en, AngleAxis(lowerElbowRotation, endJoint))).Convert();
                joint.worldBase.Rotation = (joint.worldBase.Rotation.Convert() * Inverse(FromToRotation(Vector3.Forward, end.transform.Translation.Convert()))).Convert();
            }
            joint.setLocal(false);
            joint.CalculateMatrix(true);
            joint.setUpdates(true, true, true);

            //end.setLocal(false);
            //end.CalculateMatrix(true);
            //end.setUpdates(true, true, false);
        }

        float CosAngle(float a, float b, float c)
        {
            if (!float.IsNaN(MathF.Acos((-(c * c) + (a * a) + (b * b)) / (-2 * a * b)) * Rad2Deg))
            {
                return MathF.Acos((-(c * c) + (a * a) + (b * b)) / (2 * a * b)) * Rad2Deg;
            }
            else
            {
                return 1;
            }
        }

        Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            Vector3 vector = Vector3.Normalize(forward);
            Vector3 vector2 = Vector3.Normalize(Vector3.Cross(up, vector));
            Vector3 vector3 = Vector3.Cross(vector, vector2);
            var m00 = vector2.X;
            var m01 = vector2.Y;
            var m02 = vector2.Z;
            var m10 = vector3.X;
            var m11 = vector3.Y;
            var m12 = vector3.Z;
            var m20 = vector.X;
            var m21 = vector.Y;
            var m22 = vector.Z;

            float num8 = (m00 + m11) + m22;
            var quaternion = new Quaternion();
            if (num8 > 0f)
            {
                var num = (float)MathF.Sqrt(num8 + 1f);
                quaternion.W = num * 0.5f;
                num = 0.5f / num;
                quaternion.X = (m12 - m21) * num;
                quaternion.Y = (m20 - m02) * num;
                quaternion.Z = (m01 - m10) * num;
                return quaternion;
            }
            if ((m00 >= m11) && (m00 >= m22))
            {
                var num7 = (float)MathF.Sqrt(((1f + m00) - m11) - m22);
                var num4 = 0.5f / num7;
                quaternion.X = 0.5f * num7;
                quaternion.Y = (m01 + m10) * num4;
                quaternion.Z = (m02 + m20) * num4;
                quaternion.W = (m12 - m21) * num4;
                return quaternion;
            }
            if (m11 > m22)
            {
                var num6 = (float)MathF.Sqrt(((1f + m11) - m00) - m22);
                var num3 = 0.5f / num6;
                quaternion.X = (m10 + m01) * num3;
                quaternion.Y = 0.5f * num6;
                quaternion.Z = (m21 + m12) * num3;
                quaternion.W = (m20 - m02) * num3;
                return quaternion;
            }

            var num5 = (float)MathF.Sqrt(((1f + m22) - m00) - m11);
            var num2 = 0.5f / num5;
            quaternion.X = (m20 + m02) * num2;
            quaternion.Y = (m21 + m12) * num2;
            quaternion.Z = 0.5f * num5;
            quaternion.W = (m01 - m10) * num2;
            return quaternion;
        }

        System.Numerics.Quaternion FromToRotation(Vector3 aFrom, Vector3 aTo)
        {
            Vector3 axis = Vector3.Cross(aFrom, aTo);
            float angle = Vector3.Angle(aFrom, aTo) * Rad2Deg;
            //PluginLog.Log($"{angle} | {aFrom} | {aTo} | {axis}");
            return AngleAxis(angle, Vector3.Normalize(axis));
        }

        System.Numerics.Quaternion Inverse(Quaternion value)
        {
            Quaternion ans;

            float ls = value.X * value.X + value.Y * value.Y + value.Z * value.Z + value.W * value.W;
            float invNorm = 1.0f / ls;

            ans.X = -value.X * invNorm;
            ans.Y = -value.Y * invNorm;
            ans.Z = -value.Z * invNorm;
            ans.W = value.W * invNorm;

            return ans;
        }

        Vector3 Transform(Vector3 value, Quaternion rotation)
        {
            float x2 = rotation.X + rotation.X;
            float y2 = rotation.Y + rotation.Y;
            float z2 = rotation.Z + rotation.Z;

            float wx2 = rotation.W * x2;
            float wy2 = rotation.W * y2;
            float wz2 = rotation.W * z2;
            float xx2 = rotation.X * x2;
            float xy2 = rotation.X * y2;
            float xz2 = rotation.X * z2;
            float yy2 = rotation.Y * y2;
            float yz2 = rotation.Y * z2;
            float zz2 = rotation.Z * z2;

            return new Vector3(
                value.X * (1.0f - yy2 - zz2) + value.Y * (xy2 - wz2) + value.Z * (xz2 + wy2),
                value.X * (xy2 + wz2) + value.Y * (1.0f - xx2 - zz2) + value.Z * (yz2 - wx2),
                value.X * (xz2 - wy2) + value.Y * (yz2 + wx2) + value.Z * (1.0f - xx2 - yy2));
        }

        System.Numerics.Quaternion AngleAxis(float aAngle, Vector3 aAxis)
        {
            aAxis = Vector3.Normalize(aAxis);
            float rad = aAngle * Deg2Rad * 0.5f;
            aAxis *= MathF.Sin(rad);
            return new Quaternion(aAxis.X, aAxis.Y, aAxis.Z, MathF.Cos(rad));
        }
    }
}
