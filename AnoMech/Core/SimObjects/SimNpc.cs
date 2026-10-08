using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AnoMech.Core.SimObjects;

// SimCharacter backed by a BattleChara we allocated in a CharacterManager slot. Overlay state
// (VFX, statuses) lives on the base; this layer adds movement and the "free the slot on
// Despawn" lifecycle.
public class SimNpc : SimCharacter
{
    private IBattleCharaProxy? proxy;
    private bool pendingDraw;
    private int pendingDrawFrames;

    internal override IBattleCharaProxy? Proxy => proxy;

    // Forget the slot without touching what it holds: a packet spawn whose slot ended up with
    // somebody else's actor.
    protected void DetachSlot()
    {
        proxy = null;
        pendingDraw = false;
    }

    // Re-arm the deferred EnableDraw for an actor created without one (see
    // EnemySpawnConfig.PacketSpawnEnableDraw).
    protected void RequestDraw()
    {
        pendingDraw = proxy != null;
        pendingDrawFrames = 0;
    }

    private protected override Movement Movement => field ??= new Movement(this);

    // pendingDraw=false for an actor the engine's own spawn handler created: it enables the
    // draw itself, as for a real server spawn.
    protected SimNpc(IBattleCharaProxy proxy, Coordinates coordinates, bool pendingDraw = true) : base(coordinates)
    {
        this.proxy = proxy;
        this.pendingDraw = pendingDraw;
    }

    public void SetModelState(byte value) => ActorControl.SetModelState(value);

    // Sampled for peers.
    public byte ModelState => proxy?.ModelState ?? 0;

    // Which sub-meshes show, e.g. Omega-M's shield.
    public void SetModeAttributeFlags(byte value) => ActorControl.SetModeAttributeFlags(value);

    // Forces a model rebuild via DisableDraw -> EnableDraw, which retries a failed slot load
    // and re-reads ModeAttributeFlags. The re-enable is deferred through the
    // pendingDraw path (the rebuild is async, gated on IsReadyToDraw). Only cycles draw when
    // the model is currently drawn: a hidden NPC keeps the written flags and applies them on
    // its next EnableDraw from the visibility system, so we never force it visible.
    protected void ReloadModel()
    {
        if (proxy is not { HasDrawObject: true } chara) return;
        chara.DisableDraw();
        pendingDraw = true;
    }

    // Plays an action's own animation and VFX on this doppel through the same synthetic
    // ActionEffect a boss cast fires -- what lets a bot tank visibly pop its LB3 in Umad P3
    // Limit Cut. Effects (statuses, damage) stay the caller's job, exactly as for an enemy Cast.
    //
    // Here rather than on SimPartyNpc because the same seat is a SimNetworkPuppet on every peer,
    // which has to play it too; SimEnemy inherits it but drives its own richer cast instead.
    private SimCast? actionCast;

    // Sampled for peers, same edge trigger as SimCast.EffectSeq.
    public uint PlayedActionId { get; private set; }
    public float PlayedActionAnimationLock { get; private set; }
    public int PlayedActionSeq { get; private set; }

    public void PlayAction(uint actionId, float animationLock = 0.6f)
    {
        PlayedActionId = actionId;
        PlayedActionAnimationLock = animationLock;
        PlayedActionSeq++;
        actionCast ??= new SimCast(this, Coordinates);
        actionCast.NativeActionEffect(actionId, animationLock, (ushort)actionId, 0, ActionType.Action, 0,
            animationTargetId: GameObjectId, actionTargetId: GameObjectId);
    }

    public override void Tick(float deltaSeconds)
    {
        base.Tick(deltaSeconds);

        if (pendingDraw)
        {
            if (proxy is not { Exists: true } chara)
            {
                pendingDraw = false;
            }
            else if (chara.IsReadyToDraw)
            {
                chara.EnableDraw();
                pendingDraw = false;
                DiagnosticLog.Info($"[SimNpc] EnableDraw fired for goid 0x{chara.GameObjectId.ObjectId:X} at pos {Position}.");
            }
            else
            {
                pendingDrawFrames++;
                // Once, well past a normal model load, for an IsReadyToDraw stuck false.
                if (pendingDrawFrames == 300)
                    DiagnosticLog.Warn($"[SimNpc] still pendingDraw after {pendingDrawFrames} ticks, goid 0x{chara.GameObjectId.ObjectId:X} -- IsReadyToDraw() never returned true.");
            }
        }
    }

    public override void Despawn()
    {
        if (proxy == null) return;
        base.Despawn();
        proxy.Despawn();
        proxy = null;
        pendingDraw = false;
    }
}
