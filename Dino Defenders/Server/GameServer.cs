using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Talents;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Server
{
    /// <summary>What a server starts from.</summary>
    public sealed class ServerSettings
    {
        public WorldRequest Request;
        public LandTerms? Terms;        // What the land holds, past its seed and level.
        public SaveGame? Save;
        public bool AllowTeleport;      // For scripted runs: a client may ask to be put anywhere.
        public bool Calm;               // For scripted runs that look at a place: nothing that's there to be fought stirs, so that pictures can be taken among it.
        public string[] Buffs = Array.Empty<string>();     // For scripted runs: buffs each hero arrives under, by id, for the whole run.
        public bool SameSky;            // For scripted runs: the Haven begins at the same point in its day and weather every visit, so that pictures can be compared.
        public Vector2? Start;
        public string? StartSite;
        public string? StartBiome;
        public LootRule Loot;           // Who what drops is for, as the host chose it.
    }

    /// <summary>
    /// The game, with nobody watching. It holds everyone connected and every world someone's in (<see cref="ServerRoom"/>): the Haven, and the land the way is open to, side by side, so that
    /// each hero steps through when they're ready and goes home when they choose. Every world runs on the same tick, on one thread, and this is the only place anything is decided. There's no
    /// other way to play: someone alone runs one of these beside them, a host opens theirs up to friends, and a dedicated machine can run one with no window at all. Nothing in here draws, or
    /// knows that anything does.
    /// </summary>
    public sealed class GameServer : INetListener, IDisposable
    {
        private readonly ServerSettings _settings;
        private readonly WorldCache _worlds;
        private readonly List<INetTransport> _transports = new();
        private readonly Dictionary<INetConnection, ServerPlayer> _players = new();
        private readonly List<ServerRoom> _rooms = new();
        private readonly List<(ServerPlayer Player, ServerRoom To)> _moving = new();
        private int _nextPlayerId = 1;
        private int _epoch;
        private bool _partyChanged;

        /// <summary>Milliseconds, from any starting point.</summary>
        public Func<long> Clock = System.Diagnostics.Stopwatch.StartNew() is var watch ? () => watch.ElapsedMilliseconds : null!;

        /// <summary>The tick being run, or about to be, in every world.</summary>
        public uint Tick { get; private set; } = 1;

        /// <summary>Who what drops is for: anyone's for the taking, or given out in turn. The host's to choose.</summary>
        public LootRule Loot { get; private set; }

        /// <summary>Where the Haven's way is open to, if it is: the land, what it holds, and what it's called.</summary>
        public (WorldRequest Request, LandTerms Terms, string Name)? Way { get; private set; }

        // The land the way leads to, once it's open: the room it's grown in, which waits for its first hero.
        private ServerRoom? _land;

        public IReadOnlyList<ServerRoom> Rooms => _rooms;
        public IEnumerable<ServerPlayer> Players => _players.Values;
        public int PlayerCount => _players.Count;

        /// <summary>Largest snapshot sent, and how many bytes have gone out in all.</summary>
        public int LargestSnapshot { get; private set; }
        public long BytesSent { get; private set; }

        /// <summary>Whether nothing that's there to be fought stirs, for a scripted run that looks at a place (<see cref="ServerSettings.Calm"/>).</summary>
        public bool Calm => _settings.Calm;

        /// <summary>
        /// The world that's looked at from outside, for a scripted run or a test: wherever the first to have joined is, or the first world there is. What's below goes by it, as a server
        /// once held only one world.
        /// </summary>
        public ServerRoom Main => Host?.Room ?? _rooms[0];

        public ECSWorld Ecs => Main.Ecs;
        public ServerSession? Session => Main.Session;
        public IReadOnlyList<ServerPlayer> Heroes => Main.Heroes;
        public GhostServer Ghosts => Main.Ghosts;
        public bool Paused => Main.Paused;

        /// <summary>The one whose game this is: the first to have joined who's still here. They choose how loot goes.</summary>
        public ServerPlayer? Host
        {
            get
            {
                ServerPlayer? host = null;
                foreach (ServerPlayer player in _players.Values)
                {
                    if (player.Greeted && (host == null || player.Id < host.Id)) host = player;
                }
                return host;
            }
        }

        /// <summary>Whether anyone's in a land, or on their way into one: the way can't be opened to another until they're all back.</summary>
        public bool LandOccupied => _rooms.Any(room => room.IsLand && (room.Occupied || _moving.Any(move => move.To == room)));

        public GameServer(ServerSettings settings, WorldCache worlds)
        {
            _settings = settings;
            _worlds = worlds;
            Loot = settings.Loot;

            ServerRoom first = Open(settings.Request, settings.Terms ?? new LandTerms(), settings.Save);
            // A land begun from a save or a scripted run is what the Haven's way leads back to, for anyone who goes home from it while friends are still there.
            if (first.IsLand)
            {
                _land = first;
                Way = (first.Request, first.Terms, first.Name);
            }
        }

        public void AddTransport(INetTransport transport) => _transports.Add(transport);

        /// <summary>Runs one tick, in every world.</summary>
        public void Step()
        {
            foreach (ServerRoom room in _rooms) room.Settle();
            foreach (INetTransport transport in _transports) transport.Poll(this);
            foreach (ServerRoom room in _rooms.ToList()) room.Step();
            Tick++;

            Move();
            CloseEmpty();
            if (_partyChanged) SendParty();
        }

        public void Dispose() => Close("The host has left.");

        /// <summary>Ends the game for everyone, telling them why.</summary>
        public void Close(string reason)
        {
            foreach (ServerPlayer player in _players.Values) player.Connection.Close(reason);
            foreach (INetTransport transport in _transports) transport.Dispose();
            _transports.Clear();
        }

        /// <summary>A world of its own, grown as asked, which waits for whoever's sent into it.</summary>
        private ServerRoom Open(WorldRequest request, LandTerms terms, SaveGame? save = null)
        {
            var room = new ServerRoom(this, _settings, _worlds, request, terms, save, ++_epoch);
            _rooms.Add(room);
            return room;
        }

        private ServerRoom Haven() => _rooms.FirstOrDefault(room => room.IsHaven) ?? Open(Lands.Haven, new LandTerms());

        // Everyone who's asked to go somewhere goes, once every world has finished this tick: a world can't change under what's stepping through it.
        private void Move()
        {
            if (_moving.Count == 0) return;
            var moving = _moving.ToList();
            _moving.Clear();
            foreach ((ServerPlayer player, ServerRoom to) in moving)
            {
                if (!_players.ContainsValue(player) || player.Room == to || !_rooms.Contains(to)) continue;
                ServerRoom? from = player.Room;
                from?.Remove(player);
                to.Admit(player, Message.WorldChange, beside: false);
                _partyChanged = true;
            }
        }

        // A world nobody's in any more is over, but for the Haven, which everyone comes back to, a land the way's been opened to that nobody's stepped into yet, and the last world there is.
        private void CloseEmpty()
        {
            for (int i = _rooms.Count - 1; i >= 0; i--)
            {
                ServerRoom room = _rooms[i];
                if (room.IsHaven || room.Occupied || !room.Visited || _rooms.Count == 1 || _moving.Any(move => move.To == room)) continue;
                _rooms.RemoveAt(i);
                if (room != _land) continue;

                // Everyone's back from the land: the way shuts, and anyone can open it to another.
                _land = null;
                SetWay(null);
                Console.WriteLine($"Everyone is back from {room.Name}.");
            }
        }

        private void SetWay((WorldRequest Request, LandTerms Terms, string Name)? way)
        {
            Way = way;
            foreach (ServerRoom room in _rooms)
            {
                if (room.IsHaven) room.SetPortal(way);
            }
        }

        /// <summary>The way out of the Haven is open to a land, which begins growing now. A way opened before, that nobody's stepped through, goes.</summary>
        public void OpenWay(WorldRequest request, LandTerms terms, string name)
        {
            if (LandOccupied) return;
            if (_land != null) _rooms.Remove(_land);
            _land = Open(request, terms);
            SetWay((request, terms, name));
            _partyChanged = true;
        }

        /// <summary>Someone at the map table shuts the way, which can only be done while nobody's in the land it leads to.</summary>
        public void CloseWay(ServerPlayer player)
        {
            if (Way == null) return;
            if (LandOccupied)
            {
                player.Room?.Note(player, "Friends are still in that land. The way shuts once they're all back.");
                return;
            }
            if (_land != null) _rooms.Remove(_land);
            _land = null;
            SetWay(null);
            player.Room?.Note(null, $"{player.Name} has closed the way.");
        }

        /// <summary>Someone steps through the way into the land it leads to, unless they've fallen there already: there's one life to a land.</summary>
        public void StepThrough(ServerPlayer player)
        {
            if (_land == null || _moving.Any(move => move.Player == player)) return;
            if (_land.Session?.Fallen.Contains(player.Name) == true)
            {
                player.Room?.Note(player, "You fell in that land, and can't go back in. A friend can raise you while you're down, but not once you've left.");
                return;
            }
            _moving.Add((player, _land));
            if (player.Room is ServerRoom from) from.Note(null, $"{player.Name} has stepped through to {_land.Name}.");
        }

        /// <summary>Someone goes home to the Haven, and nobody else does. What the land meant for them has been settled already.</summary>
        public void GoHome(ServerPlayer player)
        {
            if (_moving.Any(move => move.Player == player)) return;
            ServerRoom? from = player.Room;
            _moving.Add((player, Haven()));
            if (from != null && from.Heroes.Count > 1) from.Note(null, $"{player.Name} has gone home to the Haven.");
        }

        /// <summary>Lexicon (developer tool, LEXICON.md): someone into the asset lexicon from the Haven, or home from it.</summary>
        public void Travel(ServerPlayer player, WorldRequest request)
        {
            if (_moving.Any(move => move.Player == player)) return;
            ServerRoom to = Lands.IsHaven(request) ? Haven() : _rooms.FirstOrDefault(room => room.Request.Seed == request.Seed && room.Request.ArchetypeId == request.ArchetypeId) ?? Open(request, new LandTerms());
            _moving.Add((player, to));
        }

        /// <summary>The host says who what drops is for from now on. Nobody else can.</summary>
        public void ChooseLootRule(ServerPlayer player, LootRule rule)
        {
            if (player != Host || !Enum.IsDefined(rule) || rule == Loot) return;
            Loot = rule;
            _partyChanged = true;
            foreach (ServerRoom room in _rooms) room.Note(null, rule == LootRule.Allocated ? "Loot is now allocated: each find is someone's, in turn." : "Loot is now free for all.");
        }

        /// <summary>
        /// Gold that's been picked up, shared out evenly among everyone in the party, wherever they are, and what's left over to whoever picked it up and those after them. Says what each got.
        /// </summary>
        public int ShareGold(ServerPlayer taker, int gold)
        {
            var party = _players.Values.Where(player => player.Greeted && player.Hero != null).OrderBy(player => player == taker ? 0 : 1).ThenBy(player => player.Id).ToList();
            if (party.Count == 0) return 0;
            int share = gold / party.Count;
            int over = gold - share * party.Count;
            for (int i = 0; i < party.Count; i++)
            {
                int given = share + (i < over ? 1 : 0);
                if (given <= 0) continue;
                party[i].Hero.Gold += given;
                party[i].Hero.Records.GoldGathered += given;
                party[i].SummaryChanged = true;
            }
            return share;
        }

        internal void Sent(int bytes)
        {
            LargestSnapshot = Math.Max(LargestSnapshot, bytes);
            BytesSent += bytes;
        }

        void INetListener.OnConnected(INetConnection connection)
        {
            _players[connection] = new ServerPlayer(_nextPlayerId++, connection);
        }

        void INetListener.OnDisconnected(INetConnection connection, string reason)
        {
            if (!_players.Remove(connection, out ServerPlayer? player)) return;
            Console.WriteLine($"{player.Name} left: {reason}");
            _moving.RemoveAll(move => move.Player == player);

            if (player.Room is ServerRoom room)
            {
                bool standing = room.Heroes.Contains(player);
                room.Remove(player);
                if (standing) room.Note(null, $"{player.Name} has gone their own way.");
            }
            _partyChanged = true;
        }

        void INetListener.OnMessage(INetConnection connection, byte[] data, Delivery delivery)
        {
            if (!_players.TryGetValue(connection, out ServerPlayer? player)) return;
            try
            {
                var reader = new BitReader(data);
                var type = (Message)reader.ReadByte();
                if (type == Message.Hello) OnHello(player, reader);
                else if (player.Greeted && player.Room is ServerRoom room) room.Hear(player, type, reader);
            }
            catch (NetReadException e)
            {
                // Whoever this is, they're not a client of ours. Everyone else plays on.
                Console.WriteLine($"Dropping {connection.Address}: {e.Message}");
                Refuse(player, "A message from you made no sense.");
            }
        }

        public void Refuse(ServerPlayer player, string reason)
        {
            BitWriter writer = Protocol.Begin(Message.Refused);
            writer.WriteString(reason);
            player.Connection.Send(writer.ToArray(), Delivery.Reliable);
            player.Connection.Close(reason);
        }

        private void OnHello(ServerPlayer player, BitReader reader)
        {
            if (player.Greeted) return;

            uint protocol = reader.ReadUInt();
            uint schema = reader.ReadUInt();
            uint build = reader.ReadUInt();
            if (protocol != Protocol.Version || schema != NetSchema.Hash || build != Protocol.Build)
            {
                Refuse(player, "You and the host are running different versions of the game. Close it and open the launcher to update, both of you.");
                return;
            }

            string name = reader.ReadString(Protocol.LongestName).Trim();
            HeroState hero = Protocol.ReadHero(reader);
            Stash? stash = reader.ReadBool() ? Protocol.ReadStash(reader) : null;

            if (_players.Values.Count(p => p.Greeted) >= Protocol.MaxPlayers)
            {
                Refuse(player, $"This game is full: {Protocol.MaxPlayers} heroes at most.");
                return;
            }

            player.Name = Unique(name.Length > 0 ? name : $"Hero {player.Id}");
            player.Hero = hero;
            player.Stash = stash;
            TalentTree.Sanitize(hero);
            player.Greeted = true;

            // Into the Haven if there is one, so that they step into a land when they're ready, and otherwise beside whoever's here.
            ServerRoom room = _rooms.FirstOrDefault(r => r.IsHaven) ?? Main;
            room.Admit(player, Message.Welcome, beside: true);
            _partyChanged = true;
            Console.WriteLine($"{player.Name} joined from {player.Connection.Address}.");
        }

        // A hero is known by their name, so two of the same are told apart, as who's fallen in a land is remembered by it.
        private string Unique(string name)
        {
            string unique = name;
            for (int n = 2; _players.Values.Any(p => p.Greeted && p.Name == unique); n++) unique = $"{name} {n}";
            return unique;
        }

        /// <summary>Who's in the game and where, and how loot goes, to everyone, whenever any of it changes.</summary>
        public void SendParty()
        {
            _partyChanged = false;
            BitWriter writer = Protocol.Begin(Message.Party);
            var greeted = _players.Values.Where(p => p.Greeted).OrderBy(p => p.Id).ToList();
            writer.WriteVarInt(Host?.Id ?? 0);
            writer.WriteByte((byte)Loot);
            writer.WriteCount(greeted.Count);
            foreach (ServerPlayer player in greeted)
            {
                writer.WriteVarInt(player.Id);
                writer.WriteString(player.Name);
                writer.WriteString(player.Room?.Name ?? "");
                writer.WriteBool(player.Room?.IsLand == true);
            }
            byte[] data = writer.ToArray();
            foreach (ServerPlayer player in greeted) player.Connection.Send(data, Delivery.Reliable);
        }

        /// <summary>Where a place's waygate is used from, if it has one.</summary>
        public static Vector2? WaygateSpawn(WorldData world, WorldSite site) => ServerRoom.WaygateSpawn(world, site);

        /// <summary>Somewhere to stand near a point, looking outward until there is.</summary>
        public static bool TryFindGround(WorldData world, Vector2 near, out Vector2 ground) => ServerRoom.TryFindGround(world, near, out ground);

        /// <summary>Somewhere to stand near a point that's <paramref name="allowed"/>, looking outward until there is.</summary>
        public static bool TryFindGround(WorldData world, Vector2 near, Func<Vector2, bool> allowed, out Vector2 ground) => ServerRoom.TryFindGround(world, near, allowed, out ground);

        /// <summary>A scripted run's --site that stands the hero in front of whichever of a land's waygates the camera sees most squarely, for looking at its way.</summary>
        public const string FacingWaygate = ServerRoom.FacingWaygate;

        /// <summary>Opens a panel for someone, in whatever world they're in.</summary>
        public void OpenPanel(ServerPlayer hero, InteractKind kind, int site) => hero.Room?.OpenPanel(hero, kind, site);

        /// <summary>The room someone is in, or on their way into.</summary>
        public ServerRoom? RoomOf(ServerPlayer player) => player.Room;

        /// <summary>For a scripted run: one of everything, in front of the first hero, standing still or not.</summary>
        public void Parade(string which, bool fighting = false) => Main.Parade(which, fighting);

        /// <summary>For a scripted run: everyone home to the Haven from wherever they are, as if through a waygate.</summary>
        public void SendHome()
        {
            foreach (ServerRoom room in _rooms.ToList())
            {
                if (room.IsHaven) continue;
                foreach (ServerPlayer hero in room.Heroes.ToList()) room.GoHome(hero);
            }
        }

        /// <summary>For a scripted run: everyone in the Haven through the open way at once.</summary>
        public void StepThrough()
        {
            foreach (ServerRoom room in _rooms.Where(room => room.IsHaven).ToList())
            {
                foreach (ServerPlayer hero in room.Heroes.ToList()) room.StepThrough(hero);
            }
        }

        /// <summary>For a scripted run: out of the Haven to one of the lands the map table shows the first hero at the deepest level they can open, everyone at once, or only opening the way to it.</summary>
        public void Venture(int choice, bool step = true)
        {
            if (_rooms.FirstOrDefault(room => room.IsHaven && room.Heroes.Count > 0) is not ServerRoom haven) return;
            haven.Venture(choice);
            if (step) StepThrough();
        }

        /// <summary>For a scripted run: loot in front of the first hero.</summary>
        public void ShowLoot(string which) => Main.ShowLoot(which);

        /// <summary>Writes the world the host is in, and where they stand in it, into a save. The hero's own record is added on their machine.</summary>
        public void CaptureWorld(SaveGame save) => Main.CaptureWorld(save, Host);
    }
}
