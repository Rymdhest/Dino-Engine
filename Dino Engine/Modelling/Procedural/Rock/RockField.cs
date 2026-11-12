using Dino_Engine.Modelling.Procedural.Flora;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.Modelling.Procedural.Rock
{
    /// <summary>A half-space: everything behind a fracture. Rock is what's left when these have been taken away from a block.</summary>
    internal readonly record struct Fracture(Vector3 Normal, float Offset);

    /// <summary>A crack that runs in from the surface, the same width all the way and ending in a floor. Tapered to nothing it would have to pass through being narrower than the grid can hold.</summary>
    internal readonly record struct Crack(Vector3 Normal, float Offset, float Width, float Depth);

    /// <summary>
    /// One solid piece: the smooth intersection of half-spaces, as a block of rock is what the fractures around it have left (Paris et al., "Modeling Rocky Scenery using Implicit Blocks").
    /// Only the rounding is more than a plain intersection, and that's what weathering has done to the edges.
    /// </summary>
    internal sealed class RockBody
    {
        public readonly List<Fracture> Fractures = new();
        public float Rounding;

        public float Distance(Vector3 p)
        {
            float d = float.NegativeInfinity;
            foreach (Fracture f in Fractures)
            {
                float plane = Vector3.Dot(p, f.Normal) - f.Offset;
                d = float.IsNegativeInfinity(d) ? plane : Intersect(d, plane, Rounding);
            }
            return d;
        }

        // The smooth maximum of Bærentzen's, as that paper has it: it agrees with the plain one until two faces come within the radius of each other.
        private static float Intersect(float a, float b, float radius)
        {
            float k = MathF.Max(radius - MathF.Abs(a - b), 0f) / MathF.Max(radius, 1e-5f);
            return MathF.Max(a, b) + k * k * k * radius * (1f / 6f);
        }
    }

    /// <summary>
    /// A rock mass in the joints that cut it: planes along three directions in a frame of their own, sheared by how the rock was folded. Each layer is shifted along, so that joints don't line
    /// up through it like bricks in a bad wall, every joint lies a third of a spacing either side of where it would evenly be, so that no two beds or blocks are the same size, as evenly
    /// spaced ones read as masonry, and every block in it stands back from the mass by its own amount. That's the whole of how a crag looks like a pile of blocks, for one lookup a point.
    /// </summary>
    internal sealed class JointLattice
    {
        public Vector3 Along = Vector3.UnitX;   // The joint set each axis runs across.
        public Vector3 Across = Vector3.UnitZ;
        public Vector3 Up = Vector3.UnitY;      // Normal of the beds.
        public Vector3 Spacing = Vector3.One;   // Metres between joints, in the order along, across, up.
        public float Gap = 0.05f;               // Metres of open joint at the surface.
        public float Rounding = 0.05f;
        public float Retreat = 0.4f;            // The most a block stands back from the mass.
        public float Depth = 1f;                // How far in from the surface the joints stay open.
        public float BedRecess;                 // How far the soft beds wear back, for rock laid down in layers.
        public uint Seed;

        public float Distance(Vector3 p, float envelope, out float retreat)
        {
            float w = Vector3.Dot(p, Up) / Spacing.Z;
            Interval(w, Seed ^ 0x2545F491u, out int layer, out float wLow, out float wHigh);
            uint layerHash = FloraNoise.Hash((uint)layer * 0x9E3779B1u ^ Seed);

            // A bed stands out or lies back as a whole, and its joints fall where they will.
            float u = Vector3.Dot(p, Along) / Spacing.X + (layerHash & 0xFFFF) / 65535f;
            float v = Vector3.Dot(p, Across) / Spacing.Y + (layerHash >> 16) / 65535f;
            Interval(u, layerHash ^ 0x68E31DA4u, out int column, out float uLow, out float uHigh);
            Interval(v, layerHash ^ 0xB5297A4Du, out int row, out float vLow, out float vHigh);

            uint hash = FloraNoise.Hash((uint)column * 0x85EBCA6Bu ^ (uint)row * 0xC2B2AE35u ^ layerHash);
            float own = (hash & 0xFFFF) / 65535f;
            // Now and then a block has gone altogether, which is what a notch or an overhang is.
            float missing = ((hash >> 16) & 0xFF) < 26 ? 1.8f : 1f;
            float bed = ((layerHash >> 8) & 0xFF) / 255f;
            retreat = Retreat * own * own * missing + BedRecess * bed * bed;

            float x = (u - (uLow + uHigh) * 0.5f) * Spacing.X;
            float y = (v - (vLow + vHigh) * 0.5f) * Spacing.Y;
            float z = (w - (wLow + wHigh) * 0.5f) * Spacing.Z;
            float qx = MathF.Abs(x) - ((uHigh - uLow) * 0.5f * Spacing.X - Gap - Rounding);
            float qy = MathF.Abs(y) - ((vHigh - vLow) * 0.5f * Spacing.Y - Gap - Rounding);
            float qz = MathF.Abs(z) - ((wHigh - wLow) * 0.5f * Spacing.Z - Gap - Rounding);
            float outside = new Vector3(MathF.Max(qx, 0f), MathF.Max(qy, 0f), MathF.Max(qz, 0f)).Length;
            return outside + MathF.Min(MathF.Max(qx, MathF.Max(qy, qz)), 0f) - Rounding;
        }

        // Which gap between joints a coordinate falls in, counting in spacings: joint k lies a third of a spacing either side of k, so the gaps run from 0.4 of a spacing to 1.6.
        private static void Interval(float t, uint seed, out int index, out float low, out float high)
        {
            int k = (int)MathF.Floor(t);
            float b0 = JointAt(k, seed);
            float b1 = JointAt(k + 1, seed);
            if (t < b0) { index = k - 1; low = JointAt(k - 1, seed); high = b0; }
            else if (t < b1) { index = k; low = b0; high = b1; }
            else { index = k + 1; low = b1; high = JointAt(k + 2, seed); }
        }

        private static float JointAt(int k, uint seed) => k + ((FloraNoise.Hash((uint)k * 0x27D4EB2Fu ^ seed) & 0xFFFF) / 65535f - 0.5f) * 0.6f;
    }

    /// <summary>
    /// Columns of basalt: hexagonal prisms on a lattice, each its own height, standing apart by a narrow joint, as lava that cooled slowly cracks. Worked out from the few columns
    /// round a point, not by going through all of them, as a stand of them is dozens.
    /// </summary>
    internal sealed class ColumnSet
    {
        public float Width = 1f;        // Metres between the middles of neighbouring columns.
        public float Gap = 0.1f;        // Metres open between them.
        public float Reach = 3f;        // Metres from the middle the stand reaches.
        public float Low, High;         // The heights of the tops of the shortest columns, at the stand's edge, and of the tallest.
        public float Bottom = -3f;      // The floor every column goes down to.
        public float Tilt = 0.08f;      // How far a top leans, as a slope.
        public uint Seed;

        private static readonly Vector2[] Around =
        {
            new(0f, 0f), new(1f, 0f), new(-1f, 0f), new(0.5f, 0.8660254f), new(-0.5f, 0.8660254f), new(0.5f, -0.8660254f), new(-0.5f, -0.8660254f),
        };

        public float Distance(Vector3 p)
        {
            Vector2 h = p.Xz / Width;
            Vector2 s = new Vector2(1f, 1.7320508f);
            Vector2 a = Mod(h, s) - s * 0.5f;
            Vector2 b = Mod(h - s * 0.5f, s) - s * 0.5f;
            Vector2 centre = h - (Vector2.Dot(a, a) < Vector2.Dot(b, b) ? a : b);
            float inner = Width * 0.5f - Gap * 0.5f;
            float d = float.MaxValue;
            foreach (Vector2 step in Around)
            {
                Vector2 middle = (centre + step) * Width;
                float out_ = middle.Length / Reach;
                if (out_ > 1f) continue;
                uint hash = FloraNoise.Hash((uint)(int)MathF.Round(middle.X * 8f) * 0x9E3779B1u ^ FloraNoise.Hash((uint)(int)MathF.Round(middle.Y * 8f)) ^ Seed);
                float rise = (1f - out_ * out_) * (0.55f + 0.45f * ((hash & 0xFFFF) / 65535f));
                float top = Low + (High - Low) * rise;
                Vector2 lean = new Vector2(((hash >> 16) & 0xFF) / 255f - 0.5f, ((hash >> 24) & 0xFF) / 255f - 0.5f) * (2f * Tilt);
                Vector2 q = p.Xz - middle;
                float side = MathF.Max(MathF.Abs(q.X), MathF.Max(MathF.Abs(q.X * 0.5f + q.Y * 0.8660254f), MathF.Abs(q.X * 0.5f - q.Y * 0.8660254f))) - inner;
                float cap = p.Y - (top + Vector2.Dot(q, lean));
                float floor = Bottom - p.Y;
                d = MathF.Min(d, MathF.Max(side, MathF.Max(cap, floor)));
            }
            return d;
        }

        private static Vector2 Mod(Vector2 x, Vector2 y) => new Vector2(x.X - y.X * MathF.Floor(x.X / y.X), x.Y - y.Y * MathF.Floor(x.Y / y.Y));
    }

    /// <summary>A rock as a signed distance, negative inside. Everything about its shape is here: <see cref="RockForge"/> only makes a mesh of it.</summary>
    internal sealed class RockField
    {
        public readonly List<RockBody> Bodies = new();
        public readonly List<Crack> Cracks = new();
        /// <summary>Ellipsoids taken out of the rock, the opening of an arch.</summary>
        public readonly List<(Vector3 Centre, Vector3 Radii)> Hollows = new();
        public ColumnSet? Columns;
        public JointLattice? Joints;
        public float Blend;             // How far apart bodies still run together.
        public float WarpSize = 1f;     // Metres to a wander of the joints, so that no fracture is a ruled line.
        public float WarpReach;
        public uint Seed;

        public float Distance(Vector3 p)
        {
            float mass = Mass(p);
            if (Joints != null)
            {
                // Far from the surface there's nothing to work out.
                if (mass > Joints.Retreat * 2.2f + Joints.Depth) return mass;
                Vector3 warped = p + Wander(p);
                float block = Joints.Distance(warped, mass, out float retreat);
                mass = MathF.Max(mass + retreat, MathF.Min(block, mass + Joints.Depth));
            }

            foreach (Crack crack in Cracks)
            {
                float plane = MathF.Abs(Vector3.Dot(p, crack.Normal) - crack.Offset);
                mass = MathF.Max(mass, MathF.Min(crack.Width - plane, mass + crack.Depth));
            }
            foreach ((Vector3 centre, Vector3 radii) in Hollows)
            {
                // A scaled sphere's distance, which is near enough to the ellipsoid's for a surface to be found by.
                Vector3 local = (p - centre) / radii;
                float hollow = (local.Length - 1f) * MathF.Min(radii.X, MathF.Min(radii.Y, radii.Z));
                mass = MathF.Max(mass, -hollow);
            }
            return mass;
        }

        /// <summary>
        /// Whether a ball around a point is certainly all rock or all air, which is most of space, and saves working out the joints there. Rock is never outside the mass, and joints only
        /// open <see cref="JointLattice.Depth"/> into it.
        /// </summary>
        public bool IsClear(Vector3 p, float radius, out float value)
        {
            value = Mass(p);
            if (value > radius) return true;
            float inward = radius + (Joints != null ? Joints.Depth + Joints.Retreat * 2f : 0f);
            foreach (Crack crack in Cracks) inward = MathF.Max(inward, radius + crack.Depth);
            // A hollow can open anywhere inside, so nowhere near one is certainly rock.
            foreach ((Vector3 centre, Vector3 radii) in Hollows)
            {
                if ((p - centre).Length < MathF.Max(radii.X, MathF.Max(radii.Y, radii.Z)) + radius) return false;
            }
            return value < -inward;
        }

        private float Mass(Vector3 p)
        {
            float d = Columns != null ? Columns.Distance(p) : float.PositiveInfinity;
            foreach (RockBody body in Bodies)
            {
                float b = body.Distance(p);
                d = float.IsPositiveInfinity(d) ? b : Blend > 1e-4f ? SmoothMin(d, b, Blend) : MathF.Min(d, b);
            }
            return d;
        }

        private Vector3 Wander(Vector3 p)
        {
            if (WarpReach <= 0f) return Vector3.Zero;
            Vector3 at = p / WarpSize;
            return new Vector3(
                FloraNoise.Value(at, Seed) - 0.5f,
                FloraNoise.Value(at, Seed + 31u) - 0.5f,
                FloraNoise.Value(at, Seed + 77u) - 0.5f) * (WarpReach * 2f);
        }
    }
}
