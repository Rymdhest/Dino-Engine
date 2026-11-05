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
        public const int CurrentVersion = 3;

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
                if (save != null && !json.Contains("\"Version\"")) KeepOldCopy(path);
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
        private static void KeepOldCopy(string path)
        {
            try
            {
                string copy = path + ".before-talents";
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

            SaveGame? save = root.Deserialize<SaveGame>(Options);
            if (save == null) return null;
            FixBar(save.Hero);
            TalentTree.Sanitize(save.Hero);
            save.Offering.Sanitize();
            return save;
        }

        // A bar of the wrong length, from a hand or an older build, shouldn't be anyone's problem later on.
        private static void FixBar(HeroState hero)
        {
            if (hero.Bar.Length == HeroState.SkillSlots) return;
            var bar = new string?[HeroState.SkillSlots];
            Array.Copy(hero.Bar, bar, Math.Min(hero.Bar.Length, bar.Length));
            hero.Bar = bar;
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

        public static void Delete()
        {
            try
            {
                File.Delete(SavePath);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Could not delete the save: {e.Message}");
            }
        }

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
