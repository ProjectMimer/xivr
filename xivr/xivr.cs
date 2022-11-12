using System;
using System.IO;
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
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace xivr
{
    public class xivr : IDalamudPlugin
    {
        public static xivr Plugin { get; private set; }
        public string Name => "xivr";

        public static Configuration Configuration { get; private set; }

        TitleScreenMenu.TitleScreenMenuEntry xivrMenuEntry;
        xivr_hooks xivr_hooks = new xivr_hooks();

        private readonly bool pluginReady = false;
        private bool isEnabled = false;
        private UInt64 counter = 0;

        public unsafe xivr(DalamudPluginInterface pluginInterface, TitleScreenMenu titleScreenMenu)
        {
            try
            {
                Plugin = this;
                DalamudApi.Initialize(this, pluginInterface);
                Configuration = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
                Configuration.Initialize(DalamudApi.PluginInterface);

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

                    Configuration.isEnabled = false;
                    /*
                    PluginLog.Log($"isEnabled: {Configuration.isEnabled}");
                    PluginLog.Log($"isAutoEnabled: {Configuration.isAutoEnabled}");
                    PluginLog.Log($"forceFloatingScreen: {Configuration.forceFloatingScreen}");
                    PluginLog.Log($"forceFloatingInCutscene: {Configuration.forceFloatingInCutscene}");
                    PluginLog.Log($"horizontalLock: {Configuration.horizontalLock}");
                    PluginLog.Log($"verticalLock: {Configuration.verticalLock}");
                    PluginLog.Log($"horizonLock: {Configuration.horizonLock}");
                    PluginLog.Log($"offsetAmountX: {Configuration.offsetAmountX}");
                    PluginLog.Log($"offsetAmountY: {Configuration.offsetAmountY}");
                    PluginLog.Log($"snapRotateAmountX: {Configuration.snapRotateAmountX}");
                    PluginLog.Log($"snapRotateAmount:Y {Configuration.snapRotateAmountY}");
                    PluginLog.Log($"uiOffsetZ: {Configuration.uiOffsetZ}");
                    PluginLog.Log($"uiOffsetScale: {Configuration.uiOffsetScale}");
                    */
                    try
                    {
                        Process[] pname = Process.GetProcessesByName("vrserver");
                        PluginLog.LogError($"ProcessName {pname.Length}");
                        if (pname.Length > 0 && Configuration.isAutoEnabled)
                        {
                            Configuration.isEnabled = true;
                        }

                        xivr_hooks.Initialize();

                        pluginReady = true;
                    }
                    catch (Exception e) { PluginLog.LogError($"Failed initalizing vr\n{e}"); }
                }
                catch (Exception e) { PluginLog.LogError($"Failed adding menu item\n{e}"); }
            }
            catch (Exception e) { PluginLog.LogError($"Failed loading plugin\n{e}"); }
        }

        public void ToggleConfig() => PluginUI.isVisible ^= true;

        private const string subcommands = "/xivr [ on | off ]";
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
                        Configuration.isEnabled = true;
                        break;
                    }
                case "off":
                    {
                        Configuration.isEnabled = false;
                        break;
                    }
                case "recenter":
                    {
                        Configuration.runRecenter = true;
                        break;
                    }
                case "screen":
                    {
                        Configuration.forceFloatingScreen = !Configuration.forceFloatingScreen;
                        break;
                    }
                case "hlock":
                    {
                        Configuration.horizontalLock = !Configuration.horizontalLock;
                        break;
                    }
                case "vlock":
                    {
                        Configuration.verticalLock = !Configuration.verticalLock;
                        break;
                    }
                case "horizon":
                    {
                        Configuration.horizonLock = !Configuration.horizonLock;
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
                        Configuration.offsetAmountX = amount;
                        Configuration.Save();
                        xivr_hooks.SetOffsetAmount(Configuration.offsetAmountX, Configuration.offsetAmountY);
                        break;
                    }
                case "offsety":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        Configuration.offsetAmountY = amount;
                        Configuration.Save();
                        xivr_hooks.SetOffsetAmount(Configuration.offsetAmountX, Configuration.offsetAmountY);
                        break;
                    }
                case "snapanglex":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        Configuration.snapRotateAmountX = amount;
                        Configuration.Save();
                        xivr_hooks.SetSnapAmount(Configuration.snapRotateAmountX, Configuration.snapRotateAmountY);
                        break;
                    }
                case "snapangley":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        Configuration.snapRotateAmountY = amount;
                        Configuration.Save();
                        xivr_hooks.SetOffsetAmount(Configuration.snapRotateAmountX, Configuration.snapRotateAmountY);
                        break;
                    }
                case "uiz":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        Configuration.uiOffsetZ = amount;
                        Configuration.Save();
                        xivr_hooks.SetZScale(Configuration.uiOffsetZ, Configuration.uiOffsetScale);
                        break;
                    }
                case "uiscale":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        Configuration.uiOffsetScale = amount;
                        Configuration.Save();
                        xivr_hooks.SetZScale(Configuration.uiOffsetZ, Configuration.uiOffsetScale);
                        break;
                    }
                case "uireset":
                    {
                        Configuration.uiOffsetZ = 0.0f;
                        Configuration.uiOffsetScale = 1.0f;
                        Configuration.Save();
                        xivr_hooks.SetZScale(Configuration.uiOffsetZ, Configuration.uiOffsetScale);
                        break;
                    }
                case "conloc":
                    {
                        Configuration.conloc = !Configuration.conloc;
                        Configuration.Save();
                        xivr_hooks.SetConLoc(Configuration.conloc);
                        break;
                    }
                case "swapeyes":
                    {
                        Configuration.swapEyes = !Configuration.swapEyes;
                        Configuration.Save();
                        xivr_hooks.DoSwapEyes(Configuration.swapEyes);
                        break;
                    }
                case "swapeyesui":
                    {
                        Configuration.swapEyesUI = !Configuration.swapEyesUI;
                        Configuration.Save();
                        xivr_hooks.DoSwapEyesUI(Configuration.swapEyesUI);
                        break;
                    }
                case "motcontoggle":
                    {
                        Configuration.motioncontrol = !Configuration.motioncontrol;
                        Configuration.Save();
                        xivr_hooks.ToggleMotionControls(Configuration.motioncontrol);
                        break;
                    }
            }
        }

        private void Update(Framework framework)
        {
            if (pluginReady)
            {
                if(counter == 300)
                {
                    counter++;
                    xivr_hooks.SetOffsetAmount(Configuration.offsetAmountX, Configuration.offsetAmountY);
                    xivr_hooks.SetSnapAmount(Configuration.snapRotateAmountX, Configuration.snapRotateAmountY);
                    xivr_hooks.SetZScale(Configuration.uiOffsetZ, Configuration.uiOffsetScale);
                    xivr_hooks.SetConLoc(Configuration.conloc);
                    xivr_hooks.DoSwapEyes(Configuration.swapEyes);
                    xivr_hooks.DoSwapEyesUI(Configuration.swapEyesUI);
                    xivr_hooks.ToggleMotionControls(Configuration.motioncontrol);
                    PluginLog.Log("Setup Complete");
                } 
                else if(counter <= 300)
                {
                    counter++;
                }
                

                bool isCutscene = DalamudApi.Condition[ConditionFlag.OccupiedInCutSceneEvent] || DalamudApi.Condition[ConditionFlag.WatchingCutscene] || DalamudApi.Condition[ConditionFlag.WatchingCutscene78];
                bool forceFloating = Configuration.forceFloatingScreen || (Configuration.forceFloatingInCutscene && isCutscene);

                xivr_hooks.ForceFloatingScreen(forceFloating);
                xivr_hooks.SetLocks(Configuration.horizontalLock, Configuration.verticalLock, Configuration.horizonLock);
                xivr_hooks.Update(framework);

                if (Configuration.isEnabled == true && isEnabled == false)
                {
                    xivr_hooks.Start();
                    isEnabled = true;
                }
                else if (Configuration.isEnabled == false && isEnabled == true)
                {
                    xivr_hooks.Stop();
                    isEnabled = false;
                }
                if (Configuration.runRecenter == true)
                {
                    Configuration.runRecenter = false;
                    xivr_hooks.Recenter();
                }
            }
        }

        private void Draw()
        {
            if (pluginReady)
            {
                PluginUI.Draw();
                xivr_hooks.Draw();
            }
        }

        public void Dispose()
        {
            counter = 0;
            if (pluginReady)
                xivr_hooks.Dispose();
            DalamudApi.TitleScreenMenu.RemoveEntry(xivrMenuEntry);
            DalamudApi.Framework.Update -= Update;
            DalamudApi.PluginInterface.UiBuilder.Draw -= Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
            DalamudApi.Dispose();
        }

    }
}
