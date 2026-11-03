using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Progression
{
    public enum ObjectiveKind
    {
        ClearSites,     // Clear every site of a kind, or the first few of them.
        PrayAtShrines,
        SlayWarlord,
    }

    public sealed class Objective
    {
        public ObjectiveKind Kind;
        public SiteKind Site;
        public string Text = "";
        public int Goal;
        public int Progress;
        public int RewardExperience;
        public int RewardGold;
        public bool IsMain;

        public bool Done => Progress >= Goal;
        public string Summary => Goal > 1 ? $"{Text} ({Math.Min(Progress, Goal)}/{Goal})" : Text;
    }

    /// <summary>
    /// A world's story: who holds it, what was done to it, and what the hero means to do about that. It is written from the world
    /// as generated, so its names and places are the real ones, and the goals in it are the places on the map. Finishing them
    /// is what clears a world, and what opens the way to harder ones.
    /// </summary>
    public sealed class Chronicle
    {
        private static readonly string[] Titles = { "the Red Hand", "the Hollow Crown", "the Ashen Band", "the Wolf Banner", "the Last Levy", "the Black Oath" };
        private static readonly string[] Ranks = { "Warlord", "Reaver", "Lord", "Captain", "Tyrant" };
        private static readonly string[] Openings =
        {
            "{0} once kept its peace. Then {1} came over the water.",
            "They still tell of the harvest before {1} took {0}.",
            "No one in {0} speaks {1}'s name above a whisper.",
            "{0} has paid its tithe to {1} for three winters now.",
            "What was left of {0} after the burning, {1} took.",
        };
        private static readonly string[] Closings =
        {
            "The people have stopped waiting for anyone to come.",
            "The roads are not safe, and the shrines have gone dark.",
            "The old stones are the only thing here that remembers better days.",
            "Someone will have to walk the length of this land and put it right.",
        };

        public string Warlord { get; private set; } = "";
        public string Faction { get; private set; } = "";
        public string Prologue { get; private set; } = "";
        public readonly List<Objective> Objectives = new();

        public Objective Main => Objectives[0];
        public bool WorldCleared => Main.Done;

        public static Chronicle Write(WorldData world)
        {
            var random = new WorldRandom(world.Plan.Seed).Fork("chronicle");
            var chronicle = new Chronicle
            {
                Warlord = $"{random.Pick(Ranks)} {PlaceNames.Root(world.Plan.Seed, random)}",
                Faction = random.Pick(Titles),
            };

            string opening = string.Format(random.Pick(Openings), world.Plan.Name, chronicle.Warlord);
            chronicle.Prologue = $"{opening} {random.Pick(Closings)}";

            int level = world.Plan.Level;
            WorldSite keep = world.Sites.First(s => s.Kind == SiteKind.Keep);
            chronicle.Objectives.Add(new Objective
            {
                Kind = ObjectiveKind.SlayWarlord,
                Site = SiteKind.Keep,
                Text = $"Bring down {chronicle.Warlord} at {keep.Name}",
                Goal = 1,
                IsMain = true,
                RewardExperience = 400 * level,
                RewardGold = 120 * level,
            });

            int camps = world.Sites.Count(s => s.Kind == SiteKind.BanditCamp);
            if (camps > 0)
            {
                chronicle.Objectives.Add(new Objective
                {
                    Kind = ObjectiveKind.ClearSites,
                    Site = SiteKind.BanditCamp,
                    Text = $"Burn out the hideouts of {chronicle.Faction}",
                    Goal = Math.Min(camps, 3),
                    RewardExperience = 150 * level,
                    RewardGold = 60 * level,
                });
            }

            int barrows = world.Sites.Count(s => s.Kind == SiteKind.Graveyard);
            if (barrows > 0)
            {
                chronicle.Objectives.Add(new Objective
                {
                    Kind = ObjectiveKind.ClearSites,
                    Site = SiteKind.Graveyard,
                    Text = "Lay the restless dead to rest",
                    Goal = Math.Min(barrows, 2),
                    RewardExperience = 120 * level,
                    RewardGold = 50 * level,
                });
            }

            int shrines = world.Sites.Count(s => s.Kind == SiteKind.Shrine);
            if (shrines > 0)
            {
                chronicle.Objectives.Add(new Objective
                {
                    Kind = ObjectiveKind.PrayAtShrines,
                    Site = SiteKind.Shrine,
                    Text = "Rekindle the old shrines",
                    Goal = Math.Min(shrines, 3),
                    RewardExperience = 90 * level,
                    RewardGold = 30 * level,
                });
            }
            return chronicle;
        }

        /// <summary>A place's short history, for when the hero first comes upon it.</summary>
        public string Describe(WorldSite site, WorldData world)
        {
            var random = new WorldRandom(world.Plan.Seed).Fork("site lore").Fork(site.Id);
            string biome = world.Plan.Biomes[site.Biome].Name.ToLowerInvariant();
            string[] lines = site.Kind switch
            {
                SiteKind.Village => new[] { $"Market town of the {biome}. It pays {Faction} in grain and in sons.", "Its people bar their doors at dusk.", $"It was here first, and means to be here after {Warlord}." },
                SiteKind.Hamlet => new[] { "A few families who would not leave.", "Too small to be worth burning, so far.", "Their fields are all they have." },
                SiteKind.Farmstead => new[] { "One family's stubborn acres.", "The barn is newer than the house. Ask no questions." },
                SiteKind.FishingHamlet => new[] { "They still put out at dawn, whoever rules the land.", "Salt, nets and old stories." },
                SiteKind.LumberCamp => new[] { $"Timber for the walls of {Warlord}.", "The axes never stop." },
                SiteKind.Shrine => new[] { "Its light has gone out. It can be kindled again.", "Offerings lie unclaimed on the stone.", "The path here is older than the road." },
                SiteKind.StandingStones => new[] { "Raised by no people anyone remembers.", "The stones hum faintly, like held breath." },
                SiteKind.Watchtower => new[] { "From here you can see the next fire, when they are lit.", "The beacon has been cold for years." },
                SiteKind.Ruins => new[] { "Whoever lived here left in a hurry, long ago.", "Roots have done what armies could not." },
                SiteKind.BanditCamp => new[] { $"A hideout of {Faction}.", "Smoke, stolen goods and men with nothing to lose." },
                SiteKind.Graveyard => new[] { "The dead here do not lie quiet.", "Barrows of a people the land has forgotten." },
                SiteKind.Keep => new[] { $"The seat of {Warlord}.", "Built with stolen stone, held with stolen sons." },
                _ => new[] { "The way you came." },
            };
            return random.Pick(lines);
        }

        /// <summary>Where the current goal lies from a position: the nearest unfinished place of the first open objective.</summary>
        public Vector2? NextTarget(WorldData world, Func<WorldSite, bool> isDone, Vector2 from)
        {
            // Side goals first: going straight for the warlord is a choice, not the suggestion.
            for (int i = Objectives.Count - 1; i >= 0; i--)
            {
                Objective objective = Objectives[i];
                if (objective.Done) continue;

                WorldSite? nearest = null;
                float best = float.PositiveInfinity;
                foreach (WorldSite site in world.Sites)
                {
                    if (site.Kind != objective.Site || isDone(site)) continue;
                    float distance = (site.Position - from).LengthSquared;
                    if (distance < best)
                    {
                        best = distance;
                        nearest = site;
                    }
                }
                if (nearest != null) return nearest.Position;
            }
            return null;
        }
    }
}
