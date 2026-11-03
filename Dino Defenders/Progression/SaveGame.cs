using System.Text.Json;
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
        public HeroState Hero { get; set; } = HeroState.CreateNew();
        public int WorldSeed { get; set; }
        public string? WorldArchetype { get; set; }
        public int WorldLevel { get; set; } = 1;
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
                return JsonSerializer.Deserialize<SaveGame>(File.ReadAllText(path), Options);
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
            {
                // A damaged save shouldn't stop the game from starting; it starts a new journey instead.
                Console.WriteLine($"Could not read the save: {e.Message}");
                return null;
            }
        }

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
