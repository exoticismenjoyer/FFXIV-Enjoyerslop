using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace EmoteMirror.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(EmoteMirrorPlugin plugin) : base("EmoteMirror Settings")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(400, 200);
        SizeCondition = ImGuiCond.Always;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var enabled = configuration.EmoteMirrorEnabled;
        if (ImGui.Checkbox("Enable Emote Mirror", ref enabled))
        {
            configuration.EmoteMirrorEnabled = enabled;
            configuration.Save();
        }

        ImGui.Separator();

        if (!configuration.EmoteMirrorEnabled)
        {
            ImGui.TextDisabled("Enable the plugin above to configure options.");
            return;
        }

        var motionOnly = configuration.EmoteMotionOnly;
        if (ImGui.Checkbox("Motion Only (no chat message)", ref motionOnly))
        {
            configuration.EmoteMotionOnly = motionOnly;
            configuration.Save();
        }

        var friendsOnly = configuration.EmoteFriendsOnly;
        if (ImGui.Checkbox("Friends Only", ref friendsOnly))
        {
            configuration.EmoteFriendsOnly = friendsOnly;
            configuration.Save();
        }

        var debugLogging = configuration.EnableDebugLogging;
        if (ImGui.Checkbox("Enable Debug Logging", ref debugLogging))
        {
            configuration.EnableDebugLogging = debugLogging;
            configuration.Save();
        }
        
    }
}
