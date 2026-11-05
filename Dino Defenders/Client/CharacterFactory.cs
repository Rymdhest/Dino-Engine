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

        /// <summary>The model a server means by <paramref name="key"/>.</summary>
        public glModel Resolve(string key)
        {
            if (key == "skeleton") return Skeleton;
            if (key == "warlord") return Warlord;
            if (key == "arrow") return Model(key, Arrow);
            if (key == "chest") return Model(key, Chest);
            if (key == "gold") return Model(key, () => Ball(new Colour(1f, 0.78f, 0.25f, 2.2f)));
            if (key == "item") return Model(key, () => Ball(new Colour(1f, 1f, 1f, 2.2f)));
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

        private glModel Hero(int index)
        {
            int dye = Math.Abs(index) % HeroDyes.Length;
            return Model($"hero{dye}", () => HumanoidGenerator.Generate(new HumanoidLook
            {
                Tunic = HeroDyes[dye],
                Legs = new Vector3(0.1f, 0.09f, 0.1f),
                Trim = new Vector3(0.62f, 0.16f, 0.1f),
                Head = Headwear.Hair,
                Held = HeldItem.Sword,
                Shield = true,
                Armoured = true,
            }));
        }

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
            builder.Box(new Vector3(0f, 0.6f, 0f), new Vector3(1f, 0.6f, 0.66f), boards);
            builder.Surface(6, 1, (u, v) =>
            {
                float angle = u * MathF.PI;
                return new Vector3((v * 2f - 1f) * 1f, 1.2f + MathF.Sin(angle) * 0.34f, MathF.Cos(angle) * 0.66f);
            }, boards);
            foreach (float x in new[] { -0.7f, 0.7f }) builder.Box(new Vector3(x, 0.8f, 0.68f), new Vector3(0.09f, 0.76f, 0.03f), iron);
            builder.Box(new Vector3(0f, 1.1f, 0.7f), new Vector3(0.14f, 0.18f, 0.05f), iron);
            return builder.Build();
        }
    }
}
