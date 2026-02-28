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

    private const string CommandName = "/pulse";
    private const double UpdateInterval = 60;
    private double timeSinceLastUpdate = 0;

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("StatusPulse");
    private ConfigWindow ConfigWindow { get; init; }
    private readonly HttpClient httpClient = new();


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
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        timeSinceLastUpdate += Framework.UpdateDelta.TotalSeconds;

        var player = ObjectTable.LocalPlayer;
        if (player == null || timeSinceLastUpdate < UpdateInterval)
            return;

        if (Framework.UpdateDelta.TotalSeconds <= 0)
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

            // Territory name from the TerritoryType sheet
            var territorySheet = DataManager.GetExcelSheet<TerritoryType>();
            if (territorySheet != null && territorySheet.TryGetRow(territoryId, out var territoryRow))
            {

                territoryName = territoryRow.PlaceName.Value.Name.ToString();
            }

            // Duty info from ContentFinderCondition sheet
            var cfcSheet = DataManager.GetExcelSheet<ContentFinderCondition>();
            if (cfcSheet != null)
            {
                // RowRef<TerritoryType> uses RowId (uint)
                var cfc = cfcSheet.FirstOrDefault(x => x.TerritoryType.RowId == territoryId);

                // FirstOrDefault returns a struct; default(ContentFinderCondition) if no match
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

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        httpClient.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args) => ConfigWindow.Toggle();
    public void ToggleConfigUi() => ConfigWindow.Toggle();

}
