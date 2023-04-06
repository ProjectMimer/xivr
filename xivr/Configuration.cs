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
            [MarshalAs(UnmanagedType.U1)] public bool hmdloc;
            [MarshalAs(UnmanagedType.U1)] public bool vertloc;
            [MarshalAs(UnmanagedType.I4)] public int targetCursorSize;
            [MarshalAs(UnmanagedType.R4)] public float offsetAmountZ;
            [MarshalAs(UnmanagedType.U1)] public bool uiDepth;
            [MarshalAs(UnmanagedType.U1)] public bool hmdPointing;
            [MarshalAs(UnmanagedType.U1)] public bool mode2d;
            [MarshalAs(UnmanagedType.U1)] public bool asymmetricProjection;
            [MarshalAs(UnmanagedType.U1)] public bool immersiveMovement;
            [MarshalAs(UnmanagedType.U1)] public bool immersiveFull;
            [MarshalAs(UnmanagedType.R4)] public float offsetAmountYFPS;
            [MarshalAs(UnmanagedType.R4)] public float offsetAmountZFPS;
            [MarshalAs(UnmanagedType.I4)] public LanguageTypes languageType;
            [MarshalAs(UnmanagedType.U1)] public bool ultrawideshadows;
            [MarshalAs(UnmanagedType.U1)] public bool showWeaponInHand;

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
                uiOffsetZ = 50.0f;
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
                hmdloc = false;
                vertloc = false;
                targetCursorSize = 100;
                offsetAmountZ = 0.0f;
                uiDepth = true;
                hmdPointing = false;
                mode2d = false;
                asymmetricProjection = true;
                immersiveMovement = false;
                immersiveFull = false;
                offsetAmountYFPS = 0;
                offsetAmountZFPS = 0;
                languageType = LanguageTypes.en;
                ultrawideshadows = false;
                showWeaponInHand = false;
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
