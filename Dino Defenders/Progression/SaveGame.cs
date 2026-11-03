using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dino_Defenders.Progression
{
    /// <summary>What survives closing the game: the hero, and where they are.</summary>
    public sealed class SaveGame
    {
        public HeroState Hero { get; set; } = HeroState.CreateNew();
        public int WorldSeed { get; set; }
        public string? WorldArchetype { get; set; }
        public int WorldLevel { get; set; } = 1;
        public List<int> ClearedSites { get; set; } = new();
        public List<int> DiscoveredSites { get; set; } = new();
        public List<int> Objectives { get; set; } = new();
        public float PlayerX { get; set; } = -1f;
        public float PlayerZ { get; set; } = -1f;

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        private static string SavePath
        {
            get
            {
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
