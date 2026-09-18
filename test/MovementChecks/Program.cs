using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var cases = 0;
foreach (var fps in new[] { 15, 30, 60, 144 })
foreach (var gapTime in new[] { 0f, 0.1f, 0.35f, 0.65f, 0.69f, 0.75f })
foreach (var gapTarget in new[] { new Vector3(0, 0, 0.5f), new Vector3(8, 0, 2), new Vector3(0, 0, 28) })
{
    var player = new SimPlayer { Position = new(0, 0, 2), Rotation = MathF.PI };
    Movement movement = new PlayerMovement(player); // Same base-typed dispatch as SimCharacter.Tick.
    movement.Knockback(Vector3.Zero, 21, 30);
    // A gap-closer reacts to the visible knockback, after at least one slide tick.
    movement.Tick(1f / fps);
    for (var frame = 1; frame < (int)(gapTime * fps); frame++) movement.Tick(1f / fps);

    // Native gap-closer/teleport updates the actor between simulation ticks.
    // It may begin while the original slide still has a little distance left.
    player.Position = gapTarget;
    player.PlayActionTimeline(999);
    var writes = player.PositionWrites;
    var resets = player.AnimationResets;
    movement.Tick(1f / fps);
    Check(!movement.IsMoving && player.Position == gapTarget && player.PositionWrites == writes,
        $"Gap-closer must cancel the old push: fps={fps}, gapTime={gapTime}, target={gapTarget}, actual={player.Position}");
    Check(player.AnimationResets == resets, "Releasing the old slide must not reset the new native action animation");
    for (var frame = 0; frame < fps; frame++) movement.Tick(1f / fps);
    Check(player.Position == gapTarget && player.PositionWrites == writes, "The old knockback never resumes after a gap-closer");

    // Cancellation releases this knockback only, not later mechanic knockbacks.
    movement.Knockback(gapTarget - Vector3.UnitZ, 3, 6);
    for (var frame = 0; frame <= fps; frame++) movement.Tick(1f / fps);
    Check(!movement.IsMoving && Vector3.Distance(player.Position, gapTarget + Vector3.UnitZ * 3) < 0.001f,
        "A later knockback still completes normally after a gap-closer");
    cases++;
}

foreach (var fps in new[] { 15, 30, 60, 144 })
{
    var player = new SimPlayer { Position = new(0, 0, 2), Rotation = 1.2f };
    var movement = new PlayerMovement(player);
    movement.MoveTo(new(8, 0, 8));
    Check(!movement.IsMoving, "Ordinary AI movement still cannot move the player");
    movement.Knockback(Vector3.Zero, 21, 30);
    var frames = 0;
    while (movement.IsMoving && frames++ < fps * 2) movement.Tick(1f / fps);
    Check(Vector3.Distance(player.Position, new(0, 0, 23)) < 0.001f && MathF.Abs(frames / (float)fps - 0.7f) <= 1.1f / fps,
        "Uninterrupted Apocalypse knockback keeps its full distance and duration");
    Check(player.Animations.SequenceEqual(new ushort[] { 156 }) && player.AnimationResets == 1 && player.Rotation == 1.2f,
        "Uninterrupted knockback plays once, releases its animation and preserves facing");

    movement.Knockback(Vector3.Zero, 21, 30);
    // Events start knockback before SimCharacter samples this frame's native
    // position. Pending ordinary input at that first sample is not a gap-closer.
    player.Position += new Vector3(0.4f, 0, 0);
    movement.Tick(1f / fps);
    Check(movement.IsMoving, "Fresh native position on the first slide tick does not cancel knockback");
    player.Position += new Vector3(0.001f, 1, 0); // Native floor-height/rounding correction.
    movement.Tick(1f / fps);
    Check(movement.IsMoving, "Height-only changes and tiny horizontal rounding do not cancel the slide");
    movement.Stop();
    player.Position = new(4, 0, 4);
    var writes = player.PositionWrites;
    movement.Tick(1f / fps);
    Check(!movement.IsMoving && player.PositionWrites == writes, "Explicit stop/reset cannot leave a pending slide");

    var bot = new SimCharacter { Position = new(0, 0, 2) };
    var botMovement = new Movement(bot);
    botMovement.Knockback(Vector3.Zero, 21, 30);
    botMovement.Tick(1f / fps);
    bot.Position = new(0, 0, 3);
    botMovement.Tick(1f / fps);
    Check(botMovement.IsMoving && bot.Position.Z > 3, "Bot knockback retains its existing destination behavior");
}
Console.WriteLine($"PASS: production player movement; {cases} gap-closer interruption cases at 15/30/60/144 FPS; normal slides, facing, animation handoff, later knockbacks, reset and bot behavior.");
