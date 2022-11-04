using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace xivr
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public bool isEnabled { get; set; } = false;
        public bool isAutoEnabled { get; set; } = false;
        public bool forceFloatingScreen { get; set; } = false;
        public bool forceFloatingInCutscene { get; set; } = true;
        public bool horizontalLock { get; set; } = false;
        public bool verticalLock { get; set; } = false;
        public bool horizonLock { get; set; } = false;
        public bool runRecenter { get; set; } = false;

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
