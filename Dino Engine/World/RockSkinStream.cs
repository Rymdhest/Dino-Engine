using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Rock;
using OpenTK.Mathematics;
using System.Collections.Concurrent;

namespace Dino_Engine.World
{
    /// <summary>
    /// Brings the skin of the rock (<see cref="RockSkin"/>) in and out around the camera, as the terrain's chunks are. Tiles are grown on threads of their own, nearest the camera first and
    /// further out than they're shown, so that walking up to a cliff finds it clad; what's been grown is kept for as long as the world is, and only what's near is on the GPU. A world
    /// entered retires the last one's, on the render thread, which is the only thread anything here but the growing runs on.
    /// </summary>
    public sealed class RockSkinStream
    {
        private const float Ahead = 40f;            // How much further out than they're shown tiles are grown.
        private const float Linger = 24f;           // And how much further out a shown one stays, so that standing on a tile's edge doesn't bring it in and out.
        private const int UploadsPerFrame = 2;

        private static RockSkinStream? _current;
        private static (WorldData World, RockSkin Skin, ConcurrentDictionary<Vector2i, FloraMesh?> Grown)? _prepared;

        private readonly Task<RockSkin> _skin;
        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentDictionary<Vector2i, FloraMesh?> _grown = new();
        private readonly HashSet<Vector2i> _asked = new();
        private readonly List<Vector2i> _waiting = new();
        private readonly Dictionary<Vector2i, Shown> _shown = new();
        private readonly List<Vector2i> _scratch = new();
        private Vector2 _camera;
        private int _workers;

        /// <summary>A tile on the GPU: its model, if it has anything in it, and whatever stands for it in the world.</summary>
        public readonly record struct Shown(glModel? Model, object? Handle);

        public RockSkinStream(WorldData world)
        {
            _current?.Retire();
            _current = this;
            var prepared = _prepared;
            _prepared = null;
            if (prepared is { } ready && ready.World == world)
            {
                _skin = Task.FromResult(ready.Skin);
                foreach ((Vector2i tile, FloraMesh? mesh) in ready.Grown)
                {
                    _grown[tile] = mesh;
                    _asked.Add(tile);
                }
                return;
            }
            CancellationToken stop = _stop.Token;
            _skin = Task.Run(() => new RockSkin(world), stop);
        }

        /// <summary>
        /// Works out a world's skin and grows the tiles within <paramref name="reach"/> of a point, on every core, while the world is still loading, so that the land is clad when
        /// it's first seen. The stream made for that world next takes them over. Safe from any thread, and it touches no GPU.
        /// </summary>
        public static void Prepare(WorldData world, Vector2 around, float reach)
        {
            var skin = new RockSkin(world);
            var grown = new ConcurrentDictionary<Vector2i, FloraMesh?>();
            List<Vector2i> near = skin.Tiles.Where(tile => Distance(tile, around) <= reach).OrderBy(tile => Distance(tile, around)).ToList();
            Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(near, EnumerablePartitionerOptions.NoBuffering), tile => grown[tile] = skin.Build(tile));
            _prepared = (world, skin, grown);
        }

        /// <summary>The skin itself, once it's been worked out.</summary>
        public RockSkin? Skin => _skin.IsCompletedSuccessfully ? _skin.Result : null;

        /// <summary>
        /// Asks for every tile within <paramref name="reach"/> of the camera and shows those grown, a few a frame, through <paramref name="show"/>, and lets go of what's gone out of
        /// reach through <paramref name="hide"/>.
        /// </summary>
        public void Update(Vector2 camera, float reach, Func<glModel, object> show, Action<object> hide)
        {
            RockSkin? skin = Skin;
            if (skin == null) return;
            lock (_waiting) _camera = camera;

            // What's wanted.
            float growReach = reach + Ahead;
            Vector2i low = RockSkin.TileOf(camera - new Vector2(growReach));
            Vector2i high = RockSkin.TileOf(camera + new Vector2(growReach));
            bool asked = false;
            for (int z = low.Y; z <= high.Y; z++)
            {
                for (int x = low.X; x <= high.X; x++)
                {
                    var tile = new Vector2i(x, z);
                    if (!skin.Tiles.Contains(tile) || _asked.Contains(tile) || Distance(tile, camera) > growReach) continue;
                    _asked.Add(tile);
                    lock (_waiting) _waiting.Add(tile);
                    asked = true;
                }
            }
            if (asked) StartWorkers(skin);

            // What's gone.
            _scratch.Clear();
            foreach (Vector2i tile in _shown.Keys) if (Distance(tile, camera) > reach + Linger) _scratch.Add(tile);
            foreach (Vector2i tile in _scratch)
            {
                Shown gone = _shown[tile];
                if (gone.Handle != null) hide(gone.Handle);
                gone.Model?.cleanUp();
                _shown.Remove(tile);
            }

            // What's come, nearest first.
            _scratch.Clear();
            for (int z = low.Y; z <= high.Y; z++)
            {
                for (int x = low.X; x <= high.X; x++)
                {
                    var tile = new Vector2i(x, z);
                    if (_shown.ContainsKey(tile) || !_grown.ContainsKey(tile) || Distance(tile, camera) > reach) continue;
                    _scratch.Add(tile);
                }
            }
            _scratch.Sort((a, b) => Distance(a, camera).CompareTo(Distance(b, camera)));
            // A world just entered has nothing to show yet, and gets everything at once.
            int budget = _shown.Count == 0 ? int.MaxValue : UploadsPerFrame;
            for (int i = 0; i < _scratch.Count && i < budget; i++)
            {
                Vector2i tile = _scratch[i];
                FloraMesh? mesh = _grown[tile];
                if (mesh == null)
                {
                    _shown[tile] = new Shown(null, null);
                    continue;
                }
                glModel model = glLoader.loadToVAO(mesh);
                _shown[tile] = new Shown(model, show(model));
            }
        }

        private void StartWorkers(RockSkin skin)
        {
            // A quarter of the cores, on threads of their own under the frame's, so that the game and its driver always come first: a tile is never wanted this frame.
            int most = Math.Max(1, Environment.ProcessorCount / 4);
            lock (_waiting)
            {
                while (_workers < most)
                {
                    _workers++;
                    CancellationToken stop = _stop.Token;
                    var thread = new Thread(() => Work(skin, stop)) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Rock skin" };
                    thread.Start();
                }
            }
        }

        private void Work(RockSkin skin, CancellationToken stop)
        {
            while (!stop.IsCancellationRequested)
            {
                Vector2i tile;
                lock (_waiting)
                {
                    if (_waiting.Count == 0)
                    {
                        _workers--;
                        return;
                    }
                    int best = 0;
                    float nearest = float.MaxValue;
                    for (int i = 0; i < _waiting.Count; i++)
                    {
                        float d = Distance(_waiting[i], _camera);
                        if (d < nearest)
                        {
                            nearest = d;
                            best = i;
                        }
                    }
                    tile = _waiting[best];
                    _waiting.RemoveAt(best);
                }
                FloraMesh? mesh;
                try
                {
                    mesh = skin.Build(tile);
                }
                catch (Exception e)
                {
                    // A tile that can't be grown goes without, and says why: the rest of the rock is still worth having.
                    Console.WriteLine($"The rock's skin at tile {tile} couldn't be grown: {e}");
                    mesh = null;
                }
                _grown[tile] = mesh;
            }
        }

        // Metres from a point to the nearest edge of a tile.
        private static float Distance(Vector2i tile, Vector2 point)
        {
            Vector2 min = new Vector2(tile.X, tile.Y) * RockSkin.TileSize;
            Vector2 nearest = Vector2.Clamp(point, min, min + new Vector2(RockSkin.TileSize));
            return (nearest - point).Length;
        }

        // Another world has been entered: nothing more is grown, and what's on the GPU goes. Its entities went with the world.
        private void Retire()
        {
            _stop.Cancel();
            foreach (Shown shown in _shown.Values) shown.Model?.cleanUp();
            _shown.Clear();
        }
    }
}
