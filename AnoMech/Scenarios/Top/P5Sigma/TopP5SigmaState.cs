using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Top.P5Sigma
{
    public sealed record Rotation(float Mul, uint LockonId)
    {
        public static readonly Rotation Clockwise = new(-1, TopConstants.LockonId.RotateCw);
        public static readonly Rotation CounterClockwise = new(1, TopConstants.LockonId.RotateCcw);
    }

    public sealed class TopP5SigmaState
    {
        private readonly Rng rng = Rng.Detached;

        public RoleList Order { get; }
        public RoleList WaveCannonTargets { get; }
        public RoleList DynamisTargets { get; }


        public GlitchType GlitchType { get; }


        public Direction NewNorthA { get; }

        public Direction AdjustedNorthA => TowerNorthFlipped ? NewNorthA.Flip() : NewNorthA;

        public Direction NewNorthB { get; }
        public bool TowerNorthFlipped { get; }
        public Rotation SpinnerRotation { get; }
        public OmegaAttack OmegaFAttack { get; }

        public RoleList HelloWorldTargets { get; }

        // Resolved once here (not live inside TopP5SigmaAi.Run) -- see RoleList.Random(Rng, ...)'s
        // doc comment for why a live pick there would let a peer's own bot-controlled replay
        // choose different hand-bait targets than the host's bots did.
        public RoleList HandBait { get; }

        // The other four, in the order TopP5SigmaAi marks and places them; resolved here for the
        // same reason as HandBait.
        public RoleList HelloWorldJumpOrder { get; }

        public readonly Tower?[] Towers;

        public int FirstMissing;
        public int SecondMissing;

        public TopP5SigmaState(Rng rng, SimParty party, TopP5SigmaStateOverrides overrides)
        {
            this.rng = rng;
            var order = RoleList.Random(rng, party);
            Order = overrides.Order is { } pinnedOrder ? new RoleList(party, pinnedOrder) : order;
            DynamisTargets = new RoleListBuilder
            {
                Size = 6,
                Membership = overrides.ResolveDynamis(party.PlayerRole),
            }.Build(rng, party);
            WaveCannonTargets = SelectWaveCannonTargets(Order, overrides.WaveCannonSkips);

            NewNorthA = overrides.NewNorthA ?? rng.NextIntercardinal();
            GlitchType = overrides.CloseFarTether ?? rng.NextObj(GlitchType.Mid, GlitchType.Far);
            TowerNorthFlipped = overrides.TowerNorthFlip ?? rng.NextBool();
            NewNorthB = overrides.NewNorthB ?? rng.NextDirection();
            SpinnerRotation = overrides.SpinnerRotation ?? rng.NextObj(Rotation.Clockwise, Rotation.CounterClockwise);
            OmegaFAttack = overrides.OmegaFForm ?? rng.NextObj(OmegaAttack.Legs, OmegaAttack.Staff);

            var (helloSlots, helloMembership) = overrides.ResolveHelloWorld(party.PlayerRole);
            HelloWorldTargets = new RoleListBuilder
            {
                Size = 2,
                Slots = helloSlots,
                Membership = helloMembership,
            }.Build(rng, party);

            var handBait = DynamisTargets.Random(rng, 2, HelloWorldTargets.List);
            HandBait = overrides.HandBait is { } pinnedHandBait ? new RoleList(party, pinnedHandBait) : handBait;
            // "1" goes north with the hand baits, so it is a Dynamis holder too.
            var helloWorldOrHandBait = HelloWorldTargets.List.Concat(HandBait.List).ToArray();
            var first = DynamisTargets.Random(rng, 1, helloWorldOrHandBait);
            var rest = new RoleList(party, Enum.GetValues<PartyRole>())
                .Random(rng, 3, helloWorldOrHandBait.Concat(first.List).ToArray());
            var jumpOrder = new RoleList(party, first.List.Concat(rest.List).ToList());
            HelloWorldJumpOrder = overrides.HelloWorldJumpOrder is { } pinnedJumpOrder
                ? new RoleList(party, pinnedJumpOrder)
                : jumpOrder;

            Towers = (GlitchType == GlitchType.Mid ? MidGlitchTowers : FarGlitchTowers)
                     .Select(t => t == null ? t : t with { Position = AdjustedNorthA.Apply(t.Position) })
                     .ToArray();
        }

        // Network-replay constructor: reconstructs the fields TopP5SigmaAi reads.
        // WaveCannonTargets/Towers are harmless placeholders -- only the scenario's own
        // host-only resolution reads them. GlitchType/OmegaAttack/Rotation aren't
        // JSON-serializable in a reconstructible way, so which named static instance was
        // chosen is carried as a bool.
        private TopP5SigmaState(
            SimParty party, PartyRole[] order, PartyRole[] dynamisTargets, PartyRole[] helloWorldTargets,
            PartyRole[] handBait, PartyRole[] helloWorldJumpOrder, float newNorthARadians, float newNorthBRadians,
            bool towerNorthFlipped, bool glitchIsFar, bool spinnerIsClockwise, bool omegaFIsStaff, int firstMissing,
            int secondMissing)
        {
            Order = new RoleList(party, order);
            WaveCannonTargets = RoleList.Empty();
            DynamisTargets = new RoleList(party, dynamisTargets);
            GlitchType = glitchIsFar ? GlitchType.Far : GlitchType.Mid;
            NewNorthA = new Direction(newNorthARadians);
            NewNorthB = new Direction(newNorthBRadians);
            TowerNorthFlipped = towerNorthFlipped;
            SpinnerRotation = spinnerIsClockwise ? Rotation.Clockwise : Rotation.CounterClockwise;
            OmegaFAttack = omegaFIsStaff ? OmegaAttack.Staff : OmegaAttack.Legs;
            HelloWorldTargets = new RoleList(party, helloWorldTargets);
            HandBait = new RoleList(party, handBait);
            HelloWorldJumpOrder = new RoleList(party, helloWorldJumpOrder);
            Towers = [];
            FirstMissing = firstMissing;
            SecondMissing = secondMissing;
        }

        public static TopP5SigmaState FromNetworkReplay(
            SimParty party, PartyRole[] order, PartyRole[] dynamisTargets, PartyRole[] helloWorldTargets,
            PartyRole[] handBait, PartyRole[] helloWorldJumpOrder, float newNorthARadians, float newNorthBRadians,
            bool towerNorthFlipped, bool glitchIsFar, bool spinnerIsClockwise, bool omegaFIsStaff, int firstMissing,
            int secondMissing)
            => new(party, order, dynamisTargets, helloWorldTargets, handBait, helloWorldJumpOrder, newNorthARadians,
                   newNorthBRadians, towerNorthFlipped, glitchIsFar, spinnerIsClockwise, omegaFIsStaff, firstMissing,
                   secondMissing);


        // MidGlitch: 6 towers on the 22.5°-offset inner ring at radius 17, rotated so the two
        // adjacent SOLOs frame compass N (bossmod-canonical: relNorth → N).
        // N half holds only the two SOLOs; S half going E→W is PAIR, SOLO, SOLO, PAIR.
        // 15.706 = 17·cos 22.5°, 6.506 = 17·sin 22.5°.
        private static readonly Tower?[] MidGlitchTowers =
        {
            new(new Vector3(+15.706f, 0f, +6.506f), MinPlayers: 2), // PAIR ESE (112.5°)
            new(new Vector3(-15.706f, 0f, +6.506f), MinPlayers: 2), // PAIR WSW (247.5°)
            new(new Vector3(+6.506f, 0f, -15.706f), MinPlayers: 1), // SOLO NNE (22.5°)
            new(new Vector3(-6.506f, 0f, -15.706f), MinPlayers: 1), // SOLO NNW (337.5°)
            new(new Vector3(+6.506f, 0f, +15.706f), MinPlayers: 1), // SOLO SSE (157.5°)
            new(new Vector3(-6.506f, 0f, +15.706f), MinPlayers: 1), // SOLO SSW (202.5°)
        };

        // FarGlitch: 5 towers on the 45° grid at radius 17, rotated so the apex pair-tower is at
        // compass N: pairs at rel N, SE and SW, solos at rel E and W.
        // 12.021 = 17/√2.
        private static readonly Tower?[] FarGlitchTowers =
        {
            new(new Vector3(0f, 0f, -17f), MinPlayers: 2),           // PAIR apex N (0°)
            new(new Vector3(+12.021f, 0f, +12.021f), MinPlayers: 2), // PAIR base SE (135°)
            new(new Vector3(-12.021f, 0f, +12.021f), MinPlayers: 2), // PAIR base SW (225°)
            new(new Vector3(+17f, 0f, 0f), MinPlayers: 1),           // SOLO E (90°)
            new(new Vector3(-17f, 0f, 0f), MinPlayers: 1),           // SOLO W (270°)
            null
        };

        private RoleList SelectWaveCannonTargets(RoleList tethers, (int First, int Second)? pinned)
        {
            var skip1 = rng.NextInt(8);
            var skip2 = rng.NextInt(6);
            if (skip2 >= skip1 / 2 * 2) skip2 += 2;
            if (pinned is { } skips) (skip1, skip2) = skips;
            FirstMissing = skip1;
            SecondMissing = skip2;
            return RoleList.AllExcept(rng, tethers.Party, tethers[skip1], tethers[skip2]);
        }

        public (PartyRole left, PartyRole right) FullPair(int index)
        {
            int i;
            for (i = 0; i < 4; i++)
            {
                if (i == FirstMissing / 2 || i == SecondMissing / 2) continue;
                else if (index == 0) break;
                else index--;
            }

            return (Order[2 * i], Order[2 * i + 1]);
        }

        public (PartyRole baiting, PartyRole marked) HalfPair(int index)
        {
            var missing = (index == 0) ^ (FirstMissing > SecondMissing) ? FirstMissing : SecondMissing;
            var other = missing % 2 == 0 ? missing + 1 : missing - 1;
            return (Order[missing], Order[other]);
        }
    }
}
