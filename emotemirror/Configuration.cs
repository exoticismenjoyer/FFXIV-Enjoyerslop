using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmoteMirror;

[Serializable]
public class AllowedPlayer
{
    public string Name { get; set; } = string.Empty;
    public uint WorldId { get; set; }
    public string WorldName { get; set; } = string.Empty; // display only, best-effort

    public bool Matches(string name, uint worldId)
        => WorldId == worldId && string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool EmoteMirrorEnabled { get; set; } = false;
    public bool EmoteMotionOnly { get; set; } = false;   // appends "motion" to suppress chat message
    public bool EmoteFriendsOnly { get; set; } = false;  // only mirror emotes from friends
    public bool EnableDebugLogging { get; set; } = false;

    // Allow-list: when UseAllowList is true, only players in AllowedPlayers
    // can trigger the mirror (in addition to any other filters above).
    public bool UseAllowList { get; set; } = false;
    public List<AllowedPlayer> AllowedPlayers { get; set; } = new();

    public bool IsAllowed(string name, uint worldId)
        => !UseAllowList || AllowedPlayers.Any(p => p.Matches(name, worldId));

    public void Save()
    {
        EmoteMirrorPlugin.PluginInterface.SavePluginConfig(this);
    }
}