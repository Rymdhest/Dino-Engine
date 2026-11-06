using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>What a client needs from whatever it is running in: the game, or a test with no window.</summary>
    public interface IClientHost : IGhostHost
    {
        /// <summary>The player's input as of now.</summary>
        HeroIntent ReadIntent();

        /// <summary>A world is wanted. When it is ready, call <see cref="GameClient.EnterWorld"/>.</summary>
        void WorldNeeded(WorldRequest request, Offering offering);

        /// <summary>The hero is standing in the world at last.</summary>
        void Arrived(Vector2 position);

        /// <summary>The hero did something, by the player's hand, this instant: the show can start before the server has heard.</summary>
        void HeroActed(HeroStepEvents events, in HeroSimState hero);

        void OpenPanel(InteractKind kind, SiteState site);

        void Disconnected(string reason);
    }

    public enum ClientState
    {
        Connecting,
        Loading,
        Waiting,    // Has the world, and is waiting to be put in it.
        Playing,
        Disconnected,
    }

    /// <summary>
    /// One player's end of the game, the same whether the server is beside it in this process or across the world. Input goes
    /// out as commands; what comes back is drawn. All it decides for itself is where its own hero is about to be.
    /// </summary>
    public sealed class GameClient : INetListener, IDisposable
    {
        private const int FirstMargin = 3;      // Ticks of spare to start with, on top of the measured trip.
        private const int MostDamaged = 30;
        private const long TakenFor = 1500;     // Milliseconds something asked for stays hidden before it's taken not to have happened.

        private readonly INetTransport _transport;
        private readonly IClientHost _host;
        private readonly ECSWorld _ecs;
        private readonly string _name;
        private readonly List<HeroCommand> _recent = new();
        private readonly ulong _predictedMask;

        private INetConnection? _connection;
        private WorldRequest _request;
        private Offering _offering = new();
        private CharacterMover? _mover;
        private int _epoch;
        private uint _ownGhost;
        private uint _lastSnapshotTick;
        private HeroIntent _intent;
        private HeroButtons _presses;
        private string? _refusal;
        private int _damaged;
        private Loadout _loadout;
        private Condition _state;
        private Counters _counters;
        private int _gearAsked;     // How many changes to what's carried have been made here and asked of the server.
        private int _tableAsked;    // The last of those that touched a merchant's table.

        public readonly NetEventHandlers Events = new();
        public readonly GhostClient Ghosts;
        public ECSWorld Ecs => _ecs;
        public readonly CommandClock Commands = new();
        public readonly InterpolationClock Render = new();
        public readonly HeroPredictor Predictor = new();
        public readonly List<(int id, string name)> Party = new();

        /// <summary>What each hero has on, by player, as last told.</summary>
        public readonly Dictionary<int, List<WornLook>> Worn = new();

        /// <summary>This player has reached for something on the ground, and whether there was room for it. It's heard at once, as a click that answers late feels broken.</summary>
        public event Action<Item, bool>? Reached;

        /// <summary>Milliseconds, from any starting point.</summary>
        public Func<long> Clock = System.Diagnostics.Stopwatch.StartNew() is var watch ? () => watch.ElapsedMilliseconds : null!;

        public HeroState Hero { get; }

        /// <summary>
        /// What this hero comes to right now, worked out here from the same record and the same state as the server's, so that the two can't
        /// disagree for longer than a message takes.
        /// </summary>
        public ActorSheet Sheet { get; private set; }
        public HeroKit Kit { get; private set; }
        public readonly List<ActiveBuff> Buffs = new();
        public SkillDefinition? Stolen { get; private set; }
        public GameSession? Session { get; private set; }
        public ClientState State { get; private set; } = ClientState.Connecting;
        public int PlayerId { get; private set; }

        /// <summary>Whether this hero has the world to themselves, and can stop it by opening a panel.</summary>
        public bool Alone { get; private set; } = true;

        /// <summary>Round trip to the server in milliseconds, smoothed.</summary>
        public float RoundTrip { get; private set; }

        public long BytesReceived { get; private set; }
        public long BytesSent { get; private set; }

        /// <summary>How far behind the newest word from the server the world is being drawn, in milliseconds.</summary>
        public float DrawnBehind => Render.Started ? (float)((_lastSnapshotTick - Render.RenderTick) * NetTime.TickSeconds * 1000.0) : 0f;

        /// <summary>This hero's ghost, once it has one.</summary>
        public Ghost? OwnGhost { get; private set; }

        public GameClient(INetTransport transport, IClientHost host, ECSWorld ecs, HeroState hero, string name)
        {
            _transport = transport;
            _host = host;
            _ecs = ecs;
            _name = name;
            Hero = hero;
            _loadout = HeroBuild.Compile(hero);
            Sheet = _loadout.SheetFor(Condition.None);
            Kit = new HeroKit(hero, Sheet);
            Ghosts = new GhostClient(ecs, new OwnGhostWatcher(this, host), Events);
            _predictedMask = (1ul << NetSchema.IndexOfComponent(typeof(PositionComponent))) | (1ul << NetSchema.IndexOfComponent(typeof(RotationComponent)));
        }

        // Spots this hero's ghost among the others as it appears, on its way to the game's own host.
        private sealed class OwnGhostWatcher : IGhostHost
        {
            private readonly GameClient _client;
            private readonly IGhostHost _host;

            public OwnGhostWatcher(GameClient client, IGhostHost host)
            {
                _client = client;
                _host = host;
            }

            public void Decorate(Ghost ghost, List<IComponent> components)
            {
                if (ghost.Id == _client._ownGhost) ghost.Predicted = _client._predictedMask;
                _host.Decorate(ghost, components);
            }

            public void Created(Ghost ghost)
            {
                if (ghost.Id == _client._ownGhost)
                {
                    _client.OwnGhost = ghost;
                    if (_client.Session != null) _client.Session.Player = ghost.Entity;
                }
                _host.Created(ghost);
            }

            public void Removing(Ghost ghost)
            {
                if (ghost == _client.OwnGhost) _client.OwnGhost = null;
                _host.Removing(ghost);
            }
        }

        /// <summary>Everything for one frame: hear what has come in, run the ticks that are due, and say what the hero is doing.</summary>
        public void Update(double seconds)
        {
            if (State == ClientState.Disconnected) return;
            _transport.Poll(this);
            if (State == ClientState.Disconnected) return;

            Render.Advance(seconds);
            if (State == ClientState.Playing)
            {
                if (Commands.Running) UpdateHero(seconds);
                else SendCommands();
            }

            Ghosts.CreatePending();
            if (Render.Started) Events.Flush(Render.RenderTick);
        }

        private void UpdateHero(double seconds)
        {
            // Read every frame, so that a press in a frame with no tick due still lands in the next one.
            HeroIntent intent = _host.ReadIntent();
            _intent = intent;
            _presses |= (HeroButtons)intent.Slots & HeroButtons.Presses;
            if (intent.Dash) _presses |= HeroButtons.Dash;
            if (intent.Flask) _presses |= HeroButtons.Flask;
            if (intent.Interact) _presses |= HeroButtons.Interact;

            int due = Commands.Advance(seconds);
            for (int i = 0; i < due; i++) RunTick();
            if (due > 0) SendCommands();
            Predictor.Smooth((float)seconds);

            GameSession? session = Session;
            if (session == null) return;
            HeroSimState hero = Predictor.Current;
            session.PlayerPosition = Predictor.DrawnPosition(Commands.Alpha);
            session.Sim = hero;
            session.Intent = intent;
        }

        private void RunTick()
        {
            uint tick = Commands.Tick;
            int lag = (int)Math.Clamp(tick - Render.RenderTick, 0.0, 255.0);

            var command = HeroCommand.From(_intent, tick, (byte)lag);
            command.Buttons = (command.Buttons & ~HeroButtons.Presses) | _presses;
            _presses = HeroButtons.None;
            if (Session is { Paused: true })
            {
                command = new HeroCommand { Tick = tick, ViewLag = (byte)lag };
                if (Alone) command.Buttons |= HeroButtons.Frozen;
            }

            HeroStepEvents events = Predictor.Step(command, Kit, _mover!);
            if (events != HeroStepEvents.None) _host.HeroActed(events, Predictor.Current);

            _recent.Add(command);
            if (_recent.Count > Protocol.MostCommands) _recent.RemoveAt(0);
            Commands.Next();
        }

        // Every packet carries each command the server hasn't confirmed yet, not just the newest. So a lost packet is simply filled in by the
        // next one, and a command only fails to arrive if a whole run of packets in a row goes missing.
        private void SendCommands()
        {
            if (_connection == null) return;

            BitWriter writer = Protocol.Begin(Message.Commands);
            writer.WriteVarInt(_epoch);
            writer.WriteUInt(Ghosts.LatestSequence);
            writer.WriteUInt(Ghosts.AckBits);
            writer.WriteBool(Ghosts.NeedsReset);
            Ghosts.NeedsReset = false;
            writer.WriteUInt((uint)Clock());
            writer.WriteVarUInt((uint)RoundTrip);

            // Before the hero starts this is still worth sending, empty: it's what measures the line.
            uint newest = _recent.Count > 0 ? _recent[^1].Tick : 0;
            int count = 0;
            for (int i = _recent.Count - 1; i >= 0 && _recent[i].Tick == newest - (uint)count; i--) count++;

            writer.WriteUInt(newest);
            writer.WriteByte((byte)count);
            for (int i = 0; i < count; i++) _recent[_recent.Count - 1 - i].Write(writer);

            byte[] data = writer.ToArray();
            BytesSent += data.Length;
            _connection.Send(data, Delivery.Unreliable);
        }

        /// <summary>The world is ready: stands by to be put in it.</summary>
        public void EnterWorld(WorldData world)
        {
            Session = new GameSession(this, world, _request, _offering);
            _mover = new CharacterMover(world);
            State = ClientState.Waiting;

            (uint structure, uint ground) = Protocol.WorldHash(world);
            BitWriter writer = Protocol.Begin(Message.WorldReady);
            writer.WriteVarInt(_epoch);
            writer.WriteUInt(structure);
            writer.WriteUInt(ground);
            Send(writer);
        }

        private void Send(BitWriter writer) => _connection?.Send(writer.ToArray(), Delivery.Reliable);

        void INetListener.OnConnected(INetConnection connection)
        {
            _connection = connection;
            BitWriter writer = Protocol.Begin(Message.Hello);
            writer.WriteUInt(Protocol.Version);
            writer.WriteUInt(NetSchema.Hash);
            writer.WriteUInt(Protocol.Build);
            writer.WriteString(_name);
            Protocol.WriteHero(writer, Hero);
            Send(writer);
        }

        void INetListener.OnDisconnected(INetConnection connection, string reason)
        {
            State = ClientState.Disconnected;
            _host.Disconnected(_refusal ?? reason);
        }

        void INetListener.OnMessage(INetConnection connection, byte[] data, Delivery delivery)
        {
            BytesReceived += data.Length;
            try
            {
                var reader = new BitReader(data);
                var type = (Message)reader.ReadByte();
                switch (type)
                {
                    case Message.Welcome:
                    case Message.WorldChange:
                        OnWorld(reader);
                        break;
                    case Message.Refused:
                        _refusal = reader.ReadString(Protocol.LongestText);
                        break;
                    case Message.Arrived:
                        OnArrived(reader);
                        break;
                    case Message.Snapshot:
                        OnSnapshot(reader);
                        break;
                    case Message.HeroUpdate:
                        OnHeroUpdate(reader);
                        break;
                    case Message.Progress:
                        OnProgress(reader);
                        break;
                    case Message.Notice:
                        OnNotice(reader);
                        break;
                    case Message.OpenPanel:
                        OnOpenPanel(reader);
                        break;
                    case Message.Market:
                        OnMarket(reader);
                        break;
                    case Message.Party:
                        OnParty(reader);
                        break;
                    case Message.GroundItem:
                        OnGroundItem(reader);
                        break;
                    case Message.Worn:
                        (int wearer, List<WornLook> worn) = Protocol.ReadWorn(reader);
                        Worn[wearer] = worn;
                        break;
                }
            }
            catch (NetReadException e)
            {
                // A damaged packet is an ordinary thing, and the next one says the same and more. A stream of them is something else.
                Console.WriteLine($"A message from the server made no sense: {e.Message}");
                if (++_damaged > MostDamaged) connection.Close("The server is sending nonsense.");
            }
        }

        private void OnWorld(BitReader reader)
        {
            _epoch = reader.ReadVarInt();
            PlayerId = reader.ReadVarInt();
            reader.ReadUInt();
            WorldRequest request = Protocol.ReadRequest(reader);
            _offering = Protocol.ReadOffering(reader);
            _request = _offering.Shape(request);

            // Everything of the last world goes with it: ghosts, clocks, and any hero still running around in it.
            Ghosts.Clear();
            Render.Reset();
            Commands.Stop();
            _recent.Clear();
            OwnGhost = null;
            _lastSnapshotTick = 0;
            _ownGhost = 0;
            Session = null;
            State = ClientState.Loading;
            _host.WorldNeeded(_request, _offering);
        }

        private void OnArrived(BitReader reader)
        {
            int epoch = reader.ReadVarInt();
            reader.ReadUInt();
            uint ghost = reader.ReadVarUInt();
            Vector2 position = new Vector2(reader.ReadFloat(), reader.ReadFloat());
            if (epoch != _epoch || Session == null) return;

            _ownGhost = ghost;
            Session.PlayerPosition = position;
            State = ClientState.Playing;
            _host.Arrived(position);
        }

        private void OnSnapshot(BitReader reader)
        {
            int epoch = reader.ReadVarInt();
            uint tick = reader.ReadUInt();
            int interval = reader.ReadByte();
            bool alone = reader.ReadBool();
            int spare = reader.ReadVarInt();
            HeroSimState truth = HeroSimState.Read(reader);
            float barrier = reader.ReadFloat();
            float debt = reader.ReadFloat();
            uint sentAt = reader.ReadUInt();
            uint heldFor = reader.ReadVarUInt();
            uint confirmed = reader.ReadUInt();

            if (epoch != _epoch || State != ClientState.Playing || tick <= _lastSnapshotTick) return;
            if (interval < 1 || !float.IsFinite(truth.Position.X) || !float.IsFinite(truth.Position.Y) || !float.IsFinite(truth.Health) || truth.Using >= HeroState.SkillSlots || truth.Queued >= HeroState.SkillSlots)
            {
                throw new NetReadException("A snapshot that can't be true.");
            }

            Ghosts.ReadSnapshot(reader, tick);
            _lastSnapshotTick = tick;
            Alone = alone;
            Render.OnSnapshot(tick, interval);

            Refit(truth.State, truth.Counters);
            if (Session != null)
            {
                Session.Health = truth.Health;
                Session.Barrier = barrier;
                Session.Debt = debt;
            }
            if (sentAt != 0)
            {
                long sample = (uint)Clock() - (long)sentAt - heldFor;
                if (sample >= 0) RoundTrip = RoundTrip == 0f ? sample : RoundTrip + (sample - RoundTrip) * 0.1f;
            }

            while (_recent.Count > 1 && _recent[0].Tick <= confirmed) _recent.RemoveAt(0);

            if (!Commands.Running)
            {
                Predictor.Reset(truth, tick);

                // This snapshot left half a trip ago, and a command takes half a trip to get back: starting a whole round trip
                // ahead of it, and a little over, means commands are on time from the first. Guessing here would cost
                // every new arrival some seconds of being corrected while the clock found its way.
                if (RoundTrip > 0f || sentAt != 0)
                {
                    uint trip = (uint)MathF.Ceiling(RoundTrip / (1000f * NetTime.TickSeconds));
                    Commands.Start(tick + trip + FirstMargin);
                }
                return;
            }

            int jump = Commands.OnFeedback(spare);
            if (jump != 0)
            {
                // Commands numbered from here on no longer follow on from those already sent.
                _recent.Clear();
                Predictor.Jump(tick, truth, Commands.Tick - 1, Kit, _mover!);
                return;
            }
            Predictor.Reconcile(tick, truth, Kit, _mover!);
        }

        private void OnHeroUpdate(BitReader reader)
        {
            Protocol.ReadHeroSummary(reader, Hero);
            Protocol.ReadBuffs(reader, Buffs, out SkillDefinition? stolen);
            Stolen = stolen;
            if (reader.ReadBool())
            {
                // Sent before the server had heard all that's been done here, this would only undo it for a moment. Whatever it's missing is on its way.
                int answered = reader.ReadVarInt();
                Protocol.ReadGear(reader, answered == _gearAsked ? Hero : new HeroState());
            }
            Recompile();
        }

        private void Recompile()
        {
            _loadout = HeroBuild.Compile(Hero, Buffs, Stolen);
            Sheet = _loadout.SheetFor(_state, _counters);
            Kit = new HeroKit(Hero, Sheet);
        }

        // The same sheet the server has for the hero, given what the server says is true of them.
        private void Refit(Condition state, in Counters counters)
        {
            _state = state;
            _counters = counters;
            ActorSheet sheet = _loadout.SheetFor(state, counters);
            if (ReferenceEquals(sheet, Sheet)) return;
            Sheet = sheet;
            Kit = new HeroKit(Hero, sheet);
        }

        private void OnProgress(BitReader reader)
        {
            bool liberated = reader.ReadBool();
            int falls = reader.ReadVarInt();
            bool broken = reader.ReadBool();
            int objectives = reader.ReadCount(64);
            var progress = new int[objectives];
            for (int i = 0; i < objectives; i++) progress[i] = reader.ReadVarInt();

            GameSession? session = Session;
            int sites = reader.ReadCount(100000);
            for (int i = 0; i < sites; i++)
            {
                int index = reader.ReadVarInt();
                bool discovered = reader.ReadBool();
                bool cleared = reader.ReadBool();
                bool kindled = reader.ReadBool();
                bool rewarded = reader.ReadBool();
                var dead = new int[reader.ReadCount(100000)];
                for (int j = 0; j < dead.Length; j++) dead[j] = reader.ReadVarInt();
                var opened = new int[reader.ReadCount(100000)];
                for (int j = 0; j < opened.Length; j++) opened[j] = reader.ReadVarInt();

                if (session == null || index < 0 || index >= session.Sites.Count) continue;
                SiteState state = session.Sites[index];
                state.Discovered = discovered;
                state.Cleared = cleared;
                state.Kindled = kindled;
                state.Rewarded = rewarded;
                state.Dead.Clear();
                state.Dead.UnionWith(dead);
                state.Opened.Clear();
                state.Opened.UnionWith(opened);
            }

            if (session == null) return;
            session.Liberated = liberated;
            session.Falls = falls;
            session.Broken = broken;
            for (int i = 0; i < objectives && i < session.Chronicle.Objectives.Count; i++) session.Chronicle.Objectives[i].Progress = progress[i];
        }

        private void OnNotice(BitReader reader)
        {
            var kind = (NoticeKind)reader.ReadByte();
            if (kind == NoticeKind.Banner)
            {
                string title = reader.ReadString(Protocol.LongestText);
                string subtitle = reader.ReadString(Protocol.LongestText);
                float life = reader.ReadFloat();
                Session?.Announce(title, subtitle, Math.Clamp(life, 0.5f, 30f));
            }
            else
            {
                Session?.Note(reader.ReadString(Protocol.LongestText));
            }
        }

        private void OnOpenPanel(BitReader reader)
        {
            var kind = (InteractKind)reader.ReadByte();
            int site = reader.ReadVarInt();
            if (Session != null && site >= 0 && site < Session.Sites.Count) _host.OpenPanel(kind, Session.Sites[site]);
        }

        private void OnMarket(BitReader reader)
        {
            int site = reader.ReadVarInt();
            int answered = reader.ReadVarInt();
            Item?[] stock = Protocol.ReadCells(reader, Protocol.MostItems);
            Item?[] sold = Protocol.ReadCells(reader, MerchantTable.MostSold);
            if (TableAt(site) is not MerchantTable table || answered < _tableAsked) return;
            table.Stock = stock;
            sold.CopyTo(table.Sold, 0);
        }

        private MerchantTable? TableAt(int site) => Session != null && site >= 0 && site < Session.Sites.Count ? Session.Sites[site].Table : null;

        private void OnParty(BitReader reader)
        {
            Party.Clear();
            int count = reader.ReadCount(Protocol.MaxPlayers * 4);
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadVarInt();
                Party.Add((id, reader.ReadString(Protocol.LongestName)));
            }
        }

        private void OnGroundItem(BitReader reader)
        {
            int find = reader.ReadVarInt();
            Item? item = reader.ReadBool() ? Protocol.ReadItem(reader) : null;
            if (Session == null) return;

            if (item != null) Session.Ground[find] = item;
            else
            {
                Session.Ground.Remove(find);
                Session.Taken.Remove(find);
            }
        }

        private BitWriter Action(HeroAction action)
        {
            BitWriter writer = Protocol.Begin(Message.Action);
            writer.WriteByte((byte)action);
            return writer;
        }

        public void Allocate(int node)
        {
            BitWriter writer = Action(HeroAction.Allocate);
            writer.WriteVarInt(node);
            Send(writer);
        }

        public void Refund(int node)
        {
            BitWriter writer = Action(HeroAction.Refund);
            writer.WriteVarInt(node);
            Send(writer);
        }

        public void ForgetTalents() => Send(Action(HeroAction.ForgetTalents));

        public void AssignSlot(int slot, SkillDefinition? skill)
        {
            BitWriter writer = Action(HeroAction.AssignSlot);
            writer.WriteByte((byte)slot);
            writer.WriteVarInt(skill?.Number ?? 0);
            Send(writer);
        }

        // What a hero carries is the one thing here that's changed before the server has said so, by the same rules it will use, as a pack that takes a moment to answer every click is
        // no pleasure to use. All of it is sent whatever the rules said here, as the server has the last word, and it answers each of these with the truth.
        private BitWriter AskGear(HeroAction action)
        {
            BitWriter writer = Action(action);
            writer.WriteVarInt(++_gearAsked);
            return writer;
        }

        /// <param name="index">The cell it's in. What comes off goes into that same cell.</param>
        /// <param name="place">Where to put it, for what could go in more than one place. Otherwise the server finds it room.</param>
        public void Equip(int index, WornSlot? place = null)
        {
            Hero.Equip(index, place);
            BitWriter writer = AskGear(HeroAction.Equip);
            writer.WriteVarInt(index);
            writer.WriteBool(place != null);
            if (place != null) writer.WriteByte((byte)place.Value);
            Send(writer);
            Recompile();
        }

        public void Unequip(WornSlot slot)
        {
            Hero.Unequip(slot);
            BitWriter writer = AskGear(HeroAction.Unequip);
            writer.WriteByte((byte)slot);
            Send(writer);
            Recompile();
        }

        /// <summary>Moves something, and whatever's there changes places with it.</summary>
        public void Move(ItemPlace from, ItemPlace to)
        {
            Hero.Move(from, to);
            BitWriter writer = AskGear(HeroAction.Move);
            writer.WriteVarInt(from.Number);
            writer.WriteVarInt(to.Number);
            Send(writer);
            Recompile();
        }

        /// <summary>Tidies the pack, which is the only thing that ever moves what's in it without being told where.</summary>
        public void SortPack()
        {
            Hero.Sort();
            Send(AskGear(HeroAction.Sort));
        }

        /// <summary>Puts something on the ground beside the hero, for anyone to pick up.</summary>
        public void Drop(ItemPlace place)
        {
            Hero.Take(place);
            BitWriter writer = AskGear(HeroAction.Drop);
            writer.WriteVarInt(place.Number);
            Send(writer);
            Recompile();
        }

        public void Sell(int site, ItemPlace place)
        {
            TableAt(site)?.Sell(Hero, place);
            BitWriter writer = AskGear(HeroAction.Sell);
            _tableAsked = _gearAsked;
            writer.WriteVarInt(site);
            writer.WriteVarInt(place.Number);
            Send(writer);
            Recompile();
        }

        /// <param name="buyingBack">Whether it's from what they've sold there, rather than what's for sale.</param>
        /// <param name="cell">Where in the pack it should go, if anywhere in particular.</param>
        public void Buy(int site, int index, bool buyingBack = false, int? cell = null)
        {
            TableAt(site)?.Buy(Hero, buyingBack, index, cell);
            BitWriter writer = AskGear(HeroAction.Buy);
            _tableAsked = _gearAsked;
            writer.WriteVarInt(site);
            writer.WriteBool(buyingBack);
            writer.WriteVarInt(index);
            writer.WriteBool(cell != null);
            if (cell != null) writer.WriteVarInt(cell.Value);
            Send(writer);
        }

        public void BuyFlasks() => Send(Action(HeroAction.BuyFlasks));

        public void TravelToSite(int site)
        {
            BitWriter writer = Action(HeroAction.TravelToSite);
            writer.WriteVarInt(site);
            Send(writer);
        }

        /// <summary>For scripted runs: only a server that allows it will listen.</summary>
        public void TravelTo(Vector2 point)
        {
            BitWriter writer = Action(HeroAction.TravelToPoint);
            writer.WriteFloat(point.X);
            writer.WriteFloat(point.Y);
            Send(writer);
        }

        public void StepThroughWaygate(in WorldRequest request, Offering offering)
        {
            BitWriter writer = Action(HeroAction.Waygate);
            Protocol.WriteRequest(writer, request);
            Protocol.WriteOffering(writer, offering);
            Send(writer);
        }

        /// <summary>Takes something from the ground, for a hero who has walked up to it. It looks gone at once, and the server has the last word.</summary>
        public void PickUp(int find)
        {
            GameSession? session = Session;
            if (session == null || !session.Ground.TryGetValue(find, out Item? item)) return;

            if (!Hero.HasRoom())
            {
                session.Note("Your pack is full.");
                Reached?.Invoke(item, false);
                return;
            }

            BitWriter writer = Action(HeroAction.PickUp);
            writer.WriteVarInt(find);
            Send(writer);
            session.Taken[find] = Clock() + TakenFor;
            Reached?.Invoke(item, true);
        }

        public void ThankElder(int site)
        {
            BitWriter writer = Action(HeroAction.ThankElder);
            writer.WriteVarInt(site);
            Send(writer);
        }

        public void Dispose()
        {
            _connection?.Close("Left the game.");
            _transport.Dispose();
        }
    }
}
