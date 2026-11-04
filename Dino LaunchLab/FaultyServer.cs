using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Dino_LaunchLab
{
    internal enum FaultKind
    {
        Status,         // Answer with a status code.
        DropAfter,      // Send this many bytes of the body, then cut the connection.
        Corrupt,        // Send the right number of bytes, with some of them wrong.
        IgnoreRange,    // Send the whole file whatever was asked for.
        Stall,          // Send some of the body, then go silent.
    }

    internal readonly struct Fault
    {
        public readonly FaultKind Kind;
        public readonly int Value;

        public Fault(FaultKind kind, int value = 0)
        {
            Kind = kind;
            Value = value;
        }
    }

    /// <summary>What was asked for.</summary>
    internal sealed class Request
    {
        public string Method = "";
        public string Path = "";
        public string Query = "";
        public Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public byte[] Body = new byte[0];
        public long BytesSent;

        public string Header(string name) => Headers.TryGetValue(name, out string? value) ? value : "";
    }

    /// <summary>What to say back, for a handler doing more than serving files.</summary>
    internal sealed class Response
    {
        public int Status = 200;
        public byte[] Body = new byte[0];
        public string? Location;

        public static Response Text(int status, string text) => new Response { Status = status, Body = Encoding.UTF8.GetBytes(text) };
        public static Response Redirect(string location) => new Response { Status = 302, Location = location };
    }

    /// <summary>
    /// A small web server that can be told to misbehave at any point of a transfer. Written on a raw socket, as the failures worth
    /// testing are the ones a proper server won't commit: closing mid-body, ignoring a range, going quiet.
    /// </summary>
    internal sealed class FaultyServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly TcpListener _cdn;
        private readonly Dictionary<string, Queue<Fault>> _faults = new Dictionary<string, Queue<Fault>>();
        private readonly List<Request> _requests = new List<Request>();
        private volatile bool _stopped;

        /// <summary>The files on offer, by path without the leading slash.</summary>
        public Func<string, byte[]?> Content = _ => null;

        /// <summary>Answers instead of <see cref="Content"/>, for anything it returns something for.</summary>
        public Func<Request, Response?> Handler = _ => null;

        /// <summary>Sends every file by way of a redirect to another host name, as GitHub sends them to its CDN.</summary>
        public bool RedirectToCdn;

        /// <summary>How fast bodies are sent, in KB a second, to watch a download happen. Zero is as fast as possible.</summary>
        public int ThrottleKilobytes;

        public FaultyServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _cdn = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _cdn.Start();
            new Thread(() => Accept(_listener)) { IsBackground = true }.Start();
            new Thread(() => Accept(_cdn)) { IsBackground = true }.Start();
        }

        public Uri Url => new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");

        /// <summary>The same server under another address, so that a redirect there really goes somewhere else.</summary>
        public Uri CdnUrl => new Uri($"http://127.0.0.1:{((IPEndPoint)_cdn.LocalEndpoint).Port}/");

        public List<Request> Requests
        {
            get
            {
                lock (_requests) return _requests.ToList();
            }
        }

        public void ClearRequests()
        {
            lock (_requests) _requests.Clear();
        }

        /// <summary>Requests for a path ending this way, not counting the redirects on the way to it.</summary>
        public int CountOf(string suffix) => Requests.Count(r => r.Path.EndsWith(suffix) && r.Path.StartsWith("/cdn/") == RedirectToCdn);

        public void Inject(string suffix, params Fault[] faults)
        {
            lock (_faults) _faults[suffix] = new Queue<Fault>(faults);
        }

        public void ClearFaults()
        {
            lock (_faults) _faults.Clear();
        }

        private Fault? NextFault(string path)
        {
            lock (_faults)
            {
                foreach (KeyValuePair<string, Queue<Fault>> fault in _faults)
                {
                    if (path.EndsWith(fault.Key) && fault.Value.Count > 0) return fault.Value.Dequeue();
                }
            }
            return null;
        }

        private void Accept(TcpListener listener)
        {
            while (!_stopped)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    var serving = new Thread(() => Serve(client)) { IsBackground = true };
                    serving.Start();
                }
                catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
                {
                    return;
                }
            }
        }

        private void Serve(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.NoDelay = true;
                    NetworkStream stream = client.GetStream();
                    Request? request = ReadRequest(stream);
                    if (request != null) Answer(stream, client, request);
                }
                catch (Exception e) when (e is IOException || e is SocketException || e is ObjectDisposedException)
                {
                    // The client went, which is its right.
                }
            }
        }

        private Request? ReadRequest(NetworkStream stream)
        {
            var head = new MemoryStream();
            while (true)
            {
                int b = stream.ReadByte();
                if (b < 0) return null;
                head.WriteByte((byte)b);
                long n = head.Length;
                byte[] seen = head.GetBuffer();
                if (n >= 4 && seen[n - 4] == '\r' && seen[n - 3] == '\n' && seen[n - 2] == '\r' && seen[n - 1] == '\n') break;
            }

            string[] lines = Encoding.ASCII.GetString(head.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            string[] first = lines[0].Split(' ');
            string[] target = first[1].Split(new[] { '?' }, 2);
            var request = new Request { Method = first[0], Path = Uri.UnescapeDataString(target[0]), Query = target.Length > 1 ? target[1] : "" };
            foreach (string line in lines.Skip(1))
            {
                int colon = line.IndexOf(':');
                if (colon > 0) request.Headers[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
            }

            if (request.Header("Expect").Length > 0)
            {
                byte[] proceed = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                stream.Write(proceed, 0, proceed.Length);
            }

            if (long.TryParse(request.Header("Content-Length"), out long length) && length > 0)
            {
                var body = new byte[length];
                int got = 0;
                while (got < length)
                {
                    int read = stream.Read(body, got, (int)Math.Min(length - got, 1 << 20));
                    if (read <= 0) return null;
                    got += read;
                }
                request.Body = body;
            }
            else if (request.Header("Transfer-Encoding").Equals("chunked", StringComparison.OrdinalIgnoreCase))
            {
                request.Body = ReadChunked(stream);
            }

            lock (_requests) _requests.Add(request);
            return request;
        }

        private static byte[] ReadChunked(NetworkStream stream)
        {
            var body = new MemoryStream();
            while (true)
            {
                var line = new StringBuilder();
                int b;
                while ((b = stream.ReadByte()) >= 0 && b != '\n')
                {
                    if (b != '\r') line.Append((char)b);
                }

                int size = Convert.ToInt32(line.ToString().Split(';')[0], 16);
                var chunk = new byte[size + 2];
                int got = 0;
                while (got < chunk.Length)
                {
                    int read = stream.Read(chunk, got, chunk.Length - got);
                    if (read <= 0) return body.ToArray();
                    got += read;
                }
                if (size == 0) return body.ToArray();
                body.Write(chunk, 0, size);
            }
        }

        private void Answer(NetworkStream stream, TcpClient client, Request request)
        {
            Response? handled = Handler(request);
            if (handled != null)
            {
                Respond(stream, handled.Status, handled.Body, handled.Location, request);
                return;
            }

            string path = request.Path.TrimStart('/');
            if (RedirectToCdn && !path.StartsWith("cdn/"))
            {
                Respond(stream, 302, new byte[0], $"{CdnUrl}cdn/{path}", request);
                return;
            }
            if (path.StartsWith("cdn/")) path = path.Substring(4);

            Fault? fault = NextFault(path);
            if (fault?.Kind == FaultKind.Status)
            {
                Respond(stream, fault.Value.Value, new byte[0], null, request);
                return;
            }

            byte[]? file = Content(path);
            if (file == null)
            {
                Respond(stream, 404, new byte[0], null, request);
                return;
            }

            long from = 0;
            string range = request.Header("Range");
            if (range.StartsWith("bytes=") && fault?.Kind != FaultKind.IgnoreRange)
            {
                from = long.Parse(range.Substring(6).Split('-')[0]);
                if (from >= file.Length)
                {
                    Respond(stream, 416, new byte[0], null, request);
                    return;
                }
            }

            var body = new byte[file.Length - from];
            Buffer.BlockCopy(file, (int)from, body, 0, body.Length);
            if (fault?.Kind == FaultKind.Corrupt)
            {
                for (int i = 0; i < body.Length; i += 997) body[i] ^= 0xff;
            }

            string headers = from > 0 ? $"HTTP/1.1 206 Partial Content\r\nContent-Range: bytes {from}-{file.Length - 1}/{file.Length}\r\n" : "HTTP/1.1 200 OK\r\n";
            headers += $"Content-Length: {body.Length}\r\nAccept-Ranges: bytes\r\nConnection: close\r\n\r\n";
            byte[] head = Encoding.ASCII.GetBytes(headers);
            stream.Write(head, 0, head.Length);
            if (request.Method == "HEAD") return;

            int sending = body.Length;
            if (fault?.Kind == FaultKind.DropAfter || fault?.Kind == FaultKind.Stall) sending = Math.Min(fault.Value.Value, body.Length);

            if (ThrottleKilobytes > 0)
            {
                int chunk = ThrottleKilobytes * 1024 / 20;
                for (int sent = 0; sent < sending; sent += chunk)
                {
                    stream.Write(body, sent, Math.Min(chunk, sending - sent));
                    Thread.Sleep(50);
                }
            }
            else
            {
                stream.Write(body, 0, sending);
            }
            request.BytesSent = sending;

            if (fault?.Kind == FaultKind.Stall) Thread.Sleep(15000);
            if (fault?.Kind == FaultKind.DropAfter)
            {
                // A reset rather than a tidy close: nothing tells the other end how much was meant to come.
                client.LingerState = new LingerOption(true, 0);
            }
        }

        private static void Respond(NetworkStream stream, int status, byte[] body, string? location, Request request)
        {
            string headers = $"HTTP/1.1 {status} Status\r\nContent-Length: {body.Length}\r\nConnection: close\r\n";
            if (location != null) headers += $"Location: {location}\r\n";
            byte[] head = Encoding.ASCII.GetBytes(headers + "\r\n");
            stream.Write(head, 0, head.Length);
            if (request.Method != "HEAD") stream.Write(body, 0, body.Length);
        }

        public void Dispose()
        {
            _stopped = true;
            _listener.Stop();
            _cdn.Stop();
        }
    }
}
