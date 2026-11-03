using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.UI;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.ECS.Systems;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Diagnostics;

namespace Dino_Defenders
{
    /// <summary>
    /// The game from the top: carries the hero from world to world. A world is generated on a worker thread behind the loading
    /// screen and swapped in between frames; the hero, and nothing else, comes along.
    /// </summary>
    internal class ArpgGame : Game
    {
        private const float AutosaveInterval = 30f;
        private const float InterfaceHeight = 1000f;
        private const float LoadSearchRadius = 24f;
        private const float ViewSpreadAtTop = 0.967f;   // Half-width of the view at the top of the screen, per metre of camera height and of aspect ratio.
        private const float FurthestZoomAllowed = 1.25f;

        private readonly record struct PreparedWorld(WorldRequest Request, WorldData World, byte[] Map);

        private readonly GameOptions _options;
        private readonly WorldAssetLibrary _assets = new();
        private readonly CharacterFactory _characters = new();
        private readonly GameUi _ui;
        private HeroState _hero;
        private Task<PreparedWorld>? _loading;
        private Entity _player;
        private float _nextAutosave;
        private int _frames;
        private readonly List<(string name, Vector2 position)> _tourStops = new();
        private int _tourStop = -1;
        private int _tourWait;
        private bool _tourPhotographed;

        public ArpgGame(Engine engine, GameOptions options) : base(engine)
        {
            _options = options;
            RegisterSystems();
            _ui = new GameUi(Dino_Engine.Core.Engine.Gui, Travel, Quit, NewJourney);

            SaveGame.PathOverride = options.SaveFile;
            SaveGame? save = options.Saves ? SaveGame.Load() : null;
            _hero = save?.Hero ?? HeroState.CreateNew();
            _hero.Level = Math.Max(_hero.Level, options.HeroLevel);
            _hero.Might += (options.HeroLevel - 1) * 3;

            WorldRequest request = save != null
                ? new WorldRequest(save.WorldSeed, save.WorldArchetype, save.WorldLevel)
                : new WorldRequest(options.Seed ?? Environment.TickCount, options.Archetype, options.Level);

            Enter(Prepare(request), save);
        }

        private void RegisterSystems()
        {
            SystemRegistry.Remove<PlayerInputSystem>();
            SystemRegistry.InsertAfter<CameraSwitchSystem>(new PlayerControlSystem());

            // After the hero's input, and before anything moves: these all act on what it decided.
            SystemRegistry.InsertBefore<GravitySystem>(new EnemyBrainSystem());
            SystemRegistry.InsertBefore<GravitySystem>(new VillagerSystem());
            SystemRegistry.InsertBefore<GravitySystem>(new ProjectileSystem());
            SystemRegistry.InsertBefore<GravitySystem>(new DyingSystem());
            SystemRegistry.InsertBefore<GravitySystem>(new InteractionSystem());
            SystemRegistry.InsertBefore<GravitySystem>(new EncounterSystem(_characters));
            SystemRegistry.InsertBefore<GravitySystem>(new AmbienceSystem());
        }

        public override void update()
        {
            _frames++;
            FinishLoading();
            UpdateInterface();

            GameSession session = GameSession.Current;
            session.Paused = _ui.AnyOpen || _ui.Loading.Visible || _options.Tour != null;

            if (Dino_Engine.Core.Engine.Time >= _nextAutosave)
            {
                _nextAutosave = Dino_Engine.Core.Engine.Time + AutosaveInterval;
                Save();
            }

            if (_frames == _options.TravelAfterFrames) StepThroughWaygate();
            if (_frames == 90 && _options.Panel != null) OpenPanel(_options.Panel);
            if (_options.Shot != null && _frames == _options.ShotAfterFrames)
            {
                Engine.RequestScreenshot(_options.Shot, () =>
                {
                    Report();
                    Quit();
                });
            }
            if (_options.Tour != null) UpdateTour();
        }

        private void UpdateInterface()
        {
            // The interface is laid out for a screen about a thousand units tall, and grows with the window.
            Dino_Engine.GUI.GuiContext gui = Dino_Engine.Core.Engine.Gui;
            float scale = Math.Clamp(Dino_Engine.Core.Engine.Resolution.Y / InterfaceHeight, 0.75f, 3f);
            if (MathF.Abs(gui.Scale - scale) > 0.01f) gui.Scale = scale;

            if (_ui.Loading.Visible) return;

            var window = Dino_Engine.Core.Engine.WindowHandler;
            if (Dino_Engine.Core.Engine.Gui.WantsKeyboard) return;

            if (window.IsKeyPressed(Keys.Escape))
            {
                if (_ui.AnyOpen) _ui.CloseAll();
                else _ui.Toggle(_ui.Pause);
            }
            if (window.IsKeyPressed(Keys.M)) _ui.Toggle(_ui.Map);
            if (window.IsKeyPressed(Keys.C) || window.IsKeyPressed(Keys.I)) _ui.Toggle(_ui.Hero);
            if (window.IsKeyPressed(Keys.J)) _ui.Toggle(_ui.Chronicle);
        }

        private void OpenPanel(string name)
        {
            switch (name)
            {
                case "map": _ui.Toggle(_ui.Map); break;
                case "hero": _ui.Toggle(_ui.Hero); break;
                case "chronicle": _ui.Toggle(_ui.Chronicle); break;
                case "pause": _ui.Toggle(_ui.Pause); break;
                case "waygate": _ui.Open(InteractKind.Waygate, GameSession.Current.Sites[0]); break;
                case "merchant": _ui.Open(InteractKind.Merchant, GameSession.Current.Sites.First(s => s.Site.Kind == SiteKind.Village)); break;
                case "elder": _ui.Open(InteractKind.Elder, GameSession.Current.Sites.First(s => s.Site.Kind == SiteKind.Village)); break;
            }
        }

        // A hard stop for scripted runs: what became of the hero, for reading in the log.
        private void Report()
        {
            GameSession session = GameSession.Current;
            int cleared = session.Sites.Count(s => s.Cleared);
            int fallen = session.Sites.Sum(s => s.Dead.Count);
            int found = session.Sites.Count(s => s.Discovered);
            string objectives = string.Join(", ", session.Chronicle.Objectives.Select(o => $"{o.Progress}/{o.Goal}"));
            Console.WriteLine($"World: {session.World.Plan.Name}, level {session.World.Plan.Level}, {found} sites found, objectives {objectives}, liberated {session.Liberated}");
            Console.WriteLine($"Hero: level {_hero.Level}, {session.Health:0}/{session.Stats.MaxHealth:0} health, {_hero.Gold} gold, {_hero.Inventory.Count} items, {fallen} enemies fallen, {cleared} sites cleared at {session.PlayerPosition.X:0},{session.PlayerPosition.Y:0}");
        }

        private void Save()
        {
            if (!_options.Saves) return;
            var save = new SaveGame();
            GameSession.Current.Capture(save);
            save.Store();
        }

        private void Quit()
        {
            Save();
            Dino_Engine.Core.Engine.WindowHandler.Close();
        }

        private void NewJourney()
        {
            if (_options.Saves) SaveGame.Delete();
            _hero = HeroState.CreateNew();
            Travel(new WorldRequest(Environment.TickCount));
        }

        private void StepThroughWaygate()
        {
            GameSession session = GameSession.Current;
            var random = new WorldRandom(session.World.Plan.Seed).Fork("test road");
            Travel(new WorldRequest(random.Int(int.MaxValue), null, session.World.Plan.Level + 1));
        }

        private void Travel(WorldRequest request)
        {
            if (_loading != null) return;
            Save();
            _ui.CloseAll();

            LoadingScreen screen = _ui.Loading;
            WorldPlan plan = WorldPlan.Create(request);
            screen.Title = plan.Name;
            screen.Detail = $"{plan.Archetype.Name}, level {plan.Level}";
            screen.Progress = 0f;
            screen.Stage = "";
            screen.Visible = true;

            _loading = Task.Run(() => Prepare(request, (progress, stage) =>
            {
                screen.Progress = progress;
                screen.Stage = stage;
            }));
        }

        private void FinishLoading()
        {
            if (_loading == null || !_loading.IsCompleted) return;

            Task<PreparedWorld> finished = _loading;
            _loading = null;
            _ui.Loading.Visible = false;

            if (finished.IsFaulted)
            {
                Console.WriteLine($"World generation failed: {finished.Exception}");
                GameSession.Current.Announce("The way is shut", "That land would not take shape. Try the gate again.");
                return;
            }

            Enter(finished.Result, null);
            Save();
        }

        private static PreparedWorld Prepare(WorldRequest request, Action<float, string>? progress = null)
        {
            var clock = Stopwatch.StartNew();
            WorldData world = new WorldGenerator().Generate(request, progress);
            byte[] map = WorldMapImage.Render(world, MapPanel.Resolution, showSites: false);
            Console.WriteLine($"Generated {world.Plan.Name} ({world.Plan.Archetype.Name}, {world.Plan.Mood.Name}) in {clock.Elapsed.TotalSeconds:0.00} s");
            return new PreparedWorld(request, world, map);
        }

        private void Enter(PreparedWorld prepared, SaveGame? save)
        {
            var clock = Stopwatch.StartNew();
            WorldData world = prepared.World;
            WorldScene.Enter(Engine.world, world, _assets);
            Console.WriteLine($"Prepared {_assets.Count} models in {clock.Elapsed.TotalSeconds:0.00} s");

            var session = new GameSession(_hero, world, prepared.Request);
            EncounterSystem.FindFeatures(session, _assets);
            if (save != null) session.Restore(save);

            session.InteractionOpened = (kind, site) => _ui.Open(kind, session.Sites[site]);
            session.WorldCleared = () => OnWorldCleared(session);
            session.UpdateGuidance();
            GameSession.Current = session;

            _ui.Map.SetImage(prepared.Map);
            SpawnPlayer(session, save);
            if (_options.Tour != null)
            {
                _ui.Hud.Visible = false;
                PlanTour(world);
            }
            else if (save == null)
            {
                session.Announce(world.Plan.Name, session.Chronicle.Prologue, 9f);
            }

            if (_options.Bot)
            {
                var bot = new HeroBot(Engine.world, _options.Sweep, FinishSweep);
                session.Autopilot = () => bot.Think(session);
            }
        }

        private void FinishSweep(HeroBot bot)
        {
            Report();
            Console.WriteLine($"Places the bot couldn't get to: {bot.Unreachable}");
            if (_options.Shot != null) Engine.RequestScreenshot(_options.Shot, Dino_Engine.Core.Engine.WindowHandler.Close);
            else Dino_Engine.Core.Engine.WindowHandler.Close();
        }

        private void OnWorldCleared(GameSession session)
        {
            _hero.WorldsCleared++;
            session.Note("The waygate will reach further now.");
            Save();
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

            GameSession.Current.TravelTo = _tourStops[_tourStop].position;
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

        private void SpawnPlayer(GameSession session, SaveGame? save)
        {
            ECSWorld ecs = Engine.world;
            WorldData world = session.World;
            Vector2 start = StartPosition(world, save);
            float height = world.Height.Sample(start);
            session.PlayerPosition = start;

            _player = ecs.CreateEntity("Player", CharacterFactory.Body("Player", _characters.Hero, new Vector3(start.X, height, start.Y), 1f,
                new PlayerTag(),
                new HeroInvulnerableComponent()));
            session.Player = _player;

            float look = _options.Look ?? 30f;
            Vector3 offset = new Vector3(0f, look, -look * 0.43f);
            ecs.GameCamera = ecs.CreateEntity("Camera",
                new PositionComponent(new Vector3(start.X, height, start.Y) + offset),
                new RotationComponent(MathF.PI / 2.7f, MathF.PI, 0f),
                new MainCameraTag(),
                new LocalToWorldMatrixComponent(),
                new ViewMatrixComponent(),
                new FollowCameraComponent(_player, offset) { MaxZoom = FurthestZoom(look) },
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

        private static bool TryFindGround(WorldData world, Vector2 near, out Vector2 ground)
        {
            ground = near;
            if (world.Passable.IsSet(near)) return true;

            for (float radius = 1.5f; radius <= LoadSearchRadius; radius += 1.5f)
            {
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * (MathF.Tau / 16f);
                    ground = near + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    if (world.Passable.IsSet(ground)) return true;
                }
            }
            return false;
        }

        // How far out the camera can go before the corners of the view pass the end of the streamed terrain. A wide window sees further
        // to the sides at the top of the screen, so it reaches that sooner.
        private static float FurthestZoom(float cameraHeight)
        {
            Vector2i resolution = Dino_Engine.Core.Engine.Resolution;
            float aspect = (float)resolution.X / MathF.Max(resolution.Y, 1f);
            float terrainReach = (WorldScene.ChunkRadius + 0.5f) * WorldScene.ChunkSize;
            return Math.Clamp(terrainReach / (ViewSpreadAtTop * cameraHeight * aspect), 1f, FurthestZoomAllowed);
        }

        private Vector2 StartPosition(WorldData world, SaveGame? save)
        {
            if (save != null && save.PlayerX >= 0f)
            {
                // Someone can stand closer to a wall than the passable map allows for, so the spot itself may not be on it.
                Vector2 saved = new Vector2(save.PlayerX, save.PlayerZ);
                if (TryFindGround(world, saved, out Vector2 ground)) return ground;
            }
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
