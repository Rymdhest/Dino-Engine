using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// A watchtower of the land's (LORE.md: from here you can see the next fire, when they are lit; the beacon has been cold since the ships came): one of a chain of beacon towers, of
    /// the land's stone, its beacon's iron basket on its top heaped with wood that's never been lit, a hoist off its parapet to haul the wood up; the wood stacked at its foot under the
    /// Reich's tarp, roped down and sealed so nobody lights it; the stone the keepers sighted the next beacon by, and the horn they'd blow. The Reich's are its banner, its tarp, its
    /// paint over the land's mark and its seal. Model +z is a piece's front, y = 0 where people stand. Headless.
    /// </summary>
    internal static class Beacon
    {
        private static Vector3 StoneOfSpec(in PlaceSpec spec, int first) => spec.Count > first + 2 ? StoneColour(spec.Int(first), spec.Int(first + 1), spec.Int(first + 2)) : new Vector3(0.48f, 0.46f, 0.42f);

        /// <summary>Which way off the tower its hoist's rope comes down, in its own frame: what the builder stands the beacon's wood under.</summary>
        public static Vector3 HoistFoot(float radius) => new Vector3(radius + 2.2f, 0f, 0.4f);

        /// <summary>How far a tower of a radius reaches from its middle at its foot, which is where it's solid.</summary>
        public static float FootReach(float radius) => radius * 1.2f + 0.45f;

        /// <summary>
        /// The beacon tower: round, its foot battered out, or square with quoins at its corners; in coursed stone with string courses round it, its door at its foot with the keepers'
        /// mark carved over it and the Reich's paint across that, slits up it, corbels under a parapet of merlons, and in its top the beacon's iron basket on a post, heaped with wood
        /// that's never been lit. A hoist beam off the parapet with its rope down to where the wood is stacked, and the Reich's banner on a pole up there. Numbers: height, radius, form
        /// (0 round, 1 square), stone (r, g, b).
        /// </summary>
        public static void Tower(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float high = Math.Clamp(spec.Metres(0), 8f, 16f), radius = Math.Clamp(spec.Metres(1), 2f, 3.4f);
            bool square = spec.Int(2) == 1;
            Vector3 colour = StoneOfSpec(spec, 3);
            VertexMaterial wall = new VertexMaterial(MaterialCatalog.StoneWall.Index, new Colour(colour * 0.95f));
            VertexMaterial wallFoot = new VertexMaterial(MaterialCatalog.StoneWall.Index, new Colour(colour * new Vector3(0.78f, 0.82f, 0.74f)));
            Func<Vector3, VertexMaterial> dressed = n => n.Y > 0.6f ? Tint(Dressed(colour * 1.05f, random), new Vector3(0.84f, 0.95f, 0.72f)) : Dressed(colour * 1.05f, random, 3f);
            VertexMaterial wood = Timber(random, 0.6f), dark = Timber(random, 0.42f), iron = LandIron(random);
            float parapet = high - 1.4f;

            if (square)
            {
                float r = radius * 0.92f;
                // The shaft, battered at its foot, then straight; quoins up its corners in paler stone.
                Kit.Solid(b, new[]
                {
                    new Vector3(-r * 1.14f, -0.8f, r * 1.14f), new Vector3(r * 1.14f, -0.8f, r * 1.14f), new Vector3(r, 1.6f, r), new Vector3(-r, 1.6f, r),
                    new Vector3(-r * 1.14f, -0.8f, -r * 1.14f), new Vector3(r * 1.14f, -0.8f, -r * 1.14f), new Vector3(r, 1.6f, -r), new Vector3(-r, 1.6f, -r),
                }, 0.04f, wallFoot);
                Kit.Span(b, new Vector3(-r, 1.6f, -r), new Vector3(r, parapet, r), 0.05f, wall);
                for (float y = 1.6f; y < parapet - 0.3f; y += 0.62f)
                {
                    bool longer = ((int)(y / 0.62f)) % 2 == 0;
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        foreach (float sz in new[] { -1f, 1f })
                        {
                            Vector3 c = new Vector3(sx * r, y + 0.29f, sz * r);
                            Vector3 half = longer ? new Vector3(0.42f, 0.28f, 0.22f) : new Vector3(0.22f, 0.28f, 0.42f);
                            Kit.Block(b, c + new Vector3(sx * 0.03f - sx * half.X * 0.5f, 0f, sz * 0.03f - sz * half.Z * 0.5f), half, 0f, 0.025f, dressed);
                        }
                    }
                }
                foreach (float y in new[] { high * 0.38f, high * 0.7f }) Kit.Span(b, new Vector3(-r - 0.1f, y, -r - 0.1f), new Vector3(r + 0.1f, y + 0.22f, r + 0.1f), 0.03f, dressed);
                // Corbels under the parapet, and the parapet with its merlons.
                for (int side = 0; side < 4; side++)
                {
                    Vector3 n = side switch { 0 => Vector3.UnitZ, 1 => Vector3.UnitX, 2 => -Vector3.UnitZ, _ => -Vector3.UnitX };
                    Vector3 t = Vector3.Cross(Vector3.UnitY, n);
                    for (float s = -r + 0.35f; s < r; s += 0.7f) Kit.Box(b, n * (r + 0.18f) + t * s + Vector3.UnitY * (parapet - 0.3f), t, Vector3.UnitY, n, new Vector3(0.16f, 0.3f, 0.2f), 0.02f, dressed);
                }
                float pr = r + 0.35f;
                Kit.Span(b, new Vector3(-pr, parapet, -pr), new Vector3(pr, parapet + 0.25f, pr), 0.03f, dressed);
                for (int side = 0; side < 4; side++)
                {
                    Vector3 n = side switch { 0 => Vector3.UnitZ, 1 => Vector3.UnitX, 2 => -Vector3.UnitZ, _ => -Vector3.UnitX };
                    Vector3 t = Vector3.Cross(Vector3.UnitY, n);
                    Kit.Box(b, n * (pr - 0.2f) + Vector3.UnitY * (parapet + 0.6f), t, Vector3.UnitY, n, new Vector3(pr, 0.35f, 0.2f), 0.03f, wall);
                    for (float s = -pr + 0.3f; s < pr; s += 0.9f) Kit.Box(b, n * (pr - 0.2f) + t * s + Vector3.UnitY * (parapet + 1.2f), t, Vector3.UnitY, n, new Vector3(0.24f, 0.26f, 0.21f), 0.03f, dressed);
                }
                Kit.Face(b, new Vector3(-pr + 0.4f, parapet + 0.26f, pr - 0.4f), new Vector3(pr - 0.4f, parapet + 0.26f, pr - 0.4f), new Vector3(pr - 0.4f, parapet + 0.26f, -pr + 0.4f), new Vector3(-pr + 0.4f, parapet + 0.26f, -pr + 0.4f), Vector3.UnitY, Weathered(random, 0.75f));
                radius = r;
            }
            else
            {
                // Round: its foot battered out, coursed all the way up, string courses round it.
                Kit.Lathe(b, new Vector3(0f, -0.8f, 0f), Vector3.UnitY, 2.6f, Kit.Profile((0f, radius * 1.2f), (1f, radius)), 16, 2, wallFoot);
                Kit.Lathe(b, new Vector3(0f, 1.8f, 0f), Vector3.UnitY, parapet - 1.8f, Kit.Profile((0f, radius), (1f, radius * 0.96f)), 16, 4, wall);
                foreach (float y in new[] { high * 0.38f, high * 0.7f })
                {
                    float at = MathHelper.Lerp(radius, radius * 0.96f, (y - 1.8f) / (parapet - 1.8f));
                    Kit.Lathe(b, new Vector3(0f, y, 0f), Vector3.UnitY, 0.22f, Kit.Profile((0f, at + 0.1f), (1f, at + 0.1f)), 16, 1, Dressed(colour * 1.05f, random, 3f));
                }
                // Corbels under the parapet.
                const int Corbels = 16;
                float top = radius * 0.96f;
                for (int k = 0; k < Corbels; k++)
                {
                    float a = (k + 0.5f) * MathF.Tau / Corbels;
                    Vector3 n = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                    Kit.Box(b, n * (top + 0.16f) + Vector3.UnitY * (parapet - 0.3f), Vector3.Cross(Vector3.UnitY, n), Vector3.UnitY, n, new Vector3(0.16f, 0.3f, 0.2f), 0.02f, dressed);
                }
                // The parapet, a ring wall out over the corbels, and its merlons.
                float pr = top + 0.38f;
                Kit.Lathe(b, new Vector3(0f, parapet, 0f), Vector3.UnitY, 0.95f, Kit.Profile((0f, pr), (1f, pr)), 16, 1, wall);
                Kit.Lathe(b, new Vector3(0f, parapet, 0f), Vector3.UnitY, 0.3f, Kit.Profile((0f, pr + 0.06f), (1f, pr + 0.06f)), 16, 1, Dressed(colour * 1.05f, random, 3f));
                const int Merlons = 10;
                for (int k = 0; k < Merlons; k++)
                {
                    float a = k * MathF.Tau / Merlons;
                    Vector3 n = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                    Kit.Box(b, n * (pr - 0.18f) + Vector3.UnitY * (parapet + 1.22f), Vector3.Cross(Vector3.UnitY, n), Vector3.UnitY, n, new Vector3(0.36f, 0.28f, 0.2f), 0.03f, dressed);
                }
                // The platform's boards inside the parapet.
                Kit.Lathe(b, new Vector3(0f, parapet + 0.55f, 0f), Vector3.UnitY, 0.05f, Kit.Profile((0f, pr - 0.3f), (1f, pr - 0.3f)), 12, 1, Weathered(random, 0.75f));
            }

            // The door at its foot, in a porch of dressed stone out past the batter: its jambs and lintel, the door itself standing open, and over it on the wall the keepers' mark
            // with the Reich's paint across it.
            float front = (square ? radius * 1.14f : radius * 1.2f) + 0.25f;
            float doorTop = 2.3f;
            Kit.Span(b, new Vector3(-1.05f, -0.3f, front - 1.5f), new Vector3(1.05f, doorTop + 0.55f, front), 0.04f, dressed);
            Kit.Face(b, new Vector3(-0.6f, 0.05f, front + 0.01f), new Vector3(0.6f, 0.05f, front + 0.01f), new Vector3(0.6f, doorTop, front + 0.01f), new Vector3(-0.6f, doorTop, front + 0.01f), Vector3.UnitZ, Dark);
            foreach (float s in new[] { -1f, 1f }) Kit.Span(b, new Vector3(s * 0.6f - 0.18f, -0.1f, front - 0.05f), new Vector3(s * 0.6f + 0.18f, doorTop, front + 0.12f), 0.03f, dressed);
            Kit.Span(b, new Vector3(-0.9f, doorTop, front - 0.05f), new Vector3(0.9f, doorTop + 0.4f, front + 0.14f), 0.03f, dressed);
            Kit.Box(b, new Vector3(-0.55f + 0.52f * MathF.Cos(1.2f), doorTop * 0.5f + 0.05f, front + 0.06f + 0.52f * MathF.Sin(1.2f)), new Vector3(MathF.Cos(1.2f), 0f, MathF.Sin(1.2f)), Vector3.UnitY, new Vector3(-MathF.Sin(1.2f), 0f, MathF.Cos(1.2f)), new Vector3(0.53f, doorTop * 0.5f - 0.05f, 0.05f), 0.01f, wood);
            float wallAt = square ? radius : radius * (1f - 0.04f * (doorTop + 1.3f - 1.8f) / (parapet - 1.8f));
            Vector3 markAt = new Vector3(0f, doorTop + 1.35f, wallAt + 0.02f);
            Kit.Lathe(b, markAt - Vector3.UnitZ * 0.05f, Vector3.UnitZ, 0.08f, _ => 0.42f, 12, 1, Dressed(colour * 1.12f, random, 3f));
            for (int k = 0; k < 6; k++)
            {
                float a = k * MathF.Tau / 6f;
                Kit.Plank(b, markAt + Vector3.UnitZ * 0.04f, markAt + new Vector3(MathF.Cos(a) * 0.36f, MathF.Sin(a) * 0.36f, 0.04f), Vector3.UnitZ, 0.05f, 0.02f, Dressed(colour * 0.7f, random, 3f), 0f);
            }
            Kit.Plank(b, markAt + new Vector3(-0.55f, 0.45f, 0.07f), markAt + new Vector3(0.55f, -0.45f, 0.07f), Vector3.UnitZ, 0.16f, 0.01f, OxbloodPaint(), 0f);
            Kit.Plank(b, markAt + new Vector3(-0.55f, -0.45f, 0.07f), markAt + new Vector3(0.55f, 0.45f, 0.07f), Vector3.UnitZ, 0.14f, 0.01f, OxbloodPaint(0.92f), 0f);
            // Slits up it, turned about it so some face the camera.
            for (int k = 0; k < 4; k++)
            {
                float y = 3.8f + k * (parapet - 5f) / 4f;
                float a = MathF.PI * 0.5f + (k % 2 == 0 ? 0.55f : -0.7f) + random.Range(-0.15f, 0.15f);
                Vector3 n = square ? (k % 2 == 0 ? Vector3.UnitZ : Vector3.UnitX) : new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                float at = square ? radius + 0.01f : MathHelper.Lerp(radius, radius * 0.96f, (y - 1.8f) / (parapet - 1.8f)) + 0.01f;
                Vector3 t = Vector3.Cross(Vector3.UnitY, n);
                Kit.Face(b, n * at - t * 0.1f + Vector3.UnitY * y, n * at + t * 0.1f + Vector3.UnitY * y, n * at + t * 0.1f + Vector3.UnitY * (y + 0.9f), n * at - t * 0.1f + Vector3.UnitY * (y + 0.9f), n, Dark);
                Kit.Box(b, n * (at + 0.02f) + Vector3.UnitY * (y + 0.98f), t, Vector3.UnitY, n, new Vector3(0.3f, 0.09f, 0.08f), 0.01f, dressed);
            }

            // The beacon: an iron basket on a post in the middle of the platform, heaped with wood that's never been lit.
            // Big enough to read over the merlons from the camera, and its wood silver with the years it's waited.
            Vector3 basket = new Vector3(0f, parapet + 1.9f, 0f);
            Kit.Pole(b, new Vector3(0f, parapet + 0.4f, 0f), basket, 0.14f, iron);
            const float Basket = 1.3f;
            for (int k = 0; k < 10; k++)
            {
                float a = k * MathF.Tau / 10f;
                Kit.Pole(b, basket, basket + new Vector3(MathF.Cos(a) * Basket, 1.35f, MathF.Sin(a) * Basket), 0.045f, iron);
            }
            foreach (float y in new[] { 0.7f, 1.35f }) b.Tube(basket + Vector3.UnitY * (y - 0.035f), basket + Vector3.UnitY * (y + 0.035f), Basket * y / 1.35f + 0.04f, Basket * y / 1.35f + 0.04f, 12, iron, capped: false);
            VertexMaterial old = new VertexMaterial(MaterialCatalog.BarkBare.Index, new Colour(0.56f, 0.52f, 0.46f));
            Kit.Heap(b, basket + Vector3.UnitY * 0.45f, new Vector2(Basket * 0.6f), 0.5f, 0f, new VertexMaterial(MaterialCatalog.BarkBare.Index, new Colour(0.44f, 0.4f, 0.34f)), random, 3, 10);
            for (int k = 0; k < 14; k++)
            {
                float a = k * MathF.Tau / 14f + random.Range(-0.2f, 0.2f);
                Kit.Pole(b, basket + new Vector3(MathF.Cos(a) * Basket * 0.85f, 0.75f, MathF.Sin(a) * Basket * 0.85f), basket + new Vector3(MathF.Cos(a + 2.4f) * 0.25f, 2.1f, MathF.Sin(a + 2.4f) * 0.25f), 0.09f, old, 0.06f);
            }
            // The hoist: a beam out over the parapet, a block on it, and its rope down to the wood's stack.
            Vector3 hoistFoot = HoistFoot(radius);
            Vector3 beamEnd = new Vector3(hoistFoot.X, parapet + 1.2f, hoistFoot.Z);
            Kit.Plank(b, new Vector3(radius * 0.4f, parapet + 1.2f, hoistFoot.Z), beamEnd + new Vector3(0.3f, 0f, 0f), Vector3.UnitY, 0.2f, 0.22f, wood);
            Kit.Plank(b, new Vector3(radius * 0.95f, parapet + 0.4f, hoistFoot.Z), beamEnd - new Vector3(0.5f, 0.05f, 0f), Vector3.UnitZ, 0.14f, 0.14f, wood);
            Kit.Lathe(b, beamEnd - new Vector3(0f, 0.12f, 0.1f), Vector3.UnitZ, 0.2f, _ => 0.18f, 8, 1, dark);
            Kit.Pole(b, beamEnd - Vector3.UnitY * 0.2f, new Vector3(hoistFoot.X, 1.1f, hoistFoot.Z), 0.025f, Rope(random));
            Kit.Pole(b, beamEnd - new Vector3(0.18f, 0.2f, 0f), new Vector3(hoistFoot.X - 0.25f, 2.6f, hoistFoot.Z + 0.1f), 0.025f, Rope(random));
            // The Reich's banner up on the parapet, swallow-tailed, its mark on it.
            Vector3 pole = new Vector3(-(radius * 0.7f), parapet + 0.4f, -radius * 0.45f);
            float flag = random.Range(3.4f, 4.4f);
            VertexMaterial reichIron = Iron(random);
            Kit.Pole(b, pole, pole + Vector3.UnitY * flag, 0.06f, reichIron, 0.045f);
            Kit.Lathe(b, pole + Vector3.UnitY * flag, Vector3.UnitY, 0.35f, Kit.Profile((0f, 0.07f), (0.4f, 0.1f), (1f, 0f)), 6, 2, Brass(random));
            Vector3 t0 = pole + new Vector3(0.05f, flag - 0.2f, 0f), t1 = t0 + new Vector3(1.7f, -0.15f, 0.25f);
            Kit.Sheet(b, t0, t1, t1 - Vector3.UnitY * 1.1f, t0 - Vector3.UnitY * 1.25f, Oxblood(), Oxblood(0.75f));
            Kit.Mark(b, (t0 + t1) * 0.5f - Vector3.UnitY * 0.6f + new Vector3(-0.03f, 0f, 0.05f), Vector3.Normalize(t1 - t0), Vector3.UnitY, 0.55f, 0.008f, random);
        }

        /// <summary>
        /// The beacon's wood, stacked at the tower's foot to be hauled up when the next fire's seen: logs and bundles of sticks, barrels of pitch beside them, and over the most of it the
        /// Reich's tarp in oxblood, roped down to stakes, its seal on the rope, so that nobody lights it. Numbers: length.
        /// </summary>
        public static void Pyre(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 2.4f, 6f);
            float hx = length * 0.5f;
            VertexMaterial bark = Timber(random, 0.72f), twigs = new VertexMaterial(MaterialCatalog.BarkBare.Index, new Colour(0.36f, 0.3f, 0.24f));
            // Logs along it, ends to the front, in a long stack that rises in the middle.
            for (int row = 0; row < 4; row++)
            {
                float y = Ground + 0.2f + row * 0.34f;
                float reach = hx - row * 0.4f;
                for (float x = -reach + 0.2f; x < reach - 0.15f; x += 0.38f)
                {
                    Kit.Lathe(b, new Vector3(x + random.Range(-0.04f, 0.04f), y, -0.8f), Vector3.UnitZ, 1.6f + random.Range(-0.1f, 0.1f), _ => 0.17f, 6, 1, bark);
                    Kit.Lathe(b, new Vector3(x, y, 0.82f + random.Range(0f, 0.05f)), Vector3.UnitZ, 0.01f, _ => 0.15f, 6, 1, Fresh(random, 0.75f));
                }
            }
            // Bundles of sticks tied in the middle, leaning on its ends.
            foreach (float s in new[] { -1f, 1f })
            {
                for (int k = 0; k < 2; k++)
                {
                    Vector3 foot = new Vector3(s * (hx + 0.35f), Ground, -0.4f + k * 0.8f);
                    Vector3 axis = Vector3.Normalize(new Vector3(-s * 0.45f, 1f, 0f));
                    Kit.Lathe(b, foot, axis, 1.4f, Kit.Profile((0f, 0.26f), (0.5f, 0.2f), (1f, 0.3f)), 7, 2, twigs);
                    b.Tube(foot + axis * 0.66f, foot + axis * 0.74f, 0.22f, 0.22f, 7, Rope(random), capped: false);
                }
            }
            // Barrels of pitch, black at the head.
            for (int k = 0; k < 3; k++) Kit.Barrel(b, new Vector3(hx + 1.2f + (k % 2) * 0.9f, Ground - 0.04f, -0.6f + k * 0.65f), 0f, false, 0.38f, 1f, Timber(random, 0.6f), LandIron(random), Flat(new Vector3(0.04f, 0.035f, 0.03f)));
            // The Reich's tarp over most of it, roped down, its seal on the rope.
            float top = Ground + 1.5f;
            Kit.Cloth(b, new Vector3(-hx - 0.4f, top - 1.2f, 1.2f), new Vector3(hx * 0.4f, top - 1.1f, 1.25f), new Vector3(hx * 0.45f, top - 1.1f, -1.2f), new Vector3(-hx - 0.4f, top - 1.2f, -1.2f), -0.9f, 5, Oxblood(0.9f), Oxblood(0.5f), (u, v) => MathF.Sin(u * 9f + v * 4f) * 0.05f);
            foreach (float z in new[] { -0.9f, 0.9f })
            {
                Vector3 stakeA = new Vector3(-hx - 1.1f, Ground, z), stakeB = new Vector3(hx * 0.55f + 0.6f, Ground, z);
                Kit.Pole(b, stakeA - Vector3.UnitY * 0.2f, stakeA + Vector3.UnitY * 0.35f, 0.04f, Iron(random));
                Kit.Pole(b, stakeB - Vector3.UnitY * 0.2f, stakeB + Vector3.UnitY * 0.35f, 0.04f, Iron(random));
                Kit.Rope(b, stakeA + Vector3.UnitY * 0.3f, new Vector3(-hx * 0.2f, top + 0.05f, z), 0.02f, 0.02f, Rope(random), 3);
                Kit.Rope(b, new Vector3(-hx * 0.2f, top + 0.05f, z), stakeB + Vector3.UnitY * 0.3f, 0.02f, 0.02f, Rope(random), 3);
            }
            Kit.Lathe(b, new Vector3(-hx * 0.2f, top + 0.02f, 0.95f), Vector3.UnitZ, 0.04f, _ => 0.09f, 8, 1, OxbloodPaint(1.1f));
        }

        /// <summary>
        /// The stone the keepers sighted the next beacon by: a pillar of the land's stone with a ring of old bronze on a pin at its head and a notch cut for the eye, lines cut across its
        /// top towards the beacon it watches, which is along its +z. Numbers: stone (r, g, b).
        /// </summary>
        public static void SightStone(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            Vector3 colour = StoneOfSpec(spec, 0);
            Func<Vector3, VertexMaterial> stone = n => n.Y > 0.6f ? Tint(Dressed(colour, random), new Vector3(0.84f, 0.95f, 0.72f)) : Dressed(colour, random, 0.8f);
            Kit.Block(b, new Vector3(0f, -0.1f, 0f), new Vector3(0.45f, 0.3f, 0.45f), MathF.PI * 0.25f, 0.05f, stone);
            Kit.Lathe(b, new Vector3(0f, 0.15f, 0f), Vector3.UnitY, 1.15f, Kit.Profile((0f, 0.26f), (0.85f, 0.22f), (1f, 0.28f)), 8, 2, Dressed(colour * 1.04f, random, 1f));
            VertexMaterial bronze = new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(0.3f, 0.38f, 0.3f));
            Kit.Pole(b, new Vector3(0f, 1.3f, -0.15f), new Vector3(0f, 1.62f, -0.15f), 0.025f, bronze);
            const int Ring = 12;
            Vector3 c = new Vector3(0f, 1.72f, -0.15f);
            for (int k = 0; k < Ring; k++)
            {
                float a0 = k * MathF.Tau / Ring, a1 = (k + 1) * MathF.Tau / Ring;
                b.Tube(c + new Vector3(MathF.Cos(a0) * 0.12f, MathF.Sin(a0) * 0.12f, 0f), c + new Vector3(MathF.Cos(a1) * 0.12f, MathF.Sin(a1) * 0.12f, 0f), 0.016f, 0.016f, 4, bronze, capped: false);
            }
            // The notch at its front, and the lines cut across its top.
            Kit.Block(b, new Vector3(0f, 1.36f, 0.2f), new Vector3(0.06f, 0.06f, 0.05f), 0f, 0.01f, bronze);
            for (int k = -1; k <= 1; k++) Kit.Plank(b, new Vector3(k * 0.08f, 1.315f, -0.2f), new Vector3(k * 0.08f, 1.315f, 0.22f), Vector3.UnitY, 0.02f, 0.004f, Dark, 0f);
        }

        /// <summary>The beacon's horn, as long as a man, on its tripod, its mouth turned out to the land, to be blown when the fires were lit. Numbers: none.</summary>
        public static void Horn(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            VertexMaterial wood = Weathered(random, 0.85f), horn = new VertexMaterial(MaterialCatalog.Bone.Index, new Colour(0.5f, 0.42f, 0.3f)), band = new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(0.3f, 0.38f, 0.3f));
            Vector3 apex = new Vector3(0f, 1.55f, 0f);
            for (int k = 0; k < 3; k++)
            {
                float a = k * MathF.Tau / 3f + 0.5f;
                Kit.Pole(b, new Vector3(MathF.Cos(a) * 0.75f, -0.2f, MathF.Sin(a) * 0.75f), apex + new Vector3(MathF.Cos(a) * 0.05f, 0.08f, MathF.Sin(a) * 0.05f), 0.04f, wood);
            }
            // The horn itself: a long curve from the mouthpiece behind to the bell in front.
            var curve = new List<Vector3>();
            for (int k = 0; k <= 8; k++)
            {
                float t = k / 8f;
                curve.Add(apex + new Vector3(0f, 0.12f + MathF.Sin(t * MathF.PI) * 0.25f, -1.1f + t * 2.3f));
            }
            Kit.Bend(b, curve, 0.04f, 0.13f, horn);
            Kit.Lathe(b, curve[^1], Vector3.Normalize(curve[^1] - curve[^2]), 0.3f, Kit.Profile((0f, 0.13f), (1f, 0.26f)), 10, 2, horn, openEnd: true);
            foreach (int k in new[] { 2, 5 }) b.Tube(curve[k] - Vector3.UnitZ * 0.03f, curve[k] + Vector3.UnitZ * 0.03f, 0.06f + k * 0.012f, 0.06f + k * 0.012f, 8, band, capped: false);
        }
    }
}
