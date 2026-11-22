using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Tests;

// Resolves its slot on every call, like the real proxy: an empty slot reads defaults and ignores
// writes. Animation, statuses, effects and packets have no readback in the sim and do nothing,
// except what moves the local player.
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

    public float Rotation => Actor?.Rotation ?? 0f;

    public void SetPos(Vector3 position, float rotation)
    {
        Record("set_pos", position: position, rotation: rotation);
        Actor?.Teleport(position, rotation);
    }

    public void Move(Vector3 position, float rotation, ActorMoveFlags flags, float speed)
    {
        Record("move", position: position, rotation: rotation, detail: $"flags={flags} speed={SimTimeline.F(speed)}");
        if (slot >= 0) Actor?.StartGlide(position, rotation);
    }

    public void NpcKnockback(Vector3 position, float rotation, float durationSeconds)
    {
        Record("npc_knockback", position: position, rotation: rotation, detail: $"dur={SimTimeline.F(durationSeconds)}");
        if (slot >= 0) Actor?.StartGlide(position, rotation);
    }

    public float HitboxRadius => Actor?.HitboxRadius ?? 0f;

    // ── Model ────────────────────────────────────────────────────────────────

    public bool HasDrawObject => Actor?.HasDrawObject ?? false;

    public bool IsDrawObjectVisible => Actor is { HasDrawObject: true, IsDrawObjectVisible: true };


    public byte ModelState => Actor?.ModelState ?? 0;

    // ── Casting ──────────────────────────────────────────────────────────────

    public bool IsCasting => Actor?.IsCasting ?? false;
    public uint CastActionId => Actor?.CastActionId ?? 0;
    public float CurrentCastTime => Actor?.CurrentCastTime ?? 0f;
    public float TotalCastTime => Actor?.TotalCastTime ?? 0f;

    public void ReceiveActorCast(ActorCastData cast)
    {
        if (Actor is not { } a) return;
        Record("cast", cast.ActionId.ToString(), ActionName(cast.ActionId), Id(cast.Target), cast.Position, cast.Rotation,
            $"cast={SimTimeline.F(cast.CastTime)}s interruptible={(cast.Interruptible ? 1 : 0)} rel_rot={SimTimeline.F(float.Ieee754Remainder(cast.Rotation - a.Rotation, MathF.Tau))}",
            extra: new Dictionary<string, string>
            {
                ["omen_delay"] = SimTimeline.F(cast.OmenDelay, "F1"),
                ["ballista"] = Id(cast.Ballista) is var b and not 0 ? b.ToString("X8") : "",
            });
        a.IsCasting = true;
        a.CastActionId = cast.ActionId;
        a.CurrentCastTime = 0f;
        a.TotalCastTime = cast.CastTime;
    }

    private static uint nextSequence;

    // The client pushes its own player from a knockback entry the moment it applies it.
    public uint ReceiveActionEffect(ActionEffectData effect)
    {
        var sequence = ++nextSequence;
        var hits = (effect.Targets ?? []).Select(t => owner.Find(Id(t.Id))?.Name ?? Id(t.Id).ToString("X8")).ToList();
        // The replay's ability x/z is the caster's position, not the packet's.
        Record("ability", effect.ActionId.ToString(), ActionName(effect.ActionId), Id(effect.AnimationTarget),
            Actor?.Position, effect.Rotation, $"hits={hits.Count}" + (hits.Count > 0 ? $" [{string.Join(" ", hits)}]" : ""),
            extra: new Dictionary<string, string>
            {
                ["anim_lock"] = SimTimeline.F(effect.AnimationLock),
                ["anim_var"] = effect.AnimationVariation.ToString(),
                // Raw damage entry Param1 (attack type | element << 4); the comparer names them.
                ["damage_type"] = string.Join("/", Entries(effect, 0x03, 0x05, 0x06).Select(e => $"0x{e.Param1:X}").Distinct()),
                ["knockback"] = string.Join("/", Entries(effect, KnockbackEffectType).Select(e => e.Value.ToString()).Distinct()),
                // Not a replay column: the hits by entity id, for comparing two clients' rows.
                ["targets"] = string.Join(" ", (effect.Targets ?? []).Select(t => Id(t.Id).ToString("X8"))),
            });
        owner.RecordEffect(sequence, EntityId, effect.ActionId);
        if (Actor is { } caster)
            foreach (var target in effect.Targets ?? [])
                if (target.Id == owner.Player.GameObjectId)
                    foreach (var entry in target.Effects ?? [])
                        if (entry.Type == KnockbackEffectType && KnockbackHeading(entry.Value, caster, owner.Player) is { } push)
                            owner.Player.StartPush(push.Heading, push.Row.Distance, push.Row.Distance / push.Row.Speed);
        return sequence;
    }

    private const byte KnockbackEffectType = 0x1F;

    private static IEnumerable<ActionEffectEntry> Entries(ActionEffectData effect, params byte[] types)
        => (effect.Targets ?? []).SelectMany(t => t.Effects ?? []).Where(e => types.Contains(e.Type));

    // Directions 1 (sheet angle) and 2 (random) aren't faked; 6 comes as the push packet.
    private static (float Heading, KnockbackRow Row)? KnockbackHeading(uint knockbackId, FakeCharacter source, FakeCharacter pushed)
    {
        if (Natives.Data.Knockback(knockbackId) is not { Speed: > 0f } row) return null;
        float? heading = row.Direction switch
        {
            0 => MathF.Atan2(pushed.Position.X - source.Position.X, pushed.Position.Z - source.Position.Z),
            3 => source.Rotation,
            4 => source.Rotation - MathF.PI / 2f,
            5 => source.Rotation + MathF.PI / 2f,
            _ => null,
        };
        return heading is { } h ? (h, row) : null;
    }

    public void ReceiveEffectResult(uint globalSequence, byte targetIndex, uint health)
    {
        if (EntityId >> 28 == 1 && owner.TakeEffect(globalSequence) is { } effect)
            owner.Timeline.Add("impact", effect.Caster, owner.Find(effect.Caster)?.Name ?? "", effect.ActionId.ToString(),
                ActionName(effect.ActionId), EntityId, Name);
        if (Actor is { } a) a.Health = health;
    }

    public void ForcedFollow(uint? targetEntityId)
    {
        Record("forced_follow", target: targetEntityId ?? 0);
        Actor?.StartForcedFollow(targetEntityId is { } id ? owner.Find(id) : null);
    }

    public void StopForcedMove()
    {
        Record("forced_stop");
        Actor?.StartForcedFollow(null);
    }

    // Only the categories whose result sim code reads back.
    public void ActorControlTarget(uint category, GameObjectId target, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0)
        => Record("ac_target", $"0x{category:X}", target: Id(target), p: SimTimeline.Params(arg1, arg2, arg3, arg4));

    public void ActorControl(uint category, uint arg1 = 0, uint arg2 = 0, uint arg3 = 0, uint arg4 = 0, uint arg5 = 0, uint arg6 = 0, uint arg7 = 0, uint arg8 = 0)
    {
        Record(slot < 0 ? "ac_self" : "ac", $"0x{category:X}", p: SimTimeline.Params(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));
        switch (category)
        {
            case 0x0F: // cancel cast
                if (Actor is { } casting)
                {
                    casting.IsCasting = false;
                    casting.CastActionId = 0;
                }
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
            case 0x197: // action timeline; a warp_end shows a hidden actor, a warp_start hides it
                if (Actor is { } warped)
                {
                    if (arg1 is ActionTimelineId.WarpEnd or ActionTimelineId.WarpEnd2) warped.IsDrawObjectVisible = true;
                    else if (arg1 is ActionTimelineId.WarpStart or ActionTimelineId.WarpStart2) warped.IsDrawObjectVisible = false;
                }
                break;
            case 0xE1: // push, p1 = heading, p2 = distance x10000, p3 = duration x10000
                Actor?.StartPush(arg1 / (float)ushort.MaxValue * MathF.Tau - MathF.PI, arg2 / 10000f, arg3 / 10000f);
                break;
            case 0xDF: // ice slide, p1 = x<<16|y, p2 = z<<16|rotation
                Actor?.StartSlide(
                    new Vector3(Position16(arg1 >> 16), Position16(arg1 & 0xFFFF), Position16(arg2 >> 16)),
                    (arg2 & 0xFFFF) / (float)ushort.MaxValue * MathF.Tau - MathF.PI);
                break;
            case 0xF1: // warp, p1 = x<<16|y, p2 = z<<16|rotation
                Actor?.StartCarry(
                    new Vector3(Position16(arg1 >> 16), Position16(arg1 & 0xFFFF), Position16(arg2 >> 16)),
                    (arg2 & 0xFFFF) / (float)ushort.MaxValue * MathF.Tau - MathF.PI);
                break;
        }
    }

    private void Record(string kind, string id = "", string name = "", uint target = 0, Vector3? position = null, float? rotation = null,
        string detail = "", string p = "", IReadOnlyDictionary<string, string>? extra = null)
    {
        if (Actor is not { } a) return;
        owner.Timeline.Add(kind, a.EntityId, a.Name, id, name, target, target == 0 ? "" : owner.Find(target)?.Name ?? "",
            position, rotation, detail, p, extra);
    }

    private static string ActionName(uint actionId) => Natives.Data.Action(actionId)?.Name ?? "";

    private static uint Id(GameObjectId? id) => id is { ObjectId: var objectId and not 0xE0000000 } ? objectId : 0;

    private static float Position16(uint value) => value / 32.767f - 1000f;

    // ── Combat state ─────────────────────────────────────────────────────────

    public uint Health => Actor?.Health ?? 0;

    public uint MaxHealth => Actor?.MaxHealth ?? 0;

    public uint Mana => Actor?.Mana ?? 0;
    public uint MaxMana => Actor?.MaxMana ?? 0;

    public byte TargetableStatus => Actor?.TargetableStatus ?? 0;

    // ── Statuses ─────────────────────────────────────────────────────────────

    public void ReceiveStatusList(IReadOnlyList<StatusSlot> statuses, uint health, uint mana, byte shield)
    {
        if (Actor is not { } a) return;
        var now = statuses.Where(s => s.StatusId != 0).GroupBy(s => (s.StatusId, FakeCharacter.StatusSource(s.SourceEntityId))).ToDictionary(g => g.Key, g => g.First());
        foreach (var (key, s) in now.Where(n => !a.Statuses.ContainsKey(n.Key)))
            Record("status_gain", key.StatusId.ToString(), "", s.SourceEntityId, detail: $"dur={SimTimeline.F(s.RemainingTime, "F1")} param={s.Param}");
        foreach (var key in a.Statuses.Keys.Where(k => !now.ContainsKey(k)))
            Record("status_lose", key.StatusId.ToString());
        a.Statuses = now;
        a.Health = health;
        a.Mana = mana;
    }

    // ── Effects ──────────────────────────────────────────────────────────────

    public ushort GetTetherId(byte slot) => Actor is { } a && a.Tethers.TryGetValue(slot, out var id) ? id : (ushort)0;


    public void Despawn()
    {
        if (slot < 0) return;
        Record("despawn");
        owner.Free(slot);
    }

    // ── Diagnostics ──────────────────────────────────────────────────────────

    public string DescribeDrawState() => "fake";
    public string DescribeActionTimeline() => "fake";
    public string DescribeTimelineSlotIds() => "-";
    public string DescribeModelSlots() => "fake";
}
