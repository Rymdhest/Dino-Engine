using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Fence
{
    /// <summary>
    /// Walls. A wall's spans meet with nothing between them: they're built to the same section at both ends, so they join wherever they're put, and what's worked out along
    /// one (its weathering, its texture's courses) comes round again at its end. Its posts are only where a run stops or turns: a pier, a cheek of big stones, a turret.
    /// </summary>
    internal static class StoneFences
    {
        private const float CellSize = 0.55f;       // Metres to a cell of a wall's face, which carries its weathering.

        // A wall's dressed stone, the colour the church and the keep are, so that a churchyard's wall is of a piece with its church.
        private static Vector3 Dressed(Genes style) => new Vector3(0.66f, 0.655f, 0.63f) * style.Range("stone shade", 0.9f, 1.06f) * Vector3.Lerp(Vector3.One, new Vector3(1.04f, 1f, 0.94f), style.Unit("warmth"));

        // A texture repeat that fits a span a whole number of times, so the courses of one span run on into the next.
        private static float TileFor(float bay, float wanted) => bay / MathF.Max(1f, MathF.Round(bay / wanted));

        private static int Cells(float length) => Math.Max(1, (int)MathF.Round(length / CellSize));

        /// <summary>
        /// Field stones laid dry, in courses that shrink towards the top, the wall narrowing as it rises, and a row of copes stood on edge along the top, all leaning the
        /// one way. Its stones are the world's own rock, drawn as the mountains are, so a wall matches the boulders it was cleared from.
        /// </summary>
        public static void DryStone(in FenceJob job)
        {
            Genes style = job.Style;
            FenceBuilder b = job.B;
            float height = style.Range("height", 2.2f, 2.6f);
            float baseHalf = job.Spec.Kind.HalfThickness * style.Range("base", 0.95f, 1f);
            float topHalf = style.Range("top", 0.36f, 0.44f);
            float course = style.Range("course", 0.32f, 0.4f);
            float shortest = style.Range("shortest", 0.6f, 0.75f), longest = style.Range("longest", 1.1f, 1.4f);
            float copeHeight = style.Range("cope height", 0.4f, 0.55f), copeThick = style.Range("cope thickness", 0.17f, 0.27f), copeLean = style.Range("cope lean", -0.18f, 0.18f);
            var stone = new Surface(FenceMaterials.Stone, new Vector3(0.94f) * style.Range("stone shade", 0.9f, 1.04f), 2f);
            float batter = MathF.Atan2(baseHalf - topHalf, height);
            float HalfAt(float y) => MathHelper.Lerp(baseHalf, topHalf, Math.Clamp(y / height, 0f, 1f));
            Vector3 Lichen(Vector3 at, Vector3 normal) => normal.Y > 0.55f ? stone.Colour * new Vector3(0.9f, 0.97f, 0.8f) : stone.Colour;

            WorldRandom random = job.Own.Stream("stones");
            if (!job.IsSpan)
            {
                // A cheek: big squared stones, a tie across the whole wall and then a pair, in turn, and a broad cope over the top.
                float reach = random.Range(0.42f, 0.5f);
                bool tie = random.Chance(0.5f);
                for (float y = -0.25f; y < height - 0.05f; tie = !tie)
                {
                    float ch = MathF.Min(course * random.Range(1.05f, 1.35f), height - y);
                    float half = HalfAt(y + ch * 0.5f) + 0.04f;
                    if (tie)
                    {
                        b.Stone(new Vector3(random.Range(-0.03f, 0.03f), y + ch * 0.5f, 0f), new Vector3(reach, ch * 0.5f, half), Turn(random, 0.04f), 0.35f, stone.Tinted(random.Range(0.92f, 1.06f)), random, colourAt: Lichen);
                    }
                    else
                    {
                        foreach (float side in new[] { -1f, 1f })
                        {
                            b.Stone(new Vector3(random.Range(-0.04f, 0.04f), y + ch * 0.5f, side * half * 0.5f), new Vector3(reach * random.Range(0.92f, 1.04f), ch * 0.5f, half * 0.52f), Turn(random, 0.04f), 0.4f, stone, random, colourAt: Lichen);
                        }
                    }
                    y += ch;
                }
                float capHeight = copeHeight * 0.8f;
                b.Stone(new Vector3(0f, height + capHeight * 0.5f - 0.04f, 0f), new Vector3(reach + 0.06f, capHeight * 0.5f, topHalf + 0.1f), Turn(random, 0.05f), 0.4f, stone, random, Faces.NegativeY, Lichen);
                return;
            }

            // Hearting: what fills the middle, seen dark in the gaps between stones.
            Vector2[] core = { new(-(baseHalf - 0.14f), -job.Sink), new(baseHalf - 0.14f, -job.Sink), new(topHalf - 0.08f, height), new(-(topHalf - 0.08f), height) };
            b.Prism(new Vector3(0f, 0f, 0f), new Vector3(job.Bay, 0f, 0f), Vector3.UnitY, core, stone.Tinted(0.4f), new[] { 0f, 1f }, capFoot: false, capHead: false);

            // Face stones on both sides, course by course, each running a little past the span's ends so that two spans' stones lock together where they meet.
            float y0 = -0.25f;
            while (y0 < height - 0.05f)
            {
                float ch = course * MathHelper.Lerp(1.05f, 0.72f, Math.Clamp(y0 / height, 0f, 1f)) * random.Range(0.85f, 1.15f);
                if (height - (y0 + ch) < 0.12f) ch = height - y0;
                foreach (float side in new[] { -1f, 1f })
                {
                    float x = -random.Range(0f, 0.28f);
                    while (x < job.Bay + 0.05f)
                    {
                        float length = random.Range(shortest, longest) * MathHelper.Lerp(1f, 0.75f, Math.Clamp(y0 / height, 0f, 1f));
                        if (x + length > job.Bay + 0.3f) length = job.Bay + random.Range(0f, 0.25f) - x;
                        if (length < 0.25f) break;
                        float cx = x + length * 0.5f, cy = y0 + ch * 0.5f;
                        float depth = random.Range(0.32f, 0.46f);
                        float cz = side * (HalfAt(cy) - depth * 0.5f + random.Range(-0.015f, 0.03f));
                        Matrix3 turn = Matrix3.CreateRotationX(-side * batter) * Turn(random, 0.06f);
                        int mark = b.Mark;
                        // What's against the hearting and against the stones all round can't be seen, but for the dark of the joints between them.
                        Faces hidden = (side < 0f ? Faces.PositiveZ : Faces.NegativeZ) | Faces.NegativeY | Faces.PositiveY | Faces.NegativeX | Faces.PositiveX;
                        b.Stone(new Vector3(cx, cy, cz), new Vector3(length * 0.49f, ch * 0.48f, depth * 0.5f), turn, random.Range(0.4f, 0.75f), stone, random, hidden, Lichen);
                        b.Upright(mark, cx);
                        x += length + random.Range(0f, 0.03f);
                    }
                }
                y0 += ch;
            }

            // Now and then a through stone, which binds both faces and shows on each.
            if (random.Chance(0.6f))
            {
                float y = height * random.Range(0.35f, 0.6f);
                float x = random.Range(0.6f, job.Bay - 0.6f);
                int mark = b.Mark;
                b.Stone(new Vector3(x, y, 0f), new Vector3(random.Range(0.22f, 0.32f), course * 0.42f, HalfAt(y) + 0.1f), Turn(random, 0.05f), 0.5f, stone, random, colourAt: Lichen);
                b.Upright(mark, x);
            }

            // Copes on edge along the top, leaning the way the waller leant them.
            for (float x = -random.Range(0f, 0.1f); x < job.Bay;)
            {
                float t = copeThick * random.Range(0.8f, 1.25f);
                float h = copeHeight * random.Range(0.85f, 1.15f);
                Matrix3 turn = Matrix3.CreateRotationZ(copeLean + random.Range(-0.07f, 0.07f)) * Matrix3.CreateRotationY(random.Range(-0.05f, 0.05f));
                int mark = b.Mark;
                b.Stone(new Vector3(x + t * 0.5f, height + h * 0.5f - 0.06f, random.Range(-0.02f, 0.02f)), new Vector3(t * 0.47f, h * 0.5f, topHalf + 0.06f), turn, 0.35f, stone, random, Faces.NegativeY, Lichen);
                b.Upright(mark, x + t * 0.5f);
                x += t;
            }
        }

        private static Matrix3 Turn(WorldRandom random, float most)
        {
            return Matrix3.CreateRotationY(random.Range(-most, most)) * Matrix3.CreateRotationZ(random.Range(-most, most)) * Matrix3.CreateRotationX(random.Range(-most, most) * 0.5f);
        }

        /// <summary>
        /// A churchyard's wall: coursed and mortared on a plinth with a sloped top, under a saddleback coping laid in blocks, and at its ends and corners piers with a cornice and
        /// a cap, a pyramid, a ball or a step. It's the church's own stone.
        /// </summary>
        public static void Churchyard(in FenceJob job)
        {
            Genes style = job.Style;
            FenceBuilder b = job.B;
            float height = style.Range("height", 2.55f, 2.95f);
            const float BodyHalf = 0.6f, PlinthHalf = 0.76f, CopeHalf = 0.74f;
            float plinth = style.Range("plinth", 0.42f, 0.6f);
            float eave = style.Range("eave", 0.12f, 0.18f), rise = style.Range("rise", 0.2f, 0.32f);
            float blockWanted = style.Range("cope block", 0.8f, 1.1f);
            float tile = TileFor(job.Bay, style.Range("ashlar", 1.05f, 1.3f) * 4f);
            int cap = style.Pick("pier cap", 0.45f, 0.3f, 0.25f);
            Vector3 colour = Dressed(style);
            var masonry = new Surface(FenceMaterials.Masonry, colour, tile);
            uint seed = (uint)job.Style.Stream("weathering").Int(int.MaxValue);

            if (!job.IsSpan)
            {
                float half = style.Range("pier", 0.84f, 0.94f);
                float top = height + style.Range("pier rise", 0.45f, 0.7f);
                Pier(b, half, top, plinth, job.Sink, cap, masonry, colour, seed);
                return;
            }

            float bay = job.Bay;
            Vector3 Weather(Vector3 at) => FenceLook.Weathering(at, colour, bay, height, seed);
            Plinth(b, bay, plinth, PlinthHalf, BodyHalf, job.Sink, masonry.Tinted(0.84f), at => Weather(at) * 0.84f);
            foreach (float side in new[] { -1f, 1f })
            {
                b.Panel(new Vector3(0f, plinth + 0.05f, side * BodyHalf), new Vector3(bay, 0f, 0f), new Vector3(0f, height - plinth - 0.05f, 0f), Cells(bay), Cells(height - plinth), new Vector3(0f, 0f, side), masonry, Weather, new Vector2(0f, (plinth + 0.05f) / tile));
            }
            b.Panel(new Vector3(0f, height, -BodyHalf), new Vector3(bay, 0f, 0f), new Vector3(0f, 0f, BodyHalf * 2f), 1, 1, Vector3.UnitY, masonry.Tinted(0.5f));
            Ends(b, bay, masonry.Tinted(Joint), new Vector2[]
            {
                new(-PlinthHalf, -job.Sink), new(PlinthHalf, -job.Sink), new(PlinthHalf, plinth - 0.1f), new(BodyHalf, plinth + 0.05f),
                new(BodyHalf, height), new(-BodyHalf, height), new(-BodyHalf, plinth + 0.05f), new(-PlinthHalf, plinth - 0.1f),
            });

            // The coping, in blocks with a hairline between each.
            WorldRandom blocks = job.Own.Stream("coping");
            int count = Math.Max(1, (int)MathF.Round(bay / blockWanted));
            float length = bay / count;
            Vector2[] saddle = { new(-CopeHalf, 0f), new(CopeHalf, 0f), new(CopeHalf, eave), new(0f, eave + rise), new(-CopeHalf, eave) };
            for (int k = 0; k < count; k++)
            {
                Surface block = masonry.Tinted(blocks.Range(0.86f, 1.02f));
                b.Prism(new Vector3(k * length + 0.008f, height, 0f), new Vector3((k + 1) * length - 0.008f, height, 0f), Vector3.UnitY, saddle, block, new[] { 0f, 1f }, endColour: block.Colour * 0.92f);
            }
        }

        // A span's ends, across its section, which two spans on different slopes would otherwise show daylight between: where they meet, it's a joint.
        private const float Joint = 0.62f;

        private static void Ends(FenceBuilder b, float bay, Surface surface, ReadOnlySpan<Vector2> outline)
        {
            Vector2 middle = Vector2.Zero;
            foreach (Vector2 p in outline) middle += p;
            middle /= outline.Length;
            foreach (float x in new[] { 0f, bay })
            {
                Vector3 inside = new Vector3(x == 0f ? 1f : bay - 1f, middle.Y, middle.X);
                Vector3 centre = new Vector3(x, middle.Y, middle.X);
                for (int k = 0; k < outline.Length; k++)
                {
                    Vector2 a = outline[k], c = outline[(k + 1) % outline.Length];
                    b.Triangle(centre, new Vector3(x, a.Y, a.X), new Vector3(x, c.Y, c.X), inside, surface);
                }
            }
        }

        // A plinth along a span: a face down into the ground on each side, and a sloped top back to the wall, which sheds the rain off it.
        private static void Plinth(FenceBuilder b, float bay, float height, float half, float wallHalf, float sink, Surface surface, Func<Vector3, Vector3> colour)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                b.Panel(new Vector3(0f, -sink, side * half), new Vector3(bay, 0f, 0f), new Vector3(0f, height - 0.1f + sink, 0f), Cells(bay), 2, new Vector3(0f, 0f, side), surface, colour, new Vector2(0f, -sink / surface.Tile));
                b.Panel(new Vector3(0f, height - 0.1f, side * half), new Vector3(bay, 0f, 0f), new Vector3(0f, 0.15f, -side * (half - wallHalf)), Cells(bay), 1, new Vector3(0f, 1f, side), surface, colour);
            }
        }

        // A square pier: a plinth, a shaft of coursed stone, a cornice, and a cap.
        private static void Pier(FenceBuilder b, float half, float top, float plinth, float sink, int cap, Surface masonry, Vector3 colour, uint seed)
        {
            float tile = masonry.Tile;
            Vector3 Weather(Vector3 at) => FenceLook.Weathering(at, colour, half * 8f, top, seed);
            b.Block(new Vector3(0f, (plinth - sink) * 0.5f, 0f), new Vector3(half + 0.08f, (plinth + sink) * 0.5f, half + 0.08f), Matrix3.Identity, 0.04f, masonry.Tinted(0.84f), Faces.NegativeY);
            for (int side = 0; side < 4; side++)
            {
                float angle = side * MathF.PI * 0.5f;
                Vector3 normal = new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle));
                Vector3 along = new Vector3(normal.Z, 0f, -normal.X);
                b.Panel(normal * half - along * half + new Vector3(0f, plinth, 0f), along * (half * 2f), new Vector3(0f, top - plinth, 0f), 2, Cells(top - plinth), normal, masonry, Weather, new Vector2(0f, plinth / tile));
            }

            float corniceHalf = half + 0.08f;
            b.Block(new Vector3(0f, top + 0.09f, 0f), new Vector3(corniceHalf, 0.11f, corniceHalf), Matrix3.Identity, 0.035f, masonry.Tinted(0.95f));
            float base0 = top + 0.2f;
            Surface capStone = masonry.Tinted(0.92f);
            switch (cap)
            {
                case 0:
                {
                    Vector3 apex = new Vector3(0f, base0 + half * 0.75f, 0f);
                    Vector3[] rim = { new(-half, base0, -half), new(half, base0, -half), new(half, base0, half), new(-half, base0, half) };
                    for (int i = 0; i < 4; i++) b.Triangle(rim[i], rim[(i + 1) % 4], apex, new Vector3(0f, base0, 0f), capStone);
                    break;
                }
                case 1:
                    b.Block(new Vector3(0f, base0 + 0.12f, 0f), new Vector3(half * 0.55f, 0.12f, half * 0.55f), Matrix3.Identity, 0.03f, capStone);
                    b.Ball(new Vector3(0f, base0 + 0.24f + half * 0.46f, 0f), half * 0.48f, 10, 6, capStone);
                    break;
                default:
                    b.Block(new Vector3(0f, base0 + 0.1f, 0f), new Vector3(half * 0.86f, 0.1f, half * 0.86f), Matrix3.Identity, 0.03f, capStone);
                    b.Block(new Vector3(0f, base0 + 0.28f, 0f), new Vector3(half * 0.62f, 0.08f, half * 0.62f), Matrix3.Identity, 0.03f, capStone);
                    break;
            }
        }

        /// <summary>
        /// Holy ground's wall in the East: plaster over a stone plinth, framed by dark posts, sometimes earthen with white lines along it, under a little roof of its own with a
        /// ridge. Its ends and corners are a pier under a hipped cap.
        /// </summary>
        public static void Roofed(in FenceJob job)
        {
            Genes style = job.Style;
            FenceBuilder b = job.B;
            const float PlinthHalf = 0.74f, BodyHalf = 0.56f;
            float plinth = style.Range("plinth", 0.55f, 0.8f);
            float bodyTop = style.Range("height", 2.9f, 3.3f);
            bool earthen = style.Chance("earthen", 0.45f);
            int lines = earthen && style.Chance("lines", 0.6f) ? 3 + style.Pick("line count", 0.5f, 0.3f, 0.2f) : 0;
            Vector3 plaster = earthen ? new Vector3(0.7f, 0.56f, 0.38f) * style.Range("earth shade", 0.9f, 1.06f) : new Vector3(0.93f, 0.9f, 0.84f) * style.Range("lime shade", 0.94f, 1.02f);
            Vector3 timber = new Vector3(0.34f, 0.25f, 0.19f) * style.Range("timber shade", 0.85f, 1.1f);
            float eave = style.Range("eave", 0.32f, 0.44f), rise = style.Range("rise", 0.5f, 0.7f);
            float postPitch = style.Range("post pitch", 2.1f, 2.7f);
            float roofBase = bodyTop + 0.12f, roofHalf = BodyHalf + eave;
            var stone = new Surface(FenceMaterials.Masonry, new Vector3(0.59f, 0.56f, 0.53f) * style.Range("plinth shade", 0.9f, 1.06f), TileFor(job.Bay, 2.4f));
            var wall = new Surface(FenceMaterials.Plaster, plaster, 2.2f);
            var wood = new Surface(FenceMaterials.Timber, timber, 1.6f);
            var roof = new Surface(FenceMaterials.Roof, new Vector3(0.62f, 0.64f, 0.7f) * style.Range("roof shade", 0.86f, 1.08f), 1.6f);
            var ridge = new Surface(FenceMaterials.Roof, new Vector3(0.35f, 0.36f, 0.4f), 1.6f);
            uint seed = (uint)job.Style.Stream("weathering").Int(int.MaxValue);

            if (!job.IsSpan)
            {
                const float Half = 0.66f;
                b.Block(new Vector3(0f, (plinth - job.Sink) * 0.5f, 0f), new Vector3(PlinthHalf + 0.04f, (plinth + job.Sink) * 0.5f, PlinthHalf + 0.04f), Matrix3.Identity, 0.03f, stone, Faces.NegativeY);
                b.Block(new Vector3(0f, (plinth + bodyTop) * 0.5f, 0f), new Vector3(Half, (bodyTop - plinth) * 0.5f, Half), Matrix3.Identity, 0.02f, wall, Faces.NegativeY | Faces.PositiveY);
                foreach (float sx in new[] { -1f, 1f })
                {
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        b.Block(new Vector3(sx * (Half - 0.06f), (plinth + bodyTop) * 0.5f, sz * (Half - 0.06f)), new Vector3(0.09f, (bodyTop - plinth) * 0.5f, 0.09f), Matrix3.Identity, 0.01f, wood, Faces.NegativeY);
                    }
                }
                HipCap(b, roofHalf + 0.06f, roofBase + 0.03f, rise + 0.16f, roof, ridge, wood);
                return;
            }

            float bay = job.Bay;
            Vector3 Weather(Vector3 at) => plaster * (0.93f + 0.1f * FenceLook.AlongWall(at.X, at.Y, at.Z, bay, 0.9f, seed)) * (1f - 0.12f * FenceBuilder.Smoothstep(bodyTop - 0.6f, bodyTop, at.Y));
            Plinth(b, bay, plinth, PlinthHalf, BodyHalf, job.Sink, stone, at => stone.Colour * (0.9f + 0.16f * FenceLook.AlongWall(at.X, at.Y, at.Z, bay, 0.8f, seed + 3u)));
            foreach (float side in new[] { -1f, 1f })
            {
                b.Panel(new Vector3(0f, plinth + 0.05f, side * BodyHalf), new Vector3(bay, 0f, 0f), new Vector3(0f, bodyTop - plinth - 0.05f, 0f), Cells(bay), Cells(bodyTop - plinth), new Vector3(0f, 0f, side), wall, Weather);
                for (int k = 0; k < lines; k++)
                {
                    float y = MathHelper.Lerp(bodyTop - 0.3f, bodyTop - 0.3f - (lines - 1) * 0.22f, k / (float)Math.Max(lines - 1, 1));
                    float z = side * (BodyHalf + 0.006f);
                    b.Quad(new Vector3(0f, y - 0.035f, z), new Vector3(bay, y - 0.035f, z), new Vector3(bay, y + 0.035f, z), new Vector3(0f, y + 0.035f, z), new Vector3(bay * 0.5f, y, 0f), wall, new Vector3(0.95f, 0.93f, 0.88f));
                }
            }

            int posts = Math.Max(1, (int)MathF.Round(bay / postPitch));
            for (int i = 0; i < posts; i++)
            {
                float x = i * bay / posts;
                int mark = b.Mark;
                b.Block(new Vector3(x, (plinth + bodyTop) * 0.5f, 0f), new Vector3(0.11f, (bodyTop - plinth) * 0.5f, BodyHalf + 0.025f), Matrix3.Identity, 0.012f, wood, Faces.NegativeY | Faces.PositiveY);
                b.Upright(mark, x);
            }
            b.Block(new Vector3(bay * 0.5f, bodyTop + 0.04f, 0f), new Vector3(bay * 0.5f, 0.08f, BodyHalf + 0.06f), Matrix3.Identity, 0.015f, wood);
            Ends(b, bay, wall.Tinted(0.8f), new Vector2[]
            {
                new(-PlinthHalf, -job.Sink), new(PlinthHalf, -job.Sink), new(PlinthHalf, plinth - 0.1f), new(BodyHalf, plinth + 0.05f),
                new(BodyHalf, bodyTop), new(-BodyHalf, bodyTop), new(-BodyHalf, plinth + 0.05f), new(-PlinthHalf, plinth - 0.1f),
            });

            // The roof: a slope each side up to the ridge, with an edge to it and a soffit under it.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 eaveEdge = new Vector3(0f, roofBase, side * roofHalf);
                b.Panel(eaveEdge, new Vector3(bay, 0f, 0f), new Vector3(0f, rise, -side * roofHalf), Cells(bay), 2, new Vector3(0f, 1f, side * 0.6f), roof);
                b.Panel(eaveEdge - new Vector3(0f, 0.1f, 0f), new Vector3(bay, 0f, 0f), new Vector3(0f, 0.1f, 0f), 1, 1, new Vector3(0f, 0f, side), roof.Tinted(0.7f));
                b.Panel(eaveEdge - new Vector3(0f, 0.1f, 0f), new Vector3(bay, 0f, 0f), new Vector3(0f, bodyTop + 0.12f - (roofBase - 0.1f), -side * eave), 1, 1, new Vector3(0f, -1f, 0f), wood.Tinted(0.7f));
            }
            b.Block(new Vector3(bay * 0.5f, roofBase + rise + 0.02f, 0f), new Vector3(bay * 0.5f, 0.09f, 0.14f), Matrix3.Identity, 0.025f, ridge);
            Ends(b, bay, wood.Tinted(0.6f), new Vector2[]
            {
                new(-roofHalf, roofBase - 0.1f), new(-BodyHalf, bodyTop + 0.12f), new(BodyHalf, bodyTop + 0.12f), new(roofHalf, roofBase - 0.1f),
                new(roofHalf, roofBase), new(0f, roofBase + rise), new(-roofHalf, roofBase),
            });
        }

        // A hipped cap over a pier, standing a little proud of the roofs that come into it, so that it covers them where they meet whatever their angle.
        private static void HipCap(FenceBuilder b, float half, float baseY, float rise, Surface roof, Surface ridge, Surface wood)
        {
            Vector3 apex = new Vector3(0f, baseY + rise, 0f);
            Vector3[] rim = { new(-half, baseY, -half), new(half, baseY, -half), new(half, baseY, half), new(-half, baseY, half) };
            Vector3 middle = new Vector3(0f, baseY, 0f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = rim[i], c = rim[(i + 1) % 4];
                b.Triangle(a, c, apex, middle, roof);
                b.Quad(a - new Vector3(0f, 0.1f, 0f), c - new Vector3(0f, 0.1f, 0f), c, a, middle, roof.Tinted(0.7f));
            }
            b.Quad(rim[0] - new Vector3(0f, 0.1f, 0f), rim[1] - new Vector3(0f, 0.1f, 0f), rim[2] - new Vector3(0f, 0.1f, 0f), rim[3] - new Vector3(0f, 0.1f, 0f), apex, wood.Tinted(0.7f));
            b.Block(apex + new Vector3(0f, 0.05f, 0f), new Vector3(0.16f, 0.1f, 0.16f), Matrix3.Identity, 0.03f, ridge);
        }

        /// <summary>
        /// A keep's curtain wall: battered at its foot, a string course under the walk, a parapet on the outside cut into merlons with an arrow slit in each, and a low one inside.
        /// The outside is -z. Crenels fall at a span's ends, so where two spans meet is a gap in the battlements, as anywhere else along it.
        /// </summary>
        public static void Curtain(in FenceJob job)
        {
            Genes style = job.Style;
            FenceBuilder b = job.B;
            const float BodyHalf = 0.86f, FootHalf = 1.1f, ParapetDepth = 0.5f, InnerDepth = 0.32f;
            float walk = style.Range("walk", 7f, 8f);
            float batterTop = style.Range("batter", 1.4f, 2.1f);
            float parapet = style.Range("parapet", 1.5f, 1.8f), sill = style.Range("sill", 0.75f, 0.95f);
            float merlonWanted = style.Range("merlon", 1f, 1.3f), crenel = style.Range("crenel", 0.65f, 0.9f);
            float inner = style.Range("inner parapet", 0.7f, 0.9f);
            float tile = TileFor(job.Bay, style.Range("ashlar", 1.4f, 1.8f) * 4f);
            Vector3 colour = Dressed(style) * 1.01f;
            var masonry = new Surface(FenceMaterials.Masonry, colour, tile);
            uint seed = (uint)job.Style.Stream("weathering").Int(int.MaxValue);

            if (!job.IsSpan)
            {
                // A buttress where the wall stops, with its own battered foot and a merlon at each corner. Most stand inside a tower or a gatehouse, so it's slim.
                const float Half = 0.9f;
                float across = FootHalf + 0.05f, top = walk + sill;
                b.Block(new Vector3(0f, (batterTop - job.Sink) * 0.5f, 0f), new Vector3(Half + 0.2f, (batterTop + job.Sink) * 0.5f, across + 0.15f), Matrix3.Identity, 0.06f, masonry.Tinted(0.9f), Faces.NegativeY);
                b.Block(new Vector3(0f, (batterTop + top) * 0.5f, 0f), new Vector3(Half, (top - batterTop) * 0.5f, across), Matrix3.Identity, 0.05f, masonry);
                foreach (float sx in new[] { -1f, 1f })
                {
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        b.Block(new Vector3(sx * (Half - 0.3f), top + (parapet - sill) * 0.5f, sz * (across - 0.3f)), new Vector3(0.3f, (parapet - sill) * 0.5f, 0.3f), Matrix3.Identity, 0.03f, masonry, Faces.NegativeY);
                    }
                }
                return;
            }

            float bay = job.Bay;
            Vector3 Weather(Vector3 at) => FenceLook.Weathering(at, colour, bay, walk, seed);

            // Outside: the battered foot, then the face straight up into the parapet's sill.
            b.Panel(new Vector3(0f, -job.Sink, -FootHalf), new Vector3(bay, 0f, 0f), new Vector3(0f, batterTop + job.Sink, FootHalf - BodyHalf), Cells(bay), Cells(batterTop + job.Sink), new Vector3(0f, 0.3f, -1f), masonry, Weather, new Vector2(0f, -job.Sink / tile));
            b.Panel(new Vector3(0f, batterTop, -BodyHalf), new Vector3(bay, 0f, 0f), new Vector3(0f, walk + sill - batterTop, 0f), Cells(bay), Cells(walk + sill - batterTop), -Vector3.UnitZ, masonry, Weather, new Vector2(0f, batterTop / tile));
            // Inside: the same foot, the face up to the low parapet.
            b.Panel(new Vector3(0f, -job.Sink, FootHalf), new Vector3(bay, 0f, 0f), new Vector3(0f, batterTop + job.Sink, BodyHalf - FootHalf), Cells(bay), Cells(batterTop + job.Sink), new Vector3(0f, 0.3f, 1f), masonry, Weather, new Vector2(0f, -job.Sink / tile));
            b.Panel(new Vector3(0f, batterTop, BodyHalf), new Vector3(bay, 0f, 0f), new Vector3(0f, walk + inner - batterTop, 0f), Cells(bay), Cells(walk + inner - batterTop), Vector3.UnitZ, masonry, Weather, new Vector2(0f, batterTop / tile));

            // The walk between the parapets, flagged, and the tops and inner faces of both parapets.
            Surface flags = masonry.Tinted(0.82f);
            float outerInside = -BodyHalf + ParapetDepth, innerInside = BodyHalf - InnerDepth;
            b.Panel(new Vector3(0f, walk, outerInside), new Vector3(bay, 0f, 0f), new Vector3(0f, 0f, innerInside - outerInside), Cells(bay), 2, Vector3.UnitY, flags, at => Weather(at) * 0.82f);
            b.Panel(new Vector3(0f, walk, outerInside), new Vector3(bay, 0f, 0f), new Vector3(0f, sill, 0f), Cells(bay), 1, Vector3.UnitZ, masonry, Weather, new Vector2(0f, walk / tile));
            b.Panel(new Vector3(0f, walk + sill, -BodyHalf), new Vector3(bay, 0f, 0f), new Vector3(0f, 0f, ParapetDepth), Cells(bay), 1, Vector3.UnitY, masonry.Tinted(0.94f));
            b.Panel(new Vector3(0f, walk, innerInside), new Vector3(bay, 0f, 0f), new Vector3(0f, inner, 0f), Cells(bay), 1, -Vector3.UnitZ, masonry, Weather, new Vector2(0f, walk / tile));
            b.Panel(new Vector3(0f, walk + inner, innerInside), new Vector3(bay, 0f, 0f), new Vector3(0f, 0f, InnerDepth), Cells(bay), 1, Vector3.UnitY, masonry.Tinted(0.94f));

            // A string course along the outside under the walk.
            b.Block(new Vector3(bay * 0.5f, walk - 0.2f, -(BodyHalf + 0.06f)), new Vector3(bay * 0.5f, 0.13f, 0.09f), Matrix3.Identity, 0.03f, masonry.Tinted(0.9f));
            Surface joint = masonry.Tinted(Joint);
            Ends(b, bay, joint, new Vector2[] { new(-FootHalf, -job.Sink), new(FootHalf, -job.Sink), new(BodyHalf, batterTop), new(BodyHalf, walk), new(-BodyHalf, walk), new(-BodyHalf, batterTop) });
            Ends(b, bay, joint, new Vector2[] { new(-BodyHalf, walk), new(outerInside, walk), new(outerInside, walk + sill), new(-BodyHalf, walk + sill) });
            Ends(b, bay, joint, new Vector2[] { new(innerInside, walk), new(BodyHalf, walk), new(BodyHalf, walk + inner), new(innerInside, walk + inner) });

            // Merlons, each with its cap and its slit, and the crenels between them falling where spans meet.
            WorldRandom random = job.Own.Stream("merlons");
            int count = Math.Max(1, (int)MathF.Round(bay / (merlonWanted + crenel)));
            float unit = bay / count;
            float merlon = unit - crenel;
            float high = parapet - sill;
            var slit = new Surface(FenceMaterials.Masonry, new Vector3(0.05f, 0.05f, 0.055f), tile);
            for (int i = 0; i < count; i++)
            {
                float x = (i + 0.5f) * unit;
                float z = -BodyHalf + ParapetDepth * 0.5f;
                int mark = b.Mark;
                Surface block = masonry.Tinted(random.Range(0.92f, 1.04f));
                b.Block(new Vector3(x, walk + sill + high * 0.5f, z), new Vector3(merlon * 0.5f, high * 0.5f, ParapetDepth * 0.5f), Matrix3.Identity, 0.035f, block, Faces.NegativeY);
                b.Block(new Vector3(x, walk + parapet + 0.07f, z), new Vector3(merlon * 0.5f + 0.05f, 0.08f, ParapetDepth * 0.5f + 0.06f), Matrix3.Identity, 0.025f, block.Tinted(0.94f));
                float slitLow = walk + sill + high * 0.18f, slitHigh = walk + sill + high * 0.78f, front = -BodyHalf - 0.004f;
                b.Quad(new Vector3(x - 0.06f, slitLow, front), new Vector3(x + 0.06f, slitLow, front), new Vector3(x + 0.06f, slitHigh, front), new Vector3(x - 0.06f, slitHigh, front), new Vector3(x, slitLow, 0f), slit);
                b.Upright(mark, x);
            }
        }
    }
}
