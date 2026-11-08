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
    /// A world's story: who holds it, what was done to it, and what the hero means to do about that. Every land is one the Saurian Reich has taken: a commander holds it with one of its tyrants, a numbered
    /// brood of its soldiers and beasts keeps it, and the Ossuary digs in its graves. It is written from the world as generated, so its names and places are the real ones, and the goals in it are the places
    /// on the map. Finishing them is what clears a world, and what opens the way to harder ones. Read LORE.md before changing a word of it.
    /// </summary>
    public sealed class Chronicle
    {
        // The Reich names its broods by number and its tyrants in its own tongue, as it names everything it owns.
        private static readonly string[] Numbers = { "Third", "Fifth", "Seventh", "Ninth", "Eleventh", "Thirteenth", "Sixth", "Fourth" };
        private static readonly string[] Broods = { "Brood", "Talon", "Legion", "Hatch", "Fang" };
        private static readonly string[] Ranks = { "Kommandant", "Oberst", "Brutmeister", "Hauptmann" };
        private static readonly string[] Families = { "Vorhalt", "Krähling", "Grauvogt", "Maltritz", "Hagenbrand", "Drewitz", "Rauhfels", "Steinkopf", "Dornbeck", "Felsing", "Wernick", "Torbach", "Ostrander", "Kalbe" };
        private static readonly string[] BeastNames = { "Eisenmaul", "Blutzahn", "Knochenbrecher", "Donnerfuss", "Sturmkiefer", "Schwarzzahn", "Grimmzahn", "Felsbeisser", "Totenkiefer", "Brandschlund", "Hungerzahn", "Splitterkiefer" };
        private static readonly string[] Openings =
        {
            "{0} kept its peace until the iron ships came over the water, and the Saurian Reich came ashore with its beasts.",
            "They still tell of the night the first roar was heard in {4}. {1} has held it since, and {2} walks its roads.",
            "{0} has paid its tithe to the Reich for three winters: grain, and its sons for the pens.",
            "The shrines of {4} went dark the year {3} landed, and the old bones began to walk.",
            "{1} came to {4} with {3} and a tyrant called {2}, and took it in a single season.",
        };
        private static readonly string[] Closings =
        {
            "The people have stopped waiting for anyone to come.",
            "The roads are the Reich's now, and its raptors hunt on them.",
            "The old stones are the only thing here that remembers better days.",
            "Someone will have to walk the length of this land and drive them back into the sea.",
            "The Reich digs in the hills, and what it digs up does not stay dead.",
        };

        /// <summary>The tyrant that holds this land, by name, and which kind of enemy it is. Its name is all a player is told: what it is, they see.</summary>
        public string Beast { get; private set; } = "";
        public string BossKind { get; private set; } = "";

        /// <summary>Whose tyrant it is: the Reich's commander here, who is never met, as it's the beast that holds the land.</summary>
        public string Commander { get; private set; } = "";

        /// <summary>The brood of the Reich that keeps this land.</summary>
        public string Faction { get; private set; } = "";

        public string Prologue { get; private set; } = "";
        public readonly List<Objective> Objectives = new();

        public Objective Main => Objectives[0];
        public bool WorldCleared => Main.Done;

        /// <summary>What a land's tyrant is, which the story and the keep have to agree on. Most are bred and armoured; some lands have one the Ossuary raised from the rock, or one rotting where it stands.</summary>
        public static string BossKindOf(int seed)
        {
            float roll = new WorldRandom((ulong)(uint)seed).Fork("boss").Float();
            return roll < 0.7f ? "tyrant" : roll < 0.85f ? "bone-tyrant" : "rotting-tyrant";
        }

        public static Chronicle Write(WorldData world)
        {
            var random = new WorldRandom(world.Plan.Seed).Fork("chronicle");
            var chronicle = new Chronicle
            {
                Beast = random.Pick(BeastNames),
                BossKind = BossKindOf(world.Plan.Seed),
                Commander = $"{random.Pick(Ranks)} {random.Pick(Families)}",
                Faction = $"the {random.Pick(Numbers)} {random.Pick(Broods)}",
            };

            // A land's name starts a sentence as it is, and goes in the middle of one with a small "the".
            string name = world.Plan.Name;
            string within = name.StartsWith("The ") ? "the " + name.Substring(4) : name;
            string opening = string.Format(random.Pick(Openings), name, chronicle.Commander, chronicle.Beast, chronicle.Faction, within);
            chronicle.Prologue = $"{opening} {random.Pick(Closings)}";

            int level = world.Plan.Level;
            WorldSite keep = world.Sites.First(s => s.Kind == SiteKind.Keep);
            chronicle.Objectives.Add(new Objective
            {
                Kind = ObjectiveKind.SlayWarlord,
                Site = SiteKind.Keep,
                Text = chronicle.BossKind switch
                {
                    "bone-tyrant" => $"Put {chronicle.Beast}, the Ossuary's bone tyrant, back in the ground at {keep.Name}",
                    "rotting-tyrant" => $"End {chronicle.Beast}, {chronicle.Commander}'s rotting tyrant, at {keep.Name}",
                    _ => $"Bring down {chronicle.Beast}, {chronicle.Commander}'s tyrant, at {keep.Name}",
                },
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
                    Text = $"Burn out the pens of {chronicle.Faction}",
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
                    Text = "Put what the Ossuary raised back in the ground",
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
                SiteKind.Village => new[] { $"Market town of the {biome}. It pays {Faction} in grain, and in sons for the pens.", "Its people bar their doors when the roaring starts.", $"It was here first, and means to be here after {Commander}." },
                SiteKind.Hamlet => new[] { "A few families who would not leave.", "Too small for the Reich to burn, so far.", "Their fields are all they have." },
                SiteKind.Farmstead => new[] { "One family's stubborn acres.", "The raptors took the oxen last spring. Ask no questions." },
                SiteKind.FishingHamlet => new[] { "They still put out at dawn, whoever rules the land.", "They watched the iron ships come in from here." },
                SiteKind.LumberCamp => new[] { "Timber for the Reich's pens.", "The axes never stop." },
                SiteKind.Shrine => new[] { "Its light has gone out. It can be kindled again.", "The Reich tore down what it could. The rest held.", "The path here is older than the road." },
                SiteKind.StandingStones => new[] { "Raised by no people anyone remembers, over bones nobody was meant to find.", "The stones hum faintly, like held breath." },
                SiteKind.Watchtower => new[] { "From here you can see the next fire, when they are lit.", "The beacon has been cold since the ships came." },
                SiteKind.Ruins => new[] { "Whoever lived here left in a hurry, long ago.", "The Reich's diggers have been at the old foundations." },
                SiteKind.BanditCamp => new[] { $"A pen of {Faction}: cages, chains and the smell of blood.", "Where the Reich keeps its beasts between hunts." },
                SiteKind.Graveyard => new[] { "The dead here do not lie quiet.", "The Ossuary has been digging here. It is always night in that churchyard." },
                SiteKind.Keep => new[] { $"The seat of {Commander}, and the pen of {Beast}.", "Built with stolen stone, held with teeth." },
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
