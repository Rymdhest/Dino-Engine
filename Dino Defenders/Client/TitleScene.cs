using Dino_Defenders.Progression;
using Dino_Engine.Animation;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Physics;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>Where a hero stands to be looked at, and which way the camera looks at them.</summary>
    public readonly record struct TitleSpot(Vector2 At, Vector2 Facing);

    /// <summary>
    /// The place behind the main menu: a real spot in a real world, with nobody in it but the hero who's chosen. It's a world like any other, drawn by everything that draws one, so nothing here needs a server: the
    /// hero is one body, standing at their ease in what they have on, and the camera is close enough to see them. Everything in the game that has a session to read simply finds there isn't one.
    /// </summary>
    public sealed class TitleScene
    {
        /// <summary>The land it's all in. Changing either of these is changing what everyone first sees.</summary>
        public const int Seed = 7;
        public const string? Archetype = null;

        /// <summary>How far the terrain goes, as the view here is nearly level and the game's camera never is.</summary>
        public const int ChunkRadius = 20;

        private const float Distance = 11.5f;           // Metres from the hero.
        private const float Height = 4.4f;              // Of the camera, above the hero's feet.
        private const float LookHeight = 3.6f;          // Of what it looks at.
        private const float Beside = 2.6f;              // The hero stands to one side of the picture, as the other is the menu's.
        private const float DriftReach = 0.28f;         // Metres. A held breath is what a still picture looks like, so it's never quite still.
        private const float DriftRate = 0.13f;
        private const float HeroTurn = 2.5f;            // Radians from straight at the camera: nearly away from it, looking out over all of that.
        private const int Tries = 12000;
        private const float Far = 1400f;

        private readonly ECSWorld _ecs;
        private readonly WorldData _world;
        private readonly Wardrobe _wardrobe;
        private readonly CharacterFactory _characters;
        private readonly TitleSpot _spot;
        private readonly Vector3 _feet;

        private Entity _body;
        private List<WornLook> _worn = new();
        private float _time;

        public TitleScene(ECSWorld ecs, WorldData world, Wardrobe wardrobe, CharacterFactory characters, TitleSpot? spot = null)
        {
            _ecs = ecs;
            _world = world;
            _wardrobe = wardrobe;
            _characters = characters;
            _spot = spot ?? FindSpot(world);
            _feet = new Vector3(_spot.At.X, world.Height.Sample(_spot.At), _spot.At.Y);

            _ecs.GameCamera = _ecs.CreateEntity("Camera",
                new PositionComponent(CameraAt()),
                new RotationComponent(),
                new MainCameraTag(),
                new LocalToWorldMatrixComponent(),
                new ViewMatrixComponent(),
                new PerspectiveProjectionComponent(MathHelper.DegreesToRadians(42f), Engine.Resolution, 0.1f, Far));
            _ecs.Camera = _ecs.GameCamera;
            _ecs.ApplyDeferredCommands();
            Aim();
        }

        /// <summary>
        /// The best place in a world to stand. It's looked for rather than written down, as a change to how worlds grow would move anything written down. What makes a picture here is depth: open ground to stand on,
        /// nothing at all between them and the camera, and water beyond them with the far shore a long way off, as a view that ends at the first trees has nowhere to look.
        /// </summary>
        public static TitleSpot FindSpot(WorldData world)
        {
            var random = new WorldRandom(world.Plan.Seed).Fork("title");
            Vector3 towardsSun = -world.Plan.Mood.SunDirection;
            TitleSpot best = new TitleSpot(world.Sites[0].Position, Vector2.UnitY);
            float bestScore = float.NegativeInfinity;

            for (int attempt = 0; attempt < Tries; attempt++)
            {
                Vector2 at = new Vector2(random.Float(), random.Float()) * world.Size;
                float level = world.Height.Sample(at);
                if (world.Reachable.Sample(at) < 0.5f || level < WorldPlan.SeaLevel + 1.5f || world.Height.SlopeAt(at) > 0.18f) continue;

                float turn = random.Float() * MathF.Tau;
                Vector2 facing = new Vector2(MathF.Sin(turn), MathF.Cos(turn));
                Vector2 side = new Vector2(-facing.Y, facing.X);
                if (!Clear(world, at, facing, side)) continue;

                // Land that goes on a little in front of them, and then water, for as far as there is.
                float water = 0f;
                for (float metres = 34f; metres < 150f; metres += 8f)
                {
                    if (world.Height.Sample(at - facing * metres) < WorldPlan.SeaLevel) water++;
                }
                float score = water;
                if (world.Height.Sample(at - facing * 22f) < WorldPlan.SeaLevel) score -= 6f;
                if (world.Height.Sample(at - facing * 220f) > WorldPlan.SeaLevel + 1f) score += 3f;

                // The ground falling or rising between them is a picture of the ground.
                score -= MathF.Abs(world.Height.Sample(at + facing * Distance) - level) * 0.6f;

                // Lit from the side, on the camera's side: full on is flat, and from in front of them they're a shape.
                float lit = towardsSun.X * facing.X + towardsSun.Z * facing.Y;
                score -= MathF.Abs(lit - 0.45f) * 4f;

                if (score <= bestScore) continue;
                bestScore = score;
                best = new TitleSpot(at, facing);
            }
            return best;
        }

        // Nothing standing anywhere between the camera and a little way past the hero, or to either side of that.
        private static bool Clear(WorldData world, Vector2 at, Vector2 facing, Vector2 side)
        {
            for (float along = -12f; along <= Distance + 4f; along += 3f)
            {
                for (float across = -9f; across <= 12f; across += 3f)
                {
                    Vector2 point = at + facing * along + side * across;
                    if (!world.Passable.IsSet(point) || world.Canopy.Sample(point) > 0.02f) return false;
                }
            }
            return true;
        }

        private Vector3 CameraAt()
        {
            Vector2 side = new Vector2(-_spot.Facing.Y, _spot.Facing.X);
            float drift = MathF.Sin(_time * DriftRate * MathF.Tau) * DriftReach;
            float rise = MathF.Sin(_time * DriftRate * 0.7f * MathF.Tau + 1f) * DriftReach * 0.4f;
            Vector2 ground = _spot.At + _spot.Facing * Distance + side * (Beside + drift);
            return new Vector3(ground.X, _feet.Y + Height + rise, ground.Y);
        }

        // Looks past the hero, so that they stand to the right of the picture.
        private void Aim()
        {
            Vector3 from = CameraAt();
            Vector2 side = new Vector2(-_spot.Facing.Y, _spot.Facing.X);
            Vector2 middle = _spot.At + side * Beside;
            Vector3 towards = new Vector3(middle.X, _feet.Y + LookHeight, middle.Y) - from;
            float yaw = MathF.Atan2(towards.X, towards.Z);
            float pitch = -MathF.Atan2(towards.Y, towards.Xz.Length);

            EntityView camera = _ecs.GetEntityView(_ecs.GameCamera);
            camera.Set(new PositionComponent(from));
            camera.Set(new RotationComponent(pitch, MathF.PI - yaw, 0f));
        }

        /// <summary>Puts someone on the spot, or nobody.</summary>
        public void Show(HeroState? hero)
        {
            if (hero == null)
            {
                if (_body.IsValid()) _ecs.DestroyEntity(_body);
                _body = default;
                return;
            }

            _worn = hero.Equipped.Select(pair => new WornLook(pair.Key, pair.Value.LookSeed(), ItemLook.Traits(pair.Value))).ToList();
            if (_body.IsValid()) return;
            _body = Stand();
            _ecs.ApplyDeferredCommands();
        }

        private Entity Stand()
        {
            const string Model = "hero0";
            CharacterFactory.Bearing bearing = CharacterFactory.BearingOf(Model);
            var animator = new CharacterAnimator(bearing.Rig, bearing.Style, 1);
            animator.Hold(bearing.Carry, bearing.Shield);

            float yaw = MathF.Atan2(_spot.Facing.X, _spot.Facing.Y) + HeroTurn;
            return _ecs.CreateEntity("Title Hero",
                new PositionComponent(_feet),
                new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, yaw)),
                new ScaleComponent(new Vector3(1f)),
                new LocalToWorldMatrixComponent(),
                new CharacterModelComponent(_characters.Resolve(Model)),
                new AnimatorComponent(animator),
                new SkeletonComponent(bearing.Rig, animator.Skinning),
                new PlantContactComponent(2.7f));
        }

        /// <summary>Goes on every frame: the camera breathes, and what the hero's wearing keeps being asked for, which is what keeps it alive.</summary>
        public void Update(float deltaTime)
        {
            _time += deltaTime;
            Aim();

            // What a game focuses on is its own business, and it's left there when it ends.
            Engine.RenderEngine.context.focusPoint = Vector4.Zero;
            HoldTheWeather(deltaTime);

            if (!_body.IsValid() || !_ecs.TryGetEntityLocation(_body, out _, out _)) return;
            if (_wardrobe.Dressed(0, _worn) is not Dino_Engine.Modelling.Model.glModel model) return;

            EntityView body = _ecs.GetEntityView(_body);
            if (body.Get<CharacterModelComponent>().model == model) return;
            body.Set(new CharacterModelComponent(model));

            Carry carry = Carry.Nothing;
            bool shield = false;
            foreach (WornLook look in _worn)
            {
                if (look.Place == WornSlot.MainHand) carry = SkillMotions.CarryOf(ItemLook.WeaponOf(look.Traits));
                else if (look.Place == WornSlot.OffHand) shield = true;
            }
            body.Get<AnimatorComponent>().Animator.Hold(carry, shield);
        }

        // It's always a fine day here: a first look at a game shouldn't depend on the clock.
        private void HoldTheWeather(float deltaTime)
        {
            EntityView view = _ecs.GetEntityView(_ecs.GetSingleton<WeatherSingleton>());
            var weather = view.Get<WeatherSingleton>();
            weather.Clock += deltaTime;
            weather.ClockGiven = true;
            if (WeatherSample.TryNamed("clear", out WeatherSample clear)) weather.Forced = clear;
            view.Set(weather);
        }
    }
}
