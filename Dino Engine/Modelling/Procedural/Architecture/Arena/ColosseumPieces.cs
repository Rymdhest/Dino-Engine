using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Plaza;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Arena
{
    /// <summary>
    /// What the Colosseum is built of besides its walls: the arcade of two storeys round the top of its north side, half-columns on its piers and arches open to the sky; the commander's box, a
    /// tribune out over the podium under an oxblood canopy, where the bolt-throwers stand; the lift hatches in the floor, a frame round a shaft into the dark with two leaves that drop open; and the
    /// bolt-thrower itself. Model y = 0 is where people stand at a piece's foot, and +z is its front. Headless.
    /// </summary>
    internal static class ColosseumPieces
    {
        private const float Foot = -0.6f;

        // ---------------------------------------------------------------- The arcade

        /// <summary>
        /// An arcade along a line, its front towards +z (an arc's facing): <paramref name="bays"/> arches on square piers, each pier faced with a half-column, an entablature over each storey, and an attic
        /// on top with the Reich's banner hung in every other bay of it. Each arch is open right through, as the sky is meant to be seen through them.
        /// </summary>
        public static void Arcade(MeshBuilder b, WallLine line, ArenaStoneKind stone, int storeys, float storey, float pier, float depth, uint seed)
        {
            var random = new WorldRandom(seed).Fork("arcade");
            int bays = Math.Max(1, (int)MathF.Round(line.Length / (storey * 0.72f)));
            float bay = line.Length / bays;
            const float Entablature = 0.85f;
            float y = 0f;
            for (int s = 0; s < storeys; s++)
            {
                WorldRandom level = random.Fork(s);
                float springAt = y + storey * 0.58f;
                float opening = bay - pier;
                float arch = opening * 0.5f;
                float crownAt = springAt + arch;
                for (int k = 0; k <= bays; k++)
                {
                    float along = k * bay;
                    // A pier, coursed, with a half-column out of its face and a moulded impost at the springing.
                    float from = MathF.Max(along - pier * 0.5f, 0f), to = MathF.Min(along + pier * 0.5f, line.Length);
                    if (to - from < 0.2f) continue;
                    int courses = Math.Max(2, (int)MathF.Round((springAt - y) / 0.6f));
                    float rise = (springAt - y) / courses;
                    for (int c = 0; c < courses; c++)
                    {
                        float y0 = y + c * rise;
                        ArenaWalls.StoneBetween(b, line, from + 0.004f, to - 0.004f, (s == 0 ? Foot : y0) + 0.003f, y0 + rise - 0.003f, 0f, depth, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, level.Fork(k * 31 + c), y0));
                    }
                    ArenaWalls.StoneBetween(b, line, from - 0.08f, to + 0.08f, springAt - 0.02f, springAt + 0.22f, -0.1f, depth + 0.06f, 0.03f, ArenaStone.DressedFacing(stone, level.Fork(k * 31 + 29), springAt, 0.14f));
                    HalfColumn(b, line, along, y, crownAt + 0.3f, pier * 0.36f, stone, level.Fork(k * 31 + 30));
                }
                // Over each opening, the arch's voussoirs round a keystone, and the spandrels coursed up to the entablature, each stone cut to the arch's back where it meets it and running to
                // the middle of the pier either side, where it meets the next bay's.
                for (int k = 0; k < bays; k++)
                {
                    float middle = (k + 0.5f) * bay;
                    float centreY = springAt + 0.22f, extrados = arch + 0.62f;
                    Voussoirs(b, line, middle, centreY, arch, 0.62f, depth, stone, level.Fork(500 + k));
                    float top = crownAt + 0.62f + 0.2f;
                    float left = k * bay, right = (k + 1) * bay;
                    float Inner(float at) => at >= centreY + extrados ? 0f : MathF.Sqrt(MathF.Max(extrados * extrados - (at - centreY) * (at - centreY), 0f));
                    float over = centreY;
                    int course = 0;
                    while (over < top - 0.05f)
                    {
                        float rise = MathF.Min(0.5f, top - over);
                        float y1 = over + rise;
                        float i0 = Inner(over), i1 = Inner(y1);
                        WorldRandom one = level.Fork(700 + k * 41 + course);
                        if (i0 <= 0.001f && i1 <= 0.001f)
                        {
                            float joint = middle + (course % 2 == 0 ? 0.35f : -0.35f) * bay * 0.5f;
                            Spandrel(b, line, left + 0.004f, joint - 0.004f, left + 0.004f, joint - 0.004f, over, y1, depth, stone, one.Fork("a"));
                            Spandrel(b, line, joint + 0.004f, right - 0.004f, joint + 0.004f, right - 0.004f, over, y1, depth, stone, one.Fork("b"));
                        }
                        else
                        {
                            Spandrel(b, line, left + 0.004f, MathF.Max(middle - i0, left + 0.01f) - 0.004f, left + 0.004f, MathF.Max(middle - i1, left + 0.01f) - 0.004f, over, y1, depth, stone, one.Fork("a"));
                            Spandrel(b, line, MathF.Min(middle + i0, right - 0.01f) + 0.004f, right - 0.004f, MathF.Min(middle + i1, right - 0.01f) + 0.004f, right - 0.004f, over, y1, depth, stone, one.Fork("b"));
                        }
                        over = y1;
                        course++;
                    }
                }
                y = crownAt + 0.62f + 0.2f;
                // The entablature: architrave, frieze and a cornice proud of both, in long blocks.
                ArenaWalls.Course(b, line, y, y + 0.3f, -0.05f, depth + 0.05f, 1.5f, random.Range(0f, 0.8f), level.Fork("architrave"), stone, 0.03f, 0.04f);
                ArenaWalls.Course(b, line, y + 0.3f, y + 0.62f, 0f, depth, 1.3f, random.Range(0f, 0.8f), level.Fork("frieze"), stone, ArenaStone.Chamfer, 0.05f);
                ArenaWalls.Course(b, line, y + 0.62f, y + Entablature, -0.2f, depth + 0.2f, 1.45f, random.Range(0f, 0.8f), level.Fork("cornice"), stone, ArenaStone.BigChamfer, 0.04f, dust: 0.16f);
                y += Entablature;
            }

            // The attic over it all, solid, with a pilaster over every pier and the Reich's banner hung in every other bay.
            float attic = storey * 0.45f;
            ArenaWalls.Course(b, line, y, y + attic * 0.5f, 0f, depth, 1.3f, random.Range(0f, 0.9f), random.Fork("attic low"), stone, ArenaStone.Chamfer, 0.06f);
            ArenaWalls.Course(b, line, y + attic * 0.5f, y + attic, 0f, depth, 1.2f, random.Range(0f, 0.9f) + 0.6f, random.Fork("attic high"), stone, ArenaStone.Chamfer, 0.06f);
            ArenaWalls.Course(b, line, y + attic, y + attic + 0.32f, -0.16f, depth + 0.16f, 1.5f, random.Range(0f, 0.9f), random.Fork("attic cope"), stone, ArenaStone.BigChamfer, 0.04f, dust: 0.18f);
            for (int k = 0; k <= bays; k++)
            {
                float along = k * bay;
                float from = MathF.Max(along - pier * 0.4f, 0f), to = MathF.Min(along + pier * 0.4f, line.Length);
                if (to - from > 0.2f) ArenaWalls.StoneBetween(b, line, from, to, y + 0.003f, y + attic - 0.003f, -0.16f, 0.1f, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork(4000 + k), y));
                if (k < bays && k % 2 == 0)
                {
                    float middle = (k + 0.5f) * bay;
                    Vector3 hang = line.At(middle, -0.05f, y + attic - 0.05f), mid = line.At(middle, 0.2f, y + attic);
                    Vector3 outward = new Vector3(hang.X - mid.X, 0f, hang.Z - mid.Z).Normalized();
                    ArenaWalls.HungBanner(b, hang, outward, MathF.Min(bay - pier - 0.8f, 1.9f), storey * 0.9f, random.Fork(5000 + k));
                }
            }
        }

        // A stone of a spandrel, from one place along the line to another at its foot and at its head, as one cut to the back of an arch slants; too thin to be worth laying, it isn't.
        private static void Spandrel(MeshBuilder b, WallLine line, float footFrom, float footTo, float headFrom, float headTo, float bottom, float top, float depth, ArenaStoneKind stone, WorldRandom random)
        {
            if (footTo - footFrom < 0.06f || headTo - headFrom < 0.06f) return;
            PlazaWalls.Lay(b, new[]
            {
                line.At(footFrom, 0.05f, bottom + 0.003f), line.At(footTo, 0.05f, bottom + 0.003f), line.At(headTo, 0.05f, top - 0.003f), line.At(headFrom, 0.05f, top - 0.003f),
                line.At(footFrom, depth - 0.05f, bottom + 0.003f), line.At(footTo, depth - 0.05f, bottom + 0.003f), line.At(headTo, depth - 0.05f, top - 0.003f), line.At(headFrom, depth - 0.05f, top - 0.003f),
            }, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random, bottom));
        }

        // A half-column out of a pier's face: a base, a shaft in drums, and a capital under the impost of the storey above.
        private static void HalfColumn(MeshBuilder b, WallLine line, float along, float from, float to, float radius, ArenaStoneKind stone, WorldRandom random)
        {
            Vector3 foot = line.At(along, -radius * 0.55f, from);
            Vector3 head = line.At(along, -radius * 0.55f, to);
            VertexMaterial face = ArenaStone.Dressed(stone, random, from + 1f, 0.04f);
            b.Tube(foot, foot + Vector3.UnitY * 0.3f, radius * 1.25f, radius * 1.15f, 12, face);
            int drums = Math.Max(2, (int)MathF.Round((to - from - 0.6f) / 1.2f));
            for (int d = 0; d < drums; d++)
            {
                float y0 = from + 0.3f + (to - from - 0.6f) * d / drums, y1 = from + 0.3f + (to - from - 0.6f) * (d + 1) / drums;
                b.Tube(new Vector3(foot.X, y0 + 0.004f, foot.Z), new Vector3(foot.X, y1 - 0.004f, foot.Z), radius, radius * 0.97f, 12, ArenaStone.Dressed(stone, random.Fork(d), y0, 0.035f), capped: false);
            }
            b.Tube(new Vector3(head.X, to - 0.3f, head.Z), new Vector3(head.X, to, head.Z), radius * 0.98f, radius * 1.3f, 12, face);
        }

        // An arch's voussoirs over an opening whose middle is `middle` along the line, springing at `spring`, of radius `radius` and `ring` deep from the opening out, round a keystone.
        private static void Voussoirs(MeshBuilder b, WallLine line, float middle, float spring, float radius, float ring, float depth, ArenaStoneKind stone, WorldRandom random)
        {
            int stones = Math.Max(7, (int)MathF.Round(MathF.PI * (radius + ring * 0.5f) / 0.6f) | 1);
            for (int i = 0; i < stones; i++)
            {
                float a0 = MathF.PI * i / stones + 0.004f, a1 = MathF.PI * (i + 1) / stones - 0.004f;
                bool key = i == stones / 2;
                float far = radius + ring + (key ? 0.18f : 0f);
                float proud = key ? -0.08f : 0f;
                Vector3 P(float a, float r, float back) => line.At(middle - MathF.Cos(a) * r, back, spring + MathF.Sin(a) * r);
                PlazaWalls.Lay(b, new[]
                {
                    P(a1, radius, proud), P(a0, radius, proud), P(a0, far, proud), P(a1, far, proud),
                    P(a1, radius, depth), P(a0, radius, depth), P(a0, far, depth), P(a1, far, depth),
                }, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork(i), spring + radius, 0.12f));
            }
        }

        // ---------------------------------------------------------------- The commander's box

        /// <summary>
        /// The commander's box: a tribune <paramref name="width"/> across and <paramref name="depth"/> deep, its front at z = <paramref name="depth"/> standing on a podium's cope, its floor at
        /// <paramref name="floor"/> above the arena's on a coursed base behind the podium, a parapet along its front with the Reich's mark in brass, columns carrying an entablature, and an oxblood
        /// canopy over it with brass at its edge. Its front looks to +z, over the floor, which nothing of it overhangs.
        /// </summary>
        public static void Tribune(MeshBuilder b, ArenaStoneKind stone, float width, float depth, float floor, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("tribune");
            float half = width * 0.5f;
            // A coursed base under its floor behind the podium, down into the tier it stands on, and the slab of the floor itself.
            for (int c = 0; c < 4; c++)
            {
                float y1 = floor - 0.45f - c * 0.55f, y0 = y1 - 0.55f;
                ArenaGates.Slabs(b, -half, half, -0.5f, depth - 1.6f, y0 + 0.003f, y1 - 0.003f, 1.4f, stone, random.Fork(900 + c), 0f);
            }
            ArenaGates.Slabs(b, -half - 0.2f, half + 0.2f, -0.5f, depth + 0.15f, floor - 0.45f, floor, 1.5f, stone, random.Fork("floor"), 0.14f);

            // The parapet along its front and sides, with a brass plate on its front carrying the mark.
            const float Parapet = 1.5f, Thick = 0.45f;
            Box(b, -half, half, floor, floor + Parapet, depth - Thick, depth, stone, random.Fork("front"));
            foreach (float side in new[] { -1f, 1f }) Box(b, side * half - (side > 0f ? Thick : 0f), side * half + (side > 0f ? 0f : Thick), floor, floor + Parapet, 0f, depth - Thick, stone, random.Fork(side > 0f ? "east" : "west"));
            Vector3 plate = new Vector3(0f, floor + Parapet * 0.55f, depth + 0.01f);
            b.Box(plate - Vector3.UnitZ * 0.02f, new Vector3(1.1f, 0.55f, 0.03f), ArenaStone.Oxblood(0.9f));
            ArenaStone.Mark(b, plate + Vector3.UnitZ * 0.02f, Vector3.UnitX, Vector3.UnitY, 1.4f, 0.03f, ArenaStone.Brass(random), ArenaStone.Oxblood(0.9f));

            // Four columns, an entablature over them, and the canopy, oxblood with brass fringes, sloping back to the podium.
            float top = floor + 6.2f;
            foreach (float x in new[] { -half + 0.6f, -half * 0.33f, half * 0.33f, half - 0.6f })
            {
                Column(b, new Vector3(x, floor, depth - 0.8f), top - floor, stone, random.Fork((int)(x * 10f) + 77));
                Column(b, new Vector3(x, floor, 0.4f), top - floor, stone, random.Fork((int)(x * 10f) + 99));
            }
            ArenaGates.Slabs(b, -half - 0.3f, half + 0.3f, 0f, depth - 0.3f, top, top + 0.55f, 1.5f, stone, random.Fork("entablature"), 0.16f);
            VertexMaterial cloth = ArenaStone.Oxblood(1.02f), under = ArenaStone.Oxblood(0.62f);
            float canopyFront = top + 0.65f, canopyBack = top + 1.6f;
            const int Strips = 8;
            for (int i = 0; i < Strips; i++)
            {
                float x0 = -half - 0.3f + (width + 0.6f) * i / Strips, x1 = -half - 0.3f + (width + 0.6f) * (i + 1) / Strips;
                float sag = 0.14f;
                Vector3 a = new Vector3(x0, canopyBack, -0.2f), c = new Vector3(x1, canopyBack, -0.2f);
                Vector3 d = new Vector3(x1, canopyFront, depth), e = new Vector3(x0, canopyFront, depth);
                Vector3 m0 = (a + e) * 0.5f - Vector3.UnitY * sag, m1 = (c + d) * 0.5f - Vector3.UnitY * sag;
                VertexMaterial strip = i % 2 == 0 ? cloth : ArenaStone.Oxblood(0.82f);
                ArenaWalls.TwoSided(b, a, c, m1, m0, strip, under);
                ArenaWalls.TwoSided(b, m0, m1, d, e, strip, under);
                // Its valance, hanging along the front.
                ArenaWalls.TwoSided(b, e, d, d - Vector3.UnitY * 0.5f, e - Vector3.UnitY * 0.5f, strip, under);
            }
            b.Box(new Vector3(0f, canopyFront - 0.5f, depth + 0.01f), new Vector3(half + 0.3f, 0.04f, 0.02f), ArenaStone.Brass(random));
        }

        private static void Box(MeshBuilder b, float x0, float x1, float y0, float y1, float z0, float z1, ArenaStoneKind stone, WorldRandom random)
        {
            int courses = Math.Max(1, (int)MathF.Round((y1 - y0 - 0.2f) / 0.5f));
            for (int c = 0; c < courses; c++)
            {
                float ya = y0 + (y1 - y0 - 0.2f) * c / courses, yb = y0 + (y1 - y0 - 0.2f) * (c + 1) / courses;
                ArenaGates.Slabs(b, x0, x1, z0, z1, ya + 0.003f, yb - 0.003f, 1.3f, stone, random.Fork(c), 0f);
            }
            ArenaGates.Slabs(b, x0 - 0.06f, x1 + 0.06f, z0 - 0.06f, z1 + 0.06f, y1 - 0.2f, y1, 1.5f, stone, random.Fork("cope"), 0.16f);
        }

        // A column: a square plinth, a shaft in drums, a capital and abacus.
        private static void Column(MeshBuilder b, Vector3 foot, float height, ArenaStoneKind stone, WorldRandom random)
        {
            ArenaStone.Block(b, foot + new Vector3(0f, 0.2f, 0f), new Vector3(0.38f, 0.2f, 0.38f), 0f, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random, foot.Y));
            int drums = 3;
            for (int d = 0; d < drums; d++)
            {
                float y0 = 0.4f + (height - 0.9f) * d / drums, y1 = 0.4f + (height - 0.9f) * (d + 1) / drums;
                b.Tube(foot + new Vector3(0f, y0 + 0.004f, 0f), foot + new Vector3(0f, y1 - 0.004f, 0f), 0.27f - d * 0.01f, 0.26f - d * 0.01f, 12, ArenaStone.Dressed(stone, random.Fork(d), foot.Y + y0, 0.035f), capped: false);
            }
            b.Tube(foot + new Vector3(0f, height - 0.5f, 0f), foot + new Vector3(0f, height - 0.25f, 0f), 0.25f, 0.4f, 12, ArenaStone.Dressed(stone, random.Fork("capital"), foot.Y + height));
            ArenaStone.Block(b, foot + new Vector3(0f, height - 0.12f, 0f), new Vector3(0.44f, 0.12f, 0.44f), 0f, 0.03f, ArenaStone.DressedFacing(stone, random.Fork("abacus"), foot.Y + height));
        }

        /// <summary>Where the bolt-throwers stand in a tribune, in its own frame: either side of its middle, a little back from its parapet.</summary>
        public static IEnumerable<Vector3> TribuneThrowers(float width, float depth, float floor) => new[] { new Vector3(-width * 0.25f, floor, depth - 1.6f), new Vector3(width * 0.25f, floor, depth - 1.6f) };

        // ---------------------------------------------------------------- Lifts

        /// <summary>
        /// The frame of a lift's hatch in the floor, <paramref name="size"/> square: timbers bound in iron round a shaft into the dark, flush with the sand, which the leaves cover when it's shut.
        /// </summary>
        public static void HatchFrame(MeshBuilder b, float size, uint seed)
        {
            var random = new WorldRandom(seed).Fork("hatch");
            float half = size * 0.5f, ground = ArenaStone.Ground;
            const float Rim = 0.36f;
            VertexMaterial iron = ArenaStone.Iron(random);
            for (int side = 0; side < 4; side++)
            {
                float yaw = side * MathF.PI * 0.5f;
                Vector3 centre = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw)) * (half + Rim * 0.5f);
                ArenaStone.Block(b, centre + Vector3.UnitY * (ground + 0.02f), new Vector3(half + Rim, 0.07f, Rim * 0.5f), yaw, 0.03f, ArenaStone.Timber(random.Fork(side), 0.7f));
                for (int s = -1; s <= 1; s += 2) ArenaStone.Block(b, centre + new Vector3(MathF.Cos(yaw), 0f, -MathF.Sin(yaw)) * s * (half * 0.6f) + Vector3.UnitY * (ground + 0.1f), new Vector3(0.08f, 0.02f, Rim * 0.55f), yaw, 0.01f, iron);
            }
            // The shaft: dark, just under where the leaves lie.
            b.Quad(new Vector3(-half, ground + 0.01f, half), new Vector3(half, ground + 0.01f, half), new Vector3(half, ground + 0.01f, -half), new Vector3(-half, ground + 0.01f, -half), ArenaStone.Dark);
        }

        /// <summary>One leaf of a lift's hatch, hinged along its edge at the origin and reaching along +x to the middle: planks under iron straps with a ring to lift it by.</summary>
        public static void HatchLeaf(MeshBuilder b, float size, uint seed)
        {
            var random = new WorldRandom(seed).Fork("leaf");
            float reach = size * 0.5f, half = size * 0.5f, ground = ArenaStone.Ground;
            int planks = 5;
            for (int p = 0; p < planks; p++)
            {
                float z0 = -half + size * p / planks + 0.01f, z1 = -half + size * (p + 1) / planks - 0.01f;
                ArenaStone.Block(b, new Vector3(reach * 0.5f, ground + 0.05f, (z0 + z1) * 0.5f), new Vector3(reach * 0.5f - 0.01f, 0.045f, (z1 - z0) * 0.5f), 0f, 0.012f, ArenaStone.Timber(random.Fork(p), random.Range(0.62f, 0.78f)));
            }
            VertexMaterial iron = ArenaStone.Iron(random);
            foreach (float z in new[] { -half * 0.6f, 0f, half * 0.6f }) b.Box(new Vector3(reach * 0.5f, ground + 0.1f, z), new Vector3(reach * 0.5f - 0.05f, 0.012f, 0.07f), iron);
            b.Tube(new Vector3(reach - 0.35f, ground + 0.11f, -0.16f), new Vector3(reach - 0.35f, ground + 0.11f, 0.16f), 0.03f, 0.03f, 6, iron);
        }

        // ---------------------------------------------------------------- The bolt-thrower

        /// <summary>Where a bolt-thrower's bolt points from, its iron head, which turns with it about its post at the origin.</summary>
        internal static readonly Vector3 Muzzle = new Vector3(0f, 1.6f, 2.55f);

        /// <summary>
        /// A bolt-thrower on its stand, facing +z: a timber frame on a turning post, two bow arms with their cord drawn, a bolt in its groove with an iron head, and the winch that draws it.
        /// </summary>
        public static void Ballista(MeshBuilder b, uint seed)
        {
            var random = new WorldRandom(seed).Fork("ballista");
            VertexMaterial wood = ArenaStone.Timber(random, 0.85f), dark = ArenaStone.Timber(random, 0.6f);
            VertexMaterial iron = ArenaStone.Iron(random), brass = ArenaStone.Brass(random);
            b.Tube(new Vector3(0f, 0f, 0f), new Vector3(0f, 1.2f, 0f), 0.22f, 0.18f, 8, dark);
            foreach (float a in new[] { 0f, MathF.Tau / 3f, MathF.Tau * 2f / 3f })
            {
                Vector3 d = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                b.Tube(d * 0.1f + Vector3.UnitY * 0.6f, d * 0.9f, 0.06f, 0.06f, 5, dark);
            }
            // The stock, along +z, with its groove.
            ArenaStone.Block(b, new Vector3(0f, 1.45f, 0.4f), new Vector3(0.16f, 0.12f, 1.5f), 0f, 0.02f, wood);
            // The head frame where the arms are held, with brass plates.
            ArenaStone.Block(b, new Vector3(0f, 1.55f, 1.55f), new Vector3(0.62f, 0.32f, 0.16f), 0f, 0.025f, wood);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new Vector3(s * 0.42f, 1.55f, 1.73f), new Vector3(0.12f, 0.34f, 0.02f), brass);
                Vector3 root = new Vector3(s * 0.55f, 1.55f, 1.5f), tip = new Vector3(s * 1.55f, 1.6f, 1.0f);
                b.Tube(root, tip, 0.08f, 0.05f, 6, wood);
                b.Tube(tip, new Vector3(0f, 1.5f, -0.2f), 0.015f, 0.015f, 4, ArenaStone.Leather(0.8f));
            }
            // The bolt, its iron head out past the frame, and the winch at the back.
            b.Tube(new Vector3(0f, Muzzle.Y, -0.3f), new Vector3(0f, Muzzle.Y, 2.1f), 0.04f, 0.04f, 5, wood);
            b.Tube(new Vector3(0f, Muzzle.Y, 2.1f), Muzzle, 0.09f, 0.0f, 4, iron);
            b.Tube(new Vector3(-0.3f, 1.4f, -0.9f), new Vector3(0.3f, 1.4f, -0.9f), 0.08f, 0.08f, 8, dark);
            foreach (float s in new[] { -1f, 1f }) b.Tube(new Vector3(s * 0.3f, 1.4f, -0.9f), new Vector3(s * 0.3f, 1.7f, -1.05f), 0.03f, 0.03f, 4, iron);
        }
    }
}
