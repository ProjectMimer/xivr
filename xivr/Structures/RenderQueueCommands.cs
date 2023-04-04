using System;
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
}

