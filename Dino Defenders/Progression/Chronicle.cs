using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Progression
{
    public enum ObjectiveKind
    {
        FreePlaces,     // Free the places the land names, which are a few of those held.
        SlayWarlord,    // Bring down the tyrant that holds the land.
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
        public readonly List<WorldSite> Places = new();     // The places it asks to be freed, and only those count: freeing any other is its own reward.

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
        // What an arena is, from the stands, for each kind: {0} its name, {1} the commander, {2} the tyrant, {3} the brood and {4} its name inside a sentence. Then what to expect there.
        private static readonly Dictionary<ArenaKind, (string[] Openings, string[] Closings)> ArenaLines = new()
        {
            [ArenaKind.Pit] = (new[]
            {
                "{1} had {4} dug out of the hill so that {2} would never feed without an audience.",
                "{3} march the people of the land to {4} on fight days, and count them going in.",
                "Nobody who has gone down into {4} has come back out of it. The stands are made to watch anyway.",
            }, new[]
            {
                "When it goes badly for the tyrant, its keepers throw up the gates.",
                "The handlers wait behind the gates with the rest of the brood.",
                "The crowd is silent. It always is, until something falls.",
            }),
            [ArenaKind.Colosseum] = (new[]
            {
                "{1} built {4} to hold the whole land at once, and fills it on fight days.",
                "{3} keep the rest of the brood under the floor of {4}, and bring it up when it's wanted.",
                "The people of the land are packed into {4} to watch {2} feed, and {1} watches them from the box.",
            }, new[]
            {
                "When the fight turns, the floor opens and more come up.",
                "The bolt-throwers in the commander's box don't often miss.",
                "The crowd is silent until it's told to cheer.",
            }),
            [ArenaKind.Cage] = (new[]
            {
                "The Amber Institute raised {2} in {4}, and its blood-doctors still come to watch it feed.",
                "{4} is where {3} breed what the Institute grows, and where {2} is kept between hunts.",
                "Nothing that has gone into {4} has come out of it but what was hatched there.",
            }, new[]
            {
                "Its eggs lie in the straw. Leave them, and they hatch.",
                "The pens round the cage are full, and the handlers open them when they're told.",
                "The blood-doctors keep notes on whoever goes in.",
            }),
            [ArenaKind.SunTemple] = (new[]
            {
                "{4} was the land's own, before {1} made it the Reich's hall of assembly.",
                "The Reich's commanders meet in {4} under the land's own sun, and {2} keeps the floor.",
                "The people of the land raised {4} to their sun. The Reich made it a place to show them {2}.",
            }, new[]
            {
                "Its priests turn the great mirrors on whoever's on the floor. The sun burns anything.",
                "When the sun stands high, the mirrors burn.",
                "The doors open when the fight turns, and more of the brood come in.",
            }),
            [ArenaKind.Onslaught] = (new[]
            {
                "The Reich's first charge on the land was buried in ash at {4}. The Ossuary has been digging it up ever since.",
                "{4} is where the mountain took a whole war-band of {3} in a day. The Ossuary keeps what was left.",
                "The beasts of the first charge still stand in the ash at {4}, turned to stone where it caught them.",
            }, new[]
            {
                "Whatever the Ossuary raises, it raises out of the ash.",
                "The ground breathes fire where it's cracked.",
                "The dead come up through the ash when the tyrant falters.",
            }),
            [ArenaKind.Cathedral] = (new[]
            {
                "{4} was built to the Reich's creed, that the strongest blood is owed the world, and {2} is what it kneels to.",
                "{1} has {2} anointed in {4} before every hunt.",
                "The Reich's faithful light the candles in {4} every night, and leave the floor to its tyrant.",
            }, new[]
            {
                "The candles hang over the floor on iron wheels, and iron falls.",
                "When the fight turns, the gates in the screens go up.",
                "The faithful watch from behind the screens and say nothing.",
            }),
        };

        /// <summary>Every kind of tyrant there is, as a land's boss: bred, raised from bones by the Ossuary, or rotting where it stands.</summary>
        public static readonly string[] TyrantKinds = { "tyrant", "bone-tyrant", "rotting-tyrant" };

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

        /// <summary>The second tyrant, on a land whose orders gave it two: its name and what it is.</summary>
        public string SecondBeast { get; private set; } = "";
        public string SecondKind { get; private set; } = "";

        public Objective? Main => Objectives.Count > 0 ? Objectives[0] : null;

        /// <summary>Whether everything the land asks has been done. The Haven asks nothing, and is never freed.</summary>
        public bool WorldCleared => Objectives.Count > 0 && Objectives.All(objective => objective.Done);

        /// <summary>What a land's tyrant is, which the story and the keep have to agree on. Most are bred and armoured; some lands have one the Ossuary raised from the rock, or one rotting where it stands.</summary>
        public static string BossKindOf(int seed)
        {
            float roll = new WorldRandom((ulong)(uint)seed).Fork("boss").Float();
            return roll < 0.7f ? TyrantKinds[0] : roll < 0.85f ? TyrantKinds[1] : TyrantKinds[2];
        }

        /// <summary>Who holds a land, from its seed alone, so that the map table can tell of it before it's grown. The chronicle written once it is says the same.</summary>
        public static Herald HeraldOf(int seed) => HeraldOf(seed, new WorldRandom(seed).Fork("chronicle"));

        private static Herald HeraldOf(int seed, WorldRandom random)
        {
            string beast = random.Pick(BeastNames);
            string commander = $"{random.Pick(Ranks)} {random.Pick(Families)}";
            string faction = $"the {random.Pick(Numbers)} {random.Pick(Broods)}";
            return new Herald(beast, BossKindOf(seed), commander, faction);
        }

        /// <summary>What a tyrant is, in a few words: a bred one, or what the Ossuary made of one.</summary>
        public static string KindName(string bossKind) => bossKind switch
        {
            "bone-tyrant" => "bone tyrant",
            "rotting-tyrant" => "rotting tyrant",
            _ => "tyrant",
        };

        public static Chronicle Write(WorldData world, LandTerms terms, bool haven)
        {
            // The same stream the herald reads from, so that what the map table told of a land is what's there.
            var random = new WorldRandom(world.Plan.Seed).Fork("chronicle");
            Herald herald = HeraldOf(world.Plan.Seed, random);
            var chronicle = new Chronicle
            {
                Beast = herald.Beast,
                BossKind = herald.BossKind,
                Commander = herald.Commander,
                Faction = herald.Faction,
            };
            if (haven)
            {
                chronicle.Prologue = "The one place the Reich has never found. Every waygate's road comes here first, and those who fight it rest here between lands.";
                return chronicle;
            }

            // A land's name starts a sentence as it is, and goes in the middle of one with a small "the".
            string name = world.Plan.Name;
            string within = name.StartsWith("The ") ? "the " + name.Substring(4) : name;
            bool arena = world.Plan.Archetype.Arena;
            (string[] openings, string[] closings) = world.Plan.Archetype.ArenaKind is ArenaKind kind ? ArenaLines[kind] : (Openings, Closings);
            string opening = string.Format(random.Pick(openings), name, chronicle.Commander, chronicle.Beast, chronicle.Faction, within);
            chronicle.Prologue = $"{opening} {random.Pick(closings)}";

            int level = world.Plan.Level;
            bool twinned = terms.Has(OrdersCatalog.TwinPens);
            if (twinned)
            {
                // The second has to be a different beast, with a name of its own.
                var second = new WorldRandom(world.Plan.Seed).Fork("second tyrant");
                chronicle.SecondBeast = second.Pick(BeastNames.Where(name => name != chronicle.Beast).ToList());
                chronicle.SecondKind = second.Pick(TyrantKinds.Where(kind => kind != chronicle.BossKind).ToList());
            }

            if (arena)
            {
                // An arena asks for its tyrant and nothing else, and pays for it as a land does for everything.
                chronicle.Objectives.Add(new Objective
                {
                    Kind = ObjectiveKind.SlayWarlord,
                    Site = SiteKind.Arena,
                    Text = twinned ? $"Bring down both tyrants in {within}: {chronicle.Beast}, and {chronicle.SecondBeast}" : $"Bring down {chronicle.Beast}, {chronicle.Commander}'s {KindName(chronicle.BossKind)}, in {within}",
                    Goal = twinned ? 2 : 1,
                    IsMain = true,
                    RewardExperience = ArenaExperience * level,
                    RewardGold = ArenaGold * level,
                });
                return chronicle;
            }

            WorldSite keep = world.Sites.First(s => s.Kind == SiteKind.Keep);
            WorldSite? twin = twinned ? TwinSite(world) : null;
            if (twin == null && twinned)
            {
                chronicle.SecondBeast = "";
                chronicle.SecondKind = "";
            }

            chronicle.Objectives.Add(new Objective
            {
                Kind = ObjectiveKind.SlayWarlord,
                Site = SiteKind.Keep,
                Text = twin != null ? $"Bring down both tyrants: {chronicle.Beast} at {keep.Name}, and {chronicle.SecondBeast} at {twin.Name}" : chronicle.BossKind switch
                {
                    "bone-tyrant" => $"Put {chronicle.Beast}, the Ossuary's bone tyrant, back in the ground at {keep.Name}",
                    "rotting-tyrant" => $"End {chronicle.Beast}, {chronicle.Commander}'s rotting tyrant, at {keep.Name}",
                    _ => $"Bring down {chronicle.Beast}, {chronicle.Commander}'s tyrant, at {keep.Name}",
                },
                Goal = twin != null ? 2 : 1,
                IsMain = true,
                RewardExperience = 400 * level,
                RewardGold = 120 * level,
            });

            // Which places, on a stream of their own so that nothing else in the story moves. A land holds far more than it asks for, and a map showing every one of them was too much to read.
            List<WorldSite> held = world.Sites.Where(Counts).ToList();
            new WorldRandom(world.Plan.Seed).Fork("places asked").Shuffle(held);
            var freeing = new Objective
            {
                Kind = ObjectiveKind.FreePlaces,
                Site = SiteKind.Haunt,
                Text = "Free the places marked on your map from those who hold them",
                RewardExperience = 200 * level,
                RewardGold = 80 * level,
            };
            freeing.Places.AddRange(held.Take(Math.Min(terms.Places, held.Count)));
            freeing.Goal = Math.Max(freeing.Places.Count, 1);
            chronicle.Objectives.Add(freeing);
            return chronicle;
        }

        /// <summary>Whether freeing a place is something the land asks.</summary>
        public bool Asks(WorldSite site) => Objectives.Any(objective => objective.Kind == ObjectiveKind.FreePlaces && objective.Places.Contains(site));

        // What an arena pays for its tyrant, for each level: what a land pays for its tyrant and its places together.
        private const int ArenaExperience = 600;
        private const int ArenaGold = 200;

        /// <summary>Whether a place is held, and isn't the keep, which is what the places a land asks to be freed are.</summary>
        public static bool Counts(WorldSite site) => site.Kind != SiteKind.Keep && site.Spawns.Any(spawn => spawn.Role.IsHostile());

        /// <summary>Where a second tyrant stands, on a land whose orders gave it two: the pen farthest from where heroes come in, or the farthest place that's held if the land has no pen.</summary>
        public static WorldSite? TwinSite(WorldData world) => world.Sites.Where(site => site.Kind == SiteKind.BanditCamp).OrderByDescending(site => site.Remoteness).FirstOrDefault()
            ?? world.Sites.Where(Counts).OrderByDescending(site => site.Remoteness).FirstOrDefault();

        /// <summary>A place's short history, for when the hero first comes upon it.</summary>
        public string Describe(WorldSite site, WorldData world)
        {
            var random = new WorldRandom(world.Plan.Seed).Fork("site lore").Fork(site.Id);
            // "Market town of the blackwood", and not of the the blackwood: some biomes' names have their own article.
            string biome = world.Plan.Biomes[site.Biome].Name.ToLowerInvariant();
            if (biome.StartsWith("the ")) biome = biome.Substring(4);
            string[] lines = site.Kind switch
            {
                SiteKind.Village => new[] { $"Market town of the {biome}. Its people hide from {Faction} behind barred doors, and trade with anyone who isn't the Reich.", "Its people bar their doors when the roaring starts.", $"It was here first, and means to be here after {Commander}." },
                SiteKind.Hamlet => new[] { "A few families who would not leave. The raptors came for them anyway.", "Too small for the Reich to burn. It sent its beasts instead." },
                SiteKind.Farmstead => new[] { "One family's stubborn acres, and what's eating them now.", "The raptors took the oxen last spring. Then they came back." },
                SiteKind.FishingHamlet => new[] { "They watched the iron ships come in from here. The crews stayed.", "Nobody puts out at dawn any more." },
                SiteKind.LumberCamp => new[] { "Timber for the Reich's pens, cut by those it took.", "The axes never stop, and the greycoats never leave." },
                SiteKind.Shrine => new[] { "Its light has gone out, and the Reich keeps it out. It can be kindled again.", "The Reich tore down what it could. The rest held.", "The path here is older than the road." },
                SiteKind.StandingStones => new[] { "Raised by no people anyone remembers, over bones nobody was meant to find.", "The stones hum faintly, like held breath." },
                SiteKind.Watchtower => new[] { "From here you can see the next fire, when they are lit.", "The beacon has been cold since the ships came." },
                SiteKind.Ruins => new[] { "Whoever lived here left in a hurry, long ago.", "The Reich's diggers have been at the old foundations." },
                SiteKind.BanditCamp => new[] { $"A pen of {Faction}: cages, chains and the smell of blood.", "Where the Reich keeps its beasts between hunts." },
                SiteKind.Graveyard => new[] { "The dead here do not lie quiet.", "The Ossuary has been digging here. It is always night in that churchyard." },
                SiteKind.Keep => new[] { $"The seat of {Commander}, and the pen of {Beast}.", "Built with stolen stone, held with teeth." },
                SiteKind.Waygate => new[] { "One of the land's old roads. It knows the way home, and to every other gate you've found.", "Raised by no people anyone remembers. It still answers." },
                _ => new[] { "The way you came." },
            };
            return random.Pick(lines);
        }
    }
}
