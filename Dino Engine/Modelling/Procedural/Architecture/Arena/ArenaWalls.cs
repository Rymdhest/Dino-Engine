using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Plaza;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Arena
{
    /// <summary>
    /// The walls of an arena, stone by stone (<see cref="Waygates.Masonry"/>), laid along a <see cref="WallLine"/> as the Haven's are, each stone touching its neighbours only along their chamfers, and nothing
    /// that can be seen coplanar with anything else. The Reich builds them severe: squared pilasters at even steps, string courses, a heavy cope, and its iron along the top. Model y = 0 is where people stand
    /// at the wall's foot, its face is at back 0 and it stands to <c>top</c>; one that holds ground up has that ground at <c>soil</c>, where people stand behind it. Headless.
    /// </summary>
    internal static class ArenaWalls
    {
        private const float Foot = -0.6f;           // Metres below where people stand that a wall's stones start, as the floor's sand is drawn a little above it.
        private const float PilasterStep = 4.2f;    // Metres between pilasters along a podium, near enough: a whole number of bays is fitted to its length.
        private const float PilasterWide = 0.95f;
        private const float PilasterOut = 0.22f;

        public static void Build(MeshBuilder b, WallLine line, ArenaWallForm form, ArenaStoneKind stone, float top, float thick, float soil, ArenaTrim trim, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("arena wall");
            switch (form)
            {
                case ArenaWallForm.Podium: Podium(b, line, stone, top, thick, soil, trim, random, lights); break;
                case ArenaWallForm.Riser: Riser(b, line, stone, top, thick, soil, random); break;
                case ArenaWallForm.Parapet: Parapet(b, line, stone, top, thick, random); break;
                case ArenaWallForm.Footing: Footing(b, line, stone, top, thick, random); break;
                case ArenaWallForm.Quarry: Quarry(b, line, stone, top, thick, random); break;
            }
            if (soil >= 0f && form is not (ArenaWallForm.Footing or ArenaWallForm.Quarry)) Bank(b, line, soil, thick, stone);
        }

        // ---------------------------------------------------------------- Podium

        // The sheer wall round a floor: a plinth in long blocks, coursed ashlar in two bands with a string course between, pilasters standing out of it at even steps with a brass boss at
        // the head of each, a moulded cope, and the Reich's railing along the top on the side away from the floor.
        private static void Podium(MeshBuilder b, WallLine line, ArenaStoneKind stone, float top, float thick, float soil, ArenaTrim trim, WorldRandom random, List<AssetLight> lights)
        {
            const float Plinth = 0.62f, PlinthProud = 0.1f;
            const float Mould = 0.12f, Cope = 0.3f, String = 0.2f;
            float body = top - Mould - Cope;
            float stringAt = MathF.Min(Plinth + (body - Plinth) * 0.58f, body - 0.9f);

            // The plinth, proud of the face, in long blocks.
            Course(b, line, Foot, Plinth, -PlinthProud, thick, 1.7f, random.Range(0f, 0.8f), random.Fork("plinth"), stone, ArenaStone.BigChamfer, 0.05f);

            // Coursed ashlar to the string course, each course set half a block along from the one below.
            int lower = Math.Max(1, (int)MathF.Round((stringAt - Plinth) / 0.52f));
            float rise = (stringAt - Plinth) / lower;
            float stagger = random.Range(0f, 1.3f);
            for (int c = 0; c < lower; c++)
            {
                float y0 = Plinth + c * rise;
                Course(b, line, y0, y0 + rise, 0f, thick, 1.35f, stagger + (c % 2) * 0.67f, random.Fork(c), stone, ArenaStone.Chamfer, 0.06f);
            }

            // The string course, proud, then the upper band in shallower courses.
            Course(b, line, stringAt, stringAt + String, -0.08f, thick, 1.6f, random.Range(0f, 0.9f), random.Fork("string"), stone, ArenaStone.Chamfer, 0.035f, dust: 0.1f);
            float upperFrom = stringAt + String;
            int upper = Math.Max(1, (int)MathF.Round((body - upperFrom) / 0.44f));
            float upperRise = (body - upperFrom) / upper;
            for (int c = 0; c < upper; c++)
            {
                float y0 = upperFrom + c * upperRise;
                Course(b, line, y0, y0 + upperRise, 0f, thick, 1.2f, stagger + (c % 2) * 0.6f + 0.3f, random.Fork(100 + c), stone, ArenaStone.Chamfer, 0.06f);
            }

            // A bed mould and the cope, proud of the face and running back over the wall's whole thickness.
            bool parapet = soil < 0f || top - soil > 0.15f;
            Course(b, line, body, body + Mould, -0.06f, thick + (parapet ? 0.04f : 0f), 1.7f, random.Range(0f, 0.9f), random.Fork("mould"), stone, 0.03f, 0.04f);
            Course(b, line, body + Mould, top, -0.16f, thick + (parapet ? 0.1f : 0.03f), 1.5f, random.Range(0f, 0.9f), random.Fork("cope"), stone, ArenaStone.BigChamfer, 0.04f, dust: 0.16f);

            if ((trim & ArenaTrim.Pilasters) != 0) Pilasters(b, line, stone, Plinth, body, trim, random.Fork("pilasters"), lights);
            if ((trim & ArenaTrim.Railing) != 0) Railing(b, line, top, thick, random.Fork("railing"));
        }

        // Pilasters out of a podium's face, fitted evenly to its length so that its ends fall in the middle of a bay; each a stack of long and short blocks under a brass boss with the Reich's
        // mark, and, if the wall carries them, a torch at the head of every other one and the Reich's banner hung between every other pair.
        private static void Pilasters(MeshBuilder b, WallLine line, ArenaStoneKind stone, float plinth, float body, ArenaTrim trim, WorldRandom random, List<AssetLight> lights)
        {
            int bays = Math.Max(1, (int)MathF.Round(line.Length / PilasterStep));
            float step = line.Length / bays;
            for (int k = 0; k < bays; k++)
            {
                float along = (k + 0.5f) * step;
                WorldRandom one = random.Fork(k);
                float from = along - PilasterWide * 0.5f, to = along + PilasterWide * 0.5f;
                float y = plinth;
                int course = 0;
                while (y < body - 0.05f)
                {
                    float rise = MathF.Min(course % 2 == 0 ? 0.62f : 0.4f, body - y);
                    StoneBetween(b, line, from + 0.004f, to - 0.004f, y + 0.003f, y + rise - 0.003f, -PilasterOut, 0.18f, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, one.Fork(course), y));
                    y += rise;
                    course++;
                }

                // A brass boss with the mark on it, under the cope, on a stone block.
                Vector3 face = line.At(along, -PilasterOut, 0f);
                Vector3 behind = line.At(along, 0.2f, 0f);
                Vector3 outward = new Vector3(face.X - behind.X, 0f, face.Z - behind.Z).Normalized();
                Vector3 right = Vector3.Cross(Vector3.UnitY, outward).Normalized();
                float bossY = body - 0.55f;
                Vector3 boss = new Vector3(face.X, bossY, face.Z);
                Disc(b, boss, outward, 0.34f, 0.07f, ArenaStone.Brass(one, 0.9f));
                ArenaStone.Mark(b, boss + outward * 0.07f, right, Vector3.UnitY, 0.42f, 0.018f, ArenaStone.Brass(one, 1.08f), ArenaStone.Brass(one, 0.55f));

                if ((trim & ArenaTrim.Torches) != 0 && k % 2 == 0)
                {
                    Vector3 torch = new Vector3(face.X, bossY - 1.1f, face.Z) + outward * 0.05f;
                    Torch(b, torch, outward, one.Fork("torch"), lights);
                }

                // The Reich's banner, hung from the cope down the face between this pilaster and the next, in every other bay, starting from the first so that a short wall carries one.
                if ((trim & ArenaTrim.Banners) != 0 && k % 2 == 0 && k + 1 < bays)
                {
                    float middle = along + step * 0.5f;
                    {
                        Vector3 hang = line.At(middle, -0.05f, body - 0.02f);
                        Vector3 mid = line.At(middle, 0.2f, body);
                        Vector3 outwards = new Vector3(hang.X - mid.X, 0f, hang.Z - mid.Z).Normalized();
                        float length = MathF.Min(body - plinth - 0.4f, 3.6f);
                        HungBanner(b, hang, outwards, MathF.Min(step - PilasterWide - 0.6f, 1.7f), length, one.Fork("banner"));
                    }
                }
            }
        }

        // Black iron along a cope: square posts on the side away from the floor, two rails through them, and a spike between every post leaning out over the floor, as no one is meant to come up.
        private static void Railing(MeshBuilder b, WallLine line, float top, float thick, WorldRandom random)
        {
            const float Post = 0.075f, High = 1.25f, Spacing = 0.42f;
            float back = MathF.Max(thick - 0.32f, 0.3f);
            VertexMaterial iron = ArenaStone.Iron(random);
            int count = Math.Max(2, (int)MathF.Round(line.Length / Spacing));
            for (int i = 0; i <= count; i++)
            {
                float along = line.Length * i / count;
                bool post = i % 6 == 0;
                Vector3 foot = line.At(along, back, top);
                Vector3 head = line.At(along, back, top + High * (post ? 1.06f : 1f));
                if (post) b.Box((foot + head) * 0.5f, new Vector3(Post, (head.Y - foot.Y) * 0.5f, Post), iron);
                else b.Tube(foot, head, 0.03f, 0.028f, 4, iron, capped: false);
                // Its spike, leaning out over the floor.
                Vector3 lean = line.At(along, back - 0.36f, top + High + 0.42f);
                b.Tube(head, lean, 0.032f, 0.004f, 4, iron);
            }
            for (int r = 0; r < 2; r++)
            {
                float y = top + (r == 0 ? 0.18f : High - 0.12f);
                int segments = Math.Max(2, (int)MathF.Ceiling(line.Length / 0.9f));
                for (int i = 0; i < segments; i++)
                {
                    float a = line.Length * i / segments, c = line.Length * (i + 1) / segments;
                    b.Tube(line.At(a, back, y), line.At(c, back, y), 0.034f, 0.034f, 4, iron, capped: false);
                }
            }
        }

        // ---------------------------------------------------------------- Risers and parapets

        // The front of a tier of stands: a course or two of ashlar under a broad bench of a cope, which runs back over the tier's front, as people stand behind it.
        private static void Riser(MeshBuilder b, WallLine line, ArenaStoneKind stone, float top, float thick, float soil, WorldRandom random)
        {
            const float Bench = 0.24f;
            float body = top - Bench;
            int courses = Math.Max(1, (int)MathF.Round((body - Foot) / 0.48f));
            float rise = (body - Foot) / courses;
            float stagger = random.Range(0f, 1.1f);
            for (int c = 0; c < courses; c++)
            {
                float y0 = Foot + c * rise;
                Course(b, line, y0, y0 + rise, 0f, thick, 1.25f, stagger + (c % 2) * 0.6f, random.Fork(c), stone, ArenaStone.Chamfer, 0.06f);
            }
            Course(b, line, body, top, -0.12f, thick + 0.55f, 1.15f, random.Range(0f, 0.7f), random.Fork("bench"), stone, ArenaStone.BigChamfer, 0.04f, dust: 0.18f);
        }

        private static void Parapet(MeshBuilder b, WallLine line, ArenaStoneKind stone, float top, float thick, WorldRandom random)
        {
            const float Cope = 0.22f;
            float body = top - Cope;
            int courses = Math.Max(1, (int)MathF.Round((body - Foot) / 0.46f));
            float rise = (body - Foot) / courses;
            for (int c = 0; c < courses; c++)
            {
                float y0 = Foot + c * rise;
                Course(b, line, y0, y0 + rise, 0f, thick, 1.2f, (c % 2) * 0.6f + random.Range(0f, 0.3f), random.Fork(c), stone, ArenaStone.Chamfer, 0.06f);
            }
            Course(b, line, body, top, -0.09f, thick + 0.09f, 1.3f, random.Range(0f, 0.6f), random.Fork("cope"), stone, ArenaStone.BigChamfer, 0.04f, dust: 0.15f);
        }

        // What a cage's bars stand in: two squat courses under a cap sloped to shed water, deep enough for the bars to be leaded into it.
        private static void Footing(MeshBuilder b, WallLine line, ArenaStoneKind stone, float top, float thick, WorldRandom random)
        {
            const float Cap = 0.26f;
            float body = top - Cap;
            Course(b, line, Foot, body * 0.5f, -0.06f, thick, 1.5f, random.Range(0f, 0.8f), random.Fork("low"), stone, ArenaStone.BigChamfer, 0.06f);
            Course(b, line, body * 0.5f, body, 0f, thick, 1.3f, random.Range(0f, 0.8f) + 0.6f, random.Fork("high"), stone, ArenaStone.Chamfer, 0.06f);
            WorldRandom caps = random.Fork("cap");
            for (float along = -caps.Range(0f, 0.6f); along < line.Length;)
            {
                float piece = MathF.Min(caps.Range(1.1f, 1.6f), line.LongestStone);
                float from = MathF.Max(along, 0f), to = MathF.Min(along + piece, line.Length);
                if (to - from > 0.12f)
                {
                    Func<Vector3, VertexMaterial> facing = ArenaStone.DressedFacing(stone, caps.Fork((int)(along * 10f)), top, 0.12f);
                    // Sloped, higher at its back than at its face.
                    PlazaWalls.Lay(b, new[]
                    {
                        line.At(from + 0.006f, -0.08f, body), line.At(to - 0.006f, -0.08f, body), line.At(to - 0.006f, -0.08f, top - 0.1f), line.At(from + 0.006f, -0.08f, top - 0.1f),
                        line.At(from + 0.006f, thick + 0.04f, body), line.At(to - 0.006f, thick + 0.04f, body), line.At(to - 0.006f, thick + 0.04f, top), line.At(from + 0.006f, thick + 0.04f, top),
                    }, ArenaStone.Chamfer, facing);
                }
                along += piece;
            }
        }

        // A quarry's cut face: blocks as the rock came off, long and irregular, each a little proud or shy of the next, leaning back as they rise, darker low down where the ash has been
        // trodden into them, under a bench of rough slabs that runs back over the wall.
        private static void Quarry(MeshBuilder b, WallLine line, ArenaStoneKind stone, float top, float thick, WorldRandom random)
        {
            const float Batter = 0.12f;
            float body = top - 0.35f;
            float y = Foot;
            int course = 0;
            while (y < body - 0.1f)
            {
                WorldRandom row = random.Fork(course++);
                float rise = MathF.Min(row.Range(0.65f, 1.15f), body - y);
                if (body - (y + rise) < 0.3f) rise = body - y;
                for (float along = -row.Range(0f, 1.2f); along < line.Length;)
                {
                    float length = MathF.Min(row.Range(1.2f, 2.5f), line.LongestStone);
                    float from = MathF.Max(along, 0f), to = MathF.Min(along + length, line.Length);
                    if (to - from > 0.2f)
                    {
                        float proud = row.Range(-0.06f, 0.12f);
                        float lean0 = Batter * (y - Foot), lean1 = Batter * (y + rise - Foot);
                        float shade = row.Range(0.78f, 1.08f) * (y < 1f ? 0.85f : 1f);
                        VertexMaterial face = ArenaStone.Tint(ArenaStone.Dressed(stone, row.Fork((int)(along * 10f)), y, 0.08f), new Vector3(shade));
                        VertexMaterial topFace = ArenaStone.Tint(face, new Vector3(1.18f, 1.17f, 1.14f));
                        PlazaWalls.Lay(b, new[]
                        {
                            line.At(from + 0.01f, lean0 - proud, y + 0.01f), line.At(to - 0.01f, lean0 - proud + row.Range(-0.04f, 0.04f), y + 0.01f), line.At(to - 0.01f, lean1 - proud, y + rise - 0.01f), line.At(from + 0.01f, lean1 - proud + row.Range(-0.04f, 0.04f), y + rise - 0.01f),
                            line.At(from + 0.01f, thick, y + 0.01f), line.At(to - 0.01f, thick, y + 0.01f), line.At(to - 0.01f, thick, y + rise - 0.01f), line.At(from + 0.01f, thick, y + rise - 0.01f),
                        }, 0.06f, n => n.Y > 0.6f ? topFace : face);
                    }
                    along += length;
                }
                y += rise;
            }
            // The bench, rough slabs over the top, proud of the face where the last cut stopped.
            WorldRandom bench = random.Fork("bench");
            for (float along = -bench.Range(0f, 0.8f); along < line.Length;)
            {
                float length = MathF.Min(bench.Range(1.1f, 1.9f), line.LongestStone);
                float from = MathF.Max(along, 0f), to = MathF.Min(along + length, line.Length);
                if (to - from > 0.15f)
                {
                    float lean = Batter * (body - Foot);
                    StoneBetween(b, line, from + 0.012f, to - 0.012f, body - 0.02f, top + bench.Range(-0.05f, 0.05f), lean - 0.2f, thick + 0.1f, 0.06f, ArenaStone.DressedFacing(stone, bench.Fork((int)(along * 10f)), top, 0.25f));
                }
                along += length;
            }
        }

        // ---------------------------------------------------------------- Shared

        // A course of dressed blocks between two heights and two depths, each about `length` long, starting `offset` along, with the dark of the joints showing behind them.
        internal static void Course(MeshBuilder b, WallLine line, float bottom, float top, float face, float back, float length, float offset, WorldRandom random, ArenaStoneKind stone, float chamfer,
            float spread, float dust = 0f)
        {
            float along = -(offset % length);
            while (along < line.Length)
            {
                float piece = MathF.Min(length * random.Range(0.75f, 1.25f), line.LongestStone);
                float from = MathF.Max(along, 0f), to = MathF.Min(along + piece, line.Length);
                if (to - from > 0.1f)
                {
                    VertexMaterial plain = ArenaStone.Dressed(stone, random, bottom, spread);
                    Func<Vector3, VertexMaterial> facing = dust > 0f ? ArenaStone.DressedFacing(stone, random, bottom, dust, spread) : _ => plain;
                    StoneBetween(b, line, from + 0.005f, to - 0.005f, bottom + 0.002f, top - 0.002f, face, back, chamfer, facing);
                }
                along += piece;
            }
        }

        /// <summary>A stone filling a stretch of a line between two heights and two depths back from its face.</summary>
        internal static void StoneBetween(MeshBuilder b, WallLine line, float from, float to, float bottom, float top, float face, float back, float chamfer, Func<Vector3, VertexMaterial> facing)
        {
            PlazaWalls.Lay(b, new[]
            {
                line.At(from, face, bottom), line.At(to, face, bottom), line.At(to, face, top), line.At(from, face, top),
                line.At(from, back, bottom), line.At(to, back, bottom), line.At(to, back, top), line.At(from, back, top),
            }, chamfer, facing);
        }

        // Ground banked against the back of a wall that holds it up, from the wall's back a couple of metres in, at the height the ground's drawn: a metre's grid of heights lays the step as a
        // slope, which this covers. Packed earth and grit, as behind any arena's wall.
        private static void Bank(MeshBuilder b, WallLine line, float soil, float thick, ArenaStoneKind stone)
        {
            float near = thick - 0.04f, far = MathF.Min(thick + 1.9f, line.DeepestBack);
            float high = soil + ArenaStone.Ground + 0.03f, low = soil + ArenaStone.Ground - 0.3f;
            VertexMaterial earth = new VertexMaterial(Textures.MaterialCatalog.Gravel.Index, new Util.Colour(Vector3.Lerp(ArenaStone.ColourOf(stone) * 0.42f, new Vector3(0.36f, 0.3f, 0.24f), 0.5f)));
            int segments = Math.Max(1, (int)MathF.Ceiling(line.Length / 0.7f));
            for (int i = 0; i < segments; i++)
            {
                float s0 = line.Length * i / segments, s1 = line.Length * (i + 1) / segments;
                float mid = (near + far) * 0.5f;
                Vector3 a = line.At(s0, near, high), c = line.At(s1, near, high), d = line.At(s1, mid, high - 0.06f), e = line.At(s0, mid, high - 0.06f);
                Vector3 f = line.At(s1, far, low), g = line.At(s0, far, low);
                UpQuad(b, a, c, d, e, earth);
                UpQuad(b, e, d, f, g, earth);
            }
        }

        private static void UpQuad(MeshBuilder b, Vector3 a, Vector3 c, Vector3 d, Vector3 e, VertexMaterial material)
        {
            Vector3 normal = Vector3.Cross(d - a, e - c);
            if (normal.Y < 0f) b.Quad(c, a, e, d, material);
            else b.Quad(a, c, d, e, material);
        }

        /// <summary>A disc facing <paramref name="normal"/>, <paramref name="thick"/> deep out of what's behind it.</summary>
        internal static void Disc(MeshBuilder b, Vector3 centre, Vector3 normal, float radius, float thick, VertexMaterial material)
        {
            b.Tube(centre, centre + normal.Normalized() * thick, radius, radius * 0.94f, 14, material);
        }

        /// <summary>
        /// A torch on an iron bracket out of a wall facing <paramref name="outward"/>: a back plate, an arm, a cup of pitch with its flame burning as particles, and the light beside it.
        /// </summary>
        internal static void Torch(MeshBuilder b, Vector3 at, Vector3 outward, WorldRandom random, List<AssetLight> lights)
        {
            VertexMaterial iron = ArenaStone.Iron(random);
            Vector3 o = outward.Normalized();
            b.Box(at, new Vector3(0.12f, 0.24f, 0.12f) * new Vector3(MathF.Abs(o.Z) + 0.25f, 1f, MathF.Abs(o.X) + 0.25f), iron);
            Vector3 elbow = at + o * 0.42f + Vector3.UnitY * 0.05f;
            b.Tube(at + o * 0.05f, elbow, 0.035f, 0.035f, 4, iron);
            Vector3 cup = elbow + Vector3.UnitY * 0.18f;
            b.Tube(elbow, cup, 0.03f, 0.03f, 4, iron);
            b.Tube(cup, cup + Vector3.UnitY * 0.22f, 0.07f, 0.13f, 6, iron, capped: false);
            b.Tube(cup + Vector3.UnitY * 0.16f, cup + Vector3.UnitY * 0.2f, 0.11f, 0.11f, 6, ArenaStone.Coals);
            lights.Add(AssetLight.Burning(cup + Vector3.UnitY * 0.2f, 0.26f));
            lights.Add(ArenaStone.TorchLight(cup + Vector3.UnitY * 0.55f + o * 0.15f));
        }

        /// <summary>
        /// The Reich's banner hung down a face: a rod under the cope with brass finials, oxblood cloth <paramref name="width"/> across and <paramref name="length"/> long ending in a swallowtail, a band of
        /// brass at its head and its foot, and the mark in brass in its upper half. <paramref name="top"/> is the middle of the rod and <paramref name="outward"/> the way the face looks.
        /// </summary>
        internal static void HungBanner(MeshBuilder b, Vector3 top, Vector3 outward, float width, float length, WorldRandom random)
        {
            Vector3 o = outward.Normalized();
            Vector3 right = Vector3.Cross(Vector3.UnitY, o).Normalized();
            float half = width * 0.5f;
            VertexMaterial cloth = ArenaStone.Oxblood(random.Range(0.92f, 1.04f));
            VertexMaterial back = ArenaStone.Oxblood(0.7f);
            VertexMaterial brass = ArenaStone.Brass(random);
            Vector3 rodAt = top + o * 0.12f;
            b.Tube(rodAt - right * (half + 0.12f), rodAt + right * (half + 0.12f), 0.035f, 0.035f, 6, ArenaStone.Iron(random));
            foreach (float s in new[] { -1f, 1f }) b.Tube(rodAt + right * s * (half + 0.12f), rodAt + right * s * (half + 0.2f), 0.06f, 0.0f, 6, brass);

            // The cloth hangs a little out from the face at its foot, as it would under its own weight with a rod behind its head, and every corner of it is wound twice so that it's seen from either side.
            Vector3 headLeft = rodAt - right * half - Vector3.UnitY * 0.05f, headRight = rodAt + right * half - Vector3.UnitY * 0.05f;
            Vector3 drop = -Vector3.UnitY * length + o * 0.08f;
            Vector3 tail = -Vector3.UnitY * (length * 0.86f) + o * 0.07f;
            Vector3 footLeft = headLeft + drop, footRight = headRight + drop;
            Vector3 notch = (headLeft + headRight) * 0.5f + tail;
            const int Rows = 6;
            for (int r = 0; r < Rows; r++)
            {
                float t0 = r / (float)Rows, t1 = (r + 1) / (float)Rows;
                Vector3 l0 = Vector3.Lerp(headLeft, footLeft, t0), l1 = Vector3.Lerp(headLeft, footLeft, t1);
                Vector3 r0 = Vector3.Lerp(headRight, footRight, t0), r1 = Vector3.Lerp(headRight, footRight, t1);
                if (r == Rows - 1)
                {
                    // The swallowtail: the foot of the cloth cut up to a notch in the middle.
                    Vector3 m0 = (l0 + r0) * 0.5f;
                    TwoSided(b, l0, m0, notch, l1, cloth, back);
                    TwoSided(b, m0, r0, r1, notch, cloth, back);
                    continue;
                }
                TwoSided(b, l0, r0, r1, l1, cloth, back);
            }

            // Brass bands across its head and above its tail, and the mark in its upper half.
            Vector3 front = o * 0.012f;
            Band(b, headLeft + front - Vector3.UnitY * 0.12f, headRight + front - Vector3.UnitY * 0.12f, 0.07f, brass);
            Vector3 lowLeft = Vector3.Lerp(headLeft, footLeft, 0.72f) + front, lowRight = Vector3.Lerp(headRight, footRight, 0.72f) + front;
            Band(b, lowLeft, lowRight, 0.05f, brass);
            Vector3 markAt = Vector3.Lerp((headLeft + headRight) * 0.5f, (footLeft + footRight) * 0.5f, 0.32f) + o * 0.016f;
            ArenaStone.Mark(b, markAt, right, Vector3.UnitY, width * 0.78f, 0.012f, brass, cloth);
        }

        private static void Band(MeshBuilder b, Vector3 from, Vector3 to, float half, VertexMaterial material)
        {
            Vector3 up = Vector3.UnitY * half;
            b.Quad(from - up, to - up, to + up, from + up, material);
        }

        // A face wound both ways, front in one colour and back in another, as cloth seen from behind is darker.
        internal static void TwoSided(MeshBuilder b, Vector3 a, Vector3 c, Vector3 d, Vector3 e, VertexMaterial front, VertexMaterial back)
        {
            b.Quad(a, e, d, c, front);
            b.Quad(a, c, d, e, back);
        }
    }
}
