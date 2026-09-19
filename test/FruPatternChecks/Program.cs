using System.Numerics;
using AnoMech.Scenarios.Fru.FulgentBlade;
using static AnoMech.Scenarios.Fru.FruConstants;

if (args.Contains("--fru-all")) { FruSequenceChecks.Run(); return; }
if (args.Contains("--darklit")) { DarklitChecks.Run(); return; }
if (args.Contains("--ultimate-relativity")) { UltimateRelativityChecks.Run(); return; }
if (args.Contains("--apocalypse")) { ApocalypseChecks.Run(); return; }
if (args.Contains("--light-rampant")) { LightRampantChecks.Run(); return; }
if (args.Contains("--diamond-dust")) { DiamondDustChecks.Run(); return; }
FruSequenceChecks.Run();
DarklitChecks.Run();
UltimateRelativityChecks.Run();
LightRampantChecks.Run();
DiamondDustChecks.Run();

// Regression oracle: FRU-Sim fb_positions.gd's six dodges, evaluated at the
// snapshots in exawave_controller.tscn + exawave.tscn. No game client required.
Vector2[] roots = [new(0, 17), new(0, -17), new(17, 0), new(-17, 0)];
Vector2[] dodges = [new(1.15f, 2.77f), new(-1.15f, -2.77f), new(-2.77f, 1.15f), new(2.77f, -1.15f)];
int[][] routes = [[1, 0, 2, 3, 0, 1], [0, 1, 2, 3, 1, 0]];
float[] moveTimes = [12, 16, 18, 20, 22, 24];
var safeChecks = 0;
var hitChecks = 0;
var arrowChecks = 0;
var botChecks = 0;

for (var position = 0; position < 4; position++)
for (var rotation = 0; rotation < 4; rotation++)
for (var order = 0; order < 2; order++)
{
    var pattern = new FulgentBladePattern(position, rotation, order == 0);
    var plan = new FulgentBladePartyPlan(pattern);
    for (var step = 0; step < 6; step++)
    {
        var local = dodges[routes[order][step]];
        var angle = -rotation * MathF.PI / 2f;
        var expected = roots[position] + new Vector2(
            local.X * MathF.Cos(angle) - local.Y * MathF.Sin(angle),
            local.X * MathF.Sin(angle) + local.Y * MathF.Cos(angle));
        Assert(Vector3.Distance(plan.Dodge(step), new Vector3(expected.X, 0, expected.Y) * Geometry.ReferenceScale) < 0.0001f,
            "Bot dodge destination must match the independently authored reference route");
        Assert(MathF.Abs(FulgentBladePartyPlan.DodgeTime(step) - (moveTimes[step] + 7.5f)) < 0.0001f,
            "Bot dodge timing must match the reference with the raidwide lead-in");
    }
    botChecks += CheckPartyMovement(pattern, plan);
    for (var group = 0; group < FulgentBladePattern.GroupCount; group++)
    for (var line = 0; line < FulgentBladePattern.WavesPerGroup / 2; line++)
    {
        var warning = pattern.ArrowWarning(group, line);
        var transform = Quaternion.CreateFromAxisAngle(Vector3.UnitY, warning.Rotation);
        foreach (var light in new[] { false, true })
        {
            var wave = line * 2 + (light ? 1 : 0);
            var start = pattern.Wave(group, wave);
            var next = pattern.Wave(group, wave, 1);
            var nativeDirection = Vector3.Transform(light ? -Vector3.UnitX : Vector3.UnitX, transform);
            Assert(Vector3.Distance(warning.Position, start.Position) < 0.0001f,
                "The arrows must originate on the wave's seam");
            Assert(Vector3.Distance(nativeDirection, Vector3.Normalize(next.Position - start.Position)) < 0.0001f,
                "Native gold/purple arrows must point in their matching wave's travel direction");
        }
        arrowChecks++;
    }
    for (var group = 0; group < 3; group++)
    for (var hit = 0; hit < 7; hit++)
    for (var wave = 0; wave < 4; wave++)
    {
        var strip = pattern.Wave(group, wave, hit);
        var forward = new Vector3(MathF.Sin(strip.Rotation), 0, MathF.Cos(strip.Rotation));
        var tangent = new Vector3(forward.Z, 0, -forward.X);
        var center = strip.Position + forward * 2.5f;
        Assert(Inside(center + tangent * 15f, strip), "A point far along the line must be hit");
        Assert(!Inside(strip.Position - forward * 0.1f, strip), "The strip must not extend behind its wavefront");
        Assert(!Inside(strip.Position + forward * 5.1f, strip), "The strip must be five yalms deep");
        if (hit < 6)
        {
            var next = pattern.Wave(group, wave, hit + 1);
            Assert(Vector3.Distance(next.Position, strip.Position + forward * 5f) < 0.0001f,
                "Successive strips must advance exactly one strip depth");
        }
        hitChecks++;

        var time = 13f + group * 4f + hit * 2f;
        if (time >= 26f) continue;
        var dodgeIndex = Array.FindLastIndex(moveTimes, t => t <= time);
        var local = dodges[routes[order][dodgeIndex]];
        // Godot's Vector2.rotated(-yaw), independent of the production quaternion.
        var angle = -rotation * MathF.PI / 2f;
        var rotated = new Vector2(local.X * MathF.Cos(angle) - local.Y * MathF.Sin(angle),
            local.X * MathF.Sin(angle) + local.Y * MathF.Cos(angle));
        var referencePosition = (roots[position] + rotated) * Geometry.ReferenceScale;
        var player = new Vector3(referencePosition.X, 0, referencePosition.Y);
        Assert(!Inside(player, strip), $"Reference dodge hit: position={position}, rotation={rotation}, order={order}, group={group}, wave={wave}, hit={hit}, player={player}, strip={strip}");
        safeChecks++;
    }
}

Console.WriteLine($"PASS: all 32 patterns; {safeChecks} reference dodge checks; {hitChecks} strip geometry checks; {arrowChecks} paired arrow orientations; {botChecks} moving-bot snapshot checks.");
AiIntegrationChecks.Run();
SoloPartyChecks.Run();
StaticVfxTriggerChecks.Run();
ParadiseRegainedChecks.Run();
PolarizingStrikesChecks.Run();
CrystallizeTimeChecks.Run();
ApocalypseChecks.Run();

static int CheckPartyMovement(FulgentBladePattern pattern, FulgentBladePartyPlan plan)
{
    var checks = 0;
    // Include low-frame-rate movement. Events/snapshots happen before movement,
    // as in Game.Tick. This catches safe endpoints reached too late.
    foreach (var fps in new[] { 15, 30, 60 })
    {
        var positions = Enumerable.Range(0, 8).Select(role =>
            new Vector3(MathF.Sin(role * MathF.Tau / 8), 0, MathF.Cos(role * MathF.Tau / 8)) * 3.1f).ToArray();
        var targets = positions.ToArray();
        var boss = Vector3.Zero;
        var bossRotation = 0f;
        var castLocked = false;
        var tankPrepared = false;
        var prepositioned = false;
        var stacked = false;
        var step = 0;
        var hitIndices = new int[3];
        for (var frame = 1; frame <= 41 * fps; frame++)
        {
            var time = (double)frame / fps;
            if (!tankPrepared && time >= FulgentBladePartyPlan.MainTankSetupTime)
            {
                targets[0] = plan.MainTankSetup;
                tankPrepared = true;
            }
            if (!prepositioned && time >= FulgentBladePartyPlan.PrepositionTime)
            {
                Array.Fill(targets, plan.Preposition);
                prepositioned = true;
            }
            while (step < FulgentBladePartyPlan.DodgeCount && time >= FulgentBladePartyPlan.DodgeTime(step))
            {
                Array.Fill(targets, plan.Dodge(step));
                targets[0] = plan.MainTankDodge(step, boss);
                if (step == FulgentBladePartyPlan.MainTankAimStep)
                {
                    var tangent = new Vector3(plan.TankFacing.Z, 0, -plan.TankFacing.X);
                    var originalError = MathF.Abs(Vector3.Dot(plan.Dodge(step) - boss, tangent));
                    var adjustedError = MathF.Abs(Vector3.Dot(targets[0] - boss, tangent));
                    Assert(adjustedError <= originalError + 0.0001f, "MT adjustment must improve or preserve cardinal alignment");
                    Assert(originalError < 0.01f || adjustedError < originalError - 0.01f,
                        "An off-axis MT must make measurable progress toward the cardinal in every normal pattern");
                    Assert(Vector3.Distance(targets[0], plan.Dodge(step)) <= 1.5001f, "MT must stay near the proven dodge pocket");
                }
                step++;
            }
            if (!castLocked && time >= FulgentBladePartyPlan.AkhMornCastTime)
            {
                Assert(Vector3.Distance(positions[0], targets[0]) < 0.001f, "MT must reach its facing adjustment before the cast");
                var toTank = positions[0] - boss;
                bossRotation = MathF.Atan2(toTank.X, toTank.Z);
                Assert(Vector3.Dot(Vector3.Normalize(toTank), plan.TankFacing) > 0.95f,
                    "Pandora must face within 18 degrees of the dodge pocket's cardinal at cast start");
                for (var group = 0; group < 3; group++)
                for (var wave = 0; wave < 4; wave++)
                    Assert(!Inside(positions[0], pattern.Wave(group, wave, 4 - group * 2), 0.249f),
                        "Tank alignment must preserve a quarter-yalm clearance from the cast-time waves");
                castLocked = true;
            }
            if (!stacked && time >= FulgentBladePartyPlan.StackTime)
            {
                var stacks = plan.Stacks(boss, bossRotation);
                for (var role = 0; role < 8; role++)
                    targets[role] = FulgentBladePartyPlan.UsesLeftStack(role) ? stacks.Left : stacks.Right;
                var right = new Vector3(MathF.Cos(bossRotation), 0, -MathF.Sin(bossRotation));
                Assert(Vector3.Dot(stacks.Left - boss, right) < 0 && Vector3.Dot(stacks.Right - boss, right) > 0,
                    "Akh Morn stacks must be on opposite boss-relative sides");
                Assert(Vector3.Distance(stacks.Left, stacks.Right) > 2 * Geometry.AkhMornRadius,
                    "Akh Morn stack circles must not overlap");
                stacked = true;
            }
            for (var group = 0; group < 3; group++)
            {
                while (hitIndices[group] < 7 && time >= 20.5 + group * 4 + hitIndices[group] * 2)
                {
                    for (var wave = 0; wave < 4; wave++)
                    foreach (var position in positions)
                    {
                        Assert(!Inside(position, pattern.Wave(group, wave, hitIndices[group])),
                            $"Bot crossed a wave: {fps}fps, time={time}, group={group}, hit={hitIndices[group]}, wave={wave}, position={position}");
                        checks++;
                    }
                    hitIndices[group]++;
                }
            }
            if (time >= 36 && time < 36 + 1.0 / fps)
            {
                for (var role = 0; role < 8; role++)
                {
                    Assert(Vector3.Distance(positions[role], targets[role]) < 0.001f, "Bots must reach stacks before Akh Morn");
                    Assert(positions.Count(p => Vector3.Distance(p, positions[role]) <= Geometry.AkhMornRadius) == 4,
                        "Every possible Akh Morn target must have exactly four members in range");
                }
            }
            for (var role = 0; role < 8; role++)
            {
                positions[role] = Advance(positions[role], targets[role], FulgentBladePartyPlan.RunSpeed / fps);
                Assert(positions[role].Length() < Geometry.ArenaRadius, "Bots must stay inside the arena throughout movement");
            }
            // Model Pandora chasing a bot MT up to the Akh Morn cast. Its native
            // radius is 7; party bots have a 0.5 radius. Cast lock freezes facing.
            if (!castLocked && !(time >= 1.5 && time < 8.1) && Vector3.Distance(boss, positions[0]) > 7.5f)
                boss = Advance(boss, positions[0], 6f / fps);
        }
    }
    return checks;
}

static Vector3 Advance(Vector3 from, Vector3 to, float distance)
    => Vector3.Distance(from, to) <= distance ? to : from + Vector3.Normalize(to - from) * distance;

static bool Inside(Vector3 point, (Vector3 Position, float Rotation, bool IsLight) strip, float margin = 0f)
{
    var delta = point - strip.Position;
    var depth = delta.X * MathF.Sin(strip.Rotation) + delta.Z * MathF.Cos(strip.Rotation);
    var width = delta.X * MathF.Cos(strip.Rotation) - delta.Z * MathF.Sin(strip.Rotation);
    return depth >= -margin && depth <= Geometry.ExalineStep + margin && MathF.Abs(width) <= Geometry.ExalineHalfWidth + margin;
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
