using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Runtime.InteropServices;

namespace xivr
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct cfgData
        {
            // Bool: U1
            // Float: R4
            // Int: I4
            [MarshalAs(UnmanagedType.U1)] public bool isEnabled;
            [MarshalAs(UnmanagedType.U1)] public bool isAutoEnabled;
            [MarshalAs(UnmanagedType.U1)] public bool forceFloatingScreen;
            [MarshalAs(UnmanagedType.U1)] public bool forceFloatingInCutscene;
            [MarshalAs(UnmanagedType.U1)] public bool horizontalLock;
            [MarshalAs(UnmanagedType.U1)] public bool verticalLock;
            [MarshalAs(UnmanagedType.U1)] public bool horizonLock;
            [MarshalAs(UnmanagedType.U1)] public bool runRecenter;
            [MarshalAs(UnmanagedType.R4)] public float offsetAmountX;
            [MarshalAs(UnmanagedType.R4)] public float offsetAmountY;
            [MarshalAs(UnmanagedType.R4)] public float snapRotateAmountX;
            [MarshalAs(UnmanagedType.R4)] public float snapRotateAmountY;
            [MarshalAs(UnmanagedType.R4)] public float uiOffsetZ;
            [MarshalAs(UnmanagedType.R4)] public float uiOffsetScale;
            [MarshalAs(UnmanagedType.U1)] public bool conloc;
            [MarshalAs(UnmanagedType.U1)] public bool swapEyes;
            [MarshalAs(UnmanagedType.U1)] public bool swapEyesUI;
            [MarshalAs(UnmanagedType.U1)] public bool motioncontrol;
            [MarshalAs(UnmanagedType.I4)] public int hmdWidth;
            [MarshalAs(UnmanagedType.I4)] public int hmdHeight;
            [MarshalAs(UnmanagedType.U1)] public bool autoResize;
            [MarshalAs(UnmanagedType.R4)] public float ipdOffset;
            [MarshalAs(UnmanagedType.U1)] public bool vLog;

            public cfgData()
            {
                isEnabled = false;
                isAutoEnabled = true;
                forceFloatingScreen = false;
                forceFloatingInCutscene = true;
                horizontalLock = false;
                verticalLock = true;
                horizonLock = true;
                runRecenter = false;
                offsetAmountX = 0.0f;
                offsetAmountY = 0.0f;
                snapRotateAmountX = 45.0f;
                snapRotateAmountY = 15.0f;
                uiOffsetZ = 0.0f;
                uiOffsetScale = 1.0f;
                conloc = false;
                swapEyes = false;
                swapEyesUI = false;
                motioncontrol = true;
                hmdWidth = 0;
                hmdHeight = 0;
                autoResize = true;
                ipdOffset = 0.0f;
                vLog = false;
            }
        }

        public int Version { get; set; } = 0;
        public cfgData data = new cfgData();

        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface!.SavePluginConfig(this);
        }
    }
}
