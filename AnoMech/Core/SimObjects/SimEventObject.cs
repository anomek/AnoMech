using AnoMech.Core.Game;
using AnoMech.Core.Native;
using AnoMech.Helpers;
using AnoMech.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Network;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using System.Numerics;

namespace AnoMech.Core.SimObjects;

// How an EObj animation beat (the server's ActorControl category 413, param1/param2) reaches
// the client. ActorControl feeds the real packet through the client's own dispatcher; the
// other two are the FFXIVClientStructs member functions guessed to be that handler.
public enum PropBeatMode
{
    ActorControl,
    PlayAnimation,
    SetSharedTimelineState,
}

public class EventObjectSpawnConfig
{
    // References Lumina's EObj sheet,
    // the row's SgbPath/PopType drives the model picked by the engine's internal SharedGroup attach.
    // ModelChara substitution is not part of the EObj pipeline; pick the right EObj row.
    public uint EObjId { get; init; }

    // Placement.Position is scenario-local (offset from SimWorld.ScenarioOrigin),
    // same coordinate space as SimEventObject.Position / SetPosition once spawned.
    public Placement Placement { get; init; }

    public sbyte ObjectIndex { get; init; } = -1;
    public byte TargetableStatus { get; init; } = 1; // 1 - untargettable
    public byte VisibilityFlag { get; init; } = 0;
    // Local scenery has no server entity. The engine uses this sentinel and
    // its object index for identity; zero produces invalid/duplicate actors
    // for plugins that synchronize the game object table (e.g. Boss Mod).
    public uint EntityId { get; init; } = 0xE0000000;
    public uint LayoutId { get; init; } = 0;
    public EventId EventId { get; init; } = 0;
    public uint OwnerId { get; init; } = 0xE0000000;
    public uint GimmickId { get; init; } = 0;
    public float Radius { get; init; } = 1;
    public ushort FateId { get; init; } = 0;
    public byte EventState { get; init; } = 0;
    // SpawnObjectPacket +0x30; the real EObj spawns carry 3 in the low byte plus a per-object
    // index in the high word.
    public uint Arg2 { get; init; } = 0;

    // This is the SG state index that means "visible" for this EObj.
    // The orb (1EB83C) is already visible at the engine default state=0, so leave it at 0.
    // The Sigma ground circles (1EB83D / 1EB83E) need state=16 to render fully
    // state=1..6 partial-renders are the engine's player-proximity animation frames. SetVisible toggles between this value and 0.
    public ushort TimelineState { get; init; } = 0;

    public bool SpawnVisible { get; init; } = true;
    public float Lifetime { get; init; } = 0;

    // Deactivate the SGB Sound children every frame: the P1 gaze props' timeline keeps
    // re-triggering statue/Kefka voice cues.
    public bool MuteSound { get; init; } = false;

    // Debug aid (see LayoutInstanceDiagnostics.ForceActive): force the SharedGroup active once
    // attached and after every beat.
    public bool ForceSharedGroupActive { get; init; } = false;

    // For a prop whose SGB has no timeline for that state (the teleporters).
    public ushort HideAtState { get; init; } = 0;

    public unsafe SpawnObjectPacket ToPacket(Coordinates coordinates)
    {
        var objectIndex = sbyte.Max(-1, ObjectIndex);
        var worldPos = coordinates.ToGlobal(Placement.Position);

        var packet = new SpawnObjectPacket
        {
            ObjectIndex = (byte)objectIndex,
            ObjectKind = 7, // EventObject
            TargetableStatus = TargetableStatus,
            Visibility = VisibilityFlag,
            BaseId = EObjId,
            EntityId = EntityId,
            LayoutId = LayoutId,
            EventId = EventId,
            OwnerId = OwnerId,
            GimmickId = GimmickId,
            Radius = Radius,
            Rotation = MathUtil.QuantizeRotation(Placement.Rotation),
            FateId = FateId,
            EventState = EventState,
            Arg2 = Arg2,
            PositionX = worldPos.X,
            PositionY = worldPos.Y,
            PositionZ = worldPos.Z
        };

        // private in CS, so we have to set it manually
        var packetBytePtr = (byte*)&packet;
        var timelineStatePtr = (ushort*)(packetBytePtr + 0x2C);
        *timelineStatePtr = TimelineState;

        return packet;
    }
}

// Handle around an EventObject GameObject allocated via the manager's
// CreateEventObject (the 40-slot pool exposed in GameObjectManager indices
// 449-488). Mirror of SimEnemy / SimNpc for the EObj actor pool: we own the
// slot, write position/rotation/state directly on the GameObject, and release
// the slot on Despawn via GameObject.Terminate (vfunc 60).
//
// Rendering note: EObjs render via the LayoutEngine scene graph using their
// attached SharedGroup, NOT via GameObject.DrawObject. Visibility is therefore
// driven by the state field at actor+0x1B2 (which gates which SG sub-instances
// are visible), not by EnableDraw/DisableDraw — those are character-only.
//
// Why not packets: the canonical spawn path is HandleSpawnObjectPacket, which
// brings zone-state guards, housing/MJI branches, and forwards to SetEventId/
// SetFateId/SetEventState that don't apply to simulated scenery. We use the
// same internals-only pattern BattleCharaSpawn uses for SimEnemy/SimPartyNpc.
public unsafe class SimEventObject : ISimObject, IPositioned
{
    private int slot = -1;
    private GameObject* obj;
    private readonly Coordinates coordinates;
    private readonly ushort visibleState;
    private readonly float lifetime;
    private readonly uint layoutId;

    // The SharedGroup behind LayoutId can stay mid-load after the C#-visible state already
    // looks correct; logged at fixed checkpoints.
    private int loadCheckFrames;
    private bool loadCheckDone;

    public uint EObjRowId { get; }
    public string DisplayName => $"EObj 0x{EObjRowId:X}";

    // Lets a peer reconstruct the same prop (SimTower has none: a tower's states drive it).
    public EventObjectSpawnConfig? SpawnConfig { get; private set; }
    public int Slot => slot;
    public nint Address => (nint)obj;
    public GameObjectId GameObjectId => obj == null ? default : obj->GetGameObjectId();

    public bool IsAlive => slot >= 0 && obj != null;

    // The SharedGroup attaches ~1s after the spawn; a beat before that only writes the static
    // state and runs no SGB timeline.
    public bool IsSharedGroupAttached => obj != null && ((EventObject*)obj)->SharedGroupLayoutInstance != null;
    public bool IsSharedGroupTimelinePlaying => obj != null && LayoutInstanceDiagnostics.IsAnyTimelinePlaying(((EventObject*)obj)->SharedGroupLayoutInstance);
    // No death-vs-presence distinction for event objects: kept while the slot is live.
    public virtual bool IsActive => IsAlive;

    // Stored Position/Rotation mirror the native GameObject — mutators write
    // both, and Tick re-syncs from native to catch any direct-struct writes.
    public Vector3 Position { get; private set; }
    public float Rotation { get; private set; }

    // Sampled for peers alongside CurrentState.
    public ushort VisibleState => visibleState;

    // Sampled for peers; 0 would attach the wrong SharedGroup.
    public uint LayoutId => layoutId;

    // Sampled for peers: the state write at actor+0x1B2 has no other observable signal.
    public ushort CurrentState { get; private set; }

    private float lifetimeElapsed { get; set; } = 0;

    // Settable: the colossus is muted while its state history is caught up and unmuted once it
    // stands, so its real collapse sounds play.
    public bool MuteSound { get; set; }
    private readonly bool forceSharedGroupActive;
    private bool forceActiveLogged;
    // Delayed SG dumps after PlayAnimation (-1 = none pending); the synchronous one only shows
    // the timeline's first frame.
    private int animCheckFrames = -1;

    protected SimEventObject(int slot, GameObject* obj, Coordinates coordinates, uint eObjRowId, ushort visibleState, float lifetime, uint layoutId, bool muteSound = false, bool forceSharedGroupActive = false)
    {
        this.slot = slot;
        this.obj = obj;
        this.coordinates = coordinates;
        this.visibleState = visibleState;
        this.lifetime = lifetime;
        this.layoutId = layoutId;
        MuteSound = muteSound;
        this.forceSharedGroupActive = forceSharedGroupActive;
        EObjRowId = eObjRowId;
        // Without a LayoutId the checkpoints read the actor's own attached SharedGroup; a bare
        // EObj (no LayoutId, state 0) has nothing state-gated to check.
        loadCheckDone = layoutId == 0 && visibleState == 0;
    }

    internal static SimEventObject? Spawn(EventObjectSpawnConfig config, Coordinates coordinates, EventScheduler events)
    {
        var packet = config.ToPacket(coordinates);

        if (!EventObjectHelper.Create(&packet, out var slot, out var eObjPtr))
        {
            // A full 40-slot EventObjectManager pool (EObjs left over from an earlier run, or
            // the zone's own) is the usual cause.
            DiagnosticLog.Warn($"[SimEventObject.Create] Failed to spawn EObjId 0x{config.EObjId:X} at ({packet.PositionX:F2}, {packet.PositionY:F2}, {packet.PositionZ:F2}) -- EventObjectManager's 40-slot pool is likely full.");
            return null;
        }

        var eObj = new SimEventObject(slot, eObjPtr, coordinates, config.EObjId, config.TimelineState, config.Lifetime, config.LayoutId, config.MuteSound, config.ForceSharedGroupActive)
        {
            SpawnConfig = config,
        };

        if (!config.SpawnVisible && config.TimelineState != 0)
        {
            eObj.SetVisible(false);
        }

        DiagnosticLog.Info($"[SimEventObject.Create] Spawned EObj with EObjId 0x{config.EObjId:X} at Slot: {slot} ({packet.PositionX:F2}, {packet.PositionY:F2}, {packet.PositionZ:F2})");
        return eObj;
    }

    public void SetPosition(Vector3 position)
    {
        Position = position;
        if (obj == null) return;
        var w = coordinates.ToGlobal(position);
        obj->SetPosition(w.X, w.Y, w.Z);
    }

    public void SetPosition(Placement placement)
    {
        Position = placement.Position;
        Rotation = MathUtil.NormalizeRotation(placement.Rotation);
        if (obj == null) return;
        var w = coordinates.ToGlobal(placement.Position);
        obj->SetPosition(w.X, w.Y, w.Z);
        obj->SetRotation(Rotation);
    }

    // Writes the EObj state field at actor+0x1B2 and (when the SharedGroup
    // layout instance is attached at actor+0x108) notifies the SG to flip
    // sub-instance visibility. Per-EObj state values are SG-specific —
    // experiment empirically to find what activates a given visual. Safe to
    // call before the SG instance is attached: only the field write happens,
    // the notify silently no-ops; the engine picks up the field once attached.
    public void SetState(ushort state)
    {
        if (obj == null) return;
        CurrentState = state;
        EventObjectHelper.SetState(obj, state);
    }

    // Convenience for parser-driven scenarios that emit SetVisible from
    // ACT 261|Change ModelStatus events. Flips between the configured
    // VisibleState and 0 (the engine default / "hidden" for gated SGs).
    public void SetVisible(bool visible) => SetState(visible ? visibleState : (ushort)0);

    // Edge-tracked like SimEnemy.AnimationState, and sampled for peers: LastBeatMode so a peer
    // delivers the beat the same way the host chose to.
    public (uint State, uint Bitmask)? LastAnimation { get; private set; }
    public int AnimationSeq { get; private set; }
    public PropBeatMode LastBeatMode { get; private set; } = PropBeatMode.ActorControl;

    // ActorControl 607 has no state of its own to sample, so peers follow the counter.
    public int FadeOutSeq { get; private set; }

    // Client-side equivalent of ActorControl 413 (EObjAnimation): `state` becomes the new
    // SharedTimelineState, `bitmask` picks which SG timelines play. No-ops before the SG
    // instance is attached; the engine re-applies once it loads.
    public void PlayAnimation(uint state, uint bitmask) => PlayBeat(state, bitmask, PropBeatMode.PlayAnimation);

    // ActorControl 607 (self, 1, 0, 100): the prop fades out, as sent to spent boulders and
    // resolved crystals.
    public void FadeOut()
    {
        FadeOutSeq++;
        if (obj == null) return;
        PacketDispatcher.HandleActorControlPacket(obj->EntityId, 607, obj->EntityId, 1, 0, 100, 0, 0, 0, 0, 0xE0000000, false);
    }

    public uint LastDirectorState { get; private set; }
    public int DirectorModSeq { get; private set; }

    public void DirectorEObjMod(uint state)
    {
        LastDirectorState = state;
        DirectorModSeq++;
        if (obj == null) return;
        var eo = (EventObject*)obj;
        var before = eo->SharedTimelineState;
        PacketDispatcher.HandleActorControlPacket(obj->EntityId, 106, state, 0, 0, 0, 0, 0, 0, 0, 0xE0000000, false);
        animCheckFrames = 0;
        if (MuteSound) Native.LayoutInstanceDiagnostics.SilenceSounds(eo->SharedGroupLayoutInstance);
        DiagnosticLog.Info(
            $"[SimEventObject] {DisplayName} DirectorEObjMod({state}) entity=0x{obj->EntityId:X} EventId=0x{(uint)obj->EventId:X}: "
            + $"SharedTimelineState 0x{before:X} -> 0x{eo->SharedTimelineState:X} -- SG: {LayoutInstanceDiagnostics.Describe(eo->SharedGroupLayoutInstance)}");
    }

    public void PlayBeat(uint state, uint bitmask, PropBeatMode mode)
    {
        LastAnimation = (state, bitmask);
        LastBeatMode = mode;
        AnimationSeq++;
        if (obj == null) return;
        CurrentState = (ushort)state;
        var eo = (EventObject*)obj;
        try
        {
            switch (mode)
            {
                case PropBeatMode.ActorControl:
                    // Category 413 through the client's own dispatcher.
                    PacketDispatcher.HandleActorControlPacket(obj->EntityId, 413, state, bitmask, 0, 0, 0, 0, 0, 0, 0xE0000000, false);
                    break;
                case PropBeatMode.SetSharedTimelineState:
                    // Diff-based: plays the SGB timelines mapped to whichever state bits change.
                    eo->SetSharedTimelineState((ushort)state, true, 0);
                    break;
                default:
                    eo->PlayAnimation(state, bitmask, 0);
                    break;
            }
            if (forceSharedGroupActive) EnsureSharedGroupActive("beat");
            animCheckFrames = 0;
        }
        catch (System.Exception e)
        {
            // A stale FFXIVClientStructs signature must not take down the framework thread.
            DiagnosticLog.Warn($"[SimEventObject.PlayAnimation] {DisplayName} {mode}(0x{state:X},0x{bitmask:X}) threw ({e.GetType().Name}); falling back to SetState.");
            EventObjectHelper.SetState(obj, (ushort)state);
        }
        var hidden = SpawnConfig is { HideAtState: > 0 } config && config.HideAtState == state
            && Native.LayoutInstanceDiagnostics.Deactivate(eo->SharedGroupLayoutInstance);
        // The SGB timeline turns the Sound children back on; mute them again right after.
        if (MuteSound) Native.LayoutInstanceDiagnostics.SilenceSounds(eo->SharedGroupLayoutInstance);
        DiagnosticLog.Info(
            $"[SimEventObject.PlayAnimation] {DisplayName} {mode}(0x{state:X},0x{bitmask:X}) entity=0x{obj->EntityId:X} -> "
            + $"SharedTimelineState=0x{eo->SharedTimelineState:X}{(hidden ? " (SharedGroup switched off)" : "")} -- SG: {LayoutInstanceDiagnostics.Describe(eo->SharedGroupLayoutInstance)}");
    }

    private void EnsureSharedGroupActive(string when)
    {
        if (obj == null) return;
        var sg = ((EventObject*)obj)->SharedGroupLayoutInstance;
        if (!Native.LayoutInstanceDiagnostics.ForceActive(sg)) return;
        if (MuteSound) Native.LayoutInstanceDiagnostics.SilenceSounds(sg);
        if (forceActiveLogged) return;
        forceActiveLogged = true;
        DiagnosticLog.Info($"[SimEventObject] {DisplayName} SharedGroup forced active ({when}): {LayoutInstanceDiagnostics.Describe(sg)}");
    }

    // Native load state at each checkpoint: the attached SharedGroup, the fields that gate
    // rendering, and whether an instance under LayoutId exists at all.
    private void LogLoadState(string label)
    {
        if (obj == null) { DiagnosticLog.Info($"[SimEventObject.LogLoadState] {DisplayName} {label}: actor gone."); return; }
        var eo = (EventObject*)obj;
        var sgDesc = LayoutInstanceDiagnostics.Describe(eo->SharedGroupLayoutInstance);
        var layoutNote = layoutId != 0 ? $" layoutIdInstance={(LayoutInstanceDiagnostics.Exists(layoutId) ? "found" : "none")}" : "";
        DiagnosticLog.Info(
            $"[SimEventObject.LogLoadState] {DisplayName} (LayoutId 0x{layoutId:X}) {label}: "
            + $"SharedTimelineState=0x{eo->SharedTimelineState:X} Flags=0x{eo->Flags:X} Arg=0x{eo->Arg:X} EventId=0x{(uint)obj->EventId:X} EntityId=0x{obj->EntityId:X} "
            + $"DrawObject=0x{(nint)obj->DrawObject:X} RenderFlags={obj->RenderFlags} IsReadyToDraw={obj->IsReadyToDraw()}{layoutNote} -- SG: {sgDesc}.");
    }

    public virtual void Tick(float deltaSeconds)
    {
        // Re-sync stored Position/Rotation from native — catches any
        // direct-struct writes between Ticks (engine doesn't move EObjs on
        // its own, but the parallel pattern with SimNpc keeps the contract
        // uniform across IPositioned implementers).
        if (obj == null)
        {
            return;
        }

        Position = coordinates.ToLocal(obj->Position);
        Rotation = obj->Rotation;

        // The SGB timeline re-arms the Sound children as it advances, not just on the beat.
        if (MuteSound)
            Native.LayoutInstanceDiagnostics.SilenceSounds(((EventObject*)obj)->SharedGroupLayoutInstance);
        if (forceSharedGroupActive)
            EnsureSharedGroupActive("tick");

        if (animCheckFrames >= 0)
        {
            animCheckFrames++;
            if (animCheckFrames == 30 || animCheckFrames == 90)
                DiagnosticLog.Info($"[SimEventObject.PlayAnimation] {DisplayName} +{animCheckFrames} frames: state=0x{((EventObject*)obj)->SharedTimelineState:X} -- SG: {LayoutInstanceDiagnostics.Describe(((EventObject*)obj)->SharedGroupLayoutInstance)}");
            if (animCheckFrames >= 90) animCheckFrames = -1;
        }

        if (!loadCheckDone)
        {
            loadCheckFrames++;
            if (loadCheckFrames == 1) LogLoadState("+1 frame");
            else if (loadCheckFrames == 5) LogLoadState("+5 frames");
            else if (loadCheckFrames == 210)
            {
                LogLoadState("+210 frames (~3.5s)");
                loadCheckDone = true;
            }
        }

        if (lifetime > 0)
        {
            lifetimeElapsed += deltaSeconds;

            if (lifetimeElapsed >= lifetime)
            {
                Despawn();
            }
        }
    }

    public void Despawn()
    {
        if (slot < 0) return;
        var releasedSlot = slot;

        slot = -1;
        obj = null;

        byte[] packet = [(byte)releasedSlot];

        fixed (byte* packetPtr = packet)
        {
            PacketDispatcherPointers.HandleDespawnObjectPacket(0, packetPtr);
        }

        DiagnosticLog.Info($"[SimEventObject] Despawned slot {releasedSlot}.");
    }
}
