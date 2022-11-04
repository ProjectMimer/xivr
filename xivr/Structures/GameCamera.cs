using System;
using System.Runtime.InteropServices;

namespace xivr.Structures
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct GameCamera
    {
        [FieldOffset(0x0)] public IntPtr* VTable;
        [FieldOffset(0x50)] public float X;
        [FieldOffset(0x54)] public float Z;
        [FieldOffset(0x58)] public float Y;
        [FieldOffset(0x80)] public float LookAtX; // Position that the camera is focused on (Actual position when zoom is 0)
        [FieldOffset(0x84)] public float LookAtZ;
        [FieldOffset(0x98)] public float LookAtY;
        [FieldOffset(0x104)] public float CurrentZoom; // 6
        [FieldOffset(0x108)] public float MinZoom; // 1.5
        [FieldOffset(0x10C)] public float MaxZoom; // 20
        [FieldOffset(0x110)] public float CurrentFoV; // 0.78
        [FieldOffset(0x114)] public float MinFoV; // 0.69
        [FieldOffset(0x118)] public float MaxFoV; // 0.78
        [FieldOffset(0x11C)] public float AddedFoV; // 0
        [FieldOffset(0x120)] public float CurrentHRotation; // -pi -> pi, default is pi
        [FieldOffset(0x124)] public float CurrentVRotation; // -0.349066
        [FieldOffset(0x124)] public float HRotationThisFrame1;
        [FieldOffset(0x124)] public float VRotationThisFrame1;
        [FieldOffset(0x124)] public float HRotationThisFrame2;
        [FieldOffset(0x124)] public float VRotationThisFrame2;
        //[FieldOffset(0x138)] public float HRotationDelta;
        [FieldOffset(0x138)] public float MinVRotation; // -1.483530, should be -+pi/2 for straight down/up but camera breaks so use -+1.569
        [FieldOffset(0x13C)] public float MaxVRotation; // 0.785398 (pi/4)
        [FieldOffset(0x150)] public float Tilt;
        [FieldOffset(0x160)] public int Mode; // Camera mode? (0 = 1st person, 1 = 3rd person, 2+ = weird controller mode? cant look up/down)
        //[FieldOffset(0x174)] public int ControlType; // 0 first person, 1 legacy, 2 standard, 3/5/6 ???, 4 ???
        [FieldOffset(0x16C)] public float InterpolatedZoom;
        [FieldOffset(0x1A0)] public float ViewX;
        [FieldOffset(0x1A4)] public float ViewZ;
        [FieldOffset(0x1A8)] public float ViewY;
        //[FieldOffset(0x1E4)] public byte FlipCamera; // 1 while holding the keybind
        [FieldOffset(0x214)] public float LookAtHeightOffset; // No idea what to call this (0x230 is the interpolated value)
        [FieldOffset(0x218)] public byte ResetLookatHeightOffset; // No idea what to call this
        //[FieldOffset(0x230)] public float InterpolatedLookAtHeightOffset;
        [FieldOffset(0x2A4)] public float LookAtZ2;
    }
}