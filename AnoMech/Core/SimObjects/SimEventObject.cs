using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
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
    public uint EntityId { get; init; } = 0;
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

    // For an EObj bound to a zone SharedGroup: the SG outlives the EObj, so without this a
    // restarted run finds it still in its last animated state. Pair with ForceSharedGroupActive.
    public bool RestoreStateOnDespawn { get; init; } = false;
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
public class SimEventObject : ISimObject, IPositioned
{
    private IEventObjectProxy? obj;
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
    public int Slot => obj?.Slot ?? -1;
    public uint EntityId => obj?.EntityId ?? 0;
    public GameObjectId GameObjectId => obj?.GameObjectId ?? default;

    public bool IsAlive => obj != null;

    // The SharedGroup attaches ~1s after the spawn; a beat before that only writes the static
    // state and runs no SGB timeline.
    public bool IsSharedGroupAttached => obj?.IsSharedGroupAttached ?? false;
    public bool IsSharedGroupTimelinePlaying => obj?.IsSharedGroupTimelinePlaying ?? false;
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

    protected SimEventObject(IEventObjectProxy obj, Coordinates coordinates, uint eObjRowId, ushort visibleState, float lifetime, uint layoutId, bool muteSound = false, bool forceSharedGroupActive = false)
    {
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
        if (Natives.EventObjects.Spawn(config, coordinates.ToGlobal(config.Placement)) is not { } spawned) return null;

        var eObj = new SimEventObject(spawned, coordinates, config.EObjId, config.TimelineState, config.Lifetime, config.LayoutId, config.MuteSound, config.ForceSharedGroupActive)
        {
            SpawnConfig = config,
        };

        if (!config.SpawnVisible && config.TimelineState != 0)
        {
            eObj.SetVisible(false);
        }

        DiagnosticLog.Info($"[SimEventObject.Create] Spawned EObj with EObjId 0x{config.EObjId:X} at Slot: {spawned.Slot} {spawned.Position}");
        return eObj;
    }

    public void SetPosition(Vector3 position)
    {
        Position = position;
        obj?.SetPosition(coordinates.ToGlobal(position));
    }

    public void SetPosition(Placement placement)
    {
        Position = placement.Position;
        Rotation = MathUtil.NormalizeRotation(placement.Rotation);
        if (obj == null) return;
        obj.SetPosition(coordinates.ToGlobal(placement.Position));
        obj.SetRotation(Rotation);
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
        obj.SetState(state);
    }

    // Convenience for parser-driven scenarios that emit SetVisible from
    // ACT 261|Change ModelStatus events. Flips between the configured
    // VisibleState and 0 (the engine default / "hidden" for gated SGs).
    public void SetVisible(bool visible) => SetState(visible ? visibleState : (ushort)0);

    public void UpdateSharedTimelineState(ushort oldState, ushort newState) => obj?.UpdateSharedTimelineState(oldState, newState);

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
        obj?.ActorControl(607, obj.EntityId, 1, 0, 100);
    }

    public uint LastDirectorState { get; private set; }
    public int DirectorModSeq { get; private set; }

    public void DirectorEObjMod(uint state)
    {
        LastDirectorState = state;
        DirectorModSeq++;
        if (obj == null) return;
        var before = obj.SharedTimelineState;
        obj.ActorControl(106, state);
        animCheckFrames = 0;
        if (MuteSound) obj.SilenceSharedGroupSounds();
        DiagnosticLog.Info(
            $"[SimEventObject] {DisplayName} DirectorEObjMod({state}) entity=0x{obj.EntityId:X}: "
            + $"SharedTimelineState 0x{before:X} -> 0x{obj.SharedTimelineState:X} -- SG: {obj.DescribeSharedGroup()}");
    }

    public void PlayBeat(uint state, uint bitmask, PropBeatMode mode)
    {
        LastAnimation = (state, bitmask);
        LastBeatMode = mode;
        AnimationSeq++;
        if (obj == null) return;
        CurrentState = (ushort)state;
        try
        {
            switch (mode)
            {
                case PropBeatMode.ActorControl:
                    obj.ActorControl(413, state, bitmask);
                    break;
                case PropBeatMode.SetSharedTimelineState:
                    obj.SetSharedTimelineState((ushort)state);
                    break;
                default:
                    obj.PlayAnimation(state, bitmask);
                    break;
            }
            if (forceSharedGroupActive) EnsureSharedGroupActive("beat");
            animCheckFrames = 0;
        }
        catch (System.Exception e)
        {
            // A stale FFXIVClientStructs signature must not take down the framework thread.
            DiagnosticLog.Warn($"[SimEventObject.PlayAnimation] {DisplayName} {mode}(0x{state:X},0x{bitmask:X}) threw ({e.GetType().Name}); falling back to SetState.");
            obj.SetState((ushort)state);
        }
        var hidden = SpawnConfig is { HideAtState: > 0 } config && config.HideAtState == state
            && obj.DeactivateSharedGroup();
        // The SGB timeline turns the Sound children back on; mute them again right after.
        if (MuteSound) obj.SilenceSharedGroupSounds();
        DiagnosticLog.Info(
            $"[SimEventObject.PlayAnimation] {DisplayName} {mode}(0x{state:X},0x{bitmask:X}) entity=0x{obj.EntityId:X} -> "
            + $"SharedTimelineState=0x{obj.SharedTimelineState:X}{(hidden ? " (SharedGroup switched off)" : "")} -- SG: {obj.DescribeSharedGroup()}");
    }

    private void EnsureSharedGroupActive(string when)
    {
        if (obj == null || !obj.ForceSharedGroupActive()) return;
        if (MuteSound) obj.SilenceSharedGroupSounds();
        if (forceActiveLogged) return;
        forceActiveLogged = true;
        DiagnosticLog.Info($"[SimEventObject] {DisplayName} SharedGroup forced active ({when}): {obj.DescribeSharedGroup()}");
    }

    // Native load state at each checkpoint: the attached SharedGroup, the fields that gate
    // rendering, and whether an instance under LayoutId exists at all.
    private void LogLoadState(string label)
    {
        if (obj == null) { DiagnosticLog.Info($"[SimEventObject.LogLoadState] {DisplayName} {label}: actor gone."); return; }
        DiagnosticLog.Info($"[SimEventObject.LogLoadState] {DisplayName} (LayoutId 0x{layoutId:X}) {label}: {obj.DescribeLoadState(layoutId)}.");
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

        Position = coordinates.ToLocal(obj.Position);
        Rotation = obj.Rotation;

        // The SGB timeline re-arms the Sound children as it advances, not just on the beat.
        if (MuteSound)
            obj.SilenceSharedGroupSounds();
        if (forceSharedGroupActive)
            EnsureSharedGroupActive("tick");

        if (animCheckFrames >= 0)
        {
            animCheckFrames++;
            if (animCheckFrames == 30 || animCheckFrames == 90)
                DiagnosticLog.Info($"[SimEventObject.PlayAnimation] {DisplayName} +{animCheckFrames} frames: state=0x{obj.SharedTimelineState:X} -- SG: {obj.DescribeSharedGroup()}");
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
        if (obj == null) return;
        if (SpawnConfig is { RestoreStateOnDespawn: true } config)
        {
            if (obj.SharedTimelineState != config.TimelineState)
                obj.UpdateSharedTimelineState(obj.SharedTimelineState, config.TimelineState);
            obj.DeactivateSharedGroup();
        }
        var released = obj;
        obj = null;
        released.Despawn();
        DiagnosticLog.Info($"[SimEventObject] Despawned slot {released.Slot}.");
    }
}
