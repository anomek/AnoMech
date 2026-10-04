namespace AnoMech.Core.Native.Interfaces;

// Scenario music in the Content BGM scene.
public interface IBgm
{
    // Idempotent for the track already playing.
    void Play(ushort bgmId, float secondsIn = 0f);
    void Sync(float secondsIn);

    // The fight's own change of track, from the new one's top, not a hand-back: Reset would let
    // the territory's music in. Nothing to do while no scenario track plays.
    void Switch(ushort bgmId);

    // The director's null track.
    void Silence();

    // Hands the scene back so the territory's own BGM resumes.
    void Reset();

    void Tick(float deltaSeconds);
    void LogPosition(string label);
}
