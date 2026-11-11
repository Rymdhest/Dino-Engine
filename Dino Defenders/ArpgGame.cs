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
    internal class ArpgGame : Game, IClientHost, IMenuHost
    {
        private const float AutosaveInterval = 30f;
        private const float InterfaceHeight = 1000f;
        private const float ViewSpreadAtTop = 0.967f;   // Half-width of the view at the top of the screen, per metre of camera height and of aspect ratio.
        private const float FurthestZoomAllowed = 1.25f;

        private readonly record struct PreparedWorld(WorldData World, byte[] Map);

        // What's begun before there's a window (Begin), and taken up by the game when it's made.
        private static WorldCache? _earlyWorlds;
        private static WorldAssetLibrary? _earlyAssets;
        private static Task<WorldData>? _earlyTitle;

        private readonly GameOptions _options;
        private readonly WorldAssetLibrary _assets = _earlyAssets ?? new();
        private readonly CharacterFactory _characters = new();

        /// <summary>Where everything that shows gear gets it from.</summary>
        public readonly Wardrobe Wardrobe = new();
        private readonly GhostPresenter _presenter;
        private readonly WorldCache _worlds = _earlyWorlds ?? new();
        private readonly GameUi _ui;
        private readonly Stopwatch _simulatedLine = Stopwatch.StartNew();
        private readonly Queue<IPEndPoint> _joinAddresses = new();

        private HeroState _hero = null!;
        private SaveGame? _save;
        private Stash? _stash;
        private HeroRoster? _roster;
        private List<RosterEntry> _heroes = new();
        private RosterEntry? _chosen;
        private TitleScene? _title;
        private GiantShow? _giants;
        private Task<WorldData>? _titleLoading;
        private bool _charactersNext;
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
        private readonly PerfRecorder? _perf;
        private StreamWriter? _trace;
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

        /// <summary>
        /// Begins, before there's an engine, what needs no window, as making one and its materials takes seconds of this thread that every other core would spend waiting:
        /// the land behind the menu and the talent tree, which is grown the first time anything asks and the interface asks while it's made. The land's plants and stones
        /// follow the moment there are materials to give them (<see cref="Dino_Engine.Modelling.Procedural.MaterialNumbers"/>), while the rest of the engine is still being made.
        /// </summary>
        public static void Begin(GameOptions options)
        {
            Task.Run(() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Talents.TalentTree).TypeHandle));

            // The giants are in the picture behind the menu and in the Haven, and take seconds to grow, so they start now. A run that goes straight to a land grows them if it ever goes home.
            if (options.StartsAtMenu || options.Haven) CharacterFactory.GrowGiants();
            if (!options.StartsAtMenu) return;

            _earlyWorlds = new WorldCache();
            _earlyAssets = new WorldAssetLibrary();
            _earlyTitle = GrowTitle(_earlyWorlds, _earlyAssets, TitleRequest(options), Dino_Engine.Modelling.Procedural.MaterialNumbers.Ready);
        }

        private static WorldRequest TitleRequest(GameOptions options) => new WorldRequest(options.Seed ?? TitleScene.Seed, options.Archetype ?? TitleScene.Archetype, 1);

        public ArpgGame(Engine engine, GameOptions options) : base(engine)
        {
            _options = options;

            // First of all, as the takes are made on threads beside whatever else is going on, and that's what loading is for.
            Audio.Start(options.Audible);
            if (options.Record != null) Audio.Record(options.Record);
            SoundSettings.Load().Apply();
            if (options.Volume is float volume) Audio.MasterVolume = volume;
            if (options.Music is float music) Audio.SetVolume(SoundBus.Music, music);

            if (options.Quality is Dino_Engine.Rendering.GraphicsQuality quality) Dino_Engine.Rendering.GraphicsPresets.Apply(Dino_Engine.Core.Engine.RenderEngine, quality);
            ApplyRenderOptions(options.Render);
            if (options.Perf != null)
            {
                _perf = new PerfRecorder(options.Perf, options.PerfStart, options.PerfFrames);
                Dino_Engine.Core.Engine.PerformanceMonitor.AlwaysMeasureGPU = options.PerfGpu;
                Dino_Engine.Core.Engine.PerformanceMonitor.Counting = true;
            }
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
            _ui = new GameUi(Dino_Engine.Core.Engine.Gui, this, Quit, ToMenu);
            Dino_Engine.Core.Engine.WindowHandler.Closing += _ => ShutDown();

            if (options.StartsAtMenu)
            {
                ShowMenu();
                return;
            }

            _characters.Begin();
            _ui.Playing(true);
            SaveGame.PathOverride = options.SaveFile;
            _save = options.Saves ? SaveGame.Load() : null;
            _hero = _save?.Hero ?? HeroState.CreateNew();
            PrepareHero();
            OpenStash();

            if (options.Join != null)
            {
                Join(options.Join, options.Secret);
                return;
            }

            StartOwnGame(_save);
            if (options.Host) OpenToFriends();
        }

        // What everyone on this machine has put away, which only one copy of the game can have at once. A scripted run has one of its own that's never kept, unless it's told where.
        private void OpenStash()
        {
            string? path = _options.Stash;
            if (path == null && !_options.Scripted) path = Path.Combine(_options.Heroes ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DinoDefenders"), "stash.json");
            StashStore.Claim(path);
            _stash = StashStore.Held ? StashStore.Load() : null;
            if (!StashStore.Held) Console.WriteLine("The stash is open in another copy of the game.");
        }

        // Where a hero's game starts: back in the land they were in, if they were in one, or the Haven. A scripted run goes straight to the land it's asked for, unless it asks for the Haven.
        private (WorldRequest Request, LandTerms Terms) FirstWorld(SaveGame? save)
        {
            if (save is { InLand: true }) return (save.Land(), save.Terms);
            bool scriptedLand = !_options.Haven && (_options.Seed != null || _options.Archetype != null || _options.Tour != null || (_options.Scripted && save == null));
            if (!scriptedLand) return (Lands.Haven, new LandTerms());
            int level = _options.Level;
            return (new WorldRequest(_options.Seed ?? Environment.TickCount, _options.Archetype, level), _options.Terms(level));
        }

        private void PrepareHero()
        {
            _hero.Level = Math.Max(_hero.Level, _options.HeroLevel);
            _hero.Gold = Math.Max(_hero.Gold, _options.Gold);
            _hero.BestLand = Math.Max(_hero.BestLand, _options.LandLevel - 1);
            ApplyBuildOptions(_hero, _options);
            if (_options.Name == null && _hero.Name.Length > 0) HeroName = _hero.Name;
        }

        // ---- The main menu.

        IReadOnlyList<RosterEntry> IMenuHost.Heroes => _heroes;

        RosterEntry? IMenuHost.Chosen => _chosen;

        void IMenuHost.Choose(RosterEntry entry)
        {
            _chosen = entry;
            _title?.Show(entry.Hero);
        }

        void IMenuHost.Create(string name)
        {
            if (_roster?.Create(name) is not RosterEntry created) return;
            _heroes.Insert(0, created);
            ((IMenuHost)this).Choose(created);
        }

        void IMenuHost.Delete(RosterEntry entry)
        {
            _roster?.Delete(entry);
            _heroes.Remove(entry);
            _chosen = _heroes.FirstOrDefault();
            _title?.Show(_chosen?.Hero);
        }

        void IMenuHost.Quit() => Quit();

        /// <summary>Takes the chosen hero into their world, or a new one if they've never been anywhere.</summary>
        void IMenuHost.Play()
        {
            if (_chosen == null) return;

            SaveGame.PathOverride = _chosen.Path;
            SaveGame? save = SaveGame.Load();
            _hero = save?.Hero ?? _chosen.Hero;
            _save = save is { } played && !played.NeverPlayed() ? played : null;
            _firstWorldOfThisSession = true;
            PrepareHero();
            // The Archive keeps the records of every hero on this machine: the others as their saves have them.
            ArchivePlaques.Others = _heroes.Where(entry => entry != _chosen).Select(entry => entry.Hero).ToList();

            _ui.Playing(true);
            _title = null;
            _titleLoading = null;
            Music.End(1.2f);
            OpenStash();
            StartOwnGame(_save);
        }

        private void ShowMenu()
        {
            _roster ??= new HeroRoster(_options.Heroes);
            _heroes = _roster.List();
            _chosen = _heroes.FirstOrDefault();

            ShowLoading(BuildInfo.Name, "");
            LoadingScreen screen = _ui.Loading;
            _worlds.Progress = (progress, stage) =>
            {
                screen.Progress = progress;
                screen.Stage = stage;
            };
            // The first time, it was begun before there was a window.
            _titleLoading = _earlyTitle ?? GrowTitle(_worlds, _assets, TitleRequest(_options), Task.CompletedTask);
            _earlyTitle = null;
        }

        private static async Task<WorldData> GrowTitle(WorldCache worlds, WorldAssetLibrary assets, WorldRequest request, Task materials)
        {
            WorldData world = await worlds.Get(request);
            await materials;
            await Task.Run(() => assets.Grow(WorldAssetLibrary.AssetsOf(world)));
            return world;
        }

        private void FinishTitleLoading()
        {
            if (_titleLoading == null || !_titleLoading.IsCompleted) return;

            Task<WorldData> finished = _titleLoading;
            _titleLoading = null;
            if (finished.IsFaulted)
            {
                Console.WriteLine($"The land behind the menu failed: {finished.Exception}");
                Quit();
                return;
            }

            WorldData world = finished.Result;
            GameSession.Current = null!;
            _giants = null;
            WorldScene.Enter(Engine.world, world, _assets, TitleScene.ChunkRadius);

            // Nothing behind the menu is an enemy, so what a world can send at a hero is grown while the menu's open, and waited for when a world is entered. It's begun
            // the frame after this one, as the first frame of a world makes its whole view at once, on every core it can have.
            _charactersNext = true;
            _title = new TitleScene(Engine.world, world, Wardrobe, _characters);
            _title.Show(_chosen?.Hero);
            if (!Nature.Off && TitleScene.FindHerd(world, _title.Spot) is (Vector2 home, float reach, Func<Vector2, bool> allowed))
            {
                _giants = GiantShow.Around(Engine.world, world, _characters, home, reach, HerdSeed(), allowed);
            }
            Engine.WorldReady = true;

            _ui.Playing(false);
            _ui.Menu.Rebuild();
            _ui.Loading.Visible = false;
            // Behind the menu, the Haven's own tune: the one a player will come to know as the game's.
            Music.Begin(MusicPlace.Home);
            Music.Mood = MusicMood.Haven;
            Music.Intensity = 0.35f;
        }

        private void ToMenu()
        {
            Save();
            LeaveCurrentGame();
            Audio.StopWorld(1.2f);
            Music.End(1.2f);

            // Nobody's being played from here on, so there's nothing that can be saved over anyone.
            _hero = null!;
            _save = null;
            _stash = null;
            ArchivePlaques.Others = Array.Empty<HeroState>();
            StashStore.Release();
            SaveGame.PathOverride = _options.SaveFile;
            ShowMenu();
        }

        // Whoever asked may still be writing the file, and then it's for the next frame.
        private void TakeSnap()
        {
            if (_options.Snap == null || !File.Exists(_options.Snap)) return;
            try
            {
                string picture = File.ReadAllText(_options.Snap).Trim();
                File.Delete(_options.Snap);
                Engine.RequestScreenshot(picture);
            }
            catch (IOException)
            {
            }
        }

        // What a scripted run does with the menu showing, as nothing below it happens until there's a game.
        private void UpdateMenu()
        {
            _title?.Update(Dino_Engine.Core.Engine.Delta);
            if (_title == null) return;

            _frames++;
            TakeSnap();
            if (_options.Shot != null && _frames == _options.ShotAfterFrames) Engine.RequestScreenshot(_options.Shot, Quit);
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
                }
            }

            foreach (string id in options.Equip)
            {
                if (UniqueCatalog.Find(id) is not UniqueDefinition unique) continue;
                Item item = UniqueCatalog.Make(unique, hero.Level, new Dino_Engine.World.WorldRandom(1));
                hero.Pickup(item);
                hero.Equip(item);
            }

            // What's on the shelf, copied out onto the bar from the first place on, for looking at something with nothing written by hand.
            for (int i = 0; i < options.Books.Count && i < HeroState.BarSlots; i++)
            {
                if (Books.Shelf.Find(options.Books[i]) is not { Source: Books.Book written } plan) continue;
                hero.Bar[i] = ItemGenerator.Copied(written, plan.Name, (ulong)i + 1);
            }

            if (options.Wear is string family) Wear(hero, Enum.Parse<GearFamily>(family, ignoreCase: true), options.Seed ?? 1);

            var carried = new Dino_Engine.World.WorldRandom(options.Seed ?? 1).Fork("carried");
            for (int i = 0; i < options.Carry; i++) hero.Pickup(ItemGenerator.Roll(Math.Max(hero.Level, 10), carried.Fork(i), rarityBonus: 2f));
            for (int i = 0; i < options.Folios; i++) hero.Pickup(ItemGenerator.Folio(Math.Max(hero.Level, 10), carried.Fork("folio").Fork(i), (Rarity)(i % 4)));
            for (int i = 0; i < options.CarryOrders; i++) hero.Pickup(OrdersMaker.Roll(Math.Max(hero.LandLevel, 1), carried.Fork("orders").Fork(i), (Rarity)(i % 4)));
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
                    Item item = ItemGenerator.Forge(Math.Max(hero.Level, 20), random.Fork((int)place * 1000 + attempt), Rarity.Rare, 0.5f, WornSlots.Holds(place));
                    if (item.Shares() != default && item.Shares().Of(family) < 0.6f) continue;
                    hero.Pickup(item);
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
                if (parts.Length == 2 && parts[0] == "relief")
                {
                    // How deep every material's relief is drawn, as a share of its own: 0 for none (Surface/Relief.glsl).
                    render_.ReliefScale = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                    continue;
                }
                if (parts.Length == 2 && parts[0] == "aniso")
                {
                    render_.textureGenerator.SetAnisotropy(float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
                    continue;
                }
                if (parts.Length == 2 && parts[0] == "samples")
                {
                    render_.AntiAliasing.Samples = int.Parse(parts[1]);
                    continue;
                }
                if (parts.Length != 2 || !(render_.Lighting.Set(parts[0], parts[1]) || render_.Reflections.Set(parts[0], parts[1]) || render_.GlobalIllumination.Set(parts[0], parts[1]))) Console.WriteLine($"No render setting '{setting}'.");
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
            SystemRegistry.InsertAfter<GhostPresentationSystem>(new PropLightSystem());
            SystemRegistry.InsertBefore<ModelRenderSystem>(new WildlifeSystem());
            Nature.Off = _options.NoLife;
            Nature.LogFile = _options.LifeLog;
        }

        // Alone or hosting, the server is here. The world it holds and the hero it is given are the same as they would be anywhere.
        private void StartOwnGame(SaveGame? save)
        {
            (WorldRequest request, LandTerms terms) = FirstWorld(save);
            var server = new GameServer(new ServerSettings
            {
                Request = request,
                Terms = terms,
                Save = save,
                AllowTeleport = _options.Bot || _options.Tour != null,
                Calm = _options.Calm,
                SameSky = _options.Scripted,
                Start = _options.At,
                StartSite = _options.Site,
                StartBiome = _options.Biome,
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
            _client = new GameClient(transport, this, Dino_Engine.Core.Engine.Instance!.world, _hero, HeroName, _stash);
            _client.StashKept += Keep;
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

        // Plants thick along the way the hero's walking, as there's no telling where a world has put them. The plants are this end's own business, so nothing else need know.
        private void PlantGarden(string plants)
        {
            GameSession? session = GameSession.Current;
            if (session == null) return;

            string[] ids = plants.Split(',');
            Vector2 from = session.PlayerPosition;
            Vector2 to = _options.Walk ?? from + Vector2.UnitX * 30f;
            float length = (to - from).Length;
            Vector2 along = (to - from) / MathF.Max(length, 1f);
            Vector2 across = new Vector2(-along.Y, along.X);
            var random = new WorldRandom(1).Fork("garden");
            float gap = _options.GardenGap;
            float reach = MathF.Max(7f, gap * 1.2f);
            int planted = 0;
            for (float travelled = 3f; travelled < length; travelled += gap)
            {
                for (float side = -reach; side <= reach; side += gap)
                {
                    Vector2 at = from + along * travelled + across * side + new Vector2(random.Range(-0.6f, 0.6f), random.Range(-0.6f, 0.6f));
                    string id = $"{ids[planted++ % ids.Length]}#{random.Int(0, 5)}";
                    Vector3 position = new Vector3(at.X, session.World.Height.SampleCubic(at.X, at.Y), at.Y);
                    Quaternion rotation = Quaternion.FromAxisAngle(Vector3.UnitY, random.Float() * MathF.Tau);
                    Vector3 scale = new Vector3(random.Range(0.9f, 1.25f));
                    Engine.world.CreateEntity(
                        new PositionComponent(position),
                        new RotationComponent(rotation),
                        new ScaleComponent(scale),
                        new ModelComponent(_assets.Get(id)),
                        new ModelRenderTag(),
                        new LocalToWorldMatrixComponent { value = Dino_Engine.Util.MyMath.createTransformationMatrix(position, rotation, scale) });
                }
            }
        }

        /// <summary>Back to this player's own game: the land they were in, if they were in one of their own, or the Haven.</summary>
        public void ReturnHome(string? why)
        {
            LeaveCurrentGame();
            _firstWorldOfThisSession = false;
            _homecoming = why;
            StartOwnGame(_save);
        }

        // The stash has changed, as the server has said. It's kept before the hero is, so that if anything goes wrong in between, something is kept twice rather than lost.
        private void Keep(Stash stash)
        {
            StashStore.Store(stash);
            Save();
        }

        private string? _homecoming;
        private EffectStage? _stage;
        private int _travelled;

        public override void update()
        {
            _client?.Update(Dino_Engine.Core.Engine.Delta);
            HandleLostConnection();
            if (_charactersNext)
            {
                _charactersNext = false;
                _characters.Begin();
            }
            FinishLoading();
            FinishTitleLoading();
            UpdateMenu();
            _giants?.Update(Dino_Engine.Core.Engine.Delta);
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

            if (_options.Trace != null && _client.State == ClientState.Playing) WriteTrace(_options.Trace);
            if (_options.TravelAfterFrames > 0 && _travelled < _options.Travels && _frames >= _options.TravelAfterFrames * (_travelled + 1))
            {
                _travelled++;
                TravelOn(session);
            }
            if (_frames == 90 && _options.Panel != null) OpenPanel(_options.Panel);
            if (_frames == 30 && _options.Loot is string loot && _server != null)
            {
                _server.Ask(server =>
                {
                    server.ShowLoot(loot);
                    return true;
                });
            }
            if (_frames == 30 && _options.Garden is string garden) PlantGarden(garden);
            if (_frames == 30 && _options.Materials is string materials) MaterialGallery.Stand(materials, session.PlayerPosition, (x, z) => session.World.Height.SampleCubic(x, z));
            if (_frames == 30 && _options.Fx is string fx) _stage = new EffectStage(fx, session.PlayerPosition + _options.FxAt, session.World);
            _stage?.Update(Dino_Engine.Core.Engine.Delta);
            if (_frames == 2 && _options.ParticleSheet is string sheet) Dino_Engine.Core.Engine.RenderEngine._particleRenderer.Flipbooks.WriteSheets(sheet);
            if (_frames == 60 && _options.OpenWay >= 0 && _server != null)
            {
                int choice = _options.OpenWay;
                _server.Ask(server =>
                {
                    server.Venture(choice, step: false);
                    return true;
                });
            }
            if (_frames == 30 && _options.Bring is string bring) BringCreatures(session, bring);
            if (_frames == _options.StartleAfterFrames) Nature.Startle(session.PlayerPosition, 40f);
            if (_frames == 30 && _options.Parade is string parade && _server != null)
            {
                _server.Ask(server =>
                {
                    server.Parade(parade);
                    return true;
                });
            }
            if (_frames == 30 && _options.Brawl is string brawl && _server != null)
            {
                _server.Ask(server =>
                {
                    server.Parade(brawl, fighting: true);
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
            if (_perf != null)
            {
                _perf.Frame(_frames, Dino_Engine.Core.Engine.Delta);
                if (_perf.Finished) Quit();
            }
            TakeSnap();
            if (_options.Tour != null) UpdateTour();
            if (_options.Film != null) UpdateFilm();
            foreach (var (frame, speed, direction) in _options.Wind)
            {
                if (_frames == frame) session.ScriptedWind = (speed, direction);
            }
            session.WeatherClockOffset = _options.WeatherClock;
            session.ScriptedHour = _options.Hour;
            if (_options.Weather != null && WeatherSample.TryNamed(_options.Weather, out WeatherSample ordered)) session.ScriptedWeather = ordered;
            if (_frames == _options.GuideAfterFrames) session.Intent.Guide = true;
        }

        // Where the giants are found, and what they're doing: somewhere else each time anyone comes home, but the same every time for a scripted run, so that its pictures can be compared.
        // Like everything alive, they're only looked at, and --nolife takes them out with the rest, for measuring what the game costs without them.
        private uint HerdSeed() => _options.Scripted ? 1u : (uint)Environment.TickCount;

        // A little way off, in a ring round the hero, so that they're near enough to look at and far enough off to be left in peace.
        private static void BringCreatures(GameSession session, string creatures)
        {
            string[] ids = creatures.Split(',');
            for (int i = 0; i < ids.Length; i++)
            {
                float angle = i * MathF.Tau / ids.Length + 0.6f;
                Nature.Bring(ids[i], session.PlayerPosition + new Vector2(MathF.Sin(angle), MathF.Cos(angle)) * 17f);
            }
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

            _ui.Clear();
            if (_ui.Loading.Visible) return;

            var window = Dino_Engine.Core.Engine.WindowHandler;
            if (Dino_Engine.Core.Engine.Gui.WantsKeyboard || _ui.Settings.Busy) return;

            // Escape always goes back, whatever else has been given that job, as someone has to be able to get out.
            bool back = window.IsKeyPressed(Keys.Escape) || Bindings.Asked(GameAction.Menu);
            if (_ui.AtMenu)
            {
                if (back && _ui.OnPage) _ui.Back();
                return;
            }

            if (back)
            {
                if (_ui.Hero.Active && _ui.Hero.StepBack()) return;
                if (_ui.OnPage && !_ui.Pause.Visible) _ui.Back();
                else if (_ui.AnyOpen) _ui.CloseAll();
                else _ui.Toggle(_ui.Pause);
                return;
            }
            if (_ui.OnPage)
            {
                if (_ui.Glossary.Visible && Bindings.Asked(GameAction.Glossary)) _ui.Back();
                return;
            }

            // The one key that's only a habit from everywhere else, and isn't anybody's to rebind.
            if (_ui.Hero.Visible && window.IsKeyPressed(Keys.F) && window.IsKeyDown(Keys.LeftControl)) _ui.Hero.FocusSearch();
            if (Bindings.Asked(GameAction.Map)) _ui.Toggle(_ui.Map);
            if (Bindings.Asked(GameAction.Hero)) _ui.Toggle(_ui.Hero);
            if (Bindings.Asked(GameAction.Chronicle)) _ui.Toggle(_ui.Chronicle);
            if (Bindings.Asked(GameAction.Talents)) _ui.Toggle(_ui.Talents);
            if (Bindings.Asked(GameAction.Books)) _ui.Toggle(_ui.Book);
            if (Bindings.Asked(GameAction.Glossary)) _ui.Toggle(_ui.Glossary);
            if (Bindings.Asked(GameAction.Roads)) GameSession.Current?.BeginCalling();
        }

        private void WriteTrace(string path)
        {
            _trace ??= new StreamWriter(path) { AutoFlush = true };
            HeroSimState hero = _client!.Predictor.Current;
            _trace.WriteLine(FormattableString.Invariant($"{_frames} {_client.Commands.Tick} {hero.Position.X:0.00} {hero.Position.Y:0.00} {hero.Using} {hero.ImpactIn:0.000} {hero.BusyFor:0.000} {hero.DashLeft:0.000} {hero.DodgeCooldown:0.000} {hero.Safe:0.000} {hero.Health:0.0}"));
        }

        private void OpenPanel(string name)
        {
            switch (name)
            {
                case "map": _ui.Toggle(_ui.Map); break;
                case "hero": _ui.Toggle(_ui.Hero); break;
                case "talents":
                    _ui.Toggle(_ui.Talents);
                    if (_options.TreeZoom > 0f) _ui.Talents.Look(_options.TreeZoom, _options.TreeFocus);
                    break;
                case "book": _ui.Toggle(_ui.Book); break;
                case "glossary": _ui.Toggle(_ui.Glossary); break;
                case "chronicle": _ui.Toggle(_ui.Chronicle); break;
                case "sound":
                    Dino_Engine.Core.Engine.DebugOverlay.Visible = true;
                    Dino_Engine.Core.Engine.DebugOverlay.SoundWindow.Visible = true;
                    break;
                case "friends": _ui.Toggle(_ui.Multiplayer); break;
                case "pause": _ui.Toggle(_ui.Pause); break;
                case "settings": _ui.Toggle(_ui.Pause); _ui.Into(_ui.Settings); break;
                case "table": _ui.Open(InteractKind.MapTable, GameSession.Current.Sites[0]); break;
                case "travel": _ui.Open(InteractKind.Waygate, GameSession.Current.Sites[0]); break;
                case "stash": _ui.Open(InteractKind.Stash, GameSession.Current.Sites[0]); break;
                case "vendor": _ui.Open(InteractKind.BetaVendor, GameSession.Current.Sites[0]); break;
                case "uniques":
                    _ui.Open(InteractKind.BetaVendor, GameSession.Current.Sites[0]);
                    _ui.Hero.Vendor.ShowLookup();
                    break;
                case "merchant":
                    // Through the server, as what's on a table is its to say.
                    int village = GameSession.Current.Sites.FindIndex(s => s.Site.Kind == SiteKind.Village);
                    _server?.Ask(server =>
                    {
                        server.OpenPanel(server.Heroes[0], InteractKind.Merchant, village);
                        return true;
                    });
                    break;
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
            Console.WriteLine($"Hero: level {_hero.Level}, {session.Health:0}/{session.Sheet.MaxHealth:0} health, {_hero.Gold} gold, {_hero.Carried().Count()} items, {fallen} enemies fallen, {cleared} sites cleared at {session.PlayerPosition.X:0},{session.PlayerPosition.Y:0}");

            HeroPredictor predictor = _client!.Predictor;
            Console.WriteLine($"Net: {_client.Party.Count} in the party, {_client.Ghosts.Ghosts.Count} ghosts, round trip {_client.RoundTrip:0} ms, drawn {_client.DrawnBehind:0} ms behind, {predictor.Corrections} corrections in {predictor.Compared} checks (worst {predictor.WorstCorrection:0.00} m), {_client.Ghosts.Mismatches} mismatches, {_client.BytesReceived / 1024} KB in, {_client.BytesSent / 1024} KB out");
            var grass = Dino_Engine.Core.Engine.RenderEngine._grassRenderer;
            Console.WriteLine($"Grass: {grass.NearBladesDrawn} near, {grass.FarBladesDrawn} far");
            Console.WriteLine($"Gear: {Wardrobe.Models} models alive, {Wardrobe.Made} made in all, worst frame's work {Wardrobe.WorstFrameMilliseconds:0.00} ms");
            if (Audio.Mixer is Dino_Engine.Sound.Mixing.Mixer mixer) Console.WriteLine($"Sound: {Audio.TakesMade} takes, at most {mixer.MostActiveVoices} voices, {mixer.Dropped} dropped, {mixer.Load * 100f:0.0}% mixing, {Audio.Output?.Underruns ?? 0} underruns on {Audio.Output?.Device ?? "no device"}, music {Music.Mood} at {Music.Intensity:0.00}, {Music.Gaps} gaps, heard {string.Join(" ", Music.Heard)}, {mixer.Faults} faults{(mixer.Fault != null ? $", the last in {mixer.Fault}" : "")}");
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
            // At the main menu nobody's being played, and whoever's on the spot is only being looked at.
            if (!_options.Saves || _hero == null || _ui.AtMenu) return;

            // A friend's world isn't this player's to keep, but the hero who went there is.
            if (_server != null && _client?.State == ClientState.Playing)
            {
                var world = new SaveGame();
                if (_server.Ask(server =>
                {
                    server.CaptureWorld(world);
                    return true;
                })) _save = world;
            }

            // Someone whose first steps were in a friend's world, or who's never been anywhere, is in the Haven.
            _save ??= new SaveGame();
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

        // For a scripted run: home from a land, by the nearest waygate as if walked to, or out of the Haven to the first land the map table shows.
        private void TravelOn(GameSession session)
        {
            Save();
            _ui.CloseAll();
            bool haven = session.IsHaven;
            _server?.Ask(server =>
            {
                if (haven) server.Venture(0);
                else server.SendHome();
                return true;
            });
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

        void IClientHost.WorldNeeded(WorldRequest request, LandTerms terms)
        {
            _ui.CloseAll();
            Audio.StopWorld(1.2f);
            Music.End(1.2f);
            if (GameSession.Current != null) Audio.Play(Treasure.Waygate);
            WorldPlan plan = WorldPlan.Create(request);
            if (Lands.IsHaven(request)) ShowLoading(plan.Name, "Home, where the Reich has never been");
            else ShowLoading(plan.Name, terms.Count > 0 ? $"{plan.Archetype.Name}, level {plan.Level}. {terms.Count} {(terms.Count == 1 ? "omen" : "omens")}: {string.Join(", ", terms.Chosen().Select(chosen => chosen.Omen.Name))}" : $"{plan.Archetype.Name}, level {plan.Level}");

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
            Task growing = Task.Run(() => _assets.Grow(WorldAssetLibrary.AssetsOf(world)));
            byte[] map = await Task.Run(() => WorldMapImage.Render(world, MapPanel.Resolution, showSites: false));
            await growing;
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
            _giants = null;
            WorldScene.Enter(Engine.world, world, _assets);
            _characters.Prepare();
            Console.WriteLine($"Prepared {_assets.Count} models in {clock.Elapsed.TotalSeconds:0.00} s");

            _title = null;
            _client!.EnterWorld(world);
            GameSession session = _client.Session!;
            Ambience.FindFeatures(session, _assets);
            session.UpdateGuidance();
            GameSession.Current = session;
            if (session.IsHaven && !Nature.Off) _giants = GiantShow.InHaven(Engine.world, world, _characters, HerdSeed());
            _ui.Map.SetImage(prepared.Map);
            _followingHero = false;

            // Up from here, until the hero arrives: everything that draws wants a camera to draw through.
            SpawnCameras(session, world.Sites[0].Landing);
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
            else if (session.IsHaven)
            {
                // Someone who's never been anywhere is told where they are, and anyone else is home.
                bool first = _hero.Level == 1 && _hero.Experience == 0 && _hero.WorldsCleared == 0;
                if (first && _options.Tour == null) session.Announce(session.World.Plan.Name, $"{session.Chronicle.Prologue} Choose a land at the map table, and the waygate will take you there.", 10f);
                else if (!_firstWorldOfThisSession) session.Announce(session.World.Plan.Name, $"Home. The map table opens any level up to {_hero.LandLevel}.", 5f);
            }
            else if (!session.Liberated && _options.Tour == null)
            {
                session.Announce(session.World.Plan.Name, $"Level {session.World.Plan.Level}. {session.Chronicle.Prologue}", 8f);
            }
            _firstWorldOfThisSession = false;
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
            if (_tourStop < _tourStops.Count) Console.WriteLine($"Tour: {_tourStops[_tourStop].name} at {_tourStops[_tourStop].position.X:0},{_tourStops[_tourStop].position.Y:0}");
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
