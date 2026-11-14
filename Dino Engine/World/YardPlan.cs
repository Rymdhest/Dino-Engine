using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>
    /// A churchyard's shape, which the game and the checks read rather than its site's radius (CHURCHYARD.md): a rectangle with its corners cut off, turned by <see cref="Yaw"/>, its
    /// local u running along the church from its tower to its altar and v towards its porch, which faces south, the way the camera looks from; and where its gates and church are.
    /// Headless.
    /// </summary>
    public sealed class YardPlan
    {
        public Vector2 Centre;
        public float Yaw;
        public float HalfX;         // Half its length along u, to the wall's line.
        public float HalfZ;         // Half its depth along v.
        public float Chamfer;       // How far back from each corner its wall is cut across.

        /// <summary>The middle of each gate's opening on the wall's line, and the way out through it.</summary>
        public readonly List<(Vector2 At, Vector2 Out)> Gates = new();

        /// <summary>Where the church stands, and the mouth of its porch, on the ground, if it stands at all.</summary>
        public Vector2? Church;
        public Vector2? Porch;

        /// <summary>The way along u, on the ground.</summary>
        public Vector2 Ax => new Vector2(MathF.Cos(Yaw), -MathF.Sin(Yaw));

        /// <summary>The way along v, on the ground: towards the porch.</summary>
        public Vector2 Az => new Vector2(MathF.Sin(Yaw), MathF.Cos(Yaw));

        public Vector2 ToWorld(float u, float v) => Centre + Ax * u + Az * v;
        public Vector2 ToWorld(Vector2 local) => ToWorld(local.X, local.Y);
        public Vector2 Dir(Vector2 local) => Ax * local.X + Az * local.Y;

        public Vector2 ToLocal(Vector2 world)
        {
            Vector2 d = world - Centre;
            return new Vector2(Vector2.Dot(d, Ax), Vector2.Dot(d, Az));
        }

        /// <summary>Whether a point in the yard's own frame is inside its wall's line, or within <paramref name="grow"/> of it outside.</summary>
        public bool InsideLocal(Vector2 local, float grow = 0f)
        {
            float u = MathF.Abs(local.X), v = MathF.Abs(local.Y);
            return u <= HalfX + grow && v <= HalfZ + grow && u + v <= HalfX + HalfZ - Chamfer + grow * 1.4142f;
        }

        public bool Inside(Vector2 world, float grow = 0f) => InsideLocal(ToLocal(world), grow);

        /// <summary>The farthest the wall gets from its middle.</summary>
        public float Reach => MathF.Max(new Vector2(HalfX, HalfZ - Chamfer).Length, new Vector2(HalfX - Chamfer, HalfZ).Length);
    }
}
