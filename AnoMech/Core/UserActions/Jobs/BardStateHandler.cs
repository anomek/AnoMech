using System;
using System.Linq;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Gauge;

namespace AnoMech.Core.UserActions.Jobs;

// Bard's songs and their Repertoire — the Song Gauge a ResourceGauge can't hold — written as retail
// writes them. A song sets SongFlags to itself plus its last-played bits and the 45s SongTimer, and
// clears Repertoire; while it plays with the player's DoTs on an enemy, every 3s of it rolls a
// Repertoire: a stack for the Minuet (Pitch Perfect) or the Paeon, a Bloodletter charge for the
// Ballad. The song statuses and the DoTs themselves stay in the JobActions data table.
internal sealed unsafe class BardStateHandler : IUserActionHandler
{
    private const uint MagesBallad = 114, ArmysPaeon = 116, WanderersMinuet = 3559;
    private const uint PitchPerfect = 7404, EmpyrealArrow = 3558, Bloodletter = 110;
    private const uint IronJaws = 3560;
    private const ushort MagesBalladStatus = 2217, ArmysPaeonStatus = 2218, WanderersMinuetStatus = 2216;
    private static readonly ushort[] Dots = [124, 129, 1200, 1201];   // Venomous Bite, Windbite, Caustic Bite, Stormbite

    private const ushort SongMs = 45000;
    private const float TickSeconds = 3f, DotSeconds = 45f;
    private const float RepertoireChance = 0.8f;
    private const int MinuetMaxRepertoire = 3, PaeonMaxRepertoire = 4;
    // Each Repertoire also fills Soul Voice from the trait that unlocks it. UNVERIFIED in game.
    private const int SoulVoicePerRepertoire = 5, SoulVoiceLevel = 80, SoulVoiceMax = 100;
    private const float BloodletterChargeSeconds = 15f;
    private const SongFlags SongMask = (SongFlags)3;

    private readonly Random rng = new();
    private float tickIn;

    public void OnScenarioStart() => tickIn = 0f;

    public void OnAction(ActionType actionType, uint actionId, ulong targetId)
    {
        if (actionType != ActionType.Action || PlayerJob.Current != JobId.Bard) return;
        if (actionId == IronJaws) RefreshDots(targetId);

        using var edit = JobGaugeEdit.Begin();
        var jgm = edit.Gauge;
        if (jgm == null) return;
        var g = &jgm->Bard;
        switch (actionId)
        {
            case MagesBallad: Sing(g, SongFlags.MagesBallad, SongFlags.MagesBalladLastPlayed); break;
            case ArmysPaeon: Sing(g, SongFlags.ArmysPaeon, SongFlags.ArmysPaeonLastPlayed); break;
            case WanderersMinuet: Sing(g, SongFlags.WanderersMinuet, SongFlags.WanderersMinuetLastPlayed); break;
            case PitchPerfect: g->Repertoire = 0; break;
            case EmpyrealArrow: Repertoire(g); break;
        }
    }

    public void OnTick(float deltaSeconds)
    {
        if (PlayerJob.Current != JobId.Bard || Plugin.Game is not { Player: { } player } game) return;
        var jgm = JobGaugeManager.Instance();
        if (jgm == null || (jgm->Bard.SongFlags & SongMask) == 0) return;

        if (!player.HasStatus(SongStatus(jgm->Bard.SongFlags)))
        {
            using var ended = JobGaugeEdit.Begin();
            if (ended.Gauge == null) return;
            ended.Gauge->Bard.SongFlags &= ~SongMask;
            ended.Gauge->Bard.SongTimer = 0;
            ended.Gauge->Bard.Repertoire = 0;
            return;
        }

        tickIn -= deltaSeconds;
        if (tickIn > 0f) return;
        tickIn += TickSeconds;
        if (!DotsUp(game) || rng.NextSingle() >= RepertoireChance) return;
        using var edit = JobGaugeEdit.Begin();
        if (edit.Gauge != null) Repertoire(&edit.Gauge->Bard);
    }

    private static bool DotsUp(Game.Game game)
        => game.World.Children.OfType<SimEnemy>().Any(e => e.IsActive && Dots.Any(e.HasStatus));

    // Iron Jaws renews the DoTs already on its target, and applies none.
    private static void RefreshDots(ulong targetId)
    {
        if (Plugin.Game?.World.Children.OfType<SimEnemy>().FirstOrDefault(e => e.IsActive && (ulong)e.GameObjectId == targetId) is not { } enemy) return;
        foreach (var dot in Dots.Where(enemy.HasStatus))
        {
            enemy.RemoveStatus(dot);
            enemy.AddStatusParam(dot, 0, DotSeconds);
            Plugin.Multiplayer?.ReportAppliedEnemyStatus([enemy], dot, DotSeconds, 0);
        }
    }

    private void Sing(BardGauge* g, SongFlags song, SongFlags lastPlayed)
    {
        g->SongFlags = (g->SongFlags & ~(SongMask | SongFlags.WanderersMinuetLastPlayed)) | song | lastPlayed;
        g->SongTimer = SongMs;
        g->Repertoire = 0;
        tickIn = TickSeconds;
    }

    // A Repertoire for the song playing; none outside one.
    private static void Repertoire(BardGauge* g)
    {
        switch (g->SongFlags & SongMask)
        {
            case SongFlags.WanderersMinuet: g->Repertoire = (byte)Math.Min(g->Repertoire + 1, MinuetMaxRepertoire); break;
            case SongFlags.ArmysPaeon: g->Repertoire = (byte)Math.Min(g->Repertoire + 1, PaeonMaxRepertoire); break;
            case SongFlags.MagesBallad: RefundBloodletterCharge(); break;
            default: return;
        }
        if (Plugin.ObjectTable.LocalPlayer?.Level >= SoulVoiceLevel)
            g->SoulVoice = (byte)Math.Min(g->SoulVoice + SoulVoicePerRepertoire, SoulVoiceMax);
    }

    // Bloodletter's recast is client-side, so the Ballad's proc runs it on by a charge.
    private static void RefundBloodletterCharge()
    {
        var am = ActionManager.Instance();
        if (am == null) return;
        var recast = am->GetRecastGroupDetail(am->GetRecastGroup((int)ActionType.Action, Bloodletter));
        if (recast == null || !recast->IsActive) return;
        recast->Elapsed = Math.Min(recast->Elapsed + BloodletterChargeSeconds, recast->Total);
    }

    private static ushort SongStatus(SongFlags flags) => (flags & SongMask) switch
    {
        SongFlags.MagesBallad => MagesBalladStatus,
        SongFlags.ArmysPaeon => ArmysPaeonStatus,
        _ => WanderersMinuetStatus,
    };
}
