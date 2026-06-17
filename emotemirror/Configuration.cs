using Dalamud.Configuration;
using System;

namespace EmoteMirror;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool EmoteMirrorEnabled { get; set; } = false;
    public bool EmoteMotionOnly { get; set; } = false;   // appends "motion" to suppress chat message
    public bool EmoteFriendsOnly { get; set; } = false;  // only mirror emotes from friends
    public bool EnableDebugLogging { get; set; } = false;
    public void Save()
    {
        EmoteMirrorPlugin.PluginInterface.SavePluginConfig(this);
    }
}
