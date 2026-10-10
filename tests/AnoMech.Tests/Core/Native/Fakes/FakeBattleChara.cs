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

    // ── Casting ──────────────────────────────────────────────────────────────

    public bool IsCasting => Actor?.IsCasting ?? false;
    public uint CastActionId => Actor?.CastActionId ?? 0;
    public float CurrentCastTime => Actor?.CurrentCastTime ?? 0f;
    public float TotalCastTime => Actor?.TotalCastTime ?? 0f;

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

    // Only the categories whose result sim code reads back.
    public void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0)
    {
        switch (category)
        {
            case 0x0F: // cancel cast
                ClearCast();
                break;
            case 0x3F: // ModelState
                if (Actor is { } a) a.ModelState = (byte)arg1;
                break;
            case 0x23: // tether, slot 0
                if (Actor is { } tethered) tethered.Tethers[0] = (ushort)arg2;
                break;
            case 0x2F: // tether clear, slot 0
                if (Actor is { } cleared) cleared.Tethers[0] = 0;
                break;
            case 0x36: // targetable
                if (Actor is { } t)
                    t.TargetableStatus = arg1 != 0
                        ? (byte)(t.TargetableStatus | TargetableBits)
                        : (byte)(t.TargetableStatus & ~TargetableBits);
                break;
            case 0xF1: // warp, p1 = x<<16|y, p2 = z<<16|rotation
                Actor?.StartCarry(
                    new Vector3(Position16(arg1 >> 16), Position16(arg1 & 0xFFFF), Position16(arg2 >> 16)),
                    (arg2 & 0xFFFF) / (float)ushort.MaxValue * MathF.Tau - MathF.PI);
                break;
        }
    }

    private static float Position16(uint value) => value / 32.767f - 1000f;

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

    // ── Statuses ─────────────────────────────────────────────────────────────

    public void AddStatusInit(ushort statusId, ushort param, GameObjectId source = default) { }
    public void ApplyStatus(ushort statusId, float remainingTime, ushort param, GameObjectId source = default) { }
    public void RemoveStatus(ushort statusId, GameObjectId source = default) { }

    // ── Effects ──────────────────────────────────────────────────────────────

    public IActorVfxProxy? AttachVfx(string path) => Actor != null ? new FakeVfxHandle() : null;

    public ushort GetTetherId(byte slot) => Actor is { } a && a.Tethers.TryGetValue(slot, out var id) ? id : (ushort)0;

    public void ShowFlyText(uint amount, string label, uint damageTypeIcon = 0) { }

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
