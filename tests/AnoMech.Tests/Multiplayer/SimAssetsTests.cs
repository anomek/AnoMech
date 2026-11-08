using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;

namespace AnoMech.Tests;

// A peer drops what names an id SimAssets doesn't know. Every id a run gives the client, in every
// strat of every scenario played solo, has to be one it knows: each spawn's BNpcBase, model,
// layout, event object and EventId, each tether, and each action cast or played. The zone-in's
// are each client's own.
public class SimAssetsTests
{
    private const uint SetTetherCategory = 0x23;

    [Test]
    public void EveryIdTheSimGivesTheClientIsOneAPeerTakes()
    {
        var unknown = new Dictionary<(SimAssetKind Kind, ulong Id), string>();
        ScenarioCensus.EveryStrat((run, fake) =>
        {
            void Check(SimAssetKind kind, ulong id)
            {
                if (id != 0 && !SimAssets.IsKnown(kind, id)) unknown.TryAdd((kind, id), run);
            }

            void CheckAction(uint actionId)
            {
                if (!SimAssets.AllowAction(actionId, "test")) unknown.TryAdd((SimAssetKind.Action, actionId), run);
            }

            foreach (var (_, data) in fake.Timeline.RunGiven)
                switch (data)
                {
                    case SpawnData { Config: EnemySpawnConfig npc }:
                        Check(SimAssetKind.BNpcBase, npc.BNpcBaseId);
                        Check(SimAssetKind.ModelChara, npc.ModelCharaId);
                        Check(SimAssetKind.Layout, npc.LayoutId);
                        break;
                    case SpawnData { Config: EventObjectSpawnConfig obj }:
                        Check(SimAssetKind.EObj, obj.EObjId);
                        Check(SimAssetKind.Layout, obj.LayoutId);
                        Check(SimAssetKind.EventId, (uint)obj.EventId);
                        break;
                    case ActorCastData cast:
                        CheckAction(cast.ActionId);
                        break;
                    case ActionEffectData effect:
                        CheckAction(effect.ActionId);
                        break;
                }
            foreach (var row in fake.Timeline.RunRows)
                if (row["kind"] is "ac" or "ac_self" && Convert.ToUInt32(row["id"], 16) == SetTetherCategory
                    && row["p"].Split(',') is { Length: > 1 } args)
                    Check(SimAssetKind.Tether, Convert.ToUInt32(args[1], 16));
        });
        Assert.That(unknown.Select(u => $"{u.Value} gave the client {u.Key.Kind} {u.Key.Id} (0x{u.Key.Id:X})"), Is.Empty,
            "ids a peer drops: name each as a constant in a class or field named for its kind");
    }
}
