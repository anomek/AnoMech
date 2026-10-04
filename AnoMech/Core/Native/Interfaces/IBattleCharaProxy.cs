using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.Native.Interfaces;

// One BattleChara: a CharacterManager slot or the local player. The native object is looked up
// again on every call, so while the slot is empty (not created yet, despawned) reads return
// defaults and writes do nothing. Positions and rotations are world space.
public interface IBattleCharaProxy
{
    bool Exists { get; }

    // CharacterManager index; -1 for the local player.
    int Slot { get; }

    uint EntityId { get; }
    GameObjectId GameObjectId { get; }
    string Name { get; }
    byte ClassJob { get; }

    // ── Transform ────────────────────────────────────────────────────────────

    Vector3 Position { get; }

    // The setter is a bare field write; SetRotation also turns the model.
    float Rotation { get; set; }

    void SetPosition(Vector3 position);
    void SetRotation(float rotation);
    float HitboxRadius { get; }

    // ── Model ────────────────────────────────────────────────────────────────

    bool IsReadyToDraw { get; }
    void EnableDraw();
    void DisableDraw();

    // DrawObject.IsVisible: false, and the setter a no-op, while there is no DrawObject.
    bool HasDrawObject { get; }
    bool IsDrawObjectVisible { get; set; }

    // RenderFlags Model|Nameplate.
    void SetModelHidden(bool hidden);

    byte ModelState { get; }
    void SetModelState(byte value);

    byte ModeAttributeFlags { get; }
    // Read only while the model is built: takes effect on the next DisableDraw/EnableDraw.
    void SetModeAttributeFlags(byte value);

    // Null without a DrawObject.
    bool? HasUnloadedModelSlot { get; }

    // ── Animation ────────────────────────────────────────────────────────────

    // baseOverride null leaves TimelineContainer.BaseOverride as it is.
    void PlayActionTimeline(ushort timelineId, ushort loopId = 0, ushort? baseOverride = 0);

    // Slot 0 only; QuiesceActionTimeline clears all 14 sequencer slots (despawn needs that).
    void ResetActionTimeline();
    void QuiesceActionTimeline();

    void SetBaseOverride(ushort timelineId);
    ushort GetSlotTimeline(uint slot);
    void SetSlotTimeline(uint slot, ushort timelineId);
    void PlayTimelineDirect(ushort timelineId);

    // 0 when slot 0 has no scheduler timeline.
    ulong LoadBaseTimelineResources();

    void SetAnimationState(int arg2, int arg3);
    void SetMode(CharacterModes mode, byte param = 0);

    // ── Casting ──────────────────────────────────────────────────────────────

    bool IsCasting { get; }
    uint CastActionId { get; }

    // The engine advances it once ReceiveActorCast started the bar.
    float CurrentCastTime { get; }
    float TotalCastTime { get; }
    // Opens the running bar part-filled.
    void SetCurrentCastTime(float seconds);

    void ClearCast();
    void ReceiveActorCast(ActorCastData cast);
    void ReceiveActionEffect(ActionEffectData effect);

    // The server's ActorControl packet for this actor, through the client's own dispatcher.
    void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0);

    // ── Combat state ─────────────────────────────────────────────────────────

    uint Health { get; set; }
    uint MaxHealth { get; set; }

    // Health, Mana and Mode written directly: the KO pose without the engine's death handling.
    void ApplyDeadState();

    void ClearShield();
    void SetTarget(GameObjectId target);
    byte TargetableStatus { get; }
    void SetTargetable(bool targetable);

    // ── Statuses ─────────────────────────────────────────────────────────────

    // Through the engine's gain path, which applies the status loop VFX and param-driven looks.
    void AddStatusInit(ushort statusId, ushort param, GameObjectId source = default);

    // Writes the slot; the caller re-stamps it every tick.
    void ApplyStatus(ushort statusId, float remainingTime, ushort param, GameObjectId source = default);
    void RemoveStatus(ushort statusId, GameObjectId source = default);

    // ── Effects ──────────────────────────────────────────────────────────────

    // Check the path with IGameData.FileExists first: a bad path crashes on the file thread.
    // Null when the spawn failed.
    IActorVfxProxy? AttachVfx(string path);

    void SetTether(byte slot, ushort tetherId, GameObjectId target, byte progress);
    ushort GetTetherId(byte slot);
    void ClearTether(byte slot);

    void ShowFlyText(uint amount, string label, uint damageTypeIcon = 0);
    // The action's name beside MISS, for a target an action lists but leaves untouched.
    void ShowMissFlyText(string label);

    // The server's forced carry, which the client animates itself.
    void CarryTo(Vector3 destination, float rotation, bool selfTarget);

    // Frees the slot; the local player's proxy ignores it.
    void Despawn();

    // ── Diagnostics ──────────────────────────────────────────────────────────

    string DescribeDrawState();
    string DescribeActionTimeline();
    string DescribeTimelineSlotIds();
    string DescribeModelSlots();
}
