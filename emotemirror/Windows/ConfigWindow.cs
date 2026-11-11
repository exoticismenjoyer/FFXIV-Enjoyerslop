using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using System;
using System.Linq;
using System.Numerics;

namespace EmoteMirror.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private string _allowListStatus = string.Empty;

    public ConfigWindow(EmoteMirrorPlugin plugin) : base("EmoteMirror Settings")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(420, 420);
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

        DrawAllowList();
    }

    private void DrawAllowList()
    {
        ImGui.Separator();
        ImGui.Text("Allow List");

        var useAllowList = configuration.UseAllowList;
        if (ImGui.Checkbox("Only Allow Listed Players", ref useAllowList))
        {
            configuration.UseAllowList = useAllowList;
            configuration.Save();
        }

        ImGui.TextWrapped(
            "If you have the 'Friends Only' option enabled, your friends will always pass " +
            "regardless of this setting. To add someone: target " +
            "them in-game first, then click 'Add Current Target' below.");

        ImGui.Spacing();

        if (ImGui.Button("Add Current Target", new Vector2(-1, 0)))
            TryAddCurrentTarget();

        if (!string.IsNullOrEmpty(_allowListStatus))
            ImGui.TextDisabled(_allowListStatus);

        ImGui.Spacing();
        ImGui.Text("Current List:");
        ImGui.BeginChild("AllowListEntries", new Vector2(-1, 120));

        if (configuration.AllowedPlayers.Count == 0)
        {
            ImGui.TextDisabled("(empty)");
        }
        else
        {
            AllowedPlayer? toRemove = null;
            foreach (var p in configuration.AllowedPlayers)
            {
                ImGui.PushID(p.Name + "@" + p.WorldId);

                var label = string.IsNullOrEmpty(p.WorldName) ? p.Name : $"{p.Name} @ {p.WorldName}";
                ImGui.TextUnformatted(label);
                ImGui.SameLine();
                if (ImGui.SmallButton("Remove"))
                    toRemove = p;

                ImGui.PopID();
            }

            if (toRemove != null)
            {
                configuration.AllowedPlayers.Remove(toRemove);
                configuration.Save();
                _allowListStatus = $"Removed {toRemove.Name}.";
            }
        }

        ImGui.EndChild();
    }

    private void TryAddCurrentTarget()
    {
        var target = EmoteMirrorPlugin.TargetManager.Target;
        if (target is not IPlayerCharacter playerTarget)
        {
            _allowListStatus = "No player targeted - target a player character first.";
            return;
        }

        var name = playerTarget.Name.TextValue;
        var worldId = playerTarget.HomeWorld.RowId;

        if (configuration.AllowedPlayers.Any(p => p.Matches(name, worldId)))
        {
            _allowListStatus = $"{name} is already on the list.";
            return;
        }

        // Best-effort world name lookup for a readable display label - if
        // this fails for any reason we still add the entry, just with the
        // raw world id shown instead of a name.
        string worldName = $"World #{worldId}";
        try
        {
            var worldSheet = EmoteMirrorPlugin.DataManager.GetExcelSheet<World>();
            if (worldSheet != null && worldSheet.TryGetRow(worldId, out var worldRow))
                worldName = worldRow.Name.ExtractText();
        }
        catch (Exception ex)
        {
            EmoteMirrorPlugin.Log.Warning(ex, "[EmoteMirror] Could not resolve world name for allow-list entry.");
        }

        configuration.AllowedPlayers.Add(new AllowedPlayer { Name = name, WorldId = worldId, WorldName = worldName });
        configuration.Save();
        _allowListStatus = $"Added {name} @ {worldName}.";
    }
}