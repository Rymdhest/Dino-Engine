using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dino_Defenders.Progression
{
    /// <summary>
    /// What's put away, shared by every hero on this machine: tabs of fixed cells with gaps in them, like the pack, where nothing moves unless the player moves it. Both ends of a connection run these same
    /// rules, as the pack's are run: a client changes it at once and the server has the last word (see NETWORKING.md). A stash is the player's own, as their hero is, so a friend's server only ever holds
    /// a copy for as long as they're there, and it's the player's own machine that keeps it. Read LANDS.md.
    /// </summary>
    public sealed class Stash
    {
        public const int TabCount = 4;
        public const int Columns = 6;
        public const int TabSize = 48;

        public List<Item?[]> Tabs { get; set; } = new();

        public Stash()
        {
            Sanitize();
        }

        public bool InRange(int tab, int cell) => tab >= 0 && tab < Tabs.Count && cell >= 0 && cell < Tabs[tab].Length;

        public Item? At(int tab, int cell) => InRange(tab, cell) ? Tabs[tab][cell] : null;

        /// <summary>Everything in it, for what wants to look through it all.</summary>
        public IEnumerable<Item> Kept() => Tabs.SelectMany(tab => tab).OfType<Item>();

        /// <summary>
        /// Puts something from the hero into a cell. Whatever's there goes where it came from, which only a cell of the pack can take, as nothing is worn or held that wasn't chosen for its place. Says
        /// whether it could, and changes nothing if not.
        /// </summary>
        public bool Stow(HeroState hero, ItemPlace from, int tab, int cell)
        {
            if (!InRange(tab, cell) || from.IsHeld || hero.At(from) is not Item item) return false;
            Item? there = Tabs[tab][cell];
            if (there != null && !from.IsCell) return false;

            hero.Take(from);
            if (there != null) hero.Inventory[from.Number] = there;
            Tabs[tab][cell] = item;
            return true;
        }

        /// <summary>Takes something out into a cell of the pack: the one asked for, whatever's in it changing places with it, or else the first that's free. Says whether there was room.</summary>
        public bool Take(HeroState hero, int tab, int cell, int? into = null)
        {
            if (At(tab, cell) is not Item item) return false;
            if (into is int asked && ItemPlace.Cell(asked).IsCell)
            {
                Tabs[tab][cell] = hero.Inventory[asked];
                hero.Inventory[asked] = item;
                return true;
            }
            if (!hero.Pickup(item)) return false;
            Tabs[tab][cell] = null;
            return true;
        }

        /// <summary>Moves something from one cell of the stash to another, and whatever's there goes the other way.</summary>
        public bool Shift(int tab, int cell, int toTab, int toCell)
        {
            if (!InRange(tab, cell) || !InRange(toTab, toCell) || (tab == toTab && cell == toCell) || Tabs[tab][cell] == null) return false;
            (Tabs[tab][cell], Tabs[toTab][toCell]) = (Tabs[toTab][toCell], Tabs[tab][cell]);
            return true;
        }

        /// <summary>The first free cell of a tab, or -1.</summary>
        public int FreeCell(int tab) => tab >= 0 && tab < Tabs.Count ? Array.IndexOf(Tabs[tab], null) : -1;

        /// <summary>Whatever a file or a stranger says, it comes out the right shape, with everything in it brought up to the rules. Nothing that was in it is lost: what's past the end goes where there's room.</summary>
        public void Sanitize()
        {
            var spill = new List<Item>();
            var tabs = new List<Item?[]>(TabCount);
            for (int t = 0; t < TabCount; t++)
            {
                var tab = new Item?[TabSize];
                Item?[]? old = t < Tabs.Count ? Tabs[t] : null;
                for (int i = 0; old != null && i < old.Length; i++)
                {
                    if (old[i] is not Item item) continue;
                    if (i < TabSize) tab[i] = item;
                    else spill.Add(item);
                }
                tabs.Add(tab);
            }
            for (int t = TabCount; t < Tabs.Count; t++) spill.AddRange(Tabs[t]?.OfType<Item>() ?? Enumerable.Empty<Item>());

            foreach (Item item in spill)
            {
                foreach (Item?[] tab in tabs)
                {
                    int free = Array.IndexOf(tab, null);
                    if (free < 0) continue;
                    tab[free] = item;
                    break;
                }
            }
            Tabs = tabs;
        }

        public Stash Copy() => new Stash { Tabs = Tabs.Select(tab => (Item?[])tab.Clone()).ToList() };
    }

    /// <summary>
    /// Where the stash is kept: one file beside the heroes, written aside and swapped in as a save is. Only one copy of the game can have it at once, as two writing the same file would lose or double
    /// what's in it: the first takes it, and any other is told it's in use.
    /// </summary>
    public static class StashStore
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        private static FileStream? _held;

        /// <summary>Where it's kept, or nothing for a scripted run, whose stash lasts as long as it does and is nobody's.</summary>
        public static string? Path { get; private set; }

        /// <summary>Whether this copy of the game has it. Another copy already running keeps it to itself.</summary>
        public static bool Held => _held != null || Path == null;

        /// <summary>Takes the stash at a path for this copy of the game, if no other copy has it.</summary>
        public static void Claim(string? path)
        {
            if (path == Path && _held != null) return;
            Release();
            Path = path;
            if (path == null) return;
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                _held = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                _held = null;
            }
        }

        public static void Release()
        {
            _held?.Dispose();
            _held = null;
        }

        /// <summary>What's in it, or an empty one if there's nothing yet, it can't be read, or another copy of the game has it.</summary>
        public static Stash Load()
        {
            if (Path == null || _held == null || !File.Exists(Path)) return new Stash();
            try
            {
                Stash stash = JsonSerializer.Deserialize<Stash>(File.ReadAllText(Path), Options) ?? new Stash();
                stash.Sanitize();
                foreach (Item item in stash.Kept()) SaveGame.FixItem(item);
                return stash;
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // One that can't be read is kept aside rather than written over, as everything anyone put away is in it.
                Console.WriteLine($"Could not read the stash: {e.Message}");
                try
                {
                    File.Copy(Path, $"{Path}.unread-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: false);
                }
                catch (IOException)
                {
                }
                return new Stash();
            }
        }

        public static void Store(Stash stash)
        {
            if (Path == null || _held == null) return;
            try
            {
                string temporary = Path + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(stash, Options));
                File.Move(temporary, Path, overwrite: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Could not write the stash: {e.Message}");
            }
        }
    }
}
