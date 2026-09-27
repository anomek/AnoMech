using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AnoMech.Multiplayer;
using AnoMech.Network;
using static AnoMech.Tests.TestWire;

namespace AnoMech.Tests;

public class ProtocolTests
{
    // The peer types a relay passes from peers to the host; the rest are the host's alone.
    private static readonly string[] PeerTypes = ["hello", "claim", "release", "pose", "pong", "startCheckResponse", "startAbort", "sessionEnded",
        "resetRequest", "leaveRequest", "selfMitigation", "peerAppliedEnemyStatus", "peerAppliedRoleStatus"];

    // Host messages are only typed by RelayWire; the rest falls to deserialization.
    [Test]
    public void HostMessagesAreTypedThenDeserialized()
    {
        var id = RelayWire.PeerId(RelayWire.NewSecret());
        Rejects(() => Receive(Bytes("{\"t\":\"ping\",\"SentAtMs\":1e999}"), true, id), "out-of-range number refused");
        Rejects(() => Receive(Bytes("{\"t\":\"ping\",\"SentAtMs\":1"), true, id), "truncated host message refused");
        Rejects(() => Receive(Bytes("{\"t\":\"notAMessage\"}"), true, id), "unknown host message type refused");
        Assert.That(Receive(Bytes("{\"t\":\"ping\",\"SentAtMs\":5}"), true, id), Is.TypeOf<PingMessage>().With.Property(nameof(PingMessage.SentAtMs)).EqualTo(5));
    }

    [Test]
    public void FullEightPlayerSnapshotIsAccepted()
    {
        var id = RelayWire.PeerId(RelayWire.NewSecret());
        var role = new RoleState(default, true, false, 0, 0, 0, 0, [], [], 10000, 10000);
        var message = new RolesSnapshotMessage(Enumerable.Repeat(role, 8).ToList());
        Assert.That(RelayWire.Validate(JsonSerializer.SerializeToUtf8Bytes<MpMessage>(message), true, id), Is.EqualTo("rolesSnapshot"));
    }

    [Test]
    public void EveryMessageFormRoundTripsAndOnlyPeerTypesPassFromPeers()
    {
        var id = RelayWire.PeerId(RelayWire.NewSecret());
        var forms = typeof(MpMessage).GetCustomAttributes<JsonDerivedTypeAttribute>().Select(a => a.DerivedType).ToArray();
        var peerForms = 0;
        foreach (var form in forms)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(Sample<MpMessage>(form, id));
            var type = RelayWire.Validate(bytes, true, id);
            Assert.That(Receive(bytes, true, id), Is.TypeOf(form), $"{type} round-trips from the host");
            if (!PeerTypes.Contains(type))
            {
                Rejects(() => RelayWire.Validate(bytes, false, id), $"{type} refused from a peer");
                continue;
            }
            // Every Guid field is the sender's id, so a peer type names its real sender.
            Assert.That(Receive(bytes, false, id), Is.TypeOf(form), $"{type} accepted from its sender");
            Rejects(() => RelayWire.Validate(bytes, false, Guid.NewGuid()), $"{type} refused from anyone else");
            peerForms++;
        }
        Assert.That(forms, Has.Length.GreaterThan(30));
        Assert.That(peerForms, Is.EqualTo(PeerTypes.Length), "every peer type checked");
    }

    [Test]
    public void LargeRealSnapshotIsAccepted()
    {
        var id = RelayWire.PeerId(RelayWire.NewSecret());
        var enemy = Sample<EnemyState>(typeof(EnemyState), id) with
        {
            Statuses = Enumerable.Repeat(new EnemyStatusState(1, 1, 30), 64).ToArray(),
            NewVfx = Enumerable.Repeat(new AttachedVfxState("vfx/x.avfx", 1f), 200).ToArray(),
        };
        var message = new WorldSnapshotMessage(Enumerable.Repeat(enemy, 256).ToList(), [], []);
        Assert.That(Receive(JsonSerializer.SerializeToUtf8Bytes<MpMessage>(message), true, id), Is.TypeOf<WorldSnapshotMessage>());
    }

    private static T Sample<T>(Type type, Guid id)
    {
        var constructor = type.GetConstructors().Single();
        return (T)constructor.Invoke(constructor.GetParameters().Select(p => SampleValue(p.ParameterType, id)).ToArray());
    }

    private static object? SampleValue(Type type, Guid id)
    {
        if (type == typeof(Guid)) return id;
        if (type == typeof(string)) return "";
        if (type.IsValueType) return Activator.CreateInstance(type);
        if (type.IsArray) return Array.CreateInstance(type.GetElementType()!, 0);
        if (type.IsGenericType)
        {
            var concrete = type.IsInterface ? typeof(List<>).MakeGenericType(type.GenericTypeArguments) : type;
            return Activator.CreateInstance(concrete);
        }
        return null;
    }
}
