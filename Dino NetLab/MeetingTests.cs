using Dino_Defenders.Net;
using Dino_Engine.Networking;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Dino_NetLab
{
    /// <summary>
    /// Meeting (NETWORKING.md): two games behind routers finding each other through a mailbox and a STUN server. Here both are on this machine, over real UDP, with
    /// a STUN server and a mailbox of the lab's own, so nothing leaves it; `meet` does the same through the real ones on the internet. A router can't be put in
    /// between on one machine, so what opening one takes is only seen to happen: the host hears its friend is coming and sends to it.
    /// </summary>
    internal static class MeetingTests
    {
        public static void Run(Report report)
        {
            NotesAreSealed(report);
            using var stun = new LabStun();
            var place = new LabPlace();
            FriendsMeet(report, place, new[] { ("127.0.0.1", stun.Port) }, stun);
            NowhereToKnock(report);
        }

        /// <summary>The same, through Google's STUN server and ntfy.sh. Needs the internet.</summary>
        public static int Live(Options options)
        {
            var report = new Report();
            report.Section("Meeting, on this machine alone");
            Run(report);
            report.Section("Meeting through the real mailbox and STUN servers");
            FriendsMeet(report, Ntfy.Shared, Meeting.StunServers, stun: null);
            return report.Finish();
        }

        private static void NotesAreSealed(Report report)
        {
            var note = new Note(Note.Join, Note.NewId(), Protocol.Version, new[] { IPEndPoint.Parse("203.0.113.7:61360"), IPEndPoint.Parse("192.168.1.20:50000") });
            string text = Meeting.Seal(0xCAFEF00D, note);
            Note? back = Meeting.Open(0xCAFEF00D, text);
            report.Check("A note opens with the invite's secret, exactly as it was left", back != null && back.Text == note.Text, back?.Text ?? "nothing");
            report.Check("And not with any other", Meeting.Open(0xCAFEF00E, text) == null);
            report.Check("Nor does anything else left there", Meeting.Open(0xCAFEF00D, "hello there") == null && Meeting.Open(0xCAFEF00D, Convert.ToBase64String(new byte[40])) == null);
            report.Check("The mailbox shows nothing of where anyone is", !text.Contains("203.0.113.7") && !Meeting.Topic(0xCAFEF00D).Contains("cafef00d"));
            report.Check("Each invite meets under a topic of its own", Meeting.Topic(1) != Meeting.Topic(2) && Meeting.Topic(1) == Meeting.Topic(1));
        }

        // A friend with the secret alone: no address in the invite at all, so what's in the mailbox is the only way they could find the host.
        private static void FriendsMeet(Report report, IMeetingPlace place, IReadOnlyList<(string Host, int Port)> stunServers, LabStun? stun)
        {
            uint secret = InviteCode.NewSecret();
            TextWriter console = Console.Out;
            var log = new StringWriter();
            Console.SetOut(TextWriter.Synchronized(log));
            var hostEnd = new Listener();
            var friendEnd = new Listener();
            bool hostTold = false;
            try
            {
                using UdpTransport host = UdpTransport.Host(0, InviteCode.KeyFor(secret));
                host.Meet(secret, place, stunServers);
                for (int i = 0; i < 400 && !(hostTold = log.ToString().Contains("Hosting: left word in the mailbox")); i++)
                {
                    host.Poll(hostEnd);
                    Thread.Sleep(10);
                }

                var code = new InviteCode { Port = host.Port, Secret = secret };
                using UdpTransport friend = UdpTransport.Join(code, place, stunServers);
                for (int i = 0; i < 2000 && !(friendEnd.Connected && hostEnd.Connected) && friendEnd.Reason == null; i++)
                {
                    host.Poll(hostEnd);
                    friend.Poll(friendEnd);
                    Thread.Sleep(5);
                }

                friendEnd.Connection?.Send(new byte[] { 1, 2, 3 }, Delivery.Reliable);
                for (int i = 0; i < 200 && hostEnd.Received == 0; i++)
                {
                    host.Poll(hostEnd);
                    friend.Poll(friendEnd);
                    Thread.Sleep(5);
                }
                report.Check("And what's sent then gets across, through the layer that takes STUN's answers", hostEnd.Received > 0);
            }
            finally
            {
                Console.SetOut(console);
            }

            string said = log.ToString();
            report.Check("The host learns how its own port is seen, through the game's own socket", said.Contains($"Hosting: the internet sees this game's port as"), Lines(said, "Hosting:"));
            if (stun != null) report.Check("Which is the port friends come in at", said.Contains($"port as 127.0.0.1:"), Lines(said, "sees this game"));
            report.Check("And leaves word of it in the mailbox", hostTold, Lines(said, "Hosting:"));
            report.Check("A friend holding nothing but the invite's secret finds the host through the mailbox, and gets in", friendEnd.Connected, friendEnd.Reason ?? Lines(said, "Joining:"));
            report.Check("Having left word of where it is", said.Contains("Joining: left word in the mailbox"), Lines(said, "Joining:"));
            report.Check("Which the host hears, and sends to it, as opening a router takes", said.Contains("is on its way from"), Lines(said, "Hosting:"));
            foreach (string line in said.Split('\n').Where(line => line.Contains("Hosting:") || line.Contains("Joining:")).Select(line => line.Trim()))
            {
                report.Note(line.Length > 160 ? line[..160] + "..." : line);
            }
        }

        private static void NowhereToKnock(Report report)
        {
            var end = new Listener();
            TextWriter console = Console.Out;
            Console.SetOut(TextWriter.Synchronized(new StringWriter()));
            try
            {
                using UdpTransport friend = UdpTransport.Join(new InviteCode { Port = 1, Secret = 7 }, new LabPlace(), Array.Empty<(string, int)>());
                for (int i = 0; i < 200 && end.Reason == null; i++)
                {
                    friend.Poll(end);
                    Thread.Sleep(5);
                }
            }
            finally
            {
                Console.SetOut(console);
            }
            report.Check("An invite with no address, and no word from its host, fails at once and says why", end.Reason?.Contains("nowhere to try") == true, end.Reason ?? "still waiting");
        }

        private static string Lines(string log, string containing) => string.Join(" | ", log.Split('\n').Where(line => line.Contains(containing)).Select(line => line.Trim()));

        private sealed class Listener : INetListener
        {
            public bool Connected;
            public INetConnection? Connection;
            public string? Reason;
            public int Received;

            public void OnConnected(INetConnection connection)
            {
                Connected = true;
                Connection = connection;
            }

            public void OnMessage(INetConnection connection, byte[] data, Delivery delivery) => Received++;
            public void OnDisconnected(INetConnection connection, string reason) => Reason ??= reason;
        }
    }

    /// <summary>A mailbox in memory, which hears every note at once.</summary>
    internal sealed class LabPlace : IMeetingPlace
    {
        private readonly List<(string Topic, string Text)> _notes = new();
        private readonly List<(string Topic, Action<string> Heard)> _listening = new();

        public string Name => "the lab's mailbox";

        public Task Post(string topic, string text, CancellationToken cancel)
        {
            List<Action<string>> hearing;
            lock (_notes)
            {
                _notes.Add((topic, text));
                hearing = _listening.Where(listener => listener.Topic == topic).Select(listener => listener.Heard).ToList();
            }
            foreach (Action<string> heard in hearing) heard(text);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> Read(string topic, CancellationToken cancel)
        {
            lock (_notes) return Task.FromResult<IReadOnlyList<string>>(_notes.Where(note => note.Topic == topic).Select(note => note.Text).ToList());
        }

        public async Task Listen(string topic, long sinceUnixSeconds, Action<string> heard, CancellationToken cancel)
        {
            (string, Action<string>) listener = (topic, heard);
            lock (_notes) _listening.Add(listener);
            try
            {
                await Task.Delay(Timeout.Infinite, cancel);
            }
            finally
            {
                lock (_notes) _listening.Remove(listener);
            }
        }
    }

    /// <summary>A STUN server on this machine, which says what any server says: the address and port a question came from.</summary>
    internal sealed class LabStun : IDisposable
    {
        private const uint Cookie = 0x2112A442;
        private readonly UdpClient _udp = new(new IPEndPoint(IPAddress.Loopback, 0));

        public int Port => ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;

        public LabStun()
        {
            _ = Task.Run(Serve);
        }

        public void Dispose() => _udp.Dispose();

        private async Task Serve()
        {
            while (true)
            {
                UdpReceiveResult question;
                try
                {
                    question = await _udp.ReceiveAsync();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    continue;   // Windows reports an earlier answer that had nowhere to go as an error on the next receive.
                }

                byte[] asked = question.Buffer;
                if (asked.Length < 20 || asked[0] != 0 || asked[1] != 1) continue;
                byte[] answer = new byte[32];
                BinaryPrimitives.WriteUInt16BigEndian(answer, 0x0101);
                BinaryPrimitives.WriteUInt16BigEndian(answer.AsSpan(2), 12);
                BinaryPrimitives.WriteUInt32BigEndian(answer.AsSpan(4), Cookie);
                asked.AsSpan(8, 12).CopyTo(answer.AsSpan(8));
                BinaryPrimitives.WriteUInt16BigEndian(answer.AsSpan(20), 0x0020);
                BinaryPrimitives.WriteUInt16BigEndian(answer.AsSpan(22), 8);
                answer[25] = 0x01;
                BinaryPrimitives.WriteUInt16BigEndian(answer.AsSpan(26), (ushort)(question.RemoteEndPoint.Port ^ (int)(Cookie >> 16)));
                byte[] address = question.RemoteEndPoint.Address.MapToIPv4().GetAddressBytes();
                for (int i = 0; i < 4; i++) answer[28 + i] = (byte)(address[i] ^ (byte)(Cookie >> (24 - 8 * i)));
                try
                {
                    await _udp.SendAsync(answer, question.RemoteEndPoint);
                }
                catch (SocketException)
                {
                }
            }
        }
    }
}
