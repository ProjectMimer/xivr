using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Dalamud;
using Dalamud.Game;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Game.ClientState.Conditions;
using System.Collections.Generic;
using xivr.Structures;

namespace xivr
{
    public class xivr : IDalamudPlugin
    {
        public static xivr Plugin { get; private set; }
        public string Name => "xivr";

        public static Configuration cfg { get; private set; }

        TitleScreenMenu.TitleScreenMenuEntry xivrMenuEntry;
        xivr_hooks xivr_hooks = new xivr_hooks();

        private readonly bool pluginReady = false;
        private bool isEnabled = false;
        private bool hasResized = false;
        private bool firstRun = false;
        private UInt64 counter = 0;
        private Point origWindowSize = new Point(0, 0);
        public bool doUpdate = false;
        public int alphaValue = 0;

        private static Dictionary<LanguageTypes, uiOptionStrings> rawLngData = new Dictionary<LanguageTypes, uiOptionStrings>() {
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
                    immersiveMovement_Line1 = "Immersive Mode - Movement Only",
                    immersiveFull_Line1 = "Immersive Mode - Full",
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
                    targetCursorSize_Line1 = "Target Cursor Size",
                    asymmetricProjection_Line1 = "Asymmetric Projection - Requires XIVR Restart",
                    ultrawideshadows_Line1 = "Ultrawide Shadows",
                    showWeaponInHand_Line1 = "Show Weapon In Hand"
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
                    immersiveMovement_Line1 = "Immersive Mode - Movement Only",
                    immersiveFull_Line1 = "Immersive Mode - Full",
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
                    targetCursorSize_Line1 = "ターゲットカーソルサイズ",
                    asymmetricProjection_Line1 = "非対称映写（2Dモード非対応）XIVR再起動必須",
                    ultrawideshadows_Line1 = "ウルトラワイド影",
                    showWeaponInHand_Line1 = "手の中の武器表示する"
                }
            }
        };

        public unsafe xivr(DalamudPluginInterface pluginInterface, TitleScreenMenu titleScreenMenu)
        {
            try
            {
                Plugin = this;
                DalamudApi.Initialize(this, pluginInterface);
                cfg = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
                cfg.Initialize(DalamudApi.PluginInterface);


                DalamudApi.Framework.Update += Update;
                DalamudApi.PluginInterface.UiBuilder.Draw += Draw;
                DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;

                try
                {
                    Assembly myAssembly = Assembly.GetExecutingAssembly();
                    Stream imgStream = myAssembly.GetManifestResourceStream("xivr.xivr.png");
                    if (imgStream != null)
                    {
                        var imgBytes = new byte[imgStream.Length];
                        imgStream.Read(imgBytes, 0, imgBytes.Length);
                        ImGuiScene.TextureWrap image = DalamudApi.PluginInterface.UiBuilder.LoadImage(imgBytes);
                        xivrMenuEntry = DalamudApi.TitleScreenMenu.AddEntry("xivr", image, ToggleConfig);
                    }

                    cfg.data.isEnabled = false;

                    try
                    {
                        Process[] pname = Process.GetProcessesByName("vrserver");
                        PluginLog.LogError($"SteamVR Active: {((pname.Length > 0) ? true : false)}");
                        if (pname.Length > 0 && cfg.data.isAutoEnabled)
                        {
                            cfg.data.isEnabled = true;
                        }

                        try
                        {
                            Marshal.PrelinkAll(typeof(xivr_hooks));
                            counter = 50;
                            Imports.UpdateConfiguration(cfg.data);
                            pluginReady = xivr_hooks.Initialize();

                            ClientLanguage curLng = DalamudApi.ClientState.ClientLanguage;
                            if (curLng == ClientLanguage.Japanese)
                                cfg.data.languageType = LanguageTypes.jp;
                            else
                                cfg.data.languageType = LanguageTypes.en;
                        }
                        catch (Exception e) { PluginLog.LogError($"Failed loading vr dll\n{e}"); }
                    }
                    catch (Exception e) { PluginLog.LogError($"Failed initalizing vr\n{e}"); }
                }
                catch (Exception e) { PluginLog.LogError($"Failed adding menu item\n{e}"); }
            }
            catch (Exception e) { PluginLog.LogError($"Failed loading plugin\n{e}"); }
        }

        public void ToggleConfig() => PluginUI.isVisible ^= true;

        private const string subcommands = "/xivr [ on | off | recenter | hlock | vlock | horizon ]";
        [Command("/xivr")]
        [HelpMessage("Opens / closes the config. Additional usage: " + subcommands)]
        private unsafe void CheckCommands(string command, string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                ToggleConfig();
                return;
            }

            var regex = Regex.Match(argument, "^(\\w+) ?(.*)");
            var subcommand = regex.Success && regex.Groups.Count > 1 ? regex.Groups[1].Value : string.Empty;

            switch (subcommand.ToLower())
            {
                case "on":
                    {
                        cfg.data.isEnabled = true;
                        break;
                    }
                case "off":
                    {
                        cfg.data.isEnabled = false;
                        break;
                    }
                case "recenter":
                    {
                        cfg.data.runRecenter = true;
                        break;
                    }
                case "screen":
                    {
                        cfg.data.forceFloatingScreen = !cfg.data.forceFloatingScreen;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "hlock":
                    {
                        cfg.data.horizontalLock = !cfg.data.horizontalLock;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "vlock":
                    {
                        cfg.data.verticalLock = !cfg.data.verticalLock;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "horizon":
                    {
                        cfg.data.horizonLock = !cfg.data.horizonLock;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "rotatex":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        xivr_hooks.SetRotateAmount(amount, 0);
                        break;
                    }
                case "rotatey":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        xivr_hooks.SetRotateAmount(0, amount);
                        break;
                    }
                case "offsetx":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        cfg.data.offsetAmountX = amount;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "offsety":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        cfg.data.offsetAmountY = amount;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "snapanglex":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        cfg.data.snapRotateAmountX = amount;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "snapangley":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        cfg.data.snapRotateAmountY = amount;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "uiz":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        cfg.data.uiOffsetZ = amount;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "uiscale":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        cfg.data.uiOffsetScale = amount;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "uidepth":
                    {
                        cfg.data.uiDepth = !cfg.data.uiDepth;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "uireset":
                    {
                        cfg.data.uiOffsetZ = 0.0f;
                        cfg.data.uiOffsetScale = 1.0f;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "conloc":
                    {
                        cfg.data.conloc = !cfg.data.conloc;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "swapeyes":
                    {
                        cfg.data.swapEyes = !cfg.data.swapEyes;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "swapeyesui":
                    {
                        cfg.data.swapEyesUI = !cfg.data.swapEyesUI;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "motcontoggle":
                    {
                        cfg.data.motioncontrol = !cfg.data.motioncontrol;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "ipd":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        cfg.data.ipdOffset = amount;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
                case "mode2d":
                    {
                        cfg.data.mode2d = !cfg.data.mode2d;
                        cfg.Save(); doUpdate = true;
                        break;
                    }
            }
        }

        private void Update(Framework framework)
        {
            if (pluginReady)
            {
                if (doUpdate == true)
                {
                    xivr_hooks.SetRenderingMode();
                    Imports.UpdateConfiguration(cfg.data);
                    //PluginLog.Log("Setup Complete");
                    doUpdate = false;
                }

                bool isCutscene = DalamudApi.Condition[ConditionFlag.OccupiedInCutSceneEvent] || DalamudApi.Condition[ConditionFlag.WatchingCutscene] || DalamudApi.Condition[ConditionFlag.WatchingCutscene78];
                bool forceFloating = cfg.data.forceFloatingScreen || (cfg.data.forceFloatingInCutscene && isCutscene);

                xivr_hooks.ForceFloatingScreen(forceFloating, isCutscene);

                if (cfg.data.isEnabled == true && isEnabled == false)
                {
                    //----
                    // Give the game a few seconds to update the buffers before enabling vr
                    //----
                    if (counter == 50)
                    {
                        origWindowSize = xivr_hooks.GetWindowSize();
                        if(cfg.data.vLog)
                            PluginLog.Log($"Saving ScreenSize {origWindowSize.X}x{origWindowSize.Y}");

                        if (cfg.data.autoResize && cfg.data.hmdWidth != 0 && cfg.data.hmdHeight != 0)
                        {
                            xivr_hooks.WindowResize(cfg.data.hmdWidth, cfg.data.hmdHeight);
                            hasResized = true;
                            PluginLog.Log($"Resizing window to: {cfg.data.hmdWidth}x{cfg.data.hmdHeight} from {origWindowSize.X}x{origWindowSize.Y}");
                        }
                        counter--;
                    }
                    else if (counter == 25)
                    {
                        xivr_hooks.Start();
                        Point hmdSize = Imports.GetBufferSize();
                        cfg.data.hmdWidth = hmdSize.X;
                        cfg.data.hmdHeight = hmdSize.Y;
                        cfg.Save();
                        PluginLog.Log($"Saving HMD Size {cfg.data.hmdWidth}x{cfg.data.hmdHeight}");
                        counter--;
                    }
                    else if (counter == 0)
                    {
                        doUpdate = true;
                        isEnabled = true;
                        counter--;
                    }
                    else if (counter >= 0)
                    {
                        counter--;
                    }
                }
                else if (cfg.data.isEnabled == false && isEnabled == true)
                {
                    xivr_hooks.Stop();
                    if (hasResized == true)
                    {
                        xivr_hooks.WindowResize(origWindowSize.X, origWindowSize.Y);
                        PluginLog.Log($"Resizing window to: {origWindowSize.X}x{origWindowSize.Y}");
                        hasResized = false;
                    }
                    isEnabled = false;
                    counter = 50;
                }
                if (cfg.data.runRecenter == true)
                {
                    cfg.data.runRecenter = false;
                    Imports.Recenter();
                }

                xivr_hooks.Update(framework);
            }
        }

        private void Draw()
        {
            if (pluginReady)
            {
                PluginUI.Draw(rawLngData[xivr.cfg.data.languageType]);
            }
        }

        public void Dispose()
        {
            firstRun = false;
            if (pluginReady)
            {
                xivr_hooks.Stop();
                xivr_hooks.Dispose();
                if (hasResized == true)
                {
                    xivr_hooks.WindowResize(origWindowSize.X, origWindowSize.Y);
                    PluginLog.Log($"Resizing window to: {origWindowSize.X}x{origWindowSize.Y}");
                    hasResized = false;
                }
            }
            DalamudApi.TitleScreenMenu.RemoveEntry(xivrMenuEntry);
            DalamudApi.Framework.Update -= Update;
            DalamudApi.PluginInterface.UiBuilder.Draw -= Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
            DalamudApi.Dispose();
        }
    }
}