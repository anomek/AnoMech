using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

internal sealed class FakeHaterList : IHaterList
{
    public void Receive(IReadOnlyList<Hater> haters) { }
}
