using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Group;
using UltiSim.Core.Map;

namespace UltiSim.Core.SimObjects;

// Acquires an existing SharedGroup ILayoutInstance baked into the active
// zone's LGB layout and drives its state through scenario events. Restores
// the snapshot on Despawn. Never allocates — the engine owns the instance.
//
// EObj scenery (e.g. TOP arena tiles, the 1EA1A1 fixture, the Exit portal)
// is loaded by the engine at zone-init as SharedGroupLayoutInstance under
// LayoutWorld.ActiveLayout. Substituting a global ModelChara won't match
// the canonical duty-scoped .sgb assets — looking up the engine's existing
// instance does. See LayoutQuery for the discovery side.
public abstract record SharedGroupDescriptor
{
    public sealed record BySgbPath(string SgbPath) : SharedGroupDescriptor;
    public sealed record ByEObjRow(uint EObjRowId) : SharedGroupDescriptor;
    public sealed record ByPosition(Vector3 World, float Radius = 1.0f, string? PathContains = null) : SharedGroupDescriptor;
}

// Placement on the descriptor's ByPosition is in scenario-space (relative to
// SimWorld.ScenarioOrigin). BySgbPath and ByEObjRow are zone-global.
public record struct EventObjectAcquireConfig(
    SharedGroupDescriptor Descriptor,
    bool IsVisible = true,
    float Lifetime = 0f);

public sealed unsafe class SimEventObject : ISimObject, IPositioned
{
    public nint InstancePtr { get; private set; }
    public string SgbPath { get; }
    public string DisplayName => SgbPath;

    private readonly Transform initialTransform;
    private readonly bool initialActive;
    private bool alive = true;

    public bool IsAlive => alive && InstancePtr != 0;

    public Vector3 Position
    {
        get
        {
            var sg = (SharedGroupLayoutInstance*)InstancePtr;
            return sg == null ? default : sg->Transform.Translation;
        }
    }

    public float Rotation
    {
        get
        {
            var sg = (SharedGroupLayoutInstance*)InstancePtr;
            if (sg == null) return 0f;
            var q = sg->Transform.Rotation;
            // Yaw from quaternion (Y-up). Same convention SimNpc uses for nameplate rotation.
            return MathF.Atan2(2f * (q.W * q.Y + q.X * q.Z), 1f - 2f * (q.Y * q.Y + q.X * q.X));
        }
    }

    private SimEventObject(nint instance, string sgbPath, Transform snapshotTransform, bool snapshotActive)
    {
        InstancePtr = instance;
        SgbPath = sgbPath;
        initialTransform = snapshotTransform;
        initialActive = snapshotActive;
    }

    internal static SimEventObject? Acquire(EventObjectAcquireConfig config, Vector3 origin, EventScheduler events)
    {
        SharedGroupLayoutInstance* sg = config.Descriptor switch
        {
            SharedGroupDescriptor.BySgbPath p => LayoutQuery.FindBySgbPath(p.SgbPath),
            SharedGroupDescriptor.ByEObjRow e => LayoutQuery.FindByEObjRow(e.EObjRowId),
            SharedGroupDescriptor.ByPosition pos => LayoutQuery.FindByPosition(
                new Vector3(origin.X + pos.World.X, origin.Y + pos.World.Y, origin.Z + pos.World.Z),
                pos.Radius, pos.PathContains),
            _ => null,
        };

        if (sg == null)
        {
            Plugin.Log.Warning($"SimEventObject: could not resolve descriptor {config.Descriptor}");
            return null;
        }

        var path = LayoutQuery.GetSgbPath(sg) ?? "(unknown)";
        var inst = (ILayoutInstance*)sg;
        var snapTransform = sg->Transform;
        var snapActive = inst->IsActive;

        var eo = new SimEventObject((nint)sg, path, snapTransform, snapActive);
        eo.SetVisible(config.IsVisible);

        if (config.Lifetime > 0f) events.Add(config.Lifetime, eo.Despawn);

        Plugin.Log.Info($"SimEventObject: acquired '{path}' at ({snapTransform.Translation.X:F2},{snapTransform.Translation.Y:F2},{snapTransform.Translation.Z:F2})");
        return eo;
    }

    // ILayoutInstance.SetActive (vfunc 63) toggles render visibility for
    // SharedGroup instances. Same pattern as SetColliderActive (vfunc 37) used
    // for the spawn-area barrier drop, but on the render path.
    public void SetVisible(bool visible)
    {
        var sg = (SharedGroupLayoutInstance*)InstancePtr;
        if (sg == null) return;
        ((ILayoutInstance*)sg)->SetActive(visible);
    }

    // ILayoutInstance.SetTransform (vfunc 18) writes the full SRT to the
    // instance; engine handles the dirty propagation to graphics + collider.
    public void SetPosition(Vector3 position)
    {
        var sg = (SharedGroupLayoutInstance*)InstancePtr;
        if (sg == null) return;
        var t = sg->Transform;
        t.Translation = position;
        ((ILayoutInstance*)sg)->SetTransform(&t);
    }

    public void SetPosition(Placement placement)
    {
        var sg = (SharedGroupLayoutInstance*)InstancePtr;
        if (sg == null) return;
        var t = sg->Transform;
        t.Translation = placement.Position;
        t.Rotation = Quaternion.CreateFromYawPitchRoll(MathUtil.NormalizeRotation(placement.Rotation), 0f, 0f);
        ((ILayoutInstance*)sg)->SetTransform(&t);
    }

    public void Tick(float deltaSeconds)
    {
        // Engine owns the instance's per-frame update. Nothing to do.
    }

    public void Despawn()
    {
        if (!alive) return;
        alive = false;
        var sg = (SharedGroupLayoutInstance*)InstancePtr;
        if (sg != null)
        {
            // Restore the snapshot the engine had before we touched it. We do
            // not free the instance — the engine owns its lifetime.
            var inst = (ILayoutInstance*)sg;
            var t = initialTransform;
            inst->SetTransform(&t);
            inst->SetActive(initialActive);
        }
        InstancePtr = 0;
    }
}
