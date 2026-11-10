using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Waygates
{
    /// <summary>
    /// Dressed stone: a block of any eight corners, with every arris taken off. Each face is drawn in from its edges, each edge becomes a narrow bevel and each corner a small facet,
    /// so blocks laid face to face meet in a V that reads as a joint from far off, with nothing showing where they touch. Faces that touch inside a wall are hidden by the bevels round
    /// them, so nothing that can be seen ever lies in the same plane as anything else.
    /// </summary>
    internal static class Masonry
    {
        // A block's corners: the front four counter-clockwise seen from +z, then the back four behind them in the same order. Its faces, each counter-clockwise from outside.
        private static readonly int[][] Faces =
        {
            new[] { 0, 1, 2, 3 },
            new[] { 4, 7, 6, 5 },
            new[] { 1, 0, 4, 5 },
            new[] { 2, 1, 5, 6 },
            new[] { 3, 2, 6, 7 },
            new[] { 0, 3, 7, 4 },
        };

        // Each edge, and the faces either side of it.
        private static readonly (int From, int To, int FaceA, int FaceB)[] Edges =
        {
            (0, 1, 0, 2), (1, 2, 0, 3), (2, 3, 0, 4), (3, 0, 0, 5),
            (4, 5, 1, 2), (5, 6, 1, 3), (6, 7, 1, 4), (7, 4, 1, 5),
            (0, 4, 2, 5), (1, 5, 2, 3), (2, 6, 3, 4), (3, 7, 4, 5),
        };

        // The three faces at each corner.
        private static readonly int[][] Corners =
        {
            new[] { 0, 2, 5 }, new[] { 0, 2, 3 }, new[] { 0, 3, 4 }, new[] { 0, 4, 5 },
            new[] { 1, 2, 5 }, new[] { 1, 2, 3 }, new[] { 1, 3, 4 }, new[] { 1, 4, 5 },
        };

        /// <summary>A block from its eight corners, which must make a convex solid, with its arrises taken off <paramref name="chamfer"/> metres each way.</summary>
        public static void Block(MeshBuilder b, Vector3[] c, float chamfer, VertexMaterial material) => Block(b, c, chamfer, _ => material);

        /// <summary>A block as above, each face, bevel and corner in what <paramref name="facing"/> says for the way it faces, as a stone's top weathers and its face doesn't.</summary>
        public static void Block(MeshBuilder b, Vector3[] c, float chamfer, Func<Vector3, VertexMaterial> facing)
        {
            float shortest = float.MaxValue;
            foreach ((int from, int to, _, _) in Edges) shortest = MathF.Min(shortest, (c[to] - c[from]).Length);
            float cut = MathF.Min(chamfer, shortest * 0.3f);
            Vector3 middle = Vector3.Zero;
            for (int i = 0; i < 8; i++) middle += c[i];
            middle /= 8f;

            // Each face's corners, drawn in along both of its edges.
            var inset = new Vector3[6, 4];
            for (int f = 0; f < 6; f++)
            {
                int[] face = Faces[f];
                for (int k = 0; k < 4; k++)
                {
                    Vector3 at = c[face[k]];
                    Vector3 next = c[face[(k + 1) & 3]] - at;
                    Vector3 previous = c[face[(k + 3) & 3]] - at;
                    inset[f, k] = at + cut * (Unit(next) + Unit(previous));
                }
                Vector3 faceNormal = Unit(Vector3.Cross(inset[f, 2] - inset[f, 0], inset[f, 3] - inset[f, 1]));
                b.Quad(inset[f, 0], inset[f, 1], inset[f, 2], inset[f, 3], facing(faceNormal));
            }

            foreach ((int from, int to, int faceA, int faceB) in Edges)
            {
                Vector3 aFrom = inset[faceA, IndexIn(faceA, from)], aTo = inset[faceA, IndexIn(faceA, to)];
                Vector3 bFrom = inset[faceB, IndexIn(faceB, from)], bTo = inset[faceB, IndexIn(faceB, to)];
                Vector3 bevel = Unit((aFrom + aTo + bFrom + bTo) * 0.25f - middle);
                Outward(b, middle, aFrom, aTo, bTo, bFrom, facing(bevel));
            }

            for (int corner = 0; corner < 8; corner++)
            {
                int[] faces = Corners[corner];
                Vector3 p0 = inset[faces[0], IndexIn(faces[0], corner)];
                Vector3 p1 = inset[faces[1], IndexIn(faces[1], corner)];
                Vector3 p2 = inset[faces[2], IndexIn(faces[2], corner)];
                Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
                VertexMaterial material = facing(Unit((p0 + p1 + p2) / 3f - middle));
                if (Vector3.Dot(normal, (p0 + p1 + p2) / 3f - middle) < 0f) b.Triangle(p0, p2, p1, material);
                else b.Triangle(p0, p1, p2, material);
            }
        }

        /// <summary>A block square to the axes, from its least corner to its most.</summary>
        public static void Box(MeshBuilder b, Vector3 min, Vector3 max, float chamfer, VertexMaterial material)
        {
            Block(b, new[]
            {
                new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z),
                new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, max.Y, min.Z), new Vector3(min.X, max.Y, min.Z),
            }, chamfer, material);
        }

        /// <summary>A block of four corners in the xy plane, counter-clockwise seen from the front, standing from z = <paramref name="back"/> to z = <paramref name="front"/>.</summary>
        public static void Prism(MeshBuilder b, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float back, float front, float chamfer, VertexMaterial material)
        {
            Block(b, new[]
            {
                new Vector3(p0.X, p0.Y, front), new Vector3(p1.X, p1.Y, front), new Vector3(p2.X, p2.Y, front), new Vector3(p3.X, p3.Y, front),
                new Vector3(p0.X, p0.Y, back), new Vector3(p1.X, p1.Y, back), new Vector3(p2.X, p2.Y, back), new Vector3(p3.X, p3.Y, back),
            }, chamfer, material);
        }

        private static int IndexIn(int face, int corner) => Array.IndexOf(Faces[face], corner);

        private static Vector3 Unit(Vector3 v)
        {
            float length = v.Length;
            return length > 1e-6f ? v / length : Vector3.Zero;
        }

        // A quad wound to face away from the middle of the block it's part of.
        private static void Outward(MeshBuilder b, Vector3 middle, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, VertexMaterial material)
        {
            Vector3 normal = Vector3.Cross(p2 - p0, p3 - p1);
            if (Vector3.Dot(normal, (p0 + p1 + p2 + p3) * 0.25f - middle) < 0f) b.Quad(p3, p2, p1, p0, material);
            else b.Quad(p0, p1, p2, p3, material);
        }
    }
}
