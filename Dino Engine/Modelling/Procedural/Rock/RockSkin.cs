using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.Modelling.Procedural.Rock
{
    /// <summary>
    /// The skin of bare rock: every face of a mountain clad in its own joint blocks, as a mesh. A height map at a metre a node is smooth whatever's drawn on it, and from this camera
    /// lines on a smooth face read as a smooth face with lines on it, so the rock is made of what rock is made of. The world's beds (<see cref="Geology"/>) are split into courses,
    /// and each course by two sets of joints into blocks (columns, in basalt), and every block stands out of the mountain by its own amount: a hard course further than a soft one,
    /// so a ledge overhangs the band worn back under it, each tipped a little its own way and broken at its edges, by the form of the mountain it's part of
    /// (<see cref="MountainForm"/>). The joints between are open, down to the ground's own surface, which is what shows in a crack.
    /// <para>
    /// It's a signed distance like a stone's (<see cref="RockForge"/>), measured from the drawn ground: how far from it a point is goes by the ground's height a slice at a time,
    /// sideways as well as down, as a face of rock is nearly upright and the height under a point in front of one is the foot of it. A world is cut into tiles of
    /// <see cref="TileSize"/>, each meshed by surface nets at <see cref="Cell"/> and simplified, with the points it shares with its neighbours held still, so tiles meet
    /// without a crack. Only rock nobody walks on has a skin (<see cref="WeightAt"/>): it fades out into the ground over the last metres before anywhere anyone stands, and it's
    /// cut off under the line from the camera to anyone standing north of it.
    /// Headless and safe on any thread: a tile is grown wherever and whenever, and the same tile always comes out the same.
    /// </para>
    /// </summary>
    public sealed class RockSkin
    {
        public const float TileSize = 16f;
        public const float Cell = 0.25f;
        private const int Cells = 64;                   // TileSize / Cell.
        private const float Coarse = 0.5f;              // How far apart the ground's distance is worked out, and read between.
        private const float Margin = 5f;                // How far past a tile the ground is looked at, for how far it is from a point in it: as far as anything is measured.
        private const float FarthestGround = MostOut + 1.5f;    // Further than this from the ground it's all the same: well past anything that stands out of it.
        private const float Core = 0.3f;                // A joint closes this far inside the ground, which is drawn over it.
        private const float Inner = 0.7f;               // And the skin is hollow from here in.
        private const float Reach = 0.7f;               // Within this of a joint the block across it is looked at too, so that one block steps to the next with nothing torn.
        private const float LeastWeight = 0.05f;
        private const float Shut = 0.3f;                // The share of joints across a row that never opened, joining two blocks into one.
        private const float Sunk = -0.5f;               // How far into the ground a block is at the very edge of the rock.
        private const float Rough = 0.07f;              // Metres the face of a block wanders off its plane: the shader draws anything finer.
        private const float BuriedBelow = -0.1f;        // A face this far inside the ground is never seen, and goes.
        private const float Kept = 0.06f;               // The least share of a tile's triangles kept,
        private const double MostError = 0.006;         // and how far simplifying may move the faces that are left, in square metres times metres,
        private const int MostTriangles = 9000;         // unless a tile would still have more than this, and then as far as it takes.
        private const int MostNeighbours = 16;          // No point simplified into a fan of more than this many needles.

        /// <summary>What a world's rock breaks into in its skin: metres between partings in a bed, the share that ever opened, and metres between joints.</summary>
        private readonly record struct Breaks(float CourseLeast, float CourseMost, float Open, float JointLeast, float JointMost);

        /// <summary>
        /// How a form wears its blocks. Metres out of the ground: <c>Base</c> for every block, <c>HardOut</c> more for a hard course than a soft, <c>CourseOut</c> for a course and
        /// <c>BlockOut</c> for a block, at most. <c>Tilt</c> is how far a face turns off the mountain's, in metres a metre, <c>Chip</c> how much an edge is broken off, <c>Gap</c>
        /// half a joint's width, and <c>Group</c> how many courses share their joints, so that clefts run through a tower from top to bottom. <c>Mass</c> is metres more that whole
        /// runs of blocks stand out together, over stretches of <c>MassSize</c>, which is what makes a buttress of a face and a bay of the next.
        /// </summary>
        public readonly record struct Style(float Base, float HardOut, float CourseOut, float BlockOut, float Tilt, float Chip, float Gap, int Group, float Mass, float MassSize)
        {
            public float Most => MathF.Min(Base + HardOut + CourseOut + BlockOut + Mass, MostOut);
        }

        private const float MostOut = 3.2f;

        public static Style StyleOf(MountainForm form) => form switch
        {
            MountainForm.Ledged => new Style(0.3f, 1f, 0.5f, 0.35f, 0.14f, 0.25f, 0.16f, 1, 0.8f, 9f),
            MountainForm.Jagged => new Style(0.3f, 0.3f, 0.4f, 1f, 0.28f, 0.4f, 0.15f, 1, 1.1f, 6f),
            MountainForm.Towers => new Style(0.3f, 0.3f, 0.3f, 1f, 0.16f, 0.3f, 0.18f, 5, 1.3f, 7f),
            MountainForm.Domes => new Style(0.25f, 0.1f, 0.45f, 0.3f, 0.07f, 0.6f, 0.15f, 3, 0.6f, 12f),
            MountainForm.Canyons => new Style(0.3f, 1.2f, 0.5f, 0.25f, 0.12f, 0.22f, 0.16f, 2, 0.8f, 10f),
            MountainForm.Columns => new Style(0.3f, 0.2f, 0.3f, 1.1f, 0.05f, 0.1f, 0.13f, 1, 0.8f, 8f),
            _ => new Style(0.3f, 0.6f, 0.6f, 0.7f, 0.32f, 0.3f, 0.15f, 1, 1f, 6f),
        };

        private static Breaks BreaksOf(RockKind kind) => kind switch
        {
            RockKind.Granite => new Breaks(1.6f, 4f, 0.7f, 2f, 4.8f),
            RockKind.Limestone => new Breaks(0.9f, 2.2f, 0.75f, 1.4f, 3.2f),
            RockKind.Sandstone => new Breaks(1.1f, 3f, 0.55f, 1.6f, 4f),
            RockKind.Schist => new Breaks(1.2f, 2.4f, 0.5f, 1.6f, 3.2f),
            _ => new Breaks(0.8f, 1.6f, 0.6f, 0.9f, 1.5f),
        };

        private readonly WorldData _world;
        private readonly Geology _geology;
        private readonly Breaks _breaks;
        private readonly ScalarField _weight;
        private readonly Vector3 _bedNormal;
        private readonly Vector3 _along;
        private readonly Vector3 _across;
        private readonly float _layerScale;
        private readonly bool _columns;
        private readonly float _sheeted;
        private readonly uint _seed;

        /// <summary>Every tile with any skin in it.</summary>
        public readonly IReadOnlySet<Vector2i> Tiles;

        public RockSkin(WorldData world)
        {
            _world = world;
            _geology = world.Geology;
            _breaks = BreaksOf(_geology.Kind);
            _columns = _geology.Kind == RockKind.Basalt;
            _sheeted = _geology.Kind == RockKind.Granite ? 2.4f : 0.5f;
            _seed = (uint)world.Plan.Seed * 0x9E3779B9u + 0x7F4A7C15u;

            Vector3 up = new Vector3(_geology.Dip.X, 1f, _geology.Dip.Y);
            _layerScale = up.Length;
            _bedNormal = up / _layerScale;
            Vector3 strike = new Vector3(MathF.Cos(_geology.Strike), 0f, MathF.Sin(_geology.Strike));
            _along = Vector3.Normalize(strike - _bedNormal * Vector3.Dot(strike, _bedNormal));
            _across = Vector3.Cross(_bedNormal, _along);

            // How much skin the rock has: none short of half rock. Where anyone walks it fades into the ground, by how far a point is from where they'd stand, up and across, as
            // a cliff's face is a step from the path at its foot all the way up and nobody's feet are anywhere near the top of it. And nothing of it may come over the line from
            // the camera to anyone standing north of it, as the ground itself doesn't (RockSculpt.KeepHeroInSight): it's cut off flat there.
            ScalarField height = world.Height;
            int width = height.Width, depth = height.Depth;
            float cell = height.CellSize;
            _weight = new ScalarField(width, depth, cell);
            _across2 = new ScalarField(width, depth, cell);
            _standing = new ScalarField(width, depth, cell);
            _sight = new ScalarField(width, depth, cell);
            var tiles = new HashSet<Vector2i>();
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 at = new Vector2(x * cell, z * cell);
                    // Rock is what's drawn as rock: the mountains, and anything else too steep to hold soil, as every biome's slopes are (TerrainSurface).
                    float weight = MathF.Max(Smoothstep(0.42f, 0.72f, world.Rock.Sample(at)), Smoothstep(0.66f, 1f, height.SlopeAt(x, z)));
                    // And nothing anyone built stands in it: a wall on a slope, the side of an arena's pit, the edge of a bed in a square.
                    if (weight > LeastWeight) weight *= ClearOfBuilt(world, at);
                    _weight[x, z] = weight;
                    _across2[x, z] = 1e4f;
                    _sight[x, z] = 1e4f;
                    if (weight <= LeastWeight) continue;
                    Near(world, at, out float across, out float standing, out float sight);
                    _across2[x, z] = across;
                    _standing[x, z] = standing;
                    _sight[x, z] = sight;
                    // A tile reaches a little past its edges, as its blocks do.
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            Vector2 near = at + new Vector2(dx, dz) * Margin;
                            tiles.Add(new Vector2i((int)MathF.Floor(near.X / TileSize), (int)MathF.Floor(near.Y / TileSize)));
                        }
                    }
                }
            }
            Tiles = tiles;
        }

        private readonly ScalarField _across2;      // Metres across to the nearest ground anyone stands on,
        private readonly ScalarField _standing;     // and the height of it.
        private readonly ScalarField _sight;        // The lowest line from the camera to anyone north of a point, as a height there.
        private const float Clearance = 0.8f;       // Metres from where anyone stands before the skin starts,
        private const float Cleared = 2f;           // and where it's all there.
        private const float SightMargin = 0.3f;

        // The nearest ground anyone stands on, and the line to the middle of anyone north of a point (RockStage.HidesNoOne's).
        private static void Near(WorldData world, Vector2 at, out float across, out float standing, out float sight)
        {
            const float Cell = WorldData.WalkCell;
            const float Reach = Cleared + 2f;
            const float North = 8f;                 // Past this the line's well over anything the skin could add.
            across = 1e4f;
            standing = 0f;
            sight = 1e4f;
            int x0 = (int)MathF.Floor((at.X - Reach) / Cell), x1 = (int)MathF.Ceiling((at.X + Reach) / Cell);
            int z0 = (int)MathF.Floor((at.Y - Reach) / Cell), z1 = (int)MathF.Ceiling((at.Y + North) / Cell);
            for (int nz = z0; nz <= z1; nz++)
            {
                for (int nx = x0; nx <= x1; nx++)
                {
                    Vector2 node = new Vector2(nx, nz) * Cell;
                    if (!world.InBounds(node) || !world.Walkable.IsSet(node)) continue;
                    float ground = world.Height.Sample(node);
                    // A node holds for the square round it.
                    Vector2 off = Vector2.ComponentMax(Vector2.Zero, new Vector2(MathF.Abs(node.X - at.X), MathF.Abs(node.Y - at.Y)) - new Vector2(Cell * 0.5f));
                    float d = off.Length;
                    if (d < across)
                    {
                        across = d;
                        standing = ground;
                    }
                    if (MathF.Abs(node.X - at.X) <= Cell && node.Y + Cell * 0.5f >= at.Y)
                    {
                        float line = ground + 1f + MathF.Tan(MathF.PI / 2.7f) * MathF.Max(node.Y - Cell * 0.5f - at.Y, 0f) - SightMargin;
                        sight = MathF.Min(sight, line);
                    }
                }
            }
        }

        // How far a point is from anything built or laid out, as a share of the skin it may have: none within a metre, all of it from three.
        private static float ClearOfBuilt(WorldData world, Vector2 at)
        {
            const PlacementLayer Made = PlacementLayer.Built | PlacementLayer.Road;
            Footprint here = Footprint.Circle(at, 0.5f);
            if (world.Placement.Overlaps(here, Made, 1f)) return 0f;
            if (world.Placement.Overlaps(here, Made, 2f)) return 0.33f;
            if (world.Placement.Overlaps(here, Made, 3f)) return 0.67f;
            return 1f;
        }

        /// <summary>How much of its skin the rock at a point has, 0..1.</summary>
        public float WeightAt(Vector3 p)
        {
            float rock = _weight.Sample(p.X, p.Z);
            if (rock <= LeastWeight) return 0f;
            float across, standing;
            if (_world.Walkable.IsSet(p.Xz))
            {
                // Right over where someone stands: the ground under them is their height, exactly.
                across = 0f;
                standing = _world.Height.SampleCubic(p.X, p.Z);
            }
            else
            {
                Vector2i node = _weight.NearestNode(p.Xz);
                across = _across2.Clamped(node.X, node.Y);
                if (across > Cleared) return rock;
                standing = _standing.Clamped(node.X, node.Y);
            }
            float up = MathF.Max(MathF.Abs(p.Y - standing) - 0.3f, 0f);
            return rock * Smoothstep(Clearance, Cleared, MathF.Sqrt(across * across + up * up));
        }

        /// <summary>How far a point is from the ground as one tile's skin measures it, for checking the skin with no game.</summary>
        public Func<Vector3, float>? GroundOf(Vector2i tile)
        {
            var work = new Work(this, tile);
            return work.Ground() ? work.GroundDistance : null;
        }

        /// <summary>The height over which the skin at a point would hide someone, the least of the nodes round it.</summary>
        private float SightAt(float x, float z)
        {
            int i = (int)MathF.Floor(x / _sight.CellSize), k = (int)MathF.Floor(z / _sight.CellSize);
            return MathF.Min(MathF.Min(_sight.Clamped(i, k), _sight.Clamped(i + 1, k)), MathF.Min(_sight.Clamped(i, k + 1), _sight.Clamped(i + 1, k + 1)));
        }

        /// <summary>How much rock the skin has at all here, whatever the height.</summary>
        private float RockAt(float x, float z) => _weight.Sample(x, z);

        /// <summary>The tile a point is in.</summary>
        public static Vector2i TileOf(Vector2 position) => new Vector2i((int)MathF.Floor(position.X / TileSize), (int)MathF.Floor(position.Y / TileSize));

        /// <summary>The mesh of a tile, in world space, or null if there's nothing in it.</summary>
        /// <summary>What the skin makes of the points along a line, for looking into it with no game: each one's distance from the ground, its share of skin, the line over it and the field.</summary>
        public IEnumerable<string> Probe(Vector3 from, Vector3 to, int steps)
        {
            var work = new Work(this, TileOf(((from + to) * 0.5f).Xz));
            if (!work.Ground()) yield break;
            for (int i = 0; i <= steps; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / (float)steps);
                float weight = WeightAt(p);
                float s = work.GroundDistance(p);
                yield return $"{p.X:0.00} {p.Y:0.00} {p.Z:0.00}  ground {_world.Height.SampleCubic(p.X, p.Z):0.00}  s {s:0.00}  rock {RockAt(p.X, p.Z):0.00} weight {weight:0.00}  sight {SightAt(p.X, p.Z):0.00}  field {work.Distance(p):0.00}";
            }
        }

        // What one thread keeps from one tile to the next, as a tile's grid and lists come to megabytes and the collector paused the game copying them.
        [ThreadStatic] private static List<Vector3>? _scratchPoints;
        [ThreadStatic] private static List<int>? _scratchTriangles;
        [ThreadStatic] private static List<Vector3i>? _scratchCells;

        public FloraMesh? Build(Vector2i tile)
        {
            var work = new Work(this, tile);
            try
            {
                if (!work.Ground()) return null;

                List<Vector3> points = _scratchPoints ??= new List<Vector3>();
                List<int> triangles = _scratchTriangles ??= new List<int>();
                List<Vector3i> cells = _scratchCells ??= new List<Vector3i>();
                points.Clear();
                triangles.Clear();
                cells.Clear();
                work.March(points, triangles, cells);
                if (triangles.Count == 0) return null;

                // What's inside the ground is never seen, as the ground is drawn over it, and needn't be simplified either: what's left has open edges where it goes into the ground.
                Unburied(work, points, triangles);
                if (triangles.Count == 0) return null;

                // Where a tile meets the next its points are worked out by both from the same numbers, and neither may move them: the points on the edges along which the two
                // meet, which are those in the cells they share with a face on one side only, or on more than two. The rest of those cells only this tile has, and they go like any other.
                bool[] open = OnOpenEdges(points.Count, triangles);
                bool[] pinned = new bool[points.Count];
                for (int i = 0; i < pinned.Length; i++)
                {
                    Vector3i c = cells[i];
                    pinned[i] = open[i] && (c.X == 0 || c.X == Cells || c.Z == 0 || c.Z == Cells);
                }
                Decimator.Reduce(points, triangles, Math.Max(64, (int)(triangles.Count / 3 * Kept)), float.PositiveInfinity, pinned, open: true, mostError: MostError, mostNeighbours: MostNeighbours);
                if (triangles.Count / 3 > MostTriangles)
                {
                    // The points are new ones now, but a pinned one never moved, so it's still where its tile meets the next.
                    open = OnOpenEdges(points.Count, triangles);
                    pinned = new bool[points.Count];
                    for (int i = 0; i < pinned.Length; i++) pinned[i] = open[i] && IsPinned(points[i], tile);
                    Decimator.Reduce(points, triangles, MostTriangles, float.PositiveInfinity, pinned, open: true, mostNeighbours: MostNeighbours);
                }

                var kept = new List<int>(triangles);
                Unburied(work, points, kept);
                if (kept.Count == 0) return null;

                // The points kept, for the pins to stay with them.
                bool[] keptOpen = OnOpenEdges(points.Count, kept);
                int[] moved = new int[points.Count];
                Array.Fill(moved, -1);
                var keptPoints = new List<Vector3>();
                var keptPins = new List<bool>();
                for (int i = 0; i < kept.Count; i++)
                {
                    int v = kept[i];
                    if (moved[v] < 0)
                    {
                        moved[v] = keptPoints.Count;
                        keptPoints.Add(points[v]);
                        keptPins.Add(IsPinned(points[v], tile) && keptOpen[v]);
                    }
                    kept[i] = moved[v];
                }

                FloraMesh skin = RockForge.Dress(work.Distance, 1.4f, keptPoints, kept, keptPins.ToArray(), work.Tint);
                // The moss painted on bare rock stands on it as shells (SHELLS.md).
                skin.ShellStyle = Rendering.Shells.ShellStyles.RockMoss;
                // It stands in the world as it was made, and moss grows on nothing that faces the sky less than this (RockGrowth.glsl).
                skin.ShellFacing = 0.2f;
                return skin;
            }
            finally
            {
                work.Release();
            }
        }

        // Which points have an edge with a face on one side only, or more than two.
        private static bool[] OnOpenEdges(int count, List<int> triangles)
        {
            long[] edges = new long[triangles.Count];
            for (int i = 0; i < triangles.Count; i += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a = triangles[i + k], b = triangles[i + (k + 1) % 3];
                    edges[i + k] = a < b ? (long)a << 32 | (uint)b : (long)b << 32 | (uint)a;
                }
            }
            Array.Sort(edges);
            bool[] open = new bool[count];
            for (int i = 0; i < edges.Length; )
            {
                int j = i + 1;
                while (j < edges.Length && edges[j] == edges[i]) j++;
                // One face, or more than two: an edge on a saddle is shared by three faces here and one in the next tile, which sees it as open and holds it still.
                if (j - i != 2)
                {
                    open[(int)(edges[i] >> 32)] = true;
                    open[(int)(edges[i] & 0xFFFFFFFF)] = true;
                }
                i = j;
            }
            return open;
        }

        private static void Unburied(Work work, List<Vector3> points, List<int> triangles)
        {
            float[] ground = new float[points.Count];
            for (int i = 0; i < points.Count; i++) ground[i] = work.GroundDistance(points[i]);
            int w = 0;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                if (ground[triangles[i]] < BuriedBelow && ground[triangles[i + 1]] < BuriedBelow && ground[triangles[i + 2]] < BuriedBelow) continue;
                triangles[w++] = triangles[i];
                triangles[w++] = triangles[i + 1];
                triangles[w++] = triangles[i + 2];
            }
            triangles.RemoveRange(w, triangles.Count - w);
        }

        // After simplifying the points are new ones, but a pinned one never moved, so it's still on a tile's edge.
        private static bool IsPinned(Vector3 p, Vector2i tile)
        {
            float x = p.X - tile.X * TileSize, z = p.Z - tile.Y * TileSize;
            return x <= 1e-4f || z <= 1e-4f || x >= TileSize - Cell - 1e-4f || z >= TileSize - Cell - 1e-4f;
        }

        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------
        // A course: a band of one bed between two partings, which goes back or stands out as one.

        private struct Course
        {
            public int Bed, Index, Id;
            public float Lo, Hi;            // Its layers.
            public float Hard;              // -1 soft .. 1 hard.
            public float GapBelow, GapAbove;    // Half the opening of the partings under and over it, 0 if shut.
            public uint Hash;
        }

        // A block, as found from a point: everything about its shape, and the point's place in it.
        private struct Block
        {
            public float Field;
            public float Below, Above;      // Metres to its bottom and top, negative inside: the far side of each joint round it.
            public float Left, Right, Back, Front;
            public int Row, Start, End;
        }

        /// <summary>Everything one tile needs while it's made, on one thread.</summary>
        private sealed class Work
        {
            private readonly RockSkin _skin;
            private readonly Vector2i _tile;
            private readonly Vector2 _origin;
            private readonly Dictionary<int, float[]> _courses = new();

            // The ground's distance on a coarse grid, and the form of the mountain per column of it.
            private int _cx, _cy, _cz;
            private Vector3 _coarseOrigin;
            private float[] _ground = Array.Empty<float>();
            private Style[] _styles = Array.Empty<Style>();
            private float _bottom, _top;

            public Work(RockSkin skin, Vector2i tile)
            {
                _skin = skin;
                _tile = tile;
                _origin = new Vector2(tile.X, tile.Y) * TileSize;
            }

            /// <summary>Gives back what was rented for the tile.</summary>
            public void Release()
            {
                if (_ground.Length > 0) System.Buffers.ArrayPool<float>.Shared.Return(_ground);
                _ground = Array.Empty<float>();
            }

            /// <summary>Works out how far from the ground every point near the tile is. False if there's no skin in it.</summary>
            public bool Ground()
            {
                WorldData world = _skin._world;
                _cx = _cz = (int)MathF.Round((TileSize + Margin * 2f) / Coarse) + 1;
                float[] heights = new float[_cx * _cz];
                _styles = new Style[_cx * _cz];
                float low = float.MaxValue, high = float.MinValue;
                bool any = false;
                for (int k = 0; k < _cz; k++)
                {
                    for (int i = 0; i < _cx; i++)
                    {
                        float x = _origin.X - Margin + i * Coarse, z = _origin.Y - Margin + k * Coarse;
                        float h = world.Height.SampleCubic(x, z);
                        heights[i + _cx * k] = h;
                        _styles[i + _cx * k] = StyleAt(world, x, z);
                        any |= _skin.RockAt(x, z) > LeastWeight;
                        // Every column, rock or not, so that two tiles reach as high and as low over what they share.
                        low = MathF.Min(low, h);
                        high = MathF.Max(high, h);
                    }
                }
                if (!any) return false;

                // On the coarse grid, so that every tile reads the same slices.
                _bottom = MathF.Floor((low - 2f) / Coarse) * Coarse;
                // Over the highest ground by as much as a block can stand out of it, and some, or the top of a tower is cut off flat where the grid stops.
                _top = MathF.Ceiling((high + MostOut + 2.5f) / Coarse) * Coarse;
                _cy = (int)MathF.Round((_top - _bottom) / Coarse) + 1;
                _coarseOrigin = new Vector3(_origin.X - Margin, _bottom, _origin.Y - Margin);
                _ground = System.Buffers.ArrayPool<float>.Shared.Rent(_cx * _cy * _cz);

                // Slice by slice: the ground at this height is a contour, and a point's distance from the ground goes by how far down to it and how far across. Across is exact, to
                // where the contour crosses between columns, so that two tiles agree on every point they share: the crossings on each row, and then the nearest of them over every
                // row by the lower envelope of parabolas (Felzenszwalb and Huttenlocher), and the same the other way round.
                int n = _cx;
                var along = new float[n * n];
                var nearest = new float[n * n];
                var line = new float[n];
                var envelope = new float[n];
                var roots = new int[n];
                var bounds = new float[n + 1];
                for (int j = 0; j < _cy; j++)
                {
                    float y = _bottom + j * Coarse;
                    Array.Fill(nearest, Far);
                    for (int pass = 0; pass < 2; pass++)
                    {
                        // Along each line of columns, how far to the nearest crossing on it, squared, in cells.
                        for (int r = 0; r < n; r++)
                        {
                            float last = float.NegativeInfinity;
                            for (int i = 0; i < n; i++)
                            {
                                if (i > 0)
                                {
                                    float h0 = heights[Index(pass, i - 1, r)], h1 = heights[Index(pass, i, r)];
                                    if (h0 >= y != h1 >= y) last = i - 1 + (h0 - y) / (h0 - h1);
                                }
                                along[Index(pass, i, r)] = last == float.NegativeInfinity ? Far : (i - last) * (i - last);
                            }
                            last = float.PositiveInfinity;
                            for (int i = n - 1; i >= 0; i--)
                            {
                                if (i < n - 1)
                                {
                                    float h0 = heights[Index(pass, i, r)], h1 = heights[Index(pass, i + 1, r)];
                                    if (h0 >= y != h1 >= y) last = i + (h0 - y) / (h0 - h1);
                                }
                                if (last != float.PositiveInfinity) along[Index(pass, i, r)] = MathF.Min(along[Index(pass, i, r)], (last - i) * (last - i));
                            }
                        }
                        // And across the lines.
                        for (int i = 0; i < n; i++)
                        {
                            for (int r = 0; r < n; r++) line[r] = along[Index(pass, i, r)];
                            Envelope(line, envelope, roots, bounds, n);
                            for (int r = 0; r < n; r++) nearest[Index(pass, i, r)] = MathF.Min(nearest[Index(pass, i, r)], envelope[r]);
                        }
                    }

                    for (int k = 0; k < _cz; k++)
                    {
                        for (int i = 0; i < _cx; i++)
                        {
                            int c = i + _cx * k;
                            float down = y - heights[c];
                            float across = MathF.Min(MathF.Sqrt(nearest[c]) * Coarse, FarthestGround);
                            float up = MathF.Min(MathF.Abs(down), FarthestGround * 4f);
                            // The distance to a plane through both: exact on a slope, and whichever is nearer on the flat or a wall.
                            float d = up * across / MathF.Sqrt(up * up + across * across + 1e-12f);
                            _ground[i + _cx * (j + _cy * k)] = down >= 0f ? d : -d;
                        }
                    }
                }
                return true;
            }

            private const float Far = 1e12f;

            // The first pass runs along x and the second along z.
            private int Index(int pass, int along, int line) => pass == 0 ? along + _cx * line : line + _cx * along;

            // One dimension of the squared distance transform: the lower envelope of the parabolas rooted at each entry.
            private static void Envelope(float[] f, float[] d, int[] root, float[] bound, int n)
            {
                int k = -1;
                for (int q = 0; q < n; q++)
                {
                    if (f[q] >= Far) continue;
                    while (k >= 0)
                    {
                        int r = root[k];
                        float s = ((f[q] + q * q) - (f[r] + r * r)) / (2f * (q - r));
                        if (s > bound[k]) { k++; root[k] = q; bound[k] = s; break; }
                        k--;
                    }
                    if (k < 0) { k = 0; root[0] = q; bound[0] = float.NegativeInfinity; }
                    bound[k + 1] = float.PositiveInfinity;
                }
                if (k < 0)
                {
                    Array.Fill(d, Far, 0, n);
                    return;
                }
                int at = 0;
                for (int q = 0; q < n; q++)
                {
                    while (bound[at + 1] < q) at++;
                    int r = root[at];
                    d[q] = (q - r) * (q - r) + f[r];
                }
            }

            private static Style StyleAt(WorldData world, float x, float z)
            {
                FormBlend blend = MountainForms.At(world, x, z);
                float b = 0, h = 0, co = 0, bo = 0, t = 0, ch = 0, g = 0, m = 0, ms = 0;
                for (int f = 0; f < MountainForms.Count; f++)
                {
                    float w = blend[(MountainForm)f];
                    if (w <= 0f) continue;
                    Style s = StyleOf((MountainForm)f);
                    b += s.Base * w; h += s.HardOut * w; co += s.CourseOut * w; bo += s.BlockOut * w; t += s.Tilt * w; ch += s.Chip * w; g += s.Gap * w;
                    m += s.Mass * w; ms += s.MassSize * w;
                }
                return new Style(b, h, co, bo, t, ch, g, StyleOf(blend.Strongest).Group, m, MathF.Max(ms, 1f));
            }

            /// <summary>Metres from the ground, positive out of it, read between the coarse grid's points.</summary>
            public float GroundDistance(Vector3 p)
            {
                Vector3 g = (p - _coarseOrigin) / Coarse;
                g.X = Math.Clamp(g.X, 0f, _cx - 1.001f);
                g.Y = Math.Clamp(g.Y, 0f, _cy - 1.001f);
                g.Z = Math.Clamp(g.Z, 0f, _cz - 1.001f);
                int i = (int)g.X, j = (int)g.Y, k = (int)g.Z;
                float fx = g.X - i, fy = g.Y - j, fz = g.Z - k;
                int a = i + _cx * (j + _cy * k);
                int sy = _cx, sz = _cx * _cy;
                float c00 = Lerp(_ground[a], _ground[a + 1], fx);
                float c10 = Lerp(_ground[a + sy], _ground[a + sy + 1], fx);
                float c01 = Lerp(_ground[a + sz], _ground[a + sz + 1], fx);
                float c11 = Lerp(_ground[a + sy + sz], _ground[a + sy + sz + 1], fx);
                return Lerp(Lerp(c00, c10, fy), Lerp(c01, c11, fy), fz);
            }

            private Style StyleNear(float x, float z)
            {
                // By the world's grid, never the tile's, so that two tiles read the same column for a point they share.
                int i = Math.Clamp((int)MathF.Floor(x / Coarse + 0.5f) - (int)MathF.Round(_coarseOrigin.X / Coarse), 0, _cx - 1);
                int k = Math.Clamp((int)MathF.Floor(z / Coarse + 0.5f) - (int)MathF.Round(_coarseOrigin.Z / Coarse), 0, _cz - 1);
                return _styles[i + _cx * k];
            }

            /// <summary>Fills the tile's grid and meshes it.</summary>
            public void March(List<Vector3> points, List<int> triangles, List<Vector3i> cells)
            {
                int nx = Cells + 2, nz = Cells + 2;
                float bottom = MathF.Floor((_bottom + Coarse) / Cell) * Cell;
                float top = MathF.Ceiling((_top - Coarse) / Cell) * Cell;
                int ny = (int)MathF.Round((top - bottom) / Cell) + 1;
                Vector3 origin = new Vector3(_origin.X - Cell, bottom, _origin.Y - Cell);
                float[] grid = System.Buffers.ArrayPool<float>.Shared.Rent(nx * ny * nz);
                for (int z = 0; z < nz; z++)
                {
                    for (int x = 0; x < nx; x++)
                    {
                        float px = origin.X + x * Cell, pz = origin.Z + z * Cell;
                        float rock = _skin.RockAt(px, pz);
                        Style style = StyleNear(px, pz);
                        float most = style.Most + style.Tilt * 2f + Rough + 0.15f;
                        for (int y = 0; y < ny; y++)
                        {
                            int index = x + nx * (y + ny * z);
                            Vector3 p = new Vector3(px, origin.Y + y * Cell, pz);
                            float weight = rock <= LeastWeight ? 0f : _skin.WeightAt(p);
                            if (weight <= LeastWeight)
                            {
                                grid[index] = 1f;
                                continue;
                            }
                            float s = GroundDistance(p);
                            if (s < -Core - 0.15f) grid[index] = MathF.Max(s + Core, -(s + Inner));
                            else if (s > most) grid[index] = s - most + 0.05f;
                            else grid[index] = _skin.Field(p, s, weight, style, this, out _);
                        }
                    }
                }
                SurfaceNets.Extract(grid, nx, ny, nz, origin, Cell, points, triangles, cells);
                System.Buffers.ArrayPool<float>.Shared.Return(grid);
            }

            /// <summary>The skin's signed distance anywhere near the tile.</summary>
            public float Distance(Vector3 p)
            {
                float weight = _skin.WeightAt(p);
                if (weight <= LeastWeight) return 1f;
                float s = GroundDistance(p);
                if (s < -Core - 0.15f) return MathF.Max(s + Core, -(s + Inner));
                return _skin.Field(p, s, weight, StyleNear(p.X, p.Z), this, out _);
            }

            /// <summary>How light the block a point is on is, as no two blocks weathered alike.</summary>
            public float Tint(Vector3 p, Vector3 normal)
            {
                Vector3 inside = p - normal * 0.1f;
                float weight = _skin.WeightAt(inside);
                if (weight <= LeastWeight) return 1f;
                _skin.Field(inside, GroundDistance(inside), weight, StyleNear(inside.X, inside.Z), this, out uint block);
                return Lerp(0.84f, 1.1f, Unit(Mix(block ^ 0x3C6EF372u)));
            }

            private readonly Dictionary<int, GroupFrame> _frames = new();
            private readonly Dictionary<long, RowFrame> _rows = new();

            public GroupFrame Frame(int group)
            {
                if (!_frames.TryGetValue(group, out GroupFrame frame)) _frames[group] = frame = _skin.FrameOf(group);
                return frame;
            }

            public RowFrame Row(in GroupFrame frame, int group, int row)
            {
                long key = (long)group << 32 | (uint)row;
                if (!_rows.TryGetValue(key, out RowFrame made)) _rows[key] = made = RowOf(frame, group, row, _skin._breaks);
                return made;
            }

            /// <summary>Where the partings of a bed are, from its foot, in layers.</summary>
            public float[] CoursesOf(int bed, float thick)
            {
                if (_courses.TryGetValue(bed, out float[]? known)) return known;
                Breaks breaks = _skin._breaks;
                var bounds = new List<float> { 0f };
                float at = 0f;
                for (int k = 0; ; k++)
                {
                    float t = Lerp(breaks.CourseLeast, breaks.CourseMost, Unit(Hash(bed, 33, k))) * _skin._layerScale;
                    if (at + t >= thick - breaks.CourseLeast * 0.5f * _skin._layerScale || k >= 62)
                    {
                        bounds.Add(thick);
                        break;
                    }
                    at += t;
                    bounds.Add(at);
                }
                float[] made = bounds.ToArray();
                _courses[bed] = made;
                return made;
            }
        }

        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------
        // The field.

        /// <summary>
        /// The skin's signed distance at a point <paramref name="s"/> metres from the ground: the block it's in, and the blocks across any joint near it, over a core just inside
        /// the ground that closes every joint, hollow below that. <paramref name="block"/> says which block it is.
        /// </summary>
        private float Field(Vector3 p, float s, float weight, in Style style, Work work, out uint block)
        {
            float a = Vector3.Dot(p, _along);
            float c = Vector3.Dot(p, _across);
            float layer = _geology.LayerAt(p.X, p.Y, p.Z) + (Noise(a * 0.29f + p.Y * 0.07f, c * 0.29f + p.Y * 0.07f) - 0.5f) * _sheeted;
            Vector3 q = p + _across * (Noise(a * 0.075f + 11.3f, p.Y * 0.12f + 11.3f) - 0.5f) * 1.2f
                          + _along * (Noise(c * 0.075f - 5.9f, p.Y * 0.12f - 5.9f) - 0.5f) * 1.2f;
            float rough = (Noise(a * 0.8f + 3.1f, p.Y * 0.8f + c * 0.31f) + Noise(c * 0.8f - 7.3f, p.Y * 0.8f - a * 0.29f) - 1f) * Rough;

            Course course = CourseAt(layer, style, work);
            float field;
            if (_columns)
            {
                field = Column(q, p, layer, s + rough, weight, style, course, work, out block);
            }
            else
            {
                int group = GroupOf(course, style);
                Block own = BlockAt(q, layer, s + rough, weight, style, course, group, int.MinValue, int.MinValue, out block, work);
                field = own.Field;

                // Across whichever joint of each set is nearest, if it's near enough.
                if (MathF.Max(own.Left, own.Right) > -Reach)
                {
                    int next = own.Left > own.Right ? own.Start - 1 : own.End + 1;
                    field = MathF.Min(field, BlockAt(q, layer, s + rough, weight, style, course, group, own.Row, next, out _, work).Field);
                }
                if (MathF.Max(own.Back, own.Front) > -Reach)
                {
                    int row = own.Back > own.Front ? own.Row - 1 : own.Row + 1;
                    field = MathF.Min(field, BlockAt(q, layer, s + rough, weight, style, course, group, row, int.MinValue, out _, work).Field);
                }
                if (MathF.Max(own.Below, own.Above) > -Reach)
                {
                    Course other = own.Below > own.Above ? CourseAt(course.Lo - 0.01f, style, work) : CourseAt(course.Hi + 0.01f, style, work);
                    field = MathF.Min(field, BlockAt(q, layer, s + rough, weight, style, other, GroupOf(other, style), int.MinValue, int.MinValue, out _, work).Field);
                }
            }

            field = MathF.Min(field, s + Core);
            field = MathF.Max(field, -(s + Inner));
            field = MathF.Max(field, (LeastWeight - weight) * 4f);
            // Never over the line to anyone standing north.
            return MathF.Max(field, p.Y - SightAt(p.X, p.Z));
        }

        private static int GroupOf(in Course course, in Style style) => style.Group <= 1 ? course.Id : -(course.Bed * 64 + course.Index / style.Group) - 1;

        private Course CourseAt(float layer, in Style style, Work work)
        {
            _geology.BedAt(layer, out float since, out float until, out int bed);
            float foot = layer - since;
            float thick = since + until;
            float[] bounds = work.CoursesOf(bed, thick);
            int k = 0;
            while (k < bounds.Length - 2 && since >= bounds[k + 1]) k++;
            int count = bounds.Length - 1;

            var course = new Course
            {
                Bed = bed,
                Index = k,
                Id = bed * 64 + k,
                Lo = foot + bounds[k],
                Hi = foot + bounds[k + 1],
            };
            course.Hash = Hash(course.Id, 7, 11);
            course.Hard = Math.Clamp((_geology.ToneAt((course.Lo + course.Hi) * 0.5f).W - 0.575f) / 0.325f, -1f, 1f);

            // A bed's own partings are always open; the courses in it have parted where they have.
            course.GapBelow = k == 0 ? PartingGap(bed, 0, style, true) : PartingGap(bed, k, style, false);
            if (k == count - 1)
            {
                _geology.BedAt(foot + thick + 0.01f, out _, out _, out int above);
                course.GapAbove = PartingGap(above, 0, style, true);
            }
            else
            {
                course.GapAbove = PartingGap(bed, k + 1, style, false);
            }
            return course;
        }

        private float PartingGap(int bed, int index, in Style style, bool open)
        {
            uint h = Hash(bed, 41, index);
            if (!open && Unit(h) >= _breaks.Open) return 0f;
            return style.Gap * Lerp(0.6f, 1.25f, Unit(h >> 16));
        }

        // A joint a third of a spacing either side of where it would evenly be, and which gap between them a coordinate falls in.
        private static float JointAt(int group, int set, int k) => k + (Unit(Hash(group, set, k)) - 0.5f) * 0.6f;

        private static int GapOf(float t, int group, int set)
        {
            int k = (int)MathF.Floor(t);
            if (t < JointAt(group, set, k)) return k - 1;
            if (t < JointAt(group, set, k + 1)) return k;
            return k + 1;
        }

        private static bool Shutting(int group, int row, int joint) => Unit(Hash(group, row * 3 + 1, joint)) < Shut;

        /// <summary>
        /// The block of a course a point is in, or the one in a given row or at a given column, and its field there: the face it stands out to, cut by its joints and partings
        /// and broken off at its edges.
        /// </summary>
        private readonly record struct GroupFrame(Vector3 A, Vector3 C, float Spacing, float Phase);
        private readonly record struct RowFrame(Vector3 Across, float Spacing, float Phase);

        private GroupFrame FrameOf(int group)
        {
            uint groupHash = Hash(group, 17, 3);
            float turn = (Unit(groupHash >> 20) - 0.5f) * 0.28f;
            Vector3 a = _along * MathF.Cos(turn) + _across * MathF.Sin(turn);
            return new GroupFrame(a, Vector3.Cross(_bedNormal, a), Lerp(_breaks.JointLeast, _breaks.JointMost, Unit(groupHash)) * 0.85f, Unit(groupHash >> 8));
        }

        private static RowFrame RowOf(in GroupFrame frame, int group, int row, Breaks breaks)
        {
            uint rowHash = Hash(group, row, 21);
            float lean = (Unit(rowHash >> 8) - 0.5f) * 0.4f;
            return new RowFrame(frame.A * MathF.Cos(lean) + frame.C * MathF.Sin(lean), Lerp(breaks.JointLeast, breaks.JointMost, Unit(rowHash)) * 1.2f, Unit(rowHash >> 16));
        }

        private Block BlockAt(Vector3 q, float layer, float s, float weight, in Style style, in Course course, int group, int row, int column, out uint id, Work work)
        {
            GroupFrame frame = work.Frame(group);
            float sv = frame.Spacing;
            float v = Vector3.Dot(q, frame.C) / sv + frame.Phase;
            if (row == int.MinValue) row = GapOf(v, group, 61);

            RowFrame rowFrame = work.Row(frame, group, row);
            float su = rowFrame.Spacing;
            float u = Vector3.Dot(q, rowFrame.Across) / su + rowFrame.Phase;
            if (column == int.MinValue) column = GapOf(u, group, row * 3 + 2);

            int start = column, end = column;
            for (int i = 0; i < 2 && Shutting(group, row, start); i++) start--;
            for (int i = 0; i < 2 && Shutting(group, row, end + 1); i++) end++;

            float u0 = JointAt(group, row * 3 + 2, start), u1 = JointAt(group, row * 3 + 2, end + 1);
            float v0 = JointAt(group, 61, row), v1 = JointAt(group, 61, row + 1);

            var block = new Block { Row = row, Start = start, End = end };
            block.Left = (u0 - u) * su;
            block.Right = (u - u1) * su;
            block.Back = (v0 - v) * sv;
            block.Front = (v - v1) * sv;
            block.Below = (course.Lo - layer) / _layerScale;
            block.Above = (layer - course.Hi) / _layerScale;

            // A block of a group of courses is the same block in each, as the cleft beside it is: only the course's own share changes from one to the next.
            uint blockHash = Hash(group, row, start);
            id = blockHash ^ course.Hash;
            float halfU = (u1 - u0) * su * 0.5f, halfV = (v1 - v0) * sv * 0.5f, halfL = (course.Hi - course.Lo) / _layerScale * 0.5f;
            float xu = (u - (u0 + u1) * 0.5f) * su, xv = (v - (v0 + v1) * 0.5f) * sv, xl = (layer - (course.Lo + course.Hi) * 0.5f) / _layerScale;

            float gapLeft = style.Gap * Lerp(0.7f, 1.4f, Unit(Hash(group, row * 3 + 9, start)));
            float gapRight = style.Gap * Lerp(0.7f, 1.4f, Unit(Hash(group, row * 3 + 9, end + 1)));
            float gapBack = style.Gap * Lerp(0.7f, 1.4f, Unit(Hash(group, 67, row)));
            float gapFront = style.Gap * Lerp(0.7f, 1.4f, Unit(Hash(group, 67, row + 1)));

            Vector3 middle = q - rowFrame.Across * xu - frame.C * xv - _bedNormal * xl;
            float swell = style.Mass * FloraNoise.Value(middle / style.MassSize, _seed);
            block.Field = Shape(s, weight, style, course, blockHash, id, swell, xu, xv, xl, halfU, halfV, halfL,
                block.Left + gapLeft, block.Right + gapRight, block.Back + gapBack, block.Front + gapFront, block.Below + course.GapBelow, block.Above + course.GapAbove,
                course.GapBelow > 0f, course.GapAbove > 0f);
            return block;
        }


        /// <summary>
        /// A block's field from how far a point is past each of its sides, the gap of its joint taken off: its face, which stands out of the ground by the block's own amount and
        /// turns its own way, and its edges broken off, each by its own amount.
        /// </summary>
        private static float Shape(float s, float weight, in Style style, in Course course, uint blockHash, uint id, float swell, float xu, float xv, float xl, float halfU, float halfV, float halfL,
            float left, float right, float back, float front, float below, float above, bool openBelow, bool openAbove)
        {
            float hard = course.Hard * 0.5f + 0.5f;
            float outward = style.Base + style.HardOut * hard + style.CourseOut * Unit(course.Hash >> 8) + style.BlockOut * Sq(Unit(blockHash)) + swell;
            outward = Math.Clamp(outward, 0.1f, MostOut);

            // Turned its own way, never so far that a corner goes back into the ground.
            float tu = (Unit(Mix(id ^ 0x68E31DA4u)) - 0.5f) * 2f * style.Tilt;
            float tv = (Unit(Mix(id ^ 0xB5297A4Du)) - 0.5f) * 2f * style.Tilt;
            float tl = (Unit(Mix(id ^ 0x1B56C4E9u)) - 0.5f) * 2f * style.Tilt;
            float swing = MathF.Abs(tu) * halfU + MathF.Abs(tv) * halfV + MathF.Abs(tl) * halfL;
            float room = outward - 0.08f;
            if (swing > room)
            {
                float k = room / swing;
                tu *= k; tv *= k; tl *= k;
            }
            float stand = Lerp(Sunk, outward, weight);
            float face = s - stand - (tu * xu + tv * xv + tl * xl);

            float field = MathF.Max(face, MathF.Max(MathF.Max(left, right), MathF.Max(MathF.Max(back, front), MathF.Max(below, above))));

            // Broken edges: a cut across each edge of the face, some hardly anything and some taking a good bite.
            float chip = style.Chip * 2.2f;
            field = MathF.Max(field, Bevel(face, left, chip * Sq(Unit(Mix(id ^ 0x2545F491u)))));
            field = MathF.Max(field, Bevel(face, right, chip * Sq(Unit(Mix(id ^ 0x9E3779B9u)))));
            field = MathF.Max(field, Bevel(face, back, chip * Sq(Unit(Mix(id ^ 0x85EBCA6Bu)))));
            field = MathF.Max(field, Bevel(face, front, chip * Sq(Unit(Mix(id ^ 0xC2B2AE35u)))));
            if (openAbove) field = MathF.Max(field, Bevel(face, above, chip * 0.8f * Sq(Unit(Mix(id ^ 0x27D4EB2Fu)))));
            if (openBelow) field = MathF.Max(field, Bevel(face, below, chip * 0.5f * Sq(Unit(Mix(id ^ 0x165667B1u)))));
            return field;
        }

        private static float Bevel(float face, float side, float size) => (face + side + size) * 0.70710678f;

        // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------
        // Basalt: six-sided columns, each with its own cross joints.

        private float Column(Vector3 q, Vector3 p, float layer, float s, float weight, in Style style, in Course course, Work work, out uint id)
        {
            uint flowHash = Hash(course.Bed, 17, 3);
            float width = Lerp(_breaks.JointLeast, _breaks.JointMost, Unit(flowHash));
            Vector2 h = new Vector2(Vector3.Dot(q, _along), Vector3.Dot(q, _across)) / width;
            Vector2 centre = HexCentre(h);
            float field = ColumnAt(centre, h, width, q, layer, s, weight, style, course, out id, out Vector2 outward, out float side, out float joint);

            // The column across the nearest side, and the piece of this one across the nearest cross joint.
            if (side > -Reach)
            {
                field = MathF.Min(field, ColumnAt(centre + outward, h, width, q, layer, s, weight, style, course, out _, out _, out _, out _));
            }
            if (joint > -Reach)
            {
                field = MathF.Min(field, ColumnAt(centre, h, width, q, layer, s, weight, style, course, out _, out _, out _, out _, true));
            }
            return field;
        }

        private float ColumnAt(Vector2 centre, Vector2 h, float width, Vector3 q, float layer, float s, float weight, in Style style, in Course course, out uint id,
            out Vector2 outward, out float side, out float joint, bool nextPiece = false)
        {
            Vector2 off = h - centre;
            float d0 = off.X;
            float d1 = Vector2.Dot(off, new Vector2(0.5f, 0.8660254f));
            float d2 = Vector2.Dot(off, new Vector2(0.5f, -0.8660254f));
            outward = new Vector2(MathF.Sign(d0), 0f);
            float most = MathF.Abs(d0);
            if (MathF.Abs(d1) > most) { most = MathF.Abs(d1); outward = MathF.Sign(d1) * new Vector2(0.5f, 0.8660254f); }
            if (MathF.Abs(d2) > most) { most = MathF.Abs(d2); outward = MathF.Sign(d2) * new Vector2(0.5f, -0.8660254f); }
            side = (most - 0.5f) * width;

            Vector2i cell = new Vector2i((int)MathF.Round(centre.X * 2f), (int)MathF.Round(centre.Y * 1.1547005f));
            uint columnHash = Hash(cell.X, cell.Y, course.Bed);
            float spacing = Lerp(0.6f, 1.5f, Unit(columnHash));
            float z = Vector3.Dot(q, _bedNormal) / spacing + Unit(columnHash >> 16);
            int piece = (int)MathF.Floor(z);
            float fz = z - piece;
            if (nextPiece) piece += fz < 0.5f ? -1 : 1;
            // Metres past the piece's foot and head, negative inside.
            float bottom = (piece - z) * spacing;
            float topOf = (z - piece - 1f) * spacing;
            joint = MathF.Max(bottom, topOf);

            id = Hash(cell.X, cell.Y * 64 + piece, course.Bed);
            float gap = style.Gap * Lerp(0.7f, 1.3f, Unit(Hash(cell.X, cell.Y, 5)));
            float pieceGap = style.Gap * 0.8f;
            float below = MathF.Max(bottom + pieceGap, (course.Lo - layer) / _layerScale + course.GapBelow);
            float above = MathF.Max(topOf + pieceGap, (layer - course.Hi) / _layerScale + course.GapAbove);
            // A column's sides are one joint all round: the face it shows is cut by each of them as a box's would be by one.
            float sides = side + gap;
            float half = width * 0.5f;
            Vector3 middle = _along * (centre.X * width) + _across * (centre.Y * width) + _bedNormal * Vector3.Dot(q, _bedNormal);
            float swell = style.Mass * FloraNoise.Value(middle / style.MassSize, _seed);
            return Shape(s, weight, style, course, Hash(cell.X, cell.Y, course.Bed + 7), id, swell, 0f, 0f, 0f, half, half, spacing * 0.5f, sides, sides, sides, sides, below, above, true, true);
        }

        private static Vector2 HexCentre(Vector2 h)
        {
            Vector2 s = new Vector2(1f, 1.7320508f);
            Vector2 a = Mod(h, s) - s * 0.5f;
            Vector2 b = Mod(h - s * 0.5f, s) - s * 0.5f;
            return Vector2.Dot(a, a) < Vector2.Dot(b, b) ? h - a : h - b;
        }

        private static Vector2 Mod(Vector2 x, Vector2 y) => new Vector2(x.X - y.X * MathF.Floor(x.X / y.X), x.Y - y.Y * MathF.Floor(x.Y / y.Y));

        private static float Sq(float x) => x * x;

        private static uint Hash(int a, int b, int c) => RockHash.Hash(a, b, c);
        private static uint Mix(uint h) => RockHash.Mix(h);
        private static float Unit(uint h) => RockHash.Unit(h);
        private static float Noise(float x, float y) => RockHash.Noise(x, y);
    }
}
