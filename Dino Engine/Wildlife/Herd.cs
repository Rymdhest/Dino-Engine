using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using Rig = Dino_Engine.Animation.Rig;

namespace Dino_Engine.Wildlife
{
    /// <summary>What a giant's body is like, as far as where it can stand and what it can reach: read off its rig, at the size it's drawn.</summary>
    public readonly struct GiantShape
    {
        public readonly float Scale;
        public readonly float Middle;       // Metres from where the body stands, under its hips, forward to the middle of its feet, which is what it turns about.
        public readonly float HalfWidth;    // From its middle line out to the outside of a foot.
        public readonly float Front;        // From its middle forward to the front of its forefeet,
        public readonly float Back;         // and back to the back of its hind feet.
        public readonly float FootX, HindZ, ForeZ;  // Where its feet stand at their ease, from where the body stands.
        public readonly Vector3 NeckRoot;   // In its own space, from where it stands.
        public readonly Vector3 RestMouth;  // Where its mouth is at its ease.
        public readonly float Reach;        // From the root of its neck to its mouth, stretched out.
        public readonly float TailBack;     // Metres behind where it stands that its tail ends, which is as far round as a turn sweeps it.

        private GiantShape(Rig rig, float scale)
        {
            SaurianFrame frame = rig.Frame ?? throw new ArgumentException("A giant is a dinosaur.");
            Vector3 hip = rig.Pivots[(int)BoneType.UpperLegL];
            Vector3 shoulder = rig.Pivots[(int)BoneType.UpperArmL];
            float foot = frame.Thickness[(int)BoneType.FootL];
            Scale = scale;
            Middle = (hip.Z + shoulder.Z) * 0.5f * scale;
            FootX = MathF.Max(MathF.Abs(hip.X), MathF.Abs(shoulder.X)) * scale;
            HalfWidth = FootX + foot * scale;
            HindZ = hip.Z * scale;
            ForeZ = shoulder.Z * scale;
            Front = ForeZ - Middle + foot * scale;
            Back = Middle - HindZ + foot * scale;
            NeckRoot = rig.Pivots[(int)BoneType.Neck] * scale;
            RestMouth = (rig.Pivots[(int)BoneType.Head] + frame.Mouth) * scale;
            TailBack = MathF.Max(-frame.TailEnd.Z, 0f) * scale;

            float reach = frame.Mouth.Length;
            BoneType[] neck = frame.Neck;
            for (int i = 0; i < neck.Length; i++)
            {
                BoneType next = i + 1 < neck.Length ? neck[i + 1] : BoneType.Head;
                reach += (rig.Pivots[(int)next] - rig.Pivots[(int)neck[i]]).Length;
            }
            Reach = reach * scale;
        }

        public static GiantShape Of(Rig rig, float scale) => new GiantShape(rig, scale);

        /// <summary>As high as its mouth goes, with its neck stretched up as far as it will.</summary>
        public float HighestMouth => NeckRoot.Y + Reach * 0.95f;

        /// <summary>The nearest its mouth comes to the root of its neck, as a share of its reach: a sauropod's neck is a long reach and not a fold.</summary>
        public const float Nearest = 0.76f;

        /// <summary>As low as it likes to crop, with its neck lowered to about level and no further.</summary>
        public float LowestBite => NeckRoot.Y * 0.85f;

        /// <summary>How much room a foot wants round where it stands at its ease: itself, and as far again as a stride puts it ahead of there.</summary>
        public float FootRoom => HalfWidth - FootX + 0.8f * Scale;

        /// <summary>How much room its middle wants, walking, for every foot to have its own whichever way it faces, so that anywhere it can walk it can turn round.</summary>
        public float WalkRoom => MathF.Sqrt(FootX * FootX + MathF.Max(ForeZ - Middle, Middle - HindZ) * MathF.Max(ForeZ - Middle, Middle - HindZ)) + FootRoom + 0.4f;
    }

    /// <summary>A tree a giant can eat from: where its trunk stands, and where its crown is.</summary>
    public readonly record struct Browse(Vector2 Trunk, float Ground, float Height, float CrownBase, float CrownRadius)
    {
        public float Top => Ground + Height;

        /// <summary>How far out from the trunk the crown reaches at a height above the ground, taken as an ellipsoid from its base to its top.</summary>
        public float RadiusAt(float height)
        {
            float middle = (CrownBase + Height) * 0.5f;
            float half = MathF.Max((Height - CrownBase) * 0.5f, 0.5f);
            float t = (height - middle) / half;
            return CrownRadius * MathF.Sqrt(MathF.Max(1f - t * t, 0f));
        }
    }

    /// <summary>
    /// Where a herd can go and what it can eat there, worked out once from the world. The ground is a grid of how far each place is from anything a giant can't stand on or in: water, slopes too
    /// steep, rock, anything solid, and anywhere outside its range. A giant's middle needs as much room as it is wide, and paths are found through that, keeping out from under the trees, as a neck
    /// pushed through a crown is one going through it.
    /// </summary>
    public sealed class HerdLand
    {
        public const float Cell = 2f;
        private const float MostSlope = 0.42f;      // Rise over run that a giant won't walk up.
        private const float MostWater = 0.15f;      // Metres deep it won't stand in.
        private const float CanopyCost = 2.5f;      // How much further a stretch under trees counts as.
        private const float AvoidCost = 8f;         // And a stretch close by another of the herd, which it would only have to wait for.
        private const float ShortestTree = 8f;      // Metres, at the size it was grown: anything lower isn't worth stretching a neck for.

        /// <summary>Metres of room more than it needs that it wants at each foot of anywhere it means to stay, so that it can always shift a little from there.</summary>
        public const float Spare = 0.3f;

        // What's built is in the way whether it's solid or not, and of what grows, only what's solid: a tree's trunk, and not the room left round it for its crown.
        private const PlacementLayer Built = PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Fence | PlacementLayer.Keepout | PlacementLayer.Field;

        public readonly WorldData World;
        public readonly List<Browse> Trees = new();
        private readonly Func<Vector2, bool> _within;
        private readonly Vector2 _origin;
        private readonly int _nx, _nz;
        private readonly float[] _clear;

        // For finding paths, kept between searches, which happen a few times a minute.
        private readonly float[] _cost;
        private readonly int[] _from;
        private readonly int[] _stamp;
        private int _search;
        private readonly PriorityQueue<int, float> _open = new();
        private readonly List<int> _cells = new();

        /// <param name="min">The corner of everything the herd could ever reach.</param>
        /// <param name="within">Whether a place is in the herd's range: outside it is as good as a wall.</param>
        public HerdLand(WorldData world, Vector2 min, Vector2 max, Func<Vector2, bool> within)
        {
            World = world;
            _within = within;
            _origin = min;
            _nx = Math.Max((int)MathF.Ceiling((max.X - min.X) / Cell), 1);
            _nz = Math.Max((int)MathF.Ceiling((max.Y - min.Y) / Cell), 1);
            _clear = new float[_nx * _nz];
            _cost = new float[_clear.Length];
            _from = new int[_clear.Length];
            _stamp = new int[_clear.Length];

            for (int z = 0; z < _nz; z++)
            {
                for (int x = 0; x < _nx; x++) _clear[x + z * _nx] = Blocked(CentreOf(x, z)) ? 0f : float.MaxValue;
            }
            Spread();
            FindTrees();
        }

        private Vector2 CentreOf(int x, int z) => _origin + new Vector2(x + 0.5f, z + 0.5f) * Cell;

        private bool Blocked(Vector2 at)
        {
            if (!World.InBounds(at) || !_within(at)) return true;
            if (World.WaterDepthAt(at) > MostWater) return true;
            if (World.Height.SlopeAt(at) > MostSlope || World.Rock.Sample(at) > 0.5f) return true;
            // All of the cell, out to its corners: a thin trunk can stand between four cells' middles, nearer to none of them than half a cell.
            var here = Footprint.Circle(at, Cell * 0.71f);
            return World.Placement.OverlapsSolid(here) || World.Placement.Overlaps(here, Built);
        }

        // How far every cell is from the nearest blocked one, by two sweeps of a chamfer: near enough to a true distance at this size, and cheap.
        private void Spread()
        {
            const float Diagonal = Cell * 1.4142135f;
            for (int z = 0; z < _nz; z++)
            {
                for (int x = 0; x < _nx; x++)
                {
                    int i = x + z * _nx;
                    float d = _clear[i];
                    if (x > 0) d = MathF.Min(d, _clear[i - 1] + Cell);
                    if (z > 0) d = MathF.Min(d, _clear[i - _nx] + Cell);
                    if (x > 0 && z > 0) d = MathF.Min(d, _clear[i - _nx - 1] + Diagonal);
                    if (x + 1 < _nx && z > 0) d = MathF.Min(d, _clear[i - _nx + 1] + Diagonal);
                    _clear[i] = d;
                }
            }
            for (int z = _nz - 1; z >= 0; z--)
            {
                for (int x = _nx - 1; x >= 0; x--)
                {
                    int i = x + z * _nx;
                    float d = _clear[i];
                    if (x + 1 < _nx) d = MathF.Min(d, _clear[i + 1] + Cell);
                    if (z + 1 < _nz) d = MathF.Min(d, _clear[i + _nx] + Cell);
                    if (x + 1 < _nx && z + 1 < _nz) d = MathF.Min(d, _clear[i + _nx + 1] + Diagonal);
                    if (x > 0 && z + 1 < _nz) d = MathF.Min(d, _clear[i + _nx - 1] + Diagonal);
                    _clear[i] = d;
                }
            }
        }

        // Every tree in reach with a living crown tall enough to bother with, with where its crown is, worked out from its seed as it would be grown.
        private void FindTrees()
        {
            Vector2 max = _origin + new Vector2(_nx, _nz) * Cell;
            foreach (WorldObject tree in World.Objects.All())
            {
                Vector2 at = tree.Position.Xz;
                if (at.X < _origin.X || at.Y < _origin.Y || at.X > max.X || at.Y > max.Y) continue;
                string species = NatureCatalog.SpeciesOf(World.Objects.AssetIds[tree.Asset], out ulong seed);
                if (!FloraCatalog.TryGet(species, out PlantSpecies plant) || !plant.TryCrown(seed, out float height, out float crownBase, out float crownRadius)) continue;
                float scale = tree.Scale.Y;
                if (height * scale < ShortestTree) continue;
                Trees.Add(new Browse(at, tree.Position.Y, height * scale, crownBase * scale, crownRadius * scale));
            }
        }

        /// <summary>
        /// How far a place is from anything a giant can't stand on, in metres, between the cells' middles, so that it changes no faster than the distance does and a margin added to it is one. Nothing at
        /// all outside the grid.
        /// </summary>
        public float Clear(Vector2 at)
        {
            float fx = (at.X - _origin.X) / Cell - 0.5f;
            float fz = (at.Y - _origin.Y) / Cell - 0.5f;
            int x = (int)MathF.Floor(fx), z = (int)MathF.Floor(fz);
            if (x < -1 || z < -1 || x >= _nx || z >= _nz) return 0f;
            float tx = fx - x, tz = fz - z;
            float a = Cleared(x, z), b = Cleared(x + 1, z), c = Cleared(x, z + 1), d = Cleared(x + 1, z + 1);
            return MathHelper.Lerp(MathHelper.Lerp(a, b, tx), MathHelper.Lerp(c, d, tx), tz);
        }

        private float Cleared(int x, int z) => x < 0 || z < 0 || x >= _nx || z >= _nz ? 0f : _clear[x + z * _nx];

        public bool Within(Vector2 at) => _within(at);

        /// <summary>How much of the sky trees hide here, from 0 in the open to 1 in a wood.</summary>
        public float Canopy(Vector2 at) => World.Canopy.Sample(at);

        /// <summary>
        /// Whether a body this shape can stand here, facing this way: each of its four feet clear of anything in the way by more than the foot is wide, and the ground under its belly clear of anything
        /// tall. <paramref name="give"/> is how much less room it will put up with, or, below nothing, how much more it wants.
        /// </summary>
        public bool CanStand(in GiantShape shape, Vector2 middle, float yaw, float give = 0f) => Slack(shape, middle, yaw) >= -give;

        /// <summary>How much more room than it needs the body has standing here, at the foot or belly with least, in metres: below nothing, it can't stand here.</summary>
        public float Slack(in GiantShape shape, Vector2 middle, float yaw)
        {
            Vector2 forward = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
            Vector2 left = new Vector2(forward.Y, -forward.X);
            Vector2 root = middle - forward * shape.Middle;
            float slack = Clear(middle) - shape.FootX * 0.8f;
            for (int i = 0; i < 4; i++)
            {
                Vector2 at = root + left * (i % 2 == 0 ? shape.FootX : -shape.FootX) + forward * (i < 2 ? shape.HindZ : shape.ForeZ);
                slack = MathF.Min(slack, Clear(at) - shape.FootRoom);
            }
            return slack;
        }

        /// <summary>Whether its tail has room too, for somewhere it means to stay: it's carried high, so it only matters by its tip, and only where it's going to stand a while.</summary>
        public bool TailClear(in GiantShape shape, Vector2 middle, float yaw)
        {
            Vector2 forward = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
            return Clear(middle - forward * (shape.Back + 6f * shape.Scale)) >= 1.5f * shape.Scale;
        }

        /// <summary>The nearest place to <paramref name="at"/> with this much room, looked for outwards in rings.</summary>
        public bool TryRoomNear(Vector2 at, float need, float within, out Vector2 found)
        {
            found = at;
            if (Clear(at) >= need) return true;
            for (float radius = Cell; radius <= within; radius += Cell)
            {
                int steps = Math.Max((int)(MathF.Tau * radius / Cell), 8);
                for (int i = 0; i < steps; i++)
                {
                    float angle = i * MathF.Tau / steps;
                    Vector2 point = at + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    if (Clear(point) < need) continue;
                    found = point;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A way from one place to another with at least <paramref name="need"/> metres of room all along it, as a few corners to walk between. A* over the grid, keeping out from under trees, then
        /// pulled straight wherever a straight line is clear.
        /// </summary>
        public bool TryPath(Vector2 from, Vector2 to, float need, List<Vector2> path, IReadOnlyList<Vector2>? avoid = null, float avoidRadius = 0f)
        {
            _avoid = avoid;
            _avoidRadius = avoidRadius;
            path.Clear();

            // A giant can stand where it hasn't room to walk, beside a tree or between two: the way goes from and to the nearest places it has room to walk, and it steps the rest itself.
            if (!TryRoomNear(from, need, 18f, out Vector2 leave) || !TryRoomNear(to, need, 18f, out Vector2 arrive)) return false;
            int start = IndexOf(leave), goal = IndexOf(arrive);
            if (start < 0 || goal < 0) return false;
            if (leave != from) path.Add(leave);

            _search++;
            _open.Clear();
            _cost[start] = 0f;
            _from[start] = -1;
            _stamp[start] = _search;
            _open.Enqueue(start, 0f);
            Vector2 target = CentreOf(goal % _nx, goal / _nx);
            bool found = false;
            int visited = 0;
            while (_open.TryDequeue(out int cell, out _))
            {
                if (cell == goal)
                {
                    found = true;
                    break;
                }
                if (++visited > _clear.Length) break;
                int cx = cell % _nx, cz = cell / _nx;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int nx = cx + dx, nz = cz + dz;
                        if (nx < 0 || nz < 0 || nx >= _nx || nz >= _nz) continue;
                        int next = nx + nz * _nx;
                        if (_clear[next] < need) continue;
                        Vector2 at = CentreOf(nx, nz);
                        float step = (dx != 0 && dz != 0 ? 1.4142135f : 1f) * Cell * (1f + CanopyCost * World.Canopy.Sample(at)) * (Avoided(at, -1) ? AvoidCost : 1f);
                        float cost = _cost[cell] + step;
                        if (_stamp[next] == _search && cost >= _cost[next]) continue;
                        _stamp[next] = _search;
                        _cost[next] = cost;
                        _from[next] = cell;
                        _open.Enqueue(next, cost + (at - target).Length);
                    }
                }
            }
            if (!found) return false;

            _cells.Clear();
            for (int cell = goal; cell >= 0; cell = _from[cell]) _cells.Add(cell);
            _cells.Reverse();

            // Straight wherever it can be, from each corner to the furthest cell it can see with room all the way.
            Vector2 corner = leave;
            int i = 0;
            while (i < _cells.Count - 1)
            {
                int furthest = i + 1;
                for (int j = _cells.Count - 1; j > i + 1; j--)
                {
                    if (!Open(corner, CentreOf(_cells[j] % _nx, _cells[j] / _nx), need)) continue;
                    furthest = j;
                    break;
                }
                corner = CentreOf(_cells[furthest] % _nx, _cells[furthest] / _nx);
                path.Add(corner);
                i = furthest;
            }
            if (path.Count == 0 || (path[^1] - to).LengthSquared > 1e-4f) path.Add(to);
            return true;
        }

        private bool Open(Vector2 a, Vector2 b, float need)
        {
            float length = (b - a).Length;
            int steps = Math.Max((int)(length / (Cell * 0.5f)), 1);
            int startsIn = AvoidedBy(a);
            for (int s = 1; s <= steps; s++)
            {
                Vector2 at = Vector2.Lerp(a, b, s / (float)steps);
                if (Clear(at) < need || Avoided(at, startsIn)) return false;
            }
            return true;
        }

        private IReadOnlyList<Vector2>? _avoid;
        private float _avoidRadius;

        // Whether a place is close by any of what's to be kept away from, but the one given, which a straight line starting there can leave by.
        private bool Avoided(Vector2 at, int but)
        {
            if (_avoid == null) return false;
            for (int i = 0; i < _avoid.Count; i++)
            {
                if (i != but && (_avoid[i] - at).LengthSquared < _avoidRadius * _avoidRadius) return true;
            }
            return false;
        }

        private int AvoidedBy(Vector2 at)
        {
            if (_avoid == null) return -1;
            for (int i = 0; i < _avoid.Count; i++)
            {
                if ((_avoid[i] - at).LengthSquared < _avoidRadius * _avoidRadius) return i;
            }
            return -1;
        }

        private int IndexOf(Vector2 at)
        {
            int x = (int)MathF.Floor((at.X - _origin.X) / Cell);
            int z = (int)MathF.Floor((at.Y - _origin.Y) / Cell);
            if (x < 0 || z < 0 || x >= _nx || z >= _nz) return -1;
            return x + z * _nx;
        }

        /// <summary>
        /// Where a giant should stand to eat from a tree, coming at it from <paramref name="from"/>: facing the trunk, with its mouth in the side of the crown just below the top, or as high as it
        /// reaches, and its neck the length it likes to have it at. Tried all round the tree, nearest way round first.
        /// </summary>
        public bool TryBrowseSpot(in GiantShape shape, in Browse tree, Vector2 from, out Vector2 middle, out float yaw, out float biteHeight)
        {
            middle = default;
            yaw = 0f;
            float lowest = shape.LowestBite;
            float highest = shape.HighestMouth - 0.6f * shape.Scale;
            biteHeight = Math.Clamp(tree.Height - 1.4f, lowest, highest);
            if (tree.Height - 0.3f < lowest) return false;

            Vector2 toward = from - tree.Trunk;
            float start = toward.LengthSquared > 1e-4f ? MathF.Atan2(toward.X, toward.Y) : 0f;
            const int Tries = 16;
            for (int k = 0; k < Tries; k++)
            {
                // Nearest way round first, then either side of it in turn.
                float angle = start + ((k + 1) / 2) * (k % 2 == 0 ? 1f : -1f) * (MathF.Tau / Tries);
                Vector2 out_ = new Vector2(MathF.Sin(angle), MathF.Cos(angle));
                Vector2 mouth = tree.Trunk + out_ * MathF.Max(tree.RadiusAt(biteHeight) * 0.72f, 0.8f);
                float rise = biteHeight + tree.Ground - (World.Height.Sample(mouth) + shape.NeckRoot.Y);
                float chord = shape.Reach * 0.84f;
                float run = MathF.Sqrt(MathF.Max(chord * chord - rise * rise, shape.Reach * shape.Reach * 0.1f));
                Vector2 root = mouth + out_ * run;
                float facing = MathF.Atan2(-out_.X, -out_.Y);
                Vector2 forward = new Vector2(MathF.Sin(facing), MathF.Cos(facing));
                Vector2 standsAt = root - forward * shape.NeckRoot.Z;
                Vector2 centre = standsAt + forward * shape.Middle;
                // With room to spare, and somewhere it can walk away from again, not a corner it would be stuck in.
                if (!CanStand(shape, centre, facing, -Spare) || !TailClear(shape, centre, facing) || !TryRoomNear(centre, shape.WalkRoom, 18f, out _)) continue;
                middle = centre;
                yaw = facing;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Where the giants live, and which they are. The Haven's are its own, and live all over the isle but in the village: read LORE.md. They're named as any dinosaur is, and each is one animal,
    /// grown from its seed, so the same ones are there every time anyone comes home.
    /// </summary>
    public static class Herds
    {
        /// <summary>The Haven's giants, by the names their models are grown from, and the size each is drawn at.</summary>
        public static readonly (string Model, float Scale)[] Haven = { ("dino:giant.0", 1f), ("dino:giant.1", 1f), ("dino:giant.2", 1f) };

        private const float VillageMargin = 4f;     // Metres past the edge of the Haven's village that a giant's middle keeps out of, as its body is half as wide again.

        /// <summary>The village a herd keeps out of in the Haven, or nothing in any other world.</summary>
        public static WorldSite? Village(WorldData world) => world.Plan.Archetype.Peaceful ? world.Sites.Find(site => site.Kind == SiteKind.Haven) : null;

        /// <summary>The Haven's herd: all of the isle that isn't the village, starting round one of its trees, which the seed chooses.</summary>
        public static Herd? InHaven(WorldData world, IReadOnlyList<GiantShape> shapes, uint seed)
        {
            if (Village(world) is not WorldSite village) return null;
            Vector2 centre = village.Position;
            float keep = village.Radius + VillageMargin;
            var land = new HerdLand(world, Vector2.Zero, new Vector2(world.Size), at => (at - centre).LengthSquared > keep * keep);
            return Gather(land, shapes, seed, centre);
        }

        /// <summary>A herd that keeps within <paramref name="radius"/> of a place, and to wherever else it's allowed, as the one in view behind the main menu keeps in the picture.</summary>
        public static Herd? Around(WorldData world, Vector2 home, float radius, IReadOnlyList<GiantShape> shapes, uint seed, Func<Vector2, bool>? allowed = null)
        {
            var land = new HerdLand(world, home - new Vector2(radius), home + new Vector2(radius), at => (at - home).LengthSquared < radius * radius && (allowed == null || allowed(at)));
            return Gather(land, shapes, seed, home);
        }

        private static Herd? Gather(HerdLand land, IReadOnlyList<GiantShape> shapes, uint seed, Vector2 fallback)
        {
            // Home is a tree with room round it, chosen by the seed, so that the herd is somewhere different each visit but always somewhere it can be.
            Vector2 home = fallback;
            uint pick = seed * 747796405u + 2891336453u;
            for (int attempt = 0; attempt < land.Trees.Count && attempt < 40; attempt++)
            {
                pick ^= pick << 13;
                pick ^= pick >> 17;
                pick ^= pick << 5;
                Browse tree = land.Trees[(int)(pick % (uint)land.Trees.Count)];
                if (!land.TryRoomNear(tree.Trunk, 8f, 30f, out _)) continue;
                home = tree.Trunk;
                break;
            }

            var herd = new Herd(land, seed, home);
            for (int i = 0; i < shapes.Count; i++)
            {
                if (herd.Add(shapes[i]) is Giant giant) giant.Index = i;
            }
            return herd.Members.Count > 0 ? herd : null;
        }
    }

    /// <summary>What a giant is doing.</summary>
    public enum GiantDoing
    {
        Standing,   // At its ease, looking about.
        Walking,    // To somewhere it means to be.
        Turning,    // Round on the spot, to face what it's come for.
        Browsing,   // Cropping the top of a tree.
        Regarding,  // Looking at someone, its head lowered a little to see them better.
        Calling,    // Its head up, booming.
        Shuffling,  // A few steps without turning: back out of somewhere too tight to turn round in, as an elephant does, or aside from over someone.
    }

    /// <summary>One of a herd: where it stands and which way it faces, and what its head is doing, which is all its body is told.</summary>
    public sealed class Giant
    {
        public readonly GiantShape Shape;
        public readonly float Cruise;       // How fast it likes to walk.

        /// <summary>Which of the shapes the herd was given this one is, and so which body.</summary>
        public int Index { get; internal set; }

        public Vector2 Middle;              // Of its feet, on the ground: what it turns about.
        public float Yaw;
        public float Speed;
        public GiantDoing Doing = GiantDoing.Standing;

        // What its body is told.
        public Vector3 ReachFor;
        public float Reaching;
        public Vector3 LookAt;
        public bool Looking;
        public float Chewing;
        public Vector2 KeepOff;             // Whoever's nearest a foot, and how far round them no foot is to come down: see AnimationInput.KeepOff.
        public float KeepOffRadius;

        // What happened in the last step, for whoever makes it heard and seen.
        public bool Called;
        public Vector3 CalledFrom;
        public bool Tugged;
        public Vector3 TuggedAt;

        internal readonly List<Vector2> Path = new();
        internal int PathAt;
        internal float Left;
        internal float Waited;
        internal GiantDoing After;          // What it was doing before something took its attention, or what it's going to do once it's turned round.
        internal Browse Tree;
        internal bool HasTree;
        internal float BiteHeight;
        internal int Bites;
        internal Vector3 Bite;
        internal float BiteFor;
        internal float TugAt;
        internal float GoalYaw;
        internal float RegardAgain;
        internal Vector2 Watching;
        internal bool TurnLong;
        internal float TurnWay;             // Which way round it's turning on the spot, once it's chosen: 1, -1, or 0 for not yet.
        internal bool Escaped;              // Just got out of somewhere tight, so the next thing it does isn't getting out again.
        internal readonly List<(Vector2 Middle, float Yaw)> Shuffles = new();   // Where it's shuffling to, a step or a turn at a time.
        internal int ShuffleAt;
        internal float ShuffleGive;         // How much less room than it likes it puts up with on the way.
        internal float StepOffAgain;        // Seconds before it looks again for a way off someone, having found none.

        internal Giant(GiantShape shape, float cruise)
        {
            Shape = shape;
            Cruise = cruise;
        }

        public Vector2 Forward => new Vector2(MathF.Sin(Yaw), MathF.Cos(Yaw));

        /// <summary>Whether it's eating from a tree this moment, and which.</summary>
        public bool EatingFrom(out Vector2 trunk)
        {
            trunk = Tree.Trunk;
            return HasTree && Doing == GiantDoing.Browsing;
        }

        /// <summary>Where its body stands: under its hips, which is what its rig is built round.</summary>
        public Vector2 Root => Middle - Forward * Shape.Middle;

        /// <summary>The root of its neck in the world, for a body standing on ground this high.</summary>
        public Vector3 NeckRootAt(float ground)
        {
            Vector2 at = Root + Forward * Shape.NeckRoot.Z;
            return new Vector3(at.X, ground + Shape.NeckRoot.Y, at.Y);
        }

        /// <summary>Where its mouth is at its ease, in the world.</summary>
        public Vector3 EaseMouthAt(float ground)
        {
            Vector2 at = Root + Forward * Shape.RestMouth.Z;
            return new Vector3(at.X, ground + Shape.RestMouth.Y, at.Y);
        }
    }

    /// <summary>
    /// A few giants living together in a range, browsing the tops of its trees, resting and wandering between them, calling to each other now and then, and taking a long look at anyone who comes
    /// near, with their heads lowered a little to see them better. They never walk into anyone: whoever's in the way, they stop and look at, and whoever's under their feet, they step away from.
    /// It's only ever looked at, so it's simple and it can't go wrong: anything that can't be done is given up for something else.
    /// </summary>
    public sealed class Herd
    {
        private const float TurnRate = 0.3f;            // Radians a second, walking, at a scale of one.
        private const float TurnOnSpot = 0.22f;         // And turning round where it stands.
        private const float SpeedUp = 0.5f;             // Metres a second, each second.
        private const float SlowDown = 0.8f;
        private const float Ahead = 9f;                 // Metres ahead along its way that it steers for.
        private const float Arrived = 1.2f;
        private const float RegardReach = 30f;          // Metres within which someone's worth a look.
        private const float StandAside = 3.2f;          // Metres from a foot that someone has to be for it to move.
        private const float Apart = 24f;                // Metres it keeps between its middle and another's, when choosing where to go.
        private const float Gather = 70f;               // And from the middle of the herd's range of the moment.
        private const float WanderReach = 35f;          // Metres it wanders, at most, from somewhere between where it is and the others.
        private const float HomeFor = 260f;             // Seconds the herd stays round one part of its range.
        private const float ChewAfter = 2.8f;           // Seconds into a mouthful before it starts chewing, which is about how long a head that size takes to get there.
        private const float StillEnough = 1.5f;         // Metres a second under which someone is standing about, and can be stepped away from without a foot coming down where they're going.
        private const int MostPeople = 16;

        public readonly HerdLand Land;
        public readonly List<Giant> Members = new();

        /// <summary>What each giant decides and why, for a lab to print. Nothing is written unless something's listening.</summary>
        public Action<string>? Report;

        private uint _random;
        private Vector2 _home;
        private float _homeLeft;
        private float _callIn;
        private Giant? _answering;
        private float _answerIn;
        private readonly Vector2[] _was = new Vector2[MostPeople];
        private readonly float[] _moving = new float[MostPeople];
        private int _wasCount;

        public Herd(HerdLand land, uint seed, Vector2 home)
        {
            Land = land;
            _random = seed * 2654435761u + 0x9E3779B9u;
            if (_random == 0) _random = 1;
            _home = home;
            _homeLeft = HomeFor * Range(0.6f, 1.2f);
            _callIn = Range(20f, 45f);
        }

        private float Next()
        {
            _random ^= _random << 13;
            _random ^= _random >> 17;
            _random ^= _random << 5;
            return (_random & 0xFFFFFF) / 16777216f;
        }

        private float Range(float min, float max) => min + (max - min) * Next();

        private float Ground(Vector2 at) => Land.World.Height.Sample(at);

        /// <summary>A giant this shape, somewhere it can stand near the herd's home, doing something already, so that the herd is found in the middle of its day.</summary>
        public Giant? Add(GiantShape shape)
        {
            var giant = new Giant(shape, Range(2.4f, 3f) * MathF.Sqrt(shape.Scale));

            // Eating, as most of a giant's day is, at a tree no other is at.
            for (int attempt = 0; attempt < 24; attempt++)
            {
                Vector2 near = _home + Spread(Gather);
                if (!TryChooseTree(giant, near, near, out Browse tree, out Vector2 middle, out float yaw, out float height)) continue;
                giant.Middle = middle;
                giant.Yaw = yaw;
                Members.Add(giant);
                StartBrowsing(giant, tree, height);
                return giant;
            }

            // Or standing in the open, if there's no tree for it.
            for (int attempt = 0; attempt < 60; attempt++)
            {
                Vector2 at = _home + Spread(Gather);
                float yaw = Range(0f, MathF.Tau);
                if (!Land.CanStand(shape, at, yaw, -HerdLand.Spare) || Crowded(giant, at)) continue;
                giant.Middle = at;
                giant.Yaw = yaw;
                Members.Add(giant);
                StartStanding(giant);
                return giant;
            }
            return null;
        }

        private Vector2 Spread(float within)
        {
            float angle = Range(0f, MathF.Tau);
            return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * within * MathF.Sqrt(Next());
        }

        private bool Crowded(Giant giant, Vector2 at)
        {
            foreach (Giant other in Members)
            {
                if (other == giant) continue;
                if ((other.Middle - at).LengthSquared < Apart * Apart * other.Shape.Scale * giant.Shape.Scale) return true;
                if (other.Doing == GiantDoing.Walking && other.Path.Count > 0 && (other.Path[^1] - at).LengthSquared < Apart * Apart) return true;
            }
            return false;
        }

        private bool Claimed(Giant giant, in Browse tree)
        {
            foreach (Giant other in Members)
            {
                if (other != giant && other.HasTree && other.Tree.Trunk == tree.Trunk) return true;
            }
            return false;
        }

        public void Step(float deltaTime, ReadOnlySpan<Vector2> people)
        {
            if (deltaTime <= 0f) return;
            deltaTime = MathF.Min(deltaTime, 0.25f);

            // How fast everyone is going, from where they were: someone new, or put somewhere else, is taken to be moving.
            for (int i = 0; i < people.Length && i < MostPeople; i++)
            {
                float moved = i < _wasCount ? (people[i] - _was[i]).Length : float.MaxValue;
                _moving[i] = moved > 20f ? float.MaxValue : moved / deltaTime;
                _was[i] = people[i];
            }
            _wasCount = Math.Min(people.Length, MostPeople);

            _homeLeft -= deltaTime;
            if (_homeLeft <= 0f) MoveHome();
            foreach (Giant giant in Members)
            {
                giant.Called = false;
                giant.Tugged = false;
            }
            Calls(deltaTime);

            foreach (Giant giant in Members)
            {
                giant.RegardAgain -= deltaTime;
                giant.StepOffAgain -= deltaTime;
                Notice(giant, people);
                switch (giant.Doing)
                {
                    case GiantDoing.Walking: Walk(giant, deltaTime, people); break;
                    case GiantDoing.Turning: Turn(giant, deltaTime, people); break;
                    case GiantDoing.Browsing: Crop(giant, deltaTime); break;
                    case GiantDoing.Regarding: Regard(giant, deltaTime, people); break;
                    case GiantDoing.Calling: Call(giant, deltaTime); break;
                    case GiantDoing.Shuffling: Shuffle(giant, deltaTime, people); break;
                    default: Stand(giant, deltaTime); break;
                }
                KeepFeetOff(giant, people);
            }
        }

        // The part of its range the herd keeps to moves on every few minutes, to wherever there are trees it hasn't been at.
        private void MoveHome()
        {
            _homeLeft = HomeFor * Range(0.7f, 1.3f);
            if (Land.Trees.Count == 0) return;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Browse tree = Land.Trees[(int)(Next() * Land.Trees.Count) % Land.Trees.Count];
                if ((tree.Trunk - _home).Length < Gather * 0.6f) continue;
                if (!Land.TryRoomNear(tree.Trunk, 6f, 30f, out _)) continue;
                _home = tree.Trunk;
                return;
            }
        }

        // ---- What it does.

        private void StartStanding(Giant giant)
        {
            giant.Doing = GiantDoing.Standing;
            giant.Left = Range(8f, 20f);
            giant.Reaching = 0f;
            giant.Chewing = 0f;
            giant.Looking = false;
            giant.HasTree = false;
            giant.Watching = giant.Middle + giant.Forward * 60f;
        }

        private void Stand(Giant giant, float deltaTime)
        {
            Slow(giant, deltaTime);
            giant.Left -= deltaTime;

            // Now and then it looks at something: one of the others, or off over the land.
            if (Next() < deltaTime * 0.08f)
            {
                Giant other = Members[(int)(Next() * Members.Count) % Members.Count];
                giant.Watching = other != giant ? other.Middle : giant.Middle + new Vector2(MathF.Sin(giant.Yaw + Range(-1.2f, 1.2f)), MathF.Cos(giant.Yaw + Range(-1.2f, 1.2f))) * 80f;
                giant.Looking = Next() < 0.7f;
            }
            float ground = Ground(giant.Watching);
            giant.LookAt = new Vector3(giant.Watching.X, ground + 8f, giant.Watching.Y);
            if (giant.Left <= 0f) ChooseNext(giant);
        }

        private void StartBrowsing(Giant giant, in Browse tree, float height)
        {
            giant.Doing = GiantDoing.Browsing;
            giant.Tree = tree;
            giant.HasTree = true;
            giant.BiteHeight = height;
            giant.Bites = (int)Range(6f, 12f);
            giant.Left = 0f;
            giant.Looking = false;
            NextBite(giant);
        }

        // A mouthful at a time, from wherever in the side of the crown it can get to, now and then pulling at it.
        private void NextBite(Giant giant)
        {
            Browse tree = giant.Tree;
            float ground = Ground(giant.Root);
            Vector3 root = giant.NeckRootAt(ground);
            Vector2 toward = (giant.Middle - tree.Trunk);
            float facing = toward.LengthSquared > 1e-4f ? MathF.Atan2(toward.X, toward.Y) : 0f;
            float angle = facing + Range(-0.55f, 0.55f);
            float height = Math.Clamp(giant.BiteHeight + Range(-2.4f, 0.8f), giant.Shape.LowestBite, MathF.Min(tree.Height - 0.4f, giant.Shape.HighestMouth));
            float radius = MathF.Max(tree.RadiusAt(height), 1f) * Range(0.55f, 0.85f);
            Vector3 bite = new Vector3(tree.Trunk.X + MathF.Sin(angle) * radius, tree.Ground + height, tree.Trunk.Y + MathF.Cos(angle) * radius);

            // Anything out of reach is reached for as far as it goes, and anything too near for a neck that long to fold to is taken from a little further out.
            Vector3 fromRoot = bite - root;
            float most = giant.Shape.Reach * 0.88f, least = giant.Shape.Reach * GiantShape.Nearest;
            if (fromRoot.Length > most) bite = root + fromRoot.Normalized() * most;
            else if (fromRoot.Length < least) bite = root + fromRoot.Normalized() * least;

            giant.Bite = bite;
            giant.BiteFor = Range(4.5f, 8f);
            giant.TugAt = Next() < 0.35f ? Range(ChewAfter + 0.5f, giant.BiteFor - 0.8f) : -1f;
            giant.Left = 0f;
        }

        private void Crop(Giant giant, float deltaTime)
        {
            Slow(giant, deltaTime);
            giant.Left += deltaTime;
            giant.Reaching = 1f;

            // The head gets there before it starts to chew, and a pull is a moment's lean back from the crown, taking the leaves with it.
            giant.ReachFor = giant.Bite;
            giant.Chewing = giant.Left > ChewAfter ? 1f : 0f;
            if (giant.TugAt > 0f && giant.Left >= giant.TugAt)
            {
                if (giant.Left < giant.TugAt + 0.6f)
                {
                    Vector3 root = giant.NeckRootAt(Ground(giant.Root));
                    Vector3 back = (root - giant.Bite);
                    giant.ReachFor = giant.Bite + (back.LengthSquared > 1e-4f ? back.Normalized() : Vector3.UnitY) * 1.1f * giant.Shape.Scale;
                    if (!giant.Tugged && giant.Left - deltaTime < giant.TugAt)
                    {
                        giant.Tugged = true;
                        giant.TuggedAt = giant.Bite;
                    }
                }
                else giant.TugAt = -1f;
            }

            if (giant.Left < giant.BiteFor) return;
            if (--giant.Bites > 0)
            {
                NextBite(giant);
                return;
            }
            ChooseNext(giant);
        }

        // Its head lowered a little and turned to whoever it is, as anything that big and that gentle looks at what's come to see it. The body turns too, if they're round behind it.
        private void StartRegarding(Giant giant, Vector2 who)
        {
            if (giant.Doing != GiantDoing.Regarding) giant.After = giant.Doing == GiantDoing.Walking ? GiantDoing.Standing : giant.Doing;
            giant.Doing = GiantDoing.Regarding;
            giant.Left = Range(5f, 9f);
            giant.Watching = who;
            giant.Chewing = 0f;
            giant.RegardAgain = Range(35f, 60f);
        }

        private void Regard(Giant giant, float deltaTime, ReadOnlySpan<Vector2> people)
        {
            Slow(giant, deltaTime);
            giant.Left -= deltaTime;

            // Whoever it was may have moved, and it follows them with its eyes.
            if (Nearest(people, giant.Middle, out Vector2 who) < RegardReach * 1.4f) giant.Watching = who;
            float ground = Ground(giant.Watching);
            giant.LookAt = new Vector3(giant.Watching.X, ground + 4f, giant.Watching.Y);
            giant.Looking = true;

            Vector3 root = giant.NeckRootAt(Ground(giant.Root));
            Vector2 toward = giant.Watching - root.Xz;
            float distance = toward.Length;
            Vector2 way = distance > 1e-3f ? toward / distance : giant.Forward;
            float out_ = MathF.Min(giant.Shape.Reach * 0.7f, MathF.Max(distance - 8f * giant.Shape.Scale, giant.Shape.Reach * 0.4f));
            float height = MathF.Max(ground + giant.Shape.LowestBite + 1f * giant.Shape.Scale, root.Y + giant.Shape.Reach * 0.2f);
            Vector3 look = new Vector3(root.X + way.X * out_, height, root.Z + way.Y * out_);
            Vector3 fromRoot = look - root;
            float least = giant.Shape.Reach * GiantShape.Nearest;
            if (fromRoot.Length < least) look = root + fromRoot.Normalized() * least;
            giant.ReachFor = look;
            giant.Reaching = 0.85f;

            // Turning swings every foot round, and it won't while anyone's close to one: it only follows them with its head.
            float off = Spring.Shortest(MathF.Atan2(way.X, way.Y) - giant.Yaw);
            if (MathF.Abs(off) > 1.3f && !AnyUnderFoot(giant, people, 2f) && !ByTail(giant, people))
            {
                float yaw = giant.Yaw + Math.Clamp(off, -1f, 1f) * TurnOnSpot * deltaTime / MathF.Sqrt(giant.Shape.Scale);
                if (Allowed(giant, giant.Middle, yaw)) giant.Yaw = yaw;
            }

            if (giant.Left > 0f) return;
            giant.Looking = false;
            Resume(giant);
        }

        private void Resume(Giant giant)
        {
            switch (giant.After)
            {
                case GiantDoing.Browsing when giant.HasTree && InFront(giant, giant.Tree.Trunk):
                    giant.Doing = GiantDoing.Browsing;
                    NextBite(giant);
                    break;
                default:
                    ChooseNext(giant);
                    break;
            }
        }

        private void Call(Giant giant, float deltaTime)
        {
            Slow(giant, deltaTime);
            giant.Left -= deltaTime;
            Vector3 root = giant.NeckRootAt(Ground(giant.Root));
            Vector2 forward = giant.Forward;
            giant.ReachFor = root + new Vector3(forward.X, 0f, forward.Y) * giant.Shape.Reach * 0.36f + Vector3.UnitY * giant.Shape.Reach * 0.9f;
            giant.Reaching = 0.9f;
            giant.Chewing = 0f;
            giant.Looking = false;
            if (giant.Left <= 0f) Resume(giant);
        }

        // Every so often one of them booms, from its chest with its head up, and as often as not another answers.
        private void Calls(float deltaTime)
        {
            if (_answering != null)
            {
                _answerIn -= deltaTime;
                if (_answerIn <= 0f)
                {
                    StartCalling(_answering);
                    _answering = null;
                }
            }

            _callIn -= deltaTime;
            if (_callIn > 0f || Members.Count == 0) return;
            Giant? caller = Free(null);
            if (caller == null)
            {
                _callIn = 5f;
                return;
            }
            _callIn = Range(40f, 95f);
            StartCalling(caller);
            if (Next() < 0.6f && Free(caller) is Giant other)
            {
                _answering = other;
                _answerIn = Range(2.5f, 5f);
            }
        }

        // One of them, chosen at random from those that aren't on the move or busy with someone, and aren't the one given.
        private Giant? Free(Giant? but)
        {
            int start = (int)(Next() * Members.Count) % Members.Count;
            for (int k = 0; k < Members.Count; k++)
            {
                Giant giant = Members[(start + k) % Members.Count];
                if (giant != but && giant.Doing is GiantDoing.Standing or GiantDoing.Browsing) return giant;
            }
            return null;
        }

        private void StartCalling(Giant giant)
        {
            if (giant.Doing is GiantDoing.Walking or GiantDoing.Turning) return;
            if (giant.Doing != GiantDoing.Calling) giant.After = giant.Doing == GiantDoing.Regarding ? GiantDoing.Standing : giant.Doing;
            giant.Doing = GiantDoing.Calling;
            giant.Left = Range(3.6f, 4.6f);
            Vector3 root = giant.NeckRootAt(Ground(giant.Root));
            giant.Called = true;
            giant.CalledFrom = root + Vector3.UnitY * giant.Shape.Reach * 0.8f;
        }

        // ---- Deciding.

        private void ChooseNext(Giant giant)
        {
            giant.HasTree = false;
            giant.Chewing = 0f;

            // Somewhere too tight to turn round in, beside a tree or between two, it shuffles out first, a step or a turn at a time, as an elephant does.
            if (!giant.Escaped && Land.Clear(giant.Middle) < giant.Shape.WalkRoom)
            {
                giant.Escaped = true;
                if (TryWayOut(giant))
                {
                    Report?.Invoke($"giant {Members.IndexOf(giant)} shuffles out of somewhere tight at {giant.Middle.X:0},{giant.Middle.Y:0} in {giant.Shuffles.Count} moves");
                    return;
                }
                Report?.Invoke($"giant {Members.IndexOf(giant)} finds no way out of somewhere tight at {giant.Middle.X:0},{giant.Middle.Y:0}");
            }
            giant.Escaped = false;
            float roll = Next();
            int noTree = 0, noWay = 0;
            if (roll < 0.8f)
            {
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    if (!TryChooseTree(giant, _home + Spread(Gather * 0.8f), giant.Middle, out Browse tree, out Vector2 middle, out float yaw, out float height))
                    {
                        noTree++;
                        continue;
                    }
                    if (!GoTo(giant, middle, yaw, GiantDoing.Browsing))
                    {
                        noWay++;
                        continue;
                    }
                    giant.Tree = tree;
                    giant.HasTree = true;
                    giant.BiteHeight = height;
                    Report?.Invoke($"giant {Members.IndexOf(giant)} goes to eat at {tree.Trunk.X:0},{tree.Trunk.Y:0}, {(middle - giant.Middle).Length:0} m away");
                    return;
                }
            }
            else if (roll < 0.9f)
            {
                // A short way, and drifting back towards the others, as a herd keeps together.
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    Vector2 at = Vector2.Lerp(giant.Middle, _home, 0.35f) + Spread(WanderReach);
                    if (Land.Canopy(at) > 0.2f) continue;
                    float yaw = Range(0f, MathF.Tau);
                    if (!Land.CanStand(giant.Shape, at, yaw, -HerdLand.Spare) || !Land.TailClear(giant.Shape, at, yaw) || Crowded(giant, at)) continue;
                    if (GoTo(giant, at, yaw, GiantDoing.Standing))
                    {
                        Report?.Invoke($"giant {Members.IndexOf(giant)} wanders to {at.X:0},{at.Y:0}");
                        return;
                    }
                    noWay++;
                }
            }
            Report?.Invoke($"giant {Members.IndexOf(giant)} stands where it is at {giant.Middle.X:0},{giant.Middle.Y:0} (roll {roll:0.00}, {noTree} trees with no room, {noWay} with no way there)");
            StartStanding(giant);
        }

        // Every tree, best first: near this giant and near the herd, tall ones a little ahead, and a little chance in it, and of those the first it has room to eat from. A range has a few dozen trees.
        private readonly List<(float Score, int Tree)> _candidates = new();
        private readonly List<Vector2> _others = new();

        private bool TryChooseTree(Giant giant, Vector2 near, Vector2 from, out Browse chosen, out Vector2 middle, out float yaw, out float height)
        {
            chosen = default;
            middle = default;
            yaw = 0f;
            height = 0f;
            _candidates.Clear();
            for (int i = 0; i < Land.Trees.Count; i++)
            {
                Browse tree = Land.Trees[i];
                if (Claimed(giant, tree)) continue;
                _candidates.Add(((tree.Trunk - from).Length * 0.6f + (tree.Trunk - near).Length - tree.Height + Range(0f, 30f), i));
            }
            _candidates.Sort((a, b) => a.Score.CompareTo(b.Score));
            for (int k = 0; k < _candidates.Count && k < 8; k++)
            {
                Browse tree = Land.Trees[_candidates[k].Tree];
                if (!Land.TryBrowseSpot(giant.Shape, tree, from, out Vector2 spot, out float facing, out float bite) || Crowded(giant, spot)) continue;
                chosen = tree;
                middle = spot;
                yaw = facing;
                height = bite;
                return true;
            }
            return false;
        }

        private bool GoTo(Giant giant, Vector2 middle, float yaw, GiantDoing then)
        {
            // Somewhere only a few steps off is shuffled to, a step or a turn at a time, as walking turns as it goes, and near a tree there isn't the room.
            float distance = (middle - giant.Middle).Length;
            if (distance <= ShuffleWithin && TryShuffleTo(giant, middle, yaw, then)) return true;
            if (distance < Arrived * 2f)
            {
                giant.GoalYaw = yaw;
                giant.After = then;
                giant.Doing = GiantDoing.Turning;
                return true;
            }
            // To eat, it walks up to the tree face on, the last stretch straight in, so that it arrives facing it with nothing to turn.
            Vector2 forward = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
            float run = 10f * giant.Shape.Scale;
            Vector2 approach = middle - forward * run;
            bool straightIn = then == GiantDoing.Browsing && (approach - giant.Middle).Length > Arrived * 4f;
            for (float along = 0f; straightIn && along < run; along += 1f) straightIn = Land.CanStand(giant.Shape, approach + forward * along, yaw);
            // Round the others where they stand, and a narrower way if there's no other, as every step it takes is still onto ground it can stand on.
            Vector2 to = straightIn ? approach : middle;
            _others.Clear();
            foreach (Giant other in Members)
            {
                if (other != giant) _others.Add(other.Middle);
            }
            float apart = 2f * giant.Shape.HalfWidth + 6f * giant.Shape.Scale;
            if (!Land.TryPath(giant.Middle, to, giant.Shape.WalkRoom, giant.Path, _others, apart) && !Land.TryPath(giant.Middle, to, giant.Shape.WalkRoom * 0.75f, giant.Path, _others, apart)) return false;
            if (straightIn) giant.Path.Add(middle);
            giant.PathAt = 0;
            giant.GoalYaw = yaw;
            giant.After = then;
            giant.Doing = GiantDoing.Walking;
            giant.Reaching = 0f;
            giant.Chewing = 0f;
            giant.Looking = false;
            giant.Waited = 0f;
            return true;
        }

        // ---- Moving.

        private void Walk(Giant giant, float deltaTime, ReadOnlySpan<Vector2> people)
        {
            List<Vector2> path = giant.Path;
            if (giant.PathAt >= path.Count)
            {
                Arrive(giant);
                return;
            }

            // Steer for a point a little way along the path, so that corners are rounded.
            while (giant.PathAt < path.Count - 1 && (path[giant.PathAt] - giant.Middle).Length < Ahead * 0.6f) giant.PathAt++;
            Vector2 aim = path[giant.PathAt];
            Vector2 end = path[^1];
            float left = (end - giant.Middle).Length;
            if (left < Arrived && giant.PathAt == path.Count - 1)
            {
                Arrive(giant);
                return;
            }

            Vector2 toward = aim - giant.Middle;
            float wanted = MathF.Atan2(toward.X, toward.Y);
            float off = Spring.Shortest(wanted - giant.Yaw);
            float scale = MathF.Sqrt(giant.Shape.Scale);
            float turn = TurnRate / scale * deltaTime * (0.5f + 0.5f * Math.Clamp(giant.Speed / giant.Cruise, 0f, 1f));

            // Slow for a sharp turn and to come to a stop where it's going, and stop altogether for another giant or anyone in the way. With anyone close to a foot it doesn't so much as
            // turn, as turning swings every foot round.
            float pace = giant.Cruise * (1f - 0.75f * Gait.Smooth(0.35f, 1.3f, MathF.Abs(off))) * Math.Clamp(left / (8f * scale), 0.25f, 1f);
            foreach (Vector2 who in people)
            {
                if (!UnderFoot(giant, who, 1.4f)) continue;
                pace = 0f;
                turn = 0f;
                giant.Speed = 0f;
            }

            // Anyone by its tail, which a turn would sweep across them, it walks on past before it turns, as the tail only follows it straight.
            if (MathF.Abs(off) > 0.02f && ByTail(giant, people)) turn = 0f;
            if (Blocked(giant, people))
            {
                pace = 0f;
                giant.Waited += deltaTime;
                if (giant.Waited > 7f)
                {
                    giant.Waited = 0f;
                    ChooseNext(giant);
                    return;
                }
            }
            else giant.Waited = 0f;

            float change = (pace > giant.Speed ? SpeedUp : SlowDown) * deltaTime;
            giant.Speed += Math.Clamp(pace - giant.Speed, -change, change);

            // Every step and every turn is onto ground it can stand on, or, for one that finds itself somewhere tight, onto ground with more room than where it is.
            float yaw = giant.Yaw + Math.Clamp(off, -turn, turn);
            Vector2 next = giant.Middle + new Vector2(MathF.Sin(yaw), MathF.Cos(yaw)) * giant.Speed * deltaTime;
            if (!Allowed(giant, next, yaw))
            {
                // Something's in the way the path didn't know about, or a corner was cut too fine: think again from here.
                giant.Speed = 0f;
                ChooseNext(giant);
                return;
            }
            giant.Yaw = yaw;
            giant.Middle = next;
        }

        private static bool InFront(Giant giant, Vector2 at)
        {
            Vector2 toward = at - giant.Middle;
            return MathF.Abs(Spring.Shortest(MathF.Atan2(toward.X, toward.Y) - giant.Yaw)) < 1.1f;
        }

        // Every step and every turn is onto ground it can stand on. One that finds itself somewhere it can't move from shuffles out (TryWayOut).
        private bool Allowed(Giant giant, Vector2 middle, float yaw) => Land.CanStand(giant.Shape, middle, yaw);

        private void StartShuffling(Giant giant, float give)
        {
            giant.Doing = GiantDoing.Shuffling;
            giant.ShuffleAt = 0;
            giant.ShuffleGive = give;
            giant.Left = 25f + 4f * giant.Shuffles.Count;
            giant.Speed = 0f;
            giant.Reaching = 0f;
            giant.Chewing = 0f;
        }

        // A few slow steps, or a slow turn on the spot, at a time, so that every foot steps the same way and none swings round over anyone. It waits for anyone near a foot that's moving, and
        // never puts one down near anyone at all.
        private void Shuffle(Giant giant, float deltaTime, ReadOnlySpan<Vector2> people)
        {
            giant.Left -= deltaTime;
            if (giant.ShuffleAt >= giant.Shuffles.Count || giant.Left <= 0f)
            {
                giant.Speed = 0f;
                if (giant.ShuffleAt >= giant.Shuffles.Count && giant.After == GiantDoing.Browsing && giant.HasTree && InFront(giant, giant.Tree.Trunk) && CanReach(giant, giant.Tree))
                {
                    StartBrowsing(giant, giant.Tree, giant.BiteHeight);
                    return;
                }
                ChooseNext(giant);
                return;
            }
            (Vector2 to, float toYaw) = giant.Shuffles[giant.ShuffleAt];
            Vector2 toward = to - giant.Middle;
            float distance = toward.Length;
            float turnLeft = Spring.Shortest(toYaw - giant.Yaw);
            bool moving = distance > 1e-3f, turning = MathF.Abs(turnLeft) > 1e-4f;
            Vector2 way = moving ? toward / distance : Vector2.Zero;

            // What this moment's step or turn would be,
            float pace = !moving ? 0f : 1.1f * MathF.Sqrt(giant.Shape.Scale);
            float change = SlowDown * deltaTime;
            float speed = giant.Speed + Math.Clamp(pace - giant.Speed, -change, change);
            Vector2 next = giant.Middle + way * MathF.Min(speed * deltaTime, distance);
            float yaw = giant.Yaw;
            if (turning && !moving) yaw += MathF.Sign(turnLeft) * MathF.Min(TurnOnSpot / MathF.Sqrt(giant.Shape.Scale) * deltaTime, MathF.Abs(turnLeft));

            // and whether it's one to wait with: for anyone on the move close by a foot, as they could go anywhere, and for anyone standing still that it would take a foot nearer.
            bool hold = false;
            for (int i = 0; i < people.Length && !hold; i++)
            {
                Vector2 who = people[i];
                bool walking = i >= MostPeople || _moving[i] >= StillEnough;
                hold = walking ? UnderFoot(giant, who, turning && !moving ? 2f : 1.3f) : Nears(giant.Shape, giant.Middle, giant.Yaw, next, yaw, who);
                hold |= turning && !moving && ByTail(giant, who);
            }
            if (hold)
            {
                giant.Speed = 0f;
                return;
            }
            giant.Speed = speed;
            if (!Land.CanStand(giant.Shape, next, yaw, giant.ShuffleGive))
            {
                giant.Speed = 0f;
                ChooseNext(giant);
                return;
            }
            giant.Middle = next;
            giant.Yaw = yaw;
            if ((to - next).LengthSquared < 1e-4f && MathF.Abs(Spring.Shortest(toYaw - yaw)) < 1e-3f)
            {
                // A turn ends where it is, and a step is carried into the next: a pause between them is a stop.
                if (!moving) giant.Speed = 0f;
                giant.ShuffleAt++;
            }
        }

        // ---- A few steps and turns at a time.

        private const int OutReach = 12;            // Steps either way of the middle of a search that it will shuffle,
        private const float OutStep = 0.5f;         // of this many metres,
        private const int OutHeadings = 48;         // and how many headings it will turn to, all the way round.
        private const float OutGive = 0.1f;         // Metres less room than it likes that it will put up with at a foot, shuffling about somewhere tight,
        private const float BetweenGive = 0.27f;    // and less again between the places it means to stand, as anywhere it passes is within half a step of one.
        private const float FootClear = 2.6f;       // Metres within which a foot never goes any nearer anyone standing still, at a scale of one,
        private const float StepOffAt = 3.1f;       // and within which it steps off them,
        private const float SteppedOff = 3.6f;      // to this far.
        private const float TailClear = 4f;         // Metres from the line of its tail within which anyone stops it turning, at a scale of one.
        private const float TurnWait = 12f;         // Seconds it waits to turn for anyone in the way of it before thinking again.
        private const float KeepOffReach = 6f;      // Metres from where a foot stands at its ease that anyone is near enough for its legs to keep off,
        private const float KeepOffRadius = 2.2f;   // and how far from them no foot comes down.
        private const float ShuffleWithin = 5.5f;   // Metres to somewhere it means to be that it shuffles rather than walks, as a walk turns as it goes, and needs room to.
        private const int OutWidth = 2 * OutReach + 1;
        private const int OutNodes = OutWidth * OutWidth * OutHeadings;

        private readonly float[] _outCost = new float[OutNodes];
        private readonly int[] _outFrom = new int[OutNodes];
        private readonly int[] _outSeen = new int[OutNodes];
        private readonly int[] _outAsked = new int[OutNodes];
        private readonly bool[] _outStands = new bool[OutNodes];
        private readonly PriorityQueue<int, float> _outOpen = new();
        private readonly List<int> _outPath = new();
        private int _outSearch;

        private static int OutIndex(int i, int j, int k) => ((i + OutReach) + (j + OutReach) * OutWidth) * OutHeadings + k;

        private static (int I, int J, int K) OutNode(int index)
        {
            int k = index % OutHeadings, cell = index / OutHeadings;
            return (cell % OutWidth - OutReach, cell / OutWidth - OutReach, k);
        }

        /// <summary>The fewest small steps and turns on the spot that take it from somewhere too tight to turn round in to somewhere it has room to walk. Nothing if there's nowhere like that near.</summary>
        private bool TryWayOut(Giant giant)
        {
            if (!TryShuffles(giant, null, 0f, null)) return false;
            giant.After = GiantDoing.Standing;
            return true;
        }

        /// <summary>The same, to stand exactly here and face this way: for somewhere too near to walk to, as a walk turns as it goes.</summary>
        private bool TryShuffleTo(Giant giant, Vector2 middle, float yaw, GiantDoing then)
        {
            if (!TryShuffles(giant, middle, yaw, null)) return false;
            giant.After = then;
            return true;
        }

        /// <summary>
        /// A search over where it could stand and which way it could face, a few metres round where it is and where it's going, for the fewest small steps and turns on the spot from one to the other
        /// through places it can stand, which it then shuffles through. With nowhere given, it's for anywhere it has room to walk, or, given someone standing by a foot, for anywhere every foot is
        /// well off them, taking none nearer them on the way.
        /// </summary>
        private bool TryShuffles(Giant giant, Vector2? target, float targetYaw, Vector2? person)
        {
            GiantShape shape = giant.Shape;
            Vector2 start = giant.Middle;
            float startYaw = giant.Yaw;
            float turnStep = MathF.Tau / OutHeadings;
            float footOut = MathF.Max(shape.ForeZ - shape.Middle, shape.Middle - shape.HindZ);
            float footRadius = MathF.Sqrt(shape.FootX * shape.FootX + footOut * footOut);
            float swing = footRadius * turnStep;    // How far the furthest foot goes in a turn of a heading.
            // As little less room as it has where it is, if that's less than it would otherwise put up with, so that it can always move from there.
            float give = Math.Clamp(0.02f - Land.Slack(shape, start, startYaw), OutGive, 4f * OutGive);

            // Centred between here and there, on a grid through here.
            Vector2 centre = target is Vector2 to ? (start + to) * 0.5f : start;
            int ci = (int)MathF.Round((centre.X - start.X) / OutStep), cj = (int)MathF.Round((centre.Y - start.Y) / OutStep);
            if (Math.Abs(ci) > OutReach / 2 || Math.Abs(cj) > OutReach / 2) return false;
            Vector2 origin = start + new Vector2(ci, cj) * OutStep;

            _outSearch++;
            _outOpen.Clear();
            int first = OutIndex(-ci, -cj, 0);
            _outCost[first] = 0f;
            _outFrom[first] = -1;
            _outSeen[first] = _outSearch;
            _outOpen.Enqueue(first, 0f);
            int goal = -1;
            while (_outOpen.TryDequeue(out int node, out _))
            {
                (int i, int j, int k) = OutNode(node);
                Vector2 middle = origin + new Vector2(i, j) * OutStep;
                bool arrived = target is Vector2 there
                    ? (middle - there).Length <= OutStep * 0.75f && MathF.Abs(Spring.Shortest(startYaw + k * turnStep - targetYaw)) <= turnStep * 0.75f
                    : person is Vector2 off ? NearestFoot(shape, middle, startYaw + k * turnStep, off) >= SteppedOff * shape.Scale : Land.Clear(middle) >= shape.WalkRoom;
                if (arrived)
                {
                    goal = node;
                    break;
                }
                for (int n = 0; n < 10; n++)
                {
                    int ni = i, nj = j, nk = k;
                    float cost;
                    if (n < 8)
                    {
                        (int di, int dj) = n switch { 0 => (1, 0), 1 => (-1, 0), 2 => (0, 1), 3 => (0, -1), 4 => (1, 1), 5 => (1, -1), 6 => (-1, 1), _ => (-1, -1) };
                        ni += di;
                        nj += dj;
                        cost = OutStep * (n < 4 ? 1f : 1.4142135f);
                    }
                    else
                    {
                        nk = (k + (n == 8 ? 1 : OutHeadings - 1)) % OutHeadings;
                        cost = swing;
                    }
                    if (Math.Abs(ni) > OutReach || Math.Abs(nj) > OutReach) continue;
                    int next = OutIndex(ni, nj, nk);
                    float total = _outCost[node] + cost;
                    if (_outSeen[next] == _outSearch && total >= _outCost[next]) continue;
                    Vector2 nextMiddle = origin + new Vector2(ni, nj) * OutStep;
                    float nextYaw = startYaw + nk * turnStep;
                    if (_outAsked[next] != _outSearch)
                    {
                        _outAsked[next] = _outSearch;
                        _outStands[next] = Land.CanStand(shape, nextMiddle, nextYaw, give);
                    }
                    if (!_outStands[next]) continue;
                    if (person is Vector2 near && Nears(shape, middle, startYaw + k * turnStep, nextMiddle, nextYaw, near)) continue;
                    _outSeen[next] = _outSearch;
                    _outCost[next] = total;
                    _outFrom[next] = node;
                    float left = target is Vector2 aim
                        ? (nextMiddle - aim).Length + MathF.Abs(Spring.Shortest(nextYaw - targetYaw)) * footRadius
                        : person is Vector2 away ? MathF.Max(SteppedOff * shape.Scale - NearestFoot(shape, nextMiddle, nextYaw, away), 0f) : MathF.Max(shape.WalkRoom - Land.Clear(nextMiddle), 0f);
                    _outOpen.Enqueue(next, total + left);
                }
            }
            if (goal < 0) return false;

            // Back from there to here, keeping only where the kind of move changes, and a turn at most an eighth of the way round at a time, so that the short way round is always the way it planned.
            _outPath.Clear();
            for (int node = goal; node >= 0; node = _outFrom[node]) _outPath.Add(node);
            _outPath.Reverse();
            giant.Shuffles.Clear();
            (int pi, int pj, int pk) = OutNode(first);
            (int si, int sj, int sk) = (0, 0, 0);
            int run = 0;
            for (int s = 1; s < _outPath.Count; s++)
            {
                (int i, int j, int k) = OutNode(_outPath[s]);
                int ti = i - pi, tj = j - pj, tk = (k - pk + OutHeadings + OutHeadings / 2) % OutHeadings - OutHeadings / 2;
                if (s > 1 && ((ti, tj, tk) != (si, sj, sk) || run >= OutHeadings / 8))
                {
                    giant.Shuffles.Add((origin + new Vector2(pi, pj) * OutStep, startYaw + pk * turnStep));
                    run = 0;
                }
                (si, sj, sk) = (ti, tj, tk);
                (pi, pj, pk) = (i, j, k);
                run++;
            }
            if (_outPath.Count > 1) giant.Shuffles.Add((origin + new Vector2(pi, pj) * OutStep, startYaw + pk * turnStep));
            // And the last little way to exactly where it was going, which is less than half a step and a heading.
            if (target is Vector2 end) giant.Shuffles.Add((end, targetYaw));
            StartShuffling(giant, give + BetweenGive);
            return true;
        }

        // Whether it can get its mouth into the side of a tree's crown from where it stands.
        private bool CanReach(Giant giant, in Browse tree)
        {
            Vector3 root = giant.NeckRootAt(Ground(giant.Root));
            Vector2 toward = root.Xz - tree.Trunk;
            float apart = toward.Length;
            float radius = MathF.Max(tree.RadiusAt(giant.BiteHeight), 1f) * 0.7f;
            Vector2 side = tree.Trunk + (apart > 1e-3f ? toward / apart : giant.Forward) * MathF.Min(radius, apart);
            Vector3 bite = new Vector3(side.X, tree.Ground + giant.BiteHeight, side.Y);
            float distance = (bite - root).Length;
            return distance <= giant.Shape.Reach * 0.88f && distance >= giant.Shape.Reach * GiantShape.Nearest * 0.9f;
        }

        private void Arrive(Giant giant)
        {
            giant.Doing = GiantDoing.Turning;
            giant.Path.Clear();
        }

        private void Turn(Giant giant, float deltaTime, ReadOnlySpan<Vector2> people)
        {
            Slow(giant, deltaTime);

            // Turning on the spot swings every foot round, and the tail round further, and it won't while anyone's close to either. Kept waiting long enough, it thinks again.
            if (AnyUnderFoot(giant, people, 2f) || ByTail(giant, people))
            {
                giant.Waited += deltaTime;
                if (giant.Waited <= TurnWait) return;
                giant.Waited = 0f;
                giant.TurnLong = false;
                giant.TurnWay = 0f;
                ChooseNext(giant);
                return;
            }
            giant.Waited = 0f;
            // The short way round if there's room, or the long way if there isn't, and once it's chosen a way it keeps to it, or past halfway the short way would be back again.
            float off = Spring.Shortest(giant.GoalYaw - giant.Yaw);
            if (MathF.Abs(off) > 0.03f)
            {
                if (giant.TurnWay == 0f) giant.TurnWay = MathF.Sign(off);
                float left = giant.TurnWay == MathF.Sign(off) ? MathF.Abs(off) : MathF.Tau - MathF.Abs(off);
                float turn = TurnOnSpot / MathF.Sqrt(giant.Shape.Scale) * deltaTime;
                float yaw = giant.Yaw + giant.TurnWay * MathF.Min(turn, left);
                if (Allowed(giant, giant.Middle, yaw))
                {
                    giant.Yaw = yaw;
                    return;
                }
                if (!giant.TurnLong)
                {
                    giant.TurnLong = true;
                    giant.TurnWay = -giant.TurnWay;
                    return;
                }
                giant.TurnLong = false;
                giant.TurnWay = 0f;
                ChooseNext(giant);
                return;
            }
            giant.TurnLong = false;
            giant.TurnWay = 0f;

            if (giant.After == GiantDoing.Browsing && giant.HasTree)
            {
                // Only ever with the tree in front of it: if it somehow isn't, it goes and finds another.
                if (InFront(giant, giant.Tree.Trunk)) StartBrowsing(giant, giant.Tree, giant.BiteHeight);
                else ChooseNext(giant);
                return;
            }
            StartStanding(giant);
        }

        private static void Slow(Giant giant, float deltaTime)
        {
            giant.Speed = MathF.Max(giant.Speed - SlowDown * deltaTime, 0f);
            if (giant.Speed <= 0f) return;
            giant.Middle += giant.Forward * giant.Speed * deltaTime;
        }

        // Another giant close in front, or anyone at all in the way of where it's putting its feet next.
        private bool Blocked(Giant giant, ReadOnlySpan<Vector2> people)
        {
            Vector2 forward = giant.Forward;
            Vector2 side = new Vector2(forward.Y, -forward.X);
            float reach = giant.Shape.Front + 12f * giant.Shape.Scale;
            foreach (Vector2 who in people)
            {
                Vector2 off = who - giant.Middle;
                float along = Vector2.Dot(off, forward);
                if (along > -giant.Shape.Back * 0.5f && along < reach && MathF.Abs(Vector2.Dot(off, side)) < giant.Shape.HalfWidth + 3f) return true;
            }
            foreach (Giant other in Members)
            {
                if (other == giant) continue;
                Vector2 off = other.Middle - giant.Middle;
                float along = Vector2.Dot(off, forward);
                if (along > 0f && along < giant.Shape.Front + other.Shape.Back + 10f && MathF.Abs(Vector2.Dot(off, side)) < giant.Shape.HalfWidth + other.Shape.HalfWidth + 2f) return true;
            }
            return false;
        }

        // Someone coming near is looked at, someone in the way is stopped for, and someone right under a foot is stepped away from.
        private void Notice(Giant giant, ReadOnlySpan<Vector2> people)
        {
            if (people.Length == 0) return;
            float distance = Nearest(people, giant.Middle, out Vector2 who);
            int whoIndex = people.IndexOf(who);
            bool still = whoIndex >= 0 && whoIndex < MostPeople && _moving[whoIndex] < StillEnough;

            // Off anyone standing close by a foot, a step or a turn at a time, with no foot ever going nearer them, and if there's no such way, it stays as it is and lets them go. Under its
            // belly they're between its feet, and it needn't move at all.
            if (still && giant.Doing != GiantDoing.Shuffling && giant.StepOffAgain <= 0f && NearestFoot(giant.Shape, giant.Middle, giant.Yaw, who) < StepOffAt * giant.Shape.Scale)
            {
                // Eating, it goes on with the same tree from where it's stepped to, if it still can.
                bool eating = giant.HasTree && (giant.Doing == GiantDoing.Browsing || giant.Doing is GiantDoing.Regarding or GiantDoing.Calling && giant.After == GiantDoing.Browsing);
                if (TryShuffles(giant, null, 0f, who))
                {
                    giant.HasTree = eating;
                    giant.After = eating ? GiantDoing.Browsing : GiantDoing.Standing;
                    giant.Watching = who;
                    Report?.Invoke($"giant {Members.IndexOf(giant)} steps off someone at {who.X:0},{who.Y:0} in {giant.Shuffles.Count} moves");
                    return;
                }
                giant.StepOffAgain = 3f;
            }

            if (distance > RegardReach * giant.Shape.Scale || giant.RegardAgain > 0f) return;
            if (giant.Doing is GiantDoing.Standing or GiantDoing.Browsing || giant.Doing == GiantDoing.Walking && Blocked(giant, people)) StartRegarding(giant, who);
        }

        // Whoever's nearest a foot, for the body to keep its feet off, wherever its legs happen to be: a foot planted off where it would stand at its ease still comes down clear of them.
        private static void KeepFeetOff(Giant giant, ReadOnlySpan<Vector2> people)
        {
            giant.KeepOffRadius = 0f;
            float nearest = KeepOffReach * giant.Shape.Scale;
            foreach (Vector2 who in people)
            {
                float distance = NearestFoot(giant.Shape, giant.Middle, giant.Yaw, who);
                if (distance >= nearest) continue;
                nearest = distance;
                giant.KeepOff = who;
                giant.KeepOffRadius = KeepOffRadius * giant.Shape.Scale;
            }
        }

        // Where a foot stands at its ease, for a body standing here and facing this way.
        private static Vector2 FootAt(in GiantShape shape, Vector2 middle, float yaw, int foot)
        {
            Vector2 forward = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
            Vector2 left = new Vector2(forward.Y, -forward.X);
            Vector2 root = middle - forward * shape.Middle;
            return root + left * (foot % 2 == 0 ? shape.FootX : -shape.FootX) + forward * (foot < 2 ? shape.HindZ : shape.ForeZ);
        }

        private static float NearestFoot(in GiantShape shape, Vector2 middle, float yaw, Vector2 who)
        {
            float nearest = float.MaxValue;
            for (int foot = 0; foot < 4; foot++) nearest = MathF.Min(nearest, (FootAt(shape, middle, yaw, foot) - who).Length);
            return nearest;
        }

        // Whether going from one stance to the other takes any foot nearer someone that's close to them, or brings one close: the one rule every step near anyone standing still keeps.
        private static bool Nears(in GiantShape shape, Vector2 middle, float yaw, Vector2 nextMiddle, float nextYaw, Vector2 who)
        {
            float reach = FootClear * shape.Scale;
            for (int foot = 0; foot < 4; foot++)
            {
                float then = (FootAt(shape, nextMiddle, nextYaw, foot) - who).Length;
                if (then < reach && then < (FootAt(shape, middle, yaw, foot) - who).Length - 1e-4f) return true;
            }
            return false;
        }

        // Whether anyone is close by its tail, from its hips back to its end, which any turn sweeps round.
        private static bool ByTail(Giant giant, ReadOnlySpan<Vector2> people)
        {
            foreach (Vector2 who in people)
            {
                if (ByTail(giant, who)) return true;
            }
            return false;
        }

        private static bool ByTail(Giant giant, Vector2 who)
        {
            Vector2 hips = giant.Root;
            Vector2 back = -giant.Forward * giant.Shape.TailBack;
            float t = Math.Clamp(Vector2.Dot(who - hips, back) / MathF.Max(back.LengthSquared, 1e-6f), 0f, 1f);
            return (hips + back * t - who).Length < TailClear * giant.Shape.Scale;
        }

        private static bool AnyUnderFoot(Giant giant, ReadOnlySpan<Vector2> people, float wider)
        {
            foreach (Vector2 who in people)
            {
                if (UnderFoot(giant, who, wider)) return true;
            }
            return false;
        }

        private static bool UnderFoot(Giant giant, Vector2 who, float wider)
        {
            Vector2 root = giant.Root;
            Vector2 forward = giant.Forward;
            Vector2 left = new Vector2(forward.Y, -forward.X);
            GiantShape shape = giant.Shape;
            for (int foot = 0; foot < 4; foot++)
            {
                Vector2 at = root + left * (foot % 2 == 0 ? shape.FootX : -shape.FootX) + forward * (foot < 2 ? shape.HindZ : shape.ForeZ);
                if ((who - at).Length < (StandAside * shape.Scale + 1.5f) * wider) return true;
            }
            return false;
        }

        private static float Nearest(ReadOnlySpan<Vector2> people, Vector2 from, out Vector2 who)
        {
            who = from;
            float best = float.MaxValue;
            foreach (Vector2 one in people)
            {
                float d = (one - from).Length;
                if (d >= best) continue;
                best = d;
                who = one;
            }
            return best;
        }
    }
}
