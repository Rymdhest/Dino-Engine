using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Util;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>
    /// How a card that lies on the ground is laid: a <see cref="GroundForm"/>, leaf litter, moss or a cushion of thyme, lichen or campion, a few centimetres thick and metres
    /// across, so the ground under it decides whether it's seen at all. It lies on the ground as it's drawn, <see cref="TerrainRenderer.SurfaceOffset"/> over the height map:
    /// laid on the height map, as the rest of the ground cover is, every card was buried under the drawn ground but on slopes, where it stood out of the hillside as a level shelf
    /// cut off where it went in. So it's tilted to the plane that best fits the drawn ground under the whole of it, its rim settled <see cref="Settle"/> into that plane, and it
    /// isn't laid at all where the ground strays from that plane anywhere under it by more than <see cref="MostStray"/>, as a world object can only be moved, turned and
    /// stretched, and a card is metres across. <c>Dino WorldLab cover</c> holds what comes of it, measured on the models as they're drawn.
    /// </summary>
    public static class GroundCards
    {
        /// <summary>Metres a card's rim goes into the plane it's laid on: into the ground's relief, 5 cm deep for soil and litter, so that a rim over a dip doesn't stand off it.</summary>
        public const float Settle = 0.02f;

        /// <summary>Metres the drawn ground may stray from the plane a card is laid on, anywhere under it, before it isn't laid there.</summary>
        public const float MostStray = 0.12f;

        /// <summary>What laying a card needs of its model, at a scale of one and unturned: where it lies across the ground, and how far its lowest point hangs below its origin.</summary>
        public sealed class Shape
        {
            /// <summary>Where the fit reads the ground, across it (x, z): every vertex, the middle of every triangle and of each of its edges.</summary>
            public readonly Vector2[] Points;

            /// <summary>Metres its lowest point, its rim, hangs below its origin.</summary>
            public readonly float Below;

            public Shape(FloraMesh mesh)
            {
                var points = new List<Vector2>(mesh.VertexCount + mesh.TriangleCount * 4);
                float lowest = 0f;
                foreach (FloraVertex vertex in mesh.Vertices)
                {
                    points.Add(vertex.Position.Xz);
                    lowest = MathF.Min(lowest, vertex.Position.Y);
                }
                for (int t = 0; t + 2 < mesh.Indices.Count; t += 3)
                {
                    Vector2 a = mesh.Vertices[mesh.Indices[t]].Position.Xz, b = mesh.Vertices[mesh.Indices[t + 1]].Position.Xz, c = mesh.Vertices[mesh.Indices[t + 2]].Position.Xz;
                    points.Add((a + b + c) / 3f);
                    points.Add((a + b) * 0.5f);
                    points.Add((b + c) * 0.5f);
                    points.Add((c + a) * 0.5f);
                }
                Points = points.ToArray();
                Below = -lowest;
            }
        }

        /// <summary>The shape of a model if it's a card that lies on the ground, and null for anything else. It grows the model, so it's for keeping.</summary>
        public static Shape? ShapeOf(string model)
        {
            string name = NatureCatalog.SpeciesOf(model, out ulong seed);
            if (!FloraCatalog.TryGet(name, out PlantSpecies species) || species.Form is not GroundForm) return null;
            return new Shape(FloraForge.Grow(species, seed));
        }

        /// <summary>
        /// Lays a card of <paramref name="shape"/> at <paramref name="at"/>, turned <paramref name="yaw"/> and grown to <paramref name="scale"/>, on the drawn ground: where its
        /// origin goes and which way its top faces, for <see cref="Rotation"/>. False where the ground under it strays too far from any plane for it to lie on.
        /// </summary>
        public static bool TryLay(WorldData world, Vector2 at, float yaw, float scale, Shape shape, out Vector3 position, out Vector3 up)
        {
            position = default;
            up = Vector3.UnitY;
            Quaternion turn = Quaternion.FromAxisAngle(Vector3.UnitY, yaw);
            int count = shape.Points.Length;
            Span<Vector3> ground = count <= 256 ? stackalloc Vector3[count] : new Vector3[count];

            // The plane y = h + gx dx + gz dz that best fits the drawn ground under it, from its origin, by least squares.
            double n = 0, sx = 0, sz = 0, sxx = 0, sxz = 0, szz = 0, sy = 0, sxy = 0, szy = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = Vector3.Transform(new Vector3(shape.Points[i].X, 0f, shape.Points[i].Y) * scale, turn);
                float y = Drawn(world, at.X + offset.X, at.Y + offset.Z);
                ground[i] = new Vector3(offset.X, y, offset.Z);
                n++; sx += offset.X; sz += offset.Z; sxx += offset.X * offset.X; sxz += offset.X * offset.Z; szz += offset.Z * offset.Z;
                sy += y; sxy += offset.X * y; szy += offset.Z * y;
            }
            // Cramer's rule on the normal equations [n sx sz; sx sxx sxz; sz sxz szz] [h gx gz] = [sy sxy szy].
            double det = n * (sxx * szz - sxz * sxz) - sx * (sx * szz - sxz * sz) + sz * (sx * sxz - sxx * sz);
            if (Math.Abs(det) < 1e-9) return false;
            double h = (sy * (sxx * szz - sxz * sxz) - sx * (sxy * szz - sxz * szy) + sz * (sxy * sxz - sxx * szy)) / det;
            double gx = (n * (sxy * szz - sxz * szy) - sy * (sx * szz - sxz * sz) + sz * (sx * szy - sxy * sz)) / det;
            double gz = (n * (sxx * szy - sxy * sxz) - sx * (sx * szy - sxy * sz) + sy * (sx * sxz - sxx * sz)) / det;

            for (int i = 0; i < count; i++)
            {
                double plane = h + gx * ground[i].X + gz * ground[i].Z;
                if (Math.Abs(ground[i].Y - plane) > MostStray) return false;
            }

            up = new Vector3((float)-gx, 1f, (float)-gz).Normalized();
            // Its rim lies in the plane through here, settled a little into the ground: the origin stands up off it by as much as the rim hangs below.
            position = new Vector3(at.X, (float)h - Settle, at.Y) + up * (shape.Below * scale);
            return true;
        }

        /// <summary>A plant's rotation: turned <paramref name="yaw"/> about its own top, and then tilted so its top faces <paramref name="up"/>, which is zero for upright.</summary>
        public static Quaternion Rotation(float yaw, Vector3 up)
        {
            Quaternion turn = Quaternion.FromAxisAngle(Vector3.UnitY, yaw);
            return up == Vector3.Zero ? turn : MyMath.FromToRotation(Vector3.UnitY, up) * turn;
        }

        /// <summary>The ground as Terrain.vert draws it: its mesh is the cubic height map at every vertex (TerrainGenerator.getHeightAt), SurfaceOffset over it.</summary>
        public static float Drawn(WorldData world, float x, float z) => world.DrawnGroundAt(x, z);
    }
}
