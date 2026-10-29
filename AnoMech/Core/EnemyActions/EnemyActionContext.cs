using System;
using System.Collections.Generic;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.EnemyActions;

// One resolve of one cast, shared by its effects in list order. What it decides for each target
// becomes that target's entries in the ActionEffect.
public sealed class EnemyActionContext
{
    // Never removed from, so it enumerates in insertion order.
    private readonly Dictionary<SimCharacter, TargetOutcome> outcomes = [];
    private readonly List<(SimStatus Status, float RevealAt)> hiddenStatuses = [];
    private readonly List<(SimCharacter, ushort)> impactRemovals = [];

    internal EnemyActionContext(EnemyAction action, SimCharacter caster, SimCharacter? target, Placement origin, SimParty party, float releaseDelay,
        EventScheduler events)
    {
        this.releaseDelay = releaseDelay;
        Action = action;
        Caster = caster;
        Target = target;
        Origin = origin;
        Party = party;
        Events = events;
    }

    // From this resolve to the ActionEffect: a cast's CastSpec.ActionEffectOffset, 0 for an instant action.
    private readonly float releaseDelay;

    public EnemyAction Action { get; }
    // An enemy, or a party member using an action through SimParty.Act.
    public SimCharacter Caster { get; }
    public SimCharacter? Target { get; }
    public Placement Origin { get; }
    public SimParty Party { get; }
    // For an effect whose consequence comes later than this action's own hit.
    public EventScheduler Events { get; }
    // Everyone the area caught, nearest to Origin first.
    public IReadOnlyList<SimCharacter> Hits { get; internal set; } = [];

    // The NPCs the area caught, the caster aside; effects reach them only through OnNpcs.
    public IReadOnlyList<SimEnemy> NpcHits { get; internal set; } = [];

    // Decided now; the character dies to this action after TimingSpec.DamageDelay. The first kill wins.
    public void Kill(SimCharacter who, string? explanation = null)
    {
        var outcome = Outcome(who);
        if (outcome.Killed) return;
        outcome.Killed = true;
        outcome.Explanation = explanation;
    }

    public bool IsKilled(SimCharacter who) => outcomes.TryGetValue(who, out var outcome) && outcome.Killed;

    // The largest hit is the one shown.
    public void ShowDamage(SimCharacter who, uint amount, DamageKind kind)
    {
        var outcome = Outcome(who);
        if (outcome.Damage is not { } shown || amount > shown.Amount) outcome.Damage = (amount, kind);
    }

    // Decided now; moves the character after `delay` (null = TimingSpec.DamageDelay), unless it died
    // meanwhile. knockbackRow goes into the ActionEffect; null for a push with no Knockback sheet
    // row. The first push wins.
    public void Knockback(SimCharacter who, float distance, float speed, float? delay = null, uint? knockbackRow = null)
    {
        var row = knockbackRow is { } id ? Natives.Data.Knockback(id) : null;
        var from = row switch
        {
            { AwayFromTarget: true } when Target is not null => Target.Position,
            // Pushed along the caster's facing: away from a point right behind the pushed one.
            { Direction: 3 } => who.Position - new System.Numerics.Vector3(System.MathF.Sin(Caster.Rotation), 0f, System.MathF.Cos(Caster.Rotation)),
            _ => Caster.Position,
        };
        Outcome(who).Push ??= new Push(from, distance, speed, delay, knockbackRow);
    }

    // Shown as a dodge, for one who escaped the action, e.g. by facing a gaze correctly.
    public void Dodge(SimCharacter who) => Outcome(who).Dodged = true;

    // Applied now, so effects after this one see it, and shown to the client, from the caster, with
    // the ActionEffect. delayToEffectResult: shown only with the EffectResult at TimingSpec.DamageDelay.
    // Either way the full duration runs from when it shows. entryParam goes in the ActionEffect's gain entry.
    public void AddStatus(SimCharacter who, ushort statusId, float duration = 0f, int stacks = 1, bool delayToEffectResult = false,
        byte entryParam = 0)
    {
        var revealAt = delayToEffectResult ? MathF.Max(Action.Timing.DamageDelay, releaseDelay) : releaseDelay;
        var delay = duration > 0f ? revealAt : 0f;
        if (who.AddStatus(statusId, duration + delay, stacks, overrideStacks: false, sourceObject: default, broadcastAddEvent: false, hidden: revealAt > 0f,
                appliedBy: (Action, Caster)) is not { Slot: >= 0 } status) return;
        status.ShownAsFrom(Caster);
        Outcome(who).StatusGains.Add((statusId, entryParam));
        if (status.Hidden) hiddenStatuses.Add((status, revealAt));
    }

    // Taken off at TimingSpec.DamageDelay, unless the character died meanwhile.
    public void RemoveStatusAtImpact(SimCharacter who, ushort statusId) => impactRemovals.Add((who, statusId));

    internal IReadOnlyCollection<TargetOutcome> Outcomes => outcomes.Values;
    internal IReadOnlyList<(SimStatus Status, float RevealAt)> HiddenStatuses => hiddenStatuses;
    internal IReadOnlyList<(SimCharacter Who, ushort StatusId)> ImpactRemovals => impactRemovals;

    private TargetOutcome Outcome(SimCharacter who)
    {
        if (outcomes.TryGetValue(who, out var outcome)) return outcome;
        outcome = new TargetOutcome(who);
        outcomes.Add(who, outcome);
        return outcome;
    }

    internal sealed class TargetOutcome(SimCharacter target)
    {
        public SimCharacter Target { get; } = target;
        public (uint Amount, DamageKind Kind)? Damage { get; set; }
        public bool Killed { get; set; }
        public bool Dodged { get; set; }
        public string? Explanation { get; set; }
        public Push? Push { get; set; }
        public List<(ushort StatusId, byte EntryParam)> StatusGains { get; } = [];
    }

    internal sealed record Push(System.Numerics.Vector3 From, float Distance, float Speed, float? Delay, uint? KnockbackRow);
}
