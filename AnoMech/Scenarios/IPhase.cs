using System;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios;

// A weather + bgm bundle grouping sibling scenarios (e.g. TOP P5's Delta/Sigma/Omega).
// Weather is the initial weather; a scenario may still change it live via world.SetWeather.
public interface IPhase
{
    string Name { get; }
    IZone Zone { get; }
    byte? Weather => null;
    ushort Bgm => 0;

    // Optional phase-wide setup, between zone and scenario Run. Default no-op.
    void Run(SimWorld world) { }
}

// Default phase. Pass Init to attach custom phase-wide setup.
public sealed class Phase : IPhase
{
    private readonly Action<SimWorld>? init;

    public Phase(IZone zone, string name, byte? weather, ushort bgm, Action<SimWorld>? init = null)
    {
        Zone = zone;
        Name = name;
        Weather = weather;
        Bgm = bgm;
        this.init = init;
    }

    public IZone Zone { get; }
    public string Name { get; }
    public byte? Weather { get; }
    public ushort Bgm { get; }

    public void Run(SimWorld world) => init?.Invoke(world);
}
