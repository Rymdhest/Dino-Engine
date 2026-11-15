using Dino_Defenders.Gameplay;
using Dino_Engine.Animation;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Modelling.Procedural.Architecture;
using Dino_Engine.Modelling.Procedural.Architecture.Waygates;
using Dino_Engine.Modelling.Procedural.Character;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Indoor;
using Dino_Engine.Modelling.Procedural.Saurian;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using System.Collections.Concurrent;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// Makes the people and things of a world, from the names a server calls them by. Models are built once and shared, so a crowd
    /// costs little more than a person. A server never touches a model: it says what something is, and this is what that looks like.
    /// </summary>
    public sealed class CharacterFactory
    {
        public const string BoltPrefix = "bolt:";

        /// <summary>How many different villagers there are: whatever a server numbers one by is one of these.</summary>
        internal const int VillagerLooks = 6;
        private const int BanditLooks = 4;

        // So that friends can tell each other apart in a fight.
        private static readonly Vector3[] HeroDyes =
        {
            new Vector3(0.09f, 0.14f, 0.26f), new Vector3(0.26f, 0.08f, 0.07f), new Vector3(0.08f, 0.2f, 0.11f),
            new Vector3(0.24f, 0.18f, 0.05f), new Vector3(0.18f, 0.08f, 0.24f),
        };

        /// <summary>How many heroes look different, one in each player's colours.</summary>
        internal static int HeroLooks => HeroDyes.Length;

        private readonly Dictionary<string, glModel> _models = new();

        // Everything a world can send at a hero takes seconds of every core to grow, dinosaurs most of all at a few hundred milliseconds each, and nothing at the main menu needs any of it.
        // So it's begun once the land behind the menu has its plants, or at once when there's no menu (Begin), and it takes half the cores, as a world being loaded beside it takes longer
        // with fewer. A world only waits for what isn't done when it's entered, and only the upload is left for this thread.
        private Task<(string Key, Mesh Mesh)[]>? _growing;

        private bool _prepared;

        // The Haven's giants are in the picture behind the main menu, so they're wanted before anything a world can send at a hero, and they take three seconds each, as close as they're
        // seen from. So they're begun the moment the game starts (GrowGiants), one to a core, and taken up the first frame they're done (TryGiants): nothing ever waits for them.
        // They do wait for the materials to have their numbers (MaterialNumbers), which comes before there's a window: a giant begun before then wrote whatever its hide was numbered
        // into each vertex as it went, so its tail, made first, was drawn in another material from the rest of it, edged in lace where the two met.
        private static Task<Mesh[]>? _giantsGrowing;
        private glModel[]? _giants;

        /// <summary>Starts growing the giants, if nothing has yet.</summary>
        public static void GrowGiants()
        {
            _giantsGrowing ??= Task.Run(async () =>
            {
                await Dino_Engine.Modelling.Procedural.MaterialNumbers.Ready.ConfigureAwait(false);
                var meshes = new Mesh[Dino_Engine.Wildlife.Herds.Haven.Length];
                Parallel.For(0, meshes.Length, i => meshes[i] = SaurianForge.Make(SaurianSpec.Parse(Dino_Engine.Wildlife.Herds.Haven[i].Model)!.Value));
                return meshes;
            });
        }

        /// <summary>The giants' models, in the order of <see cref="Dino_Engine.Wildlife.Herds.Haven"/>, once they've grown. Nothing yet, until then.</summary>
        public bool TryGiants(out glModel[] models)
        {
            models = _giants ?? Array.Empty<glModel>();
            if (_giants != null) return true;
            GrowGiants();
            if (!_giantsGrowing!.IsCompleted) return false;
            if (_giantsGrowing.IsFaulted)
            {
                Console.WriteLine($"The giants failed to grow: {_giantsGrowing.Exception}");
                _giantsGrowing = null;
                return false;
            }

            Mesh[] meshes = _giantsGrowing.Result;
            _giants = new glModel[meshes.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                string key = Dino_Engine.Wildlife.Herds.Haven[i].Model;
                if (!_models.TryGetValue(key, out glModel? model))
                {
                    model = glLoader.loadToVAO(meshes[i]);
                    _models.Add(key, model);
                }
                _giants[i] = model;
            }
            models = _giants;
            return true;
        }

        /// <summary>Starts growing everything <see cref="Prepare"/> will want, on worker threads. Asking again does nothing.</summary>
        public void Begin()
        {
            if (_prepared || _growing != null) return;
            _growing = Task.Run(() =>
            {
                // One at a time, as some take three times as long as others, and runs handed out in advance leave cores idle at the end.
                (string Key, Func<Mesh> Build)[] recipes = PreparedRecipes().ToArray();
                var grown = new (string Key, Mesh Mesh)[recipes.Length];
                var queue = Partitioner.Create(Enumerable.Range(0, recipes.Length), EnumerablePartitionerOptions.NoBuffering);
                Parallel.ForEach(queue, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount / 2, 1) }, i => grown[i] = (recipes[i].Key, recipes[i].Build()));
                return grown;
            });
        }

        // Every model a server can send for an enemy: each kind's look, with each of its seeds, and each of its outfits for bandits. Two keys can share a model.
        private static IEnumerable<(string Key, Func<Mesh> Build)> PreparedRecipes()
        {
            var seen = new HashSet<string>();
            foreach (string key in PreparedKeys())
            {
                if (Recipe(key) is (string name, Func<Mesh> build) && seen.Add(name)) yield return (name, build);
            }
        }

        private static IEnumerable<string> PreparedKeys()
        {
            // What a prison holds, which isn't anyone's to fight and so isn't in the bestiary, but is sent like anything else (PLACES.md).
            foreach ((string look, int variants, _, _) in Dino_Engine.World.Content.PlaceStory.Captives)
            {
                for (int variant = 0; variant < variants; variant++) yield return $"{look}{variant}";
            }
            foreach (Server.EnemyKind kind in Server.Bestiary.Everyone)
            {
                if (kind.Look == "bandit")
                {
                    for (int look = 0; look < BanditLooks; look++)
                    {
                        yield return BanditKey(look, archer: false);
                        yield return BanditKey(look, archer: true);
                    }
                }
                else if (kind.Variants > 0)
                {
                    for (int variant = 0; variant < kind.Variants; variant++) yield return $"{kind.Look}{variant}";
                }
                else yield return kind.Look;
            }
        }

        private glModel Model(string key, Func<Mesh> build)
        {
            if (!_models.TryGetValue(key, out glModel? model))
            {
                model = glLoader.loadToVAO(build());
                _models.Add(key, model);
            }
            return model;
        }

        /// <summary>
        /// Builds everything a world can send at a hero, for the loading screen to wait for. Left until it's first seen, a model is built in the middle of
        /// a frame, and that frame is usually the one with a pack coming round the corner.
        /// </summary>
        public void Prepare()
        {
            if (_prepared) return;
            Begin();
            foreach ((string key, Mesh mesh) in _growing!.Result)
            {
                if (!_models.ContainsKey(key)) _models[key] = glLoader.loadToVAO(mesh);
            }
            _growing = null;
            _prepared = true;

            // Everything is already here, but for a look nobody knows, which says so and stands in as a stranger, as it would when sent.
            foreach (string key in PreparedKeys()) Resolve(key);
        }

        /// <summary>The model a server means by <paramref name="key"/>.</summary>
        public glModel Resolve(string key)
        {
            if (Recipe(key) is (string name, Func<Mesh> build)) return Model(name, build);

            // A newer server than this client could name something it has never heard of. Better a stranger than a crash.
            Console.WriteLine($"No model called '{key}'.");
            return Villager(0);
        }

        private static readonly Dictionary<string, Dino_Engine.World.Content.AssetLight[]> PropLights = new();

        /// <summary>
        /// What a prop the server moves gives off, light and flame, in its own frame (<see cref="PropLightsComponent"/>): an arena's chandeliers burn as they hang and as they lie. Of the pieces an
        /// arena moves only those have any, so nothing else is built twice to find out.
        /// </summary>
        public static IReadOnlyList<Dino_Engine.World.Content.AssetLight> LightsOf(string key)
        {
            lock (PropLights)
            {
                if (PropLights.TryGetValue(key, out var known)) return known;
            }
            var lights = new List<Dino_Engine.World.Content.AssetLight>();
            if (Dino_Engine.Modelling.Procedural.Architecture.Arena.ArenaSpec.TryParse(key, out var arena) && arena.Shape == Dino_Engine.Modelling.Procedural.Architecture.Arena.ArenaShape.Chandelier)
            {
                Dino_Engine.Modelling.Procedural.Architecture.Arena.ArenaForge.Make(arena, lights);
            }
            var found = lights.ToArray();
            lock (PropLights) PropLights[key] = found;
            return found;
        }

        // What a key is built from, and the name its model is kept by, which a few keys share. It touches nothing of a factory's, so the models can be grown on any thread.
        private static (string Key, Func<Mesh> Build)? Recipe(string key)
        {
            if (SaurianSpec.Parse(key) is SaurianSpec dinosaur) return (key, () => SaurianForge.Make(dinosaur));
            // The land's people and the Reich's, dressed in what a hero could wear, in a uniform each (Uniforms).
            if (key.StartsWith(ReichPrefix)) return (key, () => Uniforms.Make(key));
            if (key == "skeleton") return (key, SkeletonGenerator.GenerateSkeleton);
            if (Gameplay.MinionLooks.TryParse(key, out var weapon, out var offHand)) return (key, () => Outfit.Hold(Gameplay.MinionLooks.Hovers(key) ? null : SkeletonGenerator.GenerateBare(), Held(weapon, offHand, Gameplay.MinionLooks.Hovers(key))));
            if (key == "skeleton:bow") return (key, SkeletonGenerator.GenerateArcher);
            if (key == "warlord") return (key, () => Uniforms.Make(key));
            if (key == "revenant") return (key, () => Uniforms.Make(key));
            if (BoarSpec.Parse(key) is BoarSpec boar) return (boar.Key, () => BoarForge.Make(boar));
            if (WolfSpec.Parse(key) is WolfSpec wolf) return (wolf.Key, () => WolfForge.Make(wolf));
            if (BearSpec.Parse(key) is BearSpec bear) return (bear.Key, () => BearForge.Make(bear));
            if (key.StartsWith("robed:") || key.StartsWith("acolyte:")) return (key, () => Uniforms.Make(key));
            if (key.StartsWith("cutthroat") && int.TryParse(key.AsSpan(9), out int cutthroat)) return ($"cutthroat{Math.Abs(cutthroat) % 4}", () => Uniforms.Make($"cutthroat{Math.Abs(cutthroat) % 4}"));
            if (key.StartsWith("brute") && int.TryParse(key.AsSpan(5), out int brute)) return ($"brute{Math.Abs(brute) % 3}", () => Uniforms.Make($"brute{Math.Abs(brute) % 3}"));
            if (key == "arrow") return (key, Arrow);
            if (key.StartsWith(Books.Thrown.Prefix)) return (key, () => ThrownThing(key.Substring(Books.Thrown.Prefix.Length)));
            if (key == ChestLook.Closed) return (key, () => Chest(open: false));
            if (key == ChestLook.Open) return (key, () => Chest(open: true));
            if (key is ChestLook.Lid or ChestLook.Opening) return (ChestLook.Lid, ChestLid);
            if (key == "dummy") return (key, Dummy);
            if (key == "turret") return (key, Turret);
            // Whatever of an arena moves is one of its pieces, grown from its id as the rest of it is (ARENAS.md).
            if (Dino_Engine.Modelling.Procedural.Architecture.Arena.ArenaSpec.TryParse(key, out var arena)) return (key, () => Dino_Engine.Modelling.Procedural.Architecture.Arena.ArenaForge.Make(arena, new List<Dino_Engine.World.Content.AssetLight>()));
            // And whatever of a place with a story moves, a cage's door, is one of its pieces (PLACES.md).
            if (Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceSpec.TryParse(key, out var place)) return (key, () => Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceForge.Make(place, new List<Dino_Engine.World.Content.AssetLight>()));
            if (key == Progression.LootModels.Gold) return (key, GoldPile);
            if (key == Progression.LootModels.Find) return (key, () => Ball(new Colour(0f, 0f, 0f)));
            if (key.StartsWith(BoltPrefix) && Enum.TryParse(key.AsSpan(BoltPrefix.Length), out Stats.DamageType look))
            {
                Colour glow = ClientEffects.GlowOf(look);
                return (key, () => Ball(new Colour(glow.Red, glow.Green, glow.Blue, 3f)));
            }
            if (key.StartsWith("hero") && int.TryParse(key.AsSpan(4), out int hero))
            {
                int dye = Math.Abs(hero) % HeroDyes.Length;
                return ($"hero{dye}", () => HumanoidGenerator.Generate(HeroLook(dye)));
            }
            if (key.StartsWith("villager") && int.TryParse(key.AsSpan(8), out int villager))
            {
                int person = Math.Abs(villager) % VillagerLooks;
                return ($"villager{person}", () => Uniforms.Make($"villager{person}"));
            }
            if (key.StartsWith("bandit"))
            {
                int colon = key.IndexOf(':');
                if (colon > 6 && int.TryParse(key.AsSpan(6, colon - 6), out int bandit))
                {
                    int outfit = Math.Abs(bandit) % BanditLooks;
                    bool archer = key.EndsWith("True");
                    return ($"bandit{outfit}{archer}", () => Uniforms.Make($"bandit{outfit}{archer}"));
                }
            }
            return null;
        }

        // What a server calls a bandit, as Resolve reads it.
        private static string BanditKey(int look, bool archer) => $"bandit{look}:{archer}";

        // What's thrown by someone with nothing about them to say what it looks like. A hero's own is the real thing, and takes this one's place: see ThrownComponent.
        private static Mesh ThrownThing(string kind)
        {
            if (!Enum.TryParse(kind, out GearKind gear)) return Ball(new Colour(0.42f, 0.4f, 0.37f));
            var spec = new GearSpec(1, gear, 0f, 0f, 0f, GearForm.Middling, 0, GearTheme.None);
            return GearDisplay.Lying(GearForge.Make(spec), gear).Build(bones: false);
        }

        /// <summary>Whether a server means one of the land's own animals, a wolf, a bear or a boar, which are heard and hit as animals are. Each stands on a rig of its own.</summary>
        public static bool IsBeast(string key) => BoarSpec.Parse(key) != null || WolfSpec.Parse(key) != null || BearSpec.Parse(key) != null;

        /// <summary>The joints of an animal, shared by everyone of its kind. A dinosaur's are its own, as each is grown from its seed.</summary>
        public static Rig RigOf(string key)
        {
            lock (Rigs)
            {
                if (!Rigs.TryGetValue(key, out Rig? rig))
                {
                    rig = SaurianSpec.Parse(key) is SaurianSpec dinosaur ? SaurianForge.RigOf(dinosaur)
                        : BoarSpec.Parse(key) is BoarSpec boar ? BoarForge.RigOf(boar)
                        : WolfSpec.Parse(key) is WolfSpec wolf ? WolfForge.RigOf(wolf)
                        : BearSpec.Parse(key) is BearSpec bear ? BearForge.RigOf(bear)
                        : throw new ArgumentException($"No animal called {key}.");
                    Rigs.Add(key, rig);
                }
                return rig;
            }
        }

        private static readonly Dictionary<string, Rig> Rigs = new();

        /// <summary>How something a server names carries itself: what it's built like, how it moves and what's in its hands.</summary>
        public readonly record struct Bearing(Rig Rig, MotionStyle Style, Carry Carry, bool Shield, Carry? Second = null);

        // What a Minion holds, as the meshes of its gear, the second in the left hand. A turret given nothing looses arrows, so it's seen with a plain bow.
        private static List<(GearSpec spec, float side)> Held((ulong Seed, uint Traits)? weapon, (ulong Seed, uint Traits)? offHand, bool hovers = false)
        {
            var held = new List<(GearSpec spec, float side)>();
            if (weapon is (ulong seed, uint traits)) held.Add((Progression.ItemLook.SpecOf(seed, traits), -1f));
            if (offHand is (ulong other, uint looks)) held.Add((Progression.ItemLook.SpecOf(other, looks), 1f));
            if (hovers && held.Count == 0) held.Add((PlainBow, -1f));
            return held;
        }

        private static readonly GearSpec PlainBow = new GearSpec(77, GearKind.Bow, 0f, 1f, 0f, new GearForm(0.45f, 0.5f, 0.15f, 0.4f, 0.12f, 0.45f), 0, GearTheme.None);

        // How a Minion carries what it holds, as a hero would.
        private static Bearing HeldBy(Rig rig, MotionStyle style, (ulong Seed, uint Traits)? weapon, (ulong Seed, uint Traits)? offHand)
        {
            Carry carry = weapon is (_, uint traits) ? SkillMotions.CarryOf(traits) : Carry.Nothing;
            Carry? second = null;
            bool shield = false;
            if (offHand is (ulong other, uint looks))
            {
                second = SkillMotions.SecondOf(other, looks);
                shield = second == null;
            }
            return new Bearing(rig, style, carry, shield, second);
        }

        public static Bearing BearingOf(string key)
        {
            if (SaurianSpec.Parse(key) is SaurianSpec dinosaur) return new Bearing(RigOf(key), SaurianForge.StyleOf(dinosaur), Carry.Nothing, false);
            if (BoarSpec.Parse(key) != null) return new Bearing(RigOf(key), MotionStyle.Boar, Carry.Nothing, false);
            if (WolfSpec.Parse(key) != null) return new Bearing(RigOf(key), MotionStyle.Hound, Carry.Nothing, false);
            if (BearSpec.Parse(key) != null) return new Bearing(RigOf(key), MotionStyle.Bear, Carry.Nothing, false);

            Rig person = Rig.Humanoid;
            if (key == ReichPrefix + "marksman") return new Bearing(person, MotionStyle.Armoured, Carry.Bow, false);
            if (key == ReichPrefix + "legionary") return new Bearing(person, MotionStyle.Armoured, Carry.Spear, false);
            if (key == ReichPrefix + "wrangler") return new Bearing(person, MotionStyle.Brute, Carry.Spear, false);
            if (key == "skeleton") return new Bearing(person, MotionStyle.Dead, Carry.Sword, true);
            if (Gameplay.MinionLooks.TryParse(key, out var weapon, out var offHand))
            {
                // A turret's weapon is held by nobody, as steadily as a hero holds theirs, and a plain bow when it was given nothing.
                if (Gameplay.MinionLooks.Hovers(key)) return weapon == null && offHand == null ? new Bearing(person, MotionStyle.Hero, Carry.Bow, false) : HeldBy(person, MotionStyle.Hero, weapon, offHand);
                return HeldBy(person, MotionStyle.Dead, weapon, offHand);
            }
            if (key == "skeleton:bow") return new Bearing(person, MotionStyle.Dead, Carry.Bow, false);
            if (key is "warlord" or "revenant") return new Bearing(person, MotionStyle.Armoured, Carry.Sword, true);
            if (key.StartsWith("robed:")) return new Bearing(person, MotionStyle.Robed, Carry.Staff, false);
            if (key.StartsWith("acolyte:")) return new Bearing(person, MotionStyle.Outlaw, Carry.Sword, false);
            if (key.StartsWith("cutthroat")) return new Bearing(person, MotionStyle.Cutthroat, Carry.Sword, false);
            if (key.StartsWith("brute")) return new Bearing(person, MotionStyle.Brute, key == "brute2" ? Carry.Spear : Carry.Sword, false);
            if (key.StartsWith("hero")) return new Bearing(person, MotionStyle.Hero, Carry.Nothing, false);
            if (key.StartsWith("villager")) return new Bearing(person, MotionStyle.Villager, Carry.Nothing, false);
            if (key.StartsWith("bandit"))
            {
                int colon = key.IndexOf(':');
                if (colon > 6 && int.TryParse(key.AsSpan(6, colon - 6), out int bandit))
                {
                    int look = Math.Abs(bandit) % BanditLooks;
                    if (key.EndsWith("True")) return new Bearing(person, MotionStyle.Outlaw, Carry.Bow, false);
                    return new Bearing(person, MotionStyle.Outlaw, look % 2 == 0 ? Carry.Sword : Carry.Spear, look == 1);
                }
            }
            return new Bearing(person, MotionStyle.Villager, Carry.Nothing, false);
        }

        // What each power's servants wear. Deep colours, as anything pale goes white under the sun.
        private static Vector3 RobeOf(string element) => element switch
        {
            "Fire" => new Vector3(0.32f, 0.06f, 0.02f),
            "Cold" => new Vector3(0.05f, 0.15f, 0.3f),
            "Lightning" => new Vector3(0.09f, 0.09f, 0.3f),
            "Poison" => new Vector3(0.08f, 0.2f, 0.05f),
            _ => new Vector3(0.17f, 0.05f, 0.24f),
        };

        private static Vector3 TrimOf(string element) => element switch
        {
            "Fire" => new Vector3(0.75f, 0.32f, 0.04f),
            "Cold" => new Vector3(0.3f, 0.55f, 0.75f),
            "Lightning" => new Vector3(0.75f, 0.65f, 0.12f),
            "Poison" => new Vector3(0.4f, 0.6f, 0.1f),
            _ => new Vector3(0.55f, 0.2f, 0.7f),
        };

        // The Saurian Reich's own, dressed in its livery (Uniforms). Read LORE.md.
        public const string ReichPrefix = "reich:";

        /// <summary>A hero with nothing on but what they wear under everything, which is the only thing that still says whose hero it is.</summary>
        public static HumanoidLook HeroLook(int index) => new HumanoidLook
        {
            Tunic = HeroDyes[Math.Abs(index) % HeroDyes.Length],
            Legs = new Vector3(0.1f, 0.09f, 0.1f),
            Trim = new Vector3(0.62f, 0.16f, 0.1f),
            Head = Headwear.Hair,
        };

        public glModel Villager(int index) => Resolve($"villager{index}");

        private static Mesh Ball(Colour colour)
        {
            return IcoSphereGenerator.CreateIcosphere(1, new VertexMaterial(TextureGenerator.flatGlow, colour));
        }

        private const float CoinRadius = 0.1f;
        private const float CoinThickness = 0.026f;

        // Gold as it lies: a heap of coins slumped outwards, a couple of short stacks beside it and a few that rolled off. Each coin is shaded metal with a glowing face, so the pile still
        // shines as it lies there but reads as coins. Its base is LootShow.GoldLift below its middle, so whatever light it carries sits up off the ground.
        private static Mesh GoldPile()
        {
            var builder = new MeshBuilder();
            var random = new Random(7);
            float ground = -LootShow.GoldLift + 0.005f;

            // The heap, in rings, each smaller and higher than the one under it, the lower ones leaning out.
            (int count, float ring, float tilt)[] layers = { (11, 0.24f, 0.4f), (8, 0.16f, 0.32f), (5, 0.08f, 0.22f), (1, 0f, 0.05f) };
            for (int layer = 0; layer < layers.Length; layer++)
            {
                (int count, float ring, float tilt) = layers[layer];
                float turn = (float)random.NextDouble() * MathF.Tau;
                for (int i = 0; i < count; i++)
                {
                    float angle = turn + i * MathF.Tau / count + ((float)random.NextDouble() - 0.5f) * 0.4f;
                    Vector3 outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                    float lean = tilt + ((float)random.NextDouble() - 0.5f) * 0.15f;
                    Vector3 facing = Vector3.UnitY * MathF.Cos(lean) + outward * MathF.Sin(lean);
                    float height = ground + layer * 0.045f + MathF.Sin(lean) * CoinRadius * 0.7f + CoinThickness * 0.5f;
                    Coin(builder, outward * (ring + ((float)random.NextDouble() - 0.5f) * 0.03f) + Vector3.UnitY * height, facing);
                }
            }

            // Two short stacks, a little crooked.
            foreach ((float x, float z, int coins) in new[] { (0.34f, -0.13f, 6), (-0.28f, 0.24f, 4) })
            {
                for (int i = 0; i < coins; i++)
                {
                    Vector3 drift = new Vector3(((float)random.NextDouble() - 0.5f) * 0.016f, 0f, ((float)random.NextDouble() - 0.5f) * 0.016f);
                    Coin(builder, new Vector3(x, ground + CoinThickness * (i + 0.5f), z) + drift, Vector3.UnitY);
                }
            }

            // And a few that got away, lying nearly flat.
            foreach (float angle in new[] { 0.6f, 2.3f, 3.4f, 4.6f, 5.5f })
            {
                float reach = 0.36f + (float)random.NextDouble() * 0.1f;
                Vector3 outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                Vector3 facing = (Vector3.UnitY + outward * ((float)random.NextDouble() * 0.12f)).Normalized();
                Coin(builder, outward * reach + Vector3.UnitY * (ground + CoinThickness * 0.5f + 0.004f), facing);
            }
            return builder.Build();
        }

        private static readonly VertexMaterial CoinMetal = new VertexMaterial(TextureGenerator.iron, new Colour(0.86f, 0.62f, 0.18f));
        private static readonly VertexMaterial CoinShine = new VertexMaterial(TextureGenerator.flatGlow, new Colour(1f, 0.76f, 0.26f, 2.2f));

        // One coin, its middle at centre, its face towards facing: a metal disc with a glowing face just inside its rim, proud of the metal so the rim stays a darker edge.
        private static void Coin(MeshBuilder builder, Vector3 centre, Vector3 facing)
        {
            Vector3 half = facing * (CoinThickness * 0.5f);
            builder.Tube(centre - half, centre + half, CoinRadius, CoinRadius, 14, CoinMetal);
            builder.Tube(centre + half, centre + half + facing * 0.003f, CoinRadius * 0.8f, CoinRadius * 0.8f, 14, CoinShine);
        }

        private static Mesh Arrow()
        {
            var builder = new MeshBuilder();
            builder.Tube(new Vector3(0f, 0f, -0.9f), new Vector3(0f, 0f, 0.9f), 0.05f, 0.05f, 4, new VertexMaterial(TextureGenerator.wood, new Colour(150, 120, 90)));
            builder.Tube(new Vector3(0f, 0f, 0.9f), new Vector3(0f, 0f, 1.2f), 0.1f, 0.01f, 4, new VertexMaterial(TextureGenerator.iron));
            return builder.Build();
        }

        private readonly Dictionary<string, (WaygateDesign Design, WayLook Look)?> _ways = new();

        /// <summary>
        /// What a way a server names looks like (WayKeys, WAYGATES.md): the gate it fills and what's seen in it, grown from the land it goes to. Kept once grown, as a way is seen for as long
        /// as it's open, and nothing if the name means nothing to this client.
        /// </summary>
        public (WaygateDesign Design, WayLook Look)? WayOf(string key)
        {
            if (_ways.TryGetValue(key, out var known)) return known;
            (WaygateDesign Design, WayLook Look)? way = null;
            if (WayKeys.TryRead(key, out WaygateDesign design, out WorldRequest destination, out ulong seed))
            {
                try
                {
                    way = (design, WayLook.Of(WorldPlan.Create(destination), seed));
                }
                catch (ArgumentException)
                {
                    // A newer server could open a way to a kind of land this client has never heard of: it still opens, as the way home does.
                    Console.WriteLine($"No land to show in the way '{key}'.");
                    way = (design, WayLook.Of(WorldPlan.Create(Progression.Lands.Haven), seed));
                }
            }
            _ways[key] = way;
            return way;
        }

        // Straw on a post, to be hit: a cross of planks to stand on, a bundle tied twice, a sack for a head and a target painted where the heart would be. Its front is +z.
        private static Mesh Dummy()
        {
            var builder = new MeshBuilder();
            VertexMaterial wood = new VertexMaterial(TextureGenerator.wood, new Colour(118, 86, 58));
            VertexMaterial straw = new VertexMaterial(TextureGenerator.thatch, new Colour(226, 196, 128));
            VertexMaterial rope = new VertexMaterial(TextureGenerator.cloth, new Colour(70, 52, 34));
            VertexMaterial sack = new VertexMaterial(TextureGenerator.cloth, new Colour(176, 152, 112));
            VertexMaterial red = new VertexMaterial(TextureGenerator.cloth, new Colour(150, 30, 24));
            VertexMaterial white = new VertexMaterial(TextureGenerator.cloth, new Colour(222, 212, 190));

            builder.Box(new Vector3(0f, 0.07f, 0f), new Vector3(0.85f, 0.07f, 0.11f), wood);
            builder.Box(new Vector3(0f, 0.07f, 0f), new Vector3(0.11f, 0.07f, 0.85f), wood);
            builder.Tube(new Vector3(0f, 0f, 0f), new Vector3(0f, 2.25f, 0f), 0.09f, 0.08f, 6, wood);
            builder.Beam(new Vector3(-0.78f, 1.72f, 0f), new Vector3(0.78f, 1.72f, 0f), 0.055f, wood);

            builder.Tube(new Vector3(0f, 0.85f, 0f), new Vector3(0f, 1.9f, 0f), 0.36f, 0.31f, 10, straw);
            foreach (float side in new[] { -1f, 1f }) builder.Tube(new Vector3(side * 0.28f, 1.72f, 0f), new Vector3(side * 0.74f, 1.68f, 0f), 0.11f, 0.08f, 6, straw);
            foreach (float y in new[] { 1.08f, 1.62f }) builder.Tube(new Vector3(0f, y - 0.03f, 0f), new Vector3(0f, y + 0.03f, 0f), 0.37f, 0.37f, 10, rope);

            builder.Add(IcoSphereGenerator.CreateIcosphere(1, sack), new Vector3(0f, 2.2f, 0f), new Vector3(0.25f, 0.29f, 0.25f));
            builder.Tube(new Vector3(0f, 1.96f, 0f), new Vector3(0f, 2.01f, 0f), 0.15f, 0.15f, 8, rope);

            // Painted on, a little proud of the straw, so it's the first thing seen of it.
            (float radius, VertexMaterial paint)[] rings = { (0.25f, red), (0.17f, white), (0.08f, red) };
            for (int i = 0; i < rings.Length; i++)
            {
                float z = 0.36f + i * 0.012f;
                builder.Tube(new Vector3(0f, 1.38f, z), new Vector3(0f, 1.38f, z + 0.01f), rings[i].radius, rings[i].radius, 14, rings[i].paint);
            }
            return builder.Build();
        }

        // What a Defender sets down to fight for them: a timber tripod lashed with rope, under a drum with a grey iron band, which is what a turret's weapon hangs in the air over. Nothing on it is
        // the Reich's colours, and nothing glows. It never turns: the weapon over it does.
        private static Mesh Turret()
        {
            var builder = new MeshBuilder();
            VertexMaterial wood = new VertexMaterial(TextureGenerator.wood, new Colour(124, 92, 62));
            VertexMaterial dark = new VertexMaterial(TextureGenerator.wood, new Colour(88, 64, 44));
            VertexMaterial rope = new VertexMaterial(TextureGenerator.cloth, new Colour(150, 128, 92));
            VertexMaterial iron = new VertexMaterial(TextureGenerator.iron, new Colour(128, 130, 136));

            // Three legs, splayed, meeting under the head, and lashed where they meet.
            Vector3 top = new Vector3(0f, 1.25f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * MathF.Tau / 3f + MathF.PI / 6f;
                Vector3 foot = new Vector3(MathF.Cos(angle) * 0.85f, 0f, MathF.Sin(angle) * 0.85f);
                builder.Tube(foot, top, 0.07f, 0.06f, 6, wood);
                builder.Box(foot + new Vector3(0f, 0.04f, 0f), new Vector3(0.1f, 0.04f, 0.1f), iron);
            }
            builder.Tube(new Vector3(0f, 1.1f, 0f), new Vector3(0f, 1.32f, 0f), 0.13f, 0.13f, 8, rope);

            // The drum on top, banded, with a dished top, empty: what's over it isn't held by anything.
            builder.Tube(new Vector3(0f, 1.25f, 0f), new Vector3(0f, 1.55f, 0f), 0.34f, 0.38f, 12, dark);
            builder.Tube(new Vector3(0f, 1.36f, 0f), new Vector3(0f, 1.44f, 0f), 0.375f, 0.375f, 12, iron);
            builder.Tube(new Vector3(0f, 1.55f, 0f), new Vector3(0f, 1.6f, 0f), 0.4f, 0.4f, 12, wood);
            return builder.Build();
        }

        // A chest that's been opened is its box, empty and dark inside, with nothing glowing on it any more: its lid is a model of its own, on its hinge.
        private static Mesh Chest(bool open)
        {
            var builder = new MeshBuilder();
            VertexMaterial boards = new VertexMaterial(TextureGenerator.wood, new Colour(120, 84, 56));
            VertexMaterial iron = new VertexMaterial(TextureGenerator.iron, new Colour(90, 90, 96));
            VertexMaterial glow = new VertexMaterial(TextureGenerator.flatGlow, new Colour(1f, 0.4f, 0.02f, 1.6f));
            if (open)
            {
                VertexMaterial inside = new VertexMaterial(TextureGenerator.wood, new Colour(46, 32, 22));
                const float Wall = 0.07f;
                builder.Box(new Vector3(0f, 0.2f, 0f), new Vector3(1f, 0.2f, 0.66f), boards);
                builder.Box(new Vector3(0f, 0.41f, 0f), new Vector3(1f - Wall, 0.01f, 0.66f - Wall), inside);
                foreach (float z in new[] { -1f, 1f })
                {
                    builder.Box(new Vector3(0f, 0.8f, z * (0.66f - Wall * 0.5f)), new Vector3(1f, 0.4f, Wall * 0.5f), boards);
                }
                foreach (float x in new[] { -1f, 1f })
                {
                    builder.Box(new Vector3(x * (1f - Wall * 0.5f), 0.8f, 0f), new Vector3(Wall * 0.5f, 0.4f, 0.66f - Wall), boards);
                    builder.Box(new Vector3(x * 0.7f, 0.6f, 0.68f), new Vector3(0.09f, 0.6f, 0.03f), iron);
                }
                builder.Box(new Vector3(0f, 1.1f, 0.7f), new Vector3(0.14f, 0.12f, 0.05f), iron);
                return builder.Build();
            }

            builder.Box(new Vector3(0f, 0.6f, 0f), new Vector3(1f, 0.6f, 0.66f), boards);
            builder.Surface(6, 1, (u, v) =>
            {
                float angle = u * MathF.PI;
                return new Vector3((v * 2f - 1f) * 1f, 1.2f + MathF.Sin(angle) * 0.34f, MathF.Cos(angle) * 0.66f);
            }, boards);
            foreach (float x in new[] { -0.7f, 0.7f })
            {
                builder.Box(new Vector3(x, 0.8f, 0.68f), new Vector3(0.09f, 0.76f, 0.03f), iron);

                // Over the lid, as the lid is most of what's seen of a chest from up where the camera is.
                builder.Surface(6, 1, (u, v) =>
                {
                    float angle = u * MathF.PI;
                    return new Vector3(x + (v * 2f - 1f) * 0.1f, 1.2f + MathF.Sin(angle) * 0.37f, MathF.Cos(angle) * 0.69f);
                }, glow);
            }
            builder.Box(new Vector3(0f, 1.1f, 0.7f), new Vector3(0.14f, 0.18f, 0.05f), glow);
            return builder.Build();
        }

        // The lid alone, thrown back, from its hinge along the back of the top (ChestLook.Hinge), which is where its model starts, so that it turns about that. Iron where the shut one glowed.
        private static Mesh ChestLid()
        {
            var builder = new MeshBuilder();
            VertexMaterial boards = new VertexMaterial(TextureGenerator.wood, new Colour(120, 84, 56));
            VertexMaterial inside = new VertexMaterial(TextureGenerator.wood, new Colour(70, 48, 32));
            VertexMaterial iron = new VertexMaterial(TextureGenerator.iron, new Colour(90, 90, 96));
            Vector3 hinge = ChestLook.Hinge;
            builder.Surface(6, 1, (u, v) =>
            {
                float angle = u * MathF.PI;
                return new Vector3((v * 2f - 1f) * 1f, 1.2f + MathF.Sin(angle) * 0.34f, MathF.Cos(angle) * 0.66f) - hinge;
            }, boards);
            foreach (float x in new[] { -1f, 1f })
            {
                // Its ends, which a shut chest hides and an open one shows.
                builder.Surface(6, 1, (u, v) =>
                {
                    float angle = u * MathF.PI;
                    return new Vector3(x, 1.2f + MathF.Sin(angle) * 0.34f * v, MathF.Cos(angle) * 0.66f) - hinge;
                }, boards);
                builder.Surface(6, 1, (u, v) =>
                {
                    float angle = u * MathF.PI;
                    return new Vector3(x * 0.7f + (v * 2f - 1f) * 0.1f, 1.2f + MathF.Sin(angle) * 0.37f, MathF.Cos(angle) * 0.69f) - hinge;
                }, iron);
            }
            builder.Box(new Vector3(0f, 0.015f, 0.66f), new Vector3(1f, 0.015f, 0.66f), inside, closeBottom: true);
            return builder.Build();
        }
    }
}
