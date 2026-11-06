using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.UI;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Networking;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Sound.Music;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Diagnostics;
using System.Net;

namespace Dino_Defenders
{
    /// <summary>
    /// The game from the top. It carries the hero from world to world, and always plays the same way: a client on this thread,
    /// talking to a server. Alone, that server runs on a thread beside it and is reached without touching a network. A host
    /// opens the same server up to friends. Joining a friend puts this client on theirs instead, and the local one stops.
    /// </summary>
    internal class ArpgGame : Game, IClientHost
    {
        private const float AutosaveInterval = 30f;
        private const float InterfaceHeight = 1000f;
        private const float ViewSpreadAtTop = 0.967f;   // Half-width of the view at the top of the screen, per metre of camera height and of aspect ratio.
        private const float FurthestZoomAllowed = 1.25f;

        private readonly record struct PreparedWorld(WorldData World, byte[] Map);

        private readonly GameOptions _options;
        private readonly WorldAssetLibrary _assets = new();
        private readonly CharacterFactory _characters = new();

        /// <summary>Where everything that shows gear gets it from.</summary>
        public readonly Wardrobe Wardrobe = new();
        private readonly GhostPresenter _presenter;
        private readonly WorldCache _worlds = new();
        private readonly GameUi _ui;
        private readonly Stopwatch _simulatedLine = Stopwatch.StartNew();
        private readonly Queue<IPEndPoint> _joinAddresses = new();

        private HeroState _hero;
        private SaveGame? _save;
        private ServerHost? _server;
        private GameClient? _client;
        private ClientEffects? _effects;
        private PortMapper? _portMapper;
        private string? _firewallProblem;
        private InviteCode? _invite;
        private Task<PreparedWorld>? _loading;
        private string? _lostConnection;
        private string _joinKey = "";
        private bool _followingHero;
        private bool _firstWorldOfThisSession = true;
        private float _nextAutosave;
        private int _frames;
        private readonly List<(string name, Vector2 position)> _tourStops = new();
        private int _tourStop = -1;
        private int _tourWait;
        private bool _tourPhotographed;

        /// <summary>
        /// The code to give friends, once this game is open to them. It's worked out as it's asked for, as the address on the internet is
        /// only known once the router has answered, and that's the one that matters to anyone who isn't in the same house.
        /// </summary>
        public string? InviteText => _invite == null ? null : new InviteCode
        {
            Public = _portMapper?.External,
            Local = _invite.Local,
            Port = _invite.Port,
            Secret = _invite.Secret,
        }.Text;

        /// <summary>What's known about friends being able to reach this game.</summary>
        public string InviteStatus => _firewallProblem ?? _portMapper?.Status ?? "";

        /// <summary>What this player goes by in company. Left empty, the server calls them Hero and a number: their account's name is theirs to share.</summary>
        public string HeroName { get; set; } = "";

        public bool Hosting => _invite != null;
        public bool InFriendsGame => _server == null;
        public GameClient? Client => _client;

        public ArpgGame(Engine engine, GameOptions options) : base(engine)
        {
            _options = options;

            // First of all, as the takes are made on threads beside whatever else is going on, and that's what loading is for.
            Audio.Start(options.Audible);
            if (options.Record != null) Audio.Record(options.Record);
            SoundSettings.Load().Apply();
            if (options.Volume is float volume) Audio.MasterVolume = volume;
            if (options.Music is float music) Audio.SetVolume(SoundBus.Music, music);

            ApplyRenderOptions(options.Render);
            if (options.Film != null)
            {
                Directory.CreateDirectory(options.Film);
                Dino_Engine.Core.Engine.UseFixedStep(1f / options.FilmRate);
            }
            Dino_Engine.Core.Engine.SkipClock(options.Clock);
            HeroName = options.Name ?? "";
            _presenter = new GhostPresenter(_characters);
            engine.WorldReady = false;
            NetSchema.Build();
            RegisterSystems();
            _ui = new GameUi(Dino_Engine.Core.Engine.Gui, this, Travel, Quit, NewJourney);
            Dino_Engine.Core.Engine.WindowHandler.Closing += _ => ShutDown();

            SaveGame.PathOverride = options.SaveFile;
            _save = options.Saves ? SaveGame.Load() : null;
            _hero = _save?.Hero ?? HeroState.CreateNew();
            _hero.Level = Math.Max(_hero.Level, options.HeroLevel);
            _hero.Gold = Math.Max(_hero.Gold, options.Gold);
            _hero.PerilProven = Math.Max(_hero.PerilProven, options.Proven);
            ApplyBuildOptions(_hero, options);

            if (options.Join != null)
            {
                Join(options.Join, options.Secret);
                return;
            }

            StartOwnGame(_save != null
                ? new WorldRequest(_save.WorldSeed, _save.WorldArchetype, _save.WorldLevel)
                : new WorldRequest(options.Seed ?? Environment.TickCount, options.Archetype, options.Level), _save);

            if (options.Host) OpenToFriends();
        }

        // For scripted runs: a hero made to order, to look at a build without earning it.
        private static void ApplyBuildOptions(HeroState hero, GameOptions options)
        {
            foreach (string id in options.Talents)
            {
                if (Talents.TalentTree.Find(id) is not Talents.TalentNode node) continue;
                foreach (int step in Talents.TalentTree.PathTo(hero, node.Index))
                {
                    Talents.TalentNode taken = Talents.TalentTree.Nodes[step];
                    hero.Talents.Add(taken.Id);
                    foreach (Stats.Mod mod in taken.Mods)
                    {
                        if (mod.Kind == Stats.ModKind.GrantSkill) HeroBuild.OfferToBar(hero, Skills.SkillCatalog.Get(mod.Skill));
                    }
                }
            }

            foreach (string id in options.Equip)
            {
                if (UniqueCatalog.Find(id) is not UniqueDefinition unique) continue;
                Item item = UniqueCatalog.Make(unique, hero.Level, new Dino_Engine.World.WorldRandom(1));
                hero.Inventory.Add(item);
                hero.Equip(item);
            }

            if (options.Wear is string family) Wear(hero, Enum.Parse<GearFamily>(family, ignoreCase: true), options.Seed ?? 1);
        }

        // For a scripted run: something for every place, as much of one family as can be had, to look at.
        private static void Wear(HeroState hero, GearFamily family, int seed)
        {
            var random = new Dino_Engine.World.WorldRandom(seed);
            foreach (WornSlot place in Enum.GetValues<WornSlot>())
            {
                if (place == WornSlot.OffHand && hero.Equipped.TryGetValue(WornSlot.MainHand, out Item? held) && held.IsTwoHanded) continue;
                for (int attempt = 0; attempt < 300; attempt++)
                {
                    Item item = ItemGenerator.Forge(Math.Max(hero.Level, 20), random.Fork((int)place * 1000 + attempt), LootGrade.Rare, 0.5f, WornSlots.Holds(place));
                    if (item.Shares() != default && item.Shares().Of(family) < 0.6f) continue;
                    hero.Inventory.Add(item);
                    hero.Equip(item, place);
                    break;
                }
            }
        }

        private static void ApplyRenderOptions(string? render)
        {
            if (render == null) return;
            foreach (string setting in render.Split(','))
            {
                string[] parts = setting.Split('=');
                var render_ = Dino_Engine.Core.Engine.RenderEngine;
                if (parts.Length != 2 || !(render_.Lighting.Set(parts[0], parts[1]) || render_.Reflections.Set(parts[0], parts[1]))) Console.WriteLine($"No render setting '{setting}'.");
            }
        }

        private void RegisterSystems()
        {
            SystemRegistry.Remove<PlayerInputSystem>();

            // What a client does with what it is told. None of the game's rules run here: they're on the server.
            SystemRegistry.InsertBefore<GravitySystem>(new PromptSystem());
            SystemRegistry.InsertBefore<GravitySystem>(new AmbienceSystem());
            SystemRegistry.InsertAfter<VelocitySystem>(new GhostPresentationSystem(() => _client, () => _effects, Wardrobe));
            SystemRegistry.InsertBefore<GravitySystem>(new SoundscapeSystem());
            SystemRegistry.InsertBefore<GravitySystem>(new MusicDirectorSystem());
            SystemRegistry.InsertAfter<GhostPresentationSystem>(new CharacterSoundSystem(() => _effects?.Sounds));
        }

        // Alone or hosting, the server is here. The world it holds and the hero it is given are the same as they would be anywhere.
        private void StartOwnGame(WorldRequest request, SaveGame? save)
        {
            var server = new GameServer(new ServerSettings
            {
                Request = request,
                Offering = save?.Offering ?? (_options.TravelAfterFrames < 0 ? _options.Offering : null),
                Save = save,
                AllowTeleport = _options.Bot || _options.Tour != null,
                Start = _options.At,
                StartSite = _options.Site,
            }, _worlds);

            (LoopbackTransport serverEnd, LoopbackTransport clientEnd) = LoopbackTransport.CreatePair();
            server.AddTransport(serverEnd);
            _server = new ServerHost(server);
            Connect(clientEnd);
        }

        private void Connect(INetTransport transport)
        {
            if (_options.NetSim != null)
            {
                transport = new ImpairedTransport(transport, NetworkConditions.Parse(_options.NetSim), () => _simulatedLine.Elapsed.TotalMilliseconds);
            }
            _client = new GameClient(transport, this, Dino_Engine.Core.Engine.Instance!.world, _hero, HeroName);
            _effects = new ClientEffects(Dino_Engine.Core.Engine.Instance!.world, _client);
        }

        /// <summary>Lets friends in: opens a port, asks the router to pass it on, and makes a code to give them.</summary>
        public void OpenToFriends()
        {
            if (_server == null || _invite != null) return;

            uint secret = _options.Secret != null ? NetSchema.Fnv(_options.Secret) : InviteCode.NewSecret();
            string key = InviteCode.KeyFor(secret);

            // A scripted run has no business rearranging the firewall or the router of whoever happens to be running it.
            bool scripted = _options.Bot || _options.Shot != null || _options.Film != null;
            if (!scripted) _firewallProblem = WindowsFirewall.LetFriendsIn();

            try
            {
                UdpTransport transport = UdpTransport.Host(_options.Port, key);
                _server.Post(server => server.AddTransport(transport));
            }
            catch (IOException e)
            {
                GameSession.Current?.Announce("The way is shut", e.Message);
                return;
            }

            _invite = new InviteCode { Local = InviteCode.LocalAddresses().FirstOrDefault(), Port = _options.Port, Secret = secret };

            if (!scripted) _portMapper = new PortMapper(_options.Port);
            Console.WriteLine($"Open to friends on port {_options.Port}. Invite: {_invite.Text}");
        }

        /// <summary>Leaves this game for a friend's. Takes an invite code, or an address and port.</summary>
        public bool Join(string where, string? secret = null)
        {
            _joinAddresses.Clear();
            if (InviteCode.TryParse(where, out InviteCode? code))
            {
                foreach (IPEndPoint address in code!.Addresses()) _joinAddresses.Enqueue(address);
                _joinKey = code.Key;
            }
            else if (IPEndPoint.TryParse(where, out IPEndPoint? address))
            {
                _joinAddresses.Enqueue(address);
                _joinKey = InviteCode.KeyFor(secret != null ? NetSchema.Fnv(secret) : 0);
            }

            if (_joinAddresses.Count == 0) return false;

            Save();
            LeaveCurrentGame();
            ShowLoading("Finding your friends", "Reaching across the land");
            TryNextAddress();
            return true;
        }

        private void TryNextAddress()
        {
            IPEndPoint address = _joinAddresses.Dequeue();
            Console.WriteLine($"Trying {address}...");
            Connect(UdpTransport.Join(address, _joinKey));
        }

        private void LeaveCurrentGame()
        {
            _client?.Dispose();
            _client = null;
            _effects = null;
            _server?.Dispose();
            _server = null;
            _portMapper?.Dispose();
            _portMapper = null;
            _firewallProblem = null;
            _invite = null;
            _loading = null;
        }

        /// <summary>Back to this player's own world.</summary>
        public void ReturnHome(string? why)
        {
            LeaveCurrentGame();
            _firstWorldOfThisSession = false;
            _homecoming = why;

            SaveGame? save = _save;
            StartOwnGame(save != null
                ? new WorldRequest(save.WorldSeed, save.WorldArchetype, save.WorldLevel)
                : new WorldRequest(_options.Seed ?? Environment.TickCount, _options.Archetype, _options.Level), save);
        }

        private string? _homecoming;

        public override void update()
        {
            _client?.Update(Dino_Engine.Core.Engine.Delta);
            HandleLostConnection();
            FinishLoading();
            FollowHero();
            UpdateInterface();

            GameSession session = GameSession.Current;
            if (session == null || _client?.Session != session) return;

            // Counted from when there's a world to be in. How long it took to grow is no business of a script.
            if (_client.State == ClientState.Playing) _frames++;

            session.Paused = _ui.AnyOpen || _ui.Loading.Visible || _options.Tour != null;

            if (Dino_Engine.Core.Engine.Time >= _nextAutosave)
            {
                _nextAutosave = Dino_Engine.Core.Engine.Time + AutosaveInterval;
                Save();
            }

            if (_frames == _options.TravelAfterFrames) StepThroughWaygate();
            if (_frames == 90 && _options.Panel != null) OpenPanel(_options.Panel);
            if (_frames == 30 && _options.Loot is string loot && _server != null)
            {
                _server.Ask(server =>
                {
                    server.ShowLoot(loot);
                    return true;
                });
            }
            if (_frames == 30 && _options.Parade is string parade && _server != null)
            {
                _server.Ask(server =>
                {
                    server.Parade(parade);
                    return true;
                });
            }
            if (_options.Shot != null && _frames == _options.ShotAfterFrames)
            {
                Engine.RequestScreenshot(_options.Shot, () =>
                {
                    Report();
                    Quit();
                });
            }
            if (_options.Tour != null) UpdateTour();
            if (_options.Film != null) UpdateFilm();
            foreach (var (frame, speed, direction) in _options.Wind)
            {
                if (_frames == frame) session.ScriptedWind = (speed, direction);
            }
            session.WeatherClockOffset = _options.WeatherClock;
            if (_options.Weather != null && WeatherSample.TryNamed(_options.Weather, out WeatherSample ordered)) session.ScriptedWeather = ordered;
            if (_frames == _options.GuideAfterFrames) session.Intent.Guide = true;
        }

        private void UpdateFilm()
        {
            int shot = _frames - _options.FilmStart;
            if (shot < 0) return;
            if (shot < _options.FilmFrames) Engine.RequestScreenshot(Path.Combine(_options.Film!, $"{shot:D4}.png"));
            else Quit();
        }

        private void HandleLostConnection()
        {
            if (_lostConnection == null) return;
            string reason = _lostConnection;
            _lostConnection = null;

            // An invite can hold more than one address. The first one failing says nothing about the next.
            if (_client?.State == ClientState.Disconnected && _client.Session == null && _joinAddresses.Count > 0)
            {
                _client.Dispose();
                TryNextAddress();
                return;
            }

            Console.WriteLine($"Disconnected: {reason}");
            if (_options.Join != null && _options.Bot)
            {
                // A scripted run came for one game; if that's gone, so is the run.
                Report();
                Dino_Engine.Core.Engine.WindowHandler.Close();
                return;
            }
            ReturnHome(reason);
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
            if (window.IsKeyPressed(Keys.T)) _ui.Toggle(_ui.Talents);
            if (window.IsKeyPressed(Keys.K)) _ui.Toggle(_ui.Skills);
        }

        private void OpenPanel(string name)
        {
            switch (name)
            {
                case "map": _ui.Toggle(_ui.Map); break;
                case "hero": _ui.Toggle(_ui.Hero); break;
                case "talents": _ui.Toggle(_ui.Talents); break;
                case "skills": _ui.Toggle(_ui.Skills); break;
                case "chronicle": _ui.Toggle(_ui.Chronicle); break;
                case "pause": _ui.Toggle(_ui.Pause); break;
                case "sound":
                    Dino_Engine.Core.Engine.DebugOverlay.Visible = true;
                    Dino_Engine.Core.Engine.DebugOverlay.SoundWindow.Visible = true;
                    break;
                case "friends": _ui.Toggle(_ui.Friends); break;
                case "waygate": _ui.Open(InteractKind.Waygate, GameSession.Current.Sites[0]); break;
                case "merchant": _ui.Open(InteractKind.Merchant, GameSession.Current.Sites.First(s => s.Site.Kind == SiteKind.Village)); break;
                case "elder": _ui.Open(InteractKind.Elder, GameSession.Current.Sites.First(s => s.Site.Kind == SiteKind.Village)); break;
            }
        }

        // A hard stop for scripted runs: what became of the hero, for reading in the log.
        private void Report()
        {
            GameSession? session = _client?.Session;
            if (session == null)
            {
                Console.WriteLine("Hero: never reached a world.");
                return;
            }

            int cleared = session.Sites.Count(s => s.Cleared);
            int fallen = session.Sites.Sum(s => s.Dead.Count);
            int found = session.Sites.Count(s => s.Discovered);
            string objectives = string.Join(", ", session.Chronicle.Objectives.Select(o => $"{o.Progress}/{o.Goal}"));
            Console.WriteLine($"World: {session.World.Plan.Name}, level {session.World.Plan.Level}, {found} sites found, objectives {objectives}, liberated {session.Liberated}");
            Console.WriteLine($"Hero: level {_hero.Level}, {session.Health:0}/{session.Sheet.MaxHealth:0} health, {_hero.Gold} gold, {_hero.Inventory.Count} items, {fallen} enemies fallen, {cleared} sites cleared at {session.PlayerPosition.X:0},{session.PlayerPosition.Y:0}");

            HeroPredictor predictor = _client!.Predictor;
            Console.WriteLine($"Net: {_client.Party.Count} in the party, {_client.Ghosts.Ghosts.Count} ghosts, round trip {_client.RoundTrip:0} ms, drawn {_client.DrawnBehind:0} ms behind, {predictor.Corrections} corrections in {predictor.Compared} checks (worst {predictor.WorstCorrection:0.00} m), {_client.Ghosts.Mismatches} mismatches, {_client.BytesReceived / 1024} KB in, {_client.BytesSent / 1024} KB out");
            var grass = Dino_Engine.Core.Engine.RenderEngine._grassRenderer;
            Console.WriteLine($"Grass: {grass.NearBladesDrawn} near, {grass.FarBladesDrawn} far");
            Console.WriteLine($"Gear: {Wardrobe.Models} models alive, {Wardrobe.Made} made in all, worst frame's work {Wardrobe.WorstFrameMilliseconds:0.00} ms");
            if (Audio.Mixer is Dino_Engine.Sound.Mixing.Mixer mixer) Console.WriteLine($"Sound: {Audio.TakesMade} takes, at most {mixer.MostActiveVoices} voices, {mixer.Dropped} dropped, {mixer.Load * 100f:0.0}% mixing, {Audio.Output?.Underruns ?? 0} underruns on {Audio.Output?.Device ?? "no device"}, music {Music.Mood} at {Music.Intensity:0.00}, {Music.Gaps} gaps, heard {string.Join(" ", Music.Heard)}");
            if (Audio.Mixer != null)
            {
                var reading = Dino_Defenders.Client.SoundscapeSystem.Reading;
                if (Dino_Engine.Physics.Weather.TryGet(Dino_Engine.Core.Engine.Instance!.world, out var weather))
                {
                    var here = weather.Here;
                    Console.WriteLine($"Weather: clock {weather.Clock:0}, cloud {here.Cloud:0.00}, fall {here.Fall:0.00} ({here.Snow:0.00} snow, {here.Ash:0.00} ash), storm {here.Storm:0.00}, dust {here.Dust:0.00}, wet {here.Wet:0.00}, flood {here.Flood:0.00}, gale {here.Gale:0.00}");
                }
                string steps = string.Join(", ", Dino_Defenders.Client.CharacterSoundSystem.StepsHeard.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key} {pair.Value}"));
                Console.WriteLine($"Land: wind {reading.wind:0.0} m/s, crest {reading.crest:0.00}, sea {reading.sea:0.00}, bank {reading.bank:0.00}, drips {reading.drips:0.00}, steps on {steps}");
            }
        }

        private void Save()
        {
            if (!_options.Saves) return;

            // A friend's world isn't this player's to keep, but the hero who went there is.
            if (_server != null)
            {
                if (_client?.State != ClientState.Playing) return;
                var world = new SaveGame();
                if (!_server.Ask(server =>
                {
                    server.CaptureWorld(world);
                    return true;
                })) return;
                _save = world;
            }

            // Someone whose first steps were in a friend's world has no land of their own yet: this is the one they'll go home to.
            _save ??= new SaveGame { WorldSeed = Environment.TickCount };
            _save.Hero = _hero;
            _save.Store();
        }

        private void Quit()
        {
            ShutDown();
            Dino_Engine.Core.Engine.WindowHandler.Close();
        }

        private bool _shutDown;

        private void ShutDown()
        {
            if (_shutDown) return;
            _shutDown = true;
            Save();
            LeaveCurrentGame();
            Music.End(0f);
            Audio.Stop();
        }

        private void NewJourney()
        {
            if (_options.Saves) SaveGame.Delete();
            LeaveCurrentGame();
            _hero = HeroState.CreateNew();
            _save = null;
            _firstWorldOfThisSession = true;
            StartOwnGame(new WorldRequest(Environment.TickCount), null);
        }

        private void StepThroughWaygate()
        {
            GameSession session = GameSession.Current;
            var random = new WorldRandom(session.World.Plan.Seed).Fork("test road");
            Travel(new WorldRequest(random.Int(int.MaxValue), null, session.World.Plan.Level + 1), _options.Offering);
        }

        private void Travel(WorldRequest request, Offering offering)
        {
            Save();
            _ui.CloseAll();
            _client?.StepThroughWaygate(request, offering);
        }

        private void ShowLoading(string title, string detail)
        {
            LoadingScreen screen = _ui.Loading;
            screen.Title = title;
            screen.Detail = detail;
            screen.Progress = 0f;
            screen.Stage = "";
            screen.Visible = true;
        }

        void IClientHost.WorldNeeded(WorldRequest request, Offering offering)
        {
            _ui.CloseAll();
            Audio.StopWorld(1.2f);
            Music.End(1.2f);
            if (GameSession.Current != null) Audio.Play(Treasure.Waygate);
            WorldPlan plan = WorldPlan.Create(request);
            ShowLoading(plan.Name, offering.Any ? $"{plan.Archetype.Name}, level {plan.Level}. {offering.Title()}" : $"{plan.Archetype.Name}, level {plan.Level}");

            LoadingScreen screen = _ui.Loading;
            _worlds.Progress = (progress, stage) =>
            {
                screen.Progress = progress;
                screen.Stage = stage;
            };

            _loading = Prepare(request);
        }

        private async Task<PreparedWorld> Prepare(WorldRequest request)
        {
            var clock = Stopwatch.StartNew();
            WorldData world = await _worlds.Get(request);
            byte[] map = await Task.Run(() => WorldMapImage.Render(world, MapPanel.Resolution, showSites: false));
            Console.WriteLine($"Generated {world.Plan.Name} ({world.Plan.Archetype.Name}, {world.Plan.Mood.Name}) in {clock.Elapsed.TotalSeconds:0.00} s");
            return new PreparedWorld(world, map);
        }

        private void FinishLoading()
        {
            if (_loading == null || !_loading.IsCompleted) return;

            Task<PreparedWorld> finished = _loading;
            _loading = null;

            if (finished.IsFaulted)
            {
                Console.WriteLine($"World generation failed: {finished.Exception}");
                ReturnHome("That land would not take shape.");
                return;
            }

            Enter(finished.Result);
        }

        private void Enter(PreparedWorld prepared)
        {
            var clock = Stopwatch.StartNew();
            WorldData world = prepared.World;
            WorldScene.Enter(Engine.world, world, _assets);
            _characters.Prepare();
            Console.WriteLine($"Prepared {_assets.Count} models in {clock.Elapsed.TotalSeconds:0.00} s");

            _client!.EnterWorld(world);
            GameSession session = _client.Session!;
            Ambience.FindFeatures(session, _assets);
            session.UpdateGuidance();
            GameSession.Current = session;
            _ui.Map.SetImage(prepared.Map);
            _followingHero = false;

            // Up from here, until the hero arrives: everything that draws wants a camera to draw through.
            SpawnCameras(session, world.Sites[0].Position);
            Engine.WorldReady = true;

            if (_options.Tour != null)
            {
                _ui.Hud.Visible = false;
                PlanTour(world);
            }

            if (_options.Bot)
            {
                var bot = new HeroBot(Engine.world, _options.Sweep, _options.Walk, FinishSweep);
                session.Autopilot = () => bot.Think(session);
            }
        }

        void IClientHost.Arrived(Vector2 position)
        {
            GameSession session = _client!.Session!;
            _ui.Loading.Visible = false;

            if (_homecoming != null)
            {
                session.Announce("Back on your own road", _homecoming, 6f);
                _homecoming = null;
            }
            else if (_save == null && _firstWorldOfThisSession && _options.Tour == null)
            {
                session.Announce(session.World.Plan.Name, session.Chronicle.Prologue, 9f);
            }
            _firstWorldOfThisSession = false;
            if (session.Offering.Any && !session.Liberated) session.Announce(session.Offering.Title(), session.Offering.OmenNames(), 6f);
            Save();
        }

        // The camera is there before the hero's ghost is. They find each other as soon as both exist.
        private void FollowHero()
        {
            GameSession? session = _client?.Session;
            if (_followingHero || session == null || !session.Player.IsValid()) return;

            ECSWorld ecs = Engine.world;
            if (!ecs.GameCamera.IsValid() || !ecs.TryGetEntityLocation(ecs.GameCamera, out _, out _)) return;
            if (!ecs.TryGetEntityLocation(session.Player, out _, out _)) return;

            EntityView camera = ecs.GetEntityView(ecs.GameCamera);
            var follow = camera.Get<FollowCameraComponent>();
            follow.Target = session.Player;
            camera.Set(follow);
            _followingHero = true;
        }

        HeroIntent IClientHost.ReadIntent()
        {
            GameSession? session = _client?.Session;

            // Whatever they were walking to, they aren't now, and shouldn't set off for it again from a shrine on the other side of the world.
            if (session != null && session.Health <= 0f) session.Fetch.Cancel();

            // The free camera owns the keyboard and mouse, and open panels stop the hero.
            if (session == null || Engine.world.IsFreeCameraActive || session.Paused || session.Health <= 0f) return default;
            Vector2 at = session.PlayerPosition;
            HeroIntent intent = session.Autopilot != null ? session.Autopilot() : LocalInput.Read(new Vector3(at.X, session.World.Height.Sample(at), at.Y), session.Sim);
            session.Fetch.Steer(session, ref intent);
            return intent;
        }

        void IClientHost.HeroActed(HeroStepEvents events, in HeroSimState hero)
        {
            if (_client != null) _effects?.HeroActed(_client, events, hero);
        }

        void IClientHost.OpenPanel(InteractKind kind, SiteState site) => _ui.Open(kind, site);

        void IClientHost.Disconnected(string reason) => _lostConnection = reason;

        void IGhostHost.Decorate(Ghost ghost, List<IComponent> components) => _presenter.Decorate(ghost, components);
        void IGhostHost.Created(Ghost ghost) => _presenter.Created(ghost);
        void IGhostHost.Removing(Ghost ghost) => _presenter.Removing(ghost);

        private void FinishSweep(HeroBot bot)
        {
            Report();
            Console.WriteLine($"Places the bot couldn't get to: {bot.Unreachable}");
            if (_options.Shot != null) Engine.RequestScreenshot(_options.Shot, Quit);
            else Quit();
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

            _client?.TravelTo(_tourStops[_tourStop].position);
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

        private void SpawnCameras(GameSession session, Vector2 start)
        {
            ECSWorld ecs = Engine.world;
            float height = session.World.Height.Sample(start);

            float look = _options.Look ?? 30f;
            float pitch = _options.Angle is float angle ? MathHelper.DegreesToRadians(angle) : MathF.PI / 2.7f;
            Vector3 offset = new Vector3(0f, look, -look / MathF.Tan(pitch));
            ecs.GameCamera = ecs.CreateEntity("Camera",
                new PositionComponent(new Vector3(start.X, height, start.Y) + offset),
                new RotationComponent(pitch, MathF.PI, 0f),
                new MainCameraTag(),
                new LocalToWorldMatrixComponent(),
                new ViewMatrixComponent(),
                new FollowCameraComponent(default, offset) { MaxZoom = FurthestZoom(look) },
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

        // How far out the camera can go before the corners of the view pass the end of the streamed terrain. A wide window sees further
        // to the sides at the top of the screen, so it reaches that sooner.
        private static float FurthestZoom(float cameraHeight)
        {
            Vector2i resolution = Dino_Engine.Core.Engine.Resolution;
            float aspect = (float)resolution.X / MathF.Max(resolution.Y, 1f);
            float terrainReach = (WorldScene.ChunkRadius + 0.5f) * WorldScene.ChunkSize;
            return Math.Clamp(terrainReach / (ViewSpreadAtTop * cameraHeight * aspect), 1f, FurthestZoomAllowed);
        }
    }
}
