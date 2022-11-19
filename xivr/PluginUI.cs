using System;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;

namespace xivr
{
    public static class PluginUI
    {
        public static bool isVisible = false;

        public static void Draw()
        {
            if (!isVisible)
                return;

            ImGui.SetNextWindowSize(new Vector2(300, 350) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(300, 350) * ImGuiHelpers.GlobalScale, new Vector2(9999));
            if (ImGui.Begin("Configuration", ref isVisible, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.BeginGroup();
                bool isEnabled = xivr.Configuration.isEnabled;
                if (ImGui.Checkbox("Enable", ref isEnabled))
                {
                    xivr.Configuration.isEnabled = isEnabled;
                    xivr.Configuration.Save();
                }

                bool isAutoEnabled = xivr.Configuration.isAutoEnabled;
                if (ImGui.Checkbox("Auto Enable if steamvr is active", ref isAutoEnabled))
                {
                    xivr.Configuration.isAutoEnabled = isAutoEnabled;
                    xivr.Configuration.Save();
                }

                bool autoResize = xivr.Configuration.autoResize;
                if (ImGui.Checkbox("Auto Resize when activated", ref autoResize))
                {
                    xivr.Configuration.autoResize = autoResize;
                    xivr.Configuration.Save();
                }

                bool conLock = xivr.Configuration.conloc;
                if (ImGui.Checkbox("Enable first person controller locomotion", ref conLock))
                {
                    xivr.Configuration.conloc = conLock;
                    xivr.Configuration.Save();
                    xivr.Plugin.doUpdate = true;
                }

                bool motioncontrol = xivr.Configuration.motioncontrol;
                if (ImGui.Checkbox("Enable motion controllers", ref motioncontrol))
                {
                    xivr.Configuration.motioncontrol = motioncontrol;
                    xivr.Configuration.Save();
                    xivr.Plugin.doUpdate = true;
                }

                bool forceFloatingScreen = xivr.Configuration.forceFloatingScreen;
                if (ImGui.Checkbox("Force floating screen", ref forceFloatingScreen))
                {
                    xivr.Configuration.forceFloatingScreen = forceFloatingScreen;
                    xivr.Configuration.Save();
                }

                bool forceFloatingInCutscene = xivr.Configuration.forceFloatingInCutscene;
                if (ImGui.Checkbox("Force floating screen in Cutscene", ref forceFloatingInCutscene))
                {
                    xivr.Configuration.forceFloatingInCutscene = forceFloatingInCutscene;
                    xivr.Configuration.Save();
                }

                bool horizontalLock = xivr.Configuration.horizontalLock;
                if (ImGui.Checkbox("Horizontal Snap Turning", ref horizontalLock))
                {
                    xivr.Configuration.horizontalLock = horizontalLock;
                    xivr.Configuration.Save();
                    xivr.Plugin.doUpdate = true;
                }

                bool verticalLock = xivr.Configuration.verticalLock;
                if (ImGui.Checkbox("Vertical Snap Turning", ref verticalLock))
                {
                    xivr.Configuration.verticalLock = verticalLock;
                    xivr.Configuration.Save();
                    xivr.Plugin.doUpdate = true;
                }

                bool horizonLock = xivr.Configuration.horizonLock;
                if (ImGui.Checkbox("Keep headset level with horizon", ref horizonLock))
                {
                    xivr.Configuration.horizonLock = horizonLock;
                    xivr.Configuration.Save();
                    xivr.Plugin.doUpdate = true;
                }

                bool stereoHack = xivr.Configuration.stereoHack;
                if (ImGui.Checkbox("Enable Stereo eyes hack", ref stereoHack))
                {
                    xivr.Configuration.stereoHack = stereoHack;
                    xivr.Configuration.Save();
                    xivr.Plugin.doUpdate = true;
                }

                bool swapFrameCadence = xivr.Configuration.swapFrameCadence;
                if (ImGui.Checkbox("Swap stereo hack cadence", ref swapFrameCadence))
                {
                    xivr.Configuration.swapFrameCadence = swapFrameCadence;
                    xivr.Configuration.Save();
                    xivr.Plugin.doUpdate = true;
                }

                if (ImGui.Button("Recenter"))
                {
                    xivr.Configuration.runRecenter = false;
                    xivr.Configuration.Save();
                    xivr.Configuration.runRecenter = true;
                }


                ImGui.End();
            }
        }
    }
}
