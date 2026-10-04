using System;
using System.IO;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Sound;
using Lumina.Excel.Sheets;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.Native.Implementations;

// Thin wrapper around Client::Game::BGMSystem for scenario music. SetBGM
// writes into the Content scene (sceneId 3 = instance music) and starts
// the SCD from the beginning. Reset hands the slot back so the territory/director BGM
// can resume.
//
// BGMSystem can't start a track mid-way. Tried and inert: the scene's "disable restart" resume
// entry (a timer, not a position) and ISoundData.SetSpeed on the music sound (reads back, still
// plays at 1x). A track that must start mid-way plays on our own output instead.
internal sealed unsafe class Bgm : IBgm, IDisposable
{
    private const uint ContentSceneId = 3;
    private const ushort SilentBgmId = 1;

    // The bgm id currently forced into the content scene, or 0 when the slot has
    // been handed back to the territory/director. Lets Play() skip a redundant
    // restart when switching between scenarios that share a track.
    private ushort current;
    private MusicPlayer? player;
    private float trackVolume = 1f;
    // The track a Switch moved away from, fading out on our output as the game's own output would.
    private MusicPlayer? fading;
    private float fadingTrackVolume;

    public void Play(ushort bgmId, float secondsIn = 0f)
    {
        if (bgmId == 0) return; // 0 = use Reset
        if (BGMSystem.Instance() == null) return;
        if (secondsIn <= 0f && bgmId == current && player == null) return; // same id = keep playing
        StopPlayer();
        if (secondsIn > 0f) StopFading();
        var started = secondsIn > 0f && StartPlayer(bgmId, secondsIn);
        BGMSystem.SetBGM(started ? SilentBgmId : bgmId, ContentSceneId);
        current = bgmId;
    }

    public void Sync(float secondsIn)
    {
        if (current == 0) { DiagnosticLog.Info("[Bgm] Sync: no scenario track is playing."); return; }
        Play(current, secondsIn);
    }

    // The fight's own change of track, from the new one's top, not a hand-back: Reset would let
    // the territory's music in. Nothing to do while no scenario track plays.
    public void Switch(ushort bgmId)
    {
        if (current == 0 || bgmId == current) return;
        FadeOutPlayer();
        Play(bgmId);
    }

    // The director's null track.
    public void Silence() => Switch(SilentBgmId);

    public void Reset()
    {
        StopPlayer();
        StopFading();
        if (current == 0) return;
        var system = BGMSystem.Instance();
        if (system != null) system->ResetBGM(ContentSceneId);
        current = 0;
    }

    public void Tick(float deltaSeconds)
    {
        if (player != null) player.Volume = trackVolume * MusicVolume();
        if (fading == null) return;
        if (fading.FadedOut) StopFading();
        else fading.Volume = fadingTrackVolume * MusicVolume();
    }

    public void LogPosition(string label)
        => DiagnosticLog.Info($"[Bgm] {label}: track {current} {(player != null ? $"on our output at {player.PositionSeconds:F2}s, volume {player.Volume:F2}" : "on the game's own output")}.");

    private bool StartPlayer(ushort bgmId, float secondsIn)
    {
        var path = Plugin.DataManager.GetExcelSheet<BGM>()?.GetRowOrDefault(bgmId)?.File.ExtractText();
        var scd = string.IsNullOrEmpty(path) ? null : Plugin.DataManager.GetFile(path)?.Data;
        if (scd == null) { DiagnosticLog.Warn($"[Bgm] Track {bgmId} ({path}) has no readable file; playing it from the top instead."); return false; }
        var ogg = ScdOgg.Extract(scd, out var error);
        if (ogg == null) { DiagnosticLog.Warn($"[Bgm] Track {bgmId} ({path}): {error}; playing it from the top instead."); return false; }
        trackVolume = ScdOgg.SoundVolume(scd);
        var volume = MusicVolume();
        try
        {
            player = MusicPlayer.Start(ogg, secondsIn, trackVolume * volume, out error);
        }
        catch (Exception e) when (e is FileNotFoundException or FileLoadException or TypeLoadException)
        {
            // The NAudio/NVorbis dlls are missing from the plugin folder or failed to load.
            error = $"{e.GetType().Name}: {e.Message}";
        }
        if (player == null) { DiagnosticLog.Warn($"[Bgm] Track {bgmId} ({path}) failed to start on our output ({error}); playing it from the top instead."); return false; }
        DiagnosticLog.Info($"[Bgm] Track {bgmId} ({path}) started on our output at {secondsIn:F2}s, track mix {trackVolume:F2}, music volume {volume:F2}; the content scene holds the silent track.");
        return true;
    }

    private void StopPlayer()
    {
        if (player == null) return;
        player.Dispose();
        player = null;
    }

    private void FadeOutPlayer()
    {
        if (player == null) return;
        StopFading();
        fading = player;
        fadingTrackVolume = trackVolume;
        player = null;
        fading.FadeOut(DirectorFadeOutSeconds());
    }

    private void StopFading()
    {
        if (fading == null) return;
        fading.Dispose();
        fading = null;
    }

    // UNVERIFIED: the director's track change carries no fade and no BGMFade row names the content
    // scene, so the game's default fade type is assumed.
    private static float DirectorFadeOutSeconds()
        => Plugin.DataManager.GetExcelSheet<BGMFadeType>()?.GetRowOrDefault(0)?.FadeOutTime ?? 0f;

    // GetEffectiveVolume throws if its signature didn't resolve.
    private static bool volumeFallbackLogged;

    // The Music bus carries the BGM slider, the BGM mute and the background-window rule; the game
    // applies the master volume and mute at its output instead, so they're multiplied in here.
    private static float MusicVolume()
    {
        var manager = SoundManager.Instance();
        if (manager == null || manager->Disabled || manager->IsSndMaster) return 0f;
        var master = manager->MasterVolume * manager->ActiveVolume;
        try
        {
            return manager->GetEffectiveVolume(SoundBus.Music) * master;
        }
        catch (InvalidOperationException e)
        {
            if (!volumeFallbackLogged) DiagnosticLog.Warn($"[Bgm] Music volume unreadable ({e.Message}); our output follows the master volume only.");
            volumeFallbackLogged = true;
            return master;
        }
    }

    public void Dispose() => Reset();
}
