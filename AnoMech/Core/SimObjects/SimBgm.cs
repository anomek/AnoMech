using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.SimObjects;

// The run's music track. One per world: SimWorld.PlayBgm replaces any previous track.
internal sealed class SimBgm : ISimObject
{
    public bool IsActive => true;

    // Called once seated: replacing a track despawns (resets) the previous one first.
    internal void Play(ushort bgmId, float secondsIn) => Natives.Bgm.Play(bgmId, secondsIn);

    internal void Switch(ushort bgmId) => Natives.Bgm.Switch(bgmId);

    internal void Silence() => Natives.Bgm.Silence();

    public void Tick(float deltaSeconds) => Natives.Bgm.Tick(deltaSeconds);

    public void Despawn() => Natives.Bgm.Reset();
}
