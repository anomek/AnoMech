using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top;
using AnoMech.Scenarios.Top.P2PartySynergy;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Top.TopConstants;
using static AnoMech.Tests.NegativeRun;

namespace AnoMech.Tests;

// The player is always RegenHealer, first in conga, so on the left (west, x < 0) of their pair.
// Lineup: pairs (left, right) take consecutive symbols starting at the player's, so the left group
// is RegenHealer, CasterDps, MeleeDpsA, MainTank and OffTank is the player's tether partner.
// Stacks are CasterDps (left) and MeleeDpsB (right). Both new norths pinned to N, so coordinates
// below are the AI's own: mid glitch spreads at x = ±11, far at ±18 for Square/Circle, rows
// y = -16 / -5.5 / 5.5 / 16 for Cross / Square / Circle / Triangle.
public class TopP2PartySynergyScenarioTests
{
    private static readonly (PartyRole Left, PartyRole Right)[] Pairs =
        [(RegenHealer, OffTank), (CasterDps, MeleeDpsB), (MeleeDpsA, PhysRangedDps), (MainTank, ShieldHealer)];

    // Omega-F's clone at (0, -10), Omega-M's at (0, 10), both facing the center.
    private static readonly Direction FacingNorthSouth = new(0f);

    private static NegativeRun<TopP2PartySynergyScenario> Healer(
        PlaystationSymbol symbol, GlitchType glitch, OmegaAttack? f = null, OmegaAttack? m = null)
        => Negative<TopP2PartySynergyScenario>(RegenHealer)
            .Overrides<TopP2PartySynergyStateOverrides>(o =>
            {
                o.NewNorthA = Direction.N;
                o.NewNorthB = Direction.N;
                o.Glitch = glitch;
                o.AttackF = f;
                o.AttackM = m;
                if (f is not null || m is not null) o.AttackDir = FacingNorthSouth;
                for (var k = 0; k < Pairs.Length; k++)
                {
                    var pairSymbol = (PlaystationSymbol)(((int)symbol + k) % 4);
                    o.Symbol[Pairs[k].Left] = pairSymbol;
                    o.Symbol[Pairs[k].Right] = pairSymbol;
                }
                o.Stack[CasterDps] = true;
                o.Stack[MeleeDpsB] = true;
            });

    [Test]
    public void DiesToLegs()
        => Healer(PlaystationSymbol.Cross, GlitchType.Mid, f: OmegaAttack.Legs, m: OmegaAttack.Sword)
            .TeleportAt(12f, to: new(8, -5))
            .ShouldKill(ActionId.SuperliminalSteelOmenL, RegenHealer);

    [Test]
    public void DiesToStaff()
        => Healer(PlaystationSymbol.Cross, GlitchType.Mid, f: OmegaAttack.Staff, m: OmegaAttack.Sword)
            .TeleportAt(12f, to: new(0, -3))
            .ShouldKill(ActionId.OptimizedBlizzardIII, RegenHealer);

    [Test]
    public void DiesToSword()
        => Healer(PlaystationSymbol.Cross, GlitchType.Mid, f: OmegaAttack.Legs, m: OmegaAttack.Sword)
            .TeleportAt(12f, to: new(0, 6))
            .ShouldKill(ActionId.EfficientBladework, RegenHealer);

    [Test]
    public void DiesToShield()
        => Healer(PlaystationSymbol.Cross, GlitchType.Mid, f: OmegaAttack.Legs, m: OmegaAttack.Shield)
            .TeleportAt(12f, to: new(0, -8))
            .ShouldKill(ActionId.BeyondStrength, RegenHealer);

    // These two stand just west of the Optical Laser's band (x = ±8): its damage lands before Fire III's.
    [Test]
    public void DiesWhenTooCloseOnMidTether()
        => Healer(PlaystationSymbol.Circle, GlitchType.Mid)
            .TeleportAt(17f, to: new(-8.5f, 5.5f))
            .ShouldKill(ActionId.OptimizedFireIII, RegenHealer, OffTank);

    [Test]
    public void DiesWhenTooCloseOnFarTether()
        => Healer(PlaystationSymbol.Circle, GlitchType.Far)
            .TeleportAt(17f, to: new(-8.5f, 5.5f))
            .ShouldKill(ActionId.OptimizedFireIII, RegenHealer, OffTank);

    [Test]
    public void DiesWhenTooFarOnMidTether()
        => Healer(PlaystationSymbol.Square, GlitchType.Mid)
            .TeleportAt(17f, to: new(-19, 0))
            .ShouldKill(ActionId.OptimizedFireIII, RegenHealer, OffTank);

    // Far tether keeps the tether long enough with the player stepped in from x = -11 to -6.
    [Test]
    public void DiesToOpticalLaser()
        => Healer(PlaystationSymbol.Cross, GlitchType.Far)
            .TeleportAt(19f, to: new(-6, -16))
            .ShouldKill(ActionId.OpticalLaser, RegenHealer);

    // MainTank holds the left Square, 5.8y away: inside each other's Fire III.
    [Test]
    public void DiesWhenAoesOverlap()
        => Healer(PlaystationSymbol.Circle, GlitchType.Mid)
            .TeleportAt(17f, to: new(-9, 0))
            .ShouldKill(ActionId.OptimizedFireIII, RegenHealer, MainTank);

    [Test]
    public void DiesKnockedBackIntoWall()
        => Healer(PlaystationSymbol.Circle, GlitchType.Mid)
            .TeleportAt(27f, to: new(-8, 0))
            .ShouldKill(ArenaWall, RegenHealer);

    // Leaves after Efficient Bladework, so only Spotlight is left to resolve.
    [Test]
    public void StackOfThreeDies()
        => Healer(PlaystationSymbol.Circle, GlitchType.Far)
            .TeleportAt(33.1f, to: new(-16, 9))
            .ShouldKill(ActionId.Spotlight, CasterDps, MeleeDpsA, MainTank);

    [Test]
    public void DiesInStackWhenTooCloseOnFarTether()
        => Healer(PlaystationSymbol.Circle, GlitchType.Far)
            .TeleportAt(33.1f, to: new(-14.5f, 0))
            .ShouldKill(ActionId.Spotlight, RegenHealer, OffTank);

    // Mid stacks sit west (-15, 0) and south (0, 15); 5.5y further from the partner's, still in our own.
    [Test]
    public void DiesInStackWhenTooFarOnMidTether()
        => Healer(PlaystationSymbol.Circle, GlitchType.Mid)
            .TeleportAt(33.1f, to: new(-18.9f, -3.9f))
            .ShouldKill(ActionId.Spotlight, RegenHealer, OffTank);

    // The four Omega-M clones stand on the diagonals, 13y out; the NW one's reaches into the west stack.
    [Test]
    public void DiesToSwordDuringStack()
        => Healer(PlaystationSymbol.Circle, GlitchType.Mid)
            .TeleportAt(31f, to: new(-13, -3))
            .ShouldKill(ActionId.EfficientBladework, RegenHealer);
}
