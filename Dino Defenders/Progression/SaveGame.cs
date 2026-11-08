using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Engine.World;
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
        public bool Lit { get; set; }
        public List<int> Dead { get; set; } = new();
        public List<int> Opened { get; set; } = new();
    }

    /// <summary>What survives closing the game: the hero, and the land they're in, if they're in one. Nothing about the Haven is kept, as it's always the same.</summary>
    public sealed class SaveGame
    {
        /// <summary>Goes up when what's saved changes shape. <see cref="Upgrade"/> brings older saves up to it: testers carry theirs from build to build.</summary>
        public const int CurrentVersion = 10;

        /// <summary>The version that renamed the places on a hero, which needs a copy of the old save kept and its slots rewritten.</summary>
        private const int SlotsVersion = 4;

        /// <summary>The version that replaced skills with books, which needs a copy of the old save kept. Nothing a tree held means anything now, so everyone starts it again.</summary>
        private const int BooksVersion = 6;

        /// <summary>The version that made pages and stickers items of their own, which needs a copy of the old save kept. What was written stays written, each book with room for exactly what was in it.</summary>
        private const int StickersVersion = 7;

        /// <summary>The version that left four rarities with prefixes and suffixes of their own, which needs a copy of the old save kept. Most of what was rolled under the old rules comes out of it with a line or two changed.</summary>
        private const int AffixVersion = 8;

        /// <summary>The version that made the game a climb through lands from the Haven, which needs a copy of the old save kept. The world a hero was in isn't a land, so they wake in the Haven, with a climb as far as they'd come.</summary>
        private const int LandsVersion = 9;

        /// <summary>
        /// The version that made pages what they're found as, which needs a copy of the old save kept. Where a page started and went, and every Outcome but its first, can't be written any more and are gone: a page that
        /// dealt anything but physical damage is a Spell Page, and anything a found page can't do is dealing damage.
        /// </summary>
        private const int FoundPagesVersion = 10;

        public int Version { get; set; } = CurrentVersion;
        public HeroState Hero { get; set; } = HeroState.CreateNew();

        /// <summary>Whether the hero was in a land when this was written. If not, they're in the Haven, and nothing below means anything.</summary>
        public bool InLand { get; set; }
        public int WorldSeed { get; set; }
        public string? WorldArchetype { get; set; }
        public int WorldLevel { get; set; } = 1;

        /// <summary>What the land holds. It's part of which land it was, as anything saved by number is only right for the same one.</summary>
        public LandTerms Terms { get; set; } = new();
        public List<SiteRecord> Sites { get; set; } = new();
        public List<int> Objectives { get; set; } = new();
        public float PlayerX { get; set; } = -1f;
        public float PlayerZ { get; set; } = -1f;
        public float Health { get; set; } = -1f;

        /// <summary>Whether this is a hero who's never been anywhere, and should be told where they are. It's a method so that it isn't saved.</summary>
        public bool NeverPlayed() => !InLand && Hero.Level == 1 && Hero.Experience == 0 && Hero.WorldsCleared == 0;

        /// <summary>The land this was written in, as it was asked for.</summary>
        public WorldRequest Land() => new WorldRequest(WorldSeed, WorldArchetype, WorldLevel);

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
                else if (save != null && VersionOf(json) < LandsVersion) KeepOldCopy(path, ".before-lands");
                else if (save != null && VersionOf(json) < FoundPagesVersion) KeepOldCopy(path, ".before-found-pages");
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
            if (version < FoundPagesVersion) FoundPages(root);

            SaveGame? save = root.Deserialize<SaveGame>(Options);
            if (save == null) return null;
            save.Version = CurrentVersion;
            FixBar(save.Hero);
            FixPack(save.Hero);
            FixBooks(save.Hero);
            if (version < BooksVersion) save.Hero.Bar[0] ??= ItemGenerator.Book(ItemGenerator.NewSeed(new Dino_Engine.World.WorldRandom((ulong)Random.Shared.NextInt64())));
            if (version < LandsVersion) IntoTheHaven(save);
            if (save.Hero.Offers == 0) save.Hero.Offers = HeroState.NewOffers((ulong)Random.Shared.NextInt64());
            save.Hero.LandLevel = Math.Max(save.Hero.LandLevel, 1);
            save.Hero.BestLand = Math.Max(save.Hero.BestLand, 0);
            TalentTree.Sanitize(save.Hero);
            save.Terms.Sanitize();
            return save;
        }

        // Before lands there were worlds, one after another, each a level deeper once it was free. The world they were in goes, and they're in the Haven with a climb as far as they'd come, and the same
        // talent points: one for each land freed.
        private static void IntoTheHaven(SaveGame save)
        {
            HeroState hero = save.Hero;
            hero.BestLand = Math.Max(hero.BestLand, hero.WorldsCleared);
            hero.LandLevel = Math.Max(hero.LandLevel, hero.BestLand + 1);
            save.InLand = false;
            save.Sites.Clear();
            save.Objectives.Clear();
            save.PlayerX = -1f;
            save.PlayerZ = -1f;
            save.Health = -1f;
        }

        /// <summary>Brings anything kept somewhere else up to the rules, as everything a hero carries is brought when their save is read: the stash's, for one.</summary>
        public static void FixItem(Item item) => Fix(item);

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
        // Every page anywhere in a save from before pages were found, as what a found page could have been: its first Outcome, as long as a found page can do that, which is dealing unless it was raising,
        // or moving from a projectile. What dealt anything but physical damage, or does anything but deal, is a Spell. Where it started and went are left to be ignored.
        private static void FoundPages(JsonNode? node)
        {
            if (node is JsonArray array)
            {
                foreach (JsonNode? each in array) FoundPages(each);
                return;
            }
            if (node is not JsonObject found) return;
            foreach (JsonNode? each in found.Select(pair => pair.Value).ToList()) FoundPages(each);
            if (found["Outcomes"] is not JsonArray outcomes) return;

            JsonObject first = outcomes.FirstOrDefault() is JsonObject was ? (JsonObject)was.DeepClone() : new JsonObject();
            string deed = first["Deed"] is JsonValue said && said.TryGetValue(out string? id) ? id : Sentences.PlainDeed;
            bool projectile = found["Kind"] is JsonValue kind && kind.TryGetValue(out string? named) && named == nameof(PageKind.Projectile);
            Deed doing = Sentences.Find(deed)?.Deed ?? Deed.Deal;
            if (Sentences.Find(deed) is not { Found: > 0f } || doing == Deed.Move && !projectile)
            {
                deed = Sentences.PlainDeed;
                doing = Deed.Deal;
            }
            bool magic = outcomes.OfType<JsonObject>().Any(outcome => outcome["Element"] is JsonValue element && element.TryGetValue(out string? type) && type != "physical");
            first["Deed"] = deed;
            first.Remove("Element");
            found["Outcomes"] = new JsonArray(first);
            found["Hand"] = magic || doing != Deed.Deal ? nameof(Hand.Spell) : nameof(Hand.Attack);
        }

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
                Scribe.Fix(item.Page);
                PageKind kind = item.Page.Kind;
                item.Held = Held(item.Held, 0, Rulebook.MostStickers, held => held.Sticker is Sticker sticker && Stickers.GoesOn(sticker, kind));
            }
            else
            {
                item.Sticker ??= new Sticker();
                Stickers.Fix(item.Sticker, item.Rarity == Rarity.Unique);
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
