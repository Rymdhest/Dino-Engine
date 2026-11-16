using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// The Archive, inside: a hall of polished marble laid out as a great church is, and kept as clear as one. A nave runs from the door between two rows of columns to a dais
    /// under the end wall, where the tyrant's skull stands as an altar would, and every other record has a place of its own against a wall, in a bay of its own, with the whole
    /// floor open in front of it. Nothing stands in front of anything, as the camera looks down from the south. This file is its floor, which is most of what's seen of it:
    /// slabs of two whites laid as a chequer too faint to be loud, a dot of the Haven's teal at every corner, a band round the walls, a way of teal up the nave, and where the
    /// nave opens before the dais a great compass with the Haven's blossom at its heart. Basilica.Order.cs is what stands on it and Exhibits.cs the records. Read HALLS.md.
    /// </summary>
    internal static partial class Basilica
    {
        // Its marbles. One material (MaterialCatalog.Marble), which is pale: the stone is in the colour.
        internal static VertexMaterial White => Hall.Marble(204, 200, 192);
        internal static VertexMaterial Ivory => Hall.Marble(194, 186, 170);
        internal static VertexMaterial Grey => Hall.Marble(132, 136, 144);
        internal static VertexMaterial Teal => Hall.Marble(24, 84, 88);
        internal static VertexMaterial DeepTeal => Hall.Marble(16, 52, 58);
        internal static VertexMaterial Blossom => Hall.Marble(196, 100, 122);
        internal static VertexMaterial Gilt => Hall.Marble(206, 156, 56);
        internal static VertexMaterial PaleGilt => Hall.Marble(214, 182, 104);
        internal static VertexMaterial Walnut => Hall.Timber(86, 58, 40);
        internal static VertexMaterial DarkWalnut => Hall.Timber(62, 42, 30);
        internal static VertexMaterial Silver => Hall.Metal(190, 192, 198);

        /// <summary>Metres across one slab of the floor.</summary>
        internal const float Slab = 2f;

        /// <summary>Metres of plain floor between the walls and the slabs, which the band runs in.</summary>
        internal const float Margin = 1f;

        /// <summary>Half the width of the way of teal up the nave.</summary>
        internal const float WayHalf = 1.6f;

        /// <summary>The middle of the compass, where the nave opens before the dais, and how far it reaches.</summary>
        internal static readonly Vector2 Compass = new Vector2(0f, -1f);
        internal const float CompassRadius = 4.4f;

        // Heights of what's laid into the floor, each a few millimetres over the last so that nothing lies in the same plane as anything else.
        private const float Lift = 0.004f;

        /// <summary>The whole floor, laid by the shell, as it's seen through the door from the street.</summary>
        public static void Floor(MeshBuilder b, HallDesign d)
        {
            Vector2 inner = d.Inner;
            float y = Hall.FloorTop;
            var random = new WorldRandom(0xA2C1).Fork("floor");

            // The slabs: a chequer of two whites, each cut from its own block, so no vein runs on from one to the next.
            int across = (int)MathF.Round((inner.X - Margin) * 2f / Slab), deep = (int)MathF.Round((inner.Y - Margin) * 2f / Slab);
            float fromX = -across * Slab * 0.5f, fromZ = -deep * Slab * 0.5f;
            for (int i = 0; i < across; i++)
            {
                for (int j = 0; j < deep; j++)
                {
                    float x0 = fromX + i * Slab, z0 = fromZ + j * Slab;
                    Lay(b, x0, x0 + Slab, z0, z0 + Slab, y, (i + j) % 2 == 0 ? White : Ivory, random);
                }
            }
            // The margin round them, in long stones.
            float edgeX = -fromX, edgeZ = -fromZ;
            for (int i = 0; i < across; i += 2)
            {
                float x0 = fromX + i * Slab, x1 = MathF.Min(x0 + Slab * 2f, edgeX);
                Lay(b, x0, x1, edgeZ, inner.Y, y, White, random);
                Lay(b, x0, x1, -inner.Y, -edgeZ, y, White, random);
            }
            for (int j = 0; j < deep; j += 2)
            {
                float z0 = fromZ + j * Slab, z1 = MathF.Min(z0 + Slab * 2f, edgeZ);
                Lay(b, edgeX, inner.X, z0, z1, y, White, random);
                Lay(b, -inner.X, -edgeX, z0, z1, y, White, random);
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    Lay(b, MathF.Min(sx * edgeX, sx * inner.X), MathF.Max(sx * edgeX, sx * inner.X), MathF.Min(sz * edgeZ, sz * inner.Y), MathF.Max(sz * edgeZ, sz * inner.Y), y, White, random);
                }
            }

            // A dot of teal where four slabs meet.
            for (int i = 0; i <= across; i++)
            {
                for (int j = 0; j <= deep; j++)
                {
                    Vector3 at = new Vector3(fromX + i * Slab, y + Lift, fromZ + j * Slab);
                    Inlay(b, Teal, at + new Vector3(-0.3f, 0f, 0f), at + new Vector3(0f, 0f, 0.3f), at + new Vector3(0.3f, 0f, 0f), at + new Vector3(0f, 0f, -0.3f));
                }
            }

            // The band round the walls: teal between two lines of gold.
            float bandY = y + Lift * 2f;
            Frame(b, edgeX + 0.22f, edgeZ + 0.22f, 0.07f, bandY, Gilt);
            Frame(b, edgeX + 0.5f, edgeZ + 0.5f, 0.21f, bandY, Teal);
            Frame(b, edgeX + 0.78f, edgeZ + 0.78f, 0.07f, bandY, Gilt);

            Way(b, d, y + Lift * 2f);
            CompassRose(b, new Vector3(Compass.X, y + Lift * 3f, Compass.Y));

            // A ring under every column, and a round of teal before every record, where whoever reads it stands.
            foreach (HallPiece piece in d.Pieces)
            {
                Vector3 at = new Vector3(piece.At.X, y + Lift * 3f, piece.At.Y);
                if (piece.Kind == PieceKind.Column)
                {
                    Annulus(b, at, piece.Half.X + 0.12f, piece.Half.X + 0.2f, 24, Gilt);
                    Annulus(b, at, piece.Half.X + 0.2f, piece.Half.X + 0.62f, 24, Teal);
                    Annulus(b, at, piece.Half.X + 0.62f, piece.Half.X + 0.7f, 24, Gilt);
                }
                if (piece.Kind != PieceKind.Plinth || (ExhibitKind)piece.Variant == ExhibitKind.TyrantsFelled) continue;
                Vector2 forward = Hall.RotateY(Vector2.UnitY, piece.Yaw);
                Vector2 stand = piece.At + forward * (piece.Half.Y + StandOff);
                Vector3 centre = new Vector3(stand.X, y + Lift * 3f, stand.Y);
                Annulus(b, centre, 1.18f, 1.26f, 28, Gilt);
                Annulus(b, centre, 0.8f, 1.18f, 28, Teal);
                Annulus(b, centre, 0.72f, 0.8f, 28, Gilt);
                Disc(b, centre + Vector3.UnitY * Lift, 0.72f, 28, White);
                Star(b, centre + Vector3.UnitY * Lift * 2f, 0.6f, 0.24f, 8, Teal, DeepTeal, 0f);
            }
        }

        /// <summary>Metres from the front of a record's plinth to the middle of the round where whoever reads it stands.</summary>
        internal const float StandOff = 2.3f;

        // One slab, its marble turned and taken from anywhere in the block.
        private static void Lay(MeshBuilder b, float x0, float x1, float z0, float z1, float y, VertexMaterial material, WorldRandom random)
        {
            if (x1 - x0 < 1e-3f || z1 - z0 < 1e-3f) return;
            Vector3 u, v;
            switch (random.Int(4))
            {
                case 0: u = Vector3.UnitX; v = -Vector3.UnitZ; break;
                case 1: u = -Vector3.UnitZ; v = -Vector3.UnitX; break;
                case 2: u = -Vector3.UnitX; v = Vector3.UnitZ; break;
                default: u = Vector3.UnitZ; v = Vector3.UnitX; break;
            }
            Vector3 origin = new Vector3(random.Range(-40f, 40f), y, random.Range(-40f, 40f));
            b.PlaneQuad(new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), new Vector3(x0, y, z0), material, origin, u, v, new Vector4(-1e3f, 1e3f, -1e3f, 1e3f));
        }

        /// <summary>
        /// A piece of marble let into a floor: any convex outline, flat, facing up, its marble lying as the world does so that pieces cut from one stone run on into each
        /// other. A face made corner by corner starts its texture again at every corner, which on a round of wedges is a kaleidoscope.
        /// </summary>
        internal static void Inlay(MeshBuilder b, VertexMaterial material, params Vector3[] outline)
        {
            // Where it lies in the stone, by which marble it is, so two colours side by side aren't one slab stained.
            Colour c = material.colour;
            Vector2 shift = new Vector2(c.Red * 37f + c.Green * 11f, c.Blue * 29f + c.Green * 5f);
            Vector2 Uv(Vector3 p) => new Vector2(p.X, p.Z) + shift;
            for (int i = 1; i + 1 < outline.Length; i++)
            {
                Vector3 a = outline[0], p = outline[i], q = outline[i + 1];
                if (Vector3.Cross(p - a, q - a).Y < 0f) (p, q) = (q, p);
                b.MappedTriangle(a, p, q, material, Uv(a), Uv(p), Uv(q), Vertex.NoEdges, Vertex.NoEdges, Vertex.NoEdges);
            }
        }

        private static Vector3 Round(float angle, float radius) => new Vector3(MathF.Cos(angle) * radius, 0f, MathF.Sin(angle) * radius);

        internal static void Disc(MeshBuilder b, Vector3 centre, float radius, int segments, VertexMaterial material)
        {
            for (int i = 0; i < segments; i++)
            {
                Inlay(b, material, centre, centre + Round(MathF.Tau * i / segments, radius), centre + Round(MathF.Tau * (i + 1) / segments, radius));
            }
        }

        internal static void Annulus(MeshBuilder b, Vector3 centre, float inner, float outer, int segments, VertexMaterial material)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = MathF.Tau * i / segments, a1 = MathF.Tau * (i + 1) / segments;
                Inlay(b, material, centre + Round(a0, inner), centre + Round(a0, outer), centre + Round(a1, outer), centre + Round(a1, inner));
            }
        }

        /// <summary>A star of points, each split down its middle into a light half and a dark one, as a compass is drawn.</summary>
        internal static void Star(MeshBuilder b, Vector3 centre, float reach, float waist, int points, VertexMaterial light, VertexMaterial dark, float turn)
        {
            for (int i = 0; i < points; i++)
            {
                float a = turn + MathF.Tau * i / points, half = MathF.PI / points;
                Vector3 tip = centre + Round(a, reach);
                Inlay(b, light, centre, centre + Round(a - half, waist), tip);
                Inlay(b, dark, centre, tip, centre + Round(a + half, waist));
            }
        }

        // A rectangular frame on the floor, about the middle of the hall: a line `half` either side of the rectangle that reaches x and z from the middle.
        private static void Frame(MeshBuilder b, float x, float z, float half, float y, VertexMaterial material)
        {
            Strip(b, -x - half, x + half, z - half, z + half, y, material);
            Strip(b, -x - half, x + half, -z - half, -z + half, y, material);
            Strip(b, x - half, x + half, -z + half, z - half, y, material);
            Strip(b, -x - half, -x + half, -z + half, z - half, y, material);
        }

        private static void Strip(MeshBuilder b, float x0, float x1, float z0, float z1, float y, VertexMaterial material)
        {
            Inlay(b, material, new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0));
        }

        // The way up the nave, from the door to the dais: teal between lines of gold, with a white lozenge let into it every few steps, broken where the compass lies.
        private static void Way(MeshBuilder b, HallDesign d, float y)
        {
            HallPiece dais = d.Pieces.First(p => p.Kind == PieceKind.Plinth && (ExhibitKind)p.Variant == ExhibitKind.TyrantsFelled);
            float daisFront = dais.At.Y + dais.Half.Y;
            float ring = CompassRadius + 0.02f;
            foreach ((float from, float to) in new[] { (Compass.Y + ring, d.Inner.Y), (daisFront, Compass.Y - ring) })
            {
                if (to - from < 0.3f) continue;
                Strip(b, -WayHalf, WayHalf, from, to, y, Teal);
                foreach (float s in new[] { -1f, 1f })
                {
                    Strip(b, s * WayHalf - 0.09f, s * WayHalf + 0.09f, from, to, y + Lift, Gilt);
                    Strip(b, s * (WayHalf - 0.42f) - 0.035f, s * (WayHalf - 0.42f) + 0.035f, from, to, y + Lift, PaleGilt);
                }
                int lozenges = (int)((to - from) / 2.2f);
                for (int i = 0; i < lozenges; i++)
                {
                    Vector3 at = new Vector3(0f, y + Lift, from + (to - from) * (i + 0.5f) / lozenges);
                    Inlay(b, White, at + new Vector3(-0.42f, 0f, 0f), at + new Vector3(0f, 0f, 0.62f), at + new Vector3(0.42f, 0f, 0f), at + new Vector3(0f, 0f, -0.62f));
                    Inlay(b, Gilt, at + new Vector3(-0.14f, Lift, 0f), at + new Vector3(0f, Lift, 0.2f), at + new Vector3(0.14f, Lift, 0f), at + new Vector3(0f, Lift, -0.2f));
                }
            }
        }

        // The compass: a white round ringed in gold and teal, sixteen points on it, eight long in teal and eight short in gold, and at its heart the Haven's blossom, five
        // petals of rose marble round a heart of gold.
        private static void CompassRose(MeshBuilder b, Vector3 centre)
        {
            const int Segments = 64;
            Disc(b, centre, CompassRadius - 0.6f, Segments, White);
            Annulus(b, centre, CompassRadius - 0.1f, CompassRadius, Segments, Gilt);
            Annulus(b, centre, CompassRadius - 0.52f, CompassRadius - 0.1f, Segments, Teal);
            Annulus(b, centre, CompassRadius - 0.6f, CompassRadius - 0.52f, Segments, Gilt);
            // A lozenge of white in the teal ring at every point of the compass.
            for (int i = 0; i < 16; i++)
            {
                float a = MathF.Tau * i / 16f;
                Vector3 at = centre + Round(a, CompassRadius - 0.31f) + Vector3.UnitY * Lift;
                Vector3 out_ = Round(a, 1f), side = Round(a + MathF.PI * 0.5f, 1f);
                Inlay(b, i % 2 == 0 ? White : PaleGilt, at - out_ * 0.14f, at + side * 0.2f, at + out_ * 0.14f, at - side * 0.2f);
            }

            Vector3 up = Vector3.UnitY * Lift;
            Star(b, centre + up, CompassRadius - 1.55f, 0.9f, 8, PaleGilt, Gilt, MathF.PI / 8f);
            Star(b, centre + up * 2f, CompassRadius - 0.72f, 1.05f, 8, Teal, DeepTeal, 0f);
            Disc(b, centre + up * 3f, 1.42f, 40, Gilt);
            Disc(b, centre + up * 4f, 1.32f, 40, White);

            // The blossom: five petals, each a round leaf notched at its tip, and a heart.
            for (int petal = 0; petal < 5; petal++)
            {
                float a = MathF.Tau * petal / 5f - MathF.PI * 0.5f;
                Vector3 out_ = Round(a, 1f), side = Round(a + MathF.PI * 0.5f, 1f);
                Vector3 foot = centre + up * 5f + out_ * 0.16f;
                Vector3[] leaf =
                {
                    foot, foot + out_ * 0.3f - side * 0.3f, foot + out_ * 0.68f - side * 0.36f, foot + out_ * 0.98f - side * 0.2f, foot + out_ * 0.9f,
                    foot + out_ * 0.98f + side * 0.2f, foot + out_ * 0.68f + side * 0.36f, foot + out_ * 0.3f + side * 0.3f,
                };
                // Two halves about its middle line, as its outline is notched and so isn't convex.
                Inlay(b, Blossom, leaf[0], leaf[1], leaf[2], leaf[3], leaf[4]);
                Inlay(b, Blossom, leaf[0], leaf[4], leaf[5], leaf[6], leaf[7]);
                Inlay(b, PaleGilt, foot + up + out_ * 0.12f - side * 0.02f, foot + up + out_ * 0.62f, foot + up + out_ * 0.12f + side * 0.02f);
            }
            Disc(b, centre + up * 6f, 0.24f, 16, Gilt);
        }
    }
}
