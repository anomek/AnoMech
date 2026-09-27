using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Scenarios;

// A tankbuster the multiplayer host can pre-plan mitigation for. Id is a stable per-scenario
// key (e.g. "p3-thunder3-1"), not an index that shifts if a cast is reordered. RawDamage is
// the unmitigated hit in actual HP, a fixed number like a real tankbuster; 0f = any tank survives.
public readonly record struct TankBusterCastInfo(string Id, string Label, float RawDamage);

// Tankbuster-survival resolution. A human's mitigation press is intercepted, not observed
// (LocalPlayerInputHooks.UseActionDetour): the real ability is never touched, and a synthetic
// status is applied instead. A bot gets the same AddStatus from ApplyPlannedMitigationIfBot,
// so both read identically via ActiveTrackedStatusIds. A multiplayer peer's press lands on
// their own client, not the host's puppet (see SelfMitigationMessage).
public static unsafe class TankMitigation
{
    // Target-side percentages only; SourceSide entries (Reprisal) debuff the enemy instead.
    public static readonly IReadOnlyDictionary<ushort, float> Percent = TankMitigationChart.All
        .Where(a => a.StatusId != 0 && !a.SourceSide && a.Percent is > 0f)
        .ToDictionary(a => a.StatusId, a => a.Percent!.Value);

    // Checked against whichever SimEnemy a caller identifies as the hit's source.
    public static readonly IReadOnlyDictionary<ushort, float> SourceSidePercent = TankMitigationChart.All
        .Where(a => a.StatusId != 0 && a.SourceSide && a.Percent is > 0f)
        .ToDictionary(a => a.StatusId, a => a.Percent!.Value);

    // An ability needs a known Percent or an explicit Shield opt-in to be intercepted;
    // otherwise blocking it would gain nothing.
    public static readonly IReadOnlyDictionary<uint, TankMitigationAbility> ByActionId = TankMitigationChart.All
        .Where(a => a.ActionId != 0 && a.StatusId != 0 && (a.Percent is > 0f || a.Shield))
        .ToDictionary(a => a.ActionId);

    public static bool IsInvuln(ushort statusId) => Percent.TryGetValue(statusId, out var pct) && pct >= 1f;

    // The only ids a peer's mitigation report may put on the host's characters.
    private static readonly HashSet<ushort> KnownTargetSideStatusIds = TankMitigationChart.All
        .Where(a => a.StatusId != 0 && !a.SourceSide)
        .Select(a => a.StatusId)
        .ToHashSet();

    public static bool IsKnownTargetSideStatus(ushort statusId) => KnownTargetSideStatusIds.Contains(statusId);
    public static bool IsKnownSourceSideStatus(ushort statusId) => SourceSidePercent.ContainsKey(statusId);

    public static IReadOnlyList<ushort> ActiveTrackedStatusIds(SimCharacter member)
    {
        var bc = member.BattleCharaPtr;
        if (bc == null) return [];
        var result = new List<ushort>();
        foreach (var status in bc->StatusManager.Status)
            if (status.StatusId != 0 && Percent.ContainsKey(status.StatusId))
                result.Add(status.StatusId);
        return result;
    }

    // 1f = no mitigation, 0f = fully invulned. Real mitigation stacks multiplicatively
    // (Rampart 20% + Reprisal 10% is 1 - 0.8*0.9 = 28% total, not 30%), not additively.
    private static float SurvivalFraction(IEnumerable<ushort> activeStatusIds)
    {
        var fraction = 1f;
        foreach (var id in activeStatusIds)
        {
            if (!Percent.TryGetValue(id, out var pct)) continue;
            if (pct >= 1f) return 0f; // invuln short-circuits regardless of anything else active
            fraction *= 1f - pct;
        }
        return fraction;
    }

    // A puppet's native StatusManager never sees the owning peer's press, so it adds the peer's
    // self-reported set to what the host itself put on the puppet (a planned kit, an Ai invuln),
    // which only reaches the peer a round trip later. mitigationSource folds in that enemy's
    // SourceSide debuffs.
    public static float SurvivalFraction(SimParty party, PartyRole role, SimEnemy? mitigationSource = null)
    {
        var member = party.Get(role);
        if (member == null) return 1f;
        IEnumerable<ushort> activeIds = member is SimNetworkPuppet && Plugin.MultiplayerInstance is { IsHost: true } mp
            ? ActiveTrackedStatusIds(member).Union(mp.PeerMitigationStatusIds(role))
            : ActiveTrackedStatusIds(member);
        var fraction = SurvivalFraction(activeIds);
        if (mitigationSource != null) fraction *= SourceSideFraction(mitigationSource);
        return fraction;
    }

    private static float SourceSideFraction(SimEnemy source)
    {
        var bc = source.BattleCharaPtr;
        if (bc == null) return 1f;
        var fraction = 1f;
        foreach (var status in bc->StatusManager.Status)
            if (status.StatusId != 0 && SourceSidePercent.TryGetValue(status.StatusId, out var pct))
                fraction *= 1f - pct;
        return fraction;
    }

    // Observed real-game variance on an unmitigated tankbuster hit: +/-5%, uniform.
    private const float RawDamageVarianceFraction = 0.05f;
    private static readonly Random rawDamageRng = new();

    // A real party layers Divine Veil, Heart of Light, Addle, Feint, Troubadour and the rest
    // onto a tankbuster. No bot casts any of them, so a bot tank would meet one with nothing but
    // its own cooldowns and die to hits a real group survives. This flat fraction stands in for
    // the lot, folded into the damage rather than faked as a status: a synthetic status id has
    // no row in the game's Status sheet, so it never reached the native status array the
    // mitigation math reads, and the 20% silently did nothing.
    //
    // TODO: when bots actually cast party mitigation, delete this and let the real statuses
    // carry it -- leaving both would double-count. The same gap applies to a human tank
    // practising here, who gets no party mitigation at all and no compensation for it.
    // Only raw-damage busters take it: Limit Cut's charge and cyclone numbers already assume a
    // party's mitigation.
    public const float BotPartyMitigationFraction = 0.20f;

    // Checked against the target's current HP, so two close hits stack. Order matches real
    // FFXIV: flat-% mitigation first, then the shield absorbs, then real HP is spent.
    // TankShieldTracker works in fraction-of-max-HP terms, hence the conversion around Consume.
    public static unsafe bool ApplyTankBusterDamage(SimParty party, PartyRole role, float rawDamage, SimEnemy? mitigationSource = null, bool standInPartyMitigation = false)
    {
        var member = party.Get(role);
        var bc = member?.BattleCharaPtr;
        if (bc == null) return true;
        var beforeHp = bc->Health;
        var variance = 1f + (float)(rawDamageRng.NextDouble() * 2.0 - 1.0) * RawDamageVarianceFraction;
        var rolledDamage = rawDamage * variance;
        var fraction = SurvivalFraction(party, role, mitigationSource);
        var botParty = standInPartyMitigation && IsBotDriven(party, member!);
        if (botParty) fraction *= 1f - BotPartyMitigationFraction;
        var afterPercentMitigation = rolledDamage * fraction;
        var absorbedFraction = TankShieldTracker.Consume(role, afterPercentMitigation / bc->MaxHealth);
        var landingHp = afterPercentMitigation - absorbedFraction * bc->MaxHealth;
        var survives = landingHp < beforeHp;
        if (survives) bc->Health -= (uint)landingHp;
        DiagnosticLog.Info(
            $"[TankMitigation] ApplyTankBusterDamage: {role} baseRawDamage={rawDamage:F0} rolledDamage={rolledDamage:F0} "
            + $"survivingFraction={fraction:F3} (own cooldowns{(botParty ? $" x {1f - BotPartyMitigationFraction:F2} stand-in party mitigation" : "")}) "
            + $"shieldAbsorbed={absorbedFraction * bc->MaxHealth:F0}hp landingHp={landingHp:F0} maxHp={bc->MaxHealth} hp {beforeHp}->{(survives ? bc->Health : 0)} "
            + $"survives={survives}");
        return survives;
    }

    // Slots nothing real presses buttons for: a bot, or a player (local or a peer) under
    // DebugBotControl, which drives movement only.
    public static bool IsBotDriven(SimParty party, SimCharacter member) => member switch
    {
        SimNetworkPuppet puppet => Plugin.MultiplayerInstance?.IsPeerBotControlled(puppet.Role) == true,
        _ when ReferenceEquals(member, party.Player) => DebugBotControl.Enabled,
        _ => true,
    };

    // Right before a tankbuster resolves: a bot-driven target gets the host's planned status.
    public static void ApplyPlannedMitigationIfBot(SimParty party, SimCharacter target, string castId)
    {
        if (!IsBotDriven(party, target)) return;
        if (Plugin.MultiplayerInstance?.Session.TankBusterPlan.GetValueOrDefault(castId) is not { } statusId || statusId == 0)
            return;
        target.AddStatus(statusId, duration: 5f);
    }
}
