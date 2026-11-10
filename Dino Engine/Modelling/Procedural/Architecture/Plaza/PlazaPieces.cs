using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using Dino_Engine.Modelling.Procedural.Architecture.Waygates;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Plaza
{
    /// <summary>
    /// The stone pieces of a square that aren't walls: a flight of steps between cheek walls with piers and lanterns at its foot, a square pier, and an arched gateway. All of
    /// dressed stone (<see cref="PlazaStone"/>), every block touching its neighbours face to face. Model y = 0 is where people stand at the piece's foot, and the ground is drawn
    /// a little above that (<see cref="PlazaStone.Ground"/>). Headless.
    /// </summary>
    internal static class PlazaPieces
    {
        private const float Foot = -0.5f;

        // ---------------------------------------------------------------- Steps

        /// <summary>
        /// A flight rising towards -z from its bottom riser on z = 0, <paramref name="width"/> between its cheeks: each step a row of long blocks with its nosing a little proud and
        /// running back under the next, so nothing shows between them. The cheeks hold up whatever the flight climbs to and stand <paramref name="top"/> high, running on
        /// <paramref name="landing"/> behind the top step, and their ends at the foot stand out <paramref name="piers"/> in front as piers with a lantern each; their ends at the top carry
        /// a lantern too.
        /// </summary>
        public static void Steps(MeshBuilder b, float width, int count, float rise, float tread, float cheek, float top, float landing, float piers, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("steps");
            float half = width * 0.5f;
            float length = count * tread + landing;

            for (int i = 0; i < count; i++)
            {
                WorldRandom step = random.Fork(i);
                float front = -i * tread + 0.035f;
                float back = -(i + 1) * tread - (i == count - 1 ? 0.6f : 0.32f);
                float stepTop = PlazaStone.Ground + (i + 1) * rise;
                float bottom = PlazaStone.Ground + i * rise - 0.32f;
                float x = -half - 0.2f - step.Range(0f, 1.2f);
                while (x < half + 0.2f)
                {
                    float piece = step.Range(1.5f, 2.5f);
                    float x0 = MathF.Max(x, -half - 0.2f), x1 = MathF.Min(x + piece, half + 0.2f);
                    if (x1 - x0 > 0.3f)
                    {
                        Func<Vector3, VertexMaterial> facing = PlazaStone.DressedFacing(step, stepTop, 0.12f, 0.05f);
                        PlazaWalls.Lay(b, Corners(new Vector3(x0 + 0.006f, bottom, front), new Vector3(x1 - 0.006f, stepTop, back)), 0.05f, facing);
                    }
                    x += piece;
                }
            }

            // The cheeks: each a dressed wall facing the steps, holding up what the flight climbs to.
            float soil = count * rise;
            foreach (float side in new[] { -1f, 1f })
            {
                // Turned so the wall's face looks in at the steps: on the +x side it runs north from the top, on the -x side south from the foot.
                if (side > 0f) b.Place(new Vector3(half, 0f, -length), -MathF.PI * 0.5f);
                else b.Place(new Vector3(-half, 0f, 0f), MathF.PI * 0.5f);
                PlazaWalls.Build(b, WallLine.Straight(length), WallForm.Dressed, top, soil, cheek, seed + (side > 0f ? 11u : 23u));
                b.ResetPlacement();

                float middle = side * (half + cheek * 0.5f);
                // A little wider than the cheek, so the two never share a face where they meet.
                Pier(b, new Vector3(middle, 0f, (piers - 0.25f) * 0.5f), new Vector2(cheek + 0.1f, piers + 0.25f), top + 0.5f, random.Fork(side > 0f ? "east" : "west"));
                LanternHead(b, new Vector3(middle, top + 0.5f, (piers - 0.25f) * 0.5f), 1f, random.Fork(side > 0f ? "east lamp" : "west lamp"), lights);

                // A smaller lantern where the cheek ends at the top of the flight.
                float backZ = -length + cheek * 0.5f;
                PlazaStone.Block(b, new Vector3(middle, top + 0.11f, backZ), new Vector3(cheek * 0.36f, 0.11f, cheek * 0.36f), 0f, PlazaStone.Chamfer, PlazaStone.Dressed(random, top));
                LanternHead(b, new Vector3(middle, top + 0.22f, backZ), 0.8f, random.Fork(side > 0f ? "east top" : "west top"), lights);
            }
        }

        // ---------------------------------------------------------------- Piers

        /// <summary>A square pier standing on its own: a plinth, a shaft, a cap, and a lantern or an urn on top if it carries one.</summary>
        public static void PierPiece(MeshBuilder b, float width, float top, int crown, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("pier");
            Pier(b, Vector3.Zero, new Vector2(width), top, random);
            if (crown == 1) LanternHead(b, new Vector3(0f, top, 0f), 1f, random.Fork("lamp"), lights);
            if (crown == 2) Urn(b, new Vector3(0f, top, 0f), width * 0.36f, random.Fork("urn"));
        }

        /// <summary>A pier of <paramref name="size"/> (x by z) centred on <paramref name="at"/>, from below the ground to <paramref name="top"/>: plinth, coursed shaft and a moulded cap.</summary>
        public static void Pier(MeshBuilder b, Vector3 at, Vector2 size, float top, WorldRandom random)
        {
            const float Plinth = 0.5f, CapHigh = 0.26f, Proud = 0.08f;
            Vector2 half = size * 0.5f;
            PlazaStone.Block(b, new Vector3(at.X, (Foot + Plinth) * 0.5f, at.Z), new Vector3(half.X + Proud, (Plinth - Foot) * 0.5f, half.Y + Proud), 0f, PlazaStone.BigChamfer, PlazaStone.Dressed(random, 0f));
            int courses = Math.Max(1, (int)MathF.Round((top - CapHigh - Plinth) / 0.42f));
            float rise = (top - CapHigh - Plinth) / courses;
            for (int c = 0; c < courses; c++)
            {
                float y0 = Plinth + c * rise;
                // Courses alternate between one stone and two, as quoins are laid.
                if (c % 2 == 0 || size.X < 1f)
                {
                    PlazaStone.Block(b, new Vector3(at.X, y0 + rise * 0.5f, at.Z), new Vector3(half.X, rise * 0.5f - 0.004f, half.Y), 0f, PlazaStone.Chamfer, PlazaStone.Dressed(random, y0));
                }
                else
                {
                    foreach (float s in new[] { -1f, 1f })
                    {
                        PlazaStone.Block(b, new Vector3(at.X + s * half.X * 0.5f, y0 + rise * 0.5f, at.Z), new Vector3(half.X * 0.5f - 0.004f, rise * 0.5f - 0.004f, half.Y), 0f, PlazaStone.Chamfer, PlazaStone.Dressed(random, y0));
                    }
                }
            }
            float capBottom = top - CapHigh;
            PlazaStone.Block(b, new Vector3(at.X, capBottom + 0.05f, at.Z), new Vector3(half.X + 0.05f, 0.05f, half.Y + 0.05f), 0f, 0.025f, PlazaStone.Dressed(random, capBottom));
            PlazaStone.Block(b, new Vector3(at.X, capBottom + 0.1f + (CapHigh - 0.1f) * 0.5f, at.Z), new Vector3(half.X + Proud + 0.05f, (CapHigh - 0.1f) * 0.5f, half.Y + Proud + 0.05f), 0f, PlazaStone.BigChamfer,
                PlazaStone.DressedFacing(random, top, 0.35f));
        }

        /// <summary>
        /// A stone lantern standing on <paramref name="foot"/>: a base, a short post, a firebox with a post at each corner and its light inside, and a roof that sweeps out over it to a
        /// finial. It's the Haven's lantern, which lights every way in it.
        /// </summary>
        public static void LanternHead(MeshBuilder b, Vector3 foot, float scale, WorldRandom random, List<AssetLight> lights)
        {
            float s = scale;
            Func<Vector3, VertexMaterial> stone = PlazaStone.DressedFacing(random, 3f, 0.25f);
            PlazaStone.Block(b, foot + new Vector3(0f, 0.09f * s, 0f), new Vector3(0.38f, 0.09f, 0.38f) * s, 0f, 0.035f, stone);
            PlazaStone.Block(b, foot + new Vector3(0f, (0.18f + 0.24f) * s, 0f), new Vector3(0.2f, 0.24f, 0.2f) * s, 0f, 0.03f, stone);
            float boxBottom = 0.66f * s;
            PlazaStone.Block(b, foot + new Vector3(0f, boxBottom + 0.05f * s, 0f), new Vector3(0.36f, 0.05f, 0.36f) * s, 0f, 0.03f, stone);
            float fire = boxBottom + 0.1f * s, fireTop = fire + 0.5f * s;
            b.Box(foot + new Vector3(0f, (fire + fireTop) * 0.5f, 0f), new Vector3(0.22f, 0.25f, 0.22f) * s, PlazaStone.Glow(1f, 0.55f, 0.2f, 2.6f));
            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f })
                {
                    PlazaStone.Block(b, foot + new Vector3(x * 0.29f * s, (fire + fireTop) * 0.5f, z * 0.29f * s), new Vector3(0.06f * s, (fireTop - fire) * 0.5f, 0.06f * s), 0f, 0.015f, stone);
                }
            }
            PlazaStone.Block(b, foot + new Vector3(0f, fireTop + 0.06f * s, 0f), new Vector3(0.5f, 0.06f, 0.5f) * s, 0f, 0.035f, stone);
            b.Tube(foot + new Vector3(0f, fireTop + 0.12f * s, 0f), foot + new Vector3(0f, fireTop + 0.5f * s, 0f), 0.46f * s, 0.08f * s, 4, stone(Vector3.UnitY), capped: false);
            b.Tube(foot + new Vector3(0f, fireTop + 0.48f * s, 0f), foot + new Vector3(0f, fireTop + 0.64f * s, 0f), 0.09f * s, 0.02f * s, 6, stone(Vector3.UnitY));
            lights.Add(AssetLight.StoneLantern(foot + new Vector3(0f, (fire + fireTop) * 0.5f, 0f)));
        }

        /// <summary>A stone urn on <paramref name="foot"/>, of rim radius <paramref name="radius"/>, with soil in it for something to grow in (<see cref="UrnSoil"/>).</summary>
        public static void Urn(MeshBuilder b, Vector3 foot, float radius, WorldRandom random)
        {
            VertexMaterial stone = PlazaStone.Dressed(random, 3f);
            float r = radius;
            b.Tube(foot, foot + new Vector3(0f, 0.16f * r, 0f), 0.62f * r, 0.6f * r, 12, stone);
            b.Tube(foot + new Vector3(0f, 0.16f * r, 0f), foot + new Vector3(0f, 0.42f * r, 0f), 0.34f * r, 0.3f * r, 12, stone, capped: false);
            b.Tube(foot + new Vector3(0f, 0.42f * r, 0f), foot + new Vector3(0f, 0.98f * r, 0f), 0.42f * r, 0.94f * r, 12, stone, capped: false);
            b.Tube(foot + new Vector3(0f, 0.98f * r, 0f), foot + new Vector3(0f, 1.1f * r, 0f), 1.02f * r, 1.02f * r, 12, stone, capped: false);
            b.Tube(foot + new Vector3(0f, 1.0f * r, 0f), foot + new Vector3(0f, UrnSoil * r, 0f), 0.9f * r, 0.9f * r, 12, PlazaStone.Soil(0.8f));
        }

        /// <summary>How high an urn's soil is, for its rim radius.</summary>
        public const float UrnSoil = 1.04f;

        // ---------------------------------------------------------------- Arch

        /// <summary>
        /// A gateway over a street running along z: two piers on plinths, an arch of wedge-shaped stones on radial joints round a keystone, coursed spandrels cut to the arch's back,
        /// and a cornice with a lantern at each end. Its opening is <paramref name="span"/> across and springs at <paramref name="spring"/> above the ground.
        /// </summary>
        public static void Arch(MeshBuilder b, float span, float spring, float depth, float pier, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("arch");
            float half = span * 0.5f, outer = half + pier, z = depth * 0.5f;
            float springY = PlazaStone.Ground + spring;
            const float Ring = 0.72f;
            float radius = half, extrados = radius + Ring;
            float crownTop = springY + extrados;
            float cornice = crownTop + 0.55f;

            // The piers, each to the springing, with an impost on top.
            foreach (float side in new[] { -1f, 1f })
            {
                WorldRandom p = random.Fork(side > 0f ? "east" : "west");
                float x = side * (half + pier * 0.5f);
                PlazaStone.Block(b, new Vector3(x, (Foot + 0.55f) * 0.5f, 0f), new Vector3(pier * 0.5f + 0.08f, (0.55f - Foot) * 0.5f, z + 0.08f), 0f, PlazaStone.BigChamfer, PlazaStone.Dressed(p, 0f));
                int courses = Math.Max(1, (int)MathF.Round((springY - 0.22f - 0.55f) / 0.46f));
                float rise = (springY - 0.22f - 0.55f) / courses;
                for (int c = 0; c < courses; c++)
                {
                    float y0 = 0.55f + c * rise;
                    // Long and short in turn, so the joints never run up the pier.
                    float split = c % 2 == 0 ? 0.62f : 0.38f;
                    float a = x - pier * 0.5f, m = a + pier * split, e = x + pier * 0.5f;
                    PlazaWalls.Lay(b, Corners(new Vector3(a + 0.004f, y0 + 0.003f, z), new Vector3(m - 0.004f, y0 + rise - 0.003f, -z)), PlazaStone.Chamfer, PlazaStone.DressedFacing(p.Fork(c * 2), y0, 0.2f));
                    PlazaWalls.Lay(b, Corners(new Vector3(m + 0.004f, y0 + 0.003f, z), new Vector3(e - 0.004f, y0 + rise - 0.003f, -z)), PlazaStone.Chamfer, PlazaStone.DressedFacing(p.Fork(c * 2 + 1), y0, 0.2f));
                }
                PlazaStone.Block(b, new Vector3(x - side * 0.04f, springY - 0.11f, 0f), new Vector3(pier * 0.5f + 0.1f, 0.11f, z + 0.1f), 0f, PlazaStone.BigChamfer, PlazaStone.Dressed(p, springY));
            }

            // The arch: an odd number of voussoirs, so a keystone stands at the crown.
            int stones = Math.Max(7, (int)MathF.Round(MathF.PI * (radius + Ring * 0.5f) / 0.55f) | 1);
            for (int i = 0; i < stones; i++)
            {
                float a0 = MathF.PI * i / stones, a1 = MathF.PI * (i + 1) / stones;
                bool key = i == stones / 2;
                float proud = key ? 0.07f : 0f;
                float far = extrados + (key ? 0.16f : 0f);
                Vector3 P(float angle, float r, float zz) => new Vector3(-MathF.Cos(angle) * r, springY + MathF.Sin(angle) * r, zz);
                Vector3[] c =
                {
                    P(a1, radius, z + proud), P(a0, radius, z + proud), P(a0, far, z + proud), P(a1, far, z + proud),
                    P(a1, radius, -z - proud), P(a0, radius, -z - proud), P(a0, far, -z - proud), P(a1, far, -z - proud),
                };
                // Pulled in a hair from each joint, so neighbours touch only along their chamfers.
                Shrink(c, 0.004f);
                PlazaWalls.Lay(b, c, PlazaStone.Chamfer, PlazaStone.DressedFacing(random.Fork(100 + i), springY + radius, key ? 0.1f : 0.25f));
            }

            // Spandrels: courses from the springing up to the cornice, each cut to the back of the arch where it meets it, and set back a little from the arch's face so the
            // two never share a plane where a course's straight edge passes behind the curve.
            float zs = z - 0.05f;
            float y = springY;
            int course = 0;
            while (y < cornice - 0.05f)
            {
                float rise = MathF.Min(0.46f, cornice - y);
                float y1 = y + rise;
                foreach (float side in new[] { -1f, 1f })
                {
                    float Inner(float at) => at >= springY + extrados ? 0f : MathF.Sqrt(MathF.Max(extrados * extrados - (at - springY) * (at - springY), 0f));
                    float in0 = Inner(y), in1 = Inner(y1);
                    if (in0 >= outer - 0.05f && in1 >= outer - 0.05f) continue;
                    WorldRandom stone = random.Fork(200 + course * 2 + (side > 0f ? 1 : 0));
                    if (in0 <= 0.001f && in1 <= 0.001f)
                    {
                        // Above the crown, a course runs right across: lay it once, from the -x side, in two stones that meet off the middle.
                        if (side > 0f) continue;
                        float joint = (course % 2 == 0 ? 0.35f : -0.35f);
                        PlazaWalls.Lay(b, Corners(new Vector3(-outer, y + 0.003f, zs), new Vector3(joint - 0.004f, y1 - 0.003f, -zs)), PlazaStone.Chamfer, PlazaStone.DressedFacing(stone, y, 0.2f));
                        PlazaWalls.Lay(b, Corners(new Vector3(joint + 0.004f, y + 0.003f, zs), new Vector3(outer, y1 - 0.003f, -zs)), PlazaStone.Chamfer, PlazaStone.DressedFacing(stone.Fork("b"), y, 0.2f));
                        continue;
                    }
                    float x0 = side * MathF.Max(in0, 0.004f), x1 = side * MathF.Max(in1, 0.004f), xo = side * outer;
                    Vector3[] c =
                    {
                        new(x0 + side * 0.004f, y + 0.003f, zs), new(xo, y + 0.003f, zs), new(xo, y1 - 0.003f, zs), new(x1 + side * 0.004f, y1 - 0.003f, zs),
                        new(x0 + side * 0.004f, y + 0.003f, -zs), new(xo, y + 0.003f, -zs), new(xo, y1 - 0.003f, -zs), new(x1 + side * 0.004f, y1 - 0.003f, -zs),
                    };
                    PlazaWalls.Lay(b, c, PlazaStone.Chamfer, PlazaStone.DressedFacing(stone, y, 0.2f));
                }
                y = y1;
                course++;
            }

            // The cornice, proud all round, in long blocks.
            WorldRandom cornices = random.Fork("cornice");
            float corniceHalf = outer + 0.16f;
            for (float x = -corniceHalf; x < corniceHalf - 0.05f;)
            {
                float piece = MathF.Min(cornices.Range(1.1f, 1.6f), corniceHalf - x);
                if (corniceHalf - (x + piece) < 0.4f) piece = corniceHalf - x;
                PlazaWalls.Lay(b, Corners(new Vector3(x + 0.005f, cornice, z + 0.16f), new Vector3(x + piece - 0.005f, cornice + 0.22f, -z - 0.16f)), PlazaStone.BigChamfer,
                    PlazaStone.DressedFacing(cornices.Fork((int)(x * 100f) + 1000), cornice, 0.3f));
                x += piece;
            }
            Roof(b, outer + 0.5f, z + 0.62f, cornice + 0.22f, RoofRise, random.Fork("roof"));

            // A lantern hanging from the crown of the arch, under its keystone.
            float hang = springY + radius - 0.02f;
            b.Tube(new Vector3(0f, hang, 0f), new Vector3(0f, hang - 0.85f, 0f), 0.022f, 0.022f, 4, PlazaStone.DarkTimber(random));
            HangingLantern(b, new Vector3(0f, hang - 1.2f, 0f), random.Fork("lamp"), lights);
        }

        private const float RoofRise = 1.05f;

        /// <summary>How tall an arch stands to the ridge of its roof, for its span and the height it springs at.</summary>
        public static float ArchTop(float span, float spring) => PlazaStone.Ground + spring + span * 0.5f + 0.72f + 0.55f + 0.22f + RoofRise + 0.1f;

        /// <summary>
        /// A small gable roof of clay tiles over a wall's top, its ridge along x: two slopes from eaves <paramref name="halfDepth"/> out either side up to a ridge
        /// <paramref name="rise"/> above <paramref name="foot"/>, each a slab with an edge to it, a row of ridge tiles, and stone filling its gables.
        /// </summary>
        public static void Roof(MeshBuilder b, float halfLength, float halfDepth, float foot, float rise, WorldRandom random, bool gables = true)
        {
            const float Thick = 0.13f;
            VertexMaterial tiles = new VertexMaterial(Textures.MaterialCatalog.Shingle.Index, new Colour(new Vector3(0.66f, 0.36f, 0.24f) * random.Range(0.92f, 1.04f)));
            VertexMaterial ridge = new VertexMaterial(Textures.MaterialCatalog.Shingle.Index, new Colour(new Vector3(0.52f, 0.27f, 0.18f)));
            float ridgeY = foot + rise;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3[] c =
                {
                    new(-halfLength, foot, side * halfDepth), new(halfLength, foot, side * halfDepth), new(halfLength, ridgeY, side * 0.02f), new(-halfLength, ridgeY, side * 0.02f),
                    new(-halfLength, foot - Thick, side * halfDepth), new(halfLength, foot - Thick, side * halfDepth), new(halfLength, ridgeY - Thick, side * 0.02f), new(-halfLength, ridgeY - Thick, side * 0.02f),
                };
                PlazaWalls.Lay(b, c, 0.02f, _ => tiles);
            }
            b.Tube(new Vector3(-halfLength - 0.05f, ridgeY + 0.02f, 0f), new Vector3(halfLength + 0.05f, ridgeY + 0.02f, 0f), 0.13f, 0.13f, 8, ridge);
            if (!gables) return;
            // The gables, in the wall's stone, set in from the roof's ends.
            Func<Vector3, VertexMaterial> stone = PlazaStone.DressedFacing(random, foot, 0.2f);
            foreach (float end in new[] { -1f, 1f })
            {
                float x0 = end * (halfLength - 0.55f), x1 = end * (halfLength - 0.3f);
                float inset = halfDepth - 0.75f;
                Vector3[] g =
                {
                    new(MathF.Min(x0, x1), foot - 0.2f, inset), new(MathF.Max(x0, x1), foot - 0.2f, inset), new(MathF.Max(x0, x1), ridgeY - Thick - 0.02f, 0.03f), new(MathF.Min(x0, x1), ridgeY - Thick - 0.02f, 0.03f),
                    new(MathF.Min(x0, x1), foot - 0.2f, -inset), new(MathF.Max(x0, x1), foot - 0.2f, -inset), new(MathF.Max(x0, x1), ridgeY - Thick - 0.02f, -0.03f), new(MathF.Min(x0, x1), ridgeY - Thick - 0.02f, -0.03f),
                };
                PlazaWalls.Lay(b, g, 0.02f, stone);
            }
        }

        // A lantern on a cord: a wooden frame round its light, under a little roof.
        private static void HangingLantern(MeshBuilder b, Vector3 at, WorldRandom random, List<AssetLight> lights)
        {
            VertexMaterial wood = PlazaStone.DarkTimber(random);
            PlazaStone.Block(b, at + new Vector3(0f, 0.34f, 0f), new Vector3(0.26f, 0.04f, 0.26f), 0f, 0.015f, wood);
            b.Tube(at + new Vector3(0f, 0.38f, 0f), at + new Vector3(0f, 0.52f, 0f), 0.26f, 0.05f, 4, wood);
            b.Box(at + new Vector3(0f, 0.07f, 0f), new Vector3(0.18f, 0.26f, 0.18f), PlazaStone.Glow(1f, 0.58f, 0.22f, 2.4f));
            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float zz in new[] { -1f, 1f })
                {
                    b.Box(at + new Vector3(x * 0.2f, 0.07f, zz * 0.2f), new Vector3(0.028f, 0.28f, 0.028f), wood);
                }
            }
            PlazaStone.Block(b, at + new Vector3(0f, -0.22f, 0f), new Vector3(0.22f, 0.03f, 0.22f), 0f, 0.012f, wood);
            lights.Add(PlazaStone.HungLantern(at + new Vector3(0f, 0.07f, 0f)));
        }

        // ---------------------------------------------------------------- Well

        /// <summary>
        /// A well: a round wall of dressed stone with a broad cope round dark water, two oak posts on it carrying a windlass with its rope and bucket, and a little tiled roof
        /// over all of it.
        /// </summary>
        public static void Well(MeshBuilder b, float radius, uint seed)
        {
            var random = new WorldRandom(seed).Fork("well");
            const float Top = 1.4f, Thick = 0.5f;
            PlazaWalls.Build(b, WallLine.Arc(radius, 0f, 360f, 1f), WallForm.Seat, Top, -1f, Thick, seed);
            float inner = radius - Thick;
            b.Tube(new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0.55f, 0f), inner + 0.02f, inner + 0.02f, 20, PlazaStone.Glaze(new Vector3(0.025f, 0.04f, 0.05f)));
            VertexMaterial oak = PlazaStone.Timber(random, 0.85f);
            VertexMaterial dark = PlazaStone.DarkTimber(random);
            float post = radius - Thick * 0.5f, beam = 3.6f, roofFoot = 4.7f;
            foreach (float s in new[] { -1f, 1f })
            {
                PlazaWalls.Lay(b, Corners(new Vector3(s * post - 0.13f, Top - 0.05f, -0.13f), new Vector3(s * post + 0.13f, roofFoot, 0.13f)), 0.025f, _ => oak);
                PlazaWalls.Lay(b, Corners(new Vector3(s * post - 0.11f, roofFoot - 0.02f, -1.05f), new Vector3(s * post + 0.11f, roofFoot + 0.16f, 1.05f)), 0.02f, _ => oak);
            }
            b.Tube(new Vector3(-post + 0.13f, beam, 0f), new Vector3(post - 0.13f, beam, 0f), 0.12f, 0.12f, 8, oak);
            b.Tube(new Vector3(post + 0.13f, beam, 0f), new Vector3(post + 0.42f, beam, 0f), 0.05f, 0.05f, 6, dark);
            b.Tube(new Vector3(post + 0.42f, beam, 0f), new Vector3(post + 0.42f, beam - 0.5f, 0f), 0.045f, 0.045f, 6, dark);
            b.Tube(new Vector3(0.2f, beam - 0.1f, 0f), new Vector3(0.2f, 1.9f, 0f), 0.02f, 0.02f, 4, PlazaStone.Cloth(new Vector3(0.5f, 0.42f, 0.3f)));
            b.Tube(new Vector3(0.2f, 1.6f, 0f), new Vector3(0.2f, 1.95f, 0f), 0.2f, 0.24f, 10, oak);
            Roof(b, post + 0.55f, 1.35f, roofFoot + 0.16f, 0.95f, random.Fork("roof"), gables: false);
        }

        /// <summary>How far a well's wall reaches from its middle, as its footprint.</summary>
        public static float WellReach(float radius) => radius + 0.15f;

        // ---------------------------------------------------------------- Shared

        /// <summary>The corners of a block square to the axes, front (+z) four first.</summary>
        public static Vector3[] Corners(Vector3 a, Vector3 c)
        {
            Vector3 min = Vector3.ComponentMin(a, c), max = Vector3.ComponentMax(a, c);
            return new[]
            {
                new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z),
                new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, max.Y, min.Z), new Vector3(min.X, max.Y, min.Z),
            };
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
