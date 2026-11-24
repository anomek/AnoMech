using System.Linq;
using AnoMech.Core.Native.Implementations;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using AnoMech.Core.Native.Implementations.Interop;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.UserActions;

// The client only flips the auto-attack flag; the swings are server ActionEffects on the
// server's own weapon-delay timer, which the sim firewall blocks. This runs that timer and
// replays each swing at the player's target. Animation only: no damage, no flytext.
internal sealed unsafe class AutoAttackHandler : IUserActionHandler
{
    public const uint AttackActionId = 7;
    public const uint ShotActionId = 8;
    public const float SwingLock = 0.1f;
    // UNVERIFIED: the auto reach past both hitboxes.
    private const float MeleeRange = 2.1f;
    private const float RangedRange = 25f;
    private const float FallbackDelay = 3f;

    // Counts down even while out of range or idle, as the server's does, so re-engaging
    // after the delay has run out swings at once.
    private float swingCooldown;
    private byte swingCount;
    private readonly System.Random rng = new();

    // Each swing, by its action and the enemy it went at.
    public event System.Action<uint, ulong>? Swung;

    public static bool IsAutoAttack(uint actionId) => actionId is AttackActionId or ShotActionId;

    public void OnScenarioStart()
    {
        swingCooldown = 0f;
        swingCount = 0;
    }

    public void OnTick(float deltaSeconds)
    {
        if (swingCooldown > 0f) swingCooldown -= deltaSeconds;
        if (swingCooldown > 0f) return;
        if (!Natives.PlayerInput.IsAutoAttacking) return;
        // Stun and death only hold the swing; autos stay on.
        if (Plugin.Game is not { } game || game.Player is not { Dead: false, ActionsLocked: false }) return;

        var player = (BattleChara*)(Plugin.ObjectTable.LocalPlayer?.Address ?? 0);
        if (player == null || player->CastInfo.IsCasting) return;
        if (TargetedEnemy(game) is not { } enemy) return;

        var job = PlayerJob.Current;
        if (!InReach(player, enemy, ReachOf(job))) return;

        var actionId = ActionIdOf(job);
        swingCooldown = WeaponDelay();
        Swing(actionId, enemy);
        Swung?.Invoke(actionId, (ulong)enemy.GameObjectId);
        JobActions.ApplyAutoAttack(game.Player, actionId, enemy.GameObjectId, rng);
    }

    // Shaped like the server's: no source sequence (it swings on its own timer), a 0.1s lock and
    // the variation cycling 0-2.
    private void Swing(uint actionId, SimEnemy enemy)
    {
        var player = BattleCharaProxy.LocalPlayer;
        player.ReceiveActionEffect(new ActionEffectData(
            actionId, ActionType.Action, SwingLock, (ushort)actionId, (byte)(swingCount++ % 3), 0, player.Rotation, player.Position,
            AnimationTarget: enemy.GameObjectId,
            Targets: [new ActionEffectTarget(enemy.GameObjectId)]));
    }

    // The swing is delivered to the enemy, so it must resolve through CharacterManager.
    private static SimEnemy? TargetedEnemy(Game.Game game)
    {
        if (Plugin.TargetManager.Target is not { } target) return null;
        var enemy = game.World.Children.OfType<SimEnemy>()
            .FirstOrDefault(e => e.IsActive && e.Targetable && (ulong)e.GameObjectId == target.GameObjectId);
        if (enemy == null) return null;
        var cm = CharacterManager.Instance();
        if (cm == null || cm->LookupBattleCharaByEntityId(enemy.GameObjectId.ObjectId) == null) return null;
        return enemy;
    }

    // Dancer's chakram throw is Attack at range; Shot plays nothing on it.
    private static uint ActionIdOf(JobId job)
        => job is JobId.Archer or JobId.Bard or JobId.Machinist ? ShotActionId : AttackActionId;

    // Casters and healers swing in melee like everyone else.
    private static float ReachOf(JobId job)
        => job is JobId.Archer or JobId.Bard or JobId.Machinist or JobId.Dancer ? RangedRange : MeleeRange;

    // SimEnemy.Position is scenario-local; compare world positions.
    private static bool InReach(BattleChara* player, SimEnemy enemy, float range)
    {
        var bc = enemy.BattleCharaPtr();
        if (bc == null) return false;
        var dx = bc->Position.X - player->Position.X;
        var dz = bc->Position.Z - player->Position.Z;
        var reach = range + player->HitboxRadius + bc->HitboxRadius;
        return dx * dx + dz * dz <= reach * reach;
    }

    private static float WeaponDelay()
    {
        var equipped = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped == null) return FallbackDelay;
        var mainHand = equipped->GetInventorySlot(0);
        if (mainHand == null || mainHand->ItemId == 0) return FallbackDelay;
        if (!Plugin.DataManager.GetExcelSheet<Item>().TryGetRow(mainHand->ItemId, out var item) || item.Delayms == 0)
            return FallbackDelay;
        return item.Delayms / 1000f;
    }
}
