﻿using System;
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
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static xivr.Configuration;

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

        

        public unsafe xivr(DalamudPluginInterface pluginInterface, TitleScreenMenu titleScreenMenu)
        {
            try
            {
                Plugin = this;
                DalamudApi.Initialize(this, pluginInterface);
                cfg = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
                cfg.Initialize(DalamudApi.PluginInterface);


                DalamudApi.Framework.Update += Update;
                DalamudApi.ClientState.Login += OnLogin;
                DalamudApi.ClientState.Logout += OnLogout;
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
                case "resetconfig":
                    {
                        cfg.data = new cfgData();
                        cfg.Save();
                        doUpdate = true;
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

        private void OnLogin(object? sender, EventArgs e)
        {
            if (pluginReady)
                xivr_hooks.OnLogin(sender, e);
        }
        private void OnLogout(object? sender, EventArgs e)
        {
            if (pluginReady)
                xivr_hooks.OnLogout(sender, e);
        }

        private void Draw()
        {
            if (pluginReady)
            {
                PluginUI.Draw(Language.rawLngData[xivr.cfg.data.languageType]);
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
            DalamudApi.ClientState.Login -= OnLogin;
            DalamudApi.ClientState.Logout -= OnLogout;
            DalamudApi.PluginInterface.UiBuilder.Draw -= Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
            DalamudApi.Dispose();
        }
    }
}