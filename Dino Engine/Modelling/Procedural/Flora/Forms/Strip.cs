using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>How a bent ribbon is drawn.</summary>
    public struct StripStyle
    {
        public int Material;
        public Vector2 UvMin;
        public Vector2 UvMax;
        public float Fold;                  // How far its edges stand up from its spine, as a share of its width, so that it's a V and not a flat sheet.
        public int Across;                  // Columns. 1 is a flat ribbon; 2 has a crease.
        public Vector3 Centre;              // What its normals lean away from, as a card's do.
        public float Shell;                 // How far they do, 0 to 1.
        public PlantJoint Joint;
        public int Links;                   // How many joints it bends on.
        public int ParentJoint;
    }

    /// <summary>
    /// A leaf, a blade or a frond: a ribbon along a curve. Anything that arches is a chain of joints, or the side of it stretches like the outside of a bend, so the ribbon binds itself
    /// to one. All the long thin things in the world are this, from a blade of pampas to a palm frond.
    /// </summary>
    internal static class Strip
    {
        /// <param name="spine">Where it runs, from foot to tip.</param>
        /// <param name="halfWidth">How wide it is at a point along it, 0 to 1.</param>
        /// <param name="side">Which way across it is at its foot. It turns with the spine from there.</param>
        /// <param name="colour">Its colour, at a point along it and how far off the spine, -1 to 1.</param>
        public static int Add(FloraMesh mesh, IReadOnlyList<Vector3> spine, Func<float, float> halfWidth, Vector3 side, in StripStyle style, Func<float, float, Vector3> colour)
        {
            int count = spine.Count;
            if (count < 2) return style.ParentJoint;

            // How far along each point is.
            var along = new float[count];
            for (int i = 1; i < count; i++) along[i] = along[i - 1] + (spine[i] - spine[i - 1]).Length;
            float length = MathF.Max(along[count - 1], 1e-4f);

            int links = Math.Clamp(style.Links, 1, count - 1);
            int firstLink = mesh.Joints.Count;
            var points = new Vector3[links + 1];
            for (int i = 0; i <= links; i++) points[i] = PointAt(spine, along, length * i / links);
            PlantJoint[] chain = style.Joint.Chain(points);
            int parent = style.ParentJoint;
            for (int i = 0; i < chain.Length; i++)
            {
                chain[i].Parent = parent;
                parent = mesh.AddJoint(chain[i]);
            }

            int across = Math.Max(style.Across, 1);
            int row = across + 1;
            int first = mesh.VertexCount;
            Vector3 running = side;
            for (int i = 0; i < count; i++)
            {
                float t = along[i] / length;
                Vector3 forward = (i + 1 < count ? spine[i + 1] - spine[i] : spine[i] - spine[i - 1]);
                forward = forward.LengthSquared > 1e-9f ? forward.Normalized() : Vector3.UnitY;
                running = (running - forward * Vector3.Dot(running, forward));
                running = running.LengthSquared > 1e-8f ? running.Normalized() : Scaffold.Perpendicular(forward);
                Vector3 normal = Vector3.Cross(running, forward).Normalized();
                float half = halfWidth(t);
                int joint = firstLink + Math.Min((int)(t * links), links - 1);

                for (int c = 0; c <= across; c++)
                {
                    float u = (float)c / across;
                    float x = u * 2f - 1f;
                    Vector3 at = spine[i] + running * (x * half) + normal * (MathF.Abs(x) * half * style.Fold);
                    Vector3 facing = (normal - running * (x * style.Fold)).Normalized();
                    if (style.Shell > 0f)
                    {
                        Vector3 away = at - style.Centre;
                        if (away.LengthSquared > 1e-6f) facing = Vector3.Lerp(facing, (away.Normalized() + Vector3.UnitY * 0.4f).Normalized(), style.Shell).Normalized();
                    }
                    Vector3 tangent = running - facing * Vector3.Dot(running, facing);
                    tangent = tangent.LengthSquared > 1e-8f ? tangent.Normalized() : Scaffold.Perpendicular(facing);

                    mesh.Add(new FloraVertex
                    {
                        Position = at,
                        Normal = facing,
                        Tangent = tangent,
                        Colour = colour(t, x),
                        UV = new Vector2(MathHelper.Lerp(style.UvMin.X, style.UvMax.X, u), MathHelper.Lerp(style.UvMin.Y, style.UvMax.Y, t)),
                        Material = style.Material,
                        Joint = joint,
                    });
                }
            }

            for (int i = 0; i + 1 < count; i++)
            {
                for (int c = 0; c < across; c++)
                {
                    int a = first + i * row + c;
                    mesh.Quad(a, a + 1, a + row + 1, a + row);
                }
            }
            return parent;
        }

        private static Vector3 PointAt(IReadOnlyList<Vector3> spine, float[] along, float wanted)
        {
            for (int i = 1; i < spine.Count; i++)
            {
                if (along[i] < wanted) continue;
                float step = along[i] - along[i - 1];
                return Vector3.Lerp(spine[i - 1], spine[i], step > 1e-6f ? (wanted - along[i - 1]) / step : 0f);
            }
            return spine[^1];
        }

        /// <summary>A curve that rises and arches over: it starts <paramref name="lean"/> radians from straight up and bends by <paramref name="arch"/> more by its tip, in the plane of <paramref name="outward"/>.</summary>
        public static List<Vector3> Arc(Vector3 from, Vector3 outward, float length, float lean, float arch, int segments, float curl = 1.5f)
        {
            var points = new List<Vector3>(segments + 1) { from };
            float step = length / segments;
            Vector3 at = from;
            for (int i = 1; i <= segments; i++)
            {
                float t = (i - 0.5f) / segments;
                float angle = lean + arch * MathF.Pow(t, curl);
                at += (Vector3.UnitY * MathF.Cos(angle) + outward * MathF.Sin(angle)) * step;
                points.Add(at);
            }
            return points;
        }

        /// <summary>A round stem, thin enough that three sides do. For what only has a stem to hold something up.</summary>
        public static void Stalk(FloraMesh mesh, IReadOnlyList<Vector3> spine, float radiusFoot, float radiusTip, int material, Vector3 colour, int joint)
        {
            const int Sides = 3;
            int first = mesh.VertexCount;
            int count = spine.Count;
            Vector3 side = Scaffold.Perpendicular((spine[1] - spine[0]).Normalized());
            float travelled = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 forward = (i + 1 < count ? spine[i + 1] - spine[i] : spine[i] - spine[i - 1]);
                forward = forward.LengthSquared > 1e-9f ? forward.Normalized() : Vector3.UnitY;
                side = (side - forward * Vector3.Dot(side, forward)).Normalized();
                Vector3 other = Vector3.Cross(forward, side);
                float radius = MathHelper.Lerp(radiusFoot, radiusTip, (float)i / (count - 1));
                if (i > 0) travelled += (spine[i] - spine[i - 1]).Length;
                for (int s = 0; s <= Sides; s++)
                {
                    float angle = s * (MathF.Tau / Sides);
                    Vector3 outward = side * MathF.Cos(angle) + other * MathF.Sin(angle);
                    mesh.Add(new FloraVertex
                    {
                        Position = spine[i] + outward * radius,
                        Normal = outward,
                        Tangent = -side * MathF.Sin(angle) + other * MathF.Cos(angle),
                        Colour = colour,
                        UV = new Vector2((float)s / Sides, travelled),
                        Material = material,
                        Joint = joint,
                    });
                }
            }
            const int Row = Sides + 1;
            for (int i = 0; i + 1 < count; i++)
            {
                for (int s = 0; s < Sides; s++)
                {
                    int a = first + i * Row + s;
                    mesh.Quad(a, a + 1, a + Row + 1, a + Row);
                }
            }
        }
    }
}
