#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <DirectXMath.h>
#include <d3d11_4.h>

struct stCameraBase
{
    /* 0x000 */ unsigned long long* vtbl;
    /* 0x008 */ unsigned long long uk1;
};

struct stRawGameCamera
{
    /* 0x000 */ unsigned long long* vtbl;
    /* 0x008 */ byte spcr1[0x50 - 0x08];
    /* 0x050 */ float X;
    /* 0x054 */ float Y;
    /* 0x058 */ float Z;
    /* 0x05C */ byte spr2[0x80 - 0x5C];
    /* 0x080 */ float LookAtX;
    /* 0x084 */ float LookAtY;
    /* 0x088 */ float LookAtZ;
    /* 0x08C */ float uk1;
    /* 0x090 */ float RotateX;
    /* 0x094 */ float RotateY;
    /* 0x098 */ float RotateZ;
    /* 0x09C */ float uk2;
    /* 0x0A0 */  XMMATRIX ViewMatrix;
    /* 0x0E0 */ unsigned long long* BufferData;
    /* 0x0E8 */ byte spcr2[0x104 - 0xE8];
    /* 0x104 */ float CurrentZoom;
    /* 0x108 */ float MinZoom;
    /* 0x10C */ float MaxZoom;
    /* 0x110 */ float CurrentFoV;
    /* 0x114 */ float MinFoV;
    /* 0x118 */ float MaxFoV;
    /* 0x11C */ float AddedFoV;
    /* 0x120 */ float CurrentHRotation;
    /* 0x124 */ float CurrentVRotation;
    /* 0x128 */ float HRotationThisFrame1;
    /* 0x12C */ float VRotationThisFrame1;
    /* 0x130 */ float HRotationThisFrame2;
    /* 0x134 */ float VRotationThisFrame2;
    /* 0x138 */ float MinVRotation;
    /* 0x13C */ float MaxVRotation;
    /* 0x150 */ float Tilt;
    /* 0x154 */ byte spcr3[0x160 - 0x154];
    /* 0x160 */ int Mode;
    /* 0x164 */ byte spcr4[0x16C - 0x164];
    /* 0x16C */ float InterpolatedZoom;
    /* 0x170 */ byte spcr5[0x1A0 - 0x170];
    /* 0x1A0 */ float ViewX;
    /* 0x1A4 */ float ViewY;
    /* 0x1A8 */ float ViewZ;
};


struct stGameCamera
{
    /* 0x000 */ stCameraBase CameraBase;
    /* 0x010 */ stRawGameCamera Camera;
};

struct stLowCutCamera
{
    /* 0x000 */ stCameraBase CameraBase;
    /* 0x010 */ stRawGameCamera Camera;
};

struct stLobbyCamera
{
    /* 0x000 */ stCameraBase CameraBase;
    /* 0x010 */ stRawGameCamera Camera;
    /* 0x2F8 */ //void* LobbyExcelSheet;
};

struct stCamera3
{
    /* 0x000 */ stCameraBase CameraBase;
    /* 0x010 */ stRawGameCamera Camera;
};

struct stCamera4
{
    /* 0x000 */ stCameraBase CameraBase;
    /* 0x010 */ stRawGameCamera Camera;
};

struct stCameraManager
{
    /* 0x000 */ stGameCamera* GameCamera;
    /* 0x008 */ stLowCutCamera* LowCutCamera;
    /* 0x010 */ stLobbyCamera* LobbyCamera;
    /* 0x018 */ stCamera3* Camera3;
    /* 0x020 */ stCamera4* Camera4;
    /* 0x028 */ byte spcr1[0x48 - 0x28];
    /* 0x048 */ int ActiveCameraIndex;
    /* 0x04C */ int PreviousCameraIndex;
    /* 0x050 */ byte spcr2[0x60 - 0x50];
    /* 0x060 */ unsigned long long CameraBase;
};



