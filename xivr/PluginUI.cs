﻿using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using xivr.Structures;

namespace xivr
{
    public static class PluginUI
    {
        public static bool isVisible = false;

        public static void Draw(uiOptionStrings lngOptions)
        {
            if (!isVisible)
                return;

            ImGui.SetNextWindowSize(new Vector2(370, 800) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(370, 800) * ImGuiHelpers.GlobalScale, new Vector2(9999));
            //if (ImGui.Begin("Configuration", ref isVisible, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            if (ImGui.Begin("Configuration", ref isVisible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.BeginChild("VR", new Vector2(350, 200) * ImGuiHelpers.GlobalScale, true);

                if (ImGui.Checkbox(lngOptions.isEnabled_Line1, ref xivr.cfg.data.isEnabled))
                    xivr.Plugin.doUpdate = true;

                if (ImGui.Checkbox(lngOptions.isAutoEnabled_Line1, ref xivr.cfg.data.isAutoEnabled))
                    xivr.Plugin.doUpdate = true;

                if (ImGui.Checkbox(lngOptions.mode2d_Line1, ref xivr.cfg.data.mode2d))
                    xivr.Plugin.doUpdate = true;

                if (ImGui.Checkbox(lngOptions.autoResize_Line1, ref xivr.cfg.data.autoResize))
                    xivr.Plugin.doUpdate = true;

                if (ImGui.Button(lngOptions.runRecenter_Line1))
                    xivr.cfg.data.runRecenter = true;

                if (ImGui.Checkbox(lngOptions.vLog_Line1, ref xivr.cfg.data.vLog))
                    xivr.Plugin.doUpdate = true;

                ImGui.EndChild();

                ImGui.BeginChild("Misc", new Vector2(350, 300) * ImGuiHelpers.GlobalScale, true);

                if (ImGui.Checkbox(lngOptions.motioncontrol_Line1, ref xivr.cfg.data.motioncontrol))
                {
                    xivr.cfg.data.hmdPointing = !xivr.cfg.data.motioncontrol;
                    xivr.Plugin.doUpdate = true;
                }

                if (ImGui.Checkbox(lngOptions.conloc_Line1, ref xivr.cfg.data.conloc))
                    xivr.Plugin.doUpdate = true;

                if (ImGui.Checkbox(lngOptions.hmdloc_Line1, ref xivr.cfg.data.hmdloc))
                    xivr.Plugin.doUpdate = true;

                if (ImGui.Checkbox(lngOptions.vertloc_Line1, ref xivr.cfg.data.vertloc))
                    xivr.Plugin.doUpdate = true;

                if (ImGui.Checkbox(lngOptions.showWeaponInHand_Line1, ref xivr.cfg.data.showWeaponInHand))
                    xivr.Plugin.doUpdate = true;

                if (ImGui.Checkbox(lngOptions.forceFloatingScreen_Line1, ref xivr.cfg.data.forceFloatingScreen))
                    xivr.Plugin.doUpdate = true;

                if (ImGui.Checkbox(lngOptions.forceFloatingInCutscene_Line1, ref xivr.cfg.data.forceFloatingInCutscene))
                    xivr.Plugin.doUpdate = true;

                ImGui.EndChild();

                DrawLocks(lngOptions);
                DrawUISetings(lngOptions);

                if (xivr.Plugin.doUpdate == true)
                    xivr.cfg.Save();

                ImGui.End();
            }
        }

        public static void DrawLocks(uiOptionStrings lngOptions)
        {
            ImGui.BeginChild("Snap Turning", new Vector2(350, 200) * ImGuiHelpers.GlobalScale, true);

            if(ImGui.Checkbox(lngOptions.horizonLock_Line1, ref xivr.cfg.data.horizonLock))
                xivr.Plugin.doUpdate = true;

            if (ImGui.Checkbox(lngOptions.snapRotateAmountX_Line1, ref xivr.cfg.data.horizontalLock))
                xivr.Plugin.doUpdate = true;

            ImGui.Text(lngOptions.snapRotateAmountX_Line2); ImGui.SameLine();
            if (ImGui.SliderFloat("##DrawLocks:hsa", ref xivr.cfg.data.snapRotateAmountX, 0, 90, "%.0f"))
                xivr.Plugin.doUpdate = true;

            if (ImGui.Checkbox(lngOptions.snapRotateAmountY_Line1, ref xivr.cfg.data.verticalLock))
                xivr.Plugin.doUpdate = true;

            ImGui.Text(lngOptions.snapRotateAmountY_Line2); ImGui.SameLine();
            if (ImGui.SliderFloat("##DrawLocks:vsa", ref xivr.cfg.data.snapRotateAmountY, 0, 90, "%.0f"))
                xivr.Plugin.doUpdate = true;

            ImGui.EndChild();
        }



        public static void DrawUISetings(uiOptionStrings lngOptions)
        {
            ImGui.BeginChild("UI", new Vector2(350, 400) * ImGuiHelpers.GlobalScale, true);

            ImGui.Text(lngOptions.uiOffsetZ_Line1); ImGui.SameLine(); 
            if(ImGui.SliderFloat("##DrawUISetings:uizoff", ref xivr.cfg.data.uiOffsetZ, 0, 100, "%.0f"))
                xivr.Plugin.doUpdate = true;

            ImGui.Text(lngOptions.uiOffsetScale_Line1); ImGui.SameLine();
            if (ImGui.SliderFloat("##DrawUISetings:uizscale", ref xivr.cfg.data.uiOffsetScale, 1, 5, "%.00f"))
                xivr.Plugin.doUpdate = true;

            if (ImGui.Checkbox(lngOptions.uiDepth_Line1, ref xivr.cfg.data.uiDepth))
                xivr.Plugin.doUpdate = true;

            ImGui.Text(lngOptions.ipdOffset_Line1); ImGui.SameLine();
            if (ImGui.SliderFloat("##DrawUISetings:ipdoff", ref xivr.cfg.data.ipdOffset, -10, 10, "%f"))
                xivr.Plugin.doUpdate = true;

            if (ImGui.Checkbox(lngOptions.swapEyes_Line1, ref xivr.cfg.data.swapEyes))
                xivr.Plugin.doUpdate = true;

            if (ImGui.Checkbox(lngOptions.swapEyesUI_Line1, ref xivr.cfg.data.swapEyesUI))
                xivr.Plugin.doUpdate = true;

            ImGui.Text(lngOptions.offsetAmountX_Line1); ImGui.SameLine();
            if (ImGui.SliderFloat("##DrawUISetings:xoff", ref xivr.cfg.data.offsetAmountX, -150, 150, "%.0f"))
                xivr.Plugin.doUpdate = true;

            ImGui.Text(lngOptions.offsetAmountY_Line1); ImGui.SameLine();
            if (ImGui.SliderFloat("##DrawUISetings:yoff", ref xivr.cfg.data.offsetAmountY, -150, 150, "%.0f"))
                xivr.Plugin.doUpdate = true;

            ImGui.Text(lngOptions.offsetAmountZ_Line1); ImGui.SameLine();
            if (ImGui.SliderFloat("##DrawUISetings:zoff", ref xivr.cfg.data.offsetAmountZ, -150, 150, "%.0f"))
                xivr.Plugin.doUpdate = true;

            ImGui.Text(lngOptions.offsetAmountYFPS_Line1); ImGui.SameLine();
            if (ImGui.SliderFloat("##DrawUISetings:fpsyoff", ref xivr.cfg.data.offsetAmountYFPS, -150, 150, "%.0f"))
                xivr.Plugin.doUpdate = true;

            ImGui.Text(lngOptions.offsetAmountZFPS_Line1); ImGui.SameLine();
            if (ImGui.SliderFloat("##DrawUISetings:fpszoff", ref xivr.cfg.data.offsetAmountZFPS, -150, 150, "%.0f"))
                xivr.Plugin.doUpdate = true;

            ImGui.Text(lngOptions.targetCursorSize_Line1); ImGui.SameLine();
            if (ImGui.SliderInt("##DrawUISetings:targetcur", ref xivr.cfg.data.targetCursorSize, 50, 255))
                xivr.Plugin.doUpdate = true;

            if (ImGui.Checkbox(lngOptions.ultrawideshadows_Line1, ref xivr.cfg.data.ultrawideshadows))
                xivr.Plugin.doUpdate = true;

            if (ImGui.Checkbox(lngOptions.asymmetricProjection_Line1, ref xivr.cfg.data.asymmetricProjection))
                xivr.Plugin.doUpdate = true;

            ImGui.EndChild();
        }
    }
}
