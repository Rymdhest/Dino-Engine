using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dino_Launcher.Update;

namespace Dino_Release
{
    /// <summary>A file going up with a release.</summary>
    internal readonly record struct Asset(string Name, string Path);

    /// <summary>
    /// Where releases go. Reading is anonymous and goes through the same addresses a launcher uses, so what comes back here is what
    /// testers would get. Publishing is all or nothing: a release is invisible until the last step.
    /// </summary>
    internal interface IReleaseStore
    {
        byte[]? ReadLive();
        byte[]? Read(string relativeUrl);

        /// <summary>How big something is, without fetching it. Null if it isn't there.</summary>
        long? SizeOf(string relativeUrl);

        bool HasRelease(string tag);
        void Publish(string tag, string title, string notes, string commit, bool latest, IReadOnlyList<Asset> assets);
    }

    /// <summary>Reads a feed the way a launcher does.</summary>
    internal abstract class FeedReader
    {
        private readonly HttpClient _anonymous = new() { Timeout = TimeSpan.FromMinutes(5) };
        private readonly Uri _feed;

        protected FeedReader(string feed)
        {
            _feed = new Uri(feed);
            _anonymous.DefaultRequestHeaders.UserAgent.ParseAdd("DinoRelease");
        }

        public byte[]? ReadLive() => Read(Feed.ManifestPath);

        public byte[]? Read(string relativeUrl)
        {
            using HttpResponseMessage response = _anonymous.GetAsync(new Uri(_feed, relativeUrl)).Result;
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode) throw new ReleaseException($"{relativeUrl}: the server said {(int)response.StatusCode}.");
            return response.Content.ReadAsByteArrayAsync().Result;
        }

        public long? SizeOf(string relativeUrl)
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, new Uri(_feed, relativeUrl));
            using HttpResponseMessage response = _anonymous.Send(request);
            return response.IsSuccessStatusCode ? response.Content.Headers.ContentLength : null;
        }
    }

    /// <summary>
    /// GitHub Releases. Each release is made as a draft, filled, and then published, so nobody sees one with half its files. "Latest" is
    /// stated outright, as left alone GitHub goes by the date of the commit, which would ignore a release of an older commit.
    /// </summary>
    internal sealed class GitHubStore : FeedReader, IReleaseStore
    {
        private readonly HttpClient _api = new() { Timeout = TimeSpan.FromMinutes(15) };
        private readonly string _root;
        private readonly Func<string?> _findToken;

        /// <param name="findToken">Only asked when there's something to publish. Looking at what's out there needs no credentials.</param>
        public GitHubStore(string apiUrl, string feed, Func<string?> findToken) : base(feed)
        {
            _root = $"{apiUrl.TrimEnd('/')}/repos/{Dino_Launcher.Update.Feed.Owner}/{Dino_Launcher.Update.Feed.Repository}/releases";
            _findToken = findToken;

            _api.DefaultRequestHeaders.UserAgent.ParseAdd("DinoRelease");
            _api.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            _api.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        }

        public bool HasRelease(string tag)
        {
            using HttpResponseMessage response = _api.GetAsync($"{_root}/tags/{tag}").Result;
            if (response.StatusCode == HttpStatusCode.NotFound) return false;
            Require(response, $"Looking for release {tag}");
            return true;
        }

        public void Publish(string tag, string title, string notes, string commit, bool latest, IReadOnlyList<Asset> assets)
        {
            string token = _findToken() ?? throw new ReleaseException("No GitHub token. Set GITHUB_TOKEN, or push to GitHub once with git so that it has one stored.");
            _api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var draft = new JsonObject
            {
                ["tag_name"] = tag,
                ["name"] = title,
                ["body"] = notes,
                ["draft"] = true,
                ["prerelease"] = !latest,
            };
            // A tag can only go on a commit GitHub has. The manifest says what was really built either way.
            string built = commit.Split('+')[0];
            if (Git.IsPushed(built)) draft["target_commitish"] = built;
            else Console.WriteLine($"  {built} isn't pushed, so the tag goes on the default branch. Push it to keep them together.");

            JsonNode created = Send(HttpMethod.Post, _root, draft, "Creating the release");
            long id = created["id"]!.GetValue<long>();
            string uploadUrl = created["upload_url"]!.GetValue<string>().Split('{')[0];

            try
            {
                foreach (Asset asset in assets) Upload(uploadUrl, asset);
            }
            catch (Exception e) when (e is ReleaseException or HttpRequestException or AggregateException or IOException)
            {
                // Nobody saw any of this. Nothing is left to get in the way of the next try.
                TryDelete(id);
                throw new ReleaseException($"Uploading failed, and nothing was released: {e.Message}");
            }

            var publish = new JsonObject { ["draft"] = false, ["make_latest"] = latest ? "true" : "false" };
            Send(HttpMethod.Patch, $"{_root}/{id}", publish, "Publishing the release");
        }

        private void Upload(string uploadUrl, Asset asset)
        {
            Console.WriteLine($"  Uploading {asset.Name} ({new FileInfo(asset.Path).Length / 1024} KB)");
            using var stream = File.OpenRead(asset.Path);
            using var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            using HttpResponseMessage response = _api.PostAsync($"{uploadUrl}?name={Uri.EscapeDataString(asset.Name)}", content).Result;
            Require(response, $"Uploading {asset.Name}");
        }

        private JsonNode Send(HttpMethod method, string url, JsonObject body, string doing)
        {
            using var request = new HttpRequestMessage(method, url) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
            using HttpResponseMessage response = _api.Send(request);
            Require(response, doing);
            return JsonNode.Parse(response.Content.ReadAsStringAsync().Result) ?? throw new ReleaseException($"{doing}: GitHub sent nothing back.");
        }

        private void TryDelete(long id)
        {
            try
            {
                using HttpResponseMessage response = _api.DeleteAsync($"{_root}/{id}").Result;
            }
            catch (Exception e) when (e is HttpRequestException or AggregateException)
            {
                Console.WriteLine($"  The draft release couldn't be removed: {e.Message}");
            }
        }

        private static void Require(HttpResponseMessage response, string doing)
        {
            if (response.IsSuccessStatusCode) return;

            string body = response.Content.ReadAsStringAsync().Result;
            string hint = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? " The token may lack permission to write to this repository: set GITHUB_TOKEN to one that has 'contents: write'."
                : "";
            throw new ReleaseException($"{doing}: GitHub said {(int)response.StatusCode}.{hint} {body}");
        }
    }

    /// <summary>A folder laid out like a feed: for tests, and for any other static host. Copy it up and point a launcher at it.</summary>
    internal sealed class FolderStore : IReleaseStore
    {
        private readonly string _root;

        public FolderStore(string root)
        {
            _root = root;
        }

        private string PathOf(string relativeUrl) => Path.Combine(_root, relativeUrl.Replace('/', Path.DirectorySeparatorChar));

        public byte[]? ReadLive() => Read(Feed.ManifestPath);

        public byte[]? Read(string relativeUrl)
        {
            string path = PathOf(relativeUrl);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        public long? SizeOf(string relativeUrl)
        {
            var file = new FileInfo(PathOf(relativeUrl));
            return file.Exists ? file.Length : null;
        }

        public bool HasRelease(string tag) => Directory.Exists(PathOf($"download/{tag}"));

        public void Publish(string tag, string title, string notes, string commit, bool latest, IReadOnlyList<Asset> assets)
        {
            string release = PathOf($"download/{tag}");
            Directory.CreateDirectory(release);
            foreach (Asset asset in assets) File.Copy(asset.Path, Path.Combine(release, asset.Name), true);
            if (!latest) return;

            string live = PathOf("latest/download");
            Directory.CreateDirectory(live);
            foreach (string old in Directory.GetFiles(live)) File.Delete(old);
            foreach (Asset asset in assets) File.Copy(asset.Path, Path.Combine(live, asset.Name), true);
        }
    }
}
