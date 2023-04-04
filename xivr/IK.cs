using System;
using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using xivr.Structures;
using System.Transactions;

namespace xivr
{
    public class IK
    {
        private float Deg2Rad = MathF.PI / 180.0f;
        private float Rad2Deg = 180.0f / MathF.PI;
        private float a;
        private float b;
        private float c;
        private Vector3 en;

        public Bone start, joint, end;
        public Transform target, poleVector, upVector;
        public float blendPct = 1.0f;
        public float upperElbowRotation = 0;
        public float lowerElbowRotation = 0;
        private Transform startXform, jointXform, endXform;


        public void Update1()
        {
            const float epsilon = 0.001f;
            if (blendPct < epsilon)
                return;

            Vector3 preUp = Vector3.Normalize(Vector3.Cross(end.transform.Translation.Convert() - start.transform.Translation.Convert(), joint.transform.Translation.Convert() - start.transform.Translation.Convert()));
            //Vector3 preUp = upVector.up:

            Vector3 targetPosition = target.Position;
            Quaternion targetRotation = target.Rotation;

            Vector3 forward, up, result = joint.transform.Translation.Convert();
            Solve(start.transform.Translation.Convert(), targetPosition, poleVector.Position,
                (joint.transform.Translation.Convert() - start.transform.Translation.Convert()).Magnitude(),
                (end.transform.Translation.Convert() - joint.transform.Translation.Convert()).Magnitude(),
                ref result, out forward, out up);

            if (up == Vector3.Zero)
                return;

            Vector3 startPosition = start.transform.Translation.Convert();
            Vector3 jointPosition = joint.transform.Translation.Convert();
            Vector3 endPosition = end.transform.Translation.Convert();

            Quaternion startRotationLocal = start.transform.Rotation.Convert();
            Quaternion jointRotationLocal = joint.transform.Rotation.Convert();
            Quaternion endRotationLocal = end.transform.Rotation.Convert();

            //Bone startParent = start.parent;
            //Bone jointParent = joint.parent;
            //Bone endParent = end.parent;

            Vector3 startScale = start.transform.Scale.Convert();
            Vector3 jointScale = joint.transform.Scale.Convert();
            Vector3 endScale = end.transform.Scale.Convert();


            startXform.Position = startPosition;
            startXform.Rotation = LookAt(joint.transform.Translation.Convert(), preUp);
            
            jointXform.Position = jointPosition;
            jointXform.Rotation = LookAt(end.transform.Translation.Convert(), preUp);

            endXform.Position = endPosition;
            
            startXform.Rotation = LookAt(result, up);
            jointXform.Rotation = LookAt(targetPosition, up);
            endXform.Rotation = targetRotation;

            //start.parent = startParent;
            //joint.parent = jointParent;
            //end.parent = endParent;
            
            end.transform.Rotation = targetRotation.Convert(); // optionally blend?

            // handle blending in/out
            if (blendPct < 1.0f)
            {
                start.transform.Rotation = Quaternion.Slerp(startRotationLocal, start.transform.Rotation.Convert(), blendPct).Convert();
                joint.transform.Rotation = Quaternion.Slerp(jointRotationLocal, joint.transform.Rotation.Convert(), blendPct).Convert();
                end.transform.Rotation = Quaternion.Slerp(endRotationLocal, end.transform.Rotation.Convert(), blendPct).Convert();
            }

            // restore scale so it doesn't blow out
            start.transform.Scale = startScale.Convert();
            joint.transform.Scale = jointScale.Convert();
            end.transform.Scale = endScale.Convert();
        }

        public static bool Solve(
            Vector3 start, // shoulder / hip
            Vector3 end, // desired hand / foot position
            Vector3 poleVector, // point to aim elbow / knee toward
            float jointDist, // distance from start to elbow / knee
            float targetDist, // distance from joint to hand / ankle
            ref Vector3 result, // original and output elbow / knee position
            out Vector3 forward, out Vector3 up) // plane formed by root, joint and target
        {
            var totalDist = jointDist + targetDist;
            var start2end = end - start;
            var poleVectorDir = (poleVector - start).Normalized;
            var baseDist = start2end.Magnitude;

            result = start;

            const float epsilon = 0.001f;
            if (baseDist < epsilon)
            {
                // move jointDist toward jointTarget
                result += poleVectorDir * jointDist;

                forward = Vector3.Cross(poleVectorDir, Vector3.Up);
                up = Vector3.Cross(forward, poleVectorDir).Normalized;
            }
            else
            {
                forward = start2end * (1.0f / baseDist);
                up = Vector3.Cross(forward, poleVectorDir).Normalized;

                if (baseDist + epsilon < totalDist)
                {
                    // calculate the area of the triangle to determine its height
                    var p = (totalDist + baseDist) * 0.5f; // half perimeter
                    if (p > jointDist + epsilon && p > targetDist + epsilon)
                    {
                        var A = MathF.Sqrt(p * (p - jointDist) * (p - targetDist) * (p - baseDist));
                        var height = 2.0f * A / baseDist; // distance of joint from line between root and target

                        var dist = MathF.Sqrt((jointDist * jointDist) - (height * height));
                        var right = Vector3.Cross(up, forward); // no need to normalized - already orthonormal

                        result += (forward * dist) + (right * height);
                        return true; // in range
                    }
                    else
                    {
                        // move jointDist toward jointTarget
                        result += poleVectorDir * jointDist;
                    }
                }
                else
                {
                    // move elboDist toward target
                    result += forward * jointDist;
                }
            }

            return false; // edge cases
        }


        public static Quaternion LookAt(Vector3 sourcePoint, Vector3 destPoint)
        {
            Vector3 forwardVector = Vector3.Normalize(destPoint - sourcePoint);

            float dot = Vector3.Dot(Vector3.Forward, forwardVector);

            if (Math.Abs(dot - (-1.0f)) < 0.000001f)
            {
                return new Quaternion(Vector3.Up.X, Vector3.Up.Y, Vector3.Up.Z, 3.1415926535897932f);
            }
            if (Math.Abs(dot - (1.0f)) < 0.000001f)
            {
                return Quaternion.Identity;
            }

            float rotAngle = (float)Math.Acos(dot);
            Vector3 rotAxis = Vector3.Cross(Vector3.Forward, forwardVector);
            rotAxis = Vector3.Normalize(rotAxis);
            return CreateFromAxisAngle(rotAxis, rotAngle);
        }

        // just in case you need that function also
        public static Quaternion CreateFromAxisAngle(Vector3 axis, float angle)
        {
            float halfAngle = angle * 0.5f;
            float s = (float)System.Math.Sin(halfAngle);
            Quaternion q;
            q.X = axis.X * s;
            q.Y = axis.Y * s;
            q.Z = axis.Z * s;
            q.W = (float)System.Math.Cos(halfAngle);
            return q;
        }



















        
        public void Update()
        {
            a = joint.transform.Translation.Convert().Magnitude();
            b = end.transform.Translation.Convert().Magnitude();
            c = Vector3.Distance(start.transform.Translation.Convert(), target.Position);
            en = Vector3.Cross(target.Position - (Vector3)start.transform.Translation.Convert(), poleVector.Position - (Vector3)start.transform.Translation.Convert());

            start.transform.Rotation = LookRotation(target.Position - (Vector3)start.transform.Translation.Convert(), Transform(en, AngleAxis((Vector3)joint.transform.Translation.Convert() - (Vector3)start.transform.Translation.Convert(), upperElbowRotation))).Convert();
            start.transform.Rotation = ((Quaternion)start.transform.Rotation.Convert() * Quaternion.Invert(FromToRotation(Vector3.Forward, (Vector3)joint.transform.Translation.Convert()))).Convert();
            start.transform.Rotation = (Quaternion.CreateFromAxisAngle(-en, -CosAngle(a, c, b)) * (Quaternion)start.transform.Rotation.Convert()).Convert();

            joint.transform.Rotation = LookRotation(target.Position - (Vector3)joint.transform.Translation.Convert(), Transform(en, AngleAxis((Vector3)end.transform.Translation.Convert() - (Vector3)joint.transform.Translation.Convert(), lowerElbowRotation))).Convert();
            joint.transform.Rotation = ((Quaternion)joint.transform.Rotation.Convert() * Quaternion.Invert(FromToRotation(Vector3.Forward, (Vector3)end.transform.Translation.Convert()))).Convert();
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
            forward = Vector3.Normalize(forward);

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
                var num = (float)Math.Sqrt(num8 + 1f);
                quaternion.W = num * 0.5f;
                num = 0.5f / num;
                quaternion.X = (m12 - m21) * num;
                quaternion.Y = (m20 - m02) * num;
                quaternion.Z = (m01 - m10) * num;
                return quaternion;
            }
            if ((m00 >= m11) && (m00 >= m22))
            {
                var num7 = (float)Math.Sqrt(((1f + m00) - m11) - m22);
                var num4 = 0.5f / num7;
                quaternion.X = 0.5f * num7;
                quaternion.Y = (m01 + m10) * num4;
                quaternion.Z = (m02 + m20) * num4;
                quaternion.W = (m12 - m21) * num4;
                return quaternion;
            }
            if (m11 > m22)
            {
                var num6 = (float)Math.Sqrt(((1f + m11) - m00) - m22);
                var num3 = 0.5f / num6;
                quaternion.X = (m10 + m01) * num3;
                quaternion.Y = 0.5f * num6;
                quaternion.Z = (m21 + m12) * num3;
                quaternion.W = (m20 - m02) * num3;
                return quaternion;
            }
            var num5 = (float)Math.Sqrt(((1f + m22) - m00) - m11);
            var num2 = 0.5f / num5;
            quaternion.X = (m20 + m02) * num2;
            quaternion.Y = (m21 + m12) * num2;
            quaternion.Z = 0.5f * num5;
            quaternion.W = (m01 - m10) * num2;
            return quaternion;
        }

        Quaternion FromToRotation(Vector3 aFrom, Vector3 aTo)
        {
            Vector3 axis = Vector3.Cross(aFrom, aTo);
            float angle = Vector3.Angle(aFrom, aTo);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), angle);
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

        Quaternion AngleAxis(Vector3 aAxis, float aAngle)
        {
            aAxis = Vector3.Normalize(aAxis);
            float rad = aAngle * Deg2Rad * 0.5f;
            aAxis *= MathF.Sin(rad);
            return new Quaternion(aAxis.X, aAxis.Y, aAxis.Z, MathF.Cos(rad));
        }
    }
}
