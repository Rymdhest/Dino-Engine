using Dino_Engine.Networking;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Dino_Defenders.Net
{
    /// <summary>
    /// The internet, by way of LiteNetLib: UDP with a reliable channel on top. It's the only file that knows about that library, but for <see cref="Meeting"/>, which
    /// gets two games behind home routers to each other. Steam's relay network goes in beside this one, as another <see cref="INetTransport"/>, and nothing else has
    /// to change.
    /// </summary>
    public sealed class UdpTransport : INetTransport, INetEventListener
    {
        public const int DefaultPort = 27815;

        /// <summary>What a join that heard nothing back is told: nothing got to the host, or its answers didn't get back.</summary>
        public const string NobodyAnswered = "Nobody answered at that address.";

        private const int TimeoutMilliseconds = 10000;
        private const int KnockFor = 24;               // Tries, half a second apart, at every way to a host: long enough for its router to be opened from inside.
        private const int MostPeers = Protocol.MaxPlayers * 2;  // Some room over the limit, so that the game itself can say it's full.

        private readonly NetManager _manager;
        private readonly StunLayer _stun = new();
        private readonly string _key;
        private readonly bool _accepting;
        private readonly Dictionary<NetPeer, Connection> _connections = new();
        private INetListener? _listener;
        private MeetingHost? _meeting;

        // A join knocks at every way to the host at once, and keeps whichever answers first.
        private Task<List<IPEndPoint>>? _finding;
        private readonly Dictionary<NetPeer, IPEndPoint> _knocking = new();
        private readonly List<string> _turnedAway = new();
        private readonly HashSet<NetPeer> _givenUp = new();
        private NetPeer? _through;

        public int Port => _manager.LocalPort;

        /// <summary>Whether friends outside can find this game, once it's meeting them (<see cref="Meet"/>).</summary>
        public string? MeetingStatus => _meeting?.Status;

        private UdpTransport(string key, bool accepting)
        {
            _key = key;
            _accepting = accepting;
            _manager = new NetManager(this, _stun)
            {
                AutoRecycle = true,
                DisconnectTimeout = TimeoutMilliseconds,
                UnsyncedEvents = false,
                MaxConnectAttempts = KnockFor,
            };
        }

        /// <summary>Opens a port for friends to reach. Throws if it can't be had.</summary>
        public static UdpTransport Host(int port, string key)
        {
            var transport = new UdpTransport(key, accepting: true);
            if (!transport._manager.Start(port)) throw new IOException($"Port {port} is in use.");
            return transport;
        }

        /// <summary>
        /// Keeps word in the mailbox of where on the internet this game is, and opens the way to any friend's game that says it's coming (<see cref="Meeting"/>).
        /// Talks to servers on the internet, so a scripted run leaves it alone.
        /// </summary>
        public void Meet(uint secret, IMeetingPlace? place = null, IReadOnlyList<(string Host, int Port)>? stunServers = null)
        {
            _meeting ??= new MeetingHost(_manager, _stun, secret, place ?? Ntfy.Shared, stunServers ?? Meeting.StunServers);
        }

        /// <summary>Connects to a host at one address.</summary>
        public static UdpTransport Join(IPEndPoint address, string key)
        {
            var transport = new UdpTransport(key, accepting: false);
            if (!transport._manager.Start()) throw new IOException("Couldn't open a port to connect from.");
            transport._finding = Task.FromResult(new List<IPEndPoint> { address });
            return transport;
        }

        /// <summary>
        /// Joins by an invite: learns how the internet sees this game's port, leaves that for the host in the mailbox, reads where the host left word it is, and
        /// knocks at every way to it at once, along with the invite's own addresses. The host sending to this game meanwhile is what opens its router to the knocks.
        /// </summary>
        public static UdpTransport Join(InviteCode code, IMeetingPlace? place = null, IReadOnlyList<(string Host, int Port)>? stunServers = null)
        {
            var transport = new UdpTransport(code.Key, accepting: false);
            if (!transport._manager.Start()) throw new IOException("Couldn't open a port to connect from.");
            transport._finding = Task.Run(() => transport.Find(code, place ?? Ntfy.Shared, stunServers ?? Meeting.StunServers));
            return transport;
        }

        private async Task<List<IPEndPoint>> Find(InviteCode code, IMeetingPlace place, IReadOnlyList<(string Host, int Port)> stunServers)
        {
            string topic = Meeting.Topic(code.Secret);
            (IPEndPoint? seen, string? by) = await _stun.Ask(_manager, stunServers, CancellationToken.None).ConfigureAwait(false);
            GameLog.Network(seen != null
                ? $"Joining: the internet sees this game's port as {seen} (asked {by})."
                : "Joining: no STUN server answered, so the host can't be told where on the internet to send.");

            var at = new List<IPEndPoint>();
            if (seen != null) at.Add(seen);
            at.AddRange(InviteCode.LocalAddresses().Select(address => new IPEndPoint(address, _manager.LocalPort)));
            try
            {
                await place.Post(topic, Meeting.Seal(code.Secret, new Note(Note.Join, Note.NewId(), Protocol.Version, at)), CancellationToken.None).ConfigureAwait(false);
                GameLog.Network($"Joining: left word in the mailbox ({place.Name}) that this game is at {string.Join(", ", at)}, for the host to send to.");
            }
            catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException)
            {
                GameLog.Network($"Joining: couldn't reach the mailbox ({place.Name}) to tell the host: {e.Message}");
            }

            var ways = new List<IPEndPoint>();
            try
            {
                // ntfy.sh only shows a note to readers a second or so after it's left, so a host that's only just opened its game is asked again before giving up on it.
                Note? host = null;
                for (int read = 0; read < 3 && host == null; read++)
                {
                    if (read > 0) await Task.Delay(1500).ConfigureAwait(false);
                    host = (await place.Read(topic, CancellationToken.None).ConfigureAwait(false))
                        .Select(text => Meeting.Open(code.Secret, text))
                        .LastOrDefault(note => note?.Role == Note.Host);
                }
                if (host != null)
                {
                    GameLog.Network($"Joining: the host left word in the mailbox that it's at {string.Join(", ", host.At)}{(host.Protocol != Protocol.Version ? $", running protocol {host.Protocol}" : "")}.");
                    // Its address at home only means anything to a game in the same home.
                    ways.AddRange(host.At.Where(address => !InviteCode.IsPrivate(address.Address) || InviteCode.OnSameNetwork(address.Address)));
                }
                else GameLog.Network("Joining: no word from the host in the mailbox: it's an older build, or couldn't reach it. Only the invite's addresses are left.");
            }
            catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException)
            {
                GameLog.Network($"Joining: couldn't read the mailbox ({place.Name}): {e.Message}. Only the invite's addresses are left.");
            }

            foreach (IPEndPoint address in code.Addresses())
            {
                if (!ways.Contains(address)) ways.Add(address);
            }
            return ways;
        }

        public void Poll(INetListener listener)
        {
            _listener = listener;
            if (_finding is { IsCompleted: true } finding)
            {
                _finding = null;
                Knock(finding.IsCompletedSuccessfully ? finding.Result : new List<IPEndPoint>());
            }
            _manager.PollEvents();
            _listener = null;
        }

        private void Knock(List<IPEndPoint> ways)
        {
            foreach (IPEndPoint way in ways)
            {
                if (_manager.Connect(way, _key) is NetPeer peer) _knocking[peer] = way;
            }
            if (ways.Count > 1) GameLog.Network($"Joining: knocking at {string.Join(", ", ways)} at once.");
            if (_knocking.Count == 0) _listener?.OnDisconnected(new Nowhere(), ways.Count == 0 ? "There was nowhere to try: the invite held no address, and the host left no word." : NobodyAnswered);
        }

        public void Dispose()
        {
            _meeting?.Dispose();
            _manager.Stop();
        }

        // Every knock is logged, as whether anything reached the host at all is what tells a provider or router keeping friends out from anything else. Whoever's
        // turned away is told why, in words: a bare refusal reads the same on their end whatever the reason was.
        void INetEventListener.OnConnectionRequest(ConnectionRequest request)
        {
            if (!_accepting)
            {
                request.Reject();
                return;
            }

            string? theirs = null;
            try
            {
                theirs = request.Data.PeekString();
            }
            catch (Exception exception) when (exception is ArgumentException or IndexOutOfRangeException or OverflowException)
            {
                // Not a key at all: WhyNot says so.
            }

            string? why = _manager.ConnectedPeersCount >= MostPeers ? "This game is full." : InviteCode.WhyNot(theirs, _key);
            if (why != null)
            {
                GameLog.Network($"Hosting: {request.RemoteEndPoint} knocked and was turned away: {why}");
                request.Reject(Encoding.UTF8.GetBytes(why));
                return;
            }
            GameLog.Network($"Hosting: {request.RemoteEndPoint} knocked with the right invite, and was let in.");
            request.AcceptIfKey(_key);
        }

        void INetEventListener.OnPeerConnected(NetPeer peer)
        {
            if (!_accepting)
            {
                // The first way through is the one kept. Any other that gets there too is let go, and the host hears it leave before it ever said hello.
                if (_through != null)
                {
                    _givenUp.Add(peer);
                    peer.Disconnect();
                    return;
                }
                _through = peer;
                if (_knocking.Count > 1) GameLog.Network($"Joining: got through at {peer}.");
                foreach (NetPeer other in _knocking.Keys.Where(other => other != peer).ToList())
                {
                    _givenUp.Add(other);
                    other.Disconnect();
                }
            }

            var connection = new Connection(peer);
            _connections[peer] = connection;
            _listener?.OnConnected(connection);
        }

        void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            if (_givenUp.Remove(peer)) return;
            string reason = Describe(info);

            // A way that failed while others are still being tried says nothing yet. Once they all have, the reason worth hearing is a refusal in words, if any.
            if (!_accepting && _through == null && _knocking.TryGetValue(peer, out IPEndPoint? way))
            {
                if (_knocking.Count > 1) GameLog.Network($"Joining: {way}: {reason}");
                _turnedAway.Add(reason);
                if (_turnedAway.Count < _knocking.Count) return;
                reason = _turnedAway.FirstOrDefault(said => said != NobodyAnswered) ?? NobodyAnswered;
            }

            if (_connections.Remove(peer, out Connection? connection))
            {
                _listener?.OnDisconnected(connection, reason);
            }
            else
            {
                // It never got as far as connecting: the caller still needs to hear that it failed.
                _listener?.OnDisconnected(new Connection(peer), reason);
            }
        }

        void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
        {
            if (!_connections.TryGetValue(peer, out Connection? connection)) return;
            byte[] data = reader.GetRemainingBytes();
            _listener?.OnMessage(connection, data, method == DeliveryMethod.Unreliable ? Delivery.Unreliable : Delivery.Reliable);
        }

        void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            GameLog.Network($"{(_accepting ? "Hosting" : "Joining")}: network error with {endPoint}: {socketError}");
        }

        void INetEventListener.OnMessageDelivered(NetPeer peer, object userData) { }
        void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
        void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }
        void INetEventListener.OnNtpResponse(LiteNetLib.Utils.NtpPacket packet) { }
        void INetEventListener.OnPeerAddressChanged(NetPeer peer, IPEndPoint previousAddress) { }

        private static string Describe(DisconnectInfo info)
        {
            if (info.AdditionalData != null && info.AdditionalData.AvailableBytes > 0)
            {
                return Encoding.UTF8.GetString(info.AdditionalData.GetRemainingBytes());
            }

            return info.Reason switch
            {
                DisconnectReason.ConnectionFailed => NobodyAnswered,
                DisconnectReason.ConnectionRejected => "The host turned you away. Is that invite still good?",
                DisconnectReason.Timeout => "The connection timed out.",
                DisconnectReason.HostUnreachable or DisconnectReason.NetworkUnreachable => "That address can't be reached from here.",
                DisconnectReason.RemoteConnectionClose => "The other end has left.",
                DisconnectReason.InvalidProtocol => "You and the host are running different versions of the game.",
                _ => $"Disconnected: {info.Reason}.",
            };
        }

        private sealed class Connection : INetConnection
        {
            private readonly NetPeer _peer;

            public Connection(NetPeer peer)
            {
                _peer = peer;
            }

            public int MaxUnreliableSize => _peer.GetMaxSinglePacketSize(DeliveryMethod.Unreliable);
            public string Address => _peer.ToString();

            public void Send(byte[] data, Delivery delivery)
            {
                if (_peer.ConnectionState != ConnectionState.Connected) return;
                _peer.Send(data, delivery == Delivery.Reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable);
            }

            public void Close(string reason) => _peer.Disconnect(Encoding.UTF8.GetBytes(reason));
        }

        // What a join that never had anywhere to knock reports failing.
        private sealed class Nowhere : INetConnection
        {
            public int MaxUnreliableSize => 0;
            public string Address => "nowhere";
            public void Send(byte[] data, Delivery delivery) { }
            public void Close(string reason) { }
        }
    }
}
