using System.Globalization;
using System.Numerics;
using System.Text;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Tests;

// Positions are the alive members' at the moment of the check: by the time a report is written,
// the check's own knockback may have moved the victim.
internal sealed record AoeCheck(float Time, AoeQuery Query, IReadOnlyList<(PartyRole Role, Vector3 Position)> Positions);

// A plain-text dump of everything on the field, in scenario-local coordinates (+X east, +Z south,
// rotation 0 = south).
internal static class WorldSnapshot
{
    // Hidden enemies (AoE-source helpers, mostly) only with includeHidden: they matter at a death,
    // elsewhere they bury the bosses.
    public static string Describe(SimWorld world, IEnumerable<AoeCheck> aoeChecks, SimCharacter? focus = null, bool includeHidden = false)
    {
        var text = new StringBuilder();
        AppendAoeChecks(text, world, aoeChecks, focus);
        AppendParty(text, world);
        AppendEnemies(text, world, includeHidden);
        AppendOtherObjects(text, world);
        AppendTethers(text, world);
        return text.ToString();
    }

    private static void AppendAoeChecks(StringBuilder text, SimWorld world, IEnumerable<AoeCheck> aoeChecks, SimCharacter? focus)
    {
        var checks = aoeChecks.ToList();
        text.AppendLine($"AoE checks in the last second ({checks.Count}):");
        foreach (var (time, query, positions) in checks)
        {
            var action = Natives.Data.Action(query.ActionId);
            var shape = action is null ? "unknown action" : $"CastType {action.CastType}, range {action.EffectRange}, width {action.XAxisModifier}";
            var extras = $"{(query.OmenRotate != 0f ? $", omenRotate {F(query.OmenRotate)}" : "")}{(query.Size is { } size ? $", size {F(size)}" : "")}";
            text.AppendLine($"  t={F(time)} {ActionLookup.Name(query.ActionId)} ({query.ActionId}; {shape}) from {P(query.Source.Position)} facing {F(query.Source.Rotation)}{extras}");
            var targets = focus is null ? positions : positions.Where(p => world.Party.Get(p.Role) == focus);
            foreach (var (role, position) in targets)
                text.AppendLine($"    {role}: {EdgeDistance(query, position)}");
        }
    }

    private static string EdgeDistance(AoeQuery query, Vector3 position) => query.SignedDistance(position) switch
    {
        null => "unsupported shape",
        < 0f and var d => $"{F(-d)}y inside",
        var d => $"{F(d.Value)}y outside",
    };

    private static void AppendParty(StringBuilder text, SimWorld world)
    {
        text.AppendLine("Party:");
        foreach (var (role, member) in Members(world))
        {
            var flags = $"{(member.IsAlive() ? "" : " DEAD")}{(role == world.Party.PlayerRole ? " [player seat]" : "")}";
            text.AppendLine($"  {role,-13} {P(member.Position)} facing {F(member.Rotation)}{flags}{Moving(member)}");
            AppendStatuses(text, member);
        }
    }

    private static void AppendEnemies(StringBuilder text, SimWorld world, bool includeHidden)
    {
        var enemies = world.Children.OfType<SimEnemy>().ToList();
        var shown = enemies.Where(e => includeHidden || e.SpawnConfig.Visibility == SpawnVisibility.Visible).ToList();
        var omitted = enemies.Count - shown.Count;
        text.AppendLine($"Enemies ({enemies.Count}{(omitted > 0 ? $", {omitted} hidden not shown" : "")}):");
        foreach (var enemy in shown)
        {
            var flags = $"{(enemy.IsActive ? "" : " INACTIVE")}{(enemy.SpawnConfig.Visibility == SpawnVisibility.Visible ? "" : " spawned hidden")}";
            text.AppendLine($"  {Label(world, enemy)} (BNpcBase {enemy.BNpcBaseId}, id 0x{enemy.GameObjectId.ObjectId:X}) {P(enemy.Position)} facing {F(enemy.Rotation)}{flags}{Moving(enemy)}");
            if (enemy.IsCasting)
            {
                var target = enemy.CastTargetLocation is { } location ? $" at {P(location)}" : "";
                text.AppendLine($"    casting {ActionLookup.Name(enemy.CastActionId)} ({enemy.CastActionId}) {F(enemy.CastProgress * enemy.CastTotalSeconds)}/{F(enemy.CastTotalSeconds)}s{target}");
            }
            AppendStatuses(text, enemy);
        }
    }

    private static void AppendOtherObjects(StringBuilder text, SimWorld world)
    {
        var others = world.Children.OfType<IPositioned>().Where(o => o is not SimCharacter).ToList();
        if (others.Count == 0) return;
        text.AppendLine("Other objects:");
        foreach (var other in others)
            text.AppendLine($"  {other.GetType().Name} {P(other.Position)}");
    }

    private static void AppendTethers(StringBuilder text, SimWorld world)
    {
        var tethers = world.Children.OfType<SimTether>().Where(t => t.IsActive).ToList();
        if (tethers.Count == 0) return;
        text.AppendLine("Tethers:");
        foreach (var tether in tethers)
        {
            var length = tether is { A: { } a, B: { } b } ? $", {F(Distance(a.Position, b.Position))}y long" : "";
            text.AppendLine($"  {tether.TetherId}: {Label(world, tether.A)} -> {Label(world, tether.B)}{length}");
        }
    }

    private static void AppendStatuses(StringBuilder text, SimCharacter character)
    {
        var statuses = character.ActiveStatusSnapshot;
        if (statuses.Count == 0) return;
        text.AppendLine("    statuses: " + string.Join("; ", statuses.Select(s =>
            $"{Natives.Data.StatusName(s.StatusId)?.Trim() ?? "?"} ({s.StatusId}){(s.Stacks > 1 ? $" x{s.Stacks}" : "")}{(s.RemainingTime > 0f ? $" {F(s.RemainingTime)}s" : "")}")));
    }

    private static string Moving(SimCharacter character)
        => character.MoveDestination is { } destination
            ? $"  moving -> {P(destination)} ({F(Distance(character.Position, destination))}y left)"
            : "";

    internal static IEnumerable<(PartyRole Role, SimCharacter Member)> Members(SimWorld world)
    {
        for (var i = 0; i < 8; i++)
            if (world.Party.Get(i) is { } member)
                yield return ((PartyRole)i, member);
    }

    private static string Label(SimWorld world, SimCharacter? character) => character switch
    {
        null => "none",
        ISimPartyMember member => member.Role.ToString(),
        SimEnemy enemy => $"{enemy.DisplayName}#{world.Children.OfType<SimEnemy>().ToList().IndexOf(enemy)}",
        _ => character.GetType().Name,
    };

    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));
    private static string P(Vector3 v) => $"({F(v.X)}, {F(v.Z)})";
    private static string F(float value) => value.ToString("F2", CultureInfo.InvariantCulture);
}
