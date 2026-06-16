using Dalamud.Configuration;
using System;

namespace StatusPulse;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    // Supabase
    public bool EnableDebugLogging { get; set; } = false;
    public string SupabaseUrl { get; set; } = "";
    public string SupabaseAnonKey { get; set; } = "";

    // Emote Mirror
    public bool EmoteMirrorEnabled { get; set; } = false;
    public bool EmoteMotionOnly { get; set; } = false;   // appends "motion" to suppress chat spam
    public bool EmoteFriendsOnly { get; set; } = false;  // only mirror emotes from friends

    public void Save()
    {
        StatusPulsePlugin.PluginInterface.SavePluginConfig(this);
    }
}
