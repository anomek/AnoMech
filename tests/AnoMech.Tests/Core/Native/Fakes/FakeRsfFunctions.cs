using System.Security.Cryptography;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

// A record is named by a hash of its bytes, which are an obfuscated path.
internal sealed class FakeRsfFunctions(SimTimeline timeline) : IRsfFunctions
{
    public void Add(byte[] record) => timeline.Add("rsf", id: Convert.ToHexString(SHA256.HashData(record))[..16]);
}
