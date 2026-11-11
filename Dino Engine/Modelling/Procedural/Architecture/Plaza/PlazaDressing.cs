using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Plaza
{
    /// <summary>
    /// What a square is lived in with: a pergola of stone columns under oak beams with a sail over half of it, tables set for a meal, terracotta pots, masts and the bunting strung
    /// between them. A person stands about 4.6 m tall here, so a seat is 1.2 m up and a table 1.9 m. Colours are albedos under a sun of about 13: cloth and glaze stay deep enough
    /// to read in sunlight, and none of it is black iron, oxblood or brass (LORE.md). Headless.
    /// </summary>
    internal static class PlazaDressing
    {
        private const float Foot = -0.5f;

        // The colours of the Haven's cloth: blossom, cream, gold, sky, sage and lilac.
        private static readonly Vector3[] Festive =
        {
            new Vector3(0.84f, 0.8f, 0.7f),
            new Vector3(0.78f, 0.36f, 0.48f),
            new Vector3(0.8f, 0.6f, 0.16f),
            new Vector3(0.3f, 0.5f, 0.72f),
            new Vector3(0.42f, 0.56f, 0.32f),
            new Vector3(0.58f, 0.44f, 0.7f),
        };

        // ---------------------------------------------------------------- Pergola

        /// <summary>
        /// Stone columns on plinths, <paramref name="bays"/> bays along x and two rows along z, carrying an oak beam on each row, joists across them, a striped sail over
        /// <paramref name="shade"/> of it with a scalloped valance, and a lantern hanging in each bay under the sail.
        /// </summary>
        public static void Pergola(MeshBuilder b, float length, float depth, int bays, float height, float shade, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("pergola");
            float halfL = length * 0.5f, halfD = depth * 0.5f;
            float ground = PlazaStone.Ground;

            for (int k = 0; k <= bays; k++)
            {
                float x = -halfL + length * k / bays;
                foreach (float z in new[] { -halfD, halfD })
                {
                    Column(b, new Vector3(x, 0f, z), height, random.Fork(k * 2 + (z > 0f ? 1 : 0)));
                }
            }

            // Beams on the columns, overhanging the ends, their ends cut back underneath.
            float beamTop = height + 0.44f;
            foreach (float z in new[] { -halfD, halfD })
            {
                VertexMaterial oak = PlazaStone.Timber(random.Fork(z > 0f ? "front beam" : "back beam"), 0.95f);
                Timber(b, new Vector3(-halfL - 0.8f, height, z - 0.16f), new Vector3(halfL + 0.8f, beamTop, z + 0.16f), 0.34f, alongX: true, oak);
            }

            // Joists across the beams.
            int joists = Math.Max(3, (int)MathF.Round((length + 0.9f) / 0.62f));
            float joistTop = beamTop + 0.32f;
            var joistXs = new List<float>();
            for (int j = 0; j <= joists; j++)
            {
                float x = -halfL - 0.45f + (length + 0.9f) * j / joists;
                joistXs.Add(x);
                Timber(b, new Vector3(x - 0.08f, beamTop, -halfD - 0.62f), new Vector3(x + 0.08f, joistTop, halfD + 0.62f), 0.22f, alongX: false, PlazaStone.Timber(random.Fork(300 + j)));
            }

            // The sail, over the middle of it, resting on the joists and sagging a little between them, in stripes.
            float shaded = length * Math.Clamp(shade, 0f, 1f);
            if (shaded > 0.5f)
            {
                float x0 = -shaded * 0.5f, x1 = shaded * 0.5f;
                float z0 = -halfD - 0.48f, z1 = halfD + 0.48f;
                float sailY = joistTop + 0.015f;
                Vector3 stripe = Festive[1 + (int)(random.Float() * (Festive.Length - 1))];
                Vector3 cream = Festive[0];
                int columns = Math.Max(2, (int)MathF.Round(shaded / 0.31f));
                const int Rows = 6;
                for (int c = 0; c < columns; c++)
                {
                    float u0 = x0 + shaded * c / columns, u1 = x0 + shaded * (c + 1) / columns;
                    VertexMaterial cloth = PlazaStone.Cloth((c / 2) % 2 == 0 ? stripe : cream);
                    for (int r = 0; r < Rows; r++)
                    {
                        float v0 = z0 + (z1 - z0) * r / Rows, v1 = z0 + (z1 - z0) * (r + 1) / Rows;
                        Vector3 P(float u, float v) => new Vector3(u, sailY - Sag(u, joistXs) - 0.05f * MathF.Sin(MathF.PI * (v - z0) / (z1 - z0)), v);
                        Vector3 a = P(u0, v1), bb = P(u1, v1), cc = P(u1, v0), d = P(u0, v0);
                        b.Quad(a, bb, cc, d, cloth);
                        b.Quad(d, cc, bb, a, cloth);
                    }
                    // The valance along each long edge, its hem cut in scallops.
                    foreach (float edge in new[] { z0, z1 })
                    {
                        float top = sailY - Sag((u0 + u1) * 0.5f, joistXs);
                        float drop = 0.38f;
                        Vector3 p0 = new(u0, top, edge), p1 = new(u1, top, edge), p2 = new(u1, top - drop, edge), p3 = new(u0, top - drop, edge);
                        b.Quad(p3, p2, p1, p0, cloth);
                        b.Quad(p0, p1, p2, p3, cloth);
                        Vector3 tip = new((u0 + u1) * 0.5f, top - drop - 0.14f, edge);
                        b.Triangle(p3, p2, tip, cloth);
                        b.Triangle(p2, p3, tip, cloth);
                    }
                }
            }

            // Its lanterns hang in its bays as lamps of their own (PergolaLamps), or the pergola would shut their light in.
        }

        /// <summary>Where a pergola's lanterns hang, in its own frame, in every other bay, and how long the cord up to its joists is from each.</summary>
        public static IEnumerable<(Vector3 At, float Cord)> PergolaLamps(float length, int bays, float height)
        {
            float hang = height + 0.44f - 0.05f;
            for (int k = 0; k < bays; k++)
            {
                if (bays > 2 && k % 2 == 1) continue;
                yield return (new Vector3(-length * 0.5f + length * (k + 0.5f) / bays, hang - 1f, 0f), 1.3f);
            }
        }

        /// <summary>A lantern hung on a cord <paramref name="cord"/> long above it, its light at the origin.</summary>
        public static void HungLamp(MeshBuilder b, float cord, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("lamp");
            b.Tube(new Vector3(0f, 0.44f, 0f), new Vector3(0f, cord, 0f), 0.024f, 0.024f, 4, PlazaStone.DarkTimber(random));
            HangingLantern(b, Vector3.Zero, random, lights);
        }

        // How far the sail sags at x, between the joists it rests on.
        private static float Sag(float x, List<float> joists)
        {
            for (int i = 1; i < joists.Count; i++)
            {
                if (x > joists[i]) continue;
                float t = Math.Clamp((x - joists[i - 1]) / MathF.Max(joists[i] - joists[i - 1], 1e-3f), 0f, 1f);
                return 0.07f * MathF.Sin(MathF.PI * t);
            }
            return 0f;
        }

        // A column: a plinth, a moulded base, a shaft that narrows as it rises, and a capital under a square abacus that the beam sits on.
        private static void Column(MeshBuilder b, Vector3 foot, float height, WorldRandom random)
        {
            float ground = PlazaStone.Ground;
            Func<Vector3, VertexMaterial> stone = PlazaStone.DressedFacing(random, 1f, 0.2f);
            VertexMaterial face = stone(Vector3.UnitX);
            PlazaStone.Block(b, foot + new Vector3(0f, (Foot + ground + 0.36f) * 0.5f, 0f), new Vector3(0.4f, (ground + 0.36f - Foot) * 0.5f, 0.4f), 0f, PlazaStone.BigChamfer, stone);
            float y = ground + 0.36f;
            b.Tube(foot + new Vector3(0f, y, 0f), foot + new Vector3(0f, y + 0.08f, 0f), 0.36f, 0.35f, 14, face);
            b.Tube(foot + new Vector3(0f, y + 0.08f, 0f), foot + new Vector3(0f, y + 0.16f, 0f), 0.35f, 0.29f, 14, face, capped: false);
            float capital = height - 0.36f;
            // The shaft in drums, each a little narrower than the one below.
            int drums = Math.Max(2, (int)MathF.Round((capital - y - 0.16f) / 1.1f));
            for (int d = 0; d < drums; d++)
            {
                float a = y + 0.16f + (capital - y - 0.16f) * d / drums, c = y + 0.16f + (capital - y - 0.16f) * (d + 1) / drums;
                float ra = MathHelper.Lerp(0.28f, 0.24f, (float)d / drums), rc = MathHelper.Lerp(0.28f, 0.24f, (float)(d + 1) / drums);
                b.Tube(foot + new Vector3(0f, a + 0.004f, 0f), foot + new Vector3(0f, c - 0.004f, 0f), ra, rc, 14, PlazaStone.Dressed(random.Fork(d), a, 0.03f), capped: false);
            }
            b.Tube(foot + new Vector3(0f, capital - 0.01f, 0f), foot + new Vector3(0f, capital + 0.06f, 0f), 0.26f, 0.27f, 14, face, capped: false);
            b.Tube(foot + new Vector3(0f, capital + 0.06f, 0f), foot + new Vector3(0f, capital + 0.2f, 0f), 0.27f, 0.4f, 14, face, capped: false);
            PlazaStone.Block(b, foot + new Vector3(0f, capital + 0.28f, 0f), new Vector3(0.44f, 0.08f, 0.44f), 0f, 0.03f, stone);
        }

        // A length of oak between two corners, its ends cut back underneath by `undercut` along its length, as a beam's ends are shaped.
        private static void Timber(MeshBuilder b, Vector3 min, Vector3 max, float undercut, bool alongX, VertexMaterial oak)
        {
            Vector3[] c = PlazaPieces.Corners(min, max);
            // Bottom corners are 0, 1, 4, 5: pull them in along the timber's length.
            if (alongX)
            {
                c[0].X += undercut; c[4].X += undercut;
                c[1].X -= undercut; c[5].X -= undercut;
            }
            else
            {
                c[0].Z -= undercut; c[1].Z -= undercut;
                c[4].Z += undercut; c[5].Z += undercut;
            }
            PlazaWalls.Lay(b, c, 0.025f, _ => oak);
        }

        private static void HangingLantern(MeshBuilder b, Vector3 at, WorldRandom random, List<AssetLight> lights)
        {
            VertexMaterial wood = PlazaStone.DarkTimber(random);
            PlazaStone.Block(b, at + new Vector3(0f, 0.3f, 0f), new Vector3(0.24f, 0.035f, 0.24f), 0f, 0.015f, wood);
            b.Tube(at + new Vector3(0f, 0.335f, 0f), at + new Vector3(0f, 0.46f, 0f), 0.24f, 0.05f, 4, wood);
            b.Box(at + new Vector3(0f, 0.06f, 0f), new Vector3(0.17f, 0.23f, 0.17f), PlazaStone.Glow(1f, 0.6f, 0.24f, 2.4f));
            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f })
                {
                    b.Box(at + new Vector3(x * 0.19f, 0.06f, z * 0.19f), new Vector3(0.025f, 0.25f, 0.025f), wood);
                }
            }
            PlazaStone.Block(b, at + new Vector3(0f, -0.2f, 0f), new Vector3(0.21f, 0.03f, 0.21f), 0f, 0.012f, wood);
            lights.Add(PlazaStone.HungLantern(at + new Vector3(0f, 0.06f, 0f)));
        }

        // ---------------------------------------------------------------- Table

        /// <summary>A trestle table <paramref name="length"/> long, a bench along each side, and a meal laid on it: a plate and a cup at every place, and bread, fruit and jugs down the middle.</summary>
        public static void Table(MeshBuilder b, float length, uint seed)
        {
            var random = new WorldRandom(seed).Fork("table");
            float ground = PlazaStone.Ground;
            float half = length * 0.5f;
            const float Top = 1.9f, Width = 1.25f, Seat = 1.17f, BenchOut = 1.32f;
            float top = ground + Top;
            VertexMaterial oak = PlazaStone.Timber(random, 1.04f);
            VertexMaterial dark = PlazaStone.DarkTimber(random);

            // Three boards, a hair apart.
            for (int i = 0; i < 3; i++)
            {
                float z0 = -Width * 0.5f + Width * i / 3f + 0.012f, z1 = -Width * 0.5f + Width * (i + 1) / 3f - 0.012f;
                PlazaWalls.Lay(b, PlazaPieces.Corners(new Vector3(-half, top - 0.09f, z1), new Vector3(half, top, z0)), 0.02f, _ => PlazaStone.Timber(random.Fork(i), 1.04f));
            }
            // Trestles: a pair of splayed legs at each end, a foot and a rail across, and a stretcher between them.
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (half - 0.6f);
                foreach (float z in new[] { -1f, 1f })
                {
                    Leg(b, new Vector3(x, ground - 0.05f, z * 0.52f), new Vector3(x, top - 0.1f, z * 0.24f), 0.08f, dark);
                }
                PlazaStone.Block(b, new Vector3(x, ground + 0.07f, 0f), new Vector3(0.09f, 0.07f, 0.62f), 0f, 0.02f, dark);
                PlazaStone.Block(b, new Vector3(x, top - 0.16f, 0f), new Vector3(0.08f, 0.06f, Width * 0.5f - 0.06f), 0f, 0.02f, dark);
            }
            PlazaStone.Block(b, new Vector3(0f, ground + 0.62f, 0f), new Vector3(half - 0.6f, 0.06f, 0.06f), 0f, 0.02f, dark);

            // Benches.
            foreach (float z in new[] { -BenchOut, BenchOut })
            {
                float seat = ground + Seat;
                PlazaWalls.Lay(b, PlazaPieces.Corners(new Vector3(-half + 0.15f, seat - 0.1f, z - 0.27f), new Vector3(half - 0.15f, seat, z + 0.27f)), 0.025f, _ => oak);
                foreach (float s in new[] { -1f, 1f })
                {
                    PlazaStone.Block(b, new Vector3(s * (half - 0.65f), (ground - 0.05f + seat - 0.1f) * 0.5f, z), new Vector3(0.07f, (seat - 0.1f - ground + 0.05f) * 0.5f, 0.24f), 0f, 0.02f, dark);
                }
            }

            // The meal: a place at every 0.9 m of each side.
            int places = Math.Max(1, (int)MathF.Floor((length - 0.4f) / 0.9f));
            foreach (float side in new[] { -1f, 1f })
            {
                for (int p = 0; p < places; p++)
                {
                    WorldRandom place = random.Fork(1000 + p * 2 + (side > 0f ? 1 : 0));
                    if (place.Chance(0.15f)) continue;
                    float x = -half + (length / places) * (p + 0.5f) + place.Range(-0.08f, 0.08f);
                    float z = side * 0.36f;
                    Vector3 plate = new(x, top, z);
                    Disc(b, plate, 0.23f, 0.03f, PlazaStone.Glaze(Plates[(int)(place.Float() * Plates.Length)]));
                    if (place.Chance(0.6f)) Ball(b, plate + new Vector3(place.Range(-0.05f, 0.05f), 0.08f, 0f), new Vector3(0.11f, 0.06f, 0.08f), PlazaStone.Glaze(Foods[(int)(place.Float() * Foods.Length)]));
                    Cup(b, new Vector3(x + side * 0.28f, top, side * 0.5f), place);
                }
            }
            for (float x = -half + 0.55f; x < half - 0.4f; x += random.Range(0.7f, 1.05f))
            {
                WorldRandom dish = random.Fork((int)(x * 100f) + 5000);
                Vector3 at = new(x, top, dish.Range(-0.08f, 0.08f));
                switch (dish.PickWeighted(DishWeights))
                {
                    case 0: Ball(b, at + new Vector3(0f, 0.1f, 0f), new Vector3(0.26f, 0.12f, 0.15f), PlazaStone.Glaze(new Vector3(0.62f, 0.4f, 0.17f) * dish.Range(0.9f, 1.1f))); break;
                    case 1: FruitBowl(b, at, dish); break;
                    case 2: Jug(b, at, dish); break;
                    default: Disc(b, at, 0.21f, 0.15f, PlazaStone.Glaze(new Vector3(0.82f, 0.66f, 0.3f))); break;
                }
            }
        }

        private static readonly float[] DishWeights = { 0.3f, 0.3f, 0.25f, 0.15f };
        private static readonly Vector3[] Plates = { new(0.8f, 0.76f, 0.66f), new(0.36f, 0.46f, 0.62f), new(0.7f, 0.62f, 0.5f) };
        private static readonly Vector3[] Foods = { new(0.62f, 0.4f, 0.16f), new(0.56f, 0.14f, 0.1f), new(0.42f, 0.54f, 0.2f), new(0.78f, 0.6f, 0.3f) };

        private static void Leg(MeshBuilder b, Vector3 foot, Vector3 head, float half, VertexMaterial wood)
        {
            Vector3 up = head - foot;
            Vector3 side = Vector3.Normalize(Vector3.Cross(up, Vector3.UnitX)) * half;
            Vector3 across = Vector3.UnitX * half;
            Vector3[] c =
            {
                foot - across + side, foot + across + side, head + across + side, head - across + side,
                foot - across - side, foot + across - side, head + across - side, head - across - side,
            };
            PlazaWalls.Lay(b, c, 0.02f, _ => wood);
        }

        private static void Disc(MeshBuilder b, Vector3 at, float radius, float height, VertexMaterial material)
        {
            b.Tube(at, at + new Vector3(0f, height, 0f), radius, radius * 1.02f, 12, material);
        }

        private static void Cup(MeshBuilder b, Vector3 at, WorldRandom random)
        {
            VertexMaterial clay = random.Chance(0.5f) ? PlazaStone.Terracotta(random) : PlazaStone.Glaze(new Vector3(0.3f, 0.42f, 0.6f) * random.Range(0.85f, 1.1f));
            b.Tube(at, at + new Vector3(0f, 0.27f, 0f), 0.085f, 0.1f, 8, clay);
            b.Tube(at + new Vector3(0f, 0.255f, 0f), at + new Vector3(0f, 0.262f, 0f), 0.08f, 0.08f, 8, PlazaStone.Glaze(new Vector3(0.24f, 0.12f, 0.06f)));
        }

        private static void Jug(MeshBuilder b, Vector3 at, WorldRandom random)
        {
            VertexMaterial clay = PlazaStone.Terracotta(random);
            b.Tube(at, at + new Vector3(0f, 0.08f, 0f), 0.12f, 0.17f, 10, clay, capped: false);
            b.Tube(at + new Vector3(0f, 0.08f, 0f), at + new Vector3(0f, 0.3f, 0f), 0.17f, 0.15f, 10, clay, capped: false);
            b.Tube(at + new Vector3(0f, 0.3f, 0f), at + new Vector3(0f, 0.46f, 0f), 0.15f, 0.07f, 10, clay, capped: false);
            b.Tube(at + new Vector3(0f, 0.46f, 0f), at + new Vector3(0f, 0.55f, 0f), 0.07f, 0.09f, 10, clay);
        }

        private static void FruitBowl(MeshBuilder b, Vector3 at, WorldRandom random)
        {
            VertexMaterial bowl = PlazaStone.Glaze(new Vector3(0.72f, 0.68f, 0.58f));
            b.Tube(at, at + new Vector3(0f, 0.04f, 0f), 0.11f, 0.12f, 10, bowl);
            b.Tube(at + new Vector3(0f, 0.04f, 0f), at + new Vector3(0f, 0.16f, 0f), 0.13f, 0.27f, 10, bowl, capped: false);
            int fruit = random.Int(4, 7);
            for (int i = 0; i < fruit; i++)
            {
                float angle = i * MathF.Tau / fruit + random.Range(-0.3f, 0.3f);
                float r = i == fruit - 1 ? 0f : 0.13f;
                Vector3 colour = Fruit[(int)(random.Float() * Fruit.Length)];
                Ball(b, at + new Vector3(MathF.Cos(angle) * r, 0.17f + (r == 0f ? 0.07f : 0f), MathF.Sin(angle) * r), new Vector3(0.075f), PlazaStone.Glaze(colour * random.Range(0.85f, 1.1f)));
            }
        }

        private static readonly Vector3[] Fruit = { new(0.62f, 0.1f, 0.07f), new(0.78f, 0.4f, 0.05f), new(0.5f, 0.6f, 0.15f), new(0.36f, 0.18f, 0.42f), new(0.8f, 0.66f, 0.12f) };

        private static void Ball(MeshBuilder b, Vector3 centre, Vector3 radius, VertexMaterial material)
        {
            b.Surface(8, 5, (u, v) =>
            {
                float theta = u * MathF.Tau, phi = v * MathF.PI;
                return centre + new Vector3(MathF.Cos(theta) * MathF.Sin(phi) * radius.X, -MathF.Cos(phi) * radius.Y, -MathF.Sin(theta) * MathF.Sin(phi) * radius.Z);
            }, material);
        }

        // ---------------------------------------------------------------- Pots

        /// <summary>A terracotta pot of a <paramref name="form"/> and <paramref name="height"/>, turned on a wheel, with soil in it at <see cref="PotSoil"/> for whatever's planted there.</summary>
        public static void Pot(MeshBuilder b, int form, float height, uint seed)
        {
            var random = new WorldRandom(seed).Fork("pot");
            VertexMaterial clay = PlazaStone.Terracotta(random);
            VertexMaterial rim = PlazaStone.Tint(clay, new Vector3(1.04f));
            float h = height;
            // A profile of (height, radius) pairs from the foot up, as a potter would turn it.
            (float Y, float R)[] profile = form switch
            {
                1 => new[] { (0f, 0.3f), (0.1f, 0.3f), (0.12f, 0.2f), (0.3f, 0.17f), (0.62f, 0.5f), (0.86f, 0.48f), (0.9f, 0.56f), (1f, 0.56f) },
                2 => new[] { (0f, 0.48f), (0.25f, 0.62f), (0.62f, 0.95f), (0.86f, 1.02f), (1f, 1.02f) },
                3 => new[] { (0f, 0.24f), (0.32f, 0.44f), (0.6f, 0.42f), (0.8f, 0.2f), (0.9f, 0.16f), (1f, 0.21f) },
                _ => new[] { (0f, 0.32f), (0.8f, 0.46f), (0.82f, 0.53f), (1f, 0.53f) },
            };
            for (int i = 1; i < profile.Length; i++)
            {
                (float y0, float r0) = profile[i - 1];
                (float y1, float r1) = profile[i];
                b.Tube(new Vector3(0f, PlazaStone.Ground - 0.04f + y0 * h, 0f), new Vector3(0f, PlazaStone.Ground - 0.04f + y1 * h, 0f), r0 * h, r1 * h, 14, i >= profile.Length - 2 ? rim : clay, capped: i == 1);
            }
            float soil = PotSoil(form, height);
            if (form != 3) b.Tube(new Vector3(0f, soil - 0.05f, 0f), new Vector3(0f, soil, 0f), profile[^1].R * h * 0.94f, profile[^1].R * h * 0.94f, 14, PlazaStone.Soil(0.85f));
        }

        /// <summary>How high above where people stand a pot's soil is, which is where something planted in it stands.</summary>
        public static float PotSoil(int form, float height) => PlazaStone.Ground - 0.04f + height * (form switch { 1 => 0.94f, 2 => 0.9f, 3 => 1f, _ => 0.93f });

        // ---------------------------------------------------------------- Masts and bunting

        /// <summary>
        /// A pole for bunting: a dressed footing, a turned oak pole and a ball on top, with a cleat where each line is made fast, and, if it carries one, a lantern hung from an arm
        /// out towards +z. Its lines meet it at <see cref="MastTie"/> below its top.
        /// </summary>
        public static void Mast(MeshBuilder b, float height, bool lantern, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("mast");
            PlazaStone.Block(b, new Vector3(0f, (Foot + PlazaStone.Ground + 0.55f) * 0.5f, 0f), new Vector3(0.42f, (PlazaStone.Ground + 0.55f - Foot) * 0.5f, 0.42f), 0f, PlazaStone.BigChamfer, PlazaStone.DressedFacing(random, 0.3f, 0.3f));
            VertexMaterial oak = PlazaStone.Timber(random, 0.9f);
            float foot = PlazaStone.Ground + 0.55f;
            b.Tube(new Vector3(0f, foot, 0f), new Vector3(0f, foot + 0.12f, 0f), 0.3f, 0.2f, 10, oak, capped: false);
            b.Tube(new Vector3(0f, foot + 0.12f, 0f), new Vector3(0f, height - 0.2f, 0f), 0.16f, 0.11f, 10, oak, capped: false);
            b.Tube(new Vector3(0f, height - 0.2f, 0f), new Vector3(0f, height - 0.12f, 0f), 0.15f, 0.15f, 10, PlazaStone.DarkTimber(random));
            Ball(b, new Vector3(0f, height + 0.02f, 0f), new Vector3(0.17f), PlazaStone.Glaze(Festive[2] * 0.95f));
            PlazaStone.Block(b, new Vector3(0f, height - MastTie, 0f), new Vector3(0.2f, 0.05f, 0.2f), 0f, 0.015f, PlazaStone.DarkTimber(random));
            if (!lantern) return;
            // An arm with a brace under it, and the lantern on a cord from its end.
            const float Arm = 5.4f, Reach = 1.15f;
            VertexMaterial dark = PlazaStone.DarkTimber(random);
            PlazaWalls.Lay(b, PlazaPieces.Corners(new Vector3(-0.06f, Arm - 0.07f, 0.08f), new Vector3(0.06f, Arm + 0.07f, Reach + 0.1f)), 0.015f, _ => dark);
            b.Tube(new Vector3(0f, Arm - 0.75f, 0.1f), new Vector3(0f, Arm - 0.05f, Reach * 0.62f), 0.04f, 0.04f, 4, dark);
            b.Tube(new Vector3(0f, Arm - 0.07f, Reach), new Vector3(0f, Arm - 0.45f, Reach), 0.018f, 0.018f, 4, dark);
            HangingLantern(b, new Vector3(0f, Arm - 0.75f, Reach), random.Fork("lantern"), lights);
        }

        public const float MastTie = 0.45f;

        /// <summary>
        /// A line of pennants from the origin to (<paramref name="span"/>, <paramref name="drop"/>, 0), hanging <paramref name="sag"/> in the middle: a cord, and a pennant every
        /// stride along it in the Haven's colours, each hanging straight down and turned a little its own way.
        /// </summary>
        public static void Bunting(MeshBuilder b, float span, float drop, float sag, uint seed)
        {
            var random = new WorldRandom(seed).Fork("bunting");
            float Y(float x) { float t = x / MathF.Max(span, 0.01f); return drop * t - 4f * sag * t * (1f - t); }
            VertexMaterial cord = PlazaStone.Cloth(new Vector3(0.62f, 0.56f, 0.44f));
            int segments = Math.Max(4, (int)MathF.Ceiling(span / 0.8f));
            for (int i = 0; i < segments; i++)
            {
                float x0 = span * i / segments, x1 = span * (i + 1) / segments;
                b.Tube(new Vector3(x0, Y(x0), 0f), new Vector3(x1, Y(x1), 0f), 0.028f, 0.028f, 4, cord, capped: false);
            }
            const float Stride = 0.72f;
            int offset = random.Int(0, Festive.Length - 1);
            int flags = Math.Max(1, (int)MathF.Floor((span - 0.6f) / Stride));
            float start = (span - (flags - 1) * Stride) * 0.5f;
            for (int i = 0; i < flags; i++)
            {
                float x = start + i * Stride;
                float y = Y(x);
                float turn = random.Range(-0.25f, 0.25f);
                Vector3 across = new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn)) * 0.22f;
                Vector3 hang = new Vector3(random.Range(-0.03f, 0.03f), -0.6f, random.Range(-0.04f, 0.04f));
                Vector3 a = new(x, y - 0.02f, 0f);
                VertexMaterial cloth = PlazaStone.Cloth(Festive[(i + offset) % Festive.Length] * random.Range(0.92f, 1.04f));
                b.Triangle(a - across, a + across, a + hang, cloth);
                b.Triangle(a + across, a - across, a + hang, cloth);
            }
        }
    }
}
