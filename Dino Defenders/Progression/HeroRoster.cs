namespace Dino_Defenders.Progression
{
    /// <summary>One hero on this machine, as the main menu shows them.</summary>
    /// <param name="Path">Their save, which is all of them there is.</param>
    public sealed record RosterEntry(string Path, HeroState Hero);

    /// <summary>
    /// Everyone this player has made. Each hero is a save of their own in one folder, with all the world they were in, so nothing about one touches another. Whichever was played last comes first. The save from
    /// before there was more than one hero is moved in here as the first of them, and a hero who's deleted is only moved aside, as it can't be undone from the game and a slip of the hand shouldn't be final.
    /// </summary>
    public sealed class HeroRoster
    {
        public const int LongestName = 24;
        public const string FirstHero = "Wanderer";

        private readonly string _folder;
        private readonly string _oldSave;

        /// <param name="root">Where this player's saves are, which is only ever not their own for a lab.</param>
        public HeroRoster(string? root = null)
        {
            root ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DinoDefenders");
            _folder = Path.Combine(root, "heroes");
            _oldSave = Path.Combine(root, "save.json");
            Directory.CreateDirectory(_folder);
            MoveOldSave();
        }

        // Testers carry their saves from build to build. It's copied before it's anything else, and the old one only moved out of the way once the copy is there, so there's no moment at which it's nowhere.
        private void MoveOldSave()
        {
            if (!File.Exists(_oldSave)) return;
            try
            {
                string path = NewPath();
                File.Copy(_oldSave, path);

                string? previous = SaveGame.PathOverride;
                SaveGame.PathOverride = path;
                SaveGame? save = SaveGame.Load();
                if (save != null)
                {
                    if (string.IsNullOrWhiteSpace(save.Hero.Name)) save.Hero.Name = FirstHero;
                    save.Store();
                }
                SaveGame.PathOverride = previous;

                File.Move(_oldSave, _oldSave + ".before-heroes", overwrite: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Could not move the old save: {e.Message}");
            }
        }

        private string NewPath() => Path.Combine(_folder, $"{Guid.NewGuid():N}.json");

        /// <summary>Everyone there is, last played first. A save that can't be read is left where it is and not shown, rather than being lost to whatever happens next.</summary>
        public List<RosterEntry> List()
        {
            var entries = new List<RosterEntry>();
            string? previous = SaveGame.PathOverride;
            foreach (string path in Directory.GetFiles(_folder, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
            {
                SaveGame.PathOverride = path;
                if (SaveGame.Load() is SaveGame save) entries.Add(new RosterEntry(path, save.Hero));
            }
            SaveGame.PathOverride = previous;
            return entries;
        }

        /// <summary>What a name comes to, or nothing if there's nothing to it.</summary>
        public static string? Clean(string name)
        {
            string trimmed = name.Trim();
            if (trimmed.Length == 0) return null;
            return trimmed.Length > LongestName ? trimmed.Substring(0, LongestName).TrimEnd() : trimmed;
        }

        /// <summary>A new hero, who hasn't been anywhere yet. Says nothing if there's no name to give them.</summary>
        public RosterEntry? Create(string name)
        {
            if (Clean(name) is not string clean) return null;

            HeroState hero = HeroState.CreateNew();
            hero.Name = clean;

            string path = NewPath();
            string? previous = SaveGame.PathOverride;
            SaveGame.PathOverride = path;
            new SaveGame { Hero = hero, WorldSeed = Environment.TickCount }.Store();
            SaveGame.PathOverride = previous;
            return new RosterEntry(path, hero);
        }

        /// <summary>Takes a hero off the list, into a folder of its own beside it.</summary>
        public void Delete(RosterEntry entry)
        {
            string deleted = Path.Combine(_folder, "deleted");
            Directory.CreateDirectory(deleted);
            File.Move(entry.Path, Path.Combine(deleted, Path.GetFileName(entry.Path)), overwrite: true);
        }
    }
}
