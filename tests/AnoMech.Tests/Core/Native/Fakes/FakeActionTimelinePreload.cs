using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

internal sealed class FakeActionTimelinePreload(SimTimeline timeline) : IActionTimelinePreload
{
    public void Preload(IEnumerable<(ushort Id, string Key)> rows, string tag)
    {
        foreach (var (id, key) in rows) timeline.Add("preload", id: id.ToString(), name: tag, detail: key);
    }
}
