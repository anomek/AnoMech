using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.UserActions;
using AnoMech.Scenarios;

namespace AnoMech.Core.SimObjects;

// Fixed-size 8-slot party indexed by PartyRole. Each slot holds either a
// SimPartyNpc (spawned doppel) or the SimPlayer (the player's own role) — both
// ISimPartyMember. Slots are stored/returned as SimCharacter so they thread
// straight into the character-typed engine APIs (world.Tether, AddStatus, ...);
// the party-slot facets (Role, Knockback) are reached by casting to
// ISimPartyMember.
//
// HUD mirroring is owned by SimWorld (not SimParty) — see SimWorld.partyHud —
// so the addon-lifecycle listener has the same lifespan as the plugin and the
// SimParty.Empty sentinel doesn't accidentally register one at static init.
public sealed partial class SimParty : ISimObject
{
    public static readonly SimParty Empty = new();

    private readonly SimCharacter?[] slots = new SimCharacter?[8];

    public SimParty()
    {
        Find = new CharacterFind<SimCharacter>(ActiveMembers);
        LimitBreak = new LimitBreakGauge(() => Player?.ActorControl);
    }

    public CharacterFind<SimCharacter> Find { get; }

    public LimitBreakGauge LimitBreak { get; }

    // The bot in `role` casts its job's limit break at the level the gauge allows, emptying it.
    // False if the slot is not a live bot (humans press their own), no bar is filled, or the job
    // has none at that level.
    public bool UseLimitBreak(PartyRole role)
    {
        if (Get(role) is not SimPartyNpc bot) return false;
        var level = LimitBreak.FilledBars;
        if (level == 0)
        {
            DiagnosticLog.Info($"[SimParty] {role} limit break skipped: no bar is filled.");
            return false;
        }
        var actionId = bot.UseLimitBreak(level);
        if (actionId == 0) return false;
        LimitBreakHandler.Resolve(this, bot, actionId);
        return true;
    }

    public SimCharacter? Get(int roleId)
        => roleId >= 0 && roleId < slots.Length ? slots[roleId] : null;

    public SimCharacter? Get(PartyRole role) => Get((int)role);

    // The role of the slot currently holding the SimPlayer — set by SetSlot
    // when it receives one. Falls back to MainTank if no SimPlayer has been
    // placed (no scenario hits this today).
    public PartyRole PlayerRole { get; private set; } = PartyRole.MainTank;

    public SimPlayer? Player => Get(PlayerRole) as SimPlayer;

    private void SetSlot(PartyRole role, ISimPartyMember slot)
    {
        slot.Role = role;
        slots[(int)role] = (SimCharacter)slot;
        if (slot is SimPlayer) PlayerRole = role;
    }

    // What each seat was last spawned from, so Apply respawns only the seats that changed.
    private readonly PartySlot?[] seated = new PartySlot?[8];
    private SimPlayer? player;

    // A kept bot goes back to its spawn-ring spot; puppets stay where their owners stand.
    internal void Apply(IReadOnlyList<PartySlot?> roster, SimWorld world)
    {
        this.world = world;
        var ring = world.Stream("party-spawn");
        for (int i = 0; i < slots.Length; i++)
        {
            var role = (PartyRole)i;
            var slot = roster[i];
            var placement = PartyCreator.RingPlacement(role, slot, ring);
            if (Equals(slot, seated[i]) && slots[i] is { IsActive: true } kept)
            {
                if (slot is BotSlot) kept.SetPosition(placement);
                continue;
            }
            // The player only changes seats.
            if (slots[i] is { } leaving) actionHandlers.Remove(leaving);
            if (slots[i] is not SimPlayer) slots[i]?.Despawn();
            slots[i] = null;
            seated[i] = slot;
            if (slot is PlayerSlot)
            {
                player ??= new SimPlayer(world.Coordinates, world.Obstacles);
                SetSlot(role, player);
            }
            else if (slot is not null && PartyCreator.Spawn(slot, world, role, placement) is { } member)
            {
                SetSlot(role, member);
            }
        }
        foreach (var member in slots)
            if (member is not null) member.SimEvents = world.SimEvents;
    }

    private SimWorld? world;
    private readonly Dictionary<SimCharacter, EnemyActionHandler> actionHandlers = new();

    // A member using an action as an enemy does: its ActionEffect, area hits, damage and deaths.
    public void Act(SimCharacter member, EnemyAction action, CastTarget target = default)
    {
        if (!actionHandlers.TryGetValue(member, out var handler))
        {
            var w = world ?? throw new InvalidOperationException("SimParty.Act before the party was seated.");
            actionHandlers[member] = handler = new EnemyActionHandler(member, member.Casting, w);
        }
        handler.Start(action, target, 0);
    }

    internal void ForEachActive(Action<SimCharacter> action)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] is { } m && m.IsAlive()) action(m);
    }

    // Kills every alive member with the given cause. Raidwide wipe primitive
    // used by mechanics whose failure is unsurvivable.
    public void WipeAllPlayers(uint actionId, string? explanation = null)
        => ForEachActive(m => { if (m.IsAlive()) m.Die(actionId, explanation); });

    // The seats people play: this client's own, and the other players' puppets.
    internal IEnumerable<SimCharacter> Players() => AllMembers().Where(m => m is SimPlayer or SimNetworkPuppet);

    // Slots nothing real presses buttons for: a bot, the local player under DebugBotControl (which
    // drives movement only), or a remote seat a bot plays on its own client.
    public bool IsBotDriven(SimCharacter member)
        => member is SimNetworkPuppet puppet ? puppet.BotDriven : !ReferenceEquals(member, Player) || DebugBotControl.Enabled;

    internal IEnumerable<SimCharacter> ActiveMembers()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] is { } m && m.IsAlive()) yield return m;
    }

    // All filled slots with their role index, alive or dead. Used by PartyFinder
    // proximity queries (which follow the same no-alive-filter contract the old
    // FindClosest* methods had).
    internal IEnumerable<(PartyRole role, SimCharacter member)> FilledSlots()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] is { } m) yield return ((PartyRole)i, m);
    }

    // Every filled slot, alive or dead. Used by the party HUD so dead members
    // keep their slot (HP=0 is the visible "dead" state) instead of being
    // silently dropped — and so other members don't shift up the list when
    // someone dies. Mechanic resolvers should keep using ActiveMembers.
    internal IEnumerable<SimCharacter> AllMembers()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] is { } m) yield return m;
    }

    public bool IsAlive
    {
        get
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] is { } m && m.IsAlive()) return true;
            return false;
        }
    }

    // The party persists for the whole scenario and is torn down only by Reset /
    // Despawn — a wipe (every member dead) must NOT reap it, so this is not wired
    // to IsAlive.
    public bool IsActive => true;

    // Each confused member's current chase target, null while held.
    private readonly Dictionary<SimCharacter, SimCharacter?> confusedChases = new();

    // The server walks a confused player after their nearest live ally, retargeting as that
    // changes, for as long as its last ActorControl 0x52 said Confused. Whenever an ally stands in
    // the swing and nothing locks it, the confused player swings at them.
    private void TickConfusedChases()
    {
        foreach (var member in AllMembers())
        {
            if (!member.IsAlive() || !member.ActorControl.Confused)
            {
                if (confusedChases.Remove(member)) member.ForceFollow(null);
                continue;
            }
            if (!member.ConfusedChaseHeld && !member.Casting.IsBusy && Find.Closest(member.Position, member) is { } nearest)
                SwingIfInReach(member, nearest);
            var target = member.ConfusedChaseHeld ? null : Find.Closest(member.Position, member);
            if (confusedChases.TryGetValue(member, out var current) && ReferenceEquals(current, target)) continue;
            confusedChases[member] = target;
            member.ForceFollow(target);
        }
    }

    // The swing aimed at `nearest`, as it would resolve; the chase stands still for its lock.
    private void SwingIfInReach(SimCharacter member, SimCharacter nearest)
    {
        var attack = CommonActions.ConfusedAttack;
        var aim = member.Placement().Face(nearest.Position);
        var query = new AoeQuery(attack.ActionId, aim, attack.Cast.Rotation, attack.Area.Size, attack.Area.CastTypeOverride, member.HitboxRadius);
        if (!query.Run(Find).Any(h => h != member)) return;
        Act(member, attack, nearest);
        member.HoldConfusedChase(attack.Cast.AnimationLock);
    }

    public void Tick(float deltaSeconds)
    {
        TickConfusedChases();
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] is null) continue;
            slots[i]!.Tick(deltaSeconds);
            // Reap a slot only if its member is fully gone (handle released).
            // A KO'd doppel keeps IsActive == true, so this is a safety net,
            // not the normal-path death behaviour.
            if (!slots[i]!.IsActive)
            {
                slots[i]!.Despawn();
                slots[i] = null;
            }
        }
        Natives.PartyHud.Refresh(this);
    }

    // The whole party, the local player included, is in combat for the whole run.
    internal void SetInCombat(bool inCombat)
    {
        foreach (var member in AllMembers()) member.ActorControl.SetInCombat(inCombat);
    }

    public void Mark(Sign sign, SimCharacter target) => Player?.ActorControl.Mark(sign, target.GameObjectId);

    public void ClearMark(Sign sign) => Player?.ActorControl.ClearMark(sign);

    public void ClearMarks() => Player?.ActorControl.ClearMarks();

    // Between runs of one zone stay: the members stay spawned, what the run left on them goes.
    internal void EndRun()
    {
        ClearMarks();
        SetInCombat(false);
        LimitBreak.Restore();
        foreach (var member in AllMembers()) member.ClearRunState();
        confusedChases.Clear();
    }

    public void Despawn()
    {
        ClearMarks();
        SetInCombat(false);
        LimitBreak.Restore();
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i]?.Despawn();
            slots[i] = null;
            seated[i] = null;
        }
        player = null;
        confusedChases.Clear();
        actionHandlers.Clear();
        Natives.PartyHud.Clear();
    }

    public SimCharacter? GetRandom(Rng rng)
    {
        return Get(rng.Next(8));
    }
}
