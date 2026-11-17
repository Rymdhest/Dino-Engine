using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Wildlife
{
    /// <summary>
    /// How high anything standing reaches over each metre of the land, from the real shape of every model on it (<see cref="ModelTops"/>): roofs, towers, crowns, stones, walls. It's what anything
    /// flying keeps over (<see cref="LifeLand.Overhead"/>), as a flock that went through a house or a tree was seen to, and it looked cheap.
    /// It's worked out a sector at a time round the view, nearest first and a few a step, and a sector one of whose models hasn't been made yet is looked at again until it has, as a model
    /// is only made once it's near enough to be drawn. Every model is laid in by the highest it reaches, so laying one twice changes nothing.
    /// </summary>
    public sealed class SkyFloor
    {
        private const float Cell = 1f;
        private const int Side = (int)(WorldObjects.SectorSize / Cell);
        private const float Reach = 96f;            // Metres round the view that's kept, as a flock passing over comes in from well outside it.
        private const int SectorsPerStep = 6;
        private const float InViewMargin = 24f;     // Metres past the view that's laid in at once.
        private const float LookAgain = 0.75f;      // Seconds before a sector still waiting for a model is looked at again.

        private enum Read : byte { Never, Waiting, Done }

        private readonly WorldObjects _objects;
        private readonly WorldData _world;
        private readonly List<Perch>?[] _perches;
        private const float LowestPerch = 3f;
        private const float NookReach = 1.5f, NookRise = 2.6f;  // Metres round a perch, and over it, that what's near may reach: a roof rising behind its eave does, a tower doesn't.       // Metres over the ground, under which an edge isn't a roof a bird would sit up on.
        private readonly int _sectors;
        private readonly float[]?[] _tops;
        private readonly Read[] _read;
        private readonly float[] _lookedAt;
        private readonly List<int> _wanted = new();
        private readonly Dictionary<int, bool[]> _laid = new();     // For a sector still waiting, which of its objects are in already.
        private readonly int[] _versionAt;
        private float _now;

        public SkyFloor(WorldData world)
        {
            _objects = world.Objects;
            _world = world;
            _perches = new List<Perch>?[world.Objects.SectorsPerSide * world.Objects.SectorsPerSide];
            _sectors = _objects.SectorsPerSide;
            _tops = new float[]?[_sectors * _sectors];
            _read = new Read[_sectors * _sectors];
            _lookedAt = new float[_sectors * _sectors];
            _versionAt = new int[_sectors * _sectors];
        }

        /// <summary>The top of whatever stands over a point, in the world's heights, or negative infinity where nothing does.</summary>
        public float At(Vector2 at)
        {
            if (at.X < 0f || at.Y < 0f) return float.NegativeInfinity;
            int cx = (int)(at.X / Cell), cz = (int)(at.Y / Cell);
            int sx = cx / Side, sz = cz / Side;
            if (sx >= _sectors || sz >= _sectors) return float.NegativeInfinity;
            float[]? tops = _tops[sz * _sectors + sx];
            if (tops == null) return float.NegativeInfinity;
            return tops[(cz - sz * Side) * Side + (cx - sx * Side)];
        }

        /// <summary>The highest of anything within <paramref name="radius"/> metres of a point, for something with a span of its own.</summary>
        public float Around(Vector2 at, float radius)
        {
            float top = float.NegativeInfinity;
            int steps = Math.Max(1, (int)MathF.Ceiling(radius / Cell));
            float step = radius / steps;
            for (int z = -steps; z <= steps; z++)
            {
                for (int x = -steps; x <= steps; x++)
                {
                    top = MathF.Max(top, At(at + new Vector2(x * step, z * step)));
                }
            }
            return top;
        }

        /// <summary>Lays in what's near the view and hasn't been yet.</summary>
        public void Refresh(Vector2 least, Vector2 most, float deltaTime)
        {
            _now += deltaTime;
            Vector2i from = _objects.SectorOf(least - new Vector2(Reach));
            Vector2i to = _objects.SectorOf(most + new Vector2(Reach));
            Vector2 middle = (least + most) * 0.5f;

            _wanted.Clear();
            for (int z = from.Y; z <= to.Y; z++)
            {
                for (int x = from.X; x <= to.X; x++)
                {
                    int index = z * _sectors + x;
                    if (_read[index] == Read.Done) continue;
                    // What's in view is laid in at once, as what lives there is put down by it.
                    if (_read[index] == Read.Never && InView(index, least, most))
                    {
                        LayIn(index);
                        continue;
                    }
                    // Only once another model's been made since it was last looked at.
                    if (_read[index] == Read.Waiting && (_now - _lookedAt[index] < LookAgain || _versionAt[index] == ModelTops.Version)) continue;
                    _wanted.Add(index);
                }
            }
            if (_wanted.Count == 0) return;

            // Nearest first, picked out a few at a time rather than sorted, as sorting by a measure of a moving point makes garbage each step.
            int count = Math.Min(_wanted.Count, SectorsPerStep);
            for (int i = 0; i < count; i++)
            {
                int best = i;
                float nearest = Distance(_wanted[i], middle);
                for (int j = i + 1; j < _wanted.Count; j++)
                {
                    float distance = Distance(_wanted[j], middle);
                    if (distance >= nearest) continue;
                    nearest = distance;
                    best = j;
                }
                (_wanted[i], _wanted[best]) = (_wanted[best], _wanted[i]);
                LayIn(_wanted[i]);
            }
        }

        /// <summary>Lays in every sector at once, for a lab, which has every model made beforehand.</summary>
        public void LayInAll()
        {
            for (int index = 0; index < _read.Length; index++)
            {
                if (_read[index] != Read.Done) LayIn(index);
            }
        }

        private bool InView(int index, Vector2 least, Vector2 most)
        {
            float x = index % _sectors * WorldObjects.SectorSize, z = index / _sectors * WorldObjects.SectorSize;
            return x + WorldObjects.SectorSize > least.X - InViewMargin && z + WorldObjects.SectorSize > least.Y - InViewMargin && x < most.X + InViewMargin && z < most.Y + InViewMargin;
        }

        private float Distance(int index, Vector2 middle)
        {
            var centre = new Vector2((index % _sectors + 0.5f) * WorldObjects.SectorSize, (index / _sectors + 0.5f) * WorldObjects.SectorSize);
            return (centre - middle).LengthSquared;
        }

        private void LayIn(int index)
        {
            _lookedAt[index] = _now;
            _versionAt[index] = ModelTops.Version;
            IReadOnlyList<WorldObject>? items = _objects.InSector(new Vector2i(index % _sectors, index / _sectors));
            bool waiting = false;
            if (items != null)
            {
                _laid.TryGetValue(index, out bool[]? laid);
                for (int i = 0; i < items.Count; i++)
                {
                    if (laid != null && laid[i]) continue;
                    WorldObject item = items[i];
                    if (!ModelTops.TryGet(_objects.AssetIds[item.Asset], out ModelTop? top, out _))
                    {
                        waiting = true;
                        continue;
                    }
                    if (top != null)
                    {
                        Lay(top, item);
                        AddPerches(top, item);
                    }
                    if (laid == null)
                    {
                        laid = new bool[items.Count];
                        _laid[index] = laid;
                    }
                    laid[i] = true;
                }
            }
            _read[index] = waiting ? Read.Waiting : Read.Done;
            if (!waiting) _laid.Remove(index);
        }

        /// <summary>Somewhere on the edge of a roof in the world a bird could sit: where its feet go, and the way it faces, out over the drop.</summary>
        public readonly struct Perch
        {
            public readonly Vector3 At;
            public readonly Vector2 Out;

            public Perch(Vector3 at, Vector2 outward)
            {
                At = at;
                Out = outward;
            }
        }

        /// <summary>Adds every roof's edge laid in so far within <paramref name="radius"/> of a point, with nothing standing over it.</summary>
        public void PerchesNear(Vector2 at, float radius, List<Perch> into)
        {
            Vector2i from = _objects.SectorOf(at - new Vector2(radius)), to = _objects.SectorOf(at + new Vector2(radius));
            for (int z = from.Y; z <= to.Y; z++)
            {
                for (int x = from.X; x <= to.X; x++)
                {
                    List<Perch>? perches = _perches[z * _sectors + x];
                    if (perches == null) continue;
                    for (int i = 0; i < perches.Count; i++)
                    {
                        Perch perch = perches[i];
                        if ((perch.At.Xz - at).LengthSquared > radius * radius) continue;
                        // The cell's top is the highest of anything over it, which a roof rising from its eave makes up to a metre more: anything higher is something over the edge.
                        if (At(perch.At.Xz) > perch.At.Y + 1.3f) continue;
                        // And nothing much taller close behind, as an edge tucked under a tower or a higher wall is a nook a bird would have to fly out of past it.
                        if (Around(perch.At.Xz, NookReach) > perch.At.Y + NookRise) continue;
                        into.Add(perch);
                    }
                }
            }
        }

        private void AddPerches(ModelTop top, in WorldObject item)
        {
            for (int i = 0; i < top.Perches.Length; i++)
            {
                RoofPerch roof = top.Perches[i];
                Vector3 at = item.Position + Vector3.Transform(roof.At * item.Scale, item.Rotation);
                if (!_world.InBounds(at.Xz) || at.Y - _world.Height.Sample(at.Xz) < LowestPerch) continue;
                Vector2 outward = Vector3.Transform(new Vector3(roof.Out.X * MathF.Sign(item.Scale.X), 0f, roof.Out.Y * MathF.Sign(item.Scale.Z)), item.Rotation).Xz;
                if (outward.LengthSquared < 1e-4f) continue;
                Vector2i sector = _objects.SectorOf(at.Xz);
                (_perches[sector.Y * _sectors + sector.X] ??= new List<Perch>()).Add(new Perch(at, outward.Normalized()));
            }
        }

        // Each cell of the model, turned and stretched as the object is, raises every cell of the land its corners reach over to its top.
        private void Lay(ModelTop top, in WorldObject item)
        {
            Quaternion turn = item.Rotation;
            Vector3 scale = item.Scale;
            Vector3 origin = item.Position;
            for (int z = 0; z < top.Depth; z++)
            {
                for (int x = 0; x < top.Width; x++)
                {
                    float h = top.Top[z * top.Width + x];
                    if (float.IsNegativeInfinity(h)) continue;

                    float x0 = top.MinX + x * top.Cell, z0 = top.MinZ + z * top.Cell;
                    float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue, high = float.MinValue;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        var local = new Vector3((x0 + (corner & 1) * top.Cell) * scale.X, h * scale.Y, (z0 + (corner >> 1) * top.Cell) * scale.Z);
                        Vector3 world = origin + Vector3.Transform(local, turn);
                        minX = MathF.Min(minX, world.X);
                        maxX = MathF.Max(maxX, world.X);
                        minZ = MathF.Min(minZ, world.Z);
                        maxZ = MathF.Max(maxZ, world.Z);
                        high = MathF.Max(high, world.Y);
                    }
                    Raise(minX, minZ, maxX, maxZ, high);
                }
            }
        }

        private void Raise(float minX, float minZ, float maxX, float maxZ, float high)
        {
            int x0 = Math.Max(0, (int)MathF.Floor(minX / Cell)), x1 = Math.Min(_sectors * Side - 1, (int)MathF.Floor(maxX / Cell));
            int z0 = Math.Max(0, (int)MathF.Floor(minZ / Cell)), z1 = Math.Min(_sectors * Side - 1, (int)MathF.Floor(maxZ / Cell));
            for (int z = z0; z <= z1; z++)
            {
                int sz = z / Side;
                for (int x = x0; x <= x1; x++)
                {
                    int sx = x / Side;
                    int sector = sz * _sectors + sx;
                    float[]? tops = _tops[sector];
                    if (tops == null)
                    {
                        tops = new float[Side * Side];
                        Array.Fill(tops, float.NegativeInfinity);
                        _tops[sector] = tops;
                    }
                    int cell = (z - sz * Side) * Side + (x - sx * Side);
                    if (high > tops[cell]) tops[cell] = high;
                }
            }
        }
    }
}
