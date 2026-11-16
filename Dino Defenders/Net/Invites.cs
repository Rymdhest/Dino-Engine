using Dino_Engine.Networking;
using Mono.Nat;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Dino_Defenders.Net
{
    /// <summary>
    /// Everything a friend needs to reach a game, as text to paste into a chat: where it is, and the secret that lets them in. With no
    /// server of ours there's nothing to look a game up in, so the code carries the address itself. It holds both the host's
    /// address on the internet and on their own network, as a friend under the same roof usually can't use the former.
    /// </summary>
    public sealed class InviteCode
    {
        private const string Prefix = "DINO";
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";    // Crockford's: nothing that reads as something else.
        private const int Bytes = 16;

        public IPAddress? Public { get; init; }
        public IPAddress? Local { get; init; }
        public int Port { get; init; }
        public uint Secret { get; init; }

        /// <summary>What the transport checks at the door.</summary>
        public string Key => KeyFor(Secret);

        public static string KeyFor(uint secret) => $"dino-{Protocol.Version}-{secret:x8}";

        public static uint NewSecret() => BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4));

        public string Text
        {
            get
            {
                byte[] data = new byte[Bytes];
                data[0] = (byte)(Port >> 8);
                data[1] = (byte)Port;
                BitConverter.TryWriteBytes(data.AsSpan(2), Secret);
                if (Public != null) Public.GetAddressBytes().CopyTo(data, 6);
                if (Local != null) Local.GetAddressBytes().CopyTo(data, 10);
                data[14] = (byte)((Public != null ? 1 : 0) | (Local != null ? 2 : 0));
                data[15] = Check(data);

                var text = new StringBuilder(Prefix);
                string encoded = Encode(data);
                for (int i = 0; i < encoded.Length; i += 4)
                {
                    text.Append('-').Append(encoded, i, Math.Min(4, encoded.Length - i));
                }
                return text.ToString();
            }
        }

        public static bool TryParse(string text, out InviteCode? code)
        {
            code = null;
            string cleaned = text.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");
            if (!cleaned.StartsWith(Prefix)) return false;

            byte[]? data = Decode(cleaned.Substring(Prefix.Length));
            if (data == null || data.Length != Bytes || data[15] != Check(data)) return false;

            byte flags = data[14];
            code = new InviteCode
            {
                Port = (data[0] << 8) | data[1],
                Secret = BitConverter.ToUInt32(data, 2),
                Public = (flags & 1) != 0 ? new IPAddress(data.AsSpan(6, 4)) : null,
                Local = (flags & 2) != 0 ? new IPAddress(data.AsSpan(10, 4)) : null,
            };
            return true;
        }

        /// <summary>What the code holds, for the log, as what was really sent is the first thing to know when someone can't get in. Never the secret.</summary>
        public string Describe() => $"internet address {Public?.ToString() ?? "none"}, home address {Local?.ToString() ?? "none"}, port {Port}";

        /// <summary>Which of the host's addresses this is, for the log.</summary>
        public string Label(IPEndPoint address) =>
            address.Address.Equals(Public) ? "the host's internet address" : address.Address.Equals(Local) ? "the host's home address" : "an address";

        /// <summary>
        /// Why a key at the door isn't this game's, in words for whoever's turned away, or null if it is. A key carries the protocol, so a friend on another version is
        /// told so here, before they ever say hello.
        /// </summary>
        public static string? WhyNot(string? theirs, string ours)
        {
            if (theirs == ours) return null;
            string[] their = (theirs ?? "").Split('-');
            string[] our = ours.Split('-');
            if (their.Length != 3 || their[0] != "dino") return "That isn't Dino Defenders knocking.";
            if (their[1] != our[1]) return $"You and the host are running different versions of the game (protocol {their[1]} and {our[1]}). Close it and open the launcher to update, both of you.";
            return "That invite is from another game, or from before the host opened theirs again. Ask for a new one.";
        }

        /// <summary>The addresses worth trying from this machine, best first.</summary>
        public List<IPEndPoint> Addresses()
        {
            var addresses = new List<IPEndPoint>();
            if (Local != null && OnSameNetwork(Local)) addresses.Add(new IPEndPoint(Local, Port));
            if (Public != null) addresses.Add(new IPEndPoint(Public, Port));
            if (Local != null && addresses.Count == 0) addresses.Add(new IPEndPoint(Local, Port));
            return addresses;
        }

        /// <summary>Whether an address is one inside someone's home or provider, which reaches nothing from anywhere else: home networks, a provider's shared range, VPNs.</summary>
        internal static bool IsPrivate(IPAddress address)
        {
            byte[] b = address.GetAddressBytes();
            if (b.Length != 4) return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal;
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] < 32) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254) || (b[0] == 100 && b[1] >= 64 && b[1] < 128);
        }

        internal static bool OnSameNetwork(IPAddress address)
        {
            byte[] theirs = address.GetAddressBytes();
            foreach (IPAddress ours in LocalAddresses())
            {
                byte[] mine = ours.GetAddressBytes();
                if (mine[0] == theirs[0] && mine[1] == theirs[1] && mine[2] == theirs[2]) return true;
            }
            return false;
        }

        /// <summary>This machine's own addresses on the networks it is plugged into.</summary>
        public static IEnumerable<IPAddress> LocalAddresses()
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (UnicastIPAddressInformation address in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork) yield return address.Address;
                }
            }
        }

        // Catches a code cut short or mistyped, before it turns into a long wait for nobody to answer.
        private static byte Check(byte[] data)
        {
            byte check = 0x5A;
            for (int i = 0; i < Bytes - 1; i++) check = (byte)((check * 31) ^ data[i]);
            return check;
        }

        private static string Encode(byte[] data)
        {
            var text = new StringBuilder();
            int buffer = 0;
            int bits = 0;
            foreach (byte b in data)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    bits -= 5;
                    text.Append(Alphabet[(buffer >> bits) & 31]);
                }
                buffer &= (1 << bits) - 1;
            }
            if (bits > 0) text.Append(Alphabet[(buffer << (5 - bits)) & 31]);
            return text.ToString();
        }

        private static byte[]? Decode(string text)
        {
            var data = new List<byte>();
            int buffer = 0;
            int bits = 0;
            foreach (char raw in text)
            {
                char c = raw == 'O' ? '0' : raw is 'I' or 'L' ? '1' : raw;
                int value = Alphabet.IndexOf(c);
                if (value < 0) return null;
                buffer = (buffer << 5) | value;
                bits += 5;
                if (bits >= 8)
                {
                    bits -= 8;
                    data.Add((byte)((buffer >> bits) & 255));
                }
                buffer &= (1 << bits) - 1;
            }
            return data.ToArray();
        }
    }

    /// <summary>What two games have to agree on to play together. Both ends log it, so two machines' logs side by side say at once whether they could.</summary>
    public static class NetVersions
    {
        public static string Text => $"{BuildInfo.Title}, protocol {Protocol.Version}, build {Protocol.Build:x8}, schema {NetSchema.Hash:x8}";
    }

    /// <summary>
    /// Asks Windows to let friends in. Without this a friend's packets stop at the host's own firewall, in silence if its notifications
    /// are off. A rule takes an administrator, so this is the one Yes a host has to give, once for each place the game is installed.
    /// A friend who joins needs none: their traffic is answers to what they sent.
    /// </summary>
    public static class WindowsFirewall
    {
        private const int Declined = 1223;      // ERROR_CANCELLED, for No on the box Windows shows.

        /// <summary>Must come before the port opens, or Windows puts up a box of its own as well. Returns what went wrong, if anything did.</summary>
        public static string? LetFriendsIn()
        {
            string? program = Environment.ProcessPath;
            if (!OperatingSystem.IsWindows() || program == null) return null;

            // Named after the path, so that whether it's there is an exit code, whatever language Windows speaks.
            string rule = $"Dino Defenders {NetSchema.Fnv(program.ToLowerInvariant()):X8}";
            try
            {
                if (Netsh($"advfirewall firewall show rule name=\"{rule}\"", elevated: false) == 0)
                {
                    GameLog.Network($"Hosting: Windows firewall already lets this copy of the game in ({rule}).");
                    return null;
                }

                int result = Netsh($"advfirewall firewall add rule name=\"{rule}\" dir=in action=allow program=\"{program}\" protocol=udp profile=any enable=yes", elevated: true);
                if (result == 0)
                {
                    GameLog.Network($"Hosting: Windows firewall rule added for this copy of the game ({rule}).");
                    return null;
                }
                GameLog.Network($"Hosting: adding the Windows firewall rule failed, netsh exit code {result}. This PC may drop friends' packets.");
            }
            catch (Win32Exception exception) when (exception.NativeErrorCode == Declined)
            {
                GameLog.Network("Hosting: the player said no to the Windows firewall rule, so this PC will drop friends' packets.");
                return "Windows was told no, so it will keep friends out. Leave and open this world again to be asked again.";
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
            {
                GameLog.Network($"Hosting: adding the Windows firewall rule failed: {exception.Message}. This PC may drop friends' packets.");
            }
            return "Windows wouldn't let friends in, so they may not be able to reach you. Having a friend host is the easy way around it.";
        }

        private static int Netsh(string arguments, bool elevated)
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "netsh.exe"), arguments)
            {
                UseShellExecute = elevated,
                Verb = elevated ? "runas" : "",
                CreateNoWindow = !elevated,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("netsh didn't start.");
            process.WaitForExit();
            return process.ExitCode;
        }
    }

    /// <summary>
    /// Asks the router to let friends in, by UPnP or NAT-PMP, so that hosting needs no fiddling with its settings. Routers don't all
    /// allow it, and behind a provider's shared address nothing can: what is known is in <see cref="Status"/>, to tell the player the truth.
    /// It only ever runs when a player chooses to open their game, and the opening is closed again with it.
    /// </summary>
    public sealed class PortMapper : IDisposable
    {
        private const int LeaseSeconds = 3600;
        private const int AnswerWithinMilliseconds = 10000;     // A router that does this answers within a second or two.

        private readonly int _port;
        private INatDevice? _device;
        private Mapping? _mapping;
        private volatile bool _closed;
        private volatile bool _settled;

        public volatile string Status = "Looking for your router...";

        /// <summary>The address friends outside may reach this game at, as the router tells it. Never one nobody outside could, as a friend would only wait on it before giving up.</summary>
        public volatile IPAddress? External;

        public PortMapper(int port)
        {
            _port = port;
            GameLog.Network($"Hosting: asking the router to pass on UDP port {port}, by UPnP or NAT-PMP.");
            NatUtility.DeviceFound += OnDeviceFound;
            NatUtility.StartDiscovery();

            // A router with UPnP and NAT-PMP turned off never answers at all, so silence is taken as no after a while, or the panel says it's looking for ever.
            // Discovery goes on, so a router that answers late still opens the way.
            Task.Delay(AnswerWithinMilliseconds).ContinueWith(_ =>
            {
                if (_settled || _closed) return;
                GameLog.Network($"Hosting: no router had opened the port {AnswerWithinMilliseconds / 1000} s on{(_device == null ? ": none answered by UPnP or NAT-PMP" : $", though one answered at {_device.DeviceEndpoint}")}. Invites hold only this machine's home address.");
                Status = "Your router hasn't answered, so only friends on your own network can join. Turning on UPnP in its settings lets the rest in, or one of them can host instead.";
            });
        }

        private async void OnDeviceFound(object? sender, DeviceEventArgs e)
        {
            if (_device != null || _closed) return;
            _device = e.Device;
            GameLog.Network($"Hosting: the router answered by {e.Device.NatProtocol} at {e.Device.DeviceEndpoint}.");

            try
            {
                var mapping = new Mapping(Mono.Nat.Protocol.Udp, _port, _port, LeaseSeconds, "Dino Defenders");
                _mapping = await e.Device.CreatePortMapAsync(mapping);
                IPAddress external = await e.Device.GetExternalIPAsync();
                if (_closed)
                {
                    await e.Device.DeletePortMapAsync(_mapping);
                    return;
                }

                string reach = SharedByProvider(external) ? "shared by the provider (carrier-grade NAT), so friends with other providers can't reach this game"
                    : IsPrivate(external) ? "another router's, so friends outside this home can't reach this game"
                    : "public, so friends anywhere should reach this game";
                GameLog.Network($"Hosting: the router opened UDP port {_port}. Its internet address is {external}: {reach}.");
                // A provider's shared address can still reach its other customers, as some providers route between them, so it goes in the code. Another router's private one reaches nobody outside.
                External = IsPrivate(external) && !SharedByProvider(external) ? null : external;
                Status = SharedByProvider(external)
                    ? "Your provider shares one internet address among many customers, so friends with other providers may not be able to reach you. One of them can host instead, or your provider may give you an address of your own if you ask."
                    : IsPrivate(external)
                    ? "There's another router between yours and the internet, so friends outside your home can't reach you. One of them can host instead."
                    : "Your router has opened the way. Friends can join from anywhere.";
            }
            catch (Exception exception)
            {
                GameLog.Network($"Hosting: the router wouldn't open UDP port {_port}: {exception.GetType().Name}: {exception.Message}");
                Status = "Your router wouldn't open a port, so only friends on your own network can join. One of the others can host instead.";
            }
            _settled = true;
        }

        // The ranges no public address falls in, including the one set aside for providers doing this (100.64/10).
        private static bool IsPrivate(IPAddress address)
        {
            byte[] b = address.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] < 32) || (b[0] == 192 && b[1] == 168) || SharedByProvider(address);
        }

        // Carrier-grade NAT: the provider puts many customers behind one address of its own.
        private static bool SharedByProvider(IPAddress address)
        {
            byte[] b = address.GetAddressBytes();
            return b[0] == 100 && b[1] >= 64 && b[1] < 128;
        }

        public void Dispose()
        {
            _closed = true;
            NatUtility.DeviceFound -= OnDeviceFound;
            NatUtility.StopDiscovery();

            if (_device == null || _mapping == null) return;
            try
            {
                _device.DeletePortMapAsync(_mapping).Wait(1000);
                GameLog.Network($"Hosting: closed to friends, and the router's port {_port} with it.");
            }
            catch (Exception exception) when (exception is AggregateException or MappingException)
            {
                // The lease runs out by itself within the hour.
                GameLog.Network($"Hosting: closed to friends, but the router wouldn't close port {_port}: {exception.Message}");
            }
        }
    }
}
