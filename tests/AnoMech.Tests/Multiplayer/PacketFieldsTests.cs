using System.Globalization;
using System.Reflection;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad;
using AnoMech.Scenarios.Umad.P3BlackHole;

namespace AnoMech.Tests;

// Each peer's client gets every field of what the host's client is given. The fight's latest NPC
// spawn, event object spawn, cast and action effect are sent again by the host with every field
// changed but the Kept ones, and each has to reach the peer's client exactly as the host's got it:
// a field the packets don't carry fails here as soon as the sim has it.
public class PacketFieldsTests
{
    private const double SendAt = 10;

    // The ids a peer checks against what this build sends, the actors it names as its own, and
    // where things stand: the fight's own calls cover those.
    private static readonly HashSet<string> Kept = ["BNpcBaseId", "ModelCharaId", "LayoutId", "EObjId", "EventId", "ActionId",
        "EntityId", "OwnerId", "Target", "Ballista", "AnimationTarget", "Targets", "Placement", "Position"];

    [Test]
    public void EveryFieldOfWhatTheHostsClientIsGivenReachesThePeers()
    {
        var sent = new List<(string Kind, uint Actor)>();
        var sentAt = 0.0;
        var run = MultiplayerRun.Play(typeof(UmadP3BlackHoleScenario), 0, 1, new PlayerSeat(PartyRole.MeleeDpsA), [new PlayerSeat(PartyRole.CasterDps)],
            immortal: true, inputs: [(SendAt, 0, host =>
            {
                sentAt = host.Fake.Timeline.Clock();
                sent.AddRange(SendAltered(host));
            })]);
        Assert.That(run.Failure, Is.Null);
        Assert.That(sent.Select(s => s.Kind), Is.EquivalentTo(new[] { "spawn", "eobj_spawn", "cast", "ability" }), "what the fight gave by then");

        var peer = run.Peers[0];
        var problems = new List<string>();
        foreach (var (kind, actor) in sent)
        {
            var name = run.Host.NameAt(actor, double.MaxValue);
            var hostGot = Got(run.Host, kind, sentAt, e => e == actor);
            var peerGot = Got(peer, kind, sentAt, e => peer.NameAt(e, double.MaxValue) == name);
            if (hostGot != peerGot) problems.Add($"{kind} by {name}:{Environment.NewLine}  host: {hostGot}{Environment.NewLine}  peer: {peerGot ?? "never got it"}");
        }
        Assert.That(problems, Is.Empty, string.Join(Environment.NewLine, problems));
    }

    // The latest of each the host's client got, sent again altered through the host's natives, as
    // the sim would send it. The latest, not the first: the zone's own entrance comes before the
    // run, and each client makes that one for itself.
    private static List<(string Kind, uint Actor)> SendAltered(TestClient host)
    {
        var given = host.Fake.Timeline.Given.Reverse().ToList();
        var sent = new List<(string, uint)>();
        if (given.Select(g => g.Data).OfType<SpawnData>().FirstOrDefault(s => s.Config is EnemySpawnConfig) is { } npc
            && Natives.BattleCharas.SpawnBattleNpcFromPacket(Altered((EnemySpawnConfig)npc.Config), npc.Placement, out var npcId) != null)
            sent.Add(("spawn", npcId));
        if (given.Select(g => g.Data).OfType<SpawnData>().FirstOrDefault(s => s.Config is EventObjectSpawnConfig) is { } obj
            && Natives.EventObjects.Spawn(Altered((EventObjectSpawnConfig)obj.Config), obj.Placement) is { } prop)
            sent.Add(("eobj_spawn", prop.EntityId));
        if (ByAnEnemy<ActorCastData>(host, given) is ({ } caster, var cast))
        {
            caster.ReceiveActorCast(Altered(cast));
            sent.Add(("cast", caster.EntityId));
        }
        if (ByAnEnemy<ActionEffectData>(host, given) is ({ } source, var effect))
        {
            source.ReceiveActionEffect(Altered(effect));
            sent.Add(("ability", source.EntityId));
        }
        return sent;
    }

    private static (IBattleCharaProxy? Proxy, T Data) ByAnEnemy<T>(TestClient host, List<(Dictionary<string, string> Row, object Data)> given)
    {
        foreach (var (row, data) in given)
            if (data is T t && host.Game.World.Children.OfType<SimEnemy>()
                    .FirstOrDefault(e => e.EntityId == uint.Parse(row["actor"], NumberStyles.HexNumber)) is { Proxy: { } proxy })
                return (proxy, t);
        return (null, default!);
    }

    // Every property changed but the Kept ones: a flag flipped, another enum value, a number one up,
    // an absent value given one.
    private static T Altered<T>(T value) where T : notnull
    {
        var copy = typeof(T).IsValueType
            ? value
            : typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(value, null)!;
        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (Kept.Contains(property.Name) || ClientParity.HostOnly.Contains(property.Name)) continue;
            property.SetValue(copy, Changed(property.PropertyType, property.GetValue(copy), $"{typeof(T).Name}.{property.Name}"));
        }
        return (T)copy;
    }

    private static object Changed(Type type, object? value, string what)
    {
        if (Nullable.GetUnderlyingType(type) is { } inner)
            return Changed(inner, value ?? Activator.CreateInstance(inner), what);
        if (type == typeof(NpcAppearance))
        {
            var gear = value as NpcAppearance ?? new NpcAppearance(Equipment: new uint[10], Customize: new byte[26]);
            return new NpcAppearance(gear.MainHand + 1, gear.OffHand + 1, gear.Equipment?.Select(e => e + 1).ToArray(),
                gear.Customize?.Select(c => (byte)(c + 1)).ToArray());
        }
        if (type == typeof(IReadOnlyList<SpawnStatus>))
            return (value as IReadOnlyList<SpawnStatus> ?? [new SpawnStatus(UmadConstants.StatusId.FirstInLine)])
                .Select(s => s with { Param = (ushort)(s.Param + 1) }).ToList();
        return value switch
        {
            bool b => !b,
            Enum e => Enum.GetValues(type).Cast<object>().First(v => !v.Equals(e)),
            byte n => (byte)(n + 1),
            sbyte n => (sbyte)(n + 1),
            ushort n => (ushort)(n + 1),
            uint n => n + 1,
            int n => n + 1,
            float f => f + 0.25f,
            _ => throw new InvalidOperationException($"{what}: no way to change a {type.Name} -- keep it, or teach Changed one."),
        };
    }

    // The fields of the first `kind` the client got from `from` on by an actor `which` takes.
    private static string? Got(TestClient client, string kind, double from, Func<uint, bool> which)
    {
        foreach (var (row, _) in client.Fake.Timeline.Given)
        {
            var at = double.Parse(row["t"], CultureInfo.InvariantCulture);
            if (row["kind"] == kind && at >= from - 1e-3 && which(uint.Parse(row["actor"], NumberStyles.HexNumber)))
                return ClientParity.Fields(client, row, at);
        }
        return null;
    }
}
