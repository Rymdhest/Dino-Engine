using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Churchyard
{
    /// <summary>
    /// What a churchyard has besides its church and its graves: the houses families raised for their dead, the railings round their plots, the lanterns on its paths, its cross, and
    /// its gates. Model y = 0 is where people stand at a piece's foot, +z its front. Headless. Read CHURCHYARD.md.
    /// </summary>
    internal static class YardPieces
    {
        // ---------------------------------------------------------------- Mausoleums

        /// <summary>A mausoleum's half width and half depth, and its height, for its footprint.</summary>
        public static Vector3 MausoleumSize(uint seed, MausoleumForm form)
        {
            var random = new WorldRandom(seed).Fork("mausoleum size");
            return form == MausoleumForm.Classical
                ? new Vector3(random.Range(2.8f, 3.4f), random.Range(6.4f, 7.4f), random.Range(3.4f, 4.2f))
                : new Vector3(random.Range(2.6f, 3.1f), random.Range(8f, 9.4f), random.Range(3.6f, 4.4f));
        }

        public static Mesh Mausoleum(in YardSpec spec, List<AssetLight> lights)
        {
            var b = new MeshBuilder();
            var form = (MausoleumForm)Math.Clamp(spec.Int(0), 0, 1);
            bool opened = spec.Int(1) == 1;
            var random = new WorldRandom(spec.Seed).Fork("mausoleum");
            Vector3 size = MausoleumSize(spec.Seed, form);
            float hx = size.X, hz = size.Z, top = size.Y;
            Vector3 colour = YardStone.Colour(spec, 2, 3) * random.Range(0.86f, 1f);
            Func<Vector3, VertexMaterial> Stone(float y, float moss = 0.4f) => YardStone.Facing(colour, random.Fork((int)(y * 10f) + random.Int(1000)), y, moss);
            VertexMaterial walls = YardStone.Coursed(colour);
            const float Foot = -1.6f;

            // Its base, a step all round, and steps up to its door.
            YardStone.Box(b, new Vector3(-hx - 0.3f, Foot, -hz - 0.3f), new Vector3(hx + 0.3f, 0.55f, hz + 0.3f), 0.05f, Stone(0.3f, 0.5f));
            float door = 1.15f;
            for (int i = 0; i < 2; i++)
            {
                float h = 0.55f - 0.28f * (i + 1);
                YardStone.Box(b, new Vector3(-door - 0.7f - 0.3f * i, Foot, hz + 0.3f), new Vector3(door + 0.7f + 0.3f * i, h + 0.28f, hz + 0.75f + 0.45f * i), 0.04f, Stone(h));
            }
            float body = form == MausoleumForm.Classical ? top - 2.1f : top * 0.52f;
            b.Box(new Vector3(0f, (0.55f + body) * 0.5f, 0f), new Vector3(hx, (body - 0.55f) * 0.5f, hz), walls);

            // The door: an iron grille in front of the dark, broken open if the Ossuary has been in.
            float doorTop = form == MausoleumForm.Classical ? 3.6f : 3.9f;
            Vector3 front = new Vector3(0f, 0f, hz + 0.02f);
            b.Quad(front + new Vector3(-door, 0.55f, 0f), front + new Vector3(door, 0.55f, 0f), front + new Vector3(door, doorTop, 0f), front + new Vector3(-door, doorTop, 0f), YardStone.Dark);
            VertexMaterial iron = YardStone.Rust(random);
            if (!opened) Grille(b, front + new Vector3(0f, 0.55f, 0.04f), door, doorTop - 0.55f, iron, 0f);
            else
            {
                Grille(b, front + new Vector3(-door, 0.55f, 0.04f), door, doorTop - 0.55f, iron, -1.9f, hinge: true);
                // A cold light inside, where something of the Ossuary's was raised.
                b.Quad(front + new Vector3(-door * 0.5f, 0.7f, -0.6f), front + new Vector3(door * 0.5f, 0.7f, -0.6f), front + new Vector3(door * 0.5f, 1.5f, -0.6f), front + new Vector3(-door * 0.5f, 1.5f, -0.6f), YardStone.Glow(0.3f, 1f, 0.82f, 0.5f));
                lights.Add(AssetLight.Glow(new Vector3(0f, 1.4f, hz + 1.2f), new Vector3(0.38f, 1f, 0.8f), 18f, 8f));
            }
            // The jambs and lintel round the door, and its name on a panel over it.
            foreach (float s in new[] { -1f, 1f })
            {
                YardStone.Box(b, new Vector3(s * door - 0.22f, 0.55f, hz - 0.05f), new Vector3(s * door + 0.22f, doorTop, hz + 0.18f), 0.03f, Stone(2f, 0.2f));
            }
            YardStone.Box(b, new Vector3(-door - 0.4f, doorTop, hz - 0.05f), new Vector3(door + 0.4f, doorTop + 0.45f, hz + 0.22f), 0.03f, Stone(doorTop, 0.3f));
            YardStone.Box(b, new Vector3(-door * 0.8f, doorTop + 0.6f, hz), new Vector3(door * 0.8f, doorTop + 1.05f, hz + 0.05f), 0.01f, n => YardStone.Tint(Stone(doorTop)(n), new Vector3(0.8f)));

            if (form == MausoleumForm.Classical) Classical(b, random, colour, hx, hz, body, top, Stone);
            else Gothic(b, random, colour, hx, hz, body, top, Stone);
            return b.Build();
        }

        // A temple front: two columns in front of the door under an entablature and a pediment, urns at its corners, and a low roof of stone behind.
        private static void Classical(MeshBuilder b, WorldRandom random, Vector3 colour, float hx, float hz, float body, float top, Func<float, float, Func<Vector3, VertexMaterial>> stone)
        {
            float portico = hz + 0.9f;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 foot = new Vector3(s * (hx - 0.55f), 0.55f, portico);
                YardStone.Box(b, foot + new Vector3(-0.42f, 0f, -0.42f), foot + new Vector3(0.42f, 0.25f, 0.42f), 0.03f, stone(0.6f, 0.4f));
                b.Tube(foot + new Vector3(0f, 0.25f, 0f), foot + new Vector3(0f, body - 0.8f, 0f), 0.34f, 0.28f, 12, YardStone.Dressed(colour, random, 2f, 0.03f), capped: false);
                b.Tube(foot + new Vector3(0f, body - 0.8f, 0f), foot + new Vector3(0f, body - 0.6f, 0f), 0.3f, 0.42f, 12, YardStone.Dressed(colour, random, body, 0.03f), capped: false);
                YardStone.Box(b, foot + new Vector3(-0.46f, body - 0.6f - 0.55f, -0.46f), foot + new Vector3(0.46f, body - 0.6f - 0.4f, 0.46f), 0.02f, stone(body, 0.3f));
            }
            // The entablature over the columns and the walls, in three bands.
            YardStone.Box(b, new Vector3(-hx - 0.1f, body - 0.6f, -hz - 0.1f), new Vector3(hx + 0.1f, body - 0.05f, portico + 0.5f), 0.03f, stone(body, 0.25f));
            YardStone.Box(b, new Vector3(-hx - 0.25f, body - 0.05f, -hz - 0.25f), new Vector3(hx + 0.25f, body + 0.25f, portico + 0.65f), 0.04f, stone(body, 0.45f));
            // The pediment, with its cornice raking up to an urn at the apex.
            float rise = top - body - 0.4f;
            Vector3 l = new Vector3(-hx - 0.2f, body + 0.25f, portico + 0.5f), r = new Vector3(hx + 0.2f, body + 0.25f, portico + 0.5f), apex = new Vector3(0f, body + 0.25f + rise, portico + 0.5f);
            YardStone.Face(b, l, r, apex, Vector3.UnitZ, YardStone.Coursed(colour, 0.95f));
            YardStone.Face(b, r - Vector3.UnitZ * (hz * 2f + portico - hz + 0.9f), l - Vector3.UnitZ * (hz * 2f + portico - hz + 0.9f), apex - Vector3.UnitZ * (hz * 2f + portico - hz + 0.9f), -Vector3.UnitZ, YardStone.Coursed(colour, 0.95f));
            float back = -hz - 0.4f;
            // Its roof, two slabs of stone from the pediment back.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 eave = new Vector3(s * (hx + 0.35f), body + 0.2f, 0f);
                YardStone.Solid(b, new[]
                {
                    new Vector3(eave.X, eave.Y, portico + 0.62f), new Vector3(0f, apex.Y + 0.05f, portico + 0.62f), new Vector3(0f, apex.Y + 0.25f, portico + 0.62f), new Vector3(eave.X, eave.Y + 0.2f, portico + 0.62f),
                    new Vector3(eave.X, eave.Y, back), new Vector3(0f, apex.Y + 0.05f, back), new Vector3(0f, apex.Y + 0.25f, back), new Vector3(eave.X, eave.Y + 0.2f, back),
                }, 0.04f, stone(top, 0.6f));
            }
            Urn(b, apex + new Vector3(0f, 0.2f, 0.05f), 0.4f, YardStone.Dressed(colour, random, top, 0.02f));
            Urn(b, l + new Vector3(0.3f, 0.05f, 0f), 0.32f, YardStone.Dressed(colour, random, top, 0.02f));
            Urn(b, r + new Vector3(-0.3f, 0.05f, 0f), 0.32f, YardStone.Dressed(colour, random, top, 0.02f));
        }

        // A steep roof of stone slabs over a gabled house, buttresses at its corners with pinnacles, crockets up its copes and a cross at its apex.
        private static void Gothic(MeshBuilder b, WorldRandom random, Vector3 colour, float hx, float hz, float body, float top, Func<float, float, Func<Vector3, VertexMaterial>> stone)
        {
            float rise = top - body - 1.4f;
            foreach (float s in new[] { -1f, 1f })
            {
                YardStone.Solid(b, new[]
                {
                    new Vector3(s * (hx + 0.35f), body - 0.1f, hz + 0.3f), new Vector3(0f, body + rise, hz + 0.3f), new Vector3(0f, body + rise + 0.3f, hz + 0.3f), new Vector3(s * (hx + 0.35f), body + 0.18f, hz + 0.3f),
                    new Vector3(s * (hx + 0.35f), body - 0.1f, -hz - 0.3f), new Vector3(0f, body + rise, -hz - 0.3f), new Vector3(0f, body + rise + 0.3f, -hz - 0.3f), new Vector3(s * (hx + 0.35f), body + 0.18f, -hz - 0.3f),
                }, 0.04f, stone(top, 0.55f));
            }
            foreach (float z in new[] { hz, -hz })
            {
                float outward = MathF.Sign(z);
                YardStone.Face(b, new Vector3(-hx, body, z), new Vector3(hx, body, z), new Vector3(0f, body + rise - 0.1f, z), new Vector3(0f, 0f, outward), YardStone.Coursed(colour));
            }
            // A rose window in the front gable, of stone, its lights dark.
            Vector3 rose = new Vector3(0f, body + rise * 0.38f, hz + 0.06f);
            const int Petals = 8;
            VertexMaterial tracery = YardStone.Dressed(colour, random, body, 0.02f);
            for (int i = 0; i < 14; i++)
            {
                float a0 = i * MathF.Tau / 14f, a1 = (i + 1) * MathF.Tau / 14f;
                b.Tube(rose + new Vector3(MathF.Cos(a0), MathF.Sin(a0), 0f) * 0.75f, rose + new Vector3(MathF.Cos(a1), MathF.Sin(a1), 0f) * 0.75f, 0.09f, 0.09f, 5, tracery, capped: false);
            }
            b.Quad(rose + new Vector3(-0.68f, -0.68f, -0.02f), rose + new Vector3(0.68f, -0.68f, -0.02f), rose + new Vector3(0.68f, 0.68f, -0.02f), rose + new Vector3(-0.68f, 0.68f, -0.02f), YardStone.Dark);
            for (int i = 0; i < Petals; i++)
            {
                float a = i * MathF.Tau / Petals;
                b.Beam(rose, rose + new Vector3(MathF.Cos(a), MathF.Sin(a), 0f) * 0.72f, 0.05f, tracery);
            }
            // Buttresses at the corners, each up to a pinnacle.
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    Vector3 c = new Vector3(sx * hx, 0f, sz * hz);
                    Vector3 d = new Vector3(sx, 0f, sz).Normalized();
                    YardStone.Block(b, c + d * 0.25f + new Vector3(0f, (body + 0.6f) * 0.5f - 0.2f, 0f), new Vector3(0.36f, (body + 0.6f) * 0.5f, 0.36f), MathF.PI * 0.25f, YardStone.Chamfer, stone(body, 0.5f));
                    Vector3 foot = c + d * 0.25f + new Vector3(0f, body + 0.4f, 0f);
                    b.Tube(foot, foot + new Vector3(0f, 1.6f, 0f), 0.3f, 0.02f, 4, YardStone.Dressed(colour, random, body + 1f, 0.03f));
                }
            }
            // A cross on the apex.
            Vector3 apex = new Vector3(0f, body + rise + 0.3f, hz + 0.2f);
            YardStone.Box(b, apex + new Vector3(-0.09f, 0f, -0.09f), apex + new Vector3(0.09f, 1.3f, 0.09f), 0.02f, stone(top, 0.2f));
            YardStone.Box(b, apex + new Vector3(-0.4f, 0.8f, -0.08f), apex + new Vector3(0.4f, 0.98f, 0.08f), 0.02f, stone(top, 0.2f));
        }

        // An urn of stone on a little foot.
        private static void Urn(MeshBuilder b, Vector3 foot, float r, VertexMaterial m)
        {
            b.Tube(foot, foot + new Vector3(0f, 0.18f * r * 2f, 0f), 0.5f * r, 0.36f * r, 10, m);
            b.Tube(foot + new Vector3(0f, 0.36f * r, 0f), foot + new Vector3(0f, 1.1f * r, 0f), 0.6f * r, 0.95f * r, 10, m, capped: false);
            b.Tube(foot + new Vector3(0f, 1.1f * r, 0f), foot + new Vector3(0f, 1.6f * r, 0f), 0.95f * r, 0.42f * r, 10, m);
            b.Tube(foot + new Vector3(0f, 1.6f * r, 0f), foot + new Vector3(0f, 1.95f * r, 0f), 0.2f * r, 0.04f * r, 8, m);
        }

        // An iron grille across a doorway `half` either side of `at`, `height` tall, or one leaf of it hinged at `at` and swung out by `swing` radians.
        private static void Grille(MeshBuilder b, Vector3 at, float half, float height, VertexMaterial iron, float swing, bool hinge = false)
        {
            float width = hinge ? half * 2f : half * 2f;
            Vector3 along = hinge ? new Vector3(MathF.Cos(swing), 0f, -MathF.Sin(swing)) : Vector3.UnitX;
            Vector3 start = hinge ? at : at - Vector3.UnitX * half;
            int bars = Math.Max(3, (int)MathF.Round(width / 0.3f));
            for (int i = 0; i <= bars; i++)
            {
                Vector3 p = start + along * (width * i / bars);
                b.Beam(p, p + Vector3.UnitY * height, 0.035f, iron);
            }
            foreach (float y in new[] { 0.15f, height * 0.5f, height - 0.1f })
            {
                b.Beam(start + Vector3.UnitY * y, start + along * width + Vector3.UnitY * y, 0.04f, iron);
            }
        }

        // ---------------------------------------------------------------- Railings

        /// <summary>
        /// The railing round a family's plot: a kerb of stone, a stone post at each corner with a cap, and between them iron bars with points on top between two rails, in iron left
        /// to rust. Bars are spaced so that each is a few pixels from the game's camera and no finer.
        /// </summary>
        public static Mesh Railing(in YardSpec spec)
        {
            var b = new MeshBuilder();
            var random = new WorldRandom(spec.Seed).Fork("railing");
            float hx = spec.Metres(0) * 0.5f, hz = spec.Metres(1) * 0.5f;
            bool gap = spec.Int(2) == 1;
            float g = YardStone.Ground;
            Func<Vector3, VertexMaterial> stone = YardStone.Weathered(new Vector3(0.5f, 0.49f, 0.46f), YardStone.Quarry.Limestone, random.Range(0.4f, 0.9f), random);
            VertexMaterial iron = YardStone.Rust(random);
            const float Kerb = 0.32f, Height = 1.25f, Spacing = 0.32f;
            Vector3[] corners = { new(-hx, 0f, hz), new(hx, 0f, hz), new(hx, 0f, -hz), new(-hx, 0f, -hz) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = corners[i], c = corners[(i + 1) % 4];
                Vector3 along = (c - a).Normalized();
                float length = (c - a).Length;
                // The +z side has its gap in the middle, where a little gate stands open.
                var runs = new List<(float From, float To)>();
                if (gap && i == 0)
                {
                    runs.Add((0f, length * 0.5f - 0.8f));
                    runs.Add((length * 0.5f + 0.8f, length));
                }
                else runs.Add((0f, length));
                foreach ((float from, float to) in runs)
                {
                    Vector3 p0 = a + along * from, p1 = a + along * to;
                    Vector3 side = Vector3.Cross(Vector3.UnitY, along) * 0.13f;
                    YardStone.Solid(b, new[]
                    {
                        p0 - side + Vector3.UnitY * -0.3f, p1 - side + Vector3.UnitY * -0.3f, p1 - side + Vector3.UnitY * (g + Kerb), p0 - side + Vector3.UnitY * (g + Kerb),
                        p0 + side + Vector3.UnitY * -0.3f, p1 + side + Vector3.UnitY * -0.3f, p1 + side + Vector3.UnitY * (g + Kerb), p0 + side + Vector3.UnitY * (g + Kerb),
                    }, 0.025f, stone);
                    float run = to - from;
                    int bars = Math.Max(1, (int)MathF.Round(run / Spacing));
                    for (int k = 1; k < bars; k++)
                    {
                        Vector3 p = p0 + along * (run * k / bars) + Vector3.UnitY * (g + Kerb);
                        b.Beam(p, p + Vector3.UnitY * Height, 0.026f, iron);
                        b.Tube(p + Vector3.UnitY * Height, p + Vector3.UnitY * (Height + 0.16f), 0.06f, 0.005f, 4, iron);
                    }
                    foreach (float y in new[] { 0.14f, Height - 0.12f })
                    {
                        b.Beam(p0 + Vector3.UnitY * (g + Kerb + y), p1 + Vector3.UnitY * (g + Kerb + y), 0.03f, iron);
                    }
                    if (gap && i == 0 && from > 0f)
                    {
                        // The gate, standing open into the plot.
                        Vector3 hingeAt = p0 + Vector3.UnitY * (g + Kerb);
                        Vector3 swung = (Vector3.Cross(along, Vector3.UnitY) * 0.85f - along * 0.5f).Normalized();
                        for (int k = 0; k <= 4; k++) b.Beam(hingeAt + swung * (1.5f * k / 4f), hingeAt + swung * (1.5f * k / 4f) + Vector3.UnitY * (Height - 0.1f), 0.024f, iron);
                        foreach (float y in new[] { 0.12f, Height - 0.18f }) b.Beam(hingeAt + Vector3.UnitY * y, hingeAt + swung * 1.5f + Vector3.UnitY * y, 0.028f, iron);
                    }
                }
            }
            foreach (Vector3 c in corners)
            {
                YardStone.Box(b, c + new Vector3(-0.22f, -0.3f, -0.22f), c + new Vector3(0.22f, g + Kerb + Height + 0.1f, 0.22f), 0.03f, stone);
                YardStone.Box(b, c + new Vector3(-0.28f, g + Kerb + Height + 0.1f, -0.28f), c + new Vector3(0.28f, g + Kerb + Height + 0.24f, 0.28f), 0.025f, stone);
                b.Tube(c + new Vector3(0f, g + Kerb + Height + 0.24f, 0f), c + new Vector3(0f, g + Kerb + Height + 0.52f, 0f), 0.2f, 0.04f, 4, stone(Vector3.UnitY));
            }
            return b.Build();
        }

        // ---------------------------------------------------------------- Lanterns

        public static Mesh Lamp(in YardSpec spec, List<AssetLight> lights)
        {
            var b = new MeshBuilder();
            var form = (LampForm)Math.Clamp(spec.Int(0), 0, 2);
            float height = MathF.Max(spec.Metres(1), 2.4f);
            var random = new WorldRandom(spec.Seed).Fork("lamp");
            VertexMaterial iron = YardStone.Rust(random, 0.8f);
            float g = YardStone.Ground;
            switch (form)
            {
                case LampForm.Crook:
                {
                    YardStone.Box(b, new Vector3(-0.32f, -0.4f, -0.32f), new Vector3(0.32f, g + 0.3f, 0.32f), 0.03f, YardStone.Weathered(new Vector3(0.46f, 0.45f, 0.42f), YardStone.Quarry.Granite, 0.6f, random));
                    b.Tube(new Vector3(0f, g + 0.3f, 0f), new Vector3(0f, height, 0f), 0.09f, 0.06f, 8, iron);
                    // The crook: up and over, ending in a hook the lantern hangs from.
                    Vector3 last = new Vector3(0f, height, 0f);
                    const int Steps = 6;
                    for (int i = 1; i <= Steps; i++)
                    {
                        float a = i / (float)Steps * MathF.PI;
                        Vector3 here = new Vector3(0f, height + MathF.Sin(a) * 0.38f, 0.38f - MathF.Cos(a) * 0.38f);
                        b.Tube(last, here, 0.05f, 0.05f, 6, iron, capped: false);
                        last = here;
                    }
                    Hung(b, new Vector3(0f, height - 0.1f, 0.76f), 0.55f, iron, lights);
                    break;
                }
                case LampForm.Pillar:
                {
                    Func<Vector3, VertexMaterial> stone = YardStone.Weathered(YardStone.Colour(spec, 2, 3), YardStone.Quarry.Limestone, 0.5f, random);
                    YardStone.Box(b, new Vector3(-0.42f, -0.4f, -0.42f), new Vector3(0.42f, g + 0.35f, 0.42f), 0.035f, stone);
                    YardStone.Box(b, new Vector3(-0.28f, g + 0.35f, -0.28f), new Vector3(0.28f, height - 0.95f, 0.28f), 0.03f, stone);
                    YardStone.Box(b, new Vector3(-0.36f, height - 0.95f, -0.36f), new Vector3(0.36f, height - 0.8f, 0.36f), 0.03f, stone);
                    Box(b, new Vector3(0f, height - 0.8f, 0f), iron, lights);
                    break;
                }
                default:
                {
                    // An old oak post, leaning, a lantern on a hook from it.
                    VertexMaterial oak = YardStone.Silvered(random);
                    Vector3 top = new Vector3(random.Range(-0.15f, 0.15f), height, random.Range(-0.15f, 0.15f));
                    b.Beam(new Vector3(0f, -0.5f, 0f), top, 0.11f, oak);
                    b.Beam(top + new Vector3(0f, -0.2f, 0f), top + new Vector3(0f, -0.1f, 0.55f), 0.03f, iron);
                    Hung(b, top + new Vector3(0f, -0.2f, 0.55f), 0.4f, iron, lights);
                    break;
                }
            }
            return b.Build();
        }

        // A lantern hung on a short chain `drop` below `hook`: a glass of light in a frame of iron, a cap over it and a ring to hang by.
        private static void Hung(MeshBuilder b, Vector3 hook, float drop, VertexMaterial iron, List<AssetLight> lights)
        {
            Vector3 top = hook - Vector3.UnitY * drop;
            b.Beam(hook, top, 0.012f, iron);
            Box(b, top - Vector3.UnitY * 0.62f, iron, lights);
        }

        // A lantern standing on `foot`: its glass glowing, its frame, cap and finial.
        private static void Box(MeshBuilder b, Vector3 foot, VertexMaterial iron, List<AssetLight> lights)
        {
            Vector3 c = foot + Vector3.UnitY * 0.32f;
            b.Box(c, new Vector3(0.15f, 0.22f, 0.15f), YardStone.Glow(1f, 0.6f, 0.26f, 2f));
            b.Tube(foot, foot + Vector3.UnitY * 0.08f, 0.22f, 0.2f, 6, iron);
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    b.Beam(foot + new Vector3(sx * 0.17f, 0.08f, sz * 0.17f), foot + new Vector3(sx * 0.17f, 0.56f, sz * 0.17f), 0.02f, iron);
                }
            }
            b.Tube(foot + Vector3.UnitY * 0.56f, foot + Vector3.UnitY * 0.8f, 0.26f, 0.05f, 6, iron);
            b.Tube(foot + Vector3.UnitY * 0.8f, foot + Vector3.UnitY * 0.9f, 0.04f, 0.04f, 5, iron);
            lights.Add(YardStone.PathLantern(c));
        }

        // ---------------------------------------------------------------- The cross

        /// <summary>
        /// The churchyard's cross, where its paths meet: three steps of a calvary, a socket stone, a tall shaft and a cross on it (0), or a ringed high cross on a tapering shaft (1),
        /// in the church's stone, weathered.
        /// </summary>
        public static Mesh Cross(in YardSpec spec)
        {
            var b = new MeshBuilder();
            int form = spec.Int(0);
            float height = MathF.Max(spec.Metres(1), 5f);
            var random = new WorldRandom(spec.Seed).Fork("cross");
            Vector3 colour = YardStone.Colour(spec, 2, 3);
            Func<Vector3, VertexMaterial> Stone(float y) => YardStone.Facing(colour, random.Fork((int)(y * 7f)), y, 0.5f);
            float y = -0.5f;
            for (int i = 0; i < 3; i++)
            {
                float half = 1.75f - i * 0.5f;
                float to = YardStone.Ground + 0.3f + i * 0.32f;
                YardStone.Block(b, new Vector3(0f, (y + to) * 0.5f, 0f), new Vector3(half, (to - y) * 0.5f, half), MathF.PI * 0.25f * (i % 2) * 0f, YardStone.Chamfer, Stone(to));
                y = to;
            }
            YardStone.Block(b, new Vector3(0f, y + 0.4f, 0f), new Vector3(0.48f, 0.4f, 0.48f), 0f, YardStone.Chamfer, Stone(y + 0.4f));
            float from = y + 0.8f, crossAt = height - (form == 1 ? 1.1f : 0.95f);
            YardStone.Solid(b, new[]
            {
                new Vector3(-0.26f, from, 0.22f), new Vector3(0.26f, from, 0.22f), new Vector3(0.16f, crossAt, 0.14f), new Vector3(-0.16f, crossAt, 0.14f),
                new Vector3(-0.26f, from, -0.22f), new Vector3(0.26f, from, -0.22f), new Vector3(0.16f, crossAt, -0.14f), new Vector3(-0.16f, crossAt, -0.14f),
            }, 0.025f, Stone(height * 0.5f));
            YardStone.Box(b, new Vector3(-0.16f, crossAt - 0.05f, -0.14f), new Vector3(0.16f, height, 0.14f), 0.025f, Stone(height));
            float arm = form == 1 ? 0.95f : 0.8f;
            YardStone.Box(b, new Vector3(-arm, crossAt + 0.12f, -0.13f), new Vector3(arm, crossAt + 0.44f, 0.13f), 0.025f, Stone(height));
            if (form == 1)
            {
                VertexMaterial m = YardStone.Dressed(colour, random, height, 0.03f);
                const int Segments = 14;
                float r = 0.62f;
                Vector3 centre = new Vector3(0f, crossAt + 0.28f, 0f);
                for (int i = 0; i < Segments; i++)
                {
                    float a0 = i * MathF.Tau / Segments, a1 = (i + 1) * MathF.Tau / Segments;
                    b.Tube(centre + new Vector3(MathF.Cos(a0), MathF.Sin(a0), 0f) * r, centre + new Vector3(MathF.Cos(a1), MathF.Sin(a1), 0f) * r, 0.09f, 0.09f, 6, m, capped: false);
                }
            }
            return b.Build();
        }

        // ---------------------------------------------------------------- Gates

        /// <summary>How far a gate's solid parts reach either side of the way through it, and how deep they are: what the wall is drawn into.</summary>
        public static Vector2 GateReach(GateForm form, float width) => form == GateForm.Lychgate ? new Vector2(width * 0.5f + 1.48f, 2.18f) : new Vector2(width * 0.5f + 1.3f, 0.72f);

        /// <summary>How wide a pier or a lychgate's side wall is, which is what the churchyard's wall is drawn into either side of a gate.</summary>
        public static float GateSide(GateForm form) => form == GateForm.Lychgate ? 1.48f : 1.4f;

        public static Mesh Gate(in YardSpec spec, List<AssetLight> lights)
        {
            var b = new MeshBuilder();
            var form = (GateForm)Math.Clamp(spec.Int(0), 0, 1);
            float half = spec.Metres(1) * 0.5f;
            var random = new WorldRandom(spec.Seed).Fork("gate");
            Vector3 colour = YardStone.Colour(spec, 2, 3);
            Func<Vector3, VertexMaterial> Stone(float y, float moss = 0.4f) => YardStone.Facing(colour, random.Fork((int)(y * 13f) + random.Int(500)), y, moss);
            if (form == GateForm.Lychgate) Lychgate(b, half, random, colour, Stone, lights);
            else Piers(b, half, random, colour, Stone, lights);
            return b.Build();
        }

        // Two piers in banded stone with a cornice and a ball on each, a pair of iron gates standing open into the churchyard (towards -z), and an iron overthrow between the piers'
        // tops with a lantern hung from it.
        private static void Piers(MeshBuilder b, float half, WorldRandom random, Vector3 colour, Func<float, float, Func<Vector3, VertexMaterial>> stone, List<AssetLight> lights)
        {
            const float Pier = 0.6f, Height = 3.9f;
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (half + Pier);
                YardStone.Box(b, new Vector3(x - Pier - 0.1f, -0.8f, -Pier - 0.1f), new Vector3(x + Pier + 0.1f, 0.7f, Pier + 0.1f), 0.04f, stone(0.4f, 0.5f));
                float y = 0.7f;
                int k = 0;
                while (y < Height - 0.1f)
                {
                    float h = MathF.Min(0.55f, Height - y);
                    float inset = k % 2 == 0 ? 0f : 0.05f;
                    YardStone.Box(b, new Vector3(x - Pier + inset, y + 0.005f, -Pier + inset), new Vector3(x + Pier - inset, y + h - 0.005f, Pier - inset), 0.035f, stone(y, 0.2f));
                    y += h;
                    k++;
                }
                YardStone.Box(b, new Vector3(x - Pier - 0.14f, Height, -Pier - 0.14f), new Vector3(x + Pier + 0.14f, Height + 0.24f, Pier + 0.14f), 0.04f, stone(Height, 0.55f));
                YardStone.Box(b, new Vector3(x - Pier + 0.08f, Height + 0.24f, -Pier + 0.08f), new Vector3(x + Pier - 0.08f, Height + 0.4f, Pier - 0.08f), 0.03f, stone(Height, 0.5f));
                VertexMaterial ball = YardStone.Dressed(colour, random, Height + 1f, 0.03f);
                b.Tube(new Vector3(x, Height + 0.4f, 0f), new Vector3(x, Height + 0.55f, 0f), 0.2f, 0.14f, 8, ball);
                Ball(b, new Vector3(x, Height + 0.95f, 0f), 0.42f, ball);
            }
            // The gates, each a leaf of bars with a dog bar along its foot, swung open into the churchyard.
            VertexMaterial iron = YardStone.Rust(random);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 hinge = new Vector3(s * half, YardStone.Ground + 0.1f, -0.1f);
                float swing = random.Range(1.1f, 1.45f);
                Vector3 along = new Vector3(-s * MathF.Cos(swing), 0f, -MathF.Sin(swing));
                int bars = Math.Max(4, (int)MathF.Round(half / 0.3f));
                for (int i = 0; i <= bars; i++)
                {
                    float t = i / (float)bars;
                    float tall = 2.6f + 0.35f * MathF.Sin(t * MathF.PI * 0.5f + (s > 0 ? 0f : 0f));
                    Vector3 p = hinge + along * (half * t);
                    b.Beam(p, p + Vector3.UnitY * tall, 0.028f, iron);
                    b.Tube(p + Vector3.UnitY * tall, p + Vector3.UnitY * (tall + 0.2f), 0.065f, 0.005f, 4, iron);
                }
                foreach (float y in new[] { 0.12f, 0.5f, 2.35f }) b.Beam(hinge + Vector3.UnitY * y, hinge + along * half + Vector3.UnitY * y, 0.035f, iron);
                // A scroll in the leaf, a ring of iron between its top rails.
                Vector3 scroll = hinge + along * (half * 0.5f) + Vector3.UnitY * 1.5f;
                for (int i = 0; i < 10; i++)
                {
                    float a0 = i * MathF.Tau / 10f, a1 = (i + 1) * MathF.Tau / 10f;
                    b.Beam(scroll + along * MathF.Cos(a0) * 0.32f + Vector3.UnitY * MathF.Sin(a0) * 0.32f, scroll + along * MathF.Cos(a1) * 0.32f + Vector3.UnitY * MathF.Sin(a1) * 0.32f, 0.022f, iron);
                }
            }
            // The overthrow: an arch of iron between the piers' tops, scrolls in its spandrels, and the lantern hung from its crown.
            Vector3 last = new Vector3(-half - 0.1f, Height + 0.3f, 0f);
            const int Steps = 10;
            float crown = Height + 0.3f + MathF.Min(half * 0.55f, 1.6f);
            for (int i = 1; i <= Steps; i++)
            {
                float t = i / (float)Steps;
                Vector3 here = new Vector3(-half - 0.1f + (half + 0.1f) * 2f * t, Height + 0.3f + MathF.Sin(t * MathF.PI) * (crown - Height - 0.3f), 0f);
                b.Tube(last, here, 0.045f, 0.045f, 6, iron, capped: false);
                last = here;
            }
            b.Beam(new Vector3(-half - 0.1f, Height + 0.25f, 0f), new Vector3(half + 0.1f, Height + 0.25f, 0f), 0.035f, iron);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 c = new Vector3(s * half * 0.5f, Height + 0.3f + (crown - Height - 0.3f) * 0.45f, 0f);
                for (int i = 0; i < 9; i++)
                {
                    float a0 = i * MathF.Tau / 9f, a1 = (i + 1) * MathF.Tau / 9f;
                    float r0 = 0.12f + 0.025f * i, r1 = 0.12f + 0.025f * (i + 1);
                    b.Beam(c + new Vector3(MathF.Cos(a0) * r0 * s, MathF.Sin(a0) * r0, 0f), c + new Vector3(MathF.Cos(a1) * r1 * s, MathF.Sin(a1) * r1, 0f), 0.02f, iron);
                }
            }
            Hung(b, new Vector3(0f, crown, 0f), 0.35f, iron, lights);
        }

        // A ball of stone.
        private static void Ball(MeshBuilder b, Vector3 centre, float r, VertexMaterial m)
        {
            const int Rings = 5;
            for (int i = 0; i < Rings; i++)
            {
                float a0 = -MathF.PI * 0.5f + MathF.PI * i / Rings, a1 = -MathF.PI * 0.5f + MathF.PI * (i + 1) / Rings;
                b.Tube(centre + Vector3.UnitY * MathF.Sin(a0) * r, centre + Vector3.UnitY * MathF.Sin(a1) * r, MathF.Max(MathF.Cos(a0) * r, 0.01f), MathF.Max(MathF.Cos(a1) * r, 0.01f), 10, m, capped: false);
            }
        }

        // A lychgate: low walls of stone either side of the way, oak posts on them carrying plates and tie beams with braces, a steep roof over all of it with bargeboards at its
        // gables and a cross on the one towards the road, and a bench along each wall inside, where the dead were set down.
        private static void Lychgate(MeshBuilder b, float half, WorldRandom random, Vector3 colour, Func<float, float, Func<Vector3, VertexMaterial>> stone, List<AssetLight> lights)
        {
            const float Wall = 1.4f, WallTop = 1.15f, Depth = 2.1f, Post = 0.17f, Plate = 3.8f;
            float outer = half + Wall;
            VertexMaterial oak = YardStone.Oak(random, 1.1f), dark = YardStone.Oak(random, 0.8f);
            foreach (float s in new[] { -1f, 1f })
            {
                float x0 = s * half, x1 = s * outer;
                YardStone.Box(b, new Vector3(MathF.Min(x0, x1), -0.8f, -Depth), new Vector3(MathF.Max(x0, x1), WallTop, Depth), 0.04f, stone(0.6f, 0.4f));
                YardStone.Box(b, new Vector3(MathF.Min(x0, x1) - 0.08f, WallTop, -Depth - 0.08f), new Vector3(MathF.Max(x0, x1) + 0.08f, WallTop + 0.16f, Depth + 0.08f), 0.03f, stone(WallTop, 0.55f));
                // The posts, on the inner edge of each wall.
                float px = s * (half + 0.3f);
                foreach (float z in new[] { -Depth + 0.3f, Depth - 0.3f })
                {
                    YardStone.Box(b, new Vector3(px - Post, WallTop + 0.16f, z - Post), new Vector3(px + Post, Plate, z + Post), 0.02f, _ => oak);
                    b.Beam(new Vector3(px, Plate - 0.9f, z), new Vector3(px - s * 0.9f, Plate - 0.08f, z), 0.08f, dark);
                }
                // The wall plate along the top of the posts, and the bench inside.
                YardStone.Box(b, new Vector3(px - 0.2f, Plate, -Depth - 0.3f), new Vector3(px + 0.2f, Plate + 0.28f, Depth + 0.3f), 0.02f, _ => oak);
                YardStone.Box(b, new Vector3(px - s * 0.05f - 0.25f, 0.95f, -Depth + 0.6f), new Vector3(px - s * 0.05f + 0.25f, 1.05f, Depth - 0.6f), 0.015f, _ => YardStone.Silvered(random));
            }
            // Tie beams across the way at each end, and the roof.
            foreach (float z in new[] { -Depth + 0.3f, Depth - 0.3f })
            {
                YardStone.Box(b, new Vector3(-half - 0.5f, Plate + 0.05f, z - 0.16f), new Vector3(half + 0.5f, Plate + 0.35f, z + 0.16f), 0.02f, _ => oak);
                b.Beam(new Vector3(0f, Plate + 0.35f, z), new Vector3(0f, Plate + 1.7f, z), 0.1f, dark);
            }
            float reach = half + 1.0f, rise = reach * 1.15f, eave = Plate + 0.3f, roofFrom = -Depth - 0.6f, roofTo = Depth + 0.6f;
            VertexMaterial slate = YardStone.Slate(random);
            foreach (float s in new[] { -1f, 1f })
            {
                YardStone.Solid(b, new[]
                {
                    new Vector3(s * reach, eave, roofTo), new Vector3(0f, eave + rise, roofTo), new Vector3(0f, eave + rise + 0.2f, roofTo), new Vector3(s * reach, eave + 0.2f, roofTo),
                    new Vector3(s * reach, eave, roofFrom), new Vector3(0f, eave + rise, roofFrom), new Vector3(0f, eave + rise + 0.2f, roofFrom), new Vector3(s * reach, eave + 0.2f, roofFrom),
                }, 0.02f, _ => slate);
                // Bargeboards along each gable's rake.
                foreach (float z in new[] { roofFrom - 0.03f, roofTo + 0.03f })
                {
                    b.Beam(new Vector3(s * (reach + 0.05f), eave - 0.05f, z), new Vector3(0f, eave + rise + 0.05f, z), 0.1f, dark);
                }
            }
            b.Tube(new Vector3(0f, eave + rise + 0.25f, roofFrom - 0.1f), new Vector3(0f, eave + rise + 0.25f, roofTo + 0.1f), 0.12f, 0.12f, 6, YardStone.Lead);
            // The gables, boarded.
            foreach (float z in new[] { -Depth + 0.25f, Depth - 0.25f })
            {
                YardStone.Face(b, new Vector3(-reach + 0.6f, eave + 0.3f, z), new Vector3(reach - 0.6f, eave + 0.3f, z), new Vector3(0f, eave + rise - 0.05f, z), new Vector3(0f, 0f, MathF.Sign(z)), dark);
                YardStone.Face(b, new Vector3(reach - 0.6f, eave + 0.3f, z), new Vector3(-reach + 0.6f, eave + 0.3f, z), new Vector3(0f, eave + rise - 0.05f, z), new Vector3(0f, 0f, -MathF.Sign(z)), dark);
            }
            // A cross on the gable towards the road, and a lantern hung under the tie beam inside.
            Vector3 apex = new Vector3(0f, eave + rise + 0.3f, roofTo - 0.1f);
            b.Beam(apex, apex + Vector3.UnitY * 1.2f, 0.07f, dark);
            b.Beam(apex + new Vector3(-0.4f, 0.8f, 0f), apex + new Vector3(0.4f, 0.8f, 0f), 0.06f, dark);
            Hung(b, new Vector3(0f, Plate + 0.05f, 0f), 0.3f, YardStone.Rust(random), lights);
        }
    }
}
