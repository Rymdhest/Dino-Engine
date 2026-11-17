using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Halls;
using Dino_Engine.Modelling.Procedural.Architecture.Plaza;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Town
{
    /// <summary>
    /// What stands and lies about the Haven's town between its houses (HAVEN.md): the stones trodden into a path to a door, a market's stalls with what each sells set out on
    /// them, a kitchen garden's beds, signs, the Haven's banner, benches, lanterns on posts, a trough fed from a wall, an arbour over a gate, what a household's work leaves by
    /// its door, and the lanterns hung by the water for those who haven't come yet. Model +z is a piece's front, y = 0 where people stand, and what lies on the ground starts
    /// where the ground is drawn (<see cref="PlazaStone.Ground"/>). A person is about 4.6 m tall, so a seat is 1.2 m up and a table 1.9. Nothing here is black iron, oxblood or
    /// brass (LORE.md). Headless.
    /// </summary>
    internal static class TownPieces
    {
        private const float Ground = PlazaStone.Ground;

        /// <summary>The Haven's dyes, as cloth: blossom, gold, sky, sage, lilac and teal, and the cream they're striped with.</summary>
        public static readonly Vector3[] Dyes =
        {
            new Vector3(0.78f, 0.36f, 0.48f), new Vector3(0.8f, 0.6f, 0.16f), new Vector3(0.3f, 0.5f, 0.72f), new Vector3(0.42f, 0.56f, 0.32f), new Vector3(0.58f, 0.44f, 0.7f), new Vector3(0.16f, 0.46f, 0.46f),
        };

        public static readonly Vector3 Cream = new Vector3(0.84f, 0.8f, 0.7f);

        private static VertexMaterial Oak(WorldRandom random, float shade = 1f) => PlazaStone.Timber(random, shade);

        // ---------------------------------------------------------------- Underfoot

        /// <summary>
        /// Flat stones trodden into the ground along +x, each as it was found, a little apart and a little askew, the grass between them: a path to a door. Wide enough, and
        /// they're laid two abreast.
        /// </summary>
        public static void Stepping(MeshBuilder b, float length, float width, uint seed)
        {
            var random = new WorldRandom(seed).Fork("stepping");
            bool pair = width > 1.7f;
            float lane = pair ? width * 0.5f : width;
            for (int row = 0; row < (pair ? 2 : 1); row++)
            {
                float z = pair ? (row - 0.5f) * lane : 0f;
                float x = random.Range(0.05f, 0.4f) + row * 0.45f;
                while (x < length - 0.3f)
                {
                    float stone = MathF.Min(random.Range(0.75f, 1.25f), length - x);
                    if (stone < 0.4f) break;
                    float across = lane * random.Range(0.78f, 0.98f);
                    float top = Ground + random.Range(0.03f, 0.055f);
                    Vector3 centre = new Vector3(x + stone * 0.5f, (top - 0.3f) * 0.5f, z + random.Range(-0.08f, 0.08f));
                    PlazaStone.Block(b, centre, new Vector3(stone * 0.5f, (top + 0.3f) * 0.5f, across * 0.5f), random.Range(-0.16f, 0.16f), 0.06f, PlazaStone.Rubble(random, 0.3f, 0.5f));
                    x += stone + random.Range(0.1f, 0.26f);
                }
            }
        }

        /// <summary>A kerb of rough stones along +x, each sunk on edge: what a bed or a lawn is kept in by.</summary>
        public static void Edging(MeshBuilder b, float length, uint seed)
        {
            var random = new WorldRandom(seed).Fork("edging");
            float x = 0f;
            while (x < length - 0.15f)
            {
                float stone = MathF.Min(random.Range(0.4f, 0.75f), length - x);
                float top = Ground + random.Range(0.16f, 0.3f);
                PlazaStone.Block(b, new Vector3(x + stone * 0.5f, (top - 0.3f) * 0.5f, random.Range(-0.03f, 0.03f)), new Vector3(stone * 0.5f - 0.012f, (top + 0.3f) * 0.5f, random.Range(0.14f, 0.2f)),
                    random.Range(-0.08f, 0.08f), 0.055f, PlazaStone.Rubble(random, 0.4f, 0.6f));
                x += stone;
            }
        }

        // ---------------------------------------------------------------- The market

        /// <summary>
        /// A market stall: four posts under a striped awning that slopes to the front with its hem cut in points, a trestle counter under its front edge with what the stall
        /// sells set out on it, more of it stacked behind and beside, and a lantern hung under the cloth. Its counter is towards +z, where whoever's buying stands.
        /// </summary>
        public static void Stall(MeshBuilder b, float width, StallTrade trade, int colour, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("stall");
            float half = width * 0.5f;
            const float Depth = 1.45f, Front = 4.3f, Back = 5.2f;
            VertexMaterial post = Oak(random, 0.9f);
            foreach (float x in new[] { -half, half })
            {
                b.Tube(new Vector3(x, 0f, Depth), new Vector3(x, Front, Depth), 0.085f, 0.07f, 6, post);
                b.Tube(new Vector3(x, 0f, -Depth), new Vector3(x, Back, -Depth), 0.085f, 0.07f, 6, post);
                b.Beam(new Vector3(x, Front - 0.05f, Depth), new Vector3(x, Back - 0.05f, -Depth), 0.05f, post);
            }
            b.Beam(new Vector3(-half, Front - 0.05f, Depth), new Vector3(half, Front - 0.05f, Depth), 0.05f, post);
            b.Beam(new Vector3(-half, Back - 0.05f, -Depth), new Vector3(half, Back - 0.05f, -Depth), 0.05f, post);

            // The awning: stripes of one of the Haven's dyes and cream, sagging a little between the poles, its front and back hems cut in points.
            Vector3 dye = Dyes[Math.Clamp(colour, 0, Dyes.Length - 1)];
            int stripes = Math.Max(4, (int)MathF.Round(width / 0.52f));
            const float Over = 0.38f;
            Vector3 Cloth(float x, float t)
            {
                float z = Depth + Over - t * (2f * Depth + 2f * Over);
                float y = Front + (Back - Front) * t + 0.04f - 0.09f * MathF.Sin(MathF.PI * t) - 0.05f * MathF.Sin(MathF.PI * (x + half) / width);
                return new Vector3(x, y, z);
            }
            for (int i = 0; i < stripes; i++)
            {
                float x0 = -half - 0.12f + (width + 0.24f) * i / stripes, x1 = -half - 0.12f + (width + 0.24f) * (i + 1) / stripes;
                VertexMaterial cloth = PlazaStone.Cloth((i % 2 == 0 ? dye : Cream) * random.Range(0.94f, 1.03f));
                const int Rows = 4;
                for (int r = 0; r < Rows; r++)
                {
                    float t0 = (float)r / Rows, t1 = (float)(r + 1) / Rows;
                    Vector3 a = Cloth(x0, t0), c = Cloth(x1, t0), d = Cloth(x1, t1), e = Cloth(x0, t1);
                    b.Quad(a, c, d, e, cloth);
                    b.Quad(c, a, e, d, cloth);
                }
                foreach (float t in new[] { 0f, 1f })
                {
                    Vector3 a = Cloth(x0, t), c = Cloth(x1, t);
                    Vector3 drop = new Vector3(0f, -0.42f, t == 0f ? 0.05f : -0.05f);
                    Vector3 tip = (a + c) * 0.5f + drop * 1.45f;
                    int first = b.VertexCount;
                    b.Quad(a + drop, c + drop, c, a, cloth);
                    b.Quad(c + drop, a + drop, a, c, cloth);
                    b.Triangle(a + drop, tip, c + drop, cloth);
                    b.Triangle(c + drop, tip, a + drop, cloth);
                    // The hem lifts in the wind, a point at a time.
                    b.Hang(first, (a + c) * 0.5f, tip, 0.55f, 2f, 0.7f);
                }
            }

            // The counter, on trestles, under a cloth runner.
            const float Top = 1.9f;
            float counterZ = Depth - 0.25f;
            VertexMaterial board = Oak(random, 1.05f);
            b.Box(new Vector3(0f, Top - 0.05f, counterZ), new Vector3(half - 0.15f, 0.05f, 0.62f), board);
            foreach (float x in new[] { -half + 0.55f, half - 0.55f })
            {
                foreach (float z in new[] { -1f, 1f }) b.Beam(new Vector3(x, Ground - 0.1f, counterZ + z * 0.62f), new Vector3(x, Top - 0.1f, counterZ + z * 0.2f), 0.06f, post);
            }
            b.Box(new Vector3(0f, Top + 0.008f, counterZ), new Vector3(half * 0.55f, 0.008f, 0.64f), PlazaStone.Cloth(Cream * 0.96f));
            Wares(b, trade, new Vector3(0f, Top + 0.016f, counterZ), half - 0.3f, 0.5f, random.Fork("wares"));

            // Behind it, what hasn't been set out yet.
            VertexMaterial crate = Oak(random, 0.95f);
            for (int i = 0; i < 3; i++)
            {
                float x = -half + 0.7f + i * (width - 1.4f) * 0.5f + random.Range(-0.15f, 0.15f);
                float size = random.Range(0.42f, 0.56f);
                Crate(b, new Vector3(x, Ground, -Depth + 0.65f + random.Range(-0.1f, 0.15f)), size, random.Range(-0.3f, 0.3f), crate);
                if (random.Chance(0.5f)) Crate(b, new Vector3(x + random.Range(-0.1f, 0.1f), Ground + size * 2f, -Depth + 0.65f), size * 0.85f, random.Range(-0.4f, 0.4f), crate);
            }
            // Something hung from its front pole, and its lantern.
            for (float x = -half + 0.6f; x < half - 0.4f; x += random.Range(0.7f, 1.1f))
            {
                float drop = random.Range(0.35f, 0.7f);
                Vector3 hook = new Vector3(x, Front - 0.1f, Depth);
                b.Beam(hook, hook - Vector3.UnitY * drop, 0.012f, post);
                Vector3 hung = trade switch
                {
                    StallTrade.Produce => new Vector3(0.6f, 0.44f, 0.2f),
                    StallTrade.Fish => new Vector3(0.6f, 0.62f, 0.64f),
                    StallTrade.Cloth => Dyes[random.Int(Dyes.Length)],
                    StallTrade.Bread => new Vector3(0.66f, 0.46f, 0.24f),
                    _ => new Vector3(0.62f, 0.5f, 0.34f),
                };
                b.Tube(hook - Vector3.UnitY * drop, hook - Vector3.UnitY * (drop + 0.42f), 0.07f, 0.1f, 6, PlazaStone.Glaze(hung * random.Range(0.85f, 1.1f)));
            }
            Vector3 lamp = new Vector3(0f, Back - 1.15f, -0.2f);
            b.Beam(new Vector3(0f, Back - 0.45f, -0.2f), lamp + Vector3.UnitY * 0.3f, 0.015f, post);
            b.Box(lamp, new Vector3(0.15f, 0.2f, 0.15f), PlazaStone.Glow(1f, 0.6f, 0.24f, 2.2f));
            b.Box(lamp + Vector3.UnitY * 0.24f, new Vector3(0.2f, 0.035f, 0.2f), PlazaStone.DarkTimber(random));
            b.Box(lamp - Vector3.UnitY * 0.23f, new Vector3(0.18f, 0.03f, 0.18f), PlazaStone.DarkTimber(random));
            lights.Add(AssetLight.Lantern(lamp) with { Intensity = 70f, Radius = 12f, Shadow = false });
        }

        /// <summary>How far a stall reaches from its middle along x and z, for its width.</summary>
        public static Vector2 StallHalf(float width) => new Vector2(width * 0.5f + 0.15f, 1.85f);

        // What a stall sells, set out along its counter about `at`, `half` either way along it and `deep` front to back.
        private static void Wares(MeshBuilder b, StallTrade trade, Vector3 at, float half, float deep, WorldRandom random)
        {
            switch (trade)
            {
                case StallTrade.Produce:
                    {
                        // Shallow boxes tipped towards whoever's buying, each heaped with one thing.
                        Vector3[] kinds = { new(0.6f, 0.1f, 0.07f), new(0.46f, 0.58f, 0.16f), new(0.8f, 0.42f, 0.06f), new(0.34f, 0.5f, 0.2f), new(0.78f, 0.66f, 0.16f), new(0.4f, 0.2f, 0.42f) };
                        int boxes = Math.Max(2, (int)(half * 2f / 0.95f));
                        for (int i = 0; i < boxes; i++)
                        {
                            float x = at.X - half + 2f * half * (i + 0.5f) / boxes;
                            Vector3 colour = kinds[(i + random.Int(kinds.Length)) % kinds.Length];
                            Tray(b, new Vector3(x, at.Y, at.Z), 0.4f, deep * 0.8f, 0.2f, random);
                            Heap(b, new Vector3(x, at.Y + 0.12f, at.Z), 0.34f, deep * 0.68f, 0.085f, colour, random, 12);
                        }
                        break;
                    }
                case StallTrade.Bread:
                    for (float x = at.X - half + 0.35f; x < at.X + half - 0.2f; x += random.Range(0.5f, 0.72f))
                    {
                        Vector3 crust = new Vector3(0.62f, 0.4f, 0.17f) * random.Range(0.85f, 1.15f);
                        if (random.Chance(0.5f)) Ball(b, new Vector3(x, at.Y + 0.11f, at.Z + random.Range(-0.2f, 0.25f)), new Vector3(0.24f, 0.12f, 0.24f), PlazaStone.Glaze(crust));
                        else
                        {
                            float turn = random.Range(-0.5f, 0.5f);
                            Vector3 along = new Vector3(MathF.Sin(turn), 0f, MathF.Cos(turn)) * 0.34f;
                            Vector3 middle = new Vector3(x, at.Y + 0.09f, at.Z + random.Range(-0.1f, 0.15f));
                            b.Tube(middle - along, middle + along, 0.1f, 0.1f, 7, PlazaStone.Glaze(crust));
                        }
                    }
                    Basket(b, new Vector3(at.X + half - 0.2f, at.Y, at.Z - 0.1f), 0.3f, random);
                    Heap(b, new Vector3(at.X + half - 0.2f, at.Y + 0.24f, at.Z - 0.1f), 0.2f, 0.2f, 0.09f, new Vector3(0.7f, 0.5f, 0.24f), random, 5);
                    break;
                case StallTrade.Fish:
                    // A slab with the catch laid on it in rows, silver, and a tub beside.
                    b.Box(new Vector3(at.X, at.Y + 0.04f, at.Z), new Vector3(half * 0.86f, 0.04f, deep * 0.9f), PlazaStone.Glaze(new Vector3(0.52f, 0.56f, 0.58f)));
                    for (float x = at.X - half * 0.8f; x < at.X + half * 0.8f; x += 0.3f)
                    {
                        for (int r = 0; r < 2; r++)
                        {
                            if (random.Chance(0.15f)) continue;
                            Vector3 fish = new Vector3(x + random.Range(-0.04f, 0.04f), at.Y + 0.11f, at.Z + (r - 0.5f) * deep * 0.85f);
                            float shade = random.Range(0.8f, 1.1f);
                            Ball(b, fish, new Vector3(0.08f, 0.035f, 0.2f), PlazaStone.Glaze(new Vector3(0.6f, 0.64f, 0.68f) * shade));
                            b.Triangle(fish + new Vector3(0f, 0f, -0.18f), fish + new Vector3(-0.07f, 0f, -0.3f), fish + new Vector3(0.07f, 0f, -0.3f), PlazaStone.Glaze(new Vector3(0.4f, 0.46f, 0.5f) * shade));
                        }
                    }
                    break;
                case StallTrade.Cloth:
                    {
                        // Bolts lying side by side, their ends to the front, and one unrolled over the counter's edge.
                        float x = at.X - half + 0.25f;
                        int k = random.Int(Dyes.Length);
                        while (x < at.X + half - 0.2f)
                        {
                            float r = random.Range(0.11f, 0.16f);
                            Vector3 colour = (k++ % 4 == 3 ? Cream : Dyes[k % Dyes.Length]) * random.Range(0.85f, 1.05f);
                            b.Tube(new Vector3(x, at.Y + r, at.Z - deep * 0.85f), new Vector3(x, at.Y + r, at.Z + deep * 0.75f), r, r, 8, PlazaStone.Cloth(colour));
                            if (random.Chance(0.3f)) b.Tube(new Vector3(x + r, at.Y + r * 2.7f, at.Z - deep * 0.7f), new Vector3(x + r, at.Y + r * 2.7f, at.Z + deep * 0.6f), r * 0.9f, r * 0.9f, 8, PlazaStone.Cloth(Dyes[(k + 2) % Dyes.Length]));
                            x += r * 2f + random.Range(0.02f, 0.08f);
                        }
                        VertexMaterial length = PlazaStone.Cloth(Dyes[(k + 1) % Dyes.Length]);
                        Vector3 p0 = new Vector3(at.X - 0.5f, at.Y + 0.02f, at.Z + deep), p1 = p0 + Vector3.UnitX * 0.9f;
                        Vector3 drop = new Vector3(0f, -0.95f, 0.1f);
                        b.Quad(p0 + drop, p1 + drop, p1, p0, length);
                        b.Quad(p1 + drop, p0 + drop, p0, p1, length);
                        break;
                    }
                case StallTrade.Pots:
                    for (float x = at.X - half + 0.3f; x < at.X + half - 0.2f; x += random.Range(0.42f, 0.62f))
                    {
                        Vector3 foot = new Vector3(x, at.Y, at.Z + random.Range(-0.25f, 0.25f));
                        VertexMaterial clay = random.Chance(0.6f) ? PlazaStone.Terracotta(random) : PlazaStone.Glaze(new Vector3(0.28f, 0.42f, 0.56f) * random.Range(0.8f, 1.15f));
                        Jar(b, foot, random.Range(0.3f, 0.56f), random.Int(3), clay);
                    }
                    break;
                case StallTrade.Wares:
                    {
                        // The merchant's: blades laid out on a cloth, flasks in a row, a shield stood up at one end and books at the other.
                        VertexMaterial steel = Hall.Metal(170, 172, 178), grip = Hall.Wood(86, 60, 42);
                        for (int i = 0; i < 3; i++)
                        {
                            float x = at.X - half * 0.55f + i * 0.34f;
                            b.Box(new Vector3(x, at.Y + 0.02f, at.Z + 0.05f), new Vector3(0.045f, 0.012f, deep * 0.72f), steel);
                            b.Box(new Vector3(x, at.Y + 0.03f, at.Z - deep * 0.62f), new Vector3(0.13f, 0.02f, 0.025f), Hall.Metal(150, 132, 90));
                            b.Box(new Vector3(x, at.Y + 0.03f, at.Z - deep * 0.8f), new Vector3(0.035f, 0.03f, 0.1f), grip);
                        }
                        Vector3[] draughts = { new(0.62f, 0.1f, 0.1f), new(0.14f, 0.3f, 0.66f), new(0.2f, 0.52f, 0.24f), new(0.66f, 0.5f, 0.1f) };
                        for (int i = 0; i < 6; i++)
                        {
                            Vector3 foot = new Vector3(at.X + 0.3f + i * 0.19f, at.Y, at.Z + deep * 0.5f - (i % 2) * 0.16f);
                            b.Tube(foot, foot + Vector3.UnitY * 0.17f, 0.065f, 0.075f, 7, PlazaStone.Glaze(draughts[i % draughts.Length]), capped: false);
                            b.Tube(foot + Vector3.UnitY * 0.17f, foot + Vector3.UnitY * 0.27f, 0.075f, 0.03f, 7, PlazaStone.Glaze(draughts[i % draughts.Length] * 0.8f));
                        }
                        Vector3 boss = new Vector3(at.X - half + 0.2f, at.Y + 0.52f, at.Z - deep * 0.55f);
                        b.Tube(boss - Vector3.UnitZ * 0.03f, boss + Vector3.UnitZ * 0.03f, 0.5f, 0.5f, 12, PlazaStone.Glaze(Dyes[5]));
                        b.Tube(boss + Vector3.UnitZ * 0.03f, boss + Vector3.UnitZ * 0.1f, 0.14f, 0.06f, 8, steel);
                        for (int i = 0; i < 4; i++) b.Box(new Vector3(at.X + half - 0.3f, at.Y + 0.05f + i * 0.1f, at.Z - 0.2f), new Vector3(0.22f, 0.05f, 0.16f), Hall.BookCovers[random.Int(Hall.BookCovers.Length)]);
                        break;
                    }
                default:
                    // The beta vendor's: a bit of everything, wrapped and tied.
                    for (float x = at.X - half + 0.35f; x < at.X + half - 0.2f; x += random.Range(0.5f, 0.7f))
                    {
                        float size = random.Range(0.14f, 0.24f);
                        Vector3 middle = new Vector3(x, at.Y + size, at.Z + random.Range(-0.2f, 0.25f));
                        Vector3 paper = (random.Chance(0.4f) ? Cream : Dyes[random.Int(Dyes.Length)]) * random.Range(0.9f, 1.05f);
                        Vector3 ribbon = Dyes[random.Int(Dyes.Length)];
                        float turn = random.Range(-0.4f, 0.4f);
                        Hall.At(b, middle, turn, () =>
                        {
                            b.Box(Vector3.Zero, new Vector3(size * 1.2f, size, size), PlazaStone.Cloth(paper));
                            b.Box(Vector3.Zero, new Vector3(size * 1.22f, size * 1.02f, size * 0.16f), PlazaStone.Cloth(ribbon));
                            b.Box(Vector3.Zero, new Vector3(size * 0.16f, size * 1.02f, size * 1.02f), PlazaStone.Cloth(ribbon));
                            Ball(b, new Vector3(0f, size + 0.04f, 0f), new Vector3(0.07f, 0.045f, 0.07f), PlazaStone.Cloth(ribbon));
                        });
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- Gardens

        /// <summary>
        /// A kitchen garden's bed: boards on edge held by pegs, soil heaped a little between them and raked in ridges, and whatever doesn't grow by itself: poles for beans,
        /// tied at their tops and green half way up, or pumpkins swelling among their leaves. What's planted in rows is the builder's to plant.
        /// </summary>
        public static void Bed(MeshBuilder b, float length, float width, BedCrop crop, uint seed)
        {
            var random = new WorldRandom(seed).Fork("bed");
            float hx = length * 0.5f, hz = width * 0.5f;
            VertexMaterial board = Oak(random, 0.82f);
            float top = Ground + 0.34f;
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new Vector3(0f, top * 0.5f - 0.1f, s * hz), new Vector3(hx + 0.04f, top * 0.5f + 0.1f, 0.04f), board);
                b.Box(new Vector3(s * hx, top * 0.5f - 0.1f, 0f), new Vector3(0.04f, top * 0.5f + 0.1f, hz), board);
                for (float x = -hx + 0.3f; x < hx; x += 1.5f) b.Box(new Vector3(x, top * 0.5f - 0.05f, s * (hz + 0.08f)), new Vector3(0.05f, top * 0.5f + 0.1f, 0.04f), Oak(random, 0.7f));
            }
            // The soil, dark and a little heaped.
            VertexMaterial soil = PlazaStone.Soil(0.9f);
            b.Surface(Math.Max(2, (int)(length / 0.6f)), 4, (u, v) =>
            {
                float x = -hx + 0.05f + (length - 0.1f) * u, z = hz - 0.05f - (width - 0.1f) * v;
                float ridge = 0.03f * MathF.Sin(z * MathF.Tau / 0.62f);
                return new Vector3(x, top - 0.08f + 0.07f * MathF.Sin(MathF.PI * v) + ridge, z);
            }, soil);

            if (crop == BedCrop.Beans)
            {
                VertexMaterial pole = Oak(random, 1.1f);
                for (float x = -hx + 0.8f; x <= hx - 0.6f; x += 1.5f)
                {
                    Vector3 tip = new Vector3(x, top + 3.3f, 0f);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * MathF.PI * 0.5f + 0.4f;
                        Vector3 foot = new Vector3(x + MathF.Cos(a) * 0.55f, top - 0.1f, MathF.Sin(a) * MathF.Min(0.55f, hz - 0.15f));
                        b.Beam(foot, tip + (tip - foot) * 0.08f, 0.028f, pole);
                        // What's climbed it so far.
                        for (int j = 1; j <= 5; j++)
                        {
                            Vector3 at = Vector3.Lerp(foot, tip, j * 0.15f + random.Range(-0.03f, 0.03f));
                            float size = random.Range(0.14f, 0.24f) * (1.15f - j * 0.1f);
                            Leaves(b, at, size * 1.1f, random);
                            if (random.Chance(0.25f)) b.Box(at + new Vector3(0f, size * 0.6f, 0.02f), new Vector3(0.045f), PlazaStone.Glaze(new Vector3(0.7f, 0.12f, 0.1f)));
                        }
                    }
                    b.Tube(tip - Vector3.UnitY * 0.12f, tip + Vector3.UnitY * 0.04f, 0.07f, 0.07f, 6, PlazaStone.Cloth(new Vector3(0.6f, 0.52f, 0.36f)));
                }
            }
            else if (crop == BedCrop.Pumpkins)
            {
                for (float x = -hx + 0.7f; x <= hx - 0.5f; x += random.Range(1f, 1.5f))
                {
                    float z = random.Range(-hz + 0.5f, hz - 0.5f);
                    float size = random.Range(0.26f, 0.44f);
                    Vector3 colour = Vector3.Lerp(new Vector3(0.82f, 0.4f, 0.05f), new Vector3(0.72f, 0.56f, 0.1f), random.Float()) * random.Range(0.85f, 1.05f);
                    Ball(b, new Vector3(x, top + size * 0.7f, z), new Vector3(size, size * 0.78f, size), PlazaStone.Glaze(colour));
                    b.Tube(new Vector3(x, top + size * 1.45f, z), new Vector3(x + 0.04f, top + size * 1.75f, z), 0.035f, 0.025f, 5, Oak(random, 0.8f));
                    for (int i = 0; i < 4; i++)
                    {
                        float a = random.Range(0f, MathF.Tau), r = size + random.Range(0.15f, 0.5f);
                        Vector3 at = new Vector3(x + MathF.Cos(a) * r, top + random.Range(0.08f, 0.2f), Math.Clamp(z + MathF.Sin(a) * r, -hz + 0.2f, hz - 0.2f));
                        float span = random.Range(0.22f, 0.34f);
                        Ball(b, at, new Vector3(span, 0.05f, span), Leaf(random, 1.1f));
                    }
                }
            }
            else if (crop == BedCrop.Leeks)
            {
                // Rows of blue-green blades, each a fan of a few.
                VertexMaterial blade = new VertexMaterial(MaterialCatalog.Moss.Index, new Colour(0.2f, 0.34f, 0.2f));
                for (float z = -hz + 0.35f; z <= hz - 0.3f; z += 0.62f)
                {
                    for (float x = -hx + 0.3f; x <= hx - 0.25f; x += 0.42f)
                    {
                        Vector3 foot = new Vector3(x + random.Range(-0.05f, 0.05f), top - 0.05f, z);
                        b.Tube(foot, foot + Vector3.UnitY * 0.22f, 0.04f, 0.035f, 5, PlazaStone.Glaze(new Vector3(0.74f, 0.76f, 0.66f)), capped: false);
                        for (int i = 0; i < 3; i++)
                        {
                            float lean = (i - 1) * 0.3f + random.Range(-0.1f, 0.1f);
                            Vector3 tip = foot + new Vector3(lean, random.Range(0.6f, 0.85f), random.Range(-0.06f, 0.06f));
                            Hall.Board(b, foot + Vector3.UnitY * 0.2f, tip, Vector3.UnitZ, 0.035f, 0.008f, Shaded(blade, random.Range(0.9f, 1.15f)));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// An arbour over a garden's gate: two pairs of posts, laths arched between them, and a rose grown right over it in leaf and flower. The way under it runs along z.
        /// </summary>
        public static void Trellis(MeshBuilder b, float width, float height, int bloom, uint seed)
        {
            var random = new WorldRandom(seed).Fork("trellis");
            float half = width * 0.5f;
            VertexMaterial wood = Oak(random, 1.05f);
            const float Deep = 0.55f;
            foreach (float x in new[] { -half, half })
            {
                foreach (float z in new[] { -Deep, Deep }) b.Box(new Vector3(x, height * 0.5f - 0.2f, z), new Vector3(0.07f, height * 0.5f + 0.2f, 0.07f), wood);
                for (float y = 0.6f; y < height; y += 0.7f) b.Beam(new Vector3(x, y, -Deep), new Vector3(x, y + 0.5f, Deep), 0.02f, wood);
            }
            const int Arcs = 8;
            float rise = half * 0.75f;
            Vector3 At(float t, float z) => new Vector3(-MathF.Cos(t) * half, height + MathF.Sin(t) * rise, z);
            for (int i = 0; i < Arcs; i++)
            {
                float t0 = MathF.PI * i / Arcs, t1 = MathF.PI * (i + 1) / Arcs;
                foreach (float z in new[] { -Deep, Deep }) b.Beam(At(t0, z), At(t1, z), 0.035f, wood);
                b.Beam(At(t1, -Deep), At(t1, Deep), 0.02f, wood);
            }
            // The rose: leaves in clumps up both sides and thick over the top, and its flowers.
            Vector3[] blooms = { new(0.72f, 0.3f, 0.42f), new(0.82f, 0.78f, 0.7f), new(0.74f, 0.52f, 0.14f), new(0.62f, 0.2f, 0.18f), new(0.56f, 0.4f, 0.7f) };
            VertexMaterial flower = PlazaStone.Glaze(blooms[Math.Clamp(bloom, 0, blooms.Length - 1)]);
            void Clump(Vector3 at, float size)
            {
                Leaves(b, at, size * 1.15f, random);
                if (random.Chance(0.7f)) b.Box(at + new Vector3(random.Range(-size, size) * 0.6f, size * 0.7f, random.Range(-size, size) * 0.6f), new Vector3(0.09f, 0.08f, 0.09f), flower);
                if (random.Chance(0.4f)) b.Box(at + new Vector3(random.Range(-size, size) * 0.7f, random.Range(-0.1f, 0.2f), size * 0.85f), new Vector3(0.085f, 0.08f, 0.085f), flower);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                for (float y = 0.5f; y < height; y += random.Range(0.4f, 0.6f)) Clump(new Vector3(s * (half + random.Range(-0.08f, 0.14f)), y, random.Range(-Deep, Deep)), random.Range(0.2f, 0.34f) * (0.6f + 0.4f * y / height));
            }
            for (int i = 0; i <= 14; i++)
            {
                float t = MathF.PI * (i + random.Range(-0.3f, 0.3f)) / 14f;
                Clump(At(t, random.Range(-Deep, Deep)) + Vector3.UnitY * 0.08f, random.Range(0.24f, 0.4f));
            }
        }

        // ---------------------------------------------------------------- Along the way

        /// <summary>A sign hung from an arm on a post, cut out and painted on both sides with what it's for, and read from along z.</summary>
        public static void Sign(MeshBuilder b, SignEmblem emblem, float height, uint seed)
        {
            var random = new WorldRandom(seed).Fork("sign");
            VertexMaterial oak = Oak(random, 0.85f);
            b.Box(new Vector3(0f, height * 0.5f - 0.3f, 0f), new Vector3(0.11f, height * 0.5f + 0.3f, 0.11f), oak);
            b.Box(new Vector3(0f, height + 0.06f, 0f), new Vector3(0.16f, 0.06f, 0.16f), oak);
            float arm = height - 0.45f;
            b.Box(new Vector3(0.85f, arm, 0f), new Vector3(0.85f, 0.055f, 0.055f), oak);
            b.Beam(new Vector3(0.1f, arm - 0.85f, 0f), new Vector3(0.95f, arm - 0.06f, 0f), 0.04f, oak);
            Vector3 middle = new Vector3(0.95f, arm - 0.82f, 0f);
            foreach (float s in new[] { -0.4f, 0.4f }) b.Beam(new Vector3(0.95f + s, arm - 0.05f, 0f), new Vector3(0.95f + s, arm - 0.3f, 0f), 0.013f, oak);
            VertexMaterial board = Hall.Wood(214, 196, 160);
            b.Box(middle, new Vector3(0.62f, 0.52f, 0.035f), board);
            b.Box(middle, new Vector3(0.66f, 0.56f, 0.022f), Oak(random, 0.7f));
            foreach (float face in new[] { 1f, -1f })
            {
                Vector3 at = middle + Vector3.UnitZ * (face * 0.04f);
                Emblem(b, emblem, at, face);
            }
        }

        // A sign's picture, flat on its board: `face` is which side of it this is.
        private static void Emblem(MeshBuilder b, SignEmblem emblem, Vector3 at, float face)
        {
            Vector3 thin = new Vector3(0f, 0f, 0.012f);
            void Slab(float x, float y, float hw, float hh, Vector3 colour) => b.Box(at + new Vector3(x * face, y, face * 0.008f), new Vector3(hw, hh, 0.012f), PlazaStone.Glaze(colour));
            switch (emblem)
            {
                case SignEmblem.Loaf:
                    Slab(0f, -0.04f, 0.4f, 0.2f, new Vector3(0.62f, 0.4f, 0.16f));
                    Slab(0f, 0.16f, 0.3f, 0.1f, new Vector3(0.66f, 0.44f, 0.18f));
                    foreach (float x in new[] { -0.2f, 0f, 0.2f }) Slab(x, 0.02f, 0.03f, 0.14f, new Vector3(0.38f, 0.22f, 0.08f));
                    break;
                case SignEmblem.Fish:
                    Slab(-0.06f, 0f, 0.32f, 0.15f, new Vector3(0.2f, 0.42f, 0.52f));
                    Slab(0.34f, 0f, 0.1f, 0.2f, new Vector3(0.2f, 0.42f, 0.52f));
                    Slab(-0.24f, 0.04f, 0.035f, 0.035f, new Vector3(0.9f, 0.86f, 0.74f));
                    break;
                case SignEmblem.Shears:
                    b.Beam(at + new Vector3(-0.3f * face, -0.3f, face * 0.02f), at + new Vector3(0.24f * face, 0.3f, face * 0.02f), 0.04f, Hall.Metal(150, 152, 158));
                    b.Beam(at + new Vector3(0.3f * face, -0.3f, face * 0.02f), at + new Vector3(-0.24f * face, 0.3f, face * 0.02f), 0.04f, Hall.Metal(150, 152, 158));
                    Slab(-0.3f, -0.32f, 0.09f, 0.09f, new Vector3(0.16f, 0.36f, 0.4f));
                    Slab(0.3f, -0.32f, 0.09f, 0.09f, new Vector3(0.16f, 0.36f, 0.4f));
                    break;
                case SignEmblem.Mug:
                    Slab(-0.04f, -0.04f, 0.22f, 0.28f, new Vector3(0.5f, 0.32f, 0.16f));
                    Slab(0.26f, -0.04f, 0.06f, 0.16f, new Vector3(0.5f, 0.32f, 0.16f));
                    Slab(-0.04f, 0.28f, 0.24f, 0.07f, new Vector3(0.86f, 0.82f, 0.72f));
                    break;
                case SignEmblem.Hammer:
                    b.Beam(at + new Vector3(-0.26f * face, -0.34f, face * 0.02f), at + new Vector3(0.14f * face, 0.2f, face * 0.02f), 0.04f, Hall.Wood(120, 88, 58));
                    Slab(0.16f, 0.24f, 0.22f, 0.11f, new Vector3(0.4f, 0.41f, 0.44f));
                    break;
                case SignEmblem.Boat:
                    Slab(0f, -0.2f, 0.4f, 0.09f, new Vector3(0.36f, 0.24f, 0.14f));
                    Slab(0f, 0.08f, 0.02f, 0.3f, new Vector3(0.36f, 0.24f, 0.14f));
                    Slab(0.16f, 0.1f, 0.14f, 0.2f, new Vector3(0.84f, 0.8f, 0.7f));
                    break;
                case SignEmblem.Key:
                    Slab(-0.2f, 0.12f, 0.14f, 0.14f, new Vector3(0.62f, 0.5f, 0.2f));
                    Slab(0.08f, 0.12f, 0.24f, 0.04f, new Vector3(0.62f, 0.5f, 0.2f));
                    Slab(0.26f, 0.02f, 0.04f, 0.08f, new Vector3(0.62f, 0.5f, 0.2f));
                    break;
                default:
                    Blossom(b, at + Vector3.UnitZ * (face * 0.012f), 0.3f, face, new Vector3(0.78f, 0.36f, 0.48f));
                    break;
            }
        }

        /// <summary>The Haven's blossom: five petals round a gold middle, flat, facing along z.</summary>
        public static void Blossom(MeshBuilder b, Vector3 at, float radius, float face, Vector3 colour)
        {
            VertexMaterial petal = PlazaStone.Glaze(colour);
            for (int i = 0; i < 5; i++)
            {
                float a = MathF.PI * 0.5f + i * MathF.Tau / 5f;
                Vector3 out_ = new Vector3(MathF.Cos(a) * face, MathF.Sin(a), 0f);
                Vector3 side = new Vector3(-out_.Y, out_.X, 0f);
                Vector3 p0 = at + out_ * radius * 0.2f, tip = at + out_ * radius;
                Vector3 l = at + out_ * radius * 0.62f + side * radius * 0.34f, r = at + out_ * radius * 0.62f - side * radius * 0.34f;
                Hall.Face(b, p0, r, tip, l, Vector3.UnitZ * face, petal);
            }
            b.Tube(at - Vector3.UnitZ * (face * 0.004f), at + Vector3.UnitZ * (face * 0.012f), radius * 0.2f, radius * 0.2f, 8, PlazaStone.Glaze(new Vector3(0.8f, 0.6f, 0.16f)));
        }

        /// <summary>
        /// The Haven's banner: a pole with a gilt ball, a yard across its top, and a long cloth hung from it in one of the Haven's dyes with the blossom on it and its foot
        /// cut in two tails. It hangs facing +z.
        /// </summary>
        public static void Banner(MeshBuilder b, float height, int colour, uint seed)
        {
            var random = new WorldRandom(seed).Fork("banner");
            VertexMaterial oak = Oak(random, 0.9f);
            PlazaStone.Block(b, new Vector3(0f, (Ground + 0.4f - 0.5f) * 0.5f, 0f), new Vector3(0.36f, (Ground + 0.4f + 0.5f) * 0.5f, 0.36f), 0f, PlazaStone.BigChamfer, PlazaStone.DressedFacing(random, 0.3f, 0.3f));
            b.Tube(new Vector3(0f, Ground + 0.4f, 0f), new Vector3(0f, height, 0f), 0.12f, 0.08f, 8, oak, capped: false);
            Ball(b, new Vector3(0f, height + 0.1f, 0f), new Vector3(0.15f), PlazaStone.Glaze(new Vector3(0.78f, 0.58f, 0.15f)));
            float yard = height - 0.5f;
            const float Half = 0.78f;
            b.Tube(new Vector3(-Half - 0.1f, yard, 0.12f), new Vector3(Half + 0.1f, yard, 0.12f), 0.04f, 0.04f, 6, oak);
            Vector3 dye = Dyes[Math.Clamp(colour, 0, Dyes.Length - 1)] * random.Range(0.95f, 1.03f);
            VertexMaterial cloth = PlazaStone.Cloth(dye), edge = PlazaStone.Cloth(Cream);
            float drop = MathF.Min(height - 2.4f, 4.6f);
            const int Rows = 6;
            int firstCloth = b.VertexCount;
            Vector3 Hang(float x, float t) => new Vector3(x, yard - 0.05f - drop * t, 0.14f + 0.12f * MathF.Sin(t * 2.6f + x) * t);
            for (int r = 0; r < Rows; r++)
            {
                float t0 = (float)r / Rows, t1 = (float)(r + 1) / Rows;
                foreach ((float x0, float x1, VertexMaterial m) in new[] { (-Half, -Half + 0.12f, edge), (-Half + 0.12f, Half - 0.12f, cloth), (Half - 0.12f, Half, edge) })
                {
                    Vector3 a = Hang(x0, t1), c = Hang(x1, t1), d = Hang(x1, t0), e = Hang(x0, t0);
                    b.Quad(a, c, d, e, m);
                    b.Quad(c, a, e, d, m);
                }
            }
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 a = Hang(s * Half, 1f), c = Hang(0f, 1f), tip = Hang(s * Half * 0.6f, 1f) - Vector3.UnitY * 0.7f;
                b.Triangle(a, c, tip, cloth);
                b.Triangle(c, a, tip, cloth);
            }
            foreach (float face in new[] { 1f, -1f }) Blossom(b, Hang(0f, 0.36f) + Vector3.UnitZ * (face * 0.012f), 0.46f, face, colour == 0 ? Cream : new Vector3(0.82f, 0.5f, 0.6f));
            // It hangs from its yard and moves with the wind, all of a piece, its blossom with it.
            b.Hang(firstCloth, new Vector3(0f, yard - 0.05f, 0.14f), new Vector3(0f, yard - 0.75f - drop, 0.14f), 0.42f, 0.9f, 0.55f);
        }

        /// <summary>A bench of oak with a back and arms, facing +z.</summary>
        public static void Bench(MeshBuilder b, float length, uint seed)
        {
            var random = new WorldRandom(seed).Fork("bench");
            float half = length * 0.5f;
            VertexMaterial oak = Oak(random), dark = PlazaStone.DarkTimber(random);
            const float Seat = 1.18f;
            for (int i = 0; i < 3; i++) b.Box(new Vector3(0f, Seat, -0.22f + i * 0.24f), new Vector3(half, 0.045f, 0.105f), Oak(random.Fork(i)));
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (half - 0.14f);
                b.Box(new Vector3(x, Seat * 0.5f - 0.1f, 0.26f), new Vector3(0.07f, Seat * 0.5f + 0.1f, 0.07f), dark);
                b.Box(new Vector3(x, 1.15f, -0.34f), new Vector3(0.07f, 1.35f, 0.07f), dark);
                b.Box(new Vector3(x, Seat + 0.52f, -0.03f), new Vector3(0.06f, 0.045f, 0.36f), dark);
                b.Box(new Vector3(x, Seat - 0.3f, -0.04f), new Vector3(0.05f, 0.05f, 0.3f), dark);
            }
            foreach (float y in new[] { Seat + 0.55f, Seat + 0.98f }) b.Box(new Vector3(0f, y, -0.36f), new Vector3(half, 0.14f, 0.035f), oak);
        }

        public static Vector2 BenchHalf(float length) => new Vector2(length * 0.5f, 0.45f);

        /// <summary>A lantern on a timber post, hung from an arm out towards +z: what lights a lane.</summary>
        public static void Lamp(MeshBuilder b, float height, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("lamp");
            VertexMaterial oak = Oak(random, 0.82f), dark = PlazaStone.DarkTimber(random);
            PlazaStone.Block(b, new Vector3(0f, (Ground + 0.3f - 0.5f) * 0.5f, 0f), new Vector3(0.3f, (Ground + 0.3f + 0.5f) * 0.5f, 0.3f), 0f, 0.05f, PlazaStone.DressedFacing(random, 0.3f, 0.3f));
            b.Box(new Vector3(0f, height * 0.5f + 0.1f, 0f), new Vector3(0.1f, height * 0.5f - 0.1f, 0.1f), oak);
            b.Box(new Vector3(0f, height + 0.04f, 0f), new Vector3(0.15f, 0.05f, 0.15f), dark);
            float arm = height - 0.4f;
            b.Box(new Vector3(0f, arm, 0.5f), new Vector3(0.05f, 0.05f, 0.5f), oak);
            b.Beam(new Vector3(0f, arm - 0.7f, 0.1f), new Vector3(0f, arm - 0.05f, 0.7f), 0.035f, oak);
            Vector3 at = new Vector3(0f, arm - 0.6f, 0.9f);
            b.Beam(new Vector3(0f, arm - 0.05f, 0.9f), at + Vector3.UnitY * 0.34f, 0.014f, dark);
            b.Box(at, new Vector3(0.16f, 0.22f, 0.16f), PlazaStone.Glow(1f, 0.6f, 0.24f, 2.4f));
            b.Box(at + Vector3.UnitY * 0.26f, new Vector3(0.22f, 0.035f, 0.22f), dark);
            b.Tube(at + Vector3.UnitY * 0.295f, at + Vector3.UnitY * 0.42f, 0.22f, 0.04f, 4, dark);
            b.Box(at - Vector3.UnitY * 0.24f, new Vector3(0.19f, 0.03f, 0.19f), dark);
            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f }) b.Box(at + new Vector3(x * 0.17f, 0f, z * 0.17f), new Vector3(0.022f, 0.23f, 0.022f), dark);
            }
            lights.Add(AssetLight.Lantern(at) with { Intensity = 120f, Radius = 17f });
        }

        /// <summary>
        /// A trough cut from one block, standing against a wall behind it (-z), fed by a spout out of that wall and running over at one end: where the town's water comes out.
        /// </summary>
        public static void Trough(MeshBuilder b, float length, uint seed)
        {
            var random = new WorldRandom(seed).Fork("trough");
            float half = length * 0.5f;
            Func<Vector3, VertexMaterial> stone = PlazaStone.DressedFacing(random, 0.5f, 0.4f);
            const float Top = 1.25f, Deep = 0.62f, Wall = 0.2f;
            // Four sides and a floor, each a block, with water in it.
            PlazaStone.Block(b, new Vector3(0f, Top * 0.5f - 0.2f, Deep), new Vector3(half, Top * 0.5f + 0.2f, Wall * 0.5f), 0f, 0.05f, stone);
            PlazaStone.Block(b, new Vector3(0f, Top * 0.5f - 0.2f, -Deep), new Vector3(half, Top * 0.5f + 0.2f, Wall * 0.5f), 0f, 0.05f, stone);
            foreach (float s in new[] { -1f, 1f }) PlazaStone.Block(b, new Vector3(s * (half - Wall * 0.5f), Top * 0.5f - 0.2f, 0f), new Vector3(Wall * 0.5f, Top * 0.5f + 0.2f, Deep - Wall * 0.5f), 0f, 0.05f, stone);
            b.Box(new Vector3(0f, Top - 0.2f, 0f), new Vector3(half - Wall, 0.02f, Deep - Wall * 0.5f), Hall.Mirror(46, 62, 64));
            // The spout, out of a carved block in the wall.
            PlazaStone.Block(b, new Vector3(0f, Top + 1.1f, -Deep - 0.25f), new Vector3(0.42f, 0.42f, 0.3f), 0f, 0.07f, stone);
            b.Tube(new Vector3(0f, Top + 1.05f, -Deep), new Vector3(0f, Top + 0.92f, -Deep + 0.5f), 0.06f, 0.05f, 7, Hall.Metal(120, 132, 118));
            b.Tube(new Vector3(0f, Top + 0.9f, -Deep + 0.5f), new Vector3(0f, Top - 0.18f, -Deep + 0.56f), 0.028f, 0.04f, 5, Hall.Mirror(120, 150, 156), capped: false);
            // A bucket left on its rim, and one on the ground.
            foreach (Vector3 foot in new[] { new Vector3(half * 0.55f, Top, Deep * 0.2f), new Vector3(-half - 0.6f, Ground, Deep * 0.3f) })
            {
                b.Tube(foot, foot + Vector3.UnitY * 0.5f, 0.2f, 0.25f, 9, Oak(random, 0.9f), capped: false);
                b.Tube(foot + Vector3.UnitY * 0.08f, foot + Vector3.UnitY * 0.12f, 0.215f, 0.215f, 9, Hall.Iron(96), capped: false);
                b.Tube(foot + Vector3.UnitY * 0.4f, foot + Vector3.UnitY * 0.42f, 0.23f, 0.23f, 9, Hall.Mirror(52, 66, 68));
            }
        }

        /// <summary>
        /// The lanterns hung for those who haven't come yet: three posts by the water with a beam across them, a lantern on a cord for everyone someone's still waiting for,
        /// each a different length, ribbons tied to the beam, and stones at the posts' feet where people leave things. It faces +z, out to sea.
        /// </summary>
        public static void Memorial(MeshBuilder b, float width, int lanterns, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("memorial");
            float half = width * 0.5f;
            VertexMaterial oak = Oak(random, 0.72f);
            const float Beam = 5.4f;
            foreach (float x in new[] { -half, 0f, half })
            {
                b.Box(new Vector3(x, Beam * 0.5f - 0.3f, 0f), new Vector3(0.13f, Beam * 0.5f + 0.3f, 0.13f), oak);
                b.Beam(new Vector3(x, Beam - 1.1f, 0f), new Vector3(x + (x <= 0f ? 0.9f : -0.9f), Beam - 0.1f, 0f), 0.06f, oak);
                // Stones heaped at its foot.
                for (int i = 0; i < 5; i++)
                {
                    float a = random.Range(0f, MathF.Tau), r = random.Range(0.3f, 0.6f);
                    float size = random.Range(0.14f, 0.26f);
                    PlazaStone.Block(b, new Vector3(x + MathF.Cos(a) * r, Ground + size * 0.55f, MathF.Sin(a) * r), new Vector3(size, size * 0.7f, size * 0.85f), random.Range(0f, 3f), 0.05f, PlazaStone.Rubble(random, 0.2f, 0.5f));
                }
            }
            b.Box(new Vector3(0f, Beam + 0.1f, 0f), new Vector3(half + 0.5f, 0.11f, 0.11f), oak);
            VertexMaterial dark = PlazaStone.DarkTimber(random);
            int lit = 0;
            for (int i = 0; i < lanterns; i++)
            {
                float x = -half + 0.45f + (width - 0.9f) * (i + 0.5f) / lanterns + random.Range(-0.1f, 0.1f);
                float cord = random.Range(0.7f, 2.3f);
                Vector3 at = new Vector3(x, Beam - cord, random.Range(-0.05f, 0.05f));
                int first = b.VertexCount;
                b.Beam(new Vector3(x, Beam, 0f), at + Vector3.UnitY * 0.2f, 0.012f, dark);
                float size = random.Range(0.11f, 0.15f);
                // Paper round a flame, each its own warm white.
                b.Tube(at - Vector3.UnitY * size * 1.3f, at + Vector3.UnitY * size * 1.3f, size, size, 6, PlazaStone.Glow(1f, random.Range(0.62f, 0.78f), random.Range(0.3f, 0.46f), 1.7f), capped: false);
                b.Tube(at + Vector3.UnitY * size * 1.3f, at + Vector3.UnitY * size * 1.7f, size * 1.05f, size * 0.3f, 6, dark);
                b.Tube(at - Vector3.UnitY * size * 1.5f, at - Vector3.UnitY * size * 1.3f, size * 0.8f, size * 1.05f, 6, dark);
                // Each swings on its cord, the long ones furthest.
                b.Hang(first, new Vector3(x, Beam, 0f), at - Vector3.UnitY * size * 1.5f, 0.3f, 0.5f, 0.4f);
                // A handful of them light the ground; the rest only glow, as that many lights would each cost what one does.
                if (i % Math.Max(1, lanterns / 3) == 0 && lit++ < 3) lights.Add(AssetLight.Lantern(at) with { Intensity = 46f, Radius = 11f, Shadow = false, Flicker = 0.2f });
            }
            // Ribbons, tied on and left to hang.
            for (float x = -half + 0.2f; x < half; x += random.Range(0.3f, 0.55f))
            {
                VertexMaterial ribbon = PlazaStone.Cloth((random.Chance(0.35f) ? Cream : Dyes[random.Int(Dyes.Length)]) * random.Range(0.9f, 1.05f));
                float drop = random.Range(0.5f, 1.3f), sway = random.Range(-0.12f, 0.12f);
                Vector3 a = new Vector3(x, Beam, 0.12f), tip = new Vector3(x + sway, Beam - drop, 0.16f);
                int first = b.VertexCount;
                b.Quad(tip - Vector3.UnitX * 0.045f, tip + Vector3.UnitX * 0.045f, a + Vector3.UnitX * 0.045f, a - Vector3.UnitX * 0.045f, ribbon);
                b.Quad(tip + Vector3.UnitX * 0.045f, tip - Vector3.UnitX * 0.045f, a - Vector3.UnitX * 0.045f, a + Vector3.UnitX * 0.045f, ribbon);
                b.Hang(first, a, tip, 0.9f, 3f, 1.1f);
            }
        }

        /// <summary>
        /// A fly over somewhere to sit: a ridge along x between two tall poles with a gilt ball on each, the cloth falling either way from it to a pole at each corner, in
        /// stripes of one of the Haven's dyes and cream, sagging between its poles, its hems cut in points that lift in the wind, and stays out from every pole. Two slopes
        /// and stripes are what say cloth from above: a flat sail read as a sheet of paper laid on the grass.
        /// </summary>
        public static void Canopy(MeshBuilder b, float width, float depth, float height, int colour, uint seed)
        {
            var random = new WorldRandom(seed).Fork("canopy");
            float hx = width * 0.5f, hz = depth * 0.5f;
            VertexMaterial pole = Oak(random, 0.95f), rope = PlazaStone.Cloth(new Vector3(0.6f, 0.54f, 0.42f));
            float ridge = height + MathF.Min(hz * 0.62f, 1.5f);
            foreach (float x in new[] { -hx, hx })
            {
                b.Tube(new Vector3(x, 0f, 0f), new Vector3(x, ridge + 0.32f, 0f), 0.1f, 0.07f, 6, pole);
                Ball(b, new Vector3(x, ridge + 0.42f, 0f), new Vector3(0.12f), PlazaStone.Glaze(new Vector3(0.78f, 0.58f, 0.15f)));
                b.Beam(new Vector3(x, ridge, 0f), new Vector3(x + MathF.Sign(x) * 2.3f, Ground, 0f), 0.012f, rope);
                foreach (float z in new[] { -hz, hz })
                {
                    b.Tube(new Vector3(x, 0f, z), new Vector3(x, height, z), 0.075f, 0.06f, 6, pole);
                    b.Beam(new Vector3(x, height - 0.1f, z), new Vector3(x + MathF.Sign(x) * 1.15f, Ground, z + MathF.Sign(z) * 1.15f), 0.012f, rope);
                }
            }
            b.Beam(new Vector3(-hx, ridge, 0f), new Vector3(hx, ridge, 0f), 0.045f, pole);

            Vector3 dye = Dyes[Math.Clamp(colour, 0, Dyes.Length - 1)];
            int stripes = Math.Max(5, (int)MathF.Round(width / 0.58f));
            if (stripes % 2 == 0) stripes++;        // So that both ends are the dye.
            const float Over = 0.32f;
            const int Rows = 4;
            // t is 0 at the ridge and 1 at the hem, which is a little past the poles; it sags between ridge and eave, and between the poles along it.
            Vector3 Cloth(float x, float t, float side)
            {
                float y = ridge + 0.06f - (ridge - height + 0.1f) * t - 0.16f * MathF.Sin(MathF.PI * t) - 0.07f * MathF.Sin(MathF.PI * Math.Clamp((x + hx) / width, 0f, 1f)) * t;
                return new Vector3(x, y, side * (hz + Over) * t);
            }
            for (int i = 0; i < stripes; i++)
            {
                float x0 = -hx - 0.14f + (width + 0.28f) * i / stripes, x1 = -hx - 0.14f + (width + 0.28f) * (i + 1) / stripes;
                VertexMaterial cloth = PlazaStone.Cloth((i % 2 == 0 ? dye : Cream) * random.Range(0.94f, 1.03f));
                foreach (float side in new[] { 1f, -1f })
                {
                    for (int r = 0; r < Rows; r++)
                    {
                        float t0 = (float)r / Rows, t1 = (float)(r + 1) / Rows;
                        Vector3 a = Cloth(x0, t1, side), c = Cloth(x1, t1, side), d = Cloth(x1, t0, side), e = Cloth(x0, t0, side);
                        b.Quad(a, c, d, e, cloth);
                        b.Quad(c, a, e, d, cloth);
                    }
                    Vector3 h0 = Cloth(x0, 1f, side), h1 = Cloth(x1, 1f, side);
                    Vector3 drop = new Vector3(0f, -0.4f, side * 0.05f);
                    Vector3 tip = (h0 + h1) * 0.5f + drop * 1.5f;
                    int first = b.VertexCount;
                    b.Quad(h0 + drop, h1 + drop, h1, h0, cloth);
                    b.Quad(h1 + drop, h0 + drop, h0, h1, cloth);
                    b.Triangle(h0 + drop, tip, h1 + drop, cloth);
                    b.Triangle(h1 + drop, tip, h0 + drop, cloth);
                    b.Hang(first, (h0 + h1) * 0.5f, tip, 0.55f, 2f, 0.7f);
                }
            }
        }

        // ---------------------------------------------------------------- What a household's work leaves by its door

        /// <summary>What a household's work leaves standing by its door, as a group about the origin, a couple of metres across: it says who lives there.</summary>
        public static void Yardwork(MeshBuilder b, YardTrade trade, uint seed)
        {
            var random = new WorldRandom(seed).Fork("yardwork");
            VertexMaterial oak = Oak(random), dark = PlazaStone.DarkTimber(random);
            switch (trade)
            {
                case YardTrade.Washing:
                    // Two tubs on a bench, one with the wash in it, a washboard, a basket of what's wrung out and a pail.
                    b.Box(new Vector3(0f, 1.05f, 0f), new Vector3(1.35f, 0.05f, 0.42f), oak);
                    foreach (float x in new[] { -1.1f, 1.1f }) b.Box(new Vector3(x, 0.5f, 0f), new Vector3(0.07f, 0.55f, 0.36f), dark);
                    foreach (float x in new[] { -0.6f, 0.62f })
                    {
                        Vector3 foot = new Vector3(x, 1.1f, 0f);
                        b.Tube(foot, foot + Vector3.UnitY * 0.5f, 0.46f, 0.52f, 11, oak, capped: false);
                        b.Tube(foot + Vector3.UnitY * 0.18f, foot + Vector3.UnitY * 0.24f, 0.5f, 0.5f, 11, Hall.Iron(100), capped: false);
                        b.Tube(foot + Vector3.UnitY * 0.36f, foot + Vector3.UnitY * 0.39f, 0.49f, 0.49f, 11, x < 0f ? PlazaStone.Glaze(new Vector3(0.7f, 0.74f, 0.76f)) : Hall.Mirror(56, 70, 74));
                    }
                    Ball(b, new Vector3(-0.6f, 1.55f, 0.05f), new Vector3(0.3f, 0.12f, 0.26f), PlazaStone.Cloth(Cream));
                    b.Box(new Vector3(0.62f, 1.62f, -0.2f), new Vector3(0.2f, 0.34f, 0.03f), Oak(random, 1.2f));
                    Basket(b, new Vector3(1.9f, Ground, 0.5f), 0.42f, random);
                    Heap(b, new Vector3(1.9f, Ground + 0.4f, 0.5f), 0.3f, 0.3f, 0.14f, Cream, random, 4, cloth: true);
                    break;
                case YardTrade.Baking:
                    // Sacks of flour, one open, a board of loaves cooling on trestles, the peel leaned against them and faggots for the oven.
                    for (int i = 0; i < 3; i++) Sack(b, new Vector3(-1.4f + i * 0.62f, Ground, -0.5f + (i % 2) * 0.3f), random.Range(0.36f, 0.44f), random);
                    b.Box(new Vector3(0.9f, 1.5f, 0.2f), new Vector3(0.95f, 0.035f, 0.4f), Oak(random, 1.15f));
                    foreach (float x in new[] { 0.2f, 1.6f }) b.Box(new Vector3(x, 0.72f, 0.2f), new Vector3(0.06f, 0.76f, 0.3f), dark);
                    for (int i = 0; i < 5; i++) Ball(b, new Vector3(0.25f + i * 0.33f, 1.62f, 0.2f + ((i % 2) - 0.5f) * 0.3f), new Vector3(0.16f, 0.09f, 0.16f), PlazaStone.Glaze(new Vector3(0.64f, 0.42f, 0.18f) * random.Range(0.85f, 1.12f)));
                    b.Beam(new Vector3(2.1f, Ground, 0.7f), new Vector3(1.75f, 2.9f, 0.3f), 0.035f, oak);
                    b.Box(new Vector3(1.72f, 3.1f, 0.28f), new Vector3(0.24f, 0.26f, 0.02f), Oak(random, 1.2f));
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 at = new Vector3(-0.2f + (i % 2) * 0.5f, Ground + 0.2f + (i / 2) * 0.36f, 1.1f);
                        b.Tube(at - Vector3.UnitX * 0.7f, at + Vector3.UnitX * 0.7f, 0.19f, 0.17f, 7, Hall.Bark(112, 92, 74));
                    }
                    break;
                case YardTrade.Fishing:
                    // Creels stacked, an oar or two against them, a barrel of salt and a row of floats on a line.
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 foot = new Vector3(-0.9f + (i % 2) * 0.75f, Ground + (i / 2) * 0.62f, -0.3f + (i / 2) * 0.1f);
                        Creel(b, foot, random);
                    }
                    b.Beam(new Vector3(1f, Ground, 0.6f), new Vector3(0.5f, 3.6f, -0.2f), 0.04f, oak);
                    b.Box(new Vector3(0.44f, 3.9f, -0.26f), new Vector3(0.13f, 0.4f, 0.02f), oak);
                    b.Beam(new Vector3(1.3f, Ground, 0.5f), new Vector3(0.9f, 3.3f, -0.3f), 0.04f, Oak(random, 0.8f));
                    Vector3 barrel = new Vector3(1.9f, Ground - 0.05f, -0.2f);
                    b.Tube(barrel, barrel + Vector3.UnitY * 0.8f, 0.42f, 0.5f, 10, oak, capped: false);
                    b.Tube(barrel + Vector3.UnitY * 0.8f, barrel + Vector3.UnitY * 1.5f, 0.5f, 0.44f, 10, oak, capped: false);
                    b.Tube(barrel + Vector3.UnitY * 1.38f, barrel + Vector3.UnitY * 1.42f, 0.43f, 0.43f, 10, PlazaStone.Glaze(new Vector3(0.76f, 0.75f, 0.72f)));
                    for (int i = 0; i < 6; i++) Ball(b, new Vector3(-1.7f + i * 0.2f, Ground + 0.1f, 1f + MathF.Sin(i * 1.3f) * 0.12f), new Vector3(0.09f), PlazaStone.Glaze(i % 2 == 0 ? new Vector3(0.66f, 0.2f, 0.14f) : new Vector3(0.78f, 0.74f, 0.6f)));
                    break;
                case YardTrade.Joinery:
                    // Two trestles with a plank across them part planed, shavings under it, a chair with three legs yet and a stack of boards.
                    foreach (float x in new[] { -1.1f, 1.1f })
                    {
                        b.Box(new Vector3(x, 1.5f, 0f), new Vector3(0.07f, 0.07f, 0.6f), dark);
                        foreach (float z in new[] { -1f, 1f }) b.Beam(new Vector3(x, Ground - 0.1f, z * 0.75f), new Vector3(x, 1.5f, z * 0.3f), 0.055f, dark);
                    }
                    b.Box(new Vector3(0.1f, 1.63f, 0f), new Vector3(1.9f, 0.05f, 0.22f), Hall.Wood(206, 172, 124));
                    for (int i = 0; i < 16; i++)
                    {
                        Vector3 at = new Vector3(random.Range(-1.2f, 1.3f), Ground + 0.03f, random.Range(-0.5f, 0.7f));
                        b.Tube(at, at + new Vector3(random.Range(-0.12f, 0.12f), 0.02f, random.Range(-0.12f, 0.12f)), 0.045f, 0.03f, 5, Hall.Wood(214, 184, 136), capped: false);
                    }
                    Vector3 chair = new Vector3(2.2f, Ground, 0.7f);
                    b.Box(chair + new Vector3(0f, 1.15f, 0f), new Vector3(0.36f, 0.04f, 0.34f), Hall.Wood(198, 164, 118));
                    foreach ((float x, float z) in new[] { (-0.3f, -0.28f), (0.3f, -0.28f), (-0.3f, 0.28f) }) b.Box(chair + new Vector3(x, 0.55f, z), new Vector3(0.04f, 0.6f, 0.04f), Hall.Wood(198, 164, 118));
                    b.Box(chair + new Vector3(0f, 1.9f, -0.3f), new Vector3(0.34f, 0.28f, 0.03f), Hall.Wood(198, 164, 118));
                    for (int i = 0; i < 5; i++) b.Box(new Vector3(-2.2f, Ground + 0.07f + i * 0.13f, 0.2f + random.Range(-0.05f, 0.05f)), new Vector3(0.24f, 0.05f, 1.3f), Hall.Wood(196 - i * 6, 160 - i * 4, 116));
                    break;
                case YardTrade.Gardening:
                    // A barrow with a spade across it, a stack of pots, a watering can and a fork stuck in the ground.
                    Hall.At(b, new Vector3(-0.6f, 0f, 0f), 0.4f, () =>
                    {
                        b.Box(new Vector3(0f, 0.95f, 0f), new Vector3(0.5f, 0.05f, 0.75f), oak);
                        foreach (float s in new[] { -1f, 1f })
                        {
                            b.Box(new Vector3(s * 0.52f, 1.2f, 0f), new Vector3(0.035f, 0.25f, 0.75f), oak);
                            b.Beam(new Vector3(s * 0.42f, 0.95f, -0.6f), new Vector3(s * 0.5f, 1.15f, -1.7f), 0.04f, dark);
                            b.Box(new Vector3(s * 0.4f, 0.45f, -0.5f), new Vector3(0.04f, 0.5f, 0.04f), dark);
                        }
                        b.Tube(new Vector3(-0.09f, 0.5f, 0.85f), new Vector3(0.09f, 0.5f, 0.85f), 0.5f, 0.5f, 12, dark);
                        b.Box(new Vector3(0f, 1.25f, 0.76f), new Vector3(0.5f, 0.22f, 0.035f), oak);
                        Heap(b, new Vector3(0f, 1.05f, 0f), 0.4f, 0.6f, 0.13f, new Vector3(0.3f, 0.22f, 0.15f), random, 8);
                        b.Beam(new Vector3(-0.3f, 1.4f, -0.9f), new Vector3(0.35f, 1.5f, 0.9f), 0.03f, Oak(random, 1.1f));
                    });
                    for (int i = 0; i < 3; i++) Jar(b, new Vector3(1.2f + (i % 2) * 0.5f, Ground + (i / 2) * 0.4f, -0.5f), 0.42f, 3, PlazaStone.Terracotta(random));
                    Vector3 can = new Vector3(1.5f, Ground, 0.6f);
                    b.Tube(can, can + Vector3.UnitY * 0.55f, 0.24f, 0.22f, 9, Hall.Metal(128, 138, 132));
                    b.Beam(can + new Vector3(0.2f, 0.2f, 0f), can + new Vector3(0.75f, 0.6f, 0f), 0.035f, Hall.Metal(128, 138, 132));
                    b.Beam(new Vector3(2.4f, Ground - 0.3f, -0.2f), new Vector3(2.5f, 3f, -0.3f), 0.03f, oak);
                    break;
                case YardTrade.Smithing:
                    {
                        // The anvil on its stump, a hammer left on it, and the tub the work's quenched in.
                        VertexMaterial steel = Hall.Metal(120, 122, 128), bark = Hall.Bark(112, 94, 78);
                        b.Tube(new Vector3(0f, -0.2f, 0f), new Vector3(0f, 1.05f, 0f), 0.52f, 0.46f, 9, bark, capped: false);
                        b.Tube(new Vector3(0f, 1.05f, 0f), new Vector3(0f, 1.07f, 0f), 0.46f, 0.46f, 9, Hall.Wood(186, 150, 106));
                        b.Box(new Vector3(0f, 1.2f, 0f), new Vector3(0.34f, 0.13f, 0.2f), steel);
                        b.Box(new Vector3(0f, 1.42f, 0f), new Vector3(0.2f, 0.1f, 0.15f), steel);
                        b.Box(new Vector3(0.04f, 1.62f, 0f), new Vector3(0.5f, 0.11f, 0.19f), Hall.Metal(150, 152, 158));
                        b.Tube(new Vector3(-0.46f, 1.62f, 0f), new Vector3(-0.95f, 1.66f, 0f), 0.11f, 0.02f, 7, steel);
                        b.Beam(new Vector3(0.1f, 1.77f, -0.2f), new Vector3(0.35f, 1.79f, 0.45f), 0.03f, oak);
                        b.Box(new Vector3(0.08f, 1.78f, -0.24f), new Vector3(0.1f, 0.07f, 0.07f), steel);
                        Vector3 tub = new Vector3(1.5f, Ground - 0.05f, 0.5f);
                        b.Tube(tub, tub + Vector3.UnitY * 1.1f, 0.62f, 0.7f, 11, oak, capped: false);
                        foreach (float y in new[] { 0.25f, 0.85f }) b.Tube(tub + Vector3.UnitY * (y - 0.04f), tub + Vector3.UnitY * (y + 0.04f), 0.71f, 0.71f, 11, Hall.Iron(92), capped: false);
                        b.Tube(tub + Vector3.UnitY * 0.96f, tub + Vector3.UnitY * 0.99f, 0.66f, 0.66f, 11, Hall.Mirror(40, 50, 52));
                        // Coal, heaped, and bar stock on two bearers.
                        for (int i = 0; i < 22; i++)
                        {
                            float fx = random.Range(-1f, 1f), fz = random.Range(-1f, 1f);
                            float high = (1f - MathF.Max(MathF.Abs(fx), MathF.Abs(fz))) * 0.5f;
                            float size = random.Range(0.1f, 0.19f);
                            Ball(b, new Vector3(-1.9f + fx * 0.8f, Ground + high + size * 0.5f, -0.6f + fz * 0.7f), new Vector3(size, size * 0.8f, size), Hall.Flat(26 + random.Int(10), 25 + random.Int(8), 27));
                        }
                        foreach (float x in new[] { 0.6f, 2f }) b.Box(new Vector3(x, Ground + 0.12f, -1.4f), new Vector3(0.09f, 0.12f, 0.6f), dark);
                        for (int i = 0; i < 6; i++) b.Box(new Vector3(1.3f + random.Range(-0.1f, 0.1f), Ground + 0.29f + (i / 3) * 0.07f, -1.75f + (i % 3) * 0.3f), new Vector3(1.35f, 0.03f, 0.045f), Hall.Metal(96 + i * 4, 98 + i * 4, 104 + i * 4));
                        // An iron tyre leaned on the stump, waiting for its wheel.
                        Vector3 hub = new Vector3(-0.2f, 1.02f, 0.72f);
                        for (int i = 0; i < 12; i++)
                        {
                            float t0 = MathF.Tau * i / 12f, t1 = MathF.Tau * (i + 1) / 12f;
                            b.Beam(hub + new Vector3(MathF.Cos(t0), MathF.Sin(t0), -0.18f * MathF.Sin(t0)) * 0.98f, hub + new Vector3(MathF.Cos(t1), MathF.Sin(t1), -0.18f * MathF.Sin(t1)) * 0.98f, 0.035f, Hall.Metal(84, 80, 78));
                        }
                        break;
                    }
                case YardTrade.Tanning:
                    {
                        // Two frames leaned back on props, a hide laced out in each, and the vat they came out of.
                        VertexMaterial pole = Oak(random, 0.85f);
                        for (int f = 0; f < 2; f++)
                        {
                            float x = -1.4f + f * 2.9f;
                            Vector3 hide = new Vector3(0.62f, 0.48f, 0.34f) * random.Range(0.8f, 1.1f);
                            Hall.At(b, new Vector3(x, 0f, f * 0.3f), (f - 0.5f) * 0.3f, () =>
                            {
                                Vector3 P(float u, float v) => new Vector3(u, Ground + 0.2f + v * 0.94f, -v * 0.34f);
                                foreach (float s in new[] { -1.15f, 1.15f }) b.Beam(P(s, 0f), P(s, 3.1f), 0.05f, pole);
                                foreach (float v in new[] { 0.15f, 2.95f }) b.Beam(P(-1.25f, v), P(1.25f, v), 0.045f, pole);
                                b.Beam(P(0f, 2.9f) + new Vector3(0f, 0f, -0.05f), new Vector3(0f, Ground, -1.9f), 0.045f, pole);
                                // The hide: a ragged oval with its legs' corners drawn out, and its lacing out to the frame.
                                Vector3 middle = P(0f, 1.55f) + Vector3.UnitZ * 0.03f;
                                const int N = 12;
                                var rim = new Vector3[N];
                                for (int i = 0; i < N; i++)
                                {
                                    float a = MathF.Tau * i / N;
                                    float r = (i % 3 == 1 ? 1.12f : 0.86f) * random.Range(0.92f, 1.06f);
                                    rim[i] = P(MathF.Cos(a) * 0.92f * r, 1.55f + MathF.Sin(a) * 1.18f * r) + Vector3.UnitZ * 0.03f;
                                }
                                VertexMaterial skin = new VertexMaterial(MaterialCatalog.Leather.Index, new Colour(hide / new Vector3(0.44f, 0.3f, 0.2f)));
                                for (int i = 0; i < N; i++)
                                {
                                    Hall.Tri(b, middle, rim[i], rim[(i + 1) % N], Vector3.UnitZ, skin);
                                    Hall.Tri(b, middle, rim[i], rim[(i + 1) % N], -Vector3.UnitZ, skin);
                                    Vector3 lace = rim[i] + (rim[i] - middle).Normalized() * 0.28f;
                                    b.Beam(rim[i], lace, 0.008f, PlazaStone.Cloth(new Vector3(0.7f, 0.64f, 0.5f)));
                                }
                            });
                        }
                        // The vat, its liquor the brown of oak bark, and a paddle in it.
                        Vector3 vat = new Vector3(0.1f, Ground - 0.05f, 1.6f);
                        b.Tube(vat, vat + Vector3.UnitY * 0.95f, 0.72f, 0.78f, 12, oak, capped: false);
                        b.Tube(vat + Vector3.UnitY * 0.5f, vat + Vector3.UnitY * 0.58f, 0.79f, 0.79f, 12, Hall.Iron(96), capped: false);
                        b.Tube(vat + Vector3.UnitY * 0.82f, vat + Vector3.UnitY * 0.85f, 0.74f, 0.74f, 12, PlazaStone.Glaze(new Vector3(0.2f, 0.11f, 0.05f)));
                        b.Beam(vat + new Vector3(0.2f, 0.7f, 0f), vat + new Vector3(0.9f, 2.2f, 0.3f), 0.035f, pole);
                        break;
                    }
                case YardTrade.Dyeing:
                case YardTrade.Drying:
                    {
                        // Three posts, two lines between them, and what's hung on them: each length its own colour, hanging in folds.
                        bool dyed = trade == YardTrade.Dyeing;
                        VertexMaterial pole = Oak(random, 0.9f);
                        const float Span = 3.1f, High = 3.9f;
                        foreach (float x in new[] { -Span, 0f, Span })
                        {
                            b.Tube(new Vector3(x, -0.3f, 0f), new Vector3(x, High + 0.25f, 0f), 0.085f, 0.065f, 6, pole);
                            b.Box(new Vector3(x, High, 0f), new Vector3(0.045f, 0.045f, 0.55f), pole);
                        }
                        int next = random.Int(Dyes.Length);
                        float Line(float x) => High - 0.16f * MathF.Sin(MathF.PI * ((x + Span) % Span) / Span);
                        foreach (float z in new[] { -0.5f, 0.5f })
                        {
                            for (float x = -Span; x < Span - 0.01f; x += 0.62f) b.Beam(new Vector3(x, Line(x), z), new Vector3(x + 0.62f, Line(x + 0.62f), z), 0.012f, PlazaStone.Cloth(new Vector3(0.6f, 0.54f, 0.42f)));
                            float at = -Span + random.Range(0.25f, 0.5f);
                            while (at < Span - 0.9f)
                            {
                                float wide = random.Range(0.75f, 1.25f), drop = random.Range(1.5f, 2.7f);
                                if (MathF.Abs(at + wide * 0.5f) < 0.2f) { at += 0.3f; continue; }
                                Vector3 colour = dyed ? Dyes[next++ % Dyes.Length] * random.Range(0.85f, 1.05f) : (random.Chance(0.75f) ? Cream * random.Range(0.94f, 1.04f) : Dyes[next++ % Dyes.Length] * 0.9f);
                                VertexMaterial cloth = PlazaStone.Cloth(colour);
                                const int Folds = 4;
                                float sway = random.Range(-0.12f, 0.12f);
                                int first = b.VertexCount;
                                for (int i = 0; i < Folds; i++)
                                {
                                    float x0 = at + wide * i / Folds, x1 = at + wide * (i + 1) / Folds;
                                    float w0 = 0.06f * MathF.Sin(i * 2.1f + at), w1 = 0.06f * MathF.Sin((i + 1) * 2.1f + at);
                                    Vector3 t0 = new Vector3(x0, Line(x0), z), t1 = new Vector3(x1, Line(x1), z);
                                    Vector3 f0 = new Vector3(x0, Line(x0) - drop, z + w0 + sway), f1 = new Vector3(x1, Line(x1) - drop, z + w1 + sway);
                                    // In two halves down its length, so that it hangs in a curve when the wind takes it.
                                    Vector3 m0 = (t0 + f0) * 0.5f, m1 = (t1 + f1) * 0.5f;
                                    b.Quad(m0, m1, t1, t0, cloth);
                                    b.Quad(m1, m0, t0, t1, cloth);
                                    b.Quad(f0, f1, m1, m0, cloth);
                                    b.Quad(f1, f0, m0, m1, cloth);
                                }
                                // Each length swings from the line by itself: wet cloth is heavy, and what's dry lifts.
                                float middle = at + wide * 0.5f;
                                b.Hang(first, new Vector3(middle, Line(middle), z), new Vector3(middle, Line(middle) - drop, z + sway), dyed ? 0.3f : 0.5f, dyed ? 0.6f : 1.4f, 0.6f);
                                // What's dripped off it, if it's just out of the dye.
                                if (dyed && random.Chance(0.6f)) b.Tube(new Vector3(at + wide * 0.5f, Ground + 0.004f, z + sway), new Vector3(at + wide * 0.5f, Ground + 0.012f, z + sway), random.Range(0.25f, 0.45f), 0.2f, 9, PlazaStone.Glaze(colour * 0.6f));
                                at += wide + random.Range(0.12f, 0.4f);
                            }
                        }
                        Vector3 tubAt = new Vector3(Span + 1.3f, Ground - 0.05f, 0.2f);
                        b.Tube(tubAt, tubAt + Vector3.UnitY * 0.9f, 0.6f, 0.68f, 11, oak, capped: false);
                        b.Tube(tubAt + Vector3.UnitY * 0.78f, tubAt + Vector3.UnitY * 0.81f, 0.64f, 0.64f, 11, dyed ? PlazaStone.Glaze(Dyes[next % Dyes.Length] * 0.55f) : Hall.Mirror(60, 72, 76));
                        if (!dyed) Basket(b, new Vector3(-Span - 1f, Ground, 0.3f), 0.45f, random);
                        break;
                    }
                case YardTrade.Brewing:
                    {
                        // Three casks on a stillage, a table of boards on two more with mugs on it, and benches either side.
                        for (int i = 0; i < 3; i++)
                        {
                            Vector3 at = new Vector3(-2.6f, Ground + 0.75f, -0.9f + i * 0.95f);
                            b.Tube(at - Vector3.UnitX * 0.62f, at, 0.36f, 0.45f, 10, oak, capped: true);
                            b.Tube(at, at + Vector3.UnitX * 0.62f, 0.45f, 0.36f, 10, oak, capped: true);
                            foreach (float x in new[] { -0.4f, 0.4f }) b.Tube(at + Vector3.UnitX * (x - 0.03f), at + Vector3.UnitX * (x + 0.03f), 0.43f, 0.43f, 10, Hall.Iron(96), capped: false);
                        }
                        foreach (float z in new[] { -1.2f, 1.3f }) b.Box(new Vector3(-2.6f, Ground + 0.16f, z * 0.9f + 0.05f), new Vector3(0.7f, 0.16f, 0.09f), dark);
                        b.Box(new Vector3(0.6f, 1.85f, 0f), new Vector3(1.5f, 0.05f, 0.5f), oak);
                        foreach (float x in new[] { -0.5f, 1.7f })
                        {
                            Vector3 cask = new Vector3(x, Ground - 0.05f, 0f);
                            b.Tube(cask, cask + Vector3.UnitY * 0.9f, 0.4f, 0.47f, 10, Oak(random, 0.9f), capped: false);
                            b.Tube(cask + Vector3.UnitY * 0.9f, cask + Vector3.UnitY * 1.75f, 0.47f, 0.4f, 10, Oak(random, 0.9f), capped: false);
                        }
                        foreach (float z in new[] { -1.15f, 1.15f })
                        {
                            b.Box(new Vector3(0.6f, 1.15f, z), new Vector3(1.4f, 0.045f, 0.2f), Oak(random, 1.05f));
                            foreach (float x in new[] { -0.5f, 1.7f }) b.Box(new Vector3(x, 0.55f, z), new Vector3(0.06f, 0.6f, 0.17f), dark);
                        }
                        for (int i = 0; i < 4; i++)
                        {
                            Vector3 mug = new Vector3(-0.4f + i * 0.65f, 1.9f, random.Range(-0.3f, 0.3f));
                            b.Tube(mug, mug + Vector3.UnitY * 0.22f, 0.075f, 0.085f, 7, i % 2 == 0 ? Hall.Wood(120, 88, 58) : Hall.Metal(132, 134, 138));
                        }
                        break;
                    }
                case YardTrade.Thatching:
                    {
                        // Reed in sheaves, each bound twice: a stook of them stood on end with their heads together, more laid in a pile, a bundle of hazel spars and the
                        // bat a roof's dressed with left on top of it.
                        VertexMaterial cord = PlazaStone.Cloth(new Vector3(0.5f, 0.42f, 0.3f));
                        void Sheaf(Vector3 butt, Vector3 head, float r)
                        {
                            VertexMaterial reed = new VertexMaterial(MaterialCatalog.Thatch.Index, new Colour(new Vector3(1f, 0.9f, 0.62f) * random.Range(0.86f, 1.06f)));
                            Vector3 waist = Vector3.Lerp(butt, head, 0.42f), along = (head - butt).Normalized();
                            b.Tube(butt, waist, r * 1.12f, r * 0.8f, 7, reed);
                            b.Tube(waist, head, r * 0.8f, r * 1.25f, 7, reed);
                            foreach (float t in new[] { 0.3f, 0.56f })
                            {
                                Vector3 at = Vector3.Lerp(butt, head, t);
                                b.Tube(at - along * 0.03f, at + along * 0.03f, r * 0.9f, r * 0.9f, 7, cord, capped: false);
                            }
                        }
                        Vector3 top = new Vector3(-1.2f, 2.5f, 0f);
                        for (int i = 0; i < 6; i++)
                        {
                            float a = MathF.Tau * i / 6f + 0.3f;
                            Vector3 foot = new Vector3(top.X + MathF.Cos(a) * 0.85f, Ground - 0.05f, MathF.Sin(a) * 0.85f);
                            Sheaf(foot, top + new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * 0.16f, random.Range(0.21f, 0.26f));
                        }
                        for (int i = 0; i < 5; i++)
                        {
                            int row = i < 3 ? 0 : 1;
                            float z = row == 0 ? -0.5f + i * 0.5f : -0.25f + (i - 3) * 0.5f;
                            Vector3 butt = new Vector3(0.5f + random.Range(-0.08f, 0.08f), Ground + 0.24f + row * 0.4f, z + 0.6f);
                            Sheaf(butt, butt + new Vector3(2.3f, 0f, random.Range(-0.08f, 0.08f)), 0.23f);
                        }
                        // Spars, split hazel, tied in a faggot; and the leggett.
                        VertexMaterial hazel = Hall.Wood(176, 150, 108);
                        for (int i = 0; i < 9; i++)
                        {
                            Vector3 off = new Vector3(0f, random.Range(0f, 0.16f), random.Range(-0.1f, 0.1f));
                            b.Beam(new Vector3(0.7f, Ground + 0.06f, -0.9f) + off, new Vector3(2.5f, Ground + 0.06f, -1.05f) + off, 0.02f, hazel);
                        }
                        b.Tube(new Vector3(1.55f, Ground + 0.1f, -0.98f) - Vector3.UnitX * 0.03f, new Vector3(1.55f, Ground + 0.1f, -0.98f) + Vector3.UnitX * 0.03f, 0.17f, 0.17f, 7, cord, capped: false);
                        b.Box(new Vector3(1.5f, Ground + 0.94f, 0.75f), new Vector3(0.24f, 0.03f, 0.17f), oak);
                        b.Beam(new Vector3(1.7f, Ground + 0.96f, 0.75f), new Vector3(2.35f, Ground + 1.02f, 0.7f), 0.03f, oak);
                        break;
                    }
                default:
                    // Just come: a handcart still loaded, bundles roped up, a chest, a bedroll and a child's wooden horse.
                    Hall.At(b, new Vector3(-0.4f, 0f, 0f), -0.25f, () =>
                    {
                        b.Box(new Vector3(0f, 1.15f, 0f), new Vector3(0.75f, 0.05f, 1.15f), oak);
                        foreach (float s in new[] { -1f, 1f })
                        {
                            b.Box(new Vector3(s * 0.75f, 1.42f, 0f), new Vector3(0.035f, 0.22f, 1.15f), oak);
                            b.Tube(new Vector3(s * 0.84f, 0.75f, 0.1f), new Vector3(s * 0.94f, 0.75f, 0.1f), 0.75f, 0.75f, 12, dark);
                            b.Beam(new Vector3(s * 0.6f, 1.15f, 1.1f), new Vector3(s * 0.6f, 0.9f, 2.5f), 0.04f, dark);
                        }
                        Sack(b, new Vector3(-0.3f, 1.2f, -0.5f), 0.42f, random);
                        Sack(b, new Vector3(0.3f, 1.2f, -0.3f), 0.38f, random);
                        Crate(b, new Vector3(0f, 1.2f, 0.55f), 0.38f, 0.2f, Oak(random, 0.9f));
                        Ball(b, new Vector3(0f, 2.15f, 0.5f), new Vector3(0.5f, 0.2f, 0.26f), PlazaStone.Cloth(Dyes[random.Int(Dyes.Length)] * 0.9f));
                    });
                    Crate(b, new Vector3(1.6f, Ground, -0.4f), 0.5f, 0.5f, Oak(random, 0.78f));
                    b.Box(new Vector3(1.6f, Ground + 1.04f, -0.4f), new Vector3(0.52f, 0.04f, 0.4f), Hall.Metal(140, 128, 96));
                    for (int i = 0; i < 3; i++) Sack(b, new Vector3(1.3f + i * 0.5f, Ground, 0.7f + (i % 2) * 0.3f), random.Range(0.3f, 0.4f), random);
                    Vector3 horse = new Vector3(-1.9f, Ground, 0.9f);
                    b.Box(horse + new Vector3(0f, 0.42f, 0f), new Vector3(0.3f, 0.12f, 0.1f), Hall.Wood(190, 150, 100));
                    b.Box(horse + new Vector3(0.28f, 0.68f, 0f), new Vector3(0.1f, 0.2f, 0.08f), Hall.Wood(190, 150, 100));
                    foreach (float x in new[] { -0.2f, 0.2f }) b.Box(horse + new Vector3(x, 0.16f, 0f), new Vector3(0.04f, 0.16f, 0.12f), Hall.Wood(170, 132, 90));
                    break;
            }
        }

        /// <summary>How far a household's work reaches from its middle.</summary>
        public const float YardworkReach = 2.8f;

        // ---------------------------------------------------------------- Small things

        // A shallow box, tipped up at the back.
        private static void Tray(MeshBuilder b, Vector3 at, float halfX, float halfZ, float tall, WorldRandom random)
        {
            VertexMaterial wood = Oak(random, 1.05f);
            b.Box(at + new Vector3(0f, 0.03f, 0f), new Vector3(halfX, 0.03f, halfZ), wood);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(at + new Vector3(s * halfX, tall * 0.5f, 0f), new Vector3(0.025f, tall * 0.5f, halfZ), wood);
                b.Box(at + new Vector3(0f, tall * 0.5f, s * halfZ), new Vector3(halfX, tall * 0.5f, 0.025f), wood);
            }
        }

        // A heap of round things, or of folded cloth.
        private static void Heap(MeshBuilder b, Vector3 at, float halfX, float halfZ, float size, Vector3 colour, WorldRandom random, int count, bool cloth = false)
        {
            for (int i = 0; i < count; i++)
            {
                float fx = random.Range(-1f, 1f), fz = random.Range(-1f, 1f);
                float high = (1f - MathF.Max(MathF.Abs(fx), MathF.Abs(fz))) * size * 1.6f;
                Vector3 p = at + new Vector3(fx * (halfX - size), high + size * 0.6f, fz * (halfZ - size));
                Vector3 shade = colour * random.Range(0.82f, 1.12f);
                if (cloth) b.Box(p, new Vector3(size * 1.3f, size * 0.4f, size), PlazaStone.Cloth(shade));
                else Ball(b, p, new Vector3(size), PlazaStone.Glaze(shade));
            }
        }

        private static void Crate(MeshBuilder b, Vector3 foot, float half, float yaw, VertexMaterial wood)
        {
            Hall.At(b, foot, yaw, () =>
            {
                b.Box(new Vector3(0f, half, 0f), new Vector3(half, half, half), wood);
                VertexMaterial batten = Shaded(wood, 0.78f);
                foreach (float s in new[] { -1f, 1f })
                {
                    b.Box(new Vector3(0f, half, s * (half + 0.012f)), new Vector3(half, 0.05f, 0.012f), batten);
                    b.Box(new Vector3(s * (half + 0.012f), half, 0f), new Vector3(0.012f, 0.05f, half), batten);
                    foreach (float e in new[] { -1f, 1f }) b.Box(new Vector3(e * (half - 0.05f), half, s * (half + 0.012f)), new Vector3(0.05f, half, 0.012f), batten);
                }
            });
        }

        private static void Basket(MeshBuilder b, Vector3 foot, float radius, WorldRandom random)
        {
            VertexMaterial wicker = new VertexMaterial(MaterialCatalog.Thatch.Index, new Colour(new Vector3(0.86f, 0.7f, 0.48f) * random.Range(0.85f, 1.05f)));
            b.Tube(foot, foot + Vector3.UnitY * radius * 0.85f, radius * 0.72f, radius, 10, wicker, capped: false);
            b.Tube(foot + Vector3.UnitY * radius * 0.85f, foot + Vector3.UnitY * radius * 0.95f, radius * 1.05f, radius * 1.05f, 10, wicker, capped: false);
            b.Tube(foot + Vector3.UnitY * 0.02f, foot + Vector3.UnitY * 0.04f, radius * 0.72f, radius * 0.72f, 10, wicker);
        }

        private static void Sack(MeshBuilder b, Vector3 foot, float radius, WorldRandom random)
        {
            VertexMaterial hessian = PlazaStone.Cloth(new Vector3(0.62f, 0.54f, 0.4f) * random.Range(0.88f, 1.08f));
            Ball(b, foot + Vector3.UnitY * radius * 1.05f, new Vector3(radius, radius * 1.15f, radius * 0.9f), hessian);
            b.Tube(foot + Vector3.UnitY * radius * 2.05f, foot + Vector3.UnitY * radius * 2.4f, radius * 0.25f, radius * 0.4f, 6, hessian);
        }

        private static void Creel(MeshBuilder b, Vector3 foot, WorldRandom random)
        {
            VertexMaterial withy = new VertexMaterial(MaterialCatalog.Thatch.Index, new Colour(new Vector3(0.7f, 0.56f, 0.4f) * random.Range(0.8f, 1.05f)));
            b.Tube(foot + new Vector3(-0.3f, 0.3f, 0f), foot + new Vector3(0.3f, 0.3f, 0f), 0.3f, 0.3f, 8, withy);
            b.Tube(foot + new Vector3(0.3f, 0.3f, 0f), foot + new Vector3(0.36f, 0.3f, 0f), 0.3f, 0.12f, 8, Shaded(withy, 0.6f));
        }

        // A pot turned on a wheel: `form` 0 a jar, 1 a jug with a neck, 2 a bowl, 3 a flowerpot.
        private static void Jar(MeshBuilder b, Vector3 foot, float height, int form, VertexMaterial clay)
        {
            (float Y, float R)[] profile = form switch
            {
                1 => new[] { (0f, 0.3f), (0.3f, 0.48f), (0.6f, 0.44f), (0.78f, 0.2f), (0.92f, 0.18f), (1f, 0.25f) },
                2 => new[] { (0f, 0.4f), (0.4f, 0.8f), (0.9f, 1f), (1f, 1f) },
                3 => new[] { (0f, 0.36f), (0.82f, 0.5f), (0.84f, 0.58f), (1f, 0.58f) },
                _ => new[] { (0f, 0.26f), (0.28f, 0.46f), (0.62f, 0.46f), (0.86f, 0.28f), (1f, 0.32f) },
            };
            for (int i = 1; i < profile.Length; i++)
            {
                b.Tube(foot + Vector3.UnitY * profile[i - 1].Y * height, foot + Vector3.UnitY * profile[i].Y * height, profile[i - 1].R * height, profile[i].R * height, 9, clay, capped: i == 1 || i == profile.Length - 1);
            }
        }

        /// <summary>A leaf's green, one of a few, for anything grown: darker than turf, as a rose's or a vine's is.</summary>
        public static VertexMaterial Leaf(WorldRandom random, float shade = 1f)
        {
            Vector3 green = Vector3.Lerp(new Vector3(0.1f, 0.21f, 0.06f), new Vector3(0.17f, 0.31f, 0.085f), random.Float()) * shade;
            return new VertexMaterial(MaterialCatalog.Moss.Index, new Colour(green));
        }

        /// <summary>A clump of leaves about a point: a few rounded masses in greens a little apart, as a rose or a vine is seen from far off, <paramref name="flat"/> times as deep as wide.</summary>
        public static void Leaves(MeshBuilder b, Vector3 at, float size, WorldRandom random, float flat = 1f, float shade = 1f)
        {
            int masses = 2 + random.Int(3);
            for (int i = 0; i < masses; i++)
            {
                Vector3 off = new Vector3(random.Range(-1f, 1f), random.Range(-0.7f, 0.7f), random.Range(-1f, 1f) * flat) * size * 0.55f;
                float r = size * random.Range(0.5f, 0.8f);
                Ball(b, at + off, new Vector3(r, r * 0.78f, r * flat), Leaf(random, shade));
            }
        }

        public static void Ball(MeshBuilder b, Vector3 centre, Vector3 radius, VertexMaterial material)
        {
            b.Surface(7, 4, (u, v) =>
            {
                float theta = u * MathF.Tau, phi = v * MathF.PI;
                return centre + new Vector3(MathF.Cos(theta) * MathF.Sin(phi) * radius.X, -MathF.Cos(phi) * radius.Y, -MathF.Sin(theta) * MathF.Sin(phi) * radius.Z);
            }, material);
        }

        private static VertexMaterial Shaded(VertexMaterial material, float by)
        {
            Colour c = material.colour;
            return new VertexMaterial(material.materialTextureIndex, new Colour(c.Red * by, c.Green * by, c.Blue * by, c.Intensity, c.Alpha));
        }
    }
}
