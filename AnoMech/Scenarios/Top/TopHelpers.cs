using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top;

// The real fight's gimmick casters: 9020 helpers the engine builds from their spawn packet, which
// gives an action timeline a draw object to run on. A plain 9020 loads no model, so its hit effect
// never plays; it only stands in for a helper the engine dropped. The engine builds a packet actor
// a few frames late, so a pool is spawned well ahead of its first cast.
public sealed class TopHelpers
{
    private readonly SimWorld world;
    private readonly EnemySpawnConfig plain;
    private readonly string tag;
    private readonly SimEnemy?[] helpers;
    private readonly Placement[] placements;
    private int next;

    public TopHelpers(SimWorld world, uint nameId, int count, string tag)
    {
        this.world = world;
        this.tag = tag;
        plain = new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.OmegaHelper,
            NameId: nameId,
            Level: 1,
            Targetable: false,
            EnemyList: EnemyListMode.Never,
            IsVisible: false);
        helpers = new SimEnemy?[count];
        placements = new Placement[count];
        for (var i = 0; i < count; i++)
            helpers[i] = world.SpawnEnemy(plain with { NpcSpawnTemplate = TopRealPackets.HelperNpcSpawn, PacketSpawnEnableDraw = true })
                ?? world.SpawnEnemy(plain);
    }

    // Round-robin past any helper still casting or playing its last hit, so none is moved
    // mid-effect; when every one is busy, the next in turn is taken anyway.
    public SimEnemy? Next(Placement placement)
    {
        for (var i = 0; i < helpers.Length; i++)
        {
            var index = (next + i) % helpers.Length;
            if (helpers[index] is { AnimationLock: true } or { PacketSpawnPending: true }) continue;
            next = index + 1;
            return At(index, placement);
        }
        return At(next++ % helpers.Length, placement);
    }

    public SimEnemy? At(int index, Placement placement)
    {
        placements[index] = placement;
        return At(index);
    }

    // At its last placement; null while the engine is still building it.
    public SimEnemy? At(int index)
    {
        var helper = helpers[index];
        if (helper is null or { PacketSpawnFailed: true } or { IsActive: false })
        {
            helper?.Despawn();
            helper = helpers[index] = world.SpawnEnemy(plain with { Placement = placements[index] });
        }
        if (helper is { PacketSpawnPending: true })
        {
            DiagnosticLog.Warn($"[{tag}] helper {index} is still awaiting its packet actor -- skipped this frame.");
            return null;
        }
        helper?.SetPosition(placements[index]);
        return helper;
    }
}
