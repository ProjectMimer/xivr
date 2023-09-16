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
            [MarshalAs(UnmanagedType.I4)] public int resetValue;
            [MarshalAs(UnmanagedType.I4)] public LanguageTypes languageType;
            [MarshalAs(UnmanagedType.U1)] public bool isEnabled;
            [MarshalAs(UnmanagedType.U1)] public bool isAutoEnabled;
            [MarshalAs(UnmanagedType.U1)] public bool autoResize;
            [MarshalAs(UnmanagedType.U1)] public bool autoMove;
            [MarshalAs(UnmanagedType.U1)] public bool runRecenter;
            [MarshalAs(UnmanagedType.U1)] public bool vLog;
            [MarshalAs(UnmanagedType.U1)] public bool hmdPointing;
            [MarshalAs(UnmanagedType.U1)] public bool forceFloatingScreen;
            [MarshalAs(UnmanagedType.U1)] public bool forceFloatingInCutscene;
            [MarshalAs(UnmanagedType.U1)] public bool horizontalLock;
            [MarshalAs(UnmanagedType.U1)] public bool verticalLock;
            [MarshalAs(UnmanagedType.U1)] public bool horizonLock;
            [MarshalAs(UnmanagedType.U1)] public bool conloc;
            [MarshalAs(UnmanagedType.U1)] public bool hmdloc;
            [MarshalAs(UnmanagedType.U1)] public bool vertloc;
            [MarshalAs(UnmanagedType.U1)] public bool motioncontrol;
            [MarshalAs(UnmanagedType.U1)] public bool showWeaponInHand;
            [MarshalAs(UnmanagedType.R4)] public float offsetAmountX;
            [MarshalAs(UnmanagedType.R4)] public float offsetAmountY;
            [MarshalAs(UnmanagedType.R4)] public float offsetAmountZ;
            [MarshalAs(UnmanagedType.R4)] public float offsetAmountYFPS;
            [MarshalAs(UnmanagedType.R4)] public float offsetAmountZFPS;
            [MarshalAs(UnmanagedType.R4)] public float offsetAmountYFPSMount;
            [MarshalAs(UnmanagedType.R4)] public float offsetAmountZFPSMount;
            [MarshalAs(UnmanagedType.R4)] public float snapRotateAmountX;
            [MarshalAs(UnmanagedType.R4)] public float snapRotateAmountY;
            [MarshalAs(UnmanagedType.U1)] public bool uiDepth;
            [MarshalAs(UnmanagedType.R4)] public float uiOffsetZ;
            [MarshalAs(UnmanagedType.R4)] public float uiOffsetScale;
            [MarshalAs(UnmanagedType.R4)] public float ipdOffset;
            [MarshalAs(UnmanagedType.U1)] public bool swapEyes;
            [MarshalAs(UnmanagedType.U1)] public bool swapEyesUI;
            [MarshalAs(UnmanagedType.I4)] public int hmdWidth;
            [MarshalAs(UnmanagedType.I4)] public int hmdHeight;
            [MarshalAs(UnmanagedType.I4)] public int targetCursorSize;
            [MarshalAs(UnmanagedType.U1)] public bool asymmetricProjection;
            [MarshalAs(UnmanagedType.U1)] public bool mode2d;
            [MarshalAs(UnmanagedType.U1)] public bool immersiveMovement;
            [MarshalAs(UnmanagedType.U1)] public bool immersiveFull;
            [MarshalAs(UnmanagedType.U1)] public bool ultrawideshadows;
            [MarshalAs(UnmanagedType.U1)] public bool osk;
            [MarshalAs(UnmanagedType.R4)] public float armMultiplier;

            public cfgData()
            {
                resetValue = 0;
                languageType = LanguageTypes.en;
                isEnabled = false;
                isAutoEnabled = true;
                autoResize = true;
                autoMove = true;
                runRecenter = false;
                vLog = false;
                hmdPointing = false;
                forceFloatingScreen = false;
                forceFloatingInCutscene = true;
                horizontalLock = false;
                verticalLock = true;
                horizonLock = true;
                conloc = false;
                hmdloc = false;
                vertloc = false;
                motioncontrol = true;
                showWeaponInHand = false;
                offsetAmountX = 0.0f;
                offsetAmountY = 0.0f;
                offsetAmountZ = 0.0f;
                offsetAmountYFPS = 0;
                offsetAmountZFPS = 0;
                offsetAmountYFPSMount = 0;
                offsetAmountZFPSMount = 0;
                snapRotateAmountX = 45.0f;
                snapRotateAmountY = 15.0f;
                uiDepth = true;
                uiOffsetZ = 50.0f;
                uiOffsetScale = 1.0f;
                ipdOffset = 0.0f;
                swapEyes = false;
                swapEyesUI = false;
                hmdWidth = 0;
                hmdHeight = 0;
                targetCursorSize = 100;
                asymmetricProjection = true;
                mode2d = false;
                immersiveMovement = false;
                immersiveFull = false;
                ultrawideshadows = false;
                osk = false;
                armMultiplier = 100.0f;
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

        public void CheckVersion(int UpdateValue)
        {
            if (data.resetValue != UpdateValue)
            {
                int hmdWidth = data.hmdWidth;
                int hmdHeight = data.hmdHeight;
                data = new cfgData();
                data.resetValue = UpdateValue;
                data.hmdWidth = hmdWidth;
                data.hmdHeight = hmdHeight;
                Save();
            }
        }
    }
}
