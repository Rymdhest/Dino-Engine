using Dino_Defenders.Client;

namespace Dino_Defenders.UI
{
    /// <summary>How this player wants the interface to read. Like their sound it belongs to them and not to a hero or a build, so it's kept beside the saves.</summary>
    internal sealed class InterfaceSettings
    {
        private const string File = "interface.json";

        /// <summary>Whether Health, Mana and Barrier are shown over the hero as well, so that they can be followed without looking away from a fight.</summary>
        public bool PoolsOverHero { get; set; } = true;

        private static InterfaceSettings? _current;

        public static InterfaceSettings Load() => _current ??= SettingsStore.Read<InterfaceSettings>(File) ?? new InterfaceSettings();

        /// <summary>Makes a change and keeps it. These are read as they're drawn, so there's nothing to apply.</summary>
        public void Change(Action change)
        {
            change();
            SettingsStore.Write(File, this);
        }
    }
}
