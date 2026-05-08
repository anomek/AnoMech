using System;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace UltiSim.Core;

// Thin wrapper around Client::Game::BGMSystem for scenario music. SetBGM
// writes into the Content scene (sceneId 3 = instance music) and starts
// the SCD from the beginning — there is no seek-into-song API exposed by
// the engine. Reset hands the slot back so the territory/director BGM
// can resume.
public sealed unsafe class Bgm : IDisposable
{
    private const uint ContentSceneId = 3;

    public void Play(ushort bgmId)
    {
        if (BGMSystem.Instance() == null) return;
        BGMSystem.SetBGM(bgmId, ContentSceneId);
    }

    public void Reset()
    {
        var system = BGMSystem.Instance();
        if (system != null) system->ResetBGM(ContentSceneId);
    }

    public void Dispose() => Reset();
}
