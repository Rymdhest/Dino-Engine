using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Dino_Launcher.Update;

namespace Dino_LaunchLab
{
    /// <summary>
    /// Stands in for GitHub, behaving as its documentation says: drafts can't be seen, an asset name can't be used twice, "latest" is
    /// whatever was last marked so, and downloads go through two redirects. As strict as the real thing about what it's sent.
    /// </summary>
    internal sealed class FakeGitHub : IDisposable
    {
        public const string Token = "test-token";

        private const string Api = "/repos/" + Feed.Owner + "/" + Feed.Repository + "/releases";

        public sealed class Release
        {
            public long Id;
            public string Tag = "";
            public string Body = "";
            public bool Draft;
            public bool Prerelease;
            public readonly Dictionary<string, byte[]> Assets = new Dictionary<string, byte[]>();
        }

        private readonly object _lock = new object();
        private readonly List<Release> _releases = new List<Release>();
        private readonly FaultyServer _server = new FaultyServer();
        private long _nextId = 100;
        private Release? _latest;

        /// <summary>An upload of this name gets a 500.</summary>
        public string? FailUpload;

        /// <summary>What it was asked to do, in order, in a few words each.</summary>
        public readonly List<string> Calls = new List<string>();

        /// <summary>Anything sent to the API that it wouldn't have taken from a real client.</summary>
        public readonly List<string> Complaints = new List<string>();

        /// <summary>Whether anything anonymous arrived carrying the token.</summary>
        public bool TokenSeenOnFeed;

        /// <summary>How many times the runtime has been downloaded, rather than asked about.</summary>
        public int RuntimeFetches;

        public FakeGitHub()
        {
            _server.Handler = Handle;
        }

        public string ApiUrl => _server.Url.ToString().TrimEnd('/');
        public Uri FeedUrl => new Uri(_server.Url, "feed/");
        public Release? Latest => _latest;
        public List<Release> Releases
        {
            get
            {
                lock (_lock) return _releases.ToList();
            }
        }

        private Response? Handle(Request request)
        {
            lock (_lock)
            {
                if (request.Path.StartsWith("/feed/") || request.Path.StartsWith("/cdn/")) return Serve(request);

                bool lookingOnly = request.Method == "GET";
                if (!lookingOnly && request.Header("Authorization") != $"Bearer {Token}") return Response.Text(401, "{\"message\":\"Bad credentials\"}");
                if (request.Header("User-Agent").Length == 0) Complaints.Add("No User-Agent, which GitHub refuses.");

                if (request.Path == Api && request.Method == "POST") return Create(request);
                if (request.Path.StartsWith(Api + "/tags/")) return FindTag(request.Path.Substring((Api + "/tags/").Length));
                if (request.Path.StartsWith("/uploads/")) return Upload(request);
                if (request.Path.StartsWith(Api + "/") && request.Method == "PATCH") return Update(request);
                if (request.Path.StartsWith(Api + "/") && request.Method == "DELETE") return Delete(request);
                return Response.Text(404, "{\"message\":\"Not Found\"}");
            }
        }

        private Response Create(Request request)
        {
            string body = Encoding.UTF8.GetString(request.Body);
            string tag = Field(body, "tag_name");
            if (tag.Length == 0) return Response.Text(422, "{\"message\":\"tag_name is required\"}");
            if (_releases.Any(r => r.Tag == tag)) return Response.Text(422, "{\"message\":\"already_exists\"}");
            if (!body.Contains("\"draft\":true")) Complaints.Add($"{tag} wasn't made as a draft, so testers could see it half uploaded.");

            var release = new Release { Id = _nextId++, Tag = tag, Body = Field(body, "body"), Draft = true, Prerelease = body.Contains("\"prerelease\":true") };
            _releases.Add(release);
            Calls.Add($"create {tag}");

            string upload = $"{ApiUrl}/uploads/{release.Id}/assets{{?name,label}}";
            return Response.Text(201, $"{{\"id\":{release.Id},\"upload_url\":\"{upload}\"}}");
        }

        private Response FindTag(string tag)
        {
            Release? release = _releases.FirstOrDefault(r => r.Tag == tag && !r.Draft);
            return release == null ? Response.Text(404, "{\"message\":\"Not Found\"}") : Response.Text(200, $"{{\"id\":{release.Id}}}");
        }

        private Response Upload(Request request)
        {
            long id = long.Parse(request.Path.Split('/')[2]);
            Release? release = _releases.FirstOrDefault(r => r.Id == id);
            if (release == null) return Response.Text(404, "{}");

            Match name = Regex.Match(request.Query, "name=([^&]+)");
            if (!name.Success) return Response.Text(422, "{\"message\":\"name is required\"}");
            string asset = Uri.UnescapeDataString(name.Groups[1].Value);

            if (request.Header("Content-Type").Length == 0) Complaints.Add($"{asset} was uploaded with no Content-Type.");
            if (asset.Any(c => !(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_'))) Complaints.Add($"{asset} would be renamed by GitHub.");
            if (release.Assets.ContainsKey(asset)) return Response.Text(422, "{\"message\":\"already_exists\"}");

            if (FailUpload == asset) return Response.Text(500, "{\"message\":\"Server Error\"}");

            release.Assets[asset] = request.Body;
            Calls.Add($"upload {asset}");
            return Response.Text(201, "{}");
        }

        private Response Update(Request request)
        {
            long id = long.Parse(request.Path.Split('/').Last());
            Release? release = _releases.FirstOrDefault(r => r.Id == id);
            if (release == null) return Response.Text(404, "{}");

            string body = Encoding.UTF8.GetString(request.Body);
            if (body.Contains("\"draft\":false")) release.Draft = false;
            if (Field(body, "make_latest") == "true")
            {
                if (release.Draft || release.Prerelease) Complaints.Add($"{release.Tag} can't be latest: drafts and prereleases never are.");
                _latest = release;
            }
            Calls.Add($"publish {release.Tag} latest={Field(body, "make_latest")}");
            return Response.Text(200, $"{{\"id\":{id}}}");
        }

        private Response Delete(Request request)
        {
            long id = long.Parse(request.Path.Split('/').Last());
            _releases.RemoveAll(r => r.Id == id);
            Calls.Add("delete");
            return new Response { Status = 204 };
        }

        private Response Serve(Request request)
        {
            if (request.Header("Authorization").Length > 0) TokenSeenOnFeed = true;

            string[] parts = request.Path.Trim('/').Split('/');
            if (request.Path.StartsWith("/feed/latest/download/"))
            {
                return _latest == null ? Response.Text(404, "Not Found") : Response.Redirect($"{ApiUrl}/feed/download/{_latest.Tag}/{parts.Last()}");
            }

            if (request.Path.StartsWith("/feed/download/"))
            {
                Release? shown = _releases.FirstOrDefault(r => r.Tag == parts[2] && !r.Draft);
                return shown == null ? Response.Text(404, "Not Found") : Response.Redirect($"{_server.CdnUrl}cdn/{parts[2]}/{parts[3]}");
            }

            if (request.Method == "GET" && parts.Last() == Feed.RuntimePackName) RuntimeFetches++;

            Release? release = _releases.FirstOrDefault(r => r.Tag == parts[1]);
            return release != null && release.Assets.TryGetValue(parts[2], out byte[]? bytes) ? new Response { Body = bytes } : Response.Text(404, "Not Found");
        }

        private static string Field(string json, string name)
        {
            Match match = Regex.Match(json, $"\"{name}\":\"((?:[^\"\\\\]|\\\\.)*)\"");
            return match.Success ? match.Groups[1].Value : "";
        }

        public void Dispose() => _server.Dispose();
    }
}
