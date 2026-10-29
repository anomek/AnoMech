using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Fru.P4CrystallizeTime;

// Red players carry Wyrmclaw (cleansed by catching a dragon head), blue ones Wyrmfang (cleansed in
// a head's puddle).
public enum CrystallizeDebuff { RedAero, RedIce, BlueEruption, BlueIce, BlueDarkness, BlueWater }

public enum CrystallizeCorner { NW, NE, SE, SW }

// The debuffs, which hourglass pair is slowed, the corner the Tidal Light waves leave safe last,
// three Quietus carriers and the Spirit Taker target.
public sealed class FruP4CrystallizeTimeState
{
    public const float DebuffsAt = 11.8f;
    public const float DragonsSpawnAt = 15.3f;
    public const float DragonsMoveAt = 20.2f;
    public const float RedIceCleanseBy = 28.8f;
    public const float CleanseBy = 51.8f;
    public const float ReturnSavedAt = 44.8f;
    public const float StunAt = 51.8f;
    public const float StunEndsAt = 60.8f;
    public const float DragonInitialContactRadius = 2f;
    public const float DragonContactRadius = 1f;
    public const float DragonHitCooldown = 2f;
    public static readonly Vector3 FragmentPosition = new(0, 0, -16);

    // The eight debuffs, in enum order.
    public static readonly IReadOnlyList<CrystallizeDebuff> Dealt =
    [
        CrystallizeDebuff.RedAero, CrystallizeDebuff.RedAero, CrystallizeDebuff.RedIce, CrystallizeDebuff.RedIce,
        CrystallizeDebuff.BlueEruption, CrystallizeDebuff.BlueIce, CrystallizeDebuff.BlueDarkness, CrystallizeDebuff.BlueWater,
    ];

    private readonly Dictionary<PartyRole, CrystallizeDebuff> debuffs = new();
    private readonly Dictionary<PartyRole, Vector3> dragonPuddles = new();

    public bool SlowNorthwest { get; }
    public CrystallizeCorner Corner { get; }
    public IReadOnlyList<PartyRole> Quietus { get; }
    public PartyRole SpiritTakerTarget { get; }

    public FruP4CrystallizeTimeState(Rng rng, PartyRole playerRole, FruP4CrystallizeTimeStateOverrides overrides)
    {
        var deck = rng.Shuffle(Dealt).ToArray();
        foreach (var (role, pick) in ForcedDeal.Swap(rng, deck, overrides.PlayerDebuffs.Resolve(playerRole),
                     (wanted, card) => FruP4CrystallizeTimeStateOverrides.Matching(wanted).Contains(card)))
            DiagnosticLog.Warn($"[FruP4CrystallizeTime] {role} asked for {FruP4CrystallizeTimeStateOverrides.Label(pick)}, "
                + $"already held by other seats -- leaving {role} to the roll.");
        foreach (var role in Enum.GetValues<PartyRole>()) debuffs[role] = overrides.Debuffs?[role] ?? deck[(int)role];
        SlowNorthwest = rng.NextBool();
        Corner = (CrystallizeCorner)rng.Next(4);
        if (overrides.SlowNorthwest is { } slowNorthwest) SlowNorthwest = slowNorthwest;
        if (overrides.Corner is { } corner) Corner = corner;
        Quietus = overrides.Quietus ?? rng.Shuffle(Enum.GetValues<PartyRole>()).Take(3).ToList();
        SpiritTakerTarget = overrides.SpiritTakerTarget ?? rng.NextRole();
    }

    // A peer's shadow of the host's roll (see IMultiplayerReplayable): only what the Ai reads. The
    // puddles follow as the host's heads drop them.
    private FruP4CrystallizeTimeState(IReadOnlyList<CrystallizeDebuff> debuffs, bool slowNorthwest, CrystallizeCorner corner)
    {
        foreach (var role in Enum.GetValues<PartyRole>()) this.debuffs[role] = debuffs[(int)role];
        SlowNorthwest = slowNorthwest;
        Corner = corner;
        Quietus = [];
    }

    public static FruP4CrystallizeTimeState? FromNetworkReplay(IReadOnlyList<CrystallizeDebuff> debuffs, bool slowNorthwest, CrystallizeCorner corner)
        => debuffs.Order().SequenceEqual(Dealt) && Enum.IsDefined(corner) ? new(debuffs, slowNorthwest, corner) : null;

    public CrystallizeDebuff Debuff(PartyRole role) => debuffs[role];
    public IEnumerable<PartyRole> Holders(CrystallizeDebuff debuff) => debuffs.Where(d => d.Value == debuff).Select(d => d.Key);
    public static bool IsRed(CrystallizeDebuff debuff) => debuff is CrystallizeDebuff.RedAero or CrystallizeDebuff.RedIce;

    public bool East => Corner is CrystallizeCorner.NE or CrystallizeCorner.SE;
    public bool North => Corner is CrystallizeCorner.NW or CrystallizeCorner.NE;

    // The six hourglasses: N, NE, SE, S, SW, NW at radius 11.
    public static Vector3 Hourglass(int index)
    {
        var angle = index * MathF.PI / 3f;
        return new(11f * MathF.Sin(angle), 0, -11f * MathF.Cos(angle));
    }

    // The quickened pair goes off first, the slowed one last.
    public int[] HourglassPair(int wave) => wave == 0 ? [0, 3] : (wave == 2) == SlowNorthwest ? [5, 2] : [1, 4];

    // The first Tidal Light set crosses east-west, the second north-south, each towards the corner.
    public Vector3 WaveOrigin(bool second, int step)
        => second ? new(0, 0, (North ? -1 : 1) * (20 - 10 * step)) : new((East ? 1 : -1) * (20 - 10 * step), 0, 0);

    public Vector3 WaveDirection(bool second) => second ? new(0, 0, North ? 1 : -1) : new(East ? -1 : 1, 0, 0);

    // Each head flies half a circle from the north to the south, one either side.
    public static Vector3 DragonPosition(int side, float time)
    {
        var angle = MathF.Min(MathF.PI, MathF.Max(0, time - DragonsMoveAt) * DragonAngularSpeed + DragonStartAngle);
        return new((side == 0 ? -1 : 1) * 12.6f * MathF.Sin(angle), 0, -12.6f * MathF.Cos(angle));
    }

    private const float DragonStartAngle = 0.1526f;
    private const float DragonAngularSpeed = 0.237f;
    public const float DragonSpeed = 12.6f * DragonAngularSpeed;
    public const float DragonsReachSouthAt = DragonsMoveAt + (3.1415927f - DragonStartAngle) / DragonAngularSpeed;

    // Live: where each head dropped the puddle of the player it caught, as the scenario spawns them.
    public IReadOnlyDictionary<PartyRole, Vector3> DragonPuddles => dragonPuddles;
    internal void AddDragonPuddle(PartyRole catcher, Vector3 position) => dragonPuddles[catcher] = position;
    internal void RemoveDragonPuddle(PartyRole catcher) => dragonPuddles.Remove(catcher);
}
