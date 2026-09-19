using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.CrystallizeTime.CrystallizeTimeConstants;

namespace AnoMech.Scenarios.Fru.CrystallizeTime;

public sealed partial class FruCrystallizeTimeScenario : IScenario
{
    public string Name => "Crystallize Time";
    public float Duration => 67f;
    public IPhase Phase => FruZone.P4;
    public IReadOnlyList<IScenarioAi> AiStrats { get; } = [new CrystallizeTimeAi()];
    internal CrystallizeTimePlayerPattern SelectedPlayerPattern { get; set; }
    public void DrawSettings() => DrawPatternSettings();
    partial void DrawPatternSettings();
    private SimWorld world = null!;
    private CrystallizeTimePattern pattern = null!;
    private DamageSolver damage = null!;
    private SimEnemy? usurper, oracle, fragment;
    private readonly SimEnemy?[] hourglasses = new SimEnemy?[6];
    private readonly SimMapEffect?[] hourglassScenery = new SimMapEffect?[6];
    private readonly SimEnemy?[] dragons = new SimEnemy?[2];
    private readonly int[] dragonHits = new int[2];
    private readonly float[] dragonCooldown = new float[2];
    private readonly PartyRole?[] dragonPreviousInterceptors = new PartyRole?[2];
    private readonly List<ISimObject> owned = [];
    private readonly List<SimTether> tethers = [];
    private readonly Dictionary<CrystallizeTimeAssignment, (Vector3 Position, SimEventObject? Object, float Created, PartyRole Creator)> puddles = [];
    private readonly Dictionary<PartyRole, CrystallizeTimeAssignment> puddleDestinations = [];
    private readonly Dictionary<PartyRole, Vector3> rewinds = [];
    private readonly List<SimOmen> rewindMarkers = [];
    private readonly Dictionary<PartyRole, Vector3> aeroSources = [];
    private readonly HashSet<PartyRole> wingVulns = [];
    private readonly Dictionary<(PartyRole Role, ushort Status), float> statusDeadlines = [];
    private Vector3 jumpPosition, jumpStart;
    private bool jumping, jumpPending;
    private bool running;

    public void Run(SimWorld worldParam, int? selectedAi)
        => Run(worldParam, CrystallizeTimePattern.Randomize(Random.Shared, false,
            worldParam.Party.Player is null ? null : worldParam.Party.PlayerRole, SelectedPlayerPattern));

    internal void Run(SimWorld worldParam, CrystallizeTimePattern selectedPattern)
    {
        world = worldParam;
        pattern = selectedPattern;
        damage = new(world.Party);
        owned.Clear(); tethers.Clear(); puddles.Clear(); puddleDestinations.Clear();
        rewinds.Clear(); rewindMarkers.Clear(); aeroSources.Clear(); wingVulns.Clear(); statusDeadlines.Clear();
        Array.Clear(hourglasses); Array.Clear(hourglassScenery); Array.Clear(dragons); Array.Clear(dragonHits); Array.Clear(dragonCooldown);
        Array.Clear(dragonPreviousInterceptors);
        usurper = oracle = fragment = null;
        jumping = jumpPending = false;
        spreadDestinations.Clear();
        running = true;
        At(0.5f, SpawnBosses);
        At(1.1f, () => Own(world.SpawnMapEffect(FragmentSlot, FruConstants.MapEffect.Show, FruConstants.MapEffect.Hide)));
        At(1, () => { usurper?.Cast(CrystallizeUsurper, castSeconds: 4.5f); oracle?.Cast(CrystallizeOracle, castSeconds: 4.5f); });
        At(6.5f, AssignDebuffs);
        At(10f, SpawnTethers);
        At(DragonSpawnTime, SpawnDragons);
        At(15.3f, () => {
            foreach (var hg in pattern.HourglassPair(0)) Effect(Quicken, CrystallizeTimePattern.Hourglass(hg), hourglasses[hg]);
            foreach (var hg in pattern.HourglassPair(2)) Effect(Slow, CrystallizeTimePattern.Hourglass(hg), hourglasses[hg]);
        });
        At(16.3f, () => { foreach (var tether in tethers) tether.Despawn(); });
        float[] hourglassHits = [17.7f, 23f, 28.2f];
        for (var wave = 0; wave < 3; wave++)
        {
            var index = wave;
            At(hourglassHits[wave] - 1.4f, () => {
                foreach (var hg in pattern.HourglassPair(index)) hourglasses[hg]?.Cast(Maelstrom, castSeconds: 1.4f);
            });
            At(hourglassHits[wave], () => {
                foreach (var hg in pattern.HourglassPair(index)) Circle(CrystallizeTimePattern.Hourglass(hg), 12, Maelstrom, "hourglass");
            });
        }
        foreach (var (time, route) in pattern.Routes()) At(time, () => Move(route));
        At(18.7f, () => Stack(CrystallizeTimeAssignment.Water, Water, 4));
        At(20.6f, Fireworks);
        At(21.1f, () => {
            foreach (var (role, source) in aeroSources) ForceAway(world.Party.Get(role), source, 30, 30);
        });
        At(23.5f, () => { CheckCleanse(CrystallizeTimeAssignment.IceWest); CheckCleanse(CrystallizeTimeAssignment.IceEast); });
        At(23.6f, () => { Stack(CrystallizeTimeAssignment.Darkness, Darkness, 5); PositionUsurper(false); });
        ScheduleWaves(false, 24.3f);
        At(29.8f, () => PositionUsurper(true));
        ScheduleWaves(true, 30.6f);
        At(35.8f, () => MoveRoles(pattern.Rewind));
        At(37.5f, () => {
            foreach (var role in pattern.Quietus) {
                var member = world.Party.Get(role);
                if (member.IsAlive()) { Effect(Quietus, member!.Position); RaidHit(Quietus, "Quietus", false); }
                member?.RemoveStatus(QuietusStatus);
            }
        });
        At(ReturnSnapshotTime, SnapshotReturn);
        At(ReturnMarkerEndTime, () => { foreach (var marker in rewindMarkers) marker.Despawn(); });
        At(39.9f, () => oracle?.Cast(SpiritTaker, castSeconds: 2.7f));
        At(40.6f, StartSpiritSpread);
        At(41.4f, () => usurper?.SetPosition(Vector3.Zero));
        At(41.6f, () => usurper?.PlayActionTimeline(WingsWindupTimeline, WingsWindupLoop));
        // The native cast release teleports out before arrival at the first edge.
        At(42.7f, () => usurper?.Cast(WingsFirst, castSeconds: 4.8f));
        At(43.4f, () => {
            var target = world.Party.Get(pattern.JumpTarget);
            if (!target.IsAlive()) return;
            jumpPosition = target!.Position;
            jumpStart = oracle?.Position ?? jumpPosition;
            jumping = jumpPending = true;
            oracle?.Cast(SpiritHit, targetLocation: jumpPosition, targetId: target.GameObjectId, castSeconds: 0f);
        });
        At(44, () => { if (jumpPending) Circle(jumpPosition, 5, SpiritHit, "Spirit Taker spread", pattern.JumpTarget); });
        At(46.5f, () => {
            foreach (var assignment in Enum.GetValues<CrystallizeTimeAssignment>()) CheckCleanse(assignment);
        });
        At(46.6f, () => {
            foreach (var member in Members()) { member.RemoveStatus(ReturnStatus); Status(member, StunStatus, 55.5f); member.StopMoving(); }
            world.Party.Player?.SetMechanicInputLock(true);
        });
        At(48, () => {
            foreach (var (role, position) in rewinds) {
                var member = world.Party.Get(role);
                member?.RemoveStatus(ReturnStatus);
                if (!member.IsAlive()) continue;
                Effect(ReturnIV, position, member);
                Slide(member!, position, 0.5f);
            }
            PositionUsurper(false);
            usurper?.PlayActionTimeline(WingsShowTimeline, WingsShowLoop);
        });
        // Keep drawing the actor so native timelines own the teleport fades.
        // Each authored wing swing includes the body, VFX and sound.
        At(49.3f, () => usurper?.PlayActionTimeline(WingsSwingTimeline));
        At(49.9f, () => Wings(false));
        // Preserve the previously requested first-edge recovery before departure.
        At(53f, () => usurper?.PlayActionTimeline(WingsHideTimeline));
        At(53.1f, () => {
            PositionUsurper(true);
            usurper?.PlayActionTimeline(WingsShowTimeline, WingsShowLoop);
        });
        At(53.9f, () => usurper?.PlayActionTimeline(WingsSwingTimeline));
        At(54.5f, () => Wings(true));
        At(55.5f, () => {
            foreach (var member in Members()) member.RemoveStatus(StunStatus);
            world.Party.Player?.SetMechanicInputLock(false);
        });
        At(56.4f, () => MoveRoles(CrystallizeTimePattern.AkhMorn));
        At(56.8f, () => {
            foreach (var hourglass in hourglasses) hourglass?.Despawn();
            foreach (var scenery in hourglassScenery) scenery?.Despawn();
        });
        At(57.6f, () => {
            usurper?.SetPosition(new Vector3(-2.5f, 0, 0));
            oracle?.SetPosition(new Vector3(2.5f, 0, 0));
            oracle?.SetVisible(true);
            usurper?.SetTargetable(true);
            oracle?.SetTargetable(true);
        });
        At(58.4f, () => { usurper?.Cast(AkhMornUsurper, castSeconds: 3.5f); oracle?.Cast(AkhMornOracle, castSeconds: 3.5f); });
        At(60.6f, () => { foreach (var member in Members()) member.RemoveStatus(MagicVulnerability); });
        // AnoMech's existing simulated invulnerability supports the MT solo
        // soak. It does not press a job action on the user's behalf.
        At(61, () => {
            if (world.Party.Get(PartyRole.MainTank) is { } tank) Status(tank, SoloTankInvulnerability, 65.5f);
        });
        for (var hit = 0; hit < 4; hit++) At(61.9f + 1.1f * hit, AkhMorn);
        At(65.5f, () => world.Party.Get(PartyRole.MainTank)?.RemoveStatus(SoloTankInvulnerability));
        At(Duration, Finish);
    }

    private void At(float seconds, Action action) => world.Events.Add(seconds, () => { if (running) action(); });
    private T Own<T>(T actor) where T : ISimObject { owned.Add(actor); return actor; }
    private SimEnemy? Spawn(uint id, uint name, Vector3 position, bool targetable = false, float scale = 0,
        bool hostile = true, ushort spawnTimeline = 0, uint modelCharaId = 0)
    {
        var enemy = world.SpawnEnemy(new EnemySpawnConfig(id, NameId: name, Level: FruConstants.Level,
            Targetable: targetable, EnemyList: targetable && hostile ? EnemyListMode.Always : EnemyListMode.Never,
            Placement: new Placement(position, MathF.PI), Scale: scale, IsHostile: hostile,
            SpawnTimeline: spawnTimeline, ModelCharaId: modelCharaId));
        if (enemy != null) Own(enemy);
        return enemy;
    }
    private void SpawnBosses()
    {
        usurper = Spawn(Usurper, UsurperName, new(0, 0, 4.15f), true, modelCharaId: UsurperDragonModel);
        oracle = Spawn(Oracle, OracleName, Vector3.Zero, true);
        fragment = Spawn(Fragment, FragmentName, FragmentPosition, targetable: true, hostile: false);
        if (usurper == null || oracle == null || fragment == null) { Finish(); return; }
        fragment.SetHealth(FragmentMaxHealth, FragmentMaxHealth);
        // Native encounter actors suspended within the crystal, facing each other.
        // Grounded actor coordinates are separate from the persistent native
        // draw offset; raising Position.Y alone did not survive in game.
        var ryne = Spawn(VisionOfRyne, RyneName, FragmentPosition + new Vector3(-0.65f, 0, 0), hostile: false);
        var gaia = Spawn(VisionOfGaia, GaiaName, FragmentPosition + new Vector3(0.65f, 0, 0), hostile: false);
        ryne?.SetVisualHeight(FragmentVisionHeight);
        gaia?.SetVisualHeight(FragmentVisionHeight);
        ryne?.SetRotation(MathF.PI / 2);
        gaia?.SetRotation(-MathF.PI / 2);
        usurper.SetTarget(world.Party.Get(PartyRole.MainTank), follow: false);
        oracle.SetTarget(world.Party.Get(PartyRole.OffTank), follow: false);
    }
    private void AssignDebuffs()
    {
        foreach (var assignment in Enum.GetValues<CrystallizeTimeAssignment>())
        {
            var member = Member(assignment);
            if (!member.IsAlive()) continue;
            var claw = assignment is CrystallizeTimeAssignment.AeroWest or CrystallizeTimeAssignment.AeroEast or CrystallizeTimeAssignment.IceWest or CrystallizeTimeAssignment.IceEast;
            Status(member!, claw ? ClawStatus : FangStatus,
                assignment is CrystallizeTimeAssignment.IceWest or CrystallizeTimeAssignment.IceEast ? 23.5f : 46.5f);
            Status(member!, assignment switch {
                CrystallizeTimeAssignment.AeroWest or CrystallizeTimeAssignment.AeroEast => AeroStatus,
                CrystallizeTimeAssignment.Eruption => EruptionStatus, CrystallizeTimeAssignment.Water => WaterStatus,
                CrystallizeTimeAssignment.Darkness => DarknessStatus, _ => IceStatus },
                assignment == CrystallizeTimeAssignment.Water ? 18.7f : assignment == CrystallizeTimeAssignment.Darkness ? 23.6f : 20.6f);
            Status(member!, ReturnWaitingStatus, ReturnSnapshotTime);
            // The native clock lasts for the full waiting debuff, until the
            // position is saved; it is not just a final-seconds warning.
            member!.AddVfx(ReturnClockVfx);
        }
        foreach (var role in pattern.Quietus)
            if (world.Party.Get(role) is { } member) Status(member, QuietusStatus, 37.5f);
        for (var hg = 0; hg < 6; hg++)
        {
            hourglasses[hg] = Spawn(Hourglass, HourglassName, CrystallizeTimePattern.Hourglass(hg));
            // BNpc 17837 is an invisible action/tether anchor; the actual hourglass
            // is the native sgbg_n4gw_a2_gmc18 shared group in this map slot.
            hourglassScenery[hg] = Own(world.SpawnMapEffect((byte)(HourglassSlot + hg), FruConstants.MapEffect.Show, FruConstants.MapEffect.Hide));
        }
    }
    private void SpawnTethers()
    {
        oracle?.Cast(Speed, castSeconds: 5.3f);
        foreach (var hg in pattern.HourglassPair(0)) tethers.Add(world.Tether(hourglasses[hg], oracle, FastTether));
        foreach (var hg in pattern.HourglassPair(2)) tethers.Add(world.Tether(hourglasses[hg], oracle, SlowTether));
        oracle?.SetVisible(false);
        usurper?.SetTargetable(false);
        oracle?.SetTargetable(false);
    }
    private void SpawnDragons()
    {
        for (var side = 0; side < 2; side++)
        {
            dragons[side] = Spawn(Dragon, DragonName, DragonPosition(side, DragonMovementStart),
                scale: DragonInitialScale, spawnTimeline: DragonSpawnTimeline);
            var direction = DragonPosition(side, DragonMovementStart + 0.1f) - DragonPosition(side, DragonMovementStart);
            dragons[side]?.SetRotation(MathF.Atan2(direction.X, direction.Z));
            // Preserve two full seconds of protection after movement starts;
            // time spent in the appearance must not consume this cooldown.
            dragonCooldown[side] = DragonMovementStart + DragonHitCooldown;
        }
    }
    private SimCharacter? Member(CrystallizeTimeAssignment assignment) => world.Party.Get(pattern.Role(assignment));
    private void Status(SimCharacter member, ushort status, float deadline)
    {
        if (member is not ISimPartyMember slot || !member.IsAlive()) return;
        member.AddStatus(status);
        statusDeadlines[(slot.Role, status)] = deadline;
    }
    private SimCharacter[] Members() => world.Party.ActiveMembers().Where(m => m.IsAlive()).ToArray();
    private void Hit(SimCharacter member, uint action, string cause, bool lethal)
    { if (member.IsAlive()) damage.ApplyDamage(member, lethal ? 1 : 0.35f, action, cause, lethal); }
    private void RaidHit(uint action, string cause, bool lethal)
    { foreach (var member in Members()) Hit(member, action, cause, lethal); }
    private void Effect(uint action, Vector3 position, SimCharacter? target = null)
    {
        var helper = Spawn(FruConstants.BNpcBaseId.Helper, OracleName, position);
        helper?.Cast(action, targetLocation: position, castSeconds: 0, targetId: target?.GameObjectId);
        // Native effects have their authored lifetime; source actors are retained
        // through the scenario, including Return's stationary ground trace.
    }
    private void Circle(Vector3 position, float radius, uint action, string cause, PartyRole? exempt = null, float inner = 0)
    {
        foreach (var member in Members())
        {
            var distance = Vector3.Distance(member.Position, position);
            if (distance <= radius && distance >= inner)
                Hit(member, action, cause, member is not ISimPartyMember slot || slot.Role != exempt);
        }
    }
    private void CheckFragment(Vector3 position, float radius, uint action, float inner = 0)
    {
        var distance = Vector3.Distance(position, FragmentPosition);
        if (distance <= radius && distance >= inner)
        {
            fragment?.SetHealth(0, FragmentMaxHealth);
            RaidHit(action, "Fragment of Fate was hit", true);
        }
    }
    private void Stack(CrystallizeTimeAssignment assignment, uint action, int required)
    {
        var target = Member(assignment);
        if (!target.IsAlive()) { RaidHit(action, "missing stack target", true); return; }
        var position = target!.Position;
        var occupants = Members().Where(m => Vector3.Distance(m.Position, position) <= 6).ToArray();
        Effect(action, position, target);
        CheckFragment(position, 6, action);
        foreach (var member in occupants) Hit(member, action, $"{occupants.Length}/{required} players in stack", occupants.Length != required);
        target.RemoveStatus(assignment == CrystallizeTimeAssignment.Water ? WaterStatus : DarknessStatus);
    }
    private void Fireworks()
    {
        // Snapshot every source before applying damage or knockbacks.
        var sources = new[] { CrystallizeTimeAssignment.AeroWest, CrystallizeTimeAssignment.AeroEast, CrystallizeTimeAssignment.IceWest,
            CrystallizeTimeAssignment.IceEast, CrystallizeTimeAssignment.Ice, CrystallizeTimeAssignment.Eruption }
            .Select(a => (Assignment: a, Member: Member(a))).Where(x => x.Member.IsAlive())
            .Select(x => (x.Assignment, Member: x.Member!, Position: x.Member!.Position)).ToArray();
        var members = Members();
        foreach (var (assignment, member, position) in sources)
        {
            if (assignment is CrystallizeTimeAssignment.AeroWest or CrystallizeTimeAssignment.AeroEast)
            {
                Effect(Aero, position, member);
                CheckFragment(position, 15, Aero);
                foreach (var other in members)
                    if (other != member && Vector3.Distance(other.Position, position) <= 15 && other is ISimPartyMember slot)
                    {
                        if (!aeroSources.TryAdd(slot.Role, position)) Hit(other, Aero, "overlapping Aero", true);
                    }
                member.RemoveStatus(AeroStatus);
            }
            else if (assignment == CrystallizeTimeAssignment.Eruption)
            {
                Effect(Eruption, position, member);
                Circle(position, 6, Eruption, "Dark Eruption spread", pattern.Role(assignment));
                CheckFragment(position, 6, Eruption);
                member.RemoveStatus(EruptionStatus);
            }
            else
            {
                Effect(Ice, position, member);
                Circle(position, 12, Ice, "Dark Blizzard donut", inner: 3);
                CheckFragment(position, 12, Ice, 3);
                member.RemoveStatus(IceStatus);
            }
        }
    }
    private void ScheduleWaves(bool second, float start)
    {
        At(start, () => usurper?.Cast(Tidal, castSeconds: 2.7f));
        for (var step = 0; step < 4; step++)
        {
            var index = step;
            var hit = start + 2.7f + step * 2f;
            At(step == 0 ? start : hit - 1.9f, () => {
                var origin = pattern.WaveOrigin(second, index);
                var direction = pattern.WaveDirection(second);
                var helper = Spawn(FruConstants.BNpcBaseId.Helper, UsurperName, origin);
                helper?.SetRotation(MathF.Atan2(direction.X, direction.Z));
                // Action Omen 589 (exa_rz_o1v) and its own native hit timeline.
                helper?.Cast(index == 0 ? TidalFirst : TidalRest, castSeconds: index == 0 ? 2.7f : 1.9f);
            });
            At(hit, () => {
                foreach (var member in Members())
                    if (CrystallizeTimePattern.InWave(member.Position, pattern.WaveOrigin(second, index), pattern.WaveDirection(second)))
                        Hit(member, index == 0 ? TidalFirst : TidalRest, "Tidal Light", true);
            });
        }
    }
    private void PositionUsurper(bool second)
    {
        usurper?.SetPosition(pattern.WaveOrigin(second, 0));
        var direction = pattern.WaveDirection(second);
        usurper?.SetRotation(MathF.Atan2(direction.X, direction.Z));
    }
    private void SnapshotReturn()
    {
        foreach (var role in Enum.GetValues<PartyRole>()) world.Party.Get(role)?.RemoveVfx(ReturnClockVfx);
        foreach (var member in Members())
        {
            if (member is not ISimPartyMember slot) continue;
            rewinds[slot.Role] = member.Position;
            member.RemoveStatus(ReturnWaitingStatus);
            Status(member, ReturnStatus, 46.6f);
            // This AVFX has no actor binders. Spawn it at the exact saved
            // position with its authored scale, independent of subsequent
            // movement. SimOmen owns a native StaticVfx, not a drawn overlay.
            rewindMarkers.Add(Own(world.SpawnOmen(ReturnMarkerVfx, new(member.Position, 0), Vector3.One)));
            Effect(Return, member.Position, member);
        }
        oracle?.SetPosition(new Vector3(0, 0, 4.15f));
        oracle?.SetVisible(true);
    }
    private void Wings(bool second)
    {
        var origin = pattern.WaveOrigin(second, 0);
        var direction = pattern.WaveDirection(second);
        var targets = Members().OrderBy(m => Vector3.Dot(m.Position - origin, direction)).Take(4).ToArray();
        for (var i = 0; i < targets.Length; i++)
        {
            var member = targets[i];
            var role = ((ISimPartyMember)member).Role;
            Hit(member, WingsHit, i == 0 ? "Hallowed Wings tank lead" : "Hallowed Wings first four",
                (i == 0 && role is not (PartyRole.MainTank or PartyRole.OffTank)) || !wingVulns.Add(role));
            Status(member, MagicVulnerability, world.Events.Elapsed + 6);
        }
        foreach (var member in Members()) ForceAway(member, member.Position - direction, 20, 20 / 0.7f);
    }
    private void AkhMorn()
    {
        var tanks = new[] { world.Party.Get(PartyRole.MainTank), world.Party.Get(PartyRole.OffTank) };
        if (tanks.Any(t => !t.IsAlive())) { RaidHit(AkhHitUsurper, "missing Akh Morn tank", true); return; }
        for (var i = 0; i < 2; i++)
        {
            var action = i == 0 ? AkhHitUsurper : AkhHitOracle;
            var tank = tanks[i]!;
            var targets = Members().Where(m => Vector3.Distance(m.Position, tank.Position) <= 4).ToArray();
            Effect(action, tank.Position, tank);
            foreach (var member in targets) Hit(member, action, "Akh Morn 7-1 stack",
                targets.Length != (i == 0 ? 1 : 7) || Vector3.Distance(member.Position, tanks[1-i]!.Position) <= 4);
        }
    }
    private void Move(IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> route)
    {
        foreach (var (assignment, destination) in route)
        {
            var role = pattern.Role(assignment);
            puddleDestinations.Remove(role);
            if (world.Party.Get(role) is not SimPartyNpc bot || !bot.IsAlive()) continue;
            if (destination.Puddle is { } source)
            {
                puddleDestinations[role] = source;
                if (puddles.TryGetValue(source, out var puddle)) bot.MoveTo(puddle.Position, CrystallizeTimePattern.RunSpeed);
            }
            else if (aeroSources.ContainsKey(role) && world.Events.Elapsed is >= 21.1f and < 22.15f)
                world.Events.Add(22.15f - world.Events.Elapsed, () => {
                    if (running && bot.IsAlive()) bot.MoveTo(destination.Position, CrystallizeTimePattern.RunSpeed);
                });
            else bot.MoveTo(destination.Position, CrystallizeTimePattern.RunSpeed);
        }
    }
    private void MoveRoles(Func<PartyRole, Vector3> positions)
    {
        puddleDestinations.Clear();
        foreach (var member in Members())
            if (member is SimPartyNpc bot) bot.MoveTo(positions(bot.Role), CrystallizeTimePattern.RunSpeed);
    }
    private static void ForceAway(SimCharacter? member, Vector3 source, float distance, float speed)
    {
        if (!member.IsAlive()) return;
        if (member is SimPartyNpc bot) bot.Knockback(source, distance, speed);
        else if (member is SimPlayer player) player.Knockback(source, distance, speed);
    }
    private static void Slide(SimCharacter member, Vector3 destination, float seconds)
    {
        var offset = destination - member.Position;
        if (offset.LengthSquared() < 0.0001f) return;
        ForceAway(member, member.Position - offset, offset.Length(), offset.Length() / seconds);
    }
    private void CheckCleanse(CrystallizeTimeAssignment assignment)
    {
        var member = Member(assignment);
        if (member.IsAlive() && (member!.HasStatus(ClawStatus) || member.HasStatus(FangStatus)))
            Hit(member, CleanseFailure, "uncleansed Wyrmclaw / Wyrmfang", true);
    }
    internal static Vector3 DragonPosition(int side, float time)
    {
        // Reference circular travel: 0.245 rad/s. The reference model's hitbox
        // sits ahead of its model origin; use the head center as the native anchor.
        var angle = MathF.Max(0, time - DragonMovementStart) * 0.245f + 0.22f;
        return new((side == 0 ? -1 : 1) * 12.6f * MathF.Sin(angle), 0, -12.6f * MathF.Cos(angle));
    }
    public void Tick(float delta, float elapsed)
    {
        if (!running || usurper is not { IsActive: true } || oracle is not { IsActive: true }) return;
        var time = world.Events.Elapsed;
        if (time is >= 40.6f and < 43.4f) UpdateSpiritSpread();
        if (time < ReturnSnapshotTime)
            foreach (var role in Enum.GetValues<PartyRole>())
                if (world.Party.Get(role) is { } member && !member.IsAlive()) member.RemoveVfx(ReturnClockVfx);
        if (jumping)
        {
            var progress = Math.Clamp((time - 43.4f) / 0.5f, 0, 1);
            oracle.SetPosition(Vector3.Lerp(jumpStart, jumpPosition, progress * progress * (3 - 2 * progress)));
            if (progress >= 1) jumping = false;
        }
        // Display real scenario-clock countdowns while resolution/removal stays
        // event-owned. Refresh before an engine-side countdown can run out.
        foreach (var (key, deadline) in statusDeadlines)
        {
            if (time >= deadline) world.Party.Get(key.Role)?.RemoveStatus(key.Status);
            else world.Party.Get(key.Role)?.FindStatus(key.Status)?.Reapply(MathF.Max(0.2f, deadline - time), 0);
        }
        if (time >= DragonMovementStart && time < 39.6f)
            for (var side = 0; side < 2; side++) TickDragon(side, time);
        foreach (var (source, puddle) in puddles.ToArray())
        {
            if (puddle.Object is not { IsActive: true }) continue;
            if (time - puddle.Created >= 17.8f) { puddle.Object.Despawn(); continue; }
            if (time - puddle.Created < 1f) continue;
            // The interceptor cannot consume their own puddle, even after
            // leaving and returning. Other members can still waste it without Fang.
            var member = Members().FirstOrDefault(m => m is ISimPartyMember slot && slot.Role != puddle.Creator
                && Vector3.Distance(m.Position, puddle.Position) <= 1f);
            if (member == null) continue;
            var cleansed = member.HasStatus(FangStatus);
            member.RemoveStatus(FangStatus);
            puddle.Object.Despawn();
            if (cleansed && time < 35.8f && member is SimPartyNpc bot && pattern.AfterCleanse.TryGetValue(pattern.Assignment(bot.Role), out var next))
            {
                puddleDestinations.Remove(bot.Role);
                bot.MoveTo(next.Position, CrystallizeTimePattern.RunSpeed);
            }
        }
        foreach (var (role, source) in puddleDestinations)
            if (world.Party.Get(role) is SimPartyNpc bot && bot.IsAlive() && puddles.TryGetValue(source, out var puddle)
                && puddle.Object is { IsActive: true }) bot.MoveTo(puddle.Position, CrystallizeTimePattern.RunSpeed);
    }
    private void TickDragon(int side, float time)
    {
        if (dragons[side] is not { IsActive: true } head || dragonHits[side] >= 2) return;
        var position = DragonPosition(side, time);
        head.SetPosition(position);
        var next = DragonPosition(side, time + 0.1f) - position;
        head.SetRotation(MathF.Atan2(next.X, next.Z));
        // Timer expiry must not turn one continuous overlap into a second hit.
        // Use the pre-shrink radius for release, so shrinking the head itself
        // cannot count as the previous interceptor leaving its contact area.
        if (dragonPreviousInterceptors[side] is { } previous)
        {
            var member = world.Party.Get(previous);
            if (!member.IsAlive() || Vector3.Distance(member!.Position, position) > DragonInitialContactRadius)
                dragonPreviousInterceptors[side] = null;
        }
        if (time < dragonCooldown[side]) return;
        var target = Members().Where(m => m is ISimPartyMember member && member.Role != dragonPreviousInterceptors[side])
            .OrderBy(m => Vector3.DistanceSquared(m.Position, position))
            .FirstOrDefault(m => Vector3.Distance(m.Position, position) <=
                (dragonHits[side] == 0 ? DragonInitialContactRadius : DragonAfterFirstHitContactRadius));
        if (target is not ISimPartyMember slot) return;
        dragonPreviousInterceptors[side] = slot.Role;
        dragonCooldown[side] = time + DragonHitCooldown;
        var valid = target.HasStatus(ClawStatus);
        Effect(DragonHit, position, target);
        Circle(position, 12, DragonHit, "dragon interception splash", slot.Role);
        if (!valid) Hit(target, DragonHit, "dragon intercepted without Wyrmclaw", true);
        target.RemoveStatus(ClawStatus);
        if (target is SimPartyNpc bot && pattern.Assignment(slot.Role) is CrystallizeTimeAssignment.AeroWest or CrystallizeTimeAssignment.AeroEast)
        {
            // A native head can reach the interceptor before the reference's
            // fixed move time. Start the next route after actual contact.
            var escape = pattern.East ? CrystallizeTimeRoutes.POST_EARLY_SOAK_E : CrystallizeTimeRoutes.POST_EARLY_SOAK_W;
            bot.MoveTo(escape[pattern.Assignment(slot.Role)].Position, CrystallizeTimePattern.RunSpeed);
        }
        var puddle = world.SpawnEventObject(new EventObjectSpawnConfig { EObjId = Puddle, Placement = new(position, 0), TimelineState = 1 });
        if (puddle != null) Own(puddle);
        puddles[pattern.Assignment(slot.Role)] = (position, puddle, time, slot.Role);
        if (++dragonHits[side] == 2)
        {
            head.Cast(DragonDisappear, castSeconds: 0);
            head.SetVisible(false);
        }
        else
        {
            // Native head radius changes from 2y to 1y after its first contact.
            // Rebuild only the native model, keeping the actor/object-table
            // identity alive for the engine and other plugins this frame.
            head.SetScale(DragonAfterFirstHitScale);
        }
    }
    private void Finish()
    {
        running = false;
        foreach (var tether in tethers) tether.Despawn();
        foreach (var actor in owned) actor.Despawn();
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            world.Party.Get(role)?.RemoveVfx(ReturnClockVfx);
            foreach (var status in Statuses) world.Party.Get(role)?.RemoveStatus(status);
        }
        world.Party.Player?.SetMechanicInputLock(false);
        puddleDestinations.Clear();
        spreadDestinations.Clear();
    }
}
