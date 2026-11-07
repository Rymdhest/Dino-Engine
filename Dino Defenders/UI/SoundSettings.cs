using Dino_Engine.Sound.Content;
using Dino_Defenders.Client;
using Dino_Engine.Sound;

namespace Dino_Defenders.UI
{
    /// <summary>How loud this player wants things. It belongs to them and not to a hero or a build, so it's kept beside the save, not in the game's own folder.</summary>
    internal sealed class SoundSettings
    {
        public float Master { get; set; } = 0.85f;
        public float Effects { get; set; } = 1f;
        public float World { get; set; } = 1f;
        public float Interface { get; set; } = 1f;
        public float Music { get; set; } = 0.65f;
        public bool Headphones { get; set; }

        private const string File = "sound.json";

        private static SoundSettings? _current;

        public static SoundSettings Load() => _current ??= SettingsStore.Read<SoundSettings>(File) ?? new SoundSettings();

        public void Apply()
        {
            Audio.MasterVolume = Master;
            Audio.SetVolume(SoundBus.Foley, Effects);
            Audio.SetVolume(SoundBus.Combat, Effects);
            Audio.SetVolume(SoundBus.Voice, Effects);
            Audio.SetVolume(SoundBus.Ambience, World);
            Audio.SetVolume(SoundBus.Interface, Interface);
            Audio.SetVolume(SoundBus.Music, Music);
            if (Audio.Mixer != null) Audio.Mixer.Headphones = Headphones;
        }

        /// <summary>Makes a change, hears it at once and keeps it.</summary>
        public void Change(Action change)
        {
            change();
            Apply();
            SettingsStore.Write(File, this);
        }
    }
}
