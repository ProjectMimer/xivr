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

            ImGui.SetNextWindowSize(new Vector2(500, 850) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(500, 850) * ImGuiHelpers.GlobalScale, new Vector2(9999));
            if (ImGui.Begin("Configuration", ref isVisible, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.BeginChild("VR", new Vector2(300, 150) * ImGuiHelpers.GlobalScale, true);

                if (ImGui.Checkbox("Enable", ref xivr.cfg.data.isEnabled))
                {
                    xivr.cfg.Save();
                }

                if (ImGui.Checkbox("Auto Enable if steamvr is active", ref xivr.cfg.data.isAutoEnabled))
                {
                    xivr.cfg.Save();
                }

                if (ImGui.Checkbox("Auto Resize when activated", ref xivr.cfg.data.autoResize))
                {
                    xivr.cfg.Save();
                }
                if (ImGui.Button("Recenter"))
                    xivr.cfg.data.runRecenter = true;


                ImGui.EndChild();

                ImGui.BeginChild("Misc", new Vector2(300, 150) * ImGuiHelpers.GlobalScale, true);

                if (ImGui.Checkbox("Enable first person controller locomotion", ref xivr.cfg.data.conloc))
                {
                    xivr.cfg.Save(); xivr.Plugin.doUpdate = true;
                }

                if (ImGui.Checkbox("Enable motion controllers", ref xivr.cfg.data.motioncontrol))
                {
                    xivr.cfg.Save(); xivr.Plugin.doUpdate = true;
                }

                if (ImGui.Checkbox("Force floating screen", ref xivr.cfg.data.forceFloatingScreen))
                {
                    xivr.cfg.Save(); xivr.Plugin.doUpdate = true;
                }

                if (ImGui.Checkbox("Force floating screen in Cutscene", ref xivr.cfg.data.forceFloatingInCutscene))
                {
                    xivr.cfg.Save(); xivr.Plugin.doUpdate = true;
                }
                ImGui.EndChild();

                DrawLocks();
                DrawUISetings();


                if (xivr.Plugin.doUpdate == true)
                    xivr.cfg.Save();

                ImGui.End();
            }
        }


        public static void DrawLocks()
        {
            bool lockHorizon = xivr.cfg.data.horizonLock;
            bool snapX = xivr.cfg.data.horizontalLock;
            bool snapY = xivr.cfg.data.verticalLock;
            float snapXamount = xivr.cfg.data.snapRotateAmountX;
            float snapYamount = xivr.cfg.data.snapRotateAmountY;


            ImGui.BeginChild("Snap Turning", new Vector2(300, 200) * ImGuiHelpers.GlobalScale, true);

            ImGui.Checkbox("Keep headset level with horizon", ref lockHorizon);

            ImGui.Text("Horizontal Snap:");
            ImGui.Text("On/Off:"); ImGui.SameLine(); ImGui.Checkbox("##DrawLocks:hs", ref snapX);
            ImGui.Text("Amount:"); ImGui.SameLine(); ImGui.SliderFloat("##DrawLocks:hsa", ref snapXamount, 0, 90, "%.0f");

            ImGui.Text("Vertical Snap:");
            ImGui.Text("On/Off:"); ImGui.SameLine(); ImGui.Checkbox("##DrawLocks:vs", ref snapY);
            ImGui.Text("Amount:"); ImGui.SameLine(); ImGui.SliderFloat("##DrawLocks:vsa", ref snapYamount, 0, 90, "%.0f");

            if (xivr.cfg.data.horizonLock != lockHorizon)
                xivr.Plugin.doUpdate = true; xivr.cfg.data.horizonLock = lockHorizon;

            if (xivr.cfg.data.horizontalLock != snapX)
                xivr.Plugin.doUpdate = true; xivr.cfg.data.horizontalLock = snapX;

            if (xivr.cfg.data.verticalLock != snapY)
                xivr.Plugin.doUpdate = true; xivr.cfg.data.verticalLock = snapY;

            if (xivr.cfg.data.snapRotateAmountX != snapXamount)
                xivr.Plugin.doUpdate = true; xivr.cfg.data.snapRotateAmountX = snapXamount;

            if (xivr.cfg.data.snapRotateAmountY != snapYamount)
                xivr.Plugin.doUpdate = true; xivr.cfg.data.snapRotateAmountY = snapYamount;

            ImGui.EndChild();
        }

        public static void DrawUISetings()
        {
            float uiOffsetZ = xivr.cfg.data.uiOffsetZ;
            float uiOffsetScale = xivr.cfg.data.uiOffsetScale;
            float ipdOffset = xivr.cfg.data.ipdOffset;
            bool swapEyes = xivr.cfg.data.swapEyes;
            bool swapEyesUI = xivr.cfg.data.swapEyesUI;
            float offsetAmountX = xivr.cfg.data.offsetAmountX;
            float offsetAmountY = xivr.cfg.data.offsetAmountY;


            ImGui.BeginChild("UI", new Vector2(300, 250) * ImGuiHelpers.GlobalScale, true);

            ImGui.Text("Z Offset"); ImGui.SameLine(); ImGui.InputFloat("##DrawUISetings:zoff", ref uiOffsetZ);
            ImGui.Text("Z Scale"); ImGui.SameLine(); ImGui.InputFloat("##DrawUISetings:zscale", ref uiOffsetScale);
            ImGui.Text("IPD Offset"); ImGui.SameLine(); ImGui.SliderFloat("##DrawUISetings:ipdoff", ref ipdOffset, -1, 1, "%f");

            ImGui.Text("Swap Eyes"); ImGui.SameLine(); ImGui.Checkbox("##DrawUISetings:swapeyes", ref swapEyes);
            ImGui.Text("Swap Eyes UI"); ImGui.SameLine(); ImGui.Checkbox("##DrawUISetings:swapeyesui", ref swapEyesUI);

            ImGui.Text("X Offset"); ImGui.SameLine(); ImGui.SliderFloat("##DrawUISetings:xoff", ref offsetAmountX, -150, 150, "%.0f");
            ImGui.Text("Y Offset"); ImGui.SameLine(); ImGui.SliderFloat("##DrawUISetings:yoff", ref offsetAmountY, -150, 150, "%.0f");

            if (xivr.cfg.data.uiOffsetZ != uiOffsetZ)
                xivr.Plugin.doUpdate = true; xivr.cfg.data.uiOffsetZ = uiOffsetZ;

            if (xivr.cfg.data.uiOffsetScale != uiOffsetScale)
                xivr.Plugin.doUpdate = true; xivr.cfg.data.uiOffsetScale = uiOffsetScale;

            if (xivr.cfg.data.ipdOffset != ipdOffset)
                xivr.Plugin.doUpdate = true; xivr.cfg.data.ipdOffset = ipdOffset;

            if (xivr.cfg.data.swapEyes != swapEyes)
                xivr.Plugin.doUpdate = true; xivr.cfg.data.swapEyes = swapEyes;

            if (xivr.cfg.data.swapEyesUI != swapEyesUI)
                xivr.Plugin.doUpdate = true; xivr.cfg.data.swapEyesUI = swapEyesUI;

            if (xivr.cfg.data.offsetAmountX != offsetAmountX)
                xivr.Plugin.doUpdate = true; xivr.cfg.data.offsetAmountX = offsetAmountX;

            if (xivr.cfg.data.offsetAmountY != offsetAmountY)
                xivr.Plugin.doUpdate = true; xivr.cfg.data.offsetAmountY = offsetAmountY;

            ImGui.EndChild();

        }

    }
}
