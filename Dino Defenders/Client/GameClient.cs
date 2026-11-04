using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
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
        void WorldNeeded(WorldRequest request);

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
        private const int StartingLead = 6;     // Ticks, until the server has said how we're doing.
        private const int MostDamaged = 30;

        private readonly INetTransport _transport;
        private readonly IClientHost _host;
        private readonly ECSWorld _ecs;
        private readonly string _name;
        private readonly List<HeroCommand> _recent = new();
        private readonly ulong _predictedMask;

        private INetConnection? _connection;
        private WorldRequest _request;
        private CharacterMover? _mover;
        private int _epoch;
        private uint _ownGhost;
        private uint _lastSnapshotTick;
        private bool _hadSnapshot;
        private HeroIntent _intent;
        private HeroButtons _presses;
        private string? _refusal;
        private int _damaged;

        public readonly NetEventHandlers Events = new();
        public readonly GhostClient Ghosts;
        public readonly CommandClock Commands = new();
        public readonly InterpolationClock Render = new();
        public readonly HeroPredictor Predictor = new();
        public readonly List<(int id, string name)> Party = new();

        /// <summary>Milliseconds, from any starting point.</summary>
        public Func<long> Clock = System.Diagnostics.Stopwatch.StartNew() is var watch ? () => watch.ElapsedMilliseconds : null!;

        public HeroState Hero { get; }
        public HeroStats Stats { get; private set; }
        public GameSession? Session { get; private set; }
        public ClientState State { get; private set; } = ClientState.Connecting;
        public int PlayerId { get; private set; }

        /// <summary>Whether this hero has the world to themselves, and can stop it by opening a panel.</summary>
        public bool Alone { get; private set; } = true;

        /// <summary>Round trip to the server in milliseconds, smoothed.</summary>
        public float RoundTrip { get; private set; }

        public long BytesReceived { get; private set; }
        public long BytesSent { get; private set; }

        /// <summary>This hero's ghost, once it has one.</summary>
        public Ghost? OwnGhost { get; private set; }

        public GameClient(INetTransport transport, IClientHost host, ECSWorld ecs, HeroState hero, string name)
        {
            _transport = transport;
            _host = host;
            _ecs = ecs;
            _name = name;
            Hero = hero;
            Stats = hero.Compute();
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
            if (State == ClientState.Playing) UpdateHero(seconds);

            Ghosts.CreatePending();
            if (Render.Started) Events.Flush(Render.RenderTick);
        }

        private void UpdateHero(double seconds)
        {
            // Read every frame, so that a press in a frame with no tick due still lands in the next one.
            HeroIntent intent = _host.ReadIntent();
            _intent = intent;
            if (intent.Dash) _presses |= HeroButtons.Dash;
            if (intent.Whirl) _presses |= HeroButtons.Whirl;
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
            session.SwingCooldown = hero.SwingCooldown;
            session.DashCooldown = hero.DashCooldown;
            session.WhirlCooldown = hero.WhirlCooldown;
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

            HeroStepEvents events = Predictor.Step(command, Stats, _mover!);
            if (events != HeroStepEvents.None) _host.HeroActed(events, Predictor.Current);

            _recent.Add(command);
            if (_recent.Count > Protocol.MostCommands) _recent.RemoveAt(0);
            Commands.Next();
        }

        // Every packet carries each command the server hasn't confirmed yet, not just the newest. So a lost packet is simply filled in by the
        // next one, and a command only fails to arrive if a whole run of packets in a row goes missing.
        private void SendCommands()
        {
            if (_connection == null || _recent.Count == 0) return;

            BitWriter writer = Protocol.Begin(Message.Commands);
            writer.WriteVarInt(_epoch);
            writer.WriteUInt(Ghosts.LatestSequence);
            writer.WriteUInt(Ghosts.AckBits);
            writer.WriteBool(Ghosts.NeedsReset);
            Ghosts.NeedsReset = false;
            writer.WriteUInt((uint)Clock());
            writer.WriteVarUInt((uint)RoundTrip);

            uint newest = _recent[^1].Tick;
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
            Session = new GameSession(this, world, _request);
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
            _request = Protocol.ReadRequest(reader);

            // Everything of the last world goes with it: ghosts, clocks, and any hero still running around in it.
            Ghosts.Clear();
            Render.Reset();
            Commands.Stop();
            _recent.Clear();
            OwnGhost = null;
            _hadSnapshot = false;
            _lastSnapshotTick = 0;
            _ownGhost = 0;
            Session = null;
            State = ClientState.Loading;
            _host.WorldNeeded(_request);
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
            float health = reader.ReadFloat();
            uint sentAt = reader.ReadUInt();
            uint heldFor = reader.ReadVarUInt();
            uint confirmed = reader.ReadUInt();

            if (epoch != _epoch || State != ClientState.Playing || tick <= _lastSnapshotTick) return;
            if (interval < 1 || !float.IsFinite(truth.Position.X) || !float.IsFinite(truth.Position.Y) || !float.IsFinite(health))
            {
                throw new NetReadException("A snapshot that can't be true.");
            }

            Ghosts.ReadSnapshot(reader, tick);
            _lastSnapshotTick = tick;
            Alone = alone;
            Render.OnSnapshot(tick, interval);

            if (Session != null) Session.Health = health;
            if (sentAt != 0)
            {
                long sample = (uint)Clock() - (long)sentAt - heldFor;
                if (sample >= 0) RoundTrip = RoundTrip == 0f ? sample : RoundTrip + (sample - RoundTrip) * 0.1f;
            }

            while (_recent.Count > 1 && _recent[0].Tick <= confirmed) _recent.RemoveAt(0);

            if (!_hadSnapshot)
            {
                _hadSnapshot = true;
                Predictor.Reset(truth, tick);
                Commands.Start(tick + StartingLead);
                return;
            }

            int jump = Commands.OnFeedback(spare);
            if (jump != 0)
            {
                // Commands numbered from here on no longer follow on from those already sent.
                _recent.Clear();
                Predictor.Jump(tick, truth, Commands.Tick - 1, Stats, _mover!);
                return;
            }
            Predictor.Reconcile(tick, truth, Stats, _mover!);
        }

        private void OnHeroUpdate(BitReader reader)
        {
            Protocol.ReadHeroSummary(reader, Hero);
            if (reader.ReadBool()) Protocol.ReadGear(reader, Hero);
            Stats = Hero.Compute();
        }

        private void OnProgress(BitReader reader)
        {
            bool liberated = reader.ReadBool();
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
            List<Item> items = Protocol.ReadItems(reader);
            if (Session == null || site < 0 || site >= Session.Sites.Count) return;
            List<Item> stock = Session.Sites[site].Stock;
            stock.Clear();
            stock.AddRange(items);
        }

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

        private BitWriter Action(HeroAction action)
        {
            BitWriter writer = Protocol.Begin(Message.Action);
            writer.WriteByte((byte)action);
            return writer;
        }

        public void RaiseAttribute(HeroAttribute attribute)
        {
            BitWriter writer = Action(HeroAction.RaiseAttribute);
            writer.WriteByte((byte)attribute);
            Send(writer);
        }

        public void Equip(int index)
        {
            BitWriter writer = Action(HeroAction.Equip);
            writer.WriteVarInt(index);
            Send(writer);
        }

        public void Unequip(ItemSlot slot)
        {
            BitWriter writer = Action(HeroAction.Unequip);
            writer.WriteByte((byte)slot);
            Send(writer);
        }

        public void Sell(int index)
        {
            BitWriter writer = Action(HeroAction.Sell);
            writer.WriteVarInt(index);
            Send(writer);
        }

        public void Buy(int site, int index)
        {
            BitWriter writer = Action(HeroAction.Buy);
            writer.WriteVarInt(site);
            writer.WriteVarInt(index);
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

        public void StepThroughWaygate(in WorldRequest request)
        {
            BitWriter writer = Action(HeroAction.Waygate);
            Protocol.WriteRequest(writer, request);
            Send(writer);
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
