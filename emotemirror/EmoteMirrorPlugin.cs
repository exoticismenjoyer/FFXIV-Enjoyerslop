using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using System;
using System.Text;
using EmoteMirror.Windows;

namespace EmoteMirror;

public sealed class EmoteMirrorPlugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    private const string CommandName = "/emotemirror";

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("EmoteMirror");
    private ConfigWindow ConfigWindow { get; init; }
    private EmoteReaderHooks? emoteHooks;

    public EmoteMirrorPlugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens EmoteMirror configuration"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        emoteHooks = new EmoteReaderHooks();
        emoteHooks.OnEmoteTriggered += OnEmoteTriggered;
    }

    private void OnEmoteTriggered(IPlayerCharacter instigator, ushort emoteId)
    {
        if (!Configuration.EmoteMirrorEnabled) return;

        // Friends only filter
        if (Configuration.EmoteFriendsOnly && !instigator.StatusFlags.HasFlag(StatusFlags.Friend))
            return;

        // Look up the slash command for this emote from the Emote sheet
        var emoteSheet = DataManager.GetExcelSheet<Emote>();
        if (emoteSheet == null) return;
        if (!emoteSheet.TryGetRow(emoteId, out var emoteRow)) return;

        var command = emoteRow.TextCommand.Value.Command.ExtractText();
        if (string.IsNullOrWhiteSpace(command)) return;

        var fullCommand = Configuration.EmoteMotionOnly ? $"{command} motion" : command;
        ExecuteCommand(fullCommand);

       if (Configuration.EnableDebugLogging)
            Log.Info($"[EmoteMirror] {instigator.Name} used {command} → mirroring '{fullCommand}'");
    }

    private static unsafe void ExecuteCommand(string command)
    {
        var bytes = Encoding.UTF8.GetBytes(command);
        if (bytes.Length == 0 || bytes.Length > 500) return;

        var uiModule = UIModule.Instance();
        if (uiModule == null) return;

        var mes = Utf8String.FromSequence(bytes);
        if (mes == null) return;

        try
        {
            uiModule->ProcessChatBoxEntry(mes);
        }
        finally
        {
            mes->Dtor(true); // always runs — destroys string AND frees memory
        }
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;

        if (emoteHooks != null)
        {
            emoteHooks.OnEmoteTriggered -= OnEmoteTriggered;
            emoteHooks.Dispose();
        }

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args) => ConfigWindow.Toggle();
    public void ToggleConfigUi() => ConfigWindow.Toggle();
}
