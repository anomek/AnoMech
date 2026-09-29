using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;
using NUnit.Framework;

[TestFixture]
public sealed class MovementTests
{
    private static IEnumerable<TestCaseData> GapCloserCases()
    {
        foreach (var fps in new[] { 15, 30, 60, 144 })
            foreach (var gapTime in new[] { 0f, 0.1f, 0.35f, 0.65f, 0.69f, 0.75f })
                foreach (var gapTarget in new[] { new Vector3(0, 0, 0.5f), new Vector3(8, 0, 2), new Vector3(0, 0, 28) })
                    yield return new TestCaseData(fps, gapTime, gapTarget);
    }

    [TestCaseSource(nameof(GapCloserCases))]
    public void GapCloserCancelsKnockbackWithoutResettingItsAnimation(int fps, float gapTime, Vector3 gapTarget)
    {
        var player = new SimPlayer { Position = new(0, 0, 2), Rotation = MathF.PI };
        Movement movement = new PlayerMovement(player); // Same base-typed dispatch as SimCharacter.Tick.
        movement.Knockback(Vector3.Zero, 21, 30);

        // Native gap-closers react after at least one slide tick, including near arrival.
        movement.Tick(1f / fps);
        for (var frame = 1; frame < (int)(gapTime * fps); frame++)
            movement.Tick(1f / fps);

        player.Position = gapTarget;
        player.PlayActionTimeline(999);
        var writes = player.PositionWrites;
        var resets = player.AnimationResets;
        movement.Tick(1f / fps);

        Assert.That(movement.IsMoving, Is.False, "A gap-closer must release the old destination");
        Assert.That(player.Position, Is.EqualTo(gapTarget));
        Assert.That(player.PositionWrites, Is.EqualTo(writes), "The old slide must not move the player again");
        Assert.That(player.AnimationResets, Is.EqualTo(resets), "Keep the new native action animation");

        for (var frame = 0; frame < fps; frame++)
            movement.Tick(1f / fps);

        Assert.That(player.Position, Is.EqualTo(gapTarget), "The old knockback must never resume");
        Assert.That(player.PositionWrites, Is.EqualTo(writes));

        // Interrupting one knockback must not disable later mechanic knockbacks.
        movement.Knockback(gapTarget - Vector3.UnitZ, 3, 6);
        for (var frame = 0; frame <= fps; frame++)
            movement.Tick(1f / fps);

        Assert.That(movement.IsMoving, Is.False);
        Assert.That(Vector3.Distance(player.Position, gapTarget + Vector3.UnitZ * 3), Is.LessThan(0.001f));
    }

    [Test]
    public void OrdinaryAiMovementDoesNotMoveThePlayer()
    {
        var player = new SimPlayer { Position = new(0, 0, 2) };
        Movement movement = new PlayerMovement(player);

        movement.MoveTo(new(8, 0, 8));
        movement.Tick(1);

        Assert.That(movement.IsMoving, Is.False);
        Assert.That(player.Position, Is.EqualTo(new Vector3(0, 0, 2)));
        Assert.That(player.PositionWrites, Is.Zero);
    }

    [Test]
    public void UninterruptedKnockbackPreservesDistanceDurationFacingAndAnimation([Values(15, 30, 60, 144)] int fps)
    {
        var player = new SimPlayer { Position = new(0, 0, 2), Rotation = 1.2f };
        var movement = new PlayerMovement(player);
        movement.Knockback(Vector3.Zero, 21, 30);

        var frames = AdvanceUntilIdle(movement, fps);

        Assert.That(movement.IsMoving, Is.False);
        Assert.That(Vector3.Distance(player.Position, new(0, 0, 23)), Is.LessThan(0.001f));
        Assert.That(frames / (float)fps, Is.EqualTo(0.7f).Within(1.1f / fps));
        Assert.That(player.Animations, Is.EqualTo(new ushort[] { 156 }));
        Assert.That(player.AnimationResets, Is.EqualTo(1));
        Assert.That(player.Rotation, Is.EqualTo(1.2f));
    }

    [Test]
    public void FreshPositionOnFirstTickDoesNotCancelKnockback([Values(15, 30, 60, 144)] int fps)
    {
        var player = new SimPlayer { Position = new(0, 0, 2) };
        var movement = new PlayerMovement(player);
        movement.Knockback(Vector3.Zero, 21, 30);

        // Scenario events start knockback before SimCharacter samples the native actor.
        player.Position += new Vector3(0.4f, 0, 0);
        movement.Tick(1f / fps);

        Assert.That(movement.IsMoving, Is.True, "The first native sample is not a gap-closer");
    }

    [Test]
    public void FloorHeightAndRoundingDoNotCancelKnockback([Values(15, 30, 60, 144)] int fps)
    {
        var player = new SimPlayer { Position = new(0, 0, 2) };
        var movement = new PlayerMovement(player);
        movement.Knockback(Vector3.Zero, 21, 30);
        player.Position += new Vector3(0.4f, 0, 0);
        movement.Tick(1f / fps);

        player.Position += new Vector3(0.001f, 1, 0);
        movement.Tick(1f / fps);

        Assert.That(movement.IsMoving, Is.True, "Ignore floor-height changes and tiny horizontal rounding");
    }

    [Test]
    public void StopCancelsKnockbackAndFurtherPositionWrites([Values(15, 30, 60, 144)] int fps)
    {
        var player = new SimPlayer { Position = new(0, 0, 2) };
        var movement = new PlayerMovement(player);
        movement.Knockback(Vector3.Zero, 21, 30);
        movement.Tick(1f / fps);

        movement.Stop();
        player.Position = new(4, 0, 4);
        var writes = player.PositionWrites;
        movement.Tick(1f / fps);

        Assert.That(movement.IsMoving, Is.False);
        Assert.That(player.PositionWrites, Is.EqualTo(writes));
        Assert.That(player.Position, Is.EqualTo(new Vector3(4, 0, 4)));
    }

    [Test]
    public void BotKnockbackRetainsItsDestinationAfterDisplacement([Values(15, 30, 60, 144)] int fps)
    {
        var bot = new SimCharacter { Position = new(0, 0, 2) };
        var movement = new Movement(bot);
        movement.Knockback(Vector3.Zero, 21, 30);
        movement.Tick(1f / fps);

        bot.Position = new(0, 0, 3);
        movement.Tick(1f / fps);

        Assert.That(movement.IsMoving, Is.True);
        Assert.That(bot.Position.Z, Is.GreaterThan(3), "Bots retain the existing destination behavior");
    }

    [Test]
    public void ThinIceTravels32YalmsAndReleasesMovementAndAnimation([Values(15, 30, 60, 144)] int fps)
    {
        var player = new SimPlayer { Position = new(0, 0, -17) };
        var movement = new PlayerMovement(player);
        movement.Slide(Vector3.UnitZ, 32, 32);
        Assert.That(movement.IsForcedMoving, Is.True, "Thin Ice locks ordinary player locomotion");

        AdvanceUntilIdle(movement, fps);

        Assert.That(movement.IsMoving, Is.False);
        Assert.That(Vector3.Distance(player.Position, new(0, 0, 15)), Is.LessThan(0.001f));
        Assert.That(movement.IsForcedMoving, Is.False);
        Assert.That(player.Animations, Is.EqualTo(new ushort[] { 602 }));
        Assert.That(player.AnimationResets, Is.EqualTo(1));
    }

    [Test]
    public void StopReleasesASubsequentThinIceSlide([Values(15, 30, 60, 144)] int fps)
    {
        var player = new SimPlayer { Position = new(0, 0, -17) };
        var movement = new PlayerMovement(player);
        movement.Slide(Vector3.UnitZ, 32, 32);
        AdvanceUntilIdle(movement, fps);
        movement.Slide(Vector3.UnitZ, 32, 32);
        movement.Tick(1f / fps);
        Assert.That(movement.IsForcedMoving, Is.True);

        movement.Stop();

        Assert.That(movement.IsForcedMoving, Is.False);
        Assert.That(movement.IsMoving, Is.False);
    }

    private static int AdvanceUntilIdle(Movement movement, int fps)
    {
        var frames = 0;
        while (movement.IsMoving && frames < fps * 2)
        {
            movement.Tick(1f / fps);
            frames++;
        }
        return frames;
    }
}
