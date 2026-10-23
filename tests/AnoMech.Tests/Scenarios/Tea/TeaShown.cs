using System.Globalization;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Tests;

// Checks on what a TEA client was shown that more than one phase, or more than one client, makes.
// Each reads a client's SimTimeline rows and names what breaks the rule, by row.
internal static class TeaShown
{
    // A real cast bar is the sheet's cast time less the 0.3s its action takes to go out after it.
    public const float CastRelease = 0.3f;

    // The swords' 90-degree cones, apart enough that two never read as one slash.
    private const float SwordsApart = MathF.PI / 6f;

    public static IEnumerable<int> Seeds() => Enumerable.Range(1, TestContext.Parameters.Get("VisibleSeeds", 8));

    public static string Id(SimCharacter? character) => character?.EntityId.ToString("X8") ?? "";

    public static float Angle(float a, float b) => MathF.Abs(MathF.IEEERemainder(a - b, MathF.Tau));

    public static float Bar(Dictionary<string, string> cast)
        => float.Parse(cast["detail"].Split("cast=")[1].Split('s')[0], CultureInfo.InvariantCulture);

    private static string Where(Dictionary<string, string> row) => $"t={row["t"]} {row["actor_name"]}";

    public static IEnumerable<string> BarsOffTheSheet(IReadOnlyList<Dictionary<string, string>> rows)
        => from cast in rows.Where(r => r["kind"] == "cast")
           let sheet = Natives.Data.Action(uint.Parse(cast["id"], CultureInfo.InvariantCulture))?.CastSeconds
           where sheet is null || MathF.Abs(Bar(cast) + CastRelease - sheet.Value) > 0.011f
           select $"{Where(cast)}: {cast["name"]} ({cast["id"]}) bar {Bar(cast):F2}s, sheet {sheet:F2}s";

    // Its own action, from the caster, CastRelease after the bar ends give or take `slack`, unless the
    // caster goes down first and calls it off, as the slowest kills' enrages are: a bar that runs out
    // with nothing after it is a cast stuck on the client.
    public static IEnumerable<string> BarsWithoutTheirAction(IReadOnlyList<Dictionary<string, string>> rows, float slack)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var cast = rows[i];
            if (cast["kind"] != "cast") continue;
            var ends = ShownRun.Time(cast) + Bar(cast);
            var release = rows.Skip(i + 1).TakeWhile(r => ShownRun.Time(r) <= ends + CastRelease + slack + 1f)
                .FirstOrDefault(r => r["actor"] == cast["actor"] && (r["kind"] == "ability" && r["id"] == cast["id"] || r["kind"] == "ac" && r["id"] == "0xF"));
            if (release is null) yield return $"{Where(cast)}: {cast["name"]} ({cast["id"]}) never went off";
            else if (release["kind"] == "ac")
            {
                if (ShownRun.Time(release) > ends + CastRelease + slack) yield return $"{Where(cast)}: {cast["name"]} called off after it should have gone off";
            }
            else if (MathF.Abs(ShownRun.Time(release) - ends - CastRelease) > slack)
                yield return $"{Where(cast)}: {cast["name"]} went off {ShownRun.Time(release) - ends:F3}s after its bar";
        }
    }

    // A cast aimed at its own caster shows its omen along its facing; the hit goes out the same way.
    public static IEnumerable<string> OmensPointingElsewhere(IReadOnlyList<Dictionary<string, string>> rows)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var cast = rows[i];
            if (cast["kind"] != "cast" || cast["target"] is var target && target.Length > 0 && target != cast["actor"]) continue;
            var release = rows.Skip(i + 1).FirstOrDefault(r => r["kind"] == "ability" && r["actor"] == cast["actor"] && r["id"] == cast["id"]);
            if (release is not null && Angle(ShownRun.Number(release, "rot"), ShownRun.Number(cast, "rot")) > 0.011f)
                yield return $"{Where(cast)}: {cast["name"]} ({cast["id"]}) cast facing {cast["rot"]}, went off facing {release["rot"]}";
        }
    }

    // Each tether shown is taken down again, or replaced, or goes with its actor.
    public static IEnumerable<string> TethersLeftUp(IReadOnlyList<Dictionary<string, string>> rows)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var tether = rows[i];
            if (tether["kind"] is not ("ac" or "ac_self") || tether["id"] != "0x23") continue;
            if (!rows.Skip(i + 1).Any(r => r["actor"] == tether["actor"] && (r["kind"] == "despawn" || r["kind"] is "ac" or "ac_self" && r["id"] is "0x2F" or "0x23")))
                yield return $"{Where(tether)}: tether {tether["p"]} still up as the run ends";
        }
    }

    // UNVERIFIED: how fast the engine turns a model toward an ActorMove's facing; a teleport turns it at once.
    private const float ModelTurnRate = 480f * MathF.PI / 180f;
    // Off by this much, a cone still reads as aimed where it hits.
    private const float FacingSlack = MathF.PI / 18f;

    // Where `actor`'s model faces on the client as `row` plays: from its spawn, each teleport turning it
    // at once and each ActorMove turning it toward its facing at the model's turn rate, the packets of
    // `row`'s own frame before it included.
    public static float FacingOnScreen(IReadOnlyList<Dictionary<string, string>> rows, string actor, Dictionary<string, string> row)
        => Facing(rows.TakeWhile(r => !ReferenceEquals(r, row)), actor, ShownRun.Time(row));

    // The same at `time`, every packet up to it included.
    public static float FacingOnScreen(IReadOnlyList<Dictionary<string, string>> rows, string actor, float time)
        => Facing(rows.TakeWhile(r => ShownRun.Time(r) <= time), actor, time);

    private static float Facing(IEnumerable<Dictionary<string, string>> before, string actor, float time)
    {
        var (facing, toward, at) = (0f, 0f, float.NaN);
        foreach (var r in before)
        {
            if (r["actor"] != actor || r["kind"] is not ("spawn" or "set_pos" or "move")) continue;
            if (!float.IsNaN(at)) facing = Turned(facing, toward, ShownRun.Time(r) - at);
            toward = ShownRun.Number(r, "rot");
            if (r["kind"] != "move") facing = toward;
            at = ShownRun.Time(r);
        }
        return float.IsNaN(at) ? facing : Turned(facing, toward, time - at);
    }

    private static float Turned(float from, float toward, float seconds)
    {
        var left = MathF.IEEERemainder(toward - from, MathF.Tau);
        var step = ModelTurnRate * seconds;
        return MathF.Abs(left) <= step ? toward : from + MathF.Sign(left) * step;
    }

    // The client plays a hit along the model's facing: as each of `actor`'s hits among `hitIds` goes out,
    // its model already faces along it.
    public static void FacesEachHitOnScreen(IReadOnlyList<Dictionary<string, string>> rows, string actor, uint[] hitIds, string what)
    {
        foreach (var hit in rows.Where(r => r["actor"] == actor && r["kind"] == "ability" && hitIds.Contains(uint.Parse(r["id"], CultureInfo.InvariantCulture))))
            Assert.That(Angle(FacingOnScreen(rows, actor, hit), ShownRun.Number(hit, "rot")), Is.LessThan(FacingSlack),
                $"{what}: facing elsewhere as the hit at {hit["t"]} went out along {hit["rot"]}");
    }

    // `actor`'s model on screen turns round from `oldFacing` to `newFacing` in [from, until) rather than
    // snapping to it: some frame shows it partway between the two.
    public static void TurnsRoundOnScreen(IReadOnlyList<Dictionary<string, string>> rows, string actor, float from, float until, float oldFacing,
        float newFacing, string what)
    {
        var whole = Angle(oldFacing, newFacing);
        var frames = Enumerable.Range(0, (int)MathF.Ceiling((until - from) / ScenarioRun.FrameSeconds)).Select(n => from + n * ScenarioRun.FrameSeconds);
        Assert.That(frames.Select(t => FacingOnScreen(rows, actor, t)).Any(facing => Angle(oldFacing, facing) > 0.05f && Angle(facing, newFacing) > 0.05f
                                                                                   && MathF.Abs(Angle(oldFacing, facing) + Angle(facing, newFacing) - whole) < 0.02f),
            Is.True, $"{what}: a frame partway from {oldFacing:F2} to {newFacing:F2} in [{from:F2}, {until:F2})");
    }

    // Cruise Chaser's three swords: onto three players, cutting three ways, each along his facing.
    public static void SwordsCutThreeWays(IReadOnlyList<Dictionary<string, string>> rows, string cruiseChaser, uint swordId, string what)
    {
        var swords = rows.Where(r => r["actor"] == cruiseChaser && r["kind"] == "ability" && r["id"] == swordId.ToString(CultureInfo.InvariantCulture)).ToList();
        Assert.That(swords, Has.Count.EqualTo(3), what);
        Assert.Multiple(() =>
        {
            Assert.That(swords.Select(s => ShownRun.Hits(s).Single()).Distinct().Count(), Is.EqualTo(3), $"{what}: three baits, one each");
            FacesEachHitOnScreen(rows, cruiseChaser, [swordId], what);
            for (var a = 0; a < swords.Count; a++)
                for (var b = a + 1; b < swords.Count; b++)
                    Assert.That(Angle(ShownRun.Number(swords[a], "rot"), ShownRun.Number(swords[b], "rot")), Is.GreaterThan(SwordsApart),
                        $"{what}: swords {a + 1} and {b + 1} cut the same way");
        });
    }
}
