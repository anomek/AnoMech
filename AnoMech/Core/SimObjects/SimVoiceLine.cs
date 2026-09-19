using FFXIVClientStructs.FFXIV.Client.Sound;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace AnoMech.Core.SimObjects;

// Own the native sound slot until scenario teardown, so reset can stop a line
// without retaining a pointer to a slot the sound manager has already reused.
public sealed unsafe class SimVoiceLine : ISimObject
{
    private SoundData* sound;
    public bool IsActive => sound != null;

    internal SimVoiceLine(uint voiceId)
    {
        var manager = SoundManager.Instance();
        var framework = Framework.Instance();
        if (manager == null || framework == null) return;
        var option = framework->SystemConfig.GetConfigOption((uint)ConfigOption.CutsceneMovieVoice);
        var language = (option == null ? 1u : option->Value.UInt) switch
        {
            0 => "ja", 2 => "de", 3 => "fr", _ => "en"
        };
        var path = $"sound/voice/vo_line/{voiceId:D7}_{language}.scd";
        if (!Plugin.DataManager.FileExists(path)) return;
        // The SCD supplies the voice bus. Bypass character-distance/effect
        // rules for this raid callout; native voice/master volume still applies.
        sound = manager->PlaySound(path, volume: 1, fadeInDuration: 0,
            posX: 0, posY: 0, posZ: 0, speed: 1, a9: 0, soundNumber: 0,
            autoRelease: false, volumeCategory: SoundVolumeCategory.BypassVolumeRules,
            a13: false, midiNote: -1, a15: false, defaultFadeOut: false,
            isPositional: false, a18: false);
    }

    public void Tick(float deltaSeconds) { }

    public void Despawn()
    {
        if (sound == null) return;
        sound->Stop(0);
        // Let the native sound manager finish any pending load and reclaim its
        // own resource safely. The simulator never touches this slot again.
        sound->IsAutoReleaseEnabled = true;
        sound = null;
    }
}
