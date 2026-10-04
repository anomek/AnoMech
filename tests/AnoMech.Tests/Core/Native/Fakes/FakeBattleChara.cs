using System.Numerics;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Tests;

// Resolves its slot on every call, like the real proxy: an empty slot reads defaults and ignores
// writes. Animation, statuses, effects and packets have no readback in the sim and do nothing.
internal sealed class FakeBattleChara(FakeBattleCharas owner, int slot) : IBattleCharaProxy
{
    private const byte TargetableBits = 0x01 | 0x02;

    private FakeCharacter? Actor => owner.ActorAt(slot);

    public bool Exists => Actor != null;
    public int Slot => slot;
    public uint EntityId => Actor?.EntityId ?? 0;
    public GameObjectId GameObjectId => Actor?.GameObjectId ?? default;
    public string Name => Actor?.Name ?? "";
    public byte ClassJob => Actor?.ClassJob ?? 0;

    // ── Transform ────────────────────────────────────────────────────────────

    public Vector3 Position => Actor?.Position ?? default;

    public float Rotation
    {
        get => Actor?.Rotation ?? 0f;
        set { if (Actor is { } a) a.Rotation = value; }
    }

    public void SetPosition(Vector3 position)
    {
        if (Actor is { } a) a.Position = position;
    }

    public void SetRotation(float rotation)
    {
        if (Actor is { } a) a.Rotation = rotation;
    }

    public float HitboxRadius => Actor?.HitboxRadius ?? 0f;

    // ── Model ────────────────────────────────────────────────────────────────

    public bool IsReadyToDraw => Actor != null;

    public void EnableDraw()
    {
        if (Actor is not { } a) return;
        a.HasDrawObject = true;
        a.IsDrawObjectVisible = true;
    }

    public void DisableDraw()
    {
        if (Actor is not { } a) return;
        a.HasDrawObject = false;
        a.IsDrawObjectVisible = false;
    }

    public bool HasDrawObject => Actor?.HasDrawObject ?? false;

    public bool IsDrawObjectVisible
    {
        get => Actor is { HasDrawObject: true, IsDrawObjectVisible: true };
        set { if (Actor is { HasDrawObject: true } a) a.IsDrawObjectVisible = value; }
    }

    public void SetModelHidden(bool hidden) { }

    public byte ModelState => Actor?.ModelState ?? 0;

    public void SetModelState(byte value)
    {
        if (Actor is { } a) a.ModelState = value;
    }

    public byte ModeAttributeFlags => Actor?.ModeAttributeFlags ?? 0;

    public void SetModeAttributeFlags(byte value)
    {
        if (Actor is { } a) a.ModeAttributeFlags = value;
    }

    public bool? HasUnloadedModelSlot => Actor is { HasDrawObject: true } ? false : null;

    // ── Animation ────────────────────────────────────────────────────────────

    public void PlayActionTimeline(ushort timelineId, ushort loopId = 0, ushort? baseOverride = 0) { }
    public void ResetActionTimeline() { }
    public void QuiesceActionTimeline() { }
    public void SetBaseOverride(ushort timelineId) { }
    public ushort GetSlotTimeline(uint slot) => 0;
    public void SetSlotTimeline(uint slot, ushort timelineId) { }
    public void PlayTimelineDirect(ushort timelineId) { }
    public ulong LoadBaseTimelineResources() => 0;
    public void SetAnimationState(int arg2, int arg3) { }
    public void SetMode(CharacterModes mode, byte param = 0) { }

    // ── Casting ──────────────────────────────────────────────────────────────

    public bool IsCasting => Actor?.IsCasting ?? false;
    public uint CastActionId => Actor?.CastActionId ?? 0;
    public float CurrentCastTime => Actor?.CurrentCastTime ?? 0f;
    public float TotalCastTime => Actor?.TotalCastTime ?? 0f;

    public void SetCurrentCastTime(float seconds)
    {
        if (Actor is { } a) a.CurrentCastTime = seconds;
    }

    public void ClearCast()
    {
        if (Actor is not { } a) return;
        a.IsCasting = false;
        a.CastActionId = 0;
    }

    public void ReceiveActorCast(ActorCastData cast)
    {
        if (Actor is not { } a) return;
        a.IsCasting = true;
        a.CastActionId = cast.ActionId;
        a.CurrentCastTime = 0f;
        a.TotalCastTime = cast.CastTime;
    }

    public void ReceiveActionEffect(ActionEffectData effect) { }

    public void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0) { }

    // ── Combat state ─────────────────────────────────────────────────────────

    public uint Health
    {
        get => Actor?.Health ?? 0;
        set { if (Actor is { } a) a.Health = value; }
    }

    public uint MaxHealth
    {
        get => Actor?.MaxHealth ?? 0;
        set { if (Actor is { } a) a.MaxHealth = value; }
    }

    public void ApplyDeadState()
    {
        if (Actor is { } a) a.Health = 0;
    }

    public void ClearShield() { }
    public void SetTarget(GameObjectId target) { }

    public byte TargetableStatus => Actor?.TargetableStatus ?? 0;

    public void SetTargetable(bool targetable)
    {
        if (Actor is not { } a) return;
        a.TargetableStatus = targetable
            ? (byte)(a.TargetableStatus | TargetableBits)
            : (byte)(a.TargetableStatus & ~TargetableBits);
    }

    // ── Statuses ─────────────────────────────────────────────────────────────

    public void AddStatusInit(ushort statusId, ushort param, GameObjectId source = default) { }
    public void ApplyStatus(ushort statusId, float remainingTime, ushort param, GameObjectId source = default) { }
    public void RemoveStatus(ushort statusId, GameObjectId source = default) { }

    // ── Effects ──────────────────────────────────────────────────────────────

    public IActorVfxProxy? AttachVfx(string path) => Actor != null ? new FakeVfxHandle() : null;

    public void SetTether(byte slot, ushort tetherId, GameObjectId target, byte progress)
    {
        if (Actor is { } a) a.Tethers[slot] = tetherId;
    }

    public ushort GetTetherId(byte slot) => Actor is { } a && a.Tethers.TryGetValue(slot, out var id) ? id : (ushort)0;

    public void ClearTether(byte slot) => SetTether(slot, 0, default, 0);

    public void ShowFlyText(uint amount, string label, uint damageTypeIcon = 0) { }
    public void ShowMissFlyText(string label) { }

    public void CarryTo(Vector3 destination, float rotation, bool selfTarget) => Actor?.StartCarry(destination, rotation);

    public void Despawn()
    {
        if (slot >= 0) owner.Free(slot);
    }

    // ── Diagnostics ──────────────────────────────────────────────────────────

    public string DescribeDrawState() => "fake";
    public string DescribeActionTimeline() => "fake";
    public string DescribeTimelineSlotIds() => "-";
    public string DescribeModelSlots() => "fake";
}
