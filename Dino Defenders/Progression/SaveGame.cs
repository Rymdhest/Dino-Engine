using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Dino_Defenders.Progression
{
    /// <summary>What has happened at one site. Sites that nothing has happened at aren't kept.</summary>
    public sealed class SiteRecord
    {
        public int Index { get; set; }
        public bool Discovered { get; set; }
        public bool Cleared { get; set; }
        public bool Kindled { get; set; }
        public bool Rewarded { get; set; }
        public List<int> Dead { get; set; } = new();
        public List<int> Opened { get; set; } = new();
    }

    /// <summary>What survives closing the game: the hero, and where they are.</summary>
    public sealed class SaveGame
    {
        /// <summary>Goes up when what's saved changes shape. <see cref="Upgrade"/> brings older saves up to it: testers carry theirs from build to build.</summary>
        public const int CurrentVersion = 8;

        /// <summary>The version that renamed the places on a hero, which needs a copy of the old save kept and its slots rewritten.</summary>
        private const int SlotsVersion = 4;

        /// <summary>The version that replaced skills with books, which needs a copy of the old save kept. Nothing a tree held means anything now, so everyone starts it again.</summary>
        private const int BooksVersion = 6;

        /// <summary>The version that made pages and stickers items of their own, which needs a copy of the old save kept. What was written stays written, each book with room for exactly what was in it.</summary>
        private const int StickersVersion = 7;

        /// <summary>The version that left four rarities with prefixes and suffixes of their own, which needs a copy of the old save kept. Most of what was rolled under the old rules comes out of it with a line or two changed.</summary>
        private const int AffixVersion = 8;

        public int Version { get; set; } = CurrentVersion;
        public HeroState Hero { get; set; } = HeroState.CreateNew();
        public int WorldSeed { get; set; }
        public string? WorldArchetype { get; set; }
        public int WorldLevel { get; set; } = 1;

        /// <summary>What was paid to make that world what it is. It's part of which world it was, as anything saved by number is only right for the same one.</summary>
        public Offering Offering { get; set; } = new();
        public int OfferingFalls { get; set; }
        public bool OfferingBroken { get; set; }
        public List<SiteRecord> Sites { get; set; } = new();
        public List<int> Objectives { get; set; } = new();
        public float PlayerX { get; set; } = -1f;
        public float PlayerZ { get; set; } = -1f;
        public float Health { get; set; } = -1f;
        public float RespawnX { get; set; } = -1f;
        public float RespawnZ { get; set; } = -1f;

        /// <summary>Whether this is a hero who hasn't set foot anywhere yet, and has a land still to be grown. It's a method so that it isn't saved.</summary>
        public bool NeverPlayed() => Sites.Count == 0 && PlayerX < 0f;

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        /// <summary>Set to keep the save somewhere other than the player's own, for a scripted run.</summary>
        public static string? PathOverride;

        private static string SavePath
        {
            get
            {
                if (PathOverride != null) return PathOverride;
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DinoDefenders");
                Directory.CreateDirectory(folder);
                return Path.Combine(folder, "save.json");
            }
        }

        public static SaveGame? Load()
        {
            try
            {
                string path = SavePath;
                if (!File.Exists(path)) return null;

                string json = File.ReadAllText(path);
                SaveGame? save = Read(json);

                // Bringing a save up to date can't be undone, and the first thing that happens next is that it gets written over.
                if (save != null && !json.Contains("\"Version\"")) KeepOldCopy(path, ".before-talents");
                else if (save != null && VersionOf(json) < SlotsVersion) KeepOldCopy(path, ".before-gear");
                else if (save != null && VersionOf(json) < BooksVersion) KeepOldCopy(path, ".before-books");
                else if (save != null && VersionOf(json) < StickersVersion) KeepOldCopy(path, ".before-stickers");
                else if (save != null && VersionOf(json) < AffixVersion) KeepOldCopy(path, ".before-affixes");
                return save;
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // A damaged save shouldn't stop the game from starting; it starts a new journey instead.
                Console.WriteLine($"Could not read the save: {e.Message}");
                return null;
            }
        }

        // The first copy is the one worth having, and a backup put back by hand finds it already there. Failing here must not count as
        // failing to read the save, or the hero it was meant to protect is written over.
        private static void KeepOldCopy(string path, string suffix)
        {
            try
            {
                string copy = path + suffix;
                if (!File.Exists(copy)) File.Copy(path, copy);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Could not keep a copy of the old save: {e.Message}");
            }
        }

        /// <summary>A save from its text, whatever version it's from.</summary>
        public static SaveGame? Read(string json)
        {
            if (JsonNode.Parse(json) is not JsonObject root) return null;
            int version = root["Version"]?.GetValue<int>() ?? 1;
            if (version < 2) Upgrade(root);

            // Has to be before anything is read: an enum name that's gone throws, and a save that can't be read starts a new journey.
            if (version < SlotsVersion) RenameSlots(root);
            if (version < BooksVersion) ForgetSkills(root);
            if (version < AffixVersion) RenameRarities(root["Hero"]);

            SaveGame? save = root.Deserialize<SaveGame>(Options);
            if (save == null) return null;
            save.Version = CurrentVersion;
            FixBar(save.Hero);
            FixPack(save.Hero);
            FixBooks(save.Hero);
            if (version < BooksVersion) save.Hero.Bar[0] ??= ItemGenerator.Book(ItemGenerator.NewSeed(new Dino_Engine.World.WorldRandom((ulong)Random.Shared.NextInt64())));
            TalentTree.Sanitize(save.Hero);
            save.Offering.Sanitize();
            return save;
        }

        // A bar used to be a list of skills, and the tree was made of them. What's carried comes through it all, and the talents are all there to be spent again.
        private static void ForgetSkills(JsonObject root)
        {
            if (root["Hero"] is not JsonObject hero) return;
            hero.Remove("Bar");
            hero["Talents"] = new JsonArray();
        }

        // Everything carried, worn or held inside something else says what it is by name, and two of those names have gone. Anything already called something else is left alone.
        private static void RenameRarities(JsonNode? node)
        {
            if (node is JsonArray list)
            {
                foreach (JsonNode? held in list) RenameRarities(held);
            }
            if (node is not JsonObject item) return;

            if (item["Rarity"] is JsonValue rarity && rarity.TryGetValue(out string? name)) item["Rarity"] = NewRarity(name);
            foreach (KeyValuePair<string, JsonNode?> pair in item) RenameRarities(pair.Value);
        }

        private static string NewRarity(string old) => old switch
        {
            "Common" => nameof(Rarity.Normal),
            "Legendary" => nameof(Rarity.Unique),
            _ => old,
        };

        private static int VersionOf(string json)
        {
            return JsonNode.Parse(json) is JsonObject root ? root["Version"]?.GetValue<int>() ?? 1 : 1;
        }

        // Before there were ten places on a hero, what an item was worn as and where it was worn were the same word, and some of those words have gone. Any place that's already
        // called something else is left alone, so doing this twice does nothing.
        private static void RenameSlots(JsonObject root)
        {
            if (root["Hero"] is not JsonObject hero) return;
            if (hero["Inventory"] is JsonArray carried)
            {
                foreach (JsonNode? item in carried) RenameSlot(item as JsonObject);
            }
            if (hero["Equipped"] is not JsonObject worn) return;

            var renamed = new JsonObject();
            foreach (string place in worn.Select(pair => pair.Key).ToList())
            {
                JsonNode? item = worn[place];
                worn.Remove(place);
                RenameSlot(item as JsonObject);
                renamed[NewPlace(place)] = item;
            }
            hero["Equipped"] = renamed;
        }

        private static void RenameSlot(JsonObject? item)
        {
            if (item?["Slot"] is JsonValue slot && slot.TryGetValue(out string? name)) item["Slot"] = NewSlot(name);
        }

        private static string NewSlot(string old) => old switch
        {
            "Weapon" => nameof(ItemSlot.MainHand),
            "Shield" => nameof(ItemSlot.OffHand),
            "Ring" => nameof(ItemSlot.Finger),
            "Amulet" => nameof(ItemSlot.Neck),
            _ => old,
        };

        private static string NewPlace(string old) => old == "Ring" ? nameof(WornSlot.Finger1) : NewSlot(old);

        // A bar of the wrong length, from a hand or an older build, shouldn't be anyone's problem later on.
        private static void FixBar(HeroState hero)
        {
            if (hero.Bar?.Length == HeroState.BarSlots) return;
            var bar = new Item?[HeroState.BarSlots];
            if (hero.Bar != null) Array.Copy(hero.Bar, bar, Math.Min(hero.Bar.Length, bar.Length));
            hero.Bar = bar;
        }

        // Anything written can have had a sentence taken away since, and only a book goes on the bar.
        private static void FixBooks(HeroState hero)
        {
            for (int slot = 0; slot < hero.Bar.Length; slot++)
            {
                if (hero.Bar[slot] is { IsBook: false }) hero.Bar[slot] = null;
                if (hero.Bar[slot] is Item book) Fix(book);
            }
            foreach (Item item in hero.Carried()) Fix(item);
            foreach (Item item in hero.Equipped.Values) Fix(item);
        }

        // What it is says what it can hold, whatever the file says, and anything from before pages were items comes out as the items it would have been.
        private static void Fix(Item item)
        {
            if (item.Book is Book old)
            {
                foreach (Page page in old.Pages ?? new List<Page>()) AddClauses(page);
                Scribe.Fix(old);
                Item made = ItemGenerator.Copied(old, item.Name, item.LookSeed(), item.Level);
                item.Held = made.Held;
            }
            item.Book = null;
            ItemGenerator.Fix(item);

            if (!item.IsBook || !Enum.IsDefined(item.Mode)) item.Mode = BookMode.Active;
            item.When = item.Mode == BookMode.Trigger ? BookTriggers.Fix(item.When) : null;

            if (!item.IsFolio)
            {
                item.Page = null;
                item.Sticker = null;
                item.Held = null;
                return;
            }

            if (item.IsBook) item.Held = Held(item.Held, 1, Rulebook.MostPages, held => held.IsPage && (item.Mode != BookMode.Toggle || held.Page?.Kind == PageKind.Area));
            else if (item.IsPage)
            {
                item.Page ??= Scribe.NewPage(PageKind.Projectile);
                Scribe.Fix(item.Page, following: true);
                PageKind kind = item.Page.Kind;
                item.Held = Held(item.Held, 0, Rulebook.MostStickers, held => held.Sticker is Sticker sticker && Stickers.GoesOn(sticker, kind));
            }
            else
            {
                item.Sticker ??= new Sticker();
                Stickers.Fix(item.Sticker);
                item.Held = null;
            }
            foreach (Item? held in item.Held ?? Array.Empty<Item?>())
            {
                if (held != null) Fix(held);
            }
        }

        // Clauses that are gone can't be stickers, so they're only left out.
        private static void AddClauses(Page page)
        {
            foreach (string clause in page.Clauses ?? new List<string>())
            {
                if (Stickers.Written(clause) is Sticker sticker) page.Stickers.Add(sticker);
            }
            page.Clauses = null;
        }

        private static Item?[] Held(Item?[]? held, int least, int most, Func<Item, bool> belongs)
        {
            var kept = new Item?[Math.Clamp(held?.Length ?? 0, least, most)];
            for (int i = 0; i < kept.Length && held != null && i < held.Length; i++)
            {
                if (held[i] is Item item && belongs(item)) kept[i] = item;
            }
            return kept;
        }

        // Before a pack had cells it was a list of what was in it, so that's what fills the first of them, in the order they were carried. A pack that's the right size is left as it is, gaps and all.
        private static void FixPack(HeroState hero)
        {
            if (hero.Inventory?.Length == HeroState.InventorySize) return;
            Item?[] carried = hero.Inventory ?? Array.Empty<Item?>();
            hero.Inventory = new Item?[HeroState.InventorySize];
            foreach (Item? item in carried)
            {
                if (item != null) hero.Pickup(item);
            }
        }

        // Before talents there were attributes, and items held a stat and a value. Attributes are simply gone, and every point
        // spent on them comes back as a talent, as those are worked out from the hero's level. Each old stat became an affix, value for value.
        private static void Upgrade(JsonObject root)
        {
            if (root["Hero"] is not JsonObject hero) return;
            if (hero["Inventory"] is JsonArray carried)
            {
                foreach (JsonNode? item in carried) UpgradeItem(item as JsonObject);
            }
            if (hero["Equipped"] is JsonObject worn)
            {
                foreach (KeyValuePair<string, JsonNode?> item in worn) UpgradeItem(item.Value as JsonObject);
            }
        }

        private static void UpgradeItem(JsonObject? item)
        {
            if (item == null || item["Modifiers"] is not JsonArray modifiers) return;

            var lines = new JsonArray();
            foreach (JsonNode? node in modifiers)
            {
                if (node is not JsonObject modifier) continue;
                string? affix = OldStat(modifier["Stat"]?.GetValue<string>());
                if (affix == null) continue;
                lines.Add(new JsonObject { ["Affix"] = affix, ["Value"] = modifier["Value"]?.GetValue<float>() ?? 0f });
            }
            item.Remove("Modifiers");
            item["Lines"] = lines;
        }

        private static string? OldStat(string? stat) => stat switch
        {
            "Damage" => "added-physical",
            "DamagePercent" => "damage",
            "Armour" => "armour",
            "Health" => "health",
            "CritChance" => "crit-chance",
            "CritDamage" => "crit-multiplier",
            "AttackSpeed" => "attack-speed",
            "MoveSpeed" => "move-speed",
            "Regeneration" => "health-regen",
            "LifeOnHit" => "health-on-hit",
            "GoldFind" => "gold-find",
            "ItemFind" => "item-rarity",
            _ => null,
        };

        public void Store()
        {
            try
            {
                // Written aside and swapped in, so a crash mid-write can't leave a half-written save behind.
                string path = SavePath;
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(this, Options));
                File.Move(temporary, path, overwrite: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Could not write the save: {e.Message}");
            }
        }
    }
}
