using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.IO;
using System.Numerics;

namespace StatusPulse.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly string memeImagePath;


    public ConfigWindow(StatusPulsePlugin plugin) : base("ENVIRONMENT CONFIGS")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(750, 750);
        SizeCondition = ImGuiCond.Always;

        configuration = plugin.Configuration;
        memeImagePath = Path.Combine(StatusPulsePlugin.PluginInterface.AssemblyLocation.Directory?.FullName!, "hankimages","hankconfig.jpg");


    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Text("Supabase Settings");

        // Supabase URL
        var supabaseUrl = configuration.SupabaseUrl;
        if (ImGui.InputText("Supabase URL", ref supabaseUrl, 200))
        {
            configuration.SupabaseUrl = supabaseUrl;
                configuration.Save();
        }

        // Supabase Anon Key
        var supabaseKey = configuration.SupabaseAnonKey;
        if (ImGui.InputText("Supabase Anon Key", ref supabaseKey, 512, ImGuiInputTextFlags.Password))
            
            {
                configuration.SupabaseAnonKey = supabaseKey;
                configuration.Save();
        }

        ImGui.Separator();
        ImGui.Text("Debug Options");

        // Enable Debug Logging 
        var debugLogging = configuration.EnableDebugLogging;
        if (ImGui.Checkbox("Enable Debug Logging", ref debugLogging))
        {
            configuration.EnableDebugLogging = debugLogging;
            configuration.Save();
        }


        ImGui.Separator();
        ImGui.Text("HANK VOIGHT FROM CHICAGO PD !");
        var memeTexture = StatusPulsePlugin.TextureProvider.GetFromFile(memeImagePath).GetWrapOrDefault();
        if (memeTexture != null)
        {
            ImGui.Image(memeTexture.Handle, new Vector2(512, 512));
        }
        else
        {
            ImGui.TextUnformatted("Meme image not found.");
        }


    }
}

