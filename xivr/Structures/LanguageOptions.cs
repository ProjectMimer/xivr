using System.Collections.Generic;

namespace xivr.Structures
{
    public struct uiOptionStrings
    {
        public string isEnabled_Line1;
        public string isAutoEnabled_Line1;
        public string forceFloatingScreen_Line1;
        public string forceFloatingInCutscene_Line1;
        public string horizontalLock_Line1;
        public string verticalLock_Line1;
        public string horizonLock_Line1;
        public string runRecenter_Line1;
        public string offsetAmountX_Line1;
        public string offsetAmountY_Line1;
        public string offsetAmountYFPS_Line1;
        public string offsetAmountZFPS_Line1;
        public string offsetAmountYFPSMount_Line1;
        public string offsetAmountZFPSMount_Line1;
        public string snapRotateAmountX_Line1;
        public string snapRotateAmountX_Line2;
        public string snapRotateAmountX_Line3;
        public string snapRotateAmountY_Line1;
        public string snapRotateAmountY_Line2;
        public string snapRotateAmountY_Line3;
        public string uiOffsetZ_Line1;
        public string uiOffsetScale_Line1;
        public string conloc_Line1;
        public string swapEyes_Line1;
        public string swapEyesUI_Line1;
        public string motioncontrol_Line1;
        public string hmdWidth_Line1;
        public string hmdHeight_Line1;
        public string autoResize_Line1;
        public string ipdOffset_Line1;
        public string vLog_Line1;
        public string hmdloc_Line1;
        public string vertloc_Line1;
        public string targetCursorSize_Line1;
        public string offsetAmountZ_Line1;
        public string uiDepth_Line1;
        public string hmdPointing_Line1;
        public string mode2d_Line1;
        public string asymmetricProjection_Line1;
        public string immersiveMovement_Line1;
        public string immersiveFull_Line1;
        public string languageType_Line1;
        public string ultrawideshadows_Line1;
        public string showWeaponInHand_Line1;
        public string support_Line1;
        public string autoMove_Line1;
        public string enableOSK_Line1;
        public string armMultiplier_Line1;
    }

    public static class Language
    {
        public static Dictionary<LanguageTypes, uiOptionStrings> rawLngData = new Dictionary<LanguageTypes, uiOptionStrings>() {
            {
            LanguageTypes.en, new uiOptionStrings
                {
                    isEnabled_Line1 = "Enable",
                    isAutoEnabled_Line1 = "Auto Enable if steamvr is active",
                    mode2d_Line1 = "2D Mode",
                    autoResize_Line1 = "Auto Resize when activated",
                    runRecenter_Line1 = "Recenter",
                    vLog_Line1 = "Verbose Logs",
                    motioncontrol_Line1 = "Enable motion controllers",
                    conloc_Line1 = "Enable 1st person controller locomotion",
                    hmdloc_Line1 = "Enable 1st person headset locomotion",
                    vertloc_Line1 = "1st person locomotion allow vertical movement",
                    forceFloatingScreen_Line1 = "Flat mode",
                    forceFloatingInCutscene_Line1 = "Flat mode in Cutscene",
                    immersiveMovement_Line1 = "Immersive Mode",
                    horizonLock_Line1 = "Keep headset level with horizon",
                    snapRotateAmountX_Line1 = "Horizontal Snap",
                    snapRotateAmountX_Line2 = "Amount",
                    snapRotateAmountY_Line1 = "Vertical Snap",
                    snapRotateAmountY_Line2 = "Amount",
                    uiOffsetZ_Line1 = "UI Distance",
                    uiOffsetScale_Line1 = "UI Size",
                    uiDepth_Line1 = "Enable UI Depth",
                    ipdOffset_Line1 = "IPD Offset",
                    swapEyes_Line1 = "Swap Eyes",
                    swapEyesUI_Line1 = "Swap Eyes UI",
                    offsetAmountX_Line1 = "3rd Person X Offset",
                    offsetAmountY_Line1 = "3rd Person Y Offset",
                    offsetAmountZ_Line1 = "3rd Person Z Offset",
                    offsetAmountYFPS_Line1 = "1st Person Y Offset",
                    offsetAmountZFPS_Line1 = "1st Person Z Offset",
                    offsetAmountYFPSMount_Line1 = "1st Person Mount Y Offset",
                    offsetAmountZFPSMount_Line1 = "1st Person Mount Z Offset",
                    targetCursorSize_Line1 = "Target Cursor Size",
                    asymmetricProjection_Line1 = "Asymmetric Projection - Requires XIVR Restart",
                    ultrawideshadows_Line1 = "Ultrawide Shadows",
                    showWeaponInHand_Line1 = "Show Weapon In Hand",
                    support_Line1 = "Support via Ko-fi",
                    autoMove_Line1 = "Auto Move when activated",
                    enableOSK_Line1 = "Virtual Keyboard (Requires Admin)",
                    armMultiplier_Line1 = "IK Hand Distance Scale"
                }
            },
            {
            LanguageTypes.jp, new uiOptionStrings
                {
                    isEnabled_Line1 = "VR有効化",
                    isAutoEnabled_Line1 = "SteamVR実行中の場合はVR自動有効化",
                    mode2d_Line1 = "2Dモード",
                    autoResize_Line1 = "自動ウインドウサイズ変更",
                    runRecenter_Line1 = "頭位置リセット",
                    vLog_Line1 = "詳細ログ生成",
                    motioncontrol_Line1 = "モーションコントローラーを有効にする",
                    conloc_Line1 = "一人称左手移動操作",
                    hmdloc_Line1 = "一人称頭移動操作",
                    vertloc_Line1 = "一人称移動上下操作を有効にする",
                    forceFloatingScreen_Line1 = "平面モード",
                    forceFloatingInCutscene_Line1 = "ムービー中平面モード",
                    immersiveMovement_Line1 = "没入モード",
                    horizonLock_Line1 = "地平線と水平を保つ",
                    snapRotateAmountX_Line1 = "水平スナップ回転",
                    snapRotateAmountX_Line2 = "度数",
                    snapRotateAmountY_Line1 = "垂直スナップ回転",
                    snapRotateAmountY_Line2 = "度数",
                    uiOffsetZ_Line1 = "メニュー距離",
                    uiOffsetScale_Line1 = "メニューサイズ",
                    uiDepth_Line1 = "メニュー奥行き",
                    ipdOffset_Line1 = "瞳孔間距離（IPD）補正",
                    swapEyes_Line1 = "目を入れ替える　世界",
                    swapEyesUI_Line1 = "目を入れ替える　メニュー",
                    offsetAmountX_Line1 = "三人称X軸補正",
                    offsetAmountY_Line1 = "三人称Y軸補正",
                    offsetAmountZ_Line1 = "三人称Z軸補正",
                    offsetAmountYFPS_Line1 = "一人称Y軸補正",
                    offsetAmountZFPS_Line1 = "一人称Z軸補正",
                    offsetAmountYFPSMount_Line1 = "一人称マウントY軸補正",
                    offsetAmountZFPSMount_Line1 = "一人称マウントZ軸補正",
                    targetCursorSize_Line1 = "ターゲットカーソルサイズ",
                    asymmetricProjection_Line1 = "非対称映写（2Dモード非対応）XIVR再起動必須",
                    ultrawideshadows_Line1 = "ウルトラワイド影",
                    showWeaponInHand_Line1 = "手の中の武器表示する",
                    support_Line1 = "Ko-fiで支援",
                    autoMove_Line1 = "自動ウィンドウ位置調整",
                    enableOSK_Line1 = "バーチャルキーボード（管理者権限必要",
                    armMultiplier_Line1 = "IK手距離スケール"
                }
            }
        };
    }
}
