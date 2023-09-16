using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace xivr.Structures
{
    [StructLayout(LayoutKind.Explicit)]
    struct stRenderQueueCommandClear
    {
        [FieldOffset(0x00)] public int SwitchType;
        [FieldOffset(0x04)] public int clearType;
        [FieldOffset(0x08)] public float colorR;
        [FieldOffset(0x0C)] public float colorG;
        [FieldOffset(0x10)] public float colorB;
        [FieldOffset(0x14)] public float colorA;
        [FieldOffset(0x18)] public float unkn1;
        [FieldOffset(0x1C)] public float unkn2;
        [FieldOffset(0x20)] public int clearCheck;
        [FieldOffset(0x24)] public float unkn4;
        [FieldOffset(0x28)] public float unkn5;
        [FieldOffset(0x2C)] public float unkn6;
        [FieldOffset(0x30)] public float unkn7;
        [FieldOffset(0x34)] public float unkn8;
        [FieldOffset(0x38)] public float unkn9;

        public void Clear()
        {
            SwitchType = 4;
            clearType = 0;
            colorR = 0;
            colorG = 0;
            colorB = 0;
            colorA = 0;
            unkn1 = 0;
            unkn2 = 0;
            clearCheck = 0;
            unkn4 = 0;
            unkn5 = 0;
            unkn6 = 0;
            unkn7 = 0;
            unkn8 = 0;
            unkn9 = 0;
        }
    };

    [StructLayout(LayoutKind.Explicit)]
    public struct Matrix3x4
    {
        [FieldOffset(0x00)] public float M11;
        [FieldOffset(0x04)] public float M12;
        [FieldOffset(0x08)] public float M13;
        [FieldOffset(0x0C)] public float M14;
        [FieldOffset(0x10)] public float M21;
        [FieldOffset(0x14)] public float M22;
        [FieldOffset(0x18)] public float M23;
        [FieldOffset(0x1C)] public float M24;
        [FieldOffset(0x20)] public float M31;
        [FieldOffset(0x24)] public float M32;
        [FieldOffset(0x28)] public float M33;
        [FieldOffset(0x2C)] public float M34;

        public Matrix3x4(float n11, float n12, float n13, float n14, float n21, float n22, float n23, float n24, float n31, float n32, float n33, float n34)
        {
            M11 = n11; M12 = n12; M13 = n13; M14 = n14;
            M21 = n21; M22 = n22; M23 = n23; M24 = n24;
            M31 = n31; M32 = n32; M33 = n33; M34 = n34;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct ptrShaderData
    {
        [FieldOffset(0x00)] public RawShaderData* ShaderData;
    };

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct RawShaderData
    {
        [FieldOffset(0x00)] public Matrix3x4 ViewMatrix;
        [FieldOffset(0x30)] public Matrix3x4 InvViewMatrix;
        [FieldOffset(0x60)] public Matrix4x4 ViewProjectionMatrix;
        [FieldOffset(0xA0)] public Matrix4x4 InvViewProjectionMatrix;
        [FieldOffset(0xE0)] public Matrix4x4 InvProjectionMatrix;
        [FieldOffset(0x120)] public Matrix4x4 ProjectionMatrix;
        [FieldOffset(0x160)] public Matrix4x4 MainViewToProjectionMatrix;
        [FieldOffset(0x1A0)] public Vector4 EyePos;
        [FieldOffset(0x1A0)] public Vector4 LookAt;
    };


    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct cmdList
    {
        [FieldOffset(0x00)] public cmdGroup[] list;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 0x10)]
    public unsafe struct cmdGroup
    {
        [FieldOffset(0x00)] public int unk1;
        [FieldOffset(0x04)] public int unk2;
        [FieldOffset(0x08)] public cmdType* Type;
        [FieldOffset(0x08)] public cmdType0* SetRenderTarget;
        [FieldOffset(0x08)] public cmdType1* Viewport;
        [FieldOffset(0x08)] public cmdType3* Scissors;
        [FieldOffset(0x08)] public cmdType4* Clear;
        [FieldOffset(0x08)] public cmdType5* Draw;
        [FieldOffset(0x08)] public cmdType6* DrawIndex;
        [FieldOffset(0x08)] public cmdType7* DrawIndexInstance;
        [FieldOffset(0x08)] public cmdType10* CopyResource;
        [FieldOffset(0x08)] public cmdUpdateVR* UpdateVR;
    };

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct cmdUnion
    {
        [FieldOffset(0x00)] public cmdType Type;
        [FieldOffset(0x00)] public cmdType0 SetRenderTarget;
        [FieldOffset(0x00)] public cmdType1 Viewport;
        [FieldOffset(0x00)] public cmdType3 Scissors;
        [FieldOffset(0x00)] public cmdType4 Clear;
        [FieldOffset(0x00)] public cmdType5 Draw;
        [FieldOffset(0x00)] public cmdType6 DrawIndex;
        [FieldOffset(0x00)] public cmdType7 DrawIndexInstance;
        [FieldOffset(0x00)] public cmdType10 CopyResource;
        [FieldOffset(0x00)] public cmdUpdateVR UpdateVR;
    };


    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct cmdType
    {
        [FieldOffset(0x00)] public int SwitchType;
    };

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct cmdType0 // SetRendTarget
    {
        [FieldOffset(0x00)] public int SwitchType;
        [FieldOffset(0x04)] public int numRenderTargets;
        [FieldOffset(0x08)] public Texture* RenderTarget0;
        [FieldOffset(0x10)] public Texture* RenderTarget1;
        [FieldOffset(0x18)] public Texture* RenderTarget2;
        [FieldOffset(0x20)] public Texture* RenderTarget3;
        [FieldOffset(0x28)] public Texture* DepthBuffer;
        [FieldOffset(0x38)] public float unk3;
        [FieldOffset(0x3C)] public float unk4;
    };

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct cmdType1 // Viewport
    {
        [FieldOffset(0x00)] public int SwitchType;
        [FieldOffset(0x04)] public int TopLeftY;
        [FieldOffset(0x08)] public int TopLeftX;
        [FieldOffset(0x0C)] public int BottomRightY;
        [FieldOffset(0x10)] public int BottomRightX;
        [FieldOffset(0x14)] public float MinDepth;
        [FieldOffset(0x18)] public float MaxDepth;
    };

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct cmdType3 // ScissorsRect
    {
        [FieldOffset(0x00)] public int SwitchType;
        [FieldOffset(0x04)] public uint left;
        [FieldOffset(0x08)] public uint top;
        [FieldOffset(0x0C)] public uint right;
        [FieldOffset(0x10)] public uint bottom;
    };

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct cmdType4 // ClearRendDepth
    {
        [FieldOffset(0x00)] public int SwitchType;
        [FieldOffset(0x04)] public int clearType;
        [FieldOffset(0x08)] public float colorB;
        [FieldOffset(0x0C)] public float colorG;
        [FieldOffset(0x10)] public float colorR;
        [FieldOffset(0x14)] public float colorA;
        [FieldOffset(0x18)] public float clearDepth;
        [FieldOffset(0x1C)] public int clearStencil;
        [FieldOffset(0x20)] public int clearCheck;
        [FieldOffset(0x24)] public float Top;
        [FieldOffset(0x28)] public float Left;
        [FieldOffset(0x2C)] public float Width;
        [FieldOffset(0x30)] public float Height;
        [FieldOffset(0x34)] public float MinZ;
        [FieldOffset(0x38)] public float MaxZ;
    };

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct cmdType5 // Draw
    {
        [FieldOffset(0x00)] public int SwitchType;
        [FieldOffset(0x1C)] public uint uk1;
        [FieldOffset(0x1F)] public byte uk4;
        [FieldOffset(0x6C)] public ptrShaderData* ptrCurrentShaderData;
    };

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct cmdType6 // DrawIndexed
    {
        [FieldOffset(0x00)] public int SwitchType;
        [FieldOffset(0x1C)] public uint uk1;
        [FieldOffset(0x1F)] public byte uk4;
        [FieldOffset(0x6C)] public ptrShaderData* ptrCurrentShaderData;
    };

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct cmdType7 // DrawIndexedInstance
    {
        [FieldOffset(0x00)] public int SwitchType;
        [FieldOffset(0x20)] public uint uk1;
        [FieldOffset(0x23)] public byte uk4;
        [FieldOffset(0x70)] public ptrShaderData* ptrCurrentShaderData;
    };

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct cmdType10 // CopyResource
    {
        [FieldOffset(0x00)] public int SwitchType;
        [FieldOffset(0x04)] public int uk1;
        [FieldOffset(0x08)] public Texture* Destination;
        [FieldOffset(0x10)] public uint subResourceDestination;
        [FieldOffset(0x14)] public uint X;
        [FieldOffset(0x18)] public uint Y;
        [FieldOffset(0x1C)] public uint Z;
        [FieldOffset(0x20)] public Texture* Source;
        [FieldOffset(0x28)] public uint subResourceSource;
        [FieldOffset(0x30)] public float useRect;
        [FieldOffset(0x38)] public float rectTop;
        [FieldOffset(0x3C)] public float rectLeft;
        [FieldOffset(0x40)] public float rectBottom;
        [FieldOffset(0x44)] public float rectRight;
    };


    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct cmdUpdateVR
    {
        [FieldOffset(0x00)] public int SwitchType;
        [FieldOffset(0x04)] public int eye;
        [FieldOffset(0x08)] public UInt64 nextThreadedBuffer;
        [FieldOffset(0x10)] public UInt64 nextThreadedBufferL;
        [FieldOffset(0x18)] public UInt64 nextThreadedBufferR;
    };
}

