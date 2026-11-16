using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// What more than one hall has, each built in its own frame to the numbers of its <see cref="HallPiece"/>: its middle on the floor at the origin, its front towards +z, reaching
    /// <c>Half</c> each way and as tall as its <c>Height</c>, so that what's drawn is what the world takes as solid. The world is about two and a half times real size, as a person is 4.7 m
    /// tall: a table is 1.95 m high, a chair's seat 1.1 m, a candle half a metre. What stands in one hall alone is that hall's own file: GamingHouse.Pieces.cs, Smithy.Metal.cs and
    /// Smithy.Soft.cs, and for the Archive Basilica.Pieces.cs and Exhibits.cs.
    /// </summary>
    internal static class HallFurniture
    {
        public static void Build(MeshBuilder b, in HallPiece piece, HallKit kit, List<AssetLight> lights, WorldRandom random)
        {
            Vector2 half = piece.Half;
            float r = half.X;
            float h = piece.Height;
            switch (piece.Kind)
            {
                case PieceKind.Casks: Casks(b, r, h, random); break;
                case PieceKind.Rug: Rug(b, half, piece.Variant); break;
            }
        }

        /// <summary>A plain heavy table: a top a hand thick and four legs, braced.</summary>
        public static void Table(MeshBuilder b, Vector2 half, float h, VertexMaterial top, VertexMaterial legs)
        {
            b.Box(new Vector3(0f, h - 0.09f, 0f), new Vector3(half.X, 0.09f, half.Y), top);
            foreach (float x in new[] { -half.X + 0.25f, half.X - 0.25f })
            {
                foreach (float z in new[] { -half.Y + 0.22f, half.Y - 0.22f }) Hall.Leg(b, new Vector3(x, 0f, z), h - 0.18f, 0.16f, legs);
            }
            b.Box(new Vector3(0f, 0.45f, 0f), new Vector3(half.X - 0.25f, 0.06f, 0.06f), legs);
            b.Box(new Vector3(0f, h - 0.3f, half.Y - 0.22f), new Vector3(half.X - 0.3f, 0.1f, 0.04f), legs);
            b.Box(new Vector3(0f, h - 0.3f, -half.Y + 0.22f), new Vector3(half.X - 0.3f, 0.1f, 0.04f), legs);
        }

        private static void Casks(MeshBuilder b, float r, float h, WorldRandom random)
        {
            int count = r > 1f ? 3 : 1;
            for (int i = 0; i < count; i++)
            {
                float a = MathF.Tau * i / count + 0.4f;
                float cr = count == 1 ? r * 0.9f : r * 0.48f;
                Vector3 foot = count == 1 ? Vector3.Zero : new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * (r - cr);
                Barrel(b, foot, h * random.Range(0.92f, 1f), cr);
            }
        }

        /// <summary>A barrel standing on <paramref name="foot"/>: staves bellied out, hoops, a lid.</summary>
        public static void Barrel(MeshBuilder b, Vector3 foot, float height, float radius)
        {
            VertexMaterial staves = Hall.Wood(132, 94, 60);
            VertexMaterial hoop = Hall.Iron(84);
            b.Tube(foot, foot + Vector3.UnitY * height * 0.5f, radius * 0.86f, radius, 12, staves, capped: false);
            b.Tube(foot + Vector3.UnitY * height * 0.5f, foot + Vector3.UnitY * height, radius, radius * 0.86f, 12, staves);
            foreach (float t in new[] { 0.1f, 0.33f, 0.67f, 0.9f })
            {
                float rr = radius * (t < 0.5f ? 0.86f + 0.28f * t : 0.86f + 0.28f * (1f - t)) + 0.012f;
                b.Tube(foot + Vector3.UnitY * (height * t - 0.04f), foot + Vector3.UnitY * (height * t + 0.04f), rr, rr, 12, hoop, capped: false);
            }
        }

        private static void Rug(MeshBuilder b, Vector2 half, int variant)
        {
            // Border, field, and a medallion, each a hair above the last.
            Vector3 border = variant == 1 ? Hall.Cloths[3] : Hall.Cloths[2];
            Vector3 field = variant == 1 ? new Vector3(30, 74, 80) : new Vector3(36, 56, 104);
            b.Box(new Vector3(0f, 0.065f, 0f), new Vector3(half.X, 0.015f, half.Y), Hall.ClothOf(border));
            b.Box(new Vector3(0f, 0.08f, 0f), new Vector3(half.X - 0.35f, 0.01f, half.Y - 0.35f), Hall.ClothOf(field));
            b.Box(new Vector3(0f, 0.09f, 0f), new Vector3(half.X - 0.5f, 0.005f, half.Y - 0.5f), Hall.ClothOf(field * 0.8f));
            b.Box(new Vector3(0f, 0.095f, 0f), new Vector3(half.X * 0.35f, 0.006f, half.Y * 0.35f), Hall.ClothOf(border * 0.9f));
            b.Box(new Vector3(0f, 0.1f, 0f), new Vector3(half.X * 0.2f, 0.006f, half.Y * 0.2f), Hall.ClothOf(Hall.Cloths[4]));
            // Fringes at the ends.
            for (float x = -half.X + 0.1f; x < half.X; x += 0.2f)
            {
                foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(x, 0.06f, s * (half.Y + 0.12f)), new Vector3(0.03f, 0.008f, 0.12f), Hall.ClothOf(Hall.Cloths[4]));
            }
        }

        /// <summary>A sphere of <paramref name="material"/> as a grid of quads, for a globe, a glass or a gem.</summary>
        public static void Sphere(MeshBuilder b, Vector3 centre, float radius, int around, int up, VertexMaterial material)
        {
            b.Surface(around, up, (u, v) =>
            {
                float a = u * MathF.Tau, e = (v - 0.5f) * MathF.PI;
                return centre + new Vector3(MathF.Cos(a) * MathF.Cos(e), MathF.Sin(e), -MathF.Sin(a) * MathF.Cos(e)) * radius;
            }, material);
        }
    }
}
