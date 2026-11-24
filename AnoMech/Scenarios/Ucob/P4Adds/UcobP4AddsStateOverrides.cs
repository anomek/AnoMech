using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Ucob.P4Adds;

// Null leaves the field randomized at scenario start. SecondQuote falls back to one of the
// first quote's observed follow-ups rather than a free roll.
public sealed class UcobP4AddsStateOverrides
{
    public NaelQuote? FirstQuote { get; set; }
    public NaelQuote? SecondQuote { get; set; }
    public PartyRole? FirstHatchUntargeted { get; set; }
    public PartyRole? SecondHatchUntargeted { get; set; }
    public bool SimplifiedQuotes { get; set; }
}
