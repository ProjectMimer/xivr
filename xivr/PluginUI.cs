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

            ImGui.SetNextWindowSize(new Vector2(200, 270) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(200, 270) * ImGuiHelpers.GlobalScale, new Vector2(9999));
            if (ImGui.Begin("Configuration", ref isVisible, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.BeginGroup();
                bool vrEnabled = xivr.Configuration.isEnabled;
                if (ImGui.Checkbox("Enable", ref vrEnabled))
                {
                    xivr.Configuration.isEnabled = vrEnabled;
                    xivr.Configuration.Save();
                }

                bool vrAutoEnabled = xivr.Configuration.isAutoEnabled;
                if (ImGui.Checkbox("Auto Enable if steamvr is active", ref vrAutoEnabled))
                {
                    xivr.Configuration.isAutoEnabled = vrAutoEnabled;
                    xivr.Configuration.Save();
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
                }

                bool verticalLock = xivr.Configuration.verticalLock;
                if (ImGui.Checkbox("Vertical Snap Turning", ref verticalLock))
                {
                    xivr.Configuration.verticalLock = verticalLock;
                    xivr.Configuration.Save();
                }

                bool horizonlLock = xivr.Configuration.horizonLock;
                if (ImGui.Checkbox("Keep headset level with horizon", ref horizonlLock))
                {
                    xivr.Configuration.horizonLock = horizonlLock;
                    xivr.Configuration.Save();
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
