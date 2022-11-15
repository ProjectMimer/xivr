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
        private bool firstRun = false;
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

                    try
                    {
                        Process[] pname = Process.GetProcessesByName("vrserver");
                        PluginLog.LogError($"ProcessName {pname.Length}");
                        if (pname.Length > 0 && Configuration.isAutoEnabled)
                        {
                            Configuration.isEnabled = true;
                        }

                        try
                        {
                            Marshal.PrelinkAll(typeof(xivr_hooks));
                            pluginReady = xivr_hooks.Initialize();
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
                        LoadSettings();
                        break;
                    }
                case "offsety":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        Configuration.offsetAmountY = amount;
                        Configuration.Save();
                        LoadSettings();
                        break;
                    }
                case "snapanglex":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        Configuration.snapRotateAmountX = amount;
                        Configuration.Save();
                        LoadSettings();
                        break;
                    }
                case "snapangley":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        Configuration.snapRotateAmountY = amount;
                        Configuration.Save();
                        LoadSettings();
                        break;
                    }
                case "uiz":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        Configuration.uiOffsetZ = amount;
                        Configuration.Save();
                        LoadSettings();
                        break;
                    }
                case "uiscale":
                    {
                        float.TryParse(regex.Groups[2].Value, out var amount);
                        Configuration.uiOffsetScale = amount;
                        Configuration.Save();
                        LoadSettings();
                        break;
                    }
                case "uireset":
                    {
                        Configuration.uiOffsetZ = 0.0f;
                        Configuration.uiOffsetScale = 1.0f;
                        Configuration.Save();
                        LoadSettings();
                        break;
                    }
                case "conloc":
                    {
                        Configuration.conloc = !Configuration.conloc;
                        Configuration.Save();
                        LoadSettings();
                        break;
                    }
                case "swapeyes":
                    {
                        Configuration.swapEyes = !Configuration.swapEyes;
                        Configuration.Save();
                        LoadSettings();
                        break;
                    }
                case "swapeyesui":
                    {
                        Configuration.swapEyesUI = !Configuration.swapEyesUI;
                        Configuration.Save();
                        LoadSettings();
                        break;
                    }
                case "motcontoggle":
                    {
                        Configuration.motioncontrol = !Configuration.motioncontrol;
                        Configuration.Save();
                        LoadSettings();
                        break;
                    }
                case "loadcon":
                    {
                        LoadSettings();
                        break;
                    }
            }
        }

        private void LoadSettings()
        {
            xivr_hooks.SetOffsetAmount(Configuration.offsetAmountX, Configuration.offsetAmountY);
            xivr_hooks.SetSnapAmount(Configuration.snapRotateAmountX, Configuration.snapRotateAmountY);
            xivr_hooks.ToggleMotionControls(Configuration.motioncontrol);
            xivr_hooks.SetConLoc(Configuration.conloc);
            xivr_hooks.DoSwapEyes(Configuration.swapEyes);
            xivr_hooks.DoSwapEyesUI(Configuration.swapEyesUI);
            xivr_hooks.SetZScale(Configuration.uiOffsetZ, Configuration.uiOffsetScale);
        }

        private unsafe bool CheckCharacterSelection()
        {
            AtkUnitBase* CharSelectAddon = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("_CharaSelectTitle", 1);
            return (CharSelectAddon == null) ? false : true;
        }

        private void Update(Framework framework)
        {
            if (pluginReady)
            {
                if(CheckCharacterSelection() && isEnabled == true && firstRun == false)
                {
                    LoadSettings();
                    PluginLog.Log("Setup Complete");
                    firstRun = true;
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
            }
        }

        public void Dispose()
        {
            firstRun = false;
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