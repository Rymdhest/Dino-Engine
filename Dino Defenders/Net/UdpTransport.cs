using Dino_Engine.Networking;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Dino_Defenders.Net
{
    /// <summary>
    /// The internet, by way of LiteNetLib: UDP with a reliable channel on top. It's the only file that knows about that library.
    /// Steam's relay network goes in beside this one, as another <see cref="INetTransport"/>, and nothing else has to change.
    /// </summary>
    public sealed class UdpTransport : INetTransport, INetEventListener
    {
        public const int DefaultPort = 27815;

        private const int TimeoutMilliseconds = 10000;
        private const int MostPeers = Protocol.MaxPlayers * 2;  // Some room over the limit, so that the game itself can say it's full.

        private readonly NetManager _manager;
        private readonly string _key;
        private readonly bool _accepting;
        private readonly Dictionary<NetPeer, Connection> _connections = new();
        private INetListener? _listener;

        public int Port => _manager.LocalPort;

        private UdpTransport(string key, bool accepting)
        {
            _key = key;
            _accepting = accepting;
            _manager = new NetManager(this)
            {
                AutoRecycle = true,
                DisconnectTimeout = TimeoutMilliseconds,
                UnsyncedEvents = false,
            };
        }

        /// <summary>Opens a port for friends to reach. Throws if it can't be had.</summary>
        public static UdpTransport Host(int port, string key)
        {
            var transport = new UdpTransport(key, accepting: true);
            if (!transport._manager.Start(port)) throw new IOException($"Port {port} is in use.");
            return transport;
        }

        /// <summary>Connects to a host.</summary>
        public static UdpTransport Join(IPEndPoint address, string key)
        {
            var transport = new UdpTransport(key, accepting: false);
            if (!transport._manager.Start()) throw new IOException("Couldn't open a port to connect from.");
            transport._manager.Connect(address, key);
            return transport;
        }

        public void Poll(INetListener listener)
        {
            _listener = listener;
            _manager.PollEvents();
            _listener = null;
        }

        public void Dispose() => _manager.Stop();

        void INetEventListener.OnConnectionRequest(ConnectionRequest request)
        {
            if (!_accepting || _manager.ConnectedPeersCount >= MostPeers)
            {
                request.Reject();
                return;
            }
            request.AcceptIfKey(_key);
        }

        void INetEventListener.OnPeerConnected(NetPeer peer)
        {
            var connection = new Connection(peer);
            _connections[peer] = connection;
            _listener?.OnConnected(connection);
        }

        void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            string reason = Describe(info);
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
            Console.WriteLine($"Network error with {endPoint}: {socketError}");
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
                DisconnectReason.ConnectionFailed => "Nobody answered at that address.",
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
    }
}
