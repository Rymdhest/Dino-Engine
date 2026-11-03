using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Indoor;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace Dino_Defenders
{
    internal class ArpgGame : Game
    {
        private readonly GameOptions _options;
        private readonly WorldAssetLibrary _assets = new();
        private glModel? _heroModel;
        private Entity _player;
        private int _frames;
        private readonly List<(string name, Vector2 position)> _tourStops = new();
        private int _tourStop = -1;
        private int _tourWait;
        private bool _tourPhotographed;

        public ArpgGame(Engine engine, GameOptions options) : base(engine)
        {
            _options = options;
            EnterWorld(new WorldRequest(options.Seed ?? Environment.TickCount, options.Archetype));
        }

        public override void update()
        {
            _frames++;
            if (_options.Shot != null && _frames == _options.ShotAfterFrames)
            {
                Engine.RequestScreenshot(_options.Shot, () => Dino_Engine.Core.Engine.WindowHandler.Close());
            }
            if (_options.Tour != null) UpdateTour();
        }

        // Moves the player to each stop, waits for the streaming to settle, photographs it and moves on.
        private void UpdateTour()
        {
            if (_frames < 60 || --_tourWait > 0) return;

            // The photograph comes first, with a frame of its own: moving on in the same frame would photograph the jump.
            if (_tourStop >= 0 && !_tourPhotographed)
            {
                string path = Path.Combine(_options.Tour!, _tourStops[_tourStop].name + ".png");
                Engine.RequestScreenshot(path);
                _tourPhotographed = true;
                _tourWait = 2;
                return;
            }

            _tourPhotographed = false;
            _tourStop++;
            if (_tourStop >= _tourStops.Count)
            {
                Dino_Engine.Core.Engine.WindowHandler.Close();
                return;
            }

            Vector2 stop = _tourStops[_tourStop].position;
            var view = Engine.world.GetEntityView(_player);
            var position = view.Get<PositionComponent>();
            position.value = new Vector3(stop.X, position.value.Y, stop.Y);
            view.Set(position);
            _tourWait = 40;
        }

        private void PlanTour(WorldData world)
        {
            var seen = new HashSet<SiteKind>();
            foreach (WorldSite site in world.Sites)
            {
                if (seen.Add(site.Kind)) _tourStops.Add((site.Kind.ToString(), site.Position));
            }

            // Some of the wild places between the sites, picked by what is there.
            AddStop(world, "Forest", p => world.Canopy.Sample(p) > 0.85f);
            AddStop(world, "ForestEdge", p => world.Canopy.Sample(p) is > 0.3f and < 0.55f);
            AddStop(world, "Open", p => world.Canopy.Sample(p) < 0.02f && world.Height.SlopeAt(p) < 0.15f && world.Height.Sample(p) > 4f);
            AddStop(world, "Shore", p => world.Height.Sample(p) is > 0.3f and < 1f);
            AddStop(world, "Hillside", p => world.Height.SlopeAt(p) is > 0.5f and < 0.8f && world.Passable.IsSet(p));
            foreach (WorldRoad road in world.Roads)
            {
                if (road.Class != RoadClass.Highway || road.Points.Count < 60) continue;
                _tourStops.Add(("Road", road.Points[road.Points.Count / 2].Xz));
                break;
            }
        }

        private void AddStop(WorldData world, string name, Func<Vector2, bool> wanted)
        {
            var random = new WorldRandom(world.Plan.Seed).Fork(name);
            for (int attempt = 0; attempt < 4000; attempt++)
            {
                Vector2 p = new Vector2(random.Float(), random.Float()) * world.Size;
                if (world.Reachable.Sample(p) < 0.5f || !wanted(p)) continue;
                _tourStops.Add((name, p));
                return;
            }
        }

        private void EnterWorld(WorldRequest request)
        {
            var clock = Stopwatch.StartNew();
            WorldData world = new WorldGenerator().Generate(request);
            Console.WriteLine($"Generated {world.Plan.Name} ({world.Plan.Archetype.Name}, {world.Plan.Mood.Name}) in {clock.Elapsed.TotalSeconds:0.00} s");

            clock.Restart();
            WorldScene.Enter(Engine.world, world, _assets);
            Console.WriteLine($"Prepared {_assets.Count} models in {clock.Elapsed.TotalSeconds:0.00} s");

            SpawnPlayer(world);
            if (_options.Tour != null) PlanTour(world);
        }

        private void SpawnPlayer(WorldData world)
        {
            ECSWorld ecs = Engine.world;
            Vector2 start = StartPosition(world);
            float height = world.Height.Sample(start);

            _heroModel ??= glLoader.loadToVAO(SkeletonGenerator.GenerateSkeleton());

            _player = ecs.CreateEntity("Player",
                new PositionComponent(new Vector3(start.X, height, start.Y)),
                new RotationComponent(new Vector3(0f)),
                new ScaleComponent(new Vector3(1f)),
                new CharacterModelComponent(_heroModel),
                new AnimationComponent(),
                new SkeletonComponent(),
                new LocalToWorldMatrixComponent(),
                new VelocityComponent(),
                new FixToTerrainHeightComponent(),
                new GrassDisplaceComponent(2f),
                new WaterDisturberComponent(0.45f),
                new PlayerTag());

            float look = _options.Look ?? 30f;
            ecs.GameCamera = ecs.CreateEntity("Camera",
                new PositionComponent(0f, look, -look * 0.43f),
                new RotationComponent(MathF.PI / 2.7f, MathF.PI, 0f),
                new MainCameraTag(),
                new LocalToWorldMatrixComponent(),
                new ViewMatrixComponent(),
                new ParentComponent(_player, ignoreParentRotation: true),
                new PerspectiveProjectionComponent(MathHelper.DegreesToRadians(60f), Dino_Engine.Core.Engine.Resolution, 0.1f, 1000f));

            ecs.FreeCamera = ecs.CreateEntity("Free Camera",
                new PositionComponent(0f, 0f, 0f),
                new RotationComponent(),
                new MainCameraTag(),
                new LocalToWorldMatrixComponent(),
                new PositionRotationInputControlComponent(),
                new ViewMatrixComponent(),
                new PerspectiveProjectionComponent(MathHelper.DegreesToRadians(60f), Dino_Engine.Core.Engine.Resolution, 0.1f, 1000f));

            ecs.Camera = ecs.GameCamera;
        }

        private Vector2 StartPosition(WorldData world)
        {
            if (_options.At.HasValue) return _options.At.Value;
            if (_options.Site != null)
            {
                foreach (WorldSite site in world.Sites)
                {
                    if (site.Kind.ToString() == _options.Site) return site.Position;
                }
            }
            return world.Sites[0].Position;
        }
    }
}
