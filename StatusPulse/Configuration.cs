using Dalamud.Configuration;
using System;

namespace StatusPulse;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool EnableDebugLogging { get; set; } = false;
    public string SupabaseUrl { get; set; } = "";
    public string SupabaseAnonKey { get; set; } = "";


    public void Save()
    {
        StatusPulsePlugin.PluginInterface.SavePluginConfig(this);
    }
}
