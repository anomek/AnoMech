using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using System.Runtime.InteropServices;

namespace AnoMech.Tests;

// CharacterManager's slots and the local player, with BattleCharas' spawn recipes reduced to the
// fields the sim reads back.
internal sealed class FakeBattleCharas : IBattleCharas
{
    private const int SlotCount = 100;
    private const uint CreatedEntityIdBase = 0xE0000000u;
    private const uint PacketSpawnEntityIdBase = 0x4000FE00u;
    private const uint DoppelMaxHealth = 100_000;
    private const uint EnemyMaxHealth = 1_000_000;
    private const float DoppelHitboxRadius = 0.5f;
    private const uint DemihumanSkeletonIdBase = 10000;
    private const byte IsTargetable = 0x02;
    private const int OrphanForgetFrames = 300;

    private readonly FakeCharacter?[] slots = new FakeCharacter?[SlotCount];
    private readonly HashSet<int> reserved = [];
    private readonly List<(int Slot, uint EntityId, int Frames)> orphans = [];

    public FakeCharacter Player { get; } = new(0x10000001u, "Player") { MaxHealth = 100_000, Health = 100_000, HitboxRadius = 0.5f, HasDrawObject = true, IsDrawObjectVisible = true };

    public IBattleCharaProxy LocalPlayer { get; }

    public FakeBattleCharas() => LocalPlayer = new FakeBattleChara(this, -1);

    internal FakeCharacter? ActorAt(int slot) => slot < 0 ? Player : slots[slot];

    internal void Free(int slot) => slots[slot] = null;

    public IBattleCharaProxy? SpawnBattleNpcFromPacket(EnemySpawnConfig config, Placement placement, out uint entityId)
    {
        entityId = 0;
        var (baseId, modelCharaId) = config.NpcSpawnTemplate is { } template
            ? TemplateIds(template)
            : (config.BNpcBaseId, config.ModelCharaId);
        if (Natives.Data.BNpcBase(baseId) is not { } bnpc) return null;
        if (Natives.Data.ModelChara(modelCharaId != 0 ? modelCharaId : bnpc.ModelChara) is not { } modelChara) return null;
        var slot = FindFreeSlot();
        if (slot < 0) return null;
        entityId = PacketSpawnEntityIdBase + (uint)slot;
        var name = Natives.Data.BNpcName(config.NameId) ?? $"BNpc {config.BNpcBaseId:X}";
        var actor = new FakeCharacter(entityId, name)
        {
            Position = placement.Position,
            Rotation = placement.Rotation,
            HitboxRadius = NativeHitboxRadius(modelChara, bnpc.Scale),
            MaxHealth = EnemyMaxHealth,
            Health = EnemyMaxHealth,
        };
        slots[slot] = actor;
        return new FakeBattleChara(this, slot);
    }

    public IBattleCharaProxy? SpawnDoppel(PartyMemberPreset preset, Placement placement)
    {
        var slot = FindFreeSlot();
        if (slot < 0) return null;
        slots[slot] = new FakeCharacter(CreatedEntityIdBase + (uint)slot, preset.Name)
        {
            ClassJob = preset.ClassJob,
            Position = placement.Position,
            Rotation = placement.Rotation,
            HitboxRadius = DoppelHitboxRadius,
            MaxHealth = DoppelMaxHealth,
            Health = DoppelMaxHealth,
            TargetableStatus = IsTargetable,
        };
        return new FakeBattleChara(this, slot);
    }

    public bool IsInCharacterManager(uint entityId)
        => Player.EntityId == entityId || slots.Any(a => a?.EntityId == entityId);

    public void ReleaseSlot(int slot) => reserved.Remove(slot);

    public void NoteOrphan(int slot, uint entityId) => orphans.Add((slot, entityId, 0));

    public void SweepOrphans()
    {
        for (var i = orphans.Count - 1; i >= 0; i--)
        {
            var (slot, entityId, frames) = orphans[i];
            if (slots[slot]?.EntityId == entityId)
            {
                slots[slot] = null;
                reserved.Remove(slot);
                orphans.RemoveAt(i);
            }
            else if (frames >= OrphanForgetFrames)
            {
                reserved.Remove(slot);
                orphans.RemoveAt(i);
            }
            else
            {
                orphans[i] = (slot, entityId, frames + 1);
            }
        }
    }

    internal void Tick(float deltaSeconds)
    {
        Player.Tick(deltaSeconds);
        foreach (var actor in slots) actor?.Tick(deltaSeconds);
    }

    private static (uint BaseId, uint ModelCharaId) TemplateIds(byte[] template)
    {
        var packet = MemoryMarshal.Read<SpawnNpcPacket>(template);
        return (packet.Common.BaseId, packet.Common.ModelChara);
    }

    // UNVERIFIED: assumes the engine's packet handler sizes the actor the way
    // ModelContainer.UpdateHitboxRadius does.
    // ModelContainer.UpdateHitboxRadius: Scale × CalculateUnscaledRadius, which prefers the
    // ModelChara row's radius over its skeleton's. Type 2 and the skeleton-less types are UNVERIFIED.
    private static float NativeHitboxRadius(ModelCharaRow modelChara, float scale)
    {
        if (modelChara.Radius > 0f) return modelChara.Radius * scale;
        var skeletonId = modelChara.Type switch
        {
            2 => modelChara.Model + DemihumanSkeletonIdBase,
            3 => modelChara.Model,
            _ => 0u,
        };
        return (Natives.Data.ModelSkeleton(skeletonId)?.Radius ?? 0f) * scale;
    }

    private int FindFreeSlot()
    {
        for (var i = 0; i < slots.Length; i++)
            if (slots[i] == null && !reserved.Contains(i)) return i;
        return -1;
    }
}
