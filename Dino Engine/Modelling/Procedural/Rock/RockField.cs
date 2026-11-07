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
    /// up through it like bricks in a bad wall, and every block in it stands back from the mass by its own amount. That's the whole of how a crag looks like a pile of blocks, for one lookup a point.
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
            int layer = (int)MathF.Floor(w);
            uint layerHash = FloraNoise.Hash((uint)layer * 0x9E3779B1u ^ Seed);

            // A bed stands out or lies back as a whole, and its joints fall where they will.
            float u = Vector3.Dot(p, Along) / Spacing.X + (layerHash & 0xFFFF) / 65535f;
            float v = Vector3.Dot(p, Across) / Spacing.Y + (layerHash >> 16) / 65535f;
            int column = (int)MathF.Floor(u);
            int row = (int)MathF.Floor(v);

            uint hash = FloraNoise.Hash((uint)column * 0x85EBCA6Bu ^ (uint)row * 0xC2B2AE35u ^ layerHash);
            float own = (hash & 0xFFFF) / 65535f;
            // Now and then a block has gone altogether, which is what a notch or an overhang is.
            float missing = ((hash >> 16) & 0xFF) < 26 ? 1.8f : 1f;
            float bed = ((layerHash >> 8) & 0xFF) / 255f;
            retreat = Retreat * own * own * missing + BedRecess * bed * bed;

            float x = (u - column - 0.5f) * Spacing.X;
            float y = (v - row - 0.5f) * Spacing.Y;
            float z = (w - layer - 0.5f) * Spacing.Z;
            float qx = MathF.Abs(x) - (Spacing.X * 0.5f - Gap - Rounding);
            float qy = MathF.Abs(y) - (Spacing.Y * 0.5f - Gap - Rounding);
            float qz = MathF.Abs(z) - (Spacing.Z * 0.5f - Gap - Rounding);
            float outside = new Vector3(MathF.Max(qx, 0f), MathF.Max(qy, 0f), MathF.Max(qz, 0f)).Length;
            return outside + MathF.Min(MathF.Max(qx, MathF.Max(qy, qz)), 0f) - Rounding;
        }
    }

    /// <summary>A rock as a signed distance, negative inside. Everything about its shape is here: <see cref="RockForge"/> only makes a mesh of it.</summary>
    internal sealed class RockField
    {
        public readonly List<RockBody> Bodies = new();
        public readonly List<Crack> Cracks = new();
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
            return value < -inward;
        }

        private float Mass(Vector3 p)
        {
            float d = float.PositiveInfinity;
            foreach (RockBody body in Bodies)
            {
                float b = body.Distance(p);
                d = float.IsPositiveInfinity(d) ? b : SmoothMin(d, b, Blend);
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
