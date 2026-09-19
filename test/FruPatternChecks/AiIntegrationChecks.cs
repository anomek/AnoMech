using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Fru;
using AnoMech.Scenarios.Fru.FulgentBlade;

internal static class AiIntegrationChecks
{
    public static void Run()
    {
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            var presets = PartyPresets.ForRole(role, levelOverride: FruConstants.Level);
            Check(presets[(int)role] == null, "Level overrides must preserve the player's slot");
            Check(presets.Count(p => p?.Level == 100) == 7, "Every FRU bot must spawn at level 100");
            Check(PartyPresets.ForRole(role).Where(p => p != null).All(p => p!.Level == 90),
                "A FRU run must not mutate the shared default presets");
            Check(PartyPresets.ForRole(role, levelOverride: 70).Where(p => p != null).All(p => p!.Level == 70),
                "Restarting in a lower-level duty must use that duty's level");
        }
        foreach (var job in new uint[] { 19, 21, 24, 28, 22, 20, 23, 25 })
            Check(PartyPresets.ForPlayerJob(job, levelOverride: 100).Count(p => p?.Level == 100) == 7,
                "Automatic job-based role selection must also apply the bot level override");
        Console.WriteLine("PASS: FRU bot level 100 for all player roles/jobs; defaults preserved; lower-level duty restart.");

        var ai = new FulgentBladeAi();
        var pattern = new FulgentBladePattern(0, 0, true);
        var plan = new FulgentBladePartyPlan(pattern);
        var boss = new SimEnemy { Position = Vector3.Zero, Rotation = 0f };
        for (var playerRole = 0; playerRole < 8; playerRole++)
        {
            var player = new SimPlayer { Role = (PartyRole)playerRole };
            var world = new SimWorld();
            for (var role = 0; role < 8; role++)
                world.Party.Members.Add(role == playerRole ? player : new SimPartyNpc { Role = (PartyRole)role });
            ai.Run(new(pattern, () => boss), world);
            var times = new[] { FulgentBladePartyPlan.PrepositionTime }
                .Concat(Enumerable.Range(0, 6).Select(FulgentBladePartyPlan.DodgeTime))
                .Append(FulgentBladePartyPlan.StackTime).ToArray();
            var elapsed = 0f;
            foreach (var time in times)
            {
                world.Events.Tick(time - elapsed);
                elapsed = time;
            }
            Check(player.Moves.Count == 0, "The AI must never move the player in any role, including MT");
            foreach (var bot in world.Party.Members.OfType<SimPartyNpc>())
            {
                var tank = bot.Role == PartyRole.MainTank;
                var firstDodge = tank ? 2 : 1;
                Check(bot.Moves.Count == (tank ? 9 : 8), "Only the MT gets an additional cardinal setup move");
                if (tank) Check(bot.Moves[0].Target == plan.MainTankSetup, "MT must prepare on the dodge pocket's cardinal side");
                Check(bot.Moves[firstDodge - 1].Target == plan.Preposition, "Preposition must use this run's pattern");
                for (var step = 0; step < 6; step++)
                    Check(bot.Moves[step + firstDodge].Target == (tank ? plan.MainTankDodge(step, boss.Position) : plan.Dodge(step)),
                        "Only the MT may adjust the pre-cast dodge for cardinal facing");
                var stacks = plan.Stacks(boss.Position, boss.Rotation);
                Check(bot.Moves[^1].Target == (FulgentBladePartyPlan.UsesLeftStack((int)bot.Role) ? stacks.Left : stacks.Right),
                    "AI must assign each role to its correct light party");
                Check(bot.Moves.All(move => move.Speed == FulgentBladePartyPlan.RunSpeed), "AI must use tested run speed");
            }
        }

        var stopped = new SimWorld();
        var living = new SimPartyNpc();
        var dead = new SimPartyNpc { Dead = true };
        stopped.Party.Members.AddRange([living, dead]);
        ai.Run(new(pattern, () => boss), stopped);
        stopped.Events.Tick(FulgentBladePartyPlan.PrepositionTime);
        Check(living.Moves.Count == 2 && dead.Moves.Count == 0, "Dead bots must be skipped, including tank setup");
        stopped.Events.Clear();
        stopped.Events.Tick(50f);
        Check(living.Moves.Count == 2, "Reset must clear all queued bot moves, including tank alignment");

        foreach (var absent in new SimEnemy?[] { null, new() { IsActive = false } })
        {
            var missingBoss = new SimWorld();
            var tank = new SimPartyNpc { Role = PartyRole.MainTank };
            missingBoss.Party.Members.Add(tank);
            ai.Run(new(pattern, () => absent), missingBoss);
            missingBoss.Events.Tick(50f);
            Check(tank.Moves.Count == 7, "Missing/inactive boss skips setup and stacks but preserves safe dodges");
            Check(tank.Moves[4].Target == plan.Dodge(3), "Missing boss must fall back to the ordinary dodge");
        }

        var solo = new SimWorld();
        var soloPlayer = new SimPlayer();
        solo.Party.Members.Add(soloPlayer);
        ai.Run(new(pattern, () => null), solo);
        solo.Events.Tick(50f);
        Check(soloPlayer.Moves.Count == 0, "Solo mode / missing boss must not move the player or throw");
        Console.WriteLine("PASS: production AI scheduling; cardinal MT setup/alignment; all eight player roles excluded; role stacks; dead bots; reset; solo/missing boss.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

// Minimal engine stand-ins for executing the production AI and EventScheduler.
// These record movement requests; the geometric tests separately simulate travel.
// They do not pretend to test the native client or the real SimWorld lifecycle.
namespace AnoMech.Core.SimObjects
{
    public partial class SimCharacter
    {
        public bool IsActive { get; set; } = true;
        public bool Dead { get; set; }
        public PartyRole Role { get; set; }
        public Vector3 Position { get; set; }
        public float Rotation { get; set; }
        public List<(Vector3 Target, float Speed)> Moves { get; } = [];
        public void MoveTo(Vector3 target, float speed) => Moves.Add((target, speed));
    }
    public sealed partial class SimPlayer : SimCharacter, ISimPartyMember { }
    public sealed class SimPartyNpc : SimCharacter, ISimPartyMember
    {
        public byte Level { get; set; }
    }
    public sealed partial class SimEnemy : SimCharacter
    {
        public SimCharacter? Target { get; private set; }
        public bool Following { get; private set; }
        public void SetTarget(SimCharacter? target, bool follow = true)
        {
            Target = target;
            Following = follow;
        }
    }
    public sealed class SimParty
    {
        public PartyFinder Find => new(this);
        public void WipeAllPlayers(string cause) {
            foreach (var m in Members) { m.Damage.Add((0, cause, true)); m.Dead = true; }
        }
        public List<SimCharacter> Members { get; } = [];
        public SimPlayer? Player => Members.OfType<SimPlayer>().SingleOrDefault();
        public PartyRole PlayerRole => Player?.Role ?? PartyRole.MainTank;
        public SimCharacter? Get(PartyRole role) => Members.FirstOrDefault(member => member.Role == role);
        public IEnumerable<SimCharacter> ActiveMembers() => Members.Where(member => !member.Dead);
    }
    public sealed class PartyFinder(SimParty party)
    {
        public List<SimCharacter> InsideCircle(Vector3 center, float radius)
            => party.ActiveMembers().Where(m => Vector3.Distance(m.Position, center) <= radius).ToList();
        public List<SimCharacter> InsideRect(Placement p, float halfWidth, float length)
        {
            var forward = new Vector3(MathF.Sin(p.Rotation), 0, MathF.Cos(p.Rotation));
            var right = new Vector3(forward.Z, 0, -forward.X);
            return party.ActiveMembers().Where(m => {
                var d = m.Position - p.Position; var z = Vector3.Dot(d, forward);
                return z >= 0 && z <= length && MathF.Abs(Vector3.Dot(d, right)) <= halfWidth;
            }).ToList();
        }
    }
    public sealed partial class SimWorld
    {
        public EventScheduler Events { get; } = new();
        public SimParty Party { get; } = new();
        public int FillRequests { get; private set; }
        public void FillMissingPartyMembers(Func<PartyRole, Placement> placement, byte? levelOverride = null)
        {
            FillRequests++;
            var presets = PartyPresets.ForRole(Party.PlayerRole, levelOverride);
            for (var i = 0; i < presets.Count; i++)
            {
                var role = (PartyRole)i;
                if (presets[i] is not { } preset || Party.Get(role) != null) continue;
                var point = placement(role);
                Party.Members.Add(new SimPartyNpc { Role = role, Level = preset.Level,
                    Position = point.Position, Rotation = point.Rotation });
            }
        }
    }
}
