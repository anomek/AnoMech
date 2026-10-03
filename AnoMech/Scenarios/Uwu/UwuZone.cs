using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Uwu;

// Primal phases get a "P<n>" menu prefix; all share one BGM.
public sealed class UwuZone : IZone
{
    public static readonly UwuZone Instance = new();
    // Each primal uses its trial arena's weather; Ultimania is Ultima's.
    private const byte GalesWeather = 28;
    private const byte HeatWavesWeather = 26;
    private const byte EruptionsWeather = 29;
    public static readonly Phase Garuda = new(Instance, "P1", GalesWeather, 547);
    public static readonly Phase Ifrit = new(Instance, "P2", HeatWavesWeather, 547);
    public static readonly Phase Titan = new(Instance, "P3", EruptionsWeather, 547);
    public static readonly Phase Ultima = new(Instance, "", 95, 547);

    public string Name => "The Weapon's Refrain";
    public uint TerritoryId => 777;
    public Vector3 Origin => new(100f, 0f, 100f);
    public byte Level => UwuConstants.Level;
    public ushort ItemLevel => UwuConstants.ItemLevel;

    public IReadOnlyList<WaymarkLayout> WaymarkPresets { get; } =
        [new WaymarkLayout("Standard", UwuConstants.StandardWaymarks), new WaymarkLayout("Naur", UwuConstants.NaurWaymarks)];

    public void Run(SimWorld world) => world.EnforceArenaBoundary(UwuConstants.Geometry.ArenaRadius);
}
