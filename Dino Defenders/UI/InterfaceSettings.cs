using Dino_Defenders.Client;
using Dino_Defenders.Progression;

namespace Dino_Defenders.UI
{
    /// <summary>How this player wants the interface to read. Like their sound it belongs to them and not to a hero or a build, so it's kept beside the saves.</summary>
    public sealed class InterfaceSettings
    {
        private const string File = "interface.json";

        /// <summary>Whether Health, Mana and Barrier are shown over the hero as well, so that they can be followed without looking away from a fight.</summary>
        public bool PoolsOverHero { get; set; } = true;

        /// <summary>Whether what's coming is marked on the ground, by how rare what's throwing it is. What the least of them do is seldom worth a player's eye, and a crowd of it hides what is.</summary>
        public bool NormalMarks { get; set; }
        public bool MagicMarks { get; set; } = true;
        public bool RareMarks { get; set; } = true;
        public bool UniqueMarks { get; set; } = true;

        /// <summary>Whether what an enemy of this rarity does is marked. It's a method so that it isn't saved.</summary>
        public bool Marks(Rarity rarity) => rarity switch
        {
            Rarity.Normal => NormalMarks,
            Rarity.Magic => MagicMarks,
            Rarity.Rare => RareMarks,
            _ => UniqueMarks,
        };

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
