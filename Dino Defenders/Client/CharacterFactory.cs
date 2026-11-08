using Dino_Engine.Animation;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Modelling.Procedural.Architecture;
using Dino_Engine.Modelling.Procedural.Character;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Indoor;
using Dino_Engine.Modelling.Procedural.Saurian;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
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

        private const int VillagerLooks = 6;
        private const int BanditLooks = 4;

        // So that friends can tell each other apart in a fight.
        private static readonly Vector3[] HeroDyes =
        {
            new Vector3(0.09f, 0.14f, 0.26f), new Vector3(0.26f, 0.08f, 0.07f), new Vector3(0.08f, 0.2f, 0.11f),
            new Vector3(0.24f, 0.18f, 0.05f), new Vector3(0.18f, 0.08f, 0.24f),
        };

        private readonly Dictionary<string, glModel> _models = new();

        // Dinosaurs are grown, which takes a few hundred milliseconds each, so they're begun the moment there's a factory, while the menu and the first world's generation go on. They
        // take half the cores, as the world takes longer with fewer and they were done before it was. A world only waits for what isn't done when it's entered, and only the upload is left for this thread.
        private Task<(string key, Mesh mesh)[]>? _growing = Task.Run(() => DinosaurKeys().AsParallel().WithDegreeOfParallelism(Math.Max(Environment.ProcessorCount / 2, 1))
            .Select(key => (key, SaurianForge.Make(SaurianSpec.Parse(key)!.Value))).ToArray());

        // Every dinosaur a server can send: each kind's look, with each of its seeds.
        private static IEnumerable<string> DinosaurKeys()
        {
            foreach (Server.EnemyKind kind in Server.Bestiary.Everyone)
            {
                if (SaurianSpec.Parse(kind.Look) == null) continue;
                if (kind.Variants == 0) yield return kind.Look;
                for (int variant = 0; variant < kind.Variants; variant++) yield return $"{kind.Look}{variant}";
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
            foreach (Server.EnemyKind kind in Server.Bestiary.Everyone)
            {
                if (kind.Look == "bandit")
                {
                    for (int look = 0; look < BanditLooks; look++)
                    {
                        Bandit(look, archer: false);
                        Bandit(look, archer: true);
                    }
                }
                else if (kind.Variants > 0)
                {
                    for (int variant = 0; variant < kind.Variants; variant++) Queue($"{kind.Look}{variant}");
                }
                else Queue(kind.Look);
            }

            void Queue(string key)
            {
                if (SaurianSpec.Parse(key) == null) Resolve(key);
            }

            if (_growing == null) return;
            foreach ((string key, Mesh mesh) in _growing.Result)
            {
                if (!_models.ContainsKey(key)) _models[key] = glLoader.loadToVAO(mesh);
            }
            _growing = null;
        }

        /// <summary>The model a server means by <paramref name="key"/>.</summary>
        public glModel Resolve(string key)
        {
            if (SaurianSpec.Parse(key) is SaurianSpec dinosaur) return Model(key, () => SaurianForge.Make(dinosaur));
            if (key.StartsWith(ReichPrefix)) return Model(key, () => HumanoidGenerator.Generate(ReichLook(key.Substring(ReichPrefix.Length))));
            if (key == "skeleton") return Skeleton;
            if (key == "skeleton:bow") return Model(key, SkeletonGenerator.GenerateArcher);
            if (key == "warlord") return Warlord;
            if (key == "revenant") return Model(key, () => HumanoidGenerator.Generate(RevenantLook));
            if (BeastOf(key) is BeastLook beast) return Model(key, () => BeastGenerator.Generate(beast));
            if (key.StartsWith("robed:")) return Model(key, () => HumanoidGenerator.Generate(RobedLook(key.Substring(6))));
            if (key.StartsWith("acolyte:")) return Model(key, () => HumanoidGenerator.Generate(AcolyteLook(key.Substring(8))));
            if (key.StartsWith("cutthroat") && int.TryParse(key.AsSpan(9), out int cutthroat)) return Model(key, () => HumanoidGenerator.Generate(CutthroatLook(cutthroat)));
            if (key.StartsWith("brute") && int.TryParse(key.AsSpan(5), out int brute)) return Model(key, () => HumanoidGenerator.Generate(BruteLook(brute)));
            if (key == "arrow") return Model(key, Arrow);
            if (key.StartsWith(Books.Thrown.Prefix)) return Model(key, () => ThrownThing(key.Substring(Books.Thrown.Prefix.Length)));
            if (key == "chest") return Model(key, Chest);
            if (key == "dummy") return Model(key, Dummy);
            if (key == Server.ArenaSystem.PortcullisModel) return Model(key, Portcullis);
            if (key.StartsWith(PortalPrefix) && int.TryParse(key.AsSpan(PortalPrefix.Length), out int biome)) return Model(key, () => Portal(biome));
            if (key == Progression.LootModels.Gold) return Model(key, () => Ball(new Colour(1f, 0.78f, 0.25f, 2.2f)));
            if (key == Progression.LootModels.Find) return Model(key, () => Ball(new Colour(0f, 0f, 0f)));
            if (key.StartsWith(BoltPrefix) && Enum.TryParse(key.AsSpan(BoltPrefix.Length), out Stats.DamageType look))
            {
                Colour glow = ClientEffects.GlowOf(look);
                return Model(key, () => Ball(new Colour(glow.Red, glow.Green, glow.Blue, 3f)));
            }
            if (key.StartsWith("hero") && int.TryParse(key.AsSpan(4), out int hero)) return Hero(hero);
            if (key.StartsWith("villager") && int.TryParse(key.AsSpan(8), out int villager)) return Villager(villager);
            if (key.StartsWith("bandit"))
            {
                int colon = key.IndexOf(':');
                if (colon > 6 && int.TryParse(key.AsSpan(6, colon - 6), out int bandit)) return Bandit(bandit, key.EndsWith("True"));
            }

            // A newer server than this client could name something it has never heard of. Better a stranger than a crash.
            Console.WriteLine($"No model called '{key}'.");
            return Villager(0);
        }

        // What's thrown by someone with nothing about them to say what it looks like. A hero's own is the real thing, and takes this one's place: see ThrownComponent.
        private static Mesh ThrownThing(string kind)
        {
            if (!Enum.TryParse(kind, out GearKind gear)) return Ball(new Colour(0.42f, 0.4f, 0.37f));
            var spec = new GearSpec(1, gear, 0f, 0f, 0f, GearForm.Middling, 0, GearTheme.None);
            return GearDisplay.Lying(GearForge.Make(spec), gear).Build(bones: false);
        }

        /// <summary>The animal a server means by <paramref name="key"/>, if it means one. Those stand on a rig of their own.</summary>
        public static BeastLook? BeastOf(string key) => Beasts.GetValueOrDefault(key);

        /// <summary>The joints of an animal, shared by everyone of its kind. A dinosaur's are its own, as each is grown from its seed.</summary>
        public static Rig RigOf(string key)
        {
            lock (Rigs)
            {
                if (!Rigs.TryGetValue(key, out Rig? rig))
                {
                    rig = SaurianSpec.Parse(key) is SaurianSpec dinosaur ? SaurianForge.RigOf(dinosaur) : BeastGenerator.RigOf(Beasts[key]);
                    Rigs.Add(key, rig);
                }
                return rig;
            }
        }

        private static readonly Dictionary<string, Rig> Rigs = new();

        /// <summary>How something a server names carries itself: what it's built like, how it moves and what's in its hands.</summary>
        public readonly record struct Bearing(Rig Rig, MotionStyle Style, Carry Carry, bool Shield);

        public static Bearing BearingOf(string key)
        {
            if (SaurianSpec.Parse(key) is SaurianSpec dinosaur) return new Bearing(RigOf(key), SaurianForge.StyleOf(dinosaur), Carry.Nothing, false);
            if (BeastOf(key) != null)
            {
                MotionStyle moves = key == "boar" ? MotionStyle.Boar : key == "bear" ? MotionStyle.Bear : MotionStyle.Hound;
                return new Bearing(RigOf(key), moves, Carry.Nothing, false);
            }

            Rig person = Rig.Humanoid;
            if (key == ReichPrefix + "marksman") return new Bearing(person, MotionStyle.Armoured, Carry.Bow, false);
            if (key == ReichPrefix + "legionary") return new Bearing(person, MotionStyle.Armoured, Carry.Spear, false);
            if (key == ReichPrefix + "wrangler") return new Bearing(person, MotionStyle.Brute, Carry.Spear, false);
            if (key == "skeleton") return new Bearing(person, MotionStyle.Dead, Carry.Sword, true);
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

        private static readonly Dictionary<string, BeastLook> Beasts = new()
        {
            ["wolf"] = new BeastLook(),
            ["direwolf"] = new BeastLook
            {
                Height = 2.3f, Length = 4f, Girth = 0.72f, Skull = 0.46f, Muzzle = 0.66f, Tail = 1.6f, Ears = 0.3f, Hump = 0.14f, Neck = 0.95f,
                Coat = new Vector3(0.17f, 0.16f, 0.2f), Belly = new Vector3(0.26f, 0.24f, 0.28f),
            },
            ["boar"] = new BeastLook
            {
                Height = 1.35f, Length = 2.9f, Girth = 0.82f, Skull = 0.46f, Muzzle = 0.56f, Tail = 0.4f, Ears = 0.16f, Tusks = 0.36f, Hump = 0.28f, Neck = 0.42f,
                Coat = new Vector3(0.42f, 0.24f, 0.13f), Belly = new Vector3(0.5f, 0.34f, 0.22f),
            },
            ["bear"] = new BeastLook
            {
                Height = 2.3f, Length = 3.9f, Girth = 1.25f, Skull = 0.62f, Muzzle = 0.44f, Tail = 0f, Ears = 0.17f, Hump = 0.34f, Neck = 0.8f,
                Coat = new Vector3(0.34f, 0.2f, 0.1f), Belly = new Vector3(0.42f, 0.27f, 0.15f),
            },
        };

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

        private static HumanoidLook RobedLook(string element) => new HumanoidLook
        {
            Tunic = RobeOf(element),
            Legs = RobeOf(element) * 0.6f,
            Trim = TrimOf(element),
            Head = Headwear.Hood,
            HeadColour = RobeOf(element) * 0.7f,
            Skin = element == "Magic" ? new Vector3(0.36f, 0.35f, 0.34f) : new Vector3(0.62f, 0.44f, 0.32f),
            LongGarment = true,
            Held = HeldItem.Spear,
            Bulk = 0.96f,
        };

        private static HumanoidLook AcolyteLook(string element) => new HumanoidLook
        {
            Tunic = RobeOf(element) * 0.75f,
            Legs = new Vector3(0.09f, 0.08f, 0.08f),
            Trim = TrimOf(element),
            Head = Headwear.Hood,
            HeadColour = RobeOf(element),
            Held = HeldItem.Sword,
            Bulk = 0.94f,
        };

        private static HumanoidLook CutthroatLook(int look)
        {
            Vector3[] leathers = { new Vector3(0.11f, 0.08f, 0.06f), new Vector3(0.08f, 0.09f, 0.07f), new Vector3(0.13f, 0.1f, 0.07f), new Vector3(0.07f, 0.06f, 0.08f) };
            Vector3 leather = leathers[Math.Abs(look) % leathers.Length];
            return new HumanoidLook
            {
                Tunic = leather,
                Legs = leather * 0.8f,
                Trim = new Vector3(0.3f, 0.06f, 0.05f),
                Head = look % 2 == 0 ? Headwear.Hood : Headwear.Hair,
                HeadColour = leather * 0.8f,
                Held = HeldItem.Sword,
                Bulk = 0.92f,
            };
        }

        private static HumanoidLook BruteLook(int look) => new HumanoidLook
        {
            Tunic = new Vector3(0.16f, 0.1f, 0.06f),
            Legs = new Vector3(0.1f, 0.07f, 0.05f),
            Trim = new Vector3(0.45f, 0.08f, 0.06f),
            Head = look == 1 ? Headwear.Hair : Headwear.Helm,
            HeadColour = new Vector3(0.07f, 0.05f, 0.04f),
            Held = look == 2 ? HeldItem.Spear : HeldItem.Sword,
            Armoured = look == 0,
            Bulk = 1.34f,
        };

        public const string ReichPrefix = "reich:";

        // The Saurian Reich's own: greycoats in charcoal and oxblood under iron helms, and its beast-handlers in oxblood leather. Nobody else wears these colours. Read LORE.md.
        private static readonly Vector3 Greycoat = new Vector3(0.07f, 0.07f, 0.075f);
        private static readonly Vector3 Oxblood = new Vector3(0.28f, 0.03f, 0.03f);

        private static HumanoidLook ReichLook(string who) => who switch
        {
            "wrangler" => new HumanoidLook
            {
                Tunic = new Vector3(0.16f, 0.035f, 0.03f), Legs = Greycoat * 0.8f, Boots = new Vector3(0.04f, 0.035f, 0.03f), Trim = new Vector3(0.42f, 0.3f, 0.11f),
                Head = Headwear.Hood, HeadColour = new Vector3(0.08f, 0.03f, 0.025f), Held = HeldItem.Spear, Armoured = true, Bulk = 1.22f,
            },
            "marksman" => new HumanoidLook
            {
                Tunic = Greycoat, Legs = Greycoat * 0.85f, Boots = new Vector3(0.035f, 0.03f, 0.03f), Trim = Oxblood,
                Head = Headwear.Helm, HeadColour = new Vector3(0.09f, 0.09f, 0.1f), Held = HeldItem.Bow, LongGarment = true, Bulk = 1f,
            },
            _ => new HumanoidLook
            {
                Tunic = Greycoat, Legs = Greycoat * 0.85f, Boots = new Vector3(0.035f, 0.03f, 0.03f), Trim = Oxblood,
                Head = Headwear.Helm, HeadColour = new Vector3(0.09f, 0.09f, 0.1f), Held = HeldItem.Spear, Armoured = true, LongGarment = true, Bulk = 1.06f,
            },
        };

        private static readonly HumanoidLook RevenantLook = new HumanoidLook
        {
            Skin = new Vector3(0.33f, 0.32f, 0.3f),
            Tunic = new Vector3(0.08f, 0.08f, 0.1f),
            Legs = new Vector3(0.06f, 0.06f, 0.07f),
            Boots = new Vector3(0.06f, 0.06f, 0.07f),
            Trim = new Vector3(0.2f, 0.26f, 0.32f),
            Head = Headwear.Helm,
            Held = HeldItem.Sword,
            Shield = true,
            Armoured = true,
            Bulk = 1.14f,
        };

        private glModel Hero(int index)
        {
            int dye = Math.Abs(index) % HeroDyes.Length;
            return Model($"hero{dye}", () => HumanoidGenerator.Generate(HeroLook(dye)));
        }

        /// <summary>A hero with nothing on but what they wear under everything, which is the only thing that still says whose hero it is.</summary>
        public static HumanoidLook HeroLook(int index) => new HumanoidLook
        {
            Tunic = HeroDyes[Math.Abs(index) % HeroDyes.Length],
            Legs = new Vector3(0.1f, 0.09f, 0.1f),
            Trim = new Vector3(0.62f, 0.16f, 0.1f),
            Head = Headwear.Hair,
        };

        public glModel Skeleton => Model("skeleton", SkeletonGenerator.GenerateSkeleton);

        public glModel Warlord => Model("warlord", () => HumanoidGenerator.Generate(new HumanoidLook
        {
            Tunic = new Vector3(0.24f, 0.05f, 0.06f),
            Legs = new Vector3(0.08f, 0.07f, 0.08f),
            Trim = new Vector3(0.7f, 0.56f, 0.2f),
            Head = Headwear.Helm,
            Held = HeldItem.Sword,
            Shield = true,
            Armoured = true,
            Bulk = 1.2f,
        }));

        public glModel Villager(int index)
        {
            int look = Math.Abs(index) % VillagerLooks;
            return Model($"villager{look}", () => HumanoidGenerator.Generate(VillagerLook(look)));
        }

        public glModel Bandit(int index, bool archer)
        {
            int look = Math.Abs(index) % BanditLooks;
            return Model($"bandit{look}{archer}", () => HumanoidGenerator.Generate(BanditLook(look, archer)));
        }

        private static HumanoidLook VillagerLook(int look)
        {
            var random = new WorldRandom(0x7011).Fork(look);
            Vector3[] dyes =
            {
                new Vector3(0.28f, 0.21f, 0.13f), new Vector3(0.14f, 0.19f, 0.14f), new Vector3(0.3f, 0.26f, 0.2f),
                new Vector3(0.16f, 0.14f, 0.2f), new Vector3(0.32f, 0.17f, 0.11f), new Vector3(0.22f, 0.24f, 0.26f),
            };
            Headwear[] heads = { Headwear.StrawHat, Headwear.Hair, Headwear.Hood, Headwear.StrawHat, Headwear.Hair, Headwear.Hood };
            return new HumanoidLook
            {
                Tunic = dyes[look],
                Legs = dyes[(look + 2) % dyes.Length] * 0.7f,
                Skin = new Vector3(0.62f, 0.44f, 0.32f) * random.Range(0.8f, 1.1f),
                Head = heads[look],
                HeadColour = heads[look] == Headwear.Hood ? dyes[(look + 3) % dyes.Length] : new Vector3(0.07f, 0.05f, 0.04f),
                LongGarment = look % 2 == 1,
                Bulk = random.Range(0.9f, 1.05f),
            };
        }

        private static HumanoidLook BanditLook(int look, bool archer)
        {
            Vector3[] rags =
            {
                new Vector3(0.2f, 0.09f, 0.07f), new Vector3(0.13f, 0.11f, 0.09f), new Vector3(0.17f, 0.15f, 0.1f), new Vector3(0.1f, 0.08f, 0.09f),
            };
            return new HumanoidLook
            {
                Tunic = rags[look],
                Legs = rags[(look + 1) % rags.Length] * 0.8f,
                Trim = new Vector3(0.55f, 0.1f, 0.08f),
                Head = look % 2 == 0 ? Headwear.Hood : Headwear.Helm,
                HeadColour = new Vector3(0.1f, 0.08f, 0.08f),
                Held = archer ? HeldItem.Bow : look % 2 == 0 ? HeldItem.Sword : HeldItem.Spear,
                Shield = !archer && look == 1,
                Bulk = 1.06f,
            };
        }

        private static Mesh Ball(Colour colour)
        {
            return IcoSphereGenerator.CreateIcosphere(1, new VertexMaterial(TextureGenerator.flatGlow, colour));
        }

        private static Mesh Arrow()
        {
            var builder = new MeshBuilder();
            builder.Tube(new Vector3(0f, 0f, -0.9f), new Vector3(0f, 0f, 0.9f), 0.05f, 0.05f, 4, new VertexMaterial(TextureGenerator.wood, new Colour(150, 120, 90)));
            builder.Tube(new Vector3(0f, 0f, 0.9f), new Vector3(0f, 0f, 1.2f), 0.1f, 0.01f, 4, new VertexMaterial(TextureGenerator.iron));
            return builder.Build();
        }

        public const string PortalPrefix = "portal:";

        /// <summary>The colour the way to a land is lit in: its first biome's, made bright enough to glow and saturated enough to keep its hue.</summary>
        public static Colour PortalColour(int biome)
        {
            Vector3 colour = biome >= 0 && biome < BiomeCatalog.All.Count ? BiomeCatalog.All[biome].MapColour : new Vector3(0.35f, 0.85f, 1f);

            // A land's colour on the map is soft, and light that soft goes white. Pushed away from grey it keeps its hue however bright it is.
            float grey = (colour.X + colour.Y + colour.Z) / 3f;
            colour = Vector3.ComponentMax(new Vector3(grey) + (colour - new Vector3(grey)) * PortalSaturation, new Vector3(0.02f));
            float most = MathF.Max(colour.X, MathF.Max(colour.Y, colour.Z));
            return new Colour(colour / MathF.Max(most, 0.01f));
        }

        private const float PortalSaturation = 2.4f;

        // The way to a land, open in the Haven's great waygate: layers of light filling its arch, a deep rim of the colour of where it leads brightening to its heart. It matches the arch in PropGenerator.
        private static Mesh Portal(int biome)
        {
            const float Span = 5.35f;
            const float Spring = 7.1f;
            Colour hue = PortalColour(biome);
            var builder = new MeshBuilder();
            (float reach, float strength, float depth, float tone)[] layers = { (1f, 0.45f, 0f, 0.5f), (0.78f, 0.75f, 0.06f, 0.75f), (0.52f, 1.1f, 0.12f, 0.92f), (0.26f, 1.6f, 0.18f, 1f) };
            foreach ((float reach, float strength, float depth, float tone) in layers)
            {
                var light = new VertexMaterial(TextureGenerator.flatGlow, new Colour(hue.Red * tone, hue.Green * tone, hue.Blue * tone, strength));
                float radius = Span * reach;
                float floor = 1.05f + (1f - reach) * 2.6f;
                foreach (float face in new[] { depth, -depth })
                {
                    builder.Surface(18, 6, (u, v) =>
                    {
                        float angle = MathF.PI * (face >= 0f ? 1f - u : u);
                        return new Vector3(MathF.Cos(angle) * radius * v, Spring + MathF.Sin(angle) * radius * v, face);
                    }, light);
                    if (face >= 0f) builder.Quad(new Vector3(-radius, floor, face), new Vector3(radius, floor, face), new Vector3(radius, Spring, face), new Vector3(-radius, Spring, face), light);
                    else builder.Quad(new Vector3(radius, floor, face), new Vector3(-radius, floor, face), new Vector3(-radius, Spring, face), new Vector3(radius, Spring, face), light);
                }
            }
            return builder.Build();
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

        // An arena gate's portcullis, the Reich's black iron: upright bars through cross bars, with spikes at the foot, filling the gate's opening. It matches the gate in PropGenerator, and the
        // server lifts the whole of it into the block above.
        private static Mesh Portcullis()
        {
            const float HalfWidth = 1.6f;
            const float Tall = 3.8f;
            const int Uprights = 7;
            var builder = new MeshBuilder();
            VertexMaterial iron = new VertexMaterial(TextureGenerator.iron, new Colour(34, 32, 34));
            for (int i = 0; i < Uprights; i++)
            {
                float x = -HalfWidth + (i + 0.5f) * (HalfWidth * 2f / Uprights);
                builder.Box(new Vector3(x, Tall * 0.5f + 0.1f, 0f), new Vector3(0.07f, Tall * 0.5f - 0.1f, 0.07f), iron);
                builder.Box(new Vector3(x, 0.1f, 0f), new Vector3(0.05f, 0.1f, 0.05f), iron);
            }
            foreach (float y in new[] { 0.9f, 2f, 3.1f })
            {
                builder.Box(new Vector3(0f, y, 0f), new Vector3(HalfWidth, 0.06f, 0.09f), iron);
            }
            return builder.Build();
        }

        private static Mesh Chest()
        {
            var builder = new MeshBuilder();
            VertexMaterial boards = new VertexMaterial(TextureGenerator.wood, new Colour(120, 84, 56));
            VertexMaterial iron = new VertexMaterial(TextureGenerator.iron, new Colour(90, 90, 96));
            VertexMaterial glow = new VertexMaterial(TextureGenerator.flatGlow, new Colour(1f, 0.4f, 0.02f, 1.6f));
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
    }
}
