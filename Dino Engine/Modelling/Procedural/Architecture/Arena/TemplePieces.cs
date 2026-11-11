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
    /// The Sun-Temple, the land's own before the Reich took it: colonnades of golden stone round its court, the stepped platform its great disc stands on, the disc itself in gold (the land's, which is
    /// why it isn't brass), the portals its bronze doors hang in and the doors' leaves, the brass mirrors the Reich's priests turn on the floor, and the rings inlaid in the court's paving. The disc has no
    /// rays and the paving no spokes, so that nothing of it can read as any real mark (LORE.md). Model y = 0 is where people stand at a piece's foot, and +z its front. Headless.
    /// </summary>
    internal static class TemplePieces
    {
        private const float Foot = -0.6f;
        public static readonly Vector3 Gold = new Vector3(0.78f, 0.56f, 0.17f);
        public static readonly Vector3 Bronze = new Vector3(0.34f, 0.22f, 0.11f);

        public static VertexMaterial GoldLeaf(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(Gold * shade * random.Range(0.94f, 1.05f)));

        // ---------------------------------------------------------------- Colonnades

        /// <summary>
        /// A colonnade along +x, <paramref name="length"/> long, its front row of columns on z = 0 and its back row <paramref name="depth"/> behind, carrying an architrave, a frieze and a cornice, and
        /// roofed in slabs between them; the Reich's banner hangs from its front between every other pair of columns.
        /// </summary>
        public static void Colonnade(MeshBuilder b, ArenaStoneKind stone, float length, float height, float depth, uint seed)
        {
            var random = new WorldRandom(seed).Fork("colonnade");
            int bays = Math.Max(1, (int)MathF.Round(length / 3.4f));
            float bay = length / bays;
            for (int k = 0; k <= bays; k++)
            {
                float x = k * bay;
                foreach (float z in new[] { 0f, -depth })
                {
                    Column(b, new Vector3(x, 0f, z), height, stone, random.Fork(k * 2 + (z < 0f ? 1 : 0)));
                }
            }
            var line = WallLine.Straight(length + 1.2f);
            b.Place(new Vector3(-0.6f, 0f, 0.55f), 0f);
            float y = height;
            ArenaWalls.Course(b, line, y, y + 0.55f, 0f, depth + 1.1f, 1.5f, random.Range(0f, 0.8f), random.Fork("architrave"), stone, 0.03f, 0.04f);
            ArenaWalls.Course(b, line, y + 0.55f, y + 1.1f, 0.06f, depth + 1.04f, 1.3f, random.Range(0f, 0.8f), random.Fork("frieze"), stone, ArenaStone.Chamfer, 0.05f);
            ArenaWalls.Course(b, line, y + 1.1f, y + 1.4f, -0.2f, depth + 1.3f, 1.45f, random.Range(0f, 0.8f), random.Fork("cornice"), stone, ArenaStone.BigChamfer, 0.04f, dust: 0.2f);
            b.ResetPlacement();
            // The roof between the rows, in slabs, and a low blocking course along its front.
            ArenaGates.Slabs(b, -0.6f, length + 0.6f, -depth - 0.75f, 0.55f + 0.2f, y + 1.4f, y + 1.62f, 1.5f, stone, random.Fork("roof"), 0.22f);
            for (int k = 0; k < bays; k++)
            {
                if (k % 2 != 0) continue;
                float middle = (k + 0.5f) * bay;
                ArenaWalls.HungBanner(b, new Vector3(middle, y - 0.05f, 0.2f), Vector3.UnitZ, MathF.Min(bay - 1.1f, 1.8f), height * 0.62f, random.Fork(100 + k));
            }
        }

        // A column of the temple's: a square plinth, a base, a fluted shaft in drums that swells a little, a capital and an abacus.
        private static void Column(MeshBuilder b, Vector3 foot, float height, ArenaStoneKind stone, WorldRandom random)
        {
            Func<Vector3, VertexMaterial> plinth = ArenaStone.DressedFacing(stone, random.Fork("plinth"), 0f);
            ArenaStone.Block(b, foot + new Vector3(0f, (Foot + 0.45f) * 0.5f, 0f), new Vector3(0.56f, (0.45f - Foot) * 0.5f, 0.56f), 0f, ArenaStone.BigChamfer, plinth);
            VertexMaterial face = ArenaStone.Dressed(stone, random, 1f, 0.03f);
            b.Tube(foot + new Vector3(0f, 0.45f, 0f), foot + new Vector3(0f, 0.6f, 0f), 0.5f, 0.46f, 16, face);
            b.Tube(foot + new Vector3(0f, 0.6f, 0f), foot + new Vector3(0f, 0.72f, 0f), 0.46f, 0.4f, 16, face, capped: false);
            int drums = Math.Max(3, (int)MathF.Round((height - 1.4f) / 1.3f));
            for (int d = 0; d < drums; d++)
            {
                float y0 = 0.72f + (height - 1.4f) * d / drums, y1 = 0.72f + (height - 1.4f) * (d + 1) / drums;
                float t0 = (float)d / drums, t1 = (float)(d + 1) / drums;
                float r0 = 0.38f * (1f + 0.04f * MathF.Sin(t0 * MathF.PI) - 0.1f * t0), r1 = 0.38f * (1f + 0.04f * MathF.Sin(t1 * MathF.PI) - 0.1f * t1);
                // Twenty flutes read as a fluted column from up here; more is only triangles.
                b.Tube(foot + new Vector3(0f, y0 + 0.004f, 0f), foot + new Vector3(0f, y1 - 0.004f, 0f), r0, r1, 20, ArenaStone.Dressed(stone, random.Fork(d), y0, 0.03f), capped: false);
            }
            float capital = height - 0.68f;
            b.Tube(foot + new Vector3(0f, capital, 0f), foot + new Vector3(0f, capital + 0.1f, 0f), 0.36f, 0.4f, 16, face, capped: false);
            b.Tube(foot + new Vector3(0f, capital + 0.1f, 0f), foot + new Vector3(0f, capital + 0.42f, 0f), 0.4f, 0.6f, 16, face, capped: false);
            ArenaStone.Block(b, foot + new Vector3(0f, height - 0.13f, 0f), new Vector3(0.64f, 0.13f, 0.64f), 0f, 0.03f, ArenaStone.DressedFacing(stone, random.Fork("abacus"), height, 0.12f));
        }

        // ---------------------------------------------------------------- The disc

        /// <summary>
        /// The land's sun, a disc of gold <paramref name="radius"/> across its face, its front towards +z, held up in a frame of golden stone: two pylons either side and a lintel over, the disc between
        /// them on a stone ring, its face raised in plain rings round a boss. No rays, no spokes: a sun the land made, and nothing else.
        /// </summary>
        public static void SunDisc(MeshBuilder b, ArenaStoneKind stone, float radius, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("disc");
            float middle = radius + 1.6f;
            float pylon = 1.6f;
            float outer = radius + 0.9f + pylon;
            // The pylons, coursed, on plinths, and the lintel over them.
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (radius + 0.9f + pylon * 0.5f);
                ArenaStone.Block(b, new Vector3(x, 0.3f, 0f), new Vector3(pylon * 0.5f + 0.18f, 0.5f, 1f), 0f, ArenaStone.BigChamfer, ArenaStone.DressedFacing(stone, random.Fork(side > 0f ? "pa" : "pb"), 0f));
                int courses = Math.Max(4, (int)MathF.Round((middle * 2f - 0.8f) / 0.7f));
                for (int c = 0; c < courses; c++)
                {
                    float y0 = 0.8f + (middle * 2f - 0.8f) * c / courses, y1 = 0.8f + (middle * 2f - 0.8f) * (c + 1) / courses;
                    float taper = 1f - 0.08f * c / courses;
                    ArenaStone.Block(b, new Vector3(x, (y0 + y1) * 0.5f, 0f), new Vector3(pylon * 0.5f * taper, (y1 - y0) * 0.5f - 0.004f, 0.8f * taper), 0f, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork(c * 2 + (side > 0f ? 1 : 0)), y0));
                }
            }
            float top = middle * 2f + 0.8f;
            ArenaGates.Slabs(b, -outer - 0.3f, outer + 0.3f, -1.1f, 1.1f, top - 0.8f, top - 0.3f, 1.5f, stone, random.Fork("lintel"), 0.14f);
            ArenaGates.Slabs(b, -outer - 0.5f, outer + 0.5f, -1.3f, 1.3f, top - 0.3f, top, 1.5f, stone, random.Fork("cornice"), 0.2f);

            // The stone ring the disc is set in, and the disc: a plain face of gold raised in rings round a boss, and its back in bronze.
            Vector3 centre = new Vector3(0f, middle, 0f);
            Ring(b, centre, radius + 0.92f, radius + 0.15f, -0.5f, 0.5f, ArenaStone.Dressed(stone, random.Fork("ring"), middle, 0.04f), 48);
            Ring(b, centre, radius + 0.15f, 0f, -0.25f, 0.2f, GoldLeaf(random, 0.95f), 48);
            Ring(b, centre, radius - 0.1f, radius - 0.5f, 0.2f, 0.32f, GoldLeaf(random, 1.08f), 48);
            Ring(b, centre, radius * 0.66f, radius * 0.6f, 0.2f, 0.3f, GoldLeaf(random, 1.05f), 40);
            Ring(b, centre, radius * 0.34f, 0f, 0.2f, 0.42f, GoldLeaf(random, 1.12f), 32);
            Ring(b, centre, radius + 0.15f, 0f, -0.36f, -0.25f, new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(Bronze)), 32);
            // Fires either side of it, in bowls on the lintel's ends, which make it glow at night.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 bowl = new Vector3(side * (radius + 0.9f + pylon * 0.5f), top, 0f);
                b.Tube(bowl, bowl + Vector3.UnitY * 0.5f, 0.45f, 0.75f, 12, GoldLeaf(random, 0.8f));
                b.Tube(bowl + Vector3.UnitY * 0.3f, bowl + Vector3.UnitY * 0.46f, 0.66f, 0.6f, 12, ArenaStone.Coals);
                lights.Add(AssetLight.Burning(bowl + Vector3.UnitY * 0.5f, 1f));
                lights.Add(ArenaStone.BrazierLight(bowl + Vector3.UnitY * 1.4f));
            }
        }

        /// <summary>How tall a disc's frame stands, for a disc of a radius.</summary>
        public static float DiscHeight(float radius) => (radius + 1.6f) * 2f + 0.8f;

        // A flat ring in the xy plane facing +z, from one radius to another, standing from z0 to z1.
        private static void Ring(MeshBuilder b, Vector3 centre, float outer, float inner, float z0, float z1, VertexMaterial material, int sides)
        {
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * MathF.Tau / sides, a1 = (k + 1) * MathF.Tau / sides;
                Vector3 o0 = centre + new Vector3(MathF.Cos(a0) * outer, MathF.Sin(a0) * outer, 0f), o1 = centre + new Vector3(MathF.Cos(a1) * outer, MathF.Sin(a1) * outer, 0f);
                Vector3 i0 = centre + new Vector3(MathF.Cos(a0) * inner, MathF.Sin(a0) * inner, 0f), i1 = centre + new Vector3(MathF.Cos(a1) * inner, MathF.Sin(a1) * inner, 0f);
                Vector3 front = Vector3.UnitZ * z1, back = Vector3.UnitZ * z0;
                b.Quad(i0 + front, o0 + front, o1 + front, i1 + front, material);
                // Its outer edge faces out and its inner edge in, each wound to be seen from that side.
                b.Quad(o1 + back, o1 + front, o0 + front, o0 + back, material);
                if (inner > 0.01f) b.Quad(i0 + back, i0 + front, i1 + front, i1 + back, material);
            }
        }

        // ---------------------------------------------------------------- Portals and their doors

        /// <summary>
        /// A portal in the court's wall, its face on z = 0 towards +z: jambs of long and short blocks either side of an opening <paramref name="width"/> across and <paramref name="height"/> high, a lintel
        /// over it with a disc of gold, a cornice, and a passage behind going dark. Its bronze doors are models of their own, which swing in.
        /// </summary>
        public static void Portal(MeshBuilder b, ArenaStoneKind stone, float width, float height, float depth, uint seed)
        {
            var random = new WorldRandom(seed).Fork("portal");
            float half = width * 0.5f, outer = half + 1.4f;
            foreach (float side in new[] { -1f, 1f })
            {
                int courses = Math.Max(3, (int)MathF.Round(height / 0.6f));
                for (int c = 0; c < courses; c++)
                {
                    float y0 = Foot + (height - Foot) * c / courses, y1 = Foot + (height - Foot) * (c + 1) / courses;
                    float split = c % 2 == 0 ? 0.4f : 0.65f;
                    float inner = side * half, m = side * (half + (outer - half) * split), o = side * outer;
                    Lay(b, inner, m, y0, y1, 0.25f, -depth, ArenaStone.DressedFacing(stone, random.Fork(c * 2 + (side > 0f ? 1 : 0)), y0));
                    Lay(b, m, o, y0, y1, 0.25f, -depth, ArenaStone.DressedFacing(stone, random.Fork(c * 2 + 50 + (side > 0f ? 1 : 0)), y0));
                }
            }
            // The lintel, in three stones, the middle one carrying the gold disc, then a cornice and blocking course.
            float l0 = height, l1 = height + 1.2f;
            Lay(b, -outer, -half * 0.35f, l0, l1, 0.3f, -depth, ArenaStone.DressedFacing(stone, random.Fork("la"), l0));
            Lay(b, -half * 0.35f, half * 0.35f, l0, l1, 0.38f, -depth, ArenaStone.DressedFacing(stone, random.Fork("lb"), l0));
            Lay(b, half * 0.35f, outer, l0, l1, 0.3f, -depth, ArenaStone.DressedFacing(stone, random.Fork("lc"), l0));
            Ring(b, new Vector3(0f, (l0 + l1) * 0.5f, 0.38f), 0.42f, 0f, 0f, 0.06f, GoldLeaf(random), 24);
            ArenaGates.Slabs(b, -outer - 0.25f, outer + 0.25f, -depth - 0.2f, 0.55f, l1, l1 + 0.32f, 1.5f, stone, random.Fork("cornice"), 0.18f);
            ArenaGates.Slabs(b, -outer, outer, -depth, 0.25f, l1 + 0.32f, l1 + 1.1f, 1.5f, stone, random.Fork("blocking"), 0.2f);
            // The passage, going dark, and its ceiling.
            b.Quad(new Vector3(-half, -0.3f, -depth + 0.4f), new Vector3(half, -0.3f, -depth + 0.4f), new Vector3(half, height, -depth + 0.4f), new Vector3(-half, height, -depth + 0.4f), ArenaStone.Dark);
            b.Quad(new Vector3(-half, height - 0.01f, 0.2f), new Vector3(-half, height - 0.01f, -depth + 0.4f), new Vector3(half, height - 0.01f, -depth + 0.4f), new Vector3(half, height - 0.01f, 0.2f), ArenaStone.Tint(ArenaStone.Hearting(stone), new Vector3(0.6f)));
        }

        /// <summary>One leaf of a pair of bronze doors, hinged on its edge at the origin and reaching along +x, <paramref name="width"/> across it and <paramref name="height"/> high: panels in raised frames, studs, and a ring.</summary>
        public static void DoorLeaf(MeshBuilder b, float width, float height, uint seed)
        {
            var random = new WorldRandom(seed).Fork("leaf");
            VertexMaterial bronze = new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(Bronze * random.Range(0.92f, 1.06f)));
            VertexMaterial dark = new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(Bronze * 0.62f));
            VertexMaterial gold = GoldLeaf(random, 0.9f);
            b.Box(new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(width * 0.5f - 0.02f, height * 0.5f, 0.09f), dark);
            int rows = 3;
            for (int r = 0; r < rows; r++)
            {
                float y0 = 0.25f + (height - 0.5f) * r / rows + 0.08f, y1 = 0.25f + (height - 0.5f) * (r + 1) / rows - 0.08f;
                foreach (float face in new[] { 1f, -1f })
                {
                    b.Box(new Vector3(width * 0.5f, (y0 + y1) * 0.5f, face * 0.11f), new Vector3(width * 0.5f - 0.18f, (y1 - y0) * 0.5f, 0.025f), bronze);
                    b.Box(new Vector3(width * 0.5f, (y0 + y1) * 0.5f, face * 0.14f), new Vector3(width * 0.5f - 0.36f, (y1 - y0) * 0.5f - 0.18f, 0.012f), dark);
                }
                for (int s = 0; s < 4; s++)
                {
                    float x = 0.3f + (width - 0.6f) * s / 3f;
                    foreach (float y in new[] { y0 + 0.05f, y1 - 0.05f }) b.Box(new Vector3(x, y, 0.15f), new Vector3(0.045f, 0.045f, 0.03f), gold);
                }
            }
            Vector3 ring = new Vector3(width - 0.4f, height * 0.48f, 0.16f);
            b.Tube(ring + new Vector3(0f, 0.1f, 0f), ring + new Vector3(0f, 0.1f, 0.06f), 0.12f, 0.12f, 10, gold);
            const int Links = 12;
            for (int k = 0; k < Links; k++)
            {
                float a0 = k * MathF.Tau / Links, a1 = (k + 1) * MathF.Tau / Links;
                b.Tube(ring + new Vector3(MathF.Sin(a0) * 0.22f, -0.12f - MathF.Cos(a0) * 0.22f + 0.22f, 0.1f), ring + new Vector3(MathF.Sin(a1) * 0.22f, -0.12f - MathF.Cos(a1) * 0.22f + 0.22f, 0.1f), 0.03f, 0.03f, 4, gold, capped: false);
            }
        }

        // ---------------------------------------------------------------- Mirrors

        /// <summary>The pedestal a mirror turns on: a stepped base and a squat shaft of golden stone with a ring of bronze at its head, which the mirror itself stands in.</summary>
        public static void MirrorPedestal(MeshBuilder b, ArenaStoneKind stone, uint seed)
        {
            var random = new WorldRandom(seed).Fork("pedestal");
            ArenaStone.Block(b, new Vector3(0f, -0.15f, 0f), new Vector3(1.1f, 0.45f, 1.1f), 0f, ArenaStone.BigChamfer, ArenaStone.DressedFacing(stone, random.Fork("a"), 0f));
            ArenaStone.Block(b, new Vector3(0f, 0.5f, 0f), new Vector3(0.85f, 0.2f, 0.85f), 0f, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork("b"), 0.4f));
            b.Tube(new Vector3(0f, 0.7f, 0f), new Vector3(0f, 2.1f, 0f), 0.6f, 0.52f, 12, ArenaStone.Dressed(stone, random.Fork("c"), 1f));
            b.Tube(new Vector3(0f, 2.1f, 0f), new Vector3(0f, 2.3f, 0f), 0.66f, 0.66f, 12, new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(Bronze)));
        }

        /// <summary>How high a pedestal's head is, where its mirror turns.</summary>
        public const float MirrorPivot = 2.3f;

        /// <summary>
        /// A mirror, which turns on its pedestal: a yoke of black iron and a great disc of polished brass in a riveted rim, tilted down towards +z, the floor, as the Reich's priests aim it. Its pivot is
        /// the origin.
        /// </summary>
        public static void Mirror(MeshBuilder b, float radius, uint seed)
        {
            var random = new WorldRandom(seed).Fork("mirror");
            VertexMaterial iron = ArenaStone.Iron(random), brass = ArenaStone.Brass(random);
            VertexMaterial face = new VertexMaterial(MaterialCatalog.Mirror.Index, new Colour(ArenaStone.BrassColour * 1.5f));
            b.Tube(Vector3.Zero, new Vector3(0f, 0.5f, 0f), 0.35f, 0.3f, 8, iron);
            float pivot = radius + 0.8f;
            foreach (float side in new[] { -1f, 1f })
            {
                b.Tube(new Vector3(0f, 0.45f, 0f), new Vector3(side * (radius + 0.3f), 0.7f, 0f), 0.12f, 0.12f, 5, iron);
                b.Tube(new Vector3(side * (radius + 0.3f), 0.7f, 0f), new Vector3(side * (radius + 0.3f), pivot, 0f), 0.12f, 0.1f, 5, iron);
            }
            // The disc, tilted forward and down a little, as it's aimed at the floor below.
            float tilt = MathHelper.DegreesToRadians(32f);
            Vector3 centre = new Vector3(0f, pivot, 0f);
            Vector3 right = Vector3.UnitX, up = new Vector3(0f, MathF.Cos(tilt), MathF.Sin(tilt)), normal = Vector3.Cross(right, up);
            const int Sides = 28;
            for (int k = 0; k < Sides; k++)
            {
                float a0 = k * MathF.Tau / Sides, a1 = (k + 1) * MathF.Tau / Sides;
                Vector3 p0 = centre + (right * MathF.Cos(a0) + up * MathF.Sin(a0)) * radius, p1 = centre + (right * MathF.Cos(a1) + up * MathF.Sin(a1)) * radius;
                b.Triangle(centre + normal * 0.08f, p0 + normal * 0.06f, p1 + normal * 0.06f, face);
                Vector3 r0 = centre + (right * MathF.Cos(a0) + up * MathF.Sin(a0)) * (radius + 0.22f), r1 = centre + (right * MathF.Cos(a1) + up * MathF.Sin(a1)) * (radius + 0.22f);
                b.Quad(p0 + normal * 0.1f, r0 + normal * 0.1f, r1 + normal * 0.1f, p1 + normal * 0.1f, brass);
                b.Quad(r1 - normal * 0.1f, r1 + normal * 0.1f, r0 + normal * 0.1f, r0 - normal * 0.1f, brass);
                b.Triangle(centre - normal * 0.1f, r1 - normal * 0.1f, r0 - normal * 0.1f, iron);
                if (k % 4 == 0) b.Box((p0 + r0) * 0.5f + normal * 0.13f, new Vector3(0.05f), iron);
            }
        }

        // ---------------------------------------------------------------- The court's inlay

        /// <summary>
        /// Rings inlaid in a court's paving round the origin, flush with it, as a sundial's are: bands of pale stone and one of gold, each a stride across. Plain rings, and nothing radiating from them.
        /// </summary>
        public static void Inlay(MeshBuilder b, ArenaStoneKind stone, float radius, uint seed)
        {
            var random = new WorldRandom(seed).Fork("inlay");
            float y = ArenaStone.Ground + 0.03f;
            foreach ((float r, float width, bool gold) in new[] { (radius, 0.7f, false), (radius - 1.4f, 0.32f, true), (radius * 0.45f, 0.6f, false), (radius * 0.45f - 0.9f, 0.25f, true) })
            {
                int stones = Math.Max(16, (int)MathF.Round(MathF.Tau * r / 1.1f));
                for (int k = 0; k < stones; k++)
                {
                    float a0 = k * MathF.Tau / stones + 0.004f, a1 = (k + 1) * MathF.Tau / stones - 0.004f;
                    VertexMaterial m = gold ? GoldLeaf(random.Fork(k), 0.85f) : ArenaStone.Tint(ArenaStone.Dressed(stone, random.Fork(k + 500), 0.5f, 0.04f), new Vector3(1.12f));
                    Vector3 P(float a, float rr, float yy) => new Vector3(MathF.Sin(a) * rr, yy, MathF.Cos(a) * rr);
                    PlazaWalls.Lay(b, new[]
                    {
                        P(a0, r + width * 0.5f, y - 0.3f), P(a1, r + width * 0.5f, y - 0.3f), P(a1, r + width * 0.5f, y), P(a0, r + width * 0.5f, y),
                        P(a0, r - width * 0.5f, y - 0.3f), P(a1, r - width * 0.5f, y - 0.3f), P(a1, r - width * 0.5f, y), P(a0, r - width * 0.5f, y),
                    }, 0.035f, _ => m);
                }
            }
        }

        private static void Lay(MeshBuilder b, float x0, float x1, float y0, float y1, float zFront, float zBack, Func<Vector3, VertexMaterial> facing)
        {
            PlazaWalls.Lay(b, PlazaPieces.Corners(new Vector3(MathF.Min(x0, x1) + 0.004f, y0 + 0.003f, MathF.Max(zFront, zBack)), new Vector3(MathF.Max(x0, x1) - 0.004f, y1 - 0.003f, MathF.Min(zFront, zBack))), ArenaStone.Chamfer, facing);
        }
    }
}
