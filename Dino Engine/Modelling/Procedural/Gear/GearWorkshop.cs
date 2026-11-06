using Dino_Engine.Modelling.Model;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>Which model something is, by what it was made from. Two of the same are the same model.</summary>
    public readonly record struct GearKey(ulong Seed, ulong Traits);

    /// <summary>
    /// Turns specs into models without costing a frame. A boss drops ten things at once and each is a mesh nobody has made yet, so meshes are made on worker threads and only the upload
    /// happens here, a couple a frame. Models here are made and lost all the time, unlike every other model in the game, so anything using one has to keep asking for it: what nobody has
    /// asked for in a while is freed, a few frames late, as commands for a frame may still name it.
    /// </summary>
    public sealed class GearWorkshop
    {
        private const int MostUploadsAFrame = 2;
        private const int KeptFrames = 600;
        private const int FreedFramesLate = 4;

        private sealed class Entry
        {
            public Task<Mesh>? Making;
            public glModel? Model;
            public long LastAsked;
        }

        private readonly Dictionary<GearKey, Entry> _entries = new();
        private readonly List<GearKey> _expired = new();
        private readonly List<(glModel model, long frame)> _dying = new();
        private long _frame;

        public int Count => _entries.Count;

        /// <summary>How many have been asked for, ever.</summary>
        public int Made { get; private set; }

        /// <summary>The model for this, or nothing yet. Keep asking every frame for as long as it's in use: that's what keeps it.</summary>
        public glModel? Get(GearKey key, Func<Mesh> make)
        {
            if (!_entries.TryGetValue(key, out Entry? entry))
            {
                entry = new Entry { Making = Task.Run(make) };
                _entries.Add(key, entry);
                Made++;
            }
            entry.LastAsked = _frame;
            return entry.Model;
        }

        /// <summary>Runs once a frame, on the thread with the GL context.</summary>
        public void Update()
        {
            _frame++;

            int uploads = 0;
            foreach ((GearKey _, Entry entry) in _entries)
            {
                if (entry.Making == null || !entry.Making.IsCompleted || uploads >= MostUploadsAFrame) continue;
                Task<Mesh> making = entry.Making;
                entry.Making = null;
                if (making.IsCompletedSuccessfully)
                {
                    entry.Model = glLoader.loadToVAO(making.Result);
                    uploads++;
                }
                else Console.WriteLine($"A piece of gear couldn't be made: {making.Exception?.GetBaseException().Message}");
            }

            _expired.Clear();
            foreach ((GearKey key, Entry entry) in _entries)
            {
                if (entry.Making == null && _frame - entry.LastAsked > KeptFrames) _expired.Add(key);
            }
            foreach (GearKey key in _expired)
            {
                if (_entries.Remove(key, out Entry? entry) && entry.Model != null) _dying.Add((entry.Model, _frame));
            }

            for (int i = _dying.Count - 1; i >= 0; i--)
            {
                if (_frame - _dying[i].frame < FreedFramesLate) continue;
                _dying[i].model.cleanUp();
                _dying.RemoveAt(i);
            }
        }
    }
}
