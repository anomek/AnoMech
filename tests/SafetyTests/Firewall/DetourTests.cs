using System.Linq;
using AnoMech.SafetyTests.Harness;
using Xunit;

namespace AnoMech.SafetyTests.Firewall;

// The detours themselves, called the way the game calls the hooked functions, over every opcode.
public class DetourTests : FirewallTestBase
{
    [Fact]
    public void SendDetour_PassesOnlyTheHeartbeat_OverEveryOpcode()
    {
        Enter();
        var passed = Enumerable.Range(0, 0x10000).Where(op => Game.SendDetourPasses((ushort)op)).ToList();
        Assert.Equal(new[] { (int)VirtualGame.HeartbeatOpcode }, passed);
        AssertClean();
    }

    [Fact]
    public void SendDetour_NullPacket_IsHeldAndReportsSuccess()
    {
        Enter();
        Assert.Equal(1, Game.SendDetourWithNullPacket());
        AssertClean();
    }

    [Fact]
    public void ReceiveDetour_PassesOnlyTheAllowlist_OverEveryOpcode()
    {
        Game.Config.ZoneDownOpcodes = [0x0077, 0x0101, 0xFFFF];
        Enter();
        var passed = Enumerable.Range(0, 0x10000).Where(op => Game.ReceiveDetourPasses((ushort)op)).ToList();
        Assert.Equal(new[] { 0x0077, 0x0101, 0xFFFF }, passed);
        AssertClean();
    }

    [Fact]
    public void ReceiveDetour_EmptyAllowlist_PassesNothing()
    {
        Game.Config.ZoneDownOpcodes = [];
        Enter();
        Assert.DoesNotContain(Enumerable.Range(0, 0x10000), op => Game.ReceiveDetourPasses((ushort)op));
        AssertClean();
    }

    // Release builds ignore the config switch: safe mode is compiled in.
    [Fact]
    public void ReceiveDetour_SafeModeSwitchedOff_StillFilters()
    {
        Game.Config.SafeMode = false;
        Game.Config.ZoneDownOpcodes = [0x0077];
        Enter();
        Assert.False(Game.ReceiveDetourPasses(VirtualGame.InitZoneInboundOpcode));
        AssertClean();
    }

    [Fact]
    public void ReceiveDetour_NullPacket_DeliversNothing()
    {
        Enter();
        var before = Game.InboundDelivered;
        Game.ReceiveDetourWithNullPacket();
        Assert.Equal(before, Game.InboundDelivered);
        AssertClean();
    }

    [Fact]
    public void ServerZoneChangeDuringTheStay_NeverReachesTheClient()
    {
        Enter();
        Game.ServerSends(VirtualGame.InitZoneInboundOpcode, () => Assert.Fail("A held zone change was delivered."), "InitZone");
        Game.RunFor(1);
        AssertClean();
    }

    [Fact]
    public void TheHeartbeatOpcodeIsTheOneReadFromTheGame()
    {
        Enter();
        Game.RunFor(VirtualGame.HeartbeatIntervalSeconds * 3);
        Assert.Contains(Game.ServerReceived, p => p.Opcode == VirtualGame.HeartbeatOpcode);
        AssertClean();
    }
}
