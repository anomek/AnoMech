using AnoMech.Core.Native;

namespace AnoMech.Scenarios.Umad;

// RSV (Resolved String Value) name table for UMAD/Kefka content. The game stores
// these action/status names as "_rsv_<id>_..." placeholders in the Excel sheets;
// the real text is normally delivered by the server only inside the duty. AnoMech
// runs inn-only, so we seed them ourselves at scenario start (see RsvFunctions and
// memory reference_rsv_action_names). Without this, casts like "Kefka Says" render
// with a blank name on the cast bar.
//
// Captured from the type-262 RSVData lines in logs/UMAD/. Every key fits the strict
// template below; each id carries two variants (_0_ / _1_) that always resolve to
// the same string, so we store one row per id and synthesize both keys at seed time.
public static class UmadRsvStrings
{
    private const string KeyFormat = "_rsv_{0}_-1_1_0_{1}_SE2DC5B04_EE2DC5B04";

    // Decimal action/status id -> resolved name (118 rows).
    private static readonly (uint Id, string Name)[] Names =
    [
        (47842, "Definition of Insanity"),
        (47843, "Ultima Blaster"),
        (47844, "Ultima Blaster"),
        (47845, "Max"),
        (47846, "Slap Happy"),
        (47847, "Slap Happy"),
        (47848, "Slap Happy"),
        (47849, "Slap Happy"),
        (47850, "Shocking Impact"),
        (47851, "Shockwave"),
        (47852, "Look upon Me and Despair"),
        (47853, "Look upon Me and Despair"),
        (47854, "Look upon Me and Despair"),
        (47855, "Stomp-a-Mole"),
        (47856, "Stomp-a-Mole"),
        (47857, "Unmitigated Impact"),
        (47858, "Bowels of Agony"),
        (47859, "Stray Flames"),
        (47860, "Inferno"),
        (47861, "Tsunami"),
        (47862, "Stray Spray"),
        (47863, "Stray Gusts"),
        (47864, "Cyclone"),
        (47865, "Stray Earth"),
        (47866, "Earthquake"),
        (47867, "Black Hole"),
        (47868, "Nothingness"),
        (47869, "Longitudinal Implosion"),
        (47870, "Latitudinal Implosion"),
        (47871, "Shockwave"),
        (47872, "Umbra Smash"),
        (47873, "Damning Edict"),
        (47874, "Knock Down"),
        (47875, "Knock Down"),
        (47877, "Big Bang"),
        (47878, "Big Bang"),
        (47881, "Thunder III"),
        (47883, "Fire III"),
        (47884, "Thunder III"),
        (47885, "Blizzard III"),
        (47886, "Fire III"),
        (47887, "Blizzard III"),
        (47888, "Fire III"),
        (47889, "Blizzard III"),
        (47890, "Thunder III"),
        (47891, "Vacuum Wave"),
        (47892, "Grand Cross"),
        (47893, "Death Bomb"),
        (47894, "Death Shriek"),
        (47895, "Death Shriek"),
        (47896, "Death Bolt"),
        (47897, "Death Bolt"),
        (47898, "Death Wave"),
        (47899, "Death Wave"),
        (47900, "Death Surge"),
        (47901, "Death Surge"),
        (47902, "Inferno"),
        (47903, "Tsunami"),
        (47904, "Inferno"),
        (47905, "Tsunami"),
        (47906, "Stray Flames"),
        (47907, "Stray Flames"),
        (47908, "Stray Spray"),
        (47909, "Stray Spray"),
        (47925, "Forsaken"),
        (47926, "Forsaken"),
        (47927, "Forsaken Ground"),
        (47928, "Forsaken"),
        (47929, "Forsaken Bonds"),
        (47930, "Forsaken Null"),
        (47931, "Stray Apocalypse"),
        (47932, "Stray Apocalypse"),
        (47933, "Stray Apocalypse"),
        (47934, "Stray Entropy"),
        (47935, "Stray Entropy"),
        (47936, "Ultima Repeater"),
        (47937, "Ultima Repeater"),
        (47938, "Celestriad"),
        (47939, "Fire III"),
        (47940, "Blizzard III"),
        (47941, "Thunder III"),
        (47942, "Stardust Fire III"),
        (47943, "Stardust Blizzard III"),
        (47944, "Stardust Thunder III"),
        (47946, "Quake"),
        (47947, "Tornado"),
        (47951, "Chaotic Flood"),
        (47952, "Maddening Orchestra"),
        (47953, "Maddening Orchestra"),
        (47954, "Flare"),
        (47955, "Chaotic Flare"),
        (47956, "Holy"),
        (47957, "Flare Diffusion"),
        (47958, "Chaotic Holy"),
        (48333, "Black Spark"),
        (48486, "White Hole"),
        (49742, "Catastrophic Choice"),
        (49743, "Catastrophic Choice"),
        (49752, "Meteor"),
        (49753, "Bowels of Agony"),
        (49769, "Flood"),
        (49878, "Trance"),
        (49884, "Kefka Says"),
        (49890, "the Decisive Battle"),
        (49891, "the Decisive Battle"),
        (49892, "Aetherlink"),
        (49893, "Aetherlink"),
        (50066, "Flood of Naught"),
        (50067, "Flood of Naught"),
        (50068, "White Antilight"),
        (50069, "Black Antilight"),
        (50070, "Edge of Death"),
        (50081, "Flood of Naught"),
        (50082, "Flood of Naught"),
        (50545, "Earthquake"),
        (50546, "Earthquake"),
        (50718, "Meteor"),
        (50719, "Bowels of Agony"),
    ];

    // Writes every UMAD action/status name into the game's RSV table. Called at the
    // top of each UMAD scenario's Run; cheap and idempotent, so no run-once guard.
    public static void Seed()
    {
        foreach (var (id, name) in Names)
        {
            RsvFunctions.Add(string.Format(KeyFormat, id, 0), name);
            RsvFunctions.Add(string.Format(KeyFormat, id, 1), name);
        }
    }
}
