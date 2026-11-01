using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using AnoMech.Multiplayer;

namespace AnoMech.Tests;

// The fake stands in for the real client in every test, so what it gets wrong passes everywhere and
// only shows in game. These hold it to what the client has been seen doing: on a packet that moves
// characters the two kinds of character can differ, and nothing but seeing it says which.
public class FakeClientTests
{
    private const float Far = 10f;
    private const float Moved = 1f;
    private const int Frames = 180;
    private const uint AnyNpc = 8725u;
    private const ushort ThinIce = 911;
    private const ushort ThinIceSlide = 320;

    private FakeGame game = null!;

    [SetUp]
    public void InstallGame() => game = FakeGame.Install();

    // Obeyed: true or false as seen in game; null when nothing sends it there, so nothing has shown
    // what it does. A packet with a true nobody has seen is how a player comes to stand still in game
    // while every test passes.
    private sealed record Move(uint Category, Action<ActorControl, Vector3> Send, bool? OwnPlayer, bool? Others);

    private static readonly Move[] Moves =
    [
        new(0xDC, (control, to) => control.GimmickJump(to), null, null),
        new(0xDF, (control, to) => control.Slide(to, 0f), OwnPlayer: false, Others: true),
        new(0xE1, (control, _) => control.Push(0f, Far, 1f), OwnPlayer: true, Others: null),
        new(0xF1, (control, to) => control.CarryTo(to, 0f), OwnPlayer: true, Others: true),
    ];

    [Test]
    public void EveryMovePacketHasAnAnswerForWhatItDoes()
    {
        var movePackets = PacketRules.Controls.Where(r => r.Value.Delivery == Delivery.Movement).Select(r => r.Key);
        Assert.That(Moves.Select(m => m.Category), Is.EquivalentTo(movePackets));
    }

    // A peer's sim only reaches its own player with what the client there is known to act on.
    [Test]
    public void APeersSimOnlyMovesItsOwnPlayerWithWhatItObeys()
    {
        var sent = PacketRules.Controls.Where(r => r.Value is { Delivery: Delivery.Movement, OwnPlayer: true }).Select(r => r.Key);
        var obeyed = Moves.Where(m => m.OwnPlayer != false).Select(m => m.Category);
        Assert.That(sent, Is.SubsetOf(obeyed));
    }

    private static IEnumerable<TestCaseData> Characters()
        => from move in Moves
           from own in new[] { true, false }
           where (own ? move.OwnPlayer : move.Others) is not null
           select new TestCaseData(move, own).SetArgDisplayNames($"0x{move.Category:X}", own ? "own player" : "another character");

    [TestCaseSource(nameof(Characters))]
    public void TheFakeObeysAMovePacketAsTheClientDoes(object moveCase, bool own)
    {
        var move = (Move)moveCase;
        var charas = game.BattleCharas;
        var proxy = own ? charas.LocalPlayer : charas.SpawnBattleNpcFromPacket(new EnemySpawnConfig(AnyNpc), new Placement(), out _)!;
        var start = proxy.Position;

        move.Send(new ActorControl(() => proxy), start + new Vector3(0, 0, Far));
        for (var i = 0; i < Frames; i++) charas.Tick(1f / 60f);

        Assert.That(Vector3.Distance(proxy.Position, start) > Moved, Is.EqualTo(own ? move.OwnPlayer : move.Others));
    }

    [Test]
    public void TheClientDropsAnActorMoveForItsOwnPlayer()
    {
        var charas = game.BattleCharas;
        var start = charas.Player.Position;
        charas.LocalPlayer.Move(start + new Vector3(0, 0, Far), 0f, default, 6f);
        for (var i = 0; i < Frames; i++) charas.Tick(1f / 60f);
        Assert.That(charas.Player.Position, Is.EqualTo(start));
    }

    // Press to stop, measured on real clients: 21.9y along 1.06 s after the press, and done inside
    // 1.7 s. Mid-slide they cover 26-28 y/s, behind about 0.25 s of wind-up; the two windows take
    // in what the sampling can't tell apart. A strat times its slides to this.
    [TestCase(1.06f, 19f, 24f)]
    [TestCase(1.4f, 26f, 32f)]
    [TestCase(1.7f, 31.9f, 32.1f)]
    public void AThinIceSlideRunsAtTheClientsSpeed(float seconds, float least, float most)
    {
        game.BattleCharas.Player.Statuses[(ThinIce, 0)] = new StatusSlot(ThinIce, ThinIceSlide, 30f, 0);
        var start = game.BattleCharas.Player.Position;

        game.PlayerInput.Steer(new Vector2(0f, 1f), 0f);
        for (var t = 0f; t < seconds; t += 1f / 60f)
        {
            game.Frame(1f / 60f, _ => { });
            game.PlayerInput.Steer(null, 0f);
        }

        Assert.That(Vector3.Distance(game.BattleCharas.Player.Position, start), Is.InRange(least, most));
    }

    // UNVERIFIED: what the client does with a key still held as a slide stops. The fake takes the
    // reading that fails a tap held too long.
    [Test]
    public void AKeyHeldOnThinIceSlidesAgainOnceTheSlideStops()
    {
        game.BattleCharas.Player.Statuses[(ThinIce, 0)] = new StatusSlot(ThinIce, ThinIceSlide, 30f, 0);
        var start = game.BattleCharas.Player.Position;

        game.PlayerInput.Steer(new Vector2(0f, 1f), 0f);
        for (var t = 0f; t < 2f; t += 1f / 60f) game.Frame(1f / 60f, _ => { });

        Assert.That(Vector3.Distance(game.BattleCharas.Player.Position, start), Is.GreaterThan(32.5f));
    }
}
