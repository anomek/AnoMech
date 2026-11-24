using System;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Motion;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.SimObjects;

// SimCharacter backed by a BattleChara we allocated in a CharacterManager slot. Overlay state
// (statuses, played actions) lives on the base; this layer adds the packet spawn and the "free the
// slot on Despawn" lifecycle.
public class SimNpc : SimCharacter
{
    private IBattleCharaProxy? proxy;

    internal override IBattleCharaProxy? Proxy => proxy;

    // Forget the slot without touching what it holds: a packet spawn whose slot ended up with
    // somebody else's actor.
    protected void DetachSlot() => proxy = null;

    // The engine creates a packet-spawned actor a few frames after the spawn packet, so the
    // wrapper polls its reserved slot each tick. Failed = nothing arrived within
    // PacketSpawnTimeoutFrames, or the slot holds somebody else's actor.
    public bool PacketSpawnPending { get; private set; }
    public bool PacketSpawnFailed { get; private set; }
    private const int PacketSpawnTimeoutFrames = 20;
    protected int PacketSpawnFrames { get; private set; }
    private uint packetEntityId;
    private Placement packetSpawnPlacement;

    // Pending counts as alive, or SimWorld's reaper would drop the wrapper before its actor
    // exists; every consumer of IsActive null-checks the native pointer.
    public override bool IsActive => PacketSpawnPending || base.IsActive;

    internal void BeginPacketSpawn(uint entityId, Placement placement)
    {
        packetEntityId = entityId;
        packetSpawnPlacement = placement;
        PacketSpawnPending = true;
        Motion.Seed(placement);
        ProbePacketSpawn();
    }

    // The sim-side settings a spawn packet doesn't carry go on here.
    protected virtual void OnPacketSpawned(IBattleCharaProxy chara) { }

    private void ProbePacketSpawn()
    {
        if (!PacketSpawnPending || proxy is not { } chara) return;
        if (!chara.Exists)
        {
            if (PacketSpawnFrames < PacketSpawnTimeoutFrames) return;
            PacketSpawnPending = false;
            PacketSpawnFailed = true;
            Natives.BattleCharas.ReleaseSlot(chara.Slot);
            DiagnosticLog.Warn($"[SimNpc.PacketSpawn] 0x{packetEntityId:X}: nothing arrived at slot {chara.Slot} within {PacketSpawnFrames} frames -- the engine dropped the spawn.");
            return;
        }
        PacketSpawnPending = false;
        Natives.BattleCharas.ReleaseSlot(chara.Slot);
        if (chara.EntityId != packetEntityId)
        {
            PacketSpawnFailed = true;
            DiagnosticLog.Warn($"[SimNpc.PacketSpawn] slot {chara.Slot} holds entity 0x{chara.EntityId:X}, not the packet's 0x{packetEntityId:X} -- not ours; treating the spawn as failed.");
            DetachSlot();
            return;
        }
        if (Position != packetSpawnPlacement.Position || Rotation != packetSpawnPlacement.Rotation) Motion.ResendTransform();
        OnPacketSpawned(chara);
    }

    private protected SimNpc(IBattleCharaProxy proxy, Coordinates coordinates, Func<SimCharacter, CharacterMotion> createMotion)
        : base(coordinates, createMotion)
    {
        this.proxy = proxy;
    }

    public void SetModelState(byte value) => ActorControl.SetModelState(value);

    // Which sub-meshes show, e.g. Omega-M's shield.
    public void SetModeAttributeFlags(byte value) => ActorControl.SetModeAttributeFlags(value);

    // Plays an action's own animation and VFX on this character through the same synthetic
    // ActionEffect a boss cast fires, with itself as the target -- a bot tank's LB3 in Umad P3
    // Limit Cut. Effects (statuses, damage) stay the caller's job, exactly as for an enemy Cast.
    // Not for the local player: an effect the client sees itself cast ends its cast bar mid-cast
    // and leaves the Casting condition set.
    public void PlayAction(uint actionId, float animationLock = 0.6f)
    {
        Casting.NativeActionEffect(actionId, animationLock, (ushort)actionId, 0, ActionType.Action, 0,
            animationTargetId: GameObjectId, targets: [new ActionEffectTarget(GameObjectId)]);
    }

    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);
        PacketSpawnFrames++;
        ProbePacketSpawn();
    }

    public override void Despawn()
    {
        if (proxy == null) return;
        PacketSpawnPending = false;
        Casting.Despawn();
        base.Despawn();
        proxy.Despawn();
        proxy = null;
    }
}
