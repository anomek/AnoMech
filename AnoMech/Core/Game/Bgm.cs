using System;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.Game;

// Thin wrapper around Client::Game::BGMSystem for scenario music. SetBGM
// writes into the Content scene (sceneId 3 = instance music) and starts
// the SCD from the beginning — there is no seek-into-song API exposed by
// the engine. Reset hands the slot back so the territory/director BGM
// can resume.
public sealed unsafe class Bgm : IDisposable
{
    private const uint ContentSceneId = 3;

    // The bgm id currently forced into the content scene, or 0 when the slot has
    // been handed back to the territory/director. Lets Play() skip a redundant
    // restart when switching between scenarios that share a track.
    private ushort current;

    public void Play(ushort bgmId)
    {
        if (bgmId == 0 || bgmId == current) return; // 0 = use Reset; same id = keep playing
        if (BGMSystem.Instance() == null) return;
        BGMSystem.SetBGM(bgmId, ContentSceneId);
        current = bgmId;
    }

    public void Reset()
    {
        if (current == 0) return;
        var system = BGMSystem.Instance();
        if (system != null) system->ResetBGM(ContentSceneId);
        current = 0;
    }

    public void Dispose() => Reset();
}
