using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AnoMech.Multiplayer;
using AnoMech.Network;
using static AnoMech.Tests.TestWire;

namespace AnoMech.Tests;

public class ProtocolTests
{
    private static readonly Type[] Forms = typeof(MpMessage).GetCustomAttributes<JsonDerivedTypeAttribute>().Select(a => a.DerivedType).ToArray();

    // The types a relay passes from peers to the host; the rest are the host's alone.
    private static readonly string[] PeerTypes = typeof(MpMessage).GetCustomAttributes<JsonDerivedTypeAttribute>()
        .Where(a => typeof(IPeerMessage).IsAssignableFrom(a.DerivedType)).Select(a => (string)a.TypeDiscriminator!).ToArray();

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
        var role = new RoleState(default, 0, 0, 0, 0, 0, 0, false);
        var message = new RolesSnapshotMessage(Enumerable.Repeat(role, 8).ToList());
        Assert.That(RelayWire.Validate(JsonSerializer.SerializeToUtf8Bytes<MpMessage>(message), true, id), Is.EqualTo("rolesSnapshot"));
    }

    // So a new message can't skip deciding who may send it.
    [Test]
    public void EveryMessageIsEitherTheHostsAloneOrAPeers()
    {
        var unclassified = Forms.Where(f => typeof(IHostOnlyMessage).IsAssignableFrom(f) == typeof(IPeerMessage).IsAssignableFrom(f));
        Assert.That(unclassified.Select(f => f.Name), Is.Empty);
    }

    [Test]
    public void EveryMessageFormRoundTripsAndOnlyPeerTypesPassFromPeers()
    {
        var id = RelayWire.PeerId(RelayWire.NewSecret());
        Assert.That(RelayWire.PeerMessageTypes, Is.EquivalentTo(PeerTypes), "the relay's peer types are the IPeerMessage ones");
        foreach (var form in Forms)
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
        }
        Assert.That(Forms, Has.Length.GreaterThan(40));
    }

    // A frame that sends every status list at once, each slot full: the run's start.
    [Test]
    public void LargeRealPacketFrameIsAccepted()
    {
        var id = RelayWire.PeerId(RelayWire.NewSecret());
        var slots = Enumerable.Range(0, 60).Select(i => new StatusSlotPacket((byte)i, 1, 1, 30, 1)).ToList();
        var packets = Enumerable.Range(0, 256).Select(i => (GamePacket)new StatusListPacket(0x4000FE00u + (uint)i, slots, 100, 100, 10000, 0)).ToList();
        var message = new GamePacketsMessage(packets);
        Assert.That(Receive(JsonSerializer.SerializeToUtf8Bytes<MpMessage>(message), true, id), Is.TypeOf<GamePacketsMessage>());
    }

    [Test]
    public void EveryPacketFormRoundTrips()
    {
        var id = RelayWire.PeerId(RelayWire.NewSecret());
        var forms = typeof(GamePacket).GetCustomAttributes<JsonDerivedTypeAttribute>().Select(a => a.DerivedType).ToArray();
        var message = new GamePacketsMessage(forms.Select(form => Sample<GamePacket>(form, id)).ToList());
        var received = Receive(JsonSerializer.SerializeToUtf8Bytes<MpMessage>(message), true, id);
        Assert.That(((GamePacketsMessage)received).Packets.Select(p => p.GetType()), Is.EqualTo(forms));
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
