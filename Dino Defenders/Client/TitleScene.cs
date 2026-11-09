using Dino_Defenders.Progression;
using Dino_Engine.Animation;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Physics;
using Dino_Engine.Sound;
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
        private const float Fov = 42f;                  // Degrees, up and down the picture.

        // The Haven's giants live in the picture too, beyond the hero (GiantShow).
        private const float HerdReach = 45f;            // Metres the herd keeps within of the middle of its range,
        private const float HerdNearest = 75f;          // which is this far from the camera at least,
        private const float HerdFurthest = 270f;        // and at most: near enough to be seen as what they are, and far enough off to be seen whole.
        private const float ViewNearest = 110f;         // Metres from the camera that somewhere for them is looked for from, in choosing a spot.
        private const int HerdSeen = 3;                 // Places found for them that make a picture with them in it,
        private const float HerdWorth = 8f;             // worth this against a picture's other merits,
        private const int HerdPlenty = 10;              // and a little more for each more found, up to this many.
        private const float HerdMore = 0.5f;
        private const float HerdKeepsOff = 55f;         // Metres from the camera no giant comes nearer than.
        private const float HerdInView = 0.5f;          // Radians either side of where the camera looks that they keep to, which is inside the picture's edges.
        private const float HerdLooked = -0.05f;        // Radians to the right of the middle of the picture that it's best for them to be: a little left of it, between the menu and the hero.

        private readonly ECSWorld _ecs;
        private readonly WorldData _world;
        private readonly Wardrobe _wardrobe;
        private readonly CharacterFactory _characters;
        private readonly TitleSpot _spot;
        private readonly Vector3 _feet;

        /// <summary>Where the hero stands, and which way the camera looks at them.</summary>
        public TitleSpot Spot => _spot;

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
                new PerspectiveProjectionComponent(MathHelper.DegreesToRadians(Fov), Engine.Resolution, 0.1f, Far));
            _ecs.Camera = _ecs.GameCamera;
            _ecs.ApplyDeferredCommands();
            Aim();
        }

        /// <summary>
        /// The best place in a world to stand. It's looked for rather than written down, as a change to how worlds grow would move anything written down. What makes a picture here is depth: open ground to stand on,
        /// nothing at all between them and the camera, and water beyond them with the far shore a long way off, as a view that ends at the first trees has nowhere to look. And somewhere in the picture,
        /// across the water or along the shore, open ground with trees on it near enough to see who lives there: the Haven's giants (<see cref="FindHerd"/>).
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
                int herd = HerdView(world, new TitleSpot(at, facing));
                if (herd >= HerdSeen) score += HerdWorth + MathF.Min(herd, HerdPlenty) * HerdMore;

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

        /// <summary>Where the camera is, holding its breath, for a world and a spot.</summary>
        private static Vector3 CameraOf(WorldData world, TitleSpot spot)
        {
            Vector2 side = new Vector2(-spot.Facing.Y, spot.Facing.X);
            Vector2 ground = spot.At + spot.Facing * Distance + side * Beside;
            return new Vector3(ground.X, world.Height.Sample(spot.At) + Height, ground.Y);
        }

        /// <summary>
        /// Where the giants live, in the picture: their range is a circle a little off the middle of it beyond the hero, far enough off to be seen whole, on open ground with trees in it to eat from,
        /// with nothing between it and the camera, and they keep inside the picture's edges and away from the camera. It's looked for, as the spot is. Nothing in a world with nowhere like that.
        /// </summary>
        public static (Vector2 Home, float Reach, Func<Vector2, bool> Allowed)? FindHerd(WorldData world, TitleSpot spot)
        {
            Vector3 camera = CameraOf(world, spot);
            Vector2 look = -spot.Facing;
            Vector2 right = new Vector2(-look.Y, look.X);
            Vector2 from = camera.Xz;

            float bestScore = float.NegativeInfinity;
            Vector2 best = default;
            for (float distance = HerdNearest; distance <= HerdFurthest; distance += 6f)
            {
                for (float angle = -HerdInView * 0.6f; angle <= HerdInView * 0.6f; angle += 0.04f)
                {
                    Vector2 at = from + (look * MathF.Cos(angle) + right * MathF.Sin(angle)) * distance;
                    if (!Standable(world, at)) continue;

                    // Trees scattered about open ground: something to eat, and room between to walk.
                    int trees = 0, open = 0;
                    for (int k = 0; k < 16; k++)
                    {
                        float turn = k * MathF.Tau / 16f;
                        Vector2 near = at + new Vector2(MathF.Cos(turn), MathF.Sin(turn)) * (k % 2 == 0 ? HerdReach * 0.45f : HerdReach * 0.8f);
                        float canopy = world.Canopy.Sample(near);
                        if (canopy > 0.12f && canopy < 0.75f) trees++;
                        else if (canopy < 0.08f && Standable(world, near)) open++;
                    }
                    if (trees < 2 || open < 6) continue;

                    // Seen whole: nothing between the camera and their backs, ground or trees.
                    float hidden = Hidden(world, camera, at, world.Height.Sample(at) + 10f);
                    if (hidden > 0.25f) continue;

                    float score = MathF.Min(trees, 6) + open * 0.4f - hidden * 20f - MathF.Abs(angle - HerdLooked) * 9f - MathF.Abs(distance - 125f) * 0.02f;
                    if (score <= bestScore) continue;
                    bestScore = score;
                    best = at;
                }
            }
            if (float.IsNegativeInfinity(bestScore)) return null;

            // Inside the picture's edges, and no nearer than they'd fill it.
            Func<Vector2, bool> allowed = at =>
            {
                Vector2 off = at - from;
                float distance = off.Length;
                return distance > HerdKeepsOff && Vector2.Dot(off / MathF.Max(distance, 1e-3f), look) > MathF.Cos(HerdInView);
            };
            return (best, HerdReach, allowed);
        }

        // How much of what's in the picture a long way off is ground a herd could live on, trees among open ground with nothing in the way of seeing it, out of a few dozen places looked at.
        private static int HerdView(WorldData world, TitleSpot spot)
        {
            Vector3 camera = CameraOf(world, spot);
            Vector2 look = -spot.Facing;
            Vector2 right = new Vector2(-look.Y, look.X);
            int found = 0;
            for (int a = -3; a <= 4; a++)
            {
                float angle = a * 0.09f;
                Vector2 way = look * MathF.Cos(angle) + right * MathF.Sin(angle);
                for (float distance = ViewNearest; distance <= HerdFurthest; distance += 32f)
                {
                    Vector2 point = camera.Xz + way * distance;
                    if (!Standable(world, point)) continue;
                    float canopy = world.Canopy.Sample(point);
                    float around = MathF.Max(world.Canopy.Sample(point + way * 18f), world.Canopy.Sample(point - way * 18f));
                    if (canopy > 0.5f || around < 0.08f || Hidden(world, camera, point, world.Height.Sample(point) + 10f) > 0.25f) continue;
                    found++;
                }
            }
            return found;
        }

        private static bool Standable(WorldData world, Vector2 at) =>
            world.InBounds(at) && world.Height.Sample(at) > WorldPlan.SeaLevel + 1.5f && world.WaterDepthAt(at) < 0.1f && world.Height.SlopeAt(at) < 0.3f && world.Rock.Sample(at) < 0.5f;

        // How much of the way from the camera to a point is in the way of seeing it, as a share of the way from past the hero to just short of it: ground over the line, or a wood.
        private static float Hidden(WorldData world, Vector3 camera, Vector2 at, float height)
        {
            Vector3 to = new Vector3(at.X, height, at.Y);
            float length = (to.Xz - camera.Xz).Length;
            int blocked = 0, samples = 0;
            for (float along = Distance + 10f; along < length - 25f; along += 5f)
            {
                Vector3 point = Vector3.Lerp(camera, to, along / length);
                float ground = world.Height.Sample(point.Xz);
                samples++;
                if (ground > point.Y - 1f || world.Canopy.Sample(point.Xz) > 0.35f && point.Y - ground < 16f) blocked++;
            }
            return samples > 0 ? blocked / (float)samples : 0f;
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
            Listen();

            // What a game focuses on is its own business, and it's left there when it ends.
            Engine.RenderEngine.context.focusPoint = Vector4.Zero;
            HoldTheWeather(deltaTime);

            if (!_body.IsValid() || !_ecs.TryGetEntityLocation(_body, out _, out _)) return;
            if (_wardrobe.Dressed(0, _worn) is not Dino_Engine.Modelling.Model.glModel model) return;

            EntityView body = _ecs.GetEntityView(_body);
            if (body.Get<CharacterModelComponent>().model == model) return;
            body.Set(new CharacterModelComponent(model));
            SkillMotions.Hold(body.Get<AnimatorComponent>().Animator, _worn);
        }

        // What's heard here is heard from the camera, as there's no hero to be over: the giants' calls carry across to it.
        private void Listen()
        {
            Vector2 side = new Vector2(-_spot.Facing.Y, _spot.Facing.X);
            var forward = new Vector3(-_spot.Facing.X, 0f, -_spot.Facing.Y);
            var right = new Vector3(-side.X, 0f, -side.Y);
            Audio.SetListener(CameraAt(), right, forward, Vector3.UnitY);
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
