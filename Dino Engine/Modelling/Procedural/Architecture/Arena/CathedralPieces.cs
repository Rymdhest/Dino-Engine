using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Plaza;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Arena
{
    /// <summary>
    /// The Cathedral of the Iron Apex, the Reich's to its creed that the strongest blood is owed the world: clustered piers of dark granite; arches of riveted black iron springing from them across the
    /// nave, which is open to the night; the iron screens between the piers and the gates in the walls under them; the apse with its tall windows of oxblood and amber glass; the throne-cage where the
    /// tyrant is anointed and the Iron Apex over it, a tyrant's skull in black iron with amber in its eyes; the chandeliers, iron wheels of candles, and their chains; and the pews of the faithful.
    /// Model y = 0 is where people stand at a piece's foot, and +z its front. Headless.
    /// </summary>
    internal static class CathedralPieces
    {
        private const float Foot = -0.6f;
        private static readonly Vector3 Blood = new Vector3(0.62f, 0.05f, 0.035f);
        private static readonly Vector3 Amber = new Vector3(1f, 0.52f, 0.1f);

        // ---------------------------------------------------------------- Piers

        /// <summary>
        /// A clustered pier: an octagonal plinth, a core with four great shafts and four lesser ones between them, rings binding them at a third and two thirds, and a capital and abacus at
        /// <paramref name="height"/> where the iron springs.
        /// </summary>
        public static void Pier(MeshBuilder b, ArenaStoneKind stone, float height, uint seed)
        {
            var random = new WorldRandom(seed).Fork("pier");
            ArenaStone.Block(b, new Vector3(0f, (Foot + 0.7f) * 0.5f, 0f), new Vector3(1.05f, (0.7f - Foot) * 0.5f, 1.05f), MathF.PI / 8f, ArenaStone.BigChamfer, ArenaStone.DressedFacing(stone, random.Fork("plinth"), 0f));
            ArenaStone.Block(b, new Vector3(0f, 0.85f, 0f), new Vector3(0.92f, 0.15f, 0.92f), MathF.PI / 8f, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork("base"), 0.7f));
            float shaftFrom = 1f, shaftTo = height - 1f;
            int drums = Math.Max(4, (int)MathF.Round((shaftTo - shaftFrom) / 1.4f));
            for (int d = 0; d < drums; d++)
            {
                float y0 = shaftFrom + (shaftTo - shaftFrom) * d / drums, y1 = shaftFrom + (shaftTo - shaftFrom) * (d + 1) / drums;
                VertexMaterial m = ArenaStone.Dressed(stone, random.Fork(d), y0, 0.04f);
                b.Tube(new Vector3(0f, y0 + 0.003f, 0f), new Vector3(0f, y1 - 0.003f, 0f), 0.55f, 0.55f, 12, m, capped: false);
                for (int k = 0; k < 8; k++)
                {
                    float a = k * MathF.PI * 0.25f;
                    bool great = k % 2 == 0;
                    float r = great ? 0.62f : 0.6f, w = great ? 0.2f : 0.11f;
                    Vector3 off = new Vector3(MathF.Cos(a) * r, 0f, MathF.Sin(a) * r);
                    b.Tube(off + new Vector3(0f, y0 + 0.003f, 0f), off + new Vector3(0f, y1 - 0.003f, 0f), w, w, 8, m, capped: false);
                }
            }
            foreach (float t in new[] { 0.34f, 0.67f })
            {
                float y = shaftFrom + (shaftTo - shaftFrom) * t;
                b.Tube(new Vector3(0f, y - 0.09f, 0f), new Vector3(0f, y + 0.09f, 0f), 0.86f, 0.86f, 16, ArenaStone.Dressed(stone, random.Fork(100 + (int)(t * 10f)), y, 0.03f));
            }
            // The capital, flaring to the abacus, and a ring of iron where the arch is bolted to it.
            b.Tube(new Vector3(0f, shaftTo, 0f), new Vector3(0f, shaftTo + 0.6f, 0f), 0.86f, 1.15f, 16, ArenaStone.Dressed(stone, random.Fork("capital"), shaftTo, 0.03f));
            ArenaStone.Block(b, new Vector3(0f, height - 0.2f, 0f), new Vector3(1.2f, 0.2f, 1.2f), MathF.PI / 8f, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork("abacus"), height, 0.1f));
            b.Tube(new Vector3(0f, height, 0f), new Vector3(0f, height + 0.25f, 0f), 0.75f, 0.75f, 12, ArenaStone.Iron(random));
        }

        // ---------------------------------------------------------------- The iron

        /// <summary>How far under an iron arch's crown the bottom of the ring its chandelier's chain hangs from is.</summary>
        public const float RingBelow = 0.73f;

        /// <summary>
        /// An arch of riveted black iron across x from -<paramref name="span"/>/2 to +<paramref name="span"/>/2 at y = 0, pointed, its crown <paramref name="rise"/> up (<see cref="PointedArch"/>): a
        /// deep rib, two flanges and a web in the arch's own plane, as riveted iron is built, so that it's narrow seen from above and hides little of the floor under it; plates stiffening it, rivets
        /// along its flanges and web, the two halves mitred at the crown under an iron collar with the Reich's mark on it in brass, a shoe at each foot, and under the crown a ring that a chandelier's
        /// chain hangs from (<see cref="RingBelow"/>). Its centre line is the arch: span and rise are measured on it.
        /// </summary>
        public static void IronArch(MeshBuilder b, float span, float rise, uint seed)
        {
            var random = new WorldRandom(seed).Fork("iron arch");
            VertexMaterial iron = ArenaStone.Iron(random), dark = ArenaStone.Iron(random, 0.7f), brass = ArenaStone.Brass(random);
            float half = span * 0.5f;
            ArchArc[] arcs = PointedArch(half, rise);
            ArchArc last = arcs[^1];
            const float Depth = 0.56f, Flange = 0.06f, FlangeHalf = 0.13f, WebHalf = 0.03f, RivetGap = 0.4f, PlateGap = 1.15f;
            const float Outer = Depth * 0.5f, Inner = Outer - Flange;
            // Where every offset of the last arc meets x = 0, where the other half's does, so that the two meet in a mitre with nothing open between them and nothing through each other.
            float CrownAt(float n)
            {
                float r = last.Radius + n;
                return last.Centre.Y + MathF.Sqrt(MathF.Max(0f, r * r - last.Centre.X * last.Centre.X));
            }
            float length = 0f;
            foreach (ArchArc arc in arcs) length += arc.Radius * MathF.Abs(arc.To - arc.From);

            foreach (float side in new[] { -1f, 1f })
            {
                // A point n out from the arch's line (away from the arc's centre) and z across it, on an arc at an angle: the right half as the arcs are, the left half mirrored.
                Vector3 On(ArchArc arc, float angle, float n, float z)
                {
                    float r = arc.Radius + n;
                    return new Vector3(side * (arc.Centre.X + MathF.Cos(angle) * r), arc.Centre.Y + MathF.Sin(angle) * r, z);
                }
                Vector3 Out(float angle) => new Vector3(side * MathF.Cos(angle), MathF.Sin(angle), 0f);
                // The i-th of an arc's steps, the very last of all ending on the mitre.
                Vector3 Corner(int arcIndex, int i, float n, float z)
                {
                    ArchArc arc = arcs[arcIndex];
                    if (arcIndex == arcs.Length - 1 && i == arc.Steps) return new Vector3(0f, CrownAt(n), z);
                    return On(arc, arc.From + (arc.To - arc.From) * i / arc.Steps, n, z);
                }
                // A bar of the section swept along the arch, n0 to n1 out and z0 to z1 across, with or without its inner and outer faces.
                void Bar(float n0, float n1, float z0, float z1, VertexMaterial m, bool ends)
                {
                    for (int j = 0; j < arcs.Length; j++)
                    {
                        ArchArc arc = arcs[j];
                        for (int i = 0; i < arc.Steps; i++)
                        {
                            Vector3 outward = Out(arc.From + (arc.To - arc.From) * (i + 0.5f) / arc.Steps);
                            if (ends)
                            {
                                Face(b, Corner(j, i, n1, z0), Corner(j, i + 1, n1, z0), Corner(j, i + 1, n1, z1), Corner(j, i, n1, z1), outward, m);
                                Face(b, Corner(j, i, n0, z0), Corner(j, i + 1, n0, z0), Corner(j, i + 1, n0, z1), Corner(j, i, n0, z1), -outward, m);
                            }
                            Face(b, Corner(j, i, n0, z1), Corner(j, i + 1, n0, z1), Corner(j, i + 1, n1, z1), Corner(j, i, n1, z1), Vector3.UnitZ, m);
                            Face(b, Corner(j, i, n0, z0), Corner(j, i + 1, n0, z0), Corner(j, i + 1, n1, z0), Corner(j, i, n1, z0), -Vector3.UnitZ, m);
                        }
                    }
                }
                // Where along the arch a share of its length falls: its arc and the angle on it.
                (ArchArc arc, float angle) Along(float share)
                {
                    float s = share * length;
                    foreach (ArchArc arc in arcs)
                    {
                        float l = arc.Radius * MathF.Abs(arc.To - arc.From);
                        if (s <= l || arc.Equals(last)) return (arc, arc.From + (arc.To - arc.From) * MathF.Min(1f, s / l));
                        s -= l;
                    }
                    return (last, last.To);
                }

                Bar(Inner, Outer, -FlangeHalf, FlangeHalf, iron, true);
                Bar(-Outer, -Inner, -FlangeHalf, FlangeHalf, iron, true);
                // The web, into both flanges, so that where it meets them is inside them.
                Bar(-Inner - 0.01f, Inner + 0.01f, -WebHalf, WebHalf, dark, false);

                // Plates across the web, a little over a metre apart, from flange to flange.
                int plates = Math.Max(2, (int)MathF.Round(length / PlateGap));
                for (int k = 0; k < plates; k++)
                {
                    (ArchArc arc, float angle) = Along((k + 0.5f) / plates);
                    Vector3 n = Out(angle), along = new Vector3(-n.Y, n.X, 0f);
                    OrientedBox(b, On(arc, angle, 0f, 0f), along, n, Vector3.UnitZ, new Vector3(0.016f, Inner + 0.005f, FlangeHalf - 0.02f), dark);
                }
                // Rivets: two rows along the top of the outer flange, which is what's seen from above, and two along each face of the web by its flanges.
                int rivets = Math.Max(4, (int)(length / RivetGap));
                for (int k = 0; k < rivets; k++)
                {
                    (ArchArc arc, float angle) = Along((k + 0.5f) / rivets);
                    Vector3 n = Out(angle);
                    foreach (float z in new[] { -FlangeHalf + 0.045f, FlangeHalf - 0.045f })
                    {
                        Vector3 on = On(arc, angle, Outer, z);
                        b.Tube(on - n * 0.005f, on + n * 0.022f, 0.026f, 0.006f, 4, iron, capped: false);
                    }
                    foreach (float across in new[] { -1f, 1f })
                    {
                        foreach (float row in new[] { Inner - 0.05f, -Inner + 0.05f })
                        {
                            Vector3 on = On(arc, angle, row, across * WebHalf);
                            b.Tube(on - Vector3.UnitZ * across * 0.005f, on + Vector3.UnitZ * across * 0.02f, 0.022f, 0.005f, 4, iron, capped: false);
                        }
                    }
                }
                // Its foot, a shoe of iron the rib stands in, bolted to the pier.
                Vector3 foot = new Vector3(side * half, 0f, 0f);
                b.Box(foot + new Vector3(0f, 0.16f, 0f), new Vector3(Outer + 0.12f, 0.22f, FlangeHalf + 0.07f), dark);
                foreach (float bx in new[] { -1f, 1f })
                {
                    foreach (float bz in new[] { -1f, 1f })
                    {
                        Vector3 bolt = foot + new Vector3(bx * (Outer + 0.02f), 0.38f, bz * (FlangeHalf + 0.01f));
                        b.Tube(bolt, bolt + Vector3.UnitY * 0.05f, 0.045f, 0.03f, 6, iron);
                    }
                }
            }
            // The collar over the mitre at the crown, with the mark on either face, and brass straps at its edges.
            float top = CrownAt(Outer), bottom = CrownAt(-Outer);
            Vector3 collar = new Vector3(0f, (top + bottom) * 0.5f, 0f);
            Vector3 collarHalf = new Vector3(0.3f, (top - bottom) * 0.5f + 0.1f, FlangeHalf + 0.03f);
            b.Box(collar, collarHalf, dark, closeBottom: true);
            foreach (float sx in new[] { -1f, 1f })
            {
                b.Box(collar + new Vector3(sx * (collarHalf.X - 0.03f), 0f, 0f), new Vector3(0.035f, collarHalf.Y + 0.015f, collarHalf.Z + 0.015f), brass, closeBottom: true);
            }
            foreach (float face in new[] { -1f, 1f })
            {
                // Its normal is right × up, which faces out of the collar on either face.
                Vector3 right = new Vector3(face, 0f, 0f);
                ArenaStone.Mark(b, collar + new Vector3(0f, 0f, face * collarHalf.Z), right, Vector3.UnitY, 0.19f, 0.025f, brass, dark);
            }
            // The ring under the crown, its top through the collar's underside, which is about 0.4 m under the crown.
            Vector3 ring = new Vector3(0f, rise - RingBelow + 0.18f, 0f);
            const int Links = 10;
            for (int k = 0; k < Links; k++)
            {
                float a0 = k * MathF.Tau / Links, a1 = (k + 1) * MathF.Tau / Links;
                b.Tube(ring + new Vector3(MathF.Cos(a0) * 0.18f, MathF.Sin(a0) * 0.18f, 0f), ring + new Vector3(MathF.Cos(a1) * 0.18f, MathF.Sin(a1) * 0.18f, 0f), 0.035f, 0.035f, 4, iron, capped: false);
            }
        }

        /// <summary>One arc of half an arch: its centre, its radius, the angles about the centre it runs between, and how many steps it's drawn in.</summary>
        internal readonly record struct ArchArc(Vector2 Centre, float Radius, float From, float To, int Steps);

        /// <summary>
        /// The right half of a pointed arch across 2·<paramref name="half"/> rising <paramref name="rise"/>, from its springing at (half, 0) to its crown at (0, rise), in arcs that meet with no kink.
        /// One arc centred across the middle if it rises at least as far as it's half wide; otherwise four-centred, as a low Gothic arch is, a tight arc off the springing, which rises upright from
        /// it, turning into a wide one centred far below across the middle, which meets the other half's at a point whose sides fall a little over 20 degrees from level. Two arcs each centred across
        /// the middle can't make a low arch: they rise past the crown and droop into it.
        /// </summary>
        internal static ArchArc[] PointedArch(float half, float rise)
        {
            if (rise >= half)
            {
                float radius = (half * half + rise * rise) / (2f * half);
                float cx = half - radius;
                return new[] { new ArchArc(new Vector2(cx, 0f), radius, 0f, MathF.Atan2(rise, -cx), 14) };
            }
            // The tight arc is centred on the springing line at c1 = (half - r1, 0). The wide one's centre c2 lies on the line from c1 through where they meet, at an angle phi, so the two share a
            // tangent there, and the crown is on it: |c2 - (0, rise)| = r1 + d, where d = |c1 - c2|, which settles d for each phi. Of the phis that point the crown, the one whose crown falls
            // nearest the angle wanted.
            float r1 = 0.4f * half, a = half - r1;
            float want = 21f * MathF.PI / 180f, best = float.MaxValue, bestPhi = 0f, bestD = 0f;
            for (float degrees = 20f; degrees <= 85f; degrees += 0.25f)
            {
                float phi = degrees * MathF.PI / 180f;
                float den = a * MathF.Cos(phi) - rise * MathF.Sin(phi) + r1;
                if (den <= 1e-4f) continue;
                float d = (a * a + rise * rise - r1 * r1) / (2f * den);
                if (d <= 0f) continue;
                Vector2 c2 = new Vector2(a - d * MathF.Cos(phi), -d * MathF.Sin(phi));
                if (c2.X > -0.02f * half) continue;
                float fall = MathF.Atan2(-c2.X, rise - c2.Y);
                if (MathF.Abs(fall - want) < best) { best = MathF.Abs(fall - want); bestPhi = phi; bestD = d; }
            }
            if (best == float.MaxValue)
            {
                // Nothing four-centred fits: two arcs centred below the springing line and across the middle, which spring leaning in.
                float cx = -0.2f * half;
                float cy = (rise * rise - half * half + 2f * half * cx) / (2f * rise);
                float radius = MathF.Sqrt((half - cx) * (half - cx) + cy * cy);
                return new[] { new ArchArc(new Vector2(cx, cy), radius, MathF.Atan2(-cy, half - cx), MathF.Atan2(rise - cy, -cx), 14) };
            }
            Vector2 wide = new Vector2(a - bestD * MathF.Cos(bestPhi), -bestD * MathF.Sin(bestPhi));
            return new[]
            {
                new ArchArc(new Vector2(a, 0f), r1, 0f, bestPhi, 6),
                new ArchArc(wide, r1 + bestD, bestPhi, MathF.Atan2(rise - wide.Y, -wide.X), 12),
            };
        }

        // A quad wound to face `want`, whichever way round its corners were given.
        private static void Face(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 want, VertexMaterial m)
        {
            if (Vector3.Dot(Vector3.Cross(p2 - p0, p3 - p1), want) < 0f) b.Quad(p0, p3, p2, p1, m);
            else b.Quad(p0, p1, p2, p3, m);
        }

        // A box about `centre` along three axes square to each other, `half` along each.
        private static void OrientedBox(MeshBuilder b, Vector3 centre, Vector3 ax, Vector3 ay, Vector3 az, Vector3 half, VertexMaterial m)
        {
            Vector3 x = ax * half.X, y = ay * half.Y, z = az * half.Z;
            Vector3 P(float i, float j, float k) => centre + x * i + y * j + z * k;
            Face(b, P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), ax, m);
            Face(b, P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), -ax, m);
            Face(b, P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), ay, m);
            Face(b, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), -ay, m);
            Face(b, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), az, m);
            Face(b, P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), -az, m);
        }

        /// <summary>A chain hanging from a ring at y = 0 down to <paramref name="length"/> below it, link by link, ending snapped: what a chandelier hung from.</summary>
        public static void Chain(MeshBuilder b, float length, uint seed)
        {
            var random = new WorldRandom(seed).Fork("chain");
            VertexMaterial iron = ArenaStone.Iron(random), dark = ArenaStone.Iron(random, 0.7f);
            int links = Math.Max(4, (int)(length / 0.32f));
            for (int l = 0; l < links; l++)
            {
                float y0 = -length * l / links, y1 = -length * (l + 1) / links;
                bool across = l % 2 == 0;
                Vector3 a = new Vector3(0f, y0 - 0.02f, 0f), c = new Vector3(0f, y1 + 0.02f, 0f);
                Vector3 w = across ? new Vector3(0.07f, 0f, 0f) : new Vector3(0f, 0f, 0.07f);
                b.Tube(a + w, c + w, 0.028f, 0.028f, 4, across ? iron : dark);
                b.Tube(a - w, c - w, 0.028f, 0.028f, 4, across ? iron : dark);
            }
        }

        /// <summary>
        /// A chandelier, an iron wheel <paramref name="radius"/> across with candles round it, hung from four short chains to a hook over its middle at y = 0, its candles burning as particles and a light
        /// in its middle. It's a model of its own, as it comes down.
        /// </summary>
        public static void Chandelier(MeshBuilder b, float radius, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("chandelier");
            VertexMaterial iron = ArenaStone.Iron(random), brass = ArenaStone.Brass(random);
            VertexMaterial wax = new VertexMaterial(MaterialCatalog.Wax.Index, new Colour(0.86f, 0.8f, 0.66f));
            float wheel = -2.2f;
            const int Sides = 24;
            for (int k = 0; k < Sides; k++)
            {
                float a0 = k * MathF.Tau / Sides, a1 = (k + 1) * MathF.Tau / Sides;
                Vector3 p0 = new Vector3(MathF.Cos(a0) * radius, wheel, MathF.Sin(a0) * radius), p1 = new Vector3(MathF.Cos(a1) * radius, wheel, MathF.Sin(a1) * radius);
                b.Tube(p0, p1, 0.09f, 0.09f, 5, iron, capped: false);
                b.Tube(p0 * new Vector3(0.62f, 1f, 0.62f), p1 * new Vector3(0.62f, 1f, 0.62f), 0.05f, 0.05f, 4, iron, capped: false);
            }
            for (int k = 0; k < 4; k++)
            {
                float a = k * MathF.PI * 0.5f + MathF.PI * 0.25f;
                Vector3 rim = new Vector3(MathF.Cos(a) * radius, wheel, MathF.Sin(a) * radius);
                b.Tube(rim, Vector3.Zero, 0.025f, 0.025f, 4, iron);
                b.Tube(rim, rim * new Vector3(0.62f, 1f, 0.62f), 0.04f, 0.04f, 4, iron);
            }
            b.Tube(new Vector3(0f, -0.2f, 0f), new Vector3(0f, 0.15f, 0f), 0.1f, 0.06f, 6, brass);
            // The candles round the rim, spiked on iron cups, a spike at its foot.
            int candles = Math.Max(10, (int)(radius * 7f));
            for (int c = 0; c < candles; c++)
            {
                float a = c * MathF.Tau / candles;
                Vector3 cup = new Vector3(MathF.Cos(a) * radius, wheel + 0.1f, MathF.Sin(a) * radius);
                b.Tube(cup, cup + Vector3.UnitY * 0.08f, 0.1f, 0.12f, 6, iron);
                float h = random.Range(0.3f, 0.55f);
                b.Tube(cup + Vector3.UnitY * 0.08f, cup + Vector3.UnitY * (0.08f + h), 0.06f, 0.055f, 6, wax);
                if (c % 3 == 0) lights.Add(AssetLight.Burning(cup + Vector3.UnitY * (0.08f + h), 0.07f));
            }
            b.Tube(new Vector3(0f, wheel, 0f), new Vector3(0f, wheel - 0.6f, 0f), 0.12f, 0.0f, 6, iron);
            // Its light is a fill from high over the nave, and casts no shadow: three of them nearest the hero took the cube maps that are lent to lights, each rendering the whole nave and its crowd.
            lights.Add(AssetLight.CandleStand(new Vector3(0f, wheel - 0.4f, 0f)) with { Intensity = 110f, Radius = 19f, Shadow = false });
        }

        /// <summary>How far under its hook a chandelier's wheel hangs.</summary>
        public const float ChandelierDrop = 2.2f;

        // ---------------------------------------------------------------- Screens and their gates

        /// <summary>A run of iron screen along +x on top of a wall at y = 0, <paramref name="length"/> long and <paramref name="height"/> high: bars with spear heads, rails, quatrefoils of iron between them.</summary>
        public static void Screen(MeshBuilder b, float length, float height, uint seed)
        {
            var random = new WorldRandom(seed).Fork("screen");
            VertexMaterial iron = ArenaStone.Iron(random), dark = ArenaStone.Iron(random, 0.75f);
            int bars = Math.Max(4, (int)MathF.Round(length / 0.34f));
            for (int i = 0; i <= bars; i++)
            {
                float x = length * i / bars;
                b.Tube(new Vector3(x, -0.05f, 0f), new Vector3(x, height - 0.4f, 0f), 0.04f, 0.04f, 4, iron);
                b.Tube(new Vector3(x, height - 0.4f, 0f), new Vector3(x, height, 0f), 0.08f, 0.005f, 4, iron);
            }
            foreach (float y in new[] { 0.25f, height * 0.42f, height - 0.55f })
            {
                b.Box(new Vector3(length * 0.5f, y, 0f), new Vector3(length * 0.5f, 0.05f, 0.05f), dark);
            }
            int foils = Math.Max(1, (int)MathF.Round(length / 1.4f));
            float band = height * 0.42f - 0.25f;
            for (int f = 0; f < foils; f++)
            {
                Vector3 middle = new Vector3(length * (f + 0.5f) / foils, 0.25f + band * 0.5f, 0.02f);
                float r = MathF.Min(length / foils, band) * 0.22f;
                for (int k = 0; k < 4; k++)
                {
                    float a = k * MathF.PI * 0.5f;
                    Vector3 lobe = middle + new Vector3(MathF.Cos(a), MathF.Sin(a), 0f) * r * 0.9f;
                    const int Sides = 10;
                    for (int s = 0; s < Sides; s++)
                    {
                        float a0 = s * MathF.Tau / Sides, a1 = (s + 1) * MathF.Tau / Sides;
                        b.Tube(lobe + new Vector3(MathF.Cos(a0), MathF.Sin(a0), 0f) * r * 0.75f, lobe + new Vector3(MathF.Cos(a1), MathF.Sin(a1), 0f) * r * 0.75f, 0.025f, 0.025f, 4, dark, capped: false);
                    }
                }
            }
        }

        /// <summary>
        /// A gate in a wall, its face on z = 0 towards +z: a pointed arch of dressed stone round an opening <paramref name="width"/> across, springing at <paramref name="spring"/>, with a hood mould
        /// over it and a passage going dark; its grille is a model of its own, which rises.
        /// </summary>
        public static void ScreenGate(MeshBuilder b, ArenaStoneKind stone, float width, float spring, float depth, uint seed)
        {
            var random = new WorldRandom(seed).Fork("screen gate");
            float half = width * 0.5f, outer = half + 1.1f;
            float radius = width * 0.85f;
            float crownY = spring + MathF.Sqrt(MathF.Max(radius * radius - (radius - half) * (radius - half), 0f));
            foreach (float side in new[] { -1f, 1f })
            {
                int courses = Math.Max(3, (int)MathF.Round((spring - Foot) / 0.55f));
                for (int c = 0; c < courses; c++)
                {
                    float y0 = Foot + (spring - Foot) * c / courses, y1 = Foot + (spring - Foot) * (c + 1) / courses;
                    float split = c % 2 == 0 ? 0.45f : 0.65f;
                    Lay(b, side * half, side * (half + (outer - half) * split), y0, y1, 0.22f, -depth, stone, random.Fork(c * 2 + (side > 0f ? 1 : 0)));
                    Lay(b, side * (half + (outer - half) * split), side * outer, y0, y1, 0.22f, -depth, stone, random.Fork(c * 2 + 40 + (side > 0f ? 1 : 0)));
                }
            }
            // The pointed arch in voussoirs, and the spandrels squared off to a lintel course over it.
            const int Stones = 7;
            const float Ring = 0.55f;
            foreach (float side in new[] { -1f, 1f })
            {
                float cx = -side * (radius - half);
                float start = side > 0f ? 0f : MathF.PI;
                float crown = MathF.Atan2(crownY - spring, -cx);
                for (int i = 0; i < Stones; i++)
                {
                    float t0 = i / (float)Stones, t1 = (i + 1) / (float)Stones;
                    float a0 = start + (crown - start) * t0, a1 = start + (crown - start) * t1;
                    Vector3 P(float a, float r, float z) => new Vector3(cx + MathF.Cos(a) * r, spring + MathF.Sin(a) * r, z);
                    PlazaWalls.Lay(b, new[]
                    {
                        P(a1, radius, 0.26f), P(a0, radius, 0.26f), P(a0, radius + Ring, 0.26f), P(a1, radius + Ring, 0.26f),
                        P(a1, radius, -0.8f), P(a0, radius, -0.8f), P(a0, radius + Ring, -0.8f), P(a1, radius + Ring, -0.8f),
                    }, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork(100 + i + (side > 0f ? 50 : 0)), spring + 1f, 0.12f));
                }
            }
            float lintel = crownY + Ring + 0.1f;
            ArenaGates.Slabs(b, -outer, outer, -depth, 0.22f, spring, lintel + 0.6f, 1.3f, stone, random.Fork("over"), 0.14f);
            // The passage, going dark.
            b.Quad(new Vector3(-half, -0.3f, -depth + 0.3f), new Vector3(half, -0.3f, -depth + 0.3f), new Vector3(half, crownY, -depth + 0.3f), new Vector3(-half, crownY, -depth + 0.3f), ArenaStone.Dark);
        }

        // ---------------------------------------------------------------- The apse

        /// <summary>
        /// The apse round an arc about the origin, facing in towards it: piers of dark stone between tall pointed windows of oxblood and amber glass in lead, each with a mullion up its middle and a
        /// circle of tracery in its head, over a sill, under a parapet. <paramref name="windows"/> windows round from <paramref name="from"/> through <paramref name="sweep"/> degrees.
        /// </summary>
        public static void Apse(MeshBuilder b, ArenaStoneKind stone, float radius, float from, float sweep, float height, int windows, uint seed)
        {
            var random = new WorldRandom(seed).Fork("apse");
            var line = WallLine.Arc(radius, from, sweep, -1f);
            float bay = line.Length / windows;
            const float Thick = 1.6f, Sill = 5f, PierShare = 0.42f;
            // The wall under the windows, all round, coursed.
            for (float y = Foot, c = 0; y < Sill - 0.05f; c++)
            {
                float rise = MathF.Min(0.55f, Sill - y);
                ArenaWalls.Course(b, line, y, y + rise, 0f, Thick, 1.3f, (c % 2) * 0.65f + random.Range(0f, 0.3f), random.Fork((int)c), stone, ArenaStone.Chamfer, 0.06f);
                y += rise;
            }
            ArenaWalls.Course(b, line, Sill, Sill + 0.3f, -0.12f, Thick + 0.06f, 1.5f, random.Range(0f, 0.8f), random.Fork("sill"), stone, ArenaStone.Chamfer, 0.04f, dust: 0.12f);
            float windowTop = height - 1.8f;
            for (int k = 0; k <= windows; k++)
            {
                // A pier between each window, coursed, from the sill to the top.
                float along = k * bay;
                float from0 = MathF.Max(along - bay * PierShare * 0.5f, 0f), to0 = MathF.Min(along + bay * PierShare * 0.5f, line.Length);
                if (to0 - from0 < 0.2f) continue;
                for (float y = Sill + 0.3f, c = 0; y < height - 0.05f; c++)
                {
                    float rise = MathF.Min(0.6f, height - y);
                    ArenaWalls.StoneBetween(b, line, from0 + 0.004f, to0 - 0.004f, y + 0.003f, y + rise - 0.003f, 0f, Thick, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork(1000 + k * 40 + (int)c), y));
                    y += rise;
                }
            }
            // Each window: its head pointed, the wall over it, its glass in lead, its mullion and the circle in its head.
            for (int k = 0; k < windows; k++)
            {
                float middle = (k + 0.5f) * bay;
                float halfOpening = bay * (1f - PierShare) * 0.5f;
                float headFrom = windowTop - halfOpening * 1.6f;
                Glass(b, line, middle, halfOpening, Sill + 0.3f, headFrom, windowTop, Thick * 0.5f, random.Fork(2000 + k));
                // The wall over the window's point, up to the parapet.
                for (float y = headFrom, c = 0; y < height - 0.05f; c++)
                {
                    float rise = MathF.Min(0.6f, height - y);
                    float clear = WindowHalf(halfOpening, headFrom, windowTop, y + rise * 0.5f);
                    float l0 = middle - halfOpening, r0 = middle + halfOpening;
                    if (middle - clear - l0 > 0.06f) ArenaWalls.StoneBetween(b, line, l0 + 0.004f, middle - clear - 0.004f, y + 0.003f, y + rise - 0.003f, 0.04f, Thick - 0.04f, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork(3000 + k * 40 + (int)c), y));
                    if (r0 - (middle + clear) > 0.06f) ArenaWalls.StoneBetween(b, line, middle + clear + 0.004f, r0 - 0.004f, y + 0.003f, y + rise - 0.003f, 0.04f, Thick - 0.04f, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork(3500 + k * 40 + (int)c), y));
                    y += rise;
                }
            }
            ArenaWalls.Course(b, line, height, height + 0.4f, -0.18f, Thick + 0.18f, 1.5f, random.Range(0f, 0.8f), random.Fork("parapet"), stone, ArenaStone.BigChamfer, 0.04f, dust: 0.1f);
        }

        // How wide a pointed window head is at a height: full below its springing, narrowing to its point.
        private static float WindowHalf(float half, float springing, float point, float y)
        {
            if (y <= springing) return half;
            if (y >= point) return 0f;
            float t = (y - springing) / (point - springing);
            return half * MathF.Sqrt(MathF.Max(1f - t * t, 0f));
        }

        // A window's glass between its jambs, `back` into the wall, in diamonds of oxblood and amber divided by lead, with a mullion of stone up its middle and a ring of it in its head. It glows, as
        // it's always night in the cathedral and there are fires behind it.
        private static void Glass(MeshBuilder b, WallLine line, float middle, float half, float bottom, float springing, float point, float back, WorldRandom random)
        {
            VertexMaterial lead = new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(0.05f, 0.05f, 0.055f));
            const float Pane = 0.55f;
            for (float y = bottom; y < point - 0.05f; y += Pane)
            {
                for (float x = -half; x < half - 0.02f; x += Pane)
                {
                    float y1 = MathF.Min(y + Pane, point);
                    float w0 = WindowHalf(half, springing, point, y), w1 = WindowHalf(half, springing, point, y1);
                    float x0 = MathF.Max(x, -MathF.Min(w0, w1)), x1 = MathF.Min(x + Pane, MathF.Min(w0, w1));
                    if (x1 - x0 < 0.05f) continue;
                    bool blood = ((int)((x + half) / Pane) + (int)((y - bottom) / Pane)) % 3 != 0;
                    Vector3 colour = blood ? Blood * random.Range(0.85f, 1.15f) : Amber * random.Range(0.8f, 1.05f);
                    VertexMaterial glass = ArenaStone.Glow(colour.X, colour.Y, colour.Z, blood ? 1.5f : 1.2f);
                    Vector3 a = line.At(middle + x0 + 0.02f, back, y + 0.02f), c = line.At(middle + x1 - 0.02f, back, y + 0.02f);
                    Vector3 d = line.At(middle + x1 - 0.02f, back, y1 - 0.02f), e = line.At(middle + x0 + 0.02f, back, y1 - 0.02f);
                    b.Quad(e, d, c, a, glass);
                    b.Quad(a, c, d, e, glass);
                }
            }
            // Lead behind the panes, which is what shows between them.
            Vector3 la = line.At(middle - half, back + 0.03f, bottom), lc = line.At(middle + half, back + 0.03f, bottom);
            Vector3 ld = line.At(middle + half, back + 0.03f, point), le = line.At(middle - half, back + 0.03f, point);
            b.Quad(le, ld, lc, la, lead);
            b.Quad(la, lc, ld, le, lead);
            // The mullion, and the ring of tracery in the head.
            ArenaWalls.StoneBetween(b, line, middle - 0.11f, middle + 0.11f, bottom, springing + (point - springing) * 0.35f, back - 0.18f, back + 0.12f, 0.02f, _ => ArenaStone.Dressed(ArenaStoneKind.DarkGranite, random, bottom + 3f, 0.04f));
            Vector3 ring = line.At(middle, back - 0.1f, springing + (point - springing) * 0.5f);
            Vector3 across = (line.At(middle + 1f, back - 0.1f, 0f) - line.At(middle - 1f, back - 0.1f, 0f)).Normalized();
            const int Sides = 14;
            float r = half * 0.55f;
            for (int s = 0; s < Sides; s++)
            {
                float a0 = s * MathF.Tau / Sides, a1 = (s + 1) * MathF.Tau / Sides;
                b.Tube(ring + across * MathF.Cos(a0) * r + Vector3.UnitY * MathF.Sin(a0) * r, ring + across * MathF.Cos(a1) * r + Vector3.UnitY * MathF.Sin(a1) * r, 0.07f, 0.07f, 4, ArenaStone.Dressed(ArenaStoneKind.DarkGranite, random, 6f, 0.04f), capped: false);
            }
        }

        /// <summary>Where an apse's windows are, round its arc: the middle of each, at the height of its middle, for the lights that shine through them.</summary>
        public static IEnumerable<(float Bearing, float Height)> ApseWindows(float from, float sweep, float height, int windows)
        {
            for (int k = 0; k < windows; k++) yield return (from + sweep * (k + 0.5f) / windows, (5.3f + height - 1.8f) * 0.5f);
        }

        // ---------------------------------------------------------------- The throne and the Iron Apex

        /// <summary>
        /// The altar the tyrant is anointed at: three steps of dark stone up to a cage of black iron like a throne, its bars tall at the back and low at the front, brass at its finials, candles banked
        /// round it on the steps.
        /// </summary>
        public static void Throne(MeshBuilder b, ArenaStoneKind stone, float width, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("throne");
            float half = width * 0.5f;
            for (int s = 0; s < 3; s++)
            {
                float h = half - s * 0.7f, y0 = Foot + (s == 0 ? 0f : 0.6f + (s - 1) * 0.45f), y1 = 0.6f + s * 0.45f;
                ArenaGates.Slabs(b, -h, h, -h * 0.75f, h * 0.75f, y0, y1, 1.4f, stone, random.Fork(s), 0.1f);
            }
            float top = 0.6f + 2f * 0.45f;
            VertexMaterial iron = ArenaStone.Iron(random), brass = ArenaStone.Brass(random);
            float cage = half - 1.6f;
            int bars = 14;
            for (int i = 0; i < bars; i++)
            {
                float a = MathF.PI * i / (bars - 1);
                Vector3 foot = new Vector3(-MathF.Cos(a) * cage, top, -MathF.Sin(a) * cage * 0.7f);
                float tall = 3.2f + 3.4f * MathF.Sin(a);
                b.Tube(foot, foot + Vector3.UnitY * tall, 0.07f, 0.06f, 5, iron);
                b.Tube(foot + Vector3.UnitY * tall, foot + Vector3.UnitY * (tall + 0.45f), 0.1f, 0f, 4, brass);
            }
            foreach (float y in new[] { top + 0.6f, top + 2.6f })
            {
                for (int i = 0; i < bars - 1; i++)
                {
                    float a0 = MathF.PI * i / (bars - 1), a1 = MathF.PI * (i + 1) / (bars - 1);
                    b.Tube(new Vector3(-MathF.Cos(a0) * cage, y, -MathF.Sin(a0) * cage * 0.7f), new Vector3(-MathF.Cos(a1) * cage, y, -MathF.Sin(a1) * cage * 0.7f), 0.05f, 0.05f, 4, iron, capped: false);
                }
            }
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 bank = new Vector3(s * (half - 0.8f), 0.6f, half * 0.6f);
                VertexMaterial wax = new VertexMaterial(MaterialCatalog.Wax.Index, new Colour(0.86f, 0.8f, 0.66f));
                for (int c = 0; c < 9; c++)
                {
                    Vector3 at = bank + new Vector3(random.Range(-0.5f, 0.5f), 0f, random.Range(-0.4f, 0.4f));
                    float h = random.Range(0.3f, 0.9f);
                    b.Tube(at, at + Vector3.UnitY * h, 0.06f, 0.055f, 6, wax);
                    if (c % 2 == 0) lights.Add(AssetLight.Burning(at + Vector3.UnitY * h, 0.06f));
                }
                lights.Add(AssetLight.VigilCandles(bank + Vector3.UnitY * 1f));
            }
        }

        /// <summary>
        /// The Iron Apex: the skull of the first tyrant, the Reich's mark made solid, in black iron <paramref name="size"/> long and a metre and more deep, bolted to a wall at z = 0 and facing out along
        /// +z, brass on its teeth and amber burning in its eye.
        /// </summary>
        public static void IronSkull(MeshBuilder b, float size, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("skull");
            VertexMaterial iron = ArenaStone.Iron(random, 1.15f), brass = ArenaStone.Brass(random);
            float deep = size * 0.18f;
            Vector3 At(float x, float y, float z) => new Vector3(x * size, y * size, z);
            (float X, float Y)[] outline = ReichMark.Skull;
            Vector3 middle = At(ReichMark.Middle.X, ReichMark.Middle.Y, deep);
            for (int i = 0; i < outline.Length; i++)
            {
                (float x0, float y0) = outline[i];
                (float x1, float y1) = outline[(i + 1) % outline.Length];
                Vector3 a = At(x0, y0, deep), c = At(x1, y1, deep);
                b.Triangle(middle, c, a, iron);
                b.Quad(At(x1, y1, 0f), At(x0, y0, 0f), a, c, iron);
                // Its teeth along the jaw, in brass: the outline's lower points that hang down.
                if (y1 < -0.12f && y0 > y1) b.Tube(c + new Vector3(0f, 0f, -deep * 0.3f), c + new Vector3(0f, -0.06f * size, -deep * 0.3f), 0.025f * size, 0f, 4, brass);
            }
            foreach ((float x, float y, float rx, float ry) in ReichMark.Holes)
            {
                Vector3 hole = At(x, y, deep + 0.01f);
                bool eye = x < -0.2f && y > 0.1f;
                VertexMaterial inside = eye ? ArenaStone.Glow(Amber.X, Amber.Y, Amber.Z, 2.6f) : ArenaStone.Dark;
                const int Sides = 12;
                for (int k = 0; k < Sides; k++)
                {
                    float a0 = k * MathF.Tau / Sides, a1 = (k + 1) * MathF.Tau / Sides;
                    b.Triangle(hole, At(x + MathF.Cos(a0) * rx, y + MathF.Sin(a0) * ry, deep + 0.01f), At(x + MathF.Cos(a1) * rx, y + MathF.Sin(a1) * ry, deep + 0.01f), inside);
                }
                if (eye) lights.Add(AssetLight.Glow(hole + new Vector3(0f, 0f, 0.6f), Amber, 70f, 14f));
            }
            // The bolts that hold it to the wall.
            for (int i = 0; i < 6; i++)
            {
                (float x, float y) = outline[i * outline.Length / 6];
                b.Tube(At(x * 0.85f, y * 0.85f, deep), At(x * 0.85f, y * 0.85f, deep + 0.08f), 0.05f * size, 0.04f * size, 6, brass);
            }
        }

        // ---------------------------------------------------------------- Pews

        /// <summary>Rows of pews along +x, <paramref name="length"/> long, facing +z, one behind another a stride apart: <paramref name="rows"/> of them, oak darkened by hands.</summary>
        public static void Pews(MeshBuilder b, float length, int rows, uint seed)
        {
            var random = new WorldRandom(seed).Fork("pews");
            float ground = ArenaStone.Ground;
            for (int r = 0; r < rows; r++)
            {
                float z = -r * 2.2f;
                VertexMaterial oak = ArenaStone.Timber(random.Fork(r), 0.62f), dark = ArenaStone.Timber(random.Fork(r + 50), 0.45f);
                PlazaWalls.Lay(b, PlazaPieces.Corners(new Vector3(0f, ground + 1.05f, z + 0.3f), new Vector3(length, ground + 1.17f, z - 0.3f)), 0.02f, _ => oak);
                PlazaWalls.Lay(b, PlazaPieces.Corners(new Vector3(0f, ground + 1.2f, z - 0.28f), new Vector3(length, ground + 2.1f, z - 0.36f)), 0.02f, _ => oak);
                foreach (float x in new[] { 0.1f, length * 0.5f, length - 0.1f })
                {
                    PlazaWalls.Lay(b, PlazaPieces.Corners(new Vector3(x - 0.06f, ground - 0.05f, z + 0.28f), new Vector3(x + 0.06f, ground + 1.05f, z - 0.34f)), 0.015f, _ => dark);
                }
            }
        }

        private static void Lay(MeshBuilder b, float x0, float x1, float y0, float y1, float zFront, float zBack, ArenaStoneKind stone, WorldRandom random)
        {
            PlazaWalls.Lay(b, PlazaPieces.Corners(new Vector3(MathF.Min(x0, x1) + 0.004f, y0 + 0.003f, MathF.Max(zFront, zBack)), new Vector3(MathF.Max(x0, x1) - 0.004f, y1 - 0.003f, MathF.Min(zFront, zBack))), ArenaStone.Chamfer,
                ArenaStone.DressedFacing(stone, random, y0));
        }
    }
}
