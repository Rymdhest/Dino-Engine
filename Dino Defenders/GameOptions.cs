using OpenTK.Mathematics;

namespace Dino_Defenders
{
    /// <summary>
    /// What the game was started with. Playing needs none of it; these jump straight to a world for development, and let a
    /// scripted run look at one spot, take a screenshot and quit.
    ///   --seed N  --archetype id  --site Kind  --at x,z  --look distance  --shot file.png  --frames N
    ///   --tour folder   Visits one of every kind of site, and a wood, a road and a shore, photographing each.
    /// </summary>
    internal sealed class GameOptions
    {
        public int? Seed;
        public string? Archetype;
        public string? Site;
        public Vector2? At;
        public float? Look;         // Metres above the player for a high look at a place.
        public string? Shot;
        public string? Tour;
        public int ShotAfterFrames = 150;

        public static GameOptions Parse(string[] args)
        {
            var options = new GameOptions();
            for (int i = 0; i + 1 < args.Length; i += 2)
            {
                string value = args[i + 1];
                switch (args[i].TrimStart('-'))
                {
                    case "seed": options.Seed = int.Parse(value); break;
                    case "archetype": options.Archetype = value; break;
                    case "site": options.Site = value; break;
                    case "at":
                        string[] parts = value.Split(',');
                        options.At = new Vector2(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
                        break;
                    case "look": options.Look = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture); break;
                    case "shot": options.Shot = value; break;
                    case "tour": options.Tour = value; break;
                    case "frames": options.ShotAfterFrames = int.Parse(value); break;
                }
            }
            return options;
        }
    }
}
