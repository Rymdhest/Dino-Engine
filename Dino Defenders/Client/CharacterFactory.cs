using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Modelling.Procedural.Architecture;
using Dino_Engine.Modelling.Procedural.Character;
using Dino_Engine.Modelling.Procedural.Indoor;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
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
                    for (int variant = 0; variant < kind.Variants; variant++) Resolve($"{kind.Look}{variant}");
                }
                else Resolve(kind.Look);
            }
        }

        /// <summary>The model a server means by <paramref name="key"/>.</summary>
        public glModel Resolve(string key)
        {
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
            if (key == "chest") return Model(key, Chest);
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

        /// <summary>The animal a server means by <paramref name="key"/>, if it means one. Those stand on a rig of their own.</summary>
        public static BeastLook? BeastOf(string key) => Beasts.GetValueOrDefault(key);

        /// <summary>The joints of an animal, shared by everyone of its kind.</summary>
        public static Matrix4[] RigOf(string key)
        {
            if (!Rigs.TryGetValue(key, out Matrix4[]? rig))
            {
                rig = BeastGenerator.Rig(Beasts[key]);
                Rigs.Add(key, rig);
            }
            return rig;
        }

        private static readonly Dictionary<string, Matrix4[]> Rigs = new();

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
