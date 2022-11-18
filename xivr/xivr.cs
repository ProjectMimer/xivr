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
        private IntPtr GameWindowHandle = IntPtr.Zero;
        private Point origWindowSize = new Point(0, 0);
        public bool doUpdate = false;

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
                            FindWindowHandle();
                            counter = 500;
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
                        doUpdate = true;
                        break;
                    }
                case "conloc":
                    {
                        Configuration.conloc = !Configuration.conloc;
                        Configuration.Save();
                        doUpdate = true;
                        break;
                    }
                case "swapeyes":
                    {
                        Configuration.swapEyes = !Configuration.swapEyes;
                        Configuration.Save();
                        doUpdate = true;
                        break;
                    }
                case "swapeyesui":
                    {
                        Configuration.swapEyesUI = !Configuration.swapEyesUI;
                        Configuration.Save();
                        doUpdate = true;
                        break;
                    }
                case "motcontoggle":
                    {
                        Configuration.motioncontrol = !Configuration.motioncontrol;
                        Configuration.Save();
                        doUpdate = true;
                        break;
                    }
            }
        }

        public void LoadSettings()
        {
            xivr_hooks.SetOffsetAmount(Configuration.offsetAmountX, Configuration.offsetAmountY);
            xivr_hooks.SetSnapAmount(Configuration.snapRotateAmountX, Configuration.snapRotateAmountY);
            xivr_hooks.ToggleMotionControls(Configuration.motioncontrol);
            xivr_hooks.SetConLoc(Configuration.conloc);
            xivr_hooks.DoSwapEyes(Configuration.swapEyes);
            xivr_hooks.DoSwapEyesUI(Configuration.swapEyesUI);
            xivr_hooks.UpdateZScale(Configuration.uiOffsetZ, Configuration.uiOffsetScale);
        }

        private void FindWindowHandle()
        {
            if (GameWindowHandle == IntPtr.Zero)
            {
                while ((GameWindowHandle = xivr_hooks.FindWindowEx(IntPtr.Zero, GameWindowHandle, "FFXIVGAME", "FINAL FANTASY XIV")) != IntPtr.Zero)
                {
                    _ = xivr_hooks.GetWindowThreadProcessId(GameWindowHandle, out int pid);

                    if (pid == Environment.ProcessId && xivr_hooks.IsWindowVisible(GameWindowHandle))
                        break;
                }
            }
            PluginLog.Log($"GameWindowHandle: {GameWindowHandle:X}");
        }

        private void Update(Framework framework)
        {
            if (pluginReady)
            {
                if(doUpdate == true)
                {
                    LoadSettings();
                    PluginLog.Log("Setup Complete");
                    doUpdate = false;
                }

                bool isCutscene = DalamudApi.Condition[ConditionFlag.OccupiedInCutSceneEvent] || DalamudApi.Condition[ConditionFlag.WatchingCutscene] || DalamudApi.Condition[ConditionFlag.WatchingCutscene78];
                bool forceFloating = Configuration.forceFloatingScreen || (Configuration.forceFloatingInCutscene && isCutscene);

                xivr_hooks.ForceFloatingScreen(forceFloating);
                xivr_hooks.SetLocks(Configuration.horizontalLock, Configuration.verticalLock, Configuration.horizonLock);
                
                if (Configuration.isEnabled == true && isEnabled == false)
                {
                    //----
                    // Give the game a few seconds to update the buffers before enabling vr
                    //----
                    if (counter == 500)
                    {
                        if (Configuration.autoResize && Configuration.hmdWidth != 0 && Configuration.hmdHeight != 0)
                        {
                            xivr_hooks.ResizeWindow(GameWindowHandle, Configuration.hmdWidth, Configuration.hmdHeight);
                            PluginLog.Log($"Resizing window to: {Configuration.hmdWidth}, {Configuration.hmdHeight}");
                        }
                        counter--;
                    }
                    else if (counter == 150)
                    {
                        xivr_hooks.Start();
                        origWindowSize = xivr_hooks.GetWindowSize();
                        Point hmdSize = xivr_hooks.GetBufferSize();
                        Configuration.hmdWidth = hmdSize.X;
                        Configuration.hmdHeight = hmdSize.Y;
                        Configuration.Save();
                        PluginLog.Log($"Saving HMD Size {Configuration.hmdWidth} {Configuration.hmdHeight}");
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
                else if (Configuration.isEnabled == false && isEnabled == true)
                {
                    xivr_hooks.Stop();
                    xivr_hooks.ResizeWindow(GameWindowHandle, origWindowSize.X, origWindowSize.Y);
                    PluginLog.Log($"Resizing window to: {origWindowSize.X}, {origWindowSize.Y}");
                    isEnabled = false;
                    counter = 500;
                }
                if (Configuration.runRecenter == true)
                {
                    Configuration.runRecenter = false;
                    xivr_hooks.Recenter();
                }

                xivr_hooks.Update(framework);
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
            {
                xivr_hooks.Stop();
                xivr_hooks.ResizeWindow(GameWindowHandle, origWindowSize.X, origWindowSize.Y);
                PluginLog.Log($"Resizing window to: {origWindowSize.X}, {origWindowSize.Y}");
                xivr_hooks.Dispose();
            }
            DalamudApi.TitleScreenMenu.RemoveEntry(xivrMenuEntry);
            DalamudApi.Framework.Update -= Update;
            DalamudApi.PluginInterface.UiBuilder.Draw -= Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
            DalamudApi.Dispose();
        }

    }
}