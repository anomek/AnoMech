using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

internal sealed class FakeBgm : IBgm
{
    public void Play(ushort bgmId, float secondsIn = 0f) { }
    public void Sync(float secondsIn) { }
    public void Switch(ushort bgmId) { }
    public void Silence() { }
    public void Reset() { }
    public void Tick(float deltaSeconds) { }
    public void LogPosition(string label) { }
}
