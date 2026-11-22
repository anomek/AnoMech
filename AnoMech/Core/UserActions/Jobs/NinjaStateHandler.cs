using System;
using System.Collections.Generic;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Core.UserActions.Jobs;

// Mudras, Ninjutsu, Ten Chi Jin and Higi. The game picks what Ten/Chi/Jin, Ninjutsu and the Ninki
// spenders become from state that only arrives from the server (a mudra-combination value no
// structure exposes), and refuses the forms they become without it. So the handler keeps the mudras
// itself, answers the client's GetAdjustedActionId with the forms, and clears the refusals of them,
// keeping their own rules.
internal sealed unsafe class NinjaStateHandler : IUserActionHandler
{
    private const uint Ten = 2259, Chi = 2261, Jin = 2263, Ninjutsu = 2260, TenChiJin = 7403;
    private const uint TenMudra = 18805, ChiMudra = 18806, JinMudra = 18807;
    private const uint FumaShuriken = 2265, Katon = 2266, Raiton = 2267, Hyoton = 2268;
    private const uint Huton = 2269, Doton = 2270, Suiton = 2271, RabbitMedium = 2272;
    private const uint GokaMekkyaku = 16491, HyoshoRanryu = 16492;
    private const uint TenChiJinRaiton = 18877;
    private const uint ForkedRaiju = 25777, FleetingRaiju = 25778;
    private const uint HellfrogMedium = 7401, Bhavacakra = 7402, DeathfrogMedium = 36959, ZeshoMeppo = 36960;

    // Ten Chi Jin's jutsu, by how many it has spent and then by Ten/Chi/Jin.
    private static readonly uint[][] TenChiJinJutsu =
    [
        [18873, 18874, 18875],
        [18876, 18877, 18878],
        [18879, 18880, 18881],
    ];

    private const ushort MudraStatus = 496;
    private const ushort KassatsuStatus = 497;
    private const ushort TenChiJinStatus = 1186;
    private const ushort HigiStatus = 3850;
    private const int HigiFormNinki = 50;
    private const ushort RaijuReadyStatus = 2690;
    private const float MudraSeconds = 6f;
    private const float RaijuReadySeconds = 30f;
    private const int RaijuReadyMaxStacks = 3;

    private static readonly AnyWeaponskillPredicate Weaponskill = new();

    // Ten = 1, Chi = 2, Jin = 3, in the order pressed.
    private readonly List<int> mudras = [];
    private float mudraLeft;
    private readonly List<int> tenChiJinSpent = [];

    public void OnScenarioStart()
    {
        ClearMudras();
        tenChiJinSpent.Clear();
    }

    public void OnTick(float deltaSeconds)
    {
        if (mudras.Count == 0) return;
        mudraLeft -= deltaSeconds;
        if (mudraLeft <= 0f) ClearMudras();
    }

    public uint? AdjustActionId(uint actionId)
    {
        if (PlayerJob.Current != JobId.Ninja) return null;
        if (actionId is HellfrogMedium or Bhavacakra)
            return HasStatus(HigiStatus) ? actionId == HellfrogMedium ? DeathfrogMedium : ZeshoMeppo : null;
        if (InTenChiJin())
            return actionId is Ten or Chi or Jin && Mudra(actionId) is { } mudra && !tenChiJinSpent.Contains(mudra)
                ? TenChiJinJutsu[tenChiJinSpent.Count][mudra - 1]
                : null;
        if (!MudrasOpen()) return null;
        return actionId switch
        {
            Ten => TenMudra,
            Chi => ChiMudra,
            Jin => JinMudra,
            Ninjutsu when mudras.Count > 0 => Jutsu(),
            _ => null,
        };
    }

    // The client checks a pressed button's own action, which may be refused (Ten out of charges,
    // Bhavacakra under Higi) where the action the handler swapped it to isn't: answer for that one.
    // The forms themselves are refused outright, so the handler keeps their rules: a mudra form past
    // the 0.5s recast, a Higi form under Higi with the Ninki for it.
    public uint? AdjustActionStatus(ActionType actionType, uint actionId, bool checkRecast)
    {
        if (actionType != ActionType.Action || PlayerJob.Current != JobId.Ninja) return null;
        var am = ActionManager.Instance();
        if (am == null) return null;
        if (AdjustActionId(actionId) is { } swapped && swapped != actionId)
            return am->GetActionStatus(ActionType.Action, swapped, checkRecastActive: checkRecast);
        var open = actionId switch
        {
            TenMudra or ChiMudra or JinMudra => !InTenChiJin() && MudrasOpen(),
            DeathfrogMedium or ZeshoMeppo => HasStatus(HigiStatus) && JobGaugeManager.Instance()->Ninja.Ninki >= HigiFormNinki,
            _ => false,
        };
        if (!open) return null;
        return checkRecast && am->IsRecastTimerActive(ActionType.Action, actionId) ? null : 0u;
    }

    public void OnAction(ActionType actionType, uint actionId)
    {
        if (actionType != ActionType.Action || PlayerJob.Current != JobId.Ninja || Plugin.Game?.Player is not { } player) return;
        // The Raiju spend a stack each (JobActions); any other weaponskill ends them all.
        if (actionId is not (ForkedRaiju or FleetingRaiju) && Weaponskill.Matches(actionId))
            player.RemoveStatus(RaijuReadyStatus);
        if (actionId == TenChiJin)
        {
            ClearMudras();
            tenChiJinSpent.Clear();
        }
        else if (TenChiJinMudra(actionId) is { } spent)
        {
            if (tenChiJinSpent.Contains(spent)) return;
            tenChiJinSpent.Add(spent);
            if (actionId == TenChiJinRaiton) GrantRaijuReady(player);
            if (tenChiJinSpent.Count == TenChiJinJutsu.Length) player.RemoveStatus(TenChiJinStatus);
        }
        else if (Mudra(actionId) is { } mudra)
        {
            if (mudras.Count == 0) mudraLeft = MudraSeconds;
            mudras.Add(mudra);
            player.RemoveStatus(MudraStatus);
            player.AddStatusParam(MudraStatus, 0, mudraLeft);
        }
        else if (IsJutsu(actionId))
        {
            ClearMudras();
            player.RemoveStatus(KassatsuStatus);
            if (actionId == Raiton) GrantRaijuReady(player);
        }
    }

    // Kassatsu opens the mudras too, so the first one skips the charge.
    private bool MudrasOpen() => mudras.Count > 0 || HasStatus(KassatsuStatus);

    private bool InTenChiJin()
    {
        if (HasStatus(TenChiJinStatus)) return true;
        tenChiJinSpent.Clear();
        return false;
    }

    // The last mudra picks the element, how many were pressed the tier; any mudra twice fails.
    private uint Jutsu()
    {
        if (mudras.Count != new HashSet<int>(mudras).Count || mudras.Count > 3) return RabbitMedium;
        var last = mudras[^1];
        var jutsu = mudras.Count switch
        {
            1 => FumaShuriken,
            2 => last switch { 1 => Katon, 2 => Raiton, _ => Hyoton },
            _ => last switch { 1 => Huton, 2 => Doton, _ => Suiton },
        };
        if (HasStatus(KassatsuStatus))
            jutsu = jutsu switch { Katon => GokaMekkyaku, Hyoton => HyoshoRanryu, _ => jutsu };
        return jutsu;
    }

    private static void GrantRaijuReady(SimCharacter player)
    {
        var stacks = player.FindStatus(RaijuReadyStatus)?.Stacks ?? 0;
        player.RemoveStatus(RaijuReadyStatus);
        player.AddStatusParam(RaijuReadyStatus, Math.Min(stacks + 1, RaijuReadyMaxStacks), RaijuReadySeconds);
    }

    private static int? Mudra(uint actionId) => actionId switch
    {
        Ten or TenMudra => 1,
        Chi or ChiMudra => 2,
        Jin or JinMudra => 3,
        _ => null,
    };

    // The mudra a Ten Chi Jin jutsu spends.
    private static int? TenChiJinMudra(uint actionId)
    {
        foreach (var step in TenChiJinJutsu)
            if (Array.IndexOf(step, actionId) is >= 0 and var mudra) return mudra + 1;
        return null;
    }

    private static bool IsJutsu(uint actionId)
        => actionId is >= FumaShuriken and <= RabbitMedium or GokaMekkyaku or HyoshoRanryu;

    private static bool HasStatus(ushort status) => Plugin.Game?.Player is { } player && player.HasStatus(status);

    private void ClearMudras()
    {
        mudras.Clear();
        mudraLeft = 0f;
        Plugin.Game?.Player?.RemoveStatus(MudraStatus);
    }
}
