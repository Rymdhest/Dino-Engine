using System;
using System.IO;
using System.Net;
using System.Threading;

namespace Dino_Launcher.Update
{
    /// <summary>Thrown when something can't be had right now. What was there before is untouched.</summary>
    public sealed class DownloadException : Exception
    {
        public readonly bool Permanent;
        public readonly int Status;

        public DownloadException(string message, bool permanent = false, int status = 0) : base(message)
        {
            Permanent = permanent;
            Status = status;
        }
    }

    /// <summary>
    /// Gets things over a line that can't be trusted to stay up. A broken download carries on from where it stopped, even in a later run,
    /// and nothing arrives under its real name until its hash has been checked. HttpWebRequest rather than HttpClient because its
    /// timeouts cover a stalled read, which on this framework is the one that matters.
    /// </summary>
    public sealed class Downloader
    {
        private const string UserAgent = "DinoDefendersLauncher";
        private const int BufferSize = 64 * 1024;

        public int Attempts = 5;
        public int FirstWaitMilliseconds = 1000;
        public int TimeoutMilliseconds = 30000;

        /// <summary>For when someone is sat looking at a disabled Play button until this gives up.</summary>
        public int ImpatientAttempts = 2;
        public int ImpatientTimeoutMilliseconds = 6000;

        /// <summary>
        /// Anything small, whole, into memory. Being <paramref name="patient"/> is for when there's nothing else to fall back on.
        /// Otherwise a quick answer that it can't be had is worth more than a slow one that it can.
        /// </summary>
        public byte[] Fetch(Uri url, int mostBytes, bool patient, CancellationToken cancel)
        {
            int attempts = patient ? Attempts : ImpatientAttempts;
            int timeout = patient ? TimeoutMilliseconds : ImpatientTimeoutMilliseconds;
            for (int attempt = 1; ; attempt++)
            {
                cancel.ThrowIfCancellationRequested();
                try
                {
                    return Read(url, mostBytes, timeout, cancel);
                }
                catch (Exception e) when (e is WebException || e is IOException)
                {
                    cancel.ThrowIfCancellationRequested();
                    DownloadException problem = Explain(e, url);
                    if (problem.Permanent || attempt >= attempts) throw problem;
                    Wait(attempt, cancel);
                }
            }
        }

        /// <summary>Puts a file at <paramref name="target"/> once it's known to be the right one. Reports how many of its bytes are here.</summary>
        public void FetchFile(Uri url, string target, long size, string sha256, Action<long> progress, CancellationToken cancel)
        {
            if (File.Exists(target) && new FileInfo(target).Length == size && Hashing.OfFile(target) == sha256)
            {
                progress(size);
                return;
            }

            string part = target + ".part";
            int failures = 0;
            long furthest = LengthOf(part);
            while (true)
            {
                cancel.ThrowIfCancellationRequested();
                long before = LengthOf(part);
                try
                {
                    if (before > size)
                    {
                        File.Delete(part);
                        before = 0;
                    }
                    if (before < size) Receive(url, part, before, size, progress, cancel);

                    if (Hashing.OfFile(part) == sha256)
                    {
                        if (File.Exists(target)) File.Delete(target);
                        File.Move(part, target);
                        return;
                    }

                    File.Delete(part);
                    throw new DownloadException($"{Path.GetFileName(target)} arrived damaged.");
                }
                catch (Exception e) when (e is WebException || e is IOException || e is DownloadException)
                {
                    cancel.ThrowIfCancellationRequested();
                    DownloadException problem = e as DownloadException ?? Explain(e, url);

                    // A range the server won't honour means what's here isn't what it has, so it's no use to keep.
                    if (problem.Status == 416) File.Delete(part);

                    // Getting further than ever before counts as working: this is a bad line, not a dead one. Only further than
                    // ever, or a file shorter than promised would get as far as it goes, start over, and do that for good.
                    failures = LengthOf(part) > furthest ? 1 : failures + 1;
                    furthest = Math.Max(furthest, LengthOf(part));
                    if (problem.Permanent || failures >= Attempts) throw problem;
                    Wait(failures, cancel);
                }
            }
        }

        private static byte[] Read(Uri url, int mostBytes, int timeout, CancellationToken cancel)
        {
            HttpWebRequest request = Create(url, timeout);
            using (cancel.Register(request.Abort))
            using (var response = (HttpWebResponse)request.GetResponse())
            using (Stream input = response.GetResponseStream())
            using (var output = new MemoryStream())
            {
                var buffer = new byte[BufferSize];
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                    if (output.Length > mostBytes) throw new DownloadException($"{url} is far bigger than it should be.", permanent: true);
                }
                return output.ToArray();
            }
        }

        private void Receive(Uri url, string part, long from, long size, Action<long> progress, CancellationToken cancel)
        {
            HttpWebRequest request = Create(url, TimeoutMilliseconds);
            if (from > 0) request.AddRange(from);

            using (cancel.Register(request.Abort))
            using (var response = (HttpWebResponse)request.GetResponse())
            using (Stream input = response.GetResponseStream())
            {
                // A server is free to ignore the range and send the whole thing, which is just a slower way of being right.
                if (response.StatusCode != HttpStatusCode.PartialContent) from = 0;

                using var output = new FileStream(part, from == 0 ? FileMode.Create : FileMode.Append, FileAccess.Write, FileShare.None);
                var buffer = new byte[BufferSize];
                long done = from;
                progress(done);
                while (done < size)
                {
                    int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, size - done));
                    if (read == 0) throw new IOException("The connection closed early.");
                    output.Write(buffer, 0, read);
                    done += read;
                    progress(done);
                }
            }
        }

        private static HttpWebRequest Create(Uri url, int timeout)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = UserAgent;
            request.AllowAutoRedirect = true;
            request.MaximumAutomaticRedirections = 10;
            request.Timeout = timeout;
            request.ReadWriteTimeout = timeout;
            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            return request;
        }

        private static DownloadException Explain(Exception e, Uri url)
        {
            if (e is WebException web && web.Response is HttpWebResponse response)
            {
                int status = (int)response.StatusCode;
                response.Dispose();
                bool gone = status == 404 || status == 410;
                return new DownloadException($"The server said {status} for {url}.", gone, status);
            }
            return new DownloadException($"{e.Message} ({url})");
        }

        private void Wait(int failures, CancellationToken cancel)
        {
            int wait = FirstWaitMilliseconds << Math.Min(failures - 1, 4);
            cancel.WaitHandle.WaitOne(wait);
            cancel.ThrowIfCancellationRequested();
        }

        private static long LengthOf(string path)
        {
            var file = new FileInfo(path);
            return file.Exists ? file.Length : 0;
        }
    }
}
