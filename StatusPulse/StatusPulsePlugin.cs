using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Lumina.Excel.Sheets;
using StatusPulse.Windows;
using StatusPulse.Models;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Enums;

namespace StatusPulse;

public sealed class StatusPulsePlugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    private const string CommandName = "/pulse";
    private const double UpdateInterval = 60;
    private double timeSinceLastUpdate = 0;

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("StatusPulse");
    private ConfigWindow ConfigWindow { get; init; }
    private readonly HttpClient httpClient = new();
    private EmoteReaderHooks? emoteHooks;


    public StatusPulsePlugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();



        ConfigWindow = new ConfigWindow(this);
  

        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the plugin configuration"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        Framework.Update += OnFrameworkUpdate;

        // Emote mirror
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
            Log.Info($"[EmoteMirror] {instigator.Name} used {command} on us → mirroring '{fullCommand}'");
    }

    // Sends a slash command as the local player via the game's shell module
   private static unsafe void ExecuteCommand(string command)
{
    var bytes = Encoding.UTF8.GetBytes(command);
    if (bytes.Length == 0 || bytes.Length > 500) return;

    var mes = Utf8String.FromSequence(bytes);
    try
    {
        UIModule.Instance()->ProcessChatBoxEntry(mes);
    }
    finally
    {
        mes->Dtor(true); // always runs — destroys string AND frees memory
    }
}

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (Framework.UpdateDelta.TotalSeconds <= 0)
            return;

        timeSinceLastUpdate += Framework.UpdateDelta.TotalSeconds;

        var player = ObjectTable.LocalPlayer;
        if (player == null || timeSinceLastUpdate < UpdateInterval)
            return;


        var playerName = player.Name.TextValue;

        //  Job Abbreviation
        var playerJob = player.ClassJob.Value.Abbreviation.ExtractText();

        // Territory and Duty Info
        var (territoryName, dutyName, inDuty) = GetLocationInfo();
        
        // World Name 
        var worldName = player.CurrentWorld.Value.Name.ExtractText();


        var status = new PlayerStatus
        {
            Name = playerName,
            Job = playerJob,
            World = worldName,
            Territory = territoryName,
            InDuty = inDuty,
            DutyName = dutyName,
            Timestamp = DateTime.UtcNow
        };

        _ = SendStatus(status);

        timeSinceLastUpdate = 0;

        
    }

    private (string Territory, string Duty, bool InDuty) GetLocationInfo()
    {
        string territoryName = "Unknown";
        string dutyName = "None";
        bool inDuty = false;

        try
        {
            var territoryId = (uint)ClientState.TerritoryType;

            var territorySheet = DataManager.GetExcelSheet<TerritoryType>();
            if (territorySheet != null && territorySheet.TryGetRow(territoryId, out var territoryRow))
            {
                // PlaceName is empty for private housing interiors after patch 7.5.
                // Fall back through zone → region → HousingManager (which reads live
                // game memory for the correct district + ward + plot) → internal row name.
                var resolved =
                    NonEmpty(territoryRow.PlaceName.Value.Name.ToString())       ??
                    NonEmpty(territoryRow.PlaceNameZone.Value.Name.ToString())   ??
                    NonEmpty(territoryRow.PlaceNameRegion.Value.Name.ToString()) ??
                    NonEmpty(GetHousingLocationName())                           ??
                    NonEmpty(territoryRow.Name.ToString());

                if (resolved != null)
                    territoryName = resolved;

                if (Configuration.EnableDebugLogging)
                    Log.Info($"[TERRITORY] ID={territoryId} | PlaceName='{territoryRow.PlaceName.Value.Name}'" +
                             $" | Zone='{territoryRow.PlaceNameZone.Value.Name}'" +
                             $" | Region='{territoryRow.PlaceNameRegion.Value.Name}'" +
                             $" | Resolved='{territoryName}'");
            }

            var cfcSheet = DataManager.GetExcelSheet<ContentFinderCondition>();
            if (cfcSheet != null)
            {
                var cfc = cfcSheet.FirstOrDefault(x => x.TerritoryType.RowId == territoryId);
                if (!cfc.Equals(default(ContentFinderCondition)))
                {
                    inDuty = true;
                    var dName = cfc.Name.ToString();
                    if (!string.IsNullOrWhiteSpace(dName))
                        dutyName = dName;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error resolving location: {ex}");
        }

        return (territoryName, dutyName, inDuty);
    }

    private unsafe string? GetHousingLocationName()
    {
        try
        {
            var manager = HousingManager.Instance();
            if (manager == null) return null;
            if (!manager->IsInside()) return null;

            // Static method — returns the TerritoryTypeId of the outdoor ward this
            // house belongs to. Unlike the indoor territory ID (e.g. 1249), this one
            // has a proper PlaceName in the sheet (e.g. "The Lavender Beds").
            var wardTerritoryId = HousingManager.GetOriginalHouseTerritoryTypeId();
            if (wardTerritoryId == 0) return null;

            var sheet = DataManager.GetExcelSheet<TerritoryType>();
            if (sheet == null || !sheet.TryGetRow(wardTerritoryId, out var row))
                return null;

            var districtName = row.PlaceName.Value.Name.ToString();
            if (string.IsNullOrWhiteSpace(districtName)) return null;

            var ward = manager->GetCurrentWard();   // sbyte, -1 if not in a ward
            var plot = manager->GetCurrentPlot();   // sbyte, -1 if not on a plot
            var room = manager->GetCurrentRoom();   // short, room number if in a room

            if (ward >= 0 && plot >= 0)
            {
                if (room > 0)
                    return $"{districtName} (Ward {ward + 1}, Plot {plot + 1}, Room {room})";
                return $"{districtName} (Ward {ward + 1}, Plot {plot + 1})";
            }

            return districtName;
        }
        catch (Exception ex)
        {
            Log.Error($"Error resolving housing location: {ex}");
            return null;
        }
    }

// I dont know about this! — returns the string if non-empty, otherwise null (lets ?? chain cleanly)
private static string? NonEmpty(string? s) =>
    string.IsNullOrWhiteSpace(s) ? null : s;


    private async Task SendStatus(PlayerStatus status)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Configuration.SupabaseUrl) ||
                string.IsNullOrWhiteSpace(Configuration.SupabaseAnonKey))
            {
                    Log.Warning("Supabase update skipped: Missing URL or API Key.");
                return;
            }

            if (Configuration.EnableDebugLogging)
            {
                Log.Info($"[SENDING] Player: {status.Name}, Job: {status.Job}, World: {status.World}");
            }

            var endpoint = $"{Configuration.SupabaseUrl}/rest/v1/player_status?on_conflict=name";


            var json = JsonConvert.SerializeObject(new
            {
                name = status.Name,
                job = status.Job,
                territory = status.Territory,
                world = status.World,
                in_duty = status.InDuty,
                duty_name = status.DutyName,
                timestamp = status.Timestamp
            });

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Add("apikey", Configuration.SupabaseAnonKey);
            request.Headers.Add("Authorization", $"Bearer {Configuration.SupabaseAnonKey}");
            request.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");

            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                Log.Error($"Supabase insert failed: {response.StatusCode} | {body}");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Supabase error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        Framework.Update -= OnFrameworkUpdate;

        if (emoteHooks != null)
        {
            emoteHooks.OnEmoteTriggered -= OnEmoteTriggered;
            emoteHooks.Dispose();
        }

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        httpClient.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args) => ConfigWindow.Toggle();
    public void ToggleConfigUi() => ConfigWindow.Toggle();

}
