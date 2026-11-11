using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Plaza;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Arena
{
    /// <summary>
    /// The gates the brood comes in by, and what shuts them. A gatehouse is a block of dressed stone in an arena's wall with an arched opening and a vaulted passage behind it going back into the dark, its
    /// face on z = 0 towards +z, the floor; its grille hangs a little behind the face and rises into the block over the arch, which the game moves (ArenaSystem), so the grille is a model of its own. The
    /// torches beside an opening are lamps of their own (<see cref="TorchSpots"/>), as a light inside a model over 6 m across is shadowed by it. Headless.
    /// </summary>
    internal static class ArenaGates
    {
        private const float Foot = -0.6f;
        private const float Ring = 0.78f;           // How deep an arch's voussoirs are, from the opening out.

        /// <summary>How far behind a gate's face its grille hangs.</summary>
        public const float GrilleBack = 0.55f;

        /// <summary>How far in front of the wall's face a gatehouse stands, so that it reads as a block of its own.</summary>
        public const float Proud = 0.3f;

        /// <summary>
        /// A gatehouse: a plinth, jambs of long and short blocks either side of the opening up to its springing, a round arch of voussoirs on radial joints round a keystone that carries the Reich's mark in
        /// brass, coursed spandrels, a heavy cornice and a blocking course over it; behind the opening a passage under a barrel vault, its grille's slot in the vault, and darkness at its end.
        /// </summary>
        public static void Gatehouse(MeshBuilder b, ArenaStoneKind stone, float opening, float crown, float depth, float width, float height, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("gatehouse");
            float half = opening * 0.5f, outer = width * 0.5f;
            float spring = crown - half;
            float z = Proud, back = -depth;
            float cornice = height - 0.62f;

            // The plinth, proud all round the front.
            foreach (float side in new[] { -1f, 1f })
            {
                float x0 = side * (half + 0.004f), x1 = side * (outer + 0.12f);
                PlazaWalls.Lay(b, PlazaPieces.Corners(new Vector3(MathF.Min(x0, x1), Foot, z + 0.12f), new Vector3(MathF.Max(x0, x1), 0.62f, back + 0.3f)), ArenaStone.BigChamfer,
                    ArenaStone.DressedFacing(stone, random.Fork(side > 0f ? "plinth east" : "plinth west"), 0f));
            }

            // The jambs, long and short in turn so that no joint runs up them, to the springing, with an impost on top.
            foreach (float side in new[] { -1f, 1f })
            {
                WorldRandom jamb = random.Fork(side > 0f ? "east" : "west");
                float inner = side * half, out_ = side * outer;
                int courses = Math.Max(2, (int)MathF.Round((spring - 0.2f - 0.62f) / 0.5f));
                float rise = (spring - 0.2f - 0.62f) / courses;
                for (int c = 0; c < courses; c++)
                {
                    float y0 = 0.62f + c * rise;
                    float split = c % 2 == 0 ? 0.42f : 0.66f;
                    float m = inner + (out_ - inner) * split;
                    Lay(b, inner, m, y0, y0 + rise, z, back + 0.3f, ArenaStone.DressedFacing(stone, jamb.Fork(c * 2), y0));
                    Lay(b, m, out_, y0, y0 + rise, z, back + 0.3f, ArenaStone.DressedFacing(stone, jamb.Fork(c * 2 + 1), y0));
                }
                float impost = spring - 0.2f;
                PlazaWalls.Lay(b, PlazaPieces.Corners(new Vector3(MathF.Min(inner - side * 0.08f, out_), impost, z + 0.1f), new Vector3(MathF.Max(inner - side * 0.08f, out_), spring, back + 0.3f)),
                    ArenaStone.BigChamfer, ArenaStone.DressedFacing(stone, jamb.Fork("impost"), spring, 0.14f));
            }

            // The arch: an odd number of voussoirs, so that a keystone stands at the crown, a little proud and deeper than the rest.
            float radius = half, extrados = radius + Ring;
            int stones = Math.Max(9, (int)MathF.Round(MathF.PI * (radius + Ring * 0.5f) / 0.62f) | 1);
            for (int i = 0; i < stones; i++)
            {
                float a0 = MathF.PI * i / stones, a1 = MathF.PI * (i + 1) / stones;
                bool key = i == stones / 2;
                float proud = key ? 0.1f : 0f;
                float far = extrados + (key ? 0.24f : 0f);
                Vector3 P(float angle, float r, float zz) => new Vector3(-MathF.Cos(angle) * r, spring + MathF.Sin(angle) * r, zz);
                Vector3[] c =
                {
                    P(a1, radius, z + proud), P(a0, radius, z + proud), P(a0, far, z + proud), P(a1, far, z + proud),
                    P(a1, radius, z - 1.1f), P(a0, radius, z - 1.1f), P(a0, far, z - 1.1f), P(a1, far, z - 1.1f),
                };
                Shrink(c, 0.005f);
                PlazaWalls.Lay(b, c, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork(100 + i), spring + radius, key ? 0.06f : 0.12f));
                if (key)
                {
                    // The mark, in brass, on the keystone's face.
                    Vector3 face = P((a0 + a1) * 0.5f, (radius + far) * 0.5f, z + proud + 0.01f);
                    ArenaStone.Mark(b, face, Vector3.UnitX, Vector3.UnitY, MathF.Min(Ring * 0.95f, 0.9f), 0.03f, ArenaStone.Brass(random.Fork("key")), ArenaStone.Hearting(stone));
                }
            }

            // The spandrels, coursed up to the cornice, each course cut to the back of the arch, a little back from its face so that nothing shares a plane.
            float zs = z - 0.05f;
            float y = spring;
            int course = 0;
            while (y < cornice - 0.05f)
            {
                float rise = MathF.Min(0.5f, cornice - y);
                float y1 = y + rise;
                float Inner(float at) => at >= spring + extrados ? 0f : MathF.Sqrt(MathF.Max(extrados * extrados - (at - spring) * (at - spring), 0f));
                float in0 = Inner(y), in1 = Inner(y1);
                if (in0 <= 0.001f && in1 <= 0.001f)
                {
                    float joint = course % 2 == 0 ? 0.6f : -0.6f;
                    Lay(b, -outer, joint - 0.005f, y + 0.003f, y1 - 0.003f, zs, back + 0.3f, ArenaStone.DressedFacing(stone, random.Fork(200 + course * 2), y));
                    Lay(b, joint + 0.005f, outer, y + 0.003f, y1 - 0.003f, zs, back + 0.3f, ArenaStone.DressedFacing(stone, random.Fork(201 + course * 2), y));
                }
                else
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        if (in0 >= outer - 0.05f && in1 >= outer - 0.05f) continue;
                        float x0 = side * MathF.Max(in0, 0.004f), x1 = side * MathF.Max(in1, 0.004f), xo = side * outer;
                        Vector3[] c =
                        {
                            new(x0 + side * 0.005f, y + 0.003f, zs), new(xo, y + 0.003f, zs), new(xo, y1 - 0.003f, zs), new(x1 + side * 0.005f, y1 - 0.003f, zs),
                            new(x0 + side * 0.005f, y + 0.003f, back + 0.3f), new(xo, y + 0.003f, back + 0.3f), new(xo, y1 - 0.003f, back + 0.3f), new(x1 + side * 0.005f, y1 - 0.003f, back + 0.3f),
                        };
                        PlazaWalls.Lay(b, c, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork(300 + course * 2 + (side > 0f ? 1 : 0)), y));
                    }
                }
                y = y1;
                course++;
            }

            // A cornice proud all round the front, and a blocking course over it, each laid in slabs no bigger than a stone is cut, as one face of rock that size reads as crazy paving from above;
            // and merlons along the front and sides, which is most of what the camera sees of a gate.
            WorldRandom cornices = random.Fork("cornice");
            Slabs(b, -outer - 0.2f, outer + 0.2f, back + 0.1f, z + 0.22f, cornice, cornice + 0.3f, 1.5f, stone, cornices.Fork("cornice"), 0.16f);
            Slabs(b, -outer + 0.1f, outer - 0.1f, back + 0.3f, z - 0.05f, cornice + 0.3f, height, 1.45f, stone, cornices.Fork("blocking"), 0.18f);
            const float Merlon = 0.95f, MerlonHigh = 0.9f, MerlonDeep = 0.62f;
            int across = Math.Max(2, (int)MathF.Round((width + 0.4f) / 1.7f));
            for (int i = 0; i < across; i++)
            {
                float x = -outer + 0.1f + (width - 0.2f) * (i + 0.5f) / across;
                ArenaStone.Block(b, new Vector3(x, height + MerlonHigh * 0.5f, z - 0.05f - MerlonDeep * 0.5f), new Vector3(Merlon * 0.5f, MerlonHigh * 0.5f, MerlonDeep * 0.5f), 0f, ArenaStone.Chamfer,
                    ArenaStone.DressedFacing(stone, cornices.Fork(500 + i), height, 0.2f));
            }
            int deep = Math.Max(2, (int)MathF.Round((z - back) / 1.7f));
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 1; i < deep; i++)
                {
                    float zz = z - 0.05f - (z - 0.35f - back) * (i + 0.5f) / deep;
                    ArenaStone.Block(b, new Vector3(side * (outer - 0.1f - MerlonDeep * 0.5f), height + MerlonHigh * 0.5f, zz), new Vector3(MerlonDeep * 0.5f, MerlonHigh * 0.5f, Merlon * 0.5f), 0f, ArenaStone.Chamfer,
                        ArenaStone.DressedFacing(stone, cornices.Fork(600 + i * 2 + (side > 0f ? 1 : 0)), height, 0.2f));
                }
            }

            Passage(b, stone, half, spring, z, back, random.Fork("passage"));
        }

        // The passage behind an opening: walls of rougher blocks, a barrel vault, the slot the grille rises into, flagged floor, and the dark at its end.
        private static void Passage(MeshBuilder b, ArenaStoneKind stone, float half, float spring, float z, float back, WorldRandom random)
        {
            float vaultFrom = z - 1.1f;
            // Its walls are the jambs' inner faces, which run its whole depth. The vault over it, in rings of stone, darker the further in it goes, seen from below.
            const int Rings = 3, Segments = 9;
            for (int r = 0; r < Rings; r++)
            {
                float z0 = vaultFrom + (back + 0.3f - vaultFrom) * r / Rings, z1 = vaultFrom + (back + 0.3f - vaultFrom) * (r + 1) / Rings;
                float shade = 0.75f - r * 0.18f;
                for (int s = 0; s < Segments; s++)
                {
                    float a0 = MathF.PI * s / Segments, a1 = MathF.PI * (s + 1) / Segments;
                    Vector3 P(float angle, float rr, float zz) => new Vector3(-MathF.Cos(angle) * rr, spring + MathF.Sin(angle) * rr, zz);
                    VertexMaterial vault = ArenaStone.Tint(ArenaStone.Dressed(stone, random.Fork(100 + r * Segments + s), spring, 0.08f), new Vector3(shade));
                    b.Quad(P(a0, half + 0.01f, z0 - 0.003f), P(a0, half + 0.01f, z1 + 0.003f), P(a1, half + 0.01f, z1 + 0.003f), P(a1, half + 0.01f, z0 - 0.003f), vault);
                }
            }
            // The slot the grille rises into, a dark line across the vault's crown just inside the face.
            float slot = z - GrilleBack;
            b.Box(new Vector3(0f, spring + half + 0.02f, slot), new Vector3(half + 0.02f, 0.03f, 0.1f), ArenaStone.Dark);
            // The floor, in long flags, and the dark at the end.
            for (int f = 0; f < 3; f++)
            {
                float z0 = vaultFrom + 1.1f - (vaultFrom + 1.1f - back - 0.3f) * f / 3f, z1 = vaultFrom + 1.1f - (vaultFrom + 1.1f - back - 0.3f) * (f + 1) / 3f;
                Lay(b, -half - 0.01f, half + 0.01f, -0.3f, ArenaStone.Ground + 0.07f, z0 - 0.004f, z1 + 0.004f, _ => ArenaStone.Tint(ArenaStone.Dressed(stone, random.Fork(500 + f), 0f), new Vector3(0.7f - f * 0.15f)));
            }
            b.Quad(new Vector3(-half - 0.6f, -0.3f, back + 0.31f), new Vector3(half + 0.6f, -0.3f, back + 0.31f), new Vector3(half + 0.6f, spring + half + 0.5f, back + 0.31f), new Vector3(-half - 0.6f, spring + half + 0.5f, back + 0.31f), ArenaStone.Dark);
        }

        /// <summary>Where a gatehouse's two torches go, in its own frame, and which way they face: on its jambs, at the height of a person's head and a half.</summary>
        public static IEnumerable<Vector3> TorchSpots(float opening, float width)
        {
            float x = (opening * 0.5f + width * 0.5f) * 0.5f;
            foreach (float side in new[] { -1f, 1f }) yield return new Vector3(side * x, 3.2f, Proud + 0.04f);
        }

        // ---------------------------------------------------------------- Grilles

        /// <summary>
        /// A grille that rises out of an opening <paramref name="width"/> across and <paramref name="height"/> high, its foot at y = 0 and its face at z = 0, in the Reich's black iron.
        /// </summary>
        public static void Grille(MeshBuilder b, ArenaGrille form, float width, float height, uint seed)
        {
            var random = new WorldRandom(seed).Fork("grille");
            VertexMaterial iron = ArenaStone.Iron(random);
            VertexMaterial dark = ArenaStone.Iron(random, 0.75f);
            float half = width * 0.5f;
            switch (form)
            {
                case ArenaGrille.Pit:
                {
                    int uprights = Math.Max(5, (int)MathF.Round(width / 0.42f));
                    for (int i = 0; i < uprights; i++)
                    {
                        float x = -half + (i + 0.5f) * width / uprights;
                        b.Box(new Vector3(x, height * 0.5f + 0.25f, 0f), new Vector3(0.065f, height * 0.5f - 0.25f, 0.065f), iron);
                        // A spike at the foot of each, which is what comes down on whoever's under it.
                        b.Tube(new Vector3(x, 0.26f, 0f), new Vector3(x, 0f, 0f), 0.065f, 0.006f, 4, dark);
                    }
                    for (int r = 0; r < 4; r++)
                    {
                        float y = 0.7f + (height - 1.2f) * r / 3f;
                        b.Box(new Vector3(0f, y, 0.02f), new Vector3(half, 0.07f, 0.09f), dark);
                        for (int i = 0; i <= uprights; i += 2)
                        {
                            float x = -half + i * width / uprights;
                            b.Box(new Vector3(x, y, 0.11f), new Vector3(0.035f, 0.035f, 0.02f), iron);
                        }
                    }
                    break;
                }
                case ArenaGrille.Pen:
                {
                    int bars = Math.Max(4, (int)MathF.Round(width / 0.5f));
                    for (int i = 0; i < bars; i++)
                    {
                        float x = -half + (i + 0.5f) * width / bars;
                        b.Tube(new Vector3(x, 0.05f, 0f), new Vector3(x, height, 0f), 0.085f, 0.085f, 6, iron);
                    }
                    foreach (float y in new[] { 0.35f, height * 0.5f, height - 0.3f })
                    {
                        b.Box(new Vector3(0f, y, 0f), new Vector3(half + 0.05f, 0.12f, 0.06f), dark);
                        for (int i = 0; i < bars; i++)
                        {
                            float x = -half + (i + 0.5f) * width / bars;
                            b.Tube(new Vector3(x, y, 0.06f), new Vector3(x, y, 0.1f), 0.04f, 0.035f, 5, ArenaStone.Brass(random, 0.7f));
                        }
                    }
                    b.Box(new Vector3(half * 0.55f, height * 0.5f, 0.09f), new Vector3(0.22f, 0.3f, 0.03f), ArenaStone.Brass(random));
                    break;
                }
                default:
                {
                    int bars = Math.Max(5, (int)MathF.Round(width / 0.36f));
                    for (int i = 0; i < bars; i++)
                    {
                        float x = -half + (i + 0.5f) * width / bars;
                        b.Tube(new Vector3(x, 0.05f, 0f), new Vector3(x, height - 0.35f, 0f), 0.045f, 0.045f, 5, iron);
                        b.Tube(new Vector3(x, height - 0.35f, 0f), new Vector3(x, height, 0f), 0.07f, 0.005f, 4, iron);
                    }
                    foreach (float y in new[] { 0.5f, height * 0.62f })
                    {
                        b.Box(new Vector3(0f, y, 0f), new Vector3(half, 0.05f, 0.05f), dark);
                    }
                    // Quatrefoils between the rails, rings of iron.
                    float band = height * 0.62f - 0.5f;
                    int foils = Math.Max(2, (int)MathF.Round(width / 1.1f));
                    for (int i = 0; i < foils; i++)
                    {
                        float x = -half + (i + 0.5f) * width / foils;
                        Vector3 middle = new Vector3(x, 0.5f + band * 0.5f, 0.02f);
                        float r = MathF.Min(width / foils, band) * 0.28f;
                        for (int k = 0; k < 4; k++)
                        {
                            float a = k * MathF.PI * 0.5f;
                            Ring3(b, middle + new Vector3(MathF.Cos(a), MathF.Sin(a), 0f) * r * 0.9f, r * 0.75f, 0.03f, dark);
                        }
                    }
                    break;
                }
            }
        }

        // A ring of iron in the xy plane.
        private static void Ring3(MeshBuilder b, Vector3 middle, float radius, float thick, VertexMaterial iron)
        {
            const int Sides = 10;
            for (int k = 0; k < Sides; k++)
            {
                float a0 = k * MathF.Tau / Sides, a1 = (k + 1) * MathF.Tau / Sides;
                b.Tube(middle + new Vector3(MathF.Cos(a0), MathF.Sin(a0), 0f) * radius, middle + new Vector3(MathF.Cos(a1), MathF.Sin(a1), 0f) * radius, thick, thick, 4, iron, capped: false);
            }
        }

        // ---------------------------------------------------------------- Shared

        /// <summary>A layer of slabs over a box from x0 to x1 and back z0 to front z1, between two heights, cut no longer than <paramref name="longest"/> either way and staggered row by row.</summary>
        internal static void Slabs(MeshBuilder b, float x0, float x1, float z0, float z1, float y0, float y1, float longest, ArenaStoneKind stone, WorldRandom random, float dust)
        {
            int rows = Math.Max(1, (int)MathF.Ceiling((z1 - z0) / longest));
            for (int r = 0; r < rows; r++)
            {
                float za = z0 + (z1 - z0) * r / rows, zb = z0 + (z1 - z0) * (r + 1) / rows;
                float x = x0 - (r % 2) * longest * 0.5f;
                int k = 0;
                while (x < x1 - 0.05f)
                {
                    float piece = longest * random.Range(0.75f, 1f);
                    float a = MathF.Max(x, x0), c = MathF.Min(x + piece, x1);
                    if (x1 - c < 0.35f) c = x1;
                    if (c - a > 0.15f) Lay(b, a + 0.005f, c - 0.005f, y0, y1, zb - 0.005f, za + 0.005f, ArenaStone.DressedFacing(stone, random.Fork(r * 64 + k), y1, dust));
                    x = c;
                    k++;
                }
            }
        }

        private static void Lay(MeshBuilder b, float x0, float x1, float y0, float y1, float zFront, float zBack, Func<Vector3, VertexMaterial> facing)
        {
            PlazaWalls.Lay(b, PlazaPieces.Corners(new Vector3(MathF.Min(x0, x1), y0, MathF.Max(zFront, zBack)), new Vector3(MathF.Max(x0, x1), y1, MathF.Min(zFront, zBack))), ArenaStone.Chamfer, facing);
        }

        private static void Shrink(Vector3[] c, float by)
        {
            Vector3 middle = Vector3.Zero;
            foreach (Vector3 p in c) middle += p;
            middle /= c.Length;
            for (int i = 0; i < c.Length; i++)
            {
                Vector3 toward = middle - c[i];
                toward.Z = 0f;
                float length = toward.Length;
                if (length > 1e-5f) c[i] += toward / length * by;
            }
        }
    }
}
