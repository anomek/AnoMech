using System.Text;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

// Stands in for Dalamud's RsvResolver: once a key is seeded, sheet text reads resolve it.
internal sealed class FakeRsvFunctions(SimTimeline timeline) : IRsvFunctions
{
    private const int KeyLength = 0x30;

    private readonly Dictionary<string, string> resolved = new();

    public void Add(byte[] record)
    {
        var valueLength = BitConverter.ToInt32(record, 0);
        var key = Encoding.ASCII.GetString(record, 4, KeyLength).Split('\0')[0];
        resolved[key] = Encoding.UTF8.GetString(record, 4 + KeyLength, valueLength);
        timeline.Add("rsv", id: key);
    }

    public string Resolve(string text) => resolved.TryGetValue(text, out var value) ? value : text;
}
