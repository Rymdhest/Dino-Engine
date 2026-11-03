using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Character;
using Dino_Engine.Modelling.Procedural.Indoor;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Gameplay
{
    /// <summary>Makes the people of a world. Their models are built once and shared, so a crowd costs little more than a person.</summary>
    public sealed class CharacterFactory
    {
        private const int VillagerLooks = 6;
        private const int BanditLooks = 4;

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

        public glModel Hero => Model("hero", () => HumanoidGenerator.Generate(new HumanoidLook
        {
            Tunic = new Vector3(0.09f, 0.14f, 0.26f),
            Legs = new Vector3(0.1f, 0.09f, 0.1f),
            Trim = new Vector3(0.62f, 0.16f, 0.1f),
            Head = Headwear.Hair,
            Held = HeldItem.Sword,
            Shield = true,
            Armoured = true,
        }));

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

        /// <summary>The components every walking character has.</summary>
        public static IComponent[] Body(string name, glModel model, Vector3 position, float scale, params IComponent[] extra)
        {
            var components = new List<IComponent>
            {
                new PositionComponent(position),
                new RotationComponent(new Vector3(0f)),
                new ScaleComponent(new Vector3(scale)),
                new CharacterModelComponent(model),
                new AnimationComponent(),
                new SkeletonComponent(),
                new LocalToWorldMatrixComponent(),
                new VelocityComponent(),
                new FixToTerrainHeightComponent(),
                new GrassDisplaceComponent(1.4f * scale),
                new WaterDisturberComponent(0.42f * scale),
                new CharacterMotorComponent(WorldData.CharacterRadius * MathF.Min(scale, 1.4f)),
            };
            components.AddRange(extra);
            return components.ToArray();
        }

        public static void Face(EntityView entity, Vector2 direction, float deltaTime, float speed = 14f)
        {
            if (direction.LengthSquared < 1e-5f) return;
            var rotation = entity.Get<RotationComponent>();
            Quaternion target = Quaternion.FromAxisAngle(Vector3.UnitY, MathF.Atan2(direction.X, direction.Y));
            rotation.quaternion = Quaternion.Slerp(rotation.quaternion, target, Math.Clamp(speed * deltaTime, 0f, 1f));
            entity.Set(rotation);
        }
    }
}
