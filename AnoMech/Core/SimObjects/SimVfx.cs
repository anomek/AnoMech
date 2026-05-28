using System;
using AnoMech.Core.Native;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Core.SimObjects;

// A single actor-attached VFX (head markers, looping arm-unit arrows, etc.)
// bound to a SimCharacter. Spawned and tracked by SimCharacter.AddVfx; ticks
// its own optional auto-expire countdown and frees the native VfxData on
// Despawn.
//
// Only *persistent* VFX become a SimVfx — fire-and-forget VFX (persistent:
// false at the AddVfx call) are spawned directly and never tracked, because
// the AVFX self-completes and the game frees its VfxData; calling
// ActorVfxRemove on one afterwards is the VfxData::Dtor crash.
//
// duration <= 0 means the VFX lives until RemoveVfx / ClearAttachedVfx /
// Despawn; duration > 0 adds a visible-time auto-expire on its own counter.
public sealed unsafe class SimVfx : ISimObject
{
    private float duration;
    private float elapsed;

    public string Path { get; }
    public IntPtr Handle { get; private set; }
    public bool IsActive => Handle != IntPtr.Zero;

    internal SimVfx(SimCharacter target, string path, float duration)
    {
        Path = path;
        this.duration = duration;
        var chara = (Character*)target.BattleCharaPtr;
        Handle = VfxFunctions.SpawnActorVfx(path, chara, chara);
    }

    // Restart the auto-expire countdown when the same path is re-added. A
    // re-add with no duration leaves any existing countdown alone (matches the
    // original lazy "re-adding the same path is a no-op" contract).
    public void Refresh(float duration)
    {
        if (duration <= 0f) return;
        this.duration = duration;
        elapsed = 0f;
    }

    public void Tick(float deltaSeconds)
    {
        if (!IsActive || duration <= 0f) return;
        elapsed += deltaSeconds;
        if (elapsed >= duration) Despawn();
    }

    public void Despawn()
    {
        if (!IsActive) return;
        VfxFunctions.RemoveActorVfx(Handle);
        Handle = IntPtr.Zero;
    }
}
