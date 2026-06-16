using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using System;
using System.Linq;

namespace StatusPulse;

public class EmoteReaderHooks : IDisposable
{
    // Fires only when someone emotes at the local player
    // instigator = who did the emote, emoteId = which emote
    public Action<IPlayerCharacter, ushort>? OnEmoteTriggered;

    public delegate void OnEmoteFuncDelegate(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2);
    private readonly Hook<OnEmoteFuncDelegate>? hookEmote;

    public bool IsValid { get; private set; } = false;

    public EmoteReaderHooks()
    {
        try
        {
            // Hook the game's emote function - fires globally for every emote in the zone
            hookEmote = StatusPulsePlugin.GameInteropProvider.HookFromSignature<OnEmoteFuncDelegate>(
                "E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 4C 89 74 24", OnEmoteDetour);
            hookEmote.Enable();
            IsValid = true;
            StatusPulsePlugin.Log.Info("[EmoteMirror] Emote hook enabled");
        }
        catch (Exception ex)
        {
            StatusPulsePlugin.Log.Error(ex, "[EmoteMirror] Failed to hook emote function");
        }
    }

    private void OnEmoteDetour(ulong unk, ulong instigatorAddr, ushort emoteId, ulong targetId, ulong unk2)
    {
        try
        {
            var localPlayer = StatusPulsePlugin.ObjectTable.LocalPlayer;

            // Filter: only care about emotes where we are the target
            if (localPlayer != null && targetId == localPlayer.GameObjectId)
            {
                var instigator = StatusPulsePlugin.ObjectTable
                    .FirstOrDefault(x => (ulong)x.Address == instigatorAddr) as IPlayerCharacter;

                if (instigator != null &&
                    instigator.ObjectKind == ObjectKind.Pc &&
                    instigator.GameObjectId != localPlayer.GameObjectId) // not ourselves
                {
                    OnEmoteTriggered?.Invoke(instigator, emoteId);
                }
            }
        }
        catch (Exception ex)
        {
            StatusPulsePlugin.Log.Error(ex, "[EmoteMirror] Error in emote detour");
        }
        finally
        {
            // Always call original — game must process the emote normally regardless
            hookEmote!.Original(unk, instigatorAddr, emoteId, targetId, unk2);
        }
    }

    public void Dispose()
    {
        hookEmote?.Dispose();
        IsValid = false;
    }
}
