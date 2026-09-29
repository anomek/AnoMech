using AnoMech.Core.Native;
using AnoMech.Pointers;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;
using NUnit.Framework;

[TestFixture]
[NonParallelizable]
public unsafe class StaticVfxTriggerTests
{
    private VfxDataPointers.QueueResourceTriggerDelegate previousQueueTrigger = null!;
    private int calls;
    private nint actualResource;
    private uint actualNumber;

    [SetUp]
    public void SetUp()
    {
        previousQueueTrigger = VfxDataPointers.QueueResourceTrigger;
        calls = 0;
        actualResource = 0;
        actualNumber = 0;
        VfxDataPointers.QueueResourceTrigger = (resource, number) =>
        {
            calls++;
            actualResource = (nint)resource;
            actualNumber = number;
        };
    }

    [TearDown]
    public void TearDown()
    {
        VfxDataPointers.QueueResourceTrigger = previousQueueTrigger;
    }

    [Test]
    public void NullSceneDoesNotCallNativeQueue()
    {
        Assert.That(StaticVfxTrigger.TryQueue(null, 1), Is.False);
        Assert.That(calls, Is.Zero);
    }

    [Test]
    public void MissingResourceDefersActivationWithoutCallingNativeQueue()
    {
        VfxObject scene = default;

        Assert.That(StaticVfxTrigger.TryQueue(&scene, 1), Is.False);
        Assert.That(calls, Is.Zero);
    }

    [Test]
    public void AllocatedResourceQueuesTheOneBasedTriggerWhileLoading(
        [Values(0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u, 8u, 9u, 10u, 11u)] uint triggerIndex)
    {
        VfxResourceInstance resource = default;
        VfxObject scene = default;
        scene.VfxResourceInstance = &resource;

        var queued = StaticVfxTrigger.TryQueue(&scene, triggerIndex);

        Assert.That(queued, Is.True);
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(actualResource, Is.EqualTo((nint)(&resource)), "Pass the resource instance to native code");
        Assert.That(actualResource, Is.Not.EqualTo((nint)(&scene)), "Never pass the scene object to native code");
        Assert.That(actualNumber, Is.EqualTo(triggerIndex + 1), "Native queue numbers are one-based");
    }

    [TestCase(12u)]
    [TestCase(uint.MaxValue)]
    public void InvalidTriggerIndexThrowsWithoutCallingNativeQueue(uint triggerIndex)
    {
        VfxResourceInstance resource = default;
        VfxObject scene = default;
        scene.VfxResourceInstance = &resource;
        var sceneAddress = (nint)(&scene);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => StaticVfxTrigger.TryQueue((VfxObject*)sceneAddress, triggerIndex));
        Assert.That(calls, Is.Zero);
    }
}

// Test doubles for the native boundary, not a simulation of the game renderer.
namespace FFXIVClientStructs.FFXIV.Client.Graphics.Vfx
{
    public struct VfxResourceInstance { public nint VfxResourceObject; }
}

namespace FFXIVClientStructs.FFXIV.Client.Graphics.Scene
{
    public unsafe struct VfxObject { public VfxResourceInstance* VfxResourceInstance; }
}

namespace AnoMech.Pointers
{
    internal static unsafe class VfxDataPointers
    {
        public delegate void QueueResourceTriggerDelegate(VfxResourceInstance* resource, uint triggerNumber);
        public static QueueResourceTriggerDelegate QueueResourceTrigger = null!;
    }
}
