#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

struct Configuration
{
    bool isEnabled;
    bool isAutoEnabled;
    bool forceFloatingScreen;
    bool forceFloatingInCutscene;
    bool horizontalLock;
    bool verticalLock;
    bool horizonLock;
    bool runRecenter;
    float offsetAmountX;
    float offsetAmountY;
    float snapRotateAmountX;
    float snapRotateAmountY;
    float uiOffsetZ;
    float uiOffsetScale;
    bool conloc;
    bool swapEyes;
    bool swapEyesUI;
    bool motioncontrol;
    int hmdWidth;
    int hmdHeight;
    bool autoResize;
    float ipdOffset;
    bool vLog;
};