using LiteNetLib;
using LiteNetLib.Layers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dino_Defenders.Net
{
    /// <summary>
    /// How two games behind home routers reach each other with nothing of ours in between, as other games do. A router lets in only answers to what went out, and a
    /// provider sharing one address among many customers (carrier-grade NAT) lets in nothing at all that nobody asked for, so waiting for a friend to knock can't work.
    /// Instead each game asks a public STUN server how the internet sees its port, they leave that for each other in a mailbox, and both send to the other at once:
    /// each one's packets going out are what open its own router to the other's coming in. It needs a router that keeps one public port for a game whoever it's
    /// sending to, as nearly all home routers and providers' do; one that doesn't (a "symmetric" NAT) would need a relay, which this isn't.
    /// The mailbox is ntfy.sh, which is free and needs no account, under a topic made from the invite's secret, and every note is sealed with that secret, so
    /// what it holds is nothing to anyone without the invite. STUN servers are Google's and Cloudflare's, as browsers use for the same thing.
    /// </summary>
    public static class Meeting
    {
        public static readonly IReadOnlyList<(string Host, int Port)> StunServers = new[] { ("stun.l.google.com", 19302), ("stun.cloudflare.com", 3478) };

        public static string Topic(uint secret) =>
            "dino-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"dino-defenders-meeting-topic:{secret:x8}")))[..24].ToLowerInvariant();

        private static byte[] Key(uint secret) => SHA256.HashData(Encoding.UTF8.GetBytes($"dino-defenders-meeting-key:{secret:x8}"));

        public static string Seal(uint secret, Note note)
        {
            byte[] plain = Encoding.UTF8.GetBytes(note.Text);
            byte[] sealedNote = new byte[12 + 16 + plain.Length];
            Span<byte> nonce = sealedNote.AsSpan(0, 12);
            RandomNumberGenerator.Fill(nonce);
            using var aes = new AesGcm(Key(secret), 16);
            aes.Encrypt(nonce, plain, sealedNote.AsSpan(28), sealedNote.AsSpan(12, 16));
            return Convert.ToBase64String(sealedNote);
        }

        /// <summary>A note left by someone holding the same invite, or null for anything else.</summary>
        public static Note? Open(uint secret, string text)
        {
            try
            {
                byte[] sealedNote = Convert.FromBase64String(text.Trim());
                if (sealedNote.Length < 28) return null;
                byte[] plain = new byte[sealedNote.Length - 28];
                using var aes = new AesGcm(Key(secret), 16);
                aes.Decrypt(sealedNote.AsSpan(0, 12), sealedNote.AsSpan(28), sealedNote.AsSpan(12, 16), plain);
                return Note.Parse(Encoding.UTF8.GetString(plain));
            }
            catch (Exception e) when (e is FormatException or CryptographicException)
            {
                return null;
            }
        }
    }

    /// <summary>What a game leaves in the mailbox: whether it's the host or on its way, and every address it might be reached at, the internet's view first.</summary>
    public sealed record Note(string Role, string Id, uint Protocol, IReadOnlyList<IPEndPoint> At)
    {
        public const string Host = "host";
        public const string Join = "join";

        public string Text => $"{Role}|{Id}|{Protocol}|{string.Join(",", At)}";

        public static Note? Parse(string text)
        {
            string[] parts = text.Split('|');
            if (parts.Length != 4 || !uint.TryParse(parts[2], out uint protocol)) return null;
            var at = new List<IPEndPoint>();
            foreach (string address in parts[3].Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (IPEndPoint.TryParse(address, out IPEndPoint? endPoint)) at.Add(endPoint);
            }
            return new Note(parts[0], parts[1], protocol, at);
        }

        public static string NewId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
    }

    /// <summary>
    /// Asks STUN servers how the internet sees the game's own port, through the game's own socket, as that port's mapping in the router is the one friends need: a
    /// second socket would have one of its own. LiteNetLib has no way to send raw bytes, so a question goes out as an unconnected message holding a marker, which
    /// is swapped for the STUN request on its way out, and the answer is taken before LiteNetLib sees it.
    /// </summary>
    public sealed class StunLayer : PacketLayerBase
    {
        private const uint Cookie = 0x2112A442;
        private const int AskMilliseconds = 1500;
        private static readonly byte[] Marker = Encoding.ASCII.GetBytes("dino-defenders-stun-ask!");      // Longer than a request, so the request fits where it was.

        private readonly object _lock = new();
        private readonly Dictionary<IPEndPoint, Asking> _asking = new();

        private sealed record Asking(byte[] Request, TaskCompletionSource<IPEndPoint?> Answer);

        public StunLayer() : base(0) { }

        /// <summary>The first answer from any of these servers, in order, and which one gave it.</summary>
        public async Task<(IPEndPoint? Seen, string? By)> Ask(LiteNetManager manager, IReadOnlyList<(string Host, int Port)> servers, CancellationToken cancel)
        {
            foreach ((string host, int port) in servers)
            {
                IPAddress? address;
                try
                {
                    address = (await Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork, cancel)).FirstOrDefault();
                }
                catch (SocketException)
                {
                    continue;
                }
                if (address == null) continue;

                IPEndPoint? seen = await Ask(manager, new IPEndPoint(address, port), cancel);
                if (seen != null) return (seen, host);
            }
            return (null, null);
        }

        private async Task<IPEndPoint?> Ask(LiteNetManager manager, IPEndPoint server, CancellationToken cancel)
        {
            byte[] request = new byte[20];
            BinaryPrimitives.WriteUInt16BigEndian(request, 0x0001);         // A binding request, with nothing more to it.
            BinaryPrimitives.WriteUInt32BigEndian(request.AsSpan(4), Cookie);
            RandomNumberGenerator.Fill(request.AsSpan(8, 12));
            var asking = new Asking(request, new TaskCompletionSource<IPEndPoint?>(TaskCreationOptions.RunContinuationsAsynchronously));
            lock (_lock) _asking[server] = asking;

            try
            {
                for (int tries = 0; tries < 3 && !asking.Answer.Task.IsCompleted && !cancel.IsCancellationRequested; tries++)
                {
                    manager.SendUnconnectedMessage(Marker, server);
                    await Task.WhenAny(asking.Answer.Task, Task.Delay(AskMilliseconds / 3, cancel)).ConfigureAwait(false);
                }
                return asking.Answer.Task.IsCompletedSuccessfully ? asking.Answer.Task.Result : null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                lock (_lock) _asking.Remove(server);
            }
        }

        public override void ProcessOutBoundPacket(ref IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            if (length < Marker.Length || data.Length - offset < 20) return;
            lock (_lock)
            {
                if (_asking.Count == 0 || !_asking.TryGetValue(Plain(endPoint), out Asking? asking)) return;
                if (!data.AsSpan(offset + length - Marker.Length, Marker.Length).SequenceEqual(Marker)) return;
                asking.Request.CopyTo(data, offset);
                length = asking.Request.Length;
            }
        }

        public override void ProcessInboundPacket(ref IPEndPoint endPoint, ref byte[] data, ref int length)
        {
            // A binding success response: type 0x0101 and the magic cookie. Nothing LiteNetLib sends looks like that.
            if (length < 20 || data[0] != 0x01 || data[1] != 0x01 || BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4)) != Cookie) return;

            Asking? answered = null;
            lock (_lock)
            {
                foreach (Asking asking in _asking.Values)
                {
                    if (data.AsSpan(8, 12).SequenceEqual(asking.Request.AsSpan(8, 12))) answered = asking;
                }
            }
            if (answered == null) return;

            answered.Answer.TrySetResult(Mapped(data.AsSpan(0, length)));
            length = 0;
        }

        // XOR-MAPPED-ADDRESS, or the plain MAPPED-ADDRESS of older servers.
        private static IPEndPoint? Mapped(ReadOnlySpan<byte> message)
        {
            IPEndPoint? plain = null;
            for (int at = 20; at + 4 <= message.Length;)
            {
                int type = BinaryPrimitives.ReadUInt16BigEndian(message[at..]);
                int size = BinaryPrimitives.ReadUInt16BigEndian(message[(at + 2)..]);
                ReadOnlySpan<byte> value = message.Slice(at + 4, Math.Min(size, message.Length - at - 4));
                if (value.Length >= 8 && value[1] == 0x01)
                {
                    int port = BinaryPrimitives.ReadUInt16BigEndian(value[2..]);
                    byte[] address = value.Slice(4, 4).ToArray();
                    if (type == 0x0020)
                    {
                        port ^= (int)(Cookie >> 16);
                        for (int i = 0; i < 4; i++) address[i] ^= (byte)(Cookie >> (24 - 8 * i));
                        return new IPEndPoint(new IPAddress(address), port);
                    }
                    if (type == 0x0001) plain = new IPEndPoint(new IPAddress(address), port);
                }
                at += 4 + ((size + 3) & ~3);
            }
            return plain;
        }

        private static IPEndPoint Plain(IPEndPoint endPoint) =>
            endPoint.Address.IsIPv4MappedToIPv6 ? new IPEndPoint(endPoint.Address.MapToIPv4(), endPoint.Port) : endPoint;
    }

    /// <summary>Where notes are left for whoever else holds the invite: ntfy.sh in the game, a list in the labs.</summary>
    public interface IMeetingPlace
    {
        string Name { get; }

        Task Post(string topic, string text, CancellationToken cancel);

        /// <summary>Every note still there, oldest first.</summary>
        Task<IReadOnlyList<string>> Read(string topic, CancellationToken cancel);

        /// <summary>Hears every note left from <paramref name="sinceUnixSeconds"/> on, until the line drops or it's cancelled.</summary>
        Task Listen(string topic, long sinceUnixSeconds, Action<string> heard, CancellationToken cancel);
    }

    public sealed class Ntfy : IMeetingPlace
    {
        public static readonly Ntfy Shared = new();

        private const string Server = "https://ntfy.sh/";
        private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(100);       // ntfy says something every 45 s on a line that's alive.
        private static readonly HttpClient Http = MakeClient();

        public string Name => "ntfy.sh";

        private static HttpClient MakeClient()
        {
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DinoDefenders");
            return client;
        }

        public async Task Post(string topic, string text, CancellationToken cancel)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            limit.CancelAfter(TimeSpan.FromSeconds(10));
            using var content = new StringContent(text, Encoding.ASCII);
            using HttpResponseMessage response = await Http.PostAsync(Server + topic, content, limit.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        public async Task<IReadOnlyList<string>> Read(string topic, CancellationToken cancel)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            limit.CancelAfter(TimeSpan.FromSeconds(10));
            string body = await Http.GetStringAsync($"{Server}{topic}/json?poll=1&since=12h", limit.Token).ConfigureAwait(false);
            var notes = new List<string>();
            foreach (string line in body.Split('\n'))
            {
                if (Message(line) is string note) notes.Add(note);
            }
            return notes;
        }

        public async Task Listen(string topic, long sinceUnixSeconds, Action<string> heard, CancellationToken cancel)
        {
            using var quiet = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            quiet.CancelAfter(Quiet);
            using HttpResponseMessage response = await Http.GetAsync($"{Server}{topic}/json?since={sinceUnixSeconds}", HttpCompletionOption.ResponseHeadersRead, quiet.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(quiet.Token).ConfigureAwait(false));
            while (await reader.ReadLineAsync(quiet.Token).ConfigureAwait(false) is string line)
            {
                quiet.CancelAfter(Quiet);
                if (Message(line) is string note) heard(note);
            }
        }

        private static string? Message(string line)
        {
            if (line.Length == 0) return null;
            try
            {
                using JsonDocument json = JsonDocument.Parse(line);
                JsonElement root = json.RootElement;
                return root.TryGetProperty("event", out JsonElement kind) && kind.GetString() == "message" && root.TryGetProperty("message", out JsonElement message) ? message.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// The host's half of meeting: keeps the internet's view of its port in the mailbox, and sends to anyone who leaves word that they're coming, which opens the
    /// host's router and provider to them. Asking again every so often also keeps the router from forgetting the port, as an idle one is forgotten within minutes.
    /// </summary>
    public sealed class MeetingHost : IDisposable
    {
        private const int AskEveryMilliseconds = 20000;
        private const int SendRounds = 40;              // Every 250 ms for ten seconds, as long as a friend's game goes on knocking.
        private const int SendEveryMilliseconds = 250;
        private static readonly byte[] Opening = Encoding.ASCII.GetBytes("dino-defenders-opening-the-way");

        private readonly LiteNetManager _manager;
        private readonly StunLayer _stun;
        private readonly uint _secret;
        private readonly IMeetingPlace _place;
        private readonly IReadOnlyList<(string Host, int Port)> _servers;
        private readonly string _topic;
        private readonly string _id = Note.NewId();
        private readonly CancellationTokenSource _stop = new();
        private IPEndPoint? _seen;

        /// <summary>What the hosting panel says, which is this rather than the router's word once there is one, as the router no longer decides who can join.</summary>
        public volatile string Status = "Finding the way in from the internet...";

        public MeetingHost(LiteNetManager manager, StunLayer stun, uint secret, IMeetingPlace place, IReadOnlyList<(string Host, int Port)> servers)
        {
            _manager = manager;
            _stun = stun;
            _secret = secret;
            _place = place;
            _servers = servers;
            _topic = Meeting.Topic(secret);
            _ = Task.Run(Keep);
            _ = Task.Run(Hear);
        }

        public void Dispose() => _stop.Cancel();

        private async Task Keep()
        {
            bool toldNobodyAnswered = false;
            bool told = false;
            while (!_stop.IsCancellationRequested)
            {
                (IPEndPoint? seen, string? by) = await _stun.Ask(_manager, _servers, _stop.Token).ConfigureAwait(false);
                if (seen == null && !toldNobodyAnswered && !_stop.IsCancellationRequested)
                {
                    GameLog.Network("Hosting: no STUN server answered, so a friend's game can't be told where on the internet this one is.");
                    toldNobodyAnswered = true;
                }
                if (seen != null && !seen.Equals(_seen))
                {
                    GameLog.Network($"Hosting: the internet sees this game's port as {seen} (asked {by}).");
                    _seen = seen;
                    told = false;
                }
                if (!told) told = await Tell().ConfigureAwait(false);
                if (!_stop.IsCancellationRequested)
                {
                    Status = _seen == null ? "This game can't reach the internet's meeting place, so only friends on your own network can join."
                        : !told ? $"The mailbox friends meet through ({_place.Name}) can't be reached, so only friends on your own network can join."
                        : "Ready. Friends can join with this invite wherever they are.";
                }

                try
                {
                    await Task.Delay(AskEveryMilliseconds, _stop.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        // Where this game is, for a friend's game to read when it sets off.
        private async Task<bool> Tell()
        {
            var at = new List<IPEndPoint>();
            if (_seen != null) at.Add(_seen);
            at.AddRange(InviteCode.LocalAddresses().Select(address => new IPEndPoint(address, _manager.LocalPort)));
            try
            {
                await _place.Post(_topic, Meeting.Seal(_secret, new Note(Note.Host, _id, Protocol.Version, at)), _stop.Token).ConfigureAwait(false);
                GameLog.Network($"Hosting: left word in the mailbox ({_place.Name}) that this game is at {string.Join(", ", at)}.");
                return true;
            }
            catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException)
            {
                if (!_stop.IsCancellationRequested) GameLog.Network($"Hosting: couldn't reach the mailbox ({_place.Name}): {e.Message}");
                return false;
            }
        }

        private async Task Hear()
        {
            long since = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 5;
            bool troubled = false;
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    await _place.Listen(_topic, since, Heard, _stop.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException)
                {
                    if (!troubled) GameLog.Network($"Hosting: lost the mailbox ({_place.Name}), and keeps trying: {e.Message}");
                    troubled = true;
                }

                // Notes left while the line was down are heard again; anyone heard twice is only sent to twice.
                since = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 30;
                try
                {
                    await Task.Delay(3000, _stop.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void Heard(string text)
        {
            Note? note = Meeting.Open(_secret, text);
            if (note == null || note.Role != Note.Join || note.At.Count == 0) return;

            GameLog.Network($"Hosting: a friend's game ({note.Id}) is on its way from {string.Join(", ", note.At)}; sending to it, so the way in opens for it.");
            _ = Task.Run(() => OpenTheWay(note.At));
        }

        private async Task OpenTheWay(IReadOnlyList<IPEndPoint> to)
        {
            for (int round = 0; round < SendRounds && !_stop.IsCancellationRequested; round++)
            {
                foreach (IPEndPoint address in to) _manager.SendUnconnectedMessage(Opening, address);
                try
                {
                    await Task.Delay(SendEveryMilliseconds, _stop.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
